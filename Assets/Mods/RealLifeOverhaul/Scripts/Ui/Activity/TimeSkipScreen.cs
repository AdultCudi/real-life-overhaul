using System;
using BigAmbitions.DayNightCycle;
using Timemachine;
using TMPro;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's time skip (TimeMachine) on the kit's veil: the world dimmed with no blur, the clock large inside a ring
    /// that fills from where the skip set out to its goal, the day, the goal in words (the game's own line when it gave one:
    /// "You're being taken to the closest medical center"), what is left, and Cancel on Escape when the game allows one.
    /// The game's machine runs underneath, its clock and its end its own; Cancel is its own CancelTimeMachine. Looks ten
    /// times a second while it runs. Game side. Cost: a few reads a look; a layout when the clock's minute moved.
    /// </summary>
    public sealed class TimeSkipScreen : IUiScreen
    {
        private const float Every = 0.1f;   // seconds between looks: the clock runs fast, a layout a tenth of a second keeps it smooth

        private readonly TimeMachine _machine;
        private readonly UiTimeSkipModel _m = new UiTimeSkipModel();
        private bool _dirty = true;
        private float _next;
        private int _seen;
        private Timestamp _blurFor;   // the run the game's blur was taken off for: each new run turns it on again

        public TimeSkipScreen(TimeMachine machine)
        {
            _machine = machine;
        }

        public string Id => "time_skip";

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    int now = Signature();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        private int Signature()
        {
            unchecked
            {
                int sig = (int)TimeHelper.NowInMinutes();
                sig = sig * 31 + (_machine != null && _machine.cancelButton != null && _machine.cancelButton.gameObject.activeSelf ? 1 : 0);
                sig = sig * 31 + (_machine != null && _machine.infoLabel != null && _machine.infoLabel.gameObject.activeSelf ? 1 : 0);
                Timestamp goal = Goal();
                return goal != null ? sig * 31 + goal.Day * 1440 + goal.Hour * 60 + (int)goal.Minute : sig;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            _dirty = false;
            Read();
            UiTimeSkip.Lay(s, _m, frame, dark, keys);
        }

        private Timestamp Goal() => GameContract.SkipGoal.Field != null && _machine != null ? GameContract.SkipGoal.Field.GetValue(_machine) as Timestamp : null;

        private void Read()
        {
            Timestamp goal = Goal();
            if (goal != null && !ReferenceEquals(goal, _blurFor))
            {
                _blurFor = goal;
                GameLook.Clear();   // the veil dims the world itself: the game's own blur stays off under the kit
            }
            float distance = GameContract.SkipDistance.Field != null && GameContract.SkipDistance.Field.GetValue(_machine) is float d ? d : 0f;
            float left = goal != null ? Math.Max(0f, goal.GetMinutesFromNow()) : 0f;
            UiStatusModel bar = UiChrome.Bar != null ? UiChrome.Bar() : null;
            _m.Clock = TimeHelper.GetCurrentFormattedTime();
            _m.Day = bar != null ? bar.Day : null;
            _m.Share = distance > 0f ? Mathf.Clamp01(1f - left / distance) : 0f;
            _m.Title = UiText.T("playeractivityui_timemachine");
            string info = _machine.infoLabel != null && _machine.infoLabel.gameObject.activeSelf ? UiWidgets.Text(_machine.infoLabel.GetComponent<TMP_Text>()) : null;
            string when = goal != null ? When(goal) : null;
            string rest = Left(left);
            if (!string.IsNullOrEmpty(info))
            {
                // The game's own line says why time passes; the goal and what is left go under it.
                _m.Goal = info;
                _m.GoalTime = null;
                _m.Pace = when != null ? UiText.Fill("reallife_ui_skip_pace_until", "speed", UiText.Whole(Speed(distance, left)), "time", when, "left", rest) : null;
            }
            else
            {
                bool working = PlayerActivity.PlayerActivityUI.IsPanelOpen;
                _m.Goal = UiText.T(working ? "reallife_ui_skip_to_end" : "reallife_ui_skip_until");
                _m.GoalTime = when;
                _m.Pace = goal != null ? UiText.Fill("reallife_ui_skip_pace", "speed", UiText.Whole(Speed(distance, left)), "left", rest) : null;
            }
            _m.CanCancel = _machine.cancelButton != null && _machine.cancelButton.gameObject.activeSelf;
            _m.CancelLabel = UiText.T("common_cancel");
            _m.Prompts = _m.CanCancel ? new[] { new UiPrompt { Action = UiAction.Back, Label = UiText.T("common_cancel") } } : new UiPrompt[0];
            _m.PromptCount = _m.Prompts.Length;
        }

        // The pace the machine keeps now, as the game works it out (a constant pace is the whole distance a second, else the curve at how far along).
        private int Speed(float distance, float left)
        {
            bool constant = GameContract.SkipConstant.Field != null && GameContract.SkipConstant.Field.GetValue(_machine) is bool c && c;
            float curve = !constant && _machine.timeSpeedCurve != null ? _machine.timeSpeedCurve.Evaluate(UiTimeSkip.Along(distance, left)) : 0f;
            return UiTimeSkip.Speed(distance, constant, curve);
        }

        // The goal's time, and its day when it is not today: "07:00", or "Day 413, 07:00".
        private static string When(Timestamp goal)
        {
            string time = goal.GetFormattedTime();
            int today = SaveGameManager.Current != null ? SaveGameManager.Current.Day : goal.Day;
            return goal.Day == today ? time : UiText.Fill("reallife_ui_skip_goal_day", "day", UiText.Whole(goal.Day), "time", time);
        }

        // What is left in hours and minutes: "2 h 40 min", "40 min".
        private static string Left(float minutes)
        {
            int all = Mathf.CeilToInt(minutes);
            int hours = all / 60, rest = all % 60;
            return hours > 0 ? UiText.Fill("reallife_ui_skip_hours_minutes", "hours", UiText.Whole(hours), "minutes", UiText.Whole(rest))
                : UiText.Fill("reallife_ui_skip_minutes", "minutes", UiText.Whole(rest));
        }

        public bool Press(int command, int arg)
        {
            if (command != UiTimeSkip.CancelCommand || _machine == null || _machine.cancelButton == null || !_machine.cancelButton.gameObject.activeSelf)
            {
                return false;
            }
            _machine.CancelTimeMachine();   // as the game's own Cancel: the machine stops where it is
            _dirty = true;
            return true;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => false;   // with no Cancel the game's own Escape does nothing either

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }
    }
}
