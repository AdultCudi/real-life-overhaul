using System;
using System.Collections.Generic;
using System.Globalization;
using Entities;
using Localizor;
using TMPro;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Contacts as a screen of the kit, mirrored from the game's own app: its categories as groups, its contacts (the
    /// unread first, then the newest), the open contact's messages, its header's details and its buttons. Every press
    /// goes to the game: a contact opens through the game's own list (so its messages are read as the game reads them),
    /// Call and the offers' buttons hand over to the game's own view, the other buttons are the game's pressed. Words
    /// are the game's keys and the mod's (registered in UiScreens). Cost: a look at a few counts four times a second
    /// while it shows; a layout only when they change.
    /// </summary>
    public sealed class ContactsScreen : IUiScreen, IUiTyping
    {
        private const float Every = 0.25f;   // seconds between looks at the game's contacts

        private static readonly ContactCategoryName[] Categories = (ContactCategoryName[])Enum.GetValues(typeof(ContactCategoryName));

        private readonly ContactsApp _app;
        private readonly UiListDetailModel _m = new UiListDetailModel();
        private readonly List<Contact> _rows = new List<Contact>(64);
        private readonly List<ContactCategoryName> _groups = new List<ContactCategoryName>(8);
        private readonly List<Button> _services = new List<Button>(8);
        private string _query = string.Empty;
        private bool _dirty = true;
        private float _next;
        private long _seen;
        private Button _call, _map;

        public ContactsScreen(ContactsApp app)
        {
            _app = app;
        }

        public string Id => "contacts";

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    long now = Signature();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public int TypingCommand => UiListDetail.Search;

        public string TypingText => _query;

        public void Typed(string text)
        {
            _query = text ?? string.Empty;
            _m.RowsOffset = 0f;
            _dirty = true;
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Build();
            UiListDetail.Lay(s, _m, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            _dirty = true;
            Contact open = _app.selectedContact;
            switch (ContactsRoutes.Route(command, arg))
            {
                case ContactsRoute.Group:
                    if (arg < 0 || arg > _groups.Count)
                    {
                        return false;
                    }
                    _app.LoadContactsList(arg == 0 ? (ContactCategoryName?)null : _groups[arg - 1]);
                    _m.RowsOffset = 0f;
                    return true;
                case ContactsRoute.Row:
                    return arg >= 0 && arg < _rows.Count && Open(_rows[arg]);
                case ContactsRoute.Map:
                    return Done(Click(_map), "view on map " + Who(open) + ": handed to the game");
                case ContactsRoute.Call:
                    return CallNow(open);
                case ContactsRoute.Service:
                    return arg >= 0 && arg < _services.Count && Done(Click(_services[arg]), _services[arg].name + " for " + Who(open) + ": handed to the game");
                case ContactsRoute.MarkRead:
                    _app.MarkAllAsRead();
                    return Done(true, "mark all as read: handed to the game");
                case ContactsRoute.DeleteAll:
                    _app.DeleteAll();   // the game asks first, in its own box
                    return Done(true, "delete all: handed to the game, which asks first");
                case ContactsRoute.Classic:
                    ContactsKit.Classic();
                    return Done(true, "the game's own view for " + Who(open));
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
        }

        public bool Back() => false;

        public void Tab(int step)
        {
            int at = ContactCategorySelection.SelectedCategory.HasValue ? _groups.IndexOf(ContactCategorySelection.SelectedCategory.Value) + 1 : 0;
            int count = _groups.Count + 1;
            int next = ((at + step) % count + count) % count;
            Press(UiListDetail.Group, next);
        }

        public bool Act(UiAction action)
        {
            if (action == UiAction.Alt)
            {
                return Press(UiListDetail.Action, ContactsRoutes.CallArg);
            }
            return action == UiAction.Option && Press(UiListDetail.Action, ContactsRoutes.MapArg);
        }

        public void Reveal(int list, int index)
        {
            if (list == 0)
            {
                _m.GroupsOffset = Math.Max(0f, (index - 2) * (UiTokens.Control + UiTokens.S1));
            }
            else
            {
                _m.RowsOffset = Math.Max(0f, (index - 2) * (UiTokens.Row + UiListDetail.RowGap));
            }
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            float by = -notches * UiTokens.S16;
            switch (clip)
            {
                case 0: _m.GroupsOffset = Math.Max(0f, _m.GroupsOffset + by); break;
                case 1: _m.RowsOffset = Math.Max(0f, _m.RowsOffset + by); break;
                case 2: _m.ThreadOffset = Math.Max(0f, (_m.ThreadOffset < 0f ? 100000f : _m.ThreadOffset) + by); break;
                default: _m.SideOffset = Math.Max(0f, _m.SideOffset + by); break;
            }
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------------- the game's state

        // What changing would change the screen: the contacts, what is unread, the group, the one open and its messages.
        private long Signature()
        {
            List<Contact> all = SaveGameManager.Current != null ? SaveGameManager.Current.Contacts : null;
            if (all == null)
            {
                return 0;
            }
            long sig = all.Count;
            for (int i = 0; i < all.Count; i++)
            {
                Contact c = all[i];
                sig = sig * 31 + (c != null && c.messagesQueue != null ? c.messagesQueue.Count * 7 + c.NumberOfUnreadMessages : 0);
            }
            Contact open = _app.selectedContact;
            sig = sig * 31 + (open != null ? open.GetHashCode() : 0);
            sig = sig * 31 + (ContactCategorySelection.SelectedCategory.HasValue ? (int)ContactCategorySelection.SelectedCategory.Value + 1 : 0);
            return sig;
        }

        private void Build()
        {
            List<Contact> all = SaveGameManager.Current != null ? SaveGameManager.Current.Contacts : new List<Contact>();
            ContactCategoryName? shown = ContactCategorySelection.SelectedCategory;
            _m.AppName = UiText.T("reallife_ui_contacts_app");
            _m.GroupsLabel = UiText.T("reallife_ui_contacts_groups");
            Groups(all, shown);
            Rows(all, shown);
            int unread = 0;
            for (int i = 0; i < all.Count; i++)
            {
                unread += all[i] != null ? all[i].NumberOfUnreadMessages : 0;
            }
            _m.SideActions = new[]
            {
                new UiActionModel { Label = UiText.T("contacts_mark_all_as_read_button"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Check, Key = UiAction.Count,
                    Command = UiListDetail.SideAction, Arg = 0, Disabled = unread == 0 },
                new UiActionModel { Label = UiText.T("contacts_delete_all_button"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Trash, Key = UiAction.Count,
                    Command = UiListDetail.SideAction, Arg = 1, Disabled = !shown.HasValue },
            };
            _m.SideActionCount = 2;
            _m.PlayerName = PlayerName();
            _m.PlayerHue = UiColour.HueOf(_m.PlayerName);
            _m.PlayerSummary = UiText.Fill("reallife_ui_contacts_summary", "count", UiText.Whole(all.Count), "unread", UiText.Whole(unread));
            _m.ListTitle = shown.HasValue ? UiText.T(CategoryKey(shown.Value)) : UiText.T("reallife_ui_contacts_all");
            _m.ListCount = UiText.Whole(_m.RowCount);
            _m.SearchPlaceholder = UiText.T("reallife_ui_contacts_search");
            _m.Query = _query.Length > 0 ? _query : null;
            _m.SearchCount = UiText.Whole(_m.RowCount);
            _m.ListEmptyTitle = UiText.T("reallife_ui_contacts_none_title");
            _m.ListEmptyBody = UiText.T("reallife_ui_contacts_none_body");
            _m.NoDetailTitle = UiText.T("reallife_ui_contacts_pick_title");
            _m.NoDetailBody = UiText.T("reallife_ui_contacts_pick_body");
            _m.ThreadEmptyTitle = UiText.T("reallife_ui_contacts_quiet_title");
            _m.ThreadEmptyBody = UiText.T("reallife_ui_contacts_quiet_body");
            _m.ClassicLabel = UiText.T("reallife_ui_contacts_classic");
            _m.ServicesTitle = UiText.T("reallife_ui_contacts_requests");
            _m.StatsTitle = UiText.T("reallife_ui_contacts_profile");
            _m.DetailsTitle = UiText.T("reallife_ui_contacts_details");
            _m.Composer = false;
            _m.NoReply = null;
            Detail(_app.selectedContact);
            var prompts = new List<UiPrompt>(5)
            {
                new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") },
                new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") },
                new UiPrompt { Action = UiAction.TabPrev, Label = UiText.T("reallife_ui_prompt_group") },
            };
            if (Shows(_call))
            {
                prompts.Add(new UiPrompt { Action = UiAction.Alt, Label = UiText.T("phone_call_button") });
            }
            prompts.Add(new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") });
            _m.Prompts = prompts.ToArray();
            _m.PromptCount = _m.Prompts.Length;
        }

        private void Groups(List<Contact> all, ContactCategoryName? shown)
        {
            _groups.Clear();
            var nav = new List<UiNavModel>(Categories.Length + 1);
            int allUnread = 0;
            for (int i = 0; i < all.Count; i++)
            {
                allUnread += all[i] != null ? all[i].NumberOfUnreadMessages : 0;
            }
            nav.Add(new UiNavModel { Label = UiText.T("reallife_ui_contacts_all"), Count = UiText.Whole(allUnread > 0 ? allUnread : all.Count), Hue = 12f,
                Selected = !shown.HasValue, Loud = allUnread > 0 });
            foreach (ContactCategoryName c in Categories)
            {
                int count = 0, unread = 0;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null && all[i].category == c)
                    {
                        count++;
                        unread += all[i].NumberOfUnreadMessages;
                    }
                }
                if (count == 0)
                {
                    continue;
                }
                _groups.Add(c);
                nav.Add(new UiNavModel { Label = UiText.T(CategoryKey(c)), Count = UiText.Whole(unread > 0 ? unread : count), Hue = UiColour.HueOf(c.ToString()),
                    Selected = shown.HasValue && shown.Value == c, Loud = unread > 0 });
            }
            _m.Groups = nav.ToArray();
            _m.GroupCount = _m.Groups.Length;
        }

        private void Rows(List<Contact> all, ContactCategoryName? shown)
        {
            _rows.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                Contact c = all[i];
                if (c == null || (shown.HasValue && c.category != shown.Value) || (_query.Length > 0 && Name(c).IndexOf(_query, StringComparison.CurrentCultureIgnoreCase) < 0))
                {
                    continue;
                }
                _rows.Add(c);
            }
            // The unread first, then the newest, as a phone's messages list them.
            _rows.Sort((a, b) => a.HasUnreadMessages != b.HasUnreadMessages ? (a.HasUnreadMessages ? -1 : 1) : b.lastTimeUpdated.CompareTo(a.lastTimeUpdated));
            var rows = new UiRowModel[_rows.Count];
            bool sawRead = false;
            for (int i = 0; i < _rows.Count; i++)
            {
                Contact c = _rows[i];
                TextMessage last = Last(c);
                bool unread = c.HasUnreadMessages;
                string section = null;
                if (i == 0 && unread)
                {
                    section = UiText.T("reallife_ui_contacts_new");
                }
                else if (!unread && !sawRead && i > 0)
                {
                    section = UiText.T("reallife_ui_contacts_earlier");
                }
                sawRead |= !unread;
                rows[i] = new UiRowModel
                {
                    Name = Name(c), Preview = last != null ? Words(last) : UiText.T(c.description ?? string.Empty), Time = last != null ? When(last) : string.Empty,
                    Unread = c.NumberOfUnreadMessages, Hue = UiColour.HueOf(c.id), Selected = c == _app.selectedContact, Section = section
                };
            }
            _m.Rows = rows;
            _m.RowCount = rows.Length;
        }

        private void Detail(Contact c)
        {
            _services.Clear();
            _m.HasDetail = c != null;
            if (c == null)
            {
                return;
            }
            _m.Name = Name(c);
            _m.Hue = UiColour.HueOf(c.id);
            _m.Role = Describe(c);
            _m.Status = null;
            _call = Button(GameContract.KitCall);
            _map = Button(GameContract.KitMap);
            var actions = new List<UiActionModel>(2);
            if (Shows(_map))
            {
                actions.Add(new UiActionModel { Label = UiText.T("phone_view_on_map_button"), Kind = UiButtonKind.Secondary, Icon = UiIcon.MapPin, Key = UiAction.Option,
                    Command = UiListDetail.Action, Arg = ContactsRoutes.MapArg });
            }
            if (Shows(_call))
            {
                actions.Add(new UiActionModel { Label = UiText.T("phone_call_button"), Kind = UiButtonKind.Primary, Icon = UiIcon.Phone, Key = UiAction.Alt,
                    Command = UiListDetail.Action, Arg = ContactsRoutes.CallArg });
            }
            _m.Actions = actions.ToArray();
            _m.ActionCount = _m.Actions.Length;
            Messages(c);
            var services = new List<UiServiceModel>(8);
            Service(services, GameContract.KitEmployee, "contacts_manage_employee_button");
            Service(services, GameContract.KitSchedule, "myemployees_manage_schedule");
            Service(services, GameContract.KitDelivery, "contacts_manage_delivery_button");
            Service(services, GameContract.KitCandidates, "contacts_candidates_button");
            Service(services, GameContract.KitRival, "contacts_rival_button");
            Service(services, GameContract.KitTaxes, "contacts_tax_button");
            Service(services, GameContract.KitClearChat, "contacts_header_clear_chat_button");
            _m.Services = services.ToArray();
            _m.ServiceCount = _m.Services.Length;
            var details = new List<UiPairModel>(5);
            Pair(details, GameContract.KitHours, "contacts_today_opening_hours");
            Pair(details, GameContract.KitAddress, "common_address");
            Pair(details, GameContract.KitDemands, "contacts_schedule_demands");
            Pair(details, GameContract.KitWage, "myemployees_hourlywage");
            _m.Details = details.ToArray();
            _m.DetailCount = _m.Details.Length;
            string skill = Value(GameContract.KitSkill);
            int level = 0;
            bool hasSkill = skill != null && int.TryParse(skill.Trim().TrimEnd('%'), NumberStyles.Integer, CultureInfo.InvariantCulture, out level);
            _m.Stats = hasSkill ? new[] { new UiStatModel { Label = UiText.T("bizman_headhunters_skill_level_title"), Figure = skill.Trim(), Value = level } } : new UiStatModel[0];
            _m.StatCount = _m.Stats.Length;
        }

        private void Messages(Contact c)
        {
            var list = new List<UiMessageModel>(Contact.MaxMessages);
            TextMessage newest = Last(c);
            if (c.messagesQueue != null)
            {
                foreach (TextMessage msg in c.messagesQueue)
                {
                    if (msg == null)
                    {
                        continue;
                    }
                    bool taxes = msg.messageKey == "ba:messagetype_taxes" || msg.messageKey == "ba:messagetype_contacts_taxes_repossession_message";
                    bool buttons = msg.additionalData != null && msg.additionalData.contextButtonData != null && msg.additionalData.contextButtonData.Count > 0 && msg == newest;
                    list.Add(new UiMessageModel
                    {
                        From = msg.isFromPlayer ? UiFrom.Me : msg.isSpecialMessage ? UiFrom.System : UiFrom.Them,
                        Text = Words(msg), Time = When(msg), HasChoices = taxes || buttons || msg.contextAction != null
                    });
                }
            }
            _m.Messages = list.ToArray();
            _m.MessageCount = _m.Messages.Length;
            _m.ThreadOffset = -1f;
            // A queue the game has not shortened starts where the line starts; a full one has lost its oldest.
            _m.ThreadStart = list.Count < Contact.MaxMessages ? UiText.Fill("reallife_ui_contacts_start", "name", _m.Name) : null;
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
        // by its opening hours, a person, an employee). A call that starts runs in the game's own view, so the kit steps
        // aside until it ends; a call the game answers with a line ("closed now") stays on the kit, which shows that line.
        private bool CallNow(Contact open)
        {
            if (!Shows(_call))
            {
                return Done(false, "call " + Who(open));
            }
            _call.onClick.Invoke();
            bool talking = !_call.interactable;   // the game holds its call button while a call runs
            if (talking)
            {
                ContactsKit.Call(_call);
            }
            return Done(true, "call " + Who(open) + (talking ? ": handed to the game" : ": the game answered in the thread"));
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

        private Button Button(GameMember member)
        {
            object v = member.Field != null ? member.Field.GetValue(_app) : null;
            return v as Button ?? (v is GameObject go ? go.GetComponent<Button>() : null);
        }

        private void Service(List<UiServiceModel> into, GameMember member, string key)
        {
            Button b = Button(member);
            if (!Shows(b))
            {
                return;
            }
            _services.Add(b);
            into.Add(new UiServiceModel { Title = UiText.T(key), Price = string.Empty });
        }

        private void Pair(List<UiPairModel> into, GameMember member, string key)
        {
            string v = Value(member);
            if (!string.IsNullOrEmpty(v))
            {
                into.Add(new UiPairModel { Key = UiText.T(key), Value = v });
            }
        }

        // The words the game's header shows in one of its pieces: the last text in it, its value under its label.
        private string Value(GameMember member)
        {
            var go = member.Field != null ? member.Field.GetValue(_app) as GameObject : null;
            if (go == null || !go.activeInHierarchy)
            {
                return null;
            }
            TMP_Text[] texts = go.GetComponentsInChildren<TMP_Text>(false);
            return texts.Length > 0 ? texts[texts.Length - 1].text : null;
        }

        private static string Name(Contact c) => LocalizorManager.IsLocalizedKey(c.id) ? LocalizorManager.GetLocalization(c.id) : c.id;

        private static string Describe(Contact c)
        {
            string d = c.description;
            return string.IsNullOrEmpty(d) ? null : LocalizorManager.IsLocalizedKey(d) ? LocalizorManager.GetLocalization(d) : d;
        }

        private static string Words(TextMessage msg)
        {
            string text = LocalizorManager.GetLocalization(msg.messageKey, (object)msg.messageData);
            return StripTags(text);
        }

        // The game's rich text (bold, colours) as plain words: the kit sets its own type.
        private static string StripTags(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
            {
                return text;
            }
            var sb = new System.Text.StringBuilder(text.Length);
            bool tag = false;
            foreach (char ch in text)
            {
                if (ch == '<')
                {
                    tag = true;
                }
                else if (ch == '>')
                {
                    tag = false;
                }
                else if (!tag)
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }

        private static string When(TextMessage msg)
        {
            int today = SaveGameManager.Current != null ? SaveGameManager.Current.Day : msg.timestamp.Day;
            if (msg.timestamp.Day != today)
            {
                return UiText.Fill("reallife_ui_contacts_day", "day", UiText.Whole(msg.timestamp.Day));
            }
            return msg.timestamp.Hour.ToString("00", CultureInfo.InvariantCulture) + ":" + ((int)msg.timestamp.Minute).ToString("00", CultureInfo.InvariantCulture);
        }

        private static TextMessage Last(Contact c)
        {
            TextMessage last = null;
            if (c.messagesQueue != null)
            {
                foreach (TextMessage m in c.messagesQueue)
                {
                    last = m ?? last;
                }
            }
            return last;
        }

        private static string CategoryKey(ContactCategoryName c) => "contactcategoryname_" + c.ToString().ToLowerInvariant();

        private static string PlayerName()
        {
            try
            {
                return CivicText.PlayerName();
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                return string.Empty;
            }
        }
    }
}
