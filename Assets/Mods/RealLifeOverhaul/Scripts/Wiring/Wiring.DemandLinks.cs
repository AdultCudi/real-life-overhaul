using System;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.Hustles;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The mod's own businesses with the living population (plans/tech-overhaul/POPULATION.md section 10; the owner, 1 Nov 2026: "everything will be either
        /// touched or changed"): each business's bridge to what the city's people bring it (Population/Demand, CityDemand), so it keeps its own mechanics and
        /// moves its old figure by the people from the day the population is anchored. While Living population is off, or before the people are measured, every
        /// bridge answers "none" and each business keeps exactly its own figures. Nothing per frame: each is asked when its business works out a day or an hour.
        /// </summary>
        public static class DemandLinks
        {
            internal static void Connect()
            {
                OwnBankBridges.People = BankPeople;
                OwnBankBridges.Branch = BranchPeople;
                EstateBridges.People = EstatePeople;
                EstateBridges.HousingIn = HousingIn;
                ConstructionBridges.People = BuildPeople;
                DealerBridges.Buyers = CarBuyers;
                OnlineBridges.People = Online;
                HustleBridges.Sellers = Sellers;
                HustleBridges.Buyers = Bargains;
                CareersBridges.Learners = Learners;
            }

            internal static void Disconnect()
            {
                OwnBankBridges.People = null;
                OwnBankBridges.Branch = null;
                EstateBridges.People = null;
                EstateBridges.HousingIn = null;
                ConstructionBridges.People = null;
                DealerBridges.Reset();
                OnlineBridges.People = null;
                HustleBridges.Sellers = null;
                HustleBridges.Buyers = null;
                CareersBridges.Learners = null;
            }

            // True while the people's figures stand for the mod's own businesses: the option on and a day measured (a guest's, the host's).
            private static bool Live => Settings.LivingPopulation && CityDemand.Running;

            // The own bank's pools: the households' savings and the firms' cash, and what each credit band, the shops and the companies borrow.
            private static int BankPeople(int pool)
            {
                try
                {
                    if (!Live || pool < 0 || pool >= OwnBankCatalogue.Savers + OwnBankCatalogue.Segments)
                    {
                        return -1;
                    }
                    switch (pool)
                    {
                        case OwnBankCatalogue.People: return CityDemand.Factor(PopDemand.SaversPeople);
                        case OwnBankCatalogue.Firms: return CityDemand.Factor(PopDemand.SaversFirms);
                        case OwnBankCatalogue.Savers + OwnBankCatalogue.Poor: return CityDemand.Factor(PopDemand.BorrowPoor);
                        case OwnBankCatalogue.Savers + OwnBankCatalogue.Fair: return CityDemand.Factor(PopDemand.BorrowFair);
                        case OwnBankCatalogue.Savers + OwnBankCatalogue.Good: return CityDemand.Factor(PopDemand.BorrowGood);
                        case OwnBankCatalogue.Savers + OwnBankCatalogue.Shops: return CityDemand.Factor(PopDemand.BorrowShops);
                        default: return CityDemand.Factor(PopDemand.BorrowFirms);
                    }
                }
                catch (Exception e)
                {
                    Fault("DemandLinks.BankPeople", e);
                    return -1;
                }
            }

            // The property business: hotel guests, car hire's customers, a landlord's applicants and their budgets, the agency's owners; 0 for none.
            private static int EstatePeople(EstateDemand d)
            {
                try
                {
                    if (!Live)
                    {
                        return 0;
                    }
                    PopDemand of = d == EstateDemand.Guests ? PopDemand.HotelGuests : d == EstateDemand.Hirers ? PopDemand.CarHire : d == EstateDemand.Seekers ? PopDemand.Tenants
                                   : d == EstateDemand.Budget ? PopDemand.TenantBudget : PopDemand.Instructions;
                    return Math.Max(1, CityDemand.Factor(of));   // never nought, which the climate reads as none
                }
                catch (Exception e)
                {
                    Fault("DemandLinks.EstatePeople", e);
                    return 0;
                }
            }

            // The construction company: flat buyers, the city's businesses rebuilding and refitting, the council's own buildings; 0 for none.
            private static int BuildPeople(BuildDemand d)
            {
                try
                {
                    if (!Live)
                    {
                        return 0;
                    }
                    PopDemand of = d == BuildDemand.Buyers ? PopDemand.FlatBuyers : d == BuildDemand.Public ? PopDemand.PublicTenders : PopDemand.ClientTenders;
                    return Math.Max(1, CityDemand.Factor(of));
                }
                catch (Exception e)
                {
                    Fault("DemandLinks.BuildPeople", e);
                    return 0;
                }
            }

            // A demand's factor for a bridge that reads 0 as none: never nought while live.
            private static int Of(PopDemand d, string where)
            {
                try
                {
                    return Live ? Math.Max(1, CityDemand.Factor(d)) : 0;
                }
                catch (Exception e)
                {
                    Fault(where, e);
                    return 0;
                }
            }

            private static int CarBuyers() => Of(PopDemand.CarBuyers, "DemandLinks.CarBuyers");

            private static int Sellers() => Of(PopDemand.Listings, "DemandLinks.Sellers");

            private static int Bargains() => Of(PopDemand.Bargains, "DemandLinks.Bargains");

            private static int Learners() => Of(PopDemand.Students, "DemandLinks.Learners");

            // The households' online spending of the kind a business type sells; 0 for none.
            private static int Online(string businessType)
            {
                try
                {
                    return Live ? Math.Max(1, CityDemand.Online(businessType)) : 0;
                }
                catch (Exception e)
                {
                    Fault("DemandLinks.Online", e);
                    return 0;
                }
            }

            // A district's households taking or looking for a flat against the city's; 0 for none.
            private static int HousingIn(string district)
            {
                try
                {
                    return Live ? Math.Max(1, CityDemand.HousingLean(district)) : 0;
                }
                catch (Exception e)
                {
                    Fault("DemandLinks.HousingIn", e);
                    return 0;
                }
            }

            // The residents round a branch: their income, saving and debt against the city's, and how many of its borrowers are in each band.
            private static BranchPeople BranchPeople(string district)
            {
                try
                {
                    if (!Live || string.IsNullOrEmpty(district))
                    {
                        return default;
                    }
                    return new BranchPeople
                    {
                        Sums = CityDemand.IncomeLean(district), Saving = CityDemand.SavingLean(district), Borrowing = CityDemand.BorrowLean(district),
                        Poor = CityDemand.AskLean(district, PopDemandRules.Poor), Fair = CityDemand.AskLean(district, PopDemandRules.Fair), Good = CityDemand.AskLean(district, PopDemandRules.Good)
                    };
                }
                catch (Exception e)
                {
                    Fault("DemandLinks.BranchPeople", e);
                    return default;
                }
            }
        }
    }
}
