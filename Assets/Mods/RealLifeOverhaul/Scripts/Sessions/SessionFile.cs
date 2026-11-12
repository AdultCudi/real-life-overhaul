using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Sessions
{
    // What the sessions keep of a save, and no rules. Game-free: EconomyCheck compiles this file.

    /// <summary>
    /// One kind of session at one business: when it was last held and by whom, whether a manager takes it, the day the
    /// player was last reminded of it, and the effect its last holding left, which the feature reads back while it lasts.
    /// </summary>
    public sealed class SessionRecord
    {
        public string Place = string.Empty;     // the business, as a place key
        public string Session = string.Empty;   // SessionDef.Id
        public int Held = SessionRules.Never;   // the day it was last held
        public bool Delegated;                  // a manager of the business takes it
        public int By;                          // who held it last: 0 the player, 1 a manager
        public int A, B;                        // the effect its last holding left (SessionOutcome.A and B)
        public int Until = SessionRules.Never;  // ...and the day it ends on (not counted)
        public int Times;                       // how often it has been held
    }

    /// <summary>Everything the sessions keep for a save: its seed for the managers' rolls, the day of the last reminder, and the records.</summary>
    public sealed class SessionFile
    {
        public int Seed = 1;
        public int Reminded = SessionRules.Never;   // the day the one reminder of a day last went
        public readonly List<SessionRecord> Records = new List<SessionRecord>(8);

        public SessionRecord Find(string place, string session)
        {
            for (int i = 0; i < Records.Count; i++)
            {
                SessionRecord r = Records[i];
                if (string.Equals(r.Place, place, StringComparison.Ordinal) && string.Equals(r.Session, session, StringComparison.Ordinal))
                {
                    return r;
                }
            }
            return null;
        }

        /// <summary>The record, made when there is none yet.</summary>
        public SessionRecord Get(string place, string session)
        {
            SessionRecord r = Find(place, session);
            if (r == null)
            {
                r = new SessionRecord { Place = place ?? string.Empty, Session = session ?? string.Empty };
                Records.Add(r);
            }
            return r;
        }

        /// <summary>The effect a kind's last holding left at a business while it lasts on a day: false with none, the figures 0.</summary>
        public bool Effect(string place, string session, int day, out int a, out int b)
        {
            SessionRecord r = Find(place, session);
            if (r == null || r.Until == SessionRules.Never || day >= r.Until || day < r.Held)
            {
                a = b = 0;
                return false;
            }
            a = r.A;
            b = r.B;
            return true;
        }

        /// <summary>Records of businesses no longer the player's, and of kinds no feature holds any more, let go; how many.</summary>
        public int Prune(Func<string, bool> keepPlace, Func<string, bool> keepSession)
        {
            int n = 0;
            for (int i = Records.Count - 1; i >= 0; i--)
            {
                SessionRecord r = Records[i];
                if ((keepPlace != null && !keepPlace(r.Place)) || (keepSession != null && !keepSession(r.Session)))
                {
                    Records.RemoveAt(i);
                    n++;
                }
            }
            return n;
        }
    }
}
