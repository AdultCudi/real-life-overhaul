namespace RealLifeOverhaul.Health
{
    /// <summary>What ails somebody. The first five are Home's own illnesses in Home's order (Home/HomeState.Illness), so a number means the same in both; the rest are injuries.</summary>
    public enum Condition : byte
    {
        None = 0,
        Cold = 1,
        Flu = 2,
        FoodPoisoning = 3,
        BackPain = 4,
        Exhaustion = 5,   // Home's burnout
        Sprain = 6,
        BrokenArm = 7,
        Concussion = 8,
        Count = 9
    }

    /// <summary>A health insurance plan. Saved as a number.</summary>
    public enum Plan : byte
    {
        None = 0,
        Basic = 1,      // hospital stays only
        Standard = 2,   // hospital stays, half of the doctor and the prescription
        Full = 3,       // nearly everything
        Count = 4
    }

    /// <summary>What a bill is for, as a plan reads it.</summary>
    public enum Care : byte
    {
        Doctor = 0,     // a consultation or a prescription
        Hospital = 1    // a stay
    }

    /// <summary>
    /// Every figure of Health, each with its unit and where it comes from. Money is whole money at the start's prices and follows the city's prices (PriceRules.At); chances
    /// of the population are in a hundred thousand a person and day. Game-free.
    /// </summary>
    public static class HealthCatalogue
    {
        // ---- the course of each condition, in days: the least and the most (Home's own illnesses run by Home's hours for the player; these are the people's)
        public static readonly int[] CourseLeast = { 0, 2, 3, 1, 2, 3, 2, 6, 3 };
        public static readonly int[] CourseMost = { 0, 3, 5, 2, 3, 5, 4, 10, 5 };
        public const int CourseLongest = 10;           // days: the longest of CourseMost, the ring of who is ill
        public static readonly bool[] Infectious = { false, true, true, false, false, false, false, false, false };   // passed on at home and at work
        public static readonly bool[] CanTurn = { false, false, true, true, false, true, false, false, false };       // turns serious if neglected
        public static readonly bool[] Injury = { false, false, false, false, false, false, true, true, true };
        public static readonly bool[] SeriousAtOnce = { false, false, false, false, false, false, false, true, true };   // needs the hospital from the first hour
        public static readonly int[] StayDays = { 0, 1, 2, 1, 1, 2, 1, 1, 2 };   // days in hospital when it is serious
        public static readonly int[] HealDays = { 0, 0, 0, 0, 0, 0, 0, 6, 2 };   // days of healing at home after the hospital (a cast, rest)

        public static readonly string[] NameKeys =
        {
            "reallife_hc_cond_none", "reallife_hc_cond_cold", "reallife_hc_cond_flu", "reallife_hc_cond_food", "reallife_hc_cond_back", "reallife_hc_cond_exhaustion",
            "reallife_hc_cond_sprain", "reallife_hc_cond_arm", "reallife_hc_cond_concussion"
        };

        // ---- the city's people: a person's chance a day, in a hundred thousand, by season (winter, spring, summer, autumn: CalendarRules)
        public static readonly int[] ColdADay = { 300, 180, 90, 200 };   // two to three colds a year for an adult
        public const int FluADay = 40;                  // out of season
        public const int FoodADay = 30;
        public const int BackADay = 50;                 // adults
        public const int ExhaustionADay = 25;           // workers on 45 hours a week or more
        public const int SprainADay = 25;
        public const int ArmADay = 4;
        public const int LongHours = 45;                // hours a week from which exhaustion is drawn
        public const int ChildAge = 12, ElderAge = 65;  // years
        public const int ChildColdShare = 1500;         // thousandths: children catch more colds
        public const int ElderFluShare = 1400;          // thousandths: the old catch the flu more
        public const int HouseContact = 6000;           // a hundred thousand: a housemate fell ill with something catching yesterday or the day before
        public const int SeriousFlu = 40, SeriousFood = 30, SeriousExhaustion = 50;   // thousandths of cases that need the hospital
        public const int ElderSeriousShare = 3000;      // thousandths: three times as often past ElderAge

        // ---- the flu's wave: one a year, in midwinter, bounded
        public const int WaveYearShare = 120;           // thousandths of the year the wave lasts (a 60-day year: 7 days; 365: 44)
        public const int WaveDaysLeast = 6;
        public const int WavePeak = 600;                // a hundred thousand a day at the top of an average wave
        public const int WaveSeverityLeast = 600, WaveSeverityMost = 1400;   // thousandths of the peak, drawn for each year
        public const int WaveShareMost = 50;            // thousandths of the people ill at once, the bound the checks hold the wave to

        // ---- the player: what the wave and the people at home add to Home's own causes
        public const int PlayerWavePeak = 1500;         // a hundred thousand a day at the top of an average wave: Home's own flu chance (HomeBalance.FluADay, 1.5%)
        public const int PlayerContact = 4000;          // a hundred thousand a day for each person ill at home
        public const int PlayerContactMost = 2;         // people counted
        public const int LifestyleLeast = 500, LifestyleMost = 3000;   // thousandths: rested, fed and fit halves it; run down it is three times
        public const int SafeDays = 4;                  // nothing is caught before this day of a game (Home's rule)

        // ---- neglect and turning serious
        public const int NeglectDays = 3;               // untreated days without rest before it can turn
        public const int NeglectCertain = 6;            // from here it turns
        public const int TurnChance = 350;              // thousandths a neglected day between the two
        public const int RestDebt = 8;                  // hours of sleep owed at most for a day to count as rest (Home's DebtHigh)
        public const int RestEnergy = 25, RestHunger = 20;   // the game's own needs at least
        public const float SeriousDrain = 0.03f;        // energy a game minute while serious (Life's sick drain again)

        // ---- injuries
        public const int MinorHurtHours = 24;           // LifeEvents.Injure from this many hours is a sprain
        public const int MajorHurtHours = 48;           // ...and from this many a broken arm or a concussion
        public const int WorkAccidentSome = 400, WorkAccidentHigh = 1200;   // a hundred thousand a shift worked at a risky trade
        public const int WorkAccidentTired = 2;         // times, worked with the day's rest missing
        public const int WorkArmShare = 200;            // thousandths of work accidents that break an arm

        // ---- care and its prices
        public const int DoctorFee = 80;                // Home's consultation (HomeBalance.ConsultationFee)
        public const int PrescriptionFee = 45;          // Home's prescription
        public const int Admission = 800;               // a hospital stay's first charge
        public const int StayDay = 900;                 // ...and each day of it
        public const int GameStayBill = 2000;           // the game's own night after a collapse (GameManager.HospitalRespawn), not moved by prices
        public const int StayMost = 5;                  // days: no stay is longer
        public const int NoteAfterStay = 2;             // days a hospital's note covers after the discharge
        public const int MissedFee = 25;                // an appointment not kept
        public const int AppointmentEarly = 20, AppointmentLate = 45;   // minutes round the hour it may be kept in
        public const int AppointmentWait = 5;           // minutes in the waiting room with an appointment
        public const int AppointmentHour = 9, AppointmentLastHour = 16;

        // ---- insurance: by plan (none, basic, standard, full)
        public static readonly int[] PremiumMonth = { 0, 60, 140, 290 };
        public static readonly int[] HospitalShare = { 0, 700, 850, 1000 };   // thousandths of a stay's bill over the excess
        public static readonly int[] Excess = { 0, 500, 250, 100 };           // of each stay
        public static readonly int[] DoctorShare = { 0, 0, 500, 800 };        // thousandths of a consultation or a prescription
        public static readonly int[] YearCap = { 0, 20000, 60000, 200000 };   // the most a plan pays in a year
        public static readonly string[] PlanKeys = { "reallife_hc_plan_none", "reallife_hc_plan_basic", "reallife_hc_plan_standard", "reallife_hc_plan_full" };
        public const int WaitDays = 3;                  // nothing that begins in a policy's first days is covered
        public const int LapseAfter = 2;                // premiums missed running
        public static readonly int[] AgeFrom = { 0, 30, 40, 50, 60, 70 };            // years
        public static readonly int[] AgeShare = { 800, 1000, 1200, 1500, 1900, 2400 };   // thousandths of the premium
        public const int ClaimLoading = 150;            // thousandths more for each claim of the last year
        public const int ClaimLoadingMost = 600;
        public const int ClaimsKept = 16;
        public const int EmployerWaitDays = 1;          // an employer's cover counts from the day after the job began

        // ---- medical debt
        public const int DebtMonths = 2;                // paid off over this many months, with no interest
        public const int DebtLeastADay = 10;
        public const int DebtClaimAfter = 10;           // days running with nothing taken before the claim goes to court

        // ---- sick leave at the player's job
        public const int SelfCertDays = 2;              // working days running without a doctor's note
        public const int SpellGap = 3;                  // days between two sick working days of one spell
        public const int PaidYearShare = 30;            // thousandths of the year paid sick days are
        public const int PaidDaysLeast = 2;
        public const int QualifyDays = 3;               // in the job before sick pay
        public const int NoticeFirst = 200, NoticeStep = 200, NoticeMost = 800;   // thousandths: a day called in sick while well is seen through

        // ---- the player's own staff: only the wave and colleagues add to the game's own sick days
        public const int StaffWavePeak = 1000;          // a hundred thousand a day at the top of an average wave
        public const int StaffContact = 1500;           // for each colleague off at the same place
        public const int StaffContactMost = 3;
        public const int StaffPlanCut = 150;            // thousandths less for each step of the game's health plan (bronze, silver, gold)
        public const int StaffCasesMost = 128;
        public const int StaffCourseMost = 3;           // days

        // ---- the city's people: money
        public const int NpcSickPay = 500;              // thousandths of a lost day's pay an employer still pays (the Company's statutory policy)
        public const int LostDaysMost = 5;
        public static readonly int[] InsuredShare = { 700, 880, 970 };   // thousandths of households with a plan, by class, when nobody has a full job
        public const int FullJobHours = 30;             // a week: a job that brings the employer's plan
        public const int NpcDebtMost = 6000;            // the most one case puts on a household's debt
        public const int PassSlice = 12000;             // people looked at in a second of the day's pass

        // ---- the rivals
        public const int RivalADay = 120;               // a hundred thousand
        public const int RivalWavePeak = 600;
        public const int RivalSerious = 80;             // thousandths
        public const int RivalDaysLeast = 2, RivalDaysMost = 5;
        public const int PaperKept = 3;                 // days a note waits for the paper

        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        public static long Clamp(long v, long lo, long hi) => v < lo ? lo : v > hi ? hi : v;

        public static bool Is(bool[] table, Condition c) => (int)c > 0 && (int)c < table.Length && table[(int)c];

        public static string NameKey(Condition c) => NameKeys[Clamp((int)c, 0, NameKeys.Length - 1)];
    }
}
