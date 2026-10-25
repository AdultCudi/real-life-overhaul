namespace RealLifeOverhaul
{
    /// <summary>What the player is doing, read off the game's own activities once a second.</summary>
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

    /// <summary>What one of the game's items is for cooking; None: not cooked with.</summary>
    public enum FoodKind
    {
        None,
        Meat,
        Vegetable,
        Dairy,
        Grain,
        Fruit,
        Fresh,         // the game's "Fresh Food": a bag of groceries, a meal's worth
        Frozen,        // its "Frozen Food"
        Ready,         // bought ready to eat: a burger, a pizza
        Snack,         // sweets and pastries: eaten as they are
        Drink          // drunk as they are
    }

    /// <summary>One thing that can be made at home, in the cooking list's order. The save keeps <see cref="Number"/> for leftovers: never change one.</summary>
    public sealed class Dish
    {
        public int Number;
        public string Name;          // locale key
        public FoodKind[] Needs;     // one entry per unit of food for one batch
        public bool Cooker;          // made at a stove, a grill or a pizza oven; false: put together at the fridge
        public int Portions;         // from one batch: one is eaten, the rest are leftovers
        public int Minutes;          // for one batch
        public float Yield;          // the ingredients' saturation times this, shared between the portions
        public float Burn;           // chance of burning it, 0..1
        public float Calm;           // stress taken off
        public float Mess;           // tidiness it costs the flat
        public int Tile;             // the colour of its tile on the panel, 0xRRGGBB
    }

    /// <summary>Every number of home life and health. Rates are per game hour unless they say otherwise; needs run 0..100. Game-free.</summary>
    public static class HomeBalance
    {
        // Hygiene: 100 is fresh.
        public const float HygieneAwake = -3.3f;        // a day and a quarter from fresh to unwashed
        public const float HygieneAsleep = -1.2f;
        public const float HygieneWorkout = -30f;
        public const float HygieneJog = -28f;
        public const float HygieneRain = -4f;           // on top, out in the rain without cover
        public const float HygieneShower = 600f;        // ten minutes from nothing to fresh
        public const float HygieneSink = 150f;          // a wash at the sink, and only as far as SinkMost
        public const float SinkMost = 70f;              // the sink washes up to this and no further
        public const float HygieneLow = 25f;            // "Unwashed" below this
        public const float HygieneNotice = 12f;         // other people notice below this

        // Stress: 0 is calm.
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

        // The flat: 100 is spotless.
        public const float TidyAtHome = -1.6f;          // per waking hour spent in it
        public const float TidyAway = -0.15f;
        public const float TidyPerMinute = 1.1f;        // tidying up: an hour and a half from a tip to spotless
        public const float MessLow = 25f;               // "Messy home" below this
        public const float TidyCosy = 80f;              // "Cosy home" from this
        public const float DirtMost = 60f;              // the most the mod ever puts on one of the game's floor cells

        public const float SleepNeed = 7.5f;            // hours a day
        public const float DebtPerWakingHour = SleepNeed / (24f - SleepNeed);
        public const float DebtHigh = 8f;               // "Short of sleep" above this
        public const float DebtMost = 24f;              // hours
        public const int LateFrom = 1;                  // going to bed between these hours is "too late"
        public const int LateTo = 6;

        // Fitness, 0..100.
        public const float FitWorkout = 9f;             // per hour on a machine, before the day's diminishing returns
        public const float FitSport = 6f;
        public const float FitExercise = 6f;
        public const float FitJog = 11f;
        public const float FitIdlePerDay = -0.7f;       // from the third day without any exercise
        public const float FitHigh = 70f;               // "In good shape"
        public const float FitLow = 20f;                // exercise risks the back below this
        public const float FitDrainBest = -0.010f;      // energy per game minute at fitness 100 (the game's own "living" drain is 0.04)
        public const float FitDrainWorst = 0.006f;      // at fitness 0
        public const float StrengthPerHour = 0.012f;    // of weights or floor exercises, shown on the body on waking
        public const float FatPerActiveHour = -0.008f;
        public const float FatPerLazyDay = 0.004f;      // a day of bad food and no exercise
        public const float FatPerGoodDay = -0.003f;     // a day of good food
        public const float ShapePerNight = 0.02f;       // the most the body changes in one night
        public const float ShapeMost = 0.35f;           // and in all, either way, from the body the player made

        // Diet: DietScore, 0..100.
        public const float DietGood = 72f;
        public const float DietPoor = 32f;
        public const float MealAtLeast = 8f;            // a rise in the game's hunger bar of this much is a meal

        public const float JogDrain = 0.10f;            // per game minute, on top of walking
        public const float ExerciseDrain = 0.12f;       // per game minute
        public const float RoutineDrain = 0.02f;        // per game minute
        public const float CoffeeEnergy = 8f;           // energy a cup
        public const int CoffeesADay = 3;
        public const float EnergyFloor = 12f;           // nothing here takes energy below this

        public static int ConsultationFee => Prices.At(80);
        public static int PrescriptionFee => Prices.At(45);
        public static int SickNoteFee => Prices.At(20);
        public const int SickNoteHours = 48;

        // Illness: how long, from a roll of 0..1.
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
        public const float WetLimit = 60f;              // game minutes
        public const float ColdWhenRunDown = 0.06f;     // a day, with sleep debt over DebtHigh
        public const float ColdWhenUnwashed = 0.05f;    // a day, with hygiene under HygieneNotice
        public const float FluADay = 0.015f;            // doubled by a poor diet, and again by sleep debt
        public const float PoisonFromBurnt = 0.12f;
        public const float PoisonFromFilth = 0.08f;     // cooking in a flat under MessLow
        public const float BackFromStrain = 0.15f;      // more than an hour's exercise in a day at fitness under FitLow
        public const float BackFromCar = 0.12f;         // a night in the car
        public const int ImmuneDays = 3;                // nothing new for this long after anything
        public const int RestCure = 6;                  // hours a good night's sleep takes off an illness

        /// <summary>The game's food items by kind. Anything else is not cooked with: the game gives every item a saturation, batteries included.</summary>
        public static readonly System.Collections.Generic.Dictionary<string, FoodKind> Foods =
            new System.Collections.Generic.Dictionary<string, FoodKind>(System.StringComparer.Ordinal)
        {
            { "ba:itemname_groundbeef", FoodKind.Meat }, { "ba:itemname_chickenbreast", FoodKind.Meat }, { "ba:itemname_rawsausage", FoodKind.Meat },
            { "ba:itemname_carrot", FoodKind.Vegetable }, { "ba:itemname_lettuce", FoodKind.Vegetable }, { "ba:itemname_tomato", FoodKind.Vegetable },
            { "ba:itemname_onion", FoodKind.Vegetable }, { "ba:itemname_slicedonions", FoodKind.Vegetable }, { "ba:itemname_russetpotatoes", FoodKind.Vegetable },
            { "ba:itemname_milk", FoodKind.Dairy }, { "ba:itemname_butter", FoodKind.Dairy }, { "ba:itemname_cheese", FoodKind.Dairy },
            { "ba:itemname_dough", FoodKind.Grain }, { "ba:itemname_bakingmix", FoodKind.Grain }, { "ba:itemname_barley", FoodKind.Grain },
            { "ba:itemname_apple", FoodKind.Fruit }, { "ba:itemname_banana", FoodKind.Fruit }, { "ba:itemname_pear", FoodKind.Fruit },
            { "ba:itemname_grapes", FoodKind.Fruit },
            { "ba:itemname_freshfood", FoodKind.Fresh },
            { "ba:itemname_frozenfood", FoodKind.Frozen },
            { "ba:itemname_burger", FoodKind.Ready }, { "ba:itemname_hotdog", FoodKind.Ready }, { "ba:itemname_kabob", FoodKind.Ready },
            { "ba:itemname_pizza", FoodKind.Ready }, { "ba:itemname_frenchfries", FoodKind.Ready }, { "ba:itemname_salad", FoodKind.Ready },
            { "ba:itemname_cheeseplatter", FoodKind.Ready },
            { "ba:itemname_croissant", FoodKind.Snack }, { "ba:itemname_cupcake", FoodKind.Snack }, { "ba:itemname_donut", FoodKind.Snack },
            { "ba:itemname_cottoncandy", FoodKind.Snack }, { "ba:itemname_popcorn", FoodKind.Snack }, { "ba:itemname_icecream", FoodKind.Snack },
            { "ba:itemname_beer", FoodKind.Drink }, { "ba:itemname_bottleofwine", FoodKind.Drink }, { "ba:itemname_margarita", FoodKind.Drink },
            { "ba:itemname_martini", FoodKind.Drink }, { "ba:itemname_whisky", FoodKind.Drink }, { "ba:itemname_cupofcoffee", FoodKind.Drink },
            { "ba:itemname_cupoftea", FoodKind.Drink }, { "ba:itemname_energydrink", FoodKind.Drink }, { "ba:itemname_slushi", FoodKind.Drink },
            { "ba:itemname_sodacan", FoodKind.Drink }
        };

        public static readonly Dish[] Dishes =
        {
            new Dish { Number = 1, Name = "reallife_home_dish_fruit_bowl", Needs = new[] { FoodKind.Fruit, FoodKind.Fruit },
                Cooker = false, Portions = 1, Minutes = 5, Yield = 1.2f, Burn = 0f, Calm = 1f, Mess = 1f, Tile = 0xD9A441 },
            new Dish { Number = 2, Name = "reallife_home_dish_salad", Needs = new[] { FoodKind.Vegetable, FoodKind.Vegetable },
                Cooker = false, Portions = 1, Minutes = 10, Yield = 1.4f, Burn = 0f, Calm = 1f, Mess = 1f, Tile = 0x5E9E4A },
            new Dish { Number = 3, Name = "reallife_home_dish_cheese_fruit", Needs = new[] { FoodKind.Dairy, FoodKind.Fruit },
                Cooker = false, Portions = 1, Minutes = 5, Yield = 1.3f, Burn = 0f, Calm = 1f, Mess = 1f, Tile = 0xC9B458 },
            new Dish { Number = 4, Name = "reallife_home_dish_heated", Needs = new[] { FoodKind.Ready },
                Cooker = true, Portions = 1, Minutes = 10, Yield = 1.25f, Burn = 0.03f, Calm = 0f, Mess = 1f, Tile = 0xB8693A },
            new Dish { Number = 5, Name = "reallife_home_dish_frozen", Needs = new[] { FoodKind.Frozen },
                Cooker = true, Portions = 1, Minutes = 20, Yield = 2.6f, Burn = 0.05f, Calm = 0f, Mess = 1f, Tile = 0x5B8FB9 },
            new Dish { Number = 6, Name = "reallife_home_dish_fryup", Needs = new[] { FoodKind.Meat, FoodKind.Vegetable },
                Cooker = true, Portions = 1, Minutes = 20, Yield = 1.5f, Burn = 0.06f, Calm = 2f, Mess = 2f, Tile = 0xA8552F },
            new Dish { Number = 7, Name = "reallife_home_dish_pancakes", Needs = new[] { FoodKind.Grain, FoodKind.Dairy },
                Cooker = true, Portions = 1, Minutes = 15, Yield = 1.4f, Burn = 0.07f, Calm = 2f, Mess = 2f, Tile = 0xD8B36A },
            new Dish { Number = 8, Name = "reallife_home_dish_dinner", Needs = new[] { FoodKind.Fresh },
                Cooker = true, Portions = 2, Minutes = 45, Yield = 1.5f, Burn = 0.08f, Calm = 6f, Mess = 4f, Tile = 0x8C6BB1 },
            new Dish { Number = 9, Name = "reallife_home_dish_soup", Needs = new[] { FoodKind.Vegetable, FoodKind.Vegetable, FoodKind.Vegetable },
                Cooker = true, Portions = 2, Minutes = 35, Yield = 1.7f, Burn = 0.05f, Calm = 4f, Mess = 3f, Tile = 0xC8742C },
            new Dish { Number = 10, Name = "reallife_home_dish_pizza", Needs = new[] { FoodKind.Grain, FoodKind.Dairy, FoodKind.Vegetable },
                Cooker = true, Portions = 2, Minutes = 40, Yield = 1.8f, Burn = 0.09f, Calm = 5f, Mess = 4f, Tile = 0xC0453A },
            new Dish { Number = 11, Name = "reallife_home_dish_roast", Needs = new[] { FoodKind.Fresh, FoodKind.Meat, FoodKind.Vegetable },
                Cooker = true, Portions = 3, Minutes = 70, Yield = 1.5f, Burn = 0.10f, Calm = 8f, Mess = 5f, Tile = 0x7A4B2A },
            new Dish { Number = 12, Name = "reallife_home_dish_stew", Needs = new[] { FoodKind.Meat, FoodKind.Meat, FoodKind.Vegetable, FoodKind.Vegetable, FoodKind.Vegetable },
                Cooker = true, Portions = 3, Minutes = 80, Yield = 1.6f, Burn = 0.12f, Calm = 8f, Mess = 5f, Tile = 0x6B3F2A }
        };

        /// <summary>Batches at once: the list's normal, double and family size.</summary>
        public static readonly int[] Batches = { 1, 2, 4 };
        public static readonly float[] BatchMinutes = { 1f, 1.25f, 1.6f };
        public static readonly float[] BatchBurn = { 0f, 0.01f, 0.03f };
        public static readonly float[] BatchMess = { 1f, 1.2f, 1.5f };

        public const float BurnWhenWorn = 0.08f;        // added to a dish's chance when stressed (over StressHigh) or exhausted
        public const float WornEnergy = 20f;            // "exhausted": energy under this
        public const int ReheatMinutes = 8;
        public const int LeftoverSaturation = 35;       // what a portion put by fills when its own fill is not known
        public const int LeftoversMost = 8;             // portions
        public const int TakeoutMost = 2;               // portions packed to take: what one bag holds, carried until eaten at a seat
        public const float BurntYield = 0.6f;           // of the fill, for a burnt dish

        /// <summary>What is left of a need or a risk with "easy needs" on.</summary>
        public const float Easy = 0.5f;
    }
}
