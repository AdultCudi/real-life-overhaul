using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What the game answers an invitation with: the decision is the game's; the words only say it.</summary>
    public enum FlareInviteAnswer { Yes, Maybe, No }

    /// <summary>Why: each reason has its own written line and its own fact on the card.</summary>
    public enum FlareInviteWhy
    {
        Yes,          // booked: the place and the time
        Plan,         // a plan with them is on already, or their yes is on its way
        Busy,         // the player has a plan with somebody else, no hour suits within two days, or outings are switched off
        Night,        // their night hours when the message came
        Stranger,     // somebody the player has never met and has hardly talked to
        Later,        // the outing needs them closer than they are
        Nowhere,      // no place for it near the player
        Mood,         // a rough day
        Taken,        // in a relationship with somebody else
        Dislikes,     // they do not think well of the player
        Pass,         // a plain no
        Influencer    // an influencer: they meet people through friends, never from a message
    }

    /// <summary>An invitation in a Flare message as the game reads and decides it, filled by Wiring/FlareInviteLinks; never kept.</summary>
    public sealed class FlareInviteAsk
    {
        public PlanKind Kind;            // the outing read (FlareInviteRules.Read)
        public bool Influencer;          // "i:3"
        public string First, Last;       // a city person's name (the stranger as the social life would meet them)
        public int Sex = -1;             // 1 a woman, 0 a man, -1 not known (FlareInviteRules.SexOf)
        public int Age;                  // as their card and face have it (FlareLiveRules.CitizenAge)
        public int Face;                 // a city person's face's seed (FlareAvatarRules.Citizen), kept when met; 0 not known
        public int Hour;                 // the message's
        public bool NightOwl, Grumpy, Taken;   // a city person's persona and their own life (FlareInviteRules.Citizen)
        public int Chats;                // their messages to the player shown so far (earlier answers, a message they started)
        public int Standing;             // the player's public standing, 0 to 100 (the law's)
        public int Reputation;           // the account's reputation on Flare, 0 to 100
        public long From;                // game minutes: the earliest a plan can start (the yes's arrival and the margin after it)
        public int Roll;                 // 0 to 999, the save's seed and the message's minute

        // What the social life knows (Wiring/FlareInviteLinks).
        public bool Off;                 // the social life or its outings are switched off: nothing can be booked
        public bool Known;               // somebody the player has met (a city person who said yes once is in People)
        public int Cast = -1;            // ...their place in the cast; for a stranger, who they would be when met
        public bool NobodyNew;           // a stranger, and nobody of the social life is left to meet
        public bool PlanWithThem;        // a plan with them is on
        public bool PlanOther;           // the player has a plan with somebody else (one at a time)
        public bool Asleep;              // a person of the cast: their own night hours at the message's hour
        public bool LowMood;             // a rough day: half the time they say no, as on the phone
        public bool Dislikes;            // they remember being stood up, a rude word or money not repaid
        public bool NeedsMore;           // the outing needs them closer (OutingDef.Needs; a workout with somebody not sporty below friend)
        public bool NoPlace, NoSlot;     // no place near the player; no hour within two days

        // A yes: the place and the time, and the plan on now for the answer that points to it.
        public string Place;             // the business's name as the game shows it
        public string Street;            // the game's own street name and number, for the booking
        public int Number;
        public long PlanAt;              // game minutes
        public string When;              // the day and time in the game's language ("Monday 18:00")
        public string CardWhen;          // ...and as a card says it
        public string Outing;            // the outing in the game's language ("coffee", "a meal")
        public PlanKind PlanKind;        // the plan on now: its kind (Plan)
    }

    /// <summary>A yes's plan as its message keeps it, read back when it is due to be booked (FlareInviteRules.BookOf).</summary>
    public sealed class FlareInviteBook
    {
        public PlanKind Kind;
        public long At;                  // game minutes
        public string Street;            // the place: the game's own street name and number, and its name as shown
        public int Number;
        public string Place;
        public int Cast = -1;            // a person of the social life; -1 somebody new, met online now
        public string First, Last;       // ...their name, sex and age
        public int Sex = -1;
        public int Age;
        public int Face;                 // ...and their face's seed; 0 for an older save's yes (the seat's own)
    }

    /// <summary>
    /// Invitations in the player's Flare messages, read by fixed rules and decided by the social life's, rolled from the
    /// save's seed and the message's minute so a reload answers the same; the model only words the answer. A yes becomes a
    /// plan at least <see cref="PlanAfterAnswer"/> minutes on. Game-free. Cost: a pass over each message sent.
    /// </summary>
    public static partial class FlareInviteRules
    {
        public const int ChatsForYes = 3;          // messages of theirs before somebody new meets the player
        public const int StandingForYes = 70;      // ...or a public standing this high (the law's "liked", Record.LikedFrom)
        public const int DislikeBelow = 30;        // a standing or a Flare reputation below this: they do not think well of the player
        public const int StrangerYes = 750;        // in thousandths: somebody new who may say yes does, three times in four
        public const int GrumpyYes = 400;          // ...a grumpy one, two in five
        public const int StrangerNo = 200;         // in thousandths: somebody new who may not say yes says no rather than maybe
        public const int MoodNo = 500;             // in thousandths: on a rough day, half the time no (the phone's own odds)
        public const int TakenPercent = 25;        // in hundredths: the city's people in a relationship with somebody else
        public const int AnswerLeast = 30;         // game minutes: an answer comes within half an hour to two hours
        public const int AnswerSpread = 90;   // game minutes
        public const int YesLeast = 10;            // game minutes: a yes comes sooner, within ten to thirty
        public const int YesSpread = 20;   // game minutes
        public const int PlanAfterAnswer = 120;    // game minutes: the plan starts at least this long after the yes arrives
        public const int PanelBefore = 60;         // game minutes: the plan's reminder and job panel line show this long before (SocialRules.EarlyMinutes)
        public const int CheckBefore = 90;         // game minutes: whether they call it off is rolled this long before (SocialDirector.Plan)
        public const string KeyStart = "reallife_flare_dm_invite_";

        private static readonly string[] KindWords = { "coffee", "meal", "drinks", "cinema", "walk", "workout" };   // by PlanKind, Coffee to Workout; Drive is never read

        /// <summary>A city person's sex as an invitation meets them, as the city made them: 1 a woman, 0 a man.</summary>
        public static int SexOf(FlareCitizen c) => c.Female ? 1 : 0;

        /// <summary>Whether a city person is in a relationship with somebody else: a quarter of them, stable for the city's seed.</summary>
        public static bool Taken(int citySeed, int index) => FlareRules.Roll(citySeed, index, 1203) % 100 < TakenPercent;

        /// <summary>A city person's night hours: 23 to 8, a night owl's 2 to 10, as a person of the social life's.</summary>
        public static bool Asleep(int hour, bool nightOwl) => nightOwl ? hour >= 2 && hour < 10 : hour >= 23 || hour < 8;

        /// <summary>The roll of an invitation: the save's seed and the message's minute.</summary>
        public static int Roll(int seed, long sent) => FlareRules.Roll(seed, (int)(sent % int.MaxValue), 1201) % 1000;

        /// <summary>Yes, maybe or no, and why, by the rules in the summary.</summary>
        public static FlareInviteAnswer Decide(FlareInviteAsk a, out FlareInviteWhy why)
        {
            bool stranger = !a.Known && !a.Influencer;
            if (a.Dislikes || (!a.Known && (a.Standing < DislikeBelow || a.Reputation < DislikeBelow)))
            {
                why = FlareInviteWhy.Dislikes;
                return FlareInviteAnswer.No;
            }
            if (stranger && a.Taken)
            {
                why = FlareInviteWhy.Taken;
                return FlareInviteAnswer.No;
            }
            if (a.PlanWithThem)
            {
                why = FlareInviteWhy.Plan;
                return FlareInviteAnswer.Maybe;
            }
            if (a.Influencer)
            {
                why = FlareInviteWhy.Influencer;
                return FlareInviteAnswer.Maybe;
            }
            if (a.Known ? a.Asleep : Asleep(a.Hour, a.NightOwl))
            {
                why = FlareInviteWhy.Night;
                return FlareInviteAnswer.Maybe;
            }
            if (a.Off || a.PlanOther)
            {
                why = FlareInviteWhy.Busy;
                return FlareInviteAnswer.Maybe;
            }
            if (a.Known && a.LowMood && a.Roll < MoodNo)
            {
                why = FlareInviteWhy.Mood;
                return FlareInviteAnswer.No;
            }
            if (stranger)
            {
                if (!MayMeet(a))
                {
                    why = a.Grumpy || a.Roll < StrangerNo ? FlareInviteWhy.Pass : FlareInviteWhy.Stranger;
                    return why == FlareInviteWhy.Pass ? FlareInviteAnswer.No : FlareInviteAnswer.Maybe;
                }
                if (a.NobodyNew || a.Roll >= (a.Grumpy ? GrumpyYes : StrangerYes))
                {
                    why = FlareInviteWhy.Stranger;
                    return FlareInviteAnswer.Maybe;
                }
            }
            if (a.NeedsMore)
            {
                why = FlareInviteWhy.Later;
                return FlareInviteAnswer.Maybe;
            }
            if (a.NoPlace || string.IsNullOrEmpty(a.Place) || string.IsNullOrEmpty(a.Street))
            {
                why = FlareInviteWhy.Nowhere;
                return FlareInviteAnswer.Maybe;
            }
            if (a.NoSlot || a.PlanAt < a.From)
            {
                why = FlareInviteWhy.Busy;
                return FlareInviteAnswer.Maybe;
            }
            why = FlareInviteWhy.Yes;
            return FlareInviteAnswer.Yes;
        }

        /// <summary>Somebody new may meet the player: a few messages of theirs, or a good name in the city.</summary>
        public static bool MayMeet(FlareInviteAsk a) => a.Chats >= ChatsForYes || a.Standing >= StandingForYes;

        /// <summary>When the answer comes: a yes within ten to thirty minutes, the rest within half an hour to two hours.</summary>
        public static long AnswerAt(long sent, int seed, int dm, bool yes)
        {
            int roll = FlareRules.Roll(seed, dm, 844);   // every answer's roll: a maybe or a no comes when any answer would
            return yes ? sent + YesLeast + roll % YesSpread : sent + AnswerLeast + roll % AnswerSpread;
        }

        /// <summary>The earliest a plan agreed by a yes arriving then can start: its reminder, panel line and call-off all after it.</summary>
        public static long PlanFrom(long answerAt) => answerAt + PlanAfterAnswer;

        /// <summary>True if a yes arriving then is read before the plan's reminder and its line on the job panel.</summary>
        public static bool BeforePanel(long answerAt, long planAt) => answerAt <= planAt - PanelBefore;

        /// <summary>The written line's key of an answer: a yes by its outing, a maybe or a no by why.</summary>
        public static string Key(FlareInviteAnswer answer, FlareInviteWhy why, PlanKind kind)
        {
            switch (answer)
            {
                case FlareInviteAnswer.Yes:
                    return KeyStart + "yes_" + KindWords[KindIndex(kind)];
                case FlareInviteAnswer.No:
                    return KeyStart + "no_" + (why == FlareInviteWhy.Taken ? "taken" : why == FlareInviteWhy.Dislikes ? "dislikes" : why == FlareInviteWhy.Mood ? "mood" : "pass");
                default:
                    switch (why)
                    {
                        case FlareInviteWhy.Plan: return KeyStart + "maybe_plan";
                        case FlareInviteWhy.Night: return KeyStart + "maybe_night";
                        case FlareInviteWhy.Later: return KeyStart + "maybe_later";
                        case FlareInviteWhy.Nowhere: return KeyStart + "maybe_nowhere";
                        case FlareInviteWhy.Influencer: return KeyStart + "maybe_influencer";
                        case FlareInviteWhy.Stranger: return KeyStart + "maybe_stranger";
                        default: return KeyStart + "maybe_busy";
                    }
            }
        }

        /// <summary>Every key an answer can have, in the locales' order, for the checks.</summary>
        public static List<string> Keys()
        {
            var keys = new List<string>(20);
            for (int k = 0; k < KindWords.Length; k++)
            {
                keys.Add(KeyStart + "yes_" + KindWords[k]);
            }
            foreach (string m in new[] { "plan", "busy", "night", "stranger", "later", "nowhere", "influencer" })
            {
                keys.Add(KeyStart + "maybe_" + m);
            }
            foreach (string n in new[] { "taken", "dislikes", "mood", "pass" })
            {
                keys.Add(KeyStart + "no_" + n);
            }
            return keys;
        }

        /// <summary>The decision an answer's key says; false for a key that is not an invitation's answer.</summary>
        public static bool AnswerOf(string key, out FlareInviteAnswer answer)
        {
            answer = FlareInviteAnswer.Maybe;
            if (key == null || !key.StartsWith(KeyStart, StringComparison.Ordinal))
            {
                return false;
            }
            string rest = key.Substring(KeyStart.Length);
            if (rest.StartsWith("yes_", StringComparison.Ordinal))
            {
                answer = FlareInviteAnswer.Yes;
                return true;
            }
            if (rest.StartsWith("no_", StringComparison.Ordinal))
            {
                answer = FlareInviteAnswer.No;
                return true;
            }
            return rest.StartsWith("maybe_", StringComparison.Ordinal);
        }

        private static int KindIndex(PlanKind kind)
        {
            switch (kind)
            {
                case PlanKind.Meal: return 1;
                case PlanKind.Drinks: return 2;
                case PlanKind.Cinema: return 3;
                case PlanKind.Walk: return 4;
                case PlanKind.Workout: return 5;
                default: return 0;
            }
        }

        /// <summary>The answer's values as the message keeps them ("name=value" by '~'): what its line and card show and a yes's booking needs.</summary>
        public static string Data(FlareInviteAsk a, FlareInviteAnswer answer, FlareInviteWhy why)
        {
            var b = new StringBuilder(160);
            PlanKind kind = why == FlareInviteWhy.Plan ? a.PlanKind : a.Kind;
            Pair(b, "outing", a.Outing);
            Pair(b, "t_outing", CardOuting(kind));
            if (answer == FlareInviteAnswer.Yes || why == FlareInviteWhy.Plan)
            {
                Pair(b, "place", a.Place);
                Pair(b, "time", a.When);
                Pair(b, "t_time", a.CardWhen);
            }
            if (why == FlareInviteWhy.Dislikes && a.Dislikes)
            {
                Pair(b, "t_rude", "1");   // they remember something the player did: the card's reason (CardReason)
            }
            if (answer == FlareInviteAnswer.Yes)
            {
                Pair(b, "k", ((int)a.Kind).ToString(CultureInfo.InvariantCulture));
                Pair(b, "at", a.PlanAt.ToString(CultureInfo.InvariantCulture));
                Pair(b, "st", a.Street);
                Pair(b, "no", a.Number.ToString(CultureInfo.InvariantCulture));
                Pair(b, "p", (a.Known ? a.Cast : -1).ToString(CultureInfo.InvariantCulture));
                Pair(b, "f", a.First);
                Pair(b, "l", a.Last);
                Pair(b, "sx", a.Sex.ToString(CultureInfo.InvariantCulture));
                Pair(b, "ag", a.Age.ToString(CultureInfo.InvariantCulture));
                if (a.Face > 0)
                {
                    Pair(b, "fc", a.Face.ToString(CultureInfo.InvariantCulture));
                }
            }
            return b.ToString();
        }

        private static void Pair(StringBuilder b, string name, string value)
        {
            if (b.Length > 0)
            {
                b.Append('~');
            }
            b.Append(name).Append('=').Append(Safe(value));
        }

        /// <summary>A value as a message's data can keep it: no '~' or '=', one line.</summary>
        public static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            return value.Replace('~', ' ').Replace('=', ' ').Replace('\n', ' ').Replace('\r', ' ');
        }

        public static string Value(string data, string name)
        {
            if (string.IsNullOrEmpty(data))
            {
                return null;
            }
            string[] pairs = data.Split('~');
            for (int i = 0; i < pairs.Length; i++)
            {
                int eq = pairs[i].IndexOf('=');
                if (eq == name.Length && string.CompareOrdinal(pairs[i], 0, name, 0, eq) == 0)
                {
                    return pairs[i].Substring(eq + 1);
                }
            }
            return null;
        }

        /// <summary>A yes's plan read back from its message's data; null if a value it needs is missing or not a number.</summary>
        public static FlareInviteBook BookOf(FlareDm m)
        {
            string data = m != null ? m.Data : null;
            if (!int.TryParse(Value(data, "k"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int kind) || kind < (int)PlanKind.Coffee || kind > (int)PlanKind.Workout
                || !long.TryParse(Value(data, "at"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long at)
                || !int.TryParse(Value(data, "no"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
                || !int.TryParse(Value(data, "p"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cast) || string.IsNullOrEmpty(Value(data, "st")))
            {
                return null;
            }
            int.TryParse(Value(data, "sx"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int sex);
            int.TryParse(Value(data, "ag"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int age);
            int.TryParse(Value(data, "fc"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int face);
            return new FlareInviteBook
            {
                Kind = (PlanKind)kind, At = at, Street = Value(data, "st"), Number = number, Place = Value(data, "place"), Cast = cast,
                First = Value(data, "f"), Last = Value(data, "l"), Sex = sex, Age = age, Face = Math.Max(0, face)
            };
        }

        /// <summary>The mark a booked yes gets in its message, so it is never booked twice.</summary>
        public const string BookedMark = "~b=1";

        /// <summary>True if a yes's plan was booked, or the yes turned into a maybe.</summary>
        public static bool Booked(FlareDm m) => m != null && m.Data != null && m.Data.EndsWith(BookedMark, StringComparison.Ordinal);

        /// <summary>True for a yes still to be booked: the city's answer, its key a yes, not marked booked.</summary>
        public static bool Waiting(FlareDm m)
        {
            return m != null && !m.Mine && AnswerOf(m.Key, out FlareInviteAnswer a) && a == FlareInviteAnswer.Yes && !Booked(m);
        }

        /// <summary>The earliest minute at which a yes is booked (a little before it shows), or long.MaxValue for none.</summary>
        public static long Due(List<FlareDm> dms)
        {
            long due = long.MaxValue;
            for (int i = 0; dms != null && i < dms.Count; i++)
            {
                if (Waiting(dms[i]) && dms[i].At - BookAhead < due)
                {
                    due = dms[i].At - BookAhead;
                }
            }
            return due;
        }

        public const int BookAhead = 2;   // game minutes before it shows: the timer looks once a second

        /// <summary>A yes still to be booked with this account, or null (a second invitation then points to it).</summary>
        public static FlareDm WaitingWith(List<FlareDm> dms, string with)
        {
            for (int i = 0; dms != null && i < dms.Count; i++)
            {
                if (Waiting(dms[i]) && (with == null || dms[i].With == with))
                {
                    return dms[i];
                }
            }
            return null;
        }

        /// <summary>A yes that cannot be booked when due turns into "maybe, busy", a minute on so the yes's words never show with it.</summary>
        public static void Unbook(FlareDm m)
        {
            m.Key = KeyStart + "maybe_busy";
            m.At += 1;
            m.Data = (m.Data ?? string.Empty) + BookedMark;
        }

        /// <summary>The account's messages to the player shown by now.</summary>
        public static int Chats(List<FlareDm> dms, string with, long now)
        {
            int n = 0;
            for (int i = 0; dms != null && i < dms.Count; i++)
            {
                FlareDm m = dms[i];
                n += !m.Mine && m.With == with && m.At <= now ? 1 : 0;
            }
            return n;
        }

        /// <summary>The outing as an invitation's card says it.</summary>
        public static string CardOuting(PlanKind kind)
        {
            switch (kind)
            {
                case PlanKind.Meal: return "dinner";
                case PlanKind.Drinks: return "drinks";
                case PlanKind.Cinema: return "the cinema";
                case PlanKind.Walk: return "a walk";
                case PlanKind.Workout: return "a workout";
                default: return "coffee";
            }
        }

        /// <summary>The intent of an answer by its key (an influencer is worded as a city person); null for another key.</summary>
        public static string Intent(string key)
        {
            if (!AnswerOf(key, out FlareInviteAnswer a))
            {
                return null;
            }
            return "citizen.dm.invite_" + (a == FlareInviteAnswer.Yes ? "yes" : a == FlareInviteAnswer.No ? "no" : "maybe");
        }

        /// <summary>The intent of an answer if the pack is trained on it (data v3 on); null keeps its written line.</summary>
        public static string TrainedIntent(string key, int data)
        {
            return data >= TextIntents.DataV3 && key != KeyStart + "maybe_plan" ? Intent(key) : null;
        }

        // The reasons as data v3 learnt them (defs_v3.REASONS), word for word.
        public const string ReasonBarely = "they barely know you", ReasonBusy = "they are busy at the moment";
        public const string ReasonTaken = "they are seeing someone", ReasonNot = "they are not interested";
        public const string ReasonRude = "you were rude to them before", ReasonTooBusy = "they are too busy";

        /// <summary>The reason on a maybe's or a no's card, by the answer's key and, for a grudge, its data; null for a yes or another key.</summary>
        public static string CardReason(string key, string data)
        {
            if (key == null || !key.StartsWith(KeyStart, StringComparison.Ordinal))
            {
                return null;
            }
            switch (key.Substring(KeyStart.Length))
            {
                case "maybe_stranger":
                case "maybe_later":
                case "maybe_influencer": return ReasonBarely;
                case "maybe_night":
                case "maybe_busy":
                case "maybe_nowhere": return ReasonBusy;
                case "no_taken": return ReasonTaken;
                case "no_mood": return ReasonTooBusy;
                case "no_dislikes": return Value(data, "t_rude") == "1" ? ReasonRude : ReasonNot;
                case "no_pass": return ReasonNot;
                default: return null;
            }
        }

        /// <summary>A city person's mood on an invitation's card: the day's, but "fine" for a yes on a low day, a rough day for that no.</summary>
        public static string CardMood(string key, string dayMood)
        {
            if (AnswerOf(key, out FlareInviteAnswer a) && a == FlareInviteAnswer.Yes)
            {
                return dayMood == TextWords.Moods[0] || dayMood == TextWords.Moods[1] ? TextWords.Moods[2] : dayMood;
            }
            return key == KeyStart + "no_mood" ? TextWords.Moods[0] : dayMood;
        }

        /// <summary>The card's values of an answer from its message's data.</summary>
        public static void Slots(FlareDm m, TextSlots slots)
        {
            slots.Set("outing", Value(m.Data, "t_outing"));
            string place = Value(m.Data, "place"), when = Value(m.Data, "t_time"), reason = CardReason(m.Key, m.Data);
            if (!string.IsNullOrEmpty(place))
            {
                slots.Set("place", place);
            }
            if (!string.IsNullOrEmpty(when))
            {
                slots.Set("time", when);
            }
            if (reason != null)
            {
                slots.Set("reason", reason);
            }
        }
    }
}
