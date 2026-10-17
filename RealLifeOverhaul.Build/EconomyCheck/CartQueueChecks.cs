using System;
using RealLifeOverhaul;

// The pavement drinks cart's queue (Scripts/Jobs/Kinds/CartQueueRules.cs): a customer who cannot get to the cart, or is
// no longer the cart's, leaves the queue at once and is never a walk-off, and the cart gets its next customer. The
// fault it fixes: the vendor was left waiting for a customer who never came, beside an accident's scene.
internal static partial class Program
{
    private static void CartQueue()
    {
        CartWalking();
        CartCut();
        CartStuck();
        CartWaiting();
        CartCounts();
        CartRoutes();
        CartSame();
        CartWords();
    }

    // A walk of so many metres at so many metres a second, ticked five times a second as the cart is: what it ends in, and when.
    private static CartGuest CartWalk(float from, float speed, float pauseAt, float pauseFor, out float endedAt)
    {
        float until = CartQueueRules.WalkMost;
        float best = from, bestAt = 0f, distance = from;
        for (float now = 0.2f; now < 200f; now += 0.2f)
        {
            bool paused = now >= pauseAt && now < pauseAt + pauseFor;
            if (!paused)
            {
                distance = Math.Max(0f, distance - speed * 0.2f);
            }
            bool stopped = distance <= 0.2f;
            CartGuest fate = CartQueueRules.Walking(true, now > 0.3f, true, stopped, distance, now, until, ref best, ref bestAt);
            if (fate != CartGuest.Keep)
            {
                endedAt = now;
                return fate;
            }
        }
        endedAt = -1f;
        return CartGuest.Keep;
    }

    private static void CartWalking()
    {
        Check(CartWalk(34f, 1.2f, -1f, 0f, out float at) == CartGuest.There && at < CartQueueRules.WalkMost,
            "cart: a customer from 34 m away at a walk gets to the cart (" + at.ToString("F1") + " s) before " + CartQueueRules.WalkMost + " s");
        Check(CartWalk(1.4f, 1.2f, -1f, 0f, out at) == CartGuest.There, "cart: the step from the place behind to the counter ends at the counter");
        Check(CartWalk(20f, 1.2f, 4f, CartQueueRules.StuckAfter - 1.5f, out at) == CartGuest.There,
            "cart: a customer held up for a moment (a passer-by in the way) still gets there");
        float best = 0.5f, bestAt = 0f;
        Check(CartQueueRules.Walking(true, true, true, true, 0.5f, 1f, 40f, ref best, ref bestAt) == CartGuest.There, "cart: stopped within reach of their place is there");
        best = 10f;
        Check(CartQueueRules.Walking(true, false, false, false, 10f, 0.2f, 40f, ref best, ref bestAt) == CartGuest.Keep,
            "cart: while the route is still being worked out the customer is left to it");
    }

    // The case seen in play: the route does not reach, or ends short, or the customer is no longer the cart's. At once, on the first tick it is known.
    private static void CartCut()
    {
        float best = 18f, bestAt = 0f;
        Check(CartQueueRules.Walking(true, true, false, false, 18f, 0.4f, 40f, ref best, ref bestAt) == CartGuest.NoRoute,
            "cart: a route that does not reach the cart (an accident's scene on the crossing) is seen on the first tick it is known");
        best = 18f; bestAt = 0f;
        Check(CartQueueRules.Walking(true, true, true, true, 6f, 9f, 40f, ref best, ref bestAt) == CartGuest.EndsShort,
            "cart: a customer who stopped 6 m short of the cart leaves the queue at once, not " + CartQueueRules.WalkMost + " s after setting off");
        best = 1.4f; bestAt = 9f;
        Check(CartQueueRules.Walking(true, true, true, true, 1.3f, 9.2f, 40f, ref best, ref bestAt) == CartGuest.Keep,
            "cart: stopped 1.3 m from the counter (the engine's stopping distance) is left to close in, not sent away");
        best = 18f; bestAt = 0f;
        Check(CartQueueRules.Walking(true, false, false, true, 6f, 9f, 40f, ref best, ref bestAt) == CartGuest.EndsShort,
            "cart: a customer off the navigation mesh (no agent to walk them) leaves the queue at once");
        best = 18f; bestAt = 0f;
        Check(CartQueueRules.Walking(false, true, true, false, 10f, 3f, 40f, ref best, ref bestAt) == CartGuest.NotHeld,
            "cart: a walker who is no longer the cart's (taken off the street, another lease) leaves the queue at once");
        Check(CartQueueRules.Walking(false, true, true, true, 0.3f, 3f, 40f, ref best, ref bestAt) == CartGuest.NotHeld,
            "cart: one who is no longer the cart's is never counted as there, wherever they stand");
        Check(CartQueueRules.Waiting(false, 1f, 75f) == CartGuest.NotHeld, "cart: one waiting their turn who is no longer the cart's leaves the queue at once");
    }

