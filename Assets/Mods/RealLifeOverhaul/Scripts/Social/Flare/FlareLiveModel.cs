using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What a report to the city is about. Saved as a number: new kinds go at the end.</summary>
    public enum FlareReport
    {
        Road = 0,        // a broken road, a pothole: the City Office sends a crew (a site of the council's works)
        Lamp = 1,        // a street light out: a utility crew (a site of the council's works)
        Crime = 2,       // crime in a district: the police step up patrols there
        Litter = 3,      // a dirty street: a cleaning crew in the district
        Unsafe = 4,      // an unsafe place: a building site is inspected, or the police look in
        Business = 5,    // a complaint about a business: an inspection, and the talk turns against it
        Transit = 6,     // the buses: the transit company answers
        Utilities = 7    // the power or the water: the utilities office answers
    }

    /// <summary>What an announcement for a business says. Saved as a number.</summary>
    public enum FlareAnnounce
    {
        Sale = 0,
        Hours = 1,
        Event = 2,       // an event at the business at an hour: an invitation others can say they go to
        Hiring = 3,
        New = 4
    }

    /// <summary>The city's official accounts. Saved as a number.</summary>
    public enum FlareOfficial
    {
        CityOffice = 0,
        Police = 1,
        Transit = 2,
        Utilities = 3,
        Bank = 4
    }

    /// <summary>What an official account posts of its own. Saved as a number.</summary>
    public enum FlareOfficialPost
    {
        Notice = 0,      // the City Office: the week's works, a council meeting
        Patrols = 1,     // the police: patrols in a district after crime there
        Advice = 2,      // the police: advice after a break-in
        Service = 3,     // transit: the rain slows the buses, a timetable change
        Outage = 4,      // utilities: works on the network
        Rate = 5,        // the bank: the rate moved
        Policy = 6,      // the City Office: a policy came in
        Clean = 7,       // the City Office: a cleaning round in a district
        Election = 8     // the City Office: the election's dates
    }

    /// <summary>The topics the city's people post about. Saved as a number.</summary>
    public enum FlareTalk
    {
        Weather = 0,
        Prices = 1,
        Crime = 2,
        Accident = 3,
        Event = 4,
        EventTomorrow = 5,
        Election = 6,
        Roadworks = 7,
        Market = 8,
        Commute = 9,
        Policy = 10,
        Fire = 11,
        Everyday = 12,
        Night = 13,
        Food = 14,
        Sport = 15
    }

    /// <summary>The kinds of a reply of one of the city's people. Saved as a number.</summary>
    public enum FlareReplyKind
    {
        Agree = 0,
        Kind = 1,
        Funny = 2,
        Doubt = 3,
        Angry = 4,       // a pile-on
        Love = 5,
        Question = 6
    }

    /// <summary>An account's answer in character. Saved as a number.</summary>
    public enum FlareAnswer
    {
        Received = 0,    // the City Office: logged
        Works = 1,       // a crew is sent: the works and when they end
        Patrol = 2,      // the police: patrols in the district
        Cleanup = 3,     // a cleaning crew in the district
        Busy = 4,        // the crews are busy: logged, not now
        Already = 5,     // on the list already
        Inspect = 6,     // an inspector will look at it
        Thanks = 7,      // a business: thanks for the review
        Sorry = 8,       // a business: sorry, we will do better
        Transit = 9,     // the transit company's answer
        Utilities = 10,  // the utilities office's answer
        Mention = 11,    // an official account answers being called on
        Brand = 12,      // a brand answers being called on
        Tycoon = 13,     // a rival tycoon answers an announcement in its trade
        Pressure = 14,   // after the paper: the City Office acts after all
        Site = 15,       // a building site was reported: the inspector goes
        Nothing = 16,    // the city's faults: a report of nothing, "we looked and found nothing"
        Warned = 17,     // ...nothing again: a warning
        Barred = 18,     // ...too many reports of nothing: no crew for this player's reports for a few days
        Clean = 19       // ...a business inspected and found clean: nothing against it
    }

    /// <summary>What the city's faults (Scripts/Faults, FaultVerdict's numbers) make of a report, read through FlareBridges.FaultCheck.</summary>
    public enum FaultAnswer
    {
        True = 0,        // a real fault there: the crew goes to it
        Nothing = 1,     // nothing there
        Warned = 2,      // nothing again: a warning
        Barred = 3,      // the player's reports bring no crew for now
        Clean = 4        // a business inspected and found clean
    }

    /// <summary>What the City Office did about a case. Saved as a number.</summary>
    public enum FlareOrder
    {
        None = 0,
        Works = 1,       // a site of the council's works (Ref: its id)
        Patrol = 2,      // the police in the district until a day
        Cleanup = 3,     // a cleaning crew in the district until a day
        Inspection = 4,  // a business inspected: the talk turns against it
        Answered = 5,    // an answer and nothing more (the buses, the power)
        Queued = 6,      // the crews were busy: nothing yet
        SiteCheck = 7    // a building site reported as unsafe: the inspector's look
    }

    /// <summary>Where a case stands. Saved as a number.</summary>
    public enum FlareCaseState
    {
        Open = 0,        // filed: the answer comes at ReplyAt
        Acting = 1,      // answered and being acted on
        Done = 2,        // acted on, over
        Queued = 3       // answered "not now"
    }

    /// <summary>A kind of notification on the Notifications screen (worked out when it is shown, never kept).</summary>
    public enum FlareNoticeKind
    {
        Like = 0,
        Reply = 1,
        Repost = 2,
        Mention = 3,
        Case = 4,
        Dm = 5,
        Going = 6,
        Review = 7
    }

    /// <summary>What one of the city's people is like: what they post about.</summary>
    public enum FlarePersona
    {
        Commuter = 0,
        Foodie = 1,
        Parent = 2,
        Student = 3,
        Retiree = 4,
        Sporty = 5,
        Grumpy = 6,
        NightOwl = 7
    }

    /// <summary>One of the city's people on Flare: made from the city's seed, never saved (FlareLiveRules.Citizens).</summary>
    public struct FlareCitizen
    {
        public int Index;
        public string First;
        public string Last;
        public string Handle;        // "@first_last" and a number
        public string District;      // the game's own id of their district, or null
        public FlarePersona Persona;
        public bool Female;          // as their first name says
        public int Age;              // years, as suits their persona
    }

    /// <summary>A business of the city as the day's posts see it: handed over by the director when the day is played, never kept.</summary>
    public struct FlareBiz
    {
        public string Place;         // "street~number"
        public string Name;
        public string District;
        public FlareNiche Niche;
        public int Stars;            // the game's satisfaction, 0 to 100; -1 not known
        public bool Mine;            // the player's own
        public int Open;             // the hours of the day the game has it open, bit h for hour h; 0 while temporarily closed or closed all day
    }

    /// <summary>The city as the day's posts read it, handed over once a day or made up by the offline checks; never kept.</summary>
    public sealed class FlareCityFacts
    {
        public int Day;
        public bool Rain;                // raining at the morning's look
        public bool Rained;              // it rained yesterday
        public int News;                 // bits of FlareCast.News*: what the city's news had yesterday and today
        public string NewsDistrict;      // where the latest crime or accident was, the game's own id
        public int PriceTrend;           // -1 falling, 0 flat, 1 rising (the city's economy)
        public string EventToday;        // the street event's name key today, or null
        public int EventTodayFrom = -1;  // its first hour
        public string EventTomorrow;     // ...tomorrow's
        public int EventTomorrowFrom = -1;
        public bool Election;            // an election is being fought
        public readonly List<string> Candidates = new List<string>(4);
        public int LateSites;            // the council's works running late
        public string SiteDistrict;      // where one of the works is, the game's own id
        public string Policy;            // a policy that came in today, in words, or null
        public string Mover;             // the market's biggest mover yesterday, or null
        public int MoverBp;
        public int RateMove;             // the bank rate moved yesterday: -1 down, 1 up, 0 not
        public bool LitterPolicy;        // the council cut street cleaning
        public readonly List<string> Districts = new List<string>(16);
        public readonly List<FlareBiz> Shops = new List<FlareBiz>(24);   // the city's businesses, a sample
        public readonly List<FlareBiz> Mine = new List<FlareBiz>(8);     // the player's own
        public int Busy = 1;             // the option: 0 quiet, 1 normal, 2 busy
        public bool Following;           // a guest of a session that follows the host's feed: the city's posts are the host's
        public bool RealFaults;          // the city's faults are real: its people report only those
    }

    /// <summary>A case of the City Office's: a report, its answer and what was done. Saved (FlareStore, the network's line).</summary>
    public sealed class FlareCase
    {
        public int Id;
        public FlareReport Kind;
        public string District;          // the game's own id
        public string Place;             // where: a building's "street~number", or null
        public string Name;              // the business, or the street, in words
        public float X, Z;               // the point reported from (the street), for a works site near it
        public long Filed;               // game minutes since day 0
        public long ReplyAt;             // the answer is due then
        public FlareCaseState State;
        public FlareOrder Order;
        public int Ref = -1;             // the works site's id, for Works
        public int Until = -1;           // the first day without the patrols or the cleaning
        public int Post = -1;            // the report's post (local number)
        public int Answer = -1;          // the answer's post (local number)
        public string Reporter;          // whose report: "me", "c:12" (one of the city's people), "p:id" (a player of the session)
        public int Repeats;              // others who said the same since
        public int ActedDay = -1;        // the day a crew was sent (the week's crews are counted by it); -1 for none
        public bool Counted = true;      // one of its player's first three reports of the day: moves the mood, the tally and the paper
        public int Verdict = -1;         // what the city's faults made of it when answered (FaultAnswer); -1 not judged
        public int Fault = -1;           // ...the fault it found and may take; -1 none
    }

    /// <summary>A rival tycoon as Flare knows them: their place in the rivals' catalogue (their voice), name and latest move (-1 none).</summary>
    public sealed class FlareTycoon
    {
        public int Index;
        public string Name;
        public int Move = -1;
    }

    /// <summary>The police or a cleaning crew in a district until a day: what each machine applies to its own (FlareLiveRules).</summary>
    public sealed class FlareWatch
    {
        public string District;
        public FlareOrder Kind;          // Patrol or Cleanup
        public int Until;                // the first day without it
    }

    /// <summary>One direct message, to or from the player. Saved.</summary>
    public sealed class FlareDm
    {
        public int Id;
        public string With;              // the account: "c:12", "i:3", "o:0", "b:name", "p:player"
        public string Name;              // its name as shown
        public long At;                  // game minutes since day 0: before then it is not shown
        public bool Mine;                // the player wrote it
        public string Text;              // typed (the player's, or a player's of the session, filtered)
        public string Key;               // or a line of the city's (a locale key) ...
        public string Data;              // ...and its names, "name=value" each, separated by '~'
        public bool Read;
    }

    /// <summary>A piece Flare has for a morning's paper, with its names. Saved until it is printed.</summary>
    public sealed class FlareStory
    {
        public int Day;                  // the paper it goes in
        public string Key;
        public string[] Data;            // name, value, name, value
    }

    /// <summary>One complaint, the player's or one of the city's people's, kept a week for the City Office's tally.</summary>
    public sealed class FlareComplaint
    {
        public FlareReport Kind;
        public string District;
        public int Day;
        public bool Acted;               // the City Office did something about it
    }

    /// <summary>The living network's state in a save: cases, watches, complaints, messages, stories, muted and hidden players and more. Game-free.</summary>
    public sealed class FlareNet
    {
        public const int CasesMost = 40, DmsMost = 120, StoriesMost = 8, ComplaintsMost = 120, MutedMost = 32, GoingMost = 16, AppliedMost = 200;

        public readonly List<FlareCase> Cases = new List<FlareCase>(16);
        public readonly List<FlareWatch> Watches = new List<FlareWatch>(8);
        public readonly List<FlareComplaint> Complaints = new List<FlareComplaint>(32);
        public readonly List<FlareDm> Dms = new List<FlareDm>(32);
        public readonly List<FlareStory> Stories = new List<FlareStory>(4);
        public readonly List<string> Muted = new List<string>(4);      // players of the session: no notifications, no messages
        public readonly List<string> Hidden = new List<string>(4);     // players of the session: nothing of theirs is shown
        public readonly List<int> Going = new List<int>(4);            // invitations the player goes to (post numbers)
        public readonly List<int> Applied = new List<int>(32);         // shared posts whose effect this machine has applied (NetId)
        public readonly List<string> Escalated = new List<string>(8);  // "kind|district|day": a tally that reached the paper
        public int NextCase = 1;
        public int NextDm = 1;
        public long NoticeSeen;          // game minutes: notifications up to here have been seen
        public int CityDay = -1;         // the last day the city's posts were made for
        public int PicDay = -1;          // the day PicsToday counts for
        public int PicsToday;            // pictures sent to the session today
        public int PicNext = 1;          // the number of the next picture of the player's sent to the session
        public int RepliesDay = -1;      // the day RepliesToday counts for: kept for the save, nothing limits replies
        public int RepliesToday;
        public int ReportsDay = -1;      // the day ReportsToday counts for
        public int ReportsToday;         // the player's reports today that weighed in the council (FlareLiveRules.Weighs)
        public int MovedDay = -1;        // the day Moved is for
        public readonly List<string> Moved = new List<string>(8);      // what moved a business or the City Office today (FlareLiveRules.FirstToday)
        public int DmDay = -1;           // the last day one of the city's people wrote
        public string HostKey;           // a guest: the host and session whose NetIds this save's posts carry (FlareNetShare.NewHost)
        public int CitySeed;             // the seed of the city's people: this save's, or the host's it followed, so their names stay

        public FlareCase Case(int id)
        {
            for (int i = 0; i < Cases.Count; i++)
            {
                if (Cases[i].Id == id)
                {
                    return Cases[i];
                }
            }
            return null;
        }

        /// <summary>The order on in a district now, or None.</summary>
        public FlareOrder WatchIn(string district, int day, FlareOrder kind)
        {
            for (int i = 0; i < Watches.Count; i++)
            {
                FlareWatch w = Watches[i];
                if (w.Kind == kind && day < w.Until && string.Equals(w.District, district, System.StringComparison.Ordinal))
                {
                    return kind;
                }
            }
            return FlareOrder.None;
        }

        public bool IsMuted(string player)
        {
            return player != null && Muted.Contains(player);
        }

        public bool IsHidden(string player)
        {
            return player != null && Hidden.Contains(player);
        }
    }
}
