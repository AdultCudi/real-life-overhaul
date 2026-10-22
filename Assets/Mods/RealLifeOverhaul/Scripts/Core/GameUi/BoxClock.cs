namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The white box's own time on a page, for its self-check ("Info box check"). Between the box's Clear and Done the app
    /// does its own work too (the Settings app compares every option with the presets), which a Clear-to-Done time blamed on
    /// the box. Here only the time inside the box's own calls counts: each call enters and leaves, one made inside another is
    /// part of the outer, and the time between calls is the page's. Game-free arithmetic on the timestamps given; checked
    /// offline (ContractCheck BoxClockChecks). Costs two comparisons and an addition a call, nothing a frame.
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

        /// <summary>The box's own time on the page so far, in the timestamps' units, a call under way counted to now.</summary>
        public long Own(long now)
        {
            return _depth > 0 ? _own + (now - _entered) : _own;
        }

        /// <summary>The page's whole time since it began: the box's and the app's work between its calls.</summary>
        public long Page(long now)
        {
            return now - _began;
        }
    }
}
