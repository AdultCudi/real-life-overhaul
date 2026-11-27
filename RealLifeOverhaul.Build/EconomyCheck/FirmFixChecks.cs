using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;
using BizLine = RealLifeOverhaul.Economy.BizLine;
using BizLines = RealLifeOverhaul.Economy.BizLines;

// The firm account's Put cash back and the bankers' refusal, what the account counts, the way to the owner's desk on a firm's page and in the notice that a session waits,
// and closing a data centre. Each check names what it guards and is made to fail by the fault it was written for. Game-free; the sources are read for what only the game can do.
internal static partial class Program
{
    // ---------------------------------------------------------------- Put cash back and the bankers

    private static void FirmMoneyFixChecks()
    {
        Cap c = FundedCap();
        long fair = StartupMoney.Fair(c);
        long over = StartupMoney.Related(c, StartupMoney.TierPrice(fair, 2), fair, "Shop", 13);
        Check(over == fair && c.Acc.RelatedOver == fair && c.Acc.Taken == fair && StartupMoney.OwedBack(c) && ExitStockRules.ListBlocked(c) == ExitStockRules.BlockedTaken,
            "put cash back: a deal paid at double the fair price counts the excess as taken, so the button is there and the bankers refuse for exactly that (" + c.Acc.Taken + ")");
        Check(StartupMoney.Repay(c, Raise, Raise, 20) == fair && !StartupMoney.OwedBack(c) && c.Acc.Taken == 0 && c.Acc.RelatedOver == fair && ExitStockRules.ListBlocked(c) == ExitStockRules.BlockedNone,
            "put cash back: Repay clears the excess, the button goes, and the listing passes (the deal stays in the pack as history)");
        for (int step = StartupMoney.StepAsked; step <= StartupMoney.StepCharged; step++)
        {
            Cap s = FundedCap();
            StartupMoney.Misuse(s, Raise / 4, 100, false, "ba:transaction_vehiclepurchase");
            s.Acc.Step = step;
            s.Acc.Stonewalled = step == StartupMoney.StepDemand;
            StartupMoney.Repay(s, Raise, Raise, 110);
            Check(s.Acc.Taken == 0 && s.Acc.Step == StartupMoney.StepNone && !s.Acc.Stonewalled && ExitStockRules.ListBlocked(s) == ExitStockRules.BlockedNone,
                "put cash back: with everything put back no ladder step is left standing (step " + step + " cleared, so the listing is open)");
        }
        Cap stale = FundedCap();
        stale.Acc.Step = StartupMoney.StepDemand;
        stale.Acc.Stonewalled = true;
        Check(stale.Acc.Taken == 0 && ExitStockRules.ListBlocked(stale) == ExitStockRules.BlockedLadder && !StartupMoney.OwedBack(stale),
            "put cash back: a save from before has a demand standing with nothing taken, which no button could clear and the bankers read as the board's");
        Check(StartupMoney.Settle(stale) && stale.Acc.Step == StartupMoney.StepNone && !stale.Acc.Stonewalled && ExitStockRules.ListBlocked(stale) == ExitStockRules.BlockedNone && !StartupMoney.Settle(stale),
            "put cash back: such a firm is mended once, at load and by the day, and a firm in good order is left alone");
        Cap owing = FundedCap();
        StartupMoney.Misuse(owing, Raise / 10, 100, false, "a");
        owing.Acc.Step = StartupMoney.StepAsked;
        Check(!StartupMoney.Settle(owing) && owing.Acc.Step == StartupMoney.StepAsked, "put cash back: a question with money still taken is not settled for the player");
        Check(StartupMoney.TierPrice(fair, 0) == fair && StartupMoney.TierPrice(fair, 1) == fair * 3 / 2 && StartupMoney.TierPrice(fair, 2) == fair * 2 && StartupMoney.TierPrice(fair, -3) == fair,
            "put cash back: the price a stepper shows is the one function the deal pays (fair, half again, double)");

        string app = ModSource(@"Tech\Venture\TechApp.Money.cs"), live = ModSource(@"Ui\Tech\TechLive.Money.cs"), money = ModSource(@"Tech\TechDirector.Money.cs");
        Check(Occurs(app, "StartupMoney.OwedBack(c)") >= 3 && Occurs(live, "StartupMoney.OwedBack(c)") >= 3 && !Regex.IsMatch(app + live, @"Acc\.Taken > 0")
              && money.Contains("!StartupMoney.OwedBack(c) || Save == null"),
            "put cash back: the old interface, the kit and Repay all ask the one test the listing blocks on, so none can hide what another counts");
        Check(!Regex.IsMatch(app + live + money, @"1500 : [^;]*2000") && Occurs(app, "StartupMoney.TierPrice(") == 1 && Occurs(live, "StartupMoney.TierPrice(") == 1 && money.Contains("StartupMoney.TierPrice(fair, tier)"),
            "put cash back: the shown price and the paid price come from one function in both interfaces and the deal");
        Check(Occurs(money, "StartupMoney.Settle(") == 2 && MemberText(money, "private void StartMoney()").Contains("StartupMoney.Settle(") && MemberText(money, "private bool MoneyDay(").Contains("StartupMoney.Settle(c)"),
            "put cash back: a standing step with nothing taken is cleared when the save loads and each day");

        string exits = ModSource(@"Tech\TechDirector.Exits.cs") + ModSource(@"Tech\TechDirector.ExitStock.cs");
        string refuse = MemberText(exits, "private void RefuseListing(");
        Check(Occurs(exits, "RefuseListing(") == 3 && !Regex.IsMatch(exits, @"ShowError\(owed") && refuse.Contains("c != null ? Level(c.Acc.Taken)")
              && refuse.Contains("blocked == ExitStockRules.BlockedTaken ? \"reallife_exit_refuse_taken\" : \"reallife_exit_refuse_ladder\""),
            "put cash back: both refusals of the bankers go through one place, which names the money counted as taken with its sum, or the board's open matter");
        string taken = En("reallife_exit_refuse_taken"), ladder = En("reallife_exit_refuse_ladder");
        Check(taken.Contains("{amount}") && taken.Contains("{firm}") && taken.Contains(En("reallife_vc_button_repay")) && taken.Contains("firm account") && !ladder.Contains("taken") && ladder.Contains("board") && taken != ladder,
            "put cash back: the refusal for money counted as taken says the sum, the firm and the button to press, and the one for the board does not claim money");
    }

