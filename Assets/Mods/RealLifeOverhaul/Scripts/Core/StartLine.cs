using System.Text;
using BAModAPI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The directors' first loads after a save loads, one a frame instead of all in one (<see cref="StartSeats"/>). Each
    /// director waits for its seat at the top of its first tick, and the credit side does so for each follower:
    ///
    ///     yield return wait;
    ///     while (StartLine.Holds(StartSeats.Tech))
    ///     {
    ///         yield return null;
    ///     }
    ///
    /// Once given, asking is one array read. Nothing is held while there is no save, the loading screen is up or the city
    /// unloads. A director computes the same as before, only a few frames later. Cost: a few comparisons a director a
    /// second; nothing allocated but the one line with Developer diagnostics on.
    /// </summary>
    public static class StartLine
    {
        public static IModLogger Logger;

        private static readonly StartQueue Queue = new StartQueue(StartSeats.Count);
        private static bool _toTell;   // seats were given since the line last said how it went

        /// <summary>A new city: every director's first load waits for its seat again. Once a city, behind the loading screen.</summary>
        public static void Begin()
        {
            Queue.Begin();
            _toTell = false;
        }

        /// <summary>True while the director's first load must wait another frame; then false until the next city.</summary>
        public static bool Holds(int seat)
        {
            if (SaveGameManager.Current == null || LoadScene.isLoading || GameManager.isCitySceneBeingUnloaded || !ModState.Loaded)
            {
                return false;   // nothing starts yet: the director's tick returns at its first line
            }
            int frame = Time.frameCount;
            if (_toTell && frame > Queue.LastGiven && Queue.Settled(frame))
            {
                _toTell = false;
                Tell();
            }
            if (!Queue.Holds(seat, frame))
            {
                _toTell |= Queue.Given(seat) == frame;
                return false;
            }
            return true;
        }


        // With Developer diagnostics on, once every seat that asked has had its turn: the frames given, against the rule.
        private static void Tell()
        {
            if (!Meta.Dev.Verbose)
            {
                return;
            }
            var line = new StringBuilder(640);
            int given = 0, first = int.MaxValue, last = int.MinValue, most = 0;
            bool twice = false, ahead = false;
            for (int s = 0; s < StartSeats.Count; s++)
            {
                int at = Queue.Given(s);
                if (at == int.MinValue)
                {
                    continue;
                }
                for (int t = 0; t < StartSeats.Count; t++)
                {
                    int other = Queue.Given(t);
                    twice |= t != s && other == at;
                    ahead |= t < s && other > at && Queue.Asked(t) < at;   // a seat before it, waiting already, went after it
                }
                given++;
                first = Mathf.Min(first, at);
                last = Mathf.Max(last, at);
                most = Mathf.Max(most, at - Queue.Asked(s));
                line.Append(given == 1 ? string.Empty : ", ").Append(StartSeats.Names[s]).Append(" (").Append(at).Append(')');
            }
            if (given == 0)
            {
                return;
            }
            Logger?.Info("Start line check (the first loads after the save loaded): " + given + " of " + StartSeats.Count + " director(s), one a frame from frame " + first + " to " + last
                         + ", the most one waited " + most + " frame(s); in the line's order, with the frame each started in: " + line + ": "
                         + (twice ? "FAIL (two in one frame)" : ahead ? "FAIL (one went ahead of a director before it in the line)" : "PASS") + ".");
        }
    }
}
