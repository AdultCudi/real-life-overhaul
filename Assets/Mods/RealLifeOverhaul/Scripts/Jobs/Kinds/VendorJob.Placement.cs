using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The drinks cart: room for the whole setup, checked again after its cut in the pavement has settled.</summary>
    public sealed partial class VendorJob
    {
        private const int PitchPicks = 12;            // six nearby places, each facing either side of the pavement
        private const float PitchSettle = 1.6f;      // seconds for the mesh to close after a refused cart is taken away
        private const float CrossingMargin = 0.4f;   // metres round the cart, vendor and queue, including their approaches
        private static readonly int CrosswalkLayer = LayerMask.NameToLayer("Crosswalk");
        private static readonly Collider[] PitchOverlaps = new Collider[64];
        private Vector3 _pitchStart;
        private int _pitchPick;
        private bool _pitchReady;
        private float _pitchRetryAt;

        private bool PutPitch(float now)
        {
            while (FindPitch(Ctx.PlayerPosition))
            {
                _cart = Ctx.Props.Stand(CartProp, _cartAt, _outwards);
                if (_cart == null)
                {
                    return false;
                }
                _solid.Fit(_cart, CartHalfLength, CartHalfDepth, Mathf.Min(VendorBack, CustomerOut));
                _solid.Solid(true, now);
                return true;
            }
            return false;
        }

        // Along the pavement, counter to the side with room; each candidate is tried only once, even after a cut fails.
        private bool FindPitch(Vector3 near)
        {
            if (_pitchPick == 0 && !CitySpots.Pavement(near, 8f, out _pitchStart))
            {
                return false;
            }
            while (_pitchPick < PitchPicks)
            {
                int pick = _pitchPick++;
                int attempt = pick / 2;
                Vector3 place = attempt == 0 ? _pitchStart
                    : _pitchStart + Quaternion.Euler(0f, attempt * 60f, 0f) * Vector3.forward * 3.5f;
                if (!CitySpots.Stand(place, 1.1f, out Vector3 cart) || !CitySpots.Along(cart, out Vector3 along)
                    || JobSpots.Flat(cart, near) < 1.6f)
                {
                    continue;
                }
                Vector3 across = Vector3.Cross(Vector3.up, along);
                Vector3 outwards = pick % 2 == 0 ? across : -across;
                if (!CrossingClear(cart, outwards))
                {
                    continue;
                }
                if (CitySpots.OnPavement(cart + along * 0.85f + outwards * 0.5f, out _)
                    && CitySpots.OnPavement(cart - along * 0.85f + outwards * 0.5f, out _)
                    && CitySpots.OnPavement(cart + along * 0.85f - outwards * 0.5f, out _)
                    && CitySpots.OnPavement(cart - along * 0.85f - outwards * 0.5f, out _)
                    && CitySpots.Stand(cart - outwards * VendorBack, 0.35f, out _post)
                    && CitySpots.Stand(cart + outwards * CustomerOut, 0.35f, out _counter)
                    && CitySpots.Stand(cart + outwards * QueueOut, 0.35f, out _queue)
                    && CitySpots.Reachable(_counter, _post) && CitySpots.Reachable(_queue, _counter))
                {
                    _cartAt = cart;
                    _outwards = outwards;
                    return true;
                }
            }
            return false;
        }

        // Include triggers on every layer: a crossing may be identified by its layer, tag or trigger's name.
        private bool CrossingClear(Vector3 cart, Vector3 outwards)
        {
            float middle = (QueueOut - VendorBack) * 0.5f;
            var half = new Vector3(CartHalfLength + CrossingMargin, 1f,
                (QueueOut + VendorBack) * 0.5f + CrossingMargin);
            int count = Physics.OverlapBoxNonAlloc(cart + outwards * middle + Vector3.up * 0.5f, half,
                PitchOverlaps, Quaternion.LookRotation(outwards), Physics.AllLayers, QueryTriggerInteraction.Collide);
            bool clear = count < PitchOverlaps.Length;   // a full buffer may conceal a crossing
            for (int i = 0; i < count; i++)
            {
                Collider hit = PitchOverlaps[i];
                if (hit != null && (hit.gameObject.layer == CrosswalkLayer || hit.CompareTag("Crosswalk")
                    || hit.name == "CrosswalkTrigger"))
                {
                    clear = false;
                }
                PitchOverlaps[i] = null;
            }
            if (!clear && Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs check (cart crossing clearance): candidate refused (crossing overlap or full query buffer): FAIL.");
            }
            return clear;
        }

        private bool PitchReady(float now)
        {
            if (_pitchReady)
            {
                return true;
            }
            if (_cart == null)
            {
                if (now >= _pitchRetryAt && !PutPitch(now))
                {
                    Ctx.Say("reallife_job_vendor_no_room", 8f);
                    Finish(JobEnd.Interrupted);
                }
                return false;
            }
            bool cut = false, kept = true, way = CitySpots.Reachable(_counter, _post);
            if (_solid.VerifyDue && !_solid.Verify(now, _post, _counter, out cut, out kept, out way))
            {
                return false;   // not settled yet; a cart that can't be cut is steered round and checked at once
            }
            bool queue = CitySpots.OnPavement(_queue, out _) && CitySpots.Reachable(_queue, _counter);
            _pitchReady = kept && way && queue;   // an uncut middle is only walked through; a lost place or way is a dead pitch
            Ctx.Logger?.Info("Jobs check (cart solid): walker radius "
                + _solid.Radius.ToString("F2", CultureInfo.InvariantCulture)
                + " m; cut out of the mesh: the cart's middle " + (cut ? "yes" : "no")
                + "; still walkable: the vendor's place and the customer's place " + (kept ? "yes" : "no")
                + ", a way on foot from one to the other " + (way ? "yes" : "no")
                + ", the queue reachable " + (queue ? "yes" : "no") + ": "
                + (_pitchReady ? "PASS" : "FAIL (candidate refused; the next place is tried)") + ".");
            if (!_pitchReady)
            {
                DropPitch();
                _pitchRetryAt = now + PitchSettle;
                return false;
            }
            Ctx.Stand.Place(_post, _outwards, PostReach, IdleClip);
            Mark(_post, _postLabel);
            return true;
        }

        private void DropPitch()
        {
            _solid.Clear();
            Ctx.Props.Return(_cart);
            _cart = null;
        }
    }
}
