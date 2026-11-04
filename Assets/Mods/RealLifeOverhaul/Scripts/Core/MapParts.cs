using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's parts that change something for the open city map and undo it when the map closes
    /// (docs/standards/performance.md, RLO-PERF-022), each with a look that says what it still holds changed or still has to
    /// do because of the map. The map check of the session record (Diagnostics/SessionWatch.Map) asks them once a frame
    /// after the map closed, until none holds anything or its time is up, and only with Developer diagnostics on. A look
    /// counts and compares what its part keeps: it never searches the scene and allocates nothing, and writes its words only
    /// when it is given a line. Game-free.
    /// </summary>
    public static class MapParts
    {
        /// <summary>
        /// What a part still holds changed for the map, or still has to do because of it: 0 when all is as it was before the
        /// map opened. Given a line, the part says so in a few words ("far squares: 36 of 58 held again as before the map").
        /// </summary>
        public delegate int Look(StringBuilder line);

        private static readonly List<string> Names = new List<string>(16);
        private static readonly List<Look> Looks = new List<Look>(16);
        private static readonly List<string> Faults = new List<string>(16);   // the fault a look threw once (it is not asked again), or null
        private static readonly List<Func<bool>> Later = new List<Func<bool>>(4);   // parts' work for the map's last change, a slice a frame

        /// <summary>The parts known.</summary>
        public static int Count => Names.Count;

        /// <summary>Steps of the parts' work that wait for the frames after the map's last change.</summary>
        public static int Waiting => Later.Count;

        /// <summary>
        /// Work a part still has for the map's last change, done a slice a frame in the frames after it (the map's camera takes
        /// a second or more to fly up or down) instead of all in the frame the map switches in: <paramref name="step"/> runs
        /// once a frame (<see cref="RunLater"/>, from Core/MapTreeGuard) until it returns false. A step already waiting is not
        /// added twice. The step guards itself: one that throws is let go.
        /// </summary>
        public static void After(Func<bool> step)
        {
            if (step != null && !Later.Contains(step))
            {
                Later.Add(step);
            }
        }

        /// <summary>Once a frame: a slice of each waiting step; nothing but a count while none waits.</summary>
        public static void RunLater()
        {
            for (int i = Later.Count - 1; i >= 0; i--)
            {
                bool more;
                try
                {
                    more = Later[i]();
                }
                catch (Exception)
                {
                    more = false;
                }
                if (!more)
                {
                    Later.RemoveAt(i);
                }
            }
        }

        /// <summary>A part's look, once as the part starts; again under the same name, the look is replaced (a new city).</summary>
        public static void Add(string name, Look look)
        {
            if (string.IsNullOrEmpty(name) || look == null)
            {
                return;
            }
            int at = Names.IndexOf(name);
            if (at >= 0)
            {
                Looks[at] = look;
                Faults[at] = null;
                return;
            }
            Names.Add(name);
            Looks.Add(look);
            Faults.Add(null);
        }

        /// <summary>
        /// Asks every part, in the order they were added: the number that still hold something. Given a line, each part's
        /// words go into it, one after another with "; " between. A look that throws counts as holding, with its fault
        /// named; it is not asked again until its part is added anew.
        /// </summary>
        public static int Lingering(StringBuilder line)
        {
            int holding = 0;
            for (int i = 0; i < Looks.Count; i++)
            {
                if (line != null && i > 0)
                {
                    line.Append("; ");
                }
                if (Faults[i] != null)
                {
                    holding++;
                    line?.Append(Names[i]).Append(": its look failed (").Append(Faults[i]).Append("), not asked again");
                    continue;
                }
                try
                {
                    if (Looks[i](line) > 0)
                    {
                        holding++;
                    }
                }
                catch (Exception e)
                {
                    Faults[i] = e.GetType().Name;
                    holding++;
                    line?.Append(Names[i]).Append(": its look failed (").Append(Faults[i]).Append(')');
                }
            }
            return holding;
        }
    }
}
