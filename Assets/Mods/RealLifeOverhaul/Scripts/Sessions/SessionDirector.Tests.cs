using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;
using UI.Notification;

namespace RealLifeOverhaul.Sessions
{
    /// <summary>
    /// The desk sessions' entries in Options, Tests: the weekly team meeting opened at the owner's desk of the firm the
    /// player stands in (walked to, the meeting made due), or the nearest firm marked with why; and the sessions handed to
    /// managers held now, each by its own rules. Each says in a notice why it cannot run. Only the player's own firms.
    /// </summary>
    public sealed partial class SessionDirector
    {
        /// <summary>Opens the weekly team meeting at the owner's desk here, or says why not and marks the nearest firm of the player's.</summary>
        public void TestMeeting()
        {
            try
            {
                string why = TestReady();
                if (why != null)
                {
                    SessionGame.Tell(NotificationType.Warning, why, null, 8f);
                    return;
                }
                SessionDef def = SessionRegistry.Find(MeetingRules.Id);
                if (def == null)
                {
                    SessionGame.Tell(NotificationType.Warning, "reallife_session_test_no_kind", null, 8f);
                    return;
                }
                if (!SessionGame.InFirm(_here, out _, out _) || !def.HeldAt(_here.Type))
                {
                    SessionGame.Firms(_firms, _firmPool);
                    SessionFirm nearest = SessionGame.Nearest(Kinds(_firms, def));
                    if (nearest == null)
                    {
                        SessionGame.Tell(NotificationType.Warning, "reallife_session_test_no_firm", null, 10f);
                        return;
                    }
                    SessionGame.Route(nearest);
                    SessionGame.Tell(NotificationType.Info, "reallife_session_test_go", new Dictionary<string, string>(1) { { "business", nearest.Name } }, 10f);
                    return;
                }
                DeskWatch();
                SessionNeed need = SessionRules.Missing(def, _desks, _free, out int missing);
                if (_desk == null || need != SessionNeed.None)
                {
                    SessionGame.Tell(NotificationType.Warning, need == SessionNeed.Desks ? "reallife_session_test_desks" : "reallife_session_status_own_desk",
                        new Dictionary<string, string>(2) { { "missing", SessionText.Whole(missing) }, { "need", SessionText.Whole(def.DesksLeast) } }, 10f);
                    return;
                }
                if (_panel != null && _panel.Open || SessionGame.PanelBusy)
                {
                    SessionGame.Tell(NotificationType.Warning, "reallife_session_test_busy", null, 8f);
                    return;
                }
                _forced = def.Id;   // due now for this visit, whatever its record says, so the test can be pressed again
                if (!OpenPanel(_desk))
                {
                    SessionGame.Tell(NotificationType.Warning, "reallife_session_test_busy", null, 8f);
                    return;
                }
                Note("test: the weekly team meeting at " + _here.Name + " made due and its panel opened.");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Holds now every due session handed to a manager who is at work, by the managers' own rules; says how many, or why none.</summary>
        public void TestManagers()
        {
            try
            {
                string why = TestReady();
                if (why != null)
                {
                    SessionGame.Tell(NotificationType.Warning, why, null, 8f);
                    return;
                }
                int day = SessionGame.Day, held = 0, handed = 0;
                SessionGame.Firms(_firms, _firmPool);
                for (int f = 0; f < _firms.Count; f++)
                {
                    SessionFirm firm = _firms[f];
                    SessionRegistry.For(firm.Type, _kinds);
                    for (int k = 0; k < _kinds.Count; k++)
                    {
                        SessionDef def = _kinds[k];
                        SessionRecord rec = _file.Find(firm.Place, def.Id);
                        if (rec == null || !rec.Delegated || !def.Delegable)
                        {
                            continue;
                        }
                        handed++;
                        if (!SessionRules.Due(rec.Held, day, def.EveryDays) || Blocked(def, Ask(firm, day, def.Hour, false, null, 100)) != null
                            || !SessionBridges.ManagerAt(firm.Place, out string manager, out int skill))
                        {
                            continue;
                        }
                        held += HoldByManager(def, firm, day, Math.Max(def.Hour, SessionGame.Hour), manager, skill) ? 1 : 0;
                    }
                }
                SessionGame.Tell(held > 0 ? NotificationType.Success : NotificationType.Warning,
                    handed == 0 ? "reallife_session_test_none_handed" : held == 0 ? "reallife_session_test_none_due" : "reallife_session_test_held",
                    new Dictionary<string, string>(1) { { "count", SessionText.Whole(held) } }, 10f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // Why a test cannot run now (a key), or null.
        private string TestReady()
        {
            if (!Settings.LivingPopulation)
            {
                return "reallife_session_test_living_off";
            }
            if (!Settings.LivingDesks)
            {
                return "reallife_session_test_off";
            }
            if (_broken || _file == null || Save == null || !ModState.Loaded)
            {
                return "reallife_session_test_not_ready";
            }
            return null;
        }

        // The firms where a kind is held, as a list of the caller's own (made on a press).
        private static List<SessionFirm> Kinds(List<SessionFirm> firms, SessionDef def)
        {
            var at = new List<SessionFirm>(firms.Count);
            for (int i = 0; i < firms.Count; i++)
            {
                if (def.HeldAt(firms[i].Type))
                {
                    at.Add(firms[i]);
                }
            }
            return at;
        }
    }
}
