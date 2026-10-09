using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Rendering;

namespace RealLifeOverhaul
{
    /// <summary>What the lap timer found over one window, in milliseconds per frame.</summary>
    public struct PerfLoopResult
    {
        public bool Valid;
        public int Frames;
        public float TotalMs;      // every lap added up: the main thread's whole frame
        public float ScriptsMs;    // every script Update, LateUpdate, FixedUpdate, coroutine and timer, the game's and every mod's
        public float PhysicsMs;
        public float AnimationMs;  // animators, timelines, constraints, skinning set-up
        public float NavigationMs; // navigation mesh agents and obstacles
        public float UiMs;         // canvas layout, rebuilds and batching (drawing the batches is in RenderMs)
        public float RenderMs;     // everything the main thread does to draw: culling, the render pipeline, handing over to the render thread
        public float PipelineMs;   // the part of RenderMs spent inside the render pipeline's own code, all cameras
        public float AudioMs;
        public float WaitMs;       // waiting for the display or a frame cap
        public float OtherMs;
    }

    /// <summary>
    /// Says where the main thread's frame goes in a build whose profiler is silent. The engine runs a frame as a
    /// fixed list of about 130 steps (the "player loop": input, physics, script Update, animation, script
    /// LateUpdate, canvases, rendering...), and lets code put steps of its own in between. While a window is open
    /// this puts a timestamp step in front of every engine step and adds up the time between consecutive
    /// timestamps, so each engine step gets the time it took (including any job it waited for). The render
    /// pipeline's per-camera callbacks split the rendering step by camera in the same way, which also proves
    /// which cameras are really rendered each frame.
    ///
    /// Outside a window the game's own loop is back exactly as it was found and nothing here runs. Inside one
    /// the cost is about 150 calls of four instructions per frame (well under 0.05 ms).
    /// </summary>
    public sealed class PerfLoop
    {
        private const int MaxCameras = 12;
        private const string Added = "[added by a script] ";   // a step that is not the engine's own: a task or tween library, a mod

        private PlayerLoopSystem _original;
        private bool _installed;
        private string[] _names = Array.Empty<string>();
        private long[] _ticks = Array.Empty<long>();
        private int[] _calls = Array.Empty<int>();
        private int _current = -1;
        private long _last;
        private int _frames;

        // The rendering step, split by the render pipeline's callbacks.
        private readonly int[] _cameraIds = new int[MaxCameras];
        private readonly string[] _cameraNames = new string[MaxCameras];
        private readonly long[] _cameraSetup = new long[MaxCameras];
        private readonly long[] _cameraRender = new long[MaxCameras];
        private readonly int[] _cameraFrames = new int[MaxCameras];
        private int _cameras;
        private long _pipelineTicks, _pipelineEnd, _contextStart, _cameraLast;
        private int _begun = -1;          // the camera whose 'begin' was the last callback, or -1
        private int _contexts;
        private bool _hooked;

        public bool IsOpen => _installed;

