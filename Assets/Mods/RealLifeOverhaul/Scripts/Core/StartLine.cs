using System.Text;
using BAModAPI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The directors' first loads after a save loads, one a frame instead of all in one (Core/StartRules: the seats, their
    /// order and the rule). Each director that reads its file and lays out its part of the city on its first tick waits for
    /// its seat at the top of that tick, in its own one-second coroutine:
    ///
    ///     yield return wait;
    ///     while (StartLine.Holds(StartSeats.Tech))
    ///     {
    ///         yield return null;
    ///     }
    ///
    /// and the credit side does the same for each of its followers before it calls it (Economy/Banking/EconomyDirector). Once a
    /// seat has been given, asking again is one array read and false, so each director keeps its one-second rhythm from
    /// there, a few frames later than before. While the save is not there, the loading screen is up or the city is being
    /// unloaded nothing is held: nothing starts then, and the director's own tick returns at its first line.
    ///
    /// The line begins again with every city (<see cref="Begin"/>, from the mod's start behind the loading screen). What a
    /// director computes at its start is the same as before: the same order, the same save, the same state of the others it
    /// reads; only the frame it does it in is later, by one frame for each seat before it that starts in that second (some
    /// 20 frames at the most). Nothing is sent to anyone and nothing is written to the save.
    ///
    /// Cost: a few comparisons a director a second; while seats wait, one look along the seats before it each frame, for a
    /// few frames after a load. Nothing is allocated except the one line with "Developer diagnostics" on.
    /// </summary>
    public static class StartLine
    {
        public static IModLogger Logger;

        private static readonly StartQueue Queue = new StartQueue(StartSeats.Count);
        private static bool _toTell;   // seats were given since the line last said how it went

        /// <summary>A new city: every director's first load waits for its seat again. Called once a city, behind the loading screen.</summary>
        public static void Begin()
        {
            Queue.Begin();
            _toTell = false;
        }

        /// <summary>
        /// True while the director's first load after the save loaded must wait for another frame; then false for good, until
        /// the next city. Asked at the top of the director's tick, and in each frame it waits.
        /// </summary>
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

        /// <summary>The seat's first load has been done since the city loaded (for the self-check and the tests).</summary>
        public static bool Started(int seat) => Queue.Given(seat) != int.MinValue;

        // With "Developer diagnostics" on, once every seat that asked has had its turn: the frames they were given, in the line's
        // order, against the rule (one a frame, none ahead of a seat before it that was waiting).
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
