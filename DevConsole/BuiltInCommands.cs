using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;
using MonoMod.RuntimeDetour;

namespace DevConsole
{
    using Commands;
    using BindEvents;
    using static GameConsole;
    using System.Collections;

    // Contains all commands that come with the dev console
    internal static class BuiltInCommands
    {
        // Colors associated with each Unity log type
        private static readonly Dictionary<LogType, Color> logColors = new Dictionary<LogType, Color>()
        {
            { LogType.Error, new Color(0.7f, 0f, 0f) },
            { LogType.Assert, new Color(1f, 0.7f, 0f) },
            { LogType.Warning, Color.yellow },
            { LogType.Log, Color.white },
            { LogType.Exception, Color.red }
        };

        // Constants for key bindings
        private static readonly string[] eventNames = new string[] { "down", "up", "hold_down", "hold_up" };
        private static readonly string[] timingNames = new string[] { "frame", "update" };

        private static RainWorld rw;
        private static RainWorld RW => rw ??= UnityEngine.Object.FindObjectOfType<RainWorld>();
        
        // Fields for the log viewer
        private static bool showingDebug = false;
        private static readonly FieldInfo Application_s_LogCallback = typeof(Application).GetField("s_LogCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        public static void RegisterCommands()
        {
            // Commands that don't fit any other category
            #region Misc

            // Mirrors all Debug.Log* calls to the dev console
            new CommandBuilder("show_debug")
                .Run(args =>
                {
                    var cb = (Application.LogCallback)Application_s_LogCallback.GetValue(null);
                    if (showingDebug) cb -= WriteLogToConsole;
                    else cb += WriteLogToConsole;
                    showingDebug = !showingDebug;
                    Application.RegisterLogCallback(cb);
                    WriteLine(showingDebug ? "Debug messages will be displayed here." : "Debug messages will no longer be displayed here.");

                    if (args.Length > 0 && bool.TryParse(args[0], out bool argBool) && argBool)
                    {
                        WriteLine("The game will pause when errors occur.");
                        pauseOnError = true;
                    }
                    else
                        pauseOnError = false;
                })
                .Help("show_debug [pause_on_error: false]")
                .AutoComplete(new string[][]
                {
                    new string[] { "true", "false" }
                })
                .Register();

            // Clears the console
            new CommandBuilder("clear")
                .Run(args =>
                {
                    Clear();
                    WriteHeader();
                })
                .Register();

            // Throws an exception
            // Useful, right?
            new CommandBuilder("throw")
                .Run(args =>
                {
                    if (args.Length > 0) throw new Exception(string.Join(" ", args));
                    throw new Exception();
                })
                .Help("throw [message?]")
                .Register();

            // Writes a list of lines to the console
            new CommandBuilder("echo")
                .Run(args =>
                {
                    foreach (var line in args)
                        WriteLine(line);
                })
                .Help("echo [line1?] [line2?] ...")
                .Register();

            // Runs a command and suppresses all output for its duration
            new CommandBuilder("silence")
                .Run(args =>
                {
                    if (args.Length == 0)
                        WriteLine("No command given to silence!");
                    else
                        foreach (var cmd in args)
                            RunCommandSilent(cmd);
                })
                .Help("silence [command1] [command2?] [command3?] ...")
                .Register();

            // Speeds up the game
            {
                Hook hook = null;

                new CommandBuilder("game_speed")
                    .Run(args =>
                    {
                        // Support more than one physics update per frame
                        void FixedRawUpdate(On.MainLoopProcess.orig_RawUpdate orig, MainLoopProcess self, float dt)
                        {
                            self.myTimeStacker += dt * self.framesPerSecond;
                            while (self.myTimeStacker > 2f)
                            {
                                self.Update();
                                self.myTimeStacker -= 1f;

                                // Extra graphics updates are needed to reduce visual artifacts
                                if (self.myTimeStacker > 1f)
                                    self.GrafUpdate(self.myTimeStacker);
                            }

                            //self.GrafUpdate(self.myTimeStacker);
                            orig(self, 0f);
                        }

                        try
                        {
                            if (args.Length == 0)
                            {
                                // Log game speed
                                WriteLine($"Game speed: {Time.timeScale}x");
                            }
                            else
                            {
                                float targetSpeed = float.Parse(args[0]);
                                if (targetSpeed < 0) targetSpeed = 1f;

                                if (Mathf.Abs(targetSpeed - 1f) < 0.001f)
                                {
                                    Time.timeScale = 1f;
                                    hook?.Dispose();
                                    hook = null;
                                    WriteLine("Reset game speed.");
                                }
                                else
                                {
                                    Time.timeScale = targetSpeed;
                                    if (hook == null)
                                        hook = new Hook(typeof(MainLoopProcess).GetMethod("RawUpdate"), (On.MainLoopProcess.hook_RawUpdate)FixedRawUpdate);
                                    WriteLine($"Set game speed to {targetSpeed}x.");
                                }
                            }

                        }
                        catch
                        {
                            WriteLine("Failed to set game speed!");
                        }
                    })
                    .Help("game_speed [speed_multiplier?]")
                    .Register();
            }

            // Manipulate the cycle timer
            new CommandBuilder("rain_timer")
                .RunGame((game, args) =>
                {
                    try
                    {
                        var cycle = game.world.rainCycle;
                        if (args.Length == 0 || args[0] == "help")
                        {
                            WriteLine("rain_timer get");
                            WriteLine("rain_timer set [new_value]");
                            WriteLine("rain_timer reset");
                            WriteLine("rain_timer pause");
                        }
                        else
                        {
                            switch (args[0])
                            {
                                case "get": WriteLine($"Rain timer: {cycle.timer}\nTicks until rain: {cycle.TimeUntilRain}"); break;
                                case "set": cycle.timer = int.Parse(args[1]); break;
                                case "reset": cycle.timer = 0; break;
                                case "pause":
                                    if (cycle.pause > 0)
                                    {
                                        WriteLine("Unpaused rain.");
                                        cycle.pause = 0;
                                    }
                                    else
                                    {
                                        WriteLine("Paused rain.");
                                        cycle.pause = int.MaxValue / 2;
                                    }
                                    break;
                                default:
                                    WriteLine("Unknown subcommand!");
                                    break;
                            }
                        }
                    }
                    catch
                    {
                        WriteLine("Couldn't modify rain timer!");
                    }
                })
                .Help("rain_timer [subcommand?] [arg?]")
                .AutoComplete(new string[][] {
                    new string[] { "get", "set", "reset", "pause" }
                })
                .Register();

            {
                BF bf = null;
                var flags = new string[]
                {
                    "u8", "u16", "u32", "u64",
                    "neg_cells", "no_neg_cells",
                    "fast"
                };
                string Escape(string input)
                {
                    StringBuilder o = new StringBuilder();
                    bool esc = false;
                    foreach(var c in input)
                    {
                        if (esc)
                        {
                            string escapeCode;
                            switch(c)
                            {
                                case '\\': escapeCode = "\\"; break;
                                case 'r': escapeCode = "\r"; break;
                                case 'n': escapeCode = "\n"; break;
                                case '0': escapeCode = "\0"; break;
                                default: escapeCode = "\\" + c.ToString(); break;
                            }
                            o.Append(escapeCode);
                            esc = false;
                        }
                        else
                        {
                            if (c == '\\') esc = true;
                            else o.Append(c);
                        }
                    }
                    if (esc) o.Append("\\");
                    return o.ToString();
                }

                new CommandBuilder("bf")
                    .Run(args =>
                    {
                        try
                        {
                            if (bf?.Running ?? false)
                            {
                                switch(args[0])
                                {
                                    case "abort":
                                        bf.Abort();
                                        bf = null;
                                        break;

                                    case "input":
                                        foreach (var line in args.Skip(1))
                                            bf.Input(Escape(line) + '\n');
                                        break;

                                    case "line":
                                        bf.Input("\n");
                                        break;

                                    default:
                                        foreach (var line in args)
                                            bf.Input(Escape(line) + '\n');
                                        break;
                                }
                            }
                            else
                                bf = new BF(args[args.Length - 1], args.Take(args.Length - 1).ToArray());
                        }
                        catch
                        {
                            WriteLine("Failed to start BF!");
                        }
                    })
                    .AutoComplete(args =>
                    {
                        if (bf?.Running ?? false)
                        {
                            if (args.Length == 0)
                                return new string[] { "abort", "input", "line" };
                            return null;
                        }
                        if (args.All(arg => Array.IndexOf(flags, arg) != -1)) return flags.Concat(new string[] { "+[-->-[>>+>-----<<]<--<---]>-.>>>+.>>..+++[.>]<<<<.+++.------.<<-.>>>>+." });
                        return null;
                    })
                    .HideHelp() // Too cursed for the general public
                    .Register();

                // Change the console's font
                new CommandBuilder("font")
                    .Run(args =>
                    {
                        if (args.Length == 0)
                        {
                            WriteLine("Available fonts: " + string.Join(", ", Futile.atlasManager._fontsByName.Keys.ToArray()));
                            WriteLine("Current font: " + CurrentFont);
                        }
                        else
                        {
                            try
                            {
                                CurrentFont = Futile.atlasManager._fontsByName.Keys.First(fontName => fontName.Equals(args[0], StringComparison.OrdinalIgnoreCase));
                            }
                            catch
                            {
                                WriteLine("Failed to set font!");
                            }
                        }

                    })
                    .Help("font [font_name?]")
                    .AutoComplete(args =>
                    {
                        if (args.Length == 0) return Futile.atlasManager._fontsByName.Keys.ToArray();
                        return null;
                    })
                    .Register();

                // Call a static method
                {
                    Type SearchNestedTypes(Func<string, Type> getType, string[] typeParts, int startInd)
                    {
                        for(int end = startInd + 1; end <= typeParts.Length; end++)
                        {
                            Type t = getType(typeParts.Skip(startInd).Take(end - startInd).Aggregate((acc, str) => $"{acc}.{str}"));
                            if (t == null) continue;
                            if(end == typeParts.Length)
                            {
                                return t;
                            }
                            else
                            {
                                t = SearchNestedTypes(typeName => t.GetNestedType(typeName, BindingFlags.Public | BindingFlags.NonPublic), typeParts, end);
                                if (t != null) return t;
                            }
                        }
                        return null;
                    }

                    new CommandBuilder("invoke")
                        .Run(args =>
                        {
                            if (args.Length == 0)
                            {
                                WriteLine("No method specified!");
                                return;
                            }

                            // Get the type and method names
                            string[] typeParts = args[0].Split('.');
                            if(typeParts.Length < 2)
                            {
                                WriteLine("A type and method must be specified!");
                                return;
                            }

                            string methodName = typeParts[typeParts.Length - 1];
                            Array.Resize(ref typeParts, typeParts.Length - 1);

                            // Find the type
                            Type t = null;
                            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                t = SearchNestedTypes(typeName => asm.GetType(typeName, false, true), typeParts, 0);
                                if(t != null)
                                    break;
                            }

                            if(t == null)
                            {
                                WriteLine($"Could not find type: {string.Join(".", typeParts)}");
                                return;
                            }

                            // Find the method
                            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                            {
                                if (!m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase)) continue;
                                if (m.ContainsGenericParameters) continue;

                                var p = m.GetParameters();
                                if (p.Length != args.Length - 1) continue;

                                // Try converting all arguments
                                object[] realArgs = new object[p.Length];
                                for (int i = 0; i < realArgs.Length; i++)
                                {
                                    try
                                    {
                                        Type paramType = p[i].ParameterType;
                                        string arg = args[i + 1];
                                        if ((arg is "null" or "default") && !paramType.IsValueType)
                                            realArgs[i] = null;
                                        else if (arg is "default")
                                            realArgs[i] = Activator.CreateInstance(paramType);
                                        else
                                            realArgs[i] = System.ComponentModel.TypeDescriptor.GetConverter(paramType).ConvertFromInvariantString(arg);
                                    }
                                    catch
                                    {
                                        goto continueOuter;
                                    }
                                }

                                // All args are set
                                // Execute
                                object res = m.Invoke(null, realArgs);
                                if (res != null)
                                {
                                    WriteLine(res.ToString());
                                }
                                return;
                            continueOuter:;
                            }

                            WriteLine($"Could not find the specified method overload: {methodName}");
                        })
                        .Help("invoke [method] [arg1?] [arg2?] ...")
                        .Register();
                }
            }

