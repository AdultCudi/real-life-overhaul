using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// The Brands tab: each brand and each district it has a figure for. A brand's box has three pages (its figures,
    /// its settings, its campaigns); a district's says what its recognition is made of today.
    /// </summary>
    public sealed partial class BrandsApp
    {
        private static readonly string[] BrandPageKeys = { "reallife_brand_page_figures", "reallife_brand_page_settings", "reallife_brand_page_campaigns" };

        private readonly List<string> _pushDistricts = new List<string>(8);
        private int _brandPage;
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
                CellTone tone = b.Standards < BrandCatalogue.LowStandards ? TableRow.Red : b.Minimum > 0 && b.Standards < b.Minimum ? TableRow.Yellow : TableRow.Plain;
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
                WriteBrand(box, state, picked, day);
            }
            else if (district != null)
            {
                WriteDistrict(box, state, districtOf, district);
            }
            else
            {
                box.Title(BrandsText.T("reallife_brand_app"));
                box.Note(BrandsText.T("reallife_brand_intro_what"));
                box.Section("reallife_ibox_sec_how");
                box.Line(BrandsText.T("reallife_brand_intro_effect", "most", BrandCatalogue.RecFootfallMost.ToString(), "cap", BrandCatalogue.FootfallMost.ToString()));
                box.Line(BrandsText.T("reallife_brand_intro_franchise"));
                box.Section("reallife_ibox_sec_next");
                box.Line(BrandsText.T("reallife_brand_intro_start"), CellTone.Green);
            }
            return BrandsTables.Brands;
        }

        // Columns 2 to 6, which a brand's row and a district's row share.
        private static void WriteBrandCells(TableRow row, int shops, int rec, int city, int standards, CellTone tone, int worth)
        {
            row.Set(2, shops.ToString(), shops);
            row.Set(3, BrandsText.Rec(rec), rec / 100.0);
            row.Set(4, BrandsText.Rec(city), city / 100.0);
            row.Set(5, Standards(standards), standards, tone);
            row.Set(6, BrandsText.Percent(worth), worth);
        }

        private void WriteBrand(InfoBox box, BrandsState state, Brand b, int day)
        {
            box.Title(b.Name);
            box.Subtitle(BrandsText.Type(b.Type));
            box.Chip(Standards(b.Standards), b.Standards < BrandCatalogue.LowStandards ? CellTone.Red : b.Standards < b.Minimum ? CellTone.Yellow : CellTone.Green);
            if (b.Offered)
            {
                box.Chip(BrandsText.T("reallife_ibox_chip_on_offer"), CellTone.Green);
            }
            if (b.Bid > 0)
            {
                Franchise bidder = state.Franchise(b.BidBy);
                box.Line(BrandsText.T("reallife_brand_bid", "name", bidder != null ? bidder.Name : "?", "amount", BrandsText.Money(b.Bid), "day", b.BidUntil.ToString()));
                var answers = box.ButtonRow();
                int by = b.BidBy;
                string name = b.Name;
                string amount = BrandsText.Money(b.Bid);
                InfoBox.Button(answers, InfoBox.Positive, "reallife_brand_bid_sell",
                    () => Confirm("reallife_brand_confirm_sell_title", "reallife_brand_confirm_sell", new { brand = name, amount }, () => _director.Agree(by), true));
                InfoBox.Button(answers, InfoBox.Negative, "reallife_brand_bid_refuse", () => _director.Decline(by));
            }
            Choose(box, BrandPageKeys.Length, _brandPage, i => BrandsText.T(BrandPageKeys[i]), i => _brandPage = i);
            // The page's own main button, if it has one, stands beside Back in the last row.
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
                    WriteBrandFigures(box, state, b, day);
                    break;
            }
            var last = box.ButtonRow();
            if (main != null)
            {
                InfoBox.Button(last, InfoBox.Positive, mainKey, main);
            }
            InfoBox.Button(last, InfoBox.Additional, "reallife_brand_back", ClosePick);
        }

        // The brand's two figures, what they are made of and moved by, and whether the day's step has run.
        private void WriteBrandFigures(InfoBox box, BrandsState state, Brand b, int today)
        {
            int members = BrandRules.Members(state, b, null), franchised = BrandRules.Franchised(state, b, null);
            box.Section("reallife_brand_col_shops");
            box.RowPair("reallife_ibox_members", members.ToString(), "reallife_ibox_franchised", franchised.ToString());
            box.Row("reallife_ibox_founded", FinanceText.Day(b.Founded));
            box.Section("reallife_brand_col_recognition");
            box.Meter("reallife_brand_col_city", b.City / 10000f, BrandsText.Rec(b.City), CellTone.Plain);
            box.Meter("reallife_ibox_best_district", BrandRules.Best(b) / 10000f, BrandsText.Rec(BrandRules.Best(b)), CellTone.Plain);
            box.Note(BrandsText.Fill("reallife_brand_fig_rec", "city", BrandsText.Rec(b.City), "best", BrandsText.Rec(BrandRules.Best(b)), "fade", BrandsText.Bp(BrandCatalogue.DecayBp),
                "empty", BrandsText.Bp(BrandCatalogue.DecayEmptyBp)));
            // A figure that stands still because Brands was off says so here.
            int days = Math.Max(0, today - b.Founded), missed = Math.Min(days, BrandRules.Missed(b, today));
            box.Line(BrandsText.Fill(BrandRules.StepsKey(days, days - missed), "moved", (days - missed).ToString(), "days", days.ToString(), "day", b.Founded.ToString()));
            if (missed > 0)
            {
                box.Line(BrandsText.Fill(BrandRules.MissedKey(missed), "missed", missed.ToString()), CellTone.Yellow);
            }
            box.Note(BrandsText.Fill("reallife_brand_fig_city", "districts", BrandCatalogue.CityDistricts.ToString(), "step", BrandsText.Bp(BrandCatalogue.CityStepBp)));
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
            CellTone tone = b.Standards < BrandCatalogue.LowStandards ? CellTone.Red : b.Standards < b.Minimum ? CellTone.Yellow : CellTone.Green;
            box.Section("reallife_brand_col_standards");
            box.Meter("reallife_brand_col_standards", b.Standards / 100f, Standards(b.Standards), tone);
            box.Row("reallife_ibox_minimum", BrandsText.Minimum(b.Minimum));
            box.Note(worst != null
                ? BrandsText.Fill("reallife_brand_fig_worst", "business", worst, "standards", Standards(low), "mean", BrandsText.Share(BrandCatalogue.BrandMeanShare),
                    "worst", BrandsText.Share(BrandCatalogue.BrandWorstShare))
                : BrandsText.T("reallife_brand_fig_no_members"));
            box.Line(BrandsText.Fill(b.Standards < BrandCatalogue.LowStandards ? "reallife_brand_fig_effect_low" : "reallife_brand_fig_effect", "most", BrandCatalogue.RecFootfallMost.ToString(),
                "low", BrandCatalogue.LowStandards.ToString(), "tolerance", BrandRules.Tolerance(b.Standards).ToString(), "cap", BrandCatalogue.FootfallMost.ToString()),
                b.Standards < BrandCatalogue.LowStandards ? CellTone.Red : CellTone.Plain);
            if (franchised > 0 || b.Earned > 0)
            {
                box.Section("reallife_ibox_sec_money");
                box.Line(BrandsText.Fill("reallife_brand_fig_money", "net", BrandsText.Money(b.Net), "earned", BrandsText.Money(b.Earned), "fund", BrandsText.Money(b.Fund),
                    "failed", b.Failed.ToString()));
            }
        }

        // The minimum for standards, the franchise offer, shared prices and hours, the name, giving the brand up.
        private void WriteBrandSettings(InfoBox box, BrandsState state, Brand b, out string mainKey, out Action main)
        {
            int id = b.Id;
            int step = Math.Max(0, Array.IndexOf(BrandCatalogue.MinimumSteps, b.Minimum));
            box.Section("reallife_ibox_minimum");
            box.Note(BrandsText.T("reallife_brand_set_minimum"));
            Choose(box, BrandCatalogue.MinimumSteps.Length, step, i => BrandsText.T("reallife_brand_set_minimum_value", "points", BrandsText.Minimum(BrandCatalogue.MinimumSteps[i])),
                i => _director.SetMinimum(id, i));
            int good;
            bool can = BrandRules.CanOffer(state, b, out good);
            box.Section("reallife_ibox_sec_franchise");
            box.Line(BrandsText.Fill(b.Offered ? "reallife_brand_set_offered" : can ? "reallife_brand_set_can_offer" : "reallife_brand_set_cannot_offer", "good", good.ToString(),
                "shops", BrandCatalogue.OfferShops.ToString(), "standards", BrandCatalogue.OfferStandards.ToString(), "city", BrandsText.Points(BrandCatalogue.OfferCityRec),
                "district", BrandsText.Points(BrandCatalogue.OfferDistrictRec), "mean", BrandsText.Points(BrandCatalogue.OfferMeanRec), "has", BrandsText.Points(b.City),
                "best", BrandsText.Points(BrandRules.Best(b)), "average", BrandsText.Rec(BrandRules.SpreadRec(state, b))));
            bool offered = b.Offered;
            if (!box.Switch("reallife_brand_set_offer", offered, on =>
                {
                    _director.Offer(id, on);
                    Refresh();   // a brand that is not ready stays off offer: the switch is written again as it stands
                }))
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, offered ? "reallife_brand_set_offer_off" : "reallife_brand_set_offer_on", () => _director.Offer(id, !offered));
            }
            string flagship = _director.ShopName(b.Flagship);
            box.Section("reallife_ibox_sec_shared");
            box.Note(BrandsText.T("reallife_brand_set_shared", "business", flagship));
            bool keep = b.Keep, local = b.Local;
            box.Line(keep
                ? BrandsText.Fill(local ? "reallife_brand_set_kept_local" : "reallife_brand_set_kept", "business", flagship, "band", BrandsText.Share(BrandCatalogue.LocalBand))
                : BrandsText.T("reallife_brand_set_keep_about", "business", flagship));
            bool keepSwitch = box.Switch("reallife_brand_set_keep", keep, on =>
            {
                _director.KeepBrandPrices(id, on);
                Refresh();
            });
            bool localSwitch = keep && box.Switch("reallife_brand_set_local", local, on =>
            {
                _director.KeepBrandLocal(id, on);
                Refresh();
            });
            if (!keepSwitch || (keep && !localSwitch))
            {
                var switches = box.ButtonRow();
                if (!keepSwitch)
                {
                    InfoBox.Button(switches, InfoBox.Positive, keep ? "reallife_brand_set_keep_off" : "reallife_brand_set_keep_on", () => _director.KeepBrandPrices(id, !keep));
                }
                if (keep && !localSwitch)
                {
                    InfoBox.Button(switches, InfoBox.Additional, local ? "reallife_brand_set_local_off" : "reallife_brand_set_local_on", () => _director.KeepBrandLocal(id, !local));
                }
            }
            string name = b.Name;
            var shared = box.ButtonRow();
            if (!keep)
            {
                InfoBox.Button(shared, InfoBox.Positive, "reallife_brand_set_prices", () => Confirm("reallife_brand_confirm_prices_title", "reallife_brand_confirm_prices",
                    new { brand = name, business = flagship }, () => _director.SharePrices(id), false));
            }
            InfoBox.Button(shared, InfoBox.Additional, "reallife_brand_set_hours", () => Confirm("reallife_brand_confirm_hours_title", "reallife_brand_confirm_hours",
                new { brand = name, business = flagship }, () => _director.ShareHours(id), false));
            InfoBox.Button(shared, InfoBox.Negative, "reallife_brand_set_give_up", () => Confirm("reallife_brand_confirm_give_up_title", "reallife_brand_confirm_give_up", new { brand = name }, () =>
            {
                _pick = null;
                _director.GiveUp(id);
            }, true));
            mainKey = "reallife_brand_set_rename";
            main = () => _director.Rename(id);
        }

        // The campaign that runs, or the two to choose from with today's cost.
        private void WriteBrandCampaigns(InfoBox box, BrandsState state, Brand b, out string mainKey, out Action main)
        {
            int id = b.Id;
            mainKey = null;
            main = null;
            box.Note(BrandsText.Fill("reallife_brand_push_about", "local", BrandsText.Lost(BrandCatalogue.CampaignLocalGain), "city", BrandsText.Lost(BrandCatalogue.CampaignCityGain)));
            box.Section("reallife_ibox_sec_money");
            box.Line(BrandsText.T("reallife_brand_push_fund", "fund", BrandsText.Money(b.Fund),
                "most", BrandsText.Money(CompanyCatalogue.AtPrices(BrandCatalogue.FundIdleMost, BrandsDirector.Index))));
            box.Section("reallife_ibox_sec_campaign");
            if (b.Push >= 0)
            {
                string where = b.Push == BrandCatalogue.PushCity ? BrandsText.T("reallife_brand_push_everywhere") : BrandsText.District(b.PushDistrict);
                box.Line(BrandsText.Fill("reallife_brand_push_running", "campaign", BrandsText.Push(b.Push), "where", where,
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
            string chosenDistrict = district;
            int chosen = kind;
            mainKey = "reallife_brand_push_book";
            main = () => _director.BookPush(id, chosen, chosenDistrict);
        }

        // One district of a brand: what its recognition gains today and what it is worth to a shop there.
        private void WriteDistrict(InfoBox box, BrandsState state, Brand b, DistrictFile d)
        {
            int shops = BrandRules.Shops(state, b, d.Name), customers = BrandRules.Customers(state, b, d.Name);
            int pull = BrandRules.PullIn(state, b, shops);
            int gain = BrandRules.GainOn(state, b, d, b.Age + 1);
            int campaign = gain - BrandRules.Gain(shops, customers, b.Standards, 0) - pull;   // what tomorrow's campaign adds, as the step will count it
            int next = BrandRules.Step(d.Rec, gain, shops > 0);
            int effective = BrandRules.Effective(b, d.Name);
            box.Title(BrandsText.T("reallife_brand_dist_title", "brand", b.Name, "district", BrandsText.District(d.Name)));
            box.Section("reallife_brand_col_recognition");
            box.Meter("reallife_ibox_today", d.Rec / 10000f, BrandsText.Lost(d.Rec), CellTone.Plain);
            box.Row("reallife_ibox_tomorrow", BrandsText.Lost(next), next > d.Rec ? CellTone.Green : next < d.Rec ? CellTone.Yellow : CellTone.Plain);
            box.Note(shops > 0
                ? BrandsText.Fill("reallife_brand_dist_parts", "shops", shops.ToString(),
                    "presence", BrandsText.Lost(Math.Min(shops, BrandCatalogue.PresenceShopsMost) * BrandCatalogue.PresencePerShop), "customers", customers.ToString(),
                    "footfall", BrandsText.Lost(BrandCatalogue.Clamp(customers, 0, BrandCatalogue.FootfallFull) * BrandCatalogue.FootfallGainMost / BrandCatalogue.FootfallFull),
                    "standards", Standards(b.Standards), "good", BrandsText.Lost((BrandCatalogue.Clamp(b.Standards, 0, 100) - 50) * BrandCatalogue.StandardsGainMost / 50),
                    "campaign", BrandsText.Lost(campaign), "others", (BrandRules.Shops(state, b, null) - shops).ToString(), "pull", BrandsText.Lost(pull))
                : BrandsText.T("reallife_brand_dist_no_shop", "fade", BrandsText.Bp(BrandCatalogue.DecayEmptyBp)));
            box.Note(BrandsText.Fill("reallife_brand_dist_slow", "gain", BrandsText.Lost(gain), "fade", BrandsText.Bp(shops > 0 ? BrandCatalogue.DecayBp : BrandCatalogue.DecayEmptyBp)));
            if (effective > d.Rec)
            {
                box.Note(BrandsText.T("reallife_brand_dist_floor", "points", BrandsText.Points(effective), "city", BrandsText.Points(b.City)));
            }
            box.Section("reallife_brand_col_worth");
            box.Line(BrandsText.Fill("reallife_brand_dist_worth", "percent", BrandsText.Percent(BrandRules.RecFootfall(effective, b.Standards)), "most", BrandCatalogue.RecFootfallMost.ToString()));
            BackButton(box);
        }
    }
}
