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
        private readonly List<Button> _services = new List<Button>(8);   // the aside's actions: the game's buttons, null for the game's own view or Delete contact
        private int _remove = -1;          // the aside's place of Delete contact; -1 where the game's list offers none for them
        private readonly List<Button> _choices = new List<Button>(4);    // the conversation's or the call's numbered choices
        private readonly List<GameUi.DialogExtra> _extras = new List<GameUi.DialogExtra>(4);   // by choice: the dialog's own answer past the game's three; null for a button
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
        private Contact _clearing;         // the contact whose Clear chat was pressed, until the game has emptied its texts (ChatCleared)

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

        public string TypingText => _typing < 0 ? _query
            : CallFormShown ? _callForm.Text(_typing)
            : _typing < _fields.Count && _fields[_typing].Input != null ? _fields[_typing].Input.text : string.Empty;

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
            return Take(ContactsRoutes.Route(command, arg, _m.HeadActions), command, arg);
        }

        // One press by its route, to the game's action it stands for. A route the kit has no button of the game's for (a
        // press that no route answers, or a button the game has since hidden or taken away) is answered in the game's own
        // view instead of by nothing (Unrouted), with one line in Player.log.
        private bool Take(ContactsRoute route, int command, int arg)
        {
            Contact open = _app.selectedContact;
            if (CallFormPress(command, arg, out bool formed))
            {
                return formed;   // the call's form: its widget's own press
            }
            switch (route)
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
                    return Handed(_map, "view on map", open, "handed to the game");
                case ContactsRoute.Call:
                    return CallNow(open);
                case ContactsRoute.Service:
                    return Service(arg, open);
                case ContactsRoute.MarkRead:
                    return Handed(ButtonOf(GameContract.ContactsMarkAll), "mark all as read", open, "handed to the game");
                case ContactsRoute.DeleteAll:
                    return Handed(ButtonOf(GameContract.KitDeleteAll), "delete all", open, "handed to the game, which asks first");
                case ContactsRoute.Choice:
                case ContactsRoute.Reply:
                    if (arg >= 0 && arg < _extras.Count && _extras[arg] != null)
                    {
                        return CallExtra(_extras[arg]);
                    }
                    return arg >= 0 && arg < _choices.Count && Handed(_choices[arg], "a reply", open, "the game's own button");
                case ContactsRoute.HangUp:
                    return Handed(_hangUp, "hang up", open, "the game's own button");
                case ContactsRoute.Clear:
                    _query = string.Empty;
                    _m.List.Offset = 0f;
                    return true;
                case ContactsRoute.Less:
                    return arg >= 0 && arg < _fields.Count && Click(_fields[arg].Less);
                case ContactsRoute.More:
                    return arg >= 0 && arg < _fields.Count && Click(_fields[arg].More);
                case ContactsRoute.Search:
                case ContactsRoute.Field:
                    return false;   // typing: the host types into the focused box
                default:
                    return Unrouted("a press (command " + command + ", " + arg + ") that no route answers", open);
            }
        }

        // The game's own button pressed: its listener runs as a click would run it. A button the game holds (it greys one
        // while it works) is left, with a line; one the game does not have or has hidden since the kit drew it is answered
        // in the game's own view.
        private bool Handed(Button b, string what, Contact open, string how)
        {
            if (b == null || !b.gameObject.activeSelf)
            {
                return Unrouted(what, open);
            }
            if (!b.interactable)
            {
                return Done(false, what + " " + Who(open));
            }
            UiWidgets.Hand(b);   // the game's listeners run as the game's own work (View on map opens its city map)
            return Done(true, what + " " + Who(open) + ": " + how);
        }

        // Something the kit drew or was pressed on cannot be bound to a button or entry of the game's: the game's own
        // Contacts view shows until the app closes, where the same thing is the game's, and Player.log says so once.
        private static bool Unrouted(string what, Contact open)
        {
            UiDirector.Logger?.Info("Contacts (kit): " + what + " (" + Who(open)
                                    + ") could not be bound to a button or entry of the game's, so the game's own Contacts view shows until the app closes");
            ContactsKit.Classic();
            return true;
        }

        // One line a press in Player.log, so a report shows each button reaching the game (beta 3's Call left no trace).
        private static bool Done(bool done, string what)
        {
            UiDirector.Logger?.Info("Contacts (kit): " + what + (done ? string.Empty : " was held by the game, so nothing was pressed"));
            return done;
        }

        private static string Who(Contact c) => c != null ? Name(c) : "no contact";

        public void Focused(int command, int arg)
        {
            // A contact opens on a press only: the game marks its messages read as it opens, so the cursor passing never does.
            if (command == UiConversation.FormCommand)
            {
                // The field the next typing goes into: one target a field, pressed after it takes focus; on a call's form only its figures and words.
                _typing = !CallFormShown || CallFormTyped(arg) ? arg : -1;
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
            // The key of a button the game shows: with none shown there is nothing to answer, and it is no reason to leave the kit.
            if (action == UiAction.Alt)
            {
                return Shows(_call) && Press(UiAppWindow.ActionCommand, ContactsRoutes.PlaceOf(_m.HeadActions, ContactsRoutes.CallArg));
            }
            return action == UiAction.Option && Shows(_map) && Press(UiAppWindow.ActionCommand, ContactsRoutes.PlaceOf(_m.HeadActions, ContactsRoutes.MapArg));
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

        // A row of the list pressed: the game's own entry for the contact, so its messages are read as the game reads them. A
        // contact the game's list did not take (it logs "Failed to load contact" and leaves the entry out) is opened the way
        // its notification opens it. The game opens a contact in the same call (it sets the one chosen before its first
        // pause), so a contact that is not the chosen one afterwards was not bound, and the game's own view answers instead.
        private bool Open(Contact c)
        {
            ContactModel entry = EntryOf(c);
            if (entry != null)
            {
                _app.ContactButtonOnClick(entry);   // the game's own list: the group shown stays
            }
            else
            {
                _app.OpenAppWithContact(c);
            }
            if (_app.selectedContact != c)
            {
                return Unrouted("opening the contact from its row", c);
            }
            return Done(true, "open " + Who(c) + (entry != null ? ": the game's list entry" : ": no entry in the game's list, opened as its notification opens one"));
        }

        // The game's list entry of a contact, or null when the list now shown has none.
        private ContactModel EntryOf(Contact c)
        {
            List<ContactModel> data = _app.contactScrollerController != null ? _app.contactScrollerController.data : null;
            for (int i = 0; data != null && i < data.Count; i++)
            {
                if (data[i] != null && data[i].contact == c)
                {
                    return data[i];
                }
            }
            return null;
        }

        // The game's own Call: its call button's listener, which the game wires itself for every kind of contact (a business
        // by its opening hours, a person, an employee). A call that starts is drawn as the call card from its first line; a
        // call the game answers with a line ("closed now") stays in the thread, which shows that line.
        private bool CallNow(Contact open)
        {
            if (_call == null || !_call.gameObject.activeSelf)
            {
                return Unrouted("call", open);
            }
            if (!_call.interactable)
            {
                return Done(false, "call " + Who(open));   // the game holds its call button while a call runs
            }
            UiWidgets.Hand(_call);
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
            if (arg == _remove)
            {
                return Remove(open);
            }
            Button b = _services[arg];
            if (b == null)
            {
                ContactsKit.Classic();
                return Done(true, "the game's own view for another mod's buttons by " + Who(open));
            }
            _clearing = b == ButtonOf(GameContract.KitClearChat) ? open : _clearing;
            return Handed(b, b.name, open, "handed to the game");
        }

        // The game's delete of one contact: what the trash on its row in the list calls, which asks in the game's own box
        // first (and not at all with the key the game skips its boxes by).
        private bool Remove(Contact open)
        {
            if (open == null || _remove < 0)
            {
                return Unrouted("delete contact", open);
            }
            _app.RemoveContact(open);
            return Done(true, "delete contact " + Who(open) + ": handed to the game, which asks first");
        }

        private static bool Click(Button b)
        {
            if (!Shows(b))
            {
                return false;
            }
            UiWidgets.Hand(b);
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
