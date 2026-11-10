using System.Collections.Generic;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>A place of the player's that goods can be brought to.</summary>
    public sealed class SupplyPlaceFact
    {
        public string Key = string.Empty, Name = string.Empty;
        public bool Buyer;                  // somebody who orders stock decides there: the rules of its contracts run by themselves
    }

    /// <summary>One supplier as a page shows it, with its offer to the chosen place for the chosen term and share. Id 0 is the city's own wholesale stores.</summary>
    public sealed class SupplyFirmFact
    {
        public int Id;
        public int Kinds, Level, Reliability, LeadDays, Band, Size, Customers, Brought, Missed, ReopenDay, TroubleKind, TroubleUntil;
        public string OwnerName = string.Empty, Depot = string.Empty;
        public long Min;                    // cents
        public LatePolicy Late;
        public bool Open = true;
        public Refusal Why;
        public long Value, MarketValue;     // cents a delivery
        public int Against = 1000, FirstDay, Lines;
    }

    public sealed class SupplyLineFact
    {
        public string Name = string.Empty;
        public int Units;
        public long Mills;
    }

    /// <summary>One contract as a page shows it.</summary>
    public sealed class SupplyContractFact
    {
        public int Id, Supplier, Weeks, Made, Deliveries, Due, Slip, OutShare = 1000, Failures, NotTaken, Ended;
        public string Place = string.Empty;
        public Arrival Out;
        public Cause Why;
        public ContractState State;
        public long Value, MarketValue, Paid, Credit, Owed, BreakCost;   // cents
        public bool MayLeave, Buyer, Cover, LeaveRule;
        public RenewRule Renew;
        public int RenewLimit;
        public readonly List<SupplyLineFact> Lines = new List<SupplyLineFact>(SupplierCatalogue.LinesMost);

        public bool Running => State == ContractState.Running;
    }

    /// <summary>The market of one kind of goods today.</summary>
    public struct SupplyKindFact
    {
        public int Factor;                  // thousandths of the calm price
        public int Shock;                   // 0 none, 1 a shortage, 2 a glut
        public int StartsIn, Days, Size;    // days until it starts (0: running), days it then lasts, its size in thousandths
    }

    /// <summary>Everything the Suppliers pages read, in plain data: the live source fills it from the director and the samples and checks fill it by hand.</summary>
    public sealed class SupplyFacts
    {
        public bool Ready = true;
        public int Day;
        public long Cash, Owed;             // cents
        public int Place;                   // the chosen place's index
        public int TermStep, ShareStep;     // the chosen term (0: one delivery, then the catalogue's terms) and share of the week's sales
        public readonly List<SupplyPlaceFact> Places = new List<SupplyPlaceFact>(8);
        public readonly List<SupplyFirmFact> Firms = new List<SupplyFirmFact>(10);
        public readonly List<SupplyContractFact> Contracts = new List<SupplyContractFact>(8);
        public readonly SupplyKindFact[] Kinds = new SupplyKindFact[SupplierCatalogue.Kinds];
        public int OnTime, Late, Defaults;
        public readonly List<string> News = new List<string>(6);      // the suppliers' doings, in words
        public readonly List<string> Rivals = new List<string>(3);    // the rival tycoons' supply, in words
        public bool Session;                // a Going Public session: the rivals rest and the city's shops are the host's

        /// <summary>The weeks of a term step: 0 for one delivery at the day's price.</summary>
        public static int Weeks(int termStep)
        {
            return termStep <= 0 ? 0 : SupplierCatalogue.TermWeeks[SupplierCatalogue.Clamp(termStep - 1, 0, SupplierCatalogue.TermWeeks.Length - 1)];
        }

        public SupplyFirmFact Firm(int id)
        {
            for (int i = 0; i < Firms.Count; i++)
            {
                if (Firms[i].Id == id)
                {
                    return Firms[i];
                }
            }
            return null;
        }
    }
}
