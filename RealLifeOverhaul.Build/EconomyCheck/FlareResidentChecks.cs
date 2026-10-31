using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Population;

// Flare in the city (plans/tech-overhaul/DRAFT2.md section 4; the brief handover/briefs/living/flare.md; the owner, 31 Oct 2026: "rework Flare
// also. It is kinda useless as of now ... I want something groundbreaking in it"), game-free, on Build 3682's city as the population makes it:
// every adult resident follows accounts, made from who they are; a post is seen by as many followers as Flare's own reach says, resident by
// resident; what residents heard of a business draws people to it from its rivals in the same pools, and never makes people from nothing; the
// day after, what that attention brought is measured; reviews come from residents who really shopped there; ads reach who they target; the save
// keeps every resident's follows by their id; the same seed makes the same city to the byte; and the day's work fits its slices.
internal static partial class Program
{
    private static void FlareResidentChecks()
    {
        FlareResidentFollows();
        FlareResidentHear();
        FlareResidentAttention();
        FlareResidentReviews();
        FlareResidentAds();
        FlareResidentSave();
        FlareResidentCosts();
        FlareResidentMore();
    }

    // ---------------------------------------------------------------- a city to work on: the population's, its first day lived, and Flare's first pass

    private sealed class FlareResidentRig
    {
        public PopulationSim Sim;
        public PopulationState S;
        public List<BusinessSeed> Seeds;
        public FlareCityState C;
        public FlareCityPass Pass;
    }

    private static FlareResidentRig FlareResidentCity(int seed, int followers, int tier)
    {
        var rig = new FlareResidentRig();
        rig.Sim = PopulationMade(seed, out rig.S, out rig.Seeds);
        rig.C = new FlareCityState();
        rig.Pass = new FlareCityPass();
        FlareResidentInputs(rig, followers, tier);
        rig.Pass.Begin(rig.C, rig.S, rig.Sim.Demand, rig.S.Day);
        rig.Pass.RunAll();
        return rig;
    }

    private static void FlareResidentInputs(FlareResidentRig rig, int followers, int tier)
    {
        CityInputs inputs = rig.Pass.Inputs;
        inputs.Day = rig.S.Day;
        inputs.PlayerFollowers = followers;
        inputs.PlayerTier = tier;
        inputs.PlayerDistricts = new bool[rig.S.Districts.Count];
        int midtown = rig.S.DistrictOf("ba:neighborhood_midtown");
        if (midtown >= 0)
        {
            inputs.PlayerDistricts[midtown] = true;
        }
        inputs.Influencers = new[]
        {
            new CityInfluencer { Account = rig.C.Account("i:1"), Kind = (int)Spend.EatingOut, District = midtown, Followers = 20000 },
            new CityInfluencer { Account = rig.C.Account("i:2"), Kind = -1, District = -1, Followers = 6000 }
        };
        inputs.InfluencerCount = 2;
    }

    // A city read back from a line, ready to live its next day as the population's director makes one after a load.
    private static PopulationSim FlareResidentTwin(string line, List<BusinessSeed> seeds, out PopulationState s)
    {
        s = PopulationStore.Decode(line, out _);
        PopulationState city = PopulationCity(out _);
        CityTable.Rebind(s, new List<CityBuilding>(city.Buildings), new List<PopDistrict>(city.Districts));
        LabourRules.Setup(s, seeds);
        var sim = new PopulationSim { State = s, Measure = MeasureRules.Measure(s.Buildings, s.Districts, null) };
        CityTable.Middles(s);
        for (int k = 0; k < s.Businesses.Count; k++)
        {
            s.Businesses.Attention[k] = 0;
        }
        return sim;
    }

    // The biggest business of a kind the population has, -1 for none.
    private static int FlareResidentShop(PopulationState s, Spend kind)
    {
        int best = -1;
        for (int b = 0; b < s.Businesses.Count; b++)
        {
            if (s.Businesses.Kind[b] == (byte)kind && FlareCityRules.Sells(s.Businesses, b) && (best < 0 || s.Businesses.Visits[b] > s.Businesses.Visits[best]))
            {
                best = b;
            }
        }
        return best;
    }

    // ---------------------------------------------------------------- the follows

