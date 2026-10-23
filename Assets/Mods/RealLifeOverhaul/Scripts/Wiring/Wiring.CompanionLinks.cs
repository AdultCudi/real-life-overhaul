using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The companion report's capabilities (Core/CompanionBridges), each read from the feature that owns it; the report
        /// never reads a feature's save line. Called only during a capture, on a save or a reader's call: nothing per frame.
        /// A fault changes no diagnostic latch; only that capability reads "failed".
        /// </summary>
        private static class CompanionLinks
        {
            internal static void Connect()
            {
                CompanionGame.Reset();
                PlanningCompanionLinks.Connect();
                CompanionBridges.Readers["features"] = Features;
                CompanionBridges.Readers["businesses"] = Businesses;
                CompanionBridges.Readers["employment"] = Employment;
                CompanionBridges.Readers["credit"] = Credit;
                CompanionBridges.Readers["accounting"] = Accounting;
            }

            internal static void Disconnect()
            {
                CompanionBridges.Readers.Clear();
                CompanionBridges.Ownership = null;
                CompanionGame.Reset();
            }

            private static bool CityEnabled => Settings.Economy && Settings.EconomyCity;
            private static bool CompanyEnabled => Settings.Economy && Settings.EconomyCompany;
            private static bool StaffEnabled => CompanyEnabled && Settings.EconomyCompanyStaff;
            private static bool ManagersEnabled => CompanyEnabled && Settings.EconomyManagers;

            private static object Feature(string id, bool enabled, bool running)
            {
                return CompanionSnapshot.Row("id", id, "enabled", enabled, "running", enabled && running);
            }

            private static CompanionModule Features()
            {
                try
                {
                    CompanyDirector company = CompanyDirector.Instance;
                    EconomyDirector economy = EconomyDirector.Instance;
                    return new CompanionModule("ready", new object[]
                    {
                        Feature("economy", Settings.Economy, economy != null && economy.Running),
                        Feature("city", CityEnabled, CityEconomy.Running),
                        Feature("company", CompanyEnabled, company != null && company.Running),
                        Feature("staff", StaffEnabled, company != null && company.On(CompanyPart.Staff)),
                        Feature("managers", ManagersEnabled, company != null && company.ManagersOn)
                    });
                }
                catch (Exception)
                {
                    return new CompanionModule("failed");
                }
            }

            private static CompanionModule Businesses()
            {
                try
                {
                    return CompanionGame.Businesses();
                }
                catch (Exception)
                {
                    return new CompanionModule("failed");
                }
            }

            private static CompanionModule Employment()
            {
                try
                {
                    CompanyDirector company = CompanyDirector.Instance;
                    if ((CityEnabled && !CityEconomy.Running)
                        || (StaffEnabled && (company == null || !company.On(CompanyPart.Staff))))
                    {
                        return new CompanionModule("unavailable");
                    }
                    return CompanyGame.CompanionEmployment(StaffEnabled ? company : null, CityEnabled ? Prices.Level : 1000);
                }
                catch (Exception)
                {
                    return new CompanionModule("failed");
                }
            }

            private static CompanionModule Credit()
            {
                try
                {
                    if (!Settings.Economy)
                    {
                        return new CompanionModule("disabled");
                    }
                    EconomyDirector economy = EconomyDirector.Instance;
                    if (economy == null || !economy.Running || economy.Desk == null || !economy.Desk.Running)
                    {
                        return new CompanionModule("unavailable");
                    }
                    return economy.Desk.CompanionCredit();
                }
                catch (Exception)
                {
                    return new CompanionModule("failed");
                }
            }

            private static CompanionModule Accounting()
            {
                try
                {
                    // GameBooks already rebuilds the game's statements. This declares their provenance, not full coverage
                    // of every RLO activity; account principal and hourly wage observations are never added to profit.
                    return new CompanionModule("ready", CompanionSnapshot.Row("source", "game.financialSummaries",
                        "coverage", "statement-backed-only", "adjustment", "none"));
                }
                catch (Exception)
                {
                    return new CompanionModule("failed");
                }
            }
        }
    }
}
