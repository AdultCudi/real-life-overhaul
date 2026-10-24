using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>
    /// The seasons' test buttons: the season's goods as written on the game's items, with the coming snow days, into
    /// Player.log; and a heavy fall where the player stands, held for three minutes, with its check line.
    /// </summary>
    public sealed partial class SeasonDirector
    {
        private const string TestName = "Seasons";
        private const float HoldSeconds = 180f;   // the test's snow
        private const float LookAfter = 6f;       // seconds before the test's snow is checked: the look and the weather take a second each
        private const int SnowDaysListed = 8;

        /// <summary>"Show this season's goods and the coming snow in the log."</summary>
        public void TestGoods()
        {
            if (SeasonGame.Loading())
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                return;
            }
            if (!Settings.Seasons || _broken)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.FeatureOff);
                return;
            }
            int day = SeasonGame.Today, year = GameCalendar.DaysPerYear, hour = SeasonGame.WholeHour;
            int season = SeasonRules.SeasonOf(SeasonRules.DrawDay(day, hour), year);
            Demand(season, day);
            var text = new StringBuilder(2048);
            text.Append("Seasons test: day ").Append(day).Append(" of a ").Append(year).Append("-day year, ").Append(SeasonText.Name(SeasonRules.SeasonOf(day, year)))
                .Append(" since day ").Append(SeasonRules.SeasonStart(day, year)).Append("; tonight's customers are drawn for ").Append(SeasonText.Name(season)).Append('.');
            int held = 0;
            for (int g = 0; g < SeasonRules.GoodCount; g++)
            {
                text.Append("\n  ").Append(SeasonRules.Goods[g]).Append(' ').Append(SeasonRules.Change(SeasonRules.DemandOf(g, season)));
                if (SeasonGame.Ratios(g, out float own, out float now))
                {
                    held++;
                    text.Append(": sales ratio ").Append(own.ToString("0.###", CultureInfo.InvariantCulture)).Append(" as the game has it, ")
                        .Append(now.ToString("0.###", CultureInfo.InvariantCulture)).Append(" now");
                }
                else
                {
                    text.Append(": not in this build of the game");
                }
            }
            int salt = SeasonBridges.Salt != null ? SeasonBridges.Salt() : 0;
            bool winterOnly = true, same = true;
            int listed = 0, first = 0;
            text.Append("\n  snow days in the coming year:");
            for (int d = day; d < day + year && listed < SnowDaysListed; d++)
            {
                SnowDay snow = SeasonRules.SnowOn(d, year, salt);
                if (!snow.Falls)
                {
                    continue;
                }
                SnowDay again = SeasonRules.SnowOn(d, year, salt);
                same &= again.From == snow.From && again.Hours == snow.Hours && again.Heavy == snow.Heavy;
                winterOnly &= SeasonRules.SeasonOf(d, year) == CalendarRules.Winter;
                first = first == 0 ? d : first;
                listed++;
                text.Append(" day ").Append(d).Append(' ').Append(snow.From).Append(":00 to ").Append(snow.To).Append(":00").Append(snow.Heavy ? " heavy" : " light").Append(';');
            }
            text.Append(listed == 0 ? " none" : string.Empty);
            string check = DemandCheck(season, out bool ok);
            text.Append("\nSeasons test (goods): ").Append(check).Append("; snow only in winter and the same when asked again: ")
                .Append(ok && winterOnly && same ? "PASS" : "FAIL" + (winterOnly ? string.Empty : " (snow outside winter)") + (same ? string.Empty : " (snow not the same twice)"));
            Logger?.Info(text.ToString());
            Notifications.Show(NotificationType.Info, "reallife_season_test_goods_done", new Dictionary<string, string>(3)
            {
                { "season", SeasonText.Title(season) }, { "count", held.ToString(CultureInfo.InvariantCulture) },
                { "day", first > 0 ? first.ToString(CultureInfo.InvariantCulture) : "-" }
            }, 10f);
        }

        /// <summary>"Let it snow here now": a heavy fall and the ground whitening for three minutes; pressed again, it stops.</summary>
        public void TestSnow()
        {
            if (SeasonGame.Loading())
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                return;
            }
            if (!Settings.Seasons || _broken)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.FeatureOff);
                return;
            }
            if (Holding)
            {
                _holdUntil = 0f;
                Logger?.Info("Seasons test (snow): stopped; the snow is the day's own again.");
                Notifications.Show(NotificationType.Info, "reallife_season_test_snow_stopped", null, 6f);
                return;
            }
            if (!SeasonGame.Outdoors)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.Indoors);
                return;
            }
            _holdUntil = Time.unscaledTime + HoldSeconds;
            Notifications.Show(NotificationType.Info, "reallife_season_test_snow_started", null, 8f);
            StartCoroutine(Guard.Routine(LookAtSnow()));
        }

        private IEnumerator LookAtSnow()
        {
            yield return new WaitForSecondsRealtime(LookAfter);
            if (!Holding)
            {
                yield break;
            }
            var why = new StringBuilder(160);
            if (!Settings.SnowLook)
            {
                why.Append(" the snow's look is switched off in the options;");
            }
            else
            {
                if (!_look.Flaking)
                {
                    why.Append(" no flakes falling (").Append(_look.FlakesMissing ?? "not made yet").Append(");");
                }
                if (!_look.Covering)
                {
                    why.Append(" the ground not covered (").Append(_look.CoverMissing ?? "not made yet").Append(");");
                }
            }
            if (Settings.WeatherMatters)
            {
                if (Events.Weather.Snow == 0 || Events.Weather.TrafficSpeedFactor >= 1f)
                {
                    why.Append(" the roads not snowy to the weather (snow ").Append(Events.Weather.Snow).Append(");");
                }
            }
            Logger?.Info("Seasons test (snow): " + _look.Flakes + " flake(s) in the air, the cover at " + Cover.ToString("0.00", CultureInfo.InvariantCulture)
                         + (_look.Tall ? " over the ground and the roofs" : " on the ground") + ", the roads' snow " + Events.Weather.Snow + ", traffic x"
                         + Events.Weather.TrafficSpeedFactor.ToString("0.00", CultureInfo.InvariantCulture) + ", grip x" + Events.Weather.GripFactor.ToString("0.00", CultureInfo.InvariantCulture)
                         + ", walkers set off at x" + SeasonGame.WalkApplied.ToString("0.00", CultureInfo.InvariantCulture) + " of their pace: "
                         + (why.Length == 0 ? "PASS" : "FAIL:" + why.ToString().TrimEnd(';')));
        }
    }
}
