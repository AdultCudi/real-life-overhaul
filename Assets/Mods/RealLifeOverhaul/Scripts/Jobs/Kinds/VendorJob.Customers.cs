using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Characters.EmojiSystem;
using Helpers;
using RealLifeOverhaul.GameUi;
using Streets.Pedestrians;
using UI.Elements;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The drinks cart: customers coming, queueing, ordering and leaving.</summary>
    public sealed partial class VendorJob
    {
        private float _arrivalSince = -1f;   // seconds: first attempt to send the next customer, even if no route could be used

        // Somebody new comes along while there is a free place and customers left in the shift.
        private void Arrivals(float now)
        {
            if (now < _nextArrival || _came - _served - _lost >= LeftToServe)
            {
                return;
            }
            int slot = _state[0] == Guest.None ? 0 : _state[1] == Guest.None ? 1 : -1;
            if (slot < 0)
            {
                _arrivalSince = -1f;
                return;
            }
            if (_arrivalSince < 0f)
            {
                _arrivalSince = now;
            }
            if (now - _arrivalSince >= CartQueueRules.WalkMost)
            {
                ArrivalFailed(now);
                return;
            }
            if (_soon > 0)
            {
                _soon--;
                _nextArrival = now + Random.Range(1.2f, 2.4f);   // somebody was tempted: the next one follows soon
            }
            else
            {
                _nextArrival = now + Random.Range(2.5f, 6f) / Ctx.Offer.CrowdFactor;   // a quiet street or a rush, as the offer drew
            }
            // Only ever put down out of the player's sight.
            if (!CitySpots.PavementRing(_cartAt, 11f, 20f, true, out Vector3 from) && !CitySpots.PavementRing(_cartAt, 20f, 34f, true, out from))
            {
                return;
            }
            // To the counter if free, else the place behind; only from where the walk is whole and no detour round the block.
            bool counterFree = _front < 0;
            Vector3 place = counterFree ? _counter : _queue;
            float straight = JobSpots.Flat(from, place);
            if (!CitySpots.Reachable(from, place, out float route) || !CartQueueRules.RouteFits(straight, route))
            {
                if (++_routesRefused == 1 && Meta.Dev.Verbose)
                {
                    Ctx.Logger?.Info("Jobs: vendor: nobody sent from " + straight.ToString("F0") + " m away: " + (route < 0f ? "no way on foot reaches the cart from there"
                                     : "the way there is " + route.ToString("F0") + " m") + "; another place is tried for the next (said once a shift).");
                }
                return;
            }
            StreetActor person = Ctx.Cast.Put(from, false);
            if (person == null)
            {
                return;
            }
            if (!person.MoveTo(place, Pace.Walk))
            {
                Ctx.Cast.Release(person);
                return;
            }
            _arrivalSince = -1f;
            _who[slot] = person;
            _lease[slot] = person.Lease;
            _state[slot] = counterFree ? Guest.Stepping : Guest.Coming;
            _until[slot] = now + CartQueueRules.WalkMost;
            _best[slot] = straight;
            _bestAt[slot] = now;
            if (counterFree)
            {
                _front = slot;
            }
            _came++;
        }

        private void Attend(int i, float now)
        {
            StreetActor person = _who[i];
            if (_state[i] == Guest.None)
            {
                return;
            }
            bool held = Held(i);
            if (!held && (_state[i] == Guest.Served || _state[i] == Guest.Leaving))
            {
                Gone(i, false);   // served or on their way: nothing more to count
                return;
            }
            CartGuest fate;
            switch (_state[i])
            {
                case Guest.Coming:
                    fate = Walking(i, person, _queue, now);
                    if (fate == CartGuest.There)
                    {
                        person.StandAt(_queue, _cartAt);
                        _state[i] = Guest.Queued;
                        _until[i] = now + CartQueueRules.WaitMost;
                    }
                    else if (fate != CartGuest.Keep)
                    {
                        Withdraw(i, fate, now);
                    }
                    break;
                case Guest.Queued:
                    fate = CartQueueRules.Waiting(held, now, _until[i]);
                    if (fate != CartGuest.Keep)
                    {
                        Withdraw(i, fate, now);   // taken off the street, or never got to the front and gives up
                    }
                    else if (_front < 0)
                    {
                        if (!person.MoveTo(_counter, Pace.Walk))
                        {
                            Withdraw(i, CartGuest.NoRoute, now);
                            break;
                        }
                        _front = i;
                        _state[i] = Guest.Stepping;
                        _until[i] = now + 15f;
                        _best[i] = JobSpots.Flat(person.Position, _counter);
                        _bestAt[i] = now;
                    }
                    break;
                case Guest.Stepping:
                    fate = Walking(i, person, _counter, now);
                    if (fate == CartGuest.There)
                    {
                        person.StandAt(_counter, _cartAt);
                        _state[i] = Guest.Ordering;
                        _wants = Random.Range(0, JobKeys.Choices);
                        _orderedAt = now;
                        _orderEnds = now + Patience;
                    }
                    else if (fate != CartGuest.Keep)
                    {
                        Withdraw(i, fate, now);
                    }
                    break;
                case Guest.Ordering:
                    if (!held)
                    {
                        Withdraw(i, CartGuest.NotHeld, now);   // the mod's doing, never a walk-off
                    }
                    else if (now >= _orderEnds)
                    {
                        // Kept waiting: they go, and say so.
                        _misses++;
                        _lost++;
                        Ctx.Animations.Play(person.Character, JobClip.Annoyed);
                        Ctx.Cast.Emoji(person, CharacterEmojiName.CommonRedAngry, 2.5f);
                        Ctx.Say(JobText.Pick(Left, ref _line), 5f);
                        if (Meta.Dev.Verbose)
                        {
                            Ctx.Logger?.Info("Jobs: vendor: a customer walked off unserved after " + Patience.ToString("F0") + " s (walk-off " + _misses + " of " + MissesAllowed + ").");
                        }
                        Leave(i, now, 1.4f);
                        if (_misses >= MissesAllowed)
                        {
                            Ctx.Say("reallife_job_vendor_sacked", 8f);
                            Conclude(true);
                        }
                    }
                    break;
                case Guest.Served:
                    if (now >= _until[i])
                    {
                        Leave(i, now, 0f);
                    }
                    else if (_beat == 0 && _cup != null && now >= _until[i] - 1.0f)
                    {
                        // The cup leaves the counter for their hand.
                        Ctx.Props.Return(_cup);
                        _cup = null;
                        _cupHeld.Give(person.Character, Ctx.Props, CupGrips);
                    }
                    break;
                case Guest.Leaving:
                    if (now < _walkAt[i] + 1.5f)
                    {
                        break;   // a last moment at the counter, then a few steps before "arrived" means anything
                    }
                    if (person.HasArrived || now >= _until[i] || JobSpots.Flat(person.Position, _cartAt) > 26f)
                    {
                        Gone(i, true);
                    }
                    break;
            }
        }

        // No unseen starting place, usable route or walker for a whole walk's time: count the failed attempt too.
        private void ArrivalFailed(float now)
        {
            _came++;
            bool shorter = CartQueueRules.Account(CartGuest.NoRoute, ref _came, ref _lost, ref _cantReach);
            _arrivalSince = -1f;
            _nextArrival = now + CartQueueRules.NextSoon;
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: vendor: no customer could set off for "
                    + CartQueueRules.WalkMost.ToString("F0", CultureInfo.InvariantCulture)
                    + " s; not a walk-off, " + (shorter ? "the shift is one shorter" : "the next starting place is tried soon")
                    + "; " + LeftToServe + " still to come.");
            }
        }

        // Still the cart's: on the street, in use, and the same lease as when they set off.
        private bool Held(int i)
        {
            StreetActor person = _who[i];
            return person != null && person.IsValid && person.InUse && person.Lease == _lease[i] && person.Character.gameObject.activeInHierarchy;
        }

        private CartGuest Walking(int i, StreetActor person, Vector3 place, float now)
        {
            if (!Held(i))
            {
                return CartGuest.NotHeld;
            }
            bool known = person.RouteKnown;
            CartGuest fate = CartQueueRules.Walking(true, known, known && person.RouteWhole, person.HasArrived, JobSpots.Flat(person.Position, place), now, _until[i],
                ref _best[i], ref _bestAt[i]);
            if (fate == CartGuest.There)
            {
                _cantReach = 0;
            }
            return fate;
        }

        // Out of the queue at once and never a walk-off (CartQueueRules.Account): someone in view walks away, anyone else goes now.
        private void Withdraw(int i, CartGuest why, float now)
        {
            StreetActor person = _who[i];
            bool held = Held(i);
            bool shorter = CartQueueRules.Account(why, ref _came, ref _lost, ref _cantReach);
            if (!shorter)
            {
                _nextArrival = Mathf.Min(_nextArrival, now + CartQueueRules.NextSoon);
            }
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: vendor: a customer left the queue (" + _state[i] + "): " + CartQueueRules.Words(why) + "; not a walk-off, "
                                 + (shorter ? "the shift is one shorter (" + LeftToServe + " still to come)" : "back among the " + LeftToServe + " still to come, the next sets off soon")
                                 + "; walk-offs " + _misses + " of " + MissesAllowed + ".");
            }
            if (held && person.Visible)
            {
                Leave(i, now, 0f);
                return;
            }
            Gone(i, held);
        }

        // Away along the pavement; taken off the street once well away.
        private void Leave(int i, float now, float after)
        {
            StreetActor person = _who[i];
            if (_front == i)
            {
                _front = -1;
                _orderOver = now;
            }
            ClearCounter();
            _state[i] = Guest.Leaving;
            _walkAt[i] = now + after;
            _until[i] = now + 30f + after;
            if (after <= 0f)
            {
                Walk(person);
            }
            else if (Ctx.Director != null && Ctx.Director.isActiveAndEnabled)
            {
                Ctx.Director.StartCoroutine(Guard.Routine(WalkAfter(person, person.Lease, after)));
            }
        }

        private void Walk(StreetActor person)
        {
            // With nowhere to walk to they stay, and their own slot takes them off the street (the Leaving step).
            if (StreetSpots.AwayFrom(person.Position, _cartAt, 22f, false, out Vector3 to))
            {
                person.MoveTo(to, Pace.Walk);
            }
        }

        private System.Collections.IEnumerator WalkAfter(StreetActor person, int lease, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (person != null && person.IsValid && person.InUse && person.Lease == lease)
            {
                Walk(person);
            }
        }

        /// <param name="release">Still the cart's: handed back to the cast; otherwise only forgotten.</param>
        private void Gone(int i, bool release)
        {
            if (_front == i)
            {
                _front = -1;
                _orderOver = Time.time;
                ClearCounter();
            }
            if (release)
            {
                Ctx.Cast.Release(_who[i]);
            }
            _who[i] = null;
            _state[i] = Guest.None;
        }

        // Nothing left on the counter or in a customer's hand, and no moment of serving pending.
        private void ClearCounter()
        {
            _cupHeld.Take();
            _beat = 0;
            if (_cup != null)
            {
                Ctx.Props.Return(_cup);
                _cup = null;
            }
        }

        private void ReleaseGuests()
        {
            for (int i = 0; i < JobCast.Size; i++)
            {
                Ctx.Cast.Release(_who[i]);
                _who[i] = null;
                _state[i] = Guest.None;
            }
        }
    }
}
