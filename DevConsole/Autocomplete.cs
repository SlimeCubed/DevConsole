using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DevConsole
{
    using Commands;

    // Allows for autocomplete in the console
    internal class Autocomplete
    {
        private readonly FSprite back;
        private readonly List<FLabel> labels = new List<FLabel>();
        private List<string> options;

        public FContainer Container { get; }
        public Color TextColor => Color.gray;
        public Color BackColor => RWCustom.Custom.RGBA2RGB(GameConsole.BackColor);
        public string CurrentOption => (options == null || options.Count == 0) ? null : options[0];

        public Autocomplete()
        {
            Container = new FContainer();
            Container.isVisible = false;
            back = new FSprite("pixel") {
                anchorX = 0f,
                anchorY = 0f,
                color = BackColor
            };
            Container.AddChild(back);
        }

        public void UpdateText(string newText)
        {
            options = CompletionOptions(newText);

            Container.isVisible = options != null && options.Count > 0;

            RefreshLabels();
        }

        public void CycleOptions(int amount)
        {
            CycleLeft(options, amount);
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            // Remove old labels
            foreach (var label in labels)
                label.RemoveFromContainer();
            labels.Clear();

            // Add new labels and resize back sprite
            Rect backRect = new Rect(0f, 0f, 0f, 0f);
            for (int i = 0; i < options.Count; i++)
            {
                if (i > 20) break;

                // Create the label
                var option = options[i];
                var label = new FLabel("font", option);
                label.anchorX = 0f;
                label.anchorY = 0f;
                label.x = 0f;
                label.y = i * 15;
                label.color = TextColor;
                labels.Add(label);
                Container.AddChild(label);

                // Expand the back rect to include this label
                Vector2 textMin = label.LocalToOther(label.textRect.min, Container);
                Vector2 textMax = label.LocalToOther(label.textRect.max, Container);
                backRect.xMin = Math.Min(backRect.xMin, textMin.x);
                backRect.xMax = Math.Max(backRect.xMax, textMax.x);
                backRect.yMin = Math.Min(backRect.yMin, textMin.y);
                backRect.yMax = Math.Max(backRect.yMax, textMax.y);
            }
            if(backRect.width > 0 && backRect.height > 0)
                backRect = backRect.CloneWithExpansion(4f);
            back.SetPosition(backRect.min);
            back.scaleX = backRect.width;
            back.scaleY = backRect.height;
        }

        private List<string> CompletionOptions(string input)
        {
            // Don't suggest anything if there's no text
            if (input.All(char.IsWhiteSpace)) return new List<string>();

            var finalArgs = input.SplitCommandLine().ToList();
            string writingArg;

            // An argument is considered final if there is a whitespace after it
            // Quotes around args are removed, so if there's a starting quote whitespace doesn't finalize the argument
            // This may mess up with escaped unbalanced quotes, such as: command \"arg1 arg2
            if (input.Length > 0 && finalArgs.Count > 0
                && (!char.IsWhiteSpace(input[input.Length - 1]) || finalArgs[finalArgs.Count - 1][0] == '"'))
            {
                writingArg = finalArgs[finalArgs.Count - 1];
                finalArgs.RemoveAt(finalArgs.Count - 1);
            }
            else
            {
                writingArg = "";
            }

            // Get a list of all possible options
            string[] finalArgsArray = finalArgs.ToArray();
            List<string> options = new List<string>();

            foreach (var ac in GameConsole.AutoCompletableCommands)
            {
                IEnumerable<string> acOptions;
                try
                {
                    acOptions = ac.GetArgOptions(finalArgsArray);
                }
                catch
                {
                    continue;
                }
                if (acOptions != null)
                {
                    options.AddRange(acOptions
                        .Select(str => str.EscapeCommandLine())
                        .Where(str => str.StartsWith(writingArg, StringComparison.OrdinalIgnoreCase))
                        .Select(str => str.Substring(writingArg.Length))
                        .Where(str => !string.IsNullOrEmpty(str))
                    );
                }
            }

            // Remove duplicates, seek to the last selected option
            options.Sort();
            DedupSorted(options);
            return options;
        }

        // Remove duplicated elements in a sorted list
        private void DedupSorted<T>(List<T> list)
        {
            var eq = EqualityComparer<T>.Default;
            bool first = true;
            T lastEntry = default(T);
            list.RemoveAll(entry =>
            {
                bool matches = false;
                matches = eq.Equals(lastEntry, entry);
                lastEntry = entry;

                if (first) matches = first = false;
                return matches;
            });
        }

        // Shift the elements in the list, wrapping
        private void CycleLeft<T>(List<T> list, int amount)
        {
            if (list.Count <= 1) return;

            amount = (amount % list.Count + list.Count) % list.Count;

            if (amount == 0) return;
            var temp = list.GetRange(0, amount);
            list.RemoveRange(0, amount);
            list.AddRange(temp);
        }

        public void Update(StringBuilder inputString)
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                // Autocomplete when tab is pressed
                string acOption = CurrentOption;
                if (acOption != null && acOption.Length != 0)
                    inputString.Append(acOption);
                UpdateText(inputString.ToString());
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
                CycleOptions(1);
            else if (Input.GetKeyDown(KeyCode.DownArrow))
                CycleOptions(-1);
        }
    }
}
