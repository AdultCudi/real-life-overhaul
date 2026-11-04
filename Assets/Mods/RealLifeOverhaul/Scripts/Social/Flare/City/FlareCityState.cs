using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What a measured thing was: a post of the player's, an ad, a deal's post, a business's own post. Saved as a number.</summary>
    public enum FunnelKind : byte
    {
        Post = 0,
        Ad = 1,
        Deal = 2,
        Business = 3
    }

    /// <summary>
    /// One post or ad, measured resident by resident (DRAFT2.md section 4: "the app shows the funnel: seen, interested, came, spent"): the
    /// residents who saw it and were interested when it went up, and from the days after, the visits and the takings the attention it gave its
    /// business brought there (its share of what Flare's attention moved that day). Game-free.
    /// </summary>
    public sealed class FlareFunnel
    {
        public int Post;             // the local post's number (FlarePost.Id), or the ad's number for an ad
        public FunnelKind Kind;
        public string Place;         // the business it was about, as Flare writes a place ("street~number"), or null
        public int Day;
        public int Seen;             // residents who saw it
        public int Interested;       // ...and were interested
        public int Followed;         // residents who followed its account after it
        public int Came;             // visits its attention brought (negative: kept away)
        public long Spent;           // cents its attention brought (negative: kept away)
        public int Live;             // thousandths of a resident: what is left of its impressions on its business, fading as the talk does
    }

    /// <summary>An ad: who it is shown to, for how long, what it cost and what it did. Game-free; saved.</summary>
    public sealed class FlareAd
    {
        public int Id;
        public string Place;         // the business, as Flare writes a place
        public string Name;          // the business's name, as shown
        public int District = -1;    // the population's district it is shown in, -1 the whole city
        public int Age;              // FlareCityCatalogue.AgeFrom's band: 0 anybody
        public int Class = -1;       // the households' class, -1 all
        public int Interest = -1;    // the kind of spending its residents care for (Population.Spend), -1 none
        public int Reach;            // residents a day
        public int Days;
        public int From;             // its first day
        public long Cost;            // cents paid
        public bool Mine = true;     // the player's (false: a city business's, for parity)
    }

    /// <summary>One review of a business by a resident who shopped there: who, how many stars and when.</summary>
    public struct FlareCityReview
    {
        public int Person;           // the resident's id (0 for none)
        public byte Stars;
        public int Day;
    }

    /// <summary>A post as the residents hear it: who wrote it, what it is about and how it lands (FlareCityRules.Hear).</summary>
    public struct CityPost
    {
        public int Id;               // the local post's number, or an ad's
        public int Account;          // the author's account in the registry, -1 for none (nobody follows it: a city review)
        public int Business;         // the population's business slot it is about, -1 for none
        public int Kind;             // the kind of spending it is about (Population.Spend), -1 for none
        public int Sign;             // +1 good, -1 bad (a poor review, a scandal), 0 neither (a selfie)
        public int Quality;          // thousandths (FlarePost.Quality, or 1000)
        public int Fit;              // thousandths (FlareRules.ToneFit, or 1000)
        public bool Viral;
        public int District;         // the population's district it is about, -1 for none (discovery is in the business's district, then this)
        public int Seed;
        public int Gain;             // the player's: followers the account's own rules gave it (FlareRules.Post), shared between the city and beyond
        public FunnelKind Funnel;    // what it is measured as, when it is the player's
        public bool Measured;        // the player's: kept in the funnel
    }

    /// <summary>What hearing a post did, for the log and the funnel.</summary>
    public struct HeardReport
    {
        public int Seen, Interested, Shared, Discovered, Followed, Lost;
        public int Investors;        // ...of the interested, residents with savings to put in the market (FlareCityRules.Investor)
        public long Impressions;     // thousandths of a resident added to its business
    }

    /// <summary>
    /// Flare in the city, for a save: the accounts residents follow, each resident's follows (by the population's person slot, with the person's id
    /// so a slot given to somebody new starts afresh), what residents heard of each business (by the population's business slot and district, with
    /// the business's key so a city generated again binds by address), the reviews, the funnel, the ads and the voices. The indexes made from it each
    /// day (followers by account, adults by district, people by employer, households by district and class) are not saved. Game-free.
    /// COST: nothing per frame; about 20 bytes a resident and 300 a business.
    /// </summary>
    public sealed class FlareCityState
    {
        public const int Version = 1;
        public const int PlayerAccount = 0;          // the player's own account is always the first

        public int Seed;                             // the city's seed (the population's)
        public int Day = -1;                         // the last day the residents' day was played for
        public int Generated = -1;                   // the day the follows were first made
        public int NextAd = 1;

        // ---- the accounts residents can follow, by key: "me", "b:street|number" (a business), "i:3" (an influencer), "v:12" (a voice), "p:id" (a session's player)
        public readonly List<string> Accounts = new List<string>(512);
        private readonly Dictionary<string, int> _accountOf = new Dictionary<string, int>(512, StringComparer.Ordinal);

        // ---- each resident's follows, by person slot
        public int[] Owner = new int[0];             // the person id these follows are for; 0 none yet; minus the id for somebody seen too young
        public ushort[] Follows = new ushort[0];     // FlareCityCatalogue.Slots a person

        // ---- each business's figures, by the population's business slot
        public int Districts;
        public string[] BizKey = new string[0];      // the business key its figures are for ("street|number")
        public int[] Heard = new int[0];             // business * Districts + district: thousandths of a resident who heard good things lately
        public int[] Sour = new int[0];              // ...bad things
        public bool[] Answered = new bool[0];        // what put people off has been answered: it fades faster
        public int[] Attention = new int[0];         // thousandths: what Flare gives the business at the population's next day
        public int[] Given = new int[0];             // ...what it gave on the day the population last lived (the source's own answer then)
        public int[] HabitBefore = new int[0];       // business * classes: the pools' habit share (millionths) as the population asked for the day's attention, for the exact lift (not saved)
        public int HabitFor = -1;                    // the population's last day lived when that was taken; -1 none (after a load, or once measured)
        public long[] Lift = new long[0];            // cents of takings Flare's attention brought on the day last lived (negative: kept away)
        public int[] LiftVisits = new int[0];
        public int[] Followers = new int[0];         // residents who follow it (the day's count)
        public long[] RatingSum = new long[0];       // thousandths of a star, fading (FlareCityCatalogue.RatingKeep)
        public long[] RatingCount = new long[0];     // thousandths of a review, fading
        public int[] ReviewDue = new int[0];         // visits since the last review
        public int[] ReviewCount = new int[0];       // reviews ever written
        public FlareCityReview[] Reviews = new FlareCityReview[0];   // business * ReviewRing, the newest at ReviewHead
        public byte[] ReviewHead = new byte[0];

        // ---- the funnel, the ads and the voices
        public readonly List<FlareFunnel> Funnels = new List<FlareFunnel>(FlareCityCatalogue.FunnelsMost);
        public readonly List<FlareAd> Ads = new List<FlareAd>(8);
        // The city's and the rivals' own ads of the last days, by the population's business key (not saved).
        public readonly List<FlareAd> CityAds = new List<FlareAd>(FlareCityCatalogue.CityAdsKept);
        public readonly List<FlareOffer> Offers = new List<FlareOffer>(8);   // the city's businesses' offers of paid posts (FlareCityRules.Creator)
        public int NextOffer = 1;
        public long CreatorPaid;                     // the last creator income paid, cents (the studio shows it); saved at the end of the use columns
        public long CreatorTotal;                    // ...and all of them
        public int CreatorDay = -1;                  // the last day a creator income was paid (or the account reached its tier); -1 not yet
        public int ScandalDay = -1;                  // the last day a post of the player's went badly and many saw it (a pile-on): a scandal until answered
        public readonly List<FlareTrend> Trends = new List<FlareTrend>(FlareCityCatalogue.TrendsMost);   // what is trending (FlareCityRules.Trends)
        public FlarePlan Plan = new FlarePlan();     // this week's content plan from the desk session (FlareCityRules.Plan)
        public int NextTrend = 1;
        public string WarnKey;       // a business whose reviews fell a star, until the player is told
        public int RivalDay = -1;    // the last day a rival posted, promoted or answered, so the day is played once
        public readonly List<FlareReviewNote> ReviewNotes = new List<FlareReviewNote>(32);
        public readonly List<FlareStarRow> StarRows = new List<FlareStarRow>(16);
        public readonly List<FlareAdMask> AdMasks = new List<FlareAdMask>(8);
        public readonly List<FlareOfferNeed> OfferNeeds = new List<FlareOfferNeed>(8);
        public readonly List<FlareBoost> RivalBoosts = new List<FlareBoost>(8);
        public uint TrendCity;                       // the day's lookups: a bit for each kind trending in the whole city...
        public int[] TrendDistrict = FlareCityRules.NoDistricts();   // ...and the district each kind trends in, -1 none (not saved)
        public int[] Voices = new int[FlareLiveRules.Citizens];   // the person id behind each of the city's voices ("c0" to "c399"), 0 for none yet

        // ---- the day's indexes (not saved)
        public int[] FollowerStart = new int[1];     // by account: where its followers start in FollowerList (accounts + 1)
        public int[] FollowerList = new int[0];      // person slots
        public int[] AdultStart = new int[1];        // by district: where its adults start in AdultList
        public int[] AdultList = new int[0];
        public int[] Adults = new int[0];            // adults by district
        public int[] WorkStart = new int[1];         // by employer: its people in WorkList
        public int[] WorkList = new int[0];
        public int[] HomeStart = new int[1];         // by district and class: its households in HomeList
        public int[] HomeList = new int[0];
        public int Indexed = -1;                     // the day the indexes were made for; -1 not yet

        public int CityFollowers;                    // residents who follow the player (the day's count, kept up as posts bring more)
        public FlareCityStore.Loaded Pending;        // the follows a load read, by person id, until they are placed on the people (FlareCityStore.Bind)

        /// <summary>A new state: the player's own account is the registry's first (PlayerAccount), before any other is made.</summary>
        public FlareCityState()
        {
            Account("me");
        }

        public int Capacity => Owner.Length;

        /// <summary>
        /// The businesses' figures bound to the population's business slots by key (a city generated again, a slot holding another business): each
        /// figure goes to the slot whose key is its own; a business with no slot now is let go, a slot with no figures starts with none.
        /// </summary>
        public void RebindBusinesses(string[] keys, int count, int districts)
        {
            var old = new Dictionary<string, int>(BizKey.Length, StringComparer.Ordinal);
            for (int i = 0; i < BizKey.Length; i++)
            {
                if (BizKey[i] != null && !old.ContainsKey(BizKey[i]))
                {
                    old[BizKey[i]] = i;
                }
            }
            var was = new FlareCityState();
            was.Districts = Districts;
            was.BizKey = BizKey;
            was.Heard = Heard;
            was.Sour = Sour;
            was.Answered = Answered;
            was.Attention = Attention;
            was.Given = Given;
            was.Lift = Lift;
            was.LiftVisits = LiftVisits;
            was.Followers = Followers;
            was.RatingSum = RatingSum;
            was.RatingCount = RatingCount;
            was.ReviewDue = ReviewDue;
            was.ReviewCount = ReviewCount;
            was.Reviews = Reviews;
            was.ReviewHead = ReviewHead;
            Allocate(count, districts);   // fresh arrays: a slot no figure is bound to starts with nothing
            bool sameDistricts = was.Districts == Districts;
            for (int b = 0; b < count; b++)
            {
                BizKey[b] = keys[b];
                if (keys[b] == null || !old.TryGetValue(keys[b], out int o))
                {
                    continue;
                }
                if (sameDistricts)
                {
                    Array.Copy(was.Heard, o * Districts, Heard, b * Districts, Districts);
                    Array.Copy(was.Sour, o * Districts, Sour, b * Districts, Districts);
                }
                Answered[b] = was.Answered[o];
                Attention[b] = was.Attention[o];
                Given[b] = was.Given[o];
                Lift[b] = was.Lift[o];
                LiftVisits[b] = was.LiftVisits[o];
                Followers[b] = was.Followers[o];
                RatingSum[b] = was.RatingSum[o];
                RatingCount[b] = was.RatingCount[o];
                ReviewDue[b] = was.ReviewDue[o];
                ReviewCount[b] = was.ReviewCount[o];
                Array.Copy(was.Reviews, o * FlareCityCatalogue.ReviewRing, Reviews, b * FlareCityCatalogue.ReviewRing, FlareCityCatalogue.ReviewRing);
                ReviewHead[b] = was.ReviewHead[o];
            }
        }

        public int BusinessCount => BizKey.Length;

        /// <summary>An account's place in the registry, made if new; -1 when the registry is full.</summary>
        public int Account(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return -1;
            }
            if (_accountOf.TryGetValue(key, out int a))
            {
                return a;
            }
            if (Accounts.Count >= FlareCityCatalogue.AccountsMost)
            {
                return -1;
            }
            a = Accounts.Count;
            Accounts.Add(key);
            _accountOf[key] = a;
            return a;
        }

        /// <summary>An account's place in the registry, -1 when it has none.</summary>
        public int AccountOf(string key) => key != null && _accountOf.TryGetValue(key, out int a) ? a : -1;

        /// <summary>The registry filled again from the list (after a load).</summary>
        public void IndexAccounts()
        {
            _accountOf.Clear();
            for (int i = 0; i < Accounts.Count; i++)
            {
                if (Accounts[i] != null)
                {
                    _accountOf[Accounts[i]] = i;
                }
            }
            if (Accounts.Count == 0)
            {
                Account("me");
            }
            else if (!string.Equals(Accounts[PlayerAccount], "me", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Flare in the city: the player's account must be the registry's first");
            }
        }

        /// <summary>The follows table grown to hold a person slot.</summary>
        public void GrowPeople(int slots)
        {
            if (slots <= Owner.Length)
            {
                return;
            }
            int was = Owner.Length;
            int to = Math.Max(slots, Math.Max(256, was * 3 / 2));
            Array.Resize(ref Owner, to);
            Array.Resize(ref Follows, to * FlareCityCatalogue.Slots);
            for (int i = was * FlareCityCatalogue.Slots; i < Follows.Length; i++)
            {
                Follows[i] = FlareCityCatalogue.None;
            }
        }

        /// <summary>The businesses' figures grown to hold a business slot; new slots start with nothing heard.</summary>
        public void GrowBusinesses(int count, int districts)
        {
            if (districts != Districts && Districts > 0 && BizKey.Length > 0)
            {
                // the city's districts changed (a district added at a load): the figures by district start again, the rest is kept
                Heard = new int[BizKey.Length * districts];
                Sour = new int[BizKey.Length * districts];
            }
            Districts = Math.Max(1, districts);
            if (count <= BizKey.Length && Heard.Length == BizKey.Length * Districts)
            {
                return;
            }
            int n = Math.Max(count, BizKey.Length);
            Array.Resize(ref BizKey, n);
            Array.Resize(ref Heard, n * Districts);
            Array.Resize(ref Sour, n * Districts);
            Array.Resize(ref Answered, n);
            Array.Resize(ref Attention, n);
            Array.Resize(ref Given, n);
            Array.Resize(ref HabitBefore, n * Population.PopulationCatalogue.Classes);
            Array.Resize(ref Lift, n);
            Array.Resize(ref LiftVisits, n);
            Array.Resize(ref Followers, n);
            Array.Resize(ref RatingSum, n);
            Array.Resize(ref RatingCount, n);
            Array.Resize(ref ReviewDue, n);
            Array.Resize(ref ReviewCount, n);
            Array.Resize(ref Reviews, n * FlareCityCatalogue.ReviewRing);
            Array.Resize(ref ReviewHead, n);
        }

        // Every business figure made anew, empty, for so many slots.
        private void Allocate(int count, int districts)
        {
            int n = Math.Max(0, count);
            Districts = Math.Max(1, districts);
            BizKey = new string[n];
            Heard = new int[n * Districts];
            Sour = new int[n * Districts];
            Answered = new bool[n];
            Attention = new int[n];
            Given = new int[n];
            HabitBefore = new int[n * Population.PopulationCatalogue.Classes];
            Lift = new long[n];
            LiftVisits = new int[n];
            Followers = new int[n];
            RatingSum = new long[n];
            RatingCount = new long[n];
            ReviewDue = new int[n];
            ReviewCount = new int[n];
            Reviews = new FlareCityReview[n * FlareCityCatalogue.ReviewRing];
            ReviewHead = new byte[n];
        }

        /// <summary>A business slot's figures cleared (the slot now holds another business).</summary>
        public void ClearBusiness(int b)
        {
            Array.Clear(Heard, b * Districts, Districts);
            Array.Clear(Sour, b * Districts, Districts);
            Answered[b] = false;
            Attention[b] = Given[b] = 0;
            Lift[b] = 0;
            LiftVisits[b] = Followers[b] = ReviewDue[b] = ReviewCount[b] = 0;
            RatingSum[b] = RatingCount[b] = 0;
            Array.Clear(Reviews, b * FlareCityCatalogue.ReviewRing, FlareCityCatalogue.ReviewRing);
            ReviewHead[b] = 0;
        }

        /// <summary>The followers of an account, as the day's index has them: where they start and how many.</summary>
        public int FollowersOf(int account, out int start)
        {
            if (account < 0 || account + 1 >= FollowerStart.Length)
            {
                start = 0;
                return 0;
            }
            start = FollowerStart[account];
            return FollowerStart[account + 1] - start;
        }

        /// <summary>The funnel record of a post or an ad, or null.</summary>
        public FlareFunnel Funnel(FunnelKind kind, int id)
        {
            for (int i = Funnels.Count - 1; i >= 0; i--)
            {
                if (Funnels[i].Kind == kind && Funnels[i].Post == id)
                {
                    return Funnels[i];
                }
            }
            return null;
        }

        public FlareAd Ad(int id)
        {
            for (int i = 0; i < Ads.Count; i++)
            {
                if (Ads[i].Id == id)
                {
                    return Ads[i];
                }
            }
            return null;
        }

        /// <summary>A business's rating in tenths of a star (0 for none yet) and its reviews ever.</summary>
        public int Rating(int b)
        {
            return b >= 0 && b < RatingCount.Length && RatingCount[b] > 0 ? (int)((RatingSum[b] * 10 + RatingCount[b] / 2) / RatingCount[b]) : 0;
        }
    }
}
