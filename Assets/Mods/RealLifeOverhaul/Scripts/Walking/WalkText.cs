namespace RealLifeOverhaul.Walking
{
    /// <summary>The locale keys of walking, written out whole so that the text checks find them. Read only when something is shown.</summary>
    public static class WalkText
    {
        public const string On = "reallife_walk_on";                     // a press: walking ({key} is the walk key)
        public const string Off = "reallife_walk_off";                   // a press again: the game's own pace
        public const string TestWalking = "reallife_walk_test_walking";  // the test: walking, at the game's walk
        public const string TestHeld = "reallife_walk_test_held";        // the test: walking, the game's own pace for now
        public const string TestRunning = "reallife_walk_test_running";  // the test: not walking ({key} is the walk key)
    }
}
