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
        public const string MOD_VERSION = "1.2.0";
        private static bool initialized = false;

        public void Awake()
        {
            On.RainWorld.OnModsInit += (orig, self) =>
            {
                orig(self);

                if (initialized) return;
                initialized = true;

                Logger.LogWarning("Initialized");
                try
                {
                    MachineConnector.SetRegisteredOI(MOD_ID, new ConsoleConfig());
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
            };
        }
    }
}
