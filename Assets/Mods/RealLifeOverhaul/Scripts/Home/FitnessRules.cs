using System;

namespace RealLifeOverhaul
{
    /// <summary>What moved fitness, for the "why" lines of the Health app and the Wellbeing page.</summary>
    public enum FitWhy
    {
        None,
        Runs,        // runs out of doors
        Workouts,    // machines, floor exercises, sport
        Idle,        // days with no exercise at all
        Junk,        // meals bought ready to eat
        Home,        // meals cooked or eaten at home
        Drinks       // nights of drinks
    }

    /// <summary>One line of "what moved it": a kind, how many of it, and the points of fitness it was worth over the days looked at (signed, whole).</summary>
    public struct WhyLine
    {
        public FitWhy Kind;
        public int N;
        public int Amount;
    }

    /// <summary>One day's ledger of what moved fitness. Kept for <see cref="FitnessView.Days"/> days in HomeState; each figure is points of fitness or a count.</summary>
    public struct FitDay
    {
        public float Exercise;      // points from exercise of every kind
        public float RunGain;       // ...of which from runs out of doors
        public float Idle;          // points lost for want of exercise (negative)
        public float DietHome;      // points from meals at home (positive)
        public float DietJunk;      // points lost to meals bought ready to eat (negative)
        public float DietDrink;     // points lost to a night of drinks (negative)
        public int Runs, Sessions;  // runs finished, and workouts, floor exercises and sport begun
        public int HomeMeals, JunkMeals, DrinkNights;

        public bool Empty => Exercise == 0f && Idle == 0f && DietHome == 0f && DietJunk == 0f && DietDrink == 0f && Runs == 0 && Sessions == 0
                             && HomeMeals == 0 && JunkMeals == 0 && DrinkNights == 0;
    }

    /// <summary>
    /// What the pages read of the player's fitness, filled once when a page is built (HomeFitness.Fill): the figure, the fourteen days' start-of-day values, and the lines
    /// that say what moved it this week. Game-free, so the pages, the samples and the checks build from the same data.
    /// </summary>
    public sealed class FitnessView
    {
        public const int Days = 14;
        public const int WhyMost = 6;

        public bool Known;                             // fitness is being kept (home life and its fitness option are on, the save is loaded)
        public int Day;                                // the game's day today, for naming the trend's points
        public int Level;                              // 0 to 100
        public int Band;                               // FitnessRules.Band
        public int TrendCount;                         // how many of Trend hold a day (0 on a save from before the history)
        public readonly int[] Trend = new int[Days];   // [0] the oldest day kept, [TrendCount - 1] the latest; today's live figure is Level
        public int Change;                             // Level against the oldest day kept (0 with no history)
        public int WhyCount;
        public readonly WhyLine[] Why = new WhyLine[WhyMost];
        public bool DietCounts;                        // the diet option is on: meals move fitness
    }

    /// <summary>
    /// The rules of what a player's fitness does and what moves it besides exercise, in plain numbers (the exercise gains are HomeBalance's): what a day's meals and nights of
    /// drinks are worth, what a day with no exercise costs, how the fourteen days read as a trend, which lines say why, when a first notice is due, and what being fit saves
    /// (a share of an illness's chance and of a run's energy). Diet is a small lever on purpose: a day of it is worth less than a short session, a week of it less than an
    /// hour on a machine, so exercise stays the main lever. Game-free, nothing allocated. Checked offline (EconomyCheck --fitness29).
    /// </summary>
    public static class FitnessRules
    {
        // ------------------------------------------------------------------------------------------------- diet

        public const float DietPerHome = 0.20f;        // points a day, for each meal cooked or eaten at home
        public const float DietPerPlain = 0.05f;       // ...for something out of the fridge, uncooked
        public const float DietPerJunk = -0.30f;       // ...for a meal bought ready to eat
        public const float JunkMostOfWeek = 0.6f;      // junk is this share of the last days' meals or more: it counts half as much again
        public const float JunkMostFactor = 1.5f;
        public const float DrinkTipsy = 2f;            // units in a day (a beer is 0.8, a cocktail 1.2): a night of drinks
        public const float DrinkHeavy = 4f;            // units in a day: a heavy night
        public const float DrinkNight = -0.25f;        // points for a night of drinks
        public const float DrinkHeavyNight = -0.5f;    // points for a heavy one
        public const float DietDayLeast = -1.0f;       // a day's diet moves fitness by this at the worst...
        public const float DietDayMost = 0.4f;         // ...and this at the best

