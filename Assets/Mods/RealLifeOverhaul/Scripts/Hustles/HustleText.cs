using System.Collections.Generic;
using System.Globalization;
using Extensions;
using Localizor;
using UI.Notification;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>The Marketplace's words: locale keys with their blanks filled, money as the game writes it, and its notices.</summary>
    public static class HustleText
    {
        public static string T(string key)
        {
            return key.GetLocalization();
        }

        public static string T(string key, string name, string value)
        {
            return key.GetLocalization().Replace("{" + name + "}", value ?? string.Empty);
        }

        public static string T(string key, string name, string value, string name2, string value2)
        {
            return T(key, name, value).Replace("{" + name2 + "}", value2 ?? string.Empty);
        }

        public static string T(string key, string name, string value, string name2, string value2, string name3, string value3)
        {
            return T(key, name, value, name2, value2).Replace("{" + name3 + "}", value3 ?? string.Empty);
        }

        public static string T(string key, string name, string value, string name2, string value2, string name3, string value3, string name4, string value4)
        {
            return T(key, name, value, name2, value2, name3, value3).Replace("{" + name4 + "}", value4 ?? string.Empty);
        }

        public static string Money(int amount)
        {
            return ((float)amount).ToCurrencyFormat();
        }

        /// <summary>A thing's name: the catalogue's word for goods, the game's own for a car.</summary>
        public static string Thing(string thing, bool car)
        {
            if (car)
            {
                return HustleGame.CarName(thing);
            }
            ThingDef def = HustleCatalogue.Thing(thing);
            return def != null ? def.NameKey.GetLocalization() : thing ?? string.Empty;
        }

        public static string Kind(string thing, bool car)
        {
            ThingDef def = car ? null : HustleCatalogue.Thing(thing);
            return T(HustleCatalogue.KindKey(car ? ThingKind.Car : def != null ? def.Kind : ThingKind.Electronics));
        }

        public static string Condition(int percent)
        {
            string key = percent >= 85 ? "reallife_hustle_condition_great" : percent >= 65 ? "reallife_hustle_condition_good" : percent >= 40 ? "reallife_hustle_condition_fair"
                : percent >= 20 ? "reallife_hustle_condition_worn" : "reallife_hustle_condition_broken";
            return T(key, "percent", percent.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>A rating in thousandths of a star as "4.6".</summary>
        public static string Stars(int thousandths)
        {
            return (thousandths / 1000f).ToString("0.0", CultureInfo.CurrentCulture);
        }

        public static string Place(string street, int number)
        {
            return HustleGame.Name(street, number);
        }

        public static string Gig(GigKind kind)
        {
            return T(HustleCatalogue.Gig(kind).NameKey);
        }

        /// <summary>An hour stamp (day x 24 + hour) as the game writes a time of day.</summary>
        public static string Clock(int stamp)
        {
            return (((stamp % 24) + 24) % 24).GetFormattedTime(0);
        }

        public static void Note(NotificationType type, string key, float seconds = 9f)
        {
            Notifications.Show(type, key, null, seconds);
        }

        public static void Note(NotificationType type, string key, string name, string value, float seconds = 9f)
        {
            // A new dictionary each time: the game keeps hold of it for as long as the notice is listed.
            Notifications.Show(type, key, new Dictionary<string, string>(1) { { name, value ?? string.Empty } }, seconds);
        }

        public static void Note(NotificationType type, string key, string name, string value, string name2, string value2, float seconds = 9f)
        {
            Notifications.Show(type, key, new Dictionary<string, string>(2) { { name, value ?? string.Empty }, { name2, value2 ?? string.Empty } }, seconds);
        }

        public static void Note(NotificationType type, string key, string name, string value, string name2, string value2, string name3, string value3, float seconds = 9f)
        {
            Notifications.Show(type, key, new Dictionary<string, string>(3) { { name, value ?? string.Empty }, { name2, value2 ?? string.Empty }, { name3, value3 ?? string.Empty } },
                seconds);
        }
    }
}
