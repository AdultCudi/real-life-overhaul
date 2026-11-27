using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>What the player is ill with, one at a time. Saved by number: never reorder.</summary>
    public enum Illness
    {
        None,
        Cold,
        Flu,
        FoodPoisoning,
        BackPain,
        Burnout
    }

    /// <summary>What a meal was, for the diet picture.</summary>
    public enum Meal
    {
        Skipped,
        Junk,      // bought ready to eat
        Plain,     // something out of the fridge, uncooked
        Home       // cooked at home
    }

    /// <summary>Home life and health for one save, game-free: one line of modData (<see cref="Key"/>); a save without it starts from the defaults.</summary>
    public static class HomeState
    {
        public const string Key = "reallife:home";
        public const int DietDays = 5;
        private const string Version = "h1";

        public static bool Loaded { get; private set; }

        public static float Hygiene;        // 0 unwashed .. 100 fresh
        public static float Stress;         // 0 calm .. 100 at the end of the rope
        public static float Tidy;           // 0 a tip .. 100 spotless (the player's own home)

        public static float Fitness;        // 0 .. 100
        public static float SleepDebt;      // hours owed, 0 .. 24
        public static float Strength;       // what the mod has added to the body's strength so far, -0.5 .. 0.5
        public static float Fat;            // and to its fatness
        public static float ShapeDue;       // strength still to be shown on the body (applied on waking)
        public static float FatDue;
        public static Illness Ill;
        public static float IllSince;       // game minutes
        public static float IllUntil;
        public static bool IllTreated;      // medicine or a doctor: half the symptoms, the course already shortened
        public static float SickNoteUntil;  // game minutes: job penalties are paused until then
        public static int ImmuneUntilDay;   // nothing new is caught before this day

        public static int Day;              // the day the counters below belong to
        public static int Leftovers;        // cooked portions put by
        public static int LeftoverDish;     // which dish they are (Dish.Number); 0: not known
        public static int LeftoverFill;     // what one of them fills when reheated; 0: HomeBalance.LeftoverSaturation
        public static int Takeout;          // portions packed to take, carried with the player until eaten at a seat (Meals)
        public static int TakeoutDish;      // which dish they are (Dish.Number); 0: not known
        public static int TakeoutFill;      // what one of them fills; 0: HomeBalance.LeftoverSaturation
        public static int CoffeesToday;
        public static float StressHighHours; // hours on end with stress at the top
        public static int LastWorkoutDay;
        public static float ExerciseToday;  // minutes of any exercise today
        public static int MealsToday;
        public static float WetMinutes;     // out in the rain today without cover
        public static float WorkHours;      // hours on a job over the last days, decaying
        public static float LastWokeAt;     // game minutes
        public static float LastSleepHours;
        public static int SleepQuality;     // 0 none yet, 1 groggy, 2 fair, 3 well rested
        public static int MorningDay;       // the day the morning summary was last given
        public static float FitnessWeekAgo; // for the trend
        public static int TrendDay;
        public static int FitKnown;         // how many days of FitStart are kept (0 on a save from before the history)
        public static bool FitTold;         // the first notice of fitness moving has been given
        public static float DrinksToday;    // units drunk today (Leisure's bar), for the diet's night of drinks

        /// <summary>What the day that just ended held, read by HomeHealth.NewDay for the diet (not saved: it only means something the morning it is made).</summary>
        public static int EndedHome, EndedPlain, EndedJunk, EndedWeekMeals, EndedWeekJunk;
        public static float EndedDrinks;

        private static readonly int[] Diet = new int[DietDays * 4];   // per day: skipped, junk, plain, home (today first)
        private static readonly float[] FitStart = new float[FitnessView.Days];   // fitness at the start of each day (today first)
        private static readonly FitDay[] Ledger = new FitDay[FitnessView.Days];   // what moved it, per day (today first)

        public static void Reset()
        {
            Loaded = false;
            Defaults();
        }

        private static void Defaults()
        {
            Hygiene = 80f;
            Stress = 20f;
            Tidy = 75f;
            Fitness = 30f;
            SleepDebt = 0f;
            Strength = 0f;
            Fat = 0f;
            ShapeDue = 0f;
            FatDue = 0f;
            Ill = Illness.None;
            IllSince = 0f;
            IllUntil = 0f;
            IllTreated = false;
            SickNoteUntil = 0f;
            ImmuneUntilDay = 0;
            Day = 0;
            Leftovers = 0;
            LeftoverDish = 0;
            LeftoverFill = 0;
            Takeout = 0;
            TakeoutDish = 0;
            TakeoutFill = 0;
            CoffeesToday = 0;
            StressHighHours = 0f;
            LastWorkoutDay = 0;
            ExerciseToday = 0f;
            MealsToday = 0;
            WetMinutes = 0f;
            WorkHours = 0f;
            LastWokeAt = 0f;
            LastSleepHours = 0f;
            SleepQuality = 0;
            MorningDay = 0;
            FitnessWeekAgo = 30f;
            TrendDay = 0;
            FitKnown = 0;
            FitTold = false;
            DrinksToday = 0f;
            EndedHome = EndedPlain = EndedJunk = EndedWeekMeals = EndedWeekJunk = 0;
            EndedDrinks = 0f;
            System.Array.Clear(Diet, 0, Diet.Length);
            System.Array.Clear(FitStart, 0, FitStart.Length);
            System.Array.Clear(Ledger, 0, Ledger.Length);
        }

        public static void Ate(Meal meal)
        {
            Diet[(int)meal]++;
            if (meal != Meal.Skipped)
            {
                MealsToday++;
            }
        }

        public static int Meals(int daysAgo, Meal meal)
        {
            return daysAgo >= 0 && daysAgo < DietDays ? Diet[daysAgo * 4 + (int)meal] : 0;
        }

        /// <summary>A drink at a bar (units, a beer 0.8): counted for today's night of drinks.</summary>
        public static void Drank(float units)
        {
            if (units > 0f && DrinksToday < 40f)
            {
                DrinksToday += units;
            }
        }

        // ------------------------------------------------------------------------------------------------- the fitness ledger

        /// <summary>Today's ledger, to be added to as the day goes (the day's entry moves back at midnight).</summary>
        public static ref FitDay Today => ref Ledger[0];

        /// <summary>The ledger of a day (0 today, 1 yesterday, up to 13).</summary>
        public static FitDay LedgerOf(int daysAgo) => daysAgo >= 0 && daysAgo < Ledger.Length ? Ledger[daysAgo] : default(FitDay);

        public static FitDay[] LedgerDays => Ledger;

        /// <summary>The fitness at the start of a day (0 today, 1 yesterday...), or -1 where the history does not reach.</summary>
        public static float StartOf(int daysAgo) => daysAgo >= 0 && daysAgo < FitKnown && daysAgo < FitStart.Length ? FitStart[daysAgo] : -1f;

        /// <summary>Puts today's start-of-day figure right after the morning's moves (the idle days and the diet) have been made.</summary>
        public static void StartedDay()
        {
            if (FitKnown <= 0)
            {
                FitKnown = 1;
            }
            FitStart[0] = Fitness;
        }

        // Moves the fitness history on by the days that went by: yesterday's entries go back, the days nobody lived through in play are held at the figure now
        // (nothing is known of them), and today starts empty.
        private static void ShiftFitness(int shift)
        {
            for (int i = FitnessView.Days - 1; i >= 0; i--)
            {
                FitStart[i] = i - shift >= 0 ? FitStart[i - shift] : 0f;
                Ledger[i] = i - shift >= 0 ? Ledger[i - shift] : default(FitDay);
            }
            for (int k = 0; k < shift && k < FitnessView.Days; k++)
            {
                FitStart[k] = Fitness;
            }
            FitKnown = FitKnown <= 0 ? 1 : (FitKnown + shift > FitnessView.Days ? FitnessView.Days : FitKnown + shift);
        }

        // What the day that ends held, for the diet's rule: its meals, the last days' meals in all and how many were junk, and what was drunk.
        private static void CaptureEnded()
        {
            EndedHome = Diet[3];
            EndedPlain = Diet[2];
            EndedJunk = Diet[1];
            EndedWeekMeals = 0;
            EndedWeekJunk = 0;
            for (int day = 0; day < DietDays; day++)
            {
                int at = day * 4;
                EndedWeekMeals += Diet[at + 1] + Diet[at + 2] + Diet[at + 3];
                EndedWeekJunk += Diet[at + 1];
            }
            EndedDrinks = DrinksToday;
            DrinksToday = 0f;
        }

        /// <summary>0 (junk and missed meals) .. 100 (cooked at home), 50 when nothing is known.</summary>
        public static float DietScore
        {
            get
            {
                float sum = 0f;
                int meals = 0;
                for (int day = 0; day < DietDays; day++)
                {
                    int at = day * 4;
                    sum += Diet[at + 3] * 2f + Diet[at + 2] * 0.5f - Diet[at + 1] * 1.5f - Diet[at] * 1.5f;
                    meals += Diet[at] + Diet[at + 1] + Diet[at + 2] + Diet[at + 3];
                }
                if (meals == 0)
                {
                    return 50f;
                }
                float score = 50f + 25f * sum / meals;   // all home-cooked: 100; all junk: 12.5
                return score < 0f ? 0f : score > 100f ? 100f : score;
            }
        }

        /// <summary>Moves the counters on to a new day. Returns how many days went by (0: still the same day).</summary>
        public static int NewDay(int day)
        {
            if (Day == 0)
            {
                Day = day;
                TrendDay = day;
                return 0;
            }
            int passed = day - Day;
            if (passed <= 0)
            {
                return 0;
            }
            if (MealsToday < 2)
            {
                Diet[0] += 2 - MealsToday;   // the day that just ended: fewer than two meals is one or two skipped
            }
            CaptureEnded();
            CoffeesToday = 0;
            ShiftFitness(passed > FitnessView.Days ? FitnessView.Days : passed);
            int shift = passed > DietDays ? DietDays : passed;
            for (int i = DietDays - 1; i >= 0; i--)
            {
                for (int k = 0; k < 4; k++)
                {
                    Diet[i * 4 + k] = i - shift >= 0 ? Diet[(i - shift) * 4 + k] : 0;
                }
            }
            Day = day;
            ExerciseToday = 0f;
            MealsToday = 0;
            WetMinutes = 0f;
            if (day - TrendDay >= 7)
            {
                TrendDay = day;
                FitnessWeekAgo = Fitness;
            }
            return passed;
        }

        public static void Load(Dictionary<string, string> data)
        {
            Defaults();
            Loaded = true;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return;
            }
            string[] parts = text.Split(';');
            if (parts.Length < 2 || parts[0] != Version)
            {
                SaveLines.Unreadable(data, Key, "it is not of a version this one knows");   // (kept as it is: Core/SaveLines)
                return;   // a line this build does not know: the defaults, never a failed load
            }
            for (int i = 1; i < parts.Length; i++)
            {
                int equals = parts[i].IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }
                string name = parts[i].Substring(0, equals);
                string value = parts[i].Substring(equals + 1);
                if (name == "fs" || name == "led")
                {
                    ReadFitness(name, value);
                    continue;
                }
                if (name == "diet")
                {
                    string[] meals = value.Split(',');
                    for (int k = 0; k < meals.Length && k < Diet.Length; k++)
                    {
                        if (int.TryParse(meals[k], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) && count >= 0)
                        {
                            Diet[k] = count;
                        }
                    }
                    continue;
                }
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number)
                    || float.IsNaN(number) || float.IsInfinity(number))
                {
                    continue;
                }
                Set(name, number);
            }
            Hygiene = Clamp(Hygiene, 0f, 100f);
            Stress = Clamp(Stress, 0f, 100f);
            Tidy = Clamp(Tidy, 0f, 100f);
            Fitness = Clamp(Fitness, 0f, 100f);
            FitKnown = FitKnown < 0 ? 0 : FitKnown > FitnessView.Days ? FitnessView.Days : FitKnown;
            DrinksToday = Clamp(DrinksToday, 0f, 40f);
            SleepDebt = Clamp(SleepDebt, 0f, 24f);
            Strength = Clamp(Strength, -0.5f, 0.5f);
            Fat = Clamp(Fat, -0.5f, 0.5f);
            if (Leftovers < 0)
            {
                Leftovers = 0;
            }
            if (Takeout < 0 || Takeout > HomeBalance.TakeoutMost)
            {
                Takeout = Takeout < 0 ? 0 : HomeBalance.TakeoutMost;
            }
        }

        public static void Save(Dictionary<string, string> data)
        {
            if (!Loaded || data == null)
            {
                return;
            }
            var text = new StringBuilder(360);
            text.Append(Version);
            Put(text, "hy", Hygiene);
            Put(text, "st", Stress);
            Put(text, "td", Tidy);
            Put(text, "ft", Fitness);
            Put(text, "sd", SleepDebt);
            Put(text, "sg", Strength);
            Put(text, "fa", Fat);
            Put(text, "sgd", ShapeDue);
            Put(text, "fad", FatDue);
            Put(text, "ill", (int)Ill);
            Put(text, "ils", IllSince);
            Put(text, "ilu", IllUntil);
            Put(text, "ilt", IllTreated ? 1 : 0);
            Put(text, "note", SickNoteUntil);
            Put(text, "imm", ImmuneUntilDay);
            Put(text, "day", Day);
            Put(text, "gro", Leftovers);
            Put(text, "grd", LeftoverDish);
            Put(text, "grf", LeftoverFill);
            Put(text, "cf", CoffeesToday);
            Put(text, "shh", StressHighHours);
            Put(text, "lw", LastWorkoutDay);
            Put(text, "wt", ExerciseToday);
            Put(text, "mt", MealsToday);
            Put(text, "wet", WetMinutes);
            Put(text, "wh", WorkHours);
            Put(text, "wk", LastWokeAt);
            Put(text, "sl", LastSleepHours);
            Put(text, "sq", SleepQuality);
            Put(text, "md", MorningDay);
            Put(text, "fw", FitnessWeekAgo);
            Put(text, "tr", TrendDay);
            Put(text, "tko", Takeout);   // appended under h1: a build without takeout reads past names it does not know
            Put(text, "tkd", TakeoutDish);
            Put(text, "tkf", TakeoutFill);
            Put(text, "fk", FitKnown);   // appended under h1 with the fitness history: a build without it reads past the names it does not know
            Put(text, "fnt", FitTold ? 1 : 0);
            Put(text, "dk", DrinksToday);
            WriteFitness(text);
            text.Append(";diet=");
            for (int i = 0; i < Diet.Length; i++)
            {
                if (i > 0)
                {
                    text.Append(',');
                }
                text.Append(Diet[i].ToString(CultureInfo.InvariantCulture));
            }
            data[Key] = text.ToString();
        }

        private static void Set(string name, float v)
        {
            switch (name)
            {
                case "hy": Hygiene = v; break;
                case "st": Stress = v; break;
                case "td": Tidy = v; break;
                case "ft": Fitness = v; break;
                case "sd": SleepDebt = v; break;
                case "sg": Strength = v; break;
                case "fa": Fat = v; break;
                case "sgd": ShapeDue = v; break;
                case "fad": FatDue = v; break;
                case "ill": Ill = v >= 0f && v <= (int)Illness.Burnout ? (Illness)(int)v : Illness.None; break;
                case "ils": IllSince = v; break;
                case "ilu": IllUntil = v; break;
                case "ilt": IllTreated = v > 0.5f; break;
                case "note": SickNoteUntil = v; break;
                case "imm": ImmuneUntilDay = (int)v; break;
                case "day": Day = (int)v; break;
                case "gro": Leftovers = (int)v; break;
                case "grd": LeftoverDish = (int)v; break;
                case "grf": LeftoverFill = (int)v; break;
                case "cf": CoffeesToday = (int)v; break;
                case "shh": StressHighHours = v; break;
                case "lw": LastWorkoutDay = (int)v; break;
                case "wt": ExerciseToday = v; break;
                case "mt": MealsToday = (int)v; break;
                case "wet": WetMinutes = v; break;
                case "wh": WorkHours = v; break;
                case "wk": LastWokeAt = v; break;
                case "sl": LastSleepHours = v; break;
                case "sq": SleepQuality = (int)v; break;
                case "md": MorningDay = (int)v; break;
                case "fw": FitnessWeekAgo = v; break;
                case "tr": TrendDay = (int)v; break;
                case "tko": Takeout = (int)v; break;
                case "tkd": TakeoutDish = (int)v; break;
                case "tkf": TakeoutFill = (int)v; break;
                case "fk": FitKnown = (int)v; break;
                case "fnt": FitTold = v > 0.5f; break;
                case "dk": DrinksToday = v; break;
            }
        }

        private const int LedgerFields = 11;

        private static void WriteFitness(StringBuilder text)
        {
            text.Append(";fs=");
            for (int i = 0; i < FitStart.Length; i++)
            {
                text.Append(i > 0 ? "," : string.Empty).Append(FitStart[i].ToString("0.#", CultureInfo.InvariantCulture));
            }
            text.Append(";led=");
            for (int i = 0; i < Ledger.Length; i++)
            {
                FitDay d = Ledger[i];
                text.Append(i > 0 ? "," : string.Empty)
                    .Append(F(d.Exercise)).Append(',').Append(F(d.RunGain)).Append(',').Append(F(d.Idle)).Append(',').Append(F(d.DietHome)).Append(',').Append(F(d.DietJunk)).Append(',')
                    .Append(F(d.DietDrink)).Append(',').Append(d.Runs).Append(',').Append(d.Sessions).Append(',').Append(d.HomeMeals).Append(',').Append(d.JunkMeals).Append(',')
                    .Append(d.DrinkNights);
            }
        }

        private static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        // The history's two lists: a value that does not read is left at nothing, so a damaged line costs a trend and never a load.
        private static void ReadFitness(string name, string value)
        {
            string[] part = value.Split(',');
            if (name == "fs")
            {
                for (int i = 0; i < part.Length && i < FitStart.Length; i++)
                {
                    FitStart[i] = Number(part[i], 0f, 0f, 100f);
                }
                return;
            }
            for (int day = 0; day < Ledger.Length && (day + 1) * LedgerFields <= part.Length; day++)
            {
                int at = day * LedgerFields;
                Ledger[day] = new FitDay
                {
                    Exercise = Number(part[at], 0f, -200f, 200f), RunGain = Number(part[at + 1], 0f, -200f, 200f), Idle = Number(part[at + 2], 0f, -200f, 0f),
                    DietHome = Number(part[at + 3], 0f, 0f, 10f), DietJunk = Number(part[at + 4], 0f, -10f, 0f), DietDrink = Number(part[at + 5], 0f, -10f, 0f),
                    Runs = (int)Number(part[at + 6], 0f, 0f, 99f), Sessions = (int)Number(part[at + 7], 0f, 0f, 99f), HomeMeals = (int)Number(part[at + 8], 0f, 0f, 99f),
                    JunkMeals = (int)Number(part[at + 9], 0f, 0f, 99f), DrinkNights = (int)Number(part[at + 10], 0f, 0f, 99f)
                };
            }
        }

        private static float Number(string text, float fallback, float low, float high)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) || float.IsNaN(v) || float.IsInfinity(v))
            {
                return fallback;
            }
            return v < low ? low : v > high ? high : v;
        }

        private static void Put(StringBuilder text, string name, float value)
        {
            text.Append(';').Append(name).Append('=').Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        /// <summary>Puts cooked portions by, as far as there is room; returns how many. The leftovers are one lot: the last dish, the average fill.</summary>
        public static int PutBy(int portions, int dish, int fill)
        {
            int had = Leftovers < 0 ? 0 : Leftovers;
            int room = HomeBalance.LeftoversMost - had;
            int kept = portions < room ? portions : room;
            if (kept <= 0)
            {
                return 0;
            }
            int hadFill = LeftoverFill > 0 ? LeftoverFill : HomeBalance.LeftoverSaturation;
            LeftoverFill = (had * hadFill + kept * fill) / (had + kept);
            LeftoverDish = dish;
            Leftovers = had + kept;
            return kept;
        }

        /// <summary>
        /// Packs cooked portions to take, as far as the player can carry; returns how many. Like the leftovers they are one
        /// lot (the last dish, the average fill), so a portion is never two things at once.
        /// </summary>
        public static int Pack(int portions, int dish, int fill)
        {
            int had = Takeout < 0 ? 0 : Takeout;
            int room = HomeBalance.TakeoutMost - had;
            int kept = portions < room ? portions : room;
            if (kept <= 0)
            {
                return 0;
            }
            int hadFill = TakeoutFill > 0 ? TakeoutFill : HomeBalance.LeftoverSaturation;
            int portionFill = fill > 0 ? fill : HomeBalance.LeftoverSaturation;
            TakeoutFill = (had * hadFill + kept * portionFill) / (had + kept);
            TakeoutDish = dish;
            Takeout = had + kept;
            return kept;
        }

        /// <summary>One packed portion taken out to be eaten: its fill, or 0 when none is carried.</summary>
        public static int TakeOne()
        {
            if (Takeout <= 0)
            {
                Takeout = 0;
                return 0;
            }
            int fill = TakeoutFill > 0 ? TakeoutFill : HomeBalance.LeftoverSaturation;
            Takeout--;
            if (Takeout == 0)
            {
                TakeoutDish = 0;
                TakeoutFill = 0;
            }
            return fill;
        }

        /// <summary>
        /// The packed portions put by with the leftovers (the option switched off): as many as the fridge has room for; the
        /// rest stay packed, so nothing is lost. Returns how many went in.
        /// </summary>
        public static int Unpack()
        {
            if (Takeout <= 0)
            {
                return 0;
            }
            int put = PutBy(Takeout, TakeoutDish, TakeoutFill > 0 ? TakeoutFill : HomeBalance.LeftoverSaturation);
            Takeout -= put;
            if (Takeout <= 0)
            {
                Takeout = 0;
                TakeoutDish = 0;
                TakeoutFill = 0;
            }
            return put;
        }

        public static float Clamp(float value, float low, float high)
        {
            return value < low ? low : value > high ? high : value;
        }
    }
}
