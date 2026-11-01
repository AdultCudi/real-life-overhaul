using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The Flare app's lines for Flare in the city, drawn from its screens' view models (FlareCityViews) until the interface kit draws those itself:
    /// a business card's page as the residents see it, the account's followers in the city and what its posts did, and the sheet that runs an ad.
    /// Nothing per frame: written when the app writes a screen.
    /// </summary>
    public sealed partial class FlareApp
    {
        private string _adPlace, _adName;
        private int _adTarget, _adSize, _adDays;

        /// <summary>True while the residents' side runs here, or a guest follows the host's: the cards show its lines.</summary>
        internal bool CityShown => _director != null && (_director.CityOn || _director.CityFollowing);

        /// <summary>True while the residents' side runs here: a card offers an ad (a guest's residents are the host's).</summary>
        internal bool CityAds => _director != null && _director.CityOn;

        // A business card's lines from its page: followers, rating, the pull Flare gives it and what that brought, the newest review.
        private string CityShopLines(FlareFile file, FlareRow row)
        {
            FlareCityView v = _director.CityBusinessView(row.Key, row.Text);
            if (v == null)
            {
                FlareCityFigures f = _director.FollowedFigures(row.Key);
                return f != null ? FollowedLines(f) : FlareText.T("reallife_flare_city_business_note");
            }
            long followers = Hero(v, 0), rating = Hero(v, 1), pull = Hero(v, 2);
            long reviews = Detail(v, 4), came = Detail(v, 2), spent = Detail(v, 3);
            var b = new StringBuilder(240);
            b.Append(reviews > 0
                ? FlareText.Fill("reallife_flare_city_shop_follow", "followers", FlareText.N((int)followers), "rating", Stars(rating), "reviews", FlareText.N((int)reviews))
                : FlareText.Fill("reallife_flare_city_shop_follow_none", "followers", FlareText.N((int)followers)));
            b.Append('\n').Append(FlareText.Fill("reallife_flare_city_shop_pull", "pull", FlareText.Signed((int)(pull / 10)) + "%", "came", FlareText.Signed((int)came),
                "spent", FlareText.Money((int)spent)));
            if (v.Rows.Count > 0)
            {
                FlareViewRow r = v.Rows[0];
                b.Append('\n').Append(FlareText.Fill("reallife_flare_city_shop_review", "name", _director.ResidentName(r.Person, r.Female),
                    "stars", (r.Value / 10).ToString(CultureInfo.InvariantCulture)));
            }
            int ads = _director.AdsRunning(row.Key);
            if (ads > 0)
            {
                b.Append('\n').Append(FlareText.Fill("reallife_flare_city_shop_ads", "count", ads.ToString(CultureInfo.InvariantCulture)));
            }
            return b.ToString();
        }

        // The account in the city on the player's own profile: followers here and beyond, and the newest measured posts.
        private void WriteCityAccount(FlareFile file)
        {
            FlareCityView v = CityShown ? _director.CityAccountView() : null;
            if (v == null)
            {
                return;
            }
            Section(FlareText.T(v.Title));
            var b = new StringBuilder(320);
            b.Append(FlareText.Fill("reallife_flare_city_account", "city", FlareText.N((int)Hero(v, 0)), "beyond", FlareText.N((int)Hero(v, 1))));
            for (int i = 0; i < v.Rows.Count && i < 3; i++)
            {
                FlareViewRow r = v.Rows[i];
                FlareFunnel f = _director.FunnelOf(r);
                if (f == null)
                {
                    continue;
                }
                string what = r.TitleKey ? FlareText.T(r.Title) : r.Title;
                b.Append('\n').Append(FlareText.Fill("reallife_flare_city_account_line", "what", what, "seen", FlareText.N(f.Seen), "interested", FlareText.N(f.Interested),
                    "came", FlareText.Signed(f.Came), "spent", FlareText.Money((int)(f.Spent / 100))));
            }
            if (v.Rows.Count == 0)
            {
                b.Append('\n').Append(FlareText.T(v.Empty));
            }
            Note(b.ToString());
        }

        // What is trending among the residents, at the top of Explore: each trend, how far along, how many it moved and the days left.
        private bool WriteCityTrends()
        {
            FlareCityView v = CityShown ? _director.CityTrendsView() : null;
            if (v == null)
            {
                return false;
            }
            Section(FlareText.T(v.Title));
            var b = new StringBuilder(240);
            foreach (FlareViewRow r in v.Rows)
            {
                string trend = FlareText.Fill(r.Title, "what", FlareText.T(r.What), "district", r.Where != null ? FlareGame.DistrictName(r.Where) : string.Empty);
                b.Append(b.Length > 0 ? "\n" : string.Empty).Append(FlareText.Fill("reallife_flare_city_trend_line", "trend", trend, "stage", FlareText.T(r.Subtitle),
                    "moved", FlareText.N((int)r.Value), "days", FlareText.N((int)r.Second)));
            }
            Note(v.Rows.Count > 0 ? b.ToString() : FlareText.T(v.Empty));
            return true;
        }

        // A guest's business card from the host's figures.
        private string FollowedLines(FlareCityFigures f)
        {
            var b = new StringBuilder(240);
            b.Append(f.Reviews > 0
                ? FlareText.Fill("reallife_flare_city_shop_follow", "followers", FlareText.N(f.Followers), "rating", Stars(f.Rating), "reviews", FlareText.N(f.Reviews))
                : FlareText.Fill("reallife_flare_city_shop_follow_none", "followers", FlareText.N(f.Followers)));
            b.Append('\n').Append(FlareText.Fill("reallife_flare_city_shop_pull", "pull", FlareText.Signed(f.Attention / 10) + "%", "came", FlareText.Signed(f.Came),
                "spent", FlareText.Money((int)(f.Spent / 100))));
            if (f.Reviewer > 0)
            {
                b.Append('\n').Append(FlareText.Fill("reallife_flare_city_shop_review", "name", _director.ResidentName(f.Reviewer, f.ReviewerFemale),
                    "stars", f.Stars.ToString(CultureInfo.InvariantCulture)));
            }
            return b.ToString();
        }

        // The city's businesses' offers of paid posts on the Business tab: the open one with its button, the rest with where they stand.
        private void WriteOffers()
        {
            FlareCityView v = CityAds ? _director.CityOffersView() : null;
            if (v == null)
            {
                return;
            }
            Section(FlareText.T(v.RowsHeader));
            if (v.Rows.Count == 0)
            {
                Note(FlareText.T(v.Empty));
                return;
            }
            foreach (FlareViewRow row in v.Rows)
            {
                FlareOffer o = _director.OfferOf(row.Post);
                if (o == null)
                {
                    continue;
                }
                bool open = row.Subtitle.EndsWith("offer_open", System.StringComparison.Ordinal);
                FlareRow r = Row(CellDeal);
                r.Key = FlareText.Fill("reallife_flare_offer_row", "business", row.Title, "fee", FlareText.Money((int)row.Value));
                r.Text = OfferLine(o, open);
                r.Number = open ? -o.Id : 0;
            }
        }

        private static string OfferLine(FlareOffer o, bool open)
        {
            string paid = FlareText.Money((int)(o.Fee * o.Done / 100));
            if (open)
            {
                return o.Posts == 1 ? FlareText.Fill("reallife_flare_offer_one", "until", o.Until.ToString(CultureInfo.InvariantCulture))
                    : FlareText.Fill("reallife_flare_offer_sponsor", "posts", o.Posts.ToString(CultureInfo.InvariantCulture), "gap",
                        FlareCityRules.SponsorGap.ToString(CultureInfo.InvariantCulture), "until", o.Until.ToString(CultureInfo.InvariantCulture));
            }
            if (o.Running)
            {
                return FlareText.Fill("reallife_flare_offer_running", "done", o.Done.ToString(CultureInfo.InvariantCulture), "posts", o.Posts.ToString(CultureInfo.InvariantCulture), "paid", paid);
            }
            return o.Accepted ? FlareText.Fill("reallife_flare_offer_done", "posts", o.Posts.ToString(CultureInfo.InvariantCulture), "paid", paid) : FlareText.T("reallife_flare_offer_lapsed");
        }

        private static long Hero(FlareCityView v, int i) => i < v.Hero.Count ? v.Hero[i].Value : 0;

        private static long Detail(FlareCityView v, int i) => i < v.Details.Count ? v.Details[i].Value : 0;

        private static string Stars(long tenths) => (tenths / 10).ToString(CultureInfo.InvariantCulture) + "." + (tenths % 10).ToString(CultureInfo.InvariantCulture);

        // ---------------------------------------------------------------- the ad sheet

        internal void AdClicked(FlareRow row)
        {
            if (row?.Key == null || !_built || _director.File == null || !CityShown)
            {
                return;
            }
            _adPlace = row.Key;
            _adName = row.Text;
            _adTarget = 0;
            _adSize = 1;
            _adDays = 1;
            _sheetKind = SheetAd;
            ShowSheet();
        }

        private float WriteAd(float width)
        {
            FlareKit.Set(_sheetTitle, FlareText.Fill("reallife_flare_ad_title", "business", _adName));
            FlareKit.Show(_field, false);
            FlareKit.Show(_counter, false);
            FlareKit.Show(_picBack, false);
            FlareKit.Show(_camera, false);
            FlareKit.Show(_selfie, false);
            FlareKit.Show(_latest, false);
            float y = 0f, x = 0f;
            y = Label(FlareText.T("reallife_flare_ad_label_target"), y, width);
            for (int k = 0; k < FlareCityCatalogue.AdTargets; k++)
            {
                int n = k;
                Chip(FlareText.T(FlareCityViews.TargetKey(k)), _adTarget == k, () =>
                {
                    _adTarget = n;
                    WriteSheet();
                }, ref x, ref y, width);
            }
            y += ChipHeight + 16f;
            x = 0f;
            y = Label(FlareText.T("reallife_flare_ad_label_size"), y, width);
            for (int k = 0; k < FlareCityCatalogue.AdReach.Length; k++)
            {
                int n = k;
                Chip(FlareText.N(FlareCityCatalogue.AdReach[k]), _adSize == k, () =>
                {
                    _adSize = n;
                    WriteSheet();
                }, ref x, ref y, width);
            }
            y += ChipHeight + 16f;
            x = 0f;
            y = Label(FlareText.T("reallife_flare_ad_label_days"), y, width);
            for (int k = 0; k < FlareCityCatalogue.AdDays.Length; k++)
            {
                int n = k;
                Chip(FlareCityCatalogue.AdDays[k].ToString(CultureInfo.InvariantCulture), _adDays == k, () =>
                {
                    _adDays = n;
                    WriteSheet();
                }, ref x, ref y, width);
            }
            y += ChipHeight + 16f;
            FlareAd ad = _director.AdOf(_adPlace, _adTarget, _adSize, _adDays);
            bool can = _director.AdQuote(ad, out long cost, out int target, out string why);
            FlareCityView v = FlareCityViews.Ad(ad, _adTarget, target, cost, can ? null : why);
            string quote = can
                ? FlareText.Fill("reallife_flare_ad_quote", "reach", FlareText.N(System.Math.Min(ad.Reach, target)), "days", ad.Days.ToString(CultureInfo.InvariantCulture),
                    "holds", FlareText.N(target), "cost", FlareText.Money((int)(cost / 100)))
                : FlareText.T(v.Empty ?? "reallife_flare_ad_why_off");
            FlareKit.Show(_preview, true);
            float ph = FlareKit.Height(_preview, quote, width);
            FlareKit.Set(_preview, quote);
            FlareKit.Place(_preview.rectTransform, 0f, y, width, ph + 4f);
            y += ph + 16f;
            FlareKit.Set(_why, can ? string.Empty : FlareText.T("reallife_flare_ad_cannot"));
            _why.color = _look.Bad;
            FlareKit.Set(_sendLabel, FlareText.T(v.Primary ?? "reallife_flare_city_action_run"));
            _send.color = can ? _look.Accent : _look.Chip;
            return y;
        }

        private void AdSend()
        {
            FlareAd ad = _director.AdOf(_adPlace, _adTarget, _adSize, _adDays);
            if (_director.AdBuy(ad, out string why))
            {
                CloseSheet();
                Open(FlareScreen.Business, 0, -1, null, true);
            }
            else
            {
                WriteSheet();
            }
        }
    }
}
