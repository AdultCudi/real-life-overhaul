namespace RealLifeOverhaul
{
    /// <summary>What kind of car a vehicle type is: decides how its gearbox behaves, and the fallback targets.</summary>
    public enum CarKind
    {
        Untouched,   // not a car the mod tunes (scooter, hand truck, flatbed): left exactly as the game made it
        Economy,
        Saloon,
        Luxury,
        Sports,
        Utility,     // SUVs and pickups
        Van,
        Truck
    }

    /// <summary>What a car should be able to do, before the player's two sliders.</summary>
    public readonly struct CarSpec
    {
        public readonly CarKind Kind;
        public readonly float Seconds;   // 0-100 km/h
        public readonly float TopKmh;

        public CarSpec(CarKind kind, float seconds, float topKmh)
        {
            Kind = kind;
            Seconds = seconds;
            TopKmh = topKmh;
        }
    }

    /// <summary>What the game's own vehicle type says about a car: all the fallback rule has to go on.</summary>
    public readonly struct CarTraits
    {
        public readonly bool Motor;        // has a fuel tank
        public readonly bool Carried;      // spawned in the player's hands (hand truck, flatbed)
        public readonly int GameTopKmh;    // the game's own "max speed": its tier, 45 to 100
        public readonly int Cargo;
        public readonly bool Luxury;
        public readonly float Mass;

        public CarTraits(bool motor, bool carried, int gameTopKmh, int cargo, bool luxury, float mass)
        {
            Motor = motor;
            Carried = carried;
            GameTopKmh = gameTopKmh;
            Cargo = cargo;
            Luxury = luxury;
            Mass = mass;
        }
    }

    /// <summary>
    /// The targets for every vehicle type the game has (one row each; README lists them), and a rule for
    /// types it does not know (vehicles added by other mods). Game-free: the command-line check of the
    /// car model compiles this file too.
    /// </summary>
    public static class CarCatalogue
    {
        private const string Prefix = "ba:vehicletype_";

        /// <summary>The spec for a vehicle type name such as "ba:vehicletype_bima320".</summary>
        public static CarSpec For(string typeName, in CarTraits traits)
        {
            string name = typeName ?? string.Empty;
            if (name.StartsWith(Prefix, System.StringComparison.Ordinal))
            {
                name = name.Substring(Prefix.Length);
            }
            switch (name)
            {
                //                                                kind             0-100 s  top km/h
                case "honzamimic": return new CarSpec(CarKind.Economy, 12.5f, 170f);          // small hatchback
                case "vordtiaravic": return new CarSpec(CarKind.Saloon, 10.0f, 190f);         // old full-size saloon
                case "bima320": return new CarSpec(CarKind.Saloon, 8.5f, 210f);               // family saloon
                case "mersaidis500": return new CarSpec(CarKind.Luxury, 6.5f, 240f);          // luxury saloon
                case "missamvillian": return new CarSpec(CarKind.Luxury, 6.2f, 240f);         // "Lytte L6", luxury saloon
                case "petrollsfanton": return new CarSpec(CarKind.Luxury, 6.8f, 240f);        // the heaviest luxury car
                case "limo": return new CarSpec(CarKind.Luxury, 10.5f, 180f);                 // stretched limousine
                case "vordpony": return new CarSpec(CarKind.Sports, 5.8f, 240f);              // muscle coupe
                case "ferdinand112": return new CarSpec(CarKind.Sports, 5.0f, 265f);          // sports car
                case "mersaidimgagt": return new CarSpec(CarKind.Sports, 4.7f, 280f);         // grand tourer
                case "anselmoaf90": return new CarSpec(CarKind.Sports, 4.5f, 290f);           // supercar
                case "umcnunavut": return new CarSpec(CarKind.Utility, 9.0f, 190f);           // large SUV
                case "vordv150": return new CarSpec(CarKind.Utility, 10.0f, 180f);            // pickup
                case "umcdesert":
                case "umcdesertdeliveryjob": return new CarSpec(CarKind.Van, 16.0f, 140f);    // old van (and the delivery job's)
                case "mersaididash": return new CarSpec(CarKind.Van, 14.5f, 150f);            // modern panel van
                case "deliverytruck": return new CarSpec(CarKind.Truck, 18.0f, 135f);         // box truck
                case "freighttruckt1": return new CarSpec(CarKind.Truck, 20.0f, 130f);        // heavy truck
                case "electricscooter":
                case "handtruck":
                case "flatbed": return new CarSpec(CarKind.Untouched, 0f, 0f);
                default: return Guess(traits);
            }
        }

        /// <summary>A type the table does not know: placed by the tier the game itself gave it, its cargo room and its luxury flag.</summary>
        public static CarSpec Guess(in CarTraits t)
        {
            if (!t.Motor || t.Carried || t.Mass < 600f)
            {
                return new CarSpec(CarKind.Untouched, 0f, 0f);
            }
            if (t.GameTopKmh <= 55)
            {
                return new CarSpec(CarKind.Truck, 18f, 135f);
            }
            if (t.GameTopKmh <= 65)
            {
                return t.Cargo >= 14 ? new CarSpec(CarKind.Van, 15f, 145f) : new CarSpec(CarKind.Economy, 12f, 170f);
            }
            if (t.Cargo >= 16)
            {
                return new CarSpec(CarKind.Utility, 10f, 180f);
            }
            if (t.GameTopKmh >= 95)
            {
                return new CarSpec(CarKind.Sports, 4.8f, 270f);
            }
            if (t.GameTopKmh >= 85)
            {
                return new CarSpec(CarKind.Sports, 5.5f, 250f);
            }
            return t.Luxury ? new CarSpec(CarKind.Luxury, 6.5f, 240f) : new CarSpec(CarKind.Saloon, 9f, 200f);
        }

        /// <summary>Engine speed, as a share of the rev limit, at which this kind changes up when driven gently.</summary>
        public static float UpshiftAt(CarKind kind)
        {
            switch (kind)
            {
                case CarKind.Sports: return 0.58f;
                case CarKind.Luxury:
                case CarKind.Utility: return 0.48f;
                case CarKind.Van: return 0.46f;
                case CarKind.Truck: return 0.45f;
                default: return 0.50f;
            }
        }

        /// <summary>Seconds a gear change takes (no drive meanwhile).</summary>
        public static float ShiftSeconds(CarKind kind)
        {
            switch (kind)
            {
                case CarKind.Sports: return 0.12f;
                case CarKind.Saloon: return 0.22f;
                case CarKind.Utility: return 0.28f;
                case CarKind.Van: return 0.35f;
                case CarKind.Truck: return 0.45f;
                default: return 0.25f;
            }
        }
    }
}
