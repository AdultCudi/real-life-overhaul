using System;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The hours slider of a freelance contract's stint on the game's work panel, as rules with no game types (checked offline): where it stands, how long the stint then
    /// runs, and the hours the player picks, which are handed on to be kept for the next stint only when the player moves it. The panel sets its slider up inside its own
    /// offer for every activity, with a slider or not (PlayerActivityUI.SetUpSlider holds the slider's old stand to the new ends, then puts it where the activity says it
    /// stands) and tells the activity each value it puts there. Those are not the player's hands: a move counts only once the offer is done (<see cref="Offered"/>), so
    /// the hours kept are never the contract's own ends. With the slider off a stint is <see cref="OwnRules.FixedMinutes"/> and nothing is picked. Cost: a few sums.
    /// </summary>
    public sealed class StintSlider
    {
        private readonly int _left;            // minutes of the contract still to work when the panel opened
        private readonly bool _slides;         // the length is the player's to pick (its switch is on)
        private readonly Action<int> _picked;  // told the hours the player moved the slider to, to keep for the next stint
        private int _hours;                    // where the slider stands
        private bool _offered;                 // the panel's offer is done: what it tells the slider from here is the player's

        /// <summary>A slider for a contract with this much left, starting where the player last left one (held to this contract's ends); picked hears each move.</summary>
        public StintSlider(int remembered, int leftMinutes, bool slides, Action<int> picked)
        {
            _left = leftMinutes;
            _slides = slides;
            _picked = picked;
            _hours = OwnRules.StintHours(remembered, leftMinutes);
        }

        /// <summary>The slider's left end.</summary>
        public int Least => OwnRules.SittingHoursLeast;

        /// <summary>The slider's right end: the hours the contract takes, never over twelve; with the slider off, the left end.</summary>
        public int Most => _slides ? OwnRules.StintMostHours(_left) : Least;

        /// <summary>Where the slider stands.</summary>
        public int Now => _slides ? _hours : Least;

        /// <summary>The minutes the stint runs as the slider stands.</summary>
        public int Minutes => _slides ? OwnRules.StintMinutes(_hours, _left) : OwnRules.FixedMinutes(_left);

        /// <summary>True if the slider is there to move: its switch is on and the contract has more than an hour to give.</summary>
        public bool Slides => _slides && Most > Least;

        /// <summary>The panel has made its offer: from here a value it tells the slider is the player's move.</summary>
        public void Offered()
        {
            _offered = true;
        }

        /// <summary>The panel tells the slider a value: kept and handed on if the player moved it to this hour, and not if the panel is setting up or the slider stands there already.</summary>
        public void Slide(int value)
        {
            if (!_slides || !_offered)
            {
                return;
            }
            int hours = OwnRules.StintHours(value, _left);
            if (hours == _hours)
            {
                return;
            }
            _hours = hours;
            _picked?.Invoke(hours);
        }
    }
}
