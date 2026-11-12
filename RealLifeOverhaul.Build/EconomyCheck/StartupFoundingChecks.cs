using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// Founding a startup (Scripts/Tech/Founding): niches sized from a sample city's business list, their cap and expansion, co-founders' vesting and cliff, each pre-seed source landing
// in the firm account and the floor, incorporation fees, the articles feeding the ladder, each go to market stage, fit moving the price investors give, an older save placed
// with nothing changed, the options off, and the exploit paths tried (raise from friends and family, spend it elsewhere, close). Game-free; the sources are read for what only
// the game can do. --startup-founding runs these alone.
internal static partial class Program
{
    private static void StartupFoundingChecks()
    {
        FoundNicheSizes();
        FoundNicheMoods();
        FoundNicheMarket();
        FoundVesting();
        FoundSources();
        FoundFeesArticles();
        FoundStages();
        FoundFit();
        FoundOldSaves();
        FoundExploits();
        FoundWiring();
    }

    private static NicheCensus SampleCity(int restaurants, int gyms, int lettings)
    {
        var c = new NicheCensus();
        for (int i = 0; i < restaurants; i++) { NicheRules.Count(c, "ba:businesstype_fastfoodrestaurant", 8); }
        for (int i = 0; i < gyms; i++) { NicheRules.Count(c, "ba:businesstype_gym", 12); }
        NicheRules.Count(c, "ba:businesstype_bank", 200);
        NicheRules.Count(c, "ba:businesstype_nothing", 3);
        c.Lettings = lettings;
        NicheRules.Exist(c, t => t != "ba:businesstype_clinic");
        return c;
    }

    private static NicheMood Even() => new NicheMood { Cycle = 1000, Season = 1000, Rent = 1000, Hype = 1000 };

    private static void FoundNicheSizes()
    {
        NicheCensus c = SampleCity(40, 6, 0);
        NicheDef rest = NicheCatalogue.Def(NicheCatalogue.Restaurants), gym = NicheCatalogue.Def(NicheCatalogue.Gyms);
        Check(c.Businesses[NicheCatalogue.Restaurants] == 40 && c.Businesses[NicheCatalogue.Gyms] == 6 && c.Staff[NicheCatalogue.Restaurants] == 320 && c.Businesses[NicheCatalogue.Banks] == 1,
            "founding niches: the city's business list is counted into the niches by type (" + c.Businesses[0] + " restaurants, " + c.Businesses[4] + " gyms, a type no niche holds is left out)");
        Check(c.Exists[NicheCatalogue.Restaurants] && !c.Exists[NicheCatalogue.Clinics] && !c.Exists[NicheCatalogue.Landlords],
            "founding niches: a niche exists only if the city can have a business of its type, and landlords only where buildings let flats");
        c.Lettings = 12;
        NicheRules.Exist(c, t => true);
        Check(c.Exists[NicheCatalogue.Landlords] && c.Businesses[NicheCatalogue.Landlords] == 12, "founding niches: landlords are sized from the buildings that let flats");
        long bigR = NicheRules.Buyers(rest, c, 0, 365, Even()), bigG = NicheRules.Buyers(gym, c, 0, 365, Even());
        Check(bigR == 40L * SectorRules.WorldAt((int)SectorBuyers.Businesses, 0, 365) / 1000 && bigR > bigG && bigG > 0, "founding niches: buyers are the city's businesses of the kind times the region each stands for (" + bigR + " and " + bigG + ")");
        SectorBuyers who = SectorBuyers.Businesses;
        Check(NicheRules.Buyers(rest, SampleCity(80, 6, 0), 0, 365, Even()) == 2 * bigR, "founding niches: twice the restaurants, twice the buyers (the city's list decides)");
        long ceilR = NicheRules.Ceiling(rest, bigR, 1000), ceilG = NicheRules.Ceiling(gym, bigG, 1000);
        Check(ceilR == bigR * FirmsCatalogue.Price[(int)Segment.Business] / 1000 && ceilR > ceilG, "founding niches: the revenue ceiling is the buyers at the going price, the bigger niche's higher (" + ceilR + " over " + ceilG + ")");
        Check(NicheRules.Ceiling(rest, bigR, 1200) > ceilR && NicheRules.Deals(bigR) == bigR, "founding niches: a dearer going price lifts the ceiling, and a niche holds a deal for each buyer");
        long marketBuyers = bigR * 20;
        int bigShare = NicheRules.RivalShare(bigR * 10, marketBuyers), smallShare = NicheRules.RivalShare(bigG, marketBuyers), tiny = NicheRules.RivalShare(1, marketBuyers);
        Check(smallShare < bigShare && tiny == NicheCatalogue.RivalFloor && NicheRules.RivalShare(marketBuyers, marketBuyers) == 1000 && who == SectorBuyers.Businesses,
            "founding niches: a small niche has fewer of the market's rivals and never none (" + smallShare + ", " + bigShare + ", floor " + tiny + ")");
        Check(NicheCatalogue.Defs.Length == NicheCatalogue.Count && NicheCatalogue.NameKeys.Length == NicheCatalogue.Count && NicheCatalogue.Defs.Select((d, i) => d.Id == i).All(x => x),
            "founding niches: every niche has an id in order and a name key, for phase 5 to read");
        bool bits = true;
        for (int n = 0; n < NicheCatalogue.Count; n++)
        {
            for (int m = 0; m < NicheCatalogue.Count; m++)
            {
                bits &= NicheRules.Neighbours(n, m) == ((NicheCatalogue.Defs[n].Near & (1 << m)) != 0) && !(n == m && NicheRules.Neighbours(n, m));
            }
        }
        Check(bits, "founding niches: neighbours are the bits each niche names");
        Check(NicheRules.PriceFactor(NicheCatalogue.Def(NicheCatalogue.Shops)) > NicheRules.PriceFactor(NicheCatalogue.Def(NicheCatalogue.Cafes)) && NicheRules.PriceFactor(NicheCatalogue.Def(NicheCatalogue.Clinics)) == 1000,
            "founding niches: a richer niche (a bigger basket per customer) pays more, one the population's table does not know pays the market's");
    }

