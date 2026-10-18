using System;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The social director's part for what is shown: the plan's card, the prompt's panel and the destination marker.
    /// The panel over the person the player came to meet has the game's box first of all within talking range (the
    /// panels' rule, Core/GameUi/PanelPickRules). Should the box still not be had beside them for a few seconds (the
    /// game's own panel at a lift, the building box leaving no room), the hello is asked in the game's own question
    /// box instead, once, so an outing never stalls before hello.
    /// </summary>
    public sealed partial class SocialDirector
    {
        private const float CardSeconds = 11f;   // how long an outing's card stays up
        private const string ButtonAnswer = "reallife_social_button_answer";
        private const string ButtonNotNow = "reallife_social_meet_reply_brush";
        private const float AskAfter = 4f;       // real seconds beside the person without the panel before the question box asks instead

        private float _heldSince = -1f;   // real time the panel over the person was first refused while the player stood beside them; -1 none
        private string _askedFor;         // the prompt the question box was shown for: asked once for each
        private Action _onHelloAsked;

        private ShiftCard _plan;   // a copy of the game's job panel for the plan ("Coffee with Mom, 15:00 at ...")
        private bool _planTried;
        private string _planToldLine;
        private Vector3 _guidedAt;
        private string _guidedLabel;
        private bool _markSaid;

        /// <summary>What an outing cost and did, as one of the game's notifications.</summary>
        public void ShowCard(string title, string body)
        {
            SocialLife.Toast(NotificationType.Info, "reallife_social_card_toast",
                new Dictionary<string, string>(2) { { "title", title ?? string.Empty }, { "body", body ?? string.Empty } }, CardSeconds);
        }

        // Each panel is told only what changed.
        private void Draw()
        {
            if (_scene == null && !_ringing)
            {
                HideUi();
                return;
            }
            if (ThirdPersonCamera.IsUiOpen())
            {
                HideUi();
                return;
            }
            if (_scene != null && (_scene.Title != null || _scene.Line != null))
            {
                ShowPlan();
            }
            else
            {
                _plan?.Show(false);
            }
            string prompt = _ringing ? _ringPrompt : _scene != null && _held == SceneBreak.None ? _scene.Prompt : null;
            PlayerController player = PlayerHelper.PlayerController;
            if (prompt == null || player == null || SocialPhone.DialogOpen)
            {
                _door.Hide("nothing to offer here now");
                return;
            }
            WorldPanel panel = _door.Panel;
            panel.Lines(prompt, null, null, WorldPanel.White());
            panel.Button(0, _ringing ? ButtonAnswer : _scene.PromptButton, _onPrompt, true, true);
            panel.Button(1, null, null, false, false);
            panel.Button(2, null, null, false, false);
            panel.Button(3, null, null, false, false);
            Transform over = _ringing ? null : _scene.PromptOver;
            panel.Follow = over;
            panel.FollowLift = 2.1f;
            panel.Meet = over != null;   // over the person met: nearest of all within talking range
            Vector3 at = player.transform.position;
            bool up = _door.Show(at, at, 2.1f, _doorWhat);
            AskIfHeld(up, prompt, over, at);
        }

        // The panel over the person was not had for AskAfter seconds while the player stood beside them: the game's question box asks instead.
        private void AskIfHeld(bool up, string prompt, Transform over, Vector3 player)
        {
            OutingScene outing = _ringing ? null : _scene as OutingScene;
            if (up || over == null || outing == null || outing.Greeted || JobSpots.Flat(over.position, player) > PanelPickRules.TalkRange)
            {
                _heldSince = -1f;
                return;
            }
            float now = Time.unscaledTime;
            if (_heldSince < 0f)
            {
                _heldSince = now;
                return;
            }
            if (now - _heldSince < AskAfter || ReferenceEquals(prompt, _askedFor) || HudConfirm.isOpen)
            {
                return;
            }
            _askedFor = prompt;
            if (_onHelloAsked == null)
            {
                _onHelloAsked = HelloAsked;
            }
            GameConfirm.Show(prompt, SocialLife.T("reallife_social_hello_ask", SocialLife.Data(outing.Who)), _onHelloAsked, null, _scene.PromptButton, ButtonNotNow);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Social check (hello asked): the panel over " + outing.Who.Name + " was not had for " + (now - _heldSince).ToString("F0")
                             + " s while the player stood " + JobSpots.Flat(over.position, player).ToString("F1", CultureInfo.InvariantCulture)
                             + " m from them; the hello is asked in the game's question box: PASS.");
            }
        }

        // "Say hello" in the question box: as the panel's button, if that hello is still the one on offer.
        private void HelloAsked()
        {
            if (_scene != null && ReferenceEquals(_scene.Prompt, _askedFor))
            {
                PromptPressed();
            }
        }

        private string DoorWhat()
        {
            return _ringing ? "the phone is ringing" : _scene != null ? _scene.GetType().Name : string.Empty;
        }

        private void PromptPressed()
        {
            if (_broken || UiOpen())
            {
                return;
            }
            try
            {
                if (_ringing)
                {
                    AnswerRing();
                }
                else if (_scene != null && _held == SceneBreak.None && _scene.Prompt != null)
                {
                    _keyFrame = Time.frameCount;
                    _scene.Interact(Time.time);
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void ShowPlan()
        {
            if (_plan != null && !_plan.Alive)
            {
                _plan = null;
                _planTried = false;
            }
            if (_plan == null && !_planTried)
            {
                _planTried = true;
                _plan = ShiftCard.Make(Logger);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Social check (plan card): " + (_plan != null ? "a copy of the game's job panel is ready: PASS"
                        : "the game's job panel could not be copied; the plan is sent as a notification each time it changes: FAIL") + ".");
                }
            }
            if (_plan == null)
            {
                if (_scene.Line != null && !ReferenceEquals(_scene.Line, _planToldLine))
                {
                    _planToldLine = _scene.Line;
                    SocialLife.Toast(NotificationType.Info, "reallife_social_text",
                        new Dictionary<string, string>(1) { { "text", (_scene.Title != null ? _scene.Title + ": " : string.Empty) + _scene.Line } }, 8f);
                }
                return;
            }
            Color32 white = WorldPanel.White();
            _plan.Title(_scene.Title);
            _plan.Line(0, _scene.Line, white);
            _plan.Line(1, _scene.Status, white);
            _plan.Show(true);
        }

        private void HideUi()
        {
            _plan?.Show(false);
            _door?.Hide("nothing is going on");
        }

        // The game's own destination marker, also on the city map; moved only when the place moves by more than a metre.
        private void Guide()
        {
            if (_scene == null)
            {
                return;
            }
            if (!_scene.HasMarker || (SaveGameManager.Current != null && SaveGameManager.Current.currentPlayerMission != null))
            {
                Unguide();
                return;
            }
            Vector3 at = _scene.MarkerAt;
            if (_mark.Showing && (at - _guidedAt).sqrMagnitude <= 1f && ReferenceEquals(_scene.MarkerLabel, _guidedLabel))
            {
                return;
            }
            if (_mark.Show(at, _scene.MarkerLabel))
            {
                _guidedAt = at;
                _guidedLabel = _scene.MarkerLabel;
                if (Meta.Dev.Verbose && !_markSaid)
                {
                    _markSaid = true;
                    Logger?.Info("Social check (marker): the game's destination marker is on " + _scene.MarkerLabel + ": PASS.");
                }
            }
        }

        private void Unguide()
        {
            _mark.Hide();
            _guidedLabel = null;
        }

        private static bool UiOpen()
        {
            return ThirdPersonCamera.IsUiOpen() || SocialPhone.DialogOpen;
        }
    }
}
