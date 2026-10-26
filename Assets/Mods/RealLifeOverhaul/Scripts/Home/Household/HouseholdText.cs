using System.Collections.Generic;
using Entities;
using Extensions;
using Localizor;
using Localizor.LanguageChangeEvent;
using UI.Notification;

namespace RealLifeOverhaul.Household
{
    /// <summary>What a line of household staff text may be filled with: every text uses the same few names.</summary>
    public struct StaffFill
    {
        public string Name;      // {name}: the person
        public string Amount;    // {amount}: money, already formatted
        public string N;         // {n}: a number
        public string Text;      // {text}: a line built elsewhere
        public string From;      // {from}: an hour of the day
        public string To;        // {to}
        public string Where;     // {where}: a home's address, already formatted

        public static StaffFill For(string name) => new StaffFill { Name = name };

        public StaffFill Cost(int amount) { Amount = HouseholdText.Money(amount); return this; }
        public StaffFill Count(int n) { N = n.ToString(); return this; }
        public StaffFill With(string text) { Text = text; return this; }
        public StaffFill Hours(StaffRole role) { From = HouseholdText.Hour(role.From); To = HouseholdText.Hour(role.From + role.Hours); return this; }
        public StaffFill At(string where) { Where = where; return this; }
    }

    /// <summary>Text for household staff: a key and its fill as a notice, a text message or a page of the call; never built on a timer.</summary>
    public static class HouseholdText
    {
        public const string Title = "reallife_meta_title_household";
        public const string Header = "reallife_household_header";
        public const string Enabled = "reallife_household_enabled";
        public const string About = "reallife_guide_household_about";
        public const string Guide = "reallife_guide_household";
        public const string ContactDesc = "reallife_contact_household_desc";
        public const string Welcome = "reallife_text_household_welcome";
        public const string Note = "reallife_household_note";
        public const string Arrived = "reallife_household_arrived";
        public const string Unpaid = "reallife_household_note_unpaid";
        public const string Quit = "reallife_household_note_quit";
        public const string Moved = "reallife_household_moved";
        public const string NoHome = "reallife_household_no_home_left";
        public const string MovedOne = "reallife_household_moved_one";
        public const string LeftNoRoom = "reallife_household_left_no_room";

        // Written out in full for the locale check.
        public static readonly string[] Keys =
        {
            "reallife-household", "reallife_household_kind_cleaner", "reallife_household_kind_cook", "reallife_household_kind_butler", "reallife_household_kind_mechanic",
            "reallife_household_when_cleaner", "reallife_household_when_cook", "reallife_household_when_butler", "reallife_household_when_mechanic",
            "reallife_household_does_cleaner", "reallife_household_does_cook", "reallife_household_does_butler", "reallife_household_does_mechanic",
            "reallife_household_page_main", "reallife_household_page_nobody", "reallife_household_page_staff_line", "reallife_household_page_staff_line_mechanic",
            "reallife_household_page_no_home", "reallife_household_do_next_member", "reallife_household_note_line",
            "reallife_household_page_all_hired", "reallife_household_candidate", "reallife_household_candidate_mechanic", "reallife_household_member",
            "reallife_household_do_hire", "reallife_household_do_let_go", "reallife_household_do_next", "reallife_household_do_hire_this", "reallife_household_do_let_go_this",
            "reallife_household_hired", "reallife_household_let_go_done", "reallife_household_no_already", "reallife_household_no_cooking", "reallife_household_phone_off",
            "reallife_household_note_cleaned", "reallife_household_note_cooked", "reallife_household_note_butler", "reallife_household_note_butler_clean",
            "reallife_household_note_washed", "reallife_household_note_mechanic", "reallife_household_note_mechanic_nothing", "reallife_household_note_no_cars",
            "reallife_household_note_full", "reallife_household_note_no_cooking", "reallife_household_work_washed", "reallife_household_work_repaired",
            "reallife_household_work_serviced", "reallife_household_test_hire", "reallife_household_test_day", "reallife_household_test_show",
            "reallife_household_test_mechanic", "reallife_household_test_back", "reallife_household_test_hired", "reallife_household_test_day_done",
            "reallife_household_test_shown", "reallife_household_test_mechanic_done", "reallife_household_test_back_done", "reallife_household_test_nobody",
            "reallife_household_test_not_home", "reallife_household_test_no_cars", "reallife_household_test_not_shown",
            "reallife_transaction_household_cleaner", "reallife_transaction_household_cook", "reallife_transaction_household_butler",
            "reallife_transaction_household_mechanic", "reallife_opt_sec_household", "reallife_opt_tip_home_household",
            "reallife_household_no_homes_most", "reallife_household_do_staff", "reallife_household_page_home", "reallife_household_page_pick_home",
            "reallife_household_page_home_nobody", "reallife_household_page_home_staff", "reallife_household_do_this_home", "reallife_household_do_other_home",
            "reallife_household_page_all_hired_here", "reallife_household_hired_at", "reallife_household_member_at", "reallife_household_do_manage",
            "reallife_household_page_manage", "reallife_household_page_manage_stay", "reallife_household_do_move", "reallife_household_page_move",
            "reallife_household_do_move_here", "reallife_household_moved_done", "reallife_household_test_hamptons", "reallife_household_test_homes",
            "reallife_household_test_not_hamptons", "reallife_household_test_hamptons_done", "reallife_household_test_two_homes", "reallife_household_test_homes_done",
            Title, Header, Enabled, About, Guide, ContactDesc, Welcome, Note, Arrived, Unpaid, Quit, Moved, NoHome, MovedOne, LeftNoRoom
        };

