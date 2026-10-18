using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Characters.EmojiSystem;
using Helpers;
using RealLifeOverhaul.GameUi;
using Streets.Pedestrians;
using UI.Elements;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The drinks cart: the game's panel over the cart and the figures on the shift card.</summary>
    public sealed partial class VendorJob
    {
        // What is on screen, so a text is made only when it changes.
        private int _cardFigures = int.MinValue;
        private int _cardMoney = int.MinValue;
        private int _cardTime = int.MinValue;
        private int _cardState = int.MinValue;
        private int _panelState = int.MinValue;
        private int _panelUp;         // 0 not up, 1 up, 2 refused by another panel

        // The order and its buttons while somebody orders, else what the vendor can do. Texts are made only on a change; the
        // patience is one of the texts made at the start.
        private void Panel(float now)
        {
            bool ordering = _front >= 0 && _state[_front] == Guest.Ordering;
            bool hawk = !_hawkBanned && now >= _hawkReadyAt;
            int left = LeftToServe;
            int seconds = ordering ? Mathf.Clamp(Mathf.CeilToInt(_orderEnds - now), 0, _seconds.Length - 1) : 0;
            int state = (ordering ? 1 + _wants : 0) | (seconds << 3) | ((hawk ? 1 : 0) << 8) | ((_hawkBanned ? 1 : 0) << 9) | (left << 10);
            if (state != _panelState)
            {
                _panelState = state;
                if (ordering)
                {
                    _panel.Lines(_orders[_wants], _patienceWords, _seconds[seconds], seconds <= 2 ? WorldPanel.Red() : seconds <= 4 ? WorldPanel.Yellow() : WorldPanel.White());
                    for (int i = 0; i < JobKeys.Choices; i++)
                    {
                        _panel.Button(i, ItemKeys[i], _onDrink[i], true, true);
                    }
                }
                else
                {
                    _panel.Lines(left <= 2 ? _lastWords : _waitWords, _toComeWords, JobText.Number(left), WorldPanel.White());
                    _panel.Button(0, null, null, false, false);
                    _panel.Button(1, ButtonAway, _onAway, true, false);
                    _panel.Button(2, ButtonSkip, _onSkip, left > 0, true);
                }
                _panel.Button(3, _hawkBanned ? null : ButtonHawk, _onHawk, hawk, false);
            }
            // Over the cart; over the player's head while the cart is behind the camera.
            Vector3 over = _cartAt + Vector3.up * PanelUp;
            Camera camera = GameManager.GetMainCamera();
            if (camera != null && Vector3.Dot(over - camera.transform.position, camera.transform.forward) < 0.5f)
            {
                over = Ctx.Player.transform.position + Vector3.up * 2.1f;
            }
            _panel.Position = over;
            int up = _panel.Show() ? 1 : 2;
            if (up != _panelUp)
            {
                _panelUp = up;
                if (Meta.Dev.Verbose)
                {
                    Ctx.Logger?.Info("Jobs check (cart panel): " + (up == 1 ? "the game's own panel is up over the cart: PASS"
                        : "FAIL (the game's panel is showing another place; it is tried again five times a second while the player stands at the cart)") + ".");
                }
            }
        }

        private void HidePanel()
        {
            if (_panelUp != 0)
            {
                _panelUp = 0;
                _panelState = int.MinValue;
                _panel.Hide();
            }
        }

        private void StepAway()
        {
            if (Ctx == null || Time.time - _orderOver < Debounce)
            {
                return;
            }
            Ctx.Stand.StepAway();
            HidePanel();
        }

        // Five lines of the game's job panel, each written only when its figures change.
        private void Card(float now)
        {
            ShiftCard card = Ctx.Card;
            if (card == null)
            {
                return;
            }
            bool clock = _skip != null && _skip.Working;
            bool ordering = !clock && _front >= 0 && _state[_front] == Guest.Ordering;
            bool hawked = _hawkWords != null && now < _hawkWordsUntil;
            int state = clock ? 1 : !_open ? 2 : !Ctx.Stand.Standing ? 3 : hawked ? 4 : ordering ? 5 + _wants : LeftToServe <= 2 ? 9 : 10;
            if (state != _cardState)
            {
                _cardState = state;
                string words = clock ? JobText.T("reallife_job_vendor_card_clock") : !_open ? JobText.T("reallife_job_vendor_obj_post")
                    : !Ctx.Stand.Standing ? JobText.T("reallife_job_vendor_obj_back") : hawked ? _hawkWords : ordering ? _orders[_wants] : LeftToServe <= 2 ? _lastWords : _waitWords;
                card.Line(1, words, state == 3 ? WorldPanel.Yellow() : WorldPanel.White());
            }
            int served = _served + (clock ? _skipDone : 0);
            int figures = served | (_customers << 8) | (_misses << 16);
            if (figures != _cardFigures)
            {
                _cardFigures = figures;
                _text.Length = 0;
                _text.Append(JobText.T("reallife_job_vendor_card_served", "served", served.ToString(), "count", _customers.ToString())).Append(", ")
                    .Append(JobText.T("reallife_job_vendor_card_walkoffs", "misses", _misses.ToString(), "most", MissesAllowed.ToString()));
                card.Line(2, _text.ToString(), _misses >= MissesAllowed - 1 ? WorldPanel.Yellow() : WorldPanel.White());
            }
            int takings = Mathf.RoundToInt(_hand * PayEach) + _clockPay;
            int tips = Mathf.RoundToInt(_tips * Ctx.TipFactor);
            int money = takings | (tips << 16);
            if (money != _cardMoney)
            {
                _cardMoney = money;
                card.Line(3, JobText.T("reallife_job_vendor_card_money", "takings", JobText.Money(takings), "tips", JobText.Money(tips)), WorldPanel.White());
            }
            // Time on shift in steps of five game minutes, and about how long is left at the clock's pace.
            int on = Mathf.Max(0, Mathf.FloorToInt((TimeHelper.NowInMinutes() - _startMinutes) / 5f)) * 5;
            int left = Mathf.Max(0, LeftToServe - (clock ? _skipDone : 0)) * _minutesEach;
            int time = on | (left << 12);
            if (time != _cardTime)
            {
                _cardTime = time;
                card.Line(4, JobText.T("reallife_job_card_time", "on", Duration(on), "left", Duration(left)), WorldPanel.White());
            }
        }

        private static string Duration(int minutes)
        {
            return JobText.T("reallife_job_duration", "hours", (minutes / 60).ToString(), "minutes", (minutes % 60).ToString());
        }
    }
}
