using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Nav;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Delivery trips (LogisticsBridges) with the GPS's roads: a road between two of a route's places searched a slice of a
        /// few thousand points per call, on a search of its own, so neither the GPS's nor the online shops' is disturbed.
        /// </summary>
        public static class LogisticsLinks
        {
            private const float Reach = 70f;        // metres from a place within which a lane point starts or ends its road
            private const float ReachWide = 180f;   // ...and for a place with no lane that near
            private const int EndsMost = 24;        // lane points at either end
            private const int Slice = 4000;         // points a call goes on from: a fraction of a millisecond
            private const int SlicesMost = 40;      // a search that has not ended by then has no road worth the name
            private const int WaitsMost = 180;      // calls made while the GPS has no graph yet, after which the guess is kept

            private static readonly List<int> Starts = new List<int>(EndsMost);
            private static readonly List<int> Goals = new List<int>(EndsMost);
            private static readonly List<int> Path = new List<int>(512);

            private static RouteSearch _made, _search;
            private static Vector3 _from, _to;
            private static bool _begun;
            private static int _slices, _waits;

            internal static void Connect()
            {
                LogisticsBridges.Road = Road;
                _made = _search = null;
                _begun = false;
                _slices = _waits = 0;
            }

            internal static void Disconnect()
            {
                LogisticsBridges.Reset();
                _made = _search = null;
                _begun = false;
            }

            // A slice of the search for the road between two places: -1 still looking, 0 no road to be had, otherwise its metres.
            private static int Road(Vector3 from, Vector3 to)
            {
                try
                {
                    RouteSearch made = NavDirector.Roads;
                    if (made == null || made.EdgeStart == null)
                    {
                        return NavDirector.Instance != null && ++_waits < WaitsMost ? -1 : 0;   // the GPS makes its graph in a city's first moments
                    }
                    if (!ReferenceEquals(made, _made))
                    {
                        _made = made;
                        _search = new RouteSearch(made);
                        _search.SetEdges(made.EdgeStart, made.EdgeTo, made.EdgeExtra);
                        _begun = false;
                    }
                    if (!_begun || from != _from || to != _to)
                    {
                        Near(from, Starts);
                        Near(to, Goals);
                        _from = from;
                        _to = to;
                        _slices = 0;
                        _begun = Starts.Count > 0 && Goals.Count > 0 && _search.Begin(Starts, Goals, to.x, to.y, to.z, ReachWide);
                        if (!_begun)
                        {
                            return 0;
                        }
                    }
                    RouteSearch.Outcome outcome = _search.Advance(_search.Expanded + Slice, Path);
                    if (outcome == RouteSearch.Outcome.Searching)
                    {
                        if (++_slices < SlicesMost)
                        {
                            return -1;
                        }
                        _begun = false;
                        return 0;
                    }
                    _begun = false;
                    if (outcome != RouteSearch.Outcome.Found)
                    {
                        return 0;
                    }
                    float metres = 0f;
                    for (int i = 1; i < Path.Count; i++)
                    {
                        float x = _search.X[Path[i]] - _search.X[Path[i - 1]], z = _search.Z[Path[i]] - _search.Z[Path[i - 1]];
                        metres += Mathf.Sqrt(x * x + z * z);
                    }
                    Path.Clear();
                    return Mathf.Max(1, Mathf.RoundToInt(metres));
                }
                catch (Exception e)
                {
                    Fault("LogisticsLinks.Road", e);
                    _begun = false;
                    return 0;
                }
            }

            // The lane points nearest a place, a couple of dozen at most: one pass over the graph when a search begins.
            private static void Near(Vector3 place, List<int> into)
            {
                into.Clear();
                float[] x = _search.X, z = _search.Z;
                int[] edges = _search.EdgeStart;
                float reach = Reach * Reach;
                for (int round = 0; round < 2 && into.Count == 0; round++, reach = ReachWide * ReachWide)
                {
                    for (int i = 0; i < _search.Count && into.Count < EndsMost; i++)
                    {
                        float dx = x[i] - place.x, dz = z[i] - place.z;
                        if (dx * dx + dz * dz <= reach && edges[i + 1] > edges[i])
                        {
                            into.Add(i);
                        }
                    }
                }
            }
        }
    }
}
