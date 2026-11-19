namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Every figure of the flats and studios a player can buy as a home (<see cref="FlatRules"/>). Game-free. Money is whole dollars at the
    /// start's prices, a share thousandths unless named otherwise, a window or a term days.
    /// </summary>
    public static class FlatCatalogue
    {
        public const int Kinds = 4;                                  // a studio, then one, two and three bedrooms
        public static readonly int[] KindWeight = { 3, 5, 7, 10 };   // the relative floor area of each kind: a studio is the smallest share of a building

        // How a building's units are kinds: the roll against thousandths, the poorer districts' units smaller (more studios) and the richer districts' larger.
        public const int StudioBase = 350;           // thousandths of a middling district's units that are studios
        public const int StudioPerPoint = 5;         // fewer studios for each 5 points of income over the middle (and more for each 5 under), per mille
        public const int StudioLeast = 150, StudioMost = 550;
        public static readonly int[] RestShare = { 538, 308, 154 };   // of the units that are not studios: one bedroom, two, three (350, 200 and 100 of 650)

        public const int MinUnits = 3;               // a block of fewer units is its landlord's whole: no flat of it is sold
        public const int ListWindow = 20;            // days a listing stands before the draw is made again
        public const int ListChance = 130;           // thousandths of a building's units listed in a window, in a middling district at a good condition
        public const int ListRunDown = 120;          // more for a building the owner has let run down
        public const int ListPerPoint = 5;           // more for each 5 points of income under the middle (poorer districts sell more), fewer over
        public const int ListLeast = 40, ListMost = 400;
        public const int ListedMost = 3;             // flats of one building for sale at once
        public const int HeldMost = 6;               // flats one player holds, so the page and the save stay small

        public const int LeaseShare = 150;           // thousandths of a unit's freehold share (its floor's share of the building's value) a flat's long lease costs: the owner keeps the freehold
        public const int RoundTo = 10;               // dollars a price is named in

        public const int ServiceShare = 200;         // thousandths of the flat's market rent a month: the service charge to the building's owner
        public const int MonthsPerYear = 12;         // the year's property tax a month
        public const int TaxLeast = 1;               // dollars a month at the least

        public const int PayInFull = 100;            // the deposit percent that means cash: no mortgage
        public const int ListShareStart = 1000;      // the asking price a sale starts at, of the value

        public static int Clamp(int value, int least, int most) => value < least ? least : value > most ? most : value;
    }
}
