namespace RealLifeOverhaul.Economy
{
    public sealed partial class EstateDirector
    {
        /// <summary>
        /// Refuses a rent change where the property business holds the building; null when it does not. Read only
        /// during an ownership query, nothing per frame.
        /// </summary>
        public object CompanionRentOwnership(BuildingRegistration reg)
        {
            if (!_running || _broken || _state == null || !On(EstatePart.Property))
            {
                return RealLifeOverhaul.CompanionOwnership.Decision("unknown", "rlo.estate", "rent", "estate-state-unavailable");
            }
            string place = EstateGame.Place(reg.StreetName, reg.StreetNumber);
            foreach (EstateHolding holding in _state.Holdings)
            {
                if (holding.Place == place && (holding.Managed || holding.Busy || holding.AnyLet || holding.SaleDay > 0))
                {
                    return RealLifeOverhaul.CompanionOwnership.Decision("refused", "rlo.estate", "rent",
                        "estate-controls-property", "estate-may-rewrite-property");
                }
            }
            return null;
        }
    }
}
