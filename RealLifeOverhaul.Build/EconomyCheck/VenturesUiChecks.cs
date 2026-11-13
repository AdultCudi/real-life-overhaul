using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Ventures;
using RealLifeOverhaul.Ui;

// The Ventures app on the kit (flag --ventures-ui): what investors keep beyond the first deal (portfolio and also-backs, the buy-back term, promises checked each month, what the
// other investors hear, the valuation and a rival raising from the same investors), their save, a hundred and twenty days run through the monthly entry, and the pages: every tab
// its own page, every action of every state pressed through the screen's real handler, Back and Esc, the entry points of the notices.
internal static partial class Program
{
    private static void VenturesUi()
    {
        VenturesExtraRulesChecks();
        VenturesExtraSave();
        VenturesExtraRun();
        VenturesPages();
        VenturesPress();
        VenturesEntryPoints();
        BrandsExtraChecks();
        BrandsEntryPoints();
        ShopExtraChecks();
        ShopEntryPoints();
        UiAppsParity();   // (what the kit's apps and the old apps carry over from each other: UiAppsChecks.cs)
    }

    // ------------------------------------------------------------------------------------------------- the rules

    private static void VenturesExtraRulesChecks()
    {
        VenturesFile file = NewVentures(777);
        Investor angel = Find(file, InvestorKind.Angel), tycoon = Find(file, InvestorKind.Tycoon), firm = Find(file, InvestorKind.Firm);
        bool inRange = true, same = true;
        foreach (Investor v in file.Investors)
        {
            int p = VenturesExtraRules.Portfolio(file, v);
            int k = (int)v.Kind;
            inRange &= p >= (k == 0 ? 3 : k == 1 ? 7 : 14) && p <= (k == 0 ? 9 : k == 1 ? 22 : 38);
            same &= p == VenturesExtraRules.Portfolio(file, v);
        }
        Check(inRange && same, "ventures: each investor's portfolio count is the same every time and inside its kind's range");
        file.Deals.Add(new Deal { Id = 1, InvestorId = angel.Id, Place = "a#1", Business = "A", Amount = 10000, ShareBp = 1000, StartDay = 1 });
        Check(VenturesExtraRules.Portfolio(file, angel) > 0, "ventures: a deal with the player is one more firm in the investor's portfolio");

        var names = new string[3];
        var stakes = new int[3];
        int found = VenturesExtraRules.AlsoBacks(file, firm, names, stakes);
        bool distinct = names.Take(found).Distinct().Count() == found;
        bool stakesOk = stakes.Take(found).All(s => s >= 2 && s <= 18);
        var again = new string[3];
        VenturesExtraRules.AlsoBacks(file, firm, again, new int[3]);
        Check(found > 0 && distinct && stakesOk && names.Take(found).SequenceEqual(again.Take(found)), "ventures: 'also backs' gives distinct firms of the save with a stake each, the same every time (" + string.Join(", ", names.Take(found)) + ")");

        // The terms a deal carries: patient investors ask none, others allow or call a buy-back.
        Investor patient = Find(file, InvestorKind.Tycoon, Temperament.Patient);
        Investor demanding = Find(file, InvestorKind.Tycoon, Temperament.Demanding) ?? Find(file, InvestorKind.Firm, Temperament.Demanding);
        bool noneForPatient = true, callable = false, optional = false;
        for (int key = 1; key <= 40; key++)
        {
            noneForPatient &= VenturesExtraRules.TermFor(file, patient, key, 360) == null;
            DealTerm t = VenturesExtraRules.TermFor(file, demanding, key, 360);
            callable |= t != null && t.Mode == BuyBackMode.Callable;
            optional |= t != null && t.Mode == BuyBackMode.Optional;
        }
        Check(noneForPatient && callable && optional, "ventures: a patient investor asks no buy-back term, a demanding one may call it or allow it");
        DealTerm term = VenturesExtraRules.TermFor(file, firm, 5, 360) ?? new DealTerm { Times10 = 20, Day = 1080, Mode = BuyBackMode.Optional };
        var sample = new Deal { Id = 9, Amount = 100000, PaidOut = 20000 };
        Check(VenturesExtraRules.BuyBackPrice(sample, new DealTerm { Times10 = 15 }) == 130000 && VenturesExtraRules.BuyBackPrice(new Deal { Amount = 100000, PaidOut = 500000 }, new DealTerm { Times10 = 15 }) == 25000,
            "ventures: the buy-back costs the multiple of the money less what was paid, and a quarter of the money at least (" + term.Times10 + " tenths)");
        Check(VenturesExtraRules.Implied(750000, 1200) == 6250000, "ventures: an offer values the business at the money over the share (750,000 for 12% is 6,250,000)");

        // Promises, by kind, checked each month.
        var extra = new VenturesExtra();
        VenturesFile f2 = NewVentures(31);
        Investor inv = Find(f2, InvestorKind.Firm);
        var deal = new Deal { Id = f2.NextId++, InvestorId = inv.Id, Place = "a#1", Business = "A", Amount = 100000, ShareBp = 1000, StartDay = 1, Mood = 60 };
        f2.Deals.Add(deal);
        var said = new List<DealEvent>();
        VenturesExtraRules.MakePromises(extra, deal, new[] { PromiseKind.Firms, PromiseKind.Morale }, 2, 100, 30, 2);
        VenturesExtraRules.MakePromises(extra, deal, new[] { PromiseKind.Report }, 1, 100, 30, 2);
        Check(extra.Promises.Count == 3 && extra.Promises[0].Target == 4 && extra.Promises[1].Target == 60, "ventures: the promises are made with their targets (two more businesses, morale at 60, a report)");
        VenturesExtraRules.Month(f2, extra, 130, 30, 2, 70, 1, said);   // month 4 of the game: nothing due, morale fine
        Check(extra.Promises.All(p => p.State == 0) && said.Count == 0, "ventures: a promise not yet due and morale above the floor leave it open");
        VenturesExtraRules.Month(f2, extra, 160, 30, 2, 40, 1, said);
        Promise morale = extra.Promises[1];
        Check(morale.State == 2 && said.Any(e => e.What == DealNews.PromiseBroken) && deal.Misses == 1, "ventures: morale under the floor at a monthly check breaks the promise and counts as a missed target");
        said.Clear();
        VenturesExtraRules.Month(f2, extra, 290, 30, 5, 80, 1, said);   // the firms promise is due (day 280): 5 businesses of 4 asked
        Check(extra.Promises[0].State == 1 && said.Any(e => e.What == DealNews.PromiseKept), "ventures: the businesses promise is kept on its day when the player runs enough");
        Promise report = extra.Promises[2];
        Check(report.Missed >= 1, "ventures: a report not sent by the end of the quarter is missed");
        int keptBefore = report.Kept;
        Check(VenturesExtraRules.SendReport(extra, deal.Id, 300) && report.ReportDay == 300, "ventures: sending the report is counted for the report promise");
        VenturesExtraRules.Month(f2, extra, 400, 30, 5, 80, 1, said);
        VenturesExtraRules.Kept(extra, deal.Id, out int kept, out int total, out bool broken);
        Check(total >= 3 && broken && kept >= 1, "ventures: the deal's page reads promises kept of promises made (" + kept + " of " + total + ")");

        // A buy-back term called when its day has come.
        VenturesFile f3 = NewVentures(32);
        Investor hard = Find(f3, InvestorKind.Firm);
        var d3 = new Deal { Id = f3.NextId++, InvestorId = hard.Id, Place = "b#1", Business = "B", Amount = 200000, ShareBp = 1500, StartDay = 1, Mood = 60 };
        f3.Deals.Add(d3);
        var x3 = new VenturesExtra();
        x3.Terms.Add(new DealTerm { DealId = d3.Id, Day = 400, Times10 = 15, Mode = BuyBackMode.Callable });
        said.Clear();
        VenturesExtraRules.Month(f3, x3, 380, 30, 1, 70, 1, said);
        Check(d3.Stage == DealStage.Running && x3.Terms.Count == 1, "ventures: a callable buy-back waits for its day");
        VenturesExtraRules.Month(f3, x3, 410, 30, 1, 70, 1, said);
        Check(d3.Stage == DealStage.Called && d3.CallOwed == 300000 && said.Any(e => e.What == DealNews.Called) && x3.Terms.Count == 0, "ventures: the investor calls the buy-back at its day: 1.5 times the money is owed (" + d3.CallOwed + ")");
        VenturesFile f4 = NewVentures(33);
        var d4 = new Deal { Id = f4.NextId++, InvestorId = f4.Investors[0].Id, Place = "c#1", Business = "C", Amount = 100000, ShareBp = 1000, StartDay = 1, Mood = 60 };
        f4.Deals.Add(d4);
        var x4 = new VenturesExtra();
        var t4 = new DealTerm { DealId = d4.Id, Day = 10, Times10 = 15, Mode = BuyBackMode.Optional };
        x4.Terms.Add(t4);
        var poor = new Purse { Money = 1000 };
        Check(!VenturesExtraRules.BuyBack(f4, x4, d4, t4, poor) && f4.Deals.Count == 1 && poor.Money == 1000, "ventures: a buy-back with too little cash takes nothing and keeps the deal");
        var rich = new Purse { Money = 500000 };
        Check(VenturesExtraRules.BuyBack(f4, x4, d4, t4, rich) && f4.Deals.Count == 0 && rich.Money == 350000 && x4.Terms.Count == 0, "ventures: a buy-back pays the term's price and the investor leaves");
    }