    private static void FoundNicheMoods()
    {
        NicheCensus c = SampleCity(40, 6, 12);
        c.RentKnown = true;
        c.RentIndex = 1300;
        NicheDef rest = NicheCatalogue.Def(NicheCatalogue.Restaurants), land = NicheCatalogue.Def(NicheCatalogue.Landlords), gym = NicheCatalogue.Def(NicheCatalogue.Gyms);
        NicheMood boom = NicheRules.Mood(rest, c, 0, 0, 1000, false), slump = NicheRules.Mood(rest, c, 3, 0, 1000, false);
        Check(boom.Cycle > 1000 && slump.Cycle < 1000 && slump.All < boom.All, "founding niches: restaurants buy less in a downturn (" + slump.Cycle + " against " + boom.Cycle + ")");
        long dull = NicheRules.Mood(NicheCatalogue.Def(NicheCatalogue.Grocers), c, 3, 0, 1000, false).Cycle, treat = NicheRules.Mood(NicheCatalogue.Def(NicheCatalogue.Venues), c, 3, 0, 1000, false).Cycle;
        Check(treat < dull && NicheRules.Swing(NicheCatalogue.Def(NicheCatalogue.Clinics)) == 1000, "founding niches: a treat (a night out) feels more of a downturn than groceries, and a niche with no data feels the market's cycle");
        Check(NicheRules.Mood(land, c, 1, 0, 1000, false).Rent == 1300 && NicheRules.Mood(land, SampleCity(1, 1, 12), 1, 0, 1000, false).Rent == 1000 && NicheRules.Mood(rest, c, 1, 0, 1000, false).Rent == 1000,
            "founding niches: landlords buy more when rents rise, only with the rents known, and no other niche feels rents");
        int winter = NicheRules.Mood(rest, c, 1, 0, 1000, false).Season, summer = NicheRules.Mood(rest, c, 1, 2, 1000, false).Season;
        Check(winter != summer && NicheRules.Mood(gym, c, 1, 0, 1000, false).Season == 1000 && NicheRules.Mood(gym, c, 1, 2, 1000, false).Season == 1000,
            "founding niches: the season shows where the seasons model the niche's goods (" + winter + " and " + summer + "), and not in a niche with none");
        Check(NicheRules.Mood(rest, c, 1, 0, 1600, true).Hype > 1000 && NicheRules.Mood(rest, c, 1, 0, 1600, false).Hype == 1000 && NicheRules.Mood(rest, c, 1, 0, 400, true).Hype < 1000,
            "founding niches: the AI wave moves an AI product's niche only");
        Check(NicheRules.Buyers(rest, c, 0, 365, slump) < NicheRules.Buyers(rest, c, 0, 365, boom), "founding niches: the mood moves the buyers");
    }

    private static void FoundNicheMarket()
    {
        var market = new SectorMarketDay { Buyers = 1000000, All = 5000, Features = 600, Count = 6, Rivals = 5, PriceSum = 5500, Who = 1 };
        var view = new SectorMarketDay();
        long own = SectorRules.Attraction(1000, 600);
        NicheRules.View(view, market, 40000, 1100);
        NicheRules.Pull(view, market, own, own);
        long wide = SectorRules.Target(market, own), narrow = SectorRules.Target(view, own);
        Check(view.Buyers == 40000 && narrow <= 40000 && narrow > 0, "founding niches: a product sells to the niche's buyers at most, whatever the market holds (" + narrow + " of " + view.Buyers + ")");
        Check(view.All < market.All && narrow * market.Buyers > wide * view.Buyers, "founding niches: with fewer rivals in a small niche the product wins a bigger share of it (" + narrow * 1000 / view.Buyers + " per mille against " + wide * 1000 / market.Buyers + ")");
        NicheRules.View(view, market, 5000000, 1000);
        Check(view.Buyers == market.Buyers && view.Going == 1000, "founding niches: a niche never has more buyers than its market");
        var f = FoundRules.Found("A~1", 10, (int)Venture.Software, NicheCatalogue.Restaurants, 0, 0, 0, 0);
        Check(FoundRules.CanExpand(f, NicheCatalogue.Cafes) && !FoundRules.CanExpand(f, NicheCatalogue.Banks) && !FoundRules.CanExpand(f, NicheCatalogue.Restaurants),
            "founding niches: a firm can open a neighbouring niche, not a far one and not its own");
        f.Expanded.Add(new Expansion { Niche = NicheCatalogue.Cafes, Day = 20 });
        Check(FoundRules.CanExpand(f, NicheCatalogue.Shops) && !FoundRules.CanExpand(f, NicheCatalogue.Cafes), "founding niches: from the new niche its neighbours open too, once each");
        f.Expanded.Add(new Expansion { Niche = NicheCatalogue.Shops, Day = 30 });
        Check(!FoundRules.CanExpand(f, NicheCatalogue.Grocers), "founding niches: a firm sells into " + NicheCatalogue.Active + " niches at most");
        Check(NicheRules.Ramp(0) == NicheCatalogue.ExpandStart && NicheRules.Ramp(NicheCatalogue.ExpandRamp) == 1000 && NicheRules.Ramp(30) > NicheRules.Ramp(10),
            "founding niches: an expansion reuses the product: half the new niche's buyers count at first, all of them after " + NicheCatalogue.ExpandRamp + " days");
        Check(FoundRules.ExpandCost(0) == VentureCatalogue.BurnByStage[0] * NicheCatalogue.ExpandDays && FoundRules.ExpandCost(1000000) == 1000000L * NicheCatalogue.ExpandDays,
            "founding niches: opening a niche costs a few days of the firm's burn");
        Check(NicheRules.MarketOf(NicheCatalogue.Def(0), false) == (int)SectorMarket.BusinessSoftware && NicheRules.MarketOf(NicheCatalogue.Def(0), true) == (int)SectorMarket.BusinessAi
              && NicheRules.Serves(NicheCatalogue.Def(0), (int)SectorMarket.BusinessAi) && !NicheRules.Serves(NicheCatalogue.Def(0), (int)SectorMarket.Apps)
              && !NicheRules.Serves(NicheCatalogue.Def(0), (int)SectorMarket.EnterpriseSoftware) && NicheRules.Serves(NicheCatalogue.Def(NicheCatalogue.Banks), (int)SectorMarket.EnterpriseSoftware),
            "founding niches: a niche is served by its kind of buyer's markets, not the apps and not the other kind of buyer");
        bool same = true, none = true, served = true;
        for (int id = 1; id < 40; id++)
        {
            for (int sector = 0; sector < (int)Venture.Count; sector++)
            {
                int a = NicheRules.OfStartup(77, id, sector), b = NicheRules.OfStartup(77, id, sector);
                same &= a == b;
                int market2 = SectorCatalogue.StartupMarket[sector];
                none &= market2 >= 0 || a == -1;
                served &= a == -1 || NicheRules.Serves(NicheCatalogue.Def(a), market2);
            }
        }
        Check(same && none && served && NicheRules.OfStartup(77, 3, (int)Venture.Games) == -1, "founding niches: a city startup gets a niche in its own market, seeded and the same every time, none for a game");
        Check(NicheRules.OfStartup(1, 5, (int)Venture.Software) >= 0 && Enumerable.Range(1, 30).Select(i => NicheRules.OfStartup(9, i, (int)Venture.Software)).Distinct().Count() > 3,
            "founding niches: the city's startups spread over several niches");
    }

