using System;
using BepInEx;
using DevConsole.Config;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Permissions;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: IgnoresAccessChecksTo("Assembly-CSharp")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[module: UnverifiableCode]
#pragma warning restore CS0618 // Type or member is obsolete

namespace DevConsole
{
    [BepInPlugin(MOD_ID, "Dev Console", MOD_VERSION)]
    internal class DevConsoleMod : BaseUnityPlugin
    {
        public const string MOD_ID = "slime-cubed.devconsole";
        public const string MOD_VERSION = "1.5.1";
        private static bool initialized = false;
        private static ConsoleConfig config;

        public void Awake()
        {
            On.RainWorld.OnModsInit += (orig, self) =>
            {
                orig(self);

                MachineConnector.SetRegisteredOI(MOD_ID, config ??= new ConsoleConfig());

                if (initialized) return;
                initialized = true;

                try
                {
                    GameConsole.Apply(this);
                }
                catch(Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            };

            On.RainWorld.PreModsInit += (orig, self) =>
            {
                orig(self);

                ObjectSpawner.ClearSafeSpawners();
            };

            On.RainWorld.PostModsInit += (orig, self) =>
            {
                orig(self);

                ObjectSpawner.RegisterSafeSpawners();

                if(ConsoleConfig.scanOnStartup.Value)
                {
                    ObjectSpawner.ScanTypes();
                }

                GameConsole.LoadCommandHistory();

                // Set default command position
                string defaultPos = ConsoleConfig.defaultPos.Value switch
                {
                    "player" => "<default>",
                    "cursor" => "<cursor>",
                    "camera" => "<camera>",
                    _ => "<default>"
                };

                if (defaultPos != "<default>")
                {
                    InternalPositioning.GetDefaultPos = game =>
                    {
                        return Positioning.TryGetPosition(game, defaultPos, out var pos) ? pos : InternalPositioning.Pos;
                    };
                }
            };
        }
    }
}
