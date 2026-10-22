using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Street
{
    /// <summary>What the player can say to somebody in the street, in the order the choices are offered.</summary>
    public enum StreetAsk
    {
        SmallTalk,    // the weather, the evening, the street
        AboutThem,    // what they do, where they live
        Name,         // the player gives their name and asks theirs
        Area,         // what is good around here: the businesses near, the street event of the day, the news
        Directions,   // a business of a kind near: its name, its street, the way, and the game's destination marker
        Compliment,
        Joke,
        Flirt,
        Number,       // swap numbers: a yes makes them somebody the player knows (the social life, met in the street)
        Invite,       // a coffee (or dinner after a flirt that went well), decided as an invitation on Flare is
        Election,     // only while a city election is being fought
        Economy,      // prices and the bank rate
        Business,     // the player's own business: word of mouth, a small part of its customers for a few days
        Say,          // the player's own words, in the game's text box, read with fixed rules (the panel's grey button, not a choice of the round)
        Rude,
        Day,          // how their day is going: answered by today's mood
        Event,        // the street event of the day, while there is one
        Work,         // after they said what they do: whether they like it (or, retired, whether they miss it)
        Thanks,       // after they showed the way or said what is good around here
        Bye,          // goodbye, as a choice of the round (the game's cancel key says it too)
        Count
    }

    /// <summary>Every kind of line a stranger says; each has its written lines (StreetLines) in three languages.</summary>
    public enum StreetLine
    {
        OpenWarm, OpenFine, OpenCool, OpenAgain, OpenFamous, OpenInfamous,
        BrushHurry, BrushWork, BrushNight, BrushRain, BrushMood, BrushRude, BrushUnwashed, BrushRecord, BrushWanted, BrushPhone,
        Waiting,
        SmallWarm, SmallCool,
        AboutJob, AboutRetired, AboutCool,
        NameGive, NameWithhold, NameKnown,
        AreaPlaces, AreaEvent, AreaNews, AreaNothing,
        DirAsk, DirFound, DirNone, DirFar,
        ComplimentYes, ComplimentNo,
        JokeYes, JokeNo,
        FlirtYes, FlirtNo, FlirtTaken,
        NumberYes, NumberNo, NumberTaken, NumberAlready, NumberLater,
        InviteYes, InviteMaybeStranger, InviteMaybeBusy, InviteMaybeNight, InviteMaybeLater, InviteMaybeNowhere, InviteMaybePlan,
        InviteNoTaken, InviteNoDislikes, InviteNo,
        ElectionFor, ElectionUnsure, ElectionNone,
        EconomyUp, EconomyDown, EconomyFlat,
        BusinessHeard, BusinessWill, BusinessNo, BusinessAgain,
        SaidWarm, SaidCool, SaidOdd,
        RudeCold, WalkOff, ThreatLeave,
        Tired, Bye, ByeCool,
        DayGood, DayFine, DayLow,
        EventGoing, EventNot,
        WorkLikes, WorkMixed, WorkRetired,
        Welcome, GreetBack,
        Count
    }

    /// <summary>Why somebody has no time for the player. None: they talk.</summary>
    public enum StreetBrush { None, Hurry, Work, Night, Rain, Mood, Rude, Unwashed, Record, Wanted, Phone }

    /// <summary>A place a stranger can be asked the way to: the businesses of the city by their kind.</summary>
    public enum StreetPlace { Coffee, Food, Drinks, Grocery, Clothes, Gym, Cinema, Bank, Count }

    /// <summary>
    /// Somebody in the street, worked out from a seed of the save, the day and the game's figure, so they are the same all
    /// day; their sex and age are the figure's own. Game-free.
    /// </summary>
    public sealed class StreetStranger
    {
        public int Key;            // the figure the game shows (its instance): one look, one person, for the session
        public int Seed;           // of the save, the day and the figure
        public bool Female;
        public int Age;            // years, from the game's own figure
        public string First, Last;
        public int Job;            // into SocialCatalogue.Jobs (the last is "retired")
        public int District;       // where they live, a district of the game's
        public SocialTrait Traits;
        public int Mood;           // -2 a rough day ... +2 a great one: today's
        public int Style;          // into StreetRules.Styles: how they talk
        public bool Compatible;    // a romance would be possible, if the player wants one
        public bool Taken;         // with somebody else
        public bool Commuter;      // on their way to or from work at the rush hours
        public bool Humour;        // laughs at a joke
        public bool Chatty;        // talks a while longer
        public string Handle;      // as their Flare handle would be: the card of the city's people needs one

        public string Name => First + " " + Last;
    }

    /// <summary>What somebody remembers of the player today. Kept for the day only (StreetMemory); nothing is saved.</summary>
    public sealed class StreetMemo
    {
        public int Key;
        public int Day;
        public int Talks;          // conversations today
        public int Rapport;        // how it went, summed over today: -10 to 10
        public bool Rude;          // the player was rude or threatening
        public bool Named;         // they told the player their name
        public bool Swapped;       // numbers swapped (they are in the player's People now)
        public bool Invited;       // an invitation was answered (once a day)
        public bool Mentioned;     // the player's business was talked about (once a day)
        public bool Flirted, Joked, Complimented;
        public int Cast = -1;      // their place in the social life once met
        public bool Told;          // the player told them their name today (they may use it)
        public readonly List<string> Lines = new List<string>(StreetLines.SaidMost);   // the written lines they said today, newest last: none is said twice

        /// <summary>Remembers a written line for the day, letting the oldest go past <see cref="StreetLines.SaidMost"/>.</summary>
        public void Said(string key)
        {
            if (Lines.Count >= StreetLines.SaidMost)
            {
                Lines.RemoveAt(0);
            }
            Lines.Add(key);
        }
    }

    /// <summary>What the game knows when somebody is spoken to (filled by StreetGame). Game-free; kept and refilled.</summary>
    public sealed class StreetFacts
    {
        public int Day, Hour, Minute;
        public bool Weekend;
        public bool Raining;
        public int Standing = 55;        // the public's view of the player, 0 to 100 (55 a newcomer)
        public int Recognition;          // how well the city knows the player, 0 to 100
        public bool Record;              // a record somebody would mind (Wiring.Standing.HasRecord)
        public bool Wanted;              // a warrant out for the player
        public bool Unwashed;            // the player's hygiene is low
        public bool PoliceNear;          // an officer is near enough to hear
        public bool SocialOn;            // the social life runs (numbers and invitations can be had)
        public bool Election;
        public readonly List<string> Candidates = new List<string>(4);
        public int PriceTrend;           // -1 falling, 0 flat, 1 rising
        public int RateMove;             // the bank rate: -1 down, 0 not, 1 up
        public string EventToday;        // the street event's words today, or null
        public string NewsDistrict;      // where the day's latest crime or accident was, in the player's language, or null
        public string Player;            // the player's name
        public readonly List<string> Shops = new List<string>(4);   // the player's own businesses, by name
        public string EventWords;        // RLO Texts' card (data v6): the street event in the card's English words, whatever the game's language

        public void Clear()
        {
            Day = Hour = Minute = 0;
            Weekend = Raining = Record = Wanted = Unwashed = PoliceNear = SocialOn = Election = false;
            Standing = 55;
            Recognition = PriceTrend = RateMove = 0;
            EventToday = NewsDistrict = Player = EventWords = null;
            Candidates.Clear();
            Shops.Clear();
        }
    }

    /// <summary>How the approach went: whether they talk, and if not why; whether they know who the player is.</summary>
    public struct StreetApproach
    {
        public StreetBrush Brush;
        public bool Recognised;
        public bool Famed;               // they know the player's face as a well-known or a disliked business owner (and so their name)
        public int Warmth;               // -3 to 3: today's mood with what they make of the player
        public int Patience;             // turns they give the player
        public StreetLine Opening;
    }

    /// <summary>What a choice of the player's came to: the line, what it did to the rapport, and whether they walk off.</summary>
    public struct StreetOutcome
    {
        public StreetLine Line;
        public int Rapport;
        public bool Ends;                // they go (a walk off, a threat, a goodbye)
        public bool Police;              // an officer heard a threat
    }

    /// <summary>One conversation: who, what they remember, how far it has got. Made when the player presses Talk.</summary>
    public sealed class StreetChat
    {
        public StreetStranger Who;
        public StreetMemo Memo;
        public StreetApproach Approach;
        public int Turns;
        public int Rapport;              // this conversation's
        public readonly bool[] Done = new bool[(int)StreetAsk.Count];
        public readonly List<string> History = new List<string>(6);   // "P:words" and "S:words": the last few lines, for the card
        public int Menu;                 // where "Something else" has got to (back to the first after every answer)
        public int PlaceAt;              // where "Somewhere else" has got to
        public int Earned;               // turns earned back by good exchanges (StreetRules.EarnedMost at most)
        public StreetLine Last = StreetLine.Count;   // the line they said last (the next round's follow-ups come from it); Count before any
        public string Heard;             // how their last line's words came (RLO Texts' words or the written line, and why), for the turn's log line
        public double Waits;             // real seconds their line asked of RLO Texts waits at most (StreetRules.Hold, or longer for the player's AI service: TextsHub.SpokenWait)
        public string WaitFor;           // RLO Texts' own: a guess of the next line the AI service is still writing, waited for as that line (TextsHub.Adopt)
        public Action Speaking;          // the director's: the words of the line they are asked for have begun to come (the AI service): they are about to speak
        public bool Shared;              // they said what they do (asking whether they like it follows)
        internal readonly int[] Ranks = new int[(int)StreetAsk.Count];   // each choice's place in the round being built (StreetRules.Choices)

        /// <summary>Adds a turn's rapport; a good exchange earns a turn back, up to StreetRules.EarnedMost.</summary>
        public void Gain(int rapport)
        {
            Rapport += rapport;
            if (rapport > 0 && Earned < StreetRules.EarnedMost)
            {
                Earned++;
            }
        }

        public void Said(bool player, string words)
        {
            if (string.IsNullOrEmpty(words))
            {
                return;
            }
            if (History.Count >= 6)
            {
                History.RemoveAt(0);
            }
            History.Add((player ? "P:" : "S:") + words);
        }
    }
}
