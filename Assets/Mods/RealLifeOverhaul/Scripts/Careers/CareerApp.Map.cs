using System.Collections.Generic;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The Career app's Show on map: the employers that hire at a help-wanted board, each with the address of its board and a button that
    /// puts the game's destination marker on it (the Professions tab), and the same button on a vacancy, an application and the job held.
    /// </summary>
    public sealed partial class CareerApp
    {
        private readonly List<BoardEmployer> _boards = new List<BoardEmployer>(8);

        // The Professions tab's boxed note: the gig work's employers, where each one's board stands, and how to get there.
        private void WriteBoards(InfoBox box)
        {
            _boards.Clear();
            System.Action<List<BoardEmployer>> read = CareersBridges.Boards;
            if (read != null)
            {
                read(_boards);
            }
            if (_boards.Count == 0)
            {
                return;
            }
            box.Section("reallife_career_sec_boards");
            box.Note(CareersText.T("reallife_career_boards_about"));
            for (int i = 0; i < _boards.Count; i++)
            {
                BoardEmployer board = _boards[i];
                if (board.Address == null)
                {
                    box.Line(CareersText.Fill("reallife_career_boards_phone", "employer", board.Name), CellTone.Plain);
                    continue;
                }
                box.Line(CareersText.Fill("reallife_career_boards_at", "employer", board.Name, "address", board.Address), CellTone.Plain);
                MapButton(box.ButtonRow(), () => _director.ShowBoard(board));
            }
        }

        // One Show on map button: the answer, if it did not work, in the game's own notice.
        private void MapButton(UnityEngine.Transform row, System.Func<string> show)
        {
            InfoBox.Button(row, InfoBox.Additional, "reallife_career_button_map", () => Do(show));
        }
    }
}
