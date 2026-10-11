using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Entities;
using Extensions;
using UI.Smartphone.Apps.Contacts;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The subway as a journey. The game already has the stations as places in the street: walk
    /// up to an entrance, click it, pay 3, the map opens, pick a station, and a camera flies
    /// there in a few seconds with no time passing. That is left exactly as it is. Beside it,
    /// standing at an entrance on foot, the same trip is offered as a trip, in the game's own
    /// interface: its panel over a place (<see cref="GameUi.WorldPanel"/>) gives the service at
    /// this hour and a button on the game's interact key, which opens the window the game lists
    /// its stations in (<see cref="GameUi.TravelList"/>) with how many stops and how long each
    /// is; then the fare at the turnstile, the wait on the platform, the ride, and the clock
    /// moved on by all of it.
    ///
    /// Service, by rule from the game's clock (so it is the same on every load of the same day):
    /// a train every 3 minutes in the rush, 5 by day, 7 in the evening, 8 early, 12 at night;
    /// in about one three-hour window in eight there are delays of 6 to 14 minutes; and from
    /// 23:00 to 05:00 one station (a different one each night) is closed for track work. Delays
    /// and closures are sent to the phone by the City Transit contact when they begin.
    /// Ticked by <see cref="TransitDirector"/>; nothing of it runs per frame (the panel's key is the game's, read by the game).
    /// </summary>
    public sealed class SubwayJourney
    {
        private const float Reach = 7.5f;            // metres from the middle of an entrance: its stairs are that long
        private const float MetresPerStop = 650f;
        private const float MinutesPerStop = 2f;
        private const int PassRow = -1;
        private const string ButtonTake = "reallife_subway_button_take";

        private readonly MonoBehaviour _host;
        private readonly TransitRide _ride;
        private readonly List<SubwayStation> _choices = new List<SubwayStation>(24);
        private readonly StringBuilder _text = new StringBuilder(320);
        private readonly GameUi.WorldPanel _panel = new GameUi.WorldPanel();
        private GameUi.TravelList _list;
        private bool _listFailed;
        private Action _onTake;
        private Action _onListClosed;
        private Action<int> _onPick;
        private Comparison<SubwayStation> _nearestFirst;
        private Vector3 _here;
        private SubwayStation _at;
        private SubwayStation _to;
        private bool _choosing;
        private int _shownKey = int.MinValue;
        private int _keyFrame = -1;
        private int _tick;
        private float _minutes;

        public SubwayJourney(MonoBehaviour host, TransitRide ride)
        {
            _host = host;
            _ride = ride;
        }

        /// <summary>True while the station's panel or its list is on screen.</summary>
        public bool Showing => _at != null;

        /// <summary>True while the panel's button is on the game's interact key, and in the frame that key pressed it.</summary>
        public bool TakesKey => _panel.Showing || _keyFrame == Time.frameCount;

        // ---------------------------------------------------------------- the service, from the clock

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

        /// <summary>The station closed for track work tonight (23:00 to 05:00), or null.</summary>
        public static SubwayStation Closed()
        {
            int hour = TimeHelper.CurrentHour;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if ((hour < 23 && hour >= 5) || city == null || city.subwayStations == null || city.subwayStations.Count < 4)
            {
                return null;
            }
            int night = hour >= 23 ? TimeHelper.CurrentDay : TimeHelper.CurrentDay - 1;
            int wanted = Hash(night * 31 + 7) % city.subwayStations.Count;
            // By the game's own station order, so that it is the same station on every load.
            SubwayStation closed = null;
            for (int i = 0; i < city.subwayStations.Count; i++)
            {
                SubwayStation station = city.subwayStations[i];
                if (station != null && (int)station.stationName % city.subwayStations.Count == wanted)
                {
                    closed = station;
                }
            }
            return closed;
        }

        private static string Name(SubwayStation station)
        {
            return station.GetNameKey().GetLocalization();
        }

        private static int StopsBetween(SubwayStation from, SubwayStation to)
        {
            return Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(from.transform.position, to.transform.position) / MetresPerStop));
        }

        // Game minutes of the whole journey: the wait for a train (a part of the interval that depends on the minute), delays, the ride.
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

        // ---------------------------------------------------------------- ticking

        /// <summary>Four times a second.</summary>
        public void Tick(float now, Vector3 at, bool allowed)
        {
            _tick++;
            if (!Settings.SubwayJourney)
            {
                Hide();
                return;
            }
            if ((_tick & 3) != 0 && !_choosing)
            {
                return;
            }
            if ((_tick & 31) == 0)
            {
                Announce();
            }
            bool listOpen = _list != null && _list.IsOpen;
            _choosing = listOpen;
            // The list is one of the screens that free the mouse: while it is up, only the game's other screens close it.
            bool screens = listOpen ? UI.Smartphone.FullMenu.IsOpen || CityMap.IsOpen || UI.MiniMenu.MiniMenu.IsOpen || HudConfirm.isOpen : ThirdPersonCamera.IsUiOpen();
            CityManager city = InstanceBehavior<CityManager>.Instance;
            SubwayStation nearest = null;
            if (allowed && city != null && city.subwayStations != null && !screens && !JobDirector.TakesInteractKey && !SubwaySystem.IsRiding)
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
                if (listOpen)
                {
                    _list.Close();
                    listOpen = false;
                }
                _at = nearest;
                _shownKey = int.MinValue;
            }
            if (listOpen)
            {
                _panel.Hide();
                return;
            }
            ShowStation();
        }

        // The game's panel over the entrance: the station, how often trains run, the state of the service, and the button.
        private void ShowStation()
        {
            SubwayStation closed = Closed();
            int delay = Delay();
            int key = (int)_at.stationName * 4096 + Frequency() * 64 + delay * 2 + (closed == _at ? 1 : 0);
            if (key != _shownKey)
            {
                _shownKey = key;
                if (_onTake == null)
                {
                    _onTake = TakeClicked;
                    _onListClosed = ListClosed;
                    _onPick = Picked;
                }
                if (closed == _at)
                {
                    _panel.Lines(TransitText.T("reallife_subway_title", "name", Name(_at)), TransitText.T("reallife_subway_closed_here"),
                        TransitText.T("reallife_subway_status_closed"), GameUi.WorldPanel.Red());
                    _panel.Buttons(null, null, false, null, null, false);
                }
                else
                {
                    _panel.Lines(TransitText.T("reallife_subway_title", "name", Name(_at)), TransitText.T("reallife_subway_frequency", "minutes", Frequency().ToString()),
                        delay > 0 ? TransitText.T("reallife_subway_delays", "minutes", delay.ToString()) : TransitText.T("reallife_subway_good_service"),
                        delay > 0 ? GameUi.WorldPanel.Yellow() : GameUi.WorldPanel.Green());
                    _panel.Buttons(ButtonTake, _onTake, true, null, null, false);
                }
            }
            _panel.Position = _at.transform.position + Vector3.up * 2.2f;
            _panel.Show();
        }

        // The panel's button: the game's list of stations, nearest first, with the stops and the time of each.
        private void TakeClicked()
        {
            try
            {
                _keyFrame = Time.frameCount;   // the key that pressed the button does nothing else this frame
                if (_at == null || _ride.Riding || Closed() == _at || (_list != null && _list.IsOpen))
                {
                    return;
                }
                if ((_list == null || !_list.Alive) && !_listFailed)
                {
                    _list = GameUi.TravelList.Make("RealLifeOverhaul Subway Stations");
                    _listFailed = _list == null;
                }
                if (_list == null || !_list.Alive)
                {
                    TransitDirector.Logger?.Warn("Transit: the game's list of stations could not be copied, so the subway journey cannot be chosen here. A click on the entrance still opens the game's own subway map.");
                    return;
                }
                Fill();
                _list.Open(_onPick, _onListClosed);
                _choosing = true;
                _panel.Hide();
                TransitDirector.Logger?.Info("Transit check (subway list): the game's list is open at " + _at.stationName + ": " + _choices.Count + " stations, a train every " + Frequency()
                                             + " min, delays " + Delay() + " min: " + (_choices.Count > 0 ? "PASS" : "FAIL (no other open station)") + ".");
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
                Hide();
            }
        }

        private void Fill()
        {
            _choices.Clear();
            CityManager city = InstanceBehavior<CityManager>.Instance;
            SubwayStation closed = Closed();
            for (int i = 0; city != null && i < city.subwayStations.Count; i++)
            {
                SubwayStation station = city.subwayStations[i];
                if (station != null && station != _at && station != closed)
                {
                    _choices.Add(station);
                }
            }
            _here = _at.transform.position;
            _choices.Sort(_nearestFirst ?? (_nearestFirst = NearestFirst));
            int delay = Delay();
            bool pass = TransitState.HasPass;
            _text.Length = 0;
            _text.Append(TransitText.T("reallife_subway_frequency", "minutes", Frequency().ToString())).Append(". ");
            _text.Append(delay > 0 ? TransitText.T("reallife_subway_delays", "minutes", delay.ToString()) : TransitText.T("reallife_subway_good_service")).Append('.');
            if (closed != null)
            {
                _text.Append(' ').Append(TransitText.T("reallife_subway_closed_other", "name", Name(closed))).Append('.');
            }
            _text.Append('\n').Append(pass ? TransitText.T("reallife_transit_stop_pass") : TransitText.T("reallife_subway_fare", "fare", TransitText.Money(TransitState.SubwayFare)));
            _list.Begin(TransitText.T("reallife_subway_list_title", "name", Name(_at)), _text.ToString());
            if (!pass)
            {
                _list.Add(PassRow, TransitText.T("reallife_transit_list_pass"), TransitText.T("reallife_transit_list_pass_detail", "pass", TransitText.Money(TransitState.DayPass)),
                    "reallife_transit_list_buy", true);
            }
            string fare = pass ? TransitText.T("reallife_transit_list_fare_pass") : TransitText.Money(TransitState.SubwayFare);
            for (int i = 0; i < _choices.Count; i++)
            {
                SubwayStation station = _choices[i];
                _list.Add(i, Name(station), TransitText.T("reallife_subway_list_row", "stops", StopsBetween(_at, station).ToString(),
                    "minutes", Mathf.CeilToInt(JourneyMinutes(_at, station, out _)).ToString(), "fare", fare), "common_travel", true);
            }
        }

        private int NearestFirst(SubwayStation a, SubwayStation b)
        {
            return (a.transform.position - _here).sqrMagnitude.CompareTo((b.transform.position - _here).sqrMagnitude);
        }

        // An entry's button was clicked.
        private void Picked(int id)
        {
            try
            {
                _keyFrame = Time.frameCount;
                if (_at == null || _list == null)
                {
                    return;
                }
                if (id == PassRow)
                {
                    if (!TransitState.HasPass && TransitState.BuyPass())
                    {
                        Notifications.Show(NotificationType.Success, "reallife_transit_pass_bought", null, 6f);
                        Fill();   // the entry goes, and every fare now reads as the pass
                        _list.Open(_onPick, _onListClosed);
                    }
                    return;
                }
                if (id < 0 || id >= _choices.Count)
                {
                    return;
                }
                SubwayStation from = _at;
                SubwayStation to = _choices[id];
                _list.Close();   // (a journey does not begin under an open window)
                Travel(from, to);
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
                Hide();
            }
        }

        private void ListClosed()
        {
            _choosing = false;
            _shownKey = int.MinValue;   // the panel comes back with what is true now
        }

        // Through the turnstile, onto the platform, and away.
        private bool Travel(SubwayStation from, SubwayStation to)
        {
            if (!TransitRide.CanStart() || from == null || to == null || from == to)
            {
                return false;
            }
            if (Closed() == from || Closed() == to)
            {
                Notifications.Show(NotificationType.Warning, "reallife_subway_closed_toast", new Dictionary<string, string>(1) { { "name", Name(Closed()) } }, 8f);
                return false;
            }
            if (!TransitState.HasPass)
            {
                // The game's own transaction for a subway ride, at the game's own price.
                var info = new TransactionInfo("ba:transaction_subwayride");
                info.SetTaxDeductibleName("ba:transaction_subwayride");
                if (!GameManager.ChangeMoneySafe(-TransitState.SubwayFare, info, null, null, force: false, showNotification: true))
                {
                    Notifications.ShowInsufficientMoney();
                    return false;
                }
            }
            _minutes = JourneyMinutes(from, to, out int wait);
            _to = to;
            Hide();
            TransitDirector.TapCard();
            TransitState.SubwayRides++;
            TransitDirector.Logger?.Info("Transit: subway from " + from.stationName + " to " + to.stationName + ": " + StopsBetween(from, to) + " stops, a wait of " + wait
                                         + " min (a train every " + Frequency() + ", delays " + Delay() + "), " + _minutes.ToString("F0") + " game minutes in all.");
            _ride.Begin(_minutes, to.GetNavMeshTargetPosition(), Delay() > 0 ? "reallife_subway_fade_delayed" : "reallife_subway_fade", null, Arrived);
            return true;
        }

        private void Arrived()
        {
            SubwayStation to = _to;
            _to = null;
            if (to == null)
            {
                return;
            }
            try
            {
                HappinessHelper.AddModifier("ba:happinessmodifier_subway");   // what the game's own ride gives
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
            }
            Notifications.Show(NotificationType.Info, "reallife_subway_arrived",
                new Dictionary<string, string>(2) { { "name", Name(to) }, { "minutes", Mathf.RoundToInt(_minutes).ToString() } }, 7f);
        }

        // Delays and closures go to the phone when they begin: once per window, once per night.
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
            SubwayStation closed = Closed();
            int night = TimeHelper.CurrentHour >= 23 ? TimeHelper.CurrentDay : TimeHelper.CurrentDay - 1;
            if (closed != null && TransitState.ToldClosure != night)
            {
                TransitState.ToldClosure = night;
                TransitPhone.Text("reallife_text_transit_closure", "name", Name(closed));
            }
        }

        /// <summary>The panel and the list, down.</summary>
        public void Hide()
        {
            if (_list != null && _list.IsOpen)
            {
                _list.Close();
            }
            _choosing = false;
            if (_at == null)
            {
                return;
            }
            _at = null;
            _shownKey = int.MinValue;
            _panel.Hide();
        }

        /// <summary>With the director: the list's window goes too.</summary>
        public void Dispose()
        {
            try
            {
                Hide();
                _panel.Hide();
                _list?.Destroy();
                _list = null;
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
            }
        }

        /// <summary>Options button: the whole journey from the nearest station to the one after it in the game's list.</summary>
        public void Test()
        {
            TestRefusal why = !ModState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null ? TestRefusal.NotLoaded
                : BuildingManager.IsInsideBuilding ? TestRefusal.Indoors
                : !Settings.SubwayJourney ? TestRefusal.FeatureOff
                : _ride.Riding || !TransitRide.CanStart() ? TestRefusal.SceneActive : TestRefusal.None;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (why == TestRefusal.None && (city == null || city.subwayStations == null || city.subwayStations.Count < 2))
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
            SubwayStation closed = Closed();
            for (int i = 0; i < city.subwayStations.Count; i++)
            {
                SubwayStation station = city.subwayStations[i];
                float sqr = station != null ? (station.transform.position - at).sqrMagnitude : float.MaxValue;
                if (sqr < best && station != closed)
                {
                    best = sqr;
                    from = station;
                }
            }
            SubwayStation to = null;
            for (int i = 0; from != null && i < city.subwayStations.Count && to == null; i++)
            {
                SubwayStation station = city.subwayStations[(city.subwayStations.IndexOf(from) + 1 + i) % city.subwayStations.Count];
                if (station != null && station != from && station != closed)
                {
                    to = station;
                }
            }
            if (from == null || to == null || !Travel(from, to))
            {
                TestRefusals.Tell(TransitDirector.Logger, "Subway", TestRefusal.NothingFits, 0, "no pair of open stations, or the fare could not be paid");
            }
        }
    }

    /// <summary>
    /// The City Transit contact: service messages arrive from it, and calling it reads out the
    /// state of the subway and sells a day pass. Registered the way the mod's other contacts are.
    /// </summary>
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
                Dialogs.CallDialogFactory.RegisterDialog(dialogType, () => new TransitDialog());
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

    public class TransitDialog : Dialogs.Dialog
    {
        public TransitDialog()
        {
            npcNameKey = TransitPhone.ContactId;
            SubwayStation closed = SubwayJourney.Closed();
            int delay = SubwayJourney.Delay();
            var entry = new DialogEntry
            {
                messageData = (TransitState.HasPass ? "reallife_transit_call_pass" : "reallife_transit_call").Localize(new
                {
                    frequency = SubwayJourney.Frequency().ToString(),
                    service = (delay > 0 ? "reallife_transit_call_delays" : "reallife_transit_call_good").GetLocalization().Replace("{minutes}", delay.ToString()),
                    closed = closed != null ? "reallife_transit_call_closed".GetLocalization().Replace("{name}", closed.GetNameKey().GetLocalization()) : string.Empty,
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
            DialogController.current.ShowEntry(entry);
        }
    }

}
