using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Part of the market director: the player's way to the market, from the advisers to the prospectus.</summary>
    public sealed partial class MarketDirector
    {
        public bool Requirements(out Requirement[] requirements)
        {
            requirements = _requirements;
            ListingFile f = Listing;
            if (f == null)
            {
                return false;
            }
            return ListingBook.Requirements(f, Facts(), Save.Day, Year, Level, _requirements);
        }

        /// <summary>What the company looks worth to the advisers now, from its books.</summary>
        public double Worth()
        {
            ListingFile f = Listing;
            if (f == null)
            {
                return 0.0;
            }
            Named(f);
            return ListingBook.Worth(f, _engine.State.Me, Save.Day, Year, _engine.State.Env, Facts().Debt);
        }

        // The company's name and trade to begin with, until the prospectus gives it its own.
        private void Named(ListingFile f)
        {
            if (string.IsNullOrEmpty(f.Name))
            {
                f.Name = MarketBridges.Name != null ? MarketBridges.Name() ?? string.Empty : string.Empty;
                f.Ticker = ListingRules.TickerOf(f.Name);
            }
            if (f.Trade < 0 && MarketBridges.Trade != null)
            {
                f.Trade = MarketBridges.Trade();
                f.Sector = (int)SectorFor(f.Trade);
            }
        }

        // The sector of the first listed company in the trade, else business services.
        private MarketSector SectorFor(int trade)
        {
            if (trade >= 0 && trade < MarketInputs.Trades.Length)
            {
                List<Stock> stocks = _engine.State.Stocks;
                for (int i = 0; i < stocks.Count; i++)
                {
                    if (stocks[i].Listed && stocks[i].Own == null && stocks[i].Def.Trade == MarketInputs.Trades[trade])
                    {
                        return stocks[i].Sector;
                    }
                }
            }
            return MarketSector.Services;
        }

        public long Retainer()
        {
            return ListingBook.Retainer(Worth(), Level);
        }

        public long AuditFee()
        {
            ListingFile f = Listing;
            if (f == null)
            {
                return 0;
            }
            ListingBook.Last(f, Save.Day, Year, 4, out long turnover, out long _, out int _);
            return ListingBook.AuditFee(turnover, Level);
        }

        private OwnWhy WayBlocked()
        {
            if (!Running)
            {
                return OwnWhy.NotListed;
            }
            if (_following || GoingPublic.IsGuest)
            {
                return OwnWhy.Suspended;
            }
            if (!Settings.MarketsListing)
            {
                return OwnWhy.Off;
            }
            return OwnWhy.None;
        }

        /// <summary>The advisers taken on: every requirement but the audit met, their retainer paid now.</summary>
        public OwnWhy Engage()
        {
            FinishMoment();
            OwnWhy why = WayBlocked();
            ListingFile f = Listing;
            if (why != OwnWhy.None)
            {
                return why;
            }
            if (f.Stage != ListingStage.None || _engine.MineListed() != null)
            {
                return OwnWhy.TooMuch;
            }
            Requirements(out Requirement[] r);
            if (!ListingBook.ReadyForAdvisers(r))
            {
                return OwnWhy.NotReady;
            }
            long fee = Retainer();
            Named(f);
            if (!_engine.Wallet.Take(fee, ListingMoney.Advisers, f.Name))
            {
                return OwnWhy.NoMoney;
            }
            f.Stage = ListingStage.Advised;
            f.AdvisedDay = Save.Day;
            f.Fees += fee;
            Logger?.Info("Listing: advisers engaged on day " + Save.Day + " for " + fee + " (the company looks worth " + Worth().ToString("F0") + ").");
            Moved();
            return OwnWhy.None;
        }

        /// <summary>The audit ordered: its fee now, signed some days on.</summary>
        public OwnWhy OrderAudit()
        {
            FinishMoment();
            OwnWhy why = WayBlocked();
            ListingFile f = Listing;
            if (why != OwnWhy.None)
            {
                return why;
            }
            if (f.Stage != ListingStage.Advised && f.Stage != ListingStage.Audited)
            {
                return OwnWhy.NotReady;
            }
            long fee = AuditFee();
            if (!_engine.Wallet.Take(fee, ListingMoney.Audit, f.Name))
            {
                return OwnWhy.NoMoney;
            }
            f.Stage = ListingStage.Auditing;
            f.AuditFrom = Save.Day;
            f.AuditDone = Save.Day + ListingCatalogue.AuditDays;
            f.Fees += fee;
            Logger?.Info("Listing: the audit ordered on day " + Save.Day + " for " + fee + ", to be signed on day " + f.AuditDone + ".");
            Moved();
            return OwnWhy.None;
        }

        /// <summary>The prospectus published with every requirement met: the book is open from today and priced after its last day.</summary>
        public OwnWhy Publish(string name, int floatBp, int walkCents)
        {
            FinishMoment();
            OwnWhy why = WayBlocked();
            ListingFile f = Listing;
            if (why != OwnWhy.None)
            {
                return why;
            }
            if (f.Stage != ListingStage.Audited || !Requirements(out Requirement[] _))
            {
                return OwnWhy.NotReady;
            }
            Named(f);
            if (!string.IsNullOrEmpty(name))
            {
                f.Name = MarketStore.Clean(name.Trim());
                f.Ticker = ListingRules.TickerOf(f.Name);
            }
            ListingFacts facts = Facts();
            MarketEnv env = _engine.State.Env;
            int day = Save.Day;
            double worth = ListingBook.Worth(f, _engine.State.Me, day, Year, env, facts.Debt);
            f.OldShares = ListingBook.FounderShares(worth, Level);
            f.NewShares = ListingBook.NewShares(f.OldShares, ListingBook.FloatStep(floatBp));
            ListingBook.Prospectus(f, floatBp, ListingBook.FairCents(f, _engine.State.Me, day, Year, env, facts.Debt), walkCents);
            f.Stage = ListingStage.Building;
            f.BookFrom = day;
            f.BookEnd = MarketTrading.MarketDays(day, ListingCatalogue.BookDays);
            f.PriceCents = 0;
            f.ListDay = 0;
            Logger?.Info("Listing: the prospectus of " + f.Name + " (" + f.Ticker + ") on day " + day + ": " + f.NewShares + " new shares (" + f.FloatBp + " bp) beside the founder's "
                         + f.OldShares + ", a share worth " + f.FairCents + " cents by the books, the range " + f.LowCents + " to " + f.HighCents + ", the least taken " + f.WalkCents
                         + "; the book is priced at the open of day " + f.BookEnd + ".");
            Moved();
            return OwnWhy.None;
        }

        /// <summary>The offer withdrawn before it is priced; the audit stands while good, nothing paid comes back.</summary>
        public bool Withdraw()
        {
            FinishMoment();
            ListingFile f = Listing;
            if (f == null || !f.OnTheWay || f.Stage == ListingStage.Priced || _following)
            {
                return false;
            }
            f.Stage = f.Stage == ListingStage.Building ? ListingStage.Audited : ListingStage.None;
            f.BookEnd = 0;
            Logger?.Info("Listing: the offer withdrawn on day " + Save.Day + ".");
            Moved();
            return true;
        }
    }
}
