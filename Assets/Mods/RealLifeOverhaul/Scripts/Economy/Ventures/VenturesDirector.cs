using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>
    /// Investors: investors who put money into the player's businesses for a share of the profit, a pitch that wins or
    /// loses that money, and a page on what the game's own lettings bring in. See docs/features/economy-investors.md.
    /// The stock market that used to be here is the city's own now (Scripts/Markets): the shares a save held in it go
    /// over to that market once, and this file keeps none.
    ///
    /// This class keeps it going and nothing more. It has no Update. A one-second timer on the real clock waits for
    /// the save, puts the app on the phone and keeps the three event handlers in place (the game empties its events
    /// on every load). The work itself is done on the game's own events: the day (each deal's share of yesterday's
    /// profit, targets, offers that lapse, the weekly look at the businesses), the hour (only while money is promised
    /// for a business not yet open: has it been opened), the save, and when the app is opened or something in it is
    /// clicked.
    ///
    /// The rules are game-free classes in this folder (Cast, OfferRules, DealRules, Pitch); this class reads the
    /// game's figures for them (VenturesGame), moves the money they name (VenturesWallet) and says what happened.
    /// Nothing is ever written into the game's own business or property records.
    ///
    /// A fault anywhere switches Ventures off for the session with one line and leaves the game, its daily run and
    /// the rest of the economy alone (<see cref="Fault"/>).
    /// </summary>
    public sealed partial class VenturesDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeVentures";
        private static readonly string[] AppGlyphs = { "Icon-Building", "Icon-Shop" };

        public static IModLogger Logger;
        public static VenturesDirector Instance { get; private set; }

        private readonly VenturesWallet _wallet = new VenturesWallet();
        private readonly List<BuildingRegistration> _businesses = new List<BuildingRegistration>(16);
        private readonly List<DealEvent> _said = new List<DealEvent>(8);
        private readonly List<string> _names = new List<string>(VenturesCatalogue.RivalsListed);
        private readonly StringBuilder _log = new StringBuilder(600);

        private VenturesFile _file;
        private VenturesApp _view;
        private PhoneApp _app;
        private PitchMeeting _meeting;
        private bool _running;         // the file is open
        private bool _broken;          // a fault: off for the session
        private bool _gaveUp;          // the game's phone is not built as expected: the app is left off, the rest runs
        private Action _onSave, _onHour, _onDay, _hourSeen, _daySeen, _openInvestors;
        private Func<int, int> _fileClaim;

        public bool Running => _running && !_broken;

        /// <summary>The save's file; null while Ventures is not running.</summary>
        public VenturesFile File => _running ? _file : null;

        public IWallet Wallet => _wallet;

        /// <summary>How demanding investors are: the option the presets set.</summary>
        public static int Level => VenturesCatalogue.Level(Settings.VenturesLevel);

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            _onHour = Guard.Wrap(OnHour);
            _onDay = Guard.Wrap(OnDay);
            _openInvestors = () => Open(VenturesPhone.Investors);
            _fileClaim = amount => EconomyBridges.FileClaim != null ? EconomyBridges.FileClaim(amount) : 0;
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GlobalEvents.onNewHour -= _onHour;
            GlobalEvents.onNewDay -= _onDay;
            // Nothing is written to the save here: by now the game's current save may be another one, and what a save
            // keeps of the file is written when the game saves (OnSave).
            try
            {
                StandDown(false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // The first look is a second after the city's objects are made, never in the frame they are made in.
        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                if (_broken)
                {
                    continue;
                }
                while (StartLine.Holds(StartSeats.Ventures))
                {
                    yield return null;   // its first load after a save loads waits for a frame of its own (Core/StartLine)
                }
                try
                {
                    Slow();
                }
                catch (Exception e)
                {
                    Fault("the timer", e);
                }
            }
        }

        private static readonly int HotTick = HotSpots.Slot("Economy: VenturesDirector.Tick");
        // Once a second: start when the save is ready, stand down when the option goes off, hold the handlers, the app on the phone.
        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);   // the timer's tick (Core/HotSpots)
            if (Save == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (!Settings.VenturesInvestors)
            {
                if (_running)
                {
                    StandDown(true);
                }
                return;
            }
            if (!_running)
            {
                StartUp();
            }
            SaveHook.Keep(_onSave);
            EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen);
            EventHold.Keep(ref GlobalEvents.onNewDay, _onDay, ref _daySeen);
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            if (_meeting != null)
            {
                _meeting.Watch();   // a meeting whose window has gone with the game's interface is ended
            }
        }

        // ---------------------------------------------------------------- starting and stopping

        private void StartUp()
        {
            int today = Save.Day, version;
            VenturesFile file = VenturesStore.Load(Save.modData, out version);
            bool fresh = file == null;
            if (fresh)
            {
                file = new VenturesFile { StartDay = today, OfferDay = today, SettledDay = today, MarketDay = today };
                unchecked
                {
                    Cast.Make(file, (int)VenturesRandom.Hash(Environment.TickCount, today * 7919 + (int)Save.Money, (int)DateTime.UtcNow.Ticks), Rivals());
                }
            }
            else if (file.Investors.Count == 0 || file.Companies.Count == 0)
            {
                // A line that lost a part: the same seed gives the same cast again, and only the part that is missing is taken from it.
                var again = new VenturesFile();
                Cast.Make(again, file.Seed, Rivals());
                if (file.Investors.Count == 0)
                {
                    file.Investors.AddRange(again.Investors);
                }
                if (file.Companies.Count == 0)
                {
                    file.Companies.AddRange(again.Companies);
                }
            }
            _file = file;
            _running = true;
            if (fresh)
            {
                Logger?.Info("Ventures: a file is opened for this save on day " + today + " (seed " + file.Seed + "): " + file.Investors.Count + " investors, " + file.Companies.Count + " listed companies.");
            }
            if (Verbose)
            {
                Logger?.Info("Ventures check (start): file version " + (fresh ? "new" : version.ToString()) + ", " + file.Deals.Count + " deal(s), " + file.Offers.Count + " offer(s), "
                             + file.Pledges.Count + " promise(s), " + file.Holdings.Count + " holding(s), market day " + file.MarketSteps + ", name with investors " + file.Reputation
                             + ", level " + Level + ", climate " + VenturesBridges.Climate.Phase + " at " + VenturesBridges.Climate.BankRate.ToString("0.##") + "%. " + CastLine() + ": PASS");
            }
            Badge();
        }

        // The game's named rivals as the market lists them: each under a company name made from its own.
        private List<string> Rivals()
        {
            VenturesGame.RivalNames(_names);
            for (int i = 0; i < _names.Count; i++)
            {
                _names[i] = FinanceText.T("reallife_ven_company_rival", "name", _names[i]);
            }
            return _names;
        }

        private string CastLine()
        {
            _log.Length = 0;
            _log.Append("Cast: ");
            for (int i = 0; i < _file.Investors.Count; i++)
            {
                Investor v = _file.Investors[i];
                _log.Append(i > 0 ? "; " : string.Empty).Append(v.Name).Append(" (").Append(v.Kind).Append(", ").Append(v.Temper).Append(", ").Append(v.ChequeLeast).Append(" to ")
                    .Append(v.ChequeMost).Append(", diligence ").Append(v.Diligence).Append(", likes ").Append(v.Likes).Append(')');
            }
            _log.Append(". Companies: ");
            for (int i = 0; i < _file.Companies.Count; i++)
            {
                Company c = _file.Companies[i];
                _log.Append(i > 0 ? "; " : string.Empty).Append(c.Name).Append(" (").Append(c.Sector).Append(c.Rival ? ", a rival of the game's" : string.Empty).Append(", ")
                    .Append((c.PriceCents / 100f).ToString("0.00")).Append(", move ").Append(c.VolBp).Append(" bp, dividend ").Append(c.YieldBp).Append(" bp)");
            }
            return _log.ToString();
        }

        // Ventures stops: the meeting ends, the app leaves the phone. With 'keep' the file goes into the save's data
        // first, so that switching an option on again (or the next save) has it; that is only for a stop in the middle
        // of play, when the game's current save is certainly the one the file belongs to.
        private void StandDown(bool keep)
        {
            if (keep && _running && Save != null && _file != null)
            {
                VenturesStore.Save(Save.modData, _file);
            }
            _running = false;
            _file = null;
            if (_meeting != null)
            {
                _meeting.Dispose();
                _meeting = null;
            }
            if (_app != null)
            {
                _app.Remove();
                _app = null;
                _view = null;
            }
            _gaveUp = false;
        }

        /// <summary>A fault: one line, and nothing more of Ventures this session. What the save holds of it is left as it is.</summary>
        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            _wallet.BookDay = null;
            try
            {
                StandDown(false);
            }
            catch (Exception second)
            {
                Logger?.Error(second);
            }
            Logger?.Warn("Ventures: switched off for this session after an error in " + where + ". Investors, deals and the market stand still; what the save holds of them is untouched, and the game's daily run and the rest of the economy are not affected.");
        }

        private void Install()
        {
            var view = new VenturesApp(this);
            GameSprites.Want(AppGlyphs);
            bool tryAgain;
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_ven_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(120, 148, 235, 255), new Color32(62, 78, 176, 255), 0.42f, "AppIcon-Ventures"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Ventures: the game's phone is not built as this version expects, so the Ventures app is left off. Deals that exist still pay their share and the market still moves.");
                }
                return;
            }
            _view = view;
            Badge();
        }

        private static Sprite Glyph()
        {
            for (int i = 0; i < AppGlyphs.Length; i++)
            {
                Sprite sprite = GameSprites.Get(AppGlyphs[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- the hour and the day

        private static readonly int HotOnHour = HotSpots.Slot("Economy: VenturesDirector.OnHour");
        // GlobalEvents.onNewHour. Only while money is promised for a business not yet open: has one been opened.
        private void OnHour()
        {
            using var hotSpot = HotSpots.Time(HotOnHour);
            if (!_running || _broken || Save == null || _file.Pledges.Count == 0)
            {
                return;
            }
            try
            {
                CheckPledges(Save.Day);
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        private static readonly int HotOnDay = HotSpots.Slot("Economy: VenturesDirector.OnDay");
        // GlobalEvents.onNewDay: after the game's daily run, so yesterday's statement of every business is there.
        private void OnDay()
        {
            using var hotSpot = HotSpots.Time(HotOnDay);
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                int day = Save.Day;
                if (day == _file.SettledDay)
                {
                    return;
                }
                _wallet.BookDay = day - 1;   // the game books its daily run on the day that has ended
                InvestorsDay(day);
                _file.SettledDay = day;
                _wallet.BookDay = null;
                Badge();
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        // Deals carry on whether or not the option is on (what was agreed stays agreed); new offers come only while it is.
        private void InvestorsDay(int day)
        {
            for (int i = _file.Offers.Count - 1; i >= 0; i--)
            {
                Offer offer = _file.Offers[i];
                if (day > offer.LapseDay || (!offer.ForNew && VenturesGame.Business(offer.Place) == null))
                {
                    _file.Offers.RemoveAt(i);
                    Tell(NotificationType.Info, "reallife_ven_note_offer_lapsed", _openInvestors, "name", NameOf(offer.InvestorId));
                    Detail("the offer " + offer.Id + " from " + NameOf(offer.InvestorId) + " lapsed on day " + day);
                }
            }
            for (int i = _file.Pledges.Count - 1; i >= 0; i--)
            {
                Pledge pledge = _file.Pledges[i];
                if (day > pledge.OpenBy)
                {
                    _file.Pledges.RemoveAt(i);
                    Tell(NotificationType.Info, "reallife_ven_note_pledge_lapsed", _openInvestors, "name", NameOf(pledge.InvestorId), "type", VenturesGame.TypeName(pledge.TypeName));
                    Detail("the promise " + pledge.Id + " from " + NameOf(pledge.InvestorId) + " lapsed on day " + day + ": nothing is owed");
                }
            }
            CheckPledges(day);
            int level = Level;
            for (int i = _file.Deals.Count - 1; i >= 0; i--)
            {
                Deal deal = _file.Deals[i];
                BusinessFigures b = VenturesGame.Figures(VenturesGame.Business(deal.Place));
                _said.Clear();
                DealRules.RunDay(_file, deal, b, day, level, _wallet, _said, _fileClaim);
                for (int e = 0; e < _said.Count; e++)
                {
                    Said(_said[e], deal, b, day);
                }
            }
            if (Settings.VenturesInvestors && day - _file.OfferDay >= VenturesCatalogue.OfferEveryDays)
            {
                _file.OfferDay = day;
                Look(day, level);
            }
        }

        // One thing a deal's day brought: a line for the log, and for what the player should know, a notification.
        private void Said(DealEvent e, Deal deal, in BusinessFigures b, int day)
        {
            string name = NameOf(e.InvestorId), business = e.Business, amount = FinanceText.Money(e.Amount);
            if (Verbose)
            {
                Logger?.Info("Ventures check (day " + day + "): deal " + e.DealId + " with " + name + " on " + business + ": " + e.What + " " + e.Amount + " (yesterday's profit "
                             + b.ProfitYesterday.ToString("0") + ", share " + deal.ShareNow(day - 1) + " bp, recent profit " + b.AvgProfit7.ToString("0") + ", target " + deal.TargetProfit
                             + " by day " + deal.TargetDay + ", misses " + deal.Misses + ", mood " + deal.Mood + ", paid out " + deal.PaidOut + ").");
            }
            switch (e.What)
            {
                case DealNews.TargetMet:
                    Tell(NotificationType.Success, "reallife_ven_note_target_met", _openInvestors, "name", name, "business", business, "amount", amount);
                    break;
                case DealNews.Warned:
                    Tell(NotificationType.Warning, "reallife_ven_note_warned", _openInvestors, "name", name, "business", business, "amount", amount, "day", deal.TargetDay.ToString());
                    break;
                case DealNews.Demanded:
                    _file.Unseen++;
                    Tell(NotificationType.Warning, "reallife_ven_note_demand", _openInvestors, "name", name, "business", business, "day", deal.DemandDay.ToString());
                    break;
                case DealNews.DemandDefault:
                    Tell(NotificationType.Warning, deal.BoardUntil > 0 ? "reallife_ven_note_demand_seat" : "reallife_ven_note_demand_costs", _openInvestors, "name", name, "business", business,
                        "share", VenturesText.Share(deal.ShareNow(day)), "day", (deal.BoardUntil > 0 ? deal.BoardUntil : deal.CostDay).ToString());
                    break;
                case DealNews.PromiseKept:
                    Tell(NotificationType.Success, "reallife_ven_note_promise_kept", _openInvestors, "name", name, "business", business);
                    break;
                case DealNews.PromiseBroken:
                    Tell(NotificationType.Warning, "reallife_ven_note_promise_broken", _openInvestors, "name", name, "business", business, "amount", amount);
                    break;
                case DealNews.WentCold:
                    Tell(e.Amount > 0 ? NotificationType.Warning : NotificationType.Info, e.Amount > 0 ? "reallife_ven_note_cold_unpaid" : "reallife_ven_note_cold", _openInvestors,
                        "name", name, "business", business);
                    break;
                case DealNews.PaidBack:
                    Tell(NotificationType.Success, "reallife_ven_note_paid_back", _openInvestors, "name", name, "business", business);
                    break;
                case DealNews.Called:
                    _file.Unseen++;
                    Tell(NotificationType.Error, "reallife_ven_note_called", _openInvestors, "name", name, "business", business, "amount", amount, "per_day", FinanceText.Money(deal.CallPerDay));
                    break;
                case DealNews.BusinessGone:
                    Tell(NotificationType.Warning, "reallife_ven_note_gone", _openInvestors, "name", name, "business", business, "amount", amount, "per_day", FinanceText.Money(deal.CallPerDay));
                    break;
                case DealNews.Settled:
                    Tell(NotificationType.Info, "reallife_ven_note_settled", _openInvestors, "name", name, "business", business);
                    break;
                case DealNews.CallMissed:
                    Tell(NotificationType.Error, "reallife_ven_note_call_missed", _openInvestors, "name", name, "amount", amount, "count", deal.CallMisses.ToString(),
                        "most", VenturesCatalogue.CallMissesMost.ToString());
                    break;
                case DealNews.CallDone:
                    Tell(NotificationType.Success, "reallife_ven_note_call_done", _openInvestors, "name", name);
                    break;
                case DealNews.Claimed:
                    Tell(NotificationType.Error, "reallife_ven_note_claimed", _openInvestors, "name", name, "amount", amount);
                    break;
                case DealNews.Short:
                    Tell(NotificationType.Warning, "reallife_ven_note_short", _openInvestors, "name", name, "amount", amount);
                    break;
                case DealNews.BoardEnded:
                    Tell(NotificationType.Info, "reallife_ven_note_seat_ended", _openInvestors, "name", name, "business", business, "share", VenturesText.Share(deal.ShareBp));
                    break;
            }
        }

        // The weekly look: a second round where a target was met, and for a business that does well, perhaps a tycoon.
        private void Look(int day, int level)
        {
            Books.Businesses(_businesses);
            int phase = VenturesBridges.Climate.Phase;
            for (int i = 0; i < _businesses.Count; i++)
            {
                BuildingRegistration r = _businesses[i];
                string place = Books.Key(r.Address);
                BusinessFigures b = VenturesGame.Figures(r);
                Deal due = null;
                for (int d = 0; d < _file.Deals.Count; d++)
                {
                    due = _file.Deals[d].Place == place && _file.Deals[d].FollowOn ? _file.Deals[d] : due;
                }
                if (due != null)
                {
                    due.FollowOn = false;
                    Investor again = _file.Investor(due.InvestorId);
                    Offer second = again != null && day >= again.ClosedUntil ? OfferRules.Make(_file, again, place, VenturesGame.Name(r), b, day, level, OfferSource.FollowOn) : null;
                    OfferMade(second, again, b, 100, 0, day);
                    continue;
                }
                if (!OfferRules.Qualifies(b) || _file.HasOffer(place))
                {
                    if (Verbose)
                    {
                        Logger?.Info("Ventures check (the weekly look, day " + day + "): " + VenturesGame.Name(r) + " made a profit on " + b.ProfitableDays + " of its last 14 days and "
                                     + b.AvgProfit7.ToString("0") + " a day lately (needs " + VenturesCatalogue.ProfitDaysNeeded + " and " + VenturesCatalogue.OfferProfitFloor + "): no offer.");
                    }
                    continue;
                }
                // Who is free to come: a tycoon who sees people, has no offer out and no deal here; one who likes the sector first.
                Investor who = null;
                for (int v = 0; v < _file.Investors.Count; v++)
                {
                    Investor investor = _file.Investors[v];
                    if (investor.Kind == InvestorKind.Tycoon && day >= investor.ClosedUntil && !_file.HasOfferFrom(investor.Id) && !_file.HasDeal(place, investor.Id)
                        && (who == null || (investor.LikesSector(b.Sector) && !who.LikesSector(b.Sector))))
                    {
                        who = investor;
                    }
                }
                if (who == null)
                {
                    continue;
                }
                int chance = OfferRules.Chance(who.LikesSector(b.Sector), phase);
                int roll = VenturesRandom.Percent(_file.Seed, day, VenturesRandom.Of(place));
                OfferMade(roll < chance ? OfferRules.Make(_file, who, place, VenturesGame.Name(r), b, day, level, OfferSource.Inbound) : null, who, b, chance, roll, day);
            }
            _businesses.Clear();
        }

        private void OfferMade(Offer offer, Investor investor, in BusinessFigures b, int chance, int roll, int day)
        {
            if (Verbose && investor != null)
            {
                Logger?.Info("Ventures check (offer, day " + day + "): " + investor.Name + " (" + investor.Kind + ", " + investor.Temper + "), a business with " + b.ProfitableDays
                             + " profitable days of 14 and " + b.AvgProfit7.ToString("0") + " a day lately, chance " + chance + "%, roll " + roll + ": "
                             + (offer != null ? offer.Amount + " for " + offer.ShareBp + " bp (" + (offer.Source == OfferSource.FollowOn ? VenturesCatalogue.FollowOnValueDays : VenturesCatalogue.OfferValueDays)
                                                + " days of profit), growth " + offer.TargetPct + "% in " + offer.TargetDays + " days, answer by day " + offer.LapseDay
                                 : "no offer") + ".");
            }
            if (offer != null)
            {
                Tell(NotificationType.Success, offer.Source == OfferSource.FollowOn ? "reallife_ven_note_follow_on" : "reallife_ven_note_offer", _openInvestors, "name", investor.Name,
                    "business", offer.Business, "amount", FinanceText.Money(offer.Amount), "share", VenturesText.Share(offer.ShareBp), "day", offer.LapseDay.ToString());
            }
        }

        // Money promised for a business not yet open: paid, and the deal begun, once the player runs one more of that kind.
        private void CheckPledges(int day)
        {
            for (int i = _file.Pledges.Count - 1; i >= 0; i--)
            {
                Pledge pledge = _file.Pledges[i];
                BuildingRegistration opened = VenturesGame.Opened(_file, pledge);
                Investor investor = _file.Investor(pledge.InvestorId);
                if (opened == null || investor == null || day > pledge.OpenBy)
                {
                    continue;
                }
                Deal deal = OfferRules.Open(_file, pledge, Books.Key(opened.Address), VenturesGame.Name(opened), day);
                _wallet.Give(deal.Amount, VenturesMoney.Invested, investor.Name);
                Tell(NotificationType.Success, "reallife_ven_note_pledge_paid", _openInvestors, "name", investor.Name, "business", deal.Business, "amount", FinanceText.Money(deal.Amount),
                    "share", VenturesText.Share(deal.ShareBp));
                Detail("the promise " + pledge.Id + " from " + investor.Name + " is paid on day " + day + ": " + deal.Amount + " for " + deal.ShareBp + " bp of " + deal.Business);
            }
        }

        // ---------------------------------------------------------------- what the app asks for

        /// <summary>The app has been opened: a business a promise waited for may have been opened since the last hour, and what the badge counted has been seen.</summary>
        public bool AppOpened()
        {
            if (!_running || _broken || Save == null)
            {
                return false;
            }
            try
            {
                if (_file.Pledges.Count > 0)
                {
                    CheckPledges(Save.Day);
                }
                _file.Unseen = 0;
                Badge();
                return true;
            }
            catch (Exception e)
            {
                Fault("opening the app", e);
                return false;
            }
        }

        public void Open(int tab)
        {
            if (_app != null && _view != null && Running)
            {
                _view.Want(tab);
                _app.Open();
            }
        }

        /// <summary>An offer taken up: the money is paid in now, or promised for a business not yet open.</summary>
        public bool Accept(int offerId)
        {
            Offer offer = Running ? _file.Offer(offerId) : null;
            Investor investor = offer != null ? _file.Investor(offer.InvestorId) : null;
            if (investor == null || Save == null)
            {
                return false;
            }
            try
            {
                int day = Save.Day;
                if (offer.ForNew)
                {
                    Pledge pledge = OfferRules.Promise(_file, offer, day, VenturesGame.CountOf(offer.TypeName));
                    if (pledge == null)
                    {
                        Tell(NotificationType.Error, "reallife_ven_note_no_room", null);
                        return false;
                    }
                    Tell(NotificationType.Success, "reallife_ven_note_pledged", _openInvestors, "name", investor.Name, "amount", FinanceText.Money(pledge.Amount),
                        "type", VenturesGame.TypeName(pledge.TypeName), "day", pledge.OpenBy.ToString());
                    Detail("the offer " + offerId + " from " + investor.Name + " is accepted: " + pledge.Amount + " promised for a " + pledge.TypeName + " opened by day " + pledge.OpenBy
                           + " (" + pledge.Had + " of that kind run now)");
                    return true;
                }
                BuildingRegistration r = VenturesGame.Business(offer.Place);
                if (r == null)
                {
                    _file.Offers.Remove(offer);
                    Tell(NotificationType.Error, "reallife_ven_note_offer_lapsed", null, "name", investor.Name);
                    return false;
                }
                int amount = offer.Amount, share = offer.ShareBp;
                Deal deal = OfferRules.Accept(_file, offer, VenturesGame.Figures(r), day);
                deal.Business = VenturesGame.Name(r);
                _wallet.Give(amount, VenturesMoney.Invested, investor.Name);
                Tell(NotificationType.Success, "reallife_ven_note_accepted", _openInvestors, "name", investor.Name, "business", deal.Business, "amount", FinanceText.Money(amount),
                    "share", VenturesText.Share(share), "target", FinanceText.Money(deal.TargetProfit), "day", deal.TargetDay.ToString());
                Detail("the offer " + offerId + " from " + investor.Name + " is accepted: " + amount + " paid in for " + share + " bp of " + deal.Business + "; the deal now stands at "
                       + deal.Amount + " for " + deal.ShareBp + " bp, target " + deal.TargetProfit + " a day (from " + deal.BaseProfit + ") by day " + deal.TargetDay);
                return true;
            }
            catch (Exception e)
            {
                Fault("accepting an offer", e);
                return false;
            }
        }

        public void Refuse(int offerId)
        {
            Offer offer = Running ? _file.Offer(offerId) : null;
            if (offer != null)
            {
                _file.Offers.Remove(offer);
                Detail("the offer " + offerId + " from " + NameOf(offer.InvestorId) + " is refused");
            }
        }

        /// <summary>The one counter an offer allows. The answer is said by a notification and the offer shows the new terms, if any.</summary>
        public void Counter(int offerId, bool moreMoney)
        {
            Offer offer = Running ? _file.Offer(offerId) : null;
            Investor investor = offer != null ? _file.Investor(offer.InvestorId) : null;
            if (investor == null || offer.Countered)
            {
                return;
            }
            try
            {
                int wasAmount = offer.Amount, wasShare = offer.ShareBp;
                BusinessFigures b = VenturesGame.Figures(VenturesGame.Business(offer.Place));
                CounterAnswer answer = OfferRules.Counter(_file, offer, investor, b, moreMoney);
                Tell(answer == CounterAnswer.Refused ? NotificationType.Warning : NotificationType.Success,
                    answer == CounterAnswer.Accepted ? "reallife_ven_note_counter_yes" : answer == CounterAnswer.HalfWay ? "reallife_ven_note_counter_half" : "reallife_ven_note_counter_no",
                    _openInvestors, "name", investor.Name, "amount", FinanceText.Money(offer.Amount), "share", VenturesText.Share(offer.ShareBp));
                Detail("the counter to offer " + offerId + " (" + (moreMoney ? "more money" : "a smaller share") + ") to " + investor.Name + " (" + investor.Temper + ", "
                       + b.ProfitableDays + " profitable days): " + answer + "; " + wasAmount + " for " + wasShare + " bp is now " + offer.Amount + " for " + offer.ShareBp + " bp");
            }
            catch (Exception e)
            {
                Fault("a counter-offer", e);
            }
        }

        /// <summary>The answer to an investor's demand: a seat on the board, or a promise to cut costs.</summary>
        public bool AnswerDemand(int dealId, bool board)
        {
            Deal deal = Running ? _file.Deal(dealId) : null;
            if (deal == null || Save == null)
            {
                return false;
            }
            BusinessFigures b = VenturesGame.Figures(VenturesGame.Business(deal.Place));
            bool done = DealRules.Answer(deal, _file.Investor(deal.InvestorId), Save.Day, board, b);
            if (done)
            {
                Detail("the demand on deal " + dealId + " is answered with " + (board ? "a seat on the board until day " + deal.BoardUntil : "a promise of costs at or under " + deal.CostTarget
                       + " a day (now " + b.AvgCost14.ToString("0") + ") on day " + deal.CostDay) + "; the next target day is " + deal.TargetDay);
            }
            return done;
        }

        /// <summary>The price to buy an investor out now.</summary>
        public int BuyOutPrice(Deal deal)
        {
            return DealRules.BuyOutPrice(deal, VenturesGame.Figures(VenturesGame.Business(deal.Place)));
        }

        public bool BuyOut(int dealId)
        {
            Deal deal = Running ? _file.Deal(dealId) : null;
            if (deal == null)
            {
                return false;
            }
            try
            {
                BusinessFigures b = VenturesGame.Figures(VenturesGame.Business(deal.Place));
                int price = DealRules.BuyOutPrice(deal, b);
                string name = NameOf(deal.InvestorId);
                if (!DealRules.BuyOut(_file, deal, b, _wallet))
                {
                    Notifications.ShowInsufficientMoney();
                    return false;
                }
                Tell(NotificationType.Success, "reallife_ven_note_bought_out", _openInvestors, "name", name, "business", deal.Business, "amount", FinanceText.Money(price));
                Detail("deal " + dealId + " with " + name + " is bought out for " + price + " (recent profit " + b.AvgProfit14.ToString("0") + " a day, share " + deal.ShareBp + " bp, "
                       + VenturesCatalogue.BuyOutDays + " days; put in " + deal.Amount + ", paid out " + deal.PaidOut + ")");
                return true;
            }
            catch (Exception e)
            {
                Fault("buying an investor out", e);
                return false;
            }
        }

        /// <summary>Money promised for a business not yet open is given up: nothing is owed.</summary>
        public void GiveUp(int pledgeId)
        {
            for (int i = 0; Running && i < _file.Pledges.Count; i++)
            {
                if (_file.Pledges[i].Id == pledgeId)
                {
                    Detail("the promise " + pledgeId + " from " + NameOf(_file.Pledges[i].InvestorId) + " is given up: nothing is owed");
                    _file.Pledges.RemoveAt(i);
                    return;
                }
            }
        }

        // ---------------------------------------------------------------- the pitch

        /// <summary>
        /// A meeting with an investor about a business the player runs ('place') or one not yet open ('typeName' and
        /// 'budget'). The phone is put away and the meeting is held in the game's list window. False, with the reason
        /// said, if it cannot be held now. 'test': asked for by the test tools (held whether or not the option is on).
        /// </summary>
        public bool StartPitch(int investorId, string place, string typeName, int budget, bool test = false)
        {
            Investor investor = Running && (Settings.VenturesInvestors || test) ? _file.Investor(investorId) : null;
            if (investor == null || Save == null)
            {
                return false;
            }
            try
            {
                int day = Save.Day;
                BuildingRegistration r = string.IsNullOrEmpty(place) ? null : VenturesGame.Business(place);
                if (r == null && investor.Kind == InvestorKind.Tycoon)
                {
                    Tell(NotificationType.Error, "reallife_ven_note_not_open", null, "name", investor.Name);
                    return false;
                }
                if ((r == null && string.IsNullOrEmpty(typeName)) || !investor.Open(day) || (_meeting != null && _meeting.Active))
                {
                    Tell(NotificationType.Error, VenturesText.Never(investor, day) ? "reallife_ven_note_never" : "reallife_ven_note_not_now", null, "name", investor.Name,
                        "day", investor.OpenDay.ToString());
                    return false;
                }
                if (VenturesGame.Busy() || (VenturesBridges.Held != null && VenturesBridges.Held()))
                {
                    Tell(NotificationType.Error, "reallife_ven_note_held", null, "name", investor.Name);
                    return false;
                }
                PitchFacts facts = VenturesGame.Facts(_file, r, typeName, budget);
                if (_meeting == null)
                {
                    _meeting = new PitchMeeting(this);
                }
                PitchState state = Pitch.Begin(_file, investor, facts, r != null ? place : null, r != null ? VenturesGame.Name(r) : VenturesGame.TypeName(typeName), r != null ? null : typeName, day);
                state.Test = test;   // (what a test's meeting leads to is marked as a test's)
                if (!_meeting.Begin(state, investor))
                {
                    // The game's window could not be made: the approach does not count and nothing was asked.
                    investor.PitchedDay = 0;
                    _file.Pitches--;
                    Tell(NotificationType.Error, "reallife_ven_note_no_window", null);
                    return false;
                }
                if (Verbose)
                {
                    Logger?.Info("Ventures check (pitch " + state.Number + ", day " + day + "): " + investor.Name + " (" + investor.Kind + ", " + investor.Temper + ", diligence " + investor.Diligence
                                 + ") about " + state.Business + " (" + (facts.Running ? "running" : "not yet open, budget " + facts.Budget) + ", " + facts.Sector + "): " + state.Questions.Count
                                 + " questions. True figures: takings " + facts.SalesYesterday + " and " + facts.SalesWeek + ", profit " + facts.ProfitYesterday + " and " + facts.ProfitWeek
                                 + " a day, staff " + facts.Staff + ", age " + facts.AgeDays + ", profitable days " + facts.ProfitableDays + ", cash " + facts.Cash + ", rating " + facts.Rating
                                 + ", owed " + facts.Owed + ", payments " + facts.Payments + ", missed " + facts.Missed + ", convictions " + facts.Convictions + ", businesses "
                                 + facts.Businesses + ", jobs " + facts.Jobs + ", backers " + facts.Backers + ".");
                }
                return true;
            }
            catch (Exception e)
            {
                Fault("starting a meeting", e);
                return false;
            }
        }

        /// <summary>For the meeting: a fault in it ends Ventures for the session like any other.</summary>
        internal void MeetingFault(Exception e)
        {
            Fault("a meeting", e);
        }

        // ---------------------------------------------------------------- saying things

        internal string NameOf(int investorId)
        {
            Investor investor = _file != null ? _file.Investor(investorId) : null;
            return investor != null ? investor.Name : string.Empty;
        }

        /// <summary>A notification of the game's: the key's text with its names filled in (name, value, name, value).</summary>
        internal void Tell(NotificationType type, string key, Action click, params string[] pairs)
        {
            Dictionary<string, string> data = null;
            if (pairs != null && pairs.Length > 1)
            {
                data = new Dictionary<string, string>(pairs.Length / 2);   // (a dictionary each: the game keeps a notification's data)
                for (int i = 0; i + 1 < pairs.Length; i += 2)
                {
                    data[pairs[i]] = pairs[i + 1] ?? string.Empty;
                }
            }
            Notifications.Show(type, key, data, 10f, null, Guard.Wrap(click), true, false);
        }

        internal void Detail(string what)
        {
            if (Verbose)
            {
                Logger?.Info("Ventures check: " + what + ".");
            }
        }

        private void Badge()
        {
            if (_app != null && _file != null)
            {
                _app.SetBadge(_app.Showing ? 0 : _file.Unseen);
            }
        }

        private void OnSave()
        {
            try
            {
                if (_running && Save != null && _file != null)
                {
                    VenturesStore.Save(Save.modData, _file);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
