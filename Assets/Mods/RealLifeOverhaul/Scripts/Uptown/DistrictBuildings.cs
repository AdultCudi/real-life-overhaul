using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's buildings for the construction game (Scripts/Construction; docs/features/uptown.md, "For the construction
    /// game"): which lots hold a building, a building finished on an empty lot made one of the district's from the next load
    /// (in the game's own lists and simulation, its records kept by the district save), one standing over several lots of
    /// its block (a landmark's, Scripts/Landmarks), the plan's lots built from the first day, a lot's traffic index and the
    /// price of the district's land, the same on every machine with the same mod. Lot ids are the ground's plan's (UptownLots).
    /// Read while <see cref="Lives"/>; a look once a second is one comparison with <see cref="Stamp"/>.
    /// COST: nothing per frame; each call a pass over the district's lots at most.
    /// </summary>
    public static class DistrictBuildings
    {
        /// <summary>True while Uptown is a district of the game with its buildings standing at this load.</summary>
        public static bool Lives => LifeDirector.Lives;

        /// <summary>Changes whenever a lot's state does.</summary>
        public static int Stamp => LifeDirector.Stamp;

        /// <summary>
        /// What stands near a lot lifts its traffic (a landmark's crowds): points over the lot's own index, 0 for none. Set once
        /// at game start by the feature whose places do it (Scripts/Landmarks, from its own line of the save alone, so it is
        /// asked while the city loads); the same on every machine with the same save. Unset: no lift.
        /// </summary>
        public static Func<string, int> TrafficLift;

        /// <summary>
        /// Lots a feature holds for buildings of its own still to come (Scripts/Landmarks: a landmark's site), so nothing else
        /// is put on them; set once at game start. Unset: none held.
        /// </summary>
        public static Func<string, bool> HeldFor;

        private static bool _heldFaulted;

        /// <summary>
        /// True when the lot is not free for anything new: a district building on it (from the first day, or finished), one
        /// finished there waiting for the next load, or the lot held for a landmark (<see cref="HeldFor"/>). Not for sale.
        /// </summary>
        public static bool Taken(string lotId)
        {
            return LifeDirector.Taken(lotId) || Held(lotId);
        }

        /// <summary>True when a district building stands on the lot or waits for the next load there, held or not: what a feature holding lots asks of its own.</summary>
        public static bool Occupied(string lotId)
        {
            return LifeDirector.Taken(lotId);
        }

        // A lot held for buildings still to come, as the feature holding it says; none after a fault (said once).
        private static bool Held(string lotId)
        {
            Func<string, bool> held = HeldFor;
            if (held == null || _heldFaulted || string.IsNullOrEmpty(lotId))
            {
                return false;
            }
            try
            {
                return held(lotId);
            }
            catch (Exception e)
            {
                _heldFaulted = true;
                LifeDirector.Logger?.Error(e);
                return false;
            }
        }

        /// <summary>True when a district building stands on the lot at this load (its copy made behind the loading screen): place no look of your own there.</summary>
        public static bool Finished(string lotId)
        {
            return LifeDirector.StandsOn(lotId);
        }

        /// <summary>The district's buildings standing at this load, into the caller's list (not cleared): each one's Spec.Id is its own lot's id.</summary>
        public static void Standing(List<MadeBuilding> into)
        {
            LifeDirector.Buildings(into);
        }

        /// <summary>True for a lot the plan builds from the first day: fixed data, known before the city loads and whether or not the district lives.</summary>
        public static bool FirstDay(string lotId)
        {
            return LifeDirector.FirstDay(lotId);
        }

        /// <summary>
        /// A building finished on an empty lot, a district building from the next load: in the design given (a DesignEntry's name,
        /// or null for the copy engine's pick, the same on every machine), the game's size, version and type, <paramref name="storeys"/>
        /// of floor area (1 to 12; 0 for that of the game's building its design copies). <see cref="FinishOwner.Player"/>: the game's own
        /// deed at its first load (<paramref name="price"/> its purchase price), never in a Going Public session; <see cref="FinishOwner.City"/>:
        /// let to the city's businesses. False, with why, when refused (Uptown not living, the lot unknown or taken, a size its class
        /// does not take, a structure the game lacks).
        /// </summary>
        public static bool Finish(string lotId, string design, string size, int version, string type, int storeys, FinishOwner owner, double price, out string why)
        {
            return LifeDirector.Finish(lotId, design, size, version, type, storeys, owner, price, out why);
        }

        /// <summary>
        /// As <see cref="Finish"/>, the building standing over <paramref name="over"/> as well: other empty lots of the same block
        /// (a landmark's site). Its address is its own lot's, its ground the rectangle round all of them, its door on its own
        /// lot's street; its size need not be one its own lot's class takes, as its design stands only on ground it fits. Every lot
        /// is Taken from then on, and Finished while it stands. Refused, with why, for a lot not in the plan, in another block,
        /// taken, or named twice.
        /// </summary>
        public static bool FinishOver(string lotId, IList<string> over, string design, string size, int version, string type, int storeys, FinishOwner owner, double price,
                                      out string why)
        {
            return LifeDirector.FinishOver(lotId, over, design, size, version, type, storeys, owner, price, out why);
        }

        /// <summary>A building finished but not yet stood at any load taken back (a test's way back): false, with why, once a load has stood it.</summary>
        public static bool Withdraw(string lotId, out string why)
        {
            return LifeDirector.Withdraw(lotId, out why);
        }

        /// <summary>
        /// The traffic index a building on the lot gets (Building.trafficIndex, 10 to 60, lifted by <see cref="TrafficLift"/>
        /// to 80 at most), as the rules give it; -1 for a lot not in the plan.
        /// </summary>
        public static int TrafficOf(string lotId)
        {
            return LifeDirector.TrafficOf(lotId);
        }

        /// <summary>What <see cref="TrafficLift"/> answers changed (a landmark finished): <see cref="Stamp"/> moves, so readers read the lots again.</summary>
        public static void LiftsChanged()
        {
            LifeDirector.LiftsChanged();
        }

        /// <summary>The price of a square metre of Uptown's land, as the game prices a building there before its type and traffic (base price times the district's multiplier).</summary>
        public static float LandPricePerSqm => DistrictValues.LandPerSqm(LifeGame.Figures);
    }
}
