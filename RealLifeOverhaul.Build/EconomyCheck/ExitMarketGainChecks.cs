using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Tech;

// A founder's sale of a listed firm's shares in the Stocks app is a capital gain on the quarter's return, as ExitSell's older sale is (ExitGains.ForMarketDeal, called from
// TechDirector.FirmStockDealt). The market's own deal is driven through the engine; the gain is worked by the game-free rules; the game's side is read from the sources.
internal static partial class Program
{
    private static void ExitMarketGainChecks()
    {
        ExitMarketGainSale();
        ExitMarketGainRefused();
        ExitMarketGainSave();
        ExitMarketGainWiring();
    }

    // A firm on the market whose founder may sell: lock-up over, shares vested, the market open.
    private static SxFirm MarketGainFirm(ulong seed)
    {
        SxFirm f = SxMake(seed);
        SxOpenTime(f);
        Stock s = f.Stock;
        s.Own.LockDay = 0;
        s.Own.VestBase = 0;
        s.Own.VestDay = f.Engine.State.Day - 400;
        return f;
    }

    private static void ExitMarketGainSale()
    {
        const int level = 1500;
        SxFirm f = MarketGainFirm(7701);
        Stock s = f.Stock;
        long heldCap = ExitGains.PlayerShares(f.Cap), n = Math.Max(1, MarketTrading.MostPerOrder(s) / 8);
        SxDeal(f, -n);
        Check(f.Dealt.Count == 1 && f.Dealt[0][1] == -n && f.Dealt[0][3] > 0 && f.Dealt[0][4] > 0 && f.Dealt[0][4] < f.Dealt[0][3],
            "market gain: the market tells a sale with the money before commission and the commission (" + (f.Dealt.Count == 1 ? f.Dealt[0][3] + ", " + f.Dealt[0][4] : "none") + ")");
        if (f.Dealt.Count != 1)
        {
            return;
        }
        long[] d = f.Dealt[0];
        long heldBefore = s.Own.Founder + n;
        Check(heldBefore == heldCap, "market gain: the shares held before the sale are the stock's founder line plus those sold, and the cap table had them until it is aligned (" + heldBefore + ")");
        foreach (Holder h in f.Cap.Holders)
        {
            h.Invested = h.Kind == (int)HolderKind.Player ? heldBefore * d[2] / 2 : h.Invested;   // half the price a share, in cents
        }
        var book = new GainFile();
        long net = ExitGains.CentsOfMarket(d[3] - d[4]), basis = (long)((double)(heldBefore * d[2] / 2) * n / heldBefore);   // the market's money is whole dollars
        long want = Math.Max(0L, net - ExitGains.Level(basis, level));
        long gain = ExitGains.ForMarketDeal(book, ExitGains.Invested(f.Cap), SxPlace, "Nimbleloop", d[3], d[4], heldBefore, d[1], 300, level);
        Check(want > 0 && gain == want && ExitGains.Between(book, 300, 300) == (int)(want / 100) && book.Lines.Count == 1,
            "market gain: one sale gives one gain, the money less the commission and less the basis at today's prices, at a price level of 1.5 (" + gain + " of " + want + ")");
        var again = new GainFile();
        long atOne = ExitGains.ForMarketDeal(again, ExitGains.Invested(f.Cap), SxPlace, "Nimbleloop", d[3], d[4], heldBefore, d[1], 300, 1000);
        Check(gain < atOne, "market gain: the basis is levelled to today's prices and the market's money is not (" + gain + " at 1.5, " + atOne + " at 1)");
        foreach (int lv in new[] { 1000, 1500, 2400 })
        {
            Cap c1 = TaxCap(30000000), c2 = TaxCap(30000000);
            long held = ExitGains.PlayerShares(c1), take = held / 5, paid = 90000000;
            var a = new GainFile();
            var b = new GainFile();
            long viaSell = ExitGains.ForShares(a, c1, "Main~1", "Firm", paid, held, take, 50, lv);
            long viaMarket = ExitGains.ForMarketDeal(b, ExitGains.Invested(c2), "Main~1", "Firm", ExitGains.Level(paid, lv) / 100, 0, held, -take, 50, lv);
            Check(viaSell > 0 && viaSell == viaMarket && a.Basis[0].Sold == b.Basis[0].Sold && a.Basis[0].Used == b.Basis[0].Used,
                "market gain: a founder's share sold on the market has ExitSell's gain and basis for the same figures at level " + lv + " (" + viaSell + ")");
        }
        Cap c = TaxCap(30000000);
        long all = ExitGains.PlayerShares(c);
        var bk = new GainFile();
        var buy = new GainFile();
        Check(ExitGains.ForMarketDeal(buy, ExitGains.Invested(c), "Main~1", "Firm", 700000, 10, all, 500, 60, 1000) == 0 && buy.Lines.Count == 0 && buy.Basis.Count == 1
              && buy.Basis[0].Used == 0 && buy.Basis[0].Sold == 0 && buy.Basis[0].Bought == 70001000,
            "market gain: a purchase notes no gain and uses no basis, and what it cost is added to the stake's basis");
        long g1 = ExitGains.ForMarketDeal(bk, ExitGains.Invested(c), "Main~1", "Firm", 700000, 10, all, -(all / 2), 61, 1000);
        long g2 = ExitGains.ForMarketDeal(bk, ExitGains.Invested(c), "Main~1", "Firm", 700000, 10, all - all / 2, -(all - all / 2), 62, 1000);
        Check(g1 > 0 && g2 > 0 && g1 + g2 == 2 * (70000000 - 1000) - 30000000, "market gain: sales in two parts use the whole basis once and no more (" + g1 + " and " + g2 + ")");
        Check(ExitGains.ForMarketDeal(null, ExitGains.Invested(c), "Main~1", "Firm", 700000, 0, all, -1, 1, 1000) == 0
              && ExitGains.ForMarketDeal(new GainFile(), ExitGains.Invested(c), "Main~1", "Firm", 5, 9, all, -1, 1, 1000) == 0,
            "market gain: with no file, or a commission above the money, nothing is noted");
    }

