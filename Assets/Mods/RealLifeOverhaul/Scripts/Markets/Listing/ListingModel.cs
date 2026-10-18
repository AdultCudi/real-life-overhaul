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

    /// <summary>What the founder asks for, as a tape event (MarketInputs.EventOwn + this); Audit and Proceeds are not theirs to ask. Saved as a number.</summary>
    public enum OwnAction { Pay = 0, Dividend = 1, Deal = 2, BuyBack = 3, Issue = 4, Private = 5, Audit = 6, Proceeds = 7, Count }

    /// <summary>Why something the founder asked for cannot be done. Saved as a number (in the news); each has a sentence.</summary>
    public enum OwnWhy
    {
        None = 0, NotListed, Shut, Held, Closed, LockedUp, NoMoney, NoShares, TooMuch, TooLarge, Board, Bid, Reserve, Control, Suspended, Off, NotReady, YearLimit, Count
    }

    /// <summary>What an investor who buys in to press the board asks for. Saved as a number.</summary>
    public enum ActivistDemand { None = 0, Payout = 1, Pay = 2, Board = 3, Sale = 4, Count }

    /// <summary>The transaction keys of a listed company's money: "shares_" files them under investments, "fee" under fees.</summary>
    public static class ListingMoney
    {
        public const string Holders = "reallife_transaction_shares_holders";             // the other holders' part of a day's profit, into the reserve
        public const string HoldersBack = "reallife_transaction_shares_holders_back";    // their part of a loss, back out of it
        public const string Offer = "reallife_transaction_shares_offer";                 // the shares sold in the offer
        public const string Underwriting = "reallife_transaction_shares_underwriting";   // the underwriters, out of an offer or new shares
        public const string Advisers = "reallife_transaction_listing_advisers_fee";          // the advisers' retainer
        public const string Audit = "reallife_transaction_listing_audit_fee";            // a business cost the game's tax deducts
        public const string Exchange = "reallife_transaction_listing_fee";               // on the first day and each year; deducted too
        public const string ReserveIn = "reallife_transaction_shares_reserve_in";        // the reserve of the shares a founder buys, to them
        public const string ReserveOut = "reallife_transaction_shares_reserve_out";      // ...and of those they sell, from them to the reserve
        public const string BuyBack = "reallife_transaction_shares_buyback";
        public const string Issue = "reallife_transaction_shares_issue";
        public const string Private = "reallife_transaction_shares_private";
        public const string ShortSwing = "reallife_transaction_shares_short_swing";
        public const string Takeover = "reallife_transaction_shares_takeover";
        public const string DividendTax = "reallife_transaction_shares_dividend_tax";
        public const string Kept = "reallife_transaction_shares_takeover_kept";   // businesses a buyer could not be handed in time, paid for
        public const string Proceeds = "reallife_transaction_shares_proceeds";   // the other holders' part of a company business sold

        /// <summary>True for the costs of being listed that are a business's costs, which the game's own tax deducts.</summary>
        public static bool Deductible(string key)
        {
            return key == Audit || key == Exchange;
        }
    }

    /// <summary>One quarter of the company's own books, from the game's statements.</summary>
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
    /// A player's company as a listed company, alike on every machine of a session; its founder is the save that hashes to
    /// <see cref="Owner"/>. Each day the other holders' part of its profit is taken from the founder's cash into a reserve
    /// (<see cref="Rps"/>) that pays dividends, buy-backs and a take-private, and that a loss comes back out of.
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
        public double RevQ, NiQ, RevY, NiY;      // a day, after the founder's pay, followed over a quarter and a year
        public double LeverY;                    // what the council's policies did to its margin, followed over about a year
        public double Expect;                    // profit after tax and pay expected of the running quarter, at its start
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
        public int DealYear = -1;                // the game year the founder's dealing and the buy-backs below are counted for
        public int BoughtBp, SoldBp, BackBp;     // ...what the founder bought and sold of the company in it, and what it bought back
        public double Carry;                     // the other holders' part of the profit not yet moved, under a whole unit
        public readonly List<OwnDeal> Deals = new List<OwnDeal>(8);

        public long Others(Stock s)
        {
            return s.Shares - Founder;
        }

        public int StakeBp(Stock s)
        {
            return s.Shares > 0 ? (int)(Founder * 10000L / s.Shares) : 0;
        }
    }

    /// <summary>The player's own way to the market and the company's books: in their save only, never sent. Game-free.</summary>
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
        public int Trade = -1;                    // the game's trade its takings are most in (MarketInputs.Trades), -1 none
        public int StockId;                      // its id on the market
        public bool Test;                        // the test tools' listing: nothing paid, nothing raised
        public bool TestBooks;                   // ...on a test company's books (the real ones are read again once it is gone)
        public long Raised;                      // money the offer brought, after the underwriters
        public long Fees;                        // fees paid on the way (advisers, auditors, the exchange)
        public int FeeYear = -1;                 // the year the yearly fee and audit were last paid for
        public int FirstDay;                     // the day the first of the player's businesses was taken on (0 not known)
        public int BooksDay;                     // the last statement day counted into the books
        public string Suspended;                 // the company's record while its founder is a guest in another player's city
        public int SetAsideDay;                  // the day it was set aside; statements from it count when it is back
        public long Proceeds;                    // money from businesses of the listed company sold, not yet heard by the market
        public string HandOverTo;                // a bid took the company: the buyer its businesses pass to...
        public int HandOverBy;                   // ...by this day (those that cannot be handed over by then are paid for)...
        public long HandOverOwed;                // ...at the company's price, shared out over them
        public int HandOverCount;                // ...how many there were
        public readonly List<string> HandOver = new List<string>(8);   // ...and the places of those still to go
        public readonly List<BookQuarter> Books = new List<BookQuarter>(16);

        public bool OnTheWay => Stage >= ListingStage.Advised && Stage <= ListingStage.Priced;

        /// <summary>True for a file with nothing in it: the save line then has no part for it.</summary>
        public bool Empty => Stage == ListingStage.None && Books.Count == 0 && AuditDay == 0 && StockId == 0 && Fees == 0 && FirstDay == 0 && string.IsNullOrEmpty(Suspended)
                             && string.IsNullOrEmpty(Name) && AdvisedDay == 0 && AuditFrom == 0 && HandOver.Count == 0 && string.IsNullOrEmpty(HandOverTo) && Proceeds == 0;

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
