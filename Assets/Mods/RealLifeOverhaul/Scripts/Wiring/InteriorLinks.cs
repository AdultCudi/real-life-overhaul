using System;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Brands;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Interiors;
using RealLifeOverhaul.Tech;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// "Interiors and decor matter" (Scripts/Interiors) with the rest of the mod. The feature calls no other feature
        /// and none calls it; what they have to do with each other is here, one line each.
        ///
        /// What it reads:
        ///   Footfall      business pressure's writer of a business's footfall (BusinessDirector.Footfall)
        ///   Cleaned       the cleaning service looks after a business (CleaningService.Covers): its wear builds half as fast
        ///   Income        a district's income level from the city's economy (CityEconomy.DistrictIncome), for its standard
        ///
        /// What it does elsewhere:
        ///   Ambient       its part of a business's customers (-8% to +8%) is added to <see cref="BusinessGame.Ambient"/>,
        ///                 on top of the city's, the company's, the brands' and Flare's, and under business pressure's own
        ///                 bounds for the sum (BusinessCatalogue.ClampAmbient)
        ///   Bears         the city's economy judges a business's prices against what its district bears with its
        ///                 interior's part on it (CityBridges.Bears: up to 3% more, 2% less)
        ///   Complain      a business short of its standard may get a customer's complaint, business pressure's own issue
        ///                 (BusinessDirector.StartIssue, and BusinessDirector.Shabby so that its rules let it start)
        ///   Inspected     a worn place looks dirtier to the health inspector (BusinessDirector.Inspected)
        ///   Interior      the brands' standards (BrandsBridges.Interior) and Standing's customers (FameBridges.Interior)
        ///   Comfort       the company's staff (CompanyBridges.Comfort) and the professionals' morale (TechBridges.Comfort)
        ///   Office        the jobs board's esteem (TechBridges.Office) and a firm's bids (TechBridges.OfficeBid)
        ///   Story         a striking or a shabby interior in the City Ledger (Paper.Print); a striking one a Flare draft
        ///                 (FlareSource.Interior)
        ///
        /// COST: nothing per frame; every answer is a lookup in what the feature worked out this morning.
        /// </summary>
        public static class InteriorLinks
        {
            private static Func<BuildingRegistration, int> _ambientBefore;

            internal static void Connect()
            {
                InteriorBridges.Footfall = (BuildingRegistration reg, bool redraw, bool newDay, out int own, out int written) => BusinessDirector.Footfall(reg, redraw, newDay, out own, out written);
                InteriorBridges.Cleaned = CleaningService.Covers;
                InteriorBridges.Complain = Complain;
                InteriorBridges.Story = Paper.Print;
                InteriorBridges.Striking = (place, name) => FlareDirector.Instance?.Happened(FlareSource.Interior, -1, place, name, 0);
                InteriorBridges.Income = district => CityEconomy.Running ? Mathf.RoundToInt(CityEconomy.DistrictIncome(district)) : 0;
                _ambientBefore = BusinessGame.Ambient;
                BusinessGame.Ambient = Ambient;
                CityBridges.Bears = InteriorDirector.BearsOf;
                BusinessDirector.Shabby = InteriorDirector.Shabby;
                BusinessDirector.Inspected = InteriorDirector.Inspected;
                BrandsBridges.Interior = reg =>
                {
                    InteriorShop s = InteriorDirector.At(reg);
                    return s != null ? s.BrandPoints : 0;
                };
                FameBridges.Interior = place =>
                {
                    InteriorShop s = InteriorDirector.At(place);
                    return s != null ? s.StandingPoints : 0;
                };
                CompanyBridges.Comfort = place =>
                {
                    InteriorShop s = InteriorDirector.At(place);
                    return s != null ? s.Comfort : 0;
                };
                TechBridges.Comfort = place =>
                {
                    InteriorShop s = InteriorDirector.At(place);
                    return s != null ? InteriorRules.Morale(s.Comfort) : 0;
                };
                TechBridges.Office = InteriorDirector.OfficeEsteem;
                TechBridges.OfficeBid = place =>
                {
                    InteriorShop s = InteriorDirector.At(place);
                    return s != null ? s.OfficeBid : 0;
                };
            }

            internal static void Disconnect()
            {
                InteriorBridges.Reset();
                BusinessGame.Ambient = _ambientBefore;   // (Flare's, the brands', the company's and the city's own pairings take theirs off in a moment)
                _ambientBefore = null;
                CityBridges.Bears = null;
                BusinessDirector.Shabby = null;
                BusinessDirector.Inspected = null;
                BrandsBridges.Interior = null;
                FameBridges.Interior = null;
                CompanyBridges.Comfort = null;
                TechBridges.Comfort = null;
                TechBridges.Office = null;
                TechBridges.OfficeBid = null;
            }

            private static int Ambient(BuildingRegistration reg)
            {
                return (_ambientBefore != null ? _ambientBefore(reg) : 0) + InteriorDirector.FootfallOf(reg);
            }

            private static bool Complain(BuildingRegistration reg)
            {
                try
                {
                    BusinessDirector director = BusinessDirector.Instance;
                    return director != null && director.StartIssue(BusinessCatalogue.Complaint, reg);
                }
                catch (Exception e)
                {
                    Fault("InteriorLinks.Complain", e);
                    return false;
                }
            }
        }
    }
}