    // ---------------------------------------------------------------- what the firm account counts

    private static void FirmAccountCountsChecks()
    {
        Cap c = FundedCap();
        long before = StartupMoney.Balance(c);
        StartupMoney.Spent(c, Raise / 5);
        Check(StartupMoney.Balance(c) == before - Raise / 5 && StartupMoney.Floor(c, 40) == before - Raise / 5,
            "firm account: a purchase at the firm's address lowers its balance and so the floor, whatever the wallet holds");
        string hook = MemberText(ModSource(@"Tech\TechDirector.Money.cs"), "private void OnMoneyEvent(string gameEvent)");
        int spent = hook.IndexOf("StartupMoney.Spent(", StringComparison.Ordinal), under = hook.IndexOf("if (!under)", StringComparison.Ordinal), look = hook.IndexOf("LastTransaction(save)", StringComparison.Ordinal);
        Check(spent > look && look > 0 && under > spent && hook.Contains("bool under = wallet < _floorNow") && hook.Contains("_spentScans") && hook.Contains("!_funded") && !hook.Contains("wallet >= _floorNow"),
            "firm account: a purchase at the firm's address is read whether or not the wallet is under the floor, with a budget of its own, and only the floor's shortfall waits for the wallet to fall");
        string app = ModSource(@"Tech\Venture\TechApp.Money.cs"), live = ModSource(@"Ui\Tech\TechLive.Money.cs");
        string counts = En("reallife_vc_acct_counts");
        Check(app.Contains("\"reallife_vc_acct_counts\"") && live.Contains("\"reallife_vc_acct_counts\"") && counts.Contains("not a separate bank account") && counts.Contains("takings") && counts.Contains("dividends"),
            "firm account: both interfaces say in a line what the figure counts and that it is not a separate bank account");
        string related = En("reallife_vc_acct_related");
        Check(related.Contains("when it was paid") && !related.Contains("and counted as taken."), "firm account: the deals line does not say the excess is still taken after it was put back");
    }

    // ---------------------------------------------------------------- the owner's desk

