using System;
using RealLifeOverhaul.Offices;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Empty shops as offices with the features that claim buildings. The shops are chosen only once the utilities
        /// office, the courthouse, the police stations and the tech headquarters have chosen theirs (or are off), so the
        /// choice is the same on every machine. The player's two largest offices (CityReserves) are reserved from the
        /// start, whatever the options, so no feature takes them; they are not refused to the player.
        /// </summary>
        public static class OfficeLinks
        {
            internal static void Connect()
            {
                Conversions.OthersPicked = OthersPicked;
                for (int i = 0; i < CityReserves.PlayerOfficeStreets.Length; i++)
                {
                    SpecialBuildings.Reserve(CityReserves.PlayerOfficeId + (i + 1), CityReserves.PlayerOfficeStreets[i], CityReserves.PlayerOfficeNumbers[i], null);
                }
            }

            internal static void Disconnect()
            {
                Conversions.OthersPicked = null;
                for (int i = 0; i < CityReserves.PlayerOfficeStreets.Length; i++)
                {
                    SpecialBuildings.Unreserve(CityReserves.PlayerOfficeId + (i + 1));
                }
            }

            private static bool OthersPicked()
            {
                try
                {
                    Utilities.UtilitiesDirector utilities = Utilities.UtilitiesDirector.Instance;
                    bool office = !Settings.Utilities || (utilities != null && utilities.Office != null && utilities.Office.Searched);
                    JusticeDirector justice = JusticeDirector.Instance;
                    bool court = !(JusticeSettings.Enabled && JusticeSettings.Courthouse) || (justice != null && justice.Court != null && justice.Court.Searched);
                    bool law = !Settings.LawPrecincts || Law.LawDirector.PrecinctsChosen;
                    Tech.TechSites sites = Tech.TechSites.Instance;
                    bool hq = !Settings.TechSiteHq || (sites != null && sites.Hq != null && sites.Hq.Picked);
                    return office && court && law && hq;
                }
                catch (Exception e)
                {
                    Fault("OfficeLinks.OthersPicked", e);
                    return true;
                }
            }
        }
    }
}
