using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ui;

// The Banks and Your bank screens on the kit (4 Nov 2026, the audit: "a facade"): every tab builds its own page from real figures, every button is
// pressed through the host and what it does is read back, and no figure is a constant. The Banks facts come from the samples (the credit desk is the
// game's), Your bank is read from a real OwnBank that a seeded city has run for days (OwnBankReader), so its pages follow the books.
internal static partial class Program
{
    private static bool _bankFaceChecked;

    // A source standing in for the game: the page is built from facts each time it is filled; every act is recorded and moves the generation.
    private sealed class BankStub : IMoneySource, IMoneyActs
    {
        private readonly Action<MoneyModel> _fill;
        private readonly Func<MoneyAct, MoneyView, int, int, bool> _apply;
        public readonly List<string> Acts = new List<string>();
        public MoneyAct LastAct;
        public int LastId, LastValue;
        public int Moved;

        public BankStub(Action<MoneyModel> fill, Func<MoneyAct, MoneyView, int, int, bool> apply = null)
        {
            _fill = fill;
            _apply = apply;
        }

        public int Generation => Moved;

        public void Fill(MoneyModel model) => _fill(model);

        public bool Press(MoneyModel model, int command, int arg) => false;

        public bool Do(MoneyAct act, MoneyView view, int id, int value)
        {
            Acts.Add(act + ":" + id + "," + value);
            LastAct = act;
            LastId = id;
            LastValue = value;
            Moved++;
            return _apply == null || _apply(act, view, id, value);
        }
    }

    private static UiScene LayBank(MoneyHost host)
    {
        var s = new UiScene();
        host.Lay(s, UiFrame.FullMenu(16f / 9f, "Banks"), true, UiDefaultKeys.Keyboard);
        return s;
    }

    private static UtilitiesWords BankWords() => UiSamples.UtilitiesWordsOf((key, english) => english);

    private static MoneyHost BanksHost(string state, out BankStub stub, Func<MoneyAct, MoneyView, int, int, bool> apply = null)
    {
        MoneyModel start = UiSamples.BanksModelOf(state);
        stub = new BankStub(m => BanksBuild.Fill(m, UiSamples.BanksFactsOf(state), BankWords()), apply);
        var host = new MoneyHost("banks", MoneyApp.Banks, UiIcon.Bank, stub);
        host.Model.Tab = start.Tab;
        host.Model.View.Row = start.View.Row;
        LayBank(host);
        return host;
    }

    private static MoneyHost OwnHost(string state, out BankStub stub)
    {
        MoneyModel start = UiSamples.OwnModelOf(state);
        stub = new BankStub(m => OwnBankBuild.Fill(m, UiSamples.OwnBankFactsOf(state), BankWords()));
        var host = new MoneyHost("own_bank", MoneyApp.OwnBank, UiIcon.PiggyBank, stub);
        host.Model.Tab = start.Tab;
        host.Model.View.Row = start.View.Row;
        LayBank(host);
        return host;
    }

