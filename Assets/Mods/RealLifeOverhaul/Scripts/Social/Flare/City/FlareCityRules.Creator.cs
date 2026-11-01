using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>A business of the city's paying the player for posts about it: one post, or a sponsorship of several. Game-free; saved.</summary>
    public sealed class FlareOffer
    {
        public int Id;
        public string Key;           // the business's population key ("street|number")
        public string Name;          // its name, as shown
        public int Posts = 1;        // posts it pays for
        public int Done;             // posts made
        public long Fee;             // cents a post
        public int Day;              // the day it was offered
        public int Until;            // the last day it can be taken
        public bool Accepted;
        public int Next = -1;        // taken: the day of its next post, -1 none left

        public bool Open(int day) => !Accepted && day <= Until;

        public bool Running => Accepted && Done < Posts;
    }

    /// <summary>
    /// Flare in the city by rule, the player as a creator (the brief's fourth step: "brand deals offered by brands and the city's businesses as following
    /// grows (paid to the player, booked), sponsorships, a creator income at large followings"): from Local on the city's businesses offer to pay for
    /// posts about them, more often and for more as the account grows and the city trusts it; a sponsorship is several posts some days apart; from
    /// Influencer on Flare pays a creator income by the month for the views the player's posts had. A paid post is heard by the player's followers like
    /// any post, so the business that paid gets real customers from it (parity). Game-free. COST: a roll a day; a walk over the city's businesses for
    /// the one that offers, when one does.
    /// </summary>
    public static partial class FlareCityRules
    {
        private const int SaltOffer = 91, SaltOfferBusiness = 92, SaltSponsor = 93;

        /// <summary>Per mille a day the city's businesses offer the player a paid post, by the account's tier.</summary>
        public static readonly int[] OfferChance = { 0, 40, 90, 160, 250 };

        public const int DealArg = -1;                   // a sponsored post's Arg for a paid post about a business of the city's (the player's own sponsored posts carry a per cent)
        public const int OfferDays = 3;                  // days an offer stands
        public const int OfferFeeThousand = 1200;        // cents a post for each thousand followers (local creators are paid about 10 a thousand a post)
        public const int OfferFeeLeast = 2500;           // cents a post at the least
        public const int SponsorFrom = 2;                // the tier sponsorships are offered from (Known)
        public const int SponsorShare = 250;             // per mille of offers that are sponsorships, from then
        public const int SponsorPosts = 4;               // posts a sponsorship pays for
        public const int SponsorGap = 3;                 // days between them
        public const int SponsorRate = 900;              // thousandths of a single post's fee each post of a sponsorship pays
        public const int OffersMost = 16;                // offers kept, open, running or over
        public const int CreatorFrom = 3;                // the tier a creator income is paid from (Influencer)
        public const int CreatorDays = 30;               // days between two payments
        public const int CreatorThousandViews = 300;     // cents for each thousand views of the player's posts in those days

        /// <summary>What a business pays for one post, cents: by the player's followers and the city's trust in the account (its reputation), at the game's prices (thousandths).</summary>
        public static long OfferFee(int followers, int reputation, int priceLevel)
        {
            long fee = (long)Math.Max(0, followers) * OfferFeeThousand / 1000 * FlareRules.ReputationFactor(reputation) / 1000;
            return Math.Max(OfferFeeLeast, fee) * Math.Max(1, priceLevel) / 1000;
        }

        /// <summary>
        /// Today's offer, if a business of the city's makes one: none below Local, at most one open at a time; the business drawn by its room for
        /// customers among the city's and the rivals' (never the player's or a session player's); a sponsorship now and then from Known on.
        /// </summary>
        public static FlareOffer Offer(FlareCityState c, PopulationState s, int day, int followers, int tier, int reputation, int priceLevel)
        {
            int t = Math.Max(0, Math.Min(tier, OfferChance.Length - 1));
            if (OfferChance[t] <= 0 || Mille(c.Seed, day, SaltOffer, 0) >= OfferChance[t])
            {
                return null;
            }
            for (int i = 0; i < c.Offers.Count; i++)
            {
                if (c.Offers[i].Open(day))
                {
                    return null;   // one open offer at a time
                }
            }
            BusinessTable bt = s.Businesses;
            long total = 0;
            for (int b = 0; b < bt.Count; b++)
            {
                total += Offers(bt, b) ? Math.Max(1, bt.Room[b]) : 0;
            }
            if (total <= 0)
            {
                return null;
            }
            long x = (long)Millionth(c.Seed, day, SaltOfferBusiness, 0) * total / 1000000;
            int pick = -1;
            for (long sum = 0, b = 0; b < bt.Count; b++)
            {
                if (!Offers(bt, (int)b))
                {
                    continue;
                }
                sum += Math.Max(1, bt.Room[b]);
                if (x < sum)
                {
                    pick = (int)b;
                    break;
                }
            }
            if (pick < 0)
            {
                return null;
            }
            bool sponsor = t >= SponsorFrom && Mille(c.Seed, day, SaltSponsor, 0) < SponsorShare;
            long fee = OfferFee(followers, reputation, priceLevel);
            var offer = new FlareOffer
            {
                Id = c.NextOffer++, Key = bt.Key[pick], Posts = sponsor ? SponsorPosts : 1, Fee = sponsor ? fee * SponsorRate / 1000 : fee, Day = day, Until = day + OfferDays - 1
            };
            while (c.Offers.Count >= OffersMost)
            {
                c.Offers.RemoveAt(0);
            }
            c.Offers.Add(offer);
            return offer;
        }

        // A business that may offer: one of the city's or a rival's that sells to people.
        private static bool Offers(BusinessTable bt, int b)
        {
            return Sells(bt, b) && bt.Owner[b] != (byte)Population.Owner.Player && bt.Owner[b] != (byte)Population.Owner.Guest && bt.Key[b] != null;
        }

        /// <summary>The player takes an offer: its first post is due today, the rest every few days. False when it cannot be taken.</summary>
        public static bool Accept(FlareOffer offer, int day)
        {
            if (offer == null || !offer.Open(day))
            {
                return false;
            }
            offer.Accepted = true;
            offer.Next = day;
            return true;
        }

        /// <summary>A post of an offer made: the next one's day, or none when it is done.</summary>
        public static void Posted(FlareOffer offer, int day)
        {
            offer.Done++;
            offer.Next = offer.Done < offer.Posts ? day + SponsorGap : -1;
        }

        /// <summary>Whether an offer the player took has a post due today.</summary>
        public static bool Due(FlareOffer offer, int day) => offer.Running && offer.Next >= 0 && day >= offer.Next;

        /// <summary>Whether a creator income is due today: from Influencer on, every <see cref="CreatorDays"/> days.</summary>
        public static bool CreatorDue(FlareCityState c, int day, int tier)
        {
            if (tier < CreatorFrom)
            {
                return false;
            }
            if (c.CreatorDay < 0)
            {
                c.CreatorDay = day;   // the first month counts from the day the account reached it
                return false;
            }
            return day - c.CreatorDay >= CreatorDays;
        }

        /// <summary>A month's creator income, cents, for its views, at the game's prices (thousandths).</summary>
        public static long CreatorIncome(long views, int priceLevel) => Math.Max(0, views) * CreatorThousandViews / 1000 * Math.Max(1, priceLevel) / 1000;
    }
}
