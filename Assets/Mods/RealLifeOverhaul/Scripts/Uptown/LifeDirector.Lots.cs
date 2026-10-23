using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The district's lots for the construction game (DistrictBuildings): which hold a building, a finished building kept in
    /// the district's own line of the save and stood as a district building from the next load, a lot's traffic index. The
    /// same calls give the same line on every machine with the same mod. Nothing here runs a frame.
    /// </summary>
    public sealed partial class LifeDirector
    {
        /// <summary>True when the lot holds a district building at this load, or one finished there waits for the next load.</summary>
        public static bool Taken(string lotId)
        {
            if (string.IsNullOrEmpty(lotId))
            {
                return false;
            }
            return _state.On(lotId) != null || DayOneLots.Contains(lotId) || StandsOn(lotId);
        }

        /// <summary>True when a district building stands on the lot at this load.</summary>
        public static bool StandsOn(string lotId)
        {
            for (int i = 0; i < Standing.Count; i++)
            {
                if (string.Equals(Standing[i].Spec.Id, lotId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The lot's traffic index by the rules, or -1 for a lot not in the plan.</summary>
        public static int TrafficOf(string lotId)
        {
            return FindLot(lotId, out BuildingsLot lot) ? BuildingsRules.Traffic(lot, _nearest, LifeGame.AwayX, LifeGame.AwayZ) : -1;
        }

        /// <summary>A building finished on an empty lot, kept in the district's line, a district building from the next load (DistrictBuildings.Finish).</summary>
        public static bool Finish(string lotId, string design, string size, int version, string type, int storeys, FinishOwner owner, double price, out string why)
        {
            bool known = FindLot(lotId, out BuildingsLot lot);
            bool structure = known && MadeGame.StructureTakes(size, version, type);
            why = BuildingsRules.FinishRefusal(lot, known, Taken(lotId), size, version, type, storeys, owner, price, structure, Lives, GoingPublic.InSession);
            if (why == null && _stateUnread)
            {
                why = "the district's own line in this save cannot be read (a later version's, or damaged), and is kept as it is";
            }
            if (why == null && _state.Finished.Count >= LifeStore.FinishedMost)
            {
                why = "the district keeps no more than " + LifeStore.FinishedMost + " finished buildings";
            }
            if (why != null)
            {
                Logger?.Info("Uptown: a building finished on lot " + (lotId ?? "(none)") + " is refused: " + why + ".");
                return false;
            }
            GameInstance save = SaveGameManager.Current;
            _state.Finished.Add(new FinishedBuilding
            {
                Lot = lotId, Size = size, Version = version, Type = type, Design = design ?? string.Empty, Storeys = storeys, Owner = owner,
                Price = owner == FinishOwner.Player ? price : 0, Day = save != null ? save.Day : 0, Settled = false
            });
            WriteState();
            _stamp++;
            Logger?.Info("Uptown: a building finished on lot " + lotId + " (" + NewBuildingsRules.StructureName(size, version) + ", " + type + ", "
                         + (owner == FinishOwner.Player ? "the player's own" : "the city's") + ") stands there as a district building from the next load.");
            return true;
        }

        private static bool FindLot(string lotId, out BuildingsLot lot)
        {
            for (int i = 0; !string.IsNullOrEmpty(lotId) && i < Plan.Count; i++)
            {
                if (string.Equals(Plan[i].Id, lotId, System.StringComparison.Ordinal))
                {
                    lot = Plan[i];
                    return true;
                }
            }
            lot = default;
            return false;
        }
    }
}
