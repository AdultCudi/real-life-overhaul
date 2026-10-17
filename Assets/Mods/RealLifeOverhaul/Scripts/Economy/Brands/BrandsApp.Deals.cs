using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// The Applicants and Agreements tabs, and the page of one franchised shop that they and the Shops tab show. An
    /// applicant has three pages one after the other (their record; where and for how long; the money and their
    /// answer); a franchise has two chosen with the selector (the shop, the agreement).
    /// </summary>
    public sealed partial class BrandsApp
    {
        private static readonly string[] FranchisePageKeys = { "reallife_brand_page_shop", "reallife_brand_page_agreement" };
        private static readonly string[] AskAboutKeys =
        {
            null, "reallife_brand_ask_about_help", "reallife_brand_ask_about_second", "reallife_brand_ask_about_renewal", "reallife_brand_ask_about_dispute", "reallife_brand_ask_about_bid"
        };

        private int _dealPage;                    // an applicant's page: 0 their record, 1 where, 2 the money
        private int _site, _length;
        private int _fee = 1, _royalty = 1, _levy = 1;   // tiers
        private bool _exclusive;
        private int _franchisePage = -1;          // 0 the shop, 1 the agreement; -1 until first shown

        private TableSpec WriteApplicants(InfoBox box, BrandsState state, int day, ref string selected)
        {
            List<string> districts = BrandsTables.Applicants.Filters[1].Names;   // the second filter's kinds
            districts.Clear();
            Applicant picked = null;
            for (int i = 0; i < state.Applicants.Count; i++)
            {
                Applicant a = state.Applicants[i];
                Brand b = state.Brand(a.BrandId);
                TableRow row = Row("app:" + a.Id);
                row.Kind = (int)a.Temper;
                row.Kind2 = KindOf(districts, BrandsText.District(a.District));
                row.Set(0, a.Name);
                row.Set(1, b != null ? b.Name : "?");
                row.Set(2, BrandsText.District(a.District));
                row.Set(3, BrandsText.Temper(a.Temper));
                row.Set(4, BrandsText.Money(a.Capital), a.Capital);
                row.Set(5, a.Experience.ToString(), a.Experience);
                row.Set(6, BrandsText.Day(a.Until), a.Until, a.Until - day <= 2 ? TableRow.Yellow : TableRow.Plain);
                picked = Is(row, ref selected) ? a : picked;
            }
            if (state.Applicants.Count == 0)
            {
                Say("none", BrandsText.T("reallife_brand_row_no_applicant"));
            }
            Brand brand = picked != null ? state.Brand(picked.BrandId) : null;
            if (brand != null)
            {
                WriteApplicant(box, state, brand, picked, day);
                return BrandsTables.Applicants;
            }
            box.Title(BrandsText.T("reallife_brand_tab_applicants"));
            box.Note(BrandsText.Fill("reallife_brand_app_intro", "days", BrandCatalogue.ApplicantDays.ToString(), "most", BrandCatalogue.ApplicantsMost.ToString()));
            box.Section("reallife_ibox_sec_how");
            box.Note(BrandsText.Fill("reallife_brand_app_chance", "base", (BrandCatalogue.ArriveBase / 10).ToString(), "per", (BrandCatalogue.ArrivePerPoint / 10f).ToString("0.#")));
            box.Note(BrandsText.T("reallife_brand_app_tempers"));
            return BrandsTables.Applicants;
        }

        private void WriteApplicant(InfoBox box, BrandsState state, Brand b, Applicant a, int day)
        {
            int id = a.Id, index = BrandsDirector.Index;
            int bears = FranchiseRules.Bears(a.Temper, BrandRules.Effective(b, a.District));
            box.Title(a.Name);
            box.Subtitle(BrandsText.T("reallife_brand_dist_title", "brand", b.Name, "district", BrandsText.District(a.District)));
            box.Chip(BrandsText.Temper(a.Temper), (int)a.Temper == 0 ? CellTone.Green : (int)a.Temper == 2 ? CellTone.Yellow : CellTone.Plain);
            if (_dealPage <= 0)
            {
                box.Line(BrandsText.Fill(a.From != 0 ? "reallife_brand_app_second" : "reallife_brand_app_wants", "brand", b.Name, "district", BrandsText.District(a.District),
                    "day", a.Until.ToString()));
                box.Section("reallife_ibox_sec_applicant");
                box.Row("reallife_brand_col_capital", BrandsText.Money(a.Capital));
                box.Note(BrandsText.Fill("reallife_brand_app_capital", "capital", BrandsText.Money(a.Capital), "fitout", BrandsText.Money(FranchiseRules.FitOut(index))));
                if (a.From == 0)
                {
                    box.Row("reallife_brand_col_years", a.Experience.ToString());
                    if (a.ShopsRun > 0)
                    {
                        box.RowPair("reallife_ibox_shops_run", a.ShopsRun.ToString(), "reallife_ibox_shops_closed", a.ShopsClosed.ToString());
                    }
                }
                box.Row("reallife_brand_app_temper", BrandsText.Temper(a.Temper));
                box.Note(BrandsText.TemperAbout(a.Temper));
                box.Line(BrandsText.Fill("reallife_brand_app_known", "points", BrandsText.Points(BrandRules.Effective(b, a.District)), "bears", bears.ToString()));
                var first = box.ButtonRow();
                InfoBox.Button(first, InfoBox.Positive, "reallife_brand_app_terms", () =>
                {
                    _dealPage = 1;
                    Refresh();
                });
                InfoBox.Button(first, InfoBox.Negative, "reallife_brand_app_refuse", () =>
                {
                    _pick = null;
                    _director.Refuse(id);
                });
                InfoBox.Button(first, InfoBox.Additional, "reallife_brand_back", ClosePick);
                return;
            }

            IReadOnlyList<BrandSite> sites = _director.SitesFor(a);
            int site = sites.Count > 0 ? BrandCatalogue.Clamp(_site, 0, sites.Count - 1) : -1;
            if (_dealPage == 1)
            {
                box.Section("reallife_ibox_sec_where");
                box.Line(BrandsText.T("reallife_brand_deal_where", "district", BrandsText.District(a.District), "count", sites.Count.ToString()));
                if (sites.Count > 0)
                {
                    Choose(box, sites.Count, site, i => BrandsText.T("reallife_brand_deal_site", "address", sites[i].Label, "traffic", sites[i].Traffic.ToString()), i => _site = i);
                    box.Note(BrandsText.T("reallife_brand_deal_site_about"));
                }
                else
                {
                    box.Line(BrandsText.T("reallife_brand_deal_no_site"), CellTone.Yellow);
                }
                box.Section("reallife_ibox_sec_terms");
                box.Note(BrandsText.T("reallife_brand_deal_length"));
                Choose(box, BrandCatalogue.Tiers, _length, i => BrandsText.T("reallife_brand_deal_length_value", "years", BrandCatalogue.LengthYears[i].ToString(),
                    "days", (BrandCatalogue.LengthYears[i] * BrandsGame.DaysPerYear).ToString()), i => _length = i);
                bool exclusive = _exclusive;
                if (!box.Switch("reallife_brand_deal_exclusive", exclusive, on =>
                    {
                        _exclusive = on;
                        Refresh();
                    }))
                {
                    box.Line(BrandsText.T(exclusive ? "reallife_brand_deal_exclusive_on" : "reallife_brand_deal_exclusive_off"));
                }
                box.Note(BrandsText.T("reallife_brand_deal_exclusive_about", "relief", BrandCatalogue.ExclusiveRelief.ToString()));
                var second = box.ButtonRow();
                InfoBox.Button(second, InfoBox.Positive, "reallife_brand_deal_next", () =>
                {
                    _dealPage = 2;
                    Refresh();
                });
                InfoBox.Button(second, InfoBox.Additional, "reallife_brand_deal_previous", () =>
                {
                    _dealPage = 0;
                    Refresh();
                });
                return;
            }

            int fee = FranchiseRules.Tier(_fee), royalty = FranchiseRules.Tier(_royalty), levy = FranchiseRules.Tier(_levy), length = FranchiseRules.Tier(_length);
            box.Section("reallife_ibox_sec_money");
            Choose(box, BrandCatalogue.Tiers, fee, i => BrandsText.T("reallife_brand_deal_fee", "amount", BrandsText.Money(FranchiseRules.EntryFee(a, i, index))), i => _fee = i);
            Choose(box, BrandCatalogue.Tiers, royalty, i => BrandsText.T("reallife_brand_deal_royalty", "share", BrandsText.Bp(BrandCatalogue.RoyaltyBp[i])), i => _royalty = i);
            Choose(box, BrandCatalogue.Tiers, levy, i => BrandsText.T("reallife_brand_deal_levy", "share", BrandsText.Bp(BrandCatalogue.LevyBp[i])), i => _levy = i);
            Refusal answer = FranchiseRules.Answer(state, b, a, fee, royalty, levy, _exclusive, index);
            box.Line(BrandsText.Fill("reallife_brand_deal_burden", "burden", FranchiseRules.Burden(fee, royalty, levy, _exclusive).ToString(), "bears", bears.ToString()));
            box.Line("<b>" + BrandsText.Answer(answer) + "</b>", answer == Refusal.None ? CellTone.Green : CellTone.Red);
            box.Note(BrandsText.Fill("reallife_brand_deal_support", "base", BrandsText.Money(CompanyCatalogue.AtPrices(BrandCatalogue.SupportBase, index)),
                "share", BrandsText.Bp(BrandCatalogue.SupportBp)));
            var last = box.ButtonRow();
            if (answer == Refusal.None)
            {
                bool exclusive = _exclusive;
                string name = a.Name, brand = b.Name;
                string amount = BrandsText.Money(FranchiseRules.EntryFee(a, fee, index));
                string share = BrandsText.Bp(BrandCatalogue.RoyaltyBp[royalty]);
                InfoBox.Button(last, InfoBox.Positive, "reallife_brand_deal_sign", () => Confirm("reallife_brand_confirm_sign_title", "reallife_brand_confirm_sign",
                    new { name, brand, amount, royalty = share }, () => _director.Sign(id, site, fee, royalty, levy, exclusive, length), false));
            }
            InfoBox.Button(last, InfoBox.Additional, "reallife_brand_deal_previous", () =>
            {
                _dealPage = 1;
                Refresh();
            });
        }

        private TableSpec WriteAgreements(InfoBox box, BrandsState state, int day, ref string selected)
        {
            Franchise franchise = null;
            for (int i = 0; i < state.Franchises.Count; i++)
            {
                Franchise f = state.Franchises[i];
                Brand b = state.Brand(f.BrandId);
                bool waits = f.Ask != Ask.None;
                TableRow row = Row("fr:" + f.Id);
                row.Kind = waits ? BrandsTables.Waiting : f.Trouble ? BrandsTables.Trouble : BrandsTables.InOrder;
                row.Set(0, f.Name);
                row.Set(1, b != null ? b.Name : "?");
                row.Set(2, BrandsText.District(f.District));
                row.Set(3, BrandsText.Bp(f.RoyaltyBp), f.RoyaltyBp / 100.0);
                row.Set(4, BrandsText.Money(f.Royalty), f.Royalty);
                row.Set(5, BrandsText.Money(f.Paid), f.Paid);
                row.Set(6, BrandsText.Day(f.Until), f.Until);
                // Ordered by how pressing it is: 2 something waits, 1 short of cash, 0 in order.
                row.Set(7, waits ? BrandsText.Ask(f.Ask) : f.Trouble ? BrandsText.T("reallife_brand_status_trouble") : BrandsText.T("reallife_brand_row_in_order"), waits ? 2 : f.Trouble ? 1 : 0,
                    waits ? TableRow.Red : f.Trouble ? TableRow.Yellow : TableRow.Plain);
                franchise = Is(row, ref selected) ? f : franchise;
            }
            IReadOnlyList<BrandShop> shops = _director.Shops;
            BrandShop chainShop = null;
            for (int i = 0; i < shops.Count; i++)
            {
                BrandShop shop = shops[i];
                if (shop.Chain == null)
                {
                    continue;
                }
                Taken t = shop.Chain;
                TableRow row = Row("shop:" + shop.Place);
                row.Kind = BrandsTables.OwnChain;
                row.Set(0, shop.Name);
                row.Set(1, BrandsText.Chain(t.Chain));
                row.Set(2, BrandsText.District(shop.District));
                row.Set(3, BrandsText.Bp(BrandCatalogue.ChainRoyaltyBp[t.Chain]), BrandCatalogue.ChainRoyaltyBp[t.Chain] / 100.0);
                row.Set(4, BrandsText.Money(-t.Average), -t.Average);
                row.Set(5, BrandsText.Money(-t.Paid), -t.Paid);
                row.Set(6, BrandsText.Day(t.Until), t.Until);
                row.Set(7, t.Breach > 0 ? BrandsText.T("reallife_brand_row_breach_days", "days", t.Breach.ToString()) : BrandsText.T("reallife_brand_row_in_order"), t.Breach > 0 ? 2 : 0,
                    t.Breach > 0 ? TableRow.Red : TableRow.Plain);
                chainShop = Is(row, ref selected) ? shop : chainShop;
            }
            if (_rows.Count == 0)
            {
                Say("none", BrandsText.T("reallife_brand_row_no_agreement"));
            }
            if (franchise != null)
            {
                WriteFranchise(box, state, franchise, day);
            }
            else if (chainShop != null)
            {
                box.Title(chainShop.Name);
                WriteChainShop(box, chainShop);
            }
            else
            {
                box.Title(BrandsText.T("reallife_brand_tab_agreements"));
                box.Note(BrandsText.T("reallife_brand_agree_intro"));
                box.Section("reallife_ibox_sec_how");
                box.Note(BrandsText.Fill("reallife_brand_agree_cause", "warnings", BrandCatalogue.CauseWarnings.ToString(), "days", BrandCatalogue.FreshDays.ToString()));
                box.Note(BrandsText.Fill("reallife_brand_agree_unfair", "share", BrandsText.Share(BrandCatalogue.CompensationLeastShare), "points", BrandsText.Lost(BrandCatalogue.UnfairLoss)));
            }
            return BrandsTables.Agreements;
        }

        private void WriteFranchise(InfoBox box, BrandsState state, Franchise f, int day)
        {
            Brand b = state.Brand(f.BrandId);
            if (b == null)
            {
                return;
            }
            if (_franchisePage < 0)
            {
                _franchisePage = f.Ask != Ask.None || _tab == BrandsDirector.TabAgreements ? 1 : 0;
            }
            box.Title(f.Name);
            box.Subtitle(BrandsText.T("reallife_brand_dist_title", "brand", b.Name, "district", BrandsText.District(f.District)));
            if (f.Ask != Ask.None)
            {
                box.Chip(BrandsText.T("reallife_brand_fr_asks"), CellTone.Yellow);
            }
            if (f.Trouble)
            {
                box.Chip(BrandsText.T("reallife_ibox_chip_trouble"), CellTone.Red);
            }
            Choose(box, FranchisePageKeys.Length, _franchisePage, i => BrandsText.T(FranchisePageKeys[i]), i => _franchisePage = i);
            if (_franchisePage == 0)
            {
                WriteFranchiseShop(box, b, f, day);
            }
            else
            {
                WriteFranchiseAgreement(box, b, f, day);
            }
        }

        // Where the shop is and how it stands in the city, what it takes, and its standards as far as they are known.
        private void WriteFranchiseShop(InfoBox box, Brand b, Franchise f, int day)
        {
            int id = f.Id, index = BrandsDirector.Index;
            box.Section("reallife_ibox_sec_where");
            box.Line(BrandsText.Fill("reallife_brand_fr_where", "brand", b.Name, "address", BrandsGame.AddressWords(f.Place), "district", BrandsText.District(f.District)));
            box.Note(f.Hold == Hold.Books ? BrandsText.T("reallife_brand_fr_hold_books", "metres", BrandCatalogue.CannibalRadius.ToString(), "percent", BrandCatalogue.CannibalEach.ToString())
                : BrandsText.T(f.Hold == Hold.Opened ? "reallife_brand_fr_hold_opened" : "reallife_brand_fr_hold_converted", "name", f.GameName));
            box.Section("reallife_brand_col_takings");
            box.RowPair("reallife_ibox_yesterday", BrandsText.Money(f.Takings), "reallife_brand_col_royalty", BrandsText.Money(f.Royalty));
            box.Line(BrandsText.Fill("reallife_brand_fr_takings", "takings", BrandsText.Money(f.Takings), "royalty", BrandsText.Money(f.Royalty),
                "ordinary", BrandsText.Money(FranchiseRules.Ordinary(f.Base, b.Ticket)), "customers", f.Base.ToString(), "ticket", BrandsText.Money(b.Ticket / 100f)));
            box.Note(BrandsText.T("reallife_brand_fr_takings_about"));
            box.Section("reallife_brand_col_standards");
            if (f.Seen >= 0)
            {
                box.Meter("reallife_brand_col_standards", f.Seen / 100f, Standards(f.Seen), f.Seen < b.Minimum ? CellTone.Red : CellTone.Green);
            }
            box.Line(f.Seen < 0
                ? BrandsText.T("reallife_brand_fr_never_seen", "minimum", BrandsText.Minimum(b.Minimum))
                : BrandsText.Fill("reallife_brand_fr_seen", "standards", Standards(f.Seen), "day", f.SeenDay.ToString(), "minimum", BrandsText.Minimum(b.Minimum), "warnings", f.Warnings.ToString()));
            if (f.Trouble)
            {
                box.Line(BrandsText.T("reallife_brand_fr_trouble", "drop", BrandCatalogue.TroubleDrop.ToString()), CellTone.Red);
            }
            bool inside = BrandsGame.PlayerInside(f);
            box.Note(inside ? BrandsText.T("reallife_brand_fr_inspect_self")
                : BrandsText.Fill("reallife_brand_fr_inspect_paid", "fee", BrandsText.Money(FranchiseRules.InspectorFee(index)), "retrain", BrandsText.Money(FranchiseRules.RetrainFee(index))));
            var buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, inside ? "reallife_brand_fr_inspect_now" : "reallife_brand_fr_inspect_send", () => _director.Inspect(id));
            if (FranchiseRules.CanWarn(b, f, day))
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_brand_fr_warn", () => _director.Warn(id));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_brand_fr_retrain", () => _director.Retrain(id));
            BackButton(box);
        }

        // The agreement's terms, what the franchisee asks with the answers, and ending it.
        private void WriteFranchiseAgreement(InfoBox box, Brand b, Franchise f, int day)
        {
            int id = f.Id;
            box.Section("reallife_ibox_sec_terms");
            box.Line(BrandsText.Fill("reallife_brand_fr_terms", "fee", BrandsText.Money(f.EntryFee), "royalty", BrandsText.Bp(f.RoyaltyBp), "levy", BrandsText.Bp(f.LevyBp),
                "from", f.Signed.ToString(), "until", f.Until.ToString()));
            box.Line(BrandsText.Fill(f.Exclusive ? "reallife_brand_fr_alone" : "reallife_brand_fr_shared", "district", BrandsText.District(f.District), "paid", BrandsText.Money(f.Paid)));
            if (day < f.WaivedUntil)
            {
                box.Line(BrandsText.T("reallife_brand_fr_waived", "day", f.WaivedUntil.ToString()), CellTone.Yellow);
            }
            bool cause = FranchiseRules.HasCause(b, f, day);
            int cost = _director.EndCost(f);
            if (f.Ask != Ask.None)
            {
                bool inCourt = f.Ask == Ask.Dispute && f.Suit != 0;
                box.Headline("reallife_brand_fr_asks");
                box.Line(BrandsText.Fill(AskAboutKeys[(int)f.Ask], "amount", BrandsText.Money(f.AskAmount), "day", f.AskUntil.ToString(), "days", BrandCatalogue.WaiveDays.ToString(),
                    "district", BrandsText.District(f.District)));
                if (inCourt)
                {
                    box.Line(BrandsText.T("reallife_brand_fr_court", "number", f.Suit.ToString()), CellTone.Red);
                }
                var answers = box.ButtonRow();
                if (f.Ask != Ask.BuyBrand)
                {
                    InfoBox.Button(answers, InfoBox.Positive, f.Ask == Ask.Dispute ? "reallife_brand_fr_give_way" : "reallife_brand_fr_agree", () => _director.Agree(id));
                }
                if (f.Ask == Ask.Help)
                {
                    InfoBox.Button(answers, InfoBox.Additional, "reallife_brand_fr_waive", () => _director.Waive(id));
                }
                if (!inCourt)
                {
                    InfoBox.Button(answers, InfoBox.Negative, "reallife_brand_fr_decline", () => _director.Decline(id));
                }
            }
            box.Section("reallife_ibox_sec_ending");
            box.Note(cause ? BrandsText.T("reallife_brand_fr_cause", "warnings", f.Warnings.ToString())
                : BrandsText.Fill("reallife_brand_fr_no_cause", "amount", BrandsText.Money(cost), "points", BrandsText.Lost(BrandCatalogue.UnfairLoss),
                    "warnings", BrandCatalogue.CauseWarnings.ToString()));
            var last = box.ButtonRow();
            string name = f.Name;
            InfoBox.Button(last, InfoBox.Negative, "reallife_brand_fr_end", () => Confirm("reallife_brand_confirm_end_title", cause ? "reallife_brand_confirm_end_cause" : "reallife_brand_confirm_end",
                new { name, amount = BrandsText.Money(cost), points = BrandsText.Lost(BrandCatalogue.UnfairLoss) }, () =>
                {
                    _pick = null;
                    _director.End(id);
                }, true));
            InfoBox.Button(last, InfoBox.Additional, "reallife_brand_back", ClosePick);
        }
    }
}
