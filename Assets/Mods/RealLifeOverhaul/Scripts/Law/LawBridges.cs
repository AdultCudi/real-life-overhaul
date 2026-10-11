using System;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// The front desk's doors to features that are built elsewhere. Every one is optional and
    /// null-safe: a service whose bridge is not set is simply not offered (licences), or is
    /// offered without the extra (the police report still gives its reference number).
    /// Set from the other feature's own start-up; never read per frame.
    /// </summary>
    public static class Bridges
    {
        /// <summary>
        /// The Licences feature: called when the player asks at the front desk to apply for a
        /// licence (it opens whatever screen of its own it has). Null: the desk does not offer it.
        /// </summary>
        public static Action Licensing;

        /// <summary>Optional: the locale key of the desk's button for it (the default reads "Apply for a licence or permit").</summary>
        public static string LicensingLabelKey;

        /// <summary>
        /// Paperwork: called when the desk hands over a police report, with what it is about
        /// (<see cref="BreakIn"/>, <see cref="CarBreakIn"/>, <see cref="Accident"/>) and its reference number. Whoever
        /// listens gives the item or the insurance claim that goes with it. Null: the report is the reference alone.
        /// </summary>
        public static Action<string, string> PoliceReport;

        public const string BreakIn = "breakin";
        public const string CarBreakIn = "car";
        public const string Accident = "accident";

        /// <summary>
        /// The courts: asked whether whoever orders holds (<see cref="Custody.Hold"/>) still stands behind them. Null: yes.
        /// Set by Scripts/Wiring; read when a hold begins, with the clock line while one is served (eight times a second), and when one is found on a save.
        /// </summary>
        public static Func<bool> CourtOn;

        /// <summary>False when the courts have been switched off or have gone: a hold of theirs is not begun, and one that is on ends.</summary>
        public static bool CourtStands
        {
            get
            {
                Func<bool> asked = CourtOn;
                if (asked == null)
                {
                    return true;
                }
                try
                {
                    return asked();
                }
                catch (Exception)
                {
                    return false;   // nobody is held on the word of something that has just failed
                }
            }
        }

        /// <summary>How many game days a report can still be asked for after what it is about.</summary>
        public const int ReportDays = 7;

        /// <summary>
        /// Something happened to the player that the police can write a report about (a break-in at one of
        /// their shops, their parked car broken into, a crash). One line for whoever stages it; the newest stands.
        /// </summary>
        public static void NoteIncident(string kind)
        {
            if (!LawState.Loaded || string.IsNullOrEmpty(kind) || SaveGameManager.Current == null)
            {
                return;
            }
            LawState.IncidentKind = kind;
            LawState.IncidentDay = TimeHelper.CurrentDay;
        }

        /// <summary>True if there is something from the last few days that a report can be asked for.</summary>
        public static bool HasIncident =>
            LawState.Loaded && !string.IsNullOrEmpty(LawState.IncidentKind) && SaveGameManager.Current != null
            && TimeHelper.CurrentDay - LawState.IncidentDay <= ReportDays;
    }
}
