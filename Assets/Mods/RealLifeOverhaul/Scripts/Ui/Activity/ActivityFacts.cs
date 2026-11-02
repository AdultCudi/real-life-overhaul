using PlayerActivity;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the mod knows of an activity of its own beyond what the game's panel says: filled into the kit's model after
    /// the panel's own words. Game side. Cost: nothing for the game's own activities.
    /// </summary>
    internal static class ActivityFacts
    {
        /// <summary>The mod's facts for its own activities; the game's own are left as the panel says them.</summary>
        public static void Fill(IPlayerActivity a, UiActivityModel m)
        {
            if (a is TestActivity test)
            {
                m.Icon = test.What == TestActivity.Kind.Class ? UiIcon.Cap : test.What == TestActivity.Kind.Chair ? UiIcon.Presentation : UiIcon.Briefcase;
            }
        }
    }
}
