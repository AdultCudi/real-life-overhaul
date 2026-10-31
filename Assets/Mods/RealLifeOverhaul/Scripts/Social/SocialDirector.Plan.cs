using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The social director's part for the plan that is on, and the phone ringing. A ring is answered three ways: the panel's
    /// button (or the interaction key), a click on its notice, and calling them from the phone while it rings. While a screen
    /// hides the panel (the phone open, a menu) the ring waits, a while at most, so opening the phone to answer never lets it
    /// run out unseen. With Developer diagnostics on, one "Social check (ring)" line for each ring's end.
    /// COST: while it rings, one look a frame at whether a screen is open; nothing otherwise.
    /// </summary>
    public sealed partial class SocialDirector
    {
        private const float RingSeconds = 22f;     // real seconds the phone rings
        private const float RingHoldMost = 120f;   // real seconds a ring waits at most, all told, while a screen hides its panel

        private bool _ringing;
        private SocialPerson _ringPerson;
        private float _ringUntil;
        private float _ringLast;    // real time of the ring's last look
        private float _ringHeld;    // real seconds it has waited behind a screen
        private string _ringPrompt;
        private Action _onRingClicked;

        // Once a second: the reminder, their calling it off, and the outing staged near the time.
        private void Plan()
        {
            SocialPlan plan = SocialState.Plan;
            if (!plan.Active || _scene is OutingScene)
            {
                return;
            }
            SocialPerson person = SocialState.Get(plan.Person);
            if (person == null || !person.Met)
            {
                plan.Clear();
                return;
            }
            if (!Settings.SocialOutings)
            {
                // Nobody is stood up over an option.
                Dictionary<string, string> off = SocialLife.Data(person, "why", SocialLife.T("reallife_social_why_outings_off"));
                off["outing"] = SocialLife.T(SocialCatalogue.Outing(plan.Kind).Name);
                SocialLife.Toast(NotificationType.Warning, "reallife_social_break_dropped", off, 10f);
                Logger?.Info("Social: the plan with " + person.Name + " was dropped (OutingsOff).");
                plan.Clear();
                return;
            }
            float left = plan.Time - SocialLife.Now;
            if (left < -SocialRules.StoodUpMinutes)
            {
                SocialLife.StoodUp();
                return;
            }
            if (!plan.Checked && left < 90f)
            {
                plan.Checked = true;
                if (left > 15f && Random.value < SocialRules.CancelOdds(person, plan.Kind, RainHelper.isRaining))
                {
                    TheyCancel(person, plan);
                    return;
                }
            }
            if (left > SocialRules.EarlyMinutes)
            {
                return;
            }
            if (!plan.Reminded)
            {
                plan.Reminded = true;
                Dictionary<string, string> data = SocialLife.Data(person, "place", plan.Place);
                data["time"] = SocialLife.Clock(plan.Time);
                data["outing"] = SocialLife.T(SocialCatalogue.Outing(plan.Kind).Name);
                SocialLife.Toast(NotificationType.Info, "reallife_social_plan_remind", data, 12f);
            }
            if (_scene != null || !StreetReady())
            {
                return;
            }
            if (!SocialPlaces.Resolve(plan.Street, plan.Number, out Venue venue)
                && !SocialPlaces.Find(plan.Kind, new Vector3(plan.X, plan.Y, plan.Z), 0f, 250f, out venue, null, plan.Time))
            {
                // The place is not what it was: called off, nobody's fault.
                SocialLife.SendText(person, "reallife_social_plan_lost", SocialLife.Data(person), true);
                plan.Clear();
                return;
            }
            Venue to = default;
            if (plan.ToStreet != null)
            {
                SocialPlaces.Resolve(plan.ToStreet, plan.ToNumber, out to);
            }
            var scene = new OutingScene(person, plan.Kind, venue, plan.Time, plan.Arrives, plan.TheirIdea, to, false);
            RefreshContext(InstanceBehavior<GameManager>.Instance, Time.time);
            if (!Begin(scene))
            {
                SocialLife.SendText(person, "reallife_social_plan_lost", SocialLife.Data(person), true);
                plan.Clear();
            }
        }

        private void TheyCancel(SocialPerson person, SocialPlan plan)
        {
            PlanKind kind = plan.Kind;
            bool rain = RainHelper.isRaining && kind == PlanKind.Walk;
            Dictionary<string, string> data = SocialLife.PlanData(person, plan, 0f);
            data["outing"] = SocialLife.T(SocialCatalogue.Outing(kind).Name);
            SocialLife.SendText(person, SocialLines.Pick(rain ? SocialCatalogue.TextCancelRain : SocialCatalogue.TextCancel), data, true);
            Logger?.Info("Social: " + person.Name + " called off " + kind + ".");
            plan.Clear();
            // The next call to them picks it up again.
            if (kind <= PlanKind.Business && !SocialState.Offer.Active)
            {
                SocialState.Offer.Person = person.Index;
                SocialState.Offer.Kind = (int)kind;
                SocialState.Offer.Amount = 0;
                SocialState.Offer.Expires = SocialLife.Now + 48f * 60f;
            }
        }

        private bool CanRing()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            return !_ringing && _scene == null && gm != null && gm.selectedVehicle == null && !PlayerHelper.IsUsingVehicle && LifeEvents.IsSafeForEvent()
                   && !UiOpen() && !JobDirector.OnShift && !CrimeDirector.IsActive && !PoliceAttention.IsBusy;
        }

        private bool StartRing(SocialPerson person, float real)
        {
            if (person == null || !person.Met || !CanRing())
            {
                return false;
            }
            _ringing = true;
            _ringPerson = person;
            _ringUntil = real + RingSeconds;
            _ringLast = real;
            _ringHeld = 0f;
            Dictionary<string, string> data = SocialLife.Data(person, "key", Keys.Label(KeyAction.Interact));
            _ringPrompt = SocialLife.Say("reallife_social_ring_prompt", data);
            // Clicking the notice answers, as the panel's button does: the game's own notices open what they tell of.
            SocialLife.Toast(NotificationType.Info, "reallife_social_ringing", data, RingSeconds, _onRingClicked ?? (_onRingClicked = Guard.Wrap(RingClicked)));
            SocialState.LastCallInDay = SocialLife.Day;
            Logger?.Info("Social: " + person.Name + " is ringing the player.");
            return true;
        }

        private void Ring(float real)
        {
            float step = real - _ringLast;
            _ringLast = real;
            if (_ringPerson != null && _ringHeld < RingHoldMost && UiOpen())
            {
                // Behind a screen the panel cannot be pressed: the ring waits for it.
                _ringHeld += step;
                _ringUntil += step;
            }
            if (_ringPerson == null || real > _ringUntil || !Settings.Social)
            {
                RingCheck(_ringPerson, "not answered (" + _ringHeld.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " s of it behind a screen)");
                // Not answered: a text instead, and what they wanted still stands. With no text (the day's are used up) the
                // player never heard what it was, so it lapses at no cost, as an offer whose text is refused does.
                _ringing = false;
                if (_ringPerson != null && Settings.Social
                    && !SocialLife.SendText(_ringPerson, SocialLines.Pick(SocialCatalogue.TextMissedCall), SocialLife.Data(_ringPerson)))
                {
                    SocialOffer offer = SocialState.Offer;
                    if (offer.Active && offer.Person == _ringPerson.Index)
                    {
                        offer.Clear();
                    }
                }
                _ringPerson = null;
            }
        }

        private void AnswerRing(string how)
        {
            if (!_ringing || _ringPerson == null)
            {
                return;
            }
            _keyFrame = Time.frameCount;
            _ringing = false;
            SocialPerson caller = _ringPerson;
            _ringPerson = null;
            SocialPhone.Incoming = caller.Index;
            bool opened = SocialPhone.Call(caller);
            if (!opened)
            {
                SocialPhone.Incoming = -1;
            }
            RingCheck(caller, opened ? "answered " + how + "; the phone opens on their call" : "answered " + how + ", but their contact could not be had");
        }

        // The ring's notice was clicked: answered, whatever screen is open, unless a call or a conversation is on, which opening the
        // phone on the caller would end (the ring waits for it), or a question box, which the phone does not open over.
        private void RingClicked()
        {
            if (_broken || !_ringing || _ringPerson == null || DialogController.current != null || HudConfirm.isOpen)
            {
                return;
            }
            AnswerRing("from its notice");
        }

        /// <summary>
        /// A call placed to somebody whose ring is on (the phone opened over the panel, then Call on their contact): their call
        /// answered, the ring stopped. False, changing nothing, if they are not the one ringing.
        /// </summary>
        internal static bool TakeRing(int index)
        {
            SocialDirector d = Instance;
            if (d == null || !d._ringing || d._ringPerson == null || d._ringPerson.Index != index)
            {
                return false;
            }
            SocialPerson caller = d._ringPerson;
            d._ringing = false;
            d._ringPerson = null;
            d.RingCheck(caller, "answered by calling them from the phone");
            return true;
        }

        // One line for each ring's end, with Developer diagnostics on.
        private void RingCheck(SocialPerson who, string what)
        {
            if (Meta.Dev.Verbose && who != null)
            {
                Logger?.Info("Social check (ring): " + who.Name + "'s call " + what + ": PASS");
            }
        }
    }
}
