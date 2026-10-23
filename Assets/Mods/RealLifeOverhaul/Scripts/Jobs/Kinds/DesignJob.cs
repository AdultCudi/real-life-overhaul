using System;
using System.Globalization;
using Helpers;
using RealLifeOverhaul.BuildTrades;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Which of the practice's design work a shift is.</summary>
    public enum DesignWork
    {
        Architect,    // a commission: the floor plan, the facade, and the frame for an architect who is an engineer too
        Draughting,   // three sheets: one floor drawn up from a sketch, the stairs given
        Engineer      // two frames: columns placed so the floors hold
    }

    /// <summary>
    /// The architecture mini-game at the practice's desk (Scripts/BuildTrades/DesignRules), in the game's own interface: the
    /// list window (<see cref="DeskList"/>, the dispatcher's) holds the brief, the plan as it stands and a button for each
    /// place the next room can go, each facade element's choice, each column line; the game's panel over the desk has the
    /// stage's buttons; the shift card has the figures and, for the facade, the client's satisfaction on the game's bar. A
    /// stage is paid by how near it came to the best there was; an architect's commission goes in the trades' book, where a
    /// construction company reads its score. A design is a decision, so there is no clock. Cost: the job's tick; the list is
    /// written only when something is placed or changed.
    /// </summary>
    public sealed partial class DesignJob : Job
    {
        private enum Stage { Plan, Facade, Frame, Result }

        private const float StandReach = 1.4f;     // metres: the desk's stand
        private const float LeaveReach = 15f;      // metres from the desk at which the shift counts as walked off
        private const float PanelUp = 1.9f;        // metres over the desk
        private const float ResultSeconds = 5f;    // seconds a stage's result is shown before the next
        private const float Debounce = 0.5f;       // seconds after a change in which the panel's buttons do nothing
        private const int Sheets = 3, Frames = 2;  // a draughtsperson's sheets, an engineer's frames, a shift

        private readonly DesignWork _work;
        private readonly JobDef _def;
        private readonly TradeDef _trade;
        private readonly JobPanel _panel = new JobPanel("design panel");
        private ShiftFigures _figures;
        private DesignBrief _brief;
        private string _commission;        // the project's id, or the office's own
        private Vector3 _desk, _face;
        private Stage _stage;
        private float _stageEnd, _resultEnd, _quiet;
        private int _units, _done, _base;
        private bool _paid;
        private bool _withFrame;           // an architect who is an engineer too frames the commission
        private bool _listShut;            // the player closed the list; the panel's button opens it again
        private int _panelState = int.MinValue;
        private int _shownSeconds = int.MinValue;
        private string _resultWords;
        private Action _onSubmit, _onBack, _onLeave, _onList, _onBest;

        // The plan
        private readonly DesignRoom[] _order = new DesignRoom[12];
        private readonly int[] _starts = new int[12];
        private readonly DesignRoom[] _cells = new DesignRoom[DesignRules.Cells];
        private readonly int[] _places = new int[DesignRules.Cells];
        private int _count, _next, _placeCount, _lobby = -1;
        private DesignScore _score;

        // The facade
        private readonly int[] _choice = new int[DesignRules.Elements];
        private int _bestFacade;
        private GameBar _bar;
        private bool _barTried;

        // The frame
        private readonly bool[] _columns = new bool[DesignRules.Lines];
        private int _slab, _cheapest;

        public DesignJob(DesignWork work)
        {
            _work = work;
            string id = work == DesignWork.Architect ? JobCatalogue.Architect : work == DesignWork.Draughting ? JobCatalogue.Draughting : JobCatalogue.Engineer;
            _def = JobCatalogue.Find(id);
            _trade = TradeCatalogue.Find(id);
        }

        public override JobDef Def => _def;

        public override bool OwnsInteractKey => _panel.Up;

        public override bool QuitIsFailure => _done == 0 && _stage != Stage.Plan;

        /// <summary>The one building it is worked in: the practice's, while the player is on its floor.</summary>
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
            _withFrame = _work == DesignWork.Architect && TradesBridges.Holds(TradeCatalogue.Engineering);
            _units = _work == DesignWork.Architect ? (_withFrame ? 3 : 2) : _work == DesignWork.Draughting ? Sheets : Frames;
            if (!Ctx.FullShift && JobState.LeftToday(_def) < _units)
            {
                Refusal = "reallife_job_no_daily";
                return false;
            }
            Ctx.Stand.Place(_desk, _face, StandReach, ClipLibrary.Has(_trade.Clip) ? _trade.Clip : ClipLibrary.Has(_trade.ClipElse) ? _trade.ClipElse : null);
            _onSubmit = () => Submit(Time.time);
            _onBack = TakeBack;
            _onLeave = LeaveOut;
            _onList = () => { _listShut = false; _dirty = true; };
            _onBest = Best;
            _figures = new ShiftFigures(Ctx, Def, TradeCatalogue.Key + _def.Id + "_card_done");
            NewUnit(now);
            Ctx.Logger?.Info("Jobs: design work '" + _def.Id + "' begun at the practice's desk, " + _units + " unit(s)"
                             + (_commission != null ? ", for " + _commission : string.Empty) + (_withFrame ? ", the architect framing it too" : string.Empty) + ".");
            return true;
        }

        // A unit's work: a commission's first stage, or a sheet, or a frame.
        private void NewUnit(float now)
        {
            if (_work == DesignWork.Architect && _done == 0)
            {
                Commission next = DesignBook.Next;
                if (next != null)
                {
                    _brief = DesignRules.ForProject(next.Id, next.Storeys, next.Units, next.Budget);
                    _commission = next.Id;
                }
                else
                {
                    int own = DesignBook.TakeOwn();
                    _brief = DesignRules.Draw(TradeDice.Mix(own, TimeHelper.CurrentDay, 1, 0xA2C7E541u), false);
                    _commission = "own:" + own.ToString(CultureInfo.InvariantCulture);
                }
                BeginPlan(now);
            }
            else if (_work == DesignWork.Draughting)
            {
                _brief = DesignRules.Draw(TradeDice.Mix(TimeHelper.CurrentDay, Mathf.RoundToInt(TimeHelper.NowInMinutes()), _done, 0x1F3A5B7Du), true);
                BeginPlan(now);
            }
            else if (_work == DesignWork.Engineer)
            {
                _brief = DesignRules.Draw(TradeDice.Mix(TimeHelper.CurrentDay, Mathf.RoundToInt(TimeHelper.NowInMinutes()), _done, 0x5E9C0A13u), false);
                BeginFrame(now);
            }
        }

        public override void Tick(float now)
        {
            bool onFoot = !Ctx.Driving && !PlayerHelper.IsUsingVehicle;
            Ctx.Stand.Tick(Ctx.Player, onFoot, now);
            if (JobSpots.Flat(Ctx.PlayerPosition, _desk) > LeaveReach)
            {
                Ctx.Say("reallife_trades_left_desk", 8f);
                Finish(_done == 0 ? JobEnd.Quit : JobEnd.Done);
                return;
            }
            if (_stage == Stage.Result)
            {
                if (now >= _resultEnd)
                {
                    if (_done >= _units)
                    {
                        Finish(JobEnd.Done);
                        return;
                    }
                    if (_work == DesignWork.Architect)
                    {
                        if (_done == 1)
                        {
                            BeginFacade(now);
                        }
                        else
                        {
                            BeginFrame(now);
                        }
                    }
                    else
                    {
                        NewUnit(now);
                    }
                }
            }
            else if (now >= _stageEnd)
            {
                Ctx.Say("reallife_trades_design_time", 6f);
                Submit(now, true);   // out of time: the stage is judged as it stands
            }
            Draw(now);
        }

        // The panel, the list and the card: each written only when what it shows has changed.
        private void Draw(float now)
        {
            int seconds = _stage == Stage.Result ? -1 : Mathf.Max(0, Mathf.CeilToInt(_stageEnd - now));
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _panelState = int.MinValue;
            }
            int state = (int)_stage | (_next << 3) | ((seconds + 1) << 8) | (_listShut ? 1 << 16 : 0);
            if (state != _panelState)
            {
                _panelState = state;
                WorldPanel p = _panel.Panel;
                p.Lines(StageTitle(), StageLine(), seconds >= 0 ? ShiftFigures.Clock(seconds) : null, seconds >= 0 && seconds <= 10 ? WorldPanel.Red() : _panel.White);
                bool working = _stage != Stage.Result;
                p.Button(0, working ? SubmitKey() : null, working ? _onSubmit : null, working, true);
                bool plan = _stage == Stage.Plan;
                p.Button(1, plan ? "reallife_trades_design_back" : null, plan ? _onBack : null, plan && _next > 0, false);
                bool flat = plan && _next < _count && DesignRules.Flat(_order[_next]);
                p.Button(2, flat ? "reallife_trades_design_leave" : null, flat ? _onLeave : null, flat, false);
                if (_window.Failed)
                {
                    // No list window: the panel places each room at its best place, or stands a column on every wall.
                    bool best = (plan && _next < _count) || _stage == Stage.Frame;
                    p.Button(3, best ? (plan ? "reallife_trades_design_put_best" : "reallife_trades_design_frame_walls") : null, best ? _onBest : null, best, false);
                }
                else
                {
                    p.Button(3, working ? "reallife_trades_design_list" : null, working ? _onList : null, working && _listShut, false);
                }
            }
            _panel.Show(_desk + Vector3.up * PanelUp);
            if (_dirty)
            {
                _dirty = false;
                WriteList();
            }
            Figures();
        }

        private bool _dirty;

        private string StageTitle()
        {
            switch (_stage)
            {
                case Stage.Plan: return _next < _count ? JobText.T("reallife_trades_design_place", "room", Room(_order[_next])) : JobText.T("reallife_trades_design_plan_done");
                case Stage.Facade: return JobText.T("reallife_trades_design_facade");
                case Stage.Frame: return JobText.T("reallife_trades_design_frame");
                default: return _resultWords ?? string.Empty;
            }
        }

        private string StageLine()
        {
            return JobText.T("reallife_trades_design_client", "client", JobText.T(TradeCatalogue.Key + "client_" + _brief.Client), "storeys",
                _brief.Storeys.ToString(CultureInfo.InvariantCulture));
        }

        private string SubmitKey()
        {
            return _stage == Stage.Plan ? "reallife_trades_design_submit_plan" : _stage == Stage.Facade ? "reallife_trades_design_submit_facade" : "reallife_trades_design_submit_frame";
        }

        private void Figures()
        {
            if (_figures == null)
            {
                return;
            }
            _figures.Objective(Objective);
            _figures.Units(_done, _units);
            _figures.Limit(JobText.T("reallife_job_card_earned", "earned", JobText.Money(_base)), _panel.White);
            if (_stage == Stage.Facade && Bar() != null)
            {
                return;   // the client's satisfaction stands on the bar there
            }
            ShiftCard card = Ctx.Card;
            if (card != null)
            {
                card.Line(3, _stage == Stage.Plan ? JobText.T("reallife_trades_design_rooms", "left", (_count - _next).ToString(CultureInfo.InvariantCulture))
                    : _stage == Stage.Frame ? FrameLine() : string.Empty, _panel.White);
                card.Line(4, string.Empty, _panel.White);
            }
        }

        private GameBar Bar()
        {
            if (!_barTried)
            {
                _barTried = true;
                ShiftCard card = Ctx.Card;
                _bar = card != null ? card.Bar() : null;
                if (Meta.Dev.Verbose)
                {
                    Ctx.Logger?.Info("Trades check (satisfaction meter): " + (_bar != null ? "the client's satisfaction on the game's bar on the shift card: PASS"
                        : "FAIL (the game's bar could not be copied; the satisfaction is given in figures in the list)") + ".");
                }
            }
            return _bar;
        }

        // The stage's button, or its time run out: the stage judged as it stands, paid, and the result shown.
        private void Submit(float now, bool timedOut = false)
        {
            if (Over || _stage == Stage.Result || (!timedOut && now < _quiet))
            {
                return;
            }
            float quality;
            int score;
            switch (_stage)
            {
                case Stage.Plan:
                    for (int i = _next; i < _count; i++)
                    {
                        _starts[i] = -1;   // what was not placed is left out
                    }
                    _next = _count;
                    _score = DesignRules.Score(_brief, _order, _count, _starts, _cells, _lobby);
                    score = _score.Plan;
                    quality = DesignRules.Quality(score, 100);
                    _resultWords = JobText.T("reallife_trades_design_plan_result", "fit", Whole(_score.Fit), "light", Whole(_score.Light), "flow", Whole(_score.Flow), "cost", Whole(_score.Cost));
                    break;
                case Stage.Facade:
                    _score.Facade = DesignRules.Satisfaction(_brief, _choice, out int cost);
                    _score.CostPermille = DesignRules.CostWith(_brief, _score.CostPermille, cost);
                    score = _score.Facade;
                    quality = DesignRules.Quality(score, _bestFacade);
                    _bar?.Show(false);
                    _resultWords = JobText.T("reallife_trades_design_facade_result", "satisfaction", Whole(score), "best", Whole(_bestFacade));
                    break;
                default:
                    score = DesignRules.FrameScore(_columns, _slab, _brief.Storeys, _cheapest);
                    _score.Structure = score;
                    quality = DesignRules.Quality(score, 100);
                    _resultWords = JobText.T(score > 0 ? "reallife_trades_design_frame_result" : "reallife_trades_design_frame_falls", "score", Whole(score));
                    break;
            }
            int pay = TradeRules.UnitPay(Def.UnitPay * Ctx.Rate, quality);
            _base += pay;
            _done++;
            Ctx.Logger?.Info("Trades check (design score): '" + _def.Id + "' " + _stage + " judged " + score + " (quality " + quality.ToString("0.00", CultureInfo.InvariantCulture)
                             + (timedOut ? ", out of time" : string.Empty) + "), paid " + pay + ": PASS.");
            JobText.Toast(quality >= 0.85f ? NotificationType.Success : quality >= 0.5f ? NotificationType.Info : NotificationType.Warning, "reallife_trades_design_stage_paid",
                "result", _resultWords, "amount", JobText.Money(pay), 8f);
            Show(_resultWords);
            _stage = Stage.Result;
            _resultEnd = now + ResultSeconds;
            _quiet = now + Debounce;
            _dirty = true;
            _panelState = int.MinValue;
            if (_work == DesignWork.Architect && (_done >= _units))
            {
                Record();
            }
        }

        // A commission finished: its score in the trades' book, for the construction company that asked for it.
        private void Record()
        {
            _score.Total = DesignRules.Total(_score);
            if (!Ctx.Test)
            {
                DesignBook.Finished(_commission, TimeHelper.CurrentDay, _score);
            }
            Ctx.Logger?.Info("Trades check (design book): " + _commission + " finished: " + _score.Write() + (Ctx.Test ? " (a test: not kept)" : string.Empty) + ": PASS.");
            JobText.Toast(NotificationType.Success, "reallife_trades_design_total", "total", Whole(_score.Total), 10f);
        }

        private static string Whole(int value) => value.ToString(CultureInfo.InvariantCulture);

        public override void End(JobEnd why)
        {
            _bar?.Show(false);
            _panel.Hide();
            CloseList();
            Ctx.Stand.Clear();
            if (_work == DesignWork.Architect && _done > 0 && _done < _units && why != JobEnd.Fault)
            {
                Record();   // a commission left part done: what was designed is the design
            }
            if (_done > 0 && !Ctx.Test && why != JobEnd.Fault)
            {
                TradesBridges.Learnt(_trade.Learns, _done * _trade.LearnEach);
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
            if (_paid || _done <= 0)
            {
                return false;
            }
            slip = new PaySlip { Units = _done, Base = _base };
            return true;
        }
    }
}
