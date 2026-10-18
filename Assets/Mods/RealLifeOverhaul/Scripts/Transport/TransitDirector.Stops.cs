using System;
using System.Collections.Generic;
using System.Globalization;
using UI;
using UI.MiniMenu;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The bus director's part for picking where to: the city map with the stops on it, or the list, and the stop chosen.</summary>
    public sealed partial class TransitDirector
    {
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
            Measure(standing);            // the figure the list is about to show
            _panelKey = int.MinValue;     // and the panel is written again when it is next up
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
                         + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1", CultureInfo.InvariantCulture) + " ms: "
                         + (_trips.Count > 0 ? "PASS" : "FAIL (the network has no other stop)") + ".");
        }

        // The city map with the stops on it, as the game's subway entrance opens it for its stations. False if the game's window
        // could not be copied (said once) or the game would not open its map now: the list is used then.
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
            Measure(standing);            // the figure the window is about to show
            _panelKey = int.MinValue;     // and the panel is written again when it is next up
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
            Logger?.Info("Transit check (stop map): the city map is open at " + StopLabel(_served) + " with the game's window for its stations beside it: " + _trips.Count
                         + " stops, " + direct + " further along this line and " + (_trips.Count - direct) + " with a change of bus, each a marker on the map; the day pass "
                         + (TransitState.HasPass ? "is held" : "is its first entry") + "; " + _picker.Made + " entries and " + _picker.Marked + " markers made so far; bus: "
                         + PanelWords() + "; " + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1", CultureInfo.InvariantCulture) + " ms: "
                         + (_trips.Count > 0 ? "PASS" : "FAIL (the network has no other stop)") + ".");
            return true;
        }

        // Every stop offered from the served one, written into the map's window and onto the map. Returns how many are on this line.
        private int FillMap(bool standing)
        {
            int direct = TransitTrips.From(_served, _trips, NowMinutes(), Calls);
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
                _picker.Add(i, StopName(trip.To), TripWords(trip, fare), "common_travel", true, true, trip.To.Sign);
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
                    Logger?.Info("Transit check (choice): the day pass, from the map at " + StopLabel(_served) + ": "
                                 + (bought ? "bought for " + TransitState.DayPass.ToString("F2", CultureInfo.InvariantCulture) + ": PASS" : "not bought (no money, or one is held): FAIL") + ".");
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
            Logger?.Info("Transit check (stop map): closed (" + (_listClosing ?? "cancelled by the player") + ")"
                         + (_hasPicked ? "; the stop picked is taken up when the game runs again" : string.Empty) + ".");
            _listClosing = null;
        }

        // Every stop offered from the served one, written into the list. Returns how many are on this line.
        private int FillList(bool standing)
        {
            int direct = TransitTrips.From(_served, _trips, NowMinutes(), Calls);
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
                _list.Add(i, StopName(_trips[i].To), TripWords(_trips[i], fare), "common_travel", true);
            }
            return direct;
        }

        // An entry's words: the bus to board and where it goes, the change where there is one, when it comes, the ride and the fare.
        private static string TripWords(TransitTrip trip, string fare)
        {
            string minutes = Mathf.CeilToInt(trip.Minutes).ToString();
            if (!trip.Planned)
            {
                TransitLine any = TransitMap.Lines[trip.To.Line];
                return TransitText.T("reallife_transit_list_change", "route", any.Number, "to", any.To, "minutes", minutes, "fare", fare);
            }
            TransitLine line = TransitMap.Lines[trip.Board.Line];
            string wait = Mathf.Max(1, Mathf.CeilToInt(trip.Wait)).ToString();
            if (!trip.Changes)
            {
                return TransitText.T("reallife_transit_list_direct_next", "route", line.Number, "to", line.To, "wait", wait, "minutes", minutes, "fare", fare);
            }
            return TransitText.T("reallife_transit_list_change_at", "route", line.Number, "change", StopName(trip.ChangeOff), "route2", TransitMap.Lines[trip.ChangeOn.Line].Number,
                "wait", wait, "minutes", minutes, "fare", fare);
        }

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
                    Logger?.Info("Transit check (choice): the day pass, from the list at " + StopLabel(_served) + ": "
                                 + (bought ? "bought for " + TransitState.DayPass.ToString("F2", CultureInfo.InvariantCulture) + ": PASS" : "not bought (no money, or one is held): FAIL") + ".");
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

        // A stop has been picked: aboard now if a bus of its line stands, else kept for when one does.
        private void Chosen(TransitTrip trip)
        {
            _panelKey = int.MinValue;
            if (_served == null || trip.To == null)
            {
                return;
            }
            if (_changing)
            {
                DropPlan();   // a new choice at the change: the journey is this one now
            }
            int boardLine = trip.Planned ? trip.Board.Line : -1;
            string route = boardLine >= 0 ? TransitMap.Lines[boardLine].Number : LineNumber(_bus.Stop ?? _served);
            string what = "the player picks " + StopLabel(trip.To) + " at " + StopLabel(_served) + " ("
                          + (trip.Direct ? "one bus, the " + route
                              : trip.Changes ? "the " + route + ", changing at " + StopLabel(trip.ChangeOff)
                                               + (trip.ChangeOn.At != trip.ChangeOff.At ? " and walking to " + StopLabel(trip.ChangeOn) : string.Empty) + " for the "
                                               + LineNumber(trip.ChangeOn)
                              : "with a change of bus that is not planned: the ride passes under the fade")
                          + ", " + trip.Metres.ToString("F0") + " m, " + trip.Minutes.ToString("F0") + " game minutes, the first bus in " + trip.Wait.ToString("F0") + ")";
            bool fits = boardLine < 0 || (_bus.Stop != null && _bus.Stop.Line == boardLine);
            if (_bus.State == TransitBus.Phase.Standing && fits && TransitRide.CanStart())
            {
                Logger?.Info("Transit check (choice): " + what + " while its bus stands: they board now: PASS.");
                Board(trip, "picks " + StopLabel(trip.To) + " from the list");
                return;
            }
            _booked = trip.To;
            _bookedLine = boardLine;
            _bookedTrip = trip;
            Notifications.Show(NotificationType.Info, "reallife_transit_booked", new Dictionary<string, string>(2) { { "route", route }, { "name", StopName(trip.To) } }, 7f);
            Logger?.Info("Transit check (choice): " + what + " with no bus of its line to board yet (bus: " + PanelWords() + "): the stop is kept and they board when the " + route
                         + " stands and they are at the stop: PASS.");
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

        // The game's list could not be copied (one "Game UI:" warning says why): the game's own question is asked instead, for
        // the one ride that fits most: to the end of this line, or from a line's last stop to the nearest other stop.
        private void AskOne()
        {
            int direct = TransitTrips.From(_served, _trips, NowMinutes(), Calls);
            if (_trips.Count == 0)
            {
                return;
            }
            _asked = _trips[direct > 0 ? direct - 1 : 0];
            Logger?.Info("Transit check (stop list): the game's list could not be made, so the game's question is asked for the one ride to " + StopLabel(_asked.To) + ": FAIL.");
            GameConfirm.Show(TransitText.T("reallife_transit_list_title", "name", StopName(_served)),
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
    }
}
