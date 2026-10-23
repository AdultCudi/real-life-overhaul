using System;
using System.Collections.Generic;
using AI.Employees.SalaryNegotiation;
using BigAmbitions.Items;
using BigAmbitions.Neighborhoods;
using BigAmbitions.Rivals;
using Buildings.Office.Headquarters;
using Entities;
using Helpers;
using Player.FoodDeliveryJob;
using Player.PlayerMissions;
using UnityEngine;
using Vehicles;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// The load: every record the mod's section kept goes back where it stood once the game knows every place it names
    /// (its building in the game's address lookup, its district in the game's list): a whole record into its list, an
    /// element into its record of the game's, a stand-in's real value into its field. A load runs two passes: when the
    /// save has been read, before the city's scenes are built (for the places whose buildings a feature put into the
    /// game's lookup then), and at the city load (for the places made with the city). A record whose places are still
    /// not made after the last pass stays kept for a later load; a stand-in whose place is not made then stands.
    /// COST: one step a record at a load; nothing else.
    /// </summary>
    internal static partial class DistrictSaveGame
    {
        private static readonly Func<string, int, bool> AddressMade = (street, number) => BuildingHelper.GetBuilding(new Address(street, number)) != null;
        private static readonly Func<string, bool> DistrictMade = district => NeighborhoodHelper.Neighborhoods != null && NeighborhoodHelper.Neighborhoods.Contains(district);

        /// <summary>What one pass did with the records it was given.</summary>
        internal sealed class Return
        {
            public int Back;
            public int Dropped;
            public readonly List<StoredRecord> KeptRecords = new List<StoredRecord>();
            public readonly List<object> KeptItems = new List<object>();
            public readonly List<string> Faults = new List<string>(0);
            public readonly List<BuildingRegistration> Registrations = new List<BuildingRegistration>(0);
            public readonly List<EmployeeInstance> People = new List<EmployeeInstance>(0);

            public void Keep(StoredRecord record, object item)
            {
                KeptRecords.Add(record);
                KeptItems.Add(item);
            }
        }

        /// <summary>A line's records with each one's item from the payload beside it, appended to the two lists.</summary>
        public static void Aligned(StoredLine line, object[] items, List<StoredRecord> records, List<object> aligned)
        {
            for (int i = 0; i < line.Records.Count; i++)
            {
                StoredRecord record = line.Records[i];
                records.Add(record);
                aligned.Add(record.Slot >= 0 && items != null && record.Slot < items.Length ? items[record.Slot] : null);
            }
        }

        /// <summary>
        /// Puts back, in their order (each list's records in the order they were written), the records whose places the
        /// game knows now; the rest are kept. On the <paramref name="last"/> pass of a load a stand-in whose place is not
        /// made is let go, its stand-in standing.
        /// </summary>
        public static Return Restore(GameInstance save, List<StoredRecord> records, List<object> items, PlaceSet places, bool last)
        {
            var r = new Return();
            for (int i = 0; i < records.Count; i++)
            {
                StoredRecord record = records[i];
                object item = i < items.Count ? items[i] : null;
                bool stands = record.Known && DistrictSaveRules.Way(record.Kind) == SaveWay.StandsIn;
                if (!record.Known || !DistrictSaveStore.PlacesMade(record, AddressMade, DistrictMade) || !GroundMade(record, places))
                {
                    Settle(r, record, item, stands && last);
                    continue;
                }
                Action undo;
                try
                {
                    undo = Put(save, record, item, r);
                }
                catch (Exception e)
                {
                    r.Faults.Add(record.Code + " " + record.Index + ": " + e.GetType().Name + ": " + e.Message);
                    undo = null;
                }
                if (ReferenceEquals(undo, Twice))
                {
                    r.Dropped++;
                    r.Faults.Add(record.Code + " " + record.Index + ": the game's part of the save has its own now, so the kept one is let go");
                    continue;
                }
                if (undo == null)
                {
                    Settle(r, record, item, stands && last);
                    continue;
                }
                r.Back++;
            }
            Addresses(r.Registrations);
            return r;
        }

        // A record that cannot go back now: kept for the next pass or a later load; a stand-in on the last pass stands.
        private static void Settle(Return r, StoredRecord record, object item, bool letGo)
        {
            if (letGo)
            {
                r.Dropped++;
            }
            else
            {
                r.Keep(record, item);
            }
        }

        // A spot of the mod's ground goes back only while that ground is the mod's this session.
        private static bool GroundMade(StoredRecord record, PlaceSet places)
        {
            bool spot = record.Kind == SaveKind.Player || record.Kind == SaveKind.Vehicle || record.Kind == SaveKind.DriverVehicle;
            if (!spot || record.Values.Count < 10 || record.Values[9] != Ground)
            {
                return true;
            }
            return places.OnGround(DistrictSaveRules.ReadNumber(record.Values[2]), DistrictSaveRules.ReadNumber(record.Values[4]));
        }

        /// <summary>
        /// After a pass at the city load (the game had built its lookups from the save's lists as the scenes loaded): the
        /// staff into the game's lookup by id, each business's customers drawn again (through Core/CustomerDraw), the
        /// rivals' lists.
        /// </summary>
        public static void Refresh(Return r)
        {
            for (int i = 0; i < r.People.Count; i++)
            {
                EmployeeInstance e = r.People[i];
                if (e != null && !string.IsNullOrEmpty(e.id))
                {
                    EmployeeHelper.EmployeeInstancesDictionary[e.id] = e;
                }
            }
            for (int i = 0; i < r.Registrations.Count; i++)
            {
                CustomerDraw.Redraw(r.Registrations[i]);   // the mod's one way to the game's redraw of a business's customers
            }
            if (r.Registrations.Count > 0)
            {
                Rivals();
            }
        }

        // The rivals' lists of what they own, made again from the registrations as the game makes them at a load.
        private static void Rivals()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.rivalStates == null)
            {
                return;
            }
            var ids = new List<string>(save.rivalStates.Count);
            for (int i = 0; i < save.rivalStates.Count; i++)
            {
                if (save.rivalStates[i] != null)
                {
                    ids.Add(save.rivalStates[i].rivalId);
                }
            }
            RivalsHelper.FillData(ids);
        }

        // Items know their building by address, which the game sets from the registrations once at a load (ItemHelper.Init).
        private static void Addresses(List<BuildingRegistration> regs)
        {
            for (int i = 0; i < regs.Count; i++)
            {
                BuildingRegistration reg = regs[i];
                if (reg.itemInstances == null)
                {
                    continue;
                }
                foreach (ItemInstance item in reg.itemInstances.Values)
                {
                    if (item != null)
                    {
                        item.streetName = reg.StreetName;
                        item.streetNumber = reg.StreetNumber;
                    }
                }
            }
        }

        // What Put answers for a record whose like the game's part already holds (the same address, the same id): it is let
        // go, as two of one would break the game; the save was written without the mod's step, or the game made its own.
        private static readonly Action Twice = () => { };

        // One record into the game: the undo that takes it out again, null when it has nowhere to go, or Twice.
        private static Action Put(GameInstance save, StoredRecord record, object item, Return r)
        {
            switch (record.Kind)
            {
                case SaveKind.Registration:
                {
                    if (!(item is BuildingRegistration reg))
                    {
                        return null;
                    }
                    if (Registration(save, KeyOf(reg.StreetName, reg.StreetNumber)) != null)
                    {
                        return Twice;
                    }
                    r.Registrations.Add(reg);
                    return Insert(save.BuildingRegistrations, record.Index, item);
                }
                case SaveKind.Job: return Insert(save.JobInstances, record.Index, item);
                case SaveKind.DistrictStats:
                    if (item is NeighbourhoodStats stats && ById(save.NeighbourhoodStats, stats.name, n => n.name) != null)
                    {
                        return Twice;
                    }
                    return Insert<NeighbourhoodStats>(save.NeighbourhoodStats, record.Index, item);
                case SaveKind.Employee:
                case SaveKind.Candidate:
                {
                    if (!(item is EmployeeInstance person))
                    {
                        return null;
                    }
                    if (ById(save.EmployeeInstances, person.id, e => e.id) != null || ById(save.CandidateEmployeeInstances, person.id, e => e.id) != null)
                    {
                        return Twice;
                    }
                    r.People.Add(person);
                    return Insert(record.Kind == SaveKind.Employee ? save.EmployeeInstances : save.CandidateEmployeeInstances, record.Index, item);
                }
                case SaveKind.Loan: return Insert(save.Loans, record.Index, item);
                case SaveKind.Transaction: return Enqueue(save.Transactions, record.Index, item);
                case SaveKind.Recruitment: return Insert(save.RecruitmentCampaigns, record.Index, item);
                case SaveKind.Delivery: return Insert(save.DeliveryContracts, record.Index, item);
                case SaveKind.FurnitureDelivery: return Insert(save.FurnitureDeliveryContracts, record.Index, item);
                case SaveKind.FoodDelivery: return Insert(save.FoodDeliveryContracts, record.Index, item);
                case SaveKind.Task: return Once<TodoTask>(save.TodoTasks, record.Index, item, x => x.id);
                case SaveKind.MarketEvent: return Insert(save.marketEvents, record.Index, item);
                case SaveKind.ForSale: return Insert(save.buildingsForSale, record.Index, item);
                case SaveKind.Deed: return Insert(save.realEstate, record.Index, item);
                case SaveKind.Negotiation: return Once<CandidateSalaryNegotiation>(save.candidateSalaryNegotiations, record.Index, item, x => x.id);
                case SaveKind.Installation: return Insert(save.interiorInstallationFirmContracts, record.Index, item);
                case SaveKind.Moving: return Insert(save.movingServiceContracts, record.Index, item);
                case SaveKind.Import: return Once<ImportPartnership>(save.importPartnerships, record.Index, item, x => x.id);
                case SaveKind.ImporterOrder: return Order(save, record, item);
                case SaveKind.LogisticsPlan: return Once<LogisticsManagerPlan>(save.logisticsManagerPlans, record.Index, item, x => x.id);
                case SaveKind.HeadhunterPlan: return Once<HeadhunterPlan>(save.headhunterPlans, record.Index, item, x => x.id);
                case SaveKind.HrPlan: return Once<HrManagerPlan>(save.hrManagerPlans, record.Index, item, x => x.id);
                case SaveKind.PricingPlan: return Once<PricingManagerPlan>(save.pricingManagerPlans, record.Index, item, x => x.id);
                case SaveKind.FoodOffer: return Insert<FoodDeliveryOffer>(save.foodDeliveryOffers, record.Index, item);
                case SaveKind.DeliveryDone: return Insert(save.deliveryJobLocationsDoneToday, record.Index, item);
                case SaveKind.VehicleDelivery: return Insert<VehicleDeliveryContract>(save.vehicleDeliveryContracts, record.Index, item);
                case SaveKind.FeeOff: return Insert(save.disabledLicensingFees, record.Index, item);
                case SaveKind.FeePaid: return Paid(save, record.Index, item);
                case SaveKind.InsuranceOffer: return Once<HealthInsurancePlanOffer>(save.healthInsurancePlanOffers, record.Index, item, x => x.id);
                default:
                    return DistrictSaveRules.Way(record.Kind) == SaveWay.LeavesFromGame ? Into(save, record, item) : Stand(save, record, item);
            }
        }

        // An element into its record of the game's.
        private static Action Into(GameInstance save, StoredRecord record, object item)
        {
            switch (record.Kind)
            {
                case SaveKind.Campaign: return Insert(Registration(save, record.Parent)?.marketingCampaigns, record.Index, item);
                case SaveKind.AiEmployee: return Insert(Registration(save, record.Parent)?.aiEmployees, record.Index, item);
                case SaveKind.Poached: return Insert(Registration(save, record.Parent)?.poachedEmployees, record.Index, item);
                case SaveKind.Shift: return Insert(Day(save, record.Parent)?.workShifts, record.Index, item);
                case SaveKind.BusinessStatement: return Insert(Summary(save, record.Parent)?.businessIncomeStatements, record.Index, item);
                case SaveKind.HomeStatement: return Insert(Summary(save, record.Parent)?.residentialStatements, record.Index, item);
                case SaveKind.EstateStatement: return Insert(Summary(save, record.Parent)?.realEstateStatements, record.Index, item);
                case SaveKind.Demand: return Insert(Market(save, record.Parent)?.demandValues, record.Index, item);
                case SaveKind.StorePrice: return Insert(ById(save.pricingManagerPlans, record.Parent, p => p.id)?.originalStorePrices, record.Index, item);
                case SaveKind.Destination: return Insert(ById(save.logisticsManagerPlans, record.Parent, p => p.id)?.destinations, record.Index, item);
                case SaveKind.ImportProduct: return Insert(ById(save.importPartnerships, record.Parent, p => p.id)?.products, record.Index, item);
                case SaveKind.Replacement: return Insert(ById(save.headhunterPlans, record.Parent, p => p.id)?.headhunterReplacementDataList, record.Index, item);
                case SaveKind.HrMember: return Insert(ById(save.hrManagerPlans, record.Parent, p => p.id)?.assignedEmployees, record.Index, item);
                case SaveKind.Defended: return Insert(Defence(save, record.Parent)?.affectedEmployeeIds, record.Index, item);
                case SaveKind.Message: return Enqueue(ById(save.Contacts, record.Parent, c => c.id)?.messagesQueue, record.Index, item);
                default: return null;
            }
        }

        // A stand-in's real value back into its field; the undo writes the stand-in again.
        private static Action Stand(GameInstance save, StoredRecord record, object item)
        {
            List<string> v = record.Values;
            switch (record.Kind)
            {
                case SaveKind.Player:
                    return v.Count < 10 ? null : PlayerBack(save, v);
                case SaveKind.Guider:
                {
                    if (v.Count < 2 || save.customDestination != null)
                    {
                        return null;
                    }
                    save.customDestination = new Address(v[0], DistrictSaveRules.ReadWhole(v[1], 0));
                    return () => save.customDestination = null;
                }
                case SaveKind.Mission:
                {
                    if (!(item is PlayerMission mission) || save.currentPlayerMission != null)
                    {
                        return null;
                    }
                    save.currentPlayerMission = mission;
                    return () => save.currentPlayerMission = null;
                }
                case SaveKind.Vehicle:
                    return CarBack(ById(save.VehicleInstances, record.Parent, c => c.id), v);
                case SaveKind.DriverVehicle:
                    return CarBack(ById(save.privateDriverVehicleInstances, record.Parent, c => c.id), v);
                case SaveKind.ContactAddress:
                {
                    Contact c = ById(save.Contacts, record.Parent, x => x.id);
                    if (c == null || v.Count < 2)
                    {
                        return null;
                    }
                    string street = c.streetName;
                    int number = c.streetNumber;
                    c.streetName = v[0];
                    c.streetNumber = DistrictSaveRules.ReadWhole(v[1], 0);
                    return () =>
                    {
                        c.streetName = street;
                        c.streetNumber = number;
                    };
                }
                case SaveKind.EmployeeSource:
                {
                    CandidateInfo info = Person(save, record.Parent)?.candidateInfo;
                    if (info == null || v.Count < 2)
                    {
                        return null;
                    }
                    Address was = info.sourceAddress;
                    info.sourceAddress = new Address(v[0], DistrictSaveRules.ReadWhole(v[1], 0));
                    return () => info.sourceAddress = was;
                }
                case SaveKind.EmployeeHrPlan:
                {
                    EmployeeInstance e = Person(save, record.Parent);
                    if (e == null || v.Count < 1)
                    {
                        return null;
                    }
                    string was = e.assignedHrManagerPlanId;
                    e.assignedHrManagerPlanId = v[0];
                    return () => e.assignedHrManagerPlanId = was;
                }
                case SaveKind.DriverSlot:
                {
                    var warehouse = Registration(save, record.Parent) as Warehouse;
                    List<VehicleSlot> slots = warehouse != null ? warehouse.vehicleSlots : null;
                    if (slots == null || record.Index < 0 || record.Index >= slots.Count || slots[record.Index] == null || v.Count < 1)
                    {
                        return null;
                    }
                    VehicleSlot slot = slots[record.Index];
                    string was = slot.employeeDriverId;
                    slot.employeeDriverId = v[0];
                    return () => slot.employeeDriverId = was;
                }
                case SaveKind.HeadhunterHrPlan:
                {
                    string[] hr = ById(save.headhunterPlans, record.Parent, p => p.id)?.assignedHrPlans;
                    if (hr == null || record.Index < 0 || record.Index >= hr.Length || v.Count < 1)
                    {
                        return null;
                    }
                    string was = hr[record.Index];
                    int at = record.Index;
                    hr[at] = v[0];
                    return () => hr[at] = was;
                }
                default:
                    return null;
            }
        }

        private static Action PlayerBack(GameInstance save, List<string> v)
        {
            string street = save.CurrentStreetName;
            int number = save.CurrentStreetNumber;
            SerializableVector3 at = save.LastPlayerPosition;
            SerializableQuaternion turn = save.LastPlayerRotation;
            save.CurrentStreetName = v[0] ?? string.Empty;
            save.CurrentStreetNumber = DistrictSaveRules.ReadWhole(v[1], 0);
            save.LastPlayerPosition = new SerializableVector3(DistrictSaveRules.ReadNumber(v[2]), DistrictSaveRules.ReadNumber(v[3]), DistrictSaveRules.ReadNumber(v[4]));
            save.LastPlayerRotation = new SerializableQuaternion(DistrictSaveRules.ReadNumber(v[5]), DistrictSaveRules.ReadNumber(v[6]), DistrictSaveRules.ReadNumber(v[7]),
                DistrictSaveRules.ReadNumber(v[8]));
            return () =>
            {
                save.CurrentStreetName = street;
                save.CurrentStreetNumber = number;
                save.LastPlayerPosition = at;
                save.LastPlayerRotation = turn;
            };
        }

        private static Action CarBack(VehicleInstance car, List<string> v)
        {
            if (car == null || v.Count < 11)
            {
                return null;
            }
            string street = car.streetName;
            int number = car.streetNumber;
            string district = car.parkingNeighbourhood;
            SerializableVector3 at = car.position;
            SerializableQuaternion turn = car.rotation;
            car.streetName = v[0];
            car.streetNumber = DistrictSaveRules.ReadWhole(v[1], 0);
            car.position = new SerializableVector3(DistrictSaveRules.ReadNumber(v[2]), DistrictSaveRules.ReadNumber(v[3]), DistrictSaveRules.ReadNumber(v[4]));
            car.rotation = new SerializableQuaternion(DistrictSaveRules.ReadNumber(v[5]), DistrictSaveRules.ReadNumber(v[6]), DistrictSaveRules.ReadNumber(v[7]),
                DistrictSaveRules.ReadNumber(v[8]));
            car.parkingNeighbourhood = v[10];
            return () =>
            {
                car.streetName = street;
                car.streetNumber = number;
                car.parkingNeighbourhood = district;
                car.position = at;
                car.rotation = turn;
            };
        }

        // ---------------------------------------------------------------- where things go

        // A record with an id goes back unless the game's part holds one with its id already.
        private static Action Once<T>(List<T> list, int index, object item, Func<T, string> id) where T : class
        {
            if (item is T value && ById(list, id(value), id) != null)
            {
                return Twice;
            }
            return Insert(list, index, item);
        }

        private static Action Insert<T>(List<T> list, int index, object item)
        {
            if (list == null || !(item is T value))
            {
                return null;
            }
            list.Insert(Math.Min(Math.Max(0, index), list.Count), value);
            return () => list.Remove(value);
        }

        private static Action Enqueue<T>(Queue<T> queue, int index, object item) where T : class
        {
            if (queue == null || !(item is T value))
            {
                return null;
            }
            var taken = new List<Taken<T>>(1) { new Taken<T> { Index = index, Item = value } };
            var scratch = new List<T>(queue.Count + 1);
            DistrictSaveRules.PutBack(queue, taken, scratch);
            return () =>
            {
                var none = new List<Taken<T>>(1);
                DistrictSaveRules.TakeOut(queue, x => ReferenceEquals(x, value), none, scratch);
            };
        }

        private static Action Order(GameInstance save, StoredRecord record, object item)
        {
            Dictionary<Address, List<ItemAmountTarget>> orders = save.itemsOrderedThisWeekByImporter;
            if (orders == null || record.Addresses.Count == 0 || !(item is List<ItemAmountTarget> list))
            {
                return null;
            }
            var key = new Address(record.Addresses[0].Street, record.Addresses[0].Number);
            if (orders.ContainsKey(key))
            {
                return null;   // the game has its own week for it now
            }
            orders[key] = list;
            return () => orders.Remove(key);
        }

        private static Action Paid(GameInstance save, int index, object item)
        {
            List<(Address, string)> paid = save.paidLicensingFeesToday;
            if (paid == null || !(item is ValueTuple<Address, string> fee))
            {
                return null;
            }
            paid.Insert(Math.Min(Math.Max(0, index), paid.Count), fee);
            return () => paid.Remove(fee);
        }

        private static BuildingRegistration Registration(GameInstance save, string key)
        {
            if (!DistrictSaveRules.ReadPlace(key, out string street, out int number))
            {
                return null;
            }
            List<BuildingRegistration> regs = save.BuildingRegistrations;
            for (int i = 0; regs != null && i < regs.Count; i++)
            {
                if (regs[i] != null && regs[i].StreetNumber == number && string.Equals(regs[i].StreetName, street, StringComparison.Ordinal))
                {
                    return regs[i];
                }
            }
            return null;
        }

        // A shift's day: "street#number/index" of the registration's days.
        private static ScheduleDay Day(GameInstance save, string key)
        {
            int slash = key != null ? key.LastIndexOf('/') : -1;
            if (slash < 0)
            {
                return null;
            }
            BuildingRegistration reg = Registration(save, key.Substring(0, slash));
            int d = DistrictSaveRules.ReadWhole(key.Substring(slash + 1), -1);
            return reg != null && reg.scheduleDays != null && d >= 0 && d < reg.scheduleDays.Count ? reg.scheduleDays[d] : null;
        }

        private static FinancialSummary Summary(GameInstance save, string key)
        {
            int day = DistrictSaveRules.ReadWhole(key, int.MinValue);
            List<FinancialSummary> days = save.financialSummaries;
            for (int i = 0; days != null && i < days.Count; i++)
            {
                if (days[i] != null && days[i].dayNumber == day)
                {
                    return days[i];
                }
            }
            return null;
        }

        private static ProductMarketEntry Market(GameInstance save, string itemName)
        {
            return ById(save.productMarketEntries, itemName, e => e.itemName);
        }

        // A rival's defence: "rivalId/index".
        private static DefenseState Defence(GameInstance save, string key)
        {
            int slash = key != null ? key.LastIndexOf('/') : -1;
            if (slash < 0)
            {
                return null;
            }
            SpecialRivalState rival = ById(save.specialRivalStates, key.Substring(0, slash), x => x.rivalId);
            int k = DistrictSaveRules.ReadWhole(key.Substring(slash + 1), -1);
            return rival != null && rival.defenseStates != null && k >= 0 && k < rival.defenseStates.Count ? rival.defenseStates[k] : null;
        }

        // An employee by where they are kept and their id: "emp/id", "cand/id", "nego:negotiation/id".
        private static EmployeeInstance Person(GameInstance save, string key)
        {
            int slash = key != null ? key.LastIndexOf('/') : -1;
            if (slash < 0)
            {
                return null;
            }
            string where = key.Substring(0, slash);
            string id = key.Substring(slash + 1);
            if (where == "emp")
            {
                return ById(save.EmployeeInstances, id, e => e.id);
            }
            if (where == "cand")
            {
                return ById(save.CandidateEmployeeInstances, id, e => e.id);
            }
            if (where.StartsWith("nego:", StringComparison.Ordinal))
            {
                EmployeeInstance e = ById(save.candidateSalaryNegotiations, where.Substring(5), n => n.id)?.employeeInstance;
                return e != null && e.id == id ? e : null;
            }
            return null;
        }

        private static T ById<T>(List<T> list, string id, Func<T, string> idOf) where T : class
        {
            if (list == null || string.IsNullOrEmpty(id))
            {
                return null;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && string.Equals(idOf(list[i]), id, StringComparison.Ordinal))
                {
                    return list[i];
                }
            }
            return null;
        }
    }
}
