using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The player's own company and the city's market, in the game (Markets/Listing; the rules are game-free: ListingRules,
    /// ListingBook, ListingVotes, MarketEngine.Own). What it does here:
    ///
    ///   who the player is   a hash of their save (MarketState.Me), at every start: a company whose founder it is, is theirs
    ///   the way to market   the advisers engaged, the audit ordered and signed, the prospectus published (the book is then
    ///                       priced and the company listed by the market itself, at its opens), or the offer withdrawn
    ///   the books           each new day, the statement days since the last into the company's quarters (MarketBridges.Books:
    ///                       the player's own businesses only)
    ///   the year            once a game year while listed: the exchange's fee and the year's audit, whose findings the market hears
    ///   what the founder    a deal in their shares, a buy-back, new shares, a dividend proposed, their pay, taking it private:
    ///   asks for            checked here (and the cash for it) and put on the tape, so that every market plays it at the same moment
    ///   a takeover          a bid that wins: the company's businesses pass to the buyer (MarketBridges.HandOver), a few an hour
    ///                       as the game lets them go, and what cannot be handed over in time is paid for
    ///   the news            told to the founder in the game's notifications whether or not they watch their company
    ///   tax on dividends    withheld from every dividend the player is paid (ListingVotes.DividendTax)
    ///
    /// The cost: a comparison of the day and the hour once a second; a day's work on a new day (one look at the day's
    /// statements); nothing per frame.
    /// </summary>
    public sealed partial class MarketDirector
    {
        private int _ownDay = -1, _ownHour = -1;
        private readonly List<string> _places = new List<string>(16);
        private readonly Requirement[] _requirements = new Requirement[ListingBook.NeedCount];

        /// <summary>Who the player of a save is, as the market knows its founders: never nothing.</summary>
        public static ulong OwnerOf(GameInstance save)
        {
            string who = save == null ? string.Empty : (save.characterId ?? string.Empty) + "|" + (save.seed ?? string.Empty);
            ulong h = MarketRandom.Mix64(MarketRandom.Of("player|" + who));
            return h == 0UL ? 1UL : h;
        }

        private static double Level => Prices.Level / 1000.0;

        private int Year => _engine != null ? Math.Max(8, _engine.State.YearDays) : 60;

        /// <summary>The player's own listed company; null if they have none on this market now.</summary>
        public Stock MyCompany => Running ? _engine.MineListed() : null;

        /// <summary>The player's way to the market and their company's books; null while the market is not running.</summary>
        public ListingFile Listing => Running ? _engine.State.Listing : null;

        /// <summary>True while the player's company is set aside (they are a guest in another player's city).</summary>
        public bool CompanyAside => Running && (_following || !string.IsNullOrEmpty(_engine.State.Listing.Suspended));

        /// <summary>The Stocks app opened on the player's own company's page (the Company app's button).</summary>
        public void OpenListing()
        {
            OpenCompany(MarketApp.OwnPage);
        }

        /// <summary>One line on where the player's company stands with the market, for the Company app; null for nothing to say.</summary>
        public string ListingSummary()
        {
            ListingFile f = Listing;
            if (f == null)
            {
                return null;
            }
            Stock own = _engine.MineListed();
            if (own != null)
            {
                return MarketText.Fill("reallife_lst_summary_listed", "name", own.Name, "price", MarketText.Price(own.Last), "change", MarketText.Change(MarketApp.Bp(own.Last, own.PrevClose)),
                    "share", MarketText.Percent(own.Own.StakeBp(own)), "worth", MarketText.Large(own.Own.Founder * (own.Last / 100.0)));
            }
            if (CompanyAside)
            {
                return MarketText.T("reallife_lst_aside");
            }
            if (f.Stage == ListingStage.None)
            {
                if (!Settings.MarketsListing)
                {
                    return null;
                }
                Requirements(out Requirement[] r);
                int met = 0;
                for (int i = 0; i < r.Length; i++)
                {
                    met += r[i].Met ? 1 : 0;
                }
                return MarketText.Fill("reallife_lst_summary_none", "met", met.ToString(), "count", r.Length.ToString());
            }
            return MarketText.Fill("reallife_lst_summary_way", "stage", ListingText.Stage(f.Stage));
        }

        // ---------------------------------------------------------------- starting, following, alone

        private void OwnStarted(GameInstance save)
        {
            MarketState s = _engine.State;
            s.Me = OwnerOf(save);
            s.Following = false;
            _ownDay = -1;
            _ownHour = -1;
            int moved = _engine.Alone();   // (a save made as a guest: the host's company leaves, the player's own is back)
            if (moved > 0)
            {
                Logger?.Info("Stock market: playing alone, " + moved + " company or companies of another player's or the player's own set aside came or went.");
            }
        }

        private void OwnAlone()
        {
            int moved = _engine.Alone();
            Logger?.Info("Stock market: alone again; " + moved + " company or companies came or went (another player's leave this market, the player's own is back).");
            if (moved > 0)
            {
                Queue(NotificationType.Info, "reallife_lst_note_back");
            }
        }

        // ---------------------------------------------------------------- once a second: the day, the hour

        private void OwnSecond()
        {
            GameInstance save = Save;
            if (save.Day != _ownDay)
            {
                _ownDay = save.Day;
                OwnDay(save.Day);
            }
            if (save.Hour != _ownHour)
            {
                _ownHour = save.Hour;
                HandOverNow(save.Day);
            }
            YearlyFees(save.Day);
            ProceedsHeard();
        }

        /// <summary>A business of the player's sold (Economy/Services): while their company is listed its price is the company's, every holder's by their shares.</summary>
        public void BusinessSold(int price)
        {
            Stock own = MyCompany;
            if (own == null || own.Own.Test || price <= 0)
            {
                return;
            }
            _engine.State.Listing.Proceeds += price;
            Logger?.Info("Listing: a business of " + own.Name + " sold for " + price + "; the other holders' part goes into their reserve at the market's next moment.");
            ProceedsHeard();
        }

        // Money from businesses sold, put on the tape for the market (it waits while the market is catching up or held).
        private void ProceedsHeard()
        {
            ListingFile f = _engine.State.Listing;
            if (f.Proceeds < 1000 || _following || Behind || _engine.Held)
            {
                return;
            }
            Stock own = _engine.MineListed();
            if (own == null)
            {
                f.Proceeds = 0;
                return;
            }
            long thousands = Math.Min(int.MaxValue, f.Proceeds / 1000);
            _engine.Event(MarketInputs.EventOwn + (int)OwnAction.Proceeds, own.Id, (int)thousands);
            f.Proceeds -= thousands * 1000;
        }

        // Once a game year while listed: the exchange's fee and the year's audit, whose findings the market hears (an event
        // on the tape, so it waits while the market is catching up or held after a load). A year comparison otherwise.
        private void YearlyFees(int day)
        {
            ListingFile f = _engine.State.Listing;
            int year = MarketCatalogue.YearOf(day, Year);
            if (year <= f.FeeYear || _following || Behind || _engine.Held)
            {
                return;
            }
            Stock own = _engine.MineListed();
            if (own == null || own.Own.Test)
            {
                return;
            }
            ListingFacts facts = Facts();
            ListingBook.Last(f, day, Year, 4, out long turnover, out long _, out int _);
            long exchange = ListingBook.YearlyFee(own.MarketValue, Level), audit = ListingBook.AuditFee(turnover, Level);
            _world.Wallet.Force(exchange, ListingMoney.Exchange, own.Name);
            _world.Wallet.Force(audit, ListingMoney.Audit, own.Name);
            f.Fees += exchange + audit;
            f.FeeYear = year;
            f.Findings = ListingBook.Findings(facts);
            f.AuditDay = day;
            _engine.Event(MarketInputs.EventOwn + (int)OwnAction.Audit, own.Id, f.Findings);   // (the market hears what the auditors found: MarketEngine.Own)
            bool qualified = (f.Findings & ListingBook.Qualifying) != 0;
            Queue(qualified ? NotificationType.Warning : NotificationType.Info, qualified ? "reallife_lst_note_year_qualified" : "reallife_lst_note_year", "amount", MarketText.Money(exchange + audit),
                "found", ListingText.Findings(f.Findings));
            Logger?.Info("Listing: the year's fees for " + own.Name + ": the exchange " + exchange + ", the audit " + audit + "; findings " + f.Findings + ".");
        }

        // A new day: the books, the audit at its end, the year's fees and audit while listed, a hand-over's last day.
        private void OwnDay(int day)
        {
            ListingFile f = _engine.State.Listing;
            if (_following)
            {
                return;   // (the player's own company waits while they are a guest)
            }
            if (f.TestBooks && !f.OnTheWay && _engine.MineListed() == null)
            {
                TestBooksGone(f);   // (the test company is gone: the books are the game's statements again)
            }
            Books(f, day);
            ListingFacts facts = Facts();
            f.FirstDay = facts.FirstDay;
            if (f.Stage == ListingStage.Auditing && day >= f.AuditDone)
            {
                f.Findings = ListingBook.Findings(facts);
                f.AuditDay = day;
                bool clean = (f.Findings & ListingBook.Qualifying) == 0;
                f.Stage = clean ? ListingStage.Audited : ListingStage.Advised;
                Queue(clean ? NotificationType.Success : NotificationType.Warning, clean ? "reallife_lst_note_audit_clean" : "reallife_lst_note_audit_qualified", "found", ListingText.Findings(f.Findings));
                Logger?.Info("Listing: the audit is signed on day " + day + ", findings " + f.Findings + (clean ? " (clean)." : " (qualified: no listing until another clean audit)."));
            }
            if (f.HandOver.Count > 0 && day > f.HandOverBy)
            {
                long owed = f.HandOverCount > 0 ? f.HandOverOwed * f.HandOver.Count / f.HandOverCount : f.HandOverOwed;
                _world.Wallet.Force(owed, ListingMoney.Kept, f.HandOverTo ?? string.Empty);
                Logger?.Warn("Listing: " + f.HandOver.Count + " business(es) could not be handed over to " + f.HandOverTo + " in time; paid for instead: " + owed + ".");
                Queue(NotificationType.Warning, "reallife_lst_note_kept", "count", f.HandOver.Count.ToString(), "buyer", f.HandOverTo ?? string.Empty, "amount", MarketText.Money(owed));
                f.HandOver.Clear();
                f.HandOverTo = null;
            }
        }

        // The statement days since the last counted into the company's quarters (a year back at the most).
        private void Books(ListingFile f, int day)
        {
            Func<int, int, OwnBooks> books = MarketBridges.Books;
            if (books == null)
            {
                return;
            }
            int from = Math.Max(f.BooksDay + 1, day - Year * 3);
            for (int d = from; d < day; d++)
            {
                OwnBooks b = books(d, d + 1);
                if (b.Days > 0)
                {
                    ListingBook.AddDay(f, d, b.Revenue, b.Profit, Year);
                }
                f.BooksDay = d;
            }
        }

        /// <summary>What the advisers and the auditors read of the player now.</summary>
        public ListingFacts Facts()
        {
            Func<ListingFacts> facts = MarketBridges.Facts;
            if (facts != null)
            {
                try
                {
                    return facts();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            return new ListingFacts { Standing = -1 };
        }

        // ---------------------------------------------------------------- the way to the market

        /// <summary>Every requirement for coming to the market as it stands now; true when all are met.</summary>
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

        /// <summary>What the company looks worth to the advisers now, from its books (before any offer).</summary>
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

        // The sector of the first listed company in a trade (as the market answers news of a trade), else business services.
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

        /// <summary>The advisers' retainer now.</summary>
        public long Retainer()
        {
            return ListingBook.Retainer(Worth(), Level);
        }

        /// <summary>The auditors' fee now.</summary>
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

        // Why nothing of the way to the market can be done now (None: it can).
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

        /// <summary>The advisers taken on: every requirement but the audit must be met, and their retainer is paid now.</summary>
        public OwnWhy Engage()
        {
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
            if (!_world.Wallet.Take(fee, ListingMoney.Advisers, f.Name))
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
            if (!_world.Wallet.Take(fee, ListingMoney.Audit, f.Name))
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

        /// <summary>
        /// The prospectus published: the company's name, the share of it offered and the least price the founder takes.
        /// Every requirement must be met (a clean audit among them). The book is open from today and priced at the open
        /// after its last day; the company trades from the market day after.
        /// </summary>
        public OwnWhy Publish(string name, int floatBp, int walkCents)
        {
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
            Logger?.Info("Listing: the prospectus of " + f.Name + " (" + f.Ticker + ") on day " + day + ": " + f.NewShares + " new shares (" + f.FloatBp + " bp) beside the founder's " + f.OldShares
                         + ", a share worth " + f.FairCents + " cents by the books, the range " + f.LowCents + " to " + f.HighCents + ", the least taken " + f.WalkCents + "; the book is priced at the open of day "
                         + f.BookEnd + ".");
            Moved();
            return OwnWhy.None;
        }

        /// <summary>The offer withdrawn before it is priced: the audit stands while it is good, what was paid is not returned.</summary>
        public bool Withdraw()
        {
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

        // ---------------------------------------------------------------- what the founder asks for

        /// <summary>What an action of the founder's would come to now (nothing is changed).</summary>
        public OwnQuote Quote(OwnAction action, long value)
        {
            Stock own = MyCompany;
            if (own == null)
            {
                return new OwnQuote { Why = OwnWhy.NotListed };
            }
            OwnQuote q = _engine.Quote(own, action, value);
            if (q.Why == OwnWhy.None)
            {
                q.Why = Hold(q);
            }
            return q;
        }

        // Why a quote cannot be acted on now beyond the market's own rules: the market behind or held, the player a guest, the cash.
        private OwnWhy Hold(in OwnQuote q)
        {
            if (Behind || _engine.Held)
            {
                return OwnWhy.Held;
            }
            if (_following)
            {
                return OwnWhy.Suspended;
            }
            if (q.Total > 0 && q.Total > _world.Wallet.Cash)
            {
                return OwnWhy.NoMoney;
            }
            return OwnWhy.None;
        }

        /// <summary>
        /// An action of the founder's asked for: checked as the market will check it at its moment, and the cash for it,
        /// then put on the tape for the next moment of the market (every machine of a session plays it then).
        /// </summary>
        public OwnWhy Ask(OwnAction action, long value)
        {
            if (action == OwnAction.Audit || value > int.MaxValue || value < int.MinValue)
            {
                return OwnWhy.TooLarge;
            }
            OwnQuote q = Quote(action, value);
            if (q.Why != OwnWhy.None)
            {
                return q.Why;
            }
            Stock own = MyCompany;
            _engine.Event(MarketInputs.EventOwn + (int)action, own.Id, (int)value);
            if (Verbose)
            {
                Logger?.Info("Listing check (asked): " + action + " " + value + " for " + own.Name + " at " + _engine.State.Day + "/" + _engine.State.Phase + ", quoted " + q.Total + " (" + q.Cents
                             + " cents a share): put on the tape for the next moment: PASS");
            }
            Moved();
            return OwnWhy.None;
        }

        // ---------------------------------------------------------------- a takeover: the businesses pass to the buyer

        private void HandOverStart(Stock own)
        {
            ListingFile f = _engine.State.Listing;
            if (own.Own.Test)
            {
                return;   // (a test's company hands nothing over)
            }
            f.HandOver.Clear();
            _places.Clear();
            MarketBridges.Businesses?.Invoke(_places);
            f.HandOver.AddRange(_places);
            f.HandOverCount = f.HandOver.Count;
            Stock bidder = own.BidderId > 0 ? _engine.State.Stock(own.BidderId) : null;
            f.HandOverTo = bidder != null ? bidder.Name : ListingText.PrivateBuyer();
            f.HandOverBy = Save.Day + ListingCatalogue.HandOverDays;
            f.HandOverOwed = MarketTrading.Value(own.Shares, Math.Max(1, own.GoneCents), true);
            Logger?.Warn("Listing: " + own.Name + " was taken over at " + own.GoneCents + " cents a share by " + f.HandOverTo + "; its " + f.HandOverCount + " business(es) pass to the buyer by day " + f.HandOverBy + ".");
            HandOverNow(Save.Day);
        }

        // As many of the businesses as the game lets go now (not while the player stands in one, keeps a vehicle at one,
        // or is in a Going Public session, as for any sale).
        private void HandOverNow(int day)
        {
            ListingFile f = _engine != null ? _engine.State.Listing : null;
            Func<string, string, bool> hand = MarketBridges.HandOver;
            if (f == null || f.HandOver.Count == 0 || hand == null || GoingPublic.InSession)
            {
                return;
            }
            int done = 0;
            for (int i = f.HandOver.Count - 1; i >= 0; i--)
            {
                bool gone;
                try
                {
                    gone = hand(f.HandOver[i], f.HandOverTo ?? string.Empty);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    gone = false;
                }
                if (gone)
                {
                    f.HandOver.RemoveAt(i);
                    done++;
                }
            }
            if (done > 0)
            {
                Logger?.Info("Listing: " + done + " business(es) handed over to " + f.HandOverTo + "; " + f.HandOver.Count + " still to go.");
                if (f.HandOver.Count == 0)
                {
                    Queue(NotificationType.Warning, "reallife_lst_note_handed", "buyer", f.HandOverTo ?? string.Empty);
                    f.HandOverTo = null;
                }
                Moved();
            }
        }

        // ---------------------------------------------------------------- the news, told to the founder

        // News of the player's own company or of their way to it: the founder's notifications (true: told here).
        private bool OwnHeard(in MarketNews news)
        {
            MarketState s = _engine.State;
            if (news.Kind == MarketNewsKind.OwnPriced || news.Kind == MarketNewsKind.OwnPulled)
            {
                if (news.Kind == MarketNewsKind.OwnPriced)
                {
                    Queue(NotificationType.Success, "reallife_lst_note_priced", "price", MarketText.Price(news.A), "day", news.B.ToString());
                }
                else
                {
                    Queue(NotificationType.Warning, "reallife_lst_note_pulled", "price", MarketText.Price(news.A), "least", MarketText.Price(news.B));
                }
                return true;
            }
            Stock k = news.Stock > 0 ? s.Stock(news.Stock) : null;
            if (k == null || k.Own == null || !_engine.Mine(k))
            {
                if (k != null && k.Own != null && news.Kind == MarketNewsKind.OwnAway && s.Held(k.Id) != 0)
                {
                    Queue(NotificationType.Info, "reallife_lst_note_away", "name", k.Name, "price", MarketText.Price(news.A));
                }
                return k != null && k.Own != null && news.Kind >= MarketNewsKind.OwnPriced;   // (another player's company's founder news is theirs)
            }
            OwnListing o = k.Own;
            switch (news.Kind)
            {
                case MarketNewsKind.Listing:
                    Queue(NotificationType.Success, "reallife_lst_note_listed", "name", k.Name, "price", MarketText.Price(news.A), "amount", MarketText.Money(s.Listing.Raised));
                    break;
                case MarketNewsKind.Results:
                    Queue(NotificationType.Info, news.A >= news.B ? "reallife_lst_note_results_beat" : "reallife_lst_note_results_miss", "eps", MarketText.PerShare(news.A), "consensus", MarketText.PerShare(news.B));
                    break;
                case MarketNewsKind.Dividend:
                    Queue(NotificationType.Info, "reallife_lst_note_dividend", "amount", MarketText.Price(news.A), "day", news.C.ToString());
                    break;
                case MarketNewsKind.Bid:
                    Queue(NotificationType.Warning, "reallife_lst_note_bid", "price", MarketText.Price(news.A), "day", news.B.ToString(), "bidder", news.C > 0 && s.Stock(news.C) != null ? s.Stock(news.C).Name : ListingText.PrivateBuyer(),
                        "threshold", MarketText.Percent(ListingCatalogue.TakeoverThresholdBp), "stake", MarketText.Percent(o.StakeBp(k)));
                    break;
                case MarketNewsKind.BidDone:
                    Queue(NotificationType.Error, "reallife_lst_note_taken", "price", MarketText.Price(news.A), "amount", MarketText.Money(MarketTrading.Value(o.Founder, news.A, false)));
                    HandOverStart(k);
                    break;
                case MarketNewsKind.OwnBidBlocked:
                    Queue(NotificationType.Success, news.A == 1 ? "reallife_lst_note_bid_blocked" : "reallife_lst_note_bid_refused");
                    break;
                case MarketNewsKind.OwnDeal:
                    Queue(NotificationType.Success, news.A > 0 ? "reallife_lst_note_bought" : "reallife_lst_note_sold", "count", Count(Math.Abs((long)news.A)), "price", MarketText.Price(news.B));
                    break;
                case MarketNewsKind.OwnBuyBack:
                    Queue(NotificationType.Success, "reallife_lst_note_buyback", "count", Count(news.A), "price", MarketText.Price(news.B));
                    break;
                case MarketNewsKind.OwnIssue:
                    Queue(NotificationType.Success, "reallife_lst_note_issue", "count", Count(news.A), "price", MarketText.Price(news.B));
                    break;
                case MarketNewsKind.OwnMeeting:
                    Queue((news.A & (ListingVotes.PayLost | ListingVotes.PayoutVoted | ListingVotes.BoardLost | ListingVotes.DemandPassed)) != 0 ? NotificationType.Warning : NotificationType.Success,
                        "reallife_lst_note_meeting", "result", ListingText.Meeting(news.A), "against", MarketText.Percent(news.B));
                    break;
                case MarketNewsKind.OwnActivist:
                    Queue(NotificationType.Warning, "reallife_lst_note_activist", "stake", MarketText.Percent(news.A), "demand", ListingText.Demand((ActivistDemand)news.B), "day", news.C.ToString());
                    break;
                case MarketNewsKind.OwnWithdrawn:
                    Queue(NotificationType.Success, "reallife_lst_note_withdrawn", "demand", ListingText.Demand((ActivistDemand)news.A));
                    break;
                case MarketNewsKind.OwnPrivate:
                    Queue(NotificationType.Success, "reallife_lst_note_private", "price", MarketText.Price(news.A));
                    break;
                case MarketNewsKind.OwnDisgorged:
                    Queue(NotificationType.Warning, "reallife_lst_note_disgorged", "amount", MarketText.Money(news.A), "days", Math.Max(1, Year / ListingCatalogue.ShortSwingYearShare).ToString());
                    break;
                case MarketNewsKind.OwnRefused:
                    Queue(NotificationType.Error, "reallife_lst_note_refused", "why", ListingText.Why((OwnWhy)news.B));
                    break;
                case MarketNewsKind.OwnAudit:
                    Queue(NotificationType.Warning, "reallife_lst_note_audit_market", "found", ListingText.Findings(news.A));
                    break;
                case MarketNewsKind.Split:
                    Queue(NotificationType.Info, "reallife_mkt_note_split", "name", k.Name, "ratio", news.A.ToString(), "day", news.B.ToString());
                    break;
            }
            return true;
        }

        private static string Count(long n)
        {
            return n.ToString("N0");
        }

        // ---------------------------------------------------------------- tax on dividends

        // A dividend the player was paid: the tax withheld from it, at the rate the council's business tax sets.
        private void DividendTax(Stock stock, long amount)
        {
            MarketState s = _engine != null ? _engine.State : null;
            if (s == null || amount <= 0)
            {
                return;
            }
            long tax = ListingVotes.DividendTax(amount, s.Env.Lever(MarketInputs.BusinessTaxLever));
            if (tax <= 0)
            {
                return;
            }
            if (s.Account.Margin)
            {
                s.Account.Cash -= tax;   // (paid into the account, withheld from it)
            }
            else
            {
                _world.Wallet.Force(tax, ListingMoney.DividendTax, stock != null ? stock.Name : string.Empty);
            }
        }

        // ---------------------------------------------------------------- the save tools

        // Before the mod is removed: the player's own listed company is taken private at its last price (shares do not exist
        // in the plain game), the other holders paid less the reserve kept for them. Returns the money (under nothing: paid).
        private long OwnSettled(System.Text.StringBuilder log)
        {
            Stock own = _engine.MineListed();
            if (own == null)
            {
                return 0;
            }
            long others = own.Own.Others(own);
            long cost = own.Own.Test ? 0 : Math.Max(0L, MarketTrading.Value(others, own.Last, true) - (long)Math.Floor(Math.Max(0.0, own.Own.Rps) * others));
            _engine.Delist(own.Id, GoneReason.Delisted, own.Last);
            _engine.State.Listing.Stage = ListingStage.None;
            log.Append(" the player's company ").Append(own.Name).Append(" taken private for ").Append(cost).Append(';');
            return -cost;
        }
    }
}
