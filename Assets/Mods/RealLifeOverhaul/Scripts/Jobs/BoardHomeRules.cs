using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Which kinds of service building get a help-wanted board, and which building of a kind is its board's home, as rules checked offline
    /// (EconomyCheck's BoardPlaceChecks). Nothing here depends on what stands on a pavement in one second, so every machine and every load
    /// settles on the same home; a temporary block can only put a board somewhere else for a while (<see cref="Mends"/>), never choose it.
    /// </summary>
    public static class BoardHomeRules
    {
        /// <summary>
        /// The kinds to put a board up for, in the order they are put up (and so the order of the boards): first each employer's own kind of
        /// building, the first of its premises that the city has (an employer takes the first kind not already taken, as the office mapping does),
        /// then the other kinds in the city's list order until <paramref name="most"/>. Without this the six boards stood at the first six kinds
        /// the city's list showed, and an employer whose kind was not among them was given whichever board was left, a building anywhere.
        /// </summary>
        public static void Kinds(string[][] premises, IList<string> cityKinds, int most, List<string> chosen)
        {
            chosen.Clear();
            for (int e = 0; e < premises.Length && chosen.Count < most; e++)
            {
                bool found = false;
                for (int p = 0; p < premises[e].Length && !found && chosen.Count < most; p++)
                {
                    for (int k = 0; k < cityKinds.Count; k++)
                    {
                        if (Matches(cityKinds[k], premises[e][p]) && !chosen.Contains(cityKinds[k]))
                        {
                            chosen.Add(cityKinds[k]);
                            found = true;
                            break;
                        }
                    }
                }
            }
            for (int k = 0; k < cityKinds.Count && chosen.Count < most; k++)
            {
                if (!chosen.Contains(cityKinds[k]))
                {
                    chosen.Add(cityKinds[k]);
                }
            }
        }

        /// <summary>True when a building kind is one of an employer's premises (the game's type name holds the premises' word).</summary>
        public static bool Matches(string kind, string premise)
        {
            return kind != null && premise != null && kind.IndexOf(premise, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>True when the kind is any of the employer's premises.</summary>
        public static bool MatchesAny(string kind, string[] premises)
        {
            for (int p = 0; p < premises.Length; p++)
            {
                if (Matches(kind, premises[p]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Whether a board that stands at a spare building (<paramref name="rank"/> above 0: the buildings before it were asked and failed) is
        /// to be asked at a better one again: always, since a car or a passer-by that blocked the first is gone in a while; but a far
        /// building (nobody near it to make its cars and people real) only until <see cref="FarAsks"/> asks have failed, then only when the player is near it.
        /// </summary>
        public static bool Mends(int rank, int farFails, bool nearPlayer)
        {
            return rank > 0 && (nearPlayer || farFails < FarAsks);
        }

        public const int FarAsks = 6;   // failed asks of a building the player is far from, before it is only asked when he is near

        /// <summary>
        /// Whether a board may be moved to its better building now: the player must not see it go, so he is well away from where it
        /// stands and from where it will stand (the boss and the colleague are put away beyond 100 m, CareerDirector.Boss).
        /// </summary>
        public static bool MayMove(float flatFromOld, float flatFromNew)
        {
            return flatFromOld >= MoveAway && flatFromNew >= MoveAway;
        }

        public const float MoveAway = 150f;   // metres from the player a board is taken down and put up again
    }
}
