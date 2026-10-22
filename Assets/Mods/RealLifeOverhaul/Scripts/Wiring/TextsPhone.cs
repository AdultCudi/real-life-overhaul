using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Family;
using RealLifeOverhaul.Roommates;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The phone's texts worded by RLO Texts: friends, the partner, Mom, the in-laws, the roommate, the boss and the
        /// lender. The game decides the text and every effect at once; its written line's key names the card, and the text
        /// is held at most <see cref="HoldSeconds"/> for the words, else sent with the written line (late, failing the
        /// checks, or the game saving meanwhile). A friend's greeting face to face is guessed as the player walks up.
        /// Nothing per frame: a request per text, when it is made.
        /// </summary>
        public static class TextsPhone
        {
            public const double HoldSeconds = 4.0;       // real seconds at most a text waits for its words (the player never knows it was coming)
            private const double GreetSeconds = 20.0;     // real seconds a guessed greeting is kept for its click

            private static readonly string[] MomLeft =
            {
                "a casserole in the fridge", "a lasagne in the oven", "some soup on the hob", "a cake on the kitchen table", "fresh towels in the bathroom",
                "a bag of apples by the door", "a pie under a tea towel", "clean sheets on the bed"
            };

            internal static void Connect()
            {
                SocialLinks.HoldText = Social;
                SocialLinks.GuessGreeting = Guess;
                SocialLinks.TakeGreeting = Take;
                FamilyText.HoldText = Hold;
                RoommateText.HoldText = Hold;
                CareerPhone.HoldText = Hold;
                DebtPhone.HoldText = Hold;
            }

            internal static void Disconnect()
            {
                SocialLinks.HoldText = null;
                SocialLinks.GuessGreeting = null;
                SocialLinks.TakeGreeting = null;
                FamilyText.HoldText = null;
                RoommateText.HoldText = null;
                CareerPhone.HoldText = null;
                DebtPhone.HoldText = null;
            }

            // Friends and Mom of the social life.
            private static bool Social(SocialPerson person, string key, Dictionary<string, string> data, Action<string, Dictionary<string, string>> send)
            {
                try
                {
                    if (person == null)
                    {
                        return false;
                    }
                    TextsMapRules.From from = person.IsFamily ? TextsMapRules.From.Mom : person.Together ? TextsMapRules.From.Partner : TextsMapRules.From.Friend;
                    string intent = TextsMapRules.Phone(key, from, out string branch);
                    if (intent == null)
                    {
                        return false;
                    }
                    if (!TextsDirector.Words.Wants(TextUse.People, false))
                    {
                        TextsDirector.Words.NotAsked(TextUse.People, false, intent);   // counted: the written line, and why
                        return false;
                    }
                    TextRequest r = Request(intent, branch, "s:" + person.Index, key, data, send);
                    if (intent.StartsWith("mom.", StringComparison.Ordinal))
                    {
                        MomWho(person, r.Who);
                    }
                    else
                    {
                        FriendWho(person, r.Who, intent.StartsWith("partner.", StringComparison.Ordinal), false);
                    }
                    Slots(r, data, person, key, intent);
                    TextsMemory.Fill(r, person);
                    TextsMemory.Spoke(r, "text");
                    return Ask(r);
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, the phone (friends)", e);
                    return false;
                }
            }

            // The partner, Mom and the in-laws of family life, the roommate, the boss and the lender.
            private static bool Hold(string from, string key, Dictionary<string, string> data, Action<string, Dictionary<string, string>> send)
            {
                try
                {
                    if (!TextsDirector.Words.Wants(TextUse.People, false))
                    {
                        CountNotAsked(from, key);
                        return false;
                    }
                    if (!Who(from, out TextsMapRules.From who))
                    {
                        return false;
                    }
                    string intent = TextsMapRules.Phone(key, who, out string branch);
                    if (intent == null)
                    {
                        return false;
                    }
                    TextRequest r = Request(intent, branch, from, key, data, send);
                    SocialPerson person = null;
                    switch (who)
                    {
                        case TextsMapRules.From.Partner:
                            person = SocialState.Partner;
                            if (person == null || !intent.StartsWith("partner.", StringComparison.Ordinal))
                            {
                                return false;   // a partner Family keeps from before the social life has no card
                            }
                            FriendWho(person, r.Who, true, intent == "partner.appointment_missed");
                            r.Speaker = "s:" + person.Index;
                            break;
                        case TextsMapRules.From.Mom:
                            person = MomPerson();
                            if (person == null)
                            {
                                return false;
                            }
                            MomWho(person, r.Who);
                            r.Speaker = "s:" + person.Index;
                            break;
                        case TextsMapRules.From.InLaws:
                            r.Who.Name = data != null && data.TryGetValue("name", out string partner) ? First(partner) : null;
                            r.Who.Style = TextWords.InLawStyle(FamilyState.S.PSeed);
                            r.Who.Player = TextsGame.PlayerName;
                            r.Who.When = TextsGame.When();
                            break;
                        case TextsMapRules.From.Roommate:
                            person = RoommateDirector.Instance?.State?.Mate?.Person;
                            if (person == null)
                            {
                                return false;
                            }
                            FriendWho(person, r.Who, false, false);
                            r.Who.Role = "your roommate";
                            r.Speaker = "r:" + person.Name;
                            break;
                        case TextsMapRules.From.Boss:
                            CareerRank rank = CareerState.CurrentRank;
                            if (rank == null || data == null || !data.TryGetValue("boss", out string boss) || !data.TryGetValue("employer", out string employer))
                            {
                                return false;
                            }
                            r.Who.Name = boss;
                            r.Who.Job = employer;
                            r.Who.Title = JobText.T(rank.TitleKey);
                            r.Who.Style = TextWords.BossStyle(employer.GetHashCode());
                            r.Who.Player = TextsGame.PlayerName;
                            r.Who.When = TextsGame.When();
                            if (data.TryGetValue("most", out string most) && most != "3")
                            {
                                return false;   // the card counts three marks, as the training's
                            }
                            break;
                        case TextsMapRules.From.Lender:
                            r.Who.Player = TextsGame.PlayerName;
                            r.Who.When = TextsGame.When();
                            break;
                    }
                    Slots(r, data, person, key, intent);
                    r.Person = MemoryKey(who, person, r);
                    TextsMemory.Fill(r, person);
                    TextsMemory.Spoke(r, "text");
                    return Ask(r);
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, the phone (" + from + ")", e);
                    return false;
                }
            }

            // The key a held text's speaker remembers under: a person by their place, the in-laws, the lender, a boss by the employer.
            private static string MemoryKey(TextsMapRules.From who, SocialPerson person, TextRequest r)
            {
                if (who == TextsMapRules.From.Roommate)
                {
                    return r.Speaker;   // "r:" and their name: a roommate may be nobody of the social life's own
                }
                if (person != null)
                {
                    return "s:" + person.Index;
                }
                switch (who)
                {
                    case TextsMapRules.From.InLaws: return "inlaws";
                    case TextsMapRules.From.Lender: return "lender";
                    case TextsMapRules.From.Boss: return r.Who.Job != null ? "boss:" + r.Who.Job : null;
                    default: return null;
                }
            }

            // The sender of a held text, by the name its feature gives it; false for one with no card.
            private static bool Who(string from, out TextsMapRules.From who)
            {
                switch (from)
                {
                    case "partner": who = TextsMapRules.From.Partner; return true;
                    case "mom": who = TextsMapRules.From.Mom; return true;
                    case "inlaws": who = TextsMapRules.From.InLaws; return true;
                    case "roommate": who = TextsMapRules.From.Roommate; return true;
                    case "boss": who = TextsMapRules.From.Boss; return true;
                    case "lender": who = TextsMapRules.From.Lender; return true;
                    default: who = default; return false;
                }
            }

            // A held text with no model to ask: counted with why, under its card when its key has one.
            private static void CountNotAsked(string from, string key)
            {
                if (!Who(from, out TextsMapRules.From who))
                {
                    return;
                }
                string intent = TextsMapRules.Phone(key, who, out string _);
                if (intent != null)
                {
                    TextsDirector.Words.NotAsked(TextUse.People, false, intent);
                }
            }

            // A friend's greeting face to face, guessed as the player walks up.
            private static void Guess(SocialPerson person, PlanKind kind, string place, int late)
            {
                try
                {
                    string what = TextIntents.OutingWord(Outing(kind));
                    if (person == null || what == null || !TextsDirector.Words.Wants(TextUse.People, false))
                    {
                        return;
                    }
                    bool isLate = late > SocialRules.LateMinutes;
                    var r = new TextRequest
                    {
                        Use = TextUse.People, Intent = "friend.greet", Branch = isLate ? "late" : "on", Key = "greet:" + person.Index, Speaker = "s:" + person.Index,
                        Priority = TextPriority.Now, Seconds = GreetSeconds, Guess = true, Day = TextsGame.Day, Done = words => { }
                    };
                    FriendWho(person, r.Who, person.Together, false);
                    r.Slots.Set("what", what).Set("place", place).Set("late", late.ToString());
                    TextsMemory.Fill(r, person);
                    TextsDirector.Words.Ask(r, TextClock.Now);
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, a greeting", e);
                }
            }

            private static string Take(SocialPerson person)
            {
                try
                {
                    return person != null ? TextsDirector.Words.TakeGuess("greet:" + person.Index) : null;
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, a greeting", e);
                    return null;
                }
            }

            private static TextRequest Request(string intent, string branch, string speaker, string key, Dictionary<string, string> data, Action<string, Dictionary<string, string>> send)
            {
                GameInstance save = SaveGameManager.Current;   // the save it belongs to: never sent into another one
                return new TextRequest
                {
                    Use = TextUse.People, Intent = intent, Branch = branch, Key = "ph:" + speaker + ":" + key, Speaker = speaker, Priority = TextPriority.Soon,
                    Seconds = HoldSeconds, Flush = true, Day = TextsGame.Day,
                    Done = words =>
                    {
                        if (ReferenceEquals(save, SaveGameManager.Current))
                        {
                            // The words keep the written line's key, so a call back knows what the text was about.
                            send(words != null ? SocialLinks.WordsKey : key,
                                words != null ? new Dictionary<string, string>(2) { { "text", words }, { SocialFollowRules.KeyField, key } } : data);
                        }
                    }
                };
            }

            private static bool Ask(TextRequest r)
            {
                return TextsDirector.Words.Ask(r, TextClock.Now);
            }

            /// <summary>
            /// A friend or the partner, by their number, as the speaker of a Flare message's answer; false for nobody met, or
            /// Mom (no card of hers answers a Flare message).
            /// </summary>
            internal static bool FlareFriend(int index, TextWho who, out bool partner)
            {
                SocialPerson p = SocialState.Get(index);
                partner = p != null && p.Together;
                if (p == null || !p.Met || p.IsFamily)
                {
                    return false;
                }
                FriendWho(p, who, partner, false);
                return true;
            }

            /// <summary>A friend, the partner or a roommate as the speaker: their own data in the training's words.</summary>
            internal static void FriendWho(SocialPerson p, TextWho who, bool partner, bool expecting)
            {
                who.Clear();
                who.Name = p.Name;
                who.First = SocialLife.First(p);
                who.Age = p.Age;
                who.Job = TextWords.Jobs[Math.Max(0, Math.Min(p.Job, TextWords.Jobs.Length - 1))];
                who.District = SocialPlaces.District(p.District);
                who.Role = partner ? PartnerRole(p) + (expecting ? TextWords.Expecting : string.Empty) : FriendRole(p);
                TextWords.TraitsOf((int)p.Traits, who.Traits);
                who.Mood = TextWords.Mood(p.Mood);
                who.Style = TextWords.FriendStyle(p.Seed, (int)p.Traits, partner);
                who.Player = TextsGame.PlayerName;
                who.When = TextsGame.When();
            }

            private static void MomWho(SocialPerson p, TextWho who)
            {
                who.Clear();
                who.Age = p.Age;
                who.Mood = TextWords.Mood(Math.Max(-1, p.Mood));
                who.Style = TextWords.MomStyle(p.Seed);
                who.Player = TextsGame.PlayerName;
                who.When = TextsGame.When();
            }

            private static string FriendRole(SocialPerson p)
            {
                int stage = Math.Max(1, Math.Min((int)p.Stage, TextWords.Stages.Length - 1));
                return TextWords.Stages[stage] + ", " + TextWords.Origins[Math.Max(0, Math.Min((int)p.Origin, TextWords.Origins.Length - 1))];
            }

            private static string PartnerRole(SocialPerson p)
            {
                switch (FamilyState.Bond)
                {
                    case Bond.Married: return TextWords.PartnerRoles[3];
                    case Bond.Engaged: return TextWords.PartnerRoles[2];
                    default: return p.MovedIn || FamilyState.S.LivesIn ? TextWords.PartnerRoles[1] : TextWords.PartnerRoles[0];
                }
            }

            private static SocialPerson MomPerson()
            {
                for (int i = 0; SocialState.Loaded && i < SocialState.Count; i++)
                {
                    if (SocialState.People[i] != null && SocialState.People[i].IsFamily)
                    {
                        return SocialState.People[i];
                    }
                }
                return null;
            }

            // The card's values from the written line's own (and the plan's, "t_" before each), in the training's words.
            private static void Slots(TextRequest r, Dictionary<string, string> data, SocialPerson person, string key, string intent)
            {
                if (data != null)
                {
                    Copy(data, r.Slots, "street", "street");
                    Copy(data, r.Slots, "shop", "shop");
                    Copy(data, r.Slots, "amount", "amount");
                    Copy(data, r.Slots, "other", "other");
                    Copy(data, r.Slots, "t_place", "place");
                    Copy(data, r.Slots, "t_time", "time");
                    Copy(data, r.Slots, "t_waited", "waited");
                    Copy(data, r.Slots, "child", "child");
                    Copy(data, r.Slots, "n", "n");
                    Copy(data, r.Slots, "least", "least");
                    Copy(data, r.Slots, "most", "most");
                    Copy(data, r.Slots, "days", "days");
                    Copy(data, r.Slots, "marks", "marks");
                    Copy(data, r.Slots, "day", "day");
                    Copy(data, r.Slots, "degree", "degree");     // data v6's life events: the degree, the company listed
                    Copy(data, r.Slots, "company", "company");
                    if (data.TryGetValue("name", out string name) && !string.IsNullOrEmpty(name))
                    {
                        r.Slots.Set("name", First(name));
                    }
                    if (data.TryGetValue("t_kind", out string kind) && Enum.TryParse(kind, out PlanKind plan))
                    {
                        r.Slots.Set("what", TextIntents.OutingWord(Outing(plan)));
                    }
                    if (data.TryGetValue("text", out string text) && (intent == "mom.holiday" || intent == "mom.holiday_missed"))
                    {
                        r.Slots.Set("holiday", text);
                    }
                }
                if (intent.StartsWith("friend.invite.", StringComparison.Ordinal))
                {
                    r.Slots.Set("what", TextIntents.OutingWord(intent.Substring("friend.invite.".Length)));
                }
                if (intent == "friend.drift" && person != null)
                {
                    r.Slots.Set("days", Math.Max(1, SocialLife.Day - person.LastContactDay).ToString());
                }
                if (intent == "friend.lift" && person != null)
                {
                    r.Slots.Set("district", SocialPlaces.District(person.District));
                }
                if (intent == "friend.news.good" || intent == "friend.news.bad")
                {
                    r.Slots.Set("happening", TextsMapRules.Happening(key) ?? TextWords.Happening((person != null ? person.Seed : 0) + SocialLife.Day, intent == "friend.news.good"));
                }
                if (intent == "mom.visit")
                {
                    r.Slots.Set("left", MomLeft[TextWords.Roll(SocialLife.Day, 3, MomLeft.Length)]);
                }
                if (intent == "mom.holiday")
                {
                    r.Slots.Set("day", TextWords.Weekdays[((int)TimeHelper.GetDayOfWeek(SocialLife.Day + 1) + 6) % 7]);
                }
            }

            private static void Copy(Dictionary<string, string> data, TextSlots slots, string from, string to)
            {
                if (data.TryGetValue(from, out string value) && !string.IsNullOrEmpty(value))
                {
                    slots.Set(to, value);
                }
            }

            private static string Outing(PlanKind kind)
            {
                switch (kind)
                {
                    case PlanKind.Coffee: return "coffee";
                    case PlanKind.Meal: return "meal";
                    case PlanKind.Drinks: return "drinks";
                    case PlanKind.Cinema: return "cinema";
                    case PlanKind.Walk: return "walk";
                    case PlanKind.Workout: return "workout";
                    default: return null;
                }
            }

            private static string First(string name)
            {
                int space = name.IndexOf(' ');
                return space > 0 ? name.Substring(0, space) : name;
            }
        }
    }
}
