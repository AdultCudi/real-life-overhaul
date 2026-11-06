using System;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The session record, part: the frame's pace, the engine's frame timing and the minute's sums of where its frames went.</summary>
    public sealed partial class SessionWatch
    {
        // The frame rate cap and vSync, as the game has set them.
        private static string Pace()
        {
            int sync = QualitySettings.vSyncCount;
            int cap = Application.targetFrameRate;
            return (sync > 0 ? "vSync on (every " + (sync == 1 ? "refresh" : sync + " refreshes") + ")" : "vSync off")
                   + ", " + (cap > 0 ? "frame rate capped at " + cap + " a second" : "no frame rate cap") + (sync > 0 && cap > 0 ? " (vSync overrides the cap)" : string.Empty);
        }

        // Whether the build has the engine's frame timing switched on (Player Settings, Frame Timing Stats). In a method of its own:
        // an engine without FrameTimingManager fails here alone.
        private static bool TimingOffered()
        {
            try
            {
                return FrameTimingManager.IsFeatureEnabled();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void CaptureTiming()
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) >= 1 && _timings[0].gpuFrameTime > _gpuWorst)
            {
                _gpuWorst = _timings[0].gpuFrameTime;
            }
        }

        // The worst of the last few frames the engine has figures for: they come a few frames late.
        private void ReadTiming(ref FrameFigures figures)
        {
            uint got = FrameTimingManager.GetLatestTimings((uint)_timings.Length, _timings);
            for (int i = 0; i < got && i < _timings.Length; i++)
            {
                figures.GpuMs = Math.Max(figures.GpuMs, _timings[i].gpuFrameTime);
                figures.RenderThreadMs = Math.Max(figures.RenderThreadMs, _timings[i].cpuRenderThreadFrameTime);
            }
        }

        // The frame before this one as the step timing and the system saw it, the engine's own figures beside them.
        private FrameFigures Figures(float took, bool withTiming)
        {
            FrameFigures figures = FrameFigures.Unknown(took * 1000.0);
            if (FrameSteps.Valid)
            {
                figures.SpanMs = FrameSteps.LastSpanMs;
                figures.StepsMs = FrameSteps.LastStepsMs;
                figures.WaitMs = FrameSteps.LastMs(FrameSteps.Wait);
                figures.ThreadMs = MachineClock.ThreadMs;
                figures.ProcessMs = MachineClock.ProcessMs;
                figures.Busy = MachineClock.Busy;
                figures.Others = MachineClock.Others;
                figures.Faults = MachineClock.Faults;
            }
            figures.Processors = SystemInfo.processorCount;
            if (withTiming && _timing)
            {
                try
                {
                    ReadTiming(ref figures);
                }
                catch (Exception)
                {
                    _timing = false;   // the engine's figures are left out from now on
                }
            }
            return figures;
        }

        // The minute's sums of where its frames went.
        private void Split(float took)
        {
            if (_timing)
            {
                try
                {
                    CaptureTiming();
                }
                catch (Exception)
                {
                    _timing = false;
                }
            }
            if (!FrameSteps.Valid)
            {
                return;
            }
            FrameFigures f = Figures(took, false);
            _split++;
            _spanSum += f.SpanMs;
            _waitSum += f.WaitMs;
            _waitWorst = Math.Max(_waitWorst, f.WaitMs);
            _outsideSum += f.OutsideMs;
            _outsideWorst = Math.Max(_outsideWorst, f.OutsideMs);
            if (f.ThreadMs >= 0.0)
            {
                _threadFrames++;
                _threadSum += f.ThreadMs;
                _threadSpan += f.SpanMs;
            }
            if (f.Others >= 0.0)
            {
                _othersFrames++;
                _othersSum += f.Others;
                _othersWorst = Math.Max(_othersWorst, f.Others);
            }
            if (f.Faults >= 0L)
            {
                _faultFrames++;
                _faultsSum += f.Faults;
                _faultsWorst = Math.Max(_faultsWorst, f.Faults);
            }
            if (took > 0.033f)
            {
                _causes[(int)FrameRules.Cause(f)]++;
            }
        }

        // Whether the runtime counts what the main thread allocates (then the heap's growth under each of the mod's scripts
        // is exact, and read in every frame): a megabyte asked for must show in the count. Once, when the record starts.
        private static bool ExactCount()
        {
            try
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                var probe = new byte[1 << 20];
                long after = GC.GetAllocatedBytesForCurrentThread();
                GC.KeepAlive(probe);
                return after - before >= 1 << 20;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
