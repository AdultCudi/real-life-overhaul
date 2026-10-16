using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>One part of a look as the game keeps it (AppearanceElementData): its kind (AppearanceElementType as a number), the game's variant and colour ids.</summary>
    public struct LookPart
    {
        public int Type;
        public string Variant;
        public string Colour;        // null when the game gave none
    }

    /// <summary>One of a face's shapes as the game keeps it (FacialBlendshape): the game sets them from the head it rolls (eyes, mouth, nose, brows).</summary>
    public struct LookShape
    {
        public string Name;
        public float Value;          // 0 to 100
        public int Category;         // AppearanceElementType as a number
        public int Kind;             // FacialBlendshape.BlendshapeType as a number
    }

    /// <summary>
    /// A look the game made, kept as it was: somebody of the crowd the player swapped numbers with in the street (Street,
    /// Wiring/StreetLinks) is that same person ever after, in the street, on their contact's picture and on Flare. The
    /// game's pedestrians are dressed by a roll of its own that no seed gives again (BaseHumanPool: SetRandomAppearance with
    /// no seed), so the look itself is kept: the sex, build, skin and eye colour, every part's variant and colour (face,
    /// hair, beard, clothes, the game's own ids) and the face's shapes. The age is not part of it: it is the person's own,
    /// and grows with them.
    ///
    /// THE SEED. Everything of the mod that draws a person asks by sex, age and seed (Social/SocialCast, the contacts'
    /// pictures, Flare's faces, Family, the roommates). A kept look has a seed of its own, worked out from its text
    /// (<see cref="SeedOf"/>, the same on every machine and every run), and <see cref="PersonLooks"/> knows which seeds are
    /// kept looks: those draw the look kept, every other seed the game's roll as before. The portrait caches name a kept
    /// look's face apart (<see cref="PersonLooks.Mark"/>), so it never meets a rolled face of the same number.
    ///
    /// THE TEXT (<see cref="Encode"/>): "1;f;strength;fatness;skin;eyes;parts;shapes", parts "type:variant:colour" and
    /// shapes "name:value:category:kind" joined by '+', every id with its reserved characters as %XX. It holds no '|' and
    /// no line break, so it is one field of the social life's record (Social/SocialState), at its end: a save without it
    /// loads, and an older RLO reads the record and leaves the field (the person then has the look of their seed).
    ///
    /// Game-free (EconomyCheck, StreetLookChecks.cs). COST: a few hundred characters made or read when somebody is met and
    /// when a save is read or written; nothing per frame.
    /// </summary>
    public sealed class PersonLook
    {
        public const int Version = 1;
        private const char Field = ';';
        private const char Item = '+';
        private const char Part = ':';
        private const int TextMost = 4096;   // characters: a look's text is some 600; anything longer is not a look

        public bool Female;
        public float Strength, Fatness;      // 0 to 1, the game's own
        public uint Skin, Eyes;              // RGBA, a byte each (the game's Color32)
        public readonly List<LookPart> Parts = new List<LookPart>(20);
        public readonly List<LookShape> Shapes = new List<LookShape>(16);

        /// <summary>The look as one line of text (see the class's summary).</summary>
        public string Encode()
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            var b = new StringBuilder(640);
            b.Append(Version.ToString(inv)).Append(Field).Append(Female ? 'f' : 'm').Append(Field)
                .Append(Strength.ToString("R", inv)).Append(Field).Append(Fatness.ToString("R", inv)).Append(Field)
                .Append(Skin.ToString("x8", inv)).Append(Field).Append(Eyes.ToString("x8", inv)).Append(Field);
            for (int i = 0; i < Parts.Count; i++)
            {
                if (i > 0)
                {
                    b.Append(Item);
                }
                b.Append(Parts[i].Type.ToString(inv)).Append(Part);
                Escape(b, Parts[i].Variant);
                b.Append(Part);
                if (Parts[i].Colour != null)
                {
                    Escape(b, Parts[i].Colour);
                }
                else
                {
                    b.Append('-');   // (no colour: an id is never a lone '-', which is escaped)
                }
            }
            b.Append(Field);
            for (int i = 0; i < Shapes.Count; i++)
            {
                if (i > 0)
                {
                    b.Append(Item);
                }
                Escape(b, Shapes[i].Name);
                b.Append(Part).Append(Shapes[i].Value.ToString("R", inv)).Append(Part).Append(Shapes[i].Category.ToString(inv)).Append(Part).Append(Shapes[i].Kind.ToString(inv));
            }
            return b.ToString();
        }

        /// <summary>A look read back from its text; null for anything that is not a whole look of this version (it is then the seed's).</summary>
        public static PersonLook Decode(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > TextMost)
            {
                return null;
            }
            string[] f = text.Split(Field);
            CultureInfo inv = CultureInfo.InvariantCulture;
            if (f.Length != 8 || f[0] != Version.ToString(inv) || (f[1] != "f" && f[1] != "m")
                || !float.TryParse(f[2], NumberStyles.Float, inv, out float strength) || !float.TryParse(f[3], NumberStyles.Float, inv, out float fatness)
                || !uint.TryParse(f[4], NumberStyles.HexNumber, inv, out uint skin) || !uint.TryParse(f[5], NumberStyles.HexNumber, inv, out uint eyes)
                || f[6].Length == 0 || !Finite(strength) || !Finite(fatness))
            {
                return null;
            }
            var look = new PersonLook { Female = f[1] == "f", Strength = Clamp01(strength), Fatness = Clamp01(fatness), Skin = skin, Eyes = eyes };
            foreach (string item in f[6].Split(Item))
            {
                string[] p = item.Split(Part);
                if (p.Length != 3 || !int.TryParse(p[0], NumberStyles.Integer, inv, out int type) || type < 0)
                {
                    return null;
                }
                string variant = Unescape(p[1]);
                string colour = p[2] == "-" ? null : Unescape(p[2]);
                if (string.IsNullOrEmpty(variant) || (p[2] != "-" && colour == null))
                {
                    return null;
                }
                look.Parts.Add(new LookPart { Type = type, Variant = variant, Colour = colour });
            }
            if (f[7].Length > 0)
            {
                foreach (string item in f[7].Split(Item))
                {
                    string[] p = item.Split(Part);
                    if (p.Length != 4 || !float.TryParse(p[1], NumberStyles.Float, inv, out float value) || !Finite(value)
                        || !int.TryParse(p[2], NumberStyles.Integer, inv, out int category) || !int.TryParse(p[3], NumberStyles.Integer, inv, out int kind))
                    {
                        return null;
                    }
                    string name = Unescape(p[0]);
                    if (string.IsNullOrEmpty(name))
                    {
                        return null;
                    }
                    look.Shapes.Add(new LookShape { Name = name, Value = Math.Max(0f, Math.Min(100f, value)), Category = category, Kind = kind });
                }
            }
            return look;
        }

        /// <summary>A kept look's seed, from its text: FNV-1a over its characters, never 0 and never negative; the same on every machine.</summary>
        public static int SeedOf(string text)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    hash = (hash ^ text[i]) * 16777619u;
                }
                int seed = (int)(hash & 0x7fffffffu);
                return seed == 0 ? 1 : seed;
            }
        }

        // An id's reserved characters (the look's own separators, the record's '|' and line breaks, '%' itself, any control
        // character) as %XX; an id that is a lone '-' is escaped too, as '-' stands for no colour.
        private static void Escape(StringBuilder b, string id)
        {
            if (id == null)
            {
                return;
            }
            if (id == "-")
            {
                b.Append("%2D");
                return;
            }
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (c < ' ' || c == Field || c == Item || c == Part || c == '|' || c == '%' || c == '~' || c == ',' || c == '=' || c == 127)
                {
                    b.Append('%').Append(((int)c).ToString("X2", CultureInfo.InvariantCulture));
                }
                else
                {
                    b.Append(c);
                }
            }
        }

        // Null for a broken escape.
        private static string Unescape(string text)
        {
            if (text.IndexOf('%') < 0)
            {
                return text;
            }
            var b = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '%')
                {
                    b.Append(text[i]);
                    continue;
                }
                if (i + 2 >= text.Length || !int.TryParse(text.Substring(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                {
                    return null;
                }
                b.Append((char)code);
                i += 2;
            }
            return b.ToString();
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        private static float Clamp01(float v) => Math.Max(0f, Math.Min(1f, v));
    }

    /// <summary>
    /// The kept looks of this save, by their seed (<see cref="PersonLook"/>): told by the social life as it reads its save
    /// and when somebody is met in the street, asked by everything that draws a person from a seed. Forgotten when the
    /// social life's state is reset (a load, the option off). Game-free. COST: one dictionary look-up per look built or
    /// face drawn; a handful of entries (the cast is twenty-odd people).
    /// </summary>
    public static class PersonLooks
    {
        private static readonly Dictionary<int, PersonLook> Known = new Dictionary<int, PersonLook>(8);

        /// <summary>The kept looks known now.</summary>
        public static int Count => Known.Count;

        /// <summary>A kept look's text made known: its seed, or 0 if the text is not a look (nothing is kept then).</summary>
        public static int Remember(string text)
        {
            PersonLook look = PersonLook.Decode(text);
            if (look == null)
            {
                return 0;
            }
            int seed = PersonLook.SeedOf(text);
            Known[seed] = look;
            return seed;
        }

        /// <summary>The kept look of a seed; false for a seed of the game's roll.</summary>
        public static bool TryGet(int seed, out PersonLook look)
        {
            return Known.TryGetValue(seed, out look);
        }

        public static bool Has(int seed) => Known.ContainsKey(seed);

        /// <summary>What the portrait caches add to a face's file name: "s" for a kept look, nothing for a rolled one (no allocation).</summary>
        public static string Mark(int seed) => Known.ContainsKey(seed) ? "s" : string.Empty;

        public static void Forget()
        {
            Known.Clear();
        }
    }
}
