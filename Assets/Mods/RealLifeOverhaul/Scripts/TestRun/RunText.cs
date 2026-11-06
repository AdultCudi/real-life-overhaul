namespace RealLifeOverhaul.TestRun
{
    /// <summary>The test run's texts, each a key in en.json, pt.json and zh-cn.json, written out whole as the locale checks read them.</summary>
    public static class RunText
    {
        public const string Button = "reallife_testrun_button";                 // City expansion: run every check
        public const string NeedsTools = "reallife_testrun_needs_tools";        // Developer diagnostics and Show test tools must be on
        public const string Session = "reallife_testrun_session";               // not in a Going Public session
        public const string OtherCity = "reallife_testrun_other_city";          // only in the New York the mod knows
        public const string NoSave = "reallife_testrun_no_save";                // the save that undoes the run could not be written: {why}
        public const string Started = "reallife_testrun_started";               // begun, the save that undoes it named
        public const string Load = "reallife_testrun_load";                     // Save and load now; the run carries on by itself
        public const string CarriedOn = "reallife_testrun_carried_on";          // the run carries on after the load
        public const string Done = "reallife_testrun_done";                     // {passed} of {ran} PASS; failed: {failed}
        public const string DoneAll = "reallife_testrun_done_all";              // {passed} of {ran} PASS, none failed
        public const string Stopped = "reallife_testrun_stopped";               // stopped, put back where it can be
        public const string LookUptown = "reallife_testrun_look_uptown";
        public const string LookWrap = "reallife_testrun_look_wrap";
        public const string LookPlot = "reallife_testrun_look_plot";
        public const string LookWalk = "reallife_testrun_look_walk";
        public const string LookBuilding = "reallife_testrun_look_building";

        /// <summary>The run's save in this character's saves: loaded, it undoes everything the run did.</summary>
        public const string StartSave = "RealLifeOverhaul test run start";
    }
}
