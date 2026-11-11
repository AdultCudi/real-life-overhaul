using UI.Elements;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A stint of work on a freelance contract as the game's work panel runs it (Jobs/ShiftSkip): the words are the contract's sitting's, and the length is the player's to pick on the panel's
    /// slider before it starts, one hour to what the contract has left (OwnRules.StintMinutes), or three hours where its switch is off. At a desk the player sits as the game seats its
    /// own staff and stands up on every end (CareersDirector.OwnStint); with no desk they stay where they are, as the Career app has always started it. The minutes are settled once,
    /// by the sitting's record. Nothing per frame: the panel asks it only as the clock moves.
    /// </summary>
    internal sealed class ContractStint : ISlidingShift
    {
        private readonly CareersDirector _director;
        private readonly int _left;       // minutes of the contract still to work when the panel opened
        private readonly bool _slides;    // the length is the player's to pick on the slider (its switch is on)
        private int _hours;               // the hours the slider stands at

        public readonly Sitting Sitting;                  // the contract's sitting in the director's book: settled once, whichever way the stint ends
        public readonly EmployeeStationController Desk;   // the desk it is worked at; null where the player stands
        public readonly string Place;                     // the place key of the business the desk is in; null with no desk
        public ShiftSkip Skip;                            // the game's panel that runs it, once offered
        public bool Begun;                                // the player went to sit down at the desk: every end stands them up
        public bool Started;                              // the seat holds and the sitting has begun: its end counts the minutes
        public bool Over;                                 // ended, whichever way
        public int Worked;                                // minutes passed so far, as the panel's clock counts them

        public ContractStint(CareersDirector director, Sitting sitting, int left, EmployeeStationController desk, string place, int hours, bool slides)
        {
            _director = director;
            _left = left;
            _slides = slides;
            _hours = OwnRules.StintHours(hours, left);
            Sitting = sitting;
            Desk = desk;
            Place = place;
        }

        public string SkipTitle => Sitting.SkipTitle;

        public int SkipMinutes => _slides ? OwnRules.StintMinutes(_hours, _left) : OwnRules.FixedMinutes(_left);

        public LabelInfo[] SkipLabels() => Sitting.SkipLabels();

        public int SlideLeast => OwnRules.SittingHoursLeast;

        public int SlideMost => _slides ? OwnRules.StintMostHours(_left) : OwnRules.SittingHoursLeast;

        public int SlideNow => OwnRules.StintHours(_hours, _left);

        // The slider moved: the hours kept for the next stint too, while the game runs; the panel's length follows.
        public void Slide(int value)
        {
            _hours = OwnRules.StintHours(value, _left);
            _director.OwnHoursChosen(_hours);
        }

        public (string key, object arguments) SlideInfo() => ("common_hours", new { hours = SlideNow });

        public void SkipStarted() => _director.OwnStintStarted(this);

        public void SkipWorked(int minutes)
        {
            Worked = minutes;
        }

        public void SkipEnded(int minutes, bool whole) => _director.OwnStintEnded(this, minutes, whole);
    }
}
