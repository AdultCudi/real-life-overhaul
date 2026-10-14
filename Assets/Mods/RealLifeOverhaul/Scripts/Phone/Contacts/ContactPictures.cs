using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using UI.Smartphone.Apps.Contacts;
using Unity.Collections;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The pictures of the mod's phone contacts, from the game's own assets: a person's face from the game's portrait
    /// camera, an organisation's logo from the game's own logo shapes (ContactCatalogue says which is which). Every
    /// feature that adds a contact asks here (<see cref="Add"/>, or <see cref="Person"/> for someone whose look it knows),
    /// and nothing else of the mod draws a contact picture.
    ///
    /// HOW THE GAME FINDS A PICTURE. The Contacts app looks a contact's picture up by the contact's id among the sprites of
    /// GlobalReferences.contactIcons (ContactsApp.GetPredefinedContactIcon), and copies that list into a dictionary of its
    /// own the first time it looks, never again (ContactsApp.PredefinedContactIconSprites, static for the whole run). So
    /// each contact has ONE sprite for the whole session, named by its id, put in both places when the contact is added:
    /// the list for a Contacts app that has not looked yet, the dictionary (one field, read once) for one that has. The
    /// picture is then painted into that same sprite's texture when it is ready, so the app shows it without being told.
    ///
    /// COST. Adding a contact: a look through the list (a few dozen sprites) and one texture of 256 x 256, filled with its
    /// colour in the texture's own memory (it used to be a new array of 256 KB for each: eight contacts set up in one frame
    /// made exactly 2 MB of garbage on a heap of 800 MB, the owner's log of 14 Oct 2026). The painting is
    /// done by <see cref="ContactPainter"/>, one picture at a time, spread over seconds; nothing runs while there is
    /// nothing to paint. A face is drawn once per look ever: it is kept as a JPEG in the portrait cache and read from there
    /// on every later load.
    /// </summary>
    public static class ContactPictures
    {
        public const int Size = 256;                    // pixels: the game's own portraits are 256 x 256 (PortraitGenerator.widthPixels)
        private static readonly Color32 Waiting = new Color32(86, 96, 108, 255);   // a face's square until it is drawn: the game's dark grey of a panel

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
        private static FieldInfo _predefined;   // ContactsApp.PredefinedContactIconSprites (private static)
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
                    look = ContactLook.Logo(id, "notfound", 86, 96, 108);
                }
                Picture p;
                string save = SaveKey();
                if (Pictures.TryGetValue(id, out p) && p.Described != null && p.Described == save)
                {
                    Ensure(p);   // a person a feature has described in this save (Mom and Alex of the social life): their own look stands
                    return;
                }
                Set(id, look, look.Kind == PictureKind.Person ? ContactCatalogue.Seed(id, save) : 0, null);
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // (a contact without its picture still works: the game shows its own empty square)
            }
        }

        /// <summary>A person whose look another feature gives (the same sex, age and seed it builds them in the city from): their own face.</summary>
        public static void Person(string id, bool female, int age, int seed)
        {
            try
            {
                ContactLook look = ContactLook.Person(id, female, Mathf.Clamp(age, 18, 85));
                string save = SaveKey();
                Set(id, look, seed == 0 ? ContactCatalogue.Seed(id, save) : seed, save);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
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

        // The sprite is where the game looks for it: in GlobalReferences' list, and in the Contacts app's own dictionary once that has been filled.
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
                    icons[at] = p.Sprite;   // a sprite of an earlier version's (a coloured disc): this picture takes its place
                }
            }
            Dictionary<string, Sprite> cached = Predefined();
            if (cached != null && cached.Count > 0)
            {
                cached[p.Id] = p.Sprite;   // (an empty dictionary is filled from the list above the first time the app looks)
            }
        }

        private static Dictionary<string, Sprite> Predefined()
        {
            if (!_predefinedLooked)
            {
                _predefinedLooked = true;
                _predefined = GameContract.ContactIcons.Field;   // (resolved once: Core/GameContract)
                if (_predefined == null || _predefined.FieldType != typeof(Dictionary<string, Sprite>))
                {
                    _predefined = null;
                    Logger?.Warn("Contacts: the Contacts app keeps its pictures in another way in this version of the game; a contact added after the app was first opened shows its picture from the next load.");
                }
            }
            return _predefined != null ? _predefined.GetValue(null) as Dictionary<string, Sprite> : null;
        }

        // The square in one colour, written into the texture's own memory (no array of our own), then sent to the graphics card.
        internal static void Fill(Texture2D texture, Color32 colour)
        {
            NativeArray<Color32> pixels = texture.GetPixelData<Color32>(0);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = colour;
            }
            texture.Apply(false, false);
        }

        /// <summary>Test: one line per contact picture in Player.log (what it is, what it is drawn from, whether it is drawn), and a notice.</summary>
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

        /// <summary>Test: every face is drawn again by the game's portrait camera (the portrait cache's files of them are deleted first).</summary>
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
