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
                    Pos = new RoomPos(null, Input.mousePosition);
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

    /// <summary>
    /// Represents a position in the game world.
    /// </summary>
    public struct RoomPos
    {
        /// <summary>
        /// The room. This may be null.
        /// </summary>
        public AbstractRoom Room { get; }

        /// <summary>
        /// The exact position in the room.
        /// </summary>
        public Vector2 Pos { get; }

        /// <summary>
        /// Initializes a new <see cref="RoomPos"/>.
        /// </summary>
        public RoomPos(AbstractRoom room, Vector2 pos)
        {
            Room = room;
            Pos = pos;
        }
    }
}
