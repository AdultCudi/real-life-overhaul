using System;
using System.Collections.Generic;
using RealLifeOverhaul.Roommates;

namespace RealLifeOverhaul.Household
{
    /// <summary>Who can be taken on for the home. The numbers are written into the save: never change one.</summary>
    public enum StaffKind
    {
        None = 0,
        Cleaner = 1,    // a few mornings a week: the floor and the surfaces
        Cook = 2,       // every evening: a dish cooked and put by in the fridge
        Butler = 3,     // lives in: cleans, cooks and washes the cars every day
        Mechanic = 4    // once a week: the cars at home washed, repaired and serviced at a garage's prices
    }

    /// <summary>One kind of staff: the days and hours they work, what their wage is measured by, and what a visit does.</summary>
    public sealed class StaffRole
    {
        public StaffKind Kind;
        public string Title;       // locale key: what they are, after "a"
        public string When;        // locale key: their days, with {from} and {to}
        public string Does;        // locale key: what a visit does
        public int Days;           // bit 0 Monday .. bit 6 Sunday
        public int From;           // the hour they start
        public int Hours;          // how long a visit lasts; From + Hours is never past 23
        public int ShareBp;        // basis points of the city's going rate for domestic work; 0: paid by the job (the mechanic)
        public bool LiveIn;
        public bool Cleans, Cooks, Washes, Services;
        public int Salt;           // what tells their candidates apart from the other kinds'
    }

    /// <summary>A person the agency puts forward, before they are taken on.</summary>
    public struct StaffCandidate
    {
        public StaffKind Kind;
        public string Name;
        public bool Female;
        public int Age;
        public int Seed;           // of their look
        public int Skill;          // 1..100
    }

    /// <summary>
    /// The rules of household staff: the four kinds, the candidates the agency puts forward, wages from the city's own going
    /// rate, the hours of a visit, what a visit does to the home and the cars, and the day's note. Game-free and the same on
    /// every machine for the same save: chance comes only from the save's seed (<see cref="RoommateRules.Roll"/>).
    /// </summary>
    public static class HouseholdRules
    {
        public const int Most = 4;                  // people on the staff at once: one of each kind
        public const int QuitAfter = 2;             // visits in a row the player could not pay, after which they leave
        public const int NoteHour = 21;             // the hour the day's note comes, after the last visit of a day
        public const int CatchUpHours = 72;         // the most hours a late look makes up for (a long sleep); older ones are days off
        public const int GoingSkill = 50;           // the skill the city's going rate for domestic work is read at
        public const int FallbackCents = 1000;      // cents an hour when the game's wage formula cannot be read
        public const int SkillLeast = 40, SkillMost = 95;
        public const int AgeLeast = 22, AgeMost = 61;
        public const int CallOut = 95;              // the mechanic's call-out at the start's prices, as car ownership's roadside call-out (CarRules)
        public const int WashFee = 50;              // a car's wash, as the game's own in a repair bay (GasStationOverlay's WashCost)
        public const float DirtyFrom = 0.1f;        // a car is washed above this dirt, as the game's own Wash button is offered
        public const int GroceriesPerPortion = 6;   // at the start's prices: what the cook spends on one portion's food
        public const int MealFill = 45;             // of the game's 100: what a portion the cook put by fills when reheated
        public const float HomeReach = 60f;         // metres from the home's door within which a parked car is at home
        public const int LeftoversMost = HomeBalance.LeftoversMost;   // portions the fridge holds: home cooking's own limit

        private const int Weekdays = 0x1F, EveryDay = 0x7F;

        /// <summary>The dishes the cook makes (HomeBalance.Dishes' numbers): the ones cooked at a cooker that make two portions or more.</summary>
        public static readonly int[] CookDishes = { 8, 9, 10, 11, 12 };

