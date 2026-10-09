namespace RealLifeOverhaul
{
    /// <summary>What the player is doing, as far as home life and health care: read off the game's own activities once a second.</summary>
    public enum Doing
    {
        Nothing,
        Sleeping,
        Showering,     // shower or bath
        Washing,       // at a sink
        Toilet,
        Screen,        // television, a computer game
        Reading,
        Resting,       // on a seat or a bench
        Workout,       // one of the game's workout machines
        Sport,         // swimming, tennis, golf
        Working,       // the game's employer jobs
        Studying,
        HomeRoutine,   // one of the mod's own (cooking, tidying, coffee)
        Exercise,      // the mod's own floor exercises
        Jogging
    }

    /// <summary>One thing that can be cooked: what it takes and what it gives. The order is the order on the panel's slider.</summary>
    public sealed class Recipe
    {
        public string Name;          // locale key
        public int Portions;         // units of food taken from the fridge (0: yesterday's leftovers)
        public int Minutes;
        public float Yield;          // the ingredients' saturation times this
        public float Burn;           // chance of burning it, 0..1
        public int Leftovers;        // portions put by for another day
        public float Calm;           // stress taken off
        public float Mess;           // tidiness it costs the flat
    }

    /// <summary>
    /// Every number of home life and health in one place. Rates are per game hour unless they
    /// say otherwise; needs run 0..100. Game-free.
    /// </summary>
    public static class HomeBalance
    {
        // ---- hygiene (100 fresh)
        public const float HygieneAwake = -3.3f;        // a day and a quarter from fresh to unwashed
        public const float HygieneAsleep = -1.2f;
        public const float HygieneWorkout = -30f;
        public const float HygieneJog = -28f;
        public const float HygieneRain = -4f;           // on top, out in the rain without cover
        public const float HygieneShower = 600f;        // ten minutes from nothing to fresh
        public const float HygieneSink = 150f;          // a wash at the sink, and only as far as SinkMost
        public const float SinkMost = 70f;
        public const float HygieneLow = 25f;            // "Unwashed" below this
        public const float HygieneNotice = 12f;         // other people notice below this

        // ---- stress (0 calm)
        public const float StressAwake = 0.5f;
        public const float StressJobShift = 3.0f;       // on one of the mod's jobs
        public const float StressWorking = 2.5f;        // the game's employer jobs
        public const float StressStudying = 1.5f;
        public const float StressSick = 0.6f;
        public const float StressHungry = 1.0f;         // hunger under 15
        public const float StressMess = 0.8f;           // at home in a flat under MessLow
        public const float StressTired = 1.0f;          // sleep debt over DebtHigh
        public const float StressSleep = -5f;
        public const float StressScreen = -14f;
        public const float StressReading = -12f;
        public const float StressResting = -6f;
        public const float StressShower = -10f;
        public const float StressWorkout = -8f;
        public const float StressSport = -10f;
        public const float StressJog = -14f;
        public const float StressRoutine = -3f;
        public const float StressExercise = -10f;
        public const float StressHigh = 75f;            // "Stressed" above this
        public const float StressCalm = 15f;            // "Relaxed" below this
        public const float BurnoutAt = 90f;             // this high...
        public const float BurnoutHours = 30f;          // ...for this long is a burnout

        // ---- the flat (100 spotless)
        public const float TidyAtHome = -1.6f;          // per waking hour spent in it
        public const float TidyAway = -0.15f;
        public const float TidyPerMinute = 1.1f;        // tidying up: an hour and a half from a tip to spotless
        public const float MessLow = 25f;
        public const float TidyCosy = 80f;
        public const float DirtMost = 60f;              // the most the mod ever puts on one of the game's floor cells

        // ---- sleep
        public const float SleepNeed = 7.5f;            // hours a day
        public const float DebtPerWakingHour = SleepNeed / (24f - SleepNeed);
        public const float DebtHigh = 8f;               // "Short of sleep" above this
        public const float DebtMost = 24f;
        public const int LateFrom = 1;                  // going to bed between these hours is "too late"
        public const int LateTo = 6;

        // ---- fitness (0..100)
        public const float FitWorkout = 9f;             // per hour on a machine, before the day's diminishing returns
        public const float FitSport = 6f;
        public const float FitExercise = 6f;
        public const float FitJog = 11f;
        public const float FitIdlePerDay = -0.7f;       // from the third day without any exercise
        public const float FitHigh = 70f;               // "In good shape"
        public const float FitLow = 20f;
        public const float FitDrainBest = -0.010f;      // energy per game minute at fitness 100 (the game's own "living" drain is 0.04)
        public const float FitDrainWorst = 0.006f;      // at fitness 0
        public const float StrengthPerHour = 0.012f;    // of weights or floor exercises, shown on the body on waking
        public const float FatPerActiveHour = -0.008f;
        public const float FatPerLazyDay = 0.004f;      // a day of bad food and no exercise
        public const float FatPerGoodDay = -0.003f;     // a day of good food
        public const float ShapePerNight = 0.02f;       // the most the body changes in one night
        public const float ShapeMost = 0.35f;           // and in all, either way, from the body the player made

        // ---- diet
        public const float DietGood = 72f;
        public const float DietPoor = 32f;
        public const float MealAtLeast = 8f;            // a rise in the game's hunger bar of this much is a meal

        // ---- energy
        public const float JogDrain = 0.10f;            // per game minute, on top of walking
        public const float ExerciseDrain = 0.12f;
        public const float RoutineDrain = 0.02f;
        public const float CoffeeEnergy = 8f;
        public const int CoffeesADay = 3;
        public const float EnergyFloor = 12f;           // nothing here takes energy below this

        // ---- money
        public const int ConsultationFee = 80;
        public const int PrescriptionFee = 45;
        public const int SickNoteFee = 20;
        public const int SickNoteHours = 48;

        // ---- illness: hours
        public static int Hours(Illness ill, float roll)
        {
            switch (ill)
            {
                case Illness.Cold: return 36 + (int)(roll * 24f);
                case Illness.Flu: return 72 + (int)(roll * 36f);
                case Illness.FoodPoisoning: return 16 + (int)(roll * 14f);
                case Illness.BackPain: return 40 + (int)(roll * 24f);
                case Illness.Burnout: return 72 + (int)(roll * 48f);
                default: return 0;
            }
        }

        /// <summary>Extra energy lost per game minute while it lasts (colds and flu also get Life's own 0.03).</summary>
        public static float Drain(Illness ill)
        {
            switch (ill)
            {
                case Illness.Flu: return 0.03f;
                case Illness.FoodPoisoning: return 0.03f;
                case Illness.Burnout: return 0.025f;
                default: return 0f;
            }
        }

        /// <summary>True for what Life's "sick" covers (its mood modifier, its drain, the doctor on the phone).</summary>
        public static bool Infectious(Illness ill)
        {
            return ill == Illness.Cold || ill == Illness.Flu || ill == Illness.FoodPoisoning;
        }

        public const float ColdFromRain = 0.30f;        // once, after WetLimit minutes in the rain in one day
        public const float WetLimit = 60f;
        public const float ColdWhenRunDown = 0.06f;     // a day, with sleep debt over DebtHigh
        public const float ColdWhenUnwashed = 0.05f;    // a day, with hygiene under HygieneNotice
        public const float FluADay = 0.015f;            // doubled by a poor diet, and again by sleep debt
        public const float PoisonFromBurnt = 0.12f;
        public const float PoisonFromFilth = 0.08f;     // cooking in a flat under MessLow
        public const float BackFromStrain = 0.15f;      // more than an hour's exercise in a day at fitness under FitLow
        public const float BackFromCar = 0.12f;         // a night in the car
        public const int ImmuneDays = 3;                // nothing new for this long after anything
        public const int RestCure = 6;                  // hours a good night's sleep takes off an illness

        public static readonly Recipe[] Recipes =
        {
            new Recipe { Name = "reallife_home_recipe_leftovers", Portions = 0, Minutes = 8, Yield = 0f, Burn = 0f, Leftovers = -1, Calm = 0f, Mess = 0f },
            new Recipe { Name = "reallife_home_recipe_quick", Portions = 1, Minutes = 20, Yield = 1.4f, Burn = 0.05f, Leftovers = 0, Calm = 2f, Mess = 2f },
            new Recipe { Name = "reallife_home_recipe_dinner", Portions = 2, Minutes = 45, Yield = 1.7f, Burn = 0.09f, Leftovers = 0, Calm = 6f, Mess = 4f },
            new Recipe { Name = "reallife_home_recipe_stew", Portions = 3, Minutes = 80, Yield = 1.5f, Burn = 0.12f, Leftovers = 2, Calm = 8f, Mess = 5f }
        };

        public const int LeftoverSaturation = 35;
        public const int LeftoversMost = 4;
        public const float BurntYield = 0.6f;

        /// <summary>How much of a need or a risk is left on "easy needs".</summary>
        public const float Easy = 0.5f;
    }
}
