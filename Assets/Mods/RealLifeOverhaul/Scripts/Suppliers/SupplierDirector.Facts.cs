using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>What the pages read: the state and the game's figures gathered into <see cref="SupplyFacts"/> when a page is written (a press or a changed revision), never per frame.</summary>
    public sealed partial class SupplierDirector
    {
        private readonly Quote _quote = new Quote();
        private readonly List<ContractLine> _scratch = new List<ContractLine>(32);

        /// <summary>Fills the facts for a page with the chosen place, term and share; false, with Ready false, while the feature does not run.</summary>
        public bool Facts(SupplyFacts f, int place, int termStep, int shareStep)
        {
            f.Places.Clear();
            f.Firms.Clear();
            f.Contracts.Clear();
            f.News.Clear();
            f.Rivals.Clear();
            if (!Running)
            {
                f.Ready = false;
                return false;
            }
            f.Ready = true;
            f.Day = SupplierGame.Today;
            f.Cash = SupplierGame.Spare;   // what may be paid to suppliers: the cash over the investors' floor
            f.Owed = _state.OwedAll();
            f.Session = GoingPublic.InSession;
            f.OnTime = _state.Record.OnTime;
            f.Late = _state.Record.Late;
            f.Defaults = _state.Record.Defaults;
            f.TermStep = SupplierCatalogue.Clamp(termStep, 0, SupplierCatalogue.TermWeeks.Length);
            f.ShareStep = SupplierCatalogue.Clamp(shareStep, 0, SupplierCatalogue.Shares.Length - 1);
            SupplierGame.Places(f.Places, _scratch);
            f.Place = f.Places.Count > 0 ? SupplierCatalogue.Clamp(place, 0, f.Places.Count - 1) : 0;
            string key = f.Places.Count > 0 ? f.Places[f.Place].Key : null;
            for (int k = 0; k < f.Kinds.Length; k++)
            {
                SupplyShock shock = CityEconomy.SupplyShockOf(k, out int startsIn, out int days, out int size);
                f.Kinds[k] = new SupplyKindFact { Factor = CityEconomy.SupplyFactor(k), Shock = (int)shock, StartsIn = startsIn, Days = days, Size = size };
            }
            // The city's own wholesale stores first: the game's, as they are.
            f.Firms.Add(new SupplyFirmFact { Id = SupplierCatalogue.Wholesalers, Kinds = (1 << SupplierCatalogue.Kinds) - 1, Level = 1000, Open = true, Why = Refusal.None });
            int weeks = SupplyFacts.Weeks(f.TermStep);
            for (int i = 0; i < SupplierCatalogue.Firms.Length; i++)
            {
                SupplierDef def = SupplierCatalogue.Firms[i];
                FirmState firm = _state.Firms[i];
                var fact = new SupplyFirmFact
                {
                    Id = def.Id, Kinds = def.Kinds, Level = SupplierRules.Level(def, firm), Reliability = SupplierRules.Reliability(def, firm), LeadDays = def.LeadDays,
                    Band = SupplierRules.HealthBand(firm.Health), Size = firm.Size, Customers = firm.Customers, Brought = firm.Brought, Missed = firm.Missed,
                    ReopenDay = firm.ReopenDay, TroubleKind = firm.InTrouble(f.Day) ? firm.TroubleKind : 0, TroubleUntil = firm.TroubleUntil, Late = def.Late,
                    Open = firm.Open, Min = SupplierRules.AtLevel(def.MinOrder, _world.Level), Depot = SupplierGame.District(firm.Depot),
                    OwnerName = def.Owner >= 0 && !firm.Sold && SupplierBridges.RivalName != null ? SupplierBridges.RivalName(def.Owner) ?? string.Empty : string.Empty
                };
                if (key != null)
                {
                    SupplierEngine.Offer(_state, _world, def.Id, key, weeks, f.ShareStep, _quote);
                    fact.Why = _quote.Why;
                    fact.Value = _quote.Value;
                    fact.MarketValue = _quote.MarketValue;
                    fact.Against = _quote.AgainstMarket;
                    fact.FirstDay = _quote.FirstDay;
                    fact.Lines = _quote.Lines.Count;
                    fact.LeftOut = _quote.LeftOut;
                    fact.LeadDays = _quote.FirstDay > 0 ? _quote.FirstDay - f.Day : def.LeadDays;
                }
                else
                {
                    fact.Why = Refusal.NoGoods;
                }
                f.Firms.Add(fact);
            }
            for (int i = _state.Contracts.Count - 1; i >= 0; i--)
            {
                Contract c = _state.Contracts[i];
                var fact = new SupplyContractFact
                {
                    Id = c.Id, Supplier = c.Supplier, Weeks = c.Weeks, Made = c.Made, Deliveries = c.Deliveries, Due = c.Due, Slip = c.Slip, OutShare = c.OutShare,
                    Failures = c.Failures, NotTaken = c.NotTaken, Ended = c.Ended, Place = c.PlaceName, Out = c.Out, Why = c.Why, State = c.State,
                    Value = SupplierEngine.Value(c), MarketValue = SupplierEngine.MarketValue(c, _world), Paid = c.Paid, Credit = c.Credit, Owed = _state.OwedOn(c.Id),
                    BreakCost = SupplierEngine.BreakCost(c), MayLeave = SupplierEngine.MayLeave(c), Buyer = c.Running && _world.Buyer(c.Place), Cover = c.Cover,
                    LeaveRule = c.LeaveRule, Renew = c.Renew, RenewLimit = c.RenewLimit
                };
                for (int k = 0; k < c.Lines.Count; k++)
                {
                    fact.Lines.Add(new SupplyLineFact { Name = SupplierGame.ItemName(c.Lines[k].Item), Units = c.Lines[k].Units, Mills = c.Lines[k].Mills });
                }
                f.Contracts.Add(fact);
            }
            for (int i = _state.Notes.Count - 1; i >= 0 && f.News.Count < 5; i--)
            {
                f.News.Add(SupplierText.NewsLine(_state.Notes[i]));
            }
            for (int i = 0; i < _state.RivalBooks.Length; i++)
            {
                string line = RivalLine(i);
                if (!string.IsNullOrEmpty(line))
                {
                    f.Rivals.Add(line);
                }
            }
            return true;
        }
    }
}
