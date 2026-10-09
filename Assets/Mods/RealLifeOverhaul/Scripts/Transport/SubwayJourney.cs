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
    /// standing at an entrance on foot, the interaction key offers the same trip as a trip:
    /// the service at this hour, a list of stations with how many stops and how long, the fare
    /// at the turnstile, the wait on the platform, the ride, and the clock moved on by all of it.
    ///
    /// Service, by rule from the game's clock (so it is the same on every load of the same day):
    /// a train every 3 minutes in the rush, 5 by day, 7 in the evening, 8 early, 12 at night;
    /// in about one three-hour window in eight there are delays of 6 to 14 minutes; and from
    /// 23:00 to 05:00 one station (a different one each night) is closed for track work. Delays
    /// and closures are sent to the phone by the City Transit contact when they begin.
    /// Ticked by <see cref="TransitDirector"/>; per frame only the key tests, while its prompt shows.
    /// </summary>
    public sealed class SubwayJourney
    {
        private const float Reach = 7.5f;
        private const float MetresPerStop = 650f;
        private const float MinutesPerStop = 2f;
        private const int PageSize = 3;

        private readonly MonoBehaviour _host;
        private readonly TransitRide _ride;
        private readonly List<SubwayStation> _choices = new List<SubwayStation>(24);
        private readonly StringBuilder _text = new StringBuilder(320);
        private SubwayStation _at;
        private SubwayStation _to;
        private bool _choosing;
        private bool _promptOn;
        private int _page;
        private int _shownKey = int.MinValue;
        private int _keyFrame = -1;
        private int _tick;
        private float _minutes;

        public SubwayJourney(MonoBehaviour host, TransitRide ride)
        {
            _host = host;
            _ride = ride;
        }

        /// <summary>True while the station panel is on screen.</summary>
        public bool Showing => _at != null;

        public bool TakesKey => _promptOn || _keyFrame == Time.frameCount;

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
            CityManager city = InstanceBehavior<CityManager>.Instance;
            SubwayStation nearest = null;
            if (allowed && city != null && city.subwayStations != null && !ThirdPersonCamera.IsUiOpen() && !JobDirector.TakesInteractKey && !SubwaySystem.IsRiding)
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
                _choosing = false;
                _shownKey = int.MinValue;
            }
            if (_choosing)
            {
                ShowChoices();
            }
            else
            {
                ShowStation();
            }
        }

        private void ShowStation()
        {
            SubwayStation closed = Closed();
            int delay = Delay();
            int key = (int)_at.stationName * 4096 + Frequency() * 64 + delay * 2 + (closed == _at ? 1 : 0) + (TransitState.HasPass ? 2048 : 0);
            if (key != _shownKey)
            {
                _shownKey = key;
                _text.Length = 0;
                if (closed == _at)
                {
                    _text.Append(TransitText.T("reallife_subway_closed_here")).Append('\n');
                }
                else
                {
                    _text.Append(TransitText.T("reallife_subway_frequency", "minutes", Frequency().ToString())).Append('\n');
                    _text.Append(delay > 0 ? TransitText.T("reallife_subway_delays", "minutes", delay.ToString()) : TransitText.T("reallife_subway_good_service")).Append('\n');
                    if (closed != null)
                    {
                        _text.Append(TransitText.T("reallife_subway_closed_other", "name", Name(closed))).Append('\n');
                    }
                }
                _text.Append(TransitState.HasPass ? TransitText.T("reallife_transit_stop_pass") : TransitText.T("reallife_subway_fare", "fare", TransitText.Money(TransitState.SubwayFare)));
                TransportHud.Instance.ShowPanel(TransitText.T("reallife_subway_title", "name", Name(_at)), _text.ToString());
                if (closed == _at)
                {
                    HidePrompt();
                }
                else
                {
                    _promptOn = true;
                    TransportHud.Instance.ShowPrompt(TransitText.T("reallife_subway_prompt"));
                }
            }
        }

        private void ShowChoices()
        {
            int key = -1 - ((int)_at.stationName * 100 + _page);
            if (key == _shownKey)
            {
                return;
            }
            _shownKey = key;
            int pages = Mathf.Max(1, (_choices.Count + PageSize - 1) / PageSize);
            _page %= pages;
            _text.Length = 0;
            for (int i = 0; i < PageSize; i++)
            {
                int index = _page * PageSize + i;
                if (index >= _choices.Count)
                {
                    break;
                }
                SubwayStation station = _choices[index];
                _text.Append(TransitText.T("reallife_transit_choice", "choice", JobKeys.ChoiceLabel(i), "name", Name(station),
                    "stops", StopsBetween(_at, station).ToString(), "minutes", Mathf.CeilToInt(JourneyMinutes(_at, station, out _)).ToString())).Append('\n');
            }
            _text.Append('\n').Append(TransitText.T("reallife_subway_page", "page", (_page + 1).ToString(), "pages", pages.ToString()));
            TransportHud.Instance.ShowPanel(TransitText.T("reallife_subway_title", "name", Name(_at)), _text.ToString());
            _promptOn = true;
            TransportHud.Instance.ShowPrompt(TransitText.T("reallife_transit_prompt_choose"));
        }

        /// <summary>Per frame: the keys, only while the prompt is on screen.</summary>
        public void Frame()
        {
            if (!_promptOn || _at == null || _ride.Riding || ThirdPersonCamera.IsUiOpen())
            {
                return;
            }
            if (_choosing)
            {
                int choice = JobKeys.ChoiceDown();
                if (choice >= 0)
                {
                    int index = _page * PageSize + choice;
                    if (index < _choices.Count)
                    {
                        Travel(_at, _choices[index]);
                    }
                    return;
                }
                if (JobKeys.InteractDown())
                {
                    _keyFrame = Time.frameCount;
                    _page++;
                    _shownKey = int.MinValue;
                    ShowChoices();
                }
                return;
            }
            if (!JobKeys.InteractDown())
            {
                return;
            }
            _keyFrame = Time.frameCount;
            // The other stations, nearest first: the list is short, and sorted once per opening.
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
            Vector3 here = _at.transform.position;
            _choices.Sort((a, b) => (a.transform.position - here).sqrMagnitude.CompareTo((b.transform.position - here).sqrMagnitude));
            _choosing = true;
            _page = 0;
            _shownKey = int.MinValue;
            ShowChoices();
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

        private void HidePrompt()
        {
            if (_promptOn)
            {
                _promptOn = false;
                if (TransportHud.Exists)
                {
                    TransportHud.Instance.HidePrompt();
                }
            }
        }

        public void Hide()
        {
            if (_at == null && !_promptOn)
            {
                return;
            }
            _at = null;
            _choosing = false;
            _shownKey = int.MinValue;
            HidePrompt();
            if (TransportHud.Exists)
            {
                TransportHud.Instance.HidePanel();
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

        private static Sprite _icon;
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
                PhoneIcons.Add(ContactId, new Color(0.10f, 0.32f, 0.62f), ref _icon);
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

    /// <summary>A contact's picture: a plain coloured disc with a white middle, like the mod's other contacts. The game looks icons up by sprite name == contact id.</summary>
    public static class PhoneIcons
    {
        public static void Add(string id, Color colour, ref Sprite kept)
        {
            var references = InstanceBehavior<GlobalReferences>.Instance;
            if (references == null || references.contactIcons == null)
            {
                return;
            }
            foreach (Sprite existing in references.contactIcons)
            {
                if (existing != null && existing.name == id)
                {
                    return;
                }
            }
            if (kept == null)
            {
                const int size = 96;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                float radius = size / 2f - 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                        Color pixel = distance < radius * 0.42f ? Color.white : colour;
                        pixel.a = Mathf.Clamp01(radius - distance);
                        pixels[y * size + x] = pixel;
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply();
                kept = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                kept.name = id;
            }
            Sprite[] old = references.contactIcons;
            var icons = new Sprite[old.Length + 1];
            old.CopyTo(icons, 0);
            icons[old.Length] = kept;
            references.contactIcons = icons;
        }
    }
}
