using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What a car's policy at the Garage asks of Insurance (Wiring/InsuranceLinks): the tier the player chose for the car and the driver's record. Each left null, or
    /// answering that Insurance is off, leaves the policy as the Garage has always written it: one tier, the premium by the car's price and claims, 80% of a repair
    /// over the excess, and a policy that ends the morning its premium cannot be taken. Asked on a purchase, a renewal, a repair and a page, never per frame.
    /// </summary>
    public static class CarCover
    {
        /// <summary>A repair's claim: the car, its name, its list price, its damage before and after (0 to 1) and whether a police report waives the excess. False: the Garage's own terms.</summary>
        public delegate bool RepairClaim(string id, string name, float price, float before, float after, bool reported, out float payout, out float excess);

        /// <summary>A car's month's premium as its tier and the driver's record make it, from the Garage's own. Null: the Garage's own.</summary>
        public static Func<string, float, float> Premium;

        /// <summary>A policy was bought: the car, its damage (0 to 1), whether it had none before, and what was charged.</summary>
        public static Action<string, float, bool, float> Bought;

        /// <summary>A month's renewal was paid: the car and what was charged.</summary>
        public static Action<string, float> Renewed;

        /// <summary>A renewal's premium (the car, its name, the premium) could not be taken: true while the cover holds in its days of grace. Null: no grace.</summary>
        public static Func<string, string, float, bool> Missed;

        /// <summary>What the policy pays of a repair. Null: the Garage's own terms.</summary>
        public static RepairClaim Repair;

        /// <summary>The premium the Garage shows, charges and renews at for a car.</summary>
        public static float PremiumOf(string id, float garage)
        {
            Func<string, float, float> premium = Premium;
            return premium != null ? premium(id, garage) : garage;
        }

        public static void Reset()
        {
            Premium = null;
            Bought = null;
            Renewed = null;
            Missed = null;
            Repair = null;
        }
    }
}
