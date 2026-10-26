using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The district's market (plans/build-mode.md section 10): once a game day its standing buildings are counted by use (units,
    /// vacant units, traffic), each use's demand worked out (DemandRules) and the district's level moved a step toward the one
    /// the demand asks for; the level is written into Uptown's own record of the game's (its rents a square metre and its
    /// buildings' values), so the game's own rent, value and land price arithmetic follow it. A packed block lifts the traffic of
    /// its lots as the free land on it is built (DemandRules.DensityLift). Not in a Going Public session (the level stays even,
    /// so every machine's district is alike). Nothing saved: the level is worked out from the buildings at the first pass of a load.
    /// COST: nothing per frame; one comparison a second, a pass over the standing buildings once a game day.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private static readonly int[] DemandNow = { -1, -1, -1, -1 };
        private static readonly Dictionary<int, int> OpenOnBlock = new Dictionary<int, int>(32);
        private static readonly Dictionary<int, int> BuiltOnBlock = new Dictionary<int, int>(32);
        private static int _marketDay = int.MinValue;
        private static int _level = 1000;
        private static int _densityStamp = -1;
        private static bool _marketSet;

        /// <summary>The district's market level, thousandths of its rents and values (1000 even).</summary>
        public static int MarketLevel => _level;

        /// <summary>A use's demand now (0 to 100), or -1 while not counted at this load or in a Going Public session.</summary>
        public static int DemandOf(int use)
        {
            return use >= 0 && use < DemandNow.Length && !GoingPublic.InSession ? DemandNow[use] : -1;
        }

        // Once a second from the slow pass: once a game day, the market counted and the level moved.
        private static void MarketPass()
        {
            int day = LifeGame.Today;
            if (day == _marketDay || day < 0)
            {
                return;
            }
            _marketDay = day;
            DistrictMarket m = LifeGame.Market(Standing);
            for (int use = 0; use < DemandRules.Uses; use++)
            {
                DemandNow[use] = DemandRules.Demand(m, use);
            }
            int wanted = GoingPublic.InSession ? 1000 : DemandRules.DistrictLevel(m);
            _level = _marketSet ? DemandRules.Step(_level, wanted) : wanted;   // the first pass of a load: the buildings' own level, no step
            _marketSet = true;
            LifeGame.ApplyLevel(_level);
            Logger?.Info("Uptown: the market on day " + day + ": demand homes " + DemandNow[0] + ", shops " + DemandNow[1] + ", offices " + DemandNow[2] + ", warehouses " + DemandNow[3]
                         + "; vacant " + DemandRules.VacancyPermille(m, 0) / 10 + "%, " + DemandRules.VacancyPermille(m, 1) / 10 + "%, " + DemandRules.VacancyPermille(m, 2) / 10 + "%, "
                         + DemandRules.VacancyPermille(m, 3) / 10 + "%; rents and values at " + _level + " in 1000 (wanted " + wanted + ").");
        }

        // A new load: the market is counted afresh.
        private static void MarketForget()
        {
            _marketDay = int.MinValue;
            _marketSet = false;
            _level = 1000;
            _densityStamp = -1;
            for (int i = 0; i < DemandNow.Length; i++)
            {
                DemandNow[i] = -1;
            }
        }

        // The traffic points a lot gets as the free land of its block is built: none on a block that had none.
        private static int Density(BuildingsLot lot)
        {
            if (_densityStamp != _stamp)
            {
                _densityStamp = _stamp;
                OpenOnBlock.Clear();
                BuiltOnBlock.Clear();
                for (int i = 0; i < Plan.Count; i++)
                {
                    BuildingsLot l = Plan[i];
                    if (!l.Open)
                    {
                        continue;
                    }
                    OpenOnBlock[l.Block] = (OpenOnBlock.TryGetValue(l.Block, out int open) ? open : 0) + 1;
                    if (_state != null && _state.On(l.Id) != null)
                    {
                        BuiltOnBlock[l.Block] = (BuiltOnBlock.TryGetValue(l.Block, out int built) ? built : 0) + 1;
                    }
                }
            }
            return DemandRules.DensityLift(BuiltOnBlock.TryGetValue(lot.Block, out int b) ? b : 0, OpenOnBlock.TryGetValue(lot.Block, out int o) ? o : 0);
        }
    }
}
