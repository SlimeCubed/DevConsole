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
        private static Dictionary<LogType, Color> logColors = new Dictionary<LogType, Color>()
        {
            { LogType.Error, new Color(0.7f, 0f, 0f) },
            { LogType.Assert, new Color(1f, 0.7f, 0f) },
            { LogType.Exception, Color.red },
            { LogType.Log, Color.white },
            { LogType.Warning, Color.yellow }
        };
        
        // Fields for the log viewer
        private static bool showingDebug = false;
        private static FieldInfo Application_s_LogCallback = typeof(Application).GetField("s_LogCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        public static void RegisterCommands()
        {
            // Commands that don't fit any other category
            #region Misc

            // Mirrors all Debug.Log* calls to the dev console
            RegisterCommand(new SimpleCommand("show_debug", args =>
            {
                var cb = (Application.LogCallback)Application_s_LogCallback.GetValue(null);
                if (showingDebug) cb -= WriteLogToConsole;
                else cb += WriteLogToConsole;
                showingDebug = !showingDebug;
                Application.RegisterLogCallback(cb);
                WriteLine(showingDebug ? "Debug messages will be displayed here." : "Debug messages will no longer be displayed here.");
            }));

            // Clears the console
            RegisterCommand(new SimpleCommand("clear", args =>
            {
                Clear();
                WriteHeader();
            }));

            // Throws an exception
            // Useful, right?
            RegisterCommand(new SimpleCommand("throw", args =>
            {
                if (args.Length > 0) throw new Exception(string.Join(" ", args));
                else throw new Exception();
            })
            { Summary = "throw [message?]" });

            // Writes a list of lines to the console
            RegisterCommand(new SimpleCommand("echo", args =>
            {
                foreach (var line in args)
                    WriteLine(line);
            })
            { Summary = "echo [line1?] [line2?] ..." });

            // Runs a command and suppresses all output for its duration
            RegisterCommand(new SimpleCommand("silence", args =>
            {
                if (args.Length == 0)
                    WriteLine("No command given to silence!");
                else
                    foreach (var cmd in args)
                        RunCommandSilent(cmd);
            })
            { Summary = "silence [command1] [command2?] [command3?] ..." });

            // Speeds up the game
            {
                Hook hook = null;

                RegisterCommand(new SimpleCommand("game_speed", args =>
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
                            if(self.myTimeStacker > 1f)
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
                { Summary = "game_speed [speed_multiplier?]" });
            }

            // Manipulate the cycle timer
            RegisterCommand(new GameCommand("rain_timer", (game, args) =>
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
            { Summary = "rain_timer [subcommand?] [arg?]" });

            #endregion Misc


            // Commands related to event-command bindings
            #region Bindings

            // Binds a key to a command
            RegisterCommand(new SimpleCommand("bind", args =>
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
            { Summary = "bind [keycode] [commmand1?] [command2?] ..." });

            // Unbinds a key
            RegisterCommand(new SimpleCommand("unbind", args =>
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
            { Summary = "unbind [keycode] [commmand1?] [command2?] ..." });

            // Unbinds everything
            RegisterCommand(new SimpleCommand("unbind_all", args => Bindings.UnbindAll()));

            // Creates a command alias
            RegisterCommand(new SimpleCommand("alias", args =>
            {
                if (args.Length == 0)
                    WriteLine("No alias was given!");
                else if (args.Length == 1)
                    Aliases.RemoveAlias(args[0]);
                else
                    Aliases.SetAlias(args[0], args.Skip(1).ToArray());
            })
            { Summary = "alias [name] [command1?] [command2?] ..." });

            #endregion Bindings


            // Commands related to creatures
            #region Creatures

            // Spawns a single creature by type
            RegisterCommand(new GameCommand("creature", (game, args) =>
            {
                try
                {
                    var player = game.Players[0].realizedCreature as Player;
                    new AbstractCreature(
                        game.world,
                        StaticWorld.GetCreatureTemplate(WorldLoader.CreatureTypeFromString(args[0]) ?? (CreatureTemplate.Type)Enum.Parse(typeof(CreatureTemplate.Type), args[0], true)),
                        null,
                        player.coord,
                        game.GetNewID()
                    ).RealizeInRoom();
                }
                catch { WriteLine("Failed to spawn creature!"); }
            })
            { Summary = "creature [type]" });

            // Kills everything in the current region
            RegisterCommand(new GameCommand("remove_crits", (game, args) =>
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
            { Summary = "remove_crits [respawn: true]" });

            #endregion Creatures


            // Commands related to the player
            #region Players

            // Allows players to swim through everything
            {
                bool noclip = false;
                bool remove = false;
                List<Hook> hooks = new List<Hook>();

                RegisterCommand(new SimpleCommand("noclip", args =>
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
                }));
            }

            // Changes the player's current karma
            RegisterCommand(new GameCommand("karma", (game, args) =>
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
            { Summary = "karma [value?]" });

            // Changes the player's karma cap
            RegisterCommand(new GameCommand("karma_cap", (game, args) =>
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
            { Summary = "karma_cap [value?]" });

            // Makes the player mostly invulnerable
            {
                List<Hook> hooks = new List<Hook>();

                RegisterCommand(new SimpleCommand("invuln", args =>
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
                { Summary = "invuln [death_only: false]" });
            }

            #endregion Players


            // Commands related to objects
            #region Objects

            // Spawn an object by type
            RegisterCommand(new GameCommand("object", (game, args) =>
            {
                try
                {
                    var player = game.Players[0].realizedCreature as Player;
                    var pos = player.coord;
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
                    apo.RealizeInRoom();
                }
                catch { WriteLine("Failed to spawn object!"); }
            })
            { Summary = "object [type] [tag1?] [tag2?] ..." });

            // Spawns a pearl by ID
            RegisterCommand(new GameCommand("pearl", (game, args) =>
            {
                try
                {
                    if(args.Length == 0)
                    {
                        // Print all known pearl types
                        var names = Enum.GetNames(typeof(DataPearl.AbstractDataPearl.DataPearlType));
                        int inLine = 0;
                        StringBuilder sb = new StringBuilder();
                        for(int i = 0; i < names.Length; i++)
                        {
                            if (inLine != 0) sb.Append(", ");
                            sb.Append(names[i]);
                            if(inLine++ >= 10)
                            {
                                inLine = 0;
                                WriteLine(sb.ToString());
                                sb = new StringBuilder();
                            }
                        }
                        if (sb.Length > 0)
                            WriteLine(sb.ToString());
                        return;
                    }

                    var type = (DataPearl.AbstractDataPearl.DataPearlType)Enum.Parse(typeof(DataPearl.AbstractDataPearl.DataPearlType), args[0], true);
                    var player = (Player)game.Players[0].realizedCreature;
                    var pearl = new DataPearl.AbstractDataPearl(game.world, AbstractPhysicalObject.AbstractObjectType.DataPearl, null, player.coord, game.GetNewID(), -1, -1, null, type);

                    player.room.abstractRoom.AddEntity(pearl);
                    pearl.pos = player.coord;
                    pearl.RealizeInRoom();
                    pearl.realizedObject.firstChunk.HardSetPosition(player.mainBodyChunk.pos);

                    WriteLine($"Spawned pearl: {type}");
                }
                catch
                {
                    WriteLine("Could not spawn pearl!");
                }
            })
            { Summary = "pearl [pearl_type?]" });

            #endregion Objects
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