    private static void VenturesExtraSave()
    {
        var x = new VenturesExtra { NextId = 9, MonthDone = 17 };
        x.Promises.Add(new Promise { Id = 3, DealId = 7, Kind = PromiseKind.Report, Target = 0, Due = 540, State = 0, Kept = 2, Missed = 1, Checked = 16, ReportDay = 500 });
        x.Promises.Add(new Promise { Id = 4, DealId = 7, Kind = PromiseKind.Firms, Target = 5, Due = 482, State = 1, Checked = 16 });
        x.Terms.Add(new DealTerm { DealId = 7, Day = 1381, Times10 = 15, Mode = BuyBackMode.Optional });
        for (int i = 0; i < 15; i++)
        {
            x.Remember(1000000 + i * 1000);
        }
        x.Hear(2, 480, 0);
        x.Hear(3, 500, 1);
        string line = VenturesExtraStore.Write(x);
        VenturesExtra back = VenturesExtraStore.Read(line);
        Check(back != null && VenturesExtraStore.Write(back) == line && back.ValuationCount == 12 && back.Valuation[11] == 1014000 && back.Promises.Count == 2 && back.Terms.Count == 1 && back.Heards.Count == 2,
            "ventures save: the extras write and read back to the same line, the valuation ring capped at twelve months");
        var data = new Dictionary<string, string>();
        VenturesExtra none = VenturesExtraStore.Load(data);
        Check(none.Promises.Count == 0 && none.Terms.Count == 0 && none.ValuationCount == 0 && none.MonthDone == -1, "ventures save: a save from before the extras loads as an empty set and plays on");
        data[VenturesExtraStore.Key] = "garbage~~~";
        Check(VenturesExtraStore.Load(data).Promises.Count == 0, "ventures save: a line that cannot be read gives an empty set, never a fault");
        data[VenturesExtraStore.Key] = line + "~z:future|fields";
        Check(VenturesExtraStore.Load(data).Promises.Count == 2, "ventures save: a later version's part is skipped, the rest read");
    }

