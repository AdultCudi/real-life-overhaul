using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Brands;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Planner;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Brands and franchises (BrandsBridges) with business pressure's footfall (the brand's share on top of the
        /// earlier pairings', never drawn twice on a new day), the city's price losses, fires, inspections and strikes at
        /// member shops, Justice's civil suits, the managers' prices and the Planner. Connected after the company and
        /// disconnected before it, so the footfall it wrapped goes back as found. Nothing per frame; nothing allocated.
        /// </summary>
        public static class BrandLinks
        {
            /// <summary>The brands' rows in the Planner, made by the director; this only hands them over.</summary>
            private sealed class BrandsSource : IPlannerSource
            {
                public string Id => "brands";

                public bool Available => Settings.Economy && Settings.EconomyBrands && BrandsDirector.Instance != null && BrandsDirector.Instance.Running;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    IReadOnlyList<BrandAlert> alerts = BrandsDirector.Instance.Alerts;
                    for (int i = 0; i < alerts.Count; i++)
                    {
                        BrandAlert alert = alerts[i];
                        PlannerEntry e = into.Add(Id, alert.A, alert.B, PlannerCategory.Business, alert.Day);
                        if (e != null && into.WantText)
                        {
                            e.Title = alert.Title;
                            e.What = alert.What;
                            e.Money = alert.Money;
                            e.IfMissed = alert.IfMissed;
                        }
                    }
                }
            }

            private static readonly BrandsSource Source = new BrandsSource();
            private static Func<BuildingRegistration, int> _ambientBefore;

            internal static void Connect()
            {
                BrandsBridges.Footfall = Footfall;
                BrandsBridges.Promoted = BusinessGame.Promoted;
                BrandsBridges.PriceLoss = PriceLoss;
                BrandsBridges.StrikeStarts = StrikeStarts;
                CompanyBridges.WritePrice = WritePrice;
                CompanyBridges.PricesKept = PricesKept;
                BrandsBridges.FileClaim = BrandClaims.File;
                BrandsBridges.ClaimOpen = BrandClaims.Open;
                BrandsBridges.WithdrawClaim = BrandClaims.Withdraw;
                _ambientBefore = BusinessGame.Ambient;
                BusinessGame.Ambient = Ambient;
                PlannerDirector.Instance?.AddSource(Source);
                FireLosses.Billed += Fire;
                BusinessEvents.Fined += Fined;
            }

            internal static void Disconnect()
            {
                BrandsBridges.Reset();
                CompanyBridges.WritePrice = null;
                CompanyBridges.PricesKept = null;
                FireLosses.Billed -= Fire;
                BusinessEvents.Fined -= Fined;
                BusinessGame.Ambient = _ambientBefore;   // the company's and the city's pairings take theirs off next
                _ambientBefore = null;
            }

            // A manager's price, through the brands' writer: never at another player's shop (BrandsGame.KeepPrice asks Going Public).
            private static bool WritePrice(BuildingRegistration reg, string item, float price, string district)
            {
                try
                {
                    return BrandsGame.KeepPrice(reg, item, price, district, false);
                }
                catch (Exception e)
                {
                    Fault("BrandLinks.WritePrice", e);
                    return false;
                }
            }

            private static bool PricesKept(string place)
            {
                try
                {
                    BrandsDirector brands = BrandsDirector.Instance;
                    return Settings.Economy && Settings.EconomyBrands && brands != null && brands.Running && brands.KeepsPrices(place);
                }
                catch (Exception e)
                {
                    Fault("BrandLinks.PricesKept", e);
                    return false;
                }
            }

            // The earlier pairings' part with the brand's share on top; business pressure bounds the sum.
            private static int Ambient(BuildingRegistration reg)
            {
                int before = 0;
                try
                {
                    before = _ambientBefore != null ? _ambientBefore(reg) : 0;
                }
                catch (Exception e)
                {
                    Fault("BrandLinks.Ambient (the pairings before)", e);
                }
                try
                {
                    return before + BrandsDirector.FootfallOf(reg);
                }
                catch (Exception e)
                {
                    Fault("BrandLinks.Ambient", e);
                    return before;
                }
            }

            private static bool Footfall(BuildingRegistration reg, bool redraw, bool newDay, out int own, out int written)
            {
                try
                {
                    bool drawn = newDay && (CityEconomyDirector.FootfallOf(reg) != 0 || CompanyDirector.FootfallOf(reg) != 0);
                    return BusinessDirector.Footfall(reg, redraw && !drawn, newDay, out own, out written);
                }
                catch (Exception e)
                {
                    Fault("BrandLinks.Footfall", e);
                    own = 0;
                    written = 0;
                    return false;
                }
            }

            private static int PriceLoss(BuildingRegistration reg)
            {
                try
                {
                    CityEconomyDirector city = CityEconomyDirector.Instance;
                    IReadOnlyList<CityShop> shops = city != null && city.Running ? city.Shops : null;
                    for (int i = 0; shops != null && i < shops.Count; i++)
                    {
                        if (ReferenceEquals(shops[i].Reg, reg))
                        {
                            return Math.Min(0, shops[i].Price);
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("BrandLinks.PriceLoss", e);
                }
                return 0;
            }

            // The company's own strike starts at a business today.
            private static bool StrikeStarts(string place, int day)
            {
                try
                {
                    CompanyDirector company = CompanyDirector.Instance;
                    CompanyState state = company != null && company.On(CompanyPart.Staff) ? company.State : null;
                    ShopFile shop = state != null ? state.Shop(place) : null;
                    return shop != null && shop.StrikeFrom == day;
                }
                catch (Exception e)
                {
                    Fault("BrandLinks.StrikeStarts", e);
                    return false;
                }
            }

            // A fire at a business of the player's: the brand it trades under is talked about.
            private static void Fire(BuildingRegistration reg, int bill)
            {
                try
                {
                    if (reg != null && BrandsDirector.Instance != null)
                    {
                        BrandsDirector.Instance.Heard(BusinessGame.Place(reg), Scandal.Fire);
                    }
                }
                catch (Exception e)
                {
                    Fault("BrandLinks.Fire", e);
                }
            }

            // A fine from a health or fire safety inspection: a failed inspection.
            private static void Fined(string kind, string place, int amount)
            {
                try
                {
                    if ((kind == BusinessCatalogue.Health || kind == BusinessCatalogue.Fire) && BrandsDirector.Instance != null)
                    {
                        BrandsDirector.Instance.Heard(place, Scandal.Inspection);
                    }
                }
                catch (Exception e)
                {
                    Fault("BrandLinks.Fined", e);
                }
            }
        }
    }
}
