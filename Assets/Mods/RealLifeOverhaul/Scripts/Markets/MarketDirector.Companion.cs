using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    public sealed partial class MarketDirector
    {
        /// <summary>A detached price-factor index without warming the gameplay lookup cache.</summary>
        public IReadOnlyDictionary<string, double> CompanionMaterialFactors()
        {
            var factors = new Dictionary<string, double>(StringComparer.Ordinal);
            if (Running && Settings.MarketsDerivatives)
            {
                foreach (RawMaterial material in _engine.State.Materials)
                {
                    if (material.Base > 0)
                    {
                        factors[material.Name] = material.Factor;
                    }
                }
            }
            return factors;
        }
    }
}
