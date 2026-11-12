using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>
    /// Insurance the player buys (docs/features/insurance.md): a tier for each car's policy at the Garage with the driver's record priced in, a policy for each home,
    /// and one app for those and for Health's plan and the Company's business cover. It owns the state and its timing. A second at a time it looks whether a save is
    /// ready (and loads or lets go of its line); a game day it goes through once (the homes' premiums, the days of grace, the rivals' cover). A claim is worked out when
    /// another feature is about to charge for a covered loss and asks what the policy pays. Money moves only through <see cref="InsuranceGame"/> and the features whose
    /// bills they are. No Update. COST: nothing per frame; a few comparisons a second, a pass over the player's cars and homes once a day.
    /// </summary>
    public sealed partial class InsuranceDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static InsuranceDirector Instance { get; private set; }
        public const string TestKey = "reallife:insurance_test";

        private static readonly int HotDay = HotSpots.Slot("Insurance: the day's pass");

        private InsuranceState _state = new InsuranceState();
        private bool _loaded, _broken;
        private Action _onSave;
        private int _revision;
        private string _beforeTests;

        public static void Attach(GameObject root, IModLogger logger)
        {
            Logger = logger;
            root.AddComponent<InsuranceDirector>();
        }

        /// <summary>True while a save is read, the option is on and nothing has failed.</summary>
        public bool Running => _loaded && !_broken && Settings.Insurance;

        /// <summary>The state in use. Read it only while <see cref="Running"/>.</summary>
        public InsuranceState State => _state;

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
                    Logger?.Warn("Insurance: switched off for this session after the error above. The save keeps every policy as it was, and the Garage's policies go on by its own terms.");
                }
            }
        }

        private void Look()
        {
            var game = InstanceBehavior<GameManager>.Instance;
            if (game == null || game.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (!Settings.Insurance)
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
            int day = InsuranceGame.Day;
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
            _state = InsuranceStore.Load(save.modData, out int version) ?? new InsuranceState();
            int day = InsuranceGame.Day;
            for (int i = 0; i < _state.Homes.Count; i++)
            {
                HomePolicy p = _state.Homes[i];
                if (p.Active && p.NextDue < day)
                {
                    p.NextDue = day;   // a load or the option back on: one premium is due, no months of the time away
                }
            }
            _state.LastDay = 0;
            _beforeTests = save.modData.TryGetValue(TestKey, out string before) && !string.IsNullOrEmpty(before) ? before : null;
            _loaded = true;
            _revision++;
            Logger?.Info("Insurance: " + (version > 0 ? "read the save's line (version " + version + "): " + _state.Cars.Count + " car polic" + (_state.Cars.Count == 1 ? "y" : "ies") + ", "
                         + _state.Homes.Count + " home polic" + (_state.Homes.Count == 1 ? "y" : "ies") + ", " + _state.Claims.Count + " claim(s) in the history" : "no policy of its own yet") + ".");
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
            save.modData[InsuranceStore.Key] = InsuranceStore.Write(_state);   // (a line that could not be read is put back as it was after this: Core/SaveLines)
            if (_beforeTests != null)
            {
                save.modData[TestKey] = _beforeTests;
            }
            else
            {
                save.modData.Remove(TestKey);
            }
        }

        // The option was switched off: the line stays as it is, nothing runs, no premium of a home is taken and no claim is paid by a tier (the Garage's policies go on
        // by the Garage's own terms), and the app leaves the phone.
        private void PutAway()
        {
            Write();
            _loaded = false;
            RemoveApp();
            _revision++;
            Logger?.Info("Insurance: switched off; the policies are kept in the save and go on when it is on again. Meanwhile a car's policy is the Garage's own.");
        }

        /// <summary>Forgets everything of this save. The next look opens the feature afresh.</summary>
        public void Forget()
        {
            GameInstance save = SaveGameManager.Current;
            if (save != null && save.modData != null)
            {
                save.modData.Remove(InsuranceStore.Key);
                save.modData.Remove(TestKey);
            }
            _state = new InsuranceState();
            _beforeTests = null;
            _loaded = false;
            RemoveApp();
            _revision++;
        }

        // ---- the day

        private void Pass(int day)
        {
            using var hotSpot = HotSpots.Time(HotDay);
            _state.LastDay = day;
            SyncCars(day);
            SyncHomes(day);
            HomeDay(day);
            RivalDay(day);
            _revision++;
        }
    }
}
