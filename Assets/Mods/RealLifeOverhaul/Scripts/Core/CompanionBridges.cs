using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>Read-only capability providers, filled by Wiring. A capability with no provider is not claimed.</summary>
    internal static class CompanionBridges
    {
        /// <summary>Each capability's reader by its module name; called only during a capture.</summary>
        public static readonly Dictionary<string, Func<CompanionModule>> Readers
            = new Dictionary<string, Func<CompanionModule>>(StringComparer.Ordinal);
    }
}