    private static void FirmDeskFixChecks()
    {
        var fmt = new SectorWords();
        SectorFile f = SectorFresh(3);
        var ff = new FirmsFile { Seed = 3 };
        foreach (string address in new[] { "5 Elm Street", string.Empty })
        {
            foreach (int people in new[] { 0, 6 })
            {
                var s = new SectorPlace { Place = "p", Name = "Firm", Kind = (int)FirmKind.Consultancy, Crew = Team(32), Address = address, Offers = 0, OffersMonth = 4500 };
                s.Team = new SectorTeam { People = people, Builders = 4, Designers = 1, Support = 2, Sellers = 1, Reliability = 1 };
                s.Due.Add(new SectorDue { Kind = SectorRules.DeskSprint, Due = true });
                StudioScreen v = SectorViews.Firm(f, ff, s, 10, fmt);
                int desks = (v.Primary != null && v.Primary.Act == SectorAct.Desk ? 1 : 0) + v.Others.FindAll(a => a.Act == SectorAct.Desk).Count;
                string where = address.Length > 0 ? "reallife_tech_mkt_desk_where" : "reallife_tech_mkt_desk_where_plain";
                ViewSection sec = v.Sections.Find(x => x.TitleKey == "reallife_tech_mkt_sec_desk");
                bool said = sec != null && sec.Lines.Count > 0 && sec.Lines[0].Key == where && (address.Length == 0 || Array.IndexOf(sec.Lines[0].Pairs, address) >= 0);
                Check(desks == 1 && said, "owner's desk: the firm's page always has one Go to the desk and a line of where it is (" + people + " people, address '" + address + "', " + desks + " button(s))");
            }
        }
        var off = new SectorPlace { Place = "p", Name = "Firm", Kind = (int)FirmKind.Consultancy, Crew = Team(32), Desks = false };
        off.Team = new SectorTeam { People = 6, Builders = 4 };
        off.Due.Add(new SectorDue { Kind = SectorRules.DeskSprint, Due = true });
        StudioScreen none = SectorViews.Firm(f, ff, off, 10, fmt);
        Check(none.Others.FindAll(a => a.Act == SectorAct.Desk).Count == 0 && (none.Primary == null || none.Primary.Act != SectorAct.Desk), "owner's desk: with desk sessions off the page offers no way to a desk");
        string old = ModSource(@"Tech\Sector\TechApp.Sector.cs"), kit = ModSource(@"Ui\Tech\TechLive.FirmPage.cs") + ModSource(@"Ui\Pages\StudioPages.cs");
        Check(Regex.IsMatch(old, @"a\.Act == SectorAct\.Desk[\s\S]{0,200}SectorAction\(") && kit.Contains("screen.Others") && kit.Contains("case SectorAct.Desk:"),
            "owner's desk: both interfaces draw the page's way to the desk, which sets the office as the destination as the job page's does (SectorDo, FirmsGame.Destination)");
        string day = ModSource(@"Sessions\SessionDirector.Day.cs"), say = En("reallife_session_note_waiting"), many = En("reallife_session_note_waiting_many"), button = En("reallife_tech_mkt_button_desk");
        Check(day.Contains("{ \"address\", SessionGame.AddressOf(firstFirm) }") && say.Contains("{address}") && many.Contains("{address}") && say.Contains(button) && many.Contains(button) && say.Length <= 240 && many.Length <= 240,
            "owner's desk: the notice that a session waits says where the office is and names the button that sets it as the destination");
    }

    // ---------------------------------------------------------------- closing a data centre

