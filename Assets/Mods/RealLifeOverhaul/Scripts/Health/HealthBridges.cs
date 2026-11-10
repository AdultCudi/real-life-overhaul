using System;

namespace RealLifeOverhaul.Health
{
    /// <summary>
    /// What health asks of the other features, filled in by Wiring/Wiring.HealthLinks and left empty (the default answer) while a feature is off. Every answer is read on an
    /// hour's or a day's pass or a press, never per frame.
    /// </summary>
    public static class HealthBridges
    {
        // ---- Home and Life: the player's own body
        /// <summary>The illness the player has now, as a <see cref="Condition"/> number (Home's; Life's plain sickness is a cold). Null: none.</summary>
        public static Func<int> Illness;

        /// <summary>True while that illness is on a prescription. Null: no.</summary>
        public static Func<bool> Treated;

        /// <summary>The player catches a condition for a cause (a locale key), by Home's own rules; false if they did not. Null: nothing is caught.</summary>
        public static Func<int, string, bool> Catch;

        /// <summary>The hospital has cured what the player had. Null: nothing.</summary>
        public static Action Cure;

        /// <summary>How the player is living, from Home's needs. Null: the game's own needs alone.</summary>
        public static Func<HealthFactors, HealthFactors> Living;

        /// <summary>True if a doctor's sick note (Home's) covers a day. Null: no.</summary>
        public static Func<int, bool> DoctorNote;

        /// <summary>True if the player was ill on a day by the mod's older rules (Home's illness, Life's sickness). Null: no.</summary>
        public static Func<int, bool> WasIll;

        /// <summary>The day the illness the player has now really began, by Home's own record (Life's for a plain sickness); 0 when that is not known. Null: not known.</summary>
        public static Func<int> IllnessSince;

        // ---- the working life
        /// <summary>The plan the player's employer gives (a <see cref="Plan"/> number) and the day the job with it began. Null: none.</summary>
        public static Func<int> EmployerPlan;
        public static Func<int> EmployerSince;

        /// <summary>The minute of the day the hours of today's open shift end (a working day not yet worked or missed), or -1 when there is none. Null: none.</summary>
        public static Func<int> ShiftEnd;

        // ---- the player's firms
        /// <summary>The firm's sick pay policy: 0 none, 1 statutory, 2 generous (the Company's). Null: statutory.</summary>
        public static Func<int> SickPolicy;

        /// <summary>Whole money the wallet must keep for the investors of the player's firms: a bill is paid from the cash over it. Null: none.</summary>
        public static Func<long> Reserved;

        // ---- the city
        /// <summary>A hospital or clinic of the city: its street and number as the game writes them; false for none. Null: none.</summary>
        public static ClinicOf Clinic;

        /// <summary>A lender's civil claim for so much: the court's number, or 0 with no court. Null: no court.</summary>
        public static Func<int, int> FileClaim;

        /// <summary>The credit file is told of the medical debt: 0 opened for so much, 1 an instalment paid, 2 one missed, 3 paid off, 4 gone to court. Null: nothing.</summary>
        public static Action<int, int> Credit;

        // ---- the rivals
        /// <summary>A rival tycoon's worth in whole money, or -1 while they are not forward or are out. Null: -1.</summary>
        public static Func<int, long> RivalWorth;

        /// <summary>A rival pays a hospital's bill from the worth over their floor; returns what really left them. Null: nothing.</summary>
        public static Func<int, long, long> RivalPay;

        /// <summary>A rival is off until a day: no move of theirs before it, and their fortune grows at half the pace. Null: nothing.</summary>
        public static Action<int, int> RivalRest;

        public static Func<int, string> RivalName;

        public delegate bool ClinicOf(out string street, out int number);

        public static void Reset()
        {
            Illness = null;
            Treated = null;
            Catch = null;
            Cure = null;
            Living = null;
            DoctorNote = null;
            WasIll = null;
            IllnessSince = null;
            EmployerPlan = null;
            EmployerSince = null;
            ShiftEnd = null;
            SickPolicy = null;
            Reserved = null;
            Clinic = null;
            FileClaim = null;
            Credit = null;
            RivalWorth = null;
            RivalPay = null;
            RivalRest = null;
            RivalName = null;
        }
    }
}