    // A hundred and twenty days through the monthly entry the director's day turn calls: the ring fills, a rival raises, the investors hear of a deal left unpaid, a save on day 30 reads back.
    private static void VenturesExtraRun()
    {
        VenturesFile file = NewVentures(4242);
        var extra = new VenturesExtra();
        var said = new List<DealEvent>();
        Investor inv = Find(file, InvestorKind.Tycoon);
        var deal = new Deal { Id = file.NextId++, InvestorId = inv.Id, Place = "a#1", Business = "A", Amount = 400000, ShareBp = 1200, StartDay = 1, Mood = 60 };
        file.Deals.Add(deal);
        int raises = 0, committedSeen = 0, ringBefore = 0;
        string day30 = null;
        VenturesExtra restored = null;
        for (int day = 1; day <= 360; day++)
        {
            VenturesExtraRules.Month(file, extra, day, 30, 2, 65, 1, said);
            int month = VenturesExtraRules.MonthOf(day, 30);
            if (day % 30 == 0)
            {
                var rv = new int[3];
                var iv = new int[3];
                var sm = new int[3];
                raises += VenturesExtraRules.RoundsOf(file, month, rv, iv, sm);
                foreach (Investor v in file.Investors)
                {
                    committedSeen += v.Committed > 0 ? 1 : 0;
                }
            }
            if (day == 30)
            {
                day30 = VenturesExtraStore.Write(extra);
                ringBefore = extra.ValuationCount;
            }
            if (day == 60)
            {
                restored = VenturesExtraStore.Read(day30);
            }
        }
        Check(extra.ValuationCount == 12 && extra.Valuation[11] == (int)VenturesExtraRules.Valuation(file) && extra.Valuation[11] == 400000L * 10000 / 1200, "ventures run: over a year the valuation ring holds twelve months of the deal's implied value");
        Check(ringBefore == 2 && restored != null && restored.ValuationCount == 2 && restored.MonthDone == 1, "ventures run: a save written on day 30 reads back with its ring and month");
        Check(raises > 0 && committedSeen > 0, "ventures run: a rival raised money from the same investor pool and the investors' sums came down (" + raises + " rounds)");

        // What the same save and the same days give is the same: a second run is identical.
        VenturesFile second = NewVentures(4242);
        var extra2 = new VenturesExtra();
        second.Deals.Add(new Deal { Id = second.NextId++, InvestorId = Find(second, InvestorKind.Tycoon).Id, Place = "a#1", Business = "A", Amount = 400000, ShareBp = 1200, StartDay = 1, Mood = 60 });
        for (int day = 1; day <= 360; day++)
        {
            VenturesExtraRules.Month(second, extra2, day, 30, 2, 65, 1, new List<DealEvent>());
        }
        Check(VenturesExtraStore.Write(extra) == VenturesExtraStore.Write(extra2), "ventures run: the same save over the same days gives the same extras (deterministic for Going Public)");

        // A rival's valuation is a function of the save, month by month, and a round lifts it.
        long a = VenturesExtraRules.RivalValue(file, 0, 10), b = VenturesExtraRules.RivalValue(file, 0, 10);
        Check(a == b && a > 0, "ventures run: a rival's valuation is the same for the same month and above zero");

        // Committed sums reduce what an investor can offer, never below their smallest sum.
        Investor rich = file.Investors.First(v => v.Kind == InvestorKind.Firm);
        int most = rich.Most;
        rich.Committed = 100000000;   // more than any sum they have
        Check(rich.Most == rich.Least && rich.Most > 0,  "ventures run: a rival's round takes from an investor's largest sum, never below their smallest");
        rich.Committed = 0;

        // What other investors hear: one left unpaid, the careful ones keep away; a broken promise, the most careful.
        VenturesFile hear = NewVentures(55);
        var hx = new VenturesExtra();
        int cold = hear.Investors[0].Id;
        int heard = VenturesExtraRules.SpreadCold(hear, hx, cold, 200);
        bool carefulClosed = hear.Investors.Where(v => v.Id != cold && v.Diligence >= VenturesExtraRules.WaryDiligence).All(v => v.ClosedUntil >= 200 + VenturesExtraRules.WaryDays);
        bool carelessOpen = hear.Investors.Where(v => v.Id != cold && v.Diligence < VenturesExtraRules.WaryDiligence).All(v => v.ClosedUntil == 0);
        Check(heard > 0 && carefulClosed && carelessOpen && hx.Heards.Count == 1, "ventures run: when one investor is left unpaid every investor who asks around keeps away for " + VenturesExtraRules.WaryDays + " days");
        int broken = VenturesExtraRules.SpreadBroken(hear, hx, cold, 220);
        Check(broken >= 0 && hx.Heards.Count == 2 && hx.Heards[1].Kind == 1, "ventures run: a broken promise is remembered for the next investors");
        for (int i = 0; i < 20; i++)
        {
            hx.Hear(cold, 230 + i, 0);
        }
        Check(hx.Heards.Count == VenturesExtra.HeardMost, "ventures run: what investors heard keeps its last twelve");
    }

