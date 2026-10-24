namespace RealLifeOverhaul
{
    /// <summary>How a figure on the shop's figures page stands against the car as standard and as it came in.</summary>
    public enum FigureChange
    {
        Unknown,    // the model cannot tell this car's figure
        Standard,   // as the factory made it, now and after this visit
        Kept,       // changed from standard by work already paid for; this visit leaves it
        Changed     // this visit's work changes it
    }

    /// <summary>
    /// The tuning sums that need no game: how long a gear change takes with a quicker gearbox, the figures page's units
    /// and how each figure stands, and what a build fetches when its car is sold. Game-free (NavCheck's TuningCheck); run
    /// on a click in the shop or at a sale, never per frame.
    /// </summary>
    public static class TuningRules
    {
        public const float MinShiftSeconds = 0.05f;   // s: a change any quicker falls inside two or three of the game's 0.02 s physics steps
        public const float MinShiftFactor = 0.25f;    // however parts stack, a change takes at least a quarter of its standard time
        public const float HpPerKw = 1.341f;          // mechanical horsepower in a kilowatt
        public const float SaleMatch = 1f;            // money: the game's sale and its own price for the car agree within this
        public const string SoldType = "ba:transaction_itemsold";   // the game's own type for a vehicle sold from its item panel

        /// <summary>A gear change's time with the build's factor: never longer than standard, never under the floors. A box the game made instant stays so.</summary>
        public static float ShiftSeconds(float standard, float factor)
        {
            if (!(standard > 0f))
            {
                return standard;
            }
            float f = factor > 1f ? 1f : factor < MinShiftFactor ? MinShiftFactor : factor;
            float seconds = standard * f;
            float floor = standard < MinShiftSeconds ? standard : MinShiftSeconds;
            return seconds < floor ? floor : seconds;
        }

        public static int Horsepower(float kilowatts)
        {
            return kilowatts > 0f ? (int)(kilowatts * HpPerKw + 0.5f) : 0;
        }

        /// <summary>
        /// How a figure stands: <paramref name="close"/> is how near two readings count as one (a tenth of a second, a
        /// kilowatt), so the model's rounding never shows a change nobody made. Any reading below zero: unknown.
        /// </summary>
        public static FigureChange Compare(float standard, float now, float after, float close)
        {
            if (standard < 0f || now < 0f || after < 0f)
            {
                return FigureChange.Unknown;
            }
            return CompareAny(standard, now, after, close);
        }

        /// <summary>As <see cref="Compare"/>, for a figure that may be below zero (a ride height against standard).</summary>
        public static FigureChange CompareAny(float standard, float now, float after, float close)
        {
            if (Near(now, after, close))

            {
                return Near(now, standard, close) ? FigureChange.Standard : FigureChange.Kept;
            }
            return FigureChange.Changed;
        }

        /// <summary>
        /// What the work on a car fetches when it is sold: the build's share of what was spent, in the car's condition as the
        /// game priced it (its sale price against what a perfect one fetches). Nothing for nothing spent or nothing paid.
        /// </summary>
        public static int SaleWorth(int spent, float sold, float perfect)
        {
            if (spent <= 0 || !(sold > 0f))
            {
                return 0;
            }
            float condition = perfect > 0f ? sold / perfect : 1f;
            condition = condition > 1f ? 1f : condition < 0f ? 0f : condition;
            return (int)(spent * GarageCatalogue.Resale * condition + 0.5f);
        }

        /// <summary>
        /// Whether the game's newest transaction is this car's sale: the game books it as <see cref="SoldType"/> with the
        /// car's type name and its price, just before the car goes. Anything else that takes a car away (a tow, a hire, a
        /// repossession) books no such line, and fetches nothing.
        /// </summary>
        public static bool IsSale(string type, string soldInfo, float amount, string carType, float price)
        {
            return type == SoldType && carType != null && soldInfo == carType && amount > 0f && price > 0f
                   && amount - price <= SaleMatch && price - amount <= SaleMatch;
        }

        private static bool Near(float a, float b, float close)
        {
            float d = a - b;
            return d <= close && -d <= close;
        }
    }
}
