using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Meets the game for companion captures (contracts/companion/v1). A save carries the same report a live reader gets
    /// from <see cref="Companion.CaptureV1"/>; RLO never loads it back into play. Nothing per frame: a capture runs on a
    /// save or a reader's call, and costs one pass over the businesses, the employees and the credit accounts.
    /// </summary>
    internal static class CompanionGame
    {
        private static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Whether a capture or an ownership query may run: this save loaded and wired, outside Going Public. One
        /// comparison or so, nothing per frame.
        /// </summary>
        internal static bool Available(GameInstance game)
        {
            return game != null && ReferenceEquals(game, SaveGameManager.Current) && ModState.Loaded
                && Wiring.Connected && !GoingPublic.InSession;
        }

        /// <summary>The report, or null outside a loaded session or in Going Public. Throws on a fault of the envelope.</summary>
        public static string Capture(GameInstance game)
        {
            CompanionSnapshot.Collected got = Collect(game);
            return got == null ? null : CompanionSnapshot.Render(got, Failed);
        }

        // The readers run now, on this thread (they read the game); the text is written by whoever renders it.
        private static CompanionSnapshot.Collected Collect(GameInstance game)
        {
            if (!Available(game))
            {
                return null;
            }
            return CompanionSnapshot.Collect(Meta.Changelog.Latest,
                CompanionSnapshot.Row("character", game.characterId, "day", game.Day, "hour", game.Hour, "minute", (int)game.Minute),
                CompanionBridges.Readers, Failed);
        }

        /// <summary>As <see cref="Capture"/>, but a fault is logged once and gives null: another mod's call never throws.</summary>
        public static string CaptureSafe(GameInstance game)
        {
            try
            {
                return Capture(game);
            }
            catch (Exception e)
            {
                Failed("capture", e);
                return null;
            }
        }

        /// <summary>
        /// Called after the save handlers and SaveLines.PutBack: the readers run now, before the district save takes the mod's places out of
        /// the game's lists, and the report's text is written on a worker while the district save writes its section (<see cref="End"/> joins it).
        /// No report, or a failed one, removes an older report. A save of 6,900 district records took 930 ms in a player's log (28 Oct 2026),
        /// the report's JSON a share of it, written twice per module and all of it on the main thread.
        /// </summary>
        public static void Begin(GameInstance game)
        {
            _job = null;
            if (game == null)
            {
                return;
            }
            try
            {
                long began = Stopwatch.GetTimestamp();
                CompanionSnapshot.Collected got = Collect(game);
                if (got == null)
                {
                    game.modData?.Remove(CompanionSnapshot.Key);
                    return;
                }
                var job = new Job { Game = game, Got = got, ReadMs = Ms(began, Stopwatch.GetTimestamp()) };
                _job = job;
                try
                {
                    ThreadPool.UnsafeQueueUserWorkItem(Worker, job);
                }
                catch (Exception)
                {
                    // no worker: End writes the text itself
                }
            }
            catch (Exception e)
            {
                _job = null;
                game.modData?.Remove(CompanionSnapshot.Key);
                Failed("capture", e);
            }
        }

        /// <summary>Waits for the report's text (writes it here if no worker has started), sets it into the save, and says the figures with diagnostics on.</summary>
        public static void End(GameInstance game)
        {
            Job job = _job;
            _job = null;
            if (job == null || !ReferenceEquals(job.Game, game))
            {
                return;
            }
            try
            {
                long waited = Stopwatch.GetTimestamp();
                if (Interlocked.CompareExchange(ref job.Taken, 1, 0) == 0)
                {
                    Write(job);
                }
                else
                {
                    job.Done.Wait();
                }
                double waitMs = Ms(waited, Stopwatch.GetTimestamp());
                foreach (KeyValuePair<string, Exception> failure in job.Got.Failed)
                {
                    Failed(failure.Key, failure.Value);
                }
                if (job.Report == null)
                {
                    game.modData?.Remove(CompanionSnapshot.Key);
                    Failed("capture", job.Fault ?? new InvalidOperationException("no report"));
                    return;
                }
                if (game.modData == null)
                {
                    game.modData = new Dictionary<string, string>();
                }
                game.modData[CompanionSnapshot.Key] = job.Report;
                if (Meta.Dev.Verbose)
                {
                    SaveHook.Logger?.Info("Companion: the report of " + (job.Report.Length / 1024) + " KB for the save: the readers "
                                          + job.ReadMs.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                                          + " ms on the main thread (" + job.Got.Slowest(3) + "), the text " + job.WriteMs.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                                          + " ms on a worker beside the district save, waited for " + waitMs.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " ms.");
                }
            }
            catch (Exception e)
            {
                game.modData?.Remove(CompanionSnapshot.Key);
                Failed("capture", e);
            }
        }

        /// <summary>The report put into the save at once: <see cref="Begin"/> and <see cref="End"/> one after the other.</summary>
        public static void Save(GameInstance game)
        {
            Begin(game);
            End(game);
        }

        private sealed class Job
        {
            public GameInstance Game;
            public CompanionSnapshot.Collected Got;
            public string Report;
            public Exception Fault;
            public double ReadMs, WriteMs;
            public int Taken;   // 0 until a worker or End starts the text
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        private static readonly WaitCallback Worker = OnWorker;
        private static Job _job;

        private static void OnWorker(object state)
        {
            var job = (Job)state;
            if (Interlocked.CompareExchange(ref job.Taken, 1, 0) != 0)
            {
                return;   // End took it
            }
            Write(job);
        }

        // The text of the report: plain values only, so any thread may write it. Nothing of the game, nothing logged (End says what failed).
        private static void Write(Job job)
        {
            long began = Stopwatch.GetTimestamp();
            try
            {
                job.Report = CompanionSnapshot.Render(job.Got);
            }
            catch (Exception e)
            {
                job.Fault = e;
            }
            job.WriteMs = Ms(began, Stopwatch.GetTimestamp());
            job.Done.Set();
        }

        private static double Ms(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;

        /// <summary>The player's own established businesses. Type ids stay opaque; a reader declares its own model of them.</summary>
        public static CompanionModule Businesses()
        {
            var rows = new List<object>();
            List<BuildingRegistration> all = SaveGameManager.Current.BuildingRegistrations;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || !GoingPublic.Mine(reg) || !reg.HasEstablishedBusiness || !reg.HasValidAddress)
                {
                    continue;
                }
                rows.Add(CompanionSnapshot.Row("street", reg.StreetName, "number", reg.StreetNumber,
                    "name", reg.BusinessName, "typeId", reg.businessTypeName,
                    "temporarilyClosed", reg.temporarilyClosed));
            }
            return new CompanionModule("ready", rows);
        }

        /// <summary>Forgets which faults were logged, so a new session says each once again.</summary>
        public static void Reset() => Reported.Clear();

        /// <summary>
        /// Says a capability's, a capture's or an ownership query's fault once a session in Player.log; it changes no
        /// state the features read.
        /// </summary>
        internal static void Failed(string capability, Exception error)
        {
            if (Reported.Add(capability))
            {
                SaveHook.Logger?.Warn("Companion: " + capability + " could not be captured: " + error.Message);
            }
        }
    }
}
