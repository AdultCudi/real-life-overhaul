using System;
using BigAmbitions.DayNightCycle;
using BigAmbitions.InputSystem;
using PlayerActivity;
using UI.Elements;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A test's activity on the game's own panel (Options, Tests): a working day, a class at a desk, or a desk session's
    /// time in the chair, with the panel's every part (its lines, an hours slider, the time machine, fast forward, a bar,
    /// Start, Cancel and Stop) so the kit can be seen over each, live. Nothing is paid, learned or spent: its hours only
    /// pass. Game side. Cost: nothing a frame; the panel asks it as it asks any activity.
    /// </summary>
    internal sealed class TestActivity : IPlayerActivity
    {
        public enum Kind : byte { Work, Class, Chair }

        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "TestActivity", typeof(TestActivity));   // the game's calls of it (Core/Guard)

        private const int LeastHours = 1, MostHours = 8;

        private readonly Kind _kind;
        private PlayerActivityState _state = PlayerActivityState.NotStarted;
        private PlayerActivityState _before = PlayerActivityState.NotStarted;
        private int _hours = 6;
        private int _done;                // minutes passed in it
        private int _setOff = -1;         // the frame it set off in: the panel sees it on its way for a frame, then it starts
        private ButtonInfo[] _ready, _running;
        private LabelInfo[] _lines;

        public TestActivity(Kind kind)
        {
            _kind = kind;
            if (kind == Kind.Chair)
            {
                _hours = 2;
            }
        }

        /// <summary>Which test it is, for the kit's side card (ActivityFacts).</summary>
        public Kind What => _kind;

        /// <summary>Its hours as chosen, and the minutes passed.</summary>
        public int Hours => _hours;

        public int Done => _done;

        /// <summary>Straight to the time in the chair, as a desk session's panel goes once its choices are made.</summary>
        public void SetOff()
        {
            _state = PlayerActivityState.MovingTowardsActivity;
            _setOff = Time.frameCount;
        }

        public bool RequiresEnergy() => false;

        public PlayerActivityState GetState()
        {
            try
            {
                if (_state == PlayerActivityState.MovingTowardsActivity && _setOff >= 0 && Time.frameCount > _setOff + 1)
                {
                    _state = PlayerActivityState.Started;   // the panel saw it on its way; it starts, and the panel makes it Running
                }
                return _state;
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, Guard.Ended(this));
            }
        }

        public PlayerActivityState GetStateBeforeFinishing() => _before;

        public void ChangeState(PlayerActivityState state)
        {
            if (state == PlayerActivityState.Finished && _state != PlayerActivityState.Finished)
            {
                _before = _state;
            }
            _state = state;
        }

        public void Perform(int minutes)
        {
            try
            {
                _done += Math.Max(0, minutes);
                if (_done >= _hours * 60 && _state != PlayerActivityState.Finished)
                {
                    ChangeState(PlayerActivityState.Finished);
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public void Finish()
        {
            try
            {
                ChangeState(PlayerActivityState.Finished);
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public LabelInfo GetHeadlineLabel()
        {
            try
            {
                string key = _kind == Kind.Class ? "reallife_ui_test_activity_class_head"
                    : _kind == Kind.Chair ? "reallife_ui_test_activity_chair_head" : "reallife_ui_test_activity_work_head";
                return new LabelInfo(key);
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, Guard.Blank());
            }
        }

        public LabelInfo[] GetLabels()
        {
            try
            {
                return _lines ?? (_lines = new[] { new LabelInfo("reallife_ui_test_activity_line") });
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, new LabelInfo[0]);
            }
        }

        public ButtonInfo[] GetButtons()
        {
            try
            {
                if (_state == PlayerActivityState.NotStarted)
                {
                    return _ready ?? (_ready = new[]
                    {
                        new ButtonInfo("TestActivityCancel", "common_cancel", "gray", Guard.Way(Cancel), PlayerAction.Cancel),
                        new ButtonInfo("TestActivityStart", _kind == Kind.Work ? "workui_start" : "reallife_ui_test_activity_start", "blue", Guard.Way(SetOff), PlayerAction.Interact),
                    });
                }
                return _running ?? (_running = new[] { new ButtonInfo("TestActivityStop", "reallife_ui_test_activity_stop", "gray", Guard.Way(Finish), PlayerAction.Cancel) });
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, new ButtonInfo[0]);
            }
        }

        private void Cancel()
        {
            _before = PlayerActivityState.NotStarted;
            _state = PlayerActivityState.Finished;
        }

        public bool HasFastForward() => true;

        public bool HasTimeMachine() => true;

        public int GetRemainingMinutesForTimeMachine() => Math.Max(0, _hours * 60 - _done);

        public Timestamp GetTimeMachineGoal()
        {
            try
            {
                return TimeHelper.Now().AddMinutes(GetRemainingMinutesForTimeMachine());
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, TimeHelper.Now());
            }
        }

        public bool HasProgressBar() => _state == PlayerActivityState.Started || _state == PlayerActivityState.Running;

        public float GetProgressBarPercentageValue() => 100f * Mathf.Clamp01(_done / (_hours * 60f));

        public (string key, object arguments) GetProgressBarLabel()
        {
            try
            {
                int left = GetRemainingMinutesForTimeMachine();
                return ("playeractivityui_time_remaining", new { hours = left / 60, minutes = left % 60 });
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, (string.Empty, (object)null));
            }
        }

        public bool HasSlider() => _state == PlayerActivityState.NotStarted && _kind != Kind.Chair;

        public int GetMinSliderValue() => LeastHours;

        public int GetMaxSliderValue() => MostHours;

        public float GetCurrentSliderValue() => _hours;

        public void OnSliderValueChanged(int value)
        {
            try
            {
                _hours = Mathf.Clamp(value, LeastHours, MostHours);
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public (string key, object arguments) GetSliderInfo()
        {
            try
            {
                return ("common_hours", new { hours = _hours });
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, (string.Empty, (object)null));
            }
        }
    }
}
