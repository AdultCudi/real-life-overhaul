using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// RLO Texts' entries in Options > Tests: a sample message written now, with what it took or why the written line would
    /// be used; what was shown this session (the readout, the full count in Player.log); and, with a pack of data 6, a
    /// message from the player's staff, a text after a life event, and gossip, each made now rather than waited for.
    /// </summary>
    public sealed partial class TextsDirector
    {
        /// <summary>
        /// A friend's small talk written by the model now, shown as a notice when it comes back (or why not: no pack, still
        /// loading, switched off, another language, the words refused). Needs a save loaded.
        /// </summary>
        public static void TestSample()
        {
            if (Instance == null || SaveGameManager.Current == null)
            {
                TextsGame.Notice("reallife_texts_test_no_save", null);
                return;
            }
            string key;
            Dictionary<string, string> status = Status(out key);
            if (key != "reallife_texts_status_ready" || !LanguageOk())
            {
                TextsGame.Notice(LanguageOk() ? key : Hub.PackData >= TextIntents.DataV6 ? "reallife_texts_test_language_pack" : "reallife_texts_test_language",
                    LanguageOk() ? status : Langs(), 16f);
                return;
            }
            TextRequest r = SampleRequest();   // (with Your own AI up, it goes the way every message goes: the service first)
            double asked = TextClock.Now;
            r.Done = words => TextsGame.Notice(words != null ? "reallife_texts_test_sample_done" : "reallife_texts_test_sample_none",
                new Dictionary<string, string>(2) { { "text", words ?? string.Empty }, { "seconds", (TextClock.Now - asked).ToString("0.0") } }, 16f);
            if (!Hub.Ask(r, asked))
            {
                TextsGame.Notice("reallife_texts_test_sample_none", new Dictionary<string, string>(2) { { "text", string.Empty }, { "seconds", "0" } }, 12f);
            }
        }

        // The running pack and the languages it writes, for the notice that says the game's is not one of them.
        private static Dictionary<string, string> Langs()
        {
            return new Dictionary<string, string>(2) { { "pack", _pack != null ? _pack.Name : TextsHub.NoPack }, { "langs", string.Join(", ", Hub.Languages) } };
        }

        // ---------------------------------------------------------------- what was shown

        private static readonly string[] WhyKeys =
        {
            null, "reallife_texts_why_nopack", "reallife_texts_why_off", "reallife_texts_why_loading", "reallife_texts_why_busy", "reallife_texts_why_slow",
            "reallife_texts_why_refused", "reallife_texts_why_nocard", "reallife_texts_why_language", "reallife_texts_why_unwanted", "reallife_texts_why_failed"
        };

        private static readonly string[] RuleKeys =
        {
            null, "reallife_texts_rule_empty", "reallife_texts_rule_lines", "reallife_texts_rule_long", "reallife_texts_rule_must", "reallife_texts_rule_number",
            "reallife_texts_rule_dash", "reallife_texts_rule_exclaim", "reallife_texts_rule_banned", "reallife_texts_rule_language", "reallife_texts_rule_repeat",
            "reallife_texts_rule_machine", "reallife_texts_rule_capital", "reallife_texts_rule_article", "reallife_texts_rule_name", "reallife_texts_rule_cut"
        };

        /// <summary>
        /// "RLO Texts: what was shown": the running pack's lines this session as a notice (by the model, written, the two
        /// commonest reasons and, for a refusal, the commonest rule), and every pack's count by message in Player.log.
        /// </summary>
        public static void TestShown()
        {
            string pack = Hub.Pack;
            var why = new int[WhyKeys.Length];
            var rule = new int[RuleKeys.Length];
            int lines = Hub.Tally.Totals(pack, why, rule);
            if (lines == 0)
            {
                List<string> packs = Hub.Tally.Packs();
                if (packs.Count == 0)
                {
                    TextsGame.Notice("reallife_texts_shown_none", null, 12f);
                    return;
                }
                pack = packs[packs.Count - 1];
                lines = Hub.Tally.Totals(pack, why, rule);
            }
            string line = Hub.Tally.Line(null);
            if (line != null)
            {
                Logger?.Info(line);
            }
            TextsGame.Notice("reallife_texts_shown_line", new Dictionary<string, string>(4)
            {
                { "pack", pack }, { "model", why[0].ToString(CultureInfo.InvariantCulture) }, { "written", (lines - why[0]).ToString(CultureInfo.InvariantCulture) },
                { "why", Reasons(why, rule) }
            }, 20f);
        }

        // The two commonest reasons, each "{n} too slow", and the commonest rule of a refusal; empty with none.
        private static string Reasons(int[] why, int[] rule)
        {
            var b = new StringBuilder(96);
            int first = 0;
            for (int pick = 0; pick < 2; pick++)
            {
                int best = 0;
                for (int k = 1; k < why.Length; k++)
                {
                    if (why[k] > 0 && k != first && (best == 0 || why[k] > why[best]))
                    {
                        best = k;
                    }
                }
                if (best == 0)
                {
                    break;
                }
                first = best;
                string part = TextsText.T(WhyKeys[best]).Replace("{n}", why[best].ToString(CultureInfo.InvariantCulture));
                if (best == (int)TextShownWhy.Refused)
                {
                    int top = 0;
                    for (int v = 1; v < rule.Length; v++)
                    {
                        top = rule[v] > 0 && (top == 0 || rule[v] > rule[top]) ? v : top;
                    }
                    if (top > 0)
                    {
                        part += " (" + TextsText.T(RuleKeys[top]) + ")";
                    }
                }
                b.Append(b.Length > 0 ? ", " : string.Empty).Append(part);
            }
            return b.Length > 0 ? TextsText.T("reallife_texts_shown_why").Replace("{reasons}", b.ToString()) + " " : string.Empty;
        }

        // ---------------------------------------------------------------- the city's new talk, now

        // Whether the city's talk can be tried now: a save, a pack of data 6, the option; else the notice that says why not.
        private static bool CityReady()
        {
            if (Instance == null || SaveGameManager.Current == null)
            {
                TextsGame.Notice("reallife_texts_test_no_save", null);
                return false;
            }
            if (!Six)
            {
                TextsGame.Notice("reallife_texts_test_six", new Dictionary<string, string>(1) { { "pack", _pack != null ? _pack.Name + " (data v" + _pack.Data + ")" : TextsHub.NoPack } }, 14f);
                return false;
            }
            if (!Settings.TextsCity)
            {
                TextsGame.Notice("reallife_texts_test_city_off", null, 12f);
                return false;
            }
            return true;
        }

        /// <summary>A message from the first of the player's staff now (a raise asked for, or off sick before the working day), through the game's own thread with them.</summary>
        public static void TestStaff()
        {
            if (!CityReady())
            {
                return;
            }
            ReadStaff();   // (with the game's own facts of each: their job, hours, a day off sick)
            if (Views.Staff.Count == 0)
            {
                TextsGame.Notice("reallife_texts_test_staff_none", null, 12f);
                return;
            }
            TextStaff s = Views.Staff[0];
            if (s.AskTo <= s.Paid)
            {
                s.AskTo = s.Paid + 150;   // (the test's own ask: the game's pay and a dollar and a half)
            }
            StaffMessage(s, SaveGameManager.Current.Hour < TextsCityRules.StaffFrom ? TextStaffSay.Sick : TextStaffSay.RaiseAsk, TextsGame.Day);
            TextsGame.Notice("reallife_texts_test_staff_sent", new Dictionary<string, string>(1) { { "name", s.Name } }, 12f);
        }

        /// <summary>A text after a life event now: the player's graduation, told by Mom or a friend as on the day.</summary>
        public static void TestLife()
        {
            if (!CityReady())
            {
                return;
            }
            Views.People.Clear();
            TextsBridges.People?.Invoke(Views.People);
            var e = new TextLifeEvent { Kind = "graduated", Day = TextsGame.Day };
            if (!TextsCityRules.Sender(e, Views.People, TextsGame.Day, out TextLifeSender sender) || TextsBridges.PersonSay == null
                || !TextsBridges.PersonSay(sender.Index, sender.Key, LifeData(e, null)))
            {
                TextsGame.Notice("reallife_texts_test_life_none", null, 12f);
                return;
            }
            TextsGame.Notice("reallife_texts_test_life_sent", null, 12f);
        }

        /// <summary>Gossip now: the newest public event of the player's (or a crash yesterday, for the test) passed on by somebody who knows who heard it.</summary>
        public static void TestGossip()
        {
            if (!CityReady())
            {
                return;
            }
            Views.People.Clear();
            TextsBridges.People?.Invoke(Views.People);
            int day = TextsGame.Day;
            var e = new TextLifeEvent { Kind = "crash", Day = day - 1 };
            for (int i = CityStore.Events.Count - 1; i >= 0; i--)
            {
                if (TextsCityRules.Public(CityStore.Events[i].Kind) && !CityStore.Events[i].Gossiped)
                {
                    e = new TextLifeEvent { Kind = CityStore.Events[i].Kind, Day = day - 1, Value = CityStore.Events[i].Value, Texted = CityStore.Events[i].Texted };
                    break;
                }
            }
            if (!TextsCityRules.Gossip(e, Views.People, day, TextsCityRules.GossipHour(e), out int teller, out int hearer))
            {
                TextsGame.Notice("reallife_texts_test_gossip_none", null, 12f);
                return;
            }
            string first = null;
            for (int k = 0; k < Views.People.Count; k++)
            {
                first = Views.People[k].Index == teller ? Views.People[k].First : first;
            }
            bool sent = first != null && TextsBridges.PersonSay != null
                        && TextsBridges.PersonSay(hearer, TextsCityRules.GossipKey(e.Kind, TextsCityRules.Hash(e.Day, e.Kind, hearer)), LifeData(e, first));
            TextsGame.Notice(sent ? "reallife_texts_test_gossip_sent" : "reallife_texts_test_gossip_none", null, 12f);
        }

        /// <summary>Everything remembered and every life event let go (the save tools' reset of the feature's data).</summary>
        public static void ForgetAll()
        {
            MemoryStore.Clear();
            CityStore.Clear();
            _cityChanged = true;
        }
    }
}
