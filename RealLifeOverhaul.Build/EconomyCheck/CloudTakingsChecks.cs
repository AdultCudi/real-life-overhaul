using System;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Tech;

// A data centre's cloud takings in the company's figures (Tech/TechDirector.Infra.cs, Tech/Infra/InfraRules.cs, Wiring/Wiring.CompanyBooks.cs): the cloud is booked once, at the
// home centre's address, which the game's own list of businesses leaves out; the company's takings by sector, the day's takings on the market's tape and the company's books
// all ask for the centres through one place and count them as software; the home centre's thirty days hold the cloud's takings; a full connection says its figures.
// Each source check is also run on a made-up wrong case, which must fail.
internal static partial class Program
{
    private static void CloudTakings()
    {
        CloudRing();
        CloudTrade();
        CloudSources();
    }

    private static void CloudRing()
    {
        var d = new DataCentre();
        InfraRules.Roll(d, 100, 500);
        InfraRules.RollCloud(d, 100, 700);
        Check(d.Takings30 == 1200 && d.Days[100 % FirmsCatalogue.MonthDays] == 1200, "cloud takings: the home centre's day holds its racks let and the cloud's together");
        InfraRules.RollCloud(d, 100, 0);
        InfraRules.RollCloud(d, 100, -5);
        InfraRules.RollCloud(null, 100, 9);
        Check(d.Takings30 == 1200, "cloud takings: no takings, a loss or no centre adds nothing");
        // sixty days of both: the month's total is always the sum of its thirty days, and a day that comes round again is replaced, not added to
        var e = new DataCentre();
        for (int day = 0; day < 60; day++)
        {
            InfraRules.Roll(e, day, 100 + day);
            InfraRules.RollCloud(e, day, 1000 + day * 2);
        }
        long sum = 0, want = 0;
        for (int i = 0; i < e.Days.Length; i++)
        {
            sum += e.Days[i];
        }
        for (int day = 30; day < 60; day++)
        {
            want += 100 + day + 1000 + day * 2;
        }
        Check(sum == e.Takings30 && sum == want, "cloud takings: over sixty days the thirty days' total is the last thirty days' racks and cloud, once each (" + e.Takings30 + " of " + want + ")");
    }

    private static void CloudTrade()
    {
        int software = MarketInputs.SoftwareTrade, warehouse = MarketInputs.TradeOf("ba:businesstype_warehouse");
        Check(software >= 0 && MarketInputs.Trades[software] == "webdevelopment" && MarketInputs.TradeOf("reallife:businesstype_softwarestudio") == software,
            "cloud takings: software is the trade the tech sector's firms count as");
        Check(ListingSector.SectorOf(software) == MarketSector.Software, "cloud takings: the software trade is filed under the software sector");
        Check(warehouse >= 0 && ListingSector.SectorOf(warehouse) != MarketSector.Software, "cloud takings: a warehouse's own trade is no software (why a centre is given the tech sector's)");
        // a company with one shop and a cloud: the cloud's takings lead the mix when they are the larger, in software, not in its building's trade
        var takings = new long[MarketInputs.Trades.Length];
        takings[MarketInputs.TradeOf("ba:businesstype_coffeeshop")] = 300000;
        takings[software] = 900000;
        int sector = ListingSector.Leading(takings, out int trade);
        Check(sector == (int)MarketSector.Software && trade == software, "cloud takings: a company earning most from its cloud is listed in software");
    }

    private static void CloudSources()
    {
        string infra = ModSource(@"Tech\TechDirector.Infra.cs"), rules = ModSource(@"Tech\Infra\InfraRules.cs");
        string books = ModSource(@"Wiring\Wiring.CompanyBooks.cs"), listing = ModSource(@"Wiring\ListingLinks.cs"), market = ModSource(@"Wiring\MarketLinks.cs");
        string cloud = Slice(infra, "private void Cloud(int day, int cycle", "\n        /// <summary>The site reliability");
        Check(Occurs(cloud, "FirmsGame.Takings(") == 1 && cloud.IndexOf("InfraRules.RollCloud(home, day, takings)", StringComparison.Ordinal) > cloud.IndexOf("FirmsGame.Takings(", StringComparison.Ordinal),
            "cloud takings: the cloud's day is booked once, at the home centre, and then rolled into that centre's thirty days");
        Check(!Slice(infra, "private void InfraDay(int day)", "private readonly List<ColoNote>").Contains("RollCloud"), "cloud takings: the centres' own loop rolls racks let only, the cloud adds its own");
        Check(CloudRouted(books, listing, market), "cloud takings: the company's takings by trade, the market's day and the company's books all take the centres from CompanyBooks");
        Check(!CloudRouted(books, listing.Replace("CompanyBooks.Businesses(Businesses);", "Books.Businesses(Businesses);"), market), "cloud takings: a books list without the centres is caught (wrong case)");
        Check(!CloudRouted(books, listing, market.Replace("CompanyBooks.TradeOf(r)", "MarketInputs.TradeOf(r.businessTypeName)")), "cloud takings: a centre counted in its building's trade is caught (wrong case)");
        Check(books.Contains("TechDirector.Instance?.AddCentres(into)") && books.Contains("IsCentre(reg) ? MarketInputs.SoftwareTrade"), "cloud takings: the centres join the businesses and are counted as software");
        Check(infra.Contains("internal void AddCentres(List<BuildingRegistration> into)") && infra.Contains("!into.Contains(reg)"), "cloud takings: a centre already in the list is not added twice");
        string buy = Slice(infra, "internal bool BuyRacks(", "internal bool ReplaceRacks(");
        Check(buy.Contains("\"use\"") && buy.Contains("\"cap\"") && buy.Contains("InfraLimits.Of(d, plan)"), "cloud takings: a full connection's refusal carries what it carries and what it holds");
        Check(rules.Contains("d.Days[slot] += takings;") && rules.Contains("d.Takings30 += takings;"), "cloud takings: the cloud's takings are added to a day's own place, so the month's total stays the sum of its days");
    }

    // The three places that sum the company's statements read the centres through CompanyBooks, and none reads the game's list alone.
    private static bool CloudRouted(string books, string listing, string market)
    {
        string trades = Slice(listing, "private static void TradeTakings(", "\n        }\n    }\n}");
        string mine = Slice(listing, "private static void Mine()", "private static OwnBooks Statements(");
        string day = Slice(market, "private static void Trades(ref MarketEnv e)", "// The day's reports become yesterday's");
        return books.Length > 0 && Routed(mine, false) && Routed(trades, true) && Routed(day, true);
    }

    // A member asks for the businesses through CompanyBooks (and, where it counts trades, the trade) and never for the game's list alone.
    private static bool Routed(string member, bool trade)
    {
        const string list = "Books.Businesses(Businesses)";
        string alone = member.Replace("CompanyBooks." + "Businesses(Businesses)", string.Empty);
        return member.Contains("CompanyBooks.Businesses(Businesses)") && !alone.Contains(list) && (!trade || (member.Contains("CompanyBooks.TradeOf(r)") && !member.Contains("MarketInputs.TradeOf(r")));
    }

    private static string Slice(string code, string from, string to)
    {
        code = code.Replace("\r\n", "\n");   // the working copy may hold CRLF
        int at = code.IndexOf(from, StringComparison.Ordinal);
        if (at < 0)
        {
            return string.Empty;
        }
        int end = code.IndexOf(to, at + from.Length, StringComparison.Ordinal);
        return code.Substring(at, (end < 0 ? code.Length : end) - at);
    }
}
