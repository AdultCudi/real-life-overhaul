using System;
using System.Collections.Generic;
using Buildings.Office.Headquarters;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using Streets;
using UnityEngine;
using TripsDirector = RealLifeOverhaul.Economy.LogisticsDirector;
using RouteTimes = RealLifeOverhaul.Economy.RouteTimes;
using TripRun = RealLifeOverhaul.Economy.Trip;
using TripLog = RealLifeOverhaul.Economy.TripLog;
using TripRules = RealLifeOverhaul.Economy.TripRules;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// BizMan's logistics and warehouse pages while the routes run as delivery trips (Economy/LogisticsDirector): a route's
    /// departures to set, its vehicles with their drivers, where each is and its load, the trip on the road stop by stop,
    /// the week's goods moved, fuel and wages, its last run, and Send now as trips; a warehouse's slots on route or parked.
    /// Everything is read from the director's state when the page is laid; nothing is searched. Off, the pages are as before.
    /// </summary>
    public sealed partial class BizManScreen
    {
        private const int ModFirst = 11, ModRuns = 12, ModGap = 13;
        private const int StopsNamedMost = 3;   // stops of a trip on the road named in its line

        private readonly List<TripRun> _routeTrips = new List<TripRun>(4);
        private readonly List<TripLog> _routeRuns = new List<TripLog>(8);

        private static bool TripsShown => TripsDirector.Instance != null && TripsDirector.Instance.TripsOn;

        private static bool SpoilShown => TripsDirector.Instance != null && TripsDirector.Instance.SpoilOn;

        // The route as trips, in place of the game's one nightly run: its departures (the right column), its figures and vehicles.
        private void TripLines(LogisticsManagerPlan route)
        {
            TripsDirector trips = TripsDirector.Instance;
            if (route == null || trips == null || !trips.TripsOn)
            {
                return;
            }
            Warehouse from = route.targetAddress != null && !route.targetAddress.IsUndefined() ? BuildingHelper.GetBuildingRegistration(route.targetAddress) as Warehouse : null;
            bool mine = from != null && GoingPublic.Mine(from);
            RouteTimes r = trips.Times(route);
            _right = true;
            Heading(UiText.T("reallife_trips_departures"));
            var hours = new string[24];
            for (int h = 0; h < 24; h++)
            {
                hours[h] = h.GetFormattedTime(0f);
            }
            ModChoice(ModFirst, UiText.T("reallife_trips_first"), hours, r.First, !mine);
            var runs = new string[TripRules.RunChoices.Length];
            for (int i = 0; i < runs.Length; i++)
            {
                runs[i] = UiText.Whole(TripRules.RunChoices[i]);
            }
            ModChoice(ModRuns, UiText.T("reallife_trips_runs"), runs, Math.Max(0, Array.IndexOf(TripRules.RunChoices, r.Runs)), !mine);
            var gaps = new string[TripRules.GapChoices.Length];
            for (int i = 0; i < gaps.Length; i++)
            {
                gaps[i] = UiText.Fill("reallife_trips_gap_value", "count", UiText.Whole(TripRules.GapChoices[i]));
            }
            ModChoice(ModGap, UiText.T("reallife_trips_gap"), gaps, Math.Max(0, Array.IndexOf(TripRules.GapChoices, r.Gap)), !mine || r.Runs <= 1);
            int next = trips.NextRun(route);
            InfoLine(UiText.T("reallife_trips_next"), next >= 0 ? When(next) : null);
            trips.RouteWeek(route.id, out int week, out int pieces, out int fuel, out int wages);
            trips.RouteRuns(route.id, _routeRuns);
            InfoLine(UiText.T("reallife_trips_round"), _routeRuns.Count > 0 ? UiText.Fill("reallife_trips_minutes", "minutes", UiText.Whole(_routeRuns[0].Minutes)) : null);
            InfoLine(UiText.T("reallife_trips_moved"), week > 0 ? UiText.Whole(pieces) : null);
            InfoLine(UiText.T("reallife_trips_costs"), week > 0 ? UiText.Fill("reallife_trips_costs_value", "fuel", (fuel / 100f).ToCurrencyFormat(), "wages", (wages / 100f).ToCurrencyFormat()) : null);
            _right = false;
            Vehicles(trips, from, route);
            NoteLine(_routeRuns.Count > 0 ? LastRun(_routeRuns[0]) : next >= 0 ? UiText.Fill("reallife_trips_none_yet", "time", When(next)) : null);
            NoteLine(UiText.T("reallife_trips_note"));
        }

        // The warehouse's vehicles that drive the route: each with its driver, where it is, and its load on the road.
        private void Vehicles(TripsDirector trips, Warehouse from, LogisticsManagerPlan route)
        {
            List<VehicleSlot> slots = from != null ? from.vehicleSlots : null;
            if (slots == null || slots.Count == 0)
            {
                return;
            }
            Heading(UiText.T("reallife_trips_vehicles"));
            for (int s = 0; s < slots.Count; s++)
            {
                VehicleInstance v = slots[s] != null && !string.IsNullOrEmpty(slots[s].vehicleInstanceId) ? Vehicle(slots[s].vehicleInstanceId) : null;
                if (v == null)
                {
                    continue;
                }
                EmployeeInstance d = !string.IsNullOrEmpty(slots[s].employeeDriverId) ? EmployeeHelper.GetEmployeeById(slots[s].employeeDriverId, false) : null;
                string name = v.vehicleTypeName.GetLocalization() + (d != null ? ", " + d.characterData.name : string.Empty);
                TripRun t = trips.OnRoad(from, s);
                InfoLine(name, Where(t, d), t != null && t.Out ? UiRole.OkText : UiRole.None);
                if (t != null && t.Out && t.Room > 0)
                {
                    _rows.Add(new BizOpsRow
                    {
                        Kind = BizOpsRowKind.Meter, Grade = Mathf.Clamp(t.Boxes * 100 / t.Room, 0, 100), Right = _right,
                        Label = UiText.Fill("reallife_trips_load", "count", UiText.Whole(t.Boxes), "most", UiText.Whole(t.Room))
                    });
                    NoteLine(Road(t));
                }
            }
        }

        // Where a slot's vehicle is: on the road and back at a time, waiting to leave, or parked (with no driver, says so).
        private static string Where(TripRun t, EmployeeInstance driver)
        {
            if (t != null && t.Out)
            {
                return UiText.Fill("reallife_trips_on_route", "time", When(t.Back));
            }
            if (t != null)
            {
                return UiText.Fill("reallife_trips_waiting", "time", When(t.Depart));
            }
            return UiText.T(driver == null ? "reallife_trips_no_driver" : "reallife_trips_parked");
        }

        // A trip on the road, stop by stop: each stop's place and the time it arrives, and the time it is back.
        private static string Road(TripRun t)
        {
            var words = new System.Text.StringBuilder(96);
            for (int k = 0; k < t.Stops.Count && k < StopsNamedMost; k++)
            {
                BuildingRegistration stop = Economy.LogisticsDirector.Instance != null ? PlaceOf(t.Stops[k].Place) : null;
                words.Append(k > 0 ? ", " : string.Empty).Append(UiText.Fill(t.Stops[k].Done ? "reallife_trips_stop_done" : "reallife_trips_stop_at",
                    "name", stop != null ? stop.BusinessName : UiText.T("common_unassigned"), "time", When(t.Stops[k].Arrive)));
            }
            return UiText.Fill("reallife_trips_road", "stops", words.ToString(), "time", When(t.Back));
        }

        private static BuildingRegistration PlaceOf(string key)
        {
            int at = key != null ? key.LastIndexOf('~') : -1;
            if (at <= 0 || !int.TryParse(key.Substring(at + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int n))
            {
                return null;
            }
            return BuildingHelper.GetBuildingRegistration(new Address(Uri.UnescapeDataString(key.Substring(0, at)), n));
        }

        private static string LastRun(TripLog e) =>
            UiText.Fill("reallife_trips_last", "time", (e.Minute / 60).GetFormattedTime(e.Minute % 60), "day", UiText.Whole(e.Day), "stops", UiText.Whole(e.Stops),
                "count", UiText.Whole(e.Boxes), "most", UiText.Whole(e.Room), "minutes", UiText.Whole(e.Minutes), "fuel", (e.FuelCents / 100f).ToCurrencyFormat());

        // An absolute game minute as the game writes a time, with the day when it is not today.
        private static string When(int minute)
        {
            int day = minute >= 0 ? minute / TripRules.DayMinutes : 0, inDay = minute - day * TripRules.DayMinutes;
            string at = (inDay / 60).GetFormattedTime(inDay % 60);
            int today = TimeHelper.CurrentDay;
            return day == today ? at : day == today + 1 ? UiText.Fill("reallife_trips_tomorrow", "time", at)
                : UiText.Fill("reallife_trips_on_day", "day", UiText.Whole(day), "time", at);
        }

        // A press of one of the departures' rows: the route's times changed, and kept by the trips.
        private bool TripPress(int code, int what)
        {
            LogisticsManagerPlan route = ChosenRoute();
            TripsDirector trips = TripsDirector.Instance;
            Warehouse from = route != null && route.targetAddress != null && !route.targetAddress.IsUndefined()
                ? BuildingHelper.GetBuildingRegistration(route.targetAddress) as Warehouse : null;
            if (route == null || trips == null || !trips.TripsOn || from == null || !GoingPublic.Mine(from) || what < ModOption)
            {
                return false;
            }
            RouteTimes r = trips.Times(route);
            int pick = what - ModOption;
            switch (code)
            {
                case ModFirst: trips.SetTimes(route, Mathf.Clamp(pick, 0, 23), r.Runs, r.Gap); break;
                case ModRuns: trips.SetTimes(route, r.First, TripRules.RunChoices[Mathf.Clamp(pick, 0, TripRules.RunChoices.Length - 1)], r.Gap); break;
                case ModGap: trips.SetTimes(route, r.First, r.Runs, TripRules.GapChoices[Mathf.Clamp(pick, 0, TripRules.GapChoices.Length - 1)]); break;
                default: return false;
            }
            UiDirector.Logger?.Info("BizMan (kit): a route's departures changed from its page");
            return true;
        }

        // Send now as trips: the game's question box first, then every free vehicle of the route leaves at once.
        private bool SendTrips(LogisticsManagerPlan route)
        {
            string id = route.id;
            GameConfirm.Show("reallife_ui_bizops_send_title", "reallife_trips_send_body", () =>
            {
                LogisticsManagerPlan now = Economy.LogisticsDirector.Instance != null ? FindRoute(id) : null;
                int sent = now != null ? Economy.LogisticsDirector.Instance.SendNow(now) : 0;
                _sentOn = now;
                _sent = sent > 0 ? UiText.Fill("reallife_trips_sent", "count", UiText.Whole(sent)) : UiText.T("reallife_trips_sent_none");
            }, null, "reallife_ui_bizops_send_now", null, true, false);
            return true;
        }

        private static LogisticsManagerPlan FindRoute(string id)
        {
            List<LogisticsManagerPlan> plans = SaveGameManager.Current != null ? SaveGameManager.Current.logisticsManagerPlans : null;
            for (int i = 0; plans != null && i < plans.Count; i++)
            {
                if (plans[i] != null && plans[i].id == id)
                {
                    return plans[i];
                }
            }
            return null;
        }

        // What the trips' pages show changed: a trip left, reached a stop or came back, departures were set, goods spoiled.
        private static long TripsSignature() => TripsDirector.Instance != null && (TripsDirector.Instance.TripsOn || TripsDirector.Instance.SpoilOn)
            ? TripsDirector.Instance.Changes * 31L + TimeHelper.CurrentDay : 0L;

        // ------------------------------------------------------------------------------------------------- the warehouse and the stock

        // A warehouse slot's trip, for the drivers' page: on route and back at a time, or waiting to leave; null when parked.
        private static string SlotTrip(Warehouse warehouse, int slot)
        {
            TripRun t = TripsShown ? TripsDirector.Instance.OnRoad(warehouse, slot) : null;
            return t == null ? null : t.Out ? UiText.Fill("reallife_trips_on_route", "time", When(t.Back)) : UiText.Fill("reallife_trips_waiting", "time", When(t.Depart));
        }

        // When the oldest of a product at a place spoils, for the stock table: tonight, in so many days, or "-" for never.
        private static string SpoilsWords(BuildingRegistration place, string item, out UiRole tone)
        {
            tone = UiRole.None;
            int days = TripsDirector.Instance != null && item != null ? TripsDirector.Instance.SpoilsIn(place, item) : -1;
            if (days < 0)
            {
                return "-";
            }
            tone = days <= 1 ? UiRole.DangerText : days <= 2 ? UiRole.WarnText : UiRole.None;
            return days <= 1 ? UiText.T("reallife_spoil_tonight") : UiText.Fill("reallife_spoil_days", "days", UiText.Whole(days));
        }

        private const int SpoilColumn = 6;   // the stock table's Spoils in

        // A column taken out of a table laid with it: its head, and each row's cell, figure and tone.
        private static void DropColumn(UiDataTableModel t, int column)
        {
            t.Columns = Without(t.Columns, column);
            for (int i = 0; t.Rows != null && i < t.Rows.Length; i++)
            {
                t.Rows[i].Cells = Without(t.Rows[i].Cells, column);
                t.Rows[i].Values = Without(t.Rows[i].Values, column);
                t.Rows[i].Tones = Without(t.Rows[i].Tones, column);
            }
        }

        private static T[] Without<T>(T[] a, int at)
        {
            if (a == null || at < 0 || at >= a.Length)
            {
                return a;
            }
            var b = new T[a.Length - 1];
            Array.Copy(a, 0, b, 0, at);
            Array.Copy(a, at + 1, b, at, a.Length - at - 1);
            return b;
        }

        // What spoiled at a place today, for the stock page's status line; null when nothing did or spoilage is off.
        private static string SpoiledToday(BuildingRegistration place)
        {
            int pieces = 0, cents = 0;
            if (TripsDirector.Instance == null || !TripsDirector.Instance.Spoiled(place, true, out pieces, out cents, out int _) || pieces <= 0)
            {
                return null;
            }
            return UiText.Fill("reallife_spoil_today", "count", UiText.Whole(pieces), "amount", (cents / 100f).ToCurrencyFormat());
        }

        // A factory's share of what came in that spoiled over the week, for its machines' page; null when nothing came in.
        private void SpoiledShare(BuildingRegistration place)
        {
            int pieces = 0, percent = -1;
            if (TripsDirector.Instance == null || !TripsDirector.Instance.Spoiled(place, false, out pieces, out int _, out percent) || percent < 0)
            {
                return;
            }
            InfoLine(UiText.T("reallife_spoil_factory"), UiText.Fill("reallife_spoil_share", "percent", UiText.Whole(percent), "count", UiText.Whole(pieces)),
                percent >= 5 ? UiRole.WarnText : UiRole.None);
        }
    }
}
