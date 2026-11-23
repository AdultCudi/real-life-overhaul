using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Ui;
using RealLifeOverhaul.Utilities;

// Two things players asked for and the owner said yes to. Own work: the hours slider of a contract's stint says what the hours picked will do before Start (the time it ends at, the part
// of the contract left, the fee when it finishes the contract), at a desk and at the computer at home alike. The Utilities app: "Pay all now" and a way to tick several bills and pay them,
// on the old interface and the kit, each with the total in the game's question box first, refusing with the shortfall when the cash is short, every bill with its own bank line, and nothing
// paid twice when the auto debit comes (BillPayRules, UtilitiesDirector.PayMany).
internal static partial class Program
{
    private static void OwnWorkYield()
    {
        // The slider's yield: the minutes left after a stint of the hours it stands at, and whether that pays the fee.
        var s = new StintSlider(6, 16 * 60, true, null, 240);
        Check(s.Pays == 240 && s.Minutes == 360 && s.LeftAfter == 10 * 60 && !s.Finishes, "own work yield: 6 hours of a 16 hour contract leave 10 hours and do not yet pay the fee");
        var last = new StintSlider(8, 3 * 60 + 30, true, null, 90);
        Check(last.Minutes == 210 && last.LeftAfter == 0 && last.Finishes, "own work yield: the stint that works the last minute of the contract is the one that pays it");
        var shortStint = new StintSlider(2, 90, true, null, 90);
        shortStint.Offered();
        shortStint.Slide(2);
        Check(shortStint.Now == 2 && shortStint.Minutes == 90 && shortStint.Finishes, "own work yield: two hours on a contract with an hour and a half left stop at its end and finish it");
        var off = new StintSlider(8, 16 * 60, false, null, 240);
        Check(!off.Slides && off.Minutes == OwnRules.SittingMinutes && off.LeftAfter == 16 * 60 - OwnRules.SittingMinutes, "own work yield: with the slider off the stint is the fixed three hours and the yield follows it");
        var rising = new StintSlider(1, 12 * 60, true, null, 0);
        int prev = int.MaxValue;
        bool falls = true;
        for (int h = 1; h <= 12; h++)
        {
            rising.Offered();
            rising.Slide(h);
            falls &= rising.LeftAfter <= prev && rising.Now == h;
            prev = rising.LeftAfter;
        }
        Check(falls && rising.Finishes, "own work yield: every hour picked from 1 to 12 leaves less of the contract and the twelfth finishes a 12 hour one");

        string contract = ModSource(@"Careers\ContractStint.cs"), sitting = ModSource(@"Careers\ComputerSitting.cs"), info = ModSource(@"Careers\StintInfo.cs");
        string stint = ModSource(@"Careers\CareersDirector.OwnStint.cs"), home = ModSource(@"Careers\CareersDirector.OwnHome.cs");
        Check(contract.Contains("_slider.Slides ? StintInfo.Of(_slider)") && MethodText(sitting, "public (string key, object arguments) GetSliderInfo()").Contains("StintInfo.Of(_slide)")
              && info.Contains("\"reallife_own_slider_done\"") && info.Contains("\"reallife_own_slider_part\"") && info.Contains("TimeHelper.Now().AddMinutes(slider.Minutes)"),
            "own work yield sources: the line under the slider is one text for the desk's panel and the computer's, from the slider's own minutes");
        Check(stint.Contains("OwnHoursChosen, ContractPays(c))") && home.Contains("OwnHoursChosen, ContractPays(c))") && stint.Contains("OwnRules.Paid(c, Save.Day)"),
            "own work yield sources: both places make the slider with what the contract pays today, a late one's cut included, as ContractEnded pays it");
        Dictionary<string, string> en = LocaleFile("en");
        bool texts = true;
        foreach (string key in new[] { "reallife_own_slider_part", "reallife_own_slider_done" })
        {
            texts &= en.TryGetValue(key, out string t) && t.Contains("{hours}") && t.Contains("{time}") && t.Contains("{fee}") && t.Length <= 240 && !t.Contains("!") && !t.Contains("—") && !t.Contains("–");
        }
        Check(texts && en["reallife_own_slider_part"].Contains("{left}"), "own work yield texts: the slider's two lines are in en.json with the hours, the time and the fee to fill in");
    }