        /// <summary>Starts timing. Safe to call when the loop cannot be changed: the result is then simply not valid.</summary>
        public void Open()
        {
            if (_installed || !PerfTweaks.StepTiming)
            {
                return;
            }
            _original = PlayerLoop.GetCurrentPlayerLoop();
            PlayerLoopSystem[] phases = _original.subSystemList;
            if (phases == null || phases.Length == 0)
            {
                return;
            }

            var names = new List<string>(192);
            var timedPhases = new PlayerLoopSystem[phases.Length];
            for (int p = 0; p < phases.Length; p++)
            {
                PlayerLoopSystem phase = phases[p];   // a copy: the game's own arrays are never written to
                PlayerLoopSystem[] steps = phase.subSystemList ?? Array.Empty<PlayerLoopSystem>();
                string phaseName = phase.type != null ? phase.type.Name : "Phase" + p;
                var timed = new PlayerLoopSystem[steps.Length * 2 + 1];
                for (int s = 0; s < steps.Length; s++)
                {
                    timed[s * 2] = Marker(names.Count);
                    Type type = steps[s].type;
                    bool engine = type != null && type.Namespace == "UnityEngine.PlayerLoop";
                    names.Add(phaseName + "." + (engine ? string.Empty : Added) + (type != null ? type.Name : "Step" + s));
                    timed[s * 2 + 1] = steps[s];
                }
                timed[steps.Length * 2] = Marker(names.Count);
                names.Add(phaseName + ".(after its last step, up to the next phase)");
                phase.subSystemList = timed;
                timedPhases[p] = phase;
            }

            _names = names.ToArray();
            _ticks = new long[_names.Length];
            _calls = new int[_names.Length];
            _current = -1;
            _frames = 0;
            _cameras = 0;
            _contexts = 0;
            _begun = -1;
            _pipelineTicks = _pipelineEnd = 0;
            Array.Clear(_cameraSetup, 0, MaxCameras);
            Array.Clear(_cameraRender, 0, MaxCameras);
            Array.Clear(_cameraFrames, 0, MaxCameras);

            PlayerLoopSystem timedLoop = _original;
            timedLoop.subSystemList = timedPhases;
            PlayerLoop.SetPlayerLoop(timedLoop);
            _installed = true;

            RenderPipelineManager.beginContextRendering += OnBeginContext;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
            RenderPipelineManager.endContextRendering += OnEndContext;
            _hooked = true;
        }

        /// <summary>Stops timing and puts the game's own loop back.</summary>
        public PerfLoopResult Close()
        {
            if (_hooked)
            {
                _hooked = false;
                RenderPipelineManager.beginContextRendering -= OnBeginContext;
                RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
                RenderPipelineManager.endCameraRendering -= OnEndCamera;
                RenderPipelineManager.endContextRendering -= OnEndContext;
            }
            if (!_installed)
            {
                return default;
            }
            _installed = false;
            PlayerLoop.SetPlayerLoop(_original);
            _original = default;
            return Summarise();
        }

        private PlayerLoopSystem Marker(int index)
        {
            return new PlayerLoopSystem { type = typeof(PerfLoop), updateDelegate = () => Lap(index) };
        }

        // Everything since the previous timestamp belongs to the step that timestamp stood in front of.
        private void Lap(int index)
        {
            long now = Stopwatch.GetTimestamp();
            if (_current >= 0)
            {
                _ticks[_current] += now - _last;
                _calls[_current]++;
            }
            if (index == 0)
            {
                _frames++;
            }
            _current = index;
            _last = now;
        }

        private void OnBeginContext(ScriptableRenderContext context, List<Camera> cameras)
        {
            _contextStart = _cameraLast = Stopwatch.GetTimestamp();
            _begun = -1;
            _contexts++;
        }

        // The pipeline calls this just before it culls for the camera. What came since the previous callback is
        // that camera's set-up (frame settings, volumes), unless another camera had just begun: then it was that one's culling.
        private void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            long now = Stopwatch.GetTimestamp();
            int slot = Slot(camera);
            if (slot >= 0)
            {
                if (_begun >= 0)
                {
                    _cameraRender[_begun] += now - _cameraLast;
                }
                else
                {
                    _cameraSetup[slot] += now - _cameraLast;
                }
                _cameraFrames[slot]++;
            }
            _begun = slot;
            _cameraLast = now;
        }

        // Called when the camera's picture has been handed to the render thread: culling, building and running the frame graph lie before it.
        private void OnEndCamera(ScriptableRenderContext context, Camera camera)
        {
            long now = Stopwatch.GetTimestamp();
            int slot = Slot(camera);
            if (slot >= 0)
            {
                _cameraRender[slot] += now - _cameraLast;
            }
            _begun = -1;
            _cameraLast = now;
        }

        private void OnEndContext(ScriptableRenderContext context, List<Camera> cameras)
        {
            long now = Stopwatch.GetTimestamp();
            _pipelineTicks += now - _contextStart;
            _pipelineEnd += now - _cameraLast;
        }

