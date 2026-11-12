using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What stands behind the meters of a tenant's satisfaction on a unit's page: repairs (an open call), value for rent (the rent against
    /// the market) and condition (the building). Each is 0 to 100 and agrees with <see cref="EstateRules.HappyTarget"/>'s own terms; a
    /// term the rules do not have (heating, noise) is not made up. Game-free.
    /// </summary>
    public static class HappyTerms
    {
        public static void Of(EstateHolding h, int unit, int day, EstateClimate c, out int repairs, out int value, out int condition)
        {
            EstateUnit u = h.Unit(unit);
            repairs = value = condition = 0;
            if (u == null || !u.Let)
            {
                return;
            }
            int weight = EstateRules.CallWeight(u, day);
            repairs = EstateCatalogue.Clamp(100 - weight * 100 / Math.Max(1, EstateCatalogue.HappyCallMost), 0, 100);
            int market = EstateRules.UnitRent(h, c);
            if (market > 0)
            {
                long share = (long)u.Rent * 1000 / market;
                int term = EstateCatalogue.Clamp((int)((1000 - share) * EstateCatalogue.HappyRentSlope / 1000), EstateCatalogue.HappyRentLeast, EstateCatalogue.HappyRentMost);
                value = EstateCatalogue.Clamp((term - EstateCatalogue.HappyRentLeast) * 100 / (EstateCatalogue.HappyRentMost - EstateCatalogue.HappyRentLeast), 0, 100);
            }
            else
            {
                value = 50;
            }
            condition = EstateCatalogue.Clamp((EstateCatalogue.Clamp(h.Cond, 0, 1000) + h.Level * 60) / 10, 0, 100);
        }
    }
}
