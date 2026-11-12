using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.ParkSpaces
{
    /// <summary>
    /// Parking spaces in a building's car park (docs/features/parking-spaces.md): a space bought from the building's owner or rented
    /// by the term, without the building. The car is left there by the game's own rules and the game charges it by the hour as ever;
    /// each game hour (the game's own hour event) the director counts the player's cars standing in a car park as far as their
    /// spaces go, and at the day's turn the charge for those hours is paid back as a line of its own. The day's pass also takes the
    /// rent, settles the spaces of a car park that has gone and gives the rival tycoons their look. It owns the state and its timing;
    /// money moves only through <see cref="SpaceGame"/>. No Update. COST: nothing per frame; a few comparisons a second, a walk of
    /// the player's vehicles once a game hour while a space is held, a walk of the city's buildings once a day.
    /// </summary>
    public sealed partial class SpaceDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static SpaceDirector Instance { get; private set; }

        private static readonly int HotDay = HotSpots.Slot("Parking spaces: the day's pass");
        private static readonly int HotHour = HotSpots.Slot("Parking spaces: the hour's count");

        private readonly List<SpacePark> _parks = new List<SpacePark>(8);
        private readonly List<SpaceMove> _moves = new List<SpaceMove>(4);
        private readonly List<string> _cars = new List<string>(8);
        private readonly ISpaceLedger _books = new Books();
        private SpaceState _state = new SpaceState();
        private bool _loaded, _broken;
        private Action _onSave, _onHour, _hourSeen;
        private int _revision, _readFrame = -1;

        public static void Attach(GameObject root, IModLogger logger)
        {
            Logger = logger;
            root.AddComponent<SpaceDirector>();
        }

        /// <summary>True while a save is read, the option is on and nothing has failed.</summary>
        public bool Running => _loaded && !_broken && Settings.ParkingSpaces;

        /// <summary>The state in use. Read it only while <see cref="Running"/>.</summary>
        public SpaceState State => _state;

        /// <summary>The city's car parks as last read. Read them only while <see cref="Running"/>.</summary>
        public IReadOnlyList<SpacePark> Parks => _parks;

        /// <summary>Moves whenever something a page shows may have changed.</summary>
        public int Revision => _revision;

        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            _onHour = Guard.Wrap(OnHour);
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GlobalEvents.onNewHour -= _onHour;   // the handler that was put on: Guard.Wrap's
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
                    Look();
                }
                catch (Exception e)
                {
                    Fault(e);
                }
            }
        }

        private void Fault(Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            Logger?.Warn("Parking spaces: switched off for this session after the error above. The save keeps the spaces as they were; cars in a car park pay by the hour meanwhile.");
        }

        private void Look()
        {
            var game = InstanceBehavior<GameManager>.Instance;
            if (game == null || game.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (!Settings.ParkingSpaces)
            {
                if (_loaded)
                {
                    PutAway();
                }
                return;
            }
            if (!_loaded)
            {
                Load();
            }
            SaveHook.Keep(_onSave);
            EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen);
            int day = SpaceGame.Day;
            if (day != _state.LastDay)
            {
                Pass(day);
            }
            else
            {
                OwnersLook(day);
            }
        }

        private void Load()
        {
            GameInstance save = SaveGameManager.Current;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            _state = SpaceStore.Load(save.modData, out int version) ?? new SpaceState();
            if (_state.Seed == 0)
            {
                _state.Seed = UnityEngine.Random.Range(1, int.MaxValue);   // once a save, then kept: the city's holders and the tycoons' looks come from it
            }
            SpaceDay.Resume(_state, SpaceGame.Now);   // hours that went by without the feature are not counted
            _loaded = true;
            ReadParks();
            _revision++;
            Logger?.Info("Parking spaces: " + (version > 0 ? "read the save's line (version " + version + "): " + _state.Held.Count + " space(s) held" : "none held yet") + "; "
                         + _parks.Count + " car park(s) in the city.");
        }

        private void OnSave()
        {
            try
            {
                if (_loaded)
                {
                    Write();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Write()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            save.modData[SpaceStore.Key] = SpaceStore.Write(_state);
        }

        // The option was switched off: the line stays as it is, nothing is counted, paid back or taken, and the spaces wait.
        private void PutAway()
        {
            Write();
            _loaded = false;
            _revision++;
            Logger?.Info("Parking spaces: switched off; the spaces held are kept in the save and count again when it is on.");
        }

        /// <summary>Forgets everything of this save. The next look opens the feature afresh.</summary>
        public void Forget()
        {
            GameInstance save = SaveGameManager.Current;
            if (save != null && save.modData != null)
            {
                save.modData.Remove(SpaceStore.Key);
            }
            _state = new SpaceState();
            _loaded = false;
            _revision++;
        }

        /// <summary>The city's car parks read again, with today's rents and prices: at a load, each day, when a page is built, when the hour finds one missing and before a purchase.</summary>
        public void ReadParks()
        {
            SpaceGame.Parks(_parks);
            _readFrame = Time.frameCount;
            for (int i = 0; i < _state.Held.Count; i++)
            {
                SpaceRules.Noted(_state.Held[i], SpaceRules.Find(_parks, _state.Held[i].Park));   // the price and owner last seen, for a car park that then goes
            }
        }

        /// <summary>
        /// The city's car parks as they are when a page is built: read again (once in a frame at most, and only when a page is built,
        /// never per frame), so the pages never show the figures of the last day's pass.
        /// </summary>
        public IReadOnlyList<SpacePark> Open()
        {
            if (_readFrame != Time.frameCount)
            {
                ReadParks();
            }
            return _parks;
        }

        internal SpacePark Park(int number) => SpaceRules.Find(_parks, number);

        // ------------------------------------------------------------------ the hour

        // The game's own hour, just before it charges the cars in its car parks: the player's cars standing in one are counted, each for its vehicle, as far as
        // their spaces go (SpaceDay.Hour). The city is read again if a car park is not known (one that stood late at a load).
        private void OnHour()
        {
            if (!Running || !SpaceGame.Ready)
            {
                return;
            }
            using var hotSpot = HotSpots.Time(HotHour);
            try
            {
                if (SpaceDay.Stale(_state, _parks))
                {
                    ReadParks();
                }
                int counted = SpaceDay.Hour(_state, _parks, SpaceGame.Now, SpaceGame.Day, SpaceGame.CarsIn, _cars);
                if (counted > 0)
                {
                    _revision++;
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // ------------------------------------------------------------------ the day

        private void Pass(int day)
        {
            using var hotSpot = HotSpots.Time(HotDay);
            ReadParks();
            bool session = GoingPublic.InSession;
            SpaceDayResult r = SpaceDay.Pass(_state, _parks, day, session, SpaceGame.HasVehicle, _books);
            int moves = session ? 0 : Rivals(day);   // the tycoons rest in a shared city
            _revision++;
            if (Verbose && (r.Refund > 0 || r.Gone > 0 || r.Owned > 0 || r.Ended > 0 || moves > 0 || _state.Held.Count > 0))
            {
                Logger?.Info("Parking spaces check (day " + day + "): " + _parks.Count + " car park(s), " + _state.Count(0, SpaceHolding.PlayerIs) + " space(s) of the player's, "
                             + _state.Count(0, -2) + " of the tycoons', " + r.Refund + " of the game's charges paid back, " + r.Gone + " settled for a car park gone, "
                             + r.Owned + " settled for a building the player owns, " + r.Ended + " rented space(s) ended, " + moves + " move(s) of the tycoons"
                             + (session ? " (a shared city: none settled)" : string.Empty) + ": PASS");
            }
        }

        // The building of a space the player holds has become theirs since the last look: it is settled at once.
        private void OwnersLook(int day)
        {
            if (_state.Held.Count == 0 || !SpaceGame.OwnersChanged(_parks, _state))
            {
                return;
            }
            int settled = SpaceDay.Owned(_state, _parks, day, GoingPublic.InSession, _books);
            if (settled > 0)
            {
                _revision++;
                Logger?.Info("Parking spaces: " + settled + " bought space(s) settled, the building is the player's now.");
            }
        }

        // What the day's pass books and says, through the game: the one booking call and the notices.
        private sealed class Books : ISpaceLedger
        {
            private static string NameOf(SpaceLine line, SpacePark p, SpaceHolding h)
            {
                return line == SpaceLine.Refund ? string.Empty : line == SpaceLine.Gone || p == null ? (h != null ? SpaceText.Space(h.Slot) : string.Empty) : p.Name;
            }

            public bool Pay(long amount, SpaceLine line, SpacePark park) => SpaceGame.Pay(amount, SpaceDay.KeyOf(line), park != null ? park.Name : string.Empty);

            public void Receive(long amount, SpaceLine line, SpacePark park, SpaceHolding h) => SpaceGame.Receive(amount, SpaceDay.KeyOf(line), NameOf(line, park, h));

            public void Tell(SpaceNote note, SpacePark park, SpaceHolding h, long amount)
            {
                string key = note == SpaceNote.Gone ? "reallife_space_note_gone" : note == SpaceNote.Unpaid ? "reallife_space_note_unpaid"
                    : note == SpaceNote.OwnBuilding ? "reallife_space_note_own" : "reallife_space_note_ended";
                SpaceGame.Tell(note == SpaceNote.Gone || note == SpaceNote.Unpaid ? NotificationType.Warning : NotificationType.Info, key,
                    SpaceGame.Data("space", SpaceText.Space(h.Slot), "name", park != null ? park.Name : string.Empty, "amount", SpaceGame.Money(amount)));
                SpaceDirector.Logger?.Info("Parking spaces: space " + h.Slot + " of car park " + h.Park + " (" + note + ", " + amount + ").");
            }

            public void Tycoon(int who, long worth)
            {
                if (worth != 0 && SpaceBridges.RivalMoney != null)
                {
                    SpaceBridges.RivalMoney(who, worth);
                }
            }
        }

        // The rival tycoons' look, by the player's own rules (SpaceRules.RivalLook); their money moves through the rivals' own worth.
        private int Rivals(int day)
        {
            int count = SpaceBridges.RivalCount != null ? Math.Max(0, Math.Min(16, SpaceBridges.RivalCount())) : 0;
            if (count == 0 || SpaceBridges.RivalWorth == null || SpaceBridges.RivalMoney == null)
            {
                return 0;
            }
            var worth = new long[count];
            var floor = new long[count];
            for (int t = 0; t < count; t++)
            {
                worth[t] = SpaceBridges.RivalWorth(t);
                floor[t] = SpaceBridges.RivalFloor != null ? SpaceBridges.RivalFloor(t) : 0L;
            }
            _moves.Clear();
            SpaceRules.RivalLook(_state, _parks, day, worth, floor, _moves);
            for (int i = 0; i < _moves.Count; i++)
            {
                SpaceMove m = _moves[i];
                SpaceBridges.RivalMoney(m.Tycoon, m.Worth);   // what they own does not change by buying; a sale loses what it lost on the way
                Logger?.Info("Parking spaces: tycoon " + m.Tycoon + (m.Sold ? " sold space " : " bought space ") + m.Slot + " of car park " + m.Park + " for " + m.Amount + ".");
            }
            return _moves.Count;
        }

        // ------------------------------------------------------------------ what the player does

        /// <summary>The text key that says why something was refused; null for nothing refused.</summary>
        public static string Why(SpaceRefusal r) => Ui.SpaceWords.WhyKey(r);

        private static SpaceRefusal Say(SpaceRefusal r)
        {
            if (r != SpaceRefusal.None)
            {
                Notifications.ShowError(Why(r));
            }
            return r;
        }

        /// <summary>Whether the player could buy (or rent) a space in a car park now, and which: asked by the pages before they offer the button.</summary>
        public SpaceRefusal MayTake(int park, bool rent, out int slot)
        {
            SpacePark p = Running ? Park(park) : null;
            return SpaceRules.MayTake(Running ? _state : null, p, SpaceGame.Day, Running, GoingPublic.InSession, SpaceGame.Cash, p != null ? (rent ? p.RentMonth : p.Price) : 0, out slot);
        }

        /// <summary>
        /// The player buys a space in a car park from the building's owner. <paramref name="shown"/> and <paramref name="slotShown"/> are the price and the space the
        /// question box asked about: if the city has moved since (the price, the space) the purchase is refused, before any money, and the page is asked again.
        /// </summary>
        public SpaceRefusal Buy(int park, long shown = -1, int slotShown = 0) => Take(park, false, shown, slotShown);

        /// <summary>The player rents a space in a car park, the first term paid at once; asked as <see cref="Buy"/> is.</summary>
        public SpaceRefusal Rent(int park, long shown = -1, int slotShown = 0) => Take(park, true, shown, slotShown);

        private SpaceRefusal Take(int park, bool rent, long shown, int slotShown)
        {
            if (Running)
            {
                ReadParks();   // today's price and the building's owner as they are now
            }
            SpaceRefusal r = MayTake(park, rent, out int slot);
            if (r != SpaceRefusal.None)
            {
                return Say(r);
            }
            SpacePark p = Park(park);
            long cost = rent ? p.RentMonth : p.Price;
            if (SpaceRules.Moved(shown, cost, slotShown, slot))
            {
                _revision++;
                return Say(SpaceRefusal.Moved);   // the price charged is the price shown, or nothing is charged
            }
            if (!SpaceGame.Pay(cost, rent ? SpaceGame.RentKey : SpaceGame.BuyKey, p.Name))
            {
                return Say(SpaceRefusal.Cash);
            }
            SpaceHolding h = SpaceRules.Take(_state, p, slot, SpaceHolding.PlayerIs, SpaceGame.Day, rent ? 0 : cost, rent ? cost : 0);
            if (h == null)
            {
                SpaceGame.Receive(cost, rent ? SpaceGame.RentKey : SpaceGame.BuyKey, p.Name);   // the money back: the space could not be had after all
                return Say(SpaceRefusal.Full);
            }
            _state.PaidTotal += cost;
            _revision++;
            Logger?.Info("Parking spaces: space " + slot + " of car park " + p.Number + " (" + p.Name + ") " + (rent ? "rented at " : "bought for ") + cost + " from " + p.Owner + ".");
            SpaceGame.Tell(NotificationType.Info, rent ? "reallife_space_note_rented" : "reallife_space_note_bought",
                SpaceGame.Data("space", SpaceText.Space(slot), "name", p.Name, "amount", SpaceGame.Money(cost), "owner", SpaceText.Owner(p)));
            return SpaceRefusal.None;
        }

        /// <summary>What the building's owner would pay for a bought space today.</summary>
        public long BackFor(SpaceHolding h)
        {
            SpacePark p = h != null ? Park(h.Park) : null;
            return p != null && h != null && !h.Rented ? SpaceRules.Back(h.Paid, p.Price) : 0L;
        }

        /// <summary>The player sells a bought space back to the building's owner; <paramref name="shown"/> is what the question box said the owner would pay (-1: not asked).</summary>
        public SpaceRefusal Sell(int park, int slot, long shown = -1)
        {
            if (Running)
            {
                ReadParks();
            }
            SpaceHolding h = Running ? _state.At(park, slot) : null;
            h = h != null && h.Players ? h : null;
            SpacePark p = Park(park);
            SpaceRefusal r = SpaceRules.MaySell(Running ? _state : null, h, p, Running, GoingPublic.InSession);
            if (r != SpaceRefusal.None)
            {
                return Say(r);
            }
            if (SpaceRules.Moved(shown, SpaceRules.Back(h.Paid, p.Price), 0, 0))
            {
                _revision++;
                return Say(SpaceRefusal.Moved);   // what is paid is what was shown, or nothing is sold
            }
            long back = SpaceRules.Sell(_state, h, p.Price);
            SpaceGame.Receive(back, SpaceGame.SoldKey, p.Name);
            _state.PaidTotal -= back;
            _revision++;
            Logger?.Info("Parking spaces: space " + slot + " of car park " + park + " sold back for " + back + " (paid " + h.Paid + ", today " + p.Price + ").");
            SpaceGame.Tell(NotificationType.Info, "reallife_space_note_sold", SpaceGame.Data("space", SpaceText.Space(slot), "name", p.Name, "amount", SpaceGame.Money(back)));
            return SpaceRefusal.None;
        }

        /// <summary>The player gives a rented space up at the end of its term, or keeps it after all.</summary>
        public SpaceRefusal SetEnding(int park, int slot, bool ending)
        {
            SpaceHolding h = Running ? _state.At(park, slot) : null;
            h = h != null && h.Players ? h : null;
            SpaceRefusal r = SpaceRules.SetEnding(Running ? _state : null, h, ending, Running);
            if (r == SpaceRefusal.None)
            {
                _revision++;
            }
            return Say(r);
        }

        /// <summary>A line on the spaces a tycoon holds, for the rich list and their panel; empty for none.</summary>
        public string RivalLine(int tycoon)
        {
            if (!Running)
            {
                return string.Empty;
            }
            for (int i = 0; i < _state.Held.Count; i++)
            {
                SpaceHolding h = _state.Held[i];
                SpacePark p = h.Who == tycoon ? Park(h.Park) : null;
                if (p != null)
                {
                    return SpaceText.Fill("reallife_space_rival_line", "rival", SpaceBridges.RivalName != null ? SpaceBridges.RivalName(tycoon) : string.Empty,
                        "name", p.Name, "amount", SpaceGame.Money(h.Paid));
                }
            }
            return string.Empty;
        }
    }
}
