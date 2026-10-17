using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Fame
{
    // Game-free: EconomyCheck compiles this file as it is.

    /// <summary>The four parts of what the city thinks of the player. Saved as a number: new ones go at the end.</summary>
    public enum FamePart
    {
        Customers = 0,       // quality, prices, satisfaction
        Workers = 1,         // pay, standing, strikes
        Neighbourhood = 2,   // jobs brought against rents raised, district by district
        Integrity = 3        // bribes, promises, help refused in front of others, good deeds
    }

    /// <summary>What moved a part, as the Standing page says it. Saved as a number: new ones go at the end.</summary>
    public enum FameCause
    {
        None = 0,
        Satisfaction = 1,
        Staff = 2,
        Strike = 3,
        Jobs = 4,
        Rents = 5,
        GoodDeed = 6,
        Refused = 7,         // walked past somebody who needed help, and was seen
        Reported = 8,        // ...and it was in the paper
        Visit = 9,
        Interview = 10,
        Show = 11,
        Magazine = 12,
        PromiseKept = 13,
        PromiseBroken = 14,
        Complaint = 15,
        Praise = 16,
        Inspection = 17,
        Sponsor = 18,
        Charity = 19,
        Rescue = 20,         // a struggling business saved
        Answered = 21,       // a public complaint answered
        Ignored = 22,        // ...or left
        Bribe = 23,
        Scandal = 24,
        Event = 25,
        Lobby = 26,
        Partnership = 27,
        Forgotten = 28,
        Press = 29,
        Office = 30          // standing for office and holding it (Civic)
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
        Newcomer = 0,
        QuietMoney = 1,      // rich, and hardly anybody knows the name
        LocalFavourite = 2,  // known about town and thought well of
        WellKnown = 3,
        Admired = 4,         // famous, and liked for it
        Notorious = 5        // famous, and not liked at all
    }

    /// <summary>What somebody in the street does on seeing the player.</summary>
    public enum FameReaction
    {
        None = 0,
        Look = 1,
        Greeting = 2,
        Praise = 3,
        Remark = 4,          // about the money
        Rents = 5,           // about the rents in the district
        Complaint = 6        // about the business
    }

    /// <summary>One district as the player stands in it.</summary>
    public sealed class FameDistrict
    {
        public string Name = string.Empty;    // the game's own name for it
        public int Opinion = FameRules.Start; // 0 to 100

        // ---- worked out on the daily round; not saved
        public int Known;                     // recognition in the district, 0 to 100
        public int Jobs;                      // the player's staff who work here
        public int Shops;
        public int Raised;                    // homes let here above the market's rent
        public int Brand;                     // the best of the player's brands' recognition here, 0 to 100
    }

    /// <summary>A change worth telling. The newest few are kept for the Standing page.</summary>
    public sealed class FameNote
    {
        public const int Recognition = -1;

        public int Day;
        public int Part;                      // a FamePart, or Recognition
        public FameCause Cause;
        public int Delta;                     // points; for recognition, tenths of the press
        public string District;               // or null
    }

    /// <summary>One of the player's businesses as the daily round reads it.</summary>
    public struct FameShop
    {
        public string Place;
        public string District;
        public bool Figures;                  // the game has satisfaction figures for it
        public int Satisfaction;              // the game's overall figure, 0 to 100
        public int Pricing;                   // ...and its pricing part
        public int Staff;
        public int Standing;                  // the staff's average, 0 to 100; -1 when the company does not know them
        public int Low;                       // how many of them stand low
        public bool Strike;                   // now or in the last week
        public int Interior;                  // points its interior moves its figure, -2 to 2 (Interiors)
    }

    /// <summary>One building the player owns and lets, as the daily round reads it.</summary>
    public struct FameHolding
    {
        public string District;
        public bool Let;
        public int Ask;                       // the rent asked, thousandths of the market's
        public int Rent;                      // dollars a day as agreed with the tenant; 0 when the game lets it
    }

    /// <summary>What the daily round reads from the game and the other features.</summary>
    public sealed class FameInputs
    {
        public long NetWorth;                 // the game's own figure
        public long Weekly;                   // what the businesses and the lettings brought in the last seven days
        public int Followers;                 // Flare's; 0 without it
        public int Properties;
        public readonly List<FameShop> Shops = new List<FameShop>(16);
        public readonly List<FameHolding> Holdings = new List<FameHolding>(8);
        public readonly Dictionary<string, int> Brand = new Dictionary<string, int>(8, StringComparer.Ordinal);   // district: best brand recognition

        public void Clear()
        {
            NetWorth = Weekly = 0;
            Followers = Properties = 0;
            Shops.Clear();
            Holdings.Clear();
            Brand.Clear();
        }
    }

    /// <summary>What a daily round changed, for its log line and the day's one notice.</summary>
    public struct FameDayReport
    {
        public int WealthBefore, WealthAfter;
        public int RecognitionBefore, RecognitionAfter;
        public int OverallBefore, OverallAfter;
        public int TierBefore, TierAfter;
        public int Moved;                     // points the four parts moved, all together
    }

    /// <summary>
    /// Standing in a save. Only what cannot be read again from the game is saved (<see cref="FameStore"/>); the rest
    /// is worked out again on the daily round.
    /// </summary>
    public sealed class FameFile
    {
        public int Seed;
        public int StartDay;
        public int SettledDay = -1;           // the last day the daily round was played for

        public int Wealth;                    // 0 to 100
        public int Recognition;               // 0 to 100, city-wide
        public int Press;                     // tenths of a point of recognition, 0 to PressMost
        public long NetWorth, Weekly;         // as last read
        public int Followers, Businesses, Properties, Staff;
        public readonly int[] Rep = { FameRules.Start, FameRules.Start, FameRules.Start, FameRules.Start };
        public readonly int[] Target = { FameRules.Start, FameRules.Start, FameRules.Start, FameRules.Start };   // not saved

        // ---- the day's cap: points moved today by part, either way
        public readonly int[] Moved = new int[4];
        public int MovedDay = -1;

        public readonly List<FameDistrict> Districts = new List<FameDistrict>(8);
        public readonly List<FameNote> Notes = new List<FameNote>(FameRules.NotesMost);

        public int StreetDay = -1, StreetToday;
        public int StreetMinute = -10000;     // game minute of the day of the last reaction
        public int ShopDay = -1, ShopToday;
        public int VisitDay = -1;
        public readonly List<string> Visited = new List<string>(4);
        public int NoticeDay = -1;            // the day the one routine notice was shown

        // ---- the press (saved under its own key)
        public int NextId = 1;
        public int ReporterDay = -100, PhotoDay = -100, InviteDay = -100, StoryDay = -1, RefusalDay = -100;
        public int Milestones;                // bits: what the papers have told of
        public bool MilestonesSeen;           // what there was when the file was opened is no news
        public int ConditionsDay = -100, TrustDay = -100, JobsDay = -100, RentsDay = -100;   // the last piece of each kind
        public readonly List<FameInvite> Invites = new List<FameInvite>(FamePress.InvitesMost);
        public readonly List<FamePromise> Promises = new List<FamePromise>(FamePress.PromisesMost);

        // ---- the city (saved under its own key)
        public int RequestDay = -100, CharityDay = -1, ScandalUntil = -1, LobbyRest = -100, LobbyCredit;
        public readonly List<FameRequest> Requests = new List<FameRequest>(FameCity.RequestsMost);
        public readonly List<FameBribe> Bribes = new List<FameBribe>(FameCity.BribesMost);
        public readonly List<FameLobby> Lobbies = new List<FameLobby>(FameCity.LobbiesMost);
        public readonly List<FameBoost> Boosts = new List<FameBoost>(FameCity.BoostsMost);

        public int Overall => FameRules.Overall(Rep);

        public FameTier Tier => FameRules.TierOf(Recognition);

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

        /// <summary>A district, added the first time it is asked for, up to <see cref="FameRules.DistrictsMost"/>.</summary>
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
