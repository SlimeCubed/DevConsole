using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Partiality.Modloader;

namespace DevConsole
{
    internal class DevConsoleMod : PartialityMod
    {
        public DevConsoleMod()
        {
            ModID = "Dev Console";
            Version = "1.0.0";
            author = "Slime_Cubed";
        }

        public override void OnLoad()
        {
            GameConsole.Apply();
        }
    }
}
