using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoMod.RuntimeDetour;
using UnityEngine;
using System.Reflection;
using System.IO;
using RWCustom;

namespace DevConsole
{
    using Commands;
    using System.Diagnostics;

    /// <summary>
    /// Allows for interaction with and extension of the in-game console.
    /// </summary>
    public class GameConsole : MonoBehaviour
    {
        private const int consoleMargin = 10;  // Pixel margin between the border and the text
        private const int consoleHeight = 650; // Pixel height of the console's bounds
        private const int consoleWidth = 1000; // Pixel width of the console's bounds
        private const int lineHeight = 15;     // Pixel height of each line of output
        private const int maxHistory = 100;    // Maximum number of commands to store in history before clearing them
        private const int maxLines = (consoleHeight - 2 * consoleMargin) / lineHeight - 1; // Number of lines of output that can fit on the screen
        private const string startupCommandsFile = "devConsoleStartup.txt";                // File path to a list of commands to run on init

        private static readonly Color backColor = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color defaultTextColor = new Color(1f, 1f, 1f);

        private static GameConsole instance;        // The game's console instance
        private static List<IDetour> inputBlockers; // A list of detours that cause input to be ignored
        private static string currentFont = "devconsolas"; // Backing field for CurrentFont
        private static bool blockingInput = false;  // True while the input blockers are active
        private static Hook blockUpdateHook;        // A hook that pauses the game
        private static readonly List<CommandHandlerInfo> commands = new List<CommandHandlerInfo>();
        private static List<QueuedLine> queuedLines = new List<QueuedLine>(); // Lines sent before init or from another thread
        private static ForceOpenArgs forceOpen;

        private StringBuilder inputString = new StringBuilder();        // Stores the user's command line input
        private readonly Queue<LineInfo> lines = new Queue<LineInfo>(); // Stores the most recent output lines added
        private readonly List<string> history = new List<string>();     // Stores the most recent commands so they may be traversed
        private int indexInHistory;        // Which entry in history the user is viewing, or -1 if this command was written from scratch
        private bool initialized;          // True once the console has been created - it must wait for Futile to init
        private bool typing;               // True when input is redirected to the command line
        private bool silent;               // True when all logs to the console should be hidden
        private FContainer container;      // The container for all game console nodes
        private FContainer textContainer;  // The container for the console's text
        private FSprite background;        // The background rect of the game console
        private FLabel inputLabel;         // Displays the user's command line input
        private FSprite caret;             // The flashing sprite next to the input
        private float caretFlash;          // Timer for the caret flashing
        private Autocomplete autocomplete; // The autocomplete interface
        private DevConsoleMod mod;         // The parent mod

        /// <summary>
        /// Registers critical built-in commands.
        /// </summary>
        static GameConsole()
        {
            // Catches any commands that don't match any other and displays an error message
            RegisterCommand(new CatchAllCommand());

            // Displays the syntax of all registered commands
            new CommandBuilder("help")
                .Run(args =>
                {
                    int page;
                    if (args.Length == 0 || !int.TryParse(args[0], out page))
                        page = 0;
                    else
                        page = Math.Max(page - 1, 0);

                    var helps = commands
                        .Select(cmd =>
                        {
                            try { return cmd.Help(); }
                            catch { return null; }
                        })
                        .Where(help => help != null)
                        .Skip((maxLines - 1) * page)
                        .Take(maxLines - 1)
                        .ToArray();

                    Array.Sort(helps);
                    if (helps.Length > 0)
                    {
                        WriteLine($"Showing help for page {page + 1}. Run \"wiki\" for more detailed descriptions.", new Color(0.5f, 1f, 0.75f));
                        foreach (var help in helps)
                            WriteLine(help);
                    }
                    else
                    {
                        WriteLine($"Page {page} empty!", new Color(0.5f, 1f, 0.75f));
                    }
                })
                .Help("help [page: 1]")
                .Register();
        }

        /// <summary>
        /// An event called each time a line is written to the console.
        /// </summary>
        public static event Action<ConsoleLineEventArgs> OnLineWritten;

        /// <summary>
        /// Whether the in-game console is ready to open.
        /// Methods calls in <see cref="GameConsole"/>, unless otherwise specified, will wait for the console to be initialized to execute.
        /// </summary>
        public static bool Initialized => instance?.initialized ?? false;

