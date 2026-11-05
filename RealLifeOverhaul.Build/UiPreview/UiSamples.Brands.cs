using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Brands app's samples: the facts each page reads, invented but shaped as the game's are (a coffee brand with company shops and franchised ones, an applicant, an agreement with
// a rival's outlet inside its exclusive area). Names are placeholders and never ship. Every word goes through the text source, so the checks measure the longest translation.
internal static partial class UiSamples
{
    public static readonly string[] BrandsStates =
    {
        "brand", "brand_settings", "brand_campaign", "shops", "shop_plain", "shop_chain", "applicants", "applicant", "agreements", "agreement", "agreement_rebate", "empty", "off"
    };

    /// <summary>A recording stand-in for the director: each press says which action it was.</summary>
    public sealed class BraStub : IBraActions
    {
        public string Last;
        public int Count;

        private void Did(string what)
        {
            Last = what;
            Count++;
        }

        public void Rename(int brandId) => Did("rename");
        public void Minimum(int brandId, int step) => Did("minimum");
        public void Offer(int brandId, bool on) => Did("offer");
        public void KeepPrices(int brandId, bool on) => Did("keep");
        public void KeepLocal(int brandId, bool on) => Did("local");
        public void SharePrices(int brandId) => Did("share_prices");
        public void ShareHours(int brandId) => Did("share_hours");
        public void GiveUp(int brandId) => Did("give_up");
        public void BookPush(int brandId, int kind, int districtIndex) => Did("push");
        public void SellBrand(int franchiseId) => Did("sell");
        public void RefuseBid(int franchiseId) => Did("refuse_bid");
        public void Locate(string place) => Did("locate");
        public void Leave(string place) => Did("leave");
        public void MakeBrand(string place) => Did("make");
        public void JoinBrand(int brandId, string place) => Did("join");
        public void TakeChain(string place, int chain) => Did("chain");
        public void LeaveChain(string place) => Did("leave_chain");
        public void KeepChain(string place, bool on) => Did("keep_chain");
        public void ChainPrices(string place) => Did("chain_prices");
        public void Sign(int applicantId, int site, int fee, int royalty, int levy, bool exclusive, int length) => Did("sign");
        public void Refuse(int applicantId) => Did("refuse");
        public void Inspect(int franchiseId) => Did("inspect");
        public void Warn(int franchiseId) => Did("warn");
        public void Retrain(int franchiseId) => Did("retrain");
        public void Agree(int franchiseId) => Did("agree");
        public void Waive(int franchiseId) => Did("waive");
        public void Decline(int franchiseId) => Did("decline");
        public void End(int franchiseId) => Did("end");
    }

    public static VentModel Brands(UiWords t, string state, VentView view = null, IBraActions act = null)
    {
        VentWords w = VentWordsOf(t);
        BraFacts f = BrandsFacts(state);
        view = view ?? new VentView();
        switch (state)
        {
            case "brand_settings": view.Page = BraPages.PageSettings; break;
            case "brand_campaign": view.Page = BraPages.PageCampaign; break;
            case "shops": view.Tab = BraPages.TabShops; break;
            case "shop_plain": view.Tab = BraPages.TabShops; view.Pick = BraPages.ShopKey + "22 Elm Street"; break;
            case "shop_chain": view.Tab = BraPages.TabShops; view.Pick = BraPages.ShopKey + "301 Lexington Avenue"; break;
            case "applicants": view.Tab = BraPages.TabApplicants; break;
            case "applicant": view.Tab = BraPages.TabApplicants; view.Pick = BraPages.ApplicantKey + 5; break;
            case "agreements": view.Tab = BraPages.TabAgreements; break;
            case "agreement": view.Tab = BraPages.TabAgreements; view.Pick = BraPages.FranchiseKey + 9; break;
            case "agreement_rebate": view.Tab = BraPages.TabAgreements; view.Pick = BraPages.FranchiseKey + 11; break;
        }
        var m = new VentModel();
        BraPages.Fill(m, view, f, act ?? new BraStub(), w);
        return m;
    }

    private static BraFacts BrandsFacts(string state)
    {
        var f = new BraFacts { Day = 402, WaitingAnswers = 2 };
        if (state == "off")
        {
            f.Ready = false;
            return f;
        }
        if (state == "empty")
        {
            f.Shops = new[] { new BraShopFact { Key = "shop:22 Elm Street", Place = "22 Elm Street", Name = "22 Elm Street", Owner = "You", Mine = true, Kind = "plain", Status = "no brand", District = "Midtown", SalesDay = 860 } };
            f.ChainNames = new[] { "Bean Counter", "Corner Daily", "Gold Crust" };
            f.ChainFees = new long[] { 12000, 20000, 30000 };
            return f;
        }
        var districts = new[]
        {
            new BraDistrictFact { Name = "Midtown", Rec = 6300, Shops = 3 }, new BraDistrictFact { Name = "Garment District", Rec = 4800, Shops = 2 },
            new BraDistrictFact { Name = "Hell's Kitchen", Rec = 3100, Shops = 1 }, new BraDistrictFact { Name = "Murray Hill", Rec = 2200, Shops = 0 }
        };
        var weeks = new[] { "W1", "W2", "W3", "W4", "W5", "W6", "W7", "W8", "W9", "W10", "W11", "W12" };
        var brand = new BraBrandFact
        {
            Id = 1, Name = "Brewed Beans", Type = "Coffee shop", Founded = "Day 31", Standards = 78, Minimum = 60, MinimumStep = 2, MinimumSteps = 4, Shops = 9, Members = 4, Franchised = 5, Rec = 6300,
            City = 4100, Value = 2400000, RivalName = "Huang Guo", RivalValue = 3100000, Rating = 9, SalesYesterday = 13700, FranchiseSales = 4500, Royalty = 272, Net = 190, Earned = 41500, Fund = 2100,
            FeesMonth = 8160, Offered = true, CanOffer = true, Keep = false, Local = false, Pushing = true, PushLine = "Local campaign, Midtown, 6 days left", FlagshipName = "88 Broadway",
            OfferLine = "Offered as a franchise: applicants come while its recognition and standards hold up.", WeekNames = weeks,
            Mine = new long[] { 4800, 4900, 5100, 5000, 5300, 5500, 5400, 5700, 5800, 6000, 6200, 6300 },
            Rival = new long[] { 6600, 6700, 6700, 6800, 6900, 6900, 7000, 7000, 7100, 7000, 7100, 7100 }, Districts = districts,
            PushKinds = new[] { "Local campaign", "City-wide campaign", "Creators on Flare" }, PushDistricts = new[] { "Midtown", "Garment District", "Hell's Kitchen" },
            PushCosts = new long[] { 7200, 11400, 9800 }, PushDays = new[] { 7, 14, 14 }, BestShop = "540 9th Avenue"
        };
        f.Brands = new[] { brand };
        f.ChainNames = new[] { "Bean Counter", "Corner Daily", "Gold Crust" };
        f.ChainFees = new long[] { 12000, 20000, 30000 };
        f.JoinBrands = new[] { "Brewed Beans" };
        f.JoinBrandIds = new[] { 1 };
        f.Shops = new[]
        {
            new BraShopFact { Key = "shop:88 Broadway", Place = "88 Broadway", Name = "88 Broadway", Owner = "You", Mine = true, Kind = "member", Brand = "Brewed Beans", District = "Midtown", SalesDay = 1420, Rating = 9, Standards = 82, Status = "member" },
            new BraShopFact { Key = "shop:22 Elm Street", Place = "22 Elm Street", Name = "22 Elm Street", Owner = "You", Mine = true, Kind = "plain", District = "Midtown", SalesDay = 860, Rating = 7, Status = "no brand",
                Line = "A coffee shop in Midtown that is under no brand and no chain." },
            new BraShopFact { Key = "shop:301 Lexington Avenue", Place = "301 Lexington Avenue", Name = "301 Lexington Avenue", Owner = "You", Mine = true, Kind = "chain", Brand = "Bean Counter", District = "Murray Hill",
                SalesDay = 1310, Rating = 8, Standards = 64, Status = "chain", ChainExit = 6000, CanKeep = true },
            new BraShopFact { Key = "fr:9", Place = "12 W 38th Street", Name = "Brewed Beans, Sofia Marin", Owner = "Sofia Marin", Kind = "franchise", FranchiseId = 9, Brand = "Brewed Beans", District = "Garment District",
                SalesDay = 1180, Rating = -1, Standards = 71, Status = "in order" },
            new BraShopFact { Key = "fr:11", Place = "540 9th Avenue", Name = "Brewed Beans, Marcus Webb Holdings", Owner = "Marcus Webb", Kind = "franchise", FranchiseId = 11, Brand = "Brewed Beans", District = "Hell's Kitchen",
                SalesDay = 860, Status = "disputes the terms", Standards = 55 }
        };
        f.Applicants = new[]
        {
            new BraApplicantFact
            {
                Id = 5, Name = "Hana Okada", Brand = "Brewed Beans", District = "Uptown", Temper = "Careful", TemperAbout = "Keeps to the standards and asks for little.", Wants = "Wants to open a Brewed Beans in Uptown. Waits until day 408.",
                Capital = 120000, FitOut = 40000, Experience = 6, ShopsRun = 2, ShopsClosed = 0, DaysLeft = 6, Sites = new[] { "1020 Madison Avenue, traffic 82", "14 E 86th Street, traffic 64" },
                Lengths = new[] { "3 years", "5 years", "8 years" }, Fees = new[] { "$6,000", "$12,000", "$20,000" }, Royalties = new[] { "4%", "6%", "8%" }, Levies = new[] { "0%", "1%", "2%" },
                Accepts = true, Answer = "She would sign. The terms weigh 14 against the 22 she bears."
            },
            new BraApplicantFact
            {
                Id = 6, Name = "Leo Park", Brand = "Brewed Beans", District = "Midtown", Temper = "Corner cutting", TemperAbout = "Pays little attention to standards.", Wants = "Wants a second shop. Waits until day 405.",
                Capital = 80000, Experience = 3, DaysLeft = 3, Second = true, Sites = new string[0], Lengths = new[] { "3 years", "5 years", "8 years" }, Fees = new[] { "$6,000", "$12,000", "$20,000" },
                Royalties = new[] { "4%", "6%", "8%" }, Levies = new[] { "0%", "1%", "2%" }, Accepts = false, Answer = "He would not sign: the royalty is more than the shop can bear."
            }
        };
        f.Franchises = new[]
        {
            new BraFranchiseFact
            {
                Id = 9, Name = "Brewed Beans, Sofia Marin", Franchisee = "Sofia Marin", Brand = "Brewed Beans", District = "Garment District", Place = "12 W 38th Street", Status = "in order", Until = "Day 1480",
                Takings = 1180, Royalty = 94, Paid = 18900, EntryFee = 12000, RoyaltyBp = 600, LevyBp = 100, Seen = 71, Minimum = 60, Exclusive = true, Terms = "$12,000, 6%, 1%", Target = 33000, Overs = 7,
                ExclusiveLine = "600 m round the shop", StandardsLine = "Last inspected at 71 points, against a minimum of 60.", OnTime = 12, Months = 12, CanAgree = false, EndCost = 14000
            },
            new BraFranchiseFact
            {
                Id = 11, Name = "Brewed Beans, Marcus Webb Holdings", Franchisee = "Marcus Webb", Brand = "Brewed Beans", District = "Hell's Kitchen", Place = "540 9th Avenue", Status = "disputes the terms",
                Until = "Day 1330", Takings = 860, Royalty = 68, Paid = 9400, EntryFee = 12000, RoyaltyBp = 800, LevyBp = 200, Seen = 55, Minimum = 60, Exclusive = true, Terms = "$12,000, 8%, 2%", Target = 31000,
                Overs = -17, ExclusiveLine = "600 m round the shop", StandardsLine = "Last inspected at 55 points, against a minimum of 60.", OnTime = 9, Months = 12, Ask = 4, CanAgree = true,
                CanDecline = true, EndCost = 0, Cause = true, Trouble = false, Warnings = 1,
                AskLine = state == "agreement_rebate"
                    ? "A rival opened a Coffee shop in Hell's Kitchen, inside Marcus Webb's exclusive area. They claim $3,060 back by day 412."
                    : "They dispute the terms and claim $3,060 back by day 412."
            }
        };
        f.IntroText = null;
        return f;
    }
}
