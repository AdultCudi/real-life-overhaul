using System.Collections.Generic;

namespace RealLifeOverhaul.Walking
{
    /// <summary>The options page's test of walking: says, as a notice, how the player is moving now. Nothing per frame.</summary>
    public sealed partial class WalkDirector
    {
        private const string TestName = "Walking";

        /// <summary>Options-page test: walking or not, and whether the game's walk is on the player now or the game's own pace for a moment.</summary>
        public void TestSay()
        {
            if (!Settings.WalkToggle || WalkGame.Loading())
            {
                TestRefusals.Tell(Logger, TestName, Settings.WalkToggle ? TestRefusal.NotLoaded : TestRefusal.FeatureOff);
                return;
            }
            var data = new Dictionary<string, string>(1) { { "key", Keys.Label(KeyAction.Walk) } };
            if (!_walking || _agent == null)
            {
                WalkGame.Notice(WalkText.TestRunning, data);
                return;
            }
            bool walks = WalkRules.Near(WalkGame.Speed(_agent), _walk);
            WalkGame.Notice(walks ? WalkText.TestWalking : WalkText.TestHeld, data);
            Check();
        }
    }
}
