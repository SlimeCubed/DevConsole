using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DevConsole
{
    // Controls where commands should place in-game effects
    // GameConsole.SpawnPos and GameConsole.SpawnRoom use this
    internal static class DefaultPositioning
    {
        public static RoomPos Pos { get; private set; }
        public static Func<RainWorldGame, RoomPos> GetDefaultPos { private get; set; } = GetPosDefault;

        private static RainWorld rw;

        public static void Update()
        {
            if (rw == null) rw = Object.FindObjectOfType<RainWorld>();
            if (rw == null) return;

            if (rw.processManager.currentMainLoop is RainWorldGame game && GetDefaultPos != null)
            {
                try
                {
                    Pos = GetDefaultPos(game);
                }
                catch
                {
                    Pos = new RoomPos(game.cameras[0]?.room?.abstractRoom ?? Pos.Room, game.cameras[0].pos + (Vector2)Input.mousePosition);
                }
            }
            else
                Pos = new RoomPos(null, Input.mousePosition);
        }

        public static RoomPos GetPosDefault(RainWorldGame game)
        {
            return new RoomPos(game.Players[0].Room, game.Players[0].realizedObject.firstChunk.pos);
        }
    }
}