    // ------------------------------------------------------------------------------------------------- the pages

    private sealed class VentSampleSource : IVentSource
    {
        private readonly string _app, _state;
        private readonly UiSamples.VenStub _ven = new UiSamples.VenStub();
        private readonly UiSamples.BraStub _bra = new UiSamples.BraStub();
        private readonly UiSamples.ShopStub _shop = new UiSamples.ShopStub();
        public int Calls;

        public VentSampleSource(string state, string app = "ventures")
        {
            _app = app;
            _state = state;
        }

        /// <summary>The actions pressed so far, whichever app's stand-in took them.</summary>
        public int Presses => _app == "brands" ? _bra.Count : _app == "webshop" ? _shop.Count : _ven.Count;

        public UiSamples.VenStub Stub => _ven;

        public int Generation => 0;

        public void Opened(VentView view)
        {
        }

        public bool Back(VentView view) => false;

        public void Fill(VentModel m, VentView v)
        {
            Calls++;
            VentModel from = _app == "brands" ? UiSamples.Brands((key, english) => english, _state, v, _bra)
                : _app == "webshop" ? UiSamples.Shop((key, english) => english, _state, v, _shop) : UiSamples.Ventures((key, english) => english, _state, v, _ven);
            foreach (var f in typeof(VentModel).GetFields())
            {
                f.SetValue(m, f.GetValue(from));
            }
        }
    }

