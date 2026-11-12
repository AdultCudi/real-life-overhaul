using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>Why a review said what it said. Saved as a number: new reasons go at the end.</summary>
    public enum FlareReviewWhy
    {
        Mixed = 0,
        Price = 1,
        Quality = 2,
        Wait = 3,
        Clean = 4
    }

    /// <summary>
    /// What a post, a promotion or a hire would do, worked out and then thrown away. Compose, a hire and a rival's answer
    /// read it before anything is saved. The shares are the city's own (FlareCityCatalogue.SeenShare, InterestMatch,
    /// PositiveLift): the same hearing and the same attention, with no resident written and no line saved.
    /// </summary>
    public struct FlareExpect
    {
        public int Reach;
        public int Visitors;
        public int Cents;
        public int Hours;
        public int Cost;
        public int Gain;
        public int Percent;
        public int Flop;
        public int Viral;
        public bool Flopped;
        public bool WentViral;
        public bool Peak;
        public int HourFit;
        public int PhotoFit;
        public int DistrictFit;
    }

    /// <summary>What the dry run is asked. A guest's business is refused here, so another player's shop is never priced.</summary>
    public struct FlareBrief
    {
        public int Followers;
        public int Hour;
        public bool Photo;
        public bool PhotoHere;     // the picture was taken at the business. Life's photo sidecar is the join; this is the place Flare kept until that type is on the branch.
        public int SameDistrict;   // 1 the business's district, 0 another, -1 not known
        public int DayCustomers;   // a typical day at the business; 0 stands for a modest shop
        public int BasketCents;
        public int Districts;      // districts a promotion buys, at least 1
        public int PriceLevel;     // the game's prices, thousandths
        public int ReachPaid;      // residents a day a promotion buys, 0 for a free post
        public int AdDays;
        public int Reliability;    // 0 to 100 for a hire, -1 when nobody is hired
        public int Style;          // FlareStyle
        public int Seed;
        public int RollKey;
        public bool Guest;         // another player's business: the whole result is nothing
    }

    /// <summary>One post's extra customers, for the hours it runs. Saved on the account. A rival's is not the player's footfall.</summary>
    public sealed class FlareBoost
    {
        public int Post;
        public string Place;
        public int FromMinute;
        public int Hours;
        public int Visitors;
        public int Cents;
        public int GivenVisitors;
        public int GivenCents;
        public int Percent;
        public int Hour;
        public int Flags;
        public string PicPlace;

        public bool Photo => (Flags & 1) != 0;
        public bool PhotoHere => (Flags & 2) != 0;
        public bool Flopped => (Flags & 4) != 0;
        public bool WentViral => (Flags & 8) != 0;
        public bool Rival => (Flags & 16) != 0;
        public bool Paid => (Flags & PaidFlag) != 0;   // the post bought reach: its card says Promoted

        public const int PaidFlag = 32;

        public bool On(int minute, int hoursInDay)
        {
            if (Hours <= 0 || string.IsNullOrEmpty(Place))
            {
                return false;
            }
            int span = Hours * Math.Max(1, hoursInDay);
            return minute >= FromMinute && minute < FromMinute + span;
        }
    }

    /// <summary>A review kept with the reason it was written, past the six the ring shows. Saved with the city.</summary>
    public sealed class FlareReviewNote
    {
        public string Key;
        public int Person;
        public int Stars;
        public int Day;
        public int Why;
        public string Answer;
        public int Quality;
    }

    /// <summary>Stars ever given to one business, 1 to 5. Saved with the city, by the business's key.</summary>
    public sealed class FlareStarRow
    {
        public string Key;
        public int One, Two, Three, Four, Five;
        public int Recent;    // thousandths of a star, the newest stretch
        public int Earlier;   // thousandths of a star, the stretch before that
        public int Eased, EasedStars;     // poor reviews a good answer eased, and their stars (FlareReplyRules): saved after the rows
        public int Raised, RaisedStars;   // reviews a rude answer drew eyes to, and their stars

        public int Total => One + Two + Three + Four + Five;

        public int Count(int stars)
        {
            switch (stars)
            {
                case 1: return One;
                case 2: return Two;
                case 3: return Three;
                case 4: return Four;
                case 5: return Five;
                default: return 0;
            }
        }
    }

    /// <summary>A promotion's districts, when it buys more than the one FlareAd.District keeps. Saved with the city.</summary>
    public sealed class FlareAdMask
    {
        public int Id;
        public int Mask;   // a bit a district
    }

    /// <summary>What a paid post must be. Saved with the city, on the offer's id.</summary>
    public sealed class FlareOfferNeed
    {
        public int Id;
        public int Hour = -1;
        public bool Photo;
        public string Tags;
        public int MinFollowers;
    }

    /// <summary>An influencer booked by a business, so the slot is taken. Saved with the cast.</summary>
    public sealed class FlarePastHire
    {
        public int Who;
        public string Place;
        public string Name;
        public int Day;
        public int Visitors;
        public int Cents;
        public bool Flopped;
    }

    /// <summary>
    /// What makes a post, a review, a following or the city's talk worth money. Game-free. The dry run saves nothing.
    /// Reach by day is kept on the account (FlareFile.ReachDays) until Money's day series can be read; a picture's place
    /// is kept on the boost until Life's photo sidecar can be read. Cost: a few dozen sums a call, never a frame.
    /// </summary>
    public static class FlareUseRules
    {
        public const int VisitShare = 80;          // per mille of the people who see a post and then come, before the day's cap
        public const int BasketDefault = 2500;     // cents a visit brings when the business has no basket of its own
        public const int DayDefault = 160;         // customers on a modest day, when the business has not been counted
        public const int HoursBase = 4;            // hours a post keeps bringing people
        public const int HoursPeak = 2;            // more when the district is busy
        public const int HoursMost = 10;
        public const int FloorPeak = 8;            // visitors a pictured post still brings at the quietest account, so a new account is not a zero
        public const int FloorQuiet = 4;
        public const int GoodAnswer = 700;         // thousandths: a reply this careful softens a poor review
        public const int AnswerWords = 40;         // characters a careful reply takes
        public const int FlopKeep = 200;           // thousandths of the visitors a flop still brings
        public const int ViralTimes = 4;
        public const int Overlap = 850;            // thousandths kept when a promotion buys a second district (the same people are not counted twice)
        public const int ReachDays = 30;           // days of reach kept on the account. The join is Money's day series, which is not on this branch.
        public const int MinuteHour = 60;

        /// <summary>Per cent of a day's customers a post may add, by tier. It sits beside the game's own marketing, and stops here.</summary>
        public static readonly int[] PercentCap = { 8, 12, 16, 22, 28 };

        /// <summary>Thousandths a job offer pays over the post's rate, by tier.</summary>
        public static readonly int[] JobExtraOf = { 0, 0, 40, 80, 120 };

        /// <summary>Basis points added to a pay review, by tier.</summary>
        public static readonly int[] RaiseOf = { 0, 0, 0, 150, 300 };

        /// <summary>Points added to the chance an investor comes asking, by tier.</summary>
        public static readonly int[] InvestorOf = { 0, 0, 0, 4, 12 };

        /// <summary>Thousandths off a promotion or a hire, the VIP perk, by tier.</summary>
        public static readonly int[] VipOf = { 0, 0, 30, 80, 120 };

        private static readonly int[] Hours =
        {
            700, 700, 700, 700, 700, 700, 700,
            1000, 1000, 1000,
            1200, 1200,
            1300, 1300,
            1050, 1050, 1050,
            1250, 1250, 1250,
            1100, 1100,
            800, 800
        };

        public static int HourFit(int hour)
        {
            if (hour < 0)
            {
                hour = 0;
            }
            return hour > 23 ? Hours[23] : Hours[hour];
        }

        public static bool IsPeak(int hour) => HourFit(hour) >= 1200;

        public static int PhotoFit(bool photo, bool here) => !photo ? 800 : here ? 1250 : 1100;

        public static int DistrictFit(int same) => same > 0 ? 1200 : same == 0 ? 750 : 1000;

        public static int TierCap(int followers)
        {
            int t = (int)FlareRules.TierOf(followers);
            return PercentCap[t < 0 ? 0 : t >= PercentCap.Length ? PercentCap.Length - 1 : t];
        }

        public static bool Sponsorships(int followers) => (int)FlareRules.TierOf(followers) >= (int)FlareTier.Known;

        public static bool Verified(int followers, bool business) => business || (int)FlareRules.TierOf(followers) >= (int)FlareTier.Known;

        public static int JobExtra(int followers)
        {
            int t = (int)FlareRules.TierOf(followers);
            return JobExtraOf[t < 0 ? 0 : t >= JobExtraOf.Length ? JobExtraOf.Length - 1 : t];
        }

        public static int RaiseBp(int followers)
        {
            int t = (int)FlareRules.TierOf(followers);
            return RaiseOf[t < 0 ? 0 : t >= RaiseOf.Length ? RaiseOf.Length - 1 : t];
        }

        public static int InvestorChance(int followers)
        {
            int t = (int)FlareRules.TierOf(followers);
            return InvestorOf[t < 0 ? 0 : t >= InvestorOf.Length ? InvestorOf.Length - 1 : t];
        }

        public static int VipOff(int followers)
        {
            int t = (int)FlareRules.TierOf(followers);
            return VipOf[t < 0 ? 0 : t >= VipOf.Length ? VipOf.Length - 1 : t];
        }

        /// <summary>The followers the next perk waits on, or -1 at the top. The steps are FlareRules.TierFrom, never a round thousand of the mock.</summary>
        public static int NextPerkAt(int followers) => FlareRules.NextTierAt(followers);

        /// <summary>
        /// People reached, visitors, takings, hours and the cost, for a free post, a promotion or a hire. Nothing is written.
        /// A guest's business returns an empty result.
        /// </summary>
        public static FlareExpect Expect(in FlareBrief b)
        {
            var e = new FlareExpect();
            if (b.Guest)
            {
                return e;
            }
            e.HourFit = HourFit(b.Hour);
            e.PhotoFit = PhotoFit(b.Photo, b.PhotoHere);
            e.DistrictFit = DistrictFit(b.SameDistrict);
            e.Peak = e.HourFit >= 1200;
            int districts = b.Districts < 1 ? 1 : b.Districts;
            int organic = FlareRules.Reach(Math.Max(0, b.Followers), 1000, 1000);
            int reach = b.ReachPaid > 0 ? b.ReachPaid : organic;
            reach = (int)((long)reach * e.HourFit / 1000 * e.PhotoFit / 1000 * e.DistrictFit / 1000);
            if (districts > 1)
            {
                reach = (int)((long)reach * districts * Overlap / 1000);
            }
            e.Reach = Math.Max(0, reach);
            int raw = (int)((long)e.Reach * VisitShare / 1000);
            if (b.Followers > 0 && (b.Photo || b.ReachPaid > 0) && raw < (e.Peak ? FloorPeak : FloorQuiet))
            {
                raw = e.Peak ? FloorPeak : FloorQuiet;
            }
            int day = b.DayCustomers > 0 ? b.DayCustomers : DayDefault;
            int cap = TierCap(b.Followers);
            int percent = raw * 100 / day;
            if (percent > cap)
            {
                percent = cap;
            }
            if (percent < 0)
            {
                percent = 0;
            }
            e.Percent = percent;
            e.Visitors = percent * day / 100;
            int basket = b.BasketCents > 0 ? b.BasketCents : BasketDefault;
            e.Cents = e.Visitors * basket;
            int tier = (int)FlareRules.TierOf(b.Followers);
            e.Hours = HoursBase + tier + (e.Peak ? HoursPeak : 0);
            if (e.Hours > HoursMost)
            {
                e.Hours = HoursMost;
            }
            if (b.ReachPaid > 0 && b.AdDays > 0)
            {
                long cost = FlareCityRules.AdCost(b.ReachPaid, b.AdDays, b.PriceLevel < 1 ? 1000 : b.PriceLevel) * districts;
                int vip = VipOff(b.Followers);
                e.Cost = (int)(cost * (1000 - vip) / 1000);
            }
            e.Gain = e.Cents - e.Cost;
            if (b.Reliability >= 0)
            {
                int style = b.Style;
                e.Flop = (100 - FlareRules.Clamp(b.Reliability, 0, 100)) * 6;
                e.Flop += style == (int)FlareStyle.Flaky ? 120 : style == (int)FlareStyle.Edgy ? 60 : 0;
                if (e.Flop > 450)
                {
                    e.Flop = 450;
                }
                int viral = FlareRules.ViralChance(b.Followers, 1000, 1000);
                int styleFit = style == (int)FlareStyle.Edgy ? 1500 : style == (int)FlareStyle.Wholesome ? 800 : 1000;
                e.Viral = viral * styleFit / 1000;
                if (e.Viral > FlareRules.ViralMost)
                {
                    e.Viral = FlareRules.ViralMost;
                }
                int roll = FlareRules.Roll(b.Seed, b.RollKey, 17);
                if (roll < e.Flop)
                {
                    e.Flopped = true;
                    e.Visitors = e.Visitors * FlopKeep / 1000;
                    e.Cents = e.Visitors * basket;
                    e.Gain = e.Cents - e.Cost;
                    e.Percent = day > 0 ? e.Visitors * 100 / day : 0;
                }
                else if (roll < e.Flop + e.Viral)
                {
                    e.WentViral = true;
                    e.Visitors *= ViralTimes;
                    e.Cents = e.Visitors * basket;
                    e.Gain = e.Cents - e.Cost;
                    e.Reach *= ViralTimes;
                    if (e.Hours + 2 <= HoursMost)
                    {
                        e.Hours += 2;
                    }
                }
            }
            return e;
        }

        /// <summary>The per cent of customers a place is owed this hour from the player's own posts. Rivals are not in this list.</summary>
        public static int Marketing(List<FlareBoost> boosts, string place, int minute)
        {
            if (boosts == null || string.IsNullOrEmpty(place))
            {
                return 0;
            }
            int sum = 0;
            int cap = 0;
            for (int i = 0; i < boosts.Count; i++)
            {
                FlareBoost b = boosts[i];
                if (b.Rival || b.Place != place || !b.On(minute, MinuteHour))
                {
                    continue;
                }
                sum += b.Percent;
                if (b.Percent > cap)
                {
                    cap = b.Percent;
                }
            }
            int most = cap > 0 ? cap : PercentCap[PercentCap.Length - 1];
            return sum < 0 ? 0 : sum > most ? most : sum;
        }

        /// <summary>One hour of a boost credited, once. The visitors and cents still to add this hour.</summary>
        public static void Credit(FlareBoost b, int minute, out int visitors, out int cents)
        {
            visitors = 0;
            cents = 0;
            if (b == null || !b.On(minute, MinuteHour) || b.Hours <= 0)
            {
                return;
            }
            int dueV = b.Visitors * ((minute - b.FromMinute) / MinuteHour + 1) / b.Hours;
            int dueC = b.Cents * ((minute - b.FromMinute) / MinuteHour + 1) / b.Hours;
            if (dueV > b.Visitors)
            {
                dueV = b.Visitors;
            }
            if (dueC > b.Cents)
            {
                dueC = b.Cents;
            }
            visitors = dueV - b.GivenVisitors;
            cents = dueC - b.GivenCents;
            if (visitors < 0)
            {
                visitors = 0;
            }
            if (cents < 0)
            {
                cents = 0;
            }
            b.GivenVisitors += visitors;
            b.GivenCents += cents;
        }

        public static int WaitScore(int visits, int room)
        {
            if (room <= 0)
            {
                return 70;
            }
            int full = visits * 100 / room;
            int score = 100 - full;
            return score < 0 ? 0 : score > 100 ? 100 : score;
        }

        public static int WaitMinutes(int waitScore)
        {
            int score = waitScore < 0 ? 0 : waitScore > 100 ? 100 : waitScore;
            return (100 - score) * 30 / 100;
        }

        /// <summary>The weakest of price, quality, wait and cleanliness, each 0 to 100. The stars stay the city's own.</summary>
        public static FlareReviewWhy Reason(int price, int quality, int wait, int clean)
        {
            int p = price < 0 ? 0 : price > 100 ? 100 : price;
            int q = quality < 0 ? 0 : quality > 100 ? 100 : quality;
            int w = wait < 0 ? 0 : wait > 100 ? 100 : wait;
            int c = clean < 0 ? 0 : clean > 100 ? 100 : clean;
            int low = p;
            var why = FlareReviewWhy.Price;
            if (q < low)
            {
                low = q;
                why = FlareReviewWhy.Quality;
            }
            if (w < low)
            {
                low = w;
                why = FlareReviewWhy.Wait;
            }
            if (c < low)
            {
                low = c;
                why = FlareReviewWhy.Clean;
            }
            int spread = 0;
            spread += p - low;
            spread += q - low;
            spread += w - low;
            spread += c - low;
            return spread < 12 ? FlareReviewWhy.Mixed : why;
        }

        public static FlareStarRow StarsOf(List<FlareStarRow> rows, string key)
        {
            if (rows == null || string.IsNullOrEmpty(key))
            {
                return null;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Key == key)
                {
                    return rows[i];
                }
            }
            return null;
        }

        /// <summary>A review counted into the stars and the turn. True when the newest stretch has fallen a star from the one before.</summary>
        public static bool NoteReview(List<FlareReviewNote> notes, List<FlareStarRow> rows, string key, int person, int stars, int day, int why)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            stars = stars < 1 ? 1 : stars > 5 ? 5 : stars;
            if (notes != null)
            {
                if (notes.Count >= 400)
                {
                    notes.RemoveAt(0);
                }
                notes.Add(new FlareReviewNote { Key = key, Person = person, Stars = stars, Day = day, Why = why });
            }
            if (rows == null)
            {
                return false;
            }
            FlareStarRow row = StarsOf(rows, key);
            if (row == null)
            {
                row = new FlareStarRow { Key = key };
                rows.Add(row);
            }
            switch (stars)
            {
                case 1: row.One++; break;
                case 2: row.Two++; break;
                case 3: row.Three++; break;
                case 4: row.Four++; break;
                default: row.Five++; break;
            }
            int total = row.Total;
            int sum = row.One + row.Two * 2 + row.Three * 3 + row.Four * 4 + row.Five * 5;
            int avg = total > 0 ? sum * 1000 / total : 0;
            if (row.Recent == 0)
            {
                row.Recent = avg;
            }
            else if (total % 3 == 0)
            {
                row.Earlier = row.Recent;
                row.Recent = avg;
            }
            else
            {
                row.Recent = avg;
            }
            return row.Earlier > 0 && row.Recent + 1000 <= row.Earlier;
        }

        /// <summary>Thousandths a reply is worth. A careful one is at or over <see cref="GoodAnswer"/>.</summary>
        public static int AnswerQuality(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            int n = 0;
            for (int i = 0; i < text.Length; i++)
            {
                n += text[i] > ' ' ? 1 : 0;
            }
            return n >= AnswerWords ? 800 : n >= 12 ? 500 : 200;
        }

        /// <summary>What sour is worth after a reply. A good one keeps FlareCityCatalogue.SourAnswered of it.</summary>
        public static int Soften(int sour, int quality)
        {
            if (sour <= 0 || quality < GoodAnswer)
            {
                return sour;
            }
            return (int)((long)sour * FlareCityCatalogue.SourAnswered / 1000);
        }

        public static bool TermsMet(FlareOfferNeed need, int followers, int hour, bool photo, string tags)
        {
            if (need == null)
            {
                return true;
            }
            if (need.MinFollowers > 0 && followers < need.MinFollowers)
            {
                return false;
            }
            if (need.Hour >= 0 && hour != need.Hour)
            {
                return false;
            }
            if (need.Photo && !photo)
            {
                return false;
            }
            if (!string.IsNullOrEmpty(need.Tags))
            {
                if (string.IsNullOrEmpty(tags) || tags.IndexOf(need.Tags, StringComparison.Ordinal) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>The fee a counter asks, cents, and whether the business takes it. The same roll for a rival and for the player.</summary>
        public static long CounterFee(long fee, int followers, int reputation, out bool accepted)
        {
            int tier = (int)FlareRules.TierOf(followers);
            long ask = fee * (1100 + FlareRules.Clamp(reputation, 0, 100) * 2 + tier * 40) / 1000;
            long budget = fee * (1200 + FlareRules.Clamp(reputation, 0, 100) * 4) / 1000;
            accepted = ask <= budget;
            return ask < fee ? fee : ask;
        }

        /// <summary>A poll's yes share, per mille, from a taste the residents already have. The two sides add to 1000.</summary>
        public static int PollYes(int seed, int day, int taste)
        {
            int roll = FlareRules.Roll(seed, day, 400 + taste);
            int yes = 350 + roll / 2;
            return yes < 50 ? 50 : yes > 950 ? 950 : yes;
        }

        /// <summary>
        /// The demand a trend shows, per cent, from the city's own spending share for the kind the posts are about
        /// (<see cref="KindShare"/>, thousandths). Post count does not invent a demand. Nothing below zero, and nothing past the whole.
        /// </summary>
        public static int TrendDemand(int cityThousandths)
        {
            if (cityThousandths <= 0)
            {
                return 0;
            }
            int pct = cityThousandths / 10;
            return pct > 100 ? 100 : pct;
        }

        /// <summary>The middle household's share of spending on a kind, thousandths, as the population spends it. A kind the table does not have is nothing.</summary>
        public static int KindShare(int kind)
        {
            if (kind < 0 || kind >= PopulationCatalogue.Categories)
            {
                return 0;
            }
            int[] mid = PopulationCatalogue.SpendShares[1];
            return kind < mid.Length ? mid[kind] : 0;
        }

        /// <summary>A post about the player's own business is kept as a boost, whether or not they paid for reach. Another player's business is not.</summary>
        public static bool KeepsBoost(int promoteReach, int visitors, bool guest)
        {
            return !guest && visitors > 0 && promoteReach >= 0;
        }

        /// <summary>
        /// The per cent a rival's own promotion adds at that rival's place. The player's business and another player's business get nothing,
        /// even when a place string happens to match.
        /// </summary>
        public static int RivalCustomers(List<FlareBoost> boosts, string place, int minute, bool mine, bool guest)
        {
            if (mine || guest || boosts == null || string.IsNullOrEmpty(place))
            {
                return 0;
            }
            int sum = 0;
            int cap = 0;
            for (int i = 0; i < boosts.Count; i++)
            {
                FlareBoost b = boosts[i];
                if (b == null || !b.Rival || b.Place != place || !b.On(minute, MinuteHour))
                {
                    continue;
                }
                sum += b.Percent;
                if (b.Percent > cap)
                {
                    cap = b.Percent;
                }
            }
            int most = cap > 0 ? cap : PercentCap[PercentCap.Length - 1];
            return sum < 0 ? 0 : sum > most ? most : sum;
        }

        /// <summary>True once an hour of the boost has been credited, or the clock has passed its first hour.</summary>
        public static bool HourRan(FlareBoost b, int minute)
        {
            if (b == null)
            {
                return false;
            }
            if (b.GivenVisitors > 0 || b.GivenCents > 0)
            {
                return true;
            }
            return minute >= b.FromMinute + MinuteHour;
        }

        /// <summary>Visitors and cents already credited. Before an hour has run, both are nothing: the forecast is not a result.</summary>
        public static void Credited(FlareBoost b, int minute, out int visits, out int cents)
        {
            visits = 0;
            cents = 0;
            if (!HourRan(b, minute))
            {
                return;
            }
            visits = b.GivenVisitors;
            cents = b.GivenCents;
        }

        /// <summary>What the saved stars add to attention, thousandths. None when the business has no row. Three stars is the middle.</summary>
        public static int StarPull(FlareStarRow row)
        {
            if (row == null || row.Total <= 0)
            {
                return 0;
            }
            int avg = FlareReplyRules.Average(row);   // the answers weighed in: a well answered poor review pulls half as hard
            int pull = (avg - 3000) / 50;
            if (pull > 80)
            {
                pull = 80;
            }
            if (pull < -80)
            {
                pull = -80;
            }
            return pull;
        }

        /// <summary>Residents a day a promotion step buys. Step 0 is a free post.</summary>
        public static readonly int[] ReachSteps = { 0, 2000, 5000, 10000 };

        public static int PromoteOf(int step)
        {
            if (step < 0 || ReachSteps.Length == 0)
            {
                return 0;
            }
            return step >= ReachSteps.Length ? ReachSteps[ReachSteps.Length - 1] : ReachSteps[step];
        }

        /// <summary>The compose choices. Shop, Photo and Tag stay -1 until that chip is pressed, so nothing is forced onto the first business.</summary>
        public struct FlarePick
        {
            public int Shop;
            public int Photo;
            public int Tag;
            public int Promote;
            public string Pic;
            public string Tags;

            public static FlarePick Fresh()
            {
                return new FlarePick { Shop = -1, Photo = -1, Tag = -1 };
            }
        }

        /// <summary>One press of a compose chip. Photo, place, tag and promote each move their own choice. A poll is not among them.</summary>
        public static void Chip(ref FlarePick pick, int chip, int shops, int photos, int tags)
        {
            if (chip == 0)
            {
                pick.Photo = photos <= 0 ? -1 : pick.Photo < 0 ? 0 : (pick.Photo + 1) % photos;
            }
            else if (chip == 1)
            {
                pick.Shop = shops <= 0 ? -1 : pick.Shop < 0 ? 0 : (pick.Shop + 1) % shops;
            }
            else if (chip == 2)
            {
                pick.Tag = tags <= 0 ? -1 : pick.Tag < 0 ? 0 : (pick.Tag + 1) % tags;
            }
            else if (chip == 3)
            {
                int n = ReachSteps.Length > 0 ? ReachSteps.Length : 1;
                pick.Promote = (pick.Promote + 1) % n;
            }
        }

        /// <summary>The business a compose choice names, or null when none was chosen. The first shop is not a stand-in.</summary>
        public static string ChosenPlace(List<FlareShopSeen> shops, int shop)
        {
            if (shops == null || shop < 0 || shop >= shops.Count || shops[shop] == null)
            {
                return null;
            }
            return shops[shop].Place;
        }

        /// <summary>Writes the choice onto a compose. The place is <see cref="ChosenPlace"/>, and the reach is <see cref="PromoteOf"/>.</summary>
        public static void ApplyPick(FlareCompose c, List<FlareShopSeen> shops, FlarePick pick)
        {
            if (c == null)
            {
                return;
            }
            c.Place = ChosenPlace(shops, pick.Shop);
            c.Name = c.Place;
            c.Pic = pick.Pic;
            c.Tags = pick.Tags;
            c.PromoteReach = PromoteOf(pick.Promote);
            c.PromoteDays = c.PromoteDays > 0 ? c.PromoteDays : 1;
        }

        /// <summary>What a rival does today: 0 a post, 1 a promotion, 2 an attack on a reputation. The same rules as the player.</summary>
        public static int RivalMove(int seed, int day) => FlareRules.Roll(seed, day, 77) % 3;

        public static int MaskCount(int mask)
        {
            int n = 0;
            uint m = (uint)mask;
            while (m != 0)
            {
                n += (m & 1) != 0 ? 1 : 0;
                m >>= 1;
            }
            return n;
        }

        public static bool InMask(int mask, int district) => district >= 0 && district < 31 && ((mask >> district) & 1) != 0;

        /// <summary>A day of reach kept. The series lives on the account until Money's day series is the one store.</summary>
        public static void NoteReach(int[] days, ref int lastDay, int day, int reach)
        {
            if (days == null || days.Length == 0 || reach == 0)
            {
                return;
            }
            if (lastDay < 0)
            {
                lastDay = day;
            }
            int step = day - lastDay;
            if (step > 0)
            {
                if (step >= days.Length)
                {
                    for (int i = 0; i < days.Length; i++)
                    {
                        days[i] = 0;
                    }
                }
                else
                {
                    for (int i = 0; i < days.Length - step; i++)
                    {
                        days[i] = days[i + step];
                    }
                    for (int i = days.Length - step; i < days.Length; i++)
                    {
                        days[i] = 0;
                    }
                }
                lastDay = day;
            }
            int at = days.Length - 1;
            long sum = (long)days[at] + reach;
            days[at] = sum > int.MaxValue ? int.MaxValue : (int)sum;
        }

        /// <summary>The place a picture was taken. Life's sidecar wins when it is there; otherwise the place Flare saved.</summary>
        public static string PhotoPlace(string sidecar, string saved) => !string.IsNullOrEmpty(sidecar) ? sidecar : saved;

        public static FlareOfferNeed NeedOf(List<FlareOfferNeed> needs, int id)
        {
            if (needs == null)
            {
                return null;
            }
            for (int i = 0; i < needs.Count; i++)
            {
                if (needs[i].Id == id)
                {
                    return needs[i];
                }
            }
            return null;
        }

        public static int AdMaskOf(List<FlareAdMask> masks, int id)
        {
            if (masks == null)
            {
                return 0;
            }
            for (int i = 0; i < masks.Count; i++)
            {
                if (masks[i].Id == id)
                {
                    return masks[i].Mask;
                }
            }
            return 0;
        }
    }

    /// <summary>
    /// The player's following, as far as a job, a raise, an investor or a VIP price is concerned. The game sets
    /// <see cref="FollowersOf"/> while Flare is on. A check leaves it unset, so the extra is nothing.
    /// </summary>
    public static class FlareBrand
    {
        public static Func<int> FollowersOf;

        public static int Followers => FollowersOf != null ? FollowersOf() : 0;

        public static int JobExtra() => FlareUseRules.JobExtra(Followers);

        public static int RaiseExtra() => FlareUseRules.RaiseBp(Followers);

        public static int InvestorExtra() => FlareUseRules.InvestorChance(Followers);
    }
}
