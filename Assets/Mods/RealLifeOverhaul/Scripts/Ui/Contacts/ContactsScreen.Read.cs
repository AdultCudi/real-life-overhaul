using System;
using System.Collections.Generic;
using AI.Employees.SalaryNegotiation;
using Characters.EmojiSystem;
using Entities;
using Helpers;
using Localizor;
using Streets;
using TMPro;
using UI.Dialog;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the game's Contacts app into the kit's window model, after the game's (and other mods') code ran: the
    /// contacts, their messages and the header the game set up, the offers' buttons it made in the thread, and the line,
    /// buttons and form of a conversation it runs. Nothing here decides for the game: what shows and what can be pressed is
    /// what the game's own objects say. Cost: called on a layout only.
    /// </summary>
    public sealed partial class ContactsScreen
    {
        private const int MostOffers = 6;   // offers' replies as numbered choices: more than any message carries
        private static readonly int[] Order = new int[3];

        // What changing would change the screen: the contacts, what is unread, the group, the one open and its messages, the
        // header's buttons, the conversation the game runs (its line, its buttons, its form's figures), the offers' buttons and
        // the talk history of the calls (a line kept moves its count).
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
            sig = sig * 31 + (_call != null && _call.interactable ? 1 : 0) + (_call != null && _call.gameObject.activeSelf ? 2 : 0);
            sig = sig * 31 + (_app.dialog != null ? _app.dialog.GetHashCode() : 0);
            object entry = GameContract.TalkEntry.Field != null ? GameContract.TalkEntry.Field.GetValue(_app) : null;
            sig = sig * 31 + (entry != null ? entry.GetHashCode() : 0);
            CanvasGroup view = EntryView();
            sig = sig * 31 + (view != null ? Words(view.transform).Length : 0);
            for (int i = 0; i < _fields.Count; i++)
            {
                sig = sig * 31 + (_fields[i].Input != null ? _fields[i].Input.text.GetHashCode() : 0);
            }
            sig = sig * 31 + Offers(null);
            sig = sig * 31 + CallFormSignature();
            sig = sig * 31 + TalkLogGame.Changes;
            return sig;
        }

        // Clear chat was pressed on this contact and the game has since emptied its texts (it asks first): the talk history of its
        // calls goes with them, as the chat does. Nothing while the question is unanswered, or if the contact has changed.
        private void ChatCleared(Contact open)
        {
            if (_clearing == null)
            {
                return;
            }
            if (_clearing != open)
            {
                _clearing = null;
            }
            else if (open.messagesQueue == null || open.messagesQueue.Count == 0)
            {
                TalkLogGame.Clear(open);
                _clearing = null;
            }
        }

        private void Build()
        {
            List<Contact> all = SaveGameManager.Current != null ? SaveGameManager.Current.Contacts : new List<Contact>();
            ContactCategoryName? shown = ContactCategorySelection.SelectedCategory;
            _m.Title = UiText.T("reallife_ui_contacts_app");
            Groups(all, shown);
            Rows(all, shown);
            Foot(all);
            _m.PickTitle = UiText.T("reallife_ui_contacts_pick_title");
            _m.PickBody = UiText.T("reallife_ui_contacts_pick_body");
            _m.Call = null;
            _choices.Clear();
            _extras.Clear();
            _fields.Clear();
            _hangUp = null;
            Contact open = _app.selectedContact;
            ChatCleared(open);
            Detail(open);
            Talk(open);
            Prompts();
        }

        // ------------------------------------------------------------------------------------------------- the sidebar and the list

        private void Groups(List<Contact> all, ContactCategoryName? shown)
        {
            _groups.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                int c = all[i] != null ? (int)all[i].category : -1;
                if (c >= 0 && !_groups.Contains(c))
                {
                    _groups.Add(c);
                }
            }
            _groups.Sort();   // the game's seven in its order, then the mod's groups in theirs
            var nav = new UiNavEntry[_groups.Count + 1];
            nav[0] = new UiNavEntry { Icon = UiIcon.AddressBook, Tint = UiIconTints.Of(UiIcon.AddressBook), Label = UiText.T("reallife_ui_contacts_all"), Count = UiText.Whole(all.Count) };
            int chosen = 0;
            for (int g = 0; g < _groups.Count; g++)
            {
                int count = 0;
                for (int i = 0; i < all.Count; i++)
                {
                    count += all[i] != null && (int)all[i].category == _groups[g] ? 1 : 0;
                }
                UiIcon icon = ContactsRules.GroupIcon(_groups[g]);
                nav[g + 1] = new UiNavEntry { Icon = icon, Tint = UiIconTints.Of(icon), Label = UiText.T(ContactsRules.GroupKey(_groups[g])), Count = UiText.Whole(count) };
                chosen = shown.HasValue && (int)shown.Value == _groups[g] ? g + 1 : chosen;
            }
            _m.Groups = nav;
            _m.GroupShown = chosen;
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
            var rows = new UiMessageRow[_rows.Count];
            bool sawRead = false;
            for (int i = 0; i < _rows.Count; i++)
            {
                Contact c = _rows[i];
                Face(c);
                TextMessage last = Last(c);
                List<TalkLine> talk = TalkLogGame.Lines(c);
                bool talked = talk != null && (last == null || talk[talk.Count - 1].Minute >= Minute(last));   // a call after the last text: its last line is the preview
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
                string preview = talked ? talk[talk.Count - 1].Text : last != null ? Words(last) : Describe(c);
                bool mine = talked ? talk[talk.Count - 1].Mine : last != null && last.isFromPlayer;
                rows[i] = new UiMessageRow
                {
                    Name = Name(c), Hue = UiColour.HueOf(c.id), Icon = Person(c) ? UiIcon.None : ContactsRules.FirmIcon(BusinessType(c), (int)c.category),
                    Preview = mine ? UiText.Fill("reallife_ui_contacts_you", "text", preview) : preview,
                    Time = talked ? When(talk[talk.Count - 1].Minute) : last != null ? When(last) : string.Empty,
                    Unread = c.NumberOfUnreadMessages, Selected = c == _app.selectedContact, Section = section
                };
            }
            UiMessageListModel list = _m.List;
            list.Rows = rows;
            list.RowCount = rows.Length;
            list.SearchPlaceholder = UiText.T("reallife_ui_contacts_search");
            list.Query = _query.Length > 0 ? _query : null;
            list.Found = UiText.Whole(rows.Length);
            list.EmptyTitle = UiText.T("reallife_ui_contacts_none_title");
            list.EmptyBody = UiText.T("reallife_ui_contacts_none_body");
            list.EmptyAction = _query.Length > 0 ? UiText.T("reallife_ui_parts_clear_search") : null;
        }

        private void Foot(List<Contact> all)
        {
            int unread = 0;
            for (int i = 0; i < all.Count; i++)
            {
                unread += all[i] != null ? all[i].NumberOfUnreadMessages : 0;
            }
            Button mark = ButtonOf(GameContract.ContactsMarkAll), delete = ButtonOf(GameContract.KitDeleteAll);
            _m.Foot.Actions = new[]
            {
                new UiActionModel { Label = UiText.T("contacts_mark_all_as_read_button"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Check, Key = UiAction.Count,
                    Arg = ContactsRoutes.MarkReadArg, Disabled = !Shows(mark) },
                new UiActionModel { Label = UiText.T("contacts_delete_all_button"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Trash, Key = UiAction.Count,
                    Arg = ContactsRoutes.DeleteAllArg, Disabled = !Shows(delete) },   // the game allows it only on one group, never on all
            };
            string player = PlayerName();
            FaceSources.Player(player);
            _m.Foot.Player = player;
            _m.Foot.PlayerHue = UiColour.HueOf(player ?? string.Empty);
            _m.Foot.PlayerLine = UiText.Fill("reallife_ui_contacts_summary", "count", UiText.Whole(all.Count), "unread", UiText.Whole(unread));
        }

        // ------------------------------------------------------------------------------------------------- the open contact

        private void Detail(Contact c)
        {
            _services.Clear();
            _remove = -1;
            _m.HasDetail = c != null;
            if (c == null)
            {
                return;
            }
            bool candidate = false;
            EmployeeInstance employee = c.IsEmployeeContact ? EmployeeOf(c.id, out candidate) : null;
            UiHeadModel h = _m.Head;
            h.Name = Name(c);
            Face(c);
            h.Hue = UiColour.HueOf(c.id);
            h.Icon = Person(c) ? UiIcon.None : ContactsRules.FirmIcon(BusinessType(c), (int)c.category);
            h.Tint = UiRole.None;
            h.Ring = employee != null && !candidate ? StaffViews.Mood(Mathf.RoundToInt(employee.satisfaction)) : UiRole.None;   // a candidate has no mood at work yet
            h.Status = null;
            h.Owner = Relayed() ? UiText.T("reallife_ui_contacts_other_player") : null;
            if (employee != null)
            {
                string skill = employee.GetPrimarySkill();
                h.Role = string.IsNullOrEmpty(skill) ? null : LocalizorManager.GetLocalization(skill);
                h.Employer = employee.IsAssignedToAnyBusiness() ? BuildingHelper.GetBuildingRegistration(employee.assignedAddress)?.BusinessName : UiText.T("common_unassigned");
            }
            else
            {
                h.Role = Describe(c);
                h.Employer = null;
            }
            _call = ButtonOf(GameContract.KitCall);
            _map = ButtonOf(GameContract.KitMap);
            var actions = new List<UiActionModel>(2);
            if (Shows(_map))
            {
                actions.Add(new UiActionModel { Label = UiText.T("phone_view_on_map_button"), Kind = UiButtonKind.Secondary, Icon = UiIcon.MapPin, Key = UiAction.Option,
                    Arg = ContactsRoutes.MapArg });
            }
            if (Shows(_call))
            {
                actions.Add(new UiActionModel { Label = UiText.T("phone_call_button"), Kind = UiButtonKind.Primary, Icon = UiIcon.Phone, Key = UiAction.Alt, Arg = ContactsRoutes.CallArg });
            }
            _m.HeadActions = actions.ToArray();
            Aside(c, employee, candidate);
        }

        // What can be done for them (the header's own buttons the game shows, and the game's own view for another mod's), their
        // profile graded and their details, each value as the game's header shows it, the wage as it is paid.
        private void Aside(Contact c, EmployeeInstance employee, bool candidate)
        {
            var acts = new List<UiContactAction>(8);
            Service(acts, GameContract.KitEmployee, "contacts_manage_employee_button", UiIcon.IdBadge);
            Service(acts, GameContract.KitSchedule, "myemployees_manage_schedule", UiIcon.Calendar);
            Service(acts, GameContract.KitDelivery, "contacts_manage_delivery_button", UiIcon.Truck);
            Service(acts, GameContract.KitCandidates, "contacts_candidates_button", UiIcon.People);
            Service(acts, GameContract.KitRival, "contacts_rival_button", UiIcon.Flag);
            Service(acts, GameContract.KitTaxes, "contacts_tax_button", UiIcon.Receipt);
            if (Foreign())
            {
                _services.Add(null);
                acts.Add(new UiContactAction { Label = UiText.T("reallife_ui_contacts_game_view"), Icon = UiIcon.Grid });
            }
            Button clear = ButtonOf(GameContract.KitClearChat);
            if (Shows(clear))
            {
                _services.Add(clear);
                acts.Add(new UiContactAction { Label = UiText.T("contacts_header_clear_chat_button"), Icon = UiIcon.Trash, Clears = true });
            }
            // The trash on the contact's row in the game's list: there for every contact the game does not keep (Mom and the
            // friends of the mod are not kept), never for another player's relayed contact.
            _remove = -1;
            ContactModel entry = EntryOf(c);
            if (entry != null && !entry.isPermanent && !Relayed())
            {
                _remove = _services.Count;
                _services.Add(null);
                acts.Add(new UiContactAction { Label = UiText.T("reallife_ui_contacts_delete"), Icon = UiIcon.Trash, Clears = true });
            }
            _m.ActionsTitle = UiText.T("reallife_ui_contacts_requests");
            _m.Actions = acts.ToArray();
            _m.ProfileTitle = UiText.T("reallife_ui_contacts_profile");
            if (employee != null)
            {
                string skill = employee.GetPrimarySkill();
                int level = string.IsNullOrEmpty(skill) ? 0 : Mathf.FloorToInt(employee.GetSkillValue(skill));
                _m.ProfileLabels = candidate ? new[] { UiText.T("myemployees_jobtype") } : new[] { UiText.T("myemployees_jobtype"), UiText.T("myemployees_satisfaction") };
                _m.ProfileValues = candidate ? new[] { level } : new[] { level, Mathf.RoundToInt(employee.satisfaction) };
            }
            else
            {
                _m.ProfileLabels = new string[0];
                _m.ProfileValues = new int[0];
            }
            var details = new List<UiPairModel>(4);
            Pair(details, GameContract.KitHours, "contacts_today_opening_hours", null);
            Pair(details, GameContract.KitAddress, "common_address", c.HasAddressAssigned ? c.Address.ToFormattedString() : null);
            Pair(details, GameContract.KitDemands, "contacts_schedule_demands", null);
            if (employee != null && ObjectOf(GameContract.KitWage) != null && ObjectOf(GameContract.KitWage).activeInHierarchy && !(candidate && OpenTerms(c) != null))
            {
                // The wage as it is paid, in the words the offer's own form uses; an open offer's answer carries it instead.
                float paid = WageShown.Ours(employee) ? PaidWage.NowMoney(employee.hourlyWage, employee.id) : employee.hourlyWage;
                details.Add(new UiPairModel { Key = ContactsRules.Label(UiText.T("dialog_candidate_salary_offer_input_hourly_wage")), Value = UiText.Money(paid) });
            }
            _m.DetailsTitle = UiText.T("reallife_ui_contacts_details");
            _m.Details = details.ToArray();
            _m.AsideOffset = 0f;
        }

        private void Service(List<UiContactAction> into, GameMember member, string key, UiIcon icon)
        {
            Button b = ButtonOf(member);
            if (!Shows(b))
            {
                return;
            }
            _services.Add(b);
            into.Add(new UiContactAction { Label = UiText.T(key), Icon = icon });
        }

        // A detail as the game's header shows it: the value it wrote under its label, or the value given when the game's
        // piece shows.
        private void Pair(List<UiPairModel> into, GameMember member, string key, string value)
        {
            GameObject go = ObjectOf(member);
            if (go == null || !go.activeInHierarchy)
            {
                return;
            }
            string v = value ?? LastWords(go.transform);
            if (!string.IsNullOrEmpty(v))
            {
                into.Add(new UiPairModel { Key = UiText.T(key), Value = v });
            }
        }

        // The last words in a piece of the game's: its value under its label.
        private static string LastWords(Transform t)
        {
            TMP_Text[] texts = t.GetComponentsInChildren<TMP_Text>(false);
            return texts.Length > 0 ? ContactsRules.Plain(texts[texts.Length - 1].text) : null;
        }

        // A mod's own button in the header's row of the game's: the kit offers the game's own view for it.
        private bool Foreign()
        {
            Transform row = _call != null ? _call.transform.parent : null;
            if (row == null)
            {
                return false;
            }
            for (int i = 0; i < row.childCount; i++)
            {
                Transform child = row.GetChild(i);
                if (child.gameObject.activeSelf && !UiHostRules.Own(child.name, null) && child.GetComponent<Button>() != null && !Known(child))
                {
                    return true;
                }
            }
            return false;
        }

        // A button of the game's own header, by the objects the app names.
        private bool Known(Transform child)
        {
            Button b = child.GetComponent<Button>();
            return b == _call || b == _map || b == ButtonOf(GameContract.KitEmployee) || b == ButtonOf(GameContract.KitSchedule) || b == ButtonOf(GameContract.KitDelivery)
                   || b == ButtonOf(GameContract.KitCandidates) || b == ButtonOf(GameContract.KitRival) || b == ButtonOf(GameContract.KitTaxes)
                   || b == ButtonOf(GameContract.KitClearChat);
        }

        // Going Public's tint on the header's name: the contact is another player's, relayed to this one.
        private bool Relayed()
        {
            for (int i = 0; i < _names.Count; i++)
            {
                TMP_Text t = _names[i].Key;
                if (t != null && t.gameObject.activeInHierarchy && !Same(t.color, _names[i].Value))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

        // ------------------------------------------------------------------------------------------------- the conversation

        private void Talk(Contact c)
        {
            UiConversationModel t = _m.Talk;
            t.Name = c != null ? Name(c) : null;
            t.Choices = new UiDecisionChoice[0];
            t.Chosen = -1;
            t.Fields = new UiFormField[0];
            t.FieldCount = 0;
            t.FormHint = null;
            t.Typing = false;
            t.Composer = false;
            t.EmptyTitle = UiText.T("reallife_ui_contacts_quiet_title");
            t.EmptyBody = UiText.T("reallife_ui_contacts_quiet_body");
            if (c == null)
            {
                t.Messages = new UiMessageModel[0];
                t.MessageCount = 0;
                _talk = null;
                return;
            }
            var list = new List<UiMessageModel>(Contact.MaxMessages + TalkLog.LinesKept + 4);
            Messages(c, list);
            object running = _app.contact != null ? _app.dialog : null;
            if (running != _talk)
            {
                _talk = running;
                _talkSince = Time.unscaledTime;
            }
            if (running != null && !ContactsRules.InThread(running.GetType().Name))
            {
                Call(c);
            }
            else if (running != null)
            {
                Negotiation(list, t);
            }
            else
            {
                OfferChoices(t, OpenTerms(c));
            }
            t.Messages = list.ToArray();
            t.MessageCount = list.Count;
            t.Offset = -1f;
            // A queue the game has not shortened starts where the line starts; a full one has lost its oldest, and so has a full talk history.
            int texts = c.messagesQueue != null ? c.messagesQueue.Count : 0;
            t.ThreadStart = texts < Contact.MaxMessages && !TalkLogGame.Full(c) ? UiText.Fill("reallife_ui_contacts_start", "name", t.Name) : null;
        }

        // The thread: the game's texts, and among them in the order they happened what was said in the calls (the talk history:
        // the contact's lines and the player's replies and words), which the game keeps nowhere.
        private void Messages(Contact c, List<UiMessageModel> list)
        {
            List<TalkLine> talk = TalkLogGame.Lines(c);
            int said = 0;
            if (c.messagesQueue != null)
            {
                Texts(c, list, talk, ref said);
            }
            for (; talk != null && said < talk.Count; said++)
            {
                Said(list, talk[said]);
            }
            Sample(c, list);
        }

        private static void Said(List<UiMessageModel> list, TalkLine line)
        {
            list.Add(new UiMessageModel { From = line.Mine ? UiFrom.Me : UiFrom.Them, Time = When(line.Minute), Text = line.Text });
        }

        private static int Minute(TextMessage msg) => msg.timestamp != null ? (int)msg.timestamp.GetTotalMinutes() : 0;

        private void Texts(Contact c, List<UiMessageModel> list, List<TalkLine> talk, ref int said)
        {
            foreach (TextMessage msg in c.messagesQueue)
            {
                if (msg == null)
                {
                    continue;
                }
                int minute = Minute(msg);
                for (; talk != null && said < talk.Count && TalkLogRules.Before(talk[said].Minute, minute); said++)
                {
                    Said(list, talk[said]);
                }
                var m = new UiMessageModel { From = msg.isFromPlayer ? UiFrom.Me : msg.isSpecialMessage ? UiFrom.System : UiFrom.Them, Time = When(msg) };
                if (msg.messageKey == "ba:messagetype_taxes" || msg.messageKey == "ba:messagetype_contacts_taxes_repossession_message")
                {
                    ContactsTax.Fill(msg, ref m);
                }
                else
                {
                    m.Text = Words(msg);
                }
                list.Add(m);
                string offer = OfferLine(msg, out bool open);
                if (offer != null && !open)   // an open offer's terms stand on its answer instead (OfferChoices)
                {
                    list.Add(new UiMessageModel { From = UiFrom.System, Text = offer });
                }
            }
        }

        // The tax notice a test shows in a thread, until another contact is opened.
        private static void Sample(Contact c, List<UiMessageModel> list)
        {
            if (ContactsKit.Sample != null && ContactsKit.SampleFor == c)
            {
                var sample = new UiMessageModel { From = UiFrom.Them, Time = When(ContactsKit.Sample) };
                ContactsTax.Fill(ContactsKit.Sample, ref sample);
                list.Add(sample);
                list.Add(new UiMessageModel { From = UiFrom.System, Text = UiText.T("reallife_ui_test_contacts_tax_note") });
            }
            else if (ContactsKit.SampleFor != null && ContactsKit.SampleFor != c)
            {
                ContactsKit.Sample = null;   // the test's notice lasts until another contact is opened
                ContactsKit.SampleFor = null;
            }
        }

        // The terms of the offer still open in a thread, or null: its answer carries them, and the details leave them out.
        private static string OpenTerms(Contact c)
        {
            if (c == null || c.messagesQueue == null)
            {
                return null;
            }
            string terms = null;
            foreach (TextMessage msg in c.messagesQueue)
            {
                bool open = false;
                string line = msg != null ? OfferLine(msg, out open) : null;
                terms = line != null && open ? line : terms;
            }
            return terms;
        }

        // An offer's terms and where it stands, as the game's card under its message shows them; open while it waits for an answer.
        private static string OfferLine(TextMessage msg, out bool open)
        {
            open = false;
            TextMessage.ContextAction action = msg.contextAction;
            if (action == null || SaveGameManager.Current == null)
            {
                return null;
            }
            if (!string.IsNullOrEmpty(action.salaryNegotiationId))
            {
                CandidateSalaryNegotiation n = SaveGameManager.Current.candidateSalaryNegotiations.Find(x => x.id == action.salaryNegotiationId);
                if (n == null)
                {
                    return null;
                }
                string terms = ContactsRules.Plain(LocalizorManager.GetLocalization("dialog_candidate_salary_offer_hourly_wage", (object)new { price = UiText.Money(n.hourlyWage) }));
                open = !n.completed;
                return n.completed ? terms + " \u00b7 " + UiText.T(n.accepted ? "common_accepted" : "common_declined") : terms;
            }
            if (!string.IsNullOrEmpty(action.healthPlanOfferId))
            {
                HealthInsurancePlanOffer o = SaveGameManager.Current.healthInsurancePlanOffers.Find(x => x.id == action.healthPlanOfferId);
                if (o == null)
                {
                    return null;
                }
                string terms = ContactsRules.Plain(LocalizorManager.GetLocalization("dialog_health_insurance_plan_offer_price", (object)new { price = UiText.Money(o.initialOfferPrice) }));
                open = !o.negotiationFinished;
                return o.negotiationFinished ? terms + " \u00b7 " + UiText.T(o.accepted ? "common_accepted" : "common_declined") : terms;
            }
            return null;
        }

        // The offers' buttons the game made in this thread, still open, as numbered choices. An offer of work or of a health
        // plan has the game's three (decline, negotiate, accept): accept first, carrying the terms it takes.
        private void OfferChoices(UiConversationModel t, string terms)
        {
            var found = new List<Button>(MostOffers);
            Offers(found);
            bool offer = terms != null && found.Count == 3;
            int n = offer ? ContactsRules.ChoiceOrder(true, true, true, true, Order) : found.Count;
            var choices = new UiDecisionChoice[n];
            for (int i = 0; i < n; i++)
            {
                Button b = found[offer ? Order[i] : i];
                _choices.Add(b);
                _extras.Add(null);
                choices[i] = new UiDecisionChoice { Name = UiWidgets.Label(b) };
            }
            if (offer)
            {
                choices[0].Lines = new[] { new UiConsequence { Text = terms } };
            }
            t.Choices = choices;
        }

        // The game's offer buttons in the open thread that still take a press; fills the list when given. Returns how many.
        private int Offers(List<Button> into)
        {
            var all = GameContract.TalkOfferButtons.Field != null ? GameContract.TalkOfferButtons.Field.GetValue(null) as List<ContextButton> : null;
            int n = 0;
            for (int i = 0; all != null && i < all.Count && n < MostOffers; i++)
            {
                ContextButton b = all[i];
                Button button = b != null ? b.GetComponent<Button>() : null;
                if (button == null || !button.interactable || !b.gameObject.activeInHierarchy || !b.transform.IsChildOf(_app.transform))
                {
                    continue;
                }
                into?.Add(button);
                n++;
            }
            return n;
        }

        // An offer's negotiation in the thread: the line the game shows now after the messages, its buttons as numbered
        // choices (the confirm first, the cancel last) and its form's fields in the form slot.
        private void Negotiation(List<UiMessageModel> list, UiConversationModel t)
        {
            CanvasGroup view = EntryView();
            if (view == null)
            {
                return;
            }
            string line = Words(view.transform);
            if (!string.IsNullOrEmpty(line))
            {
                list.Add(new UiMessageModel { From = UiFrom.Them, Text = line, Time = Now(), Mood = LineMood() });
            }
            _m.Head.Ring = Mood(_m.Head.Ring);   // the mood the game shows by their line, in the ring of their face
            bool form = Forms(t);
            t.Choices = Choices(view.transform, true, out _);
            t.Typing = t.Choices.Length == 0 && !form;   // their next line is coming
        }

        // A call as the call card: who is on the line and for how long, what they say now, the replies and the way out.
        private void Call(Contact c)
        {
            CanvasGroup view = EntryView();
            var call = new UiCallModel
            {
                Name = _m.Head.Name, Hue = _m.Head.Hue, Icon = _m.Head.Icon, Tint = _m.Head.Tint, Ring = Mood(_m.Head.Ring),
                Role = string.IsNullOrEmpty(_m.Head.Employer) ? _m.Head.Role : _m.Head.Role + " \u00b7 " + _m.Head.Employer,
                Length = ContactsRules.Length(Time.unscaledTime - _talkSince)
            };
            if (view != null)
            {
                call.Line = CallSaidBack(view) ?? Words(view.transform);
                call.Mood = LineMood();
                bool drawn = CallForm(call);   // read first: the bank's loan tells its line what taking it costs
                call.Choices = Choices(view.transform, false, out _hangUp);
                if (_hangUp != null)
                {
                    call.Buttons = new[] { new UiActionModel { Label = UiWidgets.Label(_hangUp), Kind = UiButtonKind.Destructive, Icon = UiIcon.Phone, Key = UiAction.Back } };
                }
                call.Waiting = call.Choices.Length == 0;
                if (!drawn)
                {
                    StepAsideForForm();
                }
            }
            else
            {
                call.Waiting = true;
            }
            call.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") },
                new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_choose") },
                new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") },
            };
            call.PromptCount = call.Prompts.Length;
            _m.Call = call;
        }

        // The game's buttons under the line, in its order (cancel, the second option, confirm), as numbered choices: the
        // confirm first; the cancel last in the thread, or given back as the call's way out.
        private UiDecisionChoice[] Choices(Transform entry, bool cancelAsChoice, out Button cancel)
        {
            cancel = null;
            Transform row = entry.Find("Buttons");
            var buttons = new List<Button>(3);
            for (int i = 0; row != null && row.gameObject.activeSelf && i < row.childCount; i++)
            {
                Button b = row.GetChild(i).GetComponent<Button>();
                if (b != null && b.gameObject.activeSelf)
                {
                    buttons.Add(b);
                }
            }
            DialogEntry e = GameContract.TalkEntry.Field != null ? GameContract.TalkEntry.Field.GetValue(_app) as DialogEntry : null;
            bool hasCancel = e != null && e.OnCancel != null, hasSecond = e != null && e.OnSecondOption != null, hasConfirm = e != null && e.OnConfirm != null;
            if (buttons.Count != (hasCancel ? 1 : 0) + (hasSecond ? 1 : 0) + (hasConfirm ? 1 : 0))
            {
                hasCancel = false;   // a row the game's entry does not explain: every button a reply, in its order
                hasSecond = false;
                hasConfirm = false;
            }
            var choices = new List<UiDecisionChoice>(3);
            if (hasCancel || hasSecond || hasConfirm)
            {
                // The confirm, the second option, the dialog's own answers past the game's three, the cancel: each with the
                // lines the dialog told of it (GameUi.DialogAnswers).
                GameUi.DialogNotes notes = GameUi.DialogAnswers.Of(e);
                int n = ContactsRules.ChoiceOrder(hasCancel, hasSecond, hasConfirm, cancelAsChoice, Order), k = 0;
                if (hasConfirm && k < n)
                {
                    Add(choices, buttons[Order[k++]], DialogChoices.Lines(notes, GameUi.DialogButton.Confirm));
                }
                if (hasSecond && k < n)
                {
                    Add(choices, buttons[Order[k++]], DialogChoices.Lines(notes, GameUi.DialogButton.Second));
                }
                for (int i = 0; notes != null && i < notes.More.Count; i++)
                {
                    _choices.Add(null);
                    _extras.Add(notes.More[i]);
                    choices.Add(DialogChoices.Choice(notes.More[i]));
                }
                if (hasCancel && cancelAsChoice && k < n)
                {
                    Add(choices, buttons[Order[k++]], DialogChoices.Lines(notes, GameUi.DialogButton.Cancel));
                }
                if (hasCancel && !cancelAsChoice && buttons[0].interactable)
                {
                    cancel = buttons[0];
                }
            }
            else
            {
                for (int i = 0; i < buttons.Count; i++)
                {
                    Add(choices, buttons[i], null);
                }
            }
            return choices.ToArray();
        }

        private void Add(List<UiDecisionChoice> choices, Button b, UiConsequence[] lines)
        {
            _choices.Add(b);
            _extras.Add(null);
            choices.Add(new UiDecisionChoice { Name = UiWidgets.Label(b), Lines = lines, Disabled = !b.interactable });
        }

        // The salary offer's two fields or an offer's one, read from the game's own form: its figures and its steps. True when
        // the entry asks for one.
        private bool Forms(UiConversationModel t)
        {
            _fields.Clear();
            Transform form = GameContract.TalkForm.Field != null ? GameContract.TalkForm.Field.GetValue(_app) as Transform : null;
            if (form == null || !form.gameObject.activeInHierarchy)
            {
                t.Fields = new UiFormField[0];
                t.FieldCount = 0;
                return false;
            }
            var salary = form.GetComponent<SalaryOfferSettings>();
            var fields = new List<UiFormField>(2);
            if (salary != null)
            {
                AddField(fields, GameContract.TalkWageField.Field?.GetValue(salary) as PlayerOfferSettings, "dialog_candidate_salary_offer_input_hourly_wage");
                AddField(fields, GameContract.TalkBonusField.Field?.GetValue(salary) as PlayerOfferSettings, "dialog_candidate_salary_offer_input_signing_bonus");
            }
            else
            {
                AddField(fields, form.GetComponent<PlayerOfferSettings>(), "dialog_player_offer_input");
            }
            t.Fields = fields.ToArray();
            t.FieldCount = fields.Count;
            DialogForm.Offers(form, GameContract.TalkEntry.Field != null ? GameContract.TalkEntry.Field.GetValue(_app) as DialogEntry : null);   // what it pays, under Send Offer
            return fields.Count > 0;
        }

        private void AddField(List<UiFormField> into, PlayerOfferSettings offer, string key)
        {
            if (offer == null)
            {
                return;
            }
            var amount = GameContract.TalkAmount.Field?.GetValue(offer) as UI.Components.InputField;
            var less = GameContract.TalkLess.Field?.GetValue(offer) as Button;
            var more = GameContract.TalkMore.Field?.GetValue(offer) as Button;
            TMP_InputField input = amount != null ? amount.tmpInputField : null;
            _fields.Add(new Field { Input = input, Less = less, More = more });
            into.Add(new UiFormField
            {
                Kind = UiFormKind.Amount, Label = ContactsRules.Label(UiText.T(key)), Value = Figure(input),
                CanLess = Shows(less), CanMore = Shows(more), Disabled = input == null || !input.interactable
            });
        }

        // A form the kit cannot draw in a call (a casino's game, nothing it knows): the game's own view answers it, and the kit
        // is back the moment that line is answered or the call ends.
        private void StepAsideForForm()
        {
            DialogEntry e = GameContract.TalkEntry.Field != null ? GameContract.TalkEntry.Field.GetValue(_app) as DialogEntry : null;
            if (e == null || e.Template != DialogEntry.TemplateType.Input)
            {
                return;
            }
            UiDirector.Logger?.Info("Contacts (kit): the call's form '" + e.InputTemplate + "' is answered in the game's own view");
            ContactsApp app = _app;
            float next = 0f;
            ContactsKit.StepAside(() =>
            {
                float now = Time.unscaledTime;
                if (now < next)
                {
                    return false;
                }
                next = now + Every;
                return app.dialog == null || app.contact == null || !ReferenceEquals(GameContract.TalkEntry.Field.GetValue(app), e);
            });
        }

        private CanvasGroup EntryView()
        {
            if (_app.dialog == null || _app.contact == null || GameContract.TalkEntryView.Field == null)
            {
                return null;
            }
            return GameContract.TalkEntryView.Field.GetValue(_app) as CanvasGroup;
        }

        // The words of the line the game copied for the entry: its message, or the form's header.
        private static string Words(Transform entry)
        {
            Transform text = entry.Find("Message/MessageText") ?? entry.Find("HeaderLabel");
            TMP_Text t = text != null ? text.GetComponent<TMP_Text>() : null;
            return t != null && t.gameObject.activeInHierarchy ? ContactsRules.Plain(t.text) ?? string.Empty : string.Empty;
        }

        // The mood the game shows by the line (a negotiation's), else the one the head has.
        private UiRole Mood(UiRole otherwise)
        {
            DialogEntry e = GameContract.TalkEntry.Field != null ? GameContract.TalkEntry.Field.GetValue(_app) as DialogEntry : null;
            if (e == null || e.SecondaryIcon == null)
            {
                return otherwise;
            }
            try
            {
                return ContactsRules.MoodRing(MoodOf(e.SecondaryIcon));
            }
            catch (Exception ex) when (Guard.Missing(ex))
            {
                return otherwise;
            }
        }

        // The game's own faces for a mood, as its negotiations pick them: content, unsure, angry; -2 for another.
        private static int MoodOf(Sprite face)
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

        // ------------------------------------------------------------------------------------------------- prompts and words

        private void Prompts()
        {
            var prompts = new List<UiPrompt>(5)
            {
                new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") },
                new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") },
                new UiPrompt { Action = UiAction.TabPrev, Label = UiText.T("reallife_ui_prompt_group") },
            };
            if (Shows(_call) && _app.selectedContact != null)
            {
                prompts.Add(new UiPrompt { Action = UiAction.Alt, Label = UiText.T("phone_call_button") });
            }
            prompts.Add(new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") });
            _m.Prompts = prompts.ToArray();
            _m.PromptCount = _m.Prompts.Length;
        }

        // The employee a contact is, as the game's header finds them: by name among the staff, then among the candidates.
        private static EmployeeInstance EmployeeOf(string name, out bool candidate)
        {
            candidate = false;
            foreach (EmployeeInstance e in EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { excludeBeingReplaced = true }))
            {
                if (e != null && e.characterData.name == name)
                {
                    return e;
                }
            }
            List<CandidateSalaryNegotiation> talks = SaveGameManager.Current != null ? SaveGameManager.Current.candidateSalaryNegotiations : null;
            for (int i = 0; talks != null && i < talks.Count; i++)
            {
                if (talks[i]?.employeeInstance != null && talks[i].employeeInstance.characterData.name == name)
                {
                    candidate = true;
                    return talks[i].employeeInstance;
                }
            }
            return null;
        }

        // A person has a face: staff, rivals, Uncle Fred and the people of the mod's own; a firm or a service has its picture.
        private static bool Person(Contact c)
        {
            if (c.IsEmployeeContact || c.category == ContactCategoryName.Rivals || c.id == "uncle_fred")
            {
                return true;
            }
            string d = c.description ?? string.Empty;
            if (d == ContactGroups.BossDescription || d.StartsWith(ContactGroups.PersonDescriptionStart, StringComparison.Ordinal))
            {
                return true;
            }
            return ContactCatalogue.TryGet(c.id, out ContactLook look) && look.Kind == PictureKind.Person;
        }

        private static string BusinessType(Contact c) => c.HasAddressAssigned ? BuildingHelper.GetBuildingRegistration(c.Address)?.businessTypeName : null;

        private static string Name(Contact c) => LocalizorManager.IsLocalizedKey(c.id) ? LocalizorManager.GetLocalization(c.id) : c.id;

        private static string Describe(Contact c)
        {
            string d = c.description;
            return string.IsNullOrEmpty(d) ? null : LocalizorManager.IsLocalizedKey(d) ? ContactsRules.Plain(LocalizorManager.GetLocalization(d)) : d;
        }

        private static string Words(TextMessage msg) => ContactsRules.Plain(LocalizorManager.GetLocalization(msg.messageKey, (object)msg.messageData));

        // When a message came, as the game writes a time; an earlier day by its number.
        private static string When(TextMessage msg)
        {
            int today = SaveGameManager.Current != null ? SaveGameManager.Current.Day : msg.timestamp.Day;
            if (msg.timestamp.Day != today)
            {
                return UiText.Fill("reallife_ui_contacts_day", "day", UiText.Whole(msg.timestamp.Day));
            }
            return msg.timestamp.Hour.GetFormattedTime(msg.timestamp.Minute);
        }

        // The same for a line of the talk history, which keeps the game's minute since day 0.
        private static string When(int minute)
        {
            int day = TalkLogRules.DayOf(minute);
            int today = SaveGameManager.Current != null ? SaveGameManager.Current.Day : day;
            if (day != today)
            {
                return UiText.Fill("reallife_ui_contacts_day", "day", UiText.Whole(day));
            }
            return TalkLogRules.HourOf(minute).GetFormattedTime(TalkLogRules.MinuteOf(minute));
        }

        // The game's time now, as it writes a message's: the line the conversation shows came this moment.
        private static string Now() => SaveGameManager.Current != null ? SaveGameManager.Current.Hour.GetFormattedTime(SaveGameManager.Current.Minute) : string.Empty;

        // A form's figure as money in the game's words while it reads as a number; the words typed as they are otherwise.
        private static string Figure(TMP_InputField input)
        {
            string text = input != null ? input.text : string.Empty;
            return float.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out float v) ? UiText.Money(v) : text;
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
