using System.Collections.Generic;
using Dialogs;
using Entities;
using Localizor;

namespace RealLifeOverhaul
{
    /// <summary>What a face-to-face conversation is about, handed to the dialog by the scene, and what came of it.</summary>
    public sealed class SceneTalk
    {
        public const int Meet = 0, Outing = 1, DropBy = 2, Introduction = 3;   // Mode
        public const int Treat = 1, Split = 2, Theirs = 3;                     // Pay; 0: not answered

        public SocialPerson Person;
        public SocialPerson Introducer;
        public SocialPerson Via;        // who brought them along, here or not (an introduction); null for nobody
        public int Mode;
        public string Opener;
        public Dictionary<string, string> OpenerData;
        public int Cost;                // per head; 0 = nothing to pay
        public bool TheyOffer;          // they would pay for both
        public string GoLabel;          // what the button that gets on with it says

        public bool PersonSpeaks;       // an introduction: the friend has had their say and the new person is talking
        public bool Warm, Swapped, Brushed, Chatted;
        public int Pay;
    }

    /// <summary>A conversation in the street, in the game's dialogue panel; the scene reads <see cref="SceneTalk"/> after.</summary>
    public sealed class SceneDialog : Dialog
    {
        private readonly SceneTalk _talk;
        private readonly SocialPerson _p;

        public SceneDialog()
        {
            _talk = SocialDirector.Talk;
            _p = _talk != null ? _talk.Person : null;
            if (_p == null)
            {
                DialogController.current.ShowEntry(Guard.Entry(PhoneContacts.Closing("reallife_social_call_off")));
                return;
            }
            npcNameKey = SocialPhone.IdOf(_p);
            DialogController.current.ShowEntry(Guard.Entry(_talk.Mode == SceneTalk.Introduction ? Introduced() : _talk.Mode == SceneTalk.Meet ? MeetFirst() : Greeting()));
        }

        private static void Leave()
        {
            DialogController.current.CancelDialog();
        }

        private static DialogEntry Done()
        {
            DialogController.current.FinishDialog();
            return null;
        }

        private DialogEntry Say(string key, Dictionary<string, string> data = null)
        {
            data = data ?? SocialLife.Data(_p);
            SocialLife.Said(_p, key, data);
            return new DialogEntry { messageData = key.Localize(data) };
        }

        private DialogEntry Introduced()
        {
            SocialPerson friend = _talk.Introducer;
            Dictionary<string, string> data = SocialLife.Data(_p);
            data["friend"] = friend != null ? SocialLife.First(friend) : string.Empty;
            string said = SocialLinks.Position != null ? SocialLinks.Position(_p, _talk.Via ?? friend, true) : null;   // what they do, as the friend puts it
            string intro = SocialLines.Pick(SocialCatalogue.Introduces);
            DialogEntry entry = new DialogEntry
            {
                messageData = said != null ? SocialLinks.WordsKey.Localize(new Dictionary<string, string> { { "text", SocialLife.T(intro, data) + " " + said } }) : intro.Localize(data)
            };
            entry.ConfirmTextOverride = "reallife_social_btn_hello".Localize();
            entry.OnConfirm = MeetFirst;
            return entry;
        }

        private DialogEntry MeetFirst()
        {
            _talk.PersonSpeaks = true;
            DialogEntry entry = Say(_talk.Opener, _talk.OpenerData);
            string said = _talk.Mode != SceneTalk.Introduction && SocialLinks.Position != null ? SocialLinks.Position(_p, _talk.Via, false) : null;   // nobody here to say it for them
            if (said != null)
            {
                string opener = SocialLife.T(_talk.Opener, _talk.OpenerData ?? SocialLife.Data(_p));
                entry.messageData = SocialLinks.WordsKey.Localize(new Dictionary<string, string> { { "text", opener + " " + said } });
            }
            entry.ConfirmTextOverride = "reallife_social_meet_reply_warm".Localize();
            entry.OnConfirm = () => MeetSecond(true);
            entry.SecondOptionTextOverride = "reallife_social_meet_reply_cool";
            entry.OnSecondOption = () => MeetSecond(false);
            entry.CancelTextOverride = "reallife_social_meet_reply_brush";
            entry.OnCancel = () =>
            {
                _talk.Brushed = true;
                Leave();
            };
            return entry;
        }

        private DialogEntry MeetSecond(bool warm)
        {
            _talk.Warm = warm;
            DialogEntry entry = Say(SocialLines.Pick(warm ? SocialCatalogue.MeetWarm : SocialCatalogue.MeetCool));
            entry.ConfirmTextOverride = "reallife_social_btn_swap".Localize();
            entry.OnConfirm = () =>
            {
                _talk.Swapped = true;
                DialogEntry last = Say(SocialLines.Pick(SocialCatalogue.MeetNumbers));
                last.CancelTextOverride = "reallife_social_btn_see_you";
                last.OnCancel = Leave;
                return last;
            };
            entry.CancelTextOverride = "reallife_social_btn_nice_meeting";
            entry.OnCancel = Leave;
            return entry;
        }

        private DialogEntry Greeting()
        {
            DialogEntry entry = Say(_talk.Opener, _talk.OpenerData);
            entry.ConfirmTextOverride = "reallife_social_btn_chat".Localize();
            entry.OnConfirm = Chat;
            entry.SecondOptionTextOverride = _talk.GoLabel ?? "reallife_social_btn_lets_go";
            entry.OnSecondOption = Onward;
            return entry;
        }

        private DialogEntry Chat()
        {
            SocialTopic topic = SocialTalk.Pick(_p, out Dictionary<string, string> data);
            DialogEntry entry = Say(SocialLines.Pick(topic.Open), data);
            entry.ConfirmTextOverride = topic.ReplyA.Localize(data);
            entry.OnConfirm = () => Answered(topic, true, data);
            entry.SecondOptionTextOverride = topic.ReplyB;
            entry.OnSecondOption = () => Answered(topic, false, data);
            return entry;
        }

        private DialogEntry Answered(SocialTopic topic, bool warm, Dictionary<string, string> data)
        {
            string react = SocialTalk.Answer(_p, topic, warm, true, !_talk.Chatted);
            _talk.Chatted = true;
            DialogEntry entry = Say(react, data);
            entry.ConfirmTextOverride = (_talk.GoLabel ?? "reallife_social_btn_lets_go").Localize();
            entry.OnConfirm = Onward;
            return entry;
        }

        // Who pays a bill to come is settled here.
        private DialogEntry Onward()
        {
            if (_talk.Mode != SceneTalk.Outing || _talk.Cost <= 0)
            {
                return Done();
            }
            Dictionary<string, string> data = SocialLife.Data(_p, "cost", SocialLife.Money(_talk.Cost * 2));
            data["half"] = SocialLife.Money(_talk.Cost);
            DialogEntry entry = Say(_talk.TheyOffer ? "reallife_social_pay_ask_offer" : "reallife_social_pay_ask", data);
            entry.ConfirmTextOverride = "reallife_social_btn_treat".Localize();
            entry.OnConfirm = () =>
            {
                _talk.Pay = SceneTalk.Treat;
                return Done();
            };
            entry.SecondOptionTextOverride = "reallife_social_btn_split";
            entry.OnSecondOption = () =>
            {
                _talk.Pay = SceneTalk.Split;
                return Done();
            };
            if (_talk.TheyOffer)
            {
                entry.CancelTextOverride = "reallife_social_btn_theirs";
                entry.OnCancel = () =>
                {
                    _talk.Pay = SceneTalk.Theirs;
                    Leave();
                };
            }
            return entry;
        }
    }
}
