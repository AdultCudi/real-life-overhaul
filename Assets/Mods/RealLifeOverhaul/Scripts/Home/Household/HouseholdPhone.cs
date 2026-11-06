using System;
using System.Collections.Generic;
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
    /// The "Household staff" contact in the game's Contacts app, an agency: a call says who works for the player at which
    /// home, asks which home the next person is for (with more than one), puts forward somebody of each kind that home has
    /// nobody of (one page each, "Someone else" for the next), moves somebody to another home and lets somebody go. Every
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

        // The player's homes and the ones a member may move to, filled for one page of the call and read at once.
        private static readonly List<BuildingRegistration> Homes = new List<BuildingRegistration>(4);
        private static readonly List<BuildingRegistration> MoveTargets = new List<BuildingRegistration>(4);

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
            // Grouped by home, each home under its address, in the order its first member was taken on.
            string staff = null;
            for (int i = 0; i < s.Staff.Count; i++)
            {
                StaffMember first = s.Staff[i];
                if (EarlierAtHome(s, i))
                {
                    continue;
                }
                string block = HouseholdText.T("reallife_household_page_home", new StaffFill().At(HouseholdGame.Where(first.HomeStreet, first.HomeNumber)));
                for (int j = i; j < s.Staff.Count; j++)
                {
                    StaffMember m = s.Staff[j];
                    if (!m.WorksAt(first.HomeStreet, first.HomeNumber))
                    {
                        continue;
                    }
                    StaffRole role = HouseholdRules.Role(m.Kind);
                    block += "\n" + HouseholdText.T(role.ShareBp > 0 ? "reallife_household_page_staff_line" : "reallife_household_page_staff_line_mechanic",
                        StaffFill.For(m.Name).With(HouseholdText.KindWord(m.Kind) + " " + HouseholdText.When(role)).Cost(Visit(m, role)));
                }
                staff = staff == null ? block : staff + "\n" + block;
            }
            var f = new StaffFill().With(staff ?? HouseholdText.T("reallife_household_page_nobody"));
            return Entry(HouseholdText.Say("reallife_household_page_main", f),
                "reallife_household_do_hire", () => PickHome(0),
                "reallife_household_do_staff", s.Any ? () => Member(0) : (Func<DialogEntry>)null);
        }

        private static bool EarlierAtHome(HouseholdState s, int i)
        {
            StaffMember m = s.Staff[i];
            for (int j = 0; j < i; j++)
            {
                if (s.Staff[j].WorksAt(m.HomeStreet, m.HomeNumber))
                {
                    return true;
                }
            }
            return false;
        }

        private static int Visit(StaffMember m, StaffRole role) => HouseholdRules.VisitWage(m.Hourly, role);

        // Which home the next person is for: straight on with one home; with more, one home a page, with who works there now.
        private static DialogEntry PickHome(int index)
        {
            HouseholdDirector d = HouseholdDirector.Instance;
            d.Homes(Homes);
            int count = Homes.Count;
            if (count == 0)
            {
                return Close("reallife_household_page_no_home", default);
            }
            BuildingRegistration home = Homes[index % count];
            Homes.Clear();
            string street = home.StreetName;
            int number = home.StreetNumber;
            if (count == 1)
            {
                return OfferAt(street, number);
            }
            string who = null;
            List<StaffMember> staff = d.State.Staff;
            for (int i = 0; i < staff.Count; i++)
            {
                if (staff[i].WorksAt(street, number))
                {
                    who = (who == null ? string.Empty : who + ", ") + HouseholdText.KindWord(staff[i].Kind);
                }
            }
            string there = who == null ? HouseholdText.T("reallife_household_page_home_nobody") : HouseholdText.T("reallife_household_page_home_staff", new StaffFill().With(who));
            return Entry(HouseholdText.Say("reallife_household_page_pick_home", new StaffFill().At(HouseholdGame.Where(street, number)).With(there)),
                "reallife_household_do_this_home", () => OfferAt(street, number),
                "reallife_household_do_other_home", () => PickHome(index + 1));
        }

        // The agency's person for the first kind that home has nobody of, or why nobody can be taken on for it.
        private static DialogEntry OfferAt(string street, int number)
        {
            HouseholdDirector d = HouseholdDirector.Instance;
            List<StaffMember> staff = d.State.Staff;
            StaffKind kind = HouseholdRules.NextOpen(StaffKind.None, staff, street, number);
            if (kind == StaffKind.None)
            {
                return Close("reallife_household_page_all_hired_here", new StaffFill().At(HouseholdGame.Where(street, number)));
            }
            if (HouseholdRules.CanHire(staff, kind, street, number) == HireRefusal.HomesFull)
            {
                return Close("reallife_household_no_homes_most", new StaffFill().Count(HouseholdRules.HomesMost));
            }
            return Offer(kind, street, number);
        }

        private static DialogEntry Offer(StaffKind kind, string street, int number)
        {
            HouseholdDirector d = HouseholdDirector.Instance;
            StaffRole role = HouseholdRules.Role(kind);
            if (role == null)
            {
                return Close("reallife_household_page_all_hired_here", new StaffFill().At(HouseholdGame.Where(street, number)));
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
                    string refusal = d.Hire(kind, HouseholdGame.HomeAt(street, number));
                    return Close(refusal ?? "reallife_household_hired_at",
                        StaffFill.For(name).With(HouseholdText.KindWord(kind)).Hours(role).At(HouseholdGame.Where(street, number)).Count(HouseholdRules.HomesMost));
                },
                "reallife_household_do_next", () =>
                {
                    d.PassOver(kind);
                    StaffKind next = HouseholdRules.NextOpen(kind, d.State.Staff, street, number);
                    return Offer(next != StaffKind.None ? next : kind, street, number);
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
            var text = "reallife_household_member_at".Localize(new
            {
                name = m.Name,
                kind = HouseholdText.KindWord(m.Kind),
                where = HouseholdGame.Where(m.HomeStreet, m.HomeNumber),
                when = HouseholdText.When(role),
                days = Math.Max(0, HouseholdGame.Day - m.HiredDay).ToString(CultureInfo.InvariantCulture),
                visits = m.Visits.ToString(CultureInfo.InvariantCulture),
                paid = HouseholdText.Money(m.PaidTotal)
            });
            return Entry(text,
                "reallife_household_do_manage", () => Manage(m),
                "reallife_household_do_next_member", s.Staff.Count > 1 ? () => Member(index + 1) : (Func<DialogEntry>)null);
        }

        // Move a member to another home, or let them go; with no other home that can take them, only the second.
        private static DialogEntry Manage(StaffMember m)
        {
            HouseholdDirector d = HouseholdDirector.Instance;
            if (!d.State.Staff.Contains(m))
            {
                return Member(0);
            }
            Func<DialogEntry> letGo = () =>
            {
                string name = m.Name;
                d.LetGo(m);
                return Close("reallife_household_let_go_done", StaffFill.For(name));
            };
            var f = StaffFill.For(m.Name).At(HouseholdGame.Where(m.HomeStreet, m.HomeNumber));
            int targets = Targets(d, m);
            MoveTargets.Clear();
            if (targets == 0)
            {
                return Entry(HouseholdText.Say("reallife_household_page_manage_stay", f), "reallife_household_do_let_go_this", letGo, null, null);
            }
            return Entry(HouseholdText.Say("reallife_household_page_manage", f),
                "reallife_household_do_move", () => MoveTo(m, 0),
                "reallife_household_do_let_go_this", letGo);
        }

        // One page for each home the member may move to.
        private static DialogEntry MoveTo(StaffMember m, int index)
        {
            HouseholdDirector d = HouseholdDirector.Instance;
            int count = Targets(d, m);
            if (count == 0 || !d.State.Staff.Contains(m))
            {
                MoveTargets.Clear();
                return Manage(m);
            }
            BuildingRegistration target = MoveTargets[index % count];
            MoveTargets.Clear();
            string street = target.StreetName;
            int number = target.StreetNumber;
            string where = HouseholdGame.Where(street, number);
            return Entry(HouseholdText.Say("reallife_household_page_move", StaffFill.For(m.Name).At(where)),
                "reallife_household_do_move_here", () =>
                {
                    string name = m.Name;
                    string refusal = d.Move(m, HouseholdGame.HomeAt(street, number));
                    return Close(refusal ?? "reallife_household_moved_done", StaffFill.For(name).At(where).Count(HouseholdRules.HomesMost));
                },
                "reallife_household_do_other_home", count > 1 ? () => MoveTo(m, index + 1) : (Func<DialogEntry>)null);
        }

        // Fills MoveTargets with the player's homes the member may move to; how many.
        private static int Targets(HouseholdDirector d, StaffMember m)
        {
            d.Homes(Homes);
            MoveTargets.Clear();
            for (int i = 0; i < Homes.Count; i++)
            {
                if (HouseholdRules.CanMove(d.State.Staff, m, Homes[i].StreetName, Homes[i].StreetNumber))
                {
                    MoveTargets.Add(Homes[i]);
                }
            }
            Homes.Clear();
            return MoveTargets.Count;
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
