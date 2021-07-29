using UnityEngine;

namespace DevConsole
{
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
