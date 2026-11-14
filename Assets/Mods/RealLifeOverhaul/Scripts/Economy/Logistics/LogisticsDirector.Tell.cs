using System.Collections.Generic;
using Buildings.Office.Headquarters;
using Entities;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    public sealed partial class LogisticsDirector
    {
        private readonly TripTally _tally = new TripTally();
        private readonly List<KeyValuePair<string, RouteTally>> _written = new List<KeyValuePair<string, RouteTally>>(8);

        // The route's tally of today, its warehouse's name kept for the day's line.
        private RouteTally Tally(string plan, Warehouse from) =>
            _tally.Of(plan, LogisticsGame.Day, from != null ? LogisticsGame.NameOf(from) : null);

        // Goods a trip wanted to take and did not: counted, and the player told once a day for the route.
        private void Behind(LogisticsManagerPlan plan, Warehouse from, string place, int pieces, TripWhy why)
        {
            Tally(plan.id, from).Left += pieces;
            Missed(plan, from, place, pieces, why);
        }

        // Goods that did not reach a stop: the reason kept for the day's line, and the first one of the day told, with what to do about it.
        // A route that fails every day is told about once a day, not once a stop: the rest are in the log's line.
        private void Missed(LogisticsManagerPlan plan, Warehouse from, string place, int pieces, TripWhy why)
        {
            if (plan == null || pieces <= 0 || why == TripWhy.None)
            {
                return;
            }
            RouteTally t = Tally(plan.id, from);
            t.Why |= why;
            if (t.Told)
            {
                return;
            }
            t.Told = true;
            TripWhy first = TripTally.First(why);
            BuildingRegistration stop = place != null ? LogisticsGame.Reg(place) : null;
            Notifications.Show(NotificationType.Warning, "reallife_trips_fail_note", new Dictionary<string, string>
            {
                { "warehouse", LogisticsGame.NameOf(from) },
                { "count", pieces.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                { "place", stop != null ? LogisticsGame.NameOf(stop) : Ui.UiText.T("reallife_trips_its_stops") },
                { "reason", Ui.UiText.T(TripTally.ReasonKey(first)) },
                { "advice", Ui.UiText.T(TripTally.AdviceKey(first)) }
            }, 12f, null, null, true, false);
            Logger?.Warn("Delivery trips: " + pieces + " piece(s) from " + LogisticsGame.NameOf(from) + " did not reach " + (stop != null ? LogisticsGame.NameOf(stop) : "its stops")
                         + ": " + TripTally.Words(first) + ". The player was told.");
        }

        // The days before today's lines, one for each route that did anything: units taken, delivered, returned and lost. A trip that crosses midnight
        // takes its goods in one day's line and delivers them in the next's.
        private void TallyWrite(int today, bool all)
        {
            if (_tally.Take(today, all, _written) == 0)
            {
                return;
            }
            for (int i = 0; i < _written.Count; i++)
            {
                RouteTally t = _written[i].Value;
                if (t.Quiet)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(t.Name))
                {
                    LogisticsManagerPlan plan = LogisticsGame.Plan(_written[i].Key);
                    Warehouse from = plan != null ? LogisticsGame.From(plan) : null;
                    t.Name = from != null ? LogisticsGame.NameOf(from) : null;
                }
                Logger?.Info(TripTally.Line(t));
            }
        }
    }
}
