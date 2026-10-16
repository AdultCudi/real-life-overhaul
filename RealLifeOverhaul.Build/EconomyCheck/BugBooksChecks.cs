using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.Meta;

// "Report a bug" with the staff and the money (Scripts/Meta/BugReportRules.cs StaffText and BooksText, BugReportGame Staff
// and Books). A player's report (16 Oct, 0.28.4) said a headquarters paid over 3,000 a day where 1,000 was expected, and a
// save stood some 600,000 below nothing one morning; report.txt gave the money rounded and nothing else, so neither could
// be told from it. Now it lists every employee's wage against the game's own for the skill with their hours, each
// business's day of rota, and yesterday's and today's money in and out by kind and place, with the cash before and after.
internal static partial class Program
{
    private static void BugBooks()
    {
        BugBooksStaff();
        BugBooksMoney();
        BugBooksEdges();
        BugBooksSource();
    }

    private static BugFacts BugBooksFacts()
    {
        var f = new BugFacts
        {
            Written = "2026-10-16 06:32:55",
            ModVersion = "0.29.1",
            GameBuild = 3682,
            MadeFor = 3682,
            Platform = "WindowsPlayer",
            Language = "pt",
            GoingPublic = "not switched on",
            Preset = "Custom, nearest Realistic",
            InCity = true,
            Day = 62,
            Hour = 9,
            Minute = 32,
            Money = 13000,
            Contract = "this game is the build the mod is made for",
            Profile = BugProfile
        };
        // A headquarters as the report's: two purchasing agents and two professionals at a desk every open hour, and a gym.
        f.Staff.Add(new BugStaff { Business = "Matrix Ltda", Kind = "ba:businesstype_headquarters", Skill = "ba:skill_hrmanager", Level = 62, WageCents = 9850, FormulaCents = 4100, HoursToday = 8, HoursWeek = 40, RotaWeek = 45 });
        f.Staff.Add(new BugStaff { Business = "Matrix Ltda", Kind = "ba:businesstype_headquarters", Skill = "ba:skill_headhunter", Level = 40, WageCents = 5200, FormulaCents = 5000, HoursToday = 8, HoursWeek = 40, RotaWeek = 45 });
        f.Staff.Add(new BugStaff { Business = "Matrix Ltda", Kind = "ba:businesstype_headquarters", Skill = "ba:skill_purchasingagent", Level = 30, WageCents = 3000, FormulaCents = 3000, HoursToday = 8, HoursWeek = 40, RotaWeek = 45 });
        f.Staff.Add(new BugStaff { Business = "Matrix Ltda", Kind = "ba:businesstype_headquarters", Skill = "ba:skill_purchasingagent", Level = 20, WageCents = 2800, FormulaCents = 2800, HoursToday = 8, HoursWeek = 40, RotaWeek = 45 });
        f.Staff.Add(new BugStaff { Business = "Top Fitness", Kind = "ba:businesstype_gym", Skill = "ba:skill_gymtrainer", Level = 50, WageCents = 3030, FormulaCents = 3030, HoursToday = 11, HoursWeek = 66, RotaWeek = 84 });
        // The night of day 61 (its wages are dated on it), then day 62 so far.
        long cash = 4966600;
        void Move(int day, string kind, string where, long cents)
        {
            cash += cents;
            f.Books.Add(new BugMoney { Day = day, Kind = kind, Where = where, Cents = cents, BalanceCents = cash });
        }
        Move(61, "ba:transaction_revenue", "Top Fitness", 480000);
        Move(61, "ba:transaction_wage", "Matrix Ltda", -78800);
        Move(61, "ba:transaction_wage", "Matrix Ltda", -41600);
        Move(61, "ba:transaction_wage", "Top Fitness", -33330);
        Move(61, "ba:transaction_rent", "Top Fitness", -43100);
        Move(61, "othermod_transaction_catchup", string.Empty, -67000000);
        Move(62, "reallife_transaction_co_premium", "Top Fitness", -1600);
        Move(60, "ba:transaction_wage", "Matrix Ltda", -99999);   // the day before yesterday: not in it
        return f;
    }

