using System;
using System.Globalization;
using RealLifeOverhaul.BuildTrades;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A trade's timing on the game's bar (<see cref="GameBar"/> on the shift card): the zone drawn for each stroke, the marker
    /// running across and back (a crane's load swinging as a pendulum), the panel's button pressed while it is in the zone. A
    /// stroke out of the zone is done again, at most twice. The mason's brick, the labourer's spit or batch, the scaffolder's
    /// coupler, the painter's stroke, the roofer's tile, the crane's load set down, the surveyor's level read.
    /// </summary>
    public sealed partial class TradeJob
    {
        private bool _timing;          // a stroke's zone is on the bar and the marker runs
        private float _strokeFrom;     // Time.time the marker set off
        private float _zoneCentre, _zoneWidth;
        private int _stroke;           // strokes laid of the unit
        private int _misses;           // misses of the stroke being laid
        private float _worth;          // the unit's strokes' worth so far
        private Action _onPress, _onSkip, _onAway;
        private string _pressKey;
        private string _zoneWords;     // the zone in words, for a card without the game's bar

        private float MarkerNow(float now)
        {
            float t = now - _strokeFrom;
            return _def.Id == JobCatalogue.Crane ? TradeRules.Swing(t, _trade.Sweep) : TradeRules.Marker(t, _trade.Sweep);
        }

        private void BeginTiming(float now)
        {
            _onPress = () => Press(Time.time);
            _onSkip = OfferSkip;
            _onAway = StepAway;
            _pressKey = TradeCatalogue.Key + _def.Id + "_button";
            _stroke = 0;
            _worth = 0f;
            Say(JobText.T(TradeCatalogue.Key + _def.Id + "_obj"));
        }

        // A new stroke: its zone on the bar, the marker from the start.
        private void NewStroke(float now)
        {
            _zoneWidth = TradeRules.ZoneWidth(_trade, _skill);
            _zoneCentre = TradeRules.ZoneCentre(ref _dice, _zoneWidth);
            _strokeFrom = now;
            _timing = true;
            GameBar bar = Bar();
            if (bar != null)
            {
                Lines(null, null);   // the bar stands where the last two lines are
                bar.Zone(_zoneCentre, _zoneWidth, _zoneWidth * TradeRules.BestShare);
                bar.Show(true);
            }
            else
            {
                _zoneWords = JobText.T("reallife_trades_zone_words", "from", Percent(_zoneCentre - _zoneWidth * 0.5f), "to", Percent(_zoneCentre + _zoneWidth * 0.5f));
                Lines(_zoneWords, null);
            }
            _panelState = int.MinValue;
        }

        private void TickTiming(float now)
        {
            if (!Ctx.Stand.Ready)
            {
                _timing = false;
                _bar?.Show(false);
                _panel.Hide();
                Say(JobText.T("reallife_trades_take_place"));
                Mark(_post, JobText.T(Def.NameKey));
                return;
            }
            Unmark();
            if (!_timing && DoneCount < _units && now >= _quiet)
            {
                NewStroke(now);
                Say(JobText.T(TradeCatalogue.Key + _def.Id + "_obj"));
            }
            TimingPanel();
        }

        private void TimingPanel()
        {
            int state = (_timing ? 1 : 0) | (_stroke << 1) | (_misses << 8) | (DoneCount << 12);
            if (state != _panelState)
            {
                _panelState = state;
                string left = JobText.T("reallife_trades_stroke", "stroke", (_stroke + 1).ToString(CultureInfo.InvariantCulture), "count", _trade.Strokes.ToString(CultureInfo.InvariantCulture));
                _panel.Panel.Lines(JobText.T(TradeCatalogue.Key + _def.Id + "_panel"), left, _misses > 0 ? JobText.T("reallife_trades_again") : null,
                    _misses > 0 ? WorldPanel.Yellow() : _panel.White);
                _panel.Panel.Button(0, _pressKey, _onPress, _timing, true);
                _panel.Panel.Button(1, ButtonSkip, _onSkip, _clock != null && DoneCount < _units, false);
                _panel.Panel.Button(2, ButtonAway, _onAway, true, false);
                _panel.Panel.Button(3, null, null, false, false);
            }
            _panel.Show(Ctx.Player.transform, PanelLift);
        }

        // The panel's button, run by the game on its key or a click: how the marker stands against the zone now.
        private void Press(float now)
        {
            if (Over || !_timing || now < _quiet || PassingTime)
            {
                return;
            }
            float marker = MarkerNow(now);
            StrokeGrade grade = TradeRules.Press(marker, _zoneCentre, _zoneWidth);
            if (grade == StrokeGrade.Miss && _misses < TradeRules.MissesAllowed)
            {
                _misses++;
                JobText.Toast(NotificationType.Warning, TradeCatalogue.Key + _def.Id + "_miss", 3f);
                _strokeFrom = now;   // the same zone, from the start again
                _quiet = now + Debounce * 0.5f;
                _panelState = int.MinValue;
                return;
            }
            float worth = TradeRules.Worth(grade, _misses);
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Trades check (stroke): '" + _def.Id + "' pressed at " + marker.ToString("0.00", CultureInfo.InvariantCulture) + " against the zone "
                                 + (_zoneCentre - _zoneWidth * 0.5f).ToString("0.00", CultureInfo.InvariantCulture) + " to "
                                 + (_zoneCentre + _zoneWidth * 0.5f).ToString("0.00", CultureInfo.InvariantCulture) + ": " + grade + " after " + _misses + " miss(es), worth "
                                 + worth.ToString("0.00", CultureInfo.InvariantCulture) + ": PASS.");
            }
            _misses = 0;
            _worth += worth;
            _stroke++;
            _timing = false;
            _bar?.Show(false);
            _quiet = now + Debounce;
            if (_trade.Mode == TradeMode.Survey)
            {
                SurveyRead(worth, now);
                return;
            }
            if (_stroke >= _trade.Strokes)
            {
                float quality = TradeRules.Quality(_worth, _trade.Strokes);
                _stroke = 0;
                _worth = 0f;
                Laid(quality, now);
            }
            _panelState = int.MinValue;
        }
    }
}