    private static Account Bill(string place, Service service, int owed, int due, bool auto = true)
    {
        return new Account { Place = place, Service = service, State = SupplyState.Active, Owed = owed, DueDay = due, AutoDebit = auto, OnTime = 0 };
    }

    private static void UtilitiesPayAll()
    {
        var state = new UtilitiesState();
        state.Accounts.Add(Bill("A~1", Service.Power, 90, 20));
        state.Accounts.Add(Bill("A~1", Service.Water, 0, 0));
        state.Accounts.Add(Bill("B~2", Service.Power, 40, 18));
        state.Accounts.Add(Bill("B~2", Service.Internet, 60, 0));
        state.Accounts.Add(Bill("C~3", Service.Water, 25, 18));
        var owing = new List<Account>();
        BillPayRules.Owing(state, owing);
        Check(owing.Count == 4 && owing[0].Place == "A~1" && owing[0].Service == Service.Power, "pay all: every account of every place that owes is gathered, the one that owes nothing is left out");

        BillPayRules.DueOrder(owing);
        Check(owing[0].Place == "B~2" && owing[0].Service == Service.Power && owing[1].Place == "C~3" && owing[2].Place == "A~1" && owing[3].Service == Service.Internet,
            "pay all: bills are in due-date order, ties as they stood, a bill with no due day last");

        BillQuote rich = BillPayRules.Quote(owing, 1000);
        Check(rich.Count == 4 && rich.Total == 215 && rich.Cash == 1000 && rich.Short == 0 && rich.Covered && rich.FitCount == 4 && rich.FitTotal == 215, "pay all: with the cash the quote covers every bill and shows the total");
        BillQuote exact = BillPayRules.Quote(owing, 215);
        Check(exact.Covered && exact.Short == 0, "pay all: cash exactly the total still covers it");
        BillQuote poor = BillPayRules.Quote(owing, 150);
        Check(!poor.Covered && poor.Short == 65 && poor.Total == 215, "pay all: cash short by 65 refuses the whole, with the shortfall");
        // 150 in due order: B power 40, C water 25, A power 90 would make 155 so it does not fit, the internet's 60 does (65 + 60 = 125).
        Check(poor.FitCount == 3 && poor.FitTotal == 125, "pay all: what the cash covers is taken in due-date order and a bill that does not fit is passed over, so a smaller one after it is still paid");
        BillQuote broke = BillPayRules.Quote(owing, 0);
        Check(broke.FitCount == 0 && broke.Short == 215 && !broke.Covered, "pay all: no cash covers nothing");
        BillQuote debt = BillPayRules.Quote(owing, -50);
        Check(debt.Cash == 0 && debt.FitCount == 0 && debt.Short == 215, "pay all: a negative cash counts as none");
        Check(BillPayRules.Quote(new List<Account>(), 500).Count == 0 && !BillPayRules.Quote(new List<Account>(), 500).Covered, "pay all: with nothing owed there is nothing to ask about");

        var pay = new List<Account>();
        BillPayRules.Choose(owing, 1000, false, pay);
        Check(pay.Count == 4 && pay[0].Place == "B~2", "pay all: choosing all takes every bill, earliest due first");
        BillPayRules.Choose(owing, 150, true, pay);
        Check(pay.Count == 3 && pay[0].Place == "B~2" && pay[1].Place == "C~3" && pay[2].Service == Service.Internet, "pay all: choosing what the cash covers takes those three in due order");

        // Ticked bills: the same rules for a part of them.
        var ticked = new List<Account> { owing[2], owing[0] };
        BillQuote some = BillPayRules.Quote(ticked, 100);
        Check(some.Count == 2 && some.Total == 130 && some.Short == 30 && some.FitCount == 1 && some.FitTotal == 40, "pay ticked: two ticked bills are quoted and ordered on their own");

        // A bill with nothing owed or an account gone is never asked for.
        var odd = new List<Account> { null, Bill("D~4", Service.Power, 0, 5), Bill("E~5", Service.Water, 12, 5) };
        BillPayRules.DueOrder(odd);
        Check(odd.Count == 1 && odd[0].Place == "E~5", "pay all: an empty or missing account is dropped from the order");

        // Nothing paid twice: paying clears the account, so the auto debit's test (owed and due) finds nothing, and a second gathering finds none of them.
        int back = 0;
        for (int i = 0; i < pay.Count; i++)
        {
            back += UtilitiesRules.Paid(pay[i], 19);
        }
        Check(back == 0 && pay[0].Owed == 0 && pay[0].DueDay == 0 && pay[0].OnTime == 1, "pay all: a bill paid is cleared at once as paying it by hand clears it, and counts as paid on time");
        bool debitTakes = false;
        for (int i = 0; i < pay.Count; i++)
        {
            debitTakes |= pay[i].Owed > 0 && pay[i].AutoDebit && pay[i].DueDay > 0 && 19 >= pay[i].DueDay;
        }
        BillPayRules.Owing(state, owing);
        Check(!debitTakes && owing.Count == 1 && owing[0].Place == "A~1", "pay all: on the due day the auto debit finds nothing owed on the bills that were paid, and the one left out stays owed");

        // Speed: two hundred accounts sort at once.
        var big = new UtilitiesState();
        for (int i = 0; i < 200; i++)
        {
            big.Accounts.Add(Bill("P~" + i, Service.Power, 10 + i % 7, 40 - i % 30));
        }
        var list = new List<Account>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        BillPayRules.Owing(big, list);
        BillQuote q = BillPayRules.Quote(list, 500);
        watch.Stop();
        bool sorted = true;
        for (int i = 1; i < list.Count; i++)
        {
            sorted &= list[i - 1].DueDay <= list[i].DueDay;
        }
        Check(sorted && q.Count == 200 && watch.ElapsedMilliseconds < 50, "pay all: two hundred bills are gathered, ordered and quoted in well under 50 ms");

        UtilitiesPaySources();
    }

