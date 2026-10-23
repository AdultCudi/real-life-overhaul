using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Street;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// What people remember of the player, for RLO Texts' cards of data 6: read from the social life (never copied),
        /// recorded from street talk and from what each text, call or Flare message was about, the last text in a person's
        /// thread, and the player's life events. Only with a pack of data 6, and only on cards of the player's own people,
        /// never a shared one. Nothing per frame: a few facts a card, a thread of twenty messages at most.
        /// </summary>
        public static class TextsMemory
        {
            private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
            private static readonly List<SocialThreadLine> Lines = new List<SocialThreadLine>(Entities.Contact.MaxMessages);

            /// <summary>The memory a request's card may carry: its person's kept facts, and what the social life keeps of them.</summary>
            internal static void Fill(TextRequest r, SocialPerson person)
            {
                if (r == null || !TextsDirector.Six || r.Shared)
                {
                    return;
                }
                if (r.Person == null && TextMemoryRules.KeyOk(r.Speaker))
                {
                    r.Person = r.Speaker;
                }
                if (r.Person == null)
                {
                    return;
                }
                TextsDirector.Recall(r.Person, r.Memory);
                if (person != null)
                {
                    Derived(person, r.Day, r.Memory);
                    Texted(person, r.Day, r.Memory);
                }
                Heard(r.Memory);
            }

            // What a person last texted the player about, from their thread in the game's phone.
            private static void Texted(SocialPerson p, int today, List<TextFact> into)
            {
                SocialPhone.Thread(p, Lines);
                TextsMemoryFacts.Texted(Lines, p.IsFamily, p.Together, today, into);
                Lines.Clear();
            }

            /// <summary>What a text, a call or a Flare message was about, remembered by its person.</summary>
            internal static void Spoke(TextRequest r, string how)
            {
                if (r == null || r.Person == null || !TextsDirector.Six)
                {
                    return;
                }
                string topic = TextMemoryTopics.Text(r.Intent);
                if (how == "dm")
                {
                    if (r.Intent == null || r.Intent.IndexOf(".dm.", StringComparison.Ordinal) < 0)
                    {
                        return;   // a post or a comment, which everybody reads, is not a talk with the player
                    }
                    if (TextsDirector.Memory.Of(r.Person) == null)
                    {
                        TextsDirector.Remember(r.Person, TextMemoryKinds.Met, "dm");
                    }
                    TextsDirector.Remember(r.Person, TextMemoryKinds.Talked, "dm");
                    return;
                }
                if (how == "call")
                {
                    TextsDirector.Remember(r.Person, TextMemoryKinds.Talked, "call");
                    return;
                }
                if (topic != null)
                {
                    TextsDirector.Remember(r.Person, TextMemoryKinds.Talked, "text", topic);
                }
            }

            // The player's events of the last weeks, which everybody in their life has heard of by now.
            private static void Heard(List<TextFact> into)
            {
                IReadOnlyList<TextLifeEvent> events = TextsDirector.Events;
                for (int i = 0; i < events.Count; i++)
                {
                    TextLifeEvent e = events[i];
                    if (TextsCityRules.Public(e.Kind) || e.Kind == "win" || e.Kind == "loss")
                    {
                        into.Add(new TextFact(TextMemoryKinds.Event, e.Kind, e.Day, e.Kind == "opened" || e.Kind == "listed" ? e.Value : null));
                    }
                }
            }

            /// <summary>What the social life keeps of one of its people, as memory facts; none of it is saved again.</summary>
            internal static void Derived(SocialPerson p, int today, List<TextFact> into)
            {
                if (p == null || !p.Met)
                {
                    return;
                }
                TextsMemoryFacts.People(p, into);
                if (p.Debt > 0)
                {
                    into.Add(new TextFact(TextMemoryKinds.Favour, "owed", Math.Max(p.DebtDay, 0), TextsGame.Money(p.Debt)));
                }
                if (p.Loan > 0)
                {
                    into.Add(new TextFact(TextMemoryKinds.Favour, "owes", today, TextsGame.Money(p.Loan)));
                }
                into.Add(new TextFact(TextMemoryKinds.Knows, "name", p.MetDay));
                Knows(p, into);
            }

            // The job (to a friend or one met at work), the business (to a friend or one met at the shop), and fame.
            private static void Knows(SocialPerson p, List<TextFact> into)
            {
                bool friend = p.Stage >= SocialStage.Friend || p.IsFamily;
                CareerRank rank = CareerState.CurrentRank;
                if (rank != null && (friend || p.Origin == SocialOrigin.Work))
                {
                    into.Add(new TextFact(TextMemoryKinds.Knows, "job", p.MetDay, JobText.T(rank.TitleKey)));
                }
                if (friend || p.Origin == SocialOrigin.Customer)
                {
                    string shop = FirstBusiness();
                    if (shop != null)
                    {
                        into.Add(new TextFact(TextMemoryKinds.Knows, "business", p.MetDay, shop));
                    }
                }
                if (Settings.Standing && Standing.Recognition >= TextsCity.KnownFrom)
                {
                    into.Add(new TextFact(TextMemoryKinds.Knows, Standing.Public >= TextsCity.LikedFrom ? "famous" : "infamous", p.MetDay));
                }
            }

            private static readonly List<BuildingRegistration> Mine = new List<BuildingRegistration>(8);

            private static string FirstBusiness()
            {
                Economy.Books.Businesses(Mine);
                for (int i = 0; i < Mine.Count; i++)
                {
                    if (Mine[i] != null && !string.IsNullOrEmpty(Mine[i].BusinessName))
                    {
                        return Mine[i].BusinessName;
                    }
                }
                return null;
            }

            /// <summary>The memory key of a stranger in the street: their seed, one person for the day.</summary>
            internal static string StrangerKey(StreetChat c) => "sp:" + c.Who.Seed.ToString(Inv);

            /// <summary>A line a stranger says, as what they will remember; a number swapped takes it to the contact.</summary>
            internal static void Street(StreetChat c, StreetLine line, Dictionary<string, string> data)
            {
                if (c == null || !TextsDirector.Six)
                {
                    return;
                }
                string key = StrangerKey(c);
                if (TextsDirector.Memory.Of(key) == null)
                {
                    TextsDirector.Remember(key, TextMemoryKinds.Met, "street");
                }
                string topic = TextMemoryTopics.Street(line);
                if (topic != null)
                {
                    TextsDirector.Remember(key, TextMemoryKinds.Talked, "street", topic);
                }
                string tone = TextMemoryTopics.Tone(line);
                if (tone != null)
                {
                    TextsDirector.Remember(key, TextMemoryKinds.Tone, tone);
                }
                switch (line)
                {
                    case StreetLine.DirFound:
                    case StreetLine.DirFar:
                        if (data != null && data.TryGetValue("place", out string place) && !string.IsNullOrEmpty(place))
                        {
                            TextsDirector.Remember(key, TextMemoryKinds.Favour, "directions", place);
                        }
                        break;
                    case StreetLine.InviteYes:
                        TextsDirector.Remember(key, TextMemoryKinds.Favour, "invite");
                        break;
                    case StreetLine.NameGive:
                    case StreetLine.NameWithhold:
                    case StreetLine.NameKnown:
                        TextsDirector.Remember(key, TextMemoryKinds.Knows, "name");
                        break;
                    case StreetLine.BusinessHeard:
                    case StreetLine.BusinessWill:
                    case StreetLine.BusinessAgain:
                        if (data != null && data.TryGetValue("shop", out string shop) && !string.IsNullOrEmpty(shop))
                        {
                            TextsDirector.Remember(key, TextMemoryKinds.Knows, "business", shop);
                        }
                        break;
                    case StreetLine.OpenFamous:
                        TextsDirector.Remember(key, TextMemoryKinds.Knows, "famous");
                        break;
                    case StreetLine.OpenInfamous:
                        TextsDirector.Remember(key, TextMemoryKinds.Knows, "infamous");
                        break;
                    case StreetLine.NumberYes:
                        TextsDirector.Remember(key, TextMemoryKinds.Favour, "number");
                        if (c.Memo != null && c.Memo.Cast >= 0)
                        {
                            TextsDirector.Move(key, "s:" + c.Memo.Cast.ToString(Inv));
                        }
                        break;
                }
            }
        }
    }
}
