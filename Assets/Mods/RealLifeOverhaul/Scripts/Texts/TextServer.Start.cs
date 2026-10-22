using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>The runner's start: the server launched with its arguments, the model loaded, the first reply, the cached head and the speed test.</summary>
    public sealed partial class TextServer
    {
        private bool Launch()
        {
            if (!File.Exists(_start.Exe) || !File.Exists(_start.Model))
            {
                return Fail("the pack's files are missing (" + Path.GetFileName(File.Exists(_start.Exe) ? _start.Model : _start.Exe) + ")");
            }
            _port = FreePort();
            _key = Guid.NewGuid().ToString("N");
            if (_start.Cache != null)
            {
                Directory.CreateDirectory(_start.Cache);
                Stale(_start.Cache);
            }
            var info = new ProcessStartInfo(_start.Exe, Arguments(_start, _port, _key))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(_start.Exe),
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };
            Offline(info);
            string steam = OwnShaderCache(info);
            try
            {
                _process = Process.Start(info);
            }
            catch (Exception e)
            {
                return Fail("the server would not start: " + e.Message);
            }
            if (_process == null)
            {
                return Fail("the server would not start");
            }
            bool low = LowPriority(_start.Device);
            try
            {
                _process.PriorityClass = low ? ProcessPriorityClass.BelowNormal : ProcessPriorityClass.Normal;
            }
            catch (Exception)
            {
                // it may have ended already; the next look sees it
            }
            if (!low)
            {
                FullSpeed(_process);
            }
            TieToGame(_process);
            _log?.Invoke("RLO Texts: the server started on 127.0.0.1:" + _port + " (" + (_start.Device ?? "the processor") + ", " + _start.Threads + " threads, "
                         + (low ? "below normal" : "normal") + " priority, " + Path.GetFileName(_start.Model) + ")"
                         + (steam.Length > 0 ? "; the GPU driver's shader cache is its own (" + steam + " from Steam not passed on)." : "."));
            return true;
        }

        /// <summary>The first reply's limit: on a GPU long enough to build the shaders the driver has not kept.</summary>
        public static double WarmLimit(TextServerStart start) => start.Device != null ? Math.Max(start.WarmSeconds, start.BuildSeconds) : start.WarmSeconds;

        public const double BuiltCpuSeconds = 2.0;   // processor seconds of a GPU's first reply from which its shaders were built (about 0.05 s from the driver's cache, 10 s or more without)
        public const string ShaderMark = "gpu-shaders.txt";   // in the cache folder: the pack, device and runtime whose shaders were last built

        /// <summary>
        /// The driver's shader cache at the first reply, from the processor time the server used: building a GPU's shaders
        /// takes seconds of it, a reply from kept shaders hundredths. Built again after <paramref name="builtBefore"/>: the
        /// driver did not keep them.
        /// </summary>
        public static TextShaderCache ShaderCache(string device, double cpuSeconds, bool builtBefore)
        {
            if (device == null)
            {
                return TextShaderCache.None;
            }
            if (double.IsNaN(cpuSeconds) || double.IsInfinity(cpuSeconds))
            {
                return TextShaderCache.Unknown;
            }
            if (cpuSeconds < BuiltCpuSeconds)
            {
                return TextShaderCache.Used;
            }
            return builtBefore ? TextShaderCache.Missing : TextShaderCache.Created;
        }

        /// <summary>The cache's state as the start's line says it; empty on the processor.</summary>
        public static string ShaderCacheName(TextShaderCache state)
        {
            switch (state)
            {
                case TextShaderCache.Used: return "; the GPU's shaders: the driver's cache used";
                case TextShaderCache.Created: return "; the GPU's shaders: built, the driver's cache created for the next start";
                case TextShaderCache.Missing: return "; the GPU's shaders: built again, the driver's cache missing (cleared, or a new driver)";
                case TextShaderCache.Unknown: return "; the GPU's shaders: the driver's cache unknown";
                default: return string.Empty;
            }
        }

        /// <summary>What the shader mark holds for a start: the pack's model, the device and the runtime's release.</summary>
        public static string ShaderKey(TextServerStart start) => Path.GetFileName(start.Model) + ";" + start.Device + ";" + TextRuntime.Release;

        // Whether this pack, device and runtime built their shaders before, and the mark kept when they are built now.
        private TextShaderCache KeepShaderMark(double cpuSeconds)
        {
            string mark = _start.Cache != null && _start.Device != null ? Path.Combine(_start.Cache, ShaderMark) : null;
            bool before = false;
            try
            {
                before = mark != null && File.Exists(mark) && File.ReadAllText(mark).Trim() == ShaderKey(_start);
            }
            catch (Exception)
            {
                // unreadable: taken as never built
            }
            TextShaderCache state = ShaderCache(_start.Device, cpuSeconds, before);
            if (mark != null && (state == TextShaderCache.Created || state == TextShaderCache.Missing))
            {
                try
                {
                    File.WriteAllText(mark, ShaderKey(_start));
                }
                catch (Exception)
                {
                    // a read-only folder: the next build says created again
                }
            }
            return state;
        }

        /// <summary>
        /// True when the server runs below normal priority: on the processor, so the game's frame comes first. On a GPU its
        /// few threads only hand the work over and wait, and below normal they would wait behind every busy thread of the
        /// game (seen: a 40 s load and 3 to 6 words a second, against 3.5 s and over 100 at normal).
        /// </summary>
        public static bool LowPriority(string device) => device == null;

        /// <summary>llama-server's arguments as the runner starts it: one slot of 1024 tokens, offline, at its priority.</summary>
        public static string Arguments(TextServerStart start, int port, string key)
        {
            var args = new StringBuilder(512);
            args.Append("-m \"").Append(start.Model).Append("\" --host 127.0.0.1 --port ").Append(port)
                .Append(" --api-key ").Append(key)
                .Append(" -t ").Append(start.Threads).Append(" -tb ").Append(start.Threads)
                .Append(" -c 1024 -np 1 --no-webui --prio ").Append(LowPriority(start.Device) ? "-1" : "0")
                .Append(" --offline");   // llama.cpp never reaches the network, even to fetch a model
            if (start.Cache != null)
            {
                args.Append(" --slot-save-path \"").Append(start.Cache.TrimEnd('\\', '/')).Append('"');
            }
            if (start.Device != null)
            {
                args.Append(" -dev ").Append(start.Device).Append(" -ngl 99");
            }
            else
            {
                args.Append(" -dev none -ngl 0");
            }
            if (start.Log != null)
            {
                args.Append(" -lv 2 --log-file \"").Append(start.Log).Append('"');   // warnings and errors only, so a long session's log stays small
            }
            return args.ToString();
        }

        private bool WaitReady()
        {
            double begun = TextClock.Now;
            while (!_stop && !_watch.Over)
            {
                Process p = _process;
                if (p == null || p.HasExited)
                {
                    return Fail("the server ended while loading (exit code " + Exit() + ")");
                }
                if (TextClock.Now - begun > _start.LoadSeconds)
                {
                    return Fail("the model took longer than " + _start.LoadSeconds.ToString("0") + " s to load");
                }
                try
                {
                    string health = Get("/health", 1000);
                    if (health != null && health.IndexOf("\"ok\"", StringComparison.Ordinal) >= 0)
                    {
                        LoadedIn = TextClock.Now - begun;
                        return true;
                    }
                }
                catch (Exception)
                {
                    // not listening yet
                }
                _wake.WaitOne(200);
            }
            return false;
        }

        // One short reply before the first real one: it builds a GPU's shaders and fills the processor's caches.
        private bool WarmUp()
        {
            try
            {
                double begun = TextClock.Now, cpu = Cpu();
                string answer = Post("/completion", TextJson.Request(TextCard.ChatMl(TestCard(), _start.System ?? TextCard.System), 1, 1), Ms(WarmLimit(_start)));
                double took = TextClock.Now - begun, used = Cpu() - cpu;
                LoadedIn += took;
                _detail = "sent the test card, 1 token to write: " + Timings(answer, took, used) + ShaderCacheName(KeepShaderMark(used));
                return true;
            }
            catch (Exception e)
            {
                return Fail("the first reply failed: " + e.Message);
            }
        }

        // The head read and its state saved for every request after; without a cache folder each request reads it. The 2B is a
        // hybrid model whose state llama.cpp cannot reuse in part by itself.
        private bool KeepHead()
        {
            if (_start.Cache == null)
            {
                return true;
            }
            try
            {
                string head = Post("/completion", TextJson.Request(OwnHead, 1, 0), Ms(_start.WarmSeconds));
                _headFile = "rlo-head-" + _port + ".bin";
                string saved = Post("/slots/0?action=save", "{\"filename\":\"" + _headFile + "\"}", Ms(SaveSeconds));
                _headSaved = saved != null && saved.IndexOf("n_saved", StringComparison.Ordinal) >= 0;
                _headMs = TextJson.Number(head, "prompt_ms");
                _log?.Invoke("RLO Texts: the prompt's fixed head (" + TextJson.Number(head, "prompt_n") + " tokens, " + _headMs.ToString("0") + " ms to read) is "
                             + (_headSaved ? "kept and put back before each request." : "not kept (the server would not save it): each request reads it."));
            }
            catch (Exception e)
            {
                _headSaved = false;
                _log?.Invoke("RLO Texts: the prompt's fixed head is not kept (" + e.Message + "); each request reads it.");
            }
            return true;
        }

        private static int Ms(double seconds) => (int)(seconds * 1000.0);

        // A name's first tokens as this server's model writes them, asked of its own /tokenize once a name and kept; an answer
        // that does not come leaves the request without its ban, asked again next time. The worker's alone.
        private List<int> NameTokens(string name)
        {
            if (_names.TryGetValue(name, out List<int> known))
            {
                return known;
            }
            string[] forms = TextDecoding.NameForms(name);
            var tokens = new List<int>(2);
            bool answered = true;
            for (int i = 0; forms != null && i < forms.Length; i++)
            {
                try
                {
                    int token = TextJson.FirstToken(Post("/tokenize", TextJson.Tokenize(forms[i]), TokenizeMs));
                    if (token >= 0)
                    {
                        tokens.Add(token);
                    }
                }
                catch (Exception)
                {
                    answered = false;   // too slow this time: no ban on this request
                }
            }
            if (answered)
            {
                if (_names.Count >= NamesMost)
                {
                    _names.Clear();
                }
                _names[name] = tokens;
            }
            return tokens;
        }

        // Puts the head's state back, so the server reads only the card.
        private void Restore(string prompt)
        {
            if (!_headSaved || !prompt.StartsWith(OwnHead, StringComparison.Ordinal))
            {
                return;
            }
            try
            {
                Post("/slots/0?action=restore", "{\"filename\":\"" + _headFile + "\"}", Ms(RestoreSeconds));
            }
            catch (Exception)
            {
                _headSaved = false;   // the next requests read the head themselves
            }
        }

        // One reply from a fixed card: how fast this machine reads and writes, unless known from before for the same.
        private bool SpeedTest()
        {
            if (_start.Known != null)
            {
                Speed = _start.Known;
                return true;
            }
            var result = new TextSpeedResult { For = _start.SpeedFor, HeadMs = _headMs };
            double begun = TextClock.Now, cpu = Cpu(), restored = 0.0;
            string detail;
            try
            {
                string prompt = TextCard.ChatMl(TestCard(), _start.System ?? TextCard.System);
                Restore(prompt);
                restored = TextClock.Now - begun;
                double asked = TextClock.Now;
                cpu = Cpu();
                string answer = Post("/completion", TextJson.Request(prompt, 7, TextSpeed.ReplyTokens), (int)(TextSpeed.TestSeconds * 1000.0));
                result.PromptMsPerToken = TextJson.Number(answer, "prompt_per_token_ms");
                result.TokensPerSecond = TextJson.Number(answer, "predicted_per_second");
                if (double.IsNaN(result.PromptMsPerToken) || double.IsNaN(result.TokensPerSecond))
                {
                    throw new IOException("no timings");
                }
                detail = Timings(answer, TextClock.Now - asked, Cpu() - cpu);
            }
            catch (Exception e)
            {
                // Past the cap: at least as slow as the cap says.
                double spent = Math.Max(0.1, TextClock.Now - begun);
                result.Capped = true;
                result.PromptMsPerToken = spent * 1000.0 / TextSpeed.CardTokens;
                result.TokensPerSecond = TextSpeed.ReplyTokens / spent;
                detail = "no answer within " + TextSpeed.TestSeconds.ToString("0") + " s (" + e.Message + "); the server used " + Seconds(Cpu() - cpu) + " of processor time";
            }
            Speed = result;
            _log?.Invoke("RLO Texts: speed test: " + result.PromptMsPerToken.ToString("0.0", CultureInfo.InvariantCulture) + " ms a token read, "
                         + result.TokensPerSecond.ToString("0.0", CultureInfo.InvariantCulture)
                         + " tokens a second written: an ordinary reply in about " + result.Estimate.ToString("0.0", CultureInfo.InvariantCulture) + " s"
                         + (result.Capped ? " (past the test's cap)" : "")
                         + " (sent the test card, " + TextSpeed.ReplyTokens + " tokens to write, "
                         + (_headSaved ? "the head put back first in " + (restored * 1000.0).ToString("0") + " ms" : "the head read with it")
                         + ": " + detail + "; " + (LowPriority(_start.Device) ? "below normal" : "normal") + " priority, " + _start.Threads + " threads).");
            return true;
        }

        /// <summary>
        /// What a reply of the start took, for its log line: the model's own read and write times, the rest outside them and
        /// the processor time used, so a slow first reply tells work (shaders built) from waiting to run.
        /// </summary>
        public static string Timings(string answer, double seconds, double cpuSeconds)
        {
            double readN = TextJson.Number(answer, "prompt_n"), readMs = TextJson.Number(answer, "prompt_ms");
            double wroteN = TextJson.Number(answer, "predicted_n"), wroteMs = TextJson.Number(answer, "predicted_ms");
            if (double.IsNaN(readN) || double.IsNaN(readMs) || double.IsNaN(wroteN) || double.IsNaN(wroteMs))
            {
                return "no timings in the answer, " + Seconds(seconds) + " in all; the server used " + Seconds(cpuSeconds) + " of processor time";
            }
            double rest = Math.Max(0.0, seconds - (readMs + wroteMs) / 1000.0);
            return readN.ToString("0") + " tokens read in " + readMs.ToString("0") + " ms and " + wroteN.ToString("0") + " written in " + wroteMs.ToString("0")
                   + " ms by the model, " + Seconds(rest) + " outside them, " + Seconds(seconds) + " in all; the server used " + Seconds(cpuSeconds) + " of processor time";
        }

        // Seconds for a log line, or "unknown" where the system could not say.
        private static string Seconds(double seconds)
        {
            return double.IsNaN(seconds) || double.IsInfinity(seconds) ? "unknown" : seconds.ToString("0.00", CultureInfo.InvariantCulture) + " s";
        }

        // The processor time the server has used so far, in seconds; NaN where the system cannot say.
        private double Cpu()
        {
            try
            {
                Process p = _process;
                return p != null ? p.TotalProcessorTime.TotalSeconds : double.NaN;
            }
            catch (Exception)
            {
                return double.NaN;   // ended, or the runtime cannot read it
            }
        }

        /// <summary>The fixed card of the warm-up and the speed test.</summary>
        public static TextSheet TestCard()
        {
            var sheet = new TextSheet
            {
                Who = "Dana Whitlock, 31, florist, lives in Midtown", Role = "a friend, met at work", Mood = "in a good mood", Style = "warm", Player = "Alex",
                When = "Monday 18:05", News = "nothing has happened; Dana is just thinking of you", Write = "text: affection"
            };
            sheet.Traits.Add("generous");
            return sheet;
        }
    }
}
