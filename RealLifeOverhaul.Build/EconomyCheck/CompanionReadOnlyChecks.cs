using System;
using System.IO;
using System.Text.RegularExpressions;

internal static partial class Program
{
    private static void CompanionReadOnlyChecks()
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../Assets/Mods/RealLifeOverhaul/Scripts"));
        string[] forbidden = { "GameBooks.Refresh(", "CompanyGame.Offers(", "CompanyGame.NoteHours(",
            "UtilitiesRules.AddDay(", "BankTerms.Find(", ".MaterialFactor(", "GameManager.ChangeMoney", "Fault(",
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
    }
}
