using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>A shift of the rota planner's plan on one day, by the ids of its station and its person.</summary>
    internal struct RotaGapShift
    {
        public int From, To;            // the hours, the last one not counted
        public string Station, Worker;  // the game's ids
        public bool Cleaning;           // a cleaning shift: it may stand outside the opening hours
    }

    /// <summary>
    /// The planner's plan for one day, for BizMan's Fill the gaps: the plan its rota page speaks of and a place's manager
    /// writes (RotaSheet.Shown), as shifts with the game's ids. Nothing is written here: BizMan writes those that fit into
    /// the hours nobody works through the game's own ScheduleHelper, and leaves every shift already placed as it is.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private readonly List<RotaShift> _gapPlan = new List<RotaShift>(64);
        private static readonly bool[] EveryDay = { true, true, true, true, true, true, true };

        /// <summary>
        /// The sheet's plan on a weekday (0 Monday) as shifts; none for somebody the planner leaves as they are (a desk holder,
        /// or one on a station it did not read). The sheet must be fresh (RotaSheetFor), so its stations and people are there.
        /// </summary>
        internal void RotaDayShifts(RotaSheet sheet, int day, List<RotaGapShift> into)
        {
            into.Clear();
            RotaPlan plan = sheet != null ? sheet.Shown : null;
            int stations = sheet != null ? sheet.Stations.Count : 0, workers = sheet != null ? sheet.Workers.Count : 0;
            if (plan == null || day < 0 || day >= RotaCatalogue.Days || plan.Stations != stations || sheet.Fixed.Length < workers
                || plan.At.Length < RotaCatalogue.Cells * stations)
            {
                return;
            }
            var stationThere = new bool[stations];
            var workerThere = new bool[workers];
            for (int s = 0; s < stations; s++)
            {
                stationThere[s] = sheet.Stations[s] != null;
            }
            for (int w = 0; w < workers; w++)
            {
                workerThere[w] = sheet.Workers[w] != null;
            }
            RotaLedger.Shifts(plan, EveryDay, stationThere, workerThere, sheet.Fixed, _gapPlan);
            for (int i = 0; i < _gapPlan.Count; i++)
            {
                RotaShift shift = _gapPlan[i];
                if (shift.Day != day || shift.Station < 0 || shift.Worker < 0 || shift.From >= shift.To)
                {
                    continue;
                }
                into.Add(new RotaGapShift
                {
                    From = shift.From, To = shift.To, Station = sheet.Stations[shift.Station].id, Worker = sheet.Workers[shift.Worker].id,
                    Cleaning = sheet.Problem.Kind[shift.Station] == RotaKind.Cleaning
                });
            }
            _gapPlan.Clear();
        }
    }
}
