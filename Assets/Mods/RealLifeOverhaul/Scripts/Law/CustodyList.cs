using System;
using System.Text;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// The holding cell, in the window the game lists its subway stations in (<see cref="TravelList"/>): the charge
    /// sheet as a note, the time still to serve in the lines under the headline, and an entry for each of the three
    /// things to do (sit it out, pay bail, call a lawyer) with its cost under its name. The list cannot be left with
    /// the cancel key while the player is held (<see cref="TravelList.Locked"/>): custody ends by the clock, by bail
    /// or in the cell itself, as it did.
    ///
    /// The time is waited out by the game's own time skip, which switches the HUD off, so the window stands on the
    /// skip's own canvas while it runs (<see cref="Mount"/>) and goes back to the HUD's when it is closed. Where a
    /// real cell is in view the window stands at the right of the screen, with the room beside it.
    ///
    /// The cost: the clock line is written only when the minute shown changes; entries are written when an option
    /// changes (a lawyer called). Nothing runs per frame. If the game's window cannot be copied <see cref="Open"/>
    /// returns false and the sentence is served without the choices.
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

        /// <summary>True while the cell's list is on screen.</summary>
        public bool IsOpen => _window.IsOpen;

        /// <summary>
        /// Puts the cell's list up. <paramref name="chosen"/> is told which entry was clicked (0, 1 or 2). False if the
        /// game's window could not be copied.
        /// </summary>
        /// <param name="docked">True when the player is standing in a real cell: the list is at the right of the screen with the room in view beside it.</param>
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

        /// <summary>
        /// A stay that is not an arrest's (a court's order): only the first <paramref name="options"/> entries are listed,
        /// and the note has this heading. Call after <see cref="Open"/>, which puts both back to the arrest's.
        /// </summary>
        public void Shape(int options, string noteKey)
        {
            _shown = Mathf.Clamp(options, 1, Options);
            _noteKey = string.IsNullOrEmpty(noteKey) ? "reallife_law_cell_sheet" : noteKey;
        }

        /// <summary>Shows what was set (call after the options and the clock). Locks the window: the way out is the sentence, bail or a lawyer.</summary>
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

        /// <summary>
        /// Moves the window onto the canvas of the game's time skip, which keeps its own screen when it hides the HUD. Call once the
        /// skip has started. Without that canvas the window stays on the HUD's (and is not seen while the skip runs).
        /// </summary>
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

        /// <summary>The time still to serve. The line is rewritten only when the minute shown changes.</summary>
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

        /// <summary>What the desk or the cell answers: the game's own notification.</summary>
        public void SetStatus(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, "reallife_law_desk_toast",
                    new System.Collections.Generic.Dictionary<string, string>(1) { { "text", text } }, 8f);
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

        // Closed by the owner (the cell ends): the window goes back to the HUD's canvas for whoever uses it next.
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
