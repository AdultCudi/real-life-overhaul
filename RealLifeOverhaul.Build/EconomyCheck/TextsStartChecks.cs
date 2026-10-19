using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using RealLifeOverhaul.Texts;

// The runner's start ends by its limits for certain (the fault: the start on an integrated GPU hung
// about seven minutes with no line, although the load has 90 s and the first reply 30 s): every wait on the server bounded
// by the runner's own clock (Socket.Poll, not the socket's ReceiveTimeout, which Mono on Windows does not honour), each
// stage logged with its time, and a watchdog on a timer that ends a stage stuck past its limit. Proved against a pretend
// llama-server (this checker started by the runner as its server: the "-m" argument) that takes every connection and never
// answers, and one that loads but never answers the first reply.
internal static partial class Program
{
    private const double StartLimit = 2.0;   // seconds of load and first reply the pretend runs are given

    private static void TextsStart()
    {
        TextsStartWatch();
        TextsStartPretend("silent", "a server that takes the connection and never answers (the model never loads)", "took longer than 2 s to load");
        TextsStartPretend("loads", "a server that loads but never answers the first reply (a GPU building its shaders for ever)", "the first reply failed: too slow");
        string server = TextsSource(@"Texts\TextServer.cs");
        Check(server.Contains("client.Client.Poll(") && !server.Contains("net.Read(buffer, 0, buffer.Length)) > 0"),
            "texts, the start: every read from the server waits through Socket.Poll on the runner's own clock, never on the socket's ReceiveTimeout alone");
    }

    private static void TextsStartWatch()
    {
        var w = new TextStartWatch();
        w.Enter(TextStartStage.Load, 90, 10);
        Check(!w.Overdue(10 + 90 + TextStartWatch.Grace - 0.1, out _, out _), "texts, the start's watch: a stage within its limit and the grace is left to run");
        Check(w.Overdue(10 + 90 + TextStartWatch.Grace + 0.1, out TextStartStage stage, out double limit) && stage == TextStartStage.Load && Math.Abs(limit - 90) < 1e-9 && w.Over,
            "texts, the start's watch: a stage past its limit and the grace is overdue, named with its limit");
        Check(!w.Overdue(1000, out _, out _), "texts, the start's watch: overdue once only (one line)");
        var x = new TextStartWatch();
        x.Enter(TextStartStage.WarmUp, 30, 0);
        double took = x.Leave(12.5);
        Check(Math.Abs(took - 12.5) < 1e-9 && !x.Overdue(1000, out _, out _) && !x.Over, "texts, the start's watch: a stage that ended is never overdue, and its time is told");
        Check(TextStartWatch.Name(TextStartStage.Load) == "loading the model" && TextStartWatch.Name(TextStartStage.Speed) == "the speed test",
            "texts, the start's watch: each stage has its name for the log");
    }

    // Windows' Application Control (Smart App Control) refuses to start an unsigned program with no reputation, such as a
    // checker just built: error 4551. Only that is a skip; any other failure to start stays a failure of the check.
    private static bool Blocked(string exe)
    {
        try
        {
            using (Process p = Process.Start(new ProcessStartInfo(exe, "--probe-start") { UseShellExecute = false, CreateNoWindow = true }))
            {
                p.WaitForExit(10000);
            }
            return false;
        }
        catch (System.ComponentModel.Win32Exception e) when (e.NativeErrorCode == 4551)
        {
            return true;
        }
    }

