using System;
using System.Collections.Generic;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The social director's part for what is shown: the plan's card, the prompt's panel and the destination marker.</summary>
    public sealed partial class SocialDirector
    {
        private const float CardSeconds = 11f;   // how long an outing's card stays up
        private const string ButtonAnswer = "reallife_social_button_answer";

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
            Vector3 at = player.transform.position;
            _door.Show(at, at, 2.1f, _doorWhat);
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
