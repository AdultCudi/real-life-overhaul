using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Flare
{
    /// <summary>Flare's invitations, the reading: whether a typed message invites the reader out, and to what.</summary>
    public static partial class FlareInviteRules
    {
        // The outing's words, after the slang and typos are mended (one word each; phrases below).
        private static readonly Dictionary<string, PlanKind> Words = new Dictionary<string, PlanKind>(StringComparer.Ordinal)
        {
            { "coffee", PlanKind.Coffee }, { "coffees", PlanKind.Coffee }, { "cafe", PlanKind.Coffee }, { "latte", PlanKind.Coffee }, { "lattes", PlanKind.Coffee },
            { "espresso", PlanKind.Coffee }, { "cappuccino", PlanKind.Coffee }, { "cuppa", PlanKind.Coffee }, { "tea", PlanKind.Coffee }, { "coffeeshop", PlanKind.Coffee },
            { "dinner", PlanKind.Meal }, { "lunch", PlanKind.Meal }, { "brunch", PlanKind.Meal }, { "breakfast", PlanKind.Meal }, { "food", PlanKind.Meal },
            { "meal", PlanKind.Meal }, { "eat", PlanKind.Meal }, { "pizza", PlanKind.Meal }, { "burger", PlanKind.Meal }, { "burgers", PlanKind.Meal },
            { "sushi", PlanKind.Meal }, { "tacos", PlanKind.Meal }, { "ramen", PlanKind.Meal }, { "curry", PlanKind.Meal }, { "supper", PlanKind.Meal },
            { "takeaway", PlanKind.Meal }, { "restaurant", PlanKind.Meal },
            { "drink", PlanKind.Drinks }, { "drinks", PlanKind.Drinks }, { "beer", PlanKind.Drinks }, { "beers", PlanKind.Drinks }, { "pint", PlanKind.Drinks },
            { "pints", PlanKind.Drinks }, { "pub", PlanKind.Drinks }, { "bar", PlanKind.Drinks }, { "wine", PlanKind.Drinks }, { "cocktail", PlanKind.Drinks },
            { "cocktails", PlanKind.Drinks }, { "bevvy", PlanKind.Drinks }, { "bevvies", PlanKind.Drinks }, { "booze", PlanKind.Drinks },
            { "cinema", PlanKind.Cinema }, { "movie", PlanKind.Cinema }, { "movies", PlanKind.Cinema }, { "film", PlanKind.Cinema }, { "films", PlanKind.Cinema },
            { "flick", PlanKind.Cinema }, { "flicks", PlanKind.Cinema },
            { "walk", PlanKind.Walk }, { "walks", PlanKind.Walk }, { "stroll", PlanKind.Walk }, { "wander", PlanKind.Walk }, { "hike", PlanKind.Walk }, { "park", PlanKind.Walk },
            { "gym", PlanKind.Workout }, { "workout", PlanKind.Workout }, { "exercise", PlanKind.Workout }, { "jog", PlanKind.Workout }, { "yoga", PlanKind.Workout },
            { "swim", PlanKind.Workout }, { "training", PlanKind.Workout }
        };

        // The outing's phrases, on the mended text with a space either side; first match by position wins with the words.
        private static readonly string[] PhraseText = { " a bite ", " bite to eat ", " work out ", " for a run ", " go running ", " go jogging ", " flat white ", " pick me up " };
        private static readonly PlanKind[] PhraseKind =
        {
            PlanKind.Meal, PlanKind.Meal, PlanKind.Workout, PlanKind.Workout, PlanKind.Workout, PlanKind.Workout, PlanKind.Coffee, PlanKind.Coffee
        };

        // Meeting with no outing named: a coffee.
        private static readonly string[] Meet =
        {
            " meet up ", " meetup ", " meeting up ", " meet me ", " meet you ", " meet for ", " meet sometime ", " meet irl ", " meet in person ", " hang out ", " hangout ",
            " hang sometime ", " want to hang ", " catch up ", " get together ", " link up ", " want to chill ", " chill sometime ", " chill together ",
            " go out with ", " go out sometime ", " go out tonight ", " go out for ", " a date ", " on a date ", " date night ", " see each other ", " meet "
        };

        // Proposals that make it an invitation whatever else the message says.
        private static readonly string[] Strong =
        {
            " lets ", " let us ", " fancy ", " up for ", " down for ", " join me ", " my treat ", " on me ", " take you out ", " take you for ",
            " take you to ", " buy you ", " shall we ", " should we ", " we should ", " we could ", " could we ", " can we ", " want to grab ", " want to get ",
            " want to go ", " want to meet ", " want to hang ", " want to come ", " want to catch ", " want to hit ", " want to do ", " want to join ",
            " want to have ", " would you like ", " would you want ", " would you be ", " do you want ", " you want to ", " you want a ", " you want some ",
            " care to ", " care for ", " you free ", " are you free ", " you around ", " be free ", " free later ", " free tonight ", " free tomorrow ",
            " free this ", " free on ", " free for ", " meet up ", " meet me ", " meet you ", " hang out ", " catch up ", " get together ", " link up ",
            " go out with ", " would you go ", " will you go ", " go on a date ", " ask you out ", " asking you out ", " how about we ", " what about we ",
            " how about a ", " how about some ", " what about a ",
            " what about some ", " can i take ", " can i buy ", " invite you ", " inviting you ", " come with ", " come along ", " come for ", " join us ",
            " you in ? ", " keen ", " interested in ", " like to go ", " like to grab ", " like to get ", " like to meet ", " like to join ", " maybe we "
        };

        // Suggestions that count when nothing says otherwise and it is put as a question or opens with them.
        private static readonly string[] Weak =
        {
            " how about ", " what about ", " grab ", " go for ", " get a ", " get some ", " tonight ", " tomorrow ", " later ", " today ", " weekend ",
            " after work ", " soon ", " sometime ", " some time ", " want ", " one day ", " this evening ", " this afternoon ", " this morning ",
            " together ", " with me "
        };

        // Things said about coffee rather than to drink one: a price, a review, what somebody did, a question about the thing.
        private static readonly string[] Negative =
        {
            " how much ", " price ", " prices ", " pricey ", " cost ", " costs ", " costing ", " expensive ", " cheap ", " $ ", " dollar ", " dollars ",
            " bucks ", " sell ", " sells ", " selling ", " sold ", " review ", " reviews ", " recommend ", " recommendation ", " recommendations ",
            " favourite ", " favorite ", " best ", " do you like ", " you like ", " did you ", " have you ", " had a ", " i had ", " i went ", " went to ",
            " went for ", " yesterday ", " last night ", " where is ", " where do ", " where can ", " where to ", " wheres ", " what is ", " whats ",
            " which ", " menu ", " open ", " opening ", " close ", " closing ", " hours ", " free coffee ", " free drinks ", " free food ", " free pizza ",
            " hate ", " addicted ", " i need ", " im at ", " i am at ", " im having ", " having a ", " drinking ", " eating ", " watching ", " watched ",
            " ill ", " i will ", " im going ", " how was ", " how is ", " hows ", " how did ", " is it ", " is the "
        };

        // Phrases taken out before anything is read: they name a meeting or a wish without being an invitation.
        private static readonly string[] Veto =
        {
            " nice to meet you ", " nice meeting you ", " good to meet you ", " great to meet you ", " pleased to meet you ", " lovely to meet you ", " want to know ",
            " want to hear ", " up to date ", " to date ", " hang on ", " walk of life "
        };

        // A question opening with these is about the thing, not an invitation (unless a proposal says otherwise).
        private static readonly string[] Asking = { "is", "was", "were", "did", "does", "has", "have", "why", "which", "who", "what", "where", "when", "how" };

        // Opening words of a suggestion put without a question mark ("grab a coffee later", "drinks sometime").
        private static readonly string[] Opening = { "grab", "get", "go", "come", "join", "meet", "hang", "catch", "lets", "how", "what" };

        // Slang and shorthand, each to its words.
        private static readonly Dictionary<string, string> Slang = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "u", "you" }, { "ya", "you" }, { "yu", "you" }, { "yuo", "you" }, { "yall", "you" }, { "r", "are" }, { "ur", "you are" }, { "youre", "you are" },
            { "wanna", "want to" }, { "wana", "want to" }, { "wnna", "want to" }, { "wan", "want" }, { "wanted", "want" }, { "gonna", "going to" },
            { "gotta", "got to" }, { "lemme", "let me" }, { "lets", "lets" }, { "letz", "lets" }, { "shal", "shall" }, { "wud", "would" }, { "wld", "would" },
            { "woud", "would" }, { "cud", "could" }, { "cn", "can" }, { "tmrw", "tomorrow" }, { "tmr", "tomorrow" }, { "tmrrw", "tomorrow" }, { "tomoz", "tomorrow" },
            { "2moro", "tomorrow" }, { "2morrow", "tomorrow" }, { "tomorow", "tomorrow" }, { "tommorow", "tomorrow" }, { "2nite", "tonight" }, { "tonite", "tonight" },
            { "2night", "tonight" }, { "l8r", "later" }, { "latr", "later" }, { "l8", "later" }, { "sumtime", "sometime" }, { "smtime", "sometime" },
            { "bout", "about" }, { "abt", "about" }, { "w", "with" }, { "wit", "with" }, { "wiv", "with" }, { "rn", "now" }, { "hangout", "hang out" },
            { "meetup", "meet up" }, { "catchup", "catch up" }, { "linkup", "link up" }, { "workout", "workout" }, { "wrkout", "workout" },
            { "cofe", "coffee" }, { "cofee", "coffee" }, { "coffe", "coffee" }, { "coffie", "coffee" }, { "cofie", "coffee" }, { "cawfee", "coffee" },
            { "kofi", "coffee" }, { "coffees", "coffees" }, { "caffe", "cafe" }, { "coffeee", "coffee" }, { "starbs", "coffee" },
            { "dinr", "dinner" }, { "dinna", "dinner" }, { "diner", "dinner" }, { "dindin", "dinner" }, { "luch", "lunch" }, { "lnch", "lunch" },
            { "brekkie", "breakfast" }, { "brekky", "breakfast" }, { "brekfast", "breakfast" }, { "nosh", "food" }, { "grub", "food" }, { "eats", "eat" },
            { "drnk", "drink" }, { "drnks", "drinks" }, { "drinkz", "drinks" }, { "drinky", "drinks" }, { "bevs", "drinks" }, { "brewski", "beer" },
            { "brewskis", "beers" }, { "cinama", "cinema" }, { "cinma", "cinema" }, { "cimena", "cinema" }, { "moive", "movie" }, { "mvoie", "movie" },
            { "moviee", "movie" }, { "flim", "film" }, { "wlak", "walk" }, { "wak", "walk" }, { "gim", "gym" }, { "jym", "gym" }, { "gymm", "gym" },
            { "fancyy", "fancy" }, { "fncy", "fancy" }, { "fanc", "fancy" }, { "grb", "grab" }, { "gra", "grab" }, { "togther", "together" },
            { "togeter", "together" }, { "2gether", "together" }, { "pls", "please" }, { "plz", "please" }, { "whatabout", "what about" },
            { "howabout", "how about" }, { "sometimes", "sometime" }
        };

        // The words a typo of which is mended (one letter wrong, missing, extra or swapped, never the first; words of five letters or more).
        private static readonly string[] Fuzzy = { "coffee", "dinner", "drinks", "cinema", "movies", "breakfast", "workout", "together", "tomorrow", "tonight", "brunch", "fancy" };

        // Real words a letter away from those, never mended into them.
        private static readonly HashSet<string> RealWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "branch", "winner", "dinners", "thinks", "drunks", "fancies", "tonights", "cinemas", "workouts", "coffees", "moves", "movers", "drinker", "brunches", "toffee"
        };

        /// <summary>True if the text invites the reader out, with the outing it names (a plain "meet up" is a coffee); once a message sent.</summary>
        public static bool Read(string text, out PlanKind kind)
        {
            kind = PlanKind.Coffee;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string s = Mend(text);
            for (int i = 0; i < Veto.Length; i++)
            {
                s = s.Replace(Veto[i], " ");
            }
            if (s.Trim().Length == 0)
            {
                return false;
            }
            if (!Outing(s, out kind))
            {
                return false;
            }
            bool question = s.IndexOf(" ? ", StringComparison.Ordinal) >= 0;
            if (Any(s, Strong))
            {
                return true;
            }
            string first = Word(s, 0);
            if (Any(s, Negative) || (question && Array.IndexOf(Asking, first) >= 0 && Word(s, 1) != "about"))
            {
                return false;
            }
            int words = WordCount(s);
            if (question && words <= 4)
            {
                return true;   // "coffee?", "drinks later?", "pizza tonight?"
            }
            bool weak = Any(s, Weak);
            bool opens = Array.IndexOf(Opening, first) >= 0 || Words.ContainsKey(first);
            return weak && (question || opens);
        }

        /// <summary>The outing named first in a mended text, or a plain meeting (a coffee). False if none is named.</summary>
        private static bool Outing(string s, out PlanKind kind)
        {
            kind = PlanKind.Coffee;
            int best = int.MaxValue;
            int at = 1;
            while (at < s.Length)
            {
                int end = s.IndexOf(' ', at);
                if (end < 0)
                {
                    break;
                }
                if (end > at && Words.TryGetValue(s.Substring(at, end - at), out PlanKind k))
                {
                    best = at;
                    kind = k;
                    break;
                }
                at = end + 1;
            }
            for (int i = 0; i < PhraseText.Length; i++)
            {
                int p = s.IndexOf(PhraseText[i], StringComparison.Ordinal);
                if (p >= 0 && p + 1 < best)
                {
                    best = p + 1;
                    kind = PhraseKind[i];
                }
            }
            if (best != int.MaxValue)
            {
                return true;
            }
            if (Any(s, Meet))
            {
                kind = PlanKind.Coffee;
                return true;
            }
            return false;
        }

        /// <summary>The text mended for reading, a space either side: lower case, no accents or apostrophes, emoji and slang as words, typos mended.</summary>
        public static string Mend(string text)
        {
            var b = new StringBuilder(text.Length + 16);
            b.Append(' ');
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsHighSurrogate(c) && i + 1 < text.Length)
                {
                    int code = char.ConvertToUtf32(c, text[i + 1]);
                    i++;
                    b.Append(' ').Append(Emoji(code)).Append(' ');
                    continue;
                }
                if (c == '\u2615')
                {
                    b.Append(" coffee ");   // the hot beverage sign
                    continue;
                }
                if (c == '\'' || c == '\u2019' || c == '`' || c == '\u2018')
                {
                    continue;
                }
                if (c == '?')
                {
                    b.Append(" ? ");
                    continue;
                }
                if (c == '$' || c == '\u00a3' || c == '\u20ac')
                {
                    b.Append(" $ ");
                    continue;
                }
                if (c == '/' && b.Length >= 2 && b[b.Length - 1] == 'w' && b[b.Length - 2] == ' ')
                {
                    b.Append("ith ");   // "w/" is "with"
                    continue;
                }
                c = Plain(char.ToLowerInvariant(c));
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    int n = b.Length;
                    if (c >= 'a' && n >= 2 && b[n - 1] == c && b[n - 2] == c)
                    {
                        continue;   // "coffeeee": no letter three times in a row in English
                    }
                    b.Append(c);
                }
                else
                {
                    b.Append(' ');
                }
            }
            b.Append(' ');
            string[] tokens = b.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var o = new StringBuilder(b.Length + 16);
            o.Append(' ');
            for (int i = 0; i < tokens.Length; i++)
            {
                string t = tokens[i];
                if (Slang.TryGetValue(t, out string said))
                {
                    t = said;
                }
                else if (!Words.ContainsKey(t))
                {
                    t = Mended(t);
                }
                o.Append(t).Append(' ');
            }
            return o.ToString();
        }

        // An emoji as the word it stands for; nothing for the rest.
        private static string Emoji(int code)
        {
            switch (code)
            {
                case 0x1F37A: case 0x1F37B: case 0x1F377: case 0x1F378: case 0x1F379: case 0x1F942: case 0x1F943: return "drinks";
                case 0x1F355: case 0x1F354: case 0x1F35D: case 0x1F35C: case 0x1F37D: case 0x1F363: case 0x1F32E: case 0x1F35B: case 0x1F957: return "food";
                case 0x1F3AC: case 0x1F37F: case 0x1F3A5: case 0x1F39E: return "movie";
                case 0x1F6B6: case 0x1F333: case 0x1F332: return "walk";
                case 0x1F3CB: case 0x1F4AA: case 0x1F3C3: return "gym";
                case 0x1F375: return "coffee";
                default: return string.Empty;
            }
        }

        // A few letters with accents as English writes them.
        private static char Plain(char c)
        {
            switch (c)
            {
                case '\u00e9': case '\u00e8': case '\u00ea': case '\u00eb': return 'e';
                case '\u00e1': case '\u00e0': case '\u00e2': case '\u00e4': return 'a';
                case '\u00ed': case '\u00ee': case '\u00ef': return 'i';
                case '\u00f3': case '\u00f4': case '\u00f6': return 'o';
                case '\u00fa': case '\u00fb': case '\u00fc': return 'u';
                case '\u00f1': return 'n';
                case '\u00e7': return 'c';
                default: return c;
            }
        }

        // A word a letter away from one of the outings' and invitations' own (never the first letter, five letters or more), as that word.
        private static string Mended(string t)
        {
            if (t.Length < 5 || RealWords.Contains(t))
            {
                return t;
            }
            for (int i = 0; i < Fuzzy.Length; i++)
            {
                if (OneOff(t, Fuzzy[i]))
                {
                    return Fuzzy[i];
                }
            }
            return t;
        }

        /// <summary>True if a is b with one letter changed, left out, added or swapped with the next, never the first letter.</summary>
        public static bool OneOff(string a, string b)
        {
            if (a == b || a.Length == 0 || b.Length == 0 || a[0] != b[0] || Math.Abs(a.Length - b.Length) > 1)
            {
                return false;
            }
            if (a.Length == b.Length)
            {
                int diff = -1, count = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    if (a[i] != b[i])
                    {
                        if (count == 0)
                        {
                            diff = i;
                        }
                        count++;
                    }
                }
                if (count == 1)
                {
                    return a.Length >= 6;   // a letter changed: only in longer words ("cinama"), never "drunk" for "drink"
                }
                return count == 2 && diff + 1 < a.Length && a[diff] == b[diff + 1] && a[diff + 1] == b[diff] && Same(a, b, diff + 2);
            }
            string longer = a.Length > b.Length ? a : b, shorter = a.Length > b.Length ? b : a;
            int k = 0;
            while (k < shorter.Length && shorter[k] == longer[k])
            {
                k++;
            }
            for (int i = k; i < shorter.Length; i++)
            {
                if (shorter[i] != longer[i + 1])
                {
                    return false;
                }
            }
            return true;
        }

        private static bool Same(string a, string b, int from)
        {
            for (int i = from; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static bool Any(string s, string[] phrases)
        {
            for (int i = 0; i < phrases.Length; i++)
            {
                if (s.IndexOf(phrases[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static string Word(string s, int at)
        {
            string[] tokens = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return at < tokens.Length ? tokens[at] : string.Empty;
        }

        private static int WordCount(string s)
        {
            int n = 0;
            string[] tokens = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                n += tokens[i] == "?" ? 0 : 1;
            }
            return n;
        }
    }
}
