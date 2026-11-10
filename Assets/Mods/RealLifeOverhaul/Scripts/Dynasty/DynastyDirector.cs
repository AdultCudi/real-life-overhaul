using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>
    /// The dynasty: a will, estate tax, retiring and an heir who carries on (docs/features/dynasty.md). It owns the state and its timing. A second at a time it looks whether a
    /// save is ready (and loads or lets go of its line), goes on with a handover under way a step at a time, and compares the played person's age with their last days; a game
    /// day it goes through once (the gifts' window, the debt for tax, the retired founder, the rivals' lines, the city's estates). Money moves only through
    /// <see cref="DynastyGame"/>, which books it through the mod's one call. No Update. COST: nothing per frame; a few comparisons a second, a pass over three tycoons a day.
    /// </summary>
    public sealed partial class DynastyDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static DynastyDirector Instance { get; private set; }

        private static readonly int HotDay = HotSpots.Slot("Dynasty: the day's pass");

        private DynastyState _state;
        private bool _loaded, _broken;
        private Action _onSave;
        private int _revision;
        private readonly List<KinPerson> _people = new List<KinPerson>(24);
        private readonly List<string> _names = new List<string>(4);

        public static void Attach(GameObject root, IModLogger logger)
        {
            Logger = logger;
            DynastyGame.Logger = logger;
            root.AddComponent<DynastyDirector>();
        }

        /// <summary>True while a save is read, the option is on and nothing has failed.</summary>
        public bool Running => _loaded && !_broken && Settings.Dynasty;

        /// <summary>The state in use. Read it only while <see cref="Running"/>.</summary>
        public DynastyState State => _state;

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
                    Logger?.Warn("Dynasty: switched off for this session after the error above. The save keeps the will, the statements and a handover under way as they were.");
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
            if (!Settings.Dynasty)
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
            if (_state.Hand != null)
            {
                HandStep();   // a handover the save, the load or the option off left in the middle goes on at its step
                return;
            }
            int day = DynastyGame.Day;
            if (day != _state.LastDay)
            {
                Pass(day);
            }
            LastDays(day);
        }

        private void Load()
        {
            GameInstance save = SaveGameManager.Current;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            _state = DynastyStore.Load(save.modData, out int version) ?? new DynastyState();
            if (_state.Seed == 0)
            {
                _state.Seed = UnityEngine.Random.Range(1, int.MaxValue);   // once a save, then kept: every rival's age comes from it
            }
            _state.LastDay = 0;
            _draft = null;
            _loaded = true;
            _revision++;
            Logger?.Info("Dynasty: " + (version > 0 ? "read the save's line (version " + version + "): generation " + _state.Generation + ", " + (_state.Will.Written ? "a will" : "no will")
                         + (_state.Hand != null ? ", a handover under way at step " + _state.Hand.Step : string.Empty) : "no will yet") + ".");
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
            save.modData[DynastyStore.Key] = DynastyStore.Write(_state);
        }

        // The option was switched off: the line stays as it is (the will written, a handover under way with its step), nothing runs and the app leaves the phone. A will
        // takes effect only while the option is on: with it off the game's own end in old age comes as it always did.
        private void PutAway()
        {
            Write();
            _loaded = false;
            _draft = null;
            RemoveApp();
            _revision++;
            Logger?.Info("Dynasty: switched off; the will and the statements are kept in the save and count again when it is on.");
        }

        /// <summary>Forgets everything of this save. The next look opens the feature afresh.</summary>
        public void Forget()
        {
            GameInstance save = SaveGameManager.Current;
            if (save != null && save.modData != null)
            {
                save.modData.Remove(DynastyStore.Key);
            }
            _state = new DynastyState();
            _draft = null;
            _loaded = false;
            RemoveApp();
            _revision++;
        }

        // ---- the day

        private void Pass(int day)
        {
            using var hotSpot = HotSpots.Time(HotDay);
            _state.LastDay = day;
            int year = DynastyGame.DaysPerYear;
            DynastyRules.ForgetOld(_state.Gifts, day, year);
            SeeGrown(day);
            LoanDays(day, year);
            ElderDay(day, year);
            RetireNote(day, year);
            DynastyCity.Turn(_state.City, day / year);
            RivalsDay(day, year);
            ExpireNotes(day);
            _revision++;
        }

        // A child seen grown for the first time: family life stops counting their years at eighteen, so the day is kept and their age goes on from it.
        private void SeeGrown(int day)
        {
            if (DynastyBridges.GrownChildren == null)
            {
                return;
            }
            _names.Clear();
            DynastyBridges.GrownChildren(_names);
            for (int i = 0; i < _names.Count; i++)
            {
                if (_state.GrownOf(_names[i]) == null)
                {
                    _state.Grown.Add(new GrownChild { Name = _names[i], Day = day });
                }
            }
            for (int i = _state.Grown.Count - 1; i >= 0; i--)
            {
                if (!_names.Contains(_state.Grown[i].Name))
                {
                    _state.Grown.RemoveAt(i);   // no longer a child of the family (the heir who took over, or the family started afresh)
                }
            }
        }

        /// <summary>The day a child was first seen grown; today for one not yet on the list.</summary>
        internal int GrownDay(string name)
        {
            GrownChild g = _loaded ? _state.GrownOf(name) : null;
            return g != null ? g.Day : DynastyGame.Day;
        }

        // The founder has reached the age their will sets: said once for that age.
        private void RetireNote(int day, int year)
        {
            Will w = _state.Will;
            int age = DynastyGame.AgeYears;
            if (w.Written && age >= w.RetireAge && _state.RetireTold < w.RetireAge && _state.Elder == null)
            {
                _state.RetireTold = w.RetireAge;
                DynastyGame.Tell(NotificationType.Info, "reallife_dynasty_retire_ready", DynastyGame.Data("age", age.ToString()), OpenEstate);
            }
        }

        private void ExpireNotes(int day)
        {
            for (int i = _state.Notes.Count - 1; i >= 0; i--)
            {
                if (day - _state.Notes[i].Day > DynastyCatalogue.PaperKept)
                {
                    _state.Notes.RemoveAt(i);
                }
            }
        }

        /// <summary>A gift of money to a person in the life of the founder (Giving tells it, and a loan it wrote off): dated, it counts in the estate inside the window.</summary>
        public void Gave(long amount)
        {
            GameInstance save = SaveGameManager.Current;
            if (amount <= 0 || save == null)
            {
                return;
            }
            if (!_loaded)
            {
                // The option is off or the line is not read yet: the gift is dated in the save's line all the same, so switching the option off is no way round the window.
                if (save.modData == null)
                {
                    save.modData = new Dictionary<string, string>();
                }
                DynastyState kept = DynastyStore.Load(save.modData, out int _) ?? (save.modData.ContainsKey(DynastyStore.Key) ? null : new DynastyState());
                if (kept != null)
                {
                    DynastyRules.Gift(kept.Gifts, DynastyGame.Day, amount);
                    save.modData[DynastyStore.Key] = DynastyStore.Write(kept);
                }
                return;
            }
            DynastyRules.Gift(_state.Gifts, DynastyGame.Day, amount);
            _revision++;
        }
    }
}