    // 1. The headquarters: what each is paid against the game's own, and what a day of its rota costs.
    private static void BugBooksStaff()
    {
        string text = BugReportRules.Text(BugBooksFacts());
        Check(text.Contains("\nStaff and wages: 5 employee(s)\n"), "bug books: report.txt has a staff section with every employee counted");
        Check(text.Contains("  Matrix Ltda (headquarters): 4 staff, 32 h worked today, 180 h a week on the rota, about 1,340 a day of it at their wages\n"),
            "bug books: a business's line gives its staff, today's hours, its rota's week and a day of it at their wages (45 h at 98.50, 52, 30 and 28 over 7 days)");
        Check(text.Contains("    hrmanager 62: 98.50 an hour (the game's own for the skill 41.00, 2.4 times it), 8 h today, 40 h this week, 45 h a week on the rota\n"),
            "bug books: an employee paid over the game's own for the skill is said, with how many times it");
        Check(text.Contains("    headhunter 40: 52.00 an hour (the game's own for the skill 50.00), 8 h today"),
            "bug books: a wage within 5% of the game's own is not called higher");
        Check(text.Contains("  Top Fitness (gym): 1 staff, 11 h worked today, 84 h a week on the rota, about 364 a day of it at their wages\n"),
            "bug books: each business has its own line");
        Check(text.IndexOf("Matrix Ltda (headquarters)", StringComparison.Ordinal) < text.IndexOf("Top Fitness (gym)", StringComparison.Ordinal)
              && text.IndexOf("Game contract:", StringComparison.Ordinal) < text.IndexOf("Staff and wages:", StringComparison.Ordinal),
            "bug books: the sections come after the game contract, the businesses in the order given");
        int staffAt = text.IndexOf("Staff and wages:", StringComparison.Ordinal);
        Check(staffAt >= 0 && text.IndexOf("name", staffAt, StringComparison.OrdinalIgnoreCase) < 0, "bug books: no employee's name is written");
    }

    // 2. The money: where a night's loss went, kind by kind and place by place, with the cash before and after.
    private static void BugBooksMoney()
    {
        string text = BugReportRules.Text(BugBooksFacts());
        Check(text.Contains("\nMoney in and out (the game's books): yesterday and today\n"), "bug books: report.txt has a money section");
        Check(text.Contains("  day 61: 6 transaction(s), cash 49,666 to -617,502, in 4,800, out 671,968\n"),
            "bug books: yesterday's cash before its first transaction and after its last, and what came in and went out");
        Check(text.Contains("    othermod_transaction_catchup: -670,000 (1)\n"), "bug books: a kind of money the game does not know is named by its key");
        Check(text.Contains("    wage at Matrix Ltda: -1,204 (2)\n") && text.Contains("    wage at Top Fitness: -333 (1)\n"),
            "bug books: wages are added up by the business they were paid at, with how many payments");
        Check(text.IndexOf("othermod_transaction_catchup", StringComparison.Ordinal) < text.IndexOf("revenue at Top Fitness", StringComparison.Ordinal)
              && text.IndexOf("revenue at Top Fitness", StringComparison.Ordinal) < text.IndexOf("wage at Matrix Ltda", StringComparison.Ordinal),
            "bug books: the largest amounts first, money in or out");
        Check(text.Contains("  day 62 (today, so far): 1 transaction(s), cash -617,502 to -617,518, in 0, out 16\n")
              && text.Contains("    reallife_transaction_co_premium at Top Fitness: -16 (1)\n"), "bug books: today's money so far, a key of the mod's kept whole");
        Check(!text.Contains("-99,999") && !text.Contains("-1,000 (1)") && !text.Contains("day 60"), "bug books: only yesterday and today");
    }

