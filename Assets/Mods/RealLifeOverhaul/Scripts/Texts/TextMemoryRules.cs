using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Texts
{
    /// <summary>The six kinds of thing a person remembers of the player (data v6); as flags, what an intent's speaker may remember.</summary>
    [Flags]
    public enum TextMemoryKinds
    {
        None = 0,
        Met = 1,       // how and where they met, and when
        Talked = 2,    // when they last talked, and what about
        Tone = 4,      // how a talk went: well, coolly, rudely, flirting
        Favour = 8,    // directions given, numbers swapped, a loan, a raise
        Knows = 16,    // the player's name, job, business, fame
        Event = 32,    // something of the player's they heard of
        All = 63
    }

    /// <summary>One thing of the player's a person remembers: its kind and code, the game day it happened, the name or sum it names, and who told them.</summary>
    public struct TextFact
    {
        public TextMemoryKinds Kind;   // one of the six, never several
        public string Code;            // what exactly: "street", "directions", "warm", "name", "crash" (TextMemoryRules.Codes)
        public int Day;                // the game day it happened
        public string Value;           // the name, place or sum it names ("Bean There", "$200"), or null
        public string From;            // an event heard from somebody (gossip): their first name; else null

        public TextFact(TextMemoryKinds kind, string code, int day, string value = null, string from = null)
        {
            Kind = kind;
            Code = code;
            Day = day;
            Value = value;
            From = from;
        }
    }

    /// <summary>
    /// What people remember of the player, as RLO Texts puts it on a card (data v6). The game decides and records every
    /// fact (TextsDirector.Remember, from street talk, texts, calls, Flare messages, the staff, the social life's own
    /// memories and the player's life events); the model never writes one. A card carries a few of them as "memory:" lines,
    /// chosen here, the same way every time for the same facts:
    ///
    ///   who may    an intent says which kinds its speaker may remember and how many lines (TextIntent.MemoryKinds,
    ///              MemoryMost): none for what everybody reads (a post, a comment, the paper) or a shared card
    ///   fresh      each kind is remembered for so many days (<see cref="Lifetime"/>), then left out
    ///   relevant   a fact that bears on the intent comes first (directions on a card about the way, a loan on one about
    ///              money: <see cref="Related"/>); then the kind's weight (<see cref="Weight"/>), the newer before the older,
    ///              and the code and its name in order, so that nothing depends on the order they were recorded in
    ///   not twice  one fact of each kind and code (the newest); none that the card's own news already says (its subject)
    ///   the line   "memory: " and the fact in the card's English words, with how long ago (<see cref="Line"/>)
    ///
    /// Game-free (EconomyCheck, TextsMemoryChecks.cs). COST: a pass over a person's few facts and a sort of a handful, a card.
    /// </summary>
    public static class TextMemoryRules
    {
        public const int CardMost = 3;         // memory lines a card carries at most
        public const int PeopleMost = 400;     // people remembered at most: past this the one met with longest ago goes
        public const int FactsMost = 8;        // facts a person keeps at most: past this the least of them goes
        public const int StrangerDays = 1;     // days a stranger in the street is remembered (they are somebody else the next day: Street/StreetStranger)
        public const int RelatedBonus = 40;    // what bearing on the intent adds to a fact's weight
        public const int AgeMost = 30;         // the most a fact's age takes off its weight

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly List<TextFact> Picked = new List<TextFact>(16);
        private static readonly List<int> Scores = new List<int>(16);

        /// <summary>Days a kind of fact is remembered for; past it, it is left out of cards and let go from the store.</summary>
        public static int Lifetime(TextMemoryKinds kind)
        {
            switch (kind)
            {
                case TextMemoryKinds.Met: return 3650;
                case TextMemoryKinds.Talked: return 60;
                case TextMemoryKinds.Tone: return 30;
                case TextMemoryKinds.Favour: return 120;
                case TextMemoryKinds.Knows: return 3650;
                case TextMemoryKinds.Event: return 90;
                default: return 0;
            }
        }

        /// <summary>A kind's weight on a card: what they heard of and favours first, how they met last.</summary>
        public static int Weight(TextMemoryKinds kind)
        {
            switch (kind)
            {
                case TextMemoryKinds.Event: return 50;
                case TextMemoryKinds.Favour: return 45;
                case TextMemoryKinds.Knows: return 30;
                case TextMemoryKinds.Talked: return 25;
                case TextMemoryKinds.Tone: return 20;
                case TextMemoryKinds.Met: return 15;
                default: return 0;
            }
        }

        /// <summary>The name of a kind as the contract writes it ("met", "talked", "tone", "favour", "knows", "event").</summary>
        public static string Name(TextMemoryKinds kind)
        {
            switch (kind)
            {
                case TextMemoryKinds.Met: return "met";
                case TextMemoryKinds.Talked: return "talked";
                case TextMemoryKinds.Tone: return "tone";
                case TextMemoryKinds.Favour: return "favour";
                case TextMemoryKinds.Knows: return "knows";
                case TextMemoryKinds.Event: return "event";
                default: return null;
            }
        }

        /// <summary>The kind of a contract name; None for an unknown one.</summary>
        public static TextMemoryKinds KindOf(string name)
        {
            for (int bit = 1; bit < (int)TextMemoryKinds.All; bit <<= 1)
            {
                if (Name((TextMemoryKinds)bit) == name)
                {
                    return (TextMemoryKinds)bit;
                }
            }
            return TextMemoryKinds.None;
        }

        // ---------------------------------------------------------------- the lines

        /// <summary>
        /// Every code of every kind with its line in the card's English words; "{value}" is the fact's name or sum, and a line
        /// marked with a trailing '*' takes no "how long ago" (a standing fact). Codes whose line needs a value say so by
        /// naming it; with no value such a fact is not put on a card. The contract lists them all (SPEC_v6.json).
        /// </summary>
        public static readonly string[][] Codes =
        {
            // met
            new[] { "met", "street", "met you in the street" },
            new[] { "met", "work", "met you at work" },
            new[] { "met", "gym", "met you at the gym" },
            new[] { "met", "coffee", "met you at the coffee cart" },
            new[] { "met", "neighbour", "lives next door to you*" },
            new[] { "met", "helped", "met you when you helped them" },
            new[] { "met", "customer", "met you at your shop" },
            new[] { "met", "flare", "met you on Flare" },
            new[] { "met", "event", "met you at a street event" },
            new[] { "met", "old_friend", "is an old friend of yours*" },
            new[] { "met", "staff", "works for you at {value}*" },
            new[] { "met", "dm", "first messaged you on Flare" },
            // talked
            new[] { "talked", "street", "talked to you in the street about {value}" },
            new[] { "talked", "text", "texted you about {value}" },
            new[] { "talked", "call", "talked to you on the phone" },
            new[] { "talked", "dm", "messaged you on Flare" },
            new[] { "talked", "outing", "met up with you for {value}" },
            new[] { "talked", "visit", "dropped by your place" },
            new[] { "talked", "tagged", "was tagged in a post of yours on Flare" },
            // tone
            new[] { "tone", "warm", "the last talk with you went well" },
            new[] { "tone", "cool", "the last talk with you was cool" },
            new[] { "tone", "rude", "you were rude to them" },
            new[] { "tone", "flirty", "you flirted with them" },
            new[] { "tone", "threat", "you threatened them" },
            new[] { "tone", "stood_up", "you stood them up" },
            new[] { "tone", "late", "you were late to meet them" },
            new[] { "tone", "asked_out", "you asked them out" },
            new[] { "tone", "broke_up", "you broke up with them" },
            // favour
            new[] { "favour", "directions", "gave you directions to {value}" },
            new[] { "favour", "number", "swapped numbers with you" },
            new[] { "favour", "lent", "lent you money" },
            new[] { "favour", "owed", "you still owe them {value}*" },
            new[] { "favour", "borrowed", "borrowed money from you" },
            new[] { "favour", "owes", "still owes you {value}*" },
            new[] { "favour", "not_repaid", "you never paid them back" },
            new[] { "favour", "lift", "got a lift from you" },
            new[] { "favour", "moving", "had your help moving house" },
            new[] { "favour", "birthday", "you remembered their birthday" },
            new[] { "favour", "introduced", "introduced you to a friend" },
            new[] { "favour", "raise", "got a raise from you, to {value} an hour" },
            new[] { "favour", "invite", "said yes to going out with you" },
            // knows
            new[] { "knows", "name", "knows your name*" },
            new[] { "knows", "job", "knows you work as {value}*" },
            new[] { "knows", "business", "knows you run {value}*" },
            new[] { "knows", "famous", "knows you are well known in the city*" },
            new[] { "knows", "infamous", "knows you have a bad name in the city*" },
            // event
            new[] { "event", "crash", "you crashed your car" },
            new[] { "event", "knocked", "you were knocked down by a car" },
            new[] { "event", "arrest", "you were arrested" },
            new[] { "event", "court_guilty", "you were found guilty in court" },
            new[] { "event", "court_cleared", "you were cleared in court" },
            new[] { "event", "opened", "you opened {value}" },
            new[] { "event", "graduated", "you graduated" },
            new[] { "event", "listed", "your company {value} went on the stock market" },
            new[] { "event", "married", "you got married" },
            new[] { "event", "baby", "you had a baby" },
            new[] { "event", "win", "you had a big win" },
            new[] { "event", "loss", "you had a big loss" },
            new[] { "event", "breakin", "{value} was broken into" }
        };

        /// <summary>The line of a kind and code ("met you in the street"), or null for an unknown one; '*' still on a standing fact's.</summary>
        public static string Template(TextMemoryKinds kind, string code)
        {
            string name = Name(kind);
            for (int i = 0; i < Codes.Length; i++)
            {
                if (Codes[i][0] == name && Codes[i][1] == code)
                {
                    return Codes[i][2];
                }
            }
            return null;
        }

        /// <summary>"today", "yesterday", "3 days ago", "a few weeks ago" or "months ago": how long ago a day is, from today.</summary>
        public static string Ago(int days)
        {
            if (days <= 0)
            {
                return "today";
            }
            if (days == 1)
            {
                return "yesterday";
            }
            if (days < 14)
            {
                return days.ToString(Inv) + " days ago";
            }
            return days < 60 ? "a few weeks ago" : "months ago";
        }

        /// <summary>
        /// A fact as its memory line's words, without "memory: ": its code's line with its value, an event as heard ("heard you
        /// crashed your car", "heard from Dana that you crashed your car"), and how long ago unless it is a standing fact.
        /// Null for an unknown code, or a code that names a value it does not have.
        /// </summary>
        public static string Line(TextFact f, int today)
        {
            string template = Template(f.Kind, f.Code);
            if (template == null)
            {
                return null;
            }
            bool standing = template.EndsWith("*", StringComparison.Ordinal);
            string text = standing ? template.Substring(0, template.Length - 1) : template;
            if (text.IndexOf("{value}", StringComparison.Ordinal) >= 0)
            {
                if (string.IsNullOrEmpty(f.Value))
                {
                    return null;
                }
                text = text.Replace("{value}", TextIntents.Plain(f.Value));
            }
            if (f.Kind == TextMemoryKinds.Event)
            {
                text = string.IsNullOrEmpty(f.From) ? "heard " + text : "heard from " + TextIntents.Plain(f.From) + " that " + text;
            }
            return standing ? text : text + ", " + Ago(today - f.Day);
        }

        // ---------------------------------------------------------------- choosing a card's lines

        /// <summary>
        /// The memory lines for a card, in order, into <paramref name="lines"/> (cleared first): of the facts its intent's
        /// speaker may remember, fresh, not its own subject, the most relevant first, one of each kind and code, at most the
        /// intent's most. <paramref name="subject"/> is the event the card's own news tells (null for none).
        /// </summary>
        public static int Select(List<TextFact> facts, TextIntent intent, int today, string subject, List<string> lines)
        {
            lines.Clear();
            if (facts == null || intent == null || intent.MemoryMost <= 0 || intent.MemoryKinds == TextMemoryKinds.None)
            {
                return 0;
            }
            Picked.Clear();
            Scores.Clear();
            for (int i = 0; i < facts.Count; i++)
            {
                TextFact f = facts[i];
                int age = today - f.Day;
                if ((intent.MemoryKinds & f.Kind) == 0 || age < 0 || age > Lifetime(f.Kind) || Line(f, today) == null
                    || (f.Kind == TextMemoryKinds.Event && subject != null && f.Code.StartsWith(subject, StringComparison.Ordinal)))
                {
                    continue;   // (an event the card's own news tells: "court" is either verdict)
                }
                int same = Same(f);
                int score = Weight(f.Kind) + (Related(intent.Key, f) ? RelatedBonus : 0) - Math.Min(AgeMost, age / 2);
                if (same >= 0)
                {
                    if (Before(f, score, Picked[same], Scores[same]))
                    {
                        Picked[same] = f;
                        Scores[same] = score;
                    }
                    continue;
                }
                Picked.Add(f);
                Scores.Add(score);
            }
            // A small sort by insertion: a handful of facts, and no delegate made.
            for (int i = 1; i < Picked.Count; i++)
            {
                TextFact f = Picked[i];
                int s = Scores[i];
                int j = i - 1;
                while (j >= 0 && Before(f, s, Picked[j], Scores[j]))
                {
                    Picked[j + 1] = Picked[j];
                    Scores[j + 1] = Scores[j];
                    j--;
                }
                Picked[j + 1] = f;
                Scores[j + 1] = s;
            }
            int most = Math.Min(Math.Min(intent.MemoryMost, CardMost), Picked.Count);
            for (int i = 0; i < most; i++)
            {
                lines.Add(Line(Picked[i], today));
            }
            Picked.Clear();
            Scores.Clear();
            return lines.Count;
        }

        // The picked fact of the same kind and code, or -1.
        private static int Same(TextFact f)
        {
            for (int i = 0; i < Picked.Count; i++)
            {
                if (Picked[i].Kind == f.Kind && Picked[i].Code == f.Code)
                {
                    return i;
                }
            }
            return -1;
        }

        // The card's order: the higher score, then the kind's place, the newer, the code, the value and who told them, ordinally.
        private static bool Before(TextFact a, int scoreA, TextFact b, int scoreB)
        {
            if (scoreA != scoreB)
            {
                return scoreA > scoreB;
            }
            if (a.Kind != b.Kind)
            {
                return Weight(a.Kind) > Weight(b.Kind);
            }
            if (a.Day != b.Day)
            {
                return a.Day > b.Day;
            }
            int c = string.CompareOrdinal(a.Code, b.Code);
            if (c != 0)
            {
                return c < 0;
            }
            c = string.CompareOrdinal(a.Value ?? string.Empty, b.Value ?? string.Empty);
            return c != 0 ? c < 0 : string.CompareOrdinal(a.From ?? string.Empty, b.From ?? string.Empty) < 0;
        }

        /// <summary>
        /// Whether a fact bears on an intent: the way given on a card about the way, a loan on a card about money, numbers on
        /// one about numbers, what they know of the business on one about it, the outings and how they went on a plan's. A
        /// fixed table by the intent's key, the same on every machine.
        /// </summary>
        public static bool Related(string intent, TextFact f)
        {
            if (intent == null)
            {
                return false;
            }
            string code = f.Code;
            switch (f.Kind)
            {
                case TextMemoryKinds.Favour:
                    if (code == "directions")
                    {
                        return Starts(intent, "street.directions") || Starts(intent, "street.thanks") || Starts(intent, "street.area");
                    }
                    if (code == "number")
                    {
                        return Starts(intent, "street.number") || Starts(intent, "street.invite") || Starts(intent, "street.greet");
                    }
                    if (code == "lent" || code == "owed" || code == "borrowed" || code == "owes" || code == "not_repaid")
                    {
                        return Starts(intent, "friend.loan") || Starts(intent, "friend.debt") || Starts(intent, "friend.ask_loan") || Starts(intent, "lender.");
                    }
                    if (code == "raise")
                    {
                        return Starts(intent, "employee.");
                    }
                    if (code == "lift")
                    {
                        return Starts(intent, "friend.lift");
                    }
                    return code == "invite" && (Starts(intent, "street.invite") || Starts(intent, "citizen.dm.invite"));
                case TextMemoryKinds.Knows:
                    if (code == "name")
                    {
                        return Starts(intent, "street.name") || Starts(intent, "street.greet") || Starts(intent, "street.number");
                    }
                    if (code == "business")
                    {
                        return Starts(intent, "street.business") || Starts(intent, "friend.rich") || Starts(intent, "friend.breakin");
                    }
                    return (code == "famous" || code == "infamous") && Starts(intent, "street.greet");
                case TextMemoryKinds.Tone:
                    return Starts(intent, "street.greet") || Starts(intent, "street.hello") || Starts(intent, "partner.") || Starts(intent, "friend.drift")
                           || ((code == "stood_up" || code == "late") && (Starts(intent, "friend.invite") || Starts(intent, "friend.remind") || Starts(intent, "friend.greet")));
                case TextMemoryKinds.Talked:
                    return code == "outing" ? Starts(intent, "friend.invite") || Starts(intent, "friend.thanks") || Starts(intent, "friend.greet")
                        : Starts(intent, "friend.drift") || Starts(intent, "mom.worry") || Starts(intent, "partner.missing") || Starts(intent, "friend.missed_call");
                case TextMemoryKinds.Event:
                    return Starts(intent, "friend.gossip") || Starts(intent, "mom.worry") || Starts(intent, "friend.drift");
                default:
                    return Starts(intent, "street.greet") && code == "street";
            }
        }

        private static bool Starts(string intent, string prefix) => intent.StartsWith(prefix, StringComparison.Ordinal);

        // ---------------------------------------------------------------- whose memory

        /// <summary>
        /// Whether a key is one a person's memory may be kept under: a person of the player's own game, the same in a save
        /// played alone or in a Going Public session ("s:3" a person of the social life by their place in it, "sp:" a
        /// stranger by their seed, "e:" one of the player's staff by the game's id, "c:" a city person of Flare by their
        /// number, "boss:" a manager by the employer, "r:" the roommate, "mom", "inlaws", "lender"). Never a key of the
        /// session's own numbers ("fn:", "lp:"), which name what everybody reads.
        /// </summary>
        public static bool KeyOk(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > 80 || key.IndexOf('|') >= 0 || key.IndexOf('^') >= 0)
            {
                return false;
            }
            return key == "mom" || key == "inlaws" || key == "lender" || Prefixed(key, "s:") || Prefixed(key, "sp:") || Prefixed(key, "e:") || Prefixed(key, "c:")
                   || Prefixed(key, "boss:") || Prefixed(key, "r:") || Prefixed(key, "f") || Prefixed(key, "p");
        }

        private static bool Prefixed(string key, string prefix)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal) || key.Length == prefix.Length)
            {
                return false;
            }
            if (prefix == "f" || prefix == "p")
            {
                for (int i = 1; i < key.Length; i++)
                {
                    if (key[i] < '0' || key[i] > '9')
                    {
                        return false;   // ("f12", "p3": a friend or the partner as Flare's messages name them; never "fn:")
                    }
                }
            }
            return true;
        }

        /// <summary>True for a stranger's key: remembered only for <see cref="StrangerDays"/> past their last fact.</summary>
        public static bool Stranger(string key) => key != null && key.StartsWith("sp:", StringComparison.Ordinal);

        /// <summary>Whether a fact is still kept on this day (the kind's lifetime, a stranger's day).</summary>
        public static bool Kept(string key, TextFact f, int today)
        {
            int age = today - f.Day;
            return age >= -1 && age <= (Stranger(key) ? Math.Min(StrangerDays, Lifetime(f.Kind)) : Lifetime(f.Kind));
        }

        /// <summary>Which of a person's facts goes first when they keep too many: the least weighty kind, then the oldest; -1 for none.</summary>
        public static int Least(List<TextFact> facts)
        {
            int least = -1;
            for (int i = 0; i < facts.Count; i++)
            {
                if (least < 0 || Weight(facts[i].Kind) < Weight(facts[least].Kind)
                    || (Weight(facts[i].Kind) == Weight(facts[least].Kind) && facts[i].Day < facts[least].Day))
                {
                    least = i;
                }
            }
            return least;
        }
    }
}
