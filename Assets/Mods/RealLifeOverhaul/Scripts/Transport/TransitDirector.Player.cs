using System;
using System.Collections.Generic;
using System.Globalization;
using UI;
using UI.MiniMenu;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The bus director's part for the player's side, all in the game's own interface: the pointer on the pole, the shelter and
    /// the bus (<see cref="TransitPointer"/>), the game's panel over the stop (<see cref="GameUi.WorldPanel"/>), and where to
    /// picked on the city map (<see cref="GameUi.MapPicker"/>) or a list. Nothing per frame: tested on the director's timer,
    /// texts built only when they change.
    /// </summary>
    public sealed partial class TransitDirector
    {
        /// <summary>Metres within which the stop's panel is offered: the game's own reach (EntityController.MaxReachDistance).</summary>
        public const float Reach = 3f;

        /// <summary>The colour the bus stops are marked in on the map.</summary>
        internal static readonly Color StopColour = new Color(0.05f, 0.56f, 0.62f);
        private const float PanelUp = 1.9f;          // metres up the pole the panel stands over: under the flag
        private const int PassRow = -1;              // the list's entry that sells the day pass

        private const string ButtonChoose = "reallife_transit_button_choose";
        private const string ButtonBoard = "reallife_transit_button_board";
        private const string ButtonChange = "reallife_transit_button_change";
        private const string ButtonSit = "reallife_transit_button_sit";
        private const string ButtonStand = "reallife_transit_button_stand";
        private const string ButtonSkipChange = "reallife_transit_button_skip_change";

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
        private BusStopDressing _goShelter; // or the seat
        private int _goSeat = -1;
        private TransitTrip _asked;         // the one ride offered when the list could not be made
        private Action _onChoose;
        private Action _onSit;
        private Action _onStand;
        private Action _onListClosed;
        private Action _onAsked;
        private Action _onSkipChange;
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

        // Made once, as the game keeps these for as long as a panel, a list or a walk lasts.
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
            GameUi.PanelPlaces.Set("bus stop", StopsNear);   // somebody waiting for the player is stood clear of them
        }

        // The stops near a point, with the reach of the stop's panel from its pole, its waiting place and its shelter (Core/GameUi/PanelPlaces).
        private static int StopsNear(Vector3 around, float within, float[] x, float[] z, float[] reach, int count)
        {
            List<TransitStop> stops = TransitMap.Places;
            for (int i = 0; i < stops.Count; i++)
            {
                TransitStop stop = stops[i];
                if (stop == null)
                {
                    continue;
                }
                count = GameUi.PanelPlaces.Add(around, within, stop.Sign, Reach, x, z, reach, count);
                count = GameUi.PanelPlaces.Add(around, within, stop.Wait, Reach, x, z, reach, count);
                if (stop.Shelter)
                {
                    count = GameUi.PanelPlaces.Add(around, within, stop.ShelterAt, Reach, x, z, reach, count);
                }
            }
            return count;
        }

        private void RunPlayer(float now, Vector3 at, bool onFoot)
        {
            if (_onChoose == null)
            {
                Wire();
            }
            Pointers();
            if (_rider.Active)
            {
                HidePanel();   // on the way to the bus's door, on the bus, or getting off it
                return;
            }
            if (_ride.Riding || (_picker != null && _picker.IsOpen))
            {
                return;   // the city map is up with the stops on it, and the game's clock stands still under it
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
            // A subway entrance's panel near the stop is not a reason to stop serving it: which of the two has the game's box is
            // the panels' own rule (Core/GameUi/PanelPickRules), the nearer place the player faces.
            bool free = onFoot && !JobDirector.TakesInteractKey && (listOpen || !ThirdPersonCamera.IsUiOpen());

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
                // Asked only at a seat: whether the clips are there, and whether anything is in the way.
                if (_sitAt != null && (!TransitSeat.Possible || !TransitRide.CanStart()))
                {
                    _sitAt = null;
                    _sitSeat = -1;
                }
            }

            bool standing = _served != null && _bus.State == TransitBus.Phase.Standing && _bus.Vehicle != null;
            if (_served == null || !free || !(AtStop(_served, at) || (standing && AtBus(at))))
            {
                if (listOpen)
                {
                    CloseList("the player left the stop");
                }
                if (_hasPicked && (_served == null || !(AtStop(_served, at) || (standing && AtBus(at)))))
                {
                    // Only on leaving the stop: a screen still closing keeps the pick for the next tick.
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
                // The list is up: the panel behind it is down, and the list's headline follows the bus.
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
                // The stop picked on the map, now that the game runs again.
                TransitTrip picked = _picked;
                _hasPicked = false;
                _picked = default(TransitTrip);
                Chosen(picked);
                if (_ride.Riding)
                {
                    return;
                }
                standing = _bus.State == TransitBus.Phase.Standing && _bus.Vehicle != null;
            }
            if (_booked != null && standing && !_seat.Active && _bus.Standing >= 1f && TransitRide.CanStart()
                && (_bookedLine < 0 || (_bus.Stop != null && _bus.Stop.Line == _bookedLine)))
            {
                // The stop picked while waiting (or a journey's change): the bus of its line stands, so they get on.
                TransitTrip trip = _bookedTrip;
                _booked = null;
                _bookedLine = -1;
                _panelKey = int.MinValue;
                if (Board(trip, (_changing ? "changes onto the " + LineNumber(_bus.Stop) + " for " : "had picked ") + StopLabel(trip.To) + (_changing ? "" : " while waiting")
                                + " and gets on as the bus stands"))
                {
                    return;
                }
            }
            if (Measure(standing))
            {
                WritePanel(standing);
            }
            // Over the pole, under its flag; over the player's head while the pole is behind the camera (on the bench, say).
            Vector3 over = _served.Sign + Vector3.up * PanelUp;
            Camera camera = GameManager.GetMainCamera();
            if (camera != null && Vector3.Dot(over - camera.transform.position, camera.transform.forward) < 0.5f)
            {
                over = at + Vector3.up * 2.1f;
            }
            _panel.For(_served.Sign);
            _panel.Position = over;
            int state = _panel.Show() ? 1 : _panel.Waiting ? 3 : 2;
            if (state != _panelState)
            {
                _panelState = state;
                Logger?.Info("Transit check (stop panel): at " + StopLabel(_served) + ", " + Mathf.Sqrt(CitySpots.Flat(_served.Sign - at)).ToString("F1", CultureInfo.InvariantCulture)
                             + " m from the pole (offered within " + Reach.ToString("F0") + " m of the pole, the waiting place, the shelter or the standing bus): "
                             + (state == 1 ? "the game's own panel is up, bus: " + PanelWords() + (_booked != null ? ", the player's stop is " + StopLabel(_booked) : "") + ": PASS"
                                 : state == 3 ? "the game's building box leaves the panel no room on any side, so it waits and is tried again while the player stands here: PASS"
                                 : "FAIL (the game's panel is showing another place, a filling station or a lift; it is tried again while the player stands here)") + ".");
            }
        }

        // What the panel and the list say of the buses, measured; true when any of it differs from what was last written.
        private bool Measure(bool standing)
        {
            TransitEta.Mode mode = standing ? TransitEta.Mode.Here : _eta.State;
            bool busOut = _bus.State == TransitBus.Phase.Approaching;
            if (mode == TransitEta.Mode.Here && !standing)
            {
                mode = TransitEta.Mode.Timetable;   // the tick between a bus leaving and the next call being set
            }
            // In game minutes: the clock's measured rate wobbles, and the figure shown does not rise for that.
            int minutes = Mathf.Max(1, Mathf.CeilToInt(_eta.Seconds * _minutesPerSecond));
            if (_eta.Epoch == _shownEpoch && minutes > _shownMinutes)
            {
                minutes = _shownMinutes;
            }
            _shownEpoch = _eta.Epoch;
            _shownMinutes = minutes;
            int metres = busOut && (mode == TransitEta.Mode.Delayed || mode == TransitEta.Mode.EnRoute) ? Mathf.Clamp(Mathf.RoundToInt(_bus.Remaining / 10f) * 10, 10, 4090) : 0;
            int hold = busOut ? (int)_bus.Waiting : 0;
            int state = standing ? (_busFull ? 5 : 4) : (int)mode;
            int seat = _seat.Active ? (_seat.Seated ? 2 : 1) : 0;
            _mode = mode;
            _minutes = minutes;
            _metres = metres;
            int rows = Rows(standing);
            int key = (_served.Id << 20) | (state << 16) | (hold << 4) | (seat << 2) | (Raining() ? 2 : 0) | (TransitState.HasPass ? 1 : 0);
            int more = (metres | ((_booked != null ? _booked.Id + 1 : 0) << 12) | (_sitAt != null ? 1 << 21 : 0) | (_changing ? 1 << 22 : 0)) ^ (rows * 16777619);
            if (key == _panelKey && more == _panelMore)
            {
                return false;
            }
            _panelKey = key;
            _panelMore = more;
            return true;
        }

        private const int MostRows = 4;   // lines shown on the panel, the soonest first
        private readonly int[] _rowCall = new int[MostCalls];
        private readonly int[] _rowMinutes = new int[MostCalls];
        private readonly TransitEta.Mode[] _rowMode = new TransitEta.Mode[MostCalls];
        private int _rowCount;

        // Each line that calls at the place served with when it comes, soonest first. Returns a figure of it all, to compare.
        private int Rows(bool standing)
        {
            _rowCount = 0;
            int hash = 17;
            double now = NowMinutes();
            for (int k = 0; k < _calls; k++)
            {
                TransitEta.Mode mode;
                int minutes;
                if (k == _frontCall && (standing || _eta.State != TransitEta.Mode.None || _bus.State != TransitBus.Phase.Idle))
                {
                    mode = standing && _bus.Stop == _served.Calls[k] ? TransitEta.Mode.Here : _mode;
                    minutes = _minutes;
                }
                else if (k == _nextCall && _nextEta.State != TransitEta.Mode.None)
                {
                    mode = _nextEta.State;
                    minutes = Mathf.Max(1, Mathf.CeilToInt(_nextEta.Seconds * _minutesPerSecond));
                    if (_next.State == TransitBus.Phase.Standing)
                    {
                        mode = TransitEta.Mode.EnRoute;   // behind the front one: it pulls up when that one has gone
                        minutes = 1;
                    }
                }
                else if (_callAt[k] >= double.MaxValue)
                {
                    mode = TransitEta.Mode.None;
                    minutes = 0;
                }
                else
                {
                    mode = TransitEta.Mode.Timetable;
                    minutes = Mathf.Max(1, Mathf.CeilToInt((float)(_callAt[k] - now)));
                }
                _rowCall[_rowCount] = k;
                _rowMode[_rowCount] = mode;
                _rowMinutes[_rowCount] = minutes;
                _rowCount++;
                hash = hash * 31 + ((int)mode * 1000 + Mathf.Min(minutes, 999)) + k * 7;
            }
            // The bus at the stop first, a line not running now last.
            for (int i = 1; i < _rowCount; i++)
            {
                for (int j = i; j > 0 && Sooner(j, j - 1); j--)
                {
                    int c = _rowCall[j]; _rowCall[j] = _rowCall[j - 1]; _rowCall[j - 1] = c;
                    int m = _rowMinutes[j]; _rowMinutes[j] = _rowMinutes[j - 1]; _rowMinutes[j - 1] = m;
                    TransitEta.Mode d = _rowMode[j]; _rowMode[j] = _rowMode[j - 1]; _rowMode[j - 1] = d;
                }
            }
            return hash;
        }

        private bool Sooner(int a, int b)
        {
            return RowRank(a) < RowRank(b);
        }

        private int RowRank(int row)
        {
            TransitEta.Mode mode = _rowMode[row];
            return mode == TransitEta.Mode.Here ? -1 : mode == TransitEta.Mode.None ? 1000000
                : mode == TransitEta.Mode.Delayed && !_eta.Known && _rowCall[row] == _frontCall ? 500000 : _rowMinutes[row];
        }

        private string RowText(int row)
        {
            TransitStop call = _served.Calls[_rowCall[row]];
            TransitLine line = TransitMap.Lines[call.Line];
            string status = Status(_rowMode[row], _rowMinutes[row], _rowCall[row] == _frontCall ? _eta.Known : _nextEta.Known);
            return TransitMap.Onward(call) == 0 ? TransitText.T("reallife_transit_panel_row_end", "route", line.Number, "status", status)
                : TransitText.T("reallife_transit_panel_row", "route", line.Number, "to", line.To, "status", status);
        }

        private static string Status(TransitEta.Mode mode, int minutes, bool known)
        {
            switch (mode)
            {
                case TransitEta.Mode.Here: return TransitText.T("reallife_transit_status_here");
                case TransitEta.Mode.None: return TransitText.T("reallife_transit_status_none");
                case TransitEta.Mode.Delayed:
                    return known ? TransitText.T("reallife_transit_status_delayed_minutes", "minutes", minutes.ToString()) : TransitText.T("reallife_transit_status_delayed");
                default: return TransitText.T("reallife_transit_status_minutes", "minutes", minutes.ToString());
            }
        }

        // The panel's lines and buttons, written when Measure says they have changed.
        private void WritePanel(bool standing)
        {
            _text.Length = 0;
            if (_booked != null)
            {
                _text.Append(_bookedLine >= 0 ? TransitText.T("reallife_transit_panel_booked_line", "name", StopName(_booked), "route", TransitMap.Lines[_bookedLine].Number)
                    : TransitText.T("reallife_transit_panel_booked", "name", StopName(_booked)));
            }
            for (int r = 0; r < _rowCount && r < MostRows; r++)
            {
                _text.Append(_text.Length > 0 ? "\n" : string.Empty).Append(RowText(r));
            }
            if (_rowCount == 0)
            {
                TransitLine only = TransitMap.Lines[_served.Line];
                _text.Append(TransitMap.Onward(_served) == 0 ? TransitText.T("reallife_transit_panel_line_end", "route", only.Number)
                    : TransitText.T("reallife_transit_stop_line", "route", only.Number, "to", only.To));
            }
            string left = _text.ToString();
            string right;
            Color32 colour;
            if (standing)
            {
                right = TransitText.T(_busFull ? "reallife_transit_status_full" : "reallife_transit_status_here");
                colour = _busFull ? GameUi.WorldPanel.Red() : GameUi.WorldPanel.Green();
            }
            else if (_rowCount > 0 && _rowMode[0] == TransitEta.Mode.None)
            {
                right = TransitText.T("reallife_transit_status_none");
                colour = GameUi.WorldPanel.Yellow();
            }
            else if (_mode == TransitEta.Mode.Delayed && (_rowCount == 0 || _rowCall[0] == _frontCall))
            {
                right = Status(TransitEta.Mode.Delayed, _minutes, _eta.Known);
                colour = GameUi.WorldPanel.Yellow();
            }
            else
            {
                right = TransitText.T("reallife_transit_status_minutes", "minutes", (_rowCount > 0 ? _rowMinutes[0] : _minutes).ToString());
                colour = GameUi.WorldPanel.White();
            }
            _panel.Lines(TransitText.T("reallife_transit_stop_title", "name", StopName(_served)), left, right, colour);
            if (_seat.Active)
            {
                _panel.Buttons(_seat.Seated ? ButtonStand : null, _onStand, true, null, null, false);
            }
            else
            {
                bool boardable = standing && (_bookedLine < 0 || (_bus.Stop != null && _bus.Stop.Line == _bookedLine));
                string first = boardable && _booked == null ? ButtonBoard : _booked != null ? ButtonChange : ButtonChoose;
                _panel.Buttons(first, _onChoose, true, _sitAt != null && !standing ? ButtonSit : null, _onSit, true);
            }
            // On a journey's change the rest of it can be skipped, as the ride panel's skip does.
            _panel.Button(2, _changing ? ButtonSkipChange : null, _onSkipChange ?? (_onSkipChange = SkipChangeClicked), _changing, false);
            if (_shownAt < 0f && _eta.Figure && _frontCall >= 0)
            {
                _shownAt = Time.time + _eta.Seconds;   // the first figure the player saw for this call, held against the arrival (OnTime)
                _shownFigure = _eta.Seconds;
            }
        }

        // The two lines under the list's headline: the buses, and the fare.
        private string ListInfo(bool standing)
        {
            TransitLine line = TransitMap.Lines[(_bus.Stop ?? _served).Line];
            string bus;
            if (standing)
            {
                bus = TransitText.T(_busFull ? "reallife_transit_list_info_full" : "reallife_transit_list_info_here", "route", line.Number);
            }
            else
            {
                _text.Length = 0;
                for (int r = 0; r < _rowCount && r < MostRows; r++)
                {
                    _text.Append(r > 0 ? ", " : string.Empty).Append(RowText(r));
                }
                bus = _rowCount > 0 ? TransitText.T("reallife_transit_list_info_rows", "rows", _text.ToString()) : TransitText.T("reallife_transit_list_info_delayed");
            }
            _text.Length = 0;
            _text.Append(bus).Append('\n');
            if (Raining() && !standing)
            {
                _text.Append(TransitText.T("reallife_transit_stop_rain")).Append(". ");
            }
            _text.Append(TransitState.HasPass ? TransitText.T("reallife_transit_stop_pass")
                : TransitState.HasTransfer ? TransitText.T("reallife_transit_list_info_transfer")
                : TransitText.T("reallife_transit_list_info_fare", "fare", TransitText.Money(TransitState.BusFare)));
            return _text.ToString();
        }

        // Within reach of a stop's pole, its waiting place or its shelter.
        private static bool AtStop(TransitStop stop, Vector3 at)
        {
            const float within = Reach * Reach;
            return Mathf.Abs(stop.Sign.y - at.y) < 3f
                   && (CitySpots.Flat(stop.Sign - at) < within || CitySpots.Flat(stop.Wait - at) < within || (stop.Shelter && CitySpots.Flat(stop.ShelterAt - at) < within));
        }

        // Within reach of the standing bus's body.
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

        private static string StopName(TransitStop stop)
        {
            return stop.Name ?? TransitText.T("reallife_transit_stop_number", "number", (stop.Order + 1).ToString());
        }

        // A stop for the log: its line and its name, in plain words whatever the game's language.
        internal static string StopLabel(TransitStop stop)
        {
            if (stop == null)
            {
                return "no stop";
            }
            TransitLine line = stop.Line >= 0 && stop.Line < TransitMap.Lines.Count ? TransitMap.Lines[stop.Line] : null;
            string lines = stop.IsHost && stop.Calls.Count > 1 ? TransitMap.Numbers(stop, "/") : line != null ? line.Number : null;
            return "the " + (lines != null ? lines + " " : "") + "stop at " + (stop.Name ?? "number " + (stop.Order + 1));
        }

        private void HidePanel()
        {
            if (_panelState != 0)
            {
                _panelState = 0;
                _panel.Hide();
            }
        }

        // Everything the player is shown for the buses goes, and the stop picked is forgotten.
        private void HideHud()
        {
            CloseList("the service at this stop ended, or a ride began");
            HidePanel();
            BusPointer(false);
            _booked = null;
            _bookedLine = -1;
            _hasPicked = false;
        }

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
