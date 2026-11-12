using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Where a person's picture comes from, in the one order every screen follows (<see cref="UiFaceRules.From"/>).</summary>
    public enum UiFaceFrom
    {
        Painted,   // nothing of the game's: the kit's own painted face
        Game,      // the game's own picture: its sprite, or the game's portrait camera drawing the character
        Waiting    // the game has a picture of them that is not made yet: a plain disc, never somebody else's face
    }

    /// <summary>What the kit knows of one person, by the name a screen shows them under.</summary>
    public struct UiFaceFacts
    {
        public bool HasGame;     // the game, or the mod's portrait cache, has a picture of them (or can make one from their data)
        public bool GameReady;   // ...and it is here
        public bool HasTraits;   // the mod records their sex and age, though they have no data to draw them from
        public bool Female;
        public int Age;
    }

    /// <summary>
    /// The picture of a person, one rule for every screen of the kit: the game's own picture of them (a sprite the game
    /// holds, or the game's portrait generator drawing the character from their data, as it draws the HUD's portrait),
    /// then, only for somebody the game has no picture of, the kit's painted face, which follows the traits the mod
    /// records for them (sex and age) and never a random face. While a game picture is still being made the person shows a
    /// plain disc: a painted stand-in would be a stranger. The names are the screens' own; the book holds only what is
    /// known. Game-free. Cost: a dictionary read a portrait bound, nothing a frame.
    /// </summary>
    public static class UiFaceRules
    {
        /// <summary>The age from which a painted face has grey hair, as a person's does.</summary>
        public const int GreyFrom = 60;   // years

        /// <summary>Which picture a person shows: the game's when there is one (the disc while it comes), else the painted face.</summary>
        public static UiFaceFrom From(UiFaceFacts f)
        {
            return f.HasGame ? (f.GameReady ? UiFaceFrom.Game : UiFaceFrom.Waiting) : UiFaceFrom.Painted;
        }

        /// <summary>The seed a painted face is made from: the name's, with the recorded sex when the mod has one.</summary>
        public static int PaintSeed(string name, UiFaceFacts f)
        {
            return f.HasTraits ? UiPortraits.SeedFor(name, f.Female) : UiPortraits.Seed(name);
        }

        /// <summary>The age a painted face is made at, 0 when the mod records none.</summary>
        public static int PaintAge(UiFaceFacts f)
        {
            return f.HasTraits ? Math.Max(0, f.Age) : 0;
        }
    }

    /// <summary>
    /// The people the kit knows something of, by the name a screen shows: whether the game has a picture of them and the
    /// traits the mod records. Told by the screens that read the game and the features that make people; a person not in
    /// it is painted from their name alone. <see cref="Version"/> changes whenever something here or a game picture
    /// changes, so a screen shown is laid out again. Game-free.
    /// </summary>
    public static class UiFaceBook
    {
        private static readonly Dictionary<string, UiFaceFacts> People = new Dictionary<string, UiFaceFacts>(64, StringComparer.Ordinal);

        /// <summary>Changes whenever a person is told or a game picture arrives.</summary>
        public static int Version { get; private set; }

        /// <summary>What is known of a name, false for nobody.</summary>
        public static bool TryGet(string name, out UiFaceFacts facts)
        {
            if (!string.IsNullOrEmpty(name) && People.TryGetValue(name, out facts))
            {
                return true;
            }
            facts = default(UiFaceFacts);
            return false;
        }

        /// <summary>True when the name has a game picture told.</summary>
        public static bool HasGame(string name)
        {
            return TryGet(name, out UiFaceFacts f) && f.HasGame;
        }

        /// <summary>The game has a picture of this name (or can draw one from their data).</summary>
        public static void KnowGame(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            People.TryGetValue(name, out UiFaceFacts f);
            if (!f.HasGame)
            {
                f.HasGame = true;
                People[name] = f;
                Version++;
            }
        }

        /// <summary>The mod records this person's sex and age (0 when it does not know the age: one told before stays).</summary>
        public static void KnowTraits(string name, bool female, int age)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            People.TryGetValue(name, out UiFaceFacts f);
            age = age > 0 ? age : f.Age;
            if (!f.HasTraits || f.Female != female || f.Age != age)
            {
                f.HasTraits = true;
                f.Female = female;
                f.Age = age;
                People[name] = f;
                Version++;
            }
        }

        /// <summary>A game picture arrived, or went: screens showing it are laid out again.</summary>
        public static void Changed()
        {
            Version++;
        }

        /// <summary>Forgets everyone: a new city's people are not the last one's.</summary>
        public static void Clear()
        {
            People.Clear();
            Version++;
        }
    }
}
