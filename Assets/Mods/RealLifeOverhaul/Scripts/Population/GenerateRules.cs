using System;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The city's people generated from its buildings with the city's seed (POPULATION.md sections 2 and 3; the brief's third step): each
    /// free flat of each building with homes, in the order of the addresses, drawn from a hash of the seed, the building and the flat, so the
    /// same city gives the same people on every machine and in any order of the work. A flat is left free at the district's target share; its
    /// household's class comes from the district's shares (the game's NeighborhoodData; the game's own unit leans them by its size), its size from
    /// its class (the owner's city of about 2.8 a household, no more people than the flat holds) and its kind from the city's (couples with children,
    /// three generations, shared flats), its people's ages, schooling, work and tastes from the tables of the catalogue. Jobs, pay, savings and
    /// rents follow once the employers are known (<see cref="LabourRules"/>, <see cref="Finish"/>).
    /// Game-free. COST: nothing per frame; some ten microseconds a building, sliced by its caller.
    /// </summary>
    public static class GenerateRules
    {
        public const int TasteBits = 17;   // the interests a household may have (TasteKinds names the kind each tilts)

        /// <summary>The kind of spending each interest tilts, by its bit.</summary>
        public static readonly Spend[] TasteKinds =
        {
            Spend.Clothing, Spend.Electronics, Spend.BooksGifts, Spend.NightLife, Spend.Gym, Spend.Theatre, Spend.Cinema, Spend.Digital, Spend.EatingOut,
            Spend.Furniture, Spend.Flowers, Spend.Jewellery, Spend.Professional, Spend.Transport, Spend.Casino, Spend.Coffee, Spend.PersonalCare
        };

        /// <summary>A new population: the seed, the day and the year, and the city's table laid out with every flat free.</summary>
        public static void Begin(PopulationState s, int seed, int day, int yearDays)
        {
            s.Seed = seed;
            s.Day = day;
            s.Generated = day;
            s.Measure = PopulationCatalogue.MeasureVersion;
            s.YearDays = Math.Max(8, yearDays);
            s.MonthNumber = MonthOf(day, s.YearDays);
            s.NextHousehold = 1;
            s.NextPerson = 1;
            CityTable.Lay(s);
        }

        /// <summary>
        /// True for a saved city an older measure made (beta.2's line, homes above shops too, 47% over the owner's size; beta.3's, a fifth of its single
        /// adults at home on nothing and savings by class alone, so households started broke): it is generated again from the buildings with its own
        /// seed, the same city on every machine, rather than mended, which would orphan the jobs, the player's staff and the follows tied to its households.
        /// </summary>
        public static bool Outdated(PopulationState read)
        {
            return read != null && read.Measure != PopulationCatalogue.MeasureVersion;
        }

        /// <summary>The month's number of a day, from 0, by the save's year (a month is a twelfth of it, three days at least).</summary>
        public static int MonthOf(int day, int yearDays)
        {
            return Math.Max(0, day - 1) / MonthDays(yearDays);
        }

        public static int MonthDays(int yearDays)
        {
            return Math.Max(3, yearDays / 12);
        }

        /// <summary>A building's free flats peopled; how many households came of it.</summary>
        public static int Building(PopulationState s, int b)
        {
            if (b < 0 || b >= s.Buildings.Count || s.FlatCount[b] <= 0)
            {
                return 0;
            }
            int made = 0;
            int first = s.FlatFirst[b];
            for (int unit = 0; unit < s.FlatCount[b]; unit++)
            {
                if (s.Occupant[first + unit] == -1 && Flat(s, b, unit))
                {
                    made++;
                }
            }
            return made;
        }

        /// <summary>A flat's number for its draws: its building and place, the same on every machine.</summary>
        public static int Who(PopulationState s, int b, int unit)
        {
            unchecked
            {
                CityBuilding cb = s.Buildings[b];
                int street = 0;
                string name = cb.Street ?? string.Empty;
                for (int i = 0; i < name.Length; i++)
                {
                    street = street * 31 + name[i];   // a hash of the street's id, the same in every runtime (string.GetHashCode is not)
                }
                return street * 7919 + cb.Number * 104729 + unit * 1299709;
            }
        }

        private static bool Flat(PopulationState s, int b, int unit)
        {
            int who = Who(s, b, unit);
            if (!MeasureRules.House(s.Buildings[b], s.Districts[s.Buildings[b].District]) && PopDraw.Mille(s.Seed, PopDraw.Vacant, who, 0, PopulationCatalogue.GenerateVacancy))
            {
                return false;
            }
            Make(s, b, unit, who, false);
            return true;
        }

        /// <summary>
        /// A household from the region into a free flat, drawn as the generation's own but from the day as well, so no two that come are alike, and
        /// with a mover's age (<see cref="PopulationCatalogue.MoverAges"/>: those who move to a city are young, so the city does not age in place);
        /// its slot, or -1 when the flat is taken. Its workers look for jobs and its money is set by the caller (LabourRules, <see cref="Finish"/>).
        /// </summary>
        public static int Arrive(PopulationState s, int b, int unit, int day)
        {
            if (b < 0 || b >= s.Buildings.Count || unit < 0 || unit >= s.FlatCount[b] || s.Occupant[s.FlatFirst[b] + unit] != -1)
            {
                return -1;
            }
            int who = unchecked(Who(s, b, unit) * 31 + day * 7919 + 17);
            return Make(s, b, unit, who, true);
        }

        private static int Make(PopulationState s, int b, int unit, int who, bool mover)
        {
            CityBuilding cb = s.Buildings[b];
            PopDistrict d = s.Districts[cb.District];
            int seed = s.Seed;
            int sqm = CityTable.FlatSqmOf(s, b, unit);
            bool house = MeasureRules.House(cb, d);
            bool gameUnit = unit == 0 && cb.Use == Use.Homes;
            var cls = (PopClass)DrawClass(seed, who, d, gameUnit ? sqm : 0, house);
            int size = DrawSize(seed, who, (int)cls, sqm, house);
            int h = s.Households.Add(s.NextHousehold++);
            HouseholdTable hh = s.Households;
            hh.Building[h] = b;
            hh.Unit[h] = unit;
            hh.District[h] = (byte)cb.District;
            hh.Class[h] = (byte)cls;
            hh.Since[h] = mover ? s.Day : s.Day - PopDraw.Between(seed, PopDraw.Move, who, 0, 0, s.YearDays * 8);
            s.Occupant[s.FlatFirst[b] + unit] = h;
            People(s, h, who, cls, size, mover);
            Provide(s, h, s.Day);
            hh.Tastes[h] = Tastes(seed, who, Age(s, hh.First[h]), (int)cls);
            return h;
        }

        private static int Age(PopulationState s, int person)
        {
            return person >= 0 ? s.Age(person, s.Day) : 30;
        }

        /// <summary>A household's class: the district's shares, leaned by the size of the game's own unit (<paramref name="sqm"/>; 0 for any other flat).</summary>
        public static int DrawClass(int seed, int who, PopDistrict d, int sqm, bool villa)
        {
            int[] lean = villa ? PopulationCatalogue.VillaLean : sqm > 0 ? LeanOf(sqm) : Even;
            var w = new[] { d.Working * lean[0], d.Middle * lean[1], d.Upper * lean[2] };
            int c = PopDraw.Pick(seed, PopDraw.Class, who, 0, w);
            return c < 0 ? (int)PopClass.Middle : c;
        }

        private static readonly int[] Even = { 1000, 1000, 1000 };

        private static int[] LeanOf(int sqm)
        {
            for (int i = 0; i < PopulationCatalogue.ClassLean.Length; i++)
            {
                var row = PopulationCatalogue.ClassLean[i];
                if (sqm <= row.Sqm)
                {
                    return new[] { row.Working, row.Middle, row.Upper };
                }
            }
            return new[] { 1000, 1000, 1000 };
        }

        /// <summary>A household's people, one to six, by its class, and no more than the flat holds at <see cref="PopulationCatalogue.CrowdSqm"/> a person.</summary>
        public static int DrawSize(int seed, int who, int cls, int sqm, bool villa)
        {
            int[] sizes = villa ? PopulationCatalogue.VillaSizes : PopulationCatalogue.ClassSizes[PopulationCatalogue.Clamp(cls, 0, PopulationCatalogue.Classes - 1)];
            int i = PopDraw.Pick(seed, PopDraw.Size, who, 0, sizes);
            int size = i < 0 ? 1 : i + 1;
            return villa ? size : Math.Min(size, Math.Max(1, sqm / PopulationCatalogue.CrowdSqm));
        }

        // The household's members: its kind (alone, a couple, a parent with children, people sharing, three generations), their ages and sexes, then each one's schooling and work.
        private static void People(PopulationState s, int h, int who, PopClass cls, int size, bool mover)
        {
            int seed = s.Seed;
            if (size <= 1)
            {
                int age = BandAge(seed, who, 0, mover ? PopulationCatalogue.MoverAges : PopulationCatalogue.SingleAges);
                Member(s, h, who, 0, age, PopDraw.Mille(seed, PopDraw.Sex, who, 0, 500), Role.Head, cls);
                return;
            }
            int kind = size == 2 ? PopDraw.Pick(seed, PopDraw.Kind, who, 0, TwoKinds) : PopDraw.Pick(seed, PopDraw.Kind, who, 0, MoreKinds);
            if (kind == 2)
            {
                for (int m = 0; m < size; m++)
                {
                    int age = PopDraw.Between(seed, PopDraw.Age, who, m, 20, 40);
                    Member(s, h, who, m, age, PopDraw.Mille(seed, PopDraw.Sex, who, m, 500), m == 0 ? Role.Head : Role.Other, cls);
                }
                return;
            }
            bool three = kind == 3 && size >= 3;   // three generations: the eldest, the head (with a partner when there is room) and children
            bool couple = kind == 0 || (three && size >= 4 && PopDraw.Mille(seed, PopDraw.Kind, who, 1, 700));
            bool elders = three && size >= 5 && PopDraw.Mille(seed, PopDraw.Kind, who, 2, PopulationCatalogue.BothGrandparents);
            int children = size - (couple ? 2 : 1) - (three ? (elders ? 2 : 1) : 0);
            int headAge = couple && children == 0 ? BandAge(seed, who, 0, mover ? PopulationCatalogue.MoverAges : PopulationCatalogue.CoupleAges)
                : PopDraw.Between(seed, PopDraw.Age, who, 0, 24, mover ? MoverParentMost : 54);
            bool headFemale = couple ? PopDraw.Mille(seed, PopDraw.Sex, who, 0, 500) : PopDraw.Mille(seed, PopDraw.Sex, who, 0, 820);   // most lone parents are mothers
            int head = Member(s, h, who, 0, headAge, headFemale, Role.Head, cls);
            if (couple)
            {
                int partnerAge = Math.Max(PopulationCatalogue.AdultLeast, headAge + PopDraw.Between(seed, PopDraw.Partner, who, 0, -6, 4));
                bool same = PopDraw.Mille(seed, PopDraw.Partner, who, 1, 50);
                int partner = Member(s, h, who, 1, partnerAge, same ? headFemale : !headFemale, Role.Partner, cls);
                s.People.Partner[head] = partner;
                s.People.Partner[partner] = head;
            }
            if (three)
            {
                // The head's parent: at least a generation older, a pensioner or near it.
                int eldest = PopulationCatalogue.Clamp(headAge + PopDraw.Between(seed, PopDraw.Age, who, 400, 22, 34), PopulationCatalogue.GrandparentLeast, PopulationCatalogue.GrandparentMost);
                bool grandmother = PopDraw.Mille(seed, PopDraw.Sex, who, 8, 620);   // more grandmothers: women live longer
                int elder = Member(s, h, who, 8, eldest, grandmother, Role.Other, cls);
                if (elders)
                {
                    int other = Member(s, h, who, 9, Math.Min(PopulationCatalogue.OldestAge - 1, eldest + PopDraw.Between(seed, PopDraw.Partner, who, 2, -4, 4)), !grandmother, Role.Other, cls);
                    s.People.Partner[elder] = other;
                    s.People.Partner[other] = elder;
                }
            }
            int mother = Math.Max(headAge, couple ? s.Age(s.People.Partner[head], s.Day) : headAge);
            for (int c = 0; c < children; c++)
            {
                int oldest = Math.Min(24, mother - PopulationCatalogue.ParentAgeLeast);
                int youngest = Math.Max(0, Math.Min(oldest, mother - PopulationCatalogue.ParentAgeMost));
                int age = oldest < 0 ? 0 : PopDraw.Between(seed, PopDraw.Child, who, c, youngest, oldest);
                Member(s, h, who, 2 + c, age, PopDraw.Mille(seed, PopDraw.Sex, who, 2 + c, 488), Role.Child, cls);
            }
            if (couple && children > 0 && YoungestChild(s, h) < PopulationCatalogue.SchoolFrom)
            {
                // A couple with a child under school age: one of them often stays home with it.
                int carer = s.People.Partner[head];
                if (carer >= 0 && s.People.Status[carer] != (byte)Status.Retired && PopDraw.Mille(seed, PopDraw.Status, who, 9, 300))
                {
                    s.People.Status[carer] = (byte)Status.Home;
                    s.People.Field[carer] = (byte)Field.None;
                }
            }
        }

        // The kinds of a household of two (a couple, a parent and child, two sharing) and of more (a couple with children, a lone parent, sharing, three generations).
        private static readonly int[] TwoKinds = { PopulationCatalogue.TwoCouple, PopulationCatalogue.TwoParent, 1000 - PopulationCatalogue.TwoCouple - PopulationCatalogue.TwoParent };

        private static readonly int[] MoreKinds =
        {
            PopulationCatalogue.MoreCouple, PopulationCatalogue.MoreParent, 1000 - PopulationCatalogue.MoreCouple - PopulationCatalogue.MoreParent - PopulationCatalogue.MoreThree,
            PopulationCatalogue.MoreThree
        };

        private static int YoungestChild(PopulationState s, int h)
        {
            int youngest = int.MaxValue;
            for (int p = s.Households.First[h]; p >= 0; p = s.People.Next[p])
            {
                if (s.People.Role[p] == (byte)Role.Child)
                {
                    youngest = Math.Min(youngest, s.Age(p, s.Day));
                }
            }
            return youngest;
        }

        private static int BandAge(int seed, int who, int n, int[] bands)
        {
            int band = PopDraw.Pick(seed, PopDraw.Age, who, 100 + n, bands);
            band = band < 0 ? 1 : band;
            return PopDraw.Between(seed, PopDraw.Age, who, 200 + n, PopulationCatalogue.AgeBands[band], PopulationCatalogue.AgeBands[band + 1] - 1);
        }

        /// <summary>A person added to a household at an age: their birth day, schooling and what they do, drawn as the generation's.</summary>
        public static int Member(PopulationState s, int h, int who, int n, int age, bool female, Role role, PopClass cls)
        {
            int seed = s.Seed;
            int p = s.People.Add(s.NextPerson++);
            PersonTable pt = s.People;
            int dayInYear = PopDraw.Range(seed, PopDraw.Age, who, 300 + n, s.YearDays);
            pt.Birth[p] = s.Day - age * s.YearDays - dayInYear;
            pt.Female[p] = (byte)(female ? 1 : 0);
            pt.Role[p] = (byte)role;
            pt.Since[p] = s.Day;
            Join(s, h, p);
            Schooling(s, p, who * 31 + n, age, cls);
            int head = s.Households.First[h];
            ushort parent = role == Role.Child && head >= 0 && head != p ? pt.Games[head] : (ushort)0;
            pt.Games[p] = GameRules.Draw(seed, pt.Id[p], s.Day, age, (int)cls, female, parent);   // the games they play, a child's leaning on the head's
            return p;
        }

        /// <summary>A person put into a household (at the end of its members).</summary>
        public static void Join(PopulationState s, int h, int p)
        {
            HouseholdTable hh = s.Households;
            PersonTable pt = s.People;
            pt.Household[p] = h;
            pt.Next[p] = -1;
            if (hh.First[h] < 0)
            {
                hh.First[h] = p;
            }
            else
            {
                int last = hh.First[h];
                while (pt.Next[last] >= 0)
                {
                    last = pt.Next[last];
                }
                pt.Next[last] = p;
            }
            hh.Size[h] = (byte)Math.Min(255, hh.Size[h] + 1);
        }

        /// <summary>A person taken out of their household (they stay a person: they move, or die after).</summary>
        public static void Leave(PopulationState s, int p)
        {
            HouseholdTable hh = s.Households;
            PersonTable pt = s.People;
            int h = pt.Household[p];
            if (h < 0)
            {
                return;
            }
            if (hh.First[h] == p)
            {
                hh.First[h] = pt.Next[p];
            }
            else
            {
                for (int q = hh.First[h]; q >= 0; q = pt.Next[q])
                {
                    if (pt.Next[q] == p)
                    {
                        pt.Next[q] = pt.Next[p];
                        break;
                    }
                }
            }
            pt.Household[p] = -1;
            pt.Next[p] = -1;
            hh.Size[h] = (byte)Math.Max(0, hh.Size[h] - 1);
        }

        // A person's schooling and status by their age and class, as a generated city has them: at school, at university, in the labour force,
        // at home or retired; their field and skill for work. Whether in a job or not the labour market decides.
        private static void Schooling(PopulationState s, int p, int who, int age, PopClass cls)
        {
            int seed = s.Seed, c = (int)cls;
            PersonTable pt = s.People;
            if (age < PopulationCatalogue.SchoolFrom)
            {
                pt.Status[p] = (byte)Status.Child;
                return;
            }
            if (age < PopulationCatalogue.SchoolTo)
            {
                pt.Status[p] = (byte)Status.Pupil;
                return;
            }
            Education goal = FinalEducation(seed, who, c);
            int studyEnd = PopulationCatalogue.SchoolTo + (goal >= Education.Degree ? PopulationCatalogue.UniversityYears : 0)
                           + (goal == Education.Postgraduate ? PopulationCatalogue.PostgraduateYears : 0);
            if (age < studyEnd)
            {
                pt.Status[p] = (byte)Status.Student;
                pt.Education[p] = (byte)(age >= PopulationCatalogue.SchoolTo + PopulationCatalogue.UniversityYears ? Education.Degree : Education.School);
                pt.Field[p] = (byte)DrawField(seed, who, goal);
                return;
            }
            pt.Education[p] = (byte)goal;
            pt.Field[p] = (byte)DrawField(seed, who, goal);
            pt.Skill[p] = (byte)SkillAt(seed, who, age - studyEnd, c);
            if (age >= PopulationCatalogue.RetireAge)
            {
                pt.Status[p] = PopDraw.Mille(seed, PopDraw.Retire, who, 0, PopulationCatalogue.WorkingOld) ? (byte)Status.Unemployed : (byte)Status.Retired;
                return;
            }
            pt.Status[p] = PopDraw.Mille(seed, PopDraw.Status, who, 0, PopulationCatalogue.Participation[c]) ? (byte)Status.Unemployed : (byte)Status.Home;
            if (pt.Status[p] == (byte)Status.Home)
            {
                pt.Field[p] = (byte)Field.None;
            }
        }

        /// <summary>The schooling a person finishes with, by class: no diploma, school, college, a degree or a postgraduate degree.</summary>
        public static Education FinalEducation(int seed, int who, int cls)
        {
            int drop = PopulationCatalogue.DropOutShare[cls], uni = PopulationCatalogue.UniversityShare[cls];
            int post = PopulationCatalogue.PostgraduateShare[cls], college = PopulationCatalogue.CollegeShare[cls];
            long rest = 1000 - drop;
            int degree = (int)(rest * uni / 1000 * (1000 - post) / 1000);
            int postgrad = (int)(rest * uni / 1000 * post / 1000);
            int coll = (int)(rest * (1000 - uni) / 1000 * college / 1000);
            int school = (int)Math.Max(0, 1000 - drop - degree - postgrad - coll);
            int e = PopDraw.Pick(seed, PopDraw.Education, who, 0, new[] { drop, school, coll, degree, postgrad });
            return e < 0 ? Education.School : (Education)e;
        }

        /// <summary>A person's field of work by their schooling.</summary>
        public static Field DrawField(int seed, int who, Education education)
        {
            int f = PopDraw.Pick(seed, PopDraw.Field, who, 0, PopulationCatalogue.FieldByEducation[(int)education]);
            return f <= 0 ? Field.Service : (Field)f;
        }

        /// <summary>Skill after so many years of work: three points a year to seventy, and a draw of thirty for ability; the upper class a little more.</summary>
        public static int SkillAt(int seed, int who, int years, int cls)
        {
            int skill = Math.Min(70, Math.Max(0, years) * 3) + PopDraw.Between(seed, PopDraw.Skill, who, 0, 0, 30) + (cls == (int)PopClass.Upper ? 5 : 0);
            return PopulationCatalogue.Clamp(skill, 0, 100);
        }

        /// <summary>A household's interests: each a quarter of households, more or fewer by the head's age and class.</summary>
        public static uint Tastes(int seed, int who, int age, int cls)
        {
            uint mask = 0;
            for (int bit = 0; bit < TasteBits; bit++)
            {
                int chance = 250 + TasteLean(bit, age, cls);
                if (PopDraw.Mille(seed, PopDraw.Taste, who, bit, chance))
                {
                    mask |= 1u << bit;
                }
            }
            return mask;
        }

        // How the head's age and class lean an interest, thousandths: the young go out and play, their elders go to the theatre and keep flowers.
        private static int TasteLean(int bit, int age, int cls)
        {
            bool young = age < 35, old = age >= 60;
            switch (TasteKinds[bit])
            {
                case Spend.NightLife: return young ? 200 : old ? -180 : 0;
                case Spend.Digital: return young ? 220 : old ? -170 : 0;
                case Spend.Gym: return young ? 150 : old ? -120 : 0;
                case Spend.Theatre: return (old ? 120 : 0) + (cls == 2 ? 150 : cls == 0 ? -100 : 0);
                case Spend.Flowers: return old ? 150 : young ? -80 : 0;
                case Spend.BooksGifts: return old ? 80 : 0;
                case Spend.Jewellery: return cls == 2 ? 200 : cls == 0 ? -150 : 0;
                case Spend.Professional: return cls == 2 ? 180 : cls == 0 ? -120 : 0;
                case Spend.Casino: return cls == 2 ? 50 : -100;
                case Spend.Cinema: return cls == 0 ? 80 : 0;
                default: return 0;
            }
        }

        /// <summary>
        /// The household's money once its people's jobs are known: savings and card debt by its usual income and the head's age (the Survey of Consumer
        /// Finances), whether it owns its flat (with a mortgage or not), its cars, its rent, and its first month's rent and deposit in savings at least.
        /// </summary>
        public static void Finish(PopulationState s, int h)
        {
            HouseholdTable hh = s.Households;
            int seed = s.Seed, who = hh.Id[h], cls = hh.Class[h];
            for (int p = hh.First[h]; p >= 0; p = s.People.Next[p])
            {
                var status = (Status)s.People.Status[p];
                if ((status == Status.Retired || status == Status.Unemployed) && s.People.Pay[p] <= 0)
                {
                    // the pay they had (a pension and a benefit are measured by it)
                    s.People.Pay[p] = LabourRules.PayFor(s, Math.Max(1, (int)s.People.Field[p]), s.People.Skill[p], cls);
                    s.People.Hours[p] = PopulationCatalogue.FullTimeHours;
                }
            }
            long usual = SpendRules.UsualDaily(s, h);
            int age = Age(s, hh.First[h]);
            hh.Savings[h] = (int)Math.Min(int.MaxValue / 4, StartSavings(seed, who, usual, age));
            hh.Debt[h] = (int)Math.Min(int.MaxValue / 4, StartCard(seed, who, usual, age));
            int b = hh.Building[h];
            bool villas = b >= 0 && s.Districts[s.Buildings[b].District].Villas;
            hh.Cars[h] = (byte)(PopDraw.Mille(seed, PopDraw.Car, who, 0, villas ? PopulationCatalogue.CarShareHamptons : PopulationCatalogue.CarShare[cls]) ? 1 : 0);
            if (hh.Cars[h] > 0 && cls == (int)PopClass.Upper && PopDraw.Mille(seed, PopDraw.Car, who, 1, 300))
            {
                hh.Cars[h] = 2;
            }
            if (PopDraw.Mille(seed, PopDraw.Owner, who, 0, villas ? 900 : PopulationCatalogue.OwnerShare[cls]))
            {
                hh.Landlord[h] = Landlords.Own;
                if (PopDraw.Mille(seed, PopDraw.Mortgage, who, 0, 550))
                {
                    hh.Flags[h] |= HouseholdFlags.Mortgage;
                }
            }
            HousingRules.SetRent(s, h);
            if (hh.Landlord[h] == Landlords.City)
            {
                // A tenant the market's rent would drive out holds a regulated tenancy, as about half of New York's renters do: a rent it bears.
                SpendRules.Income(s, h, s.Day, 0, out DayIncome inc);
                long bearable = Math.Max(0, inc.Disposable) * RegulatedShare / 1000;
                if (inc.Disposable > 0 && hh.Rent[h] > Math.Max(0, inc.Disposable) * PopulationCatalogue.RentBurdenMove / 1000)
                {
                    hh.Rent[h] = (int)Math.Max(hh.Rent[h] * RegulatedLeast / 1000, bearable);
                    hh.Flags[h] |= HouseholdFlags.Regulated;
                }
            }
            // A household that comes has its first month's rent and deposit at least, so a first rent puts nobody in debt (students living on a
            // loan or their family's money among them, whose usual income is none).
            hh.Savings[h] = (int)Math.Max(hh.Savings[h], Math.Min(int.MaxValue / 4, (long)hh.Rent[h] * PopulationCatalogue.SavingsRentDays / 100));
        }

        /// <summary>
        /// Savings a household comes with, whole money: days of its usual income (cents a day) on the Survey of Consumer Finances' ladder by that income,
        /// leaned by the head's age, times a draw of the spread (a quarter to four times, the ladder's the median).
        /// </summary>
        public static long StartSavings(int seed, int who, long usual, int age)
        {
            if (usual <= 0)
            {
                return 0;
            }
            long days1000 = (long)LadderDays(usual) * PopulationCatalogue.SavingsAgeLean[MoneyAge(age)];
            int[] spread = PopulationCatalogue.SavingsSpread;
            int factor = spread[PopDraw.Range(seed, PopDraw.Savings, who, 0, spread.Length)];
            return usual * days1000 / 1000 * factor / 1000 / 100;
        }

        /// <summary>
        /// Card debt a household comes with, whole money: by its usual income's group a share carries a balance (leaned by the head's age), of the group's
        /// median days of income times the spread's draw; none for the others.
        /// </summary>
        public static long StartCard(int seed, int who, long usual, int age)
        {
            if (usual <= 0)
            {
                return 0;
            }
            int group = IncomeGroup(usual);
            int share = PopulationCatalogue.CardShare[group] * PopulationCatalogue.CardAgeLean[MoneyAge(age)] / 1000;
            if (!PopDraw.Mille(seed, PopDraw.Debt, who, 0, share))
            {
                return 0;
            }
            int[] spread = PopulationCatalogue.SavingsSpread;
            int factor = spread[PopDraw.Range(seed, PopDraw.Debt, who, 1, spread.Length)];
            return usual * PopulationCatalogue.CardDays[group] * factor / 1000 / 100;
        }

        /// <summary>The savings ladder's days at a usual income (cents a day): in a straight line between its points, its ends beyond them.</summary>
        public static int LadderDays(long usual)
        {
            var ladder = PopulationCatalogue.SavingsLadder;
            long at = usual * 1000 / PopulationCatalogue.UsualMiddle;
            if (at <= ladder[0].Income)
            {
                return ladder[0].Days;
            }
            for (int i = 1; i < ladder.Length; i++)
            {
                if (at <= ladder[i].Income)
                {
                    var (x0, y0) = ladder[i - 1];
                    var (x1, y1) = ladder[i];
                    return (int)(y0 + (at - x0) * (y1 - y0) / (x1 - x0));
                }
            }
            return ladder[ladder.Length - 1].Days;
        }

        /// <summary>The survey's income group of a usual income (0 under the 20th percentile to 5 the top tenth).</summary>
        public static int IncomeGroup(long usual)
        {
            long at = usual * 1000 / PopulationCatalogue.UsualMiddle;
            int[] starts = PopulationCatalogue.IncomeGroups;
            int g = 0;
            while (g < starts.Length && at >= starts[g])
            {
                g++;
            }
            return g;
        }

        /// <summary>The survey's age group of a head's age (0 under 35 to 5 from 75).</summary>
        public static int MoneyAge(int age)
        {
            int[] starts = PopulationCatalogue.MoneyAges;
            int g = 0;
            while (g < starts.Length && age >= starts[g])
            {
                g++;
            }
            return g;
        }

        /// <summary>
        /// A household with nobody in work, looking for work or drawing a pension: its adults at home of working age look for work (their field by their
        /// schooling, the pay their skill would fetch). Staying home to care for a family, or living on savings, needs money coming in from somebody, so
        /// nobody lives alone at home on nothing, as the generation's participation draw alone let a fifth of single adults do. Every way a household
        /// is made or loses a member ends here. Students and children alone are left as they are.
        /// </summary>
        public static void Provide(PopulationState s, int h, int day)
        {
            if (h < 0 || !s.Households.Alive(h) || (s.Households.Flags[h] & HouseholdFlags.Players) != 0)
            {
                return;   // the player's household is the player's to run
            }
            PersonTable pt = s.People;
            for (int p = s.Households.First[h]; p >= 0; p = pt.Next[p])
            {
                var status = (Status)pt.Status[p];
                if (status == Status.Worker || status == Status.Unemployed || status == Status.Retired)
                {
                    return;
                }
            }
            int cls = s.Households.Class[h];
            for (int p = s.Households.First[h]; p >= 0; p = pt.Next[p])
            {
                int age = s.Age(p, day);
                if (pt.Status[p] != (byte)Status.Home || age < PopulationCatalogue.AdultLeast || age >= PopulationCatalogue.RetireAge
                    || (pt.Flags[p] & (PersonFlags.Linked | PersonFlags.Frozen)) != 0)
                {
                    continue;   // another system's person (a partner, a roommate) is that system's to move
                }
                pt.Status[p] = (byte)Status.Unemployed;
                pt.Since[p] = day;
                if (pt.Field[p] == (byte)Field.None)
                {
                    pt.Field[p] = (byte)DrawField(s.Seed, pt.Id[p], (Education)pt.Education[p]);
                }
                pt.Pay[p] = LabourRules.PayFor(s, pt.Field[p], pt.Skill[p], cls);
                pt.Hours[p] = PopulationCatalogue.FullTimeHours;
            }
        }

        /// <summary>
        /// The households' start, counted once a city's first day is lived: those with an adult at home of working age and nobody in work, looking for
        /// work or on a pension (<see cref="Provide"/> leaves none), and those the first day put deeper in debt (their first month's rent and deposit
        /// in savings leaves none). Game-free. COST: one pass over the households and their people.
        /// </summary>
        public static void StartCheck(PopulationState s, out int unprovided, out int deeper)
        {
            unprovided = deeper = 0;
            HouseholdTable hh = s.Households;
            PersonTable pt = s.People;
            for (int h = 0; h < hh.Count; h++)
            {
                if (!hh.Alive(h))
                {
                    continue;
                }
                bool earner = false, home = false;
                for (int p = hh.First[h]; p >= 0; p = pt.Next[p])
                {
                    var status = (Status)pt.Status[p];
                    earner |= status == Status.Worker || status == Status.Unemployed || status == Status.Retired;
                    int age = s.Age(p, s.Day);
                    home |= status == Status.Home && age >= PopulationCatalogue.AdultLeast && age < PopulationCatalogue.RetireAge;
                }
                unprovided += home && !earner ? 1 : 0;
                deeper += hh.Month[h * Ledger.Lines + Ledger.DebtChange] > 0 ? 1 : 0;
            }
        }

        public const int MoverParentMost = 44;   // years: the eldest a parent who comes from the region with children at home is drawn at (movers are young)
        public const int RegulatedShare = 350;   // thousandths of its income after tax a regulated tenancy's rent is set at when a city is generated
        public const int RegulatedLeast = 250;   // ... but never under this share of the market's
    }
}
