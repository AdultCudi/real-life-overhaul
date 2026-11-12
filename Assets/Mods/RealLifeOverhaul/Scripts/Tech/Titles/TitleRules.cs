using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The names of products and games. A typed name is cleaned (<see cref="Clean"/>) and never read for anything but showing it.
    /// A made-up name is two words of its style's lists (<see cref="TitleCatalogue"/>). The rivals take the pairings whose places
    /// add up even and the player's suggestions the odd ones, so a suggestion is never a rival's name; a rival product's name
    /// comes from its id alone, so every machine of a Going Public session reads the same with nothing sent.
    /// Game-free; cost: a name is made on an event (a product started, a page drawn), never per frame.
    /// </summary>
    public static class TitleRules
    {
        /// <summary>What a typed name is answered with: kept, refused as empty, refused as taken, or refused for a withdrawn product.</summary>
        public const int Ok = 0, Empty = 1, Taken = 2, Closed = 3;

        /// <summary>The two sides that share a style's pairings: the rivals' names and the player's suggestions.</summary>
        public const int Rivals = 0, Player = 1;

        private const int StepLeast = 53;   // the stride through a style's names: neighbours in the lists are far apart in the order names are given
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static HashSet<string> _denied;

        // ---------------------------------------------------------------- typed names

        /// <summary>
        /// A typed name as it is kept: no tags or braces (they would be read as rich text or as a text's own names), no line
        /// breaks or control characters, none of the save's separators, runs of spaces as one, trimmed, at most
        /// <see cref="TitleCatalogue.Most"/> characters. Any language's letters stay. Empty for a name with nothing to show.
        /// </summary>
        public static string Clean(string typed)
        {
            if (string.IsNullOrEmpty(typed))
            {
                return string.Empty;
            }
            var b = new StringBuilder(typed.Length);
            bool gap = false, shown = false;
            for (int i = 0; i < typed.Length; i++)
            {
                char c = typed[i];
                bool pair = char.IsHighSurrogate(c) && i + 1 < typed.Length && char.IsLowSurrogate(typed[i + 1]);
                if (!pair)
                {
                    if (char.IsSurrogate(c) || c == '<' || c == '>' || c == '{' || c == '}')
                    {
                        continue;   // half a pair shows as a box; the others open a tag or a text's own name
                    }
                    UnicodeCategory kind = char.GetUnicodeCategory(c);
                    bool joiner = c == (char)0x200C || c == (char)0x200D;   // the joiners Persian and Indic words are written with
                    if (kind == UnicodeCategory.Format && !joiner)
                    {
                        continue;   // zero width spaces and the marks that turn a line round
                    }
                    if (char.IsWhiteSpace(c) || char.IsControl(c) || c == '~' || c == ';' || c == '|' || kind == UnicodeCategory.LineSeparator
                        || kind == UnicodeCategory.ParagraphSeparator)
                    {
                        gap = true;   // the save writes its own separators as a space (TechWriter.Add): what is typed is what is read back
                        continue;
                    }
                    shown |= !joiner;
                }
                if (gap && b.Length > 0)
                {
                    b.Append(' ');
                }
                gap = false;
                b.Append(c);
                if (pair)
                {
                    b.Append(typed[++i]);
                    shown = true;
                }
            }
            return shown ? Cut(b.ToString(), TitleCatalogue.Most) : string.Empty;
        }

        /// <summary>A text cut to so many characters without halving a pair, trimmed.</summary>
        public static string Cut(string text, int most)
        {
            if (text == null || most <= 0)
            {
                return string.Empty;
            }
            if (text.Length <= most)
            {
                return text.Trim();
            }
            int end = char.IsHighSurrogate(text[most - 1]) ? most - 1 : most;
            return text.Substring(0, end).Trim();
        }

        /// <summary>
        /// The form two names are compared in: their letters and digits in lower case, so "Brightsheet" and "bright sheet" are one
        /// name. A name of signs alone is compared as written. Never a key of anything saved.
        /// </summary>
        public static string Key(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }
            var b = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsLetterOrDigit(c) || char.IsSurrogate(c))
                {
                    b.Append(char.ToLowerInvariant(c));
                }
            }
            return b.Length > 0 ? b.ToString() : name.Trim().ToLowerInvariant();
        }

        public static bool Same(string a, string b) => string.Equals(Key(a), Key(b), StringComparison.Ordinal);

        /// <summary>What a cleaned name is answered with: <see cref="Ok"/>, <see cref="Empty"/> or <see cref="Taken"/>.</summary>
        public static int Check(string cleaned, Func<string, bool> taken)
        {
            return string.IsNullOrEmpty(cleaned) ? Empty : taken != null && taken(cleaned) ? Taken : Ok;
        }

        // ---------------------------------------------------------------- made-up names

        public static int StyleOf(ProductKind kind, int segment, int genre)
        {
            switch (kind)
            {
                case ProductKind.App: return TitleStyle.App;
                case ProductKind.Licence: return TitleStyle.Licence;
                case ProductKind.Model: return TitleStyle.Model;
                case ProductKind.Game: return StyleOfGenre(genre);
                default: return segment <= (int)Segment.Consumer ? TitleStyle.SaasConsumer : segment == (int)Segment.Business ? TitleStyle.SaasBusiness : TitleStyle.SaasEnterprise;
            }
        }

        public static int StyleOfGenre(int genre) => TitleStyle.Games + Math.Max(0, Math.Min((int)Genre.Count - 1, genre));

        /// <summary>The style of what is sold in one of the sector's markets: the three kinds of AI market share the models' names.</summary>
        public static int StyleOfMarket(int market)
        {
            switch ((SectorMarket)market)
            {
                case SectorMarket.ConsumerSoftware: return TitleStyle.SaasConsumer;
                case SectorMarket.BusinessSoftware: return TitleStyle.SaasBusiness;
                case SectorMarket.EnterpriseSoftware: return TitleStyle.SaasEnterprise;
                case SectorMarket.Apps: return TitleStyle.App;
                case SectorMarket.Licences: return TitleStyle.Licence;
                default: return TitleStyle.Model;
            }
        }

        /// <summary>The names one side has in a style: every first word with half of the second ones.</summary>
        public static int Space(int style)
        {
            style = Math.Max(0, Math.Min(TitleStyle.Count - 1, style));
            return TitleCatalogue.First[style].Length * (TitleCatalogue.Second[style].Length / 2);
        }

        /// <summary>
        /// A side's name of a style by its place, 0 to <see cref="Space"/> less one: the first word and one of the second words
        /// whose place makes the two add up even for the rivals and odd for the player, so the sides never share a name.
        /// </summary>
        public static string Name(int style, int side, int index)
        {
            style = Math.Max(0, Math.Min(TitleStyle.Count - 1, style));
            string[] first = TitleCatalogue.First[style], second = TitleCatalogue.Second[style];
            int half = second.Length / 2, space = first.Length * half;
            int k = ((index % space) + space) % space;
            int a = k / half, b = 2 * (k % half) + ((a + side) & 1);
            return TitleCatalogue.Joined[style] ? first[a] + second[b] : first[a] + " " + second[b];
        }

        /// <summary>
        /// A rival product's name from its id and its market, the two things every machine of a session holds alike: ids one
        /// style's worth apart never share a name, and past that a number follows it. Nothing of it is saved or sent.
        /// </summary>
        public static string Rival(int id, int market)
        {
            int style = StyleOfMarket(market), space = Space(style), n = Math.Max(0, id);
            return Numbered(Name(style, Rivals, Spread(n % space, style, space)), n / space);
        }

        /// <summary>
        /// A rival studio's game by the rivals' seed, its id and its genre: the first of its style's names from there on that
        /// <paramref name="taken"/> lacks (the rival games on sale), numbered once the style has none left.
        /// </summary>
        public static string RivalGame(int seed, int id, int genre, Func<string, bool> taken)
        {
            int style = StyleOfGenre(genre);
            return Fresh(style, Rivals, (int)(TechRandom.Hash(seed, style, 91) % (uint)Space(style)) + (long)Math.Max(0, id), taken);
        }

        /// <summary>
        /// A name offered to the player for a product (the save's seed, the product's id, its style): the first from there on that
        /// <paramref name="taken"/> lacks. Each <paramref name="turn"/> offers the next one, so "suggest" never says the same twice.
        /// </summary>
        public static string Suggest(int seed, int id, int style, int turn, Func<string, bool> taken)
        {
            style = Math.Max(0, Math.Min(TitleStyle.Count - 1, style));
            return Fresh(style, Player, (int)(TechRandom.Hash(seed, id, style, 92) % (uint)Space(style)) + (long)Math.Max(0, turn), taken);
        }

        // The first free name of a side from a place on, each tried once; with none free, the first one numbered until it is.
        private static string Fresh(int style, int side, long from, Func<string, bool> taken)
        {
            int space = Space(style);
            for (int i = 0; i < space; i++)
            {
                string name = Name(style, side, Spread((int)((from + i) % space), style, space));
                if (taken == null || !taken(name))
                {
                    return name;
                }
            }
            string stem = Name(style, side, Spread((int)(from % space), style, space));
            for (int n = 2; n < 100000; n++)
            {
                if (!taken(stem + " " + n.ToString(Inv)))
                {
                    return stem + " " + n.ToString(Inv);
                }
            }
            return stem;
        }

        // A place among a style's names spread by a stride that shares no factor with their number: every place is met once.
        private static int Spread(int index, int style, int space)
        {
            int step = StepLeast;
            while (Gcd(step, space) != 1)
            {
                step++;
            }
            return (int)(((long)index * step + style * 7L) % space);
        }

        private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

        private static string Numbered(string name, int round) => round <= 0 ? name : name + " " + (round + 1).ToString(Inv);

        // ---------------------------------------------------------------- names made from a game's name

        /// <summary>
        /// A sequel's name from the game it follows: "Ember Crown" gives "Ember Crown 2", and that gives "Ember Crown 3"; the next
        /// number <paramref name="taken"/> lacks, the name shortened to fit. Empty for a game with no name.
        /// </summary>
        public static string Sequel(string prior, Func<string, bool> taken)
        {
            string stem = Clean(prior);
            if (stem.Length == 0)
            {
                return string.Empty;
            }
            int number = 1, space = stem.LastIndexOf(' ');
            if (space > 0 && int.TryParse(stem.Substring(space + 1), NumberStyles.None, Inv, out int n) && n >= 2 && n < 1000)
            {
                number = n;
                stem = stem.Substring(0, space).Trim();
            }
            for (int next = number + 1; next < number + 1000; next++)
            {
                string tail = " " + next.ToString(Inv);
                string name = Cut(stem, TitleCatalogue.Most - tail.Length) + tail;
                if (taken == null || !taken(name))
                {
                    return name;
                }
            }
            return Cut(stem, TitleCatalogue.Most - 2) + " 2";
        }

        /// <summary>An add-on's name, the game's and one of the catalogue's after a colon, by the game's id and which add-on it is (from 1).</summary>
        public static string AddOn(string game, int id, int number)
        {
            if (string.IsNullOrEmpty(game))
            {
                return string.Empty;
            }
            int n = Math.Max(1, number) - 1, count = TitleCatalogue.AddOns.Length;
            int at = (int)((TechRandom.Hash(id, 93) % (uint)count + (uint)n) % (uint)count);
            return Numbered(game + ": " + TitleCatalogue.AddOns[at], n / count);
        }

        /// <summary>A free update's name, the game's with its version: the first is "1.1".</summary>
        public static string Update(string game, int number)
        {
            return string.IsNullOrEmpty(game) ? string.Empty : game + " 1." + Math.Max(1, number).ToString(Inv);
        }

        /// <summary>A product with who makes it, where one line has to say both: "Brightsheet (Codeharbour Software)".</summary>
        public static string By(string title, string maker)
        {
            return string.IsNullOrEmpty(title) ? maker ?? string.Empty : string.IsNullOrEmpty(maker) ? title : title + " (" + maker + ")";
        }

        // ---------------------------------------------------------------- real names

        /// <summary>True for a name that is a famous real one, or holds one as a word (<see cref="TitleCatalogue.Denied"/>).</summary>
        public static bool Denied(string name)
        {
            if (_denied == null)
            {
                _denied = new HashSet<string>(TitleCatalogue.Denied, StringComparer.Ordinal);
            }
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            if (_denied.Contains(Key(name)))
            {
                return true;
            }
            string[] words = name.Split(' ');
            for (int i = 0; words.Length > 1 && i < words.Length; i++)
            {
                if (words[i].Length > 0 && _denied.Contains(Key(words[i])))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
