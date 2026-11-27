using System;

namespace RealLifeOverhaul
{
    /// <summary>What a person can be into, hate or want, one of each kind. Saved as bits of <see cref="SocialPerson.Heard"/>: new kinds go at the end.</summary>
    public enum InterestKind { Show, Hobby, Sport, Music, Trip, Food, Hates, Plan, Peeve }

    /// <summary>
    /// The things each person is into: a TV show, a hobby, a sport, music, a dream trip, a food they love, a food they cannot stand, a plan for
    /// the future and a pet hate. Nothing about it is saved: a person's set is rolled again from their own seed on a stream of its own whenever it
    /// is wanted, so it is the same on every machine, no other roll of the cast moves, and a save made before it gets its people's things when it
    /// loads. What is saved is only which of them the player has heard (<see cref="SocialPerson.Heard"/>). Game-free. COST: one small generator
    /// when a set is rolled, on a press or a page, never per frame.
    /// </summary>
    public static partial class SocialInterests
    {
        public const int Kinds = 9;                  // the number of InterestKinds
        public const int PerKind = 12;               // things to be into for each kind: a person has one of them
        public const int AllHeard = (1 << Kinds) - 1;   // every kind heard
        private const int InterestSalt = 9101;       // theirs alone: the roll of a person's things, apart from every other roll of the cast

        /// <summary>
        /// The seed a person's things come from: the one kept the first time they were needed (<see cref="SocialPerson.InterestSeed"/>), so a look
        /// replaced later never changes what they like, else their own seed.
        /// </summary>
        public static int SeedOf(SocialPerson person)
        {
            return person.InterestSeed != 0 ? person.InterestSeed : person.Seed;
        }

        /// <summary>A person's things, one index into <see cref="PerKind"/> for each kind, by their seed: the same seed gives the same set.</summary>
        public static void Roll(int seed, int[] into)
        {
            var random = new Random(SocialRules.SlotSeed(seed, InterestSalt));
            for (int k = 0; k < Kinds && k < into.Length; k++)
            {
                into[k] = random.Next(PerKind);
            }
        }

        /// <summary>The index of the thing a person with this seed is into for one kind.</summary>
        public static int ValueOf(int seed, int kind)
        {
            var scratch = new int[Kinds];
            Roll(seed, scratch);
            return scratch[kind < 0 ? 0 : kind >= Kinds ? Kinds - 1 : kind];
        }

        /// <summary>The key of the text of one thing, as an answer reads (with a capital).</summary>
        public static string ThingKey(int kind, int value) => Things[kind][value];

        /// <summary>How many kinds a heard mask holds.</summary>
        public static int Count(int heard)
        {
            int count = 0;
            for (int k = 0; k < Kinds; k++)
            {
                count += (heard >> k & 1) != 0 ? 1 : 0;
            }
            return count;
        }

        public static bool Has(int heard, int kind) => kind >= 0 && kind < Kinds && (heard >> kind & 1) != 0;
    }
}
