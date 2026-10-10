using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Helpers;
using Streets;
using UI;
using UI.Guiders;
using UI.Load;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Courts and prison. A case is opened when the player is arrested or charged with something
    /// serious (Justice.Arrest / Justice.Summon; tickets never come here). It gets a bail decision,
    /// a court date some days ahead, and a hearing that must be attended at the courthouse; a
    /// missed date is a bench warrant. The hearing (CourtHearing) ends in a dismissal, an acquittal
    /// or a sentence: a fine, community service (CommunityService), probation, a licence
    /// suspension, custody, or prison (PrisonStay). Afterwards: appeals, civil suits, sealing.
    ///
    /// This component is the clock and the switchboard for all of that. Idle it is one time
    /// comparison a frame and a handful of integer tests a second; the day's business (instalments,
    /// deadlines, reminders) is done once per in-game hour. Per frame work happens only while a
    /// community service shift is on, while a prompt is in reach, or on a court day (one marker).
    /// Everything is kept in JusticeState, one line in the save's modData.
    ///
    /// The player is only ever held by the justice panel (game paused, the game's own navigation
    /// blocker) or by a time skip behind a black screen. Both go through JusticeHold, and the
    /// watchdog here releases all of it if the panel or the routine that should be holding the
    /// player is no longer there.
    /// </summary>
    public class JusticeDirector : MonoBehaviour
    {
        private const int MostOpenCases = 3;
        private const int StaleAfter = 25;          // seconds (of the game running) without a sign of life before a routine is taken for dead

        public static IModLogger Logger;
        public static JusticeDirector Instance { get; private set; }

        private JusticeHold _hold;
        private PrisonStay _prison;
        private Courthouse _court;
        public Courthouse Court => _court;   // read only, for the city map (World/MapLinks)
        private CommunityService _community;
        private LegalFile _file;
        private CourtHearing _hearing;

        private bool _broken;
        private int _faults;
        private float _nextSlow;
        private float _nextFast;
        private int _lastHour = -1;
        private bool _routine;              // a custody or a prison term is being served
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

        private int _promptKind;            // 0 none, 1 hearing, 2 surrender, 3 community service, 4 a lawyer's desk
        private string _prompt;
        private int _promptKeys = -1;
        private int _promptShownKind;
        private bool _markCourt;
        private string _courtLabel;

        /// <summary>False once the justice system has switched itself off after repeated errors.</summary>
        public bool Working => !_broken && isActiveAndEnabled;

        /// <summary>True while the interaction key is the justice system's.</summary>
        public bool WantsKey => _promptKind != 0 || (_community != null && _community.WantsKey);

        private void Awake()
        {
            Instance = this;
            _onSave = OnSaveGame;   // one delegate for the life of the component: re-hooking it every second allocates nothing new
            JusticeState.Reset();
            _hold = new JusticeHold(Logger);
            _prison = new PrisonStay(this, _hold, Logger);
            _court = new Courthouse(Logger);
            _community = new CommunityService(Logger);
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
                    JusticePanel.Instance.Close();
                }
                _hold.ReleaseAll(null);   // a routine that was running dies with us: what it had changed is put back here
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

        private void Update()
        {
            if (_broken && !Holding)
            {
                return;
            }
            try
            {
                if (_community.Active)
                {
                    PlayerController player = PlayerHelper.PlayerController;
                    _community.Frame(player);
                    if (Time.time >= _nextFast)
                    {
                        _nextFast = Time.time + 0.2f;
                        _community.Tick(Time.time, player);
                    }
                }
                else if (_promptKind != 0 && JobKeys.InteractDown() && !ThirdPersonCamera.IsUiOpen())
                {
                    UsePrompt();
                }
                if (_markCourt)
                {
                    PlayerController player = PlayerHelper.PlayerController;
                    if (player != null)
                    {
                        JusticeHud.Instance.ShowMarkerAt(_courtLabel, _court.Front, player.transform.position);
                    }
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

        // True while anything of ours has the player: the watchdog runs for as long, even once the feature has switched itself off.
        private bool Holding => _routine || _hold.Any || JusticePanel.IsOpen;

        /// <summary>An answer on the panel threw. Handled like any other fault: everything is let go.</summary>
        public void PanelFault(Exception e)
        {
            Fault(e);
        }

        // Whatever went wrong, the player is let go and the screens are taken down. Three faults and it stays off.
        private void Fault(Exception e)
        {
            Logger?.Error(e);
            try
            {
                Letgo();
            }
            catch (Exception again)
            {
                Logger?.Error(again);
            }
            if (++_faults >= 3)
            {
                _broken = true;
                Logger?.Warn("Justice: switched off for this session after repeated errors (see above). Cases and sentences are kept in the save.");
            }
        }

        // Every screen down, every hold released, nothing queued. The cases themselves are untouched.
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

        // ---------------------------------------------------------------- once a second

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

            // The game clears its event hub on every load, so the save hook is kept attached.
            SaveHook.Keep(_onSave);   // the mod's one relay in the game's save event (Core/SaveHook): nothing is allocated while that event has not changed

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

        /// <summary>Something in the file changed: nothing to do but be seen to have been told (the state is saved with the game).</summary>
        public void Touch()
        {
        }

        // Frees the player from any confinement nothing stands behind any more. Never throws.
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
                // A routine that is alive beats every frame it waits. One that has not for 25 of these seconds is dead.
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
                || !LifeEvents.IsSafeForEvent() || Wiring.Scenes.PlayerHeld)   // (a test paper, a driven test, another feature's panel)
            {
                return false;
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            return gm != null && (gm.selectedVehicle == null || gm.selectedVehicle.CurrentSpeed < 1.5f);
        }

        // What is waiting for the player, most pressing first; one thing a second, and only when it is safe.
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
                    return;   // the usual second: nothing waiting
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
                StartCoroutine(Routine(_prison.Serve()));
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

        // Custody through the Law bridge if it takes it (a precinct, a cell), otherwise the plain time skip.
        private void BeginCustody(int hours, bool toCourt)
        {
            hours = Mathf.Clamp(hours, 1, JusticeLaw.CustodyCapHours);
            bool taken = false;
            try
            {
                taken = Justice.Bridges.Law.Custody(hours, null);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (!taken)
            {
                StartCoroutine(Routine(_prison.Custody(hours, toCourt, null)));
            }
        }

        // ---------------------------------------------------------------- once an in-game hour

        private void OnHour(int today, int hour)
        {
            if (today != JusticeState.Day)
            {
                JusticeState.Day = today;
                NewDay(today);
            }
            for (int i = 0; i < JusticeState.Cases.Count; i++)
            {
                JusticeCase c = JusticeState.Cases[i];
                if (c.Stage == CaseStage.Pending)
                {
                    Pending(c, today, hour);
                }
                else if (c.Stage == CaseStage.Warrant)
                {
                    if (today - c.WarrantDay >= JusticeLaw.WarrantSelfArrestDays && hour >= 8 && hour <= 20 && !JusticeState.ServeWarrant)
                    {
                        JusticeState.ServeWarrant = true;   // nobody has come across them: officers come to them
                        Logger?.Info("Justice: the warrant in case " + c.Number + " is " + (today - c.WarrantDay) + " days old; it is served at the player's door.");
                    }
                }
                else if (c.Stage == CaseStage.Bail && today > c.OpenedDay + 1)
                {
                    // The bail decision could not be put to them for two days: released on their own recognisance, with a date.
                    SetDate(c, today);
                }
            }
        }

        private void Pending(JusticeCase c, int today, int hour)
        {
            // The prosecutor reads the file the day after: what the evidence cannot carry is dropped, and a case left with nothing ends there.
            if (!c.Reviewed && today > c.OpenedDay && c != (_hearing != null ? _hearing.Case : null))
            {
                c.Reviewed = true;
                var dropped = new List<string>(2);
                for (int k = 0; k < c.Charges.Count; k++)
                {
                    if (!c.Charges[k].Dropped && c.Charges[k].Evidence < JusticeLaw.DismissBelow)
                    {
                        c.Charges[k].Dropped = true;
                        dropped.Add(JusticeText.Charge(c.Charges[k].Id));
                    }
                }
                if (c.Standing == 0)
                {
                    Dismiss(c, announce: true);
                    return;
                }
                if (dropped.Count > 0)
                {
                    LegalPhone.Send("reallife_text_justice_dropped", Data("number", JusticeText.N(c.Number), "charges", string.Join(", ", dropped)));
                }
            }
            bool late = today > c.CourtDay || (today == c.CourtDay && hour >= JusticeLaw.CourtCloses);
            if (late)
            {
                bool excused = JusticeState.InPrison || _routine || (_hearing != null && _hearing.Case == c) || JusticeState.HearNow == c.Number
                               || Helpers.EnergyHelper.goingToHospital;
                if (excused && (c.Postponed < 5 || JusticeState.InPrison || _routine))   // (nobody in a cell is ever counted absent)
                {
                    c.Postponed++;
                    c.CourtDay = today + 1;
                    c.Reminded = 0;
                    Logger?.Info("Justice: case " + c.Number + " could not be attended (held elsewhere); the hearing is moved to day " + c.CourtDay + ".");
                }
                else if (_hearing == null || _hearing.Case != c)
                {
                    IssueWarrant(c, today);
                }
                return;
            }
            if (today == c.CourtDay - 1 && hour >= 19 && (c.Reminded & 1) == 0)
            {
                c.Reminded |= 1;
                LegalPhone.Send("reallife_text_justice_tomorrow", Data("number", JusticeText.N(c.Number), "place", CourtPlaceName()));
            }
            if (today == c.CourtDay && hour >= 7 && (c.Reminded & 2) == 0)
            {
                c.Reminded |= 2;
                LegalPhone.Send("reallife_text_justice_today", Data("number", JusticeText.N(c.Number), "place", CourtPlaceName()));
                JusticeText.Toast(NotificationType.Warning, "reallife_justice_toast_today", "place", CourtPlaceName(), 12f);
                PointTheWay();
            }
            if (today == c.CourtDay && hour == (int)JusticeLaw.CourtCloses - 1 && (c.Reminded & 4) == 0)
            {
                c.Reminded |= 4;
                JusticeText.Toast(NotificationType.Error, "reallife_justice_toast_last_hour", 12f);
            }
        }

        // The courthouse as the player's own map destination, if they have none set: the game's own guider, saved by the game itself.
        private void PointTheWay()
        {
            try
            {
                var save = SaveGameManager.Current;
                if (!JusticeSettings.Courthouse || !_court.Usable || _court.Address == null || save == null
                    || (save.customDestination != null && !save.customDestination.IsUndefined()))
                {
                    return;
                }
                GuidersManager.SetGuiderTarget(_court.Address, DirectionGuiderType.Destination);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void NewDay(int today)
        {
            PayInstalments(today);
            if (JusticeState.ProbationUntil > 0 && today > JusticeState.ProbationUntil)
            {
                JusticeState.ProbationUntil = 0;
                JusticeText.Toast(NotificationType.Success, "reallife_justice_toast_probation_over", 10f);
            }
            if (JusticeState.SuspendedUntil > 0 && today > JusticeState.SuspendedUntil)
            {
                JusticeState.SuspendedUntil = 0;
                JusticeText.Toast(NotificationType.Success, "reallife_justice_toast_licence_back", 10f);
            }
            if (JusticeState.CommunityHours > 0 && JusticeState.CommunityDue > 0 && today > JusticeState.CommunityDue && !_community.Active)
            {
                // Not done in time: the hours left become a fine (bounded) and probation runs two days longer. It ends there.
                int hours = JusticeState.CommunityHours;
                int fine = JusticeLaw.Bound(hours * 100, Crime.Cash, 0.10f, 100);
                JusticeState.CommunityHours = 0;
                JusticeState.CommunityDue = 0;
                JusticeState.AddDebt(JusticeDebt.Fine, fine, JusticeLaw.FinePlanDays, JusticeDebt.Final);   // final: never turned back into hours
                JusticeState.ProbationUntil = Mathf.Min(Mathf.Max(JusticeState.ProbationUntil, today) + 2, today + JusticeLaw.ProbationCapDays);
                LegalPhone.Send("reallife_text_justice_community_missed", Data("hours", JusticeText.N(hours), "amount", JusticeText.Money(fine)));
            }
            for (int i = JusticeState.Suits.Count - 1; i >= 0; i--)
            {
                JusticeSuit suit = JusticeState.Suits[i];
                if (today > suit.DueDay)
                {
                    JusticeState.Suits.RemoveAt(i);
                    JudgeSuit(suit);
                }
            }
            if (JusticeSettings.Aftermath && JusticeState.Priors > 0 && JusticeState.OpenCount == 0 && !JusticeState.OnProbation(today) && !JusticeState.InPrison
                && today - JusticeState.LastConvictionDay >= JusticeLaw.SealAfterCleanDays)
            {
                int count = JusticeState.Seal();
                try
                {
                    Justice.Bridges.Law.RecordSealed();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                ModState.AddModifier(ModState.MoodUp);
                LegalPhone.Send("reallife_text_justice_sealed", Data("count", JusticeText.N(count), "days", JusticeText.N(JusticeLaw.SealAfterCleanDays)));
                Logger?.Info("Justice: the record was sealed (" + count + " entries) after " + JusticeLaw.SealAfterCleanDays + " clean days.");
            }
            JusticeState.Trim();
        }

        // ---------------------------------------------------------------- money

        // A day's instalments. Each is taken only if it leaves the player something; one that cannot be taken five days
        // running is written off (a fine into hours of community service). No interest, no penalty: nothing grows.
        private void PayInstalments(int today)
        {
            if (JusticeState.Debts.Count == 0)
            {
                return;
            }
            int paid = 0, missed = 0;
            for (int i = JusticeState.Debts.Count - 1; i >= 0; i--)
            {
                JusticeDebt debt = JusticeState.Debts[i];
                int due = Mathf.Min(debt.PerDay, debt.Left);
                if (Crime.Cash - due >= JusticeLaw.CashFloor)
                {
                    GameManager.ChangeMoneySafe(-due, new TransactionInfo(TransactionKey(debt.Kind)), null, null, force: true);
                    debt.Left -= due;
                    debt.Missed = 0;
                    paid += due;
                    if (debt.Left <= 0)
                    {
                        JusticeState.Debts.RemoveAt(i);
                    }
                    continue;
                }
                missed++;
                if (++debt.Missed < JusticeLaw.MissedInstalmentsMost)
                {
                    continue;
                }
                JusticeState.Debts.RemoveAt(i);
                JusticeCase fined = debt.Kind == JusticeDebt.Fine ? JusticeState.Find(debt.CaseNumber) : null;
                if (fined != null)
                {
                    fined.FineImposed = Mathf.Max(0, fined.FineImposed - debt.Left);   // never paid: an appeal won must not hand it back
                }
                if (debt.Kind == JusticeDebt.Fine && JusticeSettings.Community && debt.CaseNumber != JusticeDebt.Final)
                {
                    int hours = Mathf.Min(JusticeLaw.WriteOffHoursMost, (debt.Left + JusticeLaw.WriteOffPerHour - 1) / JusticeLaw.WriteOffPerHour);
                    JusticeState.CommunityHours = Mathf.Min(JusticeLaw.CommunityCapHours, JusticeState.CommunityHours + hours);
                    JusticeState.CommunityDue = today + JusticeLaw.CommunityDeadlineDays;
                    LegalPhone.Send("reallife_text_justice_written_off_hours", Data("amount", JusticeText.Money(debt.Left), "hours", JusticeText.N(hours)));
                }
                else
                {
                    ModState.AddModifier(ModState.MoodDown);
                    LegalPhone.Send("reallife_text_justice_written_off", Data("amount", JusticeText.Money(debt.Left)));
                }
            }
            if (paid > 0)
            {
                JusticeText.Toast(NotificationType.Info, "reallife_justice_toast_instalment", "amount", JusticeText.Money(paid), "left", JusticeText.Money(JusticeState.Owed), 9f);
            }
            else if (missed > 0)
            {
                JusticeText.Toast(NotificationType.Warning, "reallife_justice_toast_instalment_missed", 9f);
            }
        }

        private static string TransactionKey(int kind)
        {
            return kind == JusticeDebt.Fee ? "reallife_transaction_legal_fee" : kind == JusticeDebt.Civil ? "reallife_transaction_civil" : "reallife_transaction_court_fine";
        }

        // Paid at once if it is small against what they have; otherwise seven daily instalments.
        private void ImposeFine(JusticeCase c, int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            float cash = Crime.Cash;
            if (amount <= cash * JusticeLaw.PayNowShare && cash - amount >= JusticeLaw.CashFloor)
            {
                GameManager.ChangeMoneySafe(-amount, new TransactionInfo("reallife_transaction_court_fine"), null, null, force: true);
            }
            else
            {
                JusticeState.AddDebt(JusticeDebt.Fine, amount, JusticeLaw.FinePlanDays, c.Number);
            }
        }

        /// <summary>The Legal file's "pay everything now". Returns what to say.</summary>
        public string PayOffAll()
        {
            int total = JusticeState.Owed;
            if (total <= 0 || Crime.Cash - total < JusticeLaw.CashFloor)
            {
                return JusticeText.T("reallife_justice_act_pay_all_short");
            }
            for (int i = JusticeState.Debts.Count - 1; i >= 0; i--)
            {
                GameManager.ChangeMoneySafe(-JusticeState.Debts[i].Left, new TransactionInfo(TransactionKey(JusticeState.Debts[i].Kind)), null, null, force: true);
            }
            JusticeState.Debts.Clear();
            Logger?.Info("Justice: everything owed (" + total + ") paid off at once.");
            return JusticeText.T("reallife_justice_act_pay_all_done", "amount", JusticeText.Money(total));
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

        // ---------------------------------------------------------------- cases

        /// <summary>Opens a case (or adds to the one opened today). Called through Justice.Arrest / Justice.Summon and by the test buttons.</summary>
        public JusticeCase OpenCase(JusticeReport report, bool arrested, bool test)
        {
            if (report == null || report.Charges == null || report.Charges.Length == 0 || !JusticeState.Loaded)
            {
                return null;
            }
            try
            {
                int today = TimeHelper.CurrentDay;
                if (JusticeState.HasWarrant)
                {
                    JusticeState.ServeWarrant = true;   // arrested with a warrant out: that is served too
                }
                // One incident, one case: what is reported on the day a case was opened joins it. And never more than three open.
                JusticeCase c = null;
                for (int i = JusticeState.Cases.Count - 1; i >= 0 && c == null; i--)
                {
                    JusticeCase open = JusticeState.Cases[i];
                    if (open.Open && open.Stage != CaseStage.Warrant && (_hearing == null || _hearing.Case != open)
                        && (open.OpenedDay == today || JusticeState.OpenCount >= MostOpenCases))
                    {
                        c = open;
                    }
                }
                bool joined = c != null;
                if (c == null)
                {
                    if (JusticeState.OpenCount >= MostOpenCases)
                    {
                        Logger?.Info("Justice: " + MostOpenCases + " cases are open already and none can take more; the new charges are not brought.");
                        return null;
                    }
                    c = JusticeState.Open(today);
                    c.Seed ^= Random.Range(0, 1 << 20);   // this save's own luck; kept in the save, so a reload changes nothing
                }
                c.Witnesses = Mathf.Max(c.Witnesses, report.Witnesses);
                c.Cameras |= report.Cameras;
                c.InTheAct |= report.InTheAct;
                c.Fled |= report.Fled;
                c.Complied = joined ? c.Complied && report.Complied : report.Complied;
                c.PaidAtScene += Mathf.Max(0, report.PaidAtScene);
                c.HeldHours += Mathf.Max(0, report.HeldHours);
                int added = 0;
                for (int i = 0; i < report.Charges.Length; i++)
                {
                    string id = report.Charges[i];
                    if (JusticeLaw.Find(id) == null || c.Has(id))
                    {
                        continue;
                    }
                    float evidence = report.Evidence != null && i < report.Evidence.Length
                        ? JusticeLaw.Clamp(report.Evidence[i], 0.05f, 0.97f)
                        : JusticeLaw.Strength(report.Witnesses, report.Cameras, report.InTheAct, report.Fled, ((JusticeLaw.Hash(id) + c.Seed) % 11 - 5) / 100f);
                    c.Charges.Add(new JusticeCharge { Id = id, Evidence = evidence });
                    added++;
                }
                if (c.Charges.Count == 0)
                {
                    JusticeState.Cases.Remove(c);
                    return null;
                }
                if (added == 0)
                {
                    return c;
                }
                JusticeCharge main = c.Main;
                if (JusticeState.OnProbation(today) && !c.Has(JusticeLaw.Violation) && main != null)
                {
                    c.Charges.Add(new JusticeCharge { Id = JusticeLaw.Violation, Evidence = main.Evidence });   // it stands or falls with the main charge
                }
                Logger?.Info("Justice: case " + c.Number + (joined ? " added to" : " opened") + " (" + (arrested ? "arrest" : "summons") + (test ? ", test" : string.Empty)
                             + "): " + Describe(c) + ".");
                if (joined)
                {
                    return c;
                }
                if (arrested)
                {
                    c.Stage = CaseStage.Bail;   // put to the player as soon as they are free to hear it (Attend)
                    JusticeText.Toast(NotificationType.Error, "reallife_justice_toast_arrested", "charge", JusticeText.Charge(main.Id), 10f);
                }
                else
                {
                    SetDate(c, today);
                    JusticeText.Toast(NotificationType.Error, "reallife_justice_toast_summons", "charge", JusticeText.Charge(main.Id), 10f);
                }
                return c;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return null;
            }
        }

        private static string Describe(JusticeCase c)
        {
            var text = new System.Text.StringBuilder(96);
            for (int i = 0; i < c.Charges.Count; i++)
            {
                text.Append(i > 0 ? ", " : string.Empty).Append(c.Charges[i].Id).Append(' ').Append(Mathf.RoundToInt(c.Charges[i].Evidence * 100f)).Append('%')
                    .Append(c.Charges[i].Dropped ? " (dropped)" : string.Empty);
            }
            text.Append("; witnesses ").Append(c.Witnesses).Append(c.Cameras ? ", filmed" : string.Empty).Append(c.InTheAct ? ", in the act" : string.Empty)
                .Append(c.Fled ? ", fled" : string.Empty).Append(c.Complied ? ", complied" : string.Empty);
            return text.ToString();
        }

        private void SetDate(JusticeCase c, int today)
        {
            c.Stage = CaseStage.Pending;
            c.CourtDay = today + JusticeLaw.DaysToCourt(c.Weight);
            c.Reminded = 0;
            LegalPhone.Send("reallife_text_justice_date", Data("number", JusticeText.N(c.Number), "day", JusticeText.N(c.CourtDay), "place", CourtPlaceName()));
            Logger?.Info("Justice: case " + c.Number + " is to be heard on day " + c.CourtDay + ".");
        }

        private void IssueWarrant(JusticeCase c, int today)
        {
            c.Stage = CaseStage.Warrant;
            c.WarrantDay = today;
            if (!c.Has(JusticeLaw.NoShow))
            {
                c.Charges.Add(new JusticeCharge { Id = JusticeLaw.NoShow, Evidence = 0.95f });
            }
            int forfeited = c.BailPaid;
            c.BailPaid = 0;
            LegalPhone.Send("reallife_text_justice_warrant", Data("number", JusticeText.N(c.Number), "amount", JusticeText.Money(forfeited)));
            JusticeText.Toast(NotificationType.Error, "reallife_justice_toast_warrant", 12f);
            Logger?.Info("Justice: case " + c.Number + " was not attended; bench warrant issued" + (forfeited > 0 ? ", bail of " + forfeited + " forfeited." : "."));
        }

        /// <summary>What a wrongly arrested player is paid: a flat sum, so much an hour held, and the roadside fine back.</summary>
        public int CompensationFor(JusticeCase c)
        {
            return (JusticeSettings.Aftermath ? JusticeLaw.CompensationFlat + JusticeLaw.CompensationPerHour * Mathf.Min(c.HeldHours, JusticeLaw.CustodyCapHours) : 0)
                   + Mathf.Max(0, c.PaidAtScene);
        }

        /// <summary>The case is dropped for want of evidence: closed, compensated, off the record.</summary>
        public void Dismiss(JusticeCase c, bool announce)
        {
            if (c == null || !c.Open)
            {
                return;
            }
            int compensation = CompensationFor(c);
            if (compensation > 0)
            {
                Crime.Give(compensation, "reallife_transaction_compensation");
                JusticeState.Compensated += compensation;
            }
            int bail = CloseCase(c, CaseOutcome.Dismissed);
            ModState.AddModifier(ModState.MoodUp);
            if (announce)
            {
                LegalPhone.Send("reallife_text_justice_dismissed", Data("number", JusticeText.N(c.Number), "amount", JusticeText.Money(compensation + bail)));
            }
            JusticeText.Toast(NotificationType.Success, "reallife_justice_toast_dismissed", "amount", JusticeText.Money(compensation + bail), 12f);
            Logger?.Info("Justice: case " + c.Number + " dismissed; " + compensation + " paid in compensation, " + bail + " of bail returned.");
        }

        // Closes a case: bail back (they appeared, or there was nothing to appear for), the lawyer's work done when it was the last. Returns the bail returned.
        private int CloseCase(JusticeCase c, CaseOutcome outcome)
        {
            c.Stage = CaseStage.Closed;
            c.Outcome = outcome;
            c.ClosedDay = TimeHelper.CurrentDay;
            int bail = c.BailPaid;
            c.BailPaid = 0;
            if (bail > 0)
            {
                Crime.Give(bail, "reallife_transaction_bail_refund");
            }
            if (JusticeState.HearNow == c.Number)
            {
                JusticeState.HearNow = 0;
            }
            if (!c.WasConviction)
            {
                try
                {
                    Justice.Bridges.Law.Cleared(c.Number);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            if (JusticeState.OpenCount == 0)
            {
                JusticeState.ClearLawyer();
                JusticeState.ServeWarrant = false;
            }
            return bail;
        }

        // ---------------------------------------------------------------- bail

        private void ShowBail(JusticeCase c)
        {
            int today = TimeHelper.CurrentDay;
            int fines = 0;
            for (int i = 0; i < c.Charges.Count; i++)
            {
                ChargeDef def = c.Charges[i].Dropped ? null : JusticeLaw.Find(c.Charges[i].Id);
                fines += def != null ? def.Fine : 0;
            }
            JusticeLaw.BailKind kind = JusticeLaw.BailFor(c.Weight, Justice.Priors, c.Fled, c.Has(JusticeLaw.NoShow));
            float cash = Crime.Cash;
            int amount = JusticeLaw.BailAmount(fines, cash);
            int bond = Mathf.Max(50, JusticeLaw.Round10(amount * 0.1f));
            var names = new List<string>(c.Charges.Count);
            for (int i = 0; i < c.Charges.Count; i++)
            {
                if (!c.Charges[i].Dropped)
                {
                    names.Add(JusticeText.Charge(c.Charges[i].Id));
                }
            }
            string body = JusticeText.T("reallife_justice_bail_open", "number", JusticeText.N(c.Number), "charges", string.Join(", ", names)) + "\n\n";
            var page = new JusticePage
            {
                Scene = JusticeScene.Court, Title = JusticeText.T("reallife_justice_bail_title"),
                Subtitle = JusticeText.T("reallife_justice_court_subtitle", "number", JusticeText.N(c.Number), "day", JusticeText.N(today)),
                Middle = JusticeFace.Judge, MiddleName = JusticeText.T("reallife_justice_bail_magistrate"), Speaker = 1,
                SpeakerName = JusticeText.T("reallife_justice_bail_magistrate")
            };
            _hold.Pause();
            _hold.Block();
            _bailOpen = true;
            if (kind == JusticeLaw.BailKind.OwnRecognisance)
            {
                page.Body = body + JusticeText.T("reallife_justice_bail_recognisance", "days", JusticeText.N(JusticeLaw.DaysToCourt(c.Weight)));
                page.Add(JusticeText.T("reallife_justice_continue"));
                JusticePanel.Instance.Show(page, _ =>
                {
                    CloseBail();
                    SetDate(c, today);
                });
                return;
            }
            if (kind == JusticeLaw.BailKind.Remand)
            {
                page.Body = body + JusticeText.T("reallife_justice_bail_refused");
                page.Add(JusticeText.T("reallife_justice_continue"));
                JusticePanel.Instance.Show(page, _ =>
                {
                    CloseBail();
                    Remand(c);
                });
                return;
            }
            bool canPay = cash - amount >= JusticeLaw.CashFloor;
            page.Body = body + JusticeText.T("reallife_justice_bail_set", "amount", JusticeText.Money(amount), "days", JusticeText.N(JusticeLaw.DaysToCourt(c.Weight)));
            page.Add(JusticeText.T("reallife_justice_bail_pay", "amount", JusticeText.Money(amount)),
                    JusticeText.T(canPay ? "reallife_justice_bail_pay_hint" : "reallife_justice_bail_pay_hint_short"))
                .Add(JusticeText.T("reallife_justice_bail_bond", "amount", JusticeText.Money(bond)), JusticeText.T("reallife_justice_bail_bond_hint"))
                .Add(JusticeText.T("reallife_justice_bail_stay"), JusticeText.T("reallife_justice_bail_stay_hint"));
            JusticePanel.Instance.Show(page, choice =>
            {
                if (choice == 2)
                {
                    CloseBail();
                    Remand(c);
                    return;
                }
                if (choice == 0 && canPay && Crime.Pay(amount, "reallife_transaction_bail"))
                {
                    c.BailPaid = amount;
                }
                else
                {
                    // The bondsman: a tenth, not returned; owed over a few days if it cannot be paid on the spot.
                    c.Bond = true;
                    if (!(Crime.Cash - bond >= JusticeLaw.CashFloor && Crime.Pay(bond, "reallife_transaction_bail_bond")))
                    {
                        JusticeState.AddDebt(JusticeDebt.Fee, bond, 3, c.Number);
                    }
                }
                CloseBail();
                SetDate(c, today);
            });
        }

        private void CloseBail()
        {
            _bailOpen = false;
            JusticePanel.Instance.Close();
            _hold.ReleaseAll(this);
        }

        // Held until the court next sits (never longer than a day), then heard at once.
        private void Remand(JusticeCase c)
        {
            int hour = TimeHelper.CurrentHour;
            bool sitting = hour >= JusticeLaw.CourtOpens && hour < JusticeLaw.CourtCloses - 1;
            int hours = sitting ? 2 : hour < JusticeLaw.CourtOpens ? (int)JusticeLaw.CourtOpens - hour : 24 - hour + (int)JusticeLaw.CourtOpens;
            hours = Mathf.Clamp(hours, 2, 24);
            c.Remand = true;
            c.Stage = CaseStage.Pending;
            c.HeldHours += hours;
            Timestamp release = TimeHelper.Now();
            release.AddDays(hours / 24);   // (the game's AddHours carries over one day only)
            release.AddHours(hours % 24);
            c.CourtDay = release.Day;
            c.Reminded = 7;
            JusticeState.HearNow = c.Number;
            JusticeState.JailHoursPending = hours;
            Logger?.Info("Justice: case " + c.Number + ": held " + hours + " h until the court sits, then heard.");
        }

        // The warrant is acted on: taken in, and brought before the court when it next sits.
        private void ServeWarrant()
        {
            JusticeState.ServeWarrant = false;
            JusticeCase c = null;
            for (int i = 0; i < JusticeState.Cases.Count && c == null; i++)
            {
                if (JusticeState.Cases[i].Stage == CaseStage.Warrant)
                {
                    c = JusticeState.Cases[i];
                }
            }
            if (c == null)
            {
                return;
            }
            JusticeText.Toast(NotificationType.Error, "reallife_justice_toast_warrant_served", "number", JusticeText.N(c.Number), 10f);
            Remand(c);
        }

        // ---------------------------------------------------------------- the hearing

        /// <summary>Asks for a hearing to begin as soon as it is safe (the Legal file's video link, the test button).</summary>
        public void RequestHearing(JusticeCase c, bool remote)
        {
            _hearingWanted = c;
            _hearingRemote = remote;
        }

        private void BeginHearing(JusticeCase c, bool remote)
        {
            HidePrompt();
            _hold.Pause();
            _hold.Block();
            _hearing = new CourtHearing(this, c, remote);
            Logger?.Info("Justice: hearing of case " + c.Number + (remote ? " by video link" : " at the courthouse") + ": " + Describe(c) + "; counsel "
                         + JusticeState.Lawyer + ", priors " + Justice.Priors + ".");
            _hearing.Begin();
        }

        /// <summary>The last page of a hearing has been answered: the panel comes down and the result takes effect.</summary>
        public void HearingOver(CourtHearing hearing)
        {
            if (hearing != _hearing)
            {
                return;
            }
            _hearing = null;
            JusticePanel.Instance.Close();
            _hold.ReleaseAll(this);
            HearingResult result = hearing.Result;
            if (result == null || result.Case == null || !result.Case.Open)
            {
                return;
            }
            JusticeCase c = result.Case;
            int today = TimeHelper.CurrentDay;
            c.LawyerQuality = LegalCounsel.Quality(c);
            if (result.Outcome == CaseOutcome.Dismissed)
            {
                Dismiss(c, announce: false);
                return;
            }
            if (result.Outcome == CaseOutcome.Acquitted)
            {
                int back = Mathf.Max(0, c.PaidAtScene);
                if (back > 0)
                {
                    Crime.Give(back, "reallife_transaction_compensation");
                }
                back += CloseCase(c, CaseOutcome.Acquitted);
                ModState.AddModifier(ModState.MoodUpBig);
                JusticeText.Toast(NotificationType.Success, "reallife_justice_toast_acquitted", "amount", JusticeText.Money(back), 12f);
                Logger?.Info("Justice: case " + c.Number + ": acquitted; " + back + " returned.");
                Wiring.Paper.Print("reallife_news_story_acquitted");   // the court pages of tomorrow's City Ledger (Scripts/Social)
                return;
            }

            JusticeSentence s = result.Sentence;
            c.FineImposed = result.FineDue;
            c.PrisonImposed = s.PrisonDays;
            c.JailImposed = s.JailHours;
            c.CommunityImposed = s.CommunityHours;
            c.ProbationImposed = s.ProbationDays;
            c.SuspensionImposed = s.SuspensionDays;
            ImposeFine(c, result.FineDue);
            for (int i = 0; i < c.Charges.Count; i++)
            {
                if (!c.Charges[i].Convicted || c.Charges[i].Dropped)
                {
                    continue;
                }
                JusticeState.AddRecord(c.Charges[i].Id, today, c.Number);
                try
                {
                    Justice.Bridges.Law.Convicted(c.Charges[i].Id, today, c.Number);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            JusticeState.LastConvictionDay = today;
            if (s.CommunityHours > 0)
            {
                JusticeState.CommunityHours = Mathf.Min(JusticeLaw.CommunityCapHours, JusticeState.CommunityHours + s.CommunityHours);
                JusticeState.CommunityDue = today + JusticeLaw.CommunityDeadlineDays;
            }
            if (s.ProbationDays > 0 && s.PrisonDays == 0)
            {
                JusticeState.ProbationUntil = Mathf.Max(JusticeState.ProbationUntil, today + s.ProbationDays);   // (after prison it runs from the release)
            }
            if (s.SuspensionDays > 0)
            {
                int until = today + s.SuspensionDays + s.PrisonDays;
                JusticeState.SuspendedUntil = Mathf.Max(JusticeState.SuspendedUntil, until);
                try
                {
                    Justice.Bridges.Licences.SuspendDriving(JusticeState.SuspendedUntil);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            CloseCase(c, result.Outcome);
            ModState.AddModifier(ModState.MoodDownBig);
            if (s.PrisonDays > 0)
            {
                JusticeState.ClearPrison();
                JusticeState.PrisonTotal = JusticeState.PrisonOriginal = s.PrisonDays;
                JusticeState.PrisonCase = c.Number;   // served from the next second (Attend)
                Wiring.Arrests.FamilyHears(3);   // the family is told of the sentence
            }
            else if (s.JailHours > 0)
            {
                JusticeState.JailHoursPending = s.JailHours;
            }
            Wiring.Paper.Print(s.PrisonDays > 0 ? "reallife_news_story_prison" : "reallife_news_story_convicted");
            LegalPhone.Send("reallife_text_justice_judgment", Data("number", JusticeText.N(c.Number), "outcome", JusticeText.T(LegalFile.OutcomeKey(result.Outcome)),
                "amount", JusticeText.Money(result.FineDue)));
            Logger?.Info("Justice: case " + c.Number + ": " + result.Outcome + "; fine " + result.FineDue + " (nominal " + s.FineNominal + (s.Bounded ? ", bounded" : string.Empty)
                         + "), prison " + s.PrisonDays + " d, custody " + s.JailHours + " h, community " + s.CommunityHours + " h, probation " + s.ProbationDays
                         + " d, licence " + s.SuspensionDays + " d.");
        }

        /// <summary>A case that is due now and, with no courthouse to go to, can be heard by video link from the Legal file. Null if none.</summary>
        public JusticeCase HearingDueRemotely()
        {
            if (JusticeSettings.Courthouse && _court.Usable)
            {
                return null;
            }
            return DueNow();
        }

        // The case that can be heard this hour: one whose date is today, or one with a warrant out, while the court sits.
        private JusticeCase DueNow()
        {
            int hour = TimeHelper.CurrentHour;
            if (hour < JusticeLaw.CourtOpens || hour >= JusticeLaw.CourtCloses)
            {
                return null;
            }
            int today = TimeHelper.CurrentDay;
            for (int i = 0; i < JusticeState.Cases.Count; i++)
            {
                JusticeCase c = JusticeState.Cases[i];
                if (c.Stage == CaseStage.Warrant || (c.Stage == CaseStage.Pending && c.CourtDay == today))
                {
                    return c;
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- places

        public string CourtPlaceName()
        {
            return JusticeSettings.Courthouse && _court.Usable
                ? JusticeText.T("reallife_justice_place_courthouse", "address", _court.Name)
                : JusticeText.T("reallife_justice_place_video");
        }

        /// <summary>Where somebody let out is put down: the Law bridge's precinct, else the courthouse front. False if neither.</summary>
        public bool ReleaseSpot(out Vector3 spot)
        {
            return ReleaseSpot(out spot, out _);
        }

        // The one decision of where somebody is put down, and the words for it: the station when the Law bridge gives one, else the courthouse, else nowhere.
        private bool ReleaseSpot(out Vector3 spot, out string place)
        {
            place = null;
            try
            {
                if (Justice.Bridges.Law.ReleaseSpot(out spot, out string name))
                {
                    place = JusticeText.T("reallife_justice_place_station", "name", string.IsNullOrEmpty(name) ? JusticeText.T("reallife_justice_place_station_plain") : name);
                    return true;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            spot = _court.Front;
            if (_court.Usable)
            {
                place = JusticeText.T("reallife_justice_place_courthouse", "address", _court.Name);
            }
            return _court.Usable;
        }

        /// <summary>Where they are let out, in words: always the place ReleaseSpot puts them down at.</summary>
        public string ReleasePlaceName()
        {
            ReleaseSpot(out _, out string place);
            return place ?? JusticeText.T("reallife_justice_place_street");
        }

        // ---------------------------------------------------------------- prompts

        // Once a second: is the player standing where the interaction key is ours?
        private void Prompts(PlayerController player)
        {
            int kind = 0;
            bool mark = false;
            if (player != null && !PlayerHelper.IsUsingVehicle && !_routine && !JusticePanel.IsOpen && !_community.Active)
            {
                if (!BuildingManager.IsInsideBuilding)
                {
                    if (JusticeSettings.Courthouse && _court.Usable)
                    {
                        JusticeCase due = DueNow();
                        mark = due != null;
                        if (_court.InReach(player.transform.position))
                        {
                            int hour = TimeHelper.CurrentHour;
                            kind = due != null ? (due.Stage == CaseStage.Warrant ? 2 : 1)
                                : JusticeState.CommunityHours > 0 && JusticeSettings.Community && hour >= 7 && hour < 20 ? 3 : 0;
                        }
                    }
                }
                else if (JusticeState.OpenCount > 0 && DialogController.current == null)
                {
                    BuildingRegistration here = InstanceBehavior<BuildingManager>.Instance != null ? InstanceBehavior<BuildingManager>.Instance.buildingRegistration : null;
                    if (here != null && here.businessTypeName == LegalCounsel.LawFirmType && (JusticeSettings.Lawyers || LegalCounsel.IsRetained(here)))
                    {
                        kind = 4;
                    }
                }
            }
            if (mark != _markCourt)
            {
                _markCourt = mark;
                if (mark)
                {
                    _courtLabel = JusticeText.T("reallife_justice_marker_court");
                }
                else if (JusticeHud.Exists)
                {
                    JusticeHud.Instance.HideMarker();
                }
            }
            _promptKind = kind;
            if (kind == 0)
            {
                if (_promptShownKind != 0)
                {
                    _promptShownKind = 0;
                    if (JusticeHud.Exists)
                    {
                        JusticeHud.Instance.HidePrompt();
                    }
                }
                return;
            }
            if (kind != _promptShownKind || _promptKeys != Keys.Version)
            {
                _promptShownKind = kind;
                _promptKeys = Keys.Version;
                _prompt = JusticeText.T(kind == 1 ? "reallife_justice_prompt_hearing" : kind == 2 ? "reallife_justice_prompt_surrender"
                    : kind == 3 ? "reallife_justice_prompt_community" : "reallife_justice_prompt_lawyer");
            }
            JusticeHud.Instance.ShowPrompt(_prompt);
        }

        private void HidePrompt()
        {
            _promptKind = 0;
            _promptShownKind = 0;
            _markCourt = false;
            if (JusticeHud.Exists)
            {
                JusticeHud.Instance.HidePrompt();
                JusticeHud.Instance.HideMarker();
            }
        }

        private void UsePrompt()
        {
            int kind = _promptKind;
            if (kind == 4)
            {
                BuildingRegistration here = InstanceBehavior<BuildingManager>.Instance != null ? InstanceBehavior<BuildingManager>.Instance.buildingRegistration : null;
                if (here != null && DialogController.current == null && LifeEvents.IsSafeForEvent())
                {
                    HidePrompt();
                    LegalPhone.OpenInPerson(LegalCounsel.From(here));
                }
                return;
            }
            if (kind == 3)
            {
                HidePrompt();
                StartCommunity(announceRefusal: true);
                return;
            }
            JusticeCase due = DueNow();
            if (due == null)
            {
                return;
            }
            if (!Safe())
            {
                JusticeText.Toast(NotificationType.Warning, "reallife_justice_toast_not_now", 6f);
                return;
            }
            BeginHearing(due, remote: false);
        }

        // ---------------------------------------------------------------- the Legal file

        /// <summary>The Legal file is opened as soon as the phone is away and nothing else has the player.</summary>
        public void QueueFile()
        {
            _fileQueued = true;
        }

        private void OpenFile()
        {
            HidePrompt();
            _hold.Pause();
            _hold.Block();
            _fileOpen = true;
            _file.ShowMain();
        }

        public void CloseFile()
        {
            _fileOpen = false;
            if (JusticePanel.Exists)
            {
                JusticePanel.Instance.Close();
            }
            _hold.ReleaseAll(this);
        }

        // ---------------------------------------------------------------- community service

        /// <summary>Begins a shift where the player stands. Returns why not, and says so if asked to.</summary>
        public JusticeRefusal StartCommunity(bool announceRefusal)
        {
            JusticeRefusal why = !JusticeSettings.Community ? JusticeRefusal.PartOff
                : PoliceAttention.IsBusy ? JusticeRefusal.PoliceBusy
                : _routine || _hearing != null || JobDirector.OnShift ? JusticeRefusal.Busy
                : TimeHelper.CurrentHour >= 21 || TimeHelper.CurrentHour < 6 ? JusticeRefusal.Night
                : _community.Start(PlayerHelper.PlayerController);
            if (why != JusticeRefusal.None && announceRefusal)
            {
                JusticeRefusals.Tell(Logger, "Community service", why);
            }
            return why;
        }

        // ---------------------------------------------------------------- what the police tell us

        public void OnPoliceContact(bool driving)
        {
            try
            {
                if (JusticeState.HasWarrant && !JusticeState.ServeWarrant)
                {
                    JusticeState.ServeWarrant = true;
                    Logger?.Info("Justice: police contact with a bench warrant out; the player is taken in when the stop is over.");
                }
                int today = TimeHelper.CurrentDay;
                if (driving && Justice.DrivingSuspended && _lastUnlicensedDay != today)
                {
                    _lastUnlicensedDay = today;
                    OpenCase(new JusticeReport(JusticeLaw.Unlicensed) { Witnesses = 1, InTheAct = true, Complied = true }, arrested: false, test: false);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Hours in a cell on an arrest made by another feature: begun by Attend, counted in today's case as time served.</summary>
        public bool HoldFor(int hours)
        {
            hours = Mathf.Min(hours, JusticeLaw.CustodyCapHours - JusticeState.JailHoursPending);
            if (hours <= 0 || JusticeState.InPrison)
            {
                return false;
            }
            int today = TimeHelper.CurrentDay;
            for (int i = JusticeState.Cases.Count - 1; i >= 0; i--)
            {
                if (JusticeState.Cases[i].Open && JusticeState.Cases[i].OpenedDay == today)
                {
                    JusticeState.Cases[i].HeldHours += hours;
                    break;
                }
            }
            JusticeState.JailHoursPending += hours;
            Logger?.Info("Justice: held " + hours + " h on an arrest made elsewhere.");
            return true;
        }

        /// <summary>
        /// Out of the police cell after the arrest behind this case. What was paid to walk out is the case's bail (returned on
        /// appearing, forfeit on a warrant), and the case has its date. Only a case the court would refuse bail in still goes
        /// before the magistrate, who remands.
        /// </summary>
        public void OnReleasedByPolice(JusticeCase c, int bailPaid)
        {
            if (c == null || !c.Open || c.Stage != CaseStage.Bail)
            {
                return;
            }
            c.BailPaid += Mathf.Max(0, bailPaid);
            if (JusticeLaw.BailFor(c.Weight, Justice.Priors, c.Fled, c.Has(JusticeLaw.NoShow)) != JusticeLaw.BailKind.Remand)
            {
                SetDate(c, TimeHelper.CurrentDay);
            }
        }

        // ---------------------------------------------------------------- civil suits

        public void OnCrashVictim(int damages, int injured, int killed)
        {
            try
            {
                int today = TimeHelper.CurrentDay;
                if (!JusticeSettings.Civil || JusticeState.Suits.Count > 0 || (JusticeState.LastSuitDay > 0 && today - JusticeState.LastSuitDay < 7))
                {
                    return;
                }
                float chance = injured + killed > 0 ? 0.5f : damages >= 800 ? 0.2f : 0f;
                if (Random.value >= chance)
                {
                    return;
                }
                Sue(damages, injured, killed, today);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Sue(int damages, int injured, int killed, int today)
        {
            var suit = new JusticeSuit
            {
                Number = JusticeState.NextNumber++, Claim = JusticeLaw.CivilClaim(damages, injured, killed, Crime.Cash), DueDay = today + 5,
                Injury = injured + killed > 0, Seed = JusticeLaw.Hash("suit" + JusticeState.NextNumber) ^ (today * 7919)
            };
            JusticeState.Suits.Add(suit);
            JusticeState.LastSuitDay = today;
            LegalPhone.Send(suit.Injury ? "reallife_text_justice_suit_injury" : "reallife_text_justice_suit_property",
                Data("amount", JusticeText.Money(suit.Claim), "day", JusticeText.N(suit.DueDay)));
            Logger?.Info("Justice: civil suit " + suit.Number + " for " + suit.Claim + " (damages " + damages + ", injured " + injured + ", killed " + killed + "), answer by day " + suit.DueDay + ".");
        }

        /// <summary>The chance of winning a contested suit, 0.10 to 0.70: property claims are easier to fight than injuries, and counsel helps.</summary>
        public float SuitWinChance(JusticeSuit suit)
        {
            return Mathf.Clamp((suit.Injury ? 0.20f : 0.35f) + LegalCounsel.Quality(null) * 0.3f, 0.10f, 0.70f);
        }

        public string SettleSuit(JusticeSuit suit)
        {
            if (!JusticeState.Suits.Remove(suit))
            {
                return JusticeText.T("reallife_justice_file_failed");
            }
            int amount = JusticeLaw.Round10(suit.Claim * 0.6f);
            bool now = Crime.Cash - amount >= JusticeLaw.CashFloor && Crime.Pay(amount, "reallife_transaction_civil");
            if (!now)
            {
                JusticeState.AddDebt(JusticeDebt.Civil, amount, JusticeLaw.CivilPlanDays, 0);
            }
            Logger?.Info("Justice: civil suit " + suit.Number + " settled for " + amount + (now ? " (paid)." : " (over " + JusticeLaw.CivilPlanDays + " days)."));
            return JusticeText.T(now ? "reallife_justice_act_settle_done" : "reallife_justice_act_settle_plan", "amount", JusticeText.Money(amount),
                "days", JusticeText.N(JusticeLaw.CivilPlanDays));
        }

        public string ContestSuit(JusticeSuit suit)
        {
            suit.Contested = true;
            return JusticeText.T("reallife_justice_act_contest_done", "day", JusticeText.N(suit.DueDay), "percent", JusticeText.Percent(SuitWinChance(suit)));
        }

        // The day after its date. Contested: won or lost by the suit's own seed against the chance as it stands now.
        // Ignored: judgment by default for the claim. Either way what is owed goes on a ten-day plan and that is the end of it.
        private void JudgeSuit(JusticeSuit suit)
        {
            if (suit.Contested)
            {
                float chance = SuitWinChance(suit);
                if (JusticeLaw.Roll(suit.Seed, 7) < Mathf.RoundToInt(chance * 1000f))
                {
                    ModState.AddModifier(ModState.MoodUp);
                    LegalPhone.Send("reallife_text_justice_suit_won", Data("amount", JusticeText.Money(suit.Claim)));
                    Logger?.Info("Justice: civil suit " + suit.Number + " contested and won.");
                    return;
                }
                int owed = JusticeLaw.Round10(suit.Claim * 1.15f);
                JusticeState.AddDebt(JusticeDebt.Civil, owed, JusticeLaw.CivilPlanDays, 0);
                LegalPhone.Send("reallife_text_justice_suit_lost", Data("amount", JusticeText.Money(owed), "days", JusticeText.N(JusticeLaw.CivilPlanDays)));
                Logger?.Info("Justice: civil suit " + suit.Number + " contested and lost: " + owed + " over " + JusticeLaw.CivilPlanDays + " days.");
                return;
            }
            JusticeState.AddDebt(JusticeDebt.Civil, suit.Claim, JusticeLaw.CivilPlanDays, 0);
            LegalPhone.Send("reallife_text_justice_suit_default", Data("amount", JusticeText.Money(suit.Claim), "days", JusticeText.N(JusticeLaw.CivilPlanDays)));
            Logger?.Info("Justice: civil suit " + suit.Number + " was not answered: judgment by default for " + suit.Claim + ".");
        }

        // ---------------------------------------------------------------- appeals

        /// <summary>The chance an appeal of this conviction succeeds, from the evidence on its main charge and the counsel who argued it.</summary>
        public float AppealChance(JusticeCase c)
        {
            float evidence = 0.6f;
            for (int i = 0, most = -1; i < c.Charges.Count; i++)
            {
                ChargeDef def = c.Charges[i].Convicted ? JusticeLaw.Find(c.Charges[i].Id) : null;
                if (def != null && def.Fine > most)
                {
                    most = def.Fine;
                    evidence = c.Charges[i].Evidence;
                }
            }
            return JusticeLaw.AppealChance(evidence, c.LawyerQuality);
        }

        /// <summary>The one appeal a conviction allows. Returns what to say.</summary>
        public string Appeal(JusticeCase c)
        {
            if (c == null || c.Appealed || !c.WasConviction)
            {
                return JusticeText.T("reallife_justice_file_failed");
            }
            int fee = JusticeLaw.AppealFee(c.FineImposed, Crime.Cash);
            if (Crime.Cash - fee < JusticeLaw.CashFloor || !Crime.Pay(fee, "reallife_transaction_legal_fee"))
            {
                return JusticeText.T("reallife_justice_act_appeal_short", "fee", JusticeText.Money(fee));
            }
            c.Appealed = true;
            float chance = AppealChance(c);
            bool won = JusticeLaw.Roll(c.Seed, 500) < Mathf.RoundToInt(chance * 1000f);
            Logger?.Info("Justice: appeal in case " + c.Number + " (fee " + fee + ", chance " + Mathf.RoundToInt(chance * 100f) + "%): " + (won ? "allowed" : "dismissed") + ".");
            if (!won)
            {
                return JusticeText.T("reallife_justice_act_appeal_lost", "fee", JusticeText.Money(fee));
            }
            // Quashed: the fine comes back (what was paid) or is struck off (what was not), the conviction leaves the record,
            // and time already served is paid for. With nothing else on the record, probation, suspension and hours go too.
            int unpaid = JusticeState.CancelDebts(c.Number, JusticeDebt.Fine);
            int back = Mathf.Max(0, c.FineImposed - unpaid);
            if (JusticeSettings.Aftermath)
            {
                back += c.PrisonImposed * JusticeLaw.CompensationPerPrisonDay + c.JailImposed * JusticeLaw.CompensationPerHour;
            }
            if (back > 0)
            {
                Crime.Give(back, "reallife_transaction_compensation");
                JusticeState.Compensated += back;
            }
            c.Outcome = CaseOutcome.Quashed;
            JusticeState.RemoveRecord(c.Number);
            try
            {
                Justice.Bridges.Law.Cleared(c.Number);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (JusticeState.Priors == 0)
            {
                JusticeState.ProbationUntil = 0;
                JusticeState.CommunityHours = 0;
                JusticeState.CommunityDue = 0;
                if (JusticeState.SuspendedUntil > 0)
                {
                    JusticeState.SuspendedUntil = 0;
                    try
                    {
                        Justice.Bridges.Licences.SuspendDriving(0);   // 0: lifted
                    }
                    catch (Exception e)
                    {
                        Logger?.Error(e);
                    }
                }
            }
            ModState.AddModifier(ModState.MoodUpBig);
            return JusticeText.T("reallife_justice_act_appeal_won", "amount", JusticeText.Money(back));
        }

        // ---------------------------------------------------------------- test buttons

        private JusticeRefusal Ready()
        {
            return !ModState.Loaded || !JusticeState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null ? JusticeRefusal.NotLoaded
                : !JusticeSettings.Enabled ? JusticeRefusal.FeatureOff
                : _broken ? JusticeRefusal.Broken
                : JusticeRefusal.None;
        }

        private JusticeRefusal Free()
        {
            JusticeRefusal why = Ready();
            return why != JusticeRefusal.None ? why
                : JusticeState.InPrison ? JusticeRefusal.InPrison
                : _routine || _hearing != null || _bailOpen || _fileOpen || JusticeState.JailHoursPending > 0 || JusticeState.HearNow != 0 ? JusticeRefusal.Busy
                : JusticeRefusal.None;
        }

        /// <summary>Test: open a case. 0 minor (evading), 1 serious (hit-and-run with an injury), 2 grave (a fatal hit-and-run), 3 one with no evidence behind it.</summary>
        public void TestCase(int kind)
        {
            try
            {
                JusticeRefusal why = Ready();
                if (why == JusticeRefusal.None && JusticeState.OpenCount >= MostOpenCases)
                {
                    why = JusticeRefusal.Busy;
                }
                if (why != JusticeRefusal.None)
                {
                    JusticeRefusals.Tell(Logger, "Open a case", why, "open cases " + JusticeState.OpenCount);
                    return;
                }
                // A case of its own each time: dated as if it were another day's business.
                JusticeReport report =
                    kind == 0 ? new JusticeReport(JusticeLaw.Evading, JusticeLaw.Reckless) { Witnesses = 1, InTheAct = true, Fled = true, PaidAtScene = 900, HeldHours = 3 }
                    : kind == 1 ? new JusticeReport(JusticeLaw.HitRunInjury, JusticeLaw.Reckless) { Witnesses = 2, Cameras = true, Fled = true, HeldHours = 4 }
                    : kind == 2 ? new JusticeReport(JusticeLaw.HitRunDeath, JusticeLaw.Evading) { Witnesses = 2, Cameras = true, InTheAct = true, Fled = true, HeldHours = 5 }
                    : new JusticeReport(JusticeLaw.Theft) { HeldHours = 4, PaidAtScene = 200, Complied = true, Evidence = new[] { 0.2f } };
                int today = TimeHelper.CurrentDay;
                for (int i = 0; i < JusticeState.Cases.Count; i++)
                {
                    if (JusticeState.Cases[i].Open && JusticeState.Cases[i].OpenedDay == today)
                    {
                        JusticeState.Cases[i].OpenedDay = today - 1;   // so the test does not join it
                    }
                }
                OpenCase(report, arrested: true, test: true);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        /// <summary>Test: the next open case's court date becomes today (tomorrow if the court has risen), with bail settled.</summary>
        public void TestCourtDate()
        {
            try
            {
                JusticeRefusal why = Free();
                JusticeCase c = JusticeState.NextOpen;
                if (why == JusticeRefusal.None && c == null)
                {
                    why = JusticeRefusal.NoCase;
                }
                if (why != JusticeRefusal.None)
                {
                    JusticeRefusals.Tell(Logger, "Advance to the court date", why);
                    return;
                }
                int today = TimeHelper.CurrentDay;
                if (c.Stage == CaseStage.Warrant)
                {
                    JusticeText.Toast(NotificationType.Info, "reallife_justice_test_date_warrant", 9f);
                    return;
                }
                c.Stage = CaseStage.Pending;
                c.CourtDay = TimeHelper.CurrentHour < JusticeLaw.CourtCloses - 1 ? today : today + 1;
                c.Reminded = 0;
                _lastHour = -1;   // the reminders are looked at again on the next second
                JusticeText.Toast(NotificationType.Info, "reallife_justice_test_date_set", "day", JusticeText.N(c.CourtDay), "place", CourtPlaceName(), 10f);
                Logger?.Info("Justice test: case " + c.Number + " is now due on day " + c.CourtDay + ".");
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        /// <summary>Test: the next open case is heard as soon as the menu is closed, wherever the player is.</summary>
        public void TestHearing()
        {
            try
            {
                JusticeRefusal why = Free();
                JusticeCase c = JusticeState.NextOpen;
                if (why == JusticeRefusal.None && c == null)
                {
                    why = JusticeRefusal.NoCase;
                }
                if (why != JusticeRefusal.None)
                {
                    JusticeRefusals.Tell(Logger, "Run the hearing", why);
                    return;
                }
                if (c.Stage == CaseStage.Bail)
                {
                    c.Stage = CaseStage.Pending;
                    c.CourtDay = TimeHelper.CurrentDay;
                }
                RequestHearing(c, remote: true);
                JusticeText.Toast(NotificationType.Info, "reallife_justice_test_soon", 6f);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        /// <summary>Test: a prison term of this many days begins as soon as the menu is closed. The days really pass.</summary>
        public void TestPrison(int days)
        {
            try
            {
                JusticeRefusal why = Free();
                if (why == JusticeRefusal.None && JusticeSettings.NoPrison)
                {
                    why = JusticeRefusal.PartOff;
                }
                if (why != JusticeRefusal.None)
                {
                    JusticeRefusals.Tell(Logger, "Prison", why);
                    return;
                }
                JusticeState.ClearPrison();
                JusticeState.PrisonTotal = JusticeState.PrisonOriginal = JusticeLaw.ClampCap(days);
                JusticeText.Toast(NotificationType.Info, "reallife_justice_test_soon", 6f);
                Logger?.Info("Justice test: a prison term of " + JusticeState.PrisonTotal + " days.");
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        /// <summary>Test: a three-day term picked up on the morning the parole board sits (its last), with behaviour that makes the answer matter.</summary>
        public void TestParole()
        {
            try
            {
                JusticeRefusal why = Free();
                if (why == JusticeRefusal.None && JusticeSettings.NoPrison)
                {
                    why = JusticeRefusal.PartOff;
                }
                if (why != JusticeRefusal.None)
                {
                    JusticeRefusals.Tell(Logger, "Parole hearing", why);
                    return;
                }
                JusticeState.ClearPrison();
                JusticeState.PrisonTotal = JusticeState.PrisonOriginal = 3;
                JusticeState.PrisonServed = JusticeLaw.ParoleDay(3) - 1;
                JusticeState.PrisonBehaviour = 1 + Justice.Priors;   // honest gets out, silent does not, flattery is a coin
                JusticeText.Toast(NotificationType.Info, "reallife_justice_test_soon", 6f);
                Logger?.Info("Justice test: parole hearing (a 3-day term, day " + (JusticeState.PrisonServed + 1) + ").");
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        /// <summary>Test: somebody sues over a crash with an injury.</summary>
        public void TestCivil()
        {
            try
            {
                JusticeRefusal why = Ready();
                if (why == JusticeRefusal.None && !JusticeSettings.Civil)
                {
                    why = JusticeRefusal.PartOff;
                }
                if (why == JusticeRefusal.None && JusticeState.Suits.Count > 0)
                {
                    why = JusticeRefusal.Busy;
                }
                if (why != JusticeRefusal.None)
                {
                    JusticeRefusals.Tell(Logger, "Civil suit", why);
                    return;
                }
                Sue(2400, 1, 0, TimeHelper.CurrentDay);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        /// <summary>Test: four hours of community service owed (if none are), and a shift begun where the player stands.</summary>
        public void TestCommunity()
        {
            try
            {
                JusticeRefusal why = Free();
                if (why != JusticeRefusal.None)
                {
                    JusticeRefusals.Tell(Logger, "Community service", why);
                    return;
                }
                bool lent = JusticeState.CommunityHours == 0;
                if (lent)
                {
                    JusticeState.CommunityHours = JusticeLaw.CommunityShiftHours;
                    JusticeState.CommunityDue = TimeHelper.CurrentDay + JusticeLaw.CommunityDeadlineDays;
                }
                if (StartCommunity(announceRefusal: true) != JusticeRefusal.None && lent)
                {
                    JusticeState.CommunityHours = 0;
                    JusticeState.CommunityDue = 0;
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        /// <summary>Test: the Legal file, as soon as the menu is closed.</summary>
        public void TestFile()
        {
            JusticeRefusal why = Free();
            if (why != JusticeRefusal.None)
            {
                JusticeRefusals.Tell(Logger, "Legal file", why);
                return;
            }
            QueueFile();
            JusticeText.Toast(NotificationType.Info, "reallife_justice_test_soon", 6f);
        }

        /// <summary>Test: every case, debt, sentence and record entry is forgotten.</summary>
        public void TestClear()
        {
            try
            {
                if (!JusticeState.Loaded)
                {
                    JusticeRefusals.Tell(Logger, "Clear everything", JusticeRefusal.NotLoaded);
                    return;
                }
                if (_routine)
                {
                    JusticeRefusals.Tell(Logger, "Clear everything", JusticeRefusal.Busy);
                    return;
                }
                Letgo();
                JusticeState.Clear();
                _lastUnlicensedDay = 0;
                try
                {
                    Justice.Bridges.Licences.SuspendDriving(0);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                JusticeText.Toast(NotificationType.Success, "reallife_justice_test_cleared", 8f);
                Logger?.Info("Justice test: everything cleared.");
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }
    }
}