    private static void FlareResidentFollows()
    {
        FlareResidentRig rig = FlareResidentCity(42, 2000, (int)FlareTier.Local);
        PopulationState s = rig.S;
        FlareCityState c = rig.C;
        int adults = 0, children = 0, owned = 0, young = 0, following = 0, follows = 0;
        for (int p = 0; p < s.People.Count; p++)
        {
            int id = s.People.Id[p];
            if (id == 0)
            {
                continue;
            }
            bool adult = FlareCityRules.Age(s, p, s.Day) >= FlareCityCatalogue.AdultAge;
            adults += adult ? 1 : 0;
            children += adult ? 0 : 1;
            owned += adult && c.Owner[p] == id ? 1 : 0;
            young += !adult && c.Owner[p] == -id ? 1 : 0;
            int n = 0;
            for (int i = 0; i < FlareCityCatalogue.Slots; i++)
            {
                n += c.Follows[p * FlareCityCatalogue.Slots + i] != FlareCityCatalogue.None ? 1 : 0;
            }
            following += n > 0 ? 1 : 0;
            follows += n;
        }
        Check(adults > 5000 && owned == adults && young == children, "flare city follows: every adult resident is on Flare and every child waits for their age (" + owned + " of " + adults + " adults, " + young + " of " + children + " children)");
        Check(following > adults / 3 && follows > adults / 2, "flare city follows: residents follow the city's accounts from who they are (" + following + " of " + adults + " follow something, " + follows + " follows)");
        int target = FlareCityRules.PlayerCityTarget(2000, (int)FlareTier.Local, adults);
        Check(Math.Abs(c.CityFollowers - target) <= Math.Max(20, target / 20), "flare city follows: the player's followers in the city are the account's city share, " + target + " (" + c.CityFollowers + ")");
        Check(target == 1600, "flare city follows: a Local account of 2,000 has four in five of its followers in the city (" + target + ")");
        int midtown = s.DistrictOf("ba:neighborhood_midtown");
        int n0 = c.FollowersOf(FlareCityState.PlayerAccount, out int start);
        int home = 0;
        for (int i = 0; i < n0; i++)
        {
            int p = c.FollowerList[start + i];
            home += p >= 0 && FlareCityRules.DistrictOf(s, p) == midtown ? 1 : 0;
        }
        int midAdults = midtown >= 0 ? c.Adults[midtown] : 0;
        Check(midtown >= 0 && n0 > 0 && home * (long)adults > (long)n0 * midAdults, "flare city follows: the player's own district follows more readily than its share of the city (" + home + " of " + n0 + " against " + midAdults + " of " + adults + " adults)");
        int shops = 0, followed = 0;
        for (int b = 0; b < s.Businesses.Count; b++)
        {
            if (!FlareCityRules.Sells(s.Businesses, b))
            {
                continue;
            }
            shops++;
            followed += c.Followers[b] > 0 ? 1 : 0;
        }
        Check(shops > 100 && followed > shops * 6 / 10, "flare city follows: most of the city's businesses have residents who follow them (" + followed + " of " + shops + ")");
        int i1 = c.AccountOf("i:1"), i2 = c.AccountOf("i:2");
        int f1 = c.FollowersOf(i1, out int s1), f2 = c.FollowersOf(i2, out _);
        bool foodies = true;
        for (int i = 0; i < f1; i++)
        {
            int p = c.FollowerList[s1 + i];
            foodies &= p >= 0 && FlareCityRules.Cares(s.Households.Tastes[s.People.Household[p]], (int)Spend.EatingOut) > 0;
        }
        int eaters = 0;
        for (int p = 0; p < s.People.Count; p++)
        {
            int h = s.People.Household[p];
            eaters += c.Owner[p] > 0 && h >= 0 && FlareCityRules.Cares(s.Households.Tastes[h], (int)Spend.EatingOut) > 0 ? 1 : 0;
        }
        Check(f1 > 0 && foodies && f1 <= eaters * FlareCityCatalogue.InfluencerCityMost * 2 / 1000 + 50,
            "flare city follows: a food influencer is followed by residents who care for eating out, never more than their share of them (" + f1 + " of " + eaters + ")");
        Check(f2 > 0, "flare city follows: an influencer of local news is followed across the city (" + f2 + ")");
        var voices = new HashSet<int>();
        bool alive = true;
        for (int v = 0; v < c.Voices.Length; v++)
        {
            voices.Add(c.Voices[v]);
            int p = FlareCityRules.Slot(rig.Pass.PersonSlot, s, c.Voices[v]);
            alive &= p >= 0 && FlareCityRules.Age(s, p, s.Day) >= FlareCityCatalogue.AdultAge;
        }
        Check(voices.Count == FlareLiveRules.Citizens && alive, "flare city voices: the city's " + FlareLiveRules.Citizens + " voices are " + voices.Count + " different adult residents" + (alive ? string.Empty : ", some not"));
        int young18 = 0;
        for (int v = 0; v < c.Voices.Length; v++)
        {
            CityVoice cv = FlareCityRules.VoiceOf(c, s, v, s.Day, rig.Pass.PersonSlot);
            young18 += cv.Age >= 18 && cv.Age < 40 ? 1 : 0;
        }
        Check(young18 > FlareLiveRules.Citizens / 3, "flare city voices: the young post most (" + young18 + " of " + FlareLiveRules.Citizens + " voices are 18 to 39)");
        // the same seed makes the same follows, to the slot
        FlareResidentRig again = FlareResidentCity(42, 2000, (int)FlareTier.Local);
        bool same = again.C.Follows.Length == c.Follows.Length && again.C.CityFollowers == c.CityFollowers;
        for (int i = 0; same && i < c.Follows.Length; i++)
        {
            same = again.C.Follows[i] == c.Follows[i];
        }
        for (int v = 0; same && v < c.Voices.Length; v++)
        {
            same = again.C.Voices[v] == c.Voices[v];
        }
        Check(same, "flare city follows: the same seed makes the same residents' follows and voices on every machine");
        // a person who dies loses their follows; somebody new in the slot starts with their own
        int dead = -1;
        for (int p = 0; p < s.People.Count && dead < 0; p++)
        {
            dead = c.Owner[p] > 0 && FlareCityRules.Follows(c, p, FlareCityState.PlayerAccount) ? p : -1;
        }
        int deadId = s.People.Id[dead];
        s.People.Id[dead] = 0;
        int[] counts = new int[c.Accounts.Count + 1];
        int[] biz = rig.Pass.AccountBiz;
        FlareCityRules.SweepOne(c, s, dead, s.Day, rig.Pass.Shops, rig.Pass.BusinessAccount, biz, rig.Pass.Inputs, counts, null, rig.Pass.VoiceIndex);
        bool gone = c.Owner[dead] == 0 && !FlareCityRules.Follows(c, dead, FlareCityState.PlayerAccount);
        s.People.Id[dead] = deadId + 1000000;
        FlareCityRules.SweepOne(c, s, dead, s.Day, rig.Pass.Shops, rig.Pass.BusinessAccount, biz, rig.Pass.Inputs, counts, null, rig.Pass.VoiceIndex);
        bool fresh = c.Owner[dead] == s.People.Id[dead] || c.Owner[dead] == -s.People.Id[dead];
        s.People.Id[dead] = deadId;
        Check(gone && fresh, "flare city follows: a resident who dies takes their follows with them, and somebody new in the slot starts afresh");
        // the account's count moves the city's: fewer followers, fewer residents follow; more, residents of the player's districts first
        rig.Pass.Inputs.PlayerFollowers = 1000;
        int dropped = FlareCityRules.Reconcile(c, s, s.Day, rig.Pass.Inputs);
        int target2 = FlareCityRules.PlayerCityTarget(1000, (int)FlareTier.Local, adults);
        Check(dropped < 0 && c.CityFollowers == target2, "flare city follows: followers lost on the account are lost in the city, " + target2 + " (" + c.CityFollowers + ", " + dropped + ")");
        rig.Pass.Inputs.PlayerFollowers = 20000;
        rig.Pass.Inputs.PlayerTier = (int)FlareTier.Known;
        int added = FlareCityRules.Reconcile(c, s, s.Day, rig.Pass.Inputs);
        int target3 = FlareCityRules.PlayerCityTarget(20000, (int)FlareTier.Known, adults);
        Check(added > 0 && c.CityFollowers == target3, "flare city follows: followers won on the account are residents too, " + target3 + " (" + c.CityFollowers + ")");
        Check(FlareCityRules.PlayerCityTarget(10000000, (int)FlareTier.Celebrity, 1000) == 1000 * FlareCityCatalogue.PlayerCityMost / 1000,
            "flare city follows: never more residents follow the player than the city's adults allow (" + FlareCityCatalogue.PlayerCityMost / 10 + "%)");
        Check(FlareCityRules.CityGain(400, (int)FlareTier.Newcomer) == 360 && FlareCityRules.CityGain(400, (int)FlareTier.Celebrity) == 100 && FlareCityRules.CityGain(-5, 0) == 0,
            "flare city follows: a post's new followers are the city's by the account's tier, the rest beyond the city");
    }