    private static void UtilitiesPaySources()
    {
        string many = ModSource(@"Utilities\UtilitiesDirector.PayMany.cs"), bills = ModSource(@"Utilities\UtilitiesDirector.Bills.cs"), app = ModSource(@"Utilities\UtilitiesApp.cs");
        string live = ModSource(@"Ui\Money\UtilitiesLive.cs"), screen = ModSource(@"Ui\Money\UtilitiesScreen.cs"), build = ModSource(@"Ui\Money\UtilitiesBuild.cs"), face = ModSource(@"Ui\Money\UtilitiesFace.cs");
        string game = ModSource(@"Utilities\UtilitiesGame.cs"), director = ModSource(@"Utilities\UtilitiesDirector.cs");
        Check(many.Length > 0 && bills.Length > 0 && app.Length > 0 && live.Length > 0 && screen.Length > 0 && build.Length > 0 && face.Length > 0 && game.Length > 0 && director.Length > 0, "pay all sources: the sources are read");

        string ask = MethodText(many, "private void Ask(List<Account> bills, Action done)");
        string pay = MethodText(many, "public void PayBills(List<Account> bills, bool whatCashCovers, Action done)");
        Check(ask.Contains("BillPayRules.Quote(bills, UtilitiesGame.Cash)") && ask.Contains("GameConfirm.Show(\"reallife_util_pay_title\".Localize()") && ask.Contains("if (q.Covered)")
              && ask.Contains("q.FitCount == 0") && ask.Contains("\"reallife_util_pay_short\"") && ask.Contains("\"reallife_util_pay_part_title\".Localize()") && ask.Contains("PayBills(list, true, done)")
              && ask.Contains("PayBills(list, false, done)") && ask.Contains("lack = shortBy") && ask.Contains("total, cash"),
            "pay all sources: the total and the cash are in the game's question box before anything is paid; a short cash refuses with the shortfall, and only a yes to the part box pays what it covers");
        Check(pay.Contains("BillPayRules.Choose(bills, UtilitiesGame.Cash, whatCashCovers, _toPay)") && pay.Contains("Pay(_toPay[i], today, true, false)") && pay.Contains("_paying = true;")
              && pay.Contains("_paying = false;") && ComesBefore(pay, "Pay(_toPay[i], today, true, false)", "_view?.Changed();") && pay.Contains("break;"),
            "pay all sources: each bill is paid by the one Pay (its own bank line, the account cleared), the cash read again, the page written once at the end, and a refusal stops the run");
        Check(bills.Contains("public bool Pay(Account a, int today, bool quiet = false, bool notify = true)") && bills.Contains("notify: notify") && game.Contains("bool deductible = false, bool notify = true)")
              && game.Contains("showNotification: notify") && director.Contains("if (!_paying)"),
            "pay all sources: paying by hand and the auto debit keep their notices, a run of bills pays without one each and the page is not rewritten per bill");
        Check(MethodText(bills, "private void Dues(int today)").Contains("a.Owed > 0 && a.AutoDebit && a.DueDay > 0 && today >= a.DueDay"), "pay all sources: the auto debit takes only a bill still owed, so a bill paid here is not taken again");
        Check(app.Contains("_director.AskPayAll(null)") && app.Contains("_director.AskPay(_tickedNow, null)") && app.Contains("\"reallife_util_box_tick\"") && app.Contains("row.Set(7,") && app.Contains("\"reallife_util_col_tick\""),
            "pay all sources: the old app has Pay all now, Pay ticked, a tick switch on an account and a Ticked column");
        Check(live.Contains("director.AskPayAll(Moved)") && live.Contains("director.AskPay(_bills, Moved)") && live.Contains("UtilitiesAct.Pick") && screen.Contains("UtilitiesAct.PayAll") && screen.Contains("UtilitiesAct.PayPicked")
              && build.Contains("m.Accounts.PickWords") && build.Contains("m.PayAll = new UiActionModel") && face.Contains("PayAllAction"),
            "pay all sources: the kit's Pay all now and its picking of bills go to the same director methods as the old app's");
        Check(Array.IndexOf(UtilitiesFace.Commands, UtilitiesFace.PayAllAction) >= 0, "pay all sources: the kit's Pay all now is a command the screen answers");

        Dictionary<string, string> en = LocaleFile("en");
        string[] keys =
        {
            "reallife_util_col_tick", "reallife_util_ticked_yes", "reallife_util_box_tick", "reallife_util_box_ticked", "reallife_util_box_ticked_value", "reallife_util_box_pay_all",
            "reallife_util_box_pay_ticked", "reallife_util_box_clear_ticks", "reallife_util_pay_none", "reallife_util_pay_title", "reallife_util_pay_body", "reallife_util_pay_short",
            "reallife_util_pay_part_title", "reallife_util_pay_part_body", "reallife_util_toast_paid_many", "reallife_ui_util_pick", "reallife_ui_util_picked", "reallife_ui_util_pick_done", "reallife_ui_util_pick_all"
        };
        bool all = true;
        foreach (string key in keys)
        {
            all &= en.TryGetValue(key, out string text) && text.Length > 2 && !text.Contains("!") && !text.Contains("—") && !text.Contains("–") && !text.Contains(" - ");
        }
        Check(all, "pay all texts: every new key is in en.json with no dash and no exclamation mark");
        Check(Fits(en, "reallife_util_pay_none") && Fits(en, "reallife_util_pay_short") && Fits(en, "reallife_util_toast_paid_many") && Fits(en, "reallife_util_pay_body") && Fits(en, "reallife_util_pay_part_body"),
            "pay all texts: the notices and the question boxes' words are within 240 characters");
        bool fills = en["reallife_util_pay_body"].Contains("{count}") && en["reallife_util_pay_body"].Contains("{total}") && en["reallife_util_pay_body"].Contains("{cash}")
                     && en["reallife_util_pay_short"].Contains("{lack}") && en["reallife_util_pay_part_body"].Contains("{lack}") && en["reallife_util_pay_part_body"].Contains("{fit}")
                     && en["reallife_util_pay_part_body"].Contains("{fit_total}") && en["reallife_util_toast_paid_many"].Contains("{paid}") && en["reallife_util_toast_paid_many"].Contains("{amount}");
        Check(fills, "pay all texts: the question boxes say the total, the cash and the shortfall, and the notice says how many were paid and for how much");
        foreach (string key in new[] { "reallife_util_pay_none", "reallife_util_pay_title", "reallife_util_pay_body", "reallife_util_pay_short", "reallife_util_pay_part_title", "reallife_util_pay_part_body", "reallife_util_toast_paid_many" })
        {
            Check((many + app).Contains("\"" + key + "\""), "pay all texts: " + key + " is read by the code");
        }
    }
}
