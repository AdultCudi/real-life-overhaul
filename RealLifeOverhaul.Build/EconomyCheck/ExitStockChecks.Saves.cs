using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Tech;

// The firm-on-the-market checks that are about what is kept and shared: the market's line and the tech sector's, a Going Public session, an old save, the options, the sources and the texts.
internal static partial class Program
{
    // A stock's record in the market's line: its columns, split as the reader splits them.
    private static string[] SxRecord(string line, int id)
    {
        foreach (string part in line.Split('~'))
        {
            if (!part.StartsWith("k:", StringComparison.Ordinal))
            {
                continue;
            }
            foreach (string record in part.Substring(2).Split(';'))
            {
                if (record.StartsWith(id + "|", StringComparison.Ordinal))
                {
                    return record.Split('|');
                }
            }
        }
        return null;
    }

    // A line as an older version wrote it: a firm's stock record cut at the last field that version reads, and its place column left off.
    private static string SxOldLine(string line, int id)
    {
        string[] parts = line.Split('~');
        for (int p = 0; p < parts.Length; p++)
        {
            if (!parts[p].StartsWith("k:", StringComparison.Ordinal))
            {
                continue;
            }
            string[] records = parts[p].Substring(2).Split(';');
            for (int r = 0; r < records.Length; r++)
            {
                if (records[r].StartsWith(id + "|", StringComparison.Ordinal))
                {
                    string[] c = records[r].Split('|');
                    c[64] = string.Join(",", c[64].Split(',').Take(34));
                    records[r] = string.Join("|", c.Take(68));
                }
            }
            parts[p] = "k:" + string.Join(";", records);
        }
        return string.Join("~", parts);
    }

