using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The measuring side of the cinematic shots: what the pipeline's volumes give a camera where it stands (its fog and its
    /// exposure, blended as the pipeline blends them), the game's lights read back after an hour is shown, the rays cast
    /// across a frame before it is taken, and a picture's brightness, spread and saturation. Each is asked once or twice a
    /// shot, on a press; nothing per frame.
    /// </summary>
    internal static partial class CineGame
    {
        private const int StatColumns = 96, StatRows = 54;   // pixels a picture's three figures are taken from

        private static readonly float[] Metres = new float[CineJudge.Rays];
        private static readonly CineHit[] Kinds = new CineHit[CineJudge.Rays];
        private static readonly int[] Surfaces = new int[CineJudge.Rays];
        private static VolumeStack _stack;   // the tool's own blend of the pipeline's volumes, made at the first shot of a run

        // ------------------------------------------------------------------ the air where the camera stands

        /// <summary>
        /// The pipeline's volumes blended for a camera where it stands now, as the pipeline itself blends them (its own
        /// anchor if the game gave it one, its own layers): the metres a ray goes in the fog and the height the fog reaches,
        /// 0 for no fog, and the fixed exposure, NaN where it is not a fixed one. The game's fog and bloom are set district
        /// by district in local volumes, so the place matters. False with no camera.
        /// </summary>
        public static bool Air(Camera camera, out float reach, out float height, out float exposure)
        {
            reach = height = 0f;
            exposure = float.NaN;
            if (camera == null)
            {
                return false;
            }
            var data = camera.GetComponent<HDAdditionalCameraData>();
            Transform anchor = data != null && data.volumeAnchorOverride != null ? data.volumeAnchorOverride : camera.transform;
            LayerMask layers = data != null ? data.volumeLayerMask : (LayerMask)1;
            VolumeManager volumes = VolumeManager.instance;
            _stack = _stack ?? volumes.CreateStack();
            volumes.Update(_stack, anchor, layers);
            Fog fog = _stack.GetComponent<Fog>();
            if (fog != null && fog.enabled.value)
            {
                reach = fog.meanFreePath.value;
                height = fog.maximumHeight.value;
            }
            Exposure light = _stack.GetComponent<Exposure>();
            if (light != null && light.mode.value == ExposureMode.Fixed)
            {
                exposure = light.fixedExposure.value - light.compensation.value;
            }
            return true;
        }

        /// <summary>The tool's blend of the volumes let go, at the end of a run.</summary>
        public static void DropAir()
        {
            _stack?.Dispose();
            _stack = null;
        }

        /// <summary>
        /// The fog for a shot. The game's own reach is read first, with the tool's setting off; then, where the shot's depth
        /// asks for clearer air than the camera stands in (CineJudge.Haze), the reach alone is set, in the tool's own volume
        /// over the game's and the look's. The fog's colour, its height and its switch are never touched. Returns what the
        /// picture is drawn with; <paramref name="game"/> is what the game had.
        /// </summary>
        public static float Haze(Fog haze, Camera camera, float depth, out float game, out float height, out float exposure)
        {
            if (haze != null)
            {
                haze.meanFreePath.overrideState = false;
            }
            Air(camera, out game, out height, out exposure);
            float wanted = CineJudge.Haze(game, depth);
            if (haze == null || wanted <= game + 1f)
            {
                return game;
            }
            haze.meanFreePath.overrideState = true;
            haze.meanFreePath.value = wanted;
            return wanted;
        }

        /// <summary>The fog left as the game has it: for the reading of the game's own view.</summary>
        public static void HazeOff(Fog haze)
        {
            if (haze != null)
            {
                haze.meanFreePath.overrideState = false;
            }
        }

        // ------------------------------------------------------------------ the light, read back

        /// <summary>
        /// The game's light as it stands, read from the game after an hour was shown: its sun and its moon (on or off, their
        /// strength, the sun's colour and height), whether its street lamps are to be on, and the hour it lights the city for.
        /// </summary>
        public static CineSeen Seen(float hazeGame, float hazeUsed, float hazeHeight, float exposure)
        {
            var seen = new CineSeen
            {
                Hour = HourShown, SunPeak = _sunPeak, SunOnHour = _sunOnHour, SunOffHour = _sunOffHour,
                HazeGame = hazeGame, HazeUsed = hazeUsed, HazeHeight = hazeHeight, Exposure = exposure
            };
            TimeOfDayController day = Clock();
            Light sun = day != null ? day.sunLight : null, moon = day != null ? day.moonLight : null;
            if (sun == null || moon == null)
            {
                return seen;
            }
            var sunData = sun.GetComponent<HDAdditionalLightData>();
            var moonData = moon.GetComponent<HDAdditionalLightData>();
            seen.Read = true;
            seen.SunOn = sun.isActiveAndEnabled;
            seen.SunLux = sunData != null ? sunData.intensity : sun.intensity;
            seen.SunKelvin = sun.colorTemperature;
            seen.SunUp = Mathf.Asin(Mathf.Clamp(-sun.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            seen.MoonOn = moon.isActiveAndEnabled;
            seen.MoonLux = moonData != null ? moonData.intensity : moon.intensity;
            seen.Lamps = day.ShouldOutdoorLightsBeOn();
            return seen;
        }

        // ------------------------------------------------------------------ what fills the frame

        /// <summary>
        /// Whether the frame from a place is worth taking: rays cast across it, each told as sky, ground, a tree, the shot's
        /// own subject or anything else, with the surface it met, and judged by the rules (CineJudge.Frame). Forty five
        /// rays and, for a park, one more a tree; before the camera is moved there.
        /// </summary>
        public static bool Fill(CineShot shot, in CineFound found, in CinePose pose, Vector3 eye, out string why)
        {
            Quaternion turn = Facing(pose, eye);
            float tanHigh = Mathf.Tan(shot.Fov * 0.5f * Mathf.Deg2Rad), tanWide = tanHigh * CineRules.Aspect;
            CineBox box = found.Box;
            bool body = box.HalfLength > 0.01f && box.Height > 0.01f;   // a street and a skyline have no body of their own
            for (int i = 0; i < CineJudge.Rays; i++)
            {
                CineJudge.RayAt(i, out float x, out float y);
                Vector3 way = turn * new Vector3((x * 2f - 1f) * tanWide, (y * 2f - 1f) * tanHigh, 1f).normalized;
                if (!Physics.Raycast(eye, way, out RaycastHit hit, CineJudge.RayReach, Solid, QueryTriggerInteraction.Ignore))
                {
                    Metres[i] = CineJudge.RayReach;
                    Kinds[i] = CineHit.Sky;
                    Surfaces[i] = 0;
                    continue;
                }
                Metres[i] = hit.distance;
                Surfaces[i] = Plane(hit);
                if ((((1 << hit.collider.gameObject.layer) & GroundMask) != 0 || hit.normal.y > 0.85f) && hit.point.y < eye.y)
                {
                    Kinds[i] = CineHit.Ground;   // the street, the grass under a park's trees, or the roof the tripod stands on
                }
                else if (body && Inside(box, hit.point))
                {
                    Kinds[i] = CineHit.Subject;
                }
                else
                {
                    Kinds[i] = hit.collider.GetComponentInParent<TreeController>() != null ? CineHit.Tree : CineHit.Solid;
                }
            }
            int trees = shot.Subject == CineSubject.Trees ? CineSubjects.TreesSeen(eye, turn, tanWide, tanHigh) : 0;
            return CineJudge.Frame(shot.Style, shot.Subject, Metres, Kinds, Surfaces, trees, out why);
        }

        // A number for the flat surface a ray met, the same for every ray that meets it: a wall is one surface however many
        // colliders it is made of. Never 0, which is the sky's.
        private static int Plane(RaycastHit hit)
        {
            Vector3 n = hit.normal;
            int facing = (Mathf.RoundToInt(n.x * 4f) * 31 + Mathf.RoundToInt(n.y * 4f)) * 31 + Mathf.RoundToInt(n.z * 4f);
            int key = unchecked(facing * 8191 + Mathf.RoundToInt(Vector3.Dot(n, hit.point) * 2f));
            return key == 0 ? 1 : key;
        }

        // Whether a point lies in the subject's own box, with a little room round it.
        private static bool Inside(in CineBox box, Vector3 point)
        {
            float dx = point.x - box.X, dz = point.z - box.Z;
            float along = dx * box.Fx + dz * box.Fz, across = dx * box.Fz - dz * box.Fx;
            return Mathf.Abs(along) <= box.HalfLength + 0.6f && Mathf.Abs(across) <= box.HalfWidth + 0.6f && point.y >= box.Y - 0.5f && point.y <= box.Y + box.Height + 1f;
        }

        // ------------------------------------------------------------------ a picture's three figures

        /// <summary>A picture's brightness, spread and saturation, from a grid of its pixels: the target's, or the screen's own.</summary>
        public static CineStat Stat(Frame frame)
        {
            if (frame == null)
            {
                return default(CineStat);
            }
            PhotosGame.Shot screen = frame.Screen;
            return screen != null ? Stat(screen.Pixels, screen.Width, screen.Height, screen.Bytes, screen.Bgr) : Stat(frame.Pixels, frame.Width, frame.Height, 3, false);
        }

        private static CineStat Stat(NativeArray<byte> pixels, int width, int height, int bytes, bool bgr)
        {
            var tally = new CineTally();
            if (!pixels.IsCreated || width <= 0 || height <= 0)
            {
                return tally.Finish();
            }
            for (int row = 0; row < StatRows; row++)
            {
                int y = (int)((row + 0.5f) * height / StatRows);
                for (int column = 0; column < StatColumns; column++)
                {
                    int x = (int)((column + 0.5f) * width / StatColumns);
                    int at = (y * width + x) * bytes;
                    if (at < 0 || at + 2 >= pixels.Length)
                    {
                        continue;
                    }
                    int first = pixels[at], green = pixels[at + 1], third = pixels[at + 2];
                    tally.Add(bgr ? third : first, green, bgr ? first : third);
                }
            }
            return tally.Finish();
        }
    }
}
