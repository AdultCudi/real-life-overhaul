using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

// Faults found in Scripts/Wiring: adapters handed to a bridge or an event with no try (Flare's paper pieces, RLO Texts'
// Flare request and the Ledger's words, and some seventy more), so a fault went into the feature that asked and was
// logged there, not as a Wiring fault; a pairing's call of the one before it outside the try (the rivals' paper pieces),
// so a fault there dropped this pairing's part too; the financial analyst's role written as the number 29; and lists of
// the game's things (the kerb's waypoints, the loans secured on a place, a shop's staff) kept after a fault.
internal static partial class Program
{
    private static void WiringFixes()
    {
        string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Wiring"));
        string[] files = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.cs") : new string[0];
        Array.Sort(files, StringComparer.Ordinal);
        int adapters = 0;
        var bare = new List<string>();
        var before = new List<string>();
        foreach (string file in files)
        {
            string code = File.ReadAllText(file);
            var handed = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(code, @"(?:[^=!<>]=|\+=)\s*([A-Z]\w*)\s*;"))
            {
                handed.Add(m.Groups[1].Value);
            }
            foreach (Match m in Regex.Matches(code, @"\?\?\s*\(\s*_\w+\s*=\s*([A-Z]\w*)\s*\)"))
            {
                handed.Add(m.Groups[1].Value);
            }
            foreach (Match m in Regex.Matches(code, @"private static [\w<>\[\], .]+? (\w+)\(([^)]*)\)\s*\r?\n\s*\{"))
            {
                if (!handed.Contains(m.Groups[1].Value))
                {
                    continue;
                }
                adapters++;
                string body = Block(code, m.Index + m.Length - 1);
                string where = System.IO.Path.GetFileName(file) + " " + m.Groups[1].Value;
                if (!Regex.IsMatch(body, @"\btry\s*\r?\n\s*\{"))
                {
                    bare.Add(where);
                }
                // The pairing before is asked inside a try: a fault there is its own, never this pairing's.
                foreach (Match call in Regex.Matches(body, @"_\w+Before(\?\.Invoke)?\("))
                {
                    if (body.LastIndexOf("try", call.Index, StringComparison.Ordinal) < 0)
                    {
                        before.Add(where);
                    }
                }
            }
        }
        Check(adapters >= 150, "wiring fix: the adapters handed to a bridge or an event are found: " + adapters);
        Check(bare.Count == 0, "wiring fix: every adapter handed to a bridge or an event has a try, its catch a Wiring fault line or a plain default (none without: " + string.Join(", ", bare) + ")");
        Check(before.Count == 0, "wiring fix: every pairing asks the one before it inside a try (none outside: " + string.Join(", ", before) + ")");

        string careers = ModSource(@"Wiring\CareersLinks.cs"), pro = ModSource(@"Careers\Work\ProCatalogue.cs");
        Check(RealLifeOverhaul.Tech.TalentCatalogue.FinancialAnalyst == 29 && RealLifeOverhaul.Tech.TalentCatalogue.Roles[RealLifeOverhaul.Tech.TalentCatalogue.FinancialAnalyst].Key == "financial_analyst"
              && careers.Contains("p.Role == TalentCatalogue.FinancialAnalyst") && !Regex.IsMatch(careers + pro, @"Role == 29\b|new ProRank\(29,"),
            "wiring fix: the financial analyst is a named role, used wherever its number stood");

        string kerb = Block(ModSource(@"Wiring\DebtLinks.cs"), "private static bool KerbSpot(");
        string services = ModSource(@"Wiring\ServicesLinks.cs");
        string owed = Block(services, "private static int LoansOwed(Address address)"), settle = Block(services, "private static bool SettleLoans(Address address)");
        string staff = Block(ModSource(@"Wiring\FlareLiveLinks.cs"), "private static void Staff(List<string> into)");
        Check(Regex.IsMatch(kerb, @"finally\s*\{\s*Route\.Clear\(\);") && Occurs(kerb, "Route.Clear();") == 1
              && Regex.IsMatch(owed, @"finally\s*\{\s*Loans\.Clear\(\);") && Regex.IsMatch(settle, @"finally\s*\{\s*Loans\.Clear\(\);")
              && Regex.IsMatch(staff, @"finally\s*\{\s*Staffed\.Clear\(\);"),
            "wiring fix: the kerb's waypoints, the loans on a place and a shop's staff are let go after every look, a fault or not");
    }

    // The block from the brace at or after a signature (or an index) to the brace that closes it; empty if not found.
    private static string Block(string code, string signature)
    {
        int at = code.IndexOf(signature, StringComparison.Ordinal);
        return at < 0 ? string.Empty : Block(code, at);
    }

    private static string Block(string code, int from)
    {
        int open = code.IndexOf('{', from);
        int depth = 0;
        for (int i = open; open >= 0 && i < code.Length; i++)
        {
            if (code[i] == '{')
            {
                depth++;
            }
            else if (code[i] == '}' && --depth == 0)
            {
                return code.Substring(open, i - open + 1);
            }
        }
        return string.Empty;
    }
}
