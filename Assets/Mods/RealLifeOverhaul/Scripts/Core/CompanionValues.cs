using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul
{
    /// <summary>Explicit units and addresses for owner projections; no game state or caches.</summary>
    public static class CompanionValues
    {
        public static Dictionary<string, object> Address(string street, int number)
        {
            return CompanionSnapshot.Row("street", street, "number", number);
        }

        public static string Decimal(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount))
            {
                throw new ArgumentException("Nonfinite companion price");
            }
            return amount.ToString("R", CultureInfo.InvariantCulture);
        }

        public static string Decimal(double amount)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount))
            {
                throw new ArgumentException("Nonfinite companion price");
            }
            return amount.ToString("R", CultureInfo.InvariantCulture);
        }

        public static long Cents(float amount)
        {
            return checked((long)Math.Round((double)amount * 100, MidpointRounding.AwayFromZero));
        }
    }
}
