using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>What the player is ill with. One at a time; the order is the order of en.json's names.</summary>
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

    /// <summary>
    /// Home life and health for one savegame: the three light needs (hygiene, stress, how tidy
    /// the flat is), fitness, sleep debt, what was eaten over the last five days, the illness
    /// the player has, and the few dates the routines need. Game-free, and kept as one line of
    /// text in the save's modData (<see cref="Key"/>), so a save made with the mod loads without
    /// it and a save from before it starts from the defaults below.
    /// </summary>
    public static class HomeState
    {
        public const string Key = "reallife:home";
        public const int DietDays = 5;
        private const string Version = "h1";

        public static bool Loaded { get; private set; }

        // ---- needs
        public static float Hygiene;        // 0 unwashed .. 100 fresh
        public static float Stress;         // 0 calm .. 100 at the end of the rope
        public static float Tidy;           // 0 a tip .. 100 spotless (the player's own home)

        // ---- health
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

        // ---- dates and counters
        public static int Day;              // the day the counters below belong to
        public static int Leftovers;        // cooked portions put by
        public static int LeftoverDish;     // which dish they are (Dish.Number); 0: not known
        public static int LeftoverFill;     // what one of them fills when reheated; 0: HomeBalance.LeftoverSaturation
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

        private static readonly int[] Diet = new int[DietDays * 4];   // per day: skipped, junk, plain, home (today first)

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
            System.Array.Clear(Diet, 0, Diet.Length);
        }

        // ---------------------------------------------------------------- diet

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

        /// <summary>
        /// Moves the counters on to a new day: today's meals become yesterday's, the daily counts
        /// start again. Returns how many days went by (0 if it is still the same day).
        /// </summary>
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
            CoffeesToday = 0;
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

        // ---------------------------------------------------------------- the save

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
            SleepDebt = Clamp(SleepDebt, 0f, 24f);
            Strength = Clamp(Strength, -0.5f, 0.5f);
            Fat = Clamp(Fat, -0.5f, 0.5f);
            if (Leftovers < 0)
            {
                Leftovers = 0;
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
            }
        }

        private static void Put(StringBuilder text, string name, float value)
        {
            text.Append(';').Append(name).Append('=').Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Puts cooked portions by, as far as there is room for them. Returns how many were kept. The leftovers are
        /// one lot: they take the name of the dish that came last, and a portion fills the average of what is there.
        /// </summary>
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

        public static float Clamp(float value, float low, float high)
        {
            return value < low ? low : value > high ? high : value;
        }
    }
}
