using System;
using System.Globalization;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    /// <summary>What the front of one of the mod's buildings shows on the game's own sign holders (Core/FrontSigns).</summary>
    public enum FrontLook
    {
        Game,   // as the game has it: its own call decides (the building is not refused, not to let, or its feature is off)
        Off,    // the game's signs switched off: no "For rent", and the building's own board or dressing is its one sign
        Named   // the game's signs carry the company's or the institution's name and logo, made by the game's logo maker
    }

    /// <summary>How a named front looks: the logo the game's maker draws, and the sign's lights. Colours are 0 to 1.</summary>
    public struct FrontStyle
    {
        public string Shape;                         // one of the game's logo shapes (StreamingAssets/LogoShapes, without ".png")
        public int Font;                             // the game's FontFace by number: 0 Exo2, 3 NotoSerif, 4 Rubik
        public float BackR, BackG, BackB;            // the logo's ground
        public float TextR, TextG, TextB;            // the name's lettering
        public float MarkR, MarkG, MarkB;            // the logo shape
        public float NeonR, NeonG, NeonB;            // the sign's neon, on a sign type that has it
        public float LampR, LampG, LampB;            // the sign's bulbs and the light they throw
        public int Preferred;                        // which of the sign types with lights to take (in turn over them)
    }

    /// <summary>
    /// The rules of the mod's buildings' fronts, game-free and checked offline (NavCheck, FrontSignCheck.cs): which of the
    /// buildings the mod has made its own (Core/SpecialNameBook) lose the game's "For rent" sign and what they show
    /// instead, how each company's or institution's sign looks, which of the game's sign types it takes, and which of them
    /// the game's city map rows of units to let and buildings for sale leave out.
    ///
    /// Nothing here is saved, and nothing is read from a save: a building's look is worked out again from its claim and
    /// its registration as it stands. Every machine of a Going Public session has the same claims, so every machine works
    /// out the same fronts, and a building that any player rents or owns is never touched (it is the player's on every
    /// machine, and not refused).
    /// </summary>
    public static class FrontSignRules
    {
        public const string HqPrefix = "techhq_";   // Tech/Sites/Headquarters.IdPrefix: a tech company's headquarters, "techhq_" and its place in the list

        /// <summary>
        /// The look of a building in the book: <paramref name="refused"/> (its feature holds it, is on, and no player rents
        /// or owns it: SpecialNameBook.RefusesEntry), <paramref name="toLet"/> (the game holds it as to let, so its own sign
        /// would say "For rent"), <paramref name="board"/> (its feature stood a board of its own at its door), and
        /// <paramref name="named"/> (the option for the special buildings' names is on). A police station and a hotel are
        /// dressed by their own features (the station's fascia, the hotel's board): the game's signs are only switched off.
        /// </summary>
        public static FrontLook Look(SpecialKind kind, bool refused, bool toLet, bool board, bool named)
        {
            if (!refused || !toLet || !SpecialNameBook.Accepts(kind))
            {
                return FrontLook.Game;
            }
            if (kind == SpecialKind.Police || kind == SpecialKind.Hotel || board || !named)
            {
                return FrontLook.Off;
            }
            return FrontLook.Named;
        }

        /// <summary>
        /// True if the game's city map is to put out a light it gave a building of the mod's (CityMapFilters.ApplyFilters):
        /// a refused building with nothing of the game's in it that the game lit for its rows of units to let or buildings
        /// for sale. A business in it stays as the game's rows of businesses show it; the player's own stay theirs.
        /// </summary>
        public static bool PutOut(bool refused, bool players, bool business, bool toLet, bool forSale)
        {
            return refused && !players && !business && (toLet || forSale);
        }

        public const string TowerPrefix = "techhq_tower_";   // Tech/Sites/Headquarters: a tower the tech companies share, a floor each, "techhq_tower_" and its number

        /// <summary>The tower's number for a tower's id ("techhq_tower_1": 1), or -1. A tower is no one company's: it is not a headquarters' id.</summary>
        public static int TowerIndex(string id)
        {
            if (id == null || id.Length <= TowerPrefix.Length || !id.StartsWith(TowerPrefix, StringComparison.Ordinal))
            {
                return -1;
            }
            return int.TryParse(id.Substring(TowerPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int t) && t >= 0 ? t : -1;
        }

        /// <summary>The company's place in the tech sector's list for a headquarters' id ("techhq_3": 3), or -1.</summary>
        public static int HqIndex(string id)
        {
            if (id == null || id.Length <= HqPrefix.Length || !id.StartsWith(HqPrefix, StringComparison.Ordinal))
            {
                return -1;
            }
            return int.TryParse(id.Substring(HqPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int k) && k >= 0 ? k : -1;
        }

        // The logo shape of each tech company, by its place in the list (HiringCatalogue.RivalFirms): the game's own shapes.
        private static readonly string[] HqShapes =
        {
            "webdevelopmentagency3",   // Codeharbour Software: code
            "webdevelopmentagency1",   // Skyvault Systems: the web
            "webdevelopmentagency2",   // Abacus Lane Software: a window
            "electronicsstore2",       // Shopfront Online: a shop on a phone
            "graphicdesigner2",        // Pixel Harbour Games: a drawing tablet
            "marketing2",              // Tallyrow Analytics: a rising chart
            "recruitment1",            // Signpost Search: a lens
            "officesupply3",           // Circuitry Works: a computer
            "headquarter2",            // Quartzline Semiconductors: a facet
            "bank2",                   // Ledgerline Payments: a bank
            "headquarter1",            // Signalhill Telecom: a tower and the globe
            "headquarter3"             // Fibreway Networks: the city
        };

        /// <summary>The number of tech companies with a shape of their own (the others go round the list).</summary>
        public static int HqShapeCount => HqShapes.Length;

        /// <summary>
        /// The style of a building's named front: a tech company's in its own colours (Tech/Sites/RoomPlans HqColours: its
        /// main colour as the ground and the neon, its second for the lettering and the logo), an institution's in the
        /// colours of its board. The same id and kind give the same style on every machine.
        /// </summary>
        public static FrontStyle Style(SpecialKind kind, string id)
        {
            var s = new FrontStyle { LampR = 1f, LampG = 0.93f, LampB = 0.80f };   // a warm white for the bulbs
            int tower = TowerIndex(id);
            if (tower >= 0)
            {
                // A tower of the tech companies: its own name, in slate and brass, the same for every tower (its companies
                // are named on the game's card, at its door and in the map's search; each floor's lobby has its company's colours).
                Set(ref s, "headquarter1", 4, 0.14f, 0.16f, 0.20f, 0.96f, 0.97f, 0.98f, 0.86f, 0.74f, 0.42f, tower);
                s.NeonR = s.MarkR;
                s.NeonG = s.MarkG;
                s.NeonB = s.MarkB;
                return s;
            }
            int hq = HqIndex(id);
            if (hq >= 0)
            {
                HqColours.Of(hq, false, out s.BackR, out s.BackG, out s.BackB);
                HqColours.Of(hq, true, out s.TextR, out s.TextG, out s.TextB);
                s.MarkR = s.TextR;
                s.MarkG = s.TextG;
                s.MarkB = s.TextB;
                s.NeonR = s.BackR;
                s.NeonG = s.BackG;
                s.NeonB = s.BackB;
                s.Shape = HqShapes[hq % HqShapes.Length];
                s.Font = 0;   // Exo2
                s.Preferred = hq;
                return s;
            }
            switch (kind)
            {
                case SpecialKind.Civic:   // City Utilities: the board's green, gold and cream
                    Set(ref s, "headquarter3", 4, 0.06f, 0.24f, 0.22f, 0.95f, 0.95f, 0.90f, 0.93f, 0.80f, 0.30f, 1);
                    break;
                case SpecialKind.Court:   // the courthouse: the board's navy and brass
                    Set(ref s, "lawfirm1", 3, 0.09f, 0.13f, 0.25f, 0.93f, 0.90f, 0.80f, 0.78f, 0.62f, 0.26f, 2);
                    break;
                case SpecialKind.Campus:  // the university: the board's burgundy and gold
                    Set(ref s, "school1", 3, 0.36f, 0.08f, 0.12f, 0.95f, 0.91f, 0.80f, 0.82f, 0.68f, 0.30f, 3);
                    break;
                default:                  // any other role: plain, in the game's own default black on white
                    Set(ref s, "headquarter1", 4, 1f, 1f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0);
                    s.NeonR = s.NeonG = s.NeonB = 1f;
                    return s;
            }
            s.NeonR = s.MarkR;
            s.NeonG = s.MarkG;
            s.NeonB = s.MarkB;
            return s;
        }

        private static void Set(ref FrontStyle s, string shape, int font, float br, float bg, float bb, float tr, float tg, float tb, float mr, float mg, float mb, int preferred)
        {
            s.Shape = shape;
            s.Font = font;
            s.BackR = br;
            s.BackG = bg;
            s.BackB = bb;
            s.TextR = tr;
            s.TextG = tg;
            s.TextB = tb;
            s.MarkR = mr;
            s.MarkG = mg;
            s.MarkB = mb;
            s.Preferred = preferred;
        }

        /// <summary>
        /// The game's sign type a named front takes on one of the game's wide signs, by number (SignType.Type1 is 0): of the
        /// types that sign has with lights (<paramref name="lit"/>, the first <paramref name="litCount"/>), the preferred one
        /// in turn, so that it lights up at night as the game's own signs do; a sign with none lit takes the first type it
        /// has (<paramref name="first"/>), and one that has none, Type1 (what the game's own "For rent" sign uses).
        /// </summary>
        public static int SignType(int preferred, int[] lit, int litCount, int first)
        {
            int n = lit == null ? 0 : Math.Min(Math.Max(0, litCount), lit.Length);
            if (n > 0)
            {
                return lit[((preferred % n) + n) % n];
            }
            return first >= 0 ? first : 0;
        }

        /// <summary>
        /// A short mark of a name and its style, for the folder the made logo is kept in (outside the save): the same
        /// name in the same style finds the same files in the next session, and a change of either makes them anew.
        /// </summary>
        public static uint Mark(string name, FrontStyle s)
        {
            uint h = 2166136261u;
            h = Mix(h, name ?? string.Empty);
            h = Mix(h, s.Shape ?? string.Empty);
            h = Mix(h, s.Font);
            h = Mix(h, s.BackR); h = Mix(h, s.BackG); h = Mix(h, s.BackB);
            h = Mix(h, s.TextR); h = Mix(h, s.TextG); h = Mix(h, s.TextB);
            h = Mix(h, s.MarkR); h = Mix(h, s.MarkG); h = Mix(h, s.MarkB);
            return h;
        }

        private static uint Mix(uint h, string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                h = (h ^ text[i]) * 16777619u;
            }
            return (h ^ 0xffu) * 16777619u;
        }

        private static uint Mix(uint h, int value)
        {
            for (int i = 0; i < 4; i++)
            {
                h = (h ^ (uint)((value >> (8 * i)) & 0xff)) * 16777619u;
            }
            return h;
        }

        private static uint Mix(uint h, float value)
        {
            return Mix(h, (int)Math.Round(value * 1000f));
        }
    }
}