    private static void ExitStockSaves()
    {
        SxFirm f = SxMake(7801);
        Stock s = f.Stock;
        if (s == null)
        {
            Check(false, "saves: made for the save's checks");
            return;
        }
        s.Own.VestDay = f.Day - 33;
        RunTo(f.Engine, f.Day + 20);
        string line = MarketStore.Write(f.State);
        MarketState back = MarketStore.Read(line, out _);
        Stock again = back != null ? back.Stock(s.Id) : null;
        Check(again != null && again.Own != null && again.Own.Firm && again.Own.Place == s.Own.Place && again.Own.Owner == SxMe && again.Name == s.Name && again.Ticker == s.Ticker && again.Sector == s.Sector,
            "saves: a firm's stock is read back from the market's line as a firm's, with its place, its player and its name");
        if (again == null)
        {
            return;
        }
        Check(again.Shares == s.Shares && again.Own.Founder == s.Own.Founder && again.Own.Float == s.Own.Float && again.Own.LockDay == s.Own.LockDay && again.Own.VestBase == s.Own.VestBase
              && again.Own.VestDay == s.Own.VestDay && again.Own.VestCliff == s.Own.VestCliff && again.Own.VestSpan == s.Own.VestSpan && again.Own.Rps == 0.0 && !again.Own.Mixed,
            "saves: its shares, the founder's and the public's, the lock-up and the vesting come back exactly");
        Check(again.Own.RevQ == s.Own.RevQ && again.Own.NiY == s.Own.NiY && again.Last == s.Last && again.Results.Count == s.Results.Count && again.Quarter == s.Quarter, "saves: and what the market has followed of the firm");
        Check(MarketStore.Write(back) == line, "saves: written again, the line is the same");
        MarketState city = MarketStore.Read(MarketStore.Write(f.State, true), out _);
        Check(city != null && city.Stock(s.Id) != null && city.Stock(s.Id).Own.Firm, "saves: the line a host gives a guest has the firm's stock, as every machine of a session must");
        // an older version's line: the same stock without the firm's fields
        string old = SxOldLine(line, s.Id);
        MarketState read = MarketStore.Read(old, out _);
        Stock plain = read != null ? read.Stock(s.Id) : null;
        Check(old != line && plain != null && plain.Own != null && !plain.Own.Firm && plain.Own.Founder == s.Own.Founder && plain.Own.LockDay == s.Own.LockDay && plain.Shares == s.Shares,
            "saves: a record without the firm's fields reads as a player's company's did, every field an older version knows intact");
        string[] columns = SxRecord(line, s.Id);
        string[] figures = columns != null && columns.Length > 64 ? columns[64].Split(',') : new string[0];
        Check(columns != null && columns.Length == 69 && figures.Length == 40 && figures[34] == "1" && columns[68] == MarketStore.Clean(SxPlace),
            "saves: the firm's fields are written after every field an older version reads: six more figures after the founder's deals of the year, the firm's place after the mix");
        // the tech sector's line
        var file = new StockFile { LastDay = 321 };
        file.Firms.Add(new StockFirm { Place = "Elm%20Street~14", ListDay = 100, OpenDay = 100, EndDay = 0, Ended = StockEnd.None, Old = false, Ticker = "ELM" });
        file.Firms.Add(new StockFirm { Place = "Ash~2", ListDay = 40, OpenDay = 45, EndDay = 300, Ended = StockEnd.WoundDown, Old = true });
        file.Firms.Add(new StockFirm { Place = "Oak~9", ListDay = 60, OpenDay = 60, EndDay = 200, Ended = StockEnd.Sold });
        file.Firms.Add(new StockFirm { Place = "Fir~7", ListDay = 70, OpenDay = 71, EndDay = 250, Ended = StockEnd.Gone });
        string text = ExitStockStore.Write(file);
        StockFile read2 = ExitStockStore.Read(text, out int version);
        Check(version == ExitStockStore.Version && read2.LastDay == 321 && read2.Firms.Count == 4 && read2.Firms[0].Place == "Elm%20Street~14" && read2.Firms[0].Ticker == "ELM" && read2.Firms[0].Live && !read2.Firms[1].Live && read2.Firms[1].Old
              && read2.Firms[1].Ended == StockEnd.WoundDown && read2.Firms[2].Ended == StockEnd.Sold && read2.Firms[3].Ended == StockEnd.Gone && ExitStockStore.Write(read2) == text,
            "saves: the tech sector's line keeps each firm's stock through every stage: made, ended by a wind down, a sale, or by the market's hand, written again the same");
        Check(read2.Of("Ash~2").EndDay == 300 && read2.Of("Nowhere") == null, "saves: a firm's record is found by its place");
        Check(ExitStockStore.Load(null, out _) == null && ExitStockStore.Load(new Dictionary<string, string>(), out _) == null, "saves: a save from before has no line, and an old save loads with nothing in it");
        StockFile older = ExitStockStore.Read("v=1~h:5~f:Ash%7E2|40|45|0|0|0", out _);
        Check(older.Firms.Count == 1 && older.Firms[0].Place == "Ash~2" && older.Firms[0].Live && older.Firms[0].Ticker.Length == 0,
            "saves: a record written before the ticker was kept reads, with none");
        StockFile bare = ExitStockStore.Read("v=1~h:5", out _);
        Check(bare.LastDay == 5 && bare.Firms.Count == 0, "saves: a line with only its header reads as no firm on the market");
        var big = new StockFile();
        for (int i = 0; i < ExitStockStore.FirmsMost + 20; i++)
        {
            big.Firms.Add(new StockFirm { Place = "P~" + i, OpenDay = 1 });
        }
        Check(ExitStockStore.Read(ExitStockStore.Write(big), out _).Firms.Count == ExitStockStore.FirmsMost, "saves: a line of too many firms is read to its bound and no more");
        // a guest's firm is set aside with a company and comes back
        ListingFile l = new ListingFile();
        Check(l.Empty, "saves: a way to the market with nothing in it is empty");
        l.SuspendedFirms.Add("1|x");
        Check(!l.Empty, "saves: ...and not empty with a firm set aside");
    }

    // ---------------------------------------------------------------- a Going Public session

