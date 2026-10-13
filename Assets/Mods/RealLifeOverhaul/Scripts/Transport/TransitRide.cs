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
        public const float SubwayFare = 3f;      // the game's own price for a subway ride
        public static float DayPass => Prices.At(7.5f);       // unlimited buses and subway until midnight

        private const string Key = "reallife:transit";

        public static bool Loaded { get; private set; }
        public static int PassDay;       // the day a pass was bought for; 0 for none
        public static int BusRides;
        public static int SubwayRides;
        public static int ToldDelay = -1;     // the three-hour window whose delays were last sent to the phone
        public static int ToldClosure = -1;   // the night whose closure was last sent (no station closes now: kept, so that the saved line keeps its shape)
        public static float FareBack;         // read from a save written part of the way through a bus ride (BusRider): given back once the city is up

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
                }
            }
            Loaded = true;
        }

        /// <param name="fareBack">A fare paid for a ride that is part of the way through as the save is written (the save has the
        /// player back at the stop they boarded at): given back when this save is loaded. 0 for none.</param>
        public static void Save(Dictionary<string, string> data, float fareBack = 0f)
        {
            if (Loaded && data != null)
            {
                data[Key] = PassDay.ToString(CultureInfo.InvariantCulture) + "|" + BusRides.ToString(CultureInfo.InvariantCulture) + "|"
                            + SubwayRides.ToString(CultureInfo.InvariantCulture) + "|" + ToldDelay.ToString(CultureInfo.InvariantCulture) + "|"
                            + ToldClosure.ToString(CultureInfo.InvariantCulture)
                            + (fareBack > 0f ? "|" + fareBack.ToString("F2", CultureInfo.InvariantCulture) : string.Empty);
            }
        }

        /// <summary>Takes a fare, or nothing with a pass. False (and a toast) if the player cannot pay.</summary>
        public static bool Pay(float fare, string transaction)
        {
            if (HasPass)
            {
                return true;
            }
            var info = new TransactionInfo(transaction);
            if (GameManager.ChangeMoneySafe(-fare, info, null, null, force: false, showNotification: true))
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
            if (!GameManager.ChangeMoneySafe(-DayPass, new TransactionInfo("reallife_transaction_transit_pass"), null, null, force: false, showNotification: true))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            PassDay = TimeHelper.CurrentDay;
            return true;
        }
    }

    /// <summary>
    /// The time a ride takes, for the bus and the subway alike: the screen fades with a line saying
    /// what is happening, the player steps out of the world, the game's own time machine runs the
    /// clock forward by the journey, and the player is put down at the far end, exactly as the
    /// game's taxi does it (TaxiSystem.TravelCoroutine).
    ///
    /// On the bus the player has been seen to get on, sit down and pull away before this begins
    /// (<see cref="BusRider"/>); the rider's own steps come in at two places: as the screen is
    /// black, before the player is taken out of the world (<c>atBlack</c>: they are taken off the
    /// moving bus), and at the far end before the screen clears (<c>arriveAboard</c>: they are put
    /// in their seat on the bus standing at the stop they get off at; if that cannot be done they
    /// are put down on the pavement as before).
    /// Every way out puts the player back: the routine's own end, a fault part of the way
    /// through, and the city unloading under it (<see cref="Abort"/>).
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

        /// <summary>True when nothing stands in the way of a ride starting now: no menu, dialog, time skip, hospital trip or other journey.</summary>
        public static bool CanStart()
        {
            return LifeEvents.IsSafeForEvent() && !ThirdPersonCamera.IsUiOpen() && !TaxiSystem.IsTraveling && !PlayerHelper.IsUsingVehicle
                   && !BuildingManager.IsInsideBuilding && !GameManager.IsAnyMiniGameActive() && !UiFader.isFading;
        }

        /// <param name="minutes">Game minutes the journey takes, waiting included.</param>
        /// <param name="arrive">Where the player is put down: a point on the pavement.</param>
        /// <param name="fadeKey">The line under the fade, a complete locale key.</param>
        /// <param name="onFaded">Called once the screen is black (the bus drives off, people are taken in).</param>
        /// <param name="onArrived">Called with the player standing at the far end, before the screen clears.</param>
        /// <param name="tap">The card was just tapped on the reader (TransitDirector.TapCard): the fade waits for the tap to land.</param>
        /// <param name="atBlack">Called once the screen is black, before the player is taken out of the world.</param>
        /// <param name="arriveAboard">Called at the far end with the player back in the world, before the screen clears: true if it has placed them itself.</param>
        public void Begin(float minutes, Vector3 arrive, string fadeKey, Action onFaded, Action onArrived, bool tap = true, Action atBlack = null, Func<bool> arriveAboard = null)
        {
            if (Riding || _host == null || !_host.isActiveAndEnabled)
            {
                return;
            }
            Riding = true;
            _host.StartCoroutine(Run(minutes, arrive, fadeKey, onFaded, onArrived, tap, atBlack, arriveAboard));
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
                // The card on the reader first (TransitDirector.TapCard was just started): the tap lands 0.7 s in.
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
                    // (In a Going Public session the ride takes no time, as Going Public's own taxi ride does: Core/GoingPublic.)
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
                    _player.Character.WarpSafely(arrive);   // (in the world again: the agent is there to be warped)
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

        /// <summary>The host is going (the city unloads): whatever the ride had changed is put back at once.</summary>
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
