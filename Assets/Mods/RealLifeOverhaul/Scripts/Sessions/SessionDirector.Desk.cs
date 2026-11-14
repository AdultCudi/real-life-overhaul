using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Careers;
using UI.Elements;
using UI.Notification;

namespace RealLifeOverhaul.Sessions
{
    /// <summary>
    /// The owner's desk: in an office of one of the player's firms where a kind of session is held, a computer workstation
    /// nobody is rostered at becomes a thing to click (the game's "Click to hold a session"), and the click walks the player
    /// to it and opens the game's activity panel (<see cref="SessionPanel"/>). What the office lacks is said on the panel.
    /// Nothing is saved: the same room gives the same desk on every visit and machine.
    /// </summary>
    public sealed partial class SessionDirector
    {
        private readonly SessionFirm _here = new SessionFirm();
        private readonly List<SessionDef> _hereKinds = new List<SessionDef>(4);
        private EmployeeStationController _desk;
        private string _deskAt;                  // the place whose desk was looked for this visit
        private int _desks, _free, _items = -1;  // the office's workstations, those nobody is rostered at, and its items when looked
        private int _roster;                     // the week's rota as it was when the desk was looked for (SessionGame.Roster)
        private SessionPanel _panel;
        private GameUi.GuideMark _mark;          // the game's destination marker on the owner's desk while a session waits there
        private string _markName;
        private bool _markDue;                   // a session waits and the marker is wanted
        private readonly HashSet<string> _told = new HashSet<string>(StringComparer.Ordinal);       // places told they have no desk, once a game session
        private readonly HashSet<string> _toldDesk = new HashSet<string>(StringComparer.Ordinal);   // places whose desk was pointed out on this visit
        private const float WorkWaitSeconds = 2f;   // real seconds the work chosen on the panel waits for the game's panel to close
        private string _forced;                  // a kind the test opens due, whatever its record says, until held or the visit ends

        private void DeskWatch()
        {
            if (_panel != null && !_panel.Open)
            {
                _panel = null;
            }
            if (_panel != null && _panel.Open && !DeskGame.Shows(_panel))
            {
                _panel.Finish();   // the game's panel let it go: settled as it stands, and the player stands up
                _panel = null;
            }
            if (!SessionGame.InFirm(_here, out BuildingManager manager, out BuildingRegistration reg))
            {
                if (_deskAt != null)
                {
                    DeskRelease("the player left the office");
                }
                return;
            }
            if (!SessionGame.Install())
            {
                return;
            }
            int items = SessionGame.Items(manager), roster = SessionGame.Roster(reg);
            if (_deskAt == _here.Place && items == _items && roster == _roster)
            {
                return;
            }
            if (_deskAt != _here.Place)
            {
                _forced = null;
            }
            _deskAt = _here.Place;
            _items = items;
            _roster = roster;
            _desk = SessionGame.OwnDesk(manager, reg, out _desks, out _free);
            SessionGame.Target = _desk;
            Logger?.Info("Sessions check (desk): " + _here.Name + ": " + _desks + " computer workstation(s), " + _free + " nobody is rostered at; the owner's desk "
                         + (_desk != null ? "is the first of those by the layout's ids at " + _desk.transform.position.ToString("F1", CultureInfo.InvariantCulture) : "is missing")
                         + ": " + (_desk != null || _free == 0 ? "PASS" : "FAIL"));
            DeskTold();
        }

