using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.Rivals;
using Buildings.Office.Headquarters;
using Entities;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// The elements that leave a record of the game's own, which stays: what in a game business, a day's accounts, a
    /// product's market, a plan or a message names one of the mod's places, or someone or something that left. Each is
    /// kept under its record's key (an address, a day, an item, an id) to go back into it. Run after the whole records,
    /// so that only the records that stay are gone through. COST: one pass over each list at a save.
    /// </summary>
    internal static partial class DistrictSaveGame
    {
        private static void Elements(GameInstance save, PlaceSet places, Strip s)
        {
            Businesses(save, places, s);
            Accounts(save, places, s);

            // save path: GameInstance.productMarketEntries[].demandValues[].neighborhood
            List<ProductMarketEntry> market = save.productMarketEntries;
            for (int i = 0; market != null && i < market.Count; i++)
            {
                ProductMarketEntry entry = market[i];
                if (entry != null)
                {
                    LeaveFrom(s, entry.demandValues, entry.itemName, SaveKind.Demand, d => d != null && places.NamesDistrict(d.neighborhood),
                        (record, d) => MarkDistrict(record, places, d.neighborhood));
                }
            }

            // save path: GameInstance.pricingManagerPlans[].originalStorePrices[].storeAddress
            List<PricingManagerPlan> pricing = save.pricingManagerPlans;
            for (int i = 0; pricing != null && i < pricing.Count; i++)
            {
                PricingManagerPlan p = pricing[i];
                if (p != null)
                {
                    LeaveFrom(s, p.originalStorePrices, p.id, SaveKind.StorePrice, o => o != null && Names(places, o.storeAddress), (record, o) => Mark(record, places, o.storeAddress));
                }
            }

            // save path: GameInstance.logisticsManagerPlans[].destinations[].deliveryTargetAddress
            List<LogisticsManagerPlan> logistics = save.logisticsManagerPlans;
            for (int i = 0; logistics != null && i < logistics.Count; i++)
            {
                LogisticsManagerPlan p = logistics[i];
                if (p != null)
                {
                    LeaveFrom(s, p.destinations, p.id, SaveKind.Destination, d => d != null && Names(places, d.deliveryTargetAddress),
                        (record, d) => Mark(record, places, d.deliveryTargetAddress));
                }
            }

            // save path: GameInstance.importPartnerships[].products[].assignedWarehouse
            List<ImportPartnership> imports = save.importPartnerships;
            for (int i = 0; imports != null && i < imports.Count; i++)
            {
                ImportPartnership p = imports[i];
                if (p != null)
                {
                    LeaveFrom(s, p.products, p.id, SaveKind.ImportProduct, d => d != null && Names(places, d.assignedWarehouse), (record, d) => Mark(record, places, d.assignedWarehouse));
                }
            }

            // A replacement waiting for someone of the mod's places, or for someone who left, or under a plan that left.
            // save path: GameInstance.headhunterPlans[].headhunterReplacementDataList[].employeeInstance.assignedAddress
            // save path: GameInstance.headhunterPlans[].headhunterReplacementDataList[].employeeInstance.candidateInfo.sourceAddress
            // save path: GameInstance.headhunterPlans[].headhunterReplacementDataList[].employeeInstance.assignedHrManagerPlanId
            List<HeadhunterPlan> headhunters = save.headhunterPlans;
            for (int i = 0; headhunters != null && i < headhunters.Count; i++)
            {
                HeadhunterPlan p = headhunters[i];
                if (p != null)
                {
                    LeaveFrom(s, p.headhunterReplacementDataList, p.id, SaveKind.Replacement,
                        d => d != null && d.employeeInstance != null && (EmbeddedNames(places, d.employeeInstance) || In(s.Employees, d.employeeInstance.id)
                                                                         || In(s.HrPlans, d.employeeInstance.assignedHrManagerPlanId)),
                        (record, d) =>
                        {
                            Person(record, places, d.employeeInstance);
                            Depends(record, s, d.employeeInstance.id);
                            Carry(record, s.PlanPlaces, d.employeeInstance.assignedHrManagerPlanId);
                        });
                }
            }

            // An HR manager's people who left.
            // save path: GameInstance.hrManagerPlans[].assignedEmployees[]
            List<HrManagerPlan> hr = save.hrManagerPlans;
            for (int i = 0; s.Employees.Count > 0 && hr != null && i < hr.Count; i++)
            {
                HrManagerPlan p = hr[i];
                if (p != null)
                {
                    LeaveFrom(s, p.assignedEmployees, p.id, SaveKind.HrMember, id => In(s.Employees, id), (record, id) => Depends(record, s, id));
                }
            }

            // A rival's moves against employees who left.
            // save path: GameInstance.specialRivalStates[].defenseStates[].affectedEmployeeIds[]
            List<SpecialRivalState> rivals = save.specialRivalStates;
            for (int i = 0; s.Employees.Count > 0 && rivals != null && i < rivals.Count; i++)
            {
                SpecialRivalState rival = rivals[i];
                for (int k = 0; rival != null && rival.defenseStates != null && k < rival.defenseStates.Count; k++)
                {
                    DefenseState state = rival.defenseStates[k];
                    if (state != null)
                    {
                        LeaveFrom(s, state.affectedEmployeeIds, rival.rivalId + "/" + k.ToString(CultureInfo.InvariantCulture), SaveKind.Defended, id => In(s.Employees, id),
                            (record, id) => Depends(record, s, id));
                    }
                }
            }

            Messages(save, s);
        }

        // The game's own businesses stay; a campaign by an agency of the mod's places, staff of theirs, and shifts of
        // someone who left, leave them.
        private static void Businesses(GameInstance save, PlaceSet places, Strip s)
        {
            List<BuildingRegistration> regs = save.BuildingRegistrations;
            for (int i = 0; regs != null && i < regs.Count; i++)
            {
                BuildingRegistration r = regs[i];
                if (r == null)
                {
                    continue;
                }
                string key = KeyOf(r.StreetName, r.StreetNumber);
                // save path: GameInstance.BuildingRegistrations[].marketingCampaigns[].agencyAddress
                LeaveFrom(s, r.marketingCampaigns, key, SaveKind.Campaign, c => c != null && Names(places, c.agencyAddress), (record, c) => Mark(record, places, c.agencyAddress));
                // save path: GameInstance.BuildingRegistrations[].aiEmployees[].aiAddress
                LeaveFrom(s, r.aiEmployees, key, SaveKind.AiEmployee, a => a != null && Names(places, a.aiAddress), (record, a) => Mark(record, places, a.aiAddress));
                // save path: GameInstance.BuildingRegistrations[].poachedEmployees[].assignedAddress
                // save path: GameInstance.BuildingRegistrations[].poachedEmployees[].candidateInfo.sourceAddress
                // save path: GameInstance.BuildingRegistrations[].poachedEmployees[].assignedHrManagerPlanId
                LeaveFrom(s, r.poachedEmployees, key, SaveKind.Poached, e => e != null && (EmbeddedNames(places, e) || In(s.HrPlans, e.assignedHrManagerPlanId)),
                    (record, e) =>
                    {
                        Person(record, places, e);
                        Carry(record, s.PlanPlaces, e.assignedHrManagerPlanId);
                    });
                // save path: GameInstance.BuildingRegistrations[].scheduleDays[].workShifts[].employeeId
                for (int d = 0; s.Employees.Count > 0 && r.scheduleDays != null && d < r.scheduleDays.Count; d++)
                {
                    ScheduleDay day = r.scheduleDays[d];
                    if (day != null)
                    {
                        LeaveFrom(s, day.workShifts, key + "/" + d.ToString(CultureInfo.InvariantCulture), SaveKind.Shift, w => w != null && In(s.Employees, w.employeeId),
                            (record, w) => Depends(record, s, w.employeeId));
                    }
                }
            }
        }

        // A day's accounts stay; the lines of a business, home or building of the mod's leave them, under the day.
        private static void Accounts(GameInstance save, PlaceSet places, Strip s)
        {
            List<FinancialSummary> days = save.financialSummaries;
            for (int i = 0; days != null && i < days.Count; i++)
            {
                FinancialSummary day = days[i];
                if (day == null)
                {
                    continue;
                }
                string key = day.dayNumber.ToString(CultureInfo.InvariantCulture);
                // save path: GameInstance.financialSummaries[].businessIncomeStatements[].Address
                LeaveFrom(s, day.businessIncomeStatements, key, SaveKind.BusinessStatement, b => b != null && Names(places, b.Address), (record, b) => Mark(record, places, b.Address));
                // save path: GameInstance.financialSummaries[].residentialStatements[].Address
                LeaveFrom(s, day.residentialStatements, key, SaveKind.HomeStatement, h => h != null && Names(places, h.Address), (record, h) => Mark(record, places, h.Address));
                // save path: GameInstance.financialSummaries[].realEstateStatements[].Address
                LeaveFrom(s, day.realEstateStatements, key, SaveKind.EstateStatement, e => e != null && Names(places, e.Address), (record, e) => Mark(record, places, e.Address));
            }
        }

        // A message whose button acts on someone, an offer or a negotiation that left leaves its contact's messages.
        // save path: GameInstance.Contacts[].messagesQueue[].contextAction.employeeInstanceId
        // save path: GameInstance.Contacts[].messagesQueue[].contextAction.healthPlanOfferId
        // save path: GameInstance.Contacts[].messagesQueue[].contextAction.salaryNegotiationId
        private static void Messages(GameInstance save, Strip s)
        {
            if (s.Employees.Count == 0 && s.Offers.Count == 0 && s.Negotiations.Count == 0)
            {
                return;
            }
            List<Contact> contacts = save.Contacts;
            for (int i = 0; contacts != null && i < contacts.Count; i++)
            {
                Contact c = contacts[i];
                if (c == null || c.messagesQueue == null || c.messagesQueue.Count == 0)
                {
                    continue;
                }
                Queue<TextMessage> queue = c.messagesQueue;
                var taken = new List<Taken<TextMessage>>(1);
                var scratch = new List<TextMessage>(queue.Count);
                if (DistrictSaveRules.TakeOut(queue, m => m != null && Acts(m, s), taken, scratch) == 0)
                {
                    continue;
                }
                s.PutBacks.Add(() => DistrictSaveRules.PutBack(queue, taken, scratch));
                for (int k = 0; k < taken.Count; k++)
                {
                    s.Left++;
                    TextMessage.ContextAction action = taken[k].Item.contextAction;
                    var record = new StoredRecord(SaveKind.Message, taken[k].Index, c.id);
                    Depends(record, s, action.employeeInstanceId);
                    Carry(record, s.TalkPlaces, action.healthPlanOfferId);
                    Carry(record, s.TalkPlaces, action.salaryNegotiationId);
                    record.Slot = s.Slot(taken[k].Item);
                    s.Records.Add(record);
                }
            }
        }

        private static bool Acts(TextMessage m, Strip s)
        {
            TextMessage.ContextAction a = m.contextAction;
            return a != null && (In(s.Employees, a.employeeInstanceId) || In(s.Offers, a.healthPlanOfferId) || In(s.Negotiations, a.salaryNegotiationId));
        }

        // A record that leaves because another left carries that one's places, to go back only with it.
        private static void Carry(StoredRecord record, Dictionary<string, StoredRecord> from, string id)
        {
            if (string.IsNullOrEmpty(id) || !from.TryGetValue(id, out StoredRecord other) || other == null)
            {
                return;
            }
            for (int i = 0; i < other.Addresses.Count; i++)
            {
                if (!record.Addresses.Contains(other.Addresses[i]))
                {
                    record.Addresses.Add(other.Addresses[i]);
                }
            }
            for (int i = 0; i < other.Districts.Count; i++)
            {
                if (!record.Districts.Contains(other.Districts[i]))
                {
                    record.Districts.Add(other.Districts[i]);
                }
            }
        }
    }
}
