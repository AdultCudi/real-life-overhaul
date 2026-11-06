using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Unity.Collections;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The pictures of the mod's phone contacts, from the game's own assets (<see cref="ContactCatalogue"/>). The Contacts
    /// app finds a picture by the contact's id in GlobalReferences.contactIcons and copies that list once into a static
    /// dictionary, so each contact has one sprite for the session in both places, painted later by
    /// <see cref="ContactPainter"/>. Cost: adding a contact looks through a few dozen sprites and fills one texture.
    /// </summary>
    public static class ContactPictures
    {
        public const int Size = 256;                    // pixels, as the game's own portraits
        private static readonly Color32 Waiting = new Color32(86, 96, 108, 255);   // a face's square until it is drawn: the game's panel grey

        public static IModLogger Logger;

        internal sealed class Picture
        {
            public string Id;
            public ContactLook Look;
            public int Seed;            // a person's look
            public Texture2D Texture;
            public Sprite Sprite;
            public bool Drawn;          // painted with the look it has now
            public bool Queued;
            public int Tries;
            public string Described;    // the save in which a feature gave this person their own look; null for the catalogue's
        }

        private static readonly Dictionary<string, Picture> Pictures = new Dictionary<string, Picture>(48, StringComparer.Ordinal);
        private static FieldInfo _predefined;   // ContactsApp.PredefinedContactIconSprites, private static
        private static bool _predefinedLooked;
        private static readonly HashSet<string> Unknown = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>A contact of the catalogue: its logo, or a face from the catalogue's sex and age and a seed of this save's.</summary>
        public static void Add(string id)
        {
            try
            {
                ContactLook look;
                if (!ContactCatalogue.TryGet(id, out look))
                {
                    if (Unknown.Add(id))
                    {
                        Logger?.Warn("Contacts: '" + id + "' is not in the contact catalogue (Phone/Contacts/ContactCatalogue.cs): it is given the game's own picture for an unknown logo.");
                    }
                    look = ContactLook.Logo(id, ContactGroup.None, "notfound", 86, 96, 108);
                }
                Picture p;
                string save = SaveKey();
                if (Pictures.TryGetValue(id, out p) && p.Described != null && p.Described == save)
                {
                    Ensure(p);   // a person a feature described in this save keeps their own look
                    return;
                }
                Set(id, look, look.Kind != PictureKind.Person ? 0 : look.Seed != 0 ? look.Seed : ContactCatalogue.Seed(id, save), null);
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // a contact without its picture still works: the game shows its empty square
            }
        }

        /// <summary>A person whose look another feature gives (the sex, age and seed it builds them from): their own face.</summary>
        public static void Person(string id, bool female, int age, int seed)
        {
            try
            {
                ContactLook look = ContactLook.Person(id, ContactGroup.None, female, Mathf.Clamp(age, 18, 85));
                string save = SaveKey();
                Set(id, look, seed == 0 ? ContactCatalogue.Seed(id, save) : seed, save);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>The face a contact's picture is (or will be) drawn from: false for a logo or a contact with none. For the people tests.</summary>
        public static bool TryLook(string id, out bool female, out int age, out int seed)
        {
            Picture p;
            if (Pictures.TryGetValue(id, out p) && p.Look.Kind == PictureKind.Person)
            {
                female = p.Look.Female;
                age = p.Look.Age;
                seed = p.Seed;
                return true;
            }
            female = false;
            age = 0;
            seed = 0;
            return false;
        }

        // The save's own name for the character: a seed of its own for every playthrough.
        private static string SaveKey()
        {
            GameInstance save = SaveGameManager.Current;
            return save != null ? save.characterId ?? string.Empty : string.Empty;
        }

        private static void Set(string id, ContactLook look, int seed, string described)
        {
            Picture p;
            if (!Pictures.TryGetValue(id, out p))
            {
                p = new Picture { Id = id };
                p.Texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "RealLife contact " + id, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
                p.Sprite = Sprite.Create(p.Texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
                p.Sprite.name = id;
                p.Sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
                Pictures.Add(id, p);
                Fill(p.Texture, look.Kind == PictureKind.Logo ? new Color32(look.R, look.G, look.B, 255) : Waiting);
            }
            bool same = p.Look.Kind == look.Kind && p.Seed == seed && p.Look.Female == look.Female && p.Look.Age == look.Age && p.Look.Shape == look.Shape;
            p.Look = look;
            p.Seed = seed;
            p.Described = described;
            if (!same)
            {
                p.Drawn = false;
                p.Tries = 0;
            }
            Ensure(p);
            if (!p.Drawn && !p.Queued)
            {
                p.Queued = true;
                ContactPainter.Want(p);
            }
        }

        // Where the game looks: GlobalReferences' list, and the Contacts app's dictionary once that is filled.
        private static void Ensure(Picture p)
        {
            var references = InstanceBehavior<GlobalReferences>.Instance;
            if (references != null)
            {
                Sprite[] icons = references.contactIcons ?? new Sprite[0];
                int at = -1;
                for (int i = 0; i < icons.Length; i++)
                {
                    if (icons[i] != null && icons[i].name == p.Id)
                    {
                        at = i;
                        break;
                    }
                }
                if (at < 0)
                {
                    var grown = new Sprite[icons.Length + 1];
                    icons.CopyTo(grown, 0);
                    grown[icons.Length] = p.Sprite;
                    references.contactIcons = grown;
                }
                else if (icons[at] != p.Sprite)
                {
                    icons[at] = p.Sprite;   // an older sprite under this id gives way to this picture
                }
            }
            Dictionary<string, Sprite> cached = Predefined();
            if (cached != null && cached.Count > 0)
            {
                cached[p.Id] = p.Sprite;   // an empty dictionary is filled from the list the first time the app looks
            }
        }

        private static Dictionary<string, Sprite> Predefined()
        {
            if (!_predefinedLooked)
            {
                _predefinedLooked = true;
                _predefined = GameContract.ContactIcons.Field;
                if (_predefined == null || _predefined.FieldType != typeof(Dictionary<string, Sprite>))
                {
                    _predefined = null;
                    Logger?.Warn("Contacts: the Contacts app keeps its pictures in another way in this version of the game; "
                                 + "a contact added after the app was first opened shows its picture from the next load.");
                }
            }
            return _predefined != null ? _predefined.GetValue(null) as Dictionary<string, Sprite> : null;
        }

        // Written into the texture's own memory, then sent to the graphics card.
        internal static void Fill(Texture2D texture, Color32 colour)
        {
            NativeArray<Color32> pixels = texture.GetPixelData<Color32>(0);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = colour;
            }
            texture.Apply(false, false);
        }

        /// <summary>Test: one line per contact picture in Player.log, and a notice.</summary>
        public static void TestReport()
        {
            try
            {
                var text = new System.Text.StringBuilder(64 * (Pictures.Count + 1));
                text.Append("Contacts check (pictures): ").Append(Report()).Append('.');
                foreach (Picture p in Pictures.Values)
                {
                    text.Append("\n  ").Append(p.Id).Append(": ").Append(p.Look.Kind == PictureKind.Person
                            ? (p.Look.Female ? "a woman of " : "a man of ") + p.Look.Age + ", seed " + p.Seed + (p.Described != null ? " (their own look)" : " (the catalogue's)")
                            : "logo '" + p.Look.Shape + "'" + (p.Look.LikeGame != null ? " or the game's '" + p.Look.LikeGame + "'" : string.Empty))
                        .Append(p.Drawn ? ", drawn" : p.Queued ? ", waiting to be drawn" : ", not drawn (" + p.Tries + " tries)")
                        .Append(InList(p) ? string.Empty : ", NOT in the game's list");
                }
                Logger?.Info(text.ToString());
                Notice("reallife_contacts_test_said", Report());
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Test: every face is drawn again, its cached file deleted first.</summary>
        public static void TestRedraw()
        {
            try
            {
                int faces = 0;
                foreach (Picture p in Pictures.Values)
                {
                    if (p.Look.Kind != PictureKind.Person)
                    {
                        continue;
                    }
                    ContactPainter.Forget(p);
                    p.Drawn = false;
                    p.Tries = 0;
                    faces++;
                    if (!p.Queued)
                    {
                        p.Queued = true;
                        ContactPainter.Want(p);
                    }
                }
                Notice("reallife_contacts_test_redrawing", faces.ToString());
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static bool InList(Picture p)
        {
            var references = InstanceBehavior<GlobalReferences>.Instance;
            for (int i = 0; references != null && references.contactIcons != null && i < references.contactIcons.Length; i++)
            {
                if (references.contactIcons[i] == p.Sprite)
                {
                    return true;
                }
            }
            return false;
        }

        private static void Notice(string key, string what)
        {
            UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, key, new Dictionary<string, string> { { "what", what } }, 8f, null, null, true, false);
        }

        /// <summary>One line for the diagnostics: how many pictures there are and how many are drawn.</summary>
        public static string Report()
        {
            int faces = 0, logos = 0, drawn = 0;
            foreach (Picture p in Pictures.Values)
            {
                faces += p.Look.Kind == PictureKind.Person ? 1 : 0;
                logos += p.Look.Kind == PictureKind.Logo ? 1 : 0;
                drawn += p.Drawn ? 1 : 0;
            }
            return Pictures.Count + " contact picture(s): " + faces + " face(s), " + logos + " logo(s), " + drawn + " drawn";
        }
    }
}
