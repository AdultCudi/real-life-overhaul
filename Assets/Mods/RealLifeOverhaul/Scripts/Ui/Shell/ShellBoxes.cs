using System;
using System.Collections.Generic;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The boxes every screen uses, drawn on the kit's compact decision card over the game's own: its question box (two of
    /// them, the full menu's and the HUD's), its acknowledgement and its text box, through the host (UiGameHost) like any
    /// game screen, each with its own switch. The game's box stays up and live underneath, its layout unseen; the card
    /// reads its words and its negative look after the game and Going Public filled it, and presses its own buttons, so
    /// Going Public's replaced confirm still runs and GameConfirm keeps working unchanged. The mod's own message box
    /// (Meta/MetaUi) draws on the same card. The boxes are found under the game's interface the first time asked in a city; one not there then (the game
    /// builds some late, and the acknowledgement only in its main menu) is looked for again only while a box of its kind
    /// is up (ShellBoxLook). Cost: nothing while no box is up; while one is, its card's input and a look at its words four
    /// times a second.
    /// </summary>
    public static class ShellBoxes
    {
        private static readonly UiGameHost[] Asks = { new UiGameHost(new AskBox(true)), new UiGameHost(new AskBox(false)) };
        private static readonly UiGameHost Ack = new UiGameHost(new AckBox());
        private static readonly UiGameHost Typing = new UiGameHost(new TypeBox());
        private static readonly ShellBoxLook Seen = new ShellBoxLook();
        private static readonly Scene Walker = new Scene();
        private static HudConfirmUi[] _asks;
        private static AcknowledgeWarningUi _ack;

        /// <summary>Once a second while the interface is on: each box's host put on while its switch is on, the game's own while off.</summary>
        public static void Install()
        {
            for (int i = 0; i < Asks.Length; i++)
            {
                Switch(Asks[i]);
            }
            Switch(Ack);
            Switch(Typing);
        }

        /// <summary>Every box the game's own again.</summary>
        public static void Remove(string why)
        {
            for (int i = 0; i < Asks.Length; i++)
            {
                Asks[i].Remove(why);
            }
            Ack.Remove(why);
            Typing.Remove(why);
        }

        /// <summary>A new city: the boxes are looked for again.</summary>
        public static void Rearm()
        {
            Seen.Rearm();
            _asks = null;
            _ack = null;
            for (int i = 0; i < Asks.Length; i++)
            {
                Asks[i].Rearm();
            }
            Ack.Rearm();
            Typing.Rearm();
        }

        /// <summary>The boxes' states for the interface's line at load.</summary>
        public static string States() => "question boxes (" + Asks[0].State + "; " + Asks[1].State + "), acknowledgement (" + Ack.State + "), text box (" + Typing.State + ")";

        private static void Switch(UiGameHost host)
        {
            if (UiScreenSwitches.On(host.Id))
            {
                host.Install();
            }
            else
            {
                host.Remove("its switch is off");
            }
        }

        // The game's boxes under its interface: no field of UIs holds them. Looked for the first time a box is asked for in a
        // city; one not found is looked for again only while a box of its kind is up, a few times at most.
        private static bool Look(ShellBox box)
        {
            if (Seen.Found(box))
            {
                return true;
            }
            bool open = Seen.Looks(box) > 0 && (box == ShellBox.Acknowledge ? AcknowledgeWarning.isOpen : GameConfirm.IsOpen);   // read only after a miss
            bool found = Seen.Seek(box, open, Walker);
            if (Seen.FirstMiss(box))
            {
                string what = box == ShellBox.Acknowledge ? "the game's acknowledgement is not in the city's interface (the game builds it in its main menu)"
                    : box == ShellBox.MenuAsk ? "the full menu's question box was not found under the game's interface" : "the question box was not found under the game's interface";
                UiDirector.Logger?.Warn("UI: " + what + ", so it stays the game's own until one is built; looked for again while a box is up.");
            }
            if (Seen.FoundLate(box))
            {
                UiDirector.Logger?.Info("UI: " + (box == ShellBox.Acknowledge ? "the acknowledgement" : box == ShellBox.MenuAsk ? "the full menu's question box" : "the question box")
                                        + " was built late and is found now (look " + Seen.Looks(box) + "), so it is drawn on the RLO interface.");
            }
            return found;
        }

        private static HudConfirmUi AskOf(bool menu)
        {
            for (int i = 0; _asks != null && i < _asks.Length; i++)
            {
                object shown = GameContract.AskInMenu.Field != null && _asks[i] != null ? GameContract.AskInMenu.Field.GetValue(_asks[i]) : null;
                if (shown is bool m && m == menu)
                {
                    return _asks[i];
                }
            }
            return null;
        }

        // The walk of the game's interface for a box.
        private sealed class Scene : IShellBoxScene
        {
            public bool Built => InstanceBehavior<UIs>.Instance != null && InstanceBehavior<UIs>.Instance.transform != null;

            public bool Holds(ShellBox box)
            {
                Transform canvases = InstanceBehavior<UIs>.Instance.transform;
                if (box == ShellBox.Acknowledge)
                {
                    AcknowledgeWarningUi[] acks = canvases.GetComponentsInChildren<AcknowledgeWarningUi>(true);
                    _ack = acks.Length > 0 ? acks[0] : null;
                    return _ack != null;
                }
                _asks = canvases.GetComponentsInChildren<HudConfirmUi>(true);
                return AskOf(box == ShellBox.MenuAsk) != null;
            }
        }

        private static string Words(GameMember member, object owner)
        {
            var label = member.Field != null ? member.Field.GetValue(owner) as Component : null;
            return label != null ? UiWidgets.Text(label.GetComponent<TMP_Text>()) : null;
        }

        private static Transform Container(GameMember member, object owner) => member.Field != null ? member.Field.GetValue(owner) as Transform : null;

        // ------------------------------------------------------------------------------------------------- the question

        private sealed class AskBox : IUiGameScreen
        {
            private readonly bool _menu;

            public AskBox(bool menu)
            {
                _menu = menu;
            }

            public string Id => "question_box";

            public string Name => _menu ? "the full menu's question box" : "the question box";

            public string Title => null;

            public UiIcon Icon => UiIcon.Info;

            public UiGameKind Kind => UiGameKind.Box;

            public Component Find()
            {
                if (!Look(_menu ? ShellBox.MenuAsk : ShellBox.HudAsk))
                {
                    return null;
                }
                HudConfirmUi box = AskOf(_menu);
                return box != null ? Container(GameContract.AskContainer, box) : null;
            }

            // The full menu's box has no panel (its field is empty in the game's scene: the words sit on its WhiteBox), so what
            // the card needs is read: the box over the container, and its two buttons.
            public string Check(Component found)
            {
                HudConfirmUi owner = Owner(found);
                if (owner == null)
                {
                    return "its box is not there";
                }
                return GameContract.AskConfirm.Field?.GetValue(owner) == null || GameContract.AskCancel.Field?.GetValue(owner) == null ? "its buttons are not there" : null;
            }

            public void Conceal(Component found, UiConceal conceal)
            {
                Transform container = found.transform;
                for (int i = 0; i < container.childCount; i++)
                {
                    conceal.Add(container.GetChild(i));   // the box's own pieces; the container is what the game switches on and off
                }
                conceal.Add(container.GetComponent<Graphic>());
            }

            public IUiScreen Make(Component found) => new QuestionScreen(Owner(found));

            private static HudConfirmUi Owner(Component found) => found.GetComponentInParent<HudConfirmUi>(true);
        }

        /// <summary>The game's question box as a compact decision card: its header, its body, its two buttons, the negative look in red.</summary>
        private sealed class QuestionScreen : IUiScreen
        {
            private const float Every = 0.25f;   // seconds between looks at the box's words
            private const int LongBody = 22;     // lines a body wraps before it ends at a whole word: a card of that height still stands inside the screen
            private readonly HudConfirmUi _box;
            private readonly UiCardModel _m = new UiCardModel();
            private string _seen;
            private float _next;
            private bool _dirty = true;

            public QuestionScreen(HudConfirmUi box)
            {
                _box = box;
            }

            public string Id => "question_box";

            public bool Dirty
            {
                get
                {
                    if (Time.unscaledTime >= _next)
                    {
                        _next = Time.unscaledTime + Every;
                        string now = Words(GameContract.AskHeader, _box) + "|" + Words(GameContract.AskBody, _box) + "|" + Negative();
                        _dirty |= now != _seen;
                        _seen = now;
                    }
                    return _dirty;
                }
            }

            public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
            {
                bool negative = Negative();
                _m.Icon = negative ? UiIcon.Warning : UiIcon.Info;
                _m.Title = Words(GameContract.AskHeader, _box);
                _m.Body = Words(GameContract.AskBody, _box);
                _m.BodyLines = LongBody;
                Button cancel = GameContract.AskCancel.Field?.GetValue(_box) as Button, confirm = GameContract.AskConfirm.Field?.GetValue(_box) as Button;
                _m.Buttons = Answers(Words(GameContract.AskCancelLabel, _box), cancel != null && cancel.gameObject.activeSelf, cancel == null || !cancel.interactable,
                    Words(GameContract.AskConfirmLabel, _box), confirm, negative);
                _m.Focus = negative && _m.Buttons.Length > 1 ? 0 : -1;   // a negative box starts on its way back
                _m.Prompts = CardPrompts();
                _m.PromptCount = _m.Prompts.Length;
                UiCard.Lay(s, _m, frame.As(UiFrameKind.Card), dark, keys);
                _dirty = false;
            }

            public bool Press(int command, int arg)
            {
                if (command != UiCard.ButtonCommand || (arg != 0 && arg != 1))
                {
                    return false;
                }
                return UiWidgets.Press((arg == 0 ? GameContract.AskCancel : GameContract.AskConfirm).Field?.GetValue(_box) as Button);
            }

            public void Focused(int command, int arg)
            {
            }

            public bool Back() => UiWidgets.Press(GameContract.AskCancel.Field?.GetValue(_box) as Button);

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

            private bool Negative()
            {
                var confirm = GameContract.AskConfirm.Field?.GetValue(_box) as Button;
                var red = GameContract.AskNegative.Field?.GetValue(_box) as Sprite;
                return confirm != null && red != null && confirm.image != null && confirm.image.sprite == red;
            }
        }

        // The way back first, the confirming one last: Back on the one, F on the other; a negative confirm in the danger's look.
        // While a field takes the keys, the confirm carries no letter's cap: Enter confirms there, and F would be typed.
        private static UiActionModel[] Answers(string back, bool backShown, bool backHeld, string yes, Button confirm, bool negative, bool typing = false)
        {
            var list = new List<UiActionModel>(2);
            if (backShown)
            {
                list.Add(new UiActionModel { Label = back, Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = UiCard.ButtonCommand, Arg = 0, Disabled = backHeld });
            }
            list.Add(new UiActionModel
            {
                Label = yes, Kind = negative ? UiButtonKind.Destructive : UiButtonKind.Primary, Key = typing ? UiAction.Count : UiAction.Alt, Command = UiCard.ButtonCommand, Arg = 1,
                Disabled = confirm == null || !confirm.interactable
            });
            return list.ToArray();
        }

        private static UiPrompt[] CardPrompts() => new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") },
            new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") },
            new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") },
        };

        // ------------------------------------------------------------------------------------------------- the acknowledgement

        private sealed class AckBox : IUiGameScreen
        {
            public string Id => "acknowledge_box";

            public string Name => "the acknowledgement box";

            public string Title => null;

            public UiIcon Icon => UiIcon.Warning;

            public UiGameKind Kind => UiGameKind.Box;

            public Component Find() => Look(ShellBox.Acknowledge) && _ack != null ? Container(GameContract.AckContainer, _ack) : null;

            public string Check(Component found) => UiWidgets.Calling(_ack, "ClickConfirm") == null ? "its confirm button is not there" : null;

            public void Conceal(Component found, UiConceal conceal)
            {
                Transform container = found.transform;
                for (int i = 0; i < container.childCount; i++)
                {
                    conceal.Add(container.GetChild(i));
                }
                conceal.Add(container.GetComponent<Graphic>());
            }

            public IUiScreen Make(Component found) => new AcknowledgeScreen(_ack);
        }

        /// <summary>The game's acknowledgement as a compact card: its words, its "don't show again" as a line to tick, its buttons.</summary>
        private sealed class AcknowledgeScreen : IUiScreen
        {
            private const float Every = 0.25f;   // seconds between looks at the box's words and its tick
            private readonly AcknowledgeWarningUi _box;
            private readonly UiCardModel _m = new UiCardModel();
            private readonly Button _yes, _no;
            private bool _dirty = true;
            private string _seen;
            private float _next;

            public AcknowledgeScreen(AcknowledgeWarningUi box)
            {
                _box = box;
                _yes = UiWidgets.Calling(box, "ClickConfirm");
                _no = UiWidgets.Calling(box, "ClickCancel");
            }

            public string Id => "acknowledge_box";

            public bool Dirty
            {
                get
                {
                    if (Time.unscaledTime >= _next)
                    {
                        _next = Time.unscaledTime + Every;
                        string now = Words(GameContract.AckHeader, _box) + "|" + Words(GameContract.AckBody, _box) + "|" + UiWidgets.On(Toggle());
                        _dirty |= now != _seen;
                        _seen = now;
                    }
                    return _dirty;
                }
            }

            public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
            {
                Toggle toggle = Toggle();
                _m.Icon = UiIcon.Warning;
                _m.Title = Words(GameContract.AckHeader, _box);
                _m.Body = Words(GameContract.AckBody, _box);
                _m.Check = toggle != null && toggle.gameObject.activeInHierarchy ? UiWidgets.Label(toggle) ?? UiText.T("reallife_ui_box_dont_show") : null;
                _m.Checked = UiWidgets.On(toggle);
                _m.Buttons = Answers(UiWidgets.Label(_no) ?? UiText.T("reallife_ui_box_cancel"), _no != null && _no.gameObject.activeSelf, _no == null || !_no.interactable,
                    Words(GameContract.AckConfirmLabel, _box), _yes, false);
                _m.Focus = _m.Buttons.Length - 1;   // on its agree, never on the opt-out line a reflexive Enter would tick for good
                _m.BodyLines = 20;                  // the game's warnings run long
                _m.Prompts = CardPrompts();
                _m.PromptCount = _m.Prompts.Length;
                UiCard.Lay(s, _m, frame.As(UiFrameKind.Card), dark, keys);
                _dirty = false;
            }

            public bool Press(int command, int arg)
            {
                _dirty = true;
                if (command == UiCard.CheckCommand)
                {
                    Toggle toggle = Toggle();
                    return toggle != null && UiWidgets.Set(toggle, !toggle.isOn);
                }
                return command == UiCard.ButtonCommand && UiWidgets.Press(arg == 0 ? _no : _yes);
            }

            public void Focused(int command, int arg)
            {
            }

            public bool Back() => UiWidgets.Press(_no);

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

            private Toggle Toggle() => GameContract.AckToggle.Field?.GetValue(_box) as Toggle;
        }

        // ------------------------------------------------------------------------------------------------- the text box

        private sealed class TypeBox : IUiGameScreen
        {
            public string Id => "text_box";

            public string Name => "the text box";

            public string Title => null;

            public UiIcon Icon => UiIcon.Message;

            public UiGameKind Kind => UiGameKind.Box;

            public Component Find()
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                return uis != null ? uis.textInputUi : null;
            }

            public string Check(Component found) => GameContract.TypeField.Field?.GetValue(found) == null ? "its field is not there" : null;

            public void Conceal(Component found, UiConceal conceal)
            {
                Transform root = found.transform;
                for (int i = 0; i < root.childCount; i++)
                {
                    conceal.Add(root.GetChild(i));
                }
                conceal.Add(root.GetComponent<Graphic>());
            }

            public IUiScreen Make(Component found) => new TextBoxScreen((TextInputUI)found);
        }

        /// <summary>
        /// The game's text box as a compact card: what it asks, the field (typed through the host's own field and set into
        /// the game's, so its limit and its listeners hold), Cancel and the game's confirm, held while the game holds it.
        /// Typing starts as it opens, as the game's own box takes the keys at once, and Enter confirms.
        /// </summary>
        private sealed class TextBoxScreen : IUiScreen, IUiTypingSubmit
        {
            private readonly TextInputUI _box;
            private readonly UiCardModel _m = new UiCardModel();
            private bool _dirty = true;
            private string _shown;   // the field's words when last laid out: the game fills them just after it opens the box

            public TextBoxScreen(TextInputUI box)
            {
                _box = box;
            }

            public string Id => "text_box";

            public bool Dirty => _dirty || (Field() != null && !string.Equals(Field().text, _shown, StringComparison.Ordinal));

            public int TypingCommand => UiCard.FieldCommand;

            public string TypingText => Field()?.text ?? string.Empty;

            public int TypingLimit => Field() != null ? Field().characterLimit : 0;

            public void Typed(string text)
            {
                UiWidgets.Type(Field(), text, false);
                _dirty = true;
            }

            public void Submitted(string text)
            {
                Button confirm = GameContract.TypeConfirm.Field?.GetValue(_box) as Button;
                if (UiWidgets.Shows(confirm))
                {
                    UiWidgets.Hand(confirm);
                }
            }

            public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
            {
                Button confirm = GameContract.TypeConfirm.Field?.GetValue(_box) as Button;
                _m.Icon = UiIcon.Message;
                _m.Title = Words(GameContract.TypePlaceholder, _box);
                _m.Body = null;
                _m.HasField = true;
                _m.Field = Field()?.text;
                _shown = _m.Field;
                _m.Placeholder = UiText.T("reallife_ui_box_type_here");
                _m.Typing = true;
                _m.Buttons = Answers(UiText.T("reallife_ui_box_cancel"), true, false, Words(GameContract.TypeButtonText, _box), confirm, false, true);
                _m.Prompts = CardPrompts();
                _m.PromptCount = _m.Prompts.Length;
                UiCard.Lay(s, _m, frame.As(UiFrameKind.Card), dark, keys);
                _dirty = false;
            }

            public bool Press(int command, int arg)
            {
                _dirty = true;
                if (command != UiCard.ButtonCommand)
                {
                    return false;
                }
                if (arg == 0)
                {
                    return Shut();
                }
                return UiWidgets.Press(GameContract.TypeConfirm.Field?.GetValue(_box) as Button);
            }

            public void Focused(int command, int arg)
            {
            }

            public bool Back() => Shut();

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

            private TMP_InputField Field() => GameContract.TypeField.Field?.GetValue(_box) as TMP_InputField;

            private Button Close() => UiWidgets.Calling(_box, "OnClose");

            // The game's own close: its button when it has one wired, else the method that button calls.
            private bool Shut()
            {
                Button close = Close();
                if (UiWidgets.Press(close))
                {
                    return true;
                }
                _box.OnClose();
                return true;
            }
        }
    }
}
