using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using Extensions;
using Helpers;
using PlayerActivity;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Random life events (rolled here, dealt by <see cref="LifeEventDeck"/>), sickness and the shared hospital trip. Also
    /// loads ModState and keeps the save hook, as the component that knows when a save is ready.
    /// Cost: per frame a few comparisons; the roll once a game hour, the hook check every two seconds.
    /// </summary>
    public class LifeEvents : MonoBehaviour
    {
        private const string SickEnergyDrain = "reallife_sick";
        private const float WakingHoursPerWeek = 112f;   // hours: sixteen a day
        public static int DoctorFee => Prices.At(150);
        public static int AmbulanceFee => Prices.At(400);

        public static IModLogger Logger;
        public static LifeEvents Instance { get; private set; }

        private int _lastHour = -1;
        private float _nextHookCheck;
        private bool _sickDrainActive;

        private void Awake()
        {
            Instance = this;
            ModState.Reset();
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            SetSickDrain(false);
            ModState.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Life: LifeEvents.Update");

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading)
            {
                return;
            }

            // The game clears its event hub on every load. Kept whatever is off: the mod's moods must leave the save. Joined before
            // the load below, so that a load that fails halfway (and stops this component) still has the moods it put into the
            // game's list taken out at every save: a save that holds one cannot be loaded again.
            if (Time.unscaledTime >= _nextHookCheck)
            {
                _nextHookCheck = Time.unscaledTime + 2f;
                SaveHook.KeepEssential(_onSave ?? (_onSave = OnSaveGame));
            }

            if (!ModState.Loaded)
            {
                try
                {
                    ModState.Load();
                    LifeMoneyRepair.OnLoad();   // a charge a purse past an int once made is paid back, once (LifeMoneyRules)
                    PhoneContacts.Setup();
                    ContactGroupsGame.Install();   // after every contact the mod adds at a load is made: the groups of the Contacts app
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    enabled = false;
                    return;
                }
                _lastHour = TimeHelper.CurrentHour;
            }

            if (TimeHelper.CurrentHour != _lastHour)
            {
                _lastHour = TimeHelper.CurrentHour;
                if (_hoursOff)
                {
                    return;
                }
                try
                {
                    OnNewHour();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    // A game that lacks something the hour names would fail every hour: the hour's work stops for the
                    // session, the save hook and the mod's state go on.
                    _hoursOff = Guard.StandsDown(e, "life events and the end of a sickness on the hour");
                }
            }
        }

        private bool _hoursOff;    // the whole hour's work stood down
        private bool _eventsOff;   // the events' roll stood down; the sickness's end on the hour goes on

        private Action _onSave;    // made once: SaveHook compares it

        private void OnSaveGame()
        {
            try
            {
                List<HappinessModifierData> removed = ModState.BeforeSave();
                if (removed.Count > 0)
                {
                    StartCoroutine(RestoreAfterSave(removed));   // not behind a feature's guard: every feature's moods go back whatever is off
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The save is serialised synchronously right after the save event, so one frame later is safe.
        private static IEnumerator RestoreAfterSave(List<HappinessModifierData> removed)
        {
            yield return null;
            ModState.AfterSave(removed);
        }

        private void OnNewHour()
        {
            if (ModState.SickUntil > 0f && !ModState.IsSick)
            {
                Cure("reallife_event_recovered");
            }
            SetSickDrain(ModState.IsSick);

            if (_eventsOff || !Settings.LifeEvents)
            {
                return;
            }
            try
            {
                if (IsSafeForEvent() && Random.value < Settings.EventFrequency / WakingHoursPerWeek)
                {
                    FireRandomEvent();
                }
            }
            catch (Exception e) when (Guard.StandsDown(e, "life events"))
            {
                _eventsOff = true;   // a game that lacks something an event names would fail every hour
            }
        }

        /// <summary>
        /// True when nothing else is demanding the player's attention. A paused game counts as busy unless
        /// <paramref name="pausedIsFine"/>: an event must not start while the clock stands, but the player may open a list.
        /// </summary>
        public static bool IsSafeForEvent(bool pausedIsFine = false)
        {
            var uis = InstanceBehavior<UIs>.Instance;
            return uis != null && !LoadScene.isLoading && !GameMembers.PlayerDead && !EnergyHelper.goingToHospital
                   && !uis.timeMachine.isRunning && !PlayerActivityUI.IsPanelOpen && !HudConfirm.isOpen
                   && DialogController.current == null && !CasinoBoatManager.IsOnCasinoBoat && !SubwaySystem.IsRiding
                   && (pausedIsFine || !uis.gameSpeed.Paused);
        }

        /// <summary>Options-menu test button: fires one event right away, ignoring the hourly roll.</summary>
        public void TriggerEventNow()
        {
            if (!ModState.Loaded)
            {
                return;
            }
            try
            {
                FireRandomEvent();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // LifeEventDeck keeps a save from seeing one twice.
        private void FireRandomEvent()
        {
            LifeContext context = LifeEventRunner.ReadContext();
            LifeEvent picked = LifeEventDeck.Draw(LifeEventCatalogue.All, context, ModState.SeenEvents, ModState.LastEventId,
                () => Random.value);
            if (picked == null)
            {
                return;
            }
            ModState.LastEventId = picked.Id;
            Logger?.Info($"Life event: {picked.Id} ({ModState.SeenEvents.Count}/{LifeEventCatalogue.All.Length} seen)");
            LifeEventRunner.Run(picked);
        }

        private static void Notify(NotificationType type, string key, int amount = 0)
        {
            var data = new Dictionary<string, string> { { "amount", ((float)amount).ToCurrencyFormat() } };
            Notifications.Show(type, key, data, 8f);
        }

        internal static void ChangeMoney(int amount, string transactionKey)
        {
            GameManager.ChangeMoneySafe(amount, new TransactionInfo(transactionKey), null, null, force: true);
        }

        /// <param name="notificationKey">Null when the caller says it itself (Home/HomeHealth names the illness).</param>
        public void MakeSick(int hours, string notificationKey = "reallife_event_sick")
        {
            ModState.SickUntil = TimeHelper.NowInMinutes() + hours * 60f;
            ModState.AddModifier(ModState.Sick, hours);
            SetSickDrain(true);
            if (notificationKey != null)
            {
                Notify(NotificationType.Warning, notificationKey);
            }
        }

        public void Injure(int hours, float energyLoss, string notificationKey)
        {
            ModState.AddModifier(ModState.Injured, hours);
            EnergyHelper.SpentEnergyOnce(energyLoss);
            Notify(NotificationType.Error, notificationKey);
        }

        public void Cure(string notificationKey)
        {
            bool wasSick = ModState.SickUntil > 0f;
            ModState.SickUntil = 0f;
            ModState.RemoveModifier(ModState.Sick);
            SetSickDrain(false);
            if (wasSick && notificationKey != null)
            {
                Notify(NotificationType.Success, notificationKey);
            }
        }

        // Being sick tires the player faster. The game does not save energy drains, so this is applied again on load.
        private void SetSickDrain(bool active)
        {
            if (active == _sickDrainActive)
            {
                return;
            }
            _sickDrainActive = active;
            EnergyHelper.RemoveEnergySpender(SickEnergyDrain);
            if (active)
            {
                EnergyHelper.AddEnergySpender(SickEnergyDrain, 0.03f);   // energy a game minute
            }
        }

        /// <summary>The ambulance: out of the vehicle, the fee charged, then the game's own hospital stay with its own bill.</summary>
        public void SendToHospital()
        {
            StartCoroutine(Guard.Routine(HospitalRoutine()));
        }

        private IEnumerator HospitalRoutine()
        {
            var gm = InstanceBehavior<GameManager>.Instance;

            // The car being driven is brought to the hospital afterwards: the player's own only, never another Going
            // Public player's.
            VehicleInstance leftBehind = VehicleTow.LeftBehind(gm.selectedVehicle);

            // After a crash the screen goes dark at once: the game's hospital sequence has the player climb out and
            // collapse in the road, which plays unseen.
            bool faded = false;
            if (gm.selectedVehicle != null)
            {
                yield return UiFader.Fade(0.7f, "reallife_event_blackout");
                faded = true;
            }

            // The game can only take the player out of a car that has stopped.
            float waited = 0f;
            while (gm.selectedVehicle != null && gm.selectedVehicle.CurrentSpeed > 1f && waited < 8f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (gm.selectedVehicle != null)
            {
                gm.selectedVehicle.ExitVehicle();
                yield return new WaitForSecondsRealtime(0.5f);

                // Leaving the car clears the saved active vehicle, but the selected one only when its HUD panel closes;
                // still set when the hospital loads, the game brings the car inside and duplicates it.
                var hud = InstanceBehavior<UIs>.Instance;
                if (gm.selectedVehicle != null && !PlayerHelper.IsUsingVehicle && hud != null && hud.playerHUD != null && hud.playerHUD.itemPanelUI != null)
                {
                    hud.playerHUD.itemPanelUI.Toggle(false);
                }
            }

            // Called from the phone, the ambulance arrives once the phone is put away.
            waited = 0f;
            float patience = faded ? 6f : 60f;   // never hold a black screen for long
            while ((PlayerHelper.IsUsingVehicle || !IsSafeForEvent()) && waited < patience)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (PlayerHelper.IsUsingVehicle || gm.selectedVehicle != null || !IsSafeForEvent())
            {
                Logger?.Warn("Hospital trip cancelled: the player is busy or still in a vehicle.");
                Notify(NotificationType.Warning, "reallife_event_ambulance_failed");
                if (faded)
                {
                    yield return UiFader.UnFade();
                }
                yield break;
            }
            if (!faded)
            {
                yield return UiFader.Fade(0.7f, "reallife_event_blackout");
            }

            Notify(NotificationType.Info, "reallife_event_ambulance", AmbulanceFee);
            ChangeMoney(-AmbulanceFee, "reallife_transaction_ambulance");
            EnergyHelper.goingToHospital = true;
            yield return gm.StartCoroutine(gm.HospitalRespawn());
            ModState.RemoveModifier(ModState.Injured);
            Cure(null);

            // The game leaves the player inside the hospital: out onto the street instead.
            var uis = InstanceBehavior<UIs>.Instance;
            while (uis != null && uis.hospitalizationNotification != null && uis.hospitalizationNotification.IsVisible)
            {
                yield return null;
            }
            // Read once, as the custody trip does: the game's instance is looked for again (a whole-scene search) when it is gone.
            var buildings = InstanceBehavior<BuildingManager>.Instance;
            if (BuildingManager.IsInsideBuilding && buildings != null)
            {
                buildings.ExitFromBuilding(0);
            }

            // Once the player is back on the street, their car is waiting in a space nearby.
            if (leftBehind != null)
            {
                float exitWait = 0f;
                while ((BuildingManager.IsInsideBuilding || (buildings != null && buildings.exitingBuilding)) && exitWait < 10f)
                {
                    exitWait += Time.unscaledDeltaTime;
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.5f);
                if (!BuildingManager.IsInsideBuilding)
                {
                    VehicleTow.BringNear(leftBehind, PlayerHelper.GetPosition());
                }
            }
        }
    }
}
