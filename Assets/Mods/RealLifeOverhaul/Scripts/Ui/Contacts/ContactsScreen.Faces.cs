using System.Collections.Generic;
using Entities;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The faces of the Contacts screen, by the one rule for a person's picture (UiFaces): a person's contact shows the
    /// picture the game has for them, never a painted face. The mod's own people are told by ContactPictures (the mod's
    /// portrait cache); for the rest it is the sprite the game's own list holds for the contact, or, for an employee or a
    /// candidate, the game's portrait camera drawing them from their own data. A contact is looked at once a screen.
    /// </summary>
    public sealed partial class ContactsScreen
    {
        private readonly HashSet<string> _faced = new HashSet<string>(System.StringComparer.Ordinal);   // contacts told to UiFaces, or found to have no game picture

        // Told while the screen reads the game's contacts; a person already told costs one set read.
        private void Face(Contact c)
        {
            if (c == null || string.IsNullOrEmpty(c.id) || !_faced.Add(c.id) || !Person(c))
            {
                return;
            }
            string name = Name(c);
            if (c.IsEmployeeContact)
            {
                FaceSources.Employee(name, EmployeeOf(c.id, out bool _));
                return;
            }
            ContactFaces.Tell(name, c.id);
        }

        // A firm's contact (a bank, a shop, a rival's business, an agency): the game's own picture for it, as the game's
        // Contacts shows it (FirmPictures); null for a person or a firm the game has none for, which keeps its icon's tile.
        private static string Logo(Contact c) => c == null || Person(c) ? null : FirmPictures.Of(c);
    }

    /// <summary>
    /// The game's own picture of a firm's contact, the one its Contacts app shows by the name (ContactsApp.GetContactIcon:
    /// the contact's own icon where the game has one, else the business's square logo, its own or an AI business's), as a
    /// picture key the kit draws (UiPictures, "contact:" and the sprite's id). The game's and the mod's businesses and every
    /// rival's are asked the same way. Each contact is asked once a city, a firm with no picture remembered as none, so
    /// the game's own "no logo" line is written once; nothing is asked again while the screen reads the list.
    /// </summary>
    internal static class FirmPictures
    {
        public const string Prefix = "contact:";

        private static readonly Dictionary<string, string> Keys = new Dictionary<string, string>(System.StringComparer.Ordinal);   // a contact's id: its picture's key, or null for none
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>(System.StringComparer.Ordinal); // a key: the game's sprite
        private static object _city;
        private static bool _on;

        /// <summary>The picture key of a firm's contact, or null when the game has no picture for it.</summary>
        public static string Of(Contact c)
        {
            if (c == null || string.IsNullOrEmpty(c.id))
            {
                return null;
            }
            On();
            object city = SaveGameManager.Current;
            if (!ReferenceEquals(city, _city))
            {
                _city = city;   // a new city: its businesses and their logos are asked again
                Keys.Clear();
                Sprites.Clear();
            }
            if (Keys.TryGetValue(c.id, out string known))
            {
                if (known == null || Sprites.TryGetValue(known, out Sprite held) && held != null)
                {
                    return known;
                }
                Keys.Remove(c.id);   // the game let its sprite go: asked again
            }
            Sprite sprite = null;
            try
            {
                sprite = ContactsApp.GetContactIcon(c);
            }
            catch (System.Exception e) when (Guard.Missing(e))
            {
                sprite = null;   // this version has no such picture: the icon's tile stays
            }
            string key = sprite != null ? Prefix + sprite.GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
            Keys[c.id] = key;
            if (key != null)
            {
                Sprites[key] = sprite;
            }
            return key;
        }

        // The keys this file gives its pictures, answered here; any other key goes to whoever asked before.
        private static void On()
        {
            if (_on)
            {
                return;
            }
            _on = true;
            System.Func<string, Sprite> before = UiPictures.Find;
            UiPictures.Find = key => key != null && key.StartsWith(Prefix, System.StringComparison.Ordinal)
                ? Sprites.TryGetValue(key, out Sprite sprite) ? sprite : null
                : before?.Invoke(key);
        }
    }
}
