using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>How close somebody is. Family (Mom) is a friend who can never be less.</summary>
    public enum SocialStage { Stranger, Acquaintance, Friend, CloseFriend, Dating, Partner }

    /// <summary>Where somebody is met; one person of the cast per origin. Saved as a number: new ones go at the end.</summary>
    public enum SocialOrigin { Family, OldFriend, Gym, Work, CoffeeCart, Neighbour, Helped, Customer, Online, Event, Street }

    /// <summary>What a person is like. Two at a time, from <see cref="SocialCatalogue.TraitPairs"/>; saved as a number.</summary>
    [Flags]
    public enum SocialTrait
    {
        None = 0,
        Punctual = 1,      // early themselves; minds lateness twice as much
        Late = 2,          // often late; does not mind yours
        Generous = 4,      // offers to pay; lends more
        Careful = 8,       // splits everything; lends little
        Sporty = 16,       // workouts and walks
        Foodie = 32,       // meals and coffee
        NightOwl = 64,     // drinks and films; free late
        Homebody = 128,    // coffee, walks, your shop; cancels more
        Reliable = 256,    // never cancels; always repays
        Flaky = 512,       // cancels; repays when it suits
        Supportive = 1024, // kind about arrests and money
        Envious = 2048     // not kind about money
    }

    /// <summary>What a plan is for: the eight outings, and the two favours that are played on the street.</summary>
    public enum PlanKind { Coffee, Meal, Drinks, Cinema, Walk, Drive, Workout, Business, Lift, Moving }

    /// <summary>Something done together or done to them: the short list each person keeps. Saved as a number.</summary>
    public enum MemoryCode
    {
        None, Met, Coffee, Meal, Drinks, Cinema, Walk, Drive, Workout, Business, Lift, Moving, Loan, Borrowed, Birthday,
        StoodUp, Late, Rude, Arrest, Crash, AskedOut, Official, MovedIn, BreakUp, Introduced, DroppedBy, NotRepaid,
        Posted     // tagged in a post of the player's on Flare
    }

    /// <summary>One entry of a person's memories: what, and on which game day.</summary>
    public struct SocialMemory
    {
        public MemoryCode Code;
        public int Day;
    }

    /// <summary>One person of the cast, rolled once a save and then stored: who they are, and how things stand with the player.</summary>
    public sealed class SocialPerson
    {
        public const int MaxMemories = 6;

        public int Index;
        public string Name;
        public bool Female;
        public int Age;              // years
        public int Seed;             // of their look; a kept look's own (People/PersonLook.SeedOf)
        public SocialOrigin Origin;
        public SocialTrait Traits;
        public int Job;              // index into SocialCatalogue.Jobs
        public int District;         // index into the game's neighbourhoods
        public int FreeFrom = 17;    // hours of a weekday they can be met between; weekends are 10 to 23 for everyone
        public int FreeTo = 23;
        public int Birthday = 1;     // day of the year, 1 to the game's days per year
        public bool Compatible;      // a romance is possible

        public bool Met;
        public int MetDay;
        public float Familiarity, Affection, Trust;   // 0 to 100
        public SocialStage Stage;
        public int StageDay;
        public bool MovedIn;
        public bool Drifted;
        public int Mood;             // -2 a rough day to +2 a great one; rolled each morning
        public int LastContactDay;
        public int LastCallDay;
        public int LastTextDay;
        public int LastOutingDay;
        public int Shared;           // things done together
        public int Dates;            // outings since they started dating
        public PlanKind Wants = PlanKind.Coffee;
        public int WantsDay;
        public string LastSaid = string.Empty;   // as it was shown
        public int LastTopic = -1;
        public int AsksInWindow;     // favours the player asked of them lately
        public int AskDay;
        public int Debt;             // what the player owes them
        public int DebtDay;
        public int Loan;             // what they owe the player
        public int LoanDue;          // negative once they have made their one excuse
        public int BirthdayYear = -1;   // the year (day / days per year) in which the player last remembered it
        public string Look;          // the look they had when met in the street (People/PersonLook's text); null: the seed's
        public readonly List<SocialMemory> Memories = new List<SocialMemory>(MaxMemories);

        public bool Has(SocialTrait trait) => (Traits & trait) != 0;
        public bool IsFamily => Origin == SocialOrigin.Family;
        public bool Together => Stage == SocialStage.Dating || Stage == SocialStage.Partner;

        /// <summary>One figure for the whole of it, 0 to 100.</summary>
        public int Closeness => (int)Math.Round((Familiarity + Affection + Trust) / 3f);

        public void Remember(MemoryCode code, int day)
        {
            if (code == MemoryCode.None)
            {
                return;
            }
            if (Memories.Count >= MaxMemories)
            {
                Memories.RemoveAt(0);
            }
            Memories.Add(new SocialMemory { Code = code, Day = day });
        }

        public bool Remembers(MemoryCode code)
        {
            for (int i = 0; i < Memories.Count; i++)
            {
                if (Memories[i].Code == code)
                {
                    return true;
                }
            }
            return false;
        }

        public bool FreeAt(int hour, bool weekend)
        {
            int from = weekend ? 10 : FreeFrom;
            int to = weekend ? 23 : FreeTo;
            return from <= to ? hour >= from && hour < to : hour >= from || hour < to;
        }

        /// <summary>Nobody answers the phone or texts in the small hours; night owls keep later ones.</summary>
        public bool Asleep(int hour)
        {
            return Has(SocialTrait.NightOwl) ? hour >= 2 && hour < 10 : hour >= 23 || hour < 8;
        }
    }

    /// <summary>The plan that is on: who, what, where and when. One at a time.</summary>
    public sealed class SocialPlan
    {
        public int Person = -1;
        public PlanKind Kind;
        public float Time;           // game minutes it is agreed for
        public string Street;        // the venue's address (the game's own street name), or null
        public int Number;
        public string Place;         // what the place is called, as it was agreed
        public float X, Y, Z;        // where they wait
        public bool TheirIdea;       // they asked: they are readier to pay
        public bool Reminded;        // the hour-before reminder went out
        public bool Checked;         // whether they cancel has been rolled
        public float Arrives;        // game minutes at which they get there
        public string ToStreet;      // a lift's destination, a move's new address
        public int ToNumber;
        public bool Active => Person >= 0;

        public void Clear()
        {
            Person = -1;
            Street = null;
            Place = null;
            ToStreet = null;
            Reminded = false;
            Checked = false;
            TheirIdea = false;
        }
    }

    /// <summary>Something they asked that waits for an answer when the player next speaks to them. One at a time.</summary>
    public sealed class SocialOffer
    {
        public const int Loan = 100;   // Kind for a loan; anything else is a PlanKind

        public int Person = -1;
        public int Kind;
        public int Amount;
        public float Expires;        // game minutes
        public bool Active => Person >= 0;

        public void Clear()
        {
            Person = -1;
            Amount = 0;
        }
    }
}
