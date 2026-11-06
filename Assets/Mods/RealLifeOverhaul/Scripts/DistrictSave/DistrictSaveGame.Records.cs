using System;
using System.Collections.Generic;
using AI.Employees.SalaryNegotiation;
using BigAmbitions.Items;
using BigAmbitions.Neighborhoods;
using Buildings.Office.Headquarters;
using Entities;
using Player.FoodDeliveryJob;
using Vehicles;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// The whole records that leave with the district: a business's registration with everything in it, its staff and
    /// candidates, the plans of an office there, and every record of the save that names one of the mod's places.
    /// A record that leaves because another left (a plan whose manager worked there, a task about an employee there)
    /// carries that one's places, so that at a load it goes back only with it. COST: one pass over each list at a save.
    /// </summary>
    internal static partial class DistrictSaveGame
    {
        private static void Registrations(GameInstance save, PlaceSet places, Strip s)
        {
            // save path: GameInstance.BuildingRegistrations[].StreetName
            // save path: GameInstance.BuildingRegistrations[].StreetNumber
            // Everything a registration holds leaves with it: its items, rota and shifts, designs, layout, its rival owners,
            // save path: GameInstance.BuildingRegistrations[].scheduleDays[].workShifts[].employeeId
            // save path: GameInstance.BuildingRegistrations[].marketingCampaigns[].agencyAddress
            // save path: GameInstance.BuildingRegistrations[].aiEmployees[].aiAddress
            // save path: GameInstance.BuildingRegistrations[].poachedEmployees[].assignedAddress
            // save path: GameInstance.BuildingRegistrations[].poachedEmployees[].assignedHrManagerPlanId
            // save path: GameInstance.BuildingRegistrations[].poachedEmployees[].candidateInfo.sourceAddress
            // save path: GameInstance.BuildingRegistrations[]<Entities.Warehouse>.vehicleSlots[].employeeDriverId
            // and another player's business (Going Public mirrors it into every world) is put back after the write, never kept.
            Leave(s, save.BuildingRegistrations, SaveKind.Registration,
                r => r != null && places.Names(r.StreetName, r.StreetNumber),
                (record, r) => record.Addresses.Add(new PlaceKey(r.StreetName, r.StreetNumber)),
                r =>
                {
                    if (!GoingPublic.Foreign(r))
                    {
                        return false;
                    }
                    s.Foreign.Add(new PlaceKey(r.StreetName, r.StreetNumber));
                    return true;
                });
        }

        private static void People(GameInstance save, PlaceSet places, Strip s)
        {
            // save path: GameInstance.EmployeeInstances[].assignedAddress
            List<Taken<EmployeeInstance>> staff = Leave(s, save.EmployeeInstances, SaveKind.Employee,
                e => e != null && Names(places, e.assignedAddress),
                (record, e) => Person(record, places, e),
                e => e.assignedAddress != null && s.Foreign.Contains(new PlaceKey(e.assignedAddress.streetName, e.assignedAddress.streetNumber)));
            Remember(s, staff);

            // save path: GameInstance.CandidateEmployeeInstances[].assignedAddress
            // save path: GameInstance.CandidateEmployeeInstances[].candidateInfo.sourceAddress
            List<Taken<EmployeeInstance>> candidates = Leave(s, save.CandidateEmployeeInstances, SaveKind.Candidate,
                e => e != null && (Names(places, e.assignedAddress) || Names(places, e.candidateInfo != null ? e.candidateInfo.sourceAddress : null)),
                (record, e) => Person(record, places, e));
            Remember(s, candidates);

            // save path: GameInstance.candidateSalaryNegotiations[].employeeInstance.assignedAddress
            // save path: GameInstance.candidateSalaryNegotiations[].employeeInstance.candidateInfo.sourceAddress
            List<Taken<CandidateSalaryNegotiation>> talks = Leave(s, save.candidateSalaryNegotiations, SaveKind.Negotiation,
                n => n != null && n.employeeInstance != null && (EmbeddedNames(places, n.employeeInstance) || In(s.Employees, n.employeeInstance.id)),
                (record, n) =>
                {
                    Person(record, places, n.employeeInstance);
                    Depends(record, s, n.employeeInstance.id);
                });
            for (int i = 0; talks != null && i < talks.Count; i++)
            {
                if (!string.IsNullOrEmpty(talks[i].Item.id))
                {
                    s.Negotiations.Add(talks[i].Item.id);
                    s.TalkPlaces[talks[i].Item.id] = Find(s, SaveKind.Negotiation, talks[i].Index);
                }
            }
        }

        private static void Plans(GameInstance save, PlaceSet places, Strip s)
        {
            // An office's plans leave with the office (its headquarters' address), and with the manager who runs them.
            // save path: GameInstance.hrManagerPlans[].headquartersAddress
            // save path: GameInstance.hrManagerPlans[].assignedEmployeeId
            // save path: GameInstance.hrManagerPlans[].assignedEmployees[] (with the plan)
            List<Taken<HrManagerPlan>> hr = Leave(s, save.hrManagerPlans, SaveKind.HrPlan,
                p => p != null && (Names(places, p.headquartersAddress) || In(s.Employees, p.assignedEmployeeId)),
                (record, p) =>
                {
                    Mark(record, places, p.headquartersAddress);
                    Depends(record, s, p.assignedEmployeeId);
                });
            for (int i = 0; hr != null && i < hr.Count; i++)
            {
                if (!string.IsNullOrEmpty(hr[i].Item.id))
                {
                    s.HrPlans.Add(hr[i].Item.id);
                    s.PlanPlaces[hr[i].Item.id] = Find(s, SaveKind.HrPlan, hr[i].Index);
                }
            }

            // save path: GameInstance.logisticsManagerPlans[].headquartersAddress
            // save path: GameInstance.logisticsManagerPlans[].targetAddress
            // save path: GameInstance.logisticsManagerPlans[].assignedEmployeeId
            Leave(s, save.logisticsManagerPlans, SaveKind.LogisticsPlan,
                p => p != null && (Names(places, p.headquartersAddress) || Names(places, p.targetAddress) || In(s.Employees, p.assignedEmployeeId)),
                (record, p) =>
                {
                    Mark(record, places, p.headquartersAddress);
                    Mark(record, places, p.targetAddress);
                    Depends(record, s, p.assignedEmployeeId);
                });

            // save path: GameInstance.headhunterPlans[].headquartersAddress
            // save path: GameInstance.headhunterPlans[].assignedEmployeeId
            Leave(s, save.headhunterPlans, SaveKind.HeadhunterPlan,
                p => p != null && (Names(places, p.headquartersAddress) || In(s.Employees, p.assignedEmployeeId)),
                (record, p) =>
                {
                    Mark(record, places, p.headquartersAddress);
                    Depends(record, s, p.assignedEmployeeId);
                });

            // save path: GameInstance.pricingManagerPlans[].headquartersAddress
            // save path: GameInstance.pricingManagerPlans[].supervisedNeighborhood
            // save path: GameInstance.pricingManagerPlans[].assignedEmployeeId
            Leave(s, save.pricingManagerPlans, SaveKind.PricingPlan,
                p => p != null && (Names(places, p.headquartersAddress) || places.NamesDistrict(p.supervisedNeighborhood) || In(s.Employees, p.assignedEmployeeId)),
                (record, p) =>
                {
                    Mark(record, places, p.headquartersAddress);
                    MarkDistrict(record, places, p.supervisedNeighborhood);
                    Depends(record, s, p.assignedEmployeeId);
                });

            // save path: GameInstance.importPartnerships[].headquartersAddress
            // save path: GameInstance.importPartnerships[].importAddress
            // save path: GameInstance.importPartnerships[].employeeInstanceId
            Leave(s, save.importPartnerships, SaveKind.Import,
                p => p != null && (Names(places, p.headquartersAddress) || Names(places, p.importAddress) || In(s.Employees, p.employeeInstanceId)),
                (record, p) =>
                {
                    Mark(record, places, p.headquartersAddress);
                    Mark(record, places, p.importAddress);
                    Depends(record, s, p.employeeInstanceId);
                });

            // An insurance offer leaves with the HR manager's plan it was made to.
            // save path: GameInstance.healthInsurancePlanOffers[].hrManagerPlanId
            List<Taken<HealthInsurancePlanOffer>> offers = Leave(s, save.healthInsurancePlanOffers, SaveKind.InsuranceOffer,
                o => o != null && In(s.HrPlans, o.hrManagerPlanId),
                (record, o) => Carry(record, s.PlanPlaces, o.hrManagerPlanId));
            for (int i = 0; offers != null && i < offers.Count; i++)
            {
                if (!string.IsNullOrEmpty(offers[i].Item.id))
                {
                    s.Offers.Add(offers[i].Item.id);
                    s.TalkPlaces[offers[i].Item.id] = Find(s, SaveKind.InsuranceOffer, offers[i].Index);
                }
            }
        }

        private static void Records(GameInstance save, PlaceSet places, Strip s)
        {
            // save path: GameInstance.JobInstances[].address
            Leave(s, save.JobInstances, SaveKind.Job, j => j != null && Names(places, j.address), (record, j) => Mark(record, places, j.address));

            // save path: GameInstance.NeighbourhoodStats[].name
            Leave<NeighbourhoodStats>(s, save.NeighbourhoodStats, SaveKind.DistrictStats, n => n != null && places.NamesDistrict(n.name),
                (record, n) => MarkDistrict(record, places, n.name));

            // save path: GameInstance.Loans[].bankAddress
            Leave(s, save.Loans, SaveKind.Loan, l => l != null && Names(places, l.bankAddress), (record, l) => Mark(record, places, l.bankAddress));

            // save path: GameInstance.Transactions[].address
            Transactions(save, places, s);

            // save path: GameInstance.RecruitmentCampaigns[].agencyAddress
            // save path: GameInstance.RecruitmentCampaigns[].businessAddress
            Leave(s, save.RecruitmentCampaigns, SaveKind.Recruitment, c => c != null && (Names(places, c.agencyAddress) || Names(places, c.businessAddress)),
                (record, c) =>
                {
                    Mark(record, places, c.agencyAddress);
                    Mark(record, places, c.businessAddress);
                });

            // save path: GameInstance.DeliveryContracts[].wholesaleAddress
            // save path: GameInstance.DeliveryContracts[].businessAddress
            Leave(s, save.DeliveryContracts, SaveKind.Delivery, c => c != null && (Names(places, c.wholesaleAddress) || Names(places, c.businessAddress)),
                (record, c) =>
                {
                    Mark(record, places, c.wholesaleAddress);
                    Mark(record, places, c.businessAddress);
                });

            // save path: GameInstance.FurnitureDeliveryContracts[].fromAddress
            // save path: GameInstance.FurnitureDeliveryContracts[].toAddress
            Leave(s, save.FurnitureDeliveryContracts, SaveKind.FurnitureDelivery, c => c != null && (Names(places, c.fromAddress) || Names(places, c.toAddress)),
                (record, c) =>
                {
                    Mark(record, places, c.fromAddress);
                    Mark(record, places, c.toAddress);
                });

            // save path: GameInstance.FoodDeliveryContracts[].toAddress
            Leave(s, save.FoodDeliveryContracts, SaveKind.FoodDelivery, c => c != null && Names(places, c.toAddress), (record, c) => Mark(record, places, c.toAddress));

            // A task at one of the mod's places, or about an employee who left.
            // save path: GameInstance.TodoTasks[].address
            // save path: GameInstance.TodoTasks[].employeeId
            Leave(s, save.TodoTasks, SaveKind.Task, t => t != null && (Names(places, t.address) || In(s.Employees, t.employeeId)),
                (record, t) =>
                {
                    Mark(record, places, t.address);
                    Depends(record, s, t.employeeId);
                });

            // save path: GameInstance.marketEvents[].neighbourhood
            // save path: GameInstance.marketEvents[].address
            Leave(s, save.marketEvents, SaveKind.MarketEvent, m => m != null && (places.NamesDistrict(m.neighbourhood) || Names(places, m.address)),
                (record, m) =>
                {
                    MarkDistrict(record, places, m.neighbourhood);
                    Mark(record, places, m.address);
                });

            // save path: GameInstance.buildingsForSale[].address
            Leave(s, save.buildingsForSale, SaveKind.ForSale, b => b != null && Names(places, b.address), (record, b) => Mark(record, places, b.address));

            // save path: GameInstance.realEstate[].address
            Leave(s, save.realEstate, SaveKind.Deed, d => d != null && Names(places, d.address), (record, d) => Mark(record, places, d.address));

            // The city map's ticked rows: a district's row is named by the district's id (CityMapFilters.CreateNeighborhoodsCategory).
            // save path: GameInstance.SelectedCitymapFilters
            Leave(s, save.SelectedCitymapFilters, SaveKind.MapFilter, f => places.NamesDistrict(f), (record, f) => MarkDistrict(record, places, f));

            // save path: GameInstance.interiorInstallationFirmContracts[].interiorInstallationFirmAddress
            // save path: GameInstance.interiorInstallationFirmContracts[].addressToDoTheInstallation
            Leave(s, save.interiorInstallationFirmContracts, SaveKind.Installation,
                c => c != null && (Names(places, c.interiorInstallationFirmAddress) || Names(places, c.addressToDoTheInstallation)),
                (record, c) =>
                {
                    Mark(record, places, c.interiorInstallationFirmAddress);
                    Mark(record, places, c.addressToDoTheInstallation);
                });

            // A move from or to one of the mod's places, or by a moving firm there (its registration rides in the contract).
            // save path: GameInstance.movingServiceContracts[].originMovingAddress
            // save path: GameInstance.movingServiceContracts[].destinationMovingAddress
            // save path: GameInstance.movingServiceContracts[].movingCompanyRegistration.StreetName
            // save path: GameInstance.movingServiceContracts[].movingCompanyRegistration.StreetNumber
            // and everything in that registration with it:
            // save path: GameInstance.movingServiceContracts[].movingCompanyRegistration.scheduleDays[].workShifts[].employeeId
            // save path: GameInstance.movingServiceContracts[].movingCompanyRegistration.marketingCampaigns[].agencyAddress
            // save path: GameInstance.movingServiceContracts[].movingCompanyRegistration.aiEmployees[].aiAddress
            // save path: GameInstance.movingServiceContracts[].movingCompanyRegistration.poachedEmployees[].assignedAddress
            // save path: GameInstance.movingServiceContracts[].movingCompanyRegistration.poachedEmployees[].assignedHrManagerPlanId
            // save path: GameInstance.movingServiceContracts[].movingCompanyRegistration.poachedEmployees[].candidateInfo.sourceAddress
            // save path: GameInstance.movingServiceContracts[].movingCompanyRegistration<Entities.Warehouse>.vehicleSlots[].employeeDriverId
            Leave(s, save.movingServiceContracts, SaveKind.Moving,
                c => c != null && (Names(places, c.originMovingAddress) || Names(places, c.destinationMovingAddress) || Firm(places, c.movingCompanyRegistration)),
                (record, c) =>
                {
                    Mark(record, places, c.originMovingAddress);
                    Mark(record, places, c.destinationMovingAddress);
                    if (c.movingCompanyRegistration != null)
                    {
                        Mark(record, places, c.movingCompanyRegistration.StreetName, c.movingCompanyRegistration.StreetNumber);
                    }
                });

            // save path: GameInstance.itemsOrderedThisWeekByImporter{key}
            ImporterOrders(save, places, s);

            // save path: GameInstance.foodDeliveryOffers[].pickupAddress
            // save path: GameInstance.foodDeliveryOffers[].destinationAddress
            Leave(s, save.foodDeliveryOffers, SaveKind.FoodOffer, o => o != null && (Names(places, o.pickupAddress) || Names(places, o.destinationAddress)),
                (record, o) =>
                {
                    Mark(record, places, o.pickupAddress);
                    Mark(record, places, o.destinationAddress);
                });

            // save path: GameInstance.deliveryJobLocationsDoneToday[]
            Leave(s, save.deliveryJobLocationsDoneToday, SaveKind.DeliveryDone, a => Names(places, a), (record, a) => Mark(record, places, a));

            // save path: GameInstance.vehicleDeliveryContracts[].deliveryAddress
            // save path: GameInstance.vehicleDeliveryContracts[].vehicleStoreAddress
            Leave(s, save.vehicleDeliveryContracts, SaveKind.VehicleDelivery, c => c != null && (Names(places, c.deliveryAddress) || Names(places, c.vehicleStoreAddress)),
                (record, c) =>
                {
                    Mark(record, places, c.deliveryAddress);
                    Mark(record, places, c.vehicleStoreAddress);
                });

            // save path: GameInstance.disabledLicensingFees[].address
            Leave(s, save.disabledLicensingFees, SaveKind.FeeOff, f => f != null && Names(places, f.address), (record, f) => Mark(record, places, f.address));

            // save path: GameInstance.paidLicensingFeesToday[].Item1
            Leave(s, save.paidLicensingFeesToday, SaveKind.FeePaid, f => Names(places, f.Item1), (record, f) => Mark(record, places, f.Item1));
        }

        // The transactions are a queue, read in the order they were made.
        private static void Transactions(GameInstance save, PlaceSet places, Strip s)
        {
            Queue<Transaction> queue = save.Transactions;
            if (queue == null || queue.Count == 0)
            {
                return;
            }
            var taken = new List<Taken<Transaction>>(2);
            var scratch = new List<Transaction>(queue.Count);
            if (DistrictSaveRules.TakeOut(queue, t => t != null && Names(places, t.address), taken, scratch) == 0)
            {
                return;
            }
            s.PutBacks.Add(() => DistrictSaveRules.PutBack(queue, taken, scratch));
            for (int i = 0; i < taken.Count; i++)
            {
                s.Left++;
                var record = new StoredRecord(SaveKind.Transaction, taken[i].Index, null);
                Mark(record, places, taken[i].Item.address);
                record.Slot = s.Slot(taken[i].Item);
                s.Records.Add(record);
            }
        }

        // What a purchasing agent ordered this week for a warehouse of the mod's, keyed by the warehouse's address.
        private static void ImporterOrders(GameInstance save, PlaceSet places, Strip s)
        {
            Dictionary<Address, List<ItemAmountTarget>> orders = save.itemsOrderedThisWeekByImporter;
            if (orders == null || orders.Count == 0)
            {
                return;
            }
            List<Address> keys = null;
            foreach (KeyValuePair<Address, List<ItemAmountTarget>> pair in orders)
            {
                if (Names(places, pair.Key))
                {
                    (keys ?? (keys = new List<Address>(1))).Add(pair.Key);
                }
            }
            if (keys == null)
            {
                return;
            }
            var values = new List<List<ItemAmountTarget>>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                values.Add(orders[keys[i]]);
                orders.Remove(keys[i]);
                s.Left++;
                var record = new StoredRecord(SaveKind.ImporterOrder, i, null);
                Mark(record, places, keys[i]);
                record.Slot = s.Slot(values[i]);
                s.Records.Add(record);
            }
            s.PutBacks.Add(() =>
            {
                for (int i = 0; i < keys.Count; i++)
                {
                    orders[keys[i]] = values[i];   // a dictionary has no order of its own: its keys are what matter
                }
            });
        }

        // A moving firm's registration riding in its contract names one of the mod's places.
        private static bool Firm(PlaceSet places, BuildingRegistration firm)
        {
            return firm != null && places.Names(firm.StreetName, firm.StreetNumber);
        }

        // An employee's places: where they work and where they were found.
        private static void Person(StoredRecord record, PlaceSet places, EmployeeInstance e)
        {
            if (e == null)
            {
                return;
            }
            Mark(record, places, e.assignedAddress);
            Mark(record, places, e.candidateInfo != null ? e.candidateInfo.sourceAddress : null);
        }

        // A copy of an employee riding in another record (a negotiation, a replacement, a poached one) names one of the mod's places.
        private static bool EmbeddedNames(PlaceSet places, EmployeeInstance e)
        {
            return e != null && (Names(places, e.assignedAddress) || Names(places, e.candidateInfo != null ? e.candidateInfo.sourceAddress : null));
        }

        // The employees and candidates that left, by id, with their records for the ones that lean on them.
        private static void Remember(Strip s, List<Taken<EmployeeInstance>> taken)
        {
            for (int i = 0; taken != null && i < taken.Count; i++)
            {
                EmployeeInstance e = taken[i].Item;
                if (string.IsNullOrEmpty(e.id))
                {
                    continue;
                }
                s.Employees.Add(e.id);
                if (e.assignedAddress != null)
                {
                    s.EmployeePlaces[e.id] = new PlaceKey(e.assignedAddress.streetName, e.assignedAddress.streetNumber);
                }
            }
        }

        // A record that leaves because an employee left carries the employee's workplace, to go back only with them.
        private static void Depends(StoredRecord record, Strip s, string employeeId)
        {
            if (!string.IsNullOrEmpty(employeeId) && s.EmployeePlaces.TryGetValue(employeeId, out PlaceKey place) && !record.Addresses.Contains(place))
            {
                record.Addresses.Add(place);
            }
        }

        // The record kept for a whole record of a kind at the index it stood at (the last such).
        private static StoredRecord Find(Strip s, SaveKind kind, int index)
        {
            for (int i = s.Records.Count - 1; i >= 0; i--)
            {
                if (s.Records[i].Kind == kind && s.Records[i].Index == index && s.Records[i].Parent == null)
                {
                    return s.Records[i];
                }
            }
            return null;
        }
    }
}
