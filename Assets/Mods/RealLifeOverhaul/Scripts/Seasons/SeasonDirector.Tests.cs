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
        private const float SampleRadius = 40f;   // metres round the player the cars and people are sampled from
        private const float ShowCheckAfter = 15f; // seconds before a season's test look is checked: the first flash may take twelve

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

        /// <summary>
        /// "Seasons: show me (a season) here now": the trees, sky, rain, lightning, leaves and the line under the date of
        /// that season at its height, held three minutes where the player stands (winter with a heavy fall); pressed again, it stops.
        /// </summary>
        public void TestShow(int season)
        {
            if (SeasonGame.Loading())
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                return;
            }
            if (!Settings.Seasons || _broken || StandDown())
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.FeatureOff);
                return;
            }
            if (Showing && _showSeason == season)
            {
                _showUntil = 0f;
                _holdUntil = season == CalendarRules.Winter ? 0f : _holdUntil;
                SeasonGame.EndRain();
                Logger?.Info("Seasons test (" + SeasonText.Name(season) + " look): stopped; the day's own look again.");
                Notifications.Show(NotificationType.Info, "reallife_season_test_show_stopped", null, 6f);
                return;
            }
            if (!SeasonGame.Outdoors)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.Indoors);
                return;
            }
            _showSeason = season;
            _showUntil = Time.unscaledTime + ShowSeconds;
            _flashDay = -1;
            if (season == CalendarRules.Winter)
            {
                _holdUntil = Time.unscaledTime + HoldSeconds;
            }
            else
            {
                _holdUntil = 0f;
                SeasonGame.EndRain();
            }
            bool wet = season == CalendarRules.Spring || season == CalendarRules.Summer;
            if (wet && Settings.SeasonRain && !GoingPublic.IsGuest)
            {
                SeasonGame.StartRain(season == CalendarRules.Summer ? StormStrength : ShowerStrength, SeasonGame.Hour + TestRainHours, true);
            }
            Notifications.Show(NotificationType.Info, "reallife_season_test_show_started", new Dictionary<string, string>(1) { { "season", SeasonText.Title(season) } }, 8f);
            StartCoroutine(Guard.Routine(LookAtSeason(season, _seasonLook.Flashes)));
        }

        private IEnumerator LookAtSeason(int season, int flashesBefore)
        {
            yield return new WaitForSecondsRealtime(ShowCheckAfter);
            if (!Showing || _showSeason != season)
            {
                yield break;
            }
            int year = GameCalendar.DaysPerYear;
            int day = SeasonLookRules.ShowDay(season, year, 0.5f);
            Tint wanted = SeasonLookRules.Foliage(day, 12f, year);
            Tint on = SeasonGame.TreeTint;
            var why = new StringBuilder(160);
            var text = new StringBuilder(512);
            text.Append("Seasons test (").Append(SeasonText.Name(season)).Append(" look): the trees ");
            if (!Settings.SeasonTrees)
            {
                text.Append("left as the game has them (option off)");
            }
            else
            {
                text.Append("x(").Append(F(on.R)).Append(", ").Append(F(on.G)).Append(", ").Append(F(on.B)).Append(") on ").Append(SeasonGame.LeafMaterials)
                    .Append(" leaf material(s), ").Append(SeasonGame.TreesWalked).Append(" of ").Append(SeasonGame.TreeCount).Append(" trees looked at, wanted x(")
                    .Append(F(wanted.R)).Append(", ").Append(F(wanted.G)).Append(", ").Append(F(wanted.B)).Append(')');
                if (SeasonGame.LeafMaterials == 0 && SeasonGame.TreeCount > 0 && SeasonGame.TreesWalked >= SeasonGame.TreeCount)
                {
                    why.Append(" no leaf material of the game's wind shader on its trees;");
                }
                else if (SeasonGame.LeafMaterials > 0 && !wanted.Plain && !on.Same(wanted))
                {
                    why.Append(" the leaves not tinted as wanted;");
                }
            }
            text.Append("; the sky ").Append(F(SeasonGame.SkyOvercast)).Append(" overcast, ").Append(F(SeasonGame.SkyWarmth)).Append(" warmer")
                .Append(SeasonGame.SkyMissing != null ? " (" + SeasonGame.SkyMissing + ")" : string.Empty);
            if (Settings.SeasonSky && SeasonGame.SkyMissing == null && season == CalendarRules.Winter && SeasonGame.SkyOvercast <= 0f)
            {
                why.Append(" no overcast on the snow;");
            }
            text.Append("; rain ").Append(SeasonGame.RainOurs ? "the season's" : SeasonGame.RainFalling ? "the game's own" : "none")
                .Append(GoingPublic.IsGuest ? " (a Going Public guest's rain is the host's)" : string.Empty);
            bool wet = season == CalendarRules.Spring || season == CalendarRules.Summer;
            if (wet && Settings.SeasonRain && !GoingPublic.IsGuest && !SeasonGame.RainFalling)
            {
                why.Append(" no rain falling;");
            }
            int flashes = _seasonLook.Flashes - flashesBefore;
            text.Append("; ").Append(flashes).Append(" flash(es) of lightning");
            if (season == CalendarRules.Summer && Settings.SeasonRain && SeasonGame.RainFalling && flashes == 0)
            {
                why.Append(" no lightning in the storm;");
            }
            text.Append("; ").Append(_seasonLook.Leaves).Append(" leaf(s) falling").Append(_seasonLook.LeavesMissing != null ? " (" + _seasonLook.LeavesMissing + ")" : string.Empty);
            if (season == CalendarRules.Autumn && Settings.SeasonLeaves && !_seasonLook.Leafing)
            {
                why.Append(" no leaves falling;");
            }
            text.Append("; under the date: ").Append(SeasonBar.Shown ? "'" + SeasonBar.Line + "'" : "nothing");
            if (Settings.SeasonBar && !SeasonBar.Shown)
            {
                why.Append(" nothing under the date;");
            }
            text.Append("; ").Append(SeasonGame.CoatsOn).Append(" person(s) dressed for the cold").Append(Settings.SeasonCoats ? string.Empty : " (option off)");
            text.Append(": ").Append(why.Length == 0 ? "PASS" : "FAIL:" + why.ToString().TrimEnd(';'));
            Logger?.Info(text.ToString());
            if (season == CalendarRules.Winter)
            {
                Logger?.Info(LookCheck());
            }
        }

        private static string F(float value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
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
                         + (_look.Way == SnowWay.Layers ? " over the ground and the roofs" : " (none lying: " + (_look.CoverMissing ?? "not made yet") + ")")
                         + ", the roads' snow " + Events.Weather.Snow + ", traffic x"
                         + Events.Weather.TrafficSpeedFactor.ToString("0.00", CultureInfo.InvariantCulture) + ", grip x" + Events.Weather.GripFactor.ToString("0.00", CultureInfo.InvariantCulture)
                         + ", walkers set off at x" + SeasonGame.WalkApplied.ToString("0.00", CultureInfo.InvariantCulture) + " of their pace: "
                         + (why.Length == 0 ? "PASS" : "FAIL:" + why.ToString().TrimEnd(';')));
            Logger?.Info(LookCheck());
        }

        // Which way the ground is whitened at this load, and a sample of the cars and people near the player, none of
        // which may be on the snow's layer; the rest of the picture the seasons leave as the game draws it.
        private string LookCheck()
        {
            SnowGround ground = _look.Ground;
            var renderers = new List<Renderer>(256);
            int bodies = SeasonGame.Bodies(SampleRadius, renderers);
            int onSnow = 0;
            string first = null;
            for (int i = 0; i < renderers.Count; i++)
            {
                if (ground.On(renderers[i]))
                {
                    onSnow++;
                    first = first ?? renderers[i].name;
                }
            }
            bool layers = SnowLook.LayersInEffect();
            var text = new StringBuilder(512);
            text.Append("Seasons test (snow look): the ground whitened ");
            bool wayOk;
            switch (_look.Way)
            {
                case SnowWay.Layers:
                    text.Append("on the snow's own decal layer (").Append(ground.Marked).Append(" piece(s) marked, ").Append(ground.Roofed).Append(" of them roofs, the projector on ")
                        .Append(_look.CoverLayer).Append("), the camera's decal layers ").Append(layers ? "in effect" : "NOT in effect");
                    wayOk = layers && _look.Covering && ground.Marked > 0 && (_look.CoverLayer & UnityEngine.Rendering.HighDefinition.DecalLayerEnum.DecalLayerDefault) == 0;
                    break;
                case SnowWay.None:
                    text.Append("not at all (").Append(_look.CoverMissing ?? "no reason kept").Append("): flakes and the roads' grip only");
                    wayOk = !_look.Covering;
                    break;
                default:
                    text.Append("not yet (").Append(_look.CoverMissing ?? ground.Missing ?? "not tried").Append(')');
                    wayOk = !_look.Covering;
                    break;
            }
            text.Append("; ").Append(renderers.Count).Append(" renderer(s) of ").Append(bodies).Append(" car(s) and person(s) within ").Append(SampleRadius.ToString("0", CultureInfo.InvariantCulture))
                .Append(" m, the player's own among them, ").Append(onSnow).Append(" on the snow's layer").Append(first != null ? " (the first: '" + first + "')" : string.Empty)
                .Append("; the seasons set no volume, fog, sky, exposure or light and no material, tint or keyword on any person or car; the flakes are ")
                .Append(_look.Flakes).Append(" small lit cubes of the mod's paint with a faint glow: ")
                .Append(wayOk && onSnow == 0 && bodies > 0 ? "PASS" : "FAIL" + (onSnow > 0 ? " (a car or person is on the snow's layer)" : string.Empty)
                                                            + (wayOk ? string.Empty : " (the ground's way is not as wanted)")
                                                            + (bodies > 0 ? string.Empty : " (nobody sampled, not even the player)"));
            return text.ToString();
        }
    }
}
