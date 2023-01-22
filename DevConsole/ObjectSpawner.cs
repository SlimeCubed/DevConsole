using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using ObjType = AbstractPhysicalObject.AbstractObjectType;
using MSCObjType = MoreSlugcats.MoreSlugcatsEnums.AbstractObjectType;
using CritType = CreatureTemplate.Type;
using MSCCritType = MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType;
using AC = DevConsole.Autocomplete;
using RWCustom;

namespace DevConsole
{
    /// <summary>
    /// Creates physical objects from string arrays.
    /// </summary>
    public static class ObjectSpawner
    {
        private static readonly Dictionary<string, Type> typeMap = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<Type, ConstructorInfo[]> typeCtors = new();
        private static readonly Regex entityID = new(@"^ID\.\d+\.\d+(\.\d+)?$");

        private static readonly HashSet<string> dllBlacklist = new()
        {
            "0Harmony",
            "0Harmony20",
            "Assembly-CSharp-firstpass",
            "BepInEx.Harmony",
            "BepInEx.MonoMod.Loader",
            "BepInEx.MultiFolderLoader",
            "BepInEx.Preloader",
            "BepInEx",
            "Dragons.PublicDragon",
            "GoKit",
            "HOOKS-Assembly-CSharp",
            "HarmonyXInterop",
            "Mono.Cecil.Mdb",
            "Mono.Cecil.Pdb",
            "Mono.Cecil.Rocks",
            "Mono.Cecil",
            "Mono.Security",
            "MonoMod.Common",
            "MonoMod.RuntimeDetour",
            "MonoMod.Utils",
            "MonoMod",
            "MonoPosixHelper",
            "Newtonsoft.Json",
            "PUBLIC-Assembly-CSharp",
            "Rewired_Core",
            "Rewired_DirectInput",
            "Rewired_Windows",
            "SonyNP",
            "SonyPS4SaveData",
            "StovePCSDK.NET",
            "System.ComponentModel.Composition",
            "System.Configuration",
            "System.Core",
            "System.Data",
            "System.Diagnostics.StackTrace",
            "System.Drawing",
            "System.EnterpriseServices",
            "System.Globalization.Extensions",
            "System.IO.Compression.FileSystem",
            "System.IO.Compression",
            "System.Net.Http",
            "System.Numerics",
            "System.Runtime.Serialization.Xml",
            "System.Runtime.Serialization",
            "System.ServiceModel.Internals",
            "System.Transactions",
            "System.Xml.Linq",
            "System.Xml.XPath.XDocument",
            "System.Xml",
            "System",
            "UnityEngine.AIModule",
            "UnityEngine.ARModule",
            "UnityEngine.AccessibilityModule",
            "UnityEngine.AndroidJNIModule",
            "UnityEngine.AnimationModule",
            "UnityEngine.AssetBundleModule",
            "UnityEngine.AudioModule",
            "UnityEngine.ClothModule",
            "UnityEngine.ClusterInputModule",
            "UnityEngine.ClusterRendererModule",
            "UnityEngine.CoreModule",
            "UnityEngine.CrashReportingModule",
            "UnityEngine.DSPGraphModule",
            "UnityEngine.DirectorModule",
            "UnityEngine.GIModule",
            "UnityEngine.GameCenterModule",
            "UnityEngine.GridModule",
            "UnityEngine.HotReloadModule",
            "UnityEngine.IMGUIModule",
            "UnityEngine.ImageConversionModule",
            "UnityEngine.InputLegacyModule",
            "UnityEngine.InputModule",
            "UnityEngine.JSONSerializeModule",
            "UnityEngine.LocalizationModule",
            "UnityEngine.ParticleSystemModule",
            "UnityEngine.PerformanceReportingModule",
            "UnityEngine.Physics2DModule",
            "UnityEngine.PhysicsModule",
            "UnityEngine.ProfilerModule",
            "UnityEngine.RuntimeInitializeOnLoadManagerInitializerModule",
            "UnityEngine.ScreenCaptureModule",
            "UnityEngine.SharedInternalsModule",
            "UnityEngine.SpriteMaskModule",
            "UnityEngine.SpriteShapeModule",
            "UnityEngine.StreamingModule",
            "UnityEngine.SubstanceModule",
            "UnityEngine.SubsystemsModule",
            "UnityEngine.TLSModule",
            "UnityEngine.TerrainModule",
            "UnityEngine.TerrainPhysicsModule",
            "UnityEngine.TextCoreModule",
            "UnityEngine.TextRenderingModule",
            "UnityEngine.TilemapModule",
            "UnityEngine.UI",
            "UnityEngine.UIElementsModule",
            "UnityEngine.UIElementsNativeModule",
            "UnityEngine.UIModule",
            "UnityEngine.UNETModule",
            "UnityEngine.UmbraModule",
            "UnityEngine.UnityAnalyticsModule",
            "UnityEngine.UnityConnectModule",
            "UnityEngine.UnityCurlModule",
            "UnityEngine.UnityTestProtocolModule",
            "UnityEngine.UnityWebRequestAssetBundleModule",
            "UnityEngine.UnityWebRequestAudioModule",
            "UnityEngine.UnityWebRequestModule",
            "UnityEngine.UnityWebRequestTextureModule",
            "UnityEngine.UnityWebRequestWWWModule",
            "UnityEngine.VFXModule",
            "UnityEngine.VRModule",
            "UnityEngine.VehiclesModule",
            "UnityEngine.VideoModule",
            "UnityEngine.VirtualTexturingModule",
            "UnityEngine.WindModule",
            "UnityEngine.XRModule",
            "UnityEngine",
            "UnityPlayer",
            "com.rlabrecque.steamworks.net",
            "mono-2.0-bdwgc",
            "mscorlib",
            "netstandard",
            "steam_api",
            "steam_api64",
            "winhttp",
        };
        private static bool scanned = false;