            // Control positioning of commands
            new CommandBuilder("default_pos")
                .Run(args =>
                {
                    if (args.Length == 0)
                    {
                        WriteLine("default_pos player [player_num: 0]");
                        WriteLine("default_pos mouse [camera_num: 0]");
                        WriteLine("default_pos camera [camera_num: 0]");
                    }
                    else
                    {
                        try
                        {
                            int num = (args.Length > 1) ? int.Parse(args[1]) : 0;
                            switch (args[0])
                            {
                                case "player":
                                    Positioning.GetDefaultPos = game => new RoomPos(game.Players[num].Room, game.Players[num].realizedObject.firstChunk.pos);
                                    WriteLine("Commands will target the player.");
                                    break;
                                case "mouse":
                                    Positioning.GetDefaultPos = game => new RoomPos(game.cameras[num].room.abstractRoom, game.cameras[num].pos + (Vector2)Input.mousePosition);
                                    WriteLine("Commands will target the mouse.");
                                    break;
                                case "camera":
                                    Positioning.GetDefaultPos = game => new RoomPos(game.cameras[num].room.abstractRoom, game.cameras[num].pos + game.cameras[num].sSize / 2f);
                                    WriteLine("Commands will target the center of the camera.");
                                    break;
                            }
                        }
                        catch
                        {
                            WriteLine("Failed to set spawning position!");
                        }
                    }
                })
                .Help("default_pos [target?] [arg?]")
                .AutoComplete(new string[][] {
                    new string[] { "player", "mouse", "camera" }
                })
                .Register();

