using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEngine;

namespace DevConsole
{
    /// <summary>
    /// Extension methods on strings.
    /// </summary>
    public static class StringEx
    {
        private static readonly Dictionary<char, string> escapeCodes = new Dictionary<char, string>()
        {
            { '"', "\"" },
            { '\\', "\\" }
        };

        /// <summary>
        /// Splits a string into command line arguments.
        /// </summary>
        /// <param name="commandLine">The line of </param>
        /// <returns></returns>
        public static IEnumerable<string> SplitCommandLine(this string commandLine)
        {
            int cursor = 0;
            int len = commandLine.Length;

            while (cursor < len)
            {
                // Eat whitespace before argument
                while (cursor < len && char.IsWhiteSpace(commandLine[cursor]))
                    cursor++;

                if (cursor >= len) yield break;

                // Slice out the argument
                int sliceStart = cursor;
                bool quoted = commandLine[cursor] == '"';
                StringBuilder arg = new StringBuilder();
                if (quoted) cursor++;

                while (cursor < len)
                {
                    char c = commandLine[cursor];

                    switch (c)
                    {
                        case '"':
                            if (quoted && (cursor + 1 == commandLine.Length || char.IsWhiteSpace(commandLine[cursor + 1])))
                            {
                                // This quote is at the end of an argument
                                // Slice it here
                                cursor++;
                                goto argDone;
                            }
                            break;


                        case '\\':
                            if (cursor + 1 < commandLine.Length && escapeCodes.TryGetValue(commandLine[cursor + 1], out string escaped))
                            {
                                // There is an escaped character here
                                // Slice just before the escape sequence
                                arg.Append(commandLine.Substring(sliceStart, cursor - sliceStart));
                                arg.Append(escaped);
                                cursor++;
                                sliceStart = cursor + 1;
                            }
                            break;

                        default:
                            // Split at spaces
                            if (!quoted && char.IsWhiteSpace(c))
                            {
                                goto argDone;
                            }
                            break;
                    }

                    cursor++;
                }

            argDone:
                // Append the rest
                if (sliceStart < cursor && sliceStart < commandLine.Length)
                    arg.Append(commandLine.Substring(sliceStart, Math.Min(cursor, commandLine.Length) - sliceStart));

                // Remove matching quotes
                if (arg.Length > 1 && arg[0] == '"' && arg[arg.Length - 1] == '"')
                {
                    arg.Remove(arg.Length - 1, 1);
                    arg.Remove(0, 1);
                }

                if (arg.Length > 0)
                    yield return arg.ToString();
            }
        }

        /// <summary>
        /// Splits a string on newlines.
        /// </summary>
        /// <param name="text">The text to split.</param>
        /// <returns>An enumerable of all lines in the string.</returns>
        public static IEnumerable<string> SplitLines(this string text)
        {
            using (StringReader sr = new StringReader(text))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                    yield return line;
            }
        }

        /// <summary>
        /// Splits a string so that each segment's width is below <paramref name="maxWidth"/>.
        /// </summary>
        /// <param name="text">The text to split.</param>
        /// <param name="maxWidth">The upper bound for line width.</param>
        /// <param name="font">The font used to measure splitting.</param>
        /// <returns>String segments that will be under <paramref name="maxWidth"/> when displayed using <paramref name="font"/>.</returns>
        public static IEnumerable<string> SplitLongLines(this string text, float maxWidth, FFont font)
        {
            int sliceStart = 0;
            int lastWhitespace = 0;
            char lastChar = '\0';

            char[] chars = text.ToCharArray();
            int len = text.Length;
            float x = 0f;
            for(int i = 0; i < len; i++)
            {
                char c = chars[i];

                FCharInfo charInfo;
                if (font._charInfosByID.ContainsKey(c))
                    charInfo = font._charInfosByID[c];
                else
                    charInfo = font._charInfosByID[0u];

                // Find kerning offset
                FKerningInfo kerningInfo = font._nullKerning;
                for (int l = 0; l < font._kerningCount; l++)
                {
                    FKerningInfo fkerningInfo2 = font._kerningInfos[l];
                    if (fkerningInfo2.first == lastChar && fkerningInfo2.second == c)
                        kerningInfo = fkerningInfo2;
                }

                // Advance based on kerning
                if (i == sliceStart)
                    x = -charInfo.offsetX;
                else
                    x += kerningInfo.amount + font._textParams.scaledKerningOffset;

                if (char.IsWhiteSpace(c))
                {
                    // Never split on whitespace
                    lastWhitespace = i;

                    x += charInfo.xadvance;
                }
                else
                {
                    // Split if this char would go over the edge
                    if (x + charInfo.width > maxWidth)
                    {
                        int sliceEnd;
                        if (sliceStart == lastWhitespace)
                            sliceEnd = i;
                        else
                            sliceEnd = lastWhitespace + 1;
                        yield return text.Substring(sliceStart, sliceEnd - sliceStart);
                        sliceStart = sliceEnd;
                        lastWhitespace = sliceEnd;
                        i = sliceStart;
                        x = 0;
                    }
                    else
                        x += charInfo.xadvance;
                }

                lastChar = c;
            }

            yield return text.Substring(sliceStart);
        }

        /// <summary>
        /// Escapes all special characters in an input string for use in the command line.
        /// Strings that contain whitespace will be surrounded in quotes
        /// </summary>
        /// <param name="input">The raw string.</param>
        /// <returns>A string with special characters escaped.</returns>
        public static string EscapeCommandLine(this string input)
        {
            string o = input.Replace("\\", "\\\\").Replace("\"", "\"");
            if (o.Any(char.IsWhiteSpace)) o = $"\"{o}\"";
            return o;
        }
    }
}
