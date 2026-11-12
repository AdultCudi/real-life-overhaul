using System;
using Localizor;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Districts;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.Hustles;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Tech;

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
                EstatePeopleLinks.Connect();
                DealerBridges.Buyers = CarBuyers;
                OnlineBridges.People = Online;
                HustleBridges.Sellers = Sellers;
                HustleBridges.Buyers = Bargains;
                CareersBridges.Learners = Learners;
                CareersBridges.Openings = Openings;
                TechBridges.Seekers = TechSeekers;
                Utilities.UtilitiesBridges.Connections = Connections;
                CivicBridges.Adults = Adults;
                CivicBridges.Taxes = Taxes;
                CivicBridges.Residents = Residents;
                CityWatch.Residents = CrimeHere;
                DistrictBridges.Crime = CrimeIn;
                Meta.Guide.PageExtra = Pointer;
            }

            internal static void Disconnect()
            {
                OwnBankBridges.People = null;
                OwnBankBridges.Branch = null;
                EstateBridges.People = null;
                EstateBridges.HousingIn = null;
                EstatePeopleLinks.Disconnect();
                DealerBridges.Reset();
                OnlineBridges.People = null;
                HustleBridges.Sellers = null;
                HustleBridges.Buyers = null;
                CareersBridges.Learners = null;
                CareersBridges.Openings = null;
                TechBridges.Seekers = null;
                Utilities.UtilitiesBridges.Connections = null;
                CivicBridges.Adults = null;
                CivicBridges.Taxes = null;
                CivicBridges.Residents = null;
                CityWatch.Residents = null;
                DistrictBridges.Crime = null;
                Meta.Guide.PageExtra = null;
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

            /// <summary>
            /// The construction company: flat buyers, the city's businesses rebuilding and refitting, the council's own buildings; 0 for none. Handed to
            /// ConstructionBridges.People by Wiring/ConstructionLinks, which fills every bridge of the construction game.
            /// </summary>
            internal static int Build(BuildDemand d)
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

            private static int Openings() => Of(PopDemand.Openings, "DemandLinks.Openings");

            private static int TechSeekers() => Of(PopDemand.TechSeekers, "DemandLinks.TechSeekers");

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

            // A business's Guide page ends with where its customers come from while Living population is on.
            private static string Pointer(string featureId)
            {
                try
                {
                    return Settings.LivingPopulation && PopDemandGuide.Points(featureId) ? "reallife_pop_dem_pointer".GetLocalization() : null;
                }
                catch (Exception e)
                {
                    Fault("DemandLinks.Pointer", e);
                    return null;
                }
            }

            // A district's adult residents, its electorate; -1 for none.
            private static int Adults(string district)
            {
                try
                {
                    return Live ? CityDemand.Adults(district) : -1;
                }
                catch (Exception e)
                {
                    Fault("DemandLinks.Adults", e);
                    return -1;
                }
            }

            private static int Taxes() => Of(PopDemand.CouncilRevenue, "DemandLinks.Taxes");

            // The residents' satisfaction against the anchor day's as points of the mayor's approval; none before the anchor.
            private static float Residents()
            {
                try
                {
                    return Live && CityDemand.Anchored ? (CityDemand.Factor(PopDemand.Satisfaction) - PopDemandCatalogue.Neutral) * PopDemandCatalogue.SatisfactionApproval / 1000f : 0f;
                }
                catch (Exception e)
                {
                    Fault("DemandLinks.Residents", e);
                    return 0f;
                }
            }

            // The district the player stands in against the city's crime; 1 with none.
            private static float CrimeHere()
            {
                try
                {
                    return Live ? CityDemand.Crime(DistrictGame.PlayerDistrict()) / 1000f : 1f;
                }
                catch (Exception e)
                {
                    Fault("DemandLinks.CrimeHere", e);
                    return 1f;
                }
            }

            // The crimes a district's residents would report over the district map's days; 0 for none.
            private static int CrimeIn(string district)
            {
                try
                {
                    return Live ? (CityDemand.Crime(district) * PopDemandCatalogue.CrimeReports + 500) / 1000 : 0;
                }
                catch (Exception e)
                {
                    Fault("DemandLinks.CrimeIn", e);
                    return 0;
                }
            }

            // A district's homes and businesses against the anchor day's: broadband's market there; 0 for none.
            private static int Connections(string district)
            {
                try
                {
                    return Live ? Math.Max(1, CityDemand.Connections(district)) : 0;
                }
                catch (Exception e)
                {
                    Fault("DemandLinks.Connections", e);
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
