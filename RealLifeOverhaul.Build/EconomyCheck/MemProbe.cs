using System;
using System.Diagnostics;

// RLO_CHECK_MEM=1 prints one line each time the suite that is running changes: the checks it ran, the
// managed heap, the working set, the peak working set and the gen 2 collections. RLO_CHECK_MEM=2 also
// collects first, so the heap column is what is still held, not what is waiting to be freed. Off by default.
internal static partial class Program
{
    private static readonly int MemMode = ParseMemMode();
    private static string _memSuite;
    private static int _memRunAtStart;
    private static volatile string _memWhat;
    private static System.Threading.Thread _memWatch;

    // A thread that names the last check that finished when the working set jumps, to find the part of a suite behind a spike.
    private static void MemWatchStart()
    {
        if (_memWatch != null)
        {
            return;
        }
        _memWatch = new System.Threading.Thread(() =>
        {
            long last = 0;
            while (true)
            {
                long ws = Process.GetCurrentProcess().WorkingSet64 / 1048576;
                if (Math.Abs(ws - last) >= 1000)
                {
                    Console.WriteLine("MEMSPIKE ws=" + ws + "MB heap=" + GC.GetTotalMemory(false) / 1048576 + "MB gen2=" + GC.CollectionCount(2) + " after check: " + _memWhat);
                    last = ws;
                }
                System.Threading.Thread.Sleep(100);
            }
        }) { IsBackground = true };
        _memWatch.Start();
    }

    private static int ParseMemMode()
    {
        string v = Environment.GetEnvironmentVariable("RLO_CHECK_MEM");
        return v == "2" ? 2 : string.IsNullOrEmpty(v) || v == "0" ? 0 : 1;
    }

    // Called from Check every so many checks: the suite is the method Main called that is on the stack now.
    private static void MemSample()
    {
        MemWatchStart();
        var trace = new StackTrace(2, false);
        string suite = null;
        for (int i = 0; i < trace.FrameCount; i++)
        {
            var m = trace.GetFrame(i).GetMethod();
            if (m != null && m.Name == "Main" && i > 0)
            {
                suite = trace.GetFrame(i - 1).GetMethod().Name;
                break;
            }
        }
        if (suite == null || suite == _memSuite)
        {
            return;
        }
        MemLine(true);
        _memSuite = suite;
        _memRunAtStart = _run;
    }

    private static void MemLine(bool ended)
    {
        if (_memSuite == null)
        {
            return;
        }
        if (MemMode == 2)
        {
            GC.Collect();
        }
        var p = Process.GetCurrentProcess();
        Console.WriteLine("MEM " + _memSuite + " checks=" + (_run - _memRunAtStart) + " heapMB=" + GC.GetTotalMemory(false) / 1048576
            + " wsMB=" + p.WorkingSet64 / 1048576 + " peakMB=" + p.PeakWorkingSet64 / 1048576 + " gen2=" + GC.CollectionCount(2));
    }
}
