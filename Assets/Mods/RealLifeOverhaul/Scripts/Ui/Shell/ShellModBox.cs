using System;
using System.Collections.Generic;
using Localizor;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The mod's own messages and questions (Meta/MetaUi.Message and Confirm: the options page's notes, a reset asked
    /// first) on the kit's compact card, on a canvas of its own above the game's menus, as MetaUi's own panel stood. With
    /// the interface or its switch off, or its fonts not in yet, MetaUi draws its own panel as before. A button closes the
    /// card, then does its work, as the panel's did. Cost: nothing until a message is asked; its card's input while it shows.
    /// </summary>
    public static class ShellModBox
    {
        private const int Order = 32100;   // above the game's menus and the options page, as MetaUi's own canvas, under the kit's tests

        private static GameObject _root;
        private static UiHost _host;
        private static ModBoxScreen _screen;

        /// <summary>Hands MetaUi the card; once, when the interface starts.</summary>
        public static void Start()
        {
            Meta.MetaUi.Kit = Show;
        }

        // A message on the card; false when the panel should draw it.
        private static bool Show(string title, string body, KeyValuePair<string, Action>[] buttons, bool danger)
        {
            if (!UiDirector.On || !UiScreenSwitches.On("mod_box") || !UiFonts.Ready)
            {
                return false;
            }
            try
            {
                Bind();
                _screen.Set(title, body, buttons, danger);
                _root.SetActive(true);
                _host.Show();
                if (_host.Up)
                {
                    return true;
                }
                Close();   // its host would not run: the mod's own panel says it instead
                return false;
            }
            catch (Exception e)
            {
                UiDirector.Logger?.Error(e);
                Close();
                return false;
            }
        }

        /// <summary>The card closed, whoever asks: a button, a fault, the interface's part stopping, the city going. Safe when it is not up.</summary>
        internal static void Close()
        {
            if (_host != null)
            {
                _host.Hide();
            }
            if (_root != null && _root.activeSelf)
            {
                _root.SetActive(false);
            }
        }

        private static void Bind()
        {
            if (_root != null)
            {
                return;
            }
            _root = new GameObject("RealLife Interface Messages", typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Order;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3840f, 2160f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            _root.AddComponent<GraphicRaycaster>();
            UiRoots.Take(_root);
            var blocker = _root.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0f);   // the page under the card takes no click while it asks
            _screen = new ModBoxScreen { Closing = Close };
            _host = _root.AddComponent<UiHost>();
            _host.Bind(_screen, UiGame.OverBoxes, UiGame.OverBoxes, UiDirector.Dark, UiDirector.Motion, UiFrame.MastheadHeight, UiFrame.BandHeight, null);
            _host.View.Scale = UiTokens.CanvasPerPx;
            _host.Failed = e =>
            {
                Close();
                Meta.MetaUi.Kit = null;   // the mod's own panel for the rest of the session
            };
            _root.SetActive(false);
        }

        /// <summary>The mod's message as a compact card: its title, its words, its buttons, the last the confirming one.</summary>
        private sealed class ModBoxScreen : IUiScreen
        {
            private readonly UiCardModel _m = new UiCardModel();
            private KeyValuePair<string, Action>[] _buttons;
            private bool _dirty = true;

            public Action Closing;

            public string Id => "mod_box";

            public bool Dirty => _dirty;

            public void Set(string title, string body, KeyValuePair<string, Action>[] buttons, bool danger)
            {
                _buttons = buttons == null || buttons.Length == 0 ? new[] { new KeyValuePair<string, Action>("reallife_meta_ok", null) } : buttons;
                _m.Icon = danger ? UiIcon.Warning : UiIcon.Info;   // a reset or a removal warns; a note informs
                _m.BodyLines = 28;       // its explanations run long (what a reset keeps and what it ends), each line of it read
                _m.Title = title;
                _m.Body = body;
                _m.Buttons = new UiActionModel[_buttons.Length];
                for (int i = 0; i < _buttons.Length; i++)
                {
                    bool last = i == _buttons.Length - 1;
                    bool back = _buttons.Length > 1 && i == 0;
                    _m.Buttons[i] = new UiActionModel
                    {
                        Label = LocalizorManager.GetLocalization(_buttons[i].Key), Kind = last ? UiButtonKind.Primary : UiButtonKind.Secondary,
                        Key = last ? UiAction.Alt : back ? UiAction.Back : UiAction.Count, Command = UiCard.ButtonCommand, Arg = i
                    };
                }
                if (_buttons.Length > 1)
                {
                    // MetaUi.Confirm gives the doing button first and Cancel last: the way back goes first on the card, the doing one last.
                    Array.Reverse(_m.Buttons);
                    for (int i = 0; i < _m.Buttons.Length; i++)
                    {
                        bool last = i == _m.Buttons.Length - 1;
                        _m.Buttons[i].Kind = last ? (danger ? UiButtonKind.Destructive : UiButtonKind.Primary) : UiButtonKind.Secondary;
                        _m.Buttons[i].Key = last ? UiAction.Alt : i == 0 ? UiAction.Back : UiAction.Count;
                    }
                }
                _m.Focus = danger && _m.Buttons.Length > 1 ? 0 : -1;   // what cannot be undone is never where the keys start
                _m.Prompts = new[]
                {
                    new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") },
                    new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") },
                    new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") },
                };
                _m.PromptCount = _m.Prompts.Length;
                _dirty = true;
            }

            public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
            {
                UiCard.Lay(s, _m, frame.As(UiFrameKind.Card), dark, keys);
                _dirty = false;
            }

            public bool Press(int command, int arg)
            {
                if (command != UiCard.ButtonCommand || _buttons == null || arg < 0 || arg >= _buttons.Length)
                {
                    return false;
                }
                Action work = _buttons[arg].Value;
                Closing?.Invoke();
                work?.Invoke();
                return true;
            }

            public void Focused(int command, int arg)
            {
            }

            public bool Back()
            {
                if (_buttons == null)
                {
                    return false;
                }
                int back = _buttons.Length - 1;   // the panel's last button is its Cancel (MetaUi.Confirm) or its only OK
                Action work = _buttons.Length > 1 ? _buttons[back].Value : null;
                Closing?.Invoke();
                work?.Invoke();
                return true;
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
        }
    }
}
