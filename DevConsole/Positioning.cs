using RWCustom;
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
        /// Standard positioning autocompletion.
        /// </summary>
        public static string[] Autocomplete => Selection.Autocomplete.Select(s => "<" + s + ">").Union(new[] { "<default>", "<cursor>", "<camera>" }).ToArray();

        /// <summary>
        /// Parses <paramref name="arg"/> and returns a position within <paramref name="game"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the operation was successful; otherwise, <see langword="false"/>.</returns>
        public static bool TryGetPosition(RainWorldGame game, string arg, out RoomPos pos)
        {
            try
            {
                if (string.IsNullOrEmpty(arg?.Trim()) || !(arg[0] == '<' && arg[arg.Length - 1] == '>'))
                {
                    pos = GameConsole.TargetPos;
                    return false;
                }

                pos = ParsePosition(game, arg.Substring(1, arg.Length - 2).ToLower());
                return true;
            }
            catch
            {
                pos = GameConsole.TargetPos;
                return false;
            }
        }

        private static RoomPos ParsePosition(RainWorldGame game, string arg)
        {
            switch (arg)
            {
                case "default": return GameConsole.TargetPos;
                case "cursor": return new RoomPos(game.cameras[0].room.abstractRoom, game.cameras[0].pos + (Vector2)Input.mousePosition);
                case "camera": return new RoomPos(game.cameras[0].room.abstractRoom, game.cameras[0].pos + game.cameras[0].sSize / 2f);
            }

            var selection = Selection.SelectAbstractObjects(game, arg);
            if (selection.FirstOrDefault() is AbstractPhysicalObject o)
            {
                return new RoomPos(o.Room, o.realizedObject?.firstChunk?.pos ?? o.pos.Tile.GetMiddleOfTile());
            }

            throw new();
        }
    }
}