using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;

internal static partial class Program
{
    private static Dictionary<string, object> CompanionClock()
    {
        return CompanionSnapshot.Row("character", "fixture-player", "day", 12, "hour", 8, "minute", 30);
    }

    private static Dictionary<string, Func<CompanionModule>> CompanionReaders()
    {
        var accounts = new List<CreditAccount>
        {
            new CreditAccount { Id = 1, Kind = ProductKind.CarFinance, Owed = 12000, RateBp = 800, Instalment = 100 },
            new CreditAccount { Id = 2, Kind = ProductKind.Mortgage, Owed = 50000, RateBp = 900, Frozen = true },
            new CreditAccount { Id = 3, Closed = true, Owed = 99 },
            new CreditAccount { Id = 4, Test = true, Owed = 99 }
        };
        return new Dictionary<string, Func<CompanionModule>>(StringComparer.Ordinal)
        {
            ["features"] = () => new CompanionModule("ready", new object[]
            {
                CompanionSnapshot.Row("id", "staff", "enabled", true, "running", true),
                CompanionSnapshot.Row("id", "managers", "enabled", false, "running", false)
            }),
            ["businesses"] = () => new CompanionModule("ready", new object[]
            {
                CompanionSnapshot.Row("street", "ba:street_test", "number", 1, "name", "Cafe \"München\"\n& Co",
                    "typeId", "reallife:businesstype_test", "temporarilyClosed", false)
            }),
            ["employment"] = () => new CompanionModule("ready", new object[] { CompanyCompanion.Wage("employee-1", 2000, 1100, 150) }),
            ["credit"] = () => new CompanionModule("ready", CreditCompanion.Accounts(accounts, 12)),
            ["accounting"] = () => new CompanionModule("ready", CompanionSnapshot.Row("source", "game.financialSummaries",
                "coverage", "statement-backed-only", "adjustment", "none"))
        };
    }

