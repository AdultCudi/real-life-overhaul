using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The outing's part for afterwards: what it was worth, the card that says so, a toast after drinks, and the goodbye.</summary>
    public sealed partial class OutingScene
    {
        private enum Toast { None, Closing, Turning, Raised, Done }

        private const float ToastGap = 1.05f;        // metres: `cheers` has two glasses meet bowl to bowl at this, and pass at 0.80
        private const float ToastSlack = 0.08f;      // metres

        private readonly StringBuilder _text = new StringBuilder(256);
        private bool _turned;
        private bool _wentWell;
        private bool _parted;
        private Toast _toast;
        private float _toastAt, _toastFrom;
        private bool _toastHeld;                     // the player is kept on the spot for the toast

        private void Wrap(float now, bool wentWell)
        {
            Prompt = null;
            Line = null;
            Unmark();
            Settle(wentWell);
            // Nobody turns round half way out of a car door.
            _wentWell = wentWell;
            _turned = false;
            _turnAt = Mathf.Max(now, _outUntil);
            Go(Step.Wrap, now, (_cups ? 9f : 5.5f) + (_turnAt - now));
        }

        private void Wrapping(float now)
        {
            if (!_turned && now >= _turnAt)
            {
                _turned = true;
                if (_friend != null && !_friend.Lost && !D.Cast.Seated(_friend))
                {
                    _friend.Stand();
                    _friend.Face(D.PlayerPosition);
                    D.Cast.Emoji(_friend, _wentWell ? CharacterEmojiName.CommonGreenHappy : CharacterEmojiName.CommonYellowNeutral, 3f);
                    if (!_cups && !RaiseToast(now) && ClipLibrary.Has("talk_a"))
                    {
                        _friend.Work("talk_a", D.Props, 0.4f);
                    }
                }
            }
            if (_toast != Toast.None && _toast != Toast.Done)
            {
                Toasting(now);
            }
            if (_cupBackAt > 0f && now >= _cupBackAt)
            {
                _cupBackAt = -1f;
                D.Body.Over("cup_hold", BodyMask.RightArm);   // the drink taken, the cup is held again
            }
            if (_cups && _sipAt > 0f && now >= _sipAt && _friend != null && !_friend.Lost)
            {
                _sipAt = -1f;
                if (ClipLibrary.Has("sip"))
                {
                    _friend.Work("sip", D.Props, 0.3f);
                    D.Body.Over("sip", BodyMask.RightArm);
                    _cupBackAt = now + 3.5f;
                }
            }
            if (now < _stepEnd)
            {
                return;
            }
            if (!_parted)
            {
                _parted = true;
                _stepEnd = now + 1.5f;
                D.Body.Rest();
                EndToast();
                if (_friend != null && !_friend.Lost)
                {
                    _friend.Idle(0.3f);
                    _friend.PutAway(D.Props);
                    Say(_who, SocialLines.Pick(_who.Together ? SocialCatalogue.PartingLove : SocialCatalogue.Parting));
                    Gesture(_friend, D.Props, ClipLibrary.Has("job_thank") ? "job_thank" : "wave_hello");
                }
                return;
            }
            Depart(_friend, now);
            if (_wentWell && _friend != null && !_friend.Lost && ClipLibrary.Has("goodbye_wave_walk"))
            {
                _friend.WorkOver("goodbye_wave_walk", D.Props, 0.25f, ClipEnd.Return, BodyMask.RightArm, false);
            }
            _friend = null;
            Title = null;
            Go(Step.Parting, now, 0f);
        }

        // After drinks that went well: `cheers` is made for two facing ToastGap apart. True if begun; it may come to nothing.
        private bool RaiseToast(float now)
        {
            if (_kind != PlanKind.Drinks || !_wentWell || _closed || !D.OnFoot || D.PlayerSpeed > 0.25f || !ClipLibrary.Has("cheers")
                || !ClipLibrary.TryGet("cheers", out CityClip cheers) || !cheers.HasTool || !PropLibrary.TryGet(cheers.Prop, out _, out _))
            {
                return false;
            }
            Vector3 player = D.PlayerPosition;
            float gap = JobSpots.Flat(_friend.Position, player);
            if (gap > HelloWithin + 1.5f)
            {
                return false;
            }
            D.Body.Hold();
            _toastHeld = true;
            D.Body.Turn(D, _friend.Position);
            _toast = Toast.Turning;
            _toastAt = now + 0.35f;
            if (Mathf.Abs(gap - ToastGap) > ToastSlack && _friend.StepTo(player, ToastGap))
            {
                _toast = Toast.Closing;
                _toastAt = now + 3.5f;
            }
            _toastFrom = now;
            _stepEnd = Mathf.Max(_stepEnd, now + 3.5f + 0.35f + cheers.Length + 1.6f);
            return true;
        }

        private void Toasting(float now)
        {
            if (_friend == null || _friend.Lost)
            {
                EndToast();
                return;
            }
            Vector3 player = D.PlayerPosition;
            switch (_toast)
            {
                case Toast.Closing:
                    if ((now - _toastFrom > 0.4f && _friend.Arrived && _friend.Speed < 0.05f) || now >= _toastAt)
                    {
                        _friend.Stand();
                        _friend.Face(player);
                        D.Body.Turn(D, _friend.Position);
                        _toast = Toast.Turning;
                        _toastAt = now + 0.35f;
                    }
                    break;

                case Toast.Turning:
                    if (now < _toastAt)
                    {
                        break;
                    }
                    // Both or neither; the glass stays in the hand until the goodbye.
                    if (Mathf.Abs(JobSpots.Flat(_friend.Position, player) - ToastGap) <= ToastSlack
                        && _friend.Work("cheers", D.Props, 0.25f, ClipEnd.Hold))
                    {
                        if (D.Body.Clip("cheers", 0.25f, ClipEnd.Hold))
                        {
                            _toast = Toast.Raised;
                            _stepEnd = Mathf.Max(_stepEnd, now + Length("cheers", 1.8f) + 1.6f);
                            break;
                        }
                        _friend.Idle(0.2f);
                        _friend.PutAway(D.Props);
                    }
                    EndToast();
                    if (ClipLibrary.Has("talk_a"))
                    {
                        _friend.Work("talk_a", D.Props, 0.4f);   // no toast after all: a word instead
                    }
                    break;
            }
        }

        // Safe at any time; their glass goes with PutAway at the goodbye.
        private void EndToast()
        {
            if (_toastHeld)
            {
                _toastHeld = false;
                D.Body.Rest(0.3f);
                D.Body.Free();
            }
            if (_toast != Toast.None)
            {
                _toast = Toast.Done;
            }
        }

        // Applied once, and shown on the card.
        private void Settle(bool wentWell)
        {
            if (_settled)
            {
                return;
            }
            _settled = true;
            float quality = wentWell ? 1f : 0.5f;
            if (_closed)
            {
                quality *= 0.9f;
            }
            float liking = SocialRules.Liking(_who, _kind);
            float familiarity = _def.Familiarity * quality;
            float affection = _def.Affection * liking * quality;
            float trust = _def.Trust * quality;
            int pay = Talk != null ? Talk.Pay : 0;
            if (pay == SceneTalk.Treat)
            {
                affection += 2f;
            }
            if (_covered)
            {
                trust -= 1f;
            }
            if (_crashes > 0)
            {
                trust -= 4f;
            }
            int gift = 0;
            if (wentWell && (_kind == PlanKind.Lift || _kind == PlanKind.Moving) && !_who.Has(SocialTrait.Careful))
            {
                gift = Prices.At(_kind == PlanKind.Lift ? 10 + Mathf.RoundToInt(_metres / 100f) : 25);
            }

            SocialStage before = _who.Stage;
            if (!_test)
            {
                SocialLife.Contacted(_who);
                _who.Shared++;
                _who.LastOutingDay = SocialLife.Day;
                if (_who.Together)
                {
                    _who.Dates++;
                }
                SocialLife.Remember(_who, _def.Memory);
                _who.Wants = (PlanKind)(((int)_who.Wants + 1 + Random.Range(0, 3)) % ((int)PlanKind.Walk + 1));
                _who.WantsDay = SocialLife.Day;
                SocialLife.Change(_who, familiarity, affection, trust);
                // Kept for a text the day after about how it went (SocialFollowRules.NextDay, in the save).
                _who.AfterDay = SocialLife.Day;
                _who.AfterKind = _kind;
                _who.AfterWell = wentWell;
                _who.AfterPlace = _venue.Valid ? _venue.Name : null;
            }
            if (gift > 0)
            {
                SocialLife.Give(gift, _def.Transaction);
            }
            if (_def.Energy > 0f)
            {
                EnergyHelper.GenerateEnergy(_def.Energy);
            }
            else if (_def.Energy < 0f && SaveGameManager.Current != null && SaveGameManager.Current.Energy > 14f - _def.Energy)
            {
                EnergyHelper.SpentEnergyOnce(-_def.Energy);   // tiring, never dangerous
            }
            ModState.AddModifier(wentWell ? (_who.Together || liking > 1.2f ? ModState.MoodUpBig : ModState.Social) : ModState.MoodDown);
            if (SocialLinks.Wellbeing != null)
            {
                SocialLinks.Wellbeing.Stress(wentWell ? -12f : 4f);
            }
            Feed();

            string after = wentWell ? SocialLines.Pick(_def.After) : SocialLines.Pick(SocialCatalogue.WentBadly);
            Dictionary<string, string> data = SocialLife.Data(_who, "place", _venue.Name);
            data["to"] = _to.Valid ? _to.Name : string.Empty;
            Say(_who, after, data, wentWell ? NotificationType.Success : NotificationType.Info);
            if (wentWell && !_test && _kind <= PlanKind.Business)
            {
                D.ThankLater(_who, _venue.Name, _kind);
            }

            _text.Length = 0;
            if (_paid > 0)
            {
                CardLine("reallife_social_card_cost", "<color=#FF8A70>-" + SocialLife.Money(_paid) + "</color>");
            }
            if (pay == SceneTalk.Theirs || _covered)
            {
                CardLine("reallife_social_card_cost", SocialLife.T("reallife_social_card_they_paid"));
            }
            if (gift > 0)
            {
                CardLine("reallife_social_card_thanks", "<color=#7CE08A>+" + SocialLife.Money(gift) + "</color>");
            }
            if (_def.Energy != 0f)
            {
                CardLine("reallife_social_card_energy", Signed(_def.Energy));
            }
            CardLine("reallife_social_card_mood", SocialLife.T(wentWell ? "reallife_social_card_mood_up" : "reallife_social_card_mood_down"));
            if (!_test)
            {
                CardLine("reallife_social_card_familiarity", Signed(familiarity));
                CardLine("reallife_social_card_affection", Signed(affection));
                CardLine("reallife_social_card_trust", Signed(trust));
                if (_lateBy > SocialRules.LateMinutes && !_who.Has(SocialTrait.Late))
                {
                    _text.Append(SocialLife.T("reallife_social_card_late")).Append('\n');
                }
                _text.Append('\n').Append(SocialLife.T(_who.Stage != before ? "reallife_social_card_now" : "reallife_social_card_still")).Append("   <b>")
                    .Append(SocialLife.StageName(_who)).Append("</b>");
            }
            else
            {
                _text.Append('\n').Append(SocialLife.T("reallife_social_card_test"));
            }
            Dictionary<string, string> title = SocialLife.Data(_who, "outing", SocialLife.T(_def.Name));
            D.ShowCard(SocialLife.Capital(SocialLife.T("reallife_social_title", title)), _text.ToString());
            SocialDirector.Logger?.Info("Social: " + _def.Kind + " with " + _who.Name + (wentWell ? " went well" : " went badly") + (_closed ? " (the place was shut: a walk)" : string.Empty)
                                        + ": paid " + _paid + ", familiarity " + familiarity.ToString("F1", CultureInfo.InvariantCulture) + ", affection "
                                        + affection.ToString("F1", CultureInfo.InvariantCulture) + ", trust "
                                        + trust.ToString("F1", CultureInfo.InvariantCulture) + (_test ? " (test: nothing recorded)." : "."));
        }

        private void CardLine(string key, string value)
        {
            _text.Append(SocialLife.T(key)).Append("   ").Append(value).Append('\n');
        }

        private static string Signed(float value)
        {
            int rounded = Mathf.RoundToInt(value);
            return rounded >= 0 ? "<color=#7CE08A>+" + rounded + "</color>" : "<color=#FF8A70>" + rounded + "</color>";
        }
    }
}
