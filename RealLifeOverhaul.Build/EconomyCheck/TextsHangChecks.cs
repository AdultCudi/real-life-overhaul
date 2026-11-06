using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using RealLifeOverhaul.Texts;

// RLO Texts' server that stops answering is started again (the fault: a server whose loop was stuck, or that had stopped
// by itself, was never started again, so every later request got the written line and no model words came for the rest
// of the session; the v6 pilot's one hang was its test harness, which never read the server's log pipe). Now a reply that
// does not come is followed by a probe of the server's slot (TextHang): a slot that comes free is a slow moment and nothing
// changes; a slot still at work, or no answer, is a hang, and the worker starts the server again in the background, as it
// does a server that stopped by itself, at most three times a session. The rules game-free, then the real runner (TextServer)
// against this checker as a pretend llama-server that answers as llama.cpp does until a card asks it to hang, be slow or stop.
internal static partial class Program
{
    private const double HangProbe = 1.0;       // seconds of the probe in the pretend runs (TextServerStart.ProbeSeconds)
    private const double HangAsked = 0.8;       // seconds a pretend request may take (its deadline)
    private const int HangSlowMs = 2500;        // the slow pretend server's reply: well past the request's time

    private static void TextsHang()
    {
        TextsHangProbeTime();
        TextsHangSlot();
        TextsHangRestarts();
        TextsHangSources();
        TextsHangPretend("hang-once", "a server that hangs on one request, its slot at work for ever, is started again and the next request is answered");
        TextsHangPretend("slow", "a slow reply on a well server (its slot free once the request is given up) restarts nothing");
        TextsHangPretend("hang-always", "a server that hangs after every start, answering nothing at all, is started again three times, then the written lines stay");
        TextsHangPretend("exits", "a server that stops by itself is started again and the next request is answered");
    }

    private static TextSpeedResult HangSpeed(double promptMs, double tokensPerSecond, bool capped = false)
    {
        return new TextSpeedResult { For = "check", PromptMsPerToken = promptMs, TokensPerSecond = tokensPerSecond, Capped = capped };
    }

    private static void TextsHangProbeTime()
    {
        Check(TextHang.ProbeSeconds(null) == TextHang.ProbeMost, "texts, a hang: with no speed measured the probe waits its most (" + TextHang.ProbeMost + " s)");
        Check(TextHang.ProbeSeconds(HangSpeed(5, 50, true)) == TextHang.ProbeMost, "texts, a hang: a machine past the speed test's cap is given the probe's most");
        Check(TextHang.ProbeSeconds(HangSpeed(5, 0)) == TextHang.ProbeMost, "texts, a hang: a speed with no words a second (no estimate) gives the probe's most");
        double fast = HangSpeed(2, 100).Estimate;
        Check(fast < 1.0 && TextHang.ProbeSeconds(HangSpeed(2, 100)) == TextHang.ProbeLeast,
            "texts, a hang: a fast machine (a reply in " + fast.ToString("0.00") + " s) still gives the slot " + TextHang.ProbeLeast + " s (llama.cpp's second of polling, then its batch)");
        TextSpeedResult mid = HangSpeed(20, 10);
        Check(Math.Abs(TextHang.ProbeSeconds(mid) - mid.Estimate * TextHang.ProbeReplies) < 1e-9,
            "texts, a hang: a middling machine's probe is " + TextHang.ProbeReplies + " ordinary replies (" + mid.Estimate.ToString("0.0") + " s each, " + TextHang.ProbeSeconds(mid).ToString("0.0") + " s)");
        Check(TextHang.ProbeSeconds(HangSpeed(200, 2)) == TextHang.ProbeMost, "texts, a hang: a very slow machine's probe stops at the most");
        bool within = true, enough = true;
        for (double ms = 0.5; ms <= 300; ms *= 1.3)
        {
            for (double tps = 0.5; tps <= 400; tps *= 1.4)
            {
                TextSpeedResult s = HangSpeed(ms, tps);
                double p = TextHang.ProbeSeconds(s);
                within &= p >= TextHang.ProbeLeast && p <= TextHang.ProbeMost;
                enough &= p >= Math.Min(TextHang.ProbeMost, s.Estimate * TextHang.ProbeReplies) - 1e-9;   // (a slow machine's whole reply, three times over, fits in its probe)
            }
        }
        Check(within && enough, "texts, a hang: over every speed the probe stays within " + TextHang.ProbeLeast + " to " + TextHang.ProbeMost + " s and covers three of the machine's replies up to its most");
        Check(TextHang.ProbeSeconds(mid) == TextHang.ProbeSeconds(HangSpeed(20, 10)), "texts, a hang: the probe's time is the same for the same speed");
    }

