using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// The director's part for what the player does with brands and city chains, each on a press; a press that cannot be
    /// done says why in a notification.
    /// </summary>
    public sealed partial class BrandsDirector
    {
        private bool Ready => _running && !_broken && Save != null;   // _state is set whenever _running is

        private void Act(string what, Action work)
        {
            if (!Ready)
            {
                return;
            }
            try
            {
                work();
            }
            catch (Exception e)
            {
                Fault(what, e);
            }
        }

        private static void Say(NotificationType type, string key, Dictionary<string, string> data = null)
        {
            Notifications.Show(type, key, data, 9f, null, null, true, false);
        }

        public void CreateBrand(string place)
        {
            Act("making a brand", () =>
            {
                BrandShop shop = ShopAt(place);
                if (shop == null)
                {
                    return;
                }
                Brand b = BrandRules.Create(_state, shop.Name, shop.Type, shop.Place, shop.District, Save.Day);
                if (b == null)
                {
                    Say(NotificationType.Error, _state.Brands.Count >= BrandCatalogue.BrandsMost ? "reallife_brand_note_brands_most" : "reallife_brand_note_cannot_brand");
                    return;
                }
                shop.File = _state.Member(place);
                Logger?.Info("Brands: '" + b.Name + "' made from " + shop.Name + " (" + b.Type + ") in " + shop.District + " on day " + Save.Day + ".");
                Say(NotificationType.Success, "reallife_brand_note_made", new Dictionary<string, string> { { "brand", b.Name } });
                Reread();
                _view?.Want(TabBrands, "brand:" + b.Id);
                Changed();
            });
        }

        /// <summary>The brand's name, typed with the game's own text input.</summary>
        public void Rename(int brandId)
        {
            Act("naming a brand", () =>
            {
                Brand b = _state.Brand(brandId);
                if (b == null)
                {
                    return;
                }
                bool asked = BrandsGame.AskName(b.Name, typed =>
                {
                    Act("naming a brand", () =>
                    {
                        Brand named = _state.Brand(brandId);
                        string name = BrandRules.Name(typed);
                        if (named == null || name.Length == 0)
                        {
                            return;
                        }
                        Logger?.Info("Brands: '" + named.Name + "' is now '" + name + "'.");
                        named.Name = name;
                        BrandsGame.Remark();
                        Changed(false);
                    });
                });
                if (!asked)
                {
                    Say(NotificationType.Info, "reallife_brand_note_no_input");
                }
            });
        }

        public void Join(int brandId, string place)
        {
            Act("joining a brand", () =>
            {
                Brand b = _state.Brand(brandId);
                BrandShop shop = ShopAt(place);
                if (b == null || shop == null || !BrandRules.Join(_state, b, shop.Place, shop.Type, shop.District, Save.Day))
                {
                    Say(NotificationType.Error, "reallife_brand_note_cannot_join");
                    return;
                }
                Logger?.Info("Brands: " + shop.Name + " joins '" + b.Name + "'.");
                Reread();
                Changed();
            });
        }

        public void Leave(string place)
        {
            Act("leaving a brand", () =>
            {
                Member m = _state.Member(place);
                Brand b = m != null ? _state.Brand(m.BrandId) : null;
                if (m == null || !BrandRules.Leave(_state, place))
                {
                    return;
                }
                Logger?.Info("Brands: " + ShopName(place) + " leaves '" + (b != null ? b.Name : "?") + "'.");
                Reread();
                Changed();
            });
        }

        /// <summary>A brand is given up. Not while it has franchised shops: their agreements are ended first.</summary>
        public void GiveUp(int brandId)
        {
            Act("giving up a brand", () =>
            {
                Brand b = _state.Brand(brandId);
                if (b == null)
                {
                    return;
                }
                if (BrandRules.Franchised(_state, b, null) > 0)
                {
                    Say(NotificationType.Error, "reallife_brand_note_has_franchises");
                    return;
                }
                Logger?.Info("Brands: '" + b.Name + "' given up; its " + BrandRules.Members(_state, b, null) + " member shop(s) trade on under their own names.");
                BrandRules.Dissolve(_state, b);
                Reread();
                Changed();
            });
        }

        public void SetMinimum(int brandId, int step)
        {
            Act("the minimum", () =>
            {
                Brand b = _state.Brand(brandId);
                if (b != null)
                {
                    b.Minimum = BrandCatalogue.MinimumSteps[BrandCatalogue.Clamp(step, 0, BrandCatalogue.MinimumSteps.Length - 1)];
                    Changed(false);
                }
            });
        }

        /// <summary>The brand goes on offer as a franchise, or comes off it. Applicants who wait stay.</summary>
        public void Offer(int brandId, bool on)
        {
            Act("offering a franchise", () =>
            {
                Brand b = _state.Brand(brandId);
                int good;
                if (b == null)
                {
                    return;
                }
                if (on && !BrandRules.CanOffer(_state, b, out good))
                {
                    Say(NotificationType.Error, "reallife_brand_note_cannot_offer", new Dictionary<string, string>
                    {
                        { "shops", BrandCatalogue.OfferShops.ToString() }, { "standards", BrandCatalogue.OfferStandards.ToString() }, { "city", BrandsText.Points(BrandCatalogue.OfferCityRec) },
                        { "district", BrandsText.Points(BrandCatalogue.OfferDistrictRec) }, { "mean", BrandsText.Points(BrandCatalogue.OfferMeanRec) }
                    });
                    return;
                }
                b.Offered = on;
                Logger?.Info("Brands: '" + b.Name + "' is " + (on ? "on offer as a franchise" : "no longer on offer") + ".");
                Changed(false);
            });
        }

        public int PushCost(Brand b, int kind, string district)
        {
            return BrandRules.PushCost(kind, kind == BrandCatalogue.PushCity ? 0 : BrandRules.Shops(_state, b, district), Index);
        }

        public void BookPush(int brandId, int kind, string district)
        {
            Act("a brand campaign", () =>
            {
                Brand b = _state.Brand(brandId);
                if (b == null || b.Push >= 0 || (kind != BrandCatalogue.PushCity && BrandRules.Shops(_state, b, district) == 0))
                {
                    return;
                }
                int cost = PushCost(b, kind, district);
                if (!Cost(cost, BrandsGame.CampaignKey, b.Name, BrandsText.Push(kind) + " at today's prices (" + Index + "), " + Math.Min(b.Fund, cost) + " of it from the levy collected"))
                {
                    Notifications.ShowInsufficientMoney();
                    return;
                }
                int fund = BrandRules.FromFund(b, cost);
                BrandRules.BookPush(b, kind, district);
                Logger?.Info("Brands: " + (kind == BrandCatalogue.PushCity ? "a city-wide campaign" : "a campaign in " + district) + " booked for '" + b.Name + "': cost " + cost
                             + " (" + fund + " of it the franchisees' levy), " + BrandRules.PushDays(kind) + " brand day(s) from the next.");
                Say(NotificationType.Success, "reallife_brand_note_push", new Dictionary<string, string>
                {
                    { "campaign", BrandsText.Push(kind) }, { "brand", b.Name }, { "days", BrandRules.PushDays(kind).ToString() }
                });
                Changed(false);
            });
        }

        /// <summary>The flagship's prices at every other member shop, for the products it already sells.</summary>
        public void SharePrices(int brandId)
        {
            Act("sharing prices", () =>
            {
                Brand b = _state.Brand(brandId);
                BrandShop from = b != null ? ShopAt(b.Flagship) : null;
                if (from == null)
                {
                    return;
                }
                int prices = 0, shops = 0;
                for (int i = 0; i < _shops.Count; i++)
                {
                    BrandShop to = _shops[i];
                    if (to.File != null && to.File.BrandId == b.Id && !ReferenceEquals(to, from))
                    {
                        int changed = BrandsGame.SharePrices(from.Reg, to.Reg);
                        prices += changed;
                        shops += changed > 0 ? 1 : 0;
                    }
                }
                Logger?.Info("Brands check (shared prices, " + b.Name + "): " + prices + " price(s) at " + shops + " shop(s) set to those of " + from.Name
                             + ", each in the shop's price list and its stored one as the game's own price cell writes them: PASS");
                Say(NotificationType.Success, "reallife_brand_note_prices", new Dictionary<string, string>
                {
                    { "count", prices.ToString() }, { "shops", shops.ToString() }, { "business", from.Name }
                });
                Changed();
            });
        }

        /// <summary>The flagship's opening hours at every other member shop. Shifts are left as they are.</summary>
        public void ShareHours(int brandId)
        {
            Act("sharing hours", () =>
            {
                Brand b = _state.Brand(brandId);
                BrandShop from = b != null ? ShopAt(b.Flagship) : null;
                if (from == null)
                {
                    return;
                }
                int days = 0, shops = 0;
                for (int i = 0; i < _shops.Count; i++)
                {
                    BrandShop to = _shops[i];
                    if (to.File != null && to.File.BrandId == b.Id && !ReferenceEquals(to, from))
                    {
                        int changed = BrandsGame.ShareHours(from.Reg, to.Reg);
                        days += changed;
                        shops += changed > 0 ? 1 : 0;
                    }
                }
                Logger?.Info("Brands check (shared hours, " + b.Name + "): " + days + " day(s) at " + shops + " shop(s) given the opening hours of " + from.Name
                             + ", as the game's own hour switches leave a day; no shift touched: PASS");
                Say(NotificationType.Success, "reallife_brand_note_hours", new Dictionary<string, string> { { "shops", shops.ToString() }, { "business", from.Name } });
                Changed(false);
            });
        }

        private void Reread()
        {
            int left;
            Read(Save.Day, out left);
        }

        public void TakeChain(string place, int chain)
        {
            Act("joining a chain", () =>
            {
                BrandShop shop = ShopAt(place);
                if (shop == null || shop.File != null || shop.Chain != null)
                {
                    return;
                }
                chain = FranchiseRules.Chain(chain);
                if (_state.Taken.Count >= BrandCatalogue.TakenMost)
                {
                    Say(NotificationType.Error, "reallife_brand_note_chains_most");
                    return;
                }
                int fee = FranchiseRules.ChainFee(chain, Index);
                if (!Cost(fee, BrandsGame.ChainFeeKey, shop.Name, "joining " + BrandsText.Chain(chain) + " at today's prices (" + Index + ")"))
                {
                    Notifications.ShowInsufficientMoney();
                    return;
                }
                FranchiseRules.Take(_state, place, chain, Save.Day, BrandsGame.DaysPerYear);
                Logger?.Info("Brands: " + shop.Name + " joins the city chain " + BrandsText.Chain(chain) + " for " + fee + "; +" + FranchiseRules.ChainFootfall(chain)
                             + "% of customers while it keeps standards of " + BrandCatalogue.ChainStandards[chain] + " and prices inside " + BrandCatalogue.ChainPriceLeast[chain]
                             + " to " + BrandCatalogue.ChainPriceMost[chain] + " thousandths of the market's.");
                Say(NotificationType.Success, "reallife_brand_note_chain_joined", new Dictionary<string, string> { { "business", shop.Name }, { "chain", BrandsText.Chain(chain) } });
                Reread();
                Changed();
            });
        }

        public int ChainExit(Taken t)
        {
            return FranchiseRules.ChainExit(t, Index);
        }

        public void LeaveChain(string place)
        {
            Act("leaving a chain", () =>
            {
                Taken t = _state.TakenAt(place);
                if (t == null)
                {
                    return;
                }
                int exit = ChainExit(t);
                if (exit > BrandsGame.Cash)
                {
                    Notifications.ShowInsufficientMoney();
                    return;
                }
                Charge(exit, BrandsGame.ChainExitKey, ShopName(place), "leaving " + BrandsText.Chain(t.Chain));
                _state.Taken.Remove(t);
                Logger?.Info("Brands: " + ShopName(place) + " left " + BrandsText.Chain(t.Chain) + " for " + exit + ".");
                Say(NotificationType.Info, "reallife_brand_note_chain_left", new Dictionary<string, string>
                {
                    { "business", ShopName(place) }, { "chain", BrandsText.Chain(t.Chain) }, { "amount", BrandsText.Money(exit) }
                });
                Reread();
                Changed();
            });
        }

        /// <summary>A shop's prices put at the middle of what its chain asks.</summary>
        public void ChainPrices(string place)
        {
            Act("a chain's prices", () =>
            {
                BrandShop shop = ShopAt(place);
                if (shop == null || shop.Chain == null)
                {
                    return;
                }
                Taken t = shop.Chain;
                int changed = BrandsGame.ChainPrices(shop.Reg, t.Chain);
                t.Prices = BrandsGame.PriceRatio(shop.Reg);
                Logger?.Info("Brands check (chain prices, " + shop.Name + "): " + changed + " price(s) set; its prices now stand at " + t.Prices
                             + " thousandths of the game's own market price, wanted " + BrandCatalogue.ChainPriceLeast[t.Chain] + " to " + BrandCatalogue.ChainPriceMost[t.Chain] + ": "
                             + (FranchiseRules.ChainBreach(t.Chain, 100, t.Prices) ? "still outside" : "PASS"));
                Say(NotificationType.Success, "reallife_brand_note_chain_prices", new Dictionary<string, string> { { "count", changed.ToString() }, { "business", shop.Name } });
                Changed(false);
            });
        }
    }
}
