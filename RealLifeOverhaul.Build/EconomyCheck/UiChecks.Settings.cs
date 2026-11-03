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
            FirstOpenChecks();
            WheelPaneChecks();
            GuideFillChecks();
        }
    }

    // A Guide page is written with {facts} the features fill (Guide.Fill, which the old Guide's page, its search and the set-up check all use).
    // The kit's window once showed the raw text, {sale_days} and all. No screen of the kit may read a topic's Body() without Guide.Fill.
    private static void GuideFillChecks()
    {
        string ui = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui"));
        int calls = 0, filled = 0;
        string offender = null;
        foreach (string file in Directory.GetFiles(ui, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            int c = Regex.Matches(text, @"\.Body\(\)").Count, f = Regex.Matches(text, @"Guide\.Fill\(\s*\w+\.Body\(\)\s*\)").Count;
            calls += c;
            filled += f;
            offender = c != f && offender == null ? System.IO.Path.GetFileName(file) : offender;
        }
        Check(calls >= 1 && calls == filled && offender == null, "guide: the kit reads a topic's text only through Guide.Fill (" + filled + " of " + calls + " reads)" + (offender != null ? ", not in " + offender : string.Empty));
        string screen = File.ReadAllText(System.IO.Path.Combine(ui, "Settings", "GuideScreen.cs"));
        int hit = screen.IndexOf("private bool Hit(int index)", System.StringComparison.Ordinal);
        int build = screen.IndexOf("private void BuildPage()", System.StringComparison.Ordinal);
        string hitBody = hit > 0 ? screen.Substring(hit, System.Math.Min(420, screen.Length - hit)) : string.Empty;
        string buildBody = build > 0 ? screen.Substring(build, System.Math.Min(900, screen.Length - build)) : string.Empty;
        Check(hitBody.Contains("BodyOf(index)") && buildBody.Contains("UiReaderRules.Parse(BodyOf(_pageAt))"),
            "guide: the kit's search and its page both read the filled text, so a {fact} is found by its words and shown as its value");
    }

    // The wheel scrolls the pane under the pointer, selected or not. The Guide and Staff and roles hold two scrolling panes (the
    // topic list and the page); the screen is told the clip the pointer is over, and had always scrolled the page whichever it was.
    private static void WheelPaneChecks()
    {
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f);
        s.Begin("wheel", frame.Width, frame.Height, true);
        var nav = new UiNavEntry[40];
        for (int i = 0; i < nav.Length; i++)
        {
            nav[i] = new UiNavEntry { Label = "Topic " + i, Icon = UiIcon.Info };
        }
        int navClip = s.ClipCount;
        float offset = 0f;
        UiAppWindow.ShellFooted(s, frame, "Guide", UiIcon.Info, nav, 0, ref offset, null, 0, UiDefaultKeys.Keyboard, out float x, out float y, out float w, out float bottom);
        int pageClip = s.ClipCount;
        s.AddClip(x, y, w, bottom - y, 0f);
        Check(pageClip == navClip + 1 && s.ClipCount == pageClip + 1 && s.Clips[navClip].X < x && s.Clips[navClip].ContentHeight > s.Clips[navClip].H,
            "wheel: the sidebar's scroll region is the clip the footed window adds first, apart from the page's, and it scrolls (more entries than room)");
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui\Settings"));
        foreach (string name in new[] { "GuideScreen.cs", "StaffScreen.cs" })
        {
            string text = File.ReadAllText(System.IO.Path.Combine(folder, name));
            int lay = text.IndexOf("_navClip = s.ClipCount;", System.StringComparison.Ordinal);
            int shell = text.IndexOf("UiAppWindow.ShellFooted(", System.StringComparison.Ordinal);
            int wheel = text.IndexOf("public void Wheel(int clip, float notches)", System.StringComparison.Ordinal);
            string body = wheel > 0 ? text.Substring(wheel, System.Math.Min(520, text.Length - wheel)) : string.Empty;
            Check(lay > 0 && shell > lay && body.Contains("if (clip == _navClip)") && body.Contains("_navOffset = Math.Max(0f, _navOffset - notches")
                  && body.Contains("_page.Offset -= notches"),
                "wheel: " + name + " scrolls the sidebar when the pointer is over it and the page otherwise, no click or selection first");
        }
    }

    // An app of the mod's on the kit has its panel built on the first opening, asleep. The kit's face must bind over it right
    // then (PhoneApp.Ready), not at the next second's tick, or the game's old panel shows beside the kit on that first opening.
    private static void FirstOpenChecks()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string apps = File.ReadAllText(System.IO.Path.Combine(scripts, "Core", "GameUi", "PhoneApps.cs"));
        string shell = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "Shell", "UiShell.cs"));
        string pages = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "Shell", "ShellPages.cs"));
        int ready = apps.IndexOf("public bool Ready()", System.StringComparison.Ordinal);
        int kitOver = ready > 0 ? apps.IndexOf("KitOver();", ready, System.StringComparison.Ordinal) : -1;
        int rootSet = ready > 0 ? apps.IndexOf("Root = root;", ready, System.StringComparison.Ordinal) : -1;
        int returned = rootSet > 0 ? apps.IndexOf("return true;", rootSet, System.StringComparison.Ordinal) : -1;
        Check(rootSet > 0 && kitOver > rootSet && kitOver < returned && apps.Contains("RealLifeOverhaul.Ui.UiShell.Built(_def.KitScreen)"),
            "first open: the app's panel is built, then its kit face binds over it before the game switches the panel on");
        int built = shell.IndexOf("public static void Built(string screenId)", System.StringComparison.Ordinal);
        string body = built > 0 ? shell.Substring(built, System.Math.Min(700, shell.Length - built)) : string.Empty;
        Check(body.Contains("UiDirector.On") && body.Contains("UiScreenSwitches.On(screenId)") && body.Contains("Faces[i].Install()") && body.Contains("ShellPages.Built(screenId)"),
            "first open: the bind happens only with the interface and the screen's own switch on, for every mod app and every page");
        Check(pages.Contains("KitScreen = p.ScreenId") && !pages.Contains("Opened = () => page.Host"),
            "first open: the overhaul's pages bind through the same path, not one of their own");
        int apps2 = 0, withScreen = 0, withoutScreen = 0;
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            if (!text.Contains("new PhoneAppDef"))
            {
                continue;
            }
            int kits = Regex.Matches(text, @"\bKit = ").Count, screens = Regex.Matches(text, @"\bKitScreen = ").Count;
            apps2 += kits;
            withScreen += System.Math.Min(kits, screens);
            withoutScreen += System.Math.Max(0, kits - screens);
        }
        bool named = true;
        foreach (string app in new[] { "Meta/SettingsApp.cs", "Meta/GuideApp.cs", "Meta/StaffApp.cs", "Planner/PlannerDirector.cs", "Calendar/CalendarDirector.cs",
                                       "Photos/CameraApp.cs", "Photos/GalleryApp.cs", "Markets/MarketDirector.App.cs" })
        {
            string text = File.ReadAllText(System.IO.Path.Combine(scripts, app));
            bool one = text.Contains("new PhoneAppDef") && text.Contains("Kit = ") && text.Contains("KitScreen = ") && text.Contains("UiShell.Face(");
            Check(one, "first open: " + app + " puts its kit face on the app (Kit, KitScreen, UiShell.Face), so PhoneApp.Ready binds it over the panel at the first opening");
            named &= one;
        }
        Check(named, "first open: Mod settings, the Guide, Staff and roles and the other kit apps all take the one first-open path");
        Check(apps2 >= 8 && withoutScreen == 0, "first open: every app with a kit face names its screen (" + withScreen + " of " + apps2 + "), so none is left to show the old panel first");
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
