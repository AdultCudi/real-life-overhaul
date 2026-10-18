namespace RealLifeOverhaul.Family
{
    /// <summary>Every figure of family life, at today's prices; affection and harmony are points of a hundred. Nothing here is read per frame.</summary>
    public static class FamilyBalance
    {
        // The couple: the default bridge's figures (Social has its own).
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

        // The home.
        public const int SqmForTwo = 30;                // square metres of floor the home must have (0 = unknown: allowed)
        public const int SqmPerChild = 15;              // square metres
        public const float HarmonyStart = 70f;
        public const float HarmonyChore = 4f;
        public const float HarmonyRoutine = 5f;
        public const float HarmonyDate = 8f;
        public const float HarmonyBrokenPromise = -8f;
        public const float HarmonyArrest = -22f;        // at severity 1; times 0.6 + 0.4 x severity
        public const float HarmonyCounselling = 15f;    // the first session; less after
        public const float HarmonyDriftPerDay = -0.6f;  // with nothing shared that day
        public const float HarmonyBreak = 15f;          // this low for BreakDays and they move out
        public const int BreakDays = 5;
        public const float HarmonyReconcile = 55f;
        public const float RentShare = 0.5f;            // of the day's rent, at most
        public const float IncomeShare = 0.35f;         // of their day's pay, at most
        public static int BillsCap => Prices.At(60);                 // a day, in a home that is owned (no rent)
        public static int ContributionCap => Prices.At(600);         // a day, whatever the rent

        // Outings.
        public static int DinnerCost => Prices.At(90);
        public const int DinnerHours = 2;
        public static int CounsellingCost => Prices.At(180);

        // Engagement and wedding.
        public static readonly int[] RingPrice = { 0, 1200, 4500, 12000, 30000 };   // by tier, 1 to 4
        public static int Ring(int tier) => Prices.At(RingPrice[tier]);
        public static readonly float[] RingOdds = { 0f, 0.00f, 0.08f, 0.15f, 0.22f };   // added to the chance of a yes
        public static readonly int[] VenuePrice = { 500, 1500, 6000, 12000 };      // city hall, a place outdoors, restaurant, theatre
        public static int Venue(int venue) => Prices.At(VenuePrice[venue]);
        public static readonly float[] TierFactor = { 1f, 1.8f, 3.2f };            // simple, classic, grand
        public static int FlowersPrice => Prices.At(800);
        public static int MusicPrice => Prices.At(1200);
        public const int CateringPerGuest = 85;
        public const float PlannerFee = 0.15f;          // of the whole, for an event planning agency
        public const float Deposit = 0.25f;             // of the whole, paid when the plan is made, kept if it is called off
        public const float PostponeFee = 0.10f;         // of the whole
        public const int WeddingFromHour = 11;
        public const int WeddingToHour = 20;
        public static int GiftPerGuest => Prices.At(140);            // on average, times the tier
        public static int HoneymoonCost => Prices.At(2500);
        public const int HoneymoonDays = 3;
        public const int HoneymoonWindowDays = 60;      // after the wedding, while it can still be asked for from the Family contact

        // Children.
        public const int MaxChildren = 3;
        public const float PregnancyYears = 0.75f;      // of a child's year: 21 days at the default scale
        public const float AdoptionYears = 0.6f;        // of a child's year
        public static int AdoptionFee => Prices.At(1500);
        public static int CotPrice => Prices.At(450);
        public static int PushchairPrice => Prices.At(380);
        public static int SuppliesPrice => Prices.At(220);
        public static int ScanPrice => Prices.At(120);
        public static int BirthPrice => Prices.At(1800);
        public static int CheckupPrice => Prices.At(80);
        // Childcare and school by the day, paid weekly and shown by the month of the player's calendar: the year's length changes nothing.
        public static int NurseryPerDay => Prices.At(32);            // a child
        public static int NannyPerDay => Prices.At(79);              // the household
        public static int PrivateSchoolPerDay => Prices.At(54);      // a child
        public static int NurseryPerMonth => GameCalendar.Monthly(NurseryPerDay);
        public static int NannyPerMonth => GameCalendar.Monthly(NannyPerDay);
        public static int PrivateSchoolPerMonth => GameCalendar.Monthly(PrivateSchoolPerDay);
        public static int PartyHomePrice => Prices.At(150);
        public static int PartyVenuePrice => Prices.At(600);
        public static readonly int[] PocketMoney = { 0, 10, 25, 50 };   // a week
        public const int FundStep = 500;
        public const int FundFull = 40000;
        public const float PushchairUntil = 3f;         // years
        public const float CotUntil = 2f;               // years
        public const float BabyUntil = 1f;              // years: a swaddled baby lies in the cot until then
        public const float HighChairFrom = 0.5f, HighChairUntil = 3f;   // years
        public const float SchoolFrom = 5f;             // years
        public const float PocketFrom = 6f;             // years
        public const float GrownUp = 18f;               // years
        public const float WalkMetres = 220f;           // a pushchair walk

        // Hard times.
        public const float SettlementShare = 0.20f;     // of cash in hand, never of a business
        public static int SettlementCap => Prices.At(250000);
        public const int DivorceAfterDays = 7;          // separated this long before either may file
        public const int PartnerFilesAfterDays = 28;    // with no word at all
        public const float SupportShare = 0.5f;         // of the children's costs, after a divorce
    }
}
