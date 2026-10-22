namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Every figure of the player's bank branches (Economy/OwnBank/Branch), game-free so the offline checks read the same
    /// table. Money in cents at the start's prices unless said; shares and factors in thousandths. A branch is a business
    /// type of the mod's own in a shop building: the game's own staff, hours, rent and fit-out; what its customers do is
    /// booked in the player's bank (OwnBankRules.Visit), and what it is busy with brings the bank more of the city's people.
    /// The offline check "bank branch: a branch pays" holds a mid-sized branch to covering its rent and wages from its fees
    /// and the bank's extra margin within <see cref="PaybackMonthsMost"/> months of its fit-out.
    /// </summary>
    public static class BranchCatalogue
    {
        // The business
        public const string TypeName = "reallife_bankbranch";   // its own business type (the name is also its text key; its designs' folder is "bankbranch")
        public const string BuildingType = "ba:buildingtype_retail";
        public const int BranchesMost = 16;                      // branches whose figures a bank keeps

        // Visitors
        public const int VisitorShare = 180;        // a branch's visitors an hour, of the customers a shop of its size would see
        public static readonly int[] HourWeight =   // when people come to a bank (thousandths of an average hour), midnight first
        {
            100, 50, 50, 50, 50, 100, 300, 600, 1000, 1300, 1300, 1400, 1700, 1600, 1300, 1200, 1300, 1500, 1300, 900, 600, 400, 200, 150
        };
        public const int SatisfactionWeight = 300;  // how far the game's customer satisfaction of the building moves it (at 0 or 100)
        public const int ServePerTeller = 6;        // visits one teller sees to in an hour: ten minutes each
        public const int SkillLeast = 700, SkillMost = 1300;   // what a teller gets through at a customer service skill of 0 and of 100
        public const int PlayerSkill = 50;          // the player at the counter works as a teller of middling skill

        // The floor while the player is in a branch (Branch/BranchFloor)
        public const int ShownMost = 8;             // customers of an hour shown coming in at most
        public const int OnFloorMost = 3;           // ...and on the floor at once (the mod's people to spare)
        public const int ServeSeconds = 20;         // seconds a customer stands at the counter being seen to
        public const int WaitSeconds = 25;          // seconds a customer nobody sees to waits before going

        // What a visit is for
        public const int Open = 0, Deposit = 1, Withdraw = 2, Loan = 3, Enquiry = 4, Kinds = 5;
        public static readonly int[] KindShare = { 120, 360, 320, 100, 100 };        // of the visits seen to, thousandths
        public static readonly long[] KindCents = { 40000L, 35000L, 30000L, 600000L, 0L };   // the usual sum: $400 to open, $350 in, $300 out, a $6,000 loan
        public const int SumLeast = 400, SumMost = 1800;     // each visit's sum: this spread of the usual, thousandths
        public static readonly int[] AskBy = { 150, 400, 450 };   // who asks for a loan at the counter: poorly, fairly and well rated (OwnBankCatalogue.Poor, Fair, Good)

        // What the bank charges at the counter
        public const long OpenFeeCents = 2500L;     // an account opened: $25
        public const long CounterFeeCents = 150L;   // cash paid in or taken out at the counter: $1.50
        public const int LoanFee = 15;              // a loan arranged: 1.5% of it...
        public const long LoanFeeLeastCents = 5000L;   // ...and never under $50

        // What branches do for the bank: its hold of the city's people grows with the visits its branches saw to in the last
        // week, towards ReachMost, half of it at ReachHalf visits; a second branch adds less than the first.
        public const int WeekDays = 7;
        public const int ReachMost = 300;           // thousandths more of the city's people the bank holds at the most
        public const int ReachHalf = 2000;          // visits seen to in a week, in all branches, at which it holds half of that
        public const int FirmsReach = 400;          // of the reach, what a business's banking gets: firms bank with managers, not at a counter

        // The offline check of a branch that pays
        public const int PaybackMonthsMost = 12;    // game months a mid-sized branch takes to pay back its fit-out at most
    }
}
