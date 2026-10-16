namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The white box's own time on a page, for its self-check (InfoBox.Parts.cs, "Info box check"). A page is written
    /// between the box's Clear and Done, and between them the app does its own work too: the Settings app compares every
    /// option with the presets and fills its table of a hundred rows, others read their figures. Timed from Clear to Done,
    /// that work was blamed on the box (a page of five parts "written in 5.5 ms", a page of seventeen elsewhere in 2.6).
    /// Here only the time inside the box's own calls counts: each call enters and leaves, a call made from inside another
    /// (a row's text, its copy of a template) is part of the outer one, and the time between calls is the page's.
    /// Plain arithmetic on the timestamps it is given: no Unity or game types. Checked offline (ContractCheck, BoxClockChecks).
    /// COST: two comparisons and an addition a call; nothing allocated; nothing per frame.
    /// </summary>
    public sealed class BoxClock
    {
        private int _depth;
        private long _began, _entered, _own;

        /// <summary>A page begins: nothing of the box's counted yet.</summary>
        public void Begin(long now)
        {
            _began = now;
            _own = 0;
            _depth = 0;
        }

        /// <summary>A call of the box's begins; one inside another counts with it.</summary>
        public void Enter(long now)
        {
            if (_depth++ == 0)
            {
                _entered = now;
            }
        }

        /// <summary>A call of the box's ends; the outermost one adds its time.</summary>
        public void Leave(long now)
        {
            if (_depth > 0 && --_depth == 0)
            {
                _own += now - _entered;
            }
        }

        /// <summary>The box's own time on the page so far, in the timestamps' units (a call under way counted to now).</summary>
        public long Own(long now)
        {
            return _depth > 0 ? _own + (now - _entered) : _own;
        }

        /// <summary>The page's whole time since it began: the box's and the app's own work between its calls.</summary>
        public long Page(long now)
        {
            return now - _began;
        }
    }
}
