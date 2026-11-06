namespace RealLifeOverhaul
{
    /// <summary>What a stuck school car comes to, in order.</summary>
    public enum StallStage
    {
        None,    // the car is going, or has not been asked to yet
        Mend,    // the pedal has been held with no travel: look at why, put right what can be, say so in the log
        GiveUp   // still no travel after the mend: the drive is called off and the fee goes back
    }

    /// <summary>
    /// When the driving school's car is taken to be stuck, as plain numbers (NavCheck's TestStallCheck runs it): the accelerator
    /// held while the car does not move. Time is the real clock the caller gives, so a paused game is told apart by the caller
    /// and not counted here. A tap on the pedal counts only while it is down; a car that really goes starts everything again.
    /// Nothing allocated; the caller asks a few times a second while a drive is on.
    /// </summary>
    public sealed class TestStallRules
    {
        public const float PedalAsked = 0.3f;   // the accelerator as the game reads it, 0 to 1, from which it is held down
        public const float StillSpeed = 0.3f;   // m/s: slower than this the car is standing
        public const float Went = 1.5f;         // metres from where it stood: further than this it has gone
        public const float MendAfter = 6f;      // seconds of accelerator held with no travel before the mend
        public const float GiveUpAfter = 18f;   // ...and before the drive is called off
        public const float LetGo = 8f;          // seconds with the pedal up after which the count starts again
        public const float TookOffSpeed = 1f;   // m/s: the car has set off when it has gone this fast and this far

        private float _x, _z;       // where the car stood when the count began
        private float _originX, _originZ;   // where it stood at the first look: how far it has come from there
        private bool _anchored;
        private float _held;        // seconds of accelerator held with the car standing
        private float _up;          // seconds with the pedal up since it was last down
        private StallStage _stage;
        private bool _tookOff;
        private float _sinceBegin;  // seconds since Begin, for the line that says how soon the car went

        /// <summary>True once the car has gone <see cref="Went"/> metres at <see cref="TookOffSpeed"/> since <see cref="Begin"/>.</summary>
        public bool TookOff => _tookOff;

        /// <summary>Seconds from <see cref="Begin"/> to the car setting off; meaningful once <see cref="TookOff"/>.</summary>
        public float SetOffAfter { get; private set; }

        public StallStage Stage => _stage;

        /// <summary>Seconds of accelerator held with the car standing, for the log.</summary>
        public float Held => _held;

        public void Begin()
        {
            _anchored = false;
            _held = 0f;
            _up = 0f;
            _stage = StallStage.None;
            _tookOff = false;
            _sinceBegin = 0f;
            SetOffAfter = 0f;
        }

        /// <summary>
        /// One look. <paramref name="seconds"/> since the last look (real time), the accelerator 0 to 1, the car's speed in m/s and its
        /// place. The stage reached at this look: each is returned once, the mend and then the give up.
        /// </summary>
        public StallStage Look(float seconds, float pedal, float speed, float x, float z)
        {
            _sinceBegin += seconds;
            if (!_anchored)
            {
                _anchored = true;
                _x = x;
                _z = z;
                _originX = x;
                _originZ = z;
            }
            float dx = x - _x, dz = z - _z;
            float apart = dx * dx + dz * dz;
            float fx = x - _originX, fz = z - _originZ;
            if (!_tookOff && speed >= TookOffSpeed && fx * fx + fz * fz >= Went * Went)
            {
                _tookOff = true;
                SetOffAfter = _sinceBegin;
            }
            if (speed >= StillSpeed || apart >= Went * Went)
            {
                // Going: the count starts again from where it is.
                _x = x;
                _z = z;
                _held = 0f;
                _up = 0f;
                _stage = StallStage.None;
                return StallStage.None;
            }
            if (pedal >= PedalAsked)
            {
                _up = 0f;
                _held += seconds;
            }
            else
            {
                _up += seconds;
                if (_up >= LetGo)
                {
                    _held = 0f;
                    _stage = StallStage.None;
                }
                return StallStage.None;
            }
            if (_held >= GiveUpAfter && _stage != StallStage.GiveUp)
            {
                _stage = StallStage.GiveUp;
                return StallStage.GiveUp;
            }
            if (_held >= MendAfter && _stage == StallStage.None)
            {
                _stage = StallStage.Mend;
                return StallStage.Mend;
            }
            return StallStage.None;
        }
    }

    /// <summary>When the examiner is taken to be in the car after all, and what a frozen game is told.</summary>
    public static class TestBoardRules
    {
        public const float WaitForExaminer = 10f;   // seconds the candidate sits at the wheel before the examiner is taken to be aboard
        public const float FrozenFor = 3f;          // seconds with time stopped, at the wheel, before it is looked into
        public const float FrozenTold = 40f;        // seconds between two notices that the game is paused

        /// <summary>
        /// True when the examiner is to be seated now: the candidate has been at the wheel long enough, with time running, and the
        /// examiner is still not aboard (they could not find a way to the door, or were taken off the street).
        /// </summary>
        public static bool SeatNow(bool atWheel, float seatedFor, bool aboard, bool timeRuns)
        {
            return atWheel && !aboard && timeRuns && seatedFor >= WaitForExaminer;
        }

        /// <summary>Time is stopped when the game's scale is (almost) nothing.</summary>
        public static bool Frozen(float timeScale)
        {
            return timeScale < 0.01f;
        }

        /// <summary>
        /// A time lock left behind: the game's time controls are disabled while nothing that disables them is open, so the player
        /// could not take the game off pause with the key. The game's own map closing is what normally lets them go.
        /// </summary>
        public static bool LockLeft(bool controlsDisabled, bool mapOpen, bool menuOpen, bool dialogOpen)
        {
            return controlsDisabled && !mapOpen && !menuOpen && !dialogOpen;
        }
    }
}