        /// <summary>
        /// What one day's food and drink are worth to fitness, in points, split by what they came from: <paramref name="home"/>, <paramref name="plain"/> and
        /// <paramref name="junk"/> are the day's meals, <paramref name="weekMeals"/> and <paramref name="weekJunk"/> the last days' meals in all (the day included) and how many
        /// of them were junk, and <paramref name="drinkUnits"/> what was drunk. The total is held between <see cref="DietDayLeast"/> and <see cref="DietDayMost"/>; the parts are
        /// scaled with it, so they always add to it.
        /// </summary>
        public static float DietDay(int home, int plain, int junk, int weekMeals, int weekJunk, float drinkUnits, out float gained, out float lostJunk, out float lostDrink)
        {
            float junkWorth = DietPerJunk;
            if (weekMeals > 0 && weekJunk >= 0 && (float)weekJunk / weekMeals >= JunkMostOfWeek)
            {
                junkWorth *= JunkMostFactor;
            }
            gained = Math.Max(0, home) * DietPerHome + Math.Max(0, plain) * DietPerPlain;
            lostJunk = Math.Max(0, junk) * junkWorth;
            lostDrink = drinkUnits >= DrinkHeavy ? DrinkHeavyNight : drinkUnits >= DrinkTipsy ? DrinkNight : 0f;
            float sum = gained + lostJunk + lostDrink;
            float held = sum < DietDayLeast ? DietDayLeast : sum > DietDayMost ? DietDayMost : sum;
            if (held != sum && sum != 0f)
            {
                float scale = held / sum;
                gained *= scale;
                lostJunk *= scale;
                lostDrink *= scale;
            }
            return held;
        }

        /// <summary>The day's diet worth alone, without the split.</summary>
        public static float DietDay(int home, int plain, int junk, int weekMeals, int weekJunk, float drinkUnits)
            => DietDay(home, plain, junk, weekMeals, weekJunk, drinkUnits, out _, out _, out _);

        // ------------------------------------------------------------------------------------------------- doing nothing

        public const int IdleGraceDays = 2;            // days after the last exercise before fitness starts to slip: the third day without it is the first that costs

        /// <summary>
        /// How many of the days that just went by cost fitness for want of exercise: the days since the last exercise that are past the grace, never more than
        /// <paramref name="passed"/>. The day after a workout is 1 and the third without exercise is the first that counts.
        /// </summary>
        public static int IdleDays(int day, int lastExerciseDay, int passed)
        {
            int idle = day - lastExerciseDay - IdleGraceDays;
            return idle < 0 ? 0 : idle > passed ? Math.Max(0, passed) : idle;
        }

        /// <summary>
        /// The morning's move of fitness, the one rule HomeHealth.NewDay runs: the days that cost for want of exercise (<see cref="IdleDays"/> at
        /// <see cref="HomeBalance.FitIdlePerDay"/>, halved on easy needs), then, with the diet option on, yesterday's food and drink (<see cref="DietDay"/>). What moved is added to
        /// <paramref name="today"/>'s ledger for the "why" lines, scaled to what the figure really moved when it was already at the top or the bottom. Returns the new fitness.
        /// With the diet off it is exactly the idle days' move and nothing else.
        /// </summary>
        public static float Morning(float fitness, int day, int lastExercise, int passed, float easy, bool dietOn, int home, int plain, int junk, int weekMeals, int weekJunk,
            float drinks, ref FitDay today)
        {
            int idle = IdleDays(day, lastExercise, passed);
            if (idle > 0)
            {
                float after = Held(fitness + HomeBalance.FitIdlePerDay * idle * easy);
                today.Idle += after - fitness;
                fitness = after;
            }
            if (dietOn)
            {
                float delta = DietDay(home, plain, junk, weekMeals, weekJunk, drinks, out float gained, out float lostJunk, out float lostDrink);
                float after = Held(fitness + delta);
                float scale = delta != 0f ? (after - fitness) / delta : 0f;   // a figure already at the top or the bottom moves less than the rule says
                today.DietHome += gained * scale;
                today.DietJunk += lostJunk * scale;
                today.DietDrink += lostDrink * scale;
                today.HomeMeals += Math.Max(0, home) + Math.Max(0, plain);
                today.JunkMeals += Math.Max(0, junk);
                today.DrinkNights += drinks >= DrinkTipsy ? 1 : 0;
                fitness = after;
            }
            return fitness;
        }

