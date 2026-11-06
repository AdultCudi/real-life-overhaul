using System;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// A police report handed over at a station's front desk (Law.Bridges.PoliceReport) goes to the insurers: for a break-in, the
        /// newest theft claim of the player's businesses that is still to be paid pays in full; for a car break-in or an accident,
        /// the next repair the car's insurer pays is paid without the excess. Answered when the desk asks, nothing polled.
        /// </summary>
        public static class ReportLinks
        {
            internal static void Connect()
            {
                Law.Bridges.PoliceReport = Handed;
            }

            internal static void Disconnect()
            {
                Law.Bridges.PoliceReport = null;
            }

            private static void Handed(string kind, string reference)
            {
                try
                {
                    if (kind == Law.Bridges.BreakIn)
                    {
                        CompanyDirector.Instance?.PoliceReport(Law.Bridges.ReportDays);
                    }
                    else if (kind == Law.Bridges.CarBreakIn || kind == Law.Bridges.Accident)
                    {
                        CarGarage.Reported(TimeHelper.CurrentDay);
                    }
                }
                catch (Exception e)
                {
                    Fault("ReportLinks.Handed", e);
                }
            }
        }
    }
}
