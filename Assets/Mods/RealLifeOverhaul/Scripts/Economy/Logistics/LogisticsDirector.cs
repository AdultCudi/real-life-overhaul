using System;
using System.Collections.Generic;
using BAModAPI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Delivery trips and food spoilage. Trips: the player's logistics routes are run by the mod, a vehicle and its driver from the
    /// route's warehouse at each of the route's departure times, round its stops and back, the goods moved on arrival by the
    /// game's own delivery call; the game's nightly run of the same routes is held so it moves nothing. Spoilage: the goods that
    /// spoil at each of the player's places are kept as batches by the day they came in, sold and moved oldest first, and what
    /// is past its life at the turn of a day is thrown out by the game's own calls. The city's rival shops pay the same supply
    /// and spoilage in their books. No Update, so no cost a frame: it follows the credit side's second and hour, a slice of its
    /// work each second.
    /// </summary>
    public sealed partial class LogisticsDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static LogisticsDirector Instance { get; private set; }

        private TripState _trips;
        private SpoilState _spoil;
        private bool _running, _broken;
        private bool _tripsOn, _spoilOn;
        private int _held;                 // routes held from the game's run of this hour
        private int _changes;              // counted up whenever what the pages show changes: a trip leaves, stops or is back, times set, goods spoil
        private Action _second, _onSave, _onHour;
        private Action<string> _onEvent, _eventSeen;

        public bool Running => _running;

        /// <summary>Counted up whenever what BizMan's pages show of the trips or spoilage changes, so a page lays itself again only then.</summary>
        public int Changes => _changes;

        /// <summary>Whether the routes run as trips now: the option on and the feature running.</summary>
        public bool TripsOn => _running && _tripsOn;

        /// <summary>Whether goods spoil now.</summary>
        public bool SpoilOn => _running && _spoilOn;

        public static bool TripsWanted => Settings.Economy && Settings.LivingPopulation && Settings.LivingTrips;

        public static bool SpoilWanted => Settings.Economy && Settings.LivingPopulation && Settings.LivingSpoilage;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            _second = EachSecond;
            _onSave = OnSave;
            _onHour = OnHour;
            _onEvent = Guard.Wrap<string>(OnGameEvent);
            EconomyDirector.Follow(StartSeats.Company, _second);   // after the company's own second, in its seat
            EconomyDirector.AfterHour += _onHour;
        }

        private void OnDestroy()
        {
            EconomyDirector.Unfollow(_second);
            EconomyDirector.AfterHour -= _onHour;
            SaveHook.Drop(_onSave);
            GameEvent.onGameEventTriggered -= _onEvent;
            if (Instance != this)
            {
                return;
            }
            try
            {
                StandDown("the city is unloading");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _running = false;
            }
            Instance = null;
        }

        private static readonly int HotSecond = HotSpots.Slot("Economy: LogisticsDirector.Second");
        private void EachSecond()
        {
            using var hotSpot = HotSpots.Time(HotSecond);
            if (_broken)
            {
                return;
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

        private void Slow()
        {
            if (LogisticsGame.Holding)
            {
                Released("the second after the hour");   // the game's own end of the hour did not come: never held past it
            }
            if (Save == null || LoadScene.isLoading || !ModState.Loaded || !LogisticsGame.Ready)
            {
                return;
            }
            EconomyDirector credit = EconomyDirector.Instance;
            bool parts = !GameContract.TripsPart.Off, trips = parts && TripsWanted, spoil = parts && SpoilWanted;
            if ((!trips && !spoil) || credit == null || !credit.Running)
            {
                if (_running)
                {
                    StandDown(!trips && !spoil ? "the options are off" : "the credit side is not running");
                }
                return;
            }
            if (!_running)
            {
                StartUp();
            }
            if (_tripsOn != trips || _spoilOn != spoil)
            {
                Logger?.Info("Delivery trips: " + (trips ? "on" : "off") + "; food spoilage: " + (spoil ? "on" : "off") + ".");
            }
            _tripsOn = trips;
            _spoilOn = spoil;
            SaveHook.Keep(_onSave);
            EventHold.Keep(ref GameEvent.onGameEventTriggered, _onEvent, ref _eventSeen);
            int now = LogisticsGame.Now;
            if (_tripsOn)
            {
                TripsSecond(now);
            }
            if (_spoilOn)
            {
                SpoilSecond(LogisticsGame.Day);
            }
            RivalsSecond(LogisticsGame.Day);
        }

        private void StartUp()
        {
            GameInstance save = Save;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            _trips = Load(save.modData, TripStore.Key, TripStore.Read) ?? new TripState();
            _spoil = Load(save.modData, SpoilStore.Key, SpoilStore.Read) ?? new SpoilState();
            _running = true;
            _tripsOn = TripsWanted;
            _spoilOn = SpoilWanted;
            LogisticsGame.Forget();
            TripsStart(LogisticsGame.Now);
            SpoilStart();
            Logger?.Info("Delivery trips and food spoilage: started with " + _trips.Routes.Count + " route(s) kept, " + _trips.Trips.Count + " trip(s) on the road or waiting, "
                         + _trips.Log.Count + " run(s) in the week's log and " + _spoil.Places.Count + " place(s) with goods that spoil.");
        }

        private static T Load<T>(Dictionary<string, string> data, string key, Func<string, T> read) where T : class
        {
            string text = data.TryGetValue(key, out string t) ? t : null;
            T state = read(text);
            if (state == null)
            {
                SaveLines.Unreadable(data, key, "its version is not one this version of the mod reads");
            }
            return state;
        }

        private void StandDown(string why)
        {
            bool was = _running;
            Released("standing down");
            if (_running && Save != null && Save.modData != null)
            {
                Write(Save.modData);
            }
            _running = false;
            _tripsOn = _spoilOn = false;
            _trips = null;
            _spoil = null;
            TripsStop();
            SpoilStop();
            RivalsStop();
            LogisticsGame.Forget();
            if (was)
            {
                Logger?.Info("Delivery trips and food spoilage: stood down (" + why + "); the game's own logistics run every route again.");
            }
        }

        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            try
            {
                StandDown("a fault");
            }
            catch (Exception second)
            {
                Logger?.Error(second);
                _running = false;
                LogisticsGame.Release();
            }
            Logger?.Warn("Delivery trips and food spoilage: switched off for this session after an error in " + where
                         + ". The game's own logistics run every route; trips on the road and the goods' ages are kept as they were last saved.");
        }

        private void OnSave()
        {
            if (!_running || Save == null)
            {
                return;
            }
            Released("the save");
            if (Save.modData == null)
            {
                Save.modData = new Dictionary<string, string>();
            }
            Write(Save.modData);
        }

        private void Write(Dictionary<string, string> data)
        {
            if (_trips != null && !SaveLines.IsKept(data, TripStore.Key))
            {
                TripRules.Trim(_trips.Log, LogisticsGame.Day);
                data[TripStore.Key] = TripStore.Write(_trips);
            }
            if (_spoil != null && !SaveLines.IsKept(data, SpoilStore.Key))
            {
                data[SpoilStore.Key] = SpoilStore.Write(_spoil);
            }
        }

        // ------------------------------------------------------------------------------------------------- the hold

        // The economy's hour, inside the game's own hour and before its run of every route at 02:00 (GameManager.RunMainGameTick):
        // the routes run as trips are held, so that run finds no stop; the game's own end of the hour puts them back.
        private static readonly int HotHour = HotSpots.Slot("Economy: LogisticsDirector.Hour");
        private void OnHour()
        {
            using var hotSpot = HotSpots.Time(HotHour);
            if (!TripsOn || _broken || Save == null || Save.Hour != TripRules.GameRunHour)
            {
                return;
            }
            try
            {
                _held = LogisticsGame.Hold(TakenOver);
            }
            catch (Exception e)
            {
                LogisticsGame.Release();
                Fault("the hold of the game's run", e);
            }
        }

        private static bool TakenOver(Buildings.Office.Headquarters.LogisticsManagerPlan plan) => LogisticsGame.From(plan) != null;

        private void OnGameEvent(string gameEvent)
        {
            if (gameEvent == "ba:gameevent_newhour" && LogisticsGame.Holding)
            {
                Released("the end of the game's hour");
            }
        }

        private void Released(string when)
        {
            int n = LogisticsGame.Release();
            if (n > 0 && (Verbose || when != "the end of the game's hour"))
            {
                Logger?.Info("Trips check (the night's run): " + n + " route(s) held from the game's own run and put back at " + when + ": "
                             + (n == _held ? "PASS" : "FAIL"));
            }
            _held = 0;
        }
    }
}
