using System.Collections.Generic;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The game-free part of TextsMemory: a social life person's meeting, memories and last text to the
        /// player as RLO Texts' memory facts, only of the kinds and codes data 6 was trained on (TextMemoryRules.Codes).
        /// Compiled into EconomyCheck. Costs a pass over six memories and a thread of twenty messages at most.
        /// </summary>
        public static class TextsMemoryFacts
        {
            /// <summary>How a person met the player, and their memories, as memory facts.</summary>
            public static void People(SocialPerson p, List<TextFact> into)
            {
                if (p == null || !p.Met)
                {
                    return;
                }
                string met = MetCode(p.Origin);
                if (met != null)
                {
                    into.Add(new TextFact(TextMemoryKinds.Met, met, p.MetDay));
                }
                for (int i = 0; i < p.Memories.Count; i++)
                {
                    if (FactOf(p.Memories[i], out TextFact f))
                    {
                        into.Add(f);
                    }
                }
            }

            /// <summary>
            /// The newest of a person's texts in their thread (oldest first) that has a topic, as a "talked" memory while it is
            /// remembered. The text a card is written for is not in the thread yet, so a text's card has the one before it.
            /// </summary>
            public static void Texted(IReadOnlyList<SocialThreadLine> thread, bool family, bool together, int today, List<TextFact> into)
            {
                for (int i = thread != null ? thread.Count - 1 : -1; i >= 0; i--)
                {
                    SocialThreadLine line = thread[i];
                    string topic = line.Player ? null : TopicOf(line.Key, family, together);
                    if (topic == null)
                    {
                        continue;
                    }
                    int day = SocialFollowRules.DayOf(line.Minute);
                    if (day >= 0 && day <= today && today - day <= TextMemoryRules.Lifetime(TextMemoryKinds.Talked))
                    {
                        into.Add(new TextFact(TextMemoryKinds.Talked, "text", day, topic));
                    }
                    return;
                }
            }

            /// <summary>A text's topic in the cards' words, from RLO Texts' map or else the call back's subject; null for neither.</summary>
            public static string TopicOf(string key, bool family, bool together)
            {
                if (key == null)
                {
                    return null;
                }
                TextsMapRules.From from = family ? TextsMapRules.From.Mom : together ? TextsMapRules.From.Partner : TextsMapRules.From.Friend;
                string intent = TextsMapRules.Phone(key, from, out string _);
                string topic = intent != null ? TextMemoryTopics.Text(intent) : null;
                return topic ?? SocialFollowCatalogue.Of(key)?.Topic;
            }

            /// <summary>How the social life met a person, as a memory's code; null for the family (Mom is not "met").</summary>
            private static string MetCode(SocialOrigin origin)
            {
                switch (origin)
                {
                    case SocialOrigin.OldFriend: return "old_friend";
                    case SocialOrigin.Gym: return "gym";
                    case SocialOrigin.Work: return "work";
                    case SocialOrigin.CoffeeCart: return "coffee";
                    case SocialOrigin.Neighbour: return "neighbour";
                    case SocialOrigin.Helped: return "helped";
                    case SocialOrigin.Customer: return "customer";
                    case SocialOrigin.Online: return "flare";
                    case SocialOrigin.Event: return "event";
                    case SocialOrigin.Street: return "street";
                    default: return null;
                }
            }

            /// <summary>One of the social life's memories as a memory fact; false for one with none.</summary>
            private static bool FactOf(SocialMemory m, out TextFact f)
            {
                f = default(TextFact);
                switch (m.Code)
                {
                    case MemoryCode.Coffee: f = new TextFact(TextMemoryKinds.Talked, "outing", m.Day, TextMemoryTopics.Outings[0]); return true;
                    case MemoryCode.Meal: f = new TextFact(TextMemoryKinds.Talked, "outing", m.Day, TextMemoryTopics.Outings[1]); return true;
                    case MemoryCode.Drinks: f = new TextFact(TextMemoryKinds.Talked, "outing", m.Day, TextMemoryTopics.Outings[2]); return true;
                    case MemoryCode.Cinema: f = new TextFact(TextMemoryKinds.Talked, "outing", m.Day, TextMemoryTopics.Outings[3]); return true;
                    case MemoryCode.Walk: f = new TextFact(TextMemoryKinds.Talked, "outing", m.Day, TextMemoryTopics.Outings[4]); return true;
                    case MemoryCode.Drive: f = new TextFact(TextMemoryKinds.Talked, "outing", m.Day, TextMemoryTopics.Outings[5]); return true;
                    case MemoryCode.Workout: f = new TextFact(TextMemoryKinds.Talked, "outing", m.Day, TextMemoryTopics.Outings[6]); return true;
                    case MemoryCode.Business: f = new TextFact(TextMemoryKinds.Talked, "outing", m.Day, TextMemoryTopics.Outings[7]); return true;
                    case MemoryCode.DroppedBy: f = new TextFact(TextMemoryKinds.Talked, "visit", m.Day); return true;
                    case MemoryCode.Posted: f = new TextFact(TextMemoryKinds.Talked, "tagged", m.Day); return true;
                    case MemoryCode.Lift: f = new TextFact(TextMemoryKinds.Favour, "lift", m.Day); return true;
                    case MemoryCode.Moving: f = new TextFact(TextMemoryKinds.Favour, "moving", m.Day); return true;
                    case MemoryCode.Loan: f = new TextFact(TextMemoryKinds.Favour, "borrowed", m.Day); return true;
                    case MemoryCode.Borrowed: f = new TextFact(TextMemoryKinds.Favour, "lent", m.Day); return true;
                    case MemoryCode.NotRepaid: f = new TextFact(TextMemoryKinds.Favour, "not_repaid", m.Day); return true;
                    case MemoryCode.Birthday: f = new TextFact(TextMemoryKinds.Favour, "birthday", m.Day); return true;
                    case MemoryCode.Introduced: f = new TextFact(TextMemoryKinds.Favour, "introduced", m.Day); return true;
                    case MemoryCode.StoodUp: f = new TextFact(TextMemoryKinds.Tone, "stood_up", m.Day); return true;
                    case MemoryCode.Late: f = new TextFact(TextMemoryKinds.Tone, "late", m.Day); return true;
                    case MemoryCode.Rude: f = new TextFact(TextMemoryKinds.Tone, "rude", m.Day); return true;
                    case MemoryCode.AskedOut: f = new TextFact(TextMemoryKinds.Tone, "asked_out", m.Day); return true;
                    case MemoryCode.BreakUp: f = new TextFact(TextMemoryKinds.Tone, "broke_up", m.Day); return true;
                    case MemoryCode.Arrest: f = new TextFact(TextMemoryKinds.Event, "arrest", m.Day); return true;
                    case MemoryCode.Crash: f = new TextFact(TextMemoryKinds.Event, "crash", m.Day); return true;
                    default: return false;
                }
            }
        }
    }
}
