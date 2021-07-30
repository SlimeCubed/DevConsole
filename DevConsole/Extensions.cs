using UnityEngine;
using RWCustom;

namespace DevConsole
{
    internal static class Extensions
    {
        public static Vector2 GetMiddleOfTile(this IntVector2 vector)
        {
            return new Vector2(10f + vector.x * 20f, 10f + vector.y * 20f);
        }

        public static IntVector2 GetTilePosition(this Vector2 pos)
        {
            return new((int)((pos.x + 20f) / 20f) - 1, (int)((pos.y + 20f) / 20f) - 1);
        }

        public static WorldCoordinate GetWorldCoordinate(this AbstractRoom self, Vector2 pos)
        {
            return Custom.MakeWorldCoordinate(pos.GetTilePosition(), self.index);
        }
    }
}
