using System;
using System.Collections.Generic;
using Dialogs;
using Entities;
using Localizor;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The quiz in the game's dialogue panel, when somebody brings their friends to meet the player: the new person asks three questions about the one
    /// who brought them (<see cref="SocialQuiz"/>), the player answers with the panel's three buttons, and the friends react to each answer, give their
    /// verdict, and the person reacts last. Each answer changes the person's figures at once (so closing the panel keeps what was done); the first
    /// standing the new person gets is read from the answers when they are met (<see cref="SceneTalk.QuizRight"/>). Pages are made when they are shown;
    /// nothing runs per frame.
    /// </summary>
    internal sealed class SceneQuiz
    {
        private const string ButtonNext = "reallife_social_btn_next";

        private readonly SceneTalk _talk;
        private readonly SocialQuiz _quiz;
        private readonly SocialPerson _who;       // the new person, who asks
        private readonly SocialPerson _friend;    // the one they are asked about
        private readonly Func<DialogEntry> _onward;   // what comes after: the new person's own hello

        public SceneQuiz(SceneTalk talk, SocialPerson who, SocialPerson friend, Func<DialogEntry> onward)
        {
            _talk = talk;
            _quiz = talk.Quiz;
            _who = who;
            _friend = friend;
            _onward = onward;
        }

        // {first} the new person's first name, {friend} the other's.
        private Dictionary<string, string> Names()
        {
            Dictionary<string, string> data = SocialLife.Data(_who);
            data["friend"] = SocialLife.First(_friend);
            return data;
        }

        private int Seed => SocialInterests.SeedOf(_friend);

        private string Pick(string[] pool, int salt)
        {
            return pool[SocialFriendsRules.Index(pool.Length, SocialLife.Day, Seed, salt)];
        }

        private DialogEntry Page(string text)
        {
            return new DialogEntry { messageData = SocialLinks.WordsKey.Localize(new Dictionary<string, string> { { "text", text } }) };
        }

        /// <summary>The new person opens it: three questions, and the player may let it go by.</summary>
        public DialogEntry Open()
        {
            DialogEntry entry = Page(SocialLife.T(Pick(SocialInterests.Open, 41), Names()));
            entry.ConfirmTextOverride = "reallife_friends_btn_go".Localize();
            entry.OnConfirm = () => Ask(0);
            entry.SecondOptionTextOverride = "reallife_friends_btn_skip";
            entry.OnSecondOption = Skip;
            return entry;
        }

        private DialogEntry Skip()
        {
            _talk.Quiz = null;   // not played: no first standing is given or taken either
            DialogEntry entry = Page(SocialLife.T(Pick(SocialInterests.Skip, 43), Names()));
            entry.ConfirmTextOverride = "reallife_friends_btn_on".Localize();
            entry.OnConfirm = _onward;
            return entry;
        }

        private DialogEntry Ask(int q)
        {
            Dictionary<string, string> names = Names();
            string[] pool = SocialInterests.Questions[_quiz.Kind[q]];
            string question = SocialLife.T(pool[_quiz.Variant[q] % pool.Length], names);
            string known = null;
            if (_quiz.Known[q])
            {
                Dictionary<string, string> data = Names();
                data["answer"] = SocialHintGame.Thing(_quiz.Kind[q], _quiz.Choice[q][_quiz.RightAt[q]]);
                known = SocialLife.T("reallife_friends_q_known", data);
            }
            DialogEntry entry = Page(SocialLife.T("reallife_friends_q_page", new Dictionary<string, string>
            {
                { "n", (q + 1).ToString() }, { "total", SocialFriendsRules.Questions.ToString() }, { "question", known != null ? question + "\n" + known : question }
            }));
            string[] answers = new string[3];
            for (int slot = 0; slot < 3; slot++)
            {
                answers[slot] = SocialInterests.ThingKey(_quiz.Kind[q], _quiz.Choice[q][slot]);
            }
            entry.ConfirmTextOverride = answers[0].Localize();
            entry.OnConfirm = () => Answer(q, 0);
            entry.SecondOptionTextOverride = answers[1];
            entry.OnSecondOption = () => Answer(q, 1);
            entry.CancelTextOverride = answers[2];
            entry.OnCancel = () => DialogController.current.ShowEntry(Guard.Entry(Answer(q, 2)));   // (the panel's third button leads on by showing the page itself)
            return entry;
        }

        private DialogEntry Answer(int q, int slot)
        {
            bool right = slot == _quiz.RightAt[q];
            _quiz.Asked++;
            _quiz.Right += right ? 1 : 0;
            SocialWorth worth = SocialFriendsRules.Answered(_friend.Stage, right);
            Give(worth);
            Dictionary<string, string> data = Names();
            data["answer"] = SocialHintGame.Thing(_quiz.Kind[q], _quiz.Choice[q][_quiz.RightAt[q]]);
            string key = right ? Pick(_quiz.Known[q] ? SocialInterests.RightKnown : SocialInterests.Right, 50 + q) : Pick(SocialInterests.Wrong, 60 + q);
            DialogEntry entry = Page(SocialLife.T(key, data));
            entry.ConfirmTextOverride = ButtonNext.Localize();
            entry.OnConfirm = _quiz.Over ? (Func<DialogEntry>)Verdict : () => Ask(_quiz.Asked);
            SocialDirector.Logger?.Info("Social check (the quiz): question " + (q + 1) + " about " + _friend.Name + " (" + (InterestKind)_quiz.Kind[q] + ", "
                                        + (_quiz.Known[q] ? "heard" : "a guess") + ") answered " + (right ? "right" : "wrong") + ": PASS.");
            return entry;
        }

        // What an answer or the end of the quiz is worth to the person asked about, now, and kept for the card.
        private void Give(SocialWorth worth)
        {
            _quiz.Familiarity += worth.Familiarity;
            _quiz.Affection += worth.Affection;
            _quiz.Trust += worth.Trust;
            SocialLife.Contacted(_friend);
            SocialLife.Change(_friend, worth.Familiarity, worth.Affection, worth.Trust);
        }

        private DialogEntry Verdict()
        {
            int right = _quiz.Right;
            Give(SocialFriendsRules.Finished(_friend.Stage, right));
            _friend.Quizzes++;
            _friend.QuizBest = Math.Max(_friend.QuizBest, right);
            _friend.Shared++;
            if (right >= SocialFriendsRules.Questions)
            {
                SocialLife.Remember(_friend, MemoryCode.QuizAced);
            }
            else if (right <= 0)
            {
                SocialLife.Remember(_friend, MemoryCode.QuizFlop);
            }
            Dictionary<string, string> names = Names();
            string[] pool = right >= SocialFriendsRules.Questions ? SocialInterests.Perfect : right == 2 ? SocialInterests.VerdictTwo : right == 1 ? SocialInterests.VerdictOne : SocialInterests.Disaster;
            string verdict = SocialLife.T(Pick(pool, 70), names);
            string after = SocialLife.T(Pick(SocialInterests.After[right], 80), names);
            DialogEntry entry = Page(verdict + "\n\n" + after);
            entry.ConfirmTextOverride = "reallife_friends_btn_on".Localize();
            entry.OnConfirm = _onward;
            SocialDirector.Logger?.Info("Social check (the quiz): " + _friend.Name + " (" + _friend.Stage + ") asked about, " + right + " of " + SocialFriendsRules.Questions
                                        + " right, familiarity " + _quiz.Familiarity.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ", affection "
                                        + _quiz.Affection.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ", trust "
                                        + _quiz.Trust.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " (" + _friend.Quizzes + " quiz(zes) with them): PASS.");
            return entry;
        }
    }
}
