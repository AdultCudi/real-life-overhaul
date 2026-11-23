using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

// A firm a boss contract holds is its buyer's from the day of the sale until the business has actually left the player (flag --sale-terms, with SaleTermChecks.cs): the contract holds it in
// every state (running, resigned, dismissed, at its term) until the hand-over is made; the hand-over is a real one and is believed only when the business is gone; the days between the end
// and the hand-over are the buyer's, costs included; no offer, no listing, no round, no second sale; a fee is never kept for a listing that did not happen; and a save that 1.0.19 left
// wrong is put right once. Game-free where the rules are; the sources are read for what only the game can do.
internal static partial class Program
{
    private static void SaleTermHeldChecks()
    {
        SaleTermHeldStateChecks();
        SaleTermHeldMoneyChecks();
        SaleTermHeldSourceChecks();
        SaleTermHeldTextChecks();
    }

    private static TermDeal HeldBoss(TermFile f, int day = 100)
    {
        return TermBegin(f, TermPlanOf(TermKind.Boss, 0, 0), TermKind.Boss, 0, 0, 0, day);
    }

    private static void SaleTermHeldStateChecks()
    {
        // The contract holds its place in every state until the business has left the player.
        foreach (int state in new[] { TermState.Open, TermState.Resigned, TermState.Dismissed, TermState.Done, TermState.Lapsed })
        {
            var f = new TermFile { Seed = 5 };
            TermDeal d = HeldBoss(f);
            d.State = state;
            Check(f.BossOf("Elm~1") == d && d.Live, "held contract: state " + state + " still holds the firm while it has not been handed over (no offer, no listing, no round, no second sale)");
            d.Handed = true;
            Check(f.BossOf("Elm~1") == null || state == TermState.Open, "held contract: state " + state + " lets go of the firm once it has been handed over");
        }
        // Resigning stamps the day it happened, and the deal goes on being settled each month until the hand-over.
        var g = new TermFile { Seed = 5 };
        TermDeal r = HeldBoss(g);
        var ev = new List<TermEvent>();
        var world = new TermWorld { FirmHere = true, Takings = r.Mrr0, Customers = r.Cust0 };
        SaleTermRules.Day(g, r, r.Day + 100, world, ev);
        ev.Clear();
        Check(SaleTermRules.Resign(r, ev, r.Day + 100 + 7) && r.State == TermState.Resigned && r.EndDay == r.Day + 107, "held contract: a resignation is dated the day it was made, not the last month's step");
        Check(!SaleTermRules.Resign(r, ev, r.Day + 120) && r.EndDay == r.Day + 107, "held contract: a resignation cannot be made twice");
        ev.Clear();
        int salaries = 0, remits = 0;
        for (int day = r.Day + 101; day <= r.Day + 101 + 95; day++)
        {
            SaleTermRules.Day(g, r, day, world, ev);
        }
        foreach (TermEvent e in ev)
        {
            salaries += e.Kind == TermEventKind.Salary ? 1 : 0;
            remits += e.Kind == TermEventKind.Remit ? 1 : 0;
        }
        Check(salaries == 0 && remits >= 3 && r.Live && f_BossHolds(g, r), "held contract: after a resignation the pay stops, the firm is still settled with its owner every month, and the contract still holds it");
        // A business that is not found is no ending until it is called gone, and only then is the contract let go.
        r.Handed = true;
        Check(g.BossOf("Elm~1") == null && !r.Live, "held contract: once handed over the deal is no longer live");
        // The hand-over was once reported done while the contract's own hold stopped it: the sources say it is not so any more.
        string sale = ModSource(@"Economy\Services\ServicesDirector.Sale.cs");
        string hand = MethodText(sale, "public bool HandOver(string place, string buyerName, bool byContract = false)");
        Check(hand.Contains("(!byContract && Held(reg))") && ModSource(@"Wiring\Wiring.ExitLinks.cs").Contains("services.HandOver(place, buyer, true)")
              && ModSource(@"Wiring\ListingLinks.cs").Contains("services.HandOver(place, buyer)"),
            "held contract: the hand-over a contract makes is not stopped by the hold the contract puts on the business, which once reported it done and left the firm with the player; a takeover of the company still leaves a held firm be");
    }

