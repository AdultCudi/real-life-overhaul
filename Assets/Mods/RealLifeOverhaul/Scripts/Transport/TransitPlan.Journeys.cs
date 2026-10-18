using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>The bus network's part for the journey planner: one bus, or two with a change at a transfer point.</summary>
    public static partial class TransitPlan
    {
        /// <summary>
        /// The network as the planner reads it: the places a bus stops at, each line's places in order with the game minutes from
        /// its first stop, and the places a short walk apart, joined for a change on foot.
        /// </summary>
        public sealed class Net
        {
            public readonly List<float> X = new List<float>();
            public readonly List<float> Z = new List<float>();
            public readonly List<List<int>> Near = new List<List<int>>();   // other places within the walk, each place's own
            public readonly List<int[]> Calls = new List<int[]>();          // per line: its places in order
            public readonly List<float[]> Minutes = new List<float[]>();    // per line: game minutes from its first stop
            public readonly List<List<int>> LinesAt = new List<List<int>>(); // per place: the lines that call there

            public int Places => X.Count;
            public int Lines => Calls.Count;

            public int AddPlace(float x, float z)
            {
                X.Add(x);
                Z.Add(z);
                Near.Add(new List<int>(4));
                LinesAt.Add(new List<int>(4));
                return X.Count - 1;
            }

            public int AddLine(int[] places, float[] minutes)
            {
                Calls.Add(places);
                Minutes.Add(minutes);
                int line = Calls.Count - 1;
                for (int i = 0; i < places.Length; i++)
                {
                    if (!LinesAt[places[i]].Contains(line))
                    {
                        LinesAt[places[i]].Add(line);
                    }
                }
                return line;
            }

            /// <summary>Joins every two places within a walk of each other.</summary>
            public void Link(float walk)
            {
                for (int i = 0; i < X.Count; i++)
                {
                    Near[i].Clear();
                }
                for (int i = 0; i < X.Count; i++)
                {
                    for (int k = i + 1; k < X.Count; k++)
                    {
                        if (Distance(X[i], Z[i], X[k], Z[k]) <= walk)
                        {
                            Near[i].Add(k);
                            Near[k].Add(i);
                        }
                    }
                }
            }

            public float Apart(int a, int b)
            {
                return Distance(X[a], Z[a], X[b], Z[b]);
            }

            /// <summary>Joins two places for a change on foot, once.</summary>
            public void Join(int a, int b)
            {
                if (a != b && !Near[a].Contains(b))
                {
                    Near[a].Add(b);
                    Near[b].Add(a);
                }
            }

            /// <summary>Joins the places round each station, as a subway station's stops are one interchange.</summary>
            public void Interchanges(IList<float> stationX, IList<float> stationZ, float within)
            {
                var near = new List<int>(8);
                for (int s = 0; s < stationX.Count; s++)
                {
                    near.Clear();
                    for (int i = 0; i < X.Count; i++)
                    {
                        if (Distance(X[i], Z[i], stationX[s], stationZ[s]) <= within)
                        {
                            near.Add(i);
                        }
                    }
                    for (int i = 0; i < near.Count; i++)
                    {
                        for (int k = i + 1; k < near.Count; k++)
                        {
                            Join(near[i], near[k]);
                        }
                    }
                }
            }
        }

        public const float InterchangeWithin = 230f;   // metres of a subway station within which its stops are one interchange

        /// <summary>One journey from a place: the bus to board, where to get off it, and the change where there is one.</summary>
        public struct Journey
        {
            public bool Found;
            public int Line;          // the first bus
            public int From;          // its order at the place boarded at
            public int Off;           // and where they get off it
            public int Line2;         // the second bus, -1 for none
            public int On2;           // its order where they board it
            public int Off2;          // and get off
            public double Arrive;     // game minutes (the game's total) they are at the destination
            public double Board;      // and they board the first bus
            public double Board2;     // and the second
            public bool Direct => Line2 < 0;
        }

        public const float WalkMetresPerMinute = 70f;   // a change on foot (the game's walk is about 1.2 m a second)
        public const float ChangeLeast = 2f;            // game minutes a change takes at the least: off one bus and onto the next

        /// <summary>The best journey by arrival from one place to every other, into <paramref name="best"/>: one bus, else two with a change.</summary>
        public static void From(Net net, int place, double now, Func<int, int, double, double> call, Journey[] best)
        {
            for (int i = 0; i < best.Length; i++)
            {
                best[i] = default(Journey);
                best[i].Line2 = -1;
                best[i].Arrive = double.MaxValue;
            }
            List<int> lines = net.LinesAt[place];
            for (int l = 0; l < lines.Count; l++)
            {
                int line = lines[l];
                int[] calls = net.Calls[line];
                float[] minutes = net.Minutes[line];
                for (int from = 0; from < calls.Length; from++)
                {
                    if (calls[from] != place)
                    {
                        continue;
                    }
                    double board = call(line, from, now);
                    if (board >= double.MaxValue)
                    {
                        continue;
                    }
                    for (int off = from + 1; off < calls.Length; off++)
                    {
                        double at = board + (minutes[off] - minutes[from]);
                        Offer(best, calls[off], at, line, from, off, -1, 0, 0, board, 0);
                        // A change here, or a short walk away, onto another line.
                        Change(net, calls[off], at, line, from, off, board, call, best, calls[off]);
                        List<int> near = net.Near[calls[off]];
                        for (int n = 0; n < near.Count; n++)
                        {
                            Change(net, near[n], at + net.Apart(calls[off], near[n]) / WalkMetresPerMinute, line, from, off, board, call, best, calls[off]);
                        }
                    }
                }
            }
            best[place] = default(Journey);
            best[place].Line2 = -1;
            best[place].Arrive = double.MaxValue;
        }

        private static void Change(Net net, int at, double ready, int line, int from, int off, double board, Func<int, int, double, double> call, Journey[] best, int offPlace)
        {
            List<int> lines = net.LinesAt[at];
            for (int l = 0; l < lines.Count; l++)
            {
                int second = lines[l];
                if (second == line)
                {
                    continue;
                }
                int[] calls = net.Calls[second];
                float[] minutes = net.Minutes[second];
                for (int on = 0; on < calls.Length; on++)
                {
                    if (calls[on] != at)
                    {
                        continue;
                    }
                    double board2 = call(second, on, ready + (at == offPlace ? ChangeLeast : 0f));
                    if (board2 >= double.MaxValue)
                    {
                        continue;
                    }
                    for (int off2 = on + 1; off2 < calls.Length; off2++)
                    {
                        Offer(best, calls[off2], board2 + (minutes[off2] - minutes[on]), line, from, off, second, on, off2, board, board2);
                    }
                }
            }
        }

        private static void Offer(Journey[] best, int place, double arrive, int line, int from, int off, int line2, int on2, int off2, double board, double board2)
        {
            // One bus is taken over a change unless the change gets there a good deal sooner.
            double held = best[place].Arrive;
            bool better = !best[place].Found || (line2 < 0 && !best[place].Direct ? arrive <= held + 15.0 : best[place].Direct && line2 >= 0 ? arrive < held - 15.0 : arrive < held);
            if (!better)
            {
                return;
            }
            best[place] = new Journey { Found = true, Line = line, From = from, Off = off, Line2 = line2, On2 = on2, Off2 = off2, Arrive = arrive, Board = board, Board2 = board2 };
        }
    }
}
