using System.Collections.Generic;
using System.Reflection;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;

// The staff rota planner (Scripts/Economy/Company): the faults found when the folder was cleaned up, each checked against
// a case the old code got wrong. A choice in a save whose rota line could not be read is kept; a fault in a page's
// redraw stops no job; an agreed raise works the sheet out again; a confirmed plan is written only as confirmed, and a
// fill that writes nothing says so; a week half written is put back.
internal static partial class Program
{
    private static void RotaFixes()
    {
        RotaFixState();
        RotaFixLadder();
    }

    private static void RotaFixState()
    {
        // A save whose rota line is of a version this one cannot read: kept as it is, until the player chooses afresh.
        var data = new Dictionary<string, string> { { RotaStore.Key, "v=9^1:Main Street~12" } };
        var modes = new Dictionary<string, int>();
        bool read = RotaStore.Read(data[RotaStore.Key], modes);
        SaveLines.Unreadable(data, RotaStore.Key, "its version is not one this version of the mod reads");
        data[RotaStore.Key] = "v=1^2:Harbour Road~4";
        SaveLines.PutBack(data);
        Check(!read && SaveLines.IsKept(data, RotaStore.Key) && data[RotaStore.Key] == "v=9^1:Main Street~12", "rota state: a rota line this version cannot read is kept and put back at a save");
        SaveLines.Release(data, RotaStore.Key);
        data[RotaStore.Key] = "v=1^2:Harbour Road~4";
        Check(SaveLines.PutBack(data) == 0 && data[RotaStore.Key] == "v=1^2:Harbour Road~4" && !SaveLines.IsKept(data, RotaStore.Key),
            "rota state: once the player chooses, the choice is what the save keeps, not the line put back over it");
        SaveLines.Release(data, "reallife:nothing_kept");
        SaveLines.Release(null, RotaStore.Key);
        string rota = CompanySource("CompanyDirector.Rota.cs");
        Check(InOrder(CompanyMethod(rota, "public void RotaChoose(string place, int mode)"), "if (_rotaModesUnread)", "SaveLines.Release(Save.modData, RotaStore.Key)",
                "_rotaModesUnread = false", "RotaSave(Save)"),
            "rota state: a choice in a save whose line could not be read lets the kept line go before it is written");

        // An agreed raise is in the wages the sheet is read with, so it is in the stamp the sheet is kept by.
        var staff = new List<StaffFile>
        {
            new StaffFile { Id = "a", Place = "High%20Street~1" }, new StaffFile { Id = "b", Place = "High%20Street~1" }, new StaffFile { Id = "c", Place = "Low%20Road~2" }
        };
        int before = StaffRules.RaisesAt(staff, "High%20Street~1");
        staff[2].Raise = 150;
        int elsewhere = StaffRules.RaisesAt(staff, "High%20Street~1");
        staff[1].Raise = 150;
        int raised = StaffRules.RaisesAt(staff, "High%20Street~1");
        Check(elsewhere == before && raised != before && raised == StaffRules.RaisesAt(staff, "High%20Street~1") && StaffRules.RaisesAt(null, "x") == StaffRules.RaisesAt(staff, "nowhere"),
            "rota state: a raise agreed at a business changes its figure, one at another business does not, and the same raises give the same figure");
        Check(CompanyMethod(rota, "private int RotaDays(RotaPlace place)").Contains("StaffRules.RaisesAt(_state.Staff, place.Place)") && CompanyMethod(rota, "private int RotaDays(RotaPlace place)").Contains("raises << 2"),
            "rota state: the raises are mixed into the stamp's extra, clear of its two low bits (the permit is read from them)");

        // The job's coroutine always lets the next start; a page that fails to redraw ends no job.
        string jobs = CompanySource("CompanyDirector.RotaJobs.cs");
        Check(InOrder(CompanyMethod(jobs, "private IEnumerator RotaWork()"), "try", "while (RotaSlice())", "finally", "_rotaWorking = false"),
            "rota state: however the planner's coroutine ends, a fault included, the next job starts it again");
        Check(InOrder(CompanyMethod(jobs, "private bool RotaSlice()"), "if (done)", "try", "_view.Changed();", "catch (Exception e)"),
            "rota state: a fault while the page is written again is caught where the job ends, not by the coroutine");

        // A confirmed plan is written only as it was confirmed; a fill that writes nothing says so.
        string fill = CompanySource("CompanyDirector.RotaFill.cs");
        Check(InOrder(CompanyMethod(fill, "public void FillRota(string place, int rung)"), "long stamp = sheet.Stamp", "RotaConfirmed(place, can != null ? JobFillHours : kind, can != null ? 1 : -1, stamp)",
                "RotaConfirmed(place, kind, -1, stamp)", "RotaConfirmed(place, kind, rung, stamp)")
              && CompanyMethod(fill, "private void RotaConfirmed(").Contains("RotaAsk(place, kind, false, rung, stamp)"),
            "rota state: what is put to the player is remembered with the job it starts");
        string step = CompanyMethod(jobs, "private bool RotaStep(RotaJob job)");
        Check(InOrder(step, "default:", "if (job.Stamp != 0 && sheet.Stamp != job.Stamp)", "return RotaUnwritten(", "if (job.Kind == JobFillHours && rung == null)", "return RotaUnwritten(",
                "return RotaWrite(", "|| RotaUnwritten("),
            "rota state: a plan worked out again after the confirm is not written unless it is the one confirmed, and a write that cannot be made is said");
        Check(InOrder(CompanyMethod(jobs, "private bool RotaToFill(RotaJob job, RotaSheet sheet)"), "if (job.Kind == JobShow)", "return RotaUnwritten("),
            "rota state: a fill refused after it was asked for is said, not dropped");
        Check(InOrder(CompanyMethod(jobs, "private bool RotaUnwritten("), "Logger?.Info(", "if (!job.Test)", "\"reallife_rota_changed\""), "rota state: a fill that writes nothing is in the log and in a notice");

        // A fault in the middle of a write puts the week kept before it back.
        Check(InOrder(CompanyMethod(fill, "private bool RotaWrite("), "_rotaWriting = place;", "RotaGame.Write(", "_rotaWriting = null;")
              && InOrder(CompanyMethod(jobs, "private bool RotaSlice()"), "catch (Exception e)", "RotaFault(", "if (_rotaWriting == job.Place)", "RotaRescue(job.Place)")
              && InOrder(CompanyMethod(jobs, "private void RotaRescue(string place)"), "RotaPutBack(place)", "\"reallife_rota_note_back\"", "catch (Exception e)"),
            "rota state: a week half written when the planner stops on a fault is put back as it was, and the player told");
    }

