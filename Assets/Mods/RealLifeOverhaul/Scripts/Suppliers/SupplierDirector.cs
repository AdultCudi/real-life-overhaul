using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>
    /// Suppliers and contracts (docs/features/suppliers.md): a choice of wholesalers that are firms of the city, fixed price contracts under take or pay, deliveries
    /// that come late or short for real causes, and the rivals buying by the same rules. It owns the state and its timing: a second at a time it looks whether a save is
    /// ready (and loads or lets go of its line), a game hour it brings the deliveries that are due, a game day it goes through once (the suppliers' own books, what is
    /// owed, tomorrow's deliveries judged, the buyers' rules) and the city's rival shops a slice a second. Every rule is the game-free engine's
    /// (<see cref="SupplierEngine"/>); money moves only through <see cref="SupplierGame"/>, which books it through the mod's one call. No Update. COST: nothing per
    /// frame; a few comparisons a second, a pass over the contracts an hour and a day, forty rival shops a second once a day.
    /// </summary>
    public sealed partial class SupplierDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static SupplierDirector Instance { get; private set; }
        public const string TestKey = "reallife:suppliers_test";

        private static readonly int HotDay = HotSpots.Slot("Suppliers: the day's pass");
        private static readonly int HotHour = HotSpots.Slot("Suppliers: the hour's deliveries");

        private SupplierState _state;
        private SupplierGame _world;
        private bool _loaded, _broken;
        private Action _onSave;
        private int _revision;
        private int _hourDay = -1, _hour = -1;
        private string _beforeTests;
        private readonly List<string> _supplies = new List<string>(16);

        public static void Attach(GameObject root, IModLogger logger)
        {
            Logger = logger;
            root.AddComponent<SupplierDirector>();
        }

        /// <summary>The option: on under the living economy's master switch.</summary>
        public static bool On => SupplierRules.On(Settings.LivingPopulation, Settings.LivingSuppliers);

        /// <summary>True while a save is read, the option is on and nothing has failed.</summary>
        public bool Running => SupplierRules.Running(_loaded, _broken, Settings.LivingPopulation, Settings.LivingSuppliers);

        /// <summary>The state in use. Read it only while <see cref="Running"/>.</summary>
        public SupplierState State => _state;

        /// <summary>Moves whenever something a page shows may have changed.</summary>
        public int Revision => _revision;

        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            GameSprites.Want(AppGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
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
                    _broken = true;
                    Logger?.Error(e);
                    Logger?.Warn("Suppliers: switched off for this session after the error above. The save keeps every contract and what is owed as they were; nothing more is "
                                 + "delivered or charged until the game is started again.");
                }
            }
        }

        private void Look()
        {
            var game = InstanceBehavior<GameManager>.Instance;
            if (game == null || game.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded || !SupplierGame.Ready)
            {
                return;
            }
            if (!On)
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
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            int day = SupplierGame.Today, hour = SupplierGame.HourNow;
            if (SupplierRules.DayDue(_state.LastDay, day))
            {
                CatchUp(day);
                DayPass(day);
            }
            if (SupplierRules.HourDue(_hourDay, _hour, day, hour))
            {
                _hourDay = day;
                _hour = hour;
                using var hotSpot = HotSpots.Time(HotHour);
                SupplierEngine.Hour(_state, _world);
                Flush();
            }
            RivalsSecond(day, hour);
        }

        private void Load()
        {
            GameInstance save = SaveGameManager.Current;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            _state = SupplierStore.Load(save.modData, out int version) ?? new SupplierState();
            if (_state.Seed == 0)
            {
                _state.Seed = UnityEngine.Random.Range(1, int.MaxValue);   // once a save, then kept: every draw of the feature is a hash of it
            }
            _world = new SupplierGame(_state);
            SupplierGame.Forget();
            Depots();
            int day = SupplierGame.Today;
            int moved = SupplierEngine.Resume(_state, day);   // a load or the option back on: a delivery whose day passed meanwhile is neither brought nor charged
            if (version == 0)
            {
                _state.LastDay = day;   // a save from before the feature: nothing in it, and its first day is counted from tomorrow
            }
            else if (_state.LastDay < day - 1)
            {
                _state.LastDay = day - 1;   // the option was off, or the save is from long ago: a pause, not days to play (Resume put the contracts on at their next delivery day)
            }
            _hourDay = -1;
            _hour = -1;
            RivalsStop();
            _beforeTests = save.modData.TryGetValue(TestKey, out string before) && !string.IsNullOrEmpty(before) ? before : null;
            _loaded = true;
            _revision++;
            Logger?.Info("Suppliers: " + (version > 0 ? "read the save's line (version " + version + "): " + _state.RunningCount() + " contract(s) running, "
                         + SupplierGame.Money(_state.OwedAll()) + " owed" + (moved > 0 ? ", " + moved + " moved on to their next delivery day" : string.Empty)
                         : "no contracts yet") + ".");
        }

        // Each supplier's depot stands by one of the city's own wholesale stores or importers: a seeded pick, made once and kept, and made again if that building is gone.
        private void Depots()
        {
            SupplierGame.Supplies(_supplies);
            for (int i = 0; i < _state.Firms.Length && _supplies.Count > 0; i++)
            {
                FirmState f = _state.Firms[i];
                if (string.IsNullOrEmpty(f.Depot) || !_supplies.Contains(f.Depot))
                {
                    f.Depot = _supplies[SupplierRules.Roll(_state.Seed, SupplierCatalogue.Firms[i].Id, 0, SupplierRules.PurposeDepot, _supplies.Count)];
                }
            }
            _supplies.Clear();
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
            RivalsFinish();   // the rival shops' day, if one is under way, is whole in the save: none is gone through twice after a load
            save.modData[SupplierStore.Key] = SupplierStore.Write(_state);
            if (_beforeTests != null)
            {
                save.modData[TestKey] = _beforeTests;
            }
            else
            {
                save.modData.Remove(TestKey);
            }
        }

        // The option was switched off: the line stays as it is, nothing is delivered or charged while it is off, and the app leaves the phone. Stock already
        // delivered is the game's own from the moment it came; the game's own delivery contracts were never touched.
        private void PutAway()
        {
            Write();
            _loaded = false;
            RivalsStop();
            RemoveApp();
            _revision++;
            Logger?.Info("Suppliers: switched off; the contracts and what is owed are kept in the save and go on, each at its next delivery day, when it is on again.");
        }

        /// <summary>Forgets everything of this save. The next look opens the feature afresh.</summary>
        public void Forget()
        {
            GameInstance save = SaveGameManager.Current;
            if (save != null && save.modData != null)
            {
                save.modData.Remove(SupplierStore.Key);
                save.modData.Remove(TestKey);
            }
            _state = new SupplierState();
            _world = new SupplierGame(_state);
            _beforeTests = null;
            _loaded = false;
            RivalsStop();
            RemoveApp();
            _revision++;
        }

        // ---- the day

        // A time skip of several days (a long sleep, a journey): each day that went by is played by the engine (SupplierEngine.CatchUp), so how the player slept does not
        // change what happened.
        private void CatchUp(int day)
        {
            int played = SupplierEngine.CatchUp(_state, _world, day);
            if (played == 0)
            {
                return;
            }
            Flush();
            if (Verbose)
            {
                Logger?.Info("Suppliers check (time skip): " + played + " elapsed day(s) played one by one up to day " + day + ": PASS");
            }
        }

        private void DayPass(int day)
        {
            using var hotSpot = HotSpots.Time(HotDay);
            int owed = _state.Owed.Count;
            SupplierEngine.Day(_state, _world);
            if (Verbose)
            {
                Logger?.Info("Suppliers check (day " + day + "): " + _state.RunningCount() + " contract(s) running, " + owed + " line(s) owed before and " + _state.Owed.Count
                             + " after, " + _state.Events.Count + " thing(s) to say: PASS");
            }
            Flush();
        }

        // What the engine did, said in the game's own notices and written to the log, once each.
        private void Flush()
        {
            List<SupplyEvent> events = _state.Events;
            if (events.Count == 0)
            {
                return;
            }
            for (int i = 0; i < events.Count; i++)
            {
                SupplyEvent e = events[i];
                string key = SupplierText.Notice(e, out Dictionary<string, string> data, out bool warn);
                if (key != null)
                {
                    SupplierGame.Tell(warn ? NotificationType.Warning : NotificationType.Info, key, data, OpenContracts);
                }
                Logger?.Info("Suppliers: " + e.Kind + (e.Contract > 0 ? ", contract " + e.Contract : string.Empty) + (e.Firm > 0 ? ", supplier " + e.Firm : string.Empty)
                             + (string.IsNullOrEmpty(e.Place) ? string.Empty : ", at " + e.Place) + " (" + e.A + ", " + e.B + ", " + e.C + ").");
            }
            events.Clear();
            _revision++;
        }

        private static readonly Action OpenContracts = () => OpenApp();

        // ---- what the player presses (the pages' calls)

        /// <summary>Signs what a supplier offers a place; null, with the reason, when it offers nothing. Nothing is paid at signing.</summary>
        public Contract Sign(int supplier, string place, int weeks, int shareStep, out Refusal why)
        {
            why = Refusal.Off;
            if (!Running)
            {
                return null;
            }
            Contract c = SupplierEngine.Sign(_state, _world, supplier, place, SupplierGame.NameOf(SupplierGame.Reg(place)), weeks, shareStep, out why);
            Flush();
            _revision++;
            return c;
        }

        /// <summary>Ends a contract early: the settlement is due.</summary>
        public bool End(int contract)
        {
            bool done = Running && SupplierEngine.Break(_state, _world, contract);
            Flush();
            return done;
        }

        /// <summary>Leaves a contract by right after the supplier's own failures.</summary>
        public bool Leave(int contract)
        {
            bool done = Running && SupplierEngine.Leave(_state, _world, contract);
            Flush();
            return done;
        }

        /// <summary>Pays what is owed to suppliers as far as the cash goes. Returns what was paid, cents.</summary>
        public long PayOwed()
        {
            long paid = Running ? SupplierEngine.Collect(_state, _world) : 0L;
            Flush();
            _revision++;
            return paid;
        }

        /// <summary>Moves one of a contract's rules for its buyer to its next value: what to do at the term's end, the price limit, covering, leaving by right.</summary>
        public void Rule(int contract, int rule)
        {
            Contract c = Running ? _state.ContractOf(contract) : null;
            if (c == null || !c.Running)
            {
                return;
            }
            switch (rule)
            {
                case SupplierCatalogue.RuleRenew: c.Renew = (RenewRule)(((int)c.Renew + 1) % 3); break;
                case SupplierCatalogue.RuleLimit: c.RenewLimit = (c.RenewLimit + 1) % SupplierCatalogue.RenewLimits.Length; break;
                case SupplierCatalogue.RuleCover: c.Cover = !c.Cover; break;
                case SupplierCatalogue.RuleLeave: c.LeaveRule = !c.LeaveRule; break;
            }
            _revision++;
        }

        // ---- what the other features ask (Wiring.SupplierLinks)

        /// <summary>The day a contract next brings a product to a business; false when none does.</summary>
        public bool NextDelivery(BuildingRegistration reg, string item, out int day)
        {
            day = -1;
            if (!Running || reg == null || reg.Address == null || string.IsNullOrEmpty(item))
            {
                return false;
            }
            string place = SupplierGame.Key(reg.Address);
            for (int i = 0; i < _state.Contracts.Count; i++)
            {
                Contract c = _state.Contracts[i];
                if (!c.Running || c.Place != place)
                {
                    continue;
                }
                for (int k = 0; k < c.Lines.Count; k++)
                {
                    if (c.Lines[k].Item == item && (day < 0 || c.Due + c.Slip < day))
                    {
                        day = c.Due + c.Slip;
                    }
                }
            }
            return day >= 0;
        }

        /// <summary>Units the contracts honestly expect to bring a business of a product from a day to another, both counted, each once: a manager's order is smaller by them.</summary>
        public int Coming(BuildingRegistration reg, string item, int fromDay, int toDay)
        {
            return Running && reg != null && reg.Address != null
                ? _state.Coming(SupplierGame.Key(reg.Address), item, fromDay, toDay, SupplierGame.BackorderOf(item), SupplierGame.Spare) : 0;
        }

        /// <summary>Units the contracts brought a business of a product since a day and hour: a manager counts them as come when working out what was used.</summary>
        public int BroughtSince(BuildingRegistration reg, string item, int day, int hour)
        {
            return Running && reg != null && reg.Address != null ? _state.Brought(SupplierGame.Key(reg.Address), item, day, hour) : 0;
        }

        /// <summary>The contracts running at a business and the day of the next delivery; 0 for none.</summary>
        public int At(BuildingRegistration reg, out int nextDay)
        {
            nextDay = -1;
            if (!Running || reg == null || reg.Address == null)
            {
                return 0;
            }
            string place = SupplierGame.Key(reg.Address);
            int n = 0;
            for (int i = 0; i < _state.Contracts.Count; i++)
            {
                Contract c = _state.Contracts[i];
                if (c.Running && c.Place == place)
                {
                    n++;
                    nextDay = nextDay < 0 ? c.Due + c.Slip : Math.Min(nextDay, c.Due + c.Slip);
                }
            }
            return n;
        }

        /// <summary>A rival tycoon's line for the rich list and their panel; empty for nothing to say.</summary>
        public string RivalLine(int tycoon)
        {
            if (!Running || tycoon < 0 || tycoon >= _state.RivalBooks.Length || SupplierBridges.RivalName == null)
            {
                return string.Empty;
            }
            return SupplierText.RivalLine(SupplierBridges.RivalName(tycoon), _state.RivalBooks[tycoon]);
        }

        /// <summary>Yesterday's news of the suppliers for the morning's paper.</summary>
        public void Stories(int day, List<SupplyStory> into)
        {
            into.Clear();
            for (int i = 0; Running && i < _state.Notes.Count && into.Count < 2; i++)
            {
                if (_state.Notes[i].Day == day - 1)
                {
                    into.Add(SupplierText.Story(_state.Notes[i]));
                }
            }
        }
    }
}
