using System.Collections;
using UI.Notification;

namespace RealLifeOverhaul.Permadeath
{
    public sealed partial class PermadeathDirector
    {
        // ---------------------------------------------------------------- the owner's test buttons

        private bool Ready(string test)
        {
            if (!ModState.Loaded || !PermadeathState.Loaded || PermadeathGame.Save == null || !ReferenceEquals(PermadeathGame.Save, _save))
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (_ending || _broken)
            {
                TestRefusals.TellKey(Logger, test, "reallife_pd_test_busy", _broken ? "the feature is off after an error" : "an obituary is under way");
                return false;
            }
            return true;
        }

        /// <summary>Test button: the obituary of this character as it would be printed now, on the City Ledger's page. Nothing ends; the switch need not be on.</summary>
        public void TestObituary()
        {
            const string Test = "The obituary";
            if (!Ready(Test))
            {
                return;
            }
            Gather(_story, DeathCause.Test);
            Logger?.Info(Test + " test: " + _story.Write());
            StartCoroutine(Reading());
        }

        private IEnumerator Reading()
        {
            _ending = true;
            yield return Print(_story, true);
            _ending = false;
        }

        /// <summary>
        /// Test button: a death rehearsed from start to end (the fade, the obituary) with nothing saved and the character
        /// alive afterwards: the game's own "the player is dead" is not used, only walking is held.
        /// </summary>
        public void TestRehearsal()
        {
            const string Test = "A death rehearsed";
            if (!Ready(Test))
            {
                return;
            }
            Logger?.Info(Test + " test: the fade, the obituary of a test, then back to the game; nothing is saved.");
            StartCoroutine(Dying(DeathCause.Test, false));
        }

        /// <summary>
        /// Test button: the character dies now, for real, exactly as from a danger (the save is written as ended, the
        /// obituary, the main menu), after the game's own red question. Only in a save where Permadeath is on, never in a
        /// session: for proving the whole of it in a save nobody needs.
        /// </summary>
        public void TestDie()
        {
            const string Test = "Die for real";
            if (!Ready(Test))
            {
                return;
            }
            if (!PermadeathState.Armed || PermadeathState.Dead)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_pd_test_needs_on");
                return;
            }
            if (GoingPublic.InSession)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_pd_refused_session");
                return;
            }
            GameConfirm.Show("reallife_pd_test_die_header", "reallife_pd_test_die_body", () =>
            {
                if (PermadeathState.Armed && !PermadeathState.Dead && !GoingPublic.InSession && !_ending)
                {
                    Logger?.Info(Test + " test: confirmed; the character dies now.");
                    Die(DeathCause.None);
                }
            }, null, "reallife_pd_test_die_yes", "reallife_meta_cancel", allowConfirmationSkip: false, isNegative: true);
        }

        /// <summary>
        /// Test button: a flu that began two days ago and has had no treatment, so that the first warning comes within the
        /// game hour (and, with the game's clock run on, the second and third a day apart each).
        /// </summary>
        public void TestFlu()
        {
            const string Test = "A flu left untreated";
            if (!Ready(Test))
            {
                return;
            }
            if (!Settings.HomeLife || !Settings.HomeIllness)
            {
                TestRefusals.Tell(Logger, Test, TestRefusal.FeatureOff);
                return;
            }
            bool given = PermadeathBridges.Flu != null && PermadeathBridges.Flu(PermadeathRules.IllWarnFirst - 0.5f);
            if (!given)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_pd_test_flu_no");
                return;
            }
            Notifications.Show(NotificationType.Info, PermadeathState.Armed ? "reallife_pd_test_flu_done" : "reallife_pd_test_flu_off", null, 12f);
            Logger?.Info(Test + " test: a flu begun " + (PermadeathRules.IllWarnFirst - 0.5f) + " hours ago, untreated; Permadeath " + (PermadeathState.Armed ? "on: the first warning comes on the hour." : "off: it takes its usual course."));
        }

        /// <summary>
        /// Test button: the hunger bar at its bottom, as if for half an hour short of a day, so that the first warning of
        /// going without food comes within the game hour (and, with the clock run on, the others a day apart). Only with
        /// Permadeath on in this save: off, the bar is simply what it always was.
        /// </summary>
        public void TestStarve()
        {
            const string Test = "Going without food";
            if (!Ready(Test))
            {
                return;
            }
            if (!PermadeathState.Armed || PermadeathState.Dead)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_pd_test_needs_on");
                return;
            }
            if (PermadeathGame.NoEnergy)
            {
                TestRefusals.Tell(Logger, Test, TestRefusal.FeatureOff);
                return;
            }
            StarvedFor(PermadeathRules.StarveWarnFirst - 0.5f);
            Notifications.Show(NotificationType.Info, "reallife_pd_test_starve_done", null, 12f);
            Logger?.Info(Test + " test: the bar at its bottom since " + (PermadeathRules.StarveWarnFirst - 0.5f) + " hours ago; the first warning comes on the hour.");
        }

        /// <summary>Test button: the notice a near miss shows (a crash's), and nothing more: nothing is remembered as warned.</summary>
        public void TestNearMiss()
        {
            const string Test = "A near miss";
            if (!Ready(Test))
            {
                return;
            }
            Notifications.Show(NotificationType.Warning, "reallife_pd_near_crash", null, 16f);
            Logger?.Info(Test + " test: the crash's near-miss notice shown; nothing is remembered.");
        }
    }
}
