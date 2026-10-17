using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What the game answers an invitation with: the decision is the game's; the words only say it.</summary>
    public enum FlareInviteAnswer { Yes, Maybe, No }

    /// <summary>Why: each reason has its own written line (and its own fact on the card once the model is trained on it).</summary>
    public enum FlareInviteWhy
    {
        Yes,          // booked: the place and the time
        Plan,         // a plan with them is on already (or their yes is on its way): that one stands
        Busy,         // the player has a plan with somebody else, no hour suits within two days, or outings are switched off
        Night,        // their night hours when the message came
        Stranger,     // somebody the player has never met and has hardly talked to
        Later,        // that outing needs them closer than they are (drinks from friends on; a workout with somebody not sporty)
        Nowhere,      // no place for it near the player
        Mood,         // a rough day
        Taken,        // in a relationship with somebody else
        Dislikes,     // they do not think well of the player
        Pass,         // a plain no
        Influencer    // an influencer: they meet people through friends, never from a message
    }

    /// <summary>
    /// An invitation in a Flare message as the game reads it, and what the game decides (FlareInviteRules.Decide): who is
    /// asked, what the social life knows of them (Wiring/FlareInviteLinks fills it), and, for a yes, the place and the time.
    /// Made once per message the player sends to one of the city's people or an influencer; never kept.
    /// </summary>
    public sealed class FlareInviteAsk
    {
        // ---- asked
        public PlanKind Kind;            // the outing read (FlareInviteRules.Read)
        public bool Influencer;          // "i:3"
        public string First, Last;       // a city person's name (the stranger as the social life would meet them)
        public int Sex = -1;             // 1 a woman, 0 a man, -1 not known (FlareInviteRules.SexOf: the person's own, as their card and face have it)
        public int Age;                  // as their cards say it (FlareLiveRules.CitizenAge, as their face has it)
        public int Face;                 // a city person's face's seed (FlareAvatarRules.Citizen): the face they keep when met; 0 not known
        public int Hour;                 // of the message
        public bool NightOwl, Grumpy, Taken;   // a city person's persona and their own life (FlareInviteRules.Citizen)
        public int Chats;                // their messages to the player shown so far (earlier answers, a message they started)
        public int Standing;             // the player's public standing, 0 to 100 (the law's)
        public int Reputation;           // the account's reputation on Flare, 0 to 100
        public long From;                // game minutes: the earliest a plan can start (the yes's arrival and the margin after it)
        public int Roll;                 // 0 to 999, the save's seed and the message's minute

        // ---- what the social life knows (Wiring/FlareInviteLinks)
        public bool Off;                 // the social life or its outings are switched off: nothing can be booked
        public bool Known;               // somebody the player has met (a city person who said yes once is in People)
        public int Cast = -1;            // ...their place in the cast; for a stranger, who they would be when met
        public bool NobodyNew;           // a stranger, and nobody of the social life is left to meet
        public bool PlanWithThem;        // a plan with them is on
        public bool PlanOther;           // the player has a plan with somebody else (one at a time)
        public bool Asleep;              // a person of the cast: their own night hours at the message's hour
        public bool LowMood;             // a rough day (the phone's own rule: half the time they say no)
        public bool Dislikes;            // they remember being stood up, a rude word or money not repaid
        public bool NeedsMore;           // the outing needs them closer (OutingDef.Needs; a workout with somebody not sporty below friend)
        public bool NoPlace, NoSlot;     // no place near the player; no hour within two days

        // ---- a yes: the place and the time (and the plan on now, for the answer that points to it)
        public string Place;             // the business's name as the game shows it
        public string Street;            // the game's own street name and number, for the booking
        public int Number;
        public long PlanAt;              // game minutes
        public string When;              // the day and time in the game's language ("Monday 18:00")
        public string CardWhen;          // ...and as a card says it
        public string Outing;            // the outing in the game's language ("coffee", "a meal")
        public PlanKind PlanKind;        // the plan on now: its kind (Plan)
    }

    /// <summary>A yes's plan as its message keeps it, read back when it is due to be booked (FlareInviteRules.BookOf).</summary>
    public sealed class FlareInviteBook
    {
        public PlanKind Kind;
        public long At;                  // game minutes
        public string Street;            // the place: the game's own street name and number, and its name as shown
        public int Number;
        public string Place;
        public int Cast = -1;            // a person of the social life; -1 somebody new, met online now
        public string First, Last;       // ...their name, sex and age (FlareInviteRules.SexOf, FlareTextsMap.Age)
        public int Sex = -1;
        public int Age;
        public int Face;                 // ...and their face's seed (0 for a yes saved before 0.29.0: the seat's own)
    }

    /// <summary>
    /// Invitations in the player's Flare messages (one accepted becomes a plan, as one made on the phone). The game
    /// decides, the model only words the answer:
    ///
    ///   Read     fixed rules over the typed text, English only: an invitation and its outing (coffee, a meal, drinks, the
    ///            cinema, a walk, a workout; a plain "meet up" or "hang out" is a coffee), robust to typos, slang, capitals
    ///            and emoji. A question about coffee's price is not an invitation.
    ///   Decide   yes, maybe or no by the social life's own rules for a plan made on the phone (a plan already on, the outing
    ///            the stage allows, the place, the hour, a rough day), plus the rules of a message from somebody new below.
    ///            Deterministic: the roll is the save's seed and the message's minute, so a reload answers the same.
    ///   Answer   the answer's written line and its RLO Texts intent by the decision, and its time: a yes comes sooner
    ///            (<see cref="YesLeast"/> to <see cref="YesLeast"/> + <see cref="YesSpread"/> minutes) and the plan starts
    ///            at least <see cref="PlanAfterAnswer"/> minutes after it, so the yes is always read before the plan's
    ///            reminder and its line on the job panel (an hour before) and before the hour at which they may call it off.
    ///
    /// SOMEBODY NEW (a city person the player has never met) says yes only if the player has talked with them a few times
    /// (<see cref="ChatsForYes"/> messages of theirs) or the city thinks well of the player (public standing of
    /// <see cref="StandingForYes"/>, the law's own "liked"), and then three times in four (a grumpy one two in five);
    /// otherwise maybe, or no for one in five and for the grumpy. Reasons: people rarely meet a stranger off an app at the
    /// first message, and do after some talk or when the name is known and liked; a rule the player can read and act on
    /// (talk more, or earn a good name), never a dice roll alone. Someone in a relationship with somebody else, or who does
    /// not think well of the player (a standing or a Flare reputation below <see cref="DislikeBelow"/>), says no. An
    /// influencer says maybe at most: they meet people through friends (a star is introduced by one, FlareLinks.MeetStar).
    ///
    /// Game-free (EconomyCheck, FlareInviteChecks.cs). COST: a pass over the message's characters and a few dozen phrase
    /// looks, once per message the player sends; nothing per frame.
    /// </summary>
    public static class FlareInviteRules
    {
        public const int ChatsForYes = 3;          // messages of theirs before somebody new meets the player
        public const int StandingForYes = 70;      // ...or a public standing this high (the law's "liked", Record.LikedFrom)
        public const int DislikeBelow = 30;        // a standing or a Flare reputation below this: they do not think well of the player
        public const int StrangerYes = 750;        // in thousandths: somebody new who may say yes does, three times in four
        public const int GrumpyYes = 400;          // ...a grumpy one, two in five
        public const int StrangerNo = 200;         // in thousandths: somebody new who may not say yes says no rather than maybe
        public const int MoodNo = 500;             // in thousandths: on a rough day, half the time no (the phone's own odds)
        public const int TakenPercent = 25;        // in hundredths: the city's people in a relationship with somebody else
        public const int AnswerLeast = 30;         // game minutes: an answer comes within half an hour to two hours (as before)
        public const int AnswerSpread = 90;
        public const int YesLeast = 10;            // game minutes: a yes comes sooner, within ten to thirty
        public const int YesSpread = 20;
        public const int PlanAfterAnswer = 120;    // game minutes: the plan starts at least this long after the yes arrives
        public const int PanelBefore = 60;         // game minutes: the plan's reminder and job panel line show this long before (SocialRules.EarlyMinutes)
        public const int CheckBefore = 90;         // game minutes: whether they call it off is rolled this long before (SocialDirector.Plan)
        public const string KeyStart = "reallife_flare_dm_invite_";

        // ---------------------------------------------------------------- reading the invitation

        private static readonly string[] KindWords = { "coffee", "meal", "drinks", "cinema", "walk", "workout" };   // by PlanKind, Coffee to Workout (Drive between is never read)

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
        private static readonly PlanKind[] PhraseKind = { PlanKind.Meal, PlanKind.Meal, PlanKind.Workout, PlanKind.Workout, PlanKind.Workout, PlanKind.Workout, PlanKind.Coffee, PlanKind.Coffee };

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
            " go out with ", " would you go ", " will you go ", " go on a date ", " ask you out ", " asking you out ", " how about we ", " what about we ", " how about a ", " how about some ", " what about a ",
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

        /// <summary>
        /// True if the text invites the reader out, with the outing it names (a plain "meet up" is a coffee). English only;
        /// slang, typos, capitals and emoji are read; a question about the thing itself (its price, whether they like it) is not
        /// an invitation unless a proposal is plainly made too. Builds a few strings: once per message sent.
        /// </summary>
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
            int words = Words_(s);
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

        /// <summary>
        /// The text mended for reading, with a space either side: small letters, accents off, emoji as their words, an
        /// apostrophe dropped ("let's" is "lets"), a question mark a word of its own, a letter said three times said twice
        /// ("coffeeee"), slang and shorthand as words, a typo of an outing mended.
        /// </summary>
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

        // The mended text's word at a place (0 the first), or an empty string.
        private static string Word(string s, int at)
        {
            string[] tokens = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return at < tokens.Length ? tokens[at] : string.Empty;
        }

        // Words in the mended text, the question mark not counted.
        private static int Words_(string s)
        {
            int n = 0;
            string[] tokens = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                n += tokens[i] == "?" ? 0 : 1;
            }
            return n;
        }

        // ---------------------------------------------------------------- the city's people as invitees

        /// <summary>A city person's sex as an invitation meets them: their own, as the city made them (FlareLiveRules.CitizensOf), the one their card and their face have: 1 a woman, 0 a man.</summary>
        public static int SexOf(FlareCitizen c) => c.Female ? 1 : 0;

        /// <summary>A city person's own life as an invitation reads it: in a relationship with somebody else (a quarter of them), stable for the city's seed.</summary>
        public static bool Taken(int citySeed, int index) => FlareRules.Roll(citySeed, index, 1203) % 100 < TakenPercent;

        /// <summary>A city person's night hours: from 23 to 8, a night owl's from 2 to 10 (as a person of the social life's).</summary>
        public static bool Asleep(int hour, bool nightOwl) => nightOwl ? hour >= 2 && hour < 10 : hour >= 23 || hour < 8;

        /// <summary>The roll of an invitation: the save's seed and the message's minute, so a reload answers the same.</summary>
        public static int Roll(int seed, long sent) => FlareRules.Roll(seed, (int)(sent % int.MaxValue), 1201) % 1000;

        // ---------------------------------------------------------------- deciding

        /// <summary>Yes, maybe or no, and why, by the rules in the summary. Everything it reads is on the ask.</summary>
        public static FlareInviteAnswer Decide(FlareInviteAsk a, out FlareInviteWhy why)
        {
            bool stranger = !a.Known && !a.Influencer;
            if (a.Dislikes || (!a.Known && (a.Standing < DislikeBelow || a.Reputation < DislikeBelow)))
            {
                why = FlareInviteWhy.Dislikes;
                return FlareInviteAnswer.No;
            }
            if (stranger && a.Taken)
            {
                why = FlareInviteWhy.Taken;
                return FlareInviteAnswer.No;
            }
            if (a.PlanWithThem)
            {
                why = FlareInviteWhy.Plan;
                return FlareInviteAnswer.Maybe;
            }
            if (a.Influencer)
            {
                why = FlareInviteWhy.Influencer;
                return FlareInviteAnswer.Maybe;
            }
            if (a.Known ? a.Asleep : Asleep(a.Hour, a.NightOwl))
            {
                why = FlareInviteWhy.Night;
                return FlareInviteAnswer.Maybe;
            }
            if (a.Off || a.PlanOther)
            {
                why = FlareInviteWhy.Busy;
                return FlareInviteAnswer.Maybe;
            }
            if (a.Known && a.LowMood && a.Roll < MoodNo)
            {
                why = FlareInviteWhy.Mood;
                return FlareInviteAnswer.No;
            }
            if (stranger)
            {
                if (!MayMeet(a))
                {
                    why = a.Grumpy || a.Roll < StrangerNo ? FlareInviteWhy.Pass : FlareInviteWhy.Stranger;
                    return why == FlareInviteWhy.Pass ? FlareInviteAnswer.No : FlareInviteAnswer.Maybe;
                }
                if (a.NobodyNew || a.Roll >= (a.Grumpy ? GrumpyYes : StrangerYes))
                {
                    why = FlareInviteWhy.Stranger;
                    return FlareInviteAnswer.Maybe;
                }
            }
            if (a.NeedsMore)
            {
                why = FlareInviteWhy.Later;
                return FlareInviteAnswer.Maybe;
            }
            if (a.NoPlace || string.IsNullOrEmpty(a.Place) || string.IsNullOrEmpty(a.Street))
            {
                why = FlareInviteWhy.Nowhere;
                return FlareInviteAnswer.Maybe;
            }
            if (a.NoSlot || a.PlanAt < a.From)
            {
                why = FlareInviteWhy.Busy;
                return FlareInviteAnswer.Maybe;
            }
            why = FlareInviteWhy.Yes;
            return FlareInviteAnswer.Yes;
        }

        /// <summary>Somebody new may meet the player: a few messages of theirs already, or a good name in the city.</summary>
        public static bool MayMeet(FlareInviteAsk a) => a.Chats >= ChatsForYes || a.Standing >= StandingForYes;

        // ---------------------------------------------------------------- the answer

        /// <summary>When the answer comes: a yes within ten to thirty minutes, the rest within half an hour to two hours as before.</summary>
        public static long AnswerAt(long sent, int seed, int dm, bool yes)
        {
            int roll = FlareRules.Roll(seed, dm, 844);   // (the same roll as every answer's: a maybe or a no comes when an answer always came)
            return yes ? sent + YesLeast + roll % YesSpread : sent + AnswerLeast + roll % AnswerSpread;
        }

        /// <summary>The earliest a plan agreed by a yes arriving then can start: its reminder, panel line and call-off roll all after the yes.</summary>
        public static long PlanFrom(long answerAt) => answerAt + PlanAfterAnswer;

        /// <summary>True if a yes arriving then is read before the plan's reminder and its line on the job panel.</summary>
        public static bool BeforePanel(long answerAt, long planAt) => answerAt <= planAt - PanelBefore;

        /// <summary>The written line's key of an answer: a yes by its outing, a maybe or a no by why.</summary>
        public static string Key(FlareInviteAnswer answer, FlareInviteWhy why, PlanKind kind)
        {
            switch (answer)
            {
                case FlareInviteAnswer.Yes:
                    return KeyStart + "yes_" + KindWords[KindIndex(kind)];
                case FlareInviteAnswer.No:
                    return KeyStart + "no_" + (why == FlareInviteWhy.Taken ? "taken" : why == FlareInviteWhy.Dislikes ? "dislikes" : why == FlareInviteWhy.Mood ? "mood" : "pass");
                default:
                    switch (why)
                    {
                        case FlareInviteWhy.Plan: return KeyStart + "maybe_plan";
                        case FlareInviteWhy.Night: return KeyStart + "maybe_night";
                        case FlareInviteWhy.Later: return KeyStart + "maybe_later";
                        case FlareInviteWhy.Nowhere: return KeyStart + "maybe_nowhere";
                        case FlareInviteWhy.Influencer: return KeyStart + "maybe_influencer";
                        case FlareInviteWhy.Stranger: return KeyStart + "maybe_stranger";
                        default: return KeyStart + "maybe_busy";
                    }
            }
        }

        /// <summary>Every written line's key an answer can have, in the locales' order (the checks: each in every language).</summary>
        public static List<string> Keys()
        {
            var keys = new List<string>(20);
            for (int k = 0; k < KindWords.Length; k++)
            {
                keys.Add(KeyStart + "yes_" + KindWords[k]);
            }
            foreach (string m in new[] { "plan", "busy", "night", "stranger", "later", "nowhere", "influencer" })
            {
                keys.Add(KeyStart + "maybe_" + m);
            }
            foreach (string n in new[] { "taken", "dislikes", "mood", "pass" })
            {
                keys.Add(KeyStart + "no_" + n);
            }
            return keys;
        }

        /// <summary>The decision an answer's key says; false for a key that is not an invitation's answer.</summary>
        public static bool AnswerOf(string key, out FlareInviteAnswer answer)
        {
            answer = FlareInviteAnswer.Maybe;
            if (key == null || !key.StartsWith(KeyStart, StringComparison.Ordinal))
            {
                return false;
            }
            string rest = key.Substring(KeyStart.Length);
            if (rest.StartsWith("yes_", StringComparison.Ordinal))
            {
                answer = FlareInviteAnswer.Yes;
                return true;
            }
            if (rest.StartsWith("no_", StringComparison.Ordinal))
            {
                answer = FlareInviteAnswer.No;
                return true;
            }
            return rest.StartsWith("maybe_", StringComparison.Ordinal);
        }

        private static int KindIndex(PlanKind kind)
        {
            switch (kind)
            {
                case PlanKind.Meal: return 1;
                case PlanKind.Drinks: return 2;
                case PlanKind.Cinema: return 3;
                case PlanKind.Walk: return 4;
                case PlanKind.Workout: return 5;
                default: return 0;
            }
        }

        // ---------------------------------------------------------------- the answer's values (FlareDm.Data)

        /// <summary>
        /// The answer's values as a message keeps them ("name=value", separated by '~'): what its written line shows (the
        /// outing, the place and the time in the game's language), the card's (t_outing, t_time, and t_rude for a grudge), and for a yes what the
        /// booking needs (the kind, the minute, the street and number, the person of the social life or -1 for somebody new,
        /// and the city person's name, sex and age). Built once per answer.
        /// </summary>
        public static string Data(FlareInviteAsk a, FlareInviteAnswer answer, FlareInviteWhy why)
        {
            var b = new StringBuilder(160);
            PlanKind kind = why == FlareInviteWhy.Plan ? a.PlanKind : a.Kind;
            Pair(b, "outing", a.Outing);
            Pair(b, "t_outing", CardOuting(kind));
            if (answer == FlareInviteAnswer.Yes || why == FlareInviteWhy.Plan)
            {
                Pair(b, "place", a.Place);
                Pair(b, "time", a.When);
                Pair(b, "t_time", a.CardWhen);
            }
            if (why == FlareInviteWhy.Dislikes && a.Dislikes)
            {
                Pair(b, "t_rude", "1");   // (they remember something the player did: the card's reason, CardReason)
            }
            if (answer == FlareInviteAnswer.Yes)
            {
                Pair(b, "k", ((int)a.Kind).ToString(CultureInfo.InvariantCulture));
                Pair(b, "at", a.PlanAt.ToString(CultureInfo.InvariantCulture));
                Pair(b, "st", a.Street);
                Pair(b, "no", a.Number.ToString(CultureInfo.InvariantCulture));
                Pair(b, "p", (a.Known ? a.Cast : -1).ToString(CultureInfo.InvariantCulture));
                Pair(b, "f", a.First);
                Pair(b, "l", a.Last);
                Pair(b, "sx", a.Sex.ToString(CultureInfo.InvariantCulture));
                Pair(b, "ag", a.Age.ToString(CultureInfo.InvariantCulture));
                if (a.Face > 0)
                {
                    Pair(b, "fc", a.Face.ToString(CultureInfo.InvariantCulture));
                }
            }
            return b.ToString();
        }

        private static void Pair(StringBuilder b, string name, string value)
        {
            if (b.Length > 0)
            {
                b.Append('~');
            }
            b.Append(name).Append('=').Append(Safe(value));
        }

        /// <summary>A value as a message's data can keep it: no '~' or '=' (a name with one has a space instead), one line.</summary>
        public static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            return value.Replace('~', ' ').Replace('=', ' ').Replace('\n', ' ').Replace('\r', ' ');
        }

        /// <summary>One value of a message's data, or null.</summary>
        public static string Value(string data, string name)
        {
            if (string.IsNullOrEmpty(data))
            {
                return null;
            }
            string[] pairs = data.Split('~');
            for (int i = 0; i < pairs.Length; i++)
            {
                int eq = pairs[i].IndexOf('=');
                if (eq == name.Length && string.CompareOrdinal(pairs[i], 0, name, 0, eq) == 0)
                {
                    return pairs[i].Substring(eq + 1);
                }
            }
            return null;
        }

        /// <summary>A yes's plan read back from its message's data; null if a value it needs is missing or not a number.</summary>
        public static FlareInviteBook BookOf(FlareDm m)
        {
            string data = m != null ? m.Data : null;
            if (!int.TryParse(Value(data, "k"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int kind) || kind < (int)PlanKind.Coffee || kind > (int)PlanKind.Workout
                || !long.TryParse(Value(data, "at"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long at)
                || !int.TryParse(Value(data, "no"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
                || !int.TryParse(Value(data, "p"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cast) || string.IsNullOrEmpty(Value(data, "st")))
            {
                return null;
            }
            int.TryParse(Value(data, "sx"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int sex);
            int.TryParse(Value(data, "ag"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int age);
            int.TryParse(Value(data, "fc"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int face);
            return new FlareInviteBook
            {
                Kind = (PlanKind)kind, At = at, Street = Value(data, "st"), Number = number, Place = Value(data, "place"), Cast = cast,
                First = Value(data, "f"), Last = Value(data, "l"), Sex = sex, Age = age, Face = Math.Max(0, face)
            };
        }

        /// <summary>The mark a yes gets once its plan is booked, so that it is never booked twice (kept in the save with the message).</summary>
        public const string BookedMark = "~b=1";

        /// <summary>True if a yes's plan has been booked (or the yes was turned into a maybe), never to be booked again.</summary>
        public static bool Booked(FlareDm m) => m != null && m.Data != null && m.Data.EndsWith(BookedMark, StringComparison.Ordinal);

        /// <summary>True for a yes still to be booked: the city's answer, its key a yes, not marked booked.</summary>
        public static bool Waiting(FlareDm m)
        {
            return m != null && !m.Mine && AnswerOf(m.Key, out FlareInviteAnswer a) && a == FlareInviteAnswer.Yes && !Booked(m);
        }

        /// <summary>The earliest minute at which a yes is booked (a little before it shows), or long.MaxValue for none.</summary>
        public static long Due(List<FlareDm> dms)
        {
            long due = long.MaxValue;
            for (int i = 0; dms != null && i < dms.Count; i++)
            {
                if (Waiting(dms[i]) && dms[i].At - BookAhead < due)
                {
                    due = dms[i].At - BookAhead;
                }
            }
            return due;
        }

        public const int BookAhead = 2;   // game minutes: a yes is booked this long before it shows (the timer looks once a second)

        /// <summary>A yes still to be booked with this account, or null (a second invitation then points to it).</summary>
        public static FlareDm WaitingWith(List<FlareDm> dms, string with)
        {
            for (int i = 0; dms != null && i < dms.Count; i++)
            {
                if (Waiting(dms[i]) && (with == null || dms[i].With == with))
                {
                    return dms[i];
                }
            }
            return null;
        }

        /// <summary>
        /// A yes that could not be booked when it was due (a plan made meanwhile by phone, the place gone, the hour past) is
        /// turned into "maybe, busy" before it shows: its key changes and it moves a minute on, so that words asked for the yes
        /// (kept under its old minute) are never shown with it. Marked so that it is not looked at again.
        /// </summary>
        public static void Unbook(FlareDm m)
        {
            m.Key = KeyStart + "maybe_busy";
            m.At += 1;
            m.Data = (m.Data ?? string.Empty) + BookedMark;
        }

        /// <summary>The messages of the account to the player shown by now: how much they have talked (an invitation's own answer not yet among them).</summary>
        public static int Chats(List<FlareDm> dms, string with, long now)
        {
            int n = 0;
            for (int i = 0; dms != null && i < dms.Count; i++)
            {
                FlareDm m = dms[i];
                n += !m.Mine && m.With == with && m.At <= now ? 1 : 0;
            }
            return n;
        }

        // ---------------------------------------------------------------- RLO Texts

        /// <summary>The outing as an invitation's card says it (as data v3 has it): coffee, dinner, drinks, the cinema, a walk, a workout.</summary>
        public static string CardOuting(PlanKind kind)
        {
            switch (kind)
            {
                case PlanKind.Meal: return "dinner";
                case PlanKind.Drinks: return "drinks";
                case PlanKind.Cinema: return "the cinema";
                case PlanKind.Walk: return "a walk";
                case PlanKind.Workout: return "a workout";
                default: return "coffee";
            }
        }

        /// <summary>The intent of an answer by its key: citizen.dm.invite_yes, _maybe or _no (an influencer is worded as a city person: data v3 has no influencer's); null for another key.</summary>
        public static string Intent(string key)
        {
            if (!AnswerOf(key, out FlareInviteAnswer a))
            {
                return null;
            }
            return "citizen.dm.invite_" + (a == FlareInviteAnswer.Yes ? "yes" : a == FlareInviteAnswer.No ? "no" : "maybe");
        }

        /// <summary>
        /// The intent of an answer if the pack in use is trained on it (data v3 and later); null otherwise, and the answer keeps
        /// its written line. A maybe because a plan with them is on already keeps its written line too: it names that plan's
        /// place and time, which the maybe card has no line for.
        /// </summary>
        public static string TrainedIntent(string key, int data)
        {
            return data >= TextIntents.DataV3 && key != KeyStart + "maybe_plan" ? Intent(key) : null;
        }

        // The reasons as data v3 learnt them (defs_v3.REASONS), word for word.
        public const string ReasonBarely = "they barely know you", ReasonBusy = "they are busy at the moment";
        public const string ReasonTaken = "they are seeing someone", ReasonNot = "they are not interested", ReasonRude = "you were rude to them before", ReasonTooBusy = "they are too busy";

        /// <summary>
        /// The reason on a maybe's or a no's card, by the answer's key (and, for a no because they do not think well of the
        /// player, whether they remember something the player did: the message's data says so): a maybe because they hardly
        /// know the player, need to know them better or are an influencer is "they barely know you"; at night, busy or with
        /// nowhere near "they are busy at the moment"; a no for somebody else "they are seeing someone", for a rough day
        /// "they are too busy", for a grudge "you were rude to them before", for a low name or a plain no "they are not
        /// interested". Null for a yes, the plan on already, or another key.
        /// </summary>
        public static string CardReason(string key, string data)
        {
            if (key == null || !key.StartsWith(KeyStart, StringComparison.Ordinal))
            {
                return null;
            }
            switch (key.Substring(KeyStart.Length))
            {
                case "maybe_stranger":
                case "maybe_later":
                case "maybe_influencer": return ReasonBarely;
                case "maybe_night":
                case "maybe_busy":
                case "maybe_nowhere": return ReasonBusy;
                case "no_taken": return ReasonTaken;
                case "no_mood": return ReasonTooBusy;
                case "no_dislikes": return Value(data, "t_rude") == "1" ? ReasonRude : ReasonNot;
                case "no_pass": return ReasonNot;
                default: return null;
            }
        }

        /// <summary>
        /// A city person's mood on an invitation's card: their mood of the day, except that a yes is never given on a low
        /// day (it reads "fine" then: the training's yes has fine, a good mood or a great day) and a no for a rough day says
        /// so ("having a rough day").
        /// </summary>
        public static string CardMood(string key, string dayMood)
        {
            if (AnswerOf(key, out FlareInviteAnswer a) && a == FlareInviteAnswer.Yes)
            {
                return dayMood == TextWords.Moods[0] || dayMood == TextWords.Moods[1] ? TextWords.Moods[2] : dayMood;
            }
            return key == KeyStart + "no_mood" ? TextWords.Moods[0] : dayMood;
        }

        /// <summary>The card's values of an answer from its message's data: the outing, for a yes the place and the time, for a maybe or a no the reason.</summary>
        public static void Slots(FlareDm m, TextSlots slots)
        {
            slots.Set("outing", Value(m.Data, "t_outing"));
            string place = Value(m.Data, "place"), when = Value(m.Data, "t_time"), reason = CardReason(m.Key, m.Data);
            if (!string.IsNullOrEmpty(place))
            {
                slots.Set("place", place);
            }
            if (!string.IsNullOrEmpty(when))
            {
                slots.Set("time", when);
            }
            if (reason != null)
            {
                slots.Set("reason", reason);
            }
        }
    }
}
