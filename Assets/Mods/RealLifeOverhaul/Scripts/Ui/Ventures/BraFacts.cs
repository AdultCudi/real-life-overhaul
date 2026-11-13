namespace RealLifeOverhaul.Ui
{
    /// <summary>A district a brand stands in: its recognition there and its shops.</summary>
    public struct BraDistrictFact
    {
        public string Name;
        public int Rec, Shops;      // hundredths of a point
    }

    /// <summary>A brand of the player's as the Brands tab shows it.</summary>
    public sealed class BraBrandFact
    {
        public int Id;
        public string Name, Type, Founded;
        public int Standards, Minimum, MinimumStep, MinimumSteps;
        public int Shops, Members, Franchised;
        public int Rec, City;                    // the best district's and the city's recognition, hundredths
        public long Value, RivalValue;
        public string RivalName;
        public int Rating = -1;                  // halves of five stars, or -1 with no shop to read
        public long SalesYesterday, FranchiseSales, Royalty, Net, Earned, Fund, FeesMonth;
        public bool Offered, CanOffer, Keep, Local, Pushing;
        public string OfferLine, PushLine, BidLine, FlagshipName;
        public long BidAmount;
        public int BidFranchise;
        public string[] WeekNames = new string[0];
        public long[] Mine = new long[0], Rival = new long[0];          // recognition in hundredths by week
        public BraDistrictFact[] Districts = new BraDistrictFact[0];
        public string[] PushKinds = new string[0];                      // the campaign kinds that can be booked: local, city, creators
        public string[] PushDistricts = new string[0];
        public long[] PushCosts = new long[0];                          // by kind
        public int[] PushDays = new int[0];
        public string BestShop;                                         // the shop furthest under the standards
    }

    /// <summary>A shop of the player's, a franchised shop or one under a chain: a row of the Shops tab.</summary>
    public sealed class BraShopFact
    {
        public string Key, Place, Name, Brand, District, Owner, Status, Kind;   // Kind: member, franchise, chain, plain
        public int FranchiseId, Standards = -1, Rating = -1;
        public long SalesDay;
        public bool Mine;
        public string Line;
        public long ChainExit;
        public bool Kept, CanKeep;
        public int Breach;
        public string KeptNote, ManagerNote;   // how a kept price stands (the band it is kept at), and that a manager of the game's watches the district and would undo it
    }

    /// <summary>Somebody who wants to open a shop under a brand.</summary>
    public sealed class BraApplicantFact
    {
        public int Id;
        public string Name, Brand, District, Temper, TemperAbout, Wants;
        public long Capital, FitOut;
        public int Experience, ShopsRun, ShopsClosed, DaysLeft, Known, Bears;
        public bool Second;
        public string[] Sites = new string[0];
        public string[] Lengths = new string[0], Fees = new string[0], Royalties = new string[0], Levies = new string[0];
        public string Answer;                    // what they say to the terms now
        public bool Accepts;
        public string Burden, Support;
    }

    /// <summary>A franchise agreement and its shop.</summary>
    public sealed class BraFranchiseFact
    {
        public int Id;
        public string Name, Franchisee, Brand, District, Place, Status, Until;
        public long Takings, Royalty, Paid, EntryFee, Target;
        public int RoyaltyBp, LevyBp, Seen = -1, Minimum, Warnings, YearsLeft;
        public bool Exclusive, Trouble, Inside, WaivedNow;
        public string ExclusiveLine, StandardsLine, AskLine, RebateLine, Terms, Waived, CourtLine, EndLine;
        public int Ask;                           // 0 none, 1 help, 2 second shop, 3 renewal, 4 dispute, 5 bid
        public bool CanAgree, CanWaive, CanDecline, Court;
        public long EndCost;
        public bool Cause;
        public int OnTime, Months;                // royalties paid on time of the months on record
        public int OwnInside;
        public int Overs;                         // sales against the target, per cent over (negative: under)
        public string[] Events = new string[0];
    }

    /// <summary>Everything the Brands pages read, plain.</summary>
    public sealed class BraFacts
    {
        public bool Ready = true;
        public string OffTitle, OffBody;
        public int Day, WaitingAnswers;
        public BraBrandFact[] Brands = new BraBrandFact[0];
        public BraShopFact[] Shops = new BraShopFact[0];
        public BraApplicantFact[] Applicants = new BraApplicantFact[0];
        public BraFranchiseFact[] Franchises = new BraFranchiseFact[0];
        public string[] ChainNames = new string[0];
        public long[] ChainFees = new long[0];
        public string[] JoinBrands = new string[0];
        public int[] JoinBrandIds = new int[0];
        public string IntroText;
    }

    /// <summary>What a button on the Brands pages does in the game.</summary>
    public interface IBraActions
    {
        void Rename(int brandId);
        void Minimum(int brandId, int step);
        void Offer(int brandId, bool on);
        void KeepPrices(int brandId, bool on);
        void KeepLocal(int brandId, bool on);
        void SharePrices(int brandId);
        void ShareHours(int brandId);
        void GiveUp(int brandId);
        void BookPush(int brandId, int kind, int districtIndex);
        void SellBrand(int franchiseId);
        void RefuseBid(int franchiseId);
        void Locate(string place);
        void Leave(string place);
        void MakeBrand(string place);
        void JoinBrand(int brandId, string place);
        void TakeChain(string place, int chain);
        void LeaveChain(string place);
        void KeepChain(string place, bool on);
        void ChainPrices(string place);
        void Sign(int applicantId, int site, int fee, int royalty, int levy, bool exclusive, int length);
        void Refuse(int applicantId);
        void Inspect(int franchiseId);
        void Warn(int franchiseId);
        void Retrain(int franchiseId);
        void Agree(int franchiseId);
        void Waive(int franchiseId);
        void Decline(int franchiseId);
        void End(int franchiseId);
    }
}
