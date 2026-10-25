using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the market director: the player's own company on the market (the rules are ListingRules, ListingBook,
    /// ListingVotes and MarketEngine.Own). A comparison of the day and hour once a second; a day's work on a new day.
    /// </summary>
    public sealed partial class MarketDirector
    {
        private int _ownDay = -1, _ownHour = -1;
        private readonly List<string> _places = new List<string>(16);
        private readonly Requirement[] _requirements = new Requirement[ListingBook.NeedCount];

        /// <summary>Who the player of a save is, as the market knows its founders; never nothing.</summary>
        public static ulong OwnerOf(GameInstance save)
        {
            string who = save == null ? string.Empty : (save.characterId ?? string.Empty) + "|" + (save.seed ?? string.Empty);
            ulong h = MarketRandom.Mix64(MarketRandom.Of("player|" + who));
            return h == 0UL ? 1UL : h;
        }

        private static double Level => Prices.Level / 1000.0;

        private int Year => _engine != null ? Math.Max(8, _engine.State.YearDays) : 60;

        public Stock MyCompany => Running ? _engine.MineListed() : null;

        public ListingFile Listing => Running ? _engine.State.Listing : null;

        /// <summary>True while the player's company is set aside: they are a guest in another player's city.</summary>
        public bool CompanyAside => Running && (_following || !string.IsNullOrEmpty(_engine.State.Listing.Suspended));

        /// <summary>For the Company app's button.</summary>
        public void OpenListing()
        {
            OpenCompany(MarketApp.OwnPage);
        }

        /// <summary>For the Company app; null for nothing.</summary>
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
                return MarketText.Fill("reallife_lst_summary_listed", "name", own.Name, "price", MarketText.Price(own.Last), "change", MarketText.Change(MarketHistory.DayMoveBp(own)),
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

        private void OwnStarted(GameInstance save)
        {
            MarketState s = _engine.State;
            s.Me = OwnerOf(save);
            s.Following = false;
            _ownDay = -1;
            _ownHour = -1;
            int moved = _engine.Alone();   // a save made as a guest: the host's company leaves, the player's own is back
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

        /// <summary>A business of the player's sold (Economy/Services): while listed, its price is every holder's by their shares.</summary>
        public void BusinessSold(int price)
        {
            FinishMoment();
            Stock own = MyCompany;
            if (own == null || own.Own.Test || price <= 0)
            {
                return;
            }
            _engine.State.Listing.Proceeds += price;
            Logger?.Info("Listing: a business of " + own.Name + " sold for " + price + "; the other holders' part goes into their reserve at the market's next moment.");
            ProceedsHeard();
        }

        // Money from businesses sold, put on the tape for the market; it waits while the market is behind or held.
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

        // Once a game year while listed: the exchange's fee and the year's audit, whose findings the market hears as a tape event.
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
            _engine.Wallet.Cover(exchange + audit);   // one give-back from worked orders' money for both
            _engine.Wallet.Force(exchange, ListingMoney.Exchange, own.Name);
            _engine.Wallet.Force(audit, ListingMoney.Audit, own.Name);
            f.Fees += exchange + audit;
            f.FeeYear = year;
            f.Findings = ListingBook.Findings(facts);
            f.AuditDay = day;
            _engine.Event(MarketInputs.EventOwn + (int)OwnAction.Audit, own.Id, f.Findings);   // the market hears what the auditors found
            bool qualified = (f.Findings & ListingBook.Qualifying) != 0;
            Queue(qualified ? NotificationType.Warning : NotificationType.Info, qualified ? "reallife_lst_note_year_qualified" : "reallife_lst_note_year", "amount", MarketText.Money(exchange + audit),
                "found", ListingText.Findings(f.Findings));
            Logger?.Info("Listing: the year's fees for " + own.Name + ": the exchange " + exchange + ", the audit " + audit + "; findings " + f.Findings + ".");
        }

        private void OwnDay(int day)
        {
            ListingFile f = _engine.State.Listing;
            if (_following)
            {
                return;   // the player's own company waits while they are a guest
            }
            if (f.TestBooks && !f.OnTheWay && _engine.MineListed() == null)
            {
                TestBooksGone(f);   // the test company is gone: the books are the game's statements again
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
                Queue(clean ? NotificationType.Success : NotificationType.Warning, clean ? "reallife_lst_note_audit_clean" : "reallife_lst_note_audit_qualified", "found",
                    ListingText.Findings(f.Findings));
                Logger?.Info("Listing: the audit is signed on day " + day + ", findings " + f.Findings + (clean ? " (clean)." : " (qualified: no listing until another clean audit)."));
            }
            bool session = GoingPublic.InSession;   // HandOverNow does nothing in one, so its days do not count against the buyer's time
            if (f.HandOver.Count > 0)
            {
                f.HandOverBy = ListingBook.HandOverDeadline(f.HandOverBy, session);
            }
            if (ListingBook.HandOverLate(f.HandOver.Count, day, f.HandOverBy, session))
            {
                long owed = f.HandOverCount > 0 ? f.HandOverOwed * f.HandOver.Count / f.HandOverCount : f.HandOverOwed;
                _engine.Wallet.Force(owed, ListingMoney.Kept, f.HandOverTo ?? string.Empty);
                Logger?.Warn("Listing: " + f.HandOver.Count + " business(es) could not be handed over to " + f.HandOverTo + " in time; paid for instead: " + owed + ".");
                Queue(NotificationType.Warning, "reallife_lst_note_kept", "count", f.HandOver.Count.ToString(), "buyer", f.HandOverTo ?? string.Empty, "amount", MarketText.Money(owed));
                f.HandOver.Clear();
                f.HandOverTo = null;
            }
        }

        // The statement days since the last into the company's quarters. The day just ended waits a day: the mod's own costs
        // of it go into its statement after the game's nightly run.
        private void Books(ListingFile f, int day)
        {
            Func<int, int, OwnBooks> books = MarketBridges.Books;
            if (books == null)
            {
                return;
            }
            int from = Math.Max(f.BooksDay + 1, day - Year * 3);
            for (int d = from; d < day - 1; d++)
            {
                OwnBooks b = books(d, d + 1);
                if (b.Days > 0)
                {
                    ListingBook.AddDay(f, d, b.Revenue, b.Profit, Year);
                }
                f.BooksDay = d;
            }
        }

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

        // Why a quote cannot be acted on now beyond the market's rules: behind or held, a guest, or the cash.
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
            if (q.Total > 0 && q.Total > _engine.Wallet.Cash)
            {
                return OwnWhy.NoMoney;
            }
            return OwnWhy.None;
        }

        /// <summary>A founder's action, checked as the market will at its moment, and the cash for it, put on the tape for the next moment.</summary>
        public OwnWhy Ask(OwnAction action, long value)
        {
            FinishMoment();
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

        private void HandOverStart(Stock own)
        {
            ListingFile f = _engine.State.Listing;
            if (own.Own.Test)
            {
                return;   // a test's company hands nothing over
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
            Logger?.Warn("Listing: " + own.Name + " was taken over at " + own.GoneCents + " cents a share by " + f.HandOverTo + "; its " + f.HandOverCount
                         + " business(es) pass to the buyer by day " + f.HandOverBy + ".");
            HandOverNow(Save.Day);
        }

        // As many of the businesses as the game lets go now; not in a Going Public session.
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

        // News of the player's own company or way to it, told to the founder; true when told here.
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
                return k != null && k.Own != null && news.Kind >= MarketNewsKind.OwnPriced;   // another player's company's founder news is theirs
            }
            OwnListing o = k.Own;
            switch (news.Kind)
            {
                case MarketNewsKind.Listing:
                    Queue(NotificationType.Success, "reallife_lst_note_listed", "name", k.Name, "price", MarketText.Price(news.A), "amount", MarketText.Money(s.Listing.Raised));
                    break;
                case MarketNewsKind.Results:
                    Queue(NotificationType.Info, news.A >= news.B ? "reallife_lst_note_results_beat" : "reallife_lst_note_results_miss", "eps", MarketText.PerShare(news.A),
                        "consensus", MarketText.PerShare(news.B));
                    break;
                case MarketNewsKind.Dividend:
                    Queue(NotificationType.Info, "reallife_lst_note_dividend", "amount", MarketText.Price(news.A), "day", news.C.ToString());
                    break;
                case MarketNewsKind.Bid:
                    Queue(NotificationType.Warning, "reallife_lst_note_bid", "price", MarketText.Price(news.A), "day", news.B.ToString(),
                        "bidder", news.C > 0 && s.Stock(news.C) != null ? s.Stock(news.C).Name : ListingText.PrivateBuyer(),
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

        // A dividend paid to the player, taxed by the council's business tax; with 'cash' it went into the cash whatever the
        // account (a bot's). Returns what was withheld.
        private long DividendTax(Stock stock, long amount, bool cash = false)
        {
            MarketState s = _engine != null ? _engine.State : null;
            if (s == null || amount <= 0)
            {
                return 0;
            }
            long tax = ListingVotes.DividendTax(amount, s.Env.Lever(MarketInputs.BusinessTaxLever));
            if (tax <= 0)
            {
                return 0;
            }
            if (s.Account.Margin && !cash)
            {
                s.Account.Cash -= tax;   // paid into the account, withheld from it
            }
            else
            {
                _engine.Wallet.Force(tax, ListingMoney.DividendTax, stock != null ? stock.Name : string.Empty);
            }
            return tax;
        }

        // Before the mod is removed: the player's listed company taken private at its last price, the others paid less their
        // reserve. Returns the money, negative when paid.
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