        /// <summary>
        /// The first words a player gets about the desk, once a visit: where it is (the game's marker on it, and a notice that says a
        /// session waits), or, with none, what to buy or free to have one. Said only for an office where something can be held, so a
        /// firm with nothing to hold is left alone. Never per frame: on going in, and when the furniture or the rota changes.
        /// </summary>
        private void DeskTold()
        {
            SessionRegistry.For(_here.Type, _hereKinds);
            int met = 0, due = 0, shortBy = 0;
            string first = null;
            for (int i = 0; i < _hereKinds.Count; i++)
            {
                SessionDef def = _hereKinds[i];
                if (Blocked(def, Ask(_here, SessionGame.Day, SessionGame.Hour, false, null, 100)) != null)
                {
                    continue;
                }
                met++;
                if (!Due(def))
                {
                    continue;
                }
                due++;
                first = first ?? SessionText.T(def.TitleKey);
                if (SessionRules.Missing(def, _desks, _free, out int missing) == SessionNeed.Desks)
                {
                    shortBy = Math.Max(shortBy, missing);
                }
            }
            if (met == 0)
            {
                Note("desk at " + _here.Name + ": no kind of session can be held here now, so nothing is said");
                return;
            }
            if (_desk == null)
            {
                if (_told.Add(_here.Place))
                {
                    SessionGame.Tell(NotificationType.Warning, "reallife_session_note_no_desk", new Dictionary<string, string>(1) { { "business", _here.Name } }, 20f);
                }
                Note("no desk at " + _here.Name + ": " + _desks + " computer workstation(s) with a chair, none free of the rota; " + met
                     + " kind(s) could be held; the player was told what to buy or free");
                return;
            }
            _markDue = due > 0;
            if (!_markDue)
            {
                _mark?.Hide();
                return;
            }
            if (_toldDesk.Add(_here.Place))
            {
                if (shortBy > 0)
                {
                    SessionGame.Tell(NotificationType.Warning, "reallife_session_status_desks",
                        new Dictionary<string, string>(2) { { "missing", SessionText.Whole(shortBy) }, { "need", SessionText.Whole(_desks + shortBy) } }, 15f);
                }
                SessionGame.Tell(NotificationType.Info, "reallife_session_note_desk",
                    new Dictionary<string, string>(3) { { "business", _here.Name }, { "session", first }, { "count", SessionText.Whole(due) } }, 15f);
            }
            _markName = _markName ?? SessionText.T("reallife_session_mark");
            bool shown = _desk != null && !SessionGame.Near(_desk) && _mark != null && _mark.Show(_desk.transform.position, _markName);
            Note("desk at " + _here.Name + ": " + due + " session(s) due, the desk is "
                 + (shown ? "marked on the game's destination marker" : "within reach or the marker is taken by something else"));
        }

        // The marker stays while a session waits at this desk and the player is not at it yet.
        private bool MarkWanted()
        {
            return On && _desk != null && _deskAt != null && _markDue && (_panel == null || !_panel.Open) && !SessionGame.Near(_desk);
        }

        /// <summary>The owner's desk let go: the panel closed (nothing held), the click taken off.</summary>
        private void DeskRelease(string why)
        {
            if (_panel != null && _panel.Open)
            {
                _panel.Close();
            }
            _panel = null;
            if (_deskAt != null && Verbose)
            {
                Logger?.Info("Sessions: the owner's desk let go (" + why + ").");
            }
            SessionGame.Target = null;
            _desk = null;
            _deskAt = null;
            _items = -1;
            _roster = 0;
            _forced = null;
            _markDue = false;
            _mark?.Hide();
            _toldDesk.Clear();
        }

        // A click on the owner's desk: the game's panel opens at once on the first kind that can be held. The walk to the chair
        // comes after the choices, as the game's own work panel does it; walking to the desk first made the click answer only when the player arrived.
        private void DeskClicked()
        {
            Note("the desk was clicked");
            if (!On || _desk == null || (_panel != null && _panel.Open) || _deskAt == null)
            {
                Note("the click is ignored: " + (!On ? "sessions are off" : _desk == null ? "there is no desk" : _deskAt == null ? "no office" : "a session panel is open already"));
                return;
            }
            _items = -1;
            DeskWatch();   // the rota or the furniture may have changed since the desk was looked for
            if (_desk == null)
            {
                SessionGame.Tell(NotificationType.Warning, "reallife_session_note_no_desk", new Dictionary<string, string>(1) { { "business", _here.Name } }, 20f);
                return;
            }
            if (!OpenPanel(_desk))
            {
                Note("the click opened nothing: another activity is open or the game's panel refused it");
                SessionGame.Tell(NotificationType.Warning, "reallife_session_test_busy", null, 8f);
            }
        }

        /// <summary>
        /// The owner's own work at the desk chosen on the panel (SessionBridges.Work): offered once the game's panel has let the
        /// session go, which it does on its next frames; given up after <see cref="WorkWaitSeconds"/>. A few frames after a press only.
        /// </summary>
        internal void WorkAfter(EmployeeStationController desk)
        {
            StartCoroutine(Guard.Routine(WorkWhenFree(desk)));
        }

