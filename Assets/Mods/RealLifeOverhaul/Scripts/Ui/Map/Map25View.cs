using System;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// How the map is seen: a camera, either one of the minimap's own (a point, the way up the picture, a pitch, how many metres
    /// across) or the game's (its two matrices, copied), with the planes of its view, the place a point has on the picture and
    /// the place on the ground a point of the picture shows. <see cref="Rise"/> is how tall the blocks stand: 1 from a pitch of 60
    /// degrees down, 0 from 80 up, so seen from above the map is flat. Arrays are made once; setting the view allocates nothing.
    /// </summary>
    public sealed class Map25View
    {
        public const float Fov = 28f;               // degrees: the minimap's camera has a little perspective, as the mockup's has
        public const float LeastRise = 0.004f;      // a flat map is not quite flat: a rooftop's pieces stay apart in depth
        public const float FlatPitch = 85f;         // degrees: the pitch the map button tips the full map to for the flat view
        public const float TiltPitch = 47f;         // the game's own map camera

        public readonly float[] View = new float[16];
        public readonly float[] Projection = new float[16];
        public readonly float[] ViewProjection = new float[16];
        public readonly float[] Planes = new float[24];
        public float Rise = 1f;
        public float PitchDegrees = 90f;

        /// <summary>
        /// The minimap's camera: looking at a point on the ground, <paramref name="heading"/> degrees clockwise from north up the
        /// picture, tipped <paramref name="pitch"/> degrees from the horizon (90 looks straight down), so that <paramref name="across"/>
        /// metres fill the width of a picture of the given shape at the point.
        /// </summary>
        public void Look(float tx, float ty, float tz, float heading, float pitch, float across, float aspect)
        {
            pitch = pitch < 10f ? 10f : pitch > 89.5f ? 89.5f : pitch;
            float h = heading * Map25Math.Deg, p = pitch * Map25Math.Deg;
            float fx = (float)(Math.Sin(h) * Math.Cos(p)), fy = (float)-Math.Sin(p), fz = (float)(Math.Cos(h) * Math.Cos(p));
            float distance = across / (2f * (float)Math.Tan(Fov * Map25Math.Deg * 0.5f) * aspect);
            Map25Math.View(View, tx - fx * distance, ty - fy * distance, tz - fz * distance, fx, fy, fz);
            Map25Math.Perspective(Projection, Fov, aspect, distance * 0.08f, distance * 4f);
            PitchDegrees = pitch;
            Rise = RiseOf(pitch);
            Finish();
        }

        /// <summary>The game's camera, from the matrices it has this frame (the world to the camera, and the projection that went with it).</summary>
        public void Set(float[] view, float[] projection, float pitchDegrees)
        {
            Array.Copy(view, View, 16);
            Array.Copy(projection, Projection, 16);
            PitchDegrees = pitchDegrees;
            Rise = RiseOf(pitchDegrees);
            Finish();
        }

        private void Finish()
        {
            Map25Math.Multiply(Projection, View, ViewProjection);
            Map25Math.Planes(ViewProjection, Planes);
        }

        /// <summary>How tall the blocks stand at a pitch: 1 at 60 degrees or less, the least at 80 or more, between them eased.</summary>
        public static float RiseOf(float pitchDegrees)
        {
            float t = (80f - pitchDegrees) / 20f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            t = t * t * (3f - 2f * t);
            return t < LeastRise ? LeastRise : t;
        }

        /// <summary>
        /// The pitch the full map's camera takes at a distance (metres from the pivot): flat from 300 m up, the game's own 47 degrees from
        /// 200 m down, eased between, so the map the player opens from afar reads as a flat map and the buildings rise as they come close.
        /// </summary>
        public static float PitchFor(float distance)
        {
            float t = (300f - distance) / 100f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            t = t * t * (3f - 2f * t);
            return FlatPitch + (TiltPitch - FlatPitch) * t;
        }

        /// <summary>A place's spot on the picture, 0 to 1 either way from its lower left; false behind the camera.</summary>
        public bool Project(float x, float y, float z, out float u, out float v)
        {
            return Map25Math.Project(ViewProjection, x, y, z, out u, out v);
        }

        /// <summary>Whether a chunk's box (with its blocks standing <see cref="Rise"/> as tall) is in view.</summary>
        public bool Sees(Map25Chunk chunk, bool blocks)
        {
            float top = blocks ? chunk.MaxY * Rise : chunk.MaxY;
            float low = blocks ? Math.Min(chunk.MinY * Rise, chunk.MinY) : chunk.MinY;
            return Map25Math.BoxIn(Planes, chunk.MinX, low, chunk.MinZ, chunk.MaxX, Math.Max(top, low + 0.1f), chunk.MaxZ);
        }
    }
}
