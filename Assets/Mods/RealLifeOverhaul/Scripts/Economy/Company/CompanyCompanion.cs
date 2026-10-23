using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Ordinary hourly wage observations, using the same rounding as the company pays.</summary>
    public static class CompanyCompanion
    {
        /// <summary>One employee's row: base, the city's part and the raise, in hundredths an hour, as the company pays them.</summary>
        public static Dictionary<string, object> Wage(string id, int baseCents, int index, int raiseCents)
        {
            int indexedCents = CompanyCatalogue.Share(baseCents, index);
            return CompanionSnapshot.Row("employeeId", id, "baseHourlyCents", baseCents,
                "cityHourlyCents", indexedCents - baseCents, "raiseHourlyCents", raiseCents,
                "ordinaryHourlyCents", (long)indexedCents + raiseCents);
        }
    }
}
