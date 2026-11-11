using System.Collections.Generic;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>The locale keys of the feature's tables of words, by the number each enum is saved as. Game-free: the pages and the checks read them.</summary>
    public static class SupplierKeys
    {
        public const string WholesalersKey = "reallife_supply_firm_0";

        /// <summary>What each supplier is like, in a sentence or two, in the catalogue's order.</summary>
        public static readonly string[] AboutKeys =
        {
            "reallife_supply_firm_about_1", "reallife_supply_firm_about_2", "reallife_supply_firm_about_3", "reallife_supply_firm_about_4", "reallife_supply_firm_about_5",
            "reallife_supply_firm_about_6", "reallife_supply_firm_about_7", "reallife_supply_firm_about_8", "reallife_supply_firm_about_9"
        };

        /// <summary>A supplier's sentence by its number.</summary>
        public static string About(int id) => AboutKeys[SupplierCatalogue.Clamp(id, 1, AboutKeys.Length) - 1];

        public static readonly string[] CauseKeys =
        {
            "reallife_supply_cause_none", "reallife_supply_cause_own", "reallife_supply_cause_trouble", "reallife_supply_cause_cash", "reallife_supply_cause_weather",
            "reallife_supply_cause_streets", "reallife_supply_cause_season", "reallife_supply_cause_shortage"
        };

        public static readonly string[] RefusalKeys =
        {
            "reallife_supply_refuse_none", "reallife_supply_refuse_off", "reallife_supply_refuse_shut", "reallife_supply_refuse_kind", "reallife_supply_refuse_no_goods",
            "reallife_supply_refuse_small", "reallife_supply_refuse_owes", "reallife_supply_refuse_sour", "reallife_supply_refuse_credit", "reallife_supply_refuse_full",
            "reallife_supply_refuse_held"
        };

        public static readonly string[] StateKeys =
        {
            "reallife_supply_state_running", "reallife_supply_state_done", "reallife_supply_state_broken", "reallife_supply_state_left", "reallife_supply_state_failed",
            "reallife_supply_state_unpaid", "reallife_supply_state_closed"
        };

        public static readonly string[] LateKeys = { "reallife_supply_late_lenient", "reallife_supply_late_firm", "reallife_supply_late_strict" };

        public static readonly string[] HealthKeys = { "reallife_supply_health_sound", "reallife_supply_health_stretched", "reallife_supply_health_trouble" };

        public static readonly string[] TroubleKeys = { "reallife_supply_trouble_none", "reallife_supply_trouble_strike", "reallife_supply_trouble_fire" };

        public static readonly string[] RenewKeys = { "reallife_supply_renew_off", "reallife_supply_renew_same", "reallife_supply_renew_best" };
    }

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
        public int Against = 1000, FirstDay, Lines, LeftOut;   // LeftOut: products this place sells that it trades and a contract has no room for
    }

    /// <summary>One product of a contract as a page shows it: its name in the game's words, the units a delivery and the unit price in mills.</summary>
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
