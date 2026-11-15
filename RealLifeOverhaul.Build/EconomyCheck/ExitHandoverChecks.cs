using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Ui;

// A player with an offer for their firm can accept it on both interfaces: the Exits button reaches the offers (not Go to market), the old Tech app has the page too, a refused
// handover names its one reason, the desk session never claims a sale it cannot make, and a founder's loan that cannot be had says why. Game-free; the sources are read for what
// only the game can do.
internal static partial class Program
{
    private static void ExitHandoverChecks()
    {
        ExitPageIdChecks();
        ExitRefusalChecks();
        ExitOldPageChecks();
        ExitLoanChecks();
    }

    private static void ExitPageIdChecks()
    {
        int[] pages = { TechView.PageCap, TechView.PageRaise, TechView.PageBooks, TechView.PageGtm, TechView.PageExits };
        Check(new HashSet<int>(pages).Count == pages.Length, "exit hand-over: every page of a firm under Funding has a number of its own (Exits and Go to market once shared 3)");
        var view = new TechView();
        view.Land(5, "exits:Main~1");
        Check(view.Pick == "cap:Main~1" && view.Page == TechView.PageExits && view.Page != TechView.PageGtm, "exit hand-over: a notice of an offer lands on the page of exits, not on Go to market");
        string venture = ModSource(@"Ui\Tech\TechLive.Venture.cs");
        Check(Regex.IsMatch(venture, @"Button\(TechText\.T\(""reallife_exit_button_page""\)[^\r\n]*Page = PageExits") && Regex.IsMatch(venture, @"Button\(TechText\.T\(""reallife_gtm_button_open""\)[^\r\n]*Page = PageGtm"),
            "exit hand-over: the Exits button sets the exits page and Go to market sets its own");
        Check(!Regex.IsMatch(ModSource(@"Ui\Tech\TechLive.Exits.cs") + ModSource(@"Ui\Tech\TechLive.Found.cs") + ModSource(@"Ui\Tech\TechLive.Money.cs"), @"private const int Page\w+ = \d"),
            "exit hand-over: the kit's page numbers come from the one list (TechView), none written again beside it");
    }

    private static void ExitRefusalChecks()
    {
        ExitBridges.Reset();
        Check(ExitBridges.WhyNotHand("Main~1") == null, "exit hand-over: with nothing in the way no reason is given");
        ExitBridges.HandOverRefusal = place => "reallife_svc_refuse_inside";
        Check(ExitBridges.WhyNotHand("Main~1") == "reallife_svc_refuse_inside", "exit hand-over: the reason the link gives is the reason told");
        ExitBridges.HandOverRefusal = place => throw new InvalidOperationException();
        Check(ExitBridges.WhyNotHand("Main~1") == ExitBridges.GenericRefusal, "exit hand-over: a link that throws refuses with the plain words, never lets the sale through");
        ExitBridges.Reset();

        string links = ModSource(@"Wiring\Wiring.ExitLinks.cs"), sale = ModSource(@"Economy\Services\ServicesGame.Sale.cs"), exits = ModSource(@"Tech\TechDirector.Exits.cs");
        Match names = Regex.Match(sale, @"enum SaleBlock \{([^}]*)\}");
        var missing = new List<string>();
        foreach (string raw in names.Groups[1].Value.Split(','))
        {
            string name = raw.Trim();
            if (name.Length > 0 && name != "None" && name != "NotABusiness" && !links.Contains("case SaleBlock." + name + ":"))
            {
                missing.Add(name);
            }
        }
        Check(names.Success && missing.Count == 0, "exit hand-over: every thing that stops a sale has words of its own, none falls to the plain ones (" + string.Join(",", missing) + ")");
        var keys = new HashSet<string>();
        foreach (Match m in Regex.Matches(links, @"return ""(reallife_(?:svc|exit)_refuse_\w+)"";"))
        {
            keys.Add(m.Groups[1].Value);
        }
        int bad = 0;
        foreach (string key in keys)
        {
            string text = LocaleText("en", key);
            bad += string.IsNullOrEmpty(text) || text.Length > 240 ? 1 : 0;
        }
        Check(keys.Count >= 6 && bad == 0, "exit hand-over: every refusal key is a text of at most 240 characters (" + keys.Count + " keys)");
        Check(ExitBridges.GenericRefusal == "reallife_exit_refuse_hand_over" && !string.IsNullOrEmpty(LocaleText("en", ExitBridges.GenericRefusal)), "exit hand-over: the plain refusal has its text");

        int accept = exits.IndexOf("internal bool ExitAccept(", StringComparison.Ordinal);
        string body = accept >= 0 ? exits.Substring(accept, Math.Min(4200, exits.Length - accept)) : string.Empty;
        Check(body.Contains("ExitBridges.WhyNotHand(place)") && body.Contains("Notifications.ShowError(why)") && body.IndexOf("WhyNotHand", StringComparison.Ordinal) < body.IndexOf("ExitBridges.HandOver ==", StringComparison.Ordinal),
            "exit hand-over: a sale that cannot be made names its reason before any money or staff moves");

        int session = exits.IndexOf("internal bool ExitSession(", StringComparison.Ordinal);
        string held = session >= 0 ? exits.Substring(session) : string.Empty;
        int why = held.IndexOf("WhyNotHand(place)", StringComparison.Ordinal), call = held.IndexOf("ExitAccept(place, o.Id)", StringComparison.Ordinal);
        Check(why > 0 && call > why && held.Contains("\"reallife_tech_desk_exit_why_inside\""),
            "exit hand-over: the desk session sits inside the firm, so it says to leave and accept in the Tech app before trying, and does not show a second refusal");
        string inside = LocaleText("en", "reallife_tech_desk_exit_why_inside");
        Check(!string.IsNullOrEmpty(inside) && inside.Contains("Tech app") && inside.Length <= 240, "exit hand-over: the session's words name where to accept the offer");
        Check(ModSource(@"Wiring\Wiring.ExitLinks.cs").Contains("ExitBridges.HandOverRefusal = HandOverRefusal;"), "exit hand-over: the link is connected with the other exit links");
    }

