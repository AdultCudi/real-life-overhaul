using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>How the ground is whitened at this load.</summary>
    internal enum SnowWay
    {
        Unknown,   // not tried yet this city
        Layers,    // the decal on the snow's own decal layer: the ground, roads, pavements and roofs alone
        None,      // no snow lying: it falls, and the roads' grip still follows it
    }

    /// <summary>
    /// What snow looks like. Flakes: one particle system kept round what the camera sees (between the camera and the
    /// player it looks at, from under the ground to over the camera), soft round dots of the game's own particle material
    /// sized and spaced for the camera's distance (<see cref="SeasonLookRules.Field"/>): a few pixels across where the
    /// camera looks, closer together for a near camera and on a heavy day. The ground: one decal projector over the
    /// streets round the player, a copy of the game's road paint carrying a patchy white made once, drawn on the snow's
    /// own decal layer alone (<see cref="SnowGround"/>: the ground, roads, pavements and roofs), its angle fade keeping
    /// walls clear. That needs HDRP's decal layers, which the game's asset supports but its camera leaves off, so they are
    /// switched on for the player's camera only while snow lies outdoors. Where they cannot be had no snow lies at all: a
    /// decal without them whitens everything in its box, cars, people and faces. Asked once a second by the director, and
    /// the flakes' box moved after the camera four times a second while they fall (a few reads and one position, nothing
    /// allocated); Unity draws the rest, so nothing of the mod's runs a frame. Made the first time snow is shown, gone with
    /// the city; indoors nothing falls.
    /// </summary>
    internal sealed class SnowLook
    {
        private const float FlakeSmallest = 0.6f;    // of the field's flake size, the smallest flake
        private const float FlakeLargest = 1.4f;     //   and the largest
        private const float FlakeHeadroom = 1.25f;   // of the field's count the system may hold, as rates and lives vary
        private const float UnderGround = 2f;        // metres under the player the box reaches, for the ground's dips
        private const float CoverSpan = 150f;        // metres: the square the snow lies on round the player
        private const float CoverTile = 12f;         // metres the white's pattern repeats over
        private const float RoofTop = 70f;           // metres over the player the cover reaches, for the roofs
        private const float RoofDepth = 76f;         //   and down to 6 m under the player's feet
        private const float ConfirmWait = 4f;        // seconds the pipeline may take to draw with the decal layers asked for
        private const float Recentre = 25f;          // metres the player may go from the cover's middle before it follows
        private const int MapSize = 128;

        private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int DecalBlendId = Shader.PropertyToID("_DecalBlend");

        private readonly Transform _root;
        private ParticleSystem _flakes;
        private Transform _fall;
        private bool _flakesFailed, _flakesOn;   // failed: cannot be made this city
        private float _rate = -1f;
        private float _falling;
        private int _fieldKey = -1;               // the camera's distance and height and the fall the flakes were last fitted to

        private DecalProjector _cover;
        private Material _coverPaint;
        private Texture2D _coverMap;
        private bool _coverFailed, _layersAsked;
        private Vector3 _centre;
        private float _fade = -1f;
        private float _askedAt;

        /// <summary>The ground's marks, made behind the loading screen; the snow lies only where they are.</summary>
        public readonly SnowGround Ground = new SnowGround();

        /// <summary>How the ground is whitened at this load.</summary>
        public SnowWay Way { get; private set; }

        /// <summary>Why a part could not be made, for the log and the test; null while it can.</summary>
        public string FlakesMissing { get; private set; }
        public string CoverMissing { get; private set; }

        public bool Flaking => _flakesOn && _flakes != null && _rate > 0f;
        public bool Covering => _cover != null && _cover.gameObject.activeSelf && _fade > 0f;
        public int Flakes => _flakes != null ? _flakes.particleCount : 0;
        public DecalLayerEnum CoverLayer => _cover != null ? _cover.decalLayerMask : DecalLayerEnum.Nothing;

        /// <summary>The flakes' box follows the camera: they are falling, outdoors.</summary>
        public bool Following => _flakesOn && _flakes != null;

        /// <summary>The flakes as last fitted to the camera, and the camera's distance from the player then.</summary>
        public SnowField Field { get; private set; }
        public float SeenFrom { get; private set; }

        /// <summary>The flakes are the game's own soft round particle (else small lit pieces, where it was not found).</summary>
        public bool Soft { get; private set; }

        public SnowLook(Transform root)
        {
            _root = root;
        }

        /// <summary>
        /// Shows <paramref name="falling"/> (0 to 1) and <paramref name="cover"/> (0 to 1) round the player, or nothing indoors.
        /// The cover can only be made behind a screen, as the game's road paint and the ground are found there: until then only flakes fall.
        /// </summary>
        public void Show(float falling, float cover, Vector3 player, bool outdoors)
        {
            if (!outdoors)
            {
                Hide();
                return;
            }
            Fall(falling, player);
            Lie(cover, player);
        }

        public void Hide()
        {
            if (_flakes != null && _flakesOn)
            {
                _flakes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _flakesOn = false;
                _rate = -1f;
                _fieldKey = -1;
            }
            Uncover();
        }

        /// <summary>The camera draws with decal layers now, as the pipeline last drew it (after its own checks of the asset).</summary>
        public static bool LayersInEffect()
        {
            Camera camera = GameManager.GetMainCamera();
            return camera != null && HDCamera.GetOrCreate(camera).frameSettings.IsEnabled(FrameSettingsField.DecalLayers);
        }

        // The cover off and the camera's decal layers given back: nothing of the snow left on the picture.
        private void Uncover()
        {
            if (_cover != null && _cover.gameObject.activeSelf)
            {
                _cover.gameObject.SetActive(false);
            }
            _fade = -1f;
            if (_layersAsked)
            {
                _layersAsked = false;
                PerfFrameSettings.ReleaseOn(FrameSettingsField.DecalLayers);
            }
        }

        private void Fall(float falling, Vector3 player)
        {
            _falling = Mathf.Clamp01(falling);
            if (falling <= 0f)
            {
                if (_flakes != null && _flakesOn && _rate != 0f)
                {
                    ParticleSystem.EmissionModule off = _flakes.emission;
                    off.rateOverTime = 0f;   // those in the air come down; none start
                    _rate = 0f;
                    _fieldKey = -1;
                }
                return;
            }
            if (_flakes == null && !BuildFlakes())
            {
                return;
            }
            Follow(player);
            if (!_flakesOn)
            {
                _flakes.Play(true);
                _flakesOn = true;
            }
        }

        /// <summary>
        /// The flakes' box moved to what the camera sees, between it and the player, and the flakes fitted again when the
        /// camera's distance, its height or the fall has changed: a few reads and one position, nothing allocated.
        /// </summary>
        public void Follow(Vector3 player)
        {
            Camera camera = GameManager.GetMainCamera();
            if (_flakes == null || camera == null)
            {
                return;
            }
            Vector3 eye = camera.transform.position;
            Vector3 gap = eye - player;
            float distance = gap.magnitude;
            int key = Mathf.RoundToInt(Mathf.Min(distance, 99f)) * 10000 + Mathf.RoundToInt(Mathf.Clamp(gap.y, 0f, 99f)) * 100 + Mathf.RoundToInt(_falling * 20f);
            if (key != _fieldKey)
            {
                _fieldKey = key;
                Fit(SeasonLookRules.Field(distance, gap.y, _falling), distance);
            }
            float height = Field.Height;
            _fall.position = new Vector3(player.x + gap.x * 0.5f, player.y - UnderGround + height * 0.5f, player.z + gap.z * 0.5f);
        }

        private void Fit(SnowField field, float distance)
        {
            Field = field;
            SeenFrom = distance;
            ParticleSystem.MainModule main = _flakes.main;
            main.startSize = new ParticleSystem.MinMaxCurve(field.Size * FlakeSmallest, field.Size * FlakeLargest);
            main.startLifetime = field.Life;
            main.maxParticles = Mathf.Min(SeasonLookRules.FlakesMost, Mathf.CeilToInt(field.Count * FlakeHeadroom));
            ParticleSystem.ShapeModule shape = _flakes.shape;
            shape.scale = new Vector3(field.Span, field.Height, field.Span);
            ParticleSystem.EmissionModule emission = _flakes.emission;
            emission.rateOverTime = field.Rate;
            _rate = field.Rate;
        }

        private bool BuildFlakes()
        {
            if (_flakesFailed)
            {
                return false;
            }
            Material soft = SeasonGame.FlakePaint;
            Material paint = soft != null ? soft : Shapes.Paint(new Color(0.95f, 0.97f, 1f), 1.2f);
            if (paint == null)
            {
                _flakesFailed = true;
                FlakesMissing = "the game has neither its soft round particle nor a lit shader for the flakes";
                SeasonDirector.Logger?.Warn("Seasons: no falling snow this session: " + FlakesMissing + ".");
                return false;
            }
            var made = new GameObject("RealLifeOverhaul Snowfall");
            made.transform.SetParent(_root, false);
            made.transform.rotation = Quaternion.identity;   // the box lies square to the world; only its place follows the camera
            var flakes = made.AddComponent<ParticleSystem>();
            flakes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = flakes.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;   // the camera moves through the snow, it does not carry it
            main.startSpeed = 0f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotation3D = soft == null;
            main.gravityModifier = 0f;
            main.maxParticles = SeasonLookRules.FlakesMost;
            ParticleSystem.EmissionModule emission = flakes.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = flakes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;   // started anywhere in the box, so a moving camera never sees an empty edge
            ParticleSystem.VelocityOverLifetimeModule drift = flakes.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            drift.y = new ParticleSystem.MinMaxCurve(-1.4f, -0.9f);
            drift.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            ParticleSystem.NoiseModule noise = flakes.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.3f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.2f;
            // Grown in over the first moments and shrunk away at the end, so no flake pops into or out of the air.
            ParticleSystem.SizeOverLifetimeModule grow = flakes.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.06f, 1f), new Keyframe(0.92f, 1f), new Keyframe(1f, 0f)));
            var renderer = made.GetComponent<ParticleSystemRenderer>();
            if (soft != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = Shapes.Cube;
            }
            renderer.sharedMaterial = paint;   // the game's own, shared and never changed
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _flakes = flakes;
            _fall = made.transform;
            Soft = soft != null;
            FlakesMissing = null;
            return true;
        }

        private void Lie(float cover, Vector3 player)
        {
            if (cover <= 0f)
            {
                Uncover();
                return;
            }
            if (_cover == null && !BuildCover())
            {
                return;
            }
            if (!Layered())
            {
                return;
            }
            bool on = _cover.gameObject.activeSelf;
            Vector3 offset = player - _centre;
            if (!on || offset.x * offset.x + offset.z * offset.z > Recentre * Recentre || Mathf.Abs(offset.y) > 4f)
            {
                Pose(player);
            }
            float fade = Mathf.Round(Mathf.Clamp01(cover) * 50f) / 50f;
            if (fade != _fade)
            {
                _cover.fadeFactor = fade;
                _fade = fade;
            }
        }

        private bool BuildCover()
        {
            if (_coverFailed)
            {
                return false;
            }
            var asset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            string none = asset == null ? "the game draws without HDRP"
                : !asset.currentPlatformRenderPipelineSettings.supportDecals ? "decals are switched off in this game's graphics"
                : !asset.currentPlatformRenderPipelineSettings.supportDecalLayers ? "this graphics setting has no decal layers, and a decal without them would whiten cars and people too"
                : null;
            if (none != null)
            {
                Refuse(none);
                return false;
            }
            if (Ground.Bit == 0u)
            {
                CoverMissing = Ground.Missing;
                return false;   // asked again: the ground is marked behind the next loading screen
            }
            Material template = RoadPaint.Decal;
            if (template == null)
            {
                CoverMissing = "the game's road paint is found behind the next loading screen";
                return false;   // asked again: found at the next door or load
            }
            DropCoverAssets();   // a projector lost with the city's objects leaves its paint behind
            _coverMap = new Texture2D(MapSize, MapSize, TextureFormat.RGBA32, false) { name = "RealLifeOverhaul Snow Cover", wrapMode = TextureWrapMode.Repeat };
            _coverMap.SetPixels32(SnowPattern());
            _coverMap.Apply(false, true);
            _coverPaint = new Material(template) { name = "RealLifeOverhaul Snow Cover" };
            _coverPaint.SetTexture(BaseColorMapId, _coverMap);
            _coverPaint.SetColor(BaseColorId, new Color(0.93f, 0.95f, 0.98f, 1f));
            _coverPaint.SetFloat(DecalBlendId, 1f);
            var piece = new GameObject("RealLifeOverhaul Snow Cover");
            piece.SetActive(false);
            piece.transform.SetParent(_root, false);
            _cover = piece.AddComponent<DecalProjector>();
            _cover.material = _coverPaint;
            _cover.decalLayerMask = Ground.Layer;   // the ground and roofs alone: never the default layer, which cars and people carry
            _cover.drawDistance = CoverSpan + 50f;
            _cover.fadeScale = 0.95f;
            _cover.startAngleFade = 30f;   // walls and fronts stay as they are (HDRP fades by angle only with decal layers, as here)
            _cover.endAngleFade = 60f;
            _cover.size = new Vector3(CoverSpan, CoverSpan, RoofDepth);
            _cover.pivot = new Vector3(0f, 0f, RoofDepth * 0.5f);
            _cover.uvScale = new Vector2(CoverSpan / CoverTile, CoverSpan / CoverTile);
            CoverMissing = null;
            SeasonDirector.Logger?.Info("Seasons: snow lies on the ground and the roofs alone (its own decal layer, on " + Ground.Marked + " piece(s)), a copy of the game's '"
                                        + template.name + "'.");
            return true;
        }

        // No white ground for the rest of this city: falling snow and the roads' grip stay.
        private void Refuse(string why)
        {
            _coverFailed = true;
            Way = SnowWay.None;
            CoverMissing = why;
            Uncover();
            SeasonDirector.Logger?.Warn("Seasons: no snow lying on the ground this session (" + why + "); it still falls and is still felt on the roads.");
        }

        // The camera's decal layers asked on, and the cover shown only once the pipeline draws with them: without them the
        // decal would reach everything in its box. False while waiting, and for good where they cannot be had.
        private bool Layered()
        {
            if (!_layersAsked)
            {
                _layersAsked = true;
                _askedAt = Time.unscaledTime;
                PerfFrameSettings.Enable(FrameSettingsField.DecalLayers);
            }
            else
            {
                PerfFrameSettings.Follow();   // two reads: the game may have a new main camera after a load
            }
            if (LayersInEffect())
            {
                Way = SnowWay.Layers;
                return true;
            }
            if (_cover.gameObject.activeSelf)
            {
                _cover.gameObject.SetActive(false);   // a camera that lost them: nothing drawn until they are back
                _fade = -1f;
            }
            if (Time.unscaledTime - _askedAt > ConfirmWait)
            {
                Refuse("the game's camera would not draw with decal layers");
            }
            return false;
        }

        // Laid with the projector off, as the decal system takes one as it is when it comes on; the pattern is pinned to
        // the street, not to the projector, so it does not slide when the cover follows the player.
        private void Pose(Vector3 player)
        {
            GameObject piece = _cover.gameObject;
            piece.SetActive(false);
            _centre = player;
            piece.transform.SetPositionAndRotation(player + Vector3.up * RoofTop, Quaternion.LookRotation(Vector3.down, Vector3.forward));
            float half = CoverSpan / CoverTile * 0.5f;
            _cover.uvBias = new Vector2(Mathf.Repeat(player.x / CoverTile - half, 1f), Mathf.Repeat(player.z / CoverTile - half, 1f));
            piece.SetActive(true);
        }

        // A white that repeats seamlessly: two layers of smoothed lattice noise, mostly opaque with thinner patches.
        private static Color32[] SnowPattern()
        {
            var pixels = new Color32[MapSize * MapSize];
            for (int y = 0; y < MapSize; y++)
            {
                for (int x = 0; x < MapSize; x++)
                {
                    float n = 0.65f * Lattice(x, y, 8) + 0.35f * Lattice(x, y, 32);
                    byte white = (byte)Mathf.RoundToInt(255f * (0.9f + 0.1f * n));
                    byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(0.62f + 0.5f * (n - 0.35f)));
                    pixels[y * MapSize + x] = new Color32(white, white, white, alpha);
                }
            }
            return pixels;
        }

        // Value noise on a lattice of cells a side, wrapping at the edges, smoothed between points.
        private static float Lattice(int x, int y, int cells)
        {
            float fx = x * cells / (float)MapSize, fy = y * cells / (float)MapSize;
            int x0 = (int)fx, y0 = (int)fy;
            float tx = Smooth(fx - x0), ty = Smooth(fy - y0);
            float a = Point(x0 % cells, y0 % cells, cells), b = Point((x0 + 1) % cells, y0 % cells, cells);
            float c = Point(x0 % cells, (y0 + 1) % cells, cells), d = Point((x0 + 1) % cells, (y0 + 1) % cells, cells);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static float Point(int x, int y, int cells)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)cells * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        private void DropCoverAssets()
        {
            if (_coverPaint != null)
            {
                Object.Destroy(_coverPaint);
            }
            if (_coverMap != null)
            {
                Object.Destroy(_coverMap);
            }
            _coverPaint = null;
            _coverMap = null;
        }

        /// <summary>The city is unloading: everything made goes, and the ground's and the camera's layers are put back.</summary>
        public void Dispose()
        {
            if (_flakes != null)
            {
                Object.Destroy(_flakes.gameObject);
            }
            if (_cover != null)
            {
                Object.Destroy(_cover.gameObject);
            }
            if (_layersAsked)
            {
                _layersAsked = false;
                PerfFrameSettings.ReleaseOn(FrameSettingsField.DecalLayers);
            }
            Ground.Unmark();
            DropCoverAssets();
            _flakes = null;
            _fall = null;
            _cover = null;
            Way = SnowWay.Unknown;
            _flakesFailed = _coverFailed = _flakesOn = false;
            _rate = _fade = -1f;
            _fieldKey = -1;
            Soft = false;
        }
    }
}
