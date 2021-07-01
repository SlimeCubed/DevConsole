using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Partiality.Modloader;
using OptionalUI;
using UnityEngine;

namespace DevConsole
{
    namespace Config
    {
        internal static class ConsoleConfig
        {
            public static void Initialize(OptionInterface self)
            {
                Vector2 listPos = new Vector2(300f, 450f);

                void AddToggle(string key, string name, string desc, bool defaultValue)
                {
                    const float width = 150f;

                    // Add default option to the end of the description
                    desc = $"{desc} Defaults to {defaultValue}.";

                    self.Tabs[0].AddItems(
                        new OpCheckBox(listPos - new Vector2(width / 2f, 12f), key, defaultValue)
                        {
                            description = desc
                        },
                        new OpLabel(listPos - new Vector2(width / 2f - 24f - 5f, 12f), new Vector2(width - 24f - 5f, 24f), name, FLabelAlignment.Right)
                        {
                            verticalAlignment = OpLabel.LabelVAlignment.Center,
                            description = desc
                        }
                    );
                    listPos.y -= 29f;
                }

                self.Tabs = new OpTab[]
                {
                    new OpTab("config")
                };

                // Header
                self.Tabs[0].AddItems(
                    new OpLabel(new Vector2(300f - 100f, 500f), new Vector2(200f, 40f), "Dev Console", bigText: true),
                    new OpLabel(new Vector2(300f - 100f, 485f), new Vector2(200f, 15f), $"version {DevConsoleMod.versionString}") { color = Color.gray }
                );

                // Toggles
                AddToggle("devconsole.autopause", "Pause when open", "Pause the game while the console is open.", false);
            }

            public static void ConfigOnChange(OptionInterface self)
            {
                DevConsoleMod.autopause = OptionInterface.config.GetBool("devconsole.autopause");
            }

            private static bool GetBool(this Dictionary<string, string> self, string key, bool defaultValue = false)
            {
                try { return bool.Parse(self[key]); }
                catch { return defaultValue; }
            }
        }
    }

    internal partial class DevConsoleMod : PartialityMod
    {
        public object LoadOI() => Config.ConfigGenerator.LoadOI(this);
    }
}
