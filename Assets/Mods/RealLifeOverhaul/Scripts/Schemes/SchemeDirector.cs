using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Schemes
{
    /// <summary>
    /// Crime that pays, until it does not: white collar schemes that sit where the honest action sits (an invented
    /// insurance claim, an invented expense on the return, dressed books before a sale, a club that does not exist, cash
    /// through a till, money to people with power), each leaving a trail that inquiries follow into the courts, with the
    /// rival tycoons doing the same by the same rules. The rules are game-free (<see cref="SchemeRules"/>); this owns the
    /// state, the day and the save, and its parts are the day's events (Day), what a press does (Acts), the people
    /// (People), the pages (Pages) and the tests (Tests). Under the master option Living population and its own switch:
    /// off, no new scheme can start, while what is open still runs its course and nothing is forgotten. The cost: no
    /// Update; a one-second timer on the real clock comparing the day; a game day's turn reads the player's businesses
    /// once and plays the rules; everything else runs on a press.
    /// </summary>
    public sealed partial class SchemeDirector : MonoBehaviour, ISchemeActs
    {
        private static readonly int HotTick = HotSpots.Slot("Schemes: SchemeDirector.Tick");

        public static IModLogger Logger;
        public static SchemeDirector Instance { get; private set; }

        private readonly SchemeWorld _world = new SchemeWorld();
        private readonly List<SchemeEvent> _events = new List<SchemeEvent>(16);
        private readonly List<SchemeShopInfo> _shops = new List<SchemeShopInfo>(16);
        private SchemeState _state;
        private SchemePanel _panel;
        private GameInstance _save;
        private Action _onSave;
        private bool _broken;
        private int _day = -1;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        /// <summary>True while new schemes may start: the master option and the schemes' own on, a save loaded, and no fault has stopped them.</summary>
        public static bool On => Settings.LivingPopulation && Settings.LivingSchemes && Instance != null && !Instance._broken && Instance._state != null;

        /// <summary>The save's state while the director runs, or null: read by the wiring (the paper, the Legal file) and never written there.</summary>
        public SchemeState State => _broken ? null : _state;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            _panel = new SchemePanel(Press, e => Logger?.Error(e));
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                _panel?.Dispose();
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
            var second = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return second;
                if (_broken)
                {
                    continue;
                }
                try
                {
                    Slow();
                }
                catch (Exception e) when (Guard.StandsDown(e, "the schemes"))
                {
                    Stop();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    Logger?.Warn("Schemes: off for this session after the error above; what the save kept is saved as it was.");
                    Stop();
                }
            }
        }

        // A fault: nothing more is run, the page is closed, and the save keeps the line as last written.
        private void Stop()
        {
            _broken = true;
            try
            {
                _panel?.Close();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);
            GameInstance save = Save;
            if (save == null || LoadScene.isLoading || GameManager.isCitySceneBeingUnloaded || Meta.SaveTools.Dormant || !ModState.Loaded)
            {
                return;
            }
            if (!ReferenceEquals(save, _save))
            {
                _panel?.Close();
                _save = save;
                Load(save);
            }
            SaveHook.Keep(_onSave);   // the line is written while off too, so switching off forgets nothing
            if (_state == null)
            {
                return;   // a line this version cannot read: kept as it was, and nothing runs for this save
            }
            int day = save.Day;
            if (day == _day)
            {
                return;
            }
            _day = day;
            if (Settings.LivingPopulation && Settings.LivingSchemes || _state.CarriesOn)
            {
                DayTurn(day);
            }
        }

        private void Load(GameInstance save)
        {
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            bool had = save.modData.ContainsKey(SchemeStore.Key);
            _state = SchemeStore.Load(save.modData, SchemeRules.SeedOf(SchemeGame.SeedText), out int version);
            _day = -1;
            if (_state == null)
            {
                Logger?.Warn("Schemes: the save's line (" + SchemeStore.Key + ") could not be read; it is kept in the save as it is, and nothing of the schemes runs for this save.");
                return;
            }
            if (had || Verbose)
            {
                Logger?.Info("Schemes: " + (had ? "loaded (version " + version + ")" : "opened for this save") + ": " + _state.Acts.Count + " act(s) on file, "
                             + _state.Cases.Count + " inquiry(ies), cash in hand " + _state.Dirty + ", seed " + _state.Seed + ".");
            }
        }

        // Inside the game's save event: the line, unless nothing was ever done and the save had none.
        private void OnSave()
        {
            GameInstance save = Save;
            if (save == null || save.modData == null || _state == null || !ReferenceEquals(save, _save) || SaveLines.IsKept(save.modData, SchemeStore.Key))
            {
                return;
            }
            if (Blank(_state) && !save.modData.ContainsKey(SchemeStore.Key))
            {
                return;
            }
            SchemeStore.Save(save.modData, _state);
        }

        // Nothing the save would miss: no act, no inquiry, no money, nobody paid, no setting, and the rivals as they began.
        private static bool Blank(SchemeState s)
        {
            if (s.Acts.Count > 0 || s.Cases.Count > 0 || s.Paid.Count > 0 || s.Cold.Count > 0 || s.Bought.Count > 0 || s.Shops.Count > 0 || s.Dirty > 0
                || s.Fund.State != FundState.None || s.Fund.Number > 0)
            {
                return false;
            }
            for (int i = 0; i < s.Rival.Length; i++)
            {
                if (s.Rival[i].Acts > 0 || s.Rival[i].Opened > 0 || s.Rival[i].Heat > 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>The page is closed and nothing is held (the options' clean-up).</summary>
        public void Release()
        {
            _panel?.Close();
        }

        /// <summary>The feature's data is reset (Meta/SaveTools): what the save kept is forgotten.</summary>
        public void Forget()
        {
            _panel?.Close();
            _state = new SchemeState { Seed = SchemeRules.SeedOf(SchemeGame.SeedText) };
            _day = -1;
            GameInstance save = Save;
            save?.modData?.Remove(SchemeStore.Key);
        }

        // A press came in on a broken or unloaded director: said once in the log, and nothing is done.
        private bool Ready(string what)
        {
            if (_broken || _state == null || Save == null)
            {
                Logger?.Info("Schemes: " + what + " was asked with nothing loaded; nothing was done.");
                return false;
            }
            return true;
        }
    }
}
