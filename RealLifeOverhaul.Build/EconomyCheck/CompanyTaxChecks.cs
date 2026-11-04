using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

// The tax additions of the Company app: the running quarter's tax at the game's rate, the reserve put aside for the year's bill (by the player, topped up by the
// finance chief when a quarter closes, back in the cash when the bill comes or the year turns over), the past returns kept, and the save.
internal static partial class Program
{
    private static void CompanyTaxReserve()
    {
        int before = _run, failedBefore = _failed;
        TaxFigures();
        TaxClosing();
        TaxReserveRules();
        TaxYears();
        TaxSaves();
        Console.WriteLine("  the quarter's tax, the reserve, the past returns and the save: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    // A company line as an older version wrote its tax record: cut after the first <keep> fields (those after them are newer).
    private static string TaxRecordCut(string line, int keep)
    {
        int at = line.IndexOf("^t:", StringComparison.Ordinal), end = line.IndexOf("^s:", StringComparison.Ordinal);
        if (at < 0 || end < at)
        {
            return line;
        }
        string[] f = line.Substring(at + 3, end - at - 3).Split('|');
        return line.Substring(0, at + 3) + string.Join("|", f, 0, Math.Min(keep, f.Length)) + line.Substring(end);
    }

    private static void TaxFigures()
    {
        Check(TaxRules.Taxable(10000, 4000) == 6000 && TaxRules.Taxable(4000, 10000) == 0 && TaxRules.Taxable(0, 0) == 0 && TaxRules.Taxable(5000, -300) == 5000,
            "quarter tax: the taxable profit is the takings less the costs that count, never below nothing, a negative cost counted as none");
        Check(TaxRules.QuarterTax(10000, 4000, 10) == 600 && TaxRules.QuarterTax(10000, 4000, 0) == 0 && TaxRules.QuarterTax(4000, 10000, 10) == 0 && TaxRules.QuarterTax(10000, 4000, -5) == 0,
            "quarter tax: the game's rate on the taxable profit; no profit or no rate, no tax");
        Check(TaxRules.QuarterTax(int.MaxValue, 0, 100) > 0 && TaxRules.QuarterTax(int.MaxValue, 0, 100) <= int.MaxValue / 4 && TaxRules.QuarterTax(int.MaxValue, 0, int.MaxValue) <= int.MaxValue / 4,
            "quarter tax: whole-number sums that cannot run over");
        var t = new TaxFile();
        t.Costs[(int)CostKind.Wages] = 700;
        t.Costs[(int)CostKind.Licences] = 300;
        t.YearTax = 200;
        t.Reserve = 150;
        TaxRules.QuarterFigures q = TaxRules.Quarter(t, 9000, 10);
        Check(q.Takings == 9000 && q.Costs == 1000 && q.Taxable == 8000 && q.Tax == 800 && q.Percent == 10 && q.Year == 200 && q.Reserve == 150 && q.Still == 200 + 800 - 150,
            "quarter figures: takings, costs that count, profit taxed, tax at the game's rate, the year's tax already closed, what is set aside and what is still to set aside");
        t.Reserve = 5000;
        Check(TaxRules.Quarter(t, 9000, 10).Still == 0, "quarter figures: a reserve past the year's tax leaves nothing still to set aside, never a negative");
        Check(TaxRules.Quarter(new TaxFile(), 0, 10).Tax == 0 && TaxRules.Quarter(new TaxFile(), -50, 10).Takings == 0, "quarter figures: a quarter with no takings has no tax and no figures below nothing");
    }

    private static void TaxClosing()
    {
        var t = new TaxFile { QuarterFrom = 1, Quarter = 3 };
        t.Costs[(int)CostKind.Marketing] = 1000;
        TaxRules.Close(t, 11000, 31, 10);
        Check(t.ReturnQuarter == 3 && t.ReturnTakings == 11000 && t.ReturnCosts == 1000 && t.ReturnTax == 1000 && t.YearTax == 1000 && t.Quarter == 4 && t.QuarterFrom == 31 && t.ReturnDue == 31 + CompanyCatalogue.ReturnDays,
            "closing a quarter: the return carries its takings, costs and tax at the game's rate, and the tax joins the year's account");
        Check(TaxRules.CostsSum(t) == 0 && t.Settled.Count == 0, "closing a quarter: the costs start again and nothing is on the ring of past returns yet");
        // Filed on time.
        Check(!TaxRules.File(t, 33) && t.Settled.Count == 1 && t.Settled[0].Quarter == 3 && t.Settled[0].Day == 33 && t.Settled[0].Takings == 11000 && t.Settled[0].Costs == 1000
              && t.Settled[0].Tax == 1000 && t.Settled[0].State == TaxRules.StateOnTime && !t.ReturnWaiting, "a return filed on time goes on the ring with its quarter, day, figures and state");
        // The next quarter closes, filed late.
        t.Costs[(int)CostKind.Licences] = 200;
        TaxRules.Close(t, 5000, 61, 10);
        Check(TaxRules.File(t, 61 + CompanyCatalogue.ReturnDays + 2) && t.Settled.Count == 2 && t.Settled[1].State == TaxRules.StateLate && t.Settled[1].Quarter == 4 && t.Settled[1].Tax == 480 && t.YearTax == 1480,
            "a return filed after its day is on the ring as late; the year's account is both quarters' tax");
        // A third quarter closes and is never filed: when the fourth closes it is dropped and counted missed.
        TaxRules.Close(t, 7000, 91, 10);
        int lateBefore = t.LateRun;
        TaxRules.Close(t, 8000, 121, 10);
        Check(t.Settled.Count == 3 && t.Settled[2].State == TaxRules.StateMissed && t.Settled[2].Quarter == 5 && t.Settled[2].Day == 121 && t.LateRun == lateBefore + 1,
            "a return never filed is dropped when the next quarter closes: on the ring as missed, and counted late");
        // The ring keeps the newest four.
        TaxRules.File(t, 122);
        TaxRules.Close(t, 100, 151, 10);
        TaxRules.File(t, 152);
        TaxRules.Close(t, 100, 181, 10);
        TaxRules.File(t, 182);
        Check(t.Settled.Count == TaxRules.SettledKept && t.Settled[0].Quarter == 5 && t.Settled[TaxRules.SettledKept - 1].Quarter == t.ReturnQuarter,
            "the ring of past returns keeps the newest " + TaxRules.SettledKept + ", the oldest first (" + t.Settled[0].Quarter + " to " + t.Settled[t.Settled.Count - 1].Quarter + ")");
        // Closing with the game's old call, without a rate: no tax estimate, nothing else changes.
        var plain = new TaxFile { QuarterFrom = 1, Quarter = 1 };
        TaxRules.Close(plain, 2000, 31);
        Check(plain.ReturnTax == 0 && plain.ReturnTakings == 2000 && plain.ReturnDue == 31 + CompanyCatalogue.ReturnDays, "closing a quarter without a rate works as it always did, with no tax estimate");
        // A return filed that is not waiting adds nothing.
        var none = new TaxFile();
        Check(!TaxRules.File(none, 10) && none.Settled.Count == 0, "filing with no return waiting adds nothing to the ring");
        // The page's rows are never mixed up with another quarter's: a settled entry keeps what it was.
        var keep = new TaxFile { QuarterFrom = 1, Quarter = 7 };
        TaxRules.Close(keep, 3000, 31, 20);
        TaxRules.File(keep, 32);
        SettledReturn first = keep.Settled[0];
        TaxRules.Close(keep, 9000, 61, 20);
        Check(first.Takings == 3000 && first.Tax == 600 && keep.ReturnTax == 1800, "a settled return keeps its own figures when the next quarter closes");
    }

    private static void TaxReserveRules()
    {
        var t = new TaxFile();
        Check(TaxRules.SetAside(t, 500, 2000) == 500 && t.Reserve == 500, "reserve: money set aside moves out of the cash into it");
        Check(TaxRules.SetAside(t, 5000, 300) == 300 && t.Reserve == 800, "reserve: no more than the cash in hand");
        Check(TaxRules.SetAside(t, -50, 1000) == 0 && TaxRules.SetAside(t, 0, 1000) == 0 && TaxRules.SetAside(t, 100, -5) == 0 && t.Reserve == 800, "reserve: nothing for no amount, a negative amount or no cash");
        t.Reserve = CompanyCatalogue.ReserveHeld - 10;
        Check(TaxRules.SetAside(t, 500, 100000) == 10 && t.Reserve == CompanyCatalogue.ReserveHeld, "reserve: it holds " + CompanyCatalogue.ReserveHeld + " at most");
        Check(TaxRules.Release(t) == CompanyCatalogue.ReserveHeld && t.Reserve == 0 && TaxRules.Release(t) == 0, "reserve: released, all of it goes back, and a second release returns nothing");
        // The finance chief's top-up when a quarter closes.
        var c = new TaxFile { YearTax = 3175, Reserve = 0 };
        int moved = TaxRules.TopUp(c, 10000);
        Check(moved == 3175 && c.Reserve == 3175, "finance chief: tops the reserve up by the whole shortfall against the year's tax when the cash allows (" + moved + ")");
        Check(TaxRules.TopUp(c, 10000) == 0, "finance chief: nothing more to top up once the reserve holds the year's tax");
        var short_ = new TaxFile { YearTax = 6000, Reserve = 1000 };
        moved = TaxRules.TopUp(short_, 4000);
        Check(moved == CompanyCatalogue.Share(4000, CompanyCatalogue.ReserveCashShare) && short_.Reserve == 1000 + moved && moved < 5000,
            "finance chief: never more than " + CompanyCatalogue.ReserveCashShare / 10 + "% of the cash in one go (" + moved + " of 4000), the rest for next time");
        var broke = new TaxFile { YearTax = 800 };
        Check(TaxRules.TopUp(broke, 0) == 0 && broke.Reserve == 0 && TaxRules.TopUp(broke, -20) == 0, "finance chief: with no cash, nothing moves and nothing goes below nothing");
        var over = new TaxFile { YearTax = 100, Reserve = 400 };
        Check(TaxRules.TopUp(over, 100000) == 0 && over.Reserve == 400, "finance chief: a reserve already past the year's tax is left alone");
        // An unset reserve changes nothing about how the return is paid: the rules never move money by themselves.
        var unset = new TaxFile { YearTax = 900 };
        TaxRules.Close(unset, 4000, 31, 10);
        Check(unset.Reserve == 0 && !unset.ReserveAuto, "reserve: closing a quarter never moves money on its own; with nothing set and the top-up off, the return is paid from cash as it always was");
        Check(TaxRules.TopUpDay(new TaxFile { QuarterFrom = 241 }, 30) == 271 && TaxRules.TopUpDay(new TaxFile { QuarterFrom = 241 }, 0) == 242, "reserve: the finance chief tops up on the day the running quarter closes");
    }

    private static void TaxYears()
    {
        Check(TaxRules.YearStart(1, 60) == 1 && TaxRules.YearStart(60, 60) == 1 && TaxRules.YearStart(61, 60) == 61 && TaxRules.YearStart(120, 60) == 61 && TaxRules.YearStart(121, 60) == 121
              && TaxRules.YearStart(0, 60) == 1 && TaxRules.YearStart(500, 0) >= 1, "tax year: its first day, the game's years being the days from one multiple of the year's length to the next");
        var t = new TaxFile();
        int released;
        Check(!TaxRules.Year(t, 61, out released) && t.YearFrom == 61 && released == 0, "tax year: the first time a year is known nothing is turned over");
        t.Reserve = 700;
        t.YearTax = 650;
        Check(!TaxRules.Year(t, 61, out released) && released == 0 && t.Reserve == 700 && t.YearTax == 650, "tax year: the same year again changes nothing");
        Check(TaxRules.Year(t, 121, out released) && released == 700 && t.Reserve == 0 && t.YearTax == 0 && t.YearFrom == 121, "tax year: when the game's year turns over the reserve goes back to the cash and the year's account starts afresh");
        Check(!TaxRules.Year(t, 121, out released) && released == 0, "tax year: and only once");
        // A jump of years at once (a long break) is one turn-over.
        t.Reserve = 50;
        Check(TaxRules.Year(t, 481, out released) && released == 50 && t.YearFrom == 481, "tax year: several years passing at once is one turn-over");
    }

    private static void TaxSaves()
    {
        var s = new CompanyState { Seed = 5, FirstDay = 1, LastDay = 200, Age = 199 };
        TaxFile x = s.Tax;
        x.QuarterFrom = 181;
        x.Quarter = 7;
        x.Costs[(int)CostKind.Wages] = 120;
        x.Costs[(int)CostKind.Rent] = 33;
        x.Reserve = 2500;
        x.ReserveAuto = true;
        x.YearTax = 3100;
        x.YearFrom = 121;
        x.ReturnTax = 450;
        x.ReturnDue = 190;
        x.ReturnQuarter = 6;
        x.Settled.Add(new SettledReturn { Quarter = 3, Day = 33, Takings = 11000, Costs = 1000, Tax = 1000, State = TaxRules.StateOnTime });
        x.Settled.Add(new SettledReturn { Quarter = 4, Day = 68, Takings = 5000, Costs = 200, Tax = 480, State = TaxRules.StateLate });
        x.Settled.Add(new SettledReturn { Quarter = 5, Day = 121, Takings = 7000, Costs = 0, Tax = 700, State = TaxRules.StateMissed });
        string line = CompanyStore.Write(s);
        int version;
        CompanyState back = CompanyStore.Read(line, out version);
        TaxFile r = back != null ? back.Tax : null;
        bool same = r != null && r.Reserve == 2500 && r.ReserveAuto && r.YearTax == 3100 && r.YearFrom == 121 && r.ReturnTax == 450 && r.Costs[(int)CostKind.Rent] == 33 && r.Settled.Count == 3
                    && r.Settled[0].Quarter == 3 && r.Settled[1].State == TaxRules.StateLate && r.Settled[2].State == TaxRules.StateMissed && r.Settled[2].Takings == 7000 && r.Settled[1].Tax == 480;
        Check(same, "tax save: the reserve, its top-up switch, the year's account, the waiting return's tax and the ring of past returns round trip");
        Check(back != null && CompanyStore.Write(back) == line, "tax save: what is read back writes the same line again");
        // A line from before: the tax record cut at the field before the reserve.
        string[] parts = line.Split('^');
        string record = Array.Find(parts, p => p.StartsWith("t:", StringComparison.Ordinal)).Substring(2);
        string[] f = record.Split('|');
        string old = string.Join("|", f, 0, 22);   // up to and with the rent's rise, the last figure of the version before
        CompanyState from = CompanyStore.Read(line.Replace("t:" + record, "t:" + old), out version);
        TaxFile o = from != null ? from.Tax : null;
        Check(o != null && o.Reserve == 0 && !o.ReserveAuto && o.YearTax == 0 && o.YearFrom == 0 && o.ReturnTax == 0 && o.Settled.Count == 0 && o.Costs[(int)CostKind.Rent] == 33 && o.Quarter == 7 && o.ReturnQuarter == 6,
            "tax save: a line written before the reserve reads with nothing set aside, no past returns, and the rest of the tax record as it was");
        // The figures that were there before are in the places they were.
        Check(f[0] == "181" && f[1] == "7" && f[21] == "33" && f[22] == "2500" && f[23] == "1", "tax save: the figures of the first version keep their places; the reserve follows them");
        // Out-of-range figures are put right, not trusted.
        var wild = new List<string>(f);
        wild[22] = "99999999";
        wild[24] = "-5";
        wild[27] = "9";
        wild[32] = "7";   // the first entry's state: out of range
        CompanyState bad = CompanyStore.Read(line.Replace("t:" + record, "t:" + string.Join("|", wild)), out version);
        TaxFile w = bad != null ? bad.Tax : null;
        Check(w != null && w.Reserve == CompanyCatalogue.ReserveHeld && w.YearTax == 0 && w.Settled.Count == 3 && w.Settled[0].State == TaxRules.StateMissed,
            "tax save: a reserve past what it can hold, a negative account and a state out of range are put right on reading");
        // Fixed width: four places always, so a record never moves what follows it.
        string empty = CompanyStore.Write(new CompanyState());
        string[] eparts = empty.Split('^');
        string[] ef = Array.Find(eparts, p => p.StartsWith("t:", StringComparison.Ordinal)).Substring(2).Split('|');
        Check(ef.Length == 27 + TaxRules.SettledKept * 6, "tax save: the record has a fixed width, " + ef.Length + " fields, with four places for past returns");
    }
}
