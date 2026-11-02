using System;
using System.Collections.Generic;
using Buildings.Office.Headquarters;
using Entities;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    public sealed partial class LogisticsDirector
    {
        /// <summary>
        /// Options > Tests: each route the trips run, read as its next departure would load it (nothing moves, nothing is paid):
        /// its slots, stops, pieces and boxes, the road and the time round, the fuel and the wages. One line each in the log.
        /// </summary>
        public void TestTrips()
        {
            if (!TripsOn)
            {
                Tell("reallife_trips_test_off", 0, 0);
                return;
            }
            int routes = 0, ok = 0;
            List<LogisticsManagerPlan> plans = LogisticsGame.Plans;
            for (int i = 0; plans != null && i < plans.Count; i++)
            {
                LogisticsManagerPlan plan = plans[i];
                Warehouse from = plan != null ? LogisticsGame.From(plan) : null;
                if (from == null)
                {
                    continue;
                }
                routes++;
                int count = LogisticsGame.Slots(from, _slots), stops = LogisticsGame.Stops(plan, _stops), pieces = 0, boxes = 0, room = 0, driving = 0;
                for (int s = 0; s < count; s++)
                {
                    driving += _slots[s].Stops > 0 ? 1 : 0;
                    room += _slots[s].Stops > 0 ? _slots[s].Room : 0;
                }
                int left = room;
                for (int k = 0; k < stops; k++)
                {
                    LogisticsGame.Needs(_stops[k], _items, _wanted);
                    for (int g = 0; g < _items.Count; g++)
                    {
                        int before = left;
                        pieces += TripRules.Fit(Math.Min(_wanted[g], LogisticsGame.InStock(from, _items[g])), LogisticsGame.BoxSize(_items[g]), ref left);
                        boxes += before - left;
                    }
                }
                RouteTimes r = Times(plan);
                bool pass = boxes <= room && (driving > 0 || stops == 0);
                ok += pass ? 1 : 0;
                Logger?.Info("Trips check (the test, a route from " + LogisticsGame.NameOf(from) + "): " + r.Runs + " run(s) a day from " + r.First + ":00 every " + r.Gap
                             + " hour(s), " + driving + " of " + count + " slot(s) driving, " + stops + " stop(s), " + pieces + " piece(s) in " + boxes + " of " + room
                             + " box(es) at its next run, " + (LogisticsGame.Managed(plan) ? "its manager at work" : "its manager away") + ": " + (pass ? "PASS" : "FAIL"));
            }
            Logger?.Info("Trips check (the test): " + routes + " route(s) run as trips, " + _trips.Trips.Count + " trip(s) on the road or waiting, " + _trips.Log.Count
                         + " run(s) in the week's log, " + (LogisticsGame.Holding ? "a route still held: FAIL" : "no route held outside the night's run: PASS"));
            Tell("reallife_trips_test_done", routes, ok);
        }

        /// <summary>Options > Tests: a whole round of the player's places at once, then what each holds that spoils and its oldest batch.</summary>
        public void TestSpoilage()
        {
            if (!SpoilOn)
            {
                Tell("reallife_spoil_test_off", 0, 0);
                return;
            }
            LogisticsGame.Owned(_owned);
            int today = LogisticsGame.Day, places = 0, products = 0, wrong = 0;
            for (int i = 0; i < _owned.Count; i++)
            {
                Visit(_owned[i], today);
                string key = LogisticsGame.Key(_owned[i].Address);
                if (!_spoil.Places.TryGetValue(key, out SpoilPlace p))
                {
                    continue;
                }
                places++;
                foreach (KeyValuePair<string, List<Cohort>> kv in p.Batches)
                {
                    products++;
                    int held = LogisticsGame.InStock(_owned[i], kv.Key), kept = SpoilRules.Total(kv.Value);
                    wrong += held == kept ? 0 : 1;
                    Logger?.Info("Spoilage check (the test, " + LogisticsGame.NameOf(_owned[i]) + "): " + kv.Key + " " + kept + " piece(s) in " + kv.Value.Count
                                 + " batch(es), the oldest spoils in " + SpoilsIn(_owned[i], kv.Key) + " day(s), " + held + " held: " + (held == kept ? "PASS" : "FAIL"));
                }
            }
            Logger?.Info("Spoilage check (the test): " + places + " place(s), " + products + " product(s) that spoil, " + wrong + " not matched: " + (wrong == 0 ? "PASS" : "FAIL"));
            Tell("reallife_spoil_test_done", places, products);
        }

        private static void Tell(string key, int a, int b)
        {
            Notifications.Show(NotificationType.Info, key, new Dictionary<string, string>
            {
                { "count", a.ToString(System.Globalization.CultureInfo.InvariantCulture) }, { "passed", b.ToString(System.Globalization.CultureInfo.InvariantCulture) }
            }, 7f, null, null, true, false);
        }
    }
}
