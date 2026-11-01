using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// Life (POPULATION.md section 4; the brief's fourth step), a person's day at a time, seeded by the day: birthdays move people from home to
    /// school, from school to university or work, from university to work, and to retirement at 65 with a pension; couples form among single
    /// adults and move in together, have children by the mother's age, and part; grown children leave home and form households of their own
    /// (what makes the demand for homes); people die by their age (Gompertz's law over US life tables). Rates are by the year and the save's
    /// year turns them into days. People of other systems (the game's staff, the player's family) and the player are never ended or moved here:
    /// their own systems own them. Game-free. COST: nothing per frame; a person's day a few comparisons and a draw or two.
    /// </summary>
    public static class LifeRules
    {
        public const int CoupleFrom = 22, CoupleTo = 45;   // years: the ages singles pair up between
        public const int LeaveFrom = 20, LeaveTo = 34;     // years: grown children leave home between
        public const int MotherFrom = 15, MotherTo = 49;   // years
        public const int AgeGapMost = 8;                   // years between partners at most
        public const int SameSexShare = 50;                // thousandths of couples of the same sex
        public const int PartnerLooks = 10;                // single adults a seeker looks at

        /// <summary>The day's life, as it is lived: who is looking for a partner (and has been this month), who leaves home, the free flats, the day's births and deaths.</summary>
        public sealed class LifeDay
        {
            public readonly List<int> Seeking = new List<int>(256);
            // singles who looked on an earlier day of the month and found nobody: they look again with each day's, so a month's singles meet as many
            // others at any year length (by the day alone a year of 365 days met a sixth as many each day and paired two fifths fewer)
            public readonly List<int> Waiting = new List<int>(256);
            public readonly List<int> WaitingIds = new List<int>(256);   // their ids: a slot freed and given again in the month is somebody else
            public readonly List<int> Leaving = new List<int>(64);
            public readonly List<int> Splitting = new List<int>(32);
            public List<int>[] Free = new List<int>[0];
            public int Births, Deaths, Formed, Paired, Parted, Left;

            public void Begin(PopulationState s)
            {
                Seeking.Clear();
                Leaving.Clear();
                Splitting.Clear();
                Births = Deaths = Formed = Paired = Parted = Left = 0;
                if (Free.Length != s.Districts.Count)
                {
                    Free = new List<int>[s.Districts.Count];
                }
                HousingRules.FreeFlats(s, Free);
            }
        }

        /// <summary>A person's day of life. A person may end in it (death), or be marked to leave home or look for a partner at the day's end.</summary>
        public static void Person(PopulationState s, LifeDay ld, int p, int day)
        {
            PersonTable pt = s.People;
            if (pt.Id[p] == 0 || (pt.Flags[p] & (PersonFlags.Frozen | PersonFlags.Linked)) != 0)
            {
                return;
            }
            int id = pt.Id[p], age = s.Age(p, day);
            int h = pt.Household[p];
            if (h < 0)
            {
                return;
            }
            if (Dies(s, id, age, day))
            {
                Die(s, ld, p, day);
                return;
            }
            if ((day - pt.Birth[p]) % Math.Max(1, s.YearDays) == 0)
            {
                Birthday(s, p, age, day);
            }
            var status = (Status)pt.Status[p];
            int cls = s.Households.Class[h];
            if (pt.Female[p] == 1 && age >= MotherFrom && age <= MotherTo)
            {
                int rate = PopulationCatalogue.Fertility[PopulationCatalogue.Clamp((age - MotherFrom) / 5, 0, PopulationCatalogue.Fertility.Length - 1)];
                if (pt.Partner[p] < 0 || pt.Household[pt.Partner[p]] != h)
                {
                    rate = rate * PopulationCatalogue.LoneBirths / 1000;   // a woman with no partner at home has children too, less often
                }
                if (PopDraw.Daily(s.Seed, PopDraw.Birth, id, day, rate, s.YearDays))
                {
                    Born(s, ld, h, day, cls);
                }
            }
            if (age >= CoupleFrom && age <= CoupleTo && pt.Partner[p] < 0 && status != Status.Student
                && PopDraw.Daily(s.Seed, PopDraw.Couple, id, day, PopulationCatalogue.CoupleFormation, s.YearDays))
            {
                ld.Seeking.Add(p);
            }
            if (pt.Role[p] == (byte)Role.Head && pt.Partner[p] >= 0 && pt.Household[pt.Partner[p]] == h
                && PopDraw.Daily(s.Seed, PopDraw.Split, id, day, PopulationCatalogue.CoupleSplit, s.YearDays))
            {
                ld.Splitting.Add(pt.Partner[p]);
            }
            if (pt.Role[p] == (byte)Role.Child && age >= LeaveFrom && age <= LeaveTo && status == Status.Worker
                && PopDraw.Daily(s.Seed, PopDraw.Leave, id, day, PopulationCatalogue.LeaveHome, s.YearDays))
            {
                ld.Leaving.Add(p);
            }
        }

        /// <summary>True when a person dies on a day: Gompertz's yearly rate by their age (a newborn's own), as a day's chance.</summary>
        public static bool Dies(PopulationState s, int id, int age, int day)
        {
            int perMille = age >= PopulationCatalogue.OldestAge ? 1000000 : YearlyDeath(age);
            return PopDraw.Daily(s.Seed, PopDraw.Death, id, day, perMille, s.YearDays);
        }

        /// <summary>The chance of dying within a year at an age, thousandths (more than a thousand reads as certain).</summary>
        public static int YearlyDeath(int age)
        {
            if (age <= 0)
            {
                return PopulationCatalogue.InfantMortality;
            }
            double q = PopulationCatalogue.MortalityA / 1000000.0 * Math.Exp(PopulationCatalogue.MortalityB / 1000.0 * age);
            return (int)Math.Min(1000000, Math.Round(q * 1000));
        }

        // A birthday that changes what a person does: school at five, the end of school at eighteen, a degree finished, retirement at 65.
        private static void Birthday(PopulationState s, int p, int age, int day)
        {
            PersonTable pt = s.People;
            int h = pt.Household[p];
            int cls = h >= 0 ? s.Households.Class[h] : 1;
            int id = pt.Id[p];
            int head = h >= 0 ? s.Households.First[h] : -1;
            ushort parent = pt.Role[p] == (byte)Role.Child && head >= 0 && head != p ? pt.Games[head] : (ushort)0;
            pt.Games[p] = GameRules.Birthday(s.Seed, id, day, age, cls, pt.Female[p] == 1, pt.Games[p], parent);   // tastes change slowly with age
            var status = (Status)pt.Status[p];
            if (age == PopulationCatalogue.SchoolFrom && status == Status.Child)
            {
                pt.Status[p] = (byte)Status.Pupil;
                pt.Since[p] = day;
                return;
            }
            if (age == PopulationCatalogue.SchoolTo && status == Status.Pupil)
            {
                bool dropped = PopDraw.Mille(s.Seed, PopDraw.Education, id, day, PopulationCatalogue.DropOutShare[cls]);
                pt.Education[p] = (byte)(dropped ? Education.None : Education.School);
                pt.Since[p] = day;
                if (!dropped && PopDraw.Mille(s.Seed, PopDraw.University, id, day, PopulationCatalogue.UniversityShare[cls]))
                {
                    pt.Status[p] = (byte)Status.Student;
                    pt.Field[p] = (byte)GenerateRules.DrawField(s.Seed, id, Education.Degree);
                    return;
                }
                if (!dropped && PopDraw.Mille(s.Seed, PopDraw.Education, id, day + 1, PopulationCatalogue.CollegeShare[cls]))
                {
                    pt.Education[p] = (byte)Education.College;
                }
                ToLabourForce(s, p, day, cls);
                return;
            }
            if (status == Status.Student)
            {
                if (age == PopulationCatalogue.SchoolTo + PopulationCatalogue.UniversityYears && pt.Education[p] < (byte)Education.Degree)
                {
                    pt.Education[p] = (byte)Education.Degree;
                    if (!PopDraw.Mille(s.Seed, PopDraw.University, id, day, PopulationCatalogue.PostgraduateShare[cls]))
                    {
                        ToLabourForce(s, p, day, cls);
                    }
                    return;
                }
                if (age >= PopulationCatalogue.SchoolTo + PopulationCatalogue.UniversityYears + PopulationCatalogue.PostgraduateYears)
                {
                    pt.Education[p] = (byte)Education.Postgraduate;
                    ToLabourForce(s, p, day, cls);
                }
                return;
            }
            if (age == PopulationCatalogue.RetireAge && (status == Status.Worker || status == Status.Unemployed || status == Status.Home)
                && !PopDraw.Mille(s.Seed, PopDraw.Retire, id, day, PopulationCatalogue.WorkingOld))
            {
                Retire(s, p, day);
                return;
            }
            if (age == PopulationCatalogue.RetireAge + 5 && (status == Status.Worker || status == Status.Unemployed))
            {
                Retire(s, p, day);
            }
        }

        private static void Retire(PopulationState s, int p, int day)
        {
            PersonTable pt = s.People;
            if (pt.Status[p] == (byte)Status.Worker)
            {
                LabourRules.Lose(s, p, day, true);
            }
            pt.Status[p] = (byte)Status.Retired;
            pt.Since[p] = day;
            if (pt.Pay[p] <= 0)
            {
                pt.Pay[p] = LabourRules.PayFor(s, Math.Max(1, (int)pt.Field[p]), pt.Skill[p], 1);
                pt.Hours[p] = PopulationCatalogue.FullTimeHours;
            }
        }

        // School or university left: into the labour force (looking for work) or, by the class's participation, home.
        private static void ToLabourForce(PopulationState s, int p, int day, int cls)
        {
            PersonTable pt = s.People;
            pt.Since[p] = day;
            if (PopDraw.Mille(s.Seed, PopDraw.Status, pt.Id[p], day, PopulationCatalogue.Participation[cls]))
            {
                pt.Status[p] = (byte)Status.Unemployed;
                if (pt.Field[p] == 0)
                {
                    pt.Field[p] = (byte)GenerateRules.DrawField(s.Seed, pt.Id[p], (Education)pt.Education[p]);
                }
                pt.Pay[p] = LabourRules.PayFor(s, pt.Field[p], pt.Skill[p], cls);
                pt.Hours[p] = PopulationCatalogue.FullTimeHours;
            }
            else
            {
                pt.Status[p] = (byte)Status.Home;
            }
        }

        // A child born into a household.
        private static void Born(PopulationState s, LifeDay ld, int h, int day, int cls)
        {
            int p = s.People.Add(s.NextPerson++);
            PersonTable pt = s.People;
            pt.Birth[p] = day;
            pt.Female[p] = (byte)(PopDraw.Mille(s.Seed, PopDraw.Sex, pt.Id[p], day, 488) ? 1 : 0);
            pt.Role[p] = (byte)Role.Child;
            pt.Status[p] = (byte)Status.Child;
            pt.Since[p] = day;
            GenerateRules.Join(s, h, p);
            ld.Births++;
            s.Births++;
            int d = s.Households.District[h];
            if (d < s.Figures.Length)
            {
                s.Figures[d].Births++;
            }
        }

        /// <summary>A person dies: out of their job and household; a household left with nobody, or with children alone, is gone and its flat freed.</summary>
        public static void Die(PopulationState s, LifeDay ld, int p, int day)
        {
            PersonTable pt = s.People;
            int h = pt.Household[p];
            if (pt.Job[p] >= 0)
            {
                LabourRules.Lose(s, p, day, false);
            }
            int partner = pt.Partner[p];
            if (partner >= 0 && pt.Alive(partner))
            {
                pt.Partner[partner] = -1;
            }
            GenerateRules.Leave(s, p);
            pt.Remove(p);
            if (ld != null)
            {
                ld.Deaths++;
            }
            s.Deaths++;
            if (h >= 0)
            {
                int d = s.Households.District[h];
                if (d < s.Figures.Length)
                {
                    s.Figures[d].Deaths++;
                }
                AfterLoss(s, h, day);
            }
        }

        // A household that lost a member: with nobody left, or children alone (taken in by family beyond the city), it is gone.
        private static void AfterLoss(PopulationState s, int h, int day)
        {
            HouseholdTable hh = s.Households;
            PersonTable pt = s.People;
            bool adult = false;
            for (int q = hh.First[h]; q >= 0; q = pt.Next[q])
            {
                adult |= s.Age(q, day) >= PopulationCatalogue.AdultLeast;
            }
            if (adult)
            {
                MakeHead(s, h);
                return;
            }
            while (hh.First[h] >= 0)
            {
                int q = hh.First[h];
                GenerateRules.Leave(s, q);
                pt.Remove(q);
            }
            Dissolve(s, h);
        }

        // A household's head again after a loss or a move: the eldest adult, their partner its partner.
        private static void MakeHead(PopulationState s, int h)
        {
            HouseholdTable hh = s.Households;
            PersonTable pt = s.People;
            int head = -1;
            for (int q = hh.First[h]; q >= 0; q = pt.Next[q])
            {
                if (pt.Role[q] == (byte)Role.Head)
                {
                    return;
                }
                if (head < 0 || pt.Birth[q] < pt.Birth[head])
                {
                    head = q;
                }
            }
            if (head >= 0)
            {
                pt.Role[head] = (byte)Role.Head;
                int partner = pt.Partner[head];
                if (partner >= 0 && pt.Household[partner] == h)
                {
                    pt.Role[partner] = (byte)Role.Partner;
                }
            }
        }

        /// <summary>A household with nobody left: its flat freed, its slot free.</summary>
        public static void Dissolve(PopulationState s, int h)
        {
            HousingRules.Vacate(s, h);
            s.Households.Remove(h);
        }

        /// <summary>
        /// The day's end: singles who looked pair up and move in together (into the larger flat), partners who part and grown children who leave
        /// form households of their own in a free flat (the demand for homes; with none free a partner who parts leaves the city, a child stays and
        /// is counted as wanting a flat in their district).
        /// </summary>
        public static void EndOfDay(PopulationState s, LifeDay ld, int day)
        {
            Pair(s, ld, day);
            for (int i = 0; i < ld.Splitting.Count; i++)
            {
                int p = ld.Splitting[i];
                if (s.People.Alive(p) && s.People.Partner[p] >= 0)
                {
                    int partner = s.People.Partner[p];
                    s.People.Partner[p] = -1;
                    s.People.Partner[partner] = -1;
                    ld.Parted++;
                    if (!Form(s, ld, p, day, true))
                    {
                        s.Growth.Unformed++;   // a home the city lacked: the housing pressure counts it
                        LeaveCity(s, p, day);
                    }
                }
            }
            for (int i = 0; i < ld.Leaving.Count; i++)
            {
                int p = ld.Leaving[i];
                if (s.People.Alive(p) && s.People.Household[p] >= 0 && !Form(s, ld, p, day, false))
                {
                    int d = s.Households.District[s.People.Household[p]];
                    if (d < s.WantFlats.Length)
                    {
                        s.WantFlats[d]++;
                    }
                    s.Growth.Unformed++;
                }
            }
        }

        // Singles who looked today and earlier in the month, paired: each with one of the others of a fitting sex and age, the nearest district first;
        // those still single wait for the next day's.
        private static void Pair(PopulationState s, LifeDay ld, int day)
        {
            PersonTable pt = s.People;
            List<int> seeking = ld.Seeking;
            for (int i = 0; i < ld.Waiting.Count; i++)
            {
                int w = ld.Waiting[i];
                if (pt.Alive(w) && pt.Id[w] == ld.WaitingIds[i] && pt.Partner[w] < 0 && pt.Household[w] >= 0 && !seeking.Contains(w))
                {
                    seeking.Add(w);
                }
            }
            ld.Waiting.Clear();
            ld.WaitingIds.Clear();
            PairAll(s, seeking, day, ld);
            for (int i = 0; i < seeking.Count; i++)
            {
                int w = seeking[i];
                if (pt.Alive(w) && pt.Partner[w] < 0 && pt.Household[w] >= 0)
                {
                    ld.Waiting.Add(w);
                    ld.WaitingIds.Add(pt.Id[w]);
                }
            }
        }

        private static void PairAll(PopulationState s, List<int> seeking, int day, LifeDay ld)
        {
            PersonTable pt = s.People;
            for (int i = 0; i < seeking.Count; i++)
            {
                int a = seeking[i];
                if (!pt.Alive(a) || pt.Partner[a] >= 0 || pt.Household[a] < 0)
                {
                    continue;
                }
                bool same = PopDraw.Mille(s.Seed, PopDraw.Partner, pt.Id[a], day, SameSexShare);
                int best = -1, bestScore = int.MaxValue;
                for (int look = 0; look < PartnerLooks && seeking.Count > 1; look++)
                {
                    int b = seeking[PopDraw.Range(s.Seed, PopDraw.Partner, pt.Id[a], day * 16 + look, seeking.Count)];
                    if (b == a || !pt.Alive(b) || pt.Partner[b] >= 0 || pt.Household[b] < 0 || pt.Household[b] == pt.Household[a] || (pt.Female[a] == pt.Female[b]) != same)
                    {
                        continue;
                    }
                    int gap = Math.Abs(pt.Birth[a] - pt.Birth[b]) / Math.Max(1, s.YearDays);
                    if (gap > AgeGapMost)
                    {
                        continue;
                    }
                    int score = gap * 100 + CityTable.Distance(s, s.Households.District[pt.Household[a]], s.Households.District[pt.Household[b]]) / 10;
                    if (score < bestScore)
                    {
                        best = b;
                        bestScore = score;
                    }
                }
                if (best >= 0)
                {
                    MoveIn(s, a, best, day);
                    ld.Paired++;
                }
            }
        }

        // Two singles become a couple: the one in the smaller flat moves into the other's household.
        private static void MoveIn(PopulationState s, int a, int b, int day)
        {
            PersonTable pt = s.People;
            int ha = pt.Household[a], hb = pt.Household[b];
            int ba = s.Households.Building[ha], bb = s.Households.Building[hb];
            int sqmA = ba >= 0 ? CityTable.FlatSqmOf(s, ba, s.Households.Unit[ha]) : 0, sqmB = bb >= 0 ? CityTable.FlatSqmOf(s, bb, s.Households.Unit[hb]) : 0;
            int stays = sqmA >= sqmB ? a : b, moves = stays == a ? b : a;
            int home = pt.Household[stays], left = pt.Household[moves];
            if ((s.Households.Flags[left] & HouseholdFlags.Players) != 0 && s.Households.Size[left] > 1)
            {
                return;   // nobody is taken out of the player's household by the population
            }
            pt.Partner[a] = b;
            pt.Partner[b] = a;
            GenerateRules.Leave(s, moves);
            GenerateRules.Join(s, home, moves);
            pt.Role[moves] = (byte)Role.Partner;
            pt.Role[stays] = (byte)Role.Head;
            s.Households.Savings[home] += s.Households.Size[left] == 0 ? s.Households.Savings[left] : s.Households.Savings[left] / 2;
            if (s.Households.Size[left] == 0)
            {
                Dissolve(s, left);
            }
            else
            {
                MakeHead(s, left);
            }
        }

        // A person forms a household of their own in a free flat they can pay for; false with none found.
        private static bool Form(PopulationState s, LifeDay ld, int p, int day, bool withChildren)
        {
            PersonTable pt = s.People;
            int old = pt.Household[p];
            if (old < 0 || (s.Households.Flags[old] & HouseholdFlags.Players) != 0)
            {
                return true;   // nothing to do (or the player's household: its people stay)
            }
            int cls = s.Households.Class[old];
            long pay = pt.Status[p] == (byte)Status.Worker ? LabourRules.DailyPay(s, p) : 0;
            long disposable = pay - SpendRules.Tax((int)pay);
            int work = pt.Job[p] >= 0 && s.Employers.Building[pt.Job[p]] >= 0 ? s.Buildings[s.Employers.Building[pt.Job[p]]].District : -1;
            int flat = HousingRules.FindFlat(s, ld.Free, s.Seed, pt.Id[p], day, 1, Math.Max(1, disposable), cls, s.Households.District[old], work, out int building);
            if (flat < 0)
            {
                return false;
            }
            int h = s.Households.Add(s.NextHousehold++);
            HouseholdTable hh = s.Households;
            hh.Class[h] = (byte)cls;
            hh.Tastes[h] = hh.Tastes[old];
            GenerateRules.Leave(s, p);
            GenerateRules.Join(s, h, p);
            pt.Role[p] = (byte)Role.Head;
            int share = hh.Savings[old] / Math.Max(2, hh.Size[old] + 1);
            hh.Savings[old] -= share;
            hh.Savings[h] = share;
            HousingRules.MoveInto(s, h, flat, building, day);
            ld.Formed++;
            if (hh.Size[old] == 0)
            {
                Dissolve(s, old);
            }
            else
            {
                MakeHead(s, old);
            }
            return true;
        }

        /// <summary>A person leaves the city for the region (no flat to be had): out of their job and household, and of the population.</summary>
        public static void LeaveCity(PopulationState s, int p, int day)
        {
            PersonTable pt = s.People;
            int h = pt.Household[p];
            if (pt.Job[p] >= 0)
            {
                LabourRules.Lose(s, p, day, false);
            }
            GenerateRules.Leave(s, p);
            pt.Remove(p);
            if (h >= 0 && s.Households.Alive(h))
            {
                if (s.Households.Size[h] == 0)
                {
                    Dissolve(s, h);
                }
                else
                {
                    AfterLoss(s, h, day);
                }
            }
        }
    }
}
