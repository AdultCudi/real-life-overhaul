using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What a post was drafted from: something that really happened. Saved as a number: new kinds go at the end, never renumbered.</summary>
    public enum FlareSource
    {
        None = 0,
        Photo = 1,         // a picture saved in photo mode
        Selfie = 2,        // a selfie saved in photo mode
        Outing = 3,        // an outing with a friend that went to the end
        Promotion = 4,     // promoted at a review
        Opened = 5,        // a business of the player's opened
        RecordDay = 6,     // a business's best day of takings yet
        Voted = 7,         // a ballot cast
        Endorsed = 8,      // a candidate endorsed
        GoodDeed = 9,      // somebody helped
        Arrested = 10,     // taken into custody
        Life = 11,         // a friend's own everyday post
        Scandal = 12,      // talk about a scandal at a business of a brand of the player's: an answer
        Sponsored = 13,    // the player's own sponsored post, booked as a campaign
        Niche = 14,        // an influencer's everyday post about their niche and their part of town
        Visit = 15,        // an influencer at one of the player's businesses, unpaid
        Hired = 16,        // an influencer paid by the player for a post about a business (a campaign)
        Rival = 17,        // an influencer paid by one of the game's rivals or a city chain
        Election = 18,     // an influencer or a candidate about the election
        Apology = 19,      // an influencer's own scandal
        BrandNews = 20,    // a brand's or a city chain's own post
        CityNews = 21,     // an influencer of local news about what happened in the city today
        Shares = 22,       // the player about a listed company they hold shares in (its results)
        Results = 23,      // a listed company's own post of its results
        Headline = 24,     // the City Ledger's own account: a headline of the morning paper
        Issue = 25,        // the player about an issue in the city (safety after a crime, the roads after a crash)
        Campaign = 26,     // the player about their own run for office or their office (Civic/CivicOffice, through Wiring/FlareLinks)
        Graduated = 27,    // the player graduated at the university's ceremony (Careers, through Wiring/FlareLinks)
        Interior = 28,     // a business of the player's with a striking interior (Scripts/Interiors, through Wiring/InteriorLinks)
        Report = 29,       // a report or a complaint to the city (Arg: FlareReport; District, Place): the City Office answers and acts (FlareLiveRules)
        Review = 30,       // a review of a business (Arg: the stars, 1 to 5; Place, Name)
        Announce = 31,     // an announcement for a business of the player's (Arg: FlareAnnounce; Place, Name)
        Reply = 32,        // a reply to a post (ReplyTo); its words are the typed text, or a line of its kind (Arg: FlareReplyKind)
        CityTalk = 33,     // one of the city's people about the day (Arg: FlareTalk; Name, District as the topic has them)
        Official = 34,     // an official account's own post (Who: FlareOfficial; Arg: FlareOfficialPost)
        BusinessPost = 35, // a business of the city's own post (Place, Name; Arg: its kind of offer)
        Invite = 36,       // an invitation to a street event or an event at a business (Arg: the event's kind; Name, Place, Topic)
        Answer = 37        // an account's answer in character to a report, a review or a mention (Arg: FlareAnswer; ReplyTo)
    }

    /// <summary>The issues a post can raise, as Flare knows them (Wiring/FlareLinks turns them into the council's own).</summary>
    public enum FlareIssue
    {
        Safety = 0,
        Roads = 1
    }

    /// <summary>What an influencer posts about. Saved as a number: new ones go at the end.</summary>
    public enum FlareNiche
    {
        Food = 0,
        Fashion = 1,
        Fitness = 2,
        Nightlife = 3,
        Tech = 4,
        LocalNews = 5,
        Business = 6
    }

    /// <summary>An influencer's manner. Saved as a number: new ones go at the end.</summary>
    public enum FlareStyle
    {
        Honest = 0,      // posts what they think, on time
        Flaky = 1,       // late, now and then off message
        Edgy = 2,        // reaches far, can backfire, the most scandals
        Wholesome = 3    // steady and liked, the fewest scandals
    }

    /// <summary>How a post is worded. Saved as a number.</summary>
    public enum FlareTone
    {
        Cheerful = 0,
        Proud = 1,
        Honest = 2,
        Cheeky = 3
    }

    /// <summary>Who wrote a post. Saved as a number: new kinds go at the end.</summary>
    public enum FlareAuthor
    {
        Player = 0,
        Friend = 1,
        Influencer = 2,
        Brand = 3,
        Candidate = 4,
        Company = 5,      // a listed company of the stock market
        Ledger = 6,       // the City Ledger's own account
        Citizen = 7,      // one of the city's people (Who: their number in the city's cast, FlareLiveRules.Citizens)
        Official = 8,     // an official account (Who: FlareOfficial)
        Business = 9,     // a business of the city's (Place, Name)
        Network = 10,     // another player of a Going Public session (AuthorId, AuthorName)
        Tycoon = 11       // a rival tycoon of the end game (Name), once that feature is in the mod (FlareBridges.Tycoons)
    }

    /// <summary>The account's tier, by followers (FlareRules.TierFrom).</summary>
    public enum FlareTier
    {
        Newcomer = 0,
        Local = 1,
        Known = 2,
        Influencer = 3,
        Celebrity = 4
    }

    /// <summary>What a friend cares about, as Flare reads it (the social life's traits, turned into Flare's own by Wiring/FlareLinks).</summary>
    [System.Flags]
    public enum FlareTaste
    {
        None = 0,
        Cheers = 1,     // kind about everything, arrests too
        Cool = 2,       // not kind about money
        Shops = 4,      // likes the player's shops
        Sport = 8,      // workouts and walks
        Food = 16,      // meals and coffee
        Night = 32,     // drinks and films
        Flaky = 64      // often does not look
    }

    /// <summary>
    /// One person of the social life as Flare sees them: handed over by Wiring/FlareLinks when the day is played or
    /// a post is made, never kept. Without the social life there is nobody, and Flare runs with followers alone.
    /// </summary>
    public struct FlarePerson
    {
        public int Index;            // the social life's own number for them
        public string First;         // first name
        public int Stage;            // 0 stranger ... 5 partner, the social life's stages
        public FlareTaste Taste;
        public int Closeness;        // 0 to 100
        public bool Partner;         // dating or partner
        public string Job;           // their job and their part of town, in words
        public string District;
        public bool Compatible;      // a romance with them is possible
        public bool Unanswered;      // they texted lately and the player has not answered

        /// <summary>Acquaintances and closer follow the player.</summary>
        public bool Follows => Stage >= FlareRules.FollowFromStage;
    }

    /// <summary>Something the player can post about: made when it happens, spent when it is posted, gone after two days.</summary>
    public sealed class FlareDraft
    {
        public int Id;
        public FlareSource Source;
        public int Day, Hour;
        public int Person = -1;      // who was there (an outing, a selfie with a friend): the social life's number; -1 for nobody
        public string Place;         // the business it is about or was taken in ("street~number"), or null
        public string Name;          // the business, the candidate, the employer or the picture's file, as it is to be shown
        public int Arg;              // the outing's kind, the new rank
    }

    /// <summary>
    /// One post, the player's or a friend's. Not text: a source, a tone, who and a seed. The words are made when it is
    /// shown (FlareText), so a change of language changes every post.
    /// </summary>
    public sealed class FlarePost
    {
        public int Id;
        public int Day, Hour;
        public FlareAuthor Author;
        public int Who = -1;         // a friend's number in the social life; -1 for the player
        public FlareSource Source;
        public FlareTone Tone;
        public int Tag = -1;         // the friend tagged; -1 for none
        public string Place;
        public string Name;
        public int Arg;
        public int Seed;
        public int Quality;          // thousandths: what the source is worth, by its tone and repeats
        public int Reach;            // people who saw it
        public int Likes;
        public int Replies;
        public int Likers;           // bits by the social life's number: friends who liked it
        public int Repliers;         // ...and who replied
        public int Gained;           // followers it brought (less what it cost)
        public bool Viral;
        public bool Bad;             // it went badly: the wrong tone for what it was about
        public bool Disclosed;       // a post about a company the player holds, from an account big enough to say so

        // ---- the living network (FlareLiveRules), each appended to the save's post line after the older fields
        public int Minute;           // the minute of the hour it went up (0 to 59): it shows from then on
        public int ReplyTo = -1;     // the post it answers (a local post's number); -1 for none
        public string Text;          // the words the player typed, as typed (never what decides an effect: the built part does)
        public string Pic;           // its picture: "f:" a photo of the Shots folder, "s:" a shot of the screen, "n:" a picture of the session's (FlareSnap)
        public int NetId;            // its number in a Going Public session's shared feed (the host's own post number); 0 for none
        public int Token;            // a guest's own post on its way to the host: the number that finds it again when it comes back
        public string AuthorId;      // a player of the session (Going Public's player id), for a Network post
        public string AuthorName;    // ...and their name, as they gave it
        public string District;      // the district a report or a city post is about (the game's own id)
        public string Topic;         // its tags, without the #, a space between each: what Trending counts
        public string Reactors;      // who of the people with names liked, replied or shared it, and when (FlareLiveRules.Reactors)
        public string Mention;       // an account it calls on, by key ("o:0", "b:name", "p:player"): it answers in character
        public int Reposts;          // shares, the city's and the players'
        public int PlayerLikes;      // likes from other players of the session
        public int Case = -1;        // the City Office's case it opened (FlareCase.Id); -1 for none
        public int Slot = -1;        // made for display (never saved): a friend's reply under a post of the player's, its place among that post's reactions; a comment under another's post, its slot (FlareComment.Slot); -1 for any other
        public bool Liked;           // the player liked it
        public bool Reposted;        // the player shared it
        public bool Shared;          // it came from the session's shared feed (a guest's copy of the host's)
        public int When = -1;        // an invitation's hour: day * 24 + hour; -1 for none
        public bool Pending;         // a guest's own post the host has not sent back yet
        public string NetPic;        // the player's own picture as the session has it ("n:" and its id), once its small copy went

        /// <summary>When it went up, in game minutes since day 0: before then it is not shown.</summary>
        public long At => ((long)Day * 24 + Hour) * 60 + Minute;
    }

    /// <summary>A business of the player's as Flare has seen it: since when, and its best day, for the record day draft.</summary>
    public sealed class FlareShopSeen
    {
        public string Place;
        public int FirstDay;
        public int Best;             // the best takings of a day yet
        public int Days;             // days with figures seen
    }

    /// <summary>Everything Flare keeps for a save (FlareStore writes it). Game-free.</summary>
    public sealed class FlareFile
    {
        public const int HistoryDays = 7;

        public int Seed;
        public int StartDay;
        public int SettledDay;           // the last day the daily round was played for
        public int Followers;
        public int Reputation = FlareRules.ReputationStart;   // 0 to 100: how the city reads the player's posts
        public int LastPostDay = -1;
        public int LastPostMinute = -1;  // game minutes since the first day (day * 1440 + minute of the day)
        public int PostsDay = -1;        // the day PostsToday counts for
        public int PostsToday;
        public int LastViralDay = -100;
        public int TotalPosts;
        public int TotalLikes;
        public int NextId = 1;
        public int Unseen;               // replies not yet seen: the app's badge
        public int Following;            // bits by the social life's number: friends who follow
        public int CivicSeen;            // bits: 1 the ballot, 2 the endorsement of the election that is on
        public int CareerKey = -1;       // the employer and rank last seen (employer * 100 + rank)
        public int NoticeDay = -1;       // the last day Flare sent a notification that asks for something (one a day at most)
        public int HypeDay = -100;       // the last day a viral post raised the game's own hype for a product
        public int ComplaintDay = -1;    // the last day a post on shift was a complaint at work
        public int NoticedDay = -1;      // the last day a picture posted while wanted drew the police's eye
        public int StoryDay = -1;        // the last day the City Ledger was given a piece about a viral post
        public int ArrestDay = -1;       // the last day an arrest trended
        public readonly int[] History = new int[HistoryDays];   // followers at the end of each of the last seven days, oldest first
        public readonly List<FlareDraft> Drafts = new List<FlareDraft>(FlareRules.DraftsMost);
        public readonly List<FlarePost> Posts = new List<FlarePost>(FlareRules.PostsMost);
        public readonly List<FlareShopSeen> Shops = new List<FlareShopSeen>(8);
        public readonly List<FlareBuzz> Buzz = new List<FlareBuzz>(8);
        public readonly List<FlareInfluencer> Influencers = new List<FlareInfluencer>(FlareCast.Most);
        public readonly List<FlareHire> Hires = new List<FlareHire>(8);
        public readonly List<string> Follows = new List<string>(8);   // the accounts the player follows ("i:3", "b:Copper Kettle", "c:Name"); a new game follows nobody
        public int CastSeed;             // the cast's own seed: the same cast for the same seed
        public int MonthDay = -1;        // the day the influencers' month was last played
        public int FollowNoticeDay = -1; // the last day the one notification of posts from followed accounts was sent
        public int NewsToday;            // bits: what the city's news had today (FlareCast.News*), for local news
        public int NewsDay = -1;
        public int ViralNicheDay = -100; // a viral post of the player's about a business: its niche draws a newcomer for a month
        public int ViralNiche = -1;
        public int OnlineDay = -100;     // the last day somebody the player had still to meet answered a post and became a contact
        public int PartnerTagDay = -1;   // the last day the player's partner was tagged
        public int OthersTagDay = -1;    // ...and somebody else
        public int NeverDay = -100;      // the last day the partner minded never being posted
        public int NeglectDay = -100;    // the last day a friend minded unanswered texts while the player posted all day
        public int LedgerDay = -1;       // the last day the City Ledger's account posted the morning's headlines
        public int MayorMood;            // -100 to 100: how the feed feels about the city's running, which pulls a little on the mayor's approval
        public int IssueDay = -1;        // the last day an issue was drafted
        public readonly List<FlareViralShares> ViralShares = new List<FlareViralShares>(4);   // viral posts about companies the player holds: selling soon after is market abuse
        public readonly List<int> ReplyLikes = new List<int>(4);   // the friends' replies the player liked, each as FlareRules.ReplyKey (post and place): those replies are made for display and have no post of their own
        public readonly List<long> CommentLikes = new List<long>(4);   // the comments under others' posts the player liked, each as FlareCommentRules.LikeKey (post, slot and who): made for display too
        public readonly FlareNet Net = new FlareNet();   // the living network: the City Office's cases, the messages, the watches (FlareLiveModel)

        public FlareInfluencer Influencer(int id)
        {
            for (int i = 0; i < Influencers.Count; i++)
            {
                if (Influencers[i].Id == id)
                {
                    return Influencers[i];
                }
            }
            return null;
        }

        public int ActiveInfluencers
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Influencers.Count; i++)
                {
                    n += Influencers[i].Active ? 1 : 0;
                }
                return n;
            }
        }

        /// <summary>What Flare adds to a business's customers now, per cent: the talk of its posts, inside the cap.</summary>
        public int BuzzAt(string place, int day)
        {
            int sum = 0;
            for (int i = 0; i < Buzz.Count; i++)
            {
                sum += Buzz[i].Place == place && day < Buzz[i].Until ? Buzz[i].Percent : 0;
            }
            return FlareRules.Clamp(sum, -FlareRules.BuzzMost, FlareRules.BuzzMost);
        }

        public FlareTier Tier => FlareRules.TierOf(Followers);

        public FlareDraft Draft(int id)
        {
            for (int i = 0; i < Drafts.Count; i++)
            {
                if (Drafts[i].Id == id)
                {
                    return Drafts[i];
                }
            }
            return null;
        }

        public FlarePost Post(int id)
        {
            for (int i = 0; i < Posts.Count; i++)
            {
                if (Posts[i].Id == id)
                {
                    return Posts[i];
                }
            }
            return null;
        }

        /// <summary>
        /// The first shop seen at a place, or null. From an index of the list (it only grows: a shop seen is added at its
        /// end, by the day's round or the save's reader), brought up to the list's end at each look-up: a dictionary hit
        /// where the day's round asked a pass of compares of every business (300 businesses: 90,000 compares a day).
        /// </summary>
        public FlareShopSeen Shop(string place)
        {
            if (place == null)
            {
                for (int i = 0; i < Shops.Count; i++)
                {
                    if (Shops[i].Place == null)
                    {
                        return Shops[i];
                    }
                }
                return null;
            }
            if (_shopsIndexed > Shops.Count)
            {
                _shopOf.Clear();   // (a list made shorter: indexed again from its start)
                _shopsIndexed = 0;
            }
            for (; _shopsIndexed < Shops.Count; _shopsIndexed++)
            {
                FlareShopSeen s = Shops[_shopsIndexed];
                if (s != null && s.Place != null && !_shopOf.ContainsKey(s.Place))
                {
                    _shopOf.Add(s.Place, s);   // (the first of a place, as the list's order has it)
                }
            }
            return _shopOf.TryGetValue(place, out FlareShopSeen seen) ? seen : null;
        }

        private readonly Dictionary<string, FlareShopSeen> _shopOf = new Dictionary<string, FlareShopSeen>(System.StringComparer.Ordinal);
        private int _shopsIndexed;   // the shops of the list the index has taken in, from its start

        /// <summary>The newest post of the player's from a source, or null.</summary>
        public FlarePost LastOwn(FlareSource source)
        {
            for (int i = Posts.Count - 1; i >= 0; i--)
            {
                if (Posts[i].Author == FlareAuthor.Player && Posts[i].Source == source)
                {
                    return Posts[i];
                }
            }
            return null;
        }

        public int OwnPosts
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Posts.Count; i++)
                {
                    n += Posts[i].Author == FlareAuthor.Player ? 1 : 0;
                }
                return n;
            }
        }
    }

    /// <summary>A viral post about a listed company the player holds: its stock and the day.</summary>
    public sealed class FlareViralShares
    {
        public int Stock;
        public string Name;
        public int Day;
    }

    /// <summary>Talk on Flare about a business: so many per cent of customers until a day (Wiring/FlareLinks adds it to business pressure's figure).</summary>
    public sealed class FlareBuzz
    {
        public string Place;
        public int Percent;
        public int Until;            // the first day without it
    }

    /// <summary>
    /// One of the city's influencers: made by seed, kept in the save, growing, fading, quitting or caught in a scandal by
    /// the month. They post under a handle; a star may turn out to be somebody the player can meet (Person).
    /// </summary>
    public sealed class FlareInfluencer
    {
        public int Id;
        public string Handle;
        public FlareNiche Niche;
        public string District;      // the game's own name for their home district ("ba:neighborhood_...")
        public int Followers;
        public int Engagement;       // hundredths of a per cent of the followers who answer a post (basis points)
        public int Reliability;      // 0 to 100
        public FlareStyle Style;
        public int Reputation = 60;  // 0 to 100
        public int StartDay;
        public bool Quit;
        public bool Star;
        public int Person = -1;      // the social life's number of who they are, once the player can meet them (a friend who became one, a star)
        public bool Staff;           // one of the player's own staff, who took it up
        public int ScandalDay = -100;
        public int Seed;

        /// <summary>The followers who answer a post.</summary>
        public int Engaged => (int)((long)Followers * Engagement / 10000);

        public bool Active => !Quit;
    }

    /// <summary>A campaign with influencers the player has paid for: the company's campaign of that kind, and what Flare does with it.</summary>
    public sealed class FlareHire
    {
        public int Id;
        public int Kind;             // the company's CampaignKind, saved as its number
        public string Place;
        public string Name;          // the business
        public int From, To;         // the first and the last day
        public int Percent;          // the customers the company's campaign adds
        public readonly List<int> Who = new List<int>(3);   // the influencers
        public bool Late;            // the first post came a day late
        public bool OffMessage;
        public bool Backfired;
        public bool Ended;           // the player ended it (a scandal), or it ran out
        public int Posted;           // the paid posts made for it so far (the Feed keeps only the newest posts, so they are counted here)
        public int LastPost = -1;    // the day of the last of them; -1 before the first
        public bool Counted = true;  // Posted counts every post since the deal began; false for a deal saved half run before the posts were counted

        public bool On(int day) => !Ended && day >= From && day <= To;
    }

    /// <summary>
    /// A campaign of the company's at one of the player's businesses, running or booked, as Flare is told of it
    /// (Wiring/FlareLinks): read when the Business tab is written, never kept.
    /// </summary>
    public struct FlareCampaign
    {
        public string Place;
        public string Name;          // the business
        public string Campaign;      // the campaign, in words
        public int Kind;             // the company's CampaignKind
        public int From, To;         // the first and the last day
        public int Percent;          // the customers it adds
        public bool OnFlare;         // hired on Flare; otherwise booked from the Company app
    }

    /// <summary>What the daily round did, for the one log line.</summary>
    public struct FlareDayReport
    {
        public int Day;
        public int FollowersBefore, FollowersAfter;
        public int ReputationBefore, ReputationAfter;
        public int Silent;               // days since the last post
        public int Lost;                 // followers lost to silence
        public int Following;            // friends who follow
        public int FriendPosts;          // friends who posted
        public int DraftsGone;
    }
}
