using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Street
{
    /// <summary>What the player's own words are taken for. The game acts only on these, by its own rules.</summary>
    public enum StreetSayKind
    {
        Other, Nonsense, Greeting, Goodbye, Thanks, SmallTalk, AboutThem, Name, Area, Directions, Compliment, Joke, Flirt, Number, Invite,
        Election, Economy, Business, Rude, Threat, Day, Age
    }

    /// <summary>The reading of what the player typed.</summary>
    public struct StreetSaid
    {
        public StreetSayKind Kind;
        public PlanKind Outing;          // an invitation's outing (FlareInviteRules.Read)
        public StreetPlace Place;        // a kind of place asked the way to; Count when none was named
        public string Target;            // the words after "where is" and the like: a business asked for by name, or null
        public int Shop;                 // one of the player's own businesses named (StreetFacts.Shops), or -1
    }

    /// <summary>
    /// The player's own words in a conversation, read with fixed rules: they change the game only through what they are read
    /// as. Game-free. COST: one pass over a line of at most <see cref="Most"/> characters, when sent.
    /// </summary>
    public static class StreetSay
    {
        public const int Most = 120;   // characters the text box takes

        private static readonly string[] Threats =
        {
            "kill you", "hurt you", "beat you", "smash your", "punch you", "stab", "i'll get you", "ill get you", "or else", "watch your back", "break your",
            "hit you", "end you", "you're dead", "youre dead", "rob you", "give me your wallet", "give me your money", "i know where you live"
        };
        private static readonly string[] Insults =
        {
            "idiot", "stupid", "ugly", "loser", "shut up", "get lost", "moron", "dumb", "freak", "piss off", "go away", "nobody asked", "clown", "pathetic",
            "disgusting", "you stink", "you're fat", "youre fat", "creep", "jerk", "useless", "hate you", "get out of my way", "move it"
        };
        private static readonly string[] NumberWords = { "your number", "ur number", "phone number", "digits", "call you", "text you", "give me your number", "swap numbers", "number?" };
        private static readonly string[] WayWords =
        {
            "where is", "where's", "wheres", "where are", "how do i get to", "how to get to", "how do i find", "nearest", "looking for", "is there a",
            "is there any", "know a", "know any", "directions", "the way to", "find a", "find the", "get to the", "near here", "around here somewhere"
        };
        private static readonly string[] AreaWords =
        {
            "around here", "nearby", "this area", "what's good", "whats good", "neighbourhood", "neighborhood", "to do here", "what's on", "whats on", "local"
        };
        private static readonly string[] ElectionWords = { "election", "vote", "voting", "mayor", "candidate", "council", "politic" };
        private static readonly string[] EconomyWords = { "prices", "price", "economy", "inflation", "rent", "expensive", "bank rate", "interest rate", "cost of living", "money these days" };
        private static readonly string[] OwnWords = { "my shop", "my business", "my store", "my company", "my place", "my restaurant", "my cafe", "my bar" };
        private static readonly string[] NameWords = { "your name", "who are you", "my name is", "i'm called", "call me ", "name?" };
        private static readonly string[] FlirtWords =
        {
            "cute", "gorgeous", "beautiful", "handsome", "single", "sexy", "pretty", "your smile", "your eyes", "a date", "go out with me", "attractive", "fancy you"
        };
        private static readonly string[] ComplimentWords = { "nice", "love your", "like your", "great", "cool", "you look", "lovely", "smart", "good taste", "well dressed", "style" };
        private static readonly string[] JokeWords = { "joke", "knock knock", "why did", "haha", "lol", "funny" };
        private static readonly string[] AgeWords = { "how old", "your age", "what age", "age are you", "when were you born", "year were you born" };
        private static readonly string[] AboutWords = { "what do you do", "your job", "do you work", "work as", "where do you live", "you from", "about you" };
        private static readonly string[] DayWords = { "how are you", "how's it going", "hows it going", "how are things", "your day", "how's life", "hows life", "how have you been" };
        private static readonly string[] ByeWords = { "bye", "see you", "see ya", "take care", "later", "have a good", "gotta go", "got to go", "i'm off", "im off", "farewell" };
        private static readonly string[] ThanksWords = { "thanks", "thank you", "cheers", "appreciate" };
        private static readonly string[] HelloWords = { "hi", "hello", "hey", "good morning", "good afternoon", "good evening", "howdy", "yo", "hiya", "excuse me" };
        private static readonly string[] SmallWords =
        {
            "weather", "raining", "rainy", "sunny", "nice day", "lovely day", "cold out", "hot out", "freezing", "traffic", "busy today", "quiet today", "quiet tonight"
        };

        private static readonly string[][] PlaceWords =
        {
            new[] { "coffee", "cafe", "café", "espresso", "latte", "cappuccino" },
            new[] { "restaurant", "eat", "food", "lunch", "dinner", "hungry", "burger", "pizza", "breakfast" },
            new[] { "bar", "pub", "drink", "club", "nightclub", "beer", "pint" },
            new[] { "supermarket", "grocer", "groceries", "food shop", "corner shop", "market" },
            new[] { "clothes", "clothing", "fashion", "shoes", "outfit" },
            new[] { "gym", "workout", "fitness", "work out" },
            new[] { "cinema", "movies", "movie", "film", "theatre", "theater" },
            new[] { "bank", "atm", "cash machine", "cashpoint" }
        };

        private static readonly string[] Lead = { "where is", "where's", "wheres", "how do i get to", "how to get to", "how do i find", "looking for", "the way to", "get to", "find" };
        private static readonly string[] Articles = { "the ", "a ", "an ", "some " };

        private static readonly StringBuilder Plain = new StringBuilder(Most + 8);

        /// <summary>Reads the player's words; <paramref name="shops"/> are the player's businesses, so a mention of one is known.</summary>
        public static StreetSaid Read(string text, IReadOnlyList<string> shops)
        {
            var said = new StreetSaid { Kind = StreetSayKind.Other, Place = StreetPlace.Count, Shop = -1 };
            string t = Lower(text);
            if (t.Length == 0 || !HasLetter(t))
            {
                said.Kind = StreetSayKind.Nonsense;
                return said;
            }
            if (Any(t, Threats))
            {
                said.Kind = StreetSayKind.Threat;
                return said;
            }
            if (Any(t, Insults) || FlareFilter.Clean(text, FlareFilter.Standard) != text)
            {
                said.Kind = StreetSayKind.Rude;
                return said;
            }
            if (FlareInviteRules.Read(text, out PlanKind outing))
            {
                said.Kind = StreetSayKind.Invite;
                said.Outing = outing;
                return said;
            }
            if (Any(t, NumberWords) || (t.Contains("number") && t.Contains("your")))
            {
                said.Kind = StreetSayKind.Number;
                return said;
            }
            said.Shop = ShopNamed(t, shops);
            if (Any(t, WayWords))
            {
                said.Kind = StreetSayKind.Directions;
                said.Place = PlaceOf(t);
                said.Target = said.Place == StreetPlace.Count ? Target(t) : null;
                if (said.Place != StreetPlace.Count || said.Target != null)
                {
                    return said;
                }
            }
            if (Any(t, ElectionWords))
            {
                said.Kind = StreetSayKind.Election;
                return said;
            }
            if (Any(t, EconomyWords))
            {
                said.Kind = StreetSayKind.Economy;
                return said;
            }
            if (said.Shop >= 0 || Any(t, OwnWords))
            {
                said.Kind = StreetSayKind.Business;
                return said;
            }
            if (Any(t, AreaWords))
            {
                said.Kind = StreetSayKind.Area;
                return said;
            }
            said.Kind = Any(t, NameWords) ? StreetSayKind.Name
                : Any(t, FlirtWords) ? StreetSayKind.Flirt
                : Any(t, JokeWords) ? StreetSayKind.Joke
                : Any(t, AgeWords) ? StreetSayKind.Age
                : Any(t, AboutWords) ? StreetSayKind.AboutThem
                : Any(t, DayWords) ? StreetSayKind.Day
                : Any(t, SmallWords) ? StreetSayKind.SmallTalk
                : Any(t, ComplimentWords) ? StreetSayKind.Compliment
                : Any(t, ByeWords) ? StreetSayKind.Goodbye
                : Any(t, ThanksWords) ? StreetSayKind.Thanks
                : Word(t, HelloWords) ? StreetSayKind.Greeting
                : StreetSayKind.Other;
            return said;
        }

        /// <summary>The choice a reading counts as for the rules; Count for words they have no question for.</summary>
        public static StreetAsk AskOf(StreetSayKind kind)
        {
            switch (kind)
            {
                case StreetSayKind.SmallTalk: return StreetAsk.SmallTalk;
                case StreetSayKind.AboutThem: return StreetAsk.AboutThem;
                case StreetSayKind.Name: return StreetAsk.Name;
                case StreetSayKind.Area: return StreetAsk.Area;
                case StreetSayKind.Directions: return StreetAsk.Directions;
                case StreetSayKind.Compliment: return StreetAsk.Compliment;
                case StreetSayKind.Joke: return StreetAsk.Joke;
                case StreetSayKind.Flirt: return StreetAsk.Flirt;
                case StreetSayKind.Number: return StreetAsk.Number;
                case StreetSayKind.Invite: return StreetAsk.Invite;
                case StreetSayKind.Election: return StreetAsk.Election;
                case StreetSayKind.Economy: return StreetAsk.Economy;
                case StreetSayKind.Business: return StreetAsk.Business;
                case StreetSayKind.Rude: return StreetAsk.Rude;
                case StreetSayKind.Day: return StreetAsk.Day;
                case StreetSayKind.Thanks: return StreetAsk.Thanks;
                case StreetSayKind.Age: return StreetAsk.Age;
                default: return StreetAsk.Count;
            }
        }

        public static StreetPlace PlaceOf(string lower)
        {
            for (int p = 0; p < PlaceWords.Length; p++)
            {
                if (Word(lower, PlaceWords[p]))
                {
                    return (StreetPlace)p;
                }
            }
            return StreetPlace.Count;
        }

        /// <summary>The name asked for after "where is" and the like, without an article; null if nothing is left.</summary>
        public static string Target(string lower)
        {
            for (int i = 0; i < Lead.Length; i++)
            {
                int at = lower.IndexOf(Lead[i], StringComparison.Ordinal);
                if (at < 0)
                {
                    continue;
                }
                string rest = lower.Substring(at + Lead[i].Length).Trim(' ', '?', '.', ',', '!');
                for (int a = 0; a < Articles.Length; a++)
                {
                    if (rest.StartsWith(Articles[a], StringComparison.Ordinal))
                    {
                        rest = rest.Substring(Articles[a].Length);
                        break;
                    }
                }
                return rest.Length >= 3 ? rest : null;
            }
            return null;
        }

        /// <summary>True if a name asked for is this business's: the same letters and figures, or one inside the other of four or more.</summary>
        public static bool Names(string asked, string business)
        {
            string a = Squash(asked), b = Squash(business);
            if (a.Length == 0 || b.Length == 0)
            {
                return false;
            }
            if (a == b)
            {
                return true;
            }
            return (a.Length >= 4 && b.Contains(a)) || (b.Length >= 4 && a.Contains(b));
        }

        private static int ShopNamed(string lower, IReadOnlyList<string> shops)
        {
            if (shops == null)
            {
                return -1;
            }
            string words = Squash(lower);
            for (int i = 0; i < shops.Count; i++)
            {
                string s = Squash(shops[i]);
                if (s.Length >= 4 && words.Contains(s))
                {
                    return i;
                }
            }
            return -1;
        }

        private static string Squash(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            var t = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = char.ToLowerInvariant(text[i]);
                if (char.IsLetterOrDigit(c))
                {
                    t.Append(c);
                }
            }
            return t.ToString();
        }

        // Lower case, one space between words, the typographer's apostrophe made plain, at most Most characters.
        private static string Lower(string text)
        {
            Plain.Length = 0;
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            bool space = true;
            for (int i = 0; i < text.Length && Plain.Length < Most; i++)
            {
                char c = text[i] == '’' ? '\'' : char.ToLowerInvariant(text[i]);
                if (char.IsWhiteSpace(c))
                {
                    if (!space)
                    {
                        Plain.Append(' ');
                    }
                    space = true;
                    continue;
                }
                Plain.Append(c);
                space = false;
            }
            return Plain.ToString().Trim();
        }

        private static bool HasLetter(string t)
        {
            for (int i = 0; i < t.Length; i++)
            {
                if (char.IsLetter(t[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Any(string t, string[] parts)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (t.IndexOf(parts[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        // A part found as a whole word: "hi" is not in "this".
        private static bool Word(string t, string[] parts)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                int at = t.IndexOf(parts[i], StringComparison.Ordinal);
                while (at >= 0)
                {
                    bool before = at == 0 || !char.IsLetter(t[at - 1]);
                    int end = at + parts[i].Length;
                    bool after = end >= t.Length || !char.IsLetter(t[end]);
                    if (before && after)
                    {
                        return true;
                    }
                    at = t.IndexOf(parts[i], at + 1, StringComparison.Ordinal);
                }
            }
            return false;
        }
    }
}
