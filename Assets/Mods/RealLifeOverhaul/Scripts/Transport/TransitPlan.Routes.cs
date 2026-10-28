using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The bus network's routes of a district's own, game-free: a way given as points it passes in order and the places its
    /// stops are wanted, where the pairing of stations would never reach every corner of a district (a district's own grid,
    /// laid by rule, knows where its blocks are). The same route both ways: the other direction passes the same points and
    /// stops at the same places in the reverse order. Checked by NavCheck (UptownStreetsCheck.cs).
    /// </summary>
    public static partial class TransitPlan
    {
        public const float ViaReach = 8f;        // metres from a point of the way its lane points are looked for: both lanes of a street, an avenue's four
        public const float WantedReach = 14f;    // metres from a wanted stop the way must pass for the stop to be laid on it
        public const int MostRouteStops = 40;    // wanted stops on one route, at the most
        public const float WantedApart = 60f;    // metres along a way between two of its stops at the least: a place it passes again is the later stop's

        /// <summary>One direction of a district's own route: the way through its points and the places its stops are wanted.</summary>
        public sealed class Route
        {
            public readonly string Name;        // for the log
            public readonly float[] ViaX;       // the way: its first point, every point it passes in order, its last
            public readonly float[] ViaZ;
            public readonly float[] StopX;      // where its stops are wanted, in the order the way passes them
            public readonly float[] StopZ;

            public Route(string name, float[] viaX, float[] viaZ, float[] stopX, float[] stopZ)
            {
                Name = name;
                ViaX = viaX ?? new float[0];
                ViaZ = viaZ ?? new float[0];
                StopX = stopX ?? new float[0];
                StopZ = stopZ ?? new float[0];
            }

            public int Points => Math.Min(ViaX.Length, ViaZ.Length);
            public int Stops => Math.Min(StopX.Length, StopZ.Length);

            /// <summary>The other direction: the same points and stops, in the reverse order.</summary>
            public Route Back(string name)
            {
                return new Route(name, Reversed(ViaX), Reversed(ViaZ), Reversed(StopX), Reversed(StopZ));
            }

            /// <summary>Why the route cannot be laid (too few points, a stop count out of bounds), or null.</summary>
            public string Fault()
            {
                if (ViaX.Length != ViaZ.Length || StopX.Length != StopZ.Length)
                {
                    return "its points or its stops are not in pairs";
                }
                if (Points < 2)
                {
                    return "it has fewer than two points";
                }
                if (Stops < 2 || Stops > MostRouteStops)
                {
                    return "it wants " + Stops + " stops (2 to " + MostRouteStops + ")";
                }
                return null;
            }

            private static float[] Reversed(float[] values)
            {
                var back = new float[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    back[i] = values[values.Length - 1 - i];
                }
                return back;
            }
        }

        /// <summary>
        /// Metres along a path (its points x, z and the metres to each) of each wanted stop: the nearest point of the path
        /// within <see cref="WantedReach"/>, looked for from <see cref="WantedApart"/> on from the last stop's, so a way that
        /// passes a place twice stops in the order wanted; -1 for a stop the path does not pass. Fills <paramref name="along"/>, returns how many
        /// were found.
        /// </summary>
        public static int WantedAlong(IList<float> x, IList<float> z, IList<float> metres, int count, float[] stopX, float[] stopZ, float[] along)
        {
            int found = 0, from = 0;
            float reach = WantedReach * WantedReach;
            for (int s = 0; s < along.Length; s++)
            {
                along[s] = -1f;
                if (s >= stopX.Length || s >= stopZ.Length)
                {
                    continue;
                }
                int best = -1;
                float nearest = reach;
                for (int i = from; i < count; i++)
                {
                    float dx = x[i] - stopX[s], dz = z[i] - stopZ[s], d = dx * dx + dz * dz;
                    if (d < nearest)
                    {
                        nearest = d;
                        best = i;
                    }
                    else if (best >= 0 && d > nearest + 400f)
                    {
                        break;   // the path has passed the stop and gone on: the next pass, if any, is another stop's
                    }
                }
                if (best >= 0)
                {
                    along[s] = metres[best];
                    from = best + 1;
                    while (from < count && metres[from] < metres[best] + WantedApart)
                    {
                        from++;
                    }
                    found++;
                }
            }
            return found;
        }

        /// <summary>Why a stop wanted on a district's route was not laid.</summary>
        public enum Miss
        {
            OffTheWay = 1,      // the way laid through the road graph passes no lane point within <see cref="WantedReach"/> of it
            NoPlace = 2,        // the way passes it, but no lane point round it takes a stop and no place another line laid there could be shared
        }

        /// <summary>How near a path comes to a point, in metres (anywhere along it); float.MaxValue for a path of no points.</summary>
        public static float Approach(IList<float> x, IList<float> z, int count, float atX, float atZ)
        {
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float d = Distance(x[i], z[i], atX, atZ);
                nearest = d < nearest ? d : nearest;
            }
            return nearest;
        }

        /// <summary>
        /// The check line's words for one stop of a district's route that was not laid: which route, which stop of how many,
        /// where it was wanted, and why. <paramref name="metres"/> is how near the way came (<see cref="Miss.OffTheWay"/>);
        /// <paramref name="tried"/> how many lane points were tried round it and <paramref name="far"/> how far from it (<see cref="Miss.NoPlace"/>).
        /// </summary>
        public static string MissWords(string route, int stop, int stops, float x, float z, Miss why, float metres, int tried, float far)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            string at = route + " stop " + stop + " of " + stops + " at (" + x.ToString("F1", inv) + ", " + z.ToString("F1", inv) + ")";
            return why == Miss.OffTheWay
                ? at + ": the way passes no nearer than " + metres.ToString("F0", inv) + " m (it must pass within " + WantedReach.ToString("F0", inv) + ")"
                : at + ": the way passes it, but none of " + tried + " lane point(s) within " + far.ToString("F0", inv) + " m takes a stop, and no place another line laid there can be shared";
        }

        /// <summary>The order the lane points round a wanted place are tried in: nearest first, alternately on and back, within a reach.</summary>
        public static int Nearest(IList<float> metres, int count, float wanted, float within, int after, int[] order)
        {
            int first = 0;
            while (first < count && metres[first] < wanted)
            {
                first++;
            }
            int on = first, back = first - 1, n = 0;
            while (n < order.Length && (on < count || back > after))
            {
                float onOff = on < count ? metres[on] - wanted : float.MaxValue;
                float backOff = back > after ? wanted - metres[back] : float.MaxValue;
                if (onOff > within && backOff > within)
                {
                    break;
                }
                if (onOff <= backOff)
                {
                    if (on > after)
                    {
                        order[n++] = on;
                    }
                    on++;
                }
                else
                {
                    order[n++] = back;
                    back--;
                }
            }
            return n;
        }
    }
}
