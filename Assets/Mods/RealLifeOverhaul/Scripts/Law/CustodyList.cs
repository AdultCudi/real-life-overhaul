using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Extensions;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// The holding cell in the game's own list window (<see cref="TravelList"/>): the charge sheet, the time left and since
    /// when the player is held, and sit it out (or wait for the hearing), bail, a lawyer or the duty doctor, each with its
    /// figure (when it ends, what it costs). Cost: nothing a frame; the clock lines are written only when the minute shown
    /// changes.
    /// </summary>
    internal sealed class CustodyList
    {
        public const int Options = 4;   // sit it out, bail, a lawyer, the duty doctor
        public const int Doctor = 3;    // the duty doctor: in every hold, whatever a court's order leaves of the others
        private const string ButtonDo = "reallife_law_list_do";

        private readonly IModLogger _logger;
        private readonly DeskList _window = new DeskList("RealLifeOverhaul Custody", 1100f, 150f);
        private readonly string[] _name = new string[Options];
        private readonly string[] _detail = new string[Options];
        private readonly string[] _figure = new string[Options];
        private readonly string[] _reason = new string[Options];
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
        private float _since = -1f;                         // when the hold began, game minutes: the "held since" line; below 0 none
        private bool _docked;
        private bool _mounted;
        private int _shown = Doctor;                        // how many of the first entries (sit it out, bail, a lawyer) this stay offers
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
            _shown = Doctor;
            _since = -1f;
            Array.Clear(_name, 0, Options);
            Array.Clear(_detail, 0, Options);
            Array.Clear(_figure, 0, Options);
            Array.Clear(_reason, 0, Options);
            Array.Clear(_on, 0, Options);
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

        /// <summary>A court's order: only the first <paramref name="options"/> entries (the duty doctor stays), and this heading over the note. Call after Open.</summary>
        public void Shape(int options, string noteKey)
        {
            _shown = Mathf.Clamp(options, 1, Doctor);
            _noteKey = string.IsNullOrEmpty(noteKey) ? "reallife_law_cell_sheet" : noteKey;
        }

        /// <summary>When the hold began, game minutes (CustodyState.StartedAt): the list says since when and how long. Call after Open.</summary>
        public void Since(float startedAt)
        {
            _since = startedAt;
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

        /// <summary>The time still to serve; the lines are rewritten only when the minute shown changes.</summary>
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
                if (_since >= 0f)
                {
                    list.Fact(0, "reallife_law_cell_held".Tr(), Held());
                }
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
            SetOption(index, name, detail, available, null, null);
        }

        /// <summary>
        /// As above, with the entry's figure (when it ends, what it costs: "Today, 14:30", "$500") and, while it is not on,
        /// why ("You are fine"). The RLO interface shows the figure at the entry's right and the why as a chip; the game's
        /// window puts them before the entry's smaller line.
        /// </summary>
        public void SetOption(int index, string name, string detail, bool available, string figure, string reason)
        {
            if (index < 0 || index >= Options)
            {
                return;
            }
            _name[index] = name;
            _detail[index] = detail;
            _on[index] = available;
            _figure[index] = figure;
            _reason[index] = reason;
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

        // "02:10 today, 6 h 35 min": when the hold began, in the game's clock, and how long it has run.
        private string Held()
        {
            int start = Mathf.Max(0, Mathf.FloorToInt(_since));
            int held = Mathf.Max(0, Mathf.FloorToInt(TimeHelper.NowInMinutes() - _since));
            int ago = TimeHelper.CurrentDay - start / 1440, m = start % 1440;
            string span = "reallife_law_cell_span".Tr().Replace("{hours}", (held / 60).ToString()).Replace("{minutes}", (held % 60).ToString("00"));
            string key = ago <= 0 ? "reallife_law_cell_held_today" : ago == 1 ? "reallife_law_cell_held_yesterday" : "reallife_law_cell_held_days";
            return key.Tr().Replace("{time}", (m / 60).GetFormattedTime(m % 60)).Replace("{days}", ago.ToString()).Replace("{span}", span);
        }

        private void Fill(TravelList list)
        {
            list.Begin(_title, InfoText());
            if (_since >= 0f)
            {
                list.Fact(0, "reallife_law_cell_held".Tr(), Held());
            }
            list.AddNote(_noteKey.Tr(), _sheet, list.HeightFor(_sheet.Length + 40 * CountLines(_sheet), false));
            for (int i = 0; i < Options; i++)
            {
                if (Offered(i))
                {
                    list.Add(i, _name[i], _detail[i], ButtonDo, _on[i]);
                    list.Mark(_figure[i], _reason[i]);
                }
            }
        }

        // The first entries the stay allows, and the duty doctor once it is set.
        private bool Offered(int id) => id >= 0 && id < Options && (id < _shown || id == Doctor && _name[Doctor] != null);

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
            if (chosen != null && Offered(id) && _on[id])
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
