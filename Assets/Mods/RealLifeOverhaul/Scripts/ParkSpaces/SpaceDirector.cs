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
        private SpaceState _state = new SpaceState();
        private bool _loaded, _broken;
        private Action _onSave, _onHour, _hourSeen;
        private int _revision;

        public static void Attach(GameObject root, IModLogger logger)
        {
            Logger = logger;
            SpaceGame.Logger = logger;
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
            _state.LastHour = SpaceGame.Now;   // hours that went by without the feature are not counted
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

        /// <summary>The city's car parks read again, with today's rents and prices: at a load, each day and when the page opens.</summary>
        public void ReadParks()
        {
            SpaceGame.Parks(_parks);
        }

        internal SpacePark Park(int number) => SpaceRules.Find(_parks, number);

        // ------------------------------------------------------------------ the hour

        // The game's own hour, just before it charges the cars in its car parks: the player's cars standing in one are counted as far as their spaces go.
        private void OnHour()
        {
            if (!Running || !SpaceGame.Ready)
            {
                return;
            }
            using var hotSpot = HotSpots.Time(HotHour);
            try
            {
                int now = SpaceGame.Now;
                if (now == _state.LastHour)
                {
                    return;   // an hour is counted once, whoever raises the event again
                }
                _state.LastHour = now;
                int day = SpaceGame.Day, counted = 0;
                for (int i = 0; i < _parks.Count; i++)
                {
                    SpacePark p = _parks[i];
                    if (_state.Count(p.Number, SpaceHolding.PlayerIs) == 0 && p.Owner != SpaceOwner.Player)
                    {
                        continue;
                    }
                    int cars = SpaceGame.CarsIn(p.Number);
                    int covered = cars > 0 ? SpaceRules.Covered(SpaceRules.Entitled(_state, p, day), cars) : 0;
                    SpaceRules.AddHours(_state, p.Number, covered);
                    counted += covered;
                }
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
            _state.LastDay = day;
            ReadParks();
            long refund = SpaceRules.Refund(_state);
            if (refund > 0)
            {
                SpaceGame.Receive(refund, SpaceGame.RefundKey, string.Empty);
                _state.PaidTotal -= refund;
            }
            int gone = Settle(day), ended = Rents(day), moves = GoingPublic.InSession ? 0 : Rivals(day);
            _revision++;
            if (Verbose && (refund > 0 || gone > 0 || ended > 0 || moves > 0 || _state.Held.Count > 0))
            {
                Logger?.Info("Parking spaces check (day " + day + "): " + _parks.Count + " car park(s), " + _state.Count(0, SpaceHolding.PlayerIs) + " space(s) of the player's, "
                             + _state.Count(0, -2) + " of the tycoons', " + refund + " of the game's charges paid back, " + gone + " settled for a car park gone, " + ended
                             + " rented space(s) ended, " + moves + " move(s) of the tycoons: PASS");
            }
        }

        // The spaces whose car park is no longer in the city: marked, and after the days of grace settled at what was paid.
        private int Settle(int day)
        {
            int settled = 0;
            for (int i = _state.Held.Count - 1; i >= 0; i--)
            {
                SpaceHolding h = _state.Held[i];
                SpaceRules.Seen(h, Park(h.Park) != null, day);
                if (!SpaceRules.Gone(h, day))
                {
                    continue;
                }
                long pay = SpaceRules.GonePay(h, day);
                _state.Held.RemoveAt(i);
                settled++;
                if (h.Players)
                {
                    SpaceGame.Receive(pay, SpaceGame.GoneKey, SpaceText.Space(h.Slot));
                    _state.PaidTotal -= pay;
                    SpaceGame.Tell(NotificationType.Warning, "reallife_space_note_gone", SpaceGame.Data("space", SpaceText.Space(h.Slot), "amount", SpaceGame.Money(pay)));
                }
                else if (SpaceBridges.RivalMoney != null)
                {
                    SpaceBridges.RivalMoney(h.Who, pay);
                }
                Logger?.Info("Parking spaces: car park " + h.Park + " is gone; space " + h.Slot + " of " + (h.Players ? "the player's" : "tycoon " + h.Who) + " settled at " + pay + ".");
            }
            return settled;
        }

        // The player's rented spaces whose term is over: the next term taken, or the space given back (given up, or the rent not there).
        private int Rents(int day)
        {
            int ended = 0;
            for (int i = _state.Held.Count - 1; i >= 0; i--)
            {
                SpaceHolding h = _state.Held[i];
                SpacePark p = Park(h.Park);
                if (!h.Players || !SpaceRules.RentDue(h, day) || p == null)
                {
                    continue;   // a space whose car park is missing waits for it to stand again or to be settled
                }
                bool own = p.Owner == SpaceOwner.Player;   // the building has become the player's: nobody is left to pay rent to, and its spaces are theirs
                if (!h.Ending && !own && SpaceGame.Pay(h.Rent, SpaceGame.RentKey, p.Name))
                {
                    SpaceRules.RentPaid(h, day);
                    _state.PaidTotal += h.Rent;
                    continue;
                }
                _state.Held.RemoveAt(i);
                ended++;
                SpaceGame.Tell(h.Ending || own ? NotificationType.Info : NotificationType.Warning, h.Ending || own ? "reallife_space_note_ended" : "reallife_space_note_unpaid",
                    SpaceGame.Data("space", SpaceText.Space(h.Slot), "name", p.Name, "amount", SpaceGame.Money(h.Rent)));
            }
            return ended;
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
                SpaceBridges.RivalMoney(m.Tycoon, m.Sold ? m.Amount : -m.Amount);
                Logger?.Info("Parking spaces: tycoon " + m.Tycoon + (m.Sold ? " sold space " : " bought space ") + m.Slot + " of car park " + m.Park + " for " + m.Amount + ".");
            }
            return _moves.Count;
        }

        // ------------------------------------------------------------------ what the player does

        private static readonly string[] RefusalKeys =
        {
            null, "reallife_space_refuse_off", "reallife_space_refuse_session", "reallife_space_refuse_no_park", "reallife_space_refuse_full",
            "reallife_space_refuse_limit", "reallife_space_refuse_park_limit", "reallife_space_refuse_cash", "reallife_space_refuse_not_held", "reallife_space_refuse_own_building",
            "reallife_space_refuse_rented", "reallife_space_refuse_owned"
        };

        /// <summary>The text key that says why something was refused; null for nothing refused.</summary>
        public static string Why(SpaceRefusal r) => RefusalKeys[SpaceCatalogue.Clamp((int)r, 0, RefusalKeys.Length - 1)];

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

        /// <summary>The player buys a space in a car park from the building's owner, at today's price. Refused before any money moves.</summary>
        public SpaceRefusal Buy(int park) => Take(park, false);

        /// <summary>The player rents a space in a car park, the first term paid at once. Refused before any money moves.</summary>
        public SpaceRefusal Rent(int park) => Take(park, true);

        private SpaceRefusal Take(int park, bool rent)
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

        /// <summary>The player sells a bought space back to the building's owner.</summary>
        public SpaceRefusal Sell(int park, int slot)
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
