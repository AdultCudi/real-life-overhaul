using System;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// The intents of data v6 (the contract SPEC_v6.json is written from this table by EconomyCheck's --texts-spec):
    /// every older intent again at its latest card, in the game's language, with what the speaker remembers, and the
    /// city's new talk (the staff, the people of the player's life, gossip, reviews). An older pack is never asked them.
    /// Game-free. COST: none until asked.
    /// </summary>
    public static partial class TextIntents
    {
        public const int DataV6 = 6;   // ...and of the contract of data/gen/v6

        // The street's facts of data v6: whether they know the name, always after the branch's own; the moment where v5 had it.
        private static readonly string[] KnownSix = { "you know their name: {known}" };
        private static readonly string[] MomentSix = { "weather: {weather}", "moment: {moment}", "today: {event}" };

        /// <summary>The kinds of the gossip branches (friend.gossip): what was passed on.</summary>
        public static readonly string[] GossipKinds = { "crash", "arrest", "court", "opened", "graduated", "listed", "married", "baby" };

        private static void V6()
        {
            int before = List.Count;
            for (int i = 0; i < before; i++)
            {
                TextIntent t = List[i];
                if (Six(t.Key, t.Branch) != null)
                {
                    continue;   // copied already
                }
                TextIntent latest = Find(t.Key, t.Branch, DataV5);
                bool street = latest.Key.StartsWith("street.", StringComparison.Ordinal);
                string[] facts = street ? Joined(latest.Facts, KnownSix) : latest.Facts;
                string[] optional = street ? (MomentBranch(latest.Key, latest.Branch) ? MomentSix : None) : latest.Optional;
                Add6(new TextIntent(latest.Key, latest.Branch, latest.Voice, latest.Channel, latest.MaxLen, latest.Face, latest.Use, latest.News, facts, latest.Write,
                    latest.Must, DataV6, optional));
            }
            Staff();
            Life();
            Gossip();
            Add6(new TextIntent("citizen.review", "mine", "citizen", "flare_post", 160, TextFace.Citizen, TextUse.Posts,
                "{first} reviews your business {shop}: {stars} stars (the game decided the stars)",
                L("business: {shop}", "stars: {stars}", "service: {service}", "prices: {prices}", "cleanliness: {cleanliness}", "interior: {interior}", "mostly about: {about}"),
                "flare post: review", L("{shop}"), DataV6, L("it was: {crowd}")));
        }

        // The player's staff (Wiring/Wiring.TextsCity, TextsCityRules.Staff), in their own thread with the player.
        private static void Staff()
        {
            Employee("employee.raise_ask", "{first} asks you for a raise at {shop}",
                L("paid now: {paid} an hour", "asks for: {asked} an hour", "the going rate: {going} an hour"), "text: asks for a raise", L("{asked}"));
            Employee("employee.sick", "{first} is ill and cannot come in to {shop} today", None, "text: off sick", null);
            Employee("employee.raise_thanks", "you raised {first}'s pay at {shop}", L("new pay: {paid} an hour"), "text: thanks for a raise", null);
            Employee("employee.hours", "{first} has more hours at {shop} this week than they agreed to", L("hours this week: {hours}", "agreed to at most: {most}"),
                "text: unhappy with their hours", null);
        }

        // The people of the player's life after the player's own events (TextsCityRules.Sender).
        private static void Life()
        {
            Person6("partner.crash", null, "partner", TextFace.Friend, "{first} has just heard you crashed your car", None, "text: your crash", null, L("street: {street}"));
            Person6("mom.news.crash", null, "mom", TextFace.Mom, "you crashed your car", None, "text: reacts to news", null, L("street: {street}"));
            Person6("partner.court", "guilty", "partner", TextFace.Friend, "your court case is over: you were found guilty", None, "text: your court case", null, null);
            Person6("partner.court", "cleared", "partner", TextFace.Friend, "your court case is over: you were cleared", None, "text: your court case", null, null);
            Person6("mom.news.court", "guilty", "mom", TextFace.Mom, "your court case is over: you were found guilty", None, "text: reacts to news", null, null);
            Person6("mom.news.court", "cleared", "mom", TextFace.Mom, "your court case is over: you were cleared", None, "text: reacts to news", null, null);
            Person6("mom.news.graduated", null, "mom", TextFace.Mom, "you graduated", None, "text: reacts to news", null, L("degree: {degree}"));
            Person6("friend.graduated", null, "friend", TextFace.Friend, "{first} heard you graduated", None, "text: congratulations", null, L("degree: {degree}"));
            Person6("mom.news.listed", null, "mom", TextFace.Mom, "your company {company} is now on the stock market", L("company: {company}"), "text: reacts to news",
                L("{company}"), null);
            Person6("friend.listed", null, "friend", TextFace.Friend, "your company {company} is now on the stock market", L("company: {company}"), "text: congratulations",
                L("{company}"), null);
            Person6("friend.married", null, "friend", TextFace.Friend, "you married {name} today", L("partner: {name}"), "text: congratulations", null, null);
            Person6("mom.news.baby", "born", "mom", TextFace.Mom, "you have had a baby", None, "text: reacts to news", null, L("partner: {name}", "child: {child}"));
            Person6("mom.news.baby", "adopted", "mom", TextFace.Mom, "you have adopted a child", None, "text: reacts to news", null, L("partner: {name}", "child: {child}"));
            Person6("friend.baby", "born", "friend", TextFace.Friend, "{first} heard you have had a baby", None, "text: congratulations", null, L("child: {child}"));
            Person6("friend.baby", "adopted", "friend", TextFace.Friend, "{first} heard you have adopted a child", None, "text: congratulations", null, L("child: {child}"));
            Person6("friend.win", null, "friend", TextFace.Friend, "you had a big win: your money is up {amount} in a day", L("up by: {amount}"), "text: your good news", null, null);
            Person6("partner.loss", null, "partner", TextFace.Friend, "you had a big loss: your money is down {amount} in a day", L("down by: {amount}"), "text: comfort", null, null);
            Person6("friend.loss", null, "friend", TextFace.Friend, "you had a big loss: your money is down {amount} in a day", L("down by: {amount}"), "text: comfort", null, null);
        }

        // Gossip (TextsCityRules.Gossip): somebody who knows one of the player's people heard of the player's public event from them.
        private static void Gossip()
        {
            string[] news =
            {
                "{other} told {first} that you crashed your car", "{other} told {first} that you were arrested", "{other} told {first} about your court case",
                "{other} told {first} that you opened {shop}", "{other} told {first} that you graduated",
                "{other} told {first} that your company {company} is on the stock market", "{other} told {first} that you got married",
                "{other} told {first} that you have a new baby"
            };
            for (int k = 0; k < GossipKinds.Length; k++)
            {
                string kind = GossipKinds[k];
                string[] facts = kind == "opened" ? L("heard from: {other}", "business: {shop}") : kind == "listed" ? L("heard from: {other}", "company: {company}") : L("heard from: {other}");
                Person6("friend.gossip", kind, "friend", TextFace.Friend, news[k], facts, "text: heard it from a friend", L("{other}"), null);
            }
        }

        private static void Employee(string key, string news, string[] facts, string write, string[] must)
        {
            Add6(new TextIntent(key, null, "employee", "text", 200, TextFace.Employee, TextUse.People, news, facts, write, must, DataV6));
        }

        private static void Person6(string key, string branch, string voice, TextFace face, string news, string[] facts, string write, string[] must, string[] optional)
        {
            Add6(new TextIntent(key, branch, voice, "text", 200, face, TextUse.People, news, facts, write, must, DataV6, optional));
        }

        // An intent of data v6 with what the contract says of it.
        private static void Add6(TextIntent t)
        {
            t.Shape = ShapeOf(t);
            t.MemoryKinds = MemoryOf(t);
            t.MemoryMost = t.MemoryKinds == TextMemoryKinds.None ? 0 : TextMemoryRules.CardMost;
            t.UntoldName = t.Key.StartsWith("street.", StringComparison.Ordinal);
            List.Add(t);
        }

        private static string[] Joined(string[] a, string[] b)
        {
            var all = new string[a.Length + b.Length];
            Array.Copy(a, all, a.Length);
            Array.Copy(b, 0, all, a.Length, b.Length);
            return all;
        }

        /// <summary>The intent of data v6 of this key and branch; null if there is none.</summary>
        public static TextIntent Six(string key, string branch)
        {
            for (int i = 0; i < List.Count; i++)
            {
                TextIntent t = List[i];
                if (t.Data == DataV6 && t.Key == key && t.Branch == branch)
                {
                    return t;
                }
            }
            return null;
        }

        /// <summary>The kind of message in the contract's words.</summary>
        public static string ShapeOf(TextIntent t)
        {
            switch (t.Channel)
            {
                case "spoken": return "spoken";
                case "text": return "text";
                case "ledger": return "paper";
                case "flare_post": return t.Key == "citizen.review" ? "review" : "post";
                default:
                    return t.Face == TextFace.CitizenDm || t.Face == TextFace.CitizenDmMood || t.Key.IndexOf(".dm.", StringComparison.Ordinal) >= 0 ? "dm" : "comment";
            }
        }

        /// <summary>What a speaker may remember of the player, by who they are; nothing for what everybody reads.</summary>
        public static TextMemoryKinds MemoryOf(TextIntent t)
        {
            switch (t.Voice)
            {
                case "stranger":
                case "friend":
                case "partner":
                case "roommate":
                    return TextMemoryKinds.All;
                case "mom": return TextMemoryKinds.Talked | TextMemoryKinds.Tone | TextMemoryKinds.Favour | TextMemoryKinds.Knows | TextMemoryKinds.Event;
                case "inlaws": return TextMemoryKinds.Talked | TextMemoryKinds.Event;
                case "boss": return TextMemoryKinds.Met | TextMemoryKinds.Talked | TextMemoryKinds.Knows;
                case "lender": return TextMemoryKinds.Talked | TextMemoryKinds.Favour;
                case "employee": return TextMemoryKinds.Met | TextMemoryKinds.Talked | TextMemoryKinds.Favour | TextMemoryKinds.Knows;
                case "citizen": return t.Face == TextFace.CitizenDm || t.Face == TextFace.CitizenDmMood ? TextMemoryKinds.All : TextMemoryKinds.None;
                default: return TextMemoryKinds.None;
            }
        }

        /// <summary>
        /// The player's event a card's own news tells, so its memory is left out and not said twice; null for none.
        /// </summary>
        public static string Subject(string key, string branch)
        {
            if (key == null)
            {
                return null;
            }
            if (key == "friend.gossip")
            {
                return branch;
            }
            if (key.EndsWith(".crash", StringComparison.Ordinal))
            {
                return "crash";
            }
            if (key.EndsWith(".arrest", StringComparison.Ordinal))
            {
                return "arrest";
            }
            if (key.EndsWith(".court", StringComparison.Ordinal))
            {
                return "court";
            }
            switch (key)
            {
                case "friend.congrats_open":
                case "mom.news.opened": return "opened";
                case "friend.breakin": return "breakin";
                case "mom.news.graduated":
                case "friend.graduated": return "graduated";
                case "mom.news.listed":
                case "friend.listed": return "listed";
                case "mom.news.married":
                case "inlaws.married":
                case "friend.married": return "married";
                case "inlaws.baby":
                case "mom.news.baby":
                case "friend.baby": return "baby";
                case "friend.win": return "win";
                case "partner.loss":
                case "friend.loss": return "loss";
                default: return null;
            }
        }
    }
}