    // ---------------------------------------------------------------- a post heard

    private static void FlareResidentHear()
    {
        FlareResidentRig rig = FlareResidentCity(42, 5000, (int)FlareTier.Known);
        PopulationState s = rig.S;
        FlareCityState c = rig.C;
        int b = FlareResidentShop(s, Spend.EatingOut);
        int followers = c.FollowersOf(FlareCityState.PlayerAccount, out _);
        var post = new CityPost
        {
            Id = 7, Account = FlareCityState.PlayerAccount, Business = b, Kind = s.Businesses.Kind[b], Sign = 1, Quality = 1000, Fit = 1150, Seed = 1234, Gain = 120
        };
        var funnel = new FlareFunnel { Post = 7, Place = "x~1" };
        int before = c.CityFollowers;
        HeardReport r = FlareCityRules.Hear(c, s, post, s.Day, rig.Pass.AccountBiz, rig.Pass.Scratch, funnel);
        int expected = followers * FlareCityRules.SeenOf(post) / 1000;
        Check(b >= 0 && r.Seen >= expected * 9 / 10 && r.Seen <= expected * 13 / 10 + r.Discovered + r.Shared * 6 + 20,
            "flare city hear: a post is seen by as many followers as Flare's reach says, resident by resident (" + r.Seen + " seen, " + expected + " of " + followers + " followers expected)");
        Check(r.Interested > 0 && r.Interested < r.Seen && r.Impressions > 0, "flare city hear: some who saw it are interested and remember the business (" + r.Interested + " of " + r.Seen + ", " + r.Impressions / 1000 + " residents)");
        Check(funnel.Seen == r.Seen && funnel.Interested == r.Interested && funnel.Live == r.Impressions, "flare city hear: the post's funnel counts who saw it and who was interested");
        Check(r.Followed > 0 && r.Followed <= post.Gain && c.CityFollowers == before + r.Followed,
            "flare city hear: residents who saw it, were interested and did not follow start to, up to the account's city gain (" + r.Followed + " of " + post.Gain + ")");
        HeardReport again = FlareCityRules.Hear(c, s, post, s.Day, rig.Pass.AccountBiz, rig.Pass.Scratch, null);
        Check(again.Seen > 0 && again.Impressions < r.Impressions, "flare city hear: the same talk again reaches fewer who had not heard (" + again.Impressions / 1000 + " against " + r.Impressions / 1000 + ")");
        CityPost viral = post;
        viral.Id = 8;
        viral.Viral = true;
        HeardReport v = FlareCityRules.Hear(c, s, viral, s.Day, rig.Pass.AccountBiz, rig.Pass.Scratch, null);
        Check(v.Seen > r.Seen * 2 && v.Discovered > r.Discovered, "flare city hear: a viral post is seen by far more, its followers and the district who come across it (" + v.Seen + " against " + r.Seen + ")");
        CityPost selfie = post;
        selfie.Id = 9;
        selfie.Business = -1;
        selfie.Sign = 0;
        HeardReport sf = FlareCityRules.Hear(c, s, selfie, s.Day, rig.Pass.AccountBiz, rig.Pass.Scratch, null);
        Check(sf.Seen > 0 && sf.Impressions == 0, "flare city hear: a post about no business is seen, and leaves no business talked about");
        CityPost bad = post;
        bad.Id = 10;
        bad.Sign = -1;
        long sourBefore = 0, sourAfter = 0;
        FlareCityRules.Talk(c, b, out _, out sourBefore);
        FlareCityRules.Hear(c, s, bad, s.Day, rig.Pass.AccountBiz, rig.Pass.Scratch, null);
        FlareCityRules.Talk(c, b, out _, out sourAfter);
        Check(sourAfter > sourBefore, "flare city hear: a bad post leaves residents who heard bad things (" + sourAfter / 1000 + ")");
        bool bounded = true;
        for (int d = 0; d < c.Districts; d++)
        {
            bounded &= c.Heard[b * c.Districts + d] <= c.Adults[d] * 1000 && c.Sour[b * c.Districts + d] <= c.Adults[d] * 1000;
        }
        Check(bounded, "flare city hear: a district's residents who heard never pass its adults");
        Check(FlareCityRules.SeenOf(post) == 345 && FlareCityRules.SeenOf(viral) == FlareCityCatalogue.SeenViral,
            "flare city hear: an ordinary post is seen by 30% of followers times its quality and fit (" + FlareCityRules.SeenOf(post) + " per mille)");
    }

