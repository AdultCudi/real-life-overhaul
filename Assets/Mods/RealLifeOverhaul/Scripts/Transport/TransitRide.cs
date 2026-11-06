using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.DayNightCycle;
using Helpers;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What the save remembers of the player's use of public transport: one line in modData.</summary>
    public static class TransitState
    {
        public static float BusFare => Prices.At(2.75f);
        public static float DayPass => Prices.At(7.5f);       // unlimited buses and subway until midnight

        private const string Key = "reallife:transit";

        public static bool Loaded { get; private set; }
        public static int PassDay;       // the day a pass was bought for; 0 for none
        public static int BusRides;
        public static int SubwayRides;
        public static int ToldDelay = -1;     // the three-hour window whose delays were last sent to the phone
        public static int ToldClosure = -1;   // no station closes any more; kept so the save line keeps its shape
        public static float FareBack;         // from a save written during a bus ride (BusRider): given back once the city is up
        public static double TransferUntil;   // game minutes (the game's total) until which a bus is boarded on the fare paid: a change

        /// <summary>True during a journey's change of bus: the next bus is boarded on the fare already paid.</summary>
        public static bool HasTransfer => TransferUntil > 0.0 && TimeHelper.NowInMinutes() < TransferUntil;

        public static bool HasPass => PassDay > 0 && PassDay == TimeHelper.CurrentDay;

        public static void Reset()
        {
            Loaded = false;
            PassDay = 0;
            BusRides = 0;
            SubwayRides = 0;
            ToldDelay = -1;
            ToldClosure = -1;
            FareBack = 0f;
            TransferUntil = 0.0;
        }

        public static void Load(Dictionary<string, string> data)
        {
            Reset();
            if (data != null && data.TryGetValue(Key, out string text) && !string.IsNullOrEmpty(text))
            {
                string[] parts = text.Split('|');
                if (parts.Length >= 3)
                {
                    int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out PassDay);
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out BusRides);
                    int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out SubwayRides);
                    if (parts.Length >= 5)
                    {
                        int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out ToldDelay);
                        int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out ToldClosure);
                    }
                    if (parts.Length >= 6 && float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float back))
                    {
                        FareBack = back > 0f && back < 1000f ? back : 0f;
                    }
                    if (parts.Length >= 7 && double.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out double transfer))
                    {
                        TransferUntil = transfer > 0.0 ? transfer : 0.0;
                    }
                }
            }
            Loaded = true;
        }

        /// <summary>Writes the line, with a fare to give back when loaded (<paramref name="fareBack"/>, a ride under way; 0 for none).</summary>
        public static void Save(Dictionary<string, string> data, float fareBack = 0f)
        {
            if (Loaded && data != null)
            {
                data[Key] = PassDay.ToString(CultureInfo.InvariantCulture) + "|" + BusRides.ToString(CultureInfo.InvariantCulture) + "|"
                            + SubwayRides.ToString(CultureInfo.InvariantCulture) + "|" + ToldDelay.ToString(CultureInfo.InvariantCulture) + "|"
                            + ToldClosure.ToString(CultureInfo.InvariantCulture)
                            + (fareBack > 0f || HasTransfer ? "|" + Math.Max(0f, fareBack).ToString("F2", CultureInfo.InvariantCulture) : string.Empty)
                            + (HasTransfer ? "|" + TransferUntil.ToString("F1", CultureInfo.InvariantCulture) : string.Empty);   // a seventh part: older versions read six
            }
        }

        /// <summary>Pays a bus fare, nothing with a pass or on a change. False, with a toast, if the player cannot pay.</summary>
        public static bool PayBus(out bool free)
        {
            free = HasPass || HasTransfer;
            if (HasTransfer)
            {
                TransferUntil = 0.0;   // the change's one boarding
                return true;
            }
            return Pay(BusFare, "reallife_transaction_bus_fare");
        }

        /// <summary>Takes a fare, or nothing with a pass. False, with a toast, if the player cannot pay.</summary>
        public static bool Pay(float fare, string transaction)
        {
            if (HasPass)
            {
                return true;
            }
            var info = new TransactionInfo(transaction);
            if (RealLifeOverhaul.Economy.BizBooks.Book(-fare, info, null, null, force: false, showNotification: true))
            {
                return true;
            }
            Notifications.ShowInsufficientMoney();
            return false;
        }

        public static bool BuyPass()
        {
            if (HasPass)
            {
                return true;
            }
            if (!RealLifeOverhaul.Economy.BizBooks.Book(-DayPass, new TransactionInfo("reallife_transaction_transit_pass"), null, null, force: false, showNotification: true))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            PassDay = TimeHelper.CurrentDay;
            return true;
        }
    }

    /// <summary>
    /// The time a ride takes, for the bus and the subway, as the game's taxi does it: the screen fades, the player steps out of
    /// the world, the time machine runs the clock forward and they are put down at the far end. Every way out puts them back.
    /// </summary>
    public sealed class TransitRide
    {
        private readonly MonoBehaviour _host;
        private PlayerController _player;
        private bool _hidden;
        private bool _blocked;
        private bool _faded;

        public TransitRide(MonoBehaviour host)
        {
            _host = host;
        }

        public bool Riding { get; private set; }

        /// <summary>True when no menu, dialog, time skip, hospital trip or other journey stands in the way of a ride.</summary>
        public static bool CanStart()
        {
            return LifeEvents.IsSafeForEvent() && !ThirdPersonCamera.IsUiOpen() && !TaxiSystem.IsTraveling && !PlayerHelper.IsUsingVehicle
                   && !BuildingManager.IsInsideBuilding && !GameManager.IsAnyMiniGameActive() && !UiFader.isFading;
        }

        /// <summary>
        /// Starts a ride of <paramref name="minutes"/> game minutes to <paramref name="arrive"/>. <paramref name="atBlack"/> then
        /// <paramref name="onFaded"/> run once the screen is black; <paramref name="arriveAboard"/> (true if it placed the player)
        /// then <paramref name="onArrived"/> at the far end before it clears.
        /// </summary>
        public void Begin(float minutes, Vector3 arrive, string fadeKey, Action onFaded, Action onArrived, bool tap = true, Action atBlack = null, Func<bool> arriveAboard = null)
        {
            if (Riding || _host == null || !_host.isActiveAndEnabled)
            {
                return;
            }
            Riding = true;
            _host.StartCoroutine(Guard.Routine(Run(minutes, arrive, fadeKey, onFaded, onArrived, tap, atBlack, arriveAboard)));
        }

        private IEnumerator Run(float minutes, Vector3 arrive, string fadeKey, Action onFaded, Action onArrived, bool tap, Action atBlack, Func<bool> arriveAboard)
        {
            var game = InstanceBehavior<GameManager>.Instance;
            UIs uis = InstanceBehavior<UIs>.Instance;
            _player = game != null ? game.playerController : null;
            if (_player == null || uis == null || uis.timeMachine == null)
            {
                Riding = false;
                yield break;
            }
            Vector3 origin = _player.transform.position;
            bool arrived = false;
            try
            {
                _player.ResetNavigation();
                _player.SetNavigationBlocker(NavigationBlocker.Taxi);
                _blocked = true;
                // The card on the reader first: the tap lands 0.7 s in.
                float tapped = Time.unscaledTime + (tap ? 1f : 0f);
                while (Time.unscaledTime < tapped)
                {
                    yield return null;
                }
                _faded = true;
                yield return UiFader.Fade(0.8f, fadeKey);
                Call(atBlack);
                _player.Character.ToggleVisibility(show: false);
                _hidden = true;
                Call(onFaded);

                Timestamp until = TimeHelper.Now();
                until.AddMinutes(Mathf.Clamp(minutes, 1f, 240f));
                if (!GoingPublic.ClockShared)
                {
                    // In a Going Public session the ride takes no time, as Going Public's own taxi ride.
                    uis.timeMachine.StartTimeMachine(until, disableCancel: true, fadeKey);
                }
                float waited = 0f;
                while (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning && waited < 120f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (arriveAboard == null)
                {
                    _player.Character.WarpSafely(arrive);
                }
                arrived = true;
                _player.Character.ToggleVisibility(show: true);
                _hidden = false;
                _player.UnsetNavigationBlocker(NavigationBlocker.Taxi);
                _blocked = false;
                _player.Character.Reset();
                if (arriveAboard != null && !Aboard(arriveAboard))
                {
                    _player.Character.WarpSafely(arrive);   // in the world again, so the agent is there to be warped
                }
                Call(onArrived);
                _faded = false;
                yield return UiFader.UnFade();
            }
            finally
            {
                Restore(arrived ? (Vector3?)null : origin);
                Riding = false;
            }
        }

        /// <summary>The city unloads: whatever the ride changed is put back at once.</summary>
        public void Abort()
        {
            if (Riding)
            {
                Restore(null);
                Riding = false;
            }
        }

        private void Restore(Vector3? back)
        {
            try
            {
                if (_player != null)
                {
                    if (_hidden)
                    {
                        _player.Character.ToggleVisibility(show: true);
                    }
                    if (_blocked)
                    {
                        _player.UnsetNavigationBlocker(NavigationBlocker.Taxi);
                    }
                    if (back.HasValue && _hidden)
                    {
                        _player.Character.WarpSafely(back.Value);
                    }
                }
                if (_faded && _host != null && _host.isActiveAndEnabled)
                {
                    _host.StartCoroutine(UiFader.UnFade());
                }
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
            }
            _hidden = false;
            _blocked = false;
            _faded = false;
        }

        private static bool Aboard(Func<bool> arriveAboard)
        {
            try
            {
                return arriveAboard();
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
                return false;
            }
        }

        private static void Call(Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
            }
        }
    }
}
