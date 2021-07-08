using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using UnityEngine;
using System.Reflection;
using System.IO;
using MonoMod.RuntimeDetour;

namespace DevConsole
{
    internal static class CustomFonts
    {
        private static readonly string[] fontNames = new string[] { "devconsolas" };

        public static void Load()
        {
            foreach (var fontName in fontNames)
                LoadFont(fontName, $"DevConsole.Fonts.{fontName}", 0f, 0f, new FTextParams());
        }

        private static FFont LoadFont(string name, string configPath, float offsetX, float offsetY, FTextParams textParams)
        {
			FFont font;
            FAtlasElement element;
            Assembly asm = Assembly.GetExecutingAssembly();

            // Generate a font atlas
            {
                Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                var stream = asm.GetManifestResourceStream(configPath + ".png");
                if (stream == null) throw new Exception($"Could not find font image at {configPath}.png");
                byte[] buf = new byte[stream.Length];
                stream.Read(buf, 0, (int)stream.Length);
                tex.LoadImage(buf);
				tex.filterMode = FilterMode.Point;
                element = Futile.atlasManager.LoadAtlasFromTexture(name, tex).elements[0];
            }

            // Load font description
            var textStream = asm.GetManifestResourceStream(configPath + ".fnt");
            if (textStream == null) throw new Exception($"Could not find font text at {configPath}.fnt");
            string fontText = new StreamReader(textStream).ReadToEnd();

			// Exclude call to LoadAndParseConfigFile from FFont.ctor
			using (Hook hk = new Hook(
				typeof(FFont).GetMethod("LoadAndParseConfigFile", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
				(On.FFont.hook_LoadAndParseConfigFile)((orig, self) => { })))
			{
				font = new FFont(name, element, configPath, offsetX, offsetY, textParams);
			}

			// Parse from provided text instead
			font.ParseConfigText(fontText);

            // From FAtlasManager.LoadFont
            Futile.atlasManager._fonts.Add(font);
            Futile.atlasManager._fontsByName.Add(name, font);

            return font;
        }

        // From Font.LoadAndParseConfigFile
        // This could have been from source, but...
        private static void ParseConfigText(this FFont font, string fontText)
        {
            string[] array = new string[]
			{
				"\n"
			};
			string[] array2 = fontText.Split(array, StringSplitOptions.RemoveEmptyEntries);
			if (array2.Length <= 1)
			{
				array[0] = "\r\n";
				array2 = fontText.Split(array, StringSplitOptions.RemoveEmptyEntries);
			}
			if (array2.Length <= 1)
			{
				array[0] = "\r";
				array2 = fontText.Split(array, StringSplitOptions.RemoveEmptyEntries);
			}
			if (array2.Length <= 1)
			{
				throw new FutileException("Your font file is messed up");
			}
			int num = 0;
			int num2 = 0;
			font._charInfosByID = new Dictionary<uint, FCharInfo>(127);
			FCharInfo value = new FCharInfo();
			font._charInfosByID[0u] = value;
			float resourceScaleInverse = Futile.resourceScaleInverse;
			Vector2 textureSize = font._element.atlas.textureSize;
			Debug.Log("texture width " + textureSize.x);
			bool flag = false;
			int num3 = array2.Length;
			for (int i = 0; i < num3; i++)
			{
				string text = array2[i];
				string[] array3 = text.Split(new char[]
				{
				' '
				}, StringSplitOptions.RemoveEmptyEntries);
				if (array3[0] == "common")
				{
					font._configWidth = int.Parse(array3[3].Split(new char[]
					{
					'='
					})[1]);
					font._configRatio = font._element.sourcePixelSize.x / (float)font._configWidth;
					font._lineHeight = (float)int.Parse(array3[1].Split(new char[]
					{
					'='
					})[1]) * font._configRatio * resourceScaleInverse;
				}
				else if (array3[0] == "chars")
				{
					int num4 = int.Parse(array3[1].Split(new char[]
					{
					'='
					})[1]);
					font._charInfos = new FCharInfo[num4 + 1];
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
							int num6 = int.Parse(array4[1]);
							float num7 = (float)num6;
							if (a == "id")
							{
								fcharInfo.charID = num6;
							}
							else if (a == "x")
							{
								fcharInfo.x = num7 * font._configRatio - font._element.sourceRect.x * Futile.resourceScale;
							}
							else if (a == "y")
							{
								fcharInfo.y = num7 * font._configRatio - font._element.sourceRect.y * Futile.resourceScale;
							}
							else if (a == "width")
							{
								fcharInfo.width = num7 * font._configRatio;
							}
							else if (a == "height")
							{
								fcharInfo.height = num7 * font._configRatio;
							}
							else if (a == "xoffset")
							{
								fcharInfo.offsetX = num7 * font._configRatio;
							}
							else if (a == "yoffset")
							{
								fcharInfo.offsetY = num7 * font._configRatio;
							}
							else if (a == "xadvance")
							{
								fcharInfo.xadvance = num7 * font._configRatio;
							}
							else if (a == "page")
							{
								fcharInfo.page = num6;
							}
						}
					}
					Rect uvRect = new Rect(font._element.uvRect.x + fcharInfo.x / textureSize.x, (textureSize.y - fcharInfo.y - fcharInfo.height) / textureSize.y - (1f - font._element.uvRect.yMax), fcharInfo.width / textureSize.x, fcharInfo.height / textureSize.y);
					fcharInfo.uvRect = uvRect;
					fcharInfo.uvTopLeft.Set(uvRect.xMin, uvRect.yMax);
					fcharInfo.uvTopRight.Set(uvRect.xMax, uvRect.yMax);
					fcharInfo.uvBottomRight.Set(uvRect.xMax, uvRect.yMin);
					fcharInfo.uvBottomLeft.Set(uvRect.xMin, uvRect.yMin);
					fcharInfo.width *= resourceScaleInverse;
					fcharInfo.height *= resourceScaleInverse;
					fcharInfo.offsetX *= resourceScaleInverse;
					fcharInfo.offsetY *= resourceScaleInverse;
					fcharInfo.xadvance *= resourceScaleInverse;
					font._charInfosByID[(uint)fcharInfo.charID] = fcharInfo;
					font._charInfos[num] = fcharInfo;
					num++;
				}
				else if (array3[0] == "kernings")
				{
					flag = true;
					int num8 = int.Parse(array3[1].Split(new char[]
					{
					'='
					})[1]);
					font._kerningInfos = new FKerningInfo[num8 + 100];
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
							int num9 = int.Parse(array5[1]);
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
								fkerningInfo.amount = (float)num9 * font._configRatio * resourceScaleInverse;
							}
						}
					}
					if (fkerningInfo.first != -1)
					{
						font._kerningInfos[num2] = fkerningInfo;
					}
					num2++;
				}
			}
			font._kerningCount = num2;
			if (!flag)
			{
				font._kerningInfos = new FKerningInfo[0];
			}
			if (font._charInfosByID.ContainsKey(32u))
			{
				font._charInfosByID[32u].offsetX = 0f;
				font._charInfosByID[32u].offsetY = 0f;
			}
		}
    }
}
