using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Interiors;
using RealLifeOverhaul.Utilities;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        private static class PlanningCompanionLinks
        {
            private static bool CompanyEnabled => Settings.Economy && Settings.EconomyCompany;
            private static bool ManagersEnabled => CompanyEnabled && Settings.EconomyManagers;

            internal static void Connect()
            {
                CompanionBridges.Readers["prices"] = () => !Settings.Economy || !Settings.EconomyCity
                    ? new CompanionModule("disabled") : CityGame.CompanionPrices(Markets.MarketDirector.Instance != null
                        ? Markets.MarketDirector.Instance.CompanionMaterialFactors() : null);
                CompanionBridges.Readers["utilities"] = () => !Settings.Utilities ? new CompanionModule("disabled")
                    : UtilitiesDirector.Instance != null ? UtilitiesDirector.Instance.CompanionUtilities() : new CompanionModule("unavailable");
                CompanionBridges.Readers["company"] = () => !CompanyEnabled ? new CompanionModule("disabled")
                    : CompanyDirector.Instance != null ? CompanyDirector.Instance.CompanionCompany() : new CompanionModule("unavailable");
                CompanionBridges.Readers["requirements"] = Requirements;
                CompanionBridges.Readers["education"] = () => !Settings.CareersStudy ? new CompanionModule("disabled")
                    : CareersDirector.Instance != null ? CareersDirector.Instance.CompanionEducation() : new CompanionModule("unavailable");
                CompanionBridges.Readers["footfall"] = Footfall;
                CompanionBridges.Readers["interiors"] = () => !Settings.Interiors ? new CompanionModule("disabled")
                    : InteriorDirector.Instance != null ? InteriorDirector.Instance.CompanionInteriors() : new CompanionModule("unavailable");
                CompanionBridges.Readers["closures"] = BusinessGame.CompanionClosures;
                CompanionBridges.Readers["bankTerms"] = () => !Settings.Economy ? new CompanionModule("disabled")
                    : BankTerms.CompanionBankTerms();
                CompanionBridges.Readers["outsideStatements"] = Outside;
                CompanionBridges.Readers["ownership"] = OwnershipSnapshot;
                CompanionBridges.Ownership = Ownership;
            }

            private static CompanionModule Footfall()
            {
                try
                {
                    Dictionary<BuildingRegistration, int> company = CompanyDirector.CompanionFootfalls();
                    return BusinessGame.CompanionFootfall(CityGame.CompanionTolerance(), CityEconomyDirector.CompanionFootfallOf,
                        reg => company.TryGetValue(reg, out int value) ? value : 0);
                }
                catch (Exception)
                {
                    return new CompanionModule("failed");
                }
            }

            private static CompanionModule Requirements()
            {
                try
                {
                    if (!CompanyEnabled || !Settings.EconomyCompanyLicences)
                    {
                        return new CompanionModule("disabled");
                    }
                    CompanyDirector company = CompanyDirector.Instance;
                    string status = company != null ? company.CompanionPartStatus(CompanyPart.Licences) : "unavailable";
                    if (status != "ready")
                    {
                        return new CompanionModule(status);
                    }
                    CompanionModule module = CompanyGame.CompanionRequirements(company.Index);
                    var data = (Dictionary<string, object>)module.Data;
                    data["internetNeeds"] = Settings.Utilities ? InternetCatalogue.Needs : new string[0];
                    return module;
                }
                catch (Exception)
                {
                    return new CompanionModule("failed");
                }
            }

            private static CompanionModule Outside()
            {
                try
                {
                    List<object> rows = CompanyGame.CompanionOutside();
                    rows.AddRange(UtilitiesGame.CompanionOutside());
                    rows.AddRange(BusinessGame.CompanionOutside());
                    var labels = (List<object>)CompanyGame.CompanionTaxLabels();
                    labels.Add(UtilitiesGame.CompanionTaxLabel());
                    labels.Add(UtilitiesGame.CompanionCompanyTaxLabel());
                    labels.AddRange((List<object>)CityGame.CompanionTaxLabels());
                    var unclassified = new SortedSet<string>(StringComparer.Ordinal);
                    foreach (string[] names in new[] { Tech.FirmsGame.CompanionUnclassifiedTaxLabels(),
                        Tech.TalentGame.CompanionUnclassifiedTaxLabels(), Tech.InfraGame.CompanionUnclassifiedTaxLabels(),
                        OnlineGame.CompanionUnclassifiedTaxLabels(), Economy.Brands.BrandsGame.CompanionUnclassifiedTaxLabels(),
                        ServicesGame.CompanionUnclassifiedTaxLabels(), GameBooks.CompanionUnclassifiedTaxLabels(),
                        EstateGame.CompanionUnclassifiedTaxLabels(), Economy.Dealership.DealerGame.CompanionUnclassifiedTaxLabels(),
                        OwnBankGame.CompanionUnclassifiedTaxLabels(), CityGame.CompanionUnclassifiedTaxLabels() })
                    {
                        unclassified.UnionWith(names);
                    }
                    string taxStatus = !CompanyEnabled || !Settings.EconomyCompanyTax ? "disabled"
                        : CompanyDirector.Instance != null ? CompanyDirector.Instance.CompanionPartStatus(CompanyPart.Tax) : "unavailable";
                    string[] taxEffects = taxStatus == "disabled" ? new string[0] : taxStatus != "ready"
                        ? new[] { "company-tax-" + taxStatus } : new[] { "hidden-cash", "accountant-rebate", "audit-penalty", "backtax" };
                    return new CompanionModule("ready", CompanionSnapshot.Row("rows", rows, "coverage", "partial",
                        "knownOmittedCategories", new[] { "cover.excess", "other" },
                        "recurringDeductionLabels", labels, "unclassifiedDeductionLabels", new List<string>(unclassified), "balancesAreSeparate", true,
                        "taxEffectsOutsideNativeForecast", taxEffects));
                }
                catch (Exception)
                {
                    return new CompanionModule("failed");
                }
            }

            private static Dictionary<string, BuildingRegistration> Registrations()
            {
                var registrations = new Dictionary<string, BuildingRegistration>(StringComparer.Ordinal);
                foreach (BuildingRegistration reg in SaveGameManager.Current.BuildingRegistrations)
                {
                    if (reg != null && reg.HasValidAddress)
                    {
                        registrations[reg.StreetName + "\n" + reg.StreetNumber] = reg;
                    }
                }
                return registrations;
            }

            private static object Ownership(string operation, JArray targets)
            {
                try
                {
                    return Ownership(operation, targets, Registrations());
                }
                catch (Exception)
                {
                    return CompanionOwnership.Decision("unknown", "rlo", "request", "ownership-invalid-or-failed");
                }
            }

            private static object Ownership(string operation, JArray targets,
                Dictionary<string, BuildingRegistration> registrations)
            {
                try
                {
                    foreach (JObject target in targets)
                    {
                        string key = (string)target["street"] + "\n" + (int)target["number"];
                        if (!registrations.TryGetValue(key, out BuildingRegistration reg))
                        {
                            return CompanionOwnership.Decision("unknown", "rlo", operation, "target-not-found");
                        }
                        if (operation == "rent")
                        {
                            if (Settings.Economy && Settings.Estate)
                            {
                                object decision = EstateDirector.Instance != null ? EstateDirector.Instance.CompanionRentOwnership(reg)
                                    : CompanionOwnership.Decision("unknown", "rlo.estate", operation, "estate-state-unavailable");
                                if (decision != null)
                                {
                                    return decision;
                                }
                            }
                            continue;
                        }
                        if (!GoingPublic.Mine(reg))
                        {
                            return CompanionOwnership.Decision("refused", "rlo", operation, "target-not-owned");
                        }
                        if (ManagersEnabled)
                        {
                            object decision = CompanyDirector.Instance != null
                                ? CompanyDirector.Instance.CompanionOwnership(operation, target, reg)
                                : CompanionOwnership.Decision("unknown", "rlo.managers", operation, "manager-state-unavailable");
                            if (decision != null)
                            {
                                return decision;
                            }
                        }
                        if (target["week"] is JArray week && CompanyEnabled && Settings.EconomyCompanyLicences)
                        {
                            object decision = CompanyDirector.Instance != null
                                ? CompanyDirector.Instance.CompanionPermit(reg, week)
                                : CompanionOwnership.Decision("unknown", "rlo.licences", operation, "licence-state-unavailable");
                            if (decision != null)
                            {
                                return decision;
                            }
                        }
                    }
                    return CompanionOwnership.Decision("allowed", "rlo", operation, null);
                }
                catch (Exception)
                {
                    return CompanionOwnership.Decision("unknown", "rlo", "request", "ownership-invalid-or-failed");
                }
            }

            private static CompanionModule OwnershipSnapshot()
            {
                try
                {
                    Dictionary<string, BuildingRegistration> registrations = Registrations();
                    var rows = new List<object>();
                    foreach (BuildingRegistration reg in SaveGameManager.Current.BuildingRegistrations)
                    {
                        if (reg == null || !reg.HasValidAddress || !GoingPublic.Mine(reg))
                        {
                            continue;
                        }
                        var target = new JObject { ["street"] = reg.StreetName, ["number"] = reg.StreetNumber,
                            ["itemIds"] = new JArray() };
                        var decisions = new Dictionary<string, object>();
                        foreach (string operation in new[] { "schedule", "hire", "wholesale", "rent" })
                        {
                            decisions[operation] = Ownership(operation, new JArray(target), registrations);
                        }
                        rows.Add(CompanionSnapshot.Row("address", CompanionValues.Address(reg.StreetName, reg.StreetNumber),
                            "operations", decisions));
                    }
                    return new CompanionModule("ready", CompanionSnapshot.Row("targets", rows,
                        "authorization", "live-query-required", "managersEnabled", ManagersEnabled,
                        "estateEnabled", Settings.Economy && Settings.Estate));
                }
                catch (Exception)
                {
                    return new CompanionModule("failed");
                }
            }
        }
    }
}
