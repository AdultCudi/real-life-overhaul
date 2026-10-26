using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The two people every game has from the start, Mom and Alex, as fixed characters: the same face for every player, on every
    /// machine, in a new game, an old game and an old save, as Uncle Fred is always Uncle Fred. THE ONE PLACE their look lives.
    /// The look is the game's own generator (AppearanceSetter.RandomizeElements) fed this gender, age and seed, which gives the same
    /// person every time; every scene that stands one of them in the world, every portrait on the phone and on Flare, and the
    /// record a save keeps read these constants and nothing else (SocialCatalogue.FixLook). Nothing about them is rolled or seeded by
    /// the save, and a save made before is put right on load (SocialState.Read, RoommateRules.Mend).
    /// To give one of them another face by hand, change that person's seed (and gender or age) here and nowhere else: every save,
    /// scene and portrait follows, and the contact pictures are drawn again. The name stays as the save has it (it is never changed).
    /// Everybody else the mod keeps (the partner, friends, people met) has one stored look per save instead (SocialPerson.Seed
    /// and Look). Game-free.
    /// </summary>
    public static class FixedPeople
    {
        public const string MomName = "Mom";
        public const int MomSeed = 77171;        // the wedding's mother has always had this look
        public const int MomAge = 58;            // years
        public const bool MomFemale = true;

        public const string AlexName = "Alex";
        public const int AlexSeed = 52817;
        public const int AlexAge = 30;           // years
        public const bool AlexFemale = true;     // a woman, as the player met her at the start (the owner, 26 Oct 2026)

        /// <summary>One fixed person's look.</summary>
        public struct Entry
        {
            public string Name;
            public bool Female;
            public int Age;          // years
            public int Seed;
        }

        private const int RepairsMost = 8;   // lines held until the director logs them

        private static readonly List<string> Lines = new List<string>(RepairsMost);
        private static readonly List<string> Names = new List<string>(RepairsMost);
        private static readonly List<string> Told = new List<string>(RepairsMost);   // who the player has been told of since the save was read

        /// <summary>The fixed person in a cast place: 0 is Mom, 1 is Alex (the cast's first two places, SocialOrigin.Family and OldFriend). False for anybody else.</summary>
        public static bool TryGet(int index, out Entry entry)
        {
            if (index == 0)
            {
                entry = new Entry { Name = MomName, Female = MomFemale, Age = MomAge, Seed = MomSeed };
                return true;
            }
            if (index == 1)
            {
                entry = new Entry { Name = AlexName, Female = AlexFemale, Age = AlexAge, Seed = AlexSeed };
                return true;
            }
            entry = default(Entry);
            return false;
        }

        /// <summary>True while a repair waits to be logged and told to the player.</summary>
        public static bool Waiting => Lines.Count > 0;

        /// <summary>A person's record was put right: one log line is held for it, and its name for a single notice. Lines past the most held are dropped.</summary>
        public static void Mended(string name, string where, bool female, int age, int seed)
        {
            if (string.IsNullOrEmpty(name) || Lines.Count >= RepairsMost)
            {
                return;
            }
            Lines.Add("People: " + name + "'s look put right in " + where + ", now the one look: " + (female ? "a woman" : "a man") + " of " + age + ", seed " + seed + ".");
            if (!Names.Contains(name))
            {
                Names.Add(name);
            }
        }

        /// <summary>
        /// Hands over what waits: one log line for each repair and each person's name for a single notice (a person already told of
        /// since the save was read, by the social life's repair or the roommate's, is not named twice). Both lists are emptied first.
        /// </summary>
        public static void Take(List<string> lines, List<string> names)
        {
            lines.Clear();
            names.Clear();
            lines.AddRange(Lines);
            for (int i = 0; i < Names.Count; i++)
            {
                if (!Told.Contains(Names[i]))
                {
                    Told.Add(Names[i]);
                    names.Add(Names[i]);
                }
            }
            Lines.Clear();
            Names.Clear();
        }

        /// <summary>A save is being read: nobody has been told of yet. What waits is kept.</summary>
        public static void Forget()
        {
            Told.Clear();
        }
    }
}
