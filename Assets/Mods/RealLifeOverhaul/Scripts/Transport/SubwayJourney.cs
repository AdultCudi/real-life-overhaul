using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using Entities;
using Helpers;
using Localizor;
using UI.Notification;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The subway as a journey, beside the game's own subway: at an entrance a panel gives the station, the frequency and the
    /// service; once the game's train has brought the player up, the clock moves on by what the trip takes beyond the game's
    /// ride, under the fade (<see cref="TransitRide"/>); a day pass gives the game's fare back. The service is a rule of the
    /// game's clock, the same on every load of a day. Ticked by <see cref="TransitDirector"/>; nothing per frame.
    /// </summary>
    public sealed class SubwayJourney
    {
        private const float Reach = 7.5f;            // metres from the middle of an entrance: its stairs are that long
        private const float MetresPerStop = 650f;    // metres between stations, for counting the stops
        private const float MinutesPerStop = 2f;     // game minutes
        private const float StartWithin = 10f;       // seconds after the game's ride in which the clock is moved on (another scene may be closing)

        private readonly MonoBehaviour _host;
        private readonly TransitRide _ride;
        private readonly GameUi.WorldPanel _panel = new GameUi.WorldPanel { Name = "subway entrance" };
        private SubwayStation _at;
        private int _shownKey = int.MinValue;
        private int _tick;

        // The game's ride while it runs, and then what is still to be added.
        private bool _gameRide;
        private SubwayStation _rideFrom, _rideTo;
        private int _rideBegan;
        private SubwayStation _to;
        private float _minutes;
        private float _more;
        private int _took;
        private float _startBy = -1f;

        public SubwayJourney(MonoBehaviour host, TransitRide ride)
        {
            _host = host;
            _ride = ride;
            GameUi.PanelPlaces.Set("subway entrance", Entrances);   // somebody waiting for the player is stood clear of them
        }

        // The game's subway entrances near a point, each with the reach of its panel (Core/GameUi/PanelPlaces). On an event, never per frame.
        private static int Entrances(Vector3 around, float within, float[] x, float[] z, float[] reach, int count)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city == null || city.subwayStations == null)
            {
                return count;
            }
            for (int i = 0; i < city.subwayStations.Count; i++)
            {
                SubwayStation station = city.subwayStations[i];
                if (station != null)
                {
                    count = GameUi.PanelPlaces.Add(around, within, station.transform.position, Reach, x, z, reach, count);
                }
            }
            return count;
        }

        /// <summary>Minutes between trains now.</summary>
        public static int Frequency()
        {
            int hour = TimeHelper.CurrentHour;
            if (hour < 5)
            {
                return 12;
            }
            if (hour < 7)
            {
                return 8;
            }
            bool rush = !TimeHelper.IsWeekend && ((hour >= 7 && hour < 9) || (hour >= 16 && hour < 19));
            return rush ? 3 : hour < 20 ? 5 : 7;
        }

        private static int Window => TimeHelper.CurrentDay * 8 + TimeHelper.CurrentHour / 3;

        private static int Hash(int value)
        {
            unchecked
            {
                uint x = (uint)value * 2654435761u;
                x ^= x >> 15;
                x *= 2246822519u;
                x ^= x >> 13;
                return (int)(x & 0x7fffffff);
            }
        }

        /// <summary>Minutes of delay on the network in this three-hour window; 0 for a good service.</summary>
        public static int Delay()
        {
            int roll = Hash(Window);
            return roll % 8 == 0 ? 6 + (roll >> 4) % 9 : 0;
        }

        private static string Name(SubwayStation station)
        {
            return GameMembers.NameKey(station).GetLocalization();
        }

        private static int StopsBetween(SubwayStation from, SubwayStation to)
        {
            return Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(from.transform.position, to.transform.position) / MetresPerStop));
        }

        // Game minutes of the whole journey: the wait for a train, delays, the ride.
        private static float JourneyMinutes(SubwayStation from, SubwayStation to, out int wait)
        {
            int frequency = Frequency();
            wait = 1 + Hash(Window * 61 + (int)TimeHelper.CurrentMinute / 2) % frequency + Delay();
            float speed = 1f;
            try
            {
                speed = Mathf.Clamp(PlayerPrefSettings.GameSpeed, 0.25f, 4f);
            }
            catch (Exception)
            {
                speed = 1f;
            }
            return (wait + StopsBetween(from, to) * MinutesPerStop) * speed;
        }

        private static int Now()
        {
            return TimeHelper.CurrentDay * 1440 + TimeHelper.CurrentHour * 60 + (int)TimeHelper.CurrentMinute;
        }

        /// <summary>Four times a second.</summary>
        public void Tick(float now, Vector3 at, bool allowed)
        {
            _tick++;
            WatchRide(now);   // the day pass covers the game's ride whether or not journeys take time
            if (!Settings.SubwayJourney)
            {
                Hide();
                return;
            }
            if ((_tick & 3) != 0)
            {
                return;
            }
            if ((_tick & 31) == 0)
            {
                Announce();
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            SubwayStation nearest = null;
            if (allowed && city != null && city.subwayStations != null && !ThirdPersonCamera.IsUiOpen() && !JobDirector.TakesInteractKey && !SubwaySystem.IsRiding && !_ride.Riding)
            {
                float best = Reach * Reach;
                for (int i = 0; i < city.subwayStations.Count; i++)
                {
                    SubwayStation station = city.subwayStations[i];
                    if (station == null)
                    {
                        continue;
                    }
                    Vector3 offset = station.transform.position - at;
                    float sqr = offset.x * offset.x + offset.z * offset.z;
                    if (sqr < best && Mathf.Abs(offset.y) < 5f)
                    {
                        best = sqr;
                        nearest = station;
                    }
                }
            }
            if (nearest == null)
            {
                Hide();
                return;
            }
            if (nearest != _at)
            {
                _at = nearest;
                _shownKey = int.MinValue;
            }
            ShowStation();
        }

        private void ShowStation()
        {
            int delay = Delay();
            int key = (int)_at.stationName * 4096 + Frequency() * 64 + delay * 2;
            if (key != _shownKey)
            {
                _shownKey = key;
                _panel.Lines(TransitText.T("reallife_subway_title", "name", Name(_at)), TransitText.T("reallife_subway_frequency", "minutes", Frequency().ToString()),
                    delay > 0 ? TransitText.T("reallife_subway_delays", "minutes", delay.ToString()) : TransitText.T("reallife_subway_good_service"),
                    delay > 0 ? GameUi.WorldPanel.Yellow() : GameUi.WorldPanel.Green());
                _panel.Buttons(null, null, false, null, null, false);
            }
            _panel.For(_at.transform.position);
            _panel.Position = _at.transform.position + Vector3.up * 2.2f;
            _panel.Show();
        }

        // The game's ride: as it begins, the fare back on a day pass; once it has brought the player up, the time still to add.
        private void WatchRide(float now)
        {
            if (SubwaySystem.IsRiding)
            {
                if (!_gameRide)
                {
                    Began();
                }
                return;
            }
            if (_gameRide)
            {
                _gameRide = false;
                Landed(now);
            }
            if (_startBy < 0f || _ride.Riding)
            {
                return;
            }
            if (TransitRide.CanStart() && PlayerHelper.PlayerController != null)
            {
                _startBy = -1f;
                _ride.Begin(_more, PlayerHelper.PlayerController.transform.position, Delay() > 0 ? "reallife_subway_fade_delayed" : "reallife_subway_fade", null, Arrived, tap: false);
            }
            else if (now > _startBy)
            {
                _startBy = -1f;
                _minutes = _took;   // the note says what it did take
                TransitDirector.Logger?.Info("Transit check (subway journey): the clock could not be moved on after the game's ride: another scene stood in the way for "
                                             + StartWithin.ToString("F0") + " s: FAIL.");
                Arrived();
            }
        }

        private void Began()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            SubwaySystem system = city != null ? city.subwaySystem : null;
            _gameRide = true;
            _rideFrom = system != null ? system.lastSubwayStation : null;
            _rideTo = system != null ? system.destinationSubwayStation : null;
            _rideBegan = Now();
            _startBy = -1f;   // a ride begun while the last one's time was still to be added lets that time go
            _to = null;
            Hide();
            TransitState.SubwayRides++;
            bool pass = TransitState.HasPass;
            if (pass)
            {
                // The game took its fare in the frame its ride began (SubwaySystem.TravelToCoroutine).
                GameManager.ChangeMoneySafe(SubwayStation.PricePerRide, new TransactionInfo(CivicBridges.FareGivenBack), null, null, force: true, showNotification: true);
            }
            TransitDirector.Logger?.Info("Transit: the game's subway ride from " + (_rideFrom != null ? _rideFrom.stationName.ToString() : "?") + " to "
                                         + (_rideTo != null ? _rideTo.stationName.ToString() : "?") + " has begun" + (pass ? "; the day pass gives the game's fare back" : string.Empty) + ".");
        }

        private void Landed(float now)
        {
            SubwayStation from = _rideFrom, to = _rideTo;
            _rideFrom = _rideTo = null;
            if (from == null || to == null || !Settings.SubwayJourney)
            {
                return;
            }
            float all = JourneyMinutes(from, to, out int wait);
            _took = Mathf.Max(0, Now() - _rideBegan);
            _to = to;
            _minutes = all;
            _more = all - _took;
            TransitDirector.Logger?.Info("Transit check (subway journey): the game's ride from " + from.stationName + " to " + to.stationName + " came up after " + _took
                                         + " game minute(s); the journey takes " + all.ToString("F0") + " (" + StopsBetween(from, to) + " stop(s), a wait of " + wait
                                         + " min, a train every " + Frequency() + ", delays " + Delay() + "): "
                                         + (_more >= 1f ? "the clock moves on " + _more.ToString("F0") + " minutes more" : "nothing more to add") + ": PASS.");
            if (_more >= 1f)
            {
                _startBy = now + StartWithin;
            }
            else
            {
                Arrived();
            }
        }

        private void Arrived()
        {
            SubwayStation to = _to;
            _to = null;
            if (to == null)
            {
                return;
            }
            Notifications.Show(NotificationType.Info, "reallife_subway_arrived",
                new Dictionary<string, string>(2) { { "name", Name(to) }, { "minutes", Mathf.RoundToInt(_minutes).ToString() } }, 7f);
        }

        // Delays go to the phone when they begin, once a window.
        private void Announce()
        {
            if (!TransitPhone.Ready || !TransitState.Loaded)
            {
                return;
            }
            int delay = Delay();
            if (delay > 0 && TransitState.ToldDelay != Window)
            {
                TransitState.ToldDelay = Window;
                TransitPhone.Text("reallife_text_transit_delay", "minutes", delay.ToString());
            }
        }

        public void Hide()
        {
            if (_at == null)
            {
                return;
            }
            _at = null;
            _shownKey = int.MinValue;
            _panel.Hide();
        }

        public void Dispose()
        {
            try
            {
                Hide();
                _panel.Hide();
                _startBy = -1f;
                _gameRide = false;
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
            }
        }

        /// <summary>Options button: a journey by the game's own subway from the nearest station to the next in its list.</summary>
        public void Test()
        {
            TestRefusal why = !ModState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null ? TestRefusal.NotLoaded
                : BuildingManager.IsInsideBuilding ? TestRefusal.Indoors
                : !Settings.SubwayJourney ? TestRefusal.FeatureOff
                : _ride.Riding || SubwaySystem.IsRiding || !TransitRide.CanStart() ? TestRefusal.SceneActive : TestRefusal.None;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (why == TestRefusal.None && (city == null || city.subwayStations == null || city.subwayStations.Count < 2 || city.subwaySystem == null || city.cityMap == null))
            {
                why = TestRefusal.NothingFits;
            }
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(TransitDirector.Logger, "Subway", why);
                return;
            }
            Vector3 at = PlayerHelper.PlayerController.transform.position;
            SubwayStation from = null;
            float best = float.MaxValue;
            for (int i = 0; i < city.subwayStations.Count; i++)
            {
                SubwayStation station = city.subwayStations[i];
                float sqr = station != null ? (station.transform.position - at).sqrMagnitude : float.MaxValue;
                if (sqr < best)
                {
                    best = sqr;
                    from = station;
                }
            }
            SubwayStation to = from != null ? city.subwayStations[(city.subwayStations.IndexOf(from) + 1) % city.subwayStations.Count] : null;
            if (from == null || to == null || to == from || SaveGameManager.Current.Money < SubwayStation.PricePerRide)
            {
                TestRefusals.Tell(TransitDirector.Logger, "Subway", TestRefusal.NothingFits, 0, "no pair of stations, or no money for the game's fare");
                return;
            }
            // As the game's entrance does it (SubwayStation.Interact), then the game's pick of a station on its open map.
            city.subwaySystem.lastSubwayStation = from;
            if (!CityMap.IsOpen)
            {
                city.cityMap.Toggle();
            }
            if (!CityMap.IsOpen)
            {
                TestRefusals.Tell(TransitDirector.Logger, "Subway", TestRefusal.SceneActive, 0, "the game would not open its map now");
                return;
            }
            city.cityMap.ToggleSubwayMode(true);
            _host.StartCoroutine(Guard.Routine(PickOnMap(to)));
        }

        // The game's map pauses the game's clock, so this waits on the interface's.
        private IEnumerator PickOnMap(SubwayStation to)
        {
            float until = Time.unscaledTime + 1.5f;
            while (Time.unscaledTime < until)
            {
                yield return null;
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city != null && CityMap.IsOpen && city.cityMap.isSubwayMode && !SubwaySystem.IsRiding)
            {
                city.subwaySystem.TravelTo(to);
            }
            else
            {
                TestRefusals.Tell(TransitDirector.Logger, "Subway", TestRefusal.SceneActive, 0, "the game's subway map was closed before a station was picked");
            }
        }
    }

    /// <summary>The City Transit contact: service messages come from it, and a call reads out the subway's state and sells a day pass.</summary>
    public static class TransitPhone
    {
        public const string ContactId = "reallife-transit";

        private static Contact _contact;

        public static bool Ready => _contact != null;

        public static void Setup()
        {
            _contact = null;
            if (!Settings.PhoneContacts || (!Settings.Buses && !Settings.SubwayJourney))
            {
                return;
            }
            try
            {
                ContactPictures.Add(ContactId);   // the game's own logo shapes (Phone/Contacts)
                Contact contact = Contact.GetContact(ContactId, ContactCategoryName.General, "reallife_contact_transit_desc", null, false, true);
                var dialogType = (Dialogs.CallDialogType)ModEnumHash.GetSafeHash("reallife_transit_calldialogtype");
                contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
                Dialogs.CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new TransitDialog()));
                if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
                {
                    contact.SendMessage(new TextMessage("reallife_text_transit_welcome"), notify: false);
                }
                _contact = contact;
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
                TransitDirector.Logger?.Warn("Transit: the City Transit contact could not be added; delays and closures are shown at the stations only.");
            }
        }

        /// <summary>A service message on the phone, with one {name} filled in.</summary>
        public static void Text(string key, string name, string value)
        {
            try
            {
                _contact?.SendMessage(new TextMessage(key, new Dictionary<string, string>(1) { { name, value } }));
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
            }
        }
    }

    /// <summary>City Transit on the line: the service and the day pass.</summary>
    public sealed class TransitDialog : Dialogs.Dialog
    {
        public TransitDialog()
        {
            npcNameKey = TransitPhone.ContactId;
            int delay = SubwayJourney.Delay();
            var entry = new DialogEntry
            {
                messageData = (TransitState.HasPass ? "reallife_transit_call_pass" : "reallife_transit_call").Localize(new
                {
                    frequency = SubwayJourney.Frequency().ToString(),
                    service = (delay > 0 ? "reallife_transit_call_delays" : "reallife_transit_call_good").GetLocalization().Replace("{minutes}", delay.ToString()),
                    fare = TransitText.Money(TransitState.BusFare),
                    pass = TransitText.Money(TransitState.DayPass)
                }),
                OnCancel = PhoneContacts.HangUp
            };
            if (!TransitState.HasPass)
            {
                entry.ConfirmTextOverride = "reallife_transit_call_buy".Localize();
                entry.OnConfirm = () => PhoneContacts.Closing(TransitState.BuyPass() ? "reallife_transit_call_bought" : "reallife_transit_call_no_money");
            }
            else
            {
                entry.ConfirmTextOverride = "reallife_transit_call_thanks".Localize();
                entry.OnConfirm = () => PhoneContacts.Closing("reallife_transit_call_bye");
            }
            DialogController.current.ShowEntry(Guard.Entry(entry));
        }
    }
}
