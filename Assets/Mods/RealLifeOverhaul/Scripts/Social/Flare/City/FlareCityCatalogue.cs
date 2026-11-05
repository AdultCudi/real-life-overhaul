namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Every figure of Flare in the city (plans/tech-overhaul/DRAFT2.md section 4; the owner, 31 Oct 2026: "I want something groundbreaking in it"):
    /// the city's residents follow accounts, see posts, hear of businesses and go where they heard good things. Game-free: EconomyCheck's
    /// FlareCityChecks reads every one. A share is thousandths, a resident's impression is a thousandth of a resident, money is cents unless named
    /// whole money. Each figure says its unit and why it is what it is.
    /// </summary>
    public static class FlareCityCatalogue
    {
        // ---- follows: who follows whom, a resident at a time

        public const int Slots = 8;                      // accounts a resident follows at most: the shops they use, a voice or two, an influencer, the player
        public const ushort None = 0xFFFF;               // an empty follow slot
        public const int AdultAge = 14;                  // years: residents are on Flare from here (the networks' own lower age)
        public const int AccountsMost = 60000;           // accounts the registry holds at most: a follow slot is a 16-bit number

        /// <summary>
        /// Thousandths of the player's followers who live in the city, by tier (Newcomer to Celebrity): a new account is its neighbours', a
        /// celebrity's following is mostly from beyond the city. The rest are "beyond the city", an aggregate, as the population's region is.
        /// </summary>
        public static readonly int[] CityShare = { 900, 800, 600, 400, 250 };

        public const int PlayerCityMost = 850;           // thousandths of the city's adults who may follow the player at the most
        public const int HomeLean = 2000;                // thousandths: residents of the player's own districts (home, businesses) follow twice as readily
        public const int ShopFollow = 90;                // per mille a resident follows a shop they use, for each kind they care about, when follows are first made
        public const int GroceryFollow = 40;             // ...and their grocer, whoever they are
        public const int VoiceTries = 2;                 // the city's voices of their own district a resident may follow
        public const int VoiceFollow = 180;              // per mille for each
        public const int InfluencerCityMost = 400;       // thousandths of the residents who share an influencer's niche who follow them at the most
        public const int BusinessUnfollow = 3;           // per mille a day a follow of a business is dropped (people move on):
                                                         // with the follows its visits bring, a shop's following settles near its daily visits
        public const int VisitsPerFollow = 400;          // a business's visits for each resident who starts to follow it (drawn from its real customers)

        // ---- a post's audience (FlareRules.Reach's own shares, resident by resident)

        public const int SeenShare = 300;                // thousandths of the followers who see an ordinary post (FlareRules.ReachShare)
        public const int SeenViral = 750;                // ...a viral one
        public const int InterestMatch = 650;            // per mille a resident who cares for what a post is about is interested
        public const int InterestOther = 220;            // ...anybody else
        public const int ShareChance = 50;               // per mille an interested resident shares a post with the people round them
        public const int ShareViral = 200;               // ...a viral one
        public const int TieSees = 600;                  // per mille each of the people round a sharer sees it: their household, two at work, a neighbour
        public const int Coworkers = 2;
        public const int Neighbours = 1;
        public const int DiscoverShare = 3;              // per mille of the district's adults who come across an ordinary post by its tags and Explore
        public const int DiscoverViral = 40;             // ...a viral one
        public const int DiscoverMost = 4000;            // residents a post's discovery reaches at the most: a bound on the work, never reached by an ordinary post
        public const int BusinessFollowFromPost = 30;    // per mille an interested resident who does not follow a business starts to
        public const int AnswerQuality = 500;            // thousandths: a polite answer to a poor review is heard as half a post

        // ---- what residents heard, and the attention it gives (PopulationAttention: a pool is only ever shared out)

        public const int HeardKeep = 750;                // thousandths of yesterday's impressions of a business kept: talk fades in a few days
                                                         // (the pools' habit keeps the customers it won a while longer)
        public const int SourKeep = 880;                 // ...of what put people off, while nobody answered
        public const int SourAnswered = 500;             // ...once the business or its owner answered honestly
        public const int PositiveLift = 1500;            // thousandths more likely a resident who heard good things goes there: two and a half times
        public const int NegativeLift = 900;             // thousandths less likely a resident who heard bad things goes: nine in ten stay away
        public const int AttentionLeast = -900;          // thousandths: the population's own bounds (PopulationCatalogue.AttentionLeast), kept here so Flare's part alone never passes them
        public const int AttentionMost = 9000;

        // ---- the player's own businesses and the player's name

        public const int FounderDaily = 25;              // per mille of the player's city followers who hear of the player's businesses each day by following the player (founder-led brands)
        public const int ScandalSour = 400;              // per mille of the player's followers who hear of a scandal of the player's, turned against the player's businesses until answered
        public const int ScandalDays = 7;                // days an arrest that trended stays a scandal, unless an honest post answers it first

        // ---- reviews from residents who really shopped there (drawn from the day's pools)

        public const int VisitsPerReview = 300;          // a business's visits for each review a customer writes (review sites: about one customer in a few hundred writes one)
        public const int ReviewRing = 6;                 // reviews kept a business: the newest
        public const int RatingKeep = 990;               // thousandths of a business's rating kept a day: a recent review counts more
        public const int ReviewBrowsers = 24;            // residents of the business's district who read a review while looking for its kind
        public const int ReviewsMostDay = 3000;          // reviews a day in the whole city at the most: a bound on the work
        public const int StarsPerPricePoint = 120;       // thousandths of the business's price over what the reviewer bears that cost a star (a fifth over: a star off)

        // ---- the funnel: what a post or an ad did

        public const int FunnelsMost = 60;               // the player's posts, ads and deals measured, the newest kept

        // ---- ads: residents targeted by district, age, income and interests, priced by reach

        public const int AdCpm = 22;                     // whole money for each thousand residents an ad is shown to (local social ads run about 5 to 25 a thousand; the game's prices go on top)
        public static readonly int[] AdReach = { 1000, 3000, 8000 };   // residents a day: the three sizes offered
        public static readonly int[] AdDays = { 1, 3, 7 };              // days: the three lengths offered
        public const int AdInterest = 750;               // per mille a targeted resident who cares for the business's kind is interested
        public const int AdInterestOther = 200;          // ...one who does not
        public const int AdsMost = 24;                   // ads of the player's kept, running or over
        public const int AdTargets = 5;                  // the targets the Business tab offers: its district's customers, the city's, its district's everybody, the young, the well off
        public static readonly int[] AgeFrom = { AdultAge, AdultAge, 30, 50 };   // the age bands an ad targets: anybody, under 30, 30 to 49, 50 and over
        public static readonly int[] AgeTo = { 200, 29, 49, 200 };
        public const int CityAdsDay = 6;                 // the city's and the rivals' businesses that run an ad a day (parity: the same rules as the player's)
        public const int CityAdsKept = 12;               // the city's own ads of the last days the kit's notifications and the shared-reach line read (not saved)
        public const int CityAdReach = 1500;             // residents a day of one of their ads

        // ---- the market and the council: the residents behind a viral thread and a following

        public const int InvestorSavings = 10000;        // whole money of savings from which a household puts some in the stock market (about a third of the city's)
        public const int InvestorsPerKick = 100;         // interested investors a viral post reaches for each kick more it gives its trade's sector on the market
        public const int MarketKicksMost = 8;            // kicks at the most: eight times the market's own buzz (MarketInputs.BuzzKickBp), a viral thread of a celebrity
        public const int IssueMoodAt = 20;               // per mille of the city's adults interested in a post about an issue for the feed's old move of its mood (FlareRules.MoodIssuePost)
        public const int IssueMoodLeast = 2, IssueMoodMost = 30;   // points of the feed's mood a post about an issue moves, by the residents it reached
        public const int EndorseFull = 500;              // per mille of the city's adults following the player at which the player's word in an election is worth the most

        // ---- trends: what is viral moves the residents' tastes for a few weeks (FlareCityRules.Trends)

        public const int TrendsMost = 3;                 // trends running at once
        public const int TrendChance = 30;               // per mille a day the city's own talk starts a trend (about one a month)
        public const int TrendRiseDays = 5;              // days a trend takes to reach its height, taking more residents each day
        public const int TrendDays = 21;                 // days from its start to when it begins to fade
        public const int TrendFadeDays = 5;              // days it takes to fade, letting a fifth of those it moved go each day
        public const int TrendStick = 100;               // per mille of those it moved who keep the taste for good: tastes move, slowly
        public const int TrendReach = 250;               // per mille of the city's households without the taste a trend of goods gives it by its height (about 5% more spent on the kind)
        public const int TrendDistrictReach = 300;       // ...of a district's households, for a district's going out
        public const int TrendGenreReach = 60;           // per mille of the adults who play games and do not like the genre who take to it (its audience about a quarter bigger)
        public const int TrendHum = 40;                  // per mille of each district's adults a day who hear of a trending district's places of its kind, at its height
        public const int TrendInterest = 150;            // per mille more of the residents who hear a post about what is trending are interested
        public const int TrendMovedMost = 12000;         // residents or households one trend moves at the most (what the save keeps of it)

        // ---- the voices: the city's people who post most, residents

        public const int VoiceYoungLean = 2000;          // thousandths: residents of 18 to 39 are twice as likely to be a voice
        public const int VoiceStudentLean = 1500;        // ...students half as likely again
        public const int VoiceTriesMost = 64;            // draws for a voice before the slot takes the next adult in order

        // ---- the day's work: slices (Core/WorkSlice), nothing per frame

        public const double SliceMs = 2.0;               // milliseconds of the residents' work a frame
        public const int PeopleStep = 2048;              // people looked at between two looks at the clock
        public const int PostsDayMost = 400;             // posts waiting to be heard at once at the most: past it the oldest are let go (never reached in play)
    }
}