        public static readonly StaffRole[] Roles =
        {
            new StaffRole
            {
                Kind = StaffKind.Cleaner, Title = "reallife_household_kind_cleaner", When = "reallife_household_when_cleaner", Does = "reallife_household_does_cleaner",
                Days = 0x15, From = 10, Hours = 3, ShareBp = 10000, Cleans = true, Salt = 101
            },
            new StaffRole
            {
                Kind = StaffKind.Cook, Title = "reallife_household_kind_cook", When = "reallife_household_when_cook", Does = "reallife_household_does_cook",
                Days = EveryDay, From = 17, Hours = 2, ShareBp = 12500, Cooks = true, Salt = 211
            },
            new StaffRole
            {
                Kind = StaffKind.Butler, Title = "reallife_household_kind_butler", When = "reallife_household_when_butler", Does = "reallife_household_does_butler",
                Days = EveryDay, From = 8, Hours = 10, ShareBp = 15000, LiveIn = true, Cleans = true, Cooks = true, Washes = true, Salt = 307
            },
            new StaffRole
            {
                Kind = StaffKind.Mechanic, Title = "reallife_household_kind_mechanic", When = "reallife_household_when_mechanic", Does = "reallife_household_does_mechanic",
                Days = 0x20, From = 10, Hours = 2, ShareBp = 0, Washes = true, Services = true, Salt = 401
            }
        };

        private static readonly string[] Women = { "Amara", "Bianca", "Carmen", "Dina", "Elena", "Fatima", "Grace", "Hana", "Ines", "Joy", "Lucia", "Marisol", "Nadia", "Olga", "Priya", "Rosa" };
        private static readonly string[] Men = { "Andre", "Benito", "Caleb", "Darius", "Emil", "Felix", "Gideon", "Hector", "Ivan", "Jonah", "Kofi", "Luis", "Mateo", "Nikos", "Omar", "Rafael" };
        private static readonly string[] Surnames =
        {
            "Abara", "Bianchi", "Castillo", "Dimitriou", "Eze", "Fontaine", "Gallagher", "Haddad", "Ivanova", "Jensen", "Kowalski", "Lindqvist", "Moreau", "Novak",
            "Okafor", "Petrova", "Quinn", "Rossi", "Santos", "Takahashi", "Udeh", "Varga", "Whitfield", "Yilmaz"
        };

        /// <summary>The role of a kind; null for none.</summary>
        public static StaffRole Role(StaffKind kind)
        {
            for (int i = 0; i < Roles.Length; i++)
            {
                if (Roles[i].Kind == kind)
                {
                    return Roles[i];
                }
            }
            return null;
        }

        /// <summary>True if they work on this weekday (0 Monday .. 6 Sunday).</summary>
        public static bool Works(StaffRole role, int weekday)
        {
            return role != null && weekday >= 0 && weekday <= 6 && (role.Days & (1 << weekday)) != 0;
        }

        public static bool Starts(StaffRole role, int weekday, int hour) => Works(role, weekday) && hour == role.From;

        public static bool Ends(StaffRole role, int weekday, int hour) => Works(role, weekday) && role.Hours > 0 && hour == role.From + role.Hours;

        /// <summary>True during a visit's hours: from the hour they come to the hour before they go.</summary>
        public static bool AtWork(StaffRole role, int weekday, int hour) => Works(role, weekday) && hour >= role.From && hour < role.From + role.Hours;

        /// <summary>Visits a week.</summary>
        public static int VisitsAWeek(StaffRole role)
        {
            int n = 0;
            for (int d = 0; role != null && d < 7; d++)
            {
                n += (role.Days >> d) & 1;
            }
            return n;
        }

        /// <summary>The city's going rate for domestic work, cents an hour: the game's wage for a cleaner of middling skill, times the city's wage market (thousandths).</summary>
        public static int Going(int formulaCents, int marketPerMille)
        {
            int formula = formulaCents > 0 ? formulaCents : FallbackCents;
            int market = marketPerMille > 0 ? marketPerMille : 1000;
            return (int)Math.Max(1L, Math.Min(int.MaxValue, (long)formula * market / 1000));
        }

