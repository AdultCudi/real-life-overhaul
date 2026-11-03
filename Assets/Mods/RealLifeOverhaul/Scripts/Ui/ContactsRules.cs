using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The rules the kit's Contacts reads the game by, game-free so EconomyCheck holds them: each group's glyph and name
    /// key (the game's seven categories, then the mod's groups, numbers 101 to 107), a firm's glyph from its kind of
    /// business, which of the game's conversations stays in the thread (an offer's negotiation) and which is a call, the
    /// order of the game's three buttons as numbered choices, a call's length in words and the game's rich text as plain
    /// words. Cost: a few comparisons each, called on a layout.
    /// </summary>
    public static class ContactsRules
    {
        public const int ModGroups = 101;   // the first of the mod's groups (Phone/Contacts/ContactGroups: Base 100, Family 1 to Other 7)

        // The game's ContactCategoryName, by number, as its keys name them ("contactcategoryname_general").
        private static readonly string[] GameCategories = { "general", "business", "finance", "furnitureandequipment", "importsandgoods", "employees", "rivals" };

        // The conversations the game runs in the thread from an offer's Negotiate button: an offer's talk, not a call.
        private static readonly string[] ThreadTalks = { "CandidateSalaryNegotiationDialog", "HealthInsuranceNegotiationDialog" };

        /// <summary>
        /// Whether a contact stands in the list: the waiting filter shows every contact with unread messages, whatever group is open,
        /// so a sender in another group is never hidden behind the count; otherwise the open group and the search decide.
        /// </summary>
        public static bool InList(bool waitingOnly, bool unread, bool inGroup, bool inSearch) => waitingOnly ? unread && inSearch : inGroup && inSearch;

        /// <summary>The senders as a line: one name, two names joined, or the first with how many more ("Ana Ruiz and 2 more"). Empty for none.</summary>
        public static string Senders(UiWords t, string[] names)
        {
            int n = names == null ? 0 : names.Length;
            if (n == 0)
            {
                return string.Empty;
            }
            if (n == 1)
            {
                return names[0];
            }
            if (n == 2)
            {
                return t("reallife_ui_contacts_from_two", "{a} and {b}").Replace("{a}", names[0]).Replace("{b}", names[1]);
            }
            return t("reallife_ui_contacts_from_more", "{a} and {count} more").Replace("{a}", names[0]).Replace("{count}", (n - 1).ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>The key of a group's name, as the game asks for a category's (ContactCategoryName.GetLocalizeKey).</summary>
        public static string GroupKey(int category) =>
            "contactcategoryname_" + (category >= 0 && category < GameCategories.Length ? GameCategories[category] : category.ToString(CultureInfo.InvariantCulture));

        /// <summary>A group's glyph: the game's categories by what they hold, the mod's groups as the game's own pictures for them show.</summary>
        public static UiIcon GroupIcon(int category)
        {
            switch (category)
            {
                case 0: return UiIcon.Person;       // General
                case 1: return UiIcon.Briefcase;    // Business
                case 2: return UiIcon.Bank;         // Finance
                case 3: return UiIcon.Sofa;         // Furniture and equipment
                case 4: return UiIcon.Package;      // Imports and goods
                case 5: return UiIcon.IdBadge;      // Employees
                case 6: return UiIcon.Flag;         // Rivals
                case ModGroups: return UiIcon.Heart;            // Family
                case ModGroups + 1: return UiIcon.People;       // Friends
                case ModGroups + 2: return UiIcon.Cap;          // Work and careers
                case ModGroups + 3: return UiIcon.Wrench;       // Services and trades
                case ModGroups + 4: return UiIcon.Coin;         // Money and banks
                case ModGroups + 5: return UiIcon.Building;     // The city
                default: return UiIcon.Info;                    // Other, and a group another mod adds
            }
        }

        /// <summary>A firm's glyph from the game's kind of business ("ba:businesstype_recruitmentagency"), else its group's.</summary>
        public static UiIcon FirmIcon(string businessType, int category)
        {
            string t = businessType ?? string.Empty;
            if (Has(t, "recruitment") || Has(t, "headhunter"))
            {
                return UiIcon.IdBadge;
            }
            if (Has(t, "marketing") || Has(t, "advert"))
            {
                return UiIcon.Megaphone;
            }
            if (Has(t, "bank"))
            {
                return UiIcon.Bank;
            }
            if (Has(t, "furniture") || Has(t, "interior"))
            {
                return UiIcon.Sofa;
            }
            if (Has(t, "wholesale") || Has(t, "import") || Has(t, "warehouse"))
            {
                return UiIcon.Package;
            }
            if (Has(t, "coffee") || Has(t, "cafe"))
            {
                return UiIcon.Coffee;
            }
            if (Has(t, "restaurant") || Has(t, "burger") || Has(t, "pizza") || Has(t, "bakery") || Has(t, "food") || Has(t, "bar"))
            {
                return UiIcon.ForkKnife;
            }
            if (Has(t, "car") || Has(t, "vehicle") || Has(t, "garage") || Has(t, "gasstation"))
            {
                return UiIcon.Car;
            }
            if (Has(t, "moving") || Has(t, "logistic") || Has(t, "tow") || Has(t, "delivery"))
            {
                return UiIcon.Truck;
            }
            if (Has(t, "hospital") || Has(t, "health") || Has(t, "pharmacy"))
            {
                return UiIcon.Heartbeat;
            }
            if (Has(t, "law"))
            {
                return UiIcon.Scales;
            }
            if (Has(t, "store") || Has(t, "shop") || Has(t, "market") || Has(t, "boutique"))
            {
                return UiIcon.Store;
            }
            return category == 0 ? UiIcon.Building : GroupIcon(category);   // a firm in General is a place, never a person's mark
        }

        private static bool Has(string text, string part) => text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>True for a conversation the game runs in the thread (an offer's negotiation), false for a call, by the dialog's class name.</summary>
        public static bool InThread(string dialogType) => dialogType != null && Array.IndexOf(ThreadTalks, dialogType) >= 0;

        /// <summary>
        /// The game's buttons under a line, in the order the game puts them (cancel, the second option, confirm), as the
        /// kit's numbered choices: the confirm first, as the line's main answer, then the second option; the cancel last in
        /// the thread, or on its own as the call's way out. Writes the game's index of each choice; returns how many.
        /// </summary>
        public static int ChoiceOrder(bool cancel, bool second, bool confirm, bool cancelAsChoice, int[] into)
        {
            int n = 0, at = 0;
            int cancelAt = cancel ? at++ : -1, secondAt = second ? at++ : -1, confirmAt = confirm ? at : -1;
            if (confirmAt >= 0)
            {
                into[n++] = confirmAt;
            }
            if (secondAt >= 0)
            {
                into[n++] = secondAt;
            }
            if (cancelAt >= 0 && cancelAsChoice)
            {
                into[n++] = cancelAt;
            }
            return n;
        }

        /// <summary>A call's length as a phone shows it: minutes and seconds ("1:05"), hours first past an hour.</summary>
        public static string Length(float seconds)
        {
            int s = Math.Max(0, (int)seconds);
            return s >= 3600
                ? (s / 3600).ToString(CultureInfo.InvariantCulture) + ":" + (s / 60 % 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (s % 60).ToString("00", CultureInfo.InvariantCulture)
                : (s / 60).ToString(CultureInfo.InvariantCulture) + ":" + (s % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>The ring of a face for the mood the game shows by a line: content green, unsure amber, angry red, none unknown.</summary>
        public static UiRole MoodRing(int mood) => mood > 0 ? UiRole.Ok : mood == 0 ? UiRole.Warn : mood < -1 ? UiRole.None : UiRole.Danger;

        /// <summary>The game's rich text as plain words: a line break kept as one, every other tag dropped, the space it leaves closed up.</summary>
        public static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
            {
                return text;
            }
            var sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                char ch = text[i];
                if (ch != '<')
                {
                    sb.Append(ch);
                    i++;
                    continue;
                }
                int end = text.IndexOf('>', i);
                if (end < 0)
                {
                    sb.Append(text, i, text.Length - i);
                    break;
                }
                if (string.Compare(text, i, "<br", 0, 3, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    sb.Append('\n');
                }
                i = end + 1;
            }
            return sb.ToString().Trim();
        }

        /// <summary>A label the game writes with its colon ("Hourly wage: "), as a field's label without it.</summary>
        public static string Label(string text)
        {
            string t = Plain(text ?? string.Empty).Trim();
            while (t.Length > 0 && (t[t.Length - 1] == ':' || t[t.Length - 1] == '：' || char.IsWhiteSpace(t[t.Length - 1])))
            {
                t = t.Substring(0, t.Length - 1);
            }
            return t;
        }
    }
}
