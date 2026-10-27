using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The district's side of the city's growth (plans/growables.md, Scripts/Construction ConstructionDirector.Grow): a grown
    /// building's unit let to its tenant on its day, as the mix pass lets one (a shop or an office given a business of the kind
    /// its street holds fewest of, run by a rival, by the game's own start of a business; a home let to residents), and a
    /// building's traffic lowered by its poor condition (the game's record is made at each load, so the drop is asked again
    /// after every load by the construction game's day). Never the player's unit or another player's, never in a Going Public
    /// session. COST: nothing per frame; a look over the standing buildings on a grown building's day.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private static readonly Dictionary<string, int> Drops = new Dictionary<string, int>(16, StringComparer.Ordinal);   // lot: traffic points its condition takes off

        /// <summary>
        /// The unit of the city's building on a lot let to a tenant now, as the mix pass lets one. Null when it was, else why not
        /// (none stands, it is let already, it is the player's, a session).
        /// </summary>
        public static string LetUnit(string lotId)
        {
            if (GoingPublic.InSession)
            {
                return "a Going Public session: the city's units are let by the game alone";
            }
            MadeBuilding made = StandingMade(lotId);
            if (made == null || made.Registration == null || made.Building == null)
            {
                return "no building of the district stands on lot " + (lotId ?? "(none)") + " at this load";
            }
            if (_stateUnread)
            {
                return "the district's own line in this save cannot be read";
            }
            GameInstance save = SaveGameManager.Current;
            return LifeGame.LetOne(made, Standing, Another, save);
        }

        /// <summary>
        /// A building's traffic lowered by so many points for its condition (0 gives it its own again), now and after each load once
        /// asked again. False when no building of the district stands on the lot.
        /// </summary>
        public static bool Retraffic(string lotId, int drop)
        {
            if (string.IsNullOrEmpty(lotId))
            {
                return false;
            }
            if (drop > 0)
            {
                Drops[lotId] = drop;
            }
            else
            {
                Drops.Remove(lotId);
            }
            MadeBuilding made = StandingMade(lotId);
            int own = TrafficOf(lotId);
            return made != null && own >= 0 && LifeGame.Traffic(made, Math.Max(1, own - Math.Max(0, drop)));
        }

        /// <summary>The traffic points a lot's building's condition takes off now (0 for none).</summary>
        public static int DropOf(string lotId) => !string.IsNullOrEmpty(lotId) && Drops.TryGetValue(lotId, out int d) ? d : 0;
    }
}
