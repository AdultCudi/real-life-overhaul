using System.Collections.Generic;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The Career app's Show on map: the employers that hire at a help-wanted board, each with the address of its board and a button that
    /// puts the game's destination marker on it (the Professions tab), the building trades' three with the address of the practice's
    /// floor or of the nearest works site and the same button, and the same button on a vacancy, an application and the job held.
    /// </summary>
    public sealed partial class CareerApp
    {
        private readonly List<BoardEmployer> _boards = new List<BoardEmployer>(12);

        // The Professions tab's boxed note: the employers, where each one's work is done, and how to get there.
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
            bool works = false;
            for (int i = 0; i < _boards.Count; i++)
            {
                BoardEmployer board = _boards[i];
                if (board.Works)
                {
                    works = true;   // (the building trades have no board: they are listed below, with lines of their own)
                    continue;
                }
                if (board.Address == null)
                {
                    box.Line(CareersText.Fill("reallife_career_boards_phone", "employer", board.Name), CellTone.Plain);
                    continue;
                }
                box.Line(CareersText.Fill("reallife_career_boards_at", "employer", board.Name, "address", board.Address, "place", board.Place ?? string.Empty), CellTone.Plain);
                MapButton(box.ButtonRow(), () => _director.ShowBoard(board));
            }
            if (works)
            {
                WriteWorks(box);
            }
        }

        // The building trades' employers: the practice's address and floor, and the nearest works site, each with its Show on map.
        private void WriteWorks(InfoBox box)
        {
            box.Section("reallife_career_sec_works");
            box.Note(CareersText.T("reallife_career_works_about"));
            for (int i = 0; i < _boards.Count; i++)
            {
                BoardEmployer site = _boards[i];
                if (!site.Works)
                {
                    continue;
                }
                box.Line(CareersText.Fill(site.Line, "employer", site.Name, "address", site.Address ?? string.Empty), CellTone.Plain);
                if (site.Address != null)
                {
                    MapButton(box.ButtonRow(), () => _director.ShowBoard(site));
                }
            }
        }

        // One Show on map button: the answer, if it did not work, in the game's own notice.
        private void MapButton(UnityEngine.Transform row, System.Func<string> show)
        {
            InfoBox.Button(row, InfoBox.Additional, "reallife_career_button_map", () => Do(show));
        }
    }
}
