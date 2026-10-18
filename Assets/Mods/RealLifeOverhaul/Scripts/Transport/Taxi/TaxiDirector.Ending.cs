using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The taxi director's part for how a call ends: the arrival, the ride, a no-show, a cancel, and the charges.</summary>
    public sealed partial class TaxiDirector
    {
        private void Arrived(float now, Vector3 player)
        {
            Vector3 at = _cab.Car.transform.position;
            float away = Mathf.Sqrt(CitySpots.Flat(player - at));
            _step = Step.Waiting;
            _waitUntil = now + TaxiRules.WaitSeconds;
            _grace = false;
            if (Loud)
            {
                _cab.Measure(out float along, out float aside, out float angle);
                bool good = Mathf.Abs(along) <= ArrivedWithin && angle <= 25f;
                Say("arrival", "after " + _cab.Took.ToString("F0") + " s over " + _cab.StartedBack.ToString("F0") + " m; " + away.ToString("F1", CultureInfo.InvariantCulture)
                    + " m from the player; standing "
                               + (_cab.AtKerb ? "at the kerb" : "on the lane") + ", its middle " + along.ToString("F1", CultureInfo.InvariantCulture) + " m along and "
                               + aside.ToString("F1", CultureInfo.InvariantCulture)
                               + " m aside of the lane point it was sent to, " + angle.ToString("F0") + " degrees off the lane: " + (good ? "PASS" : "FAIL"));
            }
            if (TaxiRules.FarAtArrival(away))
            {
                bool marked = _mark.Show(at + Vector3.up * 1.2f, _markName ?? (_markName = "reallife_taxi_mark".GetLocalization()));
                Notifications.Show(NotificationType.Warning, marked ? "reallife_taxi_note_waiting_marked" : "reallife_taxi_note_waiting", new Dictionary<string, string>(2)
                {
                    { "metres", Mathf.RoundToInt(away).ToString() }, { "seconds", TaxiRules.WaitSeconds.ToString("F0") }
                }, 10f);
                if (Loud)
                {
                    Say("wait", "the player is " + away.ToString("F0") + " m from where it stands: it waits " + TaxiRules.WaitSeconds.ToString("F0") + " s; the game's marker on it: "
                                + (marked ? "shown" : "not shown (a job or an outing has the marker)"));
                }
            }
            else
            {
                Notifications.Show(NotificationType.Success, "reallife_taxi_note_here", null, 8f);
                if (Loud)
                {
                    Say("wait", "the player is " + away.ToString("F0") + " m from where it stands: it waits " + TaxiRules.WaitSeconds.ToString("F0") + " s");
                }
            }
        }

        // The game's ride has begun with this taxi: the call-out, and the taxi goes back to the traffic AI, where the game
        // expects the taxi it puts on the road again after the ride.
        private void Ride()
        {
            float paid = Charge(_fee, CallOutKey);
            if (Loud)
            {
                Say("ride", "the game's ride began with this taxi; call-out " + _fee.ToString("F0") + " charged (" + paid.ToString("F0") + " taken)");
            }
            Stand(TaxiCab.Leave.AsTraffic, "a called taxi whose ride has begun");
        }

        private void NoShow()
        {
            float paid = Charge(TaxiRules.NoShowFee(_fee), NoShowKey);
            if (Loud)
            {
                Say("no show", "it stood " + _cab.Out.ToString("F0") + " s and nobody got in: " + paid.ToString("F0") + " taken of " + TaxiRules.NoShowFee(_fee).ToString("F0"));
            }
            Tell(paid, "reallife_taxi_note_noshow_fee", "reallife_taxi_note_noshow", NotificationType.Warning);
            Stand(TaxiCab.Leave.Unseen, "a called taxi nobody came to");
        }

        /// <summary>What a cancel would cost now.</summary>
        public float CancelFee => _step == Step.Idle ? 0f : TaxiRules.CancelFee(_fee, _step == Step.Coming ? _cab.Remaining : 0f);

        /// <summary>Metres of road the taxi still has to drive; 0 when it stands. Only while one is out.</summary>
        public float Remaining => _step == Step.Coming ? _cab.Remaining : 0f;

        /// <summary>True while the taxi stands for the caller.</summary>
        public bool Standing => _step == Step.Waiting;

        /// <summary>"Cancel it", from the phone or the test: what was charged.</summary>
        public float Cancel()
        {
            if (_step == Step.Idle)
            {
                return 0f;
            }
            try
            {
                float due = CancelFee;
                float metres = Remaining;
                float paid = Charge(due, CancelKey);
                if (Loud)
                {
                    Say("cancel", "by the caller with " + metres.ToString("F0") + " m still to drive: " + paid.ToString("F0") + " taken of " + due.ToString("F0"));
                }
                Stand(TaxiCab.Leave.Unseen, "a called taxi that was cancelled");
                return paid;
            }
            catch (Exception e)
            {
                Fail(e);
                return 0f;
            }
        }

        // The caller is no longer waiting in the street: a cancel, on a cancel's terms.
        private void WentAway(string how)
        {
            float due = CancelFee;
            float metres = Remaining;
            float paid = Charge(due, CancelKey);
            if (Loud)
            {
                Say("cancel", how + " with " + metres.ToString("F0") + " m still to drive: " + paid.ToString("F0") + " taken of " + due.ToString("F0"));
            }
            Tell(paid, "reallife_taxi_note_away_fee", "reallife_taxi_note_away", NotificationType.Info);
            Stand(TaxiCab.Leave.Unseen, "a called taxi whose caller went another way");
        }

        private void CannotCome(string why)
        {
            Notifications.Show(NotificationType.Warning, "reallife_taxi_note_cannot_come", null, 9f);
            Released(why, TaxiCab.Leave.Unseen);
        }

        // Let go with nothing charged; the released line says why.
        private void Released(string why, TaxiCab.Leave how)
        {
            Stand(how, "a called taxi let go, nothing charged: " + why);
        }

        // One notice for an ending that may or may not have cost something.
        private static void Tell(float paid, string feeKey, string freeKey, NotificationType type)
        {
            if (paid > 0f)
            {
                Notifications.Show(type, feeKey, new Dictionary<string, string>(1) { { "amount", TransitText.Money(paid) } }, 9f);
            }
            else
            {
                Notifications.Show(type, freeKey, null, 9f);
            }
        }

        // Takes what it can of a charge without the caller's money going below nothing (never forced); returns what was taken.
        private static float Charge(float due, string transaction)
        {
            GameInstance save = SaveGameManager.Current;
            float take = save != null ? TaxiRules.Payable(due, save.Money) : 0f;
            if (take > 0f && !GameManager.ChangeMoneySafe(-take, new TransactionInfo(transaction), null, null, force: false, showNotification: false))
            {
                take = 0f;
            }
            return take;
        }

        // Everything the call put anywhere comes off: the vehicle, the marker, the map's handler. Safe when idle.
        private void Stand(TaxiCab.Leave how, string by)
        {
            bool had = _cab.State != TaxiCab.Phase.Idle;
            _cab.Release(how, by);
            if (_step != Step.Idle)
            {
                if (Loud)
                {
                    Say("released", by + ": " + (!had ? "its vehicle had gone already" : _cab.LeftAsTraffic ? "handed back to traffic where it was" : "taken off the road"));
                }
            }
            _mark.Hide();
            if (_mapSeen != null)
            {
                GlobalEvents.onCityMapToggle -= _onMap;
                _mapSeen = null;
            }
            _step = Step.Idle;
            _mapOurs = false;
            _replanned = false;
            _grace = false;
        }
    }
}
