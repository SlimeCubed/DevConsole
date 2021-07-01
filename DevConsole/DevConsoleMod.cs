using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Partiality.Modloader;

namespace DevConsole
{
    internal partial class DevConsoleMod : PartialityMod
    {
        public const string versionString = "1.0.0";

        // Config
        public static bool autopause = false; // Pause the game when the console is open

        public DevConsoleMod()
        {
            ModID = "Dev Console";
            Version = versionString;
            author = "Slime_Cubed";
        }

        public override void OnLoad()
        {
            GameConsole.Apply(this);
        }
    }
}
