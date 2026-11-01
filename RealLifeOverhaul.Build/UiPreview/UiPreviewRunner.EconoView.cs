using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // EconoView on the kit in every state, over the day street the full menu leaves (the business's page over the other
    // street, as BizMan opens it), with the frame's app row under it. The German states read the game's own German words
    // and the mod's (its new words in English until the next batch, as a German player sees them).
    static partial void AddEconoView(List<Screen> screens)
    {
        foreach (string state in UiSamples.EconoStates)
        {
            string st = state;
            bool german = st.EndsWith("_de");
            var screen = new Screen
            {
                Name = "econoview_" + st, AppName = "EconoView", Icon = UiIcon.ChartPie, Backdrop = st.StartsWith("business") ? "02.jpg" : "13.jpg",
                Bar = () => UiSamples.ShellBar(UiSamples.RowEconoView, false), Lay = (s, f, d, k) => UiEcono.Lay(s, UiSamples.Econo(german ? (UiWords)EconoGerman : English, st), f, d, k)
            };
            if (st.EndsWith("_open"))
            {
                screen.HoverCommand = UiEcono.OptionCommand;
                screen.HoverArg = 2;
                screen.Walk = new[] { Down, Down, Up };
            }
            else if (st.StartsWith("overview"))
            {
                screen.HoverCommand = UiEcono.StatementFirst + UiStatement.Line;
                screen.HoverArg = 2;
                screen.FocusCommand = UiEcono.StatementFirst + UiStatement.Line;
                screen.FocusArg = 1;
                screen.Walk = new[] { Down, Down, Right, Down, Down, Down, Left, Up };
            }
            else if (st.StartsWith("transactions"))
            {
                screen.HoverCommand = UiEcono.FilterCommand;
                screen.HoverArg = 0;
                screen.FocusCommand = UiEcono.StatementFirst + UiStatement.Line;
                screen.FocusArg = 2;
                screen.Walk = new[] { Down, Down, Up, Up, Up, Right, Right };
            }
            else if (st == "loans_three" || st == "loans_de" || st == "investments_year")
            {
                screen.HoverCommand = UiEcono.PickCommand;
                screen.HoverArg = 1;
                screen.FocusCommand = UiEcono.PickCommand;
                screen.FocusArg = 0;
                screen.Walk = new[] { Down, Up, Right, Down, Down, Right, Left };
            }
            else if (st.StartsWith("taxes"))
            {
                screen.HoverCommand = UiEcono.PickCommand;
                screen.HoverArg = 0;
                screen.FocusCommand = UiEcono.PickCommand;
                screen.FocusArg = 1;
                screen.Walk = new[] { Down, Right, Left, Up };
            }
            else if (st.StartsWith("business"))
            {
                screen.HoverCommand = UiEcono.LinkCommand;
                screen.HoverArg = UiEcono.LinkBizMan;
                screen.FocusCommand = UiEcono.StatementFirst + UiStatement.Line;
                screen.FocusArg = 1;
                screen.Walk = new[] { Down, Down, Up, Up, Up, Right };
            }
            else
            {
                screen.HoverCommand = UiAppWindow.NavCommand;
                screen.HoverArg = 0;
                screen.Walk = new[] { Right, Right, Left };
            }
            screens.Add(screen);
        }
    }

    private static Dictionary<string, string> _econoGerman;

    // The German words a German player reads in EconoView: the mod's de.json first, then the game's own (its keys are the
    // game's), else the English sample.
    private static string EconoGerman(string key, string english)
    {
        if (_econoGerman == null)
        {
            _econoGerman = new Dictionary<string, string>();
            string managed = System.Environment.GetEnvironmentVariable("BA_MANAGED");
            string folder = string.IsNullOrWhiteSpace(managed) ? @"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed" : managed;
            string game = Path.Combine(folder, "..", "StreamingAssets", "locale", "de.json");
            string mod = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Assets/Mods/RealLifeOverhaul/Locales/de.json"));
            foreach (string path in new[] { game, mod })
            {
                if (!File.Exists(path))
                {
                    continue;
                }
                foreach (string line in File.ReadAllLines(path))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(line, @"^\s*""([^""]+)""\s*:\s*""(.*)"",?\s*$");
                    if (m.Success)
                    {
                        _econoGerman[m.Groups[1].Value] = System.Text.RegularExpressions.Regex.Unescape(m.Groups[2].Value);
                    }
                }
            }
        }
        return _econoGerman.TryGetValue(key, out string words) ? words : english;
    }
}
