using System;
using RealLifeOverhaul.Economy;

// Running a business (Scripts/Economy/Company): the faults found when the folder was cleaned up, each checked against a
// scripted case that the old code got wrong. A campaign's report counts its price and its give-away once and compares
// day for day; the day's pass reads yesterday's statement after the mod's own costs are in it; an average day is over
// the statements there are; a term's claims stay with a cover past a cancel; a licence renewing by itself is tried until
// paid, and one newly needed has a grace; no overtime runs through a strike; a rival tycoon keeps the waits; a strike's
// line names its last day.
internal static partial class Program
{
    private static string CompanySource(string file)
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Economy\Company"));
        string path = System.IO.Path.Combine(folder, file);
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
    }

    // The body of a method in a source: from its line to the next member at the same depth.
    private static string CompanyMethod(string source, string head)
    {
        int at = source.IndexOf(head, StringComparison.Ordinal);
        if (at < 0)
        {
            return string.Empty;
        }
        int end = source.IndexOf("\n        }\n", at, StringComparison.Ordinal);
        return end < 0 ? source.Substring(at) : source.Substring(at, end - at);
    }

    // True if every text is found in a body, each after the one before.
    private static bool InOrder(string body, params string[] texts)
    {
        int at = 0;
        foreach (string text in texts)
        {
            int found = body.IndexOf(text, at, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }
            at = found + text.Length;
        }
        return true;
    }

    private static void CompanyFixes()
    {
        CompanyFixReport();
        CompanyFixBooks();
        CompanyFixParts();
    }

    private static void CompanyFixReport()
    {
        // A discount day booked on day 10 for day 11: free to book, it gives away 15 per cent of 1,300, which the game's
        // statement of day 11 takes off as a marketing cost; the cost is the give-away, counted once.
        int given = CampaignRules.Discount(1300);
        var shop = new ShopFile();
        CampaignRules.Book(shop, (int)CampaignKind.DiscountDay, 0, 12, 10);
        CampaignRules.Gave(shop, given);
        Check(given == 195 && shop.PushCost == 195 && shop.PushGiven == 195, "company report: a discount day's give-away is part of its cost and kept as given away (" + shop.PushGiven + ")");
        CampaignReport discount = CampaignRules.Measure("Corner Shop", (int)CampaignKind.DiscountDay, shop.PushFrom, 1, shop.PushCost, shop.PushGiven, CampaignBook(11, 0, given));
        Check(discount.ProfitDuring == 420 && discount.Cost == 195 && discount.Extra == -75,
            "company report: a discount day's give-away is counted once, as its cost, and not again in the profit of its day (extra " + discount.Extra + ", wanted -75)");

        // A giveaway hired on Flare: a price of 500 the day before, and 195 given away on each of its two days.
        var giveaway = new ShopFile();
        CampaignRules.Book(giveaway, (int)CampaignKind.Giveaway, 500, 12, 10);
        CampaignRules.Gave(giveaway, given);
        CampaignRules.Gave(giveaway, given);
        CampaignReport both = CampaignRules.Measure("Corner Shop", (int)CampaignKind.Giveaway, 11, 2, giveaway.PushCost, giveaway.PushGiven, CampaignBook(11, 500, given));
        Check(giveaway.PushCost == 890 && both.ProfitBefore == 600 && both.ProfitDuring == 840 && both.Extra == -650,
            "company report: a price and two days' give-away are each counted once: 240 more made in two days less 890 (extra " + both.Extra + ", wanted -650)");
        CampaignRules.Close(giveaway);
        Check(giveaway.PushGiven == 0 && giveaway.PushCost == 0, "company report: a closed campaign keeps nothing of what it gave away");
        CampaignRules.Book(giveaway, (int)CampaignKind.Flyers, 150, 12, 30);
        Check(giveaway.PushGiven == 0, "company report: a campaign booked gives nothing away until its day");

        // The days before a business's first statement count in neither half; what is left compares day for day.
        CampaignReport young = CampaignRules.Measure("Corner Shop", (int)CampaignKind.Launch, 5, 3, 750, 0, CampaignBook(5, 750, 0, 4));
        Check(young.DaysBefore == 1 && young.DaysDuring == 3 && young.CustomersBefore == 300 && young.Footfall == 20 && young.Extra == 360 - 750,
            "company report: one day before against three during counts that day three times: 20 per cent more customers, not 260 (" + young.Footfall + ", extra " + young.Extra + ")");
        CampaignRules.DayBook gap = (int day, out int customers, out int takings, out int profit) =>
        {
            bool found = CampaignBook(11, 150)(day, out customers, out takings, out profit);
            return found && day != 12;
        };
        CampaignReport missing = CampaignRules.Measure("Corner Shop", (int)CampaignKind.Flyers, 11, 3, 150, 0, gap);
        Check(missing.DaysBefore == 3 && missing.DaysDuring == 2 && missing.ProfitBefore == 600 && missing.CustomersBefore == 200 && missing.Extra == 90,
            "company report: a day of the campaign the game has no figures for: the three days before count as two (" + missing.ProfitBefore + ", extra " + missing.Extra + ")");
        CampaignReport odd = CampaignRules.Measure("Corner Shop", (int)CampaignKind.Flyers, 11, 3, 150, 0, (int day, out int customers, out int takings, out int profit) =>
        {
            customers = day == 10 ? 101 : 100;
            takings = 1000;
            profit = day == 9 ? 150 : day == 10 ? 151 - 150 : 0;
            return day >= 9 && day <= 11;
        });
        Check(odd.DaysBefore == 2 && odd.DaysDuring == 1 && odd.CustomersBefore == 101 && odd.ProfitBefore == 151,
            "company report: a scaled figure is rounded to the nearest whole (" + odd.CustomersBefore + ", " + odd.ProfitBefore + ")");

        // The save: what was given away so far comes back, a line from before it was kept gave nothing away.
        var state = new CompanyState { Seed = 3 };
        var inFlight = new ShopFile { Place = "Main%20Street~1", FirstDay = 2 };
        CampaignRules.Book(inFlight, (int)CampaignKind.Giveaway, 500, 12, 10);
        CampaignRules.Gave(inFlight, given);
        state.Shops.Add(inFlight);
        string line = CompanyStore.Write(state);
        CompanyState back = CompanyStore.Read(line, out int version);
        Check(back != null && back.Shops[0].PushGiven == 195 && back.Shops[0].PushCost == 695 && CompanyStore.Write(back) == line,
            "company report: what a campaign gave away so far comes back from the save, and the line is written the same again");
        CompanyState older = CompanyStore.Read(line.Replace("|195^e:", "^e:"), out version);
        Check(older != null && older.Shops[0].PushGiven == 0 && older.Shops[0].PushCost == 695 && older.Shops[0].Push == (int)CampaignKind.Giveaway,
            "company report: a line from before it was kept reads as nothing given away");
        CompanyState wrong = CompanyStore.Read(line.Replace("|195^e:", "|9999^e:"), out version);
        Check(wrong != null && wrong.Shops[0].PushGiven == 695, "company report: no more is ever taken as given away than the campaign cost");
    }

    private static void CompanyFixBooks()
    {
        Check(CompanyCatalogue.DayAverage(14000, 7) == 2000 && CompanyCatalogue.DayAverage(6000, 3) == 2000 && CompanyCatalogue.DayAverage(500, 0) == 0 && CompanyCatalogue.DayAverage(-5, 2) == 0,
            "company books: an average day is the sum over the statements found, three for a business three days old (not a seventh of it)");
        string game = CompanySource("CompanyGame.cs");
        string books = CompanyMethod(game, "public static void ReadBooks(");
        Check(books.Contains("shop.Statements++") && books.Contains("CompanyCatalogue.DayAverage(shops[i].Average, shops[i].Statements)") && !books.Contains("/= 7"),
            "company books: the game's reader averages over the statements it found");

        // The day's pass: yesterday's statement brought up to date before it is read, the parts' own costs booked, then the
        // statement brought up to date again and read for the profit shown and the run of losses.
        string day = CompanySource("CompanyDirector.Day.cs");
        string pass = CompanyMethod(day, "private void OnDay()");
        Check(InOrder(pass, "Read(day, false, true,", "PartDay(part, day)", "Books(day, next)"), "company books: the day's pass reads the books, runs the parts, then reads the books again");
        Check(InOrder(CompanyMethod(day, "private void Read(int day, bool ranBefore, bool refresh,"), "if (refresh)", "Refresh(day - 1)", "CompanyGame.ReadBooks(_shops, day)"),
            "company books: the first reading brings yesterday's statement up to date before reading it");
        Check(InOrder(CompanyMethod(day, "private void Books(int day, bool countLoss)"), "Refresh(day - 1)", "CompanyGame.ReadBooks(_shops, day)", "file.LossRun ="),
            "company books: a loss is counted from the statement with the company's own costs of that day in it");
        Check(InOrder(CompanyMethod(day, "private void Refresh(int day)"), "GameBooks.Refresh(day)", "catch (Exception e)"), "company books: bringing a statement up to date is the economy's own, and a fault in it is caught");
        string marketing = CompanySource("CompanyDirector.Marketing.cs");
        Check(InOrder(CompanyMethod(marketing, "partial void MarketingDay(int day)"), "CampaignRules.Gave(file, given)", "Refresh(yesterday)", "int gave = file.PushGiven", "CampaignRules.Measure(", "file.PushCost, gave,"),
            "company books: a report reads its last day's statement after the give-away is in it, and is told what was given away");
    }


    private static void CompanyFixParts()
    {
        // Cover: a term's claims follow the place and kind past a cancel, into the next policy's price and the insurers' limits.
        int fire = (int)CoverKind.Fire;
        var state = new CompanyState();
        var policy = new Policy();
        CoverRules.Buy(policy, 1, 20, 10, 60);
        Claim first = CoverRules.Open(state, policy, "High%20Street~1", "Corner Shop", fire, 1000, 15);
        CoverRules.File(first, policy, 16);
        CoverRules.End(policy);
        Check(!policy.Active && CoverRules.Carried(policy) == 1 && CoverRules.Premium(fire, 1, 2000, 100, CoverRules.Carried(policy), 1000) == 25
              && CoverRules.WillWrite(policy, 0) && CoverRules.WillWrite(policy, 1),
            "company cover: a claim, a cancel and the same cover bought again the same day costs 25 a day, the claim's loading, not 20");
        Claim second = CoverRules.Open(state, new Policy { Insurer = 1, From = 10, Until = 69 }, "High%20Street~1", "Corner Shop", fire, 1000, 17);
        CoverRules.File(second, policy, 18);
        Check(CoverRules.Carried(policy) == 2 && !CoverRules.WillWrite(policy, 0) && !CoverRules.Declines(policy, 0) && CoverRules.WillWrite(policy, 1) && CoverRules.WillWrite(policy, 2),
            "company cover: a claim put in after the cancel counts too, and the cheap insurer, which stands one, will not take the cover on after two");
        CoverRules.Buy(policy, 1, CoverRules.Premium(fire, 1, 2000, 100, CoverRules.Carried(policy), 1000), 18, 60);
        Check(policy.Active && policy.Claims == 0 && CoverRules.Carried(policy) == 0 && policy.Premium == 30, "company cover: bought again, it is priced on both claims and its own term starts with none");
        var declined = new Policy();
        CoverRules.Buy(declined, 1, 20, 10, 60);
        declined.Claims = 3;
        CoverRules.Renew(declined, fire, 2000, 100, 1000, 70, 60);
        Check(CoverRules.Declines(declined, 1) && CoverRules.Carried(declined) == 3 && !CoverRules.WillWrite(declined, 0) && CoverRules.WillWrite(declined, 2),
            "company cover: an insurer that declines after three claims leaves them on the place: the cheap one will not write it either, the dear one will");
        var shop = new ShopFile { Place = "High%20Street~1" };
        shop.Cover[0].Claims = 2;
        var saved = new CompanyState { Seed = 1 };
        saved.Shops.Add(shop);
        CompanyState back = CompanyStore.Read(CompanyStore.Write(saved), out int version);
        Check(back != null && !back.Shops[0].Cover[0].Active && back.Shops[0].Cover[0].Claims == 2, "company cover: the last term's claims of a place with no policy come back from the save");
        Check(RotaFixKeys("reallife_co_policy_refused", "reallife_co_policy_carried") && RotaFixValues("reallife_co_policy_refused", "{insurer}", "{claims}"),
            "company cover: the page says why an insurer will not take the cover on, and what the premium counts");

        // Licences: one that renews by itself is tried every day until its fee is paid, and its running out is said once.
        var auto = new Licence { Until = 49, Auto = true };
        Check(LicenceRules.Day(auto, 50) == LicenceNote.Renew && LicenceRules.Unpaid(auto, 50) && LicenceRules.Day(auto, 51) == LicenceNote.Renew && !LicenceRules.Unpaid(auto, 51)
              && LicenceRules.Day(auto, 58) == LicenceNote.Renew,
            "company licences: a licence renewing by itself whose fee could not be paid is said to have run out once and tried again each day");
        LicenceRules.Grant(auto, (int)LicenceKind.Signage, 58);
        Check(auto.Until == 97 && !auto.Reminded && LicenceRules.Valid(auto, 58) && LicenceRules.Day(auto, 59) == LicenceNote.None, "company licences: paid at last, its term runs from that day");
        var missed = new Licence { Until = 49, Auto = true };
        Check(LicenceRules.Day(missed, 53) == LicenceNote.Renew && LicenceRules.Unpaid(missed, 53), "company licences: a day the pass did not run is no lapse in silence: the next day renews it, or says it ran out");
        Check(LicenceRules.Day(new Licence { Until = 49 }, 51) == LicenceNote.None, "company licences: one not renewing by itself is said to lapse once, the day after its last");
        var grown = new ShopFile();
        LicenceRules.First(grown, LicenceRules.Needs(true, false, false), 10, true);
        int late = 1 << (int)LicenceKind.Late, needs = LicenceRules.Needs(true, false, true);
        int granted = LicenceRules.Newly(grown, needs, 20);
        Check(granted == late && grown.Permits[(int)LicenceKind.Late].Until == 24 && LicenceRules.Missing(grown, needs, 24) == 0 && LicenceRules.Missing(grown, needs, 25) == late
              && LicenceRules.Newly(grown, needs, 30) == 0,
            "company licences: a business that starts to open late has five days to buy the permit, once, as a new business has");
        Check(CompanyMethod(CompanySource("CompanyGame.cs"), "public static void ReadShops(").Contains("LicenceRules.Newly(file, shop.Needs, day);"),
            "company licences: the reading of the businesses gives a newly needed licence its grace");

        // A strike: no overtime runs or is paid through it, and none is agreed while one is called.
        var busy = new ShopFile();
        StaffRules.OvertimeAnswer(busy, true, 90, 5);
        StaffRules.Call(busy, CompanyCatalogue.Standard, 6);
        Check(busy.OvertimeEnd == 7 && StaffRules.OvertimeRuns(busy, 6) && !StaffRules.OvertimeRuns(busy, 7) && StaffRules.Footfall(busy, 7) == CompanyCatalogue.StrikeFootfall,
            "company staff: overtime agreed before a strike stops the day the strike begins, and adds no customers to its days");
        var asked = new ShopFile { OvertimeAsk = 6 };
        StaffRules.Call(asked, CompanyCatalogue.Standard, 6);
        Check(asked.OvertimeAsk == 0 && asked.OvertimeFrom == 0, "company staff: an offer of overtime waiting when a strike is called is dropped");
        var called = new ShopFile { OvertimeAsk = 6 };
        called.StrikeFrom = 8;
        called.StrikeEnd = 10;
        StaffRules.OvertimeAnswer(called, true, 90, 7);
        Check(called.OvertimeFrom == 0 && called.OvertimeAsk == 0, "company staff: overtime is not agreed while a strike is called");
        var both = new ShopFile { OvertimeFrom = 5, OvertimeEnd = 8, OvertimeCost = 90, StrikeFrom = 6, StrikeEnd = 9 };
        Check(!StaffRules.OvertimeRuns(both, 6) && StaffRules.OvertimeRuns(both, 5) && StaffRules.Footfall(both, 6) == CompanyCatalogue.StrikeFootfall,
            "company staff: a save with both on the same day pays and counts no overtime on the strike's days");
        Check(CompanySource("CompanyDirector.Staff.cs").Contains("if (StaffRules.OvertimeRuns(file, day - 1) && file.OvertimeCost > 0)"), "company staff: overtime is paid only for a day it was worked");
        Check(InOrder(CompanyMethod(CompanySource("CompanyDirector.Staff.cs"), "private void ShopStaffDay("), "StaffRules.Call(file, _level, day);", "StaffRules.OvertimeOffered("),
            "company staff: no overtime is offered the morning a strike is called (the strike has its turn first)");

        // A rival tycoon's offer keeps the waits and stays away on Relaxed.
        StaffFile wanted = Worker(1000, 1250, 90);
        Check(StaffRules.RivalMay(wanted, CompanyCatalogue.Standard, 10) && !StaffRules.RivalMay(wanted, CompanyCatalogue.Relaxed, 10), "company staff: no rival tycoon poaches on Relaxed");
        wanted.PoachRest = 20;
        Check(!StaffRules.RivalMay(wanted, CompanyCatalogue.Hardcore, 19) && StaffRules.RivalMay(wanted, CompanyCatalogue.Hardcore, 20), "company staff: nor somebody within 21 days of answering an offer");
        string rivals = CompanySource("CompanyDirector.Rivals.cs");
        Check(InOrder(rivals, "Save.Day < _state.PoachRest", "StaffRules.RivalMay(f, _level, Save.Day)", "StaffRules.Offer(best,", "_state.PoachRest = Save.Day + CompanyCatalogue.PoachCompanyRestDays;"),
            "company staff: and a week passes between any two offers, the company's own rivals' and the tycoons'");

        // The standing meter and the strike line.
        Check(CompanySource("CompanyApp.Staff.cs").Contains("StaffRules.StandsLow(f) ? CellTone.Red"), "company staff: the standing meter is red where the strike rule counts somebody low (30 itself)");
        var strike = new ShopFile { StrikeFrom = 7, StrikeEnd = 9 };
        Check(StaffRules.StrikeShown(strike, 6) == 7 && StaffRules.StrikeShown(strike, 7) == 8 && StaffRules.StrikeShown(strike, 8) == 8,
            "company staff: a strike called names its first day, one running its last (day 8, back on day 9)");
        Check(CompanySource("CompanyApp.cs").Contains("StaffRules.StrikeShown(file, day).ToString()") && CompanySource("CompanyApp.Staff.cs").Contains("StaffRules.StrikeShown(file, day).ToString()"),
            "company staff: the business page and the staff list say the same day");
    }
}
