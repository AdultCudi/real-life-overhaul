using System;

namespace RealLifeOverhaul
{
    /// <summary>What a contact's picture is: a person's face, or an organisation's logo.</summary>
    public enum PictureKind
    {
        Logo = 0,       // one of the game's own logo shapes (StreamingAssets/LogoShapes) on a square of colour, as a business logo is made
        Person = 1      // a face drawn by the game's own portrait camera (Character.Customization.PortraitGenerator), as the Rivals app draws a rival
    }

    /// <summary>One contact's picture as the catalogue gives it.</summary>
    public struct ContactLook
    {
        public string Id;
        public PictureKind Kind;
        public string Shape;        // a logo: the game's logo shape, by its file name
        public byte R, G, B;        // a logo: the colour of its square
        public bool Female;         // a person
        public int Age;             // a person: years
        public string LikeGame;     // a logo: one of the game's own contact pictures to use instead when the game has it; null for none

        public static ContactLook Logo(string id, string shape, byte r, byte g, byte b, string likeGame = null)
        {
            return new ContactLook { Id = id, Kind = PictureKind.Logo, Shape = shape, R = r, G = g, B = b, LikeGame = likeGame };
        }

        public static ContactLook Person(string id, bool female, int age)
        {
            return new ContactLook { Id = id, Kind = PictureKind.Person, Female = female, Age = age };
        }
    }

    /// <summary>
    /// Every phone contact the mod adds, and its picture. Nothing is drawn by the mod itself: an organisation's picture is
    /// one of the game's own logo shapes on a square of colour, the way the game makes a business's logo; a person's is
    /// their face from the game's own portrait camera. People whose look the mod already gives (the people of the social
    /// life, a partner, a roommate) are drawn from that same look by the code that knows them (ContactPictures.Person);
    /// the ages and sexes here are for the contacts no other feature describes. Game-free: CatalogueCheck holds every
    /// contact id the mod registers against this table, and every shape against the game's list.
    /// </summary>
    public static class ContactCatalogue
    {
        /// <summary>The game's logo shapes this table uses: each must be a file of the game's StreamingAssets/LogoShapes.</summary>
        public static readonly string[] Shapes =
        {
            "lawfirm1", "lawfirm2", "lawfirm3", "headquarter1", "headquarter3", "bank2", "marketing2", "marketing3", "furniture1", "furniture3", "gym3",
            "recruitment1", "recruitment2", "recruitment3", "school1", "bookstore1", "wholesale3", "cardealer2", "calendar2", "planeticket", "plane"
        };

        public static readonly ContactLook[] All =
        {
            // The emergency line, the doctor, an old friend and Mom (Phone/PhoneContacts; Mom and Alex are the social life's when it is on)
            ContactLook.Logo("reallife-911", "lawfirm2", 186, 42, 42),
            ContactLook.Person("reallife-doctor", true, 46),
            ContactLook.Person("reallife-friend", false, 30),
            ContactLook.Person("reallife-mom", true, 58),
            // Money (Economy/Debt): the collections agency is a firm; the private lender and the adviser are people
            ContactLook.Logo("reallife-collections", "bank2", 128, 40, 52),
            ContactLook.Person("reallife-lender", false, 44),
            ContactLook.Person("reallife-debt-advice", true, 38),
            // The city
            ContactLook.Logo("reallife-cityhall", "headquarter3", 38, 74, 132),
            ContactLook.Logo("reallife-bulletin", "marketing3", 222, 112, 40),
            ContactLook.Logo("reallife-ledger", "marketing2", 58, 62, 72),
            ContactLook.Logo("reallife-records", "lawfirm3", 30, 52, 112),
            ContactLook.Logo("reallife-legal", "lawfirm1", 112, 38, 50),
            ContactLook.Logo("reallife-dmv", "school1", 28, 124, 136),
            // Home and family (a partner and a roommate are drawn from their own look once they are known)
            ContactLook.Logo("reallife-family", "furniture1", 204, 82, 122),
            ContactLook.Person("reallife-partner", true, 30),
            ContactLook.Person("reallife-inlaws", true, 62),
            ContactLook.Logo("reallife-roommate", "furniture3", 202, 132, 62),
            ContactLook.Logo("reallife-health", "gym3", 40, 140, 112),
            ContactLook.Logo("reallife-people", "recruitment3", 220, 104, 92),
            // Work
            ContactLook.Logo("reallife-business", "headquarter1", 44, 64, 118),
            ContactLook.Logo("reallife-career", "recruitment2", 104, 72, 160),
            ContactLook.Logo("reallife-jobs", "recruitment1", 72, 92, 172),
            // Getting around
            ContactLook.Logo("reallife-garage", "wholesale3", 80, 90, 102),
            ContactLook.Logo("reallife-roadside", "wholesale3", 216, 128, 30, "auto_tow_service_ny"),
            ContactLook.Logo("reallife-transit", "calendar2", 30, 90, 160),
            ContactLook.Logo("reallife-taxi", "cardealer2", 226, 176, 30),
            ContactLook.Logo("reallife-trips", "planeticket", 40, 112, 190),
            ContactLook.Logo("reallife-travel", "plane", 128, 44, 38),
            // The mod's own guide
            ContactLook.Logo("reallife-guide", "bookstore1", 188, 148, 40)
        };

        public static bool TryGet(string id, out ContactLook look)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i].Id, id, StringComparison.Ordinal))
                {
                    look = All[i];
                    return true;
                }
            }
            look = default(ContactLook);
            return false;
        }

        /// <summary>
        /// A person's look seed from their contact id and the save's own name for the character, so that Mom has one face
        /// in one playthrough and another in the next, and the same face on every load. FNV-1a over both, never negative;
        /// string.GetHashCode is not used, as it may differ between runtimes.
        /// </summary>
        public static int Seed(string id, string save)
        {
            unchecked
            {
                uint hash = 2166136261u;
                Mix(ref hash, id);
                hash = (hash ^ '|') * 16777619u;
                Mix(ref hash, save);
                return (int)(hash & 0x7FFFFFFF);
            }
        }

        private static void Mix(ref uint hash, string text)
        {
            unchecked
            {
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    hash = (hash ^ text[i]) * 16777619u;
                }
            }
        }

        /// <summary>A drawn face's file name in the portrait cache: what it is drawn from, so a different look is a different file. The first part is the version of how faces are drawn.</summary>
        public static string CacheName(bool female, int age, int seed)
        {
            return "v1_" + (female ? "f" : "m") + Math.Max(0, age) + "_" + Math.Max(0, seed) + ".jpg";
        }

        /// <summary>The words a contact list row is found by: whether every word of the query is in the name or the description, ignoring case and accents.</summary>
        public static bool Matches(string query, string name, string description, Func<string, string, bool> contains)
        {
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }
            int start = 0;
            while (start < query.Length)
            {
                while (start < query.Length && query[start] == ' ')
                {
                    start++;
                }
                int end = start;
                while (end < query.Length && query[end] != ' ')
                {
                    end++;
                }
                if (end > start)
                {
                    string word = query.Substring(start, end - start);
                    if (!contains(name ?? string.Empty, word) && !contains(description ?? string.Empty, word))
                    {
                        return false;
                    }
                }
                start = end;
            }
            return true;
        }
    }
}
