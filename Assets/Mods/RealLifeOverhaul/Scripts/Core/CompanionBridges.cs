using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>Read-only capability providers, connected by Wiring. No provider means no claim of support.</summary>
    public static class CompanionBridges
    {
        public static readonly Dictionary<string, Func<CompanionModule>> Readers
            = new Dictionary<string, Func<CompanionModule>>(StringComparer.Ordinal);
    }
}
