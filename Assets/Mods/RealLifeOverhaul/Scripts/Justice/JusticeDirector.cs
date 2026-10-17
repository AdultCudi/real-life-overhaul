using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Courts and prison: cases, bail, hearings (<see cref="CourtHearing"/>), sentences (<see cref="PrisonStay"/>, community
    /// service), appeals, civil suits and sealing. This component is the clock and the switchboard: idle it costs one time
    /// comparison a frame, the day's business runs once an in-game hour, and per-frame work happens only during a community
    /// service shift. The player is only ever held through <see cref="JusticeHold"/>, which the watchdog lets go of.
    /// </summary>
    public sealed partial class JusticeDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("justice", "JusticeDirector", typeof(JusticeDirector));

        private const int MostOpenCases = 3;
        private const int StaleAfter = 25;          // seconds of the game running without a sign of life before a routine is taken for dead

        public static IModLogger Logger;
        public static JusticeDirector Instance { get; private set; }

        private JusticeHold _hold;
        private PrisonStay _prison;
        private Courthouse _court;
        private CommunityService _community;
        private LegalFile _file;
        private CourtHearing _hearing;

        private bool _broken;
        private int _faults;
        private float _nextSlow;
        private float _nextFast;
        private int _lastHour = -1;
        private bool _routine;              // a custody or a prison term is being served
        private bool _lawHolds;             // ...or the Law bridge holds the player in a cell and has not said it is over
        private Action _lawReleased;
        private bool _bailOpen;
        private bool _fileOpen;
        private bool _fileQueued;
        private JusticeCase _hearingWanted;
        private bool _hearingRemote;
        private int _orphanSeconds;
        private Action _onSave;
        private int _lastBeats;
        private int _staleSeconds;
        private int _lastUnlicensedDay;

        public Courthouse Court => _court;

        /// <summary>False once the justice system has switched itself off after repeated errors.</summary>
        public bool Working => !_broken && isActiveAndEnabled;

        public bool WantsKey => (_door != null && _door.TakesKey) || (_community != null && _community.WantsKey);

        /// <summary>True while the Legal file is the page on screen, the only page that holds nobody.</summary>
        public bool FileIsOpen => _fileOpen;

        // True while anything of ours has the player: the watchdog runs as long, even once the feature has switched itself off.
        private bool Holding => _routine || _hold.Any || JusticePanel.IsOpen;

        private void Awake()
        {
            Instance = this;
            _door = new DoorPanel(Logger, "Justice check (door panel)");
            _onUse = _door.Press(UseDoor);
            _doorWhat = DoorWhat;
            _onSave = OnSaveGame;
            JusticeState.Reset();
            _hold = new JusticeHold(Logger);
            _prison = new PrisonStay(this, _hold, Logger);
            _court = new Courthouse(Logger);
            _community = new CommunityService(Logger);
            _community.KeptBy(this);
            _courtMark.Keep(this);   // the marker goes with this component, or when the guard stops it
            _file = new LegalFile(this);
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                _community.Dispose();
                _court.Dispose();
                if (JusticePanel.Exists)
                {
                    JusticePanel.Instance.Dispose();
                }
                _door.Hide("the director went with the city");
                _courtMark.Hide();
                _hold.ReleaseAll(null);   // a routine dies with us: what it had changed is put back here
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            JusticeState.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Justice: JusticeDirector.Update");
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
            if (_broken && !Holding)
            {
                return;
            }
            try
            {
                if (_community.Active && Time.time >= _nextFast)
                {
                    _nextFast = Time.time + 0.2f;
                    _community.Tick(Time.time, PlayerHelper.PlayerController);
                }
                if (Time.unscaledTime >= _nextSlow)
                {
                    _nextSlow = Time.unscaledTime + 1f;
                    Slow();
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        /// <summary>An answer on the panel threw: handled like any other fault.</summary>
        public void PanelFault(Exception e)
        {
            Fault(e);
        }

        /// <summary>The game's list window could not be copied: everything is let go and the justice system stays off for the session.</summary>
        public void UiLost()
        {
            Logger?.Warn("Justice check (page list): the game's list window could not be copied. Switched off for this session; cases and sentences are kept in the save: FAIL.");
            LetgoQuietly();
            _broken = true;
        }

        private void Fault(Exception e)
        {
            Logger?.Error(e);
            LetgoQuietly();
            if (++_faults >= 3)
            {
                _broken = true;
                Logger?.Warn("Justice: switched off for this session after repeated errors (see above). Cases and sentences are kept in the save.");
            }
        }

        private void LetgoQuietly()
        {
            try
            {
                Letgo();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Letgo()
        {
            _hearing = null;
            _bailOpen = false;
            _fileOpen = false;
            _fileQueued = false;
            _hearingWanted = null;
            _community.Stop(false);
            if (JusticePanel.Exists)
            {
                JusticePanel.Instance.Close();
            }
            if (!_routine)
            {
                _hold.ReleaseAll(this);
            }
            HidePrompt();
        }

        private void Slow()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            var save = SaveGameManager.Current;
            if (gm == null || gm.playerController == null || save == null || LoadScene.isLoading)
            {
                return;
            }
            if (!JusticeState.Loaded)
            {
                if (!ModState.Loaded)
                {
                    return;   // LifeEvents makes the save's modData ready first
                }
                JusticeState.Load(save.modData);
                LegalPhone.Setup(Logger);
                _lastHour = TimeHelper.CurrentHour;
                if (JusticeState.Day == 0)
                {
                    JusticeState.Day = TimeHelper.CurrentDay;
                }
                Logger?.Info("Justice: ready. Open cases " + JusticeState.OpenCount + ", owed " + JusticeState.Owed + ", priors " + JusticeState.Priors
                             + (JusticeState.InPrison ? ", in prison (day " + (JusticeState.PrisonServed + 1) + " of " + JusticeState.PrisonTotal + ")" : string.Empty) + ".");
            }

            SaveHook.Keep(_onSave);   // the game clears its event hub on every load; this allocates nothing while the hook stands

            Watchdog();
            if (_broken)
            {
                return;   // switched off: only the watchdog is still on duty, until nothing is held
            }
            if (!JusticeSettings.Enabled)
            {
                Quiet();
                return;
            }
            _court.Tick();
            int hour = TimeHelper.CurrentHour;
            if (hour != _lastHour)
            {
                _lastHour = hour;
                OnHour(TimeHelper.CurrentDay, hour);
            }
            Attend();
            Prompts(gm.playerController);
        }

        // Switched off in the options: nothing is asked of the player and nothing holds them. The file is kept.
        private void Quiet()
        {
            if (_community.Active || _promptKind != 0 || _markCourt)
            {
                _community.Stop(false);
                HidePrompt();
            }
            if (JusticeState.InPrison && !_routine)
            {
                JusticeState.ClearPrison();
            }
        }

        private void OnSaveGame()
        {
            try
            {
                var save = SaveGameManager.Current;
                if (JusticeState.Loaded && save != null)
                {
                    if (save.modData == null)
                    {
                        save.modData = new Dictionary<string, string>();
                    }
                    JusticeState.Save(save.modData);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // Frees the player from any hold nothing stands behind any more. Never throws.
        private void Watchdog()
        {
            try
            {
                bool panelOwned = _hearing != null || _bailOpen || _fileOpen || _routine;
                if (JusticePanel.IsOpen && (!panelOwned || !JusticePanel.IsShowing))
                {
                    Logger?.Warn("Justice watchdog: the panel was open with nothing behind it; closed, and the player released.");
                    Letgo();
                    return;
                }
                // A routine that is alive beats every frame it waits.
                if (!_routine || _hold.Beats != _lastBeats)
                {
                    _lastBeats = _hold.Beats;
                    _staleSeconds = 0;
                }
                else if (++_staleSeconds >= StaleAfter)
                {
                    _staleSeconds = 0;
                    Logger?.Warn("Justice watchdog: a custody or prison routine stopped without finishing; the player is released.");
                    StopAllCoroutines();
                    _routine = false;
                    if (JusticeState.InPrison)
                    {
                        JusticeState.ClearPrison();
                        JusticeText.Toast(NotificationType.Info, "reallife_justice_prison_released_early", 10f);
                    }
                    JusticeState.JailHoursPending = 0;
                    UIs uis = InstanceBehavior<UIs>.Instance;
                    if (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning)
                    {
                        uis.timeMachine.StopTimeMachine(0f);
                    }
                    Letgo();
                    return;
                }
                if (_hold.Any && !JusticePanel.IsShowing && !_routine)
                {
                    if (++_orphanSeconds >= 3)
                    {
                        _orphanSeconds = 0;
                        Logger?.Warn("Justice watchdog: the player was still held with no panel and no routine; released.");
                        _hold.ReleaseAll(this);
                    }
                }
                else
                {
                    _orphanSeconds = 0;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>True when a panel may be put in front of the player: nothing else has them, and they are not at speed.</summary>
        private bool Safe()
        {
            if (_routine || _hearing != null || _bailOpen || _fileOpen || JusticePanel.IsOpen || UiFader.isFading || PoliceAttention.IsBusy
                || !LifeEvents.IsSafeForEvent() || Wiring.Scenes.PlayerHeld)   // a test paper, a driven test, another feature's panel
            {
                return false;
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            return gm != null && (gm.selectedVehicle == null || gm.selectedVehicle.CurrentSpeed < 1.5f);
        }

        // What is waiting for the player, most pressing first: one thing a second, and only when it is safe.
        private void Attend()
        {
            JusticeCase bail = null;
            if (!JusticeState.InPrison && JusticeState.JailHoursPending == 0 && !JusticeState.ServeWarrant && JusticeState.HearNow == 0 && _hearingWanted == null)
            {
                for (int i = 0; i < JusticeState.Cases.Count && bail == null; i++)
                {
                    if (JusticeState.Cases[i].Stage == CaseStage.Bail)
                    {
                        bail = JusticeState.Cases[i];
                    }
                }
                if (bail == null && !_fileQueued)
                {
                    return;
                }
            }
            if (!Safe())
            {
                return;
            }
            bool forced = JusticeState.InPrison || JusticeState.JailHoursPending > 0 || JusticeState.ServeWarrant || JusticeState.HearNow != 0;
            if (forced || _hearingWanted != null)
            {
                // Nobody works a shift through their own hearing.
                _community.Stop(false);
                if (JobDirector.OnShift && JobDirector.Instance != null)
                {
                    JobDirector.Instance.Quit();
                    return;
                }
            }
            else if (_community.Active || JobDirector.OnShift)
            {
                return;   // a decision that can wait until the shift is over
            }

            if (JusticeState.InPrison)
            {
                StartCoroutine(Guard.Routine(Routine(_prison.Serve())));
            }
            else if (JusticeState.JailHoursPending > 0)
            {
                int hours = JusticeState.JailHoursPending;
                JusticeState.JailHoursPending = 0;   // begun: a save made in the cell does not start it again
                BeginCustody(hours, JusticeState.HearNow != 0);
            }
            else if (JusticeState.ServeWarrant)
            {
                ServeWarrant();
            }
            else if (JusticeState.HearNow != 0)
            {
                JusticeCase c = JusticeState.Find(JusticeState.HearNow);
                JusticeState.HearNow = 0;
                if (c != null && c.Open)
                {
                    BeginHearing(c, remote: false);
                }
            }
            else if (_hearingWanted != null)
            {
                JusticeCase c = _hearingWanted;
                _hearingWanted = null;
                if (c.Open)
                {
                    BeginHearing(c, _hearingRemote);
                }
            }
            else if (bail != null)
            {
                ShowBail(bail);
            }
            else if (_fileQueued)
            {
                _fileQueued = false;
                OpenFile();
            }
        }

        private IEnumerator Routine(IEnumerator what)
        {
            _routine = true;
            _hold.Beat();
            HidePrompt();
            try
            {
                yield return what;
            }
            finally
            {
                _routine = false;
            }
        }

        private void BeginCustody(int hours, bool toCourt)
        {
            hours = Mathf.Clamp(hours, 1, JusticeLaw.CustodyCapHours);
            bool taken = false;
            try
            {
                if (_lawReleased == null)
                {
                    _lawReleased = () => _lawHolds = false;
                }
                _lawHolds = true;   // before the call: a bridge that lets them out at once has said so by the time it returns
                taken = Justice.Bridges.Law.Custody(hours, toCourt ? JusticeLaw.HoldRemand : JusticeLaw.HoldSentence, _lawReleased);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (!taken)
            {
                _lawHolds = false;
                StartCoroutine(Guard.Routine(Routine(_prison.Custody(hours, toCourt, null))));
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Justice check (custody): " + hours + " h " + (toCourt ? "on remand" : "of custody") + ": "
                             + (taken ? "served in the police station's cell (the Law bridge took it)" : "served as time passing (no station could take it)") + ": PASS.");
            }
        }

        private bool LawHolding()
        {
            try
            {
                return Justice.Bridges.Law.Holding;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        // Another feature's code: a fault there is logged and the justice system carries on.
        private static void CallBridge(Action call)
        {
            try
            {
                call();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static Dictionary<string, string> Data(string name, string value)
        {
            return new Dictionary<string, string>(1) { { name, value } };
        }

        private static Dictionary<string, string> Data(string name, string value, string name2, string value2)
        {
            return new Dictionary<string, string>(2) { { name, value }, { name2, value2 } };
        }

        private static Dictionary<string, string> Data(string name, string value, string name2, string value2, string name3, string value3)
        {
            return new Dictionary<string, string>(3) { { name, value }, { name2, value2 }, { name3, value3 } };
        }
    }
}