        private static readonly Dictionary<ObjType, SpawnerInfo> safeObjSpawners = new();
        private static readonly Dictionary<CritType, SpawnerInfo> safeCritSpawners = new();

        internal static void ClearSafeSpawners()
        {
            safeObjSpawners.Clear();
            safeCritSpawners.Clear();
        }

        internal static void RegisterSafeSpawners()
        {
            {
                // No arguments
                var simpleObjs = new ObjType[]
                {
                    ObjType.BlinkingFlower,
                    ObjType.DartMaggot,
                    ObjType.Lantern,
                    ObjType.NSHSwarmer,
                    ObjType.Rock,
                    ObjType.ScavengerBomb,
                    ObjType.SLOracleSwarmer,
                    ObjType.SSOracleSwarmer,
                    MSCObjType.EnergyCell,
                    MSCObjType.SingularityBomb,
                };

                var spawner = new SimpleSpawnerInfo(
                    (_, _) => null,
                    (type, args, id, room, pos) => new AbstractPhysicalObject(room.world, type, null, pos, id)
                );

                foreach (var t in simpleObjs)
                {
                    if ((int)t != -1)
                        RegisterSpawner(t, spawner);
                }
            }

            {
                // No arguments, consumable object
                var consumableObjs = new ObjType[]
                {
                    ObjType.DangleFruit,
                    ObjType.PuffBall,
                    ObjType.KarmaFlower,
                    ObjType.Mushroom,
                    ObjType.FirecrackerPlant,
                    ObjType.FlareBomb,
                    ObjType.SlimeMold,
                    ObjType.JellyFish,
                    ObjType.FlyLure,
                    ObjType.NeedleEgg,
                    MSCObjType.DandelionPeach,
                    MSCObjType.GlowWeed,
                    MSCObjType.GooieDuck,
                    MSCObjType.HRGuard,
                    MSCObjType.MoonCloak,
                    MSCObjType.Seed
                };

                var spawner = new SimpleSpawnerInfo(
                    (_, _) => null,
                    (type, args, id, room, pos) => new AbstractConsumable(room.world, type, null, pos, id, -1, -1, null)
                );

                foreach (var t in consumableObjs)
                {
                    if ((int)t != -1)
                        RegisterSpawner(t, spawner);
                }
            }

            // Unique objects
            RegisterSpawner(ObjType.EggBugEgg, new SimpleSpawnerInfo(
                (_, args) => args.Length == 0 ? new string[] { AC.hintPrefix + "hue: float" } : null,

                (_, args, id, room, pos) =>
                {
                    float hue;
                    if (args.Length > 0)
                    {
                        if (!float.TryParse(args[0], out hue))
                            throw new ArgumentException("Hue must be a number!");
                    }
                    else
                    {
                        hue = UnityEngine.Random.value;
                    }
                    return new EggBugEgg.AbstractBugEgg(room.world, null, pos, id, hue);
                }
            ));

            RegisterSpawner(ObjType.DataPearl, new SimpleSpawnerInfo(
                (_, args) => args.Length == 0 ? DataPearl.AbstractDataPearl.DataPearlType.values.entries : null,

                (type, args, id, room, pos) =>
                {
                    DataPearl.AbstractDataPearl.DataPearlType pearlType;
                    if (args.Length > 0)
                        pearlType = new DataPearl.AbstractDataPearl.DataPearlType(args[0]);
                    else
                        pearlType = DataPearl.AbstractDataPearl.DataPearlType.Misc;

                    if ((int)pearlType == -1)
                        throw new ArgumentException("Invalid pearl type!");

                    return new DataPearl.AbstractDataPearl(room.world, type, null, pos, id, -1, -1, null, pearlType);
                }
            ));

            RegisterSpawner(ObjType.PebblesPearl, new SimpleSpawnerInfo(
                (_, args) => args.Length switch
                {
                    0 => Enumerable.Range(0, 3).Select(x => x.ToString()).Concat(new string[] { AC.hintPrefix + "color: int" }),
                    1 => new string[] { AC.hintPrefix + "number: int" },
                    _ => null
                },

                (_, args, id, room, pos) =>
                {
                    int color;
                    int number;

                    if (args.Length > 0)
                    {
                        if (!int.TryParse(args[0], out color))
                            throw new ArgumentException("Color must be a number!");
                    }
                    else
                        color = UnityEngine.Random.Range(0, 3);

                    if (args.Length > 1)
                    {
                        if (!int.TryParse(args[1], out number))
                            throw new ArgumentException("Number must be a number!");
                    }
                    else
                        number = UnityEngine.Random.Range(0, 10000);

                    return new PebblesPearl.AbstractPebblesPearl(room.world, null, pos, id, -1, -1, null, color, number);
                }
            ));

            RegisterSpawner(ObjType.WaterNut, new SimpleSpawnerInfo(
                AutoCompleteTags("swollen"),

                (_, args, id, room, pos) =>
                {
                    bool swollen = args.Contains("swollen", StringComparer.OrdinalIgnoreCase);
                    return new WaterNut.AbstractWaterNut(room.world, null, pos, id, -1, -1, null, swollen);
                }
            ));

            {
                string[] tags = new string[] { "explosive", "electric" };
                string[] args0 = new string[] { AC.hintPrefix + "hue: float", AC.hintPrefix + "charges: int" };
                var tagsAC = AutoCompleteTags(tags);

                RegisterSpawner(ObjType.Spear, new SimpleSpawnerInfo(
                    (type, args) =>
                    {
                        if (args.Length == 0)
                            return tagsAC(type, args).Concat(args0);
                        else
                            return tagsAC(type, args);
                    },

                    (_, args, id, room, pos) =>
                    {
                        bool explosive = args.Contains("explosive", StringComparer.OrdinalIgnoreCase);
                        bool electric = args.Contains("electric", StringComparer.OrdinalIgnoreCase);

                        if (!tags.Contains(args[0], StringComparer.OrdinalIgnoreCase))
                        {
                            if (electric)
                            {
                                if (!int.TryParse(args[0], out int charges))
                                    throw new ArgumentException("Electric charges must be an integer!");

                                return new AbstractSpear(room.world, null, pos, id, explosive, true)
                                {
                                    electricCharge = charges
                                };
                            }
                            else
                            {
                                if (!float.TryParse(args[0], out float hue))
                                    throw new ArgumentException("Hue must be an integer!");

                                return new AbstractSpear(room.world, null, pos, id, explosive, hue);
                            }
                        }
                        else
                        {
                            return new AbstractSpear(room.world, null, pos, id, explosive, electric);
                        }
                    }
                ));
            }

            RegisterSpawner(ObjType.BubbleGrass, new SimpleSpawnerInfo(
                (_, args) => args.Length == 0 ? new string[] { AC.hintPrefix + "air: float" } : null,

                (_, args, id, room, pos) =>
                {
                    float air = 1f;
                    if (args.Length > 0 && !float.TryParse(args[0], out air))
                        throw new ArgumentException("Air must be a number!");
                    return new BubbleGrass.AbstractBubbleGrass(room.world, null, pos, id, air, -1, -1, null);
                }
            ));

            RegisterSpawner(ObjType.SeedCob, new SimpleSpawnerInfo(
                AutoCompleteTags("dead"),

                (_, args, id, room, pos) =>
                {
                    bool dead = args.Contains("dead", StringComparer.OrdinalIgnoreCase);
                    return new SeedCob.AbstractSeedCob(room.world, null, pos, id, -1, -1, dead, null);
                }
            ));

            RegisterSpawner(ObjType.SporePlant, new SimpleSpawnerInfo(
                AutoCompleteTags("used", "pacified"),

                (_, args, id, room, pos) =>
                {
                    bool used = args.Contains("used", StringComparer.OrdinalIgnoreCase);
                    bool pacified = args.Contains("pacified", StringComparer.OrdinalIgnoreCase);
                    return new SporePlant.AbstractSporePlant(room.world, null, pos, id, -1, -1, null, used, pacified);
                }
            ));

            RegisterSpawner(ObjType.VultureMask, new SimpleSpawnerInfo(
                (_, args) => args.Length switch
                {
                    0 => new string[] { "normal", "king", "scav" },
                    1 => new string[] { AC.hintPrefix + "colorSeed: int" },
                    2 => new string[] { AC.hintPrefix + "sprite: string" },
                    _ => null
                },

                (_, args, id, room, pos) =>
                {
                    bool king = false;
                    bool scavKing = false;
                    string spriteOverride = "";
                    int colorSeed = UnityEngine.Random.Range(0, 10000);

                    if (args.Length > 0)
                    {
                        if (args[0].Equals("scav", StringComparison.OrdinalIgnoreCase))
                            scavKing = true;
                        else if (args[0].Equals("king", StringComparison.OrdinalIgnoreCase))
                            king = true;
                        else if (!args[0].Equals("normal", StringComparison.OrdinalIgnoreCase))
                            throw new ArgumentException("Unknown vulture mask type!");
                    }

                    if (args.Length > 1 && !int.TryParse(args[1], out colorSeed))
                        throw new ArgumentException("Color seed must be an integer!");

                    if (args.Length > 2)
                        spriteOverride = args[2];

                    return new VultureMask.AbstractVultureMask(room.world, null, pos, id, colorSeed, king, scavKing, spriteOverride);
                }
            ));

            if (ModManager.MSC)
            {
                RegisterSpawner(MSCObjType.FireEgg, new SimpleSpawnerInfo(
                    (_, args) => args.Length == 0 ? new string[] { AC.hintPrefix + "hue: float" } : null,

                    (_, args, id, room, pos) =>
                    {
                        float hue;
                        if (args.Length > 0)
                        {
                            if (!float.TryParse(args[0], out hue))
                                throw new ArgumentException("Hue must be a number!");
                        }
                        else
                        {
                            hue = UnityEngine.Random.value;
                        }
                        return new MoreSlugcats.FireEgg.AbstractBugEgg(room.world, null, pos, id, hue);
                    }
                ));

                RegisterSpawner(MSCObjType.Spearmasterpearl, new SimpleSpawnerInfo(
                    (_, args) => null,

                    (_, args, id, room, pos) =>
                    {
                        return new MoreSlugcats.SpearMasterPearl.AbstractSpearMasterPearl(room.world, null, pos, id, -1, -1, null);
                    }
                ));

                RegisterSpawner(MSCObjType.LillyPuck, new SimpleSpawnerInfo(
                    (_, args) => args.Length == 0 ? new string[] { AC.hintPrefix + "bites: int" } : null,

                    (_, args, id, room, pos) =>
                    {
                        int bites = 3;
                        if (args.Length > 0 && !int.TryParse(args[0], out bites))
                            throw new ArgumentException("Bites must be an integer!");
                        return new MoreSlugcats.LillyPuck.AbstractLillyPuck(room.world, null, pos, id, bites, -1, -1, null);
                    }
                ));

                RegisterSpawner(MSCObjType.JokeRifle, new SimpleSpawnerInfo(
                    (_, args) => args.Length switch
                    {
                        0 => JokeRifle.AbstractRifle.AmmoType.values.entries,
                        1 => new string[] { AC.hintPrefix + "ammo: int" },
                        _ => null
                    },

                    (_, args, id, room, pos) =>
                    {
                        JokeRifle.AbstractRifle.AmmoType ammoType = JokeRifle.AbstractRifle.AmmoType.Rock;
                        int ammo = 0;
                        if (args.Length > 0)
                            ammoType = new JokeRifle.AbstractRifle.AmmoType(args[0]);

                        if (args.Length > 1 && !int.TryParse(args[1], out ammo))
                            throw new ArgumentException("Ammo must be an integer!");

                        var rifle = new JokeRifle.AbstractRifle(room.world, null, pos, id, ammoType);
                        rifle.ammo[ammoType] = ammo;
                        return rifle;
                    }
                ));
            }

            // Creatures
            {
                // Only creature tags
                var simpleCreatures = new CritType[]
                {
                    CritType.BigEel,
                    CritType.BigNeedleWorm,
                    CritType.BigSpider,
                    CritType.BlackLizard,
                    CritType.BlueLizard,
                    CritType.BrotherLongLegs,
                    CritType.Centiwing,
                    CritType.CicadaA,
                    CritType.CicadaB,
                    CritType.CyanLizard,
                    CritType.DaddyLongLegs,
                    CritType.Deer,
                    CritType.DropBug,
                    CritType.EggBug,
                    CritType.Fly,
                    CritType.GarbageWorm,
                    CritType.GreenLizard,
                    CritType.JetFish,
                    CritType.KingVulture,
                    CritType.LanternMouse,
                    CritType.Leech,
                    CritType.MirosBird,
                    CritType.Overseer,
                    CritType.PinkLizard,
                    CritType.RedCentipede,
                    CritType.RedLizard,
                    CritType.Salamander,
                    CritType.Scavenger,
                    CritType.SeaLeech,
                    CritType.SmallCentipede,
                    CritType.SmallNeedleWorm,
                    CritType.Snail,
                    CritType.Spider,
                    CritType.SpitterSpider,
                    CritType.TempleGuard,
                    CritType.TubeWorm,
                    CritType.Vulture,
                    CritType.VultureGrub,
                    CritType.WhiteLizard,
                    CritType.YellowLizard,
                    CritType.PoleMimic,
                    CritType.TentaclePlant,
                    CritType.Centipede,

                    MSCCritType.AquaCenti,
                    MSCCritType.BigJelly,
                    MSCCritType.EelLizard,
                    MSCCritType.FireBug,
                    MSCCritType.HunterDaddy,
                    MSCCritType.Inspector,
                    MSCCritType.JungleLeech,
                    MSCCritType.MirosVulture,
                    MSCCritType.MotherSpider,
                    MSCCritType.ScavengerElite,
                    MSCCritType.ScavengerKing,
                    MSCCritType.SlugNPC,
                    MSCCritType.SpitLizard,
                    //MSCCritType.StowawayBug,
                    MSCCritType.TerrorLongLegs,
                    MSCCritType.TrainLizard,
                    MSCCritType.Yeek,
                    MSCCritType.ZoopLizard,

                    //CritType.Slugcat,
                };

                string[] tags = new string[]
                {
                    "Voidsea", "Winter", "Ignorecycle", "TentacleImmune", "Lavasafe", "AlternateForm", "PreCycle", "Night"
                };

                var tagsAC = AutoCompleteTags(tags);

                foreach (var t in simpleCreatures)
                {
                    if ((int)t == -1) continue;

                    RegisterSpawner(t, new SimpleSpawnerInfo(
                        (type, args) => {
                            if (args.Length == 0)
                            {
                                string hint = null;

                                if (StaticWorld.GetCreatureTemplate(t).TopAncestor().type == CritType.LizardTemplate)
                                    hint = "mean: float";
                                else if (t == CritType.Centipede)
                                    hint = "size: float";
                                else if (t == CritType.PoleMimic)
                                    hint = "length: int";

                                if (hint != null)
                                    return tagsAC(type, args).Concat(new string[] { AC.hintPrefix + hint });
                                else
                                    return tagsAC(type, args);
                            }
                            else if (args.Skip(StaticWorld.GetCreatureTemplate(t).TopAncestor().type == CritType.LizardTemplate ? 1 : 0).All(s => tags.Contains(s, StringComparer.OrdinalIgnoreCase))) return tagsAC(type, args);
                            else return null;
                        },

                        (type, args, id, room, pos) =>
                        {
                            var template = StaticWorld.GetCreatureTemplate(t);

                            // Find a good node to spawn the creature at
                            if (!pos.NodeDefined || !template.mappedNodeTypes[pos.abstractNode])
                            {
                                if (room.realizedRoom is Room realRoom)
                                {
                                    bool denCrit = t == CritType.TentaclePlant || t == CritType.PoleMimic || t == MSCCritType.StowawayBug;

                                    float minDist = float.PositiveInfinity;
                                    for (int i = 0; i < room.nodes.Length; i++)
                                    {
                                        var nodeType = room.nodes[i].type;
                                        var nodePos = realRoom.LocalCoordinateOfNode(i);
                                        nodePos.abstractNode = i;

                                        if (i < template.mappedNodeTypes.Length && template.mappedNodeTypes[i]
                                            || denCrit && nodeType == AbstractRoomNode.Type.Den
                                            || t == CritType.GarbageWorm && nodeType == AbstractRoomNode.Type.GarbageHoles)
                                        {
                                            float dist = pos.Tile.FloatDist(nodePos.Tile);
                                            if (!pos.NodeDefined || dist < minDist)
                                            {
                                                minDist = dist;
                                                pos.abstractNode = nodePos.abstractNode;
                                            }
                                        }
                                    }
                                }

                                if (!pos.NodeDefined)
                                    pos.abstractNode = room.RandomRelevantNode(template);
                            }

                            var crit = new AbstractCreature(room.world, template, null, pos, id);

                            if (args.Length > 0)
                            {
                                if (t == CritType.Centipede && float.TryParse(args[0], out float size))
                                    crit.spawnData = $"{{{size}}}";
                                else if (t == CritType.PoleMimic && int.TryParse(args[0], out int length))
                                    crit.spawnData = $"{{{length}}}";
                                else
                                {
                                    if (template.TopAncestor().type == CritType.LizardTemplate && float.TryParse(args[0], out float mean))
                                        args[0] = $"Mean:{mean}";
                                    crit.spawnData = $"{{{string.Join(",", args.Select(tag => tags.FirstOrDefault(testTag => tag.Equals(testTag, StringComparison.OrdinalIgnoreCase)) ?? tag))}}}";
                                }
                            }

                            crit.setCustomFlags();
                            crit.Move(pos);
                            return crit;
                        }
                    ));
                }
            }
        }