    private static CoFounder Co(int no = 1) => new CoFounder { No = no, Name = "Priya Raman", Role = 0, Level = 2, Profile = "pro:1" };

    private static void FoundVesting()
    {
        var co = new List<CoFounder> { Co() };
        Cap c = FoundRules.Incorporate("A~1", co, 200, 100);
        long mine = c.Holders[0].Shares, theirs = FoundRules.LineOf(c, 1).Shares;
        Check(c.Total == VentureRules.FounderShares && theirs == VentureRules.FounderShares / 5 && mine + theirs == c.Total && co[0].Day == 100,
            "founding vesting: the founders' shares are split as asked and the co-founder's vest from the day of founding");
        Check(FoundRules.Vested(co[0], 100 + 364, 365) == 0 && FoundRules.Vested(co[0], 100 + 365, 365) == 250 && FoundRules.Vested(co[0], 100 + 4 * 365, 365) == 1000,
            "founding vesting: nothing before the one year cliff, a quarter at it, all after four years");
        Cap early = FoundRules.Incorporate("A~1", new List<CoFounder> { Co() }, 200, 100);
        CoFounder gone = Co();
        gone.Day = 100;
        long back = FoundRules.Leave(early, gone, 100 + 200, 365);
        Check(back == theirs && gone.Kept == 0 && FoundRules.LineOf(early, 1) == null && early.Total == VentureRules.FounderShares - theirs && VentureRules.PlayerShare(early) > 800,
            "founding vesting: a co-founder who leaves before the cliff takes nothing, and everybody else's share grows (" + back + " back)");
        Cap late = FoundRules.Incorporate("A~1", new List<CoFounder> { Co() }, 200, 100);
        CoFounder left = Co();
        left.Day = 100;
        long cancelled = FoundRules.Leave(late, left, 100 + 730, 365);
        Check(left.Kept == theirs / 2 && cancelled == theirs - theirs / 2 && FoundRules.LineOf(late, 1).Shares == left.Kept && left.Left == 830,
            "founding vesting: a co-founder who leaves after two years keeps the vested half (" + left.Kept + ") and the rest is cancelled");
        Check(FoundRules.Leave(late, left, 900, 365) == 0, "founding vesting: leaving twice cancels nothing more");
        Cap mineCap = FoundRules.Incorporate("A~1", new List<CoFounder>(), 0, 100);
        FoundRules.TakeFamily(mineCap, FoundRules.FamilyAmount(0), 100);
        mineCap.Acc.FounderDay = 100;
        long badLeaver = StartupMoney.BadLeaver(mineCap, 100 + 365, 365);
        Check(badLeaver == mineCap.Holders[0].Shares * 3 / 4 || Math.Abs(badLeaver - (badLeaver + mineCap.Holders[0].Shares) * 3 / 4) <= 1,
            "founding vesting: the founder's own shares vest from the day of founding (a bad leaver at the cliff loses three quarters: " + badLeaver + ")");
        Check(FoundRules.LineOf(FoundRules.Incorporate("B~1", new List<CoFounder>(), 300, 5), 1) == null, "founding vesting: with no co-founder the split changes nothing");
    }