    // 3. Edges: none, a part that could not be read, more than are listed, more kinds than are listed, a day with nothing.
    private static void BugBooksEdges()
    {
        var text = new StringBuilder();
        BugReportRules.StaffText(new List<BugStaff>(), null, text);
        BugReportRules.BooksText(new List<BugMoney>(), null, 10, text);
        string none = text.ToString();
        Check(none.Contains("Staff and wages: no employees\n") && none.Contains("  day 9: nothing booked\n") && none.Contains("  day 10 (today, so far): nothing booked\n"),
            "bug books: no staff and no transactions are said as such");

        text.Clear();
        BugReportRules.StaffText(new List<BugStaff>(), "could not be read (NullReferenceException)", text);
        BugReportRules.BooksText(new List<BugMoney>(), "could not be read (InvalidCastException)", 10, text);
        Check(text.ToString() == "\nStaff and wages: could not be read (NullReferenceException)\n\nMoney in and out (the game's books): could not be read (InvalidCastException)\n",
            "bug books: a part that could not be read says why, and nothing else");

        var many = new List<BugStaff>();
        for (int i = 0; i < BugReportRules.MostStaff + 7; i++)
        {
            many.Add(new BugStaff { Business = i < 3 ? "A" : "B", Skill = "ba:skill_cleaning", Level = 10, WageCents = 1500, FormulaCents = 0, HoursToday = 1, RotaWeek = 7 });
        }
        text.Clear();
        BugReportRules.StaffText(many, null, text);
        string big = text.ToString();
        int rows = 0;
        for (int at = big.IndexOf("    cleaning 10:", StringComparison.Ordinal); at >= 0; at = big.IndexOf("    cleaning 10:", at + 1, StringComparison.Ordinal))
        {
            rows++;
        }
        Check(rows == BugReportRules.MostStaff && big.Contains("    and 7 more\n") && big.Contains("  B: 204 staff, 204 h worked today, 1428 h a week on the rota, about 3,060 a day of it at their wages\n")
              && big.Contains("(the game's own for the skill not known)"),
            "bug books: at most " + BugReportRules.MostStaff + " employees listed, the rest counted, every business still added up in full, a wage with no formula said so");

        var kinds = new List<BugMoney>();
        for (int i = 0; i < BugReportRules.MostKinds + 3; i++)
        {
            kinds.Add(new BugMoney { Day = 5, Kind = "ba:transaction_k" + i.ToString("00"), Cents = -(i + 1) * 100, BalanceCents = 0 });
        }
        text.Clear();
        BugReportRules.BooksText(kinds, null, 5, text);
        string k = text.ToString();
        Check(k.Contains("    k17: -18 (1)\n") && !k.Contains("    k02:") && k.Contains("    3 other kind(s): -6\n") && k.Contains("  day 4: nothing booked\n"),
            "bug books: the largest " + BugReportRules.MostKinds + " kinds listed, the rest in one line");

        Check(BugReportRules.Short("ba:transaction_wage") == "wage" && BugReportRules.Short("ba:skill_hrmanager") == "hrmanager" && BugReportRules.Short("ba:businesstype_headquarters") == "headquarters"
              && BugReportRules.Short("reallife_transaction_co_premium") == "reallife_transaction_co_premium" && BugReportRules.Short("ba:transaction_") == "transaction_"
              && BugReportRules.Short(null) == "?" && BugReportRules.Short("ba:itemname_x") == "itemname_x",
            "bug books: the game's keys are shortened only where the kind is plain from the section");
        Check(BugReportRules.Money(-62170750, false) == "-621,708" && BugReportRules.Money(9850, true) == "98.50" && BugReportRules.Money(0, false) == "0",
            "bug books: money whole or with cents, in one culture");

        var away = BugBooksFacts();
        away.InCity = false;
        string menu = BugReportRules.Text(away);
        Check(!menu.Contains("Staff and wages") && !menu.Contains("Money in and out"), "bug books: with no save loaded there is neither section");
    }

    // 4. The game side reads them on the press, the player's own only, each part in its own try.
    private static void BugBooksSource()
    {
        string game = BugSource("BugReportGame.cs");
        Check(game.Contains("f.StaffNote = StaffPart(f.Staff);") && game.Contains("f.BooksNote = BooksPart(f.Books, f.Day);")
              && game.Contains("                Staff(into);\n                return null;".Replace("\n", game.Contains("\r\n") ? "\r\n" : "\n"))
              && game.Contains("                Books(into, today);\n                return null;".Replace("\n", game.Contains("\r\n") ? "\r\n" : "\n")),
            "bug books: the staff and the books are read with the day and the money, each part on its own");
        Check(game.Contains("GoingPublic.Foreign(reg)") && game.Contains("e.IsCandidate") && game.Contains("Helpers.EmployeeHelper.CalculateHourlyWageForSkill(e.characterData.skills[0])"),
            "bug books: another player's staff and candidates are left out, and the game's own wage is the game's formula");
        Check(game.Contains("Economy.GameBooks.Day(day)") && game.Contains("for (int day = today - 1; day <= today; day++)"),
            "bug books: yesterday's and today's transactions come from the one walk of the history (GameBooks.Day)");
    }
}