        private int Slot(Camera camera)
        {
            int id = camera.GetInstanceID();
            for (int i = 0; i < _cameras; i++)
            {
                if (_cameraIds[i] == id)
                {
                    return i;
                }
            }
            if (_cameras >= MaxCameras)
            {
                return -1;
            }
            _cameraIds[_cameras] = id;
            _cameraNames[_cameras] = camera.name + " (" + camera.cameraType + ")";   // once per camera per window
            return _cameras++;
        }

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        private PerfLoopResult Summarise()
        {
            var result = new PerfLoopResult { Frames = _frames };
            if (_frames < 2)
            {
                return result;
            }
            double scripts = 0, physics = 0, animation = 0, navigation = 0, ui = 0, render = 0, audio = 0, wait = 0, other = 0;
            for (int i = 0; i < _names.Length; i++)
            {
                double ms = Ms(_ticks[i]);
                switch (Group(_names[i]))
                {
                    case 'S': scripts += ms; break;
                    case 'P': physics += ms; break;
                    case 'A': animation += ms; break;
                    case 'N': navigation += ms; break;
                    case 'U': ui += ms; break;
                    case 'R': render += ms; break;
                    case 'O': audio += ms; break;
                    case 'W': wait += ms; break;
                    default: other += ms; break;
                }
            }
            result.Valid = true;
            result.ScriptsMs = (float)(scripts / _frames);
            result.PhysicsMs = (float)(physics / _frames);
            result.AnimationMs = (float)(animation / _frames);
            result.NavigationMs = (float)(navigation / _frames);
            result.UiMs = (float)(ui / _frames);
            result.RenderMs = (float)(render / _frames);
            result.AudioMs = (float)(audio / _frames);
            result.WaitMs = (float)(wait / _frames);
            result.OtherMs = (float)(other / _frames);
            result.PipelineMs = (float)(Ms(_pipelineTicks) / _frames);
            result.TotalMs = result.ScriptsMs + result.PhysicsMs + result.AnimationMs + result.NavigationMs + result.UiMs + result.RenderMs + result.AudioMs + result.WaitMs + result.OtherMs;
            return result;
        }

        /// <summary>
        /// Which share of the frame a step belongs to. The names are the engine's own (UnityEngine.PlayerLoop);
        /// a step somebody else added (a task library, a tween library, a mod) is script code.
        /// </summary>
        private static char Group(string name)
        {
            if (name.Contains("WaitForLastPresentation") || name.Contains("PresentAfterDraw"))
            {
                return 'W';
            }
            if (name.Contains("FinishFrameRendering") || name.Contains("UpdateAllRenderers") || name.Contains("UpdateAllSkinnedMeshes")
                || name.Contains("SortingGroups") || name.Contains("RendererNotify") || name.Contains("ReflectionProbe") || name.Contains("UpdateCustomRenderTextures")
                || name.Contains("ClearIntermediateRenderers"))
            {
                return 'R';
            }
            if (name.Contains(Added) || name.Contains("ScriptRun") || name.Contains("UniTask") || name.Contains("SendMouseEvents") || name.Contains("NewInputUpdate") || name.Contains("NewInputFixedUpdate") || name.Contains("TriggerEndOfFrameCallbacks"))
            {
                return 'S';
            }
            if (name.Contains("Physics") || name.Contains("Cloth"))
            {
                return 'P';
            }
            if (name.Contains("Director") || name.Contains("Animation") || name.Contains("Constraint") || name.Contains("LegacyAnimation"))
            {
                return 'A';
            }
            if (name.Contains(".AIUpdate"))
            {
                return 'N';
            }
            if (name.Contains("Canvas") || name.Contains("UIElements") || name.Contains("GUI") || name.Contains("UpdateRectTransform"))
            {
                return 'U';
            }
            if (name.Contains("Audio"))
            {
                return 'O';
            }
            if (name.Contains("(after its last step"))
            {
                return '-';
            }
            return '-';
        }

