using BepInEx;
using RWCustom;
using System;
using System.Linq;
using UnityEngine;

namespace DevConsole
{
    /// <summary>
    /// Contains methods for getting an in-game position from a position string.
    /// </summary>
    public static class Positioning
    {
        /// <summary>
        /// Parses <paramref name="arg"/> and returns a position within <paramref name="game"/>.
        /// </summary>
        /// <returns>The parsed position.</returns>
        public static RoomPos GetPosition(RainWorldGame game, string arg, out bool validPositionString)
        {
            try
            {
                // Note: `string.IsNullOrWhiteSpace()` is an extension method. It will not throw a nullref.
                if (arg.IsNullOrWhiteSpace() || !(arg[0] == '<' && arg[arg.Length - 1] == '>'))
                {
                    validPositionString = false;
                    return GameConsole.DefaultPos;
                }

                validPositionString = true;
                return ParsePosition(game, arg.Substring(1, arg.Length - 2).ToLower());
            }
            catch
            {
                validPositionString = false;
                return GameConsole.DefaultPos;
            }
        }

        private static RoomPos ParsePosition(RainWorldGame game, string arg)
        {
            switch (arg)
            {
                case "default": return GameConsole.DefaultPos;
                case "cursor": return new RoomPos(game.cameras[0].room.abstractRoom, game.cameras[0].pos + (Vector2)Input.mousePosition);
                case "camera": return new RoomPos(game.cameras[0].room.abstractRoom, game.cameras[0].pos + game.cameras[0].sSize / 2f);
            }

            var selection = Selection.SelectAbstractObjects(game, arg);
            if (selection.FirstOrDefault() is AbstractPhysicalObject o)
            {
                return new RoomPos(o.Room, o.realizedObject?.firstChunk?.pos ?? GetMiddleOfTile(o.pos.Tile));
            }

            throw new();

            static Vector2 GetMiddleOfTile(IntVector2 vector)
            {
                return new Vector2(10f + vector.x * 20f, 10f + vector.y * 20f);
            }
        }
    }
}
