using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RealLifeOverhaul.Ui;

// The Company samples' one world: the people, the places, the seats, the campaigns, the licences, the tax quarters and a single today, written once. Every sample page asks it for the names,
// roles, wages, headcounts, counts and days it shows, so that no tab says what another contradicts, and the check over every state (UiChecks.Company.cs, CompanyWorldChecks) reads the
// pages against it. The sample's own names never ship: the live source reads the player's company.
internal static partial class UiSamples
{
    /// <summary>A person of the company: an employee at a place, and the post they hold if any.</summary>
    internal sealed class CPerson
    {
        public string Id, Name, Place, Skill;       // Place is the place key ("Speedy Bites~7"); Skill the English name of the work they do
        public int Game, Raise, Going;              // hundredths an hour: the game's wage, the raise agreed on top, the going rate for the work
        public int Standing, Mood, Hue100;          // 0 to 100; the hue in hundredths
        public int Post;                            // 0 crew, 1 a place's manager, 2 a district's manager
        public int MSkill;                          // the manager skill, for those who could be one
        public int Asked, AnswerBy;                 // the raise asked, in hundredths an hour (0 for none), and the day to answer by
        public string Rival;                        // a rival that offers more
        public int OfferWage, OfferBy;
        public int[] RaiseDays = new int[0];        // the days the agreed raises were given, and their sizes in hundredths
        public int[] RaiseSizes = new int[0];
        public int Band = 110;                      // a manager's raise limit, per cent of the going rate
        public int Decided, AskedYou, Spent, Budget = 400;   // a manager's month: decisions, questions they passed up, spent of the budget
        public int Hired = 1;                       // the day they joined
        public int Since;                           // the day they became a manager
        public int FarSkill;                        // a district manager's skill when standing in for a place
        public int Least, Most;                     // the hours a week they will work, from and to; Most 0 for no say

        public int City => 100;                                                     // the city's prices, added to the game's wage
        public int Paid => Game + City + Raise;
        public int Under => Math.Max(0, Going - Paid);
        public float Hue => Hue100 / 100f;
        public bool Low => Standing < CompanyWorld.LowBelow;
    }

    /// <summary>A place of the company.</summary>
    internal sealed class CPlace
    {
        public string Key, Name, Address, District, Kind;   // Kind: "Shop", "Cafe", "Office", "Warehouse", "Gym"
        public int Keep;                                    // the headcount hiring keeps the place at
        public int GapSince;                                // the day the crew fell short of Keep; 0 for never
        public bool NoRota;                                 // a warehouse keeps none
        public int Takings, Stock, Rent, Marketing;         // a typical day's takings and its costs other than wages, whole money
        public int LeaseDay, LeaseRent, LeaseOffer;         // the day the lease renews, the rent a month now and the rent offered
        public bool Books = true;                           // the game has a statement for it
        public int Pct = 100;                               // what its district counts for in a campaign, per cent
        public int Hide;                                    // cash kept off the books, per cent
        public int Property;                                // the property tax so far this year on a building it owns; 0 for rented premises
        public string ManagerId;                            // the manager's person id, or null for an open post
    }

    /// <summary>An executive seat: who sits in it, or nobody.</summary>
    internal sealed class CSeat
    {
        public int Number;                                  // ExecSeat's number
        public string Title;                                // the seat's English name
        public string Name;                                 // null for an empty seat
        public int Since, Hue100;
        public string Role, Level;                          // the talent role and level the holder was hired as
        public int Going, Allowance;
    }

    /// <summary>A professional the owner could put in a seat.</summary>
    internal sealed class CPro
    {
        public string Id, Name, Role, Level;
        public int Going, Hue100;
    }

    /// <summary>A kind of campaign: how long it runs, its own effect before the district and the economy count, and what it costs.</summary>
    internal sealed class CKind
    {
        public string Name;
        public int Days, Own, Cost;
    }

    /// <summary>A policy a place holds: the cover, the insurer, the premium a day and the day it runs to.</summary>
    internal sealed class CCover
    {
        public string Place, Cover, Insurer;
        public int Premium, Until;
    }

    /// <summary>A campaign a business runs: the days are the director's (from, and the first day without it).</summary>
    internal sealed class CCampaign
    {
        public string Place, Kind;
        public int From, End, Percent, Cost;
    }

    /// <summary>A campaign that ended, with its report.</summary>
    internal sealed class CReport
    {
        public int Index;
        public string Place, Kind;
        public int From, Days, Percent, Extra, Cost;
    }

    /// <summary>A question a manager passed up: who asked, where, when, who holds it, and whether it is still open.</summary>
    internal sealed class CAsk
    {
        public int Id, Day;
        public string Kind;                         // "raise", "lease" or "budget"
        public string Asker, Place, Holder;         // person id, place key, and the holder ("you", or the person's id)
        public bool Open;
    }

    /// <summary>A permit a business needs or holds. State: valid, auto, soon, missing, pending.</summary>
    internal sealed class CLicence
    {
        public string Place, Kind, State;
        public int Code, Until;                             // LicenceKind's number; the last day it holds or the day the city decides
        public bool Missing => State == "missing";
    }

    /// <summary>An insurance claim.</summary>
    internal sealed class CClaim
    {
        public string Place, Cover;
        public int Day, By;
        public bool Open;
    }

