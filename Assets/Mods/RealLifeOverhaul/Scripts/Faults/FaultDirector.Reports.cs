using System;

namespace RealLifeOverhaul.Faults
{
    /// <summary>
    /// The City Office's look before it acts on a report (Flare, through Wiring/FaultLinks): a report of a road, a lamp,
    /// crime, litter or an unsafe place is checked against the city's real faults where it was made; a business's
    /// inspection against how clean the business is. Only the city's runner files cases, so only it is asked. The fault a
    /// case found is kept with the case (Flare's FlareCase.Fault, in the save), so a crew sent later, under the paper's
    /// eye, takes the same one. COST: a pass over a dozen faults for a report.
    /// </summary>
    public sealed partial class FaultDirector
    {
        /// <summary>
        /// What a report comes to (FaultVerdict's number), or -1 when the faults do not judge it (switched off, a guest, a
        /// kind they do not know: the City Office answers as it always has). For a real fault its place comes back, so the
        /// crew goes there, and its id when the case may take it (-1 when another case has it in hand already: the report
        /// is true, costs nothing, and the first case keeps the fault). Reports of nothing count against a player (never
        /// the city's people) when the option says so.
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

        /// <summary>A crew is sent on a case answered true: the fault it found is taken (works end it when handed over; a cleaning round or patrols after their time). Nothing for a fault another case has in hand.</summary>
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
