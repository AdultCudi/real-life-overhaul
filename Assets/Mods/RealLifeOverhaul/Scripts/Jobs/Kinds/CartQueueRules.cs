namespace RealLifeOverhaul
{
    /// <summary>What became of a cart customer on a tick (<see cref="CartQueueRules"/>).</summary>
    public enum CartGuest
    {
        Keep,        // as they were: walking, waiting their turn or at the counter
        There,       // walking, and now at their place
        NotHeld,     // no longer the cart's: taken off the street, shelved, or handed to somebody else
        NoRoute,     // walking, and the route asked for does not reach their place
        EndsShort,   // walking, and stopped where the route ended, short of their place
        Stuck,       // walking, and no nearer for StuckAfter seconds
        TooLong,     // walking for longer than any walk to the cart takes
        GaveUp       // waited their turn behind the counter for longer than anybody would
    }

    /// <summary>
    /// The drinks cart's queue as rules with no game types, checked offline. A customer who cannot reach the cart leaves the queue
    /// at once, never as a walk-off; only one who gave up waiting makes the shift shorter, the rest go back among those to come
    /// unless <see cref="RetriesMost"/> in a row could not. A few comparisons a customer a tick.
    /// </summary>
    public static class CartQueueRules
    {
        public const float ReachedWithin = 0.9f;     // metres from their place that counts as being there
        public const float ShortOf = 2.5f;           // metres from their place a stopped walker is plainly short of it (nearer, the engine may still be closing in)
        public const float ProgressStep = 0.3f;      // metres nearer their place that counts as getting on
        public const float StuckAfter = 6f;          // seconds no nearer before a walker is stuck (one stopped by a passer-by for a moment is not)
        public const float WalkMost = 40f;           // seconds any walk to the cart may take: from 34 m away at a walk, about 30
        public const float WaitMost = 75f;           // seconds a customer waits their turn behind the counter before giving up
        public const float RouteMost = 2.2f;         // a route this many times the straight distance is a detour round the block: nobody is sent along it
        public const float RouteSlack = 8f;          // ...plus these metres, for the way along a pavement and over a crossing
        public const float RouteLongest = 45f;       // metres of route at most, walked in about 35 s, inside WalkMost
        public const float NextSoon = 1.5f;          // seconds after one left for nothing of their own doing before the next sets off
        public const int RetriesMost = 4;            // customers in a row who could not get to the cart before the shift is one shorter

        /// <summary>A customer on their way; <paramref name="best"/> and <paramref name="bestAt"/> start at the first distance and time, then are kept here.</summary>
        /// <param name="held">Still the cart's: on the street, in use, the same lease as when they set off.</param>
        /// <param name="routeKnown">The route asked for has been worked out.</param>
        /// <param name="routeWhole">...and it reaches their place.</param>
        /// <param name="stopped">The engine has them not walking on: at their route's end, with none, or off the navigation mesh.</param>
        /// <param name="distance">Metres from their place, across the ground.</param>
        /// <param name="until">When the walk is over whatever happens (set off plus <see cref="WalkMost"/>).</param>
        public static CartGuest Walking(bool held, bool routeKnown, bool routeWhole, bool stopped, float distance, float now, float until, ref float best, ref float bestAt)
        {
            if (!held)
            {
                return CartGuest.NotHeld;
            }
            if (stopped && distance < ReachedWithin)
            {
                return CartGuest.There;
            }
            if (routeKnown && !routeWhole)
            {
                return CartGuest.NoRoute;
            }
            if (stopped && distance >= ShortOf)
            {
                return CartGuest.EndsShort;
            }
            if (distance < best - ProgressStep)
            {
                best = distance;
                bestAt = now;
            }
            if (now - bestAt >= StuckAfter)
            {
                return CartGuest.Stuck;
            }
            return now >= until ? CartGuest.TooLong : CartGuest.Keep;
        }

        /// <summary>A customer standing in the place behind the counter, waiting their turn; <paramref name="until"/> is when they give up.</summary>
        public static CartGuest Waiting(bool held, float now, float until)
        {
            if (!held)
            {
                return CartGuest.NotHeld;
            }
            return now >= until ? CartGuest.GaveUp : CartGuest.Keep;
        }

        /// <summary>
        /// Whether a customer who left this way makes the shift one shorter: one who gave up waiting does; one who could not get
        /// there goes back among those to come, unless <paramref name="inARow"/>, this one counted, has reached <see cref="RetriesMost"/>.
        /// </summary>
        public static bool Shorter(CartGuest why, int inARow)
        {
            switch (why)
            {
                case CartGuest.Keep:
                case CartGuest.There:
                    return false;
                case CartGuest.GaveUp:
                    return true;
                default:
                    return inARow >= RetriesMost;
            }
        }

        /// <summary>
        /// Updates the cart's counts when a customer leaves the queue this way (none of it a walk-off): <paramref name="came"/>
        /// set off, <paramref name="lost"/> came and went unserved, <paramref name="inARow"/> could not get there in a row. True
        /// when the shift is one shorter.
        /// </summary>
        public static bool Account(CartGuest why, ref int came, ref int lost, ref int inARow)
        {
            if (why == CartGuest.Keep || why == CartGuest.There)
            {
                return false;
            }
            bool retry = why != CartGuest.GaveUp;
            if (retry)
            {
                inARow++;
            }
            if (Shorter(why, inARow))
            {
                lost++;
                if (retry)
                {
                    inARow = 0;
                }
                return true;
            }
            came--;   // never came, as the cart counts it: the next sets off in their place
            return false;
        }

        /// <summary>Whether a route of <paramref name="length"/> metres from <paramref name="straight"/> metres away is one to send a customer along.</summary>
        public static bool RouteFits(float straight, float length)
        {
            return length >= 0f && length <= straight * RouteMost + RouteSlack && length <= RouteLongest;
        }

        /// <summary>The words for the log.</summary>
        public static string Words(CartGuest why)
        {
            switch (why)
            {
                case CartGuest.NotHeld: return "no longer the cart's (taken off the street)";
                case CartGuest.NoRoute: return "no route reaches the cart";
                case CartGuest.EndsShort: return "their route ended short of the cart";
                case CartGuest.Stuck: return "no nearer for " + StuckAfter.ToString("F0") + " s";
                case CartGuest.TooLong: return "on the way for " + WalkMost.ToString("F0") + " s";
                case CartGuest.GaveUp: return "gave up waiting their turn after " + WaitMost.ToString("F0") + " s";
                default: return "still with the cart";
            }
        }
    }
}
