namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's buildings for the construction game (Scripts/Construction; docs/features/uptown.md, "For the construction
    /// game"): which lots hold a building, a building finished on an empty lot made one of the district's from the next load
    /// (in the game's own lists and simulation, its records kept by the district save), a lot's traffic index and the price
    /// of the district's land, the same on every machine with the same mod. Lot ids are the ground's plan's (UptownLots).
    /// Read while <see cref="Lives"/>; a look once a second is one comparison with <see cref="Stamp"/>.
    /// COST: nothing per frame; each call a pass over the district's lots at most.
    /// </summary>
    public static class DistrictBuildings
    {
        /// <summary>True while Uptown is a district of the game with its buildings standing at this load.</summary>
        public static bool Lives => LifeDirector.Lives;

        /// <summary>Changes whenever a lot's state does.</summary>
        public static int Stamp => LifeDirector.Stamp;

        /// <summary>True when the lot holds a district building (from the first day, or finished) or one finished there waits for the next load: not for sale.</summary>
        public static bool Taken(string lotId)
        {
            return LifeDirector.Taken(lotId);
        }

        /// <summary>True when a district building stands on the lot at this load (its copy made behind the loading screen): place no look of your own there.</summary>
        public static bool Finished(string lotId)
        {
            return LifeDirector.StandsOn(lotId);
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

        /// <summary>The traffic index a building on the lot gets (Building.trafficIndex, 10 to 60), as the rules give it; -1 for a lot not in the plan.</summary>
        public static int TrafficOf(string lotId)
        {
            return LifeDirector.TrafficOf(lotId);
        }

        /// <summary>The price of a square metre of Uptown's land, as the game prices a building there before its type and traffic (base price times the district's multiplier).</summary>
        public static float LandPricePerSqm => DistrictValues.LandPerSqm(LifeGame.Figures);
    }
}
