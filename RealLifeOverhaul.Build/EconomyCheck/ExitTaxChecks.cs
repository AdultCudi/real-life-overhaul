using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Tech;

// A startup stake sold is a capital gain on the quarter's return, as a business sold is (Scripts/Tech/Venture/ExitGains.cs, TechDirector.ExitGains.cs). Game-free rules driven
// directly; the game's side is read from the sources.
internal static partial class Program
{
    private const int TaxLevel = 1000;

    private static void ExitTaxChecks()
    {
        ExitTaxRules();
        ExitTaxSlices();
        ExitTaxQuarter();
        ExitTaxSave();
        ExitTaxWiring();
    }

    private static Cap TaxCap(long invested)
    {
        Cap c = FundedCap();
        foreach (Holder h in c.Holders)
        {
            if (h.Kind == (int)HolderKind.Player)
            {
                h.Invested = invested;
            }
        }
        return c;
    }

    private static void ExitTaxRules()
    {
        var f = new GainFile();
        Cap none = TaxCap(0);
        long g = ExitGains.ForSale(f, none, "Main~1", "Firm", 50000000, 0, 100, TaxLevel);
        Check(g == 50000000 && ExitGains.Between(f, 100, 100) == 500000, "exit tax: a firm sold (or its team taken) notes what the wallet gained, with nothing paid for the stake, on its day (" + g + ")");
        var f2 = new GainFile();
        long own = 20000000;
        Cap shared = TaxCap(0);
        long mine = ExitGains.PlayerShares(shared), all = shared.Total;
        long g2 = ExitGains.ForSale(f2, shared, "Main~1", "Firm", 50000000, own, 100, TaxLevel);
        long part = (long)((double)own * mine / all);
        Check(g2 == 50000000 - part && part < own, "exit tax: the fixtures count only in the player's share of the firm, the investors' share was their money (" + g2 + ")");
        var f3 = new GainFile();
        Check(ExitGains.ForSale(f3, none, "Main~1", "Firm", 0, 0, 100, TaxLevel) == 0 && ExitGains.ForSale(f3, none, "Main~1", "Firm", -9000000, 0, 100, TaxLevel) == 0
              && ExitGains.ForSale(f3, none, "Main~1", "Firm", 1000, 5000, 100, TaxLevel) == 0 && f3.Lines.Count == 0 && ExitGains.Between(f3, 0, 999) == 0,
            "exit tax: a payout of nothing, a negative payout (the wallet pays the investors) and a sale under the fixtures note no gain, and never a negative one");
        Cap c = FundedCap();
        SaleResult r = ExitRules.Settle(c, Raise * 3, Raise, 20);
        var f4 = new GainFile();
        long g4 = ExitGains.ForSale(f4, c, "Main~1", "Firm", r.Player, 0, 20, TaxLevel);
        Check(g4 == r.Player && g4 < r.Price && r.Cash > 0 && g4 + r.Paid == r.Price, "exit tax: the gain is the price less what the investors were paid, not the price and not the account (" + g4 + " of " + r.Price + ", account " + r.Cash + ")");
        var f5 = new GainFile();
        Check(ExitGains.ForSale(f5, none, "Main~1", "Firm", 1000000, 0, 5, 2000) == 2000000, "exit tax: the gain is at today's prices, as the wallet was paid");
        var f6 = new GainFile();
        Check(ExitGains.ForStake(f6, "Pocketloop", 3000000, 1000000, 40, TaxLevel) == 2000000 && ExitGains.ForStake(f6, "Pocketloop", 500000, 1000000, 41, TaxLevel) == 0,
            "exit tax: a stake bought in a startup of the city and sold above its cost notes the difference; at a loss, nothing");
        Check(ExitGains.ForShares(null, none, "Main~1", "x", 5, 5, 5, 1, TaxLevel) == 0 && ExitGains.ForSale(null, none, "Main~1", "x", 5, 5, 1, TaxLevel) == 0, "exit tax: with no file (Startups never on) nothing is noted");
    }