        /// <summary>
        /// Enumerates all commands that are compatible with autocomplete.
        /// </summary>
        public static IEnumerable<IAutoCompletable> AutoCompletableCommands => commands.Select(cmd => cmd.inner as IAutoCompletable).Where(cmd => cmd != null);

        /// <summary>
        /// The number of lines of console output that fit on the console.
        /// If a command outputs lots of lines, consider breaking it up in pages of this size.
        /// </summary>
        public static int OutputLines => maxLines;

        /// <summary>
        /// The default text color to use for the console.
        /// </summary>
        public static Color DefaultColor => defaultTextColor;

        /// <summary>
        /// The color of the background sprite of the console including transparency.
        /// </summary>
        public static Color BackColor => backColor;

        /// <summary>
        /// The current default position. This can be set by the default_pos command.
        /// </summary>
        public static RoomPos TargetPos => InternalPositioning.Pos;

        /// <summary>
        /// Gets or sets the font used for the console.
        /// Changing the font will clear the console.
        /// </summary>
        public static string CurrentFont
        {
            get
            {
                // Fonts may load in at any time
                // Use "font" as a fallback if the current font isn't loaded
                if (Futile.atlasManager?._fontsByName.ContainsKey(currentFont) ?? false)
                    return currentFont;
                else
                    return "font";
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value), "Font name may not be null!");
                if (value == currentFont) return;

