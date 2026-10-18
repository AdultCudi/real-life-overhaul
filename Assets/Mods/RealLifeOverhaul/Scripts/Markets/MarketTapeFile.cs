using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The market's tape on disk, a file per playthrough in the game's data folder (RealLifeOverhaul/Market/&lt;id&gt;.tape),
    /// outside every save: lines added as the market goes, the whole written again when the game saves. The writes go to one
    /// writer on the thread pool in order, since a file write with a virus guard looking would stall a frame; a day's
    /// secret is on disk within milliseconds. Before a read and as the market stops, what is on its way is written first.
    /// The whole is swapped in so a crash never leaves no tape (<see cref="MarketTapeSwap"/>), and a read puts back one left
    /// mid-swap. A file that cannot be read or written leaves the market on what it has, with one line in the log.
    /// </summary>
    public static class MarketTapeFile
    {
        public static IModLogger Logger;
        private static bool _warned;

        private struct Job
        {
            public string Path;
            public string Text;
            public string First;    // what a new file begins with
            public bool Whole;      // the whole tape again, in place of what is there
        }

        private static readonly object Gate = new object();
        private static readonly Queue<Job> Jobs = new Queue<Job>(8);
        private static bool _writing;

        // The last tape's path and first line, made once for it on the game's thread rather than at every append.
        private static ulong _namedId;
        private static string _namedPath, _namedFirst;

        public static string PathOf(ulong id)
        {
            Name(id);
            return _namedPath;
        }

        private static void Name(ulong id)
        {
            if (_namedPath == null || _namedId != id)
            {
                _namedPath = Path.Combine(Application.persistentDataPath, "RealLifeOverhaul", "Market", id.ToString("X16") + ".tape");
                _namedFirst = "T1 " + id.ToString("X16") + "\n";
                _namedId = id;
            }
        }

        public static MarketTape Read(ulong id)
        {
            Drain();
            try
            {
                string path = PathOf(id);
                MarketTapeSwap.Settle(path);
                return MarketTape.Read(id, File.Exists(path) ? File.ReadAllText(path) : null);
            }
            catch (Exception e)
            {
                Warn("read", e);
                return new MarketTape(id);
            }
        }

        public static void Append(ulong id, string lines)
        {
            if (string.IsNullOrEmpty(lines))
            {
                return;
            }
            Name(id);
            Enqueue(new Job { Path = _namedPath, Text = lines, First = _namedFirst });
        }

        public static void Rewrite(ulong id, string whole)
        {
            Enqueue(new Job { Path = PathOf(id), Text = whole, Whole = true });
        }

        /// <summary>Waits, two seconds at the most, until every write given so far is on disk.</summary>
        public static void Drain()
        {
            lock (Gate)
            {
                int waits = 0;
                while ((_writing || Jobs.Count > 0) && waits++ < 20)
                {
                    Monitor.Wait(Gate, 100);
                }
            }
        }

        // The path is made by the caller on the game's thread: the data folder is the engine's to say.
        private static void Enqueue(Job job)
        {
            lock (Gate)
            {
                Jobs.Enqueue(job);
                if (_writing)
                {
                    return;
                }
                _writing = true;
            }
            ThreadPool.QueueUserWorkItem(_ => Write());
        }

        // The one writer: the jobs in the order given, until there are none.
        private static void Write()
        {
            while (true)
            {
                Job job;
                lock (Gate)
                {
                    if (Jobs.Count == 0)
                    {
                        _writing = false;
                        Monitor.PulseAll(Gate);
                        return;
                    }
                    job = Jobs.Dequeue();
                }
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(job.Path));
                    if (job.Whole)
                    {
                        MarketTapeSwap.Write(job.Path, job.Text);
                    }
                    else
                    {
                        if (!File.Exists(job.Path))
                        {
                            MarketTapeSwap.Settle(job.Path);   // a tape left mid-swap is added to, never begun again
                        }
                        if (!File.Exists(job.Path))
                        {
                            File.WriteAllText(job.Path, job.First);
                        }
                        File.AppendAllText(job.Path, job.Text);
                    }
                }
                catch (Exception e)
                {
                    Warn(job.Whole ? "write" : "add to", e);
                }
            }
        }

        private static void Warn(string what, Exception e)
        {
            if (!_warned)
            {
                _warned = true;
                Logger?.Warn("Stock market: could not " + what + " the market's tape (" + e.Message
                             + "). The market goes on; a load may replay a day differently until the file can be written again.");
            }
        }
    }
}
