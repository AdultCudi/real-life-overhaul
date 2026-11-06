using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Tech app's pages of exits: a firm's offers, its shares sold later and the way to the market at each step, and a city startup's founder and office. Names here are
// placeholders and never ship.
internal static partial class UiSamples
{
    // A method and no field: the order static fields of a partial class start in is not defined.
    private static string[] TechExitStates() => new[] { "exits", "exits_market", "exits_listed", "exits_hire", "startup_place" };

    private static void TechExitsHead(UiWords t, TechModel m, string back)
    {
        m.Shape = TechShape.Sheet;
        m.Nav = TechTabNav(t, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 5;
        m.Back = TW(t, back, "Back to the firm");
        m.Head = new UiHeadModel { Name = WF(t, "reallife_exit_title", "Exits: Pioneer Game Studio", "firm", "Pioneer Game Studio"), Icon = UiIcon.Handshake, Role = "Game studio" };
    }

    private static UiFactCardModel TechExitShares(UiWords t, TechRunList runs, bool room)
    {
        var shares = new TechCard(runs.Runs, TW(t, "exit_shares", "Your shares"));
        if (room)
        {
            shares.Say(WF(t, "reallife_exit_shares_room", "You may sell 120,000 shares for $540,000, 19% under the round's price. The money is yours, not the firm's.", "shares", "120,000", "amount", "$540,000", "discount", "19%"))
                .Button(W(t, "reallife_exit_button_sell", "Sell my shares"), UiButtonKind.Secondary, UiAction.Count, () => true);
        }
        else
        {
            shares.Say(WF(t, "reallife_exit_shares_locked", "Your shares are locked up for 143 more days.", "days", "143"), UiRole.WarnText);
        }
        return shares.Build();
    }

    private static void TechExits(UiWords t, TechModel m, TechRunList runs, string state)
    {
        TechExitsHead(t, m, "back_cap");
        var offers = new TechCard(runs.Runs, TW(t, "exit_offers", "Offers"));
        if (state == "exits_hire")
        {
            offers.Say(WF(t, "reallife_exit_hire_line", "Northgate Systems offers $90,000 for the team of Pioneer Game Studio. It stands to day 431.", "buyer", "Northgate Systems", "amount", "$90,000", "firm", "Pioneer Game Studio", "day", "431"), UiRole.OkText)
                .Say(WF(t, "reallife_exit_offer_after", "Sold at that, the investors are paid $120,000 first and you keep $0.", "investors", "$120,000", "you", "$0"))
                .Button(W(t, "reallife_exit_button_accept", "Accept the offer"), UiButtonKind.Primary, UiAction.Count, () => true)
                .Button(W(t, "reallife_exit_button_decline", "Decline"), UiButtonKind.Secondary, UiAction.Count, () => true);
        }
        else if (state == "exits")
        {
            offers.Say(WF(t, "reallife_exit_offer_line", "Codeharbour Software offers $4,200,000 for Pioneer Game Studio. It stands to day 431.", "buyer", "Codeharbour Software", "amount", "$4,200,000", "firm", "Pioneer Game Studio", "day", "431"), UiRole.OkText)
                .Say(WF(t, "reallife_exit_offer_after", "Sold at that, the investors are paid $1,950,000 first and you keep $2,250,000.", "investors", "$1,950,000", "you", "$2,250,000"))
                .Button(W(t, "reallife_exit_button_accept", "Accept the offer"), UiButtonKind.Primary, UiAction.Count, () => true)
                .Button(W(t, "reallife_exit_button_decline", "Decline"), UiButtonKind.Secondary, UiAction.Count, () => true)
                .Step(TW(t, "exit_counter", "Counter by"), W(t, "reallife_exit_counter_1", "A fifth more"), null, true, true, () => true, () => true)
                .Button(W(t, "reallife_exit_button_counter", "Counter"), UiButtonKind.Secondary, UiAction.Count, () => true);
        }
        else
        {
            offers.Say(WF(t, "reallife_exit_offer_none", "Nobody has made an offer for Pioneer Game Studio. Offers come to a firm that takes $10,000 a month or more, more often as it grows.", "firm", "Pioneer Game Studio", "least", "$10,000"));
        }
        m.Left = new[] { offers.Build(), TechExitShares(t, runs, state == "exits") };
        var market = new TechCard(runs.Runs, TW(t, "exit_market", "The way to the market"));
        if (state == "exits")
        {
            market.Say(W(t, "reallife_exit_market_start", "Bankers take the firm to the market. Choose one: the largest reaches furthest and keeps most of what the offer raises."))
                .Step(TW(t, "exit_bank", "Bank"), WF(t, "reallife_exit_bank_line", "Harbour City Bancorp: retainer $210,000", "bank", "Harbour City Bancorp", "fee", "$210,000"), null, true, true, () => true, () => true)
                .Button(W(t, "reallife_exit_button_bankers", "Engage the bankers"), UiButtonKind.Secondary, UiAction.Count, () => true);
        }
        else if (state == "exits_market")
        {
            market.Say(WF(t, "reallife_exit_market_bankers", "Harbour City Bancorp is engaged ($210,000 paid). Next is the roadshow, at your desk or here.", "bank", "Harbour City Bancorp", "fee", "$210,000"))
                .Step(TW(t, "exit_road", "Roadshow"), W(t, "reallife_exit_road_1", "Deep: fewer, better investors"), null, true, true, () => true, () => true)
                .Button(W(t, "reallife_exit_button_road", "Hold the roadshow"), UiButtonKind.Secondary, UiAction.Count, () => true)
                .Button(W(t, "reallife_exit_button_withdraw", "Withdraw the offer"), UiButtonKind.Secondary, UiAction.Count, () => true);
        }
        else if (state == "exits_listed")
        {
            market.Say(WF(t, "reallife_exit_market_listed", "Pioneer Game Studio is listed at $42,000,000 before the money. $10,500,000 was raised, $9,900,000 reached the firm after Harbour City Bancorp.", "firm", "Pioneer Game Studio", "price", "$42,000,000", "gross", "$10,500,000", "net", "$9,900,000", "bank", "Harbour City Bancorp"), UiRole.OkText)
                .Say(WF(t, "reallife_exit_shares_locked", "Your shares are locked up for 143 more days.", "days", "143"), UiRole.WarnText);
        }
        else
        {
            market.Say(WF(t, "reallife_exit_market_called", "The book for Pioneer Game Studio was called off on day 402: the price fell under the least you take.", "firm", "Pioneer Game Studio", "day", "402"), UiRole.WarnText)
                .Say(WF(t, "reallife_exit_market_not_ready", "A listing asks for takings of $400,000 a month. Pioneer Game Studio takes $120,000.", "least", "$400,000", "now", "$120,000", "firm", "Pioneer Game Studio"));
        }
        market.Button(W(t, "reallife_exit_button_whole", "The whole company on the stock market"), UiButtonKind.Secondary, UiAction.Count, () => true);
        m.Right = new[] { market.Build() };
    }

    private static void TechStartupPlace(UiWords t, TechModel m, TechRunList runs)
    {
        TechStartup(t, m, runs);
        var place = new TechCard(runs.Runs, TW(t, "exit_place", "Founder and office"));
        place.Say(WF(t, "reallife_exit_founder_city", "Sara Bell, 34, lives in the city. Skill 62 out of 100.", "founder", "Sara Bell", "age", "34", "skill", "62"))
            .Say(WF(t, "reallife_exit_team", "9 people on the team.", "count", "9"))
            .Say(WF(t, "reallife_exit_office", "Office: floor 6 of Harbourgate Tower, 12 Main Street.", "floor", "6", "tower", "Harbourgate Tower", "address", "12 Main Street"))
            .Button(W(t, "common_setdestination", "Set destination"), UiButtonKind.Secondary, UiAction.Count, () => true);
        m.Right = new[] { place.Build() };
    }
}
