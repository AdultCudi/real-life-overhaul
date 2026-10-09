using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace CharacterPreview
{
    /// <summary>
    /// Renders the game's detailed character under HDRP, with or without the mod's looks, to PNG files.
    ///
    ///     Unity -batchmode -projectPath PreviewProject -executeMethod CharacterPreview.PreviewRunner.Run
    ///           -reference CharacterAssets\_reference\preview -plan plan.json -out folder
    ///
    /// No -quit and no -nographics: HDRP needs the GPU, and the sky and temporal anti-aliasing need a few
    /// frames to settle, so the shots are taken from the editor's update loop and the editor exits by itself.
    /// The plan is a list of shots (see preview_plan.py); each names the look, the colours, the stage and the view.
    /// </summary>
    public static class PreviewRunner
    {
        private const int WarmFrames = 14;        // sky convolution + temporal anti-aliasing history
        private const float HeadHeight = 1.58f;   // of the reference character, metres above its root

        private static JArray _shots;
        private static string _out;
        private static int _width, _height;
        private static GameCharacter _character;
        private static Stage _stage;
        private static Camera _camera;
        private static RenderTexture _target;
        private static int _index = -1, _frame;
        private static JToken _shot;

        public static void Run()
        {
            try
            {
                string reference = Argument("-reference"), plan = Argument("-plan");
                _out = Argument("-out");
                Directory.CreateDirectory(_out);
                JObject root = JObject.Parse(File.ReadAllText(plan));
                _shots = (JArray)root["shots"];
                _width = (int?)root["width"] ?? 1920;
                _height = (int?)root["height"] ?? 1080;

                Pipeline.Ensure();
                _character = new GameCharacter(reference);
                PreviewLooks.Folder = Argument("-looks", false);
                _stage = new Stage();
                _camera = _stage.Camera;
                _target = new RenderTexture(_width, _height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                _camera.targetTexture = _target;
                EditorApplication.update += Tick;
            }
            catch (Exception e)
            {
                Debug.LogError("[CharacterPreview] FAILED " + e);
                EditorApplication.Exit(1);
            }
        }

        private static void Tick()
        {
            try
            {
                if (_index < 0 || _frame >= WarmFrames)
                {
                    if (_index >= 0)
                    {
                        Save();
                    }
                    _index++;
                    if (_index >= _shots.Count)
                    {
                        Debug.Log("[CharacterPreview] done, " + _shots.Count + " shots in " + _out);
                        EditorApplication.update -= Tick;
                        EditorApplication.Exit(0);
                        return;
                    }
                    _shot = _shots[_index];
                    Arrange(_shot);
                    _frame = 0;
                }
                _camera.Render();
                _frame++;
            }
            catch (Exception e)
            {
                Debug.LogError("[CharacterPreview] FAILED on shot " + _index + ": " + e);
                EditorApplication.update -= Tick;
                EditorApplication.Exit(1);
            }
        }

        private static void Arrange(JToken shot)
        {
            var parts = new List<string>();
            foreach (JToken part in (JArray)shot["parts"])
            {
                parts.Add((string)part);
            }
            PreviewLooks.Restore();
            PreviewLooks.Undo();
            _character.Wear((string)shot["sex"], parts);
            foreach (SkinnedMeshRenderer renderer in _character.Shown)
            {
                // The body shapes of the shot before are not this shot's.
                for (int i = 0; i < 3 && i < renderer.sharedMesh.blendShapeCount; i++)
                {
                    renderer.SetBlendShapeWeight(i, 0f);
                }
            }
            foreach (JProperty shape in ((JObject)shot["shapes"] ?? new JObject()).Properties())
            {
                foreach (SkinnedMeshRenderer renderer in _character.Shown)
                {
                    int index = renderer.sharedMesh.GetBlendShapeIndex(shape.Name);
                    if (index >= 0)
                    {
                        renderer.SetBlendShapeWeight(index, (float)shape.Value);
                    }
                }
            }
            _character.Pose(null);
            _character.LowerArms((float?)shot["arms"] ?? 68f);
            _character.Pose((JObject)shot["pose"]);
            foreach (JProperty cloth in ((JObject)shot["cloth"] ?? new JObject()).Properties())
            {
                _character.SetClothColour(cloth.Name, Hex((string)cloth.Value));
            }
            _character.SetHairColour(Hex((string)shot["hair"]));
            _character.SetSkinColour(Hex((string)shot["skin"]));
            PreviewLooks.Apply(_character, shot, Hex((string)shot["hair"]), Hex((string)shot["skin"]));

            string stage = (string)shot["stage"] ?? "city";
            _stage.Set(stage, (JObject)shot["stageParams"]);
            // The camera stays where the game has it; the character turns (as it does in both screens).
            float yaw = (float?)shot["yaw"] ?? 0f;
            _character.Root.transform.SetPositionAndRotation(_stage.CharacterPosition, _stage.CharacterFacing * Quaternion.Euler(0f, yaw, 0f));
            _stage.Aim((string)shot["view"] ?? "game", HeadHeight);
        }

        private static void Save()
        {
            // Crop, in true game pixels, around the point the view is about.
            JToken crop = _shot["crop"];
            int w = Mathf.Min(_width, (int?)crop?[0] ?? _width), h = Mathf.Min(_height, (int?)crop?[1] ?? _height);
            Vector3 centre = _camera.WorldToViewportPoint(_stage.Focus);
            int x = Mathf.Clamp(Mathf.RoundToInt(centre.x * _width - w * 0.5f), 0, _width - w);
            int y = Mathf.Clamp(Mathf.RoundToInt(centre.y * _height - h * 0.5f), 0, _height - h);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _target;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(x, y, w, h), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(_out, (string)_shot["name"] + ".png"), image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            Debug.Log("[CharacterPreview] shot " + (string)_shot["name"]);
        }

        internal static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out Color colour) ? colour : Color.magenta;
        }

        private static string Argument(string name, bool required = true)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (arguments[i] == name)
                {
                    return arguments[i + 1];
                }
            }
            if (required)
            {
                throw new ArgumentException("missing " + name);
            }
            return null;
        }
    }

    /// <summary>An HDRP asset with stock settings, made on first use (the folder is generated, not committed).</summary>
    internal static class Pipeline
    {
        private const string AssetPath = "Assets/Generated/PreviewHDRP.asset";

        public static void Ensure()
        {
            var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(AssetPath);
            if (asset == null)
            {
                Directory.CreateDirectory("Assets/Generated");
                asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
                AssetDatabase.SaveAssets();
            }
            if (GraphicsSettings.renderPipelineAsset != asset)
            {
                GraphicsSettings.renderPipelineAsset = asset;
                QualitySettings.renderPipeline = null;
            }
            QualitySettings.skinWeights = SkinWeights.Unlimited;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                throw new InvalidOperationException("the preview project must be in linear colour space");
            }
        }
    }

    /// <summary>
    /// Where the character stands and how it is lit.
    ///  - "city": the game's sun (13 500 lux, white: from the F9 report) under a physically based sky, on asphalt,
    ///    seen by the gameplay camera (field of view 63, 3.1 m away, from above).
    ///  - "creation": the character creation room (level2 of the game): its spot light and directional light at their
    ///    real positions, pale walls, the close-up camera (field of view 25, 1.15 m from the head).
    /// The exposure and tone mapping of the game are not readable from outside; they are calibrated against the
    /// player's screenshots (see README, "Calibration").
    /// </summary>
    internal sealed class Stage
    {
        public readonly Camera Camera;
        public Vector3 CharacterPosition;
        public Quaternion CharacterFacing = Quaternion.identity;
        public Vector3 Focus;

        private readonly GameObject _city, _creation;
        private readonly Light _sun, _roomSun, _roomSpot, _fill, _rim;
        private readonly Volume _volume;
        private readonly Exposure _exposure;
        private readonly VisualEnvironment _environment;
        private readonly GradientSky _room;
        private readonly Tonemapping _tonemapping;
        private readonly ColorAdjustments _adjustments;
        private readonly HDAdditionalCameraData _cameraData;
        private string _name;

        public Stage()
        {
            var cameraObject = new GameObject("Preview Camera");
            Camera = cameraObject.AddComponent<Camera>();
            Camera.enabled = false;   // rendered by hand, one frame per editor tick
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 500f;
            _cameraData = cameraObject.AddComponent<HDAdditionalCameraData>();
            _cameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
            _cameraData.TAAQuality = HDAdditionalCameraData.TAAQualityLevel.High;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _environment = profile.Add<VisualEnvironment>(true);
            _exposure = profile.Add<Exposure>(true);
            _exposure.mode.Override(ExposureMode.Fixed);
            _tonemapping = profile.Add<Tonemapping>(true);
            _adjustments = profile.Add<ColorAdjustments>(true);
            var sky = profile.Add<PhysicallyBasedSky>(true);
            sky.groundTint.Override(new Color(0.25f, 0.25f, 0.25f));
            _room = profile.Add<GradientSky>(true);
            var fog = profile.Add<Fog>(true);
            fog.enabled.Override(false);
            _volume = new GameObject("Preview Volume").AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            _volume.sharedProfile = profile;

            // ---- the city
            _city = new GameObject("City");
            _sun = MakeLight(_city, "SunLight", HDLightTypeAndShape.Directional, 13500f, LightUnit.Lux, Color.white, true);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Asphalt";
            ground.transform.SetParent(_city.transform, false);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            var asphalt = new Material(Shader.Find("HDRP/Lit"));
            asphalt.SetColor("_BaseColor", new Color(0.13f, 0.13f, 0.14f));
            asphalt.SetFloat("_Smoothness", 0.25f);
            ground.GetComponent<Renderer>().sharedMaterial = asphalt;

            // ---- character creation (level2: 'Lights' and 'PlayerDisplay/HumanDefinitionHigh')
            _creation = new GameObject("Creation");
            _roomSpot = MakeLight(_creation, "Point Light (1)", HDLightTypeAndShape.ConeSpot, 30000f, LightUnit.Candela, Color.white, true);
            _roomSpot.transform.SetPositionAndRotation(new Vector3(0.96f, 3.09f, -0.97f), Quaternion.LookRotation(Vector3.down, Vector3.forward));
            _roomSpot.spotAngle = 144f;
            _roomSpot.range = 5f;
            _roomSun = MakeLight(_creation, "Directional Light", HDLightTypeAndShape.Directional, 13500f, LightUnit.Lux, Color.white, true);
            _roomSun.transform.rotation = Quaternion.LookRotation(new Vector3(-0.18f, -0.31f, 0.93f));
            // The mod's own fill and rim (Character/CharacterLighting), as the F9 reports list them.
            _rim = MakeLight(_creation, "Rim Light", HDLightTypeAndShape.ConeSpot, 27000f, LightUnit.Candela, Hex("FFF7F0"), false);
            _rim.transform.position = new Vector3(1.81f, 2.14f, 2.04f);
            _fill = MakeLight(_creation, "Fill Light", HDLightTypeAndShape.ConeSpot, 13500f, LightUnit.Candela, Hex("F0F5FF"), false);
            _fill.transform.position = new Vector3(0.07f, 1.44f, -0.80f);
            foreach (Light added in new[] { _rim, _fill })
            {
                added.spotAngle = 144f;
                added.range = 6f;
            }
            Backdrop(new Vector3(2f, -0.06f, 0f), new Vector3(8f, 0.1f, 8f));
        }

        public void Set(string name, JObject parameters)
        {
            _name = name;
            bool city = name == "city";
            _city.SetActive(city);
            _creation.SetActive(!city);
            float exposure, sunPitch = 50f, sunYaw = 140f;
            if (city)
            {
                CharacterPosition = Vector3.zero;
                CharacterFacing = Quaternion.identity;
                _environment.skyType.Override((int)SkyType.Gradient);
                _room.top.Override(new Color(0.55f, 0.62f, 0.72f));
                _room.middle.Override(new Color(0.66f, 0.68f, 0.7f));
                _room.bottom.Override(new Color(0.3f, 0.3f, 0.3f));
                exposure = 10.3f;
            }
            else
            {
                CharacterPosition = new Vector3(2.36f, -0.01f, -0.25f);
                CharacterFacing = Quaternion.LookRotation(new Vector3(-0.67f, 0f, -0.74f));
                _environment.skyType.Override((int)SkyType.Gradient);
                _room.top.Override(new Color(0.8f, 0.78f, 0.76f));
                _room.middle.Override(new Color(0.74f, 0.72f, 0.7f));
                _room.bottom.Override(new Color(0.6f, 0.58f, 0.56f));
                exposure = 10.3f;
            }
            float skyLux = city ? 2500f : 1200f, contrast = city ? 10f : 0f, saturation = city ? 12f : 0f;
            int tonemap = (int)TonemappingMode.ACES;
            bool modLights = true;
            if (parameters != null)
            {
                exposure = (float?)parameters["exposure"] ?? exposure;
                sunPitch = (float?)parameters["sunPitch"] ?? sunPitch;
                sunYaw = (float?)parameters["sunYaw"] ?? sunYaw;
                skyLux = (float?)parameters["skyLux"] ?? skyLux;
                contrast = (float?)parameters["contrast"] ?? contrast;
                saturation = (float?)parameters["saturation"] ?? saturation;
                tonemap = (int?)parameters["tonemap"] ?? tonemap;
                modLights = (bool?)parameters["modLights"] ?? modLights;
                _sun.GetComponent<HDAdditionalLightData>().SetIntensity((float?)parameters["sunLux"] ?? 13500f, LightUnit.Lux);
            }
            _sun.transform.rotation = Quaternion.Euler(sunPitch, sunYaw, 0f);
            bool shadows = (bool?)parameters?["shadows"] ?? true;
            foreach (Light light in new[] { _sun, _roomSun, _roomSpot })
            {
                light.GetComponent<HDAdditionalLightData>().EnableShadows(shadows);
            }
            _exposure.fixedExposure.Override(exposure);
            _tonemapping.mode.Override((TonemappingMode)tonemap);
            _adjustments.contrast.Override(contrast);
            _adjustments.saturation.Override(saturation);
            _environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            _room.skyIntensityMode.Override(SkyIntensityMode.Lux);
            _room.desiredLuxValue.Override(skyLux);
            _rim.gameObject.SetActive(!city && modLights);
            _fill.gameObject.SetActive(!city && modLights);
        }

        public void Aim(string view, float headHeight)
        {
            Vector3 feet = CharacterPosition;
            Vector3 head = feet + Vector3.up * headHeight;
            if (_name == "city")
            {
                // The gameplay camera: 3.1 m from the player, looking down on them.
                Vector3 chest = feet + Vector3.up * 1.25f;
                Camera.fieldOfView = 63f;
                float pitch = 40f * Mathf.Deg2Rad;
                Camera.transform.position = chest + new Vector3(0f, Mathf.Sin(pitch), Mathf.Cos(pitch)) * 3.1f;
                Camera.transform.LookAt(feet + Vector3.up * 1.05f);
                Focus = view == "head" ? head : feet + Vector3.up * 0.95f;
            }
            else
            {
                Camera.fieldOfView = 25f;
                // The game's camera direction, zoomed in as far as the screen's zoom button goes.
                Vector3 from = (new Vector3(1.86f, 1.65f, -1.29f) - head).normalized;
                Camera.transform.position = head + from * 0.95f;
                Camera.transform.LookAt(head + Vector3.up * 0.02f);
                Focus = head;
                if (view == "feet")
                {
                    // Looking down at the shoes from a step away, as in the shoe shop.
                    Vector3 ankle = feet + Vector3.up * 0.08f;
                    Camera.transform.position = ankle + new Vector3(from.x, 0f, from.z).normalized * 1.25f + Vector3.up * 0.55f;
                    Camera.transform.LookAt(ankle);
                    Focus = ankle;
                }
                if (view == "legs" || view == "body" || view == "hem")
                {
                    // A garment's whole length from the fitting room's distance; "hem" is the trouser break on the shoe.
                    float height = view == "legs" ? 0.56f : view == "hem" ? 0.22f : 0.92f, distance = view == "legs" ? 3.3f : view == "hem" ? 2.3f : 4.6f;
                    Vector3 middle = feet + Vector3.up * height;
                    Camera.transform.position = middle + new Vector3(from.x, view == "hem" ? 0.25f : 0.08f, from.z).normalized * distance;
                    Camera.transform.LookAt(middle);
                    Focus = middle;
                }
                if (view == "torso")
                {
                    // The clothing shop's distance: the same direction, far enough back to see a whole top.
                    Vector3 chest = feet + Vector3.up * 1.2f;
                    Camera.transform.position = chest + from * 2.2f;
                    Camera.transform.LookAt(chest);
                    Focus = chest;
                }
            }
            Vector3 subject = feet + Vector3.up * 1.3f;
            _rim.transform.LookAt(subject);
            _fill.transform.LookAt(subject);
        }

        private static Light MakeLight(GameObject parent, string name, HDLightTypeAndShape type, float intensity, LightUnit unit, Color colour, bool shadows)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent.transform, false);
            HDAdditionalLightData data = holder.AddHDLight(type);
            data.SetIntensity(intensity, unit);
            data.color = colour;
            data.EnableShadows(shadows);
            data.affectsVolumetric = false;
            return holder.GetComponent<Light>();
        }

        private void Backdrop(Vector3 position, Vector3 size)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Backdrop";
            wall.transform.SetParent(_creation.transform, false);
            wall.transform.position = position;
            wall.transform.localScale = size;
            var paint = new Material(Shader.Find("HDRP/Lit"));
            paint.SetColor("_BaseColor", new Color(0.72f, 0.69f, 0.66f));
            paint.SetFloat("_Smoothness", 0.2f);
            wall.GetComponent<Renderer>().sharedMaterial = paint;
        }

        private static Color Hex(string hex)
        {
            return PreviewRunner.Hex(hex);
        }
    }
}