    // ---------------------------------------------------------------- the attention, people moved between rivals, the lift

    private static void FlareResidentAttention()
    {
        FlareResidentRig rig = FlareResidentCity(42, 25000, (int)FlareTier.Influencer);
        PopulationState s = rig.S;
        FlareCityState c = rig.C;
        int b = FlareResidentShop(s, Spend.Coffee);
        Check(FlareCityRules.AttentionOf(c, s, b, rig.Pass.Weights) == c.Attention[b], "flare city attention: the pass leaves each business's attention for the next day");
        int quiet = c.Attention[b];
        var post = new CityPost { Id = 21, Account = FlareCityState.PlayerAccount, Business = b, Kind = s.Businesses.Kind[b], Sign = 1, Quality = 1000, Fit = 1150, Seed = 99, Viral = true };
        FlareCityRules.Hear(c, s, post, s.Day, rig.Pass.AccountBiz, rig.Pass.Scratch, null);
        int loud = FlareCityRules.AttentionOf(c, s, b, rig.Pass.Weights);
        Check(loud > quiet + 50 && loud <= FlareCityCatalogue.AttentionMost, "flare city attention: a viral post of an influencer draws real attention to the business, far past the old 6% (" + quiet + " to " + loud + " thousandths)");
        // the next day lived by two twins of the same city, one with the attention and one without: the pools' money is the same, only shared otherwise
        string saved = PopulationStore.Encode(s);
        PopulationSim simWith = FlareResidentTwin(saved, rig.Seeds, out PopulationState sw);
        PopulationSim simWithout = FlareResidentTwin(saved, rig.Seeds, out PopulationState twin);
        string key = s.Businesses.Key[b];
        Check(sw.BusinessOfKey(key) == b && twin.BusinessOfKey(key) == b, "flare city attention: a city read back keeps its businesses' slots (the twins compare slot by slot)");
        sw.Businesses.Attention[b] = loud;
        c.Given[b] = loud;
        FlareCityRules.TakeHabit(c, sw.Businesses, sw.Day);   // as the source takes it when the population asks
        int day = s.Day + 1;
        simWith.BeginDay(day, Ordinary, rig.Seeds);
        simWith.RunAll();
        simWithout.BeginDay(day, Ordinary, rig.Seeds);
        simWithout.RunAll();
        long totalWith = 0, totalWithout = 0, rivalsWith = 0, rivalsWithout = 0;
        int db = s.Businesses.District[b], kind = s.Businesses.Kind[b];
        for (int k = 0; k < sw.Businesses.Count; k++)
        {
            totalWith += sw.Businesses.Takings[k];
            long other = k < twin.Businesses.Count ? twin.Businesses.Takings[k] : 0;
            totalWithout += other;
            if (k != b && sw.Businesses.Kind[k] == kind && sw.Businesses.District[k] == db)
            {
                rivalsWith += sw.Businesses.Takings[k];
                rivalsWithout += other;
            }
        }
        long with = sw.Businesses.Takings[b], without = twin.Businesses.Takings[b];
        long spentWith = simWith.Demand.ResidentSpend + simWith.Demand.OutsiderSpend, spentWithout = simWithout.Demand.ResidentSpend + simWithout.Demand.OutsiderSpend;
        Check(spentWith == spentWithout, "flare city attention: the city spends the same with Flare's attention as without (" + spentWith / 100 + ")");
        Check(Math.Abs(totalWith - totalWithout) <= Math.Max(100, totalWith / 1000), "flare city attention: the businesses take the same in all; attention moves people, never makes them (" + totalWith / 100 + " against " + totalWithout / 100 + ")");
        Check(with > without && rivalsWith < rivalsWithout, "flare city attention: the business heard of takes more and its rivals in the same pools less (" + without / 100 + " to " + with / 100 + "; rivals " + rivalsWithout / 100 + " to " + rivalsWith / 100 + ")");
        FlareCityRules.MeasureLift(c, sw, simWith.Demand);
        long measured = c.Lift[b], real = with - without;
        Check(measured > 0 && real > 0 && Math.Abs(measured - real) <= real * 5 / 100 + 500, "flare city lift: the day after, the takings Flare's attention brought are measured from the pools (" + measured / 100 + " measured, " + real / 100 + " in the twin city)");
        Check(c.LiftVisits[b] > 0 && c.LiftVisits[b] <= sw.Businesses.Visits[b], "flare city lift: and the visits it brought (" + c.LiftVisits[b] + " of " + sw.Businesses.Visits[b] + ")");
        // the funnel's share of the lift: the post's live impressions of what residents heard of the business
        var funnel = new FlareFunnel { Post = 21, Place = "p~1", Live = 1 };
        c.Funnels.Clear();
        c.Funnels.Add(funnel);
        FlareCityRules.Talk(c, b, out long heard, out _);
        funnel.Live = (int)Math.Min(int.MaxValue, heard / 2);
        FlareCityRules.Apportion(c, s, place => b);
        Check(funnel.Spent > 0 && funnel.Spent <= c.Lift[b] && Math.Abs(funnel.Spent - c.Lift[b] / 2) <= c.Lift[b] / 50 + 2, "flare city funnel: a post that made half the talk is given half the lift (" + funnel.Spent / 100 + " of " + c.Lift[b] / 100 + ")");
        // bad talk keeps people away; an answer lets it fade faster
        c.Sour[b * c.Districts + db] = c.Adults[db] * 1000;
        c.Heard[b * c.Districts + db] = 0;
        int sour = FlareCityRules.AttentionOf(c, s, b, rig.Pass.Weights);
        Check(sour < 0, "flare city attention: residents who heard bad things stay away (" + sour + " thousandths)");
        int keep = c.Sour[b * c.Districts + db];
        FlareCityRules.Decay(c);
        int unanswered = c.Sour[b * c.Districts + db];
        Check(FlareCityRules.Answer(c, b), "flare city attention: bad talk can be answered");
        FlareCityRules.Decay(c);
        int answered = c.Sour[b * c.Districts + db];
        Check(unanswered < keep && answered < (long)unanswered * FlareCityCatalogue.SourKeep / 1000, "flare city attention: unanswered bad talk lingers, answered it fades faster (" + keep + ", " + unanswered + ", " + answered + ")");
        Check(FlareCityRules.AttentionOf(c, s, -1, rig.Pass.Weights) == 0 && FlareCityRules.AttentionOf(c, s, s.Businesses.Count + 5, rig.Pass.Weights) == 0, "flare city attention: no business, no attention");
    }