    private static void RotaFixLadder()
    {
        // The cleaning hours: a one-hour try that finds no room, after the round's best move was done for good, undoes nothing.
        var maker = new RotaLadder();
        const BindingFlags inside = BindingFlags.NonPublic | BindingFlags.Instance;
        int[] clean = (int[])typeof(RotaLadder).GetField("_clean", inside).GetValue(maker);
        int[] room = (int[])typeof(RotaLadder).GetField("_cleanFree", inside).GetValue(maker);
        MethodInfo move = typeof(RotaLadder).GetMethod("CleanMove", inside);
        room[5] = 1;
        int done = (int)move.Invoke(maker, new object[] { 5, 1 });
        int tried = (int)move.Invoke(maker, new object[] { 0, 1 });
        move.Invoke(maker, new object[] { 0, -1 });
        int again = (int)move.Invoke(maker, new object[] { 5, 1 });
        move.Invoke(maker, new object[] { 5, -1 });
        Check(done == 1 && tried == 0 && again == 0 && clean[0] == 0 && clean[5] == 1,
            "rota ladder: a cleaning hour tried where there is no room, after the best move was made, takes no hour off another (midnight " + clean[0] + ", 05:00 " + clean[5] + ")");

        // Theft follows the customers the game sends, which stop at what the stations serve, not those who would come past them.
        RotaCase busy = RotaOneTill(300);
        RotaRoster(busy, 0, 0, 0x7F, 9, 17);
        busy.Shop.TheftRate = 10;
        long sent = 0, past = 0;
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            sent += busy.Shop.Open[cell] ? busy.Shop.Customers[cell] * 10L : 0;
            past += busy.Shop.Open[cell] ? System.Math.Max(busy.Shop.Customers[cell], busy.Shop.Wanting[cell]) * 10L : 0;
        }
        RotaRung full = RotaValue(busy);
        Check(past > sent && full.Sum.Theft == sent, "rota ladder: a till at its limit is counted the theft of the customers it takes in (" + full.Sum.Theft + ", wanted " + sent + ", not " + past + ")");

