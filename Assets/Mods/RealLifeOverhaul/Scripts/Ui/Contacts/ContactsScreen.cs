using System;
using System.Collections.Generic;
using Entities;
using TMPro;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Contacts as a screen of the kit, mirrored from the game's own app (ContactsScreen.Read.cs reads it): its categories
    /// and the mod's groups as the window's sections, its contacts as a message list (the unread first, then the newest),
    /// the open contact's head, conversation, actions, profile and details, and a conversation the game runs on the app:
    /// an offer's negotiation in the thread with its form, a call as the call card over the dimmed world. Every press goes
    /// to the game: a contact opens through the game's own list (so its messages are read as the game reads them), View on
    /// map and Call are the game's buttons, an offer's reply is the game's own button in the thread, a line's reply and
    /// Hang up are the game's buttons under the line, a figure is typed into the game's field and stepped by its buttons,
    /// and the header's buttons are the game's pressed. Words are the game's keys and the mod's (registered in UiScreens).
    /// Cost: a look at a few counts four times a second while it shows; a layout only when they change (and once a second
    /// while a call's length shows).
    /// </summary>
    public sealed partial class ContactsScreen : IUiScreen, IUiTyping, IUiStepping
    {
        private const float Every = 0.25f;   // seconds between looks at the game's contacts

        private readonly ContactsApp _app;
        private readonly UiContactsModel _m = new UiContactsModel();
        private readonly List<Contact> _rows = new List<Contact>(64);
        private readonly List<int> _groups = new List<int>(16);          // the categories shown, the game's then the mod's
        private readonly List<Button> _services = new List<Button>(8);   // the aside's actions: the game's buttons, null for the game's own view
        private readonly List<Button> _choices = new List<Button>(4);    // the conversation's or the call's numbered choices
        private readonly List<Field> _fields = new List<Field>(2);       // the form's fields
        private readonly List<KeyValuePair<TMP_Text, Color>> _names = new List<KeyValuePair<TMP_Text, Color>>(2);   // the header's name labels as the game made them
        private string _query = string.Empty;
        private bool _dirty = true;
        private float _next;
        private long _seen;
        private int _typing = -1;          // the form's field the next typing goes into; -1 the search
        private int _second = -1;          // the call's length in whole seconds as last laid out
        private object _talk;              // the conversation the game runs now, as last seen
        private float _talkSince;          // when it began, real time
        private Button _call, _map, _hangUp;

        // A field of the form: the game's own figure field and its two steps.
        private struct Field
        {
            public TMP_InputField Input;
            public Button Less, More;
        }

        public ContactsScreen(ContactsApp app)
        {
            _app = app;
            AddHome(GameContract.KitContactInfo);
            AddHome(GameContract.KitEmployeeInfo);
        }

        public string Id => "contacts";

        public bool Dirty
        {
            get
            {
                float now = Time.unscaledTime;
                if (now >= _next)
                {
                    _next = now + Every;
                    long sig = Signature();
                    _dirty |= sig != _seen;
                    _seen = sig;
                }
                CallFormDue();
                if (_talk != null && !_dirty && _m.Call != null && (int)(now - _talkSince) != _second)
                {
                    _dirty = true;   // the call's length ticks on
                }
                return _dirty;
            }
        }

        public int TypingCommand => _typing >= 0 ? UiConversation.FormCommand : UiMessageList.SearchCommand;

        public string TypingText => _typing >= 0 ? (CallFormShown ? _callForm.Text(_typing) : _typing < _fields.Count && _fields[_typing].Input != null ? _fields[_typing].Input.text : string.Empty) : _query;

        public void Typed(string text)
        {
            if (_typing >= 0)
            {
                if (CallFormShown)
                {
                    CallFormType(_typing, text);   // the call's form: its own field, read back by the call's dialog
                }
                else if (_typing < _fields.Count)
                {
                    UiWidgets.Type(_fields[_typing].Input, text ?? string.Empty, false);   // the game's own field, read back by its conversation on Send
                }
                _dirty = true;
                return;
            }
            _query = text ?? string.Empty;
            _m.List.Offset = 0f;
            _dirty = true;
        }

        public bool Steps(int command) => command == UiConversation.FormCommand;

        public void Step(int command, int arg, int step)
        {
            if (CallFormShown)
            {
                CallFormStep(arg, step);
                _dirty = true;
                return;
            }
            Press(step < 0 ? UiConversation.FormLess : UiConversation.FormMore, arg);
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            _callForm.Pick.Most = UiDraw.SnapDown((frame.BandY - UiTokens.S8) * CallFormShare);
            Build();
            UiContacts.Lay(s, _m, frame, dark, keys);
            _second = _talk != null ? (int)(Time.unscaledTime - _talkSince) : -1;
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            _dirty = true;
            Contact open = _app.selectedContact;
            if (CallFormPress(command, arg, out bool formed))
            {
                return formed;   // the call's form: its widget's own press
            }
            switch (ContactsRoutes.Route(command, arg))
            {
                case ContactsRoute.Group:
                    if (arg < 0 || arg > _groups.Count)
                    {
                        return false;
                    }
                    _app.LoadContactsList(arg == 0 ? (ContactCategoryName?)null : (ContactCategoryName)_groups[arg - 1]);
                    _m.List.Offset = 0f;
                    return true;
                case ContactsRoute.Row:
                    return arg >= 0 && arg < _rows.Count && Open(_rows[arg]);
                case ContactsRoute.Map:
                    return Done(Click(_map), "view on map " + Who(open) + ": handed to the game");
                case ContactsRoute.Call:
                    return CallNow(open);
                case ContactsRoute.Service:
                    return Service(arg, open);
                case ContactsRoute.MarkRead:
                    return Done(Click(ButtonOf(GameContract.ContactsMarkAll)), "mark all as read: handed to the game");
                case ContactsRoute.DeleteAll:
                    return Done(Click(ButtonOf(GameContract.KitDeleteAll)), "delete all: handed to the game, which asks first");
                case ContactsRoute.Choice:
                case ContactsRoute.Reply:
                    return arg >= 0 && arg < _choices.Count && Done(Click(_choices[arg]), "a reply to " + Who(open) + ": the game's own button");
                case ContactsRoute.HangUp:
                    return Done(Click(_hangUp), "the call with " + Who(open) + " ended by the game's own button");
                case ContactsRoute.Clear:
                    _query = string.Empty;
                    _m.List.Offset = 0f;
                    return true;
                case ContactsRoute.Less:
                    return arg >= 0 && arg < _fields.Count && Click(_fields[arg].Less);
                case ContactsRoute.More:
                    return arg >= 0 && arg < _fields.Count && Click(_fields[arg].More);
                default:
                    return false;
            }
        }

        // One line a press in Player.log, so a report shows each button reaching the game (beta 3's Call left no trace).
        private static bool Done(bool done, string what)
        {
            UiDirector.Logger?.Info("Contacts (kit): " + what + (done ? string.Empty : " was not shown by the game, so nothing was pressed"));
            return done;
        }

        private static string Who(Contact c) => c != null ? Name(c) : "no contact";

        public void Focused(int command, int arg)
        {
            // A contact opens on a press only: the game marks its messages read as it opens, so the cursor passing never does.
            if (command == UiConversation.FormCommand)
            {
                _typing = !CallFormShown || CallFormTyped(arg) ? arg : -1;   // a call's form types into its figures and words alone   // the field the next typing goes into: one target a field, pressed after it takes focus
            }
            else if (command == UiMessageList.SearchCommand)
            {
                _typing = -1;
            }
        }

        public bool Back()
        {
            if (CallFormBack())
            {
                _dirty = true;
                return true;   // an open list closes, a held slider is let go, before the call ends
            }
            if (_m.Call != null && Shows(_hangUp))
            {
                return Press(UiCallCard.ButtonCommand, 0);   // Back on a call is its way out, as the game's Esc ends it
            }
            return false;
        }

        public void Tab(int step)
        {
            if (_m.Call != null)
            {
                return;
            }
            int at = ContactCategorySelection.SelectedCategory.HasValue ? _groups.IndexOf((int)ContactCategorySelection.SelectedCategory.Value) + 1 : 0;
            int count = _groups.Count + 1;
            int next = ((at + step) % count + count) % count;
            Press(UiAppWindow.NavCommand, next);
        }

        public bool Act(UiAction action)
        {
            if (_m.Call != null)
            {
                return false;
            }
            if (action == UiAction.Alt)
            {
                return Press(UiAppWindow.ActionCommand, ContactsRoutes.CallArg);
            }
            return action == UiAction.Option && Press(UiAppWindow.ActionCommand, ContactsRoutes.MapArg);
        }

        public void Reveal(int list, int index)
        {
            if (CallFormReveal(list, index))
            {
                _dirty = true;
                return;
            }
            if (list == _m.GroupsList)
            {
                _m.GroupsOffset = Math.Max(0f, (index - 2) * (UiTokens.Control + UiTokens.S3));
            }
            else
            {
                _m.List.Reveal = index;
            }
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            float by = -notches * UiTokens.S16;
            if (CallFormWheel(clip, notches))
            {
                _dirty = true;
                return;
            }
            if (clip == _m.GroupsClip)
            {
                _m.GroupsOffset = Math.Max(0f, _m.GroupsOffset + by);
            }
            else if (clip == _m.ListClip)
            {
                _m.List.Offset = Math.Max(0f, _m.List.Offset + by);
            }
            else if (clip == _m.TalkClip)
            {
                _m.Talk.Offset = Math.Max(0f, (_m.Talk.Offset < 0f ? 100000f : _m.Talk.Offset) + by);
            }
            else if (clip == _m.AsideClip)
            {
                _m.AsideOffset = Math.Max(0f, _m.AsideOffset + by);
            }
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------------- the game's pieces

        private bool Open(Contact c)
        {
            List<ContactModel> data = _app.contactScrollerController != null ? _app.contactScrollerController.data : null;
            for (int i = 0; data != null && i < data.Count; i++)
            {
                if (data[i] != null && data[i].contact == c)
                {
                    _app.ContactButtonOnClick(data[i]);   // the game's own list: the group shown stays
                    return true;
                }
            }
            _app.OpenAppWithContact(c);
            return true;
        }

        // The game's own Call: its call button's listener, which the game wires itself for every kind of contact (a business
        // by its opening hours, a person, an employee). A call that starts is drawn as the call card from its first line; a
        // call the game answers with a line ("closed now") stays in the thread, which shows that line.
        private bool CallNow(Contact open)
        {
            if (!Shows(_call))
            {
                return Done(false, "call " + Who(open));
            }
            _call.onClick.Invoke();
            bool talking = !_call.interactable;   // the game holds its call button while a call runs
            return Done(true, "call " + Who(open) + (talking ? ": the game's call, drawn as the call card" : ": the game answered in the thread"));
        }

        // An action beside the conversation: the game's own button, or the game's own view for what another mod added there.
        private bool Service(int arg, Contact open)
        {
            if (arg < 0 || arg >= _services.Count)
            {
                return false;
            }
            Button b = _services[arg];
            if (b == null)
            {
                ContactsKit.Classic();
                return Done(true, "the game's own view for another mod's buttons by " + Who(open));
            }
            return Done(Click(b), b.name + " for " + Who(open) + ": handed to the game");
        }

        private static bool Click(Button b)
        {
            if (!Shows(b))
            {
                return false;
            }
            b.onClick.Invoke();
            return true;
        }

        private static bool Shows(Button b) => b != null && b.gameObject.activeSelf && b.interactable;

        private Button ButtonOf(GameMember member)
        {
            object v = member.Field != null ? member.Field.GetValue(_app) : null;
            return v as Button ?? (v is GameObject go ? go.GetComponent<Button>() : null);
        }

        private GameObject ObjectOf(GameMember member) => member.Field != null ? member.Field.GetValue(_app) as GameObject : null;

        // The header's name labels as the game made them, before any conversation was set up on them (the kit is made as the
        // city's phone is built, before the app is first opened): Going Public tints the name of a contact another player
        // relays, and a tint the kit sees later is that, shown as a chip on the head, never as a recoloured name.
        private void AddHome(GameMember member)
        {
            GameObject info = ObjectOf(member);
            if (info == null)
            {
                return;
            }
            foreach (TMP_Text t in info.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t != null && (t.gameObject.name == "ContactName" || (t.transform.parent != null && t.transform.parent.name == "ContactName")))
                {
                    _names.Add(new KeyValuePair<TMP_Text, Color>(t, t.color));
                }
            }
        }
    }
}
