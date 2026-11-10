using System.Collections.Generic;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>How a contract stands. Saved as a number: never renumber, add at the end.</summary>
    public enum ContractState
    {
        Running = 0,
        Done = 1,       // its last delivery came
        Broken = 2,     // ended early by the customer: the settlement was due
        Left = 3,       // ended by the customer's right to leave after the supplier's failures: nothing to pay
        Failed = 4,     // the supplier went under
        Unpaid = 5,     // ended by the supplier for want of payment: the settlement and what was owed are due
        Closed = 6      // the place closed, was sold or is no longer the customer's: the settlement was due
    }

    /// <summary>How the next delivery will come, judged the day before. Saved as a number.</summary>
    public enum Arrival
    {
        None = 0,       // not judged yet
        Whole = 1,
        Late = 2,
        Short = 3
    }

    /// <summary>Why a delivery is late or short. Saved as a number. The first three are the supplier's own and earn the remedy; the rest are nobody's fault.</summary>
    public enum Cause
    {
        None = 0,
        Own = 1,        // the supplier's own unreliability
        Trouble = 2,    // a strike or a fire at its depot
        Cash = 3,       // its own money trouble
        Weather = 4,
        Streets = 5,    // a city event closes the streets
        Season = 6,     // fresh goods in winter
        Shortage = 7    // the kind of goods is short across the whole market
    }

    /// <summary>What the place's buyer does with a contract when its term ends. Saved as a number.</summary>
    public enum RenewRule
    {
        Off = 0,
        Same = 1,       // the same supplier again, if its price is inside the limit
        Best = 2        // the cheapest supplier that offers, inside the limit
    }

    /// <summary>One product of a contract: so many units a delivery at a unit price in mills (thousandths of a money unit).</summary>
    public sealed class ContractLine
    {
        public string Item = string.Empty;
        public int Kind;
        public int Units;
        public long Mills;
    }

    /// <summary>A contract with a supplier for one place: its products, its term, what has come, how the next delivery stands and the rules its buyer follows.</summary>
    public sealed class Contract
    {
        public int Id, Supplier;
        public string Place = string.Empty, PlaceName = string.Empty;
        public int Weeks;                   // the term in weekly deliveries; 0: one delivery at the day's price
        public int Share = 1000;            // thousandths of the week's sales its quantities were set at
        public int Start, Due, Slip;        // the day signed, the day of the next delivery, days it is late
        public int Made;                    // deliveries made or passed
        public Arrival Out;
        public Cause Why;
        public int OutShare = 1000;         // thousandths a short delivery brings
        public int OutKind = -1;            // the kind of goods a short delivery is short of; -1: all of it
        public int JudgedFor;               // the due day the outcome was judged for
        public ContractState State;
        public int Ended;
        public int Failures, Withheld;
        public long Paid, Credit;           // cents paid so far; cents of credit carried to the next payment
        public int NotTaken;                // units paid for that found no room
        public RenewRule Renew;
        public int RenewLimit = SupplierCatalogue.RenewLimitDefault;
        public bool Cover, LeaveRule;       // the buyer's rules: cover a late or short delivery at the day's price, leave when the right to leave arises
        public bool Reminded;               // the end of the term was told
        public bool Covering;               // a one-off order made to cover another contract's delivery: it is never covered itself
        public readonly List<ContractLine> Lines = new List<ContractLine>(SupplierCatalogue.LinesMost);

        public bool Running => State == ContractState.Running;

        /// <summary>Deliveries the whole term holds.</summary>
        public int Deliveries => Weeks > 0 ? Weeks : 1;

        /// <summary>Deliveries still to come.</summary>
        public int Left => Running ? System.Math.Max(0, Deliveries - Made) : 0;

        /// <summary>True for a fixed price contract; false for one delivery at the day's price.</summary>
        public bool Fixed => Weeks > 0;
    }

    /// <summary>Money owed to a supplier: a delivery that could not be paid on the day, or a settlement. Never silent: the page lists it and a notice says it.</summary>
    public sealed class OwedLine
    {
        public int Supplier, Contract;
        public long Cents;
        public int Since, Due;              // the day it arose, the last day to pay it before the supplier acts
        public bool Settlement;             // a settlement or what an ended contract left: nothing more can follow from it but the record
    }

    /// <summary>A supplier as the save keeps it: its health, its trouble, whether it trades, how far it has grown and the book of fixed prices it has written.</summary>
    public sealed class FirmState
    {
        public int Health;
        public int Size = 1;
        public int TroubleKind, TroubleUntil;   // 0 none, 1 a strike, 2 a fire at the depot; the last day it runs
        public int ClosedDay, ReopenDay;        // the day it went under and the day new owners open it; 0 while it trades
        public int Strong;                      // days running at or over the health it grows from
        public int SourUntil;                   // the last day it offers the customer nothing after a default
        public bool Sold;                       // it went under and came back under new owners: no longer a tycoon's
        public int Customers;                   // the city's shops it served at the last count
        public int Brought, Missed;             // deliveries it made whole and on the day, and those it did not
        public string Depot = string.Empty;     // the key of the game's wholesale store or importer its depot stands by; empty while the city shows none
        public readonly int[] Book = new int[SupplierCatalogue.Kinds];   // thousandths of the calm price its fixed contracts are written at, by kind

        public bool Open => ClosedDay == 0;

        public bool InTrouble(int day) => TroubleKind != 0 && day <= TroubleUntil;
    }

    /// <summary>The customer's payment record with all suppliers.</summary>
    public sealed class PayRecord
    {
        public int OnTime, Late, Defaults;
        public readonly List<int> LateDays = new List<int>(8);   // the days of the latest late payments

        public int LateSince(int day)
        {
            int n = 0;
            for (int i = 0; i < LateDays.Count; i++)
            {
                n += LateDays[i] >= day ? 1 : 0;
            }
            return n;
        }
    }

    /// <summary>What supply has done to one rival tycoon's outlets.</summary>
    public sealed class RivalSupply
    {
        public long Month, Total;           // whole money against calm prices: more paid is positive
        public int Failures;                // deliveries late or short this month
        public int Firm;                    // the supplier most of their outlets buy from; 0 the wholesale stores
        public int MonthOf = -1;
    }

    /// <summary>What a note for the paper is about. Saved as a number.</summary>
    public enum NoteKind
    {
        Trouble = 0,    // a supplier's strike or fire: A the kind of trouble, B its last day
        Under = 1,      // a supplier went under: A the shops it served
        Back = 2,       // new owners opened it
        Grew = 3,       // it grew a step: A its size
        Short = 4       // the city's shops left short by a supplier's failure: A how many
    }

    public sealed class SupplyNote
    {
        public int Day, Firm, A, B;
        public NoteKind Kind;
    }

    /// <summary>Goods a contract brought to a place: what a manager's order counts as come.</summary>
    public sealed class DeliveryLog
    {
        public int Day, Units;
        public string Place = string.Empty, Item = string.Empty;
    }

    /// <summary>What the engine did, for the director to say and the checks to read. Never saved.</summary>
    public enum SupplyEventKind
    {
        Signed, Delivered, NotTaken, Tomorrow, Credit, MayLeave, Ending, Renewed, Switched, Covered, OnAccount, Withheld, EndedUnpaid, EndedPlace, EndedDone, Broken, Left,
        FirmTrouble, FirmUnder, FirmBack, FirmGrew, OwedPaid, CreditBack, CreditLost, NoCover
    }

    public struct SupplyEvent
    {
        public SupplyEventKind Kind;
        public int Contract, Firm;
        public string Place;
        public long A, B;
        public int C;
    }

    /// <summary>Suppliers and contracts as the save keeps them (<see cref="SupplierStore"/>). Game-free.</summary>
    public sealed class SupplierState
    {
        public const int Rivals = 3;        // the rival tycoons (RivalCatalogue.TycoonCount)

        public int Seed;
        public int LastDay, LastHour = -1;
        public int NextId = 1;
        public int RivalDay;                // the last day the rival shops' books were done
        public int ShopsShort;              // the city's shops left short on that day
        public readonly FirmState[] Firms = new FirmState[SupplierCatalogue.Firms.Length];
        public readonly List<Contract> Contracts = new List<Contract>(8);
        public readonly List<OwedLine> Owed = new List<OwedLine>(4);
        public readonly PayRecord Record = new PayRecord();
        public readonly RivalSupply[] RivalBooks = new RivalSupply[Rivals];
        public readonly List<SupplyNote> Notes = new List<SupplyNote>(4);
        public readonly List<DeliveryLog> Log = new List<DeliveryLog>(32);
        public readonly List<SupplyEvent> Events = new List<SupplyEvent>(8);   // not saved

        public SupplierState()
        {
            for (int i = 0; i < Firms.Length; i++)
            {
                Firms[i] = new FirmState { Health = SupplierCatalogue.Firms[i].HealthStart };
                for (int k = 0; k < SupplierCatalogue.Kinds; k++)
                {
                    Firms[i].Book[k] = 1000;
                }
            }
            for (int i = 0; i < RivalBooks.Length; i++)
            {
                RivalBooks[i] = new RivalSupply();
            }
        }

        public FirmState Firm(int id) => id >= 1 && id <= Firms.Length ? Firms[id - 1] : null;

        public Contract ContractOf(int id)
        {
            for (int i = 0; i < Contracts.Count; i++)
            {
                if (Contracts[i].Id == id)
                {
                    return Contracts[i];
                }
            }
            return null;
        }

        public int RunningCount()
        {
            int n = 0;
            for (int i = 0; i < Contracts.Count; i++)
            {
                n += Contracts[i].Running ? 1 : 0;
            }
            return n;
        }

        /// <summary>Everything owed to suppliers, cents.</summary>
        public long OwedAll()
        {
            long sum = 0;
            for (int i = 0; i < Owed.Count; i++)
            {
                sum += Owed[i].Cents;
            }
            return sum;
        }

        /// <summary>What is owed under one contract, cents.</summary>
        public long OwedOn(int contract)
        {
            long sum = 0;
            for (int i = 0; i < Owed.Count; i++)
            {
                sum += Owed[i].Contract == contract ? Owed[i].Cents : 0L;
            }
            return sum;
        }

        /// <summary>Units the contracts brought to a place of an item since a day and hour (a delivery comes at the catalogue's hour).</summary>
        public int Brought(string place, string item, int sinceDay, int sinceHour)
        {
            int n = 0;
            for (int i = 0; i < Log.Count; i++)
            {
                DeliveryLog e = Log[i];
                if (e.Place == place && e.Item == item && (e.Day > sinceDay || (e.Day == sinceDay && SupplierCatalogue.DeliveryHour > sinceHour)))
                {
                    n += e.Units;
                }
            }
            return n;
        }

        /// <summary>Units the running contracts are due to bring to a place of an item from a day up to and counting another.</summary>
        public int Coming(string place, string item, int fromDay, int toDay)
        {
            int n = 0;
            for (int i = 0; i < Contracts.Count; i++)
            {
                Contract c = Contracts[i];
                if (!c.Running || c.Place != place)
                {
                    continue;
                }
                for (int k = 0; k < c.Lines.Count; k++)
                {
                    if (c.Lines[k].Item != item)
                    {
                        continue;
                    }
                    for (int n2 = 0; n2 < c.Left; n2++)
                    {
                        int day = c.Due + n2 * SupplierCatalogue.WeekDays + (n2 == 0 ? c.Slip : 0);   // only the delivery that is late slips: the ones after keep their week
                        n += day >= fromDay && day <= toDay ? c.Lines[k].Units : 0;
                    }
                }
            }
            return n;
        }

        internal void Say(SupplyEventKind kind, Contract c, int firm, long a = 0, long b = 0, int n = 0)
        {
            Events.Add(new SupplyEvent { Kind = kind, Contract = c != null ? c.Id : 0, Firm = firm, Place = c != null ? c.PlaceName : string.Empty, A = a, B = b, C = n });
        }
    }
}
