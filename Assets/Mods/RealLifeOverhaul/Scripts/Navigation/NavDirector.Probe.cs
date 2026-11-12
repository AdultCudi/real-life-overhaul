using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The director's part for the city map's cards: how long the way from the player to a place is, by the roads the GPS follows, asked once when a place is picked. The
    /// searches are the card's own over the same roads, so the route under way is never disturbed. Cost: two bounded searches when a place is picked, nothing otherwise.
    /// </summary>
    public sealed partial class NavDirector
    {
        private const int ProbeSteps = 12000;   // search steps one probe may take before it says there is no way worth giving

        private RouteSearch _probeDrive, _probeWalk;
        private readonly List<int> _probeStarts = new List<int>(32);
        private readonly List<int> _probeGoals = new List<int>(32);
        private readonly List<int> _probePath = new List<int>(512);

        /// <summary>Minutes on the game's clock to a second of its time, as it has been running; the cards turn seconds into the minutes the clock will say.</summary>
        public float Rate => _rate;

        /// <summary>
        /// The way from the player to a point of the ground in metres, on foot (either way along a street) or by car (with the traffic), counted from the player's spot and to
        /// the place itself. False when the roads are not made yet or no way is found.
        /// </summary>
        public bool Probe(float gx, float gz, bool onFoot, out float metres)
        {
            metres = 0f;
            if (_broken || !_graph.Ready || _graph.Places == null || _graph.Drive == null)
            {
                return false;
            }
            Vector3 me = Helpers.PlayerHelper.PlayerController != null ? Helpers.PlayerHelper.GetCityPosition() : Vector3.zero;
            NavIndex places = _graph.Places;
            RouteSearch roads = _graph.Drive;
            _probeStarts.Clear();
            _probeGoals.Clear();
            places.Within(me.x, me.z, onFoot ? StartReachOnFoot : StartReachDriving, _probeStarts);
            if (_probeStarts.Count == 0)
            {
                int nearest = places.Nearest(me.x, me.z, FarReach);
                if (nearest >= 0)
                {
                    _probeStarts.Add(nearest);
                }
            }
            float reach = GoalReach;
            places.Within(gx, gz, GoalReach, _probeGoals);
            if (_probeGoals.Count == 0)
            {
                int nearest = places.Nearest(gx, gz, FarReach);
                if (nearest >= 0)
                {
                    _probeGoals.Add(nearest);
                    reach = Mathf.Sqrt((roads.X[nearest] - gx) * (roads.X[nearest] - gx) + (roads.Z[nearest] - gz) * (roads.Z[nearest] - gz)) + 1f;
                }
            }
            if (_probeStarts.Count == 0 || _probeGoals.Count == 0)
            {
                return false;
            }
            RouteSearch search = onFoot ? (_probeWalk ?? (_probeWalk = RouteSearch.Over(_graph.Walk, false))) : (_probeDrive ?? (_probeDrive = RouteSearch.Over(roads, false)));
            if (!search.Find(_probeStarts, _probeGoals, gx, roads.Y[_probeGoals[0]], gz, reach, ProbeSteps, _probePath) || _probePath.Count == 0)
            {
                return false;
            }
            float along = 0f;
            for (int i = 1; i < _probePath.Count; i++)
            {
                int a = _probePath[i - 1], b = _probePath[i];
                float dx = roads.X[b] - roads.X[a], dz = roads.Z[b] - roads.Z[a];
                along += Mathf.Sqrt(dx * dx + dz * dz);
            }
            int first = _probePath[0], last = _probePath[_probePath.Count - 1];
            along += Mathf.Sqrt((roads.X[first] - me.x) * (roads.X[first] - me.x) + (roads.Z[first] - me.z) * (roads.Z[first] - me.z));
            along += Mathf.Sqrt((roads.X[last] - gx) * (roads.X[last] - gx) + (roads.Z[last] - gz) * (roads.Z[last] - gz));
            metres = along;
            return true;
        }
    }
}
