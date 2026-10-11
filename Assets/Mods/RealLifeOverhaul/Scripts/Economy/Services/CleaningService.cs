using Streets;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The cleaning service's face for the rest of the mod: one question. The staff rota planner asks it so that a
    /// business the service looks after is not also given a cleaning shift of its own.
    ///
    ///     CleaningService.Covers(reg.Address)      true while the service cleans that business of the player's
    ///
    /// Answered from what the director already keeps: a look through the round's list (a few dozen entries at most).
    /// False while the cleaning service is off, has no crew, or the business is not on its list.
    /// </summary>
    public static class CleaningService
    {
        /// <summary>True while the cleaning service looks after the business at this address and has somebody to send.</summary>
        public static bool Covers(Address address)
        {
            ServicesDirector d = ServicesDirector.Instance;
            ServicesState state = d != null && d.CleaningOn ? d.State : null;
            if (state == null || address == null || state.Crew.Count == 0 || state.Clients.Count == 0)
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

        /// <summary>The same by a place as business pressure and "running a business" write one.</summary>
        public static bool Covers(string place)
        {
            ServicesDirector d = ServicesDirector.Instance;
            ServicesState state = d != null && d.CleaningOn ? d.State : null;
            CleaningClient c = state != null && state.Crew.Count > 0 ? state.Client(place) : null;
            return c != null && !c.Outside;
        }

        // A place is the street, escaped, a '~' and the number. The number is compared first: the street is looked at only where it matches.
        private static bool Same(string place, Address address)
        {
            int at = place.LastIndexOf('~');
            int number;
            if (at <= 0 || !int.TryParse(place.Substring(at + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out number) || number != address.streetNumber)
            {
                return false;
            }
            return string.Equals(System.Uri.UnescapeDataString(place.Substring(0, at)), address.streetName, System.StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Selling a business's face for the rest of the mod: what the tax return needs.
    ///
    ///     BusinessSales.GainsBetween(fromDay, toDay)   the capital gains of the sales completed on those days: for each,
    ///                                                  the price above the worth of the stock and furniture sold with it
    ///
    /// Answered from the notes the director keeps of the last sales (two dozen at most). Nothing while the feature is
    /// not running.
    /// </summary>
    public static class BusinessSales
    {
        public static int GainsBetween(int fromDay, int toDay)
        {
            ServicesDirector d = ServicesDirector.Instance;
            return d != null ? SaleRules.Gains(d.State, fromDay, toDay) : 0;
        }
    }
}
