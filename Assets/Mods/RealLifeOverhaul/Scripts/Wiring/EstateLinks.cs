using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Planner;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The property business with the rest of the mod (Economy/Estate/EstateBridges). It calls no other feature
        /// and none calls it: what they have to do with each other is here.
        ///
        ///   RentLever     Civic/CivicPolicy.Position(Lever.Rent): rent controls take a little off the rent of homes
        ///                 and lengthen notice; freed rents do the opposite
        ///   PermitLever   CivicPolicy.Position(Lever.Permits): how likely the council is to allow a change of use,
        ///                 and how fast a site works (Civic's own site speed)
        ///   EventOn       Events/EventCalendar.For(day): a parade or the fireworks fill a hotel and hire a car
        ///   reception     Travel/HotelReception: at a hotel the player holds, the desk has "The books" and the room
        ///                 is free (HotelReception.Owner, HotelReception.Books)
        ///   the Planner   a source of its own (<see cref="EstateSource"/>): the rows the property business has made
        ///
        /// The city's economy is read by the property business directly (Economy/City/CityEconomy is the same
        /// tree's public surface): the cycle, prices, the bank's rate, a district's income. Car records
        /// (Transport/CarGarage) are written when a hire ends: distance, tyres, brakes.
        ///
        /// Not linked, because the features have no entry for it: a landlord's possession claim in the courts
        /// (Justice files claims against the player only), business insurance for a building that is no business of
        /// the player's (the company's policies are per business), a site notice and a vote for a change of use
        /// (Civic shows its own sites only), a report to the police of a car that was not brought back, and the
        /// company's quarterly return for rent and gains (rent goes through the game's own rent transaction and is
        /// taxed as the game taxes it; a gain is taxed by the property business itself).
        /// </summary>
        public static class EstateLinks
        {
            private static readonly EstateSource Source = new EstateSource();

            internal static void Connect()
            {
                EstateBridges.RentLever = () => Policy(Lever.Rent);
                EstateBridges.PermitLever = () => Policy(Lever.Permits);
                EstateBridges.EventOn = EventOn;
                HotelReception.Owner = EstateDirector.OwnsHotel;
                HotelReception.Books = EstateDirector.OpenBooks;
                PlannerDirector.Instance?.AddSource(Source);
            }

            internal static void Disconnect()
            {
                EstateBridges.Reset();   // (the Planner's director goes with the city, and its sources with it)
                HotelReception.Owner = null;
                HotelReception.Books = null;
            }

            private static int Policy(Lever lever)
            {
                try
                {
                    return CivicPolicy.Position(lever);
                }
                catch (Exception e)
                {
                    Fault("EstateLinks.Policy", e);
                    return 0;
                }
            }

            private static int EventOn(int day)
            {
                try
                {
                    return (int)Events.EventCalendar.For(day).Kind;
                }
                catch (Exception e)
                {
                    Fault("EstateLinks.EventOn", e);
                    return 0;
                }
            }

            /// <summary>
            /// The property business's rows in the Planner: applicants who wait, rent that is behind, notice that
            /// runs out, a court's order, works that finish, an offer for a building, a sale that completes, an
            /// instruction to answer, a group that asks at a hotel, an inspection that would be failed, and a hire
            /// car that waits for its space. The rows are made by the property business on the day event and when
            /// the player changes something (EstateDirector.Alerts); this only hands them over.
            /// </summary>
            private sealed class EstateSource : IPlannerSource
            {
                public string Id => "estate";

                public bool Available => Settings.Economy && (Settings.Estate || Settings.EstateHotel || Settings.EstateHire) && EstateDirector.Instance != null && EstateDirector.Instance.Running;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    IReadOnlyList<EstateAlert> alerts = EstateDirector.Instance.Alerts;
                    for (int i = 0; i < alerts.Count; i++)
                    {
                        EstateAlert alert = alerts[i];
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
        }
    }
}
