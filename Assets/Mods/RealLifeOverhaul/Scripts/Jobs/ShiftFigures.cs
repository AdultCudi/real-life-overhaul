using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A job's use of the shift card (<see cref="ShiftCard"/>, the copy of the game's job panel at the top right), laid out
    /// as the drinks cart lays it out: the title and the employer (or "Gig Line") are written once; line 1 is the objective
    /// in words; line 2 the units done of the shift's total and, after a comma, what can end the shift early; line 3
    /// what has been earned and the tips; line 4 the time on shift and about how long is left.
    ///
    /// Every line is made only when one of its figures changes: each method compares the numbers it is given with the
    /// ones it wrote last and builds nothing when they are the same, so the job may call them on every tick. One of these
    /// per shift, made in the job's OnBegin; if the game's panel could not be copied every call does nothing.
    /// </summary>
    public sealed class ShiftFigures
    {
        private readonly ShiftCard _card;
        private readonly string _doneKey;
        private readonly float _startMinutes;
        private readonly Color32 _white = WorldPanel.White();
        private string _units = string.Empty;
        private string _limit;
        private Color32 _limitColour;
        private int _done = int.MinValue;
        private int _total;
        private int _money = int.MinValue;
        private int _time = int.MinValue;

        /// <param name="doneKey">The locale key of line 2's first part, with {done} and {count} in it ("Flyers taken {done} of {count}").</param>
        public ShiftFigures(JobContext ctx, JobDef def, string doneKey)
        {
            _card = ctx.Card;
            _doneKey = doneKey;
            _startMinutes = TimeHelper.NowInMinutes();
            _limitColour = _white;
            if (_card == null)
            {
                return;
            }
            _card.Title(JobText.T(ctx.Shift != null ? ctx.Shift.Rank.TitleKey : def.NameKey));
            _card.Line(0, ctx.Shift != null
                ? JobText.T("reallife_job_card_employer", "employer", JobText.T(ctx.Shift.Employer.NameKey), "time", CareerClock.Span(CareerState.RotaHour))
                : JobText.T("reallife_job_card_gig"), _white);
            for (int line = 1; line < ShiftCard.Lines; line++)
            {
                _card.Line(line, string.Empty, _white);   // what the last shift left on the shared card
            }
        }

        /// <summary>True when the game's panel was copied and the figures are being shown.</summary>
        public bool Shown => _card != null;

        /// <summary>Line 1: what to do now, as final words.</summary>
        public void Objective(string words)
        {
            _card?.Line(1, words, _white);
        }

        /// <summary>Line 1 in a colour (yellow for something to put right).</summary>
        public void Objective(string words, Color32 colour)
        {
            _card?.Line(1, words, colour);
        }

        /// <summary>Line 2, first part: the units done of the shift's total.</summary>
        public void Units(int done, int total)
        {
            if (_card == null || (done == _done && total == _total))
            {
                return;
            }
            _done = done;
            _total = total;
            _units = JobText.T(_doneKey, "done", done.ToString(), "count", total.ToString());
            Compose();
        }

        /// <summary>Line 2, second part: what can end the shift early, as final words (null for nothing). The caller makes the words only when they change.</summary>
        public void Limit(string words, Color32 colour)
        {
            if (_card == null)
            {
                return;
            }
            _limit = words;
            _limitColour = colour;
            Compose();
        }

        private void Compose()
        {
            _card.Line(2, string.IsNullOrEmpty(_limit) ? _units : _units + ", " + _limit, _limitColour);
        }

        /// <summary>Line 3: what the shift has earned and the tips in it.</summary>
        public void Money(int earned, int tips)
        {
            if (_card == null)
            {
                return;
            }
            int money = earned | (tips << 16);
            if (money == _money)
            {
                return;
            }
            _money = money;
            _card.Line(3, tips > 0
                ? JobText.T("reallife_job_card_earned_tips", "earned", JobText.Money(earned), "tips", JobText.Money(tips))
                : JobText.T("reallife_job_card_earned", "earned", JobText.Money(earned)), _white);
        }

        /// <summary>
        /// Line 4: the time on shift in steps of five game minutes, and about how long is left at the clock's pace
        /// (<paramref name="leftMinutes"/>, or negative for a shift whose length is not known).
        /// </summary>
        public void Time(int leftMinutes)
        {
            if (_card == null)
            {
                return;
            }
            int on = Mathf.Max(0, Mathf.FloorToInt((TimeHelper.NowInMinutes() - _startMinutes) / 5f)) * 5;
            int time = on | ((leftMinutes + 1) << 12);
            if (time == _time)
            {
                return;
            }
            _time = time;
            _card.Line(4, leftMinutes < 0
                ? JobText.T("reallife_job_card_time_on", "on", Duration(on))
                : JobText.T("reallife_job_card_time", "on", Duration(on), "left", Duration(leftMinutes)), _white);
        }

        /// <summary>"1 h 20 min", from the same text the drinks cart's card uses.</summary>
        public static string Duration(int minutes)
        {
            return JobText.T("reallife_job_duration", "hours", (minutes / 60).ToString(), "minutes", (minutes % 60).ToString());
        }

        /// <summary>A countdown in seconds as "6:05".</summary>
        public static string Clock(int seconds)
        {
            seconds = Mathf.Max(0, seconds);
            return (seconds / 60).ToString() + ":" + (seconds % 60).ToString("00");
        }
    }
}
