using System;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The driving school's one list window (a <see cref="DeskList"/>: a copy of the window the game lists its subway
    /// stations in) and the plain page every report uses: a headline, a line or two, the report as a note and a "Close"
    /// entry. The desk's menu (<see cref="SchoolDesk"/>), the written test (<see cref="TheoryTest"/>) and the result of a
    /// drive (<see cref="PracticalTest"/>) all stand on this window, one at a time: taking it closes whichever had it.
    ///
    /// If the game's window cannot be copied <see cref="Failed"/> is true and each user does its simplest thing (the
    /// desk speaks through the game's dialogue panel, a report is a notification, a written test is not started and the
    /// fee is returned). Nothing here runs per frame.
    /// </summary>
    internal sealed class SchoolScreens
    {
        private const int RowClose = -1;
        private const string ButtonDo = "reallife_lic_list_do";

        /// <summary>The window every school screen shares.</summary>
        public readonly DeskList Window = new DeskList("RealLifeOverhaul Licences", 1100f, 150f);

        private readonly Action<int> _onPick;
        private readonly Action _onClosed;
        private Action _closed;

        public SchoolScreens()
        {
            _onPick = Picked;
            _onClosed = Closed;
        }

        /// <summary>True when the game's window could not be copied.</summary>
        public bool Failed => Window.Failed;

        public bool IsOpen => Window.IsOpen;

        /// <summary>
        /// A report: <paramref name="title"/> as the headline, <paramref name="info"/> under it, <paramref name="body"/> as a
        /// note and a Close entry. <paramref name="closed"/> runs once when the page is closed, by Close, the cancel key or
        /// another screen taking the window. False if the window cannot be made (nothing shown).
        /// </summary>
        public bool Report(object owner, string title, string info, string body, Action closed)
        {
            TravelList list = Window.Take(owner);
            if (list == null)
            {
                return false;
            }
            _closed = closed;
            list.Begin(title, info);
            if (!string.IsNullOrEmpty(body))
            {
                int lines = 0;
                for (int i = 0; i < body.Length; i++)
                {
                    lines += body[i] == '\n' ? 1 : 0;
                }
                list.AddNote(Licences.T("reallife_lic_page_report"), body, list.HeightFor(body.Length + 40 * lines, false));
            }
            list.Add(RowClose, Licences.T("reallife_lic_btn_close"), null, ButtonDo, true);
            list.Open(_onPick, _onClosed);
            if (Meta.Dev.Verbose)
            {
                Licences.Logger?.Info("Licences check (report): the game's list is open with '" + title + "', " + list.Made + " entries made so far: PASS.");
            }
            return true;
        }

        /// <summary>Closes whatever is on the window.</summary>
        public void Close()
        {
            Window.Close();
        }

        private void Picked(int id)
        {
            if (id == RowClose)
            {
                Window.Close();
            }
        }

        private void Closed()
        {
            Action closed = _closed;
            _closed = null;
            closed?.Invoke();
        }
    }
}