    // What every control of a laid page does: a button of a card is an act (or a tab), a step moves a figure or asks an act, a row picks, the strip picks a bank.
    // Returns how many controls were pressed and how many did nothing (a dead button).
    private static void BankPressAll(string label, Func<BankStub, MoneyHost> make, ref int pressed, ref int dead, ref int unreasoned)
    {
        MoneyHost probe = make(null);
        MoneyModel shape = probe.Model;
        int cards = shape.Cards.Length;
        for (int k = 0; k < cards; k++)
        {
            for (int i = 0; i < shape.Cards[k].Actions.Length; i++)
            {
                UiActionModel a = shape.Cards[k].Actions[i];
                MoneyHost host = make(null);
                UiScene before = LayBank(host);
                int node = NodeOf(before, a.Command, a.Arg);
                if (node < 0)
                {
                    dead++;
                    Console.WriteLine("        " + label + ": the button " + a.Label + " has no target");
                    continue;
                }
                if (a.Disabled)
                {
                    // drawn disabled: its target is not pressable and the card says why (a line in a warning ink)
                    bool why = false;
                    foreach (UtilitiesLine line in shape.Cards[k].Lines)
                    {
                        why |= line.Label == null && (line.Ink == UiRole.WarnText || line.Ink == UiRole.DangerText);
                    }
                    if (!before.Nodes[node].Disabled || !why)
                    {
                        unreasoned++;
                        Console.WriteLine("        " + label + ": the disabled button " + a.Label + " is " + (before.Nodes[node].Disabled ? "" : "pressable, and ") + (why ? "" : "has no reason on its card"));
                    }
                    continue;
                }
                int tab = host.Model.Tab;
                int acts = ((BankStub)StubOf(host)).Acts.Count;
                bool done = host.Press(a.Command, a.Arg);
                bool acted = a.Command == MoneyCommands.Go ? host.Model.Tab == a.Arg : ((BankStub)StubOf(host)).Acts.Count == acts + 1 && ((BankStub)StubOf(host)).LastAct == (MoneyAct)(a.Arg & 0xFF);
                pressed++;
                if (!done || !acted)
                {
                    dead++;
                    Console.WriteLine("        " + label + ": the button " + a.Label + " did nothing (tab " + tab + ")");
                }
            }
            for (int i = 0; i < shape.Cards[k].Steps.Length && i < shape.CardSteps[k].Length; i++)
            {
                MoneyStep step = shape.CardSteps[k][i];
                for (int dir = 0; dir < 2; dir++)
                {
                    bool more = dir == 1;
                    if (more ? !step.CanMore : !step.CanLess)
                    {
                        continue;
                    }
                    MoneyHost host = make(null);
                    int arg = step.Typed ? k * 8 + i : i;
                    var stub = (BankStub)StubOf(host);
                    int acts = stub.Acts.Count;
                    bool done = host.Press((more ? MoneyCommands.CardMore : MoneyCommands.CardLess) + k, arg);
                    int to = more ? step.More : step.Less;
                    bool acted = step.Act == MoneyAct.None ? host.Model.View.F[step.Field] == to : stub.Acts.Count == acts + 1 && stub.LastAct == step.Act && stub.LastValue == to && stub.LastId == step.Id;
                    pressed++;
                    if (!done || !acted)
                    {
                        dead++;
                        Console.WriteLine("        " + label + ": step " + i + " of card " + k + (more ? " plus" : " minus") + " did nothing");
                    }
                }
                if (step.Typed)
                {
                    MoneyHost host = make(null);
                    host.Focused(MoneyCommands.TypeField, k * 8 + i);
                    host.Typed("123456");
                    host.Submitted("123456");
                    int want = Math.Max(step.Low, Math.Min(Math.Max(step.Low, step.High), 123456));
                    pressed++;
                    if (host.Model.View.F[step.Field] != want)
                    {
                        dead++;
                        Console.WriteLine("        " + label + ": the typed figure of step " + i + " of card " + k + " was not kept (" + host.Model.View.F[step.Field] + " not " + want + ")");
                    }
                }
            }
            if (k < shape.CardSwitchActs.Length && shape.CardSwitchActs[k] != 0)
            {
                MoneyHost host = make(null);
                var stub = (BankStub)StubOf(host);
                bool done = host.Press(MoneyCommands.CardSwitch + k, 0);
                pressed++;
                if (!done || stub.Acts.Count != 1 || stub.LastAct != (MoneyAct)shape.CardSwitchActs[k])
                {
                    dead++;
                    Console.WriteLine("        " + label + ": the switch of card " + k + " did nothing");
                }
            }
        }
        for (int r = 0; r < shape.RowIds.Length; r++)
        {
            if (string.IsNullOrEmpty(shape.RowIds[r]))
            {
                continue;
            }
            MoneyHost host = make(null);
            bool done = host.Press(UiDataTable.First + UiDataTable.Row, r);
            pressed++;
            if (!done || host.Model.View.Row != shape.RowIds[r])
            {
                dead++;
                Console.WriteLine("        " + label + ": row " + r + " did not pick " + shape.RowIds[r]);
            }
        }
        for (int r = 0; r < shape.RowIds2.Length; r++)
        {
            if (string.IsNullOrEmpty(shape.RowIds2[r]))
            {
                continue;
            }
            MoneyHost host = make(null);
            bool done = host.Press(MoneyPages.Table2Base + UiDataTable.Row, r);
            pressed++;
            if (!done || !(host.Model.View.Bank == r))
            {
                dead++;
                Console.WriteLine("        " + label + ": the other banks' row " + r + " did not pick the bank");
            }
        }
        for (int b = 0; b < shape.PickNames.Length; b++)
        {
            MoneyHost host = make(null);
            bool done = host.Press(MoneyCommands.PickBank, b);
            pressed++;
            if (!done || host.Model.View.Bank != b)
            {
                dead++;
                Console.WriteLine("        " + label + ": the strip did not pick bank " + b);
            }
        }
        if (shape.PeriodNames.Length > 1)
        {
            MoneyHost host = make(null);
            int was = host.Model.View.F[MoneyView.FPeriod];
            bool done = host.Press(UiStatement.First + UiStatement.Period, 0);
            pressed++;
            if (!done || host.Model.View.F[MoneyView.FPeriod] == was)
            {
                dead++;
                Console.WriteLine("        " + label + ": the statement's period did not move");
            }
        }
    }

