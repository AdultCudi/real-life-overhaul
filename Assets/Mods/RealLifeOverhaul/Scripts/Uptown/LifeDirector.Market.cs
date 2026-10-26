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
            DistrictMarket m = LifeGame.Market(Standing, _state, day);
            for (int use = 0; use < DemandRules.Uses; use++)
            {
                DemandNow[use] = DemandRules.Demand(m, use);
            }
            int wanted = GoingPublic.InSession ? 1000 : DemandRules.DistrictLevel(m);
            int from = 1000;
            if (GoingPublic.InSession)
            {
                _level = 1000;   // even in a session: every machine's district alike, nothing written
            }
            else
            {
                // From the level the save kept (1000 in a save from before it was kept), a step a day at most from the first day on:
                // a load never jumps the rents to the level the count asks for.
                from = _state != null && _state.MarketLevel > 0 ? _state.MarketLevel : 1000;
                _level = _state != null && _state.MarketDay == day && _state.MarketLevel > 0 ? from : DemandRules.Step(from, wanted);
                if (_state != null && !_stateUnread && (_state.MarketLevel != _level || _state.MarketDay != day))
                {
                    _state.MarketLevel = _level;
                    _state.MarketDay = day;
                    WriteState();
                }
            }
            _marketSet = true;
            LifeGame.ApplyLevel(_level);
            Logger?.Info("Uptown: the market on day " + day + ": demand homes " + DemandNow[0] + ", shops " + DemandNow[1] + ", offices " + DemandNow[2] + ", warehouses " + DemandNow[3]
                         + "; vacant " + Shares(m, false) + " against normal " + Shares(m, true) + ", still being let " + m.Filling[0] + ", " + m.Filling[1] + ", " + m.Filling[2] + ", "
                         + m.Filling[3] + " unit(s); rents and values at " + _level + " in 1000 (from " + from + ", wanted " + wanted + ", " + DemandRules.LevelStep + " a day at most).");
        }

        // Each use's vacancy, or its normal share, as whole per cent.
        private static string Shares(DistrictMarket m, bool normal)
        {
            string s = string.Empty;
            for (int use = 0; use < DemandRules.Uses; use++)
            {
                s += (use > 0 ? ", " : string.Empty) + (normal ? DemandRules.NormalPermille(m, use) : DemandRules.VacancyPermille(m, use)) / 10 + "%";
            }
            return s;
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
