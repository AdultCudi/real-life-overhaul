using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// When a character's look may be built, and how many have been.
    ///
    /// Building a look is the one costly thing the mod's people need: the game merges the character's
    /// meshes into one (AppearanceSetter.UpdateVisuals), a few milliseconds and a good deal of memory
    /// to be collected each time. The game's own pedestrians are dressed once and kept, so while the
    /// city is played the game does none of this; whatever the mod does is its own. The rule:
    ///
    /// - Looks are built where it cannot be seen: while the loading screen is up, or the game is paused
    ///   (a menu, the map, a panel), one a frame.
    /// - Otherwise one at a time, with <see cref="Gap"/> seconds between two, whoever asks: every pool
    ///   that prepares a look ahead of need asks <see cref="Free"/> first.
    /// - Never in the frame somebody is wanted, if it can be helped: the pools keep people dressed for
    ///   what was asked of them before and hand those out; a look nobody has is prepared for the next
    ///   time. Where a scene must have its person now and nobody is dressed for it, the look is still
    ///   built on the spot: that is counted as built "in play", and the count is what the session
    ///   record shows (Diagnostics/SessionWatch).
    ///
    /// The alternative the game offers, AppearanceSetter.skipMeshMerging (no merge at all; the mod uses
    /// it for the player's one high-detail double), was weighed and not taken for street people: every
    /// garment stays a skinned renderer of its own, eight to eleven a character instead of one, drawn,
    /// culled and skinned every frame for up to twenty-odd people. That trades a hitch now and then
    /// for a cost in every frame, and the pools' hiding of people out of view switches one renderer.
    /// </summary>
    public static class Looks
    {
        /// <summary>Seconds between two looks built while the game is being played.</summary>
        public const float Gap = 5f;

        /// <summary>A look this many times out, or this old, is due a change (made quietly, never at need).</summary>
        public const int UsesBeforeChange = 3;
        public const float SecondsBeforeChange = 600f;

        /// <summary>Looks built since the city loaded, and how many of those while the game was being played.</summary>
        public static int Built { get; private set; }
        public static int BuiltInPlay { get; private set; }

        private static float _last = -1000f;
        private static int _lastFrame = -1;
        private static int _mark;   // the session record's number for the look being built

        /// <summary>True while a long frame would not be seen as one: the loading screen is up, or the game is paused.</summary>
        public static bool Hidden => UI.Load.LoadScene.isLoading || Time.timeScale <= 0f;

        /// <summary>
        /// Whether a look may be built ahead of need in this frame: at most one a frame while hidden, else one
        /// every <see cref="Gap"/> seconds over all the pools together.
        /// </summary>
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

        /// <summary>Called by whoever builds a look, just before it does. A constant text only (it names the work for the long-frame line).</summary>
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

        /// <summary>
        /// Called by whoever built the look, just after: the merge's time is then written beside its name in the
        /// session record's line for a long frame. One read of the clock.
        /// </summary>
        public static void Done()
        {
            SessionWatch.Done(_mark);
        }

        /// <summary>The city is loading: the counts start again.</summary>
        public static void Reset()
        {
            Built = 0;
            BuiltInPlay = 0;
            _last = -1000f;
            _lastFrame = -1;
        }

        /// <summary>Whether a look worn since a time, and handed out so many times, is due a change.</summary>
        public static bool Worn(int uses, float dressedAt)
        {
            return uses >= UsesBeforeChange || Time.unscaledTime - dressedAt >= SecondsBeforeChange;
        }
    }
}