    private static readonly Dictionary<MoneyHost, BankStub> BankStubs = new Dictionary<MoneyHost, BankStub>();

    private static object StubOf(MoneyHost host) => BankStubs[host];

    private static int BankTextDistance(string a, string b)
    {
        var x = new HashSet<string>(a.Split('\n'));
        var y = new HashSet<string>(b.Split('\n'));
        x.SymmetricExceptWith(y);
        return x.Count;
    }

    private sealed class BankWordsSay : IOwnBankSay
    {
        private readonly UiWords _en = UiWords("en");

        public string T(string key) => _en(key, key);

        public string F(string key, params string[] pairs)
        {
            string text = _en(key, key);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1]);
            }
            return text;
        }

        public string Day(int day) => "day " + day.ToString(CultureInfo.InvariantCulture);

        public string Cents(long cents) => "$" + (cents / 100).ToString("N0", CultureInfo.InvariantCulture);
    }

    private static void BankFaceChecks()
    {
        BanksFaceChecks();
        OwnBankFaceChecks();
        OwnBankRunChecks();
    }

    // ------------------------------------------------------------------------------------------------- Banks

    private static readonly string[] BanksTabStates = { "accounts", "loans_pick", "overdraft", "card", "apply", "rating" };

    private static void BanksFaceChecks()
    {
        // Every tab its own page: no two share their words, and what only one holds (the statement, the card's movements, the bands) is on that one alone.
        var texts = new List<string>();
        foreach (string state in BanksTabStates)
        {
            texts.Add(TextsOf(LayBank(BanksHost(state, out _))));
        }
        int same = 0;
        for (int i = 0; i < texts.Count; i++)
        {
            for (int j = i + 1; j < texts.Count; j++)
            {
                same += BankTextDistance(texts[i], texts[j]) < 12 ? 1 : 0;
            }
        }
        Check(same == 0, "banks face: every tab draws a page of its own (" + same + " pairs of tabs share nearly all their words)");
        Check(texts[0].Contains("Movements in the period") && !texts[1].Contains("Movements in the period") && !texts[3].Contains("Movements in the period"),
            "banks face: the statement is on Accounts alone, not on Loans or Card (the audit: Accounts and Loans drew the same page)");
        Check(texts[5].Contains("What Hudson Trust gives at each rating") && !texts[4].Contains("What Hudson Trust gives at each rating"), "banks face: the rating's bands are on Rating alone");
        Check(texts[2].Contains("Overdraft") && texts[3].Contains("Card, Hudson Trust") && !texts[3].Contains("Overdraft, Hudson Trust"), "banks face: Overdraft and Card each show their own account");

        // No constant figures: change the save and the words change; the old figure is gone.
        BanksFacts a = UiSamples.BanksFactsOf("accounts"), b = UiSamples.BanksFactsOf("accounts");
        b.Cash = 777123f;
        b.Banks[0].Rate = 6.1f;
        b.Banks[0].Most = 123000;
        b.Banks[0].Since = 71;
        b.Halves = 3;
        var ma = new MoneyModel { App = MoneyApp.Banks };
        var mb = new MoneyModel { App = MoneyApp.Banks };
        BanksBuild.Fill(ma, a, BankWords());
        BanksBuild.Fill(mb, b, BankWords());
        string ta = string.Join("\n", ma.Profile.FactValues) + ma.Statement.Balance, tb = string.Join("\n", mb.Profile.FactValues) + mb.Statement.Balance;
        Check(ta.Contains("4.8% a year") && ta.Contains("$400,000") && ta.Contains("day 36") && ta.Contains("$1,204,300") && tb.Contains("6.1% a year") && tb.Contains("$123,000") && tb.Contains("day 71")
              && tb.Contains("$777,123") && !tb.Contains("4.8%") && !tb.Contains("$1,204,300") && ma.Profile.Rating == 8 && mb.Profile.Rating == 3,
            "banks face: the rate, the most, the account's day, the cash and the standing follow the save (no 4.8%, no day of today, no constant)");
        BanksFacts other = UiSamples.BanksFactsOf("accounts");
        other.Banks[1].Rate = 7.3f;
        other.Banks[1].Since = 99;
        var mo = new MoneyModel { App = MoneyApp.Banks };
        mo.View.Bank = 1;
        other.Bank = 1;
        BanksBuild.Fill(mo, other, BankWords());
        Check(string.Join("\n", mo.Profile.FactValues).Contains("7.3% a year") && string.Join("\n", mo.Profile.FactValues).Contains("day 99") && mo.Profile.Name == "Empire Federal",
            "banks face: another bank picked shows its own rate, terms and history with it");
        BanksFacts refused = UiSamples.BanksFactsOf("apply_refused");
        var mr = new MoneyModel { App = MoneyApp.Banks, Tab = BanksBuild.Apply };
        BanksBuild.Fill(mr, refused, BankWords());
        var mg = new MoneyModel { App = MoneyApp.Banks, Tab = BanksBuild.Apply };
        BanksBuild.Fill(mg, UiSamples.BanksFactsOf("apply"), BankWords());
        Check(mg.Table2.Rows[0].Cells[3] == "Yes" && mr.Table2.Rows[0].Cells[3] == "No" && mr.Table.Rows[0].Cells[5] == "Not offered", "banks face: the answer column and the products follow the rating (a rating under the line is No at every bank)");

        // Every control pressed through the host's handler, and what it does read back.
        int pressed = 0, dead = 0, unreasoned = 0;
        foreach (string state in UiSamples.MoneyBankStates)
        {
            string st = state;
            BankPressAll("banks " + st, stub =>
            {
                MoneyHost host = BanksHost(st, out BankStub made);
                BankStubs[host] = made;
                return host;
            }, ref pressed, ref dead, ref unreasoned);
        }
        Check(pressed >= 60, "banks face: the controls of every state are pressed (" + pressed + ")");
        Check(dead == 0, "banks face: no button, step, row or switch does nothing (" + dead + " dead)");
        Check(unreasoned == 0, "banks face: a button that cannot work is drawn disabled with its reason (" + unreasoned + " without)");

        // Back steps out of a row first, then lets the game close the window; the tab key moves; the shared command 1 is only a tab here.
        MoneyHost back = BanksHost("loans_pick", out BankStub backStub);
        bool first = back.Back(), second = back.Back();
        Check(first && !second && back.Model.View.Row == null, "banks face: Back steps out of the debt picked, then closes the window");
        MoneyHost nav = BanksHost("accounts", out _);
        Check(!nav.Press(UiAppWindow.NavCommand, 99) && nav.Press(UiAppWindow.NavCommand, 4) && nav.Model.Tab == 4, "banks face: command 1 moves a tab and nothing else (a stray number does not)");
        Check(!BanksHost("accounts", out BankStub dry).Press(MoneyCommands.CardAct, (int)MoneyAct.Pay) || dry.Acts.Count == 1, "banks face: a press with no source behind it does nothing quietly");

        // A paid debt reads back: the fake port takes the typed payment off the debt and the page shows what is left.
        BanksFacts debts = UiSamples.BanksFactsOf("loans_pick");
        MoneyHost pay = null;
        var payStub = new BankStub(m => BanksBuild.Fill(m, debts, BankWords()), (act, view, id, value) =>
        {
            if (act == MoneyAct.Pay)
            {
                foreach (BankDebt d in debts.Debt)
                {
                    if (d.Id == view.Row)
                    {
                        int amount = view.F[MoneyView.FPay] > 0 ? view.F[MoneyView.FPay] : d.PaySuggested;
                        d.Owed -= amount;
                        d.PayMost = (int)d.Owed;
                    }
                }
            }
            return true;
        });
        pay = new MoneyHost("banks", MoneyApp.Banks, UiIcon.Bank, payStub);
        pay.Model.Tab = BanksBuild.Loans;
        pay.Model.View.Row = "acct:7";
        LayBank(pay);
        pay.Focused(MoneyCommands.TypeField, 0);
        pay.Submitted("80000");
        UiScene shown = LayBank(pay);
        int node = NodeOf(shown, MoneyCommands.CardAct, (int)MoneyAct.Pay | (7 << 8));
        bool paid = node >= 0 && pay.Press(MoneyCommands.CardAct, (int)MoneyAct.Pay | (7 << 8));
        string after = TextsOf(LayBank(pay));
        Check(paid && after.Contains("$700,000") && !after.Contains("$780,000 of $800,000"), "banks face: paying $80,000 of the mortgage reads back as $700,000 owed");
    }

    private static UiScene LayBankModel(MoneyModel m)
    {
        var s = new UiScene();
        MoneyPages.Lay(s, m, UiFrame.FullMenu(16f / 9f, "Banks"), true, UiDefaultKeys.Keyboard);
        return s;
    }

    // ------------------------------------------------------------------------------------------------- Your bank

    private static void OwnBankFaceChecks()
    {
        var texts = new List<string>();
        foreach (string state in new[] { "bank", "savers", "lending", "fund", "branches" })
        {
            texts.Add(TextsOf(LayBank(OwnHost(state, out _))));
        }
        int same = 0;
        for (int i = 0; i < texts.Count; i++)
        {
            for (int j = i + 1; j < texts.Count; j++)
            {
                same += BankTextDistance(texts[i], texts[j]) < 12 ? 1 : 0;
            }
        }
        Check(same == 0, "own bank face: every tab draws a page of its own (" + same + " pairs share nearly all their words)");
        string hero = texts[0];
        Check(hero.Contains("Regional bank") && hero.Contains("1 of 3 conditions met"), "own bank face: the bank's level and how far it is from the next come from its books, not a constant");
        OwnBankFacts f = UiSamples.OwnBankFactsOf("bank");
        var m = new MoneyModel { App = MoneyApp.OwnBank };
        OwnBankBuild.Fill(m, f, BankWords());
        int progress = OwnBankBuild.LevelProgress(f, out int met);
        Check(m.Hero.Progress == progress && progress > 0 && progress < 100 && progress != 40, "own bank face: the hero's bar is the three conditions' real progress, not a constant 40 (" + progress + ")");
        f.HeldMonths = 6;
        f.Capital = f.CapitalLeast;
        int full = OwnBankBuild.LevelProgress(f, out int metAll);
        Check(full == 100 && metAll == 3, "own bank face: with every condition met the bar is full");
        f.RegulatorOk = false;
        Check(OwnBankBuild.LevelProgress(f, out _) < 100, "own bank face: a regulator's complaint holds the bar back");

        // The old facade's invented "biggest accounts": the book's own segments follow the bank's balances and order by them.
        OwnBankFacts s = UiSamples.OwnBankFactsOf("lending");
        var ml = new MoneyModel { App = MoneyApp.OwnBank, Tab = OwnBankBuild.LendingTab };
        OwnBankBuild.Fill(ml, s, BankWords());
        bool ordered = true;
        for (int i = 1; i < ml.Table.Rows.Length; i++)
        {
            ordered &= ParseMoney(ml.Table.Rows[i - 1].Cells[1]) >= ParseMoney(ml.Table.Rows[i].Cells[1]);
        }
        Check(ordered && ml.Table.Rows.Length == s.Segments.Length && ml.Table.Rows[0].Cells[0] == "Companies", "own bank face: the lending table is the bank's own segments, largest balance first");
        s.Segments[0].Lent = D(9000000);
        OwnBankBuild.Fill(ml, s, BankWords());
        Check(ml.Table.Rows[0].Cells[0] == s.Segments[0].Name && ml.Table.Rows[0].Cells[1] == "$9,000,000", "own bank face: a balance that moves moves the row");
        OwnBankFacts fresh = UiSamples.OwnBankFactsOf("bank_new");
        var mn = new MoneyModel { App = MoneyApp.OwnBank };
        OwnBankBuild.Fill(mn, fresh, BankWords());
        string foundText = TextsOf(LayBankModel(mn));
        Check(foundText.Contains("Found a bank") && mn.Cards[0].Actions[0].Disabled == false, "own bank face: with no bank the page offers to found one");
        var mh = new MoneyModel { App = MoneyApp.OwnBank };
        OwnBankBuild.Fill(mh, UiSamples.OwnBankFactsOf("bank_nohq"), BankWords());
        Check(mh.Cards[0].Actions[0].Disabled, "own bank face: without a headquarters Found is drawn disabled, with its reason");

        int pressed = 0, dead = 0, unreasoned = 0;
        foreach (string state in UiSamples.MoneyOwnStates)
        {
            string st = state;
            BankPressAll("own bank " + st, stub =>
            {
                MoneyHost host = OwnHost(st, out BankStub made);
                BankStubs[host] = made;
                return host;
            }, ref pressed, ref dead, ref unreasoned);
        }
        Check(pressed >= 60, "own bank face: the controls of every state are pressed (" + pressed + ")");
        Check(dead == 0, "own bank face: no button, step, row or switch does nothing (" + dead + " dead)");
        Check(unreasoned == 0, "own bank face: a button that cannot work is drawn disabled with its reason (" + unreasoned + " without)");
    }

    private static long D(long dollars) => dollars * 100L;

    private static long ParseMoney(string text)
    {
        long value = 0;
        foreach (char c in text)
        {
            if (c >= '0' && c <= '9')
            {
                value = value * 10 + (c - '0');
            }
        }
        return value;
    }

    // ------------------------------------------------------------------------------------------------- the real bank, day by day

    // The port the checks put where the director is: the same fields the director's methods write, so a press changes the real OwnBank and the page is read again from it.
    private static bool OwnPort(OwnBank b, MoneyAct act, MoneyView view, int id, int value)
    {
        switch (act)
        {
            case MoneyAct.SaverRate: b.SaverOffset[id] = Math.Max(OwnBankCatalogue.SaverOffsetLeast, Math.Min(OwnBankCatalogue.SaverOffsetMost, value)); return true;
            case MoneyAct.LoanRate: b.LoanOffset[id] = Math.Max(OwnBankCatalogue.LoanOffsetLeast, Math.Min(OwnBankCatalogue.LoanOffsetMost, value)); return true;
            case MoneyAct.Lending: b.Lending[int.Parse(view.Row.Substring(2), CultureInfo.InvariantCulture)] = value == 1; return true;
            case MoneyAct.Strategy: b.Strategy = value; return true;
            case MoneyAct.FundLimit: b.FundLimit = value; return true;
            case MoneyAct.PayMode: b.PayMode = value; return true;
            case MoneyAct.PayShare: b.PayShare = value; return true;
            case MoneyAct.PayFixed: b.PayFixed = (long)view.F[MoneyView.FPay] * 100L; return true;
            case MoneyAct.DividendEvery: OwnBankRules.SetDividends(b, value, b.DivShare, 100); return true;
            case MoneyAct.DividendShare: OwnBankRules.SetDividends(b, b.DivEvery, value, 100); return true;
            case MoneyAct.PutIn: { long put = (long)(view.F[MoneyView.FPut] > 0 ? view.F[MoneyView.FPut] : 1000000) * 100L; b.Capital += put; b.Reserves += put; return true; }
            case MoneyAct.TakeOut: { long taken = (long)(view.F[MoneyView.FPut] > 0 ? view.F[MoneyView.FPut] : 1000000) * 100L; b.Capital -= taken; b.Reserves -= taken; return true; }
            default: return true;
        }
    }

    private static void OwnBankRunChecks()
    {
        BankCity c = BankCity.Quiet(BankYear);
        OwnBank b = NewBank(c, 1, 11, OwnBankRules.CapitalLeast(c.Level) * 3);
        var say = new BankWordsSay();
        var world = new OwnBankWorld { Ready = true, Today = 1, Level = c.Level, Buying = 0, HasHq = true, HqName = "7 5th Avenue", Integrity = 60, BranchOn = true };
        var day = new BankDay();
        var pages = new Dictionary<int, string>();
        var figures = new List<string>();
        bool balancedAll = true;
        int dayAt = 1;
        for (int step = 0; step < 6; step++)
        {
            balancedAll &= RunBank(b, c, dayAt, 15, null, day);
            dayAt += 15;
            world.Today = dayAt;
            var f = new OwnBankFacts();
            OwnBankReader.Read(f, b, c, world, say);
            var m = new MoneyModel { App = MoneyApp.OwnBank };
            OwnBankBuild.Fill(m, f, BankWords());
            pages[dayAt] = TextsOf(LayBankModel(m));
            figures.Add(m.Figures[0].Value + "|" + m.Figures[1].Value + "|" + m.Figures[2].Value);
            if (m.Figures.Length == 0 || m.Figures[0].Value != UiSamples.UtilitiesMoney(b.Savings / 100f))
            {
                Console.WriteLine("        own bank run: figure " + (m.Figures.Length > 0 ? m.Figures[0].Value : "none") + " for savings " + b.Savings + ", ready " + f.Ready + ", state " + f.State + ", lent " + f.Lent + "/" + b.Lent);
            }
            Check(f.Ready && f.State == 1 && f.Deposits == b.Savings && f.Lent == b.Lent && f.Capital == OwnBankRules.CapitalNow(b) && m.Figures[0].Value == UiSamples.UtilitiesMoney(b.Savings / 100f),
                "own bank run: day " + dayAt + " the page's deposits, loans and capital are the bank's own books");
        }
        var distinct = new HashSet<string>(figures);
        Check(balancedAll && distinct.Count >= 4, "own bank run: over 90 days of a seeded city the figures the page shows move day by day (" + distinct.Count + " different readings in 6)");

        // Every tab at the end of the run: each its own page, read from the same bank.
        var tabs = new List<string>();
        var facts = new OwnBankFacts();
        OwnBankReader.Read(facts, b, c, world, say);
        world.FundDays = new[] { 80, 81, 82, 83, 84 };
        world.FundSeries = new[] { 1f, 2f, 3f, 4f, 5f };
        OwnBankReader.Read(facts, b, c, world, say);
        for (int tab = 0; tab < OwnBankBuild.Tabs; tab++)
        {
            var m = new MoneyModel { App = MoneyApp.OwnBank, Tab = tab };
            OwnBankBuild.Fill(m, facts, BankWords());
            tabs.Add(TextsOf(LayBankModel(m)));
        }
        int clash = 0;
        for (int i = 0; i < tabs.Count; i++)
        {
            for (int j = i + 1; j < tabs.Count; j++)
            {
                clash += BankTextDistance(tabs[i], tabs[j]) < 10 ? 1 : 0;
            }
        }
        Check(clash == 0, "own bank run: the five tabs of the real bank are five pages (" + clash + " alike)");

        // Press a control through the host and read the real bank back: the rate offset, the strategy, the fund limit, the owner's pay, the dividends, capital in.
        MoneyHost host = null;
        var stub = new BankStub(m =>
        {
            var fresh = new OwnBankFacts();
            OwnBankReader.Read(fresh, b, c, world, say);
            OwnBankBuild.Fill(m, fresh, BankWords());
        }, (act, view, id, value) => OwnPort(b, act, view, id, value));
        host = new MoneyHost("own_bank", MoneyApp.OwnBank, UiIcon.PiggyBank, stub);
        host.Model.Tab = OwnBankBuild.SaversTab;
        host.Model.View.Row = "s|0";
        LayBank(host);
        int was = b.SaverOffset[0];
        bool up = host.Press(MoneyCommands.CardMore, 0);
        LayBank(host);
        Check(up && b.SaverOffset[0] == was + OwnBankBuild.SaverStep && stub.LastAct == MoneyAct.SaverRate, "own bank run: a plus on the savers' rate raises the real bank's offset by a step (" + was + " to " + b.SaverOffset[0] + ")");
        host.Model.Tab = OwnBankBuild.LendingTab;
        host.Model.View.Row = "l|3";
        LayBank(host);
        bool lent = b.Lending[3];
        bool toggled = host.Press(MoneyCommands.CardSwitch, 0);
        Check(toggled && b.Lending[3] == !lent, "own bank run: the lending switch flips the segment's flag in the real bank");
        host.Model.Tab = OwnBankBuild.FundTab;
        host.Model.View.Row = null;
        LayBank(host);
        int strat = b.Strategy;
        bool next = host.Press(MoneyCommands.CardMore, 0);
        Check(next && b.Strategy == Math.Min(OwnBankCatalogue.Strategies - 1, strat + 1), "own bank run: a step on the fund's strategy sets the real strategy");
        bool limit = host.Press(MoneyCommands.CardMore, 1);
        Check(limit && b.FundLimit == OwnBankBuild.FundLimitStep, "own bank run: a step on the fund's limit sets the real limit");
        host.Model.Tab = OwnBankBuild.BankTab;
        host.Model.View.Row = OwnBankBuild.PayRow;
        LayBank(host);
        bool mode = host.Press(MoneyCommands.CardMore, 0);
        LayBank(host);
        host.Focused(MoneyCommands.TypeField, 1);
        host.Submitted("400");
        LayBank(host);
        bool fixedPay = host.Press(MoneyCommands.CardAct, (int)MoneyAct.PayFixed);
        Check(mode && fixedPay && b.PayMode == BankTiers.PayFixedMode && b.PayFixed == 40000, "own bank run: the owner's pay set to a fixed $400 a day reaches the real bank");
        host.Model.View.Row = OwnBankBuild.DivRow;
        LayBank(host);
        bool every = host.Press(MoneyCommands.CardMore, 0);
        Check(every && b.DivEvery == BankDividends.Weekly, "own bank run: dividends set to weekly reach the real bank");
        host.Model.View.Row = null;
        long capital = OwnBankRules.CapitalNow(b);
        host.Focused(MoneyCommands.TypeField, 0);
        host.Submitted("200000");
        LayBank(host);
        bool put = host.Press(MoneyCommands.CardAct, (int)MoneyAct.PutIn);
        string shown = TextsOf(LayBank(host));
        Check(put && OwnBankRules.CapitalNow(b) == capital + D(200000) && shown.Contains(UiSamples.UtilitiesMoney((capital + D(200000)) / 100f)),
            "own bank run: $200,000 of capital put in is in the real bank and on the page");

        // A save written on day 90 and read back gives the same page; an old line (before the pay and dividend fields) reads and plays on.
        string line = OwnBankStore.Write(b);
        OwnBank back = OwnBankStore.Read(line, out _);
        var fa = new OwnBankFacts();
        var fb = new OwnBankFacts();
        OwnBankReader.Read(fa, b, c, world, say);
        OwnBankReader.Read(fb, back, c, world, say);
        var ma = new MoneyModel { App = MoneyApp.OwnBank };
        var mb = new MoneyModel { App = MoneyApp.OwnBank };
        OwnBankBuild.Fill(ma, fa, BankWords());
        OwnBankBuild.Fill(mb, fb, BankWords());
        Check(back != null && TextsOf(LayBankModel(ma)) == TextsOf(LayBankModel(mb)), "own bank run: a bank saved on day 90 and read back draws the same page");
        bool played = back != null && RunBank(back, c, dayAt, 30, null, day);
        var fc = new OwnBankFacts();
        OwnBankReader.Read(fc, back, c, world, say);
        Console.WriteLine("        own bank run: read back " + (back != null) + ", played " + played + ", ready " + fc.Ready + ", running " + (back != null && back.Running));
        Check(played && fc.Ready && fc.Deposits == back.Savings, "own bank run: the bank read back plays on for 30 more days and the page follows it");
    }
}
