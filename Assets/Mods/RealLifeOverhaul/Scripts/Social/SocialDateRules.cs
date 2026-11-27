using System;

namespace RealLifeOverhaul
{
    /// <summary>How long it is since the player last saw somebody, in the steps a greeting can tell apart (SocialDateRules.GreetSpanOf).</summary>
    public enum GreetSpan { Same, Days, Week, Long }

    /// <summary>
    /// The rules of keeping a date that need no game: which greetings fit the time since the two last met, who is late when
    /// the other is not there, the pace a walking companion keeps and which places a walk starts at. No allocation; all of
    /// it runs once per event or every 0.4 s of a walk.
    /// </summary>
    public static class SocialDateRules
    {
        public const int GreetDays = 1;          // days since they last met from which a line may speak of "today" or of missing them
        public const int GreetWeekDays = 5;      // ...from which "I've so much to tell you"
        public const int GreetLongDays = 10;     // ...from which "it's been too long"
        public const float KeptWaitingMinutes = 8f;   // game minutes the player stood at the place before the other was there: they say so
        public const float CatchUpGap = 3f;      // metres behind the player from which a companion hurries a little
        public const float FarGap = 7f;          // ...and from which they go at the fastest pace the game has
        public const float CatchUpFactor = 1.25f;   // of the player's pace, while hurrying a little
        public const float SightSlack = 40f;     // metres beyond a sight's own radius at which a place still counts as at it

        /// <summary>The span a greeting is chosen for, from the game day of the last outing and of first meeting (0 when none is known: nothing is then claimed).</summary>
        public static GreetSpan GreetSpanOf(int today, int lastOutingDay, int metDay)
        {
            int last = Math.Max(lastOutingDay, metDay);
            int apart = last > 0 ? today - last : 0;
            return apart >= GreetLongDays ? GreetSpan.Long : apart >= GreetWeekDays ? GreetSpan.Week : apart >= GreetDays ? GreetSpan.Days : GreetSpan.Same;
        }

        /// <summary>The fewest days apart a span stands for.</summary>
        public static int SpanDays(GreetSpan span)
        {
            return span == GreetSpan.Long ? GreetLongDays : span == GreetSpan.Week ? GreetWeekDays : span == GreetSpan.Days ? GreetDays : 0;
        }

        /// <summary>
        /// Game minutes the player is late by: from the agreed time to the minute they were at the place (<paramref name="playerAt"/>, negative
        /// when never noted), never to the minute the other turned up or the hello was said. Negative is early.
        /// </summary>
        public static float LateBy(float now, float agreed, float playerAt)
        {
            return (playerAt >= 0f ? playerAt : now) - agreed;
        }

        /// <summary>
        /// Whether the player is to be counted as having stood the other up when the window for it has passed: unless they were at the
        /// place within it and the other never turned up (<paramref name="partnerAt"/> negative), when it was the other who did not come.
        /// </summary>
        public static bool StoodUpByPlayer(float agreed, float playerAt, float partnerAt)
        {
            bool came = playerAt >= 0f && playerAt <= agreed + SocialRules.StoodUpMinutes;
            return !(came && partnerAt < 0f);
        }

        /// <summary>Whether the other came well after the player was at the place (both game minutes, negative when not noted).</summary>
        public static bool KeptWaiting(float playerAt, float partnerAt)
        {
            return playerAt >= 0f && partnerAt >= 0f && partnerAt - playerAt > KeptWaitingMinutes;
        }

        /// <summary>
        /// The speed a walking companion is sent at, m/s: the player's own, kept between the game's walk and run (its
        /// GlobalReferences figures), a little more while behind and the fastest when far behind. <paramref name="usual"/> is
        /// the game's jog, what the player walks at unless they run, taken when the player's speed is not known.
        /// </summary>
        public static float PartnerPace(float player, float gap, float slowest, float usual, float fastest)
        {
            float pace = player > 0.01f ? Math.Min(fastest, Math.Max(slowest, player)) : Math.Min(fastest, Math.Max(slowest, usual));
            if (gap >= FarGap)
            {
                return fastest;
            }
            return gap > CatchUpGap ? Math.Min(fastest, pace * CatchUpFactor) : pace;
        }

        /// <summary>A sight a walk may be set at: any but the hospital.</summary>
        public static bool PleasantSight(string id)
        {
            return !string.IsNullOrEmpty(id) && id != "hospital";
        }

        /// <summary>Whether a point is at a sight: on the flat within its radius and <see cref="SightSlack"/>.</summary>
        public static bool NearSight(float dx, float dz, float radius)
        {
            float reach = radius + SightSlack;
            return dx * dx + dz * dz <= reach * reach;
        }
    }
}
