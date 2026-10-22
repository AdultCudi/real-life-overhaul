using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Flare
{
    /// <summary>The Flare app's Business screen: the player's businesses, the deals and the company's campaigns there.</summary>
    public sealed partial class FlareApp
    {
        private void WriteBusiness(FlareFile file)
        {
            List<BuildingRegistration> shops = FlareGame.Businesses();
            int[] parts = new int[4];
            for (int i = 0; i < shops.Count; i++)
            {
                FlareRow r = Row(CellShop);
                r.Key = FlareGame.Place(shops[i]);
                r.Text = FlareGame.Name(shops[i]);
                int overall;
                r.Number = FlareGame.Satisfaction(shops[i], out overall, parts) ? overall : -1;
            }
            if (shops.Count == 0)
            {
                Note(FlareText.T("reallife_flare_business_none"));
            }
            int day = _director.CurrentDay;
            bool title = false;
            for (int i = file.Hires.Count - 1; i >= 0; i--)
            {
                FlareHire h = file.Hires[i];
                if (!title)
                {
                    Section(FlareText.T("reallife_flare_deals_title"));
                    title = true;
                }
                FlareRow r = Row(CellDeal);
                r.Key = FlareText.Fill("reallife_flare_row_hire", "business", h.Name, "who", _director.WhoLine(h));
                r.Text = DealLines(file, h, day);
                r.Number = h.Ended ? 0 : h.Id;
            }
            List<FlareCampaign> campaigns = _director.Campaigns();
            for (int i = 0; i < campaigns.Count; i++)
            {
                FlareCampaign c = campaigns[i];
                if (FlareRules.IsDeal(file, c.Place, c.Kind, c.From))
                {
                    continue;
                }
                if (!title)
                {
                    Section(FlareText.T("reallife_flare_deals_title"));
                    title = true;
                }
                FlareRow r = Row(CellDeal);
                r.Key = FlareText.Fill("reallife_flare_row_campaign", "business", c.Name, "campaign", c.Campaign);
                r.Text = FlareText.Busy(c, day);
            }
            Note(FlareText.Fill("reallife_flare_business_talk", "most", FlareRules.BuzzMost.ToString(), "days", FlareRules.BuzzDays.ToString()));
        }

        private static string DealLines(FlareFile file, FlareHire h, int day)
        {
            var b = new StringBuilder(160);
            b.Append(FlareText.Pair("reallife_flare_hire_deal", FlareText.Deal(h.Kind - FlareCast.FirstDealKind)));
            b.Append('\n').Append(FlareText.Fill("reallife_flare_hire_days", "from", h.From.ToString(), "to", h.To.ToString(), "percent", h.Percent.ToString()));
            // Its posts: when the first comes, how many have come and when the last did, and where they are.
            string posts = FlareCast.HirePostsKey(h, day);
            if (posts != null)
            {
                b.Append('\n').Append(FlareText.Fill(posts, "day", (h.Posted > 0 ? h.LastPost : h.From).ToString(), "count", h.Posted.ToString()));
            }
            b.Append('\n').Append(FlareText.T("reallife_flare_hire_posts_where"));
            if (h.Ended)
            {
                b.Append('\n').Append(FlareText.T("reallife_flare_hire_ended"));
            }
            if (h.Late)
            {
                b.Append('\n').Append(FlareText.T(day >= h.From ? "reallife_flare_hire_late" : "reallife_flare_hire_unknown"));
            }
            if (h.OffMessage && day >= h.From)
            {
                b.Append('\n').Append(FlareText.T("reallife_flare_hire_off"));
            }
            if (h.Backfired && day >= h.From)
            {
                b.Append('\n').Append(FlareText.T("reallife_flare_hire_backfired"));
            }
            for (int i = 0; i < h.Who.Count; i++)
            {
                FlareInfluencer inf = file.Influencer(h.Who[i]);
                if (inf != null && inf.ScandalDay >= h.From && inf.ScandalDay <= h.To)
                {
                    b.Append('\n').Append(FlareText.T("reallife_flare_hire_scandal"));
                    break;
                }
            }
            return b.ToString();
        }

        private string ShopLines(FlareRow row)
        {
            FlareFile file = _director.File;
            if (file == null)
            {
                return string.Empty;
            }
            int day = _director.CurrentDay;
            int talk = file.BuzzAt(row.Key, day), own, last, reviews = 0, stars = 0;
            int posts = FlareCast.PostsAbout(file, row.Key, out own, out last);   // the player's own and the paid posts of the deals there
            long now = _director.Now;
            for (int k = 0; k < file.Posts.Count; k++)
            {
                FlarePost p = file.Posts[k];
                if (p.Place == row.Key && p.Source == FlareSource.Review && p.At <= now)   // the reviews that went up, not those of visits still to come
                {
                    reviews++;
                    stars += p.Arg;
                }
            }
            string text = (talk != 0 ? FlareText.Fill("reallife_flare_shop_talk", "percent", FlareText.Signed(talk)) : FlareText.T("reallife_flare_shop_quiet"))
                          + "\n" + FlareText.Fill("reallife_flare_shop_posts_line", "posts", posts.ToString(), "last",
                                                 last >= 0 ? FlareText.Fill("reallife_flare_day", "day", last.ToString()) : FlareText.T("reallife_flare_never"));
            if (posts > own)
            {
                text += "\n" + FlareText.Pair("reallife_flare_shop_paid_posts", (posts - own).ToString());   // the posts of the influencers hired for it
            }
            if (reviews > 0)
            {
                text += "\n" + FlareText.Fill("reallife_flare_shop_reviews", "count", reviews.ToString(), "stars", (stars / (float)reviews).ToString("0.0"));
            }
            FlareCampaign campaign;
            if (_director.CampaignAt(row.Key, out campaign))
            {
                text += "\n" + FlareText.Busy(campaign, day);
            }
            int reach = FlareRules.SponsoredReach(file.Followers);
            text += "\n" + (reach > 0 ? FlareText.Fill("reallife_flare_shop_sponsored", "share", (reach / 10).ToString())
                : FlareText.Fill("reallife_flare_shop_sponsored_later", "followers", FlareText.N(FlareRules.SponsoredFrom)));
            return text;
        }
    }
}
