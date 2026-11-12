using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>The tabs of the notifications page: all of them, then the ones about sales, reviews, mentions and deals.</summary>
    public enum FlareNoticeTab : byte
    {
        All = 0,
        Sales = 1,
        Reviews = 2,
        Mentions = 3,
        Deals = 4
    }

    /// <summary>
    /// What the notifications page adds to the account's records, by rule: the tab each kind of notice is in, the milestone a tier passed
    /// leaves (the day it was passed on is kept, so the notice is a row, not a toast), the followers a post brought, a tag trending that
    /// the player's post is part of, a rival's or the city's promoted post in a district, and what a post of the player's brought, told
    /// under the notice that is about it. Game-free: EconomyCheck's FlareAuditChecks runs every one. Cost: one walk over the posts, the
    /// ads and the boosts when the page opens; the tier check is one comparison on the day's round.
    /// </summary>
    public static class FlareNoticeRules
    {
        public const int Tabs = 5;
        public const int GainedLeast = 5;        // followers a post must have brought to be told
        public const int GainedDays = 3;         // ...for this many days after it went up
        public const int TrendingMost = 3;       // tags of the day's trending that are looked for among the player's posts
        public const int RivalDays = 1;          // a promoted post of a rival's is news the day it starts and the day after
        public const int RivalMost = 3;

        /// <summary>The tab a kind of notice belongs to besides All; All itself for the kinds that are only there.</summary>
        public static FlareNoticeTab TabOf(FlareNoticeKind kind)
        {
            switch (kind)
            {
                case FlareNoticeKind.Promo:
                case FlareNoticeKind.Campaign:
                    return FlareNoticeTab.Sales;
                case FlareNoticeKind.Review:
                    return FlareNoticeTab.Reviews;
                case FlareNoticeKind.Like:
                case FlareNoticeKind.Reply:
                case FlareNoticeKind.Repost:
                case FlareNoticeKind.Mention:
                case FlareNoticeKind.Going:
                case FlareNoticeKind.Follow:
                case FlareNoticeKind.Gained:
                case FlareNoticeKind.TrendPart:
                    return FlareNoticeTab.Mentions;
                case FlareNoticeKind.Offer:
                case FlareNoticeKind.Job:
                    return FlareNoticeTab.Deals;
                default:
                    return FlareNoticeTab.All;
            }
        }

        /// <summary>Whether a notice shows under a tab: every notice under All, the rest under their own.</summary>
        public static bool InTab(int tab, FlareNoticeKind kind) => tab <= 0 || (int)TabOf(kind) == tab;

        /// <summary>How many of the notices show under a tab.</summary>
        public static int Count(List<FlareNotice> notices, int tab)
        {
            int n = 0;
            for (int i = 0; i < notices.Count; i++)
            {
                n += InTab(tab, notices[i].Kind) ? 1 : 0;
            }
            return n;
        }

        // ------------------------------------------------------------------------------------------------ the tiers

        /// <summary>Whether any tier has a day kept (the account's line is written for it).</summary>
        public static bool AnyTierDay(FlareFile f)
        {
            for (int i = 0; i < f.TierDays.Length; i++)
            {
                if (f.TierDays[i] >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The account passed a tier since it was last looked at: from Local on, the day is kept for each tier passed (a jump over two keeps
        /// both) and the tier seen moves up. Called on the day's round and after a post, so a tier passed by followers alone is told too.
        /// False when nothing was passed.
        /// </summary>
        public static bool TierUp(FlareFile f, int day, out int tier)
        {
            tier = (int)f.Tier;
            if (tier <= f.TierSeen || tier < (int)FlareTier.Local)
            {
                return false;
            }
            for (int t = Math.Max(f.TierSeen + 1, (int)FlareTier.Local); t <= tier && t < f.TierDays.Length; t++)
            {
                f.TierDays[t] = day;
            }
            f.TierSeen = tier;
            return true;
        }

        // ------------------------------------------------------------------------------------------------ the notices

        /// <summary>
        /// The notices the account's own records tell: a tier passed, the followers a post brought, a tag trending that a post of the player's
        /// is part of, and the promoted posts of the city's businesses that compete with one of the player's shops (the same kind of thing
        /// sold, or the same district). Added to <paramref name="into"/>, none ahead of now.
        /// </summary>
        public static void AddDerived(FlareFile f, long now, IReadOnlyList<FlareAd> ads, List<FlareNotice> into, List<string> tags, List<int> counts)
        {
            for (int t = (int)FlareTier.Local; t < f.TierDays.Length; t++)
            {
                long at = f.TierDays[t] >= 0 ? FlareLiveRules.Now(f.TierDays[t], FlareLiveRules.NoticeHour, 0) : long.MaxValue;
                if (at <= now)
                {
                    into.Add(new FlareNotice { Kind = FlareNoticeKind.Milestone, At = at, Post = -1, Dm = -1, Who = "me", Figure = t });
                }
            }
            for (int i = f.Posts.Count - 1; i >= 0; i--)
            {
                FlarePost p = f.Posts[i];
                if (p.Author != FlareAuthor.Player || p.Source == FlareSource.Reply || p.At > now)
                {
                    continue;
                }
                if (now - p.At <= GainedDays * 1440L && p.Gained >= GainedLeast)
                {
                    into.Add(new FlareNotice { Kind = FlareNoticeKind.Gained, At = Math.Min(now, p.At + 120), Post = p.Id, Dm = -1, Who = "me", Figure = p.Gained, Name = p.Name });
                }
            }
            FlareLiveRules.Trending(f.Posts, now, TrendingMost, tags, counts);
            for (int k = 0; k < tags.Count; k++)
            {
                for (int i = f.Posts.Count - 1; i >= 0; i--)
                {
                    FlarePost p = f.Posts[i];
                    if (p.Author == FlareAuthor.Player && p.At <= now && now - p.At <= 1440 && p.Topic != null && FlareFeedRules.HasTag(p.Topic, tags[k]))
                    {
                        into.Add(new FlareNotice { Kind = FlareNoticeKind.TrendPart, At = p.At, Post = p.Id, Dm = -1, Who = "me", Name = tags[k], Figure = counts[k] });
                        break;
                    }
                }
            }
            int today = (int)(now / 1440), shown = 0;
            for (int i = ads != null ? ads.Count - 1 : -1; i >= 0 && shown < RivalMost; i--)
            {
                FlareAd ad = ads[i];
                long at = FlareLiveRules.Now(ad.From, 8, 0);
                if (ad.Mine || !ad.Rival || today - ad.From > RivalDays || at > now)
                {
                    continue;
                }
                into.Add(new FlareNotice { Kind = FlareNoticeKind.Rival, At = at, Post = -1, Dm = -1, Who = ad.Place, Name = ad.Name, Figure = ad.Reach, Extra = ad.District });
                shown++;
            }
        }

        // ------------------------------------------------------------------------------------------------ what a post brought

        /// <summary>
        /// What a post of the player's brought, told under the notice about it: the visitors and takings its boost has credited so far.
        /// False when the post has no boost (it was not about a business) or its hours have not run yet.
        /// </summary>
        public static bool Brought(FlareFile f, int post, long now, out int visitors, out long cents)
        {
            visitors = 0;
            cents = 0;
            for (int i = f.Boosts.Count - 1; i >= 0; i--)
            {
                FlareBoost b = f.Boosts[i];
                if (b.Post != post)
                {
                    continue;
                }
                FlareUseRules.Credited(b, (int)Math.Min(int.MaxValue, now), out int given, out int givenCents);
                visitors = given;
                cents = givenCents;
                return given > 0 || givenCents > 0;
            }
            return false;
        }
    }
}
