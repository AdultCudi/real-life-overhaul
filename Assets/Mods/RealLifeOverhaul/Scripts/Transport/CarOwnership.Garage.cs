using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Tags;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Car ownership's part for the repair bay: what is due, the game's panel and workshop list there, and the service itself.</summary>
    public sealed partial class CarOwnership
    {
        private readonly StringBuilder _text = new StringBuilder(256);
        private GasStationController[] _stations;
        private readonly List<GasStationController> _stationList = new List<GasStationController>(16);
        private bool _dueService, _dueTyres, _dueBrakes, _dueRegistration;
        private int _registrationLeft;
        private float _serviceTotal;
        private int _serviceItems;

        private GasStationController[] Stations()
        {
            if (_stations == null)
            {
                // From the city's list of buildings, as the game finds them for a tow: a scene search was over 100 ms.
                CityManager city = InstanceBehavior<CityManager>.Instance;
                CityBuildingController[] buildings = GameDlc.Open(city);
                _stationList.Clear();
                for (int i = 0; buildings != null && i < buildings.Length; i++)
                {
                    if (buildings[i] != null && buildings[i].TryGetComponent(out GasStationController station))
                    {
                        _stationList.Add(station);
                    }
                }
                _stations = _stationList.Count > 0 ? _stationList.ToArray()
                    : SceneFinds.All<GasStationController>("car ownership: the petrol stations being looked for (a search of the whole scene: the city's buildings had none)");
                _stationList.Clear();
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Car ownership check (stations): " + _stations.Length + " petrol station(s) among " + (buildings != null ? buildings.Length : 0)
                                 + " city buildings" + (_stations.Length > 0 ? ": PASS." : ": FAIL (none: the repair bay is never recognised)."));
                }
            }
            return _stations;
        }

        // A station's repair bay of the car's kind (the game has separate ones for trucks).
        private bool InBay()
        {
            if (_car.vehicleCollider == null)
            {
                return false;
            }
            Bounds bounds = _car.vehicleCollider.bounds;
            bool truck = _car.vehicleType.HasTag(TagRef.Vehicletag.istruck);
            GasStationController[] stations = Stations();
            for (int s = 0; s < stations.Length; s++)
            {
                GasStationTrigger[] bays = stations[s] != null ? stations[s].repairStationTriggers : null;
                for (int i = 0; bays != null && i < bays.Length; i++)
                {
                    if (bays[i] != null && bays[i].stationCollider != null && bays[i].isTruckGarage == truck && bays[i].IntersectsBounds(bounds))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private int NearestStation(Vector3 from)
        {
            float best = float.MaxValue;
            GasStationController[] stations = Stations();
            for (int s = 0; s < stations.Length; s++)
            {
                if (stations[s] != null && stations[s].gasStationTriggers != null && stations[s].gasStationTriggers.Length > 0)
                {
                    best = Mathf.Min(best, (stations[s].transform.position - from).sqrMagnitude);
                }
            }
            return best < float.MaxValue ? Mathf.RoundToInt(Mathf.Sqrt(best)) : 0;
        }

        /// <summary>Days until a car's registration runs out; negative once it has.</summary>
        public static int RegistrationLeft(CarRecord record)
        {
            int year = GameCalendar.DaysPerYear;   // a registration runs a year
            return record.RegisteredDay + year - TimeHelper.CurrentDay;
        }

        // The timer has looked at this car, which may have been got into a moment ago. True if it has a record.
        private bool Tracks(VehicleController car)
        {
            if (_failed || !Settings.CarOwnership || car == null)
            {
                return false;
            }
            if (!ReferenceEquals(_car, car))
            {
                try
                {
                    _nextTick = 0f;
                    Tick(Time.time);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    return false;
                }
            }
            return _car != null && ReferenceEquals(_car, car) && _record != null;
        }

        /// <summary>True if this is a car of the player's the service looks after.</summary>
        public bool Looks(VehicleController car)
        {
            return Tracks(car);
        }

        private void Quote()
        {
            float since = _record.Km - _record.ServicedKm;
            float every = CarRules.ServiceEvery(_kind);
            _dueService = _record.Broken || since >= every * 0.6f;
            _dueTyres = _record.Tyres < CarRules.ReplaceBelow;
            _dueBrakes = _record.Brakes < CarRules.ReplaceBelow;
            _registrationLeft = RegistrationLeft(_record);
            _dueRegistration = _registrationLeft <= CarRules.RegistrationEarly;
            _serviceItems = (_dueService ? 1 : 0) + (_dueTyres ? 1 : 0) + (_dueBrakes ? 1 : 0) + (_dueRegistration ? 1 : 0);
            _serviceTotal = (_dueService ? CarRules.Service(_price) : 0f) + (_dueTyres ? CarRules.TyreSet(_price) : 0f) + (_dueBrakes ? CarRules.BrakeSet(_price) : 0f)
                            + (_dueRegistration ? RegistrationCost() : 0f);
        }

        private float RegistrationCost()
        {
            return CarRules.Registration(_price) + (_registrationLeft < 0 ? CarRules.RegistrationLateFee : 0f);
        }

        /// <summary>The game's repair bay panel's second line: what is due and what it comes to; null for a car without a record.</summary>
        public void BayLine(VehicleController car, out string left, out string right)
        {
            left = null;
            right = null;
            if (!Tracks(car))
            {
                return;
            }
            Quote();
            if (_serviceItems == 0)
            {
                left = TransitText.T("reallife_garage_bay_nothing");
                return;
            }
            _text.Length = 0;
            Word(_dueService, "reallife_garage_word_service");
            Word(_dueTyres, "reallife_garage_word_tyres");
            Word(_dueBrakes, "reallife_garage_word_brakes");
            Word(_dueRegistration, "reallife_garage_word_registration");
            left = TransitText.T("reallife_garage_bay_due", "what", _text.ToString());
            right = TransitText.Money(_serviceTotal);
        }

        private void Word(bool due, string key)
        {
            if (due)
            {
                if (_text.Length > 0)
                {
                    _text.Append(", ");
                }
                _text.Append(TransitText.T(key));
            }
        }

        public string DueWords(VehicleController car)   // BayLine in plain words, for Player.log
        {
            if (!Tracks(car))
            {
                return "not a car with a record";
            }
            Quote();
            return _serviceItems == 0 ? "nothing due" : _serviceItems + " thing(s) due for " + _serviceTotal.ToString("F0") + (_record.Broken ? ", broken down" : string.Empty);
        }

        /// <summary>The service's entry in the workshop's list: the work, each part's cost, the total and the time.</summary>
        public void ListEntry(GameUi.TravelList list, int id, VehicleController car)
        {
            if (!Tracks(car))
            {
                return;
            }
            Quote();
            _text.Length = 0;
            if (_serviceItems == 0)
            {
                float since = _record.Km - _record.ServicedKm;
                _text.Append(TransitText.T("reallife_garage_nothing_due", "km", Mathf.Max(0f, CarRules.ServiceEvery(_kind) - since).ToString("F0"),
                    "tyres", Mathf.RoundToInt(_record.Tyres * 100f).ToString(), "brakes", Mathf.RoundToInt(_record.Brakes * 100f).ToString()));
            }
            else
            {
                Line(_dueService, "reallife_garage_line_service", CarRules.Service(_price));
                Line(_dueTyres, "reallife_garage_line_tyres", CarRules.TyreSet(_price));
                Line(_dueBrakes, "reallife_garage_line_brakes", CarRules.BrakeSet(_price));
                Line(_dueRegistration, "reallife_garage_line_registration", RegistrationCost());
                _text.Append(TransitText.T("reallife_garage_list_total", "amount", TransitText.Money(_serviceTotal), "minutes", ServiceMinutes(_serviceItems).ToString("F0")));
            }
            list.Add(id, TransitText.T("reallife_garage_list_service"), _text.ToString(), "reallife_garage_list_order", _serviceItems > 0 && !_servicing);
        }

        private void Line(bool due, string key, float cost)
        {
            if (due)
            {
                _text.Append(TransitText.T(key, "amount", TransitText.Money(cost))).Append(". ");
            }
        }

        private static float ServiceMinutes(int items)
        {
            return 25f + 15f * items;
        }

        /// <summary>Pays for and does everything due in a repair bay. False, with nothing taken, if it cannot (the game says why).</summary>
        public bool OrderService(VehicleController car)
        {
            if (!Tracks(car) || _servicing)
            {
                return false;
            }
            Quote();
            if (_serviceItems == 0 || !InBay())
            {
                return false;
            }
            if (!GameManager.ChangeMoneySafe(-_serviceTotal, new TransactionInfo("reallife_transaction_car_service"), null, null, force: false, showNotification: true))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            StartCoroutine(Guard.Routine(Service(_car, _record, _kind, _serviceTotal, ServiceMinutes(_serviceItems))));
            return true;
        }

        // The work is done behind a fade while the clock runs on: most of an hour.
        private IEnumerator Service(CarController car, CarRecord record, CarKind kind, float total, float minutes)
        {
            _servicing = true;
            UIs uis = InstanceBehavior<UIs>.Instance;
            try
            {
                _faded = true;
                yield return UiFader.Fade(0.6f, "reallife_garage_fade");
                SfxManager sounds = InstanceBehavior<SfxManager>.Instance;
                if (sounds != null && car != null)
                {
                    sounds.PlayAudio(BigAmbitions.SoundSystem.SoundType.CarRepair, car.transform.position);
                }
                if (uis != null && uis.timeMachine != null && LifeEvents.IsSafeForEvent() && !GoingPublic.ClockShared)   // no time passes in a Going Public session
                {
                    Timestamp until = TimeHelper.Now();
                    until.AddMinutes(minutes);
                    uis.timeMachine.StartTimeMachine(until, disableCancel: true, "reallife_garage_fade");
                    float waited = 0f;
                    while (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning && waited < 60f)
                    {
                        waited += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
                bool wasBroken = record.Broken;
                float since = record.Km - record.ServicedKm;
                if (record.Broken || since >= CarRules.ServiceEvery(kind) * 0.6f)
                {
                    record.ServicedKm = record.Km;
                    record.Broken = false;
                    record.GraceKm = 0f;
                }
                if (record.Tyres < CarRules.ReplaceBelow)
                {
                    record.Tyres = 1f;
                }
                if (record.Brakes < CarRules.ReplaceBelow)
                {
                    record.Brakes = 1f;
                }
                if (RegistrationLeft(record) <= CarRules.RegistrationEarly)
                {
                    record.RegisteredDay = TimeHelper.CurrentDay;
                    record.RemindedDay = 0;
                }
                Logger?.Info("Car ownership: serviced for " + total.ToString("F0") + " at " + record.Km.ToString("F1", CultureInfo.InvariantCulture) + " km"
                    + (wasBroken ? " (it had broken down)" : "") + ".");
                _faded = false;
                yield return UiFader.UnFade();
                Notifications.Show(NotificationType.Success, "reallife_garage_done", new Dictionary<string, string>(1) { { "amount", TransitText.Money(total) } }, 9f);
            }
            finally
            {
                _servicing = false;
                GarageBay.Refresh();   // the bay's panel says what is due now
                if (_faded)
                {
                    _faded = false;
                    if (isActiveAndEnabled)
                    {
                        StartCoroutine(UiFader.UnFade());
                    }
                }
            }
        }
    }
}
