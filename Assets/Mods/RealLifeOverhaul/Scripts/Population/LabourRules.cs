using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Population
{
    /// <summary>One registration of the game's as the population reads it: a business that sells to people, employs them, or both.</summary>
    public struct BusinessSeed
    {
        public string Street;
        public int Number;
        public string Type;        // the game's business type
        public Owner Owner;
        public int Room;           // the customers its building's size has room for as that type (BuildingSizeData.customerCapacities)
        public int Hours;          // hours open a day, on average over the week
        public int Price;          // thousandths: its prices against the game's own
        public int Quality;        // its interior, 0 to 100
        public int Content;        // its customers' satisfaction, 0 to 100
        public int Staff;          // the player's: the game's staff there now; -1 for the city's and the rivals' (worked out from the floor)
        public int Promotion;      // thousandths: the game's promotion multiplier now (its base plus 0.75 times the promotion over 100)
        public string Name;        // the business's name as the game shows it; empty or null where it has none (no story is told of a business without one)
    }

    /// <summary>
    /// The labour market's base (the brief's fifth step): every job slot belongs to an employer (a business of the player's, the city's or a
    /// rival's; the other firms of an office building; the public sector; the game's special services; a residential building's staff), and
    /// people fill them by their field, their skill and how far they would go; commuters from the region take what residents do not. Out of
    /// that come unemployment, vacancies and pay, each field's pay following how hard its people are to find. The player's businesses employ the
    /// game's own staff (each one a linked resident), hired and let go by the player alone. Game-free. COST: nothing per frame; a person's day
    /// a few comparisons, a match a handful of looks.
    /// </summary>
    public static class LabourRules
    {
        public const int StartVacancy = 30;      // thousandths of each employer's slots left open when a city is generated (US JOLTS openings run 3 to 7%)
        public const int Looks = 6;              // employers of a field a jobseeker looks at in a day
        // The wage curve: a field's pay is pulled towards a level set by how many of its own people are out of work against the city's usual
        // share, about 7% lower where twice as many are looking and as much higher where none are (Blanchflower and Oswald's wage curve, an
        // elasticity near a tenth), within a band the region's pay holds it to: commuters come for the region's pay, so the city's cannot drift
        // far from it either way. Moving towards a level rather than by steps keeps the bounds a guard and never a resting place.
        public const int WageCurve = 70;         // thousandths off the start for each time the field's unemployment is past the city's usual (PopulationCatalogue.UnemploymentStart)
        public const int WageTargetLeast = 860, WageTargetMost = 1200;   // thousandths: the most the field's own market moves its pay either way
        public const int WageAdjust = 80;        // thousandths of the gap to that level a field's pay closes in a month: slow, most of the way within the save's year
        public const int WageLeast = 800, WageMost = 1600;   // thousandths of its start a field's pay keeps within: a guard past the curve's own band
        public const int WageFollow = 100;       // thousandths of the gap to the going rate a worker's pay closes in a month
        public const int SkillPerYear = 3;       // points of skill a year of work brings, to the most
        public const int FindRate = 3800;        // per mille a year: how fast a jobseeker finds work while there is plenty (about three months out of work on average)
        public const int CommuterFillLeast = 200;   // thousandths of the region's usual fill left while the city's own are out of work at twice the start rate

        /// <summary>
        /// The employers and businesses of the city: one for each registration with jobs or a kind of spending (its business), the other firms
        /// of each office building, the special services and the public sector by their floor, a residential building's staff. A key already
        /// known keeps its slot (a load's or a day's look again): only its figures are brought up to date, and its jobs follow its floor.
        /// </summary>
        public static void Setup(PopulationState s, IList<BusinessSeed> seeds, int day = 0)
        {
            var byBuilding = new Dictionary<int, int>(seeds != null ? seeds.Count : 0);
            for (int i = 0; seeds != null && i < seeds.Count; i++)
            {
                int b = s.BuildingOfKey(PopulationState.KeyOf(seeds[i].Street, seeds[i].Number));
                if (b >= 0 && !byBuilding.ContainsKey(b))
                {
                    byBuilding.Add(b, i);
                }
            }
            for (int e = 0; e < s.Employers.Count; e++)
            {
                s.Employers.Slots[e] = 0;   // set again below for every one still there; a gone one keeps none
            }
            for (int bz = 0; bz < s.Businesses.Count; bz++)
            {
                s.Businesses.Seen[bz] = false;
            }
            for (int b = 0; b < s.Buildings.Count; b++)
            {
                CityBuilding cb = s.Buildings[b];
                int seed = byBuilding.TryGetValue(b, out int at) ? at : -1;
                BusinessSeed bs = seed >= 0 ? seeds[seed] : default;
                cb.Business = seed >= 0 ? bs.Type : null;
                s.Buildings[b] = cb;
                int jobs = MeasureRules.Jobs(cb);
                string key = PopulationState.KeyOf(cb.Street, cb.Number);
                int business = -1;
                if (seed >= 0 && PopulationCatalogue.CategoryOfType(bs.Type) >= 0)
                {
                    business = Business(s, key, b, bs, day);
                }
                switch (cb.Use)
                {
                    case Use.Homes:
                        Employer(s, key, b, EmployerKind.Building, Field.Service, jobs, -1, false);
                        break;
                    case Use.Office:
                        int own = seed < 0 ? 0 : bs.Staff >= 0 ? bs.Staff : Math.Min(jobs, OneUnit(cb));
                        if (seed >= 0)
                        {
                            Employer(s, key, b, EmployerKind.Business, MeasureRules.OfficeField(bs.Type), own, business, bs.Owner == Owner.Player);
                        }
                        Employer(s, key + "|t", b, EmployerKind.Tenants, Field.Office, Math.Max(0, jobs - own), -1, false);
                        break;
                    case Use.Shop:
                    case Use.Cinema:
                    case Use.Theatre:
                    case Use.Warehouse:
                        if (seed >= 0)
                        {
                            int slots = bs.Staff >= 0 ? bs.Staff : jobs;
                            Employer(s, key, b, EmployerKind.Business, cb.Use == Use.Warehouse ? Field.Trades : Field.Service, slots, business, bs.Owner == Owner.Player);
                        }
                        else if (cb.Use == Use.Warehouse)
                        {
                            Employer(s, key + "|t", b, EmployerKind.Tenants, Field.Trades, jobs, -1, false);   // the city's own carriers use an unlet warehouse
                        }
                        break;
                    case Use.Special:
                        Employer(s, key, b, MeasureRules.SpecialKind(cb.Special), MeasureRules.JobField(cb), jobs, business, false);
                        break;
                }
            }
            for (int e = 0; e < s.Employers.Count; e++)
            {
                Shed(s, e);
            }
        }

        // One unit's desks of an office building: what the game's own business in it holds of its floors.
        private static int OneUnit(in CityBuilding b)
        {
            return Math.Max(PopulationCatalogue.ShopJobsLeast, (int)((long)Math.Max(b.UnitSqm, 1) * PopulationCatalogue.OfficeNetShare / 1000 / PopulationCatalogue.DeskSqm));
        }

        // A business found by its key or added, its figures brought up to date. On a game day (day above 0) what a city or rival firm did is noted:
        // a business new to the table opened, and a city business whose prices moved (the same kind of business under the same owner) moved them.
        private static int Business(PopulationState s, string key, int building, in BusinessSeed bs, int day)
        {
            BusinessTable bt = s.Businesses;
            int b = s.BusinessOfKey(key);
            bool opened = false;
            if (b < 0)
            {
                b = bt.Add(key);
                s.KnowBusiness(key, b);
                opened = true;
            }
            int was = !opened && string.Equals(bt.Type[b], bs.Type, StringComparison.Ordinal) && bt.Owner[b] == (byte)bs.Owner ? bt.Price[b] : 0;
            if (day > 0)
            {
                CityFirmRules.Noted(s, bs, opened, was, bs.Price > 0 ? bs.Price : 1000, day);
            }
            if (!string.Equals(bt.Type[b], bs.Type, StringComparison.Ordinal))
            {
                bt.Type[b] = bs.Type;
                bt.AnchorDay[b] = -1;   // another kind of business in the same place starts from the game's own count again
                bt.Anchor[b] = 0;
                Array.Clear(bt.Habit, b * PopulationCatalogue.Classes, PopulationCatalogue.Classes);
            }
            bt.Building[b] = building;
            bt.District[b] = (byte)s.Buildings[building].District;
            bt.Kind[b] = (byte)PopulationCatalogue.CategoryOfType(bs.Type);
            bt.Room[b] = Math.Max(1, bs.Room);
            bt.Hours[b] = (byte)PopulationCatalogue.Clamp(bs.Hours, 0, 24);
            bt.Price[b] = bs.Price > 0 ? bs.Price : 1000;
            bt.Quality[b] = (byte)PopulationCatalogue.Clamp(bs.Quality, 0, 100);
            bt.Content[b] = (byte)PopulationCatalogue.Clamp(bs.Content, 0, 100);
            bt.Owner[b] = (byte)bs.Owner;
            bt.Seen[b] = true;
            if (bs.Promotion > 0 && bt.AnchorDay[b] < 0)
            {
                bt.AnchorPromotion[b] = bs.Promotion;
            }
            return b;
        }

        // An employer found by its key or added; its slots set.
        private static void Employer(PopulationState s, string key, int building, EmployerKind kind, Field field, int slots, int business, bool players)
        {
            EmployerTable et = s.Employers;
            int e = s.EmployerOfKey(key);
            if (e < 0)
            {
                e = et.Add(key, building, kind, field, slots, business);
                s.KnowEmployer(key, e);
            }
            et.Building[e] = building;
            et.Kind[e] = (byte)kind;
            et.Field[e] = (byte)field;
            et.Base[e] = Math.Max(0, slots);
            et.Slots[e] = kind == EmployerKind.Tenants ? CityFirmRules.SlotsNow(s, e) : et.Base[e];   // the firms' level of the city's growth, a new floor let over the months
            et.Business[e] = business;
            et.Players[e] = players;
            if (business >= 0)
            {
                s.Businesses.Employer[business] = e;
            }
        }

        /// <summary>An employer with more people than slots lets the commuters go first, then the last residents taken on.</summary>
        public static void Shed(PopulationState s, int e)
        {
            EmployerTable et = s.Employers;
            int over = et.Filled[e] + et.Commuters[e] - et.Slots[e];
            if (over <= 0)
            {
                return;
            }
            int commuters = Math.Min(over, et.Commuters[e]);
            et.Commuters[e] -= commuters;
            s.Region.Commuting = Math.Max(0, s.Region.Commuting - commuters);
            over -= commuters;
            for (int p = s.People.Count - 1; p >= 0 && over > 0; p--)
            {
                if (s.People.Id[p] != 0 && s.People.Job[p] == e && (s.People.Flags[p] & PersonFlags.Linked) == 0)
                {
                    Lose(s, p, s.Day, false);
                    over--;
                }
            }
        }

        /// <summary>A field's going rate an hour for a skill and class, cents: the game's scale, the class's firms, and how hard its people are to find.</summary>
        public static int PayFor(PopulationState s, int field, int skill, int cls)
        {
            field = PopulationCatalogue.Clamp(field, 1, PopulationCatalogue.Fields - 1);
            long pay = PopulationCatalogue.FieldPay[field];
            pay = pay * (1000 + PopulationCatalogue.SkillBoostMost * PopulationCatalogue.Clamp(skill, 0, 100) / 100) / 1000;
            pay = pay * PopulationCatalogue.ClassPay[PopulationCatalogue.Clamp(cls, 0, 2)] / 1000;
            int market = field < s.FieldWage.Length && s.FieldWage[field] > 0 ? s.FieldWage[field] : 1000;
            return (int)Math.Min(int.MaxValue / 4, pay * market / 1000);
        }

        /// <summary>A person takes a job: their pay the going rate, their hours full or part time.</summary>
        public static void Hire(PopulationState s, int p, int e, int day)
        {
            PersonTable pt = s.People;
            if (pt.Job[p] >= 0)
            {
                s.Employers.Filled[pt.Job[p]] = Math.Max(0, s.Employers.Filled[pt.Job[p]] - 1);
            }
            pt.Job[p] = e;
            pt.Status[p] = (byte)Status.Worker;
            pt.Since[p] = day;
            s.Employers.Filled[e]++;
            int h = pt.Household[p];
            int cls = h >= 0 ? s.Households.Class[h] : 1;
            int field = pt.Field[p] > 0 ? pt.Field[p] : s.Employers.Field[e];
            pt.Field[p] = (byte)field;
            pt.Pay[p] = PayFor(s, field, pt.Skill[p], cls);
            pt.Hours[p] = (byte)(PopDraw.Mille(s.Seed, PopDraw.Part, pt.Id[p], day, PopulationCatalogue.PartTimeShare) ? PopulationCatalogue.PartTimeHours : PopulationCatalogue.FullTimeHours);
        }

        /// <summary>A person's job ends: out of work (their last pay kept for their benefit), or retired.</summary>
        public static void Lose(PopulationState s, int p, int day, bool retire)
        {
            PersonTable pt = s.People;
            int e = pt.Job[p];
            if (e >= 0)
            {
                s.Employers.Filled[e] = Math.Max(0, s.Employers.Filled[e] - 1);
            }
            pt.Job[p] = -1;
            pt.Status[p] = (byte)(retire ? Status.Retired : Status.Unemployed);
            pt.Since[p] = day;
        }

        /// <summary>The labour force put to work when a city is generated, all at once (the steps of <see cref="FillPerson"/> and <see cref="FillCommuters"/>).</summary>
        public static void FillAtStart(PopulationState s)
        {
            var open = new List<int>[PopulationCatalogue.Fields];
            Openings(s, open);
            for (int p = 0; p < s.People.Count; p++)
            {
                FillPerson(s, p, open);
            }
            FillCommuters(s);
        }

        /// <summary>
        /// A person of a generated city's labour force, in the order of their ids: out of work at the city's start rate, or in a job of their field
        /// near home.
        /// </summary>
        public static void FillPerson(PopulationState s, int p, List<int>[] open)
        {
            PersonTable pt = s.People;
            if (pt.Id[p] == 0 || pt.Status[p] != (byte)Status.Unemployed || (pt.Flags[p] & PersonFlags.Linked) != 0)
            {
                return;
            }
            int h = pt.Household[p];
            int cls = h >= 0 ? s.Households.Class[h] : 1;
            if (PopDraw.Mille(s.Seed, PopDraw.Unemployed, pt.Id[p], 0, PopulationCatalogue.UnemploymentStart))
            {
                pt.Pay[p] = PayFor(s, pt.Field[p], pt.Skill[p], cls);
                pt.Hours[p] = PopulationCatalogue.FullTimeHours;
                pt.Since[p] = s.Day - PopDraw.Between(s.Seed, PopDraw.Unemployed, pt.Id[p], 1, 0, s.YearDays / 2);
                return;
            }
            int e = Match(s, p, open, 0);
            if (e >= 0)
            {
                Hire(s, p, e, s.Day);
                pt.Since[p] = s.Day - PopDraw.Between(s.Seed, PopDraw.Work, pt.Id[p], 0, 0, s.YearDays * 6);
            }
            else
            {
                pt.Pay[p] = PayFor(s, pt.Field[p], pt.Skill[p], cls);
                pt.Hours[p] = PopulationCatalogue.FullTimeHours;
            }
        }

        /// <summary>The region's commuters in a generated city's jobs: all that residents did not take but a start share of open vacancies.</summary>
        public static void FillCommuters(PopulationState s)
        {
            EmployerTable et = s.Employers;
            s.Region.Commuting = 0;
            for (int e = 0; e < et.Count; e++)
            {
                if (et.Players[e])
                {
                    continue;   // the player's staff are the game's own, every one a resident
                }
                int keep = (int)((long)et.Slots[e] * StartVacancy / 1000);
                et.Commuters[e] = Math.Max(0, et.Slots[e] - et.Filled[e] - keep);
                s.Region.Commuting += et.Commuters[e];
            }
        }

        /// <summary>The employers with vacancies, by field (the players' left out: the player hires).</summary>
        public static void Openings(PopulationState s, List<int>[] open)
        {
            for (int f = 0; f < open.Length; f++)
            {
                if (open[f] == null)
                {
                    open[f] = new List<int>(64);
                }
                open[f].Clear();
            }
            EmployerTable et = s.Employers;
            for (int e = 0; e < et.Count; e++)
            {
                if (!et.Players[e] && et.Vacancies(e) > 0)
                {
                    open[PopulationCatalogue.Clamp(et.Field[e], 0, PopulationCatalogue.Fields - 1)].Add(e);
                }
            }
        }

        /// <summary>
        /// The employer a person would take a job at: a few with vacancies in their field (any field's, for one with none), the nearest to home;
        /// -1 for none. The list is kept: a full employer is taken off it.
        /// </summary>
        public static int Match(PopulationState s, int p, List<int>[] open, int day)
        {
            PersonTable pt = s.People;
            int field = PopulationCatalogue.Clamp(pt.Field[p] > 0 ? pt.Field[p] : (int)Field.Service, 1, PopulationCatalogue.Fields - 1);
            List<int> list = open[field];
            if (list.Count == 0)
            {
                list = open[(int)Field.Service].Count > 0 ? open[(int)Field.Service] : open[(int)Field.Office];
            }
            if (list.Count == 0)
            {
                return -1;
            }
            int h = pt.Household[p];
            int home = h >= 0 ? s.Households.District[h] : 0;
            int best = -1, bestAt = -1, bestDistance = int.MaxValue;
            for (int look = 0; look < Looks; look++)
            {
                int at = PopDraw.Range(s.Seed, PopDraw.Find, pt.Id[p], day * 8 + look, list.Count);
                int e = list[at];
                if (s.Employers.Vacancies(e) <= 0)
                {
                    continue;
                }
                int b = s.Employers.Building[e];
                int distance = b >= 0 ? CityTable.Distance(s, home, s.Buildings[b].District) : 4000;
                if (distance < bestDistance)
                {
                    best = e;
                    bestAt = at;
                    bestDistance = distance;
                }
            }
            if (best >= 0 && s.Employers.Vacancies(best) <= 1)
            {
                list[bestAt] = list[list.Count - 1];   // full once this one is taken on
                list.RemoveAt(list.Count - 1);
            }
            return best;
        }

        /// <summary>
        /// A person's day at work or looking for it: a job ends at the turnover's rate (into retirement past the age), a jobseeker finds one at a
        /// rate that falls as vacancies grow scarce against seekers, a worker's skill grows over the year. Not for linked people: their job is
        /// another system's.
        /// </summary>
        public static void Day(PopulationState s, int p, int day, List<int>[] open, int tightness)
        {
            PersonTable pt = s.People;
            if ((pt.Flags[p] & (PersonFlags.Linked | PersonFlags.Frozen)) != 0)
            {
                return;
            }
            var status = (Status)pt.Status[p];
            int id = pt.Id[p];
            if (status == Status.Worker)
            {
                if (PopDraw.Daily(s.Seed, PopDraw.Quit, id, day, PopulationCatalogue.Turnover, s.YearDays))
                {
                    Lose(s, p, day, false);
                }
                else if ((day - pt.Since[p]) % Math.Max(1, s.YearDays) == 0 && day != pt.Since[p])
                {
                    pt.Skill[p] = (byte)Math.Min(100, pt.Skill[p] + SkillPerYear);
                }
                return;
            }
            if (status != Status.Unemployed)
            {
                return;
            }
            // tightness: thousandths of vacancies over seekers; plenty of work finds it at the full rate
            int rate = (int)((long)FindRate * Math.Min(1000, tightness) / 1000);
            if (PopDraw.Daily(s.Seed, PopDraw.Find, id, day, rate, s.YearDays))
            {
                int e = Match(s, p, open, day);
                if (e >= 0)
                {
                    Hire(s, p, e, day);
                }
            }
        }

        /// <summary>
        /// Thousandths of the region's usual fill of the city's vacancies while so many thousandths of the city's own labour force are out of work: all
        /// of it at the start rate or under, falling to <see cref="CommuterFillLeast"/> at twice it. Employers take those who live near first, so the
        /// commuters are fewer while the city's own people are looking; a bigger city fills more of its own jobs.
        /// </summary>
        public static int LocalFill(int residentOut)
        {
            int start = PopulationCatalogue.UnemploymentStart;
            if (residentOut <= start)
            {
                return 1000;
            }
            if (residentOut >= start * 2)
            {
                return CommuterFillLeast;
            }
            return 1000 - (1000 - CommuterFillLeast) * (residentOut - start) / start;
        }

        /// <summary>
        /// The employers' day: commuters leave at the turnover's rate and the region fills a share of each one's vacancies while it has people
        /// to send, less while the city's own are out of work (<see cref="LocalFill"/>). The players' employers are the player's to staff.
        /// </summary>
        public static void Commuters(PopulationState s, int day, int residentOut)
        {
            int fill = (int)((long)PopulationCatalogue.CommuterFillMonth / GenerateRules.MonthDays(s.YearDays) * LocalFill(residentOut) / 1000);
            EmployerTable et = s.Employers;
            RegionState r = s.Region;
            int free = Math.Max(0, (int)((long)r.Pool * (1000 - PopulationCatalogue.RegionWorkersShare) / 1000) - r.Commuting);
            for (int e = 0; e < et.Count; e++)
            {
                if (et.Players[e])
                {
                    continue;
                }
                int leaving = (int)((long)et.Commuters[e] * PopulationCatalogue.Turnover / 1000 / Math.Max(1, s.YearDays));
                leaving += PopDraw.Mille(s.Seed, PopDraw.Commuter, e, day, (int)((long)et.Commuters[e] * PopulationCatalogue.Turnover / Math.Max(1, s.YearDays) % 1000)) ? 1 : 0;
                leaving = Math.Min(leaving, et.Commuters[e]);
                et.Commuters[e] -= leaving;
                r.Commuting -= leaving;
                free += leaving;
                int vacancies = et.Vacancies(e);
                int coming = Math.Min(free, (int)((long)vacancies * fill / 1000));
                if (coming <= 0 && vacancies > 0 && free > 0 && PopDraw.Mille(s.Seed, PopDraw.Commuter, e, day + 1, (int)((long)vacancies * fill % 1000)))
                {
                    coming = 1;
                }
                et.Commuters[e] += coming;
                r.Commuting += coming;
                free -= coming;
            }
        }

        /// <summary>
        /// The level a field's pay is drawn to, thousandths of its start, by the share of its own labour force out of work (thousandths) against
        /// the city's usual share: the wage curve, within its band.
        /// </summary>
        public static int WageTarget(int outOfWork)
        {
            int usual = PopulationCatalogue.UnemploymentStart;
            long off = (long)WageCurve * (Math.Max(0, outOfWork) - usual) / usual;
            return PopulationCatalogue.Clamp((int)(1000 - off), WageTargetLeast, WageTargetMost);
        }

        /// <summary>
        /// The month's pay: each field's moves part of the way to the level its own people out of work set (<see cref="WageTarget"/>), so it
        /// falls while they look and comes back as they find work; workers' pay closes on the rate.
        /// </summary>
        public static void Month(PopulationState s, int[] seekers, int[] workers)
        {
            for (int f = 1; f < PopulationCatalogue.Fields && f < s.FieldWage.Length; f++)
            {
                if (seekers[f] + workers[f] <= 0)
                {
                    continue;
                }
                int outOfWork = (int)(1000L * seekers[f] / (seekers[f] + workers[f]));
                int w = s.FieldWage[f];
                w += (int)((long)(WageTarget(outOfWork) - w) * WageAdjust / 1000);
                s.FieldWage[f] = PopulationCatalogue.Clamp(w, WageLeast, WageMost);
            }
            PersonTable pt = s.People;
            for (int p = 0; p < pt.Count; p++)
            {
                if (pt.Id[p] == 0 || pt.Status[p] != (byte)Status.Worker || (pt.Flags[p] & PersonFlags.Linked) != 0)
                {
                    continue;
                }
                int h = pt.Household[p];
                int going = PayFor(s, pt.Field[p], pt.Skill[p], h >= 0 ? s.Households.Class[h] : 1);
                pt.Pay[p] += (int)((long)(going - pt.Pay[p]) * WageFollow / 1000);
            }
        }

        /// <summary>A commuter's pay a day at an employer, cents: the going rate of its field at a middling skill, full time.</summary>
        public static int CommuterPay(PopulationState s, int e)
        {
            return (int)((long)PayFor(s, s.Employers.Field[e], 40, 1) * PopulationCatalogue.FullTimeHours / PopulationCatalogue.DaysPerWeek);
        }

        /// <summary>A person's pay a day, cents: their hourly pay for their hours, a week's spread over its seven days.</summary>
        public static int DailyPay(PopulationState s, int p)
        {
            return (int)Math.Min(int.MaxValue / 4, (long)s.People.Pay[p] * s.People.Hours[p] / PopulationCatalogue.DaysPerWeek);
        }
    }
}