    /// <summary>A tax quarter's days: the quarter running now, the return waiting for the one before, and the returns filed.</summary>
    internal sealed class CTax
    {
        public int From, Days, Number, ReturnQuarter, ReturnTakings, ReturnCosts, ReturnTax, ReturnPenalties, ReturnDue, Percent;
        public bool ReturnWaiting;
        public List<int[]> Filed = new List<int[]>();       // quarter, day filed, tax, late (0 or 1)
        public int Reserve;
        public bool ReserveAuto;                            // the finance chief tops the reserve up when a quarter closes
    }

    /// <summary>One company's world: everything the Company tabs read.</summary>
    internal sealed class CompanyWorld
    {
        public const int LowBelow = 40;             // standing under which a person counts as low
        public const int ReturnDays = 5;            // days to file a return (CompanyCatalogue.ReturnDays)
        public const int QuarterDays = 90;          // days in a tax quarter (CompanyCatalogue.QuarterMonths of a 30 day month)
        public const int Kinds = 4;

        public string Name, Mark, Owner, Head;              // Head is the head office's address
        public string Weekday => WeekdayOf(Today);
        public int Today, FirstDay, Week;
        public bool Deep;
        public bool HandOver;                       // the owner has taken decisions back: fewer are held by the managers
        public readonly List<CPlace> Places = new List<CPlace>();
        public readonly List<CPerson> People = new List<CPerson>();
        public readonly List<CSeat> Seats = new List<CSeat>();
        public readonly List<CPro> Pros = new List<CPro>();
        public readonly List<CCampaign> Campaigns = new List<CCampaign>();
        public readonly List<CReport> Reports = new List<CReport>();
        public readonly List<CCover> Covers = new List<CCover>();
        public int Cash = 48210;                    // in the bank

        // The kinds of campaign there are, as the booking page lists them.
        public static readonly CKind[] CampaignKinds =
        {
            new CKind { Name = "Flyers", Days = 4, Own = 11, Cost = 180 },
            new CKind { Name = "Radio", Days = 4, Own = 8, Cost = 620 },
            new CKind { Name = "Discount week", Days = 7, Own = 5, Cost = 220 }
        };

        public static CKind KindOf(string name)
        {
            foreach (CKind k in CampaignKinds)
            {
                if (k.Name == name)
                {
                    return k;
                }
            }
            throw new InvalidOperationException("No campaign kind " + name);
        }

        /// <summary>A campaign's effect on customers: its own figure counted at the district's share and the economy's (a whole per cent, rounded).</summary>
        public int Effect(CKind kind, CPlace pl) => (kind.Own * pl.Pct * 100 + 5000) / 10000;

        // What a permit costs, how long it runs and how many days the city takes over an application, by the permit's number (CompanyCatalogue's tables; the check holds them equal).
        public static readonly int[] LicenceFee = { 400, 900, 300, 150, 350, 250, 200, 600 };
        public static readonly int[] LicenceTerm = { 30, 30, 20, 40, 60, 30, 30, 60 };
        public const int LicenceFineMultiple = 2;

        public static readonly string[] CoverKinds = { "Fire", "Theft", "Liability" };
        public static readonly int[] CoverPremium = { 14, 11, 9 };
        public readonly List<CLicence> Licences = new List<CLicence>();
        public readonly List<CClaim> Claims = new List<CClaim>();
        public readonly List<CAsk> Asks = new List<CAsk>();
        public CTax Tax = new CTax();
        public string Region = "Midtown";           // the district a regional manager looks over

        // ---------------------------------------------------------------------------------------- people and places

        public CPerson P(string name)
        {
            for (int i = 0; i < People.Count; i++)
            {
                if (People[i].Name == name || People[i].Id == name)
                {
                    return People[i];
                }
            }
            throw new InvalidOperationException("The sample world has no " + name);
        }

        public CPerson PersonOrNull(string name)
        {
            for (int i = 0; i < People.Count; i++)
            {
                if (People[i].Name == name)
                {
                    return People[i];
                }
            }
            return null;
        }

        public CPlace Pl(string key)
        {
            for (int i = 0; i < Places.Count; i++)
            {
                if (Places[i].Key == key || Places[i].Name == key)
                {
                    return Places[i];
                }
            }
            throw new InvalidOperationException("The sample world has no place " + key);
        }

        /// <summary>The place's manager, or null for an open post.</summary>
        public CPerson ManagerOf(CPlace place) => place.ManagerId == null ? null : P(place.ManagerId);

        /// <summary>Everybody who works at a place, the manager among them: the headcount the company's own staff count adds up.</summary>
        public List<CPerson> StaffAt(string place)
        {
            var list = new List<CPerson>();
            for (int i = 0; i < People.Count; i++)
            {
                if (People[i].Place == place)
                {
                    list.Add(People[i]);
                }
            }
            return list;
        }

        /// <summary>The crew of a place: its staff, its manager and any district manager not counted.</summary>
        public List<CPerson> Crew(string place)
        {
            var list = new List<CPerson>();
            CPlace pl = Pl(place);
            for (int i = 0; i < People.Count; i++)
            {
                if (People[i].Place == place && People[i].Id != pl.ManagerId && People[i].Post == 0)
                {
                    list.Add(People[i]);
                }
            }
            return list;
        }

        /// <summary>How the crew of a place stands: the mean of the standing of everybody who works there, as the business page and the director's StandingAt say it.</summary>
        public int CrewStanding(string place)
        {
            List<CPerson> crew = StaffAt(place);
            if (crew.Count == 0)
            {
                return -1;
            }
            int sum = 0;
            for (int i = 0; i < crew.Count; i++)
            {
                sum += crew[i].Standing;
            }
            return (sum + crew.Count / 2) / crew.Count;
        }