    private static bool f_BossHolds(TermFile f, TermDeal d) => f.BossOf(d.Place) == d;

    private static void SaleTermHeldMoneyChecks()
    {
        // A loss over a stretch: while the contract runs the buyer carries up to the cap and the boss the rest; the days after the end are the buyer's whole.
        Check(SaleTermRules.Cover(-3000, 30, 0, 1000) == 1000, "held contract: while it runs the buyer carries a loss up to its cap");
        Check(SaleTermRules.Cover(-3000, 30, 30, 1000) == 3000, "held contract: a loss in the days after the contract ended is the buyer's whole, whatever the cap");
        Check(SaleTermRules.Cover(-3000, 30, 15, 1000) == 1500 + 1000, "held contract: a stretch that spans the end is split by its days");
        Check(SaleTermRules.Cover(500, 30, 10, 1000) == 0 && SaleTermRules.Cover(-100, 0, 0, 1000) == 0, "held contract: a profit or no days is no cover");
        Check(SaleTermRules.Cover(-3000, 30, 99, 1000) == 3000 && SaleTermRules.Cover(-3000, 30, -5, 1000) == 1000, "held contract: the gap's days are held inside the stretch");
        var f = new TermFile { Seed = 5 };
        TermDeal d = HeldBoss(f);
        Check(SaleTermRules.GapDays(d, 100, 140) == 0, "held contract: a contract still open has no gap");
        var ev = new List<TermEvent>();
        SaleTermRules.Resign(d, ev, 150);
        Check(SaleTermRules.GapDays(d, 140, 170) == 20 && SaleTermRules.GapDays(d, 160, 170) == 10 && SaleTermRules.GapDays(d, 100, 149) == 0,
            "held contract: the gap is the days after the end, up to the day the profit is settled");
        // the save: the mark that a repair was made is kept, and a deal handed over wrongly can be opened again
        var data = new Dictionary<string, string> { { SaleTermStore.Key, SaleTermStore.Write(f) } };
        f.Mended = TermMend.Handed | TermMend.Fees;
        data[SaleTermStore.Key] = SaleTermStore.Write(f);
        TermFile back = SaleTermStore.Load(data, out _);
        Check(back != null && back.Mended == (TermMend.Handed | TermMend.Fees) && back.Deals.Count == 1 && back.Deals[0].State == TermState.Resigned && back.Deals[0].EndDay == 150,
            "held contract: the saved line keeps the mark that the repairs were made, so they are made once");
        var bare = new TermFile { Mended = TermMend.Handed };
        Check(SaleTermStore.Write(bare) != null && SaleTermStore.Load(new Dictionary<string, string> { { SaleTermStore.Key, SaleTermStore.Write(bare) } }, out _).Mended == TermMend.Handed,
            "held contract: a mark alone is saved, so a repair made in a save with no deal is not made again");
        f.Mended = 0;
        Check(SaleTermStore.Load(new Dictionary<string, string> { { SaleTermStore.Key, SaleTermStore.Write(f) } }, out _).Mended == 0, "held contract: a save with no mark reads as not yet repaired");
    }

