using System;

namespace RealLifeOverhaul
{
    /// <summary>The groups the mod's contacts stand in, as buttons beside the game's own categories in the Contacts app.</summary>
    public enum ContactGroup
    {
        None = 0,       // not a group: a contact that is the game's own, or a catalogue entry that forgot its group (a check fails)
        Family = 1,
        Friends = 2,    // friends and the people met
        Work = 3,       // work and careers
        Services = 4,   // services and trades
        Money = 5,      // money and banks
        City = 6,       // the city and its government
        Other = 7
    }

    /// <summary>
    /// The rules for the Contacts app's groups, game-free (CatalogueCheck and EconomyCheck hold them). Every contact the mod adds
    /// is in one group, named in ONE place: its entry in <see cref="ContactCatalogue.All"/> (Group). A contact is found by its id,
    /// and the two kinds with no fixed id by their description: the boss of a job (<see cref="BossDescription"/>) and the people
    /// met (<see cref="PersonDescriptionStart"/>). Each group is a number of the game's ContactCategoryName beyond the seven it
    /// has (<see cref="Base"/> and up), so the game's own buttons, badges, filter and tooltip do the rest; with the option off a
    /// contact has the category the mod gave it before groups, <see cref="Plain"/>. Cost: a pass over the phone's contacts when
    /// one is added or the option changes, never a frame.
    /// </summary>
    public static class ContactGroups
    {
        public const int Base = 100;                            // the first group's category number; the game's are 0 to 6
        public const int Count = 7;                             // groups, Family to Other
        public const int PlainGeneral = 0;                      // ContactCategoryName.General: where the mod's contacts were before groups
        public const int PlainBusiness = 1;                     // ContactCategoryName.Business: where the BizMan desk's contact was
        public const int PlainEmployees = 5;                    // ContactCategoryName.Employees: where the boss of a job was
        public const string OwnIdStart = "reallife-";           // every id the mod makes for a contact
        public const string BossDescription = "reallife_contact_boss_desc";
        public const string PersonDescriptionStart = "reallife_social_desc_";   // under the name of somebody the player met
        public const string NameKeyStart = "contactcategoryname_";              // the game's key for a category's name: its enum number follows
        public const string CountKey = "reallife_contacts_group_count";         // the tooltip's second line on every button: "{count} contact(s)"

        /// <summary>The game's own picture for each group, by name (GameSprites): Family to Other.</summary>
        public static readonly string[] Glyphs =
        {
            "Icon-Heart", "Icon-People", "Icon-Briefcase", "Icon-PersonEngineer", "Icon-Coin", "Icon-Building", "Icon-Info"
        };

        /// <summary>The category number of a group.</summary>
        public static int CategoryOf(ContactGroup group)
        {
            return Base + (int)group;
        }

        /// <summary>The group a category number belongs to, or None for the game's own categories and any number beyond ours.</summary>
        public static ContactGroup GroupOfCategory(int category)
        {
            int n = category - Base;
            return n >= 1 && n <= Count ? (ContactGroup)n : ContactGroup.None;
        }

        /// <summary>The key of a group's name, the one the game asks for a category (ContactCategoryName.GetLocalizeKey).</summary>
        public static string NameKey(ContactGroup group)
        {
            return NameKeyStart + CategoryOf(group).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The group of a contact the mod adds, or None for the game's own. <paramref name="plain"/> is the category it has with the
        /// option off. A "reallife-" id the catalogue lacks is in Other (a check fails first, so no release carries one).
        /// </summary>
        public static ContactGroup GroupOf(string id, string description, out int plain)
        {
            plain = PlainGeneral;
            if (id != null && id.StartsWith(OwnIdStart, StringComparison.Ordinal))
            {
                if (ContactCatalogue.TryGet(id, out ContactLook look) && look.Group != ContactGroup.None)
                {
                    plain = look.Plain;
                    return look.Group;
                }
                return ContactGroup.Other;
            }
            if (string.Equals(description, BossDescription, StringComparison.Ordinal))
            {
                plain = PlainEmployees;
                return ContactGroup.Work;
            }
            if (description != null && description.StartsWith(PersonDescriptionStart, StringComparison.Ordinal))
            {
                return ContactGroup.Friends;
            }
            return ContactGroup.None;
        }

        /// <summary>The category a contact should have: its group's with grouping on, its plain one with it off, and the one it has when it is the game's.</summary>
        public static int CategoryFor(string id, string description, int current, bool grouped)
        {
            ContactGroup group = GroupOf(id, description, out int plain);
            return group == ContactGroup.None ? current : grouped ? CategoryOf(group) : plain;
        }

        /// <summary>Puts every contact in its category (<paramref name="categories"/> is read and written); how many moved.</summary>
        public static int Regroup(string[] ids, string[] descriptions, int[] categories, bool grouped)
        {
            int moved = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                int now = CategoryFor(ids[i], descriptions[i], categories[i], grouped);
                if (now != categories[i])
                {
                    categories[i] = now;
                    moved++;
                }
            }
            return moved;
        }

        /// <summary>How many contacts a category holds.</summary>
        public static int CountIn(int[] categories, int category)
        {
            int n = 0;
            for (int i = 0; i < categories.Length; i++)
            {
                if (categories[i] == category)
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>How wide a row of <paramref name="buttons"/> category buttons asks to be: both paddings, each button's width and the spacing between.</summary>
        public static float RowNeeds(int buttons, float each, float spacing, float padding)
        {
            return buttons <= 0 ? 0f : padding + buttons * each + (buttons - 1) * spacing;
        }

        /// <summary>The scale that makes a row that needs <paramref name="need"/> fit in <paramref name="room"/>: 1 when it fits already, never more.</summary>
        public static float ScaleFor(float need, float room)
        {
            return need <= 0f || room <= 0f || need <= room ? 1f : room / need;
        }
    }
}