        /// <summary>Everybody who holds a manager's file: a place's manager, a district's.</summary>
        public List<CPerson> Managers()
        {
            var list = new List<CPerson>();
            for (int i = 0; i < People.Count; i++)
            {
                if (People[i].Post != 0)
                {
                    list.Add(People[i]);
                }
            }
            return list;
        }

        /// <summary>How the people of a district stand: the mean of everybody who works at its places.</summary>
        public int StandingIn(string district)
        {
            int sum = 0, n = 0;
            foreach (CPlace pl in Places)
            {
                if (pl.District != district)
                {
                    continue;
                }
                foreach (CPerson p in StaffAt(pl.Key))
                {
                    sum += p.Standing;
                    n++;
                }
            }
            return n == 0 ? -1 : (sum + n / 2) / n;
        }

        public int OpenAsksOf(string place)
        {
            int n = 0;
            foreach (CAsk a in Asks)
            {
                n += a.Open && a.Place == place ? 1 : 0;
            }
            return n;
        }

        public int PlaceManagers()
        {
            int n = 0;
            for (int i = 0; i < Places.Count; i++)
            {
                n += Places[i].ManagerId != null ? 1 : 0;
            }
            return n;
        }

        public List<CPlace> OpenPosts()
        {
            var list = new List<CPlace>();
            for (int i = 0; i < Places.Count; i++)
            {
                if (Places[i].ManagerId == null)
                {
                    list.Add(Places[i]);
                }
            }
            return list;
        }

        public List<CPerson> Asking()
        {
            var list = new List<CPerson>();
            for (int i = 0; i < People.Count; i++)
            {
                if (People[i].Asked > 0)
                {
                    list.Add(People[i]);
                }
            }
            return list;
        }

        public List<CPerson> Underpaid()
        {
            var list = new List<CPerson>();
            for (int i = 0; i < People.Count; i++)
            {
                if (People[i].Under > 0)
                {
                    list.Add(People[i]);
                }
            }
            return list;
        }

        public List<CPerson> LowStanding()
        {
            var list = new List<CPerson>();
            for (int i = 0; i < People.Count; i++)
            {
                if (People[i].Low)
                {
                    list.Add(People[i]);
                }
            }
            list.Sort((a, b) => a.Standing.CompareTo(b.Standing));
            return list;
        }

        public CSeat Seat(int number)
        {
            for (int i = 0; i < Seats.Count; i++)
            {
                if (Seats[i].Number == number)
                {
                    return Seats[i];
                }
            }
            return null;
        }

        public CPro Pro(string id)
        {
            for (int i = 0; i < Pros.Count; i++)
            {
                if (Pros[i].Id == id)
                {
                    return Pros[i];
                }
            }
            throw new InvalidOperationException("The sample world has no professional " + id);
        }

        // ---------------------------------------------------------------------------------------- the rota

        /// <summary>A place's week: who works which hours on which day, as the rota writes it.</summary>
        internal sealed class CRota
        {
            public CPerson[] People;                // the crew, and one more where a hire is in the plan
            public bool[] ToHire;
            public string[][] Shifts;               // per person, per day Monday to Sunday: "07-15", "07-11, 13-17" or null
            public int[] Hours;                     // per person, the week's hours
            public int[] DayTotals;
            public int Total, ShiftCount, From, To, Length, Open, Short;
            public int[][] Need;                    // blocks of from, to, crew
            public int[] Starts;
        }

        /// <summary>The week the crew of a place works on the plan in use (variant 0), or on the one a rung of the planner's ladder gives (1 and 2, the second with a hire).</summary>
        public CRota RotaOf(CPlace pl, int variant = 0, bool hire = false)
        {
            bool cafe = pl.Kind == "Cafe" && Places.Count == 1 || pl.Name == "Corner Cafe";
            List<CPerson> crew = StaffAt(pl.Key);   // everybody who works there, the manager among them, as every page counts the staff
            var r = new CRota { Starts = cafe ? new[] { 7, 12 } : new[] { 7, 11, 15 }, Length = cafe ? 7 : 8, From = 7, To = cafe ? 19 : 23 };
            r.Need = cafe ? new[] { new[] { 7, 12, 1 }, new[] { 12, 19, 2 } } : new[] { new[] { 7, 11, 2 }, new[] { 11, 15, 5 }, new[] { 15, 23, 3 } };
            int count = crew.Count, n = count + (hire ? 1 : 0);
            r.People = new CPerson[n];
            r.ToHire = new bool[n];
            r.Shifts = new string[n][];
            r.Hours = new int[n];
            r.DayTotals = new int[7];
            r.Open = (r.To - r.From) * 7;
            for (int i = 0; i < n; i++)
            {
                bool toHire = i >= count;
                r.ToHire[i] = toHire;
                r.People[i] = toHire ? null : crew[i];
                r.Shifts[i] = new string[7];
                int offA = (i * 2 + variant) % 7, offB = (i * 2 + 3 + variant) % 7;
                for (int d = 0; d < 7; d++)
                {
                    if (d == offA || d == offB || (toHire && d == 0))
                    {
                        continue;
                    }
                    int from = r.Starts[i % r.Starts.Length];
                    string shift = from.ToString("00") + "-" + (from + r.Length).ToString("00");
                    if (!toHire && variant == 0 && i == (count > 5 ? 3 : 0) && d >= 3)
                    {
                        // A split day, as the live rota writes it: two runs with two hours between, the same hours worked.
                        int first = r.Length / 2;
                        shift = from.ToString("00") + "-" + (from + first).ToString("00") + ", " + (from + first + 2).ToString("00") + "-" + (from + r.Length + 2).ToString("00");
                    }
                    r.Shifts[i][d] = shift;
                    r.Hours[i] += r.Length;
                    r.DayTotals[d] += r.Length;
                    r.ShiftCount++;
                }
                r.Total += r.Hours[i];
            }
            for (int d = 0; d < 7; d++)
            {
                for (int h = r.From; h < r.To; h++)
                {
                    int need = 0, on = 0;
                    foreach (int[] b in r.Need)
                    {
                        need = h >= b[0] && h < b[1] ? b[2] : need;
                    }
                    for (int i = 0; i < n; i++)
                    {
                        on += OnShiftAt(r.Shifts[i][d], h) ? 1 : 0;
                    }
                    r.Short += on < need ? 1 : 0;
                }
            }
            return r;
        }