        /// <summary>What somebody asks an hour, cents rounded to five: the going rate times their role's share, a little more for more skill.</summary>
        public static int Hourly(int going, StaffRole role, int skill)
        {
            if (role == null || role.ShareBp <= 0 || going <= 0)
            {
                return 0;
            }
            long cents = (long)going * role.ShareBp / 10000 * (8000 + Clamp(skill, 1, 100) * 40) / 10000;
            return (int)Math.Min(int.MaxValue, (cents + 2) / 5 * 5);
        }

        /// <summary>A visit's wage in whole money: the hours at the hourly rate, at least one.</summary>
        public static int VisitWage(int hourlyCents, StaffRole role)
        {
            if (role == null || hourlyCents <= 0 || role.Hours <= 0)
            {
                return 0;
            }
            return (int)Math.Max(1L, ((long)hourlyCents * role.Hours + 50) / 100);
        }

        /// <summary>A week's wages in whole money, for the hiring page.</summary>
        public static int WeekWage(int hourlyCents, StaffRole role) => VisitWage(hourlyCents, role) * VisitsAWeek(role);

        /// <summary>The cook's shopping for so many portions, at today's prices (level in tenths of a point, 1000 at the start).</summary>
        public static int Groceries(int portions, int level)
        {
            return portions <= 0 ? 0 : (int)Math.Max(1L, ((long)GroceriesPerPortion * portions * Math.Max(1, level) + 500) / 1000);
        }

        /// <summary>The mechanic's call-out at today's prices.</summary>
        public static int CallOutAt(int level) => (int)Math.Max(1L, ((long)CallOut * Math.Max(1, level) + 500) / 1000);

        /// <summary>Points of a hundred of the home's tidiness a cleaning puts back: more for more skill, near spotless at the top.</summary>
        public static float CleanPoints(int skill) => 60f + Clamp(skill, 1, 100) * 0.4f;

        /// <summary>
        /// The dish cooked on a day (an index into <see cref="CookDishes"/>): every dish once in each run of as many days, in
        /// an order of the save's own, and never the same two days running (a run that would begin with the last one's dish
        /// begins with its second instead; a run's last dish is never moved, so no day depends on more than the run before).
        /// </summary>
        public static int DishOn(int seed, int day, StaffKind kind)
        {
            int n = CookDishes.Length;
            int run = FloorDiv(day, n), at = day - run * n;
            int first = RunOrder(seed, run, kind, 0), second = RunOrder(seed, run, kind, 1);
            if (first == RunOrder(seed, run - 1, kind, n - 1))
            {
                return at == 0 ? second : at == 1 ? first : RunOrder(seed, run, kind, at);
            }
            return RunOrder(seed, run, kind, at);
        }

        // The dish at a place in one run: a shuffle of the dishes from the save's seed, the same every time.
        private static int RunOrder(int seed, int run, StaffKind kind, int place)
        {
            int n = CookDishes.Length;
            var order = new int[n];   // (once a visit: never on a frame)
            for (int i = 0; i < n; i++)
            {
                order[i] = i;
            }
            for (int i = n - 1; i > 0; i--)
            {
                int k = RoommateRules.Roll(seed, run, 900 + (int)kind * 16 + i) % (i + 1);
                int t = order[i];
                order[i] = order[k];
                order[k] = t;
            }
            return order[place];
        }

        private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        /// <summary>The dish of an index into <see cref="CookDishes"/>, from home cooking's own table; null for none.</summary>
        public static Dish DishAt(int dishIndex)
        {
            if (dishIndex < 0 || dishIndex >= CookDishes.Length)
            {
                return null;
            }
            for (int i = 0; i < HomeBalance.Dishes.Length; i++)
            {
                if (HomeBalance.Dishes[i].Number == CookDishes[dishIndex])
                {
                    return HomeBalance.Dishes[i];
                }
            }
            return null;
        }

        public static int PortionsOf(int dishIndex)
        {
            Dish dish = DishAt(dishIndex);
            return dish != null ? dish.Portions : 0;
        }

        /// <summary>Portions that will go in: a dish's, as far as the fridge has room.</summary>
        public static int PortionsIn(int dishIndex, int leftovers)
        {
            int room = LeftoversMost - Math.Max(0, leftovers);
            return Math.Max(0, Math.Min(PortionsOf(dishIndex), room));
        }

