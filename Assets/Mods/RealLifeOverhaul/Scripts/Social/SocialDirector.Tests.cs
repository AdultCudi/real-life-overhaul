using System.Collections.Generic;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The social director's test buttons (Options, Tests). Each says why when it cannot do what it was asked.</summary>
    public sealed partial class SocialDirector
    {
        private int _favourTurn;

        private bool TestReady(string test, bool street)
        {
            TestRefusal why = !SocialState.Loaded ? TestRefusal.NotLoaded
                : _broken || !Settings.Social ? TestRefusal.FeatureOff
                : street ? TestRefusals.Street()
                : TestRefusal.None;
            if (why == TestRefusal.None && street && _scene != null && !_scene.Waiting)
            {
                why = TestRefusal.SceneActive;
            }
            if (why == TestRefusal.None && street && JobDirector.OnShift)
            {
                why = TestRefusal.OnJob;
            }
            if (why == TestRefusal.None && street && (GameMembers.PlayerDead || EnergyHelper.goingToHospital))
            {
                why = TestRefusal.ScreenBusy;
            }
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why);
                return false;
            }
            if (street)
            {
                EndScene(SceneEnd.Left, SceneBreak.TestButton);   // a plan that was only showing the way gives the street up to the test
                RefreshContext(InstanceBehavior<GameManager>.Instance, Time.time);
            }
            return true;
        }

        // The chosen person, or failing that the one known best (Mom if nobody else).
        private static SocialPerson TestTarget(bool mustBeMet)
        {
            SocialPerson chosen = SocialState.Get(SocialState.TestPerson);
            if (chosen != null && (chosen.Met || !mustBeMet))
            {
                return chosen;
            }
            SocialPerson best = null;
            for (int i = 0; i < SocialState.Count; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p != null && p.Met && !p.IsFamily && (best == null || p.Closeness > best.Closeness))
                {
                    best = p;
                }
            }
            return best ?? (mustBeMet ? SocialState.ByOrigin(SocialOrigin.Family) : chosen);
        }

        public void TestMeet()
        {
            if (!TestReady("Meet somebody", true))
            {
                return;
            }
            SocialPerson stranger = NextStranger();
            if (stranger == null)
            {
                TestRefusals.Tell(Logger, "Meet somebody", TestRefusal.AllMet);
                return;
            }
            var scene = new MeetScene(MeetScene.Kind.Stranger, stranger);
            if (!Begin(scene))
            {
                TestRefusals.Tell(Logger, "Meet somebody", scene.Refusal, 0, scene.RefusalDetail);
            }
        }

        /// <summary>
        /// The old friend (Mom if the old friend is not known) brings the next person to be met over, here and now, as an
        /// introduction does (for the networking life's test). The person's index, or -1 when it cannot start, said why.
        /// </summary>
        public int TestIntroduction()
        {
            const string test = "An introduction";
            if (!TestReady(test, true))
            {
                return -1;
            }
            SocialPerson friend = SocialState.ByOrigin(SocialOrigin.OldFriend);
            if (friend == null || !friend.Met)
            {
                friend = SocialState.ByOrigin(SocialOrigin.Family);
            }
            SocialPerson stranger = NextStranger();
            if (stranger == null || friend == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.AllMet);
                return -1;
            }
            var scene = new MeetScene(MeetScene.Kind.Introduction, stranger, friend);
            if (!Begin(scene))
            {
                TestRefusals.Tell(Logger, test, scene.Refusal, 0, scene.RefusalDetail);
                return -1;
            }
            return stranger.Index;
        }

        /// <summary>At the player's own shop if one is within 25 m.</summary>
        public void TestDropBy()
        {
            if (!TestReady("Friend drops by", true))
            {
                return;
            }
            SocialPerson friend = TestTarget(true);
            if (friend == null)
            {
                TestRefusals.Tell(Logger, "Friend drops by", TestRefusal.NobodyKnown);
                return;
            }
            var scene = new MeetScene(MeetScene.Kind.DropBy, friend, null, SocialPlaces.Find(PlanKind.Business, Where(), 0f, 25f, out _));
            if (!Begin(scene))
            {
                TestRefusals.Tell(Logger, "Friend drops by", scene.Refusal, 0, scene.RefusalDetail);
            }
        }

        /// <summary>The phone rings once the options screen is closed.</summary>
        public void TestCall()
        {
            if (!TestReady("Friend calls", false))
            {
                return;
            }
            SocialPerson friend = TestTarget(true);
            if (friend == null)
            {
                TestRefusals.Tell(Logger, "Friend calls", TestRefusal.NobodyKnown);
                return;
            }
            _callWhenFree = friend;
            _callUntil = Time.unscaledTime + 60f;
            SocialLife.Toast(NotificationType.Info, "reallife_social_test_call_soon", SocialLife.Data(friend), 6f);
        }

        /// <summary>
        /// A call opened now with somebody whose chat the player types (the chosen person first, then anybody else met whose
        /// chat today is still to come), its first topic the shop's loss, whose answer the reading turns on (good news gets a
        /// glad line, bad news the sorry one); says why not: the option off, no pack running, nobody it can be. Records
        /// nothing beyond what the call itself does.
        /// </summary>
        public void TestComposer()
        {
            const string test = "A call with typing";
            if (!TestReady(test, false))
            {
                return;
            }
            if (!Settings.SocialComposer)
            {
                TestRefusals.TellKey(Logger, test, "reallife_social_test_composer_off");
                return;
            }
            if (SocialPhone.DialogOpen || DialogController.current != null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.ScreenBusy, 0, "a conversation is open");
                return;
            }
            SocialPerson person = null;
            string noPack = null;
            bool chatted = false;
            for (int i = -1; i < SocialState.Count && person == null; i++)
            {
                SocialPerson p = SocialState.Get(i < 0 ? SocialState.TestPerson : i);
                if (p == null || !p.Met || p.IsFamily)
                {
                    continue;
                }
                string textsWhy = SocialLinks.ComposerWhyNot != null ? SocialLinks.ComposerWhyNot(p) : "RLO Texts is not running";
                string why = SocialComposerRules.Why(Settings.Social, true, TextBox.CanType, textsWhy);
                if (why != null)
                {
                    noPack = noPack ?? why;
                }
                else if (p.LastCallDay == SocialLife.Day)
                {
                    chatted = true;
                }
                else
                {
                    person = p;
                }
            }
            if (person == null)
            {
                TestRefusals.TellKey(Logger, test, noPack != null ? "reallife_social_test_composer_nopack" : "reallife_social_test_composer_nobody",
                    noPack ?? (chatted ? "everybody it could be has had today's chat" : "nobody but Mom is known, and no pack answers her yet"));
                return;
            }
            PersonDialog.TestTopic = SocialCatalogue.TopicShopBad;
            PersonDialog.TestTopicFor = person.Index;
            bool opened = SocialPhone.Call(person);
            if (!opened)
            {
                PersonDialog.TestTopic = PersonDialog.TestTopicFor = -1;
            }
            Logger?.Info("Social check (composer test): a call with " + person.Name
                         + (opened ? " opened; press Chat: they ask how the shop is holding up; type good or bad news: PASS" : ": FAIL, the phone could not open the call"));
            if (!opened)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.ScreenBusy, 0, "the phone could not open the call");
            }
        }

        /// <summary>Whatever the day's cap; the switch still holds.</summary>
        public void TestText()
        {
            if (!TestReady("Friend texts", false))
            {
                return;
            }
            SocialPerson friend = TestTarget(true);
            if (friend == null)
            {
                TestRefusals.Tell(Logger, "Friend texts", TestRefusal.NobodyKnown);
                return;
            }
            if (!Settings.SocialTexts)
            {
                TestRefusals.Tell(Logger, "Friend texts", TestRefusal.FeatureOff, 0, "text messages are switched off");
                return;
            }
            SocialLife.SendText(friend, SocialLines.Pick(friend.Mood <= -1 ? SocialCatalogue.TextBadDay : SocialCatalogue.TextNews), SocialLife.Data(friend), true);
        }

        /// <summary>A lift, a loan and help moving in turn; the player calls them to answer.</summary>
        public void TestFavour()
        {
            if (!TestReady("Favour asked", false))
            {
                return;
            }
            SocialPerson friend = TestTarget(true);
            if (friend == null)
            {
                TestRefusals.Tell(Logger, "Favour asked", TestRefusal.NobodyKnown);
                return;
            }
            if (SocialState.Plan.Active)
            {
                TestRefusals.Tell(Logger, "Favour asked", TestRefusal.SceneActive, 0, "a plan is already on");
                return;
            }
            AskFavour(friend, Time.unscaledTime + 1000f, _favourTurn++ % 3);   // never rings: the options screen is open
        }

        /// <summary>The test buttons act on the next person.</summary>
        public void TestPerson()
        {
            if (!TestReady("Choose person", false))
            {
                return;
            }
            SocialState.TestPerson = (SocialState.TestPerson + 1) % Mathf.Max(1, SocialState.Count);
            TellPerson(SocialState.Get(SocialState.TestPerson));
        }

        private static void TellPerson(SocialPerson person)
        {
            if (person == null)
            {
                return;
            }
            Dictionary<string, string> data = SocialLife.Data(person, "stage", person.Met ? SocialLife.StageName(person) : SocialLife.T(SocialCatalogue.StageNames[0]));
            data["f"] = Mathf.RoundToInt(person.Familiarity).ToString();
            data["a"] = Mathf.RoundToInt(person.Affection).ToString();
            data["t"] = Mathf.RoundToInt(person.Trust).ToString();
            SocialLife.Toast(NotificationType.Info, "reallife_social_test_person_now", data, 9f);
        }

        /// <summary>Met, friend, close friend, dating, partner, living together.</summary>
        public void TestAdvance()
        {
            if (!TestReady("Advance relationship", false))
            {
                return;
            }
            SocialPerson p = TestTarget(false);
            if (p == null)
            {
                TestRefusals.Tell(Logger, "Advance relationship", TestRefusal.NobodyKnown);
                return;
            }
            int day = SocialLife.Day;
            if (!p.Met)
            {
                SocialLife.Meet(p, true);
            }
            else if (p.Stage == SocialStage.Acquaintance)
            {
                p.Drifted = false;
                p.Shared = Mathf.Max(p.Shared, SocialRules.FriendShared);
                Raise(p, SocialRules.FriendFamiliarity + 5f, SocialRules.FriendAffection + 5f, SocialRules.FriendTrust + 5f);
            }
            else if (p.Stage == SocialStage.Friend)
            {
                p.Shared = Mathf.Max(p.Shared, SocialRules.CloseShared);
                p.MetDay = Mathf.Min(p.MetDay, day - SocialRules.CloseDays);
                Raise(p, SocialRules.CloseFamiliarity + 5f, SocialRules.CloseAffection + 5f, SocialRules.CloseTrust + 5f);
            }
            else if (p.Stage == SocialStage.CloseFriend && p.Compatible && SocialState.Partner == null)
            {
                Raise(p, p.Familiarity, SocialRules.DateAffection + 5f, SocialRules.DateTrust + 5f);
                SocialLife.SetStage(p, SocialStage.Dating);
            }
            else if (p.Stage == SocialStage.Dating)
            {
                Raise(p, p.Familiarity, SocialRules.PartnerAffection + 5f, SocialRules.PartnerTrust + 5f);
                SocialLife.SetStage(p, SocialStage.Partner);
            }
            else if (p.Stage == SocialStage.Partner && !p.MovedIn)
            {
                p.MovedIn = true;
                SocialLife.Remember(p, MemoryCode.MovedIn);
            }
            else
            {
                TestRefusals.Tell(Logger, "Advance relationship", TestRefusal.NothingFits, 0, p.Name + " is as close as they can be");
                return;
            }
            p.LastContactDay = day;
            TellPerson(p);
        }

        private static void Raise(SocialPerson p, float familiarity, float affection, float trust)
        {
            p.Familiarity = Mathf.Max(p.Familiarity, familiarity);
            p.Affection = Mathf.Max(p.Affection, affection);
            p.Trust = Mathf.Max(p.Trust, trust);
            SocialLife.Restage(p);
        }

        public void TestOuting(PlanKind kind)
        {
            const string test = "Outing";
            if (!TestReady(test, true))
            {
                return;
            }
            if (!Settings.SocialOutings)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return;
            }
            SocialPerson person = TestTarget(true);
            if (person == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NobodyKnown);
                return;
            }
            Vector3 here = Where();
            if ((kind == PlanKind.Drive || kind == PlanKind.Lift) && JobDirector.NearestCar(150f) == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NoCar, 150);
                return;
            }
            if (kind == PlanKind.Moving && (!ClipLibrary.Has("carry_box_hold") || !PropLibrary.Ready))
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NothingFits, 0, "no clip or prop to carry a box with");
                return;
            }
            // A walk needs no business: "here" is the pavement where the player stands, if any.
            Venue venue = default;
            if (!(kind == PlanKind.Walk && SocialPlaces.Here(here, out venue)) && !SocialPlaces.Find(kind, here, 3f, 700f, out venue))
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NoVenue, 700, kind.ToString());
                return;
            }
            Venue to = default;
            if ((kind == PlanKind.Lift && !SocialPlaces.Find(kind, venue.Front, 250f, 1200f, out to, venue.Building))
                || (kind == PlanKind.Moving && !SocialPlaces.Find(kind, venue.Front, 22f, 90f, out to, venue.Building)))
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NoVenue, 1200, "nowhere to go to from " + venue.Name);
                return;
            }
            float nowMinutes = SocialLife.Now;
            var scene = new OutingScene(person, kind, venue, nowMinutes, nowMinutes - 1f, false, to, true);
            if (!Begin(scene))
            {
                TestRefusals.Tell(Logger, test, scene.Refusal, 0, scene.RefusalDetail);
                return;
            }
            Logger?.Info("Social: test outing " + kind + " with " + person.Name + " at " + venue.Name + ", " + JobSpots.Flat(here, venue.Front).ToString("F0") + " m away.");
        }

        /// <summary>A walk from the pavement where the player stands, so no business is needed. Nothing is recorded.</summary>
        public void TestOutingWithMom()
        {
            const string test = "Outing with Mom";
            if (!TestReady(test, true))
            {
                return;
            }
            if (!Settings.SocialOutings)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return;
            }
            SocialPerson mom = SocialState.ByOrigin(SocialOrigin.Family);
            if (mom == null || !mom.Met)
            {
                mom = TestTarget(true);
            }
            if (mom == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NobodyKnown);
                return;
            }
            Vector3 here = Where();
            if (!SocialPlaces.Here(here, out Venue venue))
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NoPavement);
                return;
            }
            float nowMinutes = SocialLife.Now;
            var scene = new OutingScene(mom, PlanKind.Walk, venue, nowMinutes, nowMinutes - 1f, false, default, true);
            if (!Begin(scene))
            {
                TestRefusals.Tell(Logger, test, scene.Refusal, 0, scene.RefusalDetail);
                return;
            }
            Logger?.Info("Social: test outing with " + mom.Name + ", here: a walk from the pavement " + JobSpots.Flat(here, venue.Front).ToString("F0") + " m away ("
                         + venue.Name + "); the player is " + (OnFoot ? "on foot" : "in a vehicle") + ".");
        }

        /// <summary>Today's paper written again, each piece to the log; the City Ledger's half of the button prints it.</summary>
        public void TestPaper()
        {
            if (!SocialState.Loaded || SaveGameManager.Current == null)
            {
                TestRefusals.Tell(Logger, "Print the paper", TestRefusal.NotLoaded);
                return;
            }
            if (!Settings.SocialNewspaper)
            {
                TestRefusals.Tell(Logger, "Print the paper", TestRefusal.FeatureOff);
                return;
            }
            News.Compose(SaveGameManager.Current.Day, true);
            for (int i = 0; i < News.Today.Count; i++)
            {
                Logger?.Info("Ledger: " + SocialLife.T(News.Today[i].Key, News.Today[i].Data));
            }
        }

        /// <summary>The plan, if any, is dropped without blame.</summary>
        public void TestStop()
        {
            if (!SocialState.Loaded)
            {
                TestRefusals.Tell(Logger, "Stop", TestRefusal.NotLoaded);
                return;
            }
            EndScene(SceneEnd.Interrupted, SceneBreak.TestButton);
            _ringing = false;
            HideUi();
            _pendingMeet = null;
            _pendingDropBy = null;
            _introStranger = null;
            _callWhenFree = null;
            SocialState.Plan.Clear();
            SocialState.Offer.Clear();
        }
    }
}
