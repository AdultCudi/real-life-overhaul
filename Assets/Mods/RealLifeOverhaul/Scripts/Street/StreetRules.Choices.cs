using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Street
{
    /// <summary>The rules of a conversation's turns: what is offered, what each choice comes to, the way somewhere, and after.</summary>
    public static partial class StreetRules
    {
        // A choice's tier in the round: what follows from their last line, the number and the invitation once they are warm,
        // what suits the moment, the rest, goodbye, and rudeness last.
        private const int TierFollow = 0, TierWarm = 1, TierMoment = 2, TierRest = 3, TierBye = 4, TierRude = 5;
        private const int TierSize = 10000;       // a tier's span of ranks
        private const int FitSize = 1000;         // ...and how well a choice suits the moment, within it (0 best)

        /// <summary>
        /// The things the player can say now, in the order "Something else" goes through them: by tier, then by the person's
        /// seed. COST: a pass over the choices and an insertion into a list of at most twenty, on a click.
        /// </summary>
        public static void Choices(StreetChat c, StreetFacts f, List<StreetAsk> into)
        {
            into.Clear();
            for (int i = 0; i < (int)StreetAsk.Count; i++)
            {
                var ask = (StreetAsk)i;
                if (ask == StreetAsk.Say || !Offered(c, f, ask))
                {
                    continue;
                }
                int rank = Rank(c, f, ask);
                c.Ranks[i] = rank;
                int at = into.Count;
                while (at > 0 && c.Ranks[(int)into[at - 1]] > rank)
                {
                    at--;
                }
                into.Insert(at, ask);
            }
        }

        /// <summary>A choice's place in the round: its tier, how well it suits the moment, the person's own order (lower first).</summary>
        public static int Rank(StreetChat c, StreetFacts f, StreetAsk ask)
        {
            int follow = FollowUp(c.Last, ask, c.Who.Seed);
            int turn = Roll(c.Who.Seed, 300 + (int)ask, FitSize);   // the person's own order, the same all day
            if (follow >= 0)
            {
                return TierFollow * TierSize + follow * FitSize + turn;
            }
            bool warm = Score(c) >= 2;
            switch (ask)
            {
                case StreetAsk.Number:
                case StreetAsk.Invite:
                    return (warm ? TierWarm : TierRest) * TierSize + turn;
                case StreetAsk.Day:
                    return TierMoment * TierSize + (c.Who.Mood == 2 || c.Who.Mood == -2 ? 0 : 1) * FitSize + turn;   // first when their mood shows
                case StreetAsk.SmallTalk:
                    return TierMoment * TierSize + (f.Raining || Night(f.Hour) || RushHour(f.Hour, f.Weekend) ? 0 : 1) * FitSize + turn;
                case StreetAsk.Event:
                    return TierMoment * TierSize + turn;
                case StreetAsk.AboutThem:
                case StreetAsk.Directions:
                case StreetAsk.Area:
                case StreetAsk.Compliment:
                    return (c.Turns == 0 ? TierMoment : TierRest) * TierSize + FitSize + turn;   // a usual opening; later, one of the rest
                case StreetAsk.Name:
                    return TierRest * TierSize + (c.Turns >= 1 ? 0 : 1) * FitSize + turn;
                case StreetAsk.Bye:
                    return TierBye * TierSize;
                case StreetAsk.Rude:
                    return TierRude * TierSize;
                default:
                    return TierRest * TierSize + FitSize + turn;
            }
        }

        /// <summary>The choice's place among the follow-ups of their last line (0 first), or -1; several are turned by the person's seed.</summary>
        public static int FollowUp(StreetLine last, StreetAsk ask, int seed)
        {
            int turn = Roll(seed, 400 + (int)last, 12);
            switch (last)
            {
                case StreetLine.AboutJob:
                case StreetLine.AboutRetired:
                    return At(ask, 0, StreetAsk.Work, StreetAsk.Name);
                case StreetLine.DirFound:
                case StreetLine.DirFar:
                case StreetLine.DirNone:
                    return At(ask, 0, StreetAsk.Thanks, StreetAsk.Area);
                case StreetLine.AreaPlaces:
                case StreetLine.AreaEvent:
                case StreetLine.AreaNews:
                case StreetLine.AreaNothing:
                    return At(ask, 0, StreetAsk.Directions, StreetAsk.Thanks);
                case StreetLine.ComplimentYes:
                case StreetLine.JokeYes:
                case StreetLine.FlirtYes:
                    return At(ask, 0, StreetAsk.Name, StreetAsk.Number, StreetAsk.Invite);
                case StreetLine.NameGive:
                    return At(ask, turn, StreetAsk.AboutThem, StreetAsk.Number, StreetAsk.Day);
                case StreetLine.SmallWarm:
                case StreetLine.DayGood:
                case StreetLine.DayFine:
                case StreetLine.SaidWarm:
                case StreetLine.GreetBack:
                    return At(ask, turn, StreetAsk.AboutThem, StreetAsk.Name, StreetAsk.Compliment, StreetAsk.Joke);
                case StreetLine.SmallCool:
                case StreetLine.DayLow:
                case StreetLine.AboutCool:
                case StreetLine.NameWithhold:
                case StreetLine.SaidCool:
                case StreetLine.ComplimentNo:
                case StreetLine.JokeNo:
                case StreetLine.FlirtNo:
                    return At(ask, turn, StreetAsk.Area, StreetAsk.Directions, StreetAsk.Day);
                case StreetLine.FlirtTaken:
                    return At(ask, turn, StreetAsk.AboutThem, StreetAsk.Area);
                case StreetLine.WorkLikes:
                case StreetLine.WorkMixed:
                case StreetLine.WorkRetired:
                    return At(ask, turn, StreetAsk.Name, StreetAsk.Area, StreetAsk.Compliment);
                case StreetLine.EventGoing:
                    return At(ask, 0, StreetAsk.Invite, StreetAsk.Name);
                case StreetLine.EventNot:
                    return At(ask, turn, StreetAsk.Area, StreetAsk.AboutThem);
                case StreetLine.NumberYes:
                    return At(ask, 0, StreetAsk.Invite, StreetAsk.Bye);
                case StreetLine.InviteYes:
                    return At(ask, 0, StreetAsk.Bye);
                case StreetLine.BusinessHeard:
                case StreetLine.BusinessWill:
                    return At(ask, 0, StreetAsk.Number, StreetAsk.Invite);
                case StreetLine.Welcome:
                    return At(ask, turn, StreetAsk.Name, StreetAsk.Day, StreetAsk.AboutThem);
                case StreetLine.ElectionFor:
                case StreetLine.ElectionUnsure:
                case StreetLine.EconomyUp:
                case StreetLine.EconomyDown:
                case StreetLine.EconomyFlat:
                    return At(ask, turn, StreetAsk.Day, StreetAsk.AboutThem, StreetAsk.SmallTalk);
                default:
                    return -1;
            }
        }

        // The place of a choice among the follow-ups given, the order turned by `turn` places.
        private static int At(StreetAsk ask, int turn, StreetAsk a, StreetAsk b = StreetAsk.Count, StreetAsk c = StreetAsk.Count, StreetAsk d = StreetAsk.Count)
        {
            int n = b == StreetAsk.Count ? 1 : c == StreetAsk.Count ? 2 : d == StreetAsk.Count ? 3 : 4;
            int at = ask == a ? 0 : ask == b ? 1 : ask == c ? 2 : ask == d ? 3 : -1;
            return at < 0 ? -1 : (at + turn) % n;
        }

        /// <summary>True for the lines after which the player can say thanks.</summary>
        public static bool Helped(StreetLine line)
        {
            return line == StreetLine.DirFound || line == StreetLine.DirFar || line == StreetLine.DirNone || line == StreetLine.AreaPlaces || line == StreetLine.AreaEvent
                   || line == StreetLine.AreaNews || line == StreetLine.AreaNothing;
        }

        public static bool Offered(StreetChat c, StreetFacts f, StreetAsk ask)
        {
            StreetMemo m = c.Memo;
            bool done = c.Done[(int)ask];
            switch (ask)
            {
                case StreetAsk.SmallTalk:
                case StreetAsk.AboutThem:
                case StreetAsk.Area:
                case StreetAsk.Economy:
                case StreetAsk.Day:
                    return !done;
                case StreetAsk.Event: return !done && !string.IsNullOrEmpty(f.EventToday);
                case StreetAsk.Work: return !done && c.Shared;
                case StreetAsk.Thanks: return Helped(c.Last);
                case StreetAsk.Bye: return true;
                case StreetAsk.Name: return !done && (m == null || !m.Named);
                case StreetAsk.Directions: return true;
                case StreetAsk.Compliment: return !done && (m == null || !m.Complimented);
                case StreetAsk.Joke: return !done && (m == null || !m.Joked);
                case StreetAsk.Flirt: return !done && (m == null || !m.Flirted);
                case StreetAsk.Number: return !done && c.Turns >= 1 && f.SocialOn && (m == null || !m.Swapped);
                case StreetAsk.Invite: return !done && c.Turns >= 1 && f.SocialOn && (m == null || !m.Invited);
                case StreetAsk.Election: return !done && f.Election;
                case StreetAsk.Business: return !done && f.Shops.Count > 0 && (m == null || !m.Mentioned);
                case StreetAsk.Rude: return !done;
                default: return false;
            }
        }

        /// <summary>All that counts for how a thing said lands: this talk, today's memory and their warmth.</summary>
        public static int Score(StreetChat c) => c.Rapport + (c.Memo != null ? c.Memo.Rapport : 0) + c.Approach.Warmth;

        public const int WarmThanksLeast = -1;   // the talk's score at or above which thanks are taken warmly (and earn a point)

        public static bool WarmThanks(StreetChat c) => Score(c) >= WarmThanksLeast;

        /// <summary>What a choice comes to that needs nothing more of the game; <paramref name="roll"/> is the turn's own.</summary>
        public static StreetOutcome Answer(StreetChat c, StreetAsk ask, StreetFacts f, int roll)
        {
            StreetStranger s = c.Who;
            int score = Score(c);
            int luck = Roll(roll, 30, 3) - 1;   // -1, 0 or 1: how it lands this time
            var o = new StreetOutcome();
            switch (ask)
            {
                case StreetAsk.SmallTalk:
                    o.Line = score >= 0 ? StreetLine.SmallWarm : StreetLine.SmallCool;
                    o.Rapport = score >= 0 ? 1 : 0;
                    break;
                case StreetAsk.AboutThem:
                    o.Line = score < -1 ? StreetLine.AboutCool : s.Job == SocialCatalogue.Jobs.Length - 1 ? StreetLine.AboutRetired : StreetLine.AboutJob;
                    o.Rapport = score < -1 ? 0 : 1;
                    break;
                case StreetAsk.Name:
                    o.Line = c.Memo != null && c.Memo.Named ? StreetLine.NameKnown : score >= 0 ? StreetLine.NameGive : StreetLine.NameWithhold;
                    o.Rapport = o.Line == StreetLine.NameGive ? 1 : 0;
                    break;
                case StreetAsk.Compliment:
                    o.Line = score + luck >= 0 ? StreetLine.ComplimentYes : StreetLine.ComplimentNo;
                    o.Rapport = o.Line == StreetLine.ComplimentYes ? 1 : -1;
                    break;
                case StreetAsk.Joke:
                    o.Line = score + luck >= (s.Humour ? -1 : 1) ? StreetLine.JokeYes : StreetLine.JokeNo;
                    o.Rapport = o.Line == StreetLine.JokeYes ? 1 : -1;
                    break;
                case StreetAsk.Flirt:
                    o.Line = s.Taken ? StreetLine.FlirtTaken : s.Compatible && score + luck >= 1 ? StreetLine.FlirtYes : StreetLine.FlirtNo;
                    o.Rapport = o.Line == StreetLine.FlirtYes ? 2 : o.Line == StreetLine.FlirtTaken ? 0 : -1;
                    break;
                case StreetAsk.Election:
                    o.Line = !f.Election || f.Candidates.Count == 0 ? StreetLine.ElectionNone : Roll(s.Seed, 31, 100) < 35 ? StreetLine.ElectionUnsure : StreetLine.ElectionFor;
                    break;
                case StreetAsk.Economy:
                    o.Line = f.PriceTrend > 0 || f.RateMove > 0 ? StreetLine.EconomyUp : f.PriceTrend < 0 || f.RateMove < 0 ? StreetLine.EconomyDown : StreetLine.EconomyFlat;
                    break;
                case StreetAsk.Rude:
                    o.Rapport = -3;
                    o.Ends = score - 3 <= -2 || (c.Memo != null && c.Memo.Rude);
                    o.Line = o.Ends ? StreetLine.WalkOff : StreetLine.RudeCold;
                    break;
                case StreetAsk.Day:
                    o.Line = s.Mood >= 1 ? StreetLine.DayGood : s.Mood <= -1 || score < -1 ? StreetLine.DayLow : StreetLine.DayFine;
                    o.Rapport = score >= -1 ? 1 : 0;   // most people like being asked how they are
                    break;
                case StreetAsk.Event:
                    o.Line = score + luck >= 0 ? StreetLine.EventGoing : StreetLine.EventNot;
                    o.Rapport = o.Line == StreetLine.EventGoing ? 1 : 0;
                    break;
                case StreetAsk.Work:
                    o.Line = s.Job == SocialCatalogue.Jobs.Length - 1 ? StreetLine.WorkRetired : Roll(s.Seed, 35, 100) < LikesWorkPercent + s.Mood * 10 ? StreetLine.WorkLikes : StreetLine.WorkMixed;
                    o.Rapport = 1;
                    break;
                case StreetAsk.Thanks:
                    o.Line = StreetLine.Welcome;
                    o.Rapport = score >= WarmThanksLeast ? 1 : 0;
                    break;
                case StreetAsk.Bye:
                    o.Line = score >= 0 ? StreetLine.Bye : StreetLine.ByeCool;
                    o.Ends = true;
                    break;
                default:
                    o.Line = score >= 0 ? StreetLine.SaidWarm : StreetLine.SaidCool;
                    break;
            }
            return o;
        }

        /// <summary>The candidate somebody votes for, by their seed; -1 with none.</summary>
        public static int Candidate(StreetStranger s, int candidates) => candidates > 0 ? Roll(s.Seed, 32, candidates) : -1;

        /// <summary>A threat: they go at once, and an officer near enough hears it.</summary>
        public static StreetOutcome Threat(StreetFacts f)
        {
            return new StreetOutcome { Line = StreetLine.ThreatLeave, Rapport = -6, Ends = true, Police = f.PoliceNear };
        }

        /// <summary>What the player's own words come to when they are no question of the rules'.</summary>
        public static StreetOutcome Said(StreetChat c, StreetSayKind kind)
        {
            int score = Score(c);
            var o = new StreetOutcome();
            switch (kind)
            {
                case StreetSayKind.Nonsense:
                    o.Line = StreetLine.SaidOdd;
                    break;
                case StreetSayKind.Goodbye:
                    o.Line = score >= 0 ? StreetLine.Bye : StreetLine.ByeCool;
                    o.Ends = true;
                    break;
                case StreetSayKind.Greeting:
                    o.Line = score >= -1 ? StreetLine.GreetBack : StreetLine.SaidCool;
                    o.Rapport = score >= -1 ? 1 : 0;
                    break;
                default:
                    o.Line = score >= 0 ? StreetLine.SaidWarm : StreetLine.SaidCool;
                    break;
            }
            return o;
        }

        /// <summary>
        /// Whether they give their number: a good enough talk, and never when the social life is off or the player's life has
        /// no room for anybody new. That last says so (NumberFull), so the player knows a talk that went well was not the trouble.
        /// </summary>
        public static StreetLine Number(StreetChat c, StreetFacts f, bool nobodyNew)
        {
            if (c.Memo != null && c.Memo.Swapped)
            {
                return StreetLine.NumberAlready;
            }
            int score = Score(c);
            bool yes = score >= NumberAt || (f.Standing >= LikedFrom && score >= NumberLiked);
            if (yes && !f.SocialOn)
            {
                return StreetLine.NumberLater;
            }
            if (yes && nobodyNew)
            {
                return StreetLine.NumberFull;
            }
            if (yes)
            {
                return StreetLine.NumberYes;
            }
            return c.Who.Taken && c.Memo != null && c.Memo.Flirted ? StreetLine.NumberTaken : score <= 0 ? StreetLine.NumberNo : StreetLine.NumberLater;
        }

        /// <summary>An invitation to be decided as Flare decides one, the talk standing in for the messages exchanged.</summary>
        public static FlareInviteAsk Invite(StreetChat c, StreetFacts f, bool flirted, long now)
        {
            StreetStranger s = c.Who;
            int score = Score(c);
            return new FlareInviteAsk
            {
                Kind = flirted ? PlanKind.Meal : PlanKind.Coffee, First = s.First, Last = s.Last, Sex = s.Female ? 1 : 0, Age = s.Age, Hour = f.Hour,
                NightOwl = (s.Traits & SocialTrait.NightOwl) != 0, Grumpy = s.Mood <= -2 || Styles[s.Style] == "gruff", Taken = s.Taken,
                Chats = score >= NumberAt ? FlareInviteRules.ChatsForYes : score >= 1 ? 1 : 0, Standing = f.Standing, Reputation = f.Standing,
                Roll = Roll(s.Seed, 33, 1000), From = now + InviteFrom
            };
        }

        public const long InviteFrom = 45;   // game minutes: a plan made face to face starts at least this long from now

        /// <summary>The line of an invitation's answer, by the game's decision and its reason.</summary>
        public static StreetLine InviteLine(FlareInviteAnswer answer, FlareInviteWhy why)
        {
            if (answer == FlareInviteAnswer.Yes)
            {
                return StreetLine.InviteYes;
            }
            switch (why)
            {
                case FlareInviteWhy.Taken: return StreetLine.InviteNoTaken;
                case FlareInviteWhy.Dislikes: return StreetLine.InviteNoDislikes;
                case FlareInviteWhy.Plan: return StreetLine.InviteMaybePlan;
                case FlareInviteWhy.Busy: return StreetLine.InviteMaybeBusy;
                case FlareInviteWhy.Night: return StreetLine.InviteMaybeNight;
                case FlareInviteWhy.Later: return StreetLine.InviteMaybeLater;
                case FlareInviteWhy.Nowhere: return StreetLine.InviteMaybeNowhere;
                case FlareInviteWhy.Stranger: return StreetLine.InviteMaybeStranger;
                default: return answer == FlareInviteAnswer.No ? StreetLine.InviteNo : StreetLine.InviteMaybeStranger;
            }
        }

        /// <summary>The line about the player's business: heard of it, will go (word of mouth), or not interested.</summary>
        public static StreetLine Business(StreetChat c, int roll)
        {
            if (c.Memo != null && c.Memo.Mentioned)
            {
                return StreetLine.BusinessAgain;
            }
            int score = Score(c) + Roll(roll, 34, 3) - 1;
            return c.Approach.Recognised && score >= 0 ? StreetLine.BusinessHeard : score >= 1 ? StreetLine.BusinessWill : StreetLine.BusinessNo;
        }

        /// <summary>True for the business lines that count as word of mouth.</summary>
        public static bool Told(StreetLine line) => line == StreetLine.BusinessHeard || line == StreetLine.BusinessWill;

        /// <summary>Which way a place is, from degrees clockwise off the player's facing: 0 ahead, 1 right, 2 behind, 3 left.</summary>
        public static int Way(float degrees)
        {
            float d = degrees % 360f;
            if (d < 0f)
            {
                d += 360f;
            }
            return d < 45f || d >= 315f ? 0 : d < 135f ? 1 : d < 225f ? 2 : 3;
        }

        /// <summary>Minutes on foot, at least one.</summary>
        public static int Minutes(float metres) => Math.Max(1, (int)Math.Round(metres / WalkMetresPerMinute));

        public static StreetLine Directions(bool found, float metres) => !found ? StreetLine.DirNone : metres > FarMetres ? StreetLine.DirFar : StreetLine.DirFound;

        /// <summary>True if a business of this type ("ba:businesstype_...") is a place of this kind.</summary>
        public static bool IsPlace(string businessType, StreetPlace place)
        {
            if (string.IsNullOrEmpty(businessType) || place < 0 || place >= StreetPlace.Count)
            {
                return false;
            }
            string[] parts = PlaceTypes[(int)place];
            for (int i = 0; i < parts.Length; i++)
            {
                if (businessType.IndexOf(parts[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Adds what an ended conversation did to the day's memory of the person, within bounds.</summary>
        public static void Close(StreetChat c)
        {
            StreetMemo m = c.Memo;
            if (m == null)
            {
                return;
            }
            m.Talks++;
            m.Rapport = Math.Max(-RapportMost, Math.Min(RapportMost, m.Rapport + c.Rapport));
        }

        /// <summary>The turns they give the player in all, earned ones included, <see cref="PatienceMost"/> at most.</summary>
        public static int Turns(StreetChat c) => Math.Min(PatienceMost, c.Approach.Patience + c.Earned);

        public static bool Spent(StreetChat c) => c.Turns >= Turns(c);

        /// <summary>Which remark of small talk suits the moment (into StreetLines.Small): the rain, the night, else one by the person's seed.</summary>
        public static int SmallTopic(StreetStranger s, StreetFacts f)
        {
            if (f.Raining)
            {
                return SmallRain;
            }
            if (Night(f.Hour) || f.Hour >= 20)
            {
                return SmallNight;
            }
            int hour = f.Hour >= 6 && f.Hour < 11 ? SmallMorning : f.Hour >= 17 ? SmallEvening : SmallAny;
            int day = f.Weekend && f.Hour >= 10 && f.Hour < 18 ? SmallWeekend : RushHour(f.Hour, f.Weekend) ? SmallBusy : SmallAny;
            int n = 1 + (hour != SmallAny ? 1 : 0) + (day != SmallAny ? 1 : 0);   // any day's remark is always one of them
            int pick = Roll(s.Seed, 41, n);
            return pick == 0 ? SmallAny : pick == 1 ? (hour != SmallAny ? hour : day) : day;
        }

        public const int SmallAny = 0, SmallRain = 1, SmallNight = 2, SmallMorning = 3, SmallEvening = 4, SmallWeekend = 5, SmallBusy = 6;   // by StreetLines.Small
        public const int LikesWorkPercent = 55;   // of people asked, who like what they do on a fine day; ten more or less a step of mood
    }
}
