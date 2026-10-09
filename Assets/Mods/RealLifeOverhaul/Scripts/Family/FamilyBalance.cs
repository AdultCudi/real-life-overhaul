namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// Every figure of family life in one place: what things cost, how long they take, what a
    /// relationship needs before the next step. Money is set against the game's own economy
    /// (a cashier earns 25 an hour, a starter flat rents for a few hundred a day). Nothing here
    /// is read per frame.
    /// </summary>
    public static class FamilyBalance
    {
        // ---------------------------------------------------------------- the couple (default bridge figures; Social has its own)
        public const float AffectionStart = 35f;
        public const float AffectionChat = 3f;          // a call or a chat, once a day
        public const float AffectionDate = 9f;
        public const float AffectionNeglectPerDay = 1.5f;   // after NeglectDays without a word
        public const int NeglectDays = 4;
        public const float MoveInAffection = 60f;
        public const int MoveInDaysTogether = 7;
        public const float ProposeAffection = 75f;
        public const int ProposeDaysTogether = 14;
        public const int ProposeAgainDays = 7;
        public const int MeetAgainDays = 14;            // after a divorce or a parting, before the default bridge introduces someone new

        // ---------------------------------------------------------------- the home
        public const int SqmForTwo = 30;                // the flat must have this much floor (0 = unknown: allowed)
        public const int SqmPerChild = 15;
        public const float HarmonyStart = 70f;
        public const float HarmonyChore = 4f;
        public const float HarmonyRoutine = 5f;
        public const float HarmonyDate = 8f;
        public const float HarmonyArgueGood = 6f;
        public const float HarmonyArgueBad = -9f;
        public const float HarmonyBrokenPromise = -8f;
        public const float HarmonyArrest = -22f;
        public const float HarmonyCounselling = 15f;
        public const float HarmonyDriftPerDay = -0.6f;  // with nothing shared that day
        public const float HarmonyTalkLow = 40f;        // counselling is offered below this
        public const float HarmonyBreak = 15f;          // this low for BreakDays and they move out
        public const int BreakDays = 5;
        public const float HarmonyReconcile = 55f;
        public const float RentShare = 0.5f;            // of the day's rent, at most
        public const float IncomeShare = 0.35f;         // of their day's pay, at most
        public const int BillsCap = 60;                 // a day, in a home that is owned (no rent)
        public const int ContributionCap = 600;         // a day, whatever the rent

        // ---------------------------------------------------------------- outings
        public const int DinnerCost = 90;
        public const int DinnerHours = 2;
        public const int CounsellingCost = 180;

        // ---------------------------------------------------------------- engagement and wedding
        public static readonly int[] RingPrice = { 0, 1200, 4500, 12000, 30000 };
        public static readonly float[] RingOdds = { 0f, 0.00f, 0.08f, 0.15f, 0.22f };
        public const int EngagementMinDays = 7;
        public static readonly int[] VenuePrice = { 500, 1500, 6000, 12000 };      // city hall, a place outdoors, restaurant, theatre
        public static readonly float[] TierFactor = { 1f, 1.8f, 3.2f };            // simple, classic, grand
        public const int FlowersPrice = 800;
        public const int MusicPrice = 1200;
        public const int CateringPerGuest = 85;
        public const float PlannerFee = 0.15f;          // of the whole, for an event planning agency
        public const float Deposit = 0.25f;             // paid when the plan is made, kept if it is called off
        public const float PostponeFee = 0.10f;
        public const int WeddingFromHour = 11;
        public const int WeddingToHour = 20;
        public const int GiftPerGuest = 140;            // on average, times the tier
        public const int HoneymoonCost = 2500;
        public const int HoneymoonDays = 3;

        // ---------------------------------------------------------------- children
        public const int MaxChildren = 3;
        public const int WeeksPerChildYearDefault = 4;  // the options slider: one year of a child's age per so many in-game weeks
        public const float PregnancyYears = 0.75f;      // of a child's year: 21 days at the default scale
        public const float AdoptionYears = 0.6f;
        public const int AdoptionFee = 1500;
        public const int CotPrice = 450;
        public const int PushchairPrice = 380;
        public const int SuppliesPrice = 220;
        public const int ScanPrice = 120;
        public const int BirthPrice = 1800;
        public const int CheckupPrice = 80;
        public const int NurseryPerMonth = 900;
        public const int NannyPerMonth = 2200;
        public const int PrivateSchoolPerMonth = 1500;
        public const int PartyHomePrice = 150;
        public const int PartyVenuePrice = 600;
        public static readonly int[] PocketMoney = { 0, 10, 25, 50 };   // a week
        public const int FundStep = 500;
        public const int FundFull = 40000;
        public const float PushchairUntil = 3f;         // years
        public const float CotUntil = 2f;
        public const float BabyUntil = 1f;              // a swaddled baby lies in the cot until then (the modelled `baby_bundle`)
        public const float HighChairFrom = 0.5f, HighChairUntil = 3f;
        public const float SchoolFrom = 5f;
        public const float PocketFrom = 6f;
        public const float GrownUp = 18f;
        public const float WalkMetres = 220f;           // a pushchair walk
        public const int MonthDays = 28;

        // ---------------------------------------------------------------- hard times
        public const float SettlementShare = 0.20f;     // of cash in hand, never of a business
        public const int SettlementCap = 250000;
        public const int DivorceAfterDays = 7;          // separated this long before either may file
        public const int PartnerFilesAfterDays = 28;    // with no word at all
        public const float SupportShare = 0.5f;         // of the children's costs, after a divorce
    }
}
