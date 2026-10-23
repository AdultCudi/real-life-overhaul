using System;
using System.Text.RegularExpressions;

namespace RealLifeOverhaul.Street
{
    /// <summary>Which card of RLO Texts words a line of street talk, and the model's words made fit to be said. Game-free. COST: a switch.</summary>
    public static class StreetCards
    {
        private static readonly Regex Chat = new Regex(@"\b(flare|messages?|dms?|inbox|followers?|following|posts?|posted|online|app|text me|texting|emoji|profile)\b|@",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>The card of data v4 for a line: its intent and branch (null intent: none), and which way the mood leans (-1, 0, 1).</summary>
        public static string V4(StreetLine line, out string branch, out int lean)
        {
            branch = null;
            lean = 0;
            switch (line)
            {
                case StreetLine.OpenWarm: case StreetLine.OpenFine: case StreetLine.OpenCool: branch = "stranger"; return "street.greet";
                case StreetLine.OpenAgain: branch = "again"; lean = 1; return "street.greet";
                case StreetLine.OpenFamous: branch = "famous"; lean = 1; return "street.greet";
                case StreetLine.OpenInfamous: branch = "infamous"; lean = -1; return "street.greet";
                case StreetLine.BrushHurry: branch = "hurry"; return "street.brush";
                case StreetLine.BrushWork: branch = "work"; return "street.brush";
                case StreetLine.BrushNight: branch = "night"; return "street.brush";
                case StreetLine.BrushRain: branch = "rain"; return "street.brush";
                case StreetLine.BrushMood: branch = "mood"; lean = -1; return "street.brush";
                case StreetLine.BrushRude: branch = "rude"; lean = -1; return "street.brush";
                case StreetLine.BrushUnwashed: branch = "unwashed"; return "street.brush";
                case StreetLine.BrushRecord: branch = "record"; return "street.brush";
                case StreetLine.BrushWanted: branch = "wanted"; return "street.brush";
                case StreetLine.BrushPhone: branch = "phone"; return "street.brush";
                case StreetLine.SmallWarm: branch = "well"; lean = 1; return "street.smalltalk";
                case StreetLine.SmallCool: branch = "badly"; lean = -1; return "street.smalltalk";
                case StreetLine.AboutJob: branch = "share"; return "street.about";
                case StreetLine.AboutRetired: branch = "retired"; return "street.about";
                case StreetLine.AboutCool: branch = "refuse"; lean = -1; return "street.about";
                case StreetLine.NameGive: branch = "give"; lean = 1; return "street.name";
                case StreetLine.NameWithhold: branch = "withhold"; return "street.name";
                case StreetLine.NameKnown: branch = "known"; return "street.name";
                case StreetLine.AreaPlaces: branch = "places"; return "street.area";
                case StreetLine.AreaEvent: branch = "event"; return "street.area";
                case StreetLine.AreaNews: branch = "news"; return "street.area";
                case StreetLine.AreaNothing: branch = "nothing"; return "street.area";
                case StreetLine.DirFound: branch = "found"; return "street.directions";
                case StreetLine.DirFar: branch = "far"; return "street.directions";
                case StreetLine.DirNone: branch = "none"; return "street.directions";
                case StreetLine.ComplimentYes: branch = "well"; lean = 1; return "street.compliment";
                case StreetLine.ComplimentNo: branch = "badly"; lean = -1; return "street.compliment";
                case StreetLine.JokeYes: branch = "well"; lean = 1; return "street.joke";
                case StreetLine.JokeNo: branch = "badly"; lean = -1; return "street.joke";
                case StreetLine.FlirtYes: branch = "yes"; lean = 1; return "street.flirt";
                case StreetLine.FlirtNo: branch = "no"; return "street.flirt";
                case StreetLine.FlirtTaken: branch = "taken"; return "street.flirt";
                case StreetLine.NumberYes: branch = "yes"; lean = 1; return "street.number";
                case StreetLine.NumberNo: branch = "no"; lean = -1; return "street.number";
                case StreetLine.NumberTaken: branch = "taken"; return "street.number";
                case StreetLine.NumberAlready: branch = "already"; return "street.number";
                case StreetLine.NumberLater: branch = "later"; return "street.number";
                case StreetLine.InviteYes: branch = "yes"; lean = 1; return "street.invite";
                case StreetLine.InviteMaybeStranger: branch = "maybe_barely"; return "street.invite";
                case StreetLine.InviteMaybeBusy: branch = "maybe_busy"; return "street.invite";
                case StreetLine.InviteMaybeNight: branch = "maybe_night"; return "street.invite";
                case StreetLine.InviteMaybeLater: branch = "maybe_soon"; return "street.invite";
                case StreetLine.InviteMaybeNowhere: branch = "maybe_nowhere"; return "street.invite";
                case StreetLine.InviteMaybePlan: branch = "maybe_plans"; return "street.invite";
                case StreetLine.InviteNoTaken: branch = "no_taken"; return "street.invite";
                case StreetLine.InviteNoDislikes: branch = "no_dislike"; lean = -1; return "street.invite";
                case StreetLine.InviteNo: branch = "no"; return "street.invite";
                case StreetLine.ElectionFor: branch = "for"; return "street.election";
                case StreetLine.ElectionUnsure: branch = "unsure"; return "street.election";
                case StreetLine.ElectionNone: branch = "none"; return "street.election";
                case StreetLine.EconomyUp: branch = "up"; return "street.economy";
                case StreetLine.EconomyDown: branch = "down"; return "street.economy";
                case StreetLine.EconomyFlat: branch = "flat"; return "street.economy";
                case StreetLine.BusinessHeard: branch = "heard"; lean = 1; return "street.business";
                case StreetLine.BusinessWill: branch = "will"; lean = 1; return "street.business";
                case StreetLine.BusinessNo: branch = "no"; return "street.business";
                case StreetLine.BusinessAgain: branch = "again"; return "street.business";
                case StreetLine.SaidWarm: branch = "warm"; lean = 1; return "street.said";
                case StreetLine.SaidCool: branch = "cool"; lean = -1; return "street.said";
                case StreetLine.SaidOdd: branch = "odd"; return "street.said";
                case StreetLine.RudeCold: branch = "cold"; lean = -1; return "street.rude";
                case StreetLine.WalkOff: branch = "leave"; lean = -1; return "street.rude";
                case StreetLine.ThreatLeave: branch = "leave"; lean = -1; return "street.threat";
                case StreetLine.Tired: return "street.tired";
                case StreetLine.Bye: branch = "warm"; lean = 1; return "street.bye";
                case StreetLine.ByeCool: branch = "cool"; lean = -1; return "street.bye";
                default: return null;   // waiting, the question of where to: always written
            }
        }

        /// <summary>The card of data v5 for a line: v4's, and the lines v4 had no card for. Null: written.</summary>
        public static string V5(StreetLine line, bool warmThanks, out string branch, out int lean)
        {
            branch = null;
            lean = 0;
            switch (line)
            {
                case StreetLine.DayGood: branch = "good"; lean = 1; return "street.day";
                case StreetLine.DayFine: branch = "fine"; return "street.day";
                case StreetLine.DayLow: branch = "low"; lean = -1; return "street.day";
                case StreetLine.EventGoing: branch = "going"; return "street.event";
                case StreetLine.EventNot: branch = "not"; return "street.event";
                case StreetLine.WorkLikes: branch = "likes"; return "street.work";
                case StreetLine.WorkMixed: branch = "mixed"; return "street.work";
                case StreetLine.WorkRetired: branch = "retired"; return "street.work";
                case StreetLine.Welcome: branch = warmThanks ? "warm" : "cool"; return "street.thanks";
                case StreetLine.GreetBack: branch = "again"; return "street.hello";
                default: return V4(line, out branch, out lean);
            }
        }

        /// <summary>The card of a line for a pack of this data version (a v3 pack is never asked this).</summary>
        public static string ByData(StreetLine line, int data, bool warmThanks, out string branch, out int lean)
        {
            return data >= Texts.TextIntents.DataV5 ? V5(line, warmThanks, out branch, out lean) : V4(line, out branch, out lean);
        }

        /// <summary>The card's moment (defs_v5.moment_of) from the weekday (0 Monday) and hour; null when there is none.</summary>
        public static string Moment(int weekday, int hour)
        {
            bool weekend = weekday >= 5;
            if (!weekend && ((hour >= 7 && hour < 10) || (hour >= 17 && hour < 19)))
            {
                return "rush hour";
            }
            if (hour >= 20 || hour < 6)
            {
                return "night";
            }
            if (weekend && hour >= 10 && hour < 18)
            {
                return "the weekend";
            }
            if (hour >= 6 && hour < 11)
            {
                return "morning";
            }
            return hour >= 17 ? "evening" : null;
        }

        /// <summary>Whether a stranger knows the player's name: told today, or famed; never for their own name given or numbers swapped.</summary>
        public static bool KnowsName(bool famed, bool askedName, bool toldToday) => famed || askedName || toldToday;

        /// <summary>The card of data v3 that fits a line, and the mood's lean; null for the written line.</summary>
        public static string V3(StreetLine line, out string reason, out int lean)
        {
            reason = null;
            V4(line, out _, out lean);
            switch (line)
            {
                case StreetLine.OpenWarm: case StreetLine.OpenFine: case StreetLine.OpenCool: case StreetLine.OpenAgain:
                case StreetLine.SmallWarm: case StreetLine.SmallCool: case StreetLine.ComplimentYes: case StreetLine.ComplimentNo:
                case StreetLine.JokeYes: case StreetLine.JokeNo: case StreetLine.FlirtYes: case StreetLine.FlirtNo:
                case StreetLine.SaidWarm: case StreetLine.SaidCool: case StreetLine.Bye: case StreetLine.ByeCool:
                    return "citizen.dm.answer";
                case StreetLine.InviteYes:
                    return "citizen.dm.invite_yes";
                case StreetLine.InviteMaybeStranger: reason = "they barely know you"; return "citizen.dm.invite_maybe";
                case StreetLine.InviteMaybeBusy: case StreetLine.InviteMaybeNight: case StreetLine.InviteMaybeLater: case StreetLine.InviteMaybeNowhere:
                case StreetLine.InviteMaybePlan:
                    reason = "they are busy at the moment"; return "citizen.dm.invite_maybe";
                case StreetLine.InviteNoTaken: reason = "they are seeing someone"; return "citizen.dm.invite_no";
                case StreetLine.InviteNoDislikes: reason = "you were rude to them before"; return "citizen.dm.invite_no";
                case StreetLine.InviteNo: reason = "they are not interested"; return "citizen.dm.invite_no";
                default: return null;
            }
        }

        /// <summary>The stranger's role on a v4 card, one of the generator's own two.</summary>
        public static string Role(string intent, string branch)
        {
            bool again = (intent == "street.greet" && branch == "again") || (intent == "street.name" && branch == "known") || (intent == "street.number" && branch == "already")
                         || (intent == "street.business" && branch == "again") || (intent == "street.brush" && branch == "rude");
            return again ? "somebody you talked to in the street earlier today" : "a stranger in the street";
        }

        /// <summary>Today's mood leaned the way the game decided (-2 a rough day ... 2 a great one).</summary>
        public static int Leaned(int mood, int lean) => lean > 0 ? Math.Max(0, mood) : lean < 0 ? Math.Min(0, mood) : mood;

        /// <summary>True for words that speak of chat rather than of speech (the v3 cards are a Flare message's): never shown.</summary>
        public static bool Chatty(string words) => words != null && Chat.IsMatch(words);

        /// <summary>The lines no card of data v4 words: the v4 model's nearest cards answered them wrongly.</summary>
        public static bool Written(StreetLine line) => Written(line, Texts.TextIntents.DataV4);

        /// <summary>The lines always written for a pack of this data version.</summary>
        public static bool Written(StreetLine line, int data)
        {
            if (data >= Texts.TextIntents.DataV5)
            {
                return line == StreetLine.Waiting || line == StreetLine.DirAsk;
            }
            switch (line)
            {
                case StreetLine.Waiting: case StreetLine.DirAsk:
                case StreetLine.DayGood: case StreetLine.DayFine: case StreetLine.DayLow: case StreetLine.EventGoing: case StreetLine.EventNot:
                case StreetLine.WorkLikes: case StreetLine.WorkMixed: case StreetLine.WorkRetired: case StreetLine.Welcome: case StreetLine.GreetBack:
                    return true;
                default:
                    return false;
            }
        }

        private static readonly Regex Starts = new Regex(@"([.?]\s+)([a-z])", RegexOptions.CultureInvariant);

        /// <summary>
        /// The model's words made fit to be said: each sentence capitalised (the v4 model leaves a slot's words in lower case),
        /// an <paramref name="untold"/> name said in passing taken out; null when the name is still in them.
        /// </summary>
        public static string Spoken(string words, string untold)
        {
            if (string.IsNullOrEmpty(words))
            {
                return words;
            }
            string w = words;
            if (!string.IsNullOrEmpty(untold) && Regex.IsMatch(w, @"\b" + Regex.Escape(untold) + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                string name = Regex.Escape(untold);
                w = Regex.Replace(w, @",\s*" + name + @"\b(?=\s*[.,?;:]|\s*$)", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                w = Regex.Replace(w, @"^\s*" + name + @"\s*,\s*", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (Regex.IsMatch(w, @"\b" + name + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) || w.Trim().Length == 0)
                {
                    return null;
                }
                w = w.Trim();
            }
            w = Starts.Replace(w, m => m.Groups[1].Value + char.ToUpperInvariant(m.Groups[2].Value[0]));
            return w.Length > 0 && char.IsLower(w[0]) ? char.ToUpperInvariant(w[0]) + w.Substring(1) : w;
        }

        /// <summary>The model's last lines across all strangers: the v4 model greets most of them alike, so one said lately is not said again.</summary>
        public sealed class Echoes
        {
            public const int Most = 8;   // lines remembered: about two conversations' worth

            private readonly string[] _said = new string[Most];
            private int _next;

            /// <summary>True if one of the last lines is these words, case aside.</summary>
            public bool Heard(string words)
            {
                for (int i = 0; i < Most; i++)
                {
                    if (_said[i] != null && string.Equals(_said[i], Plain(words), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }

            public void Add(string words)
            {
                _said[_next] = Plain(words);
                _next = (_next + 1) % Most;
            }

            public void Clear()
            {
                Array.Clear(_said, 0, Most);
                _next = 0;
            }

            private static string Plain(string words) => words == null ? string.Empty : words.Trim().TrimEnd('.', '?', ',');
        }
    }
}
