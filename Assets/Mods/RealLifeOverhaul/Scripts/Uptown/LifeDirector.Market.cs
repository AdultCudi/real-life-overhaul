using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The districts' markets (plans/build-mode.md section 10; plans/uptown-split.md): once a game day each district's standing
    /// buildings are counted by use (units, vacant units, traffic), each use's demand worked out (DemandRules) and the district's
    /// level moved a step toward the one the demand asks for; the level is written into the district's own record of the game's
    /// (its rents a square metre and its buildings' values), so the game's own rent, value and land price arithmetic follow it.
    /// Uptown and Cedar Heights each have a market of their own: a glut in one is no glut in the other. A packed block lifts the
    /// traffic of its lots as the free land on it is built (DemandRules.DensityLift). Not in a Going Public session (the levels
    /// stay even, so every machine's districts are alike). The levels are kept in the district's line, each from its own day; the
    /// counts are worked out from the buildings at the first pass of a load.
    /// COST: nothing per frame; one comparison a second, a pass over the standing buildings once a game day.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private static readonly int[][] DemandNow = { new[] { -1, -1, -1, -1 }, new[] { -1, -1, -1, -1 } };   // each district's demand by use
        private static readonly Dictionary<int, int> OpenOnBlock = new Dictionary<int, int>(32);
        private static readonly Dictionary<int, int> BuiltOnBlock = new Dictionary<int, int>(32);
        private static readonly Dictionary<int, int> PeopleOnBlock = new Dictionary<int, int>(32);   // homes and offices finished on a block
        private static int _marketDay = int.MinValue;
        private static readonly int[] Level = { 1000, 1000 };   // each district's market level
        private static int _densityStamp = -1;

        /// <summary>A district's market level, thousandths of its rents and values (1000 even).</summary>
        public static int MarketLevelOf(int district)
        {
            return district >= 0 && district < Level.Length ? Level[district] : 1000;
        }

        /// <summary>The market level of the district a lot is in.</summary>
        public static int MarketLevelOfLot(string lotId)
        {
            Lot lot = UptownLots.Get(lotId);
            return MarketLevelOf(lot != null ? lot.District : DistrictSplit.Uptown);
        }

        /// <summary>A use's demand in a district now (0 to 100), or -1 while not counted at this load or in a Going Public session.</summary>
        public static int DemandOf(int use, int district)
        {
            return district >= 0 && district < DemandNow.Length && use >= 0 && use < DemandNow[district].Length && !GoingPublic.InSession ? DemandNow[district][use] : -1;
        }

        // Once a second from the slow pass: once a game day, each market counted and its level moved.
        private static void MarketPass()
        {
            int day = LifeGame.Today;
            if (day == _marketDay || day < 0)
            {
                return;
            }
            _marketDay = day;
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                MarketPassOf(d, day);
            }
        }

        private static void MarketPassOf(int district, int day)
        {
            DistrictMarket m = LifeGame.Market(Standing, _state, day, district);
            int[] now = DemandNow[district];
            for (int use = 0; use < DemandRules.Uses; use++)
            {
                now[use] = DemandRules.Demand(m, use);
            }
            int wanted = GoingPublic.InSession ? 1000 : DemandRules.DistrictLevel(m);
            int from = 1000;
            if (GoingPublic.InSession)
            {
                Level[district] = 1000;   // even in a session: every machine's districts alike, nothing written
            }
            else
            {
                // From the level the save kept (1000 in a save from before it was kept; a save from before the split starts Cedar Heights
                // from the whole band's), a step a day at most from the first day on: a load never jumps the rents to the level the count asks for.
                int kept = _state != null ? _state.LevelOf(district) : 0;
                if (kept <= 0 && district != DistrictSplit.Uptown && _state != null)
                {
                    kept = _state.LevelOf(DistrictSplit.Uptown);
                }
                from = kept > 0 ? kept : 1000;
                bool today = _state != null && _state.DayOf(district) == day && _state.LevelOf(district) > 0;
                Level[district] = today ? from : DemandRules.Step(from, wanted);
                if (_state != null && !_stateUnread && (_state.LevelOf(district) != Level[district] || _state.DayOf(district) != day))
                {
                    _state.SetMarket(district, Level[district], day);
                    WriteState();
                }
            }
            LifeGame.ApplyLevel(district, Level[district]);
            Logger?.Info(DistrictName(district) + ": the market on day " + day + ": demand homes " + now[0] + ", shops " + now[1] + ", offices " + now[2] + ", warehouses " + now[3]
                         + "; vacant " + Shares(m, false) + " against normal " + Shares(m, true) + ", still being let " + m.Filling[0] + ", " + m.Filling[1] + ", " + m.Filling[2] + ", "
                         + m.Filling[3] + " unit(s); rents and values at " + Level[district] + " in 1000 (from " + from + ", wanted " + wanted + ", " + DemandRules.LevelStep + " a day at most).");
        }

        // A district's name for the log lines, in English.
        private static string DistrictName(int district)
        {
            return district == DistrictSplit.CedarHeights ? "Cedar Heights" : "Uptown";
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

        // A new load: the markets are counted afresh.
        private static void MarketForget()
        {
            Drops.Clear();   // a new load makes every record at its own traffic: the construction game's day asks each drop again
            _marketDay = int.MinValue;
            _densityStamp = -1;
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                Level[d] = 1000;
                for (int i = 0; i < DemandNow[d].Length; i++)
                {
                    DemandNow[d][i] = -1;
                }
            }
        }

        // The traffic points a lot gets as the free land of its block is built (none on a block that had none), and from the homes and
        // offices finished on it (DemandRules.PeopleLift): the game's own count of passers-by at every door of the block rises with them.
        private static int Density(BuildingsLot lot)
        {
            if (_densityStamp != _stamp)
            {
                _densityStamp = _stamp;
                OpenOnBlock.Clear();
                BuiltOnBlock.Clear();
                PeopleOnBlock.Clear();
                for (int i = 0; i < Plan.Count; i++)
                {
                    BuildingsLot l = Plan[i];
                    if (!l.Open)
                    {
                        continue;
                    }
                    OpenOnBlock[l.Block] = (OpenOnBlock.TryGetValue(l.Block, out int open) ? open : 0) + 1;
                    FinishedBuilding f = _state != null ? _state.On(l.Id) : null;
                    if (f != null)
                    {
                        BuiltOnBlock[l.Block] = (BuiltOnBlock.TryGetValue(l.Block, out int built) ? built : 0) + 1;
                        if (f.Type == MixRules.HomeType || f.Type == BuildingsRules.OfficeType)
                        {
                            PeopleOnBlock[l.Block] = (PeopleOnBlock.TryGetValue(l.Block, out int people) ? people : 0) + 1;   // new homes and offices bring their people past every door
                        }
                    }
                }
            }
            return DemandRules.DensityLift(BuiltOnBlock.TryGetValue(lot.Block, out int b) ? b : 0, OpenOnBlock.TryGetValue(lot.Block, out int o) ? o : 0)
                   + DemandRules.PeopleLift(PeopleOnBlock.TryGetValue(lot.Block, out int p) ? p : 0);
        }
    }
}
