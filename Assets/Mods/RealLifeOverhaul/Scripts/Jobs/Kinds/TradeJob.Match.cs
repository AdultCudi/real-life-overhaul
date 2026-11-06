using System;
using System.Globalization;
using RealLifeOverhaul.BuildTrades;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The electrician's and the plumber's game: what is asked (a wire's colour, a turn in the run) and the three answers on
    /// the game's panel's three keys, against the seconds shown on the card. The right one quickly is best; the wrong one, or
    /// none in time, is done again. "More" turns the panel to Let time pass and Step away, and back.
    /// </summary>
    public sealed partial class TradeJob
    {
        private const int Choices = 3;
        private readonly Action[] _onPick = new Action[Choices];
        private Action _onMore, _onBack;
        private int _right;            // the answer asked for now
        private int _ask;              // which of the trade's asks is shown (its words)
        private float _pickFrom, _pickSeconds;
        private bool _asking, _more;
        private int _picks;            // picks asked this shift: the time a pick has grows shorter
        private int _shownSeconds = int.MinValue;

        private void BeginMatch(float now)
        {
            for (int i = 0; i < Choices; i++)
            {
                int pick = i;
                _onPick[i] = () => Pick(pick, Time.time);
            }
            _onMore = () => { _more = true; _panelState = int.MinValue; };
            _onBack = () => { _more = false; _panelState = int.MinValue; };
            _onSkip = OfferSkip;
            _onAway = StepAway;
            _stroke = 0;
            _worth = 0f;
            Say(JobText.T(TradeCatalogue.Key + _def.Id + "_obj"));
        }

        private void NewPick(float now)
        {
            _right = TradeRules.PickRight(ref _dice);
            _ask = _right;   // each answer has its ask's words: a colour, a turn
            _pickSeconds = TradeRules.PickSeconds(_trade, _picks, _skill);
            _pickFrom = now;
            _asking = true;
            _picks++;
            _panelState = int.MinValue;
        }

        private void TickMatch(float now)
        {
            if (!Ctx.Stand.Ready)
            {
                _asking = false;
                _panel.Hide();
                Say(JobText.T("reallife_trades_take_place"));
                Mark(_post, JobText.T(Def.NameKey));
                return;
            }
            Unmark();
            if (!_asking && DoneCount < _units && now >= _quiet && !_more)
            {
                NewPick(now);
            }
            if (_asking && !_more && now - _pickFrom > _pickSeconds)
            {
                Pick(-1, now);   // too late: as a wrong answer
            }
            int seconds = _asking ? Mathf.Max(0, Mathf.CeilToInt(_pickSeconds - (now - _pickFrom))) : -1;
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                Lines(_asking ? JobText.T(TradeCatalogue.Key + _def.Id + "_ask_" + _ask) : null,
                    seconds >= 0 ? JobText.T("reallife_trades_seconds", "seconds", seconds.ToString(CultureInfo.InvariantCulture)) : null);
            }
            MatchPanel(seconds);
        }

        private void MatchPanel(int seconds)
        {
            int state = (_asking ? 1 : 0) | (_more ? 2 : 0) | (_ask << 2) | (_misses << 4) | ((seconds + 1) << 8) | (DoneCount << 16);
            if (state != _panelState)
            {
                _panelState = state;
                WorldPanel p = _panel.Panel;
                if (_more)
                {
                    p.Lines(JobText.T(TradeCatalogue.Key + _def.Id + "_panel"), null, null, _panel.White);
                    p.Button(0, "reallife_trades_button_back", _onBack, true, true);
                    p.Button(1, ButtonSkip, _onSkip, _clock != null && DoneCount < _units, false);
                    p.Button(2, ButtonAway, _onAway, true, false);
                    p.Button(3, null, null, false, false);
                }
                else
                {
                    p.Lines(_asking ? JobText.T(TradeCatalogue.Key + _def.Id + "_ask_" + _ask) : JobText.T(TradeCatalogue.Key + _def.Id + "_panel"),
                        JobText.T("reallife_trades_stroke", "stroke", (_stroke + 1).ToString(CultureInfo.InvariantCulture), "count", _trade.Strokes.ToString(CultureInfo.InvariantCulture)),
                        seconds >= 0 ? ShiftFigures.Clock(seconds) : null, seconds >= 0 && seconds <= 1 ? WorldPanel.Red() : _panel.White);
                    for (int i = 0; i < Choices; i++)
                    {
                        p.Button(i, TradeCatalogue.Key + _def.Id + "_pick_" + i, _onPick[i], _asking, true);
                    }
                    p.Button(3, "reallife_trades_button_more", _onMore, true, false);
                }
            }
            _panel.Show(Ctx.Player.transform, PanelLift);
        }

        // One of the three answers, or -1 for none in time.
        private void Pick(int chosen, float now)
        {
            if (Over || !_asking || (chosen >= 0 && now < _quiet) || PassingTime)
            {
                return;
            }
            StrokeGrade grade = TradeRules.Pick(chosen, _right, now - _pickFrom, _pickSeconds);
            if (grade == StrokeGrade.Miss && _misses < TradeRules.MissesAllowed)
            {
                _misses++;
                JobText.Toast(NotificationType.Warning, chosen < 0 ? "reallife_trades_too_late" : TradeCatalogue.Key + _def.Id + "_miss", 3f);
                _pickFrom = now;   // the same ask again, the time from the start
                _quiet = now + Debounce * 0.5f;
                _panelState = int.MinValue;
                return;
            }
            float worth = TradeRules.Worth(grade, _misses);
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Trades check (pick): '" + _def.Id + "' answered " + chosen + " for " + _right + " in "
                                 + (now - _pickFrom).ToString("0.0", CultureInfo.InvariantCulture) + " of " + _pickSeconds.ToString("0.0", CultureInfo.InvariantCulture)
                                 + " s: " + grade + ", worth " + worth.ToString("0.00", CultureInfo.InvariantCulture) + ": PASS.");
            }
            _misses = 0;
            _worth += worth;
            _stroke++;
            _asking = false;
            _quiet = now + Debounce;
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