    private static void TextsHangSlot()
    {
        const string busy = "[{\"id\":0,\"n_ctx\":1024,\"speculative\":false,\"is_processing\":true,\"id_task\":12}]";
        const string free = "[{\"id\":0,\"n_ctx\":1024,\"speculative\":false,\"is_processing\":false,\"params\":{\"n_predict\":70}}]";
        Check(TextHang.Read(busy) == TextSlotLook.Busy, "texts, a hang: llama.cpp's /slots with the slot at work reads as busy");
        Check(TextHang.Read(free) == TextSlotLook.Idle, "texts, a hang: llama.cpp's /slots with the slot free reads as idle");
        Check(TextHang.Read(" [ { \"id\" : 0, \"is_processing\" :  false } ]") == TextSlotLook.Idle && TextHang.Read("[{\"is_processing\":true}]") == TextSlotLook.Busy,
            "texts, a hang: spaces in the answer change nothing");
        Check(TextHang.Read(null) == TextSlotLook.Silent, "texts, a hang: no answer is silent");
        Check(TextHang.Read("") == TextSlotLook.Unreadable && TextHang.Read("{\"error\":{\"code\":501}}") == TextSlotLook.Unreadable
              && TextHang.Read("[{\"id\":0}]") == TextSlotLook.Unreadable && TextHang.Read("[{\"is_processing\":maybe}]") == TextSlotLook.Unreadable,
            "texts, a hang: an error, a slot without its state or a state that is not true or false gives no verdict");
        Check(TextHang.Hung(TextSlotLook.Busy) && TextHang.Hung(TextSlotLook.Silent) && !TextHang.Hung(TextSlotLook.Idle) && !TextHang.Hung(TextSlotLook.Unreadable),
            "texts, a hang: the server is hung when its slot is still at work at the probe's end, or it gave no answer; never when its slot is free or the answer gives no verdict");
        Check(TextHang.Settled(TextSlotLook.Idle) && TextHang.Settled(TextSlotLook.Unreadable) && !TextHang.Settled(TextSlotLook.Busy) && !TextHang.Settled(TextSlotLook.Silent),
            "texts, a hang: the probe ends early only on a free slot or an answer with no verdict; a busy slot is looked at again until the probe's end");
        Check(TextHang.Name(TextSlotLook.Busy) != TextHang.Name(TextSlotLook.Silent) && TextHang.Name(TextSlotLook.Idle).Length > 0, "texts, a hang: each look has its name for the log");
    }

    private static void TextsHangRestarts()
    {
        Check(TextHang.RestartsMost == 3, "texts, a hang: the server is started again at most three times a session");
        Check(TextHang.MayRestart(0) && TextHang.MayRestart(1) && TextHang.MayRestart(TextHang.RestartsMost - 1), "texts, a hang: below the most a hung server is started again");
        Check(!TextHang.MayRestart(TextHang.RestartsMost) && !TextHang.MayRestart(TextHang.RestartsMost + 5) && !TextHang.MayRestart(-1),
            "texts, a hang: at the most (or a count that makes no sense) it is not started again");
    }

    private static void TextsHangSources()
    {
        string server = TextsSource(@"Texts\TextServer.cs") + TextsSource(@"Texts\TextServer.Start.cs") + TextsSource(@"Texts\TextServer.Process.cs"), director = TextsSource(@"Texts\TextsDirector.cs");
        Check(server.Contains("if (Write(job) && Hung() && !Recover(\"did not answer\"))") && server.Contains("!Recover(\"stopped by itself (exit code \" + Exit() + \")\")"),
            "texts, a hang: the worker probes after a reply that did not come and starts a hung server again, and a server that stopped by itself too");
        Check(server.Contains("Get(\"/slots\"") && server.Contains("TextHang.ProbeSeconds(Speed)") && server.Contains("TextHang.MayRestart(_restarts)"),
            "texts, a hang: the probe asks llama.cpp for its slot within the time the speed gives, and the restarts are counted against the most");
        Check(server.Contains("&& (!first || Stage(TextStartStage.Speed"), "texts, a hang: a restart runs the start's stages, its watchdog with them, but not the speed test");
        Check(!director.Contains("Recover(") && !director.Contains("Restarts"), "texts, a hang: nothing of the restart is on the game's thread (the director never starts the server again for it)");
    }

