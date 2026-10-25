using System;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A level of the management ladder, the same numbers in a career at one of the city's companies and in the player's own
    /// companies' hierarchy: the saved number of <see cref="LadderFile.Rung"/>. Owner is the player's own company alone, never a rung
    /// of a career.
    /// </summary>
    public enum Rung
    {
        Frontline = 0, Supervisor = 1, StoreManager = 2, Regional = 3, Executive = 4, Owner = 5
    }

    /// <summary>An executive's seat: the saved number of <see cref="LadderFile.Seat"/> and <see cref="Colleague.Seat"/>.</summary>
    public enum ExecSeat
    {
        None = 0, Coo = 1, Chro = 2, Cfo = 3
    }

    /// <summary>What a rung may do. Each is a tool in the Career app's Management tab; a promotion brings every one of the new rung's at once.</summary>
    [Flags]
    public enum RungPower
    {
        None = 0,
        OwnSchedule = 1 << 0,   // see one's own schedule, wage and requests
        Requests = 1 << 1,      // ask the boss for a shift, equipment or insurance
        Cover = 1 << 2,         // fill a gap on a shift from the staff on hand
        ShiftAsks = 1 << 3,     // answer the staff's requests about shifts
        PassUp = 1 << 4,        // send a matter to the rung above
        Rota = 1 << 5,          // set the store's schedule, within the company's staffing policy
        Hire = 1 << 6,          // fill a post within the hiring policy
        Raises = 1 << 7,        // give a small raise within the salary band
        Demands = 1 << 8,       // meet the staff's demands within the budget
        Resolve = 1 << 9,       // settle what is sent up from below
        Stores = 1 << 10,       // see and run several stores
        Transfers = 1 << 11,    // move staff between stores
        Appoint = 1 << 12,      // appoint a store manager
        Policy = 1 << 13        // set the company's policy in the seat's area
    }

    /// <summary>One rung: what it takes to be promoted to it, what it pays, the least stores the company needs for it, what it may do.</summary>
    public sealed class RungDef
    {
        public readonly Rung Level;
        public readonly string Key;        // reallife_ladder_rung_{Key}; an executive's reallife_ladder_rung_executive_{seat}
        public readonly int Days;          // days at the rung below before this one, written for a year of 365 (LifeRules.Paced)
        public readonly int Score;         // the performance score (0 to 100) it asks for
        public readonly int Need;          // points of its two skills (the first counting three fifths) it asks for
        public readonly int SkillA, SkillB;   // its skills (TalentCatalogue); an executive's are the seat's
        public readonly bool Recommend;    // a superior's recommendation is required (otherwise it takes a third off the days)
        public readonly int PayRank;       // the management profession's rank (ProCatalogue.Manager) whose pay it starts from; -1 the job's own
        public readonly int Stores;        // the least stores the company must have for this rung to be there
        public readonly RungPower Powers;

        public RungDef(Rung level, string key, int days, int score, int need, int skillA, int skillB, bool recommend, int payRank, int stores, RungPower powers)
        {
            Level = level;
            Key = key;
            Days = days;
            Score = score;
            Need = need;
            SkillA = skillA;
            SkillB = skillB;
            Recommend = recommend;
            PayRank = payRank;
            Stores = stores;
            Powers = powers;
        }
    }

    /// <summary>
    /// The management ladder's rungs, the one list of names and levels for a career at a city company and for the player's own
    /// companies. Game-free. Kept small and apart so that it can be folded into the shared hierarchy catalogue of the player's own
    /// companies (Economy/Company) when both are in main: the names, numbers and powers there are these.
    /// </summary>
    public static class LadderRanks
    {
        private const RungPower Staff = RungPower.OwnSchedule | RungPower.Requests;
        private const RungPower Shift = Staff | RungPower.Cover | RungPower.ShiftAsks | RungPower.PassUp;
        private const RungPower InStore = Shift | RungPower.Rota | RungPower.Hire | RungPower.Raises | RungPower.Demands | RungPower.Resolve;
        private const RungPower Region = InStore | RungPower.Stores | RungPower.Transfers | RungPower.Appoint;
        private const RungPower Top = Region | RungPower.Policy;

        public static readonly RungDef[] All =
        {
            new RungDef(Rung.Frontline, "frontline", 0, 0, 0, -1, -1, false, -1, 1, Staff),
            new RungDef(Rung.Supervisor, "supervisor", 30, 60, 5, TalentCatalogue.Leadership, TalentCatalogue.Delivery, false, 0, 1, Shift),
            new RungDef(Rung.StoreManager, "store_manager", 60, 65, 18, TalentCatalogue.Leadership, TalentCatalogue.Delivery, false, 1, 1, InStore),
            new RungDef(Rung.Regional, "regional", 120, 70, 34, TalentCatalogue.Leadership, TalentCatalogue.Analysis, false, 3, 2, Region),
            new RungDef(Rung.Executive, "executive", 180, 75, 50, -1, -1, true, 5, 2, Top),
            new RungDef(Rung.Owner, "owner", 0, 0, 0, -1, -1, false, 6, 1, Top)
        };

        /// <summary>The highest rung of a career: an owner is never promoted to.</summary>
        public const int CareerTop = (int)Rung.Executive;

        public static RungDef Of(int rung) => All[rung < 0 ? 0 : rung >= All.Length ? All.Length - 1 : rung];

        public static bool Has(int rung, RungPower p) => (Of(rung).Powers & p) == p;

        /// <summary>The two skills a rung is judged on; an executive's are its seat's.</summary>
        public static void SkillsOf(int rung, ExecSeat seat, out int a, out int b)
        {
            RungDef r = Of(rung);
            if (r.Level != Rung.Executive)
            {
                a = r.SkillA;
                b = r.SkillB;
                return;
            }
            switch (seat)
            {
                case ExecSeat.Chro: a = TalentCatalogue.Recruiting; b = TalentCatalogue.Leadership; return;
                case ExecSeat.Cfo: a = TalentCatalogue.Accounting; b = TalentCatalogue.Analysis; return;
                default: a = TalentCatalogue.Delivery; b = TalentCatalogue.Leadership; return;
            }
        }

        /// <summary>A rung's points from the points of each skill: the first counting three fifths, as the professions' (ProRules.Points).</summary>
        public static int Points(int rung, ExecSeat seat, Func<int, int> pointsOf)
        {
            SkillsOf(rung, seat, out int a, out int b);
            if (a < 0 || pointsOf == null)
            {
                return 0;
            }
            return (pointsOf(a) * 3 + pointsOf(b) * 2) / 5;
        }

        /// <summary>The seat the player's skills fit best, the operations seat on a tie.</summary>
        public static ExecSeat BestSeat(Func<int, int> pointsOf)
        {
            ExecSeat best = ExecSeat.Coo;
            int most = -1;
            for (int s = (int)ExecSeat.Coo; s <= (int)ExecSeat.Cfo; s++)
            {
                int p = Points((int)Rung.Executive, (ExecSeat)s, pointsOf);
                if (p > most)
                {
                    most = p;
                    best = (ExecSeat)s;
                }
            }
            return best;
        }

        /// <summary>A rung's title key: an executive's by the seat.</summary>
        public static string TitleKey(int rung, ExecSeat seat)
        {
            RungDef r = Of(rung);
            return r.Level == Rung.Executive && seat != ExecSeat.None
                ? Key("rung_executive", seat.ToString().ToLowerInvariant())
                : Key("rung", r.Key);
        }

        /// <summary>Every power, in the order the Career app lists them.</summary>
        public static readonly RungPower[] Listed =
        {
            RungPower.OwnSchedule, RungPower.Requests, RungPower.Cover, RungPower.ShiftAsks, RungPower.PassUp, RungPower.Rota, RungPower.Hire, RungPower.Raises,
                RungPower.Demands, RungPower.Resolve,
            RungPower.Stores, RungPower.Transfers, RungPower.Appoint, RungPower.Policy
        };

        /// <summary>A power's line in the Career app: reallife_ladder_power_{name}.</summary>
        public static string PowerKey(RungPower p) => Key("power", p.ToString().ToLowerInvariant());

        private const string Mod = "reallife";   // (keys made of parts are put together from this, so the text check reads only whole keys)

        /// <summary>A key of the ladder's texts made of its part and a name: reallife_ladder_{part}_{name}.</summary>
        public static string Key(string part, string name) => Mod + "_ladder_" + part + "_" + name;
    }
}
