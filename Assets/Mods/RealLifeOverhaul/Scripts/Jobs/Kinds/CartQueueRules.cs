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
    /// The pavement drinks cart's queue (Jobs/Kinds/VendorJob), as rules with no Unity or game types, checked offline.
    /// A customer on their way who cannot get to the cart (their route cut by an accident's scene, a vehicle standing
    /// on the crossing, a carved cordon; a route that stops short; nobody nearer for seconds on end) leaves the queue at
    /// once, never on a long timer, and so does one who is no longer the cart's to direct. None of that is the
    /// customer walking off: only a customer kept waiting at the counter is a walk-off (VendorJob's Ordering step,
    /// never decided here), and only one who truly gave up waiting makes the shift one shorter. Whoever left for
    /// something not of their own doing goes back among those still to come, so the cart gets its next customer, unless
    /// that has happened <see cref="RetriesMost"/> times in a row, when the shift is one shorter instead and cannot be
    /// held open by a street nobody can walk.
    /// A few comparisons a customer a tick, five times a second while the cart is open; nothing allocated.
    /// </summary>
    public static class CartQueueRules
    {
        public const float ReachedWithin = 0.9f;     // metres from their place that counts as being there
        public const float ShortOf = 2.5f;           // metres from their place a walker who has stopped is plainly short of it (nearer, the engine may still be closing in, its stopping distance short of the goal)
        public const float ProgressStep = 0.3f;      // metres nearer their place that counts as getting on
        public const float StuckAfter = 6f;          // seconds no nearer before a walker is stuck (one stopped by a passer-by for a moment is not)
        public const float WalkMost = 40f;           // seconds any walk to the cart may take: from 34 m away at a walk, about 30
        public const float WaitMost = 75f;           // seconds a customer waits their turn behind the counter before giving up
        public const float RouteMost = 2.2f;         // a route to the cart this many times the distance as the crow flies is a detour round the block: nobody is sent along it
        public const float RouteSlack = 8f;          // ...plus these metres, for the way along a pavement and over a crossing
        public const float RouteLongest = 45f;       // metres of route at most, whatever the distance: walked in about 35 s, inside WalkMost
        public const float NextSoon = 1.5f;          // seconds after one left for nothing of their own doing before the next sets off
        public const int RetriesMost = 4;            // customers in a row who could not get to the cart before the shift is one shorter

        /// <summary>
        /// A customer on their way, to the place behind the counter or to the counter itself. <paramref name="best"/> and
        /// <paramref name="bestAt"/> are the nearest they have been to their place and when; set them to the first
        /// distance and the time they set off, and leave them to this after that.
        /// </summary>
        /// <param name="held">Still the cart's: on the street, in use, the same lease as when they set off.</param>
        /// <param name="routeKnown">The route asked for has been worked out (it is no longer pending).</param>
        /// <param name="routeWhole">...and it reaches their place.</param>
        /// <param name="stopped">The engine says they are not walking on: at the end of their route (or within its stopping distance of it), with none, or off the navigation mesh.</param>
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
        /// Whether a customer who left the queue this way makes the shift one shorter. One who gave up waiting their turn
        /// does. One the cart could not get to it (the street, or the mod) goes back among those to come, unless
        /// <paramref name="inARow"/> (counting this one) has reached <see cref="RetriesMost"/>.
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
        /// The cart's counts when a customer leaves the queue this way: <paramref name="came"/> (how many have set off),
        /// <paramref name="lost"/> (came and went unserved) and <paramref name="inARow"/> (in a row who could not get
        /// there). The walk-offs are not among them: nothing here is one. True when the shift is one shorter.
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
