using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Partiality.Modloader;

namespace DevConsole
{
    internal partial class DevConsoleMod : PartialityMod
    {
        public const string versionString = "0.2.0";
        public int version = 0;
        public string updateURL = "http://beestuff.pythonanywhere.com/audb/api/mods/6/1";
        public string keyE = "AQAB";
        public string keyN = "szjz4lkR8G9JuQ4Jt2DEk7h5hRcvpX0LfHWXp203VrsSwWenj2xho0zl8m6gsSYNVaBFm3WXbqkj7snI+DuheYfvSLpfLZsHCOF2XdIO2FCyOFSUmQ7T4Jvd/ap5jFMofXu6geBf0hl0H4VJ1/D2SpDg7rkAi+hAbHBd1d7o1mfON1ZdzDKIeTeFCstw5w+ImfE83sg1OspLmrrec3UNyXlNzc5x+r5gHwgOfMMTWLfI1fUVRd3o43U+zV7PHsyOjPGzHfLVLS3IO6va3Pc7sng+bxifchP9IWS4RTps4qmGA6AcQE2qaI1oH0Ql9EzAfBeIhvNXica0nlTHBJQ8tZxewA1igdHl2deSgszpKseAPPxsg9+njoaq4rvqcEys3/KfJImxyS3W49U+GxGmoPx298GMSUlfyw3zY3Ytlbb7/7tbHfP71G4/ISwkn+WyhufE3SLYWX/6uR//0aMGNe/zoH8AOvnPtepX4Mwy3HYnETzc5WsCgetmCViEI0YdAKl3FClgtuhsYRXmEXDy7yeVpTSsAzoUdkqnzFSG5ykm1mh1ISCpBiQ9prB2inCaWMc6DALWsFUElOV6yVbmWorfX2EiNesDhoFmAxz6pt6CADVBoxewDTFUtT103jYVkROKe4oNUr2W0Sj1sEv6kURHfjE5+3OLfbrk3OLJrnU=";

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