    // ---------------------------------------------------------------- reviews from residents who really shopped there

    private static void FlareResidentReviews()
    {
        FlareResidentRig rig = FlareResidentCity(42, 2000, (int)FlareTier.Local);
        PopulationState s = rig.S;
        FlareCityState c = rig.C;
        PoolRules.DemandDay dd = rig.Sim.Demand;
        int reviews = 0, real = 0, checkedReviews = 0;
        var mine = new long[s.Businesses.Count];
        var people = new Dictionary<int, int>();
        for (int p = 0; p < s.People.Count; p++)
        {
            if (s.People.Id[p] != 0)
            {
                people[s.People.Id[p]] = p;
            }
        }
        var newest = new FlareCityReview[FlareCityCatalogue.ReviewRing];
        for (int b = 0; b < s.Businesses.Count && checkedReviews < 40; b++)
        {
            int n = FlareCityRules.NewestReviews(c, b, newest);
            reviews += c.ReviewCount[b];
            for (int i = 0; i < n && checkedReviews < 40; i++)
            {
                if (!people.TryGetValue(newest[i].Person, out int p))
                {
                    continue;
                }
                int h = s.People.Household[p];
                Array.Clear(mine, 0, mine.Length);
                PoolRules.Attribute(s, dd, h, mine);
                checkedReviews++;
                real += mine[b] > 0 ? 1 : 0;
            }
        }
        Check(reviews > 0 && checkedReviews > 10 && real == checkedReviews, "flare city reviews: every reviewer's household really spent at the business that day, by the pools (" + real + " of " + checkedReviews + ", " + reviews + " reviews written)");
        int b0 = FlareResidentShop(s, Spend.Groceries);
        long visits = s.Businesses.Visits[b0];
        Check(b0 >= 0 && c.ReviewCount[b0] + c.ReviewDue[b0] / FlareCityCatalogue.VisitsPerReview <= visits / FlareCityCatalogue.VisitsPerReview + 1,
            "flare city reviews: one review for every " + FlareCityCatalogue.VisitsPerReview + " visits (" + c.ReviewCount[b0] + " for " + visits + ")");
        BusinessTable bt = s.Businesses;
        byte content = bt.Content[b0];
        bt.Content[b0] = 95;
        int good = FlareCityRules.Stars(bt, b0, 1, 1000, 500);
        bt.Content[b0] = 15;
        int poor = FlareCityRules.Stars(bt, b0, 1, 1000, 500);
        bt.Content[b0] = 70;
        int price = bt.Price[b0];
        bt.Price[b0] = 1600;
        int dear = FlareCityRules.Stars(bt, b0, 0, 1000, 500);
        bt.Price[b0] = price;
        int fair = FlareCityRules.Stars(bt, b0, 0, 1000, 500);
        bt.Content[b0] = content;
        Check(good == 5 && poor <= 2 && dear < fair, "flare city reviews: the stars follow satisfaction, and prices past what the reviewer's class bears cost stars (" + good + ", " + poor + ", " + dear + " against " + fair + ")");
        FlareCityRules.AddReview(c, b0, 1, 5, s.Day);
        FlareCityRules.AddReview(c, b0, 2, 1, s.Day);
        Check(c.Rating(b0) > 10 && c.Rating(b0) < 50, "flare city reviews: a rating is the reviews' average, in tenths of a star (" + c.Rating(b0) + ")");
        Check(FlareCityRules.Customer(c, s, null, b0, 1, 0) == -1 && FlareCityRules.Customer(c, s, dd, -1, 1, 0) == -1, "flare city reviews: with no pools, no customer is drawn");
    }

