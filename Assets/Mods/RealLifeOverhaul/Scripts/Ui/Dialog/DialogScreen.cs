using System;
using System.Collections.Generic;
using Characters.EmojiSystem;
using Entities;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UI.Dialog;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's dialogue panel as the kit's conversation docked at the right third of the screen over the live scene:
    /// who is talking at its head, the lines said so far as a thread (theirs, and the answers the player gave on the kit),
    /// the line now with its buttons as numbered choices on 1 to 9 (the confirm first, Leave last), and the form the line
    /// asks for in the conversation's form slot, read from the game's own widgets (DialogForm) and pressed through them,
    /// so the dialog reads its values back as it always has. A casino's game, and a form with nothing the kit can show,
    /// are answered in the game's own panel, the kit back the moment that line is answered; a list of the game's own a
    /// form's button opens (a delivery's goods) is answered there too, the kit back the moment it closes; another mod's
    /// piece in the panel is offered as the game's own view until the panel closes. Cost: a look at a signature four times
    /// a second while it shows; a read and a layout only when it changed.
    /// </summary>
    public sealed class DialogScreen : IUiScreen, IUiTyping, IUiStepping, IUiDragging
    {
        private const float Every = 0.25f;                     // seconds between looks at the game's panel
        private const float FormShare = 0.45f;                 // of the room over the status bar, the most a form takes before it scrolls
        private const string Entries = "Entries (Scroll View)";
        private static readonly int[] Order = new int[3];

        private readonly DialogUI _ui;
        private readonly Transform _panel;
        private readonly UiConversationModel _m = new UiConversationModel();
        private readonly DialogForm _form = new DialogForm();
        private readonly List<Button> _choices = new List<Button>(4);   // the numbered choices' buttons; null for the game's own view or the dialog's own answer
        private readonly List<DialogExtra> _extraOf = new List<DialogExtra>(4);   // by choice: the dialog's own answer past the game's three; null for a button
        private readonly List<Button> _buttons = new List<Button>(4);
        private readonly List<UiDecisionChoice> _made = new List<UiDecisionChoice>(4);
        private readonly List<UiMessageModel> _lines = new List<UiMessageModel>(32);
        private readonly UiPrompt[] _prompts = new UiPrompt[3];
        private static readonly UiFormField[] NoFields = new UiFormField[0];
        private readonly List<KeyValuePair<int, string>> _said = new List<KeyValuePair<int, string>>(32);   // a line's copy and the answer the player gave it here
        private readonly List<AsideLine> _asides = new List<AsideLine>(8);   // answers taken on a line itself and what was said back, by the line's copy
        private Transform _content;
        private Button _leave;
        private bool _dirty = true, _formShown;
        private int _due;                  // the frame after a press, when the game's answer to it is read
        private float _next;
        private long _seen;
        private int _typing = -1, _dragging = -1, _shownLines = -1;
        private object _dialog;            // the conversation the game runs now, as last seen
        private object _asideFor;          // the line the game's own panel answers now

        // An answer of the dialog's own taken on the line itself, or the words said back to it.
        private struct AsideLine
        {
            public int Line;
            public UiFrom From;
            public string Text;
        }

        public DialogScreen(DialogUI ui, Transform panel)
        {
            _ui = ui;
            _panel = panel;
        }

        public string Id => DialogKits.DialogId;

        public bool Dirty
        {
            get
            {
                if (_due > 0 && Time.frameCount >= _due)
                {
                    _due = 0;
                    _dirty = true;
                }
                float now = Time.unscaledTime;
                if (now >= _next)
                {
                    _next = now + Every;
                    long sig = Signature();
                    _dirty |= sig != _seen;
                    _seen = sig;
                }
                return _dirty;
            }
        }

        public int TypingCommand => _typing >= 0 ? UiFormRows.FieldCommand : -1;

        /// <summary>The line shows an answer besides Leave (the interface's tests press the first one).</summary>
        internal bool HasAnswer => _made.Count > (_leave != null ? 1 : 0);

        public string TypingText => _form.Text(_typing);

        public void Typed(string text)
        {
            if (_typing >= 0 && _form.Type(_typing, text ?? string.Empty))
            {
                Soon();
            }
        }

        public bool Steps(int command) => command == UiFormRows.FieldCommand;

        public void Step(int command, int arg, int step)
        {
            // A slider steps while the keys hold it, so the d-pad still crosses a form of sliders.
            if (arg < 0 || arg >= _form.Count || (_form.Rows[arg].Kind == UiFormKind.Slider && _form.Pick.Held != arg))
            {
                return;
            }
            if (_form.Step(arg, step))
            {
                Soon();
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            _form.Pick.Most = UiDraw.SnapDown((frame.BandY - UiTokens.S4 * 2f) * FormShare);
            Build();
            UiConversation.LayDocked(s, _m, frame, dark, keys);
            _dirty = false;
            _seen = Signature();
            _next = Time.unscaledTime + Every;
        }

        public bool Press(int command, int arg)
        {
            if (command == UiConversation.ChoiceCommand)
            {
                return Choose(arg);
            }
            bool kit = command == UiFormRows.FieldCommand && arg >= 0 && arg < _form.Count
                       && (_form.Rows[arg].Kind == UiFormKind.Choice || _form.Rows[arg].Kind == UiFormKind.Slider);
            bool done = _form.Press(command, arg);
            if (kit || command == UiFormRows.OptionCommand)
            {
                _dirty = true;   // the open list or the held slider is the kit's own: drawn this frame
            }
            if (done && command == UiFormRows.RowCommand)
            {
                GameList();
            }
            if (!kit)
            {
                Soon();
            }
            return done;
        }

        public void Focused(int command, int arg)
        {
            _typing = command == UiFormRows.FieldCommand && _form.Typed(arg) ? arg : -1;   // the field the next typing goes into
        }

        public bool Back()
        {
            if (_form.Back())
            {
                _dirty = true;
                return true;
            }
            // Leave, as the game's own Esc presses it (DialogUI.OnCancel): taken here, so it is pressed once.
            if (_leave != null && UiWidgets.Shows(_leave))
            {
                return Answer(_leave, "Leave");
            }
            return false;
        }

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
            if (list >= 0 && list == _form.Pick.List)
            {
                _form.Pick.Reveal = index;
                _dirty = true;
            }
        }

        public void Wheel(int clip, float notches)
        {
            float by = -notches * UiTokens.S16;
            UiFormPick pick = _form.Pick;
            if (clip >= 0 && clip == pick.ListClip)
            {
                pick.ListOffset = Math.Max(0f, pick.ListOffset + by);
            }
            else if (clip >= 0 && clip == pick.Clip)
            {
                pick.Offset = Math.Max(0f, pick.Offset + by);
            }
            else
            {
                _m.Offset = Math.Max(0f, (_m.Offset < 0f ? 100000f : _m.Offset) + by);
            }
            _dirty = true;
        }

        public void PointerDown(int command, int arg, float x, float y)
        {
            if (command == UiFormRows.FieldCommand)
            {
                Slide(arg, x);
            }
        }

        public bool DragBegin(int command, int arg, float x, float y)
        {
            _dragging = command == UiFormRows.FieldCommand && arg >= 0 && arg < _form.Count && _form.Rows[arg].Kind == UiFormKind.Slider ? arg : -1;
            if (_dragging >= 0)
            {
                Slide(_dragging, x);
            }
            return _dragging >= 0;
        }

        public void DragMove(float x, float y)
        {
            if (_dragging >= 0)
            {
                Slide(_dragging, x);
            }
        }

        public void DragEnd(bool cancelled)
        {
            _dragging = -1;
        }

        // ------------------------------------------------------------------------------------------------- presses

        // The game reads the press's answer in its own frame (a list's lines rebuilt, the next line made): read it in the next.
        private void Soon()
        {
            _due = Time.frameCount + 1;
        }

        private bool Choose(int arg)
        {
            if (arg < 0 || arg >= _choices.Count)
            {
                return false;
            }
            if (_extraOf[arg] != null)
            {
                return Extra(_extraOf[arg]);
            }
            Button b = _choices[arg];
            if (b == null)
            {
                Log("another mod's piece in the panel: the game's own view until it closes");
                DialogKits.Classic();
                return true;
            }
            return Answer(b, "a reply");
        }

        // A button of the line pressed as a click presses it; the player's answer kept for the thread (the form's values for a form).
        private bool Answer(Button b, string what)
        {
            CanvasGroup view = View();
            string words = _formShown && !ReferenceEquals(b, _leave) ? _form.Summary() : null;
            words = words ?? UiWidgets.Label(b);
            if (!UiWidgets.Press(b))
            {
                Log(what + " was not shown by the game, so nothing was pressed");
                return false;
            }
            Log(what + ": the game's own button");
            if (view != null && !string.IsNullOrEmpty(words))
            {
                _said.Add(new KeyValuePair<int, string>(view.GetInstanceID(), words));
            }
            Soon();
            return true;
        }

        // An answer of the dialog's own past the game's three: done on the line (what is said back joins the thread), or the
        // dialog's next line shown through the game's own ShowEntry, the answer kept for the thread as a button's is.
        private bool Extra(DialogExtra extra)
        {
            CanvasGroup view = View();
            int line = view != null ? view.GetInstanceID() : 0;
            if (!DialogChoices.Take(extra, _ui, out bool moved, out string back))
            {
                Log("the dialog's own answer '" + extra.Name + "' could not be taken now");
                return false;
            }
            if (moved)
            {
                if (view != null)
                {
                    _said.Add(new KeyValuePair<int, string>(line, DialogRules.OneLine(extra.Name)));
                }
                Log("the dialog's own answer '" + extra.Name + "': its next line, shown by the game");
                Soon();
                return true;
            }
            _asides.Add(new AsideLine { Line = line, From = UiFrom.Me, Text = DialogRules.OneLine(extra.Name) });
            if (!string.IsNullOrEmpty(back))
            {
                _asides.Add(new AsideLine { Line = line, From = UiFrom.Them, Text = back });
            }
            Log("the dialog's own answer '" + extra.Name + "', answered on the line");
            _dirty = true;
            return true;
        }

        private void Slide(int row, float x)
        {
            UiFormPick pick = _form.Pick;
            if (row < 0 || row >= _form.Count || _form.Rows[row].Kind != UiFormKind.Slider || pick.ControlW <= 0f)
            {
                return;
            }
            float face = Mathf.Clamp01((x - pick.ControlX) / pick.ControlW);
            if (_form.Slide(row, UiDraw.SliderShare(pick.ControlW, _form.Rows[row].Value ?? string.Empty, face)))
            {
                Soon();
            }
        }

        private static void Log(string what)
        {
            UiDirector.Logger?.Info("Dialog (kit): " + what);
        }

        // ------------------------------------------------------------------------------------------------- the reading

        // What changing would change the screen: the conversation, its lines, the line now with its buttons and its form,
        // and anything another mod put in the panel.
        private long Signature()
        {
            if (_ui == null)
            {
                return 0L;
            }
            long sig = _ui.dialog != null ? _ui.dialog.GetHashCode() : 0;
            Transform content = Content();
            for (int i = 0; content != null && i < content.childCount; i++)
            {
                Transform c = content.GetChild(i);
                sig = sig * 31L + (c.gameObject.activeSelf ? c.GetInstanceID() : 1);
            }
            CanvasGroup view = View();
            if (view != null)
            {
                sig = sig * 31L + Hash(view.transform.Find("Message/MessageText")) + Hash(view.transform.Find("HeaderLabel")) * 7L;
                Transform row = view.transform.Find("Buttons");
                for (int i = 0; row != null && i < row.childCount; i++)
                {
                    Button b = row.GetChild(i).GetComponent<Button>();
                    sig = sig * 31L + (b != null && b.gameObject.activeSelf ? (b.interactable ? 3 : 2) + Hash(b.transform) : 1);
                }
                DialogEntry e = Entry();
                if (e != null && e.Template == DialogEntry.TemplateType.Input)
                {
                    sig = sig * 31L + DialogForm.Signature(FormNow());
                }
            }
            return sig * 31L + Foreign();
        }

        private static long Hash(Transform t)
        {
            TMP_Text text = t != null ? t.GetComponentInChildren<TMP_Text>(false) : null;
            return text != null && text.text != null ? text.text.GetHashCode() : 0;
        }

        private void Build()
        {
            _choices.Clear();
            _extraOf.Clear();
            _buttons.Clear();
            _made.Clear();
            _lines.Clear();
            _leave = null;
            _formShown = false;
            if (!ReferenceEquals(_dialog, _ui.dialog))
            {
                _dialog = _ui.dialog;
                _said.Clear();
                _asides.Clear();
                _form.Clear();
                _shownLines = -1;
                _asideFor = null;
            }
            CanvasGroup view = View();
            DialogEntry e = Entry();
            string speaker = null;
            int mood = -2;
            Transform content = Content();
            for (int i = 0; content != null && i < content.childCount; i++)
            {
                Transform c = content.GetChild(i);
                if (!c.gameObject.activeSelf)
                {
                    continue;   // the two templates the game copies each line from, and lines it took away
                }
                bool now = view != null && c == view.transform;
                bool asks = c.Find("InputTemplates") != null;
                if (!asks)
                {
                    string words = Words(c.Find("Message/MessageText"));
                    if (!string.IsNullOrEmpty(words))
                    {
                        int m = MoodOf(c);
                        Add(new UiMessageModel { From = UiFrom.Them, Text = words, Mood = DialogRules.MoodIcon(m) });
                        mood = m;
                    }
                    speaker = Words(c.Find("HeaderLabel")) ?? speaker;
                }
                Asides(c);
                if (!now)
                {
                    string answer = Said(c);
                    if (answer != null)
                    {
                        Add(new UiMessageModel { From = UiFrom.Me, Text = answer });
                    }
                }
            }
            Head(speaker, mood);
            if (view != null)
            {
                Line(view, e);
            }
            int foreign = Foreign();
            if (foreign > 0)
            {
                _choices.Add(null);
                _extraOf.Add(null);
                _made.Add(new UiDecisionChoice { Name = UiText.T("reallife_ui_contacts_game_view") });
            }
            _m.Messages = _lines.ToArray();
            _m.MessageCount = _lines.Count;
            if (_lines.Count != _shownLines)
            {
                _shownLines = _lines.Count;
                _m.Offset = -1f;   // a new line: the thread shows its newest
            }
            _m.Choices = _made.ToArray();
            _m.Chosen = -1;
            _m.Typing = _made.Count == 0 && !_formShown;   // their next line is coming
            Prompts();
        }

        // Who is talking: the latest line's speaker as the game heads it, else the name the conversation gives its speaker;
        // under them where the talk is held (the contact it is with, the business the player stands in, or the street).
        private void Head(string speaker, int mood)
        {
            Contact c = _ui.contact;
            string with = c != null ? ContactName(c) : null;
            string name = speaker ?? Speaker() ?? with;
            string where = with ?? Place();
            _m.Name = name ?? UiText.T("reallife_ui_dialog_title");
            _m.Hue = UiColour.HueOf(_m.Name);
            _m.Ring = ContactsRules.MoodRing(mood);
            _m.Role = where != null && where != _m.Name ? where : null;
            string word = DialogRules.MoodWord(mood);   // the mood in a word beside the face, in the ring's colour
            _m.Status = word != null ? UiText.T(word) : null;
            _m.StatusTone = _m.Ring;
            _m.Title = UiText.T("reallife_ui_dialog_title");
            _m.ThreadStart = null;
            _m.EmptyTitle = null;
            _m.EmptyBody = null;
            _m.Composer = false;
        }

        // The line now: its form, then its buttons as numbered choices (the confirm first, the second option, the dialog's own
        // answers past the game's three, Leave last), each with the lines the dialog told of it. The form is read first, as
        // reading the bank's loan tells its line what taking it costs.
        private void Line(CanvasGroup view, DialogEntry e)
        {
            Form(view, e);
            Transform row = view.transform.Find("Buttons");
            for (int i = 0; row != null && row.gameObject.activeSelf && i < row.childCount; i++)
            {
                Button b = row.GetChild(i).GetComponent<Button>();
                if (b != null && b.gameObject.activeSelf)
                {
                    _buttons.Add(b);
                }
            }
            bool hasCancel = e != null && e.OnCancel != null, hasSecond = e != null && e.OnSecondOption != null, hasConfirm = e != null && e.OnConfirm != null;
            if (_buttons.Count != (hasCancel ? 1 : 0) + (hasSecond ? 1 : 0) + (hasConfirm ? 1 : 0))
            {
                hasCancel = hasSecond = hasConfirm = false;   // a row the game's line does not explain (another mod's button in it): each a reply, in its order
            }
            if (hasCancel || hasSecond || hasConfirm)
            {
                DialogNotes notes = DialogAnswers.Of(e);
                int n = ContactsRules.ChoiceOrder(hasCancel, hasSecond, hasConfirm, true, Order), k = 0;
                if (hasConfirm && k < n)
                {
                    Choice(_buttons[Order[k++]], DialogChoices.Lines(notes, DialogButton.Confirm));
                }
                if (hasSecond && k < n)
                {
                    Choice(_buttons[Order[k++]], DialogChoices.Lines(notes, DialogButton.Second));
                }
                for (int i = 0; notes != null && i < notes.More.Count; i++)
                {
                    _choices.Add(null);
                    _extraOf.Add(notes.More[i]);
                    _made.Add(DialogChoices.Choice(notes.More[i]));
                }
                if (hasCancel && k < n)
                {
                    Choice(_buttons[Order[k++]], DialogChoices.Lines(notes, DialogButton.Cancel));
                }
                _leave = hasCancel ? _buttons[0] : null;
            }
            else
            {
                for (int i = 0; i < _buttons.Count; i++)
                {
                    Choice(_buttons[i], null);
                }
            }
        }

        private void Choice(Button b, UiConsequence[] lines)
        {
            _choices.Add(b);
            _extraOf.Add(null);
            _made.Add(new UiDecisionChoice { Name = DialogRules.OneLine(UiWidgets.Label(b)), Lines = lines, Disabled = !b.interactable });
        }

        // The line's form in the slot, read from the game's own widgets; a casino's game, or a form with nothing the kit can
        // show, answered in the game's own panel until the line is answered.
        private void Form(CanvasGroup view, DialogEntry e)
        {
            _m.Form = UiFormKind.None;
            _m.Fields = NoFields;
            _m.FieldCount = 0;
            _m.Rows = _form.Rows;
            _m.RowCount = 0;
            _m.Pick = _form.Pick;
            if (e == null || e.Template != DialogEntry.TemplateType.Input)
            {
                _form.Clear();
                _m.Rows = _form.Rows;
                return;
            }
            Transform form = FormNow();
            string question = Words(view.transform.Find("HeaderLabel"));
            bool casino = _ui.dialog != null && DialogRules.Casino(_ui.dialog.GetType().Name);
            if (form == null || casino || DialogRules.CasinoForm(form.name) || !_form.Read(form, question, e, _ui))
            {
                _form.Clear();
                _m.Rows = _form.Rows;
                Aside(e, form != null ? form.name : "none");
                return;
            }
            _m.Rows = _form.Rows;
            _m.RowCount = _form.Count;
            _formShown = true;
        }

        // The game's own panel answers this line; the kit is back the moment it is answered or the talk ends.
        private void Aside(DialogEntry e, string what)
        {
            if (ReferenceEquals(_asideFor, e))
            {
                return;
            }
            _asideFor = e;
            Log("the line's form '" + what + "' is answered in the game's own panel");
            DialogUI ui = _ui;
            object dialog = ui.dialog;
            float next = 0f;
            DialogKits.StepAside(() =>
            {
                float now = Time.unscaledTime;
                if (now < next)
                {
                    return false;
                }
                next = now + Every;
                return ui == null || ui.dialog == null || !ReferenceEquals(ui.dialog, dialog) || !ReferenceEquals(GameContract.DialogEntryNow.Field.GetValue(ui), e);
            });
        }

        // The game's own list a form's button opened over the panel (a delivery's goods to add): it is seen and answered in the
        // game's own view, and the kit is back the moment it closes; while the kit draws that list itself (the world's items
        // list, UiWorld.ItemsId) the panel stays and the list shows over it.
        private void GameList()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            global::ItemsList list = uis != null ? uis.itemsList : null;
            if (list == null || !list.gameObject.activeInHierarchy || WorldKits.Tool(UiWorld.ItemsId)?.Showing == true)
            {
                return;
            }
            Log("the game's own list over the form is answered in the game's own view");
            float next = 0f;
            DialogKits.StepAside(() =>
            {
                float now = Time.unscaledTime;
                if (now < next)
                {
                    return false;
                }
                next = now + Every;
                return list == null || !list.gameObject.activeInHierarchy || WorldKits.Tool(UiWorld.ItemsId)?.Showing == true;   // the kit took the list up
            });
        }

        // Move and Choose while the line has something to choose; Leave as the game's own Esc presses it, while it shows.
        // Between two lines the game shows no button, and the bar then promises nothing.
        private void Prompts()
        {
            int n = 0;
            if (_made.Count > 0 || _formShown)
            {
                _prompts[n++] = new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") };
                _prompts[n++] = new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_choose") };
            }
            if (_leave != null && UiWidgets.Shows(_leave))
            {
                _prompts[n++] = new UiPrompt { Action = UiAction.Back, Label = DialogRules.OneLine(UiWidgets.Label(_leave)) };
            }
            _m.Prompts = _prompts;
            _m.PromptCount = n;
        }

        private void Add(UiMessageModel line)
        {
            if (_lines.Count >= DialogRules.MostLines)
            {
                _lines.RemoveAt(0);   // the game keeps its last thirty lines; the thread keeps as many
            }
            _lines.Add(line);
        }

        // What was answered on a line itself and said back, after the line, in the order it was said.
        private void Asides(Transform entry)
        {
            if (_asides.Count == 0)
            {
                return;
            }
            CanvasGroup group = entry.GetComponent<CanvasGroup>();
            int id = group != null ? group.GetInstanceID() : 0;
            for (int i = 0; i < _asides.Count; i++)
            {
                if (_asides[i].Line == id)
                {
                    Add(new UiMessageModel { From = _asides[i].From, Text = _asides[i].Text });
                }
            }
        }

        private string Said(Transform entry)
        {
            CanvasGroup group = entry.GetComponent<CanvasGroup>();
            int id = group != null ? group.GetInstanceID() : 0;
            for (int i = _said.Count - 1; i >= 0; i--)
            {
                if (_said[i].Key == id)
                {
                    return _said[i].Value;
                }
            }
            return null;
        }

        // Another mod's piece in the panel beside the game's lines: offered as the game's own view.
        private int Foreign()
        {
            int n = 0;
            for (int i = 0; _panel != null && i < _panel.childCount; i++)
            {
                Transform c = _panel.GetChild(i);
                n += c.gameObject.activeSelf && c.name != Entries ? 1 : 0;
            }
            return n;
        }

        private Transform Content()
        {
            if (_content == null && GameContract.DialogTextTemplate.Field != null)
            {
                var template = GameContract.DialogTextTemplate.Field.GetValue(_ui) as CanvasGroup;
                _content = template != null ? template.transform.parent : null;
            }
            return _content;
        }

        private CanvasGroup View() => GameContract.DialogEntryView.Field != null ? GameContract.DialogEntryView.Field.GetValue(_ui) as CanvasGroup : null;

        private DialogEntry Entry() => GameContract.DialogEntryNow.Field != null ? GameContract.DialogEntryNow.Field.GetValue(_ui) as DialogEntry : null;

        private Transform FormNow()
        {
            var form = GameContract.DialogEntryForm.Field != null ? GameContract.DialogEntryForm.Field.GetValue(_ui) as Transform : null;
            return form != null && form.gameObject.activeInHierarchy ? form : null;
        }

        private static string Words(Transform t)
        {
            TMP_Text text = t != null && t.gameObject.activeInHierarchy ? t.GetComponent<TMP_Text>() : null;
            string words = text != null ? ContactsRules.Plain(text.text) : null;
            return string.IsNullOrEmpty(words) ? null : words.Trim();
        }

        private static string ContactName(Contact c) => Named(c.id);

        // The business the player stands in, or the street when the talk is with someone out in the city; null for neither.
        private string Place()
        {
            if (BuildingManager.IsInsideBuilding)
            {
                BuildingManager building = InstanceBehavior<BuildingManager>.Instance;
                string business = building != null && building.buildingRegistration != null ? building.buildingRegistration.BusinessName : null;
                return string.IsNullOrEmpty(business) ? null : business;
            }
            return _ui.npcInDialog != null ? UiText.T("reallife_ui_dialog_in_street") : null;
        }

        // The name the conversation gives its speaker (a dialog's own, the game's or the mod's), read only when the lines change.
        private string Speaker()
        {
            object dialog = _ui.dialog;
            return dialog != null && GameContract.DialogSpeaker.Field != null ? Named(GameContract.DialogSpeaker.Field.GetValue(dialog) as string) : null;
        }

        // A key's words, a name as it is, and nothing for an id that is neither.
        private static string Named(string key) =>
            DialogRules.SpeakerName(key, !string.IsNullOrEmpty(key) && Localizor.LocalizorManager.IsLocalizedKey(key) ? Localizor.LocalizorManager.GetLocalization(key) : null);

        // The face the game puts by a line (a negotiation's): content, unsure, angry; -2 for none or another.
        private static int MoodOf(Transform entry)
        {
            Transform face = entry.Find("Message/Mood/FacialExpression");
            Image image = face != null && face.gameObject.activeInHierarchy ? face.GetComponent<Image>() : null;
            if (image == null || image.sprite == null)
            {
                return -2;
            }
            try
            {
                return Faces(image.sprite);
            }
            catch (Exception ex) when (Guard.Missing(ex))
            {
                return -2;
            }
        }

        private static int Faces(Sprite face)
        {
            if (Is(face, CharacterEmojiName.CommonGreenHappy))
            {
                return 1;
            }
            if (Is(face, CharacterEmojiName.CommonYellowNeutral))
            {
                return 0;
            }
            return Is(face, CharacterEmojiName.CommonRedAngry) ? -1 : -2;
        }

        private static bool Is(Sprite face, CharacterEmojiName name)
        {
            CharacterEmoji emoji = CharacterEmojiSystem.GetCharacterEmojiByName(name);
            return emoji != null && emoji.icon == face;
        }
    }
}