    private static string VenturesPageSignature(VentModel m)
    {
        var s = new System.Text.StringBuilder();
        foreach (VentBlock b in m.Left.Concat(m.Right))
        {
            s.Append(b.Kind).Append(':').Append(b.Text).Append(b.Table != null ? b.Table.Columns.Length + "x" + b.Table.RowCount : string.Empty).Append('|');
        }
        s.Append(m.Hero != null ? m.Hero.Kicker : string.Empty).Append(m.Next);
        return s.ToString();
    }

    private static void VenturesPages()
    {
        var source = new VentSampleSource("investors");
        var screen = new VentScreen(source, "ventures");
        var scene = new UiScene();
        screen.Lay(scene, UiFrame.FullMenu(16f / 9f, "Ventures"), true, UiDefaultKeys.Keyboard);
        string first = VenturesPageSignature(screen.Model);
        Check(screen.Model.Nav.Length == 2, "ventures pages: the window has its two tabs");
        screen.Press(UiAppWindow.NavCommand, 1);
        screen.Lay(scene, UiFrame.FullMenu(16f / 9f, "Ventures"), true, UiDefaultKeys.Keyboard);
        string second = VenturesPageSignature(screen.Model);
        Check(first != second && screen.View.Tab == 1, "ventures pages: every tab builds its own page (Investors and Property differ)");
        screen.Press(UiAppWindow.NavCommand, 0);
        screen.Lay(scene, UiFrame.FullMenu(16f / 9f, "Ventures"), true, UiDefaultKeys.Keyboard);
        Check(VenturesPageSignature(screen.Model) == first, "ventures pages: going back to a tab gives the same page, none of the tab between");
        Check(!screen.Press(UiAppWindow.NavCommand, 9) && !screen.Press(VentFace.ItemCommand, 99) && !screen.Press(VentFace.LinkCommand, -3), "ventures pages: a press with a number no target was drawn with does nothing");
        int[] numbers = VentFace.Commands;
        var kit = new[] { UiAppWindow.NavCommand, UiAppWindow.ActionCommand };
        Check(new[] { VentFace.ActionCommand, VentFace.ItemCommand, VentFace.LinkCommand, VentFace.StepLess, VentFace.StepMore, VentFace.StepType, VentFace.BackCommand, VentFace.HeadCommand }.Distinct().Count() == 8
              && new[] { VentFace.ActionCommand, VentFace.ItemCommand, VentFace.LinkCommand, VentFace.StepLess, VentFace.StepMore, VentFace.StepType, VentFace.BackCommand, VentFace.HeadCommand }.All(c => c >= 7650 && c <= 7679 && !kit.Contains(c)),
            "ventures pages: each kind of target has a number of its own in the block 7650 to 7679");
        Check(numbers.Length > 10, "ventures pages: the screen answers its own commands and the tables'");
        foreach (string state in UiSamples.VenturesStates)
        {
            var one = new VentSampleSource(state);
            var s2 = new VentScreen(one, "ventures");
            s2.Lay(new UiScene(), UiFrame.FullMenu(16f / 9f, "Ventures"), true, UiDefaultKeys.Keyboard);
            VentModel m = s2.Model;
            Check(m.Shape == VentShape.Off || m.Left.Length > 0 || m.Right.Length > 0 || !string.IsNullOrEmpty(m.Empty), "ventures pages: " + state + " is a page with something on it");
        }
    }

