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
        private const int maxLines = (consoleHeight - 2 * consoleMargin) / lineHeight - 1; // Number of lines of output that can fit on the screen
        private const string startupCommandsFile = "devConsoleStartup.txt";                // File path to a list of commands to run on init

        private static GameConsole instance;        // The game's console instance
        private static List<IDetour> inputBlockers; // A list of detours that cause input to be ignored
        private static bool blockingInput = false;  // True while the input blockers are active
        private static bool isGamePaused = false;   // True while the game is paused
        private static readonly List<CommandHandlerInfo> commands = new List<CommandHandlerInfo>();
        private static List<QueuedLine> queuedLines = new List<QueuedLine>(); // Lines sent before init
        private static readonly string[] newLines = new string[]              // All characters that will be replaced by a line break
        { 
            Environment.NewLine, "\r\n", "\r", "\n"
        }; 

        private bool initialized;     // True once the console has been created - it must wait for Futile to init
        private bool typing;          // True when input is redirected to the command line
        private bool silent;          // True when all logs to the console should be hidden
        private FContainer container; // The container for all game console nodes
        private FSprite background;   // The background rect of the game console
        private FLabel inputLabel;    // Displays the user's command line input
        private StringBuilder inputString = new StringBuilder();        // Stores the user's command line input
        private readonly Queue<LineInfo> lines = new Queue<LineInfo>(); // Stores the most recent output lines added
        private DevConsoleMod mod;

        /// <summary>
        /// Registers critical built-in commands.
        /// </summary>
        static GameConsole()
        {
            // Catches any commands that don't match any other and displays an error message
            RegisterCommand(new CatchAllCommand());

            // Displays the syntax of all registered commands
            RegisterCommand(new SimpleCommand("help", args =>
            {
                int page;
                if (args.Length == 0 || !int.TryParse(args[0], out page))
                    page = 0;
                else
                    page = Math.Max(page - 1, 0);

                var helps = commands
                    .Select(cmd => cmd.Help())
                    .Where(help => help != null)
                    .Skip(maxLines * page)
                    .Take(maxLines)
                    .ToArray();

                Array.Sort(helps);
                if (helps.Length > 0)
                {
                    foreach (var help in helps)
                        WriteLine(help);
                }
                else
                {
                    WriteLine("That page is empty!");
                }
            })
            { Summary = "help [page?]" });
        }

        /// <summary>
        /// Whether the in-game console is ready to open.
        /// Methods calls in <see cref="GameConsole"/>, unless otherwise specified, will wait for the console to be initialized to execute.
        /// </summary>
        public static bool Initialized => instance?.initialized ?? false;

        internal static void Apply(DevConsoleMod mod)
        {
            instance = new GameObject("Dev Console").AddComponent<GameConsole>();
            instance.mod = mod;
        }

        /// <summary>
        /// Writes one or more lines of white text to the console.
        /// </summary>
        /// <param name="text">The text to write.</param>
        public static void WriteLine(string text) => WriteLine(text, Color.white);

        /// <summary>
        /// Writes one or more lines of colored text to the console.
        /// </summary>
        /// <param name="text">The text to write.</param>
        /// <param name="color">The color of the text.</param>
        public static void WriteLine(string text, Color color)
        {
            if (text == null) text = "null";

            if (Futile.instance == null || Futile.atlasManager == null)
            {
                if(!instance?.silent ?? true)
                    queuedLines.Add(new QueuedLine() { color = color, text = text });
                return;
            }
            foreach (string line in text.Split(newLines, StringSplitOptions.None))
                instance.AddLine(line, color);
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
            queuedLines?.Clear();

            if (instance == null) return;

            foreach (var line in instance.lines)
                line.label.RemoveFromContainer();
            instance.lines.Clear();
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

        private void Update()
        {
            if (!initialized)
            {
                if (Futile.instance == null || Futile.atlasManager == null) return;
                Initialize();
            }

            // Run bound commands
            Bindings.Run();

            CaptureInput(false);

            bool skipInput = false;

            // Open and close the console
            if (!typing && Input.GetKeyDown(KeyCode.BackQuote))
            {
                typing = true;
                container.isVisible = true;
                skipInput = true;

                PauseGame(DevConsoleMod.autopause);
            }
            else if (typing && (Input.GetKeyUp(KeyCode.Escape) || Input.GetKeyDown(KeyCode.BackQuote)))
            {
                typing = false;
                container.isVisible = false;

                PauseGame(false);
            }

            // Do input
            if (typing && !skipInput)
            {
                foreach (var c in Input.inputString)
                {
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
                        case '\n':
                        case '\r':

                            SubmitCommand(inputString.ToString().Trim());
                            inputString = new StringBuilder();

                            break;

                        // Otherwise, add to the current input
                        default:
                            inputString.Append(c);
                            break;
                    }
                }

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
                inputLabel.text = " > " + inputString;
                y += lineHeight;

                foreach (var line in lines.Reverse())
                {
                    line.label.x = consoleMargin;
                    line.label.y = y;
                    y += lineHeight;
                }

                container.MoveToFront();
            }
        }

        private void Initialize()
        {
            initialized = true;
            container = new FContainer();

            background = new FSprite("pixel")
            {
                anchorX = 0f,
                anchorY = 0f,
                scaleX = consoleWidth,
                scaleY = consoleHeight,
                color = Color.black,
                alpha = 0.75f
            };
            inputLabel = new FLabel("font", "")
            {
                anchorX = 0f,
                anchorY = 0f
            };

            container.AddChild(background);
            container.AddChild(inputLabel);
            container.isVisible = false;
            Futile.stage.AddChild(container);

            WriteHeader();

            foreach (var line in queuedLines)
                WriteLine(line.text, line.color);
            queuedLines = null;

            BuiltInCommands.RegisterCommands();

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
            void BlockUpdate(On.RainWorld.orig_Update orig, RainWorld self) { }

            if (shouldPause && !isGamePaused)
            {
                On.RainWorld.Update += BlockUpdate;
                isGamePaused = true;
            }
            else if(!shouldPause && isGamePaused)
            {
                On.RainWorld.Update -= BlockUpdate;
                isGamePaused = false;
            }
        }

        private void SubmitCommand(string command, bool echo = true)
        {
            if(echo) AddLine(" > " + command, new Color(0.7f, 0.7f, 0.7f));

            string[] args = SplitCommandLine(command).ToArray();
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
                        string Indent(string str) => "  " + str.Replace(Environment.NewLine, Environment.NewLine + "    ");

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

        private void AddLine(string text) => AddLine(text, Color.white);

        private void AddLine(string text, Color color)
        {
            if (silent) return;

            LineInfo line;
            if (lines.Count < maxLines)
            {
                line = new LineInfo();
                line.label = new FLabel("font", "")
                {
                    anchorX = 0f,
                    anchorY = 0f,
                    color = color
                };
                container.AddChild(line.label);
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

        private static readonly Dictionary<char, string> escapeCodes = new Dictionary<char, string>()
        {
            { '"', "\"" },
            { '\\', "\\" }
        };
        private static IEnumerable<string> SplitCommandLine(string commandLine)
        {
            int cursor = 0;
            int len = commandLine.Length;

            while (cursor < len)
            {
                // Eat whitespace before argument
                while (cursor < len && char.IsWhiteSpace(commandLine[cursor]))
                    cursor++;

                if (cursor >= len) yield break;

                // Slice out the argument
                int sliceStart = cursor;
                bool quoted = commandLine[cursor] == '"';
                StringBuilder arg = new StringBuilder();
                if (quoted) cursor++;

                while (cursor < len)
                {
                    char c = commandLine[cursor];

                    switch (c)
                    {
                        case '"':
                            if (quoted && (cursor + 1 == commandLine.Length || char.IsWhiteSpace(commandLine[cursor + 1])))
                            {
                                // This quote is at the end of an argument
                                // Slice it here
                                cursor++;
                                goto argDone;
                            }
                            break;


                        case '\\':
                            if (cursor + 1 < commandLine.Length && escapeCodes.TryGetValue(commandLine[cursor + 1], out string escaped))
                            {
                                // There is an escaped character here
                                // Slice just before the escape sequence
                                arg.Append(commandLine.Substring(sliceStart, cursor - sliceStart));
                                arg.Append(escaped);
                                cursor++;
                                sliceStart = cursor + 1;
                            }
                            break;

                        default:
                            // Split at spaces
                            if (!quoted && char.IsWhiteSpace(c))
                            {
                                goto argDone;
                            }
                            break;
                    }

                    cursor++;
                }

            argDone:
                // Append the rest
                if (sliceStart < cursor && sliceStart < commandLine.Length)
                    arg.Append(commandLine.Substring(sliceStart, Math.Min(cursor, commandLine.Length) - sliceStart));

                // Remove matching quotes
                if (arg.Length > 1 && arg[0] == '"' && arg[arg.Length - 1] == '"')
                {
                    arg.Remove(arg.Length - 1, 1);
                    arg.Remove(0, 1);
                }

                if (arg.Length > 0)
                    yield return arg.ToString();
            }
        }

        private static bool GetKey(Func<string, bool> orig, string name) => blockingInput ? false : orig(name);
        private static bool GetKey(Func<KeyCode, bool> orig, KeyCode code) => blockingInput ? false : orig(code);
        private static bool GetKeyDown(Func<string, bool> orig, string name) => blockingInput ? false : orig(name);
        private static bool GetKeyDown(Func<KeyCode, bool> orig, KeyCode code) => blockingInput ? false : orig(code);
        private static bool GetKeyUp(Func<string, bool> orig, string name) => blockingInput ? false : orig(name);
        private static bool GetKeyUp(Func<KeyCode, bool> orig, KeyCode code) => blockingInput ? false : orig(code);

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
                    var method = registerTrace.GetFrame(1).GetMethod();
                    return $"{method.Name} in {method.DeclaringType.Name}";
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
