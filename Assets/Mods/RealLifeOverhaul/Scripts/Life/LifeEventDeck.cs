using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Picks the next life event like dealing from a deck: one seen in this save is not dealt again until every event that
    /// fits the situation has been. Game-free, for CatalogueCheck.
    /// </summary>
    internal static class LifeEventDeck
    {
        /// <summary>The event to fire, recorded in <paramref name="seen"/>, or null when nothing fits; <paramref name="random01"/> gives 0 to 1.</summary>
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
                // Everything that fits has been seen: those alone are shuffled again, so events for other situations
                // still do not repeat when their turn comes.
                foreach (LifeEvent e in fitting)
                {
                    seen.Remove(e.Id);
                    if (e.Id != lastId || fitting.Count == 1)
                    {
                        fresh.Add(e);   // the new round does not lead with the event that ended the last
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