    private static void CartStuck()
    {
        Check(CartWalk(20f, 0f, -1f, 0f, out float at) == CartGuest.Stuck && at <= CartQueueRules.StuckAfter + 0.3f,
            "cart: a walker who gets no nearer leaves the queue after " + CartQueueRules.StuckAfter + " s (" + at.ToString("F1") + " s)");
        Check(CartWalk(20f, 0.04f, -1f, 0f, out at) == CartGuest.Stuck,
            "cart: shuffling on the spot (" + CartQueueRules.ProgressStep + " m takes longer than " + CartQueueRules.StuckAfter + " s) is stuck");
        Check(CartWalk(30f, 1.2f, 6f, 20f, out at) == CartGuest.Stuck && at < 6f + CartQueueRules.StuckAfter + 0.5f, "cart: a walker who stops on the way for good is stuck, a few seconds after stopping");
        Check(CartWalk(200f, 1.2f, -1f, 0f, out at) == CartGuest.TooLong && Math.Abs(at - CartQueueRules.WalkMost) < 0.3f,
            "cart: no walk to the cart goes on past " + CartQueueRules.WalkMost + " s");
    }

    private static void CartWaiting()
    {
        Check(CartQueueRules.Waiting(true, 10f, 75f) == CartGuest.Keep, "cart: waiting their turn, before their patience is out, they wait");
        Check(CartQueueRules.Waiting(true, 75f, 75f) == CartGuest.GaveUp, "cart: waiting their turn too long, they give up");
    }

    // The counts: nobody who left the queue is a walk-off, the shift is one shorter only for a real give-up or four in a row.
    private static void CartCounts()
    {
        CartGuest[] cut = { CartGuest.NotHeld, CartGuest.NoRoute, CartGuest.EndsShort, CartGuest.Stuck, CartGuest.TooLong };
        foreach (CartGuest why in cut)
        {
            int came = 9, lost = 1, row = 0;
            bool shorter = CartQueueRules.Account(why, ref came, ref lost, ref row);
            Check(!shorter && came == 8 && lost == 1 && row == 1, "cart: " + why + ": back among those still to come (came 9 -> " + came + ", lost " + lost + ")");
        }
        {
            int came = 9, lost = 1, row = 0, shorterAt = -1;
            for (int n = 1; n <= CartQueueRules.RetriesMost; n++)
            {
                came++;   // the next sets off
                if (CartQueueRules.Account(CartGuest.NoRoute, ref came, ref lost, ref row) && shorterAt < 0)
                {
                    shorterAt = n;
                }
            }
            Check(shorterAt == CartQueueRules.RetriesMost && lost == 2 && row == 0,
                "cart: " + CartQueueRules.RetriesMost + " in a row who cannot get there make the shift one shorter, so no street holds it open for ever (at " + shorterAt + ")");
        }
        {
            int came = 9, lost = 1, row = 2;
            Check(CartQueueRules.Account(CartGuest.GaveUp, ref came, ref lost, ref row) && lost == 2 && came == 9 && row == 2,
                "cart: one who gave up waiting their turn makes the shift one shorter, and leaves the count in a row alone");
        }
        {
            int came = 9, lost = 1, row = 2;
            Check(!CartQueueRules.Account(CartGuest.There, ref came, ref lost, ref row) && !CartQueueRules.Account(CartGuest.Keep, ref came, ref lost, ref row)
                  && came == 9 && lost == 1 && row == 2, "cart: one still with the cart changes no count");
        }
        // The case seen in play: 8 served of 16, one lost, 7 still to come, one on the way who cannot get there. After: still 7 to come, and a free place for the next.
        {
            int customers = 16, served = 8, came = 10, lost = 1, row = 0;
            int toComeBefore = customers - served - lost;
            CartQueueRules.Account(CartGuest.NoRoute, ref came, ref lost, ref row);
            int toCome = customers - served - lost;
            int underWay = came - served - lost;
            Check(toCome == toComeBefore && toCome == 7 && underWay == 0 && underWay < toCome,
                "cart: the owner's case: 7 still to come before and after, nobody counted as on the way, so the next customer sets off");
        }
    }