    // ---------------------------------------------------------------- ads

    private static void FlareResidentAds()
    {
        FlareResidentRig rig = FlareResidentCity(42, 2000, (int)FlareTier.Local);
        PopulationState s = rig.S;
        FlareCityState c = rig.C;
        int b = FlareResidentShop(s, Spend.Clothing);
        int midtown = s.DistrictOf("ba:neighborhood_midtown");
        var all = new FlareAd { Id = 1, District = -1, Reach = 3000, Days = 1, From = s.Day };
        var narrow = new FlareAd { Id = 2, District = midtown, Age = 1, Class = 1, Interest = (int)Spend.Clothing, Reach = 3000, Days = 1, From = s.Day };
        int everyone = FlareCityRules.TargetSize(c, s, all, s.Day), few = FlareCityRules.TargetSize(c, s, narrow, s.Day);
        Check(everyone > few && few > 0, "flare city ads: a target by district, age, class and interest is a part of the city's residents (" + few + " of " + everyone + ")");
        var funnel = new FlareFunnel { Kind = FunnelKind.Ad, Post = 2 };
        HeardReport r = FlareCityRules.Deliver(c, s, narrow, b, (int)Spend.Clothing, rig.Pass.BusinessAccount[b], s.Day, rig.Pass.AccountBiz, rig.Pass.Scratch, funnel);
        Check(r.Seen == Math.Min(narrow.Reach, few) && r.Interested > r.Seen / 2 && r.Impressions > 0, "flare city ads: an ad is shown to as many of its target as it bought, and most who care for what it sells are interested (" + r.Seen + " seen, " + r.Interested + " interested)");
        HeardReport broad = FlareCityRules.Deliver(c, s, all, b, (int)Spend.Clothing, rig.Pass.BusinessAccount[b], s.Day, rig.Pass.AccountBiz, rig.Pass.Scratch, null);
        Check(broad.Seen == all.Reach && broad.Interested * 1000L / broad.Seen < r.Interested * 1000L / Math.Max(1, r.Seen), "flare city ads: an untargeted ad of the same reach finds fewer interested (" + broad.Interested + " of " + broad.Seen + ")");
        Check(FlareCityRules.AdCost(1000, 1, 1000) == FlareCityCatalogue.AdCpm * 100 && FlareCityRules.AdCost(3000, 7, 2000) == 3000L * 7 * FlareCityCatalogue.AdCpm * 100 / 1000 * 2,
            "flare city ads: an ad costs " + FlareCityCatalogue.AdCpm + " a thousand residents a day, at the game's prices");
        Check(FlareCityRules.RunsAfterFirst(new FlareAd { From = 10, Days = 3 }, 11) && FlareCityRules.RunsAfterFirst(new FlareAd { From = 10, Days = 3 }, 12)
              && !FlareCityRules.RunsAfterFirst(new FlareAd { From = 10, Days = 3 }, 13) && !FlareCityRules.RunsAfterFirst(new FlareAd { From = 10, Days = 3 }, 10),
            "flare city ads: an ad of three days runs on its first day (when bought) and the two after");
        var into = new int[FlareCityCatalogue.CityAdsDay * 2];
        int n = FlareCityRules.CityAdvertisers(s, c.Seed, s.Day, 1, into);
        bool notPlayers = true;
        for (int i = 0; i < n; i++)
        {
            notPlayers &= s.Businesses.Owner[into[i]] != (byte)Owner.Player;
        }
        Check(n > 0 && notPlayers, "flare city ads: the city's own businesses run ads too, by the same rules (" + n + " today)");
    }