        /// <summary>Whether a day written as the rota writes it ("07-15", or runs joined by a comma and a space) has the person at work in an hour.</summary>
        public static bool OnShiftAt(string day, int hour)
        {
            if (day == null)
            {
                return false;
            }
            foreach (string run in day.Split(new[] { ", " }, StringSplitOptions.None))
            {
                string[] ab = run.Split('-');
                if (ab.Length == 2 && int.Parse(ab[0], CultureInfo.InvariantCulture) <= hour && hour < int.Parse(ab[1], CultureInfo.InvariantCulture))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>What a manager is allowed to spend a day, at the prices of a thousand tenths of a point: a base and a sum for each point of skill, to the nearest ten (ManagerRules.Allowance).</summary>
        public static int Allowance(int skill, bool region = false) => ((region ? 80 + 2 * skill : 30 + skill) * 1000 + 5000) / 10000 * 10;

        // ---------------------------------------------------------------------------------------- campaigns, permits, claims

        public CCampaign CampaignAt(string place)
        {
            for (int i = 0; i < Campaigns.Count; i++)
            {
                if (Campaigns[i].Place == place)
                {
                    return Campaigns[i];
                }
            }
            return null;
        }

        public int Running()
        {
            int n = 0;
            for (int i = 0; i < Campaigns.Count; i++)
            {
                n += Today >= Campaigns[i].From && Today < Campaigns[i].End ? 1 : 0;
            }
            return n;
        }

        public CompanyDays.Campaign Days(CCampaign c) => CompanyDays.Campaign.Of(c.From, c.End, Today);

        public int ShopsMissingLicence()
        {
            var seen = new List<string>();
            for (int i = 0; i < Licences.Count; i++)
            {
                if (Licences[i].Missing && !seen.Contains(Licences[i].Place))
                {
                    seen.Add(Licences[i].Place);
                }
            }
            return seen.Count;
        }

        public int OpenClaims()
        {
            int n = 0;
            for (int i = 0; i < Claims.Count; i++)
            {
                n += Claims[i].Open ? 1 : 0;
            }
            return n;
        }

        public CompanyDays.Quarter Quarter => CompanyDays.Quarter.Of(Tax.From, Tax.Days, Today, ReturnDays);

        /// <summary>The sidebar's counts, from the same function the live source uses.</summary>
        public string[] Counts(bool managersOn = true) => CompanyDays.Counts(People.Count, managersOn ? Managers().Count : 0, ShopsMissingLicence(), Tax.ReturnWaiting, OpenClaims(), Running());

        // ---------------------------------------------------------------------------------------- the days every sample date comes from

        /// <summary>Every day number a page may say, with what it is: a page that writes another one is on a different calendar.</summary>
        public readonly List<int> Calendar = new List<int>();

        public int D(int day)
        {
            if (!Calendar.Contains(day))
            {
                Calendar.Add(day);
            }
            return day;
        }

        // ---------------------------------------------------------------------------------------- money

        public const int DayHours = 8;                      // hours a person works a day, for what the wages a day come to

        /// <summary>What a place pays in wages in a typical day: its people's pay for a day's hours.</summary>
        public int Wages(CPlace pl)
        {
            int hundredths = 0;
            foreach (CPerson p in StaffAt(pl.Key))
            {
                hundredths += p.Paid * DayHours;
            }
            return (hundredths + 50) / 100;
        }

        public int Costs(CPlace pl) => Wages(pl) + pl.Stock + pl.Rent + pl.Marketing;

        public int Result(CPlace pl) => pl.Takings - Costs(pl);

        public int DayTakings() { int n = 0; foreach (CPlace p in Places) { n += p.Takings; } return n; }

        public int DayCosts() { int n = 0; foreach (CPlace p in Places) { n += Costs(p); } return n; }

        public int DayWages() { int n = 0; foreach (CPlace p in Places) { n += Wages(p); } return n; }

        public int DayStock() { int n = 0; foreach (CPlace p in Places) { n += p.Stock; } return n; }

        public int DayRent() { int n = 0; foreach (CPlace p in Places) { n += p.Rent; } return n; }

        public int DayMarketing() { int n = 0; foreach (CPlace p in Places) { n += p.Marketing; } return n; }

        /// <summary>The weekday of a day: day 214 is a Tuesday.</summary>
        public static string WeekdayOf(int day)
        {
            string[] names = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            return names[((day + 4) % 7 + 7) % 7];
        }

        // The tax arithmetic, the same as TaxRules: what is left of takings after the costs, the tax on it, and the penalty on a late return. The check holds them equal.
        public static int Taxable(int takings, int costs) => Math.Max(0, takings - Math.Max(0, costs));

        public static int QuarterTax(int takings, int costs, int percent) => Taxable(takings, costs) * Math.Max(0, percent) / 100;

        public static int Penalty(int takings) => Math.Max(200, Math.Min(5000, (Math.Max(0, takings) * 5 + 500) / 1000));

        /// <summary>The takings and costs of the days of the quarter that have been closed, at a typical day's.</summary>
        public int QuarterTakings() => (Today - Tax.From) * DayTakings();

        public int QuarterCosts() => (Today - Tax.From) * DayCosts();

        public int ReturnTakingsOf() => QuarterDays * DayTakings();

        public int ReturnCostsOf() => QuarterDays * DayCosts();

        public int ReturnTaxOf() => QuarterTax(ReturnTakingsOf(), ReturnCostsOf(), Tax.Percent);

        /// <summary>The tax of the quarters already closed in the tax year: those filed and the one waiting.</summary>
        public int YearTax()
        {
            int n = Tax.ReturnWaiting ? ReturnTaxOf() : 0;
            foreach (int[] f in Tax.Filed)
            {
                n += f[2];
            }
            return n;
        }

        public int Still() => Math.Max(0, YearTax() + QuarterTax(QuarterTakings(), QuarterCosts(), Tax.Percent) - Tax.Reserve);

        public static string Wage(int hundredths) => "$" + (hundredths / 100).ToString(CultureInfo.InvariantCulture) + "." + (hundredths % 100).ToString("00", CultureInfo.InvariantCulture);

        public static string PerHour(int hundredths) => Wage(hundredths) + " / h";

        // ---------------------------------------------------------------------------------------- building

        private CPlace AddPlace(string name, int number, string address, string district, string kind, int keep, string manager = null, bool noRota = false)
        {
            var pl = new CPlace { Key = name + "~" + number, Name = name, Address = address, District = district, Kind = kind, Keep = keep, ManagerId = manager, NoRota = noRota };
            Places.Add(pl);
            return pl;
        }

        private static CPlace Money(CPlace pl, int takings, int stock, int rent, int marketing, int leaseDay, int leaseRent, int leaseOffer = 0)
        {
            pl.Takings = takings;
            pl.Stock = stock;
            pl.Rent = rent;
            pl.Marketing = marketing;
            pl.LeaseDay = leaseDay;
            pl.LeaseRent = leaseRent;
            pl.LeaseOffer = leaseOffer;
            return pl;
        }

        private CPerson AddPerson(string id, string name, CPlace place, string skill, int game, int raise, int going, int standing, int mood, int hue, int post = 0, int mskill = 0)
        {
            var p = new CPerson { Id = id, Name = name, Place = place.Key, Skill = skill, Game = game, Raise = raise, Going = going, Standing = standing, Mood = mood, Hue100 = hue, Post = post, MSkill = mskill };
            People.Add(p);
            return p;
        }

        private static CPerson WantsRaise(CPerson p, int wage, int by)
        {
            p.Asked = wage;
            p.AnswerBy = by;
            return p;
        }

        private static CPerson Raised(CPerson p, params int[] daysAndSizes)
        {
            p.RaiseDays = new int[daysAndSizes.Length / 2];
            p.RaiseSizes = new int[daysAndSizes.Length / 2];
            for (int i = 0; i + 1 < daysAndSizes.Length; i += 2)
            {
                p.RaiseDays[i / 2] = daysAndSizes[i];
                p.RaiseSizes[i / 2] = daysAndSizes[i + 1];
            }
            return p;
        }

        private static int Sum(int[] a)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++)
            {
                n += a[i];
            }
            return n;
        }

