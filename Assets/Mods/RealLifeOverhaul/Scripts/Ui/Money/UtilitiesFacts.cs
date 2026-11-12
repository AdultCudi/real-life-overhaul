using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>How one utility account stands, as the kit tells it: supplied (paid or owing) or not.</summary>
    public enum UtilityBillState : byte
    {
        Paid = 0,       // supplied, nothing owed
        Due,            // a bill owed, not yet late
        Overdue,        // a bill owed past its due day
        Pending,        // an account applied for, connecting
        Temporary,      // a temporary supply
        Cut,            // not supplied
        Closed          // the place is gone, its last bill is owed
    }

    /// <summary>What an account can be given in the app: a new account, or a reconnection, only while there is no office to visit.</summary>
    public enum UtilityOffer : byte
    {
        None = 0,
        Apply,
        Reconnect
    }

    /// <summary>One power, water or internet account of a place, read from the save as plain words and figures.</summary>
    public sealed class UtilityAccountFact
    {
        public string Key;              // the place and the service, so a chosen account stays chosen as the list reorders
        public string Place;            // the business's name, or the home's address
        public int Service;             // 0 power, 1 water, 2 internet
        public bool Home;
        public string Provider;         // who supplies it
        public UtilityBillState State;
        public string StateText;        // the game's own words for the state ("Connects on day 12")
        public int SoFar;               // money used this month so far
        public int Owed;                // money billed and not paid
        public int DueDay;              // the day the bill owed falls due, 0 with nothing owed
        public bool AutoDebit;
        public bool CanDebit;           // a closed account has no auto debit to set
        public string Units;            // "12.4", the month's use in kWh or cubic metres, or null (the internet has none)
        public int LastBill, LastBillDay;
        public int Deposit, DepositLeft;
        public UtilityOffer Offer;
        public int ApplyCost;           // what applying costs now, money
        public int ApplyDeposit, ApplyFee;
    }

    /// <summary>One of the player's places and its internet connection, with every plan it can be given.</summary>
    public sealed class UtilityPlaceFact
    {
        public string Key;
        public string Name;
        public bool Home;
        public bool Connected;          // an account that is not closed
        public bool Cut, Down, Pending; // cut off, in an outage, or still being installed
        public int ProviderIndex = -1;  // the connection's provider, as the options number them
        public bool Needs;              // a business that cannot open without a connection
        public bool Orders;             // an online shop that takes orders only with one
        public string Provider;         // the connection's provider, or null
        public int Mbps;                // the connection's speed
        public int PriceCents;          // what it costs a month
        public string StateText;
        public string ContractText;     // "Month by month", or "until day 40"
        public bool OnContract;
        public int ExitFee;             // leaving now, money
        public string District;
        public bool Grace;              // inside the internet's grace period
        public bool Owes;               // a bill is owed on the connection
        public UtilityOptionFact[] Options = new UtilityOptionFact[0];
    }

    /// <summary>One plan of one provider that reaches a place: what the player can buy there.</summary>
    public sealed class UtilityOptionFact
    {
        public int Provider;            // an index of InternetCatalogue.Providers, or the company's own
        public int Plan;
        public string ProviderName;
        public string Kind;             // "cable", "fibre"
        public int Mbps;
        public int ContractCents;       // a month on a contract
        public int MonthlyCents;        // a month without one
        public bool Own;                // the player's own company: free, no contract
        public bool Current;            // the plan the place has now
        public string Speed;            // what that speed means for the work there, or null
    }

    /// <summary>One district of the broadband company's market.</summary>
    public sealed class UtilityDistrictFact
    {
        public string Name;
        public int State;               // 0 not started, 1 digging, 2 live
        public int Homes;               // homes and businesses the network reaches, or would
        public int Customers;
        public int ReadyDay;
        public int DigCost;             // money, to dig it now
        public int ExtendCost;          // money, to extend it to the whole district, 0 when it is
        public int Upkeep;              // money a month once dug
    }

    /// <summary>One of the company's plans: its name, its speed, what it costs a month and who is on it.</summary>
    public sealed class UtilityPlanFact
    {
        public int Mbps;
        public int PriceCents;
        public int Customers;
    }

    /// <summary>A rival provider of the city, by the catalogue's own figures.</summary>
    public sealed class UtilityRivalFact
    {
        public string Name;
        public string Kind;
        public int Districts;
        public int FromMbps, ToMbps;
        public int FromCents;
    }

    /// <summary>The player's own broadband company as the kit shows it.</summary>
    public sealed class UtilityNetworkFact
    {
        public int State;               // 0 not founded, 1 running, 2 sold
        public int FoundedDay, SoldDay;
        public long SalePrice;
        public bool HasHeadquarters;
        public string HeadquartersName;
        public int Licence;             // money
        public int DigLeast, DigMost;   // money: the cheapest and dearest district to dig
        public int BuildMonths;
        public int Customers;
        public int TakingsDay, UpkeepDay;   // money a day
        public int Homes;               // homes and businesses of the live districts
        public int PriceShare;          // thousandths of the usual prices
        public int PriceLeast, PriceMost, PriceStep;
        public int Maintenance;         // thousandths of the usual upkeep
        public int MaintenanceLeast, MaintenanceMost, MaintenanceStep;
        public int Marketing;           // the budget chosen, an index
        public string BuyerName;        // who buys the company
        public int OutagePerMille;
        public int[] MarketingBudgets = new int[0];   // money a month by index
        public long SaleValue;          // what a buyer pays less what it owes, money
        public long Owed;               // money the company owes
        public UtilityDistrictFact[] Districts = new UtilityDistrictFact[0];
        public UtilityPlanFact[] Plans = new UtilityPlanFact[0];
        public UtilityRivalFact[] Rivals = new UtilityRivalFact[0];
        public int[] Series = new int[0];   // customers by day, oldest first, from the day the company opened
        public int SeriesFirstDay;
    }

    /// <summary>Everything the Utilities kit screen reads from the save, as plain facts. The sample fills the same fields.</summary>
    public sealed class UtilitiesFacts
    {
        public bool Ready;              // false while there is no save, or the utilities are off
        public int Day;
        public string Office;           // the utilities office's name
        public bool ByPhone;            // no office to visit: accounts are applied for in the app
        public int InstallFee, InstallDays, ContractMonths;
        public int Owed, Month;         // money owed in all, and this month so far
        public UtilityAccountFact[] Accounts = new UtilityAccountFact[0];
        public UtilityPlaceFact[] Places = new UtilityPlaceFact[0];
        public UtilityNetworkFact Network = new UtilityNetworkFact();
        public int GraceLeft;           // days of the internet's grace left, 0 when none
        public int GraceDays, GraceUntilDay;
    }

    /// <summary>What the player asked for, by the index of the facts' lists; a source does it and says why not.</summary>
    public enum UtilitiesAct : byte
    {
        Pay = 0,            // a: the account
        Apply,              // a: the account
        Debit,              // a: the account, b: 1 on, 0 off
        Buy,                // a: the place, b: the provider, c: the plan, d: 1 on a contract
        Cancel,             // a: the place
        Reconnect,          // a: the place
        Found,
        Dig,                // a: the district
        Extend,             // a: the district
        Prices,             // a: thousandths of the usual
        Maintenance,        // a: thousandths of the usual
        Marketing,          // a: the budget's index
        Sell
    }

    /// <summary>Reads the facts from the save and carries the player's acts to the features that own them. The game sets it; a check stands one in.</summary>
    public interface IUtilitiesSource
    {
        /// <summary>Changes whenever the facts do, so the screen reads them again only then.</summary>
        int Generation { get; }

        void Read(UtilitiesFacts into);

        /// <summary>Does an act, saying so in the game as the old app did. False when it could not be done.</summary>
        bool Do(UtilitiesAct act, int a, int b, int c, int d);
    }
}
