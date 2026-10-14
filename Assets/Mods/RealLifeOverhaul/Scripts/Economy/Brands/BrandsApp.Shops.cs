using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// The Shops tab. The table (BrandsTables.Shops): every shop of the player's and every franchised shop, with who runs it, where,
    /// what it took yesterday (and the royalty on that) and its standards. The box: for a shop of the player's under
    /// no brand, the ways into one (make a brand, join one, take a city chain's franchise); for a member shop, its
    /// standards and what the brand brings it, part by part; for a shop under a chain, how it stands with the chain
    /// and the switch that keeps its prices inside the chain's range;
    /// for a franchised shop, the same page the Agreements tab shows.
    /// </summary>
    public sealed partial class BrandsApp
    {
        private readonly List<Brand> _joinable = new List<Brand>(4);
        private int _joinBrand;   // the brand whose "join" is showing
        private int _chain;       // the city chain whose quote is showing

        private TableSpec WriteShops(InfoBox box, BrandsState state, int day, ref string selected)
        {
            IReadOnlyList<BrandShop> shops = _director.Shops;
            List<string> districts = BrandsTables.Shops.Filters[1].Names;   // the second filter's kinds: the districts the shops stand in
            districts.Clear();
            BrandShop picked = null;
            Franchise franchise = null;
            string you = BrandsText.T("reallife_brand_run_you");
            for (int i = 0; i < shops.Count; i++)
            {
                BrandShop shop = shops[i];
                Brand b = shop.File != null ? state.Brand(shop.File.BrandId) : null;
                TableRow row = Row("shop:" + shop.Place);
                row.Kind = b != null ? BrandsTables.IsMember : shop.Chain != null ? BrandsTables.IsChainShop : BrandsTables.IsPlain;
                row.Kind2 = KindOf(districts, BrandsText.District(shop.District));
                row.Set(0, shop.Name);
                row.Set(1, you);
                row.Set(2, b != null ? b.Name : shop.Chain != null ? BrandsText.Chain(shop.Chain.Chain) : string.Empty);
                row.Set(3, BrandsText.District(shop.District));
                if (b != null)
                {
                    row.Set(4, BrandsText.Money(shop.Takings), shop.Takings);
                    row.Set(6, Standards(shop.File.Standards), shop.File.Standards, b.Minimum > 0 && shop.File.Standards < b.Minimum ? TableRow.Yellow : TableRow.Plain);
                    row.Set(7, BrandsText.T("reallife_brand_status_member"));
                }
                else if (shop.Chain != null)
                {
                    bool breach = shop.Chain.Breach > 0;
                    row.Set(4, BrandsText.Money(shop.Takings), shop.Takings);
                    row.Set(5, BrandsText.Money(-shop.Chain.Average), -shop.Chain.Average);
                    row.Set(6, Standards(shop.Chain.Standards), shop.Chain.Standards, shop.Chain.Standards < BrandCatalogue.ChainStandards[shop.Chain.Chain] ? TableRow.Yellow : TableRow.Plain);
                    row.Set(7, breach ? BrandsText.T("reallife_brand_row_breach_days", "days", shop.Chain.Breach.ToString()) : BrandsText.T("reallife_brand_status_chain"), breach ? TableRow.Red : TableRow.Plain);
                }
                else
                {
                    row.Set(7, BrandsText.T("reallife_brand_status_plain"));
                }
                picked = Is(row, ref selected) ? shop : picked;
            }
            for (int i = 0; i < state.Franchises.Count; i++)
            {
                Franchise f = state.Franchises[i];
                Brand b = state.Brand(f.BrandId);
                TableRow row = Row("fr:" + f.Id);
                row.Kind = BrandsTables.IsFranchised;
                row.Kind2 = KindOf(districts, BrandsText.District(f.District));
                row.Set(0, BrandsText.Shop(b, f));
                row.Set(1, f.Name);
                row.Set(2, b != null ? b.Name : "?");
                row.Set(3, BrandsText.District(f.District));
                row.Set(4, BrandsText.Money(f.Takings), f.Takings);
                row.Set(5, BrandsText.Money(f.Royalty), f.Royalty);
                if (f.Seen < 0)
                {
                    row.Set(6, BrandsText.T("reallife_brand_row_not_seen"));
                }
                else
                {
                    row.Set(6, Standards(f.Seen), f.Seen, b != null && f.Seen < b.Minimum ? TableRow.Yellow : TableRow.Plain);
                }
                row.Set(7, f.Ask != Ask.None ? BrandsText.Ask(f.Ask) : f.Trouble ? BrandsText.T("reallife_brand_status_trouble") : BrandsText.T("reallife_brand_row_in_order"),
                    f.Ask != Ask.None || f.Trouble ? TableRow.Red : TableRow.Plain);
                franchise = Is(row, ref selected) ? f : franchise;
            }
            if (_rows.Count == 0)
            {
                Say("none", BrandsText.T("reallife_brand_row_no_shop"));
            }
            if (picked != null)
            {
                WriteShop(box, state, picked, day);
            }
            else if (franchise != null)
            {
                WriteFranchise(box, state, franchise, day);
            }
            else
            {
                box.Title(BrandsText.T("reallife_brand_tab_shops"));
                box.Note(BrandsText.T("reallife_brand_shops_intro"));
                box.Section("reallife_look_sec_how");
                box.Note(BrandsText.Fill("reallife_brand_shops_standards", "satisfaction", BrandCatalogue.WeightSatisfaction.ToString(), "clean", BrandCatalogue.WeightClean.ToString(),
                    "stock", BrandCatalogue.WeightStock.ToString(), "skill", BrandCatalogue.WeightSkill.ToString()));
                box.Note(BrandsText.T("reallife_brand_shops_franchised"));
            }
            return BrandsTables.Shops;
        }

        private void WriteShop(InfoBox box, BrandsState state, BrandShop shop, int day)
        {
            box.Title(shop.Name);
            box.Subtitle(BrandsText.T("reallife_look_type_in", "type", BrandsText.Type(shop.Type), "district", BrandsText.District(shop.District)));
            if (shop.File != null)
            {
                WriteMember(box, state, shop);
            }
            else if (shop.Chain != null)
            {
                WriteChainShop(box, shop);
            }
            else
            {
                WritePlainShop(box, state, shop);
            }
        }

        // A shop under a brand of the player's: its standards and what the brand brings it, each from its parts.
        private void WriteMember(InfoBox box, BrandsState state, BrandShop shop)
        {
            Member m = shop.File;
            Brand b = state.Brand(m.BrandId);
            string place = shop.Place;
            box.Chip(b != null ? b.Name : "?");
            box.Line(BrandsText.Fill("reallife_brand_member_of", "brand", b != null ? b.Name : "?", "district", BrandsText.District(m.District), "day", m.Joined.ToString()));
            box.Section("reallife_brand_col_standards");
            box.Meter("reallife_brand_col_standards", m.Standards / 100f, Standards(m.Standards), b != null && m.Standards < b.Minimum ? CellTone.Red : CellTone.Green);
            box.RowPair("reallife_look_satisfaction", m.Satisfaction.ToString(), "reallife_look_clean", m.Clean.ToString());
            box.RowPair("reallife_look_stock", m.Stock + "%", "reallife_look_skill", m.Skill.ToString());
            box.Note(BrandsText.Fill("reallife_brand_member_weights", "satisfaction", BrandCatalogue.WeightSatisfaction.ToString(), "clean", BrandCatalogue.WeightClean.ToString(),
                "stock", BrandCatalogue.WeightStock.ToString(), "skill", BrandCatalogue.WeightSkill.ToString()));
            if (b != null)
            {
                box.Section("reallife_look_sec_customers");
                if (!_director.Promoted(shop))
                {
                    box.Note(BrandsText.T(EmpireMotors.IsDealership(shop.Reg) ? "reallife_brand_member_empire" : "reallife_brand_member_no_figure"));   // (Core/EmpireMotors)
                }
                else
                {
                    box.Line(BrandsText.Fill("reallife_brand_member_name", "percent", BrandsText.Percent(m.Rec), "points", BrandsText.Points(BrandRules.Effective(b, m.District)), "most", BrandCatalogue.RecFootfallMost.ToString()));
                    box.Line(m.PriceLoss < 0
                        ? BrandsText.Fill("reallife_brand_member_prices", "loss", BrandsText.Percent(m.PriceLoss), "back", BrandsText.Percent(m.Forgiven), "most", BrandRules.Tolerance(b.Standards).ToString())
                        : BrandsText.T("reallife_brand_member_prices_none", "most", BrandRules.Tolerance(b.Standards).ToString()));
                    if (m.Cannibal > 0)
                    {
                        box.Line(BrandsText.Fill("reallife_brand_member_cannibal", "percent", BrandsText.Percent(-m.Cannibal), "metres", BrandCatalogue.CannibalRadius.ToString()));
                    }
                    box.Line(BrandsText.Fill("reallife_brand_member_all", "percent", BrandsText.Percent(shop.Asked), "cap", BrandCatalogue.FootfallMost.ToString()), CellTone.Green);
                }
                if (_director.Kept(shop))
                {
                    box.Line(b.Local
                        ? BrandsText.Fill("reallife_brand_member_kept_local", "business", _director.ShopName(b.Flagship), "share", BrandsText.Share(_director.KeptFactor(place)))
                        : BrandsText.T("reallife_brand_member_kept", "business", _director.ShopName(b.Flagship)));
                    if (_director.ManagerWatches(shop))
                    {
                        box.Note(BrandsText.T("reallife_brand_kept_manager", "district", BrandsText.District(shop.District)));
                    }
                }
            }
            var buttons = box.ButtonRow();
            string name = shop.Name;
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_brand_member_leave", () => Confirm("reallife_brand_confirm_leave_title", "reallife_brand_confirm_leave", new { business = name }, () => _director.Leave(place), true));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_brand_back", Back);
        }

        // A shop under a city chain: what the chain asks, how the shop stands, what it pays and what leaving costs.
        private void WriteChainShop(InfoBox box, BrandShop shop)
        {
            Taken t = shop.Chain;
            int chain = t.Chain;
            string place = shop.Place, name = shop.Name, chainName = BrandsText.Chain(chain);
            bool breach = FranchiseRules.ChainBreach(chain, t.Standards, t.Prices);
            box.Chip(chainName, breach || t.Breach > 0 ? CellTone.Red : CellTone.Plain);
            box.Line(BrandsText.Fill("reallife_brand_chain_of", "chain", chainName, "day", t.From.ToString(), "percent", BrandsText.Percent(FranchiseRules.ChainFootfall(chain)), "royalty", BrandsText.Bp(BrandCatalogue.ChainRoyaltyBp[chain])));
            box.Section("reallife_look_sec_what_it_asks");
            box.Meter("reallife_brand_col_standards", t.Standards / 100f, Standards(t.Standards), t.Standards < BrandCatalogue.ChainStandards[chain] ? CellTone.Red : CellTone.Green);
            box.Row("reallife_look_minimum", BrandCatalogue.ChainStandards[chain].ToString());
            box.Line(t.Prices > 0
                ? BrandsText.Fill("reallife_brand_chain_prices", "prices", BrandsText.Share(t.Prices), "least", BrandsText.Share(BrandCatalogue.ChainPriceLeast[chain]), "most", BrandsText.Share(BrandCatalogue.ChainPriceMost[chain]))
                : BrandsText.T("reallife_brand_chain_no_prices"));
            box.Line(breach || t.Breach > 0
                ? BrandsText.Fill("reallife_brand_chain_breach", "days", t.Breach.ToString(), "warn", BrandCatalogue.ChainWarnDays.ToString(), "fine", BrandCatalogue.ChainFineDays.ToString(), "end", BrandCatalogue.ChainEndDays.ToString(),
                    "amount", BrandsText.Money(FranchiseRules.ChainFine(t, BrandsDirector.Index)))
                : BrandsText.T("reallife_brand_chain_good"), breach || t.Breach > 0 ? CellTone.Red : CellTone.Green);
            int exit = _director.ChainExit(t);
            box.Section("reallife_look_sec_money");
            box.RowPair("reallife_look_ordinary_day", BrandsText.Money(t.Average), "reallife_brand_col_paid", BrandsText.Money(t.Paid));
            box.Row("reallife_look_leaving_costs", BrandsText.Money(exit));
            bool kept = t.Keep;
            box.Section("reallife_look_sec_prices");
            box.Note(kept
                ? BrandsText.Fill("reallife_brand_chain_kept", "district", BrandsText.District(shop.District), "least", BrandsText.Share(KeptPrices.Least(chain)), "most", BrandsText.Share(KeptPrices.Most(chain)))
                : BrandsText.T("reallife_brand_chain_keep_about"));
            if (kept && _director.ManagerWatches(shop))
            {
                box.Note(BrandsText.T("reallife_brand_kept_manager", "district", BrandsText.District(shop.District)));
            }
            bool switched = box.Switch("reallife_brand_chain_keep", kept, on =>
            {
                _director.KeepChainPrices(place, on);
                Refresh();
            });
            var buttons = box.ButtonRow();
            if (!switched)
            {
                InfoBox.Button(buttons, InfoBox.Positive, kept ? "reallife_brand_chain_keep_off" : "reallife_brand_chain_keep_on", () => _director.KeepChainPrices(place, !kept));
            }
            if (!kept)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_brand_chain_set_prices", () => Confirm("reallife_brand_confirm_chain_prices_title", "reallife_brand_confirm_chain_prices", new { business = name, chain = chainName },
                    () => _director.ChainPrices(place), false));
            }
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_brand_chain_leave", () => Confirm("reallife_brand_confirm_chain_leave_title", "reallife_brand_confirm_chain_leave",
                new { business = name, chain = chainName, amount = BrandsText.Money(exit) }, () => _director.LeaveChain(place), true));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_brand_back", Back);
        }

        // A shop under nothing: make a brand of it, join a brand of its type, or take a city chain's franchise.
        private void WritePlainShop(InfoBox box, BrandsState state, BrandShop shop)
        {
            string place = shop.Place, name = shop.Name;
            box.Note(BrandsText.T("reallife_brand_plain", "type", BrandsText.Type(shop.Type), "district", BrandsText.District(shop.District)));
            _joinable.Clear();
            for (int i = 0; i < state.Brands.Count; i++)
            {
                if (string.Equals(state.Brands[i].Type, shop.Type, StringComparison.Ordinal))
                {
                    _joinable.Add(state.Brands[i]);
                }
            }
            Brand join = null;
            if (_joinable.Count > 0)
            {
                int at = BrandCatalogue.Clamp(_joinBrand, 0, _joinable.Count - 1);
                box.Section("reallife_look_sec_join");
                box.Note(BrandsText.T("reallife_brand_plain_join"));
                Choose(box, _joinable.Count, at, i => _joinable[i].Name, i => _joinBrand = i);
                join = _joinable[at];
            }
            int chain = BrandCatalogue.Clamp(_chain, 0, BrandCatalogue.Chains - 1);
            box.Section("reallife_look_sec_chain");
            box.Note(BrandsText.T("reallife_brand_plain_chain"));
            Choose(box, BrandCatalogue.Chains, chain, BrandsText.Chain, i => _chain = i);
            int fee = FranchiseRules.ChainFee(chain, BrandsDirector.Index);
            box.Line(BrandsText.Fill("reallife_brand_plain_chain_quote", "fee", BrandsText.Money(fee), "royalty", BrandsText.Bp(BrandCatalogue.ChainRoyaltyBp[chain]), "percent", BrandsText.Percent(FranchiseRules.ChainFootfall(chain)),
                "standards", BrandCatalogue.ChainStandards[chain].ToString(), "least", BrandsText.Share(BrandCatalogue.ChainPriceLeast[chain]), "most", BrandsText.Share(BrandCatalogue.ChainPriceMost[chain])));
            var buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_brand_plain_make", () => _director.CreateBrand(place));
            int chosen = chain;
            string chainName = BrandsText.Chain(chain);
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_brand_plain_take", () => Confirm("reallife_brand_confirm_chain_title", "reallife_brand_confirm_chain",
                new { business = name, chain = chainName, amount = BrandsText.Money(fee) }, () => _director.TakeChain(place, chosen), false));
            var more = box.ButtonRow();
            if (join != null)
            {
                int brand = join.Id;
                InfoBox.Button(more, InfoBox.Positive, "reallife_brand_plain_join_button", () => _director.Join(brand, place));
            }
            InfoBox.Button(more, InfoBox.Additional, "reallife_brand_back", Back);
        }
    }
}