            #endregion Misc


            // Commands related to event-command bindings
            #region Bindings

            // Binds a key to a command
            new CommandBuilder("bind")
                .Run(args =>
                {
                    if (args.Length < 1 || args[0].Length == 0)
                    {
                        WriteLine("No keycode specified!");
                        return;
                    }

                    int skip = 1;
                    string mode = "down";
                    bool syncWithUpdate = true;

                    if (skip < args.Length && eventNames.Contains(args[skip]))
                        mode = args[skip++];

                    if (skip < args.Length && timingNames.Contains(args[skip]))
                        syncWithUpdate = args[skip++] == "update";
                    
                    IBindEvent e = EventFromKey(args[0], mode);
                    if (e == null)
                    {
                        WriteLine($"Couldn't find key: {args[0]}");
                        return;
                    }

                    if (skip >= args.Length)
                    {
                        // Only the key was specified - get and print all binds
                        foreach (var cmd in Bindings.GetBoundCommands(e, syncWithUpdate))
                            WriteLine(cmd);
                    }
                    else
                    {
                        // A list of commands was specified - bind them
                        foreach (var cmd in args.Skip(skip).Where(s => !string.IsNullOrEmpty(s)))
                            Bindings.Bind(e, cmd, syncWithUpdate);
                    }
                })
                .Help("bind [keycode] [event?] [timing?] [commmand1?] [command2?] ...")
                .AutoComplete(args => args.Length switch
                {
                    0 => GetKeyNames(),
                    1 => eventNames,
                    2 => eventNames.Contains(args[1]) ? timingNames : null,
                    _ => null
                })
                .Register();

            // Unbinds a key
            new CommandBuilder("unbind")
                .Run(args =>
                {
                    if (args.Length < 1 || args[0].Length == 0)
                    {
                        WriteLine("No keycode specified!");
                        return;
                    }

                    int skip = 1;
                    string mode = null;
                    bool? syncWithUpdate = null;

                    if (skip < args.Length && eventNames.Contains(args[skip]))
                        mode = args[skip++];

                    if (skip < args.Length && timingNames.Contains(args[skip]))
                        syncWithUpdate = args[skip++] == "update";

                    // Find what events to unbind
                    // Leaving it blank selects all
                    IBindEvent[] events;
                    if (mode == null)
                        events = eventNames.Select(mode => EventFromKey(args[0], mode)).ToArray();
                    else
                        events = new IBindEvent[] { EventFromKey(args[0], mode) };

                    // Find what sync mods to unbind
                    // Leaving it blank selects both
                    bool[] syncs;
                    if (syncWithUpdate == null)
                        syncs = new bool[] { true, false };
                    else
                        syncs = new bool[] { syncWithUpdate.Value };

                    if (events.Any(o => o == null))
                    {
                        WriteLine($"Couldn't find key: {args[0]}");
                        return;
                    }

                    if (skip >= args.Length)
                    {
                        // Only the key was specified - unbind all
                        foreach(var e in events)
                            foreach(var sync in syncs)
                                Bindings.UnbindAll(e, sync);
                    }
                    else
                    {
                        // A list of commands was specified - unbind them specifically
                        foreach (var cmd in args.Skip(skip).Where(s => !string.IsNullOrEmpty(s)))
                        {
                            foreach (var e in events)
                                foreach (var sync in syncs)
                                    Bindings.Unbind(e, cmd, sync);
                        }
                    }
                })
                .Help("unbind [keycode] [event?] [timing?] [commmand1?] [command2?] ...")
                .AutoComplete(args => args.Length switch
                {
                    0 => GetKeyNames(),
                    1 => eventNames,
                    2 => eventNames.Contains(args[1]) ? timingNames : null,
                    _ => null
                })
                .Register();

            // Unbinds everything
            new CommandBuilder("unbind_all")
                .Run(args => Bindings.UnbindAll())
                .Register();

            // Creates a command alias
            new CommandBuilder("alias")
                .Run(args =>
                {
                    if (args.Length == 0)
                        WriteLine("No alias was given!");
                    else if (args.Length == 1)
                        Aliases.RemoveAlias(args[0]);
                    else
                        Aliases.SetAlias(args[0], args.Skip(1).ToArray());
                })
                .Help("alias [name] [command1?] [command2?] ...")
                .AutoComplete(args =>
                {
                    if (args.Length == 0) return Aliases.GetAliases();
                    else return null;
                })
                .Register();

            #endregion Bindings


            // Commands related to creatures
            #region Creatures

