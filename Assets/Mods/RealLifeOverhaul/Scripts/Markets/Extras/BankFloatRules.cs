using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Why the player's bank cannot go to the market, or its way there stopped. Each has a sentence (ExtrasText).</summary>
    public enum BankWhy
    {
        None = 0,
        Off,             // the extras are switched off, or the market is not this machine's own (a guest in another player's city)
        NoBank,          // no bank of the player's runs
        Test,            // a bank the test tools founded
        Young,           // it has not run long enough
        Books,           // too few complete quarters of its own books
        Profit,          // too few of them in profit
        Regulator,       // watched or in breach, or in a run
        Company,         // the player's whole company is on the market, or on its way: the bank is part of it already
        OnTheWay,        // the advisers are engaged already
        Listed,          // it trades already
        NoMoney,         // the retainer
        Float,           // not one of the shares that may be offered
        Full,            // the market holds as many of the player's firms, or companies, as it may
        Count
    }

    /// <summary>The player's bank as the market needs to know it, read from the bank on a press or a day. Game-free.</summary>
    public struct BankFacts
    {
        public bool Running, Test, Clear;        // Clear: neither watched nor in breach, and no run
        public bool Gone;                        // sold, or closed by the regulator: its stock leaves the market
        public int FoundedDay;
        public long CapitalCents;                // its own capital: what its owner paid in and what it kept of its profit; once gone, what it fetched or left
        public string Name;
    }

    /// <summary>
    /// A bank of the player's own on the stock market by itself. Until now a bank had no books the market could read: its
    /// takings were lines of the headquarters' statement, which the market counts only inside the player's whole company,
    /// and the one way to list a single firm asked for a tech firm's cap table. Here the bank's own days (its income and its
    /// profit after savers' interest, losses and running costs) are kept as it books them, a day at a time, and make the
    /// quarters a listing is priced on. Its worth is the market's own model on those books and never more than
    /// <see cref="ExtrasCatalogue.BankBookMost"/> times its own capital: savers' deposits and the cash that covers them
    /// are in neither figure. The offer's money goes into the bank's capital, never the player's pocket, and from then on
    /// everything the bank pays its owner is every holder's by their shares. Game-free.
    /// </summary>
    public static class BankFloatRules
    {
        /// <summary>A day of the bank's own books, in cents: kept for the market's mornings and added to its quarter. A day booked twice counts once.</summary>
        public static void AddDay(BankFloat b, int day, long incomeCents, long profitCents, int year)
        {
            for (int i = b.Days.Count - 1; i >= 0; i--)
            {
                if (b.Days[i].Day == day)
                {
                    return;
                }
            }
            b.Days.Add(new BankDayNote { Day = day, Income = incomeCents, Profit = profitCents });
            while (b.Days.Count > ExtrasCatalogue.BankDaysKept)
            {
                b.Days.RemoveAt(0);
            }
            int quarter = MarketCatalogue.QuarterOf(day, Math.Max(8, year));
            int at = -1;
            for (int i = 0; i < b.Books.Count && at < 0; i++)
            {
                at = b.Books[i].Quarter == quarter ? i : -1;
            }
            if (at < 0)
            {
                b.Books.Add(new BookQuarter { Quarter = quarter });
                at = b.Books.Count - 1;
                while (b.Books.Count > ExtrasCatalogue.BankQuartersKept)
                {
                    b.Books.RemoveAt(0);
                    at--;
                }
            }
            BookQuarter q = b.Books[at];
            q.Revenue += incomeCents / 100L;
            q.Profit += profitCents / 100L;
            q.Days++;
            b.Books[at] = q;
        }

        /// <summary>The bank's takings and profit over the days from one up to, not with, another: whole money, as a firm's statements give it.</summary>
        public static void Sum(BankFloat b, int from, int to, out long revenue, out long profit)
        {
            long income = 0, made = 0;
            for (int i = 0; i < b.Days.Count; i++)
            {
                if (b.Days[i].Day >= from && b.Days[i].Day < to)
                {
                    income += b.Days[i].Income;
                    made += b.Days[i].Profit;
                }
            }
            revenue = income / 100L;
            profit = made / 100L;
        }

        /// <summary>Complete quarters before the one a day is in, of the last four, and how many of them made a profit.</summary>
        public static int Quarters(BankFloat b, int day, int year, out int profitable)
        {
            int now = MarketCatalogue.QuarterOf(day, Math.Max(8, year)), found = 0;
            profitable = 0;
            for (int i = 0; i < b.Books.Count; i++)
            {
                BookQuarter q = b.Books[i];
                if (q.Quarter < now && q.Quarter >= now - 4 && q.Days > 0)
                {
                    found++;
                    profitable += q.Profit > 0 ? 1 : 0;
                }
            }
            return found;
        }

        /// <summary>Why the bank may not engage advisers now.</summary>
        public static BankWhy Ready(BankFloat b, in BankFacts f, int day, int year, bool on, bool companyListed, bool companyOnTheWay)
        {
            if (!on)
            {
                return BankWhy.Off;
            }
            if (b.Stage == BankStage.Listed)
            {
                return BankWhy.Listed;
            }
            if (b.Stage == BankStage.Advised)
            {
                return BankWhy.OnTheWay;
            }
            if (!f.Running)
            {
                return BankWhy.NoBank;
            }
            if (f.Test)
            {
                return BankWhy.Test;
            }
            if (companyListed || companyOnTheWay)
            {
                return BankWhy.Company;
            }
            if (day - f.FoundedDay < ExtrasCatalogue.BankAgeYears * Math.Max(8, year))
            {
                return BankWhy.Young;
            }
            if (Quarters(b, day, year, out int profitable) < ExtrasCatalogue.BankQuartersLeast)
            {
                return BankWhy.Books;
            }
            if (profitable < ExtrasCatalogue.BankProfitQuartersLeast)
            {
                return BankWhy.Profit;
            }
            return f.Clear ? BankWhy.None : BankWhy.Regulator;
        }

        /// <summary>The bank's books as a listing's file, for the market's own model: nothing of the player's other businesses in it.</summary>
        public static ListingFile File(BankFloat b, string name)
        {
            var f = new ListingFile
            {
                Stage = ListingStage.Listed, Name = name ?? string.Empty, Ticker = ListingRules.TickerOf(name), Sector = (int)MarketSector.Banks, Trade = -1, StockId = b.StockId,
                OldShares = b.OldShares, NewShares = b.NewShares, PriceCents = b.PriceCents, FloatBp = b.FloatBp, LowCents = b.LowCents, HighCents = b.HighCents, FairCents = b.FairCents
            };
            for (int i = 0; i < b.Books.Count; i++)
            {
                if (b.Books[i].Days > 0)
                {
                    f.Books.Add(b.Books[i]);
                }
            }
            return f;
        }

        /// <summary>
        /// What the bank is worth, in money: the market's model of a bank on its own books' profit, never over so many times its own
        /// capital. Deposits are in neither: a bank that took in twice the savings with the same profit and capital is worth the same.
        /// </summary>
        public static double Worth(BankFloat b, in BankFacts f, ulong owner, int day, int year, in MarketEnv env)
        {
            double model = ListingBook.Worth(File(b, f.Name), owner, day, year, env, 0L);
            double capital = Math.Max(0L, f.CapitalCents) / 100.0;
            return Math.Max(0.0, Math.Min(model, capital * ExtrasCatalogue.BankBookMost));
        }

        public static int FloatStep(int bp)
        {
            int best = ExtrasCatalogue.BankFloatSteps[0];
            for (int i = 0; i < ExtrasCatalogue.BankFloatSteps.Length; i++)
            {
                if (Math.Abs(ExtrasCatalogue.BankFloatSteps[i] - bp) < Math.Abs(best - bp))
                {
                    best = ExtrasCatalogue.BankFloatSteps[i];
                }
            }
            return best;
        }

        /// <summary>
        /// The prospectus: the founder's shares (as many as make one worth about the usual price), the new shares offered, and the
        /// range round the worth a share less the usual discount, as any company's. Returns the worth it was written on.
        /// </summary>
        public static double Prospectus(BankFloat b, in BankFacts f, int floatBp, ulong owner, int day, int year, in MarketEnv env)
        {
            double level = Math.Max(100, env.PriceLevel) / 1000.0;
            double worth = Worth(b, f, owner, day, year, env);
            b.FloatBp = FloatStep(floatBp);
            b.OldShares = ListingBook.FounderShares(worth, level);
            b.NewShares = ListingBook.NewShares(b.OldShares, b.FloatBp);
            // The new shares bring money in: a share is the worth before it over the shares before it.
            int fair = (int)Math.Max(1.0, Math.Min(int.MaxValue, Math.Round(worth * 100.0 / Math.Max(1L, b.OldShares))));
            var file = new ListingFile();
            ListingBook.Prospectus(file, b.FloatBp, fair, ListingBook.WalkLow);
            b.FairCents = file.FairCents;
            b.LowCents = file.LowCents;
            b.HighCents = file.HighCents;
            return worth;
        }

        /// <summary>The price the book comes to on its day, drawn as any company's is; 0 when it is under the range's bottom and the offer is called off.</summary>
        public static int Price(BankFloat b, string name, ulong secret, int stockId, int indexMonthBp, int standing)
        {
            ListingFile file = File(b, name);
            int price = ListingBook.Price(file, secret, stockId, indexMonthBp, standing, out double _);
            return price < b.LowCents ? 0 : price;
        }

        /// <summary>The offer's money: what the new shares bring, the underwriters' and the exchange's fees out of it, and what reaches the bank's capital.</summary>
        public static long Raised(BankFloat b, int priceCents, double level, out long underwriting, out long exchange)
        {
            long gross = MarketTrading.Value(b.NewShares, priceCents, false);
            underwriting = ListingBook.Underwriting(gross, level);
            double worth = (b.OldShares + b.NewShares) * (double)priceCents / 100.0;
            exchange = Math.Min(Math.Max(0L, gross - underwriting), ListingBook.ListingFee(worth, level));
            return Math.Max(0L, gross - underwriting - exchange);
        }

        /// <summary>The bank as a firm's listing for the market: its own shares, price, books and a lock-up as any founder's.</summary>
        public static FirmListing Listing(BankFloat b, string name, int day, int year)
        {
            var books = new BookQuarter[b.Books.Count];
            b.Books.CopyTo(books);
            return new FirmListing
            {
                Place = ExtrasCatalogue.BankPlace, Name = name, Sector = (int)MarketSector.Banks, Trade = -1, Shares = b.OldShares + b.NewShares, Founder = b.OldShares,
                Float = b.NewShares, PriceCents = Math.Max(1, b.PriceCents), LockDay = day + Math.Max(1, Math.Max(8, year) / ListingCatalogue.LockUpYearShare), Books = books
            };
        }

        /// <summary>Of a sum the bank pays its owner, the owner's own part once other people hold shares of it: by the founder's shares, rounded down.</summary>
        public static long OwnerPart(long cents, long founder, long shares)
        {
            if (cents <= 0 || shares <= 0 || founder >= shares)
            {
                return Math.Max(0L, cents);
            }
            return (long)((decimal)cents * Math.Max(0L, founder) / shares);
        }

        public static bool IsBank(string place)
        {
            return place == ExtrasCatalogue.BankPlace;
        }
    }
}
