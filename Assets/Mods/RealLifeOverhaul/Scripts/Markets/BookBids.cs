using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Who bids in the book. Saved as a number. A rival is a template index; the others are the city's own kinds, not named people.</summary>
    public enum BidWho : byte
    {
        Fund = 0,
        Investor = 1,
        Retail = 2,
        Rival = 3
    }

    /// <summary>One bid in the book: a price, a size, and what it was given. Cut is 1 when it was scaled back, 2 when it was under the price.</summary>
    public struct BookBid
    {
        public BidWho Who;
        public int WhoId;
        public int Cents;
        public long Size;
        public long Filled;
        public byte Cut;
    }

    /// <summary>
    /// The book as named bids, built once when the price is set, from the same secret as the price and a draw of its own
    /// so the price itself does not move. Largest prices are filled first. The founder can set the price inside the range
    /// and the same bids are cut again. Game-free.
    /// </summary>
    public static class BookBids
    {
        public const int Most = 8;
        public const int Under = 2;
        public const int Scaled = 1;

        /// <summary>Fills the file's bids at <paramref name="priceCents"/> and the cover. The price already decided is not changed.</summary>
        public static void Fill(ListingFile file, ulong secret, int stockId, int priceCents)
        {
            if (file == null || priceCents <= 0)
            {
                return;
            }
            long offered = file.NewShares > 0 ? file.NewShares : 100000L;
            for (int i = 0; i < Most; i++)
            {
                int who = i % 4;
                int cents = file.LowCents > 0 && file.HighCents > file.LowCents
                    ? file.LowCents + (int)(MarketRandom.Unit(secret, MarketRandom.BookBid, stockId, i) * (file.HighCents - file.LowCents + ListingCatalogue.PriceStepCents))
                    : priceCents;
                cents = ListingBook.Step(cents, false);
                long size = offered / 4 + (long)(MarketRandom.Unit(secret, MarketRandom.BookBid, stockId, 16 + i) * (offered / 2));
                if (size < 1)
                {
                    size = 1;
                }
                file.Bids[i] = new BookBid { Who = (BidWho)who, WhoId = i % Math.Max(1, MarketCatalogue.RivalTemplates.Length), Cents = Math.Max(1, cents), Size = size };
            }
            file.BidCount = Most;
            Allocate(file, priceCents);
        }

        /// <summary>The founder sets the price inside the range the book already printed. The bids stay; who is filled changes.</summary>
        public static int SetPrice(ListingFile file, int cents)
        {
            if (file == null || file.BidCount <= 0)
            {
                return 0;
            }
            int low = file.LowCents > 0 ? file.LowCents : 1;
            int high = file.HighCents > low ? file.HighCents : low;
            if (cents < low)
            {
                cents = low;
            }
            if (cents > high)
            {
                cents = high;
            }
            cents = ListingBook.Step(cents, false);
            file.SetCents = cents;
            file.PriceCents = cents;
            Allocate(file, cents);
            return cents;
        }

        public static void Allocate(ListingFile file, int priceCents)
        {
            long offered = file.NewShares > 0 ? file.NewShares : 0;
            long cover = 0;
            for (int i = 0; i < file.BidCount; i++)
            {
                BookBid b = file.Bids[i];
                b.Filled = 0;
                b.Cut = b.Cents < priceCents ? (byte)Under : (byte)0;
                if (b.Cents >= priceCents)
                {
                    cover += b.Size;
                }
                file.Bids[i] = b;
            }
            // Largest price first. A handful of bids: a straight pass, no extra list.
            long left = offered;
            for (int n = 0; n < file.BidCount && left > 0; n++)
            {
                int best = -1;
                for (int i = 0; i < file.BidCount; i++)
                {
                    BookBid b = file.Bids[i];
                    if (b.Cents < priceCents || b.Filled > 0)
                    {
                        continue;
                    }
                    if (best < 0 || b.Cents > file.Bids[best].Cents)
                    {
                        best = i;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                BookBid take = file.Bids[best];
                take.Filled = take.Size < left ? take.Size : left;
                take.Cut = take.Filled < take.Size ? (byte)Scaled : (byte)0;
                left -= take.Filled;
                file.Bids[best] = take;
            }
            file.CoverBp = offered > 0 ? (int)(cover * 10000L / offered) : 0;
        }
    }
}
