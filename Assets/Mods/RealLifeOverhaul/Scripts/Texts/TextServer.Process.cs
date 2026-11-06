using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RealLifeOverhaul.Texts
{
    /// <summary>The runner's process and its socket: the requests over HTTP, the server killed, its environment, and the Windows job object and power settings.</summary>
    public sealed partial class TextServer
    {
        private string Get(string path, int timeoutMs) => Http("GET", path, null, timeoutMs);

        private string Post(string path, string body, int timeoutMs) => Http("POST", path, body, timeoutMs);

        // One request over a fresh local socket, the answer's body as text; throws on a time-out. Every wait is bounded by
        // Socket.Poll, never the socket's ReceiveTimeout, which Mono on Windows ignores. The socket is kept in _socket while in
        // use, so the watchdog or a stop can close it.
        private string Http(string method, string path, string body, int timeoutMs)
        {
            byte[] payload = body != null ? Encoding.UTF8.GetBytes(body) : null;
            var head = new StringBuilder(256);
            head.Append(method).Append(' ').Append(path).Append(" HTTP/1.1\r\nHost: 127.0.0.1:").Append(_port)
                .Append("\r\nAuthorization: Bearer ").Append(_key).Append("\r\nConnection: close\r\n");
            if (payload != null)
            {
                head.Append("Content-Type: application/json\r\nContent-Length: ").Append(payload.Length).Append("\r\n");
            }
            head.Append("\r\n");
            double until = TextClock.Now + timeoutMs / 1000.0;
            var client = new TcpClient(AddressFamily.InterNetwork);
            Interlocked.Exchange(ref _socket, client);
            try
            {
                client.NoDelay = true;
                client.SendTimeout = timeoutMs;
                client.ReceiveTimeout = timeoutMs;
                IAsyncResult connect = client.BeginConnect(IPAddress.Loopback, _port, null, null);
                if (!connect.AsyncWaitHandle.WaitOne(Math.Min(timeoutMs, 1000)))
                {
                    throw new IOException("no connection");
                }
                client.EndConnect(connect);
                NetworkStream net = client.GetStream();
                byte[] h = Encoding.ASCII.GetBytes(head.ToString());
                net.Write(h, 0, h.Length);
                if (payload != null)
                {
                    net.Write(payload, 0, payload.Length);
                }
                var all = new MemoryStream(1024);
                var buffer = new byte[4096];
                while (true)
                {
                    double left = until - TextClock.Now;
                    if (left <= 0.0 || !client.Client.Poll((int)(left * 1000000.0), SelectMode.SelectRead))
                    {
                        throw new IOException("too slow");
                    }
                    int read = net.Read(buffer, 0, buffer.Length);   // readable: data, or the end of the answer
                    if (read <= 0)
                    {
                        break;
                    }
                    all.Write(buffer, 0, read);
                }
                string text = Encoding.UTF8.GetString(all.GetBuffer(), 0, (int)all.Length);
                int split = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (!text.StartsWith("HTTP/1.1 200", StringComparison.Ordinal) && !text.StartsWith("HTTP/1.0 200", StringComparison.Ordinal))
                {
                    return null;
                }
                return split >= 0 ? text.Substring(split + 4) : null;
            }
            finally
            {
                Interlocked.CompareExchange(ref _socket, null, client);
                client.Close();
            }
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private string Exit()
        {
            try
            {
                return _process.ExitCode.ToString();
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        private bool Fail(string why)
        {
            lock (_failing)
            {
                if (_state == TextServerState.Failed)
                {
                    return false;   // one line: the watchdog and the worker may both end the same start
                }
                Why = why;
                _state = TextServerState.Failed;
            }
            _log?.Invoke("RLO Texts: " + why + "; the written lines are used.");
            return false;
        }

        private void Kill()
        {
            Process p = Interlocked.Exchange(ref _process, null);   // once: Dispose and the worker may both end it
            if (p != null)
            {
                try
                {
                    if (!p.HasExited)
                    {
                        p.Kill();
                        p.WaitForExit(1000);
                    }
                }
                catch (Exception)
                {
                    // gone already
                }
                p.Dispose();
            }
            IntPtr job = Interlocked.Exchange(ref _job, IntPtr.Zero);
            if (job != IntPtr.Zero)
            {
                CloseHandle(job);   // kill on close: anything still in the job ends now
            }
            string head = Interlocked.Exchange(ref _headFile, null);
            if (head != null && _start.Cache != null)
            {
                try
                {
                    File.Delete(Path.Combine(_start.Cache, head));   // this server's own head state, so the cache never grows
                }
                catch (Exception)
                {
                    // in use or gone: the next start's sweep takes it
                }
            }
        }

        /// <summary>Takes away at a start the head files a crashed earlier session left behind.</summary>
        private static void Stale(string cache)
        {
            try
            {
                foreach (string file in Directory.GetFiles(cache, "rlo-head-*.bin"))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception)
                    {
                        // another game's server has it open: left
                    }
                }
            }
            catch (Exception)
            {
                // no folder: nothing to sweep
            }
        }

        /// <summary>Keeps a child of llama.cpp's offline and to the mod's arguments: no LLAMA_ARG_ or HF_ variable reaches it.</summary>
        public static void Offline(ProcessStartInfo info)
        {
            var drop = new List<string>(4);
            foreach (System.Collections.DictionaryEntry e in info.EnvironmentVariables)
            {
                string name = e.Key as string;
                if (name != null && (name.StartsWith("LLAMA_ARG_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("HF_", StringComparison.OrdinalIgnoreCase)))
                {
                    drop.Add(name);
                }
            }
            for (int i = 0; i < drop.Count; i++)
            {
                info.EnvironmentVariables.Remove(drop[i]);
            }
        }

        /// <summary>True for a variable that points a GPU driver's shader cache elsewhere, as Steam sets them for its shader pre-caching.</summary>
        public static bool ShaderCacheVariable(string name)
        {
            return name != null && (name.StartsWith("__GL_SHADER_DISK_CACHE", StringComparison.OrdinalIgnoreCase) || name.StartsWith("MESA_SHADER_CACHE", StringComparison.OrdinalIgnoreCase)
                                    || name.StartsWith("MESA_GLSL_CACHE", StringComparison.OrdinalIgnoreCase) || name.StartsWith("MESA_DISK_CACHE", StringComparison.OrdinalIgnoreCase)
                                    || name.StartsWith("DXVK_STATE_CACHE", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gives a child of llama.cpp's the driver's own shader cache: under Steam's none was kept and every start built them
        /// again (10 s against 0.05 s). The names taken out, for the log.
        /// </summary>
        public static string OwnShaderCache(ProcessStartInfo info)
        {
            var drop = new List<string>(4);
            foreach (System.Collections.DictionaryEntry e in info.EnvironmentVariables)
            {
                string name = e.Key as string;
                if (ShaderCacheVariable(name))
                {
                    drop.Add(name);
                }
            }
            drop.Sort(StringComparer.OrdinalIgnoreCase);   // the log's order the same each start
            for (int i = 0; i < drop.Count; i++)
            {
                info.EnvironmentVariables.Remove(drop[i]);
            }
            return string.Join(", ", drop.ToArray());
        }

        // A job object, so the server ends with the game, even in a crash.
        private const int JobObjectExtendedLimitInformation = 9;
        private const uint KillOnJobClose = 0x2000;

        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimits
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimits
        {
            public BasicLimits Basic;
            public IoCounters Io;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr attributes, string name);

        [DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref ExtendedLimits info, int length);

        [DllImport("kernel32.dll")]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        private void TieToGame(Process p)
        {
            try
            {
                _job = CreateJobObject(IntPtr.Zero, null);
                if (_job == IntPtr.Zero)
                {
                    return;
                }
                var limits = new ExtendedLimits();
                limits.Basic.LimitFlags = KillOnJobClose;
                if (!SetInformationJobObject(_job, JobObjectExtendedLimitInformation, ref limits, Marshal.SizeOf(typeof(ExtendedLimits)))
                    || !AssignProcessToJobObject(_job, p.Handle))
                {
                    _log?.Invoke("RLO Texts: the server could not be tied to the game's process; it is still ended when the game closes normally.");
                }
            }
            catch (Exception e)
            {
                _log?.Invoke("RLO Texts: no job object (" + e.GetType().Name + "); the server is ended when the game closes normally.");
            }
        }

        // On a GPU: no power throttling (Windows' EcoQoS).
        private const int ProcessPowerThrottling = 4;
        private const uint ThrottleExecutionSpeed = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct PowerThrottling
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessInformation(IntPtr process, int infoClass, ref PowerThrottling info, int length);

        // Windows may run a windowless background process slowly to save power; llama.cpp keeps its own worker threads out
        // of that, but not the thread that drives the GPU, so the whole process is kept out. Where the call is missing,
        // nothing changes.
        private void FullSpeed(Process p)
        {
            try
            {
                var state = new PowerThrottling { Version = 1, ControlMask = ThrottleExecutionSpeed, StateMask = 0 };
                if (!SetProcessInformation(p.Handle, ProcessPowerThrottling, ref state, Marshal.SizeOf(typeof(PowerThrottling))))
                {
                    _log?.Invoke("RLO Texts: Windows would not keep the server out of power throttling; it runs as Windows decides.");
                }
            }
            catch (Exception e)
            {
                _log?.Invoke("RLO Texts: no power throttling setting on this Windows (" + e.GetType().Name + "); the server runs as Windows decides.");
            }
        }
    }
}
