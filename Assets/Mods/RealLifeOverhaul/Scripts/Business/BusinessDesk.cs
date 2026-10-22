using System;
using System.Collections.Generic;
using BAModAPI;
using Dialogs;
using Entities;
using Extensions;
using Localizor;
using UI.Smartphone.Apps.Contacts;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The Business desk: a phone contact that goes through the open issues one at a time, each choice read out with its
    /// cost before it is taken, and whose messages are the record. The same conversation opens face to face for a visitor.
    /// </summary>
    public static class BusinessDesk
    {
        public const string ContactId = "reallife-business";
        private const string DialogName = "reallife_business_calldialogtype";

        private static Contact _contact;
        private static bool _tried;

        /// <summary>True once the contact is there, or adding it has been tried with the options as they are.</summary>
        public static bool Ready => _contact != null || _tried || !Settings.PhoneContacts;

        /// <summary>The issue the next dialogue opens on, read once; null for the whole list.</summary>
        internal static BusinessIssue Focus;

        public static void Setup()
        {
            _contact = null;
            _tried = Settings.Business && Settings.PhoneContacts;
            if (!Settings.Business || !Settings.PhoneContacts)
            {
                return;
            }
            try
            {
                ContactPictures.Add(ContactId);
                Contact contact = Contact.GetContact(ContactId, ContactCategoryName.Business, "reallife_contact_business_desc", null, false, true);
                var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogName);
                contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
                CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new BusinessDeskDialog()));
                if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
                {
                    contact.SendMessage(new TextMessage("reallife_text_business_welcome"), notify: false);
                }
                _contact = contact;
            }
            catch (Exception e)
            {
                BusinessDirector.Logger?.Error(e);
                BusinessDirector.Logger?.Warn("Business pressure: the Business desk contact could not be added; issues are still announced, and decided by their deadlines.");
            }
        }

        /// <summary>A line for the record, in the desk's messages.</summary>
        public static void Text(string key, Dictionary<string, string> data, bool notify = true)
        {
            if (_contact == null)
            {
                return;
            }
            try
            {
                // a copy: the game keeps a message's data while it is listed
                _contact.SendMessage(new TextMessage(key, data != null ? new Dictionary<string, string>(data) : null), notify);
            }
            catch (Exception e)
            {
                BusinessDirector.Logger?.Error(e);
            }
        }

        /// <summary>True while any of the game's dialogue panels is open.</summary>
        public static bool DialogOpen
        {
            get
            {
                var uis = InstanceBehavior<UI.UIs>.Instance;
                return uis != null && uis.playerHUD != null && uis.playerHUD.dialogUI != null && uis.playerHUD.dialogUI.isPanelOpen;
            }
        }

        /// <summary>The desk face to face, in the game's own dialogue panel: on one issue, or on the whole list.</summary>
        public static void OpenInPerson(BusinessIssue issue)
        {
            Focus = issue;
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogName);
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new BusinessDeskDialog()));
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog);
        }
    }

    /// <summary>The Business desk's conversation in the game's dialogue panel: the issues, their options, the racket.</summary>
    public class BusinessDeskDialog : Dialog
    {
        private static readonly IssueOption[] Options = new IssueOption[BusinessIssues.MostOptions];

        // A page for a button of the game's panel: nothing thrown here may reach the game's UI code.
        private static Func<DialogEntry> G(Func<DialogEntry> page)
        {
            return () =>
            {
                try
                {
                    return page();
                }
                catch (Exception e)
                {
                    BusinessDirector.Logger?.Error(e);
                    return PhoneContacts.Closing("reallife_biz_desk_gone");
                }
            };
        }

        public BusinessDeskDialog()
        {
            npcNameKey = BusinessDesk.ContactId;
            BusinessIssue focus = BusinessDesk.Focus;
            BusinessDesk.Focus = null;
            DialogEntry entry;
            try
            {
                if (!Settings.Business || !BusinessState.Loaded || BusinessDirector.Instance == null)
                {
                    entry = PhoneContacts.Closing("reallife_biz_desk_off");
                }
                else if (focus != null && BusinessState.Issues.Contains(focus))
                {
                    entry = IssuePage(BusinessState.Issues.IndexOf(focus));
                }
                else
                {
                    entry = Page(0);
                }
            }
            catch (Exception e)
            {
                BusinessDirector.Logger?.Error(e);
                entry = PhoneContacts.Closing("reallife_biz_desk_off");
            }
            DialogController.current.ShowEntry(Guard.Entry(entry));
        }

        private static bool RacketListed =>
            BusinessState.Racket != RacketStage.None && BusinessState.Racket != RacketStage.Ended && BusinessState.Racket != RacketStage.Demand;

        private static int Count => BusinessState.Issues.Count + (RacketListed ? 1 : 0);

        private static DialogEntry Page(int index)
        {
            int count = Count;
            if (count == 0)
            {
                return PhoneContacts.Closing(Settings.BusinessEasy ? "reallife_biz_desk_empty_easy" : "reallife_biz_desk_empty");
            }
            index %= count;
            return index < BusinessState.Issues.Count ? IssuePage(index) : RacketPage();
        }

        private static string Fill(string key, Dictionary<string, string> data)
        {
            string text = key.GetLocalization();
            if (data != null && text.IndexOf('{') >= 0)
            {
                foreach (KeyValuePair<string, string> pair in data)
                {
                    text = text.Replace("{" + pair.Key + "}", pair.Value);
                }
            }
            return text;
        }

        private static DialogEntry Say(string key, Dictionary<string, string> data)
        {
            return PhoneContacts.Closing("reallife_biz_desk_say", new { text = Fill(key, data) });
        }

        private static DialogEntry IssuePage(int index)
        {
            if (index < 0 || index >= BusinessState.Issues.Count)
            {
                return Page(0);
            }
            BusinessIssue issue = BusinessState.Issues[index];
            BuildingRegistration reg = BusinessGame.Find(issue.Place);
            if (reg == null)
            {
                return PhoneContacts.Closing("reallife_biz_desk_gone");
            }
            Dictionary<string, string> data = BusinessIssues.Data(issue, reg);
            int options = BusinessIssues.Options(issue, reg, Options);
            var entry = new DialogEntry
            {
                messageData = "reallife_biz_desk_issue".Localize(new
                {
                    name = issue.Def.NameKey.GetLocalization(),
                    business = BusinessGame.Name(reg),
                    text = Fill(BusinessIssues.TextKey(issue), data),
                    day = issue.DueDay.ToString(),
                    time = issue.DueHour.GetFormattedTime(0f, true),
                    today = TimeHelper.CurrentDay.ToString()
                }),
                OnCancel = PhoneContacts.HangUp
            };
            bool more = Count > 1;
            if (options > 0)
            {
                entry.ConfirmTextOverride = "reallife_biz_desk_options".Localize();
                entry.OnConfirm = G(() => OptionPage(issue, 0));
                if (more)
                {
                    entry.SecondOptionTextOverride = "reallife_biz_desk_next";
                    entry.OnSecondOption = G(() => Page(index + 1));
                }
            }
            else if (more)
            {
                entry.ConfirmTextOverride = "reallife_biz_desk_next".Localize();
                entry.OnConfirm = G(() => Page(index + 1));
            }
            else
            {
                entry.ConfirmTextOverride = "reallife_biz_desk_noted".Localize();
                entry.OnConfirm = G(() => PhoneContacts.Closing("reallife_biz_desk_bye"));
            }
            return entry;
        }

        // One way at a time: what it means and costs, then "do this", "another way" or "decide later".
        private static DialogEntry OptionPage(BusinessIssue issue, int option)
        {
            BuildingRegistration reg = BusinessGame.Find(issue.Place);
            if (reg == null || !BusinessState.Issues.Contains(issue))
            {
                return PhoneContacts.Closing("reallife_biz_desk_gone");
            }
            int count = BusinessIssues.Options(issue, reg, Options);
            if (count == 0)
            {
                return PhoneContacts.Closing("reallife_biz_desk_gone");
            }
            option %= count;
            IssueOption picked = Options[option];
            Dictionary<string, string> data = BusinessIssues.Data(issue, reg);
            data["amount"] = ((float)picked.Amount).ToCurrencyFormat();
            string text = Fill(picked.Text, data);
            if (picked.Blocked)
            {
                text += "\n" + "reallife_biz_desk_blocked".GetLocalization();
            }
            var entry = new DialogEntry
            {
                messageData = "reallife_biz_desk_option".Localize(new
                {
                    number = (option + 1).ToString(),
                    count = count.ToString(),
                    text
                }),
                CancelTextOverride = "reallife_biz_desk_later",
                OnCancel = PhoneContacts.HangUp
            };
            int next = option + 1;
            if (picked.Blocked)
            {
                entry.ConfirmTextOverride = "reallife_biz_desk_another".Localize();
                entry.OnConfirm = G(() => OptionPage(issue, next));
            }
            else
            {
                entry.ConfirmTextOverride = picked.Label.Localize();
                entry.OnConfirm = G(() => Choose(issue, option));
                if (count > 1)
                {
                    entry.SecondOptionTextOverride = "reallife_biz_desk_another";
                    entry.OnSecondOption = G(() => OptionPage(issue, next));
                }
            }
            return entry;
        }

        private static DialogEntry Choose(BusinessIssue issue, int option)
        {
            BuildingRegistration reg = BusinessGame.Find(issue.Place);
            if (reg == null || !BusinessState.Issues.Contains(issue))
            {
                return PhoneContacts.Closing("reallife_biz_desk_gone");
            }
            string key = BusinessIssues.Choose(issue, reg, option, out Dictionary<string, string> data);
            if (key != "reallife_biz_cannot" && key != "reallife_biz_desk_gone")
            {
                BusinessDesk.Text(key, data, notify: false);   // for the record: they have just been told
            }
            return Say(key, data);
        }

        // The racket between demands: where it stands, and while paying the two ways out.
        private static DialogEntry RacketPage()
        {
            BuildingRegistration reg = BusinessGame.Find(BusinessState.RacketPlace);
            var data = new Dictionary<string, string>(6)
            {
                { "business", BusinessGame.Name(reg) },
                { "amount", ((float)BusinessState.RacketWeekly).ToCurrencyFormat() },
                { "day", BusinessState.RacketDay.ToString() },
                { "time", BusinessCatalogue.RacketStingHour.GetFormattedTime(0f, true) },
                { "security", reg != null ? BusinessGame.Security(reg).ToString() : "0" },
                { "need", BusinessCatalogue.RacketSecurityLevel.ToString() }
            };
            string key;
            switch (BusinessState.Racket)
            {
                case RacketStage.Paying: key = "reallife_biz_racket_page_paying"; break;
                case RacketStage.Refused: key = "reallife_biz_racket_page_refused"; break;
                case RacketStage.Reported: key = "reallife_biz_racket_page_reported"; break;
                case RacketStage.Sting: key = "reallife_biz_racket_page_sting"; break;
                default: key = "reallife_biz_racket_page_securing"; break;
            }
            var entry = new DialogEntry
            {
                messageData = "reallife_biz_desk_say".Localize(new { text = Fill(key, data) }),
                OnCancel = PhoneContacts.HangUp
            };
            if (BusinessState.Racket == RacketStage.Paying)
            {
                entry.ConfirmTextOverride = "reallife_biz_racket_do_stop".Localize();
                entry.OnConfirm = G(() => RacketChange(false, data));
                entry.SecondOptionTextOverride = "reallife_biz_racket_do_police";
                entry.OnSecondOption = G(() => RacketChange(true, data));
            }
            else if (BusinessState.Issues.Count > 0)
            {
                entry.ConfirmTextOverride = "reallife_biz_desk_next".Localize();
                entry.OnConfirm = G(() => Page(0));
            }
            else
            {
                entry.ConfirmTextOverride = "reallife_biz_desk_noted".Localize();
                entry.OnConfirm = G(() => PhoneContacts.Closing("reallife_biz_desk_bye"));
            }
            return entry;
        }

        private static DialogEntry RacketChange(bool police, Dictionary<string, string> data)
        {
            string key = BusinessIssues.RacketChange(police);
            BusinessDirector.Logger?.Info("Business: racket, from the desk: " + (police ? "reported to the police" : "payments stopped") + " (" + key + ").");
            BusinessDesk.Text(key, data, notify: false);
            return Say(key, data);
        }
    }
}
