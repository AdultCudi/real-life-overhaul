using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// The Brands tab. The table (BrandsTables.Brands): each brand, and each district it has a figure for. The box: for a brand,
    /// one of three pages chosen with the selector at the top (its figures explained, its settings, its campaigns);
    /// for a district, what its recognition is made of today.
    /// </summary>
    public sealed partial class BrandsApp
    {
        private static readonly string[] BrandPageKeys = { "reallife_brand_page_figures", "reallife_brand_page_settings", "reallife_brand_page_campaigns" };

        private readonly List<string> _pushDistricts = new List<string>(8);
        private int _brandPage;      // which of a brand's three pages is showing
        private int _pushKind;       // the campaign whose quote is showing
        private int _pushDistrict;   // and the district for a local one

        private TableSpec WriteBrands(InfoBox box, BrandsState state, int day, ref string selected)
        {
            Brand picked = null;
            DistrictFile district = null;
            Brand districtOf = null;
            for (int i = 0; i < state.Brands.Count; i++)
            {
                Brand b = state.Brands[i];
                string type = BrandsText.Type(b.Type);
                int best = BrandRules.Best(b);
                int tone = b.Standards < BrandCatalogue.LowStandards ? TableRow.Red : b.Minimum > 0 && b.Standards < b.Minimum ? TableRow.Yellow : TableRow.Plain;
                TableRow row = Row("brand:" + b.Id);
                row.Kind = BrandsTables.IsBrand;
                row.Set(0, b.Name);
                row.Set(1, type);
                WriteBrandCells(row, BrandRules.Shops(state, b, null), best, b.City, b.Standards, tone, BrandRules.RecFootfall(best, b.Standards));
                picked = Is(row, ref selected) ? b : picked;
                for (int k = 0; k < b.Districts.Count; k++)
                {
                    DistrictFile d = b.Districts[k];
                    TableRow sub = Row("dist:" + b.Id + ":" + d.Name);
                    sub.Kind = BrandsTables.IsDistrict;
                    sub.Set(0, BrandsText.T("reallife_brand_row_district", "brand", b.Name, "district", BrandsText.District(d.Name)));
                    sub.Set(1, type);
                    WriteBrandCells(sub, BrandRules.Shops(state, b, d.Name), d.Rec, b.City, b.Standards, tone, BrandRules.RecFootfall(BrandRules.Effective(b, d.Name), b.Standards));
                    if (Is(sub, ref selected))
                    {
                        district = d;
                        districtOf = b;
                    }
                }
            }
            if (state.Brands.Count == 0)
            {
                Say("none", BrandsText.T("reallife_brand_row_no_brand"));
            }
            if (picked != null)
            {
                WriteBrand(box, state, picked);
            }
            else if (district != null)
            {
                WriteDistrict(box, state, districtOf, district);
            }
            else
            {
                box.Title(BrandsText.T("reallife_brand_app"));
                box.Line(BrandsText.T("reallife_brand_intro_what"));
                box.Line(BrandsText.T("reallife_brand_intro_effect", "most", BrandCatalogue.RecFootfallMost.ToString(), "cap", BrandCatalogue.FootfallMost.ToString()));
                box.Line(BrandsText.T("reallife_brand_intro_franchise"));
                box.Line(BrandsText.T("reallife_brand_intro_start"));
            }
            return BrandsTables.Brands;
        }

        // The five figures a brand's row and a district's row share (columns 2 to 6 of BrandsTables.Brands).
        private static void WriteBrandCells(TableRow row, int shops, int rec, int city, int standards, int tone, int worth)
        {
            row.Set(2, shops.ToString(), shops);
            row.Set(3, BrandsText.Points(rec), rec / 100.0);
            row.Set(4, BrandsText.Points(city), city / 100.0);
            row.Set(5, Standards(standards), standards, tone);
            row.Set(6, BrandsText.Percent(worth), worth);
        }

        private void WriteBrand(InfoBox box, BrandsState state, Brand b)
        {
            box.Title(b.Name);
            Choose(box, BrandPageKeys.Length, _brandPage, i => BrandsText.T(BrandPageKeys[i]), i => _brandPage = i);
            // (the page's own main button, if it has one, stands beside Back in the last row)
            string mainKey = null;
            Action main = null;
            switch (BrandCatalogue.Clamp(_brandPage, 0, BrandPageKeys.Length - 1))
            {
                case 1:
                    WriteBrandSettings(box, state, b, out mainKey, out main);
                    break;
                case 2:
                    WriteBrandCampaigns(box, state, b, out mainKey, out main);
                    break;
                default:
                    WriteBrandFigures(box, state, b);
                    break;
            }
            if (b.Bid > 0)
            {
                Franchise bidder = state.Franchise(b.BidBy);
                box.Line(BrandsText.T("reallife_brand_bid", "name", bidder != null ? bidder.Name : "?", "amount", BrandsText.Money(b.Bid), "day", b.BidUntil.ToString()));
                var answers = box.ButtonRow();
                int by = b.BidBy;
                string name = b.Name;
                string amount = BrandsText.Money(b.Bid);
                InfoBox.Button(answers, InfoBox.Positive, "reallife_brand_bid_sell", () => Confirm("reallife_brand_confirm_sell_title", "reallife_brand_confirm_sell", new { brand = name, amount }, () => _director.Agree(by), true));
                InfoBox.Button(answers, InfoBox.Negative, "reallife_brand_bid_refuse", () => _director.Decline(by));
            }
            var last = box.ButtonRow();
            if (main != null)
            {
                InfoBox.Button(last, InfoBox.Positive, mainKey, main);
            }
            InfoBox.Button(last, InfoBox.Additional, "reallife_brand_back", Back);
        }

        // What the brand's two figures are, what they are made of and what moves them.
        private void WriteBrandFigures(InfoBox box, BrandsState state, Brand b)
        {
            int members = BrandRules.Members(state, b, null), franchised = BrandRules.Franchised(state, b, null);
            box.Line(BrandsText.Fill("reallife_brand_fig_shops", "type", BrandsText.Type(b.Type), "members", members.ToString(), "franchised", franchised.ToString(), "day", b.Founded.ToString()));
            box.Line(BrandsText.Fill("reallife_brand_fig_rec", "city", BrandsText.Points(b.City), "best", BrandsText.Points(BrandRules.Best(b)), "fade", BrandsText.Bp(BrandCatalogue.DecayBp),
                "empty", BrandsText.Bp(BrandCatalogue.DecayEmptyBp)));
            string worst = null;
            int low = 101;
            for (int i = 0; i < state.Members.Count; i++)
            {
                if (state.Members[i].BrandId == b.Id && state.Members[i].Standards < low)
                {
                    low = state.Members[i].Standards;
                    worst = _director.ShopName(state.Members[i].Place);
                }
            }
            box.Line(BrandsText.Fill("reallife_brand_fig_standards", "standards", Standards(b.Standards), "minimum", BrandsText.Minimum(b.Minimum)));
            box.Line(worst != null
                ? BrandsText.Fill("reallife_brand_fig_worst", "business", worst, "standards", Standards(low), "mean", BrandsText.Share(BrandCatalogue.BrandMeanShare), "worst", BrandsText.Share(BrandCatalogue.BrandWorstShare))
                : BrandsText.T("reallife_brand_fig_no_members"));
            box.Line(BrandsText.Fill(b.Standards < BrandCatalogue.LowStandards ? "reallife_brand_fig_effect_low" : "reallife_brand_fig_effect", "most", BrandCatalogue.RecFootfallMost.ToString(),
                "low", BrandCatalogue.LowStandards.ToString(), "tolerance", BrandRules.Tolerance(b.Standards).ToString(), "cap", BrandCatalogue.FootfallMost.ToString()));
            if (franchised > 0 || b.Earned > 0)
            {
                box.Line(BrandsText.Fill("reallife_brand_fig_money", "net", BrandsText.Money(b.Net), "earned", BrandsText.Money(b.Earned), "fund", BrandsText.Money(b.Fund), "failed", b.Failed.ToString()));
            }
        }

        // The minimum for standards, the franchise offer, shared prices and hours, the name, giving the brand up.
        private void WriteBrandSettings(InfoBox box, BrandsState state, Brand b, out string mainKey, out Action main)
        {
            int id = b.Id;
            int step = Math.Max(0, Array.IndexOf(BrandCatalogue.MinimumSteps, b.Minimum));
            box.Line(BrandsText.T("reallife_brand_set_minimum"));
            Choose(box, BrandCatalogue.MinimumSteps.Length, step, i => BrandsText.T("reallife_brand_set_minimum_value", "points", BrandsText.Minimum(BrandCatalogue.MinimumSteps[i])), i => _director.SetMinimum(id, i));
            int good;
            bool can = BrandRules.CanOffer(state, b, out good);
            box.Line(BrandsText.Fill(b.Offered ? "reallife_brand_set_offered" : can ? "reallife_brand_set_can_offer" : "reallife_brand_set_cannot_offer", "good", good.ToString(), "shops", BrandCatalogue.OfferShops.ToString(),
                "standards", BrandCatalogue.OfferStandards.ToString(), "city", BrandsText.Points(BrandCatalogue.OfferCityRec), "district", BrandsText.Points(BrandCatalogue.OfferDistrictRec),
                "has", BrandsText.Points(b.City), "best", BrandsText.Points(BrandRules.Best(b))));
            bool offered = b.Offered;
            if (!box.Switch("reallife_brand_set_offer", offered, on =>
                {
                    _director.Offer(id, on);
                    Refresh();   // (a brand that is not ready stays off offer: the switch is written again as it stands)
                }))
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, offered ? "reallife_brand_set_offer_off" : "reallife_brand_set_offer_on", () => _director.Offer(id, !offered));
            }
            string flagship = _director.ShopName(b.Flagship);
            box.Line(BrandsText.T("reallife_brand_set_shared", "business", flagship));
            string name = b.Name;
            var shared = box.ButtonRow();
            InfoBox.Button(shared, InfoBox.Positive, "reallife_brand_set_prices", () => Confirm("reallife_brand_confirm_prices_title", "reallife_brand_confirm_prices", new { brand = name, business = flagship }, () => _director.SharePrices(id), false));
            InfoBox.Button(shared, InfoBox.Additional, "reallife_brand_set_hours", () => Confirm("reallife_brand_confirm_hours_title", "reallife_brand_confirm_hours", new { brand = name, business = flagship }, () => _director.ShareHours(id), false));
            InfoBox.Button(shared, InfoBox.Negative, "reallife_brand_set_give_up", () => Confirm("reallife_brand_confirm_give_up_title", "reallife_brand_confirm_give_up", new { brand = name }, () =>
            {
                _pick = null;
                _director.GiveUp(id);
            }, true));
            mainKey = "reallife_brand_set_rename";
            main = () => _director.Rename(id);
        }

        // A campaign for the brand: the one that runs, or the two to choose from with today's cost.
        private void WriteBrandCampaigns(InfoBox box, BrandsState state, Brand b, out string mainKey, out Action main)
        {
            int id = b.Id;
            mainKey = null;
            main = null;
            box.Line(BrandsText.Fill("reallife_brand_push_about", "local", BrandsText.Lost(BrandCatalogue.CampaignLocalGain), "city", BrandsText.Lost(BrandCatalogue.CampaignCityGain)));
            box.Line(BrandsText.T("reallife_brand_push_fund", "fund", BrandsText.Money(b.Fund), "most", BrandsText.Money(CompanyCatalogue.AtPrices(BrandCatalogue.FundIdleMost, BrandsDirector.Index))));
            if (b.Push >= 0)
            {
                box.Line(BrandsText.Fill("reallife_brand_push_running", "campaign", BrandsText.Push(b.Push), "where", b.Push == BrandCatalogue.PushCity ? BrandsText.T("reallife_brand_push_everywhere") : BrandsText.District(b.PushDistrict),
                    "days", Math.Max(0, b.PushEnd - Math.Max(b.Age + 1, b.PushFrom)).ToString()));
                return;
            }
            _pushDistricts.Clear();
            for (int i = 0; i < b.Districts.Count; i++)
            {
                if (BrandRules.Shops(state, b, b.Districts[i].Name) > 0)
                {
                    _pushDistricts.Add(b.Districts[i].Name);
                }
            }
            int kind = _pushDistricts.Count == 0 ? BrandCatalogue.PushCity : BrandCatalogue.Clamp(_pushKind, 0, 1);
            Choose(box, 2, kind, BrandsText.Push, i => _pushKind = i);
            string district = null;
            if (kind == BrandCatalogue.PushLocal)
            {
                int at = BrandCatalogue.Clamp(_pushDistrict, 0, _pushDistricts.Count - 1);
                Choose(box, _pushDistricts.Count, at, i => BrandsText.District(_pushDistricts[i]), i => _pushDistrict = i);
                district = _pushDistricts[at];
            }
            int cost = _director.PushCost(b, kind, district);
            box.Line(BrandsText.Fill("reallife_brand_push_quote", "cost", BrandsText.Money(cost), "days", BrandRules.PushDays(kind).ToString(), "fund", BrandsText.Money(Math.Min(b.Fund, cost))));
            string where = district;
            int chosen = kind;
            mainKey = "reallife_brand_push_book";
            main = () => _director.BookPush(id, chosen, where);
        }

        // One district of a brand: what its recognition gains today and what it is worth to a shop there.
        private void WriteDistrict(InfoBox box, BrandsState state, Brand b, DistrictFile d)
        {
            int shops = BrandRules.Shops(state, b, d.Name), customers = BrandRules.Customers(state, b, d.Name);
            int campaign = !b.Pushing ? 0 : b.Push == BrandCatalogue.PushCity ? (shops > 0 ? BrandCatalogue.CampaignCityGain : 0) : d.Name == b.PushDistrict ? BrandCatalogue.CampaignLocalGain : 0;
            int gain = BrandRules.Gain(shops, customers, b.Standards, campaign);
            int next = BrandRules.Step(d.Rec, gain, shops > 0);
            int effective = BrandRules.Effective(b, d.Name);
            box.Title(BrandsText.T("reallife_brand_dist_title", "brand", b.Name, "district", BrandsText.District(d.Name)));
            box.Line(BrandsText.Fill("reallife_brand_dist_rec", "points", BrandsText.Lost(d.Rec), "next", BrandsText.Lost(next)));
            box.Line(shops > 0
                ? BrandsText.Fill("reallife_brand_dist_parts", "shops", shops.ToString(), "presence", BrandsText.Lost(Math.Min(shops, BrandCatalogue.PresenceShopsMost) * BrandCatalogue.PresencePerShop),
                    "customers", customers.ToString(), "footfall", BrandsText.Lost(BrandCatalogue.Clamp(customers, 0, BrandCatalogue.FootfallFull) * BrandCatalogue.FootfallGainMost / BrandCatalogue.FootfallFull),
                    "standards", Standards(b.Standards), "good", BrandsText.Lost((BrandCatalogue.Clamp(b.Standards, 0, 100) - 50) * BrandCatalogue.StandardsGainMost / 50), "campaign", BrandsText.Lost(campaign))
                : BrandsText.T("reallife_brand_dist_no_shop", "fade", BrandsText.Bp(BrandCatalogue.DecayEmptyBp)));
            box.Line(BrandsText.Fill("reallife_brand_dist_slow", "gain", BrandsText.Lost(gain), "fade", BrandsText.Bp(shops > 0 ? BrandCatalogue.DecayBp : BrandCatalogue.DecayEmptyBp)));
            if (effective > d.Rec)
            {
                box.Line(BrandsText.T("reallife_brand_dist_floor", "points", BrandsText.Points(effective), "city", BrandsText.Points(b.City)));
            }
            box.Line(BrandsText.Fill("reallife_brand_dist_worth", "percent", BrandsText.Percent(BrandRules.RecFootfall(effective, b.Standards)), "most", BrandCatalogue.RecFootfallMost.ToString()));
            BackButton(box);
        }
    }
}
