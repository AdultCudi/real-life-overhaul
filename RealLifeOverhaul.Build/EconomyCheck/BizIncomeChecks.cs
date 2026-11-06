using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Economy;

// BizMan's income (Economy/BizBooks.cs, BizLines.cs, GameBooks.Refresh): what BizMan shows of a business is the game's daily statement, built from its own
// tills; the mod's lines reach it by address and key. Every money line of the mod goes through the one call BizBooks.Book, every key of the businesses the
// mod runs is in one list of BizLines (takings, a running cost, capital), the day's refresh counts a takings key and a cost key once, and the day's self-check
// line names a business that does not count. Each check here is also run on a made-up wrong case, which must fail.
internal static partial class Program
{
    private static string BizScripts()
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
    }

    /// <summary>The files of the mod that book money past the one call, with the line: a call of the game's, outside Economy/BizBooks.cs, that is no comment.</summary>
    private static List<string> BizBypasses(IEnumerable<KeyValuePair<string, string>> sources)
    {
        var found = new List<string>();
        foreach (KeyValuePair<string, string> file in sources)
        {
            if (file.Key.Replace('\\', '/').EndsWith("Economy/BizBooks.cs", StringComparison.Ordinal))
            {
                continue;
            }
            string[] lines = file.Value.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }
                if (lines[i].Contains("GameManager.ChangeMoneySafe(") || lines[i].Contains("GameManager.ChangeMoney("))
                {
                    found.Add(file.Key + ":" + (i + 1));
                }
            }
        }
        return found;
    }

    private static void BizIncome()
    {
        string scripts = BizScripts();
        var sources = new List<KeyValuePair<string, string>>();
        foreach (string path in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            sources.Add(new KeyValuePair<string, string>(path, File.ReadAllText(path)));
        }
        Check(sources.Count > 400, "biz income: the mod's sources were read (" + sources.Count + " files)");

        // Every takings or cost call goes through the one helper: the game's ChangeMoneySafe is named only in BizBooks.
        List<string> bypass = BizBypasses(sources);
        Check(bypass.Count == 0, "biz income: every money line of the mod goes through BizBooks.Book" + (bypass.Count > 0 ? " (outside it: " + string.Join(", ", bypass) + ")" : string.Empty));
        string book = File.ReadAllText(System.IO.Path.Combine(scripts, "Economy", "BizBooks.cs"));
        Check(book.Contains("return GameManager.ChangeMoneySafe(amount, transactionInfo, dayOfTransaction, address, force, showNotification);")
              && System.Text.RegularExpressions.Regex.Matches(book, @"GameManager\.ChangeMoneySafe\(").Count == 1,
            "biz income: BizBooks.Book books the money once, by the game's call, with every argument it was given");
        // made to fail: a file that books money past the helper is found
        var wrong = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Tech/Wrong.cs", "x = 1;\n    GameManager.ChangeMoneySafe(5f, info, null, null, true);\n") };
        Check(BizBypasses(wrong).Count == 1 && BizBypasses(new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Tech/Right.cs", "// GameManager.ChangeMoneySafe(\n") }).Count == 0,
            "biz income: the bypass scan finds a call past the helper (made to fail once) and ignores a comment");

        // Every key of the features the table keeps is in a list; none in two.
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var key = new Regex("\"(reallife_transaction_[a-z0-9_]+)\"");
        foreach (KeyValuePair<string, string> file in sources)
        {
            foreach (Match m in key.Matches(file.Value))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        string audit = BizLines.Audit(keys);
        Check(audit == null, "biz income: every key of the businesses the mod runs is in one list of BizLines" + (audit != null ? " (" + audit + ")" : string.Empty));
        Check(BizLines.Audit(new[] { "reallife_transaction_tech_a_new_key" }) != null && BizLines.Audit(new[] { "reallife_transaction_co_raise" }) == null,
            "biz income: a new key of the tech sector in no list is found (made to fail once), another feature's is left to its own accounting");

        // The table: the takings, the costs and the capital each way.
        Check(BizLines.Of("reallife_transaction_tech_takings") == BizLine.Takings && BizLines.Of("reallife_transaction_tech_cloud_takings") == BizLine.Takings
              && BizLines.Of("reallife_transaction_broadband_takings") == BizLine.Takings && BizLines.Of("reallife_transaction_ownbank_takings") == BizLine.Takings
              && BizLines.Of("reallife_transaction_study_academy") == BizLine.Takings && BizLines.Of("reallife_transaction_bld_contract") == BizLine.Takings,
            "biz income: a tech firm's, a data centre's, the broadband company's, the bank's, an academy's and a builder's takings are takings");
        Check(BizLines.Of("reallife_transaction_tech_upkeep") == BizLine.Cost && BizLines.Of("reallife_transaction_tech_it_services") == BizLine.Cost
              && BizLines.Of("reallife_transaction_tech_recruitment_ad") == BizLine.Cost && BizLines.Of("reallife_transaction_bld_crews") == BizLine.Cost
              && BizLines.Of("reallife_transaction_fire_repair") == BizLine.Cost && BizLines.Of("reallife_transaction_holdup_till") == BizLine.Cost,
            "biz income: the tech firms' upkeep, services and recruitment, the builder's crews, a fire's repair and a hold-up are running costs");
        Check(BizLines.Of("reallife_transaction_tech_investment_racks") == BizLine.Capital && BizLines.Of("reallife_transaction_tech_venture_investment") == BizLine.Capital
              && BizLines.Of("reallife_transaction_bld_land") == BizLine.Capital && BizLines.Of("reallife_transaction_bld_sales") == BizLine.Capital
              && BizLines.Of("reallife_transaction_co_raise") == BizLine.Foreign && BizLines.Of(null) == BizLine.Foreign,
            "biz income: racks, startup money, land and a property's sale are capital (not income, so not taxed twice); another feature's key is its own");
        Check(BizLines.RunningRow == "reallife_books_running_costs", "biz income: the running costs row has its key");

        // The refresh reads the table once, in the order that counts a line once.
        string books = File.ReadAllText(System.IO.Path.Combine(scripts, "Economy", "GameBooks.cs")).Replace("\r\n", "\n");
        Check(books.Contains("BizLine kind = Ours(t) ? BizLines.Of(t.transactionType) : BizLine.Foreign;")
              && books.Contains("|| kind == BizLine.Takings || (takings != null && Ours(t) && takings(t.transactionType)))")
              && books.Contains("else if (kind == BizLine.Cost && !inLine)")
              && books.Contains("?? BizLines.RunningRow;")
              && books.IndexOf("kind == BizLine.Takings", StringComparison.Ordinal) < books.IndexOf("kind == BizLine.Cost && !inLine", StringComparison.Ordinal),
            "biz income: GameBooks.Refresh counts a takings key as sales and a cost key in no game line as a row among the goods, each once");
        Check(books.Contains("inLine = true;") && System.Text.RegularExpressions.Regex.Matches(books, "inLine = true;").Count == 4,
            "biz income: a cost the game's four lines (salaries, rent, marketing, licences) already hold is not counted a second time");
        Check(books.Contains("Took(t.address, amount);") && books.Contains("entry.totalRevenue = Mathf.Max(0f, (float)sold + took.Value[0]);"),
            "biz income: the revenue of the business's order book carries what the mod's lines took, set and not added to");
        Check(books.Contains("Verify(day, summary, byAddress);") && books.Contains("public static string BizVerdict()"),
            "biz income: each refresh runs the self-check of the day");

        // The construction company's money is booked at its office, its own lines only.
        string build = File.ReadAllText(System.IO.Path.Combine(scripts, "Construction", "ConstructionGame.cs")).Replace("\r\n", "\n");
        Check(build.Contains("Info(key, name, deductible), null, At(key), false, true)") && build.Contains("Info(key, name, deductible), null, At(key), true)")
              && build.Contains("Info(key, name, false), null, At(key), true)") && build.Contains("BizLines.InDomain(key) ? OfficeAddress() : null"),
            "biz income: the builder's takings and costs are booked at its office, parking lots and the rest are not its books");

        // Where the check is said: the economy's day and the tech sector's, once a day.
        string day = File.ReadAllText(System.IO.Path.Combine(scripts, "Economy", "Banking", "EconomyDirector.Day.cs")).Replace("\r\n", "\n");
        string tech = File.ReadAllText(System.IO.Path.Combine(scripts, "Wiring", "TechLinks.cs")).Replace("\r\n", "\n");
        Check(day.Contains("LogBizman();") && day.Contains("GameBooks.BizVerdict()") && tech.Contains("Economy.GameBooks.BizVerdict()"),
            "biz income: the self-check line is said by the economy's day and the tech sector's");

        // The self-check line: PASS when every business counts, FAIL naming the ones that do not (made to fail once).
        string pass = BizLines.Verdict(12, 7, 7, string.Empty);
        string fail = BizLines.Verdict(12, 7, 5, "Crest Studio: the game made it no statement for the day; Delta Hosting: a key in no list of BizLines");
        Check(pass == "BizMan check (income, day 12): 7 of 7 businesses run by the mod add their takings and costs to BizMan: PASS", "biz income: the self-check line passes when 7 of 7 businesses count: " + pass);
        Check(fail.EndsWith("FAIL (Crest Studio: the game made it no statement for the day; Delta Hosting: a key in no list of BizLines)", StringComparison.Ordinal) && fail.Contains("5 of 7"),
            "biz income: the self-check line fails, naming the businesses that do not count (made to fail once)");

        // The offline model of the day's figures: a takings line and cost lines make a profit once, a refresh twice changes nothing.
        BizDay a = BizDay.Of(new[] { ("reallife_transaction_tech_takings", 1000f), ("reallife_transaction_tech_hosting", -100f), ("reallife_transaction_tech_upkeep", -50f), ("reallife_transaction_tech_venture_raise", 5000f), ("reallife_transaction_tech_wages", -200f) });
        Check(Math.Abs(a.Sales - 1000f) < 0.001f && Math.Abs(a.Costs - 150f) < 0.001f && Math.Abs(a.Profit - 850f) < 0.001f,
            "biz income: a firm's day is its takings 1000, hosting 100 and upkeep 50 as costs (its wages are the game's salaries line, its raise capital): profit 850, counted once");
    }

    // The statement's sums for a day as GameBooks.Refresh makes them, from the table alone: wages (a game category line) are counted by the game, not here.
    private struct BizDay
    {
        public float Sales, Costs, Profit;

        public static BizDay Of((string type, float amount)[] lines)
        {
            var day = new BizDay();
            for (int i = 0; i < lines.Length; i++)
            {
                BizLine kind = BizLines.Of(lines[i].type);
                if (kind == BizLine.Takings)
                {
                    day.Sales += lines[i].amount;
                }
                else if (kind == BizLine.Cost && !lines[i].type.EndsWith("_wages", StringComparison.Ordinal))
                {
                    day.Costs -= lines[i].amount;
                }
            }
            day.Profit = day.Sales - day.Costs;
            return day;
        }
    }
}