            // Spawns a single creature by type
            new CommandBuilder("creature")
                .RunGame((game, args) =>
                {
                    try
                    {
                        // Find the creature to spawn, first by exact name then by creature type enum
                        CreatureTemplate template = StaticWorld.creatureTemplates.FirstOrDefault(t => t.name.Equals(args[0], StringComparison.OrdinalIgnoreCase));
                        if (template == null) template = StaticWorld.GetCreatureTemplate((CreatureTemplate.Type)Enum.Parse(typeof(CreatureTemplate.Type), args[0], true));

                        EntityID? id = null;
                        if(args.Length > 1)
                        {
                            if (args[1].Contains('.'))
                                id = EntityID.FromString(args[1]);
                            else
                                id = new EntityID(0, int.Parse(args[1]));
                        }

                        var crit = new AbstractCreature(
                            game.world,
                            template,
                            null,
                            SpawnRoom.GetWorldCoordinate(SpawnPos),
                            id ?? game.GetNewID()
                        );
                        SpawnRoom.abstractRoom.AddEntity(crit);
                        crit.RealizeInRoom();
                    }
                    catch(Exception e)
                    {
                        WriteLine("Failed to spawn creature! See console log for more info.");
                        Debug.Log("Failed to spawn creature!\n" + e.ToString());
                    }
                })
                .Help("creature [type] [ID?]")
                .AutoComplete(new string[][] {
                    Enum.GetNames(typeof(CreatureTemplate.Type)).Concat(StaticWorld.creatureTemplates.Select(t => t.name)).ToArray()
                })
                .Register();

            // Kills everything in the current region
            new CommandBuilder("remove_crits")
                .RunGame((game, args) =>
                {
                    try
                    {
                        bool respawn = (args.Length > 0) ? bool.Parse(args[0]) : false;

                        foreach (var room in game.world.abstractRooms.Concat(new AbstractRoom[] { game.world.offScreenDen }))
                        {
                            foreach (var crit in room.creatures)
                            {
                                if (crit.creatureTemplate.type != CreatureTemplate.Type.Slugcat)
                                {
                                    if (respawn)
                                        crit.Die();
                                    crit.realizedCreature?.LoseAllGrasps();
                                    crit.realizedObject?.Destroy();
                                    crit.Destroy();
                                }
                            }
                        }
                    }
                    catch
                    {
                        WriteLine("Failed to destroy everything in the region.");
                    }
                })
                .Help("remove_crits [respawn: true]")
                .AutoComplete(new string[][] {
                    new string[] { "true", "false" }
                })
                .Register();

            // Destroys all selected objects
            new CommandBuilder("destroy")
                .RunGame((game, args) =>
                {
                    bool respawn = (args.Length > 1) ? bool.Parse(args[1]) : false;

                    foreach (var obj in Selection.SelectAbstractObjects(game, args.Length > 0 ? args[0] : null))
                    {
                        try
                        {
                            if (obj is AbstractCreature crit)
                            {
                                if (respawn)
                                    crit.Die();
                                crit.realizedCreature?.LoseAllGrasps();
                            }
                            obj.realizedObject?.Destroy();
                            obj.Destroy();
                        }
                        catch { /* YOLO */ }
                    }
                })
                .Help("destroy [selector?] [respawn: true]")
                .AutoComplete(new string[][] {
                    null,
                    new string[] { "true", "false" }
                })
                .Register();

            #endregion Creatures


            // Commands related to the player
            #region Players

            // Teleport all selected objects to the targeted point
            new CommandBuilder("tp")
                .RunGame((game, args) =>
                {
                    var abstrobjs = Selection.SelectAbstractObjects(game, args.Length > 0 ? args[0] : null);
                    var logs = new DedupCache<string>();

                    var pos = SpawnPos;
                    var room = SpawnRoom;
                    foreach (var abstrobj in abstrobjs)
                    {

                        bool newRoom = room.abstractRoom.index != abstrobj.Room.index;

                        if (abstrobj.realizedObject is PhysicalObject o && o.room == null || abstrobj.realizedObject is Creature c && c.inShortcut)
                        {
                            logs.Add("Failed to teleport from a shortcut.");
                            continue;
                        }

                        abstrobj.Move(room.GetWorldCoordinate(pos));

                        if (abstrobj.realizedObject is PhysicalObject physobj)
                        {
                            foreach (var chunk in physobj.bodyChunks)
                            {
                                chunk.HardSetPosition(pos);
                            }

                            if (newRoom)
                            {
                                physobj.NewRoom(room);
                            }
                        }
                        else if (newRoom)
                        {
                            room.abstractRoom.AddEntity(abstrobj);
                            abstrobj.RealizeInRoom();

                            if (abstrobj.realizedObject is not null)
                                foreach (var chunk in abstrobj.realizedObject.bodyChunks)
                                {
                                    chunk.HardSetPosition(pos);
                                    chunk.vel = Vector2.zero;
                                }
                        }
                    }

                    foreach (var line in logs.AsStrings())
                        WriteLine(line);
                })
                .Help("tp [selector?]")
                .Register();

            // Kill all selected objects
            new CommandBuilder("kill")
                .RunGame((game, args) =>
                {
                    var abstrobjs = Selection.SelectAbstractObjects(game, args.Length > 0 ? args[0] : null);

                    foreach (var abstrobj in abstrobjs)
                    {
                        if (abstrobj is not AbstractCreature c)
                        {
                            WriteLine($"Failed to kill creature because targeted object ({abstrobj.type}) is not a creature.");
                            continue;
                        }

                        if (c.realizedCreature != null)
                            c.realizedCreature.Die();
                        else
                            c.Die();
                    }
                })
                .Help("kill [selector?]")
                .Register();

            // Apply a force to all selected objects
            new CommandBuilder("move")
                .RunGame((game, args) =>
                {
                    if (args.Length < 2 || !float.TryParse(args[0], out var x) || !float.TryParse(args[1], out var y))
                    {
                        WriteLine("Expected a float [velX] and a float [velY] argument.");
                        return;
                    }

                    var abstrobjs = Selection.SelectAbstractObjects(game, args.Length > 2 ? args[2] : null);
                    var logs = new DedupCache<string>();

                    foreach (var abstrobj in abstrobjs)
                    {
                        if (abstrobj.realizedObject is not PhysicalObject o)
                        {
                            logs.Add("Failed to move a non-realized object.");
                            continue;
                        }

                        foreach (var chunk in o.bodyChunks)
                        {
                            chunk.vel += new Vector2(x, y);
                        }
                    }

                    foreach (var line in logs.AsStrings())
                        WriteLine(line);
                })
                .Help("move [vel_x] [vel_y] [selector?]")
                .Register();

