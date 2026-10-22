using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>Reporting bridges. Feature owners supply facts; the companion layer never reads their save formats.</summary>
        private static class CompanionLinks
        {
            internal static void Connect()
            {
                CompanionGame.Reset();
                CompanionBridges.Readers["features"] = Features;
                CompanionBridges.Readers["businesses"] = CompanionGame.Businesses;
                CompanionBridges.Readers["employment"] = Employment;
                CompanionBridges.Readers["credit"] = Credit;
                CompanionBridges.Readers["accounting"] = Accounting;
            }

            internal static void Disconnect()
            {
                CompanionBridges.Readers.Clear();
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
                CompanyDirector company = CompanyDirector.Instance;
                EconomyDirector economy = EconomyDirector.Instance;
                return new CompanionModule("ready", new object[]
                {
                    Feature("economy", Settings.Economy, economy != null && economy.Running),
                    Feature("city", CityEnabled, CityEconomy.Running),
                    Feature("company", CompanyEnabled, company != null && company.Running),
                    Feature("staff", StaffEnabled,
                        company != null && company.On(CompanyPart.Staff)),
                    Feature("managers", ManagersEnabled,
                        company != null && company.ManagersOn)
                });
            }

            private static CompanionModule Employment()
            {
                CompanyDirector company = CompanyDirector.Instance;
                if ((CityEnabled && !CityEconomy.Running)
                    || (StaffEnabled && (company == null || !company.On(CompanyPart.Staff))))
                {
                    return new CompanionModule("unavailable");
                }
                return CompanyGame.CompanionEmployment(StaffEnabled ? company : null, CityEnabled ? Prices.Level : 1000);
            }

            private static CompanionModule Credit()
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

            private static CompanionModule Accounting()
            {
                // GameBooks already rebuilds the game's statements. This declares their provenance, not full coverage
                // of every RLO activity; account principal and hourly wage observations are never added to profit.
                return new CompanionModule("ready", CompanionSnapshot.Row("source", "game.financialSummaries",
                    "coverage", "statement-backed-only", "adjustment", "none"));
            }
        }
    }
}
