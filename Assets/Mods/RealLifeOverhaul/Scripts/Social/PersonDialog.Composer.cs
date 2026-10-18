using System;
using System.Collections.Generic;
using System.Globalization;
using Entities;
using Localizor;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A call's composer. While RLO Texts can answer this person now (SocialLinks.ComposerWhyNot: a pack running whose data
    /// knows the card, the model loaded and quick enough) and "Type what you say in calls" is on, a topic they raise is
    /// answered by the player's own words, typed in the game's text box (Core/GameUi/TextBox), in place of its two answers,
    /// and they answer in their own words, asked of the model off the game's thread (SocialLinks.AnswerTyped). The typed
    /// words count as the warm answer (SocialComposerRules), settled the moment they are sent, so nothing depends on what
    /// either side wrote; the answer only shows. Words that do not come within the hold, or fail the checks, are the warm
    /// answer's written reaction; a call that ends meanwhile lets them go. Mom, a game without a pack, a pack of an older
    /// data version, the option off, the model loading or slow: the two answers, exactly as without the composer. Plans,
    /// favours, gifts, asking out and the rest of "Something else" stay buttons. With Developer diagnostics on, one
    /// "Social check (composer)" line for each topic and each typed message.
    /// COST: nothing per frame; on a click a few short strings, and one request of RLO Texts a typed message.
    /// </summary>
    public sealed partial class PersonDialog
    {
        private const string Signed = "+0.0;-0.0;0.0";   // a change in points as the check line writes it

        private readonly List<SocialCallLine> _lines = new List<SocialCallLine>(SocialComposerRules.LinesKept + 1);
        private SocialTopic _typing;                      // the topic the text box is open for; null once the call has moved on
        private Dictionary<string, string> _typingData;
        private bool _waiting;                            // typed words were sent, and their answer is awaited
        private string _react;                            // ...the warm answer's written reaction, if no words come
        private Dictionary<string, string> _reactData;
        private string _counted;                          // what the typed words counted as, for the check line
        private Action<string> _onTyped;
        private Action _onTypingClosed;
        private Action<string, string> _onAnswer;
        private Func<bool> _wanted;

        // The game's call is still this one: hung up, the phone shut or another call, and it is not.
        private bool Live => DialogController.current != null && DialogController.current.dialog == this;

        // A line of theirs, for the card; any text box opened for an earlier topic is no longer answered.
        private void TheySaid(string words)
        {
            _typing = null;
            SocialComposerRules.Keep(_lines, false, words);
        }

        // True when the topic's answer is typed: the entry gets the text box's button and "Something else".
        private bool Composing(SocialTopic topic, Dictionary<string, string> data, DialogEntry entry)
        {
            string textsWhy = SocialLinks.ComposerWhyNot != null ? SocialLinks.ComposerWhyNot(_p) : "RLO Texts is not running";
            string why = SocialComposerRules.Why(Settings.Social, Settings.SocialComposer, TextBox.CanType, textsWhy);
            if (why == null)
            {
                entry.ConfirmTextOverride = "reallife_social_btn_type".Localize();
                entry.OnConfirm = () => Type(topic, data);
                entry.SecondOptionTextOverride = "reallife_social_btn_else";
                entry.OnSecondOption = () => Menu(0);
            }
            if (Meta.Dev.Verbose)
            {
                string shown = why == null ? "typed, RLO Texts can answer" : "the two answers, as without it (" + why + ")";
                SocialDirector.Logger?.Info("Social check (composer): " + _p.Name + "'s call, topic " + topic.Id + ": " + shown + ": PASS");
            }
            return why == null;
        }

        // The game's text box over the call; the entry and its buttons stay until the words are sent.
        private DialogEntry Type(SocialTopic topic, Dictionary<string, string> data)
        {
            if (_waiting || !Live)
            {
                return null;
            }
            _typing = topic;
            _typingData = data;
            if (!TextBox.Ask("reallife_social_type_placeholder", "reallife_social_type_send", _onTyped ?? (_onTyped = Guard.Wrap<string>(Typed)),
                    _onTypingClosed ?? (_onTypingClosed = Guard.Wrap(TypingClosed)), null, false, SocialComposerRules.TypedMost))
            {
                _typing = null;   // (the game has no text box after all: "Something else" is still there)
            }
            return null;
        }

        private void TypingClosed()
        {
            _typing = null;
        }

        // The words sent: what they are worth is settled at once, they are shown as the player's, and the answer is asked.
        private void Typed(string text)
        {
            SocialTopic topic = _typing;
            Dictionary<string, string> data = _typingData;
            _typing = null;
            if (topic == null || _waiting || !Live)
            {
                return;   // (the call moved on or ended while the box was open)
            }
            string words = SocialLinks.CleanTyped != null ? SocialLinks.CleanTyped(text) : null;
            if (words == null)
            {
                return;   // (nothing left to say: the topic and its buttons stay)
            }
            float familiarity = _p.Familiarity, affection = _p.Affection, trust = _p.Trust;
            SocialWorth want = SocialTalk.Worth(_p, topic, SocialComposerRules.TypedWarm, false, _topics == 0);
            string react = Answered(topic, SocialComposerRules.TypedWarm);
            _counted = "counted as the warm answer to topic " + topic.Id + ": " + Moved("familiarity", familiarity, _p.Familiarity, want.Familiarity)
                       + ", " + Moved("affection", affection, _p.Affection, want.Affection) + ", " + Moved("trust", trust, _p.Trust, want.Trust);
            Show(Mine(words));
            SocialComposerRules.Keep(_lines, true, words);
            _react = react;
            _reactData = data;
            string why = "RLO Texts is not running";
            _waiting = true;
            _onAnswer = _onAnswer ?? Guard.Wrap<string, string>(AnswerCame);
            if (SocialLinks.AnswerTyped != null && SocialLinks.AnswerTyped(_p, _lines, _wanted ?? (_wanted = Wanted), _onAnswer, out why))
            {
                return;   // (shown when the words come or the hold is over: AnswerCame)
            }
            AnswerCame(null, why);
        }

        // Asked by RLO Texts when the words come: false once the call has moved on or ended.
        private bool Wanted() => _waiting && Live;

        // Their answer: the model's words, or the warm answer's written reaction; then the call goes on as after a choice.
        private void AnswerCame(string words, string how)
        {
            if (!_waiting)
            {
                return;
            }
            _waiting = false;
            bool live = Live;
            if (live)
            {
                Show(Onward(words != null ? Words(words) : Line(_react, _reactData)));
            }
            if (Meta.Dev.Verbose)
            {
                bool counted = _counted != null && _counted.IndexOf("FAIL", StringComparison.Ordinal) < 0;
                SocialDirector.Logger?.Info("Social check (composer): " + _p.Name + "'s call, a typed message " + _counted + "; "
                                            + (words != null ? "answered (" + how + ")" : "the written answer (" + how + ")")
                                            + (live ? string.Empty : ", the call had ended") + ": " + (counted ? "PASS" : "FAIL, the message was not worth the warm answer"));
            }
        }

        // The player's own words, as theirs, with only the way out while the answer is awaited.
        private static DialogEntry Mine(string words)
        {
            var data = new Dictionary<string, string>(1) { { "text", words } };
            return new DialogEntry { messageData = "reallife_social_typed_you".Localize(data), OnCancel = PhoneContacts.HangUp };
        }

        // The model's words as their line: shown, and kept as the last thing they said.
        private DialogEntry Words(string words)
        {
            var data = new Dictionary<string, string>(1) { { "text", words } };
            SocialLife.Said(_p, SocialLinks.WordsKey, data);
            TheySaid(words);
            return new DialogEntry { messageData = SocialLinks.WordsKey.Localize(data), OnCancel = PhoneContacts.HangUp };
        }

        // "affection +3.5" as measured against the rule's own figure; a figure held at the top of its scale is no fault.
        private static string Moved(string what, float before, float after, float want)
        {
            float moved = after - before;
            bool ok = Math.Abs(moved - want) < 0.01f || (after >= 99.99f && moved <= want);
            string said = what + " " + moved.ToString(Signed, CultureInfo.InvariantCulture);
            return ok ? said : said + " (FAIL: the rules give " + want.ToString(Signed, CultureInfo.InvariantCulture) + ")";
        }
    }
}
