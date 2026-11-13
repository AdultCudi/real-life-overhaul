using System;
using System.Collections.Generic;
using BigAmbitions.InputSystem;
using Buildings.Office.Headquarters;
using EnhancedUI.EnhancedScroller;
using Entities;
using Helpers;
using TMPro;
using UI;
using UI.Smartphone.Apps.MyEmployees;
using UnityEngine;
using UnityEngine.UI;
using Dropdown = UI.Elements.Dropdown;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// My Employees as a screen of the kit, mirrored from the game's own app (Football Manager's squad screen): its two tabs,
    /// the game's table of each as the kit's big table (in the order the game sorts it, narrowed by the game's filters and
    /// search, and by business on the kit's side), several picked for the game's mass actions, and one person opened from
    /// it as a profile from the game's employee box. Every press goes to the game: a row opens through the game's own row
    /// event, a filter sets the game's toggles and the game filters its table again, a sort is the game's own header, the
    /// box's buttons, the train buttons and the choice of business are the game's pressed, a pick goes through the game's
    /// own selection, a mass action is chosen in the game's own drop-down, so its question box, Going Public's patches and
    /// other mods' run as a click runs them. A wage shown is what is paid (Core/PaidWage). Cost: a look at the game's
    /// table four times a second while it shows (a few numbers a row, nothing allocated); a layout only when it changed.
    /// </summary>
    public sealed partial class EmployeesScreen : IUiScreen, IUiTyping
    {
        private const float Every = 0.25f;                 // seconds between looks at the game's app
        private const string EmployeesTab = "Employees";    // the game's names for its tabs (MyEmployeesTab)
        private const string CandidatesTab = "Candidates";

        private readonly MyEmployees _app;
        private readonly StaffView _v = new StaffView();
        private readonly UiStaffModel _m = new UiStaffModel();
        private readonly List<EmployeeInstance> _rows = new List<EmployeeInstance>(64);   // the table's rows in order
        private readonly List<int> _dataIndex = new List<int>(64);                        // each row's place in the game's table
        private readonly List<Button> _train = new List<Button>(4);
        private readonly List<string> _optionKeys = new List<string>(24);                 // what each option of the open list stands for
        private readonly Button _positive, _negative, _additional, _bonus;
        private string _business;          // the kit's business filter: null every business, empty the unassigned
        private bool _picking, _armed, _dirty = true;
        private int _open = -1, _lastPick = -1, _reveal = -1, _previewRow;
        private float _optionOffset, _offset, _next;
        private long _seen;
        private string _cardPutAway;       // the person whose training card was put away this time their page is open
        private EmployeeInstance _lastPerson;

        public EmployeesScreen(MyEmployees app)
        {
            _app = app;
            _positive = UiWidgets.Calling(app, "OnPositiveActionButtonClick");   // the box's buttons, as the game wires them in its Inspector
            _negative = UiWidgets.Calling(app, "OnNegativeActionButtonClick");
            _additional = UiWidgets.Calling(app, "OnAdditionalActionButtonClick");
            _bonus = UiWidgets.Calling(app, "OnPayBonusButtonClick");
        }

        public string Id => EmployeesKit.StaffId;

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    long now = Signature();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public int TypingCommand => UiDataTable.First + UiDataTable.Search;

        public string TypingText => Search() != null ? Search().text : string.Empty;

        public void Typed(string text)
        {
            TMP_InputField field = Search();
            if (field == null)
            {
                return;
            }
            field.SetTextWithoutNotify(text ?? string.Empty);
            Refilter();   // the game's filter reads its own field: its table filters itself again, as typing there does
            _offset = 0f;
            _dirty = true;
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Build();
            StaffViews.Staff(_m, UiText.Words, _v);
            UiStaff.Lay(s, _m, frame, dark, keys);
            _offset = _m.Table.Offset;
            _reveal = -1;
            if (_m.Batch != null)
            {
                _batchOffset = _m.Batch.Table.Offset;   // the sheet's own list scrolls on its own offset
            }
            _batchReveal = -1;
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            _dirty = true;
            if (_m.Batch != null)
            {
                return BatchPress(command, arg);   // the review sheet has the keys and the pointer: nothing under it answers
            }
            if (_m.Card != null)
            {
                return command == UiCard.ButtonCommand && PutCardAway();
            }
            switch (command)
            {
                case UiAppWindow.NavCommand:
                    return ShowTab(arg == 1);
                case UiAppWindow.ActionCommand:
                    return Action(arg);
                case UiStaff.SwitchCommand:
                    return HideJobBoard();
                case UiStaff.AssignCommand:
                    return Open(UiStaff.ListAssign);
                case UiStaff.TrainCommand:
                    return arg >= 0 && arg < _train.Count && Done(UiWidgets.Press(_train[arg]), "train a skill of " + Who() + ": handed to the game");
                case UiStaff.OptionCommand:
                    return Option(arg);
                case UiStaff.PreviewCommand:
                    if (arg == UiStaff.PreviewLink)
                    {
                        return _previewRow < _rows.Count && OpenPerson(_previewRow);
                    }
                    if (arg == UiStaff.PreviewAssign)
                    {
                        // The candidate's business: their page opens as a press on the row opens it, with the game's own drop-down of businesses open over it.
                        return Candidates() && _previewRow < _rows.Count && OpenPerson(_previewRow) && Open(UiStaff.ListAssign);
                    }
                    // A candidate's discard, negotiation or hire from the row beside the table: their page opens as the row's press opens it,
                    // then the box's own button is pressed, so the game asks and answers as it does for the page.
                    return Candidates() && arg >= 0 && arg <= 2 && _previewRow < _rows.Count && OpenPerson(_previewRow) && Action(arg);
                case UiStaff.EmptyCommand:
                    return ShowTab(true);   // nobody hired yet: the way out is the candidates
                case UiAppWindow.PersonCommand:
                    return arg >= 0 && arg < _mates.Count && OpenMate(_mates[arg]);
                case UiAppWindow.AllCommand:
                    return AllMates();
                case UiDataTable.First + UiDataTable.Row:
                    return arg >= 0 && arg < _rows.Count && (_picking ? Pick(arg) : OpenPerson(arg));
                case UiDataTable.First + UiDataTable.Head:
                    return Sort(arg);
                case UiDataTable.First + UiDataTable.Filter:
                    return Open(arg);
                case UiDataTable.First + UiDataTable.Clear:
                    return ClearFilters();
                case UiDataTable.First + UiDataTable.Pick:
                    _picking = true;
                    _lastPick = -1;
                    return true;
                case UiDataTable.First + UiDataTable.Done:
                    return StopPicking();
                case UiDataTable.First + UiDataTable.Mass:
                    return Mass(arg);
                case UiDataTable.First + UiDataTable.MassChoice:
                    return Open(UiStaff.ListMass);
                case UiDataTable.First + UiDataTable.MassAnswer:
                    return Answer(arg);
                default:
                    return false;
            }
        }

        public void Focused(int command, int arg)
        {
            // The cursor on a row shows it in the preview; a person opens on a press only, as the game's row event writes its box.
            if (command == UiDataTable.First + UiDataTable.Row && arg != _previewRow && arg >= 0 && arg < _rows.Count)
            {
                _previewRow = arg;
                _dirty = true;
            }
        }

        public bool Back()
        {
            _dirty = true;
            if (StaffBatch.Sheet != null)
            {
                return Done(StaffBatch.Cancel(), "the review sheet put away: nothing was changed");   // the sheet goes first, back to the game's panel of choices
            }
            if (_open >= 0)
            {
                _open = -1;   // an open list closes first, as the game's drop-down does
                return true;
            }
            if (_m.Card != null)
            {
                return PutCardAway();
            }
            if (_armed)
            {
                _armed = false;   // an armed discard is let go before anything closes
                return true;
            }
            if (_picking)
            {
                return StopPicking();
            }
            if (_app.SelectedEmployeeInstance != null)
            {
                ClosePerson();
                return true;
            }
            return false;
        }

        public void Tab(int step)
        {
            if (_m.Card == null && _m.Batch == null && _open < 0)
            {
                ShowTab(!Candidates());
            }
        }

        public bool Act(UiAction action) => false;   // F and P are the person page's own buttons, keyed on them

        public void Reveal(int list, int index)
        {
            if (_m.Batch != null)
            {
                _batchReveal = index;
            }
            else if (_open >= 0)
            {
                _optionOffset = Math.Max(0f, (index - 7) * UiTokens.ControlSmall);
            }
            else
            {
                _reveal = index;
            }
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            if (_m.Batch != null)
            {
                _batchOffset = Math.Max(0f, _batchOffset - notches * UiTokens.S16);
            }
            else if (_open >= 0)
            {
                _optionOffset = Math.Max(0f, _optionOffset - notches * UiTokens.ControlSmall);
            }
            else
            {
                _offset = Math.Max(0f, _offset - notches * UiTokens.S16);
            }
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------------- presses

        private bool Candidates() => _app.CurrentTab == CandidatesTab;

        // The game's own tab change: it closes its panels, clears what was picked and loads that tab's table.
        private bool ShowTab(bool candidates)
        {
            if (candidates == Candidates())
            {
                return true;
            }
            StaffBatch.Close();
            _massType = null;
            _app.ChangeTab(candidates ? CandidatesTab : EmployeesTab);
            _picking = false;
            _armed = false;
            _open = -1;
            _business = null;
            _offset = 0f;
            _lastPick = -1;
            _lastPerson = null;
            _previewRow = 0;
            return true;
        }

        // Someone beside the person open: their page through the game's own ShowEmployee (what its row event calls); a
        // colleague of a candidate is on the employees' tab, so the game changes tab first and shows them a frame later.
        private bool OpenMate(EmployeeInstance mate)
        {
            if (mate == null)
            {
                return false;
            }
            if (Candidates())
            {
                ShowTab(false);
                _app.DelayShowEmployee(mate);
            }
            else
            {
                _app.ShowEmployee(mate);
            }
            _armed = false;
            _cardPutAway = null;
            return Done(true, "open a colleague's page: handed to the game");
        }

        // Every colleague in the table: back from the page with the table narrowed to their business.
        private bool AllMates()
        {
            string business = _person.MatesAt;
            if (Candidates() || string.IsNullOrEmpty(business))
            {
                return false;
            }
            ClosePerson();
            _lastPerson = null;
            _business = business;
            _offset = 0f;
            _previewRow = 0;
            return true;
        }

        // A row opens its person through the game's own row event, so the game (and Going Public's patch) writes the box.
        private bool OpenPerson(int row)
        {
            int at = _dataIndex[row];
            bool done;
            if (Candidates())
            {
                List<CandidateModel> data = _app.candidateScrollerController.data;
                done = at < data.Count && data[at] != null;
                if (done)
                {
                    _app.candidateScrollerController.onRowSelected.Invoke(data[at]);
                }
            }
            else
            {
                List<EmployeeModel> data = _app.employeeScrollerController.data;
                done = at < data.Count && data[at] != null;
                if (done)
                {
                    _app.employeeScrollerController.onRowSelected.Invoke(data[at]);
                }
            }
            _armed = false;
            _cardPutAway = null;
            return Done(done, "open " + (row < _rows.Count && _rows[row] != null ? "a person" : "nobody") + ": handed to the game");
        }

        // Back from a person: the game's own way out of its box, as changing tab does.
        private void ClosePerson()
        {
            _lastPerson = _app.SelectedEmployeeInstance;
            _app.DeselectEmployee();
            _app.employeeScrollerController.SelectRow(null, null);
            _app.candidateScrollerController.SelectRow(null, null);
            _armed = false;
            _open = -1;
        }

        // The box's buttons: the danger (0), the bonus or the negotiation (1), the main (2). A discard asks twice, since the
        // game discards at once; firing is asked by the game's own question box.
        private bool Action(int arg)
        {
            bool candidate = _app.SelectedEmployeeInstance != null && _app.SelectedEmployeeInstance.IsCandidate;
            switch (arg)
            {
                case 0:
                    if (candidate && !_armed)
                    {
                        _armed = true;
                        return UiWidgets.Shows(_negative);
                    }
                    _armed = false;
                    return Done(UiWidgets.Press(_negative), (candidate ? "discard " : "fire ") + Who() + ": handed to the game");
                case 1:
                    return candidate ? Done(UiWidgets.Press(_additional), "negotiate with " + Who() + ": handed to the game")
                        : Done(UiWidgets.Press(_bonus), "pay a bonus to " + Who() + ": handed to the game");
                case 2:
                    return Done(UiWidgets.Press(_positive), (candidate ? "hire " : "manage the schedule of ") + Who() + ": handed to the game");
                case 3:
                    return !candidate && HrManager();
                default:
                    return false;
            }
        }

        // The HR manager button of the game's row: BizMan opens on its HR managers tab at the plan the employee is under, by the
        // two calls the row's button makes.
        private bool HrManager()
        {
            EmployeeInstance e = _app.SelectedEmployeeInstance;
            HrManagerPlan plan = e != null && !string.IsNullOrEmpty(e.assignedHrManagerPlanId) ? HrManagerHelper.GetPlanFromId(e.assignedHrManagerPlanId) : null;
            UIs uis = InstanceBehavior<UIs>.Instance;
            var bizMan = uis != null && uis.fullMenu != null ? uis.fullMenu.bizMan : null;
            if (plan == null || plan.HrManagerInstance == null || bizMan == null || bizMan.business == null)
            {
                return Done(false, "show an employee's HR manager plan");
            }
            bizMan.Open(plan.headquartersAddress, BizManRules.HrManagers);
            bizMan.business.hrManagersPlanList.SelectPlan(plan);
            return Done(true, "open BizMan on the HR manager plan of " + Who() + ": handed to the game");
        }

        // The game's own toggle for the job board's candidates, set through itself so its listener saves it and reloads the table.
        private bool HideJobBoard()
        {
            var toggle = GameContract.StaffHideJobBoard.Field?.GetValue(_app.candidateScrollerController) as Toggle;
            return toggle != null && Done(UiWidgets.Set(toggle, !toggle.isOn), "hide the job board's candidates: handed to the game");
        }

        // A sort is the game's own header: its table orders itself and shows its arrow, which the kit reads back.
        private bool Sort(int column)
        {
            string header = Header(column);
            if (header == null)
            {
                return false;
            }
            if (Candidates())
            {
                _app.candidateScrollerController.ChangeSortOrder(header);
            }
            else
            {
                _app.employeeScrollerController.ChangeSortOrder(header);
            }
            return true;
        }

        // A pick through the game's own selection; with the game's key for several held (Shift), every row from the last
        // one picked, as the game's tick boxes take a range.
        private bool Pick(int row)
        {
            List<EmployeeInstance> picked = MyEmployeesMassActionsUI.massActionSelectedEmployees;
            if (picked == null)
            {
                return false;
            }
            bool on = !picked.Contains(_rows[row]);
            if (PlayerAction.SelectMultipleElements.Pressing() && _lastPick >= 0 && _lastPick < _rows.Count)
            {
                int from = Math.Min(_lastPick, row), to = Math.Max(_lastPick, row);
                for (int i = from; i <= to; i++)
                {
                    PickRow(i, on);
                }
            }
            else if (!PickRow(row, on))
            {
                return Done(false, "pick a row its tick box holds (another player's)");
            }
            _lastPick = row;
            _app.lastSelectedEmployeeIndex = _dataIndex[row];
            _app.GetCurrentScroller().RefreshActiveCellViews();
            return true;
        }

        // Every row listed picked, or none, through the game's own selection (the kit's business filter may list fewer than the game's).
        private bool PickAll()
        {
            List<EmployeeInstance> picked = MyEmployeesMassActionsUI.massActionSelectedEmployees;
            if (picked == null)
            {
                return false;
            }
            bool on = !_v.AllPicked;
            for (int i = 0; i < _rows.Count; i++)
            {
                PickRow(i, on);
            }
            _app.GetCurrentScroller().RefreshActiveCellViews();
            return true;
        }

        // A row picked or let go through the game's own selection, where the game's tick box for it can be pressed: Going
        // Public holds the box of a candidate another player claimed, and only the row's cell knows, so the cell is brought
        // into the game's (unseen) view and read first. False when the box is held.
        private bool PickRow(int row, bool on)
        {
            EnhancedScroller scroller = _app.GetCurrentScroller();
            int at = _dataIndex[row];
            if (scroller != null && on)
            {
                scroller.JumpToDataIndex(at);
                EnhancedScrollerCellView cell = scroller.GetCellViewAtDataIndex(at);
                Toggle box = cell is EmployeeCellView e ? e.massActionToggle : cell is CandidateCellView c ? c.massActionToggle : null;
                if (box != null && !box.interactable)
                {
                    return false;
                }
            }
            _app.massActionsUI.ToggleSelectedEmployee(_rows[row], on);
            return true;
        }

        // Picking ends and what was picked is let go, as the game's own selection is when it changes tab.
        private bool StopPicking()
        {
            StaffBatch.Close();
            _massType = null;
            _picking = false;
            _open = -1;
            List<EmployeeInstance> picked = MyEmployeesMassActionsUI.massActionSelectedEmployees;
            if (picked != null && picked.Count > 0)
            {
                var held = new List<EmployeeInstance>(picked);
                for (int i = 0; i < held.Count; i++)
                {
                    _app.massActionsUI.ToggleSelectedEmployee(held[i], false);
                }
                _app.GetCurrentScroller().RefreshActiveCellViews();
            }
            _app.massActionsUI.ClosePanel();
            return true;
        }

        // A mass action is chosen in the game's own drop-down, so the game asks its question or opens its panel of choices.
        private bool Mass(int index)
        {
            if (index < 0 || index >= _m.Table.Mass.Length)
            {
                return false;
            }
            int type = _m.Table.Mass[index].Arg;
            if (type < 0)
            {
                return PickAll();
            }
            var drop = GameContract.StaffMassDropdown.Field?.GetValue(_app.massActionsUI) as Dropdown;
            if (drop == null)
            {
                return false;
            }
            _massType = type < _v.Mass.Length ? _v.Mass[type].Type : null;
            drop.SelectOption(type);
            return Done(true, "a mass action for " + _v.PickedCount + " picked: handed to the game");
        }

        // The panel of choices a mass action opened: Cancel (0) closes it, Confirm (1) answers with the choice made.
        private bool Answer(int arg)
        {
            _open = -1;
            if (arg == 0)
            {
                _massType = null;
                _app.massActionsUI.ClosePanel();
                return true;
            }
            if (ReviewHire())
            {
                return Done(true, "hiring to a business: the review sheet is up, nothing is hired yet");   // the yes on the sheet hires, each by the game's own hire
            }
            _app.massActionsUI.ConfirmMassActionOption();   // what the panel's Confirm calls: the game asks its question next
            return Done(true, "a mass action's choice confirmed: handed to the game");
        }

        // A list opens under its field: a filter's, the choice of business, a mass action's choice.
        private bool Open(int list)
        {
            if (_open == list)
            {
                _open = -1;
                return true;
            }
            _open = list;
            _optionOffset = 0f;
            return true;
        }

        // An option of the open list taken: through the game's own drop-down or toggles, or the kit's business filter.
        private bool Option(int option)
        {
            int list = _open;
            // A filter of skills, demands or states stands open for the next pick, as the game's panel lets any number be switched on.
            bool several = list >= 0 && list < Slots().Length && Slots()[list] != Slot.Business && list != UiStaff.ListAssign && list != UiStaff.ListMass;
            if (!several)
            {
                _open = -1;
            }
            if (option < 0 || option >= _optionKeys.Count)
            {
                return false;
            }
            if (list == UiStaff.ListAssign)
            {
                Dropdown assign = GameContract.StaffAssign.Field?.GetValue(_app) as Dropdown;
                if (assign == null || !assign.gameObject.activeInHierarchy)
                {
                    return false;
                }
                assign.SelectOption(option);   // the game reassigns, takes them off their shifts and writes its box again
                return Done(true, "assign " + Who() + " to a business: handed to the game");
            }
            if (list == UiStaff.ListMass)
            {
                Dropdown choices = GameContract.StaffMassOptions.Field?.GetValue(_app.massActionsUI) as Dropdown;
                choices?.SelectOption(option);
                return choices != null;
            }
            if (!Candidates() && list == StaffViews.Business)
            {
                _business = option == 0 ? null : _optionKeys[option];
                _offset = 0f;
                return true;
            }
            return SetFilter(list, option);
        }

        private bool PutCardAway()
        {
            _cardPutAway = _app.SelectedEmployeeInstance != null ? _app.SelectedEmployeeInstance.id : null;
            return true;
        }

        private string Who()
        {
            EmployeeInstance e = _app.SelectedEmployeeInstance;
            return e != null && e.characterData != null ? "a " + (e.IsCandidate ? "candidate" : "employee") : "nobody";
        }

        // One line a press in Player.log, so a report shows each button reaching the game.
        private static bool Done(bool done, string what)
        {
            UiDirector.Logger?.Info("My Employees (kit): " + what + (done ? string.Empty : " was not shown by the game, so nothing was pressed"));
            return done;
        }
    }
}
