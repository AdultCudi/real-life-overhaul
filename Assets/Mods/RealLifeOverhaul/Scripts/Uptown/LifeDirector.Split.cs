using System;
using System.Collections.Generic;
using Buildings;
using RealLifeOverhaul.DistrictSave;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The split of the band into two districts for a save made before it (SplitRules; plans/uptown-split.md): once, at the first
    /// load after the split, the rebalance of the band's large stores is decided lot by lot (a lot someone holds keeps its store;
    /// the others are rebuilt as offices, their old shops' records let go) before the plan is read, and, once the district save has
    /// put the save's records back, everything the save holds under Uptown's id that the second district needs is copied for it
    /// (stats, demand rows, the ticked map row, the market level); buildings, lots, cells, projects, deeds and rentals name a place,
    /// so their district follows from it with nothing moved. One log line says what was done. A save that never had the band is
    /// untouched but for the line's marks. COST: nothing per frame; a pass over nineteen lots and the save's demand rows once a save.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private static readonly List<string> DecidedPins = new List<string>(8);   // the lots this save keeps as stores, decided at the load's start
        private static readonly List<string> DecidedRebuilt = new List<string>(24);
        private static readonly List<string> DecidedRetired = new List<string>(24);
        private static int _decidedGeneration;     // the rebalance's generation decided for this save (0: not decided at this load)
        private static int _splitWas;              // the split generation the save's line had when it was read
        private static bool _splitDone;            // the first load's moves have been made for this save

        // The rebalance of the band's large stores for a save, before anything reads the plan (the district save's early step, or the
        // city load when it had none). A save that decided it earlier keeps its pins; one whose line cannot be read keeps every lot as
        // it was; a save that never had the band has none held. Only where the band may live at this load (its option, the city, a
        // Going Public session): else nothing is decided or let go, and the plan stays the first plan's for this load.
        private static void DecidePlan(GameInstance save)
        {
            DecidedPins.Clear();
            DecidedRebuilt.Clear();
            DecidedRetired.Clear();
            _decidedGeneration = 0;
            LayoutRules.Pin(null);   // the plan with nothing held: its swaps are the candidates
            IReadOnlyList<string> swaps = LayoutRules.Swaps;
            if (save == null || !GroundSession.Agrees(out string why) || !LifeGame.NewYorkSave(save, out why))
            {
                SplitRules.KeepAll(swaps, DecidedPins);
                LayoutRules.Pin(DecidedPins);
                return;
            }
            LifeState read = LifeStore.Load(save.modData, out int _);
            if (read == null)
            {
                SplitRules.KeepAll(swaps, DecidedPins);   // its line cannot be read: kept as it is, nothing decided
                LayoutRules.Pin(DecidedPins);
                return;
            }
            if (!SplitRules.Undecided(read.Rebalance))
            {
                DecidedPins.AddRange(read.Pinned);
                _decidedGeneration = read.Rebalance;
                LayoutRules.Pin(DecidedPins);
                return;
            }
            _decidedGeneration = LayoutRules.RebalanceGeneration;
            if (read.Plan < LayoutRules.PlanGeneration)
            {
                return;   // a save of an earlier plan is set up afresh (RetireEarlierPlan): nothing of it is held
            }
            SplitRules.Decide(swaps, id => KeptAt(id) != null, id => HeldLot(id, read), DecidedPins, DecidedRebuilt, DecidedRetired);
            LayoutRules.Pin(DecidedPins);
            if (DecidedRetired.Count > 0)
            {
                var addresses = new List<KeyValuePair<string, int>>(DecidedRetired.Count);
                for (int i = 0; i < DecidedRetired.Count; i++)
                {
                    Lot lot = UptownLots.Get(DecidedRetired[i]);
                    addresses.Add(new KeyValuePair<string, int>(lot.Street, lot.Number));
                }
                DistrictSaveDirector.RetireAddresses(save, addresses, "Uptown's plan builds these lots as offices now");
            }
            Logger?.Info("Uptown: the rebalance of the band's large stores, decided for this save: " + swaps.Count + " of its M1 stores are over the districts' quotas (Uptown "
                         + DistrictSplit.LargeStoresUptown + ", Cedar Heights " + DistrictSplit.LargeStoresCedar + "), " + DecidedPins.Count + " of them stay stores because someone holds them, "
                         + DecidedRebuilt.Count + " are built as J1 offices, " + DecidedRetired.Count + " of those had records the district save lets go.");
        }

        // The registration the district save keeps for a lot's address, or null.
        private static BuildingRegistration KeptAt(string lotId)
        {
            Lot lot = UptownLots.Get(lotId);
            return lot != null ? DistrictSaveDirector.KeptRegistration(lot.Street, lot.Number) : null;
        }

        // A lot someone holds: the player's rent or deed, another player's, a building the construction game finished over it or knocked down there.
        private static bool HeldLot(string lotId, LifeState state)
        {
            Lot lot = UptownLots.Get(lotId);
            if (lot == null)
            {
                return true;
            }
            var b = new BuildingsLot { Id = lot.Id, Street = lot.Street, Number = lot.Number };
            return LifeGame.Someones(b) || state.Covering(lotId) != null || state.IsCleared(lotId);
        }

        // The decision (and the split's mark) into the state just read, so the line says them from this load on.
        private static void ApplyDecision()
        {
            _splitWas = _state.Split;
            if (_decidedGeneration > 0)
            {
                if (_state.Rebalance != _decidedGeneration || _state.Pinned.Count != DecidedPins.Count)
                {
                    _state.Rebalance = _decidedGeneration;
                    _state.Pinned.Clear();
                    _state.Pinned.AddRange(DecidedPins);
                }
            }
            _state.Split = LifeStore.SplitGeneration;
            _splitDone = false;
        }

        // After the district save has put the save's records back (the late "game loaded" call): the second district given its own of
        // what the save holds under Uptown's id. Once; idempotent. Returns the line for the log, or null when nothing was to move.
        private static string MoveBand(GameInstance save)
        {
            if (_splitDone)
            {
                return null;
            }
            _splitDone = true;
            bool band = _splitWas < LifeStore.SplitGeneration && LifeGame.HasStats(save, DistrictSplit.Uptown) && !LifeGame.HasStats(save, DistrictSplit.CedarHeights);
            if (!band)
            {
                return null;
            }
            int homes = 0, shops = 0, offices = 0, warehouses = 0, west = 0, east = 0, held = 0;
            for (int i = 0; i < Standing.Count; i++)
            {
                MadeBuilding made = Standing[i];
                if (made == null || made.Building == null)
                {
                    continue;
                }
                bool cedar = DistrictSplit.Index(made.Spec.District) == DistrictSplit.CedarHeights;
                west += cedar ? 1 : 0;
                east += cedar ? 0 : 1;
                BuildingRegistrationHeld(made, ref held, cedar);
                switch (DemandRules.UseOfType(made.Building.BuildingType))
                {
                    case 0: homes += cedar ? 1 : 0; break;
                    case 1: shops += cedar ? 1 : 0; break;
                    case 2: offices += cedar ? 1 : 0; break;
                    case 3: warehouses += cedar ? 1 : 0; break;
                }
            }
            int moved = LifeGame.MoveRows(save, out int demand, out int filters);
            return "Uptown: this save was made when the band was one district; it is now Uptown and Cedar Heights, cut along 5th Street: " + west + " building(s) west of it are Cedar Heights' ("
                   + homes + " homes, " + shops + " shops, " + offices + " offices, " + warehouses + " warehouses; " + held
                   + " of them held by the player or another player, whose rents and deeds are as they were) and "
                   + east + " Uptown's; the second district's stats start as the first's, " + demand + " demand row(s) and " + filters + " ticked map row(s) copied for it ("
                   + moved + " in all), its market starts at Uptown's level; lots, cells, projects and deeds name a place, so each is in the district its place is in.";
        }

        private static void BuildingRegistrationHeld(MadeBuilding made, ref int held, bool cedar)
        {
            BuildingRegistration reg = made.Registration;
            if (cedar && reg != null && (reg.RentedByPlayer || reg.BuildingOwnedByPlayer || GoingPublic.Foreign(reg)))
            {
                held++;
            }
        }
    }
}