        private IEnumerator WorkWhenFree(EmployeeStationController desk)
        {
            float from = UnityEngine.Time.unscaledTime, until = from + WorkWaitSeconds;
            yield return null;
            while (SessionGame.PanelBusy && UnityEngine.Time.unscaledTime < until)
            {
                yield return null;
            }
            bool busy = SessionGame.PanelBusy, same = ReferenceEquals(desk, _desk);
            string waited = busy ? "was still up after " + WorkWaitSeconds + " s" : "let go after " + (UnityEngine.Time.unscaledTime - from).ToString("F2", CultureInfo.InvariantCulture) + " s";
            Note("work here: the game's panel " + waited + ", sessions " + (On ? "on" : "off") + ", the desk " + (same ? "is still the owner's" : "is no longer the owner's")
                 + ", the card " + (Ui.SessionKit.Up ? "is still up" : "is away") + (busy || !On || !same ? ": the work is not offered" : ": the work is offered"));
            if (!busy && On && same)
            {
                SessionBridges.WorkNow(desk);
                yield break;
            }
            SessionGame.Tell(NotificationType.Warning, "reallife_session_note_work_lost", null, 10f);
        }

        private bool OpenPanel(EmployeeStationController desk)
        {
            if (!On || desk == null || !ReferenceEquals(desk, _desk) || (_panel != null && _panel.Open))
            {
                return false;
            }
            SessionRegistry.For(_here.Type, _hereKinds);
            if (_hereKinds.Count == 0)
            {
                // nothing to hold here: said in a notice, and no card or panel opens over an empty page
                Note("the panel was not opened: no kind of session is held at " + _here.Type);
                SessionGame.Tell(NotificationType.Warning, "reallife_session_note_nothing", new Dictionary<string, string>(1) { { "business", _here.Name } }, 10f);
                return true;
            }
            // Only the kinds whose premise is met are drawn (all of them, with their reasons, when none is), and the panel opens on the
            // first that can be held: it used to open on the first never-held kind, whatever it lacked, so a player always landed on the same dead page.
            int n = _hereKinds.Count;
            var met = new bool[n];
            var can = new bool[n];
            var due = new bool[n];
            for (int i = 0; i < n; i++)
            {
                SessionDef def = _hereKinds[i];
                met[i] = Blocked(def, Ask(_here, SessionGame.Day, SessionGame.Hour, false, null, 100)) == null;
                due[i] = Due(def);
                can[i] = met[i] && due[i] && SessionRules.Missing(def, _desks, _free, out _) == SessionNeed.None;
            }
            var pick = new int[n];
            int shown = SessionRules.Series(met, n, pick);
            var defs = new SessionDef[shown];
            var shownCan = new bool[shown];
            var shownDue = new bool[shown];
            for (int i = 0; i < shown; i++)
            {
                defs[i] = _hereKinds[pick[i]];
                shownCan[i] = can[pick[i]];
                shownDue[i] = due[pick[i]];
            }
            int first = SessionRules.FirstPage(shownCan, shownDue, shown);
            var panel = new SessionPanel(this, desk, defs, first);
            if (!panel.HasPage)
            {
                // a page with no words or no way out would open a card with only its title: never opened
                Note("the panel was not opened: the first page has no lines or no buttons (" + shown + " kind(s) drawn)");
                SessionGame.Tell(NotificationType.Warning, "reallife_session_note_nothing", new Dictionary<string, string>(1) { { "business", _here.Name } }, 10f);
                return true;
            }
            if (!DeskGame.Show(panel))
            {
                Note("the panel was not opened: the game's panel is busy or refused it");
                return false;
            }
            _panel = panel;
            _markDue = false;
            _mark?.Hide();
            Note("the panel is open at " + _here.Name + ": " + shown + " of " + n + " kind(s) drawn, opening on " + (first + 1));
            Ui.SessionKit.Show(panel);   // the RLO interface over the panel's pages while they are chosen; the panel keeps the flow and the seat
            return true;
        }

