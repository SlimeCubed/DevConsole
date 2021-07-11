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
                            while (self.myTimeStacker > 1f)
                            {
                                self.Update();
                                self.myTimeStacker -= 1f;

                                // Extra graphics updates are needed to reduce visual artifacts
                                if (self.myTimeStacker > 1f)
                                    self.GrafUpdate(self.myTimeStacker);
                            }
                            self.GrafUpdate(self.myTimeStacker);
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
            }

            // Control positioning of commands
            new CommandBuilder("target_pos")
                .Run(args =>
                {
                    if (args.Length == 0)
                    {
                        WriteLine("target_pos player [player_num: 0]");
                        WriteLine("target_pos mouse [camera_num: 0]");
                        WriteLine("target_pos camera [camera_num: 0]");
                    }
                    else
                    {
                        try
                        {
                            int num = (args.Length > 1) ? int.Parse(args[1]) : 0;
                            switch (args[0])
                            {
                                case "player":
                                    Positioning.getPos = game => new Positioning.RoomPos(game.Players[num].realizedObject.room, game.Players[num].realizedObject.firstChunk.pos);
                                    WriteLine("Commands will target the player.");
                                    break;
                                case "mouse":
                                    Positioning.getPos = game => new Positioning.RoomPos(game.cameras[num].room, game.cameras[num].pos + (Vector2)Input.mousePosition);
                                    WriteLine("Commands will target the mouse.");
                                    break;
                                case "camera":
                                    Positioning.getPos = game => new Positioning.RoomPos(game.cameras[num].room, game.cameras[num].pos + game.cameras[num].sSize / 2f);
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
                .Help("target_pos [target?] [arg?]")
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

                    IBindEvent e = EventFromKey(args[0]);
                    if (e == null)
                    {
                        WriteLine($"Couldn't find key: {args[0]}");
                        return;
                    }

                    if (args.Length == 1)
                    {
                        // Only the key was specified
                        // Get and print all binds
                        var boundCommands = Bindings.GetBoundCommands(e);
                        if (boundCommands.Length == 0)
                            WriteLine("No commands bound.");
                        else
                            foreach (var cmd in boundCommands)
                                WriteLine(cmd);
                    }
                    else
                    {
                        // A list of commands was specified
                        // Bind them
                        foreach (var cmd in args.Skip(1))
                        {
                            if (cmd == "") continue;
                            Bindings.Bind(e, cmd);
                        }
                    }
                })
                .Help("bind [keycode] [commmand1?] [command2?] ...")
                .AutoComplete(args =>
                {
                    if (args.Length == 0) return GetKeyNames();
                    else return null;
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

                    IBindEvent e = EventFromKey(args[0]);
                    if (e == null)
                    {
                        WriteLine($"Couldn't find key: {args[0]}");
                        return;
                    }

                    if (args.Length == 1)
                    {
                        // Only the key was specified
                        // Unbind all
                        Bindings.UnbindAll(e);
                    }
                    else
                    {
                        // A list of commands was specified
                        // Unbind them all
                        foreach (var cmd in args.Skip(1))
                        {
                            if (cmd == "") continue;
                            Bindings.Unbind(e, cmd);
                        }
                    }
                })
                .Help("unbind [keycode] [commmand1?] [command2?] ...")
                .AutoComplete(args =>
                {
                    if (args.Length == 0) return GetKeyNames();
                    else return null;
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

                        var crit = new AbstractCreature(
                            game.world,
                            template,
                            null,
                            SpawnRoom.GetWorldCoordinate(SpawnPos),
                            game.GetNewID()
                        );
                        SpawnRoom.abstractRoom.AddEntity(crit);
                        crit.RealizeInRoom();
                    }
                    catch { WriteLine("Failed to spawn creature!"); }
                })
                .Help("creature [type]")
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

            #endregion Creatures


            // Commands related to the player
            #region Players

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

                        Room.Tile RemoveTiles(On.Room.orig_GetTile_3 orig, Room self, int x, int y)
                        {
                            return noclip ? new Room.Tile(x, y, Room.Tile.TerrainType.Air, false, false, false, 0, 0) : orig(self, x, y);
                        }

                        if (hooks.Count == 0)
                        {
                            hooks.Add(new Hook(typeof(Player).GetMethod("Update"), (On.Player.hook_Update)NoClip));
                            hooks.Add(new Hook(typeof(Room).GetMethod("FloatWaterLevel"), (On.Room.hook_FloatWaterLevel)HighWaterLevel));
                            hooks.Add(new Hook(typeof(Room).GetMethod("GetTile", new Type[] { typeof(int), typeof(int) }), (On.Room.hook_GetTile_3)RemoveTiles));
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
                            if (hooks.Count == 0)
                            {
                                hooks.Add(new Hook(typeof(Player).GetMethod("Die"), (On.Player.hook_Die)StopDeath));

                                if (args.Length == 0 || !bool.Parse(args[0]))
                                {
                                    hooks.Add(new Hook(typeof(Creature).GetMethod("Violence"), (On.Creature.hook_Violence)StopViolence));
                                    hooks.Add(new Hook(typeof(Player).GetMethod("Update"), (On.Player.hook_Update)StopHarm));
                                }
                                WriteLine("Enabled invulnerability.");
                            }
                            else
                            {
                                foreach (var hook in hooks)
                                    hook.Dispose();
                                hooks.Clear();
                                WriteLine("Disabled invulnerability.");
                            }
                        }
                        catch
                        {
                            WriteLine("Failed to toggle invulnerability!");
                        }
                    })
                    .Help("invuln [death_only: false]")
                    .AutoComplete(new string[][] {
                        new string[] { "true", "false" }
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
                        PlayerProgression.MiscProgressionData miscProg = UnityEngine.Object.FindObjectOfType<RainWorld>().progression.miscProgressionData;

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
                                        PlayerProgression.MiscProgressionData miscProg = UnityEngine.Object.FindObjectOfType<RainWorld>()?.progression?.miscProgressionData;
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
                    catch { WriteLine("Failed to spawn object!"); }
                })
                .Help("object [type] [tag1?] [tag2?] ...")
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

            #endregion Objects
        }

        private static string[] keyNames;
        private static IEnumerable<string> GetKeyNames()
        {
            if (keyNames == null)
                keyNames = Enum.GetNames(typeof(KeyCode));
            return keyNames;
        }

        private static IBindEvent EventFromKey(string key)
        {
            KeyMode mode = KeyMode.Down;

            // Find mode prefix
            if (key.Length > 1)
            {
                bool trimKey = true;

                switch (key[0])
                {
                    default: trimKey = false; goto case '+';
                    case '+': mode = KeyMode.Down; break;
                    case '_': mode = KeyMode.HoldDown; break;
                    case '-': mode = KeyMode.Up; break;
                    case '^': mode = KeyMode.HoldUp; break;
                }

                if (trimKey) key = key.Substring(1);
            }
            
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

        private static void WriteLogToConsole(string logString, string stackTrace, LogType type)
        {
            WriteLine($"[{type}] {logString}", logColors.TryGetValue(type, out Color col) ? col : Color.white);
        }
    }
}
