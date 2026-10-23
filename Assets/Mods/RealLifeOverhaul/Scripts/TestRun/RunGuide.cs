using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.TestRun
{
    /// <summary>
    /// The test run's button, first of the Diagnostics tests (Options, Troubleshooting, Test tools, Diagnostics), beside
    /// the two switches it needs. It registers itself (<see cref="DescribesFeatureAttribute"/>); Features.Add hands back
    /// the Diagnostics entry Meta/FeatureList made. A test of a feature joins the run with one <c>.Check(...)</c> in that
    /// feature's own guide (Meta/Features.cs says how).
    /// </summary>
    [DescribesFeature]
    public static class RunGuide
    {
        private static void DescribeFeature()
        {
            Features.Add("diagnostics", "reallife_meta_title_diagnostics")
                .TestFirst(RunText.Button, RunDirector.Press);
        }
    }
}
