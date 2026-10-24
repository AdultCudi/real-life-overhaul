using System.Collections.Generic;
using Buildings;
using Helpers;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What the infrastructure part reads from or does to the game: the player's warehouses, money as dated transactions at the
    /// data centre, and the racks' writing down as a deductible expense with no money moving.
    /// </summary>
    internal static class InfraGame
    {
        /// <summary>Owner deduction labels whose mixed costs have no single portable recurrence rule.</summary>
        internal static string[] CompanionUnclassifiedTaxLabels() => new[] { "reallife_tech_deduct".GetLocalization(), WriteDownDeduct.GetLocalization() };

        public const string Warehouse = "ba:businesstype_warehouse";
        public const string WarehouseBuilding = "ba:buildingtype_warehouse";
        public const string TakingsKey = "reallife_transaction_tech_cloud_takings";
        public const string PowerKey = "reallife_transaction_tech_util_power";
        public const string WaterKey = "reallife_transaction_tech_util_water";
        public const string RacksKey = "reallife_transaction_tech_investment_racks";
        public const string FitKey = "reallife_transaction_tech_investment_fitout";
        public const string LicenceKey = "reallife_transaction_tech_cloud_licence_fee";
        public const string ComputeKey = "reallife_transaction_tech_compute";
        public const string WriteDownKey = "reallife_transaction_tech_write_down";
        public const string UpkeepKey = "reallife_transaction_tech_upkeep";          // at the end game's scale: racks' support and the building's upkeep and insurance
        public const string CrewsKey = "reallife_transaction_tech_crew_wages";       // ...the data centres' crews and the cloud's operations people
        private const string WriteDownDeduct = "reallife_tech_write_down_deduct";

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>The player's warehouses, one pass over the buildings: what can be fitted out as a data centre.</summary>
        public static void Warehouses(List<BuildingRegistration> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && GoingPublic.Mine(reg) && reg.HasValidAddress && reg.GetBuildingType() == WarehouseBuilding)
                {
                    into.Add(reg);
                }
            }
        }

        public static int Sqm(BuildingRegistration reg)
        {
            Building b = reg != null ? BuildingHelper.GetBuilding(reg.Address) : null;
            return b != null ? Mathf.Max(0, b.totalSqm) : 0;
        }

        public static string District(BuildingRegistration reg) => reg != null && reg.Neighborhood != null ? reg.Neighborhood : string.Empty;

        /// <summary>A charge the player chose (racks, fitting out, a licence), cents: refused if the cash does not cover it.</summary>
        public static bool Pay(long cents, string key, BuildingRegistration at, string name, bool deductible)
        {
            if (cents <= 0)
            {
                return true;
            }
            var info = new TransactionInfo(key, new Dictionary<string, string>(2) { { "name", name ?? string.Empty } });
            if (deductible)
            {
                info.SetTaxDeductibleName("reallife_tech_deduct".GetLocalization());
            }
            return Save != null && GameManager.ChangeMoneySafe(-cents / 100f, info, null, at != null ? at.Address : null, false);
        }

        /// <summary>The racks' writing down for the game's tax: a deductible expense with no money moving.</summary>
        public static void WriteDown(long cents)
        {
            if (cents <= 0)
            {
                return;
            }
            var info = new TransactionInfo(WriteDownKey);
            info.SetTaxDeductibleName(WriteDownDeduct.GetLocalization());
            TaxHelper.TrackTransaction(new Transaction(info) { amount = -cents / 100f });
        }
    }
}
