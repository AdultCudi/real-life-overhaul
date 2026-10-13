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
    /// Everything selling a business reads from or does to the game.
    ///
    /// READ: a business's recent profit (the investors' own figures of it, Ventures: the game's daily statements),
    /// its stock and furniture at the game's resale price (ItemHelper.GetSellingPrice), the deposit the landlord
    /// holds, its staff, its type and district.
    ///
    /// DONE, when a sale completes, in the order the game itself would do it:
    ///   the staff     with a buyer who keeps them, each is paid what they worked today and leaves the player's
    ///                 employ (EmployeeInstance.PayWage, RemoveEmployee: what the game's "fire" does). Otherwise they
    ///                 stay with the player, unassigned, as the game leaves the staff of a business that is shut
    ///   the business  BusinessHelper.ShutdownBusiness: the game's own "shut down this business" (its schedule,
    ///                 prices, campaigns, delivery contracts, logistics, headquarters plans, tasks; the staff taken
    ///                 off their shifts)
    ///   the lease     ended member by member exactly as the game's own "terminate contract" ends it
    ///                 (BizManPresentation.OnTerminateContractConfirm, which is private to that screen and tied to
    ///                 the business it is showing, so it cannot be called): the stock and furniture are taken out of
    ///                 the building (they went with the business: the price includes them, so they are not paid
    ///                 for a second time), the deposit comes back under the game's own transaction, the building is
    ///                 no longer rented by the player and is free to rent, its map marker and the guiders are updated,
    ///                 furniture and food deliveries to it are cancelled
    ///   the buyer     where the game has one of its own businesses of that kind for a building of that size
    ///                 (CompetitionHelper.GetBusinessDefaultsByType, BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet),
    ///                 it is opened there by the game's own function for a rival's new business
    ///                 (CompetitionHelper.StartNewCompetitorBusiness). Otherwise the building stays free to rent. A
    ///                 building the player owns stays theirs and is not given a business.
    /// </summary>
    internal static partial class ServicesGame
    {
        private const string DepositKey = "ba:transaction_depositreturn";   // the game's own, for a deposit that comes back
        private const string Residential = "ba:buildingtype_residential", Warehouse = "ba:buildingtype_warehouse";

        private static readonly List<ItemInstance> Items = new List<ItemInstance>(64);
        private static readonly List<EmployeeInstance> Staff = new List<EmployeeInstance>(16);

        /// <summary>The player's businesses that can be sold: the ones that take money (the Finance app's own test).</summary>
        public static void Sellable(List<BuildingRegistration> into)
        {
            Books.Businesses(into);
        }

        public static bool IsSellable(BuildingRegistration reg)
        {
            return reg != null && Books.IsBusiness(reg);
        }

        /// <summary>What a business's value is worked out from, as it stands now.</summary>
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
                        // an item whose worth cannot be read is counted as worth nothing
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

        /// <summary>
        /// What stands in the way of a sale completing now. The game does not let a lease end while the player
        /// stands in the building (BizManPresentation.TerminateContract); a vehicle kept at the building would be
        /// sold with it, and a special gift lost, so the player moves those first.
        /// </summary>
        public static SaleBlock Blocked(BuildingRegistration reg)
        {
            if (!IsSellable(reg) || reg.Address == null)
            {
                return SaleBlock.NotABusiness;
            }
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || UI.Load.LoadScene.isLoading || FuneralHelper.PlayerDead || EnergyHelper.goingToHospital || (uis.timeMachine != null && uis.timeMachine.isRunning))
            {
                return SaleBlock.Busy;   // the game is in the middle of something of its own: a load, a time skip, the way to hospital
            }
            Address address = reg.Address;
            if (BuildingManager.IsInsideBuilding && InstanceBehavior<BuildingManager>.Instance.building != null && InstanceBehavior<BuildingManager>.Instance.building.Address == address)
            {
                return SaleBlock.Inside;
            }
            // An Empire Motors dealership's cars are its stock, which only that mod can sell or take back: a sale would leave
            // them and its records of them behind (Core/EmpireMotors). Its only dealership's stock is its own wherever it stands.
            // One of this mod's own dealerships: its cars are its stock too, sold or sent to the trade from the Dealership app first
            // (Economy/Dealer): they cannot go with a sale.
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

        /// <summary>The day the player took a business on (BuildingRegistration.creationDay, written by AddToPlayer and when a business is started).</summary>
        public static int Created(BuildingRegistration reg)
        {
            return reg != null ? reg.creationDay : -1;
        }

        /// <summary>
        /// Ends the player's hold on a business and its building, as the summary above lists it. The money of the sale
        /// is not moved here. Returns what became of the building; 'staffGone' is how many left with it, 'deposit'
        /// what came back from the landlord, 'log' one line of what was done for Player.log.
        /// </summary>
        public static SaleAfter Complete(BuildingRegistration reg, bool keepsStaff, out int staffGone, out int deposit, out string log)
        {
            Address address = reg.Address;
            Building building = reg.BuildingCached;
            string name = reg.BusinessName, type = reg.businessTypeName;
            bool owned = reg.BuildingOwnedByPlayer;
            staffGone = 0;

            // The staff. (The list is copied: leaving changes the game's own.)
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

            // The business: the game's own shut-down, and the logo it made for the name (as BizManSettings.ShutdownBusiness does).
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

            // The lease, as BizManPresentation.OnTerminateContractConfirm ends one.
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
            // (The game's code also looks whether the player still has a home, which a business never is: left out.)
            reg.temporarilyClosed = false;   // a closure was the player's own: it does not go on to whoever has the building next (written as a field: the game's TemporarilyClose is for a business of the player's and pays its licensing fees)

            // The buyer moves in, where the game has a business of that kind for a building of this size.
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

            // What shows the building: its marker on the map, the guiders, and whoever listens for a change of registration.
            try
            {
                CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
                CityBuildingController controller = city != null ? city.FindCityBuildingController(address) : null;
                if (controller != null)
                {
                    controller.UpdatePoi();
                }
                UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
                if (uis != null && uis.mapFilters != null)
                {
                    uis.mapFilters.ApplyFilters();
                }
                GuidersManager.UpdateGuidersWithAddress(address);
                GlobalEvents.onBuildingRegistrationChange?.Invoke(address);
            }
            catch (Exception e)
            {
                Logger?.Warn("Services: the map could not be told of the sale at " + address.ToFormattedString() + " (" + e.Message + "); it shows the change after the next load.");
            }
            log = "'" + name + "' at " + address.ToFormattedString() + ": " + staff + " on the staff, " + (keepsStaff ? staffGone + " left with the business (paid for today, then off the payroll)" : "all unassigned and still employed")
                  + "; the game's own shut-down run; " + items + " item(s) of stock and furniture taken out of the building; deposit " + deposit + " back under the game's own transaction; the building "
                  + (after == SaleAfter.Rival ? "is now '" + opened + "', a business of the game's own" : after == SaleAfter.Owned ? "stays the player's property, with no business in it" : "is free to rent");
            return after;
        }

        /// <summary>The name of the business of the game's own that would move in after a sale; null if the building would be free to rent or stay the player's. Changes nothing.</summary>
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

        // One of the game's own businesses of the same kind whose layout is made for a building of this size (the game's own test of a fit: CompetitionHelper.GetBuildingMatchingLayout); null if there is none.
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

        // That business, opened as the game opens a rival's new business. Null, and nothing changed, if there is none or no owner for it.
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