        // Staff first makes a demand below high priority a limit: where somebody has one, there is a choice to make.
        RotaCase days = RotaOneTill();
        days.Problem.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.FreeDays, 1, 0, 0, 1UL << 6) };
        RotaLadderResult staff = RotaClimb(days, RotaMode.StaffFirst);
        Check(staff.ModeMatters && staff.Mode == RotaMode.StaffFirst && !staff.ModeAuto,
            "rota ladder: a free Sunday of medium priority makes staff first and lean differ, and the player's staff first is kept (" + staff.Mode + ")");
        RotaCase high = RotaOneTill();
        high.Problem.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.FreeDays, RotaCatalogue.PriorityHigh, 0, 0, 1UL << 6) };
        Check(!RotaClimb(high).ModeMatters, "rota ladder: a demand of high priority is a limit in both, so there is still nothing to choose");

        // The next hire is counted with everybody there, not as the last week tried left them out.
        RotaCase two = RotaShopCase(1, 0, 2);
        two.Problem.Can[0] = 60;
        two.Problem.Can[1] = 40;
        two.Problem.Worker[0].Wage = two.Problem.Worker[1].Wage = 1500;
        two.Problem.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 1, 30, 40) };
        two.Problem.Worker[1].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 1, 30, 40) };
        RotaOpenHours(two, 0x7F, 8, 18);
        RotaRoster(two, 0, 0, 0x7F, 8, 18);
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            if (cell % 24 < 9 || cell % 24 >= 17)
            {
                two.Shop.Customers[cell] = two.Shop.Wanting[cell] = 0;
            }
        }
        RotaLadderResult covered = RotaClimb(two);
        Check(covered.Count == 2 && covered.Stop == RotaEnd.Covered && covered.Recommended == 1,
            "rota ladder: two cashiers for one till open 56 hours a week need nobody more, even when a week tried left one of them out (" + covered.Count + " rungs, stop " + covered.Stop + ")");

        // A support station (a guard) is paid in every hour it opens: an hour that pays the cashier but not both is not worth it.
        var advisor = new RotaAdvisor();
        RotaShop guard = RotaTills(1);
        guard.Size(2, 2);
        guard.GroupKind[0] = RotaGroupKind.Service;
        guard.GroupKind[1] = RotaGroupKind.Support;
        guard.Group[1] = 1;
        guard.Cap[0] = 20;
        guard.StationWage[0] = guard.StationWage[1] = 1500;
        guard.Margin = 400;
        guard.HasCustomers = true;
        var need = new bool[2];
        advisor.Crew(guard, 5, need, out int served, out int wage, out int count);
        guard.Customers[10] = 5;
        guard.Customers[11] = 10;
        var net = new int[RotaCatalogue.Cells];
        var crew = new int[RotaCatalogue.Cells];
        advisor.Net(guard, net, crew);
        Check(need[0] && need[1] && served == 5 && wage == 3000 && count == 1 && net[10] == 5 * 400 - 3000 && net[11] == 10 * 400 - 3000,
            "rota ladder: 5 customers bring 20.00 against a cashier and a guard at 15.00 each: that hour is worth -10.00, not +5.00 (" + net[10] + ")");
        RotaShop guards = RotaTills(1);
        guards.Size(2, 1);
        guards.GroupKind[0] = RotaGroupKind.Support;
        guards.Group[0] = guards.Group[1] = 0;
        guards.StationWage[0] = 1500;
        guards.StationWage[1] = 1200;
        guards.HasCustomers = true;
        advisor.Crew(guards, 8, need, out served, out wage, out count);
        Check(served == 8 && wage == 2700 && need[0] && need[1], "rota ladder: where no station limits the customers every support station wanted is paid, not the cheapest alone (" + wage + ")");

        // A plan that keeps more people within their hours may be worth less; the page and the confirm say "less".
        RotaCase lesser = RotaOneTill();
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            lesser.Shop.Open[cell] = false;
            lesser.Shop.Customers[cell] = cell % 24 >= 9 && cell % 24 < 12 ? lesser.Shop.Customers[cell] : 0;
            lesser.Shop.Wanting[cell] = lesser.Shop.Customers[cell];
        }
        RotaOpenHours(lesser, 0x7F, 9, 12);
        lesser.Problem.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 0, 30, 40) };
        lesser.Problem.Worker[0].Wage = 5000;
        RotaRoster(lesser, 0, 0, 0x7F, 9, 12);
        RotaLadderResult fits = RotaClimb(lesser);
        Check(fits.Recommended == 1 && fits.Rungs[1].Source == 4 && fits.Rungs[1].Sum.Net < fits.Rungs[0].Sum.Net && fits.Rungs[1].Out == 0 && fits.Rungs[0].Out == 1,
            "rota ladder: the plan that gives a cashier the 30 hours they ask for is advised though it is worth less (" + fits.Rungs[1].Sum.Net + " against " + fits.Rungs[0].Sum.Net + ")");
        Check(CompanySource("CompanyApp.RotaPlans.cs").Contains("more < 0 ? \"reallife_rota_advice_best_less\" : \"reallife_rota_advice_best\"")
              && CompanySource("CompanyDirector.RotaFill.cs").Contains("gain < 0 ? \"reallife_rota_confirm_plan_less\" : \"reallife_rota_confirm_plan\"")
              && CompanySource("CompanyDirector.RotaFill.cs").Contains("CompanyText.Money(Math.Abs(gain) / 100f)"),
            "rota ladder: a plan worth less is said to be worth less, never \"-59.40 a week more\"");
        Check(RotaFixKeys("reallife_rota_advice_best_less", "reallife_rota_confirm_plan_less"), "rota ladder: the words for a plan worth less are in English, Portuguese and Chinese");

        // An hour with nobody coming is closed because it does not pay, not because nobody can be at the till.
        RotaCase quiet = RotaOneTill();
        RotaOpenHours(quiet, 0x7F, 22, 24);
        RotaRoster(quiet, 0, 0, 0x7F, 9, 17);
        RotaRoster(quiet, 0, 0, 0x7F, 22, 24);
        RotaLadderResult closes = RotaClimb(quiet);
        bool noCrew = false, noPay = false;
        foreach (RotaChange change in closes.Rungs[1].Changes)
        {
            noCrew |= change.Why == RotaWhy.NoCrew && change.From <= 23 && change.To > 23;
            noPay |= change.Why == RotaWhy.NoPay && change.From <= 23 && change.To > 23;
        }
        Check(!noCrew && noPay && closes.Rungs[0].Served[23] == 0 && closes.Rungs[0].Come[23] == 0,
            "rota ladder: 23:00, with the cashier there and nobody coming, is closed because it does not pay");
        Check(CompanySource("RotaLadder.Steps.cs").Contains("int missing = last == RotaWhy.NoCrew ? Unmanned(first, d * Hours + from) : -1;"),
            "rota ladder: an hour closed for want of a crew names the group nobody stands at, not always the first");

        // What a hire is paid once is set against what the hires add together over the best week without them.
        RotaCase hires = RotaSouvenirs();
        hires.Shop.HireCost = 500000;
        RotaLadderResult paid = RotaClimb(hires);
        bool back = paid.Count >= 4;
        for (int i = 2; i < paid.Count; i++)
        {
            long together = paid.Rungs[i].Sum.Net - paid.Rungs[1].Sum.Net;
            back &= paid.Rungs[i].OneOff == 500000L * paid.Rungs[i].Hires && paid.Rungs[i].WeeksBack == (int)((paid.Rungs[i].OneOff + together - 1) / together);
        }
        Check(back && paid.Rungs[paid.Count - 1].WeeksBack == 2,
            "rota ladder: three hires paid 5,000 each are paid back by what the three add, in 2 weeks, not by the last one's gain alone (" + paid.Rungs[paid.Count - 1].WeeksBack + ")");

        // A day holds one shift for somebody brought up to their least hours: twelve hours, not fourteen.
        RotaCase round = RotaOneTill();
        RotaOpenHours(round, 0x7F, 0, 24);
        Check(RotaFit.Room(round.Shop, round.Problem, round.Shop.Open, 0) == 7 * RotaCatalogue.ShiftCap,
            "rota ladder: a week open round the clock holds " + 7 * RotaCatalogue.ShiftCap + " hours for one person, a shift a day (" + RotaFit.Room(round.Shop, round.Problem, round.Shop.Open, 0) + ")");

        // The log line of the plans says why the ladder ended and which plan was made to fit everybody.
        string jobs = CompanySource("CompanyDirector.RotaJobs.cs");
        Check(jobs.Contains("l.Stop == RotaEnd.Stock ? \"one more for \"") && jobs.Contains("r.Source == 4 ? \"the hours kept and more, so everybody has their hours\""),
            "rota ladder: the log names the stop for the goods and the plan made to fit everybody");
    }

    // True if every key is in en.json, pt.json and zh-cn.json.
    private static bool RotaFixKeys(params string[] keys)
    {
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            string text = System.IO.File.ReadAllText(System.IO.Path.Combine(LocalesFolder(), code + ".json"));
            foreach (string key in keys)
            {
                if (!text.Contains("\"" + key + "\":"))
                {
                    return false;
                }
            }
        }
        return true;
    }
}
