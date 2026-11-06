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
        /// Interiors and decor with business pressure (footfall, complaints, inspections), the city's prices, the cleaning
        /// service, brands, Standing, the staff, the tech sector, Flare and the paper. Nothing per frame: every answer is a
        /// lookup in what the feature worked out this morning.
        /// </summary>
        public static class InteriorLinks
        {
            internal static void Connect()
            {
                InteriorBridges.Footfall = (BuildingRegistration reg, bool redraw, bool newDay, out int own, out int written) => BusinessDirector.Footfall(reg, redraw, newDay, out own, out written);
                InteriorBridges.Cleaned = CleaningService.Covers;
                InteriorBridges.Complain = Complain;
                InteriorBridges.Story = Paper.Print;
                InteriorBridges.Striking = (place, name) => FlareDirector.Instance?.Happened(FlareSource.Interior, -1, place, name, 0);
                InteriorBridges.Income = district => CityEconomy.Running ? Mathf.RoundToInt(CityEconomy.DistrictIncome(district)) : 0;
                BusinessGame.Interior = InteriorDirector.FootfallOf;
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
                BusinessGame.Interior = null;
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
