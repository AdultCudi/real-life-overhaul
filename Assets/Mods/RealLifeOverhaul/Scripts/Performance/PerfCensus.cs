using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>The probe report's census of the scene and the engine: it walks every object, so once, after the window.</summary>
    internal static class PerfCensus
    {
        private const int Most = 14;   // rows per "largest groups" list

        public static void Write(StringBuilder text, Camera camera)
        {
            Section(text, "MACHINE AND ENGINE", t => Engine(t));
            Section(text, "QUALITY SETTINGS", t => Quality(t));
            Section(text, "RENDER PIPELINE (HDRP asset in use)", t => Pipeline(t));
            Section(text, "MAIN CAMERA: frame settings and the volume stack it renders with", t => MainCamera(t, camera));
            Section(text, "VOLUMES IN THE SCENE", t => Volumes(t));
            Section(text, "CAMERAS", t => Cameras(t));
            Section(text, "LIGHTS", t => Lights(t, camera));
            Section(text, "REFLECTION PROBES", t => Probes(t));
            Section(text, "RENDERERS", t => Renderers(t, camera));
            Section(text, "THE MOD'S THINGS IN THE STREET: drawn by distance (Performance/ModCulling)", t => ModCulling.Census(t));
            Section(text, "CHARACTERS: skinned meshes and animators", t => Characters(t));
            Section(text, "GAME: traffic, people, streaming", t => PerfGame.Describe(t));
            Section(text, "PARTICLES, PHYSICS, NAVIGATION, AUDIO", t => Simulation(t));
            Section(text, "UI", t => Ui(t));
            Section(text, "SCRIPTS: components the engine calls every frame", t => Scripts(t));
            Section(text, "SCENES", t => Scenes(t));
        }

        private static void Section(StringBuilder text, string title, Action<StringBuilder> write)
        {
            text.AppendLine();
            text.Append("===== ").Append(title).AppendLine(" =====");
            try
            {
                write(text);
            }
            catch (Exception e)
            {
                text.AppendLine("  (this section failed: " + e.GetType().Name + ": " + e.Message + ")");
            }
        }

        private static void Engine(StringBuilder text)
        {
            text.AppendLine("  cpu: " + SystemInfo.processorType + "  logical cores " + SystemInfo.processorCount + "  " + SystemInfo.processorFrequency + " MHz  memory "
                            + SystemInfo.systemMemorySize + " MB");
            text.AppendLine("  gpu: " + SystemInfo.graphicsDeviceName + "  vram " + SystemInfo.graphicsMemorySize + " MB  api " + SystemInfo.graphicsDeviceType
                            + " (" + SystemInfo.graphicsDeviceVersion + ")");
            text.AppendLine("  rendering thread mode: " + SystemInfo.renderingThreadingMode + "  multi-threaded " + SystemInfo.graphicsMultiThreaded
                            + "  job workers " + JobsUtility.JobWorkerCount + " of " + JobsUtility.JobWorkerMaximumCount);
            Resolution display = Screen.currentResolution;
            text.AppendLine("  screen: " + Screen.width + "x" + Screen.height + "  mode " + Screen.fullScreenMode + "  display " + display.width + "x" + display.height
                            + " at " + display.refreshRateRatio.value.ToString("F0") + " Hz");
            text.AppendLine("  frame pacing: targetFrameRate " + Application.targetFrameRate + " (-1 = uncapped)  vSyncCount " + QualitySettings.vSyncCount
                            + "  maxQueuedFrames " + QualitySettings.maxQueuedFrames + "  render every " + OnDemandRendering.renderFrameInterval + " frame(s)"
                            + "  runInBackground " + Application.runInBackground + "  focused " + Application.isFocused);
            text.AppendLine("  time: timeScale " + PerfText.F2(Time.timeScale) + "  fixedDeltaTime " + Time.fixedDeltaTime.ToString("F4") + "  maximumDeltaTime "
                            + PerfText.F2(Time.maximumDeltaTime));
            text.AppendLine("  physics: simulationMode " + Physics.simulationMode + "  autoSyncTransforms " + Physics.autoSyncTransforms + "  reuseCollisionCallbacks "
                            + Physics.reuseCollisionCallbacks + "  solver iterations " + Physics.defaultSolverIterations + "/" + Physics.defaultSolverVelocityIterations
                            + "  2D " + Physics2D.simulationMode);
            text.AppendLine("  garbage collector: incremental " + GarbageCollector.isIncremental + "  slice " + (GarbageCollector.incrementalTimeSliceNanoseconds / 1000000.0).ToString("F1")
                            + " ms  mode " + GarbageCollector.GCMode + "  managed heap " + (UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() >> 20) + " MB, in use "
                            + (UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() >> 20) + " MB");
            text.AppendLine("  loading: backgroundLoadingPriority " + Application.backgroundLoadingPriority + "  asyncUploadTimeSlice " + QualitySettings.asyncUploadTimeSlice
                            + " ms  buffer " + QualitySettings.asyncUploadBufferSize + " MB  persistent " + QualitySettings.asyncUploadPersistentBuffer);
            text.AppendLine("  frame timing feature enabled in this build: " + FrameTimingManager.IsFeatureEnabled() + "  dynamic resolution handler: "
                            + (DynamicResolutionHandler.instance.DynamicResolutionEnabled() ? "on, scale " + PerfText.F2(DynamicResolutionHandler.instance.GetCurrentScale()) : "off"));
        }

        private static void Quality(StringBuilder text)
        {
            string[] names = QualitySettings.names;
            int level = QualitySettings.GetQualityLevel();
            text.AppendLine("  level " + level + " '" + (level < names.Length ? names[level] : "?") + "' of [" + string.Join(", ", names) + "]");
            text.AppendLine("  lodBias " + PerfText.F2(QualitySettings.lodBias) + "  maximumLODLevel " + QualitySettings.maximumLODLevel + "  skinWeights " + QualitySettings.skinWeights
                            + "  anisotropic " + QualitySettings.anisotropicFiltering + "  texture mip limit " + QualitySettings.globalTextureMipmapLimit);
            text.AppendLine("  (built-in values, mostly unused by HDRP) shadows " + QualitySettings.shadows + "  shadowDistance " + PerfText.F1(QualitySettings.shadowDistance)
                            + "  cascades " + QualitySettings.shadowCascades + "  pixelLightCount " + QualitySettings.pixelLightCount + "  antiAliasing " + QualitySettings.antiAliasing
                            + "  realtimeReflectionProbes " + QualitySettings.realtimeReflectionProbes);
            text.AppendLine("  streamingMipmaps " + QualitySettings.streamingMipmapsActive + "  budget " + PerfText.F1(QualitySettings.streamingMipmapsMemoryBudget)
                            + " MB  discardUnusedMips " + Texture.streamingTextureDiscardUnusedMips + "  particleRaycastBudget " + QualitySettings.particleRaycastBudget
                            + "  softParticles " + QualitySettings.softParticles);
        }

        private static void Pipeline(StringBuilder text)
        {
            var asset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            if (asset == null)
            {
                text.AppendLine("  not HDRP: " + (GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.GetType().Name : "built-in"));
                return;
            }
            text.AppendLine("  asset '" + asset.name + "'  SRP batcher " + GraphicsSettings.useScriptableRenderPipelineBatching
                            + "  DLSS detected " + HDDynamicResolutionPlatformCapabilities.DLSSDetected);
            PerfText.Dump(text, asset.currentPlatformRenderPipelineSettings, "  ", 3);
        }

        private static void MainCamera(StringBuilder text, Camera camera)
        {
            if (camera == null)
            {
                text.AppendLine("  no main camera");
                return;
            }
            HDCamera hd = HDCamera.GetOrCreate(camera);
            FrameSettings frame = hd.frameSettings;
            var on = new StringBuilder();
            var off = new StringBuilder();
            foreach (FrameSettingsField field in (FrameSettingsField[])Enum.GetValues(typeof(FrameSettingsField)))
            {
                if (field == FrameSettingsField.None)
                {
                    continue;
                }
                (frame.IsEnabled(field) ? on : off).Append(field).Append(' ');
            }
            text.AppendLine("  lit shader mode " + frame.litShaderMode + "  msaa " + frame.msaaMode + "  lodBias " + PerfText.F2(frame.lodBias) + " (" + frame.lodBiasMode
                            + ")  maximumLODLevel " + frame.maximumLODLevel + " (" + frame.maximumLODLevelMode + ")  material quality " + frame.materialQuality);
            text.AppendLine("  frame settings ON:  " + on);
            text.AppendLine("  frame settings OFF: " + off);

            VolumeStack stack = hd.volumeStack ?? VolumeManager.instance.stack;
            if (stack == null)
            {
                text.AppendLine("  no volume stack");
                return;
            }
            text.AppendLine("  volume stack (every value is the blended result the camera uses; 'True'/'False' after a value is whether a volume overrides it):");
            foreach (Type type in VolumeManager.instance.baseComponentTypeArray)
            {
                VolumeComponent component = stack.GetComponent(type);
                if (component == null)
                {
                    continue;
                }
                var line = new StringBuilder();
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (typeof(VolumeParameter).IsAssignableFrom(field.FieldType) && field.GetValue(component) is VolumeParameter parameter)
                    {
                        line.Append(field.Name).Append('=').Append(parameter).Append("  ");
                    }
                }
                text.AppendLine("    " + type.Name + (component.active ? string.Empty : " (inactive)") + ": " + line);
            }
        }

        private static void Volumes(StringBuilder text)
        {
            Volume[] volumes = VolumeManager.instance.GetVolumes(~0);
            text.AppendLine("  enabled volumes: " + volumes.Length);
            foreach (Volume volume in volumes)
            {
                if (volume == null)
                {
                    continue;
                }
                VolumeProfile profile = volume.sharedProfile;
                var parts = new StringBuilder();
                if (profile != null)
                {
                    foreach (VolumeComponent component in profile.components)
                    {
                        if (component != null)
                        {
                            parts.Append(component.GetType().Name).Append(component.active ? " " : "(off) ");
                        }
                    }
                }
                text.AppendLine("    '" + volume.name + "' " + (volume.isGlobal ? "global" : "local") + " priority " + PerfText.F1(volume.priority) + " weight " + PerfText.F2(volume.weight)
                                + " layer " + LayerMask.LayerToName(volume.gameObject.layer) + " scene '" + volume.gameObject.scene.name + "' profile '"
                                + (profile != null ? profile.name : "none") + "': " + parts);
            }
        }

        private static void Cameras(StringBuilder text)
        {
            Camera[] all = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int enabled = 0;
            foreach (Camera c in all)
            {
                if (c.isActiveAndEnabled)
                {
                    enabled++;
                }
            }
            text.AppendLine("  cameras loaded: " + all.Length + "  rendering now (active and enabled): " + enabled);
            foreach (Camera c in all)
            {
                bool renders = c.isActiveAndEnabled;
                RenderTexture target = c.targetTexture;
                var line = new StringBuilder("    ");
                line.Append(renders ? "RENDERS " : "idle    ").Append('\'').Append(c.name).Append("' under '").Append(PerfText.Above(c.transform, 2))
                    .Append("' scene '").Append(c.gameObject.scene.name).Append('\'');
                line.Append(" depth ").Append(PerfText.F1(c.depth)).Append(" type ").Append(c.cameraType).Append(" near ").Append(PerfText.F2(c.nearClipPlane))
                    .Append(" far ").Append(PerfText.F1(c.farClipPlane)).Append(c.orthographic ? " ortho" : " fov " + PerfText.F1(c.fieldOfView))
                    .Append(" mask 0x").Append(c.cullingMask.ToString("X")).Append(" occlusion ").Append(c.useOcclusionCulling);
                line.Append(target != null ? " -> texture '" + target.name + "' " + target.width + "x" + target.height
                    : " -> display " + (c.targetDisplay + 1) + (c.targetDisplay < Display.displays.Length && Display.displays[c.targetDisplay].active
                        ? string.Empty : " (NOT IN USE: the engine should not draw this camera; the step table lists the cameras really drawn)"));
                if (renders)
                {
                    float[] distances = c.layerCullDistances;
                    int limited = 0;
                    foreach (float d in distances)
                    {
                        if (d > 0f)
                        {
                            limited++;
                        }
                    }
                    line.Append(" layers with their own cull distance ").Append(limited);
                }
                var data = c.GetComponent<HDAdditionalCameraData>();
                if (data != null)
                {
                    line.Append(" | aa ").Append(data.antialiasing).Append(data.antialiasing == HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing ? " " + data.TAAQuality : string.Empty)
                        .Append(" custom frame settings ").Append(data.customRenderingSettings).Append(" dynamic resolution ").Append(data.allowDynamicResolution)
                        .Append(" dlss allowed ").Append(data.allowDeepLearningSuperSampling).Append(" passthrough ").Append(data.fullscreenPassthrough);
                }
                text.AppendLine(line.ToString());
            }
        }

        private sealed class LightGroup
        {
            public int Count, Shadowed, Animated, InRange;
            public float Range, Intensity, Nearest = float.MaxValue;
            public LightType Type;
            public string Sample;
        }

        private static void Lights(StringBuilder text, Camera camera)
        {
            Light[] all = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Vector3 eye = camera != null ? camera.transform.position : Vector3.zero;
            float far = camera != null ? camera.farClipPlane : 1000f;
            Plane[] planes = camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null;

            int active = 0, enabled = 0, shadowed = 0, baked = 0, directional = 0, spot = 0, point = 0, area = 0;
            int beyondFar = 0, inFrustum = 0, inFrustumShadowed = 0, beyondFade = 0, beyondShadowFade = 0, volumetric = 0, animated = 0, noData = 0;
            int shadowEveryFrame = 0, shadowOnEnable = 0, shadowOnDemand = 0;
            var byDistance = new PerfText.Ranges(25f, 50f, 100f, 200f, 400f, 800f);
            var shadowedByDistance = new PerfText.Ranges(25f, 50f, 100f, 200f, 400f, 800f);
            var byRange = new PerfText.Ranges(5f, 10f, 20f, 40f, 80f);
            var byFade = new PerfText.Ranges(50f, 100f, 200f, 500f, 1000f, 10000f);
            var byShadowFade = new PerfText.Ranges(50f, 100f, 200f, 500f, 1000f, 10000f);
            var byScene = new PerfText.Tally();
            var groups = new Dictionary<string, LightGroup>();

            foreach (Light light in all)
            {
                if (!light.gameObject.activeInHierarchy)
                {
                    continue;
                }
                active++;
                if (!light.enabled)
                {
                    continue;
                }
                enabled++;
                byScene.Add(light.gameObject.scene.name);
                if (light.bakingOutput.isBaked && light.bakingOutput.lightmapBakeType == LightmapBakeType.Baked)
                {
                    baked++;
                }
                switch (light.type)
                {
                    case LightType.Directional: directional++; break;
                    case LightType.Spot: spot++; break;
                    case LightType.Point: point++; break;
                    default: area++; break;
                }
                bool casts = light.shadows != LightShadows.None;
                if (casts)
                {
                    shadowed++;
                }
                float distance = Vector3.Distance(light.transform.position, eye);
                bool local = light.type != LightType.Directional;
                if (local)
                {
                    byDistance.Add(distance);
                    byRange.Add(light.range);
                    if (casts)
                    {
                        shadowedByDistance.Add(distance);
                    }
                    if (distance - light.range > far)
                    {
                        beyondFar++;
                    }
                    else if (planes != null && GeometryUtility.TestPlanesAABB(planes, new Bounds(light.transform.position, Vector3.one * (light.range * 2f))))
                    {
                        inFrustum++;
                        if (casts)
                        {
                            inFrustumShadowed++;
                        }
                    }
                }

                bool isAnimated = light.GetComponent<Animator>() != null;
                var data = light.GetComponent<HDAdditionalLightData>();
                if (data == null)
                {
                    noData++;
                }
                else
                {
                    if (isAnimated)
                    {
                        animated++;
                    }
                    if (local)
                    {
                        byFade.Add(data.fadeDistance);
                        if (distance > data.fadeDistance)
                        {
                            beyondFade++;
                        }
                        if (casts)
                        {
                            byShadowFade.Add(data.shadowFadeDistance);
                            if (distance > data.shadowFadeDistance)
                            {
                                beyondShadowFade++;
                            }
                        }
                    }
                    if (data.affectsVolumetric && data.volumetricDimmer > 0f)
                    {
                        volumetric++;
                    }
                    if (casts)
                    {
                        switch (data.shadowUpdateMode)
                        {
                            case ShadowUpdateMode.EveryFrame: shadowEveryFrame++; break;
                            case ShadowUpdateMode.OnEnable: shadowOnEnable++; break;
                            default: shadowOnDemand++; break;
                        }
                    }
                }

                string key = PerfText.Family(light.name) + " < " + PerfText.Above(light.transform, 2) + "  [" + light.gameObject.scene.name + "]";
                if (!groups.TryGetValue(key, out LightGroup group))
                {
                    group = new LightGroup { Type = light.type, Sample = light.name };
                    groups[key] = group;
                }
                group.Count++;
                group.Range += light.range;
                group.Intensity += light.intensity;
                group.Nearest = Mathf.Min(group.Nearest, distance);
                if (casts)
                {
                    group.Shadowed++;
                }
                if (isAnimated)
                {
                    group.Animated++;
                }
                if (distance - light.range <= far)
                {
                    group.InRange++;
                }
            }

            text.AppendLine("  Light components loaded " + all.Length + "  on active objects " + active + "  ENABLED " + enabled + "  (directional " + directional + ", spot " + spot
                            + ", point " + point + ", area " + area + ")");
            text.AppendLine("  casting shadows " + shadowed + "  baked-only (free at run time) " + baked + "  affecting volumetric fog " + volumetric + "  with an Animator " + animated
                            + "  without HDRP light data " + noData);
            text.AppendLine("  every enabled light with HDRP data costs one script LateUpdate call per frame (HDAdditionalLightData), on screen or not");
            text.AppendLine("  camera far plane " + PerfText.F1(far) + " m: lights wholly beyond it " + beyondFar + "  lights whose range touches the view frustum " + inFrustum
                            + " (shadow-casting " + inFrustumShadowed + ")" + "  beyond their own fade distance " + beyondFade + "  shadow-casting beyond their shadow fade distance "
                            + beyondShadowFade);
            text.AppendLine("  shadow update mode of shadow-casting lights: every frame " + shadowEveryFrame + "  on enable " + shadowOnEnable + "  on demand " + shadowOnDemand);
            text.AppendLine("  distance from camera (m), local lights:        " + byDistance);
            text.AppendLine("  distance from camera (m), shadow-casting ones:  " + shadowedByDistance);
            text.AppendLine("  range (m):                 " + byRange);
            text.AppendLine("  fade distance (m):         " + byFade);
            text.AppendLine("  shadow fade distance (m):  " + byShadowFade);
            text.AppendLine("  enabled lights by scene:");
            byScene.Write(text, Most, "  ");

            var rows = new List<KeyValuePair<string, LightGroup>>(groups);
            rows.Sort((a, b) => b.Value.Count.CompareTo(a.Value.Count));
            text.AppendLine("  largest groups of enabled lights (name < parents [scene]):");
            for (int i = 0; i < rows.Count && i < 30; i++)
            {
                LightGroup g = rows[i].Value;
                text.AppendLine("  " + g.Count.ToString().PadLeft(6) + "  " + rows[i].Key + "  " + g.Type + " range " + PerfText.F1(g.Range / g.Count) + " intensity "
                                + PerfText.F1(g.Intensity / g.Count) + " shadows " + g.Shadowed + " animated " + g.Animated + " within far plane " + g.InRange + " nearest "
                                + PerfText.F1(g.Nearest) + " m");
            }
            if (rows.Count > 30)
            {
                text.AppendLine("     ...  and " + (rows.Count - 30) + " smaller groups");
            }
        }

        private static void Probes(StringBuilder text)
        {
            HDProbe[] probes = UnityEngine.Object.FindObjectsByType<HDProbe>(FindObjectsSortMode.None);
            int realtime = 0, everyFrame = 0, planar = 0;
            foreach (HDProbe probe in probes)
            {
                bool isPlanar = probe is PlanarReflectionProbe;
                if (isPlanar)
                {
                    planar++;
                }
                if (probe.mode == ProbeSettings.Mode.Realtime)
                {
                    realtime++;
                    if (probe.realtimeMode == ProbeSettings.RealtimeMode.EveryFrame)
                    {
                        everyFrame++;
                    }
                    text.AppendLine("    realtime " + (isPlanar ? "PLANAR " : "probe ") + "'" + probe.name + "' under '" + PerfText.Above(probe.transform, 2) + "' update "
                                    + probe.realtimeMode + " time-sliced " + probe.timeSlicing + " scene '" + probe.gameObject.scene.name + "'");
                }
            }
            text.AppendLine("  enabled probes " + probes.Length + "  planar " + planar + "  realtime " + realtime + "  rendered every frame " + everyFrame
                            + " (each of those is a whole extra camera render)");
        }

        private static void Renderers(StringBuilder text, Camera camera)
        {
            Renderer[] all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            int enabled = 0, visible = 0, meshes = 0, skinned = 0, particles = 0, other = 0, casting = 0, visibleCasting = 0, staticBatched = 0, motionVectors = 0;
            var visibleFamilies = new PerfText.Tally();
            var visibleLayers = new PerfText.Tally();
            var enabledScenes = new PerfText.Tally();
            var visibleScenes = new PerfText.Tally();
            var materials = new HashSet<Material>();
            var shared = new List<Material>(8);
            var shaders = new PerfText.Tally();
            int instanced = 0, notInstanced = 0, materialSlots = 0;
            Vector3 eye = camera != null ? camera.transform.position : Vector3.zero;
            var visibleByDistance = new PerfText.Ranges(25f, 50f, 100f, 200f, 400f, 800f);
            var byLayerAndDistance = new Dictionary<string, PerfText.Ranges>();

            foreach (Renderer renderer in all)
            {
                if (!renderer.enabled)
                {
                    continue;
                }
                enabled++;
                enabledScenes.Add(renderer.gameObject.scene.name);
                if (renderer is MeshRenderer)
                {
                    meshes++;
                }
                else if (renderer is SkinnedMeshRenderer)
                {
                    skinned++;
                }
                else if (renderer is ParticleSystemRenderer)
                {
                    particles++;
                }
                else
                {
                    other++;
                }
                bool casts = renderer.shadowCastingMode != ShadowCastingMode.Off;
                if (casts)
                {
                    casting++;
                }
                if (renderer.isPartOfStaticBatch)
                {
                    staticBatched++;
                }
                if (renderer.motionVectorGenerationMode == MotionVectorGenerationMode.Object)
                {
                    motionVectors++;
                }
                if (!renderer.isVisible)
                {
                    continue;
                }
                visible++;
                if (casts)
                {
                    visibleCasting++;
                }
                float away = Vector3.Distance(renderer.bounds.center, eye);
                visibleByDistance.Add(away);
                string layerName = LayerMask.LayerToName(renderer.gameObject.layer);
                if (!byLayerAndDistance.TryGetValue(layerName, out PerfText.Ranges bands))
                {
                    bands = new PerfText.Ranges(50f, 100f, 150f, 200f, 250f, 300f);
                    byLayerAndDistance.Add(layerName, bands);
                }
                bands.Add(away);
                visibleFamilies.Add(PerfText.Family(renderer.name) + " (" + renderer.GetType().Name + ", " + LayerMask.LayerToName(renderer.gameObject.layer) + ")");
                visibleLayers.Add(LayerMask.LayerToName(renderer.gameObject.layer));
                visibleScenes.Add(renderer.gameObject.scene.name);
                renderer.GetSharedMaterials(shared);
                materialSlots += shared.Count;
                foreach (Material material in shared)
                {
                    if (material != null && materials.Add(material))
                    {
                        shaders.Add(material.shader != null ? material.shader.name : "(no shader)");
                        if (material.enableInstancing)
                        {
                            instanced++;
                        }
                        else
                        {
                            notInstanced++;
                        }
                    }
                }
            }

            text.AppendLine("  enabled renderers " + enabled + " (mesh " + meshes + ", skinned " + skinned + ", particle " + particles + ", other " + other + ")  casting shadows "
                            + casting + "  in a static batch " + staticBatched + "  per-object motion vectors " + motionVectors + "  LOD groups "
                            + UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None).Length);
            text.AppendLine("  VISIBLE to some camera or shadow pass this frame: " + visible + " (shadow-casting " + visibleCasting + ")  material slots on them " + materialSlots
                            + "  distinct materials " + materials.Count + " (GPU instancing on " + instanced + ", off " + notInstanced + ")");
            text.AppendLine("  visible renderers by distance from camera (m): " + visibleByDistance);
            text.AppendLine("  occlusion culling data baked for the scenes: see 'occlusion' on the camera lines above (the camera flag alone does nothing without baked data)");
            text.AppendLine("  enabled renderers by scene (every one is tested against the view each frame, wherever the player is):");
            enabledScenes.Write(text, Most, "  ");
            text.AppendLine("  visible renderers by scene:");
            visibleScenes.Write(text, Most, "  ");
            text.AppendLine("  visible renderers by layer:");
            visibleLayers.Write(text, Most, "  ");
            text.AppendLine("  visible renderers by layer and distance from the camera (m): what a per-layer draw distance would remove");
            foreach (KeyValuePair<string, PerfText.Ranges> row in byLayerAndDistance)
            {
                text.AppendLine("    " + row.Key.PadRight(26) + row.Value);
            }
            CustomPassVolume[] passVolumes = UnityEngine.Object.FindObjectsByType<CustomPassVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            text.AppendLine("  custom pass volumes (what the game injects into the render pipeline): " + passVolumes.Length);
            foreach (CustomPassVolume volume in passVolumes)
            {
                var passes = new StringBuilder();
                foreach (CustomPass pass in volume.customPasses)
                {
                    if (pass != null)
                    {
                        passes.Append(pass.GetType().Name).Append(" '").Append(pass.name).Append(pass.enabled ? "' on; " : "' off; ");
                    }
                }
                text.AppendLine("    '" + volume.name + "' " + (volume.isActiveAndEnabled ? "ACTIVE" : "inactive") + (volume.isGlobal ? " global" : " local") + " at "
                                + volume.injectionPoint + ": " + passes);
            }
            text.AppendLine("  largest groups of visible renderers:");
            visibleFamilies.Write(text, 30, "  ");
            text.AppendLine("  shaders of the visible materials:");
            shaders.Write(text, Most, "  ");
        }

        private static void Characters(StringBuilder text)
        {
            SkinnedMeshRenderer[] skins = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
            int enabled = 0, visible = 0, offscreenUpdating = 0, bones = 0, maxBones = 0, blendShapes = 0, motion = 0, vertices = 0;
            var quality = new PerfText.Tally();
            foreach (SkinnedMeshRenderer skin in skins)
            {
                if (!skin.enabled)
                {
                    continue;
                }
                enabled++;
                if (skin.isVisible)
                {
                    visible++;
                }
                if (skin.updateWhenOffscreen)
                {
                    offscreenUpdating++;
                }
                int count = skin.bones.Length;
                bones += count;
                maxBones = Mathf.Max(maxBones, count);
                quality.Add(skin.quality.ToString());
                if (skin.skinnedMotionVectors)
                {
                    motion++;
                }
                Mesh mesh = skin.sharedMesh;
                if (mesh != null)
                {
                    vertices += mesh.vertexCount;
                    if (mesh.blendShapeCount > 0)
                    {
                        blendShapes++;
                    }
                }
            }
            text.AppendLine("  skinned meshes enabled " + enabled + "  visible " + visible + "  updateWhenOffscreen " + offscreenUpdating + "  skinned motion vectors " + motion
                            + "  with blend shapes " + blendShapes);
            text.AppendLine("  bones in total " + bones + " (largest " + maxBones + ")  vertices skinned if all were visible " + vertices + "  global skin weights " + QualitySettings.skinWeights);
            text.AppendLine("  per-renderer quality:");
            quality.Write(text, 6, "  ");

            Animator[] animators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
            var modes = new PerfText.Tally();
            var wasted = new PerfText.Tally();
            var controllers = new PerfText.Tally();
            var children = new List<Renderer>(32);
            int running = 0, unseen = 0, unseenAlways = 0, noRenderer = 0, rootMotion = 0, physicsMode = 0;
            foreach (Animator animator in animators)
            {
                if (!animator.enabled)
                {
                    continue;
                }
                running++;
                modes.Add(animator.cullingMode.ToString());
                RuntimeAnimatorController controller = animator.runtimeAnimatorController;
                controllers.Add(controller != null ? controller.name : "(no controller)");
                if (animator.applyRootMotion)
                {
                    rootMotion++;
                }
                if (animator.updateMode == AnimatorUpdateMode.AnimatePhysics)
                {
                    physicsMode++;
                }
                animator.GetComponentsInChildren(false, children);
                bool seen = false, any = false;
                foreach (Renderer renderer in children)
                {
                    if (renderer.enabled)
                    {
                        any = true;
                        if (renderer.isVisible)
                        {
                            seen = true;
                            break;
                        }
                    }
                }
                if (!any)
                {
                    noRenderer++;
                }
                else if (!seen)
                {
                    unseen++;
                    if (animator.cullingMode == AnimatorCullingMode.AlwaysAnimate)
                    {
                        unseenAlways++;
                        wasted.Add(PerfText.Family(animator.name) + " < " + PerfText.Above(animator.transform, 1) + " (" + (controller != null ? controller.name : "none") + ")");
                    }
                }
            }
            text.AppendLine("  animators running " + running + "  with nothing of theirs on screen " + unseen + "  of which set to AlwaysAnimate (posing every bone for nobody) " + unseenAlways
                            + "  with no enabled renderer at all " + noRenderer + "  root motion " + rootMotion + "  physics update mode " + physicsMode);
            text.AppendLine("  culling modes:");
            modes.Write(text, 4, "  ");
            text.AppendLine("  largest groups animating off screen with AlwaysAnimate:");
            wasted.Write(text, Most, "  ");
            text.AppendLine("  controllers:");
            controllers.Write(text, Most, "  ");
        }

        private static void Simulation(StringBuilder text)
        {
            ParticleSystem[] systems = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            int playing = 0, particles = 0;
            var families = new PerfText.Tally();
            foreach (ParticleSystem system in systems)
            {
                if (system.isPlaying)
                {
                    playing++;
                    particles += system.particleCount;
                    families.Add(PerfText.Family(system.name) + " < " + PerfText.Above(system.transform, 1), system.particleCount);
                }
            }
            text.AppendLine("  particle systems active " + systems.Length + "  playing " + playing + "  live particles " + particles);
            families.Write(text, 10, "  ", "particles");

            Rigidbody[] bodies = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            int dynamic = 0, awake = 0;
            var awakeFamilies = new PerfText.Tally();
            foreach (Rigidbody body in bodies)
            {
                if (body.isKinematic)
                {
                    continue;
                }
                dynamic++;
                if (!body.IsSleeping())
                {
                    awake++;
                    awakeFamilies.Add(PerfText.Family(body.name));
                }
            }
            text.AppendLine("  rigidbodies " + bodies.Length + "  non-kinematic " + dynamic + "  awake " + awake + "  colliders "
                            + UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length + "  wheel colliders "
                            + UnityEngine.Object.FindObjectsByType<WheelCollider>(FindObjectsSortMode.None).Length);
            awakeFamilies.Write(text, 8, "  ");

            NavMeshAgent[] agents = UnityEngine.Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            int walking = 0;
            foreach (NavMeshAgent agent in agents)
            {
                if (agent.enabled && agent.isOnNavMesh && agent.hasPath)
                {
                    walking++;
                }
            }
            text.AppendLine("  navigation agents " + agents.Length + "  with a path " + walking + "  carving obstacles " + CountCarving());
            var avoidance = new PerfText.Tally();
            foreach (NavMeshAgent agent in agents)
            {
                if (agent.enabled)
                {
                    avoidance.Add(agent.obstacleAvoidanceType + (agent.autoRepath ? ", auto-repath" : string.Empty) + "  (" + PerfText.Family(agent.name) + ")");
                }
            }
            text.AppendLine("  agents by avoidance quality (the thorough kinds are what the navigation step spends its time on when agents are close together):");
            avoidance.Write(text, 8, "  ");
            // obstacles that cut the navigation mesh: one that moves re-cuts the mesh round it each time it does
            var obstacles = new PerfText.Tally();
            int onMoving = 0;
            foreach (NavMeshObstacle obstacle in UnityEngine.Object.FindObjectsByType<NavMeshObstacle>(FindObjectsSortMode.None))
            {
                if (!obstacle.enabled || !obstacle.carving)
                {
                    continue;
                }
                Rigidbody carrier = obstacle.GetComponentInParent<Rigidbody>();
                bool moving = carrier != null && !carrier.isKinematic && !carrier.IsSleeping() && carrier.velocity.sqrMagnitude > 0.01f;
                if (moving)
                {
                    onMoving++;
                }
                obstacles.Add((moving ? "MOVING NOW  " : carrier != null && !carrier.isKinematic ? "on a body at rest  " : "fixed  ")
                              + (obstacle.carveOnlyStationary ? "carves only when stationary" : "CARVES WHILE MOVING") + ", move threshold "
                              + PerfText.F2(obstacle.carvingMoveThreshold) + " m, time to stationary " + PerfText.F2(obstacle.carvingTimeToStationary) + " s  ("
                              + PerfText.Family(obstacle.transform.root.name) + " / " + PerfText.Family(obstacle.name) + ")");
            }
            text.AppendLine("  carving obstacles on something moving right now: " + onMoving + "   largest groups:");
            obstacles.Write(text, 12, "  ");
            text.AppendLine("  texture streaming: active " + QualitySettings.streamingMipmapsActive + "  renderers re-checked per frame " + QualitySettings.streamingMipmapsRenderersPerFrame
                            + "  add all cameras " + QualitySettings.streamingMipmapsAddAllCameras + "  max level reduction " + QualitySettings.streamingMipmapsMaxLevelReduction
                            + "   navigation: pathfinding iterations per frame " + NavMesh.pathfindingIterationsPerFrame + "  avoidance prediction "
                            + PerfText.F2(NavMesh.avoidancePredictionTime) + " s");

            AudioSource[] sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            int sounding = 0;
            foreach (AudioSource source in sources)
            {
                if (source.isPlaying)
                {
                    sounding++;
                }
            }
            text.AppendLine("  audio sources " + sources.Length + "  playing " + sounding);
        }

        private static int CountCarving()
        {
            int carving = 0;
            foreach (NavMeshObstacle obstacle in UnityEngine.Object.FindObjectsByType<NavMeshObstacle>(FindObjectsSortMode.None))
            {
                if (obstacle.enabled && obstacle.carving)
                {
                    carving++;
                }
            }
            return carving;
        }

        private static void Ui(StringBuilder text)
        {
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var graphics = new List<Graphic>(256);
            int roots = 0, nested = 0, pixelPerfect = 0, world = 0, totalGraphics = 0, totalTargets = 0;
            var rows = new List<KeyValuePair<int, string>>();
            foreach (Canvas canvas in canvases)
            {
                if (!canvas.enabled)
                {
                    continue;
                }
                if (canvas.isRootCanvas)
                {
                    roots++;
                }
                else
                {
                    nested++;
                }
                if (canvas.pixelPerfect)
                {
                    pixelPerfect++;
                }
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    world++;
                }
                if (!canvas.isRootCanvas)
                {
                    continue;
                }
                canvas.GetComponentsInChildren(false, graphics);
                int targets = 0, texts = 0;
                foreach (Graphic graphic in graphics)
                {
                    if (graphic.raycastTarget)
                    {
                        targets++;
                    }
                    if (graphic is TMP_Text)
                    {
                        texts++;
                    }
                }
                totalGraphics += graphics.Count;
                totalTargets += targets;
                rows.Add(new KeyValuePair<int, string>(graphics.Count, "'" + canvas.name + "' " + canvas.renderMode + (canvas.pixelPerfect ? " PIXEL-PERFECT" : string.Empty)
                                                                        + " order " + canvas.sortingOrder + " nested canvases " + (canvas.GetComponentsInChildren<Canvas>(false).Length - 1)
                                                                        + " graphics " + graphics.Count + " (texts " + texts + ", raycast targets " + targets + ") scene '"
                                                                        + canvas.gameObject.scene.name + "'"));
            }
            text.AppendLine("  enabled canvases: root " + roots + ", nested " + nested + "  world-space " + world + "  pixel-perfect " + pixelPerfect + "  active graphics "
                            + totalGraphics + "  raycast targets " + totalTargets + "  graphic raycasters "
                            + UnityEngine.Object.FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None).Length + "  layout groups "
                            + UnityEngine.Object.FindObjectsByType<LayoutGroup>(FindObjectsSortMode.None).Length + "  content size fitters "
                            + UnityEngine.Object.FindObjectsByType<ContentSizeFitter>(FindObjectsSortMode.None).Length);
            rows.Sort((a, b) => b.Key.CompareTo(a.Key));
            for (int i = 0; i < rows.Count && i < 25; i++)
            {
                text.AppendLine("    " + rows[i].Value);
            }
            if (rows.Count > 25)
            {
                text.AppendLine("    ... and " + (rows.Count - 25) + " smaller root canvases");
            }
        }

        // which per-frame messages a script type receives, looked up once a type
        private static readonly Dictionary<Type, int> Messages = new Dictionary<Type, int>();
        private const int HasUpdate = 1, HasLate = 2, HasFixed = 4, HasGui = 8, HasRender = 16;

        // messages the engine sends because of a renderer or a camera: per visible object, per camera, per frame
        private static readonly string[] RenderMessages = { "OnWillRenderObject", "OnBecameVisible", "OnBecameInvisible", "OnRenderObject", "OnPreCull", "OnPreRender", "OnPostRender" };

        private static int MessagesOf(Type type)
        {
            if (Messages.TryGetValue(type, out int flags))
            {
                return flags;
            }
            const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (Type t = type; t != null && t != typeof(MonoBehaviour) && t != typeof(Behaviour); t = t.BaseType)
            {
                if (t.GetMethod("Update", Declared, null, Type.EmptyTypes, null) != null)
                {
                    flags |= HasUpdate;
                }
                if (t.GetMethod("LateUpdate", Declared, null, Type.EmptyTypes, null) != null)
                {
                    flags |= HasLate;
                }
                if (t.GetMethod("FixedUpdate", Declared, null, Type.EmptyTypes, null) != null)
                {
                    flags |= HasFixed;
                }
                if (t.GetMethod("OnGUI", Declared, null, Type.EmptyTypes, null) != null)
                {
                    flags |= HasGui;
                }
                foreach (string message in RenderMessages)
                {
                    if (t.GetMethod(message, Declared, null, Type.EmptyTypes, null) != null)
                    {
                        flags |= HasRender;
                    }
                }
            }
            Messages[type] = flags;
            return flags;
        }

        private static void Scripts(StringBuilder text)
        {
            MonoBehaviour[] all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            var counts = new Dictionary<Type, int>();
            var quiet = new Dictionary<Type, int>();
            int enabled = 0, updates = 0, lates = 0, fixeds = 0, guis = 0, renders = 0;
            foreach (MonoBehaviour behaviour in all)
            {
                if (behaviour == null || !behaviour.enabled)
                {
                    continue;
                }
                enabled++;
                Type type = behaviour.GetType();
                int flags = MessagesOf(type);
                if (flags == 0)
                {
                    quiet.TryGetValue(type, out int silent);
                    quiet[type] = silent + 1;
                    continue;
                }
                counts.TryGetValue(type, out int count);
                counts[type] = count + 1;
                renders += (flags & HasRender) != 0 ? 1 : 0;
                updates += (flags & HasUpdate) != 0 ? 1 : 0;
                lates += (flags & HasLate) != 0 ? 1 : 0;
                fixeds += (flags & HasFixed) != 0 ? 1 : 0;
                guis += (flags & HasGui) != 0 ? 1 : 0;
            }
            text.AppendLine("  enabled script components " + enabled + "  calls per frame: Update " + updates + "  LateUpdate " + lates + "  per physics step: FixedUpdate " + fixeds
                            + "  OnGUI components " + guis);
            text.AppendLine("  components with a rendering message (OnWillRenderObject, OnBecameVisible...: called per visible object or per camera) " + renders);
            text.AppendLine("  (each call crosses from the engine into script code whether or not the method then does anything;"
                            + " U = Update, L = LateUpdate, F = FixedUpdate, G = OnGUI, R = a rendering message)");
            var rows = new List<KeyValuePair<Type, int>>(counts);
            rows.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < rows.Count && i < 60; i++)
            {
                int flags = MessagesOf(rows[i].Key);
                string marks = ((flags & HasUpdate) != 0 ? "U" : "-") + ((flags & HasLate) != 0 ? "L" : "-") + ((flags & HasFixed) != 0 ? "F" : "-")
                               + ((flags & HasGui) != 0 ? "G" : "-") + ((flags & HasRender) != 0 ? "R" : "-");
                text.AppendLine("  " + rows[i].Value.ToString().PadLeft(6) + "  " + marks + "  " + rows[i].Key.FullName + "  [" + rows[i].Key.Assembly.GetName().Name + "]");
            }
            if (rows.Count > 60)
            {
                text.AppendLine("     ...  and " + (rows.Count - 60) + " more types");
            }
            var silentRows = new List<KeyValuePair<Type, int>>(quiet);
            silentRows.Sort((a, b) => b.Value.CompareTo(a.Value));
            text.AppendLine("  the rest have none of those methods: the engine never calls them per frame (they cost memory and loading time, not frame time). Largest groups:");
            for (int i = 0; i < silentRows.Count && i < 15; i++)
            {
                text.AppendLine("  " + silentRows[i].Value.ToString().PadLeft(6) + "        " + silentRows[i].Key.FullName + "  [" + silentRows[i].Key.Assembly.GetName().Name + "]");
            }
        }

        private static void Scenes(StringBuilder text)
        {
            text.AppendLine("  loaded scenes: " + SceneManager.sceneCount + "  active '" + SceneManager.GetActiveScene().name + "'");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                text.AppendLine("    '" + scene.name + "' loaded " + scene.isLoaded + " root objects " + (scene.isLoaded ? scene.rootCount : 0));
            }
        }
    }
}