                currentFont = value;
                Clear();
            }
        }

        internal static void Apply(DevConsoleMod mod)
        {
            instance = new GameObject("Dev Console").AddComponent<GameConsole>();
            instance.mod = mod;
        }

        internal static void Undo()
        {
            BF.Undo();

            Futile.stage.RemoveChild(instance.container);

            Destroy(instance.gameObject);

            instance = null;
        }

        /// <summary>
        /// Writes one or more lines of white text to the console.
        /// </summary>
        /// <param name="text">The text to write.</param>
        public static void WriteLine(string text) => WriteLine(text, DefaultColor);

        /// <summary>
        /// Writes one or more lines of colored text to the console.
        /// </summary>
        /// <param name="text">The text to write.</param>
        /// <param name="color">The color of the text.</param>
        public static void WriteLine(string text, Color color)
        {
            if (text == null) text = "null";

            if (!Initialized)
            {
                if (!instance?.silent ?? true)
                {
                    lock (queuedLines)
                    {
                        queuedLines.Add(new QueuedLine() { color = color, text = text });
                    }
                }
                return;
            }

            var font = Futile.atlasManager.GetFontWithName(CurrentFont);
            foreach (string line in text.SplitLines().SelectMany(str => str.SplitLongLines(consoleWidth - consoleMargin * 2, font)))
            {
                instance.AddLine(line, color);
            }
        }

        /// <summary>
        /// <see cref="WriteLine(string)"/>, but thread safe. Use the other one when possible.
        /// </summary>
        /// <param name="text">The text to write.</param>
        public static void WriteLineThreaded(string text) => WriteLineThreaded(text, DefaultColor);

        /// <summary>
        /// <see cref="WriteLine(string, Color)"/>, but thread safe. Use the other one when possible.
        /// </summary>
        /// <param name="text">The text to write.</param>
        /// <param name="color">The color of the text.</param>
        public static void WriteLineThreaded(string text, Color color)
        {
            lock (queuedLines)
            {
                queuedLines.Add(new QueuedLine()
                {
                    text = text,
                    color = color
                });
                if (queuedLines.Count > maxLines)
                    queuedLines.RemoveAt(0);
            }
        }

        /// <summary>
        /// Force the console to open next frame.
        /// </summary>
        /// <param name="pause">Whether the game should pause while the console is open.</param>
        public static void ForceOpen(bool pause = false)
        {
            forceOpen = new ForceOpenArgs(pause);
        }

        /// <summary>
        /// Registers a command to be called when the user enters a line to the console.
        /// </summary>
        /// <param name="handler">The command handler to register.</param>
        /// <seealso cref="RemoveCommand(ICommandHandler)"/>
        public static void RegisterCommand(ICommandHandler handler)
        {
            if (!commands.Any(cmd => cmd.inner == handler))
                commands.Add(new CommandHandlerInfo(handler, new StackTrace()));
        }

        /// <summary>
        /// Unregisters a previously registered command.
        /// </summary>
        /// <param name="handler">The command handler to unregister.</param>
        /// <returns><c>true</c> if a command handler was found to remove, <c>false</c> otherwise.</returns>
        /// <seealso cref="RegisterCommand(ICommandHandler)"/>
        public static bool RemoveCommand(ICommandHandler handler)
        {
            return commands.RemoveAll(cmd => cmd.inner == handler) > 0;
        }

        /// <summary>
        /// Removes all lines of output from the console.
        /// </summary>
        public static void Clear()
        {
            lock (queuedLines)
            {
                queuedLines.Clear();
            }

            if (!Initialized) return;

            foreach (var line in instance.lines)
                line.label.RemoveFromContainer();
            instance.lines.Clear();

            // Make a new label with the new font
            var newLabel = new FLabel(CurrentFont, "")
            {
                anchorX = 0f,
                anchorY = 0f
            };
            instance.inputLabel.container.AddChild(newLabel);
            newLabel.MoveBehindOtherNode(instance.inputLabel);
            instance.inputLabel.RemoveFromContainer();
            instance.inputLabel = newLabel;
        }

        /// <summary>
        /// Prints the welcome message to the console.
        /// </summary>
        public static void WriteHeader()
        {
            WriteLine("Welcome to the dev console! Please enjoy your stay.", new Color(0.5f, 1f, 0.75f));
        }

        /// <summary>
        /// Runs a console command as if the user had input it.
        /// Calling this before the console has initialized may fail silently.
        /// </summary>
        /// <param name="command">The line of input, including command name and arguments.</param>
        /// <param name="echo"><c>true</c> to log this line to the console before running.</param>
        public static void RunCommand(string command, bool echo = false)
        {
            instance?.SubmitCommand(command, echo);
        }

        /// <summary>
        /// Like <see cref="RunCommand(string, bool)"/>, but all output from the command is suppressed.
        /// </summary>
        /// <param name="command">The command to run.</param>
        public static void RunCommandSilent(string command)
        {
            if (instance == null) return;

            bool wasSilent = instance.silent;
            instance.silent = true;
            try
            {
                RunCommand(command);
            }
            finally
            {
                instance.silent = wasSilent;
            }
        }

        // No construction allowed
        private GameConsole()
        {
        }

        private void Update()
        {
            if (!initialized)
            {
                if (Futile.instance == null || Futile.atlasManager == null) return;
                Initialize();
            }

            // Print out lines sent from another thread or before init
            lock (queuedLines)
            {
                if (queuedLines.Count > 0)
                {
                    foreach (var line in queuedLines)
                        WriteLine(line.text, line.color);
                    queuedLines.Clear();
                }
            }

            // Run bound commands
            Bindings.RunFrame();

            // Update target position
            InternalPositioning.Update();

            CaptureInput(false);

            bool skipInput = false;

            // Open and close the console
            if (!typing && (Input.GetKeyDown(KeyCode.BackQuote) || forceOpen != null))
            {
                typing = true;
                container.isVisible = true;
                skipInput = true;

                PauseGame(DevConsoleMod.autopause || (forceOpen?.pause ?? false));
            }
            else if (typing && (Input.GetKeyUp(KeyCode.Escape) || Input.GetKeyDown(KeyCode.BackQuote)))
            {
                forceOpen = null;
                typing = false;
                container.isVisible = false;

                PauseGame(false);
            }

            // Do input
            if (typing && !skipInput)
            {
                string input;
                // Allow pasting into the command line
                if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.V))
                    input = GetClipboard().Replace("\r\n", "\n");
                else
                    input = Input.inputString;

                bool inputChanged = false;
                foreach (var c in input)
                {
                    inputChanged = true;
                    switch (c)
                    {
                        // Remove one character when backspace is pressed
                        case '\b':
                            if (inputString.Length == 0) break;
                            inputString.Remove(inputString.Length - 1, 1);
                            break;

                        // If Ctrl+Backspace is entered, delete a whole word
                        case '\x7F':
                            if (inputString.Length == 0) break;
                            do inputString.Remove(inputString.Length - 1, 1);
                            while (inputString.Length > 0 && !char.IsWhiteSpace(inputString[inputString.Length - 1]));
                            break;

                        // Submit a command when enter is pressed
                        case '\r':
                        case '\n':

                            string command = inputString.ToString().Trim();

                            // Add command to history
                            if (history.Count >= maxHistory) history.RemoveAt(0);
                            if (command != "")
                                history.Add(command);
                            indexInHistory = -1;

                            // Execute
                            SubmitCommand(command);
                            inputString = new StringBuilder();

                            break;

                        // Otherwise, add to the current input
                        default:
                            inputString.Append(c);
                            break;
                    }
                }

                bool allowACScroll = true;

                if(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    allowACScroll = false;
                    
                    // Allow scrolling through history
                    if (history.Count > 0)
                    {
                        bool moved = false;
                        if (Input.GetKeyDown(KeyCode.UpArrow))
                        {
                            if (indexInHistory > 0)
                                indexInHistory--;
                            else
                                indexInHistory = history.Count - 1;
                            moved = true;
                        }
                        else if (Input.GetKeyDown(KeyCode.DownArrow))
                        {
                            indexInHistory = (indexInHistory + 1) % history.Count;
                            moved = true;
                        }

                        if (moved)
                        {
                            if (indexInHistory >= history.Count)
                                indexInHistory = history.Count - 1;

                            if (indexInHistory >= 0)
                                inputString = new StringBuilder(history[indexInHistory]);
                            else
                                inputString = new StringBuilder();
                            inputChanged = true;
                        }
                    }
                }

                if (inputChanged)
                {
                    caretFlash = 0f;
                    autocomplete.UpdateText(inputString.ToString());
                }

                autocomplete.Update(inputString, allowACScroll);
                
                // Disallow inputs for the rest of the frame
                CaptureInput(true);
            }

            // Draw console
            if (container.isVisible)
            {
                // Center console
                container.x = Mathf.Floor(Futile.screen.halfWidth) - consoleWidth / 2 + 0.1f;
                container.y = Mathf.Floor(Futile.screen.halfHeight) - consoleHeight / 2 + 0.1f;

                // Position labels
                int y = consoleMargin;
                inputLabel.x = consoleMargin;
                inputLabel.y = y;
                string str = inputString.ToString();
                inputLabel.text = " > " + str.Substring(0, Math.Min(str.Length, 1000));
                y += lineHeight;

                foreach (var line in lines.Reverse())
                {
                    line.label.x = consoleMargin;
                    line.label.y = y;
                    y += lineHeight;
                }

                // Draw caret
                caret.SetPosition(inputLabel.LocalToOther(new Vector2(inputLabel.textRect.xMax + 1f, inputLabel.textRect.yMin + 1f), caret.container));
                caret.isVisible = caretFlash < 0.5f;
                caretFlash += Time.unscaledDeltaTime;
                if (caretFlash >= 1f)
                    caretFlash %= 1f;

                container.MoveToFront();

                autocomplete.Container.SetPosition(inputLabel.LocalToOther(new Vector2(inputLabel.textRect.xMax + 1f, inputLabel.textRect.yMin), autocomplete.Container.container));
                
                autocomplete.Container.isVisible = true;
            }
        }

        private static readonly PropertyInfo GUIUtility_systemCopyBuffer = typeof(GUIUtility).GetProperty("systemCopyBuffer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        private static string GetClipboard()
        {
            return (string)(GUIUtility_systemCopyBuffer == null ? "" : GUIUtility_systemCopyBuffer.GetValue(null, null));
        }
        private static void SetClipboard(string text)
        {
            GUIUtility_systemCopyBuffer?.SetValue(null, text, null);
        }

        private void Initialize()
        {
            initialized = true;

            CustomFonts.Load();
            container = new FContainer();
            textContainer = new FContainer();
            autocomplete = new Autocomplete();

            background = new FSprite("pixel")
            {
                anchorX = 0f,
                anchorY = 0f,
                scaleX = consoleWidth,
                scaleY = consoleHeight,
                color = BackColor
            };
            inputLabel = new FLabel(CurrentFont, "")
            {
                anchorX = 0f,
                anchorY = 0f
            };
            caret = new FSprite("pixel")
            {
                scaleX = 6f,
                scaleY = 1f,
                anchorX = 0f,
                anchorY = 1f
            };

            container.AddChild(background);
            container.AddChild(textContainer);
            container.AddChild(autocomplete.Container);
            container.AddChild(inputLabel);
            container.AddChild(caret);

            container.isVisible = false;
            Futile.stage.AddChild(container);

            WriteHeader();

            BuiltInCommands.RegisterCommands();

            // Very important
            Aliases.SetAlias("slug", "echo \"jjjjjjjjjjjjjjjjjjjjg1                                     .wjjjjjjjjjjjjjjjjjjjjjjjjj\" \"jir.            1lBF:                                     1ljjh,            ,Lljjjjj\" \"ji;,            .7Bjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj2:.           .1ljjjjj\" \"jl1.                .zBjjjjjj4:                     ;IBjR:.                :Mjjjj\" \"jjjjjjE;.                                                                  .7Bjjjjjjjjj\" \"  1ljjjjjj81                                                             ,sBd;.     \" \"      .Cjjjf;.     .wjjlc.                          ,4BP:.     .YjjjjjjQ;.      \" \" .:JBjjjjH:..1ljjjjjjjjjjjj0;.               .@jjjjjjjjjjjR;,.:HjjjjjQ;,.     \" \" ,Wjf;.      ,rBjjjjjjjjjjj0;.               .7BjjjjjjjjjjM:      .7lD1.     \" \" ,7lW,           .YB@.                         .1Bjlc.           .cgh;,     \" \" ,YlW:.                         ,rBjjjjjjD:.                         ,LlM;,     \" \" ,YlW:.                                                                 ,VBi1.     \" \" ,YlW:.                                                                 ,VBi1.     \" \" ,YlW:.                                                                 ,VBi1.     \" \" ,wM;.                                                                 .:hM;,      \" \" ,YlM;.                                                                 ,7lM;,     \"");
            
            RunStartupCommands();
        }

        private void RunStartupCommands()
        {
            try
            {
                string[] lines = File.ReadAllLines(Path.Combine(Custom.RootFolderDirectory(), startupCommandsFile.Replace('\\', Path.DirectorySeparatorChar)));
                foreach (var line in lines)
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.StartsWith("//")) continue;
                    RunCommand(line);
                }
            }
            catch {}
        }

        private void LateUpdate()
        {
            if (container.isVisible)
                container.MoveToFront();
        }

        private void PauseGame(bool shouldPause)
        {
            static void BlockUpdate(On.RainWorld.orig_Update orig, RainWorld self) { }

            if (shouldPause && blockUpdateHook == null)
            {
                blockUpdateHook = new Hook(
                    typeof(RainWorld).GetMethod(nameof(RainWorld.Update), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    (On.RainWorld.hook_Update)BlockUpdate
                );
            }
            else if(!shouldPause && blockUpdateHook != null)
            {
                blockUpdateHook.Dispose();
                blockUpdateHook = null;
            }
        }

        private void SubmitCommand(string command, bool echo = true)
        {
            if (echo) WriteLine(" > " + command, new Color(0.7f, 0.7f, 0.7f));

            string[] args = command.SplitCommandLine().ToArray();
            if (args.Length > 0)
            {
                // Check all aliases
                if (Aliases.RunAlias(args)) return;

                // Check all commands
                for (int i = commands.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        if (commands[i].RunCommand(args, command)) return;
                    }
                    catch(Exception runException)
                    {
                        // Log a short description of what went wrong to the console, and an in-depth one to the console log
                        static string Indent(string str) => "  " + str.Replace(Environment.NewLine, Environment.NewLine + "    ");

                        WriteLine($"Failed to execute command from {commands[i].Registrant}!\nSee consoleLog.txt for more information.", Color.red);

                        string helpText;
                        try
                        {
                            helpText = commands[i].Help();
                        }
                        catch(Exception helpException)
                        {
                            helpText = helpException.ToString();
                        }

                        UnityEngine.Debug.Log("Failed to execute console command!");
                        UnityEngine.Debug.Log(Indent($"Command type: {commands[i].inner?.GetType().FullName ?? "NULL"}"));
                        UnityEngine.Debug.Log(Indent($"Help text: {helpText}"));
                        UnityEngine.Debug.Log(Indent($"Exception: {runException}"));
                        UnityEngine.Debug.Log(Indent($"Registered here: {commands[i].registerTrace}"));
                    }
                }
            }
        }

        // Add a single line
        private void AddLine(string text, Color color)
        {
            if (silent) return;

            OnLineWritten?.Invoke(new ConsoleLineEventArgs(text, color));

            LineInfo line;
            if (lines.Count < maxLines)
            {
                line = new LineInfo();
                line.label = new FLabel(CurrentFont, "")
                {
                    anchorX = 0f,
                    anchorY = 0f,
                    color = color
                };
                textContainer.AddChild(line.label);
            }
            else
                line = lines.Dequeue();

            line.label.color = color;
            line.label.text = text;
            lines.Enqueue(line);
        }

        // Blocks input from reaching other listeners
        private static void CaptureInput(bool shouldCapture)
        {
            if (shouldCapture && !blockingInput)
            {
                blockingInput = true;
                if (inputBlockers == null)
                {
                    var input = typeof(Input);
                    var self = typeof(GameConsole);

                    Hook MakeHook(string method, params Type[] types)
                    {
                        Type[] toTypes = new Type[types.Length + 1];
                        types.CopyTo(toTypes, 1);
                        toTypes[0] = (types[0] == typeof(KeyCode)) ? typeof(Func<KeyCode, bool>) : typeof(Func<string, bool>);
                        return new Hook(
                            input.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, types, null),
                            self.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, toTypes, null)
                        );
                    }

                    inputBlockers = new List<IDetour>() {
                        MakeHook(nameof(GetKey), typeof(string)),
                        MakeHook(nameof(GetKey), typeof(KeyCode)),
                        MakeHook(nameof(GetKeyDown), typeof(string)),
                        MakeHook(nameof(GetKeyDown), typeof(KeyCode)),
                        MakeHook(nameof(GetKeyUp), typeof(string)),
                        MakeHook(nameof(GetKeyUp), typeof(KeyCode))
                    };
                }
                else
                {
                    //foreach (var blocker in inputBlockers)
                    //    blocker.Apply();
                }
            }
            else if (!shouldCapture && blockingInput)
            {
                blockingInput = false;
                //foreach (var blocker in inputBlockers)
                //    blocker.Undo();
            }
        }

        private static bool GetKey(Func<string, bool> orig, string name) => blockingInput ? false : orig(name);
        private static bool GetKey(Func<KeyCode, bool> orig, KeyCode code) => blockingInput ? false : orig(code);
        private static bool GetKeyDown(Func<string, bool> orig, string name) => blockingInput ? false : orig(name);
        private static bool GetKeyDown(Func<KeyCode, bool> orig, KeyCode code) => blockingInput ? false : orig(code);
        private static bool GetKeyUp(Func<string, bool> orig, string name) => blockingInput ? false : orig(name);
        private static bool GetKeyUp(Func<KeyCode, bool> orig, KeyCode code) => blockingInput ? false : orig(code);

        /// <summary>
        /// Holds information about a line of console text.
        /// This is used with the <see cref="OnLineWritten"/> event.
        /// </summary>
        public class ConsoleLineEventArgs : EventArgs
        {
            /// <summary>
            /// The line of text written to the console.
            /// </summary>
            /// <remarks>
            /// One call to <see cref="WriteLine(string)"/> doesn't necessarily only produce one line of text,
            /// since it will be split at line breaks or in very long lines.
            /// </remarks>
            public string Text { get; }

            /// <summary>
            /// The color of this line of text.
            /// </summary>
            public Color TextColor { get; }

            internal ConsoleLineEventArgs(string text, Color color)
            {
                Text = text;
                TextColor = color;
            }
        }

        // Info about a specific line of output
        private class LineInfo
        {
            public FLabel label;
        }

        // Info about a line that was submitted before initialization
        private class QueuedLine
        {
            public Color color;
            public string text;
        }

        // Info about how the console should act when forced open
        private class ForceOpenArgs
        {
            public bool pause;

            public ForceOpenArgs(bool pause)
            {
                this.pause = pause;
            }
        }

        // Info about a command, used for debugging
        private class CommandHandlerInfo : ICommandHandler
        {
            // The actual command hander
            public ICommandHandler inner;

            // A stack trace taken when the command was registered
            public StackTrace registerTrace;

            // A best guess for the name of the assembly that registered the command
            public string Registrant
            {
                get
                {
                    // Some modloaders mess with the assembly names, so a type name is good enough
                    // The full stack trace will be logged to console anyway
                    for (int i = 1; i < registerTrace.FrameCount; i++)
                    {
                        var method = registerTrace.GetFrame(i).GetMethod();
                        if (method.DeclaringType == typeof(CommandBuilder)) continue;
                        return $"{method.Name} in {method.DeclaringType.Name}";
                    }
                    return "Unknown";
                }
            }

            public CommandHandlerInfo(ICommandHandler inner, StackTrace registerTrace)
            {
                this.inner = inner;
                this.registerTrace = registerTrace;
            }

            public string Help() => inner.Help();

            public bool RunCommand(string[] args, string rawCommand) => inner.RunCommand(args, rawCommand);
        }

        // The very final command, always runs if no other command triggers
        private class CatchAllCommand : ICommandHandler
        {
            public bool RunCommand(string[] args, string rawCommand)
            {
                WriteLine("Command not found! Try typing 'help'.");
                return true;
            }

            public string Help() => null;
        }
    }
}
