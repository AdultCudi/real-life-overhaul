using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;
using RealLifeOverhaul.Utilities;
using RealLifeOverhaul.Careers;

internal static partial class Program
{
    private static void CompanionPlanningChecks(string output)
    {
        CompanionReadOnlyChecks();
        CompanionOwnershipChecks();
        CompanionManagerChecks();
        var promotion = new CompanionPromotion();
        promotion.Remember(false, 31, 45, -2, 7, 9, 0.25f);
        promotion.Remember(true, 31, 31, 0, 0, 0, 0.25f);
        Check(promotion.Known && promotion.Own == 31 && promotion.Total == 45 && promotion.Percent == -2
            && promotion.Ambient == 7 && promotion.Interior == 9, "companion: active projection survives save put-back byte for byte");
        promotion.Remember(false, 33, 47, -1, 7, 9, 0.25f);
        Check(promotion.Own == 33 && promotion.Total == 47, "companion: normal play refreshes preserved projection after save");
        var study = new StudyFile();
        study.Courses.Add(new Enrolment { Course = StudyCatalogue.All[0].Id, Start = 5, Fee = 100,
            Instalments = 1, Seen = 2 });
        string studyBefore = CareersStore.Write(study);
        object education = EducationCompanion.Capture(study, 10, 1137, 100000, 1, true, course => true).Data;
        string educationJson = CompanionSnapshot.Json(education);
        Check(CareersStore.Write(study) == studyBefore, "companion: education quotes preserve enrollment and loan state bytes");
        Check(educationJson.Contains("totalFeeCents") && educationJson.Contains("installments"), "companion: education includes complete course investment and session triggers");
        if (output != null)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(System.IO.Path.Combine(output, "education-sample.json"), educationJson + "\n");
        }
        CompanionPortableVectors(output);
        var vectors = new List<object>();
        var calendars = new List<object>();
        foreach (int year in new[] { 60, 365 })
        {
            calendars.Add(UtilitiesCompanion.Terms(1137, year));
            foreach (int day in new[] { 1, 10, 11, 25, 26, 40, 41, 55, 56, 61, 365 })
            {
                int season = CalendarRules.Season(day, year);
                foreach (int kind in new[] { 0, 1, 3, 8 })
                {
                    foreach (bool open in new[] { false, true })
                    {
                        foreach (int service in new[] { 0, 1 })
                        {
                            long use = UtilitiesRules.Use((Service)service, (PlaceKind)kind, 73, 2, open, season);
                            int cents = UtilitiesRules.DayCents((Service)service, use, 1137, true);
                            vectors.Add(CompanionSnapshot.Row("input", CompanionSnapshot.Row("year", year, "day", day,
                                "service", service, "kind", kind, "sqm", 73, "people", 2, "open", open,
                                "level", 1137, "temporary", true), "expected", CompanionSnapshot.Row("season", season,
                                "use", use, "dayCents", cents, "money", UtilitiesRules.Money(cents))));
                        }
                    }
                }
            }
        }
        var tariff = new List<object>();
        foreach (long use in new long[] { 0, 1, 499, 500, 501, 10001, 2000000000 })
        {
            foreach (int service in new[] { 0, 1 })
            {
                foreach (int level in new[] { 499, 500, 990, 999, 1000, 1009, 1010, 4000, 4001 })
                {
                    foreach (bool temporary in new[] { false, true })
                    {
                        tariff.Add(CompanionSnapshot.Row("use", use, "service", service, "level", level,
                            "temporary", temporary, "expectedCents", UtilitiesRules.DayCents((Service)service, use, level, temporary)));
                    }
                }
            }
        }
        var internet = new List<object>();
        foreach (int month in new[] { 3, 5, 30, 31 })
        {
            foreach (int cents in new[] { 0, 1, 5001, 12345 })
            {
                var account = new RealLifeOverhaul.Utilities.Account { PriceCents = cents, Accrued = 17, Owed = 99, State = SupplyState.Active };
                internet.Add(CompanionSnapshot.Row("monthDays", month, "monthCents", cents,
                    "expectedDayCents", InternetRules.DayCents(account, month)));
                Check(account.Accrued == 17 && account.Owed == 99, "companion: internet quote preserves account");
            }
        }
        var state = new UtilitiesState { LastDay = 11, MonthStart = 6 };
        var observed = new RealLifeOverhaul.Utilities.Account { Place = "fixture", Service = Service.Internet, State = SupplyState.Active,
            Accrued = 1234, Owed = 87, LastBill = 92, Deposit = 30, PriceCents = 5001 };
        state.Accounts.Add(observed);
        string before = UtilitiesStore.Write(state);
        object sample = CompanionSnapshot.Row("model", "rlo-utilities-v1", "terms", UtilitiesCompanion.Terms(1137, 60),
            "accounts", new[] { UtilitiesCompanion.AccountRow(observed, CompanionValues.Address("ba:street_test", 1), state) },
            "internet", UtilitiesCompanion.Internet(1137, 5, state));
        Check(UtilitiesStore.Write(state) == before, "companion: utility terms leave owner state byte-identical");
        Check(CompanionValues.Decimal(0.000125f) == "0.000125", "companion: fractional-cent price remains exact");
        string json = CompanionSnapshot.Json(CompanionSnapshot.Row("model", "rlo-utilities-v1",
            "calendars", calendars, "useVectors", vectors, "tariffVectors", tariff, "internetVectors", internet, "sample", sample));
        string directory = output ?? System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../contracts/companion/v1"));
        string path = System.IO.Path.Combine(directory, "utilities-vectors.json");
        if (output != null)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, json + "\n");
        }
        else
        {
            Check(File.Exists(path) && File.ReadAllText(path) == json + "\n", "companion: utility vectors match owner production rules");
        }
    }
}