    private static void SaleTermHeldSourceChecks()
    {
        string deal = ModSource(@"Tech\TechDirector.SaleTerms.cs"), exits = ModSource(@"Tech\TechDirector.Exits.cs"), deals = ModSource(@"Tech\TechDirector.Deals.cs");
        string agreed = ModSource(@"Tech\TechDirector.ExitAgreed.cs");
        // the hand-over: settled, real, believed only when the business is gone, retried each day, and said when it cannot be made
        string hand = MethodText(deal, "private void TermHand(TermDeal d, FirmPlace firm)");
        int remit = hand.IndexOf("TermRemit(d, firm, day)", StringComparison.Ordinal), over = hand.IndexOf("ExitBridges.HandOver(d.Place, d.Buyer)", StringComparison.Ordinal);
        Check(remit > 0 && over > remit && hand.Contains("ExitBridges.StillHeld") && hand.Contains("TellHandWait(d, why, day)") && hand.Contains("SaleTermRules.WhyBoss") && hand.Contains("d.Handed = true"),
            "held contract: the days up to the hand-over are settled first, the hand-over is believed only when the business is no longer the player's, and what stands in the way is named");
        Check(MethodText(deal, "private void TellHandWait(").Contains("day - told.Value < 7") && MethodText(deal, "private void TellHandWait(").Contains("reallife_terms_note_hand_wait"),
            "held contract: a hand-over that waits is told when it first waits, when the cause changes and once a week, not every day");
        Check(deal.Contains("if (d.Kind == TermKind.Boss && !d.Open && !d.Handed)") && MethodText(deal, "private void TermsDay(int day)").Contains("TermHand(d, firm)"),
            "held contract: a contract that has ended is tried for hand-over on every day until it has been made");
        Check(MethodText(deal, "private void TermRemit(").Contains("SaleTermRules.Cover(sum, days, SaleTermRules.GapDays(d, since, day)") && deal.Contains("SaleTermRules.Resign(d, _termEvents, Save.Day)"),
            "held contract: a loss in the days between the end and the hand-over is the buyer's, and a resignation is dated");
        Check(deal.Contains("TermFile TermsKnown()") && MethodText(deal, "internal bool Contracted(string place)").Contains("TermsKnown()") && deal.Contains("SaleTermStore.Load(Save.modData"),
            "held contract: the firm stays held when the venture part is switched off, read from the save");
        Check(deal.Contains("internal int OwnShare(string place)") && MethodText(deal, "internal int OwnShare(string place)").Contains("return 0;"), "held contract: the player owns none of a firm a contract holds");
        // nothing offered, listed, raised or sold for it
        string accept = MethodText(exits, "internal bool ExitAccept(");
        Check(accept.IndexOf("ContractRefused(place", StringComparison.Ordinal) > 0 && accept.IndexOf("ContractRefused(place", StringComparison.Ordinal) < accept.IndexOf("ExitBridges.WhyNotHand", StringComparison.Ordinal)
              && accept.Contains("e.Offers.Clear()"), "held contract: a firm already sold on a contract cannot be sold again, and an offer left standing goes");
        Check(MethodText(agreed, "internal bool ExitAgree(").Contains("ContractRefused(place, null)"), "held contract: a sale cannot be agreed at a desk for a firm a contract holds");
        string loop = MethodText(exits, "private void FirmExitsDay(int day)");
        Check(loop.IndexOf("Contracted(place)", StringComparison.Ordinal) > 0 && loop.IndexOf("Contracted(place)", StringComparison.Ordinal) < loop.IndexOf("ExitRules.Round(", StringComparison.Ordinal)
              && loop.Contains("|| (mrr < ExitCatalogue.OfferMrrLeast") && loop.Contains("Contracted(place) ||"), "held contract: a firm a contract holds takes no offer in any contract state, and no way to the market goes on");
        Check(MethodText(deals, "internal bool StartRaise(").Contains("ContractRefused") && MethodText(deals, "internal bool GrantOptions(").Contains("ContractRefused") && MethodText(deals, "internal void SetChief(").Contains("ContractRefused")
              && MethodText(deals, "internal void PrepareListing(").Contains("ContractRefused") && MethodText(exits, "internal void ListWholeCompany(").Contains("ContractRefused") && exits.Contains("reallife_terms_why_contract_desk"),
            "held contract: a firm a contract holds raises no round, grants no options, names no chief, is asked for no listing and has no desk session");
        // the listing: refused before any fee; a fee kept for nothing is given back
        string bankers = MethodText(exits, "internal bool ExitBankers(");
        Check(bankers.IndexOf("ContractRefused(place", StringComparison.Ordinal) > 0 && bankers.IndexOf("ContractRefused(place", StringComparison.Ordinal) < bankers.IndexOf("VentureGame.Pay(", StringComparison.Ordinal)
              && MethodText(exits, "internal bool ExitRoadshow(").Contains("ContractRefused"), "held contract: the way to the market is refused, with the reason, before any fee is taken");
        string list = MethodText(exits, "private void ListFirm(");
        Check(list.Contains("if (c == null)") && list.Contains("CancelWay(firm, e)") && !list.Contains("e.Stage = ListStage.None;"), "held contract: a listing that cannot happen on the day gives the fee back with a notice and does not end silently");
        Check(MethodText(deal, "private long GiveFeeBack(").Contains("VentureGame.Give(cents, VentureGame.BankersKey") && MethodText(deal, "private long GiveFeeBack(").Contains("e.Fee = 0;")
              && exits.Contains("GiveFeeBack(firm.File.Name, firm.Reg, e)"), "held contract: a fee is given back once, on the retainer's own bank line, and the way is closed");
        string mend = MethodText(deal, "private void MendContracts()");
        Check(mend.Contains("TermMend.Handed") && mend.Contains("TermMend.Fees") && mend.Contains("_terms.Mended |=") && mend.Contains("firm.File.Founded <= d.Day") && mend.Contains("_terms.BossOf(d.Place) == null")
              && mend.Contains("e.PriceDay > d.Day") && exits.Contains("MendContracts();"), "held contract: a save from 1.0.19 is put right once at the load: a contract wrongly handed over is opened again, and retainers for a listing that could not happen are given back");
        Check(deal.Contains("reallife_terms_note_mended") && deal.Contains("reallife_terms_note_fee_back") && deal.Contains("_feeBack = 0"), "held contract: the repairs are told with one notice each, and nothing the player was paid is taken back");
        Check(!mend.Contains("Pay(") && !mend.Contains("PayDeductible("), "held contract: the repair takes no money from the player");
        // the company's listing leaves a held firm out
        string links = ModSource(@"Wiring\ListingLinks.cs");
        Check(links.Contains("private static bool Apart(BuildingRegistration reg)") && Occurs(links, "!Apart(") >= 3 && links.Contains("ServicesBridges.Contracted(ServicesGame.Place(reg))"),
            "held contract: the whole company's listing, hand-over and trade leave out a firm a contract holds");
        // both interfaces: ownership shown, no market offered, the choices explained
        string kitCap = ModSource(@"Ui\Tech\TechLive.Venture.cs"), oldCap = ModSource(@"Tech\Venture\TechApp.Venture.cs"), kitExit = ModSource(@"Ui\Tech\TechLive.Exits.cs"), oldExit = ModSource(@"Tech\Venture\TechApp.Exits.cs");
        Check(kitCap.Contains("reallife_terms_cap_owner") && oldCap.Contains("reallife_terms_cap_owner") && kitCap.Contains("_d.OwnShare(") && oldCap.Contains("_director.OwnShare(")
              && ModSource(@"Ui\Tech\TechLive.FirmPage.cs").Contains("_d.OwnShare(place)"), "held contract: the cap table and the funding rows show the buyer as the owner from the day of the sale, on both interfaces");
        Check(kitExit.Contains("reallife_terms_exit_closed") && oldExit.Contains("reallife_terms_exit_closed") && kitExit.Contains("return;") && oldExit.Contains("if (contract == null)"),
            "held contract: neither interface offers the bankers, the roadshow or the whole company's listing for a firm a contract holds");
        Check(ModSource(@"Tech\Venture\SaleTermText.cs").Contains("reallife_terms_boss_what") && ModSource(@"Tech\Venture\SaleTermText.cs").Contains("reallife_terms_deal_waiting")
              && kitCap.Contains("reallife_vc_chief_help") && oldCap.Contains("reallife_vc_chief_help"), "held contract: the choice of staying as the boss says what it means, and the chief executive's selector says what choosing someone else does, on both interfaces");
        Check(ModSource(@"Tech\TechDirector.Money.cs").Contains("replaced = string.IsNullOrEmpty(c.Ceo)") && MethodText(ModSource(@"Tech\TechDirector.Venture.cs"), "private void ReplaceChief(").Contains("LetGo(was, false)"),
            "held contract: what the chief executive selector does is what the page says: only the board's replacement reads it, and it lets a named professional go");
    }

