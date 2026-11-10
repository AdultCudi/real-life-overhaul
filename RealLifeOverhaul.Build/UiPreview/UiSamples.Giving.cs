using System;
using System.Collections.Generic;
using RealLifeOverhaul.Giving;
using RealLifeOverhaul.Ui;

// The Giving app's samples: the three charities with a standing gift, two loans out and a tax year's giving. The people are the cast the other pages' samples use and the rich
// list name is one of the game's own. Names here are placeholders and never ship.
internal static partial class UiSamples
{
    public static readonly string[] GivingStates = { "charities", "charities_pick", "charities_broke", "charities_session", "loans", "loans_empty", "year", "off" };

    /// <summary>A recording stand-in for the director: each press says which action it was.</summary>
    public sealed class GivingStub : IGiveActions
    {
        public string Last;
        public int Count;
        public int Charity, Step;

        private void Did(string what, int charity, int step)
        {
            Last = what;
            Count++;
            Charity = charity;
            Step = step;
        }

        public void Give(int charity, int step) => Did("give", charity, step);
        public void Monthly(int charity, int step) => Did("monthly", charity, step);
        public void StopMonthly(int charity) => Did("stop", charity, 0);
    }

    public static GiveFacts GivingFactsOf(string state)
    {
        var f = new GiveFacts { Level = 1000, Cash = 84000, Spare = 61500 };
        if (state == "off")
        {
            f.Ready = false;
            return f;
        }
        f.Charities = new[]
        {
            new GiveCharityFact { Id = 0, Pot = 3200, Cap = 280000, PaidMonth = 0, PlayerMonth = 500, PlayerYear = 3000, OthersYear = 11800, PaidYear = 12400, LastMoney = 3100, PlayerTotal = 6500,
                PeopleYear = 14, LastPeople = 3, Standing = 250 },
            new GiveCharityFact { Id = 1, Pot = 900, Cap = 200000, PlayerYear = 1000, OthersYear = 4200, PaidYear = 4300, LastMoney = 900, PlayerTotal = 1000, PeopleYear = 5, LastPeople = 1 },
            new GiveCharityFact { Id = 2, Pot = 0, Cap = 80000, PlayerYear = 0, OthersYear = 2500, PaidYear = 2400, LastMoney = 0, PlayerTotal = 0, PeopleYear = 3, LastPeople = 0 }
        };
        f.TaxGifts = 4000;
        f.Income = 310000;
        f.TaxCap = GivingRules.DeductionCap(f.Income);
        f.TaxClaimed = 4000;
        f.NeighbourYear = 6400;
        f.NeighbourHelped = 21;
        f.RivalsYear = 18500;
        f.RivalLine = "Space Karen has given $12,000 to the city's charities this year, most lately $1,000 to Harbour Food Bank.";
        if (state != "loans_empty")
        {
            f.Loans.Add(new GiveLoanFact { Name = "Maya Okafor", Principal = 1200, Remaining = 800, Paid = 400, Part = 200, Made = 40, Next = 100, State = LoanState.Open });
            f.Loans.Add(new GiveLoanFact { Name = "Tom Becker", Principal = 600, Remaining = 600, Paid = 0, Part = 100, Made = 20, Next = 90, State = LoanState.Open, Misses = 1 });
            f.Loans.Add(new GiveLoanFact { Name = "Hana Sato", Principal = 500, Remaining = 0, Paid = 500, Part = 100, Made = 5, State = LoanState.Repaid, Closed = 70 });
            f.Loans.Add(new GiveLoanFact { Name = "Nadia Haddad", Principal = 400, Remaining = 300, Paid = 100, Part = 100, Made = 8, State = LoanState.WrittenOff, Closed = 80 });
        }
        if (state == "charities_session")
        {
            f.Why = 2;
        }
        if (state == "charities_broke")
        {
            f.Spare = 20;
            f.Cash = 20;
        }
        return f;
    }

    public static VentModel Giving(UiWords t, string state, VentView view = null, IGiveActions act = null)
    {
        VentWords w = VentWordsOf(t);
        GiveFacts f = GivingFactsOf(state);
        view = view ?? new VentView();
        switch (state)
        {
            case "charities_pick": view.Tab = GivePages.TabCharities; view.Pick = GivePages.CharityKey + 1; view.A = 5; break;
            case "loans":
            case "loans_empty": view.Tab = GivePages.TabLoans; break;
            case "year": view.Tab = GivePages.TabYear; break;
        }
        var m = new VentModel();
        GivePages.Fill(m, view, f, act ?? new GivingStub(), w);
        return m;
    }
}
