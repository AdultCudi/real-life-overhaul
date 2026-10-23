namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// What the city expansion's test run (Scripts/TestRun) asks of the test building besides its buttons: asked for at the
    /// next load on the airport's open grass alone (never round where the player stands, which would put it on ground the
    /// run gives its other checks, or on nothing free), no longer asked for at the run's end when the run asked, and the
    /// made buildings shown again once the switch-off test's next hour is checked. Each is the button's own work with the
    /// choice made for it; nothing runs a frame. COST: on the run's call only.
    /// </summary>
    public sealed partial class NewBuildingsDirector
    {
        /// <summary>The test building asked for at the next load on the airport's grass; true when the run asked it (it was not asked for already).</summary>
        public static bool TestAskOnOpenGround()
        {
            bool already = TestAsked;
            UnityEngine.PlayerPrefs.SetInt(TestPref, 1);
            UnityEngine.PlayerPrefs.SetString(TestHerePref, string.Empty);
            UnityEngine.PlayerPrefs.Save();
            string address = MadeGame.Written(NewBuildingsTestPlot.Street, NewBuildingsTestPlot.Number);
            Logger?.Info("New buildings test: the test building is made at " + address + " from the next load, on the open ground by the airport (asked by the test run"
                         + (already ? ", which found it asked for already" : string.Empty) + ").");
            return !already;
        }

        /// <summary>The test building no longer asked for from the next load (it stands in this city until then).</summary>
        public static void TestNoLongerAsked()
        {
            UnityEngine.PlayerPrefs.SetInt(TestPref, 0);
            UnityEngine.PlayerPrefs.SetString(TestHerePref, string.Empty);
            UnityEngine.PlayerPrefs.Save();
            Logger?.Info("New buildings test: the test building is no longer made from the next load.");
        }

        /// <summary>Every made building the switch-off test hid shown again, as the option switched on shows them; false when none was held hidden.</summary>
        public bool TestShowAgain()
        {
            if (!_heldHidden || !Settings.NewBuildings)
            {
                return false;
            }
            _heldHidden = false;
            Apply("the switch-off test is over");
            return true;
        }
    }
}
