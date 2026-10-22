using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>What a contract is. Saved as a number: never renumber.</summary>
    public enum ContractKind { Long = 0, Short = 1, Call = 2, Put = 3 }

    /// <summary>Where a contract stands. Saved as a number: never renumber.</summary>
    public enum ContractEnd
    {
        Open = 0,
        Closed = 1,      // sold back or bought back before its day
        Settled = 2,     // a future settled in cash at its day's price
        Exercised = 3,   // an option in the money at its day, paid in cash
        Lapsed = 4,      // an option out of the money at its day: worth nothing
        Delivered = 5,   // a future settled by delivery to a warehouse, what did not fit in cash
        Forced = 6,      // a future closed by the broker after a margin call
        Gone = 7         // a share option whose company left the market, settled at what each share was paid out
    }

    /// <summary>Why a contract cannot be bought, sold or paid now. Each has a sentence (reallife_mkt_drv_why_...).</summary>
    public enum DerivWhy { None = 0, Closed, Held, Behind, Off, NoCity, Unknown, NotOffered, TooMany, TooMuch, NoCash, NotHeld, Ended, NoPlace, NoCall, Count }

    /// <summary>
    /// A raw material on the exchange, the city's: the same on every machine of a Going Public session (the host's line
    /// carries it). Its price at the last close is what the importers charge until the next (Economy/City writes it).
    /// </summary>
    public sealed class RawMaterial
    {
        public string Name = string.Empty;   // the game's item name, never translated
        public MaterialGroup Group;
        public long Base;                    // micros: its own price a unit, with the game's price multiplier
        public double X;                     // ln of the price against Base at the price level
        public long Fix;                     // micros: the price set at the last close
        public int FixDay;                   // the market day whose close set it; 0 never
        public int Level = 1000;             // the price level (tenths of a point) it was set at
        public readonly List<long> Closes = new List<long>(DerivCatalogue.ClosesKept + 1);   // the last closes' prices, oldest first

        /// <summary>What the importers' price is against the material's own: the price level and its move.</summary>
        public double Factor => Base > 0 ? (double)Fix / Base : 1.0;
    }

    /// <summary>
    /// A future or an option of the player's. A future's money is settled every close into the margin account; an option
    /// is paid for when bought and pays in cash at its day if in the money.
    /// </summary>
    public sealed class DerivContract
    {
        public int Id;
        public ContractKind Kind;
        public string Material = string.Empty;   // the material's name; empty for an option on shares
        public int StockId;                       // the company of an option on shares; 0 for a material
        public long Qty;                          // units, or shares
        public long Price;                        // micros a unit: a future's agreed price, an option's strike
        public long Premium;                      // micros a unit an option cost; 0 for a future
        public long Mark;                         // micros a unit: a future's price at the last close, its money settled to it
        public long Margin;                       // cents: a future's margin while it is open
        public int OpenedDay, SettleDay;          // bought on; settles at the close of
        public string Warehouse = string.Empty;   // a long future's place to deliver to (the game's address); empty: in cash
        public string PlaceName = string.Empty;   // that place's name when it was bought, never translated
        public bool Test;                         // bought by the test tools
        public ContractEnd End;
        public int EndDay;
        public long EndPrice;                     // micros a unit: the price it ended at
        public long Result;                       // cents: what it made or lost in all, its commission included
        public long Delivered;                    // units delivered
        public long Fees;                         // cents of commission paid on it

        public bool IsOpen => End == ContractEnd.Open;
        public bool IsFuture => Kind == ContractKind.Long || Kind == ContractKind.Short;
        public bool OnShares => StockId > 0;
    }

    /// <summary>
    /// The player's futures and options with their margin account: each player's own, never in the city's line. The
    /// account's balance is the margins held and the moves settled since, in cents.
    /// </summary>
    public sealed class DerivBook
    {
        public readonly List<DerivContract> Contracts = new List<DerivContract>(16);   // open, and ended lately
        public int NextId = 1;
        public long Balance;                 // cents with the broker for futures
        public int CallDay;                  // a margin call to be met by the close of this day; 0 none
        public long CallAmount;              // money
        public long Commission;              // cents paid in all
        public long Realised;                // cents made or lost on contracts that ended

        public bool Empty => Contracts.Count == 0 && Balance == 0 && CallDay == 0 && Commission == 0 && Realised == 0;

        public DerivContract Contract(int id)
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

        /// <summary>The player's own contracts brought into another market (a Going Public guest takes the host's).</summary>
        public void Take(DerivBook from)
        {
            if (from == null || ReferenceEquals(from, this))
            {
                return;
            }
            Contracts.AddRange(from.Contracts);
            NextId = from.NextId > NextId ? from.NextId : NextId;
            Balance = from.Balance;
            CallDay = from.CallDay;
            CallAmount = from.CallAmount;
            Commission = from.Commission;
            Realised = from.Realised;
        }
    }

    /// <summary>What a <see cref="DerivNote"/> tells.</summary>
    public enum DerivNoteKind { Settled = 0, Exercised = 1, Lapsed = 2, Deliver = 3, Called = 4, Forced = 5, Gone = 6, Shortfall = 7 }

    /// <summary>
    /// What a contract did, for the director to tell. Money in whole units. Settled, Exercised, Lapsed, Gone, Forced: A what
    /// it made in all (under nothing a loss). Deliver: Qty units at Price micros to the warehouse, for the director to
    /// put on its pallets and charge. Called: A the call, B the day it is due by. Shortfall: A what was forced from the cash.
    /// </summary>
    public struct DerivNote
    {
        public DerivNoteKind Kind;
        public int Contract;
        public long A, B;
        public long Qty, Price;
        public string Material, Warehouse;
    }

    /// <summary>A price on offer now: what it is bought or sold at, its fair price, and what buying it would take.</summary>
    public struct DerivQuote
    {
        public long Fair;          // micros a unit: the expected price at its day, or an option's fair premium
        public long Ask, Bid;      // micros a unit
        public long Forward;       // micros a unit: the future's fair price for that day
        public long Strike;        // micros a unit: an option's
        public long Value;         // money: the future's worth, or the premium for all of it
        public long Margin;        // money held for a future
        public long Fee;           // money: the commission
        public double Spread;      // a share of the price: the move a week may bring, one spread
        public DerivWhy Why;
    }
}
