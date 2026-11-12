using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>An influencer as the residents' follows see it: its account, the kind of spending its niche is about, its home district and its followers.</summary>
    public struct CityInfluencer
    {
        public int Account;
        public int Kind;             // Population.Spend, -1 for a niche of no kind (local news)
        public int District;         // the population's district, -1 none
        public int Followers;        // its followers in all (the cast's figure)
    }

    /// <summary>What the residents' day reads from the rest of Flare (FlareDirector.Residents fills it in).</summary>
    public sealed class CityInputs
    {
        public int Day;
        public int PlayerFollowers;              // the player's followers in all (FlareFile.Followers)
        public int PlayerTier;                   // FlareTier
        public bool[] PlayerDistricts = new bool[0];   // by the population's district: the player's home and businesses
        public int[] PlayerBusinesses = new int[0];    // the population's business slots of the player's businesses
        public int PlayerBusinessCount;
        public CityInfluencer[] Influencers = new CityInfluencer[0];
        public int InfluencerCount;
        public int Busy = 1;                     // the feed's busyness option: 0 quiet, 1 normal, 2 busy (the city's ads follow it)
        public bool Scandal;                     // the player is in a scandal nobody has answered (FlareCityRules.ScandalSour)
    }

    /// <summary>
    /// Flare in the city by rule (DRAFT2.md section 4; the owner, 31 Oct 2026: "followers never become money ... I want something groundbreaking"):
    /// the residents' follows, a resident at a time, made from who they are (their district, their household's tastes, the shops they use) and kept
    /// up day by day as people are born, come of age, move on and die. This part: the hashes every roll is made of (the city's seed, the day, the
    /// person, never a machine's clock), the kinds a household cares for, the follows themselves and the day's indexes. Game-free.
    /// COST: nothing per frame; the day's indexes a pass over the people (about 3 ms for 50,000 in .NET), sliced by FlareCityPass.
    /// </summary>
    public static partial class FlareCityRules
    {
        public const int Kinds = PopulationCatalogue.Categories;
        private const int SaltPlayer = 11, SaltShop = 12, SaltShopPick = 13, SaltVoice = 14, SaltVoicePick = 15, SaltInfluencer = 16, SaltUnfollow = 17;
        private const int SaltReconcile = 19, SaltDrop = 20;

        // ---------------------------------------------------------------- the rolls

        /// <summary>A hash of four numbers: the same everywhere for the same four (every roll of Flare in the city is one).</summary>
        public static uint Hash(int seed, int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)a * 0x85EBCA77u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)b * 0xC2B2AE3Du;
                h = (h << 11) | (h >> 21);
                h ^= (uint)c * 0x27D4EB2Fu;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return h;
            }
        }

        /// <summary>0 to 999 for the same four numbers.</summary>
        public static int Mille(int seed, int a, int b, int c) => (int)(Hash(seed, a, b, c) % 1000u);

        /// <summary>0 to 999,999 for the same four numbers: a finer roll for a small chance.</summary>
        public static int Millionth(int seed, int a, int b, int c) => (int)(Hash(seed, a, b, c) % 1000000u);

        // ---------------------------------------------------------------- who a resident is

        private static readonly uint[] KindMasks = MakeKindMasks();

        private static uint[] MakeKindMasks()
        {
            var masks = new uint[Kinds];
            for (int bit = 0; bit < GenerateRules.TasteBits && bit < GenerateRules.TasteKinds.Length; bit++)
            {
                masks[(int)GenerateRules.TasteKinds[bit]] |= 1u << bit;
            }
            return masks;
        }

        /// <summary>Whether a household's tastes are for a kind of spending: 1 yes, 0 no; a kind no taste names (groceries) is everybody's a little: -1.</summary>
        public static int Cares(uint tastes, int kind)
        {
            if (kind < 0 || kind >= Kinds)
            {
                return 0;
            }
            uint mask = KindMasks[kind];
            return mask == 0 ? -1 : (tastes & mask) != 0 ? 1 : 0;
        }

        /// <summary>Per mille a resident with these tastes is interested in something of a kind.</summary>
        public static int InterestOf(uint tastes, int kind, int match, int other)
        {
            int cares = Cares(tastes, kind);
            return cares > 0 ? match : cares < 0 ? (match + other) / 2 : other;
        }

        /// <summary>The kind of spending an influencer's niche is about, -1 for none (local news is for anybody).</summary>
        public static int KindOfNiche(FlareNiche niche)
        {
            switch (niche)
            {
                case FlareNiche.Food: return (int)Spend.EatingOut;
                case FlareNiche.Fashion: return (int)Spend.Clothing;
                case FlareNiche.Fitness: return (int)Spend.Gym;
                case FlareNiche.Nightlife: return (int)Spend.NightLife;
                case FlareNiche.Tech: return (int)Spend.Electronics;
                case FlareNiche.Business: return (int)Spend.Professional;
                default: return -1;
            }
        }

        /// <summary>A person's age in whole years on a day.</summary>
        public static int Age(PopulationState s, int p, int day) => PopulationState.AgeOf(s.People.Birth[p], day, s.YearDays);

        /// <summary>The population's district a person lives in, -1 for none.</summary>
        public static int DistrictOf(PopulationState s, int p)
        {
            int h = s.People.Household[p];
            return h >= 0 && s.Households.Alive(h) ? s.Households.District[h] : -1;
        }

        /// <summary>The account key of a business: "b:" and its address key ("street|number").</summary>
        public static string BusinessKey(string addressKey) => "b:" + addressKey;

        /// <summary>A person's slot by their id through the id-to-slot table (built at each pass), checked against the population; -1 when gone.</summary>
        public static int Slot(int[] personSlot, PopulationState s, int id)
        {
            if (personSlot == null || id <= 0 || id >= personSlot.Length)
            {
                return -1;
            }
            int slot = personSlot[id];
            return slot >= 0 && slot < s.People.Count && s.People.Id[slot] == id ? slot : -1;
        }

        // ---------------------------------------------------------------- the follows of one resident

        public static bool Follows(FlareCityState c, int slot, int account)
        {
            int at = slot * FlareCityCatalogue.Slots;
            for (int i = 0; i < FlareCityCatalogue.Slots; i++)
            {
                if (c.Follows[at + i] == account)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A resident follows an account, in a free slot; false when they did already or have no room.</summary>
        public static bool Follow(FlareCityState c, int slot, int account)
        {
            if (account < 0 || account >= FlareCityCatalogue.None)
            {
                return false;
            }
            int at = slot * FlareCityCatalogue.Slots, free = -1;
            for (int i = 0; i < FlareCityCatalogue.Slots; i++)
            {
                ushort f = c.Follows[at + i];
                if (f == account)
                {
                    return false;
                }
                if (f == FlareCityCatalogue.None && free < 0)
                {
                    free = i;
                }
            }
            if (free < 0)
            {
                return false;
            }
            c.Follows[at + free] = (ushort)account;
            return true;
        }

        /// <summary>A resident follows an account, making room by dropping a follow of a business when every slot is taken; false when they did already.</summary>
        public static bool FollowMakingRoom(FlareCityState c, int slot, int account, int[] accountBiz, int roll)
        {
            if (Follow(c, slot, account))
            {
                return true;
            }
            if (Follows(c, slot, account))
            {
                return false;
            }
            int at = slot * FlareCityCatalogue.Slots;
            for (int k = 0; k < FlareCityCatalogue.Slots; k++)
            {
                int i = (roll + k) % FlareCityCatalogue.Slots;
                ushort f = c.Follows[at + i];
                if (f != FlareCityState.PlayerAccount && f < accountBiz.Length && accountBiz[f] >= 0)
                {
                    c.Follows[at + i] = (ushort)account;
                    return true;
                }
            }
            return false;
        }

        public static bool Unfollow(FlareCityState c, int slot, int account)
        {
            int at = slot * FlareCityCatalogue.Slots;
            for (int i = 0; i < FlareCityCatalogue.Slots; i++)
            {
                if (c.Follows[at + i] == account)
                {
                    c.Follows[at + i] = FlareCityCatalogue.None;
                    return true;
                }
            }
            return false;
        }

        public static void ClearFollows(FlareCityState c, int slot)
        {
            int at = slot * FlareCityCatalogue.Slots;
            for (int i = 0; i < FlareCityCatalogue.Slots; i++)
            {
                c.Follows[at + i] = FlareCityCatalogue.None;
            }
        }

        // ---------------------------------------------------------------- the shops and voices a resident may follow

        /// <summary>The businesses of each district and kind, with their room for customers summed, for a pick by weight (made at each day's start).</summary>
        public sealed class ShopIndex
        {
            public int Districts;
            public int[] Start = new int[1];     // (district * Kinds + kind) + 1 entries
            public int[] List = new int[0];      // business slots
            public long[] Cum = new long[0];     // room summed along List, within each district and kind

            public void Build(PopulationState s, int districts)
            {
                BusinessTable bt = s.Businesses;
                Districts = Math.Max(1, districts);
                int cells = Districts * Kinds;
                if (Start.Length != cells + 1)
                {
                    Start = new int[cells + 1];
                }
                Array.Clear(Start, 0, Start.Length);
                for (int b = 0; b < bt.Count; b++)
                {
                    if (Sells(bt, b) && bt.District[b] < Districts)
                    {
                        Start[bt.District[b] * Kinds + bt.Kind[b] + 1]++;
                    }
                }
                for (int i = 0; i < cells; i++)
                {
                    Start[i + 1] += Start[i];
                }
                int n = Start[cells];
                if (List.Length < n)
                {
                    List = new int[n];
                    Cum = new long[n];
                }
                var at = new int[cells];
                Array.Copy(Start, at, cells);
                for (int b = 0; b < bt.Count; b++)
                {
                    if (Sells(bt, b) && bt.District[b] < Districts)
                    {
                        List[at[bt.District[b] * Kinds + bt.Kind[b]]++] = b;
                    }
                }
                for (int cell = 0; cell < cells; cell++)
                {
                    long sum = 0;
                    for (int i = Start[cell]; i < Start[cell + 1]; i++)
                    {
                        sum += Math.Max(1, bt.Room[List[i]]);
                        Cum[i] = sum;
                    }
                }
            }

            /// <summary>A business of a kind in a district by its room for customers, from a roll of 0 to 999,999; -1 for none there.</summary>
            public int Pick(int district, int kind, int roll)
            {
                if (district < 0 || district >= Districts || kind < 0 || kind >= Kinds)
                {
                    return -1;
                }
                int cell = district * Kinds + kind, from = Start[cell], to = Start[cell + 1];
                if (to <= from)
                {
                    return -1;
                }
                long total = Cum[to - 1];
                long x = (long)roll * total / 1000000;
                int lo = from, hi = to - 1;
                while (lo < hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (Cum[mid] > x)
                    {
                        hi = mid;
                    }
                    else
                    {
                        lo = mid + 1;
                    }
                }
                return List[lo];
            }
        }

        /// <summary>The city's voices by the district they live in, and the account of the first voice (the voices' accounts follow it in order).</summary>
        public sealed class VoiceIndex
        {
            public int[] Start = new int[1];     // district + 1 entries
            public int[] List = new int[0];      // voice numbers (0 to 399)
            public int AccountFirst = -1;        // the account of voice 0

            public void Build(FlareCityState c, PopulationState s, int districts, int[] personSlot)
            {
                districts = Math.Max(1, districts);
                if (Start.Length != districts + 1)
                {
                    Start = new int[districts + 1];
                }
                Array.Clear(Start, 0, Start.Length);
                if (List.Length < c.Voices.Length)
                {
                    List = new int[c.Voices.Length];
                }
                var home = new int[c.Voices.Length];
                for (int v = 0; v < c.Voices.Length; v++)
                {
                    int p = Slot(personSlot, s, c.Voices[v]);
                    home[v] = p >= 0 ? DistrictOf(s, p) : -1;
                    if (home[v] >= 0 && home[v] < districts)
                    {
                        Start[home[v] + 1]++;
                    }
                }
                for (int d = 0; d < districts; d++)
                {
                    Start[d + 1] += Start[d];
                }
                var at = new int[districts];
                Array.Copy(Start, at, districts);
                for (int v = 0; v < c.Voices.Length; v++)
                {
                    if (home[v] >= 0 && home[v] < districts)
                    {
                        List[at[home[v]]++] = v;
                    }
                }
            }

            /// <summary>The account of a voice of a district by a roll, -1 for none there.</summary>
            public int Pick(int district, int roll)
            {
                if (AccountFirst < 0 || district < 0 || district + 1 >= Start.Length)
                {
                    return -1;
                }
                int from = Start[district], n = Start[district + 1] - from;
                return n > 0 ? AccountFirst + List[from + roll % n] : -1;
            }
        }

        /// <summary>Whether a business of the population's sells to people (a kind of spending a shop sells, seen at the last look).</summary>
        public static bool Sells(BusinessTable bt, int b)
        {
            int k = bt.Kind[b];
            return bt.Seen[b] && k >= PopulationCatalogue.ShopCategoryFirst && k <= PopulationCatalogue.ShopCategoryLast;
        }

        // ---------------------------------------------------------------- a resident's first follows

        /// <summary>
        /// The follows a resident starts with when Flare first meets them (the first day, or when they come of age or move to the city): the shops of
        /// the kinds their household cares for and their grocer, a voice or two of their district, the influencers of their interests, and on the first
        /// day the player, at the share the player's account has in the city. The same resident, seed and day give the same follows on every machine.
        /// </summary>
        public static void FirstFollows(FlareCityState c, PopulationState s, int slot, ShopIndex shops, int[] businessAccount, CityInputs inputs,
            int playerPerMillion, long[] influencerChance, VoiceIndex voices)
        {
            int id = s.People.Id[slot];
            int h = s.People.Household[slot];
            if (h < 0 || !s.Households.Alive(h))
            {
                return;
            }
            int d = s.Households.District[h];
            uint tastes = s.Households.Tastes[h];
            int seed = c.Seed;
            if (playerPerMillion > 0)
            {
                bool home = d >= 0 && d < inputs.PlayerDistricts.Length && inputs.PlayerDistricts[d];
                long chance = home ? (long)playerPerMillion * FlareCityCatalogue.HomeLean / 1000 : playerPerMillion;
                if (Millionth(seed, id, SaltPlayer, 0) < chance)
                {
                    Follow(c, slot, FlareCityState.PlayerAccount);
                }
            }
            for (int k = PopulationCatalogue.ShopCategoryFirst; k <= PopulationCatalogue.ShopCategoryLast; k++)
            {
                int cares = Cares(tastes, k);
                int chance = cares > 0 ? FlareCityCatalogue.ShopFollow : k == (int)Spend.Groceries ? FlareCityCatalogue.GroceryFollow : 0;
                if (chance <= 0 || Mille(seed, id, SaltShop, k) >= chance)
                {
                    continue;
                }
                int b = shops.Pick(d, k, Millionth(seed, id, SaltShopPick, k));
                if (b >= 0 && b < businessAccount.Length && businessAccount[b] >= 0)
                {
                    Follow(c, slot, businessAccount[b]);
                }
            }
            for (int t = 0; voices != null && t < FlareCityCatalogue.VoiceTries; t++)
            {
                if (Mille(seed, id, SaltVoice, t) < FlareCityCatalogue.VoiceFollow)
                {
                    int v = voices.Pick(d, Mille(seed, id, SaltVoicePick, t));
                    if (v >= 0)
                    {
                        Follow(c, slot, v);
                    }
                }
            }
            for (int i = 0; influencerChance != null && i < inputs.InfluencerCount && i < influencerChance.Length; i++)
            {
                CityInfluencer inf = inputs.Influencers[i];
                if (inf.Account < 0 || (inf.Kind >= 0 && Cares(tastes, inf.Kind) == 0))
                {
                    continue;
                }
                long chance = d >= 0 && d == inf.District ? Math.Min(1000000L, influencerChance[i] * 2) : influencerChance[i];
                if (Millionth(seed, id, SaltInfluencer, inf.Account) < chance)
                {
                    Follow(c, slot, inf.Account);
                }
            }
        }

        /// <summary>
        /// Per million an adult of an influencer's interest follows them: their followers' city share (a known account's, three fifths) over the city's
        /// adults who share the interest (all adults for local news), at most <see cref="FlareCityCatalogue.InfluencerCityMost"/>; their own district
        /// doubles it in <see cref="FirstFollows"/>.
        /// </summary>
        public static long InfluencerChance(CityInfluencer inf, int interestedAdults)
        {
            long city = (long)Math.Max(0, inf.Followers) * FlareCityCatalogue.CityShare[(int)FlareTier.Known] / 1000;
            long perMillion = city * 1000000L / Math.Max(1, interestedAdults);
            return Math.Min(perMillion, FlareCityCatalogue.InfluencerCityMost * 1000L);
        }

        /// <summary>
        /// Per million of the city's adults who follow the player when follows are first made: the account's city share of its followers over the
        /// city's adults (its own districts weighed at <see cref="FlareCityCatalogue.HomeLean"/>), at most <see cref="FlareCityCatalogue.PlayerCityMost"/>.
        /// </summary>
        public static int PlayerChance(int followers, int tier, int adults, int homeAdults)
        {
            long target = PlayerCityTarget(followers, tier, adults);
            long weighed = (long)(adults - homeAdults) * 1000 + (long)homeAdults * FlareCityCatalogue.HomeLean;   // thousandths of an adult
            if (target <= 0 || weighed <= 0)
            {
                return 0;
            }
            return (int)Math.Min(1000000L, target * 1000000L * 1000L / weighed);
        }

        /// <summary>The player's followers who live in the city, by the account's own count and tier, within the city's adults.</summary>
        public static int PlayerCityTarget(int followers, int tier, int adults)
        {
            int share = FlareCityCatalogue.CityShare[Math.Max(0, Math.Min(tier, FlareCityCatalogue.CityShare.Length - 1))];
            long target = (long)Math.Max(0, followers) * share / 1000;
            long most = (long)Math.Max(0, adults) * FlareCityCatalogue.PlayerCityMost / 1000;
            return (int)Math.Min(target, most);
        }

        // ---------------------------------------------------------------- the day's sweep of the people

        /// <summary>
        /// One person slot in the day's sweep: a slot whose person changed starts afresh (somebody new, their first follows once they are an adult);
        /// each follow of a business may be dropped (people move on); the follows counted by account for the followers index.
        /// </summary>
        public static void SweepOne(FlareCityState c, PopulationState s, int slot, int day, ShopIndex shops, int[] businessAccount, int[] accountBiz, CityInputs inputs,
            int[] counts, long[] influencerChance, VoiceIndex voices)
        {
            int id = s.People.Id[slot];
            if (id == 0)
            {
                if (c.Owner[slot] != 0)
                {
                    c.Owner[slot] = 0;
                    ClearFollows(c, slot);   // the person left the city or died: their follows go with them
                }
                return;
            }
            bool adult = Age(s, slot, day) >= FlareCityCatalogue.AdultAge;
            if (c.Owner[slot] != id)
            {
                if (c.Owner[slot] != -id)
                {
                    ClearFollows(c, slot);   // somebody new in this slot
                }
                if (adult)
                {
                    c.Owner[slot] = id;
                    FirstFollows(c, s, slot, shops, businessAccount, inputs, 0, influencerChance, voices);
                }
                else
                {
                    c.Owner[slot] = -id;
                }
            }
            if (!adult)
            {
                return;
            }
            int at = slot * FlareCityCatalogue.Slots;
            for (int i = 0; i < FlareCityCatalogue.Slots; i++)
            {
                ushort f = c.Follows[at + i];
                if (f == FlareCityCatalogue.None)
                {
                    continue;
                }
                if (f >= counts.Length)
                {
                    c.Follows[at + i] = FlareCityCatalogue.None;   // an account the registry no longer has (a damaged line)
                    continue;
                }
                if (f < accountBiz.Length && accountBiz[f] >= 0 && Mille(c.Seed, id, SaltUnfollow, day * FlareCityCatalogue.Slots + i) < FlareCityCatalogue.BusinessUnfollow)
                {
                    c.Follows[at + i] = FlareCityCatalogue.None;
                    continue;
                }
                counts[f]++;
            }
        }

        /// <summary>The followers index from the counts (FollowerStart's prefix sums), ready to be filled.</summary>
        public static void BeginFollowerIndex(FlareCityState c, int[] counts, int accounts)
        {
            if (c.FollowerStart.Length != accounts + 1)
            {
                c.FollowerStart = new int[accounts + 1];
            }
            c.FollowerStart[0] = 0;
            for (int a = 0; a < accounts; a++)
            {
                c.FollowerStart[a + 1] = c.FollowerStart[a] + counts[a];
            }
            int n = c.FollowerStart[accounts];
            if (c.FollowerList.Length < n)
            {
                c.FollowerList = new int[n + n / 8 + 16];
            }
        }

        /// <summary>One person's follows into the followers index (<paramref name="at"/>: each account's next place, from FollowerStart).</summary>
        public static void FillFollowers(FlareCityState c, PopulationState s, int slot, int[] at)
        {
            if (s.People.Id[slot] == 0 || c.Owner[slot] != s.People.Id[slot])
            {
                return;
            }
            int from = slot * FlareCityCatalogue.Slots;
            for (int i = 0; i < FlareCityCatalogue.Slots; i++)
            {
                ushort f = c.Follows[from + i];
                if (f != FlareCityCatalogue.None && f < at.Length && at[f] < c.FollowerStart[f + 1])
                {
                    c.FollowerList[at[f]++] = slot;
                }
            }
        }

        /// <summary>
        /// The adults of each district, the people of each employer and the households of each district and class, as indexes (the day's: who shares
        /// a post with whom, who comes across one, who shopped where). One pass over the people and one over the households.
        /// </summary>
        public static void BuildPeopleIndexes(FlareCityState c, PopulationState s, int day)
        {
            int districts = Math.Max(1, s.Districts.Count);
            int employers = s.Employers.Count;
            PersonTable pt = s.People;
            c.AdultStart = Sized(c.AdultStart, districts + 1);
            c.WorkStart = Sized(c.WorkStart, employers + 1);
            if (c.Adults.Length != districts)
            {
                c.Adults = new int[districts];
            }
            Array.Clear(c.Adults, 0, districts);
            for (int p = 0; p < pt.Count; p++)
            {
                if (pt.Id[p] == 0 || Age(s, p, day) < FlareCityCatalogue.AdultAge)
                {
                    continue;
                }
                int d = DistrictOf(s, p);
                if (d >= 0 && d < districts)
                {
                    c.AdultStart[d + 1]++;
                    c.Adults[d]++;
                }
                int e = pt.Job[p];
                if (e >= 0 && e < employers)
                {
                    c.WorkStart[e + 1]++;
                }
            }
            for (int d = 0; d < districts; d++)
            {
                c.AdultStart[d + 1] += c.AdultStart[d];
            }
            for (int e = 0; e < employers; e++)
            {
                c.WorkStart[e + 1] += c.WorkStart[e];
            }
            c.AdultList = Fit(c.AdultList, c.AdultStart[districts]);
            c.WorkList = Fit(c.WorkList, c.WorkStart[employers]);
            var da = new int[districts];
            Array.Copy(c.AdultStart, da, districts);
            var ea = new int[employers];
            Array.Copy(c.WorkStart, ea, employers);
            for (int p = 0; p < pt.Count; p++)
            {
                if (pt.Id[p] == 0 || Age(s, p, day) < FlareCityCatalogue.AdultAge)
                {
                    continue;
                }
                int d = DistrictOf(s, p);
                if (d >= 0 && d < districts)
                {
                    c.AdultList[da[d]++] = p;
                }
                int e = pt.Job[p];
                if (e >= 0 && e < employers)
                {
                    c.WorkList[ea[e]++] = p;
                }
            }
            BuildHomes(c, s, districts);
            c.Indexed = day;
        }

        // The households of each district and class (for a customer drawn from a pool).
        private static void BuildHomes(FlareCityState c, PopulationState s, int districts)
        {
            HouseholdTable hh = s.Households;
            int cells = districts * PopulationCatalogue.Classes;
            c.HomeStart = Sized(c.HomeStart, cells + 1);
            for (int h = 0; h < hh.Count; h++)
            {
                if (hh.Alive(h) && hh.District[h] < districts)
                {
                    c.HomeStart[hh.District[h] * PopulationCatalogue.Classes + Math.Min((int)hh.Class[h], 2) + 1]++;
                }
            }
            for (int i = 0; i < cells; i++)
            {
                c.HomeStart[i + 1] += c.HomeStart[i];
            }
            c.HomeList = Fit(c.HomeList, c.HomeStart[cells]);
            var at = new int[cells];
            Array.Copy(c.HomeStart, at, cells);
            for (int h = 0; h < hh.Count; h++)
            {
                if (hh.Alive(h) && hh.District[h] < districts)
                {
                    c.HomeList[at[hh.District[h] * PopulationCatalogue.Classes + Math.Min((int)hh.Class[h], 2)]++] = h;
                }
            }
        }

        private static int[] Sized(int[] a, int n)
        {
            if (a.Length != n)
            {
                return new int[n];
            }
            Array.Clear(a, 0, n);
            return a;
        }

        private static int[] Fit(int[] a, int n) => a.Length >= n ? a : new int[n + n / 8 + 16];

        // ---------------------------------------------------------------- the player's followers in the city, kept with the account's own

        /// <summary>
        /// The player's followers in the city brought to the account's own count (its posts, silence, a bad post, an arrest move that): over it, the
        /// residents with the lowest roll of the day stop following; under it, residents of the player's districts first take it up. The followers
        /// index must be the day's. How many changed, signed.
        /// </summary>
        public static int Reconcile(FlareCityState c, PopulationState s, int day, CityInputs inputs)
        {
            int adults = 0;
            for (int d = 0; d < c.Adults.Length; d++)
            {
                adults += c.Adults[d];
            }
            int target = PlayerCityTarget(inputs.PlayerFollowers, inputs.PlayerTier, adults);
            int now = c.CityFollowers;
            int seed = c.Seed;
            if (now > target)
            {
                int drop = now - target, dropped = 0;
                int n = c.FollowersOf(FlareCityState.PlayerAccount, out int start);
                int cut = (int)((long)drop * 1000 / Math.Max(1, n)) + 1;
                for (int pass = 0; pass < 6 && dropped < drop; pass++)
                {
                    for (int i = 0; i < n && dropped < drop; i++)
                    {
                        int p = c.FollowerList[start + i];
                        if (p >= 0 && Mille(seed, s.People.Id[p], SaltDrop, day * 8 + pass) < cut && Unfollow(c, p, FlareCityState.PlayerAccount))
                        {
                            c.FollowerList[start + i] = -1;   // the index keeps its shape; a hole is passed over
                            dropped++;
                        }
                    }
                    cut = Math.Min(1000, cut * 2);
                }
                c.CityFollowers -= dropped;
                return -dropped;
            }
            if (now < target && c.AdultList.Length > 0)
            {
                int add = target - now, added = 0;
                int districts = Math.Min(c.Adults.Length, c.AdultStart.Length - 1);
                for (int round = 0; round < 2 && added < add; round++)
                {
                    for (int d = 0; d < districts && added < add; d++)
                    {
                        bool home = d < inputs.PlayerDistricts.Length && inputs.PlayerDistricts[d];
                        if (home != (round == 0))
                        {
                            continue;   // the player's own districts first, then the rest of the city
                        }
                        int from = c.AdultStart[d], n = c.AdultStart[d + 1] - from;
                        if (n <= 0)
                        {
                            continue;
                        }
                        int step = Coprime(1 + Millionth(seed, day, SaltReconcile, d) % n, n);
                        int first = Millionth(seed, day, SaltReconcile + 1, d) % n;
                        for (int k = 0; k < n && added < add; k++)
                        {
                            int p = c.AdultList[from + (int)(((long)first + (long)k * step) % n)];
                            if (Follow(c, p, FlareCityState.PlayerAccount))
                            {
                                added++;
                            }
                        }
                    }
                }
                c.CityFollowers += added;
                return added;
            }
            return 0;
        }

        // A step that walks every place of a ring of n once.
        private static int Coprime(int step, int n)
        {
            if (n <= 1)
            {
                return 1;
            }
            int s = Math.Max(1, step % n);
            while (Gcd(s, n) != 1)
            {
                s = s % n + 1;
            }
            return s;
        }

        private static int Gcd(int a, int b)
        {
            while (b != 0)
            {
                int t = a % b;
                a = b;
                b = t;
            }
            return a;
        }
    }
}
