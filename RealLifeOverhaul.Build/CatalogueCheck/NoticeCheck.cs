using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Offline checks of the mod's notifications (Core/GameUi/NoticeKinds.cs, as it ships): which kind, and so which
    /// picture, a key is drawn with; that the game's own keys are never taken for the mod's; that the table is read in
    /// the right order; and that every notification the sources raise by a key of their own has a kind and a text short
    /// enough for the game's toast. The keys are read from the sources as text (each call whose first argument is a
    /// NotificationType, its second argument's literals), the texts from en.json.
    /// </summary>
    internal static class NoticeCheck
    {
        private const int MostLength = 240;          // characters: a toast is one or two sentences and fits in about 240 (Workshop/STYLE.md)
        private const double MostMicroseconds = 5.0; // a key's kind, at most, on average (it is asked once per toast and per row shown)

        public static void Run(Action<string> fail, Dictionary<string, string> locale, string src)
        {
            Console.WriteLine("\n== Notifications: kinds and texts ==");
            int checks = 0;
            void Check(bool ok, string message)
            {
                checks++;
                if (!ok) fail("notices: " + message);
            }

            // The game's own keys, and anything that is not the mod's, are left alone.
            foreach (string key in new[] { null, "", "reallife_", "notification_destination_set", "notificationui_notification_insufficient_money", "Reallife_car_low_fuel", "reallifecar_low_fuel", "xreallife_car_low_fuel" })
                Check(NoticeKinds.Of(key) == NoticeKind.None, $"'{key ?? "null"}' is taken for the mod's");

            // One of each, and the edges of the table: a prefix stops at its underscore, an exception wins over its section.
            var cases = new (string Key, NoticeKind Kind)[]
            {
                ("reallife_accident_closed", NoticeKind.Road), ("reallife_tow_coming", NoticeKind.Road), ("reallife_car_breakdown", NoticeKind.Road),
                ("reallife_careers_note", NoticeKind.Work), ("reallife_career_signed", NoticeKind.Work), ("reallife_car", NoticeKind.Other),
                ("reallife_crime_stop_fleeing", NoticeKind.Police), ("reallife_law_wanted", NoticeKind.Police), ("reallife_justice_toast_warrant", NoticeKind.Police),
                ("reallife_biz_racket_start", NoticeKind.Police), ("reallife_biz_visit_police", NoticeKind.Police), ("reallife_biz_poach_kept", NoticeKind.Business),
                ("reallife_health_caught", NoticeKind.Health), ("reallife_event_ambulance", NoticeKind.Health), ("reallife_event_film_cut", NoticeKind.Other),
                ("reallife_family_checkup_due", NoticeKind.Health), ("reallife_family_birth_time", NoticeKind.People), ("reallife_pd_ill_1", NoticeKind.Health),
                ("reallife_econ_note_lent", NoticeKind.Money), ("reallife_util_toast_bills", NoticeKind.Money), ("reallife_net_toast_outage", NoticeKind.Money),
                ("reallife_estate_note_pipe", NoticeKind.Property), ("reallife_roommate_rent_paid", NoticeKind.Property), ("reallife_home_rent_due", NoticeKind.Property),
                ("reallife_home_coffee_done", NoticeKind.Other), ("reallife_co_note_offer", NoticeKind.Business), ("reallife_rota_changed", NoticeKind.Business),
                ("reallife_tech_note_breach", NoticeKind.Tech), ("reallife_bb_toast_open", NoticeKind.Tech), ("reallife_hire_note_started", NoticeKind.Tech),
                ("reallife_ledger_note_delivered", NoticeKind.News), ("reallife_civic_polls_open", NoticeKind.News), ("reallife_macro_note_rate_up", NoticeKind.News),
                ("reallife_social_met", NoticeKind.People), ("reallife_travel_burnout_doctor", NoticeKind.Health), ("reallife_travel_welcome_back", NoticeKind.Other),
                ("reallife_planner_reminder", NoticeKind.Other), ("reallife_gp_city_left", NoticeKind.Other)
            };
            foreach (var c in cases)
                Check(NoticeKinds.Of(c.Key) == c.Kind, $"{c.Key} is {NoticeKinds.Of(c.Key)}, wanted {c.Kind}");

            // The options page's test: one key of each kind, each with its text, each drawn as the kind it names.
            var tested = new HashSet<NoticeKind>();
            foreach (NoticeKind kind in Enum.GetValues(typeof(NoticeKind)))
            {
                if (kind == NoticeKind.None) continue;
                string key = "reallife_notice_test_" + kind.ToString().ToLowerInvariant();
                Check(locale.ContainsKey(key), $"{key} has no text in en.json");
                Check(NoticeKinds.Of(key) == kind, $"{key} is drawn as {NoticeKinds.Of(key)}");
                tested.Add(NoticeKinds.Of(key));
            }
            Check(tested.Count == NoticeKinds.Count - 1, $"the test shows {tested.Count} kinds of {NoticeKinds.Count - 1}");
            string look = File.ReadAllText(Path.Combine(src, "Scripts", "Core", "GameUi", "NoticeLook.cs"));
            foreach (NoticeKind kind in Enum.GetValues(typeof(NoticeKind)))
                if (kind != NoticeKind.None)
                    Check(look.Contains("\"reallife_notice_test_" + kind.ToString().ToLowerInvariant() + "\""), $"the test does not show {kind}");

            // The table: no prefix twice, none hidden behind an earlier one that begins it, each ending where a word does.
            string[] table = NoticeKinds.Table();
            Check(table.Length == table.Distinct(StringComparer.Ordinal).Count(), "a prefix stands in the table twice");
            for (int i = 0; i < table.Length; i++)
            {
                Check(Regex.IsMatch(table[i], "^[a-z0-9_]+$"), $"prefix '{table[i]}' has a character no key has");
                for (int j = i + 1; j < table.Length; j++)
                    Check(!table[j].StartsWith(table[i], StringComparison.Ordinal), $"'{table[j]}' can never match: '{table[i]}' comes first");
            }

            // Every notification the sources raise by a literal key of the mod's: a kind, a text, and short enough.
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            var call = new Regex(@"\b\w+\s*\(\s*(?:UI\.Notification\.)?NotificationType\.\w+\s*,\s*");
            var literal = new Regex("\"(reallife_[a-z0-9_]+)\"");
            foreach (string file in Directory.GetFiles(Path.Combine(src, "Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                foreach (Match m in call.Matches(text))
                {
                    string argument = SecondArgument(text, m.Index + m.Length);
                    foreach (Match k in literal.Matches(argument))
                        keys[k.Groups[1].Value] = Path.GetFileName(file);
                }
            }
            Check(keys.Count >= 800, $"only {keys.Count} notification keys were found in the sources (the scan is broken)");
            var byKind = new int[NoticeKinds.Count];
            int longest = 0;
            string longestKey = null;
            foreach (var pair in keys)
            {
                NoticeKind kind = NoticeKinds.Of(pair.Key);
                byKind[(int)kind]++;
                Check(kind != NoticeKind.None, $"{pair.Key} ({pair.Value}) is not taken for the mod's");
                if (!locale.TryGetValue(pair.Key, out string words))
                {
                    fail($"notices: {pair.Key} ({pair.Value}) has no text in en.json");
                    continue;
                }
                checks++;
                if (words.Length > longest) { longest = words.Length; longestKey = pair.Key; }
                Check(words.Length <= MostLength, $"{pair.Key} is {words.Length} characters, over the {MostLength} a toast holds");
            }
            int other = byKind[(int)NoticeKind.Other];
            Check(other * 2 < keys.Count, $"{other} of {keys.Count} notifications fall under 'anything else'");

            // Determinism and cost.
            string[] sample = keys.Keys.ToArray();
            foreach (string key in sample) Check(NoticeKinds.Of(key) == NoticeKinds.Of(new string(key.ToCharArray())), $"{key} is not drawn the same way twice");
            int rounds = Math.Max(1, 200000 / Math.Max(1, sample.Length));
            int sink = 0;
            var watch = Stopwatch.StartNew();
            for (int r = 0; r < rounds; r++)
                for (int i = 0; i < sample.Length; i++)
                    sink += (int)NoticeKinds.Of(sample[i]);
            watch.Stop();
            double each = watch.Elapsed.TotalMilliseconds * 1000.0 / (rounds * (double)sample.Length);
            Check(each <= MostMicroseconds, $"a key's kind takes {each:F2} microseconds on average, over {MostMicroseconds}");

            Console.WriteLine($"  {keys.Count} notification keys in the sources; longest text {longest} characters ({longestKey}); a kind in {each:F2} microseconds (sum {sink % 10})");
            Console.WriteLine("  " + string.Join(", ", Enum.GetValues(typeof(NoticeKind)).Cast<NoticeKind>().Where(k => k != NoticeKind.None).Select(k => k + " " + byKind[(int)k])));
            Console.WriteLine($"  {checks} checks");
        }

        // The text of a call's argument that starts at <paramref name="from"/>, up to the comma or bracket that ends it.
        private static string SecondArgument(string text, int from)
        {
            int depth = 0;
            int i = from;
            for (; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"')
                {
                    for (i++; i < text.Length && text[i] != '"'; i++)
                        if (text[i] == '\\') i++;
                    continue;
                }
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (depth == 0) break;
                    depth--;
                }
                else if (c == ',' && depth == 0) break;
            }
            return text.Substring(from, i - from);
        }
    }
}