            // Pull all selecetd objects towards the targeted point
            new CommandBuilder("pull")
                .RunGame((game, args) =>
                {
                    if (args.Length < 1 || !float.TryParse(args[0], out float str))
                    {
                        WriteLine("Expected a float [strength] argument.");
                        return;
                    }

                    var abstrobjs = Selection.SelectAbstractObjects(game, args.Length > 1 ? args[1] : null);
                    var logs = new DedupCache<string>();

                    foreach (var abstrobj in abstrobjs)
                    {
                        if (abstrobj.realizedObject is not PhysicalObject o)
                        {
                            logs.Add("Failed to pull a non-realized object.");
                            continue;
                        }

                        if (o.room != SpawnRoom)
                        {
                            logs.Add("Failed to pull an object in another room.");
                            continue;
                        }

                        if (o is Creature c && c is not Player)
                        {
                            c.Stun(12);
                        }

                        foreach (var chunk in o.bodyChunks)
                        {
                            chunk.vel += (SpawnPos - chunk.pos).normalized * str;
                        }

                        foreach (var line in logs.AsStrings())
                            WriteLine(line);
                    }
                })
                .Help("pull [strength] [selector?]")
                .Register();

            // Push all objects away from the targeted point
            new CommandBuilder("push")
                .RunGame((game, args) =>
                {
                    if (args.Length < 1 || !float.TryParse(args[0], out float str))
                    {
                        WriteLine("Expected a float [strength] argument.");
                        return;
                    }

                    var abstrobjs = Selection.SelectAbstractObjects(game, args.Length > 1 ? args[1] : null);
                    var logs = new DedupCache<string>();

                    foreach (var abstrobj in abstrobjs)
                    {
                        if (abstrobj.realizedObject is not PhysicalObject o)
                        {
                            logs.Add("Failed to push a non-realized object.");
                            continue;
                        }

                        if (o.room != SpawnRoom)
                        {
                            logs.Add("Failed to push an object in another room.");
                            continue;
                        }

                        if (o is Creature c && c is not Player)
                        {
                            c.Stun(12);
                        }

                        foreach (var chunk in o.bodyChunks)
                        {
                            chunk.vel -= (SpawnPos - chunk.pos).normalized * str;
                        }

                        foreach (var line in logs.AsStrings())
                            WriteLine(line);
                    }
                })
                .Help("push [strength] [selector?]")
                .Register();

            // Allows players to swim through everything
            {
                bool noclip = false;
                bool remove = false;
                List<Hook> hooks = new List<Hook>();

                new CommandBuilder("noclip")
                    .Run(args =>
                    {
                        void NoClip(On.Player.orig_Update orig, Player self, bool eu)
                        {
                            noclip = true;
                            bool roomWater = self.room.water;
                            try
                            {
                                self.room.water = true;
                                orig(self, eu);
                                self.airInLungs = 1f;

                                self.CollideWithTerrain = false;

                                if (remove)
                                {
                                    foreach (var hook in hooks)
                                        hook.Dispose();
                                    hooks.Clear();
                                    self.CollideWithTerrain = true;
                                    remove = false;
                                }
                            }
                            finally
                            {
                                noclip = false;
                                self.room.water = roomWater;
                            }
                        }

                        float HighWaterLevel(On.Room.orig_FloatWaterLevel orig, Room self, float horizontalPos)
                        {
                            return noclip ? 100000f : orig(self, horizontalPos);
                        }

#if BEPCOMP
                        Room.Tile RemoveTiles(On.Room.orig_GetTile_int_int orig, Room self, int x, int y)
#else
                        Room.Tile RemoveTiles(On.Room.orig_GetTile_3 orig, Room self, int x, int y)
#endif
                        {
                            return noclip ? new Room.Tile(x, y, Room.Tile.TerrainType.Air, false, false, false, 0, 0) : orig(self, x, y);
                        }

                        if (hooks.Count == 0)
                        {
                            hooks.Add(new Hook(typeof(Player).GetMethod("Update"), (On.Player.hook_Update)NoClip));
                            hooks.Add(new Hook(typeof(Room).GetMethod("FloatWaterLevel"), (On.Room.hook_FloatWaterLevel)HighWaterLevel));
#if BEPCOMP
                            hooks.Add(new Hook(typeof(Room).GetMethod("GetTile", new Type[] { typeof(int), typeof(int) }), (On.Room.hook_GetTile_int_int)RemoveTiles));
#else
                            hooks.Add(new Hook(typeof(Room).GetMethod("GetTile", new Type[] { typeof(int), typeof(int) }), (On.Room.hook_GetTile_3)RemoveTiles));
#endif
                            WriteLine("Enabled noclip.");
                        }
                        else
                        {
                            WriteLine("Disabled noclip.");
                            remove = true;
                        }
                    })
                    .Register();
            }

            // Changes the player's current karma
            new CommandBuilder("karma")
                .RunGame((game, args) =>
                {
                    try
                    {
                        if (args.Length == 0) WriteLine("Karma: " + game.GetStorySession?.saveState?.deathPersistentSaveData.karma.ToString() ?? "N/A");
                        else
                        {
                            game.GetStorySession.saveState.deathPersistentSaveData.karma = int.Parse(args[0]);

                            for (int i = 0; i < game.cameras.Length; i++)
                            {
                                HUD.KarmaMeter karmaMeter = game.cameras[i].hud.karmaMeter;
                                karmaMeter?.UpdateGraphic();
                            }
                        }
                    }
                    catch
                    {
                        WriteLine("Failed to set karma!");
                    }
                })
                .Help("karma [value?]")
                .Register();

            // Changes the player's karma cap
            new CommandBuilder("karma_cap")
                .RunGame((game, args) =>
                {
                    try
                    {
                        if (args.Length == 0) WriteLine("Karma cap: " + game.GetStorySession?.saveState?.deathPersistentSaveData.karmaCap.ToString() ?? "N/A");
                        else game.GetStorySession.saveState.deathPersistentSaveData.karmaCap = int.Parse(args[0]);
                    }
                    catch
                    {
                        WriteLine("Failed to set karma cap!");
                    }
                })
                .Help("karma_cap [value?]")
                .Register();