    // The real runner on this checker as a pretend llama-server in one of the modes below.
    private static void TextsHangPretend(string mode, string what)
    {
        string exe = System.IO.Path.Combine(AppContext.BaseDirectory, "EconomyCheck.exe");
        if (!File.Exists(exe) || Blocked(exe))
        {
            Console.WriteLine("  SKIPPED texts, a hang on a pretend server (" + mode + "): the checker cannot be started as a program on this PC; the rest of the checks run");
            return;
        }
        string model = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo-pretend-" + mode + "-" + Process.GetCurrentProcess().Id + ".gguf");
        string cache = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo-pretend-cache-" + mode + "-" + Process.GetCurrentProcess().Id);
        File.WriteAllText(model, mode);
        Directory.CreateDirectory(cache);
        var before = PretendServers();
        var log = new List<string>();
        var q = new TextQueue();
        q.Expected(0.05);   // (the pretend server answers at once: a request's short deadline is not refused for the start's guess)
        using (var server = new TextServer(q, new TextServerStart { Exe = exe, Model = model, Threads = 2, LoadSeconds = 5, WarmSeconds = 5, Cache = cache, SpeedFor = "check", ProbeSeconds = HangProbe },
                   l => { lock (log) { log.Add(l); } }))
        {
            server.Start();
            bool ready = HangWait(() => server.State != TextServerState.Loading, 15) && server.State == TextServerState.Ready;
            Check(ready, "texts, a hang (" + mode + "): the pretend server starts (" + server.State + "; " + HangLines(log) + ")");
            if (ready)
            {
                switch (mode)
                {
                    case "hang-once":
                        {
                            TextJob hung = HangAsk(server, q, "HANG");
                            bool back = HangWait(() => server.Restarts == 1 && server.State == TextServerState.Ready, 10);
                            TextJob next = HangAsk(server, q, "Dana");
                            string lines = HangLines(log);
                            Check(hung.State == TextJobState.Missed && back && next.State == TextJobState.Written && next.Raw == "Hello there.",
                                "texts, a hang: " + what + " (the hung one " + hung.State + ", restarts " + server.Restarts + ", " + server.State + ", the next " + next.State + "; " + lines + ")");
                            Check(lines.Contains("RLO Texts: the server did not answer; it is started again in the background (1 of 3 this session); the written lines are used meanwhile.")
                                  && lines.Contains("Texts check (server answers): a reply did not come in its time; then its slot still at work after ") && lines.Contains("FAIL (the server is hung: started again)")
                                  && lines.Contains("Texts check (server restart): started again in ") && lines.Contains("the head kept, 1 of 3 restarts used: PASS"),
                                "texts, a hang: the hang, the restart and the head kept again are each logged, the self-checks in the mod's style (" + lines + ")");
                            break;
                        }
                    case "slow":
                        {
                            TextJob late = HangAsk(server, q, "HANG");
                            Thread.Sleep(1500);
                            TextJob next = HangAsk(server, q, "Dana");
                            string lines = HangLines(log);
                            Check(late.State == TextJobState.Missed && server.Restarts == 0 && server.State == TextServerState.Ready && next.State == TextJobState.Written
                                  && lines.Contains("then its slot free after ") && lines.Contains("PASS (a slow moment, not a hang: nothing restarted)") && !lines.Contains("started again in the background"),
                                "texts, a hang: " + what + " (the late one " + late.State + ", restarts " + server.Restarts + ", the next " + next.State + "; " + lines + ")");
                            break;
                        }
                    case "hang-always":
                        {
                            for (int i = 0; i <= TextHang.RestartsMost && server.State == TextServerState.Ready; i++)
                            {
                                HangAsk(server, q, "HANG");
                                HangWait(() => server.State != TextServerState.Loading && (server.Restarts == i + 1 || server.State == TextServerState.Failed), 10);
                            }
                            string lines = HangLines(log);
                            Check(server.State == TextServerState.Failed && server.Restarts == TextHang.RestartsMost && server.Why == "the server stopped answering 4 times this session",
                                "texts, a hang: " + what + " (" + server.State + ", restarts " + server.Restarts + ", " + server.Why + ")");
                            Check(lines.Contains("then no answer after ") && lines.Contains("(3 of 3 this session)")
                                  && lines.Contains("Texts check (server restarts): the server did not answer after 3 restart(s) this session, the most is 3; the written lines stay until the pack is switched off and on: FAIL")
                                  && lines.Contains("RLO Texts: the server stopped answering 4 times this session; the written lines are used."),
                                "texts, a hang: a server that answers nothing is found by the probe, and the end of the restarts is said in the log (" + lines + ")");
                            TextJob after = HangAsk(server, q, "Dana");
                            Check(after.State == TextJobState.Missed || after.State == TextJobState.Dropped || after.State == TextJobState.Waiting,
                                "texts, a hang: past the restarts no request reaches the model (" + after.State + "): the written lines");
                            break;
                        }
                    case "exits":
                        {
                            TextJob gone = HangAsk(server, q, "HANG");
                            bool back = HangWait(() => server.Restarts == 1 && server.State == TextServerState.Ready, 10);
                            TextJob next = HangAsk(server, q, "Dana");
                            string lines = HangLines(log);
                            Check(gone.State == TextJobState.Missed && back && next.State == TextJobState.Written
                                  && lines.Contains("RLO Texts: the server stopped by itself (exit code 3); it is started again in the background (1 of 3 this session)"),
                                "texts, a hang: " + what + " (" + gone.State + ", restarts " + server.Restarts + ", the next " + next.State + "; " + lines + ")");
                            break;
                        }
                }
            }
        }
        bool ended = HangWait(() =>
        {
            foreach (int id in PretendServers())
            {
                if (!before.Contains(id))
                {
                    return false;
                }
            }
            return true;
        }, 5);
        Check(ended, "texts, a hang (" + mode + "): every pretend server started is ended, the hung ones and the last");
        try
        {
            File.Delete(model);
            Directory.Delete(cache, true);
        }
        catch (IOException)
        {
        }
    }

