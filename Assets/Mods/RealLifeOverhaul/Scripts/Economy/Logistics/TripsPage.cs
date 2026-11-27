using System;
using System.Collections.Generic;
using Buildings.Office.Headquarters;
using Extensions;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UI.Smartphone.Apps.BizMan.LogisticsManagers;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The route's departures on the game's own Logistics Managers page, the old interface's: a Departures button beside Show warehouse (a copy of it,
    /// put between it and Add new destination) that opens the game's list window with the route's first run, runs a day and hours between runs, each
    /// chosen from a list of its own, and Send now. It stands only while the routes run as delivery trips (Living population and Delivery trips) and the
    /// route is the player's own to run so; the kit's route page (Ui/BizMan/Ops/BizManScreen.Trips.cs) has the same settings and both read and write
    /// the director's own (LogisticsDirector.Times, SetTimes, SendNowOnce). The button is made once BizMan's page is there and looked at once a second
    /// from the director; nothing runs a frame. A page the game does not build as expected is left as it is, with one line in Player.log.
    /// </summary>
    internal static class TripsPage
    {
        private const string ButtonName = "RealLifeDepartures";
        private const string Select = "common_select";   // the game's own word on a list's button
        private const float Gap = 16f;                   // the game's units between this button and the two beside it, where it stands between them
        private const float Least = 180f;                // and the width it must keep to read, or it stands under Show warehouse instead

        private const int PageMenu = 0, PageFirst = 1, PageRuns = 2, PageGap = 3;
        private const int RowFirst = 1, RowRuns = 2, RowGap = 3, RowSend = 4, RowBack = -1;

        private static readonly DeskList Window = new DeskList("RealLife Departures", 1000f, 124f);
        private static readonly object Owner = new object();
        private static readonly Action<int> OnPick = Picked;
        private static readonly Action OnClosed = Closed;

        private static LogisticsManagerPlanUI _ui;
        private static GameObject _button;
        private static bool _failed;
        private static string _route;     // the route whose list is open
        private static int _page;

        /// <summary>A city loaded: the button may be made again.</summary>
        public static void Begin()
        {
            _failed = false;
            _ui = null;
            _button = null;
            _route = null;
        }

        /// <summary>The city is unloading: the window and the button go with it.</summary>
        public static void End()
        {
            Window.Destroy(e => LogisticsDirector.Logger?.Error(e));
            _ui = null;
            _button = null;
            _route = null;
        }

        /// <summary>Once a second from the director: the button is made once BizMan's logistics page is there, and shows while a route it serves is chosen.</summary>
        public static void Keep(bool wanted)
        {
            if (_failed)
            {
                return;
            }
            try
            {
                if (_button == null)
                {
                    if (!wanted)
                    {
                        return;
                    }
                    Make();
                    if (_button == null)
                    {
                        return;
                    }
                }
                bool show = wanted && Chosen() != null;
                if (_button.activeSelf != show)
                {
                    _button.SetActive(show);
                }
                if (!show && Window.IsOpen && _route != null)
                {
                    Window.Close();
                }
            }
            catch (Exception e)
            {
                _failed = true;
                LogisticsDirector.Logger?.Error(e);
                Remove();
            }
        }

        // The route the game's page shows now, when it runs as trips.
        private static LogisticsManagerPlan Chosen()
        {
            var plan = _ui != null && GameContract.TripUiPlan.Field != null ? GameContract.TripUiPlan.Field.GetValue(_ui) as LogisticsManagerPlan : null;
            return plan != null && LogisticsGame.From(plan) != null ? plan : null;
        }

        private static void Make()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            BizManBusiness business = uis != null && uis.fullMenu != null && uis.fullMenu.bizMan != null ? uis.fullMenu.bizMan.business : null;
            LogisticsManagerPlanUI ui = business != null && business.logisticsLogisticsManagersPlanList != null ? business.logisticsLogisticsManagersPlanList.logisticsManagerPlanUI : null;
            if (ui == null)
            {
                return;   // BizMan is not built yet
            }
            var show = GameContract.TripUiShow.Field != null ? GameContract.TripUiShow.Field.GetValue(ui) as Button : null;
            var add = GameContract.TripUiAdd.Field != null ? GameContract.TripUiAdd.Field.GetValue(ui) as Button : null;
            var label = GameContract.TripUiShowLabel.Field != null ? GameContract.TripUiShowLabel.Field.GetValue(ui) as Component : null;
            if (show == null || add == null || label == null || GameContract.TripUiPlan.Field == null || !(show.transform is RectTransform) || show.transform.parent == null
                || !label.transform.IsChildOf(show.transform))
            {
                UiClone.Fallback("the game's Logistics Managers page is not built as expected, so the route's departures are not put on it");
                _failed = true;
                return;
            }
            GameObject copy = UnityEngine.Object.Instantiate(show.gameObject, show.transform.parent, false);
            copy.name = ButtonName;
            Transform words = Counterpart(show.transform, label.transform, copy.transform);
            UiClone.Key(words, "reallife_trips_departures");
            UiClone.Rewire(copy.transform, Clicked);
            copy.SetActive(false);
            _ui = ui;
            _button = copy;
            Place((RectTransform)copy.transform, (RectTransform)show.transform, add.transform as RectTransform);
            LogisticsDirector.Logger?.Info("Delivery trips: a Departures button is on the game's Logistics Managers page.");
        }

        // The same place in the copy as a part stands in the original.
        private static Transform Counterpart(Transform root, Transform part, Transform copy)
        {
            var path = new Stack<int>(4);
            for (Transform t = part; t != null && t != root; t = t.parent)
            {
                path.Push(t.GetSiblingIndex());
            }
            Transform at = copy;
            while (path.Count > 0 && at != null)
            {
                int i = path.Pop();
                at = i < at.childCount ? at.GetChild(i) : null;
            }
            return at != null ? at : copy;
        }

        // Where the game lays its buttons out itself, the copy is one more of them. Where it stands them at the card's two ends (Show warehouse at the left,
        // Add new destination at the right), the copy stands in the gap between them, as wide as that gap leaves, and when the gap is too narrow under Show warehouse.
        private static void Place(RectTransform copy, RectTransform show, RectTransform add)
        {
            if (copy.parent.GetComponent<LayoutGroup>() != null || add == null || add.parent != show.parent)
            {
                return;
            }
            float scale = Mathf.Max(0.0001f, show.lossyScale.x);
            float between = Mathf.Abs(add.position.x - show.position.x) / scale;   // centre to centre, in the page's own units
            float room = between - show.rect.width - Gap * 2f;
            if (room >= Least && copy.anchorMin == copy.anchorMax)
            {
                copy.position = (show.position + add.position) / 2f;
                copy.sizeDelta = new Vector2(Mathf.Min(show.rect.width, room), copy.sizeDelta.y);
                return;
            }
            copy.position = show.position + new Vector3(0f, -(show.rect.height + Gap) * scale, 0f);
        }

        private static void Remove()
        {
            if (_button != null)
            {
                UnityEngine.Object.Destroy(_button);
            }
            _button = null;
        }

        // ------------------------------------------------------------------------------------------------- the list

        private static void Clicked()
        {
            LogisticsManagerPlan plan = Chosen();
            if (plan == null || LogisticsDirector.Instance == null || !LogisticsDirector.Instance.TripsOn)
            {
                return;
            }
            _route = plan.id;
            Fill(PageMenu);
        }

        // The game's list window written with a page: the route's three settings and Send now, or the choices of one setting.
        private static void Fill(int page)
        {
            LogisticsDirector trips = LogisticsDirector.Instance;
            LogisticsManagerPlan plan = LogisticsGame.Plan(_route);
            if (trips == null || plan == null || !trips.TripsOn || LogisticsGame.From(plan) == null)
            {
                Window.Close();
                return;
            }
            TravelList list = Window.Take(Owner);
            if (list == null)
            {
                UiClone.Fallback("the game's list window cannot be made, so the route's departures are not listed");
                return;
            }
            _page = page;
            RouteTimes r = trips.Times(plan);
            string title = Ui.UiText.T("reallife_trips_departures");
            if (page == PageMenu)
            {
                int next = trips.NextRun(plan);
                list.Begin(title, LogisticsGame.NameOf(LogisticsGame.From(plan)) + (next >= 0 ? ". " + Ui.UiText.T("reallife_trips_next") + ": " + When(next) : string.Empty));
                list.Add(RowFirst, Ui.UiText.T("reallife_trips_first"), Hour(r.First), Select, true);
                list.Add(RowRuns, Ui.UiText.T("reallife_trips_runs"), Ui.UiText.Whole(r.Runs), Select, true);
                list.Add(RowGap, Ui.UiText.T("reallife_trips_gap"), Hours(r.Gap), Select, r.Runs > 1);
                bool sentToday = !SendNowLimit.Allowed(plan.id, LogisticsGame.Day);
                list.Add(RowSend, Ui.UiText.T("reallife_ui_bizops_send_now"), Ui.UiText.T(sentToday ? "reallife_ui_bizops_sent_today" : "reallife_trips_send_body"),
                    "reallife_ui_bizops_send_now", !sentToday && LogisticsGame.CanSendNow(plan));
            }
            else
            {
                list.Begin(Ui.UiText.T(page == PageFirst ? "reallife_trips_first" : page == PageRuns ? "reallife_trips_runs" : "reallife_trips_gap"), title);
                list.Add(RowBack, Ui.UiText.T("common_back"), null, "common_back", true);
                if (page == PageFirst)
                {
                    for (int h = 0; h < 24; h++)
                    {
                        list.Add(h, Hour(h), null, Select, h != r.First);   // the one it is now is not offered again
                    }
                }
                else
                {
                    int[] choices = page == PageRuns ? TripRules.RunChoices : TripRules.GapChoices;
                    for (int i = 0; i < choices.Length; i++)
                    {
                        list.Add(i, page == PageRuns ? Ui.UiText.Whole(choices[i]) : Hours(choices[i]), null, Select, choices[i] != (page == PageRuns ? r.Runs : r.Gap));
                    }
                }
                list.ScrollToTop();
            }
            list.Open(OnPick, OnClosed);
            if (Meta.Dev.Verbose)
            {
                LogisticsDirector.Logger?.Info("Trips check (the old page's list): page " + page + " of a route's departures is open, " + list.Count + " entries: PASS.");
            }
        }

        private static void Picked(int id)
        {
            LogisticsDirector trips = LogisticsDirector.Instance;
            LogisticsManagerPlan plan = LogisticsGame.Plan(_route);
            if (trips == null || plan == null || !trips.TripsOn || LogisticsGame.From(plan) == null)
            {
                Window.Close();
                return;
            }
            if (id == RowBack)
            {
                Fill(PageMenu);
                return;
            }
            RouteTimes r = trips.Times(plan);
            if (_page == PageMenu)
            {
                if (id == RowSend)
                {
                    string route = _route;
                    Window.Close();
                    Send(route);
                    return;
                }
                Fill(id == RowFirst ? PageFirst : id == RowRuns ? PageRuns : PageGap);
                return;
            }
            int first = r.First, runs = r.Runs, gap = r.Gap;
            if (_page == PageFirst)
            {
                first = id;
            }
            else if (_page == PageRuns && id >= 0 && id < TripRules.RunChoices.Length)
            {
                runs = TripRules.RunChoices[id];
            }
            else if (_page == PageGap && id >= 0 && id < TripRules.GapChoices.Length)
            {
                gap = TripRules.GapChoices[id];
            }
            trips.SetTimes(plan, first, runs, gap);
            LogisticsDirector.Logger?.Info("Delivery trips: a route's departures changed from the game's own page");
            Fill(PageMenu);
        }

        private static void Closed()
        {
            _route = null;
        }

        // Send now as trips: the game's question box first, then every free vehicle of the route leaves at once, and the answer is a notice.
        private static void Send(string id)
        {
            GameConfirm.Show("reallife_ui_bizops_send_title", "reallife_trips_send_body", () =>
            {
                int sent = LogisticsDirector.Instance != null ? LogisticsDirector.Instance.SendNowOnce(id) : 0;
                if (sent < 0)
                {
                    return;   // sent while the question stood
                }
                if (sent > 0)
                {
                    Notifications.Show(NotificationType.Info, "reallife_trips_sent", new Dictionary<string, string> { { "count", Ui.UiText.Whole(sent) } }, 8f, null, null, true, false);
                }
                else
                {
                    Notifications.Show(NotificationType.Warning, "reallife_trips_sent_none", null, 8f, null, null, true, false);
                }
            }, null, "reallife_ui_bizops_send_now", null, true, false);
        }

        // ------------------------------------------------------------------------------------------------- words

        private static string Hour(int hour) => hour.GetFormattedTime(0f);

        private static string Hours(int count) => Ui.UiText.Fill("reallife_trips_gap_value", "count", Ui.UiText.Whole(count));

        /// <summary>An absolute game minute as the game writes a time, with the day when it is not today (both interfaces' words for a departure).</summary>
        public static string When(int minute)
        {
            int day = minute >= 0 ? minute / TripRules.DayMinutes : 0, inDay = minute - day * TripRules.DayMinutes;
            string at = (inDay / 60).GetFormattedTime(inDay % 60);
            int today = TimeHelper.CurrentDay;
            return day == today ? at : day == today + 1 ? Ui.UiText.Fill("reallife_trips_tomorrow", "time", at)
                : Ui.UiText.Fill("reallife_trips_on_day", "day", Ui.UiText.Whole(day), "time", at);
        }
    }
}
