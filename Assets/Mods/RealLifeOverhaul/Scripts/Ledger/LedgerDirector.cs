using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using Streets;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The City Ledger as a paper the player has in their hands. Once a second it looks at the clock:
    ///
    ///   the edition    once the city's paper is written for the day (Social/News, from 06:00), today's edition is put
    ///                  together from it, the council's policies that came in or ended since the last, what is on in the
    ///                  street, the market's close and the economy's figures, and kept in the save: the page shows the same
    ///                  paper all day, however often it is opened
    ///   the round      that morning a subscriber's paper is left inside the door of their home, and a subscription whose
    ///                  week is over pays the next or stops (<see cref="LedgerRules.Morning"/>)
    ///   the kiosks     two newspaper kiosks on the pavement near the player's home, found once for that home and kept in
    ///                  the save, stand there from 06:00 to 20:00 with the game's own panel at their counter (LedgerKiosks)
    ///   the copy       inside the home, the copy the player has lies by the door, a click away (LedgerHome)
    ///
    /// No subscription and no copy bought: no paper. The page itself is the full menu's (LedgerPage); the phone's City
    /// Ledger contact is the subscriptions desk (LedgerDesk). Online, the paper and three other titles are read on the
    /// phone by subscription over the home's internet (Newsstand: LedgerDirector.Online, NewsstandApp). Per frame only the kiosk's counter is looked at, and only
    /// while the player was near a kiosk at the last second's look.
    /// </summary>
    public sealed partial class LedgerDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ledger", "LedgerDirector", typeof(LedgerDirector));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public static IModLogger Logger;
        public static LedgerDirector Instance { get; private set; }

        private const string Residential = "ba:buildingtype_residential";
        private const string CopyTransaction = "reallife_transaction_ledger_copy";
        private const string WeekTransaction = "reallife_transaction_ledger_subscription";

        private readonly List<LedgerPiece> _scratch = new List<LedgerPiece>(32);
        private LedgerState _state;
        private GameInstance _save;
        private LedgerKiosks _kiosks;
        private LedgerHome _home;
        private LedgerPage _page;
        private LedgerPage _spare;   // the page for a special front page while the paper itself is not running (ShowSpecial)
        private Action _onSave;
        private Func<bool> _payWeek;
        private bool _broken;
        private BuildingRegistration _homeReg;
        private float _homeLook;

        /// <summary>True while a kiosk's panel is up with its button on the game's interact key, and in the frame it ran: the mod's own interact key does nothing else then.</summary>
        public static bool TakesInteractKey => Instance != null && Instance._kiosks != null && Instance._kiosks.TakesInteractKey;

        public bool Running => _state != null && !_broken;
        public LedgerState State => Running ? _state : null;

        /// <summary>The kiosks near the home, for the city map (World/MapLinks); null while the paper is not running.</summary>
        public LedgerKiosks Kiosks => Running ? _kiosks : null;

        public int CopyPrice => Prices.At(LedgerCatalogue.CopyAtStart);
        public int WeekPrice => Prices.At(LedgerCatalogue.WeekAtStart);

        private static GameInstance Save => SaveGameManager.Current;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            _payWeek = PayWeek;
            StartCoroutine(Tick());
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                StandDown(false);
                _spare?.Remove();
                _spare = null;
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

        private void Update()
        {
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (_state == null || _broken || _kiosks == null)
            {
                return;
            }
            try
            {
                _kiosks.Frame();
            }
            catch (Exception e)
            {
                Fault("a kiosk's counter", e);
            }
        }

        // Once a second: start when the save is ready, stand down when the option goes off or another save is loaded.
        private void Slow()
        {
            GameInstance save = Save;
            if (save == null || GameManager.isCitySceneBeingUnloaded)
            {
                if (_state != null)
                {
                    StandDown(false);   // back to the menu: the street goes with the city, and the save has what was saved
                }
                return;
            }
            if (LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (_state != null && !ReferenceEquals(save, _save))
            {
                StandDown(false);   // another save: what this one's paper was is in that save already
            }
            if (!Settings.SocialNewspaper)
            {
                if (_state != null)
                {
                    StandDown(true);
                }
                return;
            }
            if (_state == null)
            {
                StartUp(save);
            }
            SaveHook.Keep(_onSave);
            int day = save.Day;
            int hour = save.Hour;
            var uis = InstanceBehavior<UI.UIs>.Instance;
            bool skipping = uis != null && uis.timeMachine != null && uis.timeMachine.isRunning;
            if (!skipping)
            {
                Print(day, hour, false);
                Round(day, hour);
            }
            BuildingRegistration home = Home();
            _kiosks.Slow(home, skipping);
            _home.Slow(AtHome() && LedgerRules.HasCopy(_state));
            _page.Install();
            OnlineSlow(day, hour, skipping);
        }

        // ---------------------------------------------------------------- starting and stopping

        private void StartUp(GameInstance save)
        {
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            _state = LedgerStore.Load(save.modData);
            _save = save;
            _homeReg = null;
            _homeLook = 0f;
            _kiosks = new LedgerKiosks(this);
            _home = new LedgerHome(this);
            _page = _spare ?? new LedgerPage(this);   // the spare one is in the menu already: one page of the paper's name, never two
            _spare = null;
            OnlineStart(save);
            Logger?.Info("City Ledger: ready; " + (_state.Subscribed ? "delivered to the door, paid until day " + _state.PaidUntil + (_state.Cancelled ? " (cancelled)" : string.Empty) : "no subscription")
                         + "; " + _state.Kiosks.Count + " kiosk(s) near the home; latest edition " + (_state.Edition != null ? "of day " + _state.Edition.Day : "none") + ".");
        }

        // The paper stops: with 'keep' it goes into the save's data first (a stop in the middle of play, when the game's
        // current save is certainly the one it belongs to); the kiosks and the copy leave the street, the page the menu.
        private void StandDown(bool keep)
        {
            if (keep && _state != null && Save != null && ReferenceEquals(Save, _save) && Save.modData != null)
            {
                LedgerStore.Save(Save.modData, _state);
                OnlineSave(Save.modData);
            }
            _kiosks?.Dispose();
            _home?.Dispose();
            _page?.Remove();
            OnlineStop();
            _kiosks = null;
            _home = null;
            _page = null;
            _state = null;
            _save = null;
            _homeReg = null;
        }

        /// <summary>A fault: one line, and nothing more of the paper this session. What the save holds of it is left as it is.</summary>
        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            try
            {
                _kiosks?.Dispose();
                _home?.Dispose();
                _page?.Remove();
                _stand?.Remove();
            }
            catch (Exception second)
            {
                Logger?.Error(second);
            }
            Logger?.Warn("City Ledger: switched off for this session after an error in " + where + ". No paper is delivered or sold; what the save holds of the subscription is untouched.");
        }

        private void OnSave()
        {
            try
            {
                if (_state != null && Save != null && ReferenceEquals(Save, _save) && Save.modData != null)
                {
                    LedgerStore.Save(Save.modData, _state);
                    OnlineSave(Save.modData);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// A front page of its own (Permadeath's obituary) on the paper's page, whether the paper itself runs or not: the
        /// page is put in the menu if it is not there (its own spare one while the paper is off, which the paper takes over
        /// if it starts), then opened on the special. <paramref name="closed"/> is told once when the page goes down. False
        /// while the menu cannot have the page yet (the phone is built a little after the city): ask again a moment later.
        /// Called on an event, never per frame.
        /// </summary>
        public bool ShowSpecial(LedgerSpecial special, Action closed)
        {
            try
            {
                LedgerPage page = _page;
                if (page == null)
                {
                    page = _spare ?? (_spare = new LedgerPage(this));
                }
                page.Install();
                return page.ShowSpecial(special, closed);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        /// <summary>Takes the kiosks and the copy off the street and the page off the menu; the save keeps the paper (Meta: "Clean up").</summary>
        public void StopAll()
        {
            StandDown(true);
        }

        /// <summary>The save's data of the paper has been removed: what is held of it is forgotten, and it starts afresh on its next second.</summary>
        public void Forget()
        {
            StandDown(false);
        }

        // ---------------------------------------------------------------- the edition

        // Today's edition, once the city's paper is written for today ('again': written anew, for the test button that
        // writes the city's paper again). If the city's paper does not come (its feature is not running), the edition is
        // put together an hour after the round without it.
        private void Print(int day, int hour, bool again)
        {
            int paperDay = LedgerBridges.PaperDay != null ? LedgerBridges.PaperDay() : 0;
            bool fresh = _state.Edition == null || _state.Edition.Day != day;
            if (!fresh && !again)
            {
                return;
            }
            if (hour < LedgerCatalogue.DeliveryHour || (paperDay != day && hour < LedgerCatalogue.DeliveryHour + 1))
            {
                return;
            }
            int since = _state.Edition != null && _state.Edition.Day < day ? Math.Max(_state.Edition.Day, day - LedgerCatalogue.MarketNewsDays) : day - LedgerCatalogue.MarketNewsDays;
            var e = new LedgerEdition { Day = day };
            if (paperDay == day && LedgerBridges.Paper != null)
            {
                _scratch.Clear();
                LedgerBridges.Paper(_scratch);
                e.Pieces.AddRange(_scratch);
            }
            if (fresh)
            {
                if (LedgerBridges.Council != null)
                {
                    _scratch.Clear();
                    _state.Council = LedgerBridges.Council(_state.Council, _scratch);
                    e.Pieces.AddRange(_scratch);
                }
            }
            else
            {
                for (int i = 0; i < _state.Edition.Pieces.Count; i++)
                {
                    LedgerPiece kept = _state.Edition.Pieces[i];
                    if (kept != null && kept.Key.StartsWith(PolicyPrefix, StringComparison.Ordinal))
                    {
                        e.Pieces.Add(kept);   // written again: the council's news since the last edition is kept
                    }
                }
            }
            if (LedgerBridges.Extra != null)
            {
                _scratch.Clear();
                LedgerBridges.Extra(day, _scratch);
                e.Pieces.AddRange(_scratch);
            }
            LedgerBridges.Market?.Invoke(e, since);
            LedgerBridges.Economy?.Invoke(e);
            e.Pieces.RemoveAll(p => p == null);
            if (e.Pieces.Count > LedgerCatalogue.PiecesMost)
            {
                e.Pieces.Sort((a, b) => b.Weight.CompareTo(a.Weight));
                e.Pieces.RemoveRange(LedgerCatalogue.PiecesMost, e.Pieces.Count - LedgerCatalogue.PiecesMost);
            }
            KeepEarlier(_state.Edition, day);   // the edition it replaces, to be read again on the phone
            _state.Edition = e;
            _page?.Changed();
            _stand?.Changed();
            Logger?.Info("City Ledger: the edition of day " + day + (again ? " written again" : string.Empty) + " has " + e.Pieces.Count + " pieces"
                         + (e.HasMarket ? ", the market's close of day " + e.MarketDay + " (" + e.Quotes.Count + " companies)" : ", no market report") + ".");
        }

        private static readonly string PolicyPrefix = "reallife" + "_ledger_policy";

        // ---------------------------------------------------------------- the morning's round

        private void Round(int day, int hour)
        {
            if (_state.Edition == null || _state.Edition.Day != day || hour < LedgerCatalogue.DeliveryHour)
            {
                return;
            }
            if (_state.Subscribed && day > _state.LastMorning && Home() == null)
            {
                return;   // no home to bring it to: the round does not come, and a morning it does not come is never charged
            }
            int price = WeekPrice;
            LedgerMorning m = LedgerRules.Morning(_state, day, _payWeek);
            switch (m)
            {
                case LedgerMorning.Kept:
                case LedgerMorning.Billed:
                    if (m == LedgerMorning.Billed)
                    {
                        Say("reallife_ledger_msg_billed", "price", LedgerText.Money(price), "paid", _state.PaidUntil.ToString());
                    }
                    LedgerRules.Deliver(_state, day);
                    Notifications.Show(NotificationType.Info, AtHome() ? "reallife_ledger_note_delivered_here" : "reallife_ledger_note_delivered", null, 8f);
                    Logger?.Info("City Ledger: the round of day " + day + ": " + (m == LedgerMorning.Billed ? "a week paid (" + price + "), " : string.Empty) + "the paper is at the door.");
                    break;
                case LedgerMorning.Lapsed:
                    Say("reallife_ledger_msg_lapsed", "price", LedgerText.Money(price));
                    Logger?.Info("City Ledger: the round of day " + day + ": the next week (" + price + ") could not be paid; the subscription has stopped.");
                    break;
                case LedgerMorning.Ended:
                    Say("reallife_ledger_msg_ended");
                    Logger?.Info("City Ledger: the round of day " + day + ": the cancelled subscription has ended.");
                    break;
            }
        }

        private bool PayWeek()
        {
            return Charge(WeekPrice, WeekTransaction);
        }

        // Money out through the game's own call; false (nothing taken) if there is not that much.
        private static bool Charge(int amount, string transaction)
        {
            GameInstance save = Save;
            if (save == null || save.Money < amount)
            {
                return false;
            }
            GameManager.ChangeMoneySafe(-amount, new TransactionInfo(transaction), null, null, force: true);
            return true;
        }

        private static void Say(string key, params string[] pairs)
        {
            try
            {
                LedgerBridges.Say?.Invoke(key, LedgerText.Data(pairs));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- the home

        /// <summary>
        /// The player's home: the first home they rent (the same one while they keep it, whichever home they stand in, so
        /// that the kiosks and the address on the round stay put). Looked up at most every few seconds.
        /// </summary>
        public BuildingRegistration Home()
        {
            if (Time.unscaledTime < _homeLook)
            {
                return _homeReg != null && GoingPublic.Mine(_homeReg) ? _homeReg : null;
            }
            _homeLook = Time.unscaledTime + 5f;
            if (_homeReg != null && GoingPublic.Mine(_homeReg))
            {
                return _homeReg;
            }
            _homeReg = null;
            try
            {
                List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
                for (int i = 0; all != null && i < all.Count; i++)
                {
                    if (all[i] != null && GoingPublic.Mine(all[i]) && all[i].GetBuildingType() == Residential)
                    {
                        _homeReg = all[i];
                        break;
                    }
                }
            }
            catch (Exception)
            {
                _homeReg = null;
            }
            return _homeReg;
        }

        /// <summary>True while the player stands in a home of their own.</summary>
        public static bool AtHome()
        {
            if (!BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            var manager = InstanceBehavior<BuildingManager>.Instance;
            return manager != null && manager.building != null && GoingPublic.Mine(manager.buildingRegistration) && manager.building.BuildingType == Residential;
        }

        /// <summary>The home's address as the game writes it, or null without a home.</summary>
        public string HomeName
        {
            get
            {
                BuildingRegistration home = Home();
                return home != null ? home.Address.ToFormattedString() : null;
            }
        }

        // ---------------------------------------------------------------- the desk: the subscription

        /// <summary>
        /// A subscription taken out on the phone: the first week paid now (or nothing, for a cancelled one taken up
        /// again). False if it cannot be: no home to deliver to, one already runs, or the money is not there (noMoney).
        /// </summary>
        public bool Subscribe(out int firstDay, out bool noMoney)
        {
            firstDay = 0;
            noMoney = false;
            GameInstance save = Save;
            if (!Running || save == null || Home() == null)
            {
                return false;
            }
            if (!LedgerRules.Subscribe(_state, save.Day, save.Hour, out bool payNow))
            {
                return false;
            }
            if (payNow)
            {
                if (!Charge(WeekPrice, WeekTransaction))
                {
                    noMoney = true;
                    return false;
                }
                LedgerRules.Start(_state, save.Day, save.Hour);
                firstDay = _state.PaidUntil - LedgerCatalogue.WeekDays + 1;
                Logger?.Info("City Ledger: subscribed on day " + save.Day + " (" + WeekPrice + " a week); the first paper on day " + firstDay + ".");
            }
            else
            {
                firstDay = LedgerRules.FirstDelivery(_state, save.Day, save.Hour);
                Logger?.Info("City Ledger: the cancelled subscription is taken up again on day " + save.Day + "; paid until day " + _state.PaidUntil + ".");
            }
            return true;
        }

        /// <summary>The subscription is cancelled: the paper comes until the last day paid for. False if there is none to cancel.</summary>
        public bool Cancel()
        {
            if (!Running || !LedgerRules.Cancel(_state))
            {
                return false;
            }
            Logger?.Info("City Ledger: the subscription is cancelled; the last paper is on day " + _state.PaidUntil + ".");
            return true;
        }

        // ---------------------------------------------------------------- the kiosk and the page

        /// <summary>
        /// At a kiosk's counter: today's paper bought (or the copy already bought read again), and the page opened. False,
        /// with a notification, if the paper is not on sale now or the money is not there.
        /// </summary>
        public bool BuyCopy()
        {
            GameInstance save = Save;
            if (!Running || save == null)
            {
                return false;
            }
            if (!LedgerRules.OnSale(_state, save.Day, save.Hour))
            {
                Notifications.Show(NotificationType.Info, LedgerRules.KioskOpen(save.Hour) ? "reallife_ledger_note_not_in" : "reallife_ledger_note_closed", null, 5f);
                return false;
            }
            if (!LedgerRules.BoughtCopy(_state))
            {
                int price = CopyPrice;
                if (!Charge(price, CopyTransaction))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ledger_note_broke", LedgerText.Data("price", LedgerText.Money(price)), 5f);
                    return false;
                }
                LedgerRules.Buy(_state, save.Day);
                Logger?.Info("City Ledger: a copy of day " + save.Day + " bought at a kiosk (" + price + ").");
            }
            Read(LedgerPage.FromKiosk);
            return true;
        }

        /// <summary>Opens the page on the latest edition (from the copy at home, at the kiosk, or a test). False if there is nothing to show yet.</summary>
        public bool Read(int from)
        {
            if (!Running || _state.Edition == null || _page == null)
            {
                return false;
            }
            return _page.Show(_state.Edition, from);
        }
    }
}