    private static void ExitMarketGainRefused()
    {
        SxFirm f = SxMake(7702);
        SxOpenTime(f);
        Stock s = f.Stock;
        long small = Math.Max(1, MarketTrading.MostPerOrder(s) / 8);
        f.Engine.Event(MarketInputs.EventOwn + (int)OwnAction.Deal, s.Id, (int)-small);
        RunTo(f.Engine, f.Engine.State.Day, f.Engine.State.Phase + 1);
        Check(f.Dealt.Count == 0 && f.World.Last(MarketNewsKind.OwnRefused).B == (int)OwnWhy.FirmLocked, "market gain: a sale inside the lock-up is refused and the tech sector is told nothing");
        SxFirm g = MarketGainFirm(7703);
        g.Stock.Own.Owner = SxMe + 1UL;
        SxDeal(g, -Math.Max(1, MarketTrading.MostPerOrder(g.Stock) / 8));
        Check(g.Dealt.Count == 0, "market gain: another player's firm, as a guest sees it in Going Public, tells this machine's tech sector nothing");
        SxFirm h = MarketGainFirm(7704);
        h.State.Following = true;
        SxDeal(h, -Math.Max(1, MarketTrading.MostPerOrder(h.Stock) / 8));
        Check(h.Dealt.Count == 0, "market gain: a guest following the host's market tells its tech sector nothing");
        SxFirm k = MarketGainFirm(7705);
        SxDeal(k, Math.Max(1, MarketTrading.MostPerOrder(k.Stock) / 8));
        Check(k.Dealt.Count == 1 && k.Dealt[0][1] > 0 && ExitGains.ForMarketDeal(new GainFile(), ExitGains.Invested(k.Cap), SxPlace, "Nimbleloop", k.Dealt[0][3], k.Dealt[0][4], 100, k.Dealt[0][1], 9, 1000) == 0,
            "market gain: a purchase is told but notes no gain");
    }

    private static void ExitMarketGainSave()
    {
        Cap c = TaxCap(30000000);
        long all = ExitGains.PlayerShares(c);
        var f = new GainFile();
        long first = ExitGains.ForMarketDeal(f, ExitGains.Invested(c), "Main~1", "Firm", 700000, 10, all, -(all / 4), 80, 1500);
        string line = ExitGainStore.Write(f);
        GainFile back = ExitGainStore.Read(line, out _);
        Check(first > 0 && back.Lines.Count == 1 && back.Lines[0].Cents == first && back.Basis.Count == 1 && back.Basis[0].Sold == all / 4 && ExitGains.Between(back, 80, 80) == (int)(first / 100),
            "market gain: a save and reload keeps the gain and the basis used, neither repeated nor lost");
        long second = ExitGains.ForMarketDeal(back, ExitGains.Invested(c), "Main~1", "Firm", 700000, 10, all - all / 4, -(all - all / 4), 81, 1500);
        long whole = ExitGains.ForMarketDeal(new GainFile(), ExitGains.Invested(TaxCap(30000000)), "Main~1", "Firm", 1400000, 20, all, -all, 81, 1500);
        Check(second > 0 && first + second == whole, "market gain: the rest sold after a reload uses what the first part left of the basis (" + (first + second) + " of " + whole + ")");
    }

    private static void ExitMarketGainWiring()
    {
        string sale = ModSource(@"Tech\TechDirector.ExitStock.cs"), gains = ModSource(@"Tech\Venture\ExitGains.cs");
        Check(Regex.IsMatch(sale, @"if \(!_ventureOn \|\| _stock == null \|\| Save == null\)\s*\{\s*return;")
              && Regex.IsMatch(sale, @"ExitGains\.ForMarketDeal\(_gains, ExitGains\.Invested\(c\), place, _firms\[i\]\.File\.Name, money, fee, info\.Founder - shares, shares, Save\.Day, Prices\.Level\);\s*GainNotice\([^;]*;\s*ExitStockRules\.Align\(c, info\);"),
            "market gain: with Startups off nothing is noted; a deal notes its gain or its cost once, with the shares held before it and before the cap table is aligned, and the existing notice tells it");
        Check(!Regex.IsMatch(gains, @"ForMarketDeal[\s\S]{0,1200}(VentureGame|Wallet|\.Give\(|\.Force\()"), "market gain: noting a gain moves no money");
        Check(Regex.IsMatch(ModSource(@"Markets\MarketEngine.Own.cs"), @"if \(Mine\(s\) && !o\.Test\)\s*\{\s*long fee = [^;]*;\s*if \(o\.Firm\)\s*\{\s*FirmDealt\?\.Invoke"),
            "market gain: only the player's own firm on the player's own machine, never a test's, tells the tech sector of a deal");
        Check(!Regex.IsMatch(ModSource(@"Tech\TechDirector.ExitGains.cs"), @"Peer|Rival|Npc"), "market gain: the gains are the player's alone, a city startup's founder or a rival files no return here");
    }
}