        /// <summary>Pioneer Holdings: four businesses, twenty eight people, three place managers and one district manager, two executives and an empty finance seat, on day 214.</summary>
        public static CompanyWorld Pioneer()
        {
            var w = new CompanyWorld { Name = "Pioneer Holdings", Mark = "PH", Owner = "Alex Moreno", Head = "7 5th Avenue, Midtown", Today = 214, FirstDay = 1, Week = 31, Deep = true };
            CPlace sb = w.AddPlace("Speedy Bites", 7, "7 5th Avenue", "Midtown", "Cafe", 11, "ray");
            sb.GapSince = 209;
            CPlace bc = w.AddPlace("Bloom & Co", 214, "41 West 23rd Street", "Chelsea", "Shop", 6, "tessa");
            CPlace pg = w.AddPlace("Pioneer Game Studio", 9, "88 Broadway", "Midtown", "Office", 7);
            CPlace hw = w.AddPlace("Hudson Warehouse", 4, "310 Lenox Avenue", "Harlem", "Warehouse", 6, "omar", true);
            Money(sb, 4860, 1500, 600, 155, 238, 4200);
            Money(bc, 1980, 700, 160, 0, 266, 2600);
            Money(pg, 2240, 400, 650, 0, 301, 5200);
            Money(hw, 1150, 0, 280, 0, 223, 6300, 6800);

            // Speedy Bites: a manager and nine on the crew. Four of the crew stand low.
            w.AddPerson("ray", "Ray Walsh", sb, "Customer service", 2200, 50, 2350, 80, 74, 55, 1, 78);
            Raised(WantsRaise(w.AddPerson("dev", "Dev Kapoor", sb, "Customer service", 1500, 100, 1900, 52, 47, 35), 1900, 218), 131, 50, 188, 50);
            Raised(w.AddPerson("lena", "Lena Adler", sb, "Cooking", 1500, 50, 1650, 64, 58, 10, 0, 64), 214, 50);
            Raised(w.AddPerson("noa", "Noa Silva", sb, "Customer service", 1380, 100, 1580, 78, 72, 20, 0, 71), 131, 100);
            w.AddPerson("kai", "Kai Brennan", sb, "Cleaning", 1470, 0, 1570, 35, 42, 45, 0, 55);
            w.AddPerson("zara", "Zara Quinn", sb, "Cooking", 1540, 0, 1640, 66, 60, 70, 0, 60);
            w.AddPerson("rui", "Rui Tanaka", sb, "Customer service", 1490, 0, 1590, 28, 35, 50, 0, 49);
            w.AddPerson("ben", "Ben Ortiz", sb, "Cooking", 1450, 0, 1550, 38, 44, 10, 0, 48);
            w.AddPerson("eli", "Eli Novak", sb, "Cleaning", 1460, 0, 1560, 70, 66, 85, 0, 57);
            w.AddPerson("ines", "Ines Duarte", sb, "Customer service", 1520, 0, 1620, 33, 40, 15, 0, 62);
            CPerson lena = w.P("lena");
            lena.Rival = "Harbour Grill";
            lena.OfferWage = 1850;
            lena.OfferBy = 219;
            w.P("dev").Hired = 173;

            // Bloom & Co: a manager and five.
            w.AddPerson("tessa", "Tessa Brandt", bc, "Customer service", 2000, 100, 2200, 81, 70, 90, 1, 82);
            WantsRaise(w.AddPerson("maya", "Maya Okafor", bc, "Customer service", 1720, 0, 1970, 57, 50, 60, 0, 58), 1970, 220);
            Raised(w.AddPerson("hana", "Hana Sato", bc, "Customer service", 1460, 160, 1560, 71, 66, 80, 0, 54), 214, 160);
            w.AddPerson("leo", "Leo Martin", bc, "Customer service", 1500, 0, 1600, 66, 62, 30, 0, 51);
            w.AddPerson("jonah", "Jonah Reyes", bc, "Customer service", 1610, 0, 1710, 74, 68, 55, 0, 63);
            w.AddPerson("pia", "Pia Lund", bc, "Customer service", 1490, 0, 1590, 63, 60, 40, 0, 53);

            // Hudson Warehouse: a manager and five; the district manager works here too.
            w.AddPerson("omar", "Omar Haddad", hw, "Logistics", 2000, 0, 2100, 64, 58, 75, 1, 64);
            w.AddPerson("hugo", "Hugo Brandt", hw, "Driving", 1460, 0, 1560, 62, 58, 65, 0, 56);
            w.AddPerson("mei", "Mei Lin", hw, "Driving", 1460, 0, 1560, 70, 64, 5, 0, 52);
            w.AddPerson("carlos", "Carlos Vega", hw, "Logistics", 1580, 0, 1680, 55, 52, 40, 0, 61);
            w.AddPerson("tomas", "Tomas Reyes", hw, "Cleaning", 1440, 0, 1540, 60, 56, 80, 0, 47);
            w.AddPerson("meera", "Meera Iyer", hw, "Logistics", 2900, 100, 3100, 77, 71, 75, 2, 81);

            // Pioneer Game Studio: six, and nobody manages it.
            Raised(w.AddPerson("sana", "Sana Malik", pg, "Programmer", 2200, 100, 2600, 31, 44, 25, 0, 71), 150, 100);
            Raised(w.AddPerson("noor", "Noor Rahman", pg, "Programmer", 2300, 100, 2500, 69, 64, 60, 0, 59), 120, 100);
            w.AddPerson("lenahoff", "Lena Hoff", pg, "Graphic designer", 2150, 0, 2250, 72, 66, 45, 0, 66);
            Raised(w.AddPerson("ivo", "Ivo Petrov", pg, "Programmer", 2200, 100, 2600, 58, 54, 40, 0, 52), 160, 100);
            w.AddPerson("inespark", "Ines Park", pg, "Programmer", 2150, 0, 2250, 66, 62, 90, 0, 78);
            w.AddPerson("theo", "Theo Marsh", pg, "Graphic designer", 1950, 0, 2050, 61, 57, 30, 0, 64);

            // The managers: the month's decisions, what was passed up to the owner, what was spent of the budget, and the raise limit each holds.
            CPerson ray = w.P("ray"), tessa = w.P("tessa"), omar = w.P("omar"), meera = w.P("meera");
            ray.Band = 95;
            ray.Decided = 86;
            ray.AskedYou = 1;
            ray.Spent = 310;
            tessa.Band = 110;
            tessa.Decided = 52;
            tessa.Spent = 380;
            omar.Band = 105;
            omar.Decided = 33;
            omar.AskedYou = 1;
            omar.Spent = 120;
            meera.Decided = 41;
            meera.Spent = 90;
            ray.Since = 41; tessa.Since = 55; omar.Since = 70; meera.Since = 88;
            meera.FarSkill = 64;
            w.P("dev").MSkill = 66;
            w.Asks.Add(new CAsk { Id = 1, Day = 214, Kind = "raise", Asker = "ray", Place = sb.Key, Holder = "you", Open = true });
            w.Asks.Add(new CAsk { Id = 2, Day = 213, Kind = "lease", Asker = "omar", Place = hw.Key, Holder = "you", Open = true });
            w.Asks.Add(new CAsk { Id = 3, Day = 211, Kind = "budget", Asker = "tessa", Place = bc.Key, Holder = "tessa", Open = false });
            w.Asks.Add(new CAsk { Id = 7, Day = 201, Kind = "budget", Asker = "ray", Place = sb.Key, Holder = "ray", Open = false });

            // The hours a week the crew of Speedy Bites will work, as the rota planner reads them.
            w.P("zara").Least = 20; w.P("zara").Most = 32;
            w.P("rui").Least = 32; w.P("rui").Most = 40;
            w.P("dev").Least = 20; w.P("dev").Most = 40;
            w.P("lena").Least = 24;
            w.P("noa").Least = 16; w.P("noa").Most = 40;

            w.Seats.Add(new CSeat { Number = 1, Title = "Chief operating officer", Name = "Ethan Brooks", Since = 60, Hue100 = 15, Role = "business_analyst", Level = "senior", Going = 4800, Allowance = 9600 });
            w.Seats.Add(new CSeat { Number = 2, Title = "Chief human resources officer", Name = null });
            w.Seats.Add(new CSeat { Number = 3, Title = "Chief financial officer", Name = "Noah Feldman", Since = 96, Hue100 = 20, Role = "accountant", Level = "senior", Going = 4200, Allowance = 8400 });
            w.Pros.Add(new CPro { Id = "pro1", Name = "Lena Voss", Role = "hr_manager", Level = "senior", Going = 3900, Hue100 = 20 });
            w.Pros.Add(new CPro { Id = "pro2", Name = "Ivan Kolar", Role = "recruiter", Level = "principal", Going = 4700, Hue100 = 50 });
            w.Pros.Add(new CPro { Id = "pro3", Name = "Marta Silva", Role = "business_analyst", Level = "senior", Going = 4600, Hue100 = 20 });
            w.Pros.Add(new CPro { Id = "pro4", Name = "Tobias Wren", Role = "business_analyst", Level = "principal", Going = 5800, Hue100 = 50 });

            // Campaigns: the Radio spot at Speedy Bites began yesterday for four days. Reports of three that ended.
            w.Campaigns.Add(new CCampaign { Place = sb.Key, Kind = "Radio", From = 213, End = 217, Percent = 9, Cost = 620 });
            w.Reports.Add(new CReport { Index = 0, Place = bc.Key, Kind = "Radio", From = 173, Days = 4, Percent = 9, Extra = 280, Cost = 620 });
            w.Reports.Add(new CReport { Index = 1, Place = sb.Key, Kind = "Discount week", From = 182, Days = 7, Percent = 6, Extra = -40, Cost = 220 });
            w.Reports.Add(new CReport { Index = 2, Place = bc.Key, Kind = "Flyers", From = 198, Days = 4, Percent = 24, Extra = 180, Cost = 180 });

            // Permits.
            w.Licences.Add(new CLicence { Place = sb.Key, Kind = "hygiene", Code = 0, State = "auto", Until = 241 });
            w.Licences.Add(new CLicence { Place = sb.Key, Kind = "fire", Code = 4, State = "soon", Until = 216 });
            w.Licences.Add(new CLicence { Place = bc.Key, Kind = "signage", Code = 3, State = "missing", Until = 0 });
            w.Licences.Add(new CLicence { Place = hw.Key, Kind = "bonded", Code = 7, State = "pending", Until = 216 });
            w.Licences.Add(new CLicence { Place = bc.Key, Kind = "hygiene", Code = 0, State = "valid", Until = 250 });
            w.Licences.Add(new CLicence { Place = bc.Key, Kind = "late", Code = 2, State = "valid", Until = 240 });
            sb.Hide = 5;
            hw.Hide = 10;
            hw.Property = 2140;
            sb.Pct = 110;
            bc.Pct = 110;
            pg.Pct = 110;
            hw.Pct = 105;
            w.Covers.Add(new CCover { Place = sb.Key, Cover = "Fire", Insurer = "Safeguard", Premium = 14, Until = 280 });
            w.Covers.Add(new CCover { Place = sb.Key, Cover = "Liability", Insurer = "Safeguard", Premium = 9, Until = 280 });
            w.Covers.Add(new CCover { Place = bc.Key, Cover = "Liability", Insurer = "Safeguard", Premium = 9, Until = 302 });

            w.Claims.Add(new CClaim { Place = sb.Key, Cover = "Fire", Day = 214, By = 220, Open = true });
            w.Claims.Add(new CClaim { Place = sb.Key, Cover = "Liability", Day = 188, By = 193, Open = false });
            w.Claims.Add(new CClaim { Place = bc.Key, Cover = "Liability", Day = 212, By = 217, Open = false });

            // Tax: quarters of ninety days from day 31. The second closed on day 211 and its return waits for three more days; the third has run three.
            w.Tax = new CTax
            {
                From = 211, Days = QuarterDays, Number = 3, ReturnWaiting = true, ReturnQuarter = 2, ReturnDue = 211 + ReturnDays, Percent = 20, Reserve = 30000, ReserveAuto = true
            };
            w.Tax.Filed.Add(new[] { 1, 128, 21500, 1 });
            return w;
        }

