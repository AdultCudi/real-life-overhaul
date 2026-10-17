namespace RealLifeOverhaul.Economy
{
    /// <summary>What the company changes in the game: business pressure's footfall figures, and money as transactions.</summary>
    public sealed partial class CompanyDirector
    {
        // Per cent of customers the company asks for at a business on a day: a function of the files and the day alone.
        private int Footfall(CompanyShop shop, int day)
        {
            return (On(CompanyPart.Staff) ? StaffRules.Footfall(shop.File, day) : 0) + (On(CompanyPart.Marketing) ? CampaignRules.Footfall(shop.File, day) : 0);
        }

        /// <summary>Per cent of customers the company adds at a business now; 0 while stopped or held for a save.</summary>
        public static int FootfallOf(BuildingRegistration reg)
        {
            CompanyDirector d = Instance;
            if (d == null || !d._running || d._holding || d._broken || Save == null || reg == null)
            {
                return 0;
            }
            CompanyShop shop = d._regs.First(d._shops, reg, d._read);   // a look-up: asked for every figure written
            return shop != null ? d.Footfall(shop, Save.Day) : 0;
        }

        private static string PlaceOf(CompanyShop shop)
        {
            return shop.File != null ? shop.File.Place : null;
        }

        private static BuildingRegistration RegOf(CompanyShop shop)
        {
            return shop.Reg;
        }

        private void WriteFootfall(CompanyShop shop, int asked, bool redraw, bool newDay)
        {
            int own = 0, written = 0;
            bool has = CompanyBridges.Footfall != null && shop.Reg != null && CompanyBridges.Footfall(shop.Reg, redraw, newDay, out own, out written);
            bool applied = has && _running && !_holding && asked != 0;
            if (Verbose && (redraw || applied != shop.Applied || asked != shop.Asked))
            {
                Logger?.Info("Company check (footfall, " + shop.Name + "): " + (asked > 0 ? "+" : string.Empty) + asked + "% of customers asked for"
                             + (has ? ", promotion " + own + " to " + written + " (with whatever else the mod has there)"
                                 : ", the game keeps no promotion figure there: nothing written")
                             + (applied ? ": applied" : shop.Applied ? ": restored" : string.Empty) + ": PASS");
            }
            shop.Asked = asked;
            shop.Applied = applied;
        }

        // Every business the company changes or did, as one batch drawn once over the next frames (Core/CustomerDraw).
        private int WriteAll(int day, bool redraw, bool newDay)
        {
            int written = 0;
            CustomerDraw.Hold("the company's figures");
            try
            {
                for (int i = 0; i < _shops.Count; i++)
                {
                    int asked = Footfall(_shops[i], day);
                    if (asked != 0 || _shops[i].Applied)
                    {
                        WriteFootfall(_shops[i], asked, redraw, newDay);
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

        /// <summary>One business's figure, now: the player has just changed something there.</summary>
        internal void Refoot(CompanyShop shop)
        {
            WriteFootfall(shop, Footfall(shop, Save.Day), true, false);
        }

        /// <summary>True where the game keeps a customer figure for the business, so the company can change its footfall.</summary>
        public bool Promoted(CompanyShop shop)
        {
            return shop != null && shop.Reg != null && CompanyBridges.Promoted != null && CompanyBridges.Promoted(shop.Reg);
        }

        /// <summary>A business cost, deductible and counted in the quarter; unless forced, false and nothing taken when cash is short.</summary>
        internal bool Cost(int amount, string key, CompanyShop shop, string name, CostKind kind, int? day, string how, bool force = false)
        {
            bool taken = CompanyGame.Take(amount, key, name, shop, day, (int)kind, force);
            if (taken && amount > 0)
            {
                TaxRules.Note(_state.Tax, kind, amount);
            }
            if (Verbose && amount > 0)
            {
                Logger?.Info("Company check (transaction, " + key + ", " + name + "): " + amount
                             + (taken ? " taken, deductible, counted in quarter " + _state.Tax.Quarter : " could not be taken: the cash does not cover it")
                             + "; " + how + ": PASS");
            }
            return taken;
        }

        /// <summary>A charge that is no business cost (a penalty, a fine, back-tax): never deductible; with 'force', whatever the cash.</summary>
        internal bool Charge(int amount, string key, CompanyShop shop, string name, int? day, bool force, string how)
        {
            bool taken = CompanyGame.Take(amount, key, name, shop, day, -1, force);
            if (Verbose && amount > 0)
            {
                Logger?.Info("Company check (transaction, " + key + ", " + name + "): " + amount
                             + (taken ? " taken" : " could not be taken: the cash does not cover it") + "; " + how + ": PASS");
            }
            return taken;
        }

        /// <summary>Money paid to the player: a claim, a rebate.</summary>
        internal void Pay(int amount, string key, CompanyShop shop, string name, int? day, string how)
        {
            CompanyGame.Give(amount, key, name, shop, day);
            if (Verbose && amount > 0)
            {
                Logger?.Info("Company check (transaction, " + key + ", " + name + "): " + amount + " paid to the player; " + how + ": PASS");
            }
        }
    }
}
