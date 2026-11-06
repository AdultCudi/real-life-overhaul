using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's big special places in the district's load (docs/features/uptown.md, "The wholesale store and the filling
    /// station"): each of BigPlacesRules' places declared to the made buildings' engine as one of the game's own on its
    /// ground, a copy of the game's building whole with the game's service copied under its own name; a place whose lots the
    /// save holds someone's unit on (the player's, or another player's in a Going Public session) stands down at that load
    /// and its lots are the plan's ordinary ones, so nobody's unit is ever taken. Nothing here runs a frame.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private static readonly HashSet<string> BigDown = new HashSet<string>(StringComparer.Ordinal);   // lots of a place that does not stand at this load: ordinary ones
        private static readonly List<string> BigDeclared = new List<string>(2);   // the own lots of the places declared at this load

        /// <summary>The big special place of that kind standing at this load, or null (it stood down: Player.log says why).</summary>
        public static MadeBuilding Big(BigKind kind)
        {
            BigPlace place = BigPlacesRules.Of(kind);
            if (place == null || !Lives || !BigDeclared.Contains(place.Lot))
            {
                return null;
            }
            for (int i = 0; i < Standing.Count; i++)
            {
                if (Standing[i].Spec.Id == place.Lot)
                {
                    return Standing[i];
                }
            }
            return null;
        }

        // Before the plan's lots are declared: a place whose lots the save holds someone's unit on does not stand, and its lots
        // are declared as the plan's ordinary ones (a kept registration builds one), so the unit keeps its building.
        private static void DecideBig()
        {
            BigDown.Clear();
            BigDeclared.Clear();
            for (int p = 0; p < BigPlacesRules.All.Length; p++)
            {
                BigPlace place = BigPlacesRules.All[p];
                string whose = null;
                ForEachLot(place, id =>
                {
                    if (whose == null && FindLot(id, out BuildingsLot lot) && LifeGame.Someones(lot))
                    {
                        whose = id;
                    }
                });
                if (whose == null)
                {
                    continue;
                }
                ForEachLot(place, id => BigDown.Add(id));
                Logger?.Info("Uptown: " + place.Name + " (" + place.Kind + ") does not stand at this load: the save holds someone's unit on lot " + whose
                             + "; its lots are the plan's ordinary ones.");
            }
        }

        private static void ForEachLot(BigPlace place, Action<string> each)
        {
            each(place.Lot);
            for (int i = 0; i < place.Over.Length; i++)
            {
                each(place.Over[i]);
            }
            for (int i = 0; i < place.KeptClear.Length; i++)
            {
                each(place.KeptClear[i]);
            }
        }

        // The store among the game's wholesale stores once the district's buildings are in the game's lists, or out of them
        // as the district stands down: the game keeps that list once made, until the next load (LifeGame.Wholesaler).
        private static void ListBig(bool listed)
        {
            for (int i = 0; i < Standing.Count; i++)
            {
                MadeBuilding made = Standing[i];
                if (made != null && BigDeclared.Contains(made.Spec.Id) && !LifeGame.Wholesaler(made.Building, made.Registration, listed))
                {
                    Logger?.Warn("Uptown: " + made.Address + " could not be " + (listed ? "put among" : "taken out of") + " the game's wholesale stores.");
                }
            }
        }

        /// <summary>
        /// "Uptown check (big places)", once a load (a FAIL always written, a PASS with Developer diagnostics): each place that may stand
        /// (not stood down for someone's unit) has its building up as one of the game's own, whole. A place's ground is never among the
        /// plan's lots, so build mode draws no squares there: the building is all that is on it, and one that never stood left a bare
        /// patch with no squares (the owner, 27 Oct 2026: "empty spots in the block with no grids").
        /// </summary>
        private static void BigCensus(string when)
        {
            var line = new StringBuilder(256);
            int wanted = 0, whole = 0;
            for (int p = 0; p < BigPlacesRules.All.Length; p++)
            {
                BigPlace place = BigPlacesRules.All[p];
                if (BigDown.Contains(place.Lot))
                {
                    continue;   // stood down for a unit somebody holds: its lots are the plan's ordinary ones, said at the load
                }
                wanted++;
                MadeBuilding made = Big(place.Kind);
                string why = made == null ? "no building stands on its lot " + place.Lot : LifeGame.BigWhy(made, place);
                whole += why == null ? 1 : 0;
                line.Append(line.Length > 0 ? "; " : string.Empty).Append(place.Name).Append(made != null ? " at " + made.Address : string.Empty)
                    .Append(why == null ? ": standing whole" : ": " + why);
            }
            bool pass = whole == wanted;
            string text = "Uptown check (big places): at " + when + ", " + whole + " of " + wanted + " stand" + (wanted > 0 ? ": " + line : string.Empty) + ": " + (pass ? "PASS" : "FAIL");
            if (!pass)
            {
                Logger?.Warn(text);
            }
            else if (Meta.Dev.Verbose)
            {
                Logger?.Info(text);
            }
        }

        // Every place that may stand declared on its ground; how many.
        private static int DeclareBig()
        {
            int declared = 0;
            for (int p = 0; p < BigPlacesRules.All.Length; p++)
            {
                BigPlace place = BigPlacesRules.All[p];
                if (BigDown.Contains(place.Lot))
                {
                    continue;
                }
                if (!BigPlacesRules.Ground(place, Plan, out BuildingsLot ground))
                {
                    Logger?.Warn("Uptown: " + place.Name + " (" + place.Kind + ") does not stand: a lot of it is not in the district's plan.");
                    continue;
                }
                Lot plain = UptownLots.PlaceOf(place.Lot);   // a place is never among the plan's lots: Get(place.Lot) is null, and a null here kept both places from ever standing
                MadeSpec spec = LifeGame.BigSpec(place, ground, TrafficOf(place.Lot), () => _known && !_down && UptownLots.Ready && plain != null && UptownLots.Open(plain));
                MadeBuildings.Declare(spec);
                Declared.Add(spec);
                BigDeclared.Add(place.Lot);
                declared++;
            }
            return declared;
        }
    }
}
