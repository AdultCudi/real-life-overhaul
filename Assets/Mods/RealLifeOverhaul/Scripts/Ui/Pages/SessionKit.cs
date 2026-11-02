using System;
using System.Collections.Generic;
using Localizor;
using PlayerActivity;
using UI.Elements;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A desk session's pages on the kit's session template while they are chosen: the game's own activity panel keeps
    /// the session's flow, the seat at the desk (DeskGame.SitAt and SitOn, untouched) and the time in the chair with its
    /// skip and fast forward; the kit stands over it with the same title, words and buttons, takes the keys the panel
    /// answers to, and steps aside the moment the time in the chair begins, the panel closes, a Going Public session
    /// runs, or anything fails. Cost: the host's input reads while a page shows; nothing otherwise.
    /// </summary>
    public static class SessionKit
    {
        private static GameObject _root;
        private static UiHost _host;
        private static object _for;

        /// <summary>True while the kit's session pages stand over the game's activity panel (the activity panel's kit then draws nothing).</summary>
        public static bool Up => _root != null;

        /// <summary>Over a desk session's panel as it opens (the RLO interface on, no Going Public session).</summary>
        public static void Show(IPlayerActivity panel)
        {
            if (panel == null || !UiDirector.On || !UiScreenSwitches.On("session_panel") || GoingPublic.InSession || UiDirector.Instance == null)
            {
                return;
            }
            try
            {
                Hide(null);
                _for = panel;
                var screen = new ActivityScreen(panel);
                _host = UiDirector.Instance.Overlay(screen, UiText.T("reallife_ui_page_desk"), host => screen.Close = () => Hide(panel), UiIcon.Presentation);
                _host.Exclusive = true;
                _host.Failed = e => Hide(panel);
                _root = _host.gameObject;
            }
            catch (Exception e)
            {
                UiDirector.Logger?.Warn("UI: the desk session stays on the game's own panel (" + e.GetType().Name + ").");
                Hide(null);
            }
        }

        /// <summary>The panel alone again: the time in the chair began, the panel closed, or the kit failed.</summary>
        public static void Hide(object panel)
        {
            if (panel != null && !ReferenceEquals(panel, _for))
            {
                return;
            }
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
            _root = null;
            _host = null;
            _for = null;
        }
    }

    /// <summary>
    /// A panel of the game's activity system (an IPlayerActivity: its headline, its words, its buttons) as the kit's
    /// session template: the choices (the buttons that pick one) as cards on 1, 2 and 3, the panel's other buttons at
    /// the foot (the blue one the primary on F, the next on P, Cancel on Esc), every press the button's own. Looks at the
    /// panel four times a second while it shows. Cost: a few reads and a layout when the page changes.
    /// </summary>
    public sealed class ActivityScreen : IUiScreen
    {
        private const float Every = 0.25f;

        private readonly IPlayerActivity _panel;
        private readonly List<ButtonInfo> _choices = new List<ButtonInfo>(4);
        private readonly List<ButtonInfo> _actions = new List<ButtonInfo>(5);
        private ButtonInfo _cancel;
        private bool _dirty = true;
        private float _next;
        private int _seen;

        public Action Close;

        public ActivityScreen(IPlayerActivity panel)
        {
            _panel = panel;
        }

        public string Id => "session_panel";

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    int now = Signature();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            UiSessionSteps.Lay(s, Build(), frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            ButtonInfo b = command == UiSessionSteps.ChoiceCommand && arg >= 0 && arg < _choices.Count ? _choices[arg]
                : command == UiSessionSteps.ActionCommand && arg >= 0 && arg < _actions.Count ? _actions[arg] : null;
            if (b == null || !b.interactable || b.onClick == null)
            {
                return false;
            }
            b.onClick();
            _dirty = true;
            return true;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            if (_cancel != null && _cancel.onClick != null)
            {
                _cancel.onClick();
                return true;
            }
            Close?.Invoke();
            return Close != null;
        }

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }

        // The page now, as names and keys: the panel builds new buttons when its page changes.
        private int Signature()
        {
            ButtonInfo[] buttons = _panel.GetButtons();
            int sig = buttons != null ? buttons.Length : 0;
            for (int i = 0; buttons != null && i < buttons.Length; i++)
            {
                sig = sig * 31 + (buttons[i] != null && buttons[i].name != null ? buttons[i].name.GetHashCode() : 0) + (buttons[i] != null && buttons[i].interactable ? 1 : 0);
            }
            return sig;
        }

        private UiSessionModel Build()
        {
            LabelInfo head = _panel.GetHeadlineLabel();
            LabelInfo[] labels = _panel.GetLabels() ?? new LabelInfo[0];
            ButtonInfo[] buttons = _panel.GetButtons() ?? new ButtonInfo[0];
            _choices.Clear();
            _actions.Clear();
            _cancel = null;
            var m = new UiSessionModel
            {
                Title = Words(head),
                About = labels.Length > 0 ? Words(labels[0]) : null,
                StepCount = 0,
                SummaryTitle = null,
            };
            var choices = new List<UiChoiceModel>(4);
            var actions = new List<UiActionModel>(5);
            int keyed = 0;
            foreach (ButtonInfo b in buttons)
            {
                if (b == null)
                {
                    continue;
                }
                string words = LocalizorManager.GetLocalization(b.key, b.arguments);
                if (b.name != null && b.name.StartsWith("StartRealLifeSessionChoice", StringComparison.Ordinal))
                {
                    int i = choices.Count;
                    string trade = 1 + i < labels.Length ? Words(labels[1 + i]) : null;
                    if (trade != null && trade.StartsWith(words + ":", StringComparison.Ordinal))
                    {
                        trade = trade.Substring(words.Length + 1).TrimStart();   // the panel's line names the choice the card already names
                    }
                    choices.Add(new UiChoiceModel { Key = (UiAction)((int)UiAction.Pick1 + i), Name = words, Trade = trade, Disabled = !b.interactable });
                    _choices.Add(b);
                }
                else if (b.playerAction == global::PlayerAction.Cancel)
                {
                    _cancel = b;
                    actions.Add(new UiActionModel { Label = words, Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = UiSessionSteps.ActionCommand, Disabled = !b.interactable });
                    _actions.Add(b);
                }
                else
                {
                    bool primary = b.color == "blue";
                    actions.Add(new UiActionModel
                    {
                        Label = words, Kind = primary ? UiButtonKind.Primary : UiButtonKind.Secondary, Key = primary ? UiAction.Alt : keyed++ == 0 ? UiAction.Option : UiAction.Count,
                        Command = UiSessionSteps.ActionCommand, Disabled = !b.interactable
                    });
                    _actions.Add(b);
                }
            }
            m.Question = choices.Count > 0 && labels.Length > 0 ? Words(labels[0]) : null;
            m.About = choices.Count > 0 ? null : m.About;
            m.Choices = choices.ToArray();
            m.ChoiceCount = m.Choices.Length;
            var summary = new List<UiPairModel>(4);
            for (int i = choices.Count > 0 ? labels.Length : 1; i < labels.Length; i++)
            {
                summary.Add(new UiPairModel { Key = Words(labels[i]), Value = string.Empty });
            }
            m.Summary = summary.ToArray();
            m.SummaryCount = m.Summary.Length;
            m.Actions = actions.ToArray();
            m.ActionCount = m.Actions.Length;
            m.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") },
                new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_choose") },
                new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") },
            };
            m.PromptCount = m.Prompts.Length;
            return m;
        }

        private static string Words(LabelInfo l)
        {
            if (l == null || string.IsNullOrEmpty(l.key))
            {
                return string.Empty;
            }
            return l.localize ? LocalizorManager.GetLocalization(l.key, l.arguments) : l.key;
        }
    }
}
