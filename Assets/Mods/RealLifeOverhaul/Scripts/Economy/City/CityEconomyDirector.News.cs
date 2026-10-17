using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The city's economy director: what the player is told, in notifications and in the paper.</summary>
    public sealed partial class CityEconomyDirector
    {
        private readonly List<CityNews> _picked = new List<CityNews>(MacroCatalogue.NewsADay);

        // Only what touches the player: a rate move while they owe on a card or overdraft, a shock in goods they stock.
        private void Notify(CityTick tick)
        {
            if (tick.RateMoved != 0 && OwesVariable())
            {
                Notifications.Show(tick.RateMoved > 0 ? NotificationType.Warning : NotificationType.Info, tick.RateMoved > 0 ? "reallife_macro_note_rate_up" : "reallife_macro_note_rate_down",
                    new Dictionary<string, string> { { "points", CityText.Points(tick.RateMoved) }, { "rate", CityText.Rate(_state.Base) } }, 10f, null, Guard.Wrap(OpenCity), true, false);
            }
            int kind = tick.ShockAnnounced;
            if (kind >= 0 && (_state.Buys & (1 << kind)) != 0 && _state.Supply[kind].Announced(_state.Age))
            {
                SupplyLine line = _state.Supply[kind];
                Notifications.Show(line.Shock == SupplyShock.Shortage ? NotificationType.Warning : NotificationType.Info,
                    line.Shock == SupplyShock.Shortage ? "reallife_macro_note_shortage" : "reallife_macro_note_glut",
                    new Dictionary<string, string>
                    {
                        { "kind", CityText.Kind(kind) }, { "percent", Math.Abs(CityModel.ShockPercent(line, _level)).ToString() }, { "days", line.Days.ToString() }
                    }, 12f, null, Guard.Wrap(OpenCity), true, false);
            }
        }

        // The evening before a decision of the central bank takes effect, one notice of the move announced; once a day.
        private void DecisionEve(int day, int hour)
        {
            if (!Settings.EconomyRateAhead || !CityModel.DecisionEve(_state, _monthDays, day, hour, _eveTold))
            {
                return;
            }
            _eveTold = day;
            int after = MacroCatalogue.Clamp(_state.Base + _state.PendingMove, MacroCatalogue.BaseLeast, MacroCatalogue.BaseMost);
            Notifications.Show(NotificationType.Info, CityModel.DecisionEveKey(_state.PendingMove),
                new Dictionary<string, string> { { "points", CityText.Points(_state.PendingMove) }, { "rate", CityText.Rate(after) } }, 10f, null, Guard.Wrap(OpenCity), true, false);
            if (Verbose)
            {
                Logger?.Info("Economy check (city, the evening before a decision, day " + day + " at " + hour.ToString("00") + ":00): the rate " + Quarters(_state.Base) + " to "
                             + Quarters(after) + " from tomorrow; the notice sent once: PASS");
            }
        }

        private static bool OwesVariable()
        {
            CreditFile file = CreditLedger.File;
            for (int i = 0; file != null && i < file.Accounts.Count; i++)
            {
                CreditAccount a = file.Accounts[i];
                if (!a.Closed && !a.Test && a.Owed > 0 && ProductCatalogue.Revolving(a.Kind))
                {
                    return true;
                }
            }
            return false;
        }

        private static void OpenCity()
        {
            Finance.OpenCity();
        }

        /// <summary>The pieces for a day's paper, the most pressing first, two at most. The list is reused by the next call.</summary>
        public IReadOnlyList<CityNews> Paper(int day)
        {
            _picked.Clear();
            if (!_running)
            {
                return _picked;
            }
            List<CityNews> news = _state.News;
            for (int rank = 0; rank < MacroCatalogue.NewsKinds && _picked.Count < MacroCatalogue.NewsADay; rank++)
            {
                for (int i = 0; i < news.Count && _picked.Count < MacroCatalogue.NewsADay; i++)
                {
                    if (news[i].Day == day && MacroCatalogue.NewsRank(news[i].Kind) == rank)
                    {
                        _picked.Add(news[i]);
                    }
                }
            }
            return _picked;
        }
    }
}
