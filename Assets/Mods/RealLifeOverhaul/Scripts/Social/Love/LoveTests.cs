using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The love life's read-only probe and its test entries. The probe changes nothing and costs nothing until it is called: it says in plain
    /// words who the second person is, whether it is secret, what each suspects, who has found out, the nights, the children and the support.
    /// The test entries put a situation in place the way the feature's own rules would (a person made a friend or a partner, somebody found
    /// out, an evening's offer taken, a child on the way) and everything after goes through the real code.
    /// </summary>
    public static class LoveTests
    {
        /// <summary>The scandal's recognition for a test (Standing's measure, 0 to 100); -1 for the real one.</summary>
        internal static int Recognition = -1;

        /// <summary>
        /// For the self-test and the player's own checking: "seeing Jo secret yes suspicion 34/0 caught no nights 1 last warm baby none support 0 partner Sam".
        /// Plain words an <c>expect</c> can match; the person is named by first name, or empty for the whole.
        /// </summary>
        public static string Probe(string person)
        {
            if (!LoveState.Loaded)
            {
                return "love not loaded";
            }
            LoveFile f = LoveState.File;
            CultureInfo inv = CultureInfo.InvariantCulture;
            SocialPerson second = LoveState.SecondPerson, first = LoveState.FirstPerson, w = SocialState.Get(f.ConfrontWho);
            var text = new StringBuilder(160);
            text.Append("seeing ").Append(second != null ? SocialLife.First(second) : "nobody").Append(" secret ").Append(second != null && f.Knows[0] == 0 && f.Knows[1] == 0 ? "yes" : "no")
                .Append(" suspicion ").Append(f.Suspicion[0].ToString(inv)).Append('/').Append(f.Suspicion[1].ToString(inv))
                .Append(" caught ").Append(w != null ? SocialLife.First(w) : "no")
                .Append(" forgiven ").Append(f.Forgiven.ToString(inv)).Append(" denied ").Append(f.DeniedDay[0] > 0 || f.DeniedDay[1] > 0 ? "yes" : "no")
                .Append(" nights ").Append(f.Nights.ToString(inv));
            LoveHook h = f.Hooks.Count > 0 ? f.Hooks[f.Hooks.Count - 1] : null;
            text.Append(" last ").Append(h != null ? h.Outcome.ToString().ToLowerInvariant() : "none");
            LoveBaby b = f.Babies.Count > 0 ? f.Babies[f.Babies.Count - 1] : null;
            text.Append(" baby ").Append(b == null ? "none" : !b.Told ? "coming" : b.Born ? "born" : b.Choice == LoveChoice.None ? "asked" : b.Choice.ToString().ToLowerInvariant())
                .Append(" support ").Append(b != null ? b.Support.ToString(inv) : "0")
                .Append(" partner ").Append(first != null ? SocialLife.First(first) : "nobody");
            if (!string.IsNullOrEmpty(person))
            {
                SocialPerson p = Find(person);
                text.Append(" ").Append(person).Append(p == null ? " unknown" : " stage " + p.Stage.ToString().ToLowerInvariant() + " affection " + ((int)p.Affection).ToString(inv)
                                                           + " trust " + ((int)p.Trust).ToString(inv) + " memories " + p.Memories.Count.ToString(inv));
            }
            return text.ToString();
        }

        /// <summary>The first name of somebody of the cast, by slot or by name.</summary>
        public static string NameOf(string slot)
        {
            SocialPerson p = Find(slot);
            return p != null ? SocialLife.First(p) : "unknown";
        }

        private static SocialPerson Find(string key)
        {
            if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
            {
                return SocialState.Get(index);
            }
            for (int i = 0; i < SocialState.Count; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p != null && string.Equals(SocialLife.First(p), key, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }
            return null;
        }

        /// <summary>
        /// Makes somebody of the cast what a scene needs, by the social life's own calls: "friend" (met, a friend, open to a romance, of the
        /// player's age), "dating" (that, asked and said yes), "second" (that, asked while the player is with somebody, through the same code).
        /// </summary>
        public static string Prepare(string who, string what)
        {
            SocialPerson p = Find(who);
            if (p == null || !SocialState.Loaded)
            {
                return "nobody";
            }
            if (!p.Met)
            {
                SocialLife.Meet(p, true);
            }
            int player = SocialLife.PlayerYears();
            p.Compatible = true;
            p.Age = player > 0 ? player : 28;
            p.AgeDay = SocialLife.Day;
            p.Familiarity = 70f;
            p.Affection = 75f;
            p.Trust = 65f;
            p.Shared = Math.Max(p.Shared, 5);
            p.MetDay = Math.Min(p.MetDay, SocialLife.Day - 12);
            p.Stage = SocialStage.Friend;
            p.Drifted = false;
            if (what == "dating" || what == "second")
            {
                if (what == "second" && LoveHooks.Taken)
                {
                    LoveHooks.BeginSecond(p);
                }
                p.StageDay = SocialLife.Day - 20;
                SocialLife.SetStage(p, SocialStage.Dating);
                p.StageDay = SocialLife.Day - 20;
            }
            return SocialLife.First(p) + " " + p.Stage.ToString().ToLowerInvariant();
        }

        /// <summary>Somebody finds out, by the real code: the text, the notice and the wait to be answered.</summary>
        public static string Catch(string watcher, string with)
        {
            SocialPerson w = Find(watcher), o = Find(with);
            if (w == null || o == null || !LoveState.Loaded)
            {
                return "nobody";
            }
            LoveLife.Catch(w, o, CatchHow.Seen);
            return Probe(string.Empty);
        }

        /// <summary>
        /// The evening's offer, taken at their place, by the real code: the screen goes dark, the night is settled, the
        /// morning told. <paramref name="outcome"/> names an outcome to settle on, or empty for the roll.
        /// </summary>
        public static string Night(string who, string outcome)
        {
            SocialPerson p = Find(who);
            if (p == null || SocialDirector.Instance == null)
            {
                return "nobody";
            }
            LoveNight.ForcedOutcome = Enum.TryParse(outcome, true, out NightOutcome forced) ? (int)forced : -1;
            var offer = new LoveOffer { Person = p, Kind = NightKind.Drinks, Facts = LoveNight.FactsOf(p, NightKind.Drinks, true, 2f), Chosen = 1 };
            SocialDirector.Instance.BeginNight(offer);
            return "night begins with " + SocialLife.First(p);
        }

        /// <summary>A night that brought a child, by the real code: the news is due today. Say who carries (0 them, 1 the player).</summary>
        public static string Baby(string who, string carrier)
        {
            SocialPerson p = Find(who);
            if (p == null || !LoveState.Loaded)
            {
                return "nobody";
            }
            LoveFile f = LoveState.File;
            int day = SocialLife.Day;
            int yearDays = Family.ChildYearRules.Days(Settings.FamilyYearWeeks);
            f.Babies.Add(new LoveBaby
            {
                Person = p.Index, Day = day - 6, NewsDay = day, DueDay = day + Family.ChildYearRules.Span(0.5f, yearDays), Carrier = carrier == "1" ? 1 : 0, Girl = true
            });
            LoveBabies.Daily(day);
            return Probe(string.Empty);
        }

        /// <summary>A scandal, by the real code, at a given recognition (0 to 100); empty for the real one.</summary>
        public static string Scandal(string recognition, string child)
        {
            Recognition = int.TryParse(recognition, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : -1;
            LoveState.File.LastScandalDay = 0;
            LoveLife.Scandal(child == "1", true);
            Recognition = -1;
            return "scandal at " + recognition + " last " + LoveState.File.LastScandalDay;
        }

        /// <summary>The player's partner learns, now: the confrontation by the real code, the way the daily roll or a post would.</summary>
        public static string Answer(string watcher, string answer)
        {
            SocialPerson w = Find(watcher);
            if (w == null || !LoveState.Loaded || !Enum.TryParse(answer, true, out LoveAnswer a))
            {
                return "nobody";
            }
            LoveReport r = LoveLife.Resolve(w, a);
            return (r.Held ? "held" : r.Left ? "leaves" : "stays") + " " + r.Line;
        }
    }
}