    private static void ExitStockSession()
    {
        SxFirm host = SxMake(7901);
        SxFirm guest = SxMake(7902, 20000, 3000, 3000000000L, true, "Oak~2", "Oakwell");
        if (host.Stock == null || guest.Stock == null)
        {
            Check(false, "session: both firms are on their markets for the session's checks");
            return;
        }
        guest.State.Me = SxGuest;
        guest.Stock.Own.Owner = SxGuest;
        // the host's firm is in the city's market; the guest's own is set aside, whole
        SxOpenTime(host);
        host.Stock.Own.LockDay = 0;
        string text = MarketShare.Write(host.State, host.Engine.Tape);
        MarketState adopted = MarketShare.Adopt(guest.State, text, new List<long>(), null, out MarketTape tape, out long frontier);
        Check(adopted != null && adopted.Me == SxGuest, "session: a guest takes the host's market and keeps who they are");
        if (adopted == null)
        {
            return;
        }
        Stock theirs = adopted.Stock(host.Stock.Id);
        Check(theirs != null && theirs.Own != null && theirs.Own.Firm && theirs.Own.Owner == SxMe && theirs.Own.Float == host.Stock.Own.Float && theirs.Shares == host.Stock.Shares,
            "session: the host's firm is in it, the host's, with the shares the host's market has");
        Stock ours = adopted.Stock(guest.Stock.Id);
        Check(adopted.Listing.SuspendedFirms.Count == 1 && string.IsNullOrEmpty(adopted.Listing.Suspended) && (ours == null || ours.Own == null || ours.Own.Owner != SxGuest),
            "session: the guest's own firm is set aside, not in the host's city, and the guest's whole company (none here) is not confused with it");
        string saved = MarketStore.Write(adopted);
        Check(saved.Contains("~v:") && MarketStore.Read(saved, out _).Listing.SuspendedFirms.Count == 1 && !MarketStore.Write(adopted, true).Contains("~v:"),
            "session: a firm set aside is in the guest's own line and never in the city's line a host gives");
        var guestWorld = new StockWorld(7902) { Silent = true };
        guestWorld.State = () => adopted;
        var follow = new MarketEngine(adopted, tape, guestWorld);
        adopted.Following = true;
        Check(!follow.Mine(theirs) && follow.MineFirms(new List<Stock>()) == 0 && follow.MineListed() == null, "session: to the guest the host's firm is another holder's, and nothing of theirs is listed in it");
        // the host plays on, deals, and its tape alone is what the guest plays: the same prices, the same shares, the firm's days from the tape
        long cash = guestWorld.Purse.Money;
        long mine = host.Stock.Own.Founder;
        long n = Math.Min(ListingFirm.Sellable(host.Stock.Own, host.Engine.State.Day), Math.Max(1, MarketTrading.MostPerOrder(host.Stock) / 4));
        SxDeal(host, -n);
        RunTo(host.Engine, host.Engine.State.Day + 25);
        string lines = host.Engine.Tape.From(adopted.Day);
        var checks = new List<long>();
        long frontier2 = frontier;
        bool ok = MarketShare.Follow(follow.Tape, lines, checks, ref frontier2);
        follow.Follow(frontier2);
        int work = int.MaxValue;
        follow.RunTo(frontier2, ref work);
        Stock played = adopted.Stock(host.Stock.Id);
        Check(ok && host.Dealt.Count == 1 && played != null && played.Last == host.Stock.Last && played.Own.Founder == mine - n && played.Own.Founder == host.Stock.Own.Founder && played.Own.Float == host.Stock.Own.Float
              && played.Own.RevQ == host.Stock.Own.RevQ && played.Shares == host.Stock.Shares && MarketShare.Checksum(adopted) == MarketShare.Checksum(host.State),
            "session: a guest plays the host's firm's days from the host's tape alone, its deals too: the same price, the same shares, the same figures (" + host.Stock.Last + ", " + (played != null ? played.Last : 0) + ")");
        Check(guestWorld.Purse.Money == cash && guest.World.Purse.Money == 1000000000, "session: ...and the guest's cash is never touched by the host's firm: the founder's alone moves");
        // the guest leaves: the host's firm leaves their market, their own is back as it was
        adopted.Following = false;
        long founderBefore = guest.Stock.Own.Founder, floatBefore = guest.Stock.Own.Float;
        int moved = follow.Alone();
        List<Stock> back = new List<Stock>();
        follow.MineFirms(back);
        Check(moved == 2 && !played.Listed && played.Reason == GoneReason.Delisted && back.Count == 1 && back[0].Own.Place == guest.Stock.Own.Place && back[0].Own.Founder == founderBefore
              && back[0].Own.Float == floatBefore && back[0].Own.LockDay == guest.Stock.Own.LockDay && adopted.Listing.SuspendedFirms.Count == 0,
            "session: alone again, the host's firm leaves this market and the guest's own firm is back as it was, found by its place");
        Check(follow.Alone() == 0, "session: ...and only once");
        Check(!MarketStore.Write(adopted).Contains("~v:"), "session: nothing set aside is left in the line");
        // the same market on two machines
        SxFirm a = SxMake(7903), b = SxMake(7903);
        RunTo(a.Engine, a.Day + 90);
        RunTo(b.Engine, b.Day + 90);
        Check(a.Stock.Last == b.Stock.Last && a.Stock.Own.RevQ == b.Stock.Own.RevQ && a.Stock.Shares == b.Stock.Shares && a.World.Purse.Money == b.World.Purse.Money && MarketShare.Checksum(a.State) == MarketShare.Checksum(b.State),
            "session: the same market and the same firm are played to the same price on two runs (" + a.Stock.Last + ", " + b.Stock.Last + ")");
    }

