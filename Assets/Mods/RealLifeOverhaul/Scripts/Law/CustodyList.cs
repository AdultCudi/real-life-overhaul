using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// The holding cell in the game's own list window (<see cref="TravelList"/>): the charge sheet, the time left, and sit it
    /// out, bail or a lawyer. Cost: nothing a frame; the clock line is written only when the minute shown changes.
    /// </summary>
    internal sealed class CustodyList
    {
        public const int Options = 3;
        private const string ButtonDo = "reallife_law_list_do";

        private readonly IModLogger _logger;
        private readonly DeskList _window = new DeskList("RealLifeOverhaul Custody", 1100f, 150f);
        private readonly string[] _name = new string[Options];
        private readonly string[] _detail = new string[Options];
        private readonly bool[] _on = new bool[Options];
        private readonly StringBuilder _info = new StringBuilder(200);
        private readonly Action<int> _onPick;
        private readonly Action _onClosed;
        private Action<int> _chosen;
        private string _title;
        private string _sheet;
        private string _sentence;
        private string _precinct;
        private string _clockFormat;
        private int _hours = -1;
        private int _minutes = -1;
        private bool _docked;
        private bool _mounted;
        private int _shown = Options;                       // how many of the entries this stay offers
        private string _noteKey = "reallife_law_cell_sheet"; // the heading over the note

        public CustodyList(IModLogger logger)
        {
            _logger = logger;
            _onPick = Picked;
            _onClosed = Closed;
            TravelList.Logger = logger;
        }

        public bool IsOpen => _window.IsOpen;

        /// <summary>Puts the cell's list up, at the right when <paramref name="docked"/> (a cell in view). False if it could not be made.</summary>
        public bool Open(string precinct, string sheet, string sentence, Action<int> chosen, bool docked)
        {
            TravelList list = _window.Take(this);
            if (list == null)
            {
                _logger?.Info("Law check (cell list): the game's list could not be made; the sentence is served without the choices: FAIL.");
                return false;
            }
            _chosen = chosen;
            _precinct = precinct;
            _title = "reallife_law_cell_title".Tr();
            _sheet = sheet;
            _sentence = sentence;
            _docked = docked;
            _shown = Options;
            _noteKey = "reallife_law_cell_sheet";
            _clockFormat = "reallife_law_cell_clock".Tr().Replace("{hours}", "{0}").Replace("{minutes}", "{1}");
            _hours = -1;
            _minutes = -1;
            RectTransform window = list.Window;
            Vector2 at = docked ? new Vector2(1f, 0.5f) : new Vector2(0.5f, 0.5f);
            window.anchorMin = window.anchorMax = window.pivot = at;
            window.anchoredPosition = docked ? new Vector2(-80f, 0f) : Vector2.zero;
            return true;
        }

        /// <summary>A court's order: only the first <paramref name="options"/> entries, and this heading over the note. Call after Open.</summary>
        public void Shape(int options, string noteKey)
        {
            _shown = Mathf.Clamp(options, 1, Options);
            _noteKey = string.IsNullOrEmpty(noteKey) ? "reallife_law_cell_sheet" : noteKey;
        }

        /// <summary>Shows what was set and locks the window: the way out is the sentence, bail or a lawyer.</summary>
        public void Show()
        {
            TravelList list = _window.Take(this);
            if (list == null)
            {
                return;
            }
            Fill(list);
            list.Open(_onPick, _onClosed);
            list.Locked = true;
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Law check (cell list): the game's list is open at " + _precinct + (_docked ? " (beside the cell)" : string.Empty) + ", " + list.Made + " entries made so far: PASS.");
            }
        }

        /// <summary>Moves the window onto the time skip's own canvas, which stays when the HUD goes. Call once the skip has started.</summary>
        public void Mount()
        {
            TravelList list = _window.Take(this);
            UIs uis = InstanceBehavior<UIs>.Instance;
            Canvas canvas = uis != null && uis.timeMachine != null ? uis.timeMachine.canvas : null;
            if (list == null || canvas == null)
            {
                _logger?.Info("Law check (cell list): the time skip's canvas was not found; the list stays on the HUD's: FAIL.");
                return;
            }
            list.MoveTo(canvas.transform);
            _mounted = true;
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Law check (cell list): the list stands on the time skip's canvas: PASS.");
            }
        }

        public void Close()
        {
            _chosen = null;
            if (_window.IsOpen)
            {
                _window.Close();
            }
        }

        public void Dispose()
        {
            Close();
            _window.Destroy(e => _logger?.Error(e));
        }

        /// <summary>The time still to serve; the line is rewritten only when the minute shown changes.</summary>
        public void SetClock(int hours, int minutes)
        {
            if (hours == _hours && minutes == _minutes)
            {
                return;
            }
            _hours = hours;
            _minutes = minutes;
            TravelList list = _window.IsOpen ? _window.Take(this) : null;
            if (list != null)
            {
                list.Info(InfoText());
            }
        }

        public void SetStatus(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                Notifications.Show(NotificationType.Info, "reallife_law_desk_toast", new Dictionary<string, string>(1) { { "text", text } }, 8f);
            }
        }

        /// <summary>An entry's words and whether its button is on. Shown by the next <see cref="Show"/> or <see cref="Refresh"/>.</summary>
        public void SetOption(int index, string name, string detail, bool available)
        {
            if (index < 0 || index >= Options)
            {
                return;
            }
            _name[index] = name;
            _detail[index] = detail;
            _on[index] = available;
        }

        /// <summary>Writes the entries again after an option changed while the list is up.</summary>
        public void Refresh()
        {
            TravelList list = _window.IsOpen ? _window.Take(this) : null;
            if (list != null)
            {
                Fill(list);
                list.Open(_onPick, _onClosed);
            }
        }

        private string InfoText()
        {
            _info.Length = 0;
            string clock = _hours < 0 ? string.Empty : string.Format(_clockFormat, _hours, _minutes.ToString("00"));
            _info.Append(clock).Append("\n<size=80%>").Append(_precinct).Append(". ").Append(_sentence).Append("</size>");
            return _info.ToString();
        }

        private void Fill(TravelList list)
        {
            list.Begin(_title, InfoText());
            list.AddNote(_noteKey.Tr(), _sheet, list.HeightFor(_sheet.Length + 40 * CountLines(_sheet), false));
            for (int i = 0; i < _shown; i++)
            {
                list.Add(i, _name[i], _detail[i], ButtonDo, _on[i]);
            }
        }

        private static int CountLines(string text)
        {
            int lines = 0;
            for (int i = 0; i < text.Length; i++)
            {
                lines += text[i] == '\n' ? 1 : 0;
            }
            return lines;
        }

        private void Picked(int id)
        {
            Action<int> chosen = _chosen;
            if (chosen != null && id >= 0 && id < _shown && _on[id])
            {
                chosen(id);
            }
        }

        // Closed by the owner: the window goes back to the HUD's canvas for whoever uses it next.
        private void Closed()
        {
            _chosen = null;
            if (_mounted)
            {
                _mounted = false;
                TravelList list = _window.Take(this);
                list?.MoveTo(null);
            }
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Law check (cell list): closed.");
            }
        }
    }
}
