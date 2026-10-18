using System;
using System.Collections.Generic;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>FamilyDirector's part for what the player is asked: the key prompt, the objective card and the questions.</summary>
    public sealed partial class FamilyDirector
    {
        private struct Question
        {
            public string Header, Body, Yes, No;
            public Args Fill;
            public Action OnYes, OnNo;
        }

        private readonly List<Question> _questions = new List<Question>(4);

        private DoorPanel _door;            // the game's own panel over the person or place a prompt is about
        private ShiftCard _card;            // a copy of the game's job panel for the objective
        private bool _cardTried;
        private Action _onPrompt;
        private Func<string> _promptWhat;
        private string _promptButton;
        private bool _promptOn;
        private bool _promptWanted;
        private string _promptKey;
        private string _promptFill;
        private int _promptCost;
        private string _promptText;
        private Action _promptAction;
        private string _objectiveKey;
        private string _objectiveFill;
        private string _objectiveChild;
        private string _objectiveTitle;
        private string _objectiveLine;
        private bool _objectiveWanted;

        /// <summary>This tick's key prompt, on the game's panel over <paramref name="over"/> (the player if null); the first to ask in a tick has it.</summary>
        internal void Prompt(string key, string fill, Action action, Transform over = null, int cost = 0)
        {
            if (_promptWanted)
            {
                return;
            }
            _promptWanted = true;
            _promptAction = action;
            if (!ReferenceEquals(key, _promptKey) || fill != _promptFill || cost != _promptCost)
            {
                _promptKey = key;
                _promptFill = fill;
                _promptCost = cost;
                string line = FamilyText.Fill(key.GetLocalization().Replace("{key}: ", string.Empty),
                    new Args { Name = fill, Child = fill, Place = fill, Amount = cost > 0 ? FamilyText.Money(cost) : null });
                _promptText = line.Length > 0 ? char.ToUpperInvariant(line[0]) + line.Substring(1) : line;
                _promptButton = key.Replace("_prompt_", "_button_");
            }
            PlayerController player = PlayerHelper.PlayerController;
            if (CanAct() && player != null)
            {
                WorldPanel panel = _door.Panel;
                panel.Lines(_promptText, null, null, WorldPanel.White());
                panel.Button(0, _promptButton, _onPrompt, true, true);
                panel.Button(1, null, null, false, false);
                panel.Button(2, null, null, false, false);
                panel.Button(3, null, null, false, false);
                panel.Follow = over;
                panel.FollowLift = 2.1f;
                _promptOn = _door.Show(player.transform.position, player.transform.position, 2.1f, _promptWhat);
            }
            else if (_promptOn || _door.Up)
            {
                ClearPrompt();
            }
        }

        private string PromptWhat()
        {
            return _promptKey ?? string.Empty;
        }

        // The panel's button, on the player's key or a click: the prompt's own action, once.
        private void PromptPressed()
        {
            if (!CanAct())
            {
                return;
            }
            try
            {
                Action act = _promptAction;
                ClearPrompt();
                act?.Invoke();
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Error(e);
                Logger?.Warn("Family: switched off for this session after the error above. The save keeps the family as it was.");
                Stop(false);
            }
        }

        private void ClearPrompt()
        {
            _promptAction = null;
            if (_promptOn || _door.Up)
            {
                _promptOn = false;
                _door.Panel.Follow = null;
                _door.Hide("nothing to offer here now");
            }
        }

        /// <summary>This tick's objective on the copy of the game's job panel; without that panel, a notification each time it changes.</summary>
        internal void Objective(string titleKey, string lineKey, string fill, string child = null)
        {
            _objectiveWanted = true;
            bool changed = !ReferenceEquals(lineKey, _objectiveKey) || fill != _objectiveFill || child != _objectiveChild;
            if (changed)
            {
                _objectiveKey = lineKey;
                _objectiveFill = fill;
                _objectiveChild = child;
                _objectiveTitle = FamilyText.T(titleKey);
                _objectiveLine = FamilyText.T(lineKey, new Args { Name = Partner.Name, Place = fill, Child = child ?? fill, Text = fill, N = fill });
            }
            if (!_cardTried || (_card != null && !_card.Alive))
            {
                _cardTried = true;
                _card = ShiftCard.Make(Logger);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Family check (objective card): " + (_card != null ? "a copy of the game's job panel is ready: PASS"
                        : "the game's job panel could not be copied; objectives are sent as notifications: FAIL") + ".");
                }
            }
            if (_card == null)
            {
                if (changed && !ThirdPersonCamera.IsUiOpen())
                {
                    FamilyText.Toast(NotificationType.Info, "reallife_family_text", new Args { Text = _objectiveTitle + ": " + _objectiveLine }, 8f);
                }
                return;
            }
            if (ThirdPersonCamera.IsUiOpen())
            {
                _card.Show(false);
                return;
            }
            _card.Title(_objectiveTitle);
            _card.Line(0, _objectiveLine, WorldPanel.White());
            for (int i = 1; i < ShiftCard.Lines; i++)
            {
                _card.Line(i, null, WorldPanel.White());
            }
            _card.Show(true);
        }

        /// <summary>A question with two answers in the game's confirmation box, asked once nothing else has the player's attention.</summary>
        internal void Ask(string header, string body, in Args fill, string yes, string no, Action onYes, Action onNo = null)
        {
            _questions.Add(new Question { Header = header, Body = body, Fill = fill, Yes = yes, No = no, OnYes = onYes, OnNo = onNo });
        }

        private void Put(Question q)
        {
            GameConfirm.Show(FamilyText.Say(q.Header, q.Fill), FamilyText.Say(q.Body, q.Fill),
                () => Guarded(q.OnYes), () => Guarded(q.OnNo), q.Yes, q.No, false);
        }

        internal bool HasQuestions => _questions.Count > 0;
    }
}
