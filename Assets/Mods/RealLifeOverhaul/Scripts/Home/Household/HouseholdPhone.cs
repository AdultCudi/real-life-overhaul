using System;
using System.Globalization;
using BAModAPI;
using Dialogs;
using Entities;
using Extensions;
using Localizor;
using Localizor.LanguageChangeEvent;
using UI.Smartphone.Apps.Contacts;

namespace RealLifeOverhaul.Household
{
    /// <summary>
    /// The "Household staff" contact in the game's Contacts app, an agency: a call says who works for the player, puts
    /// forward somebody of each kind to take on (one page each, "Someone else" for the next), and lets somebody go. Every
    /// page is the game's own call panel, its answers in the panel's two buttons and its cancel to hang up.
    /// </summary>
    public static class HouseholdPhone
    {
        public const string ContactId = "reallife-household";
        private const string DialogTypeName = "reallife_household_calldialogtype";

        /// <summary>The contact, once it has been added to this save's phone; null before, and with the phone's contacts off.</summary>
        public static Contact Contact { get; private set; }

        public static void Reset()
        {
            Contact = null;
        }

        /// <summary>Adds the contact (or finds it again in a loaded save) and gives it its call. Safe to call again.</summary>
        public static void Setup()
        {
            Contact = null;
            if (!Settings.HouseholdStaff || !Settings.PhoneContacts || SaveGameManager.Current == null)
            {
                return;
            }
            try
            {
                ContactPictures.Add(ContactId);
                Contact contact = Contact.GetContact(ContactId, ContactCategoryName.General, HouseholdText.ContactDesc, null, false, true);
                var type = (CallDialogType)ModEnumHash.GetSafeHash(DialogTypeName);
                contact.callDialogTypeOverride = type;   // not saved by the game, so set on every load
                CallDialogFactory.RegisterDialog(type, Guard.Maker(() => new HouseholdCallDialog()));
                if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
                {
                    contact.SendMessage(new TextMessage(HouseholdText.Welcome), notify: false);
                }
                Contact = contact;
            }
            catch (Exception e)
            {
                HouseholdDirector.Logger?.Error(e);
                HouseholdDirector.Logger?.Warn("Household staff: the Household staff contact could not be added; the day's notes are shown as notices instead.");
            }
        }

        internal static DialogEntry Menu()
        {
            HouseholdDirector d = HouseholdDirector.Instance;
            if (d == null || !d.Running)
            {
                return Close("reallife_household_phone_off", default);
            }
            HouseholdState s = d.State;
            if (!s.Any && d.Home == null)
            {
                return Close("reallife_household_page_no_home", default);
            }
            string staff = null;
            for (int i = 0; i < s.Staff.Count; i++)
            {
                StaffMember m = s.Staff[i];
                StaffRole role = HouseholdRules.Role(m.Kind);
                string line = HouseholdText.T(role.ShareBp > 0 ? "reallife_household_page_staff_line" : "reallife_household_page_staff_line_mechanic",
                    StaffFill.For(m.Name).With(HouseholdText.KindWord(m.Kind) + " " + HouseholdText.When(role)).Cost(Visit(m, role)));
                staff = staff == null ? line : staff + "\n" + line;
            }
            var f = new StaffFill().With(staff ?? HouseholdText.T("reallife_household_page_nobody"));
            return Entry(HouseholdText.Say("reallife_household_page_main", f),
                "reallife_household_do_hire", () => Offer(HouseholdRules.NextOpen(StaffKind.None, s.Staff)),
                "reallife_household_do_let_go", s.Any ? () => Member(0) : (Func<DialogEntry>)null);
        }

        private static int Visit(StaffMember m, StaffRole role) => HouseholdRules.VisitWage(m.Hourly, role);

