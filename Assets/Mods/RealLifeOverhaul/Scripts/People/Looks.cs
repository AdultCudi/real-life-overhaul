using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// When a character's look may be built (a mesh merge: milliseconds and garbage): one a frame while hidden, else one
    /// every <see cref="Gap"/> seconds over all the pools; one built at need is counted "in play" for the session record.
    /// The game's skipMeshMerging is not used: it would cost every frame instead of a hitch now and then.
    /// </summary>
    public static class Looks
    {
        public const float Gap = 5f;   // seconds between two looks built in play

        public const int UsesBeforeChange = 3;           // a look handed out this often, or worn this long, is due a change
        public const float SecondsBeforeChange = 600f;   // seconds

        /// <summary>Looks built since the city loaded, and how many of those while the game was being played.</summary>
        public static int Built { get; private set; }
        public static int BuiltInPlay { get; private set; }

        private static float _last = -1000f;
        private static int _lastFrame = -1;
        private static int _mark;   // the session record's number for the look being built

        /// <summary>True while a long frame would not be seen as one: the loading screen is up, or the game is paused.</summary>
        public static bool Hidden => UI.Load.LoadScene.isLoading || Time.timeScale <= 0f;

        /// <summary>True when a look may be built ahead of need this frame: one a frame while hidden, else one per Gap.</summary>
        public static bool Free
        {
            get
            {
                if (Time.frameCount == _lastFrame)
                {
                    return false;
                }
                return Hidden || Time.unscaledTime - _last >= Gap;
            }
        }

        /// <summary>Called just before a look is built; <paramref name="what"/> names the work in the long-frame line.</summary>
        public static void Count(string what)
        {
            Built++;
            if (!Hidden)
            {
                BuiltInPlay++;
            }
            _last = Time.unscaledTime;
            _lastFrame = Time.frameCount;
            _mark = SessionWatch.Mark(what);
        }

        /// <summary>Called just after a look is built: its time goes beside its name in the session record.</summary>
        public static void Done()
        {
            SessionWatch.Done(_mark);
        }

        public static void Reset()
        {
            Built = 0;
            BuiltInPlay = 0;
            _last = -1000f;
            _lastFrame = -1;
        }

        public static bool Worn(int uses, float dressedAt)
        {
            return uses >= UsesBeforeChange || Time.unscaledTime - dressedAt >= SecondsBeforeChange;
        }
    }
}
