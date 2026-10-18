using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The social director's part for the plan that is on, and the phone ringing.</summary>
    public sealed partial class SocialDirector
    {
        private const float RingSeconds = 22f;   // real seconds the phone rings

        private bool _ringing;
        private SocialPerson _ringPerson;
        private float _ringUntil;
        private string _ringPrompt;

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
                && !SocialPlaces.Find(plan.Kind, new Vector3(plan.X, plan.Y, plan.Z), 0f, 250f, out venue))
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
            Dictionary<string, string> data = SocialLife.Data(person, "key", Keys.Label(KeyAction.Interact));
            _ringPrompt = SocialLife.Say("reallife_social_ring_prompt", data);
            SocialLife.Toast(NotificationType.Info, "reallife_social_ringing", data, 6f);
            SocialState.LastCallInDay = SocialLife.Day;
            Logger?.Info("Social: " + person.Name + " is ringing the player.");
            return true;
        }

        private void Ring(float real)
        {
            if (_ringPerson == null || real > _ringUntil || !Settings.Social)
            {
                // Not answered: a text instead, and what they wanted still stands.
                _ringing = false;
                if (_ringPerson != null && Settings.Social)
                {
                    SocialLife.SendText(_ringPerson, SocialLines.Pick(SocialCatalogue.TextMissedCall), SocialLife.Data(_ringPerson));
                }
                _ringPerson = null;
            }
        }

        private void AnswerRing()
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
            if (!SocialPhone.Call(caller))
            {
                SocialPhone.Incoming = -1;
            }
        }
    }
}
