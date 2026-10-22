namespace RealLifeOverhaul.Meals
{
    /// <summary>Meals and music's test buttons: a seated meal, a home-cooked plate carried over and a dance, each started where the player stands, or a notice why not.</summary>
    public sealed partial class MealsDirector
    {
        private const string TestName = "Meals";
        private const float TestFill = 40f;          // of the game's 100: a meal's worth, for how long the test meal is eaten
        private const float TestSpeakerReach = 12f;  // m: the loudspeaker the dance test looks for

        /// <summary>As if the player had just eaten here: taken to the nearest free seat, seated with the food, up again. The hunger bar is not touched.</summary>
        public void TestSeated()
        {
            StartTest(false);
        }

        /// <summary>As if a home-cooked plate were ready where the player stands: carried to the nearest free seat and eaten there. Nothing is cooked or eaten.</summary>
        public void TestCooked()
        {
            StartTest(true);
        }

        private void StartTest(bool cooked)
        {
            if (cooked ? !Settings.MealsCooked : !Settings.MealsSeated)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.FeatureOff);
                return;
            }
            if (MealsGame.Loading())
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                return;
            }
            PlayerController player = MealsGame.Player();
            if (_step != Step.None || _dancing || !MealsGame.Free(player) || !MealsGame.HandsFit())
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.ScreenBusy, 0, "a meal or a dance is on, or the player is held, seated, in a vehicle or carrying something");
                return;
            }
            MealSeat seat = MealsGame.Find(player, out int looked);
            if (seat == null)
            {
                TestRefusals.TellKey(Logger, TestName, MealsText.NoSeat, looked + " seats looked at");
                return;
            }
            _hungerKnown = true;
            Begin(seat, cooked, TestFill, cooked ? "a test plate as if cooked at home" : "a test meal");
        }

        /// <summary>Dancing to the music of the nearest loudspeaker, as its radio panel's button does; or why not.</summary>
        public void TestDance()
        {
            if (!Settings.MealsDance)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.FeatureOff);
                return;
            }
            if (MealsGame.Loading())
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                return;
            }
            PlayerController player = MealsGame.Player();
            if (player == null || !MealsGame.Inside || !MealsGame.OwnPlace() || MealsGame.Speaker(player.transform.position, TestSpeakerReach) == null || !MealsGame.MusicPlaying())
            {
                TestRefusals.TellKey(Logger, TestName, MealsText.NoMusic, "no loudspeaker playing within " + TestSpeakerReach + " m in a place of the player's own");
                return;
            }
            DanceClicked();
        }
    }
}
