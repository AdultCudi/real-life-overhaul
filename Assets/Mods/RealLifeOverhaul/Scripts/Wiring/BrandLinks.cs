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
        /// Brands and franchises with the rest of the mod (Economy/Brands/BrandsBridges). Brands call no other feature
        /// and none calls them: what they have to do with each other is here.
        ///
        ///   Footfall      a business's promotion figure, written by business pressure's own mechanism
        ///                 (BusinessDirector.Footfall): the mod's one writer of that figure, whoever asks.
        ///                 <see cref="BusinessGame.Ambient"/> is asked for what the city's economy, the company and
        ///                 a brand add together (this pairing puts the brand's share on top of whatever answered
        ///                 before it), and business pressure keeps the sum inside -25% to +15%. On a new day a
        ///                 business whose customers the city's economy or the company has already had drawn for it
        ///                 is not drawn a second time for the brand
        ///   PriceLoss     the customers the city's economy takes from a business for its prices (CityShop.Price):
        ///                 what a trusted brand gets some of back
        ///   scandals      a fire's bill (Events/FireLosses) and an inspector's fine (Business/BusinessEvents) at a
        ///                 member shop are told to the brand; the loss itself is the other feature's own and is not
        ///                 changed. A strike is asked for on the day event: the company's own, by its public state
        ///   the courts    a franchisee's dispute, filed as one of Justice's own civil suits (Economy/Brands/BrandClaims)
        ///   the Planner   a source of its own (<see cref="BrandsSource"/>): the rows the brands have made for the day
        ///
        /// Read from the city's economy without a bridge, as every feature may: the cycle, a district's income and the
        /// price index (Economy/City/CityEconomy). Connected after the company and disconnected before it, so that the
        /// footfall answer it wrapped is put back as it was found.
        /// </summary>
        public static class BrandLinks
        {
            /// <summary>
            /// The brands' rows in the Planner, as a source of its own: an applicant on the last day they wait, what a
            /// franchisee asks on the last day to answer, a renewal on the day the agreement runs out, an inspection
            /// that is due, and a shop that is outside what its city chain asks. The rows are made by the director
            /// on the day event and when the player changes something; this only hands them over, and changes nothing.
            /// </summary>
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
                FireLosses.Billed -= Fire;
                BusinessEvents.Fined -= Fined;
                BusinessGame.Ambient = _ambientBefore;   // (the company's and the city's own pairings take theirs off in a moment)
                _ambientBefore = null;
            }

            // What was asked for before this pairing, and the brand's share on top. Business pressure keeps the sum inside its own bounds.
            private static int Ambient(BuildingRegistration reg)
            {
                return (_ambientBefore != null ? _ambientBefore(reg) : 0) + BrandsDirector.FootfallOf(reg);
            }

            private static bool Footfall(BuildingRegistration reg, bool redraw, bool newDay, out int own, out int written)
            {
                bool drawn = newDay && (CityEconomyDirector.FootfallOf(reg) != 0 || CompanyDirector.FootfallOf(reg) != 0);
                return BusinessDirector.Footfall(reg, redraw && !drawn, newDay, out own, out written);
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

            // The company's own strike starts at a business today (its state is public; its place is written as the brand's is).
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

            // A fire's repair bill at a business of the player's: the brand it trades under is talked about.
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

            // A fine from one of business pressure's inspections (health, fire safety): a failed inspection.
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
