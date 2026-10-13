using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Fame
{
    // Everything in this file is free of the game and of the engine: RealLifeOverhaul.Build/EconomyCheck compiles it as
    // it is and runs it outside the game.

    /// <summary>The four parts of what the city thinks of the player. Saved as a number: new ones go at the end.</summary>
    public enum FamePart
    {
        Customers = 0,       // quality, prices, satisfaction: what the player's customers make of the businesses
        Workers = 1,         // pay, standing, strikes: what it is like to work for the player
        Neighbourhood = 2,   // jobs brought against rents raised, district by district
        Integrity = 3        // bribes, promises, help refused in front of others, good deeds
    }

    /// <summary>What moved a part, as the Standing page says it. Saved as a number: new ones go at the end.</summary>
    public enum FameCause
    {
        None = 0,
        Satisfaction = 1,    // the customers' own figures, day by day
        Staff = 2,           // the staff's standing, day by day
        Strike = 3,
        Jobs = 4,            // a district's jobs against its rents, day by day
        Rents = 5,
        GoodDeed = 6,
        Refused = 7,         // walked past somebody who needed help, and was seen
        Reported = 8,        // ...and it was in the paper
        Visit = 9,           // a visit to one of the player's businesses
        Interview = 10,
        Show = 11,
        Magazine = 12,
        PromiseKept = 13,
        PromiseBroken = 14,
        Complaint = 15,      // a complaint at a business, answered or not
        Praise = 16,
        Inspection = 17,
        Sponsor = 18,
        Charity = 19,
        Rescue = 20,         // a struggling business saved
        Answered = 21,       // a public complaint answered
        Ignored = 22,        // ...or left
        Bribe = 23,
        Scandal = 24,
        Event = 25,          // a business or city event attended
        Lobby = 26,
        Partnership = 27,
        Forgotten = 28,      // time: the city forgets slowly
        Press = 29           // what the papers wrote
    }

    /// <summary>How well the city knows the player, by their recognition.</summary>
    public enum FameTier
    {
        Unknown = 0,
        Local = 1,
        Known = 2,
        Prominent = 3,
        Famous = 4
    }

    /// <summary>The three parts together, in a word: what the Standing page calls the player.</summary>
    public enum FameProfile
    {
        Newcomer = 0,        // nobody much yet
        QuietMoney = 1,      // rich, and hardly anybody knows the name
        LocalFavourite = 2,  // known about town and thought well of
        WellKnown = 3,       // known, and nobody minds
        Admired = 4,         // famous, and liked for it
        Notorious = 5        // famous, and not liked at all
    }

    /// <summary>What somebody in the street does on seeing the player.</summary>
    public enum FameReaction
    {
        None = 0,
        Look = 1,            // a second look
        Greeting = 2,        // a smile and a nod
        Praise = 3,          // a good word
        Remark = 4,          // a remark about the money
        Rents = 5,           // a complaint about the rents in the district
        Complaint = 6        // a cold look and a word about the business
    }

    /// <summary>One district as the player stands in it.</summary>
    public sealed class FameDistrict
    {
        public string Name = string.Empty;   // the game's own name for it
        public int Opinion = FameRules.Start; // 0 to 100: jobs here, rents there

        // ---- worked out on the daily round from what was read; not saved
        public int Known;                    // recognition in the district, 0 to 100
        public int Jobs;                     // the player's staff who work here
        public int Shops;                    // the player's businesses here
        public int Raised;                   // homes let here above the market's rent
        public int Brand;                    // the best of the player's brands' recognition here, 0 to 100
    }

    /// <summary>A change worth telling: what moved, by how much and why. The newest few are kept for the Standing page.</summary>
    public sealed class FameNote
    {
        public int Day;
        public int Part;                     // a FamePart, or Recognition
        public FameCause Cause;
        public int Delta;                    // points; for recognition tenths of the press
        public string District;              // the district it was in, or null

        public const int Recognition = -1;
    }

    /// <summary>One of the player's businesses as the daily round reads it.</summary>
    public struct FameShop
    {
        public string Place;
        public string Name;
        public string District;
        public bool Figures;                 // the game has satisfaction figures for it
        public int Satisfaction;             // the game's overall figure, 0 to 100
        public int Pricing;                  // ...and its pricing part
        public int Staff;                    // the company's count of its staff
        public int Standing;                 // ...their standing, an average, 0 to 100 (-1: the company does not know them)
        public int Low;                      // ...how many of them stand low
        public bool Strike;                  // a strike there now or in the last week
    }

    /// <summary>One building the player owns and lets, as the daily round reads it.</summary>
    public struct FameHolding
    {
        public string District;
        public bool Let;                     // a tenant is in
        public int Ask;                      // the rent asked against the market's, thousandths (1000 is the market's)
        public int Rent;                     // dollars a day, as agreed with the tenant (0: the game lets it, and its rent is in no statement of the mod's)
    }

    /// <summary>What the daily round reads from the game and the other features. The director fills it; the checks make their own.</summary>
    public sealed class FameInputs
    {
        public long NetWorth;                // the game's own figure of what the player is worth
        public long Weekly;                  // what the businesses and the lettings brought in the last seven days
        public int Followers;                // Flare's followers; 0 without it
        public int Properties;               // buildings the player owns
        public readonly List<FameShop> Shops = new List<FameShop>(16);
        public readonly List<FameHolding> Holdings = new List<FameHolding>(8);
        public readonly Dictionary<string, int> Brand = new Dictionary<string, int>(8, StringComparer.Ordinal);   // district: the best brand recognition there

        public void Clear()
        {
            NetWorth = Weekly = 0;
            Followers = Properties = 0;
            Shops.Clear();
            Holdings.Clear();
            Brand.Clear();
        }
    }

    /// <summary>What a daily round changed, for its one line in Player.log and the day's one notice.</summary>
    public struct FameDayReport
    {
        public int WealthBefore, WealthAfter;
        public int RecognitionBefore, RecognitionAfter;
        public int OverallBefore, OverallAfter;
        public int TierBefore, TierAfter;
        public int Moved;                    // points the four parts moved, all together
    }

    /// <summary>
    /// Standing in a save: the three parts (wealth, recognition, reputation in four), the districts, the newest changes and
    /// the few dates that keep what it does rare. Everything that is worked out each day is worked out again on the daily
    /// round, so only what cannot be read again is saved (FameStore).
    /// </summary>
    public sealed class FameFile
    {
        public int Seed;
        public int StartDay;
        public int SettledDay = -1;          // the last day the daily round was played for

        // ---- the three parts
        public int Wealth;                   // 0 to 100
        public int Recognition;              // 0 to 100, city-wide
        public int Press;                    // tenths of a point of recognition from what the papers and the television made of the player, 0 to PressMost
        public long NetWorth, Weekly;        // as last read
        public int Followers, Businesses, Properties, Staff;
        public readonly int[] Rep = { FameRules.Start, FameRules.Start, FameRules.Start, FameRules.Start };
        public readonly int[] Target = { FameRules.Start, FameRules.Start, FameRules.Start, FameRules.Start };   // where each is heading, as the last round found it; not saved

        // ---- what moved today, for the day's cap: points by part, either way
        public readonly int[] Moved = new int[4];
        public int MovedDay = -1;

        public readonly List<FameDistrict> Districts = new List<FameDistrict>(8);
        public readonly List<FameNote> Notes = new List<FameNote>(FameRules.NotesMost);

        // ---- the street, the shops, the staff: dates that keep it rare
        public int StreetDay = -1, StreetToday;    // reactions in the street today
        public int StreetMinute = -10000;          // game minute of the day of the last one
        public int ShopDay = -1, ShopToday;        // customers recognising the player in their shops today
        public int VisitDay = -1;                  // the day the staff visits below were made
        public readonly List<string> Visited = new List<string>(4);
        public int NoticeDay = -1;                 // the day the one routine notice was shown

        // ---- the press (FamePress, saved under its own key)
        public int NextId = 1;
        public int ReporterDay = -100, PhotoDay = -100, InviteDay = -100, StoryDay = -1, RefusalDay = -100;
        public int Milestones;                     // bits: what the papers have told of (FamePress.MilestoneFrom)
        public bool MilestonesSeen;                // what there was when the file was opened is known: it is no news
        public int ConditionsDay = -100, TrustDay = -100, JobsDay = -100, RentsDay = -100;   // the last piece of each kind
        public readonly List<FameInvite> Invites = new List<FameInvite>(FamePress.InvitesMost);
        public readonly List<FamePromise> Promises = new List<FamePromise>(FamePress.PromisesMost);

        public int Overall => FameRules.Overall(Rep);

        public FameInvite Invite(int id)
        {
            for (int i = 0; i < Invites.Count; i++)
            {
                if (Invites[i].Id == id)
                {
                    return Invites[i];
                }
            }
            return null;
        }

        public FameTier Tier => FameRules.TierOf(Recognition);

        public FameDistrict District(string name)
        {
            for (int i = 0; name != null && i < Districts.Count; i++)
            {
                if (string.Equals(Districts[i].Name, name, StringComparison.Ordinal))
                {
                    return Districts[i];
                }
            }
            return null;
        }

        /// <summary>A district, added the first time it is asked for (a few at most: the city has a handful).</summary>
        public FameDistrict DistrictOrNew(string name)
        {
            FameDistrict d = District(name);
            if (d == null && !string.IsNullOrEmpty(name) && Districts.Count < FameRules.DistrictsMost)
            {
                d = new FameDistrict { Name = name };
                Districts.Add(d);
            }
            return d;
        }
    }
}
