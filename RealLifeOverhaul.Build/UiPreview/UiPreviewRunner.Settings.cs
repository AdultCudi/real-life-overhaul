using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    static partial void AddSettings(List<Screen> screens)
    {
        foreach (string state in UiSamples.SettingsStates)
        {
            string st = state;
            screens.Add(new Screen
            {
                Name = "settings_" + st,
                AppName = "Mod settings",
                Icon = UiIcon.Gear,
                Backdrop = st == "trouble" || st == "tests" ? "02.jpg" : "13.jpg",
                Bar = () => UiSamples.ShellBar(0, false),
                Lay = (s, f, d, k) => UiSamples.SettingsKit(s, f, d, k, English, st),
                HoverCommand = UiSettingsList.GroupCommand,
                HoverArg = 2,
                FocusCommand = UiSettingsList.RowCommand,
                FocusArg = 2,
                Walk = new[] { Down, Down, Right }
            });
        }
        screens.Add(SettingsShot("settings_longest", "Mod settings", UiIcon.Gear, "02.jpg",
            (s, f, d, k) => UiSamples.SettingsKit(s, f, d, k, French, "you"), UiSettingsList.GroupCommand, 1));
        screens.Add(SettingsShot("guide_page", "Guide", UiIcon.Info, "13.jpg", (s, f, d, k) => UiSamples.GuideKit(s, f, d, k, English, "page"), UiAppWindow.NavCommand, 1));
        screens.Add(Typing(SettingsShot("guide_search", "Guide", UiIcon.Info, "13.jpg", (s, f, d, k) => UiSamples.GuideKit(s, f, d, k, English, "search"), UiSettingsList.SearchCommand, 0),
            UiSettingsList.SearchCommand, 0));
        screens.Add(SettingsShot("guide_whatsnew", "Guide", UiIcon.Info, "02.jpg", (s, f, d, k) => UiSamples.GuideKit(s, f, d, k, English, "whatsnew"), UiReader.AnchorCommand, 0));
        screens.Add(SettingsShot("guide_long", "Guide", UiIcon.Info, "13.jpg", (s, f, d, k) => UiSamples.GuideKit(s, f, d, k, English, "long"), UiReader.RelatedCommand, 0));
        screens.Add(SettingsShot("guide_empty", "Guide", UiIcon.Info, "13.jpg", (s, f, d, k) => UiSamples.GuideKit(s, f, d, k, English, "empty"), UiSettingsList.SearchCommand, 0));
        screens.Add(SettingsShot("staff_page", "Staff and roles", UiIcon.People, "13.jpg", (s, f, d, k) => UiSamples.StaffKit(s, f, d, k, English, "page"), UiReader.AnchorCommand, 0));
        screens.Add(Typing(SettingsShot("staff_search", "Staff and roles", UiIcon.People, "02.jpg", (s, f, d, k) => UiSamples.StaffKit(s, f, d, k, English, "search"), UiSettingsList.SearchCommand, 0),
            UiSettingsList.SearchCommand, 0));
        screens.Add(SettingsShot("staff_empty", "Staff and roles", UiIcon.People, "13.jpg", (s, f, d, k) => UiSamples.StaffKit(s, f, d, k, English, "empty"), UiSettingsList.SearchCommand, 0));
        screens.Add(SettingsShot("bug_report", "Report a bug", UiIcon.Bug, "13.jpg", (s, f, d, k) => UiSamples.BugKit(s, f, d, k, English, false), UiReport.ButtonCommand, 1));
        screens.Add(Typing(SettingsShot("bug_report_shot", "Report a bug", UiIcon.Bug, "02.jpg", (s, f, d, k) => UiSamples.BugKit(s, f, d, k, English, true), UiReport.FieldCommand, 0),
            UiReport.FieldCommand, 0));
        screens.Add(SettingsShot("save_prepare", "Save tools", UiIcon.Warning, "13.jpg", (s, f, d, k) => UiSamples.SaveKit(s, f, d, k, English, 0), UiCard.ButtonCommand, 0));
        screens.Add(SettingsShot("save_reset", "Save tools", UiIcon.Warning, "02.jpg", (s, f, d, k) => UiSamples.SaveKit(s, f, d, k, English, 1), UiCard.ButtonCommand, 0));
        screens.Add(SettingsShot("save_repair", "Save tools", UiIcon.Warning, "13.jpg", (s, f, d, k) => UiSamples.SaveKit(s, f, d, k, English, 2), UiCard.ButtonCommand, 1));
    }

    private static Screen SettingsShot(string name, string app, UiIcon icon, string backdrop, LayOut lay, int hover, int arg)
    {
        return new Screen
        {
            Name = name,
            AppName = app,
            Icon = icon,
            Backdrop = backdrop,
            Bar = () => UiSamples.ShellBar(0, false),
            Lay = lay,
            HoverCommand = hover,
            HoverArg = arg,
            FocusCommand = hover,
            FocusArg = arg,
            Walk = new[] { Down, Right }
        };
    }

    private static Dictionary<string, string> _french;

    // French, the longest labels on the settings sidebar.
    private static string French(string key, string english)
    {
        if (_french == null)
        {
            _french = new Dictionary<string, string>();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..",
                "Assets", "Mods", "RealLifeOverhaul", "Locales", "fr.json"));
            if (File.Exists(path))
            {
                foreach (Match pair in Regex.Matches(File.ReadAllText(path), FrenchPair))
                {
                    _french[pair.Groups[1].Value] = Regex.Unescape(pair.Groups[2].Value);
                }
            }
        }
        return key != null && _french.TryGetValue(key, out string words) ? words : english;
    }

    private const string FrenchPair = "\"([^\"\\\\]+)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"";
}
