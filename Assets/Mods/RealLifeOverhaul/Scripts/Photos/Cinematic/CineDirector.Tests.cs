using UI.Notification;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The cinematic shots' two buttons in Options, Tests: the whole set in four lights, and the set in the light of the
    /// hour there is. Each waits for the menu to close, then runs. On a press only.
    /// </summary>
    public sealed partial class CineDirector
    {
        private const string TestName = "Cinematic shots";

        /// <summary>"Cinematic shots: take the set now": every shot, each in its own light of the day.</summary>
        public void TestSet()
        {
            Ask(true);
        }

        /// <summary>"Cinematic shots: at this hour only": the light is left as the game's clock has it.</summary>
        public void TestHour()
        {
            Ask(false);
        }

        private void Ask(bool allLights)
        {
            if (FrameGuard.Off)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.FeatureOff);
                return;
            }
            if (Active || _armed)
            {
                PhotosGame.Tell(NotificationType.Warning, "reallife_cine_running");
                return;
            }
            if (GoingPublic.InSession)
            {
                RefuseSession();
                return;
            }
            TestRefusal why = CineGame.Refuse();
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, TestName, why);
                return;
            }
            _armed = true;
            PhotosGame.Tell(NotificationType.Info, "reallife_cine_armed");
            StartCoroutine(Guard.Routine(Run(allLights)));
        }

        // The camera and the light are every player's in a session: nothing is started.
        private void RefuseSession()
        {
            Logger?.Info(TestName + " test: not started (a Going Public session: the tool moves the camera and shows other hours' light).");
            PhotosGame.Tell(NotificationType.Warning, "reallife_cine_refuse_session");
        }
    }
}
