using System;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Tech;

// The older "sell a tenth of my shares" button is gone: a player's own shares are sold by one set of rules (ExitRules.Room and Sell, TechDirector.ExitSell), vested shares
// only, once a round's window, up to its limit. Game-free rules driven directly; the game's side is read from the sources.
internal static partial class Program
{
    private const int TenthYear = 360;

    private static void SellTenthChecks()
    {
        SellTenthRules();
        SellTenthWiring();
    }

    private static void SellTenthRules()
    {
        Cap c = FundedCap();
        var file = new ExitFile { Seed = 3 };
        FirmExit e = file.Exit("Main~1", true);
        long mine = ExitRules.PlayerShares(c);
        int opened = c.Acc.FounderDay;
        e.RoundDay = opened;
        int soon = opened + 5;
        Check(VentureRules.Vested(opened, soon, TenthYear) == 0 && ExitRules.Room(c, e, soon, TenthYear) == 0 && ExitRules.Sell(c, e, 1, 100, 0, soon, TenthYear) == 0 && ExitRules.PlayerShares(c) == mine,
            "sell a tenth: in the round's window, before the cliff, no unvested share can be sold and none leaves the player");
        int day = opened + TenthYear * 5 / 4;
        e.RoundDay = day;
        long room = ExitRules.Room(c, e, day, TenthYear);
        long most = mine * VentureRules.Vested(opened, day, TenthYear) / 1000 * ExitCatalogue.SecondaryShare / 1000;
        Check(room > 0 && room == most && room * 10 < mine && ExitRules.Sell(c, e, room + 1, 100, 0, day, TenthYear) == 0 && ExitRules.PlayerShares(c) == mine,
            "sell a tenth: a quarter vested, the room is a fifth of the vested part, never a tenth of everything, and one share over it is refused (" + room + " of " + mine + ")");
        int discount = ExitRules.Discount(0, 0, false);
        long paid = ExitRules.Sell(c, e, room, discount, 4, day, TenthYear);
        Check(paid > 0 && paid == ExitRules.SharePrice(c, room, discount) && ExitRules.PlayerShares(c) == mine - room && discount >= ExitCatalogue.DiscountLeast,
            "sell a tenth: the price is the round's price less the discount the rules give (" + paid + ")");
        Check(ExitRules.Room(c, e, day, TenthYear) == 0 && ExitRules.Sell(c, e, 1, discount, 4, day, TenthYear) == 0 && ExitRules.Sell(c, e, 1, discount, 4, day + 1, TenthYear) == 0,
            "sell a tenth: pressing again on the day, or the day after, sells nothing more");
        ExitFile back = ExitStore.Read(ExitStore.Write(file), out _);
        FirmExit again = back.Exit("Main~1", false);
        Check(again != null && again.Sold == room && ExitRules.Room(c, again, day, TenthYear) == 0 && ExitRules.Sell(c, again, 1, discount, 4, day, TenthYear) == 0,
            "sell a tenth: a save and reload between presses keeps what was sold, so reloading does not give the room back");
        Check(ExitRules.Room(c, e, day + ExitCatalogue.SecondaryDays + 1, TenthYear) == 0, "sell a tenth: outside a round's window nothing can be sold");
        e.StageSeen = c.Stage;
        c.Stage++;
        ExitRules.Round(c, e, day + 40);
        Check(e.Sold == 0 && ExitRules.Room(c, e, day + 40, TenthYear) > 0, "sell a tenth: the next round opens a new window with its own limit");
        Cap old = FundedCap();
        long before = ExitRules.PlayerShares(old);
        old.Holders.Find(h => h.Kind == (int)HolderKind.Player).Shares = before / 20;
        FirmExit oe = new FirmExit { Place = "Main~1", RoundDay = old.Acc.FounderDay + 5 };
        Check(ExitRules.Room(old, oe, old.Acc.FounderDay + 5, TenthYear) == 0 && ExitRules.PlayerShares(old) == before / 20,
            "sell a tenth: a player who sold unvested shares before keeps what they got, nothing is taken back, and has nothing more to sell until it vests");
    }

    private static void SellTenthWiring()
    {
        string deals = ModSource(@"Tech\TechDirector.Deals.cs"), exits = ModSource(@"Tech\TechDirector.Exits.cs"), caps = ModSource(@"Tech\Venture\VentureRules.Caps.cs");
        string live = ModSource(@"Ui\Tech\TechLive.Venture.cs"), app = ModSource(@"Tech\Venture\TechApp.Venture.cs");
        Check(!Regex.IsMatch(deals, @"\bSellShares\b") && !Regex.IsMatch(caps, @"\bSellShares\b") && !Regex.IsMatch(live + app, @"\bSellShares\b"),
            "sell a tenth: the second way to sell a player's own shares is gone from the director, the rules and both pages");
        Check(live.Contains("_d.OpenExits(place)") && app.Contains("_director.OpenExits(place)") && live.Contains("reallife_exit_button_sell") && app.Contains("reallife_exit_button_sell"),
            "sell a tenth: the button on the firm's page opens the exits page, where the one sale is");
        Check(Regex.Matches(exits, @"ExitGains\.ForShares\(").Count == 1 && !deals.Contains("ExitGains.ForShares"), "sell a tenth: a share sale reaches the tax return from the one path that is left, once");
        Check(exits.Contains("ExitRules.Sell(c, e, room") && exits.Contains("ExitRules.Locked(e, day)"), "sell a tenth: the one path keeps the lock-up and the room");
        Check(!Regex.IsMatch(ModSource(@"Tech\Venture\ExitPlaces.cs") + ModSource(@"Tech\TechDirector.Rivals.cs") + ModSource(@"Tech\Venture\RivalRules.cs"), @"ExitSell|ExitRules\.Sell\("),
            "NPC: the city's startups and rivals never sell shares through the player's path");
    }
}
