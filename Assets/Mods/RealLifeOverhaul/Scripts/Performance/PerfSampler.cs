using System;
using Unity.Profiling;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What one measuring window found. Times are milliseconds; a negative time was not reported by the platform.</summary>
    public struct PerfWindow
    {
        public int Frames;
        public float Seconds;
        public float AverageMs;
        public float MedianMs;
        public float Low1Ms;        // mean of the slowest 1 % of frames
        public float Low01Ms;       // mean of the slowest 0.1 % of frames (at least one frame)
        public float WorstMs;
        public int FixedSteps;
        public int Collections;     // garbage collections that ran inside the window
        public int TimedFrames;     // frames the engine's frame timing answered for
        public float MainMs;        // main thread, without the wait for the display
        public float RenderMs;      // render thread
        public float GpuMs;
        public float PresentWaitMs; // main thread waiting for the display (a cap or v-sync shows up here)

        public float Fps => AverageMs > 0f ? 1000f / AverageMs : 0f;
        public float Low1Fps => Low1Ms > 0f ? 1000f / Low1Ms : 0f;
        public float Low01Fps => Low01Ms > 0f ? 1000f / Low01Ms : 0f;
    }

    /// <summary>
    /// Measures frames over a window: every frame time, the engine's own main thread / render thread / GPU split
    /// where the platform reports it, and a set of the engine's profiler counters. Shared by the probe and the
    /// benchmark. The component is switched off (no Update, no FixedUpdate, no recorder running) except between
    /// Open and Close, and a window allocates nothing: the buffers are made once.
    /// </summary>
    public sealed class PerfSampler : MonoBehaviour
    {
        /// <summary>One of the engine's profiler statistics, read once per frame while a window is open.</summary>
        public sealed class Stat
        {
            public readonly string Name;
            public readonly bool AllThreads;
            public ProfilerRecorder Recorder;
            public double Sum;
            public long Peak;
            public int Samples;

            public Stat(string name, bool allThreads)
            {
                Name = name;
                AllThreads = allThreads;
            }

            public bool Valid => Recorder.Valid;
            public ProfilerMarkerDataUnit Unit => Recorder.Valid ? Recorder.UnitType : ProfilerMarkerDataUnit.Undefined;
            public double Average => Samples > 0 ? Sum / Samples : 0.0;
        }

        private const int Capacity = 16384;   // frames: ten seconds at 1600 frames a second

        private readonly float[] _frames = new float[Capacity];
        private readonly float[] _sorted = new float[Capacity];
        private readonly FrameTiming[] _timing = new FrameTiming[1];

        // The engine's own split of the frame. Following these three is also what switches the engine's frame
        // timing on in a build that ships with it off, so every window follows them, whatever else it asks for.
        private readonly Stat[] _split =
        {
            new Stat("CPU Main Thread Frame Time", false), new Stat("CPU Render Thread Frame Time", false), new Stat("GPU Frame Time", false),
        };

        private Stat[] _stats = Array.Empty<Stat>();
        private int _count;
        private int _fixedSteps;
        private int _collectionsAtOpen;
        private float _openedAt;
        private bool _skipFirst;
        private int _timed;
        private double _main, _render, _gpu, _presentWait;

        public bool IsOpen => enabled;

        /// <summary>Seconds the open window has been running.</summary>
        public float Elapsed => enabled ? Time.realtimeSinceStartup - _openedAt : 0f;

        /// <summary>Every frame time of the window just closed (or still open), in order, in milliseconds.</summary>
        public float[] Frames => _frames;
        public int FrameCount => _count;

        private void Awake()
        {
            enabled = false;
        }

        /// <summary>Which statistics to follow from now on. Recorders are only made here, never in a window.</summary>
        public void Follow(Stat[] stats)
        {
            Release();
            _stats = stats ?? Array.Empty<Stat>();
            foreach (Stat stat in _stats)
            {
                ProfilerRecorderOptions options = ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame;
                if (!stat.AllThreads)
                {
                    options |= ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
                }
                // Looked up by name in every category the engine has: a name it does not know leaves the recorder invalid.
                stat.Recorder = Find(stat.Name, options);
            }
        }

        internal static ProfilerRecorder Find(string name, ProfilerRecorderOptions options)
        {
            foreach (ProfilerCategory category in Categories)
            {
                var recorder = new ProfilerRecorder(category, name, 1, options);
                if (recorder.Valid)
                {
                    return recorder;
                }
                recorder.Dispose();
            }
            return default;
        }

        private static readonly ProfilerCategory[] Categories =
        {
            ProfilerCategory.Render, ProfilerCategory.Memory, ProfilerCategory.Internal, ProfilerCategory.Scripts,
            ProfilerCategory.Animation, ProfilerCategory.Physics, ProfilerCategory.Gui, ProfilerCategory.Ai,
            ProfilerCategory.Particles, ProfilerCategory.Lighting, ProfilerCategory.Loading, ProfilerCategory.Audio,
            ProfilerCategory.Vr, ProfilerCategory.Input, ProfilerCategory.Video, ProfilerCategory.Network,
        };

        public void Open()
        {
            _count = 0;
            _fixedSteps = 0;
            _timed = 0;
            _main = _render = _gpu = _presentWait = 0.0;
            _collectionsAtOpen = GC.CollectionCount(0);
            _openedAt = Time.realtimeSinceStartup;
            _skipFirst = true;   // the frame a window opens in began before it
            foreach (Stat stat in _split)
            {
                if (!stat.Recorder.Valid)
                {
                    stat.Recorder = Find(stat.Name, ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame);
                }
                Begin(stat);
            }
            foreach (Stat stat in _stats)
            {
                Begin(stat);
            }
            enabled = true;
        }

        private static void Begin(Stat stat)
        {
            stat.Sum = 0.0;
            stat.Peak = 0;
            stat.Samples = 0;
            if (stat.Recorder.Valid)
            {
                stat.Recorder.Start();
            }
        }

        public PerfWindow Close()
        {
            enabled = false;
            foreach (Stat stat in _split)
            {
                if (stat.Recorder.Valid)
                {
                    stat.Recorder.Stop();
                }
            }
            foreach (Stat stat in _stats)
            {
                if (stat.Recorder.Valid)
                {
                    stat.Recorder.Stop();
                }
            }

            var window = new PerfWindow
            {
                Frames = _count,
                Seconds = Time.realtimeSinceStartup - _openedAt,
                FixedSteps = _fixedSteps,
                Collections = GC.CollectionCount(0) - _collectionsAtOpen,
                TimedFrames = _timed,
                MainMs = -1f,
                RenderMs = -1f,
                GpuMs = -1f,
                PresentWaitMs = -1f,
            };
            if (_timed > 0)
            {
                window.MainMs = (float)(_main / _timed);
                window.RenderMs = (float)(_render / _timed);
                window.GpuMs = (float)(_gpu / _timed);
                window.PresentWaitMs = (float)(_presentWait / _timed);
            }
            else if (_split[0].Samples > 0 && _split[0].Sum > 0.0)
            {
                // The frame timing manager stayed silent: the same three numbers as the engine's counters report them.
                window.TimedFrames = _split[0].Samples;
                window.MainMs = (float)(_split[0].Average / 1000000.0);
                window.RenderMs = (float)(_split[1].Average / 1000000.0);
                window.GpuMs = (float)(_split[2].Average / 1000000.0);
            }
            if (_count == 0)
            {
                return window;
            }

            double total = 0.0;
            for (int i = 0; i < _count; i++)
            {
                total += _frames[i];
            }
            window.AverageMs = (float)(total / _count);
            Array.Copy(_frames, _sorted, _count);
            Array.Sort(_sorted, 0, _count);
            window.MedianMs = _sorted[_count / 2];
            window.WorstMs = _sorted[_count - 1];
            window.Low1Ms = MeanOfSlowest(Mathf.Max(1, _count / 100));
            window.Low01Ms = MeanOfSlowest(Mathf.Max(1, _count / 1000));
            return window;
        }

        private float MeanOfSlowest(int frames)
        {
            double total = 0.0;
            for (int i = _count - frames; i < _count; i++)
            {
                total += _sorted[i];
            }
            return (float)(total / frames);
        }

        private static readonly int HotUpdate = HotSpots.Slot("Performance: PerfSampler.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (_skipFirst)
            {
                _skipFirst = false;
                return;
            }
            if (_count < Capacity)
            {
                _frames[_count++] = Time.unscaledDeltaTime * 1000f;
            }

            // The engine answers a few frames late, and only where the platform supports it.
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timing) > 0 && _timing[0].cpuFrameTime > 0.0)
            {
                _timed++;
                _main += _timing[0].cpuMainThreadFrameTime;
                _render += _timing[0].cpuRenderThreadFrameTime;
                _gpu += _timing[0].gpuFrameTime;
                _presentWait += _timing[0].cpuMainThreadPresentWaitTime;
            }

            for (int i = 0; i < _split.Length; i++)
            {
                Read(_split[i]);
            }
            for (int i = 0; i < _stats.Length; i++)
            {
                Read(_stats[i]);
            }
        }

        private static void Read(Stat stat)
        {
            if (!stat.Recorder.Valid)
            {
                return;
            }
            long value = stat.Recorder.LastValue;   // the frame before this one, complete
            stat.Sum += value;
            stat.Samples++;
            if (value > stat.Peak)
            {
                stat.Peak = value;
            }
        }

        private static readonly int HotFixedUpdate = HotSpots.Slot("Performance: PerfSampler.FixedUpdate");
        private void FixedUpdate()
        {
            using var hotSpot = HotSpots.Time(HotFixedUpdate);
            _fixedSteps++;
        }

        private void Release()
        {
            foreach (Stat stat in _stats)
            {
                if (stat.Recorder.Valid)
                {
                    stat.Recorder.Dispose();
                }
            }
            _stats = Array.Empty<Stat>();
        }

        private void OnDestroy()
        {
            foreach (Stat stat in _split)
            {
                if (stat.Recorder.Valid)
                {
                    stat.Recorder.Dispose();
                }
            }
            Release();
        }
    }
}