    // ---------------------------------------------------------------- the save

    private static void FlareResidentSave()
    {
        FlareResidentRig rig = FlareResidentCity(42, 2000, (int)FlareTier.Local);
        PopulationState s = rig.S;
        FlareCityState c = rig.C;
        int b = FlareResidentShop(s, Spend.EatingOut);
        FlareCityRules.AddReview(c, b, s.People.Id[0], 4, s.Day);
        c.Funnels.Add(new FlareFunnel { Post = 3, Kind = FunnelKind.Post, Place = "a~1", Day = 2, Seen = 10, Interested = 4, Followed = 1, Came = 3, Spent = 1200, Live = 900 });
        c.Ads.Add(new FlareAd { Id = 5, Place = "a~1", Name = "Five Cups", District = 2, Age = 1, Class = 0, Interest = 4, Reach = 1000, Days = 3, From = 2, Cost = 6600 });
        string line = FlareCityStore.Encode(c);
        FlareCityState back = FlareCityStore.Decode(line, out int version);
        Check(back != null && version == FlareCityStore.Version, "flare city save: the line reads back (" + line.Length / 1024 + " KB)");
        int placed = FlareCityStore.Bind(back, s, rig.Pass.PersonSlot);
        bool follows = true;
        int compared = 0;
        for (int p = 0; p < s.People.Count && follows; p++)
        {
            if (c.Owner[p] == 0)
            {
                continue;
            }
            compared++;
            follows = back.Owner[p] == c.Owner[p];
            for (int i = 0; i < FlareCityCatalogue.Slots && follows; i++)
            {
                ushort f = c.Follows[p * FlareCityCatalogue.Slots + i];
                follows = f == FlareCityCatalogue.None || FlareCityRules.Follows(back, p, f);
            }
        }
        Check(follows && placed == compared && placed > 5000, "flare city save: every resident's follows come back on the person, by their id (" + placed + ")");
        bool figures = back.BizKey[b] == c.BizKey[b] && back.Rating(b) == c.Rating(b) && back.ReviewCount[b] == c.ReviewCount[b] && back.Attention[b] == c.Attention[b];
        for (int d = 0; figures && d < c.Districts; d++)
        {
            figures = back.Heard[b * c.Districts + d] == c.Heard[b * c.Districts + d] && back.Sour[b * c.Districts + d] == c.Sour[b * c.Districts + d];
        }
        Check(figures, "flare city save: each business's figures, reviews and talk come back by its key");
        FlareFunnel f2 = back.Funnel(FunnelKind.Post, 3);
        FlareAd ad = back.Ad(5);
        Check(f2 != null && f2.Spent == 1200 && f2.Came == 3 && f2.Live == 900 && ad != null && ad.Name == "Five Cups" && ad.Cost == 6600 && ad.Interest == 4,
            "flare city save: the funnel and the ads come back whole");
        bool voices = true;
        for (int v = 0; v < c.Voices.Length && voices; v++)
        {
            voices = back.Voices[v] == c.Voices[v];
        }
        Check(voices && back.CityFollowers == c.CityFollowers && back.Seed == c.Seed && back.Day == c.Day && back.Accounts.Count == c.Accounts.Count, "flare city save: the voices, the counts and the accounts come back");
        Check(FlareCityStore.Encode(back) == FlareCityStore.Encode(back), "flare city save: the same state writes the same line, to the byte");
        Check(FlareCityStore.Decode("v=1~AAAA", out _) == null && FlareCityStore.Decode("garbage", out _) == null && FlareCityStore.Decode(null, out _) == null
              && FlareCityStore.Decode(line.Substring(0, line.Length / 2), out _) == null, "flare city save: a damaged or cut line gives no state, so the follows are made again");
        // a city generated again: the figures follow their business's address to its slot
        string[] keys = new string[s.Businesses.Count];
        for (int k = 0; k < keys.Length; k++)
        {
            keys[k] = s.Businesses.Key[(k + 7) % keys.Length];
        }
        int rating = c.Rating(b);
        string key = c.BizKey[b];
        c.RebindBusinesses(keys, keys.Length, c.Districts);
        int moved = Array.IndexOf(c.BizKey, key);
        Check(moved >= 0 && moved != b && c.Rating(moved) == rating, "flare city save: a business's figures follow its address when the slots change");
    }