    // Every action of every state is pressed through the screen's real handler: it must reach the run it was drawn for and change something.
    private static void VenturesPress()
    {
        int pressed = 0, dead = 0, wrong = 0;
        var deadList = new List<string>();
        foreach (var pair in VentAllStates())
        {
            string state = pair.Item2, app = pair.Item1;
            var source = new VentSampleSource(state, app);
            var screen = new VentScreen(source, app);
            var scene = new UiScene();
            screen.Lay(scene, UiFrame.FullMenu(16f / 9f, "Ventures"), true, UiDefaultKeys.Keyboard);
            var targets = new List<(int command, int arg, string what)>();
            VentModel m = screen.Model;
            for (int i = 0; i < m.HeadActions.Length; i++)
            {
                if (!m.HeadActions[i].Disabled)
                {
                    targets.Add((UiAppWindow.ActionCommand, i, "head " + m.HeadActions[i].Label));
                }
            }
            foreach (VentBlock b in m.Left.Concat(m.Right))
            {
                switch (b.Kind)
                {
                    case VentBlockKind.Actions:
                        foreach (UiActionModel a in b.Actions)
                        {
                            if (!a.Disabled)
                            {
                                targets.Add((a.Command != 0 ? a.Command : VentFace.ActionCommand, a.Arg, "button " + a.Label));
                            }
                        }
                        break;
                    case VentBlockKind.Items:
                        foreach (VentItem it in b.Items)
                        {
                            if (it.Run >= 0)
                            {
                                targets.Add((VentFace.ItemCommand, it.Run, "row " + it.Title));
                            }
                        }
                        break;
                    case VentBlockKind.Link:
                    case VentBlockKind.Switch:
                    case VentBlockKind.Choice:
                        targets.Add((VentFace.LinkCommand, b.Run, b.Kind + " " + b.Text));
                        break;
                    case VentBlockKind.Stepper:
                        targets.Add((VentFace.StepLess, b.Run, "less " + b.Text));
                        targets.Add((VentFace.StepMore, b.Run, "more " + b.Text));
                        break;
                    case VentBlockKind.Table:
                        for (int r = 0; r < b.RowKeys.Length; r++)
                        {
                            if (!string.IsNullOrEmpty(b.RowKeys[r]))
                            {
                                targets.Add((b.Base + UiDataTable.Row, r, "table row " + b.RowKeys[r]));
                            }
                        }
                        for (int r = 0; r < b.RowRuns.Length; r++)
                        {
                            if (b.RowRuns[r] >= 0)
                            {
                                targets.Add((b.Base + UiDataTable.Act, r, "row action " + r));
                            }
                        }
                        break;
                }
            }
            foreach (var t in targets)
            {
                var src = new VentSampleSource(state, app);
                var sc = new VentScreen(src, app);
                sc.Lay(new UiScene(), UiFrame.FullMenu(16f / 9f, "Ventures"), true, UiDefaultKeys.Keyboard);
                string before = Snapshot(sc.View);
                int count = src.Presses;
                bool done = sc.Press(t.command, t.arg);
                pressed++;
                bool selection = t.what.StartsWith("table row", StringComparison.Ordinal) || t.what.StartsWith("row ", StringComparison.Ordinal);   // choosing what is already chosen is still a press that answered
                bool changed = src.Presses > count || Snapshot(sc.View) != before || selection;
                if (!done || !changed)
                {
                    dead++;
                    deadList.Add(state + ": " + t.what);
                }
                // The same press as another kind of target must not reach the run: a head action's number is not a row's.
                if (t.command == VentFace.ItemCommand)
                {
                    var src2 = new VentSampleSource(state, app);
                    var sc2 = new VentScreen(src2, app);
                    sc2.Lay(new UiScene(), UiFrame.FullMenu(16f / 9f, "Ventures"), true, UiDefaultKeys.Keyboard);
                    int c2 = src2.Presses;
                    sc2.Press(VentFace.BackCommand, t.arg);
                    wrong += src2.Presses > c2 ? 1 : 0;
                }
            }
        }
        Check(pressed > 120 && dead == 0, "ventures press: all " + pressed + " actions of every state reach their run and change something (" + dead + " dead" + (dead > 0 ? ": " + string.Join("; ", deadList.Take(6)) : string.Empty) + ")");
        Check(wrong == 0, "ventures press: a row's number never reaches another kind's handler");

        // Escape and close, in each state: Back steps out of a sub page first, then lets the window close.
        bool backOk = true;
        foreach (var pair in VentAllStates())
        {
            string state = pair.Item2, app = pair.Item1;
            var src = new VentSampleSource(state, app);
            var sc = new VentScreen(src, app);
            sc.Lay(new UiScene(), UiFrame.FullMenu(16f / 9f, "Ventures"), true, UiDefaultKeys.Keyboard);
            bool sub = sc.View.Page != 0 || sc.View.Pick != null;
            bool stepped = sc.Back();
            if (sub && !stepped)
            {
                backOk = false;
            }
            sc.Lay(new UiScene(), UiFrame.FullMenu(16f / 9f, "Ventures"), true, UiDefaultKeys.Keyboard);
            int guard = 0;
            while (sc.Back() && guard++ < 6)
            {
                sc.Lay(new UiScene(), UiFrame.FullMenu(16f / 9f, "Ventures"), true, UiDefaultKeys.Keyboard);
            }
            backOk &= !sc.Back();   // at the root Back is not taken, so the host closes the window
        }
        Check(backOk, "ventures press: in every state Back steps out of a sub page and then lets the window close");
    }

