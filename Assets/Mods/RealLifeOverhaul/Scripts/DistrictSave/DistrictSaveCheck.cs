using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// The save's self-check (DistrictSaveScan) on a worker while the main thread writes the mod's section: from the strip
    /// to the end of the section the main thread does nothing but the game's serializer reading the records that left,
    /// and the check reads only what stayed, so neither changes what the other reads. The main thread takes the check
    /// itself if no worker has started it by the time the section is written, and waits for it otherwise: it is always
    /// done before the save goes on, so it never reads the save while the game changes it. COST: nothing per frame; at a
    /// save of a game with a district, the post of one work item and the wait for what is left of the check.
    /// </summary>
    internal static class DistrictSaveCheck
    {
        private static readonly WaitCallback OnWorker = Worker;
        private static readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        private static int _taken;                  // 0 until the check is started, by a worker or the main thread
        private static GameInstance _save;
        private static PlaceSet _places;
        private static DistrictSaveGame.Strip _strip;
        private static int _most;
        private static List<string> _hits;
        private static double _ms;

        /// <summary>Posts the check of the save as it stands after the strip.</summary>
        public static void Start(GameInstance save, PlaceSet places, DistrictSaveGame.Strip strip, int most)
        {
            _save = save;
            _places = places;
            _strip = strip;
            _most = most;
            _hits = null;
            Done.Reset();
            Interlocked.Exchange(ref _taken, 0);   // last, a full fence: a worker that starts it sees everything above
            try
            {
                ThreadPool.UnsafeQueueUserWorkItem(OnWorker, null);
            }
            catch (Exception)
            {
                // no worker: the main thread takes it at the end
            }
        }

        /// <summary>The check's findings (empty when the save names nothing of the mod's) and its own time; on the main thread, before the save goes on.</summary>
        public static List<string> End(out double ms)
        {
            if (Interlocked.CompareExchange(ref _taken, 1, 0) == 0)
            {
                Run();
            }
            else
            {
                Done.Wait();
            }
            ms = _ms;
            List<string> hits = _hits ?? new List<string>(0);
            _save = null;   // nothing of the save held past it
            _strip = null;
            return hits;
        }

        // A work item left from an earlier save that the main thread took finds the next save's check, or nothing to do.
        private static void Worker(object _)
        {
            if (Interlocked.CompareExchange(ref _taken, 1, 0) != 0)
            {
                return;
            }
            Run();
            Done.Set();
        }

        private static void Run()
        {
            long began = Stopwatch.GetTimestamp();
            try
            {
                _hits = DistrictSaveScan.Find(_save, _places, _strip, _most);
            }
            catch (Exception e)
            {
                _hits = new List<string>(1) { "the check itself failed: " + e.GetType().Name + ": " + e.Message };
            }
            _ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
        }
    }
}
