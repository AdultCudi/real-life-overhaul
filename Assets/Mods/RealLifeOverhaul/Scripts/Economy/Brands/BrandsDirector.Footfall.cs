namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>The director's part for footfall: what each shop's brand or chain asks for, written through business pressure.</summary>
    public sealed partial class BrandsDirector
    {
        // What the brand or the chain asks for at each shop, per cent of customers.
        private void Recount()
        {
            for (int i = 0; i < _shops.Count; i++)
            {
                BrandShop shop = _shops[i];
                Brand b = shop.File != null ? _state.Brand(shop.File.BrandId) : null;
                shop.Asked = b != null ? BrandRules.Apply(_state, b, shop.File)
                    : shop.Chain != null && FranchiseRules.ChainGives(shop.Chain) ? FranchiseRules.ChainFootfall(shop.Chain.Chain) : 0;
            }
        }

        /// <summary>
        /// What a brand or a chain adds to a business's footfall, per cent, asked whenever business pressure writes its
        /// figure: an index look-up. 0 while not running or held for a save.
        /// </summary>
        public static int FootfallOf(BuildingRegistration reg)
        {
            BrandsDirector d = Instance;
            if (d == null || !d._running || d._holding || d._broken || reg == null)
            {
                return 0;
            }
            BrandShop shop = d._regs.First(d._shops, reg, d._read);
            return shop != null ? shop.Asked : 0;
        }

        private static string PlaceOf(BrandShop shop)
        {
            return shop.Place;
        }

        private static BuildingRegistration RegOf(BrandShop shop)
        {
            return shop.Reg;
        }

        private void WriteFootfall(BrandShop shop, bool redraw, bool newDay)
        {
            int own = 0, written = 0;
            bool has = BrandsBridges.Footfall != null && shop.Reg != null && BrandsBridges.Footfall(shop.Reg, redraw, newDay, out own, out written);
            bool applied = has && _running && !_holding && shop.Asked != 0;
            if (Verbose && (applied != shop.Applied || shop.Asked != shop.Last))
            {
                Member m = shop.File;
                Logger?.Info("Brands check (footfall, " + shop.Name + "): " + (shop.Asked > 0 ? "+" : string.Empty) + shop.Asked + "% of customers asked for"
                             + (m != null ? " (the name +" + m.Rec + ", prices given back +" + m.Forgiven + ", near franchises -" + m.Cannibal + "; bounds " + BrandCatalogue.FootfallLeast
                                            + " to +" + BrandCatalogue.FootfallMost + ")" : string.Empty)
                             + (has ? ", promotion " + own + " to " + written + " (with whatever else the mod has there, the sum kept to +15%)"
                                 : ", the game keeps no promotion figure there: nothing written")
                             + (applied ? ": applied" : shop.Applied ? ": restored" : string.Empty) + ": PASS");
            }
            shop.Last = _running && !_holding ? shop.Asked : 0;
            shop.Applied = applied;
        }

        // Every shop where a brand adds something, or did, in one batch: a shop drawn again is drawn once (Core/CustomerDraw).
        private int WriteAll(bool redraw, bool newDay)
        {
            int written = 0;
            CustomerDraw.Hold("the brands' figures");
            try
            {
                for (int i = 0; i < _shops.Count; i++)
                {
                    if (_shops[i].Asked != 0 || _shops[i].Applied)
                    {
                        WriteFootfall(_shops[i], redraw, newDay);
                        written++;
                    }
                }
            }
            finally
            {
                CustomerDraw.Release();
            }
            return written;
        }

        // The shops where what is asked for is not what was last written: their figures, now.
        private void Sync()
        {
            CustomerDraw.Hold("the brands' figures");
            try
            {
                for (int i = 0; i < _shops.Count; i++)
                {
                    if (_shops[i].Asked != _shops[i].Last)
                    {
                        WriteFootfall(_shops[i], true, false);
                    }
                }
            }
            finally
            {
                CustomerDraw.Release();
            }
        }

        /// <summary>True where the game keeps a promotion figure for the business, so that a brand can change its footfall.</summary>
        public bool Promoted(BrandShop shop)
        {
            return shop != null && shop.Reg != null && BrandsBridges.Promoted != null && BrandsBridges.Promoted(shop.Reg);
        }
    }
}
