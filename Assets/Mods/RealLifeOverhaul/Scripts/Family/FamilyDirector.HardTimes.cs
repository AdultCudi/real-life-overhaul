namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// FamilyDirector's part for the difficult moments: the options page's test of one, from what is going on.
    /// </summary>
    public sealed partial class FamilyDirector
    {
        /// <summary>
        /// The difficult moment that fits what is going on best, asked now whatever its hour and the days since the
        /// last (the cause it came of is in Player.log); with nothing wrong between the two, a notice says so and
        /// nothing is asked.
        /// </summary>
        public void TestArgue()
        {
            const string test = "Family difficult moment";
            if (!TestReady(test, false, false) || !TestPartner(test))
            {
                return;
            }
            if (!Settings.FamilyFriction)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff, 0, "hard times are switched off");
                return;
            }
            Guarded(() =>
            {
                if (HardTimesWatch.Test(out _) < 0)
                {
                    Refuse(test, "reallife_family_no_argue_cause");
                }
            });
        }
    }
}
