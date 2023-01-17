using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System.IO;
using MonoMod.RuntimeDetour;
using System.Globalization;
using System.Linq;

namespace DevConsole
{
    internal static class CustomFonts
    {
        private static readonly string[] fontNames = new string[] { "devconsolas" };

        public static void Load()
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            using (new Detour(
                typeof(FFont).GetMethod(nameof(FFont.LoadAndParseConfigFile), flags),
                new Action<FFont, float>(LoadAndParseConfigFile).Method))
            {
                foreach (var fontPath in AssetManager.ListDirectory("consolefonts").Where(path => path.EndsWith(".fnt")))
                {
                    var fontName = Path.GetFileNameWithoutExtension(fontPath);
                    if (!Futile.atlasManager.DoesContainFontWithName(fontName))
                    {
                        var atlas = Futile.atlasManager.LoadImage($"consolefonts/{fontName}");
                        Futile.atlasManager.LoadFont(fontName, atlas.elements[0].name, $"consolefonts/{fontName}", 0f, 0f);
                    }
                }
            }
        }

        private static void LoadAndParseConfigFile(FFont self, float fontScale)
        {
            string path = AssetManager.ResolveFilePath(self._configPath + ".fnt");
            string text;
            if (File.Exists(path))
            {
                text = File.ReadAllText(path);
            }
            else
            {
                TextAsset textAsset = (TextAsset)Resources.Load(self._configPath, typeof(TextAsset));
                if (textAsset == null)
                {
                    throw new FutileException("Couldn't find font config file " + self._configPath);
                }
                text = textAsset.text;
            }

            string[] array = new string[] { "\n" };
            string[] array2 = text.Split(array, StringSplitOptions.RemoveEmptyEntries);
            if (array2.Length <= 1)
            {
                array[0] = "\r\n";
                array2 = text.Split(array, StringSplitOptions.RemoveEmptyEntries);
            }
            if (array2.Length <= 1)
            {
                array[0] = "\r";
                array2 = text.Split(array, StringSplitOptions.RemoveEmptyEntries);
            }
            if (array2.Length <= 1)
            {
                throw new FutileException("Your font file is messed up");
            }
            int num = 0;
            int num2 = 0;
            self._charInfosByID = new Dictionary<uint, FCharInfo>(127);
            FCharInfo value = new FCharInfo();
            self._charInfosByID[0U] = value;
            float resourceScaleInverse = Futile.resourceScaleInverse;
            Vector2 textureSize = self._element.atlas.textureSize;
            bool flag = false;
            int num3 = array2.Length;
            for (int i = 0; i < num3; i++)
            {
                string[] array3 = array2[i].Split(new char[]
                {
                ' '
                }, StringSplitOptions.RemoveEmptyEntries);
                if (array3[0] == "common")
                {
                    self._configWidth = int.Parse(array3[3].Split(new char[]
                    {
                    '='
                    })[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                    self._configRatio = self._element.sourcePixelSize.x / (float)self._configWidth;
                    self._lineHeight = (float)int.Parse(array3[1].Split(new char[]
                    {
                    '='
                    })[1], NumberStyles.Any, CultureInfo.InvariantCulture) * self._configRatio * resourceScaleInverse;
                }
                else if (array3[0] == "chars")
                {
                    int num4 = int.Parse(array3[1].Split(new char[]
                    {
                    '='
                    })[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                    self._charInfos = new FCharInfo[num4 + 1];
                }
                else if (array3[0] == "char")
                {
                    FCharInfo fcharInfo = new FCharInfo();
                    int num5 = array3.Length;
                    for (int j = 1; j < num5; j++)
                    {
                        string[] array4 = array3[j].Split(new char[]
                        {
                        '='
                        });
                        string a = array4[0];
                        if (a == "letter")
                        {
                            if (array4[1].Length >= 3)
                            {
                                fcharInfo.letter = array4[1].Substring(1, 1);
                            }
                        }
                        else if (!(a == "\r"))
                        {
                            int num6 = int.Parse(array4[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                            float num7 = (float)num6;
                            if (a == "id")
                            {
                                fcharInfo.charID = num6;
                            }
                            else if (a == "x")
                            {
                                fcharInfo.x = num7 * self._configRatio - self._element.sourceRect.x * Futile.resourceScale;
                            }
                            else if (a == "y")
                            {
                                fcharInfo.y = num7 * self._configRatio - self._element.sourceRect.y * Futile.resourceScale;
                            }
                            else if (a == "width")
                            {
                                fcharInfo.width = num7 * self._configRatio;
                            }
                            else if (a == "height")
                            {
                                fcharInfo.height = num7 * self._configRatio;
                            }
                            else if (a == "xoffset")
                            {
                                fcharInfo.offsetX = num7 * self._configRatio;
                            }
                            else if (a == "yoffset")
                            {
                                fcharInfo.offsetY = num7 * self._configRatio;
                            }
                            else if (a == "xadvance")
                            {
                                fcharInfo.xadvance = num7 * self._configRatio;
                            }
                            else if (a == "page")
                            {
                                fcharInfo.page = num6;
                            }
                        }
                    }
                    Rect uvRect = new Rect(self._element.uvRect.x + fcharInfo.x / textureSize.x, (textureSize.y - fcharInfo.y - fcharInfo.height) / textureSize.y - (1f - self._element.uvRect.yMax), fcharInfo.width / textureSize.x, fcharInfo.height / textureSize.y);
                    fcharInfo.uvRect = uvRect;
                    fcharInfo.uvTopLeft.Set(uvRect.xMin, uvRect.yMax);
                    fcharInfo.uvTopRight.Set(uvRect.xMax, uvRect.yMax);
                    fcharInfo.uvBottomRight.Set(uvRect.xMax, uvRect.yMin);
                    fcharInfo.uvBottomLeft.Set(uvRect.xMin, uvRect.yMin);
                    fcharInfo.width *= resourceScaleInverse * fontScale;
                    fcharInfo.height *= resourceScaleInverse * fontScale;
                    fcharInfo.offsetX *= resourceScaleInverse * fontScale;
                    fcharInfo.offsetY *= resourceScaleInverse * fontScale;
                    fcharInfo.xadvance *= resourceScaleInverse * fontScale;
                    self._charInfosByID[(uint)fcharInfo.charID] = fcharInfo;
                    self._charInfos[num] = fcharInfo;
                    num++;
                }
                else if (array3[0] == "kernings")
                {
                    flag = true;
                    int num8 = int.Parse(array3[1].Split(new char[]
                    {
                    '='
                    })[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                    self._kerningInfos = new FKerningInfo[num8 + 100];
                }
                else if (array3[0] == "kerning")
                {
                    FKerningInfo fkerningInfo = new FKerningInfo();
                    fkerningInfo.first = -1;
                    int num5 = array3.Length;
                    for (int k = 1; k < num5; k++)
                    {
                        string[] array5 = array3[k].Split(new char[]
                        {
                        '='
                        });
                        if (array5.Length >= 2)
                        {
                            string a2 = array5[0];
                            int num9 = int.Parse(array5[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                            if (a2 == "first")
                            {
                                fkerningInfo.first = num9;
                            }
                            else if (a2 == "second")
                            {
                                fkerningInfo.second = num9;
                            }
                            else if (a2 == "amount")
                            {
                                fkerningInfo.amount = (float)num9 * self._configRatio * resourceScaleInverse;
                            }
                        }
                    }
                    if (fkerningInfo.first != -1)
                    {
                        self._kerningInfos[num2] = fkerningInfo;
                    }
                    num2++;
                }
            }
            self._kerningCount = num2;
            if (!flag)
            {
                self._kerningInfos = new FKerningInfo[0];
            }
            if (self._charInfosByID.ContainsKey(32U))
            {
                self._charInfosByID[32U].offsetX = 0f;
                self._charInfosByID[32U].offsetY = 0f;
            }
            for (int l = 0; l < self._charInfos.Length; l++)
            {
                if (self._charInfos[l] != null && self._charInfos[l].width > self._maxCharWidth)
                {
                    self._maxCharWidth = self._charInfos[l].width;
                }
            }
        }
    }
}