        /// <summary>Corner Cafe: one business, two people, on day 7.</summary>
        public static CompanyWorld Cafe()
        {
            var w = new CompanyWorld { Name = "Corner Cafe", Mark = "CC", Owner = "Alex Moreno", Head = "12 Elm Street, Brooklyn", Today = 2, FirstDay = 1, Week = 1, Deep = false, Region = "Brooklyn" };
            CPlace cc = w.AddPlace("Corner Cafe", 12, "12 Elm Street", "Brooklyn", "Cafe", 3);
            cc.GapSince = 1;
            Money(cc, 1100, 280, 62, 0, 61, 1980);
            cc.Pct = 100;
            w.AddPerson("ana", "Ana Costa", cc, "Cooking", 1200, 0, 1300, 72, 66, 45, 0, 45);
            w.AddPerson("jin", "Jin Park", cc, "Customer service", 1200, 0, 1300, 64, 70, 70, 0, 50);
            w.Licences.Add(new CLicence { Place = cc.Key, Kind = "hygiene", Code = 0, State = "valid", Until = 31 });
            w.Licences.Add(new CLicence { Place = cc.Key, Kind = "signage", Code = 3, State = "missing", Until = 0 });
            w.Tax = new CTax { From = 1, Days = QuarterDays, Number = 1, Percent = 20 };
            return w;
        }

