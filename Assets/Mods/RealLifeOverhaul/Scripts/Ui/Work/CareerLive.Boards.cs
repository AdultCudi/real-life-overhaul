using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;

namespace RealLifeOverhaul.Ui
{
    // Show on map for the employers that hire at a help-wanted board, and for the building trades' practice and works sites, on the Professions tab: the old page listed each with its
    // address and a button, here one selector chooses the employer and the profession's detail says where it is and puts it on the map.
    public sealed partial class CareerLive
    {
        private const int BoardSlot = 10;   // the amount place the chosen employer is kept in
        private readonly List<BoardEmployer> _boards = new List<BoardEmployer>(12);
        private readonly List<WorkSection> _boardNotes = new List<WorkSection>(16);
        private readonly List<WorkAct> _boardActs = new List<WorkAct>(16);
        private WorkAmount _boardAmount;
        private string[] _boardNames = new string[0];

        private void ReadBoards(WorkTab tab)
        {
            _boards.Clear();
            _boardNotes.Clear();
            _boardActs.Clear();
            _boardAmount = null;
            Action<List<BoardEmployer>> read = CareersBridges.Boards;
            if (read != null)
            {
                read(_boards);
            }
            if (_boards.Count == 0 || tab.Rows.Count == 0)
            {
                return;
            }
            UtilitiesWords words = W;
            _boardNames = new string[_boards.Count];
            for (int i = 0; i < _boardNames.Length; i++)
            {
                _boardNames[i] = _boards[i].Name;
            }
            _boardAmount = new WorkAmount { Id = BoardSlot, Value = 0, Min = 0, Max = _boards.Count - 1, Words = _boardNames, Figure = _boardNames[0] };
            tab.Amounts.Add(_boardAmount);
            int amount = tab.Amounts.Count - 1;
            for (int i = 0; i < tab.Rows.Count; i++)
            {
                WorkRow r = tab.Rows[i];
                r.Sections.Add(new WorkSection { Part = WorkPart.Amount, Title = words.T("reallife_career_sec_boards", "Employers at the help-wanted boards"), Stepper = amount });
                var note = new WorkSection { Part = WorkPart.Text };
                r.Sections.Add(note);
                _boardNotes.Add(note);
                WorkAct act = Act(CareerAct.ShowBoard, 0, words.T("reallife_career_button_map", "Show on map"), UiButtonKind.Secondary, UiIcon.Locate, true);
                r.Acts.Add(act);
                _boardActs.Add(act);
            }
        }

        // The employer the view has chosen: its address in the detail, and the button dimmed with the reason where the city has no board for it.
        private void ChooseBoard(WorkView view)
        {
            if (_boardAmount == null || _boards.Count == 0)
            {
                return;
            }
            int at = Math.Max(0, Math.Min(_boards.Count - 1, view.Get(BoardSlot, 0)));
            BoardEmployer board = _boards[at];
            _boardAmount.Value = at;
            _boardAmount.Figure = _boardNames[at];
            string text = board.Address == null ? CareersText.Fill("reallife_career_boards_phone", "employer", board.Name)
                : board.Works ? CareersText.Fill(board.Line, "employer", board.Name, "address", board.Address)
                : CareersText.Fill("reallife_career_boards_at", "employer", board.Name, "address", board.Address, "place", board.Place ?? string.Empty);
            for (int i = 0; i < _boardNotes.Count; i++)
            {
                _boardNotes[i].Text = text;
                _boardActs[i].Why = board.Address == null ? text : null;
            }
        }

        private bool ShowBoardNow(CareersDirector d, WorkView view)
        {
            if (_boards.Count == 0)
            {
                return false;
            }
            return Said(d.ShowBoard(_boards[Math.Max(0, Math.Min(_boards.Count - 1, view.Get(BoardSlot, 0)))]), d);
        }
    }
}
