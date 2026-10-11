namespace RealLifeOverhaul
{
    /// <summary>
    /// The counting behind "another mod has the camera": told once a frame whether something that is neither the
    /// game nor this mod drove the camera in the frame before, it says when this mod's camera stands down and when
    /// it comes back.
    ///
    /// One odd frame proves nothing (a camera cut, a frame in which the game moves the player), so the camera stands
    /// down only after <see cref="ToYield"/> such frames running, or <see cref="ToYieldKnown"/> when a camera mod
    /// this mod knows by name is installed (then one sighting is no surprise). It comes back only after
    /// <see cref="ToReturn"/> clean frames running: far more than it took to stand down, so two cameras never take
    /// turns frame by frame, which is the jitter this exists to prevent.
    ///
    /// Two integers and a flag: no game types, nothing allocated. One call a frame. Checked offline (NavCheck).
    /// </summary>
    public struct CameraWatch
    {
        public const int ToYield = 12;        // frames running: a fifth of a second at 60 frames a second, longer than any cut of the game's own
        public const int ToYieldKnown = 3;    // frames running when a known camera mod is installed
        public const int ToReturn = 90;       // clean frames running before this mod's camera comes back: a second and a half at 60 frames a second

        private int _foreign;
        private int _clean;

        /// <summary>True while this mod's camera is to do nothing at all.</summary>
        public bool StoodDown { get; private set; }

        /// <summary>Frames running in which something else drove the camera (0 after a clean one).</summary>
        public int Foreign => _foreign;

        /// <summary>Clean frames running (0 after one in which something else drove the camera).</summary>
        public int Clean => _clean;

        /// <summary>
        /// One frame's finding. True in the frame in which <see cref="StoodDown"/> changes, false in every other.
        /// </summary>
        /// <param name="foreign">Something that is neither the game nor this mod drove the camera.</param>
        /// <param name="known">A camera mod this mod knows by name is installed.</param>
        public bool Feed(bool foreign, bool known)
        {
            if (foreign)
            {
                _clean = 0;
                if (_foreign < int.MaxValue)
                {
                    _foreign++;
                }
                if (!StoodDown && _foreign >= (known ? ToYieldKnown : ToYield))
                {
                    StoodDown = true;
                    return true;
                }
                return false;
            }
            _foreign = 0;
            if (_clean < int.MaxValue)
            {
                _clean++;
            }
            if (StoodDown && _clean >= ToReturn)
            {
                StoodDown = false;
                return true;
            }
            return false;
        }

        /// <summary>Back to the beginning: a new city, where nothing has been seen yet.</summary>
        public void Reset()
        {
            _foreign = 0;
            _clean = 0;
            StoodDown = false;
        }
    }
}