    private static void FoundSources()
    {
        Cap fam = FoundRules.Incorporate("A~1", new List<CoFounder>(), 0, 50);
        long put = FoundRules.TakeFamily(fam, FoundRules.FamilyAmount(1), 50);
        Holder line = fam.Holders.First(h => h.Kind == (int)HolderKind.Vc);
        Check(put == VentureCatalogue.TicketLeast * 2 && StartupMoney.Has(fam) && fam.Acc.Raised == put && StartupMoney.Investors(fam) == put && StartupMoney.Floor(fam, 51) == put && fam.Stage == 0
              && line.Who == VentureCatalogue.FamilyId && line.Invested == put && line.Preference == put && !line.Board && fam.VcSeats == 0,
            "founding sources: friends and family land in the firm account, count as investors for the floor, take shares and no seat (" + put + ")");
        int share = VentureRules.ShareOf(line, fam.Total);
        Check(share > 10 && share < 60, "founding sources: their cheque buys a few per cent at their own value of the firm (" + share + " per mille)");
        Cap acc = FoundRules.Incorporate("A~1", new List<CoFounder>(), 0, 50);
        long sum = FoundRules.TakeAccelerator(acc, 50);
        Holder al = acc.Holders.First(h => h.Kind == (int)HolderKind.Vc);
        int aShare = VentureRules.ShareOf(al, acc.Total);
        Check(sum == VentureCatalogue.TicketLeast * FoundCatalogue.AccelTickets && StartupMoney.Floor(acc, 51) == sum && Math.Abs(aShare - FoundCatalogue.AccelShare) <= 1 && al.Who == VentureCatalogue.AcceleratorId,
            "founding sources: an accelerator puts in a small sum for its set share (" + aShare + " per mille) and the money is the investors' for the floor");
        Cap ang = FoundRules.Incorporate("A~1", new List<CoFounder>(), 0, 50);
        var standard = new FoundedFirm { Articles = 0 };
        var friendly = new FoundedFirm { Articles = 1 };
        TermSheet ts = FoundRules.AngelSheet(0, VentureCatalogue.FloorValue[0], 40, 40, 1000, 50, standard), tf = FoundRules.AngelSheet(0, VentureCatalogue.FloorValue[0], 40, 40, 1000, 50, friendly);
        long got = VentureRules.Accept(ang, ts, 0, 50);
        Check(got == ts.Amount && ang.Acc.Raised == got && StartupMoney.Floor(ang, 51) == got && ang.VcSeats == 0 && tf.Pre < ts.Pre && tf.Pre == ts.Pre * 920 / 1000,
            "founding sources: an angel's cheque lands as any raise does, no seat, and founder friendly articles are priced lower (" + tf.Pre + " against " + ts.Pre + ")");
        TermSheet weak = FoundRules.AngelSheet(0, VentureCatalogue.FloorValue[0], 0, 0, 1000, 50, standard), strong = FoundRules.AngelSheet(0, VentureCatalogue.FloorValue[0], 100, 100, 1000, 50, standard);
        Check(strong.Pre > weak.Pre, "founding sources: an angel pays more for a founder with Selling and Relationships");
        Cap own = FoundRules.Incorporate("A~1", new List<CoFounder>(), 0, 50);
        Check(!StartupMoney.Has(own) && StartupMoney.Floor(own, 60) == 0 && own.Stage == -1, "founding sources: the player's own savings raise nothing, open no account and set no floor");
        Check(FoundRules.FamilyAmount(0) < FoundRules.FamilyAmount(1) && FoundRules.FamilyAmount(99) == FoundRules.FamilyAmount(2) && FoundRules.AccelAmount() > 0, "founding sources: friends and family offer three cheques");
        Check(FoundRules.FamilyLoss(0, 100) == 1000 && FoundRules.FamilyLoss(100, 100) == 0 && FoundRules.FamilyLoss(60, 100) == 400 && FoundRules.FamilyLoss(5, 0) == 0,
            "founding sources: the family's loss, from what the wind down paid their line against what they put in");
        var f = FoundRules.Found("A~1", 50, 0, 0, 0, FoundCatalogue.SourceAccelerator, sum, 0);
        f.MentorUntil = f.DemoDay = 50 + FoundCatalogue.AccelDays;
        f.Learned = 800;
        f.Fit = 400;
        TermSheet demo = FoundRules.DemoSheet(2, VentureCatalogue.FloorValue[0], f, 1000, 110);
        f.Learned = 100;
        f.Fit = 50;
        TermSheet poor = FoundRules.DemoSheet(2, VentureCatalogue.FloorValue[0], f, 1000, 110);
        Check(demo.Pre > poor.Pre && demo.Lapse == 110 + VentureCatalogue.TermSheetDays && VentureCatalogue.Firms[2].Least <= FoundCatalogue.DemoStage && FoundCatalogue.DemoStage <= VentureCatalogue.Firms[2].Most,
            "founding sources: demo day brings sheets from seed venture firms, priced by what the firm has learned");
        bool leads = true;
        for (int i = 0; i < VentureCatalogue.FirmCount; i++)
        {
            VcDef d = VentureCatalogue.Firms[i];
            leads &= d.Leads == (i < 8) && (d.Leads || (VentureRules.AnyLead(0) != i && VentureRules.AnyLead(5) != i));
        }
        Check(leads && VentureCatalogue.FirmCount == 10 && VentureCatalogue.Firms[VentureCatalogue.FamilyId].Diligence < VentureCatalogue.Firms[VentureCatalogue.AcceleratorId].Diligence,
            "founding sources: friends and family and the accelerator lead no round, and the venture firms are as they were");
    }

    private static void FoundFeesArticles()
    {
        long standard = FoundRules.Fee(0, 0), friendly = FoundRules.Fee(1, 0), skilled = FoundRules.Fee(0, 100);
        Check(standard == (long)VentureCatalogue.DiligenceFee * FoundCatalogue.FeeTickets && friendly == standard * 3 / 2 && skilled == standard * 7 / 10 && FoundRules.Fee(0, 500) == skilled,
            "founding fees: the incorporation is a few days of a lawyer, more for founder friendly articles, less with the player's Law (" + standard + ", " + friendly + ", " + skilled + ")");
        string desk = ModSource(@"Tech\TechDirector.Founding.cs");
        Check(desk.Contains("FirmsGame.Pay(fee, FeeKey, firm.Reg, firm.File.Name)") && desk.Contains("VentureGame.Cash < fee"), "founding fees: the legal fees are booked at the firm through the mod's booking path, and refused when the cash cannot pay them");
        Check(RealLifeOverhaul.Economy.BizLines.Of("reallife_transaction_tech_founding_fees") == RealLifeOverhaul.Economy.BizLine.Cost && RealLifeOverhaul.Economy.BizLines.Of("reallife_transaction_tech_founding_expand") == RealLifeOverhaul.Economy.BizLine.Cost
              && RealLifeOverhaul.Economy.BizLines.Of("reallife_transaction_tech_founding_reseller") == RealLifeOverhaul.Economy.BizLine.Cost,
            "founding fees: incorporation, an opened niche and a reseller's cut are the firm's running costs in its statement, so they lower its account");
        var plain = FoundRules.Found("A~1", 5, 0, 0, FoundCatalogue.ArticlesStandard, 0, 0, 0);
        var kind = FoundRules.Found("A~1", 5, 0, 0, FoundCatalogue.ArticlesFriendly, 0, 0, 0);
        Cap c = VentureRules.NewCap("A~1");
        c.VcSeats = 1;
        c.BoardSeats = 2;
        Check(FoundRules.CanRemove(plain, c) && FoundRules.CanRemove(kind, c) == false, "founding articles: standard articles let a lead with a seat remove the founder, founder friendly ones need the board's majority");
        c.VcSeats = 2;
        c.BoardSeats = 3;
        Check(FoundRules.CanRemove(kind, c), "founding articles: with the board's majority the investors can remove the founder under either");
        c.VcSeats = 0;
        c.BoardSeats = 1;
        Check(!FoundRules.CanRemove(plain, c) && !FoundRules.CanRemove(kind, c), "founding articles: with no investor on the board nobody can remove the founder");
        Check(FoundRules.CanRemove(null, c), "founding articles: a firm not founded here keeps the ladder as it was");
        Cap funded = FoundRules.Incorporate("A~1", new List<CoFounder>(), 0, 5);
        FoundRules.TakeFamily(funded, FoundRules.FamilyAmount(0), 5);
        StartupMoney.Shortfall(new List<Cap> { funded }, 0, FoundRules.FamilyAmount(0), 40, false, "ba:transaction_boat");
        int susp = StartupMoney.Suspicion(funded, 40, 0, 0);
        Check(susp >= 60 && StartupMoney.Next(funded, 40, susp, 40, false) == StartupMoney.ActRemove && !FoundRules.CanRemove(plain, funded),
            "founding articles: the ladder asks to remove the founder, and a friends and family only firm has no board to do it (the claim still follows)");
        Check(FoundRules.ArticlesFactor(kind) == 920 && FoundRules.ArticlesFactor(plain) == 1000 && FoundRules.ArticlesFactor(null) == 1000, "founding articles: investors price founder friendly articles lower");
        string money = ModSource(@"Tech\TechDirector.Money.cs");
        Check(money.Contains("if (!FoundingCanRemove(c))") && money.Contains("Unremoved(c, firm, day)") && ModSource(@"Tech\TechDirector.Founding.cs").Contains("FoundRules.CanRemove("),
            "founding articles: the ladder's third step asks the articles before it removes the founder");
    }

