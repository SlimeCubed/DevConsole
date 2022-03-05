using System.Collections.Generic;
using System.Linq;
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

                self.Tabs = new OpTab[]
                {
                    new OpTab("config")
                };

                // Header
                self.Tabs[0].AddItems(
                    new OpLabel(new Vector2(300f - 100f, 500f), new Vector2(200f, 40f), "Dev Console", bigText: true),
                    new OpLabel(new Vector2(300f - 100f, 485f), new Vector2(200f, 15f), $"version {DevConsoleMod.versionString}") { color = Color.gray }
                );

                // Options
                string desc;

                // Autopause
                desc = "Pause the game while the console is open.";
                self.Tabs[0].AddItems(
                    new OpLabel(listPos - new Vector2(102f, 0f), new Vector2(100f, 24f), "Pause when open")
                    {
                        alignment = FLabelAlignment.Right,
                        verticalAlignment = OpLabel.LabelVAlignment.Center,
                        description = desc
                    },
                    new OpCheckBox(listPos + new Vector2(2f, 0f), "devconsole.autopause", true)
                    {
                        description = desc
                    }
                );
                listPos.y -= 29f;

                // Font
                desc = "The default font to use in the console. Some don't look good.";
                self.Tabs[0].AddItems(
                    new OpLabel(listPos - new Vector2(102f, 0f), new Vector2(100f, 24f), "Font")
                    {
                        alignment = FLabelAlignment.Right,
                        verticalAlignment = OpLabel.LabelVAlignment.Center,
                        description = desc
                    },
                    new OpComboBox(listPos + new Vector2(2f, 0f), 100f, "devconsole.font", Futile.atlasManager._fontsByName.Keys.ToArray(), "devconsolas")
                    {
                        description = desc
                    }
                );
                listPos.y -= 29f;
            }

            public static void ConfigOnChange(OptionInterface self)
            {
                DevConsoleMod.autopause = OptionInterface.config.GetBool("devconsole.autopause");
                if (OptionInterface.config.TryGetValue("devconsole.font", out string font))
                    GameConsole.CurrentFont = font;
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
