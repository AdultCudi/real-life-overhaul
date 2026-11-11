namespace RealLifeOverhaul
{
    /// <summary>A kind of offence by the player. DrunkDriving is not in the road data: it is found at a stop or reported by Leisure.</summary>
    internal enum Offence { None, RedLight, Speeding, Reckless, WrongSide, Jaywalk, CrossOnRed, DrunkDriving }

    /// <summary>
    /// Which traffic offences the police act on, one switch each (Settings.Offences, the options page's Traffic offences). Reckless
    /// is driving fast on the pavement and WrongSide driving fast against the traffic. The horn has a switch of its own in
    /// HornReactions, so it is not here.
    /// </summary>
    internal readonly struct OffenceSwitches
    {
        public readonly bool RedLight, Speeding, Pavement, WrongWay, Jaywalk, CrossRed, Drink;

        public OffenceSwitches(bool redLight, bool speeding, bool pavement, bool wrongWay, bool jaywalk, bool crossRed, bool drink)
        {
            RedLight = redLight;
            Speeding = speeding;
            Pavement = pavement;
            WrongWay = wrongWay;
            Jaywalk = jaywalk;
            CrossRed = crossRed;
            Drink = drink;
        }

        /// <summary>True if any offence at the wheel that the road data shows is on: only then is the road map worth building.</summary>
        public bool Driving => RedLight || Speeding || Pavement || WrongWay;

        /// <summary>True if either offence on foot is on: only then is the foot look worth making.</summary>
        public bool OnFoot => Jaywalk || CrossRed;

        /// <summary>True if the timed looks have anything to find. Drink is reported by Leisure and the horn is HornReactions', so neither counts.</summary>
        public bool Any => Driving || OnFoot;

        /// <summary>Whether the police act on this kind of offence. False for None, and for a value this table does not know.</summary>
        public bool Allows(Offence kind)
        {
            switch (kind)
            {
                case Offence.RedLight: return RedLight;
                case Offence.Speeding: return Speeding;
                case Offence.Reckless: return Pavement;
                case Offence.WrongSide: return WrongWay;
                case Offence.Jaywalk: return Jaywalk;
                case Offence.CrossOnRed: return CrossRed;
                case Offence.DrunkDriving: return Drink;
                default: return false;
            }
        }
    }

    /// <summary>
    /// What shows that the player is carried and not walking, each a cheap read of the game (PlayerRide.Read). One sign is enough:
    /// the game's own vehicles, its taxi and private driver, the mod's own bus and another mod's ride each set a different one.
    /// </summary>
    internal readonly struct RideFacts
    {
        public readonly bool OwnVehicle;     // the save has an active vehicle: a car, a bike or a scooter of the game's own
        public readonly bool TaxiTrip;       // the game's taxi or private driver trip is under way
        public readonly bool Seated;         // sitting on a seat of the game's
        public readonly bool Blocked;        // the game does not let the player steer: every ride sets one of its walking blockers
        public readonly bool UnderVehicle;   // the player's body hangs under a vehicle in the scene
        public readonly bool TooFast;        // moved faster than anyone on foot goes since the last look

        public RideFacts(bool ownVehicle, bool taxiTrip, bool seated, bool blocked, bool underVehicle, bool tooFast)
        {
            OwnVehicle = ownVehicle;
            TaxiTrip = taxiTrip;
            Seated = seated;
            Blocked = blocked;
            UnderVehicle = underVehicle;
            TooFast = tooFast;
        }
    }

    /// <summary>
    /// The rules of the player's traffic offences, with no Unity or game types: what a new switch starts from, and when the
    /// player is carried and so cannot be walking in a road. Checked offline (EconomyCheck, TrafficOffenceChecks).
    /// </summary>
    internal static class OffenceRules
    {
        public const float FootMost = 8f;    // m/s: above the most the game's movement gives (WalkRules.MostForward, 6)
        public const float LookStale = 2f;   // seconds: a look this long ago (a menu, a cooldown) is no measure of a speed

        /// <summary>
        /// A traffic offence's switch when the options are read: the player's own saved choice, else the choice the one switch that
        /// came before them had, else the default. Nothing is written, so a switch the player has not touched follows the older
        /// one until they do.
        /// </summary>
        public static bool Seeded(bool hasOwn, bool own, bool hasOlder, bool older, bool fallback)
        {
            return hasOwn ? own : hasOlder ? older : fallback;
        }

        /// <summary>
        /// True when the player went faster than anyone on foot goes: <paramref name="movedSqr"/> square metres in
        /// <paramref name="seconds"/>. False when there was no earlier look, or it is too old to measure a speed by.
        /// </summary>
        public static bool TooFast(float movedSqr, float seconds)
        {
            return seconds > 0f && seconds <= LookStale && movedSqr > FootMost * FootMost * seconds * seconds;
        }

        /// <summary>
        /// True when something carries the player and they cannot be walking, whoever made the ride: any one sign is enough, so a
        /// ride is told whichever of them its maker set. No offence on foot is looked for then.
        /// </summary>
        public static bool Carried(RideFacts ride)
        {
            return ride.OwnVehicle || ride.TaxiTrip || ride.Seated || ride.Blocked || ride.UnderVehicle || ride.TooFast;
        }

        /// <summary>The signs that are on, for the log; "no sign of a ride" when none is. Allocates: for a log line written once per event.</summary>
        public static string Signs(RideFacts ride)
        {
            string signs = Add(string.Empty, ride.OwnVehicle, "the game's own vehicle");
            signs = Add(signs, ride.TaxiTrip, "a taxi trip");
            signs = Add(signs, ride.Seated, "seated");
            signs = Add(signs, ride.Blocked, "walking blocked");
            signs = Add(signs, ride.UnderVehicle, "under a vehicle");
            signs = Add(signs, ride.TooFast, "faster than anyone walks");
            return signs.Length == 0 ? "no sign of a ride" : signs;
        }

        private static string Add(string signs, bool on, string sign)
        {
            return !on ? signs : signs.Length == 0 ? sign : signs + ", " + sign;
        }
    }
}
