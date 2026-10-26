using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.NewBuildings;
using UI.Notification;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// "Uptown: the districts' census" (Options, Test tools; plans/uptown-split.md): each of the band's two districts counted from
    /// its standing buildings, area, buildings by use, shops by the customers they have room for, the customer volume (those
    /// customers times the district's weight), its people's classes, against the most of the game's old districts counted the same
    /// way from the game's own buildings now; PASS when neither district's volume nor its large stores (room for 75) are over it.
    /// One "Uptown check (districts' census)" line. COST: nothing per frame; a pass over the city's buildings when pressed.
    /// </summary>
    public sealed partial class LifeDirector
    {
        /// <summary>The census of the two districts against the old ones: the line, a notice of how each stands, PASS when neither is over the old districts' most.</summary>
        public static void TestSplit()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                bool pass = SplitCensus(out string line, out string[] brief);
                if (pass)
                {
                    Logger?.Info(line);
                }
                else
                {
                    Logger?.Warn(line);
                }
                Notifications.Show(pass ? NotificationType.Info : NotificationType.Warning, LifeText.SplitDone, new Dictionary<string, string>
                {
                    { "uptown", brief[0] }, { "cedar", brief[1] }, { "volume", brief[2] }, { "large", brief[3] }
                }, 16f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The census itself: the rows of the two districts from the standing buildings and the old districts' rows from the game's, the verdicts.
        private static bool SplitCensus(out string line, out string[] brief)
        {
            var c = CultureInfo.InvariantCulture;
            var rows = new CensusRow[DistrictSplit.Count];
            var units = new List<CensusUnit>(Standing.Count);
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                units.Clear();
                for (int i = 0; i < Standing.Count; i++)
                {
                    MadeBuilding made = Standing[i];
                    if (made != null && made.Building != null && DistrictSplit.Index(made.Spec.District) == d)
                    {
                        units.Add(new CensusUnit { Type = made.Building.BuildingType, Capacity = LifeGame.CapacityOf(made.Building) });
                    }
                }
                DistrictSplit.Bounds(d, UptownLots.Ground, out float x1, out float z1, out float x2, out float z2);
                rows[d] = DistrictCensus.Count(DistrictName(d), (x2 - x1) * (double)(z2 - z1), LifeGame.Figures(d), units);
            }
            var old = new List<CensusRow>(8);
            bool live = LifeGame.OldCensus(old);
            double volumeMost = 0.0, upperMost = 0.0;
            int largeMost = 0, shopsMost = 0;
            string volumeBy = null, largeBy = null;
            for (int i = 0; i < old.Count; i++)
            {
                if (old[i].Volume > volumeMost)
                {
                    volumeMost = old[i].Volume;
                    volumeBy = old[i].Name;
                }
                if (old[i].Large > largeMost)
                {
                    largeMost = old[i].Large;
                    largeBy = old[i].Name;
                }
                shopsMost = Math.Max(shopsMost, old[i].Shops);
                upperMost = Math.Max(upperMost, old[i].UpperHomes);
            }
            bool pass = old.Count > 0;
            var text = new System.Text.StringBuilder(1024);
            text.Append("Uptown check (districts' census): ");
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                string why = DistrictCensus.Verdict(rows[d], volumeMost, largeMost);
                pass &= why == null && rows[d].Buildings > 0;
                text.Append(rows[d].Describe()).Append(": ").Append(why == null ? (rows[d].Buildings > 0 ? "PASS" : "FAIL (no building of it stands)") : "FAIL (" + why + ")").Append("; ");
            }
            text.Append("the game's old districts (").Append(live ? "counted now from its buildings" : "as Build 3682 has them").Append(") have at most volume ")
                .Append(volumeMost.ToString("0", c)).Append(" (").Append(volumeBy ?? "-").Append("), ").Append(largeMost).Append(" large stores (").Append(largeBy ?? "-").Append("), ")
                .Append(shopsMost).Append(" shops, ").Append(upperMost.ToString("0.0", c)).Append(" upper-class homes");
            text.Append(": ").Append(pass ? "PASS" : "FAIL");
            line = text.ToString();
            brief = new[]
            {
                rows[0].Volume.ToString("0", c), rows[1].Volume.ToString("0", c), volumeMost.ToString("0", c), largeMost.ToString(c)
            };
            return pass;
        }
    }
}
