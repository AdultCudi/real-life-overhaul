using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul
{
    /// <summary>Explicit units and addresses for owner projections; no game state or caches.</summary>
    public static class CompanionValues
    {
        /// <summary>An address as the contract writes it.</summary>
        public static Dictionary<string, object> Address(string street, int number)
        {
            return CompanionSnapshot.Row("street", street, "number", number);
        }

        /// <summary>Orders addresses by street, then by number, the same on every machine.</summary>
        internal static int CompareAddress(string leftStreet, int leftNumber, string rightStreet, int rightNumber)
        {
            int street = string.CompareOrdinal(leftStreet, rightStreet);
            return street != 0 ? street : leftNumber.CompareTo(rightNumber);
        }

        /// <summary>
        /// A price as an exact decimal string; a value that is not finite throws, so only its module fails.
        /// </summary>
        public static string Decimal(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount))
            {
                throw new ArgumentException("Nonfinite companion price");
            }
            return amount.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A factor as an exact decimal string; a value that is not finite throws, so only its module fails.
        /// </summary>
        public static string Decimal(double amount)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount))
            {
                throw new ArgumentException("Nonfinite companion price");
            }
            return amount.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>Money in whole cents, rounded half away from zero.</summary>
        public static long Cents(float amount)
        {
            return checked((long)Math.Round((double)amount * 100, MidpointRounding.AwayFromZero));
        }
    }
}
