using System;
using RealLifeOverhaul.Street;

namespace RealLifeOverhaul.Texts
{
    /// <summary>The words of what a person remembers talking with the player about, for "talked" memory lines. Game-free. COST: a switch.</summary>
    public static class TextMemoryTopics
    {
        /// <summary>The words of an outing with somebody of the social life, by the kind the social life keeps.</summary>
        public static readonly string[] Outings = { "coffee", "dinner", "drinks", "a film", "a walk", "a drive", "the gym", "a look round your business" };

        /// <summary>What a text's card was about, in a memory line's words; null for one not worth remembering.</summary>
        public static string Text(string intent)
        {
            if (intent == null)
            {
                return null;
            }
            if (intent.StartsWith("friend.invite.", StringComparison.Ordinal) || intent == "friend.remind")
            {
                return "meeting up";
            }
            if (intent.StartsWith("friend.cancel.", StringComparison.Ordinal))
            {
                return "calling off a plan";
            }
            if (intent.StartsWith("friend.news.", StringComparison.Ordinal) || intent == "friend.reply_how" || intent == "mom.reply_how")
            {
                return "their day";
            }
            if (intent.StartsWith("friend.gossip", StringComparison.Ordinal))
            {
                return "what they heard about you";
            }
            if (intent.EndsWith(".crash", StringComparison.Ordinal))
            {
                return "your crash";
            }
            if (intent.EndsWith(".arrest", StringComparison.Ordinal) || intent == "mom.news.arrest")
            {
                return "your arrest";
            }
            if (intent.EndsWith(".court", StringComparison.Ordinal))
            {
                return "your court case";
            }
            switch (intent)
            {
                case "friend.stood_up":
                case "partner.stood_up": return "being stood up";
                case "friend.thanks": return "your time together";
                case "friend.birthday_own":
                case "friend.birthday_missed": return "their birthday";
                case "friend.missed_call": return "a missed call";
                case "friend.ask_loan":
                case "friend.loan_back":
                case "friend.loan_excuse":
                case "friend.debt": return "money";
                case "friend.drift":
                case "mom.worry":
                case "partner.missing": return "keeping in touch";
                case "friend.rich":
                case "friend.congrats_open":
                case "mom.news.opened": return "your business";
                case "friend.breakin": return "the break-in";
                case "friend.introduce": return "a friend of theirs";
                case "friend.lift": return "a lift";
                case "friend.graduated":
                case "mom.news.graduated": return "your graduation";
                case "friend.listed":
                case "mom.news.listed": return "your company";
                case "friend.married":
                case "mom.news.married": return "your wedding";
                case "friend.baby":
                case "mom.news.baby":
                case "mom.news.expecting": return "your baby";
                case "friend.win": return "your big win";
                case "friend.loss":
                case "partner.loss": return "your big loss";
                case "partner.affection": return "you two";
                case "mom.visit": return "her visit";
                case "mom.holiday":
                case "mom.holiday_missed": return "a holiday";
                case "roommate.rent_late":
                case "roommate.rent_short": return "the rent";
                case "employee.raise_ask": return "a raise";
                case "employee.sick": return "being off sick";
                case "employee.raise_thanks": return "their raise";
                case "employee.hours": return "their hours";
                default:
                    return intent.StartsWith("boss.", StringComparison.Ordinal) ? "work"
                        : intent.StartsWith("lender.", StringComparison.Ordinal) ? "the money you owe" : null;
            }
        }

        /// <summary>What a line of street talk was about, in a memory's words; null for one that is no topic.</summary>
        public static string Street(StreetLine line)
        {
            switch (line)
            {
                case StreetLine.SmallWarm:
                case StreetLine.SmallCool: return "small things";
                case StreetLine.AboutJob:
                case StreetLine.AboutRetired:
                case StreetLine.WorkLikes:
                case StreetLine.WorkMixed:
                case StreetLine.WorkRetired: return "their work";
                case StreetLine.DirFound:
                case StreetLine.DirFar:
                case StreetLine.DirNone: return "the way somewhere";
                case StreetLine.AreaPlaces:
                case StreetLine.AreaEvent:
                case StreetLine.AreaNews:
                case StreetLine.AreaNothing: return "what is good around here";
                case StreetLine.ElectionFor:
                case StreetLine.ElectionUnsure:
                case StreetLine.ElectionNone: return "the election";
                case StreetLine.EconomyUp:
                case StreetLine.EconomyDown:
                case StreetLine.EconomyFlat: return "prices";
                case StreetLine.BusinessHeard:
                case StreetLine.BusinessWill:
                case StreetLine.BusinessNo:
                case StreetLine.BusinessAgain: return "your business";
                case StreetLine.DayGood:
                case StreetLine.DayFine:
                case StreetLine.DayLow: return "their day";
                case StreetLine.EventGoing:
                case StreetLine.EventNot: return "the street event";
                default: return null;
            }
        }

        /// <summary>How a line of street talk went, as a memory's tone code; null for one that says nothing of it.</summary>
        public static string Tone(StreetLine line)
        {
            switch (line)
            {
                case StreetLine.SmallWarm:
                case StreetLine.ComplimentYes:
                case StreetLine.JokeYes:
                case StreetLine.SaidWarm:
                case StreetLine.Bye: return "warm";
                case StreetLine.SmallCool:
                case StreetLine.ComplimentNo:
                case StreetLine.JokeNo:
                case StreetLine.SaidCool:
                case StreetLine.ByeCool: return "cool";
                case StreetLine.FlirtYes:
                case StreetLine.FlirtNo:
                case StreetLine.FlirtTaken: return "flirty";
                case StreetLine.RudeCold:
                case StreetLine.WalkOff: return "rude";
                case StreetLine.ThreatLeave: return "threat";
                default: return null;
            }
        }
    }
}
