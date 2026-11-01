using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The residents' day on Flare, in steps a frame can hold (rules.md: nothing per frame, heavy work in slices): the accounts bound to the city as it
    /// stands, the people's indexes, the voices, every resident's follows kept up (and made, the first time), the followers index, the lift the last
    /// day's attention brought and its share for each post and ad, the talk fading, the follows and reviews the day's visits bring, the ads, the
    /// founder-led talk, the posts waiting to be heard, the player's city followers kept with the account and every business's attention for the
    /// population's next day. Each step does what fits in its slice; <see cref="RunAll"/> is the same work at once (the offline checks, a save, a
    /// test). Game-free. COST: nothing while idle; the slice given while it works (Core/DayPump's 2 ms).
    /// </summary>
    public sealed class FlareCityPass
    {
        public enum Phase
        {
            Idle, Begin, People, Voices, Count, Generate, Sweep, Followers, Lift, Decay, Trends, Visits, Ads, Founder, Posts, Reconcile, Attention, Done
        }

        public FlareCityState City;
        public PopulationState State;
        public PoolRules.DemandDay Pools;
        public CityInputs Inputs = new CityInputs();
        public int[] Bears = new int[0];                        // by district: what its customers bear against the game's prices (CityEconomy), 1000 none
        public readonly List<CityPost> Waiting = new List<CityPost>(32);   // posts to hear once the indexes are the day's
        public readonly List<FlareFunnel> WaitingFunnels = new List<FlareFunnel>(32);
        public readonly HearScratch Scratch = new HearScratch();
        public readonly FlareCityRules.ShopIndex Shops = new FlareCityRules.ShopIndex();
        public readonly FlareCityRules.VoiceIndex VoiceIndex = new FlareCityRules.VoiceIndex();
        public int[] BusinessAccount = new int[0];              // by business slot: its account
        public int[] AccountBiz = new int[0];                   // by account: its business slot, -1 for an account that is not a business's
        public int[] PersonSlot = new int[0];                   // by person id: their slot
        public long[] Weights = new long[0];                    // FlareCityRules.OriginWeights
        public int Day = -1;

        // what the day did, for the one log line
        public int Generated, Changed, Reviewed, Followed, Heard, Advertised, Reconciled, Placed = -1;
        public FlareTrend Started;                              // a trend the city's talk started today, for the notice; null none
        public int TrendMoved;                                  // residents or households the trends moved today (taken to it, or let go)
        public long Lifted, FounderGood, FounderBad;

        private Phase _phase = Phase.Idle;
        private int _cursor;
        private int[] _counts = new int[0];
        private int[] _at = new int[0];
        private long[] _chances = new long[0];
        private int _playerChance;
        private readonly int[] _advertisers = new int[FlareCityCatalogue.CityAdsDay * 2];

        public bool Busy => _phase != Phase.Idle;

        public Phase Now => _phase;

        /// <summary>The day begins (after the population lived its day, its pools still standing).</summary>
        public void Begin(FlareCityState c, PopulationState s, PoolRules.DemandDay dd, int day)
        {
            City = c;
            State = s;
            Pools = dd;
            Day = day;
            Generated = Changed = Reviewed = Followed = Heard = Advertised = Reconciled = TrendMoved = 0;
            Placed = -1;
            Started = null;
            Lifted = 0;
            _phase = Phase.Begin;
            _cursor = 0;
        }

        /// <summary>The work of a frame; true once the day is done.</summary>
        public bool Step(ref WorkSlice slice)
        {
            while (_phase != Phase.Idle && slice.More())
            {
                StepOne(ref slice);
            }
            return _phase == Phase.Idle;
        }

        /// <summary>The whole day at once.</summary>
        public void RunAll()
        {
            WorkSlice slice = WorkSlice.Whole;
            while (_phase != Phase.Idle)
            {
                StepOne(ref slice);
            }
        }

        /// <summary>The work stops where it is (the feature stood down, the city unloaded): nothing half done is used.</summary>
        public void Stop()
        {
            _phase = Phase.Idle;
            Waiting.Clear();
            WaitingFunnels.Clear();
        }

        private void StepOne(ref WorkSlice slice)
        {
            FlareCityState c = City;
            PopulationState s = State;
            switch (_phase)
            {
                case Phase.Begin:
                    Bind();
                    _phase = Phase.People;
                    break;
                case Phase.People:
                    FlareCityRules.BuildPeopleIndexes(c, s, Day);
                    PersonSlot = FlareCityRules.PersonSlots(s, PersonSlot);
                    if (c.Pending != null)
                    {
                        Placed = FlareCityStore.Bind(c, s, PersonSlot);   // a load's follows, by person id, on the people as they stand
                    }
                    _phase = Phase.Voices;
                    break;
                case Phase.Voices:
                    Changed = FlareCityRules.Voices(c, s, Day, PersonSlot);
                    VoiceIndex.Build(c, s, s.Districts.Count, PersonSlot);
                    _chances = Chances();
                    _phase = c.Generated < 0 ? Phase.Count : Phase.Sweep;
                    break;
                case Phase.Count:
                    _playerChance = PlayerChance();
                    _cursor = 0;
                    _phase = Phase.Generate;
                    break;
                case Phase.Generate:
                    Generate(ref slice);
                    break;
                case Phase.Sweep:
                    Sweep(ref slice);
                    break;
                case Phase.Followers:
                    Fill(ref slice);
                    break;
                case Phase.Lift:
                    FlareCityRules.MeasureLift(c, s, Pools);
                    for (int b = 0; b < c.BusinessCount; b++)
                    {
                        Lifted += c.Lift[b];
                    }
                    FlareCityRules.Apportion(c, s, BusinessOfPlace);
                    _phase = Phase.Decay;
                    break;
                case Phase.Decay:
                    FlareCityRules.Decay(c);
                    _phase = Phase.Trends;
                    break;
                case Phase.Trends:
                    Started = FlareCityRules.CityTrend(c, s, Day);
                    TrendMoved = FlareCityRules.TrendsDay(c, s, Day, PersonSlot);
                    _cursor = 0;
                    _phase = Phase.Visits;
                    break;
                case Phase.Visits:
                    Visits(ref slice);
                    break;
                case Phase.Ads:
                    Ads();
                    _phase = Phase.Founder;
                    break;
                case Phase.Founder:
                    FlareCityRules.Founder(c, s, Inputs, Day, out FounderGood, out FounderBad);
                    _phase = Phase.Posts;
                    break;
                case Phase.Posts:
                    HearWaiting(ref slice);
                    break;
                case Phase.Reconcile:
                    Reconciled = FlareCityRules.Reconcile(c, s, Day, Inputs);
                    _phase = Phase.Attention;
                    break;
                case Phase.Attention:
                    FlareCityRules.OriginWeights(s, Pools, c.Districts, Weights);
                    FlareCityRules.AttentionAll(c, s, Weights);
                    c.Day = Day;
                    if (c.Generated < 0)
                    {
                        c.Generated = Day;
                    }
                    _phase = Phase.Idle;
                    break;
                default:
                    _phase = Phase.Idle;
                    break;
            }
        }

        // The accounts and the businesses' figures bound to the city as it stands: a slot holding another business starts afresh.
        private void Bind()
        {
            FlareCityState c = City;
            PopulationState s = State;
            BusinessTable bt = s.Businesses;
            c.Seed = c.Seed != 0 ? c.Seed : s.Seed;
            c.IndexAccounts();
            int districts = Math.Max(1, s.Districts.Count);
            bool bound = c.Districts == districts && c.BusinessCount >= bt.Count;
            for (int b = 0; bound && b < bt.Count; b++)
            {
                bound = c.BizKey[b] == null || string.Equals(c.BizKey[b], bt.Key[b], StringComparison.Ordinal);
            }
            if (!bound)
            {
                c.RebindBusinesses(bt.Key, bt.Count, districts);   // the figures follow their business's address to its slot now
            }
            c.GrowBusinesses(bt.Count, districts);
            c.GrowPeople(s.People.Count);
            if (BusinessAccount.Length < bt.Count)
            {
                BusinessAccount = new int[bt.Count + 64];
            }
            for (int b = 0; b < bt.Count; b++)
            {
                if (c.BizKey[b] == null)
                {
                    c.BizKey[b] = bt.Key[b];
                }
                BusinessAccount[b] = bt.Key[b] != null ? c.Account(FlareCityRules.BusinessKey(bt.Key[b])) : -1;
            }
            if (c.AccountOf("v:0") < 0)
            {
                for (int v = 0; v < c.Voices.Length; v++)
                {
                    c.Account("v:" + v.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            VoiceIndex.AccountFirst = c.AccountOf("v:0");
            int accounts = c.Accounts.Count;
            if (AccountBiz.Length < accounts)
            {
                AccountBiz = new int[accounts + 64];
            }
            for (int a = 0; a < AccountBiz.Length; a++)
            {
                AccountBiz[a] = -1;
            }
            for (int b = 0; b < bt.Count; b++)
            {
                if (BusinessAccount[b] >= 0)
                {
                    AccountBiz[BusinessAccount[b]] = b;
                }
            }
            if (_counts.Length < accounts)
            {
                _counts = new int[accounts + 64];
                _at = new int[accounts + 64];
            }
            Array.Clear(_counts, 0, _counts.Length);
            int need = districts * FlareCityRules.Kinds * districts;
            if (Weights.Length < need)
            {
                Weights = new long[need];
            }
            Shops.Build(s, districts);
        }

        // Per million, each influencer's chance of a resident of their interest following them (from the adults who share it).
        private long[] Chances()
        {
            FlareCityState c = City;
            PopulationState s = State;
            int n = Inputs.InfluencerCount;
            var chances = new long[n];
            if (n == 0)
            {
                return chances;
            }
            var interested = new int[FlareCityRules.Kinds];
            int adults = 0;
            for (int i = 0; i < c.AdultList.Length && i < c.AdultStart[c.AdultStart.Length - 1]; i++)
            {
                int h = s.People.Household[c.AdultList[i]];
                if (h < 0 || !s.Households.Alive(h))
                {
                    continue;
                }
                adults++;
                uint tastes = s.Households.Tastes[h];
                for (int k = 0; k < FlareCityRules.Kinds; k++)
                {
                    interested[k] += FlareCityRules.Cares(tastes, k) > 0 ? 1 : 0;
                }
            }
            for (int i = 0; i < n; i++)
            {
                int kind = Inputs.Influencers[i].Kind;
                chances[i] = FlareCityRules.InfluencerChance(Inputs.Influencers[i], kind >= 0 ? interested[kind] : adults);
            }
            return chances;
        }

        private int PlayerChance()
        {
            FlareCityState c = City;
            int adults = 0, home = 0;
            for (int d = 0; d < c.Adults.Length; d++)
            {
                adults += c.Adults[d];
                home += d < Inputs.PlayerDistricts.Length && Inputs.PlayerDistricts[d] ? c.Adults[d] : 0;
            }
            return FlareCityRules.PlayerChance(Inputs.PlayerFollowers, Inputs.PlayerTier, adults, home);
        }

        // The first day: every adult's first follows, the player's among them.
        private void Generate(ref WorkSlice slice)
        {
            FlareCityState c = City;
            PopulationState s = State;
            int n = s.People.Count;
            while (_cursor < n)
            {
                int end = Math.Min(n, _cursor + FlareCityCatalogue.PeopleStep);
                for (int p = _cursor; p < end; p++)
                {
                    int id = s.People.Id[p];
                    FlareCityRules.ClearFollows(c, p);
                    if (id == 0)
                    {
                        c.Owner[p] = 0;
                        continue;
                    }
                    if (FlareCityRules.Age(s, p, Day) < FlareCityCatalogue.AdultAge)
                    {
                        c.Owner[p] = -id;
                        continue;
                    }
                    c.Owner[p] = id;
                    FlareCityRules.FirstFollows(c, s, p, Shops, BusinessAccount, Inputs, _playerChance, _chances, VoiceIndex);
                    Generated++;
                }
                _cursor = end;
                if (!slice.More())
                {
                    return;
                }
            }
            _cursor = 0;
            _phase = Phase.Sweep;
        }

        private void Sweep(ref WorkSlice slice)
        {
            FlareCityState c = City;
            PopulationState s = State;
            int n = s.People.Count;
            if (_cursor == 0)
            {
                Array.Clear(_counts, 0, _counts.Length);
            }
            while (_cursor < n)
            {
                int end = Math.Min(n, _cursor + FlareCityCatalogue.PeopleStep);
                for (int p = _cursor; p < end; p++)
                {
                    FlareCityRules.SweepOne(c, s, p, Day, Shops, BusinessAccount, AccountBiz, Inputs, _counts, _chances, VoiceIndex);
                }
                _cursor = end;
                if (!slice.More())
                {
                    return;
                }
            }
            FlareCityRules.BeginFollowerIndex(c, _counts, c.Accounts.Count);
            Array.Copy(c.FollowerStart, _at, Math.Min(_at.Length, c.FollowerStart.Length));
            _cursor = 0;
            _phase = Phase.Followers;
        }

        private void Fill(ref WorkSlice slice)
        {
            FlareCityState c = City;
            PopulationState s = State;
            int n = s.People.Count;
            while (_cursor < n)
            {
                int end = Math.Min(n, _cursor + FlareCityCatalogue.PeopleStep);
                for (int p = _cursor; p < end; p++)
                {
                    FlareCityRules.FillFollowers(c, s, p, _at);
                }
                _cursor = end;
                if (!slice.More())
                {
                    return;
                }
            }
            for (int b = 0; b < c.BusinessCount && b < BusinessAccount.Length; b++)
            {
                c.Followers[b] = BusinessAccount[b] >= 0 ? c.FollowersOf(BusinessAccount[b], out _) : 0;
            }
            c.CityFollowers = c.FollowersOf(FlareCityState.PlayerAccount, out _);
            _cursor = 0;
            _phase = c.Day >= 0 && Pools != null && Pools.Day >= 0 ? Phase.Lift : Phase.Decay;
        }

        // The day's visits: the residents who follow a business from them, and the reviews its customers write.
        private void Visits(ref WorkSlice slice)
        {
            FlareCityState c = City;
            PopulationState s = State;
            int n = Math.Min(c.BusinessCount, s.Businesses.Count);
            bool pools = Pools != null && Pools.Day >= 0;
            while (_cursor < n && pools)
            {
                int b = _cursor++;
                if (!FlareCityRules.Sells(s.Businesses, b))
                {
                    continue;
                }
                Followed += FlareCityRules.VisitFollows(c, s, Pools, b, Day, BusinessAccount[b]);
                int district = s.Businesses.District[b];
                int bears = district < Bears.Length && Bears[district] > 0 ? Bears[district] : 1000;
                Reviewed += FlareCityRules.Reviews(c, s, Pools, b, Day, bears, FlareCityCatalogue.ReviewsMostDay - Reviewed, Scratch, AccountBiz);
                if (!slice.More())
                {
                    return;
                }
            }
            _cursor = 0;
            _phase = Phase.Ads;
        }

        // The player's ads still running, and the city's own of the day.
        private void Ads()
        {
            FlareCityState c = City;
            PopulationState s = State;
            for (int i = 0; i < c.Ads.Count; i++)
            {
                FlareAd ad = c.Ads[i];
                if (!FlareCityRules.RunsAfterFirst(ad, Day))
                {
                    continue;
                }
                int b = BusinessOfPlace(ad.Place);
                int kind = b >= 0 ? s.Businesses.Kind[b] : -1;
                int account = b >= 0 && b < BusinessAccount.Length ? BusinessAccount[b] : -1;
                FlareCityRules.Deliver(c, s, ad, b, kind, account, Day, AccountBiz, Scratch, c.Funnel(FunnelKind.Ad, ad.Id));
                Advertised++;
            }
            int n = FlareCityRules.CityAdvertisers(s, c.Seed, Day, Inputs.Busy, _advertisers);
            for (int i = 0; i < n; i++)
            {
                int b = _advertisers[i];
                FlareAd ad = FlareCityRules.CityAd(s, b, Day, c.Seed);
                FlareCityRules.Deliver(c, s, ad, b, s.Businesses.Kind[b], BusinessAccount[b], Day, AccountBiz, Scratch, null);
                Advertised++;
            }
        }

        private void HearWaiting(ref WorkSlice slice)
        {
            while (_cursor < Waiting.Count)
            {
                int i = _cursor++;
                FlareCityRules.Hear(City, State, Waiting[i], Day, AccountBiz, Scratch, i < WaitingFunnels.Count ? WaitingFunnels[i] : null);
                Heard++;
                if (!slice.More())
                {
                    return;
                }
            }
            Waiting.Clear();
            WaitingFunnels.Clear();
            _cursor = 0;
            _phase = Phase.Reconcile;
        }

        /// <summary>The population's business slot of a place as Flare writes it ("street~number"), -1 for none.</summary>
        public int BusinessOfPlace(string place)
        {
            string key = PlaceToKey(place);
            return key != null && State != null ? State.BusinessOfKey(key) : -1;
        }

        /// <summary>A place as Flare writes it ("street~number", the street escaped) as the population's key ("street|number"); null for one it cannot read.</summary>
        public static string PlaceToKey(string place)
        {
            int at = place != null ? place.LastIndexOf('~') : -1;
            if (at <= 0 || !int.TryParse(place.Substring(at + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int number))
            {
                return null;
            }
            try
            {
                return PopulationState.KeyOf(Uri.UnescapeDataString(place.Substring(0, at)), number);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
