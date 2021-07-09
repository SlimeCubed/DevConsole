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
    internal static class Positioning
    {
        public static RoomPos pos;
        public static Func<RainWorldGame, RoomPos> getPos = GetPosDefault;

        private static RainWorld rw;

        public static void Update()
        {
            if (rw == null) rw = Object.FindObjectOfType<RainWorld>();
            if (rw == null) return;

            if (rw.processManager.currentMainLoop is RainWorldGame game && getPos != null)
            {
                try
                {
                    pos = getPos(game);
                }
                catch
                {
                    pos = new RoomPos(Input.mousePosition);
                }
            }
            else
                pos = new RoomPos(Input.mousePosition);
        }

        public static RoomPos GetPosDefault(RainWorldGame game)
        {
            return new RoomPos(game.Players[0].realizedObject.room, game.Players[0].realizedObject.firstChunk.pos);
        }

        public struct RoomPos
        {
            public readonly Room room;
            public readonly Vector2 pos;

            public RoomPos(Room room, Vector2 pos)
            {
                this.room = room;
                this.pos = pos;
            }

            public RoomPos(Vector2 pos)
            {
                room = null;
                this.pos = pos;
            }
        }
    }
}