    private static void ExitOldPageChecks()
    {
        string app = ModSource(@"Tech\Venture\TechApp.Venture.cs"), page = ModSource(@"Tech\Venture\TechApp.Exits.cs");
        Check(app.Contains("Picked(_picked, TechDirector.ExitRow)") && app.Contains("WriteExits(box, picked)") && app.Contains("\"reallife_exit_button_page\""),
            "exit hand-over: the old Tech app lands a notice of an offer on the firm's page of exits and the firm's page has the Exits button");
        Check(page.Contains("_director.ExitAccept(place, id)") && page.Contains("_director.ExitDecline(place, id)") && page.Contains("_director.ExitCounter(place, offer, at)")
              && page.Contains("_director.ExitBankers(place, at)") && page.Contains("_director.ExitRoadshow(place, at)") && page.Contains("_director.ExitSell(place)"),
            "exit hand-over: the old page accepts, declines and counters an offer, sells shares and goes to market as the kit's does");
        Check(page.Contains("GameConfirm.Show(title, body"), "exit hand-over: accepting asks the game's own question first, as on the kit");
        var missing = new List<string>();
        var seen = new HashSet<string>();
        foreach (Match m in Regex.Matches(page, @"""(reallife_\w+)"""))
        {
            string key = m.Groups[1].Value;
            if (seen.Add(key) && string.IsNullOrEmpty(LocaleText("en", key)))
            {
                missing.Add(key);
            }
        }
        Check(seen.Count > 20 && missing.Count == 0, "exit hand-over: every text of the old page exists (" + string.Join(",", missing) + ")");
        Check(!Regex.IsMatch(page, @"void (Update|LateUpdate|FixedUpdate)\("), "exit hand-over: the old page runs nothing of its own in a frame");
    }

    private static void ExitLoanChecks()
    {
        var none = new Cap();
        Check(StartupMoney.LoanRefusal(null) == "reallife_vc_acct_none" && StartupMoney.LoanRefusal(none) == "reallife_vc_acct_none", "founder's loan: a firm with no account is told it has none");
        var c = new Cap();
        c.Acc.Open = true;
        Check(StartupMoney.LoanRefusal(c) == "reallife_vc_loan_no_raise" && StartupMoney.Lend(c, 10) == 0, "founder's loan: an account that raised nothing is told so and lends nothing");
        c.Acc.Raised = 1000000;
        c.Acc.Out = 1000000;
        Check(StartupMoney.LoanRefusal(c) == "reallife_vc_loan_no_cash" && StartupMoney.Lend(c, 10) == 0, "founder's loan: an emptied account is told so and lends nothing");
        c.Acc.Out = 0;
        Check(StartupMoney.LoanRefusal(c) == null, "founder's loan: an account with investors' money in it has no reason against");
        long lent = StartupMoney.Lend(c, 10);
        Check(lent > 0 && lent == StartupMoney.LoanMax(c) && StartupMoney.LoanRefusal(c) == "reallife_vc_acct_loan_used" && StartupMoney.Lend(c, 11) == 0, "founder's loan: taken once, then refused as used (" + lent + ")");
        Check(ModSource(@"Tech\TechDirector.Money.cs").Contains("StartupMoney.LoanRefusal(c)"), "founder's loan: a press that lends nothing says why");
        foreach (string key in new[] { "reallife_vc_loan_no_raise", "reallife_vc_loan_no_cash", "reallife_vc_acct_none", "reallife_vc_acct_loan_used" })
        {
            string text = LocaleText("en", key);
            Check(!string.IsNullOrEmpty(text) && text.Length <= 240, "founder's loan: " + key + " is a text of at most 240 characters");
        }
    }
}
