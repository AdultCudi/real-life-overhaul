namespace RealLifeOverhaul.Tech
{
    /// <summary>What a reaction of a rival is, saved as a number: never renumber.</summary>
    public static class ReactKind
    {
        public const int Cut = 1, Raised = 2, Copying = 3, Copied = 4, Chest = 5, Funded = 6, Poach = 7, Backed = 8, Entered = 9, Bought = 10, Count = 11;

        /// <summary>Whole text keys, one for each kind: the short line the firm's page says.</summary>
        public static readonly string[] LineKeys =
        {
            "reallife_rival_line_none", "reallife_rival_line_cut", "reallife_rival_line_raised", "reallife_rival_line_copying", "reallife_rival_line_copied",
            "reallife_rival_line_chest", "reallife_rival_line_funded", "reallife_rival_line_poach", "reallife_rival_line_backed", "reallife_rival_line_entered",
            "reallife_rival_line_bought"
        };

        public static string LineKey(int kind) => LineKeys[kind < 0 || kind >= LineKeys.Length ? 0 : kind];
    }

    /// <summary>How a bake off stands, saved as a number: never renumber.</summary>
    public static class BakeState
    {
        public const int Open = 0, Won = 1, Lost = 2, Missed = 3;
    }

    /// <summary>
    /// The figures of competition in niches (the plan's section 7). Every one is a rate or a share of what the sector, the venture part or the catalogues already hold; none is a price.
    /// Game-free.
    /// </summary>
    public static class RivalryCatalogue
    {
        public const string Key = "reallife:tech_rivals";

        // ---------------------------------------------------------------- one pool of buyers for a niche
        public const int RampDays = 30;              // days from the day a save is loaded over which a niche's own pool takes the place of the market's share of it

        // ---------------------------------------------------------------- the reactions
        public const int LogMost = 30;               // reactions kept for the firms' pages

        public const int CopyFocus = 600;            // thousandths of a rival's points that go to copying a feature the player led with (its usual build keeps 250)
        public const int CopyLeast = 7, CopyMost = 90;   // days a copy takes at the least and the most
        public const int CopyGap = 14;               // days after a copy before the rival looks at the player's lead again

        public const int ChestGap = 90;              // days since its last round before a rival brings the next one forward
        public const int ChestChance = 600;          // per mille a day that a rival in the player's niche brings its round forward after the player's raise
        public const int ChestBring = 3;             // days from the call to the round closing (the round rules close it ten days after it starts raising)
        public const int ChestMinShare = 300;        // thousandths of the stage's usual round that the player's raise must be to count as a raise
        public const int ChestDays = 60;             // days a rival's war chest is spent over
        public const int ChestSpend = 250;           // thousandths of the round that goes on reach in those days

        public const int FundedRunway = 120;         // days of money that make a startup funded
        public const int PoachFunded = 1500;         // thousandths: the pull of a funded rival in the player's niche (a company's lead gives the same)
        public const int PoachBacked = 1800;         // ...of one a listed company stands behind, or a listed company that came in
        public const int PoachNoteGap = 30;          // days between two lines about the same rival hiring

        public const int BackMarketing = 500;        // thousandths of its buyer's marketing a bought rival spends
        public const int BackPoints = 250;           // thousandths of its buyer's team that works for a bought rival

        public const int BigTimes = 1500;            // thousandths of the average niche of its kind that a niche must hold to have got big
        public const int EnterChance = 5;            // per mille a day that a listed company comes into a big niche
        public const int BuyChance = 4;              // per mille a day that a listed company buys a rival startup in a big niche
        public const int EntrantsMost = 2;           // listed companies that come into one niche

        // ---------------------------------------------------------------- bake offs
        public const int BakeEvery = 40;             // days between one niche's bake offs, on average
        public const int BakeDays = 10;              // days a bake off is open
        public const int BakeMonths = 6;             // months of its subscription a big customer's contract is worth
        public const int BakeSeatsBusinesses = 24, BakeSeatsEnterprises = 3;   // sites the customer signs for at the least (it asks for up to twice as many)
        public const int BakeSelling = 300;          // thousandths a product's score rises with Selling at 100
        public const int BakeFit = 250;              // thousandths a product's score moves with its fit (500 is the bar's middle)
        public const int BakeDemo = 200, BakeRelations = 200, BakePrice = 150;   // thousandths: a demo (Product sense), the relationships, a sharper price
        public const int BakePriceCut = 100;         // thousandths off the contract that a sharper price costs
        public const int BakeJitter = 100;           // thousandths each rival's score moves on the day, seeded
        public const int BakesMost = 24;             // bake offs kept
    }
}