        private static Func<ObjType, string[], IEnumerable<string>> AutoCompleteTags(params string[] tags)
        {
            return (_, args) => tags.Except(args, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Registers a spawner for the given object type.
        /// </summary>
        /// <param name="type">The object type to handle.</param>
        /// <param name="info">The spawner to use.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="type"/> already has a handler or is invalid.</exception>
        public static void RegisterSpawner(ObjType type, SpawnerInfo info)
        {
            if (type == ObjType.Creature)
                throw new ArgumentException("Cannot register an object spawner for creatures! Use RegisterSpawner(CreatureTemplate.Type, SpawnerInfo) instead.");

            if ((int)type == -1)
                throw new ArgumentException("Invalid object type!");

            if (safeObjSpawners.ContainsKey(type))
                throw new ArgumentException($"A spawner for {type} already been registered!");

            safeObjSpawners[type] = info;
        }

        /// <summary>
        /// Registers a spawner for the given creature type.
        /// </summary>
        /// <param name="type">The creature type to handle.</param>
        /// <param name="info">The spawner to use.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="type"/> already has a handler or is invalid.</exception>
        public static void RegisterSpawner(CritType type, SpawnerInfo info)
        {
            if ((int)type == -1)
                throw new ArgumentException("Invalid creature type!");

            if (safeCritSpawners.ContainsKey(type))
                throw new ArgumentException($"A spawner for {type} already been registered!");

            safeCritSpawners[type] = info;
        }

        /// <summary>
        /// Creates an object or creature from a spawner registered via <see cref="RegisterSpawner(ObjType, SpawnerInfo)"/> or <see cref="RegisterSpawner(CritType, SpawnerInfo)"/>.
        /// </summary>
        /// <param name="args">The console arguments of the command.</param>
        /// <param name="room">The room to spawn this object in.</param>
        /// <param name="pos">The position to spawn this object at. Depending on the constructor, this may not be used.</param>
        /// <exception cref="ArgumentException">Thrown when no spawner could be found for the given type.</exception>
        public static AbstractPhysicalObject CreateAbstractObjectSafe(string[] args, AbstractRoom room, WorldCoordinate pos)
        {
            if (args.Length == 0)
                throw new ArgumentException("No type specified!");

            var objType = new ObjType(args[0]);
            var critType = new CritType(args[0]);

            int startInd = 1;
            EntityID id = room.world.game.GetNewID();

            if (args.Length > 1 && entityID.IsMatch(args[1]))
            {
                try
                {
                    id = ParseExtendedID(args[1]);
                    startInd = 2;
                }
                catch { }
            }

            var subArgs = new string[args.Length - startInd];
            Array.ConstrainedCopy(args, startInd, subArgs, 0, subArgs.Length);

            if ((int)objType != -1 || (int)critType != -1)
            {
                SpawnerInfo spawner;
                if (!safeObjSpawners.TryGetValue(objType, out spawner) && !safeCritSpawners.TryGetValue(critType, out spawner))
                    throw new ArgumentException($"{objType} has no registered spawners! Try using \"spawn_raw\" instead.");

                return spawner.Spawn(objType, subArgs, id, room, pos);
            }
            else
            {
                throw new ArgumentException($"Unknown object or creature type: {args[0]}");
            }
        }

        internal static void AddToRoom(AbstractPhysicalObject obj)
        {
            var room = obj.world.GetAbstractRoom(obj.pos);
            if (obj is AbstractCreature crit &&
                (crit.creatureTemplate.type == CritType.PoleMimic
                || crit.creatureTemplate.type == CritType.TentaclePlant
                || crit.creatureTemplate.type == MSCCritType.StowawayBug)
                && crit.pos.NodeDefined
                && room.GetNode(crit.pos).type == AbstractRoomNode.Type.Den)
            {
                obj.world.GetAbstractRoom(0).entitiesInDens.Add(obj);
            }
            else
            {
                room.AddEntity(obj);
                if (room.realizedRoom != null)
                {
                    obj.RealizeInRoom();
                }
            }
        }

        internal static IEnumerable<string> AutocompleteSafe(string[] args)
        {
            if (args.Length == 0)
            {
                foreach (var key in safeObjSpawners.Keys)
                    yield return key.ToString();
                foreach (var key in safeCritSpawners.Keys)
                    yield return key.ToString();
            }
            else
            {
                var objType = new ObjType(args[0]);
                var critType = new CritType(args[0]);
                int startInd = 1;

                if (args.Length > 1 && entityID.IsMatch(args[1]))
                {
                    startInd = 2;
                }

                var subArgs = new string[args.Length - startInd];
                Array.ConstrainedCopy(args, startInd, subArgs, 0, subArgs.Length);

                if ((int)objType != -1 || (int)critType != -1)
                {
                    SpawnerInfo spawner;
                    if (safeObjSpawners.TryGetValue(objType, out spawner) || safeCritSpawners.TryGetValue(critType, out spawner))
                    {
                        foreach (var entry in spawner.Autocomplete(objType, subArgs))
                            yield return entry;
                    }
                }
            }
        }

        internal static IEnumerable<string> Autocomplete(string[] args)
        {
            if (args.Length == 0)
            {
                foreach (var key in typeMap.Keys)
                    yield return key;
            }
            else
            {
                // Fetch type
                if (typeMap.TryGetValue(args[0], out Type type))
                {
                    var ctors = GetConstructors(type);

                    int argInd = args.Length - 1;

                    if (args.Length > 1 && entityID.IsMatch(args[1]))
                        argInd--;

                    var options = ctors
                        .Select(ctor => ctor.GetParameters()
                            .Where(param => !TryFillAutoParam(param, null, default, default, out _))
                            .ElementAtOrDefault(argInd))
                        .Where(param => param != null);

                    foreach (var op in options)
                    {
                        var opType = op.ParameterType;

                        yield return $"{DevConsole.Autocomplete.hintPrefix}{op.Name}: {opType.Name}";

                        if (opType == typeof(bool))
                        {
                            yield return bool.TrueString;
                            yield return bool.FalseString;
                        }
                        else if (opType.IsEnum)
                        {
                            foreach (var val in Enum.GetNames(opType))
                                yield return val.ToString();
                        }
                        else if (opType.IsExtEnum())
                        {
                            foreach (var val in ExtEnumBase.GetExtEnumType(opType).entries)
                                yield return val;
                        }
                        else if (opType == typeof(CreatureTemplate))
                        {
                            foreach (var template in StaticWorld.creatureTemplates)
                                yield return template.type.value;
                        }
                    }
                }
                else
                {
                    yield return "Couldn't resolve type";
                }
            }
        }

        /// <summary>
        /// Creates an <see cref="AbstractPhysicalObject"/> at a given room and position.
        /// </summary>
        /// <param name="args">The object type name, ID, and constructor arguments. Some arguments, such as <see cref="World"/>, are automatically filled and should be omitted.</param>
        /// <param name="room">The room to spawn this object in.</param>
        /// <param name="pos">The position to spawn this object at. Depending on the constructor, this may not be used.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="args"/> does not match any constructors.</exception>
        public static AbstractPhysicalObject CreateAbstractObject(string[] args, AbstractRoom room, WorldCoordinate pos)
        {
            if (!scanned)
                ScanTypes();

            // Fetch type
            if (!typeMap.TryGetValue(args[0], out Type type))
            {
                try
                {
                    type = Type.GetType(args[0], true, true);
                }
                catch (Exception e)
                {
                    throw new ArgumentException("Couldn't resolve type!", e);
                }

                typeMap[args[0]] = type;
            }

            // Fetch constructors
            ConstructorInfo[] ctors = GetConstructors(type);

            var argList = new List<string>(args.Skip(1));

            // Find ID
            EntityID id;
            if (argList.Count > 0 && entityID.IsMatch(argList[0]))
            {
                id = ParseExtendedID(argList[0]);
                argList.RemoveAt(0);
            }
            else
                id = room.world.game.GetNewID();

            if (ctors.Length == 0)
            {
                throw new ArgumentException($"No constructors found for {type}!");
            }

            // Try converting all arguments to the correct type
            string error = null;
            Exception innerException = null;
            foreach (ConstructorInfo ctor in ctors)
            {
                try
                {
                    return CallConstructor(ctor, argList, room, pos, id);
                }
                catch (ArgumentException e)
                {
                    error = e.Message;
                    innerException = e.InnerException;
                }
            }

            throw new ArgumentException(error, innerException);
        }

        private static ConstructorInfo[] GetConstructors(Type type)
        {
            if (!typeCtors.TryGetValue(type, out ConstructorInfo[] ctors))
            {
                ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                Array.Sort(ctors, (a, b) =>
                {
                    if (a.IsPublic != b.IsPublic) return a.IsPublic ? -1 : 1;

                    return a.GetParameters().Length - b.GetParameters().Length;
                });
                typeCtors[type] = ctors;
            }

            return ctors;
        }

        private static AbstractPhysicalObject CallConstructor(ConstructorInfo ctor, List<string> argList, AbstractRoom room, WorldCoordinate pos, EntityID id)
        {
            var @params = ctor.GetParameters();

            object[] finalArgs = new object[@params.Length];

            int inArgInd = 0;
            for (int outArgInd = 0; outArgInd < finalArgs.Length; outArgInd++)
            {
                var param = @params[outArgInd];

                if (!TryFillAutoParam(param, room, pos, id, out finalArgs[outArgInd]))
                {
                    if (inArgInd >= argList.Count)
                    {
                        throw new ArgumentException("Too few arguments given!");
                    }

                    var arg = argList[inArgInd++];
                    try
                    {
                        finalArgs[outArgInd] = FromString(arg, param.ParameterType);
                    }
                    catch (Exception e)
                    {
                        throw new ArgumentException($"Couldn't convert {param.Name} = \"{arg}\" to {param.ParameterType}!", e);
                    }
                }
            }

            if (inArgInd < argList.Count)
            {
                throw new ArgumentException("Too many arguments given!");
            }

            // All parameters were successfully converted
            // Try creating the object
            return (AbstractPhysicalObject)ctor.Invoke(finalArgs);
        }

        private static bool TryFillAutoParam(ParameterInfo info, AbstractRoom room, WorldCoordinate pos, EntityID id, out object value)
        {
            Type type = info.ParameterType;
            if (type == typeof(Room))
            {
                value = room?.realizedRoom;
            }
            else if (type == typeof(AbstractRoom))
            {
                value = room;
            }
            else if (type == typeof(World))
            {
                value = room?.world;
            }
            else if (type == typeof(WorldCoordinate))
            {
                value = pos;
            }
            else if (type == typeof(EntityID))
            {
                value = id;
            }
            else if (typeof(PhysicalObject).IsAssignableFrom(type))
            {
                value = null;
            }
            else
            {
                value = null;
                return false;
            }

            return true;
        }

        private static readonly Type[] fromStringTypes = new Type[] { typeof(string) };
        private static object FromString(string text, Type toType)
        {
            // Try hardcoded, safe conversions
            if (text.Equals("null", StringComparison.OrdinalIgnoreCase) || text.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            else if (toType.IsEnum)
            {
                return Enum.Parse(toType, text, true);
            }
            else if (toType.IsExtEnum())
            {
                return ExtEnumBase.Parse(toType, text, true);
            }
            else if (toType == typeof(CreatureTemplate))
            {
                return StaticWorld.GetCreatureTemplate(WorldLoader.CreatureTypeFromString(text));
            }

            // Try finding a method called FromString
            var fromString = toType.GetMethod("FromString", BindingFlags.Static, null, fromStringTypes, null);
            if (fromString != null)
            {
                try
                {
                    var res = fromString.Invoke(null, new object[] { text });
                    if (res != null && toType.IsAssignableFrom(res.GetType()))
                        return res;
                }
                catch { }
            }

            // Default to conversion
            return Convert.ChangeType(text, toType);
        }

        private static IEnumerable<Assembly> GetScanAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(asm => !dllBlacklist.Contains(asm.GetName().Name));
        }

        internal static void ScanTypes()
        {
            scanned = true;

            foreach (var asm in GetScanAssemblies())
            {
                Type[] types = null;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                if (types != null)
                {
                    foreach (var t in types.Where(t =>
                        typeof(AbstractPhysicalObject).IsAssignableFrom(t)
                        && !t.ContainsGenericParameters
                        && !t.IsAbstract))
                    {
                        typeMap[t.FullName] = t;
                        typeMap[t.Name] = t;
                        typeMap[t.Name.Replace("Abstract", "")] = t;
                    }
                }
            }
        }

        private static EntityID ParseExtendedID(string id)
        {
            EntityID outID = EntityID.FromString(id);
            string[] split = id.Split('.');
            if(split.Length > 3 && int.TryParse(split[3], out int altSeed))
            {
                outID.setAltSeed(altSeed);
            }
            return outID;
        }

        /// <summary>
        /// Describes how to spawn an object.
        /// </summary>
        public abstract class SpawnerInfo
        {
            /// <summary>
            /// Gets all possible values of the next argument for autocomplete.
            /// </summary>
            /// <param name="type">The object type that is going to be spawned.</param>
            /// <param name="args">The partial console arguments of the command.</param>
            public abstract IEnumerable<string> Autocomplete(ObjType type, string[] args);

            /// <summary>
            /// Creates an <see cref="AbstractPhysicalObject"/> from string arguments.
            /// </summary>
            /// <param name="type">The obejct type to spawn.</param>
            /// <param name="args">The console arguments of the command.</param>
            /// <param name="id">The id of the new entity.</param>
            /// <param name="room">The room to spawn this object in.</param>
            /// <param name="pos">The position to spawn this object at. Depending on the constructor, this may not be used.</param>
            public abstract AbstractPhysicalObject Spawn(ObjType type, string[] args, EntityID id, AbstractRoom room, WorldCoordinate pos);
        }

        /// <summary>
        /// A <see cref="SpawnerInfo"/> that uses delegates.
        /// </summary>
        public class SimpleSpawnerInfo : SpawnerInfo
        {
            private readonly Func<ObjType, string[], IEnumerable<string>> autocomplete;
            private readonly Func<ObjType, string[], EntityID, AbstractRoom, WorldCoordinate, AbstractPhysicalObject> spawn;

            /// <summary>
            /// Creates a new spawner from the given delegates.
            /// </summary>
            public SimpleSpawnerInfo(Func<ObjType, string[], IEnumerable<string>> autocomplete, Func<ObjType, string[], EntityID, AbstractRoom, WorldCoordinate, AbstractPhysicalObject> spawn)
            {
                this.autocomplete = autocomplete;
                this.spawn = spawn;
            }


            /// <inheritdoc/>
            public override IEnumerable<string> Autocomplete(ObjType type, string[] args) => autocomplete(type, args);

            /// <inheritdoc/>
            public override AbstractPhysicalObject Spawn(ObjType type, string[] args, EntityID id, AbstractRoom room, WorldCoordinate pos) => spawn(type, args, id, room, pos);
        }
    }
}
