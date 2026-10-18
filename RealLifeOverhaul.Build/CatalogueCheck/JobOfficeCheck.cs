using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Offline checks that the player is told where an employer's office is (Jobs/CareerOfficeRules and the sources that use it):
    /// every text that sends the player to the office has a twin that names the address, used when the employer has a board
    /// in this city, with the plain text kept for one that has none; "Show me the office" is offered only with a board; the
    /// city map's Job offices row lists every placed board; the texts exist in en, pt and zh-cn and the notifications stay
    /// within 240 characters with a long address; and the What's new line is in some reallife_changes_ row.
    /// </summary>
    internal static class JobOfficeCheck
    {
        private const int MostLength = 240;   // characters in a notification (NoticeCheck)
        private static readonly string[] Twins =
        {
            "reallife_career_review_due", "reallife_career_text_review_due", "reallife_career_note_review", "reallife_career_reminder",
            "reallife_career_text_reminder", "reallife_career_at_depot"
        };
        private static readonly string[] Notices = { "reallife_career_review_due", "reallife_career_reminder" };   // (the toasts; the rest are texts and dialogue)
        private static readonly string[] Plain =
        {
            "reallife_career_office_show", "reallife_career_office_page", "reallife_career_office_shown", "reallife_career_office_busy",
            "reallife_career_office_none", "reallife_map_row_job_offices"
        };

        public static void Run(Action<string> fail, string src)
        {
            Console.WriteLine("\n== Jobs: finding the office ==");
            int checks = 0;
            void Check(bool ok, string message)
            {
                checks++;
                if (!ok) fail("job office: " + message);
            }

            // ---- the rules
            const string address = "1234 Hudson Boulevard";
            Check(CareerOfficeRules.Key("reallife_career_reminder", address) == "reallife_career_reminder_at", "an employer with a board gets the twin that names the address");
            foreach (string none in new string[] { null, "", " ", "  \t" })
                Check(CareerOfficeRules.Key("reallife_career_reminder", none) == "reallife_career_reminder", $"'{none ?? "null"}' (no board) keeps the phone wording");
            Check(!CareerOfficeRules.Has(null) && CareerOfficeRules.Has(address), "Has: null is no address, a street is one");
            Check(CareerOfficeRules.ShowMeOffered(0, 1) && CareerOfficeRules.ShowMeOffered(5, 6), "Show me is offered for a placed board, first and last");
            Check(!CareerOfficeRules.ShowMeOffered(-1, 6) && !CareerOfficeRules.ShowMeOffered(6, 6) && !CareerOfficeRules.ShowMeOffered(0, 0), "Show me is not offered with no board (-1, past the end, no boards at all)");

            var rows = new List<int>();
            CareerOfficeRules.Offices(new[] { 2, -1, 0, 5, -1, 1 }, 6, rows);
            Check(rows.SequenceEqual(new[] { 0, 2, 3, 5 }), "the Job offices row lists every employer with a placed board, in order: " + string.Join(",", rows));
            CareerOfficeRules.Offices(new[] { -1, -1, -1 }, 4, rows);
            Check(rows.Count == 0, "a city with no board lists nothing (and clears an earlier list)");
            CareerOfficeRules.Offices(new[] { 3, 7, 0 }, 4, rows);
            Check(rows.SequenceEqual(new[] { 0, 2 }), "a board past the end is not listed");
            int[] real = new int[CareerCatalogue.All.Length];
            for (int i = 0; i < real.Length; i++) real[i] = i;
            CareerOfficeRules.Offices(real, real.Length, rows);
            Check(rows.Count == CareerCatalogue.All.Length, "every employer of the catalogue gets a row when all have boards");

            // ---- the texts
            string locales = Path.Combine(src, "Locales");
            var texts = new Dictionary<string, Dictionary<string, string>>();
            foreach (string l in new[] { "en", "pt", "zh-cn" })
                texts[l] = Read(Path.Combine(locales, l + ".json"));
            int longest = CareerCatalogue.All.Max(e => Math.Max(texts["en"].GetValueOrDefault(e.NameKey, "").Length, texts["en"].GetValueOrDefault(e.BossKey, "").Length));
            foreach (var language in texts)
            {
                Dictionary<string, string> t = language.Value;
                foreach (string key in Twins)
                {
                    bool has = t.TryGetValue(key, out string plain), twin = t.TryGetValue(key + "_at", out string at);
                    Check(has && twin, $"{language.Key}: {key} and {key}_at are both there");
                    if (!has || !twin) continue;
                    Check(at.Contains("{address}") && !plain.Contains("{address}"), $"{language.Key}: {key}_at names {{address}} and the plain text does not");
                    Check(Holes(plain).SetEquals(Holes(at).Where(h => h != "address")), $"{language.Key}: {key}_at has the same other placeholders as {key}");
                    Check(!at.Contains('!') && !at.Contains('—') && !at.Contains('–') && !at.Contains(" - "), $"{language.Key}: {key}_at has no exclamation mark or dash");
                }
                foreach (string key in Plain)
                    Check(t.TryGetValue(key, out string v) && v.Length > 0 && !v.Contains('!') && !v.Contains('—') && !v.Contains('–'), $"{language.Key}: {key} is there, with no exclamation mark or dash");
                foreach (string key in Notices)
                {
                    if (!t.TryGetValue(key + "_at", out string at)) continue;
                    string filled = Regex.Replace(at, @"\{(\w+)\}", m => m.Groups[1].Value == "address" ? "1234 Hudson Boulevard North" : new string('x', longest));
                    Check(language.Key != "en" || filled.Length <= MostLength, $"{key}_at is {filled.Length} characters with a long address, over {MostLength}");
                }
            }

            // ---- the sources
            string jobs = Path.Combine(src, "Scripts", "Jobs");
            string director = File.ReadAllText(Path.Combine(jobs, "CareerDirector.cs"));
            string phone = File.ReadAllText(Path.Combine(jobs, "CareerPhone.cs"));
            string jobDirector = File.ReadAllText(Path.Combine(jobs, "JobDirector.Tests.cs"));
            string links = File.ReadAllText(Path.Combine(src, "Scripts", "World", "MapLinks.cs"));
            foreach (string key in new[] { "reallife_career_review_due", "reallife_career_text_review_due", "reallife_career_reminder", "reallife_career_text_reminder" })
                Check(Regex.IsMatch(director, "OfficeKey\\(\"" + key + "\""), $"{key} is raised through OfficeKey, which fills the address");
            Check(Regex.IsMatch(jobDirector, "OfficeKey\\(\"reallife_career_review_due\""), "the options page's review test raises the notice through OfficeKey too");
            Check(director.Contains("CareerOfficeRules.Key(\"reallife_career_at_depot\"") && phone.Contains("address = career.OfficeAddress("), "the clock-on refusal names the address");
            Check(phone.Contains("CareerOfficeRules.Key(\"reallife_career_note_review\""), "the Work contact's review note names the address");
            Check(Regex.Matches(phone, "\"reallife_career_office_show\"").Count >= 3 && Regex.Matches(phone, "CareerOfficeRules\\.Has\\(").Count >= 4, "Show me the office is offered only behind CareerOfficeRules.Has (an address)");
            Check(director.Contains("_officeMark.Show(") && director.Contains("GuideMark"), "Show me puts the game's destination marker (GuideMark) on the board");
            int at0 = links.IndexOf("Id = \"job_offices\"", StringComparison.Ordinal);
            string row = at0 < 0 ? string.Empty : links.Substring(at0, Math.Min(links.Length - at0, 2200));
            Check(row.Contains("reallife_map_row_job_offices") && row.Contains("CareerOfficeRules.ShowMeOffered(") && row.Contains("CareerCatalogue.All.Length") && row.Contains("{ Lit = boards.Building(board) }"),
                "the map's Job offices row lists the board of every employer and lights its building");

            // ---- the guide and What's new
            foreach (var language in texts)
            {
                string guide = language.Value.GetValueOrDefault("reallife_guide_careers", "");
                string mark = language.Key == "en" ? "Show me the office" : language.Key == "pt" ? "Mostre o escritório" : "带我去办公室";
                string rowName = language.Key == "en" ? "Job offices" : language.Key == "pt" ? "Escritórios de emprego" : "工作办公室";
                Check(guide.Contains(mark) && guide.Contains(rowName), $"{language.Key}: the careers guide page says how to find the office (Show me and the map row)");
                Check(language.Value.GetValueOrDefault("reallife_career_office_show", "") == mark, $"{language.Key}: the guide names the button as it is worded");
                // any reallife_changes_ row: the line is in the next row until a release numbers it
                string line = language.Key == "en" ? "the office for your job's reviews and shifts was hard to find" : language.Key == "pt" ? "escritório das avaliações e dos turnos do seu emprego" : "很难找到工作的考核和班次所在的办公室";
                Check(language.Value.Any(r => r.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !r.Key.EndsWith("_title", StringComparison.Ordinal) && r.Value.Contains(line)),
                    $"{language.Key}: What's new has the fix's line in some reallife_changes_ row");
            }
            Console.WriteLine($"  {checks} checks on the office");
        }

        private static HashSet<string> Holes(string text)
        {
            return new HashSet<string>(Regex.Matches(text, @"\{(\w+)\}").Select(m => m.Groups[1].Value));
        }

        private static Dictionary<string, string> Read(string path)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
                foreach (JsonProperty p in doc.RootElement.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.String) map[p.Name] = p.Value.GetString();
            return map;
        }
    }
}
