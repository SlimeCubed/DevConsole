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

        public static float MeasureWidth(this FLabel label, string text)
        {
            var font = label._font;
            char[] chars = text.ToCharArray();
            int lineLength = 0;
            float x = 0f;
            char lastChar = '\0';

            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c == '\n')
                {
                    x = 0f;
                }
                else
                {
                    if (!font._charInfosByID.TryGetValue(c, out FCharInfo charInfo))
                    {
                        charInfo = font._charInfosByID[0];
                    }

                    if (lineLength == 0)
                    {
                        x = -charInfo.offsetX;
                    }
                    else
                    {
                        FKerningInfo kerning = font._nullKerning;
                        for (int j = 0; j < font._kerningCount; j++)
                        {
                            FKerningInfo testKerning = font._kerningInfos[j];
                            if (testKerning.first == lastChar && testKerning.second == c)
                            {
                                kerning = testKerning;
                            }
                        }

                        x += kerning.amount + font._textParams.scaledKerningOffset + label._textParams.scaledKerningOffset;
                    }
                    x += charInfo.xadvance;
                    lineLength++;
                }
                lastChar = c;
            }

            return x;
        }
    }
}
