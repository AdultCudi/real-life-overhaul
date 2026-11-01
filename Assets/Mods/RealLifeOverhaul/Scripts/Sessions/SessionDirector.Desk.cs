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
        private SessionPanel _panel;
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
            int items = SessionGame.Items(manager);
            if (_deskAt == _here.Place && items == _items)
            {
                return;
            }
            if (_deskAt != _here.Place)
            {
                _forced = null;
            }
            _deskAt = _here.Place;
            _items = items;
            _desk = SessionGame.OwnDesk(manager, reg, out _desks, out _free);
            SessionGame.Target = _desk;
            if (Verbose)
            {
                Logger?.Info("Sessions check (desk): " + _here.Name + ": " + _desks + " computer workstation(s), " + _free + " nobody is rostered at; the owner's desk "
                             + (_desk != null ? "is the first of those by the layout's ids at " + _desk.transform.position.ToString("F1", CultureInfo.InvariantCulture) : "is missing")
                             + ": " + (_desk != null || _free == 0 ? "PASS" : "FAIL"));
            }
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
            _forced = null;
        }

        // A click on the owner's desk: the player walks there, then the game's panel opens on the first kind due.
        private void DeskClicked()
        {
            if (!On || _desk == null || (_panel != null && _panel.Open) || _deskAt == null)
            {
                return;
            }
            _items = -1;
            DeskWatch();   // the week's rota may have changed since the desk was looked for (no item comes or goes for that)
            if (_desk == null)
            {
                return;
            }
            EmployeeStationController desk = _desk;
            DeskGame.WalkTo(desk, () => OpenPanel(desk));
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
            float until = UnityEngine.Time.unscaledTime + WorkWaitSeconds;
            yield return null;
            while (SessionGame.PanelBusy && UnityEngine.Time.unscaledTime < until)
            {
                yield return null;
            }
            if (!SessionGame.PanelBusy && On && ReferenceEquals(desk, _desk))
            {
                SessionBridges.WorkNow(desk);
            }
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
                return false;
            }
            int first = 0;
            for (int i = 0; i < _hereKinds.Count; i++)
            {
                if (Due(_hereKinds[i]))
                {
                    first = i;
                    break;
                }
            }
            var panel = new SessionPanel(this, desk, _hereKinds.ToArray(), first);
            if (!DeskGame.Show(panel))
            {
                return false;
            }
            _panel = panel;
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
            if (SessionRules.Missing(def, _desks, _free, out _) != SessionNeed.None)
            {
                return "reallife_session_status_own_desk";
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
                SessionGame.Tell(NotificationType.Warning, o.Done ? "reallife_session_refused_fault" : o.Key ?? "reallife_session_refused_fault", SessionGame.Data(o.Pairs), 10f);
                return;
            }
            Record(def, _here.Place, ask.Day, false, o);
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
            if (Verbose)
            {
                Logger?.Info("Sessions: " + def.Id + " stopped after " + done + " of " + length + " minutes; nothing held, it stays due.");
            }
        }
    }
}
