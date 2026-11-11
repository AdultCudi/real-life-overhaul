using UI.Elements;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A stint of work on a freelance contract as the game's work panel runs it (Jobs/ShiftSkip): the words are the contract's sitting's, and the length is the player's to pick on the panel's
    /// slider before it starts, one hour to twelve or what the contract has left (StintSlider), or three hours where its switch is off. At a desk the player sits as the game seats its
    /// own staff and stands up on every end (CareersDirector.OwnStint); with no desk they stay where they are, as the Career app has always started it. The minutes are settled once,
    /// by the sitting's record. Nothing per frame: the panel asks it only as the clock moves.
    /// </summary>
    internal sealed class ContractStint : ISlidingShift
    {
        private readonly CareersDirector _director;
        private readonly StintSlider _slider;   // the hours: where the slider stands, and the moves of it that are the player's, kept for the next stint

        public readonly Sitting Sitting;                  // the contract's sitting in the director's book: settled once, whichever way the stint ends
        public readonly EmployeeStationController Desk;   // the desk it is worked at; null where the player stands
        public readonly string Place;                     // the place key of the business the desk is in; null with no desk
        public ShiftSkip Skip;                            // the game's panel that runs it, once offered
        public bool Begun;                                // the player went to sit down at the desk: every end stands them up
        public bool Started;                              // the seat holds and the sitting has begun: its end counts the minutes
        public bool Over;                                 // ended, whichever way
        public int Worked;                                // minutes passed so far, as the panel's clock counts them

        public ContractStint(CareersDirector director, Sitting sitting, EmployeeStationController desk, string place, StintSlider slider)
        {
            _director = director;
            _slider = slider;
            Sitting = sitting;
            Desk = desk;
            Place = place;
        }

        public string SkipTitle => Sitting.SkipTitle;

        public int SkipMinutes => _slider.Minutes;

        public LabelInfo[] SkipLabels() => Sitting.SkipLabels();

        public int SlideLeast => _slider.Least;

        public int SlideMost => _slider.Most;

        public int SlideNow => _slider.Now;

        /// <summary>The panel has made its offer: from here a move of the slider is the player's, and the hours they pick are kept for the next stint.</summary>
        public void Offered() => _slider.Offered();

        // The slider moved: the slider keeps a move of the player's (and tells the director the hours), not the values the panel sets it up with; the panel's length follows.
        public void Slide(int value) => _slider.Slide(value);

        public (string key, object arguments) SlideInfo() => ("common_hours", new { hours = SlideNow });

        public void SkipStarted() => _director.OwnStintStarted(this);

        public void SkipWorked(int minutes)
        {
            Worked = minutes;
        }

        public void SkipEnded(int minutes, bool whole) => _director.OwnStintEnded(this, minutes, whole);
    }
}
