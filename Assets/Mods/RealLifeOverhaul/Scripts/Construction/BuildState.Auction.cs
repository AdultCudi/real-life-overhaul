using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>Where an auction of a city lot stands.</summary>
    public enum AuctionState
    {
        Open = 0,
        Sold = 1,       // knocked down to the leading bid; the lot has changed hands
        Passed = 2,     // no bid reached the reserve: the lot stays for sale
        Lapsed = 3      // the player led and could not pay at the hammer
    }

    /// <summary>
    /// A city lot sold by auction on the calendar (<see cref="AuctionRules"/>): its opening bid and reserve, the leading bid and who made it (the player or one of the
    /// city's eight builders), the last few bids, and the auctioneer, a person of the shared people pool. Saved in the line's "^e:" part.
    /// </summary>
    public sealed class AuctionFile
    {
        public const int LogSize = 4;

        public int Id;
        public string Lot = string.Empty;        // the lot's id (a plain lot, or a free space of the grid)
        public int Opened, Ends;                 // the first day and the day of the hammer
        public int Area;                         // m2: the builders' licence levels decide which lots they bid on
        public long List;                        // the lot's list price when it was put up
        public long Opening, Reserve;            // the first bid asked and the least price that sells
        public long Bid;                         // the leading bid; 0 for none
        public int Leader = -2;                  // -2 nobody, -1 the player, else a builder (its index)
        public int Bids;
        public long Mine;                        // the player's own highest bid; 0 for none
        public AuctionState State;
        public int Closed;                       // the day it was settled
        public int Round;                        // the last day the rivals' bidding was run for
        public int HallId;                       // the auctioneer's id in the people pool (0: a name of the catalogue)
        public string Hall = string.Empty;       // ... and their name
        public readonly int[] LogWho = new int[LogSize];
        public readonly long[] LogBid = new long[LogSize];
        public readonly int[] LogDay = new int[LogSize];
        public int LogCount;

        public bool Live => State == AuctionState.Open;

        /// <summary>Notes a bid in the log, the oldest forgotten.</summary>
        public void Note(int who, long amount, int day)
        {
            if (LogCount == LogSize)
            {
                for (int i = 1; i < LogSize; i++)
                {
                    LogWho[i - 1] = LogWho[i];
                    LogBid[i - 1] = LogBid[i];
                    LogDay[i - 1] = LogDay[i];
                }
                LogCount--;
            }
            LogWho[LogCount] = who;
            LogBid[LogCount] = amount;
            LogDay[LogCount] = day;
            LogCount++;
        }
    }

    /// <summary>Where the player's offer for a city lot stands.</summary>
    public enum CityOfferState
    {
        Waiting = 0,
        Accepted = 1,
        Refused = 2,
        Lapsed = 3
    }

    /// <summary>The player's offer, under the lot's list price, for a city lot that is not at auction; answered the next working day by a seeded chance that rises with the price.</summary>
    public sealed class CityOfferFile
    {
        public int Id;
        public string Lot = string.Empty;
        public long Price, List;                 // what is offered and the lot's list price when it was made
        public int Made, Due, Answered;
        public CityOfferState State;

        public bool Waiting => State == CityOfferState.Waiting;
    }

    public sealed partial class BuildState
    {
        public readonly List<AuctionFile> Auctions = new List<AuctionFile>(8);
        public readonly List<CityOfferFile> CityOffers = new List<CityOfferFile>(4);
        public int NextAuctionDay;               // the day the next lots go under the hammer; 0 before the calendar first looked

        /// <summary>The auction with this number, or null.</summary>
        public AuctionFile Auction(int id)
        {
            for (int i = 0; id > 0 && i < Auctions.Count; i++)
            {
                if (Auctions[i].Id == id)
                {
                    return Auctions[i];
                }
            }
            return null;
        }

        /// <summary>The auction still running for a lot, or null.</summary>
        public AuctionFile AuctionOn(string lot)
        {
            for (int i = 0; !string.IsNullOrEmpty(lot) && i < Auctions.Count; i++)
            {
                if (Auctions[i].Live && string.Equals(Auctions[i].Lot, lot, StringComparison.Ordinal))
                {
                    return Auctions[i];
                }
            }
            return null;
        }

        /// <summary>The newest auction of a lot, run or over, or null.</summary>
        public AuctionFile LastAuction(string lot)
        {
            AuctionFile last = null;
            for (int i = 0; i < Auctions.Count; i++)
            {
                if (string.Equals(Auctions[i].Lot, lot, StringComparison.Ordinal) && (last == null || Auctions[i].Opened >= last.Opened))
                {
                    last = Auctions[i];
                }
            }
            return last;
        }

        /// <summary>The player's offer for a city lot, with this number, or null.</summary>
        public CityOfferFile CityOffer(int id)
        {
            for (int i = 0; id > 0 && i < CityOffers.Count; i++)
            {
                if (CityOffers[i].Id == id)
                {
                    return CityOffers[i];
                }
            }
            return null;
        }

        /// <summary>The player's newest offer for a city lot, answered or waiting, or null.</summary>
        public CityOfferFile LastCityOffer(string lot)
        {
            CityOfferFile last = null;
            for (int i = 0; i < CityOffers.Count; i++)
            {
                if (string.Equals(CityOffers[i].Lot, lot, StringComparison.Ordinal) && (last == null || CityOffers[i].Made >= last.Made))
                {
                    last = CityOffers[i];
                }
            }
            return last;
        }

        /// <summary>The player's offers that still wait for an answer.</summary>
        public int CityOffersWaiting()
        {
            int n = 0;
            for (int i = 0; i < CityOffers.Count; i++)
            {
                n += CityOffers[i].Waiting ? 1 : 0;
            }
            return n;
        }

        /// <summary>The auctions over and the offers answered beyond the most kept let go, the oldest first; those still open or waiting are never.</summary>
        public void TidyAuctions()
        {
            for (int i = 0; i < Auctions.Count && Auctions.Count > AuctionRules.KeptMost; i++)
            {
                if (!Auctions[i].Live)
                {
                    Auctions.RemoveAt(i);
                    i--;
                }
            }
            for (int i = 0; i < CityOffers.Count && CityOffers.Count > AuctionRules.OffersKept; i++)
            {
                if (!CityOffers[i].Waiting)
                {
                    CityOffers.RemoveAt(i);
                    i--;
                }
            }
        }
    }
}