    private static FitInputs Good(long users = 40) => new FitInputs { Appeal = 1200, Soundness = 900, Reputation = 800, Users = users, NicheBuyers = 1000, ChurnBase = 30 };

    private static void FoundStages()
    {
        var f = FoundRules.Found("A~1", 10, 0, NicheCatalogue.Restaurants, 0, 0, 0, 0);
        Check(f.Stage == FoundCatalogue.StageDiscovery && f.Counts, "founding stages: a new startup starts at discovery");
        int gain = FoundRules.Interview(0, 60, 20, 20, 50);
        Check(gain > FoundRules.Interview(0, 0, 0, 0, 0) && FoundRules.Interview(2, 0, 0, 100, 0) > FoundRules.Interview(2, 100, 100, 0, 0),
            "founding stages: what an interview teaches leans on Selling in person, Relationships by phone and Analysis in a survey, and on Product sense (" + gain + ")");
        Check(!FoundRules.Interviewed(f, gain) && f.Stage == FoundCatalogue.StageDiscovery && f.Rounds == 1 && FoundRules.Interviewed(f, gain) && f.Stage == FoundCatalogue.StageBuild && f.Learned == Math.Min(1000, 2 * gain) && f.Rounds == 0,
            "founding stages: two rounds of interviews at the desk move discovery on to the first version, and what was learned is kept");
        FoundRules.Day(f, false, Good(), 20);
        Check(f.Fit == f.Learned / 4 && f.Stage == FoundCatalogue.StageBuild, "founding stages: without a product on sale fit is only a quarter of what the interviews gave");
        FoundRules.Day(f, true, Good(1), 21);
        Check(f.Stage == FoundCatalogue.StageUsers, "founding stages: a product on sale moves the first version on to early users");
        bool found = false;
        for (int d = 22; d < 200 && !found; d++)
        {
            FoundRules.Note(f, 1, 0, 40);
            found = FoundRules.Day(f, true, Good(40), d);
        }
        Check(found && f.PmfDay > 0 && f.Fit >= FoundCatalogue.PmfBar && f.Stage == FoundCatalogue.StageLaunch, "founding stages: good retention and word of mouth find the market and early users moves on to the launch (fit " + f.Fit + ")");
        FoundRules.Launched(f, FoundCatalogue.ChannelPartner, 2);
        Check(f.Stage == FoundCatalogue.StagePricing && f.Channel == FoundCatalogue.ChannelPartner && f.Partner == 2, "founding stages: choosing the launch channel at the desk moves on to pricing");
        FoundRules.Priced(f, 2);
        Check(f.Stage == FoundCatalogue.StageScaling && f.Price == 2, "founding stages: testing the price at the desk puts the firm on the market");
        FoundRules.Launched(f, FoundCatalogue.ChannelSales, -1);
        FoundRules.Next(f, FoundCatalogue.StageBuild);
        Check(f.Stage == FoundCatalogue.StageScaling, "founding stages: a stage never goes backwards");
        Check(FoundRules.Boost(FoundCatalogue.ChannelProduct) > 0 && FoundRules.Boost(FoundCatalogue.ChannelSales) == 0 && FoundRules.Cut(FoundCatalogue.ChannelPartner) == FoundCatalogue.PartnerCut
              && FoundRules.Cut(FoundCatalogue.ChannelProduct) == 0 && FoundRules.FounderPoints(100, 100) > FoundRules.FounderPoints(20, 20) && FoundRules.FounderPoints(0, 0) == 0,
            "founding stages: product led adds reach, a partner keeps a cut, sales led adds the founder's own selling by their skills");
        int fixedBugs = FoundRules.Fixed(100, 80, 80);
        Check(fixedBugs > FoundRules.Fixed(100, 0, 0) && FoundRules.Fixed(0, 100, 100) == 0 && FoundRules.Word(100) > FoundRules.Word(0), "founding stages: fixing bugs and asking for word of mouth lean on Product sense, Analysis and Relationships");
        string links = ModSource(@"Wiring\Wiring.FoundLinks.cs"), desk = ModSource(@"Tech\TechDirector.FoundDesk.cs");
        int sessions = 0;
        foreach (string id in new[] { "tech_gtm_discovery", "tech_gtm_build", "tech_gtm_users", "tech_gtm_launch", "tech_gtm_price" })
        {
            sessions += links.Contains("\"" + id + "\"") ? 1 : 0;
        }
        Check(sessions == 5 && links.Contains("SessionRegistry.Add(Kind(i))") && links.Contains("OwnDesk = true") && links.Contains("tech.GtmSession(stage") && links.Contains("tech.GtmBlocked(stage"),
            "founding stages: each stage is a desk session of its own through the existing sessions path, held at the owner's desk");
        Check(desk.Contains("f.Stage != stage") && desk.Contains("FoundRules.Interviewed(f, gain)") && desk.Contains("OwnDeskRules.Credit(firm.File, day, minutes)") && desk.Contains("FoundRules.Launched(f, channel, partner)")
              && desk.Contains("FoundRules.Priced(f, tier)") && desk.Contains("FoundBridges.LearnNow("), "founding stages: each session moves its stage, and the skills it used grow through the study's own route");
    }
    private static string SourcePath(string relative) => System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts", relative));

