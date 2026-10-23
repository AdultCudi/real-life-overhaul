using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Markets;

// The city's builders on the stock market and a project's hedge (Scripts/Markets: BuilderMarket, MarketEngine.Builders,
// HedgeRules, MarketEngine.Hedge; Scripts/Construction: ExposureRules and the project's hedge; Wiring.MarketsBuildLinks):
// which rival builders are listed and as which companies, the builder news list, each piece's move and that a tender's
// moves add up to nothing, the news played from the tape alike on two machines, a builder's own news drawn at the close
// at its chance, the option keeping the builders' rows out, the save's line; the exchange's own building materials, their
// fair forwards, cash settlement and the importers' rule; a hedge's legs, sizes and limits, its money matching a purchase's
// dearer or cheaper price, bought by the broker's rules, settled and undone by the tests to the unit; a project's exposure
// from its stages, the construction line's new fields and books; a construction company's sector and books on the market;
// the sources, the shared files' lines, the texts and the cost.
internal static partial class Program
{
    private static void MarketsBuild()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        BuilderListChecks();
        BuilderNewsChecks();
        BuilderEngineChecks();
        BuilderDrawChecks();
        BuildingMaterialChecks();
        HedgeRuleChecks();
        HedgeEngineChecks();
        ExposureChecks();
        BuildBooksChecks();
        ListingBuilderChecks();
        MarketsBuildSources();
        MarketsBuildTexts();
        MarketsBuildCost();
        Console.WriteLine("  builders on the market and hedging: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- who is listed

    private static void BuilderListChecks()
    {
        Check(BuilderMarket.Rivals == BuildCatalogue.RivalCount && BuilderMarket.RivalStock.Length == BuildCatalogue.RivalCount,
            "builders: the market knows each of the construction game's rival builders");
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json")) ?? new Dictionary<string, string>();
        int listed = 0;
        for (int r = 0; r < BuilderMarket.Rivals; r++)
        {
            int id = BuilderMarket.RivalStock[r];
            string rival = en.TryGetValue(BuildCatalogue.RivalKeys[r], out string n) ? n : string.Empty;
            if (id == 0)
            {
                Check(rival.Contains("Brothers") || rival.Contains("Daughters") || rival.Contains("Mercer"), "builders: " + rival + " stays private (a family firm)");
                continue;
            }
            listed++;
            CompanyDef d = MarketCatalogue.Find(id);
            Check(d != null && d.Sector == MarketSector.Construction && !d.Pipeline && !d.Holding && BuilderMarket.RivalOf(id) == r,
                "builders: " + rival + " is listed in the construction sector, from the start (" + (d != null ? d.Name : "none") + ")");
            if (d != null && BuilderMarket.OwnRow(id))
            {
                Check(d.Name == rival, "builders: " + rival + "'s company has its own name on the market (" + d.Name + ")");
            }
            else if (d != null)
            {
                // A builder of the construction game listed as part of a company the market had: the same name's first word, and
                // no second company of that name's in the sector, so the two never stand side by side.
                string first = rival.Split(' ')[0];
                Check(d.Name.StartsWith(first + " ", StringComparison.Ordinal) && MarketCatalogue.All.Count(c => c.Name.StartsWith(first + " ", StringComparison.Ordinal)) == 1,
                    "builders: " + rival + " is listed as the arm of " + d.Name + ", the only company of its name");
            }
        }
        Check(listed == 5 && BuilderMarket.OwnRows.Length == 4 && BuilderMarket.OwnRows.All(id => BuilderMarket.RivalOf(id) >= 0),
            "builders: the five a market would list are, four of them new rows (" + listed + ")");
        Check(MarketCatalogue.All.Where(d => BuilderMarket.OwnRow(d.Id)).All(d => d.Id > 145 && d.Id < 200 && d.Ticker.Length == 3),
            "builders: their rows take ids never used before and tickers of three capitals");
        Check(MarketCatalogue.Find(28).Trade == "construction" && MarketCatalogue.All.Count(d => d.Trade == "construction") == 1,
            "builders: one listed company (Gorsebank Construction) is in the construction company's trade");
        // The option keeps the builders' own rows out of a new market and out of an older one's join; once listed they stay.
        MarketWorld w = new MarketWorld(505);
        MarketState off = MarketSetup.Open(505, 1, 0, 60, w.Env(1), 505, null, null, null, false);
        MarketState on = MarketSetup.Open(505, 1, 0, 60, w.Env(1), 505, null, null, null, true);
        Check(BuilderMarket.OwnRows.All(id => off.Stock(id) == null) && off.Stock(32) != null && BuilderMarket.OwnRows.All(id => on.Stock(id) != null)
              && on.Stocks.Count == off.Stocks.Count + BuilderMarket.OwnRows.Length, "builders: a market opened with the option off lists none of the builders' own rows");
        Check(MarketSetup.Join(off, false) == 0 && MarketSetup.Join(off, true) == BuilderMarket.OwnRows.Length && BuilderMarket.OwnRows.All(id => off.Stock(id) != null && off.Stock(id).Daily.Count > 0)
              && MarketSetup.Join(off, true) == 0, "builders: switched on, they join a market without them at its load, with their past; they join once");
    }

    // ---------------------------------------------------------------- the news list and the moves

    private static void BuilderNewsChecks()
    {
        BuilderMarket.Clear();
        BuilderMarket.Add(3, BuilderNewsKind.TenderWon, 3);
        BuilderMarket.Add(-1, BuilderNewsKind.TenderWon, 1);
        BuilderMarket.Add(-1, BuilderNewsKind.Late, 1);      // the player has no site news of the market's
        BuilderMarket.Add(9, BuilderNewsKind.Finished, 1);   // no such builder
        BuilderMarket.Add(2, BuilderNewsKind.None, 1);
        BuilderMarket.Add(6, BuilderNewsKind.Late, 9);       // a weight held to three
        Check(BuilderMarket.Waiting == 3 && BuilderMarket.Take(out BuilderItem a) && a.Rival == 3 && a.Kind == BuilderNewsKind.TenderWon && a.Weight == 3
              && BuilderMarket.Take(out BuilderItem b) && b.Rival == -1 && BuilderMarket.Take(out BuilderItem c) && c.Rival == 6 && c.Weight == 3 && !BuilderMarket.Take(out _),
            "builders: the news list takes what the construction game tells, oldest first, and refuses what is no builder's news");
        for (int i = 0; i < BuilderMarket.QueueMost + 5; i++)
        {
            BuilderMarket.Add(i % 8, BuilderNewsKind.Finished, 1);
        }
        Check(BuilderMarket.Waiting == BuilderMarket.QueueMost && BuilderMarket.Take(out BuilderItem oldest) && oldest.Rival == 5 % 8, "builders: a full list lets the oldest go");
        BuilderMarket.Clear();
        Check(BuilderMarket.Waiting == 0, "builders: the list is cleared at a start and a stand down");
        bool packs = true;
        for (int k = 1; k < (int)BuilderNewsKind.Count; k++)
        {
            for (int w = 1; w <= BuilderMarket.WeightMost; w++)
            {
                int p = BuilderMarket.Packed((BuilderNewsKind)k, w);
                packs &= BuilderMarket.KindOf(p) == (BuilderNewsKind)k && BuilderMarket.WeightOf(p) == w;
            }
        }
        Check(packs && BuilderMarket.KindOf(0) == BuilderNewsKind.None && BuilderMarket.KindOf(999) == BuilderNewsKind.None, "builders: a piece packs onto the tape's event and back, an unknown kind as none");
        // A tender's moves: the winner's share of a win and every other builder's loss add up to nothing for builders alike.
        bool zero = true, signs = true;
        foreach (MarketSize size in new[] { MarketSize.Mid })
        {
            for (int w = 1; w <= BuilderMarket.WeightMost; w++)
            {
                int win = BuilderMarket.MoodBp(BuilderNewsKind.TenderWon, w, true, size), lose = BuilderMarket.MoodBp(BuilderNewsKind.TenderWon, w, false, size);
                zero &= win + (BuilderMarket.Rivals - 1) * lose == 0;
                signs &= win > 0 && lose < 0;
            }
        }
        Check(zero && signs, "builders: a tender's win and the others' losses add up to nothing, the market expecting each to win its share");
        Check(BuilderMarket.MoodBp(BuilderNewsKind.TenderWon, 3, true, MarketSize.Small) > BuilderMarket.MoodBp(BuilderNewsKind.TenderWon, 3, true, MarketSize.Mid)
              && BuilderMarket.MoodBp(BuilderNewsKind.TenderWon, 3, true, MarketSize.Mid) > BuilderMarket.MoodBp(BuilderNewsKind.TenderWon, 1, true, MarketSize.Mid),
            "builders: a small builder moves more, and a public building's tender more than the city's works");
        Check(BuilderMarket.MoodBp(BuilderNewsKind.Finished, 1, true, MarketSize.Mid) > 0 && BuilderMarket.MoodBp(BuilderNewsKind.Late, 1, true, MarketSize.Mid) < 0
              && BuilderMarket.MoodBp(BuilderNewsKind.Bribery, 1, true, MarketSize.Mid) < 0 && BuilderMarket.MoodBp(BuilderNewsKind.Strike, 1, true, MarketSize.Mid) < 0
              && BuilderMarket.MoodBp(BuilderNewsKind.Rigged, 1, true, MarketSize.Mid) < 0 && BuilderMarket.MoodBp(BuilderNewsKind.Award, 1, true, MarketSize.Mid) > 0
              && BuilderMarket.MoodBp(BuilderNewsKind.Developed, 1, true, MarketSize.Mid) > BuilderMarket.MoodBp(BuilderNewsKind.Developed, 1, false, MarketSize.Mid)
              && BuilderMarket.MoodBp(BuilderNewsKind.Developed, 1, false, MarketSize.Mid) > 0 && BuilderMarket.MoodBp(BuilderNewsKind.Late, 1, false, MarketSize.Mid) == 0,
            "builders: on time lifts, late costs, a scandal or a strike costs, an award lifts; the district's growth lifts its builder most and the others a little");
        Check(Enumerable.Range(1, (int)BuilderNewsKind.Count - 1).All(k => Math.Abs(BuilderMarket.MoodBp((BuilderNewsKind)k, 3, true, MarketSize.Small)) <= BuilderMarket.MoodMostBp),
            "builders: no piece of news moves a builder more than its most");
        Check(BuilderMarket.MarginPermille(BuilderNewsKind.Bribery) < 0 && BuilderMarket.MarginPermille(BuilderNewsKind.Rigged) < BuilderMarket.MarginPermille(BuilderNewsKind.Bribery)
              && BuilderMarket.MarginPermille(BuilderNewsKind.Award) > 0 && BuilderMarket.MarginPermille(BuilderNewsKind.TenderWon) == 0,
            "builders: a scandal costs its usual margin for good, a rigged tender most; a tender is the mood's alone");
    }

    private static MarketEngine BuildersMarket(ulong seed, out MarketWorld world)
    {
        world = new MarketWorld(seed);
        world.Purse.Money = 100000000L;
        return NewMarket(seed, 60, 1, 0, world);
    }

    private static void BuilderEngineChecks()
    {
        MarketEngine m = BuildersMarket(611, out MarketWorld world);
        int day = SessionDay(m, 3, 10);
        int[] ids = BuilderMarket.RivalStock.Where(id => id > 0).ToArray();
        var before = ids.ToDictionary(id => id, id => m.State.Stock(id).MidCents);
        int news = world.Heard.Count(n => n.Kind == MarketNewsKind.Builder);
        var moodBefore = ids.ToDictionary(id => id, id => m.State.Stock(id).Sent);
        m.Event(MarketInputs.EventBuilder, 3, BuilderMarket.Packed(BuilderNewsKind.TenderWon, 3));   // Ostrander wins a public building
        RunTo(m, day, MarketCatalogue.PhaseOpen + 11);
        bool movedRight = ids.All(id =>
        {
            Stock k = m.State.Stock(id);
            double want = BuilderMarket.MoodBp(BuilderNewsKind.TenderWon, 3, id == 146, k.Size) / 10000.0;
            double got = k.Sent - moodBefore[id];
            return Math.Sign(got) == Math.Sign(want) && Math.Abs(got) > Math.Abs(want) * 0.5;
        });
        Check(movedRight && world.Heard.Count(n => n.Kind == MarketNewsKind.Builder) == news + 1
              && world.Heard.Last(n => n.Kind == MarketNewsKind.Builder).Stock == 146 && world.Heard.Last(n => n.Kind == MarketNewsKind.Builder).A == (int)BuilderNewsKind.TenderWon,
            "builders: a tender won, put on the tape, lifts the winner's mood and lowers every other listed builder's, with the winner's news");
        // The player wins: every listed builder lost it.
        double[] sent = ids.Select(id => m.State.Stock(id).Sent).ToArray();
        m.Event(MarketInputs.EventBuilder, BuilderMarket.Player, BuilderMarket.Packed(BuilderNewsKind.TenderWon, 1));
        RunTo(m, day, MarketCatalogue.PhaseOpen + 12);
        Check(ids.Select((id, i) => m.State.Stock(id).Sent < sent[i]).All(x => x), "builders: a tender the player's company wins takes a little off every listed builder");
        // A scandal: its margin for good, priced at once.
        Stock dunmore = m.State.Stock(147);
        double shift = dunmore.MarginShift, fair = dunmore.LogFair;
        var shifts = ids.ToDictionary(id => id, id => m.State.Stock(id).MarginShift);
        m.Event(MarketInputs.EventBuilder, 0, BuilderMarket.Packed(BuilderNewsKind.Bribery, 1));
        RunTo(m, day, MarketCatalogue.PhaseOpen + 13);
        Check(dunmore.MarginShift < shift && dunmore.LogFair < fair && ids.Where(id => id != 147).All(id => m.State.Stock(id).MarginShift == shifts[id]),
            "builders: a bribe that comes out cuts the builder's usual margin for good and its worth at once, no one else's");
        // Two machines on the same tape: the same prices.
        MarketEngine host = BuildersMarket(612, out _), guest = BuildersMarket(612, out _);
        int d2 = SessionDay(host, 3, 5);
        SessionDay(guest, 3, 5);
        foreach (MarketEngine e in new[] { host, guest })
        {
            e.Event(MarketInputs.EventBuilder, 6, BuilderMarket.Packed(BuilderNewsKind.Late, 2));
            e.Event(MarketInputs.EventBuilder, 4, BuilderMarket.Packed(BuilderNewsKind.Developed, 1));
            RunTo(e, d2 + 7);
        }
        Check(MarketShare.Checksum(host.State) == MarketShare.Checksum(guest.State),
            "builders: two machines playing the same tape have the same prices (Ironbridge Civil Works late, Tri-Street's building finished)");
        // The test tools: news played now and taken back.
        MarketEngine t = BuildersMarket(613, out _);
        SessionDay(t, 3, 20);
        var moves = new List<BuilderMove>();
        int[] cents = ids.Select(id => t.State.Stock(id).MidCents).ToArray();
        double margin = t.State.Stock(149).MarginShift;
        int moved = t.BuilderNow(1, BuilderNewsKind.Bribery, 1, moves) + t.BuilderNow(3, BuilderNewsKind.TenderWon, 3, moves);
        bool changed = t.State.Stock(149).MidCents < cents[Array.IndexOf(ids, 149)] && t.State.Stock(146).MidCents > cents[Array.IndexOf(ids, 146)];
        int undone = t.BuilderUndo(moves);
        Check(moved == 1 + ids.Length && changed && undone == moves.Count && ids.Select((id, i) => Math.Abs(t.State.Stock(id).MidCents - cents[i]) <= 1).All(x => x)
              && Math.Abs(t.State.Stock(149).MarginShift - margin) < 1e-12, "builders: the tests' news moves the prices now and is taken back to the cent");
        // The save's line keeps the builders' news; an unknown kind is left out by an older reader.
        string line = MarketStore.Write(m.State);
        MarketState back = MarketStore.Read(line, out _);
        Check(back != null && back.News.Count(n => n.Kind == MarketNewsKind.Builder) == m.State.News.Count(n => n.Kind == MarketNewsKind.Builder) && back.Stock(146) != null,
            "builders: the save's line keeps the builders and their news");
        Check(MarketNewsKind.Builder == (MarketNewsKind)32 && MarketInputs.EventBuilder == MarketInputs.EventCompany + 1 && MarketInputs.EventCount == MarketInputs.EventBuilder + 1
              && MarketRandom.BuilderNews == 40, "builders: the news kind, the tape's event and the draw have numbers of their own after every other (never renumbered)");
    }

    // A listed builder's own news at the close: a bribe or a strike at their chances, the builders only, drawn from the day's secret.
    private static void BuilderDrawChecks()
    {
        int bribes = 0, strikes = 0, others = 0, closes = 0;
        foreach (ulong seed in new ulong[] { 621, 622, 623, 624 })
        {
            MarketEngine m = BuildersMarket(seed, out MarketWorld world);
            RunTo(m, 300);
            closes += (int)Math.Round(300 * 5.0 / 7.0);
            bribes += world.Heard.Count(n => n.Kind == MarketNewsKind.Builder && n.A == (int)BuilderNewsKind.Bribery);
            strikes += world.Heard.Count(n => n.Kind == MarketNewsKind.Builder && n.A == (int)BuilderNewsKind.Strike);
            others += world.Heard.Count(n => n.Kind == MarketNewsKind.Builder && BuilderMarket.RivalOf(n.Stock) < 0);
        }
        double year = MarketCatalogue.TradingDaysPerYear(60);
        double wantB = BuilderMarket.BriberyChanceYear / year * closes * 5, wantS = BuilderMarket.StrikeChanceYear / year * closes * 5;
        Console.WriteLine("  builders: over " + closes + " closes of four markets, " + bribes + " bribes (" + wantB.ToString("F1") + " expected) and " + strikes + " strikes (" + wantS.ToString("F1") + ")");
        Check(others == 0 && Math.Abs(bribes - wantB) < 4.0 * Math.Sqrt(wantB) + 2 && Math.Abs(strikes - wantS) < 4.0 * Math.Sqrt(wantS) + 2,
            "builders: a builder's bribe or strike comes at its chance a year, to a listed builder only");
        MarketWorld w = new MarketWorld(625);
        MarketState state = MarketSetup.Open(625, 1, 0, 60, w.Env(1), 625, null, null, null, false);
        var off = new MarketEngine(state, new MarketTape(625), w);
        RunTo(off, 300);
        Check(w.Heard.Count(n => n.Kind == MarketNewsKind.Builder) == 0, "builders: a market that lists none of the builders' own rows draws no builder news, Ironbridge Civil Works' neither");
        MarketEngine a = BuildersMarket(626, out _), b = BuildersMarket(626, out _);
        RunTo(a, 120);
        RunTo(b, 120);
        Check(MarketShare.Checksum(a.State) == MarketShare.Checksum(b.State), "builders: the same market drawn twice has the same builders' news");
    }

    // ---------------------------------------------------------------- the exchange's building materials

    private static void BuildingMaterialChecks()
    {
        Check(DerivCatalogue.BuildingNames.Length == 4 && DerivCatalogue.BuildingBaseMicros.Length == 4 && DerivCatalogue.BuildingNames.All(DerivCatalogue.IsBuilding)
              && DerivCatalogue.BuildingNames.All(n => DerivCatalogue.GroupOf(n) == MaterialGroup.Building) && !DerivCatalogue.IsBuilding("ba:itemname_glass")
              && DerivCatalogue.GroupNames.SelectMany(g => g).All(n => !DerivCatalogue.IsBuilding(n)), "materials: cement, steel, bricks and timber are the exchange's own, in a group of their own");
        Check(BuildCatalogue.ExchangeItem[(int)Material.Cement] == DerivCatalogue.BuildingNames[0] && BuildCatalogue.ExchangeItem[(int)Material.Steel] == DerivCatalogue.BuildingNames[1]
              && BuildCatalogue.ExchangeItem[(int)Material.Bricks] == DerivCatalogue.BuildingNames[2] && BuildCatalogue.ExchangeItem[(int)Material.Timber] == DerivCatalogue.BuildingNames[3]
              && BuildCatalogue.ExchangeItem[(int)Material.Glass] == "ba:itemname_glass" && BuildCatalogue.ExchangeItem.Distinct().Count() == (int)Material.Count,
            "materials: each of the builders' materials follows its own on the exchange (glass the game's)");
        int[] units = { 20, 20, 10, 25 };
        int[] loads = { (int)Material.Cement, (int)Material.Steel, (int)Material.Bricks, (int)Material.Timber };
        Check(Enumerable.Range(0, 4).All(i => DerivCatalogue.BuildingBaseMicros[i] * units[i] == BuildCatalogue.LoadPrice[loads[i]] * DerivCatalogue.Micro),
            "materials: a merchant's lorry load is a round number of the exchange's units (20 t of cement or steel, 10 thousand bricks, 25 m3 of timber)");
        Check(DerivRules.MarginPct(MaterialGroup.Building) >= DerivCatalogue.MarginLeastPct && DerivRules.MarginPct(MaterialGroup.Building) <= DerivCatalogue.MarginMostPct
              && DerivCatalogue.Phi(MaterialGroup.Building) > 0.8 && DerivCatalogue.Phi(MaterialGroup.Building) < 0.97 && DerivCatalogue.GroupKeys.Length == (int)MaterialGroup.Count,
            "materials: building materials move by the exchange's rules, with a margin in bounds and a name");
        // The forward is the true expectation for the group too, so a future bought and held earns only what it pays.
        var m = new RawMaterial { Name = DerivCatalogue.BuildingNames[0], Group = MaterialGroup.Building, Base = DerivCatalogue.BuildingBaseMicros[0], Level = 1000 };
        m.X = 0.05;
        long forward = MaterialRules.Forward(m, 10, 1000, 0, 0, 60);
        double sum = 0;
        int n = 60000;
        for (int i = 0; i < n; i++)
        {
            double x = m.X;
            for (int c = 0; c < 10; c++)
            {
                x = MaterialRules.Step(x, MaterialGroup.Building, MarketRandom.Normal(9001UL, 1, i, c), MarketRandom.Normal(9002UL, 2, i, c));
            }
            sum += MaterialRules.Price(m.Base, 1000, x);
        }
        double mean = sum / n;
        Check(Math.Abs(mean / forward - 1.0) < 0.004, "materials: a building material's forward is what its price comes to on average (" + (mean / forward).ToString("F4") + ")");
        // Settled in cash, never delivered, and traded without the city's economy, which only writes the game's goods.
        MarketEngine e = DerivMarket(631, 100000000L, out MarketWorld world);
        e.KnowMaterials(DerivCatalogue.BuildingNames.ToList(), DerivCatalogue.BuildingBaseMicros.ToList());
        SessionDay(e, 1, 10);
        int[] days = DerivDays(e);
        e.MaterialsLive = false;
        DerivContract cement = Buy(e, ContractKind.Long, DerivCatalogue.BuildingNames[0], 0, days[1], 0, 500, "Main Street#1");
        Check(cement != null && cement.Warehouse.Length == 0 && BuyWhy(e, ContractKind.Long, Wheat, 0, days[1], 0, 500) == DerivWhy.NoCity && e.MaterialOf(DerivCatalogue.BuildingNames[3]) != null,
            "materials: a building material's future is bought without the city's economy and never delivered, while a game good's waits for the economy");
        e.MaterialsLive = true;
    }

    // ---------------------------------------------------------------- a hedge's rules

    private static void HedgeRuleChecks()
    {
        int[] days = { 5, 12, 19, 26 };
        Check(HedgeRules.SettleFor(4, days, 4) == -1 && HedgeRules.SettleFor(5, days, 4) == -1 && HedgeRules.SettleFor(6, days, 4) == 5 && HedgeRules.SettleFor(13, days, 4) == 12
              && HedgeRules.SettleFor(40, days, 4) == 26 && HedgeRules.SettleFor(13, days, 0) == -1,
            "hedging: a purchase is hedged to the last Friday before it, too near before the first, to the last after it");
        Check(HedgeRules.Units(450000, 450 * DerivCatalogue.Micro) == 1000 && HedgeRules.Units(0, 5) == 0 && HedgeRules.Units(5, 0) == 0 && HedgeRules.Units(1000, 3 * DerivCatalogue.Micro) == 333,
            "hedging: units are the money at today's price, to the nearest");
        string cement = DerivCatalogue.BuildingNames[0], steel = DerivCatalogue.BuildingNames[1];
        Func<string, long> price = name => name == cement ? 450 * DerivCatalogue.Micro : name == steel ? 1600 * DerivCatalogue.Micro : 0;
        var needs = new List<HedgeNeed>
        {
            new HedgeNeed { Material = steel, Day = 9, Money = 160000 }, new HedgeNeed { Material = cement, Day = 9, Money = 90000 },
            new HedgeNeed { Material = cement, Day = 8, Money = 45000 }, new HedgeNeed { Material = cement, Day = 15, Money = 45000 },
            new HedgeNeed { Material = cement, Day = 3, Money = 9000 }, new HedgeNeed { Material = "ba:itemname_nothing", Day = 9, Money = 7000 },
        };
        var legs = new List<HedgeLeg>();
        int n = HedgeRules.Plan(needs, days, 4, price, legs, out long near, out long unpriced);
        Check(n == 3 && legs[0].Material == cement && legs[0].SettleDay == 5 && legs[0].Units == 300 && legs[0].Money == 135000 && legs[1].Material == cement && legs[1].SettleDay == 12
              && legs[1].Units == 100 && legs[2].Material == steel && legs[2].Units == 100 && near == 9000 && unpriced == 7000,
            "hedging: purchases of a material for the same Friday are one future, sized to their money; too near and not priced are said");
        Check(HedgeRules.Fit(legs, name => name == cement ? 350 : 1000) == 50 && legs.Count == 3 && legs[0].Units == 300 && legs[1].Units == 50 && legs[1].Money == 22500
              && HedgeRules.Fit(legs, name => 0) == 450 && legs.Count == 0, "hedging: a hedge is held to the units still free on each material");
        // The money: whatever the price does, the purchase less the future's result costs what the future fixed.
        bool fixedCost = true;
        long money = 360000, now = 450 * DerivCatalogue.Micro, f = 455 * DerivCatalogue.Micro;
        long u = HedgeRules.Units(money, now);
        foreach (double move in new[] { 0.7, 0.9, 1.0, 1.1, 1.4 })
        {
            long settle = (long)Math.Round(now * move);
            double purchase = money * (double)settle / now;
            double gain = DerivRules.Move(ContractKind.Long, u, f, settle) / 100.0;
            fixedCost &= Math.Abs(purchase - gain - money * (double)f / now) < 2.0;
        }
        Check(fixedCost, "hedging: a purchase hedged costs the future's price whatever the price does at its day");
        Check(HedgeRules.Saved(12345) == 123 && HedgeRules.Saved(-12345) == -124 && HedgeRules.Saved(0) == 0, "hedging: what a hedge saved is rounded against the player");
    }

    private static void HedgeEngineChecks()
    {
        MarketEngine e = DerivMarket(641, 50000000L, out MarketWorld world);
        e.KnowMaterials(DerivCatalogue.BuildingNames.ToList(), DerivCatalogue.BuildingBaseMicros.ToList());
        RunTo(e, 1, MarketCatalogue.PhaseResults);
        int[] days = DerivDays(e);
        var legs = new List<HedgeLeg>
        {
            new HedgeLeg { Material = DerivCatalogue.BuildingNames[0], SettleDay = days[0], Units = 800, Money = 360000 },
            new HedgeLeg { Material = DerivCatalogue.BuildingNames[1], SettleDay = days[1], Units = 300, Money = 480000 }
        };
        var ids = new List<int>();
        Check(e.Hedge(legs, e.Trading, false, ids, out DerivWhy shut) == 0 && shut == DerivWhy.Closed && ids.Count == 0, "hedging: no hedge is bought while the market is shut");
        long cash = world.Purse.Money;
        Check(e.Hedge(legs, false, true, ids, out DerivWhy test) == 2 && test == DerivWhy.None && e.State.Derivs.Contracts.All(c => c.Test && c.Kind == ContractKind.Long && c.Warehouse.Length == 0),
            "hedging: the test tools' hedge is bought outside a session, marked as theirs");
        int day = SessionDay(e, days[0] - 3, 30);
        RunTo(e, days[1] + 1);   // closes with their moves, the first settled at its day
        int undone = e.UndoTests(out long back);
        Check(undone == 2 && e.State.Derivs.Contracts.Count == 0 && Math.Abs(world.Purse.Money - cash) <= 2 && e.State.Derivs.Balance == 0,
            "hedging: the tests' hedge undone puts the cash back as it was, to the broker's rounding (" + (world.Purse.Money - cash) + ")");
        // A real hedge: in a session, through the margin account, as any future.
        day = SessionDay(e, e.State.Day + 1, 10);
        days = DerivDays(e);
        legs[0] = new HedgeLeg { Material = legs[0].Material, SettleDay = days[0], Units = 800, Money = 360000 };
        legs[1] = new HedgeLeg { Material = legs[1].Material, SettleDay = days[1], Units = 300, Money = 480000 };
        long margin = e.HedgeCost(legs, out long fees);
        cash = world.Purse.Money;
        ids.Clear();
        int bought = e.Hedge(legs, e.Trading, false, ids, out DerivWhy why);
        Check(bought == 2 && why == DerivWhy.None && world.Purse.Money == cash - margin - fees && e.State.Derivs.Balance == margin * 100
              && e.State.Derivs.Contracts.All(c => !c.Test), "hedging: a hedge bought in a session takes its margin into the account and its commission, as any future (" + bought + ", " + why
                                                              + ", day " + e.State.Day + " phase " + e.State.Phase + ", " + string.Join("/", days) + ")");
        Check(e.SettleNow(new List<int> { ids[0] }) == 1 && !e.State.Derivs.Contract(ids[0]).IsOpen && e.State.Derivs.Contract(ids[1]).IsOpen,
            "hedging: the tests settle only the futures they are given");
        var tooMuch = new List<HedgeLeg> { new HedgeLeg { Material = DerivCatalogue.BuildingNames[2], SettleDay = days[0], Units = DerivCatalogue.MaterialUnitsMost + 1, Money = 1 } };
        Check(e.Hedge(tooMuch, e.Trading, false, null, out DerivWhy over) == 0 && over == DerivWhy.TooMuch, "hedging: the exchange's limit on one material holds for a hedge");
        Check(e.MaterialRoom(DerivCatalogue.BuildingNames[1]) == DerivCatalogue.MaterialUnitsMost - 300 && e.MaterialNow(DerivCatalogue.BuildingNames[1]) > 0 && e.MaterialNow("none") == 0,
            "hedging: the room left on a material and its price now are the book's and the exchange's");
    }

    // ---------------------------------------------------------------- a project's exposure

    private static ProjectFile HedgedProject()
    {
        var p = new ProjectFile
        {
            Id = 7, Kind = ProjectKind.Plot, Stage = ProjectStage.Ready, Use = BuildUse.Homes, Area = 2400, Storeys = 6, PlansDone = true, Planning = PlanningState.Granted
        };
        p.Hours = PlansRules.Hours(p.Use, p.Area, p.Storeys, p.Kind);
        return p;
    }

    private static void ExposureChecks()
    {
        int[] exchange = { 1000, 1000, 1000, 1000, 1000 };
        var needs = new List<MaterialNeed>();
        ProjectFile p = HedgedProject();
        int n = ExposureRules.Of(p, 10, 1000, exchange, 1000, needs);
        bool loads = true;
        foreach (MaterialNeed need in needs)
        {
            loads &= need.Tenths == ExposureRules.StageNeed(p, need.Stage, need.Material)
                     && need.Money == PlansRules.Cost(need.Tenths, PlansRules.LoadPrice(need.Material, 1000, 1000, BuildCatalogue.MerchantPricePermille[0])) && need.Day > 10;
        }
        int foundation = needs.Where(x => x.Stage == WorkStage.Foundation).Select(x => x.Day).DefaultIfEmpty(0).Max();
        int frame = needs.Where(x => x.Stage == WorkStage.Frame).Select(x => x.Day).DefaultIfEmpty(0).Min();
        int walls = needs.Where(x => x.Stage == WorkStage.Envelope).Select(x => x.Day).DefaultIfEmpty(0).Min();
        Check(n > 0 && loads && needs.All(x => x.Stage != WorkStage.Ground) && foundation > 10 && frame > foundation && walls > frame,
            "hedging: a project ready to build will buy each stage's loads, the very ones its site asks for, on the days its stages begin (" + foundation + ", " + frame + ", " + walls + ")");
        long all = needs.Sum(x => x.Money);
        ExposureRules.Of(p, 10, 1000, exchange, 950, needs);
        Check(Math.Abs(needs.Sum(x => x.Money) - all * 950 / 1000) <= needs.Count, "hedging: the company's speciality's saving is in the exposure");
        ExposureRules.Of(p, 10, 1000, new[] { 1200, 1000, 1000, 1000, 1000 }, 1000, needs);
        Check(needs.Where(x => x.Material == Material.Cement).Sum(x => x.Money) > 0 && Math.Abs(ExposureRules.Total(needs, Material.Cement, out int tenths) - needs.Where(x => x.Material == Material.Cement).Sum(x => x.Money)) == 0
              && tenths > 0, "hedging: the exposure follows the exchange's move and adds up by material");
        // Building, in its frame: what it lacks and has not ordered, now; the stages after it to come.
        p.Stage = ProjectStage.Building;
        p.StartDay = 10;
        p.DueDay = PlanningRules.AfterWorkingDays(10, 18);
        p.StageSeen = (int)WorkStage.Frame;
        for (int m = 0; m < (int)Material.Count; m++)
        {
            p.Need[m] = ExposureRules.StageNeed(p, WorkStage.Frame, (Material)m);
            p.Have[m] = p.Need[m] / 3;
            p.OnOrder[m] = p.Need[m] / 3;
        }
        ExposureRules.Of(p, 20, 1000, exchange, 1000, needs);
        Check(needs.Where(x => x.Stage == WorkStage.Frame).All(x => x.Day == 20 && x.Tenths == p.Need[(int)x.Material] - p.Have[(int)x.Material] - p.OnOrder[(int)x.Material])
              && needs.All(x => x.Stage >= WorkStage.Frame) && needs.Any(x => x.Stage == WorkStage.Envelope && x.Day > 20),
            "hedging: a stage under way is exposed by what it lacks and has not ordered (an order is paid), at once; the stages after it later");
        p.Stage = ProjectStage.Finished;
        Check(ExposureRules.Of(p, 20, 1000, exchange, 1000, needs) == 0 && needs.Count == 0, "hedging: a finished project buys nothing more");
        Check(ExposureRules.WorkingDaysBetween(1, 8) == 5 && ExposureRules.WorkingDaysBetween(5, 5) == 0, "hedging: working days are counted as the site's");
        // A hedge's futures folded as they end, kept when the exchange does not answer.
        var q = new ProjectFile { Id = 8 };
        q.Hedges.Add(new HedgeHold { Contract = 1, Result = 100 });
        q.Hedges.Add(new HedgeHold { Contract = 2, Result = 200 });
        q.Hedges.Add(new HedgeHold { Contract = 3, Result = 300 });
        int ended = ExposureRules.Fold(q, id => id == 1 ? new HedgeLook { Known = true, Open = true, Result = 150 } : id == 2 ? new HedgeLook { Known = true, Open = false, Result = -50 }
            : new HedgeLook { Known = false });
        Check(ended == 2 && q.Hedges.Count == 1 && q.Hedges[0].Result == 150 && q.HedgeEnded == -50 + 300 && q.HedgeCents == 400,
            "hedging: an ended future's result is kept, one gone from the book its last, an open one followed");
    }

    // ---------------------------------------------------------------- the construction line's new parts

    private static void BuildBooksChecks()
    {
        var s = new BuildState { Seed = 5, NextId = 20 };
        s.TakeIn(1000, 10);
        s.PayOut(300, 10);
        s.PayOut(200, 12);
        s.TakeIn(0, 12);
        Check(s.Firm.Earned == 1000 && s.Firm.Spent == 500 && s.Books.Count == 2 && s.BooksOver(10, 11, out long e1, out long o1) == 1 && e1 == 1000 && o1 == 300
              && s.BooksOver(0, 100, out long e2, out long o2) == 2 && e2 == 1000 && o2 == 500 && s.BooksOver(13, 20, out _, out _) == 0,
            "books: the company's takings and costs are kept by day as they are counted");
        for (int d = 13; d < 13 + ExposureRules.BooksDays + 30; d++)
        {
            s.PayOut(1, d);
        }
        Check(s.Books.Count <= ExposureRules.BooksDays && s.Books[0].Day >= 13 + 30 - 1, "books: a year and more of days is kept, no more");
        ProjectFile p = HedgedProject();
        p.HedgeDay = 33;
        p.HedgeEnded = -1234;
        p.HedgeTest = true;
        p.Hedges.Add(new HedgeHold { Contract = 41, Result = 560 });
        p.Hedges.Add(new HedgeHold { Contract = 42, Result = -70 });
        s.Projects.Add(p);
        string line = BuildStore.Write(s);
        BuildState back = BuildStore.Read(line, out _);
        ProjectFile r = back != null ? back.Project(7) : null;
        Check(back != null && BuildStore.Write(back) == line && r != null && r.HedgeDay == 33 && r.HedgeEnded == -1234 && r.HedgeTest && r.Hedges.Count == 2 && r.Hedges[1].Contract == 42
              && r.Hedges[1].Result == -70 && back.Books.Count == s.Books.Count, "books: the line keeps a project's hedge and the company's books, read back to the same line");
        string older = line.Substring(0, line.IndexOf("^k:", StringComparison.Ordinal));
        older = Regex.Replace(older, @"\|33\|-1234\|1\|41:560,42:-70", string.Empty);
        BuildState old = BuildStore.Read(older, out _);
        Check(old != null && old.Books.Count == 0 && old.Project(7) != null && old.Project(7).HedgeDay == 0 && old.Project(7).Hedges.Count == 0,
            "books: a line from before reads with no books and no hedge");
    }

    // ---------------------------------------------------------------- a construction company on the market

    private static void ListingBuilderChecks()
    {
        int trade = MarketInputs.TradeOf("reallife:businesstype_construction");
        Check(trade == MarketInputs.Trades.Length - 1 && MarketInputs.Trades[trade] == "construction" && MarketInputs.Trades[trade - 1] == "bank"
              && MarketInputs.TradeOf("ba:businesstype_bank") == trade - 1, "listing: the construction company's trade is the market's newest, the others as they were");
        MarketEngine m = BuildersMarket(651, out _);
        Stock first = m.State.Stocks.FirstOrDefault(s => s.Listed && s.Own == null && s.Def.Trade == MarketInputs.Trades[trade]);
        Check(first != null && first.Sector == MarketSector.Construction, "listing: a company whose largest business is a construction company is placed in the construction sector");
    }

    // ---------------------------------------------------------------- the sources and the shared files

    private static void MarketsBuildSources()
    {
        string builders = ModSource(@"Markets\BuilderMarket.cs"), engine = ModSource(@"Markets\MarketEngine.Builders.cs"), hedgeEngine = ModSource(@"Markets\MarketEngine.Hedge.cs");
        string hedgeRules = ModSource(@"Markets\Derivatives\HedgeRules.cs"), exposure = ModSource(@"Construction\ExposureRules.cs");
        foreach (KeyValuePair<string, string> f in new Dictionary<string, string>
                 {
                     { "BuilderMarket.cs", builders }, { "MarketEngine.Builders.cs", engine }, { "MarketEngine.Hedge.cs", hedgeEngine }, { "HedgeRules.cs", hedgeRules }, { "ExposureRules.cs", exposure }
                 })
        {
            Check(f.Value.Length > 0 && !Regex.IsMatch(f.Value, @"\bUnityEngine\b|\bRandom\.|DateTime|TickCount|\bTime\.|using System\.Linq|SaveGameManager"), "builders sources: " + f.Key + " is game-free and draws from no clock");
        }
        string director = ModSource(@"Markets\MarketDirector.Builders.cs"), hedge = ModSource(@"Markets\MarketDirector.Hedge.cs");
        string build = ModSource(@"Construction\ConstructionDirector.Hedge.cs"), app = ModSource(@"Construction\ConstructionApp.Hedge.cs");
        string all = director + hedge + build + app + builders + engine + hedgeEngine + hedgeRules + exposure;
        Check(!Regex.IsMatch(all, @"void Update\(|void LateUpdate\(|FindObjectsOfType|FindObjectsByType|\bHarmony\b|MPClient|MPServer|\.Where\(|\.Select\("),
            "builders sources: no frame of their own, no scene search, no patch, nothing sent, no LINQ");
        Check(!Regex.IsMatch(build + app + exposure, @"\b(MarketDirector|MarketEngine|BuilderMarket|HedgeRules|DerivCatalogue)\b"),
            "builders sources: the construction game meets the market through its bridges only");
        Check(ModSource(@"Markets\MarketDirector.cs").Contains("BuildersSecond();") && MethodText(director, "private void BuildersSecond()").Contains("_engine.Event(MarketInputs.EventBuilder")
              && MethodText(director, "private void BuildersSecond()").Contains("BuilderMarket.Clear();"),
            "builders sources: the news goes onto the tape once a second, and is let go with the option off");
        Check(ModSource(@"Markets\MarketEngine.cs").Contains("BuilderEvent(e);") && ModSource(@"Markets\MarketEngine.Close.cs").Contains("BuildersClose(day);"),
            "builders sources: the engine plays the builders' events and draws their own news at the close");
        string market = ModSource(@"Construction\ConstructionDirector.Market.cs").Replace("\r", string.Empty), sites = ModSource(@"Construction\ConstructionDirector.Sites.cs");
        Check(market.Contains("RivalTenderWon = " + (int)BuilderNewsKind.TenderWon + ", RivalFinished = " + (int)BuilderNewsKind.Finished + ", RivalLate = " + (int)BuilderNewsKind.Late
                              + ", RivalDeveloped = " + (int)BuilderNewsKind.Developed + ", RivalRigged = " + (int)BuilderNewsKind.Rigged + ", RivalAward = " + (int)BuilderNewsKind.Award) && MethodText(market, "private void Award(").Contains("RivalNews(won.Bidder, RivalTenderWon, WeightOf(t));")
              && MethodText(market, "private void FinishDevelopment(").Contains("if (!dev.Public)\n            {\n                RivalNews(rival, RivalDeveloped, 1);") && MethodText(sites, "public void SiteHanded(").Contains("RivalHanded(siteId, day);")
              && MethodText(ModSource(@"Construction\ConstructionDirector.Rings.cs"), "private void RingFound(").Contains("RivalNews(r, RivalRigged, 1);")
              && ModSource(@"Construction\ConstructionDirector.Award.cs").Contains("RivalNews(e.Builder, RivalAward, 1);"),
            "builders sources: a tender's award, a rival's works handed over and a development finished tell the market, by its kinds' numbers");
        Check(sites.Contains("p.Need[m] = ExposureRules.StageNeed(p, stage, (Material)m);"), "hedging sources: a stage's loads and the exposure are one rule");
        string wiring = ModSource(@"Wiring\Wiring.ConstructionLinks.cs"), links = ModSource(@"Wiring\Wiring.MarketsBuildLinks.cs"), listing = ModSource(@"Wiring\ListingLinks.cs");
        Check(wiring.Contains("Markets.MarketDirector.Instance.MaterialMove(item)") && !wiring.Contains("Markets.MarketDirector.Instance.MaterialFactor(item)"),
            "builders sources: a load's list price follows the price level and the exchange gives its move alone, so the level is not counted twice");
        Check(MethodText(links, "internal static void RivalNews(").Contains("BuilderMarket.Add(rival, (BuilderNewsKind)kind, weight);")
              && MethodText(listing, "private static OwnBooks Statements(").Contains("MarketsBuildLinks.ConstructionBooks(Businesses, from, to, ref revenue, ref profit);")
              && ModSource(@"Wiring\MarketLinks.cs").Contains("MarketBridges.BuilderBooks = MarketsBuildLinks.BuilderTakings;"),
            "builders sources: the builder news list is fed through the wiring, and the listing counts the construction company's books");
        string hedgeMethod = MethodText(hedge, "public bool HedgePlan(");
        Check(hedgeMethod.Contains("DerivReady()") && hedgeMethod.Contains("HedgeRules.Fit(legs, _roomNow)") && hedgeMethod.Contains("DerivCatalogue.ContractsMost")
              && MethodText(hedgeEngine, "public int Hedge(").Contains("OpenContract(ContractKind.Long, leg.Material, 0, leg.SettleDay, 0, leg.Units, null, null"),
            "hedging sources: a hedge is planned within the exchange's limits and bought as futures in cash, in a session (the tests' only past it)");
        // The shared files' lines.
        string settings = ModSource(@"Core\Settings.cs"), tree = ModSource(@"Meta\OptionsTree.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), guide = ModSource(@"Markets\BuildersGuide.cs");
        Check(settings.Contains("public static bool MarketsBuilders = true;") && settings.Contains("MarketsBuilders = Bool(modId, MarketsBuildersId, MarketsBuilders);")
              && tree.Contains("S(\"buildersmarket\").For(\"markets_builders\").Opt(\"markets_builders\", \"markets\")")
              && ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"markets_builders\", CityScope.AnyCity,") && ModSource(@"Meta\FeatureList.Economy.cs").Contains("Markets.BuildersGuide.Register();")
              && mod.Contains(".AddToggle(Settings.MarketsBuildersId, \"reallife_mkt_builders_opt\", Settings.MarketsBuilders,"),
            "builders sources: the option is on by default, read, in its section, in the city table, in the feature list and on the page");
        foreach (Match b in Regex.Matches(guide, @"\.Check\(""(reallife_mkt_(?:builders|hedge)_test_[a-z]+)"", ([A-Za-z.]+), ""([^""]+)"", RunPlace\.Here\)"))
        {
            string method = b.Groups[2].Value.Substring(b.Groups[2].Value.LastIndexOf('.') + 1);
            string body = MethodText(director + build, "public static void " + method + "()");
            Check(body.Length > 0 && !mod.Contains("\"" + b.Groups[1].Value + "\"") && (director + build).Contains(b.Groups[3].Value.Substring(b.Groups[3].Value.IndexOf(" (", StringComparison.Ordinal))),
                "builders sources: the test " + b.Groups[1].Value + " is in the test run, its button, a public static method (" + method + ") writing its line");
        }
        Check(Regex.Matches(guide, @"\.Check\(""reallife_mkt_(?:builders|hedge)_test_").Count == 7, "builders sources: the seven test entries are in the test run");
        foreach (string check in new[] { "Builders on the market check (a rival builder wins a tender now)", "Builders on the market check (a bribery scandal at a rival builder now)",
                     "Builders on the market check (put the builders back)", "Listing check (a company holding a construction business)" })
        {
            Check(director.Contains("\"" + check + "\""), "builders sources: a builder test writes its check line (" + check + ")");
        }
        foreach (string check in new[] { "Hedging check (hedge the test project)", "Hedging check (the test project's end)", "Hedging check (put the hedge tests back)" })
        {
            Check(build.Contains("const string test = \"" + check + "\";"), "hedging sources: a hedge test writes its check line (" + check + ")");
        }
        Check(build.Contains("GoingPublic.InSession") && director.Contains("GoingPublic.InSession") && !Regex.IsMatch(ModSource(@"Markets\MarketShare.cs"), @"BuilderMarket|BuilderNews|EventBuilder|Hedge"),
            "builders sources: the tests stay out of a Going Public session, and nothing new is sent (the builders' news is the tape's own event)");
    }

    // ---------------------------------------------------------------- the texts

    private static void MarketsBuildTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("builders on the market", words, codes))
        {
            return;
        }
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string file in new[]
                 {
                     @"Markets\BuilderMarket.cs", @"Markets\MarketDirector.Builders.cs", @"Markets\MarketDirector.Hedge.cs", @"Markets\BuildersGuide.cs", @"Markets\MarketText.cs",
                     @"Construction\ConstructionDirector.Hedge.cs", @"Construction\ConstructionApp.Hedge.cs", @"Wiring\Wiring.MarketsBuildLinks.cs"
                 })
        {
            foreach (Match m in Regex.Matches(ModSource(file), "\"(reallife_(?:mkt_news_builder|mkt_builders|mkt_hedge|bld_hedge|bld_unit|bld_button_hedge|bld_sec_exposure|bld_sec_hedge|bld_row_hedge|bld_confirm_hedge|bld_notice_hedge|meta_title_markets_builders|guide_markets_builders)[a-z_]*)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string name in DerivCatalogue.BuildingNames.Concat(new[] { DerivCatalogue.GroupKeys[(int)MaterialGroup.Building], "reallife_opt_tip_markets_builders" }))
        {
            keys.Add(name);
        }
        var missing = new List<string>();
        foreach (string k in keys)
        {
            for (int l = 0; l < 3; l++)
            {
                if (words[l] == null || !words[l].TryGetValue(k, out string v) || string.IsNullOrWhiteSpace(v) || (l == 2 && !Regex.IsMatch(v, @"\p{IsCJKUnifiedIdeographs}")))
                {
                    missing.Add(k + " (" + codes[l] + ")");
                }
            }
        }
        Check(keys.Count >= 50 && missing.Count == 0, "builders texts: every text is in en, pt and zh-cn (" + keys.Count + " keys; missing: " + string.Join(", ", missing) + ")");
        // Each sentence keeps its names in every language.
        var lost = new List<string>();
        foreach (string k in keys)
        {
            if (!words[0].TryGetValue(k, out string en))
            {
                continue;
            }
            foreach (Match p in Regex.Matches(en, @"\{[a-z_]+\}"))
            {
                for (int l = 1; l < 3; l++)
                {
                    if (words[l] != null && words[l].TryGetValue(k, out string v) && !v.Contains(p.Value))
                    {
                        lost.Add(k + " " + p.Value + " (" + codes[l] + ")");
                    }
                }
            }
        }
        Check(lost.Count == 0, "builders texts: every name in a sentence is in each language's (" + string.Join(", ", lost) + ")");
        Check(keys.Where(k => k.StartsWith("reallife_mkt_news_builder", StringComparison.Ordinal) || k.StartsWith("reallife_bld_notice_hedge", StringComparison.Ordinal))
                  .All(k => words[0][k].Length <= 240), "builders texts: a notification is 240 characters at most");
        // What's new: the feature line in all nine, before the first fix mark of its row.
        var lines = new Dictionary<string, string>
        {
            { "en", "The city's builders on the stock market" }, { "pt", "As construtoras da cidade na bolsa de valores" }, { "zh-cn", "股市上的城里建筑商" },
            { "de", "Die Baufirmen der Stadt an der Börse" }, { "fr", "Les entreprises de construction de la ville en bourse" }, { "es", "Las constructoras de la ciudad en bolsa" },
            { "ru", "Строительные компании города на бирже" }, { "nl", "De bouwbedrijven van de stad op de beurs" }, { "tr", "Şehrin inşaat şirketleri borsada" }
        };
        var marks = new Dictionary<string, string>
        {
            { "en", "Fixed: " }, { "pt", "Corrigido: " }, { "zh-cn", "已修复：" }, { "de", "Behoben: " }, { "fr", "Corrigé : " }, { "es", "Corregido: " }, { "ru", "Исправлено: " },
            { "nl", "Opgelost: " }, { "tr", "Düzeltildi: " }
        };
        var lacking = new List<string>();
        foreach (KeyValuePair<string, string> line in lines)
        {
            Dictionary<string, string> own = LoadWords(System.IO.Path.Combine(LocalesFolder(), line.Key + ".json"));
            string row = own != null ? own.Where(kv => kv.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && kv.Value.Contains(line.Value)).Select(kv => kv.Value).FirstOrDefault() : null;
            int fix = row != null ? row.IndexOf(marks[line.Key], StringComparison.Ordinal) : -1;
            if (row == null || (fix >= 0 && fix < row.IndexOf(line.Value, StringComparison.Ordinal)))
            {
                lacking.Add(line.Key);
            }
        }
        Check(lacking.Count == 0, "builders texts: What's new has the line in all nine languages, before the fixes (" + string.Join(", ", lacking) + ")");
    }

    // ---------------------------------------------------------------- the cost

    private static void MarketsBuildCost()
    {
        int[] exchange = { 1000, 1000, 1000, 1000, 1000 };
        ProjectFile p = HedgedProject();
        var needs = new List<MaterialNeed>(24);
        var hedgeNeeds = new List<HedgeNeed>(24);
        var legs = new List<HedgeLeg>(8);
        int[] days = { 5, 12, 19, 26 };
        Func<string, long> price = name => 500 * DerivCatalogue.Micro;
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            ExposureRules.Of(p, 2, 1000, exchange, 1000, needs);
            hedgeNeeds.Clear();
            foreach (MaterialNeed n in needs)
            {
                hedgeNeeds.Add(new HedgeNeed { Material = BuildCatalogue.ExchangeItem[(int)n.Material], Day = n.Day, Money = n.Money });
            }
            HedgeRules.Plan(hedgeNeeds, days, 4, price, legs, out _, out _);
        }
        double each = watch.Elapsed.TotalMilliseconds / 1000.0;
        Check(each < 0.2, "builders cost: a project's exposure and hedge plan take under 0.2 ms (" + each.ToString("F4") + " ms), asked on a page or a press only");
    }
}