    // ---------------------------------------------------------------- an old save, the options, the sources

    private static void ExitStockOldAndOff()
    {
        // a firm that listed before there was a stock is given one with no fee and no jump
        SxFirm f = SxMake(8001, put: false);
        f.Exit.ListDay = (f.Day - ExitCatalogue.LockUpDays + 15);
        f.Exit.LockTo = f.Day + 15;
        long book = 5200000000L;
        f.Ask = ExitStockRules.Ask(f.Cap, f.Exit, SxPlace, "Nimbleloop", book, f.Cap.Total, SxYear);
        f.Listing = SxListingOf(f.Ask, f.Day, 50000, 8000);
        Check(f.Ask.PriceCents == (int)(book / f.Cap.Total) && f.Ask.ListedDay == (f.Day - ExitCatalogue.LockUpDays + 15) && f.Ask.LockDay == f.Day + 15,
            "old save: a firm that listed before is priced by its worth by the books now over all its shares, and keeps its own lock-up and listing day (" + f.Ask.PriceCents + ")");
        Stock s = f.Engine.ListFirm(f.Listing, out FirmWhy why);
        f.Stock = s;
        Check(s != null && why == FirmWhy.None && s.Last == f.Ask.PriceCents && s.ListedDay == (f.Day - ExitCatalogue.LockUpDays + 15) && s.Own.LockDay == f.Day + 15 && s.Shares == f.Cap.Total,
            "old save: the stock appears at that price, as a listing of its own old day: no new listing's first morning to jump it (" + (s != null ? s.Last + " against " + f.Ask.PriceCents + ", listed " + s.ListedDay + " against " + ((f.Day - ExitCatalogue.LockUpDays + 15)) + ", lock " + s.Own.LockDay + " against " + (f.Day + 15) + ", shares " + s.Shares + " against " + f.Cap.Total : why.ToString()) + ")");
        Check(f.World.Purse.Money == 1000000000 && f.World.Purse.In == 0 && f.World.Purse.Out == 0 && f.World.Purse.Forced == 0 && f.World.Count(MarketNewsKind.Listing) == 1,
            "old save: no fee is charged, no money comes or goes");
        double mood = s.Sent;
        RunTo(f.Engine, f.Day + 1, MarketCatalogue.PhaseOpen);
        Check(s.Daily.Count == 0 || s.ListedDay < f.Engine.State.Day, "old save: ...and it opens as any company that has long been listed");
        SxFirm fresh = SxMake(8002);
        Check(fresh.Stock.ListedDay == fresh.Engine.State.Day && s.ListedDay < f.Day, "old save: a listing priced today is a new listing, and has its first morning (" + fresh.Stock.ListedDay + " on day " + fresh.Engine.State.Day + ")");
        // the lock-up it was priced with is honoured: still running, a sale is refused; run out, it is not
        SxOpenTime(f);
        int day = f.Engine.State.Day;
        long small = Math.Max(1, MarketTrading.MostPerOrder(s) / 4);
        s.Own.VestDay = day - 1000;
        Check((day < s.Own.LockDay ? OwnWhy.FirmLocked : OwnWhy.None) == ListingRules.DealBlocked(s, day, SxYear, -small) || ListingRules.DealBlocked(s, day, SxYear, -small) != OwnWhy.FirmLocked,
            "old save: the lock-up it carries over is kept as it was");
        s.Own.LockDay = day - 1;
        Check(ListingRules.DealBlocked(s, day, SxYear, -small) != OwnWhy.FirmLocked, "old save: ...and when it has run out the founder may sell");
        // the options: no bridge, no stock; the tech sector's methods answer nothing
        StockBridges.Reset();
        Check(!StockBridges.Ready && !StockBridges.OpenNow() && !StockBridges.LookNow(SxPlace).Live && StockBridges.Make == null && StockBridges.Sweep == null && StockBridges.Startup == null,
            "options: with the stock market off or the bridges not connected, no firm is put on it and none is found");
        string director = ModSource(@"Tech\TechDirector.ExitStock.cs");
        Check(Regex.IsMatch(director, @"internal StockInfo StockOf\(string place\)[\s\S]{0,120}_ventureOn && _stock != null \? StockBridges\.LookNow\(place\) : default\(StockInfo\)"),
            "options: with Startups off a firm has no stock to the tech sector: nothing asked of the market, nothing changed");
        Check(Regex.IsMatch(director, @"private void ExitStockDay\(int day\)[\s\S]{0,200}_stock == null \|\| _exits == null") && Regex.IsMatch(ModSource(@"Tech\TechDirector.Venture.cs"), @"ExitsDay\(day\);\s*ExitStockDay\(day\);"),
            "options: the stock's day runs with the venture part's, once a game day, and only while it runs");
        string all = director + ModSource(@"Wiring\Wiring.ExitStockLinks.cs") + ModSource(@"Markets\MarketDirector.Firms.cs") + ModSource(@"Markets\MarketEngine.Firms.cs") + ModSource(@"Markets\Listing\ListingFirm.cs");
        Check(!Regex.IsMatch(all, @"void (Update|LateUpdate|FixedUpdate|OnGUI)\(") && !all.Contains("InvokeRepeating") && !all.Contains("Time.deltaTime"),
            "cost: nothing of it runs per frame: it is the day's work, a click's, a market morning's and a deal's");
    }