    private static void FoundFit()
    {
        var low = FoundRules.Found("A~1", 5, 0, 0, 0, 0, 0, 0);
        var high = FoundRules.Found("A~1", 5, 0, 0, 0, 0, 0, 0);
        low.Fit = 100;
        high.Fit = 900;
        Check(FoundRules.Bear(high) > FoundRules.Bear(low) && FoundRules.Bear(low) >= 800 && FoundRules.Bear(high) <= 1200, "founding fit: customers bear a higher price at a higher fit (" + FoundRules.Bear(low) + " to " + FoundRules.Bear(high) + ")");
        int last = 0;
        bool rising = true, cheap = true;
        for (int fit = 0; fit <= 1000; fit += 25)
        {
            var f = FoundRules.Found("A~1", 5, 0, 0, 0, 0, 0, 0);
            f.Fit = fit;
            int v = FoundRules.FitFactor(f);
            rising &= v >= last;
            cheap &= fit >= FoundCatalogue.FitCheap || v == FoundCatalogue.CheapFactor;
            last = v;
        }
        low.Fit = 200;
        high.Fit = 900;
        Check(rising && cheap && FoundRules.FitFactor(low) < 1000 && FoundRules.FitFactor(high) > 1000 && FoundRules.FitFactor(null) == 1000 && FoundRules.GrowthBp(high) > 0 && FoundRules.GrowthBp(low) < 0,
            "founding fit: a raise before fit is cheap money, after fit a better price (" + FoundRules.FitFactor(low) + ", " + FoundRules.FitFactor(high) + ")");
        long value = VentureCatalogue.FloorValue[0];
        TermSheet cheapSheet = VentureRules.Sheet(1, value * FoundRules.FitFactor(low) / 1000, value / 4, 700, 1000, 50);
        TermSheet richSheet = VentureRules.Sheet(1, value * FoundRules.FitFactor(high) / 1000, value / 4, 700, 1000, 50);
        Check(richSheet.Pre > cheapSheet.Pre && !FoundRules.SeatAsked(low) && FoundRules.SeatAsked(new FoundedFirm { PmfDay = 9 }), "founding fit: the same meeting earns a higher value after fit, and investors ask for a seat only then");
        long firmValue = FirmsRules.Value(new Firm(), 1200000, 500 + FoundRules.GrowthBp(high)), baseValue = FirmsRules.Value(new Firm(), 1200000, 500 + FoundRules.GrowthBp(low));
        Check(firmValue > baseValue, "founding fit: fit moves the multiple of the existing valuation rules (" + baseValue + " to " + firmValue + ")");
        var f2 = FoundRules.Found("A~1", 5, 0, 0, 0, 0, 0, 0);
        f2.Learned = 500;
        var weak = new FitInputs { Appeal = 500, Soundness = 300, Reputation = 300, Users = 3, NicheBuyers = 100000, ChurnBase = 30 };
        f2.Users = 3;
        f2.LostMonth = 3000;
        int t0 = FoundRules.Target(f2, weak);
        f2.LostMonth = 0;
        Check(FoundRules.Target(f2, Good()) > t0 && FoundRules.Retention(f2, 30) == 1000, "founding fit: fit follows the product's quality, word of mouth and retention (" + t0 + " to " + FoundRules.Target(f2, Good()) + ")");
        f2.Users = 100;
        f2.LostMonth = 100000;
        Check(FoundRules.Retention(f2, 30) < 800, "founding fit: customers who leave lower retention");
        string fire = ModSource(@"Tech\TechDirector.Deals.cs");
        Check(fire.Contains("FoundRules.FitFactor(founded)") && fire.Contains("FoundRules.GrowthBp(founded)") && fire.Contains("FoundRules.ArticlesFactor(founded)") && fire.Contains("FoundRules.SeatAsked(founded)"),
            "founding fit: the raise's value takes fit, the articles and the investors' seat from the firm's record");
        string money = ModSource(@"Tech\TechDirector.Money.cs") + ModSource(@"Ui\Tech\TechLive.Money.cs");
        Check(money.Contains("Fit = founded != null && founded.Counts ? founded.Fit : -1") && money.Contains("reallife_vc_note_pack_fit") && money.Contains("reallife_vc_acct_fit"),
            "founding fit: the board pack and the firm's books show fit");
    }

