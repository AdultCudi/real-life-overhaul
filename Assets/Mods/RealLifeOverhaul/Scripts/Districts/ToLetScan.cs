using System;
using System.Collections.Generic;
using Buildings;

namespace RealLifeOverhaul.Districts
{
    /// <summary>
    /// The premises to let in each neighbourhood (the game's own id): a building for rent that nobody rents and that holds no business. The city's
    /// buildings are walked a few hundred a step, on the day's round (Flare's day steps), so no frame pays for the whole city; the finished map is what
    /// the pages read. COST: <c>most</c> buildings a step, one pass a game day.
    /// </summary>
    internal sealed class ToLetScan
    {
        private const string EmptyBusiness = "ba:businesstype_empty";

        private CityBuildingController[] _all;
        private int _at;
        private readonly Dictionary<string, int> _into = new Dictionary<string, int>(16, StringComparer.Ordinal);

        /// <summary>The last finished map: premises to let by neighbourhood id.</summary>
        public readonly Dictionary<string, int> Done = new Dictionary<string, int>(16, StringComparer.Ordinal);

        /// <summary>A pass starts over the city as it is now.</summary>
        public void Begin()
        {
            _into.Clear();
            _at = 0;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            _all = GameDlc.Open(city);
        }

        /// <summary>Walks up to <paramref name="most"/> buildings on; true when the pass is finished and <see cref="Done"/> holds its map.</summary>
        public bool Step(int most)
        {
            if (_all == null)
            {
                return true;
            }
            int end = Math.Min(_all.Length, _at + Math.Max(1, most));
            for (; _at < end; _at++)
            {
                CityBuildingController c = _all[_at];
                BuildingRegistration reg = c != null ? c.buildingRegistration : null;
                if (reg == null || c.building == null || string.IsNullOrEmpty(c.building.Neighbourhood) || !reg.AvailableForRent || reg.RentedByPlayer
                    || !(string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == EmptyBusiness))
                {
                    continue;
                }
                _into.TryGetValue(c.building.Neighbourhood, out int n);
                _into[c.building.Neighbourhood] = n + 1;
            }
            if (_at < _all.Length)
            {
                return false;
            }
            Done.Clear();
            foreach (KeyValuePair<string, int> kv in _into)
            {
                Done[kv.Key] = kv.Value;
            }
            _all = null;
            return true;
        }
    }
}