            // Makes the player mostly invulnerable
            {
                List<Hook> hooks = new List<Hook>();

                new CommandBuilder("invuln")
                    .Run(args =>
                    {
                        void StopViolence(On.Creature.orig_Violence orig, Creature self, BodyChunk source, Vector2? directionAndMomentum, BodyChunk hitChunk, PhysicalObject.Appendage.Pos hitAppendage, Creature.DamageType type, float damage, float stunBonus)
                        {
                            if (self.Template?.type != CreatureTemplate.Type.Slugcat)
                                orig(self, source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);
                        }

                        void StopDeath(On.Player.orig_Die orig, Player self) { }

                        void StopHarm(On.Player.orig_Update orig, Player self, bool eu)
                        {
                            self.airInLungs = 1f;
                            self.stun = 0;
                            self.rainDeath = 0f;
                            self.AllGraspsLetGoOfThisObject(true);
                            orig(self, eu);
                        }

                        try
                        {
                            if (args[0] is not "nothing" and not "death" and not "everything")
                            {
                                WriteLine("Failed to toggle invulnerability!");
                                return;
                            }

                            if (args[0] == "nothing")
                            {
                                if (hooks.Count > 0)
                                {
                                    foreach (var hook in hooks)
                                        hook.Dispose();
                                    hooks.Clear();
                                }

                                WriteLine("Disabled invulnerability.");
                            }
                            else
                            {
                                if (hooks.Count == 0)
                                    hooks.Add(new Hook(typeof(Player).GetMethod("Die"), (On.Player.hook_Die)StopDeath));

                                if (hooks.Count < 3 && args[0] == "everything")
                                {
                                    hooks.Add(new Hook(typeof(Creature).GetMethod("Violence"), (On.Creature.hook_Violence)StopViolence));
                                    hooks.Add(new Hook(typeof(Player).GetMethod("Update"), (On.Player.hook_Update)StopHarm));
                                }

                                WriteLine("Enabled invulnerability" + (args[0] == "death" ? " against death." : " against all harm."));
                            }
                        }
                        catch
                        {
                            WriteLine("Failed to toggle invulnerability!");
                        }
                    })
                    .Help("invuln [to]")
                    .AutoComplete(new string[][] {
                        new string[] { "everything", "death", "nothing" }
                    })
                    .Register();
            }

            // Locks or unlocks sandbox tokens
            new CommandBuilder("unlock")
                .Run(args =>
                {
                    if(args.Length == 0)
                    {
                        WriteLine("unlock list");
                        WriteLine("unlock give [ID]");
                        WriteLine("unlock take [ID]");
                        return;
                    }

                    try
                    {
                        PlayerProgression.MiscProgressionData miscProg = RW.progression.miscProgressionData;

                        if(args[0] == "list")
                        {
                            var sandboxIDs = (MultiplayerUnlocks.SandboxUnlockID[])Enum.GetValues(typeof(MultiplayerUnlocks.SandboxUnlockID));
                            var levelIDs = (MultiplayerUnlocks.LevelUnlockID[])Enum.GetValues(typeof(MultiplayerUnlocks.LevelUnlockID));
                            WriteLine($"Sandbox tokens (unlocked): {string.Join(", ", sandboxIDs.Where(miscProg.GetTokenCollected).Select(id => id.ToString()).ToArray())}");
                            WriteLine($"Sandbox tokens (locked): {string.Join(", ", sandboxIDs.Where(id => !miscProg.GetTokenCollected(id)).Select(id => id.ToString()).ToArray())}", Color.Lerp(DefaultColor, Color.grey, 0.4f));
                            WriteLine($"Level tokens (unlocked): {string.Join(", ", levelIDs.Where(miscProg.GetTokenCollected).Select(id => id.ToString()).ToArray())}");
                            WriteLine($"Level tokens (locked): {string.Join(", ", levelIDs.Where(id => !miscProg.GetTokenCollected(id)).Select(id => id.ToString()).ToArray())}", Color.Lerp(DefaultColor, Color.grey, 0.4f));
                            return;
                        }

                        if(args[0] != "give" && args[0] != "take")
                        {
                            WriteLine("Unknown subcommand!");
                            return;
                        }
                        bool give = args[0] == "give";
                        MultiplayerUnlocks.SandboxUnlockID? sid = null;
                        MultiplayerUnlocks.LevelUnlockID? lid = null;
                        try
                        {
                            sid = (MultiplayerUnlocks.SandboxUnlockID)Enum.Parse(typeof(MultiplayerUnlocks.SandboxUnlockID), args[1], true);
                        }
                        catch { }

                        try
                        {
                            lid = (MultiplayerUnlocks.LevelUnlockID)Enum.Parse(typeof(MultiplayerUnlocks.LevelUnlockID), args[1], true);
                        }
                        catch { }

                        if (sid == null && lid == null)
                        {
                            WriteLine("No valid unlock ID was given! Try running \"unlock list\".");
                            return;
                        }

                        switch(args[0])
                        {
                            case "give":
                                if (lid != null)
                                    miscProg.SetTokenCollected(lid.Value);
                                else
                                    miscProg.SetTokenCollected(sid.Value);
                                WriteLine($"Unlocked \"{lid?.ToString() ?? sid.ToString()}\".");
                                break;
                            case "take":
                                if (lid != null)
                                    miscProg.levelTokens[(int)lid.Value] = false;
                                else
                                    miscProg.sandboxTokens[(int)sid.Value] = false;
                                WriteLine($"Locked \"{lid?.ToString() ?? sid.ToString()}\".");
                                break;
                        }
                    }
                    catch
                    {
                        WriteLine("Failed to get or set unlock!");
                    }
                })
                .Help("unlock [subcommand?] [ID?]")
                .AutoComplete(args =>
                {
                    switch(args.Length)
                    {
                        case 0: return new string[] { "list", "give", "take" };
                        case 1:
                            switch(args[0])
                            {
                                case "list":
                                    return null;
                                case "give":
                                case "take":
                                    try
                                    {
                                        PlayerProgression.MiscProgressionData miscProg = RW.progression.miscProgressionData;
                                        bool onlyUnlocked = args[0] == "take";

                                        var sids = (MultiplayerUnlocks.SandboxUnlockID[])Enum.GetValues(typeof(MultiplayerUnlocks.SandboxUnlockID));
                                        var lids = (MultiplayerUnlocks.LevelUnlockID[])Enum.GetValues(typeof(MultiplayerUnlocks.LevelUnlockID));

                                        return sids.Where(sid => miscProg.GetTokenCollected(sid) == onlyUnlocked).Select(sid => sid.ToString())
                                            .Concat(lids.Where(lid => miscProg.GetTokenCollected(lid) == onlyUnlocked).Select(lid => lid.ToString()));
                                    }
                                    catch
                                    {
                                        return null;
                                    }
                            }
                            goto default;
                        default:
                            return null;
                    }
                })
                .Register();

