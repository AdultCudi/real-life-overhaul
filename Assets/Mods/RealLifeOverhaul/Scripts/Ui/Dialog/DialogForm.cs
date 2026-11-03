using System;
using System.Collections.Generic;
using Entities;
using Localizor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A game dialog's input form (the bank's loan, an agency's campaign, a delivery, a tow's place) read into the kit's
    /// form rows from the game's own widgets, as the game laid them out after its code and any mod's ran: a drop-down is a
    /// choice with its options, a figure's field with its steps an amount, a delivery's line its amount with the button
    /// that takes it away, a field alone words or a figure, a slider with the figure the game writes for it, a toggle a
    /// switch (one of a group an option), a colour's button a swatch, two words in a row a figure read only (one the game
    /// has not written yet says so), a lone line a note, a heading over the group it opens, a list's line with its button
    /// an entry, a button alone an action, and the game's splitters hairlines. A label the game hangs under another one to
    /// stand beside a lone figure further down (a wholesale contract's fee) names that figure; words the game switched off
    /// (a design's missing mods warning) are not shown. Every press goes to that widget (the drop-down's own SelectOption,
    /// the field's text, the slider's value, the toggle, the button's click), so the dialog reads its values back through
    /// GetInputTransform as it always has; what the line's confirm then pays and costs stands under it (DialogForm.Costs).
    /// Game side. Cost: a walk of the form's pieces four times a second while it shows, allocating nothing; a read only
    /// when that walk changed.
    /// </summary>
    internal sealed partial class DialogForm
    {
        private enum Piece : byte
        {
            None, Text, Splitter, Spacer, Choice, Amount, Field, Slider, Switch, Radio, Swatch, Button, Group
        }

        // What a row presses: the game's widgets behind it.
        private struct Widget
        {
            public global::UI.Elements.Dropdown Drop;
            public TMP_InputField Input;
            public Button Less, More, Remove, Press;
            public Slider Slide;
            public Toggle Toggle;
            public bool EndsEdit;          // the game's amount selector: its figure counts once its edit ends, so each typed figure ends it
            public Button[] Swatches;
            public int Kit;                // a row of the kit's own over the game's form (the loan's term): which; 0 for the game's widget
        }

        private static readonly List<Button> SwatchesFound = new List<Button>(16);
        private static readonly List<string> KeysFound = new List<string>(16);

        private readonly List<UiFormRow> _rows = new List<UiFormRow>(16);
        private readonly List<Widget> _widgets = new List<Widget>(16);
        private Transform _form;
        private string _carried;   // a label the game hung under another one, for the next lone figure

        public UiFormRow[] Rows = new UiFormRow[0];
        public int Count;
        public readonly UiFormPick Pick = new UiFormPick();

        /// <summary>The form read last, or null.</summary>
        public Transform Form => _form;

        /// <summary>
        /// Reads the form, its question (the entry's header) as the heading over it. A new form starts with no list open,
        /// nothing held and its top in view. Returns false when nothing in it is the kit's to draw.
        /// </summary>
        public bool Read(Transform form, string question) => Read(form, question, null, null);

        /// <summary>
        /// Reads the form of a line of a conversation the game runs: the bank's loan form takes its term, its figures and the
        /// bank's answers past the game's three as well (DialogForm.Loan).
        /// </summary>
        public bool Read(Transform form, string question, DialogEntry entry, DialogController talk)
        {
            if (!ReferenceEquals(form, _form))
            {
                Pick.Reset();
                _form = form;
            }
            _rows.Clear();
            _widgets.Clear();
            _carried = null;
            if (form == null)
            {
                Publish();
                return false;
            }
            if (!string.IsNullOrEmpty(question))
            {
                Add(new UiFormRow { Kind = UiFormKind.Heading, Label = question }, default);
            }
            int heading = _rows.Count;
            Walk(form, 0);
            Tidy();
            Loan(form, entry, talk, heading);
            Costs(form, entry, talk, heading);
            bool any = _rows.Count > heading;
            Publish();
            if (Pick.Row >= Count || (Pick.Row >= 0 && Rows[Pick.Row].Kind != UiFormKind.Choice))
            {
                Pick.Row = -1;
            }
            if (Pick.Row >= 0)
            {
                Options(Pick.Row);
            }
            if (Pick.Held >= Count || (Pick.Held >= 0 && Rows[Pick.Held].Kind != UiFormKind.Slider))
            {
                Pick.Held = -1;
            }
            return any;
        }

        /// <summary>Forgets the form: the line it stood on is answered or gone.</summary>
        public void Clear()
        {
            _form = null;
            _loanForm = null;   // a line answered in the game's own panel takes the bank's own figures
            _rows.Clear();
            _widgets.Clear();
            Pick.Reset();
            Publish();
        }

        /// <summary>
        /// The form's state as one number: what shows, the words, the figures, the states and the chosen. Walks the pieces
        /// without making anything.
        /// </summary>
        public static long Signature(Transform form)
        {
            return form == null ? 0L : Sign(form, 0);
        }

        // ------------------------------------------------------------------------------------------------- what a row takes

        /// <summary>True for a row typed into: a figure or words.</summary>
        public bool Typed(int row) => row >= 0 && row < Count && UiFormRows.Typed(Rows[row].Kind) && _widgets[row].Input != null && !Rows[row].Disabled;

        /// <summary>True for a row Left and Right step: an amount, a slider, a choice.</summary>
        public bool Steps(int row) =>
            row >= 0 && row < Count && !Rows[row].Disabled
            && (Rows[row].Kind == UiFormKind.Amount || Rows[row].Kind == UiFormKind.Slider || Rows[row].Kind == UiFormKind.Choice);

        /// <summary>The words in a typed row's field as the game holds them.</summary>
        public string Text(int row) => row >= 0 && row < Count && _widgets[row].Input != null ? _widgets[row].Input.text : string.Empty;

        /// <summary>
        /// Words typed into a row's field, through the field (its listeners reformat and the form's figures follow). An
        /// amount's figure counts once its edit ends (the game's AmountSelector takes it then), so each typed figure ends it;
        /// never its submit, which the game binds to the line's confirm on a form of one field (KeyboardInputHelper).
        /// </summary>
        public bool Type(int row, string text)
        {
            if (!Typed(row))
            {
                return false;
            }
            Widget w = _widgets[row];
            if (!UiWidgets.Type(w.Input, text, false))
            {
                return false;
            }
            if (w.EndsEdit)
            {
                w.Input.onEndEdit?.Invoke(w.Input.text);
            }
            return true;
        }

        /// <summary>A step left or right on a row: an amount's minus or plus, a slider's step, the choice before or after.</summary>
        public bool Step(int row, int step)
        {
            if (!Steps(row))
            {
                return false;
            }
            Widget w = _widgets[row];
            switch (Rows[row].Kind)
            {
                case UiFormKind.Amount:
                    return UiWidgets.Press(step < 0 ? w.Less : w.More);
                case UiFormKind.Slider:
                {
                    Slider s = w.Slide;
                    if (s == null)
                    {
                        return false;
                    }
                    float by = s.wholeNumbers ? 1f : Mathf.Max((s.maxValue - s.minValue) / 20f, 0.0001f);
                    return UiWidgets.Slide(s, Mathf.Clamp(s.value + (step < 0 ? -by : by), s.minValue, s.maxValue));
                }
                default:
                    return Choose(row, (w.Kit != 0 ? KitIndex(w.Kit) : Index(w.Drop)) + (step < 0 ? -1 : 1), false);
            }
        }

        /// <summary>A slider moved to a share of its track (the pointer's drag), through the slider.</summary>
        public bool Slide(int row, float share)
        {
            if (row < 0 || row >= Count || Rows[row].Kind != UiFormKind.Slider || Rows[row].Disabled || _widgets[row].Slide == null)
            {
                return false;
            }
            Slider s = _widgets[row].Slide;
            float value = Mathf.Lerp(s.minValue, s.maxValue, Mathf.Clamp01(share));
            return UiWidgets.Slide(s, s.wholeNumbers ? Mathf.Round(value) : value);
        }

        /// <summary>
        /// A press on the form: a choice opens or closes its list and an option is chosen through the drop-down, a switch or
        /// an option is set, a slider held or let go, a step, a line taken away, a list's button or an action, a colour.
        /// Returns false when the game's widget is not there or takes no press now.
        /// </summary>
        public bool Press(int command, int arg)
        {
            if (command == UiFormRows.OptionCommand)
            {
                int row = Pick.Row;
                Pick.Row = -1;
                return Choose(row, arg, true);
            }
            if (command == UiFormRows.SwatchCommand)
            {
                int row = UiFormRows.SwatchRow(arg), k = UiFormRows.SwatchIndex(arg);
                Button[] swatches = row >= 0 && row < Count ? _widgets[row].Swatches : null;
                return swatches != null && k < swatches.Length && UiWidgets.Press(swatches[k]);
            }
            if (arg < 0 || arg >= Count)
            {
                return false;
            }
            Widget w = _widgets[arg];
            ref UiFormRow r = ref Rows[arg];
            switch (command)
            {
                case UiFormRows.LessCommand:
                    return UiWidgets.Press(w.Less);
                case UiFormRows.MoreCommand:
                    return UiWidgets.Press(w.More);
                case UiFormRows.RemoveCommand:
                    return UiWidgets.Press(w.Remove);
                case UiFormRows.RowCommand:
                    return r.Kind == UiFormKind.Radio ? UiWidgets.Set(w.Toggle, true) : UiWidgets.Press(w.Press);
                case UiFormRows.FieldCommand:
                    switch (r.Kind)
                    {
                        case UiFormKind.Choice:
                            if (r.Disabled)
                            {
                                return false;
                            }
                            Pick.Row = Pick.Row == arg ? -1 : arg;
                            Pick.ListOffset = 0f;
                            if (Pick.Row >= 0)
                            {
                                Options(arg);
                            }
                            return true;
                        case UiFormKind.Switch:
                            return w.Toggle != null && UiWidgets.Set(w.Toggle, !w.Toggle.isOn);
                        case UiFormKind.Slider:
                            if (r.Disabled)
                            {
                                return false;
                            }
                            Pick.Held = Pick.Held == arg ? -1 : arg;
                            return true;
                        default:
                            return false;
                    }
                default:
                    return false;
            }
        }

        /// <summary>Back on the form: the open list closes, or the held slider is let go. False when neither was.</summary>
        public bool Back()
        {
            if (Pick.Row >= 0)
            {
                Pick.Row = -1;
                return true;
            }
            if (Pick.Held >= 0)
            {
                Pick.Held = -1;
                return true;
            }
            return false;
        }

        /// <summary>The form's values in a line each ("Amount  $5,000"), for the player's answer in the thread once it is sent.</summary>
        public string Summary()
        {
            string words = null;
            for (int i = 0; i < Count; i++)
            {
                ref UiFormRow r = ref Rows[i];
                string line = null;
                switch (r.Kind)
                {
                    case UiFormKind.Choice:
                    case UiFormKind.Amount:
                    case UiFormKind.Text:
                    case UiFormKind.Slider:
                        line = string.IsNullOrEmpty(r.Value) ? null : string.IsNullOrEmpty(r.Label) ? r.Value : r.Label + ": " + r.Value;
                        break;
                    case UiFormKind.Radio:
                    case UiFormKind.Switch:
                        line = r.On && !string.IsNullOrEmpty(r.Label) ? r.Label : null;
                        break;
                }
                if (line != null)
                {
                    words = words == null ? line : words + "\n" + line;
                }
            }
            return words;
        }

        // ------------------------------------------------------------------------------------------------- the walk

        private void Publish()
        {
            if (Rows.Length < _rows.Count)
            {
                Rows = new UiFormRow[Math.Max(_rows.Count, Rows.Length * 2)];
            }
            _rows.CopyTo(Rows);
            Count = _rows.Count;
        }

        private int Add(UiFormRow row, Widget widget)
        {
            if (_rows.Count >= DialogRules.MostRows)
            {
                return -1;
            }
            _rows.Add(row);
            _widgets.Add(widget);
            return _rows.Count - 1;
        }

        // A container's pieces in order. A label waits for the control after it (or the group it opens, as a heading); a
        // text after a control names it when it has no name (a delivery's goods, the vehicle's deliver to). Two texts alone
        // in a group are a figure; a text left alone a note.
        private void Walk(Transform parent, int depth)
        {
            if (depth > 0 && Figure(parent))
            {
                return;
            }
            string pending = null, pendingName = null;
            int lastControl = -1;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                Piece piece = Classify(c);
                switch (piece)
                {
                    case Piece.None:
                        break;
                    case Piece.Spacer:
                        Note(ref pending, pendingName);
                        lastControl = -1;
                        break;
                    case Piece.Splitter:
                        Note(ref pending, pendingName);
                        Add(new UiFormRow { Kind = UiFormKind.Break }, default);
                        lastControl = -1;
                        break;
                    case Piece.Text:
                    {
                        string words = Words(c), inner = Inner(c);
                        if (inner != null)
                        {
                            _carried = inner;
                        }
                        if (string.IsNullOrEmpty(words) || Echo(c, lastControl))
                        {
                            break;
                        }
                        if (pending == null && lastControl >= 0 && Name(lastControl, words))
                        {
                            break;
                        }
                        Note(ref pending, pendingName);
                        pending = words;
                        pendingName = c.name;
                        lastControl = -1;
                        break;
                    }
                    case Piece.Swatch:
                    {
                        int at = i;
                        SwatchesFound.Clear();
                        KeysFound.Clear();
                        int chosen = -1;
                        for (; at < parent.childCount; at++)
                        {
                            Transform s = parent.GetChild(at);
                            Piece next = Classify(s);
                            if (next == Piece.None)
                            {
                                continue;
                            }
                            if (next != Piece.Swatch)
                            {
                                break;
                            }
                            Transform selected = s.Find("Selected");
                            chosen = selected != null && selected.gameObject.activeSelf ? SwatchesFound.Count : chosen;
                            Image image = s.GetComponent<Image>();
                            Color32 colour = image != null ? (Color32)image.color : new Color32(255, 255, 255, 255);
                            SwatchesFound.Add(s.GetComponent<Button>());
                            KeysFound.Add(DialogRules.Swatch(colour.r, colour.g, colour.b));
                        }
                        i = at - 1;
                        bool held = SwatchesFound.Count > 0 && !SwatchesFound[0].interactable;
                        lastControl = Add(new UiFormRow { Kind = UiFormKind.Swatches, Label = pending, Swatches = KeysFound.ToArray(), Chosen = chosen, Disabled = held },
                            new Widget { Swatches = SwatchesFound.ToArray() });
                        pending = null;
                        break;
                    }
                    case Piece.Group:
                    {
                        int before = _rows.Count;
                        Walk(c, depth + 1);
                        int made = _rows.Count - before;
                        if (pending != null)
                        {
                            if (made == 1 && Take(before, pending))
                            {
                                pending = null;
                            }
                            else if (made > 0)
                            {
                                _rows.Insert(before, new UiFormRow { Kind = UiFormKind.Heading, Label = pending });
                                _widgets.Insert(before, default);
                                pending = null;
                            }
                        }
                        lastControl = -1;
                        break;
                    }
                    default:
                        lastControl = Control(c, piece, pending);
                        pending = null;
                        break;
                }
            }
            Note(ref pending, pendingName);
        }

        // A group of two lines alone (a fee and its figure): one figure row, the total's words strong; a sentence for a
        // figure (a fund's risk) reads as a heading and its note.
        private bool Figure(Transform group)
        {
            Transform a = null, b = null;
            for (int i = 0; i < group.childCount; i++)
            {
                Transform c = group.GetChild(i);
                Piece piece = Classify(c);
                if (piece == Piece.None || piece == Piece.Spacer)
                {
                    continue;
                }
                if (piece != Piece.Text || b != null)
                {
                    return false;
                }
                if (a == null)
                {
                    a = c;
                }
                else
                {
                    b = c;
                }
            }
            if (b == null)
            {
                return false;
            }
            string label = ContactsRules.Label(Words(a)), figure = Words(b);
            if (DialogRules.LongFigure(figure))
            {
                Add(new UiFormRow { Kind = UiFormKind.Heading, Label = label }, default);
                Add(new UiFormRow { Kind = UiFormKind.Note, Label = figure, Warn = DialogRules.Warning(b.name) }, default);
                return true;
            }
            Add(new UiFormRow
            {
                Kind = UiFormKind.Figure, Label = label, Value = figure, Hint = string.IsNullOrEmpty(figure) ? UiText.T("reallife_ui_dialog_figure_unset") : null,
                Strong = DialogRules.Total(group.name)
            }, default);
            return true;
        }

        // A group that made one row takes the label before it: a control without a name, a colour row, a list's button (an entry).
        private bool Take(int row, string label)
        {
            UiFormRow r = _rows[row];
            if (r.Kind == UiFormKind.Action)
            {
                r.Kind = UiFormKind.Entry;
                r.Label = label;
                _rows[row] = r;
                return true;
            }
            if (!string.IsNullOrEmpty(r.Label) || r.Kind == UiFormKind.Heading || r.Kind == UiFormKind.Note || r.Kind == UiFormKind.Break || r.Kind == UiFormKind.Figure)
            {
                return false;
            }
            r.Label = ContactsRules.Label(label);
            _rows[row] = r;
            return true;
        }

        // A text after a control with no name of its own names it; a slider's figure the game writes beside it is its value.
        private bool Name(int row, string words)
        {
            UiFormRow r = _rows[row];
            if (string.IsNullOrEmpty(r.Label) && r.Kind != UiFormKind.Slider)
            {
                r.Label = DialogRules.OneLine(words);
                _rows[row] = r;
                return true;
            }
            if (r.Kind == UiFormKind.Slider && string.IsNullOrEmpty(r.Value))
            {
                r.Value = words;
                _rows[row] = r;
                return true;
            }
            return false;
        }

        // The slider's own figure, which the game writes into a text beside it: already its value, never a second line.
        private bool Echo(Transform text, int row)
        {
            if (row < 0 || row >= _widgets.Count || _widgets[row].Slide == null)
            {
                return false;
            }
            var label = _widgets[row].Slide.GetComponent<global::UI.Elements.SliderToLabel>();
            return label != null && label.label != null && label.label.transform == text;
        }

        // A lone line: a note, or the figure a label carried from higher up names (a wholesale contract's fee).
        private void Note(ref string pending, string name)
        {
            if (pending == null)
            {
                return;
            }
            if (_carried != null && !DialogRules.LongFigure(pending))
            {
                Add(new UiFormRow { Kind = UiFormKind.Figure, Label = ContactsRules.Label(_carried), Value = pending, Strong = DialogRules.Total(name) }, default);
                _carried = null;
            }
            else
            {
                Add(new UiFormRow { Kind = UiFormKind.Note, Label = pending, Warn = DialogRules.Warning(name) }, default);
            }
            pending = null;
        }

        // A control's row, named by the label before it.
        private int Control(Transform c, Piece piece, string label)
        {
            string name = label != null ? ContactsRules.Label(label) : null;
            switch (piece)
            {
                case Piece.Choice:
                {
                    var drop = c.GetComponent<global::UI.Elements.Dropdown>();
                    bool chosen = drop.SelectedOptionIndex >= 0;
                    string shown = Shown(drop);
                    return Add(new UiFormRow
                    {
                        Kind = UiFormKind.Choice, Label = name, Value = chosen ? shown : null, Hint = chosen ? null : shown, Disabled = !Open(drop)
                    }, new Widget { Drop = drop });
                }
                case Piece.Amount:
                {
                    var selector = c.GetComponent<global::UI.AmountSelector>();
                    var input = GameContract.DialogAmountField.Field?.GetValue(selector) as TMP_InputField;
                    var less = GameContract.DialogAmountLess.Field?.GetValue(selector) as Button;
                    var more = GameContract.DialogAmountMore.Field?.GetValue(selector) as Button;
                    var remove = GameContract.DialogAmountRemove.Field?.GetValue(selector) as Button;
                    return Add(new UiFormRow
                    {
                        Kind = UiFormKind.Amount, Label = name, Value = input != null ? input.text : null, Hint = Placeholder(input),
                        CanLess = UiWidgets.Shows(less), CanMore = UiWidgets.Shows(more), Remove = remove != null && remove.gameObject.activeInHierarchy,
                        Disabled = input == null || !input.interactable
                    }, new Widget { Input = input, Less = less, More = more, Remove = remove, EndsEdit = true });
                }
                case Piece.Field:
                {
                    var input = c.GetComponent<TMP_InputField>();
                    Button less = Sibling(c, true), more = Sibling(c, false);
                    bool steps = less != null || more != null;
                    string hint = Placeholder(input);
                    return Add(new UiFormRow
                    {
                        Kind = steps ? UiFormKind.Amount : UiFormKind.Text, Label = name ?? hint, Value = input.text, Hint = name != null ? hint : null,
                        CanLess = UiWidgets.Shows(less), CanMore = UiWidgets.Shows(more), Disabled = !input.interactable
                    }, new Widget { Input = input, Less = less, More = more });
                }
                case Piece.Slider:
                {
                    var slider = c.GetComponent<Slider>();
                    var said = c.GetComponent<global::UI.Elements.SliderToLabel>();
                    TMP_Text figure = said != null && said.label != null ? said.label.GetComponent<TMP_Text>() : null;
                    return Add(new UiFormRow
                    {
                        Kind = UiFormKind.Slider, Label = name, Value = figure != null ? UiWidgets.Text(figure) : null, Share = slider.normalizedValue,
                        Disabled = !slider.interactable
                    }, new Widget { Slide = slider });
                }
                case Piece.Switch:
                case Piece.Radio:
                {
                    var toggle = c.GetComponent<Toggle>();
                    string own = piece == Piece.Radio ? DialogRules.OneLine(UiWidgets.Label(toggle)) : null;
                    return Add(new UiFormRow
                    {
                        Kind = piece == Piece.Radio ? UiFormKind.Radio : UiFormKind.Switch, Label = own ?? name, On = toggle.isOn, Disabled = !toggle.interactable
                    }, new Widget { Toggle = toggle });
                }
                case Piece.Button:
                {
                    var button = c.GetComponent<Button>();
                    string words = DialogRules.OneLine(UiWidgets.Label(button));
                    return Add(new UiFormRow
                    {
                        Kind = label != null ? UiFormKind.Entry : UiFormKind.Action, Label = label, Action = words, Disabled = !button.interactable
                    }, new Widget { Press = button });
                }
                default:
                    return -1;
            }
        }

        // No hairline at the form's ends or twice in a row, and none straight under a heading.
        private void Tidy()
        {
            for (int i = _rows.Count - 1; i >= 0; i--)
            {
                if (_rows[i].Kind != UiFormKind.Break)
                {
                    continue;
                }
                bool edge = i == 0 || i == _rows.Count - 1;
                bool twice = i + 1 < _rows.Count && _rows[i + 1].Kind == UiFormKind.Break;
                bool underHeading = i > 0 && _rows[i - 1].Kind == UiFormKind.Heading;
                if (edge || twice || underHeading)
                {
                    _rows.RemoveAt(i);
                    _widgets.RemoveAt(i);
                }
            }
        }

        private static Piece Classify(Transform c)
        {
            if (c == null || !c.gameObject.activeSelf)
            {
                return Piece.None;
            }
            if (c.GetComponent<global::UI.Elements.Dropdown>() != null)
            {
                return Piece.Choice;
            }
            if (c.GetComponent<global::UI.AmountSelector>() != null)
            {
                return Piece.Amount;
            }
            if (c.GetComponent<Slider>() != null)
            {
                return Piece.Slider;
            }
            Toggle toggle = c.GetComponent<Toggle>();
            if (toggle != null)
            {
                return toggle.group != null ? Piece.Radio : Piece.Switch;
            }
            if (c.GetComponent<TMP_InputField>() != null)
            {
                return Piece.Field;
            }
            Button button = c.GetComponent<Button>();
            if (button != null)
            {
                if (DialogRules.Less(c.name) || DialogRules.More(c.name))
                {
                    return Piece.None;   // a figure's steps, taken with its field
                }
                return c.GetComponentInChildren<TMP_Text>(false) == null ? Piece.Swatch : Piece.Button;
            }
            TMP_Text words = c.GetComponent<TMP_Text>();
            if (words != null && c.GetComponentInChildren<Selectable>(false) == null)
            {
                if (!words.enabled)
                {
                    return c.childCount > 0 ? Piece.Group : Piece.None;   // words the game switched off (a design's missing mods warning)
                }
                return Piece.Text;
            }
            if (DialogRules.Splitter(c.name))
            {
                return Piece.Splitter;
            }
            if (c.childCount == 0 || DialogRules.Spacer(c.name))
            {
                return Piece.Spacer;
            }
            return Piece.Group;
        }

        private static string Words(Transform t)
        {
            TMP_Text text = t != null ? t.GetComponent<TMP_Text>() : null;
            return text != null ? ContactsRules.Plain(text.text) : null;
        }

        // A label the game hangs under another one, placed beside a lone figure further down; null for none.
        private static string Inner(Transform text)
        {
            for (int i = 0; i < text.childCount; i++)
            {
                Transform c = text.GetChild(i);
                TMP_Text t = c.gameObject.activeSelf ? c.GetComponent<TMP_Text>() : null;
                if (t != null && t.enabled)
                {
                    string words = ContactsRules.Plain(t.text);
                    if (!string.IsNullOrEmpty(words))
                    {
                        return words;
                    }
                }
            }
            return null;
        }

        private static string Placeholder(TMP_InputField input)
        {
            string words = input != null && input.placeholder is TMP_Text t ? ContactsRules.Plain(t.text) : null;
            return string.IsNullOrEmpty(words) ? null : words;
        }

        // A figure's step beside its field, by the game's names for them.
        private static Button Sibling(Transform field, bool less)
        {
            Transform parent = field.parent;
            for (int i = 0; parent != null && i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c.gameObject.activeSelf && (less ? DialogRules.Less(c.name) : DialogRules.More(c.name)))
                {
                    return c.GetComponent<Button>();
                }
            }
            return null;
        }

        // The drop-down's words for its choice as it shows them, or its placeholder.
        private static string Shown(global::UI.Elements.Dropdown drop)
        {
            var label = GameContract.DialogDropShown.Field?.GetValue(drop) as TMP_Text;
            if (label != null)
            {
                return ContactsRules.Plain(label.text);
            }
            return drop.SelectedOption;
        }

        private static bool Open(global::UI.Elements.Dropdown drop)
        {
            var group = GameContract.DialogDropGroup.Field?.GetValue(drop) as CanvasGroup;
            return group == null || group.interactable;
        }

        private static int Index(global::UI.Elements.Dropdown drop) => drop != null ? drop.SelectedOptionIndex : -1;

        // The open choice's options in the game's words, as its own list shows them.
        private void Options(int row)
        {
            if (_widgets[row].Kit != 0)
            {
                KitOptions(_widgets[row].Kit);
                return;
            }
            var drop = _widgets[row].Drop;
            var options = drop != null && GameContract.DialogDropOptions.Field != null ? GameContract.DialogDropOptions.Field.GetValue(drop) as List<string> : null;
            bool localised = drop != null && GameContract.DialogDropLocalised.Field != null && GameContract.DialogDropLocalised.Field.GetValue(drop) is bool l && l;
            int n = options != null ? options.Count : 0;
            if (Pick.Options.Length < n)
            {
                Pick.Options = new string[Math.Max(n, 8)];
            }
            for (int i = 0; i < n; i++)
            {
                string o = options[i] ?? string.Empty;
                Pick.Options[i] = ContactsRules.Plain(localised ? o.GetLocalization() : o);
            }
            Pick.Count = n;
            Pick.Chosen = Index(drop);
        }

        // An option chosen through the drop-down's own SelectOption, so its listeners (the form's prices, the next fields) run.
        private bool Choose(int row, int option, bool exact)
        {
            if (row < 0 || row >= Count || Rows[row].Kind != UiFormKind.Choice || Rows[row].Disabled)
            {
                return false;
            }
            if (_widgets[row].Kit != 0)
            {
                return KitChoose(_widgets[row].Kit, option, exact);
            }
            var drop = _widgets[row].Drop;
            var options = drop != null && GameContract.DialogDropOptions.Field != null ? GameContract.DialogDropOptions.Field.GetValue(drop) as List<string> : null;
            int n = options != null ? options.Count : 0;
            if (n == 0)
            {
                return false;
            }
            if (!exact)
            {
                option = Mathf.Clamp(option, 0, n - 1);
            }
            if (option < 0 || option >= n || (option == drop.SelectedOptionIndex && !exact))
            {
                return false;
            }
            drop.SelectOption(option);
            return true;
        }

        // ------------------------------------------------------------------------------------------------- the signature

        private static long Sign(Transform parent, int depth)
        {
            long h = parent.childCount;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (!c.gameObject.activeSelf)
                {
                    h = h * 3L + 1L;
                    continue;
                }
                h = h * 31L + Leaf(c, out bool widget);
                if (!widget && depth < 8)
                {
                    h = h * 17L + Sign(c, depth + 1);
                }
            }
            return h;
        }

        // One piece's state: its words, its figure, its toggle, its choice, whether it takes a press.
        private static long Leaf(Transform c, out bool widget)
        {
            widget = true;
            var drop = c.GetComponent<global::UI.Elements.Dropdown>();
            if (drop != null)
            {
                return drop.SelectedOptionIndex * 7L + (Open(drop) ? 1L : 0L);
            }
            var input = c.GetComponent<TMP_InputField>();
            if (input != null)
            {
                return (input.text != null ? input.text.GetHashCode() : 0) * 3L + (input.interactable ? 1L : 0L);
            }
            var slider = c.GetComponent<Slider>();
            if (slider != null)
            {
                return (long)(slider.value * 1000f) * 3L + (slider.interactable ? 1L : 0L);
            }
            var toggle = c.GetComponent<Toggle>();
            if (toggle != null)
            {
                return (toggle.isOn ? 2L : 0L) + (toggle.interactable ? 1L : 0L);
            }
            widget = false;
            long h = 0L;
            var text = c.GetComponent<TMP_Text>();
            if (text != null)
            {
                h = (text.text != null ? text.text.GetHashCode() : 0) * 2L + (text.enabled ? 1L : 0L);
            }
            var button = c.GetComponent<Button>();
            if (button != null)
            {
                h = h * 3L + (button.interactable ? 1L : 0L);
            }
            return h;
        }
    }
}