        /// <summary>
        /// The person the agency puts forward for a kind, the <paramref name="n"/>th of this save: the same every time for the
        /// same seed, kind and number.
        /// </summary>
        public static StaffCandidate Candidate(int seed, StaffKind kind, int n)
        {
            StaffRole role = Role(kind);
            int salt = role != null ? role.Salt : 1;
            var random = new Random(unchecked(seed * 31 + salt * 7919 + n * 104729));
            var c = new StaffCandidate { Kind = kind, Seed = random.Next(1, int.MaxValue) };
            c.Female = random.Next(2) == 0;
            c.Name = (c.Female ? Women[random.Next(Women.Length)] : Men[random.Next(Men.Length)]) + " " + Surnames[random.Next(Surnames.Length)];
            c.Age = AgeLeast + random.Next(AgeMost - AgeLeast);
            c.Skill = SkillLeast + random.Next(SkillMost - SkillLeast + 1);
            return c;
        }

        /// <summary>The next kind for the hiring page after <paramref name="kind"/>, skipping those taken on; None when all are.</summary>
        public static StaffKind NextOpen(StaffKind kind, List<StaffMember> staff)
        {
            int at = 0;
            for (int i = 0; i < Roles.Length; i++)
            {
                if (Roles[i].Kind == kind)
                {
                    at = i + 1;
                }
            }
            for (int k = 0; k < Roles.Length; k++)
            {
                StaffKind next = Roles[(at + k) % Roles.Length].Kind;
                if (Find(staff, next) == null)
                {
                    return next;
                }
            }
            return StaffKind.None;
        }

        public static StaffMember Find(List<StaffMember> staff, StaffKind kind)
        {
            for (int i = 0; staff != null && i < staff.Count; i++)
            {
                if (staff[i] != null && staff[i].Kind == kind)
                {
                    return staff[i];
                }
            }
            return null;
        }

        /// <summary>The one shown at home at an hour: the butler before the cook before the cleaner; the mechanic works at the cars, outdoors.</summary>
        public static StaffMember OnShow(List<StaffMember> staff, int day, int weekday, int hour)
        {
            StaffMember best = null;
            int rank = 0;
            for (int i = 0; staff != null && i < staff.Count; i++)
            {
                StaffMember m = staff[i];
                StaffRole role = m != null ? Role(m.Kind) : null;
                if (role == null || role.Kind == StaffKind.Mechanic || m.OnDay != day || !AtWork(role, weekday, hour))
                {
                    continue;
                }
                int r = role.Kind == StaffKind.Butler ? 3 : role.Kind == StaffKind.Cook ? 2 : 1;
                if (r > rank)
                {
                    rank = r;
                    best = m;
                }
            }
            return best;
        }

        /// <summary>What a visit that could not be paid does: true when it was the last straw and they leave.</summary>
        public static bool Unpaid(StaffMember m)
        {
            if (m == null)
            {
                return false;
            }
            m.Missed = Math.Min(QuitAfter, m.Missed + 1);
            m.OnDay = -1;
            return m.Missed >= QuitAfter;
        }

        /// <summary>A visit paid for: the count goes up and the run of unpaid ones is over.</summary>
        public static void Paid(StaffMember m, int day, int amount)
        {
            m.Missed = 0;
            m.Visits++;
            m.PaidTotal = (int)Math.Min(int.MaxValue, (long)m.PaidTotal + Math.Max(0, amount));
            m.Charged = Math.Max(0, amount);
            m.OnDay = day;
        }

        /// <summary>The hour stamps a look has to go through, oldest first: the ones since the last, at most <see cref="CatchUpHours"/>.</summary>
        public static int FirstToRun(int last, int now)
        {
            if (last <= 0 || now <= last)
            {
                return now + 1;   // a first look, or the clock went back (a load): nothing to make up for
            }
            return Math.Max(last + 1, now - CatchUpHours + 1);
        }

        public static int Clamp(int value, int low, int high) => value < low ? low : value > high ? high : value;
    }
}
