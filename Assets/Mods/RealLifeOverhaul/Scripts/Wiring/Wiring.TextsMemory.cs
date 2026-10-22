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
        /// What people remember of the player, for RLO Texts' cards of data v6 (Texts/TextMemoryRules), from what the game and
        /// the mod already decided:
        ///
        ///   the social life   read, never copied: how and when a person met the player (their origin and day), their
        ///                     outings, loans and favours, a birthday remembered, being stood up, a rudeness, the arrest
        ///                     and crash they heard of (SocialPerson.Memories, Debt, Loan), and what they know: the name,
        ///                     the player's job and business, how well known the player is
        ///   street talk       recorded at each line the stranger says (<see cref="Street"/>): met in the street, what was
        ///                     talked about, how it went, the way given, numbers swapped, the name told, the business
        ///                     heard of, a famous face recognised; a stranger who swaps numbers takes it with them
        ///   texts and calls   what each text, call or Flare message was about (<see cref="Spoke"/>), and what a person of
        ///                     the social life last texted the player about, read from their own thread in the game's phone
        ///                     (<see cref="Texted"/>: every feature's texts, the family's, life's little moments' and
        ///                     travel's too, worded by a pack or not), so a text remembers the one before it and a call back
        ///                     the text it opens on
        ///   the city          the player's own life events the people of the player's life have heard of
        ///                     (TextsDirector.Events), and what gossip passed on
        ///
        /// Only with a pack of data 6 (TextsDirector.Six); only on cards of the player's own people, never a shared one. A
        /// card's memory lines are of the kinds and codes data 6 was trained on, at most TextMemoryRules.CardMost of them, so a
        /// card keeps the shape and the size the packs know (the thread's own lines are not put on a text's card: see
        /// docs/features/social.md, What a later pack could add).
        /// COST: nothing per frame; a few facts a card and one thread of twenty messages at most, a few a line of street talk.
        /// </summary>
        public static class TextsMemory
        {
            private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
            private static readonly List<SocialThreadLine> Lines = new List<SocialThreadLine>(Entities.Contact.MaxMessages);

            /// <summary>The memory a request's card may carry: its person's kept facts, and for a person of the social life what it keeps of them.</summary>
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

            // What a person last texted the player about, from their own thread in the game's phone (TextsMemoryFacts.Texted).
            private static void Texted(SocialPerson p, int today, List<TextFact> into)
            {
                SocialPhone.Thread(p, Lines);
                TextsMemoryFacts.Texted(Lines, p.IsFamily, p.Together, today, into);
                Lines.Clear();
            }

            /// <summary>What a text, a call or a Flare message was about, remembered by its person (only an intent of the player's own people).</summary>
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
                        return;   // (a post or a comment: what everybody reads is not a talk with the player)
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

            // The player's own events of the last weeks, as everybody in the player's life has heard of them by now.
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

            /// <summary>What the social life keeps of one of its people, as memory facts (none of it saved again).</summary>
            internal static void Derived(SocialPerson p, int today, List<TextFact> into)
            {
                if (p == null || !p.Met)
                {
                    return;
                }
                TextsMemoryFacts.People(p, into);   // (how they met, and their memories)
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

            // What a person of the social life knows of the player: their job (one met at work, or a friend), their business
            // (a friend, or one met at the shop), and how well known the player is.
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

            // The player's first business by name, or null.
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

            // ---------------------------------------------------------------- street talk

            /// <summary>The memory key of a stranger in the street: their seed (one person for the day).</summary>
            internal static string StrangerKey(StreetChat c) => "sp:" + c.Who.Seed.ToString(Inv);

            /// <summary>
            /// A line a stranger says, as what they will remember: met in the street (their first line), what was talked
            /// about, how it went, a favour, what they now know. A number swapped takes what they remember to the contact.
            /// </summary>
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
                            TextsDirector.Move(key, "s:" + c.Memo.Cast.ToString(Inv));   // (now somebody the player knows: what they remember goes with them)
                        }
                        break;
                }
            }
        }
    }
}
