using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One shift of a week in the planner's numbers.</summary>
    public struct RotaShift
    {
        public int Day, From, To;       // the weekday (0 Monday) and the hours, the last one not counted
        public int Worker, Station;     // places in the lists the planner read, or RotaLedger.Unread or Gone
    }

    /// <summary>What a fill may touch in a week already there and what a put-back restores: RotaGame's rules, game-free and checked offline.</summary>
    public static class RotaLedger
    {
        public const int Unread = -1;   // there in the game, and not among what the planner read: left alone
        public const int Gone = -2;     // no longer in the game (a station sold, somebody who has left)

        /// <summary>Whether a business is within what the planner keeps. Beyond it nothing is read in part and nothing is written.</summary>
        public static bool Fits(int stations, int workers)
        {
            return stations <= RotaCatalogue.StationsMost && workers <= RotaCatalogue.WorkersMost;
        }

        /// <summary>Who keeps every shift: the holder of a whole-week station (a fill only fills empty desks), and anybody at a station not read.</summary>
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

        /// <summary>The present week on the grid: hours a fill may move in <paramref name="at"/>, the rest at stations read <paramref name="taken"/>.</summary>
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

        /// <summary>The plan's shifts, a run of hours each; none on a closed day, for a station or person gone, or for somebody fixed.</summary>
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

        /// <summary>A fill on a week of plain shifts, as RotaGame does it to the game's own lists: what it takes out goes, the plan's shifts come.</summary>
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

        /// <summary>Whether a kept shift goes back: one of somebody the planner may place, still there, at a station still there.</summary>
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

        /// <summary>Whether the game's "idle" steps follow a fill: never for a plan holder, as the game cannot give a plan back.</summary>
        public static bool Idles(bool hadShift, bool hasShift, bool holdsPlan)
        {
            return hadShift && !hasShift && !holdsPlan;
        }

        public const long StampStart = 1469598103934665603L;

        /// <summary>One more figure into a stamp: a number that changes when anything mixed into it does, or its order.</summary>
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

        /// <summary>A text's hash; 0 for null, so that a missing id is told from an empty one.</summary>
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
