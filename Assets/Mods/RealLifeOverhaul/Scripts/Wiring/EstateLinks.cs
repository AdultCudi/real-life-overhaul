using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Planner;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The property business (EstateBridges) with the council's rent and permits policies, the event calendar, a
        /// hotel's reception and the Planner. Not linked, for want of an entry on the other side: a landlord's claim in
        /// the courts (Justice files claims against the player only) and a vote on a change of use (Civic shows its own
        /// sites only).
        /// </summary>
        public static class EstateLinks
        {
            private static readonly EstateSource Source = new EstateSource();

            internal static void Connect()
            {
                EstateBridges.RentLever = () => EstateRules.FromCouncil(Policy(Lever.Rent));
                EstateBridges.PermitLever = () => Policy(Lever.Permits);
                EstateBridges.EventOn = EventOn;
                HotelReception.Owner = EstateDirector.OwnsHotel;
                HotelReception.Books = EstateDirector.OpenBooks;
                PlannerDirector.Instance?.AddSource(Source);
            }

            internal static void Disconnect()
            {
                EstateBridges.Reset();   // the Planner's director goes with the city, and its sources with it
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

            /// <summary>The property business's rows in the Planner, made by EstateDirector.Alerts; this only hands them over.</summary>
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
