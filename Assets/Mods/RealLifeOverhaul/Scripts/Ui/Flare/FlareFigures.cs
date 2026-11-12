using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What an account stood at over its last week: the facts every page that shows the week reads, whichever page it is.</summary>
    public struct FlareWeekFacts
    {
        public long Followers;
        public int Gain;                  // followers won over the week the history holds
        public long Reach, ReachBefore;   // the people the account's posts reached this week and the week before
        public int StarsMilli;            // the rating of the player's businesses together, thousandths of a star
        public int Reviews;               // the reviews it stands on
    }

    /// <summary>
    /// The figures of Flare's pages and how they are worded, written once: the side's week, the studio's tiles, the heads that count something and what a post or an ad did. The
    /// Live fill (FlareScreen) and the preview's samples both call these, so a rail, a head, a tile and a count are the same fact worded the same wherever they stand. Game-free.
    /// Cost: a few string joins when a page is written, nothing per frame.
    /// </summary>
    public static class FlareFigures
    {
        private static readonly string[] None = new string[0];

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

        /// <summary>A whole number with its thousands separated, the sign shown on a gain.</summary>
        public static string N(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        public static string Signed(long value) => (value > 0 ? "+" : string.Empty) + N(value);

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

        /// <summary>
        /// Whether the account has nothing to count yet: no followers, no reach in two weeks and no review. Its week is then not drawn on any page (a row of zeros says nothing the
        /// page's own empty state does not), and the page that has an empty state of its own says it once.
        /// </summary>
        public static bool NewAccount(in FlareWeekFacts w) => w.Followers <= 0 && w.Reach <= 0 && w.ReachBefore <= 0 && w.Reviews <= 0;

        /// <summary>"1 review" or "128 reviews".</summary>
        public static string ReviewsCount(int n, UiWords t) =>
            n == 1 ? t("reallife_flare_kit_reviews_one", "1 review") : t("reallife_flare_kit_reviews_count", "{count} reviews").Replace("{count}", N(n));

        /// <summary>"1 post" or "3,104 posts".</summary>
        public static string PostsCount(int n, UiWords t) =>
            n == 1 ? t("reallife_flare_kit_posts_one", "1 post") : t("reallife_flare_kit_posts_count", "{count} posts").Replace("{count}", Count(n));

        /// <summary>
        /// The week beside a page: followers won, the people reached against the week before, the rating of the player's businesses. On the player's own profile, whose head
        /// says the followers already, the followers are left out. A new account gets nothing (<see cref="NewAccount"/>), the arrays empty.
        /// </summary>
        public static void Week(in FlareWeekFacts w, UiWords t, bool own, out string[] labels, out string[] figures, out string[] deltas, out int[] tones)
        {
            if (NewAccount(w))
            {
                labels = figures = deltas = None;
                tones = new int[0];
                return;
            }
            int change = Change(w.Reach, w.ReachBefore);
            labels = new[] { t("reallife_flare_kit_fig_followers", "Followers"), t("reallife_flare_kit_fig_reach", "Reach"), t("reallife_flare_kit_fig_rating", "Rating") };
            figures = new[]
            {
                Count(w.Followers), Count(w.Reach), w.Reviews > 0 ? Stars(w.StarsMilli) : t("reallife_flare_kit_rating_none", "Not yet")
            };
            deltas = new[]
            {
                t("reallife_flare_kit_week_followers", "{change} this week").Replace("{change}", Signed(w.Gain)),
                t("reallife_flare_kit_week_reach", "{change}% on last week").Replace("{change}", Signed(change)),
                w.Reviews > 0 ? ReviewsCount(w.Reviews, t) : t("reallife_flare_kit_week_no_reviews", "No reviews yet")
            };
            tones = new[] { Math.Sign(w.Gain), Math.Sign(change), 0 };
            if (own)
            {
                labels = new[] { labels[1], labels[2] };
                figures = new[] { figures[1], figures[2] };
                deltas = new[] { deltas[1], deltas[2] };
                tones = new[] { tones[1], tones[2] };
            }
        }

        /// <summary>A rating as the pages write it, one decimal: thousandths of a star in, "4.3" out.</summary>
        public static string Stars(int milli) => (milli / 1000f).ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>
        /// The creator studio's four tiles and what changed under each, from the same week the side shows (followers and reach) and the visitors and takings the businesses
        /// had from Flare (whole dollars, written by <paramref name="money"/>, the game's own formatting in the game).
        /// </summary>
        public static void StudioTiles(in FlareWeekFacts w, int visitors, long visitorsBefore, long dollars, long dollarsBefore, UiWords t, Func<long, string> money, FlareStudioModel st)
        {
            int change = Change(w.Reach, w.ReachBefore);
            st.Labels = new[]
            {
                t("reallife_flare_kit_fig_followers", "Followers"), t("reallife_flare_kit_fig_reach", "Reach"), t("reallife_flare_kit_fig_visitors", "Visitors"),
                t("reallife_flare_kit_fig_money", "Money from Flare")
            };
            st.Figures = new[] { Count(w.Followers), Count(w.Reach), N(visitors), money(dollars) };
            string before = t("reallife_flare_kit_week_before", "{change} on last week");
            st.Deltas = new[]
            {
                t("reallife_flare_kit_week_followers", "{change} this week").Replace("{change}", Signed(w.Gain)),
                t("reallife_flare_kit_week_reach", "{change}% on last week").Replace("{change}", Signed(change)),
                before.Replace("{change}", Signed(visitors - visitorsBefore)),
                before.Replace("{change}", SignedMoney(dollars - dollarsBefore, money))
            };
            st.Tones = new[] { Math.Sign(w.Gain), Math.Sign(change), Math.Sign(visitors - visitorsBefore), Math.Sign(dollars - dollarsBefore) };
        }

        /// <summary>An amount of money with its sign: +$1,180 or -$140.</summary>
        public static string SignedMoney(long dollars, Func<long, string> money) => (dollars > 0 ? "+" : dollars < 0 ? "-" : string.Empty) + money(Math.Abs(dollars));

        /// <summary>The head of the inbox: how many are unread, or that all are read.</summary>
        public static string UnreadHead(int unread, UiWords t) =>
            unread > 0 ? t("reallife_flare_kit_unread", "{count} unread").Replace("{count}", N(unread)) : t("reallife_flare_kit_all_read", "All read");

        /// <summary>The head of the notifications: how many are new, or that all are seen.</summary>
        public static string NewHead(int count, UiWords t) =>
            count > 0 ? t("reallife_flare_kit_new", "{count} new").Replace("{count}", N(count)) : t("reallife_flare_kit_all_seen", "All caught up");

        /// <summary>
        /// The replies a post has: the ones listed under it and the ones the list says are more. The count on the post's card is this and no other, so the card and its list
        /// never disagree.
        /// </summary>
        public static int Replies(int listed, int more) => Math.Max(0, listed) + Math.Max(0, more);

        /// <summary>
        /// How many replies a post has, for its card in a feed and for its card in its own thread alike: the replies posted under it that the player can read, and the ones its
        /// model says it drew (those have no row of their own). One rule, so a post is never counted two ways.
        /// </summary>
        public static int ReplyTotal(int posted, int drawn) => Math.Max(0, posted) + Math.Max(0, drawn);

        /// <summary>The replies of a post that its thread does not list: the total less the rows listed, the "N more replies" line. Never below nothing.</summary>
        public static int ReplyMore(int total, int listed) => Math.Max(0, total - Math.Max(0, listed));

        /// <summary>
        /// Words with a figure kept in one piece: the space between a number and what it counts ("12 in", "100 posts") and the little words between two numbers ("12 in 100", "12 em cada
        /// 100") becomes a no-break space, so a note that wraps never leaves "12 in" at the end of a line and "100 posts" at the start of the next. Languages that write without spaces
        /// are kept whole by FlareFace.Balanced instead. Nothing else changes.
        /// </summary>
        public static string Bind(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(' ') < 0)
            {
                return text;
            }
            string[] tok = text.Split(' ');
            var bound = new bool[tok.Length];   // bound[i]: the space after tok[i] does not break
            for (int i = 0; i + 1 < tok.Length; i++)
            {
                if (!HasDigit(tok[i]))
                {
                    continue;
                }
                bound[i] |= char.IsLetter(tok[i + 1].Length > 0 ? tok[i + 1][0] : ' ');
                for (int gap = 1; gap <= 2 && i + gap + 1 < tok.Length; gap++)
                {
                    bool between = true;
                    for (int g = 1; g <= gap; g++)
                    {
                        between &= Connector(tok[i + g]);
                    }
                    if (between && HasDigit(tok[i + gap + 1]))
                    {
                        for (int g = 0; g <= gap; g++)
                        {
                            bound[i + g] = true;
                        }
                    }
                }
            }
            var sb = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < tok.Length; i++)
            {
                sb.Append(tok[i]);
                if (i + 1 < tok.Length)
                {
                    sb.Append(bound[i] ? ' ' : ' ');
                }
            }
            return sb.ToString();
        }

        private static bool HasDigit(string word)
        {
            for (int i = 0; i < word.Length; i++)
            {
                if (char.IsDigit(word[i]))
                {
                    return true;
                }
            }
            return false;
        }

        // A short word of letters only: the "in" of "12 in 100", the "em cada" of "12 em cada 100".
        private static bool Connector(string word)
        {
            if (word.Length == 0 || word.Length > 4)
            {
                return false;
            }
            for (int i = 0; i < word.Length; i++)
            {
                if (!char.IsLetter(word[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// What the city page's donut counts, said in its own words so it cannot be read as the account's whole following: the followers the city has placed in districts, out of
        /// the account's followers (the rail's figure, written the same way). Followers living outside the city are in the account's count and not in the donut.
        /// </summary>
        public static string DonutTotal(long placed, long followers, UiWords t) =>
            t("reallife_flare_kit_donut_total", "{count} of your {followers} followers live in the city").Replace("{count}", N(placed)).Replace("{followers}", Count(Math.Max(followers, placed)));

        /// <summary>How many districts the hire card and the hire sheet name for an influencer's audience.</summary>
        public const int AudienceMost = 4;

        /// <summary>A part of a whole as a whole per cent, "34%"; nothing for an empty whole.</summary>
        public static string Percent(long part, long whole) => (whole > 0 ? part * 100 / whole : 0).ToString(CultureInfo.InvariantCulture) + "%";

        /// <summary>"12" for twelve in a hundred, from a rate in tenths of a per cent: how the hire card and the hire sheet say the odds that a post falls flat or goes viral.</summary>
        public static string Odds(int tenths) => (tenths / 10).ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Where an influencer's followers in the city live, as the hire card's bars and the hire sheet's bars both show it: the districts that hold most (at most
        /// <see cref="AudienceMost"/>, largest first), each as its per cent of the whole audience, and its bar as a share of the largest. One function, so the card and the sheet of one
        /// influencer cannot name different districts or shares.
        /// </summary>
        public static void Audience(System.Collections.Generic.IReadOnlyList<string> districts, System.Collections.Generic.IReadOnlyList<int> counts, int total, Func<string, string> name,
            out string[] labels, out string[] figures, out float[] shares)
        {
            int n = Math.Min(AudienceMost, Math.Min(districts.Count, counts.Count));
            labels = new string[n];
            figures = new string[n];
            shares = new float[n];
            int most = n > 0 ? Math.Max(1, counts[0]) : 1;
            for (int i = 0; i < n; i++)
            {
                labels[i] = name(districts[i]);
                figures[i] = Percent(counts[i], total);
                shares[i] = counts[i] / (float)most;
            }
        }

        /// <summary>
        /// The views a post of the player's shows: what the post reached, and never fewer than the residents the city counted seeing it (they saw it, so they viewed it).
        /// Null with no views.
        /// </summary>
        public static string Views(int reached, int seenInCity) => Math.Max(reached, seenInCity) > 0 ? Count(Math.Max(reached, seenInCity)) : null;

        /// <summary>What a post or an ad did in the city: how many saw it and came, or how many it put off.</summary>
        public static string FunnelSaid(int seen, int came, bool off, UiWords t) => off
            ? t("reallife_flare_kit_funnel_bad", "Seen by {seen} and it put {kept} off").Replace("{seen}", Count(seen)).Replace("{kept}", N(Math.Max(0, -came)))
            : t("reallife_flare_kit_funnel_good", "Seen by {seen} and {came} came").Replace("{seen}", Count(seen)).Replace("{came}", N(came));

        /// <summary>The second line of a post's result: how many were interested and how many followed.</summary>
        public static string FunnelMore(int interested, int followed, UiWords t) =>
            t("reallife_flare_kit_funnel_more", "{interested} were interested and {followed} followed you").Replace("{interested}", Count(interested)).Replace("{followed}", Count(followed));
    }
}
