using System;
using BAModAPI;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// Family life's one component. Cost: a few comparisons a frame, a tick a second (five in a scene), the rest by the hour
    /// and day. A fault in a tick switches family life off for the session; the save is left as it was.
    /// </summary>
    public sealed partial class FamilyDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("family", "FamilyDirector", typeof(FamilyDirector));

        public static IModLogger Logger;
        public static FamilyDirector Instance { get; private set; }

        /// <summary>True while one of family life's key prompts is on screen: the mod's own E/F stands aside.</summary>
        public static bool TakesInteractKey => Instance != null && Instance._door != null && Instance._door.TakesKey;

        internal FamilyCast Cast;
        internal FamilyProps Props;
        internal JobBody Body;
        internal FamilyScene Scene;
        internal bool Busy;                 // a sequence (a fade, a skip of time) is running

        private JobProps _bodyProps;
        private float _nextTick;
        private float _nextHook;
        private Action _onSave;
        private int _lastHour = -1;
        private bool _started;
        private bool _failed;
        private bool _social;               // whether Social supplied the partner when the phone contacts were last set up

        private void Awake()
        {
            Instance = this;
            _door = new DoorPanel(Logger, "Family check (prompt panel)");
            _onPrompt = _door.Press(PromptPressed);
            _promptWhat = PromptWhat;
            FamilyState.Logger = Logger;
            FamilyState.Reset();
            FamilyPlaces.Reset();
            FamilyModifiers.Register();   // before Core/ModState puts back the moods a save carried
            Cast = new FamilyCast(transform, Logger);
            Props = new FamilyProps(transform);
            _bodyProps = new JobProps(transform);
            Body = new JobBody(_bodyProps, Logger);
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            Stop(true);
            try
            {
                Cast.Dispose();
                _bodyProps.Dispose();
                _door.Hide("the director went with the city");
                _card?.Destroy();
                _card = null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            FamilyState.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Family: FamilyDirector.Update");
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
            if (_failed)
            {
                return;
            }
            if (!Settings.Family)
            {
                if (_started)
                {
                    Stop(false);
                }
                return;
            }
            var game = InstanceBehavior<GameManager>.Instance;
            if (game == null || game.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            try
            {
                if (!FamilyState.Loaded)
                {
                    if (Partner.UsesSocial && !FamilyBridges.Social.Ready)
                    {
                        return;   // who the partner is can only be read once Social has read its people
                    }
                    FamilyState.Load();
                    FamilyModifiers.Register();
                    _social = Partner.UsesSocial;
                    FamilyPhone.Setup();
                    Partner.Resync();
                    _lastHour = TimeHelper.CurrentHour;
                    if (FamilyState.S.LastDay <= 0)
                    {
                        FamilyState.S.LastDay = TimeHelper.CurrentDay;
                    }
                }
                _started = true;

                // The social life was switched on or off: who the partner is, and their contact, follow.
                if (Partner.UsesSocial != _social)
                {
                    _social = Partner.UsesSocial;
                    FamilyPhone.Setup();
                    Partner.Resync();
                }

                // The game clears its event hub on every load, so the save hook is kept attached.
                if (Time.unscaledTime >= _nextHook)
                {
                    _nextHook = Time.unscaledTime + 2f;
                    SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));
                }

                if (Cast.AnyOut)
                {
                    Cast.Tick(Time.deltaTime);
                }
                float now = Time.time;
                if (now < _nextTick)
                {
                    return;
                }
                _nextTick = now + (Scene != null ? 0.2f : 1f);
                Tick(now);
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Error(e);
                Logger?.Warn("Family: switched off for this session after the error above. The save keeps the family as it was.");
                Stop(false);
            }
        }

        private void Tick(float now)
        {
            int day = TimeHelper.CurrentDay;
            int hour = TimeHelper.CurrentHour;
            FamilySave s = FamilyState.S;
            if (day != s.LastDay)
            {
                int elapsed = Mathf.Clamp(day - s.LastDay, 1, 14);
                s.LastDay = day;
                FamilyCalendar.Daily(day, elapsed);
            }
            if (hour != _lastHour)
            {
                _lastHour = hour;
                FamilyCalendar.Hourly(day, hour);
            }

            Body.Bind(PlayerHelper.PlayerController);
            Body.Tick(now);
            _promptWanted = false;
            _objectiveWanted = false;

            if (!Busy && _questions.Count > 0 && LifeEvents.IsSafeForEvent() && !HudConfirm.isOpen)
            {
                Question q = _questions[0];
                _questions.RemoveAt(0);
                Put(q);
            }

            if (Scene != null)
            {
                Scene.Tick(now);
                if (Scene != null && Scene.Done)
                {
                    EndScene();
                }
            }
            else if (!Busy)
            {
                HomeLife.Tick(now);
                Errands.Tick(now);
                Errands.Counters(this);
            }
            if (!_promptWanted)
            {
                ClearPrompt();
            }
            if (!_objectiveWanted)
            {
                _card?.Show(false);
                _objectiveKey = null;
            }
        }

        /// <summary>Everything going on ends and everything borrowed goes back; the save is not touched. For the save tools.</summary>
        public void Halt()
        {
            Stop(false);
        }

        private void Stop(bool leaving)
        {
            _started = false;
            try
            {
                StopAllCoroutines();
                if (Busy)
                {
                    Busy = false;
                    Recover();
                }
                if (Scene != null)
                {
                    EndScene();
                }
                HomeLife.Leave();
                Cast.ReleaseAll();
                Props.ReturnAll();
                Body.Release();
                FamilyPlaces.Unguide();
                _questions.Clear();
                ClearPrompt();
                _card?.Show(false);
                _objectiveKey = null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (leaving)
            {
                FamilyBridges.Reset();
            }
        }

        private void OnSaveGame()
        {
            try
            {
                FamilyState.Write();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Runs what is not on the director's own tick (a button, a phone choice, an answer): a fault is logged and goes no further.</summary>
        public void Guarded(Action action)
        {
            if (!FamilyState.Loaded || SaveGameManager.Current == null || action == null)
            {
                return;
            }
            try
            {
                action();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>True when the player could answer a prompt: no menu, no conversation, no job's key in the way.</summary>
        internal static bool CanAct()
        {
            return !ThirdPersonCamera.IsUiOpen() && DialogController.current == null && !JobDirector.TakesInteractKey
                   && !GameMembers.PlayerDead && LifeEvents.IsSafeForEvent();
        }
    }
}
