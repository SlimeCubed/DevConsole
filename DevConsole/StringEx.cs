using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

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
