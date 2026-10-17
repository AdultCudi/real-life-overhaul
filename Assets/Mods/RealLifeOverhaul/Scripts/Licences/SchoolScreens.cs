using System;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The school's one list window, taken in turn by the desk, the written test and a drive's result. When the game's
    /// window cannot be copied (<see cref="Failed"/>), each falls back to the dialogue panel or a notification.
    /// </summary>
    internal sealed class SchoolScreens
    {
        private const int RowClose = -1;
        private const string ButtonDo = "reallife_lic_list_do";

        public readonly DeskList Window = new DeskList("RealLifeOverhaul Licences", 1100f, 150f);

        private readonly Action<int> _onPick;
        private readonly Action _onClosed;
        private Action _closed;

        public SchoolScreens()
        {
            _onPick = Picked;
            _onClosed = Closed;
        }

        public bool Failed => Window.Failed;

        /// <summary>A report page; <paramref name="closed"/> runs once when it closes. False if the window cannot be made.</summary>
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
