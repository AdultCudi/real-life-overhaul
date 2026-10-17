using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// The director's part for kept prices (<see cref="KeptPrices"/>): a chain's shop kept inside the chain's range, or
    /// a member shop kept on the flagship's prices, each by the player's switch. On the day event every kept shop is
    /// written afresh, before the chain looks at its prices. Each hour one round compares each price with what was
    /// last written (a dictionary look-up a product, nothing allocated) and puts back a price something else moved,
    /// at most <see cref="BrandCatalogue.KeepPerRound"/> shops a round. Switched off, prices stay as they stand.
    /// </summary>
    public sealed partial class BrandsDirector
    {
        private readonly Dictionary<string, KeptBook> _books = new Dictionary<string, KeptBook>(16, StringComparer.Ordinal);
        private int _keepFrom;   // the shop the next hourly round starts with

        /// <summary>True for a shop whose prices are kept: a chain's shop with its switch on, or a member shop (not the flagship) of a brand that keeps them.</summary>
        internal bool Kept(BrandShop shop)
        {
            BrandShop from;
            Brand b;
            return Kept(shop, out b, out from);
        }

        private bool Kept(BrandShop shop, out Brand b, out BrandShop from)
        {
            b = null;
            from = null;
            if (shop == null || shop.Reg == null)
            {
                return false;
            }
            if (shop.Chain != null)
            {
                return shop.Chain.Keep;
            }
            b = shop.File != null ? _state.Brand(shop.File.BrandId) : null;
            if (b == null || !b.Keep || string.Equals(b.Flagship, shop.Place, StringComparison.Ordinal))
            {
                return false;
            }
            from = ShopAt(b.Flagship);
            return from != null && from.Reg != null;
        }

        private KeptBook Book(string place)
        {
            KeptBook book;
            if (!_books.TryGetValue(place, out book))
            {
                book = new KeptBook();
                _books.Add(place, book);
            }
            return book;
        }

        /// <summary>
        /// One round over the kept shops: on the day event every one is written afresh; on the hour only those whose
        /// prices are not as they should stand, a capped number, the next round starting with the first left.
        /// </summary>
        private void KeepRound(bool day)
        {
            int count = _shops.Count, written = 0, prices = 0, outside = 0, kept = 0;
            int next = -1;
            for (int k = 0; k < count; k++)
            {
                int i = (_keepFrom + k) % count;
                BrandShop shop = _shops[i];
                Brand b;
                BrandShop from;
                if (!Kept(shop, out b, out from))
                {
                    continue;
                }
                kept++;
                KeptBook book = Book(shop.Place);
                if (!day && Look(shop, from, book) == PriceLook.Same)
                {
                    continue;
                }
                if (!day && written >= BrandCatalogue.KeepPerRound)
                {
                    next = next < 0 ? i : next;
                    continue;
                }
                int moved;
                int changed = shop.Chain != null ? KeepChain(shop, book, day, out moved) : KeepMember(shop, b, from, book, out moved);
                written++;
                prices += changed;
                outside += moved;
                if (moved > 0)
                {
                    PutBack(shop, moved);
                }
            }
            _keepFrom = next >= 0 ? next : 0;
            if (Verbose && (written > 0 || day))
            {
                Logger?.Info("Brands check (kept prices, " + (day ? "the day" : "the hour") + "): " + kept + " kept shop(s), " + written + " written, " + prices + " price(s) changed, "
                             + outside + " of them put back after a change by something else"
                             + (next >= 0 ? "; the round's cap of " + BrandCatalogue.KeepPerRound + " was reached, the rest wait for the next" : string.Empty) + ": PASS");
            }
        }

        // How a kept shop's prices stand against what was written: the worst of its products. Nothing allocated.
        private PriceLook Look(BrandShop shop, BrandShop from, KeptBook book)
        {
            BuildingRegistration reg = shop.Reg;
            List<RetailPrice> prices = reg.retailPrices;
            PriceLook look = PriceLook.Same;
            for (int i = 0; prices != null && i < prices.Count && look != PriceLook.Moved; i++)
            {
                RetailPrice p = prices[i];
                if (p == null || string.IsNullOrEmpty(p.itemName))
                {
                    continue;
                }
                float want;
                if (shop.Chain != null)
                {
                    if (BrandsGame.Market(p.itemName) <= 0f)
                    {
                        continue;
                    }
                    float written;
                    want = book.Written.TryGetValue(p.itemName, out written) ? written : p.price;
                }
                else
                {
                    float source;
                    if (!BrandsGame.PriceOf(from.Reg, p.itemName, out source) || source <= 0f)
                    {
                        continue;
                    }
                    want = KeptPrices.AtShare(source, book.Factor);
                }
                look = KeptPrices.Worse(look, KeptPrices.Look(book, p.itemName, p.price, want));
            }
            List<string> shelves = shop.Chain != null ? reg.cachedAvailableProducts : null;
            for (int i = 0; shelves != null && i < shelves.Count && look != PriceLook.Moved; i++)
            {
                // A product on the shelves with no price: one just stocked, or one whose price something took away.
                float price;
                string item = shelves[i];
                if (!string.IsNullOrEmpty(item) && !BrandsGame.PriceOf(reg, item, out price) && BrandsGame.Market(item) > 0f)
                {
                    look = KeptPrices.Worse(look, book.Written.ContainsKey(item) ? PriceLook.Moved : PriceLook.New);
                }
            }
            return look;
        }

        // A chain's shop: every product with a market price inside the chain's range. On the day each is worked out
        // afresh; on the hour one already written is put back at what was written, and only a new one is worked out.
        // Returns how many prices changed.
        private int KeepChain(BrandShop shop, KeptBook book, bool day, out int moved)
        {
            moved = 0;
            BuildingRegistration reg = shop.Reg;
            int chain = shop.Chain.Chain, changed = 0;
            string district = shop.District;
            int index = BrandsGame.DistrictIndex(district), tolerance = CityEconomy.Bears(district);
            List<RetailPrice> prices = reg.retailPrices;
            int listed = prices != null ? prices.Count : 0;
            for (int i = 0; i < listed; i++)
            {
                RetailPrice p = prices[i];
                float market = p != null ? BrandsGame.Market(p.itemName) : 0f;
                if (market <= 0f)
                {
                    continue;
                }
                float written;
                bool had = book.Written.TryGetValue(p.itemName, out written);
                if (had && Math.Abs(p.price - written) >= KeptPrices.Cent)
                {
                    moved++;
                }
                float want = had && !day ? written : ChainPrice(p.itemName, district, market, chain, index, tolerance);
                changed += BrandsGame.KeepPrice(reg, p.itemName, want, district, false) ? 1 : 0;
                KeptPrices.Wrote(book, p.itemName, want);
            }
            List<string> shelves = reg.cachedAvailableProducts;
            for (int i = 0; shelves != null && i < shelves.Count; i++)
            {
                string item = shelves[i];
                float price, market = BrandsGame.Market(item);
                if (market <= 0f || BrandsGame.PriceOf(reg, item, out price))
                {
                    continue;
                }
                float written;
                bool had = book.Written.TryGetValue(item, out written);
                moved += had ? 1 : 0;
                float want = had && !day ? written : ChainPrice(item, district, market, chain, index, tolerance);
                changed += BrandsGame.KeepPrice(reg, item, want, district, true) ? 1 : 0;
                KeptPrices.Wrote(book, item, want);
            }
            shop.Chain.Prices = BrandsGame.PriceRatio(reg);
            return changed;
        }

        private static float ChainPrice(string item, string district, float market, int chain, int index, int tolerance)
        {
            bool monopoly;
            int demand = BrandsGame.Demand(item, district, out monopoly);
            int target = KeptPrices.ChainTarget(chain, KeptPrices.Bears(index, tolerance, monopoly), demand, BrandsGame.Ceiling(item, district, market), BrandsGame.Cost(item, market));
            return KeptPrices.ChainPrice(market, target, chain);
        }

        // A member shop: the flagship's price for each product both sell, times the district's adjustment. Returns how many prices changed.
        private int KeepMember(BrandShop shop, Brand b, BrandShop from, KeptBook book, out int moved)
        {
            moved = 0;
            BuildingRegistration reg = shop.Reg;
            book.Factor = KeptPrices.LocalFactor(b.Local, KeptPrices.Bears(BrandsGame.DistrictIndex(shop.District), CityEconomy.Bears(shop.District), false),
                KeptPrices.Bears(BrandsGame.DistrictIndex(from.District), CityEconomy.Bears(from.District), false));
            List<RetailPrice> prices = reg.retailPrices;
            int changed = 0, listed = prices != null ? prices.Count : 0;
            for (int i = 0; i < listed; i++)
            {
                RetailPrice p = prices[i];
                float source;
                if (p == null || string.IsNullOrEmpty(p.itemName) || !BrandsGame.PriceOf(from.Reg, p.itemName, out source) || source <= 0f)
                {
                    continue;
                }
                float written;
                if (book.Written.TryGetValue(p.itemName, out written) && Math.Abs(p.price - written) >= KeptPrices.Cent)
                {
                    moved++;
                }
                float want = KeptPrices.AtShare(source, book.Factor);
                changed += BrandsGame.KeepPrice(reg, p.itemName, want, shop.District, false) ? 1 : 0;
                KeptPrices.Wrote(book, p.itemName, want);
            }
            return changed;
        }

        // Something else changed a kept price and it was put back: told once for each shop, written to the log each time.
        private void PutBack(BrandShop shop, int moved)
        {
            bool told = shop.Chain != null ? shop.Chain.Told : shop.File != null && shop.File.Told;
            if (!told || Verbose)
            {
                Logger?.Info("Brands: " + moved + " price(s) at " + shop.Name
                             + " had been changed by something other than this mod (the game's price screen, its pricing manager or another mod) and were put back; its prices are kept.");
            }
            if (told)
            {
                return;
            }
            if (shop.Chain != null)
            {
                shop.Chain.Told = true;
            }
            else if (shop.File != null)
            {
                shop.File.Told = true;
            }
            Tell(NotificationType.Info, "reallife_brand_note_kept_back", new Dictionary<string, string> { { "business", shop.Name } }, TabShops, "shop:" + shop.Place);
        }

        /// <summary>A chain's shop: its prices kept inside the chain's range, or no longer.</summary>
        public void KeepChainPrices(string place, bool on)
        {
            Act("keeping a chain's prices", () =>
            {
                BrandShop shop = ShopAt(place);
                if (shop == null || shop.Chain == null || shop.Chain.Keep == on)
                {
                    return;
                }
                shop.Chain.Keep = on;
                _books.Remove(place);
                int changed = 0, moved;
                if (on)
                {
                    changed = KeepChain(shop, Book(place), true, out moved);
                }
                Logger?.Info("Brands: the prices of " + shop.Name + " are " + (on ? "kept inside what " + BrandsText.Chain(shop.Chain.Chain) + " asks (" + changed
                                                                                    + " changed; they stand at " + shop.Chain.Prices + " thousandths of the market's)" : "no longer kept") + ".");
                Say(NotificationType.Success, on ? "reallife_brand_note_kept_chain" : "reallife_brand_note_kept_off", new Dictionary<string, string>
                {
                    { "business", shop.Name }, { "count", changed.ToString() }
                });
                Changed(false);
            });
        }

        /// <summary>A brand: its member shops kept on the flagship's prices, or no longer.</summary>
        public void KeepBrandPrices(int brandId, bool on)
        {
            Act("keeping a brand's prices", () =>
            {
                Brand b = _state.Brand(brandId);
                if (b == null || b.Keep == on)
                {
                    return;
                }
                b.Keep = on;
                int changed = KeepBrandNow(b);
                Logger?.Info("Brands: the member shops of '" + b.Name + "' are " + (on ? "kept on the flagship's prices"
                                                                                       + (b.Local ? ", adjusted by district within " + BrandCatalogue.LocalBand + " thousandths" : string.Empty)
                                                                                       + " (" + changed + " changed)" : "no longer kept on the flagship's prices") + ".");
                Say(NotificationType.Success, on ? "reallife_brand_note_kept_brand" : "reallife_brand_note_kept_brand_off", new Dictionary<string, string>
                {
                    { "brand", b.Name }, { "count", changed.ToString() }
                });
                Changed(false);
            });
        }

        /// <summary>A brand that keeps its prices: the district's adjustment on or off.</summary>
        public void KeepBrandLocal(int brandId, bool on)
        {
            Act("a brand's district adjustment", () =>
            {
                Brand b = _state.Brand(brandId);
                if (b == null || b.Local == on)
                {
                    return;
                }
                b.Local = on;
                int changed = KeepBrandNow(b);
                Logger?.Info("Brands: the district adjustment of '" + b.Name + "' is " + (on ? "on" : "off") + "; " + changed + " price(s) changed.");
                Changed(false);
            });
        }

        // The brand's member shops written afresh now; none while it does not keep them. Returns how many prices changed.
        private int KeepBrandNow(Brand b)
        {
            int changed = 0;
            for (int i = 0; i < _shops.Count; i++)
            {
                BrandShop shop = _shops[i];
                if (shop.File == null || shop.File.BrandId != b.Id)
                {
                    continue;
                }
                _books.Remove(shop.Place);
                Brand of;
                BrandShop from;
                int moved;
                if (Kept(shop, out of, out from))
                {
                    changed += KeepMember(shop, of, from, Book(shop.Place), out moved);
                }
            }
            return changed;
        }

        /// <summary>The district's adjustment in use at a kept member shop, thousandths of the flagship's prices (1000 before the first round).</summary>
        internal int KeptFactor(string place)
        {
            KeptBook book;
            return place != null && _books.TryGetValue(place, out book) ? book.Factor : 1000;
        }

        /// <summary>True while one of the game's own pricing managers sets prices in the shop's district, which the keeping puts back.</summary>
        internal bool ManagerWatches(BrandShop shop)
        {
            return shop != null && BrandsGame.ManagerWatches(shop.District);
        }
    }
}
