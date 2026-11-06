using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;

// The day's result (Economy/Finance/DayRules.cs, DayResult.cs, GameBooks.Result.cs, EconomyDirector.Result.cs): the profit or loss on the day-end panel and the
// top bar is the game's figure with the money of the mod that the game's figure does not hold, less the transfers (a loan's payout, debts consolidated, shares
// bought or sold, deposits). The counting rules run offline on a made-up day; the sources are held to the design (the game's stored figure is never written, the
// labels are written once a day, no per-frame work); the option, the self-check line and the texts are checked. Each check is also run on a made-up wrong case,
// which must fail.
internal static partial class Program
{
    private static string DayScripts()
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
    }

    private static string DaySource(string relative)
    {
        string path = System.IO.Path.Combine(DayScripts(), relative);
        return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
    }

    // A made-up day: (type, amount, held by the game's figure).
    private static DayFigure DayOf(float game, (string type, float amount, bool held)[] lines)
    {
        var tally = new ResultTally();
        for (int i = 0; i < lines.Length; i++)
        {
            tally.Add(lines[i].type, lines[i].amount, lines[i].held);
        }
        return tally.Figure(12, game);
    }

    private static void DayResults()
    {
        const string P = "reallife_transaction_", G = "ba:transaction_";
        var day = new (string type, float amount, bool held)[]
        {
            (G + "loanpayout", 50000f, false),               // a loan's payout: a transfer
            (P + "consolidation_paid_out", 20000f, false),   // a consolidation loan paid out...
            (P + "consolidation_paid_off", -20000f, false),  // ...and the debts it paid off: transfers
            (P + "shares_bought", -3000f, false),            // shares bought and sold: transfers
            (P + "shares_sold", 3500f, false),
            (P + "util_power", -40f, false),                 // home utilities: counted
            (P + "util_water", -15f, false),
            (G + "tuitionfee", -200f, false),                // tuition: counted
            (P + "career_wage", 120f, false),                // a wage of the mod's careers: counted
            (G + "loanpayment", -100f, true),                // the game's own loan payment: the game's figure holds it
            (G + "wage", -80f, true),                        // a business's wage: in its statement
            (P + "shares_commission", -5f, false),           // what a share costs and pays: counted
            (P + "shares_dividend", 12f, false)
        };
        DayFigure figure = DayOf(1000f, day);
        Check(figure.Counted == 6 && figure.Held == 2 && figure.Left == 5 && figure.Total == 13,
            "day result: of 13 transactions 6 are counted, 2 are the game's already and 5 are left out as transfers (" + figure.Counted + ", " + figure.Held + ", " + figure.Left + ")");
        Check(Math.Abs(figure.Extra - -128f) < 0.001f && Math.Abs(figure.Mod - 872f) < 0.001f && Math.Abs(figure.Game - 1000f) < 0.001f,
            "day result: utilities -55, tuition -200, a wage +120, the shares' commission and dividend +7 make -128 on the game's 1000: 872 (" + figure.Mod + ")");
        // made to fail once: a sum of everything, transfers and the game's own lines too, is nowhere near
        float everything = 0f;
        for (int i = 0; i < day.Length; i++)
        {
            everything += day[i].amount;
        }
        Check(Math.Abs(1000f + everything - figure.Mod) > 1000f, "day result: counting every transaction would show " + (1000f + everything) + ", not " + figure.Mod + " (made to fail once)");

        // Each kind of transfer, and what is not one.
        string[] transfers = { G + "loanpayout", G + "loanpayoff", G + "buildingbought", G + "buildingsold", G + "vehiclebought", G + "deposit", G + "depositreturn",
            P + "credit_drawn", P + "finance_advance", P + "private_loan_taken", P + "consolidation_paid_out", P + "consolidation_paid_off", P + "shares_bought", P + "shares_sold",
            P + "shares_bond_bought", P + "shares_option_bought", P + "shares_deposit", P + "shares_withdrawal", P + "shares_reserve_in", P + "util_deposit", P + "social_loan",
            P + "tech_investment_racks", P + "ownbank_investment_capital", P + "bld_land", P + "tech_venture_raise" };
        string[] counted = { G + "tuitionfee", G + "wage", G + "healthinsurance", G + "rent", P + "util_power", P + "util_water", P + "co_accountant", P + "interest", P + "late_fee",
            P + "credit_payment", P + "finance_charge", P + "consolidation_fee", P + "shares_commission", P + "shares_dividend", P + "shares_margin_interest", P + "career_wage",
            P + "tech_hosting", P + "social_repay" };
        string wrongTransfer = null, wrongCounted = null;
        foreach (string t in transfers)
        {
            wrongTransfer = DayRules.IsTransfer(t) ? wrongTransfer : t;
        }
        foreach (string t in counted)
        {
            wrongCounted = DayRules.IsTransfer(t) ? t : wrongCounted;
        }
        Check(wrongTransfer == null, "day result: every payout, consolidation, share, deposit, lending and capital line is a transfer" + (wrongTransfer != null ? " (not: " + wrongTransfer + ")" : string.Empty));
        Check(wrongCounted == null, "day result: bills, wages, tuition, interest, fees, commissions, dividends and payments on what is owed are not transfers" + (wrongCounted != null ? " (wrongly: " + wrongCounted + ")" : string.Empty));
        Check(!DayRules.IsTransfer(null) && DayRules.Of(P + "shares_bought", true) == DayPart.Transfer && DayRules.Of(P + "util_power", true) == DayPart.Held && DayRules.Of(P + "util_power", false) == DayPart.Counted,
            "day result: a transfer is left out even when the game holds it; a held line is never counted; a null type is no transfer");

        // The audit: every key of EconoView's Investments and Loans categories is sorted; a new one is found (made to fail once).
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var key = new Regex("\"(reallife_transaction_[a-z0-9_]+)\"");
        foreach (string path in Directory.EnumerateFiles(DayScripts(), "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match m in key.Matches(File.ReadAllText(path)))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        string[] codes;
        Dictionary<string, string>[] langs = LanguageWords(out codes);
        if (!LanguagesRead("day result", langs, codes))
        {
            return;
        }
        foreach (string k in langs[0].Keys)
        {
            if (k.StartsWith("reallife_transaction_", StringComparison.Ordinal) && !k.EndsWith("_label", StringComparison.Ordinal))
            {
                keys.Add(k);
            }
        }
        string audit = DayRules.Audit(keys);
        Check(audit == null, "day result: every key of EconoView's Investments and Loans categories is a transfer or counts, and none is both (" + keys.Count + " keys)" + (audit != null ? ": " + audit : string.Empty));
        var more = new List<string>(keys) { P + "loan_new_kind" };
        string wrongAudit = DayRules.Audit(more);
        Check(wrongAudit != null && wrongAudit.Contains("loan_new_kind"), "day result: a new loan or share key in neither list is found (made to fail once)");

        // The self-check line.
        string pass = DayRules.Verdict(figure, null);
        string fail = DayRules.Verdict(figure, "the top bar does not show it");
        Check(pass == "Day result check (day 12): the game's figure 1000, with the mod's money 872 (6 transaction(s) counted, 5 left out as transfers): PASS", "day result: the self-check line reads as asked: " + pass);
        Check(fail.EndsWith("FAIL (the top bar does not show it)", StringComparison.Ordinal) && fail.Contains("left out as transfers): FAIL"), "day result: the self-check line fails, naming the reason (made to fail once)");
        Check(DayRules.Problem(1000f, 1000f, figure) == null && DayRules.Problem(1000f, 900f, figure) != null && DayRules.Problem(float.NaN, 0f, figure) != null,
            "day result: the game's figure must be the sum of its parts, as the refresh leaves it (made to fail once)");

        // The design, from the sources: the game's stored figure is read and never written; nothing runs every frame.
        string result = DaySource(@"Economy\GameBooks.Result.cs");
        string view = DaySource(@"Economy\Finance\DayResult.cs");
        string director = DaySource(@"Economy\Banking\EconomyDirector.Result.cs");
        var writes = new Regex(@"totalProfit\s*=[^=]|UpdateIncomeStatementTotalProfit|financialSummaries\.(Add|Remove|Insert|Clear)");
        Check(result.Length > 0 && view.Length > 0 && director.Length > 0 && !writes.IsMatch(result) && !writes.IsMatch(view) && !writes.IsMatch(director),
            "day result: the game's stored figure (FinancialSummary.totalProfit) is never written, so the bank, the tax and the rivals' ranking read what they always did");
        Check(!Regex.IsMatch(view + director, @"void (Update|LateUpdate|FixedUpdate)\b") && director.Contains("new WaitForSecondsRealtime(ResultStep)") && director.Contains("ResultFrames = 6"),
            "day result: no Update; the labels are written once a day, after a few looks at the panel 20 times a second and six frames");
        Check(result.Contains("List<Transaction> ofDay = Day(day);") && result.Contains("tally.Add(t.transactionType, t.amount, Holds(t));") && Regex.Matches(result, @"foreach|for \(int").Count == 2,
            "day result: the day's transactions are read once, from the day's cut Refresh uses");
        // Holds mirrors Refresh: each test the refresh counts a transaction into the statements by.
        foreach (string piece in new[] { "type == UnassignedWageType", "categories.Contains(GameLoans.ExpenseCategory)", "categories.Contains(HealthInsurance)", "categories.Contains(Headhunter)",
            "categories.Contains(Parking)", "categories.Contains(SalaryIncome)", "categories.Contains(Rent)", "type == CityGame.RentKey", "Statements.Contains(t.address)",
            "categories.Contains(Salaries)", "type == RevenueType", "BizLines.Of(type)", "EconomyBridges.Takings", "EconomyBridges.CostRow", "CashCategory.Stock" })
        {
            Check(result.Contains(piece), "day result: the walk holds back what the game's figure counts already: " + piece);
        }
        Check(view.Contains("label.color = amount < 0f ? colors.lightRed : colors.lime;") && view.Contains("amount.ToShortCurrencyFormat(abbreviated: true)") && view.Contains("\"NegativeArrow\"")
              && view.Contains("panel.panel.alpha >= 1f"),
            "day result: the labels take the game's colours (red below nothing, lime from it), its short money format, the top bar's arrow, and wait for the panel's fade to end");
        string day2 = DaySource(@"Economy\Banking\EconomyDirector.Day.cs");
        string main = DaySource(@"Economy\Banking\EconomyDirector.cs");
        Check(day2.IndexOf("LogBizman();", StringComparison.Ordinal) > 0 && day2.IndexOf("ShowDayResult(Save.Day - 1);", StringComparison.Ordinal) > day2.IndexOf("LogBizman();", StringComparison.Ordinal)
              && main.Contains("ShowDayResultAtStart();") && main.Contains("KeepDayResult();") && director.Contains("if (Settings.EconomyDayResult && Save != null)"),
            "day result: it is shown after the day's statement is brought up to date, once after a load, and stands down when the option is off");

        // The option: on by default, off leaves the game's figure, in the options page and its feature and tree.
        string settings = DaySource(@"Core\Settings.cs");
        Check(settings.Contains("public static bool EconomyDayResult = true;") && settings.Contains("EconomyDayResult = Bool(modId, EconomyDayResultId, EconomyDayResult);")
              && DaySource(@"Core\RealLifeOverhaulMod.cs").Contains("AddToggle(Settings.EconomyDayResultId, \"reallife_econ_opt_day_result\"")
              && DaySource(@"Meta\FeatureList.Economy.cs").Contains("Settings.EconomyDayResultId") && DaySource(@"Meta\OptionsTree.cs").Contains(".Opt(\"economy_day_result\", \"economy\")"),
            "day result: the option is on by default, in the options page, its feature and the options tree");

        // The texts, in every language the per-feature checks hold: the option's name and tip, and What's new's line (any row).
        bool texts = true;
        string badText = null;
        for (int l = 0; l < codes.Length; l++)
        {
            Dictionary<string, string> w = langs[l];
            string label = null, tip = null;
            bool own = w.TryGetValue("reallife_econ_opt_day_result", out label) && w.TryGetValue("reallife_opt_tip_economy_day_result", out tip) && label.Length > 0 && tip.Length > 0
                       && (l == 0 || (label != langs[0]["reallife_econ_opt_day_result"] && tip != langs[0]["reallife_opt_tip_economy_day_result"]))
                       && !Regex.IsMatch(label + tip, @"[–—]| - |!");
            bool news = WhatsNewHas(w, "EconoView") && (l == 0 || !WhatsNewHas(w, "profit or loss"));
            bool guide = label != null && w["reallife_guide_economy"].Contains("<b>" + label + "</b>");
            if (!own || !news || !guide)
            {
                texts = false;
                badText = codes[l] + (own ? string.Empty : " option") + (news ? string.Empty : " what's new") + (guide ? string.Empty : " guide");
                break;
            }
        }
        Check(texts, "day result: the option's name and tip, What's new's line and the Guide's section are in every language, each its own, with no dash and no exclamation mark" + (badText != null ? " (" + badText + ")" : string.Empty));
        Check(langs[0]["reallife_econ_opt_day_result"] == "Day-end result includes the mod's money" && langs[0]["reallife_guide_economy"].Contains("# The day's result"),
            "day result: the option is called as asked, and the Finance page of the Guide has the day's result section");
    }
}
