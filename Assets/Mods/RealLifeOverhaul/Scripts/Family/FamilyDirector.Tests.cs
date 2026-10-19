using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>FamilyDirector's part for the options page's test buttons.</summary>
    public sealed partial class FamilyDirector
    {
        private bool TestReady(string test, bool outdoors, bool scene)
        {
            TestRefusal why = scene ? SceneRefusal(outdoors) : !ModState.Loaded || !FamilyState.Loaded || PlayerHelper.PlayerController == null ? TestRefusal.NotLoaded
                : _failed || !Settings.Family ? TestRefusal.FeatureOff : Busy ? TestRefusal.SceneActive : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why);
                return false;
            }
            return true;
        }

        private void Refuse(string test, string reason)
        {
            Logger?.Info(test + " test: not started (" + reason + ").");
            FamilyText.Toast(NotificationType.Warning, reason ?? "reallife_family_no_fault", Args.Us);
        }

        // Somebody to test with: without Social a partner is made up on the spot; with it, the tests need its partner.
        private bool TestPartner(string test)
        {
            Partner.Resync();
            if (FamilyState.Together)
            {
                return true;
            }
            if (Partner.UsesSocial)
            {
                Refuse(test, "reallife_family_no_partner_social");
                return false;
            }
            Partner.Meet(Settings.FamilyInterest);
            FamilyState.S.PAffection = 90f;
            FamilyState.S.PMetDay = TimeHelper.CurrentDay - 30;
            FamilyState.S.SinceDay = FamilyState.S.PMetDay;
            FamilyText.Toast(NotificationType.Info, "reallife_family_test_partner_made", Args.Us);
            return true;
        }

        public void TestMoveIn()
        {
            if (!TestReady("Family move-in", false, false) || !TestPartner("Family move-in"))
            {
                return;
            }
            Guarded(() =>
            {
                string why = HomeLife.MoveIn(true);
                if (why != null)
                {
                    Refuse("Family move-in", why);
                }
            });
        }

        // The cut back at work asked for now, whatever the player's cash: the text, then the talk in a family conversation.
        public void TestWork()
        {
            if (!TestReady("Family work", false, false) || !TestPartner("Family work"))
            {
                return;
            }
            Guarded(() =>
            {
                FamilySave s = FamilyState.S;
                if (!s.LivesIn)
                {
                    Refuse("Family work", "reallife_family_no_lives_in");
                    return;
                }
                if (!Settings.FamilyWork)
                {
                    Refuse("Family work", "reallife_family_no_work_off");
                    return;
                }
                PartnerWork.Reset(s);
                s.PWorkAsk = true;
                s.PWorkAskDay = TimeHelper.CurrentDay;
                FamilyText.Message(FamilyPhone.PartnerContact, "reallife_family_work_text", Args.Us);
                Logger?.Info("Family work test: " + Partner.Name + " asks to cut back to half days; talk it over from the Family contact.");
            });
        }

        public void TestPropose()
        {
            if (!TestReady("Family proposal", false, true) || !TestPartner("Family proposal"))
            {
                return;
            }
            Guarded(() =>
            {
                FamilySave s = FamilyState.S;
                if (FamilyState.Bond != Bond.Dating)
                {
                    Refuse("Family proposal", "reallife_family_no_already_engaged");
                    return;
                }
                if (s.Ring <= 0)
                {
                    s.Ring = 2;
                }
                s.LastProposalDay = 0;
                if (!StartScene(new ProposalScene(true), out string refusal))
                {
                    Refuse("Family proposal", refusal);
                }
            });
        }

        /// <summary>Opens the wedding plan, engaged at once if need be.</summary>
        public void TestPlanWedding()
        {
            if (!TestReady("Family wedding plan", false, false) || !TestPartner("Family wedding plan"))
            {
                return;
            }
            Guarded(() =>
            {
                TestEngage();
                FamilyTalk.Open(() => WeddingPlanner.Start(false));
            });
        }

        /// <summary>Holds the wedding here and now, free: no bill and no presents.</summary>
        public void TestWedding()
        {
            if (!TestReady("Family wedding", true, true) || !TestPartner("Family wedding"))
            {
                return;
            }
            Guarded(() =>
            {
                if (FamilyState.Bond == Bond.Married)
                {
                    Refuse("Family wedding", "reallife_family_no_already_married");
                    return;
                }
                TestEngage();
                WeddingPlan plan = FamilyState.S.Wedding;
                if (!plan.Active)
                {
                    WeddingPlanner.Quick(plan);
                }
                Vector3 here = PlayerHelper.GetPosition();
                plan.Venue = 1;
                plan.X = here.x;
                plan.Y = here.y;
                plan.Z = here.z;
                plan.Day = TimeHelper.CurrentDay;
                plan.TheirColdFeet = false;
                if (!StartScene(new WeddingScene { Free = true }, out string refusal))
                {
                    Refuse("Family wedding", refusal);
                }
            });
        }

        private static void TestEngage()
        {
            if (FamilyState.Bond == Bond.Dating)
            {
                if (FamilyState.S.Ring <= 0)
                {
                    FamilyState.S.Ring = 2;
                }
                Courtship.Engage(false);
            }
        }

        public void TestChild(bool adoption)
        {
            if (!TestReady("Family child", false, false) || !TestPartner("Family child"))
            {
                return;
            }
            Guarded(() =>
            {
                string why = Children.TestAdvance(adoption);
                if (why != null)
                {
                    Refuse("Family child", why);
                }
            });
        }

        public void TestYear()
        {
            if (!TestReady("Family year", false, false))
            {
                return;
            }
            Guarded(() =>
            {
                if (FamilyState.S.Children.Count == 0)
                {
                    Refuse("Family year", "reallife_family_no_children");
                    return;
                }
                Children.Grow(1f);
                FamilyText.Toast(NotificationType.Info, "reallife_family_test_year_done", Args.Us);
            });
        }

        public void TestWalk()
        {
            if (!TestReady("Family pushchair walk", true, true))
            {
                return;
            }
            Guarded(() =>
            {
                if (!StartScene(new WalkScene(true), out string refusal))
                {
                    Refuse("Family pushchair walk", refusal);
                }
            });
        }

        public void TestPage()
        {
            if (!TestReady("Family page", false, false))
            {
                return;
            }
            if (DialogController.current != null)
            {
                TestRefusals.Tell(Logger, "Family page", TestRefusal.SceneActive);
                return;
            }
            Guarded(() => FamilyTalk.Open(() => FamilyPage.Page(0)));
        }

        /// <summary>Nothing of the family is left, in memory or (from the next save on) in the save.</summary>
        public void TestClear()
        {
            if (!ModState.Loaded || !FamilyState.Loaded)
            {
                TestRefusals.Tell(Logger, "Family clear", TestRefusal.NotLoaded);
                return;
            }
            Guarded(() =>
            {
                Stop(false);
                FamilyModifiers.RemoveAll();
                FamilyState.Clear();
                Errands.Reset();
                Partner.Resync();
                _started = true;
                Logger?.Info("Family: all family state cleared by the test button.");
                FamilyText.Toast(NotificationType.Info, "reallife_family_test_cleared", Args.Us);
            });
        }
    }
}
