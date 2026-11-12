using System;
using UI.Notification;

namespace RealLifeOverhaul.Schemes
{
    // The tests (Options, Tests): each situation the owner would otherwise wait days for, made now; each says in a notice what it did or why it could not.
    public sealed partial class SchemeDirector
    {
        private bool TestReady(string test)
        {
            if (_broken || _state == null || Save == null)
            {
                SchemeGame.Tell(NotificationType.Warning, "reallife_scheme_test_no_save", null);
                Logger?.Info("Schemes check (test: " + test + "): no save is loaded, or the schemes stopped after a fault: FAIL");
                return false;
            }
            if (!On)
            {
                SchemeGame.Tell(NotificationType.Warning, "reallife_scheme_test_off", null);
                Logger?.Info("Schemes check (test: " + test + "): the option is off: FAIL");
                return false;
            }
            return true;
        }

        private void TestCarry(int day)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                Carry(_events[i], day);
            }
            Verdicts(day);
        }

        /// <summary>Test: a small invented expense that moved no money goes on file, and an inquiry into it opens now with its letter.</summary>
        public void TestInquiry()
        {
            if (!TestReady("inquiry"))
            {
                return;
            }
            try
            {
                int day = SchemeGame.Day;
                ReadWorld(day);
                _events.Clear();
                if (SchemeRules.Count(_state, SchemeKind.Expense, day) == 0)
                {
                    SchemeRules.Record(_state, SchemeKind.Expense, day, 0, SchemeCatalogue.Small, 0, true, string.Empty, 0);
                }
                SchemeInquiry q = SchemeRules.OpenNow(_state, SchemeKind.Expense, day, _events);
                TestCarry(day);
                SchemeGame.Tell(q != null ? NotificationType.Info : NotificationType.Warning, q != null ? "reallife_scheme_test_inquiry_done" : "reallife_scheme_test_inquiry_open", null);
                Logger?.Info("Schemes check (test: inquiry): " + (q != null ? "inquiry " + q.Id + " opened with its letter; the Legal file lists it: PASS"
                    : "an inquiry into the return is open already: FAIL"));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Test: every open inquiry plays one more day now: the visit, the findings and the court come in their order.</summary>
        public void TestStep()
        {
            if (!TestReady("a day of the inquiries"))
            {
                return;
            }
            try
            {
                int day = SchemeGame.Day;
                ReadWorld(day);
                _events.Clear();
                int open = SchemeRules.Hurry(_state, day, _world, _events);
                TestCarry(day);
                SchemeGame.Tell(open > 0 ? NotificationType.Info : NotificationType.Warning, open > 0 ? "reallife_scheme_test_step_done" : "reallife_scheme_test_step_none",
                    Data("count", SchemeText.Whole(open)));
                Logger?.Info("Schemes check (test: a day of the inquiries): " + open + " inquiry(ies) played a day, " + _events.Count + " event(s): " + (open > 0 ? "PASS" : "FAIL, none is open"));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Test: a rival offers the player a fund now, whatever the wait since the last.</summary>
        public void TestFund()
        {
            if (!TestReady("a rival's fund"))
            {
                return;
            }
            try
            {
                int day = SchemeGame.Day;
                ReadWorld(day);
                _events.Clear();
                bool offered = SchemeRules.FundNow(_state, day, _world, _events);
                TestCarry(day);
                if (!offered)
                {
                    SchemeGame.Tell(NotificationType.Warning, "reallife_scheme_test_no_rival", null);
                }
                Logger?.Info("Schemes check (test: a rival's fund): " + (offered ? "offered by rival " + _state.Fund.Tycoon + " for " + _state.Fund.Amount
                    + (_state.Fund.Sham ? ", a sham" : ", honest") + ": PASS" : "no rival is free to offer one, a session is on, or one is open already: FAIL"));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Test: the first rival who has come forward is found out today, and is tried three days on; the paper prints it in the morning.</summary>
        public void TestRival()
        {
            if (!TestReady("a rival found out"))
            {
                return;
            }
            try
            {
                int day = SchemeGame.Day, tycoon = -1;
                ReadWorld(day);
                _events.Clear();
                for (int i = 0; i < _world.RivalActive.Length && tycoon < 0; i++)
                {
                    tycoon = _world.RivalActive[i] ? i : tycoon;
                }
                bool done = tycoon >= 0 && !_world.Session && SchemeRules.RivalNow(_state, tycoon, day, _world, _events);
                TestCarry(day);
                SchemeGame.Tell(done ? NotificationType.Info : NotificationType.Warning, done ? "reallife_scheme_test_rival_done" : "reallife_scheme_test_no_rival",
                    Data("rival", done ? RivalName(tycoon) : string.Empty));
                Logger?.Info("Schemes check (test: a rival found out): " + (done ? RivalName(tycoon) + " was found out and is tried on day " + _state.Rival[tycoon].TrialDay + ": PASS"
                    : "no rival has come forward, or the first is before the court already: FAIL"));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Test: where every trail stands is written to the log: its heat, the line an inquiry opens at today, what is on file and the cash in hand.</summary>
        public void TestStanding()
        {
            if (!TestReady("standing"))
            {
                return;
            }
            int day = SchemeGame.Day;
            var b = new System.Text.StringBuilder(240);
            for (int k = 0; k < SchemeCatalogue.Kinds; k++)
            {
                var kind = (SchemeKind)k;
                b.Append(k > 0 ? "; " : string.Empty).Append(kind).Append(" heat ").Append(_state.Heat[k]).Append(" against a line of ")
                    .Append(SchemeRules.Line(_state.Seed, kind, day, _state.Inquiries[k])).Append(", ").Append(SchemeRules.Count(_state, kind, day)).Append(" on file owing ")
                    .Append(SchemeRules.Owed(_state, kind, day)).Append(_state.OpenOf(kind) != null ? ", an inquiry open" : string.Empty);
            }
            Logger?.Info("Schemes check (test: standing): day " + day + ": " + b + "; cash in hand " + _state.Dirty + (_state.Watched ? " (watched)" : string.Empty) + ": PASS");
            SchemeGame.Tell(NotificationType.Info, "reallife_scheme_test_standing_done", null);
        }
    }
}
