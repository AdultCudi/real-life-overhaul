using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Where a face comes from when a person has a contact on the phone.</summary>
    public enum ContactFaceFrom
    {
        Painted = 0,   // no picture at all: the kit paints a face from the sex and age on record
        Mod = 1,       // the mod's rendered photo of the person (ContactPictures)
        Game = 2       // the sprite the game's own contacts list holds
    }

    /// <summary>The choices behind <see cref="ContactFaces"/>. Game-free.</summary>
    public static class ContactFaceRules
    {
        /// <summary>A contact's picture wins over the painted face wherever there is one; the mod's own photo is the one Contacts shows for its people.</summary>
        public static ContactFaceFrom Pick(bool modPicture, bool gameIcon)
        {
            return modPicture ? ContactFaceFrom.Mod : gameIcon ? ContactFaceFrom.Game : ContactFaceFrom.Painted;
        }

        /// <summary>
        /// True the first time a name is shown for a contact (or for another contact than before), and records it: the person is told once, not on every
        /// read of a screen. A dictionary read when nothing is new.
        /// </summary>
        public static bool Fresh(Dictionary<string, string> told, string name, string id)
        {
            if (told == null || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(id))
            {
                return false;
            }
            if (told.TryGetValue(name, out string was) && was == id)
            {
                return false;
            }
            told[name] = id;
            return true;
        }
    }
}