    private static bool CompanionRejects(object value)
    {
        try
        {
            CompanionSnapshot.Json(value);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    // Optional directory writes the shared fixtures; without it, the committed bytes must match the producer.
    private static void CompanionChecks(string output)
    {
        CompanionPlanningChecks(output);
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        foreach (string language in new[] { "en", "pt", "zh-cn" })
        {
            using (JsonDocument locale = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(root,
                "Assets/Mods/RealLifeOverhaul/Locales", language + ".json"))))
            {
                bool announced = false;
                foreach (JsonProperty row in locale.RootElement.EnumerateObject())
                {
                    announced |= row.Name.StartsWith("reallife_changes_", StringComparison.Ordinal)
                        && row.Value.GetString().Contains("Big Copilot");
                }
                Check(announced && locale.RootElement.GetProperty("reallife_guide_savetools").GetString().Contains("Big Copilot"),
                    "companion: the Guide and What's new explain reporting in " + language);
            }
        }
        Dictionary<string, Func<CompanionModule>> readers = CompanionReaders();
        string ready = CompanionSnapshot.Capture("fixture-v1", CompanionClock(), readers);
        using (JsonDocument json = JsonDocument.Parse(ready))
        {
            JsonElement modules = json.RootElement.GetProperty("modules");
            Check(json.RootElement.GetProperty("schemaVersion").GetInt32() == 1, "companion: envelope version is 1");
            Check(modules.GetProperty("employment").GetProperty("data")[0].GetProperty("ordinaryHourlyCents").GetInt32() == 2350,
                "companion: ordinary wage is 2000 base + 200 city + 150 raise cents");
            Check(modules.GetProperty("credit").GetProperty("data").GetArrayLength() == 2, "companion: closed and test credit accounts are excluded");
            Check(modules.GetProperty("credit").GetProperty("data")[1].GetProperty("paymentTomorrowCents").GetInt64() == 0,
                "companion: frozen debt remains outstanding but has no payment tomorrow");
            Check(modules.GetProperty("businesses").GetProperty("data")[0].GetProperty("name").GetString() == "Cafe \"München\"\n& Co",
                "companion: quoted Unicode and newlines round trip through JSON");
        }
        CultureInfo culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Check(ready == CompanionSnapshot.Capture("fixture-v1", CompanionClock(), readers), "companion: repeated capture is invariant under culture");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
        var account = new CreditAccount { Id = 7, Kind = ProductKind.CarFinance, Owed = 4000, RateBp = 700, Carry = 19, Instalment = 50 };
        CreditCompanion.Accounts(new List<CreditAccount> { account }, 12);
        Check(account.Owed == 4000 && account.Carry == 19 && account.Instalment == 50, "companion: observing payment does not settle or change interest carry");
        Check(CreditCompanion.Accounts(new List<CreditAccount>(), 12).Count == 0, "companion: no accounts is an empty ready list");
        Check((long)CompanyCompanion.Wage("x", 0, 1000, 0)["ordinaryHourlyCents"] == 0, "companion: zero wage remains zero");
        Check((long)CompanyCompanion.Wage("x", 2000, PriceRules.Level(1005), 0)["ordinaryHourlyCents"] == 2000,
            "companion: city quiet band follows the company's price level");
        Check((long)CompanyCompanion.Wage("x", 2000, 900, 0)["ordinaryHourlyCents"] == 1800, "companion: deflation can reduce ordinary pay");
        int faults = 0;
        readers["credit"] = () => throw new InvalidOperationException("synthetic");
        readers["employment"] = () => new CompanionModule("unavailable");
        readers["features"] = () => new CompanionModule("disabled");
        string partial = CompanionSnapshot.Capture("fixture-v1", CompanionClock(), readers, (key, error) => faults++);
        using (JsonDocument json = JsonDocument.Parse(partial))
        {
            JsonElement modules = json.RootElement.GetProperty("modules");
            Check(faults == 1 && modules.GetProperty("credit").GetProperty("status").GetString() == "failed",
                "companion: a throwing provider becomes one failed capability");
            Check(modules.GetProperty("businesses").GetProperty("status").GetString() == "ready", "companion: a failed provider leaves other capabilities ready");
        }
        readers["credit"] = () => new CompanionModule("ready", double.NaN);
        Check(CompanionSnapshot.Capture("fixture-v1", CompanionClock(), readers).Contains("\"status\":\"failed\""),
            "companion: unsupported provider data is isolated before serialising the envelope");
        foreach (CompanionModule invalid in new[] { new CompanionModule(""), new CompanionModule("unknown"),
            new CompanionModule("disabled", new object[0]), new CompanionModule("ready") { Version = 0 } })
        {
            readers["credit"] = () => invalid;
            using (JsonDocument json = JsonDocument.Parse(CompanionSnapshot.Capture("fixture-v1", CompanionClock(), readers,
                (key, error) => throw new InvalidOperationException("diagnostic failure"))))
            {
                JsonElement modules = json.RootElement.GetProperty("modules");
                Check(modules.GetProperty("credit").GetProperty("status").GetString() == "failed"
                    && modules.GetProperty("businesses").GetProperty("status").GetString() == "ready",
                    "companion: invalid module wrapper and diagnostic failures stay isolated");
            }
        }
        Check(CompanionRejects(9007199254740992L), "companion: integers outside JavaScript precision are rejected");
        Check(CompanionSnapshot.Json(9007199254740991L) == "9007199254740991", "companion: largest exact integer survives");
        Check(CompanionRejects(string.Empty), "companion: empty text is rejected");
        Check(CompanionRejects(new string('x', 1025)), "companion: text over 1024 characters is rejected");
        Check(CompanionSnapshot.Json(new string('x', 1024)).Length == 1026, "companion: maximum text length survives");
        var large = new object[5000];
        for (int i = 0; i < large.Length; i++)
        {
            large[i] = new string('x', 1024);
        }
        Check(CompanionRejects(large), "companion: output over four MiB is rejected");
        Check(CompanionRejects(new object[CompanionSnapshot.MaxRows + 1]), "companion: more than 10000 records is rejected");
        object deep = null;
        for (int i = 0; i < 18; i++)
        {
            deep = new object[] { deep };
        }
        Check(CompanionRejects(deep), "companion: recursion is bounded");
        object providerDepth = null;
        for (int i = 0; i < 14; i++)
        {
            providerDepth = new object[] { providerDepth };
        }
        readers["credit"] = () => new CompanionModule("ready", providerDepth);
        using (JsonDocument json = JsonDocument.Parse(CompanionSnapshot.Capture("fixture-v1", CompanionClock(), readers)))
        {
            JsonElement modules = json.RootElement.GetProperty("modules");
            Check(modules.GetProperty("credit").GetProperty("status").GetString() == "failed"
                && modules.GetProperty("businesses").GetProperty("status").GetString() == "ready",
                "companion: provider depth includes its position in the envelope and stays isolated");
        }

        var timer = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            CompanionSnapshot.Capture("fixture-v1", CompanionClock(), readers);
        }
        Check(timer.ElapsedMilliseconds < 1000, "companion: 100 small captures fit a 1000 ms budget");
        string directory = output ?? System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../contracts/companion/v1"));
        var fixtures = new Dictionary<string, string> { ["ready.json"] = ready, ["partial.json"] = partial };
        if (output != null)
        {
            Directory.CreateDirectory(directory);
        }
        foreach (KeyValuePair<string, string> fixture in fixtures)
        {
            string path = System.IO.Path.Combine(directory, fixture.Key);
            if (output != null)
            {
                File.WriteAllText(path, fixture.Value + "\n");
            }
            Check(File.Exists(path) && File.ReadAllText(path) == fixture.Value + "\n", "companion: producer matches shared fixture " + fixture.Key);
        }
    }
}
