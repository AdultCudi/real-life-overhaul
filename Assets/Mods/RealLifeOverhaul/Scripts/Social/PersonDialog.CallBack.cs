using System.Collections.Generic;
using System.Globalization;
using Entities;
using Localizor;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A call back: the player rings somebody who texted them today or yesterday, and the call opens on what they wrote
    /// about (SocialFollowRules.Pick over their own thread in the game's phone, so every feature's texts count: the social
    /// life's, the family's, life's little moments' and travel's), with their line about it (SocialFollowCatalogue) and its
    /// two answers, or the player's typed words with a pack (PersonDialog.Composer), as the call's first topic: worth what
    /// any chat is, with the same limits (one chat a day, two topics a call; after the day's chat on an earlier call it is
    /// worth nothing more). It is spent once shown (SocialPerson.FollowedAt, in the save): hanging up or loading never plays
    /// it again. Not for a call they make, an invitation or favour still to answer, or a plan that is on: those keep their
    /// own way, before this. With Developer diagnostics on, one "Social check (call back)" line a call.
    /// COST: nothing per frame; on a call, a pass over one thread of twenty messages at most.
    /// </summary>
    public sealed partial class PersonDialog
    {
        private static readonly List<SocialThreadLine> Thread = new List<SocialThreadLine>(Contact.MaxMessages);

        // The call's first entry when it opens on their text; null for the ordinary greeting.
        private DialogEntry CallBack()
        {
            if (_testTopic >= 0)
            {
                return null;   // (a test asked for a topic of its own: SocialDirector.TestComposer)
            }
            SocialPhone.Thread(_p, Thread);
            bool found = SocialFollowRules.Pick(Thread, _p.FollowedAt, SocialLife.Day, out SocialFollowPick pick, out string why);
            Thread.Clear();
            if (!found)
            {
                if (Meta.Dev.Verbose)
                {
                    SocialDirector.Logger?.Info("Social check (call back): " + _p.Name + "'s call opens as it always did: " + why + ": PASS");
                }
                return null;
            }
            _p.FollowedAt = pick.Minute;   // (spent now: never again, even after a load)
            SocialTopic topic = pick.Subject.Talk;
            Dictionary<string, string> data = SocialFollowRules.Filled(pick.Data, SocialLife.Data(_p));
            DialogEntry entry = Line(topic.Open[0], data);
            string shown;
            if (SocialRules.ChatHad(_topics, _p.LastCallDay, SocialLife.Day))
            {
                // Today's chat was had on an earlier call: they say it, the player answers, and that is worth nothing more.
                entry.ConfirmTextOverride = topic.ReplyA.Localize(data);
                entry.OnConfirm = () => Heard(topic, true, data);
                entry.SecondOptionTextOverride = topic.ReplyB;
                entry.OnSecondOption = () => Heard(topic, false, data);
                shown = "its two answers, worth nothing more (today's chat was had on an earlier call)";
            }
            else if (Composing(topic, data, entry))
            {
                shown = "typed (worth the warm answer, as the call's first topic)";
            }
            else
            {
                entry.ConfirmTextOverride = topic.ReplyA.Localize(data);
                entry.OnConfirm = () => Reply(topic, true, data);
                entry.SecondOptionTextOverride = topic.ReplyB;
                entry.OnSecondOption = () => Reply(topic, false, data);
                shown = "its two answers, as the call's first topic";
            }
            if (Meta.Dev.Verbose)
            {
                int minute = pick.Minute % (60 * 24);
                SocialDirector.Logger?.Info("Social check (call back): " + _p.Name + "'s call opens on their text of day " + pick.Day + " at "
                                            + (minute / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (minute % 60).ToString("00", CultureInfo.InvariantCulture)
                                            + " (" + pick.Key + ", from " + (pick.Source ?? "another feature") + "), today day " + SocialLife.Day + ": "
                                            + pick.Subject.Id + " (" + pick.Subject.Topic + "), " + topic.Open[0] + ", " + shown + ": PASS");
            }
            return entry;
        }

        // An answer to a call back after the day's chat: their reaction, worth nothing; the call goes on as after any topic.
        private DialogEntry Heard(SocialTopic topic, bool warm, Dictionary<string, string> data)
        {
            SocialLife.Contacted(_p);
            return Onward(Line(warm ? topic.ReactA : topic.ReactB, data));
        }
    }
}