    private static void ExitTaxSlices()
    {
        Cap c = TaxCap(30000000);
        long held = ExitGains.PlayerShares(c);
        var f = new GainFile();
        long gains = 0, whole = 0, left = held, slice = Math.Max(1, held / 1000);
        int n = 0;
        while (left > 0 && n < 5000)
        {
            long take = Math.Min(slice, left);
            long paidFor = 70000L * take / slice;
            gains += ExitGains.ForShares(f, c, "Main~1", "Firm", paidFor, left, take, 200 + n / 400, TaxLevel);
            whole += paidFor;
            left -= take;
            n++;
        }
        BasisLine b = f.BasisOf("Main~1", false);
        Check(left == 0 && b != null && b.Used == 30000000 && gains == whole - 30000000, "exit tax: shares sold in " + n + " slices use exactly the whole basis once (" + (b != null ? b.Used : -1) + "), gain " + gains + " of " + whole);
        var one = new GainFile();
        long total = ExitGains.ForShares(one, c, "Main~1", "Firm", whole, held, held, 200, TaxLevel);
        Check(total == gains, "exit tax: slicing does not change the tax (" + total + " in one, " + gains + " in slices)");
        var tiny = new GainFile();
        for (int i = 0; i < 400; i++)
        {
            ExitGains.Note(tiny, 50, "Firm", 50);
        }
        Check(ExitGains.Between(tiny, 50, 50) == 200 && tiny.Lines.Count == 1, "exit tax: 400 gains of fifty cents on one day count as $200 (" + ExitGains.Between(tiny, 50, 50) + ") on one line");
        var sold = new GainFile();
        sold.Basis.Add(new BasisLine { Place = "Main~1", Sold = 5, Used = 100 });
        ExitGains.ForSale(sold, c, "Main~1", "Firm", 1, 0, 9, TaxLevel);
        Check(sold.Basis.Count == 0, "exit tax: a firm sold leaves no basis behind");
    }

    private static void ExitTaxQuarter()
    {
        var f = new GainFile();
        ExitGains.Note(f, 89, "A", 1000000);
        ExitGains.Note(f, 90, "B", 2000000);
        Check(ExitGains.Between(f, 0, 89) == 10000 && ExitGains.Between(f, 90, 179) == 20000 && ExitGains.Between(f, 0, 179) == 30000, "exit tax: a sale on a quarter's last day is in that return, the next day's in the next, never both");
        Check(ExitGains.Between(f, 0, 88) == 0 && ExitGains.Between(null, 0, 99) == 0, "exit tax: a quarter before the sale holds none of it");
        Func<int, int, int> biz = (a, b) => 700;
        Func<int, int, int> stake = (a, b) => ExitGains.Between(f, a, b);
        Check(TaxRules.OtherIncome(biz, stake, 0, 179) == 30700 && TaxRules.OtherIncome(null, stake, 0, 179) == 30000 && TaxRules.OtherIncome(biz, null, 0, 179) == 700 && TaxRules.OtherIncome(null, null, 0, 9) == 0,
            "exit tax: the return counts a business sale's gain and a startup stake's gain together, either alone, and neither twice");
        Check(TaxRules.OtherIncome((a, b) => int.MaxValue / 2, (a, b) => int.MaxValue / 2, 0, 1) == int.MaxValue / 2, "exit tax: the sum cannot overflow");
        var old = new GainFile();
        ExitGains.Note(old, 1, "Old", 100);
        ExitGains.Note(old, 1 + ExitGains.KeptDays + 1, "New", 100);
        Check(old.Lines.Count == 1 && old.Lines[0].Name == "New", "exit tax: a note long in a closed return is dropped");
    }

    private static void ExitTaxSave()
    {
        var f = new GainFile();
        ExitGains.Note(f, 12, "Firm", 123456);
        f.Basis.Add(new BasisLine { Place = "Main~1", Sold = 7, Used = 99 });
        string line = ExitGainStore.Write(f);
        GainFile back = ExitGainStore.Read(line, out int version);
        Check(version == ExitGainStore.Version && back.Lines.Count == 1 && back.Lines[0].Cents == 123456 && back.Lines[0].Day == 12 && back.Basis.Count == 1 && back.Basis[0].Used == 99 && back.Basis[0].Sold == 7,
            "exit tax: the line reads back whole");
        Check(ExitGainStore.Write(new GainFile()) == null && ExitGainStore.Load(new Dictionary<string, string>()) == null && ExitGainStore.Load(null) == null,
            "exit tax: an old save has no line and loads with nothing noted; an empty file writes nothing");
        Check(ExitGainStore.Key == "reallife:tech_exit_gains" && ExitGains.Between(ExitGainStore.Read("v=1", out _), 0, 999) == 0, "exit tax: the line has its own key and a bare header reads as nothing");
        GainFile bad = ExitGainStore.Read("v=1~g:5|-900|x", out _);
        Check(bad.Lines.Count == 0 && ExitGains.Between(bad, 0, 999) == 0, "exit tax: a negative gain in a damaged line is dropped");
        Check(ExitStore.Key == "reallife:tech_exits" && VentureStore.Key == "reallife:tech_ventures" && !ModSource(@"Tech\Venture\ExitStore.cs").Contains("Gain") && !ModSource(@"Tech\Venture\VentureStore.cs").Contains("GainFile"),
            "exit tax: reallife:tech_exits and reallife:tech_ventures keep their fields");
    }

