namespace RealLifeOverhaul
{
    /// <summary>
    /// The counting behind "another mod has the camera", fed once a frame. One odd frame proves nothing (a cut, a frame in
    /// which the game moves the player), so the camera stands down only after <see cref="ToYield"/> such frames running
    /// (<see cref="ToYieldKnown"/> with a known camera mod installed), and comes back only after far more clean frames
    /// (<see cref="ToReturn"/>), so two cameras never take turns frame by frame. Nothing allocated; checked offline.
    /// </summary>
    public struct CameraWatch
    {
        public const int ToYield = 12;        // frames running: a fifth of a second at 60 fps, longer than any cut of the game's
        public const int ToYieldKnown = 3;    // frames running when a known camera mod is installed
        public const int ToReturn = 90;       // clean frames running: a second and a half at 60 fps

        private int _foreign;
        private int _clean;

        /// <summary>True while this mod's camera is to do nothing at all.</summary>
        public bool StoodDown { get; private set; }

        /// <summary>Frames running in which something else drove the camera (0 after a clean one).</summary>
        public int Foreign => _foreign;

        /// <summary>Clean frames running (0 after one in which something else drove the camera).</summary>
        public int Clean => _clean;

        /// <summary>One frame's finding; true in the frame in which <see cref="StoodDown"/> changes.</summary>
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

        /// <summary>Back to the beginning, for a new city.</summary>
        public void Reset()
        {
            _foreign = 0;
            _clean = 0;
            StoodDown = false;
        }
    }
}
