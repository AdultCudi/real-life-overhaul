using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>Which of the options a text belongs to (each can be switched off on its own).</summary>
    public enum TextUse
    {
        Replies,    // Flare comments and replies
        Posts,      // Flare posts of the city's people and businesses
        People,     // messages from people: the phone's texts, Flare messages, spoken greetings
        Official,   // the city's accounts and their answers
        Rivals,     // the rival tycoons
        Ledger      // the City Ledger's briefs
    }

    /// <summary>The values a card's lines are filled with, by name ("place", "amount"). A handful at a time; kept and cleared.</summary>
    public sealed class TextSlots
    {
        private readonly string[] _names = new string[12];
        private readonly string[] _values = new string[12];
        private int _count;

        public int Count => _count;

        public TextSlots Set(string name, string value)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_names[i] == name)
                {
                    _values[i] = value;
                    return this;
                }
            }
            if (_count < _names.Length)
            {
                _names[_count] = name;
                _values[_count] = value;
                _count++;
            }
            return this;
        }

        public string Get(string name)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_names[i] == name)
                {
                    return _values[i];
                }
            }
            return null;
        }

        public void Clear()
        {
            for (int i = 0; i < _count; i++)
            {
                _names[i] = _values[i] = null;
            }
            _count = 0;
        }
    }

    /// <summary>
    /// One trained intent: what a character says in one situation, with the card's news, facts, write and must lines as
    /// the training generator made them (data/gen/v2: defs_people, defs_work, defs_flare), its {slots} filled from the
    /// game. A branch tells apart the cards one intent makes for different outcomes the game decided (a yes or a no, on
    /// time or late). Its data version is the training data that has it (2: data/gen/v2; 3: data/gen/v3, the answers to
    /// what the player wrote and the comments under every kind of post): a pack is asked only the intents of its own
    /// data version or older (TextPack.Data, TextsHub.Data). Optional facts (data v5's weather, moment, today's event and
    /// whether the speaker knows the player's name) follow the facts, each left out when its slot has no value, as the
    /// generator leaves them out. Game-free.
    /// </summary>
    public sealed class TextIntent
    {
        public readonly string Key, Branch, Voice, Channel, News, Write;
        public readonly string[] Facts, Must, Optional;
        public readonly TextFace Face;
        public readonly TextUse Use;
        public readonly int MaxLen;
        public readonly int Data;

        public TextIntent(string key, string branch, string voice, string channel, int maxLen, TextFace face, TextUse use, string news,
            string[] facts, string write, string[] must, int data = TextIntents.DataV2, string[] optional = null)
        {
            Data = data;
            Optional = optional ?? Array.Empty<string>();
            Key = key;
            Branch = branch;
            Voice = voice;
            Channel = channel;
            MaxLen = maxLen;
            Face = face;
            Use = use;
            News = news;
            Facts = facts ?? Array.Empty<string>();
            Write = write;
            Must = must ?? Array.Empty<string>();
        }

        /// <summary>True for the voices whose training lines use an exclamation mark now and then (Mom, the partner).</summary>
        public bool Exclaims => Voice == "mom" || Voice == "partner";
    }

    /// <summary>
    /// The intents the mod uses, each mapped from a situation of the game (the map is in TextsGuide and the report), and
    /// the card built for one: the speaker's lines (TextFaces), then news, facts, history, write and must from the
    /// intent's lines with the game's values put in. The slots {first} and {player} come from the speaker; {self} is the
    /// speaker's own name and {company} a tycoon's company. A line of facts or must that names a slot with no value is
    /// left out, as the generator leaves out what a card does not have.
    ///
    /// Proved byte-identical to the generator's own cards for the same values (EconomyCheck, TextsChecks.cs: every
    /// intent here against fixtures the training generator wrote). Game-free. COST: a few short strings a request.
    /// </summary>
    public static partial class TextIntents
    {
        public const int DataV2 = 2;   // the training data of the first packs (data/gen/v2): a pack.txt without a data line
        public const int DataV3 = 3;   // ...and of the retrained packs (data/gen/v3: v2's rows unchanged and 60 new intents)

        private static readonly string[] None = Array.Empty<string>();
        private static readonly List<TextIntent> List = new List<TextIntent>(260);
        private static readonly StringBuilder Fill = new StringBuilder(256);

        /// <summary>The words a plan's kind goes by on a card (world.KIND_WORDS), by the outing's name.</summary>
        public static string OutingWord(string kind)
        {
            switch (kind)
            {
                case "coffee": return "coffee";
                case "meal": return "dinner";
                case "drinks": return "drinks";
                case "cinema": return "a film";
                case "walk": return "a walk";
                case "workout": return "the gym";
                default: return null;
            }
        }

        public static IReadOnlyList<TextIntent> All => List;

        /// <summary>The intent of this key and branch (null for the plain one); null if the mod has no such intent.</summary>
        public static TextIntent Find(string key, string branch = null)
        {
            for (int i = 0; i < List.Count; i++)
            {
                TextIntent t = List[i];
                if (t.Key == key && t.Branch == branch)
                {
                    return t;
                }
            }
            return null;
        }

        /// <summary>
        /// The card of this key and branch a pack of this data version was trained on: of the intents registered for it, the
        /// one of the highest data version not above the pack's (data v5's street cards beside v4's, so a data 4 pack keeps
        /// v4's exact cards); null if the pack has none. COST: a pass over the table.
        /// </summary>
        public static TextIntent Find(string key, string branch, int data)
        {
            TextIntent best = null;
            for (int i = 0; i < List.Count; i++)
            {
                TextIntent t = List[i];
                if (t.Key == key && t.Branch == branch && t.Data <= data && (best == null || t.Data > best.Data))
                {
                    best = t;
                }
            }
            return best;
        }

        /// <summary>
        /// Fills the sheet for this intent: the speaker's lines, the intent's lines with the slots put in, the thread (oldest
        /// first) and lang. False if a slot a line needs has no value (nothing is invented: the written line is used).
        /// </summary>
        public static bool Build(TextIntent intent, TextWho who, TextSlots slots, IReadOnlyList<TextSaid> history, TextSheet sheet)
        {
            sheet.Clear();
            TextFaces.Fill(intent.Face, who, sheet);
            string news = Put(intent.News, who, slots);
            if (news == null)
            {
                return false;
            }
            sheet.News = news;
            for (int i = 0; i < intent.Facts.Length; i++)
            {
                string f = Put(intent.Facts[i], who, slots);
                if (f == null)
                {
                    return false;
                }
                sheet.Facts.Add(f);
            }
            for (int i = 0; i < intent.Optional.Length; i++)
            {
                string f = Put(intent.Optional[i], who, slots);
                if (f != null)
                {
                    sheet.Facts.Add(f);   // (an optional fact whose slot has no value is left out)
                }
            }
            if (history != null)
            {
                for (int i = 0; i < history.Count; i++)
                {
                    sheet.History.Add(new TextSaid(Plain(history[i].Who), Plain(history[i].Said)));   // (a player's typed words among them: one line, no turn markers)
                }
            }
            sheet.Write = intent.Write;
            for (int i = 0; i < intent.Must.Length; i++)
            {
                string m = Put(intent.Must[i], who, slots);
                if (string.IsNullOrEmpty(m))
                {
                    return false;
                }
                sheet.Must.Add(m);
            }
            sheet.Lang = "en";
            return true;
        }

        /// <summary>
        /// A thread's line as a card may carry it: line breaks made spaces, and the prompt's turn markers ("&lt;|", "|&gt;")
        /// taken out, so that nothing typed (a player's own post or message) can end the card's turn or start another.
        /// </summary>
        public static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text ?? string.Empty;
            }
            if (text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0 && text.IndexOf("<|", StringComparison.Ordinal) < 0 && text.IndexOf("|>", StringComparison.Ordinal) < 0)
            {
                return text;
            }
            string plain = text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
            while (plain.IndexOf("<|", StringComparison.Ordinal) >= 0 || plain.IndexOf("|>", StringComparison.Ordinal) >= 0)
            {
                plain = plain.Replace("<|", string.Empty).Replace("|>", string.Empty);
            }
            return plain;
        }

        // A line with its {slots} put in; null if one has no value.
        private static string Put(string line, TextWho who, TextSlots slots)
        {
            if (line == null)
            {
                return null;
            }
            if (line.IndexOf('{') < 0)
            {
                return line;
            }
            Fill.Length = 0;
            int i = 0;
            while (i < line.Length)
            {
                char c = line[i];
                int close = c == '{' ? line.IndexOf('}', i + 1) : -1;
                if (close < 0)
                {
                    Fill.Append(c);
                    i++;
                    continue;
                }
                string name = line.Substring(i + 1, close - i - 1);
                string value;
                switch (name)
                {
                    case "first": value = who.First; break;
                    case "player": value = slots.Get("player") ?? who.Player; break;
                    case "self": value = who.Name; break;
                    case "company": value = slots.Get("company") ?? who.Job; break;
                    default: value = slots.Get(name); break;
                }
                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }
                Fill.Append(value);
                i = close + 1;
            }
            return Fill.ToString();
        }

        // ---------------------------------------------------------------- the table

        private static string[] L(params string[] lines) => lines;

        private static void R(string key, string voice, string channel, int max, TextFace face, TextUse use, string news, string[] facts, string write,
            string[] must = null, string branch = null)
        {
            List.Add(new TextIntent(key, branch, voice, channel, max, face, use, news, facts, write, must));
        }

        // An intent of data v3 (data/gen/v3/defs_v3.py, SPEC_v3.txt).
        private static void R3(string key, string voice, string channel, int max, TextFace face, TextUse use, string news, string[] facts, string write,
            string[] must = null)
        {
            List.Add(new TextIntent(key, null, voice, channel, max, face, use, news, facts, write, must, DataV3));
        }

        private static void Friend(string key, string news, string[] facts, string write, string[] must = null, string branch = null)
        {
            R(key, "friend", "text", 200, TextFace.Friend, TextUse.People, news, facts, write, must, branch);
        }

        static TextIntents()
        {
            // ---- friends (defs_people: make_invite, make_remind, make_cancel, make_stood_up, make_thanks, make_news, make_simple_friend,
            //      make_drift, make_rich, make_arrest, make_fact_friend, make_greet, make_reply_how)
            string[] kinds = { "coffee", "meal", "drinks", "cinema", "walk", "workout" };
            for (int k = 0; k < kinds.Length; k++)
            {
                Friend("friend.invite." + kinds[k], "{first} wants to meet you for {what}; the plan is fixed when you call", L("outing: {what}"), "text: invite");
            }
            Friend("friend.remind", "you have a plan with {first} today: {what} at {place}, {time}; it starts in an hour", L("place: {place}", "time: {time}"),
                "text: reminder", L("{place}", "{time}"));
            Friend("friend.cancel.work", "{first} has called off today's plan ({what} at {place}, {time}); the reason: work kept them late",
                L("plan: {what} at {place}, {time}", "reason: work kept them late"), "text: cancel");
            Friend("friend.cancel.home", "{first} has called off today's plan ({what} at {place}, {time}); the reason: something came up at home",
                L("plan: {what} at {place}, {time}", "reason: something came up at home"), "text: cancel");
            Friend("friend.cancel.ill", "{first} has called off today's plan ({what} at {place}, {time}); the reason: they feel unwell",
                L("plan: {what} at {place}, {time}", "reason: they feel unwell"), "text: cancel");
            Friend("friend.cancel.rain", "{first} has called off today's plan ({what} at {place}, {time}); the reason: rain",
                L("plan: {what} at {place}, {time}", "reason: rain"), "text: cancel");
            Friend("friend.stood_up", "you did not come to {what} at {place}, {time}; {first} waited {waited} minutes and went home",
                L("place: {place}", "waited: {waited} minutes"), "text: stood up", L("{place}"));
            Friend("friend.thanks", "you and {first} just had {what} at {place}; it went well", L("outing: {what} at {place}"), "text: thanks after outing");
            Friend("friend.news.good", "nothing for you to do; {first} is just saying hello", L("their day: {happening}"), "text: small talk");
            Friend("friend.news.bad", "nothing for you to do; {first} is just saying hello", L("their day: {happening}"), "text: small talk");
            Friend("friend.birthday_own", "it is {first}'s birthday today; they hope you call", None, "text: own birthday");
            Friend("friend.birthday_missed", "it was {first}'s birthday yesterday and you did not call", None, "text: birthday forgotten");
            Friend("friend.missed_call", "{first} rang you and you did not answer", None, "text: missed call");
            Friend("friend.ask_loan", "{first} wants to ask you for a loan; they will say how much when you call", None, "text: favour to ask");
            Friend("friend.drift", "you have not been in touch with {first} for {days} days", None, "text: drifting apart");
            Friend("friend.rich", "{first} has heard your business is doing very well, {shop} most of all", L("business: {shop}"), "text: your success");
            Friend("friend.arrest", "{first} heard you were arrested yesterday", None, "text: your arrest");
            Friend("friend.crash", "{first} heard you crashed your car on {street}", L("street: {street}"), "text: your crash", L("{street}"));
            Friend("friend.breakin", "{first} heard your business {shop} was broken into last night", L("business: {shop}"), "text: break-in", L("{shop}"));
            Friend("friend.loan_back", "{first} has paid back the {amount} you lent them; the money is already in your account", L("amount: {amount}"),
                "text: loan repaid", L("{amount}"));
            Friend("friend.loan_excuse", "{first} owes you {amount} and cannot pay it back yet", L("amount: {amount}"), "text: loan late", L("{amount}"));
            Friend("friend.debt", "you borrowed {amount} from {first} a while ago and have not paid it back", L("you owe: {amount}"), "text: asks for their money",
                L("{amount}"));
            Friend("friend.introduce", "{first} wants you to meet their friend {other}; they will bring them over next time you are out", L("friend: {other}"),
                "text: introduce a friend", L("{other}"));
            Friend("friend.lift", "{first} is stranded in {district} and asks you for a lift", L("where: {district}"), "text: asks for a lift", L("{district}"));
            Friend("friend.congrats_open", "you have just opened a new business, {shop}", L("business: {shop}"), "text: congratulations", L("{shop}"));
            R("friend.greet", "friend", "spoken", 140, TextFace.Friend, TextUse.People, "you meet {first} for {what} at {place}; you are on time", None,
                "spoken: greeting", null, "on");
            R("friend.greet", "friend", "spoken", 140, TextFace.Friend, TextUse.People, "you meet {first} for {what} at {place}; you are {late} minutes late", None,
                "spoken: greeting", null, "late");
            Friend("friend.reply_how", "you asked how they are", None, "text: reply");

            // ---- the partner (defs_people.make_partner); the role line is the game's ("dating you", "married to you")
            R("partner.affection", "partner", "text", 200, TextFace.Friend, TextUse.People, "nothing has happened; {first} is just thinking of you", None, "text: affection");
            R("partner.stood_up", "partner", "text", 200, TextFace.Friend, TextUse.People, "you missed your dinner date with {first} at {place}, {time}",
                L("place: {place}", "time: {time}"), "text: stood up", L("{place}"));
            R("partner.missing", "partner", "text", 200, TextFace.Friend, TextUse.People, "you have hardly spent time with {first} for {days} days", None, "text: misses you");
            R("partner.arrest", "partner", "text", 200, TextFace.Friend, TextUse.People, "you were arrested for a minor offence", None, "text: your arrest", null, "minor");
            R("partner.arrest", "partner", "text", 200, TextFace.Friend, TextUse.People, "you were arrested for something serious", None, "text: your arrest", null, "serious");
            R("partner.appointment_missed", "partner", "text", 200, TextFace.Friend, TextUse.People, "you missed the pregnancy scan; {first} went alone", None,
                "text: missed appointment");
            R("partner.wedding_eve", "partner", "text", 200, TextFace.Friend, TextUse.People, "your wedding is tomorrow at {place}", L("venue: {place}"), "text: wedding eve");
            R("partner.anniversary", "partner", "text", 200, TextFace.Friend, TextUse.People, "it is your anniversary with {first} today", None, "text: anniversary");

            // ---- Mom (defs_people.make_mom)
            Mom("mom.news.moved_in", "you have moved in with {name}", L("partner: {name}"), "text: reacts to news");
            Mom("mom.news.engaged", "you are engaged to {name}", L("partner: {name}"), "text: reacts to news");
            Mom("mom.news.married", "you married {name} today", L("partner: {name}"), "text: reacts to news");
            Mom("mom.news.expecting", "you and {name} are expecting a baby", L("partner: {name}"), "text: reacts to news");
            Mom("mom.news.separated", "you and {name} have split up", L("partner: {name}"), "text: reacts to news");
            Mom("mom.news.arrest", "you were arrested", None, "text: reacts to news");
            Mom("mom.news.opened", "you opened a business, {shop}", L("business: {shop}"), "text: reacts to news", L("{shop}"));
            Mom("mom.news.promotion", "you were promoted at work", None, "text: reacts to news");
            Mom("mom.visit", "Mom dropped by while you were out and left {left}", L("left: {left}"), "text: visit", L("{left}"));
            Mom("mom.holiday", "Mom invites you to hers for {holiday}, on {day}", L("holiday: {holiday}", "day: {day}"), "text: invitation", L("{holiday}"));
            Mom("mom.holiday_missed", "you did not come to Mom's for {holiday}", L("holiday: {holiday}"), "text: you missed it", L("{holiday}"));
            Mom("mom.worry", "you have not called Mom for {days} days", None, "text: hasn't heard from you");
            Mom("mom.reply_how", "you asked how she is", None, "text: reply");
            Mom("mom.grandchild_birthday", "it is your child {child}'s birthday, {n} today; Grandma has sent {amount} for a present",
                L("child: {child}", "age: {n}", "gift: {amount}"), "text: birthday gift", L("{child}", "{amount}"));

            // ---- the in-laws (defs_people.make_inlaws): {name} is the partner, their child
            InLaws("inlaws.dinner", "they invite you and {name} to Sunday lunch", L("their child: {name}"));
            InLaws("inlaws.married", "you married their child {name}", L("their child: {name}"));
            InLaws("inlaws.baby", "you and {name} have had a baby", L("their child: {name}"));
            InLaws("inlaws.thanks_visit", "you and {name} visited them yesterday", L("their child: {name}"));
            InLaws("inlaws.birthday", "it is your birthday today", L("their child: {name}"));
            InLaws("inlaws.moved", "you and {name} have moved in together", L("their child: {name}"));
            R("inlaws.holiday", "inlaws", "text", 200, TextFace.InLaws, TextUse.People, "they invite you and {name} to theirs for {holiday}",
                L("their child: {name}", "holiday: {holiday}"), "text: in-laws", L("{holiday}"));

            // ---- roommates (defs_people.make_roommate): the role line is "your roommate"
            Roommate("roommate.rent_late", "{first} cannot pay this week's rent of {amount}; they ask for time until {day}", L("amount: {amount}", "until: {day}"), L("{amount}", "{day}"));
            Roommate("roommate.rent_short", "{first} paid part of the rent and is {amount} short; they ask for time until {day}", L("amount: {amount}", "until: {day}"),
                L("{amount}", "{day}"));
            Roommate("roommate.cooked", "{first} cooked and left a portion in the fridge for you", None, null);
            Roommate("roommate.noise", "{first} had friends round late last night and is sorry for the noise", None, null);
            Roommate("roommate.notice", "{first} is moving out; they will be gone by {day}", L("leaves by: {day}"), L("{day}"));
            Roommate("roommate.borrow", "{first} wants to ask you something when you are home", None, null);
            Roommate("roommate.guest", "{first} has a friend staying over on {day}", L("staying: {day}"), L("{day}"));
            Roommate("roommate.groceries", "{first} is at the shop and asks if you need anything", None, null);

            // ---- the boss (defs_work.make_boss)
            Boss("boss.noshow", "you missed your shift; that is mark {marks} of 3; at 3 you are dismissed", L("marks: {marks} of 3"), "text: no-show", L("{marks}", "3"));
            Boss("boss.review_due", "your performance review is due; come to the office", None, "text: review due");
            Boss("boss.praise", "you worked a good shift today; nothing changes yet", None, "text: praise");
            Boss("boss.late", "you are {late} minutes late for your shift; from an hour late it counts as a mark", L("late by: {late} minutes"), "text: you are late");
            Boss("boss.dismissed", "you missed a third shift; that is mark 3 of 3 and you are dismissed", L("marks: 3 of 3"), "text: dismissal");
            Boss("boss.review_good", "your review went well; your wage goes up to {wage} an hour", L("new wage: {wage} an hour"), "text: review result", L("{wage}"));
            Boss("boss.review_flat", "your review is done; no change to your pay this time", None, "text: review result");
            Boss("boss.shift_change", "your shift on {day} now starts at {time}", L("day: {day}", "start: {time}"), "text: rota change", L("{day}", "{time}"));

            // ---- the lender (defs_work.make_lender)
            Lender("lender.intro", "the lender offers loans from {least} to {most}, today, no forms", L("from: {least}", "up to: {most}"), L("{least}", "{most}"));
            Lender("lender.due", "today the {amount} you owe the lender is due", L("due today: {amount}"), L("{amount}"));
            Lender("lender.warn", "you are {days} day(s) late paying the lender {amount}", L("late by: {days} day(s)", "owed: {amount}"), L("{amount}"));
            Lender("lender.paid", "you paid the lender back {amount} in full", L("paid: {amount}"), L("{amount}"));

            // ---- the city's people on Flare (defs_flare: make_cit_reply, make_cit_post, make_cit_react, make_review, make_dm, make_cit_thread)
            string[] posts = { "opened", "record", "promotion", "selfie", "outing", "moved", "voted", "photo" };
            for (int k = 0; k < posts.Length; k++)
            {
                R("citizen.reply." + posts[k], "citizen", "flare_reply", 140, TextFace.Citizen, TextUse.Replies,
                    "{first} replies to your post; the game chose the reaction: {reaction}", None, "flare reply");
            }
            string[] topics = { "weather", "prices", "crime", "commute", "roadworks", "food", "night out", "sport", "election", "everyday", "rent", "events" };
            for (int k = 0; k < topics.Length; k++)
            {
                R("citizen.post." + topics[k].Replace(' ', '_'), "citizen", "flare_post", 180, TextFace.Citizen, TextUse.Posts,
                    "people in {district} are talking about " + topics[k], L("topic: " + topics[k], "district: {district}"), "flare post");
                R("citizen.thread." + topics[k].Replace(' ', '_'), "citizen", "flare_reply", 140, TextFace.Citizen, TextUse.Replies,
                    "{first} replies to a post by {other} about " + topics[k], L("topic: " + topics[k]), "flare reply");
            }
            React("transit_delay", "City Transit says subway trains are running about {minutes} minutes late after signal problems",
                L("source: City Transit", "delay: about {minutes} minutes"), None);
            React("patrols", "City Police have put extra patrols in {district} after reports of crime", L("source: City Police", "district: {district}"), L("{district}"));
            React("roadworks", "City Office says there are roadworks in {district} this week", L("source: City Office", "district: {district}"), L("{district}"));
            React("outage", "City Utilities warns of short power and water breaks in {district} while engineers work", L("source: City Utilities", "district: {district}"),
                L("{district}"));
            React("cleaning", "street cleaning crews are going round {district} this week", L("source: City Office", "district: {district}"), L("{district}"));
            React("rate_up", "the City Savings Bank is raising its savings and loan rates after the bank rate rose", L("source: City Savings Bank"), None);
            React("rate_down", "the City Savings Bank is cutting its savings and loan rates after the bank rate fell", L("source: City Savings Bank"), None);
            React("prices_up", "the City Ledger reports prices up {change}% in a month", L("source: the City Ledger", "change: up {change}%"), None);
            React("prices_down", "the City Ledger reports prices down {change}% in a month", L("source: the City Ledger", "change: down {change}%"), None);
            React("tycoon_buys", "{company}, owned by {tycoon}, is buying up buildings in {district}", L("who: {tycoon}", "district: {district}"), L("{tycoon}", "{district}"));
            React("tycoon_opens", "{company}, owned by {tycoon}, is opening a business in {district}", L("who: {tycoon}", "district: {district}"), L("{district}"));
            React("tycoon_hiring", "{company}, owned by {tycoon}, is hiring the best staff in town, whoever they work for now", L("who: {tycoon}"), L("{tycoon}"));
            React("new_shop", "a new business, {shop}, has opened in {district}", L("business: {shop}", "district: {district}"), L("{shop}"));
            React("election", "polling day for the city election is coming", L("source: City Office"), None);
            React("district_up", "the City Ledger says incomes in {district} are rising", L("source: the City Ledger", "district: {district}"), L("{district}"));
            React("district_down", "the City Ledger says {district} is having harder times, with incomes falling", L("source: the City Ledger", "district: {district}"),
                L("{district}"));
            React("storm", "City Office warns of a storm tonight", L("source: City Office"), None);
            R("citizen.review", "citizen", "flare_post", 160, TextFace.Citizen, TextUse.Posts, "{first} reviews {shop}: {stars} stars (the game decided the stars)",
                L("business: {shop}", "stars: {stars}"), "flare post: review", L("{shop}"));
            R("citizen.dm.chat", "citizen", "flare_reply", 160, TextFace.CitizenDm, TextUse.People, "they thank you for the kind message", None, "flare dm reply");
            R("citizen.dm.collab", "citizen", "flare_reply", 160, TextFace.CitizenDm, TextUse.People,
                "they are an influencer and are open to working with you; hiring them is done from your Business tab", None, "flare dm reply");
            R("citizen.dm.visit", "citizen", "flare_reply", 160, TextFace.CitizenDm, TextUse.People, "they would like to come by {shop} some time", None, "flare dm reply",
                L("{shop}"));
            R("citizen.dm.thanks_review", "citizen", "flare_reply", 160, TextFace.CitizenDm, TextUse.People, "they are glad you saw their review of {shop}", None,
                "flare dm reply", L("{shop}"));

            // ---- the city's official accounts (defs_flare: make_official, make_answer)
            Official("patrols", "extra police patrols in {district} after reports of crime", L("district: {district}"), L("{district}"));
            Official("roadworks", "roadworks in {district} this week", L("district: {district}"), L("{district}"));
            Official("outage", "engineers are working on the network in {district}; short breaks in power and water are possible", L("district: {district}"), L("{district}"));
            Official("transit", "signal problems; subway delays of about {minutes} minutes", L("delay: about {minutes} minutes"), L("{minutes}"));
            Official("rate_up", "savings and loan rates are going up after the bank rate rose", None, null);
            Official("rate_down", "savings and loan rates are coming down after the bank rate fell", None, null);
            Official("clean", "cleaning crews are in {district} this week", L("district: {district}"), L("{district}"));
            Official("election", "polling day is coming", None, null);
            Official("storm", "a storm is expected tonight", None, null);
            Official("market", "a street market in {district} on {day}", L("district: {district}", "day: {day}"), L("{district}", "{day}"));
            Answer("noted", "the report is noted and will be looked at", null);
            Answer("crew", "a crew is booked for works at {address} from tomorrow, done in about {days} days", L("{address}", "{days}"));
            Answer("patrols", "officers will patrol {district} more often for the next few days", L("{district}"));
            Answer("cleaning", "a cleaning crew will go round {district} this week", L("{district}"));
            Answer("booked", "the crews are fully booked this week and cannot come yet", null);
            Answer("already", "this is on the list already", null);
            Answer("inspector", "an inspector will visit {shop}", L("{shop}"));
            Answer("sorry_wait", "services are running late today and staff are working on it", null);

            // ---- the rival tycoons (defs_flare.make_tycoon): {self} the tycoon, {company} their company
            Tycoon("boast", "nothing new; a boast", None, null, false);
            Tycoon("property", "{company} is buying up buildings in {district}", L("district: {district}"), L("{district}"), false);
            Tycoon("outlet", "{company} is opening a business in {district}, near yours", L("district: {district}"), L("{district}"), false);
            Tycoon("poach", "{company} is hiring the best staff in town, whoever they work for now", None, null, false);
            Tycoon("lobby", "{self} met the council about inspections", None, null, false);
            Tycoon("press", "{self} has a piece about a rival in the City Ledger", None, null, false);
            Tycoon("answer", "replying to your post about {shop}", None, null, true);
            Tycoon("react_record", "{player}'s business {shop} had a record day; {self} reacts", L("business: {shop}", "owner: {player}"), L("{shop}"), false);
            Tycoon("react_rival", "{other} ({other_company}) is buying up buildings in {district}; {self} reacts", L("rival: {other}", "district: {district}"),
                L("{other}"), false);
            Tycoon("react_rate_up", "the bank rate rose; {self} reacts", None, null, false);
            Tycoon("react_rate_down", "the bank rate fell; {self} reacts", None, null, false);
            Tycoon("react_prices", "the City Ledger reports prices up {change}% in a month; {self} reacts", L("change: up {change}%"), null, false);
            Tycoon("react_election", "polling day for the city election is coming; {self} reacts", None, null, false);
            Tycoon("reply_citizen", "a resident mocked {self} on Flare; {self} replies", None, null, true);
            Tycoon("reply_rival", "{other} posted; {self} answers them", None, null, true);

            // ---- business accounts (defs_flare.make_biz): {self} the business
            Biz("post.sale", "two for one today");
            Biz("post.late", "open late tonight");
            Biz("post.hiring", "hiring; ask at the counter");
            Biz("post.new", "new stock in");
            Biz("post.thanks", "a good week");
            Biz("post.rain", "it is raining; the shop is warm and dry");
            Biz("post.closed", "closed today for a staff day");
            R("business.reply.good", "business", "flare_reply", 160, TextFace.Business, TextUse.Posts, "a happy customer reviewed {self}; reply with thanks", None,
                "flare reply: business");
            R("business.reply.bad", "business", "flare_reply", 160, TextFace.Business, TextUse.Posts,
                "an unhappy customer reviewed {self}; reply politely, sorry, no promises of refunds", None, "flare reply: business");

            // ---- the City Ledger's briefs (defs_flare.make_ledger)
            Ledger("prices_up", "prices up {change}% in a month", L("change: {change}%"), L("{change}%"));
            Ledger("prices_down", "prices down {change}% in a month", L("change: {change}%"), L("{change}%"));
            Ledger("rate_up", "the bank rate is to rise {points} points", L("points: {points}"), L("{points}"));
            Ledger("rate_down", "the bank rate is to fall {points} points", L("points: {points}"), L("{points}"));
            Ledger("district_up", "{district} is doing better: incomes rising", L("district: {district}"), L("{district}"));
            Ledger("district_down", "{district} is having harder times: incomes falling", L("district: {district}"), L("{district}"));
            Ledger("new_business", "a new business, {shop}, has opened in {district}", L("business: {shop}", "district: {district}"), L("{shop}"));
            Ledger("rival_buys", "{company} is buying up buildings in {district}", L("company: {company}", "district: {district}"), L("{company}", "{district}"));
            Ledger("election", "polling day for the city election is coming", None, null);
            Ledger("storm", "a storm is expected tonight", None, null);

            V3();
            V4();   // street talk (TextIntents.Street.cs)
            V5();   // ...and its answers to what was said, with the moment and the name (TextIntents.Street.cs)
        }

        // ---------------------------------------------------------------- data v3 (defs_v3.INTENTS): answers to what the player wrote, comments

        /// <summary>The comment kinds under a city person's or an influencer's post (defs_v3.THREAD_TOPIC): the post is in history, its topic on the card.</summary>
        public static readonly string[] ThreadKinds = { "accident", "fire", "market", "policy", "niche_fashion", "niche_tech", "niche_business" };
        public static readonly string[] ThreadTopics = { "an accident", "a fire", "the stock market", "a new council policy", "fashion", "tech", "business" };

        /// <summary>
        /// The comment kinds whose post is told in the news line and the facts (defs_v3.NEWS_KINDS): the kind, its news after
        /// "comments on a post: ", then its facts. History holds only an earlier comment, when one is answered.
        /// </summary>
        public static readonly string[][] NewsKinds =
        {
            new[] { "biz_late", "{shop} posted that it is open late tonight", "business: {shop}" },
            new[] { "biz_hiring", "{shop} posted that it is hiring", "business: {shop}" },
            new[] { "biz_thanks", "{shop} thanked its customers for a good week", "business: {shop}" },
            new[] { "biz_rain", "{shop} posted that it is raining and the shop is warm and dry", "business: {shop}" },
            new[] { "biz_closed", "{shop} posted that it is closed today for a staff day", "business: {shop}" },
            new[] { "brand_news", "{shop}, a city chain, posted news about its shops", "business: {shop}" },
            new[] { "review_good", "{other} reviewed {shop}: {stars} stars", "business: {shop}", "stars: {stars}" },
            new[] { "review_bad", "{other} reviewed {shop}: {stars} stars", "business: {shop}", "stars: {stars}" },
            new[] { "results_up", "{target}, a company on the stock market, posted results better than expected", "company: {target}" },
            new[] { "results_down", "{target}, a company on the stock market, posted results worse than expected", "company: {target}" },
            new[] { "official_notice", "City Office posted a notice about the week's works and a council meeting", "source: City Office", "district: {district}" },
            new[] { "official_advice", "City Police posted advice on keeping homes and shops safe after a break-in in {district}", "source: City Police", "district: {district}" },
            new[] { "official_service", "City Transit posted that the buses are slowed by the weather and the timetable is changing", "source: City Transit" },
            new[] { "official_policy", "City Office posted that a new council policy has come in", "source: City Office" },
            new[] { "tycoon_stake", "{company}, owned by {tycoon}, bought a block of shares in {target} and is pressing its board", "who: {tycoon}", "company: {target}" },
            new[] { "tycoon_bid", "{company}, owned by {tycoon}, made a bid to buy {target}", "who: {tycoon}", "company: {target}" },
            new[] { "tycoon_lobby", "{tycoon} met the council about inspections", "who: {tycoon}" },
            new[] { "tycoon_suit", "{tycoon} is suing {player} in the civil court", "who: {tycoon}", "sued: {player}" },
            new[] { "tycoon_press", "{tycoon} has a piece in the City Ledger attacking a rival's business", "who: {tycoon}" },
            new[] { "tycoon_boast", "{tycoon} posted a boast", "who: {tycoon}" },
            new[] { "mine_endorsed", "{player} endorsed a candidate in the city election", "poster: {player}" },
            new[] { "mine_good_deed", "{player} posted about helping somebody out", "poster: {player}" },
            new[] { "mine_arrested", "{player} posted about being arrested", "poster: {player}" },
            new[] { "mine_sponsored", "{player} posted a sponsored post for their business {shop}", "poster: {player}", "business: {shop}" },
            new[] { "mine_shares", "{player} posted about their shares in {target} after its results", "poster: {player}", "company: {target}" },
            new[] { "mine_issue", "{player} posted about {issue} in {district}", "poster: {player}", "district: {district}" },
            new[] { "mine_campaign", "{player} posted about their run for office", "poster: {player}" },
            new[] { "mine_graduated", "{player} posted that they graduated from the university", "poster: {player}" },
            new[] { "mine_report", "{player} reported {problem} in {district} to the City Office", "poster: {player}", "district: {district}" },
            new[] { "mine_review_good", "{player} reviewed {shop}: {stars} stars", "poster: {player}", "business: {shop}", "stars: {stars}" },
            new[] { "mine_review_bad", "{player} reviewed {shop}: {stars} stars", "poster: {player}", "business: {shop}", "stars: {stars}" },
            new[] { "mine_announce", "{player} announced {what} at their business {shop}", "poster: {player}", "business: {shop}" }
        };

        private static void V3()
        {
            // The answers to a message of the player's (defs_v3.make_dm): the typed message is history's last line.
            R3("citizen.dm.answer", "citizen", "flare_reply", 160, TextFace.CitizenDmMood, TextUse.People, "{first} answers your message", None, "flare dm reply");
            R3("citizen.dm.influencer", "citizen", "flare_reply", 160, TextFace.CitizenDmMood, TextUse.People,
                "{first} is an influencer and answers your message; hiring them is done from your Business tab", None, "flare dm reply");
            R3("friend.dm.answer", "friend", "flare_reply", 160, TextFace.Friend, TextUse.People, "{first} answers your message on Flare", None, "flare dm reply");
            R3("partner.dm.answer", "partner", "flare_reply", 160, TextFace.Friend, TextUse.People, "{first} answers your message on Flare", None, "flare dm reply");
            R3("official.dm.answer", "official", "flare_reply", 160, TextFace.Official, TextUse.Official, "{self} answers your message", None, "flare dm reply");
            R3("business.dm.answer", "business", "flare_reply", 160, TextFace.Business, TextUse.People, "{self} answers your message", None, "flare dm reply");
            R3("tycoon.dm.answer", "tycoon", "flare_reply", 160, TextFace.Tycoon, TextUse.Rivals, "{self} answers your message", None, "flare dm reply");

            // The invitations the game decided (defs_v3.make_invite_decided): the reasons are the training's own words (FlareInviteRules.CardReason).
            R3("citizen.dm.invite_yes", "citizen", "flare_reply", 160, TextFace.CitizenDmMood, TextUse.People, "{first} says yes to your invitation; the plan is booked",
                L("outing: {outing}", "place: {place}", "time: {time}"), "flare dm reply", L("{place}", "{time}"));
            R3("citizen.dm.invite_maybe", "citizen", "flare_reply", 160, TextFace.CitizenDmMood, TextUse.People, "{first} is not sure about your invitation yet",
                L("outing: {outing}", "reason: {reason}"), "flare dm reply");
            R3("citizen.dm.invite_no", "citizen", "flare_reply", 160, TextFace.CitizenDmMood, TextUse.People, "{first} says no to your invitation",
                L("outing: {outing}", "reason: {reason}"), "flare dm reply");

            // Replies and answers to what the player typed in a post or a reply (make_reply_words, make_answer_reply, make_account_answer).
            R3("citizen.reply.words", "citizen", "flare_reply", 140, TextFace.Citizen, TextUse.Replies, "{first} replies to what you wrote in your post", None, "flare reply");
            R3("citizen.answer.reply", "citizen", "flare_reply", 140, TextFace.Citizen, TextUse.Replies, "{first} answers your reply under their post about {topic}", None,
                "flare reply");
            R3("official.answer.said", "official", "flare_reply", 140, TextFace.Official, TextUse.Official, "{self} answers what you wrote to it", None,
                "flare reply: official answer");
            R3("business.answer.said", "business", "flare_reply", 140, TextFace.Business, TextUse.Posts, "{self} answers what you wrote to it", None, "flare reply: business");
            R3("tycoon.answer.said", "tycoon", "flare_reply", 160, TextFace.Tycoon, TextUse.Rivals, "{self} answers what you wrote to them", None, "flare reply: tycoon");

            // Comments on a post's words, and answers to an earlier comment under it (make_comment_pair).
            R3("citizen.comment.words", "citizen", "flare_reply", 140, TextFace.Citizen, TextUse.Replies, "{first} comments on a post by {other}", None, "flare reply");
            R3("citizen.comment.reply", "citizen", "flare_reply", 140, TextFace.Citizen, TextUse.Replies, "{first} answers {other2}'s comment under a post by {other}", None,
                "flare reply");

            // The rivals' moves that had no post (make_typost): RivalMoveKind Stake, Bid and Suit.
            R3("tycoon.stake", "tycoon", "flare_post", 200, TextFace.Tycoon, TextUse.Rivals,
                "{company} has bought a block of shares in {target}, your listed company, and is pressing its board", L("company: {target}"), "flare post: tycoon", L("{target}"));
            R3("tycoon.bid", "tycoon", "flare_post", 200, TextFace.Tycoon, TextUse.Rivals, "{company} has made a bid to buy {target}, your listed company",
                L("company: {target}"), "flare post: tycoon", L("{target}"));
            R3("tycoon.suit", "tycoon", "flare_post", 200, TextFace.Tycoon, TextUse.Rivals, "{self} is suing {player} in the civil court", L("sued: {player}"),
                "flare post: tycoon");

            // The comments under each kind of post (make_comment): a thread kind has the post in history, a news kind in news and facts.
            for (int k = 0; k < ThreadKinds.Length; k++)
            {
                R3("citizen.comment." + ThreadKinds[k], "citizen", "flare_reply", 140, TextFace.Citizen, TextUse.Replies,
                    "{first} comments on a post by {other} about " + ThreadTopics[k], L("topic: " + ThreadTopics[k]), "flare reply");
            }
            R3("citizen.comment.ledger_headline", "citizen", "flare_reply", 140, TextFace.Citizen, TextUse.Replies, "{first} comments on the City Ledger's headline", None,
                "flare reply");
            for (int k = 0; k < NewsKinds.Length; k++)
            {
                string[] n = NewsKinds[k];
                var facts = new string[n.Length - 2];
                Array.Copy(n, 2, facts, 0, facts.Length);
                R3("citizen.comment." + n[0], "citizen", "flare_reply", 140, TextFace.Citizen, TextUse.Replies, "{first} comments on a post: " + n[1], facts, "flare reply");
            }
        }

        private static void Mom(string key, string news, string[] facts, string write, string[] must = null)
        {
            R(key, "mom", "text", 200, TextFace.Mom, TextUse.People, news, facts, write, must);
        }

        private static void InLaws(string key, string news, string[] facts)
        {
            R(key, "inlaws", "text", 200, TextFace.InLaws, TextUse.People, news, facts, "text: in-laws");
        }

        private static void Roommate(string key, string news, string[] facts, string[] must)
        {
            R(key, "roommate", "text", 200, TextFace.Friend, TextUse.People, news, facts, "text: roommate", must);
        }

        private static void Boss(string key, string news, string[] facts, string write, string[] must = null)
        {
            R(key, "boss", "text", 200, TextFace.Boss, TextUse.People, news, facts, write, must);
        }

        private static void Lender(string key, string news, string[] facts, string[] must)
        {
            R(key, "lender", "text", 200, TextFace.Lender, TextUse.People, news, facts, "text: lender", must);
        }

        private static void React(string kind, string news, string[] facts, string[] must)
        {
            R("citizen.react." + kind, "citizen", "flare_post", 180, TextFace.Citizen, TextUse.Posts, news, facts, "flare post: reacting to the news", must);
        }

        private static void Official(string kind, string news, string[] facts, string[] must)
        {
            R("official.post." + kind, "official", "flare_post", 200, TextFace.Official, TextUse.Official, news, facts, "flare post: official", must);
        }

        private static void Answer(string kind, string news, string[] must)
        {
            R("official.answer." + kind, "official", "flare_reply", 200, TextFace.OfficialAnswer, TextUse.Official, news, None, "flare reply: official answer", must);
        }

        private static void Tycoon(string kind, string news, string[] facts, string[] must, bool reply)
        {
            R("tycoon." + kind, "tycoon", reply ? "flare_reply" : "flare_post", 200, TextFace.Tycoon, TextUse.Rivals, news, facts,
                reply ? "flare reply: tycoon" : "flare post: tycoon", must);
        }

        private static void Biz(string kind, string news)
        {
            R("business." + kind, "business", "flare_post", 160, TextFace.Business, TextUse.Posts, news, L("business: {self}"), "flare post: business", L("{self}"));
        }

        private static void Ledger(string kind, string news, string[] facts, string[] must)
        {
            R("ledger." + kind, "ledger", "ledger", 200, TextFace.Ledger, TextUse.Ledger, news, facts, "ledger brief: two sentences", must);
        }
    }
}