    private static void ExitTaxWiring()
    {
        string exits = ModSource(@"Tech\TechDirector.Exits.cs"), deals = ModSource(@"Tech\TechDirector.Deals.cs"), venture = ModSource(@"Tech\TechDirector.Venture.cs");
        string gains = ModSource(@"Tech\TechDirector.ExitGains.cs"), links = ModSource(@"Wiring\Wiring.ExitLinks.cs"), game = ModSource(@"Economy\Company\CompanyGame.cs");
        Check(Regex.IsMatch(exits, @"VentureGame\.Give\(Level\(r\.Price\)[\s\S]*?ExitGains\.ForSale\(_gains, c, place, name, r\.Player, fixtures \* 100L") && exits.Contains("GainNotice(name, gain);"),
            "exit tax: a firm sold to a buyer, and a team taken for the hire (one path), notes its gain after the buyer's money came in");
        Check(Regex.IsMatch(exits, @"VentureGame\.Give\(Level\(paid\), VentureGame\.ShareSaleKey[\s\S]*?ExitGains\.ForShares\(_gains, c, place, firm\.File\.Name, paid, held, room"),
            "exit tax: the player's vested shares sold after a round, and a listed firm's after the lock-up (one path), note theirs");
        Check(venture.Contains("ExitGains.ForStake(_gains, s.Name, h.Paid, h.Invested"), "exit tax: an angel's stake in a startup of the city sold with it notes its gain");
        int hand = exits.IndexOf("ExitBridges.HandOver(place, o.Name)"), note = exits.IndexOf("ExitGains.ForSale(");
        Check(hand > 0 && note > hand, "exit tax: a sale refused at the hand over notes nothing (the note comes after it)");
        int shares = exits.IndexOf("ExitRules.Sell(c, e, room");
        Check(shares > 0 && exits.IndexOf("if (paid <= 0)", shares) < exits.IndexOf("ExitGains.ForShares(", shares), "exit tax: shares refused (no room, locked up) note nothing");
        Check(venture.Contains("StartGains();") && venture.Contains("StopGains(keep);") && venture.Contains("WriteGains(data);"), "exit tax: the line is read, kept and written with the venture part");
        Check(gains.Contains("_gains ?? (Save != null ? ExitGainStore.Load(Save.modData) : null)"), "options off: a gain already noted stays in the quarter's return when Startups are turned off");
        Check(links.Contains("CompanyBridges.StartupGains =") && links.Contains("CompanyBridges.StartupGains = null;") && ModSource(@"Wiring\ServicesLinks.cs").Contains("CompanyBridges.OtherIncome = BusinessSales.GainsBetween;"),
            "exit tax: a source of its own beside the business sales' one, so neither overwrites the other");
        string tax = ModSource(@"Economy\Company\CompanyDirector.Tax.cs"), reserve = ModSource(@"Economy\Company\CompanyDirector.TaxReserve.cs");
        Check(tax.Contains("CompanyBridges.Other(from, yesterday)") && reserve.Contains("CompanyBridges.Other(t.QuarterFrom, to)") && game.Contains("TaxRules.OtherIncome(OtherIncome, StartupGains"),
            "exit tax: the quarter's close and the page's figure both read the two sources through one sum");
        Check(!Regex.IsMatch(gains, @"void (Update|LateUpdate|FixedUpdate)\(") && !Regex.IsMatch(gains + ModSource(@"Tech\Venture\ExitGains.cs"), @"GoingPublic|Network"), "cost, going public: it runs on a sale, and nothing is sent");
        Check(!Regex.IsMatch(ModSource(@"Tech\Venture\ExitPlaces.cs") + ModSource(@"Tech\TechDirector.Rivals.cs"), @"TaxRules|CompanyBridges|StartupGains"), "NPC: the city's startups and rivals have no tax return here, so nothing is taxed for them");
        string en = ModSource(@"..\Locales\en.json");
        Match m = Regex.Match(en, "\"reallife_co_return_takings\": \"([^\"]*)\"");
        Check(m.Success && !m.Groups[1].Value.ToLowerInvariant().Contains("sale") && ModSource(@"Economy\Company\CompanyApp.Tax.cs").Contains("t.ReturnTakings"), "exit tax: the tax page's takings line (" + m.Groups[1].Value + ") already holds the gain and does not say business sale");
        Check(en.Contains("\"reallife_exit_note_gain\"") && gains.Contains("reallife_exit_note_gain"), "exit tax: the exit's result says a capital gain was noted");
    }
}
