using System;

internal static partial class Program
{
    private static void VendorCartFixes()
    {
        string vendor = ModSource(@"Jobs\Kinds\VendorJob.cs");
        string placement = ModSource(@"Jobs\Kinds\VendorJob.Placement.cs");
        string crossing = MemberText(placement, "private bool CrossingClear(");
        string shared = MemberText(ModSource(@"CityLife\CitySpots.cs"), "public static bool ClearOfCrossings(");
        Check(crossing.Contains("CitySpots.ClearOfCrossings(cart + outwards * middle + Vector3.up * 0.5f, half, Quaternion.LookRotation(outwards), out _)")
              && shared.Contains("Physics.OverlapBoxNonAlloc(") && shared.Contains("Physics.AllLayers")
              && shared.Contains("QueryTriggerInteraction.Collide") && shared.Contains("hit.gameObject.layer == _crosswalkLayer")
              && shared.Contains("hit.CompareTag(\"Crosswalk\")")
              && shared.Contains("full = count >= CrossingOverlaps.Length;") && shared.Contains("CrossingOverlaps[i] = null;"),
            "vendor cart: the whole setup refuses crossing layers, tags and triggers, including a full query buffer");
        Check(crossing.Contains("CartHalfLength + CrossingMargin")
              && crossing.Contains("(QueueOut + VendorBack) * 0.5f + CrossingMargin"),
            "vendor cart: crossing clearance covers the cart, vendor and queue with room round them");

        string find = MemberText(placement, "private bool FindPitch(");
        Check(find.Contains("int pick = _pitchPick++;") && find.Contains("_pitchPick < PitchPicks")
              && find.Contains("!CrossingClear(cart, outwards)")
              && find.Contains("CitySpots.Reachable(_queue, _counter)"),
            "vendor cart: each candidate is tried once and the queue must reach the counter before the cart is put down");
        string tick = MemberText(vendor, "public override void Tick(");
        int ready = tick.IndexOf("if (!PitchReady(now))", StringComparison.Ordinal);
        int held = tick.IndexOf("Ctx.Stand.Tick(", StringComparison.Ordinal);
        Check(ready >= 0 && held > ready && !MemberText(vendor, "protected override bool OnBegin(").Contains("Ctx.Stand.Place("),
            "vendor cart: the player cannot be held at an unverified pitch");
        string verify = MemberText(placement, "private bool PitchReady(");
        Check(verify.Contains("_solid.Verify(now, _post, _counter,")
              && verify.Contains("_pitchReady = kept && way && queue;")
              && verify.Contains("CitySpots.Reachable(_queue, _counter)")
              && verify.Contains("if (!_pitchReady)") && verify.Contains("DropPitch();")
              && verify.Contains("_pitchRetryAt = now + PitchSettle;") && verify.Contains("!PutPitch(now)")
              && verify.Contains("Finish(JobEnd.Interrupted);") && verify.Contains("Jobs check (cart solid)"),
            "vendor cart: a failed solid check takes the cart back, waits for the mesh and tries the next candidate or ends safely");
        int due = verify.IndexOf("if (_solid.VerifyDue)", StringComparison.Ordinal);
        int asked = verify.IndexOf("CitySpots.Reachable(_counter, _post)", StringComparison.Ordinal);
        Check(due >= 0 && asked > verify.IndexOf("else", due, StringComparison.Ordinal) && verify.IndexOf("else", due, StringComparison.Ordinal) > due,
            "vendor cart: while the cut settles a frame asks the mesh nothing (the way is asked once, by Verify or for a cart that cannot be cut)");
        string drop = MemberText(placement, "private void DropPitch(");
        Check(drop.Contains("_solid.Clear();") && drop.Contains("Ctx.Props.Return(_cart);") && drop.Contains("_cart = null;"),
            "vendor cart: a refused pitch leaves no cart or cut behind");

        string customers = ModSource(@"Jobs\Kinds\VendorJob.Customers.cs");
        string arrivals = MemberText(customers, "private void Arrivals(");
        int timeout = arrivals.IndexOf("now - _arrivalSince >= CartQueueRules.WalkMost", StringComparison.Ordinal);
        Check(timeout >= 0 && timeout < arrivals.IndexOf("CitySpots.PavementRing(", StringComparison.Ordinal)
              && arrivals.Contains("ArrivalFailed(now);") && arrivals.Contains("_arrivalSince = -1f;"),
            "vendor cart: no starting place, rejected routes and failed walk orders share a bounded arrival time");
        int wanted = arrivals.IndexOf("_came - _served - _lost >= LeftToServe", StringComparison.Ordinal);
        Check(wanted >= 0 && arrivals.IndexOf("_arrivalSince = -1f;", wanted, StringComparison.Ordinal) > wanted
              && arrivals.IndexOf("_arrivalSince = -1f;", wanted, StringComparison.Ordinal) < arrivals.IndexOf("int slot", StringComparison.Ordinal)
              && tick.IndexOf("_arrivalSince = -1f;", tick.IndexOf("_skip = null;", StringComparison.Ordinal) + 1, StringComparison.Ordinal) > 0,
            "vendor cart: the arrival time restarts when nobody is wanted and after the clock's run, so neither counts as a failed attempt");
        string failed = MemberText(customers, "private void ArrivalFailed(");
        Check(failed.Contains("_came++;")
              && failed.Contains("CartQueueRules.Account(CartGuest.NoRoute, ref _came, ref _lost, ref _cantReach)")
              && failed.Contains("_nextArrival = now + CartQueueRules.NextSoon;")
              && failed.Contains("Meta.Dev.Verbose") && failed.Contains("not a walk-off"),
            "vendor cart: failed arrivals are replaced without a walk-off and repeated failures shorten the shift");
        string attend = MemberText(customers, "private void Attend(");
        Check(attend.Contains("if (!person.MoveTo(_counter, Pace.Walk))")
              && attend.Contains("Withdraw(i, CartGuest.NoRoute, now);")
              && attend.Contains("Walking(i, person, _queue, now)") && attend.Contains("Walking(i, person, _counter, now)")
              && MemberText(customers, "private CartGuest Walking(").Contains("ref _best[i], ref _bestAt[i]")
              && MemberText(customers, "private void Withdraw(").Contains("CartQueueRules.NextSoon"),
            "vendor cart: failed queue steps and stalled walks release the customer and send another soon");
        Check(FixLineEverywhere("drinks cart keeps clear of crossings", "carrinho de bebidas fica longe das faixas",
                                "饮品推车会避开人行横道", "Getränkewagen hält Abstand zu Fußgängerüberwegen",
                                "chariot à boissons reste à distance des passages piétons",
                                "carrito de bebidas se mantiene lejos de los pasos de peatones",
                                "тележка с напитками не ставится на пешеходных переходах",
                                "drankenkar blijft uit de buurt van oversteekplaatsen",
                                "içecek arabası yaya geçitlerinden uzak duruyor"),
            "vendor cart: What's new describes the fix in every language");
    }
}
