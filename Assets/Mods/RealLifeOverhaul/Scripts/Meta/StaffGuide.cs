using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Localizor;
using RealLifeOverhaul.Staffing;
using UI.Notification;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The Guide's page "Staff and roles": what the reference is and where it can be searched, with every business and role listed
    /// from A to Z. The entries themselves are read in the Staff and roles app (<see cref="StaffView"/>) and the game's help.
    /// Built when the page opens; no cost while it is shut.
    /// </summary>
    public static class StaffGuide
    {
        private const string PointerKey = "reallife_staff_pointer";

        /// <summary>The Guide pages that end with a pointer to the reference: tech people and jobs pages (the tech sector page names the entries itself).</summary>
        public static bool Points(string featureId)
        {
            return featureId == "tech_people" || featureId == "tech_jobs";
        }

        public static string Pointer() => PointerKey.GetLocalization();

        /// <summary>
        /// Options > Tests: every entry written in the language shown (none with a name or a key left unfilled), found again by its own name by the
        /// search, and how many of them the game's help holds. Says the result in a notice.
        /// </summary>
        public static void Test()
        {
            StaffStyle style = StaffPages.GuideStyle(key => key.GetLocalization(), cents => Guide.Money(Prices.At((int)System.Math.Min(int.MaxValue / 4, cents / 100))), true);
            int entries = 0, bad = 0, inHelp = 0;
            string first = string.Empty;
            foreach (StaffEntry entry in StaffIndex.Entries)
            {
                entries++;
                string page = StaffPages.AsGuideText(StaffPages.Page(entry, style));
                string name = entry.NameKey.GetLocalization();
                bool ok = page.Length > 80 && page.IndexOf('{') < 0 && page.IndexOf("reallife_", System.StringComparison.Ordinal) < 0 && StaffIndex.Match(name, name) >= 0 && name != entry.NameKey;
                if (!ok)
                {
                    bad++;
                    first = first.Length == 0 ? name : first;
                }
                inHelp += LocalizorManager.IsLocalizedKey("help_" + entry.NameKey + "_content") ? 1 : 0;
            }
            var data = new Dictionary<string, string>
            {
                { "count", entries.ToString(CultureInfo.InvariantCulture) }, { "bad", bad.ToString(CultureInfo.InvariantCulture) }, { "first", first },
                { "help", inHelp.ToString(CultureInfo.InvariantCulture) }
            };
            Notifications.Show(bad == 0 ? NotificationType.Success : NotificationType.Warning, bad == 0 ? "reallife_staff_test_pass" : "reallife_staff_test_fail", data, 10f);
            if (Dev.Verbose)
            {
                MetaLog.Info("Meta check (staff reference): " + entries + " entries, " + bad + " unreadable, " + inHelp + " in the game's help: " + (bad == 0 ? "PASS" : "FAIL") + ".");
            }
        }

        /// <summary>The page: its text, then the businesses and the roles, each from A to Z.</summary>
        public static string Page()
        {
            var text = new StringBuilder(4096);
            text.Append("reallife_staff_guide".GetLocalization());
            for (int c = 0; c < 2; c++)
            {
                text.Append("\n# ").Append((c == 0 ? "reallife_staff_cat_places" : "reallife_staff_cat_roles").GetLocalization()).Append('\n');
                foreach (StaffEntry entry in StaffIndex.Sorted(c == 0, key => key.GetLocalization()))
                {
                    text.Append("- ").Append(entry.NameKey.GetLocalization()).Append('\n');
                }
            }
            return text.ToString();
        }
    }
}
