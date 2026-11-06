using System;
using System.IO;
using System.Text.RegularExpressions;

internal static partial class Program
{
    private static void CompanionReadOnlyChecks()
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../Assets/Mods/RealLifeOverhaul/Scripts"));
        string prices = File.ReadAllText(System.IO.Path.Combine(root, "Economy/City/CityGame.Companion.cs"));
        foreach (string state in new[] { "Own", "Goods", "Priced", "KindCount" })
        {
            Check(!Regex.IsMatch(prices, @"\b" + state + @"(?:\s*\[[^\]]+\]\s*=|\s*\.\s*(?:Clear|Add|Remove)\s*\()"),
                "companion: registered capture never mutates " + state);
        }
        string company = File.ReadAllText(System.IO.Path.Combine(root, "Economy/Company/CompanyDirector.Companion.cs"));
        Check(company.Contains("states[part.ToString().ToLowerInvariant()] = CompanionPartStatus(part)"),
            "companion: company rows and independent modules share part status precedence");
        string office = File.ReadAllText(System.IO.Path.Combine(root, "Interiors/InteriorDirector.cs"));
        string round = office.Substring(office.IndexOf("private void Round("));
        Check(round.IndexOf("_carry.Clear();") < round.IndexOf("Rework(day, true)"),
            "companion: office sequence begins every morning with zero carry");
        string[] forbidden = { "GameBooks.Refresh(", "CompanyGame.Offers(", "CompanyGame.NoteHours(",
            "UtilitiesRules.AddDay(", "BankTerms.Find(", ".MaterialFactor(", "GameManager.ChangeMoney",
            "GameLoans.MeansLimit(", "BusinessHelper.UpdatePromotion(", "SaveHook.Keep(", "CompanionGame.Save(" };
        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string name = System.IO.Path.GetFileName(file);
            if (!name.Contains(".Companion") && name != "EducationCompanion.cs" && name != "Wiring.PlanningCompanionLinks.cs")
            {
                continue;
            }
            string code = Regex.Replace(File.ReadAllText(file), @"//[^\r\n]*", string.Empty);
            foreach (string call in forbidden)
            {
                Check(!code.Contains(call), "companion: read-only " + name + " never invokes " + call);
            }
        }
        // A fault stays local but is still said once in Player.log, so a broken module can be found.
        foreach (string links in new[] { "Wiring/Wiring.CompanionLinks.cs", "Wiring/Wiring.PlanningCompanionLinks.cs" })
        {
            string code = File.ReadAllText(System.IO.Path.Combine(root, links));
            Check(!Regex.IsMatch(code, @"catch \(Exception\)") && Regex.Matches(code, @"catch \(Exception e\)\s*\{\s*Fault\(""").Count
                == Regex.Matches(code, @"catch \(Exception e\)").Count, "companion: every fault in " + links + " is logged once through Fault");
        }
        string game = File.ReadAllText(System.IO.Path.Combine(root, "Core/CompanionGame.cs"));
        Check(game.Contains("CompanionBridges.Readers, Failed);") && Regex.IsMatch(game, @"catch \(Exception e\)\s*\{\s*Failed\(""capture"", e\);\s*return null;")
            && game.Contains("if (Reported.Add(capability))"), "companion: a module's or a capture's fault is logged once a session");
        string ownership = File.ReadAllText(System.IO.Path.Combine(root, "Core/CompanionOwnership.cs"));
        Check(Regex.IsMatch(ownership, @"catch \(Exception e\)\s*\{\s*CompanionGame\.Failed\(""ownership"", e\);"),
            "companion: an ownership query's fault is logged once a session");
    }
}
