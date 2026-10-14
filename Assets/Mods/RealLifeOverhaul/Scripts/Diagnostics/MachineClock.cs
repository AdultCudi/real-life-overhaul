using System;
using System.Runtime.InteropServices;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the operating system says of the frame before this one, for the long-frame line of the session record: how
    /// long the game's main thread ran on a processor in it (GetThreadTimes), how long the whole game ran over all its
    /// threads (GetProcessTimes), how busy the machine's processors were and so how much of them other programs held
    /// (GetSystemTimes), and how many page faults the game took (K32GetProcessMemoryInfo). That is what tells a frame the
    /// game spent working from one in which it waited, and one in which another program held the processor.
    ///
    /// Read once a frame by <see cref="FrameSteps"/> at the frame's roll-over, on the main thread, so it covers exactly the
    /// frame the steps cover; only while the steps are timed ("Developer diagnostics" on, and the option "Measurements
    /// time every step of the frame"). Four calls into kernel32 with fixed arguments and out values, a microsecond or a
    /// few, nothing allocated. Windows counts a thread's and the system's times at its clock tick (about 15.6 ms), so
    /// the times are good for a long frame and for sums, not for a single short one. An entry point that is missing (an
    /// older Windows, Wine, another system) is found out once, when the timing starts: that measure then says it is not
    /// available, and nothing throws.
    /// </summary>
    public static class MachineClock
    {
        private const string Kernel = "kernel32.dll";
        private static readonly IntPtr ThisThread = new IntPtr(-2);    // GetCurrentThread's pseudo-handle: the calling thread, never closed
        private static readonly IntPtr ThisProcess = new IntPtr(-1);   // GetCurrentProcess's pseudo-handle

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryCounters   // PROCESS_MEMORY_COUNTERS
        {
            public uint Size;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
        }

        [DllImport(Kernel, EntryPoint = "GetThreadTimes")]
        private static extern int ThreadTimes(IntPtr thread, out long creation, out long exit, out long kernel, out long user);

        [DllImport(Kernel, EntryPoint = "GetProcessTimes")]
        private static extern int ProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);

        [DllImport(Kernel, EntryPoint = "GetSystemTimes")]
        private static extern int SystemTimes(out long idle, out long kernel, out long user);

        [DllImport(Kernel, EntryPoint = "K32GetProcessMemoryInfo")]
        private static extern int MemoryInfo(IntPtr process, ref MemoryCounters counters, uint size);

        private static readonly uint CountersSize = (uint)Marshal.SizeOf(typeof(MemoryCounters));

        // Each measure: whether it can be read, why not, the reading at the last roll-over and the frame's difference.
        private static bool _thread, _process, _system, _memory, _tried;
        private static string _threadWhy = "not measured", _processWhy = "not measured", _systemWhy = "not measured", _memoryWhy = "not measured";
        private static long _threadAt, _processAt, _idleAt, _totalAt;
        private static uint _faultsAt;
        private static long _threadLast, _processLast, _idleLast, _totalLast, _faultsLast;
        private static int _samples;

        /// <summary>True from the second reading on: the differences then speak of a whole frame.</summary>
        public static bool Valid => _samples >= 2;

        /// <summary>Tries each entry point once (when the timing starts); the frame's first roll-over takes the first reading.</summary>
        public static void Start()
        {
            if (!_tried)
            {
                _tried = true;
                _thread = Try(TryThread, "GetThreadTimes", ref _threadWhy);
                _process = Try(TryProcess, "GetProcessTimes", ref _processWhy);
                _system = Try(TrySystem, "GetSystemTimes", ref _systemWhy);
                _memory = Try(TryMemory, "K32GetProcessMemoryInfo", ref _memoryWhy);
            }
            _samples = 0;
        }

        /// <summary>One reading, at the frame's roll-over: the frame before this one is the difference from the last.</summary>
        public static void Sample()
        {
            try
            {
                if (_thread && ThreadTimes(ThisThread, out _, out _, out long k, out long u) != 0)
                {
                    _threadLast = k + u - _threadAt;
                    _threadAt = k + u;
                }
                if (_process && ProcessTimes(ThisProcess, out _, out _, out long pk, out long pu) != 0)
                {
                    _processLast = pk + pu - _processAt;
                    _processAt = pk + pu;
                }
                if (_system && SystemTimes(out long idle, out long sk, out long su) != 0)
                {
                    _idleLast = idle - _idleAt;
                    _totalLast = sk + su - _totalAt;   // the system's kernel time includes its idle time
                    _idleAt = idle;
                    _totalAt = sk + su;
                }
                if (_memory)
                {
                    var counters = new MemoryCounters { Size = CountersSize };
                    if (MemoryInfo(ThisProcess, ref counters, CountersSize) != 0)
                    {
                        _faultsLast = unchecked(counters.PageFaultCount - _faultsAt);   // the count wraps at four billion
                        _faultsAt = counters.PageFaultCount;
                    }
                }
                if (_samples < 2)
                {
                    _samples++;
                }
            }
            catch (Exception)
            {
                _thread = _process = _system = _memory = false;   // a record must never be the fault
                _threadWhy = _processWhy = _systemWhy = _memoryWhy = "not available (the system refused it)";
            }
        }

        /// <summary>Milliseconds the main thread ran on a processor in the frame before this one, or below zero when not known.</summary>
        public static double ThreadMs => _thread && Valid ? _threadLast / 10000.0 : -1.0;

        /// <summary>Milliseconds the game ran over all its threads in the frame before this one, or below zero.</summary>
        public static double ProcessMs => _process && Valid ? _processLast / 10000.0 : -1.0;

        /// <summary>How busy the machine's processors were in the frame before this one, 0 to 1, or below zero.</summary>
        public static double Busy => _system && Valid && _totalLast > 0L ? Clamp01(1.0 - (double)_idleLast / _totalLast) : -1.0;

        /// <summary>Of the machine's processors, the share other programs held in the frame before this one (the busy share less the game's), or below zero.</summary>
        public static double Others => _system && _process && Valid && _totalLast > 0L ? Clamp01((double)(_totalLast - _idleLast - _processLast) / _totalLast) : -1.0;

        /// <summary>The game's page faults in the frame before this one, or below zero.</summary>
        public static long Faults => _memory && Valid ? _faultsLast : -1L;

        /// <summary>For a long frame's line, in place of a figure that is not known: "not available (...)", or "not measured".</summary>
        public static string ThreadWhy => _thread ? "not measured yet" : _threadWhy;

        public static string SystemWhy => _system ? "not measured yet" : _systemWhy;

        public static string MemoryWhy => _memory ? "not measured yet" : _memoryWhy;

        /// <summary>For the recording line: which of the four can be read.</summary>
        public static string Status()
        {
            if (!_tried)
            {
                return "not tried (the frame's steps are not timed)";
            }
            if (_thread && _process && _system && _memory)
            {
                return "all read";
            }
            return "the main thread's time " + (_thread ? "read" : _threadWhy) + ", the game's " + (_process ? "read" : _processWhy)
                   + ", the machine's " + (_system ? "read" : _systemWhy) + ", the page faults " + (_memory ? "read" : _memoryWhy);
        }

        private static double Clamp01(double v) => v < 0.0 ? 0.0 : v > 1.0 ? 1.0 : v;

        // Each entry point in a method of its own, so that one that is missing fails alone when it is first called.
        private static bool Try(Func<bool> call, string entry, ref string why)
        {
            try
            {
                if (call())
                {
                    return true;
                }
                why = "not available (" + entry + " gave no answer)";
            }
            catch (EntryPointNotFoundException)
            {
                why = "not available (this system has no " + entry + ")";
            }
            catch (DllNotFoundException)
            {
                why = "not available (this system has no kernel32)";
            }
            catch (Exception)
            {
                why = "not available (" + entry + " could not be called)";
            }
            return false;
        }

        private static bool TryThread() => ThreadTimes(ThisThread, out _, out _, out _, out _) != 0;

        private static bool TryProcess() => ProcessTimes(ThisProcess, out _, out _, out _, out _) != 0;

        private static bool TrySystem() => SystemTimes(out _, out _, out _) != 0;

        private static bool TryMemory()
        {
            var counters = new MemoryCounters { Size = CountersSize };
            return MemoryInfo(ThisProcess, ref counters, CountersSize) != 0;
        }
    }
}