        /// <summary>The whole table for the probe's report: every step that took time, largest first, then the cameras.</summary>
        public void Describe(StringBuilder text, PerfLoopResult result)
        {
            if (!result.Valid)
            {
                text.AppendLine(PerfTweaks.StepTiming
                    ? "  The frame's steps could not be timed in this version of the game (" + _frames + " frames seen)."
                    : "  Not timed: the option 'Measurements time every step of the frame' is off.");
                return;
            }
            text.AppendLine("  measured by the mod on the main thread, " + _frames + " frames: a timestamp in front of each of the engine's " + _names.Length + " frame steps. Milliseconds per frame.");
            text.AppendLine("  by share:  scripts " + PerfText.F2(result.ScriptsMs) + "   rendering " + PerfText.F2(result.RenderMs) + " (of it inside the render pipeline " + PerfText.F2(result.PipelineMs) + ")   physics "
                            + PerfText.F2(result.PhysicsMs) + "   animation " + PerfText.F2(result.AnimationMs) + "   navigation " + PerfText.F2(result.NavigationMs) + "   UI " + PerfText.F2(result.UiMs)
                            + "   audio " + PerfText.F2(result.AudioMs) + "   waiting for the display " + PerfText.F2(result.WaitMs) + "   everything else " + PerfText.F2(result.OtherMs) + "   = " + PerfText.F2(result.TotalMs));
            text.AppendLine("  'scripts' is every Update, LateUpdate, FixedUpdate, coroutine and timer of the game and of every mod together: one script cannot be told from another without patching the game.");

            var order = new int[_names.Length];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }
            Array.Sort(order, (a, b) => _ticks[b].CompareTo(_ticks[a]));
            text.AppendLine("  steps, largest first (ms per frame, share of the frame, times run per frame):");
            double shown = 0.0;
            for (int i = 0; i < order.Length; i++)
            {
                int step = order[i];
                double ms = Ms(_ticks[step]) / _frames;
                if (ms < 0.005 && i >= 12)
                {
                    break;
                }
                shown += ms;
                text.Append("    ").Append(PerfText.F2(ms).PadLeft(7)).Append("  ").Append(PerfText.F1((float)(result.TotalMs > 0f ? ms / result.TotalMs * 100.0 : 0.0)).PadLeft(5)).Append("%  x")
                    .Append(PerfText.F2((float)_calls[step] / _frames)).Append("  ").AppendLine(_names[step]);
            }
            text.AppendLine("    " + PerfText.F2(result.TotalMs - shown).PadLeft(7) + "          all the smaller steps together");
            text.AppendLine("  how to read it: Update.ScriptRunBehaviourUpdate = every script's Update; PreLateUpdate.ScriptRunBehaviourLateUpdate = every LateUpdate; FixedUpdate.* run once per physics step;");
            text.AppendLine("  PostLateUpdate.FinishFrameRendering = culling, the render pipeline and handing the frame to the render thread; PostLateUpdate.PlayerUpdateCanvases = UI rebuilds;");
            text.AppendLine("  PreLateUpdate.DirectorUpdateAnimationBegin/End = animators; PreUpdate.AIUpdate / PreLateUpdate.AIUpdatePostScript = navigation; TimeUpdate.WaitForLastPresentationAndUpdateTime = waiting for the display or a frame cap.");

            text.AppendLine("  cameras the render pipeline drew (" + _contexts + " pipeline runs in " + _frames + " frames, " + PerfText.F2(result.PipelineMs) + " ms per frame inside the pipeline):");
            for (int i = 0; i < _cameras; i++)
            {
                text.AppendLine("    '" + _cameraNames[i] + "' drawn in " + _cameraFrames[i] + " of " + _frames + " frames   set-up before culling " + PerfText.F2(Ms(_cameraSetup[i]) / _frames)
                                + " ms   culling + building and running the frame graph " + PerfText.F2(Ms(_cameraRender[i]) / _frames) + " ms per frame");
            }
            if (_cameras == 0)
            {
                text.AppendLine("    none answered (the pipeline's camera callbacks did not fire)");
            }
            text.AppendLine("    after the last camera " + PerfText.F2(Ms(_pipelineEnd) / _frames) + " ms.  A camera that is listed under CAMERAS as active but is missing here is not drawn (it targets a display that is not in use) and costs nothing.");
        }
    }
}
