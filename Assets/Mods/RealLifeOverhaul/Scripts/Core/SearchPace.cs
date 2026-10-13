namespace RealLifeOverhaul
{
    /// <summary>
    /// When a search of the whole scene for something that can turn up at any time may be made: only while a long
    /// frame is not seen (the loading screen, a pause, the game's own black screen at a door), and there twice at the
    /// most in one stretch of it: once when it has been up for <see cref="First"/> seconds (the screen is black by
    /// then), and once more at <see cref="Second"/> if it is still up (a building that took its time to come in). Never
    /// while the game is being played: such a search is a frame of some 100 ms in a city (Core/SceneFinds), and the
    /// characters' hair finish asked for one every five seconds of play (Character/CharacterQuality.FindOthers).
    ///
    /// One per searcher. Asked once a frame, it costs two comparisons. No Unity or game types: checked offline
    /// (NavCheck, HitchCheck.cs).
    /// </summary>
    public sealed class SearchPace
    {
        public const float First = 0.5f;    // seconds into a stretch of black screen, loading or pause before the first search
        public const float Second = 2.5f;   // ...and before the second, if it is still up

        private float _since = -1f;         // when the screen went; negative while the game is being played
        private int _made;                  // searches made in this stretch

        /// <summary>Searches made while hidden since this pace was made, for the log and the checks.</summary>
        public int Made { get; private set; }

        /// <summary>
        /// True if a search may be made now. <paramref name="hidden"/>: a long frame would not be seen; <paramref name="now"/>:
        /// real seconds. The caller makes the search when this says so.
        /// </summary>
        public bool Due(bool hidden, float now)
        {
            if (!hidden)
            {
                _since = -1f;
                _made = 0;
                return false;
            }
            if (_since < 0f)
            {
                _since = now;
            }
            float into = now - _since;
            if ((_made == 0 && into >= First) || (_made == 1 && into >= Second))
            {
                _made++;
                Made++;
                return true;
            }
            return false;
        }
    }
}
