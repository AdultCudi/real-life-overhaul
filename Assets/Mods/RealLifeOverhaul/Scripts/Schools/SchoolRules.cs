using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Schools
{
    /// <summary>One school of the city as the market starts it: its name, kind, district, first owner and figures.</summary>
    public struct SchoolDef
    {
        public string Key;          // its name's locale key; a council school's is a phrase the district's name fills
        public SchoolKind Kind;
        public string District;     // the game's district it stands in
        public int Owner;           // its first owner (SchoolOwner)
        public int Capacity;        // the most places its buildings hold
        public int Places, Fee, Teachers, Skill, PayBp, HeadSkill, Scholar, Reputation;
        public int Upkeep;          // a day's upkeep of its buildings, whole money at the start's prices
        public long Cash;           // its owner's account at it at the start

        public SchoolDef(string key, SchoolKind kind, string district, int owner, int capacity, int places, int fee, int teachers, int skill, int payBp, int headSkill,
                         int scholar, int reputation, int upkeep, long cash)
        {
            Key = key;
            Kind = kind;
            District = district;
            Owner = owner;
            Capacity = capacity;
            Places = places;
            Fee = fee;
            Teachers = teachers;
            Skill = skill;
            PayBp = payBp;
            HeadSkill = headSkill;
            Scholar = scholar;
            Reputation = reputation;
            Upkeep = upkeep;
            Cash = cash;
        }
    }

    /// <summary>What a day's pass did that the player should hear of, besides the City Ledger's pieces.</summary>
    [Flags]
    public enum SchoolDayFlags
    {
        None = 0,
        TermStarted = 1,
        Results = 2,
        Inspected = 4,           // the campus was inspected
        Poached = 8,             // the campus gained or lost teachers to another school
        Sold = 16,               // the campus changed hands without the player (the opening sale)
        Offer = 32,              // a trust offers to buy the player's school
        OfferLapsed = 64,
        WeekBooked = 128,        // the player's school's week is ready to be booked to the game (SchoolState.Campus.Week)
        OpenDay = 256
    }

    /// <summary>
    /// The city's schools as one market (plans/campus.md): Birchwood School on the campus, the four education trusts' private
    /// schools and the council's free school of each of the game's districts. Families share the private places by each
    /// school's name, fee and scholarships; teaching comes from the head, the staff and the pupils to a teacher; results day,
    /// inspections and the pay war are one set of rules for every school, the player's included (the owner, 25 Oct 2026:
    /// "Whatever I do, the rivals and NPCs must be able to do"). Every answer comes from the state, the day and fixed figures,
    /// picks and chance by FNV-1a hashes: the same on every machine in any order. Game-free (EconomyCheck's SchoolChecks).
    /// COST: nothing per frame; a day's pass over twelve schools.
    /// </summary>
    public static class SchoolRules
    {
        public const int CampusId = 0;
        public const int Trusts = 4;
        public const int ClassSize = 30;            // places a class
        public const int RatioTarget = 18;          // pupils to a teacher at which the staff teach as well as they can
        public const int TeacherDay = 240;          // a teacher's day at the city's going rate, at the start's prices
        public const int HeadDay = 650;             // a headteacher's day
        public const int PupilDay = 9;              // a pupil's day of meals, books and cleaning
        public const int FairFee = 60;              // a pupil's day the city's families call fair (the family's own private school asks 54)
        public const int FeeLeast = 20, FeeMost = 200, FeeStep = 5;
        public const int PayLeast = 8000, PayMost = 15000, PayStep = 500;
        public const int ScholarMost = 20, ScholarStep = 5;
        public const int PrivatePool = 3000;        // the city's children whose families pay for a school
        public const int InspectEvery = 45;         // days between a school's inspections
        public const int SaleDays = 14;             // days the council offers the new campus before a trust takes it
        public const int OfferAfter = 21;           // days the player keeps the school before a trust first offers for it
        public const int OfferDays = 5;             // days a trust's offer stands
        public const int HireMost = 6;              // teachers a school can take on in a week from the city's pool
        public const int PoachShare = 10;           // per cent of a school's staff another may take at a term's start
        public const int PoachMost = 8;
        public const int PoachPremium = 11000;      // basis points of the other's pay a school must pay to take its teachers
        public const long ValueFloor = 12000000;    // the least a school with its buildings is worth
        public const int ValueDays = 600;           // days of its present profit a school is worth
        public const int AskingPremium = 115;       // per cent of its worth an owner asks when another buys
        public const int SalePart = 90;             // per cent of its worth the trusts pay the player
        public const int OfferPart = 105;           // per cent of its worth a trust offers for the player's school
        public const int SellAfterWeeks = 4;        // a trust sells a school that has lost money this many weeks in a row
        public const int HeadHireDays = 10;         // days of a head's pay a hire costs (the search)
        public const int OpenDayFrom = 10, OpenDayTo = 14;   // game hours of an open day, the term's third day
        public const int RunMorningFrom = 450, RunMorningTo = 540;     // minutes of the day of the school run: 07:30 to 09:00 ...
        public const int RunAfternoonFrom = 885, RunAfternoonTo = 975; // ... and 14:45 to 16:15
        public const int StandingAtFifty = 50;      // the results at which a child's reports neither rise nor fall
        public const int CouncilPerPupil = 14;      // the council's grant a pupil a day at its own schools
        public const float SchoolFrom = 5f;         // years: a child starts school (family life's own age, FamilyBalance.SchoolFrom) ...
        public const float SchoolTo = 18f;          // ... and leaves it
        public const int CouncilSway = 6;           // points of its teachers' skill the council's money for schools moves its own schools by
        public const int LiftReach = 160;           // m from the campus within which its name lifts the district's lots

        public const int NewsOpened = 1, NewsSold = 2, NewsResults = 3, NewsInspected = 4, NewsPoached = 5, NewsOpenDay = 6;

        private const string Uptown = "reallife_neighborhood_uptown";

        /// <summary>The city's twelve schools, in the order every pass goes through them.</summary>
        public static readonly SchoolDef[] Catalogue =
        {
            new SchoolDef("reallife_schools_name_birchwood", SchoolKind.Campus, Uptown, SchoolOwner.Council, 1200, 600, 50, 30, 58, 10000, 62, 5, 600, 3000, 0),
            new SchoolDef("reallife_schools_name_meridian", SchoolKind.Trust, "ba:neighborhood_midtown", 0, 800, 600, 75, 33, 66, 10500, 70, 0, 680, 2400, 2000000),
            new SchoolDef("reallife_schools_name_ashford", SchoolKind.Trust, "ba:neighborhood_murrayhill", 1, 700, 510, 65, 28, 62, 10000, 64, 5, 620, 2100, 1500000),
            new SchoolDef("reallife_schools_name_northbridge", SchoolKind.Trust, "ba:neighborhood_lowermanhattan", 2, 900, 660, 55, 36, 55, 9500, 58, 10, 560, 2600, 1200000),
            new SchoolDef("reallife_schools_name_calder", SchoolKind.Trust, "ba:neighborhood_thehamptons", 3, 500, 360, 110, 21, 74, 12000, 78, 0, 760, 2200, 3000000),
            new SchoolDef("reallife_schools_name_council", SchoolKind.Council, "ba:neighborhood_midtown", SchoolOwner.Council, 1000, 1000, 0, 50, 56, 10000, 58, 0, 560, 0, 0),
            new SchoolDef("reallife_schools_name_council", SchoolKind.Council, "ba:neighborhood_hellskitchen", SchoolOwner.Council, 1100, 1100, 0, 55, 50, 10000, 52, 0, 470, 0, 0),
            new SchoolDef("reallife_schools_name_council", SchoolKind.Council, "ba:neighborhood_garmentdistrict", SchoolOwner.Council, 900, 900, 0, 45, 52, 10000, 55, 0, 500, 0, 0),
            new SchoolDef("reallife_schools_name_council", SchoolKind.Council, "ba:neighborhood_murrayhill", SchoolOwner.Council, 900, 900, 0, 45, 57, 10000, 60, 0, 580, 0, 0),
            new SchoolDef("reallife_schools_name_council", SchoolKind.Council, "ba:neighborhood_lowermanhattan", SchoolOwner.Council, 1000, 1000, 0, 50, 54, 10000, 54, 0, 520, 0, 0),
            new SchoolDef("reallife_schools_name_council", SchoolKind.Council, "ba:neighborhood_industrycity", SchoolOwner.Council, 1200, 1200, 0, 60, 48, 10000, 50, 0, 450, 0, 0),
            new SchoolDef("reallife_schools_name_council", SchoolKind.Council, "ba:neighborhood_thehamptons", SchoolOwner.Council, 400, 400, 0, 21, 64, 10000, 66, 0, 640, 0, 0),
        };

        public static int Count => Catalogue.Length;

        /// <summary>The trusts' names' locale keys, by trust.</summary>
        public static readonly string[] TrustKeys = { "reallife_schools_trust_0", "reallife_schools_trust_1", "reallife_schools_trust_2", "reallife_schools_trust_3" };

        public static SchoolDef Def(int id) => Catalogue[Math.Max(0, Math.Min(Count - 1, id))];

        /// <summary>True for a school whose families pay: the campus and the trusts' (the council's are free and take everyone else).</summary>
        public static bool Private(int id) => Def(id).Kind != SchoolKind.Council;

        // ---------------------------------------------------------------- the start

        /// <summary>Every school as the catalogue starts it (the state's files made afresh, its passes' days kept).</summary>
        public static void Fresh(SchoolState s)
        {
            for (int i = 0; i < Count; i++)
            {
                s.Files[i] = Start(i);
            }
        }

        /// <summary>One school as the catalogue starts it.</summary>
        public static SchoolFile Start(int id)
        {
            SchoolDef d = Def(id);
            var f = new SchoolFile
            {
                Id = id, Owner = d.Owner, Places = d.Places, Fee = d.Fee, Teachers = d.Teachers, Skill = d.Skill, PayBp = d.PayBp, Head = HeadSeed(id, 0),
                HeadSkill = d.HeadSkill, Scholar = d.Scholar, Reputation = d.Reputation, Cash = d.Cash
            };
            f.Pupils = d.Kind == SchoolKind.Council ? d.Places : Math.Min(d.Places, d.Places * 9 / 10);
            return f;
        }

        // ---------------------------------------------------------------- families choose

        /// <summary>
        /// A private school's pull on the city's families: its name squared, its fee against the fair one (dearer pulls less,
        /// never nothing: some families want the dearest), and its scholarships. 0 for a council school or one with no places.
        /// </summary>
        public static double Pull(SchoolFile f)
        {
            if (f == null || !Private(f.Id) || f.Places <= 0)
            {
                return 0.0;
            }
            double name = Math.Max(0, Math.Min(1000, f.Reputation)) / 1000.0;
            double fee = Math.Pow(FairFee / (double)Math.Max(FeeLeast, f.Fee), 0.8);
            return name * name * fee * (1.0 + f.Scholar / 100.0);
        }

        /// <summary>
        /// The families who want each private school this term (into <paramref name="applications"/>, by id): the city's
        /// private pool shared by pull, rounded down, the remainders to the largest shares first, ties by id.
        /// </summary>
        public static void Applications(SchoolState s, int[] applications)
        {
            double total = 0.0;
            for (int i = 0; i < Count; i++)
            {
                applications[i] = 0;
                total += Pull(s.Files[i]);
            }
            if (total <= 0.0)
            {
                return;
            }
            int given = 0;
            var rest = new double[Count];
            for (int i = 0; i < Count; i++)
            {
                double share = PrivatePool * Pull(s.Files[i]) / total;
                applications[i] = (int)Math.Floor(share);
                rest[i] = share - applications[i];
                given += applications[i];
            }
            for (int left = PrivatePool - given; left > 0; left--)
            {
                int best = -1;
                for (int i = 0; i < Count; i++)
                {
                    if (Pull(s.Files[i]) > 0.0 && (best < 0 || rest[i] > rest[best] + 1e-12))
                    {
                        best = i;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                applications[best]++;
                rest[best] = -1.0;
            }
        }

        /// <summary>A term's start: each private school takes the families who want it up to its places, the rest wait.</summary>
        public static void Admit(SchoolState s)
        {
            var applications = new int[Count];
            Applications(s, applications);
            for (int i = 0; i < Count; i++)
            {
                SchoolFile f = s.Files[i];
                if (!Private(i))
                {
                    f.Pupils = Math.Min(Def(i).Capacity, Math.Max(0, f.Places));
                    f.Waiting = 0;
                    continue;
                }
                f.Pupils = Math.Min(f.Places, applications[i]);
                f.Waiting = Math.Max(0, applications[i] - f.Places);
            }
        }

        // ---------------------------------------------------------------- teaching and results

        /// <summary>Teachers a school needs for its pupils at the ratio it teaches best at: one at least.</summary>
        public static int Need(SchoolFile f) => Math.Max(1, (Math.Max(0, f.Pupils) + RatioTarget - 1) / RatioTarget);

        /// <summary>How well a school teaches, 0 to 100: the head, the staff's skill, and the pupils to a teacher against the ratio.</summary>
        public static int Teaching(SchoolFile f)
        {
            int ratio = f.Pupils <= 0 ? 100 : (int)Math.Min(100L, 100L * RatioTarget * Math.Max(0, f.Teachers) / f.Pupils);
            int q = (35 * Clamp(f.HeadSkill, 0, 100) + 45 * Clamp(f.Skill, 0, 100) + 20 * ratio + 50) / 100;
            return Clamp(q, 0, 100);
        }

        /// <summary>The ability its pupils come with, 0 to 100: a council school's everyone; a private school's lifted by scholarships and a waiting list to choose from.</summary>
        public static int Intake(SchoolFile f)
        {
            if (!Private(f.Id))
            {
                return 40;
            }
            int choice = f.Places <= 0 ? 0 : (int)Math.Min(20L, 20L * Math.Max(0, f.Waiting) / f.Places);
            return Clamp(45 + 2 * Clamp(f.Scholar, 0, ScholarMost) + choice, 0, 100);
        }

        /// <summary>A results day's score for a school, per cent of good passes: teaching, intake, its name and a year's luck (four points either way, the same everywhere).</summary>
        public static int ResultsOf(SchoolFile f, int year)
        {
            int luck = (int)(Hash(year, f.Id, 7919) % 9u) - 4;
            int score = (60 * Teaching(f) + 25 * Intake(f) + 15 * (Clamp(f.Reputation, 0, 1000) / 10) + 50) / 100 + luck;
            return Clamp(score, 0, 100);
        }

        /// <summary>The inspectors' verdict on a score of teaching and results.</summary>
        public static SchoolGrade GradeOf(SchoolFile f)
        {
            int results = f.Results >= 0 ? f.Results : Teaching(f);
            int score = (60 * Teaching(f) + 40 * results + 50) / 100;
            return score >= 78 ? SchoolGrade.Outstanding : score >= 60 ? SchoolGrade.Good : score >= 45 ? SchoolGrade.Improve : SchoolGrade.Inadequate;
        }

        /// <summary>What a verdict is worth to a school's name, 0 to 100 (none yet: a fair 60).</summary>
        public static int GradeScore(int grade)
        {
            switch ((SchoolGrade)grade)
            {
                case SchoolGrade.Outstanding: return 95;
                case SchoolGrade.Good: return 75;
                case SchoolGrade.Improve: return 50;
                case SchoolGrade.Inadequate: return 25;
                default: return 60;
            }
        }

        /// <summary>The name a school's figures earn it, in tenths of a point: its results, its teaching and its last verdict.</summary>
        public static int ReputationTarget(SchoolFile f)
        {
            int teaching = Teaching(f);
            int results = f.Results >= 0 ? f.Results : teaching;
            return Clamp(5 * results + 3 * teaching + 2 * GradeScore(f.Grade), 0, 1000);
        }

        /// <summary>A day's drift of a school's name toward what it earns: a thirtieth of the way, a tenth of a point at least.</summary>
        public static void Drift(SchoolFile f)
        {
            int target = ReputationTarget(f), gap = target - f.Reputation;
            if (gap == 0)
            {
                return;
            }
            int step = gap / 30;
            f.Reputation = Clamp(f.Reputation + (step != 0 ? step : Math.Sign(gap)), 0, 1000);
        }

        // ---------------------------------------------------------------- staff

        /// <summary>The skill of the teachers a school can hire at its pay: better pay draws better teachers.</summary>
        public static int HireSkill(int payBp) => Clamp(40 + (payBp - PayLeast) / 150, 30, 85);

        /// <summary>
        /// A week's staffing to the ratio, the same for every school (the head hires and lets go): short of teachers, it takes
        /// on up to <see cref="HireMost"/> at the skill its pay draws; two or more over, it lets the spare go but one. Returns
        /// the teachers taken on (negative: let go).
        /// </summary>
        public static int Staff(SchoolFile f)
        {
            int need = Need(f);
            if (f.Teachers < need)
            {
                int hire = Math.Min(HireMost, need - f.Teachers);
                f.Skill = Blend(f.Skill, f.Teachers, HireSkill(f.PayBp), hire);
                f.Teachers += hire;
                return hire;
            }
            if (f.Teachers > need + 2)
            {
                int go = f.Teachers - (need + 1);
                f.Teachers -= go;
                return -go;
            }
            return 0;
        }

        /// <summary>
        /// A term's start: the pay war, the same for every school. In id order, a school short of teachers takes them from the
        /// school paying least, when it pays at least <see cref="PoachPremium"/> of that school's pay: a tenth of its staff at
        /// most, <see cref="PoachMost"/> at most, never its last two; their skill comes with them. Returns how many moved; the
        /// moves that touch the campus are in the news.
        /// </summary>
        public static int Poach(SchoolState s, int day)
        {
            int moved = 0;
            for (int a = 0; a < Count; a++)
            {
                SchoolFile taker = s.Files[a];
                int short_ = Need(taker) - taker.Teachers;
                if (short_ <= 0)
                {
                    continue;
                }
                int from = -1;
                for (int b = 0; b < Count; b++)
                {
                    SchoolFile other = s.Files[b];
                    if (b == a || other.Teachers <= 2 || (long)taker.PayBp * 10000 < (long)other.PayBp * PoachPremium)
                    {
                        continue;
                    }
                    if (from < 0 || other.PayBp < s.Files[from].PayBp)
                    {
                        from = b;
                    }
                }
                if (from < 0)
                {
                    continue;
                }
                SchoolFile giver = s.Files[from];
                int take = Math.Min(short_, Math.Min(PoachMost, Math.Min(Math.Max(1, giver.Teachers * PoachShare / 100), giver.Teachers - 2)));
                if (take <= 0)
                {
                    continue;
                }
                taker.Skill = Blend(taker.Skill, taker.Teachers, giver.Skill, take);
                taker.Teachers += take;
                giver.Teachers -= take;
                moved += take;
                if (a == CampusId || from == CampusId)
                {
                    s.News.Add(new SchoolNews { Day = day, Kind = NewsPoached, School = a, Other = from, Value = take });
                }
            }
            return moved;
        }

        // ---------------------------------------------------------------- money

        /// <summary>A day's takings: the paying pupils' fees (scholars pay none); a council school's the council's grant.</summary>
        public static long Income(SchoolFile f)
        {
            if (!Private(f.Id))
            {
                return (long)Math.Max(0, f.Pupils) * CouncilPerPupil;
            }
            long paying = Math.Max(0, f.Pupils) - (long)Math.Max(0, f.Pupils) * Clamp(f.Scholar, 0, ScholarMost) / 100;
            return paying * Math.Max(0, f.Fee);
        }

        /// <summary>A day's costs: the teachers at their pay, the head, the pupils' running costs and the buildings' upkeep.</summary>
        public static long Costs(SchoolFile f)
        {
            long teachers = (long)Math.Max(0, f.Teachers) * TeacherDay * Clamp(f.PayBp, 0, PayMost) / 10000;
            return teachers + HeadDay + (long)Math.Max(0, f.Pupils) * PupilDay + Def(f.Id).Upkeep;
        }

        public static long Profit(SchoolFile f) => Income(f) - Costs(f);

        /// <summary>What a school is worth: <see cref="ValueDays"/> of its present profit, never under <see cref="ValueFloor"/>.</summary>
        public static long Valuation(SchoolFile f)
        {
            return Math.Max(ValueFloor, Profit(f) * ValueDays);
        }

        /// <summary>What its owner asks of a buyer: the player buying from a trust or the council, and a trust buying from another, alike.</summary>
        public static long Asking(SchoolFile f) => Valuation(f) * AskingPremium / 100;

        /// <summary>What the trusts pay the player for their school.</summary>
        public static long SalePrice(SchoolFile f) => Valuation(f) * SalePart / 100;

        /// <summary>The trust with the most money in its schools, never <paramref name="except"/>; ties to the lower trust. -1 for none.</summary>
        public static int RichestTrust(SchoolState s, int except)
        {
            var cash = new long[Trusts];
            for (int i = 0; i < Count; i++)
            {
                int owner = s.Files[i].Owner;
                if (SchoolOwner.IsTrust(owner))
                {
                    cash[owner] += s.Files[i].Cash;
                }
            }
            int best = -1;
            for (int t = 0; t < Trusts; t++)
            {
                if (t != except && (best < 0 || cash[t] > cash[best]))
                {
                    best = t;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- the owners' levers, one set for all

        /// <summary>
        /// A week's running of a school by the council or a trust, by the same levers the player has: the fee up with a waiting
        /// list and down with empty places; a class opened with a long list and money, closed when two in ten places stand
        /// empty; pay up when short of teachers, down when over; scholarships up while its name is weak, down when strong; a
        /// weak head replaced while it can pay for the search.
        /// </summary>
        public static void Policy(SchoolFile f, int week)
        {
            if (f.Owner == SchoolOwner.Player || !Private(f.Id))
            {
                if (f.Owner != SchoolOwner.Player)
                {
                    Staff(f);   // the council's schools keep their staff to the ratio, their fees and places the council's own
                }
                return;
            }
            if (f.Waiting > f.Places / 10)
            {
                StepFee(f, +1);
            }
            else if (f.Pupils < f.Places * 85 / 100)
            {
                StepFee(f, -1);
            }
            if (f.Waiting > f.Places * 15 / 100 && f.Cash >= 0)
            {
                StepClass(f, +1);
            }
            else if (f.Pupils < f.Places * 70 / 100)
            {
                StepClass(f, -1);
            }
            int need = Need(f);
            if (f.Teachers < need)
            {
                StepPay(f, +1);
            }
            else if (f.Teachers > need + 1 && f.PayBp > 10000)
            {
                StepPay(f, -1);
            }
            if (f.Reputation < 500)
            {
                StepScholar(f, +1);
            }
            else if (f.Reputation > 750)
            {
                StepScholar(f, -1);
            }
            if (f.HeadSkill < 50 && f.Cash > (long)HeadDay * HeadHireDays)
            {
                Candidate(f.Id, week, BestCandidate(f.Id, week), out int seed, out int skill);
                if (skill > f.HeadSkill)
                {
                    f.Cash -= HireCost(skill);
                    f.Head = seed;
                    f.HeadSkill = skill;
                }
            }
            Staff(f);
        }

        /// <summary>The fee a step up or down, within its bounds; false when it is at its bound already.</summary>
        public static bool StepFee(SchoolFile f, int way) => Step(ref f.Fee, way * FeeStep, FeeLeast, FeeMost);

        /// <summary>A class opened or closed, within the buildings' places and never the last; false at a bound.</summary>
        public static bool StepClass(SchoolFile f, int way)
        {
            bool done = Step(ref f.Places, way * ClassSize, ClassSize, Def(f.Id).Capacity / ClassSize * ClassSize);
            f.Pupils = Math.Min(f.Pupils, f.Places);
            return done;
        }

        /// <summary>The teachers' pay a step up or down against the going rate, within its bounds.</summary>
        public static bool StepPay(SchoolFile f, int way) => Step(ref f.PayBp, way * PayStep, PayLeast, PayMost);

        /// <summary>Scholarships a step up or down, within their bounds.</summary>
        public static bool StepScholar(SchoolFile f, int way) => Step(ref f.Scholar, way * ScholarStep, 0, ScholarMost);

        private static bool Step(ref int value, int by, int least, int most)
        {
            int next = Clamp(value + by, least, most);
            if (next == value)
            {
                return false;
            }
            value = next;
            return true;
        }

        // ---------------------------------------------------------------- headteachers

        /// <summary>A head's seed for a school: the game makes their name from it (the same on every machine).</summary>
        public static int HeadSeed(int school, int n) => (int)(Hash(school, n, 104729) & 0x3fffffff) + 1;

        /// <summary>
        /// One of the three candidates for a school's headship in a week: their seed and their skill, 40 to 90. The same three
        /// all week, other ones the next.
        /// </summary>
        public static void Candidate(int school, int week, int n, out int seed, out int skill)
        {
            seed = HeadSeed(school, week * 3 + Clamp(n, 0, 2) + 1);
            skill = 40 + (int)(Hash(seed, school, 7) % 51u);
        }

        /// <summary>The best of a week's three candidates (the first of equals).</summary>
        public static int BestCandidate(int school, int week)
        {
            int best = 0, bestSkill = -1;
            for (int n = 0; n < 3; n++)
            {
                Candidate(school, week, n, out _, out int skill);
                if (skill > bestSkill)
                {
                    bestSkill = skill;
                    best = n;
                }
            }
            return best;
        }

        /// <summary>What hiring a head costs: the search, days of their pay, more for a better one.</summary>
        public static long HireCost(int skill) => (long)HeadDay * HeadHireDays * (100 + Clamp(skill, 0, 100)) / 100;

        // ---------------------------------------------------------------- the calendar

        /// <summary>A term's days: a third of the game's year, a day at least.</summary>
        public static int TermDays(int year) => Math.Max(1, Math.Max(1, year) / 3);

        /// <summary>The term a day is in, counted from the first.</summary>
        public static int TermOf(int day, int year) => (Math.Max(1, day) - 1) / TermDays(year);

        /// <summary>The year a day is in, from 1.</summary>
        public static int YearOf(int day, int year) => (Math.Max(1, day) - 1) / Math.Max(1, year) + 1;

        /// <summary>Results day: the end of the summer term, two thirds through the game's year.</summary>
        public static bool ResultsDay(int day, int year)
        {
            year = Math.Max(1, year);
            int dayOfYear = (Math.Max(1, day) - 1) % year + 1;
            return dayOfYear == Math.Max(1, year * 2 / 3);
        }

        /// <summary>A school's inspection day: every <see cref="InspectEvery"/> days, each school on its own.</summary>
        public static bool InspectionDay(int day, int school) => (day + school * 7) % InspectEvery == 0;

        /// <summary>The week's end: the owners' week of running, and the player's school's books.</summary>
        public static bool WeekDay(int day) => day > 0 && day % 7 == 0;

        /// <summary>Monday to Friday (the game's day 1 is a Monday).</summary>
        public static bool SchoolDay(int day) => (Math.Max(1, day) - 1) % 7 < 5;

        public const int OpensAt = 7, ClosesAt = 18;   // game hours the main school is open on a school day: the school run's first to the last club's end

        /// <summary>True while the main school is open: a school day, from <see cref="OpensAt"/> to <see cref="ClosesAt"/> (whole game hours, as the game's cards count them).</summary>
        public static bool Open(int day, int hour) => SchoolDay(day) && hour >= OpensAt && hour < ClosesAt;

        /// <summary>
        /// Hours to the next change of the main school's state from this hour of this day, and the hour of the day it changes at: while open, the hours
        /// to its closing; while closed, the hours to the next opening, over the nights and the weekend (never more than 3 days: the week has school days).
        /// </summary>
        public static int HoursToChange(int day, int hour, out int atHour)
        {
            hour = Math.Max(0, Math.Min(23, hour));
            if (Open(day, hour))
            {
                atHour = ClosesAt;
                return ClosesAt - hour;
            }
            atHour = OpensAt;
            int hours = 0;
            int d = Math.Max(1, day);
            int h = hour;
            while (hours < 24 * 8)
            {
                h++;
                hours++;
                if (h == 24)
                {
                    h = 0;
                    d++;
                }
                if (Open(d, h))
                {
                    return hours;
                }
            }
            return hours;
        }

        /// <summary>True in the school run's hours of a school day (minutes of the day).</summary>
        public static bool SchoolRun(int day, int minute)
        {
            return SchoolDay(day) && ((minute >= RunMorningFrom && minute < RunMorningTo) || (minute >= RunAfternoonFrom && minute < RunAfternoonTo));
        }

        public const int BreakFrom = 630, BreakTo = 650;     // minutes of a school day: the morning break, 10:30 to 10:50 ...
        public const int LunchFrom = 750, LunchTo = 810;     // ... lunch, 12:30 to 13:30 ...
        public const int AfterFrom = 930, AfterTo = 1050;    // ... and the afternoon's clubs and games, 15:30 to 17:30

        /// <summary>
        /// Who is out on the campus at a minute of a day, as <see cref="CrowdWhen"/> flags: the school run's hours (families and pupils at the gates and the
        /// door), the morning break and lunch (the courtyard, the court, the green and the yards), the afternoon's clubs and games (the court and the field),
        /// and a term's open day. None on a weekend, and none at night: the school is quiet.
        /// </summary>
        public static CrowdWhen CrowdMoments(int day, int minute, int year)
        {
            if (!SchoolDay(day))
            {
                return CrowdWhen.None;
            }
            CrowdWhen f = CrowdWhen.None;
            f |= SchoolRun(day, minute) ? CrowdWhen.Run : CrowdWhen.None;
            f |= minute >= BreakFrom && minute < BreakTo ? CrowdWhen.Break : CrowdWhen.None;
            f |= minute >= LunchFrom && minute < LunchTo ? CrowdWhen.Lunch : CrowdWhen.None;
            f |= minute >= AfterFrom && minute < AfterTo ? CrowdWhen.After : CrowdWhen.None;
            f |= OpenDay(day, year) && minute >= OpenDayFrom * 60 && minute < OpenDayTo * 60 ? CrowdWhen.OpenDay : CrowdWhen.None;
            return f;
        }

        /// <summary>A term's open day: its third day, in its hours.</summary>
        public static bool OpenDay(int day, int year) => (Math.Max(1, day) - 1) % TermDays(year) == Math.Min(2, TermDays(year) - 1);

        // ---------------------------------------------------------------- a day

        /// <summary>
        /// One day of the market, the same on every machine with the same state: every name drifts and every school's day of
        /// money is kept (the player's for the week's book); a term's start admits the families and runs the pay war; the
        /// week's end runs every owner's levers but the player's, staffs the player's school, sells a trust school deep in the
        /// red, closes the council's opening sale and brings a trust's offer for the player's school; results day and the
        /// inspections come on their days. In a Going Public session nothing is bought or sold. <paramref name="council"/> is the
        /// council's money for schools (its grants lever: +1 more, -1 less, 0 as the city came). Returns what the player hears of.
        /// </summary>
        public static SchoolDayFlags Day(SchoolState s, int day, int year, bool inSession, int council)
        {
            var flags = SchoolDayFlags.None;
            if (s.Opened <= 0)
            {
                s.Opened = day;
                // In the next morning's paper, whatever the hour it opened.
                s.News.Add(new SchoolNews { Day = day + 1, Kind = NewsOpened, School = CampusId, Value = s.Campus.Places, Other = day + SaleDays });
            }
            for (int i = 0; i < Count; i++)
            {
                SchoolFile f = s.Files[i];
                Drift(f);
                long profit = Profit(f);
                if (f.Owner == SchoolOwner.Player)
                {
                    f.Week += profit;
                }
                else
                {
                    f.Cash += profit;
                }
            }
            int term = TermOf(day, year);
            if (term != s.Term)
            {
                s.Term = term;
                Admit(s);
                int before = s.News.Count;
                Poach(s, day);
                flags |= SchoolDayFlags.TermStarted | (s.News.Count > before ? SchoolDayFlags.Poached : SchoolDayFlags.None);
            }
            if (OpenDay(day, year))
            {
                s.News.Add(new SchoolNews { Day = day, Kind = NewsOpenDay, School = CampusId });
                flags |= SchoolDayFlags.OpenDay;
            }
            if (WeekDay(day))
            {
                int week = day / 7;
                for (int i = 0; i < Count; i++)
                {
                    SchoolFile f = s.Files[i];
                    f.LossWeeks = Profit(f) < 0 ? f.LossWeeks + 1 : 0;
                    if (f.Owner == SchoolOwner.Player)
                    {
                        Staff(f);
                    }
                    else
                    {
                        Policy(f, week);
                    }
                    if (!Private(i))
                    {
                        f.Skill += Math.Sign(CouncilSkill(i, council) - f.Skill);   // the council's teachers follow its money for schools, a point a week
                    }
                }
                if (!inSession)
                {
                    flags |= Trade(s, day);
                }
                if (s.Campus.Owner == SchoolOwner.Player)
                {
                    flags |= SchoolDayFlags.WeekBooked;
                }
            }
            if (s.OfferPrice > 0 && day > s.OfferEnd)
            {
                s.OfferPrice = 0;
                s.OfferBy = -1;
                flags |= SchoolDayFlags.OfferLapsed;
            }
            int y = YearOf(day, year);
            if (ResultsDay(day, year) && y != s.Year)
            {
                s.Year = y;
                Results(s, day, y);
                flags |= SchoolDayFlags.Results;
            }
            for (int i = 0; i < Count; i++)
            {
                if (InspectionDay(day, i))
                {
                    Inspect(s, i, day);
                    flags |= i == CampusId ? SchoolDayFlags.Inspected : SchoolDayFlags.None;
                }
            }
            s.LastDay = day;
            return flags;
        }

        // The week's buying and selling, never the player's without them: the council's opening sale to the richest trust;
        // a trust school deep in the red to the richest other trust; a trust's offer for the player's school.
        private static SchoolDayFlags Trade(SchoolState s, int day)
        {
            var flags = SchoolDayFlags.None;
            SchoolFile campus = s.Campus;
            if (campus.Owner == SchoolOwner.Council && day >= s.Opened + SaleDays)
            {
                int buyer = RichestTrust(s, -1);
                if (buyer >= 0)
                {
                    Sell(s, CampusId, buyer, Asking(campus), day);
                    flags |= SchoolDayFlags.Sold;
                }
            }
            for (int i = 0; i < Count; i++)
            {
                SchoolFile f = s.Files[i];
                if (!SchoolOwner.IsTrust(f.Owner) || f.LossWeeks < SellAfterWeeks)
                {
                    continue;
                }
                int buyer = RichestTrust(s, f.Owner);
                if (buyer >= 0)
                {
                    Sell(s, i, buyer, Asking(f), day);
                }
            }
            if (campus.Owner == SchoolOwner.Player && s.OfferPrice == 0 && s.OwnedSince > 0 && day - s.OwnedSince >= OfferAfter && (day / 7) % 4 == 0)
            {
                int buyer = RichestTrust(s, -1);
                if (buyer >= 0)
                {
                    s.OfferPrice = (int)Math.Min(int.MaxValue, Valuation(campus) * OfferPart / 100 / 1000);
                    s.OfferBy = buyer;
                    s.OfferEnd = day + OfferDays;
                    flags |= SchoolDayFlags.Offer;
                }
            }
            return flags;
        }

        /// <summary>
        /// A school changes hands: the buyer's account pays, the seller's (a trust's, the council's) is paid, its account
        /// starts afresh with the buyer. The player's money is the game's own: the director books it.
        /// </summary>
        public static void Sell(SchoolState s, int school, int buyer, long price, int day)
        {
            SchoolFile f = s.Files[school];
            int seller = f.Owner;
            if (SchoolOwner.IsTrust(seller))
            {
                int pay = FirstOf(s, seller, school);
                if (pay >= 0)
                {
                    s.Files[pay].Cash += price;
                }
            }
            if (SchoolOwner.IsTrust(buyer))
            {
                int from = FirstOf(s, buyer, -1);
                if (from >= 0)
                {
                    s.Files[from].Cash -= price;
                }
            }
            f.Owner = buyer;
            f.Cash = 0;
            f.Week = 0;
            f.LossWeeks = 0;
            if (school == CampusId)
            {
                s.OwnedSince = buyer == SchoolOwner.Player ? day : 0;
                s.OfferPrice = 0;
                s.OfferBy = -1;
            }
            s.News.Add(new SchoolNews { Day = day + 1, Kind = NewsSold, School = school, Other = buyer, Value = price });   // a sale at any hour makes the next morning's paper
        }

        // The first school a trust holds but one: where its money is kept when it buys or sells another.
        private static int FirstOf(SchoolState s, int trust, int except)
        {
            for (int i = 0; i < Count; i++)
            {
                if (i != except && s.Files[i].Owner == trust)
                {
                    return i;
                }
            }
            return -1;
        }

        // Results day: every school's score, the league table in the news (the first school and the campus's place).
        private static void Results(SchoolState s, int day, int year)
        {
            for (int i = 0; i < Count; i++)
            {
                s.Files[i].Results = ResultsOf(s.Files[i], year);
            }
            int[] order = League(s);
            int campusRank = Array.IndexOf(order, CampusId) + 1;
            s.News.Add(new SchoolNews { Day = day + 1, Kind = NewsResults, School = order[0], Other = campusRank, Value = s.Files[order[0]].Results });
        }

        /// <summary>Results day now, whatever the day (the tests): every school's score and the league table in the news.</summary>
        public static void ResultsNow(SchoolState s, int day, int year)
        {
            s.Year = year;
            Results(s, day, year);
        }

        /// <summary>A school inspected now, whatever the day (the tests).</summary>
        public static void InspectNow(SchoolState s, int school, int day) => Inspect(s, Math.Max(0, Math.Min(Count - 1, school)), day);

        /// <summary>A term's start now (the tests): the families placed and the pay war run. Returns the teachers moved.</summary>
        public static int TermNow(SchoolState s, int day)
        {
            Admit(s);
            return Poach(s, day);
        }

        /// <summary>The league table: every school by its last results, best first, equals by id.</summary>
        public static int[] League(SchoolState s)
        {
            var order = new int[Count];
            for (int i = 0; i < Count; i++)
            {
                order[i] = i;
            }
            Array.Sort(order, (a, b) =>
            {
                int byResults = s.Files[b].Results.CompareTo(s.Files[a].Results);
                return byResults != 0 ? byResults : a.CompareTo(b);
            });
            return order;
        }

        // An inspection: the verdict on the day, in the news for the campus and for any school rated outstanding or inadequate.
        private static void Inspect(SchoolState s, int school, int day)
        {
            SchoolFile f = s.Files[school];
            f.Grade = (int)GradeOf(f);
            f.Inspected = day;
            if (school == CampusId || f.Grade == (int)SchoolGrade.Outstanding || f.Grade == (int)SchoolGrade.Inadequate)
            {
                s.News.Add(new SchoolNews { Day = day + 1, Kind = NewsInspected, School = school, Value = f.Grade });
            }
        }

        /// <summary>The news kept: what a morning still prints, the last days' only (the line stays short).</summary>
        public static void TrimNews(SchoolState s, int today)
        {
            for (int i = s.News.Count - 1; i >= 0; i--)
            {
                if (s.News[i].Day < today - 2)
                {
                    s.News.RemoveAt(i);
                }
            }
            while (s.News.Count > 24)
            {
                s.News.RemoveAt(0);
            }
        }

        /// <summary>The skill a council school's teachers settle at under the council's money for schools (-1, 0 or +1).</summary>
        public static int CouncilSkill(int school, int council) => Clamp(Def(school).Skill + Math.Sign(council) * CouncilSway, 0, 100);

        // ---------------------------------------------------------------- the district round the campus

        /// <summary>
        /// The traffic points a lot of the district gets from the campus's name: none before it opens or under a name of 600;
        /// 2, 4 or 6 next to it with a name of 600, 700 or 800, falling to none at <see cref="LiftReach"/> m from its ground
        /// (families want to live near a good school, and the shops round it see them). The same on every machine.
        /// </summary>
        public static int Lift(float x, float z, int reputation, bool opened, float x0, float z0, float x1, float z1)
        {
            int level = !opened ? 0 : reputation >= 800 ? 6 : reputation >= 700 ? 4 : reputation >= 600 ? 2 : 0;
            if (level == 0)
            {
                return 0;
            }
            double dx = x < x0 ? x0 - x : x > x1 ? x - x1 : 0.0, dz = z < z0 ? z0 - z : z > z1 ? z - z1 : 0.0;
            double d = Math.Sqrt(dx * dx + dz * dz);
            return d >= LiftReach ? 0 : (int)Math.Round(level * (LiftReach - d) / LiftReach, MidpointRounding.AwayFromZero);
        }

        // ---------------------------------------------------------------- the player's children

        /// <summary>A child's reports at the campus: points over or under a public school's, from its last results.</summary>
        public static float Standing(SchoolFile campus)
        {
            int results = campus.Results >= 0 ? campus.Results : Teaching(campus);
            return (results - StandingAtFifty) / 2.5f;
        }

        /// <summary>True when the campus has a place for one more child now.</summary>
        public static bool HasPlace(SchoolFile campus) => campus.Pupils < campus.Places;

        // ---------------------------------------------------------------- helpers

        public static int Clamp(int value, int least, int most) => value < least ? least : value > most ? most : value;

        // The skill of a staff after some join at another skill.
        private static int Blend(int skill, int count, int joining, int joined)
        {
            int total = Math.Max(0, count) + Math.Max(0, joined);
            return total <= 0 ? skill : (int)Math.Round(((double)skill * Math.Max(0, count) + (double)joining * Math.Max(0, joined)) / total, MidpointRounding.AwayFromZero);
        }

        /// <summary>FNV-1a over three numbers: the market's only chance, the same on every machine.</summary>
        public static uint Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)c) * 16777619u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return h;
            }
        }
    }
}
