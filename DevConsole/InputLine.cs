using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevConsole
{
    internal class InputLine
    {
        private StringBuilder text;
        private int index;

        public string Text => text.ToString();
        public int Length => text.Length;
        public int CursorIndex
        {
            get => index;
            set
            {
                index = Math.Max(0, Math.Min(value, text.Length));
            }
        }

        public event Action<string> Submitted;

        public InputLine()
        {
            Clear();
        }

        public void Clear()
        {
            text = new StringBuilder();
            index = 0;
        }

        public void Add(string input)
        {
            foreach (char c in input)
            {
                switch (c)
                {
                    // Remove one character when backspace is pressed
                    case '\b':
                        if (index == 0) break;
                        text.Remove(--index, 1);
                        break;

                    // If Ctrl+Backspace is entered, delete a whole word
                    case '\x7F':
                        if (index == 0) break;
                        do
                        {
                            text.Remove(--index, 1);
                        }
                        while (index > 0 && !char.IsWhiteSpace(text[index - 1]));
                        break;

                    // Submit a command when enter is pressed
                    case '\r':
                    case '\n':

                        string line = text.ToString();

                        // Execute
                        Submitted?.Invoke(line);
                        Clear();

                        break;

                    // Otherwise, add to the current input
                    default:
                        text.Insert(index++, c);
                        break;
                }
            }
        }

        public void Replace(string input)
        {
            Clear();
            Add(input);
        }

        public void NextWord()
        {
            // Skip whitespace
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;

            // Read until the next whitespace is found
            while (index < text.Length && !char.IsWhiteSpace(text[index]))
                index++;
        }

        public void PrevWord()
        {
            // Skip whitespace
            while (index > 0 && char.IsWhiteSpace(text[index - 1]))
                index--;

            // Read until the next whitespace is found
            while (index > 0 && !char.IsWhiteSpace(text[index - 1]))
                index--;
        }
    }
}