    // The runner started on a pretend server: it must fail by its limits with its line, and the pretend server must be ended.
    private static void TextsStartPretend(string mode, string what, string stage)
    {
        string exe = System.IO.Path.Combine(AppContext.BaseDirectory, "EconomyCheck.exe");
        Check(File.Exists(exe), "texts, the start: the checker can be started as a pretend server (" + exe + ")");
        if (!File.Exists(exe))
        {
            return;
        }
        if (Blocked(exe))
        {
            Console.WriteLine("  SKIPPED texts, the start on a pretend server (" + mode + "): Windows' Application Control (Smart App Control) does not let the freshly built checker start as a program on this PC; the rest of the checks run");
            return;
        }
        string model = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo-pretend-" + mode + "-" + Process.GetCurrentProcess().Id + ".gguf");
        File.WriteAllText(model, mode);
        var before = new HashSet<int>();
        foreach (Process p in Process.GetProcessesByName("EconomyCheck"))
        {
            before.Add(p.Id);
        }
        var log = new List<string>();
        var q = new TextQueue();
        var sw = Stopwatch.StartNew();
        using (var server = new TextServer(q, new TextServerStart { Exe = exe, Model = model, Threads = 2, LoadSeconds = StartLimit, WarmSeconds = StartLimit, SpeedFor = "check" },
                   l => { lock (log) { log.Add(l); } }))
        {
            server.Start();
            double cap = TextServer.LaunchSeconds + StartLimit * 2 + TextStartWatch.Grace * 2 + 5;   // (the check's own wait: past every limit the runner has)
            while (server.State == TextServerState.Loading && sw.Elapsed.TotalSeconds < cap)
            {
                Thread.Sleep(50);
            }
            double ended = sw.Elapsed.TotalSeconds;
            string lines;
            lock (log)
            {
                lines = string.Join(" | ", log);
            }
            Check(server.State == TextServerState.Failed && ended < StartLimit + TextStartWatch.Grace + 3,
                "texts, the start: " + what + " ends the start by its limit (" + server.State + " after " + ended.ToString("0.0") + " s; " + lines + ")");
            Check(lines.Contains(stage) && lines.Contains("the written lines are used.") && lines.Contains("RLO Texts: start-up, starting the server in ") && (stage.StartsWith("took", StringComparison.Ordinal) || lines.Contains("RLO Texts: start-up, loading the model in ")),
                "texts, the start: the stage it stopped at is logged with the written lines used, and the stages before it with their times (" + lines + ")");
        }
        bool gone = false;
        for (int i = 0; i < 60 && !gone; i++)
        {
            gone = true;
            foreach (Process p in Process.GetProcessesByName("EconomyCheck"))
            {
                gone &= before.Contains(p.Id);
            }
            if (!gone)
            {
                Thread.Sleep(50);
            }
        }
        Check(gone, "texts, the start: the pretend server is ended with the failed start");
        try
        {
            File.Delete(model);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>
    /// This checker as a pretend llama-server, started by TextServer with llama.cpp's arguments: "silent" takes every connection
    /// and never answers; "loads" answers /health and never the first reply; the modes of TextsHangChecks.cs answer as
    /// llama.cpp does until a card asks them to hang, be slow or stop. Ends by itself after two minutes at the latest.
    /// </summary>
    private static int PretendLlama(string[] args)
    {
        string mode = File.Exists(args[1]) ? File.ReadAllText(args[1]).Trim() : "silent";
        int port = 0;
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "--port")
            {
                int.TryParse(args[i + 1], out port);
            }
        }
        if (PretendServes(mode))
        {
            return PretendServe(args[1], port);   // (a server that answers as llama.cpp does, then hangs, is slow or stops: TextsHangChecks.cs)
        }
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        var held = new List<TcpClient>();
        var until = Stopwatch.StartNew();
        while (until.Elapsed.TotalSeconds < 120)
        {
            if (!listener.Pending())
            {
                Thread.Sleep(10);
                continue;
            }
            TcpClient c = listener.AcceptTcpClient();
            held.Add(c);   // (kept open, never answered, unless it is /health on a server that loads)
            if (mode != "loads")
            {
                continue;
            }
            try
            {
                NetworkStream net = c.GetStream();
                var buffer = new byte[8192];
                c.ReceiveTimeout = 1000;
                int n = net.Read(buffer, 0, buffer.Length);
                string request = System.Text.Encoding.ASCII.GetString(buffer, 0, Math.Max(0, n));
                if (request.StartsWith("GET /health", StringComparison.Ordinal))
                {
                    byte[] ok = System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 15\r\nConnection: close\r\n\r\n{\"status\":\"ok\"}");
                    net.Write(ok, 0, ok.Length);
                    c.Close();
                    held.Remove(c);
                }
            }
            catch (IOException)
            {
                // (the runner gave up on it)
            }
        }
        return 0;
    }
}
