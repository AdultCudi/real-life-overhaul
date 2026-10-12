using System;
using System.Collections.Generic;
using Helpers;
using UI;
using UI.MiniMenu;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The player's side of the buses, all of it in the game's own interface:
    ///
    /// - POINTING. The pole, the shelter and the standing bus are things the game's pointer reacts to
    ///   (<see cref="TransitPointer"/>): outlined, named, with "Click to choose a stop", "Click to sit down" or
    ///   "Click to board" in the game's blue. As with the game's own objects, they can be pointed at from anywhere
    ///   in view, and a click from further off than arm's reach walks the player over first (the game's own
    ///   PlayerController.SetGoal, as a click on a subway entrance does).
    /// - AT THE STOP. Within <see cref="Reach"/> of the pole, the waiting place, the shelter or the standing bus,
    ///   the game's panel over a place (<see cref="GameUi.WorldPanel"/>: the box it shows at a petrol pump) says
    ///   which stop this is, the line or the stop the player has picked, and when the bus comes, and has buttons
    ///   with their keys: choose a stop on the game's interact key, sit down on its secondary one.
    /// - WHERE TO. On the city map, as the game offers its subway stations from an entrance (<see cref="GameUi.MapPicker"/>):
    ///   every stop the buses reach from here (<see cref="TransitTrips"/>) is a marker on the map and an entry in the
    ///   game's own window for its stations beside it, with its line, the time and the fare, searchable by name; the day
    ///   pass is the window's first entry and the stop the player is at is marked in the game's red. A pick closes the
    ///   map, and is taken up on the next tick once the game runs again (the map holds its clock still). Picked while the
    ///   bus stands, the ride begins. Picked while waiting, the stop is kept and the player gets on by themselves when
    ///   the bus stands, so nobody has to catch the moment. Should the game's window not be copied, the same list stands
    ///   in the middle of the screen (<see cref="GameUi.TravelList"/>).
    ///
    /// Nothing here runs per frame. On the director's 0.25 s timer the place is tested, and a text is built only
    /// when what it says has changed. The game asks <see cref="Offer"/> only for what the pointer is on.
    /// </summary>
    public sealed partial class TransitDirector
    {
        /// <summary>Metres at which the stop's panel and its keys are offered: the game's own reach of an object (EntityController.MaxReachDistance).</summary>
        public const float Reach = 3f;

        /// <summary>The colour the bus stops are marked in on the map: their own row's (World/MapLinks) and the stops offered from one.</summary>
        internal static readonly Color StopColour = new Color(0.05f, 0.56f, 0.62f);
        private const float PanelUp = 1.9f;          // metres up the pole the panel stands over: under the flag
        private const int PassRow = -1;              // the list's entry that sells the day pass

        private const string ButtonChoose = "reallife_transit_button_choose";
        private const string ButtonBoard = "reallife_transit_button_board";
        private const string ButtonChange = "reallife_transit_button_change";
        private const string ButtonSit = "reallife_transit_button_sit";
        private const string ButtonStand = "reallife_transit_button_stand";

        private readonly GameUi.WorldPanel _panel = new GameUi.WorldPanel { Name = "stop" };
        private readonly List<TransitTrip> _trips = new List<TransitTrip>(64);
        private readonly bool[] _pointed = new bool[Dressings];
        private GameUi.TravelList _list;
        private bool _listFailed;
        private GameUi.MapPicker _picker;
        private bool _pickerFailed;
        private TransitTrip _picked;        // the stop picked on the map: taken up once the map has closed and the game runs again
        private bool _hasPicked;
        private string _listClosing;        // why the list is being closed, for the log; null when the player cancels it
        private TransitPointer _busPointer;
        private BoxCollider _busBox;
        private bool _busPointed;
        private Vector3 _busCentre;
        private Vector3 _busForward;
        private float _busHalfLength;
        private float _busHalfWidth;
        private TransitStop _booked;        // the stop picked while waiting: boarded for when the bus stands
        private TransitStop _goStop;        // the stop a click is walking the player to
        private BusStopDressing _goShelter; // ...or the seat
        private int _goSeat = -1;
        private TransitTrip _asked;         // the one ride offered when the list could not be made
        private Action _onChoose;
        private Action _onSit;
        private Action _onStand;
        private Action _onListClosed;
        private Action _onAsked;
        private Action<int> _onPick;
        private Action<int> _onMapPick;
        private Action _onMapClosed;
        private UnityAction _chooseOnArrival;
        private UnityAction _sitOnArrival;
        private int _panelMore;
        private int _panelState;            // 0 not up, 1 up, 2 refused by the game's own panel
        private TransitEta.Mode _mode;
        private int _minutes;
        private int _metres;

        // Made once: the game keeps these for as long as a panel, a list or a walk lasts.
        private void Wire()
        {
            TransitCta.Logger = Logger;
            GameUi.WorldPanel.Logger = Logger;
            GameUi.TravelList.Logger = Logger;
            GameUi.MapPicker.Logger = Logger;
            _onChoose = ChooseClicked;
            _onSit = SitClicked;
            _onStand = StandClicked;
            _onListClosed = ListClosed;
            _onAsked = AskedYes;
            _onPick = Picked;
            _onMapPick = MapPicked;
            _onMapClosed = MapClosed;
            _chooseOnArrival = ChooseOnArrival;
            _sitOnArrival = SitOnArrival;
        }

        // ---------------------------------------------------------------- the timer

        private void RunPlayer(float now, Vector3 at, bool onFoot)
        {
            if (_onChoose == null)
            {
                Wire();
            }
            Pointers();
            if (_ride.Riding || (_picker != null && _picker.IsOpen))
            {
                return;   // (the city map is up with the stops on it, and the game's clock stands still under it)
            }
            if (_player == PlayerStep.Riding)
            {
                _player = PlayerStep.None;
            }
            bool listOpen = _list != null && _list.IsOpen;
            if (listOpen && (!onFoot || FullMenu.IsOpen || CityMap.IsOpen || MiniMenu.IsOpen || HudConfirm.isOpen || JobDirector.TakesInteractKey))
            {
                CloseList("another screen or prompt came up");
                listOpen = false;
            }
            bool free = onFoot && !JobDirector.TakesInteractKey && !_subway.Showing && (listOpen || !ThirdPersonCamera.IsUiOpen());

            // A free seat of a dressed shelter within reach.
            _sitAt = null;
            _sitSeat = -1;
            if (free && !listOpen && !_seat.Active)
            {
                for (int i = 0; i < Dressings && _sitAt == null; i++)
                {
                    BusStopDressing dressing = _dressings[i];
                    int seat = dressing != null ? dressing.FreeSeat(at, SitRange) : -1;
                    if (seat >= 0)
                    {
                        _sitAt = dressing;
                        _sitSeat = seat;
                    }
                }
                // (Asked only at a seat: whether the clips are there, and whether a menu, a dialog or a journey is in the way.)
                if (_sitAt != null && (!TransitSeat.Possible || !TransitRide.CanStart()))
                {
                    _sitAt = null;
                    _sitSeat = -1;
                }
            }

            bool standing = _served != null && _bus.State == TransitBus.Phase.Standing && _bus.Car != null;
            if (_served == null || !free || !(AtStop(_served, at) || (standing && AtBus(at))))
            {
                if (listOpen)
                {
                    CloseList("the player left the stop");
                }
                if (_hasPicked && (_served == null || !(AtStop(_served, at) || (standing && AtBus(at)))))
                {
                    // (Only for leaving the stop: a screen still closing as the game runs again keeps the pick for the next tick.)
                    _hasPicked = false;
                    Logger?.Info("Transit check (stop map): the stop picked on the map is let go: the player is no longer at the stop.");
                }
                if (_player != PlayerStep.None)
                {
                    _player = PlayerStep.None;
                }
                HidePanel();
                return;
            }
            if (listOpen)
            {
                // The list is up: the panel behind it is down, and the line under the list's headline follows the bus.
                _player = PlayerStep.Choosing;
                if (standing && _bus.Standing < 0.6f)
                {
                    _chooseUntil = now + ChooseFor;   // the bus has just pulled in under the open list: it waits for the choice
                }
                HidePanel();
                if (Measure(standing))
                {
                    _list.Info(ListInfo(standing));
                }
                return;
            }
            _player = PlayerStep.AtStop;
            if (_hasPicked)
            {
                // The stop picked on the map, now that the map has closed and the game runs again.
                TransitTrip picked = _picked;
                _hasPicked = false;
                _picked = default(TransitTrip);
                Chosen(picked);
                if (_ride.Riding)
                {
                    return;
                }
                standing = _bus.State == TransitBus.Phase.Standing && _bus.Car != null;
            }
            if (_booked != null && standing && !_busFull && !_seat.Active && _bus.Standing >= 1f && TransitRide.CanStart())
            {
                // The stop was picked while waiting: the bus stands, so they get on.
                TransitStop to = _booked;
                _booked = null;
                _panelKey = int.MinValue;
                if (Board(to, "had picked " + StopLabel(to) + " while waiting and gets on as the bus stands"))
                {
                    return;
                }
            }
            if (Measure(standing))
            {
                WritePanel(standing);
            }
            // Over the pole, under its flag; over the player's own head while the pole is behind the camera (on the bench, say).
            Vector3 over = _served.Sign + Vector3.up * PanelUp;
            Camera camera = GameManager.GetMainCamera();
            if (camera != null && Vector3.Dot(over - camera.transform.position, camera.transform.forward) < 0.5f)
            {
                over = at + Vector3.up * 2.1f;
            }
            _panel.Position = over;
            int state = _panel.Show() ? 1 : _panel.Waiting ? 3 : 2;
            if (state != _panelState)
            {
                _panelState = state;
                Logger?.Info("Transit check (stop panel): at " + StopLabel(_served) + ", " + Mathf.Sqrt(CitySpots.Flat(_served.Sign - at)).ToString("F1") + " m from the pole (offered within "
                             + Reach.ToString("F0") + " m of the pole, the waiting place, the shelter or the standing bus): "
                             + (state == 1 ? "the game's own panel is up, bus: " + PanelWords() + (_booked != null ? ", the player's stop is " + StopLabel(_booked) : "") + ": PASS"
                                 : state == 3 ? "the game's building box leaves the panel no room on any side, so it waits and is tried again while the player stands here: PASS"
                                 : "FAIL (the game's panel is showing another place, a filling station or a lift; it is tried again while the player stands here)") + ".");
            }
        }

        // What the panel and the list say of the bus, measured. True when any of it differs from what was last written.
        private bool Measure(bool standing)
        {
            TransitEta.Mode mode = standing ? TransitEta.Mode.Here : _eta.State;
            bool busOut = _bus.State == TransitBus.Phase.Approaching;
            if (mode == TransitEta.Mode.Here && !standing)
            {
                mode = TransitEta.Mode.Timetable;   // (the tick between a bus leaving and the timetable being set)
            }
            // The figure, in game minutes: the clock's rate is measured and wobbles, the figure shown does not rise for that.
            int minutes = Mathf.Max(1, Mathf.CeilToInt(_eta.Seconds * _minutesPerSecond));
            if (_eta.Epoch == _shownEpoch && minutes > _shownMinutes)
            {
                minutes = _shownMinutes;
            }
            _shownEpoch = _eta.Epoch;
            _shownMinutes = minutes;
            bool figure = mode == TransitEta.Mode.Timetable || mode == TransitEta.Mode.EnRoute;
            int metres = busOut && (mode == TransitEta.Mode.Delayed || mode == TransitEta.Mode.EnRoute) ? Mathf.Clamp(Mathf.RoundToInt(_bus.Remaining / 10f) * 10, 10, 4090) : 0;
            int hold = busOut ? (int)_bus.Waiting : 0;
            int state = standing ? (_busFull ? 5 : 4) : (int)mode;
            int seat = _seat.Active ? (_seat.Seated ? 2 : 1) : 0;
            int key = (_served.Id << 20) | (state << 16) | ((figure ? Mathf.Min(minutes, 999) : 0) << 6) | (hold << 4) | (seat << 2) | (Raining() ? 2 : 0) | (TransitState.HasPass ? 1 : 0);
            int more = metres | ((_booked != null ? _booked.Id + 1 : 0) << 12) | (_sitAt != null ? 1 << 21 : 0);
            _mode = mode;
            _minutes = minutes;
            _metres = metres;
            if (key == _panelKey && more == _panelMore)
            {
                return false;
            }
            _panelKey = key;
            _panelMore = more;
            return true;
        }

        // The panel's three lines and its buttons, written when Measure says they have changed.
        private void WritePanel(bool standing)
        {
            TransitLine line = TransitMap.Lines[_served.Line];
            string left = _booked != null ? TransitText.T("reallife_transit_panel_booked", "name", StopName(_booked))
                : TransitMap.Onward(_served) == 0 ? TransitText.T("reallife_transit_panel_line_end", "route", line.Number)
                : TransitText.T("reallife_transit_stop_line", "route", line.Number, "to", line.To);
            string right;
            Color32 colour;
            if (standing)
            {
                right = TransitText.T(_busFull ? "reallife_transit_status_full" : "reallife_transit_status_here");
                colour = _busFull ? GameUi.WorldPanel.Red() : GameUi.WorldPanel.Green();
            }
            else if (_mode == TransitEta.Mode.Delayed)
            {
                right = TransitText.T("reallife_transit_status_delayed");
                colour = GameUi.WorldPanel.Yellow();
            }
            else
            {
                right = TransitText.T("reallife_transit_status_minutes", "minutes", _minutes.ToString());
                colour = GameUi.WorldPanel.White();
            }
            _panel.Lines(TransitText.T("reallife_transit_stop_title", "name", StopName(_served)), left, right, colour);
            if (_seat.Active)
            {
                _panel.Buttons(_seat.Seated ? ButtonStand : null, _onStand, true, null, null, false);
            }
            else
            {
                string first = standing && !_busFull && _booked == null ? ButtonBoard : _booked != null ? ButtonChange : ButtonChoose;
                _panel.Buttons(first, _onChoose, true, _sitAt != null && !standing ? ButtonSit : null, _onSit, true);
            }
        }

        // The two lines under the list's headline: the bus, and what a ride costs.
        private string ListInfo(bool standing)
        {
            TransitLine line = TransitMap.Lines[_served.Line];
            string bus;
            if (standing)
            {
                bus = TransitText.T(_busFull ? "reallife_transit_list_info_full" : "reallife_transit_list_info_here", "route", line.Number);
            }
            else if (_mode == TransitEta.Mode.EnRoute)
            {
                bus = TransitText.T("reallife_transit_list_info_coming", "route", line.Number, "metres", _metres.ToString(), "minutes", _minutes.ToString());
            }
            else if (_mode == TransitEta.Mode.Delayed)
            {
                bus = _bus.State != TransitBus.Phase.Approaching ? TransitText.T("reallife_transit_list_info_delayed")
                    : TransitText.T(_bus.Waiting == TransitBus.Hold.Light ? "reallife_transit_stop_delayed_light"
                        : _bus.Waiting == TransitBus.Hold.Traffic ? "reallife_transit_stop_delayed_traffic"
                        : _bus.Waiting == TransitBus.Hold.Closure ? "reallife_transit_stop_delayed_closure" : "reallife_transit_stop_delayed_late", "metres", _metres.ToString());
            }
            else
            {
                bus = TransitText.T("reallife_transit_list_info_next", "route", line.Number, "minutes", _minutes.ToString());
            }
            _text.Length = 0;
            _text.Append(bus).Append('\n');
            if (Raining() && !standing)
            {
                _text.Append(TransitText.T("reallife_transit_stop_rain")).Append(". ");
            }
            _text.Append(TransitState.HasPass ? TransitText.T("reallife_transit_stop_pass")
                : TransitText.T("reallife_transit_list_info_fare", "fare", TransitText.Money(TransitState.BusFare)));
            return _text.ToString();
        }

        // ---------------------------------------------------------------- where the player is

        /// <summary>At a stop: within reach of its pole, its waiting place or its shelter.</summary>
        private static bool AtStop(TransitStop stop, Vector3 at)
        {
            const float within = Reach * Reach;
            return Mathf.Abs(stop.Sign.y - at.y) < 3f
                   && (CitySpots.Flat(stop.Sign - at) < within || CitySpots.Flat(stop.Wait - at) < within || (stop.Shelter && CitySpots.Flat(stop.ShelterAt - at) < within));
        }

        /// <summary>At the standing bus: within reach of its body, whichever side or end.</summary>
        private bool AtBus(Vector3 at)
        {
            if (!_busPointed)
            {
                return false;
            }
            Vector3 offset = at - _busCentre;
            float along = offset.x * _busForward.x + offset.z * _busForward.z;
            float aside = offset.x * _busForward.z - offset.z * _busForward.x;
            return Mathf.Abs(along) < _busHalfLength + Reach && Mathf.Abs(aside) < _busHalfWidth + Reach;
        }

        // The dressed stop the player stands at, or null. A pass over the three dressings, once a second.
        private TransitStop StoodAt(Vector3 at)
        {
            for (int i = 0; i < Dressings; i++)
            {
                TransitStop stop = _dressings[i] != null ? _dressings[i].Stop : null;
                if (stop != null && AtStop(stop, at))
                {
                    return stop;
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- pointing at a stop or the bus

        // Each dressing gets its two pointers when it is first seen, and the bus's box follows whether a bus stands.
        private void Pointers()
        {
            bool installed = TransitCta.Install();
            for (int i = 0; installed && i < Dressings; i++)
            {
                BusStopDressing dressing = _dressings[i];
                if (dressing == null || _pointed[i])
                {
                    continue;
                }
                _pointed[i] = true;
                TransitPointer.Make(dressing.Sign, TransitPointer.Kind.Stop, dressing, new Vector3(0.25f, 1.45f, 0f), new Vector3(0.7f, 2.9f, 0.35f), true);
                TransitPointer.Make(dressing.Shelter, TransitPointer.Kind.Bench, dressing, new Vector3(0f, 1.25f, 0f),
                    new Vector3(BusStopDressing.ShelterDepth, 2.5f, BusStopDressing.ShelterLength), true);
            }
            BusPointer(_served != null && _bus.State == TransitBus.Phase.Standing && _bus.Car != null);
        }

        // The box round the standing bus: put where the bus has stopped, switched off when it goes. The bus itself is not touched.
        private void BusPointer(bool on)
        {
            if (on == _busPointed)
            {
                return;
            }
            _busPointed = on;
            if (!on)
            {
                if (_busPointer != null)
                {
                    _busPointer.gameObject.SetActive(false);
                }
                return;
            }
            TrafficTools.Footprint(_bus.Car, out _busCentre, out _busForward, out _busHalfLength, out _busHalfWidth);
            _busForward.y = 0f;
            _busForward = _busForward.sqrMagnitude > 0.01f ? _busForward.normalized : Vector3.forward;
            if (!TransitCta.Installed)
            {
                return;   // (the place is still measured: the panel is offered beside the bus all the same)
            }
            if (_busPointer == null)
            {
                _busPointer = TransitPointer.Make(_root, TransitPointer.Kind.Bus, null, Vector3.zero, Vector3.one, false);
                if (_busPointer == null)
                {
                    return;
                }
                _busBox = _busPointer.GetComponent<BoxCollider>();
            }
            _busPointer.transform.SetPositionAndRotation(new Vector3(_busCentre.x, _bus.Car.transform.position.y + 1.4f, _busCentre.z), Quaternion.LookRotation(_busForward, Vector3.up));
            _busBox.size = new Vector3(_busHalfWidth * 2f + 0.2f, 2.8f, _busHalfLength * 2f + 0.2f);
            _busPointer.gameObject.SetActive(true);
        }

        /// <summary>
        /// What the game's pointer is offered on one of the buses' pointers: a locale key, or null for nothing. Asked
        /// by the game when the pointer comes onto the object and when it is clicked, never per frame.
        /// </summary>
        public static string Offer(TransitPointer pointer)
        {
            TransitDirector director = Instance;
            return director != null && pointer != null ? director.PointerOffer(pointer) : null;
        }

        private string PointerOffer(TransitPointer pointer)
        {
            if (_failed || !Settings.Buses || _ride.Riding || _seat.Active || (_list != null && _list.IsOpen) || (_picker != null && _picker.IsOpen) || PlayerHelper.IsUsingVehicle || BuildingManager.IsInsideBuilding)
            {
                return null;
            }
            if (pointer.What == TransitPointer.Kind.Bus)
            {
                return _served != null && _bus.State == TransitBus.Phase.Standing && !_busFull ? TransitCta.Board : null;
            }
            BusStopDressing dressing = pointer.Dressing;
            if (dressing == null || dressing.Stop == null)
            {
                return null;
            }
            if (pointer.What == TransitPointer.Kind.Bench && _bus.State != TransitBus.Phase.Standing && TransitSeat.Possible
                && dressing.FreeSeat(PlayerHelper.GetPosition(), 1000f) >= 0)
            {
                return TransitCta.Sit;
            }
            return TransitCta.Choose;
        }

        /// <summary>The game ran the click on one of the buses' pointers.</summary>
        internal void PointerClicked(TransitPointer pointer)
        {
            try
            {
                string offer = PointerOffer(pointer);
                var game = InstanceBehavior<GameManager>.Instance;
                PlayerController player = game != null ? game.playerController : null;
                if (offer == null || player == null)
                {
                    return;
                }
                if (_onChoose == null)
                {
                    Wire();
                }
                Vector3 at = player.transform.position;
                if (offer == TransitCta.Sit)
                {
                    BusStopDressing shelter = pointer.Dressing;
                    int seat = shelter.FreeSeat(at, 1000f);
                    if (seat < 0)
                    {
                        return;
                    }
                    Vector3 feet = shelter.SeatFeetAt(seat);
                    float away = Mathf.Sqrt(CitySpots.Flat(feet - at));
                    Logger?.Info("Transit check (pointer): the player clicks the bench at " + StopLabel(shelter.Stop) + ", " + away.ToString("F1") + " m from seat " + (seat + 1) + ": "
                                 + (away <= SitRange ? "they sit down" : "the game walks them to it first") + ": PASS.");
                    if (away <= SitRange)
                    {
                        SitDown(shelter, seat);
                    }
                    else
                    {
                        _goStop = null;
                        _goShelter = shelter;
                        _goSeat = seat;
                        player.SetGoal(feet, _sitOnArrival);
                    }
                    return;
                }
                bool bus = pointer.What == TransitPointer.Kind.Bus;
                TransitStop stop = bus ? _served : pointer.Dressing.Stop;
                if (stop == null)
                {
                    return;
                }
                bool here = AtStop(stop, at) || (stop == _served && _bus.State == TransitBus.Phase.Standing && AtBus(at));
                Logger?.Info("Transit check (pointer): the player clicks " + (bus ? "the bus standing at " : pointer.What == TransitPointer.Kind.Bench ? "the shelter of " : "the pole of ") + StopLabel(stop)
                             + ", " + Mathf.Sqrt(CitySpots.Flat(stop.Sign - at)).ToString("F1") + " m from the pole: " + (here ? "the list of stops is opened" : "the game walks them to the stop first") + ": PASS.");
                if (here)
                {
                    ChooseAt(stop);
                }
                else
                {
                    _goShelter = null;
                    _goStop = stop;
                    player.SetGoal(stop.Wait, _chooseOnArrival);
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // The walk a click started has ended at the stop.
        private void ChooseOnArrival()
        {
            try
            {
                TransitStop stop = _goStop;
                _goStop = null;
                if (stop == null || _failed || !Settings.Buses || _ride.Riding || PlayerHelper.IsUsingVehicle)
                {
                    return;
                }
                Vector3 at = PlayerHelper.GetPosition();
                if (!AtStop(stop, at))
                {
                    Logger?.Info("Transit check (pointer): the walk to " + StopLabel(stop) + " ended " + Mathf.Sqrt(CitySpots.Flat(stop.Wait - at)).ToString("F1")
                                 + " m from its waiting place: FAIL (nothing is opened; the panel is offered within " + Reach.ToString("F0") + " m).");
                    return;
                }
                ChooseAt(stop);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void SitOnArrival()
        {
            try
            {
                BusStopDressing shelter = _goShelter;
                int seat = _goSeat;
                _goShelter = null;
                _goSeat = -1;
                if (shelter == null || shelter.Stop == null || _failed || _ride.Riding || _seat.Active || !shelter.SeatFree(seat)
                    || CitySpots.Flat(shelter.SeatFeetAt(seat) - PlayerHelper.GetPosition()) > SitRange * SitRange)
                {
                    return;
                }
                SitDown(shelter, seat);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // The list at a dressed stop the player stands at: it becomes the stop that is served, if it was not.
        private void ChooseAt(TransitStop stop)
        {
            if (_served != stop)
            {
                if (!Dressed(stop))
                {
                    return;
                }
                if (_served != null)
                {
                    EndService();
                }
                BeginService(stop, Time.time);
            }
            OpenList();
        }

        // ---------------------------------------------------------------- the panel's buttons

        private void ChooseClicked()
        {
            try
            {
                _keyFrame = Time.frameCount;   // the key that pressed the button does nothing else this frame
                if (_served != null && !_ride.Riding && !_seat.Active)
                {
                    OpenList();
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void SitClicked()
        {
            try
            {
                _keyFrame = Time.frameCount;
                if (_sitAt != null && !_ride.Riding)
                {
                    SitDown(_sitAt, _sitSeat);
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void StandClicked()
        {
            try
            {
                _keyFrame = Time.frameCount;
                if (_seat.Seated)
                {
                    _seat.Stand(Time.time);
                    _panelKey = int.MinValue;
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void SitDown(BusStopDressing shelter, int seat)
        {
            bool sat = _seat.Sit(shelter, seat, out string why);
            Logger?.Info("Transit check (seat): the player sits down on seat " + (seat + 1) + " of " + BusStopDressing.Seats + " at " + StopLabel(shelter.Stop) + ", seat "
                         + BusStopDressing.BenchSeat.ToString("F2") + " m up as the clips are made for"
                         + (sat ? "; feet " + _seat.FeetOff.ToString("F2") + " m from where the clip wants them: " + (_seat.FeetOff < 0.12f ? "PASS" : "FAIL (the mesh does not reach the seat's place)")
                             : ": FAIL (" + why + ")") + ".");
            _sitAt = null;
            _sitSeat = -1;
            _panelKey = int.MinValue;
        }

        // ---------------------------------------------------------------- the list of stops

        private void OpenList()
        {
            if (_served == null || _ride.Riding || _seat.Active || (_list != null && _list.IsOpen) || (_picker != null && _picker.IsOpen))
            {
                return;
            }
            float began = Time.realtimeSinceStartup;
            if (OpenMap(began))
            {
                return;
            }
            if ((_list == null || !_list.Alive) && !_listFailed)
            {
                _list = GameUi.TravelList.Make("RealLifeOverhaul Bus Stops");
                _listFailed = _list == null;
            }
            bool standing = _bus.State == TransitBus.Phase.Standing;
            Measure(standing);            // (the figure the list is about to show)
            _panelKey = int.MinValue;     // ...and the panel is written again when it is next up
            if (_list == null || !_list.Alive)
            {
                AskOne();
                return;
            }
            int direct = FillList(standing);
            _list.Open(_onPick, _onListClosed);
            _player = PlayerStep.Choosing;
            _chooseUntil = Time.time + ChooseFor;
            _listClosing = null;
            HidePanel();
            Logger?.Info("Transit check (stop list): the game's list is open at " + StopLabel(_served) + ": " + _trips.Count + " stops, " + direct + " further along this line and "
                         + (_trips.Count - direct) + " with a change of bus (none of another line within " + TransitTrips.WalkedTo.ToString("F0") + " m); the day pass "
                         + (TransitState.HasPass ? "is held" : "is its first entry") + "; " + _list.Made + " entries made so far; bus: " + PanelWords() + "; "
                         + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1") + " ms: " + (_trips.Count > 0 ? "PASS" : "FAIL (the network has no other stop)") + ".");
        }

        // The city map with the stops on it, as the game's subway entrance opens it for its stations. False if the game's
        // window could not be copied (said once) or the game would not open its map now: the list is used then.
        private bool OpenMap(float began)
        {
            if ((_picker == null || !_picker.Alive) && !_pickerFailed)
            {
                _picker = GameUi.MapPicker.Make("RealLifeOverhaul Bus Stops Map");
                _pickerFailed = _picker == null;
            }
            if (_picker == null || !_picker.Alive)
            {
                return false;
            }
            bool standing = _bus.State == TransitBus.Phase.Standing;
            Measure(standing);            // (the figure the window is about to show)
            _panelKey = int.MinValue;     // ...and the panel is written again when it is next up
            _hasPicked = false;
            int direct = FillMap(standing);
            if (!_picker.Open(_served.Sign, _onMapPick, _onMapClosed))
            {
                Logger?.Info("Transit check (stop map): the game would not open its map at " + StopLabel(_served) + " now; the list is used instead.");
                return false;
            }
            _player = PlayerStep.Choosing;
            _chooseUntil = Time.time + ChooseFor;
            _listClosing = null;
            HidePanel();
            Logger?.Info("Transit check (stop map): the city map is open at " + StopLabel(_served) + " with the game's window for its stations beside it: " + _trips.Count + " stops, "
                         + direct + " further along this line and " + (_trips.Count - direct) + " with a change of bus, each a marker on the map; the day pass "
                         + (TransitState.HasPass ? "is held" : "is its first entry") + "; " + _picker.Made + " entries and " + _picker.Marked + " markers made so far; bus: " + PanelWords() + "; "
                         + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1") + " ms: " + (_trips.Count > 0 ? "PASS" : "FAIL (the network has no other stop)") + ".");
            return true;
        }

        // Every stop offered from the served one, written into the map's window and onto the map. Returns how many are on this line.
        private int FillMap(bool standing)
        {
            int direct = TransitTrips.From(_served, _trips);
            _picker.Begin(TransitText.T("reallife_transit_list_title", "name", StopName(_served)), MapPlaces.Glyph(MapIcon.Bus), StopColour);
            _picker.Note(ListInfo(standing));
            bool pass = TransitState.HasPass;
            if (!pass)
            {
                _picker.Add(PassRow, TransitText.T("reallife_transit_list_pass"), TransitText.T("reallife_transit_list_pass_detail", "pass", TransitText.Money(TransitState.DayPass)),
                    "reallife_transit_list_buy", true, false, Vector3.zero);
            }
            string fare = pass ? TransitText.T("reallife_transit_list_fare_pass") : TransitText.Money(TransitState.BusFare);
            for (int i = 0; i < _trips.Count; i++)
            {
                TransitTrip trip = _trips[i];
                TransitLine line = TransitMap.Lines[trip.To.Line];
                _picker.Add(i, StopName(trip.To), TransitText.T(trip.Direct ? "reallife_transit_list_direct" : "reallife_transit_list_change", "route", line.Number, "to", line.To,
                    "minutes", Mathf.CeilToInt(trip.Minutes).ToString(), "fare", fare), "common_travel", true, true, trip.To.Sign);
            }
            _picker.Here(_served.Sign, StopName(_served));
            return direct;
        }

        // An entry or a marker of the map was clicked: the day pass is bought there and then; a stop closes the map.
        private void MapPicked(int id)
        {
            try
            {
                _keyFrame = Time.frameCount;
                if (_served == null || _picker == null)
                {
                    CloseList("no stop is served any more");
                    return;
                }
                if (id == PassRow)
                {
                    bool bought = !TransitState.HasPass && TransitState.BuyPass();
                    Logger?.Info("Transit check (choice): the day pass, from the map at " + StopLabel(_served) + ": " + (bought ? "bought for " + TransitState.DayPass.ToString("F2") + ": PASS" : "not bought (no money, or one is held): FAIL") + ".");
                    if (bought)
                    {
                        Notifications.Show(NotificationType.Success, "reallife_transit_pass_bought", null, 6f);
                        FillMap(_bus.State == TransitBus.Phase.Standing);   // the entry goes, and every fare now reads as the pass
                        _picker.Open(_served.Sign, _onMapPick, _onMapClosed);
                    }
                    return;
                }
                if (id < 0 || id >= _trips.Count)
                {
                    return;
                }
                _picked = _trips[id];
                _hasPicked = true;
                CloseList("a stop was picked on the map");
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // Runs once per closing of the map, whoever closed it (a pick, Cancel, the game's cancel key, or the director).
        private void MapClosed()
        {
            _panelKey = int.MinValue;   // the panel comes back with what is true now
            if (_player == PlayerStep.Choosing)
            {
                _player = PlayerStep.AtStop;
            }
            Logger?.Info("Transit check (stop map): closed (" + (_listClosing ?? "cancelled by the player") + ")" + (_hasPicked ? "; the stop picked is taken up when the game runs again" : string.Empty) + ".");
            _listClosing = null;
        }

        // Every stop offered from the served one, written into the list. Returns how many are on this line.
        private int FillList(bool standing)
        {
            int direct = TransitTrips.From(_served, _trips);
            _list.Begin(TransitText.T("reallife_transit_list_title", "name", StopName(_served)), ListInfo(standing));
            bool pass = TransitState.HasPass;
            if (!pass)
            {
                _list.Add(PassRow, TransitText.T("reallife_transit_list_pass"), TransitText.T("reallife_transit_list_pass_detail", "pass", TransitText.Money(TransitState.DayPass)),
                    "reallife_transit_list_buy", true);
            }
            string fare = pass ? TransitText.T("reallife_transit_list_fare_pass") : TransitText.Money(TransitState.BusFare);
            for (int i = 0; i < _trips.Count; i++)
            {
                TransitTrip trip = _trips[i];
                TransitLine line = TransitMap.Lines[trip.To.Line];
                _list.Add(i, StopName(trip.To), TransitText.T(trip.Direct ? "reallife_transit_list_direct" : "reallife_transit_list_change", "route", line.Number, "to", line.To,
                    "minutes", Mathf.CeilToInt(trip.Minutes).ToString(), "fare", fare), "common_travel", true);
            }
            return direct;
        }

        // An entry's button was clicked.
        private void Picked(int id)
        {
            try
            {
                _keyFrame = Time.frameCount;
                if (_served == null || _list == null)
                {
                    CloseList("no stop is served any more");
                    return;
                }
                if (id == PassRow)
                {
                    bool bought = !TransitState.HasPass && TransitState.BuyPass();
                    Logger?.Info("Transit check (choice): the day pass, from the list at " + StopLabel(_served) + ": " + (bought ? "bought for " + TransitState.DayPass.ToString("F2") + ": PASS" : "not bought (no money, or one is held): FAIL") + ".");
                    if (bought)
                    {
                        Notifications.Show(NotificationType.Success, "reallife_transit_pass_bought", null, 6f);
                        FillList(_bus.State == TransitBus.Phase.Standing);   // the entry goes, and every fare now reads as the pass
                        _list.Open(_onPick, _onListClosed);
                    }
                    return;
                }
                if (id < 0 || id >= _trips.Count)
                {
                    return;
                }
                TransitTrip trip = _trips[id];
                CloseList("a stop was picked");
                Chosen(trip);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // A stop has been picked: aboard now if the bus stands, else kept for when it does.
        private void Chosen(TransitTrip trip)
        {
            _panelKey = int.MinValue;
            if (_served == null || trip.To == null)
            {
                return;
            }
            TransitLine line = TransitMap.Lines[trip.To.Line];
            string what = "the player picks " + StopLabel(trip.To) + " at " + StopLabel(_served) + " (" + (trip.Direct ? "the same bus" : "with a change of bus") + ", "
                          + trip.Metres.ToString("F0") + " m, " + trip.Minutes.ToString("F0") + " game minutes)";
            if (_bus.State == TransitBus.Phase.Standing && !_busFull && TransitRide.CanStart())
            {
                Logger?.Info("Transit check (choice): " + what + " while the bus stands: they board now: PASS.");
                Board(trip.To, "picks " + StopLabel(trip.To) + " from the list");
                return;
            }
            _booked = trip.To;
            Notifications.Show(NotificationType.Info, "reallife_transit_booked",
                new Dictionary<string, string>(2) { { "route", TransitMap.Lines[_served.Line].Number }, { "name", StopName(trip.To) } }, 7f);
            Logger?.Info("Transit check (choice): " + what + " with no bus to board yet (bus: " + PanelWords() + "): the stop is kept and they board when a bus that is not full stands and they are at the stop (line of the stop: "
                         + line.Number + "): PASS.");
        }

        private void CloseList(string why)
        {
            if (_list != null && _list.IsOpen)
            {
                _listClosing = why;
                _list.Close();
            }
            if (_picker != null && _picker.IsOpen)
            {
                _listClosing = why;
                _picker.Close();
            }
        }

        // Runs once per closing, whoever closed it (a pick, the Cancel button, the game's cancel key, or the director).
        private void ListClosed()
        {
            _panelKey = int.MinValue;   // the panel comes back with what is true now
            if (_player == PlayerStep.Choosing)
            {
                _player = PlayerStep.AtStop;
            }
            Logger?.Info("Transit check (stop list): closed (" + (_listClosing ?? "cancelled by the player") + ").");
            _listClosing = null;
        }

        // The game's list could not be copied (one "Game UI:" warning says why): the game's own question is asked instead,
        // for the one ride that fits most: to the end of this line, or from a line's last stop to the nearest other stop.
        private void AskOne()
        {
            int direct = TransitTrips.From(_served, _trips);
            if (_trips.Count == 0)
            {
                return;
            }
            _asked = _trips[direct > 0 ? direct - 1 : 0];
            Logger?.Info("Transit check (stop list): the game's list could not be made, so the game's question is asked for the one ride to " + StopLabel(_asked.To) + ": FAIL.");
            HudConfirm.Show(TransitText.T("reallife_transit_list_title", "name", StopName(_served)),
                TransitText.T("reallife_transit_ask_one", "name", StopName(_asked.To), "minutes", Mathf.CeilToInt(_asked.Minutes).ToString(),
                    "fare", TransitState.HasPass ? TransitText.T("reallife_transit_list_fare_pass") : TransitText.Money(TransitState.BusFare)),
                _onAsked, null, null, null, false);
        }

        private void AskedYes()
        {
            try
            {
                Chosen(_asked);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // ---------------------------------------------------------------- the ride

        // The fare is paid and the ride begins. False if it cannot begin now, or the fare could not be paid (the game says so).
        private bool Board(TransitStop to, string how)
        {
            if (_served == null || to == null || to == _served || !TransitRide.CanStart() || _bus.State != TransitBus.Phase.Standing || _busFull)
            {
                return false;
            }
            bool pass = TransitState.HasPass;
            if (!TransitState.Pay(TransitState.BusFare, "reallife_transaction_bus_fare"))
            {
                Logger?.Info("Transit check (ride): the player " + how + " but cannot pay the fare of " + TransitState.BusFare.ToString("F2") + ": nobody boards: FAIL.");
                return false;
            }
            StartRide(_served, to, pass, how + " while the bus stands (" + _bus.Standing.ToString("F0") + " s)");
            return true;
        }

        private void StartRide(TransitStop from, TransitStop to, bool pass, string how)
        {
            TransitTrip trip = TransitTrips.Between(from, to);
            float minutes = trip.Minutes;
            TransitLine line = TransitMap.Lines[from.Line];
            _rideTo = to;
            _player = PlayerStep.Riding;
            _seat.Abort();
            HideHud();
            TapCard();
            TransitState.BusRides++;
            bool sound = to != from && minutes >= 1f;
            string paid = pass ? "on a day pass" : "fare " + TransitState.BusFare.ToString("F2");
            if (trip.Direct)
            {
                _text.Length = 0;
                for (int i = from.Order + 1; i <= to.Order && i < line.Stops.Count; i++)
                {
                    _text.Append(i > from.Order + 1 ? ", " : "").Append(line.Stops[i].Name ?? "stop " + (i + 1));
                }
                Logger?.Info("Transit check (ride): the player " + how + " and rides the " + line.Number + " from " + (from.Name ?? "stop " + (from.Order + 1)) + ", calling at " + _text
                             + ": " + (to.Order - from.Order) + " stop(s), " + trip.Metres.ToString("F0") + " m, " + minutes.ToString("F0") + " game minutes, "
                             + paid + ": " + (sound ? "PASS" : "FAIL (not a ride to another stop)") + ".");
            }
            else
            {
                Logger?.Info("Transit check (ride): the player " + how + " and rides from " + StopLabel(from) + " to " + StopLabel(to) + " with a change of bus: about " + trip.Metres.ToString("F0")
                             + " m of road (the straight line times " + TransitTrips.RoadFactor.ToString("F2") + "), " + minutes.ToString("F0") + " game minutes with "
                             + TransitTrips.ChangeMinutes.ToString("F0") + " for the change, " + paid + ": " + (sound ? "PASS" : "FAIL (not a ride to another stop)") + ".");
            }
            _ride.Begin(minutes, to.Wait, "reallife_transit_ride_fade", RideFaded, RideArrived);
        }

        private void RideFaded()
        {
            // Nobody can see: the stop the player left is cleared, and the stop they will get off at is made ready.
            ClearStreet(true);
            _streetClear = false;
        }

        private void RideArrived()
        {
            TransitStop to = _rideTo;
            _rideTo = null;
            if (to == null)
            {
                return;
            }
            DressNear(to.Sign, true, Time.time);
            // The bus that brought them pulls away: put on the lane while the screen is still dark (traffic may need a moment to start again).
            _seeOff = to;
            _seeOffBy = Time.time + 1.2f;
            ModState.AddModifier(ModState.MoodUp);
            Notifications.Show(NotificationType.Info, "reallife_transit_ride_arrived", new Dictionary<string, string>(1) { { "name", StopName(to) } }, 6f);
            Vector3 at = PlayerHelper.GetPosition();
            float off = Mathf.Sqrt(CitySpots.Flat(at - to.Wait));
            bool onPavement = CitySpots.Pavement(at, 0.3f, out _);
            Logger?.Info("Transit check (put-down): the player stands " + off.ToString("F1") + " m from the waiting place of " + StopLabel(to) + ", on the pavement: " + (onPavement ? "yes" : "no")
                         + ", the stop dressed: " + (Dressed(to) ? "yes" : "no") + ": " + (off < 3f && onPavement ? "PASS" : "FAIL") + ".");
        }

        // The card on the reader: the upper body only, over whatever the legs are doing.
        internal static void TapCard()
        {
            try
            {
                var game = InstanceBehavior<GameManager>.Instance;
                Animator animator = game != null && game.playerController != null && game.playerController.Character != null ? game.playerController.Character.animator : null;
                if (animator != null && ClipLibrary.TryGet("pay_tap", out CityClip tap))
                {
                    ActivityAnimator player = ActivityAnimators.For(animator);
                    if (player != null && player.Usable)
                    {
                        player.PlayOver(tap.Clip, BodyMask.Upper, 0.15f);
                    }
                }
                SfxManager sounds = InstanceBehavior<SfxManager>.Instance;
                if (sounds != null && game != null && game.playerController != null)
                {
                    sounds.PlayAudio(BigAmbitions.SoundSystem.SoundType.PurchaseSuccess, game.playerController.transform.position);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Game minutes of a ride from one stop to another (see <see cref="TransitTrips"/>).</summary>
        public static float RideMinutes(TransitStop from, TransitStop to)
        {
            return TransitTrips.Between(from, to).Minutes;
        }

        // ---------------------------------------------------------------- names, and putting things away

        private static string StopName(TransitStop stop)
        {
            return stop.Name ?? TransitText.T("reallife_transit_stop_number", "number", (stop.Order + 1).ToString());
        }

        // A stop for the log: its line and its name, in plain words whatever the game's language.
        private static string StopLabel(TransitStop stop)
        {
            TransitLine line = stop.Line >= 0 && stop.Line < TransitMap.Lines.Count ? TransitMap.Lines[stop.Line] : null;
            return "the " + (line != null ? line.Number + " " : "") + "stop at " + (stop.Name ?? "number " + (stop.Order + 1));
        }

        private void HidePanel()
        {
            if (_panelState != 0)
            {
                _panelState = 0;
                _panel.Hide();
            }
        }

        // Everything the player is shown for the buses, down: the panel, the list, the bus's pointer; and the stop picked is forgotten.
        private void HideHud()
        {
            CloseList("the service at this stop ended, or a ride began");
            HidePanel();
            BusPointer(false);
            _booked = null;
            _hasPicked = false;
        }

        // With the director itself: the list's window goes, and the buses' entry leaves the game's list of click actions.
        private void RemovePlayerUi()
        {
            try
            {
                HideHud();
                _panel.Hide();
                _list?.Destroy();
                _list = null;
                _picker?.Destroy();
                _picker = null;
                _subway.Dispose();
                TransitCta.Remove();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
