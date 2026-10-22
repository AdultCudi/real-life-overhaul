using System;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Calendar;

// Faults found in Scripts/Calendar: a source that threw after adding its nightly lines lost its entries but kept its
// lines on the page, and the entries it lost were not given back to the pool; and the app's page was made anew every
// second while the game's phone was still being built, as every app of the mod's made its own (PhoneApp.PhoneUp).
internal static partial class Program
{
    private static void CalendarFixes()
    {
        var list = new CalendarList();
        list.Begin(1, 31);
        CalendarEntry kept = list.Add("good", 1, 0, CalendarGroup.Money, 3);
        list.Night("good", "rent", -1, 40f);
        CalendarEntry lost = list.Add("bad", 1, 0, CalendarGroup.Money, 4);
        CalendarEntry lost2 = list.Add("bad", 2, 0, CalendarGroup.Time, 5);
        list.Night("bad", "wages", -1, 25f);
        NightLine lostNight = list.Nights[1];
        list.Cut(1, 1);
        Check(list.Items.Count == 1 && list.Items[0] == kept && list.Nights.Count == 1 && list.Nights[0].Title == "rent",
            "calendar fix: a source that fails leaves none of its entries and none of its nightly lines, the others' kept");
        CalendarEntry again = list.Add("next", 3, 0, CalendarGroup.City, 6);
        list.Night("next", "loan", -1, 10f);
        Check((again == lost || again == lost2) && list.Nights[1] == lostNight, "calendar fix: what it left is given back to the pools and used again");
        list.Cut(10, 10);
        list.Cut(-1, -1);
        Check(list.Items.Count == 0 && list.Nights.Count == 0, "calendar fix: a cut past the end changes nothing, one from before the start takes all");

        string director = ModSource(@"Calendar\CalendarDirector.cs");
        string look = MemberText(director, "public void Look(int month)");
        Check(look.Contains("int before = List.Items.Count, nightsBefore = List.Nights.Count;") && look.Contains("List.Cut(before, nightsBefore);") && !look.Contains("RemoveRange("),
            "calendar fix: a failed source's look is cut back to where it began, lines and entries alike");

        // Every app's install asks whether the phone can take an app before it makes anything for one.
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        int installs = 0;
        var made = new System.Collections.Generic.List<string>();
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            if (path.EndsWith("PhoneApps.cs", StringComparison.Ordinal))
            {
                continue;
            }
            string code = File.ReadAllText(path);
            int at = code.IndexOf("PhoneApp.Install(", StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }
            installs++;
            int up = code.LastIndexOf("if (!PhoneApp.PhoneUp)", at, StringComparison.Ordinal);
            int method = 0;
            foreach (string head in new[] { "\n        private ", "\n        public ", "\n        internal ", "\n        partial " })
            {
                method = Math.Max(method, code.LastIndexOf(head, up < 0 ? at : up, StringComparison.Ordinal));
            }
            string between = up < 0 ? string.Empty : code.Substring(up, at - up);
            if (up < 0 || Regex.IsMatch(code.Substring(method, up - method), @"var view = new |GameSprites\.Want\(") || !between.Contains("return;"))
            {
                made.Add(System.IO.Path.GetFileName(path));
            }
        }
        string phone = ModSource(@"Core\GameUi\PhoneApps.cs");
        Check(installs >= 25 && made.Count == 0 && MemberText(phone, "public static PhoneApp Install(PhoneAppDef def, out bool tryAgain)").Contains("if (!PhoneUp)"),
            "calendar fix: all " + installs + " of the mod's apps make their page only once the game's phone can take it" + (made.Count > 0 ? " (not: " + string.Join(", ", made) + ")" : string.Empty));
    }
}
