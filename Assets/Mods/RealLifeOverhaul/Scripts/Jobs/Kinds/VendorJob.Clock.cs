using System.Collections.Generic;
using System.Globalization;
using Characters.EmojiSystem;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Elements;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The drinks cart: the rest of the shift left to the game's clock.</summary>
    public sealed partial class VendorJob
    {
        private ShiftSkip _skip;
        private int _skipLeft;        // customers the clock had to serve when it started
        private int _skipDone;        // ...and how many of them it has, so far
        private int _skipHours;       // the hours the slider keeps before the clock starts; 0: all the customers still to come
        private bool _skipRunning;    // the clock is on: its length is fixed

        private void OfferSkip()
        {
            if (_skip != null || !Ctx.Stand.Ready || LeftToServe <= 0 || Time.time - _orderOver < Debounce)
            {
                return;
            }
            _skip = ShiftSkip.Offer(Ctx.Director, this);
            if (_skip != null)
            {
                HidePanel();   // the game's activity panel has the keys while it is up
            }
        }

        string ISkippableShift.SkipTitle => Ctx.Shift != null
            ? JobText.T("reallife_career_shift_title", "title", JobText.T(Ctx.Shift.Rank.TitleKey), "employer", JobText.T(Ctx.Shift.Employer.NameKey)) : _title;

        int ISkippableShift.SkipMinutes => (_skipRunning ? _skipLeft : ShiftFactsRules.Units(_skipHours, LeftToServe, _minutesEach)) * _minutesEach;

        // The clock's length is the player's to pick before it starts: from one hour to the customers still to come. It can stop part way,
        // since the customers it has served by then are paid and the rest are left to serve by hand (SkipEnded).
        int ISlidingShift.SlideLeast => 1;

        int ISlidingShift.SlideMost => ShiftFactsRules.MostHours(LeftToServe * _minutesEach);

        int ISlidingShift.SlideNow => _skipHours > 0 ? Mathf.Min(_skipHours, ((ISlidingShift)this).SlideMost) : ((ISlidingShift)this).SlideMost;

        void ISlidingShift.Slide(int value) => _skipHours = ShiftFactsRules.Chosen(value, ((ISlidingShift)this).SlideMost);

        (string key, object arguments) ISlidingShift.SlideInfo() => ("common_hours", new { hours = ((ISlidingShift)this).SlideNow });

        int IShiftFacts.PayFor(int minutes)
        {
            int left = _skipRunning ? _skipLeft : ShiftFactsRules.Units(_skipHours, LeftToServe, _minutesEach);
            return ShiftFactsRules.Pay(PayEach, Mathf.Min(left, minutes / _minutesEach), ShiftRates.Share);
        }

        string IShiftFacts.SkillKey => ShiftFactsRules.SkillKey(_def, null);

        float IShiftFacts.SkillFor(int minutes) => ShiftFactsRules.Skill(_def.UnitSkill, minutes / _minutesEach);

        float IShiftFacts.StressRate => HomeBalance.StressJobShift;

        LabelInfo[] ISkippableShift.SkipLabels()
        {
            int left = LeftToServe;
            int minutes = left * _minutesEach;
            Color32 white = WorldPanel.White();
            var time = new LabelInfo("reallife_job_skip_info_time", new { count = left, hours = minutes / 60, minutes = minutes % 60 }, white);
            var pay = new LabelInfo("reallife_job_skip_info_pay", new
            {
                share = Mathf.RoundToInt(ShiftRates.Share * 100f), amount = JobText.Money(Mathf.RoundToInt(left * PayEach * ShiftRates.Share))
            }, white);
            return Ctx.Shift != null
                ? new[] { time, pay, new LabelInfo(CareerCatalogue.SkipWageKey(Ctx.Shift.Rank), null, white) }
                : new[] { time, pay };
        }

        void ISkippableShift.SkipStarted()
        {
            // The street is cleared: whoever was coming or at the counter goes back among those still to come.
            ClearCounter();
            ReleaseGuests();
            _front = -1;
            _came = _served + _lost;
            ReleaseLookers();
            _skipLeft = ShiftFactsRules.Units(_skipHours, LeftToServe, _minutesEach);
            _skipDone = 0;
            _skipRunning = true;
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: vendor: the rest of the shift is left to the clock: " + _skipLeft + " customers, " + (_skipLeft * _minutesEach) + " game minutes, at "
                                 + Mathf.RoundToInt(ShiftRates.Share * 100f) + "% of " + PayEach.ToString("F2", CultureInfo.InvariantCulture)
                                 + " a customer; the game's activity panel shows its progress.");
            }
        }

        void ISkippableShift.SkipWorked(int minutes)
        {
            _skipDone = Mathf.Min(_skipLeft, minutes / _minutesEach);
        }

        void ISkippableShift.SkipEnded(int minutes, bool whole)
        {
            if (_ending)
            {
                return;
            }
            int count = whole ? _skipLeft : Mathf.Min(_skipLeft, minutes / _minutesEach);
            _skipDone = 0;
            _skipLeft = 0;
            _skipRunning = false;
            _nextArrival = Time.time + 2f;
            _panelState = int.MinValue;
            float energyBefore = SaveGameManager.Current != null ? SaveGameManager.Current.Energy : 0f;
            int pay = 0;
            if (count > 0)
            {
                pay = ShiftFactsRules.Pay(PayEach, count, ShiftRates.Share);
                _served += count;
                _came = _served + _lost;
                _clockPay += pay;
                Ctx.Pay(this, new PaySlip { Units = count, Base = pay }, "reallife_job_vendor_slip");
            }
            float energyAfter = SaveGameManager.Current != null ? SaveGameManager.Current.Energy : 0f;
            Ctx.Logger?.Info("Jobs: vendor: shift skipped by the clock: " + (minutes / 60f).ToString("F1", CultureInfo.InvariantCulture) + " game hours (" + minutes + " min), " + count
                             + " customers at "
                             + Mathf.RoundToInt(ShiftRates.Share * 100f) + "%: pay " + pay + ", no tips, no bonus; energy " + energyBefore.ToString("F1", CultureInfo.InvariantCulture) + " -> "
                             + energyAfter.ToString("F1", CultureInfo.InvariantCulture)
                             + "; " + (whole ? "to the end of the shift" : "stopped early: " + LeftToServe + " customers are left to serve by hand") + ".");
        }
    }
}