    private static void CartRoutes()
    {
        Check(CartQueueRules.RouteFits(20f, 23f), "cart: a route along the pavement and over the crossing is one to send a customer along");
        Check(CartQueueRules.RouteFits(0.5f, 6f), "cart: a short walk round the cart is one to send a customer along");
        Check(!CartQueueRules.RouteFits(20f, 140f), "cart: a detour round the block (140 m for 20 m) is not");
        Check(!CartQueueRules.RouteFits(20f, -1f), "cart: no route is not");
        Check(CartQueueRules.RouteFits(10f, 10f * CartQueueRules.RouteMost + CartQueueRules.RouteSlack)
              && !CartQueueRules.RouteFits(10f, 10f * CartQueueRules.RouteMost + CartQueueRules.RouteSlack + 0.5f), "cart: the longest route from 10 m away is exact");
        Check(CartQueueRules.RouteFits(34f, CartQueueRules.RouteLongest) && !CartQueueRules.RouteFits(34f, CartQueueRules.RouteLongest + 0.5f)
              && CartQueueRules.RouteLongest / 1.2f < CartQueueRules.WalkMost, "cart: no route is longer than " + CartQueueRules.RouteLongest + " m, walked at 1.2 m/s inside " + CartQueueRules.WalkMost + " s");
    }

    private static void CartSame()
    {
        bool same = true;
        for (int run = 0; run < 2 && same; run++)
        {
            CartGuest a = CartWalk(25f, 1.1f, 3f, 2f, out float atA);
            CartGuest b = CartWalk(25f, 1.1f, 3f, 2f, out float atB);
            same = a == b && atA == atB;
        }
        Check(same, "cart: the same walk ends the same way at the same moment (nothing random, nothing of the machine)");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        float best = 30f, bestAt = 0f;
        int kept = 0;
        for (int i = 0; i < 200000; i++)
        {
            kept += CartQueueRules.Walking(true, true, true, false, 30f - (i % 100) * 0.01f, i * 0.0001f, 40f, ref best, ref bestAt) == CartGuest.Keep ? 1 : 0;
        }
        watch.Stop();
        Check(kept > 0 && watch.Elapsed.TotalMilliseconds < 50.0, "cart: 200,000 looks at a walker take " + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms (under 50)");
    }

    private static void CartWords()
    {
        foreach (CartGuest why in (CartGuest[])Enum.GetValues(typeof(CartGuest)))
        {
            string words = CartQueueRules.Words(why);
            Check(!string.IsNullOrEmpty(words) && words.IndexOf('!') < 0 && words.IndexOf((char)0x2013) < 0 && words.IndexOf((char)0x2014) < 0 && words.IndexOf(" - ", StringComparison.Ordinal) < 0,
                "cart: the log's words for " + why + " are plain");
        }
    }
}
