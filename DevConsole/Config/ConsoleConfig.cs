using Menu.Remix.MixedUI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DevConsole
{
    namespace Config
    {
        internal class ConsoleConfig : OptionInterface
        {
            public static Configurable<bool> autopause;
            public static Configurable<string> font;
            public static Configurable<KeyCode> keybind;
            public static Configurable<bool> scanOnStartup;
            public static Configurable<string> defaultPos;
            public static Configurable<bool> saveHistory;

            public ConsoleConfig()
            {
                autopause = config.Bind(
                    key: "autopause",
                    defaultValue: true,
                    info: new ConfigurableInfo("Pause the game while the console is open."));
                
                font = config.Bind(
                    key: "font",
                    defaultValue: "devconsolas",
                    info: new ConfigurableInfo(
                        description: "The default font to use in the console. Some don't look good.",
                        acceptable: new AcceptFonts("font", "DisplayFont")
                    ));
                
                keybind = config.Bind(
                    key: "keybind",
                    defaultValue: KeyCode.BackQuote,
                    info: new ConfigurableInfo("The key that, when pressed, opens the console."));

                scanOnStartup = config.Bind(
                    key: "scan_on_startup",
                    defaultValue: true,
                    info: new ConfigurableInfo("Scan for spawnable objects on startup."));

                defaultPos = config.Bind(
                    key: "default_pos",
                    defaultValue: "player",
                    info: new ConfigurableInfo(
                        description: "The position that commands should affect by default.",
                        acceptable: new ConfigAcceptableList<string>("player", "cursor", "camera")));

                saveHistory = config.Bind(
                    key: "save_history",
                    defaultValue: true,
                    info: new ConfigurableInfo("Save command history between sessions."));
            }

            public override void Initialize()
            {
                Vector2 listPos = new(300f, 450f);

                Tabs = new OpTab[]
                {
                    new OpTab(this)
                };

                // Header
                Tabs[0].AddItems(
                    new OpLabel(new Vector2(300f - 100f, 500f), new Vector2(200f, 40f), "Dev Console", bigText: true),
                    new OpLabel(new Vector2(300f - 100f, 485f), new Vector2(200f, 15f), $"version {DevConsoleMod.MOD_VERSION}") { color = Color.gray }
                );

                // Autopause
                Tabs[0].AddItems(
                    new OpLabel(listPos - new Vector2(102f, 0f), new Vector2(100f, 24f), "Pause when open")
                    {
                        alignment = FLabelAlignment.Right,
                        verticalAlignment = OpLabel.LabelVAlignment.Center,
                        description = autopause.info.description
                    },
                    new OpCheckBox(autopause, listPos + new Vector2(2f, 0f))
                );
                listPos.y -= 29f;

                // Font
                Tabs[0].AddItems(
                    new OpLabel(listPos - new Vector2(102f, 0f), new Vector2(100f, 24f), "Font")
                    {
                        alignment = FLabelAlignment.Right,
                        verticalAlignment = OpLabel.LabelVAlignment.Center,
                        description = font.info.description
                    },
                    new OpComboBox(font, listPos + new Vector2(2f, 0f), 140f)
                );
                listPos.y -= 39f;

                // Keybind
                Tabs[0].AddItems(
                    new OpLabel(listPos - new Vector2(102f, 0f), new Vector2(100f, 34f), "Keybind")
                    {
                        alignment = FLabelAlignment.Right,
                        verticalAlignment = OpLabel.LabelVAlignment.Center,
                        description = font.info.description
                    },
                    new OpKeyBinder(keybind, listPos + new Vector2(4f, 2f), new Vector2(146f, 30f), false)
                );
                listPos.y -= 29f;

                // Scan on startup
                Tabs[0].AddItems(
                    new OpLabel(listPos - new Vector2(102f, 0f), new Vector2(100f, 24f), "Scan on load")
                    {
                        alignment = FLabelAlignment.Right,
                        verticalAlignment = OpLabel.LabelVAlignment.Center,
                        description = scanOnStartup.info.description
                    },
                    new OpCheckBox(scanOnStartup, listPos + new Vector2(2f, 0f))
                );
                listPos.y -= 29f;

                // Save command history
                Tabs[0].AddItems(
                    new OpLabel(listPos - new Vector2(102f, 0f), new Vector2(100f, 24f), "Save history")
                    {
                        alignment = FLabelAlignment.Right,
                        verticalAlignment = OpLabel.LabelVAlignment.Center,
                        description = saveHistory.info.description
                    },
                    new OpCheckBox(saveHistory, listPos + new Vector2(2f, 0f))
                );
                listPos.y -= 29f;

                // Default position
                Tabs[0].AddItems(
                    new OpLabel(listPos - new Vector2(102f, 0f), new Vector2(100f, 24f), "Default position")
                    {
                        alignment = FLabelAlignment.Right,
                        verticalAlignment = OpLabel.LabelVAlignment.Center,
                        description = defaultPos.info.description
                    },
                    new OpComboBox(defaultPos, listPos + new Vector2(2f, 0f), 140f)
                );
                listPos.y -= 29f;
            }

            private class AcceptFonts : ConfigAcceptableList<string>
            {
                public AcceptFonts(params string[] safeFonts) : base(safeFonts)
                {
                }

                public override string[] AcceptableValues => Futile.atlasManager._fontsByName.Keys.ToArray();

                public override bool IsValid(object value) => true;
                public override object Clamp(object value) => value;
            }
        }
    }
}
