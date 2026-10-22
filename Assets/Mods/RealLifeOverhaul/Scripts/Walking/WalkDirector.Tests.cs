using System.Collections.Generic;

namespace RealLifeOverhaul.Walking
{
    /// <summary>Walking's test button: a notice of how the player moves now.</summary>
    public sealed partial class WalkDirector
    {
        private const string TestName = "Walking";

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
