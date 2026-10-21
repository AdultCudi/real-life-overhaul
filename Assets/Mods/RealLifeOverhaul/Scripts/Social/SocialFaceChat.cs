using System;
using System.Collections.Generic;
using Entities;
using Localizor;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A chat face to face with the partner at home, inside Family's conversation there (through Wiring/FamilyLinks): the
    /// topics of a call, each with its two answers, worth what a chat face to face is (SocialTalk.Answer in person), up to
    /// <see cref="SocialRules.CallTopics"/> of them, then "Something else" goes back to the rest of what can be said. Their
    /// lines carry their name, as Family's lines there do. Typing an answer stays with calls (PersonDialog.Composer).
    /// COST: nothing per frame; a few short strings on a click.
    /// </summary>
    public static class SocialFaceChat
    {
        /// <summary>The first topic; <paramref name="more"/> is the rest of the conversation, <paramref name="leave"/> walks away.</summary>
        public static DialogEntry Start(SocialPerson person, Func<DialogEntry> more, Action leave)
        {
            return Topic(person, more, leave, 0);
        }

        private static DialogEntry Topic(SocialPerson person, Func<DialogEntry> more, Action leave, int had)
        {
            SocialTopic topic = SocialTalk.Pick(person, out Dictionary<string, string> data);
            DialogEntry entry = Line(person, SocialLines.Pick(topic.Open), data, leave);
            entry.ConfirmTextOverride = topic.ReplyA.Localize(data);
            entry.OnConfirm = () => Reply(person, topic, true, data, more, leave, had);
            entry.SecondOptionTextOverride = topic.ReplyB;
            entry.OnSecondOption = () => Reply(person, topic, false, data, more, leave, had);
            return entry;
        }

        private static DialogEntry Reply(SocialPerson person, SocialTopic topic, bool warm, Dictionary<string, string> data, Func<DialogEntry> more, Action leave, int had)
        {
            string react = SocialTalk.Answer(person, topic, warm, true, had == 0);
            DialogEntry entry = Line(person, react, data, leave);
            if (had + 1 < SocialRules.CallTopics)
            {
                entry.ConfirmTextOverride = "reallife_social_btn_go_on".Localize();
                entry.OnConfirm = () => Topic(person, more, leave, had + 1);
            }
            entry.SecondOptionTextOverride = "reallife_social_btn_else";
            entry.OnSecondOption = more;
            return entry;
        }

        // A line of theirs, remembered as said, shown with their name in front.
        private static DialogEntry Line(SocialPerson person, string key, Dictionary<string, string> data, Action leave)
        {
            data = data ?? SocialLife.Data(person);
            SocialLife.Said(person, key, data);
            var shown = new Dictionary<string, string>(2) { { "name", person.Name }, { "text", SocialLife.T(key, data) } };
            return new DialogEntry { messageData = "reallife_family_face_line".Localize(shown), OnCancel = leave };
        }
    }
}
