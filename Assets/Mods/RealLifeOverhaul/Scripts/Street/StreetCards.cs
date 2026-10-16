using System;
using System.Text.RegularExpressions;

namespace RealLifeOverhaul.Street
{
    /// <summary>
    /// Which card of RLO Texts words a line of street talk (Wiring/StreetTexts asks it): data v4's own (TextIntents.Street.cs,
    /// a branch per outcome) and, until a v4 pack is trained, what of data v3 fits; the stranger's role, the mood leaned the
    /// way the game decided, and the words never shown because they speak of chat rather than of speech. Game-free
    /// (EconomyCheck, StreetTalkChecks.cs: every line's card is registered and is the generator's). COST: a switch.
    /// </summary>
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
                default: return null;   // (waiting, the question of where to: always written)
            }
        }

        /// <summary>The card of data v3 that fits a line until a v4 pack is trained (its branch none), and the mood's lean; null for the written line.</summary>
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

        /// <summary>The stranger's role on a v4 card: the generator's own two (somebody met earlier today on the branches that say so).</summary>
        public static string Role(string intent, string branch)
        {
            bool again = (intent == "street.greet" && branch == "again") || (intent == "street.name" && branch == "known") || (intent == "street.number" && branch == "already")
                         || (intent == "street.business" && branch == "again") || (intent == "street.brush" && branch == "rude");
            return again ? "somebody you talked to in the street earlier today" : "a stranger in the street";
        }

        /// <summary>Today's mood leaned the way the game decided (-2 a rough day ... 2 a great one).</summary>
        public static int Leaned(int mood, int lean) => lean > 0 ? Math.Max(0, mood) : lean < 0 ? Math.Min(0, mood) : mood;

        /// <summary>Words that speak of chat rather than of speech (the v3 cards are a Flare message's): never shown.</summary>
        public static bool Chatty(string words) => words != null && Chat.IsMatch(words);
    }
}
