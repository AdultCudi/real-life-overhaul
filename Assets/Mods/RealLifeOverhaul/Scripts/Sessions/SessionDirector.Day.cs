using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Sessions
{
    /// <summary>
    /// The sessions' hours: on each turn of the game's hour, every kind due at each of the player's firms is held by the
    /// firm's manager if it is handed to one who is at work (from the kind's own hour), else it waits, and once a day from
    /// the reminder's hour one gentle notice says what waits. Records of kinds no longer held are let go.
    /// </summary>
    public sealed partial class SessionDirector
    {
        private readonly List<SessionFirm> _firms = new List<SessionFirm>(8), _firmPool = new List<SessionFirm>(8);
        private readonly List<SessionDef> _kinds = new List<SessionDef>(4);
        private int[] _picks = new int[4];

        private void HourTurn(int day, int hour)
        {
            if (_file == null || SessionRegistry.All.Count == 0)
            {
                return;
            }
            SessionGame.Firms(_firms, _firmPool);
            // Kinds no feature holds any more are let go. A business's records stay while it is not read as the player's: a
            // load still building the city's list must not cost the player what they held, and a sold firm's few cost nothing.
            int pruned = _file.Prune(null, s => SessionRegistry.Find(s) != null);
            if (pruned > 0)
            {
                Logger?.Info("Sessions: " + pruned + " record(s) of kinds of session no longer held let go.");
            }
            int waiting = 0, held = 0;
            SessionFirm firstFirm = null;
            SessionDef firstKind = null;
            for (int f = 0; f < _firms.Count; f++)
            {
                SessionFirm firm = _firms[f];
                SessionRegistry.For(firm.Type, _kinds);
                for (int k = 0; k < _kinds.Count; k++)
                {
                    SessionDef def = _kinds[k];
                    SessionRecord rec = _file.Find(firm.Place, def.Id);
                    int last = rec != null ? rec.Held : SessionRules.Never;
                    if (!SessionRules.Due(last, day, def.EveryDays))
                    {
                        continue;
                    }
                    SessionAsk ask = Ask(firm, day, hour, false, null, 100);
                    if (Blocked(def, ask) != null)
                    {
                        continue;   // nothing to hold it for yet (no staff): no manager holds it and nobody is reminded
                    }
                    bool there = SessionBridges.ManagerAt(firm.Place, out string manager, out int skill);
                    if (SessionRules.ManagerHolds(rec != null && rec.Delegated && def.Delegable, there, true, hour, def.Hour))
                    {
                        held += HoldByManager(def, firm, day, hour, manager, skill) ? 1 : 0;
                        continue;
                    }
                    if (rec != null && rec.Delegated && def.Delegable && there)
                    {
                        continue;   // the manager holds it at its hour: nothing waits for the player
                    }
                    waiting++;
                    if (firstFirm == null)
                    {
                        firstFirm = firm;
                        firstKind = def;
                    }
                }
            }
            if (SessionRules.Remind(waiting, _file.Reminded, day, hour))
            {
                _file.Reminded = day;
                Note("reminder on day " + day + ": " + waiting + " session(s) wait, first " + firstKind.Id + " at " + firstFirm.Name);
                SessionGame.Tell(NotificationType.Info, waiting > 1 ? "reallife_session_note_waiting_many" : "reallife_session_note_waiting",
                    new Dictionary<string, string>(3) { { "session", SessionText.T(firstKind.TitleKey) }, { "business", firstFirm.Name }, { "count", SessionText.Whole(waiting) } }, 10f);
            }
            if (Verbose && (held > 0 || waiting > 0))
            {
                Logger?.Info("Sessions: day " + day + ", " + hour.ToString("00") + ":00: " + held + " held by managers, " + waiting + " waiting for you, over " + _firms.Count + " firm(s).");
            }
        }

        private static SessionAsk Ask(SessionFirm firm, int day, int hour, bool byManager, string manager, int skill)
        {
            return new SessionAsk { Place = firm.Place, Name = firm.Name, Type = firm.Type, Day = day, Hour = hour, ByManager = byManager, Manager = manager ?? string.Empty, Skill = skill };
        }

        // Why a kind cannot be held at a firm now (a key), or null; a fault in the feature's own test counts as refused.
        private static string Blocked(SessionDef def, SessionAsk ask)
        {
            if (def.Blocked == null)
            {
                return null;
            }
            try
            {
                return def.Blocked(ask);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return "reallife_session_refused_fault";
            }
        }

        // A manager holds a session: their choices by skill, the feature's callback, the record, the notice.
        private bool HoldByManager(SessionDef def, SessionFirm firm, int day, int hour, string manager, int skill)
        {
            SessionAsk ask = Ask(firm, day, hour, true, manager, skill);
            if (_picks.Length < def.Steps.Length)
            {
                _picks = new int[def.Steps.Length];
            }
            int mistakes = SessionRules.ManagerChoices(def, ask, _file.Seed, KindIndex(def), _picks);
            SessionOutcome o = Apply(def, ask, _picks);
            if (!o.Done || string.IsNullOrEmpty(o.Key))
            {
                return false;
            }
            Record(def, firm.Place, day, true, o);
            string title = SessionText.T(def.TitleKey);
            string result = SessionText.Fill(o.Key, o.Pairs).Replace("{session}", title);   // the feature's own words, said after the manager's name
            SessionGame.Tell(NotificationType.Success, "reallife_session_note_manager",
                new Dictionary<string, string>(3) { { "manager", manager }, { "session", title }, { "result", result } }, 12f);
            if (Verbose)
            {
                Logger?.Info("Sessions check (manager): " + def.Id + " at " + firm.Name + " held by " + manager + " at skill " + skill + ", " + mistakes + " step(s) by mistake against the chance "
                             + Economy.ManagerRules.MistakeChance(skill, 0) + "/1000; the record's day " + day + " is the day held: PASS");
            }
            return true;
        }

        private static int KindIndex(SessionDef def)
        {
            IReadOnlyList<SessionDef> all = SessionRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (ReferenceEquals(all[i], def))
                {
                    return i;
                }
            }
            return 0;
        }

        // The feature's callback, guarded: a fault holds nothing and says why.
        private static SessionOutcome Apply(SessionDef def, SessionAsk ask, int[] choices)
        {
            try
            {
                return def.Apply(ask, choices);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return new SessionOutcome { Done = false, Key = "reallife_session_refused_fault" };
            }
        }

        // A session held: its day, who held it and the effect it left.
        private void Record(SessionDef def, string place, int day, bool byManager, SessionOutcome o)
        {
            SessionRecord rec = _file.Get(place, def.Id);
            rec.Held = day;
            rec.By = byManager ? 1 : 0;
            rec.A = o.A;
            rec.B = o.B;
            rec.Until = o.Days > 0 ? SessionRules.Until(day, o.Days) : SessionRules.Never;
            rec.Times++;
        }
    }
}
