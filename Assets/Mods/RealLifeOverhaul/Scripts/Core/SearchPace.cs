namespace RealLifeOverhaul
{
    /// <summary>
    /// When a whole-scene search for something that can turn up at any time may be made: only on a frame the screen
    /// shows black, never in play and never in the game's 0.2 s fades at a door, where a frame of 80 to 110 ms stops the
    /// picture showing through. In one dark stretch (the fade under way, or the loading screen) a search is made at most
    /// twice: as soon as the game says a building has come in behind it (<see cref="Arrived"/>, onEnterBuildingDelayed,
    /// four frames before the fade back in), else after <see cref="First"/> seconds and again at <see cref="Second"/>.
    /// One per searcher; a few comparisons a frame. Game-free.
    /// </summary>
    public sealed class SearchPace
    {
        public const float First = 0.5f;    // seconds into a stretch of black screen or loading before the first search
        public const float Second = 2.5f;   // ...and before the second, if it is still up
        public const int Most = 2;          // searches at the most in one stretch

        private float _since = -1f;         // when the stretch began; negative while the game is being played
        private int _made;                  // searches made in this stretch
        private bool _arrived;              // a building came in behind the black screen, and no search since

        /// <summary>Searches made while hidden, for the log and the checks.</summary>
        public int Made { get; private set; }

        /// <summary>A building has come in behind the black screen: the next black frame makes a search.</summary>
        public void Arrived()
        {
            _arrived = true;
        }

        /// <summary>
        /// True if a search may be made now. <paramref name="dark"/>: the stretch is under way; <paramref name="black"/>:
        /// the screen shows black in this frame; <paramref name="now"/>: real seconds.
        /// </summary>
        public bool Due(bool dark, bool black, float now)
        {
            if (!dark)
            {
                _since = -1f;
                _made = 0;
                _arrived = false;   // said with nothing dark: the game went in without its fade
                return false;
            }
            if (_since < 0f)
            {
                _since = now;
            }
            if (!black || _made >= Most)
            {
                return false;
            }
            float into = now - _since;
            if (_arrived || (_made == 0 && into >= First) || (_made == 1 && into >= Second))
            {
                _arrived = false;
                _made++;
                Made++;
                return true;
            }
            return false;
        }
    }
}
