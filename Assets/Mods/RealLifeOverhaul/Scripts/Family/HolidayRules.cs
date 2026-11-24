using System;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// What a holiday taken together does for the family, by the rules family life already has for time well spent (a date's affection and harmony, a school
    /// event's closeness): more for a longer trip, never past a date's clamp. Game-free; computed when a trip ends.
    /// </summary>
    public static class HolidayRules
    {
        public const float AffectionBase = 9f;         // a date's affection (FamilyBalance.AffectionDate)
        public const float AffectionPerDay = 1.5f;     // and this much more for each day of the trip,
        public const int DaysCounted = 5;              // up to this many
        public const float HarmonyBase = 6f;           // the home's harmony: a little under a date's (FamilyBalance.HarmonyDate)
        public const float HarmonyPerDay = 1f;
        public const float HarmonyMost = 10f;          // the most a shared time may give (FamilyApi.SharedTime)
        public const float TrustGiven = 3f;            // as a date gives
        public const float ClosenessBase = 3f;         // a child's closeness for a trip together, as for a school event
        public const float ClosenessPerDay = 1f;
        public const float ClosenessMost = 8f;         // the most one trip gives a child (a school play gives 8)

        public static float Affection(int days) => AffectionBase + AffectionPerDay * Math.Min(Math.Max(days, 0), DaysCounted);

        public static float Harmony(int days) => Math.Min(HarmonyMost, HarmonyBase + HarmonyPerDay * Math.Min(Math.Max(days, 0), DaysCounted));

        public static float Closeness(int days) => Math.Min(ClosenessMost, ClosenessBase + ClosenessPerDay * Math.Min(Math.Max(days, 0), DaysCounted));
    }
}