        private static float Held(float v) => v < 0f ? 0f : v > 100f ? 100f : v;

        // ------------------------------------------------------------------------------------------------- bands and the trend

        public static readonly int[] BandFrom = { 0, 20, 35, 55, 70, 85 };   // out of shape, below average, average, fit, very fit, athletic

        public const int Bands = 6;

        public static int Band(float fitness)
        {
            int band = 0;
            for (int i = 1; i < Bands; i++)
            {
                if (fitness >= BandFrom[i])
                {
                    band = i;
                }
            }
            return band;
        }

        public static readonly string[] BandKeys =
        {
            "reallife_fitness_band_0", "reallife_fitness_band_1", "reallife_fitness_band_2", "reallife_fitness_band_3", "reallife_fitness_band_4", "reallife_fitness_band_5"
        };

        /// <summary>The English the samples and the checks read a band in.</summary>
        public static readonly string[] BandEnglish = { "out of shape", "below average", "average", "fit", "very fit", "athletic" };

        public const int Rising = 1, Steady = 0, Slipping = -1;
        public const float TrendBand = 1.5f;           // points over the days kept that make a trend

        public static int Direction(float change) => change > TrendBand ? Rising : change < -TrendBand ? Slipping : Steady;

        /// <summary>Rounds a figure for a page the way the needs' bars do (Unity's Mathf.RoundToInt: a half goes to the even number), so the bar and the page never disagree.</summary>
        public static int Whole(float v) => (int)Math.Round(v, MidpointRounding.ToEven);

        // ------------------------------------------------------------------------------------------------- what moved it

        /// <summary>
        /// The lines that say what moved fitness over the last <paramref name="days"/> days of a ledger (index 0 today, 1 yesterday...): at most
        /// <see cref="FitnessView.WhyMost"/>, the biggest movers first. A kind that did nothing is left out. Returns how many were put in.
        /// </summary>
        public static int Why(FitDay[] ledger, int days, WhyLine[] into)
        {
            int nRuns = 0, nWork = 0, nHome = 0, nJunk = 0, nDrinks = 0, nIdle = 0;
            float gainRuns = 0f, gainWork = 0f, loseIdle = 0f, dietHome = 0f, dietJunk = 0f, dietDrink = 0f;
            for (int d = 0; d < days && d < ledger.Length; d++)
            {
                FitDay day = ledger[d];
                nRuns += day.Runs;
                nWork += day.Sessions;
                nHome += day.HomeMeals;
                nJunk += day.JunkMeals;
                nDrinks += day.DrinkNights;
                gainRuns += day.RunGain;
                gainWork += day.Exercise - day.RunGain;
                if (day.Idle < 0f)
                {
                    nIdle++;
                    loseIdle += day.Idle;
                }
                dietHome += day.DietHome;
                dietJunk += day.DietJunk;
                dietDrink += day.DietDrink;
            }
            int count = 0;
            count = Put(into, count, FitWhy.Runs, nRuns, gainRuns);
            count = Put(into, count, FitWhy.Workouts, nWork, gainWork);
            count = Put(into, count, FitWhy.Idle, nIdle, loseIdle);
            count = Put(into, count, FitWhy.Junk, nJunk, dietJunk);
            count = Put(into, count, FitWhy.Home, nHome, dietHome);
            count = Put(into, count, FitWhy.Drinks, nDrinks, dietDrink);
            for (int i = 1; i < count; i++)   // the biggest mover first (an insertion sort over at most six)
            {
                WhyLine line = into[i];
                int j = i - 1;
                while (j >= 0 && Math.Abs(into[j].Amount) < Math.Abs(line.Amount))
                {
                    into[j + 1] = into[j];
                    j--;
                }
                into[j + 1] = line;
            }
            return count;
        }

        // A kind is told when it happened and was worth at least a point (a meal worth a few hundredths is not a line); idle days are told as soon as they cost anything.
        private static int Put(WhyLine[] into, int count, FitWhy kind, int n, float points)
        {
            int amount = Whole(points);
            if (n <= 0 || count >= into.Length || amount == 0)
            {
                return count;
            }
            into[count++] = new WhyLine { Kind = kind, N = n, Amount = amount };
            return count;
        }