        /// <summary>Pioneer with the managers cut back: the flat one has two, and the bare one none, so that the businesses wait for a post to be filled.</summary>
        public static CompanyWorld Bare(bool none)
        {
            CompanyWorld w = Pioneer();
            w.Deep = false;
            w.Seats.Clear();
            w.Pros.Clear();
            CPlace cc = w.AddPlace("Corner Cafe", 12, "12 Elm Street", "Brooklyn", "Cafe", 3);
            CPlace rg = w.AddPlace("Riverside Gym", 15, "9 Hudson Street", "Harlem", "Gym", 3);
            Money(cc, 540, 140, 62, 0, 261, 1980);
            Money(rg, 760, 120, 210, 0, 270, 2900);
            CPlace ub = w.AddPlace("Uptown Books", 17, "52 Amsterdam Avenue", "Chelsea", "Shop", 3);
            CPlace dg = w.AddPlace("Dockside Garage", 18, "6 Pier Road", "Harlem", "Garage", 3);
            Money(ub, 840, 260, 190, 0, 272, 2300);
            Money(dg, 970, 300, 240, 0, 275, 2700);
            w.AddPerson("ana", "Ana Costa", cc, "Cooking", 1200, 0, 1300, 72, 66, 45);
            w.AddPerson("jin", "Jin Park", cc, "Customer service", 1200, 0, 1300, 64, 70, 70);
            w.AddPerson("dana", "Dana Kerr", rg, "Cleaning", 1300, 0, 1400, 68, 64, 20, 0, 44);
            w.AddPerson("milo", "Milo Chen", rg, "Customer service", 1300, 0, 1400, 59, 55, 55, 0, 46);
            w.AddPerson("cora", "Cora Webb", ub, "Customer service", 1250, 0, 1350, 66, 62, 35, 0, 54);
            w.AddPerson("finn", "Finn Hale", ub, "Cleaning", 1250, 0, 1350, 61, 58, 60, 0, 47);
            w.AddPerson("gus", "Gus Moran", dg, "Driving", 1350, 0, 1450, 63, 60, 15, 0, 55);
            w.AddPerson("ivy", "Ivy Stone", dg, "Customer service", 1350, 0, 1450, 69, 64, 50, 0, 52);
            // The regional manager and the warehouse manager do not hold a post here: the flat company has Ray and Tessa, the bare one nobody.
            foreach (CPerson p in w.People)
            {
                if (none || p.Id == "omar" || p.Id == "meera")
                {
                    p.Post = 0;
                }
                if (none && (p.Id == "ray" || p.Id == "tessa"))
                {
                    p.Post = 0;
                }
            }
            foreach (CPlace pl in w.Places)
            {
                if (none || pl.ManagerId == "omar")
                {
                    pl.ManagerId = null;
                }
            }
            return w;
        }

