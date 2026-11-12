namespace RealLifeOverhaul.Health
{
    /// <summary>A plan as a page offers it: its month's premium for the player today.</summary>
    public struct PlanFact
    {
        public Plan Plan;
        public int Premium;
    }

    /// <summary>
    /// What the Health pages read, filled by the director from the state and the game in one call (HealthDirector.Facts). Game-free, so the samples and the checks build
    /// the same pages from made-up facts.
    /// </summary>
    public sealed class HealthFacts
    {
        public bool Ready;
        public bool Session;             // a Going Public session: the city's people and the rivals rest
        public bool People;              // the living population is there
        public int Level = 1000, Day, YearDays = 60, MonthDays = 5;
        public long Cash, Spare;

        // the player
        public Condition Kind;
        public bool Serious, Treated, AtWork;
        public int DaysLeft;             // an injury's days still to run; 0 for an illness (Home says)
        public int Neglect;
        public bool InHospital;
        public int StayUntilDay;
        public int Wave;                 // thousandths of an average wave's top
        public int IllAtHome;
        public int Lifestyle = 1000;     // thousandths

        // the doctor
        public bool Booked;
        public int BookedDay, BookedHour;
        public string Clinic = string.Empty;
        public bool CanBook;

        // insurance
        public Plan Plan;
        public int PolicySince, NextDue, Missed, Premium, WaitUntil;
        public long PlanPaidYear;
        public Plan Employer;
        public readonly PlanFact[] Plans = { new PlanFact { Plan = Plan.Basic }, new PlanFact { Plan = Plan.Standard }, new PlanFact { Plan = Plan.Full } };
        public int Age, Claims;

        // bills and leave
        public long BillsYear, ClaimsYear, LastBill, LastClaim;
        public int LastBillDay;
        public long Owed, DebtPaid;
        public int DebtPerDay, DebtLate;
        public bool ShiftOpen, CalledIn;
        public int PaidUsed, PaidDays, SpellDays, NoteUntil, CalledWell, Noticed;
        public long SickPay;

        // the city
        public int IllNow, CityPeople, Cases, SeriousCases, LostDays, StaffOff;
        public long CityBills, CityDebt;
        public int SickPolicy = 1;
        public string RivalLine = string.Empty;
    }
}
