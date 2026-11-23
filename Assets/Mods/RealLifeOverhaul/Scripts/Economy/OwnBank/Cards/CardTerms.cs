namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank's credit card business (Economy/OwnBank/Cards), game-free so the offline checks and the Guide read one table. Cards go to the bank's
    /// own clients, a kind of client at a time (the five kinds of borrower), on rules the owner sets: the limit by kind, the rate, the yearly fee and
    /// the credit scores that are approved at once, queued or declined. A card earns interest on the part of its balance that is carried and the
    /// shops' interchange on what is spent, and loses to defaults by the same risk model as a loan (OwnBankRules.DefaultPerMille) and to fraud.
    /// Money in cents at the start's prices unless said; shares and factors in thousandths; rates in hundredths of a per cent.
    /// </summary>
    public static class CardTerms
    {
        public const int Kinds = OwnBankCatalogue.Segments;
        public static readonly string[] TierKeys =
        {
            "reallife_ob_card_tier_0", "reallife_ob_card_tier_1", "reallife_ob_card_tier_2", "reallife_ob_card_tier_3", "reallife_ob_card_tier_4"
        };
        public const string BlackKey = "reallife_ob_card_black";             // the owner's own card: the bank's top tier name

        // The limit a card of each kind is given, the owner's choice of four steps (cents at the start's prices).
        public const int LimitSteps = 4, LimitDefault = 1;
        public static readonly long[][] Limits =
        {
            new[] { 20000L, 30000L, 50000L, 75000L },                         // $200 to $750: the poorly rated are lent small sums
            new[] { 50000L, 100000L, 150000L, 250000L },
            new[] { 200000L, 400000L, 700000L, 1200000L },
            new[] { 250000L, 500000L, 1000000L, 2000000L },
            new[] { 1000000L, 2500000L, 5000000L, 10000000L }
        };

        // What a card does in a day, by kind.
        public static readonly long[] SpendDay = { 1200L, 3000L, 7500L, 22000L, 90000L };   // spent on one card, cents a day: a small shop's till sees more than a person's
        public static readonly int[] Util = { 600, 400, 280, 300, 250 };      // the balance carried, thousandths of the limit: the poorly rated live on their cards
        public static readonly int[] Revolve = { 800, 600, 400, 350, 250 };   // of the balance, the part that is not paid off in the grace days and so bears interest
        public const int UtilFollow = 250;                    // a month's way to the balance wanted, thousandths
        public const int InterchangeBp = 150;                                 // what a shop pays the card's bank on a sale: about 1.5%
        public static readonly int[] Rewards = { 0, 0, 60, 80, 100 };        // cashback and points, hundredths of a per cent of the spend: only the upper kinds are courted
        public const int FraudBp = 10;                                        // spend lost to fraud and chargebacks, hundredths of a per cent
        public const long CardDayCents = 3L;                                  // processing, statements and customer service for one card a day
        public const long FixedDayCents = 8000L;                              // the card business's systems and its processor's contract a day: $80

        // Who wants the bank's card. The bank's clients are its savers; a part of each kind of client wants a card, more when it is cheap and light.
        public static readonly int[] TakeUp = { 300, 400, 450, 400, 350 };    // of the clients of a kind who take the bank's card at the rivals' price, thousandths
        public static readonly int[] KindShare = { 150, 450, 400, 700, 300 }; // the share of the people (the first three) and of the firms (the last two) who are of each kind
        public static readonly long[] SaverCents = { 250000L, 4000000L };     // what a client of the bank holds on average: $2,500 a person, $40,000 a firm
        public const int Pull = 300;                                          // e to minus this many thousandths of the take-up for each point dearer than the rivals
        public const int FeePull = 4;                                         // thousandths of the take-up lost for each dollar of the yearly fee
        public const int Follow = 250;                                        // a month's way to the cards wanted, thousandths
        public const int CardPremium = 600;                                   // a card costs this much more a year than a loan at the same rating: it has no security

        // The rate the owner sets, over the rivals' card rate, and the yearly fee.
        public const int RateLeast = -500, RateMost = 800, RateStep = 50;
        public static readonly long[] FeeSteps = { 0L, 2000L, 5000L, 10000L };   // a year's fee: none, $20, $50, $100
        public const int FeeDefault = 0;

        // Approval by the applicant's credit score: at once from one line, queued between the two, declined under the other.
        public const int AutoLeast = 520, AutoMost = 820, DeclineLeast = 420, LineStep = 20;
        public const int AutoDefault = 720, DeclineDefault = 600;
        public static readonly int[] KindScore = { 500, 620, 740, 660, 780 };   // the typical credit score of a kind's applicants: the poorly rated are not priced at nothing here
        public const int ScoreSd = 70;                                        // the spread of applicants' scores in a kind, points
        public const int ScoreScale = 80;                                     // points of score that make the defaults e times as many: a looser card brings worse clients
        public const int QueueFade = 100;                                     // thousandths of a queue that goes elsewhere each day
        public const int QueueMost = 2000;                                    // applicants waiting at most in a kind

        // Risk.
        public static readonly int[] DefaultMult = { 1500, 1500, 1500, 1300, 1200 };   // a card has no security: defaults against a loan of the same kind, thousandths
        public static readonly int[] CureShare = { 100, 150, 200, 150, 200 };          // of what a month settles, the part put right; the rest is written off
        public const int CardWeight = 1500;                                   // the regulator's weight on a card balance, thousandths: as an unsecured loan to the poorly rated
        public const int WindCards = 700, WindCardArrears = 150;              // what a bank taken by the regulator fetches for its card book, thousandths

        // The balance a kind carries in a slump and in a boom, and what is spent.
        public static readonly int[] UtilCycle = { 1050, 1000, 1000, 1050 };  // people lean on cards a little more when money is short
        public static readonly int[] SpendCycle = { 1100, 1000, 900, 800 };

        // The owner's own card (the bank's top tier).
        public const int BlackMostShare = 100;                                // its limit is at most this share of the capital: no single client is more than a tenth of it
        public const long BlackLeastCents = 2000000L;                         // $20,000
        public const long BlackStepCents = 500000L;                           // limits go in steps of $5,000
        public const int BlackDiscountBp = 300;                               // three points under the best card's rate: the top tier's privilege
        public const int BlackWeight = 1500;

        // This month's figures kept for the page.
        public const int Figures = 7;
        public const int FigInterest = 0, FigInterchange = 1, FigFees = 2, FigLost = 3, FigFraud = 4, FigCosts = 5, FigSpend = 6;

        public static int KindOf(int kind) => kind < 0 ? 0 : kind >= Kinds ? Kinds - 1 : kind;

        public static long LimitCents(int kind, int step, int level)
        {
            int k = KindOf(kind), s = step < 0 ? 0 : step >= LimitSteps ? LimitSteps - 1 : step;
            return OwnBankRules.At(Limits[k][s], level);
        }

        public static long FeeCents(int step, int level)
        {
            int s = step < 0 ? 0 : step >= FeeSteps.Length ? FeeSteps.Length - 1 : step;
            return OwnBankRules.At(FeeSteps[s], level);
        }
    }
}