        /// <summary>Every name the sample worlds hold (people, places, addresses, districts, rivals), which no language translates.</summary>
        public static List<string> AllNames()
        {
            var names = new List<string>();
            foreach (CompanyWorld w in new[] { Pioneer(), Cafe(), Bare(false), Bare(true) })
            {
                names.AddRange(new[] { w.Name, w.Owner, w.Head, w.Region });
                foreach (CPerson p in w.People)
                {
                    names.Add(p.Name);
                    if (p.Rival != null)
                    {
                        names.Add(p.Rival);
                    }
                }
                foreach (CPlace p in w.Places)
                {
                    names.AddRange(new[] { p.Name, p.Address, p.District, p.Address + ", " + p.District });
                }
                foreach (CSeat s in w.Seats)
                {
                    names.Add(s.Name);
                }
                foreach (CPro p in w.Pros)
                {
                    names.Add(p.Name);
                }
            }
            names.Add("Blossom Lane");
            names.RemoveAll(x => string.IsNullOrEmpty(x));
            return names.Distinct().ToList();
        }

        /// <summary>The world a state is set in.</summary>
        public static CompanyWorld Of(string state)
        {
            bool one = state.EndsWith("_one", StringComparison.Ordinal);
            CompanyWorld w = one ? Cafe() : state == "managers_flat" ? Bare(false) : state == "managers_none" ? Bare(true) : Pioneer();
            w.HandOver = state == "managers_handover";
            return w;
        }
    }
}
