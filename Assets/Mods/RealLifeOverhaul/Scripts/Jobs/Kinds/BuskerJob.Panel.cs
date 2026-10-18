using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.Characters;
using Characters.EmojiSystem;
using Entities;
using Helpers;
using RealLifeOverhaul.GameUi;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Busking: the game's panel over the pitch and the figures on the shift card.</summary>
    public sealed partial class BuskerJob
    {
        private int _panelState = int.MinValue;
        private int _cardStrums = int.MinValue;

        // The songs to pick, or the strum while one plays; More has Step away and Let time pass.
        private void Panel(float now)
        {
            int songs = _songsWanted - _songs;
            int state;
            if (_step == Step.Song)
            {
                state = 1 | (_beatOpen ? 2 : 0) | (_hits << 4) | (_beats << 12);
            }
            else
            {
                state = 2 | (_more ? 4 : 0) | (songs << 4);
            }
            if (state != _panelState)
            {
                _panelState = state;
                if (_step == Step.Song)
                {
                    _panel.Panel.Lines(_beatOpen ? _panelNow : _panelWait, _panelStrums, JobText.T("reallife_job_busker_panel_hits", "hits", _hits.ToString(), "beats", _beats.ToString()),
                        _beatOpen ? WorldPanel.Green() : _panel.White);
                    _panel.Panel.Button(0, ButtonStrum, _onStrum, true, true);
                    _panel.Panel.Button(1, null, null, false, false);
                    _panel.Panel.Button(2, null, null, false, false);
                    _panel.Panel.Button(3, null, null, false, false);
                }
                else if (_more)
                {
                    _panel.Panel.Lines(_panelMore, _panelSongs, JobText.Number(_songs) + "/" + JobText.Number(_songsWanted), _panel.White);
                    _panel.Panel.Button(0, null, null, false, false);
                    _panel.Panel.Button(1, ButtonAway, _onAway, true, false);
                    _panel.Panel.Button(2, ButtonSkip, _onSkip, songs > 0, true);
                    _panel.Panel.Button(3, ButtonBack, _onMore, true, false);
                }
                else
                {
                    _panel.Panel.Lines(_panelPick, _panelSongs, JobText.Number(_songs) + "/" + JobText.Number(_songsWanted), _panel.White);
                    for (int i = 0; i < JobKeys.Choices; i++)
                    {
                        _panel.Panel.Button(i, SongKeys[i], _onSong[i], true, true);
                    }
                    _panel.Panel.Button(3, ButtonMore, _onMore, true, false);
                }
            }
            _panel.Show(_pitch + Vector3.up * PanelUp, Ctx.Player);
        }

        private void More()
        {
            if (Over || _step != Step.Pick || Time.time - _songOver < Debounce)
            {
                return;
            }
            _more = !_more;
            _panelState = int.MinValue;
        }

        private void StepAway()
        {
            if (Over || _step != Step.Pick || !_more || Time.time - _songOver < Debounce)
            {
                return;
            }
            Ctx.Stand.StepAway();   // the music stops and the shift waits on the pitch (LeftPitch)
            _panel.Hide();
            _panelState = int.MinValue;
            Show(_objBack);
        }

        private void Figures(float now)
        {
            bool clock = _clock.Working;
            if (clock)
            {
                Show(_objClock);
            }
            else if (_step != Step.ToPitch && !Ctx.Stand.Standing)
            {
                Show(_objBack);
            }
            _figures.Objective(Objective);
            _figures.Units(_songs + _clock.Done, _songsWanted);
            int strums = _step == Step.Song && !clock ? _hits | (_beats << 12) : int.MinValue;
            if (strums != _cardStrums)
            {
                _cardStrums = strums;
                _figures.Limit(strums == int.MinValue ? null : JobText.T("reallife_job_busker_card_strums", "hits", _hits.ToString(), "beats", _beats.ToString()), _panel.White);
            }
            _figures.Money(_tips + _clockTips, 0);
            _figures.Time(Mathf.Max(0, _songsWanted - _songs - _clock.Done) * _clock.MinutesEach);
        }
    }
}