        /// <summary>Notices, each at most 240 characters in English (the text check measures them).</summary>
        public static readonly string[] Notices =
        {
            Arrived, Unpaid, Quit, Moved, NoHome, "reallife_household_test_hired", "reallife_household_test_day_done", "reallife_household_test_shown",
            "reallife_household_test_mechanic_done", "reallife_household_test_back_done", "reallife_household_test_nobody", "reallife_household_test_not_home",
            "reallife_household_test_no_cars", "reallife_household_test_not_shown", MovedOne, LeftNoRoom, "reallife_household_test_not_hamptons",
            "reallife_household_test_hamptons_done", "reallife_household_test_two_homes", "reallife_household_test_homes_done"
        };

        public static string Money(int amount) => ((float)amount).ToCurrencyFormat();

        public static string Hour(int hour) => (hour % 24).GetFormattedTime(0f);

        public static string T(string key) => key.GetLocalization();

        public static string T(string key, in StaffFill f)
        {
            string text = key.GetLocalization();
            if (text == null || text.IndexOf('{') < 0)
            {
                return text;
            }
            return text.Replace("{name}", f.Name ?? string.Empty).Replace("{amount}", f.Amount ?? string.Empty).Replace("{n}", f.N ?? string.Empty)
                .Replace("{text}", f.Text ?? string.Empty).Replace("{from}", f.From ?? string.Empty).Replace("{to}", f.To ?? string.Empty)
                .Replace("{where}", f.Where ?? string.Empty);
        }

        public static LanguageChangeEventDataHolder Say(string key, in StaffFill f)
        {
            return key.Localize(new
            {
                name = f.Name ?? string.Empty,
                amount = f.Amount ?? string.Empty,
                n = f.N ?? string.Empty,
                text = f.Text ?? string.Empty,
                from = f.From ?? string.Empty,
                to = f.To ?? string.Empty,
                where = f.Where ?? string.Empty
            });
        }

        // A new dictionary each time: the game keeps hold of it for as long as the notice or the message is listed.
        private static Dictionary<string, string> Data(in StaffFill f)
        {
            return new Dictionary<string, string>(7)
            {
                { "name", f.Name ?? string.Empty }, { "amount", f.Amount ?? string.Empty }, { "n", f.N ?? string.Empty },
                { "text", f.Text ?? string.Empty }, { "from", f.From ?? string.Empty }, { "to", f.To ?? string.Empty }, { "where", f.Where ?? string.Empty }
            };
        }

        public static void Toast(NotificationType type, string key, in StaffFill f, float seconds = 9f)
        {
            Notifications.Show(type, key, Data(f), seconds);
        }

        /// <summary>A text from the Household staff contact; with the phone's contacts off it is said on screen instead.</summary>
        public static void Message(string key, in StaffFill f)
        {
            Contact contact = HouseholdPhone.Contact;
            if (contact == null)
            {
                Toast(NotificationType.Info, key, f, 12f);
                return;
            }
            contact.SendMessage(new TextMessage(key, Data(f)));
        }

        public static string KindWord(StaffKind kind)
        {
            StaffRole role = HouseholdRules.Role(kind);
            return role != null ? T(role.Title) : string.Empty;
        }

        /// <summary>Their days and hours in words.</summary>
        public static string When(StaffRole role) => role != null ? T(role.When, new StaffFill().Hours(role)) : string.Empty;

        public static string DishName(int dishIndex)
        {
            Dish dish = HouseholdRules.DishAt(dishIndex);
            return dish != null ? T(dish.Name) : string.Empty;
        }
    }
}