    // A request through the queue, as the hub asks it; returns once it is written or missed (or the wait's cap).
    private static TextJob HangAsk(TextServer server, TextQueue q, string who)
    {
        var job = new TextJob { Key = "hang:" + who + ":" + Guid.NewGuid().ToString("N"), Prompt = TextServer.Head + "who: " + who + "<|im_end|>\n<|im_start|>assistant\n", Priority = TextPriority.Now,
                                Deadline = TextClock.Now + HangAsked };
        if (!q.Add(job, TextClock.Now))
        {
            return job;
        }
        server.Nudge();
        HangWait(() => job.State != TextJobState.Waiting && job.State != TextJobState.Running, 6);
        return job;
    }

    private static bool HangWait(Func<bool> done, double seconds)
    {
        var sw = Stopwatch.StartNew();
        while (!done())
        {
            if (sw.Elapsed.TotalSeconds > seconds)
            {
                return false;
            }
            Thread.Sleep(20);
        }
        return true;
    }

    private static string HangLines(List<string> log)
    {
        lock (log)
        {
            return string.Join(" | ", log);
        }
    }

    // ---------------------------------------------------------------- the pretend server's modes

    private const string HangReply = "{\"content\":\"Hello there.\",\"tokens_predicted\":3,\"timings\":{\"prompt_n\":10,\"prompt_ms\":5.0,\"prompt_per_token_ms\":0.5,"
                                     + "\"predicted_n\":3,\"predicted_ms\":5.0,\"predicted_per_second\":600.0}}";

    private static bool PretendServes(string mode) => mode == "hang-once" || mode == "hang-always" || mode == "slow" || mode == "exits" || mode == "well";

