using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>A branch's hour as the game has it (BranchGame.Hour): game-free, so an hour runs offline as in the game.</summary>
    public struct BranchHourIn
    {
        public bool Open;                   // open now by its opening hours, and not closed for anything
        public float ShopCustomers;         // the customers an hour the game's own count gives a shop of this building (its capacity)
        public int Promotion;               // the building's promotion (0 to 100)
        public float BaseMultiplier;        // the game's own base for the promotion (gameVariables.baseCustomerPromotionMultiplier)
        public int Satisfaction;            // the game's customer satisfaction of the building (0 to 100; 50 is even)
        public int Tellers;                 // tellers on shift there now (customer service their main skill)
        public int Skill;                   // their average customer service skill (0 to 100)
        public bool Player;                 // the player is working its counter
    }

    /// <summary>One customer of an hour as the branch's floor shows them (Branch/BranchFloor).</summary>
    public struct FloorCustomer
    {
        public int Minute;          // the minute of the hour they come in
        public bool Served;         // seen to at the counter, or left without
        public int Kind;            // what they came for (BranchCatalogue.Open ... Enquiry)
    }

    /// <summary>What the branches' reach brings the bank (BranchRules.Worth): more deposits and loans, and their margin a day, in cents.</summary>
    public struct BranchWorth
    {
        public long Deposits, Loans, DayCents;
    }

    /// <summary>What an hour of a branch came to.</summary>
    public struct BranchHourOut
    {
        public int Visitors, Served, Lost, Opened, Loans, Refused;
        public long PaidIn, TakenOut, Lent, Fees;
    }

    /// <summary>
    /// The rules of the player's bank branches, game-free: how many people come in an hour (the game's own count for a shop of
    /// the building, a branch's share of it, by the hour, its promotion and its customers' satisfaction), how many its tellers
    /// and the player see to, what each visit is for (the same for the same bank, hour, branch and visit), and how far the
    /// visits of the last week take the bank's hold of the city (<see cref="Reach"/>). An hour is a few dozen sums a branch
    /// and allocates nothing.
    /// </summary>
    public static class BranchRules
    {
        /// <summary>Thousandths of a visitor in an hour: the game's count for a shop of the building, the branch's share, by hour and satisfaction.</summary>
        public static long VisitorThousandths(in BranchHourIn h, int hour)
        {
            if (!h.Open || h.ShopCustomers <= 0f)
            {
                return 0;
            }
            double shop = h.ShopCustomers * (h.BaseMultiplier + 0.75 * OwnBankRules.Clamp(h.Promotion, 0, 100) / 100.0);
            int satisfaction = 1000 + (OwnBankRules.Clamp(h.Satisfaction, 0, 100) - 50) * BranchCatalogue.SatisfactionWeight / 50;
            double v = shop * BranchCatalogue.VisitorShare * BranchCatalogue.HourWeight[OwnBankRules.Clamp(hour, 0, 23)] / 1000.0 * satisfaction / 1000.0;
            return (long)Math.Round(v, MidpointRounding.AwayFromZero);
        }

        /// <summary>Whole visitors this hour, with the thousandths left over carried to the next; nothing carried past a closed hour.</summary>
        public static int Visitors(in BranchHourIn h, int hour, ref int carry)
        {
            long t = VisitorThousandths(h, hour) + Math.Max(0, carry);
            int whole = (int)Math.Min(1000, t / 1000);
            carry = h.Open ? (int)(t - whole * 1000L) : 0;
            return h.Open ? whole : 0;
        }

        /// <summary>What a teller of a skill gets through, thousandths of <see cref="BranchCatalogue.ServePerTeller"/>.</summary>
        public static int SkillFactor(int skill)
        {
            return BranchCatalogue.SkillLeast + (BranchCatalogue.SkillMost - BranchCatalogue.SkillLeast) * OwnBankRules.Clamp(skill, 0, 100) / 100;
        }

        /// <summary>Visits the counter can see to in the hour: its tellers by their skill, and the player as a teller of middling skill.</summary>
        public static int Capacity(in BranchHourIn h)
        {
            if (!h.Open)
            {
                return 0;
            }
            long t = (long)Math.Max(0, h.Tellers) * BranchCatalogue.ServePerTeller * SkillFactor(h.Skill)
                     + (h.Player ? (long)BranchCatalogue.ServePerTeller * SkillFactor(BranchCatalogue.PlayerSkill) : 0);
            return (int)Math.Min(1000, t / 1000);
        }

        /// <summary>
        /// Thousandths more of the city's people the bank holds for the visits its branches saw to in the last week, all of
        /// them together: towards <see cref="BranchCatalogue.ReachMost"/>, half of it at <see cref="BranchCatalogue.ReachHalf"/>,
        /// so that each visit more, and each branch more, adds less than the one before.
        /// </summary>
        public static int Reach(long weekServed)
        {
            if (weekServed <= 0)
            {
                return 0;
            }
            return (int)(BranchCatalogue.ReachMost * weekServed / (weekServed + BranchCatalogue.ReachHalf));
        }

        /// <summary>The reach of all the bank's branches on a day.</summary>
        public static int Reach(OwnBank b, int today)
        {
            if (b == null || !b.Running)
            {
                return 0;
            }
            long sum = 0;
            for (int i = 0; i < b.Branches.Count; i++)
            {
                sum += b.Branches[i].WeekServed(today);
            }
            return Reach(sum);
        }

        /// <summary>
        /// What the branches' reach is worth to the bank now, once the city has come round to it: the deposits and loans it
        /// would hold with the reach and without it, and the margin that difference earns a day before staff (loans at their
        /// rates less the reserve rate and the part of their defaults that is lost; deposits at the reserve rate less what
        /// savers are paid). <paramref name="trust"/> is the city's trust in the bank today (OwnBankRules.Trust).
        /// </summary>
        public static BranchWorth Worth(BankCity c, OwnBank b, int trust)
        {
            var w = new BranchWorth();
            if (b == null || !b.Running || c.Reach <= 0)
            {
                return w;
            }
            BankCity none = c;
            none.Reach = 0;
            int y = Math.Max(1, c.YearDays);
            long reserveBp = Math.Max(0, c.CityRateBp - OwnBankCatalogue.ReserveSpread);
            double day = 0;
            for (int k = 0; k < OwnBankCatalogue.Savers; k++)
            {
                long more = OwnBankRules.SaversWanted(c, b, k, trust) - OwnBankRules.SaversWanted(none, b, k, trust);
                w.Deposits += more;
                day += more * (reserveBp - OwnBankRules.SaverBp(c, b, k)) / 10000.0 / y;
            }
            for (int j = 0; j < OwnBankCatalogue.Segments; j++)
            {
                long more = OwnBankRules.LoansWanted(c, b, j) - OwnBankRules.LoansWanted(none, b, j);
                w.Loans += more;
                double lossBp = OwnBankRules.DefaultPerMille(c, b, j) * 10.0 * (1000 - OwnBankCatalogue.CureShare[j]) / 1000.0 * (1000 - OwnBankCatalogue.RecoverShare[j]) / 1000.0;
                day += more * (OwnBankRules.LoanBp(c, b, j) - reserveBp - lossBp) / 10000.0 / y;
            }
            w.DayCents = (long)Math.Round(day);
            return w;
        }

        /// <summary>A number for a branch's place, the same on every machine (FNV-1a over its characters).</summary>
        public static int PlaceKey(string place)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; place != null && i < place.Length; i++)
                {
                    h ^= place[i];
                    h *= 16777619u;
                }
                return (int)(h & 0x7FFFFFFF);
            }
        }

        /// <summary>Thousandths from 0 to 999 for a visit: the same for the same bank, hour, branch, visit and question.</summary>
        public static int Draw(int seed, int hourIndex, int place, int visit, int question)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)(hourIndex * 40503 + place * 9973 + visit * 7919 + question * 104729 + 31);
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (int)(h % 1000u);
            }
        }

        /// <summary>What a visit is for, by the shares of the kinds; in a run, those who came to pay in take their money out instead.</summary>
        public static int Kind(int draw, bool run)
        {
            int at = 0;
            for (int k = 0; k < BranchCatalogue.Kinds; k++)
            {
                at += BranchCatalogue.KindShare[k];
                if (draw < at)
                {
                    return run && (k == BranchCatalogue.Open || k == BranchCatalogue.Deposit) ? BranchCatalogue.Withdraw : k;
                }
            }
            return BranchCatalogue.Enquiry;
        }

        /// <summary>Who asks for a loan at the counter: a poorly, fairly or well rated person (OwnBankCatalogue.Poor, Fair, Good).</summary>
        public static int Asker(int draw)
        {
            int sum = 0;
            for (int i = 0; i < BranchCatalogue.AskBy.Length; i++)
            {
                sum += BranchCatalogue.AskBy[i];
            }
            int at = 0, pick = draw * sum / 1000;
            for (int i = 0; i < BranchCatalogue.AskBy.Length; i++)
            {
                at += BranchCatalogue.AskBy[i];
                if (pick < at)
                {
                    return i;   // the first three segments are the people's, in this order
                }
            }
            return OwnBankCatalogue.Good;
        }

        /// <summary>A visit's sum at today's prices: the usual for its kind, spread from <see cref="BranchCatalogue.SumLeast"/> to <see cref="BranchCatalogue.SumMost"/> of it.</summary>
        public static long Sum(int kind, int draw, int level)
        {
            long usual = BranchCatalogue.KindCents[OwnBankRules.Clamp(kind, 0, BranchCatalogue.Kinds - 1)];
            int spread = BranchCatalogue.SumLeast + (BranchCatalogue.SumMost - BranchCatalogue.SumLeast) * OwnBankRules.Clamp(draw, 0, 999) / 999;
            return OwnBankRules.At(usual * spread / 1000, level);
        }

        /// <summary>
        /// One hour of a branch: its visitors come, the counter sees to as many as it can, and each visit seen to is booked in
        /// the bank (<see cref="OwnBankRules.Visit"/>); the branch's figures and its week are counted. Nothing comes to a branch
        /// of a bank that is not running. <paramref name="hourIndex"/> is day x 24 + hour; a branch's month is <paramref name="monthDays"/> long.
        /// </summary>
        public static BranchHourOut Hour(OwnBank b, BankBranch br, in BranchHourIn h, int hourIndex, int level, int monthDays)
        {
            var o = new BranchHourOut();
            int day = hourIndex / 24, hour = hourIndex - day * 24;
            Roll(br, day, monthDays);
            br.HourIndex = hourIndex;
            br.HourOpen = h.Open && b.Running;
            br.HourTellers = Math.Max(0, h.Tellers) + (h.Player ? 1 : 0);
            if (!b.Running)
            {
                br.Carry = 0;
                br.HourVisitors = br.HourServed = 0;
                return o;
            }
            o.Visitors = Visitors(h, hour, ref br.Carry);
            o.Served = Math.Min(o.Visitors, Capacity(h));
            o.Lost = o.Visitors - o.Served;
            int place = PlaceKey(br.Place);
            for (int i = 0; i < o.Served; i++)
            {
                int kind = Kind(Draw(b.Seed, hourIndex, place, i, 0), b.Run);
                int segment = kind == BranchCatalogue.Loan ? Asker(Draw(b.Seed, hourIndex, place, i, 1)) : OwnBankCatalogue.Fair;
                long sum = Sum(kind, Draw(b.Seed, hourIndex, place, i, 2), level);
                VisitResult r = OwnBankRules.Visit(b, kind, segment, sum, level, out long fee, out long moved);
                o.Fees += fee;
                switch (r)
                {
                    case VisitResult.PaidIn:
                        o.PaidIn += moved;
                        o.Opened += kind == BranchCatalogue.Open ? 1 : 0;
                        break;
                    case VisitResult.TakenOut:
                        o.TakenOut += moved;
                        break;
                    case VisitResult.Lent:
                        o.Loans++;
                        o.Lent += moved;
                        break;
                    case VisitResult.Refused:
                        o.Refused++;
                        break;
                }
            }
            br.HourVisitors = o.Visitors;
            br.HourServed = o.Served;
            br.CountServed(day, o.Served);
            Add(br.Month, o);
            return o;
        }

        /// <summary>
        /// The customers of an hour the floor shows: up to <see cref="BranchCatalogue.ShownMost"/> of the hour's visitors, in
        /// the order they come, the share seen to as the hour's, each at its minute; the same for the same bank, hour and
        /// branch on every machine. Fills <paramref name="into"/>.
        /// </summary>
        public static void Floor(int seed, int hourIndex, string place, int visitors, int served, bool run, System.Collections.Generic.List<FloorCustomer> into)
        {
            into.Clear();
            int shown = Math.Min(Math.Max(0, visitors), BranchCatalogue.ShownMost);
            if (shown == 0)
            {
                return;
            }
            int key = PlaceKey(place), seen = Math.Max(0, Math.Min(served, visitors)) * shown / Math.Max(1, visitors);
            for (int i = 0; i < shown; i++)
            {
                into.Add(new FloorCustomer
                {
                    Minute = Draw(seed, hourIndex, key, i, 3) * 55 / 1000,
                    Served = i < seen,
                    Kind = Kind(Draw(seed, hourIndex, key, i, 0), run)
                });
            }
            into.Sort((a, b) => a.Minute.CompareTo(b.Minute));
        }

        /// <summary>The branch's month closed once it has run a month: its figures kept as last month's.</summary>
        public static void Roll(BankBranch br, int day, int monthDays)
        {
            if (br.MonthFrom > day)
            {
                br.MonthFrom = day;   // a test's rewind
            }
            if (day - br.MonthFrom < Math.Max(1, monthDays))
            {
                return;
            }
            Array.Copy(br.Month, br.Last, BranchFigure.Count);
            Array.Clear(br.Month, 0, BranchFigure.Count);
            br.MonthFrom = day - (day - br.MonthFrom) % Math.Max(1, monthDays);
        }

        private static void Add(long[] m, in BranchHourOut o)
        {
            m[BranchFigure.Visitors] += o.Visitors;
            m[BranchFigure.Served] += o.Served;
            m[BranchFigure.Lost] += o.Lost;
            m[BranchFigure.Opened] += o.Opened;
            m[BranchFigure.PaidIn] += o.PaidIn;
            m[BranchFigure.TakenOut] += o.TakenOut;
            m[BranchFigure.Loans] += o.Loans;
            m[BranchFigure.Lent] += o.Lent;
            m[BranchFigure.Refused] += o.Refused;
            m[BranchFigure.Fees] += o.Fees;
        }
    }
}
