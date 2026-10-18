using System;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// The front desk's doors to features built elsewhere, each optional: a service whose bridge is not set is not offered,
    /// or is offered without the extra. Set from the other feature's own start-up.
    /// </summary>
    public static class Bridges
    {
        /// <summary>The Licences feature's screen, opened from the front desk. Null: the desk does not offer it.</summary>
        public static Action Licensing;

        /// <summary>Optional: the locale key of the desk's entry for it.</summary>
        public static string LicensingLabelKey;

        /// <summary>
        /// Called when the desk hands over a police report, with what it is about (<see cref="BreakIn"/>,
        /// <see cref="CarBreakIn"/>, <see cref="Accident"/>) and its reference; whoever listens gives the item or claim.
        /// </summary>
        public static Action<string, string> PoliceReport;

        public const string BreakIn = "breakin";
        public const string CarBreakIn = "car";
        public const string Accident = "accident";

        /// <summary>
        /// The courts: whether whoever orders holds (<see cref="Custody.Hold"/>) still stands behind them. Null: yes. Read
        /// when a hold begins, eight times a second while one is served, and when one is found on a save.
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

        /// <summary>How many game days a report can still be asked for.</summary>
        public const int ReportDays = 7;

        /// <summary>Something the police can write a report about happened to the player (a break-in, a crash); the newest stands.</summary>
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