            // Make the game over popup appear
            // Useful when an invuln player dies
            new CommandBuilder("game_over")
                .RunGame((game, args) => game.GameOver(null))
                .Register();

            // Toggles the Mark
            new CommandBuilder("the_mark")
                .RunGame((game, args) =>
                {
                    try
                    {
                        var dpsd = game.GetStorySession.saveState.deathPersistentSaveData;
                        dpsd.theMark = !dpsd.theMark;
                        if (dpsd.theMark) WriteLine("Given the Mark!");
                        else WriteLine("Taken the Mark!");
                    }
                    catch
                    {
                        WriteLine("Failed to toggle the Mark!");
                    }
                })
                .Register();

            // Toggles the Glow
            new CommandBuilder("the_glow")
                .RunGame((game, args) =>
                {
                    try
                    {
                        var save = game.GetStorySession.saveState;
                        save.theGlow = !save.theGlow;
                        foreach(var ply in game.Players)
                        {
                            if (ply?.realizedObject is Player realPly)
                                realPly.glowing = save.theGlow;
                        }
                        if (save.theGlow) WriteLine("Given the Glow!");
                        else WriteLine("Taken the Glow!");
                    }
                    catch
                    {
                        WriteLine("Failed to toggle the Glow!");
                    }
                })
                .Register();

            new CommandBuilder("food")
                .RunGame((game, args) =>
                {
                    try
                    {
                        void AddFood(Player ply, int add)
                        {
                            if (add < 0)
                            {
                                add = Math.Max(add, -ply.playerState.foodInStomach);
                                foreach(var cam in ply.room.game.cameras)
                                {
                                    if(cam.hud.owner == ply && cam.hud.foodMeter is HUD.FoodMeter fm)
                                    {
                                        for (int i = add; i < 0; i++)
                                        {
                                            if (fm.showCount > 0)
                                                fm.circles[--fm.showCount].EatFade();
                                        }
                                    }
                                }
                            }
                            ply.AddFood(add);
                        }

                        int amount = int.Parse(args[0]);
                        int plyNum = args.Length > 1 ? int.Parse(args[1]) : -1;
                        if(args.Length > 1)
                        {
                            AddFood(game.Players[int.Parse(args[1])].realizedObject as Player, amount);
                        }
                        else
                        {
                            foreach (var ply in game.Players.Select(ply => ply.realizedObject as Player))
                            {
                                if (ply == null) continue;
                                AddFood(ply, amount);
                            }
                        }
                    }
                    catch
                    {
                        WriteLine("Failed to add food!");
                    }
                })
                .Help("food [add_amount] [player?]")
                .AutoComplete(new string[][] {
                    null,
                    new string[] { "0", "1", "2", "3" }
                })
                .Register();

#endregion Players


            // Commands related to objects
            #region Objects

            // Spawn an object by type
            new CommandBuilder("object")
                .RunGame((game, args) =>
                {
                    try
                    {
                        var pos = SpawnRoom.GetWorldCoordinate(SpawnPos);
                        var id = game.GetNewID();
                        var type = (AbstractPhysicalObject.AbstractObjectType)Enum.Parse(typeof(AbstractPhysicalObject.AbstractObjectType), args[0], true);
                        AbstractPhysicalObject apo = null;
                        switch (type)
                        {
                            case AbstractPhysicalObject.AbstractObjectType.Spear: apo = new AbstractSpear(game.world, null, pos, id, args.Skip(1).Contains("explosive")); break;
                            case AbstractPhysicalObject.AbstractObjectType.BubbleGrass: apo = new BubbleGrass.AbstractBubbleGrass(game.world, null, pos, id, 1f, -1, -1, null); break;
                            case AbstractPhysicalObject.AbstractObjectType.SporePlant: apo = new SporePlant.AbstractSporePlant(game.world, null, pos, id, -1, -1, null, false, true); break;
                            case AbstractPhysicalObject.AbstractObjectType.WaterNut: apo = new WaterNut.AbstractWaterNut(game.world, null, pos, id, -1, -1, null, args.Skip(1).Contains("swollen")); break;
                            case AbstractPhysicalObject.AbstractObjectType.DataPearl: break;
                            default:
                                if (AbstractConsumable.IsTypeConsumable(type)) apo = new AbstractConsumable(game.world, type, null, pos, id, -1, -1, null);
                                else apo = new AbstractPhysicalObject(game.world, type, null, pos, id); break;
                        }
                        SpawnRoom.abstractRoom.AddEntity(apo);
                        apo.RealizeInRoom();
                    }
                    catch (Exception e)
                    {
                        WriteLine("Failed to spawn object! See console log for more info.");
                        Debug.Log("Failed to spawn object!\n" + e.ToString());
                    }
                })
                .Help("object [pos?] [type] [tag1?] [tag2?] ...")
                .AutoComplete(new string[][] {
                    Enum.GetNames(typeof(AbstractPhysicalObject.AbstractObjectType))
                })
                .Register();

            // Spawns a pearl by ID
            new CommandBuilder("pearl")
                .RunGame((game, args) =>
                {
                    try
                    {
                        if (args.Length == 0)
                        {
                            // Print all known pearl types
                            var names = Enum.GetNames(typeof(DataPearl.AbstractDataPearl.DataPearlType));
                            WriteLine("Pearls: " + string.Join(", ", names));
                            return;
                        }

                        var type = (DataPearl.AbstractDataPearl.DataPearlType)Enum.Parse(typeof(DataPearl.AbstractDataPearl.DataPearlType), args[0], true);
                        var pearl = new DataPearl.AbstractDataPearl(game.world, AbstractPhysicalObject.AbstractObjectType.DataPearl, null, SpawnRoom.GetWorldCoordinate(SpawnPos), game.GetNewID(), -1, -1, null, type);

                        SpawnRoom.abstractRoom.AddEntity(pearl);
                        pearl.pos = SpawnRoom.GetWorldCoordinate(SpawnPos);
                        pearl.RealizeInRoom();
                        pearl.realizedObject.firstChunk.HardSetPosition(SpawnPos);

                        WriteLine($"Spawned pearl: {type}");
                    }
                    catch
                    {
                        WriteLine("Could not spawn pearl!");
                    }
                })
                .Help("pearl [pearl_type?]")
                .AutoComplete(new string[][] {
                    Enum.GetNames(typeof(DataPearl.AbstractDataPearl.DataPearlType))
                })
                .Register();

