using System;
using System.Collections;
using System.Text;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Helpers;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Custody and prison as time passing, never anywhere the game does not know: a save made at any moment loads without the mod.</summary>
    public sealed partial class PrisonStay
    {
        private const float SkipMost = 300f;       // real seconds one time skip may take before it is stopped
        private const float NeedsFloor = 55f;      // energy and hunger are not let below this while inside
        private const int Morning = 7;             // hour of the day

        private readonly JusticeDirector _director;
        private readonly JusticeHold _hold;
        private readonly IModLogger _logger;
        private readonly StringBuilder _text = new StringBuilder(400);

        private bool _waiting;          // a page is up and the routine waits for the last answer of the morning
        private bool _paroled;
        private string _note;           // said at the top of the next morning's page
        private string _dayText;        // what the day's choices came to, shown before the event
        private VehicleInstance _leftBehind;

        private int _summaryServed, _summaryOriginal, _summaryRemitted, _summaryEarned, _summaryStudied, _summaryExercised, _summaryProbation;
        private bool _summaryParoled;

        public PrisonStay(JusticeDirector director, JusticeHold hold, IModLogger logger)
        {
            _director = director;
            _hold = hold;
            _logger = logger;
        }

        /// <summary>True from the moment a custody or a term begins until the player is free again.</summary>
        public bool Running { get; private set; }

        /// <summary>Hours in a cell as a time skip; <paramref name="toCourt"/> brings a player on foot to the courthouse first.</summary>
        public IEnumerator Custody(int hours, bool toCourt, Action done)
        {
            Running = true;
            hours = Mathf.Clamp(hours, 1, JusticeLaw.CustodyCapHours);
            try
            {
                _hold.Beat();
                yield return _hold.Fade("reallife_justice_custody_fade");
                // At the wheel they stay with their car: only a prison term leaves a car behind, and brings it back.
                var gm = InstanceBehavior<GameManager>.Instance;
                if (toCourt && gm != null && gm.selectedVehicle == null && !PlayerHelper.IsUsingVehicle)
                {
                    yield return TakeOffTheStreet();
                }
                _hold.Block();
                Timestamp release = TimeHelper.Now();
                release.AddDays(hours / 24);   // the game's AddHours carries over one day only
                release.AddHours(hours % 24);
                yield return Skip(release, "reallife_justice_custody_info");
                Needs(70f, 70f);
                _hold.Rest(false);
                yield return _hold.Unfade();
                if (SaveGameManager.Current != null)
                {
                    ModState.AddModifier(ModState.MoodDown);
                    JusticeText.Toast(NotificationType.Info, JusticeLaw.CustodyReleasedKey(GoingPublic.ClockShared), "hours", JusticeText.N(hours), 10f);
                }
                _logger?.Info("Justice: " + hours + " h of custody served.");
            }
            finally
            {
                Running = false;
                _hold.ReleaseAll(_director);
            }
            done?.Invoke();
        }

        /// <summary>The term in JusticeState, from the morning it has reached, until the player is released.</summary>
        public IEnumerator Serve()
        {
            Running = true;
            _paroled = false;
            _note = null;
            bool released = false;
            try
            {
                _hold.Beat();
                bool fresh = JusticeState.PrisonServed == 0;
                yield return _hold.Fade(fresh ? "reallife_justice_prison_fade" : "reallife_justice_prison_fade_resume");
                yield return TakeOffTheStreet();
                if (_leftBehind != null && string.IsNullOrEmpty(JusticeState.PrisonCar))
                {
                    JusticeState.PrisonCar = _leftBehind.id ?? string.Empty;
                }
                _logger?.Info("Justice: prison, day " + (JusticeState.PrisonServed + 1) + " of " + JusticeState.PrisonTotal + ".");

                while (JusticeState.InPrison && JusticeState.PrisonServed < JusticeState.PrisonTotal && !_paroled)
                {
                    _hold.Pause();
                    _hold.Block();
                    _waiting = true;
                    ShowMorning();
                    while (_waiting)
                    {
                        _hold.Beat();
                        if (!JusticePanel.IsShowing)
                        {
                            throw new InvalidOperationException("the prison panel went away while a day was being decided");
                        }
                        yield return null;
                    }
                    JusticePanel.Instance.Close();
                    if (_paroled)
                    {
                        break;
                    }
                    Timestamp next = TimeHelper.Now();
                    if (next.Hour >= Morning)
                    {
                        next.AddHours(24);
                    }
                    next.Hour = Morning;
                    next.Minute = 0f;
                    yield return Skip(next, "reallife_justice_prison_skip");
                    JusticeState.PrisonServed++;
                    Remit();
                }

                Release();
                released = true;
                _hold.Pause();
                _waiting = true;
                ShowSummary();
                while (_waiting && JusticePanel.IsShowing)
                {
                    _hold.Beat();
                    yield return null;
                }
                if (JusticePanel.Exists)
                {
                    JusticePanel.Instance.Close();
                }
                _hold.Unpause();
                _hold.Unblock();
                yield return _hold.Unfade();
                yield return new WaitForSecondsRealtime(0.5f);
                ReturnCar();
            }
            finally
            {
                Running = false;
                if (!released && JusticeState.InPrison)
                {
                    // Cut short by a fault or by the scene going: the rest of the term is let go, and the summary is a toast.
                    _logger?.Warn("Justice: the prison term ended early (" + JusticeState.PrisonServed + " of " + JusticeState.PrisonTotal + " days); the player is released.");
                    try
                    {
                        if (JusticePanel.Exists)
                        {
                            JusticePanel.Instance.Close();
                        }
                        Release();
                        JusticeText.Toast(NotificationType.Info, "reallife_justice_prison_released_early", 10f);
                    }
                    catch (Exception e)
                    {
                        _logger?.Error(e);
                        JusticeState.ClearPrison();
                    }
                }
                _hold.ReleaseAll(_director);
            }
        }

        private void Remit()
        {
            int earned = JusticeLaw.Remission(JusticeState.PrisonBehaviour, JusticeState.PrisonOriginal) - JusticeState.PrisonRemitted;
            if (earned > 0 && JusticeState.PrisonTotal - earned >= JusticeState.PrisonServed)
            {
                JusticeState.PrisonTotal -= earned;
                JusticeState.PrisonRemitted += earned;
                _note = JusticeText.T("reallife_justice_prison_remitted", "days", JusticeText.N(earned));
                _logger?.Info("Justice: " + earned + " day(s) off for good behaviour; the term is now " + JusticeState.PrisonTotal + ".");
            }
        }

        // Settled in one go before anything is shown: after this the save no longer says "in prison".
        private void Release()
        {
            int today = TimeHelper.CurrentDay;
            _summaryServed = JusticeState.PrisonServed;
            _summaryOriginal = JusticeState.PrisonOriginal;
            _summaryRemitted = JusticeState.PrisonRemitted;
            _summaryEarned = JusticeState.PrisonEarned;
            _summaryStudied = JusticeState.PrisonStudied;
            _summaryExercised = JusticeState.PrisonExercised;
            _summaryParoled = _paroled;
            int left = Mathf.Max(0, JusticeState.PrisonTotal - JusticeState.PrisonServed);
            JusticeCase c = JusticeState.Find(JusticeState.PrisonCase);
            int probation = Mathf.Max(5, c != null ? c.ProbationImposed : 5) + (_paroled ? left * 2 : 0);
            probation = Mathf.Min(JusticeLaw.ProbationCapDays, probation);
            JusticeState.ProbationUntil = Mathf.Max(JusticeState.ProbationUntil, today + probation);
            _summaryProbation = JusticeState.ProbationUntil;
            if (_summaryEarned > 0 && SaveGameManager.Current != null)
            {
                Crime.Give(_summaryEarned, "reallife_transaction_prison_work");
            }
            Needs(_summaryExercised > 0 ? 100f : 75f, 85f);
            ModState.AddModifier(_summaryExercised > 0 ? ModState.MoodUp : ModState.MoodDown);
            string car = JusticeState.PrisonCar;
            JusticeState.ClearPrison();
            JusticeState.PrisonCar = car;   // kept until the car has been seen to (ReturnCar)
            _logger?.Info("Justice: released from prison after " + _summaryServed + " of " + _summaryOriginal + " days" + (_summaryParoled ? " (parole)" : string.Empty)
                          + ", earned " + _summaryEarned + ", probation to day " + _summaryProbation + ".");
        }

        private void ShowSummary()
        {
            _text.Length = 0;
            _text.Append(JusticeText.T(_summaryParoled ? "reallife_justice_release_parole" : "reallife_justice_release_served",
                "served", JusticeText.N(_summaryServed), "days", JusticeText.N(_summaryOriginal)));
            if (_summaryRemitted > 0)
            {
                _text.Append(' ').Append(JusticeText.T("reallife_justice_release_remitted", "days", JusticeText.N(_summaryRemitted)));
            }
            _text.Append('\n');
            if (_summaryEarned > 0)
            {
                _text.Append('\n').Append(JusticeText.T("reallife_justice_release_earned", "amount", JusticeText.Money(_summaryEarned)));
            }
            if (_summaryStudied > 0)
            {
                _text.Append('\n').Append(JusticeText.T("reallife_justice_release_studied", "days", JusticeText.N(_summaryStudied)));
            }
            if (_summaryExercised > 0)
            {
                _text.Append('\n').Append(JusticeText.T("reallife_justice_release_exercised", "days", JusticeText.N(_summaryExercised)));
            }
            _text.Append('\n').Append(JusticeText.T("reallife_justice_release_probation", "day", JusticeText.N(_summaryProbation)));
            _text.Append('\n').Append(JusticeText.T(string.IsNullOrEmpty(JusticeState.PrisonCar) ? "reallife_justice_release_no_car" : "reallife_justice_release_car"));
            _text.Append("\n\n").Append(JusticeText.T("reallife_justice_release_where", "place", _director.ReleasePlaceName()));
            var page = new JusticePage
            {
                Scene = JusticeScene.Office, Title = JusticeText.T("reallife_justice_release_title"),
                Subtitle = JusticeText.T("reallife_justice_release_subtitle", "day", JusticeText.N(TimeHelper.CurrentDay)),
                Body = _text.ToString()
            };
            page.Add(JusticeText.T("reallife_justice_release_walk"));
            JusticePanel.Instance.Show(page, _ => _waiting = false);
        }

        private void ReturnCar()
        {
            string id = JusticeState.PrisonCar;
            JusticeState.PrisonCar = string.Empty;
            if (string.IsNullOrEmpty(id) || BuildingManager.IsInsideBuilding || SaveGameManager.Current == null)
            {
                return;
            }
            try
            {
                VehicleInstance car = null;
                foreach (VehicleController vehicle in VehicleHelper.AllPlayerVehicles)
                {
                    if (vehicle != null && vehicle.vehicleInstance != null && vehicle.vehicleInstance.id == id && GoingPublic.MineVehicle(vehicle))
                    {
                        car = vehicle.vehicleInstance;
                        break;
                    }
                }
                if (car == null)
                {
                    _logger?.Info("Justice: the car left behind is no longer on the street; nothing to return.");
                    return;
                }
                Vector3 near = PlayerHelper.GetPosition();
                if (Justice.Bridges.Transport.ReturnCar(car, near, _summaryServed))
                {
                    return;
                }
                int storage = JusticeLaw.Bound(Prices.At(25) * Mathf.Max(1, _summaryServed), Crime.Cash, 0.10f, 0);
                storage = Mathf.Min(storage, 300);
                if (storage > 0)
                {
                    RealLifeOverhaul.Economy.BizBooks.Book(-storage, new TransactionInfo("reallife_transaction_impound"), null, null, force: true);
                    JusticeText.Toast(NotificationType.Info, "reallife_justice_release_impound", "amount", JusticeText.Money(storage), 10f);
                }
                VehicleTow.BringNear(car, near);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // Out of the car and any building, and down where they will be let go. Every wait has an end; what cannot be done is skipped.
        private IEnumerator TakeOffTheStreet()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            _leftBehind = null;
            if (gm == null || gm.playerController == null)
            {
                yield break;
            }
            if (gm.selectedVehicle != null)
            {
                _leftBehind = VehicleTow.LeftBehind(gm.selectedVehicle);   // their own only: another player's car stays where it stopped
                float waited = 0f;
                while (gm.selectedVehicle != null && gm.selectedVehicle.CurrentSpeed > 1f && waited < 8f)
                {
                    waited += Time.unscaledDeltaTime;
                    _hold.Beat();
                    yield return null;
                }
                if (gm.selectedVehicle != null)
                {
                    gm.selectedVehicle.ExitVehicle();
                    yield return new WaitForSecondsRealtime(0.5f);
                    // Leaving the car clears the saved active vehicle, but the selected one only goes when its HUD panel closes.
                    var hud = InstanceBehavior<UIs>.Instance;
                    if (gm.selectedVehicle != null && !PlayerHelper.IsUsingVehicle && hud != null && hud.playerHUD != null && hud.playerHUD.itemPanelUI != null)
                    {
                        hud.playerHUD.itemPanelUI.Toggle(false);
                    }
                }
            }
            var buildings = InstanceBehavior<BuildingManager>.Instance;   // read once: a gone instance is looked for again (a whole-scene search)
            if (BuildingManager.IsInsideBuilding && buildings != null)
            {
                buildings.ExitFromBuilding(0, playFadeAnimation: false);
                float waited = 0f;
                while ((BuildingManager.IsInsideBuilding || buildings.exitingBuilding) && waited < 10f)
                {
                    waited += Time.unscaledDeltaTime;
                    _hold.Beat();
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.3f);
            }
            else if (Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking)
            {
                Parking.UndergroundParking.UndergroundParkingManager.ExitParking(playFadeAnimation: false);   // as the game's own hospital trip does
                float waited = 0f;
                while (Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking && waited < 10f)
                {
                    waited += Time.unscaledDeltaTime;
                    _hold.Beat();
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.3f);
            }
            // Some ways out of a building (the Hamptons houses) fade back in by themselves: dark again before anything is seen.
            if (_hold.Faded && !UiFader.isFading)
            {
                yield return UiFader.Fade(0.2f);
            }
            _hold.Beat();
            if (PlayerHelper.IsUsingVehicle || gm.selectedVehicle != null)
            {
                _leftBehind = null;   // the game put them back at the wheel: no car was left anywhere
                yield break;
            }
            if (BuildingManager.IsInsideBuilding || Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking || !_director.ReleaseSpot(out Vector3 spot))
            {
                yield break;   // stays where they are: the time passes all the same
            }
            PlayerController player = gm.playerController;
            if (player.Character != null && player.Character.navmeshAgent != null && player.Character.navmeshAgent.isOnNavMesh)
            {
                player.ResetNavigation();
                if (player.Character.navmeshAgent.Warp(spot))
                {
                    _logger?.Info("Justice: the player is put down at " + _director.ReleasePlaceName() + ".");
                }
            }
        }

        // Energy and hunger are held up while the clock runs. It ends a moment after: the game puts its interface back then.
        private IEnumerator Skip(Timestamp until, string infoKey)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.timeMachine == null || SaveGameManager.Current == null || until.GetTotalMinutes() <= TimeHelper.NowInMinutes() + 1f)
            {
                yield break;
            }
            if (GoingPublic.ClockShared)
            {
                yield break;   // a Going Public clock is everyone's: the stretch is served at once
            }
            _hold.Rest(true);
            _hold.PauseHandedOver();
            uis.timeMachine.StartTimeMachine(until, disableCancel: true, infoKey);
            float waited = 0f, nextNeeds = 0f;
            while (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning && waited < SkipMost)
            {
                waited += Time.unscaledDeltaTime;
                _hold.Beat();
                if (waited >= nextNeeds)
                {
                    nextNeeds = waited + 0.25f;
                    var save = SaveGameManager.Current;
                    if (save != null)
                    {
                        if (save.Energy < NeedsFloor)
                        {
                            save.Energy = NeedsFloor;
                        }
                        if (save.Hunger < NeedsFloor)
                        {
                            save.Hunger = NeedsFloor;
                        }
                    }
                }
                yield return null;
            }
            if (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning)
            {
                _logger?.Warn("Justice: a time skip took over " + SkipMost + " s and was stopped.");
                uis.timeMachine.StopTimeMachine(0f);
            }
            // At least 1.3 s, and until the game's own stop routine has finished (it resets the game speed last); 4 s at most.
            float settle = 0f;
            while (settle < 1.3f || (settle < 4f && uis != null && uis.timeMachine != null && uis.timeMachine.isBlockingUi))
            {
                settle += Time.unscaledDeltaTime;
                _hold.Beat();
                yield return null;
            }
        }

        private static void Needs(float energy, float hunger)
        {
            var save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            if (save.Energy < energy)
            {
                save.Energy = energy;   // as the game's own hospital stay sets it
            }
            if (save.Hunger < hunger)
            {
                EnergyHelper.SetCurrentHunger(hunger);
            }
        }
    }
}