    // ---------------------------------------------------------------- the sources and the texts

    private static readonly string[] SxKeys =
    {
        "reallife_exit_note_stock", "reallife_exit_note_sell_market", "reallife_exit_note_raise_market", "reallife_exit_stock_line", "reallife_exit_stock_free", "reallife_exit_button_stock",
        "reallife_exit_refuse_taken", "reallife_exit_refuse_ladder", "reallife_exit_note_called_off_owed", "reallife_exit_test_stock", "reallife_exit_test_stock_off",
        "reallife_stk_firm_yours", "reallife_stk_firm_other", "reallife_stk_founder_title", "reallife_stk_founder_stake", "reallife_stk_founder_public", "reallife_stk_founder_locked",
        "reallife_stk_founder_free", "reallife_lst_why_firm", "reallife_lst_why_firm_locked", "reallife_lst_why_unvested"
    };

    private static void ExitStockWiring()
    {
        string market = ModSource(@"Markets\MarketEngine.Own.cs"), rules = ModSource(@"Markets\Listing\ListingRules.cs"), links = ModSource(@"Wiring\Wiring.ExitStockLinks.cs");
        Check(Regex.IsMatch(rules, @"public static double ToReserve[\s\S]{0,200}if \(o\.Firm\)[\s\S]{0,80}return 0\.0;") && market.Contains("if (o.Firm && action != OwnAction.Deal)")
              && Regex.IsMatch(market, @"private void OwnBidChance[\s\S]{0,200}o\.Firm \|\|"), "wiring: the market keeps no reserve for a firm, takes only the founder's deals of it and makes no bid for it");
        Check(ModSource(@"Markets\MarketEngine.Session.cs").Contains("OwnBusiness(s, s.Own.Firm ? ListingFirm.EnvFor(env, s.Id) : env, from, day);") && market.Contains("FirmDealt?.Invoke(s.Id, shares, q.Cents, q.Value, q.Reserve + q.Fee)"),
            "wiring: a firm's days are its own statements' in the env the tape keeps, and each of the founder's deals is told to the tech sector");
        Check(links.Contains("StockBridges.Open = Open;") && links.Contains("MarketBridges.FirmDealt = Dealt;") && ModSource(@"Wiring\MarketLinks.cs").Contains("ExitStockLinks.Connect();")
              && ModSource(@"Wiring\MarketLinks.cs").Contains("ExitStockLinks.Disconnect();"), "wiring: the bridges are connected when the market's are and let go with them");
        Check(ModSource(@"Wiring\ListingLinks.cs").Contains("ExitStockLinks.Env(ref e, from, day);") && Regex.IsMatch(ModSource(@"Wiring\ListingLinks.cs"), @"bool company = book \|\| market\.MyCompany != null, firms = ExitStockLinks\.Any\(market\);"),
            "wiring: a market morning gives each firm's statements to the tape when the player has a firm on it, with or without a company of their own");
        Check(ModSource(@"Wiring\Wiring.ExitLinks.cs").Contains("(s.Own == null || !s.Own.Firm)"), "wiring: a firm of the player's is not a buyer of their others");
        Check(ModSource(@"Core\RealLifeOverhaulMod.cs").Contains(".AddButton(\"reallife_exit_test_stock\", () => Tech.TechDirector.Instance?.TestExitStock())") && ModSource(@"Tech\TechDirector.ExitStock.cs").Contains("public void TestExitStock()"),
            "wiring: the test button under the tech startups lists a firm at once");
        string sale = ModSource(@"Tech\TechDirector.ExitStock.cs");
        Check(sale.Contains("ExitBridges.Practised?.Invoke(TalentCatalogue.Selling, 1);") && sale.Contains("internal void FirmStockDealt(int stockId, long shares, int cents, long money, long fee)") && !Regex.IsMatch(sale, @"StockBridges\.Grow\([^;]*\);[\s\S]{0,40}FirmStockDealt"),
            "wiring: the founder's sale on the market is followed by the tech sector at FirmStockDealt, where the exit-tax job's gain record goes beside ExitSell's; it changes nothing in the market");
        // the texts
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("firm stock's texts", words, codes))
        {
            return;
        }
        var lacking = new List<string>();
        var long240 = new List<string>();
        for (int i = 0; i < SxKeys.Length; i++)
        {
            string miss = LanguagesLacking(words, codes, SxKeys[i]);
            if (miss.Length > 0)
            {
                lacking.Add(SxKeys[i] + " (" + miss + ")");
            }
            for (int l = 0; l < Math.Min(3, words.Length); l++)
            {
                if (words[l].TryGetValue(SxKeys[i], out string text) && (text.Length > 240 || text.Contains("!") || text.Contains(" - ") || text.Contains((char)0x2014) || text.Contains((char)0x2013)))
                {
                    long240.Add(SxKeys[i] + "/" + codes[l]);
                }
            }
        }
        Check(lacking.Count == 0, "firm stock's texts: every key is in every language, with the same names in curly brackets (" + string.Join(", ", lacking) + ")");
        Check(long240.Count == 0, "firm stock's texts: none is over 240 characters, has an exclamation mark or a dash (" + string.Join(", ", long240) + ")");
        // The line is looked for in any row of What's new: the version step moves the next row's lines into the version's row.
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "A startup that goes public now appears in the Stocks app" }, { "pt", "Uma startup que abre capital agora aparece no app Ações" },
            { "zh-cn", "上市的初创公司现在会在股票应用中作为独立股票出现" }, { "de", "Ein Startup, das jetzt an die Börse geht, erscheint in der App Aktien" },
            { "fr", "Une startup qui entre en bourse apparaît désormais dans l'application Bourse" }, { "es", "Una startup que sale a bolsa ahora aparece en la app Bolsa" },
            { "ru", "Стартап, который выходит на биржу, теперь появляется в приложении «Акции»" }, { "nl", "Een startup die naar de beurs gaat, verschijnt nu in de Aandelen-app" },
            { "tr", "Halka açılan bir girişim artık Borsa uygulamasında kendi hissesi olarak görünür" }
        };
        var noLine = new List<string>();
        for (int l = 0; l < codes.Length; l++)
        {
            if (starts.TryGetValue(codes[l], out string start) && !WhatsNewHas(words[l], start))
            {
                noLine.Add(codes[l]);
            }
        }
        Check(noLine.Count == 0 && words[0].TryGetValue("reallife_changes_next", out string next) && !next.Contains("Fixed: A startup that goes public"),
            "firm stock's texts: What's new says in one plain sentence that a startup that goes public is a stock in the Stocks app, in every language the checks read (" + string.Join(", ", noLine) + ")");
    }
}
