using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// One shift of a week as the planner's numbers. Worker and Station are places in the lists the planner read, or
    /// <see cref="RotaLedger.Unread"/> or <see cref="RotaLedger.Gone"/>.
    /// </summary>
    public struct RotaShift
    {
        public int Day, From, To;       // the weekday (0 Monday) and the hours, the last one not counted
        public int Worker, Station;
    }

    /// <summary>
    /// What a fill may touch in a week that is already there, and what putting the old week back restores: the
    /// decisions of RotaGame.Write and RotaGame.PutBack, kept apart from the game so that they are checked offline.
    /// RotaGame turns the game's shifts into <see cref="RotaShift"/>s, asks here, and does what it is told.
    ///
    /// THE RULES
    ///   left alone     a shift of somebody the planner did not read (a delivery driver, nobody) is never touched; and
    ///                  somebody who is "fixed" keeps every shift they have: whoever holds a desk with the
    ///                  headquarters rule (a fill only ever fills desks that are empty, so nobody loses a desk and with
    ///                  it the plans the game ties to it), and whoever has a shift at a station the planner did not read
    ///   taken out      every other shift of somebody the planner read, so that the new rota is the whole of theirs
    ///   written        the plan's shifts, and none for a station or a person that is not there at that moment, none
    ///                  on a closed day, and none for somebody fixed
    ///   idle           the game's own steps for an employee left with no shift (unassigned from work, the "idle" task)
    ///                  are taken only for somebody who had a shift before and has none now, and never for somebody
    ///                  who holds a headquarters plan: the game has no call that would give such a plan back
    ///   put back       the week kept is restored shift for shift where the person still works there and the station
    ///                  still stands; shifts of those left alone stay as they are today
    ///
    /// Cost: nothing unless a rota is read, written or put back; then one pass over the week's shifts.
    /// </summary>
    public static class RotaLedger
    {
        public const int Unread = -1;   // there in the game, and not among what the planner read: left alone
        public const int Gone = -2;     // no longer in the game (a station sold, somebody who has left)

        /// <summary>Whether a business is within what the planner keeps. Beyond it nothing is read in part and nothing is written.</summary>
        public static bool Fits(int stations, int workers)
        {
            return stations <= RotaCatalogue.StationsMost && workers <= RotaCatalogue.WorkersMost;
        }

        /// <summary>
        /// Who keeps every shift they have: the holder of a station with the whole-week rule, and anybody with a shift
        /// at a station that is there but was not read.
        /// </summary>
        public static void Fixed(List<RotaShift> present, bool[] whole, int workers, bool[] fixedWorker)
        {
            for (int w = 0; w < workers; w++)
            {
                fixedWorker[w] = false;
            }
            for (int i = 0; i < present.Count; i++)
            {
                RotaShift shift = present[i];
                if (shift.Worker >= 0 && shift.Worker < workers && (shift.Station == Unread || (shift.Station >= 0 && whole[shift.Station])))
                {
                    fixedWorker[shift.Worker] = true;
                }
            }
        }

        /// <summary>Whether a fill takes a shift of the present week out.</summary>
        public static bool Removes(RotaShift shift, bool[] fixedWorker)
        {
            return shift.Worker >= 0 && !fixedWorker[shift.Worker];
        }

        /// <summary>
        /// The present week as the planner's grid: the hours of those a fill may move go into <paramref name="at"/>
        /// (the rota it has, to be rated), every other hour at a station the planner read is
        /// <paramref name="taken"/>: somebody stands there and stays.
        /// </summary>
        public static void Lay(List<RotaShift> present, bool[] fixedWorker, int stations, short[] at, bool[] taken)
        {
            for (int i = 0; i < present.Count; i++)
            {
                RotaShift shift = present[i];
                if (shift.Station < 0 || shift.Station >= stations || shift.Day < 0 || shift.Day >= RotaCatalogue.Days)
                {
                    continue;
                }
                bool moves = Removes(shift, fixedWorker);
                for (int h = shift.From < 0 ? 0 : shift.From; h < shift.To && h < RotaCatalogue.Hours; h++)
                {
                    int cell = (shift.Day * RotaCatalogue.Hours + h) * stations + shift.Station;
                    if (moves)
                    {
                        at[cell] = (short)shift.Worker;
                    }
                    else
                    {
                        taken[cell] = true;
                    }
                }
            }
        }

        /// <summary>Somebody fixed is placed nowhere by the planner: they can stand at no station of its problem.</summary>
        public static void Bench(RotaProblem problem, bool[] fixedWorker)
        {
            for (int w = 0; w < problem.Workers; w++)
            {
                for (int s = 0; fixedWorker[w] && s < problem.Stations; s++)
                {
                    problem.Can[w * problem.Stations + s] = 0;
                }
            }
        }

        /// <summary>
        /// The shifts a plan comes to, one for each run of hours of one person at one station. None on a day that
        /// is closed, none for a station or a person that is not there (any more), none for somebody fixed.
        /// </summary>
        public static void Shifts(RotaPlan plan, bool[] dayOpen, bool[] stationThere, bool[] workerThere, bool[] fixedWorker, List<RotaShift> into)
        {
            into.Clear();
            int stations = plan.Stations;
            for (int d = 0; d < RotaCatalogue.Days; d++)
            {
                for (int s = 0; dayOpen[d] && s < stations; s++)
                {
                    int holder = -1, from = 0;
                    for (int h = 0; h <= RotaCatalogue.Hours; h++)
                    {
                        int w = h < RotaCatalogue.Hours && stationThere[s] ? plan.At[(d * RotaCatalogue.Hours + h) * stations + s] : -1;
                        w = w >= 0 && workerThere[w] && !fixedWorker[w] ? w : -1;
                        if (w == holder)
                        {
                            continue;
                        }
                        if (holder >= 0)
                        {
                            into.Add(new RotaShift { Day = d, From = from, To = h, Worker = holder, Station = s });
                        }
                        holder = w;
                        from = h;
                    }
                }
            }
        }

        /// <summary>
        /// A fill on a week of plain shifts: what it takes out is gone, what the plan comes to is added. (RotaGame
        /// does the same to the game's own lists, shift by shift, by the two rules above.)
        /// </summary>
        public static void Write(List<RotaShift> week, RotaPlan plan, bool[] dayOpen, bool[] stationThere, bool[] workerThere, bool[] fixedWorker, List<RotaShift> scratch)
        {
            for (int i = week.Count - 1; i >= 0; i--)
            {
                if (Removes(week[i], fixedWorker))
                {
                    week.RemoveAt(i);
                }
            }
            Shifts(plan, dayOpen, stationThere, workerThere, fixedWorker, scratch);
            week.AddRange(scratch);
        }

        /// <summary>Whether a shift of the week kept goes back in: it is somebody's the planner may place, they still work there and the station still stands.</summary>
        public static bool Restores(RotaShift kept)
        {
            return kept.Worker >= 0 && kept.Station != Gone;
        }

        /// <summary>Whether a shift of today's week stays when the old week is put back: only one of those left alone.</summary>
        public static bool Stays(RotaShift now)
        {
            return now.Worker == Unread;
        }

        /// <summary>Putting a week back on plain shifts: today's shifts of those left alone stay, the kept shifts that can go back do.</summary>
        public static void PutBack(List<RotaShift> kept, List<RotaShift> week)
        {
            for (int i = week.Count - 1; i >= 0; i--)
            {
                if (!Stays(week[i]))
                {
                    week.RemoveAt(i);
                }
            }
            for (int i = 0; i < kept.Count; i++)
            {
                if (Restores(kept[i]))
                {
                    week.Add(kept[i]);
                }
            }
        }

        /// <summary>
        /// Whether the game's steps for an employee with no shift are taken after a fill: only for somebody who had
        /// one and has none now, and who holds no headquarters plan.
        /// </summary>
        public static bool Idles(bool hadShift, bool hasShift, bool holdsPlan)
        {
            return hadShift && !hasShift && !holdsPlan;
        }

        // ---------------------------------------------------------------- the stamp

        public const long StampStart = 1469598103934665603L;

        /// <summary>One more figure into a stamp: a number that changes when anything mixed into it does, or the order of it.</summary>
        public static long Mix(long stamp, int value)
        {
            unchecked
            {
                return (stamp ^ value) * 1099511628211L;
            }
        }

        public static long Mix(long stamp, string text)
        {
            return Mix(stamp, Hash(text));
        }

        public static int Hash(string text)
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    h = h * 31 + text[i];
                }
                return text == null ? 0 : h;
            }
        }
    }
}