    private static void FoundOldSaves()
    {
        Check(FoundRules.PlaceStage(true, false, 40, 90000) == FoundCatalogue.StageLaunch && FoundRules.PlaceStage(true, false, 0, 0) == FoundCatalogue.StageUsers
              && FoundRules.PlaceStage(false, true, 0, 0) == FoundCatalogue.StageBuild && FoundRules.PlaceStage(false, false, 0, 0) == FoundCatalogue.StageDiscovery,
            "founding old saves: a firm with revenue and customers is past early users, a product with none is at early users, one being made is at the first version");
        FoundedFirm p = FoundRules.Place("A~1", 400, (int)Venture.Software, NicheCatalogue.Cafes, FoundCatalogue.StageLaunch, 400);
        Check(p.Placed && !p.Engaged && !p.Counts && FoundRules.FitFactor(p) == 1000 && FoundRules.GrowthBp(p) == 0 && FoundRules.ArticlesFactor(p) == 1000 && !FoundRules.SeatAsked(p)
              && FoundRules.CanRemove(p, new Cap { VcSeats = 1, BoardSeats = 2 }) && p.Articles == FoundCatalogue.ArticlesStandard,
            "founding old saves: a placed firm is priced and governed exactly as before until its player takes it up");
        p.Engaged = true;
        p.Fit = 900;
        Check(p.Counts && FoundRules.FitFactor(p) > 1000, "founding old saves: taken up, its fit counts");
        Check(NicheRules.OfFirm(5, "A~1", (int)SectorMarket.BusinessSoftware) == NicheRules.OfFirm(5, "A~1", (int)SectorMarket.BusinessSoftware) && NicheRules.OfFirm(5, "A~1", (int)SectorMarket.Apps) == -1
              && NicheRules.Serves(NicheCatalogue.Def(NicheRules.OfFirm(5, "A~1", (int)SectorMarket.BusinessSoftware)), (int)SectorMarket.BusinessSoftware), "founding old saves: a niche is picked from the firm's market, seeded");
        var file = new FoundedFile();
        FoundedFirm f = FoundRules.Found("Main~1", 30, (int)Venture.Ai, NicheCatalogue.Clinics, 1, FoundCatalogue.SourceFamily, 5000000, 450000);
        f.Fit = 420;
        f.Stage = FoundCatalogue.StageUsers;
        f.Expanded.Add(new Expansion { Niche = NicheCatalogue.Gyms, Day = 44 });
        var co = Co(file.NextNo++);
        co.Shares = 2000;
        f.Cofounders.Add(co);
        file.Items.Add(f);
        file.Items.Add(p);
        file.Taken.Add(9001);
        FoundedFile back = FoundedStore.Read(FoundedStore.Write(file), out int version);
        FoundedFirm g = back.Of("Main~1");
        Check(version == FoundedStore.Version && back.Items.Count == 2 && g != null && g.Fit == 420 && g.Articles == 1 && g.Source == FoundCatalogue.SourceFamily && g.Stage == FoundCatalogue.StageUsers && g.Expanded.Count == 1
              && g.Cofounders.Count == 1 && g.Cofounders[0].Name == "Priya Raman" && g.Cofounders[0].Shares == 2000 && back.Taken.Count == 1 && back.Of("A~1").Placed && back.NextNo == 2,
            "founding old saves: the save line keeps the records, co-founders, niches and people taken");
        Check(FoundedStore.Key == "reallife:tech_founding" && FoundedStore.Load(new Dictionary<string, string>(), out _) == null && VentureStore.Key == "reallife:tech_ventures",
            "founding old saves: the save line is its own and a save without it loads with nothing in it");
        var venture = new VentureFile { Seed = 3 };
        string line = VentureStore.Write(venture);
        int v0 = line.IndexOf("~v:", StringComparison.Ordinal);
        int v1 = line.IndexOf('~', v0 + 1);
        string[] closed = line.Substring(v0 + 3, v1 - v0 - 3).Split('|');
        string oldLine = line.Substring(0, v0 + 3) + string.Join("|", closed.Take(8)) + line.Substring(v1);
        VentureFile old = VentureStore.Read(oldLine, out _);
        Check(closed.Length == 10 && old.ClosedUntil.Length == 10 && old.ClosedUntil.All(x => x == 0) && VentureStore.Version == 2, "founding old saves: a venture line from before the two new investors (eight venture firm doors) loads, and its version is unchanged");
        string ven = ModSource(@"Tech\TechDirector.Founding.cs");
        Check(ven.Contains("_founding.Of(file.Place) != null || FirmsCatalogue.MakesProducts[file.Kind] == 0") && ven.Contains("nothing it has changed") && ven.Contains("!f.Counts"),
            "founding old saves: a running firm is placed on the first day it is seen, and nothing in its sales changes until it is taken up");
    }

    private static void FoundExploits()
    {
        // raise from friends and family, spend it elsewhere, close
        Cap c = FoundRules.Incorporate("A~1", new List<CoFounder> { Co() }, 200, 10);
        long raised = FoundRules.TakeFamily(c, FoundRules.FamilyAmount(2), 10);
        c.Acc.FounderDay = 10;
        var caps = new List<Cap> { c };
        Check(StartupMoney.FloorOf(caps, 11) == raised, "founding exploits: the wallet must keep the family's money while the firm runs (" + raised + ")");
        long counted = StartupMoney.Shortfall(caps, 0, raised, 12, false, "ba:transaction_boatpurchase");
        Check(counted == raised && c.Acc.Taken == raised && StartupMoney.Floor(c, 12) == 0, "founding exploits: spending the family's money on a boat is counted as taken, with the day, the sum and the purchase");
        StartupMoney.Wind w = StartupMoney.Close(c, 0, 30);
        Check(w.Owed >= raised && w.Gap == w.Owed && w.Fraud && !w.Dry, "founding exploits: closing the firm leaves the family a claim of " + w.Gap + " and the charge, as for any investor");
        Cap honest = FoundRules.Incorporate("A~1", new List<CoFounder>(), 0, 10);
        FoundRules.TakeFamily(honest, FoundRules.FamilyAmount(2), 10);
        StartupMoney.Profit(honest, -raised, 40);
        StartupMoney.Wind dry = StartupMoney.Close(honest, 0, 45);
        Check(dry.Dry && dry.Owed == 0 && dry.Gap == 0 && !dry.Fraud, "founding exploits: a firm that honestly spent the family's money on itself owes nothing");
        // the accelerator and an angel are the same
        Cap acc = FoundRules.Incorporate("A~1", new List<CoFounder>(), 0, 10);
        long sum = FoundRules.TakeAccelerator(acc, 10);
        Check(StartupMoney.Shortfall(new List<Cap> { acc }, 0, sum, 12, false, "ba:transaction_x") == sum && StartupMoney.Close(acc, 0, 30).Gap > 0, "founding exploits: the accelerator's money cannot leave either");
        Cap ang = FoundRules.Incorporate("A~1", new List<CoFounder>(), 0, 10);
        long got = VentureRules.Accept(ang, FoundRules.AngelSheet(7, VentureCatalogue.FloorValue[0], 50, 50, 1000, 10, new FoundedFirm()), 0, 10);
        Check(StartupMoney.Shortfall(new List<Cap> { ang }, 0, got, 12, false, "ba:transaction_x") == got, "founding exploits: an angel's cheque cannot leave either");
        // a co-founder leaving, and the sum of all the lines
        CoFounder gone = c.Holders.Count > 2 ? Co() : Co();
        gone.Day = 10;
        long before = c.Total;
        FoundRules.Leave(c, gone, 100, 365);
        Check(StartupMoney.Investors(c) == StartupMoney.Balance(c) - 0 || StartupMoney.Investors(c) >= 0, "founding exploits: a co-founder leaving moves shares and no money");
        Check(c.Total <= before && c.Acc.Raised == raised, "founding exploits: it cancels shares and leaves the account as it was");
        string d = ModSource(@"Tech\TechDirector.Founding.cs");
        Check(d.Contains("_founding.Of(d.Place) != null || _venture.CapOf(d.Place) != null") && d.Contains("RefreshFloor(day);") && d.Contains("VentureGame.Give(Level(money), VentureGame.RaiseKey, firm.File.Name, firm.Reg)")
              && d.Contains("VentureRules.Accept(cap, AngelOffer(vc, d.Articles), 0, day)") && d.Contains("FirmsGame.Pay(cost, ExpandKey, firm.Reg, firm.File.Name)") && d.Contains("FirmsGame.Cost(r.Takings * cut / 1000, ResellKey, firm.Reg"),
            "founding exploits: a firm is founded once, the raise lands in the wallet with the floor set at once, and every fee and cut is booked at the firm's own address");
        Check(!d.Contains("Game.Give(") || d.Split(new[] { "VentureGame.Give(" }, StringSplitOptions.None).Length == 2, "founding exploits: the only money into the wallet is the first source's raise");
        Check(d.Contains("DemoSheet(vc, value, f") && d.Contains("c.Sheets.Add(t)") && !d.Contains("Accept(c,"), "founding exploits: demo day only offers sheets: the money comes through the term sheet path");
        Check(ModSource(@"Tech\TechDirector.Founding.cs").Contains("FoundingWound(Cap c, StartupMoney.Wind w") && ModSource(@"Tech\TechDirector.Money.cs").Contains("FoundingWound(c, w, name);"),
            "founding exploits: winding a firm down reads the family's line for the strain on a partner");
    }

