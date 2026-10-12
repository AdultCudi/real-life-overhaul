using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Roommates
{
    /// <summary>What somebody is like to live with. Each pair is one or the other or neither; never both.</summary>
    [Flags]
    public enum RoomTrait
    {
        None = 0,
        Tidy = 1,            // cleans the flat a little every day
        Messy = 2,           // leaves it dirtier
        Quiet = 4,           // never a noisy night
        Noisy = 8,           // music and visitors late
        EarlyBird = 16,      // in bed at ten, up at six
        NightOwl = 32,       // in bed at two, up at ten
        Generous = 64,       // pays for what they took, settles up when leaving
        Tight = 128,         // counts every dollar
        Reliable = 256,      // the rent is never late
        LateWithRent = 512,  // sometimes late or short
        Cooks = 1024,        // leaves a portion in the fridge some evenings
        RaidsFridge = 2048,  // helps themselves to the player's food
        Sociable = 4096,     // company at dinner, evenings out
        Private = 8192       // in their room from nine
    }

    /// <summary>Where a roommate is at an hour of a day.</summary>
    public enum RoomWhere { In, Asleep, Work, Out, InRoom }

    /// <summary>What a home offers somebody who moves in.</summary>
    public enum RoomKind { None, Sofa, Bed }

    public enum RentOutcome { Paid, Late, Short }

    /// <summary>Why an arrangement ended, or is ending.</summary>
    public enum EndWhy
    {
        None, PlayerNotice, NewJob, NewPartner, Row, PartnerMovedIn, HomeGivenUp, NoRoomAfterMove, NotCloseEnoughToMove, BecamePartner
    }

    /// <summary>Something that happened between the two of them: what it does to the relationship is in <see cref="RoommateRules.Effect"/>.</summary>
    public enum RoomMoment
    {
        DayTogether, Talk, WarmReply, ShortReply, SharedMeal, Lent, RefusedLoan, Repaid, NotRepaid, CoveredRent, Waited, Chased, ChaseRefused,
        Row, OverdueDay, NoisyNight, NoiseForgiven, AskedQuietly, RaidForgiven, AskedToStop, Thanked, GoodLuck, ToldToGo
    }

    /// <summary>What a day brings at home, decided once a day from the seed.</summary>
    [Flags]
    public enum DayEvents { None = 0, Noise = 1, Cooked = 2, Raid = 4, Tidied = 8, Mess = 16, Borrow = 32, Notice = 64 }

    /// <summary>What <see cref="RoommateRules.Day"/> needs to know of how things stand.</summary>
    public struct DayContext
    {
        public bool InAtNight;        // they were at home last night
        public bool QuietAsked;       // the player asked them to keep it down
        public bool RaidStopped;      // ...and to leave the food alone
        public int RaidsThisWeek;
        public bool Owes;             // rent is outstanding
        public bool HasLoan;          // they owe the player a loan
        public bool OnNotice;
        public int DaysIn;
        public float Affection, Trust;
        public int Level;
    }

    public struct RentResult
    {
        public RentOutcome Outcome;
        public int Paid;
        public int Owed;
        public int DueInDays;   // when the rest is promised; 0 when nothing is owed
    }

    /// <summary>
    /// Every rule and figure of living with a roommate, free of the game: who answers a notice, when they are in,
    /// what they pay and when it is late, what a day brings, what each thing does to the relationship, how much
    /// food they may take, what room a home has, and every way it ends. Deterministic: the same seed and day give
    /// the same answer, so a save loaded twice plays the same day twice. Checked offline (EconomyCheck, RoommateChecks.cs).
    /// Nothing here runs by itself; the director asks on a day, an hour or a button.
    /// </summary>
    public static class RoommateRules
    {
        // ---- finding one
        public const int CandidatesLeast = 2, CandidatesMost = 4;
        public const int AnswerHours = 36;          // game hours over which the answers to a notice come in ("a day or two")
        public const int AdDays = 5;                // days a notice stays up
        public static int AdFee => Prices.At(25);                // dollars: what putting the notice up costs
        public const int ShareLeast = 35, ShareMost = 55;   // per cent of the rent an applicant offers
        public const int SofaPct = 60;              // per cent of that for somebody who gets the sofa, not a bed
        public const float FriendAskTrust = 35f;    // a friend says yes from this much trust
        public const int FriendSharePct = 45;       // what a friend pays: this, five more if generous, five less if careful
        public const int JobCount = 15;             // the jobs of SocialCatalogue.Jobs a roommate can have (all but "retired", the last)

        // ---- money
        public const int RentEveryDays = 7;         // a rent day every so many days from the day they moved in
        public const int RentHour = 9;              // from this hour of a rent day
        public const int LateOddsLate = 3000;       // of 10000, somebody late with rent: nothing on the day
        public const int LateOddsShort = 2500;      // ...or part of it
        public const int PlainOddsLate = 600;       // somebody neither reliable nor late by nature
        public const int LowLevelOddsLate = 1000;   // added while the relationship is under LowLevel
        public const int SlipsMost = 2;             // times a promised day may pass before they pay whatever happens
        public const int SlipOdds = 3500;
        public const int SlipDays = 2;
        public const int ChaseOdds = 5500;          // somebody late by nature pays at once when chased, this often (plus the level, a point a point)
        public const int BorrowOdds = 400, BorrowOddsLate = 800;
        public const int BorrowDays = 5;            // a loan is promised back in so many days
        public static int FoodPrice => Prices.At(8);             // dollars: what they put down for something they took, when asked

        // ---- the home
        public const float TidyPerDay = 6f, MessPerDay = -5f, OneMorePerDay = -1f;   // points of a hundred of the home's tidiness
        public const int RaidsPerWeek = 2;
        public const int RaidLeaves = 2;            // units of food they never go below
        public const int NoiseOdds = 3000, NoiseOddsAsked = 1000, NoiseOddsOwl = 1000;
        public const int CookOdds = 3500, RaidOdds = 3000;
        public const float NoiseSleepHours = 0.5f;  // sleep owed after a noisy night
        public const int MealFill = 45;             // what a portion they leave fills, of the game's hundred

        // ---- the relationship (the person model's own three figures, 0 to 100; the level is their average)
        public const float StartFamiliarity = 15f, StartAffection = 25f, StartTrust = 25f;
        public const float FamiliarityByLiving = 60f;   // living together alone makes people this familiar and no more
        public const int LowLevel = 30, GoodLevel = 45, FriendLevel = 60;
        public const float RowAffection = 10f, RowTrust = 8f;   // under either, it ends in a row

        // ---- ending it
        public const int NoticeDays = 3;
        public const int MinStayDays = 10;          // nobody leaves of their own accord before this
        public const int LeaveOdds = 120, LeaveOddsLow = 800;   // of 10000 a day
        public const int StayOdds = 5000;           // asked to stay after giving notice, with the level at FriendLevel or more
        public const int RevealDays = 4;            // a hidden trait shows after every so many days together

        private static readonly RoomTrait[][] Pairs =
        {
            new[] { RoomTrait.Tidy, RoomTrait.Messy }, new[] { RoomTrait.Quiet, RoomTrait.Noisy }, new[] { RoomTrait.EarlyBird, RoomTrait.NightOwl },
            new[] { RoomTrait.Generous, RoomTrait.Tight }, new[] { RoomTrait.Reliable, RoomTrait.LateWithRent }, new[] { RoomTrait.Cooks, RoomTrait.RaidsFridge },
            new[] { RoomTrait.Sociable, RoomTrait.Private }
        };

        // Invented first names and surnames, dealt without replacement so that no two applicants to one notice share either.
        private static readonly string[] Women = { "Ada", "Bea", "Cora", "Della", "Esme", "Faye", "Gwen", "Hattie", "Iris", "Josie", "Kit", "Lena", "Mabel", "Nell", "Opal", "Posy" };
        private static readonly string[] Men = { "Abel", "Bram", "Cal", "Dov", "Ellis", "Finn", "Gus", "Hal", "Ivo", "Jem", "Kip", "Lew", "Milo", "Ned", "Otis", "Pip" };
        private static readonly string[] Surnames =
        {
            "Ashdown", "Birkett", "Calloway", "Dunmore", "Elwood", "Fairlie", "Garrow", "Hollis", "Inskip", "Jessop", "Kirkby", "Lorimer", "Maddox", "Northey", "Orrin", "Pruett"
        };

        // By job (SocialCatalogue.Jobs): the hour work starts and ends (past 24: into the small hours) and the days it is on (bit 0 Monday).
        // 0 0 0: shifts, three days round (an early, a late, a day off).
        private static readonly byte[] WorkFrom = { 6, 0, 8, 8, 14, 9, 17, 0, 9, 7, 9, 7, 12, 8, 10 };
        private static readonly byte[] WorkTo = { 13, 0, 16, 17, 23, 17, 26, 0, 17, 16, 17, 15, 20, 16, 19 };
        private static readonly byte[] WorkDays = { 0x3F, 0, 0x1F, 0x1F, 0x3E, 0x1F, 0x7C, 0, 0x1F, 0x1F, 0x1F, 0x3F, 0x3F, 0x3E, 0x1F };
        private static readonly int[] JobPay = { 110, 210, 180, 190, 170, 260, 150, 160, 230, 220, 140, 130, 170, 120, 200 };   // dollars a day: what they can afford

        // ---------------------------------------------------------------- chance

        /// <summary>A figure from 0 to 9999 that is always the same for the same three numbers.</summary>
        public static int Roll(int seed, int day, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)day * 2246822519u ^ (uint)salt * 3266489917u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (int)(h % 10000u);
            }
        }

        public static bool Has(RoomTrait traits, RoomTrait trait) => (traits & trait) != 0;

        /// <summary>How many traits a set holds.</summary>
        public static int Count(RoomTrait traits)
        {
            int count = 0;
            for (int bits = (int)traits; bits != 0; bits &= bits - 1)
            {
                count++;
            }
            return count;
        }

        /// <summary>True if no pair has both of its sides in the set.</summary>
        public static bool Consistent(RoomTrait traits)
        {
            for (int i = 0; i < Pairs.Length; i++)
            {
                if (Has(traits, Pairs[i][0]) && Has(traits, Pairs[i][1]))
                {
                    return false;
                }
            }
            return true;
        }

        // ---------------------------------------------------------------- who answers a notice

        /// <summary>
        /// The people who answer a notice: two to four, each with a name, a look, a job, two or three traits of which
        /// one shows at once, the share of the rent they offer and the hour their answer comes. The same for the same
        /// seed and notice number. <paramref name="stamp"/> is the day times 24 plus the hour the notice went up.
        /// </summary>
        public static void Candidates(int seed, int notice, int stamp, RoomKind room, List<Flatmate> into)
        {
            into.Clear();
            var random = new Random(unchecked(seed * 31 + notice * 7919));
            int count = CandidatesLeast + random.Next(CandidatesMost - CandidatesLeast + 1);
            int[] first = Shuffled(random, Women.Length);
            int[] last = Shuffled(random, Surnames.Length);
            for (int i = 0; i < count; i++)
            {
                var p = new SocialPerson { Index = -1, Origin = SocialOrigin.Neighbour, Seed = random.Next(1, int.MaxValue) };
                p.Female = random.Next(2) == 0;
                p.Name = (p.Female ? Women[first[i]] : Men[first[i]]) + " " + Surnames[last[i]];
                p.Age = 20 + random.Next(22);
                p.Job = random.Next(JobCount);
                int[] pairs = Shuffled(random, Pairs.Length);
                int traits = 2 + random.Next(2);
                RoomTrait set = RoomTrait.None;
                for (int t = 0; t < traits; t++)
                {
                    set |= Pairs[pairs[t]][random.Next(2)];
                }
                // Somebody whose work runs late keeps late hours whatever else they are.
                if (WorkTo[p.Job] > 24 && Has(set, RoomTrait.EarlyBird))
                {
                    set = (set & ~RoomTrait.EarlyBird) | RoomTrait.NightOwl;
                }
                Start(p, set, StartFamiliarity, StartAffection, StartTrust);
                int share = ShareLeast + random.Next(ShareMost - ShareLeast + 1);
                share += Has(set, RoomTrait.Generous) ? 5 : Has(set, RoomTrait.Tight) ? -5 : 0;
                share = Math.Min(share, Affordable(p.Job));
                into.Add(new Flatmate
                {
                    Person = p,
                    Traits = set,
                    Known = FirstKnown(set, random.Next(8)),
                    SharePct = SharePct(share, room),
                    Answers = stamp + 1 + (i == 0 ? random.Next(4) : random.Next(AnswerHours))   // the first within a few hours, so a notice is never met with silence
                });
            }
        }

        /// <summary>A friend as a roommate: traits read from what the player already knows of them, all of them known.</summary>
        public static Flatmate Friend(SocialPerson friend, RoomKind room)
        {
            RoomTrait set = RoomTrait.None;
            if (friend.Has(SocialTrait.NightOwl)) set |= RoomTrait.NightOwl;
            if (friend.Has(SocialTrait.Punctual)) set |= RoomTrait.EarlyBird;
            if (friend.Has(SocialTrait.Generous)) set |= RoomTrait.Generous;
            if (friend.Has(SocialTrait.Careful)) set |= RoomTrait.Tight;
            if (friend.Has(SocialTrait.Reliable)) set |= RoomTrait.Reliable;
            if (friend.Has(SocialTrait.Flaky)) set |= RoomTrait.LateWithRent;
            if (friend.Has(SocialTrait.Foodie)) set |= RoomTrait.Cooks;
            if (friend.Has(SocialTrait.Homebody)) set |= RoomTrait.Private;
            if (friend.Has(SocialTrait.Late)) set |= RoomTrait.Messy;
            if (friend.Has(SocialTrait.Sporty)) set |= RoomTrait.Tidy;
            if ((set & RoomTrait.NightOwl) != 0)
            {
                set &= ~RoomTrait.EarlyBird;
            }
            if ((set & RoomTrait.Tidy) != 0)
            {
                set &= ~RoomTrait.Messy;
            }
            var p = new SocialPerson
            {
                Index = friend.Index, Origin = friend.Origin, Seed = friend.Seed, Female = friend.Female, Name = friend.Name, Age = friend.Age,
                Job = Math.Max(0, Math.Min(JobCount - 1, friend.Job)), Traits = friend.Traits
            };
            Start(p, set, Math.Max(StartFamiliarity, friend.Familiarity), Math.Max(StartAffection, friend.Affection), Math.Max(StartTrust, friend.Trust));
            int share = FriendSharePct + (Has(set, RoomTrait.Generous) ? 5 : Has(set, RoomTrait.Tight) ? -5 : 0);
            return new Flatmate { Person = p, Traits = set, Known = set, SharePct = SharePct(share, room), Answers = 0, Friend = friend.Index };
        }

        /// <summary>True if a friend would move in: a friend or closer who is not family, not the player's partner, and trusts them enough.</summary>
        public static bool FriendWould(SocialPerson friend)
        {
            return friend != null && friend.Met && !friend.IsFamily && !friend.Together && !friend.MovedIn && !friend.Drifted
                   && friend.Stage >= SocialStage.Friend && friend.Trust >= FriendAskTrust;
        }

        // The person model's own fields, set for somebody who is about to live in: met, their free hours from their job.
        private static void Start(SocialPerson p, RoomTrait set, float familiarity, float affection, float trust)
        {
            p.Met = true;
            p.Stage = SocialStage.Acquaintance;
            p.Familiarity = SocialRules.Clamp(familiarity);
            p.Affection = SocialRules.Clamp(affection);
            p.Trust = SocialRules.Clamp(trust);
            int from = WorkFrom[p.Job], to = WorkTo[p.Job];
            p.FreeFrom = to == 0 ? 15 : to >= 24 ? 10 : to;
            p.FreeTo = to >= 24 ? from : Has(set, RoomTrait.EarlyBird) ? 22 : 23;
        }

        private static RoomTrait FirstKnown(RoomTrait set, int pick)
        {
            int count = Count(set);
            int want = count > 0 ? pick % count : 0;
            for (int bit = 1; bit <= (int)RoomTrait.Private; bit <<= 1)
            {
                if ((set & (RoomTrait)bit) != 0 && want-- == 0)
                {
                    return (RoomTrait)bit;
                }
            }
            return RoomTrait.None;
        }

        /// <summary>The next hidden trait to show, after so many days together: one every <see cref="RevealDays"/>. None when all are known.</summary>
        public static RoomTrait Reveal(RoomTrait traits, RoomTrait known, int daysIn)
        {
            int due = 1 + daysIn / RevealDays;
            if (Count(known & traits) >= due)
            {
                return RoomTrait.None;
            }
            for (int bit = 1; bit <= (int)RoomTrait.Private; bit <<= 1)
            {
                if ((traits & (RoomTrait)bit) != 0 && (known & (RoomTrait)bit) == 0)
                {
                    return (RoomTrait)bit;
                }
            }
            return RoomTrait.None;
        }

        /// <summary>The share of the rent, in per cent, that somebody pays for the room a home has: less for a sofa.</summary>
        public static int SharePct(int offered, RoomKind room)
        {
            int share = room == RoomKind.Sofa ? offered * SofaPct / 100 : offered;
            return share < 5 ? 5 : share > 70 ? 70 : share;
        }

        // What a job leaves for rent: nobody offers more than their pay can carry.
        private static int Affordable(int job)
        {
            return JobPay[job] >= 200 ? ShareMost + 5 : JobPay[job] >= 150 ? 50 : 42;
        }

        private static int[] Shuffled(Random random, int count)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }
            for (int i = count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int swap = order[i];
                order[i] = order[j];
                order[j] = swap;
            }
            return order;
        }

        // ---------------------------------------------------------------- the hours they are in

        /// <summary>True if their job has them at work at this hour. <paramref name="weekday"/> is 0 for Monday.</summary>
        public static bool AtWork(int job, int day, int weekday, int hour)
        {
            if (job < 0 || job >= JobCount)
            {
                return false;
            }
            int from = WorkFrom[job], to = WorkTo[job];
            if (to == 0)
            {
                int turn = ((day % 3) + 3) % 3;
                return turn == 0 ? hour >= 6 && hour < 14 : turn == 1 && hour >= 14 && hour < 22;
            }
            if (((WorkDays[job] >> weekday) & 1) != 0 && hour >= from && hour < Math.Min(to, 24))
            {
                return true;
            }
            // A shift that began yesterday evening and runs past midnight.
            return to > 24 && hour < to - 24 && ((WorkDays[job] >> ((weekday + 6) % 7)) & 1) != 0;
        }

        /// <summary>The hour they go to bed and the hour they get up, by what they are like and what their job asks.</summary>
        public static void Sleep(int job, RoomTrait traits, out int bed, out int rise)
        {
            bed = Has(traits, RoomTrait.EarlyBird) ? 22 : Has(traits, RoomTrait.NightOwl) ? 2 : 23;
            rise = Has(traits, RoomTrait.EarlyBird) ? 6 : Has(traits, RoomTrait.NightOwl) ? 10 : 7;
            if (job < 0 || job >= JobCount)
            {
                return;
            }
            int from = WorkFrom[job], to = WorkTo[job];
            if (to == 0)
            {
                bed = 23;   // shifts: an early starts at six
                rise = 5;
            }
            else if (to > 24)
            {
                bed = to - 24;
                rise = bed + 8;
            }
            else if (from <= rise)
            {
                rise = from - 1;
                bed = (rise + 16) % 24;
            }
        }

        /// <summary>Where they are at an hour of a day: at work, asleep, out for the evening, in their room, or in the flat.</summary>
        public static RoomWhere Where(int job, RoomTrait traits, int seed, int day, int weekday, int hour)
        {
            if (AtWork(job, day, weekday, hour))
            {
                return RoomWhere.Work;
            }
            Sleep(job, traits, out int bed, out int rise);
            if (bed < rise ? hour >= bed && hour < rise : hour >= bed || hour < rise)
            {
                return RoomWhere.Asleep;
            }
            // An evening of their own: often for the sociable, never for those who keep to themselves.
            if (hour >= 18 && hour < 22 && !Has(traits, RoomTrait.Private))
            {
                int every = Has(traits, RoomTrait.Sociable) ? 3 : 5;
                if ((day * 7 + (seed & 0xff)) % every == 0)
                {
                    return RoomWhere.Out;
                }
            }
            if (Has(traits, RoomTrait.Private) && hour >= 21)
            {
                return RoomWhere.InRoom;
            }
            return RoomWhere.In;
        }

        /// <summary>True for the two answers that mean "in the flat but not to be seen".</summary>
        public static bool Hidden(RoomWhere where) => where == RoomWhere.Asleep || where == RoomWhere.InRoom;

        // ---------------------------------------------------------------- rent

        /// <summary>What a week of their share comes to. Never negative.</summary>
        public static int Weekly(float rentPerDay, int sharePct)
        {
            if (rentPerDay <= 0f || sharePct <= 0)
            {
                return 0;
            }
            return (int)Math.Round(rentPerDay * RentEveryDays * sharePct / 100.0);
        }

        /// <summary>The first rent day on or after a day, counted in weeks from the day they moved in.</summary>
        public static int NextRentDay(int movedIn, int day)
        {
            if (day <= movedIn)
            {
                return movedIn + RentEveryDays;
            }
            int weeks = (day - movedIn + RentEveryDays - 1) / RentEveryDays;
            return movedIn + weeks * RentEveryDays;
        }

        /// <summary>What happens on a rent day: paid, late (nothing yet) or short (part of it), and when the rest is promised.</summary>
        public static RentResult Rent(int seed, int day, RoomTrait traits, int level, int due)
        {
            var result = new RentResult { Outcome = RentOutcome.Paid, Paid = due };
            if (due <= 0 || Has(traits, RoomTrait.Reliable))
            {
                result.Paid = Math.Max(0, due);
                return result;
            }
            bool late = Has(traits, RoomTrait.LateWithRent);
            int lateOdds = (late ? LateOddsLate : PlainOddsLate) + (level < LowLevel ? LowLevelOddsLate : 0);
            int roll = Roll(seed, day, 11);
            if (roll < lateOdds)
            {
                result.Outcome = RentOutcome.Late;
                result.Paid = 0;
                result.Owed = due;
                result.DueInDays = 1 + Roll(seed, day, 12) % 3;
            }
            else if (late && roll < lateOdds + LateOddsShort)
            {
                result.Outcome = RentOutcome.Short;
                result.Paid = due * (50 + Roll(seed, day, 13) % 31) / 100;
                result.Owed = due - result.Paid;
                result.DueInDays = 2 + Roll(seed, day, 12) % 3;
            }
            return result;
        }

        /// <summary>The promised day has come. True if they pay what they owe; false if it slips by <see cref="SlipDays"/> (never more than <see cref="SlipsMost"/> times).</summary>
        public static bool PaysArrears(int seed, int day, RoomTrait traits, int slips)
        {
            if (!Has(traits, RoomTrait.LateWithRent) || slips >= SlipsMost)
            {
                return true;
            }
            return Roll(seed, day, 14) >= SlipOdds;
        }

        /// <summary>The player asks for the money now. True if they pay on the spot.</summary>
        public static bool PaysWhenChased(int seed, int day, RoomTrait traits, int level)
        {
            if (!Has(traits, RoomTrait.LateWithRent))
            {
                return true;
            }
            return Roll(seed, day, 15) < ChaseOdds + level * 100 / 2;
        }

        /// <summary>What they ask to borrow: a tenth of a week's share, in tens, between 20 and 200 dollars.</summary>
        public static int BorrowAmount(int weekly)
        {
            int amount = weekly / 10 / 10 * 10;
            return amount < 20 ? 20 : amount > 200 ? 200 : amount;
        }

        /// <summary>True if a loan comes back on the day it was promised for; a late payer keeps it a while longer, half the time.</summary>
        public static bool Repays(int seed, int day, RoomTrait traits)
        {
            return !Has(traits, RoomTrait.LateWithRent) || Roll(seed, day, 16) >= 5000;
        }

        /// <summary>What they pay of what they owe on the day they leave: everything if reliable or generous, else half.</summary>
        public static int SettledOnLeaving(int owed, RoomTrait traits)
        {
            if (owed <= 0)
            {
                return 0;
            }
            return Has(traits, RoomTrait.Reliable) || Has(traits, RoomTrait.Generous) ? owed : owed / 2;
        }

        /// <summary>The part of a week's share for the days since the last rent day, on the day they leave.</summary>
        public static int FinalRent(int weekly, int daysSinceRentDay)
        {
            int days = daysSinceRentDay < 0 ? 0 : daysSinceRentDay > RentEveryDays ? RentEveryDays : daysSinceRentDay;
            return weekly <= 0 ? 0 : weekly * days / RentEveryDays;
        }

        // ---------------------------------------------------------------- the relationship

        /// <summary>The level of the relationship, 0 to 100: the person model's own one figure.</summary>
        public static int Level(SocialPerson person) => person != null ? person.Closeness : 0;

        /// <summary>What a moment is worth: familiarity, affection, trust, in points of a hundred.</summary>
        public static void Effect(RoomMoment moment, out float familiarity, out float affection, out float trust)
        {
            familiarity = 0f;
            affection = 0f;
            trust = 0f;
            switch (moment)
            {
                case RoomMoment.DayTogether: familiarity = 1f; break;
                case RoomMoment.Talk: familiarity = 1f; affection = 0.5f; break;
                case RoomMoment.WarmReply: affection = 1.5f; trust = 0.5f; break;
                case RoomMoment.ShortReply: affection = -1f; break;
                case RoomMoment.SharedMeal: familiarity = 2f; affection = 3f; trust = 1f; break;
                case RoomMoment.Lent: affection = 4f; trust = 4f; break;
                case RoomMoment.RefusedLoan: affection = -1f; break;
                case RoomMoment.Repaid: trust = 2f; break;
                case RoomMoment.NotRepaid: trust = -3f; break;
                case RoomMoment.CoveredRent: affection = 6f; trust = 4f; break;
                case RoomMoment.Waited: affection = 1f; trust = 1f; break;
                case RoomMoment.Chased: affection = -2f; break;
                case RoomMoment.ChaseRefused: affection = -4f; trust = -3f; break;
                case RoomMoment.Row: affection = -8f; trust = -5f; break;
                case RoomMoment.OverdueDay: trust = -1.5f; break;
                case RoomMoment.NoisyNight: affection = -1f; break;
                case RoomMoment.NoiseForgiven: affection = 2f; break;
                case RoomMoment.AskedQuietly: trust = 1f; break;
                case RoomMoment.RaidForgiven: affection = 2f; break;
                case RoomMoment.AskedToStop: affection = -1f; trust = 1f; break;
                case RoomMoment.Thanked: affection = 2f; break;
                case RoomMoment.GoodLuck: affection = 2f; break;
                case RoomMoment.ToldToGo: affection = -3f; trust = -1f; break;
            }
        }

        /// <summary>Moves the person's figures by a moment, inside 0 to 100. Living together alone stops making people more familiar at <see cref="FamiliarityByLiving"/>.</summary>
        public static void Apply(SocialPerson person, RoomMoment moment)
        {
            Effect(moment, out float familiarity, out float affection, out float trust);
            if (moment == RoomMoment.DayTogether && person.Familiarity + familiarity > FamiliarityByLiving)
            {
                familiarity = Math.Max(0f, FamiliarityByLiving - person.Familiarity);
            }
            person.Familiarity = SocialRules.Clamp(person.Familiarity + familiarity);
            person.Affection = SocialRules.Clamp(person.Affection + affection);
            person.Trust = SocialRules.Clamp(person.Trust + trust);
        }

        /// <summary>True when things have got so bad that it ends in a row.</summary>
        public static bool Broken(float affection, float trust) => affection < RowAffection || trust < RowTrust;

        // ---------------------------------------------------------------- what a day brings

        /// <summary>What happens at home on a day, by what they are like and how things stand. Decided once, in the morning.</summary>
        public static DayEvents Day(int seed, int day, RoomTrait traits, in DayContext c)
        {
            DayEvents events = DayEvents.None;
            if (c.InAtNight && !Has(traits, RoomTrait.Quiet))
            {
                int odds = Has(traits, RoomTrait.Noisy) ? (c.QuietAsked ? NoiseOddsAsked : NoiseOdds) : Has(traits, RoomTrait.NightOwl) && !c.QuietAsked ? NoiseOddsOwl : 0;
                if (Roll(seed, day, 1) < odds)
                {
                    events |= DayEvents.Noise;
                }
            }
            if (Has(traits, RoomTrait.Cooks) && Roll(seed, day, 2) < CookOdds)
            {
                events |= DayEvents.Cooked;
            }
            if (Has(traits, RoomTrait.RaidsFridge) && !c.RaidStopped && c.RaidsThisWeek < RaidsPerWeek && Roll(seed, day, 3) < RaidOdds)
            {
                events |= DayEvents.Raid;
            }
            events |= Has(traits, RoomTrait.Tidy) ? DayEvents.Tidied : DayEvents.Mess;
            if (!c.Owes && !c.HasLoan && !c.OnNotice && c.DaysIn >= 3 && !Has(traits, RoomTrait.Reliable)
                && Roll(seed, day, 4) < (Has(traits, RoomTrait.LateWithRent) ? BorrowOddsLate : BorrowOdds))
            {
                events |= DayEvents.Borrow;
            }
            if (!c.OnNotice && Leaving(seed, day, c.DaysIn, c.Level, c.Affection, c.Trust) != EndWhy.None)
            {
                events |= DayEvents.Notice;
            }
            return events;
        }

        /// <summary>How much tidier (positive) or messier the home is for a day with them in it.</summary>
        public static float TidyChange(RoomTrait traits)
        {
            return Has(traits, RoomTrait.Tidy) ? TidyPerDay : Has(traits, RoomTrait.Messy) ? MessPerDay : OneMorePerDay;
        }

        /// <summary>How many units of food they take: one, while the fridges hold more than <see cref="RaidLeaves"/> and the week's limit is not reached; else none.</summary>
        public static int RaidTake(int unitsInFridges, int raidsThisWeek, bool stopped)
        {
            return !stopped && raidsThisWeek < RaidsPerWeek && unitsInFridges > RaidLeaves ? 1 : 0;
        }

        // ---------------------------------------------------------------- room

        /// <summary>True if the game's name for a piece of furniture is a bed (not a bedside table, not a flatbed trolley).</summary>
        public static bool IsBed(string itemName)
        {
            return itemName != null && itemName.IndexOf("bed", StringComparison.Ordinal) >= 0 && itemName.IndexOf("bedside", StringComparison.Ordinal) < 0
                   && itemName.IndexOf("flatbed", StringComparison.Ordinal) < 0;
        }

        public static bool IsSofa(string itemName)
        {
            return itemName != null && (itemName.IndexOf("sofa", StringComparison.Ordinal) >= 0 || itemName.IndexOf("couch", StringComparison.Ordinal) >= 0);
        }

        /// <summary>
        /// The room a home has for a roommate: a second bed is a room of their own; with one bed or none, a sofa will
        /// do, at less rent, unless a partner lives in (the living room is then the couple's); otherwise none.
        /// </summary>
        public static RoomKind Room(int beds, int sofas, bool partnerLivesIn)
        {
            if (beds >= 2)
            {
                return RoomKind.Bed;
            }
            return sofas >= 1 && !partnerLivesIn ? RoomKind.Sofa : RoomKind.None;
        }

        // ---------------------------------------------------------------- endings

        /// <summary>Whether they give notice today of their own accord, and why. A row whenever things are broken; otherwise only after <see cref="MinStayDays"/>.</summary>
        public static EndWhy Leaving(int seed, int day, int daysIn, int level, float affection, float trust)
        {
            if (Broken(affection, trust))
            {
                return EndWhy.Row;
            }
            if (daysIn < MinStayDays)
            {
                return EndWhy.None;
            }
            int roll = Roll(seed, day, 5);
            if (roll < (level < LowLevel ? LeaveOddsLow : LeaveOdds))
            {
                return level < LowLevel ? EndWhy.Row : (roll & 1) == 0 ? EndWhy.NewJob : EndWhy.NewPartner;
            }
            return EndWhy.None;
        }

        /// <summary>The player has moved home: they come along (None) if the new home has room and the two get on, else why not.</summary>
        public static EndWhy OnMove(bool hasNewHome, RoomKind roomThere, int level)
        {
            if (!hasNewHome)
            {
                return EndWhy.HomeGivenUp;
            }
            if (roomThere == RoomKind.None)
            {
                return EndWhy.NoRoomAfterMove;
            }
            return level >= GoodLevel ? EndWhy.None : EndWhy.NotCloseEnoughToMove;
        }

        /// <summary>The player's partner has moved in: the roommate stays (None) only with a bed of their own.</summary>
        public static EndWhy OnPartner(int beds)
        {
            return beds >= 2 ? EndWhy.None : EndWhy.PartnerMovedIn;
        }

        /// <summary>Asked to stay after giving notice: only somebody close, leaving for a reason that is not a row, and then half the time.</summary>
        public static bool StaysWhenAsked(int seed, int day, int level, EndWhy why)
        {
            return why != EndWhy.Row && why != EndWhy.PlayerNotice && level >= FriendLevel && Roll(seed, day, 6) < StayOdds;
        }

        /// <summary>True on and after the day a notice runs out.</summary>
        public static bool Gone(int leaveDay, int day) => leaveDay > 0 && day >= leaveDay;
    }
}
