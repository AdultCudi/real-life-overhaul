namespace RealLifeOverhaul.Designs
{
    /// <summary>The locale keys of the ready-made designs, written out whole so that the text checks find them. Read only when something is shown.</summary>
    public static class DesignsText
    {
        public const string SaySome = "reallife_designs_say_some";              // the test: {count} designs for {types} kinds of business
        public const string SayWorking = "reallife_designs_say_working";        // the test: the designs are being put in place
        public const string SayFailed = "reallife_designs_say_failed";          // the test: none could be put in place
        public const string InstallNone = "reallife_designs_install_none";      // the test: no order waits for a business of the mod's
        public const string InstallDone = "reallife_designs_install_done";      // the test: {count} orders done now
        public const string InstallSession = "reallife_designs_install_session"; // the test: not in a Going Public session
        public const string ReportRemoved = "reallife_designs_report_removed";  // prepare for removal: {count} designs taken away
    }
}