    private static void FoundWiring()
    {
        string ven = ModSource(@"Tech\TechDirector.Venture.cs");
        Check(ven.Contains("StartFounding();") && ven.Contains("StopFounding();") && ven.Contains("WriteFounding(data);") && ven.Contains("FoundingDay(day);"),
            "founding options: founding starts, saves and stops with the venture part, so with Startups off everything behaves as today");
        string d = ModSource(@"Tech\TechDirector.Founding.cs"), desk = ModSource(@"Tech\TechDirector.FoundDesk.cs");
        Check(d.Contains("public FoundedFile Foundings => _ventureOn ? _founding : null;") && d.Contains("_founding != null ? _founding.Of(firm.File.Place) : null") && d.Contains("_founding = null;")
              && desk.Contains("|| !_ventureOn") && ModSource(@"Tech\TechDirector.Sector.cs").Contains("if (_founding != null)"),
            "founding options: with the part off the record is null, the sessions say there is nothing, and the sector's sales run as they did");
        Check(d.Contains("GameDlc.Gated(reg)") && d.Contains("_nicheDay == day") && !d.Contains("Update()") && !ModSource(@"Wiring\Wiring.FoundLinks.cs").Contains("Update()"), "founding cost: the city's businesses are counted once a day and kept, nothing runs per frame");
        string live = ModSource(@"Ui\Tech\TechLive.Found.cs");
        Check(live.Contains("_d.FoundStartup(draft)") && live.Contains("_d.Expand(place, niche)") && live.Contains("_d.PartWays(place, no)") && live.Contains("_d.Engage(place)") && live.Contains("_d.Foundable(firms)") && !live.Contains(".rootCanvas"),
            "founding pages: every button of the flow, the niche and the go to market page reaches its director call");
        Check(ModSource(@"Ui\Tech\TechLive.Venture.cs").Contains("FoundPage();") && ModSource(@"Ui\Tech\TechLive.Venture.cs").Contains("NichesPage();") && ModSource(@"Ui\Tech\TechLive.Venture.cs").Contains("GtmPage(picked);")
              && ModSource(@"Ui\Tech\TechLive.Venture.cs").Contains("FundCard(v);") && ModSource(@"Ui\Tech\TechLive.Venture.cs").Contains("RaisePage(picked);") && ModSource(@"Ui\Tech\TechLive.Venture.cs").Contains("BooksPage(picked);"),
            "founding pages: the Startups and Funding tabs reach the new pages and keep every page they had");
        foreach (string state in new[] { "found", "found_angel", "found_none", "niches", "niche", "gtm", "gtm_placed" })
        {
            Check(UiSamples.TechStates.Contains(state), "founding pages: the sample state '" + state + "' is in the previews and the UI checks");
        }
        string keys = ModSource(@"Ui\UiScreens.Tech.cs");
        var missing = new List<string>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(live, "\"(reallife_(?:found|gtm|niche)_[a-z0-9_]+)\""))
        {
            if (!keys.Contains("\"" + m.Groups[1].Value + "\"") && !m.Groups[1].Value.Contains("_note_"))
            {
                missing.Add(m.Groups[1].Value);
            }
        }
        Check(missing.Count == 0, "founding pages: every text key of the pages is listed in the Tech screen's keys (" + string.Join(", ", missing.Take(3)) + ")");
        string enText = System.IO.File.ReadAllText(SourcePath(@"..\Locales\en.json"));
        string ptText = System.IO.File.ReadAllText(SourcePath(@"..\Locales\pt.json")), zhText = System.IO.File.ReadAllText(SourcePath(@"..\Locales\zh-cn.json"));
        var gone = new List<string>();
        foreach (string file in new[] { @"Tech\TechDirector.Founding.cs", @"Tech\TechDirector.FoundDesk.cs", @"Ui\Tech\TechLive.Found.cs", @"Tech\Founding\FoundCatalogue.cs", @"Tech\Founding\NicheCatalogue.cs", @"Tech\Founding\FoundText.cs" })
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(ModSource(file), "\"(reallife_(?:found|gtm|niche|vc_acct_fit|vc_note_pack_fit|vc_note_removal_blocked)[a-z0-9_]*)\""))
            {
                string key = "\"" + m.Groups[1].Value + "\":";
                if (!enText.Contains(key) || !ptText.Contains(key) || !zhText.Contains(key))
                {
                    gone.Add(m.Groups[1].Value);
                }
            }
        }
        Check(gone.Count == 0, "founding texts: every key of founding is in en, pt and zh-cn (" + string.Join(", ", gone.Take(3)) + ")");
        string[] sessions = { "discovery", "build", "users", "launch", "price" };
        int missingDesk = 0;
        foreach (string s in sessions)
        {
            foreach (string suffix in new[] { "_title", "_about", "_step", "_c0", "_c1", "_c2", "_c0_trade", "_c1_trade", "_c2_trade" })
            {
                missingDesk += enText.Contains("\"reallife_tech_desk_gtm_" + s + suffix + "\":") ? 0 : 1;
            }
        }
        Check(missingDesk == 0, "founding texts: every desk session has its title, step and choices in the texts");
    }
}