        private bool Due(SessionDef def)
        {
            if (_forced == def.Id)
            {
                return true;
            }
            SessionRecord rec = _file != null ? _file.Find(_here.Place, def.Id) : null;
            return SessionRules.Due(rec != null ? rec.Held : SessionRules.Never, SessionGame.Day, def.EveryDays);
        }

        /// <summary>
        /// The first page's line for a kind here: due, when next, who takes it, what the office lacks or why not; its colour (0
        /// white, 1 yellow, 3 red); whether it can begin now; and the manager button: 1 hand it over, -1 take it back, 0 none.
        /// </summary>
        internal string Status(SessionDef def, out int colour, out bool can, out int hand)
        {
            can = false;
            colour = 0;
            hand = 0;
            SessionRecord rec = _file != null ? _file.Find(_here.Place, def.Id) : null;
            bool there = SessionBridges.ManagerAt(_here.Place, out string manager, out _);
            bool delegated = rec != null && rec.Delegated && def.Delegable;
            if (def.Delegable)
            {
                hand = delegated ? -1 : there ? 1 : 0;
            }
            SessionNeed need = SessionRules.Missing(def, _desks, _free, out int missing);
            if (need == SessionNeed.Desks)
            {
                colour = 3;
                return SessionText.Fill("reallife_session_status_desks", "missing", SessionText.Whole(missing), "need", SessionText.Whole(def.DesksLeast));
            }
            if (need == SessionNeed.OwnDesk)
            {
                colour = 3;
                return SessionText.T("reallife_session_status_own_desk");
            }
            string blocked = Blocked(def, Ask(_here, SessionGame.Day, SessionGame.Hour, false, null, 100));
            if (blocked != null)
            {
                colour = 3;
                return SessionText.T(blocked);
            }
            if (!Due(def))
            {
                int days = SessionRules.DaysToDue(rec.Held, SessionGame.Day, def.EveryDays);
                return delegated && there
                    ? SessionText.Fill("reallife_session_status_manager_next", "manager", manager, "days", SessionText.Whole(days))
                    : SessionText.Fill("reallife_session_status_next", "days", SessionText.Whole(days));
            }
            can = true;
            colour = 1;
            if (delegated && there)
            {
                return SessionText.Fill("reallife_session_status_manager_due", "manager", manager, "hour", def.Hour.ToString("00", CultureInfo.InvariantCulture));
            }
            return delegated ? SessionText.T("reallife_session_status_manager_away") : SessionText.T("reallife_session_status_due");
        }

        /// <summary>Hands a kind here to the business's manager, or takes it back; said in a notice.</summary>
        internal void Hand(SessionDef def)
        {
            if (_file == null || !def.Delegable)
            {
                return;
            }
            SessionRecord rec = _file.Get(_here.Place, def.Id);
            bool there = SessionBridges.ManagerAt(_here.Place, out string manager, out int skill);
            Note((rec.Delegated ? "take back" : "hand to manager") + " pressed for " + def.Id + " at " + _here.Name + ": " + (there ? "a manager is at work" : "no manager at work"));
            if (rec.Delegated)
            {
                rec.Delegated = false;
                SessionGame.Tell(NotificationType.Info, "reallife_session_note_taken",
                    new Dictionary<string, string>(2) { { "session", SessionText.T(def.TitleKey) }, { "business", _here.Name } }, 8f);
                return;
            }
            if (!there)
            {
                SessionGame.Tell(NotificationType.Warning, "reallife_session_note_no_manager", new Dictionary<string, string>(1) { { "business", _here.Name } }, 10f);
                SessionBridges.OpenManagersNow();
                return;
            }
            rec.Delegated = true;
            SessionGame.Tell(NotificationType.Success, "reallife_session_note_handed", new Dictionary<string, string>(4)
            {
                { "session", SessionText.T(def.TitleKey) }, { "business", _here.Name }, { "manager", manager },
                { "strength", SessionText.Whole(SessionRules.Strength(true, skill) / 10) + "%" }
            }, 10f);
        }

        /// <summary>The sound choices for a kind here, as the owner would make them unseen (a Going Public session hides the panel).</summary>
        internal void Sound(SessionDef def, int[] choices)
        {
            SessionRules.SoundChoices(def, Ask(_here, SessionGame.Day, SessionGame.Hour, false, null, 100), choices);
        }

