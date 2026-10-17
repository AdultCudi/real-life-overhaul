using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Street
{
    /// <summary>
    /// The rules of talking to people in the street, every figure in one place: who somebody is (from their seed), whether
    /// they stop for the player and why not, how warm they are, how many turns they give, what each thing the player says
    /// comes to, numbers and invitations (decided as an invitation on Flare is: FlareInviteRules.Decide), word of mouth for
    /// the player's business (capped), what is offered next. The model never decides any of it: it only words the line the
    /// rules chose. Game-free (EconomyCheck, StreetTalkChecks.cs).
    /// COST: arithmetic and a few short lists, on a press or a click; nothing per frame.
    /// </summary>
    public static class StreetRules
    {
        // ---------------------------------------------------------------- the street
        public const float Reach = 3.2f;          // metres: somebody this near and in front of the player can be talked to (a step and a half: talking distance)
        public const float LetGo = 4.0f;          // metres: the one offered keeps the offer until this far (a step of give, so it does not flicker)
        public const float Facing = 0.5f;         // the cosine of the widest angle off the player's facing a new person may stand at (60 degrees)
        public const float KeepFacing = 0.17f;    // ...and the one offered keeps it out to about 80 degrees (a passer-by drifting to the side)
        public const float Dwell = 0.3f;          // real seconds the same person must have been in front before Talk is offered (not everybody brushed past)
        public const float Grace = 1.5f;          // real seconds the offer stays on somebody who has just left the reach or the angle (a passer-by can be caught)
        public const float GraceMetres = 4.2f;    // ...as long as they are no further than this (under WalkAway: a press then still holds them)
        public const float Tick = 0.2f;           // real seconds between two looks for somebody to talk to, on foot out of doors
        public const float Height = 1.5f;         // metres up or down somebody may stand from the player (not on a balcony, not under a bridge)
        public const float WalkAway = 4.5f;       // metres: the player this far from them ends the conversation
        public const float PoliceMetres = 35f;    // an officer this near hears a threat

        // ---------------------------------------------------------------- timing (a spoken line must appear fast)
        public const double Hold = 1.1;           // real seconds the most a line waits for the model's words (then the written line)
        public const float Beat = 0.45f;          // real seconds of turning to the player before the first line, when nothing is waited for
        public const float TurnSeconds = 0.6f;    // how long they take to turn to the player (the game's own turn)
        public const double GuessSeconds = 12.0;  // real seconds the line guessed while the player looks at someone is kept for the press

        // ---------------------------------------------------------------- people
        public const int MemoryMost = 48;         // people remembered today at most (the oldest go first)
        public const int PatienceLeast = 4;       // turns anybody who stops gives the player (three was the same three questions and gone)
        public const int PatienceMost = 10;       // ...at most, earned turns included: somebody warm and chatty
        public const int EarnedMost = 3;          // turns a conversation earns back at most, one for each good exchange
        public const int CoolestStranger = -1;    // warmth: how cool a stranger is at worst, unless the player was rude, they know a bad name or record, or it is night
        public const int InfamousBelow = 40;      // public standing: below this, somebody who knows the player's face has heard bad things
        public const int RapportMost = 10;
        public const int NumberAt = 3;            // rapport (today's and this talk's, with their warmth) at which a number is given
        public const int NumberLiked = 1;         // ...enough when the city thinks well of the player
        public const int LikedFrom = 70;          // public standing: the law's "liked" (Reputation.LikedFrom)
        public const int DislikedBelow = 30;      // ...below this the city thinks badly of the player
        public const int RecogniseMost = 90;      // in hundredths: at full recognition nine in ten know the player's face
        public const int CompatiblePercent = 45;  // of adults 21 to 60, a romance would be possible
        public const int TakenPercent = 30;       // with somebody else
        public const int CommuterPercent = 45;    // on their way to work at the rush hours
        public const int HumourPercent = 60;
        public const int ChattyPercent = 30;
        public const int RetiredFrom = 66;        // years

        // ---------------------------------------------------------------- brush-offs, in thousandths before the option's factor
        public const int HurryOdds = 110;         // anybody, any time: somewhere to be
        public const int WorkOdds = 600;          // a commuter at the rush hours
        public const int NightOdds = 450;         // a stranger in the small hours
        public const int RainOdds = 280;          // in the rain
        public const int MoodOdds = 450;          // on a rough day
        public const int UnwashedOdds = 350;      // the player has not washed
        public const int RecordOdds = 500;        // they know the player's face and their record
        public const int CallOdds = 400;          // on a call (the game's own figure has the phone at its ear)
        public const int TextingOdds = 150;       // looking at their phone
        public const int OddsMost = 950;
        public static readonly float[] BrushFactor = { 0.5f, 1f, 1.7f };   // the option: rarely, sometimes, often

        // ---------------------------------------------------------------- word of mouth (the player's business)
        public const int MouthPercent = 1;        // per cent more customers for each person who says they will go
        public const int MouthMost = 3;           // per cent at most for one business, however many are told
        public const int MouthDays = 2;           // game days it lasts from the day it was told
        public const int MouthPerDay = 3;         // people told a day that count, for all the player's businesses

        // ---------------------------------------------------------------- the way
        public const float WalkMetresPerMinute = 75f;   // a walk, to say how far
        public const float FarMetres = 900f;            // further than this is "a fair way"
        public const float FindMetres = 1600f;          // a place is looked for this far at most

        /// <summary>How people talk (the card's style line): plain spoken ways, never the written ones (lowercase, emoji).</summary>
        public static readonly string[] Styles = { "warm", "dry", "blunt", "shy", "cheeky", "brief", "chatty", "formal", "playful", "gruff" };

        // ---------------------------------------------------------------- who they are

        /// <summary>A number from a seed and a slot, 0 to n-1: the same every time for the same person and purpose.</summary>
        public static int Roll(int seed, int slot, int n)
        {
            if (n <= 1)
            {
                return 0;
            }
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)(slot + 0x5bd1) * 2246822519u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (int)(h % (uint)n);
            }
        }

        /// <summary>The seed of somebody: the save's own number, the day and the game's figure. The same all day.</summary>
        public static int Seed(int save, int day, int key)
        {
            unchecked
            {
                return (int)((uint)save * 73856093u ^ (uint)day * 19349663u ^ (uint)key * 83492791u) & 0x7fffffff;
            }
        }

        /// <summary>
        /// Who somebody is, from their seed and what the game shows of them (sex and age): a name of the cast's lists, a job
        /// (retired from 66), the district they live in (the one they were met in more often than not), two traits that go
        /// together, today's mood, how they talk, and the parts of their own life the rules ask about.
        /// </summary>
        public static StreetStranger Identity(int save, int day, int key, bool female, int age, int districts, int metIn)
        {
            int seed = Seed(save, day, key);
            int life = Seed(save, 0, key);   // (what does not change from one day to the next while the figure stays: name, job, home)
            var s = new StreetStranger { Key = key, Seed = seed, Female = female, Age = Math.Max(18, Math.Min(85, age)) };
            string[] firsts = female ? SocialCatalogue.Women : SocialCatalogue.Men;
            s.First = firsts[Roll(life, 1, firsts.Length)];
            s.Last = SocialCatalogue.Surnames[Roll(life, 2, SocialCatalogue.Surnames.Length)];
            s.Job = s.Age >= RetiredFrom ? SocialCatalogue.Jobs.Length - 1 : Roll(life, 3, SocialCatalogue.Jobs.Length - 1);
            int n = Math.Max(1, districts);
            s.District = metIn >= 0 && metIn < n && Roll(life, 4, 100) < 60 ? metIn : Roll(life, 5, n);
            SocialTrait[] pair = SocialCatalogue.TraitPairs[Roll(life, 6, SocialCatalogue.TraitPairs.Length)];
            s.Traits = pair[0] | pair[1];
            s.Mood = Roll(seed, 7, 5) - 2;
            s.Style = Roll(life, 8, Styles.Length);
            s.Compatible = s.Age >= 21 && s.Age <= 60 && Roll(life, 9, 100) < CompatiblePercent;
            s.Taken = Roll(life, 10, 100) < TakenPercent;
            s.Commuter = s.Job != SocialCatalogue.Jobs.Length - 1 && Roll(seed, 11, 100) < CommuterPercent;
            s.Humour = Roll(life, 12, 100) < HumourPercent;
            s.Chatty = Roll(life, 13, 100) < ChattyPercent;
            s.Handle = "@" + s.First.ToLowerInvariant() + (10 + Roll(life, 14, 90)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return s;
        }

        // ---------------------------------------------------------------- the approach

        public static bool Night(int hour) => hour >= 23 || hour < 6;

        public static bool RushHour(int hour, bool weekend) => !weekend && ((hour >= 7 && hour < 10) || (hour >= 17 && hour < 19));

        /// <summary>
        /// Whether they stop for the player, and how they take them: today's mood and memory of the player, how the city sees
        /// the player (public standing; a face they know, the record behind it; not washed), the hour and the weather. A roll
        /// of the person's seed and the conversation of the day, so asking twice in a row does not reroll; the option sets
        /// how often anybody has no time (rarely, sometimes, often).
        /// </summary>
        /// <param name="phone">What the game's figure is doing with its phone: 0 nothing, 1 texting, 2 on a call.</param>
        public static StreetApproach Approach(StreetStranger s, StreetMemo memo, StreetFacts f, int option, int phone)
        {
            var a = new StreetApproach();
            int talks = memo != null ? memo.Talks : 0;
            int roll = Seed(s.Seed, f.Hour, talks);
            a.Recognised = f.Recognition > 0 && Roll(roll, 20, 100) < Math.Min(RecogniseMost, f.Recognition);
            a.Famed = a.Recognised && (f.Standing >= Reputation55 || f.Standing < InfamousBelow);   // (the famous and the infamous greeting's: they know the name too)
            a.Warmth = Warmth(s, memo, f, a.Recognised);
            a.Brush = Brush(s, memo, f, a.Recognised, a.Warmth, roll, option, phone);
            int patience = PatienceLeast + Math.Max(0, a.Warmth) + (a.Warmth >= 2 ? 1 : 0) + (s.Chatty ? 2 : 0) - (s.Commuter && RushHour(f.Hour, f.Weekend) ? 1 : 0);
            a.Patience = Math.Max(PatienceLeast, Math.Min(PatienceMost, patience));
            a.Opening = memo != null && memo.Talks > 0 && memo.Rapport >= 0 ? StreetLine.OpenAgain
                : a.Recognised && f.Standing >= Reputation55 ? StreetLine.OpenFamous
                : a.Recognised && f.Standing < InfamousBelow ? StreetLine.OpenInfamous
                : a.Warmth >= 1 ? StreetLine.OpenWarm
                : a.Warmth <= -1 ? StreetLine.OpenCool
                : StreetLine.OpenFine;
            return a;
        }

        private const int Reputation55 = 55;   // the newcomer's public standing (Reputation.Start)

        /// <summary>
        /// -3 to 3: their mood today with what they make of the player. However the rest adds up (a rough day, a player who
        /// has not washed, one the city thinks badly of), a stranger is at worst <see cref="CoolestStranger"/>: cooler only
        /// when the player was rude to them or the talk went badly today, they know the player's face and its bad name or
        /// record, or it is night.
        /// </summary>
        public static int Warmth(StreetStranger s, StreetMemo memo, StreetFacts f, bool recognised)
        {
            int w = s.Mood;
            w += f.Standing >= LikedFrom ? 1 : f.Standing < DislikedBelow ? -1 : 0;
            if (recognised)
            {
                w += f.Standing >= Reputation55 ? 1 : -1;
                w -= f.Record ? 1 : 0;
            }
            w -= f.Unwashed ? 1 : 0;
            w -= Night(f.Hour) ? 1 : 0;
            if (memo != null)
            {
                w += memo.Rapport >= 2 ? 1 : memo.Rapport <= -2 ? -1 : 0;
            }
            bool reason = Night(f.Hour) || (recognised && (f.Record || f.Standing < InfamousBelow)) || (memo != null && (memo.Rude || memo.Rapport <= -2));
            return Math.Max(reason ? -3 : CoolestStranger, Math.Min(3, w));
        }

        private static StreetBrush Brush(StreetStranger s, StreetMemo memo, StreetFacts f, bool recognised, int warmth, int roll, int option, int phone)
        {
            if (memo != null && memo.Rude)
            {
                return StreetBrush.Rude;   // (they remember: not today)
            }
            if (recognised && f.Wanted)
            {
                return StreetBrush.Wanted;
            }
            if (memo != null && memo.Talks > 0 && memo.Rapport >= 1)
            {
                return StreetBrush.None;   // (they liked talking to the player earlier today)
            }
            float k = BrushFactor[Math.Max(0, Math.Min(BrushFactor.Length - 1, option))];
            if (recognised && f.Record && Roll(roll, 21, 1000) < Odds(RecordOdds, k))
            {
                return StreetBrush.Record;
            }
            if (f.Unwashed && Roll(roll, 22, 1000) < Odds(UnwashedOdds, k))
            {
                return StreetBrush.Unwashed;
            }
            if (s.Commuter && RushHour(f.Hour, f.Weekend) && Roll(roll, 23, 1000) < Odds(WorkOdds, k))
            {
                return StreetBrush.Work;
            }
            if (Night(f.Hour) && warmth <= 0 && Roll(roll, 24, 1000) < Odds(NightOdds, k))
            {
                return StreetBrush.Night;
            }
            if (f.Raining && Roll(roll, 25, 1000) < Odds(RainOdds, k))
            {
                return StreetBrush.Rain;
            }
            if (s.Mood <= -2 && Roll(roll, 26, 1000) < Odds(MoodOdds, k))
            {
                return StreetBrush.Mood;
            }
            if (phone > 0 && Roll(roll, 27, 1000) < Odds(phone >= 2 ? CallOdds : TextingOdds, k))
            {
                return StreetBrush.Phone;
            }
            if (Roll(roll, 28, 1000) < Odds(HurryOdds, k))
            {
                return StreetBrush.Hurry;
            }
            return StreetBrush.None;
        }

        private static int Odds(int odds, float factor) => Math.Min(OddsMost, (int)(odds * factor));

        /// <summary>The line of a brush-off.</summary>
        public static StreetLine BrushLine(StreetBrush b)
        {
            switch (b)
            {
                case StreetBrush.Work: return StreetLine.BrushWork;
                case StreetBrush.Night: return StreetLine.BrushNight;
                case StreetBrush.Rain: return StreetLine.BrushRain;
                case StreetBrush.Mood: return StreetLine.BrushMood;
                case StreetBrush.Rude: return StreetLine.BrushRude;
                case StreetBrush.Unwashed: return StreetLine.BrushUnwashed;
                case StreetBrush.Record: return StreetLine.BrushRecord;
                case StreetBrush.Wanted: return StreetLine.BrushWanted;
                case StreetBrush.Phone: return StreetLine.BrushPhone;
                default: return StreetLine.BrushHurry;
            }
        }

        // ---------------------------------------------------------------- what is offered

        // Where a choice stands in the round, before the person's own turn of the order: what follows from their last line,
        // the number and the invitation once they are warm, what suits the moment, the rest, goodbye, and rudeness last.
        private const int TierFollow = 0, TierWarm = 1, TierMoment = 2, TierRest = 3, TierBye = 4, TierRude = 5;
        private const int TierSize = 10000;       // a tier's span of ranks
        private const int FitSize = 1000;         // ...and how well a choice suits the moment, within it (0 best)

        /// <summary>
        /// The things the player can say now, in the order "Something else" goes through them (the first is the panel's blue
        /// button). Nothing already done this conversation (or today, for their name, a compliment, a joke, a flirt, their
        /// number, an invitation and the business) is offered again. The order comes from the moment and the person, never
        /// from a fixed list:
        ///   follow-ups   what follows from what they just said, in its own order (<see cref="FollowUp"/>): after what they
        ///                do, whether they like it; after the way somewhere, thanks; after a compliment that landed, their
        ///                name and their number
        ///   warm         their number and a coffee, once they warm to the player
        ///   the moment   how their day is going (first when their mood shows), the weather or the hour (first in the rain,
        ///                at night and at the rush hours), the street event of the day; on the first turn also what they do,
        ///                the way somewhere, what is good around here or a compliment
        ///   the rest     their name (after a first exchange), the area, the way somewhere, a compliment, a joke, a flirt, the
        ///                election while one is fought, prices, the player's business when they have one
        ///   then         goodbye, and rudeness last
        /// Within each, the person's seed sets the order, so two people do not open the same way. The player's own words are
        /// not a choice of the round: they are the panel's grey button (StreetDirector.Talk.cs).
        /// COST: a pass over the choices and an insertion into a list of at most twenty, on a click; nothing allocated.
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
            int turn = Roll(c.Who.Seed, 300 + (int)ask, FitSize);   // (the person's own order: the same all day for them, different from the next person's)
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
                    return TierMoment * TierSize + (c.Who.Mood == 2 || c.Who.Mood == -2 ? 0 : 1) * FitSize + turn;   // (first when their mood shows: a great day or a rough one)
                case StreetAsk.SmallTalk:
                    return TierMoment * TierSize + (f.Raining || Night(f.Hour) || RushHour(f.Hour, f.Weekend) ? 0 : 1) * FitSize + turn;
                case StreetAsk.Event:
                    return TierMoment * TierSize + turn;
                case StreetAsk.AboutThem:
                case StreetAsk.Directions:
                case StreetAsk.Area:
                case StreetAsk.Compliment:
                    return (c.Turns == 0 ? TierMoment : TierRest) * TierSize + FitSize + turn;   // (an opening a stranger is often met with; later, one of the rest)
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

        /// <summary>
        /// What follows from the line they said last: the choice's place among its follow-ups (0 first), or -1 when it is
        /// none of them. Where one follow-up plainly comes first (whether they like their work after what they do, thanks
        /// after the way, their name after a compliment that landed, a coffee after their number) the order is fixed; where
        /// several would do, the person's seed turns it (<paramref name="seed"/>), so two talks do not run the same way. A
        /// follow-up is offered first only if it can be said (<see cref="Offered"/>).
        /// </summary>
        public static int FollowUp(StreetLine last, StreetAsk ask, int seed)
        {
            int turn = Roll(seed, 400 + (int)last, 12);   // (a turn of the order where several follow-ups would do)
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

        // The place of a choice among the follow-ups a, b, c, d (as many as are given), the order turned by `turn` places.
        private static int At(StreetAsk ask, int turn, StreetAsk a, StreetAsk b = StreetAsk.Count, StreetAsk c = StreetAsk.Count, StreetAsk d = StreetAsk.Count)
        {
            int n = b == StreetAsk.Count ? 1 : c == StreetAsk.Count ? 2 : d == StreetAsk.Count ? 3 : 4;
            int at = ask == a ? 0 : ask == b ? 1 : ask == c ? 2 : ask == d ? 3 : -1;
            return at < 0 ? -1 : (at + turn) % n;
        }

        /// <summary>True for the lines after which the player can say thanks (the way somewhere, what is around).</summary>
        public static bool Helped(StreetLine line)
        {
            return line == StreetLine.DirFound || line == StreetLine.DirFar || line == StreetLine.DirNone || line == StreetLine.AreaPlaces || line == StreetLine.AreaEvent
                   || line == StreetLine.AreaNews || line == StreetLine.AreaNothing;
        }

        /// <summary>True when this can be said now (the player's own words: the panel's grey button, wherever the game has its text box).</summary>
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

        // ---------------------------------------------------------------- what it comes to

        /// <summary>All that counts for how a thing said lands: this talk, today's memory and their warmth.</summary>
        public static int Score(StreetChat c) => c.Rapport + (c.Memo != null ? c.Memo.Rapport : 0) + c.Approach.Warmth;

        public const int WarmThanksLeast = -1;   // the talk's score: at or above it thanks are taken warmly (and earn a point), below it coolly

        /// <summary>Whether thanks now are taken warmly: the talk so far friendly enough (the card's "the talk so far").</summary>
        public static bool WarmThanks(StreetChat c) => Score(c) >= WarmThanksLeast;

        /// <summary>
        /// What a choice comes to that needs nothing more of the game (the place, the number, the invitation and the area are
        /// worked out with it: <see cref="Directions"/>, <see cref="Number"/>, <see cref="Invite"/>, <see cref="Area"/>).
        /// The roll is the conversation's own (StreetRules.Seed of the person and the turn).
        /// </summary>
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
                    o.Rapport = score >= -1 ? 1 : 0;   // (asked how they are: most people like that)
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

        /// <summary>The candidate somebody votes for: one of those standing, by their seed.</summary>
        public static int Candidate(StreetStranger s, int candidates) => candidates > 0 ? Roll(s.Seed, 32, candidates) : -1;

        /// <summary>A threat: they go at once, and an officer near enough hears it.</summary>
        public static StreetOutcome Threat(StreetFacts f)
        {
            return new StreetOutcome { Line = StreetLine.ThreatLeave, Rapport = -6, Ends = true, Police = f.PoliceNear };
        }

        /// <summary>What the player's own words come to that are no question of the rules' (a greeting, thanks, anything else).</summary>
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
        /// Whether they give the player their number: a good talk today (rapport with their warmth at <see cref="NumberAt"/>,
        /// or <see cref="NumberLiked"/> when the city thinks well of the player), never when the social life cannot have
        /// somebody new (nobody of the cast is left to meet: "let's see if we bump into each other").
        /// </summary>
        public static StreetLine Number(StreetChat c, StreetFacts f, bool nobodyNew)
        {
            if (c.Memo != null && c.Memo.Swapped)
            {
                return StreetLine.NumberAlready;
            }
            int score = Score(c);
            bool yes = score >= NumberAt || (f.Standing >= LikedFrom && score >= NumberLiked);
            if (yes && (nobodyNew || !f.SocialOn))
            {
                return StreetLine.NumberLater;
            }
            if (yes)
            {
                return StreetLine.NumberYes;
            }
            return c.Who.Taken && c.Memo != null && c.Memo.Flirted ? StreetLine.NumberTaken : score <= 0 ? StreetLine.NumberNo : StreetLine.NumberLater;
        }

        /// <summary>
        /// An invitation as the game decides one on Flare (FlareInviteRules.Decide): the stranger's own life (seeing somebody,
        /// a night owl, grumpy on a rough day), how the talk went standing in for messages exchanged (a good talk counts as
        /// enough of them), the player's standing; the social life fills the rest (Wiring/StreetLinks, through Flare's own
        /// look: a plan on already, a place near, the first hour that suits). The kind: coffee, or dinner after a flirt that
        /// went well.
        /// </summary>
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

        public const long InviteFrom = 45;   // game minutes: a plan made face to face starts at least this long from now (the phone's own margin)

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

        /// <summary>
        /// The player's business: somebody who takes to it says they will go (word of mouth: <see cref="MouthPercent"/> more
        /// customers for <see cref="MouthDays"/> days, at most <see cref="MouthMost"/> for one business), somebody who knows
        /// the player's face has heard of it; a cool one is not interested.
        /// </summary>
        public static StreetLine Business(StreetChat c, int roll)
        {
            if (c.Memo != null && c.Memo.Mentioned)
            {
                return StreetLine.BusinessAgain;
            }
            int score = Score(c) + Roll(roll, 34, 3) - 1;
            return c.Approach.Recognised && score >= 0 ? StreetLine.BusinessHeard : score >= 1 ? StreetLine.BusinessWill : StreetLine.BusinessNo;
        }

        /// <summary>True for the lines of the business that tell somebody about it for good.</summary>
        public static bool Told(StreetLine line) => line == StreetLine.BusinessHeard || line == StreetLine.BusinessWill;

        // ---------------------------------------------------------------- the way

        /// <summary>Which way a place is from the player: 0 ahead, 1 to the right, 2 behind, 3 to the left (the angle in degrees off their facing, clockwise).</summary>
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

        /// <summary>The line of a place found (or not), by how far it is.</summary>
        public static StreetLine Directions(bool found, float metres) => !found ? StreetLine.DirNone : metres > FarMetres ? StreetLine.DirFar : StreetLine.DirFound;

        /// <summary>The kinds of business a place of each kind is (the game's business types, by a part of their names).</summary>
        public static readonly string[][] PlaceTypes =
        {
            new[] { "coffeeshop" },
            new[] { "restaurant" },
            new[] { "nightclub", "bar" },
            new[] { "supermarket", "fruitandvegetable", "grocery" },
            new[] { "clothingstore", "shoestore" },
            new[] { "gym" },
            new[] { "cinema", "theater" },
            new[] { "_bank" }
        };

        /// <summary>True if a business of this type (the game's "ba:businesstype_...") is a place of this kind.</summary>
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

        // ---------------------------------------------------------------- after

        /// <summary>A conversation is over: what it did is added to the day's memory of the person, within bounds.</summary>
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

        /// <summary>The turns they give the player in all: their patience and what good exchanges earned back, <see cref="PatienceMost"/> at most.</summary>
        public static int Turns(StreetChat c) => Math.Min(PatienceMost, c.Approach.Patience + c.Earned);

        /// <summary>True once they have given the player all the turns they will.</summary>
        public static bool Spent(StreetChat c) => c.Turns >= Turns(c);

        // ---------------------------------------------------------------- what the player says

        /// <summary>
        /// Which remark of small talk suits the moment (the button's words, StreetLines.Small): the rain, the night, or one of
        /// those that fit the hour (a morning, an evening, the weekend, the rush hour, any day) by the person's seed.
        /// </summary>
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
            int n = 1 + (hour != SmallAny ? 1 : 0) + (day != SmallAny ? 1 : 0);   // (any day's remark is always one of them)
            int pick = Roll(s.Seed, 41, n);
            return pick == 0 ? SmallAny : pick == 1 ? (hour != SmallAny ? hour : day) : day;
        }

        public const int SmallAny = 0, SmallRain = 1, SmallNight = 2, SmallMorning = 3, SmallEvening = 4, SmallWeekend = 5, SmallBusy = 6;   // by StreetLines.Small
        public const int LikesWorkPercent = 55;   // of people asked, who like what they do (on a fine day; ten more or less a step of mood)
    }

    /// <summary>
    /// The day's memory of who the player talked to: at most <see cref="StreetRules.MemoryMost"/> people (the one talked to
    /// longest ago goes first), all of it forgotten the next day. Game-free; nothing is saved (a load is a new crowd).
    /// COST: a look-up per conversation.
    /// </summary>
    public sealed class StreetMemory
    {
        private readonly Dictionary<int, StreetMemo> _memos = new Dictionary<int, StreetMemo>(StreetRules.MemoryMost);
        private readonly List<int> _order = new List<int>(StreetRules.MemoryMost);
        private int _day = -1;

        public int Count => _memos.Count;

        /// <summary>The memory of somebody today: null if they have not met the player today.</summary>
        public StreetMemo Find(int key, int day)
        {
            NewDay(day);
            return _memos.TryGetValue(key, out StreetMemo m) ? m : null;
        }

        /// <summary>The memory of somebody today, made when they first talk to the player.</summary>
        public StreetMemo Get(int key, int day)
        {
            NewDay(day);
            if (_memos.TryGetValue(key, out StreetMemo m))
            {
                _order.Remove(key);
                _order.Add(key);
                return m;
            }
            if (_memos.Count >= StreetRules.MemoryMost && _order.Count > 0)
            {
                _memos.Remove(_order[0]);
                _order.RemoveAt(0);
            }
            m = new StreetMemo { Key = key, Day = day };
            _memos[key] = m;
            _order.Add(key);
            return m;
        }

        public void Clear()
        {
            _memos.Clear();
            _order.Clear();
            _day = -1;
        }

        private void NewDay(int day)
        {
            if (day != _day)
            {
                _memos.Clear();
                _order.Clear();
                _day = day;
            }
        }
    }

    /// <summary>One business told about: by how much and until when (game days).</summary>
    public struct StreetTold
    {
        public string Address;     // the game's own "street~number"
        public int Day;            // the day it was told
        public int Percent;
    }

    /// <summary>
    /// Word of mouth for the player's businesses: who said they would go, per business and day, capped
    /// (<see cref="StreetRules.MouthMost"/> per cent for a business, <see cref="StreetRules.MouthPerDay"/> people a day that
    /// count), each lasting <see cref="StreetRules.MouthDays"/> days. Kept in the save (StreetStore). Game-free.
    /// </summary>
    public sealed class StreetMouth
    {
        public readonly List<StreetTold> Told = new List<StreetTold>(8);
        public int CountedDay = -1;
        public int Counted;

        /// <summary>Somebody will go to the business at this address: true if it counted (the day's few, under the cap).</summary>
        public bool Tell(string address, int day)
        {
            if (string.IsNullOrEmpty(address))
            {
                return false;
            }
            Forget(day);
            if (CountedDay != day)
            {
                CountedDay = day;
                Counted = 0;
            }
            if (Counted >= StreetRules.MouthPerDay || Percent(address, day) + StreetRules.MouthPercent > StreetRules.MouthMost)
            {
                return false;
            }
            Counted++;
            Told.Add(new StreetTold { Address = address, Day = day, Percent = StreetRules.MouthPercent });
            return true;
        }

        /// <summary>Per cent more customers at this address today: what was told within the last days, capped.</summary>
        public int Percent(string address, int day)
        {
            int sum = 0;
            for (int i = 0; i < Told.Count; i++)
            {
                StreetTold t = Told[i];
                if (t.Address == address && day >= t.Day && day - t.Day < StreetRules.MouthDays)
                {
                    sum += t.Percent;
                }
            }
            return Math.Min(StreetRules.MouthMost, sum);
        }

        /// <summary>What has run its days is dropped.</summary>
        public void Forget(int day)
        {
            for (int i = Told.Count - 1; i >= 0; i--)
            {
                if (day - Told[i].Day >= StreetRules.MouthDays || Told[i].Day > day)
                {
                    Told.RemoveAt(i);
                }
            }
        }
    }
}
