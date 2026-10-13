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
        Life = 11          // a friend's own everyday post
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
        Friend = 1
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
        public readonly int[] History = new int[HistoryDays];   // followers at the end of each of the last seven days, oldest first
        public readonly List<FlareDraft> Drafts = new List<FlareDraft>(FlareRules.DraftsMost);
        public readonly List<FlarePost> Posts = new List<FlarePost>(FlareRules.PostsMost);
        public readonly List<FlareShopSeen> Shops = new List<FlareShopSeen>(8);

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

        public FlareShopSeen Shop(string place)
        {
            for (int i = 0; i < Shops.Count; i++)
            {
                if (Shops[i].Place == place)
                {
                    return Shops[i];
                }
            }
            return null;
        }

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
