using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// The English words a card uses for the mod's people, as the models were trained on them (common.py's lists, in
    /// SocialCatalogue's order), and each person's style of writing, drawn from their own seed by the generator's rule so a
    /// person always writes the same way. Cards are always in English. Game-free. COST: a lookup or a few hashes.
    /// </summary>
    public static class TextWords
    {
        /// <summary>By SocialCatalogue.Jobs.</summary>
        public static readonly string[] Jobs =
        {
            "barista", "nurse", "teacher", "mechanic", "line cook", "accountant", "bartender", "cab driver", "graphic designer", "electrician",
            "librarian", "bike courier", "personal trainer", "florist", "reporter", "retired"
        };

        /// <summary>By the bit of SocialTrait (Punctual first), as SocialCatalogue.TraitNames.</summary>
        public static readonly string[] Traits =
        {
            "always on time", "runs late", "generous", "careful with money", "sporty", "loves food", "a night owl", "a homebody", "reliable",
            "a bit flaky", "supportive", "a little envious"
        };

        /// <summary>By a person's mood, -2 (a rough day) to +2 (a great one).</summary>
        public static readonly string[] Moods = { "having a rough day", "a bit low", "fine", "in a good mood", "having a great day" };

        /// <summary>How they met, by SocialOrigin; Event, not in the training data, takes the nearest.</summary>
        public static readonly string[] Origins =
        {
            "family", "an old friend", "met at the gym", "met at work", "met at the coffee cart", "lives next door", "you helped them once",
            "a customer at your shop", "met on Flare", "met at work", "met in the street"
        };

        /// <summary>A friend's stage (SocialStage Acquaintance to CloseFriend) as the card's role starts.</summary>
        public static readonly string[] Stages = { "an acquaintance", "an acquaintance", "a friend", "a close friend" };

        /// <summary>The partner's role on a card by the bond (Wiring/TextsPhone): dating, living together, engaged, married.</summary>
        public static readonly string[] PartnerRoles = { "dating you", "your partner, living with you", "engaged to you", "married to you" };

        /// <summary>What the partner's role adds while a baby is on the way.</summary>
        public const string Expecting = ", expecting a baby";

        public static readonly string[] Weekdays = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        /// <summary>Small things of a friend's day, nice and annoying (world.HAPPENINGS_GOOD and _BAD): a small-talk card's one fact.</summary>
        public static readonly string[] HappeningsGood =
        {
            "my landlord finally fixed the heating", "I found twenty dollars in an old coat", "work let us go early", "my boss said good job today",
            "I saw a dog on the subway wearing a raincoat", "the bakery on my street had fresh bread", "I got the last seat on the subway", "my plant has a new leaf",
            "I passed my evening class test", "I fixed the leaky tap all by myself", "my sister finally called me back", "I slept a full eight hours",
            "the sun came out on my lunch break", "a stranger paid for my coffee", "I finished a book I'd been stuck on", "my neighbour gave me a tomato plant"
        };

        public static readonly string[] HappeningsBad =
        {
            "I got a parking ticket", "the lift in my building is stuck again", "the coffee machine at work broke", "somebody on my street is learning the trumpet",
            "I missed my bus by a second", "it rained on me twice", "I burnt my dinner", "my phone screen cracked", "work was long and loud", "my landlord put the rent up",
            "I spilt coffee down my shirt", "my train sat in a tunnel for ages", "I locked myself out", "the shop was out of my favourite bread",
            "my umbrella blew inside out", "I stood in a puddle first thing"
        };

        /// <summary>A small thing of a friend's day for small talk, from a seed of the person and the day.</summary>
        public static string Happening(int seed, bool good)
        {
            string[] list = good ? HappeningsGood : HappeningsBad;
            return list[Roll(seed, 11, list.Length)];
        }

        private static readonly string[] FriendStyles = { "warm", "warm", "dry", "playful", "blunt", "shy", "cheeky" };
        private static readonly string[] CitizenStyles = { "warm", "dry", "playful", "blunt", "grumpy", "cheerful" };
        private static readonly string[] MomStyles = { "gushing", "fussy", "warm" };
        private static readonly string[] BossStyles = { "formal", "blunt", "brief", "warm", "dry" };

        private const int SupportiveBit = 10, EnviousBit = 11;   // bits of SocialTrait

        private static readonly StringBuilder Build = new StringBuilder(64);

        /// <summary>A mood's words for the mod's -2 to +2.</summary>
        public static string Mood(int mood) => Moods[Clamp(mood + 2, 0, Moods.Length - 1)];

        /// <summary>The traits' words, in the order of their bits.</summary>
        public static void TraitsOf(int traits, List<string> into)
        {
            for (int bit = 0; bit < Traits.Length; bit++)
            {
                if ((traits & (1 << bit)) != 0)
                {
                    into.Add(Traits[bit]);
                }
            }
        }

        /// <summary>"Monday 14:05": the day of the week and the time, as a card's when line.</summary>
        public static string When(int weekday, int hour, int minute)
        {
            return Weekdays[Clamp(weekday, 0, 6)] + " " + Two(hour) + ":" + Two(minute);
        }

        /// <summary>A time of day as a card writes it: "18:30".</summary>
        public static string Clock(int hour, int minute) => Two(hour) + ":" + Two(minute);

        /// <summary>Money as the game writes it in English and the cards had it: "$1,250".</summary>
        public static string Money(long amount)
        {
            return "$" + amount.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A friend's, the partner's or a roommate's way of writing, from their own seed (world.friend). The supportive never
        /// write coldly; the envious often dryly.
        /// </summary>
        public static string FriendStyle(int seed, int traits, bool partner)
        {
            string main = FriendStyles[Roll(seed, 1, FriendStyles.Length)];
            if ((traits & (1 << SupportiveBit)) != 0 && (main == "dry" || main == "blunt"))
            {
                main = "warm";
            }
            if ((traits & (1 << EnviousBit)) != 0 && Roll(seed, 2, 2) == 0)
            {
                main = "dry";
            }
            Build.Length = 0;
            Build.Append(main);
            bool lower = Roll(seed, 3, 100) < 30;
            if (lower)
            {
                Build.Append(", lowercase");
            }
            else if (Roll(seed, 4, 100) < 25)
            {
                Build.Append(", brief");
            }
            if (Roll(seed, 5, 100) < 12)
            {
                Build.Append(", uses emoji");
            }
            if (partner && !lower && Roll(seed, 6, 100) < 25)
            {
                Build.Append(", signs off with x");
            }
            return Build.ToString();
        }

        /// <summary>One of the city's people's way of writing on Flare (world.citizen).</summary>
        public static string CitizenStyle(int seed)
        {
            Build.Length = 0;
            Build.Append(CitizenStyles[Roll(seed, 1, CitizenStyles.Length)]);
            if (Roll(seed, 3, 100) < 30)
            {
                Build.Append(", lowercase");
            }
            if (Roll(seed, 5, 100) < 15)
            {
                Build.Append(", uses emoji");
            }
            return Build.ToString();
        }

        /// <summary>Mom's way of writing (defs_people.mom): gushing, fussy or warm, and now and then signing off.</summary>
        public static string MomStyle(int seed)
        {
            string main = MomStyles[Roll(seed, 1, MomStyles.Length)];
            return Roll(seed, 2, 100) < 35 ? main + ", signs off Love, Mom" : main;
        }

        /// <summary>A manager's way of writing (defs_work.boss).</summary>
        public static string BossStyle(int seed) => BossStyles[Roll(seed, 1, BossStyles.Length)];

        /// <summary>The in-laws' way of writing (defs_people.make_inlaws).</summary>
        public static string InLawStyle(int seed) => Roll(seed, 1, 3) == 0 ? "warm" : Roll(seed, 1, 3) == 1 ? "fussy" : "formal";

        /// <summary>A business account's way of writing (defs_flare.make_biz).</summary>
        public static string BusinessStyle(int seed) => Roll(seed, 1, 3) == 2 ? "neat, polite" : "friendly, plain";

        /// <summary>An age from a seed in the generator's range: the city's people have none in the game.</summary>
        public static int Age(int seed, int least, int most) => least + Roll(seed, 7, most - least + 1);

        /// <summary>0 to n-1 from a seed and a slot: the same every time for the same person.</summary>
        public static int Roll(int seed, int slot, int n)
        {
            if (n <= 1)
            {
                return 0;
            }
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)slot * 2246822519u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (int)(h % (uint)n);
            }
        }

        private static string Two(int n) => n < 10 ? "0" + n : n.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static int Clamp(int v, int least, int most) => v < least ? least : v > most ? most : v;

        // Data v6: the card's English words for what the game shows in the player's language.

        /// <summary>The kinds of place street talk is asked the way to, by StreetPlace (the English of StreetLines.Whats).</summary>
        public static readonly string[] StreetWhats = { "a cafe", "a restaurant", "a bar", "a supermarket", "a clothes shop", "a gym", "a cinema", "a bank" };

        /// <summary>Which way, by StreetRules.Way (the English of StreetLines.Ways).</summary>
        public static readonly string[] StreetWays = { "straight ahead", "off to your right", "back the way you came", "off to your left" };

        /// <summary>A walk of so many minutes, as street talk's card says it.</summary>
        public static string Walk(int minutes) => minutes == 1 ? "a minute" : minutes.ToString(System.Globalization.CultureInfo.InvariantCulture) + " minutes";

        /// <summary>The street events' locale keys (Events/EventCalendar.Name) and their English words on a card.</summary>
        public static readonly string[] StreetEvents =
        {
            "reallife_event_name_market", "Street market", "reallife_event_name_farmers", "Farmers' market", "reallife_event_name_parade", "Parade",
            "reallife_event_name_protest", "Protest", "reallife_event_name_run", "Charity fun run", "reallife_event_name_film", "Film shoot",
            "reallife_event_name_fireworks", "Fireworks"
        };

        /// <summary>A street event's English words by its locale key; null for another key.</summary>
        public static string EventWords(string key)
        {
            for (int i = 0; key != null && i + 1 < StreetEvents.Length; i += 2)
            {
                if (StreetEvents[i] == key)
                {
                    return StreetEvents[i + 1];
                }
            }
            return null;
        }

        /// <summary>The game's skills (their names in the game) and the job a card calls one of the player's staff by.</summary>
        public static readonly string[] StaffJobs =
        {
            "ba:skill_customerservice", "shop assistant", "ba:skill_cleaning", "cleaner", "ba:skill_lawyer", "lawyer", "ba:skill_negotiation", "purchasing agent",
            "ba:skill_logisticsmanager", "logistics manager", "ba:skill_deliverydriver", "delivery driver", "ba:skill_programmer", "programmer",
            "ba:skill_hrmanager", "HR manager", "ba:skill_graphicdesigner", "graphic designer", "ba:skill_securityguard", "security guard", "ba:skill_headhunter", "headhunter"
        };

        /// <summary>The job a card calls one of the staff by, from their main skill's name; "member of staff" for an unknown one.</summary>
        public static string StaffJob(string skill)
        {
            for (int i = 0; skill != null && i + 1 < StaffJobs.Length; i += 2)
            {
                if (StaffJobs[i] == skill)
                {
                    return StaffJobs[i + 1];
                }
            }
            return "member of staff";
        }

        /// <summary>The ways of writing of the player's staff (the contract lists them).</summary>
        public static readonly string[] StaffStyles = { "polite", "warm", "brief", "nervous", "matter of fact", "cheerful" };

        /// <summary>One of the player's staff's way of writing, from their own seed.</summary>
        public static string StaffStyle(int seed) => StaffStyles[Roll(seed, 1, StaffStyles.Length)];

        /// <summary>A review's word for one of the game's satisfaction figures (0 to 100).</summary>
        public static string Rating(int figure) => figure >= 75 ? "very good" : figure >= 55 ? "good" : figure >= 35 ? "fair" : "poor";
    }
}
