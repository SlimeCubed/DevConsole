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

            public ConsoleConfig()
            {
                autopause = config.Bind("autopause", true, new ConfigurableInfo("Pause the game while the console is open."));
                font = config.Bind("font", "devconsolas",
                    new ConfigurableInfo(
                        "The default font to use in the console. Some don't look good.",
                        new AcceptFonts("font", "DisplayFont")
                    )
                );
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
                    new OpComboBox(font, listPos + new Vector2(2f, 0f), 100f)
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
