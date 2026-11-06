using System.Collections.Generic;
using Entities;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The social director's test buttons for a call back and the day after an outing (Options, Tests). Each says why when it
    /// cannot do what it was asked, and records nothing beyond what the text and the call themselves do.
    /// </summary>
    public sealed partial class SocialDirector
    {
        private int _followTurn, _nextDayTurn;

        // A text of each feature that texts the people of the social life, in turn: the social life's, the family's, a little
        // moment of life's, travel's. The family's, life's and travel's go to Mom, the old friend or the partner as each does.
        private static string FollowTestKey(SocialPerson p, int turn, out string source)
        {
            switch (turn % 4)
            {
                case 1:
                    source = "the family";
                    if (p.IsFamily)
                    {
                        return "reallife_family_mom_visit_1";
                    }
                    if (p.Together)
                    {
                        return "reallife_family_neglect_2";
                    }
                    break;
                case 2:
                    source = "life's little moments";
                    if (p.IsFamily)
                    {
                        return "reallife_ev_fm02";
                    }
                    if (p.Origin == SocialOrigin.OldFriend)
                    {
                        return "reallife_ev_fr02";
                    }
                    break;
                case 3:
                    source = "travel";
                    if (p.IsFamily)
                    {
                        return "reallife_travel_text_mom_souvenir";
                    }
                    if (p.Origin == SocialOrigin.OldFriend)
                    {
                        return "reallife_travel_burnout_friend_1";
                    }
                    break;
            }
            source = "the social life";
            return p.Mood <= -1 ? "reallife_social_text_badday_1" : "reallife_social_text_news_1";
        }

        /// <summary>
        /// The chosen person (or the one known best) texts now, the way each feature's text lands in their thread, in turn the
        /// social life's, the family's, life's and travel's (where that feature texts this person; the social life's
        /// otherwise), and the call to them opens at once: it should open on what they wrote about.
        /// </summary>
        public void TestFollowUp()
        {
            const string test = "A text, then a call back";
            if (!TestReady(test, false))
            {
                return;
            }
            if (SocialPhone.DialogOpen || DialogController.current != null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.ScreenBusy, 0, "a conversation is open");
                return;
            }
            SocialPerson person = TestTarget(true);
            Contact contact = person != null ? SocialPhone.ContactOf(person) : null;
            if (person == null || contact == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NobodyKnown);
                return;
            }
            string key = FollowTestKey(person, _followTurn++, out string source);
            var message = new TextMessage(key, SocialLife.Data(person));
            int minute = (int)message.timestamp.GetTotalMinutes();
            person.FollowedAt = System.Math.Min(person.FollowedAt, minute - 1);   // (a test pressed twice in a paused minute is still new)
            contact.SendMessage(message, notify: false);
            SocialLife.Said(person, key, SocialLife.Data(person));
            bool other = (SocialState.Offer.Active && SocialState.Offer.Person == person.Index) || (SocialState.Plan.Active && SocialState.Plan.Person == person.Index);
            bool opened = SocialPhone.Call(person);
            Logger?.Info("Social check (call back test): " + person.Name + " texts " + key + " (" + source + ") and the call "
                         + (!opened ? "could not open: FAIL, the phone could not open the call"
                             : other ? "opens on their invitation, favour or plan first, as it must: PASS"
                             : "opens: it should open on " + (SocialFollowCatalogue.Of(key)?.Id ?? "nothing") + ": PASS"));
            if (!opened)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.ScreenBusy, 0, "the phone could not open the call");
            }
            else if (other)
            {
                SocialLife.Toast(NotificationType.Info, "reallife_social_test_followup_other", SocialLife.Data(person), 9f);
            }
        }

        /// <summary>
        /// The chosen person (or the one known best) texts now about yesterday's outing, as on the day after one, in turn as if
        /// it went well and as if it went badly: Mom's, the partner's after a date or a friend's. Whatever the day's cap; the
        /// switch still holds. No outing is recorded.
        /// </summary>
        public void TestNextDay()
        {
            const string test = "The day after an outing";
            if (!TestReady(test, false))
            {
                return;
            }
            SocialPerson person = TestTarget(true);
            if (person == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NobodyKnown);
                return;
            }
            if (!Settings.SocialTexts)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff, 0, "text messages are switched off");
                return;
            }
            bool well = _nextDayTurn++ % 2 == 0;
            Dictionary<string, string> data = SocialLife.Data(person);
            data["t_place"] = string.Empty;   // (no outing: RLO Texts' thanks card needs one, so a test is the written line)
            data["t_kind"] = PlanKind.Coffee.ToString();
            string key = SocialFollowRules.Line(SocialFollowRules.NextDayLines(person.IsFamily, person.Together, well), SocialLife.Day + _nextDayTurn / 2, person.Seed);
            bool sent = SocialLife.SendText(person, key, data, true);
            Logger?.Info("Social check (the day after test): " + person.Name + (well ? " after a good one" : " after a bad one") + ": " + key + (sent ? ": PASS" : ": FAIL, it could not be sent"));
        }
    }
}
