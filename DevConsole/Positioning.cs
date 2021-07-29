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
                    Pos = new RoomPos(Input.mousePosition);
                }
            }
            else
                Pos = new RoomPos(Input.mousePosition);
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
