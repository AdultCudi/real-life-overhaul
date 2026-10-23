using System;
using System.Globalization;
using System.Text;
using RealLifeOverhaul.BuildTrades;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The design's stages in the game's list window: the brief and the plan as it stands as notes at the top, then an entry
    /// for each place the next room can go (or each facade element, or each column line) with its button. Written when
    /// something is placed or changed; the panel over the desk has the stage's own buttons. Without the window (it could not
    /// be copied) the panel's buttons place each room at its best place in turn, and one line says so.
    /// </summary>
    public sealed partial class DesignJob
    {
        private const int LeaveId = 100, BackId = 101, ElementId = 200, LineId = 300, SlabId = 320;

        private readonly DeskList _window = new DeskList("RealLifeOverhaul Design", 1000f, 124f);
        private readonly StringBuilder _text = new StringBuilder(320);
        private Action<int> _onPick;
        private Action _onShut;
        private bool _listSaid;

        // ---- the stages begun ----

        private void BeginPlan(float now)
        {
            Array.Clear(_cells, 0, _cells.Length);
            _lobby = -1;
            _count = DesignRules.Order(_brief, _order);
            for (int i = 0; i < _starts.Length; i++)
            {
                _starts[i] = -1;
            }
            if (_brief.Simple)
            {
                _cells[DesignRules.SimpleStairs] = DesignRoom.Stairs;   // the sketch's stairs, drawn already
            }
            _next = 0;
            Skip();
            _stage = Stage.Plan;
            _stageEnd = now + _trade.Seconds + (_brief.Simple ? 0f : 15f);
            _quiet = now + Debounce;
            _listShut = false;
            _dirty = true;
            _panelState = int.MinValue;
            Show(JobText.T(_brief.Simple ? "reallife_trades_draughtsperson_obj" : "reallife_trades_architect_obj"));
        }

        private void BeginFacade(float now)
        {
            for (int e = 0; e < DesignRules.Elements; e++)
            {
                _choice[e] = 0;
            }
            _bestFacade = DesignRules.BestSatisfaction(_brief);
            _stage = Stage.Facade;
            _stageEnd = now + _trade.Seconds * 0.8f;
            _quiet = now + Debounce;
            _listShut = false;
            _dirty = true;
            _panelState = int.MinValue;
            Show(JobText.T("reallife_trades_design_facade_obj"));
            Meter();
        }

        private void BeginFrame(float now)
        {
            for (int i = 0; i < _columns.Length; i++)
            {
                _columns[i] = false;
            }
            _slab = 0;
            _cheapest = DesignRules.CheapestFrame(_brief.Storeys);
            _stage = Stage.Frame;
            _stageEnd = now + _trade.Seconds * 0.8f;
            _quiet = now + Debounce;
            _listShut = false;
            _dirty = true;
            _panelState = int.MinValue;
            Show(JobText.T("reallife_trades_design_frame_obj"));
        }

        // A room with nowhere left to go is left out: the plan moves on to the next.
        private void Skip()
        {
            while (_next < _count && DesignRules.Places(_order[_next], _cells, _places) == 0)
            {
                _starts[_next++] = -1;
            }
            _placeCount = _next < _count ? DesignRules.Places(_order[_next], _cells, _places) : 0;
        }

        // ---- the player's choices ----

        private void Picked(int id)
        {
            if (Over || Time.time < _quiet)
            {
                return;
            }
            if (_stage == Stage.Plan)
            {
                if (id == LeaveId)
                {
                    LeaveOut();
                }
                else if (id == BackId)
                {
                    TakeBack();
                }
                else if (id >= 0 && id < _placeCount && _next < _count)
                {
                    Place(_places[id]);
                }
            }
            else if (_stage == Stage.Facade && id >= ElementId && id < ElementId + DesignRules.Elements)
            {
                int e = id - ElementId;
                _choice[e] = (_choice[e] + 1) % DesignRules.Options;
                Meter();
                _dirty = true;
            }
            else if (_stage == Stage.Frame)
            {
                if (id == SlabId)
                {
                    _slab = (_slab + 1) % DesignRules.Slabs;
                }
                else if (id > LineId && id < LineId + DesignRules.Lines - 1)
                {
                    _columns[id - LineId] = !_columns[id - LineId];
                }
                _dirty = true;
            }
            _panelState = int.MinValue;
        }

        private void Place(int at)
        {
            DesignRules.Put(_order[_next], at, _cells, ref _lobby);
            _starts[_next++] = at;
            Skip();
            _dirty = true;
            _quiet = Time.time + 0.2f;
            if (_next >= _count)
            {
                Show(JobText.T("reallife_trades_design_plan_done"));
            }
        }

        // The panel's Take one back: the last room placed comes off the plan (rooms left out come back too).
        private void TakeBack()
        {
            if (Over || _stage != Stage.Plan || _next <= 0)
            {
                return;
            }
            do
            {
                _next--;
                if (_starts[_next] >= 0)
                {
                    DesignRules.Take(_order[_next], _starts[_next], _cells, ref _lobby);
                    _starts[_next] = -1;
                    break;
                }
            }
            while (_next > 0);
            _placeCount = DesignRules.Places(_order[_next], _cells, _places);
            _dirty = true;
            _panelState = int.MinValue;
        }

        // The panel's Leave it out: this flat is not in the plan (its fit is lost, and its cost saved).
        private void LeaveOut()
        {
            if (Over || _stage != Stage.Plan || _next >= _count || !DesignRules.Flat(_order[_next]))
            {
                return;
            }
            _starts[_next++] = -1;
            Skip();
            _dirty = true;
            _panelState = int.MinValue;
        }

        // Without the list window: the next room at its best place (the most daylight, then the fewest steps to a core, then the
        // first), or a column on every wall of the frame.
        private void Best()
        {
            if (Over || Time.time < _quiet)
            {
                return;
            }
            if (_stage == Stage.Frame)
            {
                for (int i = 1; i < DesignRules.Lines - 1; i++)
                {
                    _columns[i] = DesignRules.OnWall(i);
                }
                _dirty = true;
                _panelState = int.MinValue;
                return;
            }
            if (_stage != Stage.Plan || _next >= _count || _placeCount == 0)
            {
                return;
            }
            DesignRoom room = _order[_next];
            int best = 0, bestValue = int.MinValue;
            for (int i = 0; i < _placeCount; i++)
            {
                int at = _places[i], lit = 0, steps = 99;
                for (int k = 0; k < DesignRules.Length(room); k++)
                {
                    lit += DesignRules.Lit(at + k) ? 1 : 0;
                    int d = DesignRules.ToCore(at + k, _cells);
                    steps = d < steps ? d : steps;
                }
                bool core = room == DesignRoom.Stairs || room == DesignRoom.Lift;
                int value = core ? (DesignRules.Lit(at) ? 0 : 100) - Math.Min(steps, 9) : lit * 100 - Math.Min(steps, 9) * 10;
                if (value > bestValue)
                {
                    bestValue = value;
                    best = i;
                }
            }
            Place(_places[best]);
            _panelState = int.MinValue;
        }

        // The client's satisfaction on the card's bar, as the choices stand.
        private void Meter()
        {
            int satisfaction = DesignRules.Satisfaction(_brief, _choice, out _);
            GameBar bar = Bar();
            if (bar != null)
            {
                ShiftCard card = Ctx.Card;
                card?.Line(3, string.Empty, _panel.White);
                card?.Line(4, string.Empty, _panel.White);
                bar.Fill(satisfaction / 100f);
                bar.Show(true);
            }
        }

        private string FrameLine()
        {
            int span = DesignRules.LongestSpan(_columns);
            int most = DesignRules.SpanSteps(_slab);
            return JobText.T(span <= most ? "reallife_trades_design_span_ok" : "reallife_trades_design_span_long", "span", Metres(span * DesignRules.LineStep),
                "most", Metres(most * DesignRules.LineStep));
        }

        private static string Metres(float m) => m.ToString("0.#", CultureInfo.InvariantCulture);

        private static string Room(DesignRoom room) => JobText.T(TradeCatalogue.Key + "room_" + (int)room);

        // ---- the list window ----

        private void WriteList()
        {
            if (_listShut || _stage == Stage.Result)
            {
                if (_stage == Stage.Result)
                {
                    _window.Close();
                }
                return;
            }
            TravelList list = _window.Take(this);
            if (list == null)
            {
                if (!_listSaid)
                {
                    _listSaid = true;
                    Ctx.Logger?.Info("Trades check (design list): the game's list could not be made; each room goes to its best place on the panel's button: FAIL.");
                }
                return;
            }
            if (_onPick == null)
            {
                _onPick = Picked;
                _onShut = () => { _listShut = true; _panelState = int.MinValue; };
            }
            switch (_stage)
            {
                case Stage.Plan: PlanList(list); break;
                case Stage.Facade: FacadeList(list); break;
                default: FrameList(list); break;
            }
            list.Open(_onPick, _onShut);
            list.Locked = false;
            if (Meta.Dev.Verbose && !_listSaid)
            {
                _listSaid = true;
                Ctx.Logger?.Info("Trades check (design list): the game's list is open for the " + _stage + ", " + list.Made + " entries made so far: PASS.");
            }
        }

        private void CloseList()
        {
            _window.Close();
            _window.Destroy(e => Ctx.Logger?.Error(e));
        }

        private void PlanList(TravelList list)
        {
            list.Begin(JobText.T("reallife_trades_design_plan_title"), JobText.T("reallife_trades_design_brief", "client", JobText.T(TradeCatalogue.Key + "client_" + _brief.Client),
                "storeys", _brief.Storeys.ToString(CultureInfo.InvariantCulture), "flats", Flats()));
            _text.Length = 0;
            for (int r = 0; r < DesignRules.Rows; r++)
            {
                _text.Append(JobText.T(TradeCatalogue.Key + "row_" + r)).Append(": ");
                for (int c = 0; c < DesignRules.Cols; c++)
                {
                    DesignRoom room = _cells[r * DesignRules.Cols + c];
                    _text.Append(c > 0 ? " | " : string.Empty).Append(room == DesignRoom.Empty ? JobText.T("reallife_trades_room_free") : Room(room));
                }
                _text.Append('\n');
            }
            if (!_brief.Simple)
            {
                _text.Append(_lobby >= 0 ? JobText.T("reallife_trades_design_lobby_at", "bay", (_lobby % DesignRules.Cols + 1).ToString(CultureInfo.InvariantCulture))
                    : JobText.T("reallife_trades_design_lobby_none"));
            }
            else if (_text.Length > 0)
            {
                _text.Length--;
            }
            list.AddNote(JobText.T("reallife_trades_design_plan_now"), _text.ToString(), list.HeightFor(_text.Length + 60, false));
            if (_next < _count)
            {
                DesignRoom room = _order[_next];
                for (int i = 0; i < _placeCount; i++)
                {
                    list.Add(i, PlaceName(room, _places[i]), PlaceDetail(room, _places[i]), "reallife_trades_design_put", true);
                }
                if (DesignRules.Flat(room))
                {
                    list.Add(LeaveId, JobText.T("reallife_trades_design_leave_name", "room", Room(room)), JobText.T("reallife_trades_design_leave_detail"), "reallife_trades_design_leave", true);
                }
            }
            list.Add(BackId, JobText.T("reallife_trades_design_back_name"), JobText.T("reallife_trades_design_back_detail"), "reallife_trades_design_back", _next > 0);
        }

        private string Flats()
        {
            return JobText.T("reallife_trades_design_flats", "studios", _brief.Studios.ToString(CultureInfo.InvariantCulture), "twobeds",
                _brief.TwoBeds.ToString(CultureInfo.InvariantCulture), "families", _brief.Families.ToString(CultureInfo.InvariantCulture))
                   + (_brief.Lift ? " " + JobText.T("reallife_trades_design_lift") : string.Empty);
        }

        private static string PlaceName(DesignRoom room, int at)
        {
            int row = at / DesignRules.Cols, bay = at % DesignRules.Cols + 1;
            if (room == DesignRoom.Lobby)
            {
                return JobText.T("reallife_trades_place_lobby", "bay", bay.ToString(CultureInfo.InvariantCulture));
            }
            int last = bay + DesignRules.Length(room) - 1;
            return JobText.T(last > bay ? "reallife_trades_place_bays" : "reallife_trades_place_bay", "row", JobText.T(TradeCatalogue.Key + "row_" + row), "bay",
                bay.ToString(CultureInfo.InvariantCulture), "last", last.ToString(CultureInfo.InvariantCulture));
        }

        // What a place gives the room: daylight, and its steps to the stairs and the lift as they stand.
        private string PlaceDetail(DesignRoom room, int at)
        {
            if (room == DesignRoom.Stairs || room == DesignRoom.Lift)
            {
                return JobText.T(DesignRules.Lit(at) ? "reallife_trades_place_core_lit" : "reallife_trades_place_core_middle");
            }
            int steps = 99;
            int lit = 0, length = DesignRules.Length(room);
            for (int k = 0; k < length; k++)
            {
                lit += DesignRules.Lit(at + k) ? 1 : 0;
                int d = DesignRules.ToCore(at + k, _cells);
                steps = d < steps ? d : steps;
            }
            string light = room == DesignRoom.Lobby ? string.Empty
                : JobText.T(lit == length ? "reallife_trades_place_lit" : lit > 0 ? "reallife_trades_place_part_lit" : "reallife_trades_place_dark") + ", ";
            return light + (steps >= 99 ? JobText.T("reallife_trades_place_no_core") : JobText.T("reallife_trades_place_steps", "steps", steps.ToString(CultureInfo.InvariantCulture)));
        }

        private void FacadeList(TravelList list)
        {
            int satisfaction = DesignRules.Satisfaction(_brief, _choice, out int cost);
            list.Begin(JobText.T("reallife_trades_design_facade_title"), JobText.T("reallife_trades_design_facade_info", "cost", cost.ToString(CultureInfo.InvariantCulture),
                "budget", _brief.FacadeBudget.ToString(CultureInfo.InvariantCulture), "satisfaction", Whole(satisfaction)));
            _text.Length = 0;
            _text.Append(JobText.T("reallife_trades_design_taste", "loves", Option(_brief.Loves / DesignRules.Options, _brief.Loves % DesignRules.Options),
                "hates", Option(_brief.Hates / DesignRules.Options, _brief.Hates % DesignRules.Options)));
            list.AddNote(JobText.T(TradeCatalogue.Key + "client_" + _brief.Client), _text.ToString(), list.HeightFor(_text.Length + 40, false));
            for (int e = 0; e < DesignRules.Elements; e++)
            {
                list.Add(ElementId + e, JobText.T(TradeCatalogue.Key + "facade_" + e) + ": " + Option(e, _choice[e]),
                    JobText.T(e == 0 ? "reallife_trades_facade_cost_storey" : "reallife_trades_facade_cost", "cost",
                        DesignRules.FacadeCost(e, _choice[e], e == 0 ? 1 : _brief.Storeys).ToString(CultureInfo.InvariantCulture)),
                    "reallife_trades_design_change", true);
            }
        }

        private static string Option(int element, int option) => JobText.T(TradeCatalogue.Key + "facade_" + element + "_" + option);

        private void FrameList(TravelList list)
        {
            int cost = DesignRules.FrameCost(_columns, _slab, _brief.Storeys);
            list.Begin(JobText.T("reallife_trades_design_frame_title"), JobText.T("reallife_trades_design_frame_info", "cost", cost.ToString(CultureInfo.InvariantCulture),
                "storeys", _brief.Storeys.ToString(CultureInfo.InvariantCulture)));
            _text.Length = 0;
            _text.Append(FrameLine());
            list.AddNote(JobText.T("reallife_trades_design_frame_now"), _text.ToString(), list.HeightFor(_text.Length + 40, false));
            list.Add(SlabId, JobText.T(TradeCatalogue.Key + "slab_" + _slab), JobText.T("reallife_trades_slab_detail", "span", Metres(DesignRules.SpanSteps(_slab) * DesignRules.LineStep)),
                "reallife_trades_design_change", true);
            for (int i = 1; i < DesignRules.Lines - 1; i++)
            {
                list.Add(LineId + i, JobText.T(_columns[i] ? "reallife_trades_line_column" : "reallife_trades_line_open", "line", i.ToString(CultureInfo.InvariantCulture), "at",
                        Metres(i * DesignRules.LineStep)),
                    JobText.T(DesignRules.OnWall(i) ? "reallife_trades_line_wall" : "reallife_trades_line_room"),
                    _columns[i] ? "reallife_trades_design_column_off" : "reallife_trades_design_column_on", true);
            }
        }
    }
}
