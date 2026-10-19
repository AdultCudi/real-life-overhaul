using System;
using System.Collections.Generic;
using System.IO;
using BigAmbitions.Items;
using BigAmbitions.Rivals;
using Blueprints;
using Buildings;
using Buildings.BuildingTypes.Special.FoodDelivery;
using Buildings.Office.Headquarters;
using BusinessLayoutSets;
using Entities;
using Helpers;
using RealLifeOverhaul.Economy.Ventures;
using Streets;
using UI.Guiders;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What stands in the way of a sale completing now.</summary>
    public enum SaleBlock { None, NotABusiness, Inside, Vehicle, Gift, Busy, Stock, Lot }

    /// <summary>What became of the building after a sale.</summary>
    public enum SaleAfter { ToRent, Rival, Owned }

    /// <summary>
    /// What selling a business reads from and does to the game. A sale ends the business as the game would: the staff,
    /// the game's shut-down and the lease (as BizManPresentation.OnTerminateContractConfirm, private to its screen); where
    /// the game has a business of that kind for the building, it opens there as a rival's.
    /// </summary>
    internal static partial class ServicesGame
    {
        private const string DepositKey = "ba:transaction_depositreturn";   // the game's own, for a deposit that comes back
        private const string Residential = "ba:buildingtype_residential", Warehouse = "ba:buildingtype_warehouse";

        private static readonly List<ItemInstance> Items = new List<ItemInstance>(64);
        private static readonly List<EmployeeInstance> Staff = new List<EmployeeInstance>(16);

        /// <summary>The player's businesses that can be sold: those that take money (the Finance app's own test).</summary>
        public static void Sellable(List<BuildingRegistration> into)
        {
            Books.Businesses(into);
        }

        public static bool IsSellable(BuildingRegistration reg) => reg != null && Books.IsBusiness(reg);

        public static SaleInputs Inputs(BuildingRegistration reg)
        {
            var i = new SaleInputs { Income = 100, Phase = Phase };
            if (!IsSellable(reg))
            {
                return i;
            }
            BusinessFigures b = VenturesGame.Figures(reg);
            i.AvgProfit7 = b.AvgProfit7;
            i.DaysOnRecord = b.DaysOnRecord;
            i.Sector = (int)b.Sector;
            i.Income = Mathf.RoundToInt(CityEconomy.DistrictIncome(reg.Neighborhood));
            i.Fixtures = Fixtures(reg);
            i.Deposit = Mathf.Max(0, Mathf.FloorToInt(reg.lastDeposit));
            i.Investors = ServicesBridges.InvestorsOwed != null ? Mathf.Max(0, ServicesBridges.InvestorsOwed(reg)) : 0;
            i.Loans = ServicesBridges.LoansOwed != null ? Mathf.Max(0, ServicesBridges.LoansOwed(reg.Address)) : 0;
            return i;
        }

        /// <summary>A business's stock and furniture at the price the game itself pays for them when a lease ends.</summary>
        public static int Fixtures(BuildingRegistration reg)
        {
            float sum = 0f;
            if (reg != null && reg.itemInstances != null)
            {
                foreach (ItemInstance item in reg.itemInstances.Values)
                {
                    try
                    {
                        sum += item != null ? item.GetSellingPrice() : 0f;
                    }
                    catch (Exception)
                    {
                        // an item whose worth cannot be read counts as nothing
                    }
                }
            }
            return Mathf.FloorToInt(Mathf.Min(sum, int.MaxValue / 4));
        }

        public static int StaffAt(BuildingRegistration reg)
        {
            if (reg == null || reg.Address == null)
            {
                return 0;
            }
            int count = EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { withAssignedAddress = reg.Address }, Staff).Count;
            Staff.Clear();
            return count;
        }

        /// <summary>What stands in the way of a sale now: the game ends no lease with the player inside, and a vehicle or gift kept there would go too.</summary>
        public static SaleBlock Blocked(BuildingRegistration reg)
        {
            if (!IsSellable(reg) || reg.Address == null)
            {
                return SaleBlock.NotABusiness;
            }
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || UI.Load.LoadScene.isLoading || GameMembers.PlayerDead || EnergyHelper.goingToHospital || (uis.timeMachine != null && uis.timeMachine.isRunning))
            {
                return SaleBlock.Busy;   // the game is in the middle of something of its own: a load, a time skip, the way to hospital
            }
            Address address = reg.Address;
            if (BuildingManager.IsInsideBuilding && InstanceBehavior<BuildingManager>.Instance.building != null && InstanceBehavior<BuildingManager>.Instance.building.Address == address)
            {
                return SaleBlock.Inside;
            }
            // A dealership's cars are its stock and cannot go with a sale: Empire Motors' (all, while it has one) and this mod's own.
            bool dealership = EmpireMotors.IsDealership(reg), only = dealership && EmpireMotors.DealershipCount() == 1, own = Dealership.DealerType.Is(reg);
            List<VehicleInstance> vehicles = Save.VehicleInstances;
            for (int i = 0; vehicles != null && i < vehicles.Count; i++)
            {
                if (own && vehicles[i] != null && vehicles[i].Address == address && Dealerships.Own(vehicles[i]))
                {
                    return SaleBlock.Lot;
                }
                if (vehicles[i] != null && (vehicles[i].Address == address || only) && dealership && EmpireMotors.Stock(vehicles[i]))
                {
                    return SaleBlock.Stock;
                }
                if (vehicles[i] != null && vehicles[i].Address == address)
                {
                    return SaleBlock.Vehicle;
                }
            }
            try
            {
                if (ItemHelper.AreThereSpecialGiftsInAddress(address))
                {
                    return SaleBlock.Gift;
                }
            }
            catch (Exception)
            {
                // a building whose items cannot be read has no gift that can be told
            }
            return SaleBlock.None;
        }

        /// <summary>The game's type and district of a business, for a clause.</summary>
        public static void Trade(BuildingRegistration reg, out string type, out string district)
        {
            type = reg != null ? reg.businessTypeName ?? string.Empty : string.Empty;
            district = string.Empty;
            try
            {
                district = reg != null ? reg.Neighborhood ?? string.Empty : string.Empty;
            }
            catch (Exception)
            {
                // a building that cannot be read is in no district
            }
        }

        /// <summary>The day the player took a business on (BuildingRegistration.creationDay).</summary>
        public static int Created(BuildingRegistration reg) => reg != null ? reg.creationDay : -1;

        /// <summary>Ends the player's hold on a business and its building; the caller moves the sale's money. 'log' is a line for Player.log.</summary>
        public static SaleAfter Complete(BuildingRegistration reg, bool keepsStaff, out int deposit, out string log)
        {
            Address address = reg.Address;
            Building building = reg.BuildingCached;
            string name = reg.BusinessName, type = reg.businessTypeName;
            bool owned = reg.BuildingOwnedByPlayer;
            int staffGone = 0;

            // Staff is a copy: somebody leaving changes the game's own list.
            EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { withAssignedAddress = address }, Staff);
            int staff = Staff.Count;
            for (int i = 0; keepsStaff && i < Staff.Count; i++)
            {
                EmployeeInstance e = Staff[i];
                if (e != null && !e.isBeingReplaced && !ReferenceEquals(e, Self()))
                {
                    e.PayWage(reg);
                    e.RemoveEmployee();
                    staffGone++;
                }
            }
            Staff.Clear();
            if (staffGone > 0)
            {
                RivalOfferGuard.Look("staff gone with a business sold");   // a rival's offer to them goes now
            }

            // The game's own shut-down, and the logo it made for the name (as BizManSettings.ShutdownBusiness).
            try
            {
                string logo = LogoHelper.GetPlayerBusinessLogoPath(name);
                if (!string.IsNullOrEmpty(logo) && Directory.Exists(logo))
                {
                    Directory.Delete(logo, true);
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Services: the logo folder of '" + name + "' could not be removed (" + e.Message + "); the sale goes on.");
            }
            BusinessHelper.ShutdownBusiness(reg);

            // The lease, as OnTerminateContractConfirm ends one; the stock and furniture go with the business, whose price includes them.
            Items.Clear();
            Items.AddRange(reg.itemInstances.Values);
            int items = Items.Count;
            for (int i = 0; i < Items.Count; i++)
            {
                reg.RemoveItemInstanceFromBuilding(Items[i]);
            }
            Items.Clear();
            deposit = Mathf.Max(0, Mathf.FloorToInt(reg.lastDeposit));
            reg.BusinessName = null;
            reg.RentedByPlayer = false;
            reg.AvailableForRent = true;
            reg.takenOver = false;
            if (building != null && building.BuildingType == Warehouse)
            {
                PurchasingAgentHelper.CancelPlansThatDeliverToAddress(address);
            }
            if (building == null || building.BuildingType != Residential)
            {
                reg.businessTypeName = EmptyType;
            }
            if (deposit > 0)
            {
                GameManager.ChangeMoneySafe(deposit, new TransactionInfo(DepositKey, new Dictionary<string, string>(1) { { "address", address.ToFormattedString() } }), null, address);
            }
            Save.FurnitureDeliveryContracts.RemoveAll(x => x.toAddress == address);
            FoodDeliveryHelper.RemoveContractsForAddress(address);
            // The game's own also checks that the player still has a home, which a business never is.
            reg.temporarilyClosed = false;   // the player's closure does not pass to the next tenant; TemporarilyClose would pay licensing fees

            SaleAfter after = owned ? SaleAfter.Owned : SaleAfter.ToRent;
            string opened = null;
            if (!owned && building != null)
            {
                try
                {
                    opened = Reopen(reg, building, type);
                    after = opened != null ? SaleAfter.Rival : SaleAfter.ToRent;
                }
                catch (Exception e)
                {
                    Logger?.Warn("Services: the buyer's business could not be opened at " + address.ToFormattedString() + " (" + e.Message + "); the building is free to rent.");
                }
            }

            try
            {
                CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
                CityBuildingController controller = city != null ? city.FindCityBuildingController(address) : null;
                if (controller != null)
                {
                    controller.UpdatePoi();
                }
                UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
                if (uis != null)
                {
                    MapPlaces.Refilter(uis.mapFilters);
                }
                GuidersManager.UpdateGuidersWithAddress(address);
                GlobalEvents.onBuildingRegistrationChange?.Invoke(address);
            }
            catch (Exception e)
            {
                Logger?.Warn("Services: the map could not be told of the sale at " + address.ToFormattedString() + " (" + e.Message + "); it shows the change after the next load.");
            }
            log = "'" + name + "' at " + address.ToFormattedString() + ": " + staff + " on the staff, "
                  + (keepsStaff ? staffGone + " left with the business (paid for today, then off the payroll)" : "all unassigned and still employed")
                  + "; the game's own shut-down run; " + items + " item(s) of stock and furniture taken out of the building; deposit " + deposit
                  + " back under the game's own transaction; the building "
                  + (after == SaleAfter.Rival ? "is now '" + opened + "', a business of the game's own"
                      : after == SaleAfter.Owned ? "stays the player's property, with no business in it" : "is free to rent");
            return after;
        }

        /// <summary>The name of the game's own business that would move in after a sale; null if the building would be free to rent or stay the player's.</summary>
        public static string BuyerFor(BuildingRegistration reg)
        {
            try
            {
                Building building = reg != null ? reg.BuildingCached : null;
                AiBusinessDefault def = building != null && !reg.BuildingOwnedByPlayer ? Default(reg, building, reg.businessTypeName) : null;
                return def != null ? def.businessName : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // A business of the game's own of this kind with a layout for a building of this size (CompetitionHelper.GetBuildingMatchingLayout).
        private static AiBusinessDefault Default(BuildingRegistration reg, Building building, string type)
        {
            if (string.IsNullOrEmpty(type) || type == EmptyType || RivalsHelper.IgnoredAddresses.Contains(reg.Address))
            {
                return null;
            }
            AiBusinessDefault[] defaults = CompetitionHelper.GetBusinessDefaultsByType(type);
            var size = new BuildingSizeInfo(building);
            for (int i = 0; defaults != null && i < defaults.Length; i++)
            {
                AiBusinessDefault def = defaults[i];
                if (def != null && !string.IsNullOrEmpty(def.buildingLayout) && BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(type, size, def.buildingLayout, false) != null)
                {
                    return def;
                }
            }
            return null;
        }

        // That business, opened as the game opens a rival's new business; null, and nothing changed, if there is none or no owner for it.
        private static string Reopen(BuildingRegistration reg, Building building, string type)
        {
            AiBusinessDefault def = Default(reg, building, type);
            string rival = def != null ? CompetitionHelper.GetRivalIdForBusinessDefault(def) : null;
            if (string.IsNullOrEmpty(rival))
            {
                return null;
            }
            CompetitionHelper.StartNewCompetitorBusiness(type, reg, true, def, rival);
            return def.businessName;
        }
    }
}