        /// <summary>Why a kind cannot begin now (a key), or null with the minutes its choices take.</summary>
        internal string Begins(SessionDef def, int[] choices, out int minutes)
        {
            minutes = SessionRules.Minutes(def, choices);
            if (!On || _file == null || _desk == null)
            {
                return "reallife_session_refused_off";
            }
            SessionNeed need = SessionRules.Missing(def, _desks, _free, out _);
            if (need != SessionNeed.None)
            {
                return need == SessionNeed.Desks ? "reallife_session_refused_desks" : "reallife_session_status_own_desk";
            }
            string blocked = Blocked(def, Ask(_here, SessionGame.Day, SessionGame.Hour, false, null, 100));
            if (blocked != null)
            {
                return blocked;
            }
            return Due(def) ? null : "reallife_session_refused_not_due";
        }

        /// <summary>The labels shown while it runs: the choices made, one a step.</summary>
        internal LabelInfo[] Summary(SessionDef def, int[] choices)
        {
            var labels = new LabelInfo[def.Steps.Length];
            for (int i = 0; i < def.Steps.Length; i++)
            {
                SessionChoice[] c = def.Steps[i].Choices;
                int at = choices[i] >= 0 && choices[i] < c.Length ? choices[i] : 0;
                labels[i] = DeskGame.Label(SessionText.Fill("reallife_session_label_choice", "choice", SessionText.T(c[at].NameKey), "trade", SessionText.T(c[at].TradeKey)), 0);
            }
            return labels;
        }

        /// <summary>In the chair: in a Going Public session the choices taken unseen are said.</summary>
        internal void Sat(SessionDef def)
        {
            if (GoingPublic.InSession && def.Steps.Length > 0)
            {
                SessionGame.Tell(NotificationType.Info, "reallife_session_note_unseen", new Dictionary<string, string>(1) { { "session", SessionText.T(def.TitleKey) } }, 10f);
            }
        }

        /// <summary>The time sat whole: the feature applies the choices, the record keeps the day and the effect, and the result is told.</summary>
        internal void Held(SessionDef def, int[] choices)
        {
            if (!On || _file == null)
            {
                return;
            }
            SessionAsk ask = Ask(_here, SessionGame.Day, SessionGame.Hour, false, null, 100);
            SessionOutcome o = Apply(def, ask, choices);
            if (!o.Done || string.IsNullOrEmpty(o.Key))
            {
                Note("refused at the end of " + def.Id + " at " + _here.Name + ": " + (o.Done ? "no words to tell it" : o.Key ?? "no reason given") + "; nothing changed, it stays due");
                SessionGame.Tell(NotificationType.Warning, o.Done ? "reallife_session_refused_fault" : o.Key ?? "reallife_session_refused_fault", SessionGame.Data(o.Pairs), 10f);
                return;
            }
            Record(def, _here.Place, ask.Day, false, o);
            Note("held " + def.Id + " at " + _here.Name + " on day " + ask.Day + " with the choices " + string.Join(",", choices)
                 + ": the record is kept, due again on day " + (ask.Day + def.EveryDays));
            if (_forced == def.Id)
            {
                _forced = null;
            }
            var data = SessionGame.Data(o.Pairs);
            data["session"] = SessionText.T(def.TitleKey);
            SessionGame.Tell(NotificationType.Success, o.Key, data, 12f);
            if (Verbose)
            {
                Logger?.Info("Sessions check (held): " + def.Id + " at " + _here.Name + " with the choices " + string.Join(",", choices) + ": held on day " + ask.Day
                             + ", due again on day " + (ask.Day + def.EveryDays) + ", the effect " + o.A + "/" + o.B + " for " + o.Days + " day(s): PASS");
            }
        }

        /// <summary>Stopped before its time was sat: nothing changes and it stays due.</summary>
        internal void Stopped(SessionDef def, int done, int length)
        {
            SessionGame.Tell(NotificationType.Warning, "reallife_session_note_stopped", new Dictionary<string, string>(1) { { "session", SessionText.T(def.TitleKey) } }, 8f);
            Note(def.Id + " stopped after " + done + " of " + length + " minutes; nothing held, it stays due");
        }
    }
}
