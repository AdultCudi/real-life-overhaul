using System;
using System.Collections.Generic;
using RealLifeOverhaul.Suppliers;
using RealLifeOverhaul.Ui;

// The Suppliers app's samples: two businesses, the nine suppliers with their offers to the chosen one, four contracts in the states a page must show, and the market
// calm or short. The business names are placeholders and never ship; the tycoon's name is one of the game's own.
internal static partial class UiSamples
{
    public static readonly string[] SuppliersStates =
    {
        "suppliers", "suppliers_pick", "suppliers_once", "suppliers_refused", "suppliers_wholesale", "suppliers_none", "contracts", "contracts_late", "contracts_leave",
        "contracts_owed", "contracts_ended", "contracts_empty", "market", "market_shortage", "off"
    };

    /// <summary>A recording stand-in for the director: each press says which action it was.</summary>
    public sealed class SupplyStub : ISupplyActions
    {
        public string Last, Place;
        public int Count, A, B, C;

        private void Did(string what, int a, int b, int c, string place = null)
        {
            Last = what;
            Count++;
            A = a;
            B = b;
            C = c;
            Place = place;
        }

        public void Sign(int supplier, string place, int weeks, int shareStep) => Did("sign", supplier, weeks, shareStep, place);
        public void End(int contract) => Did("end", contract, 0, 0);
        public void Leave(int contract) => Did("leave", contract, 0, 0);
        public void PayOwed() => Did("pay", 0, 0, 0);
        public void Rule(int contract, int rule) => Did("rule", contract, rule, 0);
    }

    public static SupplyFacts SuppliersFactsOf(string state, int termStep = SupplyPages.TermDefault, int shareStep = SupplierCatalogue.ShareDefault)
    {
        var f = new SupplyFacts { Day = 212, Cash = 8400000, TermStep = termStep, ShareStep = shareStep };
        if (state == "off")
        {
            f.Ready = false;
            return f;
        }
        if (state != "suppliers_none")
        {
            f.Places.Add(new SupplyPlaceFact { Key = "Bleecker%20Street~12", Name = "Corner Grocer", Buyer = true });
            f.Places.Add(new SupplyPlaceFact { Key = "Mercer%20Street~40", Name = "Mercer Fashion" });
        }
        for (int k = 0; k < f.Kinds.Length; k++)
        {
            f.Kinds[k] = new SupplyKindFact { Factor = k == 0 ? 1080 : k == 4 ? 980 : 1000 };
        }
        if (state == "market_shortage")
        {
            f.Kinds[1] = new SupplyKindFact { Factor = 1280, Shock = 1, StartsIn = 0, Days = 4, Size = 280 };
            f.Kinds[3] = new SupplyKindFact { Factor = 1000, Shock = 1, StartsIn = 1, Days = 6, Size = 200 };
            f.Kinds[2] = new SupplyKindFact { Factor = 880, Shock = 2, StartsIn = 0, Days = 3, Size = 120 };
            f.News.Add("SUPPLY: Penny Barrow Cash and Carry has a drivers' strike. Shops that buy from it expect late and short deliveries until about day 216.");
            f.News.Add("SUPPLY: 14 of the city's shops had a late or short delivery yesterday.");
            f.Rivals.Add("Space Karen's shops buy from Penny Barrow Cash and Carry. Supply has cost them $4,200 more than calm prices this month, with 3 deliveries late or short.");
        }
        f.Firms.Add(new SupplyFirmFact { Id = 0, Kinds = 63, Level = 1000 });
        for (int i = 0; i < SupplierCatalogue.Firms.Length; i++)
        {
            SupplierDef def = SupplierCatalogue.Firms[i];
            bool trades = def.Trades(0) || def.Trades(1);
            var firm = new SupplyFirmFact
            {
                Id = def.Id, Kinds = def.Kinds, Level = def.Level, Reliability = def.Reliability, LeadDays = def.LeadDays, Band = def.Id == 2 ? 1 : 0, Size = 1,
                Customers = 12 + def.Id * 3, Brought = 40 + def.Id, Missed = def.Id == 2 ? 6 : 1, Late = def.Late, Min = def.MinOrder * 100L, Depot = def.Id % 2 == 0 ? "Industry City" : "Midtown",
                OwnerName = def.Owner == 0 ? "Space Karen" : string.Empty, Why = f.Places.Count == 0 ? Refusal.NoGoods : trades ? Refusal.None : Refusal.Kind,
                MarketValue = 186000, Lines = 5, FirstDay = f.Day + def.LeadDays,
                LeftOut = def.Id == 2 && state == "suppliers_pick" ? 3 : 0   // a place that sells more than a contract holds
            };
            int discount = termStep > 0 ? SupplierCatalogue.TermDiscount[termStep - 1] : 0;
            firm.Against = def.Level * (1000 - discount) / 1000;
            firm.Value = firm.MarketValue * firm.Against / 1000;
            if (def.Id == 2 && state == "market_shortage")
            {
                firm.TroubleKind = 1;
                firm.TroubleUntil = 216;
            }
            if (def.Id == 2 && state == "suppliers_refused")
            {
                firm.Why = Refusal.Small;
            }
            if (def.Id == 3 && state == "suppliers_refused")
            {
                firm.Open = false;
                firm.ReopenDay = 230;
                firm.Why = Refusal.Shut;
            }
            f.Firms.Add(firm);
        }
        if (state.StartsWith("contracts", StringComparison.Ordinal) && state != "contracts_empty")
        {
            f.Contracts.Add(SampleContract(4, 1, "Corner Grocer", 4, 2, 214, state == "contracts_late" ? Arrival.Late : Arrival.None, Cause.Weather, ContractState.Running));
            f.Contracts.Add(SampleContract(3, 2, "Corner Grocer", 8, 5, 216, state == "contracts_late" ? Arrival.Short : Arrival.None, Cause.Own, ContractState.Running));
            f.Contracts.Add(SampleContract(2, 5, "Mercer Fashion", 0, 0, 213, Arrival.None, Cause.None, ContractState.Running));
            f.Contracts.Add(SampleContract(1, 4, "Mercer Fashion", 2, 2, 0, Arrival.None, Cause.None, state == "contracts_ended" ? ContractState.Unpaid : ContractState.Done));
            SupplyContractFact first = f.Contracts[0];
            if (state == "contracts_late")
            {
                first.Slip = 2;
            }
            if (state == "contracts_leave")
            {
                first.Failures = SupplierCatalogue.FailuresToLeave;
                first.MayLeave = true;
                first.Credit = 4200;
            }
            if (state == "contracts_owed")
            {
                first.Owed = 96000;
                f.Owed = 96000;
                first.NotTaken = 24;
            }
        }
        f.OnTime = 23;
        f.Late = state == "contracts_owed" ? 2 : 0;
        if (state == "contracts_ended")
        {
            f.Defaults = 1;
            f.Owed = 31000;
        }
        return f;
    }

