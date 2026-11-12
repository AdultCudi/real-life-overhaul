using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The city's economy as other features read it: game-free and safe at any time; while it is not running, every member answers as a city without one.</summary>
    public static class CityEconomy
    {
        /// <summary>The state in use, or null. Set by the city's economy director only.</summary>
        internal static CityState Live { get; set; }

        /// <summary>The strength in use (the option's step); the catalogue's default while nothing is live.</summary>
        internal static int Level { get; set; } = MacroCatalogue.LevelDefault;

        public static bool Running => Live != null;

        public static CycleState State => Live != null ? Live.Cycle : CycleState.Steady;

        /// <summary>The central bank's rate in points on top of the banks' own, -2 to +6 in quarters.</summary>
        public static float BasePoints => Live != null ? CityModel.BasePoints(Live) : 0f;

        /// <summary>The price index, 100 at the start.</summary>
        public static float PriceIndex => Live != null ? Live.Index / 1000f : MacroCatalogue.IndexStart / 1000f;

        /// <summary>What the game's own costs are adjusted by, in tenths of a point: the price level of every price of the mod's (Core/Prices).</summary>
        public static int CostFactor => Live != null ? CityModel.Costs(Live) : PriceRules.Start;

        /// <summary>Inflation in per cent a year.</summary>
        public static float InflationPercent => Live != null ? Live.Inflation / 100f : MacroCatalogue.InflationTarget / 100f;

        /// <summary>The month of the state the cycle is in, from 1.</summary>
        public static int MonthOfState => Live != null ? Live.MonthsIn : 1;

        /// <summary>A district's income level, 60 to 140, by the game's name for the neighbourhood; 100 for one not known.</summary>
        public static float DistrictIncome(string name)
        {
            CityDistrict d = Live != null && name != null ? Live.District(name) : null;
            return d != null ? d.Income / 10f : MacroCatalogue.IncomeMiddle / 10f;
        }

        /// <summary>What a district's customers bear against the game's own price, in thousandths (<see cref="CityModel.Tolerance"/>); 1000 while not running.</summary>
        public static int Bears(string name)
        {
            CityState s = Live;
            return s != null ? CityModel.Tolerance(s, name != null ? s.District(name) : null, Level) : 1000;
        }

        /// <summary>Today's price factor of a kind of goods, thousandths of its calm price (the season, the weather, a city event, a shortage or a glut); 1000 while not running.</summary>
        public static int SupplyFactor(int kind)
        {
            CityState s = Live;
            return s != null && kind >= 0 && kind < s.Supply.Length && s.Supply[kind].Factor > 0 ? s.Supply[kind].Factor : 1000;
        }

        /// <summary>
        /// The shortage or glut of a kind of goods that is running or announced: the days until it starts (0 while it runs), the days it then still lasts and its size at
        /// the strength in use, thousandths. None, with nothing, while there is none or the city's economy is not running (Suppliers reads it).
        /// </summary>
        public static SupplyShock SupplyShockOf(int kind, out int startsIn, out int days, out int size)
        {
            startsIn = days = size = 0;
            CityState s = Live;
            if (s == null || kind < 0 || kind >= s.Supply.Length || s.Supply[kind].Shock == SupplyShock.None)
            {
                return SupplyShock.None;
            }
            SupplyLine line = s.Supply[kind];
            startsIn = Math.Max(0, line.From - s.Age);
            days = Math.Max(0, line.From + line.Days - Math.Max(s.Age, line.From));
            size = Math.Abs(CityModel.ShockPercent(line, Level)) * 10;
            return days > 0 ? line.Shock : SupplyShock.None;
        }

        /// <summary>A month of the model has turned: raised once, on the game's day event, after its figures are in place.</summary>
        public static event Action MonthTurned;

        // Each handler on its own, so one that throws stops none of the others.
        internal static void RaiseMonthTurned(Action<Exception> fault)
        {
            Action handlers = MonthTurned;
            if (handlers == null)
            {
                return;
            }
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception e)
                {
                    fault?.Invoke(e);
                }
            }
        }
    }
}
