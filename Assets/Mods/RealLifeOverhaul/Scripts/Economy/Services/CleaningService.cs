using System;
using System.Globalization;
using Streets;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Whether the cleaning service looks after a business of the player's, for the rota planner and interiors; false while it is off or has no crew.</summary>
    public static class CleaningService
    {
        public static bool Covers(Address address)
        {
            ServicesDirector d = ServicesDirector.Instance;
            ServicesState state = d != null && d.CleaningOn ? d.State : null;
            if (state == null || address == null || state.Crew.Count == 0)
            {
                return false;
            }
            for (int i = 0; i < state.Clients.Count; i++)
            {
                CleaningClient c = state.Clients[i];
                if (!c.Outside && Same(c.Place, address))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The same by a place, as business pressure and "running a business" write one.</summary>
        public static bool Covers(string place)
        {
            ServicesDirector d = ServicesDirector.Instance;
            ServicesState state = d != null && d.CleaningOn ? d.State : null;
            CleaningClient c = state != null && state.Crew.Count > 0 ? state.Client(place) : null;
            return c != null && !c.Outside;
        }

        // A place is the escaped street, a '~' and the number; the number is compared first.
        private static bool Same(string place, Address address)
        {
            int at = place.LastIndexOf('~');
            if (at <= 0 || !int.TryParse(place.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number != address.streetNumber)
            {
                return false;
            }
            return string.Equals(Uri.UnescapeDataString(place.Substring(0, at)), address.streetName, StringComparison.Ordinal);
        }
    }

    /// <summary>Selling a business's figure for the tax return: the capital gains of sales completed between two days, both counted.</summary>
    public static class BusinessSales
    {
        public static int GainsBetween(int fromDay, int toDay)
        {
            ServicesDirector d = ServicesDirector.Instance;
            return d != null ? SaleRules.Gains(d.State, fromDay, toDay) : 0;
        }
    }
}
