using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Picks the next life event like dealing from a deck: an event that has already come up in
    /// this save is not dealt again until every event that fits the current situation has been
    /// seen. Free of game types so tools\CatalogueCheck can exercise it.
    /// </summary>
    internal static class LifeEventDeck
    {
        /// <summary>
        /// Returns the event to fire and records it in <paramref name="seen"/>, or null when
        /// nothing fits. <paramref name="random01"/> returns a value from 0 to 1.
        /// </summary>
        public static LifeEvent Draw(IReadOnlyList<LifeEvent> all, in LifeContext context, ISet<string> seen, string lastId,
            Func<float> random01)
        {
            var fresh = new List<LifeEvent>();
            var fitting = new List<LifeEvent>();
            foreach (LifeEvent e in all)
            {
                if (!e.Fits(context))
                {
                    continue;
                }
                fitting.Add(e);
                if (!seen.Contains(e.Id))
                {
                    fresh.Add(e);
                }
            }
            if (fitting.Count == 0)
            {
                return null;
            }

            if (fresh.Count == 0)
            {
                // Everything that fits has been seen: reshuffle just those. Events for other
                // situations stay marked, so they still won't repeat when their turn comes.
                foreach (LifeEvent e in fitting)
                {
                    seen.Remove(e.Id);
                    if (e.Id != lastId || fitting.Count == 1)
                    {
                        fresh.Add(e);   // don't lead the new round with the event that ended the last one
                    }
                }
            }

            float total = 0f;
            foreach (LifeEvent e in fresh)
            {
                total += e.DrawWeight;
            }
            float roll = random01() * total;
            LifeEvent picked = fresh[fresh.Count - 1];
            foreach (LifeEvent e in fresh)
            {
                roll -= e.DrawWeight;
                if (roll <= 0f)
                {
                    picked = e;
                    break;
                }
            }
            seen.Add(picked.Id);
            return picked;
        }
    }
}
