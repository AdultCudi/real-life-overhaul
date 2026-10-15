using System;
using RealLifeOverhaul.Offices;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Empty shops as offices (Scripts/Offices) with the rest of the mod:
        ///
        ///   OthersPicked  the utilities office, the courthouse, the police stations and the tech companies' headquarters
        ///                 have chosen their buildings (or are off), so that the empty shops made offices are chosen from
        ///                 a city whose claims are settled: the police stations may take a shop, and the offices those
        ///                 features take are not the player's to let. The same on every machine.
        ///
        /// The other way round, every feature that chooses an office building asks Conversions.Holds itself (one line in its
        /// own choice), so none of them takes a shop made an office. Everything is read from what those features already
        /// keep in public; nothing of theirs is changed.
        ///
        ///   the player's offices  the two largest office buildings a player can rent or buy (Core/CityReserves) are
        ///                 reserved in the book of the mod's buildings from the start, before any feature chooses, so every
        ///                 feature passes them over; they are not refused to the player. Always, whatever the options.
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
