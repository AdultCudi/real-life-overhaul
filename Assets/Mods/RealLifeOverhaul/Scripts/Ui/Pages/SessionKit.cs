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
    /// decision card: the choices (the buttons that pick one) numbered 1, 2 and 3 with what each does in plain lines, the
    /// panel's other buttons at the foot (the blue one the primary on F, the next on P, Cancel on Esc), every press the
    /// button's own. Looks at the panel four times a second while it shows. Cost: a few reads and a layout when the page changes.
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
            UiDecision.Lay(s, Build(), frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            ButtonInfo b = command == UiDecision.ChoiceCommand && arg >= 0 && arg < _choices.Count ? _choices[arg]
                : command == UiDecision.ButtonCommand && arg >= 0 && arg < _actions.Count ? _actions[arg] : null;
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

        private UiDecisionModel Build()
        {
            LabelInfo head = _panel.GetHeadlineLabel();
            LabelInfo[] labels = _panel.GetLabels() ?? new LabelInfo[0];
            ButtonInfo[] buttons = _panel.GetButtons() ?? new ButtonInfo[0];
            _choices.Clear();
            _actions.Clear();
            _cancel = null;
            var choices = new List<UiDecisionChoice>(4);
            var actions = new List<UiActionModel>(5);
            int keyed = 0, primary = -1, other = -1;
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
                    choices.Add(new UiDecisionChoice { Key = (UiAction)((int)UiAction.Pick1 + i), Name = words, Lines = SweepRules.Consequences(trade, words), Disabled = !b.interactable });
                    _choices.Add(b);
                }
                else if (b.playerAction == global::PlayerAction.Cancel)
                {
                    _cancel = b;
                    actions.Add(new UiActionModel { Label = words, Kind = UiButtonKind.Secondary, Key = UiAction.Back, Disabled = !b.interactable });
                    _actions.Add(b);
                }
                else
                {
                    bool main = b.color == "blue";
                    primary = main ? actions.Count : primary;
                    other = other < 0 && !main ? actions.Count : other;
                    actions.Add(new UiActionModel { Label = words, Kind = main ? UiButtonKind.Primary : UiButtonKind.Secondary,
                        Key = main ? UiAction.Alt : keyed++ == 0 ? UiAction.Option : UiAction.Count, Disabled = !b.interactable });
                    _actions.Add(b);
                }
            }
            string about = labels.Length > 0 ? Words(labels[0]) : null;
            var more = new List<string>(4);   // a page without choices says the rest of what the panel says
            for (int i = 1; choices.Count == 0 && i < labels.Length; i++)
            {
                more.Add(Words(labels[i]));
            }
            string rest = string.Join(" ", more);
            var m = new UiDecisionModel
            {
                Kicker = UiText.T("reallife_session_enabled"), Title = Words(head), Emblem = UiIcon.Presentation, EmblemTint = UiRole.Accent,
                Body = string.IsNullOrEmpty(rest) ? about : (string.IsNullOrEmpty(about) ? rest : about + " " + rest), BodyLines = choices.Count > 0 ? 3 : 8,
                Choices = choices.ToArray(), Selected = choices.Count > 0 ? 0 : -1, Buttons = actions.ToArray(), Start = choices.Count > 0 ? 0 : -1,
                StartButton = choices.Count > 0 ? -1 : primary >= 0 ? primary : Math.Max(0, other)   // a page that cannot begin starts on the skip, never on Cancel: Enter there closed the desk
            };
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
