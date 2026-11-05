using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>The screens of Flare in the city: the account in the city, a business's page, an ad, one post's funnel.</summary>
    public enum FlareViewKind : byte
    {
        Account = 0,
        Business = 1,
        Ad = 2,
        Funnel = 3,
        Offers = 4,
        Trends = 5
    }

    /// <summary>How a figure is written (the renderer uses the game's own number and money formatting).</summary>
    public enum FlareViewFormat : byte
    {
        Count = 0,
        Money = 1,       // whole money
        Permille = 2,    // a share in thousandths, written as a per cent with its sign
        Key = 3,         // a locale key, written in the player's language
        Stars = 4,       // tenths of a star, out of five
        Signed = 5       // a count with its sign
    }

    /// <summary>One figure: its label's locale key and its value.</summary>
    public struct FlareViewFigure
    {
        public string Label;
        public long Value;
        public FlareViewFormat Format;
        public string Text;          // for a Key figure: the key written

        public static FlareViewFigure Of(string label, long value, FlareViewFormat format) => new FlareViewFigure { Label = label, Value = value, Format = format };
    }

    /// <summary>One row of a screen's list: who or what it is, one figure, and where it leads.</summary>
    public sealed class FlareViewRow
    {
        public string Title;         // a locale key when TitleKey, else a name the renderer writes as it is
        public bool TitleKey;
        public int Person;           // a resident's id: the renderer names them (FlareLiveRules.ResidentName); 0 for none
        public bool Female;
        public string Subtitle;      // a locale key, or null
        public long Value;
        public FlareViewFormat Format;
        public long Second;          // a second figure (a funnel's takings beside its visits), or 0
        public string Target;        // where it leads: a business's place, or null
        public int Post = -1;        // ...or a post's number
        public string What;          // a trend: the locale key of what trends (a kind of goods, a genre), written into the title's words
        public string Where;         // ...and its district by the game's id, or null for the whole city
        public int Day = -1;         // the day it went up (a post's, an ad's), or -1
        public int Seen;             // residents who saw it
    }

    /// <summary>
    /// One screen of Flare in the city as the design system draws it (not-in-repo/design/DESIGN_SPEC.md; the owner, 1 Nov 2026: "absolutely beautiful
    /// ... not cluttered"): a title, a few figures at the top, one list with its header, a few details, one primary action and the words for an empty
    /// list. Only what the page shows: no colours, no layout, nothing of the game's. Game-free.
    /// </summary>
    public sealed class FlareCityView
    {
        public FlareViewKind Kind;
        public string Title;
        public bool TitleKey;
        public string Subtitle;
        public readonly List<FlareViewFigure> Hero = new List<FlareViewFigure>(4);
        public string RowsHeader;
        public readonly List<FlareViewRow> Rows = new List<FlareViewRow>(FlareCityViews.RowsMost);
        public readonly List<FlareViewFigure> Details = new List<FlareViewFigure>(6);
        public string Primary;       // the primary action's label key, or null
        public string PrimaryTarget; // its business's place, or null
        public string Empty;         // the words for an empty list
        public int Today = -1;       // the day it was worked out, so its entries can say how long ago they went up, or -1 when unknown
    }

    /// <summary>
    /// Flare in the city's screens worked out from its records, when a screen opens, never per frame: the account in the city (its followers in the
    /// city and beyond, and what each recent post and ad did, seen to spent), a business's page (its followers, rating, reviews by its customers,
    /// the pull Flare gives it and what that brought yesterday, its ads), an ad (its target, reach and price) and one post's funnel. Game-free
    /// (EconomyCheck's FlareResidentChecks builds every one).
    /// </summary>
    public static class FlareCityViews
    {
        public const int RowsMost = 6;   // rows a list shows: a screen keeps to a few things
        private const string K = "reallife" + "_flare_city_";   // the words' keys (split, so the locale check does not take a prefix for a key)

        /// <summary>The account in the city: followers here and beyond, and what the newest posts and ads did, each with the day it went up and who saw it.</summary>
        public static FlareCityView Account(FlareCityState c, int followers, Func<string, string> nameOf)
        {
            var v = new FlareCityView { Kind = FlareViewKind.Account, Title = K + "title_account", TitleKey = true };
            int city = Math.Min(c.CityFollowers, Math.Max(0, followers));
            v.Hero.Add(FlareViewFigure.Of(K + "fig_city", city, FlareViewFormat.Count));
            v.Hero.Add(FlareViewFigure.Of(K + "fig_beyond", Math.Max(0, followers - city), FlareViewFormat.Count));
            v.RowsHeader = K + "rows_funnel";
            for (int i = c.Funnels.Count - 1; i >= 0 && v.Rows.Count < RowsMost; i--)
            {
                FlareFunnel f = c.Funnels[i];
                if (f.Kind != FunnelKind.Post && f.Kind != FunnelKind.Ad)
                {
                    continue;
                }
                v.Rows.Add(new FlareViewRow
                {
                    Title = f.Place != null && nameOf != null ? nameOf(f.Place) : K + (f.Kind == FunnelKind.Ad ? "row_ad" : "row_post"), TitleKey = f.Place == null || nameOf == null,
                    Subtitle = K + (f.Kind == FunnelKind.Ad ? "row_ad" : "row_post"), Value = f.Came, Format = FlareViewFormat.Signed, Second = f.Spent / 100, Target = f.Place,
                    Post = f.Post, Day = f.Day, Seen = f.Seen
                });
            }
            v.Empty = K + "empty_funnel";
            return v;
        }

        /// <summary>
        /// A business's page: its followers in the city, its rating by customers and the pull Flare gives it, its newest reviews (each by the resident
        /// who wrote it), what the residents heard of it and what Flare's attention brought yesterday; the player's own may run an ad.
        /// </summary>
        public static FlareCityView Business(FlareCityState c, PopulationState s, int[] personSlot, int b, string name, string place, bool mine)
        {
            var v = new FlareCityView { Kind = FlareViewKind.Business, Title = name, TitleKey = false, Subtitle = K + (mine ? "sub_mine" : "sub_city") };
            bool known = b >= 0 && b < c.BusinessCount;
            v.Hero.Add(FlareViewFigure.Of(K + "fig_followers", known ? c.Followers[b] : 0, FlareViewFormat.Count));
            v.Hero.Add(FlareViewFigure.Of(K + "fig_rating", known ? c.Rating(b) : 0, FlareViewFormat.Stars));
            v.Hero.Add(FlareViewFigure.Of(K + "fig_pull", known ? c.Attention[b] : 0, FlareViewFormat.Permille));
            v.RowsHeader = K + "rows_reviews";
            if (known)
            {
                var newest = new FlareCityReview[RowsMost];
                int n = FlareCityRules.NewestReviews(c, b, newest);
                for (int i = 0; i < n; i++)
                {
                    v.Rows.Add(new FlareViewRow
                    {
                        Person = newest[i].Person, Female = FemaleOf(s, personSlot, newest[i].Person), Value = newest[i].Stars * 10, Format = FlareViewFormat.Stars,
                        Subtitle = K + "row_review"
                    });
                }
                FlareCityRules.Talk(c, b, out long heard, out long sour);
                v.Details.Add(FlareViewFigure.Of(K + "detail_heard", heard / 1000, FlareViewFormat.Count));
                v.Details.Add(FlareViewFigure.Of(K + "detail_sour", sour / 1000, FlareViewFormat.Count));
                v.Details.Add(FlareViewFigure.Of(K + "detail_came", c.LiftVisits[b], FlareViewFormat.Signed));
                v.Details.Add(FlareViewFigure.Of(K + "detail_spent", c.Lift[b] / 100, FlareViewFormat.Money));
                v.Details.Add(FlareViewFigure.Of(K + "detail_reviews", c.ReviewCount[b], FlareViewFormat.Count));
            }
            if (mine)
            {
                v.Primary = K + "action_ad";
                v.PrimaryTarget = place;
            }
            v.Empty = K + "empty_reviews";
            return v;
        }

        /// <summary>An ad as the player sets it up: its target, reach a day and days, the residents its target holds, and its price, or why it cannot run.</summary>
        public static FlareCityView Ad(FlareAd ad, int targetKind, int target, long cost, string why)
        {
            var v = new FlareCityView { Kind = FlareViewKind.Ad, Title = ad.Name, TitleKey = false, Subtitle = K + "title_ad" };
            v.Hero.Add(FlareViewFigure.Of(K + "fig_reach", ad.Reach, FlareViewFormat.Count));
            v.Hero.Add(FlareViewFigure.Of(K + "fig_days", ad.Days, FlareViewFormat.Count));
            v.Hero.Add(FlareViewFigure.Of(K + "fig_cost", cost / 100, FlareViewFormat.Money));
            v.Details.Add(new FlareViewFigure { Label = K + "detail_target", Format = FlareViewFormat.Key, Text = TargetKey(targetKind) });
            v.Details.Add(FlareViewFigure.Of(K + "detail_holds", target, FlareViewFormat.Count));
            v.Primary = why == null ? K + "action_run" : null;
            v.PrimaryTarget = ad.Place;
            v.Empty = why;   // why it cannot run, in words; null when it can
            return v;
        }

        /// <summary>One post's or ad's funnel: seen, interested, followed, came and spent.</summary>
        public static FlareCityView Funnel(FlareFunnel f, string title)
        {
            var v = new FlareCityView { Kind = FlareViewKind.Funnel, Title = title, TitleKey = false, Subtitle = K + (f.Kind == FunnelKind.Ad ? "row_ad" : "row_post") };
            v.Hero.Add(FlareViewFigure.Of(K + "fig_seen", f.Seen, FlareViewFormat.Count));
            v.Hero.Add(FlareViewFigure.Of(K + "fig_interested", f.Interested, FlareViewFormat.Count));
            v.Hero.Add(FlareViewFigure.Of(K + "fig_came", f.Came, FlareViewFormat.Signed));
            v.Hero.Add(FlareViewFigure.Of(K + "fig_spent", f.Spent / 100, FlareViewFormat.Money));
            v.Details.Add(FlareViewFigure.Of(K + "fig_followed", f.Followed, FlareViewFormat.Count));
            v.Empty = K + "empty_funnel";
            return v;
        }

        internal static bool FemaleOf(PopulationState s, int[] personSlot, int person)
        {
            int p = s != null ? FlareCityRules.Slot(personSlot, s, person) : -1;
            return p >= 0 && s.People.Female[p] != 0;
        }

        /// <summary>
        /// The city's businesses' offers of paid posts, newest first: each business, its fee a post and its posts, and where it stands (open, taken, done,
        /// not taken); the open one can be taken.
        /// </summary>
        public static FlareCityView Offers(FlareCityState c, int day)
        {
            var v = new FlareCityView { Kind = FlareViewKind.Offers, Title = K + "title_offers", TitleKey = true, RowsHeader = K + "rows_offers", Empty = K + "empty_offers" };
            for (int i = c.Offers.Count - 1; i >= 0 && v.Rows.Count < RowsMost; i--)
            {
                FlareOffer o = c.Offers[i];
                string state = o.Open(day) ? "offer_open" : o.Running ? "offer_running" : o.Accepted ? "offer_done" : "offer_lapsed";
                v.Rows.Add(new FlareViewRow { Title = o.Name, Value = o.Fee / 100, Format = FlareViewFormat.Money, Second = o.Posts, Post = o.Id, Subtitle = K + state });
                if (state == "offer_open")
                {
                    v.Primary = K + "action_accept";
                }
            }
            return v;
        }

        /// <summary>What is trending among the residents, newest first: what and where, how far along, how many it moved and the days left.</summary>
        public static FlareCityView Trends(IList<FlareTrendSeen> trends, int day)
        {
            var v = new FlareCityView { Kind = FlareViewKind.Trends, Title = K + "trend_title", TitleKey = true, Empty = K + "trend_empty" };
            for (int i = 0; trends != null && i < trends.Count && v.Rows.Count < RowsMost; i++)
            {
                FlareTrendSeen t = trends[i];
                int age = day - t.Day;
                string stage = age < FlareCityCatalogue.TrendRiseDays ? "trend_rising" : age < FlareCityCatalogue.TrendDays ? "trend_height" : "trend_fading";
                v.Rows.Add(new FlareViewRow
                {
                    Title = K + (t.Kind == TrendKind.Genre ? "trend_genre" : t.District != null ? "trend_district" : "trend_goods"), TitleKey = true,
                    What = t.Kind == TrendKind.Genre ? Tech.StudioViews.GenreKey(t.What) : TrendKindKey(t.What), Where = t.Kind == TrendKind.Genre ? null : t.District,
                    Subtitle = K + stage, Value = t.Moved, Format = FlareViewFormat.Count, Second = FlareCityRules.DaysLeft(t.Day, day)
                });
            }
            return v;
        }

        /// <summary>The words of a kind of goods or going out that may trend (FlareCityRules.TrendGoods, TrendGoingOut).</summary>
        public static string TrendKindKey(int kind)
        {
            return K + "trend_kind_" + Math.Max(0, Math.Min(kind, FlareCityRules.Kinds - 1)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // The tag a kind of goods or going out is posted under, one word in English whatever the language: the same on every machine of a session.
        private static readonly string[] KindTags =
        {
            null, null, null, "eatingout", "coffee", "fashion", "gadgets", "furniture", "booksandgifts", "flowers", "jewellery", "workingout", "cinema", "theatre",
            "nightlife", "casinos", null, "hairandbeauty"
        };

        /// <summary>
        /// The tag a trend is promoted under, from the trend's own key (<see cref="TrendKindKey"/> or a genre's), never from its words in the player's
        /// language; null for a key it does not know.
        /// </summary>
        public static string TrendTag(string whatKey)
        {
            if (string.IsNullOrEmpty(whatKey))
            {
                return null;
            }
            const string kindStart = K + "trend_kind_";
            if (whatKey.StartsWith(kindStart, StringComparison.Ordinal))
            {
                return int.TryParse(whatKey.Substring(kindStart.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int kind)
                       && kind >= 0 && kind < KindTags.Length ? KindTags[kind] : null;
            }
            int at = whatKey.IndexOf("_genre_", StringComparison.Ordinal);
            return at >= 0 && at + 7 < whatKey.Length ? whatKey.Substring(at + 7).ToLowerInvariant() : null;
        }

        /// <summary>The words of an ad's target, by its number (FlareDirector.AdOf's targets).</summary>
        public static string TargetKey(int target)
        {
            return K + "target_" + Math.Max(0, Math.Min(target, FlareCityCatalogue.AdTargets - 1)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Every locale key the screens use (EconomyCheck checks each is in the three locales).</summary>
        public static readonly string[] Keys =
        {
            K + "title_account", K + "fig_city", K + "fig_beyond", K + "rows_funnel", K + "row_post", K + "row_ad", K + "empty_funnel",
            K + "sub_mine", K + "sub_city", K + "fig_followers", K + "fig_rating", K + "fig_pull", K + "rows_reviews", K + "row_review", K + "detail_heard", K + "detail_sour",
            K + "detail_came", K + "detail_spent", K + "detail_reviews", K + "action_ad", K + "empty_reviews",
            K + "title_ad", K + "fig_reach", K + "fig_days", K + "fig_cost", K + "detail_target", K + "detail_holds", K + "action_run",
            K + "target_0", K + "target_1", K + "target_2", K + "target_3", K + "target_4",
            K + "fig_seen", K + "fig_interested", K + "fig_came", K + "fig_spent", K + "fig_followed",
            K + "title_offers", K + "rows_offers", K + "empty_offers", K + "offer_open", K + "offer_running", K + "offer_done", K + "offer_lapsed", K + "action_accept",
            K + "trend_title", K + "trend_empty", K + "trend_rising", K + "trend_height", K + "trend_fading", K + "trend_goods", K + "trend_district", K + "trend_genre"
        };
    }
}
