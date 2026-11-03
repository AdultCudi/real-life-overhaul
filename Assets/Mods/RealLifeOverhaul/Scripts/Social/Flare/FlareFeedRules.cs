using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Flare
{
    /// <summary>How long ago a post or a message went up, in the feed's short words: now, 5m, 2h, 3d, or the day.</summary>
    public enum FlareAgo
    {
        Now = 0,
        Minutes = 1,
        Hours = 2,
        Days = 3,
        Date = 4         // older than a week: the day's number
    }

    /// <summary>The tabs over Flare's home feed.</summary>
    public enum FlareFeedTab
    {
        ForYou = 0,      // everything the account sees
        Following = 1,   // the accounts the player follows, their friends and their own
        Businesses = 2   // the city's businesses, brands and companies, and posts about a place
    }

    /// <summary>
    /// Flare's feed by rule, for the kit face: how long ago, a count in short form, the words of a post without the
    /// hash marks its tags line carries, which tab a post belongs to, what a search finds and the week's figures.
    /// Game-free (EconomyCheck's FlareCompleteChecks). Cost: a few comparisons a post, when a page is written.
    /// </summary>
    public static class FlareFeedRules
    {
        public const int ShortDays = 7;          // under a week a post says "3d"; older, its day
        public const int TagsShown = 3;          // tags a card shows under its words
        public const int SearchMost = 24;        // results a search lists
        public const int WeekDays = 7;

        /// <summary>How long ago, and the number to write with it (minutes, hours, days, or the day itself).</summary>
        public static FlareAgo Ago(long at, long now, out long n)
        {
            long since = now - at;
            if (since < 1)
            {
                n = 0;
                return FlareAgo.Now;
            }
            if (since < 60)
            {
                n = since;
                return FlareAgo.Minutes;
            }
            if (since < 1440)
            {
                n = since / 60;
                return FlareAgo.Hours;
            }
            if (since < ShortDays * 1440L)
            {
                n = since / 1440;
                return FlareAgo.Days;
            }
            n = at < 0 ? 0 : at / 1440;
            return FlareAgo.Date;
        }

        /// <summary>A count as a feed writes it: 842, 1.2K, 11.9K, 96K, 120K, 1.4M. The point is the invariant one.</summary>
        public static string Count(long n)
        {
            if (n < 0)
            {
                return "-" + Count(-n);
            }
            if (n < 1000)
            {
                return n.ToString(CultureInfo.InvariantCulture);
            }
            if (n < 100000)
            {
                long tenths = n / 100;   // 11,950 is 11.9K: a count is never rounded up past what it is
                return tenths % 10 == 0 ? (tenths / 10).ToString(CultureInfo.InvariantCulture) + "K"
                    : (tenths / 10).ToString(CultureInfo.InvariantCulture) + "." + (tenths % 10).ToString(CultureInfo.InvariantCulture) + "K";
            }
            if (n < 1000000)
            {
                return (n / 1000).ToString(CultureInfo.InvariantCulture) + "K";
            }
            long m = n / 100000;
            return m % 10 == 0 ? (m / 10).ToString(CultureInfo.InvariantCulture) + "M"
                : (m / 10).ToString(CultureInfo.InvariantCulture) + "." + (m % 10).ToString(CultureInfo.InvariantCulture) + "M";
        }

        /// <summary>A post's words with the hash marks taken off its tags ("#lunch" reads "lunch"): the tags line under it carries them.</summary>
        public static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('#') < 0)
            {
                return text;
            }
            var b = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool tag = c == '#' && i + 1 < text.Length && (char.IsLetterOrDigit(text[i + 1]) || text[i + 1] == '_') && (i == 0 || !char.IsLetterOrDigit(text[i - 1]));
                if (!tag)
                {
                    b.Append(c);
                }
            }
            return b.ToString();
        }

        /// <summary>A post's tags (its Topic, a space between each) into an array; how many, at most its length.</summary>
        public static int TagsOf(string topic, string[] into)
        {
            if (string.IsNullOrEmpty(topic) || into == null)
            {
                return 0;
            }
            int n = 0, start = 0;
            for (int i = 0; i <= topic.Length && n < into.Length; i++)
            {
                if (i == topic.Length || topic[i] == ' ')
                {
                    if (i > start)
                    {
                        string tag = topic.Substring(start, i - start);
                        bool again = false;
                        for (int k = 0; k < n; k++)
                        {
                            again |= string.Equals(into[k], tag, StringComparison.OrdinalIgnoreCase);
                        }
                        if (!again)
                        {
                            into[n++] = tag;
                        }
                    }
                    start = i + 1;
                }
            }
            return n;
        }

        /// <summary>Whether a post belongs on a tab of the home feed.</summary>
        public static bool InTab(FlareFeedTab tab, FlareAuthor author, FlareSource source, bool followed, bool aboutPlace)
        {
            switch (tab)
            {
                case FlareFeedTab.Following:
                    return followed || author == FlareAuthor.Player || author == FlareAuthor.Friend;
                case FlareFeedTab.Businesses:
                    return author == FlareAuthor.Business || author == FlareAuthor.Brand || author == FlareAuthor.Company || author == FlareAuthor.Tycoon
                           || (aboutPlace && (source == FlareSource.Review || source == FlareSource.Announce || source == FlareSource.Visit || source == FlareSource.Hired
                                              || source == FlareSource.Sponsored || source == FlareSource.BusinessPost || source == FlareSource.Opened));
                default:
                    return true;
            }
        }

        /// <summary>Whether words hold what a search asks, whatever the case; an empty search holds everything.</summary>
        public static bool Matches(string words, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }
            return !string.IsNullOrEmpty(words) && CultureInfo.InvariantCulture.CompareInfo.IndexOf(words, query.Trim().TrimStart('#', '@'), CompareOptions.IgnoreCase) >= 0;
        }

        /// <summary>Followers won (or lost) over the week the history holds: now against the oldest day kept.</summary>
        public static int WeekGain(int[] history, int now)
        {
            return history == null || history.Length == 0 ? 0 : now - history[0];
        }

        /// <summary>A change in whole per cent: from nothing to something is a hundred.</summary>
        public static int Change(long now, long before)
        {
            if (before <= 0)
            {
                return now > 0 ? 100 : 0;
            }
            long c = (now - before) * 100 / before;
            return c > 9999 ? 9999 : c < -100 ? -100 : (int)c;
        }

        /// <summary>The people the player's own posts reached in the last week (ending today) and in the week before.</summary>
        public static void Reach(List<FlarePost> posts, int today, out long week, out long before)
        {
            week = before = 0;
            for (int i = 0; posts != null && i < posts.Count; i++)
            {
                FlarePost p = posts[i];
                if (p.Author != FlareAuthor.Player || p.Source == FlareSource.Reply)
                {
                    continue;
                }
                int age = today - p.Day;
                if (age >= 0 && age < WeekDays)
                {
                    week += Math.Max(0, p.Reach);
                }
                else if (age >= WeekDays && age < WeekDays * 2)
                {
                    before += Math.Max(0, p.Reach);
                }
            }
        }

        /// <summary>The follower line for a chart: the week kept and today's count at its end.</summary>
        public static float[] Line(int[] history, int now)
        {
            int n = history != null ? history.Length : 0;
            var line = new float[n + 1];
            for (int i = 0; i < n; i++)
            {
                line[i] = history[i];
            }
            line[n] = now;
            return line;
        }

        /// <summary>The tag a poll carries under its words: a post with it asks yes or no.</summary>
        public const string PollTag = "poll";

        /// <summary>
        /// A poll's result at a minute: its votes are the people who reacted so far (the likes, replies and reposts shown then,
        /// and the player's own like), and the yes share is the post's own roll, leaning yes on a post that went viral and no on
        /// one that went badly; per cent, 5 to 95. False for a post that is not a poll.
        /// </summary>
        public static bool Poll(FlarePost p, long now, out int yes, out int votes)
        {
            yes = votes = 0;
            if (p == null || !HasTag(p.Topic, PollTag))
            {
                return false;
            }
            votes = FlareLiveRules.Shown(p.Likes, p, now) + FlareLiveRules.Shown(p.Replies, p, now) + FlareLiveRules.Shown(p.Reposts, p, now)
                    + Math.Max(0, p.PlayerLikes) + (p.Liked ? 1 : 0);
            int lean = p.Bad ? -15 : p.Viral ? 15 : 0;
            yes = FlareRules.Clamp(35 + FlareRules.Roll(p.Seed, p.Id, 931) % 31 + lean, 5, 95);
            return true;
        }

        /// <summary>Whether a post's topic holds a tag as a whole word, in any case.</summary>
        public static bool HasTag(string topic, string tag)
        {
            if (string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(tag))
            {
                return false;
            }
            int at = topic.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
            while (at >= 0)
            {
                bool start = at == 0 || topic[at - 1] == ' ', end = at + tag.Length == topic.Length || topic[at + tag.Length] == ' ';
                if (start && end)
                {
                    return true;
                }
                at = topic.IndexOf(tag, at + 1, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        /// <summary>Whether a post's promotion was paid for: a boost of it that bought reach.</summary>
        public static bool Promoted(List<FlareBoost> boosts, int post)
        {
            for (int i = 0; boosts != null && i < boosts.Count; i++)
            {
                if (boosts[i].Post == post && boosts[i].Paid)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
