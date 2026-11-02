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
            if (!UiFaces.Has(name))
            {
                FaceSources.Held(name, FaceSources.Icon(c.id));   // the sprite the game's own contacts list holds for them
            }
        }
    }
}
