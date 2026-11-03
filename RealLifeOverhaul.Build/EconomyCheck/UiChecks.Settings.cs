using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Meta;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static bool _settingsChecked;

    private static readonly int[] SettingsKitCommands =
    {
        UiSettingsList.RowCommand, UiSettingsList.SearchCommand, UiSettingsList.PresetCommand, UiSettingsList.HistoryCommand,
        UiSettingsList.GroupCommand, UiSettingsList.ChoiceCommand
    };

    private static readonly int[] GuideKitCommands =
    {
        UiAppWindow.NavCommand, UiSettingsList.SearchCommand, UiReader.AnchorCommand, UiReader.RelatedCommand, UiReader.ContentsCommand
    };

    private static readonly int[] StaffKitCommands =
    {
        UiAppWindow.NavCommand, UiSettingsList.SearchCommand, UiReader.AnchorCommand, UiReader.RelatedCommand
    };

    private static readonly int[] ReportCommands =
    {
        UiReport.FieldCommand, UiReport.WhereCommand, UiReport.CheckCommand, UiReport.LinkCommand, UiReport.ButtonCommand, UiReport.OptionCommand
    };

    private static readonly int[] SaveCommands = { UiCard.ButtonCommand, UiCard.CheckCommand, UiCard.FieldCommand };

    static partial void UiCasesSettings(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id == "mod_settings")
        {
            foreach (string state in UiSamples.SettingsStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "mod settings (" + st + ")", Title = "Mod settings", Commands = SettingsKitCommands,
                    Lay = (s, f, d, k, t) => UiSamples.SettingsKit(s, f, d, k, t, st)
                });
            }
        }
        else if (def.Id == "guide")
        {
            foreach (string state in new[] { "page", "search", "whatsnew", "long", "empty" })
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "guide (" + st + ")", Title = "Guide", Commands = GuideKitCommands,
                    Lay = (s, f, d, k, t) => UiSamples.GuideKit(s, f, d, k, t, st)
                });
            }
        }
        else if (def.Id == "staff_roles")
        {
            foreach (string state in new[] { "page", "search", "empty" })
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "staff (" + st + ")", Title = "Staff and roles", Commands = StaffKitCommands,
                    Lay = (s, f, d, k, t) => UiSamples.StaffKit(s, f, d, k, t, st)
                });
            }
        }
        else if (def.Id == "bug_report")
        {
            cases.Add(new UiScreenCase
            {
                Def = def, Name = "bug report", Title = "City", Commands = ReportCommands,
                Lay = (s, f, d, k, t) => UiSamples.BugKit(s, f, d, k, t, false)
            });
            cases.Add(new UiScreenCase
            {
                Def = def, Name = "bug report with a screenshot", Title = "City", Commands = ReportCommands,
                Lay = (s, f, d, k, t) => UiSamples.BugKit(s, f, d, k, t, true)
            });
        }
        else if (def.Id == "save_tools")
        {
            for (int i = 0; i < 3; i++)
            {
                int which = i;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "save tools " + which, Title = "City", Commands = SaveCommands,
                    Lay = (s, f, d, k, t) => UiSamples.SaveKit(s, f, d, k, t, which)
                });
            }
        }
        else
        {
            return;
        }
        if (!_settingsChecked)
        {
            _settingsChecked = true;
            SettingsTreeChecks();
            ReportJoinChecks();
            ReleaseDateChecks();
        }
    }

    private static void SettingsTreeChecks()
    {
        int options = 0;
        int sections = 0;
        for (int i = 0; i < OptionsTree.Groups.Length; i++)
        {
            int groupSections, groupOptions;
            OptionsTree.Counts(OptionsTree.Groups[i], out groupSections, out groupOptions);
            sections += groupSections;
            options += groupOptions;
            Check(groupOptions > 0, "settings: " + OptionsTree.Groups[i].Id + " declares options");
        }
        Check(options >= 300 && sections >= 100, "settings: the tree still holds the options (" + options + " across " + sections + " sections)");
        TreeSection sounds = null;
        for (int i = 0; i < OptionsTree.Groups.Length && sounds == null; i++)
        {
            for (int s = 0; s < OptionsTree.Groups[i].Sections.Count; s++)
            {
                if (OptionsTree.Groups[i].Sections[s].GuideFeature != null)
                {
                    sounds = OptionsTree.Groups[i].Sections[s];
                    break;
                }
            }
        }
        Check(sounds != null && OptionsTree.GuidePage(sounds) == "feature_" + sounds.GuideFeature, "settings: a section's About this opens its Guide page");
        Check(OptionsTree.GuidePage(null) == null, "settings: a missing section opens no page");
    }

    private static void ReportJoinChecks()
    {
        string joined = BugReportRules.Join("The banks showed 0.", "An offer with a rate.", "Where: Finance, Borrow");
        Check(joined.Contains("The banks showed 0.") && joined.Contains("An offer with a rate.") && joined.Contains("Finance") && joined.Length <= BugReportRules.MostWords,
            "bug report: the two boxes and the place join into one report within the word limit");
        string huge = new string('a', 400);
        string cut = BugReportRules.Join(huge, huge, "Where: the city");
        Check(cut.Length <= BugReportRules.MostWords, "bug report: a long report is cut to " + BugReportRules.MostWords);
    }

    private static void ReleaseDateChecks()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Meta"));
        string changelog = File.ReadAllText(System.IO.Path.Combine(folder, "Changelog.cs"));
        int dated = 0;
        int missing = 0;
        foreach (Match match in Regex.Matches(changelog, @"new Entry\((\d+),"))
        {
            int order = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (order == 63095)
            {
                Check(!ReleaseDates.Try(order, out int _, out int _, out int _), "what's new: the unnumbered row has no day yet");
                continue;
            }
            if (ReleaseDates.Try(order, out int year, out int month, out int day) && year >= 2026 && month >= 1 && month <= 12 && day >= 1 && day <= 31)
            {
                dated++;
            }
            else
            {
                missing++;
            }
        }
        Check(missing == 0 && dated > 100, "what's new: every released row has a day (" + dated + " dated, " + missing + " missing)");
        string line = ReleaseDates.Words(63090, key => key == "reallife_calendar_month_11" ? "November" : key);
        Check(line == "2 November 2026", "what's new: 0.63.9 reads 2 November 2026 (" + line + ")");
    }
}
