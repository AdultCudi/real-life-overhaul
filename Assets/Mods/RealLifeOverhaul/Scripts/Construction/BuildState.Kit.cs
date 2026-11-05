using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>One district's land asks a square metre, a figure a month in a ring of twelve (<see cref="AskRules"/>), saved in <see cref="BuildStore"/>'s "^a:" part.</summary>
    public sealed class AskRing
    {
        public string District = string.Empty;
        public readonly int[] Months = new int[AskRules.RingMonths];
        public readonly long[] Asks = new long[AskRules.RingMonths];
        public int Count;

        /// <summary>The newest ask, or 0.</summary>
        public long Latest => Count > 0 ? Asks[Count - 1] : 0L;

        /// <summary>The change of the newest ask on the month before, in thousandths; 0 without two neighbouring months.</summary>
        public int Change => AskRules.Change(Months, Asks, Count);
    }

    public sealed partial class BuildState
    {
        /// <summary>The land's asks by district: one ring for each, at most <see cref="AskRules.DistrictsMost"/>.</summary>
        public readonly List<AskRing> Asks = new List<AskRing>(2);

        /// <summary>The ring of a district's asks; made when asked for and there is room, else null.</summary>
        public AskRing AskOf(string district, bool make)
        {
            for (int i = 0; i < Asks.Count; i++)
            {
                if (Asks[i].District == district)
                {
                    return Asks[i];
                }
            }
            if (!make || string.IsNullOrEmpty(district) || Asks.Count >= AskRules.DistrictsMost)
            {
                return null;
            }
            var ring = new AskRing { District = district };
            Asks.Add(ring);
            return ring;
        }
    }
}
