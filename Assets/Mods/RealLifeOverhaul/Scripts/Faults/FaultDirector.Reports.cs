using System;

namespace RealLifeOverhaul.Faults
{
    /// <summary>
    /// The city's faults, part: the City Office's check of a Flare report against the real faults where it was made, and
    /// of a business's inspection against its cleanliness. The fault found is kept with the case, so a crew sent later
    /// takes the same one. Cost: a pass over a dozen faults a report.
    /// </summary>
    public sealed partial class FaultDirector
    {
        /// <summary>
        /// What a report comes to (a <see cref="FaultVerdict"/>), or -1 when the faults do not judge it. For a real fault
        /// its place comes back, and its id when the case may take it (-1 when another case has it in hand). Reports of
        /// nothing count against a player, never the city's people, when the option says so.
        /// </summary>
        public int Check(int report, string district, float x, float z, string reporter, int caseId, string place, out float fx, out float fz, out int fault)
        {
            fx = x;
            fz = z;
            fault = -1;
            if (!_loaded || _failed || !Settings.CityFaults || Following || GoingPublic.IsGuest)
            {
                return -1;
            }
            if (report == BusinessReport)
            {
                int clean = FaultBridges.Cleanliness != null && place != null ? FaultBridges.Cleanliness(place) : -1;
                return clean >= 0 && FaultRules.Clean(clean) ? (int)FaultVerdict.Clean : -1;
            }
            if (!FaultRules.KindOf(report, out FaultKind kind))
            {
                return -1;
            }
            bool player = reporter == "me" || (reporter != null && reporter.StartsWith("p:", StringComparison.Ordinal));
            int day = (int)(FaultGame.Now() / 1440);
            if (player && FaultRules.Barred(_file, reporter, day))
            {
                Logger?.Info("City faults: a report of " + kind + " from " + reporter + " brings no crew: their reports of nothing have stopped the crews for a few days.");
                return (int)FaultVerdict.Barred;
            }
            Fault f = FaultRules.Match(_file, kind, district, x != 0f || z != 0f, x, z);
            if (f != null)
            {
                fx = f.X;
                fz = f.Z;
                fault = FaultRules.Claimable(f) ? f.Id : -1;
                Logger?.Info("City faults: the report of case " + caseId + " is true: the " + f.Kind + " at " + Where(f) + (fault < 0 ? " (a crew has it in hand already)." : "."));
                return (int)FaultVerdict.True;
            }
            if (!player || !Settings.CityFaultsFalseCost)
            {
                Logger?.Info("City faults: the report of case " + caseId + " (" + kind + ") found nothing" + (player ? "; reports of nothing cost nothing (the option)." : "."));
                return (int)FaultVerdict.Nothing;
            }
            FaultVerdict verdict = FaultRules.Nothing(_file, reporter, day);
            Logger?.Info("City faults: the report of case " + caseId + " (" + kind + " from " + reporter + ") found nothing: " + verdict + ".");
            return (int)verdict;
        }

        private const int BusinessReport = 5;   // FlareReport.Business

        /// <summary>A crew is sent on a case answered true: the fault it found is taken, unless another case has it in hand.</summary>
        public void Acted(int caseId, int faultId, bool works)
        {
            Fault f = _loaded && faultId >= 0 ? _file.Find(faultId) : null;
            if (f == null || !FaultRules.Claimable(f))
            {
                return;
            }
            FaultRules.Taken(f, caseId, FaultGame.Now(), works);
            Logger?.Info("City faults: a crew is sent to the " + f.Kind + " at " + Where(f) + (works ? "; it is gone when the works are handed over." : "; it is gone in a while."));
            Changed(true);
        }

        /// <summary>The works of a case are handed over: its fault is mended.</summary>
        public void Done(int caseId)
        {
            Fault f = _loaded ? _file.OfCase(caseId) : null;
            if (f == null)
            {
                return;
            }
            FaultRules.Fixed(_file, f.Id, (int)(FaultGame.Now() / 1440));
            Logger?.Info("City faults: the " + f.Kind + " at " + Where(f) + " is mended (case " + caseId + ").");
            Changed(true);
        }
    }
}
