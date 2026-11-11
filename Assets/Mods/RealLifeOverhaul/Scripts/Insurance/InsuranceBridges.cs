using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>A car of the player's as the Garage knows it, read on a day's pass, a claim or a press.</summary>
    public struct CarLook
    {
        public string Id, Name;
        public float Price;          // the list price
        public float Damage;         // 0 to 1
        public bool Broken;
        public int InsuredUntil;     // the last day of the paid month; under today for none
        public float Premium;        // the Garage's month's premium before the tier and the driver's record
    }

    /// <summary>A home of the player's as the game knows it.</summary>
    public struct HomeLook
    {
        public string Place, Name, District;
        public int Sqm;
    }

    /// <summary>
    /// What insurance asks of the other features, filled in by Wiring/Wiring.InsuranceLinks and left empty (the default answer) while a feature is off. Every answer is
    /// read on a day's pass, a claim or a press, never per frame.
    /// </summary>
    public static class InsuranceBridges
    {
        // ---- the driver
        /// <summary>The player's credit grade, very poor 0 to excellent 5. Null: good, the list price.</summary>
        public static Func<int> Grade;

        /// <summary>The points on the player's driving licence today. Null: none.</summary>
        public static Func<int> Points;

        // ---- the Garage's cars
        /// <summary>Fills in the cars the Garage keeps a policy for (the player's own, never a dealer's stock or a partner's); false while car ownership does not run.</summary>
        public static Func<List<CarLook>, bool> Cars;

        /// <summary>Insures a car at the Garage from today at its premium; what was charged, under nothing if the money was not there. Null: nothing.</summary>
        public static Func<string, float> InsureCar;

        /// <summary>Ends a car's policy at the Garage. Null: nothing.</summary>
        public static Action<string> EndCar;

        /// <summary>The id of the car the player is driving, if it is their own. Null: none.</summary>
        public static Func<string> DrivenCar;

        // ---- the homes
        /// <summary>A district's income level, 60 to 140, by the game's name for it. Null: the middle.</summary>
        public static Func<string, int> DistrictIncome;

        // ---- Health's plan
        /// <summary>Fills in the player's health plan and the three on offer; false while Health does not run.</summary>
        public static Func<HealthCoverFact, bool> Health;

        /// <summary>Takes or changes the health plan (1 to 3) by Health's own call, which says what happened. Null: nothing.</summary>
        public static Action<int> TakeHealth;

        /// <summary>Cancels the health plan by Health's own call. Null: nothing.</summary>
        public static Action CancelHealth;

        // ---- the Company's business cover
        /// <summary>Fills in each business's three kinds of cover and the insurers' names; false while the Company's cover does not run.</summary>
        public static Func<List<BizCoverFact>, string[], bool> Business;

        /// <summary>Adds the Company's claims (still to put in, put in, paid) to a list.</summary>
        public static Action<List<ClaimFact>> BusinessClaims;

        /// <summary>Buys a kind of cover at a business from an insurer by the Company's own call. Null: nothing.</summary>
        public static Action<string, int, int> BuyBusiness;

        /// <summary>Cancels a kind of cover at a business by the Company's own call. Null: nothing.</summary>
        public static Action<string, int> CancelBusiness;

        /// <summary>Puts a business claim in by the Company's own call. Null: nothing.</summary>
        public static Action<int> FileBusinessClaim;

        // ---- the rival tycoons
        /// <summary>A rival tycoon's worth in whole money, or -1 while they are not forward or are out, and for every one in a Going Public session. Null: -1.</summary>
        public static Func<int, long> RivalWorth;

        /// <summary>The floor a tycoon's worth is raised to each day, at today's prices. Null: 0.</summary>
        public static Func<int, long> RivalFloor;

        /// <summary>A tycoon pays so much from the worth over their floor; returns what really left them. Null: nothing.</summary>
        public static Func<int, long, long> RivalPay;

        /// <summary>A tycoon's name. Null: "".</summary>
        public static Func<int, string> RivalName;

        /// <summary>A month's premium of a health plan (1 to 3) for a tycoon today, by Health's own rule. Null: no plan can be bought.</summary>
        public static Func<int, long> RivalQuote;

        /// <summary>What a health plan (1 to 3) pays of a hospital bill given what it has paid this year, by Health's own rule. Null: nothing.</summary>
        public static Func<int, long, long, long> RivalClaim;

        /// <summary>A health plan's name (1 to 3) in the game's language. Null: "".</summary>
        public static Func<int, string> PlanName;

        public static void Reset()
        {
            Grade = null;
            Points = null;
            Cars = null;
            InsureCar = null;
            EndCar = null;
            DrivenCar = null;
            DistrictIncome = null;
            Health = null;
            TakeHealth = null;
            CancelHealth = null;
            Business = null;
            BusinessClaims = null;
            BuyBusiness = null;
            CancelBusiness = null;
            FileBusinessClaim = null;
            RivalWorth = null;
            RivalFloor = null;
            RivalPay = null;
            RivalName = null;
            RivalQuote = null;
            RivalClaim = null;
            PlanName = null;
        }
    }
}