    private static void FirmCloseChecks()
    {
        var f = new InfraFile();
        DataCentre d = Centre(20, 2, 1, "x"), other = Centre(10, 0, 1, "y");
        f.Centres.Add(d);
        f.Centres.Add(other);
        f.Colo.Add(new ColoContract { Id = 1, Place = d.Place, Client = 1, Racks = 8, Fee = 900000, Start = 10, End = 400 });
        f.Colo.Add(new ColoContract { Id = 2, Place = d.Place, Client = 2, Racks = 4, Fee = 600000, Start = 10, End = 115 });
        f.Colo.Add(new ColoContract { Id = 3, Place = other.Place, Client = 3, Racks = 4, Fee = 500000, Start = 10, End = 400 });
        long book = d.Book;
        CentreClose q = InfraClose.Quote(f, d, 100, 500);
        Check(book > 0 && q.Salvage == book * 500 / 1000 && q.Contracts == 2 && q.Racks == 22 && q.Penalty == 900000 + 600000 * 15 / FirmsCatalogue.MonthDays,
            "close a data centre: the racks fetch the game's selling multiplier of their book value, each client is paid a month's fee or what is left of its term, and only this centre's contracts count (" + q.Salvage + ", " + q.Penalty + ")");
        Check(InfraClose.Quote(f, d, 100, 0).Salvage == 0 && InfraClose.Quote(f, d, 100, 5000).Salvage == book && InfraClose.Quote(f, d, 500, 500).Penalty == 0,
            "close a data centre: a multiplier the game cannot give pays nothing, one over the whole pays the book value at most, and a term that has run out costs nothing");
        Check(InfraClose.End(f, d) == 2 && f.CentreAt(d.Place) == null && f.CentreAt(other.Place) == other && f.Colo.Count == 1 && f.Colo[0].Place == other.Place,
            "close a data centre: its contracts end and it leaves the list, with the other centre and its contracts as they were, so no cost is booked for it again");
        string dir = ModSource(@"Tech\TechDirector.Infra.cs"), close = MemberText(dir, "internal bool CloseCentre(string place)");
        Check(close.Contains("InfraClose.Quote(") && close.IndexOf("ShowInsufficientMoney", StringComparison.Ordinal) < close.IndexOf("VentureGame.Give(salvage", StringComparison.Ordinal)
              && close.IndexOf("VentureGame.Give(salvage", StringComparison.Ordinal) < close.IndexOf("InfraGame.Pay(penalty", StringComparison.Ordinal) && close.Contains("InfraClose.End(_infra, d)")
              && close.Contains("TechBridges.DropLine?.Invoke(reg)") && !Regex.IsMatch(close, @"BuildingHelper|DestroyBuilding|Sell\w*Building|itemInstances"),
            "close a data centre: the cash is checked before anything is booked, the racks come back, the clients are paid, the line is cancelled, and the building and its items are not touched");
        Check(ModSource(@"Wiring\TechLinks.cs").Contains("TechBridges.DropLine = DropLine") && ModSource(@"Tech\TechBridges.cs").Contains("DropLine = null")
              && ModSource(@"Utilities\UtilitiesDirector.Internet.cs").Contains("public void DropLineAt(BuildingRegistration reg)"),
            "close a data centre: the leased line is given up through the tech bridges to the utilities, and let go with them");
        string app = ModSource(@"Tech\Infra\TechApp.Infra.cs"), live = ModSource(@"Ui\Tech\TechLive.Infra.cs");
        Check(Regex.IsMatch(app, @"InfoBox\.Negative, ""reallife_infra_button_close""[\s\S]{0,400}GameConfirm\.Show\(closeTitle, closeBody")
              && Regex.IsMatch(live, @"""reallife_infra_button_close""\), UiButtonKind\.Destructive[\s\S]{0,400}GameConfirm\.Show\(closeTitle, closeBody")
              && app.Contains("_director.CloseAsk(place") && live.Contains("_d.CloseAsk(place"),
            "close a data centre: both interfaces have the button on the hall's page, and the game's question box asks before anything is closed");
        Check(BizLines.Of(InfraGameKey("SaleKey")) == BizLine.Capital && BizLines.Of(InfraGameKey("EndedKey")) == BizLine.Cost && En("reallife_transaction_tech_investment_racks_sale_label").Length > 0
              && En("reallife_transaction_tech_colo_ended_label").Length > 0,
            "close a data centre: the racks' sale is capital, the clients' payment a cost, each with its label");
        string body = En("reallife_infra_close_body");
        Check(En("reallife_infra_button_close").Length > 0 && body.Contains("{salvage}") && body.Contains("{penalty}") && body.Contains("{contracts}") && body.Contains("warehouse stays yours") && body.Contains("leased line"),
            "close a data centre: the question says what comes back, what is paid, what ends and that the building stays the player's");
    }

    // A key the infrastructure's game file holds, read from its source so the checker needs nothing of the game.
    private static string InfraGameKey(string constant)
    {
        Match m = Regex.Match(ModSource(@"Tech\Infra\InfraGame.cs"), constant + @" = ""([a-z_]+)""");
        return m.Success ? m.Groups[1].Value : string.Empty;
    }
}
