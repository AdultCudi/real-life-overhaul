using System;
using System.Globalization;
using RealLifeOverhaul.BuildTrades;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The two trades that walk the site. The surveyor goes to each corner of the land the game's marker shows (a plot's own
    /// corners, else the building's front paced out), sets up the level there and reads it on the bar. The night watch walks
    /// its rounds after hours: a noise somewhere round the site, the marker on it, and the thief caught if the player is there
    /// in time. Nobody is put on the street for the thief: the noise and the catch are said in the game's notifications.
    /// </summary>
    public sealed partial class TradeJob
    {
        private const float RoundSeconds = 30f;     // real seconds a round of the watch lasts
        private const float PegUp = 0.4f;           // metres over a peg its panel stands
        private const float ReadingHold = 6f;       // seconds the player is held at the level while the reading runs, at most

        private readonly Vector3[] _points = new Vector3[TradeRules.Corners];
        private int _point;                 // the corner to go to, or the post a noise came from
        private bool _reading;              // the level is set up at a corner and its bar runs
        private float _roundEnds;
        private bool _noise;
        private float _noiseUntil;
        private uint _watchSeed;
        private Action _onSetUp, _onCatch;

        // The site's own axes: its front is the way the player came to it; a plot's are its land's.
        private Vector3 Local(float x, float z)
        {
            if (_onSite && _site.Plot)
            {
                PlotRules.ToWorld(_site.At.x, _site.At.z, _site.Yaw, x, z, out float wx, out float wz);
                return Ground(new Vector3(wx, _site.At.y, wz));
            }
            Vector3 front = -_face;   // from the site towards the player: the street side
            Vector3 right = Vector3.Cross(Vector3.up, front);
            Vector3 middle = _onSite ? _site.At : _post + _face * 4f;
            return Ground(middle + right * x - front * z);
        }

        // A place on the pavement near a point if there is one with room, else the point itself.
        private static Vector3 Ground(Vector3 at)
        {
            return CitySpots.Stand(at, 0.3f, out Vector3 spot) && JobSpots.Flat(spot, at) < 3f ? spot : at;
        }

        private void BeginSurvey(float now)
        {
            float width = _onSite && _site.Plot ? _site.Width : 12f;
            float depth = _onSite && _site.Plot ? _site.Depth : 6f;
            for (int c = 0; c < TradeRules.Corners; c++)
            {
                TradeRules.Corner(c, width, depth, out float x, out float z);
                _points[c] = Local(x, z);
            }
            _units = TradeRules.Corners;
            _onSetUp = () => SetUp(Time.time);
            _onPress = () => Press(Time.time);
            _onSkip = OfferSkip;
            _pressKey = "reallife_trades_surveyor_button";
            _point = 0;
            Say(JobText.T("reallife_trades_surveyor_obj", "corner", "1"));
        }

        private void TickSurvey(float now)
        {
            if (_point >= TradeRules.Corners)
            {
                return;
            }
            Vector3 peg = _points[_point];
            Mark(peg, JobText.T("reallife_trades_surveyor_peg", "corner", (_point + 1).ToString(CultureInfo.InvariantCulture)));
            float away = JobSpots.Flat(Ctx.PlayerPosition, peg);
            bool near = away <= TradeRules.CornerReach;
            int state = _point | (near ? 1 << 4 : 0) | (_reading ? 1 << 5 : 0) | (_timing ? 1 << 6 : 0) | (_misses << 7);
            if (state != _panelState)
            {
                _panelState = state;
                WorldPanel p = _panel.Panel;
                p.Lines(JobText.T("reallife_trades_surveyor_peg", "corner", (_point + 1).ToString(CultureInfo.InvariantCulture)),
                    near ? JobText.T("reallife_trades_surveyor_here") : JobText.T("reallife_trades_surveyor_walk"), _misses > 0 ? JobText.T("reallife_trades_again") : null,
                    _misses > 0 ? WorldPanel.Yellow() : _panel.White);
                if (_reading)
                {
                    p.Button(0, _pressKey, _onPress, _timing, true);
                }
                else
                {
                    p.Button(0, "reallife_trades_surveyor_set_up", _onSetUp, near, true);
                }
                p.Button(1, ButtonSkip, _onSkip, _clock != null && DoneCount < _units && !_reading, false);
                p.Button(2, null, null, false, false);
                p.Button(3, null, null, false, false);
            }
            if (_reading && !_timing && now >= _quiet)
            {
                NewStroke(now);
            }
            if (_reading && !Ctx.Self.Holding)
            {
                _reading = false;   // the hold ran out or something took the body: set up again
                _timing = false;
                _bar?.Show(false);
                _panelState = int.MinValue;
            }
            _panel.Show(peg + Vector3.up * PegUp);
        }

        // At the corner's peg: the level set up, the player held while it is read.
        private void SetUp(float now)
        {
            if (Over || _reading || _point >= TradeRules.Corners || JobSpots.Flat(Ctx.PlayerPosition, _points[_point]) > TradeRules.CornerReach)
            {
                return;
            }
            Ctx.Self.Hold(ReadingHold);
            if (ClipLibrary.Has(_trade.Clip))
            {
                Ctx.Self.Clip(_trade.Clip);
            }
            _reading = true;
            _misses = 0;
            _quiet = now + 0.3f;
            _panelState = int.MinValue;
        }

        // The level read at a corner: the corner done, by how true the reading was.
        private void SurveyRead(float worth, float now)
        {
            _reading = false;
            Ctx.Self.Free();
            _stroke = 0;
            _worth = 0f;
            Laid(worth, now);
            _point++;
            Unmark();
            if (_point < TradeRules.Corners)
            {
                Say(JobText.T("reallife_trades_surveyor_obj", "corner", (_point + 1).ToString(CultureInfo.InvariantCulture)));
            }
            _panelState = int.MinValue;
        }

        private void BeginWatch(float now)
        {
            for (int p = 0; p < TradeRules.Posts; p++)
            {
                TradeRules.Post(p, out float x, out float z);
                _points[p] = Local(x, z);
            }
            _watchSeed = (uint)TradeDice.Mix(TimeHelper.CurrentDay, _onSite ? _site.Id : 0, Mathf.RoundToInt(TimeHelper.NowInMinutes()), 0x2B992DDFu);
            _onCatch = () => Catch(Time.time);
            _onSkip = OfferSkip;
            NewRound(now);
        }

        private void NewRound(float now)
        {
            _roundEnds = now + RoundSeconds;
            _noise = TradeRules.Noise(_watchSeed, DoneCount, out _point);
            _noiseUntil = now + _trade.Seconds;
            _panelState = int.MinValue;
            if (_noise)
            {
                JobText.Toast(NotificationType.Warning, "reallife_trades_security_noise", "where", JobText.T(TradeCatalogue.Key + "security_post_" + _point), 6f);
                Say(JobText.T("reallife_trades_security_obj_noise"));
            }
            else
            {
                Say(JobText.T("reallife_trades_security_obj"));
            }
        }

        private void TickWatch(float now)
        {
            if (DoneCount >= _units)
            {
                return;
            }
            if (_noise && now > _noiseUntil)
            {
                JobText.Toast(NotificationType.Warning, "reallife_trades_security_away", 6f);   // the thief got away with something
                RoundDone(false, now);
                return;
            }
            if (!_noise && now >= _roundEnds)
            {
                RoundDone(false, now);
                return;
            }
            Vector3 at = _noise ? _points[_point] : Ctx.Player.transform.position;
            bool near = _noise && JobSpots.Flat(Ctx.PlayerPosition, at) <= TradeRules.CatchReach;
            int seconds = Mathf.Max(0, Mathf.CeilToInt((_noise ? _noiseUntil : _roundEnds) - now));
            int state = (_noise ? 1 : 0) | (near ? 2 : 0) | (seconds << 2) | (DoneCount << 10);
            if (state != _panelState)
            {
                _panelState = state;
                WorldPanel p = _panel.Panel;
                p.Lines(JobText.T(_noise ? TradeCatalogue.Key + "security_post_" + _point : "reallife_trades_security_quiet"),
                    JobText.T("reallife_trades_security_round", "round", (DoneCount + 1).ToString(CultureInfo.InvariantCulture), "count", _units.ToString(CultureInfo.InvariantCulture)),
                    ShiftFigures.Clock(seconds), _noise && seconds <= 8 ? WorldPanel.Red() : _panel.White);
                p.Button(0, _noise ? "reallife_trades_security_catch" : null, _noise ? _onCatch : null, near, true);
                p.Button(1, ButtonSkip, _onSkip, !_noise && _clock != null, false);
                p.Button(2, null, null, false, false);
                p.Button(3, null, null, false, false);
                Lines(_noise ? JobText.T(TradeCatalogue.Key + "security_post_" + _point) : JobText.T("reallife_trades_security_quiet"),
                    JobText.T("reallife_trades_seconds", "seconds", seconds.ToString(CultureInfo.InvariantCulture)));
            }
            if (_noise)
            {
                Mark(at, JobText.T(TradeCatalogue.Key + "security_post_" + _point));
                _panel.Show(at + Vector3.up * PanelLift);
            }
            else
            {
                Unmark();
                _panel.Show(Ctx.Player.transform, PanelLift);
            }
        }

        private void Catch(float now)
        {
            if (Over || !_noise || JobSpots.Flat(Ctx.PlayerPosition, _points[_point]) > TradeRules.CatchReach)
            {
                return;
            }
            JobText.Toast(NotificationType.Success, "reallife_trades_security_caught", 6f);
            if (ClipLibrary.Has("point_direct"))
            {
                Ctx.Self.Hold(2f);
                Ctx.Self.Clip("point_direct");
            }
            RoundDone(true, now);
        }

        private void RoundDone(bool caught, float now)
        {
            float worth = TradeRules.RoundWorth(_noise, caught);
            int pay = Mathf.RoundToInt(Def.UnitPay * Ctx.Rate * worth);
            _base += pay;
            _done++;
            _learnt++;
            _perfect += _noise && caught ? 1 : 0;
            Ctx.Logger?.Info("Jobs: night watch round " + _done + " of " + _units + ": " + (!_noise ? "quiet" : caught ? "a thief caught at post " + _point : "a thief got away at post " + _point)
                             + ", paid " + pay + ".");
            Unmark();
            if (DoneCount < _units)
            {
                NewRound(now);
            }
        }
    }
}
