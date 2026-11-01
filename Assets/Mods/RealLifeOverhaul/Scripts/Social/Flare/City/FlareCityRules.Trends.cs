using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What a trend is about: a kind of goods in the whole city, a kind of going out in one district, or a game genre.</summary>
    public enum TrendKind : byte
    {
        Goods = 0,
        District = 1,
        Genre = 2
    }

    /// <summary>
    /// A trend: what went viral moving residents' tastes for a few weeks. The households it gave the taste (for a genre, the people who took to
    /// it) by slot and id, so they are found again after a load wherever the population's slots are, and let go again as it fades. Game-free; saved.
    /// </summary>
    public sealed class FlareTrend
    {
        public int Id;
        public TrendKind Kind;
        public int What;             // the kind of spending (Population.Spend) for goods and a district's going out, the genre (GameRules) for a genre
        public int District = -1;    // a district's going out: the population's district; -1 the whole city
        public int Day;              // the day it began
        public bool Mine;            // a post of the player's started it
        public int Let;              // of those it moved, how many it has looked at again as it fades (in the order moved)
        public readonly List<int> Slots = new List<int>();   // the households' (a genre: the people's) slots it moved, -1 to find again by id
        public readonly List<int> Ids = new List<int>();     // ...and their ids

        public int Moved => Ids.Count;
    }

    /// <summary>A trend as a screen and a guest see it: what, where, since when and how many it moved.</summary>
    public struct FlareTrendSeen
    {
        public TrendKind Kind;
        public int What;
        public string District;      // the district's id in the game, or null for the whole city
        public int Day;
        public int Moved;
    }

    /// <summary>
    /// Flare in the city by rule, trends (the brief's fifth step: "residents' tastes move with what is viral: demand for a kind of goods (a spending
    /// kind's share), a game genre (the population's genre bits, through the studio's audience), a district's night life. Spotting one early pays"):
    /// a viral post about a business, or now and then the city's own talk, starts a trend. While it rises it gives more of the city's households the
    /// taste for its kind of goods (the population then spends more of their money on it: SpendRules.Shares tilts a liked kind, the total kept), or
    /// more of a district's households the taste for going out there while each district's adults hear of its places (their attention draws people
    /// there from the same pools elsewhere), or more of the residents who play games a liking for its genre (the population's genre bits, which the
    /// game studio's audience counts). At its height for a while, then it fades, letting go all but the few who keep the taste. A post about what is
    /// trending interests more residents. At most three at once. Game-free. COST: once a day, a walk over the city's households (or its adults, for
    /// a genre) for each rising trend and over the trends' own residents as one fades.
    /// </summary>
    public static partial class FlareCityRules
    {
        private const int SaltTrend = 101, SaltTrendKind = 102, SaltTrendWhat = 103, SaltTrendDistrict = 104, SaltTrendTake = 105, SaltTrendStick = 106;

        public const int StageRising = 0, StageHeight = 1, StageFading = 2, StageOver = 3;

        /// <summary>Kinds of goods that may trend in the whole city: each a taste of the households' (GenerateRules.TasteKinds).</summary>
        public static readonly Spend[] TrendGoods =
        {
            Spend.Clothing, Spend.Electronics, Spend.Furniture, Spend.BooksGifts, Spend.Flowers, Spend.Jewellery, Spend.Gym, Spend.PersonalCare, Spend.Coffee, Spend.EatingOut
        };

        /// <summary>Kinds of going out that may trend in a district: a viral post about such a place makes its district the place to go.</summary>
        public static readonly Spend[] TrendGoingOut = { Spend.NightLife, Spend.EatingOut, Spend.Coffee, Spend.Theatre, Spend.Cinema, Spend.Casino };

        /// <summary>No kind trending in any district: the day's lookup before any trend.</summary>
        public static int[] NoDistricts()
        {
            var d = new int[Kinds];
            for (int k = 0; k < d.Length; k++)
            {
                d[k] = -1;
            }
            return d;
        }

        /// <summary>The households' taste bit for a kind of spending (GenerateRules.TasteKinds), -1 for a kind nobody has a taste for.</summary>
        public static int TasteBit(int kind)
        {
            for (int bit = 0; bit < GenerateRules.TasteBits; bit++)
            {
                if ((int)GenerateRules.TasteKinds[bit] == kind)
                {
                    return bit;
                }
            }
            return -1;
        }

        /// <summary>Where a trend stands on a day: rising, at its height, fading, or over.</summary>
        public static int Stage(FlareTrend t, int day)
        {
            int age = day - t.Day;
            return age < FlareCityCatalogue.TrendRiseDays ? StageRising
                : age < FlareCityCatalogue.TrendDays ? StageHeight
                : age < FlareCityCatalogue.TrendDays + FlareCityCatalogue.TrendFadeDays ? StageFading : StageOver;
        }

        /// <summary>Days until a trend has faded.</summary>
        public static int DaysLeft(int start, int day) => Math.Max(0, start + FlareCityCatalogue.TrendDays + FlareCityCatalogue.TrendFadeDays - day);

        /// <summary>
        /// A trend begun, if it may: none past <see cref="FlareCityCatalogue.TrendsMost"/>, none for what trends already (the same kind in the city,
        /// the same district's going out, the same genre), none for a kind nobody has a taste for. Null when not.
        /// </summary>
        public static FlareTrend StartTrend(FlareCityState c, TrendKind kind, int what, int district, int day, bool mine)
        {
            if (c.Trends.Count >= FlareCityCatalogue.TrendsMost || (kind == TrendKind.District && district < 0))
            {
                return null;
            }
            if (kind == TrendKind.Genre ? what < 0 || what >= GameRules.Genres : TasteBit(what) < 0)
            {
                return null;
            }
            for (int i = 0; i < c.Trends.Count; i++)
            {
                FlareTrend t = c.Trends[i];
                if (t.Kind == kind && t.What == what && (kind != TrendKind.District || t.District == district))
                {
                    return null;
                }
            }
            var trend = new FlareTrend { Id = c.NextTrend++, Kind = kind, What = what, District = kind == TrendKind.District ? district : -1, Day = day, Mine = mine };
            c.Trends.Add(trend);
            TrendLookups(c);
            return trend;
        }

        /// <summary>The trend a viral post about a business starts: its district's going out for a place to go out, its kind in the whole city for goods; null for neither.</summary>
        public static FlareTrend TrendOf(FlareCityState c, PopulationState s, int b, int day, bool mine)
        {
            if (b < 0 || b >= s.Businesses.Count || !Sells(s.Businesses, b))
            {
                return null;
            }
            int kind = s.Businesses.Kind[b];
            if (In(TrendGoingOut, kind))
            {
                return StartTrend(c, TrendKind.District, kind, s.Businesses.District[b], day, mine);
            }
            return In(TrendGoods, kind) ? StartTrend(c, TrendKind.Goods, kind, -1, day, mine) : null;
        }

        /// <summary>
        /// The city's own talk starting a trend today, at <see cref="FlareCityCatalogue.TrendChance"/>: a kind of goods in the whole city (half the
        /// time), a district's going out (a quarter, the district by its adults), a game genre (a quarter). Null for none.
        /// </summary>
        public static FlareTrend CityTrend(FlareCityState c, PopulationState s, int day)
        {
            if (Mille(c.Seed, day, SaltTrend, 0) >= FlareCityCatalogue.TrendChance)
            {
                return null;
            }
            int roll = Mille(c.Seed, day, SaltTrendKind, 0), pick = Mille(c.Seed, day, SaltTrendWhat, 0);
            if (roll < 500)
            {
                return StartTrend(c, TrendKind.Goods, (int)TrendGoods[pick * TrendGoods.Length / 1000], -1, day, false);
            }
            if (roll < 750)
            {
                int d = DistrictByAdults(c, Millionth(c.Seed, day, SaltTrendDistrict, 0));
                return d >= 0 ? StartTrend(c, TrendKind.District, (int)TrendGoingOut[pick * TrendGoingOut.Length / 1000], d, day, false) : null;
            }
            return StartTrend(c, TrendKind.Genre, pick * GameRules.Genres / 1000, -1, day, false);
        }

        /// <summary>
        /// The trends' day: each rising one takes more residents (a household's taste for its kind, or a player's liking for its genre), each
        /// district's adults hear of a trending district's places of its kind, and a fading one lets a fifth of those it moved go (all but the few
        /// who keep it); one over is done. Households and residents taken or let go today.
        /// </summary>
        public static int TrendsDay(FlareCityState c, PopulationState s, int day, int[] personSlot)
        {
            int moved = 0;
            Dictionary<int, int> households = null;
            for (int i = c.Trends.Count - 1; i >= 0; i--)
            {
                FlareTrend t = c.Trends[i];
                int stage = Stage(t, day);
                if (stage == StageRising)
                {
                    moved += Take(c, s, t, day);
                }
                else if (stage != StageHeight)
                {
                    moved += LetGo(c, s, t, day, personSlot, ref households);
                }
                if (stage <= StageHeight && t.Kind == TrendKind.District)
                {
                    Hum(c, s, t, day);
                }
                if (stage == StageOver)
                {
                    c.Trends.RemoveAt(i);
                }
            }
            TrendLookups(c);
            return moved;
        }

        /// <summary>The day's lookups of what trends where, for <see cref="Trending"/>: after a trend begins or ends, and after a load.</summary>
        public static void TrendLookups(FlareCityState c)
        {
            c.TrendCity = 0;
            if (c.TrendDistrict == null || c.TrendDistrict.Length != Kinds)
            {
                c.TrendDistrict = NoDistricts();
            }
            for (int k = 0; k < c.TrendDistrict.Length; k++)
            {
                c.TrendDistrict[k] = -1;
            }
            for (int i = 0; i < c.Trends.Count; i++)
            {
                FlareTrend t = c.Trends[i];
                if (t.What < 0 || t.What >= Kinds)
                {
                    continue;
                }
                if (t.Kind == TrendKind.Goods)
                {
                    c.TrendCity |= 1u << t.What;
                }
                else if (t.Kind == TrendKind.District)
                {
                    c.TrendDistrict[t.What] = t.District;
                }
            }
        }

        /// <summary>Whether a post is about what is trending: its kind in the whole city, or its kind in its business's district.</summary>
        public static bool Trending(FlareCityState c, PopulationState s, in CityPost post)
        {
            int k = post.Kind;
            if (c.Trends.Count == 0 || k < 0 || k >= Kinds)
            {
                return false;
            }
            if ((c.TrendCity & (1u << k)) != 0)
            {
                return true;
            }
            int d = c.TrendDistrict[k];
            return d >= 0 && post.Business >= 0 && post.Business < s.Businesses.Count && s.Businesses.District[post.Business] == d;
        }

        /// <summary>The trends as a screen and a guest see them, newest first.</summary>
        public static List<FlareTrendSeen> Seen(FlareCityState c, PopulationState s)
        {
            var seen = new List<FlareTrendSeen>(c.Trends.Count);
            for (int i = c.Trends.Count - 1; i >= 0; i--)
            {
                FlareTrend t = c.Trends[i];
                string district = t.District >= 0 && s != null && t.District < s.Districts.Count ? s.Districts[t.District].Id : null;
                seen.Add(new FlareTrendSeen { Kind = t.Kind, What = t.What, District = district, Day = t.Day, Moved = t.Moved });
            }
            return seen;
        }

        // A rising trend's day: everyone among its own (the city's households, a district's, or the adults who play games) whose roll falls under the
        // share its day has reached, and who has not the taste yet, takes to it.
        private static int Take(FlareCityState c, PopulationState s, FlareTrend t, int day)
        {
            int reach = t.Kind == TrendKind.Goods ? FlareCityCatalogue.TrendReach : t.Kind == TrendKind.District ? FlareCityCatalogue.TrendDistrictReach : FlareCityCatalogue.TrendGenreReach;
            int under = reach * Math.Min(FlareCityCatalogue.TrendRiseDays, day - t.Day + 1) / FlareCityCatalogue.TrendRiseDays;
            int taken = 0;
            if (t.Kind == TrendKind.Genre)
            {
                int bit = 1 << t.What;
                int to = c.AdultStart.Length > 0 ? c.AdultStart[c.AdultStart.Length - 1] : 0;
                for (int i = 0; i < to && i < c.AdultList.Length && t.Moved < FlareCityCatalogue.TrendMovedMost; i++)
                {
                    int p = c.AdultList[i];
                    ushort games = s.People.Games[p];
                    if (GameRules.PlatformsOf(games) == 0 || (games & bit) != 0 || Mille(c.Seed, s.People.Id[p], SaltTrendTake, t.Id) >= under)
                    {
                        continue;
                    }
                    s.People.Games[p] = (ushort)(games | bit);
                    t.Slots.Add(p);
                    t.Ids.Add(s.People.Id[p]);
                    taken++;
                }
                return taken;
            }
            uint taste = 1u << TasteBit(t.What);
            HouseholdTable hh = s.Households;
            int cells = Math.Max(1, c.Districts) * PopulationCatalogue.Classes;
            int from = 0, end = c.HomeStart.Length > cells ? c.HomeStart[cells] : 0;
            if (t.Kind == TrendKind.District)
            {
                int first = t.District * PopulationCatalogue.Classes;
                from = first < c.HomeStart.Length ? c.HomeStart[first] : 0;
                end = first + PopulationCatalogue.Classes < c.HomeStart.Length ? c.HomeStart[first + PopulationCatalogue.Classes] : from;
            }
            for (int i = from; i < end && i < c.HomeList.Length && t.Moved < FlareCityCatalogue.TrendMovedMost; i++)
            {
                int h = c.HomeList[i];
                if (!hh.Alive(h) || (hh.Tastes[h] & taste) != 0 || Mille(c.Seed, hh.Id[h], SaltTrendTake, t.Id) >= under)
                {
                    continue;
                }
                hh.Tastes[h] |= taste;
                t.Slots.Add(h);
                t.Ids.Add(hh.Id[h]);
                taken++;
            }
            return taken;
        }

        // A fading trend's day: the next fifth of those it moved let go, in the order moved, but for the few who keep the taste; over, all of them.
        private static int LetGo(FlareCityState c, PopulationState s, FlareTrend t, int day, int[] personSlot, ref Dictionary<int, int> households)
        {
            int age = day - t.Day - FlareCityCatalogue.TrendDays;
            int upto = (int)((long)t.Moved * Math.Min(FlareCityCatalogue.TrendFadeDays, age + 1) / FlareCityCatalogue.TrendFadeDays);
            int let = 0;
            for (; t.Let < upto; t.Let++)
            {
                int id = t.Ids[t.Let];
                if (Mille(c.Seed, id, SaltTrendStick, t.Id) < FlareCityCatalogue.TrendStick)
                {
                    continue;   // tastes move: this one keeps it
                }
                if (t.Kind == TrendKind.Genre)
                {
                    int p = Slot(personSlot, s, id);
                    int bit = 1 << t.What;
                    if (p >= 0 && (s.People.Games[p] & bit) != 0)
                    {
                        s.People.Games[p] = (ushort)(s.People.Games[p] & ~bit);
                        let++;
                    }
                    continue;
                }
                int h = t.Slots[t.Let];
                if (!s.Households.Alive(h) || s.Households.Id[h] != id)
                {
                    households = households ?? HouseholdSlots(s);   // the slots changed since (a load): found again by id, once
                    h = households.TryGetValue(id, out int found) ? found : -1;
                }
                uint taste = 1u << TasteBit(t.What);
                if (h >= 0 && (s.Households.Tastes[h] & taste) != 0)
                {
                    s.Households.Tastes[h] &= ~taste;
                    let++;
                }
            }
            return let;
        }

        // A trending district's places of its kind heard of by each district's adults, as the share of them who had not heard yet: their attention
        // then draws people there from the same pools elsewhere (the attention source), so the district is the place to go.
        private static void Hum(FlareCityState c, PopulationState s, FlareTrend t, int day)
        {
            int share = FlareCityCatalogue.TrendHum * Math.Min(FlareCityCatalogue.TrendRiseDays, day - t.Day + 1) / FlareCityCatalogue.TrendRiseDays;
            int n = c.Districts;
            BusinessTable bt = s.Businesses;
            for (int b = 0; b < c.BusinessCount && b < bt.Count; b++)
            {
                if (bt.Kind[b] != t.What || bt.District[b] != t.District || !Sells(bt, b))
                {
                    continue;
                }
                for (int o = 0; o < n && o < c.Adults.Length; o++)
                {
                    int at = b * n + o;
                    long most = (long)c.Adults[o] * 1000;
                    c.Heard[at] = (int)Math.Max(0, Math.Min(int.MaxValue, c.Heard[at] + (most - c.Heard[at]) * share / 1000));
                }
            }
        }

        // A district drawn by its adults (a millionth roll), -1 with none.
        private static int DistrictByAdults(FlareCityState c, int millionth)
        {
            long total = 0;
            for (int d = 0; d < c.Adults.Length; d++)
            {
                total += c.Adults[d];
            }
            if (total <= 0)
            {
                return -1;
            }
            long x = millionth * total / 1000000;
            for (int d = 0; d < c.Adults.Length; d++)
            {
                x -= c.Adults[d];
                if (x < 0)
                {
                    return d;
                }
            }
            return c.Adults.Length - 1;
        }

        private static Dictionary<int, int> HouseholdSlots(PopulationState s)
        {
            var map = new Dictionary<int, int>(s.Households.Live + 16);
            for (int h = 0; h < s.Households.Count; h++)
            {
                if (s.Households.Id[h] != 0)
                {
                    map[s.Households.Id[h]] = h;
                }
            }
            return map;
        }

        private static bool In(Spend[] kinds, int kind)
        {
            for (int i = 0; i < kinds.Length; i++)
            {
                if ((int)kinds[i] == kind)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
