using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BAModAPI;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The ten-second capture (F7): frame times, the thread split, the engine's counters and a census of the scene, written
    /// to RealLifeOverhaul_perf_HHmmss.txt with a screenshot. Nothing runs unless a capture is asked for.
    /// </summary>
    public sealed class PerfProbe
    {
        private const float Seconds = 10f;   // seconds measured

        private static readonly string[] Counters =
        {
            "Main Thread", "CPU Total Frame Time", "CPU Main Thread Frame Time", "CPU Render Thread Frame Time", "GPU Frame Time",
            "GC Allocated In Frame", "GC Allocation In Frame Count", "GC Used Memory", "GC Reserved Memory", "System Used Memory", "Total Used Memory",
            "Draw Calls Count", "Batches Count", "SetPass Calls Count", "Triangles Count", "Vertices Count", "Shadow Casters Count",
            "Visible Skinned Meshes Count", "Render Textures Changes Count", "Used Buffers Count", "Vertex Buffer Upload In Frame Count",
            "Index Buffer Upload In Frame Count", "Dynamic Batches Count", "Static Batches Count", "Instanced Batches Count",
            "Render Textures Count", "Render Textures Bytes", "Used Textures Bytes", "Video Memory Bytes",
        };

        // Timed sections of the main thread; a build without profiling answers for few. What this build offers is written
        // once to RealLifeOverhaul_perf_markers.txt.
        private static readonly string[] MainThreadMarkers =
        {
            "PlayerLoop", "Initialization.PlayerUpdateTime", "EarlyUpdate.UpdatePreloading", "EarlyUpdate.UpdateMainGameViewRect",
            "FixedUpdate.ScriptRunBehaviourFixedUpdate", "FixedUpdate.PhysicsFixedUpdate", "FixedUpdate.DirectorFixedUpdate",
            "PreUpdate.AIUpdate", "PreUpdate.PhysicsUpdate", "PreUpdate.SendMouseEvents",
            "Update.ScriptRunBehaviourUpdate", "Update.ScriptRunDelayedDynamicFrameRate", "Update.DirectorUpdate",
            "PreLateUpdate.AIUpdatePostScript", "PreLateUpdate.DirectorUpdateAnimationBegin", "PreLateUpdate.DirectorUpdateAnimationEnd",
            "PreLateUpdate.ScriptRunBehaviourLateUpdate", "PreLateUpdate.ParticleSystemBeginUpdateAll", "PreLateUpdate.ConstraintManagerUpdate",
            "PostLateUpdate.UpdateAllSkinnedMeshes", "PostLateUpdate.UpdateAllRenderers", "PostLateUpdate.PlayerUpdateCanvases",
            "PostLateUpdate.PlayerEmitCanvasGeometry", "PostLateUpdate.UpdateAudio", "PostLateUpdate.ParticleSystemEndUpdateAll",
            "PostLateUpdate.FinishFrameRendering", "PostLateUpdate.PresentAfterDraw", "PostLateUpdate.UpdateCustomRenderTextures",
            "BehaviourUpdate", "LateBehaviourUpdate", "FixedBehaviourUpdate", "CoroutinesDelayedCalls",
            "Animators.Update", "Animator.Update", "Director.ProcessFrame", "Director.PrepareFrame", "MeshSkinning.Update", "MeshSkinning.Skin",
            "Physics.Simulate", "Physics.Processing", "Physics.FetchResults", "Physics.SyncColliderTransform", "Physics.SyncRigidbodyTransform", "Physics.UpdateBodies",
            "NavMeshManager", "CrowdManager.Update", "NavMesh.Carving",
            "Canvas.SendWillRenderCanvases", "UIEvents.WillRenderCanvases", "UGUI.Rendering.UpdateBatches", "Canvas.BuildBatch", "Canvas.RenderOverlays",
            "Layout", "Render", "TMP.GenerateText", "EventSystem.Update",
            "Camera.Render", "RenderPipelineManager.DoRenderLoop_Internal", "Inl_HDRenderPipelineAllRenderRequest", "HDRenderPipelineAllRenderRequest",
            "HDRenderPipelineRenderCamera", "CullScriptable", "SceneCulling", "CullSceneDynamicObjects", "Shadows.CullShadowCasters", "CullLightPerObjects",
            "RenderGraph Execute", "Inl_Execute", "Inl_Compile", "ScriptableRenderContext.Submit", "Inl_ScriptableRenderContext.Submit",
            "ReflectionProbes.Update", "ParticleSystem.Update", "ParticleSystem.EndUpdateAll", "UpdateRendererBoundingVolumes", "TransformChangedDispatch",
            "Gfx.WaitForPresentSyncPoint", "Gfx.WaitForRenderThread", "WaitForJobGroupID", "Semaphore.WaitForSignal", "GC.Collect", "GC.Alloc",
            "Loading.UpdatePreloading", "Application.Integrate Assets in Background", "AsyncUploadManager.ScheduleAsyncRead", "Audio.Update",
        };

        private static readonly string[] AnyThreadMarkers =
        {
            "Gfx.PresentFrame", "Gfx.WaitForGfxCommandsFromMainThread", "Gfx.ProcessCommands", "GfxDeviceD3D11Base.DrawBuffers", "Gfx.UploadTexture",
            "RenderLoop.DrawSRPBatcher", "SRPBatcher.Flush", "Shadows.Draw", "RenderLoop.Draw", "Mesh.DrawVBO", "DrawBuffersBatchMode",
            "MeshSkinning.GPUSkinning", "MeshSkinning.Render", "Camera.ImageEffects", "Shader.CreateGPUProgram",
        };

        private readonly PerfSampler _sampler;
        private readonly PerfLoop _loop;
        private readonly IModLogger _logger;
        private PerfSampler.Stat[] _stats;
        private bool _markersWritten;

        // what the UI rebuilds while the window is open, read just before the UI system clears its queues
        private readonly Dictionary<ICanvasElement, int> _layoutRebuilds = new Dictionary<ICanvasElement, int>(256);
        private readonly Dictionary<ICanvasElement, int> _graphicRebuilds = new Dictionary<ICanvasElement, int>(256);
        private IList<ICanvasElement> _layoutQueue;
        private IList<ICanvasElement> _graphicQueue;
        private long _layoutTotal, _graphicTotal;
        private int _uiFrames;
        private bool _uiHooked;
        private Canvas.WillRenderCanvases _countUi;
        private string _uiNote;

        public bool Running { get; private set; }

        public PerfProbe(PerfSampler sampler, PerfLoop loop, IModLogger logger)
        {
            _sampler = sampler;
            _loop = loop;
            _logger = logger;
        }

        public void Begin()
        {
            if (Running || _sampler.IsOpen || _loop.IsOpen)
            {
                return;
            }
            if (_stats == null)
            {
                var stats = new List<PerfSampler.Stat>(Counters.Length + MainThreadMarkers.Length + AnyThreadMarkers.Length);
                foreach (string name in Counters)
                {
                    stats.Add(new PerfSampler.Stat(name, false));
                }
                foreach (string name in MainThreadMarkers)
                {
                    stats.Add(new PerfSampler.Stat(name, false));
                }
                foreach (string name in AnyThreadMarkers)
                {
                    stats.Add(new PerfSampler.Stat(name, true));
                }
                _stats = stats.ToArray();
            }
            _sampler.Follow(_stats);
            HookUi();
            Running = true;
            _sampler.Open();
            _loop.Open();
            _logger?.Info("Performance probe: measuring for " + Seconds + " seconds. Play normally.");
            PerfDirector.Notify("reallife_perf_probe_started", 5f);
        }

        /// <summary>Called each frame by the director while a capture runs.</summary>
        public void Step()
        {
            if (_sampler.Elapsed < Seconds)
            {
                return;
            }
            Finish();
        }

        /// <summary>Stops without a report: the scene is going.</summary>
        public void Cancel()
        {
            if (Running)
            {
                Running = false;
                UnhookUi();
                _loop.Close();
                _sampler.Close();
                _sampler.Follow(null);
            }
        }

        private void Finish()
        {
            Running = false;
            UnhookUi();
            PerfLoopResult loop = _loop.Close();
            PerfWindow window = _sampler.Close();
            try
            {
                string root = Application.persistentDataPath;
                string stamp = DateTime.Now.ToString("HHmmss");
                string shots = Path.Combine(root, "RealLifeOverhaul_shots");
                Directory.CreateDirectory(shots);
                ScreenCapture.CaptureScreenshot(Path.Combine(shots, "perf_" + stamp + ".png"));
                string report = Path.Combine(root, "RealLifeOverhaul_perf_" + stamp + ".txt");
                File.WriteAllText(report, Describe(window, loop));
                if (!_markersWritten)
                {
                    _markersWritten = true;
                    File.WriteAllText(Path.Combine(root, "RealLifeOverhaul_perf_markers.txt"), DescribeAvailable());
                }
                _logger?.Info("Performance probe: " + PerfText.F1(window.Fps) + " fps average, report written to " + report);
                PerfDirector.Notify("reallife_perf_probe_done", 8f);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            finally
            {
                _sampler.Follow(null);   // no recorder stays alive between captures
            }
        }

        private string Describe(PerfWindow window, PerfLoopResult loop)
        {
            var text = new StringBuilder(64 * 1024);
            Camera camera = GameManager.GetMainCamera();
            text.AppendLine("Real Life Overhaul performance probe  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  mod " + typeof(PerfProbe).Assembly.GetName().Version
                            + "  Unity " + Application.unityVersion);
            text.AppendLine("where: " + PerfGame.Where()
                            + (camera != null
                                ? "  camera '" + camera.name + "' at " + camera.transform.position.ToString("F1") + " looking " + camera.transform.forward.ToString("F2")
                                : "  no main camera"));
            text.AppendLine("screen: " + Screen.width + "x" + Screen.height + "  quality level " + QualitySettings.GetQualityLevel());

            text.AppendLine();
            text.AppendLine("===== FRAME TIMES over " + PerfText.F1(window.Seconds) + " s, " + window.Frames + " frames =====");
            text.AppendLine("  average " + PerfText.F2(window.AverageMs) + " ms = " + PerfText.F1(window.Fps) + " fps    median " + PerfText.F2(window.MedianMs) + " ms");
            text.AppendLine("  1% low  " + PerfText.F2(window.Low1Ms) + " ms = " + PerfText.F1(window.Low1Fps) + " fps    0.1% low " + PerfText.F2(window.Low01Ms) + " ms = "
                            + PerfText.F1(window.Low01Fps) + " fps    worst frame " + PerfText.F2(window.WorstMs) + " ms   (a 'low' is the mean of the slowest 1% / 0.1% of frames)");
            var spread = new PerfText.Ranges(4f, 6f, 8f, 10f, 12f, 16f, 25f, 50f, 100f);
            int hitches = 0;
            for (int i = 0; i < _sampler.FrameCount; i++)
            {
                float ms = _sampler.Frames[i];
                spread.Add(ms);
                if (ms > window.MedianMs * 2.5f)
                {
                    hitches++;
                }
            }
            text.AppendLine("  frames by duration (ms): " + spread);
            text.AppendLine("  hitches (frames over 2.5x the median): " + hitches + "    physics steps " + window.FixedSteps + " = "
                            + PerfText.F2(window.Frames > 0 ? (float)window.FixedSteps / window.Frames : 0f) + " per frame    garbage collections in the window: " + window.Collections);

            text.AppendLine();
            text.AppendLine("===== WHERE THE FRAME GOES (engine frame timing, " + window.TimedFrames + " frames answered) =====");
            if (window.TimedFrames == 0)
            {
                text.AppendLine("  The engine's frame timing reported nothing in this build (feature enabled: " + FrameTimingManager.IsFeatureEnabled()
                                + "). Use the counters below: 'Main Thread' and the Gfx.* waits.");
            }
            else
            {
                text.AppendLine("  main thread " + PerfText.Ms(window.MainMs) + " ms    render thread " + PerfText.Ms(window.RenderMs) + " ms    GPU " + PerfText.Ms(window.GpuMs)
                                + " ms    main thread waiting for the display " + PerfText.Ms(window.PresentWaitMs) + " ms");
                text.AppendLine("  reading: " + Reading(window));
            }

            text.AppendLine();
            text.AppendLine("===== WHERE THE MAIN THREAD'S FRAME GOES, step by step =====");
            _loop.Describe(text, loop);

            text.AppendLine();
            text.AppendLine("===== ENGINE COUNTERS (per frame: average, peak) =====");
            int silent = 0;
            foreach (PerfSampler.Stat stat in _stats)
            {
                if (!stat.Valid)
                {
                    silent++;
                    continue;
                }
                if (stat.Samples == 0 || (stat.Sum == 0.0 && stat.Unit == ProfilerMarkerDataUnit.TimeNanoseconds))
                {
                    continue;   // a timed section that never ran
                }
                text.Append("  ").Append(stat.Name.PadRight(46)).Append(Scaled(stat.Average, stat.Unit).PadLeft(14)).Append(Scaled(stat.Peak, stat.Unit).PadLeft(14));
                text.AppendLine(stat.AllThreads ? "   (all threads)" : string.Empty);
            }
            text.AppendLine("  (" + silent + " of " + _stats.Length + " statistics asked for are not offered by this build; RealLifeOverhaul_perf_markers.txt lists what is.");
            text.AppendLine("   The draw call, batch and triangle counters exist but stay at zero in a release build, and the render pipeline's own timers are compiled out of it:"
                            + " the step table above is what replaces them.)");

            text.AppendLine();
            text.AppendLine("===== UI REBUILDS during the window =====");
            DescribeUi(text);

            text.AppendLine();
            text.AppendLine("===== THE MOD'S PERFORMANCE TWEAKS at the time =====");
            foreach (PerfTweak tweak in PerfTweaks.All)
            {
                if (tweak.Trial && !tweak.Active)
                {
                    continue;
                }
                text.AppendLine("  " + tweak.Id.PadRight(22) + (tweak.Active ? "ON " : "off") + (tweak.Faulted ? " FAULTED" : string.Empty)
                                + (tweak.Wanted != tweak.Active && !tweak.Faulted ? " (option " + (tweak.Wanted ? "on" : "off") + ")" : string.Empty) + "  " + tweak.Describe());
            }

            PerfCensus.Write(text, camera);
            return text.ToString();
        }

        // cautious: it names only what the numbers show
        private static string Reading(PerfWindow w)
        {
            if (w.PresentWaitMs > w.AverageMs * 0.25f)
            {
                return "the main thread spends " + PerfText.F1(w.PresentWaitMs) + " ms of each frame waiting for the display: a frame cap or v-sync is holding the rate, not the game's cost.";
            }
            if (w.GpuMs > 0f && w.GpuMs >= w.AverageMs * 0.92f)
            {
                return "the GPU time is about the whole frame: the GPU is the limit here.";
            }
            if (w.MainMs >= w.RenderMs && w.MainMs >= w.AverageMs * 0.8f)
            {
                return "the main thread fills the frame: game scripts, animation, culling and the render pipeline's own CPU work are the limit.";
            }
            if (w.RenderMs > w.MainMs && w.RenderMs >= w.AverageMs * 0.8f)
            {
                return "the render thread fills the frame: the number of draw calls and state changes sent to Direct3D 11 is the limit.";
            }
            return "no single thread fills the frame by these numbers; compare 'Main Thread' and the Gfx.* waits below.";
        }

        private static string Scaled(double value, ProfilerMarkerDataUnit unit)
        {
            switch (unit)
            {
                case ProfilerMarkerDataUnit.TimeNanoseconds:
                    return (value / 1000000.0).ToString("F3") + " ms";
                case ProfilerMarkerDataUnit.Bytes:
                    return value >= 1048576.0 ? (value / 1048576.0).ToString("F1") + " MB" : (value / 1024.0).ToString("F1") + " KB";
                default:
                    return value >= 1000000.0 ? (value / 1000000.0).ToString("F2") + " M" : value.ToString("F1");
            }
        }

        private void HookUi()
        {
            _layoutRebuilds.Clear();
            _graphicRebuilds.Clear();
            _layoutTotal = _graphicTotal = 0;
            _uiFrames = 0;
            _uiNote = null;
            try
            {
                if (_layoutQueue == null)
                {
                    CanvasUpdateRegistry registry = CanvasUpdateRegistry.instance;
                    _layoutQueue = GameContract.LayoutQueue.Field?.GetValue(registry) as IList<ICanvasElement>;
                    _graphicQueue = GameContract.GraphicQueue.Field?.GetValue(registry) as IList<ICanvasElement>;
                }
                if (_layoutQueue == null || _graphicQueue == null)
                {
                    _uiNote = "the UI system's rebuild queues could not be reached in this version";
                    return;
                }
                _countUi = _countUi ?? new Canvas.WillRenderCanvases(Guard.Wrap(CountUi));   // made once, through the guard
                Canvas.preWillRenderCanvases += _countUi;
                _uiHooked = true;
            }
            catch (Exception e)
            {
                _uiNote = "could not watch the UI: " + e.Message;
            }
        }

        private void UnhookUi()
        {
            if (_uiHooked)
            {
                _uiHooked = false;
                Canvas.preWillRenderCanvases -= _countUi;
            }
        }

        // once a frame while a capture is open, before the UI system rebuilds and empties its queues
        private void CountUi()
        {
            _uiFrames++;
            _layoutTotal += Count(_layoutQueue, _layoutRebuilds);
            _graphicTotal += Count(_graphicQueue, _graphicRebuilds);
        }

        private static int Count(IList<ICanvasElement> queue, Dictionary<ICanvasElement, int> into)
        {
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                ICanvasElement element = queue[i];
                if (element == null)
                {
                    continue;
                }
                into.TryGetValue(element, out int seen);
                into[element] = seen + 1;
            }
            return count;
        }

        private void DescribeUi(StringBuilder text)
        {
            if (_uiNote != null)
            {
                text.AppendLine("  " + _uiNote);
                return;
            }
            if (_uiFrames == 0)
            {
                text.AppendLine("  no UI frame was seen");
                return;
            }
            text.AppendLine("  per frame on average: layout rebuilds " + PerfText.F2((float)_layoutTotal / _uiFrames) + "  graphic rebuilds (text, images re-meshed) "
                            + PerfText.F2((float)_graphicTotal / _uiFrames) + "   over " + _uiFrames + " frames. Any rebuild also re-batches the whole canvas it sits on.");
            WriteRebuilds(text, "elements rebuilding their graphic most often", _graphicRebuilds);
            WriteRebuilds(text, "elements rebuilding their layout most often", _layoutRebuilds);
        }

        private void WriteRebuilds(StringBuilder text, string title, Dictionary<ICanvasElement, int> counts)
        {
            var rows = new List<KeyValuePair<ICanvasElement, int>>(counts);
            rows.Sort((a, b) => b.Value.CompareTo(a.Value));
            text.AppendLine("  " + title + " (times in " + _uiFrames + " frames):");
            for (int i = 0; i < rows.Count && i < 20; i++)
            {
                string who = "(destroyed)";
                if (rows[i].Key is Component component && component != null)
                {
                    Canvas canvas = component.GetComponentInParent<Canvas>();
                    who = component.GetType().Name + " '" + component.name + "' under '" + PerfText.Above(component.transform, 3) + "' on canvas '"
                          + (canvas != null ? canvas.rootCanvas.name : "none") + "'";
                    if (component is TMPro.TMP_Text label)
                    {
                        string shown = label.text ?? string.Empty;
                        who += " text \"" + (shown.Length > 24 ? shown.Substring(0, 24) + "..." : shown).Replace("\n", " ") + "\"";
                    }
                }
                text.AppendLine("  " + rows[i].Value.ToString().PadLeft(6) + "  " + who);
            }
        }

        // every statistic this build of the engine can be asked for: written once a session
        private static string DescribeAvailable()
        {
            var handles = new List<ProfilerRecorderHandle>(4096);
            ProfilerRecorderHandle.GetAvailable(handles);
            var names = new List<string>(handles.Count);
            foreach (ProfilerRecorderHandle handle in handles)
            {
                ProfilerRecorderDescription d = ProfilerRecorderHandle.GetDescription(handle);
                names.Add(d.Category.Name + "\t" + d.UnitType + "\t" + d.Name);
            }
            names.Sort(string.CompareOrdinal);
            var text = new StringBuilder(names.Count * 48);
            text.AppendLine("Profiler statistics this build offers (" + names.Count + "): category, unit, name");
            foreach (string name in names)
            {
                text.AppendLine(name);
            }
            return text.ToString();
        }
    }
}
