namespace RealLifeOverhaul.Subway
{
    /// <summary>Why the districts' stations are asked to go. Every cause goes through the same rule (<see cref="StationsGoing"/>).</summary>
    public enum StationExit
    {
        OptionOff = 0,         // the option was switched off in play
        DistrictClosing = 1,   // the districts' ground closed, or does not stand
        CleanUp = 2,           // the feature's clean-up (the options page, faults in a row, the save tools, another city)
        DirectorGone = 3,      // the director was destroyed with the city staying, whoever stopped the mod
        CityUnloading = 4      // the city's own scene is going: whatever stands goes with it
    }

    /// <summary>What the caller does next.</summary>
    public enum StationStep
    {
        Remove = 0,              // out of the game's lists, then away
        ShutMapThenRemove = 1,   // the game's map shut first, then as Remove
        Wait = 2,                // a ride or a taxi trip is under way: asked again at the next look
        HandOver = 3,            // like Wait, but the asker is going: a watcher that outlives it asks in its place
        Drop = 4                 // nothing is destroyed or taken out: the scene takes them, only the mod's own record is dropped
    }

    /// <summary>What of the game's holds a station of the districts' own, read at the moment of asking.</summary>
    public readonly struct StationHolds
    {
        public readonly bool Riding;          // a ride of the game's is under way
        public readonly bool RideTouchesOwn;  // ... and it begins or ends at one of the districts' stations
        public readonly bool TaxiTravelling;  // a taxi trip (a taxi, the private driver) is under way: the game holds the place it was sent to
        public readonly bool MapPicksFromOwn; // the game's map is open on its list of stations from one of the districts'

        public StationHolds(bool riding, bool rideTouchesOwn, bool taxiTravelling, bool mapPicksFromOwn)
        {
            Riding = riding;
            RideTouchesOwn = rideTouchesOwn;
            TaxiTravelling = taxiTravelling;
            MapPicksFromOwn = mapPicksFromOwn;
        }
    }

    /// <summary>
    /// The one rule by which the districts' stations go (docs/features/subway-uptown.md, "Going"), game-free: whatever the cause (the option
    /// off, the districts closing, the clean-up, the director destroyed, the city unloading) the stations are never taken away under a rider
    /// of the game's ride or of a taxi trip, and the game's map is shut before one it lists from them goes. The director and the watcher that
    /// outlives it both ask it and do what it says. COST: a few comparisons; asked on the look, never per frame.
    /// </summary>
    public sealed class StationsGoing
    {
        /// <summary>True while a going waits for a ride or a taxi trip to end.</summary>
        public bool Waiting { get; private set; }

        /// <summary>The cause of the going that waits.</summary>
        public StationExit Cause { get; private set; }

        /// <summary>The words for the log of the going that waits.</summary>
        public string Why { get; private set; }

        /// <summary>What holds the going now, when it waits.</summary>
        public StationRemoval Holding { get; private set; }

        /// <summary>A going is asked for: what to do now.</summary>
        public StationStep Go(StationExit cause, string why, StationHolds holds)
        {
            if (cause == StationExit.CityUnloading)
            {
                Rest();
                return StationStep.Drop;
            }
            StationRemoval removal = StationsRules.Removal(holds.Riding, holds.RideTouchesOwn, holds.TaxiTravelling, holds.MapPicksFromOwn);
            if (removal == StationRemoval.AfterRide || removal == StationRemoval.AfterTaxi)
            {
                if (cause == StationExit.DirectorGone)
                {
                    Rest();
                    return StationStep.HandOver;
                }
                Waiting = true;
                Cause = cause;
                Why = why;
                Holding = removal;
                return StationStep.Wait;
            }
            Rest();
            return removal == StationRemoval.MapFirst ? StationStep.ShutMapThenRemove : StationStep.Remove;
        }

        /// <summary>A going that waits is asked again (the look, the watcher's): the same rule on what holds now.</summary>
        public StationStep Again(StationHolds holds)
        {
            return Go(Cause, Why, holds);
        }

        /// <summary>The going is called off (the option is back on while a ride was let finish).</summary>
        public void Cancel()
        {
            Rest();
        }

        private void Rest()
        {
            Waiting = false;
            Why = null;
            Holding = StationRemoval.Now;
        }
    }
}