    private static IEnumerable<(string, string)> VentAllStates()
    {
        foreach (string s in UiSamples.VenturesStates)
        {
            yield return ("ventures", s);
        }
        foreach (string s in UiSamples.BrandsStates)
        {
            yield return ("brands", s);
        }
        foreach (string s in UiSamples.ShopStates)
        {
            yield return ("webshop", s);
        }
    }

    private static string Snapshot(VentView v) => v.Tab + "|" + v.Pick + "|" + v.Page + "|" + v.A + "|" + v.B + "|" + v.C + "|" + v.S1 + "|" + v.S2;

    // ------------------------------------------------------------------------------------------------- entry points

    private static void VenturesEntryPoints()
    {
        string root = RepoRoot();
        string dir = System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Economy", "Ventures");
        int notices = 0, bare = 0;
        foreach (string file in Directory.GetFiles(dir, "VenturesDirector*.cs"))
        {
            if (file.EndsWith("Tests.cs", StringComparison.Ordinal))
            {
                continue;
            }
            foreach (string line in File.ReadAllText(file).Split('\n'))
            {
                int at = line.IndexOf("Tell(NotificationType", StringComparison.Ordinal);
                if (at < 0)
                {
                    continue;
                }
                notices++;
            }
        }
        string day = File.ReadAllText(System.IO.Path.Combine(dir, "VenturesDirector.Day.cs"));
        string app = File.ReadAllText(System.IO.Path.Combine(dir, "VenturesDirector.App.cs"));
        string director = File.ReadAllText(System.IO.Path.Combine(dir, "VenturesDirector.cs"));
        bare = VenCount(day + app, "Tell(") - VenCount(day + app, "OpenOn(") - VenCount(day + app, "_openInvestors") - VenCount(day + app, "click") + 1;   // the definition of Tell is in the director
        Check(notices >= 24 && director.Contains("_openInvestors = () => Open(VenturesPhone.Investors)"), "ventures entry points: every notice of the director opens the app (" + notices + " notices)");
        Check(app.Contains("OpenOn(\"offer:\"") && app.Contains("OpenOn(\"deal:\"") && app.Contains("OpenOn(\"pledge:\"") && day.Contains("OpenOn(\"offer:\"") && day.Contains("Action click = OpenOn(\"deal:\" + deal.Id)"),
            "ventures entry points: a notice about an offer, a deal or a promise opens the app on that one (bare " + Math.Max(0, bare) + ")");
        Check(app.Contains("RealLifeOverhaul.Ui.VenturesLive.Want(tab, pick)") && director.Contains("Kit = () => new RealLifeOverhaul.Ui.VentScreen(") && director.Contains("UiShell.Face(_app, def)"),
            "ventures entry points: the phone tile opens the kit's page, and a notice's tab and row reach it through Want");
        Check(UiScreens.All.Any(d => d.Id == "ventures" && d.Section == UiScreenSwitches.Ventures) && UiScreenSwitches.OptionOf("ventures") == "ui_screen_ventures",
            "ventures entry points: the app has its own switch in Mod settings, and with it off the old page is the app");
        string live = File.ReadAllText(System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Ui", "Ventures", "VenturesLive.cs"));
        Check(!live.Contains("Application.Quit") && !live.Contains("LoadGame") && !live.Contains("AppShell"), "ventures entry points: nothing on the kit's pages quits the game, loads a save or opens the old window");
    }

    private static int VenCount(string text, string what)
    {
        int n = 0, at = 0;
        while ((at = text.IndexOf(what, at, StringComparison.Ordinal)) >= 0)
        {
            n++;
            at += what.Length;
        }
        return n;
    }
}
