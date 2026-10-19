using System;
using System.Globalization;
using RealLifeOverhaul.GameUi;
using UI.Elements;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What a job tells its <see cref="ShiftClock"/> about the units of its shift.</summary>
    public interface IClockedShift
    {
        int ClockLeft { get; }

        /// <summary>What one unit pays when it is done by hand, in whole money, before <see cref="ShiftRates.Share"/>.</summary>
        float ClockPay { get; }

        /// <summary>The clock starts: whatever stands on the street for the hand-played shift is put away.</summary>
        void ClockStarted();

        /// <summary>The clock is over: <paramref name="count"/> units done for <paramref name="pay"/>, in <paramref name="away"/> real seconds.</summary>
        void ClockEnded(int count, int pay, float away);
    }

    /// <summary>The rest of a shift left to the game's clock (<see cref="ShiftSkip"/>), for every job but the cart, which does it inline.</summary>
    public sealed class ShiftClock : ISkippableShift
    {
        private readonly JobContext _ctx;
        private readonly Job _job;
        private readonly IClockedShift _shift;
        private readonly string _name;
        private readonly string _unitKey;
        private readonly string _unitsKey;
        private readonly string _slipKey;
        private readonly bool _asTips;
        private readonly Func<bool> _canWork;
        private readonly Func<int> _openFor;
        private readonly int _each;
        private ShiftSkip _skip;
        private int _left;
        private int _done;
        private int _open;   // game minutes the place of work stays open from the run's start
        private float _startedAt;
        private bool _closed;

        /// <param name="name">What the log calls the job ("flyers").</param>
        /// <param name="asTips">The clock's pay goes on the slip as tips (a busker lives on the hat), not as base pay.</param>
        /// <param name="canWork">Asked while the clock runs: false once the place of work has closed (the job then calls the work off).</param>
        /// <param name="openFor">Game minutes the place of work stays open from now; no unit finished after that counts. None: no closing.</param>
        public ShiftClock(JobContext ctx, Job job, IClockedShift shift, string name, string unitKey, string unitsKey, string slipKey, bool asTips = false,
            Func<bool> canWork = null, Func<int> openFor = null)
        {
            _ctx = ctx;
            _job = job;
            _shift = shift;
            _name = name;
            _unitKey = unitKey;
            _unitsKey = unitsKey;
            _slipKey = slipKey;
            _asTips = asTips;
            _canWork = canWork;
            _openFor = openFor;
            _each = ShiftRates.Minutes(job.Def);
        }

        public bool Working => _skip != null && _skip.Working;

        /// <summary>True from the offer until it is over.</summary>
        public bool Offered => _skip != null;

        /// <summary>Units the clock has done so far in this run (for the card).</summary>
        public int Done => Working ? _done : 0;

        public int MinutesEach => _each;

        /// <summary>Shows the offer on the game's panel. False if there is nothing left to do or the panel is busy.</summary>
        public bool Offer()
        {
            if (_closed || _skip != null || _shift.ClockLeft <= 0)
            {
                return false;
            }
            _skip = ShiftSkip.Offer(_ctx.Director, this);
            return _skip != null;
        }

        /// <summary>First on the job's tick: true while the offer or the clock is on the game's panel (the job then does only its card).</summary>
        public bool Hold()
        {
            if (_skip == null)
            {
                return false;
            }
            if (_skip.Open && _skip.Shown)
            {
                return true;
            }
            _skip.Close();   // over, or dropped by the game's panel: it settles nothing more
            _skip = null;
            return false;
        }

        /// <summary>What a run of the clock still going has done and not been paid (it is paid when the run ends). Changes nothing.</summary>
        public bool Owed(out PaySlip slip)
        {
            slip = default;
            int count = Done;
            if (_closed || count <= 0)
            {
                return false;
            }
            int pay = Mathf.RoundToInt(count * _shift.ClockPay * ShiftRates.Share);
            slip = _asTips ? new PaySlip { Units = count, Tips = pay } : new PaySlip { Units = count, Base = pay };
            return true;
        }

        /// <summary>The job is stopping: the game's panel goes and nothing more is settled through it.</summary>
        public void Close()
        {
            _closed = true;
            if (_skip != null)
            {
                _skip.Close();
                _skip = null;
            }
        }

        /// <summary>Calls the work off, paying the clock's completed units once before cancelling the activity and its time skip.</summary>
        public void Stop()
        {
            if (!_closed && Working)
            {
                ((ISkippableShift)this).SkipEnded(_done * _each, false);
            }
            Close();
        }

        string ISkippableShift.SkipTitle => _ctx.Shift != null
            ? JobText.T("reallife_career_shift_title", "title", JobText.T(_ctx.Shift.Rank.TitleKey), "employer", JobText.T(_ctx.Shift.Employer.NameKey))
            : JobText.T(_job.Def.NameKey);

        int ISkippableShift.SkipMinutes => _shift.ClockLeft * _each;

        LabelInfo[] ISkippableShift.SkipLabels()
        {
            int left = _shift.ClockLeft;
            int minutes = left * _each;
            Color32 white = WorldPanel.White();
            string things = JobText.T(_unitsKey);
            string thing = JobText.T(_unitKey);
            string amount = JobText.Money(Mathf.RoundToInt(left * _shift.ClockPay * ShiftRates.Share));
            var time = new LabelInfo(JobText.T("reallife_job_skip_info_units", "count", left.ToString(), "things", things)
                .Replace("{hours}", (minutes / 60).ToString()).Replace("{minutes}", (minutes % 60).ToString()), white, false);
            var pay = new LabelInfo(JobText.T(_asTips ? "reallife_job_skip_info_unit_tips" : "reallife_job_skip_info_unit_pay", "share", ShiftRates.Percent.ToString(), "thing", thing)
                .Replace("{amount}", amount), white, false);
            return _ctx.Shift != null
                ? new[] { time, pay, new LabelInfo("reallife_job_skip_info_wage", null, white) }
                : new[] { time, pay };
        }

        void ISkippableShift.SkipStarted()
        {
            _left = _shift.ClockLeft;
            _done = 0;
            _open = _openFor != null ? _openFor() : int.MaxValue;
            _startedAt = Time.time;
            if (!_ctx.Stand.Standing)
            {
                _ctx.Self.Hold();   // the player stays put while the clock runs: the game's blocker, no walk order
            }
            _shift.ClockStarted();
            if (Meta.Dev.Verbose)
            {
                _ctx.Logger?.Info("Jobs: " + _name + ": the rest of the shift is left to the clock: " + _left + " " + JobText.T(_unitsKey) + ", " + (_left * _each) + " game minutes, at "
                                  + ShiftRates.Percent + "% of " + _shift.ClockPay.ToString("F2", CultureInfo.InvariantCulture) + " a " + JobText.T(_unitKey)
                                  + "; the game's activity panel shows its progress.");
            }
        }

        void ISkippableShift.SkipWorked(int minutes)
        {
            // Counted before the client is asked: the game's time machine reports its minutes once, at the end, so a client
            // found shut then is still paid for the units done before it closed, and none after.
            _done = ClockRules.Units(minutes, false, _left, _each, _open);
            _canWork?.Invoke();   // a client can close during a time skip, before the job's next street tick: the job then calls it off
        }

        void ISkippableShift.SkipEnded(int minutes, bool whole)
        {
            if (_closed)
            {
                return;
            }
            int count = ClockRules.Units(minutes, whole, _left, _each, _open);
            _done = 0;
            if (!_ctx.Stand.Standing)
            {
                _ctx.Self.Free();
            }
            float energyBefore = SaveGameManager.Current != null ? SaveGameManager.Current.Energy : 0f;
            int pay = count > 0 ? Mathf.RoundToInt(count * _shift.ClockPay * ShiftRates.Share) : 0;
            _shift.ClockEnded(count, pay, Time.time - _startedAt);
            if (count > 0)
            {
                _ctx.Pay(_job, _asTips ? new PaySlip { Units = count, Tips = pay } : new PaySlip { Units = count, Base = pay }, _slipKey);
            }
            float energyAfter = SaveGameManager.Current != null ? SaveGameManager.Current.Energy : 0f;
            _ctx.Logger?.Info("Jobs: " + _name + ": shift skipped by the clock: " + (minutes / 60f).ToString("F1", CultureInfo.InvariantCulture) + " game hours (" + minutes + " min), " + count + " "
                              + JobText.T(_unitsKey) + " at " + ShiftRates.Percent + "%: pay " + pay + ", no tips, no bonus; energy " + energyBefore.ToString("F1", CultureInfo.InvariantCulture)
                              + " -> "
                              + energyAfter.ToString("F1", CultureInfo.InvariantCulture) + "; "
                              + (count >= _left ? "to the end of the shift" : (minutes > _open ? "the client closed " + _open + " min in: " : "stopped early: ")
                                 + _shift.ClockLeft + " are left to do by hand") + ".");
        }
    }
}
