using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // The Tech app: the firms, one studio as the approved window, a game's page, a data centre, every kind of firm's page, the tabs and the sub pages, over the day streets
    // the full menu leaves (02 and 13) and an interior for the data centre (05), with the frame's app row under each. The same pages in Brazilian Portuguese and Chinese,
    // whose words run longest, on the three that carry the most text.
    static partial void AddTech(List<Screen> screens)
    {
        for (int i = 0; i < UiSamples.TechStates.Length; i++)
        {
            string st = UiSamples.TechStates[i];
            TechTargets(UiSamples.Tech(ModEnglish, st), out int hoverCommand, out int hoverArg);
            screens.Add(new Screen
            {
                Name = "tech_" + st, AppName = "Tech", Icon = UiIcon.Monitor, Backdrop = st == "datacentre" ? "05.jpg" : i % 2 == 0 ? "13.jpg" : "02.jpg",
                Bar = () => UiSamples.ShellBar(4, false), HoverCommand = hoverCommand, HoverArg = hoverArg, Walk = Moves("DDR"),
                Lay = (s, f, d, k) => TechFace.Lay(s, UiSamples.Tech(ModEnglish, st), f, d, k)
            });
        }
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            UiWords words = ModWords(lang);
            string suffix = lang == "pt" ? "pt" : "zh";
            foreach (string st in new[] { "firms", "studio", "game", "datacentre", "cap_account", "books", "terms" })
            {
                string name = st;
                screens.Add(new Screen
                {
                    Name = "tech_" + name + "_" + suffix, AppName = "Tech", Icon = UiIcon.Monitor, Backdrop = "13.jpg", Bar = () => UiSamples.ShellBar(4, false), HoverCommand = UiAppWindow.NavCommand,
                    HoverArg = 1, Walk = Moves("DDR"), Single = true, Lay = (s, f, d, k) => TechFace.Lay(s, UiSamples.Tech(words, name), f, d, k)
                });
            }
        }
    }

    // What the pointer rests on in a state's hover shot: the first row of a table, else the sidebar's second section.
    private static void TechTargets(TechModel m, out int command, out int arg)
    {
        command = UiAppWindow.NavCommand;
        arg = 1;
        if (m.Shape == TechShape.Table && m.Table != null && m.Table.RowCount > 0 && m.RowKeys.Length > 0)
        {
            command = TechFace.TableBase + UiDataTable.Row;
            arg = 0;
        }
        else if (m.Shape == TechShape.Off)
        {
            command = UiDraw.CloseCommand;
            arg = 0;
        }
    }
}