        /// <summary>The key of the sentence a line is told in (a count of one has its own wording).</summary>
        public static string WhyKey(WhyLine line)
        {
            bool one = line.N == 1;
            switch (line.Kind)
            {
                case FitWhy.Runs: return one ? "reallife_fitness_why_run_one" : "reallife_fitness_why_runs";
                case FitWhy.Workouts: return one ? "reallife_fitness_why_work_one" : "reallife_fitness_why_work";
                case FitWhy.Idle: return one ? "reallife_fitness_why_idle_one" : "reallife_fitness_why_idle";
                case FitWhy.Junk: return one ? "reallife_fitness_why_junk_one" : "reallife_fitness_why_junk";
                case FitWhy.Home: return one ? "reallife_fitness_why_home_one" : "reallife_fitness_why_home";
                case FitWhy.Drinks: return one ? "reallife_fitness_why_drinks_one" : "reallife_fitness_why_drinks";
                default: return "reallife_fitness_why_none";
            }
        }

        /// <summary>The English the samples and the checks read a line in.</summary>
        public static string WhyEnglish(WhyLine line)
        {
            bool one = line.N == 1;
            switch (line.Kind)
            {
                case FitWhy.Runs: return one ? "A run this week: Fitness {amount}" : "{n} runs this week: Fitness {amount}";
                case FitWhy.Workouts: return one ? "A workout this week: Fitness {amount}" : "{n} workouts this week: Fitness {amount}";
                case FitWhy.Idle: return one ? "A day with no exercise: Fitness {amount}" : "{n} days with no exercise: Fitness {amount}";
                case FitWhy.Junk: return one ? "A fast food meal: Fitness {amount}" : "{n} fast food meals: Fitness {amount}";
                case FitWhy.Home: return one ? "A meal at home: Fitness {amount}" : "{n} meals at home: Fitness {amount}";
                case FitWhy.Drinks: return one ? "A night of drinks: Fitness {amount}" : "{n} nights of drinks: Fitness {amount}";
                default: return "Nothing moved your fitness this week.";
            }
        }

        /// <summary>A signed figure as a page writes it: "+18", "-3" (an ordinary minus, no dash).</summary>
        public static string Signed(int amount) => amount > 0 ? "+" + amount : amount < 0 ? "-" + (-amount) : "0";

        // ------------------------------------------------------------------------------------------------- the first notice

        public const int NoticePoints = 4;             // a move this big over the days kept is the first the player is told of
        public const int NoticeDaysLeast = 3;          // ...and the history holds at least this many days (so the notice always says two days or more)

        /// <summary>True when fitness has moved enough to tell the player once: <paramref name="since"/> is the oldest day kept, with <paramref name="known"/> days behind it.</summary>
        public static bool Noticeable(float now, float since, int known) => known >= NoticeDaysLeast && Math.Abs(now - since) >= NoticePoints;

        // ------------------------------------------------------------------------------------------------- what being fit saves

        public const float RunSaveMost = 0.25f;        // a run's and an exercise's extra energy cost is this much less at fitness 100
        public const int IllnessLow = 20;              // below this a body catches more
        public const int IllnessMid = 50;
        public const int IllnessHigh = 70;

        /// <summary>The share of a run's or an exercise's extra energy cost that is spent at a fitness: 1 at 0, three quarters at 100.</summary>
        public static float EffortShare(float fitness)
        {
            float f = fitness < 0f ? 0f : fitness > 100f ? 100f : fitness;
            return 1f - RunSaveMost * f / 100f;
        }

        /// <summary>
        /// The share, in thousandths, of Home's own chance of a cold, the flu or a bad back from a night in the car that a body at this fitness meets: a tenth more under
        /// <see cref="IllnessLow"/>, a tenth less from <see cref="IllnessMid"/> and a fifth less from <see cref="IllnessHigh"/>. A small lever, as diet is.
        /// </summary>
        public static int IllnessShare(float fitness) => fitness < IllnessLow ? 1100 : fitness >= IllnessHigh ? 800 : fitness >= IllnessMid ? 900 : 1000;
    }
}
