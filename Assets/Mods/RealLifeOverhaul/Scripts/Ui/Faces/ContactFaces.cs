using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The one lookup that tells <see cref="UiFaces"/> a person's contact picture under the name a screen shows them by: the mod's rendered
    /// photo (ContactPictures) for the mod's own people, else the sprite the game's contacts list holds. Contacts and Flare both ask here,
    /// so a person wears the same picture in both; only a person with no picture at all keeps the painted face. A person already told
    /// under the same name costs one dictionary read; nothing is allocated, searched or drawn after the first time.
    /// </summary>
    internal static class ContactFaces
    {
        private static readonly Dictionary<string, string> Told = new Dictionary<string, string>(64, System.StringComparer.Ordinal);   // a shown name: the contact id it was told for
        private static object _save;

        /// <summary>The contact <paramref name="id"/>'s picture is the face of whoever a screen shows as <paramref name="name"/>.</summary>
        public static void Tell(string name, string id)
        {
            object save = SaveGameManager.Current;
            if (!ReferenceEquals(save, _save))
            {
                _save = save;   // another city: UiFaces forgot everybody, so they are told again
                Told.Clear();
            }
            if (!ContactFaceRules.Fresh(Told, name, id))
            {
                return;
            }
            UiFaceSource face = ContactPictures.FaceOf(id);
            Sprite icon = face == null && !UiFaces.Has(name) ? FaceSources.Icon(id) : null;
            switch (ContactFaceRules.Pick(face != null, icon != null))
            {
                case ContactFaceFrom.Mod:
                    UiFaces.Source(name, face);
                    break;
                case ContactFaceFrom.Game:
                    FaceSources.Held(name, icon);
                    break;
            }
        }
    }
}