using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The rules behind the kit face's working pages that no other rules file had: what the player may write in their profile, how the
    /// influencers for hire are filtered and ordered for a business, what a campaign running has brought so far, the week's totals of
    /// what the player's posts and ads did in the city, and the districts a following lives in. Game-free (EconomyCheck's FlareAuditChecks). Cost: a walk over the cast (30), the funnels
    /// (kept to FlareCityCatalogue.FunnelsMost) or the shops, when a page is written.
    /// </summary>
    public static class FlareDeskRules
    {
        public const int BioMost = 90;           // characters of the profile's words
        public const int BannerMost = 300;       // characters of a picture's key
        public const int DistrictsShown = 5;     // districts the donut names; the rest are one slice
        public const int WeekDays = 7;

        // ------------------------------------------------------------------------------------------------ the profile

        /// <summary>The bio as it is kept: one line of at most <see cref="BioMost"/> characters, or null for none.</summary>
        public static string CleanBio(string text) => FlareLiveRules.Clip(text, BioMost);

        /// <summary>The banner as it is kept: a picture key ("f:" and the file), or null for the newest picture of the account.</summary>
        public static string CleanBanner(string key)
        {
            return !string.IsNullOrEmpty(key) && key.Length <= BannerMost && key.StartsWith("f:", StringComparison.Ordinal) ? key : null;
        }

        // ------------------------------------------------------------------------------------------------ hiring

        /// <summary>The niches an influencer can have, as the hire page's filter counts them.</summary>
        public static readonly int NicheCount = Enum.GetValues(typeof(FlareNiche)).Length;

        /// <summary>Whether an influencer shows under a niche choice: 0 shows all, 1 and up one niche each.</summary>
        public static bool NicheShown(FlareInfluencer inf, int choice) => choice <= 0 || (int)inf.Niche == choice - 1;

        /// <summary>How well an influencer suits a business of the player's, thousandths (FlareCast.Fit).</summary>
        public static int FitOf(FlareInfluencer inf, FlareShop shop) => FlareCast.Fit(inf, shop.Niche, shop.District);

        /// <summary>The cast ordered for hiring: by fit to the business first, or by followers; ties by the number, so the order is the same each time.</summary>
        public static void Sort(List<FlareInfluencer> cast, bool byFit, FlareShop shop)
        {
            if (byFit)
            {
                cast.Sort((a, b) =>
                {
                    int fa = FitOf(a, shop), fb = FitOf(b, shop);
                    return fb != fa ? fb.CompareTo(fa) : b.Followers != a.Followers ? b.Followers.CompareTo(a.Followers) : a.Id.CompareTo(b.Id);
                });
                return;
            }
            cast.Sort((a, b) => b.Followers != a.Followers ? b.Followers.CompareTo(a.Followers) : a.Id.CompareTo(b.Id));
        }

        /// <summary>A fit in thousandths as a per cent of a perfect match (the best a niche and a district give is 1.56).</summary>
        public static int FitPercent(int fit) => Math.Max(0, Math.Min(100, (int)Math.Round(fit * 100.0 / 1560.0)));

        /// <summary>
        /// What a campaign running has brought so far: the visitors and takings its influencers' posts have been worth since it began, from
        /// the posts they made (FlareCast.Day keeps one record a post). Zero before the first post.
        /// </summary>
        public static void CampaignSoFar(FlareFile f, FlareHire h, int today, out int visitors, out long cents)
        {
            visitors = 0;
            cents = 0;
            for (int i = 0; i < f.PastHires.Count; i++)
            {
                FlarePastHire p = f.PastHires[i];
                if (p.Place == h.Place && p.Day >= h.From && p.Day <= today && h.Who.Contains(p.Who))
                {
                    visitors += p.Visitors;
                    cents += p.Cents;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ ads

        /// <summary>
        /// The ad of another business that shares a district's reach with an ad at <paramref name="place"/> today: running now (its days not over),
        /// shown in that district or the whole city. The first, newest ad first; null with none.
        /// </summary>
        public static FlareAd SharedWith(IReadOnlyList<FlareAd> ads, int district, int day, string place)
        {
            for (int i = ads != null ? ads.Count - 1 : -1; i >= 0; i--)
            {
                FlareAd ad = ads[i];
                if (!ad.Mine && ad.Place != place && day >= ad.From && day < ad.From + Math.Max(1, ad.Days) && (ad.District < 0 || ad.District == district))
                {
                    return ad;
                }
            }
            return null;
        }

        // ------------------------------------------------------------------------------------------------ the city

        /// <summary>
        /// The week's totals of what the player's posts and ads did in the city: residents who saw, visits it brought and takings (cents), the
        /// funnels of the last <see cref="WeekDays"/> days, today included.
        /// </summary>
        public static void WeekTotals(IReadOnlyList<FlareFunnel> funnels, int today, out int seen, out int came, out long spent)
        {
            seen = 0;
            came = 0;
            spent = 0;
            for (int i = 0; funnels != null && i < funnels.Count; i++)
            {
                FlareFunnel f = funnels[i];
                if (f.Kind != FunnelKind.Business && f.Day > today - WeekDays && f.Day <= today)
                {
                    seen += f.Seen;
                    came += f.Came;
                    spent += f.Spent;
                }
            }
        }

        /// <summary>
        /// The districts that hold most of a following, largest first: the first <see cref="DistrictsShown"/> with followers into
        /// <paramref name="order"/>, how many there are, and what the others hold together in <paramref name="rest"/>.
        /// </summary>
        public static int TopDistricts(int[] counts, int[] order, out int rest)
        {
            int n = 0;
            for (int d = 0; d < counts.Length; d++)
            {
                if (counts[d] <= 0)
                {
                    continue;
                }
                int at = n;
                while (at > 0 && counts[order[at - 1]] < counts[d])
                {
                    if (at < order.Length)
                    {
                        order[at] = order[at - 1];
                    }
                    at--;
                }
                if (at < order.Length)
                {
                    order[at] = d;
                }
                n = Math.Min(order.Length, n + 1);
            }
            int shown = 0, total = 0;
            for (int d = 0; d < counts.Length; d++)
            {
                total += Math.Max(0, counts[d]);
            }
            for (int i = 0; i < n; i++)
            {
                shown += counts[order[i]];
            }
            rest = Math.Max(0, total - shown);
            return n;
        }
    }
}
