using System;
using System.Globalization;
using System.Text;
using Extensions;
using Helpers;
using RealLifeOverhaul.BuildTrades;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The estimator at the practice's desk (Scripts/BuildTrades/TenderRules): three tenders against the clock, each five items
    /// priced on the game's panel over the desk by picking the rate that is the last one paid moved by the market, then a
    /// margin; the bill so far in the game's list window. Won if it comes in under the rival builder's bid; paid by how true
    /// its rates were, with a bonus for a tender won at a profit. Cost: the job's tick; the list written on a change.
    /// </summary>
    public sealed class TenderJob : Job
    {
        private enum Step { Pricing, Margin, Result }

        private const int Tenders = 3;
        private const float StandReach = 1.4f;
        private const float LeaveReach = 15f;
        private const float PanelUp = 1.9f;
        private const float ResultSeconds = 6f;
        private const float Debounce = 0.6f;
        private const int BackId = 0;

        private readonly JobDef _def = JobCatalogue.Find(JobCatalogue.Estimator);
        private readonly TradeDef _trade = TradeCatalogue.Find(JobCatalogue.Estimator);
        private readonly JobPanel _panel = new JobPanel("tender panel");
        private readonly TenderItem[] _items = new TenderItem[TenderRules.Items];
        private readonly int[] _picks = new int[TenderRules.Items];
        private readonly object[] _rateArgs = new object[3];
        private readonly object[] _marginArgs = new object[3];
        private readonly Action[] _onRate = new Action[3];
        private readonly Action[] _onMargin = new Action[3];
        private readonly DeskList _window = new DeskList("RealLifeOverhaul Tender", 900f, 110f);
        private readonly StringBuilder _text = new StringBuilder(400);
        private Action<int> _onPick;
        private Action _onShut, _onBack;
        private ShiftFigures _figures;
        private Vector3 _desk, _face;
        private Step _step;
        private uint _seed;
        private int _tender, _item, _base, _won;
        private float _stepEnd, _quiet;
        private bool _paid, _listShut, _dirty;
        private int _panelState = int.MinValue;
        private string _result;

        public override JobDef Def => _def;

        public override bool OwnsInteractKey => _panel.Up;

        public override bool QuitIsFailure => _tender == 0 && _item > 0;

        public override bool WorksInside(Buildings.Building building) => TradeOffice.Here;

        protected override bool OnBegin(float now)
        {
            if (!Settings.Trades || !Settings.TradesOffice)
            {
                Refusal = "reallife_trades_off";
                return false;
            }
            if (!TradeOffice.Desk(out _desk, out _face))
            {
                Refusal = "reallife_trades_no_office";
                return false;
            }
            if (!Ctx.Test && !TradesBridges.Holds(_trade.Needs))
            {
                Refusal = _trade.Needs.LacksKey;
                return false;
            }
            Ctx.Stand.Place(_desk, _face, StandReach, ClipLibrary.Has(_trade.Clip) ? _trade.Clip : ClipLibrary.Has(_trade.ClipElse) ? _trade.ClipElse : null);
            for (int i = 0; i < 3; i++)
            {
                int pick = i;
                _onRate[i] = () => Rate(pick, Time.time);
                _onMargin[i] = () => Margin(pick, Time.time);
                _marginArgs[i] = new { margin = TenderRules.Margins[i].ToString(CultureInfo.InvariantCulture) };
            }
            _onPick = id => { if (id == BackId) { Back(); } };
            _onShut = () => { _listShut = true; };
            _onBack = Back;
            _figures = new ShiftFigures(Ctx, Def, "reallife_trades_estimator_card_done");
            NewTender(now);
            Ctx.Logger?.Info("Jobs: estimator's shift begun at the practice's desk: " + Tenders + " tenders of " + TenderRules.Items + " items.");
            return true;
        }

        private void NewTender(float now)
        {
            _seed = TradeDice.Mix(TimeHelper.CurrentDay, Mathf.RoundToInt(TimeHelper.NowInMinutes()), _tender, 0x6C8E9CF5u);
            TenderRules.Draw(_seed, _items);
            for (int i = 0; i < _picks.Length; i++)
            {
                _picks[i] = -1;
            }
            _item = 0;
            _step = Step.Pricing;
            _stepEnd = now + _trade.Seconds;
            _quiet = now + Debounce;
            _listShut = false;
            _dirty = true;
            _panelState = int.MinValue;
            Show(JobText.T("reallife_trades_estimator_obj", "tender", (_tender + 1).ToString(CultureInfo.InvariantCulture), "count", Tenders.ToString(CultureInfo.InvariantCulture)));
        }

        public override void Tick(float now)
        {
            Ctx.Stand.Tick(Ctx.Player, !Ctx.Driving && !PlayerHelper.IsUsingVehicle, now);
            if (JobSpots.Flat(Ctx.PlayerPosition, _desk) > LeaveReach)
            {
                Ctx.Say("reallife_trades_left_desk", 8f);
                Finish(_tender == 0 ? JobEnd.Quit : JobEnd.Done);
                return;
            }
            if (_step == Step.Result)
            {
                if (now >= _stepEnd)
                {
                    if (_tender >= Tenders)
                    {
                        Finish(JobEnd.Done);
                        return;
                    }
                    NewTender(now);
                }
            }
            else if (now >= _stepEnd)
            {
                Ctx.Say("reallife_trades_estimator_time", 6f);
                Close(TenderRules.Margins[1], now);   // out of time: what is not priced goes in at its dearest, the usual margin
                return;
            }
            Draw(now);
        }

        private void Draw(float now)
        {
            int seconds = _step == Step.Result ? -1 : Mathf.Max(0, Mathf.CeilToInt(_stepEnd - now));
            int state = (int)_step | (_item << 2) | ((seconds + 1) << 6) | (_tender << 14);
            if (state != _panelState)
            {
                _panelState = state;
                WorldPanel p = _panel.Panel;
                if (_step == Step.Pricing && _item < TenderRules.Items)
                {
                    TenderItem item = _items[_item];
                    p.Lines(JobText.T(TradeCatalogue.Key + "tender_item_" + item.Kind, "quantity", item.Quantity.ToString(CultureInfo.InvariantCulture)),
                        JobText.T("reallife_trades_tender_last", "rate", Cents(item.LastCents), "move", Signed(item.MovePercent)),
                        ShiftFigures.Clock(seconds), seconds <= 10 ? WorldPanel.Red() : _panel.White);
                    for (int i = 0; i < 3; i++)
                    {
                        _rateArgs[i] = new { rate = Cents(item.Rate(i)) };
                        p.Button(i, "reallife_trades_tender_rate", _rateArgs[i], _onRate[i], true, true);
                    }
                    p.Button(3, "reallife_trades_tender_back", null, _onBack, _item > 0, false);
                }
                else if (_step == Step.Margin)
                {
                    p.Lines(JobText.T("reallife_trades_tender_margin"), JobText.T("reallife_trades_tender_cost", "cost", Money(TenderRules.Bid(_items, _picks, 0))),
                        ShiftFigures.Clock(seconds), seconds <= 10 ? WorldPanel.Red() : _panel.White);
                    for (int i = 0; i < 3; i++)
                    {
                        p.Button(i, "reallife_trades_tender_margin_button", _marginArgs[i], _onMargin[i], true, true);
                    }
                    p.Button(3, "reallife_trades_tender_back", null, _onBack, true, false);
                }
                else
                {
                    p.Lines(_result ?? string.Empty, null, null, _panel.White);
                    for (int i = 0; i < 4; i++)
                    {
                        p.Button(i, null, null, false, false);
                    }
                }
            }
            _panel.Show(_desk + Vector3.up * PanelUp);
            if (_dirty)
            {
                _dirty = false;
                Bill();
            }
            if (_figures != null)
            {
                _figures.Objective(Objective);
                _figures.Units(_tender, Tenders);
                _figures.Limit(JobText.T("reallife_job_card_earned", "earned", JobText.Money(_base)), _panel.White);
                _figures.Money(_base, 0);
                _figures.Time(-1);
            }
        }

        private static string Cents(int cents) => (cents / 100f).ToCurrencyFormat();

        private static string Money(long cents) => (cents / 100f).ToCurrencyFormat();

        private static string Signed(int percent) => (percent >= 0 ? "+" : string.Empty) + percent.ToString(CultureInfo.InvariantCulture) + "%";

        private void Rate(int pick, float now)
        {
            if (Over || _step != Step.Pricing || _item >= TenderRules.Items || now < _quiet)
            {
                return;
            }
            _picks[_item] = pick;
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Trades check (tender item): " + (_item + 1) + " priced at " + _items[_item].Rate(pick) + " against the true " + _items[_item].TrueCents
                                 + " (last " + _items[_item].LastCents + ", market " + Signed(_items[_item].MovePercent) + "), worth "
                                 + TenderRules.Worth(_items[_item], pick).ToString("0.0", CultureInfo.InvariantCulture) + ": PASS.");
            }
            _item++;
            if (_item >= TenderRules.Items)
            {
                _step = Step.Margin;
            }
            _quiet = now + Debounce * 0.5f;
            _dirty = true;
            _panelState = int.MinValue;
        }

        private void Back()
        {
            if (Over || _step == Step.Result)
            {
                return;
            }
            if (_step == Step.Margin)
            {
                _step = Step.Pricing;
            }
            if (_item > 0)
            {
                _item--;
                _picks[_item] = -1;
            }
            _dirty = true;
            _panelState = int.MinValue;
        }

        private void Margin(int pick, float now)
        {
            if (Over || _step != Step.Margin || now < _quiet)
            {
                return;
            }
            Close(TenderRules.Margins[pick], now);
        }

        // The tender goes in: won or lost against the rival, paid by how true its rates were.
        private void Close(int margin, float now)
        {
            long cost = TenderRules.TrueTotal(_items);
            long bid = TenderRules.Bid(_items, _picks, margin);
            long rival = TenderRules.Rival(_seed, cost);
            bool won = bid < rival;
            float quality = TenderRules.Quality(_items, _picks, won);
            int pay = TradeRules.UnitPay(Def.UnitPay * Ctx.Rate, quality);
            if (TenderRules.Earns(bid, rival, cost))
            {
                pay += Mathf.RoundToInt(Def.UnitPay * Ctx.Rate * TenderRules.WinBonus);
            }
            _base += pay;
            _won += won ? 1 : 0;
            _tender++;
            _result = JobText.T(won ? (bid >= cost ? "reallife_trades_tender_won" : "reallife_trades_tender_won_loss") : "reallife_trades_tender_lost",
                "bid", Money(bid), "rival", Money(rival));
            JobText.Toast(won && bid >= cost ? NotificationType.Success : NotificationType.Info, "reallife_trades_tender_paid", "result", _result, "amount", JobText.Money(pay), 8f);
            Ctx.Logger?.Info("Trades check (tender): bid " + bid + " against the rival's " + rival + " and the true cost " + cost + " (margin " + margin + "%): "
                             + (won ? "won" : "lost") + ", quality " + quality.ToString("0.00", CultureInfo.InvariantCulture) + ", paid " + pay + ": PASS.");
            Show(_result);
            _step = Step.Result;
            _stepEnd = now + ResultSeconds;
            _dirty = true;
            _panelState = int.MinValue;
        }

        // The bill so far, in the game's list window: each item with the rate picked; Take one back.
        private void Bill()
        {
            if (_listShut || _step == Step.Result)
            {
                _window.Close();
                return;
            }
            TravelList list = _window.Take(this);
            if (list == null)
            {
                return;   // the panel over the desk has every choice
            }
            list.Begin(JobText.T("reallife_trades_tender_title", "tender", (_tender + 1).ToString(CultureInfo.InvariantCulture)), JobText.T("reallife_trades_tender_info"));
            _text.Length = 0;
            for (int i = 0; i < TenderRules.Items; i++)
            {
                TenderItem item = _items[i];
                _text.Append(i == _item && _step == Step.Pricing ? "> " : string.Empty)
                    .Append(JobText.T(TradeCatalogue.Key + "tender_item_" + item.Kind, "quantity", item.Quantity.ToString(CultureInfo.InvariantCulture))).Append(": ")
                    .Append(_picks[i] >= 0 ? Cents(item.Rate(_picks[i])) : JobText.T("reallife_trades_tender_unpriced"));
                if (i < TenderRules.Items - 1)
                {
                    _text.Append('\n');
                }
            }
            list.AddNote(JobText.T("reallife_trades_tender_bill"), _text.ToString(), list.HeightFor(_text.Length + 80, false));
            list.Add(BackId, JobText.T("reallife_trades_design_back_name"), JobText.T("reallife_trades_tender_back_detail"), "reallife_trades_design_back", _item > 0);
            list.Open(_onPick, _onShut);
            list.Locked = false;
        }

        public override void End(JobEnd why)
        {
            _panel.Hide();
            _window.Close();
            _window.Destroy(e => Ctx.Logger?.Error(e));
            Ctx.Stand.Clear();
            if (_tender > 0 && !Ctx.Test && why != JobEnd.Fault)
            {
                TradesBridges.Learnt(_trade.Learns, _tender * _trade.LearnEach);
            }
            if (why == JobEnd.Fault || !Owed(out PaySlip slip))
            {
                return;
            }
            _paid = true;
            Ctx.Pay(this, slip, "reallife_trades_slip");
        }

        protected override bool Owed(out PaySlip slip)
        {
            slip = default;
            if (_paid || _tender <= 0)
            {
                return false;
            }
            slip = new PaySlip { Units = _tender, Base = _base };
            return true;
        }
    }
}
