using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using Streets;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The City Ledger in the player's hands: once a second it prints today's edition, does the subscription's round,
    /// and keeps the kiosks (LedgerKiosks), the copy at home (LedgerHome) and the page (LedgerPage). The cost: per
    /// frame only a kiosk's counter, while the player was near one at the last look.
    /// </summary>
    public sealed partial class LedgerDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ledger", "LedgerDirector", typeof(LedgerDirector));

        public static IModLogger Logger;
        public static LedgerDirector Instance { get; private set; }

        private const string Residential = "ba:buildingtype_residential";
        private const string CopyTransaction = "reallife_transaction_ledger_copy";
        private const string WeekTransaction = "reallife_transaction_ledger_subscription";
        private static readonly string PolicyPrefix = "reallife" + "_ledger_policy";   // in parts: a key's beginning, not a key

        private readonly List<LedgerPiece> _scratch = new List<LedgerPiece>(32);
        private LedgerState _state;
        private GameInstance _save;
        private LedgerKiosks _kiosks;
        private LedgerHome _home;
        private LedgerPage _page;
        private LedgerPage _spare;   // a special front page's, while the paper itself is off (ShowSpecial)
        private Action _onSave;
        private Func<bool> _payWeek;
        private bool _broken;
        private BuildingRegistration _homeReg;
        private float _homeLook;

        /// <summary>True while a kiosk's counter holds the game's interact key, and in the frame it was pressed.</summary>
        public static bool TakesInteractKey => Instance != null && Instance._kiosks != null && Instance._kiosks.TakesInteractKey;

        public bool Running => _state != null && !_broken;
        public LedgerState State => Running ? _state : null;

        /// <summary>The day of the edition a piece is in (today's, or one kept to be read again); -1 if none holds it.</summary>
        public int DayOf(LedgerPiece piece)
        {
            if (!Running || piece == null)
            {
                return -1;
            }
            if (_state.Edition != null && _state.Edition.Pieces.Contains(piece))
            {
                return _state.Edition.Day;
            }
            for (int i = 0; _online != null && i < _online.Earlier.Count; i++)
            {
                if (_online.Earlier[i].Pieces.Contains(piece))
                {
                    return _online.Earlier[i].Day;
                }
            }
            return -1;
        }

        public bool HasDay(int day)
        {
            if (!Running)
            {
                return false;
            }
            if (_state.Edition != null && _state.Edition.Day == day)
            {
                return true;
            }
            for (int i = 0; _online != null && i < _online.Earlier.Count; i++)
            {
                if (_online.Earlier[i].Day == day)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The kiosks near the home, for the city map; null while the paper is not running.</summary>
        public LedgerKiosks Kiosks => Running ? _kiosks : null;

        public int CopyPrice => Prices.At(LedgerCatalogue.CopyAtStart);
        public int WeekPrice => Prices.At(LedgerCatalogue.WeekAtStart);

        private static GameInstance Save => SaveGameManager.Current;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            _payWeek = PayWeek;
            ShareRegister();
            StartCoroutine(Guard.Routine(Tick()));
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
                while (StartLine.Holds(StartSeats.Ledger))
                {
                    yield return null;   // the first load after a save waits for a frame of its own (Core/StartLine)
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

        private static readonly int HotUpdate = HotSpots.Slot("Ledger: LedgerDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
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

        private static readonly int HotTick = HotSpots.Slot("Ledger: LedgerDirector.Tick");
        // Once a second: starts with the save, stands down when the option goes off or another save is loaded.
        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);
            GameInstance save = Save;
            if (save == null || GameManager.isCitySceneBeingUnloaded)
            {
                if (_state != null)
                {
                    StandDown(false);   // back to the menu: the save already has it
                }
                return;
            }
            if (LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (_state != null && !ReferenceEquals(save, _save))
            {
                StandDown(false);   // another save: the old one's paper is in that save already
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
                if (SharedCity.Follows(LedgerShare.Part))
                {
                    GuestPieces(day, hour);   // a guest following the host's paper prints none: its own stories go to the host
                }
                else
                {
                    OwnAgain(day);
                    Print(day, hour, false);
                    Reprint(day, hour);
                }
                Round(day, hour);
            }
            BuildingRegistration home = Home();
            _kiosks.Slow(home, skipping);
            _home.Slow(AtHome() && LedgerRules.HasCopy(_state));
            _page.Install();
            OnlineSlow(day, hour, skipping);
        }

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
            _page = _spare ?? new LedgerPage(this);   // the spare is in the menu already: never two pages of one name
            _spare = null;
            OnlineStart(save);
            Logger?.Info("City Ledger: ready; "
                         + (_state.Subscribed ? "delivered to the door, paid until day " + _state.PaidUntil + (_state.Cancelled ? " (cancelled)" : string.Empty) : "no subscription")
                         + "; " + _state.Kiosks.Count + " kiosk(s) near the home; latest edition " + (_state.Edition != null ? "of day " + _state.Edition.Day : "none") + ".");
        }

        // 'keep' saves first: a stop in play, when the current save is certainly its own.
        private void StandDown(bool keep)
        {
            if (keep)
            {
                WriteSave();
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

        /// <summary>Switches the paper off for the session; the save's data of it is left as it is.</summary>
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
                WriteSave();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void WriteSave()
        {
            if (_state != null && Save != null && ReferenceEquals(Save, _save) && Save.modData != null)
            {
                LedgerStore.Save(Save.modData, _state);
                OnlineSave(Save.modData);
            }
        }

        /// <summary>A front page of its own (Permadeath's obituary), paper running or not; false while the menu cannot take it yet.</summary>
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

        /// <summary>The kiosks, the copy and the page go; the save keeps the paper (Meta's "Clean up").</summary>
        public void StopAll()
        {
            StandDown(true);
        }

        /// <summary>The save's data of the paper was removed: what is held is forgotten, and it starts afresh on its next second.</summary>
        public void Forget()
        {
            StandDown(false);
        }

        // Today's edition once the city's paper is out, or an hour after the round without it; 'again' writes it anew.
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
                        e.Pieces.Add(kept);   // written again: the council's pieces stay
                    }
                }
            }
            if (LedgerBridges.Extra != null)
            {
                _scratch.Clear();
                LedgerBridges.Extra(day, _scratch);
                e.Pieces.AddRange(_scratch);
            }
            GuestPiecesInto(e, day);
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
            LedgerBridges.TextsEdition?.Invoke(e);   // RLO Texts asks for the briefs' words now, ahead of the reading
            _page?.Changed();
            _stand?.Changed();
            SharedCity.Changed(LedgerShare.Part);   // to a session's guests with the next update
            Logger?.Info("City Ledger: the edition of day " + day + (again ? " written again" : string.Empty) + " has " + e.Pieces.Count + " pieces"
                         + (e.HasMarket ? ", the market's close of day " + e.MarketDay + " (" + e.Quotes.Count + " companies)" : ", no market report") + ".");
        }

        private void Round(int day, int hour)
        {
            if (_state.Edition == null || _state.Edition.Day != day || hour < LedgerCatalogue.DeliveryHour)
            {
                return;
            }
            if (_state.Subscribed && day > _state.LastMorning && Home() == null)
            {
                return;   // no home, no round; a morning without one is never charged
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

        // The game's own charge; false, with nothing taken, if the money is not there.
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

        /// <summary>The player's first rented home, kept while they keep it; looked up at most every five seconds.</summary>
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

        /// <summary>A subscription from the phone, its first week paid now (free for a cancelled one taken up again); false if it cannot be.</summary>
        public bool Subscribe(out int firstDay, out bool noMoney)
        {
            firstDay = 0;
            noMoney = false;
            GameInstance save = Save;
            if (!Running || save == null || Home() == null)
            {
                return false;
            }
            if (!LedgerRules.Subscribe(_state, out bool payNow))
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

        /// <summary>At a kiosk's counter: today's paper bought (or the copy bought read again) and opened; false, with a notice, if not.</summary>
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

        /// <summary>Opens the page on the latest edition (<paramref name="from"/>: LedgerPage's From values); false if there is none yet.</summary>
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