            // Randomize the fields of all selected realized objects
            new CommandBuilder("corrupt")
                .RunGame((game, args) =>
                {
                    try
                    {
                        float chance = 0.1f;
                        float strength = 5.0f;

                        if (args.Length > 1) chance = float.Parse(args[1]);
                        if (args.Length > 2) strength = float.Parse(args[2]);

                        foreach (var obj in Selection.SelectAbstractObjects(game, args.Length > 0 ? args[0] : null).Select(obj => obj.realizedObject).Where(obj => obj != null))
                        {
                            Corrupt(obj, chance, strength);
                            if (obj.graphicsModule is GraphicsModule gm)
                            {
                                Corrupt(gm, chance, strength);
                            }
                        }
                    }
                    catch
                    {
                        WriteLine("Corruption failed!");
                    }
                })
                //.Help("corrupt [selector?] [chance: 0.1] [strength: 5.0]")
                .HideHelp()
                .Register();

            #endregion Objects
        }

        private static string[] keyNames;
        private static IEnumerable<string> GetKeyNames()
        {
            if (keyNames == null)
                keyNames = Enum.GetNames(typeof(KeyCode));
            return keyNames;
        }

        private static IBindEvent EventFromKey(string key, string keyEvent)
        {
            KeyMode mode = keyEvent switch
            {
                "down" => KeyMode.Down,
                "up" => KeyMode.Up,
                "hold_down" => KeyMode.HoldDown,
                "hold_up" => KeyMode.HoldUp,
                _ => KeyMode.Down
            };

            // Generate event from key
            try
            {
                KeyCode keycode = (KeyCode)Enum.Parse(typeof(KeyCode), key, true);
                return new KeyCodeEvent(keycode, mode);
            }
            catch
            {
                // Make sure the key is valid
                try
                {
                    Input.GetKey(key);
                }
                catch
                {
                    return null;
                }
                return new KeyNameEvent(key, mode);
            }
        }

        private static bool pauseOnError;
        private static void WriteLogToConsole(string logString, string stackTrace, LogType type)
        {
            if (!logColors.TryGetValue(type, out Color color)) color = Color.white;

            WriteLine($"[{type}] {logString}", color);
            if(type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            {
                WriteLine(stackTrace, color);
                if (pauseOnError)
                    ForceOpen(true);
            }
        }

        private static readonly string[] fragileFields = new string[]
        {
            nameof(PhysicalObject.collisionLayer)
        };

        static void Corrupt(object obj, float chance, float strength, int depth = 5)
        {
            if (obj == null) return;

            foreach (var field in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (fragileFields.Contains(field.Name)) continue;
                if (UnityEngine.Random.value >= chance) continue;

                object val = field.GetValue(obj);
                if (val == null) continue;

                float rand = UnityEngine.Random.value * 2f - 1f;
                switch (Type.GetTypeCode(field.FieldType))
                {
                    // Bool
                    case TypeCode.Boolean: val = rand < 0.5f; break;

                    // Numeric types
                    case TypeCode.Byte: val = (byte)((int)val + (int)(rand * strength)); break;
                    case TypeCode.Char: val = (char)((int)val + (int)(rand * strength)); break;
                    case TypeCode.Double: val = (double)val + rand * strength; break;
                    case TypeCode.Int16: val = (short)val + (short)(rand * strength); break;
                    case TypeCode.Int32: val = (int)val + (int)(rand * strength); break;
                    case TypeCode.Int64: val = (long)val + (long)(rand * strength); break;
                    case TypeCode.SByte: val = (sbyte)val + (sbyte)(rand * strength); break;
                    case TypeCode.Single: val = (float)val + rand * strength; break;
                    case TypeCode.UInt16: val = (ushort)val + (short)(rand * strength); break;
                    case TypeCode.UInt32: val = (uint)((uint)val + (int)(rand * strength)); break;
                    case TypeCode.UInt64: val = (ulong)((long)(ulong)val + (long)(rand * strength)); break;

                    // String
                    case TypeCode.String:
                        char[] chars = ((string)val).ToCharArray();
                        for (int i = 0; i < chars.Length; i++)
                        {
                            if (UnityEngine.Random.value < strength)
                                chars[i] = (char)UnityEngine.Random.Range(0x0000, 0x10000);
                        }
                        val = new string(chars);
                        break;

                    // Struct
                    case TypeCode.Object:
                        if(field.FieldType.IsValueType)
                        {
                            Corrupt(val, chance, strength, depth - 1);
                        }
                        break;
                }

                field.SetValue(obj, val);
            }
        }

        // May be used to clean up outputs that contain a lot of repeated lines
        private class DedupCache<T> : IEnumerable<DedupCache<T>.Entry>
        {
            private List<Entry> entries;

            public void Add(T value)
            {
                entries ??= new();
                for(int i = entries.Count - 1; i >= 0; i--)
                {
                    var entry = entries[i];
                    if ((value == null && entry.Value == null) || entry.Value.Equals(value))
                    {
                        entries[i] = new Entry(entry.Value, entry.Count + 1);
                        return;
                    }
                }
                entries.Add(new Entry(value, 1));
            }

            public IEnumerator<Entry> GetEnumerator() => (entries ?? Enumerable.Empty<Entry>()).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public IEnumerable<string> AsStrings()
            {
                if (entries == null)
                    yield break;

                for (int i = 0; i < entries.Count; i++)
                {
                    var pair = entries[i];
                    if (pair.Count > 1) yield return $"{pair.Value} (x{pair.Count})";
                    else yield return pair.Value.ToString();
                }
            }

            public struct Entry
            {
                public T Value { get; }
                public int Count { get; }

                public Entry(T value, int count)
                {
                    Value = value;
                    Count = count;
                }
            }
        }
    }
}
