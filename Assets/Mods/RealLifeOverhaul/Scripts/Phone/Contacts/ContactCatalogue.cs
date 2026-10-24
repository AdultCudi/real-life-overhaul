using System;

namespace RealLifeOverhaul
{
    /// <summary>What a contact's picture is: a person's face, or an organisation's logo.</summary>
    public enum PictureKind
    {
        Logo = 0,       // one of the game's logo shapes on a square of colour, as a business logo is made
        Person = 1      // a face from the game's own portrait camera, as the Rivals app draws a rival
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
        public string LikeGame;     // a logo: the game's own contact picture to use instead when the game has it, or null
        public int Seed;            // a person: a look's seed that is the same in every game (Mom, Alex); 0 for each save's own

        public static ContactLook Logo(string id, string shape, byte r, byte g, byte b, string likeGame = null)
        {
            return new ContactLook { Id = id, Kind = PictureKind.Logo, Shape = shape, R = r, G = g, B = b, LikeGame = likeGame };
        }

        public static ContactLook Person(string id, bool female, int age, int seed = 0)
        {
            return new ContactLook { Id = id, Kind = PictureKind.Person, Female = female, Age = age, Seed = seed };
        }
    }

    /// <summary>
    /// The looks of Mom (a woman of 58) and Alex, the old friend (a man of 30), the same in every game and wherever they are
    /// shown: the game builds a look from its seed alone. Game-free.
    /// </summary>
    public static class FixedLooks
    {
        public const int MomSeed = 77171;        // the wedding's mother has always had this look
        public const int MomAge = 58;            // years
        public const bool MomFemale = true;
        public const int AlexSeed = 52817;
        public const int AlexAge = 30;           // years
        public const bool AlexFemale = false;
    }

    /// <summary>
    /// Every phone contact the mod adds, and its picture: a logo on a square of colour, or a face from the game's portrait
    /// camera (people another feature gives a look are drawn from that, ContactPictures.Person). Game-free: CatalogueCheck
    /// holds every registered contact id and every shape against this table.
    /// </summary>
    public static class ContactCatalogue
    {
        /// <summary>The game's logo shapes this table uses: each is a file of the game's StreamingAssets/LogoShapes.</summary>
        public static readonly string[] Shapes =
        {
            "lawfirm1", "lawfirm2", "lawfirm3", "headquarter1", "headquarter3", "bank2", "marketing2", "marketing3", "furniture1", "furniture3", "gym3",
            "recruitment1", "recruitment2", "recruitment3", "school1", "bookstore1", "wholesale3", "cardealer2", "calendar2", "planeticket", "plane", "furniture2"
        };

        public static readonly ContactLook[] All =
        {
            // The emergency line, the doctor, an old friend and Mom (Mom and Alex take the social life's look when it is on)
            ContactLook.Logo("reallife-911", "lawfirm2", 186, 42, 42),
            ContactLook.Person("reallife-doctor", true, 46),
            ContactLook.Person("reallife-friend", FixedLooks.AlexFemale, FixedLooks.AlexAge, FixedLooks.AlexSeed),
            ContactLook.Person("reallife-mom", FixedLooks.MomFemale, FixedLooks.MomAge, FixedLooks.MomSeed),
            // Money: the collections agency is a firm; the private lender and the adviser are people
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
            // Home and family (a partner and a roommate are drawn from their own look once known)
            ContactLook.Logo("reallife-family", "furniture1", 204, 82, 122),
            ContactLook.Person("reallife-partner", true, 30),
            ContactLook.Person("reallife-inlaws", true, 62),
            ContactLook.Logo("reallife-roommate", "furniture3", 202, 132, 62),
            ContactLook.Logo("reallife-household", "furniture2", 64, 112, 150),
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

        /// <summary>A person's look seed from their contact id and the save's character: FNV-1a, never negative, the same on every runtime.</summary>
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

        /// <summary>A drawn face's file name in the portrait cache, from what it is drawn from; "v1" is the way faces are drawn.</summary>
        public static string CacheName(bool female, int age, int seed)
        {
            // The mark keeps a kept look's file apart from a rolled one of the same seed.
            return "v1_" + (female ? "f" : "m") + Math.Max(0, age) + "_" + Math.Max(0, seed) + PersonLooks.Mark(seed) + ".jpg";
        }

        /// <summary>Whether every word of the query is in the name or the description, by the given comparison.</summary>
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
