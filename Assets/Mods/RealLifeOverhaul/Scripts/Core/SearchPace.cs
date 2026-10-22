namespace RealLifeOverhaul
{
    /// <summary>
    /// When a search of the whole scene for something that can turn up at any time may be made: only while the screen
    /// shows black (the loading screen, or the game's own black screen drawn at full strength), never while the game is
    /// being played, and never while that black screen fades in or out. Such a search is a frame of some 80 to 110 ms in
    /// a city (Core/SceneFinds), and the game's fades at a door take 0.2 s each way (BuildingManager.EnterBuildingCoroutine
    /// and ExitFromBuildingCoroutine: UiFader.Fade(0.2f), UnFade(0.2f)): a long frame in one of them stops the picture that
    /// shows through it, and the player sees it.
    ///
    /// A stretch is the time the game's fade is under way, from the first frame of its fade out to the end of its fade
    /// back in, or the loading screen (<c>dark</c>). A search is made only on a frame of it in which the screen is fully
    /// black (<c>black</c>), twice at the most in one stretch:
    ///  - as soon as the game says a building has come in behind it (<see cref="Arrived"/>: its "entered, and settled"
    ///    call, GlobalEvents.onEnterBuildingDelayed, made with the screen still black, four frames before its fade back
    ///    in begins: BuildingManager.DelayedEnterBuildingActions, then WarmUpBehaviourManager);
    ///  - otherwise once it has been up for <see cref="First"/> seconds (a sleep, a taxi, a long black screen), and once
    ///    more at <see cref="Second"/> if it is still up.
    /// Up to 0.47.0 the search was made half a second into any fade, which at a door is in the middle of the fade back in:
    /// every walk into a building ended in a frame of 80 to 110 ms with the room half shown (the session of 21 Oct 2026:
    /// 27 searches, 21 of them frames over 80 ms).
    ///
    /// One per searcher. Asked once a frame, it costs a few comparisons. No Unity or game types: checked offline
    /// (NavCheck, HotSpotCheck.cs).
    /// </summary>
    public sealed class SearchPace
    {
        public const float First = 0.5f;    // seconds into a stretch of black screen or loading before the first search
        public const float Second = 2.5f;   // ...and before the second, if it is still up
        public const int Most = 2;          // searches at the most in one stretch

        private float _since = -1f;         // when the stretch began; negative while the game is being played
        private int _made;                  // searches made in this stretch
        private bool _arrived;              // the game has said a building came in behind the black screen, and no search has been made since

        /// <summary>Searches made while hidden since this pace was made, for the log and the checks.</summary>
        public int Made { get; private set; }

        /// <summary>
        /// The game says a building has come in behind its black screen (its "entered, and settled" call): the next frame
        /// of the stretch in which the screen is black makes a search, the one that finds who is in it.
        /// </summary>
        public void Arrived()
        {
            _arrived = true;
        }

        /// <summary>
        /// True if a search may be made now. <paramref name="dark"/>: the stretch is under way (the loading screen, or the
        /// game's black screen up or fading); <paramref name="black"/>: the screen shows black in this frame (the loading
        /// screen, or the black screen at full strength); <paramref name="now"/>: real seconds. The caller makes the search
        /// when this says so.
        /// </summary>
        public bool Due(bool dark, bool black, float now)
        {
            if (!dark)
            {
                _since = -1f;
                _made = 0;
                _arrived = false;   // (said with nothing dark: the game went in without its fade)
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