        private static DialogEntry Offer(StaffKind kind)
        {
            HouseholdDirector d = HouseholdDirector.Instance;
            StaffRole role = HouseholdRules.Role(kind);
            if (role == null)
            {
                return Close("reallife_household_page_all_hired", default);
            }
            StaffCandidate c = d.Candidate(kind, out int hourly);
            LanguageChangeEventDataHolder text;
            if (role.Kind == StaffKind.Mechanic)
            {
                text = "reallife_household_candidate_mechanic".Localize(new
                {
                    name = c.Name,
                    age = c.Age.ToString(CultureInfo.InvariantCulture),
                    when = HouseholdText.When(role),
                    does = HouseholdText.T(role.Does),
                    callout = HouseholdText.Money(HouseholdRules.CallOutAt(HouseholdGame.PriceLevel)),
                    wash = HouseholdText.Money(HouseholdRules.WashFee)
                });
            }
            else
            {
                text = "reallife_household_candidate".Localize(new
                {
                    name = c.Name,
                    age = c.Age.ToString(CultureInfo.InvariantCulture),
                    kind = HouseholdText.KindWord(kind),
                    when = HouseholdText.When(role),
                    does = HouseholdText.T(role.Does),
                    hourly = (hourly / 100f).ToCurrencyFormat(),
                    visit = HouseholdText.Money(HouseholdRules.VisitWage(hourly, role)),
                    week = HouseholdText.Money(HouseholdRules.WeekWage(hourly, role))
                });
            }
            string name = c.Name;
            return Entry(text,
                "reallife_household_do_hire_this", () =>
                {
                    string refusal = d.Hire(kind);
                    return Close(refusal ?? "reallife_household_hired", StaffFill.For(name).With(HouseholdText.KindWord(kind)).Hours(role));
                },
                "reallife_household_do_next", () =>
                {
                    d.PassOver(kind);
                    StaffKind next = HouseholdRules.NextOpen(kind, d.State.Staff);
                    return Offer(next != StaffKind.None ? next : kind);
                });
        }

        private static DialogEntry Member(int index)
        {
            HouseholdDirector d = HouseholdDirector.Instance;
            HouseholdState s = d.State;
            if (s.Staff.Count == 0)
            {
                return Close("reallife_household_page_nobody", default);
            }
            StaffMember m = s.Staff[index % s.Staff.Count];
            StaffRole role = HouseholdRules.Role(m.Kind);
            var text = "reallife_household_member".Localize(new
            {
                name = m.Name,
                kind = HouseholdText.KindWord(m.Kind),
                when = HouseholdText.When(role),
                days = Math.Max(0, HouseholdGame.Day - m.HiredDay).ToString(CultureInfo.InvariantCulture),
                visits = m.Visits.ToString(CultureInfo.InvariantCulture),
                paid = HouseholdText.Money(m.PaidTotal)
            });
            return Entry(text,
                "reallife_household_do_let_go_this", () =>
                {
                    string name = m.Name;
                    d.LetGo(m);
                    return Close("reallife_household_let_go_done", StaffFill.For(name));
                },
                "reallife_household_do_next_member", s.Staff.Count > 1 ? () => Member(index + 1) : (Func<DialogEntry>)null);
        }

        private static DialogEntry Entry(LanguageChangeEventDataHolder text, string firstKey, Func<DialogEntry> first, string secondKey, Func<DialogEntry> second)
        {
            return new DialogEntry
            {
                messageData = text,
                ConfirmTextOverride = firstKey.Localize(),
                OnConfirm = Safe(first),
                SecondOptionTextOverride = second != null ? secondKey : null,
                OnSecondOption = second != null ? Safe(second) : null,
                OnCancel = Leave
            };
        }

        /// <summary>A last line: the call ends by itself once it is read.</summary>
        private static DialogEntry Close(string key, in StaffFill f)
        {
            DialogController call = DialogController.current;
            return new DialogEntry
            {
                messageData = HouseholdText.Say(key, f),
                OnVisible = call != null ? call.FinishDialog : (Action)null,
                OnCancel = call != null ? (Action)null : Leave
            };
        }

        private static void Leave()
        {
            if (DialogController.current != null)
            {
                DialogController.current.CancelDialog();
            }
        }

        internal static Func<DialogEntry> Safe(Func<DialogEntry> action)
        {
            return () =>
            {
                try
                {
                    HouseholdDirector d = HouseholdDirector.Instance;
                    if (d == null || !d.Running || SaveGameManager.Current == null)
                    {
                        return Close("reallife_household_phone_off", default);
                    }
                    return action();
                }
                catch (Exception e)
                {
                    HouseholdDirector.Logger?.Error(e);
                    return Close("reallife_household_phone_off", default);
                }
            };
        }
    }

    /// <summary>A call to the Household staff contact.</summary>
    public sealed class HouseholdCallDialog : Dialog
    {
        public HouseholdCallDialog()
        {
            npcNameKey = HouseholdPhone.ContactId;
            DialogController.current.ShowEntry(Guard.Entry(HouseholdPhone.Safe(HouseholdPhone.Menu)()));
        }
    }
}