    private static SupplyContractFact SampleContract(int id, int supplier, string place, int weeks, int made, int due, Arrival arrival, Cause why, ContractState state)
    {
        var c = new SupplyContractFact
        {
            Id = id, Supplier = supplier, Place = place, Weeks = weeks, Made = made, Deliveries = Math.Max(1, weeks), Due = due, Out = arrival, Why = why,
            OutShare = arrival == Arrival.Short ? 700 : 1000, State = state, Ended = state == ContractState.Running ? 0 : 205, Value = 178000, MarketValue = 191000,
            Paid = 178000L * made, BreakCost = 89000, Buyer = place == "Corner Grocer", Renew = id == 4 ? RenewRule.Same : RenewRule.Off, RenewLimit = 2, Cover = id == 4
        };
        c.Lines.Add(new SupplyLineFact { Name = "Apples", Units = 240, Mills = 1250 });
        c.Lines.Add(new SupplyLineFact { Name = "Bread", Units = 180, Mills = 2100 });
        c.Lines.Add(new SupplyLineFact { Name = "Milk", Units = 120, Mills = 1640 });
        return c;
    }

    public static VentModel Suppliers(UiWords t, string state, VentView view = null, ISupplyActions act = null)
    {
        VentWords w = VentWordsOf(t);
        view = view ?? new VentView();
        switch (state)
        {
            case "suppliers_pick": view.Pick = SupplyPages.FirmKey + 2; view.B = 3 + 1; break;
            case "suppliers_once": view.Pick = SupplyPages.FirmKey + 1; view.B = 0 + 1; break;
            case "suppliers_refused": view.Pick = SupplyPages.FirmKey + 2; break;
            case "suppliers_wholesale": view.Pick = SupplyPages.FirmKey + 0; break;
            case "contracts":
            case "contracts_late":
            case "contracts_leave":
            case "contracts_owed":
            case "contracts_empty": view.Tab = SupplyPages.TabContracts; break;
            case "contracts_ended": view.Tab = SupplyPages.TabContracts; view.Pick = SupplyPages.ContractKey + 1; break;
            case "market":
            case "market_shortage": view.Tab = SupplyPages.TabMarket; break;
        }
        if (view.Pick == null && view.Tab == SupplyPages.TabSuppliers && state != "suppliers_none")
        {
            view.Pick = SupplyPages.FirmKey + 1;
        }
        SupplyFacts f = SuppliersFactsOf(state, SupplyPages.TermOf(view.B), SupplyPages.ShareOf(view.C));
        f.Place = Math.Max(0, Math.Min(Math.Max(0, f.Places.Count - 1), view.A));
        var m = new VentModel();
        SupplyPages.Fill(m, view, f, act ?? new SupplyStub(), w);
        return m;
    }
}