    private static void SaleTermHeldTextChecks()
    {
        Dictionary<string, string> en = LocaleFile("en");
        bool all = true, plain = true;
        foreach (string k in new[]
        {
            "reallife_terms_why_contract", "reallife_terms_why_contract_desk", "reallife_terms_cap_owner", "reallife_terms_exit_closed", "reallife_terms_boss_what", "reallife_terms_deal_waiting",
            "reallife_terms_note_hand_wait", "reallife_terms_wait_inside", "reallife_terms_wait_vehicle", "reallife_terms_wait_gift", "reallife_terms_wait_busy", "reallife_terms_wait_stock",
            "reallife_terms_wait_off", "reallife_terms_wait_other", "reallife_terms_note_mended", "reallife_terms_note_fee_back", "reallife_exit_note_way_ended", "reallife_exit_note_called_off_owed",
            "reallife_vc_chief_help"
        })
        {
            bool has = en.TryGetValue(k, out string t) && !string.IsNullOrWhiteSpace(t);
            all &= has;
            plain &= has && !t.Contains("!") && !t.Contains("—") && !t.Contains("–") && !t.Contains(" - ") && (!k.Contains("_note_") || t.Length <= 240);
        }
        Check(all && plain, "held contract: every new text is in en, plain, with no dash or exclamation mark, and every notice within 240 characters");
        // every way a hand-over can be stopped has words, and a notice with the longest of them stays inside 240 characters
        bool words = true;
        int longest = 0;
        foreach (string refusal in new[] { "reallife_svc_refuse_inside", "reallife_svc_refuse_vehicle", "reallife_svc_refuse_gift", "reallife_svc_refuse_busy", "reallife_svc_refuse_stock", "reallife_svc_refuse_lot",
            "reallife_svc_refuse_not_business", SaleTermRules.WhyBoss, "reallife_exit_refuse_hand_over" })
        {
            string wait = SaleTermRules.WaitKey(refusal);
            words &= en.TryGetValue(wait, out string w) && w.Length > 0;
            longest = Math.Max(longest, en.TryGetValue(wait, out string w2) ? w2.Length : 0);
        }
        string note = (en.TryGetValue("reallife_terms_note_hand_wait", out string n) ? n : string.Empty).Replace("{firm}", new string('x', 30)).Replace("{buyer}", new string('y', 30)).Replace("{why}", new string('z', longest));
        Check(words && note.Length <= 240, "held contract: every cause that can stop a hand-over has words, and the notice that names it fits 240 characters with long names (" + note.Length + ")");
        Check(SaleTermRules.WaitKey("reallife_svc_refuse_inside") == "reallife_terms_wait_inside" && SaleTermRules.WaitKey(SaleTermRules.WhyBoss) == "reallife_terms_wait_off"
              && SaleTermRules.WaitKey(null) == "reallife_terms_wait_other", "held contract: each refusal maps to its own short words");
        string guide = en.TryGetValue("reallife_guide_tech_startups", out string g) ? g : string.Empty;
        Check(guide.Contains("cap table shows the buyer as the owner") && guide.Contains("so it cannot be listed") && guide.Contains("Nothing else reads who the chief executive is"),
            "held contract: the Guide's startup page says what staying as the boss means and what the chief executive selector does");
    }
}
