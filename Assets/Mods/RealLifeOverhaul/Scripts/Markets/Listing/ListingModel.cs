using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Where the player's company stands on its way to the market. Saved as a number: never renumber.</summary>
    public enum ListingStage
    {
        None = 0,        // not on its way
        Advised = 1,     // advisers engaged (their retainer paid)
        Auditing = 2,    // the auditors are at work
        Audited = 3,     // a clean audit in hand
        Building = 4,    // the prospectus is out and the book is open
        Priced = 5,      // the book has priced it; it trades from its first day
        Listed = 6       // on the market
    }

    /// <summary>What the founder of a listed company asks for (a tape event: MarketInputs.EventOwn + this). Saved as a number.</summary>
    public enum OwnAction { Pay = 0, Dividend = 1, Deal = 2, BuyBack = 3, Issue = 4, Private = 5, Count }

    /// <summary>Why something the founder asked for cannot be done. Saved as a number (in the news); each has a sentence.</summary>
    public enum OwnWhy
    {
        None = 0, NotListed, Shut, Held, Closed, LockedUp, NoMoney, NoShares, TooMuch, TooLarge, Board, Bid, Reserve, Control, Suspended, Count
    }

    /// <summary>What an investor who buys in to press the board asks for. Saved as a number.</summary>
    public enum ActivistDemand { None = 0, Payout = 1, Pay = 2, Board = 3, Sale = 4, Count }

    /// <summary>The transaction keys of a player's company's money (each has a text and a "_label" in en.json; "shares_" puts them under investments, "fee" under fees, "tax" under tax).</summary>
    public static class ListingMoney
    {
        public const string Holders = "reallife_transaction_shares_holders";             // the other holders' part of a day's profit, into the reserve
        public const string HoldersBack = "reallife_transaction_shares_holders_back";    // their part of a loss, back out of it
        public const string Offer = "reallife_transaction_shares_offer";                 // the shares sold in the offer
        public const string Underwriting = "reallife_transaction_shares_underwriting";   // the underwriters, out of an offer or new shares
        public const string Advisers = "reallife_transaction_listing_advisers_fee";          // the advisers' retainer
        public const string Audit = "reallife_transaction_listing_audit_fee";            // the auditors (a business cost the game's tax deducts)
        public const string Exchange = "reallife_transaction_listing_fee";               // the exchange, on the first day and each year (deducted too)
        public const string ReserveIn = "reallife_transaction_shares_reserve_in";        // the reserve of the shares a founder buys, to them
        public const string ReserveOut = "reallife_transaction_shares_reserve_out";      // ...and of those they sell, from them to the reserve
        public const string BuyBack = "reallife_transaction_shares_buyback";
        public const string Issue = "reallife_transaction_shares_issue";
        public const string Private = "reallife_transaction_shares_private";
        public const string ShortSwing = "reallife_transaction_shares_short_swing";
        public const string Takeover = "reallife_transaction_shares_takeover";
        public const string DividendTax = "reallife_transaction_shares_dividend_tax";

        /// <summary>True for the costs of being listed that are a business's costs, which the game's own tax deducts.</summary>
        public static bool Deductible(string key)
        {
            return key == Audit || key == Exchange;
        }
    }

    /// <summary>The figures of one quarter of the company's own books, from the game's statements.</summary>
    public struct BookQuarter
    {
        public int Quarter;          // counted from the start of the game (MarketCatalogue.QuarterOf)
        public long Revenue;         // takings, money
        public long Profit;          // profit before tax, money
        public int Days;             // statement days counted in it
    }

    /// <summary>A founder's deal in their own company's shares, kept while the rules look back at it (short swings).</summary>
    public struct OwnDeal
    {
        public int Day;
        public long Shares;          // bought, or under nothing sold; what is left of it to be matched
        public int Cents;
    }

    /// <summary>
    /// A player's company as a listed company (<see cref="Stock.Own"/>): what the market and its holders know of it beyond
    /// a company of the city's. On every machine of a Going Public session alike (it is in the city's market); its
    /// founder is the player whose save hashes to <see cref="Owner"/>. Game-free.
    ///
    /// THE RESERVE. Every day the company's profit, after tax and the founder's pay, belongs to all its holders by their
    /// shares. The founder's part is in their cash already (the game puts the takings there); the other holders' part is
    /// taken from it and kept for them (<see cref="Rps"/>, money a share): it is what dividends are paid from, what a
    /// buy-back or taking the company private pays first, and what the price counts as the company's cash. A loss is
    /// borne the same way: it comes back out of the reserve, and beyond it is made good first by later profits.
    /// </summary>
    public sealed class OwnListing
    {
        public ulong Owner;                      // the founder: a hash of their save (MarketState.Me on their machine)
        public bool Test;                        // made by the test tools: no money moves for it
        public long Founder;                     // shares the founder holds
        public double Rps;                       // the reserve a share, money (under nothing: losses still to be made good)
        public int PayBp;                        // the founder's pay: a share of each day's profit before tax
        public int ProposedCents;                // the dividend the board proposes with the next results, a share
        public bool Board = true;                // the founder chairs the board (lost at a meeting: until the next yearly one)
        public double RevQ, NiQ, RevY, NiY;      // takings and profit (after the founder's pay) a day, followed over about a quarter and about a year
        public double LeverY;                    // what the council's policies did to its margin, followed over about a year
        public double Expect;                    // profit after tax and pay the market expected of the quarter that runs, at its start
        public double PendingExpect;             // ...and of the quarter that waits for its results
        public int DebtBp;                       // its usual debt against a year's takings, as it came to the market
        public int ListedCents;                  // the price it came to the market at
        public int LockDay;                      // the founder sells nothing before this day
        public int AgmDay;                       // the next yearly meeting (0: after the year's last results)
        public int YearCents, YearIndex;         // the price and the city's index at the last yearly meeting (or the listing)
        public int YearScandals;                 // scandals since the last yearly meeting
        public int YearDividendCents;            // dividends declared since then, a share
        public int LastStanding = -1;            // the founder's Standing as the market last read it
        public int ActBp;                        // an investor pressing a demand: their stake in bp (0 none)
        public ActivistDemand ActDemand;
        public int ActDay;                       // ...the day of the meeting they called
        public int Votes;                        // what passed at the last meeting (ListingVotes bits)
        public int VotesAgainstBp;               // ...and the share of the votes cast against the board
        public int IssuedYear = -1;              // the year new shares were last sold in
        public int IssuedBp;                     // ...and how many, against the company then
        public int SaleDay;                      // its holders voted to sell it: a bid is made on this day (0 none)
        public double Carry;                     // the other holders' part of the profit not yet moved, under a whole unit
        public readonly List<OwnDeal> Deals = new List<OwnDeal>(8);

        /// <summary>Shares the founder does not hold: the other holders'.</summary>
        public long Others(Stock s)
        {
            return s.Shares - Founder;
        }

        /// <summary>The founder's stake in bp of the shares in issue.</summary>
        public int StakeBp(Stock s)
        {
            return s.Shares > 0 ? (int)(Founder * 10000L / s.Shares) : 0;
        }
    }

    /// <summary>
    /// The player's own way to the market, and the company's books: kept in their market's line of the save, never sent
    /// to anybody (MarketStore writes it only for the save). Game-free.
    /// </summary>
    public sealed class ListingFile
    {
        public ListingStage Stage;
        public int AdvisedDay;                   // the advisers were engaged
        public int AuditFrom, AuditDone;         // the audit runs from one day to the other
        public int AuditDay;                     // the last audit was signed (0 none)
        public int Findings;                     // what it found (ListingBook.Finding bits)
        public int FloatBp;                      // the share of the company offered
        public int LowCents, HighCents;          // the range in the prospectus
        public int WalkCents;                    // the least price the founder takes
        public int BookFrom, BookEnd;            // the book is open from one market day; it is priced at the open of the other
        public int PriceCents;                   // the price the book came to
        public int ListDay;                      // its first day of trading
        public long OldShares, NewShares;        // the founder's and those offered
        public int FairCents;                    // what the advisers found a share worth, before their discount
        public string Name = string.Empty, Ticker = string.Empty;
        public int Sector;
        public int StockId;                      // its id on the market
        public bool Test;                        // the test tools' listing: nothing paid, nothing raised
        public long Raised;                      // money the offer brought, after the underwriters
        public long Fees;                        // fees paid on the way (advisers, auditors, the exchange)
        public int FeeYear = -1;                 // the year the yearly fee and audit were last paid for
        public int FirstDay;                     // the day the first of the player's businesses was taken on (0 not known)
        public int BooksDay;                     // the last statement day counted into the books
        public string Suspended;                 // the company's record while its founder is a guest in another player's city
        public int SetAsideDay;                  // ...the day it was set aside: the founder's statements from it on are counted when it is back
        public readonly List<BookQuarter> Books = new List<BookQuarter>(16);

        public bool OnTheWay => Stage >= ListingStage.Advised && Stage <= ListingStage.Priced;

        /// <summary>True for a file with nothing in it (a save's line then has no part for it, as before there was a listing).</summary>
        public bool Empty => Stage == ListingStage.None && Books.Count == 0 && AuditDay == 0 && StockId == 0 && Fees == 0 && FirstDay == 0 && string.IsNullOrEmpty(Suspended)
                             && string.IsNullOrEmpty(Name) && AdvisedDay == 0 && AuditFrom == 0;

        /// <summary>The books' quarter of a number, or null-like (Quarter -1) if none.</summary>
        public int IndexOf(int quarter)
        {
            for (int i = 0; i < Books.Count; i++)
            {
                if (Books[i].Quarter == quarter)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
