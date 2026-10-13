using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using Extensions;
using BigAmbitions.DayNightCycle;
using Entities;
using Helpers;
using Localizor;
using PlayerActivity;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Random life events (rolled here, dealt from LifeEventCatalogue by LifeEventDeck), the
    /// sickness mechanic and the shared hospital trip. Also owns loading/saving of ModState,
    /// because it is the component that knows when a savegame is ready.
    /// </summary>
    public class LifeEvents : MonoBehaviour
    {
        private const string SickEnergyDrain = "reallife_sick";
        private const float WakingHoursPerWeek = 112f;
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

            if (!ModState.Loaded)
            {
                try
                {
                    ModState.Load();
                    PhoneContacts.Setup();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    enabled = false;
                    return;
                }
                _lastHour = TimeHelper.CurrentHour;
            }

            // The game clears its event hub on every load, so keep our save hook attached.
            if (Time.unscaledTime >= _nextHookCheck)
            {
                _nextHookCheck = Time.unscaledTime + 2f;
                SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // the mod's one relay in the game's save event (Core/SaveHook): nothing is allocated while that event has not changed
            }

            if (TimeHelper.CurrentHour != _lastHour)
            {
                _lastHour = TimeHelper.CurrentHour;
                try
                {
                    OnNewHour();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        private System.Action _onSave;   // made once: Core/SaveHook compares it, it is never built again

        private void OnSaveGame()
        {
            try
            {
                List<HappinessModifierData> removed = ModState.BeforeSave();
                if (removed.Count > 0)
                {
                    StartCoroutine(RestoreAfterSave(removed));
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

            if (!Settings.LifeEvents || !IsSafeForEvent())
            {
                return;
            }
            if (Random.value < Settings.EventFrequency / WakingHoursPerWeek)
            {
                FireRandomEvent();
            }
        }

        /// <summary>True when nothing else is demanding the player's attention.</summary>
        public static bool IsSafeForEvent()
        {
            var uis = InstanceBehavior<UIs>.Instance;
            return uis != null && !LoadScene.isLoading && !FuneralHelper.PlayerDead && !EnergyHelper.goingToHospital
                   && !uis.timeMachine.isRunning && !PlayerActivityUI.IsPanelOpen && !HudConfirm.isOpen
                   && DialogController.current == null && !CasinoBoatManager.IsOnCasinoBoat && !SubwaySystem.IsRiding
                   && !uis.gameSpeed.Paused;
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

        // Deals the next event from the catalogue; LifeEventDeck keeps a save from seeing one twice.
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

        /// <param name="notificationKey">What to say; null when whoever calls says it themselves (Home/HomeHealth names the illness).</param>
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

        // Being sick tires the player out faster. The game doesn't save energy drains, so this is re-applied on load.
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
                EnergyHelper.AddEnergySpender(SickEnergyDrain, 0.03f);
            }
        }

        /// <summary>
        /// Ambulance trip: leaves the vehicle if needed, charges the ambulance fee and runs the
        /// game's own hospital stay (which bills its own fee and skips to the next morning).
        /// </summary>
        public void SendToHospital()
        {
            StartCoroutine(HospitalRoutine());
        }

        private IEnumerator HospitalRoutine()
        {
            var gm = InstanceBehavior<GameManager>.Instance;

            // Remember the car being driven, so it can be brought to the hospital afterwards: the player's own only (in a
            // Going Public session the car may be another player's, and that one is left where it stopped).
            VehicleInstance leftBehind = VehicleTow.LeftBehind(gm.selectedVehicle);

            // After a crash the screen goes dark straight away: the game's own hospital sequence makes the
            // player climb out and collapse in the road, which looks wrong, so it plays unseen.
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

                // The game tracks "driving" in two places. Leaving the car clears the saved active vehicle,
                // but the selected vehicle is only cleared when its HUD panel closes; if it is still set
                // when the hospital loads, the game brings the car inside with the player and duplicates it.
                var hud = InstanceBehavior<UIs>.Instance;
                if (gm.selectedVehicle != null && !PlayerHelper.IsUsingVehicle && hud != null && hud.playerHUD != null && hud.playerHUD.itemPanelUI != null)
                {
                    hud.playerHUD.itemPanelUI.Toggle(false);
                }
            }

            // When called from the phone, the ambulance arrives once the player has put the phone away.
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

            // The game leaves the player inside the hospital; discharge them onto the street instead.
            var uis = InstanceBehavior<UIs>.Instance;
            while (uis != null && uis.hospitalizationNotification != null && uis.hospitalizationNotification.IsVisible)
            {
                yield return null;
            }
            if (BuildingManager.IsInsideBuilding)
            {
                InstanceBehavior<BuildingManager>.Instance.ExitFromBuilding(0);
            }

            // Once the player is back on the street, their car is waiting in a space nearby.
            if (leftBehind != null)
            {
                float exitWait = 0f;
                while ((BuildingManager.IsInsideBuilding || InstanceBehavior<BuildingManager>.Instance.exitingBuilding) && exitWait < 10f)
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
