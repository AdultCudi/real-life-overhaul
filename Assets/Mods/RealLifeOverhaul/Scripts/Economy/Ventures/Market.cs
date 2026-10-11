using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>Something that happened in the city that a share price answers to. The numbers are in saves (a company's last cause).</summary>
    public enum MarketCause
    {
        None = 0, Storm, Rain, HeatWave, ColdSnap, Ice, StreetMarket, Parade, Protest, FilmShoot, Crime, BreakIn, Accident, Policy, Election, RivalMove, BankRate, Count
    }

    /// <summary>One piece of the day's news, as the bridges in Scripts/Wiring hand it over.</summary>
    public struct MarketNews
    {
        public MarketCause Cause;
        public int Scale;        // per cent of the table's effect, signed: -100 turns it round (a policy relaxed, the bank rate cut)
        public int Lever;        // a policy: which of the council's (the number of Civic's Lever)
        public string Company;   // a rival's move: the listed name of the rival that made it
    }

    /// <summary>The market as it stood before the test tools moved it, so that they can put it back and a save never keeps a test's prices.</summary>
    public sealed class MarketBackup
    {
        public readonly List<Company> Companies = new List<Company>(VenturesCatalogue.Companies);
        public int Steps, Day;
    }

    public enum TradeAnswer { Done, NoCash, OverLimit, NoShares, Nothing }

    /// <summary>
    /// The city's stock market. Game-free. A price moves once a day:
    ///
    ///     move = drift + the company's ordinary move x a seeded bell-shaped number + pull back towards what the
    ///            company is worth + the day's news for its sector (capped) + the city's climate
    ///
    /// What a company is worth rises by the drift each day, so prices follow it over the years and wander about it
    /// in between. The news of one day can move a company by 1.5% at most, less than the 2% it costs to sell and buy
    /// back, so knowing the news before the market moves is worth nothing. Dividends are paid every fifteen market
    /// days on the price of that day. Every purchase and sale costs 1%, five at least.
    /// </summary>
    public static class Market
    {
        public const int RivalMoveBp = 60;       // a rival that makes a move: its own shares
        public const int ElectionBp = 15;        // an election counted: every company (the doubt is over)

        // Basis points by sector: transport, property, retail, food, security, utilities, banking, entertainment.
        private static readonly int[][] Effects =
        {
            null,
            new[] { -60, 0, -40, -20, 10, 40, 0, -40 },      // a storm
            new[] { 10, 0, -15, 0, 0, 0, 0, -15 },           // rain
            new[] { 0, 0, 0, 20, 0, 40, 0, 10 },             // a heat wave
            new[] { -10, 0, -20, 0, 0, 50, 0, -10 },         // a cold snap
            new[] { -50, 0, -15, 0, 0, 20, -10, 0 },         // ice on the roads
            new[] { 0, 0, 30, 40, 0, 0, 0, 10 },             // a street market
            new[] { -15, 0, 10, 20, 10, 0, 0, 40 },          // a parade, a run, fireworks
            new[] { -20, -10, -30, 0, 30, 0, 0, -10 },       // a protest
            new[] { 0, 10, 0, 10, 0, 0, 0, 40 },             // a film crew in town
            new[] { 0, -15, -20, 0, 40, 0, 0, 0 },           // a crime in the paper
            new[] { 0, -10, -25, 0, 50, 0, -10, 0 },         // a break-in
            new[] { -30, 0, 0, 0, 0, 0, -10, 0 },            // a road accident
            null,                                            // a policy: by lever, below
            new[] { ElectionBp, ElectionBp, ElectionBp, ElectionBp, ElectionBp, ElectionBp, ElectionBp, ElectionBp },
            new[] { 0, 0, -10, 0, 0, 0, 0, 0 },              // a rival's move: the other shops (its own shares rise)
            new[] { 0, -30, 0, 0, 0, 0, VenturesCatalogue.RateMoveBp, 0 }   // the bank rate up by a point
        };

        // A council policy tightened or raised by one step (relaxed or cut: the opposite), in the order of Civic's levers:
        // policing, traffic, business tax, trade levy, grants, rents, parking, transit, cleaning, night life, permits, events, wages.
        private static readonly int[][] Policies =
        {
            new[] { 0, 15, 15, 0, -20, 0, 0, 0 }, null, new[] { -20, -20, -20, -20, -20, -20, -20, -20 }, new[] { -10, 0, -15, -15, 0, 0, 0, 0 },
            new[] { 0, 0, 15, 15, 0, 0, 0, 10 }, new[] { 0, 50, -15, -15, 0, 0, 0, 0 }, null, new[] { 40, 0, 10, 0, 0, 0, 0, 0 }, null,
            new[] { 0, 0, 0, 20, 10, 0, 0, 50 }, new[] { 0, 20, 0, 0, 0, 10, 0, 0 }, new[] { 0, 0, 10, 10, 0, 0, 0, 20 }, new[] { 0, 0, -15, -15, 0, 0, 0, -10 }
        };

        public static readonly string[] CauseKeys =
        {
            "reallife_ven_cause_none", "reallife_ven_cause_storm", "reallife_ven_cause_rain", "reallife_ven_cause_heat", "reallife_ven_cause_cold", "reallife_ven_cause_ice",
            "reallife_ven_cause_market", "reallife_ven_cause_parade", "reallife_ven_cause_protest", "reallife_ven_cause_film", "reallife_ven_cause_crime",
            "reallife_ven_cause_break_in", "reallife_ven_cause_accident", "reallife_ven_cause_policy", "reallife_ven_cause_election", "reallife_ven_cause_rival",
            "reallife_ven_cause_rate"
        };

        /// <summary>What one piece of news does to one company, in basis points, before the cap.</summary>
        public static int Effect(in MarketNews news, Company company)
        {
            int cause = (int)news.Cause;
            if (cause <= 0 || cause >= Effects.Length)
            {
                return 0;
            }
            if (news.Cause == MarketCause.RivalMove && company.Rival && news.Company != null && news.Company == company.RivalName)
            {
                return RivalMoveBp;
            }
            int[] row = news.Cause == MarketCause.Policy ? (news.Lever >= 0 && news.Lever < Policies.Length ? Policies[news.Lever] : null) : Effects[cause];
            return row == null ? 0 : row[(int)company.Sector] * (news.Scale == 0 ? 100 : news.Scale) / 100;
        }

        /// <summary>The day's news for one company: every piece added up and held to the cap. Says which piece moved it most.</summary>
        public static int NewsFor(Company company, List<MarketNews> news, out int cause)
        {
            int sum = 0, strongest = 0;
            cause = 0;
            for (int i = 0; news != null && i < news.Count; i++)
            {
                int effect = Effect(news[i], company);
                sum += effect;
                if (Math.Abs(effect) > strongest)
                {
                    strongest = Math.Abs(effect);
                    cause = (int)news[i].Cause;
                }
            }
            return Math.Max(-VenturesCatalogue.NewsCapBp, Math.Min(VenturesCatalogue.NewsCapBp, sum));
        }

        // A bell-shaped number about nothing, at most 3.5 either way: four seeded numbers added up.
        private static double Bell(int seed, int step, int company)
        {
            double sum = 0.0;
            for (int i = 0; i < 4; i++)
            {
                sum += VenturesRandom.Unit(seed, step * 64 + company, 4001 + i);
            }
            return (sum - 2.0) * 1.7320508;
        }

        /// <summary>One day of the market. The same for the same seed, day of the market and news, whatever else has happened.</summary>
        public static void Step(VenturesFile file, int day, int phase, List<MarketNews> news, StringBuilder log)
        {
            file.MarketSteps++;
            file.MarketDay = day;
            for (int i = 0; i < file.Companies.Count; i++)
            {
                Company c = file.Companies[i];
                int cause;
                int newsBp = NewsFor(c, news, out cause);
                double pull = VenturesCatalogue.PullPerDay * Math.Log(c.FairCents / Math.Max(1, c.PriceCents)) * 10000.0;
                double move = VenturesCatalogue.DriftBp + c.VolBp * Bell(file.Seed, file.MarketSteps, c.Id) + pull + newsBp + (phase < 0 ? -1 : phase > 0 ? 1 : 0) * VenturesCatalogue.ClimateBp;
                int was = c.PriceCents;
                c.FairCents *= Math.Exp(VenturesCatalogue.DriftBp / 10000.0);
                c.PriceCents = Math.Max(VenturesCatalogue.PriceFloorCents, (int)Math.Round(was * Math.Exp(move / 10000.0)));
                c.LastBp = (int)((long)(c.PriceCents - was) * 10000L / was);
                c.LastCause = cause;
                c.Remember(c.PriceCents);
                if (log != null)
                {
                    log.Append(i > 0 ? "; " : string.Empty).Append(c.Name).Append(' ').Append((was / 100.0).ToString("0.00")).Append(" to ").Append((c.PriceCents / 100.0).ToString("0.00"))
                        .Append(" (news ").Append(newsBp).Append(" bp").Append(cause > 0 ? ", " + (MarketCause)cause : string.Empty).Append(')');
                }
            }
        }

        // ---------------------------------------------------------------- dividends

        public static bool DividendDue(VenturesFile file)
        {
            return file.MarketSteps > 0 && file.MarketSteps % VenturesCatalogue.DividendEveryDays == 0;
        }

        /// <summary>The market days until the next dividend.</summary>
        public static int DaysToDividend(VenturesFile file)
        {
            return VenturesCatalogue.DividendEveryDays - file.MarketSteps % VenturesCatalogue.DividendEveryDays;
        }

        public static int Dividend(Company company, int shares)
        {
            return (int)((long)shares * company.PriceCents * company.YieldBp / 1000000L);
        }

        /// <summary>Pays every holding its dividend. Returns the total.</summary>
        public static int PayDividends(VenturesFile file, IWallet wallet)
        {
            int total = 0;
            for (int i = 0; i < file.Holdings.Count; i++)
            {
                Holding h = file.Holdings[i];
                Company c = file.Company(h.CompanyId);
                int amount = c != null ? Dividend(c, h.Shares) : 0;
                if (amount > 0)
                {
                    wallet.Give(amount, VenturesMoney.Dividend, c.Name);
                    h.Dividends += amount;
                    total += amount;
                }
            }
            file.DividendsTotal += total;
            return total;
        }

        // ---------------------------------------------------------------- trading

        /// <summary>What so many shares fetch: whole units, rounded down.</summary>
        public static int Value(Company company, int shares)
        {
            return (int)((long)shares * company.PriceCents / 100L);
        }

        /// <summary>What so many shares cost: whole units, rounded up.</summary>
        public static int Cost(Company company, int shares)
        {
            return (int)(((long)shares * company.PriceCents + 99L) / 100L);
        }

        public static int Commission(int amount)
        {
            return amount <= 0 ? 0 : Math.Max(VenturesCatalogue.CommissionLeast, (int)(((long)amount * VenturesCatalogue.CommissionBp + 9999L) / 10000L));
        }

        /// <summary>The most shares of a company that can be bought now: the holding limit, and what the cash carries with the commission.</summary>
        public static int MostBuyable(VenturesFile file, Company company, int cash)
        {
            Holding held = file.Holding(company.Id);
            int room = VenturesCatalogue.HoldingMost - (held != null ? held.Shares : 0);
            int byCash = (int)Math.Min(room, (long)cash * 100L / Math.Max(1, company.PriceCents));
            while (byCash > 0 && Cost(company, byCash) + Commission(Cost(company, byCash)) > cash)
            {
                byCash--;
            }
            return Math.Max(0, Math.Min(room, byCash));
        }

        public static TradeAnswer Buy(VenturesFile file, Company company, int shares, IWallet wallet)
        {
            if (shares <= 0 || company == null)
            {
                return TradeAnswer.Nothing;
            }
            Holding held = file.Holding(company.Id);
            if ((held != null ? held.Shares : 0) + shares > VenturesCatalogue.HoldingMost)
            {
                return TradeAnswer.OverLimit;
            }
            int cost = Cost(company, shares), fee = Commission(cost);
            if (wallet.Cash < cost + fee || !wallet.Take(cost, VenturesMoney.SharesBought, company.Name))
            {
                return TradeAnswer.NoCash;
            }
            wallet.Take(fee, VenturesMoney.Commission, company.Name);
            if (held == null)
            {
                held = new Holding { CompanyId = company.Id };
                file.Holdings.Add(held);
            }
            held.Shares += shares;
            held.Cost += cost + fee;
            return TradeAnswer.Done;
        }

        public static TradeAnswer Sell(VenturesFile file, Company company, int shares, IWallet wallet)
        {
            Holding held = company != null ? file.Holding(company.Id) : null;
            if (shares <= 0 || held == null)
            {
                return held == null ? TradeAnswer.NoShares : TradeAnswer.Nothing;
            }
            if (shares > held.Shares)
            {
                return TradeAnswer.NoShares;
            }
            int proceeds = Value(company, shares), fee = Math.Min(proceeds, Commission(proceeds));
            wallet.Give(proceeds, VenturesMoney.SharesSold, company.Name);
            wallet.Take(fee, VenturesMoney.Commission, company.Name);
            held.Cost -= (int)((long)held.Cost * shares / held.Shares);
            held.Shares -= shares;
            if (held.Shares <= 0)
            {
                file.Holdings.Remove(held);
            }
            return TradeAnswer.Done;
        }

        /// <summary>What the holdings are worth at today's prices, and what they cost.</summary>
        public static int Worth(VenturesFile file, out int cost)
        {
            int worth = 0;
            cost = 0;
            for (int i = 0; i < file.Holdings.Count; i++)
            {
                Company c = file.Company(file.Holdings[i].CompanyId);
                worth += c != null ? Value(c, file.Holdings[i].Shares) : 0;
                cost += file.Holdings[i].Cost;
            }
            return worth;
        }

        // ---------------------------------------------------------------- for the test tools

        public static Company Copy(Company c)
        {
            var copy = new Company
            {
                Id = c.Id, Sector = c.Sector, Rival = c.Rival, NameIndex = c.NameIndex, RivalName = c.RivalName, PriceCents = c.PriceCents, FairCents = c.FairCents,
                VolBp = c.VolBp, YieldBp = c.YieldBp, LastBp = c.LastBp, LastCause = c.LastCause, HistoryCount = c.HistoryCount
            };
            Array.Copy(c.History, copy.History, c.History.Length);
            return copy;
        }

        public static MarketBackup Backup(VenturesFile file)
        {
            var backup = new MarketBackup { Steps = file.MarketSteps, Day = file.MarketDay };
            for (int i = 0; i < file.Companies.Count; i++)
            {
                backup.Companies.Add(Copy(file.Companies[i]));
            }
            return backup;
        }

        public static void Restore(VenturesFile file, MarketBackup backup)
        {
            file.MarketSteps = backup.Steps;
            file.MarketDay = backup.Day;
            file.Companies.Clear();
            file.Companies.AddRange(backup.Companies);
        }
    }
}