    // ---------------------------------------------------------------- the costs

    private static void FlareResidentCosts()
    {
        PopulationSim sim = PopulationMade(42, out PopulationState s, out List<BusinessSeed> seeds);
        var c = new FlareCityState();
        var pass = new FlareCityPass();
        pass.Inputs.PlayerFollowers = 5000;
        pass.Inputs.PlayerTier = (int)FlareTier.Known;
        pass.Inputs.PlayerDistricts = new bool[s.Districts.Count];
        double worst = 0, total = 0;
        int slices = 0;
        for (int round = 0; round < 3; round++)
        {
            pass.Begin(c, s, sim.Demand, s.Day + round);
            var watch = new Stopwatch();
            while (pass.Busy)
            {
                watch.Restart();
                WorkSlice slice = WorkSlice.Of(FlareCityCatalogue.SliceMs, 1);
                pass.Step(ref slice);
                double ms = watch.Elapsed.TotalMilliseconds;
                worst = Math.Max(worst, round > 0 ? ms : 0);   // the first day makes every follow; it is behind its own slices too
                total += ms;
                slices++;
            }
        }
        Check(worst < FlareCityCatalogue.SliceMs * 6, "flare city costs: a slice of the residents' day stays near its " + FlareCityCatalogue.SliceMs + " ms (" + worst.ToString("0.00") + " ms at worst, " + slices + " slices, " + total.ToString("0") + " ms in all over three days)");
        var watchSave = Stopwatch.StartNew();
        string line = FlareCityStore.Encode(c);
        double saveMs = watchSave.Elapsed.TotalMilliseconds;
        Check(line.Length < 2 * 1024 * 1024 && saveMs < 400, "flare city costs: the save's line is small and quick (" + line.Length / 1024 + " KB in " + saveMs.ToString("0") + " ms with one stream; the game's save compresses it on its workers)");
        long bytes = (long)c.Owner.Length * (4 + 2 * FlareCityCatalogue.Slots) + (long)c.BusinessCount * (c.Districts * 8 + 120);
        Check(bytes < 4L * 1024 * 1024, "flare city costs: what it keeps is a few bytes a resident and a few hundred a business (" + bytes / 1024 + " KB)");
    }
}