    /// <summary>
    /// A pretend llama-server that answers as llama.cpp does (/health, /completion with timings, the slot's save and restore,
    /// GET /slots) until a request's card holds "HANG": "hang-once" then never answers that request or any other (its slot
    /// at work for ever: GET /slots says so) and leaves the next start well; "hang-always" does so after every start and
    /// answers no /slots either; "slow" answers it well past its time, its slot free; "exits" ends the process (exit code
    /// 3) and leaves the next start well. /health is answered even hung, as llama.cpp's own HTTP thread does.
    /// </summary>
    private static int PretendServe(string model, int port)
    {
        string mode = File.ReadAllText(model).Trim();
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        var held = new List<TcpClient>();
        bool hung = false;
        var until = Stopwatch.StartNew();
        while (until.Elapsed.TotalSeconds < 120)
        {
            if (!listener.Pending())
            {
                Thread.Sleep(5);
                continue;
            }
            TcpClient c = listener.AcceptTcpClient();
            if (!PretendRead(c, out string path, out string body))
            {
                c.Close();
                continue;
            }
            if (path == "/health")
            {
                PretendAnswer(c, "{\"status\":\"ok\"}");
                continue;
            }
            if (hung)
            {
                if (path == "/slots" && mode == "hang-once")
                {
                    PretendAnswer(c, "[{\"id\":0,\"n_ctx\":1024,\"is_processing\":true}]");
                    continue;
                }
                held.Add(c);   // (everything else waits on the stuck loop)
                continue;
            }
            if (path == "/completion" && body.Contains("HANG"))
            {
                switch (mode)
                {
                    case "hang-once":
                        File.WriteAllText(model, "well");
                        hung = true;
                        held.Add(c);
                        continue;
                    case "hang-always":
                        hung = true;
                        held.Add(c);
                        continue;
                    case "exits":
                        File.WriteAllText(model, "well");
                        c.Close();
                        return 3;
                    case "slow":
                        TcpClient late = c;
                        new Thread(() =>
                        {
                            Thread.Sleep(HangSlowMs);
                            PretendAnswer(late, HangReply);   // (to a connection given up: it fails quietly)
                        }) { IsBackground = true }.Start();
                        continue;
                }
            }
            if (path == "/slots")
            {
                PretendAnswer(c, "[{\"id\":0,\"n_ctx\":1024,\"is_processing\":false}]");
            }
            else if (path.StartsWith("/slots/0?action=save", StringComparison.Ordinal))
            {
                PretendAnswer(c, "{\"id_slot\":0,\"filename\":\"x\",\"n_saved\":57,\"n_written\":1000}");
            }
            else if (path.StartsWith("/slots/0?action=restore", StringComparison.Ordinal))
            {
                PretendAnswer(c, "{\"id_slot\":0,\"filename\":\"x\",\"n_restored\":57,\"n_read\":1000}");
            }
            else
            {
                PretendAnswer(c, HangReply);
            }
        }
        return 0;
    }

    // One HTTP request: its path and its body (the runner sends the head and the body in two writes).
    private static bool PretendRead(TcpClient c, out string path, out string body)
    {
        path = body = null;
        try
        {
            c.ReceiveTimeout = 2000;
            NetworkStream net = c.GetStream();
            var all = new MemoryStream(1024);
            var buffer = new byte[8192];
            while (true)
            {
                int n = net.Read(buffer, 0, buffer.Length);
                if (n <= 0)
                {
                    return false;
                }
                all.Write(buffer, 0, n);
                string text = Encoding.Latin1.GetString(all.GetBuffer(), 0, (int)all.Length);
                int end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (end < 0)
                {
                    continue;
                }
                int length = 0;
                foreach (string line in text.Substring(0, end).Split("\r\n"))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(line.Substring(15).Trim(), out length);
                    }
                }
                if (all.Length - (end + 4) < length)
                {
                    continue;
                }
                string first = text.Substring(0, text.IndexOf("\r\n", StringComparison.Ordinal));
                string[] parts = first.Split(' ');
                path = parts.Length > 1 ? parts[1] : "";
                body = Encoding.UTF8.GetString(all.GetBuffer(), end + 4, length);
                return true;
            }
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void PretendAnswer(TcpClient c, string json)
    {
        try
        {
            byte[] payload = Encoding.UTF8.GetBytes(json);
            byte[] head = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
            NetworkStream net = c.GetStream();
            net.Write(head, 0, head.Length);
            net.Write(payload, 0, payload.Length);
        }
        catch (Exception)
        {
            // (the runner gave up on it)
        }
        finally
        {
            c.Close();
        }
    }
}
