namespace RealLifeOverhaul
{
    /// <summary>
    /// When the mod's own calls on the player excuse a walk in the road. The mod sends the player to a downed person, an accident, a
    /// crime or a street event, to a car at the kerb or a job's mark, and asks them to step into the road to do it; the same police
    /// must not fine that. No Unity or game types: checked offline (EconomyCheck, TrafficOffenceChecks).
    /// </summary>
    internal static class CallRules
    {
        /// <summary>Metres from an accident, a downed person or a crime within which the player is there because of it.</summary>
        public const float SceneReach = 45f;

        /// <summary>Metres from a street event's middle: it shuts the road for this much of its length.</summary>
        public const float EventReach = 70f;

        /// <summary>Metres from a job's, quest's or taxi's mark on the game's guiders.</summary>
        public const float MarkReach = 40f;

        /// <summary>Metres from one of the player's own vehicles: getting in or out, loading it, a hand truck wheeled to it.</summary>
        public const float CarReach = 8f;

        /// <summary>Metres beyond the tape of a closed road that still count as inside it.</summary>
        public const float TapeMargin = 6f;

        /// <summary>Seconds after the last look that found a call within reach during which the walk is still excused.</summary>
        public const float Linger = 25f;

        /// <summary>Seconds after the player gets up from being knocked down during which nothing on foot is counted.</summary>
        public const float RiseSeconds = 8f;

        /// <summary>True if a point is within <paramref name="reach"/> metres of another, from their offset on the ground plane.</summary>
        public static bool Near(float dx, float dz, float reach)
        {
            return dx * dx + dz * dz <= reach * reach;
        }

        /// <summary>True for <see cref="RiseSeconds"/> after the player got up; a negative time means they were never knocked down.</summary>
        public static bool JustRisen(float risenAt, float now)
        {
            return risenAt >= 0f && now >= risenAt && now - risenAt < RiseSeconds;
        }
    }

    /// <summary>Remembers when a call was last within reach, so that the walk stays excused for <see cref="CallRules.Linger"/> after it ends.</summary>
    internal sealed class CallLedger
    {
        private float _calledAt = -1f;

        public void Reset()
        {
            _calledAt = -1f;
        }

        /// <summary>One look: true while a call is within reach or was a moment ago.</summary>
        public bool Hold(bool callsNow, float now)
        {
            if (callsNow)
            {
                _calledAt = now;
                return true;
            }
            return _calledAt >= 0f && now >= _calledAt && now - _calledAt < CallRules.Linger;
        }
    }
}
