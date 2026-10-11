using System;
using Helpers;
using RealLifeOverhaul.Law;
using FamilyApi = RealLifeOverhaul.Family.FamilyApi;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// One arrest, one path (the table is in the README, "Who does what at an arrest"):
        ///
        ///   the police code (Crime/PoliceAttention, Traffic/PolicePursuit) catches the player and
        ///       charges the roadside fines once;
        ///   Scripts/Law holds them: cuffed, the cell at the nearest precinct, a line on the record;
        ///   Scripts/Justice opens the case, and is told here how long the cell really was (time
        ///       served comes off any custody the court gives) and how it ended: let out after
        ///       sitting it out, the case goes straight to a court date; bail paid at the precinct
        ///       IS the case's bail (returned on appearing), never a second one;
        ///   Scripts/Licences adds the points from the same police events, and is told by the
        ///       court when a sentence suspends the licence (LicenceLinks).
        ///
        /// Nothing is polled: the police record says when a line is added or closed
        /// (<see cref="Record.Changed"/>), and that is the only thing listened to.
        /// </summary>
        public static class Arrests
        {
            private static readonly LawBridge Bridge = new LawBridge();
            private static JusticeCase _case;      // the case today's arrest belongs to, if the courts are on
            private static bool _watching;         // a stay in a cell that began in this session is being followed
            private static int _day, _charges;     // which line of the record it is
            private static int _hours;             // as sentenced at the precinct: what the case was told
            private static int _bail;              // what walking out at once costs there

            internal static void Connect()
            {
                Forget();
                Justice.Bridges.Law = Bridge;
                Law.Bridges.CourtOn = CourtOn;
                Record.Changed += OnRecord;
            }

            internal static void Disconnect()
            {
                Record.Changed -= OnRecord;
                Justice.Bridges.Law = null;
                Law.Bridges.CourtOn = null;
                Forget();
            }

            // A hold in a station's cell that a court ordered lasts only while the courts are on.
            private static readonly Func<bool> CourtOn = () => Justice.Active;

            private static void Forget()
            {
                _case = null;
                _watching = false;
                _day = _charges = _hours = _bail = 0;
            }

            // A line was added to the police record, or the stay it stands for has ended.
            private static void OnRecord()
            {
                try
                {
                    if (Record.Count == 0)
                    {
                        Forget();
                        return;
                    }
                    RecordEntry latest = Record.Entry(Record.Count - 1);
                    if (latest.Outcome == Law.Outcome.Held)
                    {
                        if (!_watching || latest.Day != _day || latest.Charges != _charges)
                        {
                            Booked(latest);
                        }
                    }
                    else if (_watching || Custody.Active)
                    {
                        Released(latest);
                    }
                }
                catch (Exception e)
                {
                    Fault("Arrests.OnRecord", e);
                }
            }

            // Cuffed: the case opened for the same thing learns how long the cell is, and word gets round.
            private static void Booked(RecordEntry entry)
            {
                _watching = true;
                _day = entry.Day;
                _charges = entry.Charges;
                _hours = entry.Hours;
                _bail = LawBook.Bail(entry.Charges, Record.TimesHeld(LawBook.RepeatDays));
                _case = CaseOf(entry.Day);
                if (_case != null)
                {
                    _case.HeldHours += entry.Hours;
                }
                News.Report(NewsKind.PlayerArrested, PlayerHelper.GetPosition());
                SocialHooks.PlayerArrested();
                FamilyHears(0);
                Logger?.Info("Wiring: held at a precinct for " + entry.Hours + " h" + (_case != null ? "; case " + _case.Number + " is told." : "; no case is open for it."));
            }

            // Out of the cell. What was really served replaces what was sentenced, and the case does not ask for bail a second time.
            private static void Released(RecordEntry entry)
            {
                bool followed = _watching;
                JusticeCase c = followed ? _case : CaseOf(entry.Day);   // (a stay resumed after a load: the case is found by its day)
                int bail = followed ? _bail : LawBook.Bail(entry.Charges, Mathf.Max(0, Record.TimesHeld(LawBook.RepeatDays) - 1));
                int told = _hours;
                Forget();
                if (c == null || !c.Open)
                {
                    return;
                }
                if (followed)
                {
                    c.HeldHours = Mathf.Max(0, c.HeldHours + entry.Hours - told);
                }
                if (entry.Outcome != Law.Outcome.Served && entry.Outcome != Law.Outcome.Bailed)
                {
                    return;   // the arrest was dropped at the roadside: nothing was served, and the court's own bail decision stands
                }
                Justice.ReleasedByPolice(c, entry.Outcome == Law.Outcome.Bailed ? bail : 0);
                Logger?.Info("Wiring: out of the precinct after " + entry.Hours + " h (" + entry.Outcome + "); case " + c.Number + " goes on from there.");
            }

            /// <summary>
            /// The family hears of an arrest, once, as it happens (only if the player has a partner and family life is on): 1 a night in the cells,
            /// 2 when a case has been opened for it, 3 a prison term (pass 0 to have it worked out). Never called for a test button, and not
            /// when a save is loaded, so an arrest that was under way at the save was told when it happened.
            /// </summary>
            internal static void FamilyHears(int severity)
            {
                try
                {
                    if (FamilyApi.HasPartner)
                    {
                        FamilyApi.PlayerArrested(severity > 0 ? severity : CaseOf(TimeHelper.CurrentDay) != null ? 2 : 1);
                    }
                }
                catch (Exception e)
                {
                    Fault("Arrests.FamilyHears", e);
                }
            }

            // The open case an arrest on this day belongs to: one incident is one case, so it is the one opened that day.
            private static JusticeCase CaseOf(int day)
            {
                if (!JusticeState.Loaded)
                {
                    return null;
                }
                for (int i = JusticeState.Cases.Count - 1; i >= 0; i--)
                {
                    JusticeCase c = JusticeState.Cases[i];
                    if (c.Open && c.OpenedDay == day)
                    {
                        return c;
                    }
                }
                return null;
            }

            /// <summary>
            /// What Scripts/Law does for the courts. Its record is the police's (tickets, stays,
            /// escapes) and has no line for a conviction and no way to take one line off; so the
            /// convictions stay the courts' own list (asked together through
            /// <see cref="Standing"/>). What this bridge gives is the cell and the place: hours a
            /// court orders (remand, a term of custody) are served in the nearest precinct's holding
            /// cell (<see cref="Custody.Hold"/>), and somebody let out of prison is put down on the
            /// pavement outside it.
            /// </summary>
            private sealed class LawBridge : ILawBridge
            {
                public int PriorConvictions => -1;

                // The arrest already put its line on the police record and moved the reputation: a second line for the
                // conviction would count the same thing twice.
                public void Convicted(string chargeId, int day, int caseNumber) { }
                public void Cleared(int caseNumber) { }
                public void RecordSealed() { }

                // Remand and a term of custody are served where an arrest is: the nearest precinct's cell. No bail: a court
                // that holds somebody has already refused it, or the player chose to stay. False (custody is off, there is no
                // precinct, somebody is already held) leaves it to the courts' own time skip.
                public bool Custody(int hours, string reasonKey, Action released)
                {
                    try
                    {
                        return Law.Custody.Hold(hours, reasonKey, 0, released);
                    }
                    catch (Exception e)
                    {
                        Fault("Arrests.Custody", e);
                        return false;
                    }
                }

                public bool Holding => Law.Custody.Active || (LawState.Loaded && LawState.Custody.Active);

                public bool ReleaseSpot(out Vector3 spot, out string name)
                {
                    spot = default;
                    name = null;
                    try
                    {
                        Precinct precinct = Settings.LawPrecincts ? LawDirector.NearestPrecinct(PlayerHelper.GetCityPosition()) : null;
                        if (precinct == null)
                        {
                            return false;
                        }
                        if (precinct.Building == null || !JobSpots.Front(precinct.Building, out spot))
                        {
                            spot = precinct.Door;
                        }
                        name = precinct.Name;
                        return true;
                    }
                    catch (Exception e)
                    {
                        Fault("Arrests.ReleaseSpot", e);
                        return false;
                    }
                }
            }
        }
    }
}
