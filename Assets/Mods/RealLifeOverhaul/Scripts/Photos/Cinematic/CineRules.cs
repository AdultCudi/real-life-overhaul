using System;
using System.Globalization;

namespace RealLifeOverhaul.Photos
{
    /// <summary>What a cinematic shot is of. Each is looked for in the city as it stands when its shot comes up.</summary>
    public enum CineSubject
    {
        Player,      // the player on foot
        Car,         // the car the player drives, or their own nearest
        Walkers,     // the busiest stretch of pavement: the most walkers within a few metres of each other
        Trees,       // a park or a square: the most trees within a few metres of each other
        Avenue,      // the longest straight run of street from where the player stands
        Skyline,     // the city seen from a roof near the player
        Landmark,    // a landmark or a sight on the mod's map rows
        Bus,         // the mod's bus out on the road
        Stall,       // a stall, a cart or a street cafe of the living city
        Incident,    // an accident, a crime, a fire or a street event that is on
        Frontage,    // the nearest shopfront, for its lit sign
        Station,     // a police station or a hotel of the mod's, for its lit front
        Count
    }

    /// <summary>The light a shot is taken in. With the hour left alone every shot is taken in the light there is.</summary>
    public enum CineLight
    {
        Midday,
        Golden,   // the last of the sun
        Blue,     // the sun gone, the street lamps on
        Night,
        Count
    }

    /// <summary>A lens by its vertical field of view.</summary>
    public enum CineLens
    {
        Long,     // 20 to 35 degrees: it draws the far end of a street close
        Normal,   // over 35 to 50
        Wide      // over 50 to 70
    }

    /// <summary>Where the camera stands for a shot, in a photographer's words.</summary>
    public enum CineStyle
    {
        Establishing,   // wide, from a roof
        Symmetry,       // down the middle of a street at eye height
        Compression,    // a long lens down a street
        Thirds,         // a person or a small group on a third of the frame
        ThreeQuarter,   // three quarters from the front, low
        RearQuarter,    // three quarters from behind, low
        WheelHeight,    // beside a car at the height of its wheels
        OverShoulder,   // behind the player's shoulder, looking where they look
        LowWide,        // from the ground looking up at something tall
        Facade          // square on to a front
    }

    /// <summary>One shot of the list. Plain values, never changed.</summary>
    public sealed class CineShot
    {
        public readonly int Number;            // 1 for the first: the picture's place in the set and in its name
        public readonly string Slug;           // the picture's own name, small letters, digits and underscores
        public readonly CineSubject Subject;
        public readonly CineStyle Style;
        public readonly float Fov;             // degrees, vertical
        public readonly CineLight Light;
        public readonly bool Blur;             // a wide aperture: the subject sharp, the rest soft
        public readonly int Twin;              // the number of the shot it repeats when the hour is left alone, 0 for none

        public CineShot(int number, string slug, CineSubject subject, CineStyle style, float fov, CineLight light, bool blur, int twin = 0)
        {
            Number = number;
            Slug = slug;
            Subject = subject;
            Style = style;
            Fov = fov;
            Light = light;
            Blur = blur;
            Twin = twin;
        }

        /// <summary>The parts of the game a shot changes, as bits of <see cref="CinePart"/>: each must be one the hold gives back.</summary>
        public int Touches(bool allLights)
        {
            int parts = Bit(CinePart.Place) | Bit(CinePart.Lens) | Bit(CinePart.Look) | Bit(CinePart.Target);
            return allLights ? parts | Bit(CinePart.Light) : parts;
        }

        private static int Bit(CinePart part)
        {
            return 1 << (int)part;
        }
    }

    /// <summary>A subject as the camera's sums see it: a point on the ground, the way it faces on the flat, and its size in metres.</summary>
    public struct CineBox
    {
        public float X, Y, Z;
        public float Fx, Fz;                     // the unit way it faces, on the flat
        public float HalfLength, HalfWidth;      // along that way and across it
        public float Height;
    }

    /// <summary>Where the camera stands and what it looks at, before the scene is asked whether the place is clear.</summary>
    public struct CinePose
    {
        public float EyeX, EyeY, EyeZ;
        public float AimX, AimY, AimZ;
        public float AnchorX, AnchorY, AnchorZ;   // a point at the subject known to be in the open: the camera's line runs from it
        public float FocusX, FocusY, FocusZ;      // what is kept sharp
        public float Turn;                        // degrees the camera is turned after aiming, which puts the subject on a third
        public float Reach;                       // metres of the line that are the subject's own body
        public bool NearOnly;                     // only what is close to the lens is soft (the shoulder in an over the shoulder shot)
        public bool Free;                         // the camera stands in open air and no line is tested, only the place itself
    }

    /// <summary>One line of the picker's answer: a shot, whether it is taken, and why not.</summary>
    public struct CinePick
    {
        public int Index;     // in CineRules.Shots
        public bool Take;
        public string Why;    // null for a shot taken
    }

    /// <summary>
    /// The cinematic shots in plain numbers: the list, the lenses, the names, which shots are taken, where the camera stands for
    /// each kind of shot, how far it is pulled in when something is in the way, the hours of the four lights and the depth of
    /// field. Game-free, so EconomyCheck runs it (CinematicChecks.cs). Asked once a shot, never on a frame.
    /// </summary>
    public static class CineRules
    {
        public const int Width = 3840, Height = 2160;     // pixels of a picture
        public const float Aspect = 16f / 9f;
        public const int JpgQuality = 92;

        public const float LongLeast = 20f, LongMost = 35f;   // degrees: a long lens
        public const float NormalMost = 50f;                   // degrees: above it a lens is wide
        public const float WideMost = 70f;                     // degrees: no shot is wider, straight lines bend past it

        public const float Reach = 140f;            // metres from the player a subject is looked for: the game draws its people, cars and lamps to 200
        public const float WalkerRadius = 9f;       // metres: walkers this near each other are one busy stretch
        public const int WalkersLeast = 3;
        public const float TreeRadius = 20f;        // metres: trees this near each other are one park
        public const int TreesLeast = 6;
        public const int ClusterMost = 256;         // points looked at for the busiest spot, the nearest first come
        public const int Bearings = 24;             // ways looked down for a street, 15 degrees apart
        public const float AvenueLeast = 110f;      // metres of clear run that make a street worth a long lens
        public const float AvenueMost = 400f;       // metres a run is measured to
        public const float RoofLeast = 12f;         // metres over the street a roof must be to stand on
        public const float RoofMost = 130f;         // metres: above this the street is a map, not a place
        public const float RoofBest = 60f;          // metres: a higher roof is no better
        public const int Stands = 8;                // places tried for a roof behind the player
        public const float EyeOverRoof = 2.2f;      // metres: a tripod at a parapet
        public const float RoofView = 60f;          // metres of open air a roof must have toward the city: a taller wall in front is no view
        public const float WheelClear = 1.3f;       // metres before the lens of a wheel height shot that lie outside the car: only they are tested

        public const float LensRadius = 0.3f;       // metres of clear air the lens wants round it
        public const float Margin = 0.6f;           // metres the camera stops short of what is in its way
        public const float SlideStep = 0.6f;        // metres it moves along its line at a time while its place is not clear
        public const int Slides = 10;               // steps before the place is given up
        public const float LeastHeight = 0.22f;     // metres over the ground the lens is kept
        public const int TriesMost = 8;             // places tried for one shot at the most: sides and facings, ways round a thing, or roofs
        public const float WholeAbove = 0.38f;      // of the field of view: a tall thing's top stands no higher than this over the aim, so sky is over it
        public const float WholeBelow = 0.46f;      // and its foot no lower than this under it
        public const float LowWideMost = 160f;      // metres a tall thing is stood back from at the most

        public const float Vignette = 0.22f;        // HDRP's vignette intensity: slight
        public const float VignetteSmooth = 0.4f;

        public const float Midday = 12.5f;          // the hour of the midday light
        public const float Night = 23f;             // the hour of the night light: the game's own slider ends at 24
        public const float GoldenStrong = 0.35f;    // of the sun's most: the golden hour is taken while the sun still has this much of its strength
        public const float GoldenBack = 3f;         // hours before the sun goes out that the golden hour is looked for in
        public const float BlueAfter = 0.35f;       // hours after the sun is gone and the lamps are on
        public const float HourLeast = 5f, HourMost = 23.75f;   // the game's own time of day tool runs from 5:00 to 24:00

        public const int SettleFrames = 24;         // frames drawn at a new place before the picture: shadows, lights and what is streamed in
        public const float SettleSeconds = 0.6f;    // and real seconds at the least
        public const int LightFrames = 40;          // frames drawn after the light changes: the sky's light on the city is spread over frames
        public const float LightSeconds = 1.2f;
        public const int LampFrames = 60;           // frames drawn at a new place with the lamps on: the game lights them a few a frame as they come into view
        public const float LampSeconds = 1.5f;
        public const int FirstFrames = 50;          // frames before the first shot: what the game had hidden between its camera and the player fades back in
        public const float FirstSeconds = 1f;
        public const int TargetFrames = 16;         // frames drawn at the picture's own size before it is read: the smoothing of edges builds over frames
        public const float ArmedSeconds = 30f;      // real seconds a press waits for the menu to close

        /// <summary>The set, in the order it is taken: by light, so the light changes three times at the most.</summary>
        public static readonly CineShot[] Shots =
        {
            new CineShot(1, "skyline", CineSubject.Skyline, CineStyle.Establishing, 62f, CineLight.Midday, false),
            new CineShot(2, "avenue_symmetry", CineSubject.Avenue, CineStyle.Symmetry, 42f, CineLight.Midday, false),
            new CineShot(3, "pavement", CineSubject.Walkers, CineStyle.Thirds, 32f, CineLight.Midday, true),
            new CineShot(4, "park", CineSubject.Trees, CineStyle.LowWide, 58f, CineLight.Midday, false),
            new CineShot(5, "landmark", CineSubject.Landmark, CineStyle.LowWide, 60f, CineLight.Midday, false),
            new CineShot(6, "car_front", CineSubject.Car, CineStyle.ThreeQuarter, 35f, CineLight.Golden, true),
            new CineShot(7, "car_wheel", CineSubject.Car, CineStyle.WheelHeight, 56f, CineLight.Golden, true),
            new CineShot(8, "avenue_long", CineSubject.Avenue, CineStyle.Compression, 24f, CineLight.Golden, false),
            new CineShot(9, "over_shoulder", CineSubject.Player, CineStyle.OverShoulder, 40f, CineLight.Golden, true),
            new CineShot(10, "player_thirds", CineSubject.Player, CineStyle.Thirds, 30f, CineLight.Golden, true),
            new CineShot(11, "stall", CineSubject.Stall, CineStyle.Thirds, 35f, CineLight.Golden, true),
            new CineShot(12, "skyline_lit", CineSubject.Skyline, CineStyle.Establishing, 62f, CineLight.Blue, false, 1),
            new CineShot(13, "bus", CineSubject.Bus, CineStyle.ThreeQuarter, 35f, CineLight.Blue, true),
            new CineShot(14, "incident", CineSubject.Incident, CineStyle.ThreeQuarter, 30f, CineLight.Blue, false),
            new CineShot(15, "avenue_lights", CineSubject.Avenue, CineStyle.Compression, 22f, CineLight.Night, false, 8),
            new CineShot(16, "frontage", CineSubject.Frontage, CineStyle.Facade, 40f, CineLight.Night, false),
            new CineShot(17, "car_tail", CineSubject.Car, CineStyle.RearQuarter, 35f, CineLight.Night, true),
            new CineShot(18, "station", CineSubject.Station, CineStyle.ThreeQuarter, 38f, CineLight.Night, false),
        };

        private static readonly string[] LightWords = { "midday", "golden", "blue", "night" };
        public const string NowWord = "now";   // a picture's light in its name when the hour was left alone

        private static readonly string[] SubjectWords =
        {
            "the player on foot", "a car of the player's", "a busy pavement", "a park or a square", "a long straight street", "the skyline from a roof",
            "a landmark", "a bus", "a stall or a street cafe", "an incident", "a shopfront", "a police station or a hotel"
        };

        private static readonly string[] StyleWords =
        {
            "wide, from a roof", "down the middle of the street at eye height", "a long lens down the street", "the subject on a third of the frame",
            "three quarters from the front, low", "three quarters from behind, low", "beside the car at wheel height", "over the player's shoulder",
            "from the ground, looking up", "square on to the front"
        };

        public static string LightWord(CineLight light)
        {
            return light >= 0 && light < CineLight.Count ? LightWords[(int)light] : NowWord;
        }

        public static string SubjectWord(CineSubject subject)
        {
            return subject >= 0 && subject < CineSubject.Count ? SubjectWords[(int)subject] : "nothing";
        }

        public static string StyleWord(CineStyle style)
        {
            return (int)style >= 0 && (int)style < StyleWords.Length ? StyleWords[(int)style] : "plain";
        }

        // ------------------------------------------------------------------ lenses

        public static CineLens LensOf(float fov)
        {
            return fov <= LongMost ? CineLens.Long : fov <= NormalMost ? CineLens.Normal : CineLens.Wide;
        }

        /// <summary>Whether a field of view is one a kind of shot is taken with: a long lens for a street or a person, a wide one from a roof or the ground.</summary>
        public static bool LensFits(CineStyle style, float fov)
        {
            if (!(fov >= LongLeast && fov <= WideMost))
            {
                return false;
            }
            CineLens lens = LensOf(fov);
            switch (style)
            {
                case CineStyle.Compression:
                case CineStyle.Thirds:
                    return lens == CineLens.Long;
                case CineStyle.Establishing:
                case CineStyle.WheelHeight:
                case CineStyle.LowWide:
                    return lens == CineLens.Wide;
                case CineStyle.Symmetry:
                case CineStyle.OverShoulder:
                case CineStyle.Facade:
                    return lens == CineLens.Normal;
                default:
                    return lens != CineLens.Wide;   // three quarters of a car: wide would stretch its nose
            }
        }

        /// <summary>The focal length on a 36 by 24 mm frame that sees a vertical field of view, in millimetres: what a photographer calls the lens.</summary>
        public static int FocalLength(float fov)
        {
            return (int)Math.Round(12.0 / Math.Tan(Clamp(fov, 1f, 170f) * Math.PI / 360.0));
        }

        /// <summary>A lens as shots.txt writes it: "24 degrees, about 56 mm (long)".</summary>
        public static string LensWords(float fov)
        {
            return fov.ToString("0", CultureInfo.InvariantCulture) + " degrees, about " + FocalLength(fov).ToString(CultureInfo.InvariantCulture) + " mm ("
                   + LensOf(fov).ToString().ToLowerInvariant() + ")";
        }

        // ------------------------------------------------------------------ names

        /// <summary>
        /// A picture's name without its ending: "07_golden_avenue_long". The light's word is the light the game was read to
        /// show when the picture was taken (CineJudge.Word), never the one that was asked for; "now" where it could not be read.
        /// </summary>
        public static string FileName(CineShot shot, string lightWord)
        {
            return shot.Number.ToString("00", CultureInfo.InvariantCulture) + "_" + (string.IsNullOrEmpty(lightWord) ? NowWord : lightWord) + "_" + shot.Slug;
        }

        /// <summary>A name any disk takes: small letters, digits and underscores, 1 to 48 of them.</summary>
        public static bool FileSafe(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 48)
            {
                return false;
            }
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>The folder of one run, by the computer's clock: "2026-11-12_21-04-33".</summary>
        public static string RunFolder(DateTime now)
        {
            return now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------ the picker

        /// <summary>
        /// Whether a shot is taken: not without its subject, and not a second time in the same light (a twin, when the hour is
        /// left alone). <paramref name="why"/> says why not.
        /// </summary>
        public static bool Take(CineShot shot, bool allLights, bool found, string whyNot, out string why)
        {
            if (!allLights && shot.Twin > 0)
            {
                why = "the same picture as shot " + shot.Twin.ToString("00", CultureInfo.InvariantCulture) + " in one light";
                return false;
            }
            if (!found)
            {
                why = string.IsNullOrEmpty(whyNot) ? SubjectWord(shot.Subject) + ": none found near the player" : whyNot;
                return false;
            }
            why = null;
            return true;
        }

        /// <summary>The whole list decided at once, for subjects known beforehand; returns how many are taken. The run asks <see cref="Take"/> shot by shot.</summary>
        public static int Pick(bool allLights, bool[] found, string[] whyNot, CinePick[] into)
        {
            int taken = 0;
            for (int i = 0; i < Shots.Length && i < into.Length; i++)
            {
                int subject = (int)Shots[i].Subject;
                bool there = found != null && subject < found.Length && found[subject];
                bool take = Take(Shots[i], allLights, there, whyNot != null && subject < whyNot.Length ? whyNot[subject] : null, out string why);
                into[i] = new CinePick { Index = i, Take = take, Why = why };
                taken += take ? 1 : 0;
            }
            return taken;
        }

        /// <summary>How many shots a run tries: all of them, or all but the twins.</summary>
        public static int Planned(bool allLights)
        {
            int count = 0;
            for (int i = 0; i < Shots.Length; i++)
            {
                count += allLights || Shots[i].Twin == 0 ? 1 : 0;
            }
            return count;
        }

        // ------------------------------------------------------------------ where the camera stands

        private static readonly float[] Round = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };

        /// <summary>
        /// How many places a kind of shot is tried from: both sides of a front or the player's shoulder; both sides and both
        /// ends of anything else; every way round a thing seen from the ground; every roof found for a skyline.
        /// </summary>
        public static int Tries(CineStyle style)
        {
            switch (style)
            {
                case CineStyle.Facade:
                case CineStyle.OverShoulder:
                    return 2;
                case CineStyle.LowWide:
                    return Round.Length;
                case CineStyle.Establishing:
                    return Stands;
                default:
                    return 4;
            }
        }

        /// <summary>The side of a try and the degrees its subject is turned for it: sides first, then the other end; a thing seen from the ground is gone round.</summary>
        public static void Try(CineStyle style, int attempt, out int side, out float turn)
        {
            if (style == CineStyle.LowWide)
            {
                side = 1;
                turn = Round[Math.Max(0, Math.Min(Round.Length - 1, attempt))];
                return;
            }
            side = (attempt & 1) == 0 ? 1 : -1;
            turn = attempt >= 2 && style != CineStyle.Facade && style != CineStyle.OverShoulder && style != CineStyle.Establishing ? 180f : 0f;
        }

        /// <summary>A subject seen from another way round: its facing turned, and its length and width with it.</summary>
        public static CineBox Turned(in CineBox box, float degrees)
        {
            CineBox turned = box;
            double t = degrees * Math.PI / 180.0;
            turned.Fx = (float)(box.Fx * Math.Cos(t) + box.Fz * Math.Sin(t));
            turned.Fz = (float)(-box.Fx * Math.Sin(t) + box.Fz * Math.Cos(t));
            float quarter = Math.Abs(degrees) % 180f;
            if (quarter > 67.5f && quarter < 112.5f)
            {
                turned.HalfLength = box.HalfWidth;
                turned.HalfWidth = box.HalfLength;
            }
            else if (quarter > 22.5f && quarter < 157.5f)
            {
                turned.HalfLength = turned.HalfWidth = Math.Max(box.HalfLength, box.HalfWidth);   // seen from a corner
            }
            return turned;
        }

        /// <summary>The share of the wanted distance the camera may be pulled in to before the place is given up.</summary>
        public static float LeastShare(CineStyle style)
        {
            return style == CineStyle.OverShoulder || style == CineStyle.WheelHeight ? 0.75f : 0.55f;
        }

        /// <summary>The tangent of half the frame's width for a vertical field of view.</summary>
        public static float HalfWidthTan(float fov)
        {
            return (float)Math.Tan(Clamp(fov, 1f, 170f) * Math.PI / 360.0) * Aspect;
        }

        /// <summary>Degrees the camera turns after aiming so the subject stands on a third of the frame, not its middle.</summary>
        public static float ThirdsTurn(float fov)
        {
            return (float)(Math.Atan(HalfWidthTan(fov) / 3.0) * 180.0 / Math.PI);
        }

        /// <summary>
        /// The camera for a kind of shot of a subject, on the side asked (1 right of the way it faces, -1 left). Every kind but
        /// the roof's, which needs the roof (<see cref="Establish"/>). The scene is not asked here: Pull and the place's own test are.
        /// </summary>
        public static void Frame(CineStyle style, in CineBox box, float fov, int side, out CinePose pose)
        {
            pose = default(CinePose);
            float s = side < 0 ? -1f : 1f;
            float fx = box.Fx, fz = box.Fz, rx = box.Fz, rz = -box.Fx;   // right of the way it faces, on the flat
            float radius = (float)Math.Sqrt(box.HalfLength * box.HalfLength + box.HalfWidth * box.HalfWidth);
            float halfTan = (float)Math.Tan(Clamp(fov, 1f, 170f) * Math.PI / 360.0);
            float wide = HalfWidthTan(fov);
            float along, across, up, aimAlong = 0f, aimAcross = 0f, aimUp;
            switch (style)
            {
                case CineStyle.ThreeQuarter:
                case CineStyle.RearQuarter:
                {
                    double bearing = (style == CineStyle.ThreeQuarter ? 35.0 : 145.0) * Math.PI / 180.0;
                    float distance = Math.Max(radius / wide / 0.62f, radius + 2.5f);
                    along = (float)Math.Cos(bearing) * distance;
                    across = (float)Math.Sin(bearing) * distance * s;
                    up = style == CineStyle.ThreeQuarter ? Clamp(box.Height * 0.55f, 0.75f, 1.7f) : Clamp(box.Height * 0.45f, 0.6f, 1.5f);
                    aimUp = box.Height * 0.45f;
                    pose.Reach = radius;
                    break;
                }
                case CineStyle.WheelHeight:
                    along = box.HalfLength + 1.7f;
                    across = (box.HalfWidth + 0.9f) * s;
                    up = 0.32f;
                    aimAlong = -box.HalfLength * 0.25f;
                    aimAcross = box.HalfWidth * 0.3f * s;
                    aimUp = 0.6f;
                    pose.Reach = box.HalfWidth;
                    break;
                case CineStyle.OverShoulder:
                    along = -1.15f;
                    across = 0.45f * s;
                    up = box.Height * 0.94f;
                    aimAlong = 16f;
                    aimAcross = -0.6f * s;
                    aimUp = box.Height * 0.7f;
                    pose.NearOnly = true;
                    break;
                case CineStyle.Thirds:
                {
                    double bearing = 28.0 * Math.PI / 180.0;
                    float distance = Math.Max(box.Height * 0.5f / 0.55f / halfTan, 3.5f);
                    along = (float)Math.Cos(bearing) * distance;
                    across = (float)Math.Sin(bearing) * distance * s;
                    up = Math.Min(1.5f, box.Height * 0.8f);
                    aimUp = box.Height * 0.58f;
                    pose.Turn = ThirdsTurn(fov) * s;
                    pose.Reach = 0.5f;
                    break;
                }
                case CineStyle.Symmetry:
                    along = across = 0f;
                    up = 1.55f;
                    aimAlong = 120f;
                    aimUp = 4f;
                    pose.Free = true;
                    break;
                case CineStyle.Compression:
                    along = across = 0f;
                    up = 2.3f;
                    aimAlong = 240f;
                    aimUp = 3f;
                    pose.Free = true;
                    break;
                case CineStyle.LowWide:
                {
                    // Far enough back for the whole of it: its top under the frame's top with sky over it, its foot over the frame's foot.
                    up = 1.05f;
                    aimUp = box.Height * 0.5f;
                    float field = Clamp(fov, 1f, 170f) * (float)Math.PI / 180f;
                    float distance = Math.Max(8f, Math.Max(box.HalfLength, box.HalfWidth) * 0.9f);
                    for (; distance < LowWideMost; distance += 2f)
                    {
                        double aimAt = Math.Atan2(aimUp - up, distance);
                        if (Math.Atan2(box.Height - up, distance) - aimAt <= field * WholeAbove && aimAt + Math.Atan2(up, distance) <= field * WholeBelow)
                        {
                            break;
                        }
                    }
                    along = box.HalfLength + distance;
                    across = distance * 0.25f * s;
                    pose.Reach = box.HalfLength;
                    break;
                }
                default:   // Facade
                {
                    float distance = Math.Max(box.HalfWidth / wide / 0.9f, 6f);
                    along = distance;
                    across = 0f;
                    up = 1.5f;
                    aimUp = Math.Min(box.Height * 0.5f, 2.8f);
                    pose.Reach = 0.3f;
                    break;
                }
            }
            pose.EyeX = box.X + fx * along + rx * across;
            pose.EyeY = box.Y + up;
            pose.EyeZ = box.Z + fz * along + rz * across;
            pose.AimX = box.X + fx * aimAlong + rx * aimAcross;
            pose.AimY = box.Y + aimUp;
            pose.AimZ = box.Z + fz * aimAlong + rz * aimAcross;
            if (style == CineStyle.OverShoulder)
            {
                // The line is tested from the player's head, which is in the open; the far aim may be inside a wall.
                pose.AnchorX = box.X;
                pose.AnchorY = box.Y + box.Height * 0.9f;
                pose.AnchorZ = box.Z;
            }
            else if (pose.Free)
            {
                pose.AnchorX = pose.EyeX;
                pose.AnchorY = pose.EyeY;
                pose.AnchorZ = pose.EyeZ;
            }
            else
            {
                pose.AnchorX = pose.AimX;
                pose.AnchorY = pose.AimY;
                pose.AnchorZ = pose.AimZ;
            }
            if (style == CineStyle.LowWide && along > 0.01f)
            {
                // The aim is the thing's own middle, inside its walls: the line is tested from a metre outside the side the lens is on.
                float dx = pose.EyeX - pose.AimX, dy = pose.EyeY - pose.AimY, dz = pose.EyeZ - pose.AimZ;
                pose.Reach = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz) * box.HalfLength / along + 1f;
            }
            if (style == CineStyle.WheelHeight)
            {
                // The wheel nearest the lens is the sharp thing.
                pose.FocusX = box.X + fx * box.HalfLength * 0.6f + rx * box.HalfWidth * s;
                pose.FocusY = box.Y + 0.35f;
                pose.FocusZ = box.Z + fz * box.HalfLength * 0.6f + rz * box.HalfWidth * s;
                // The aim is inside the car, and the line leaves its body a little before the lens: the body is not what is in the way.
                float dx = pose.EyeX - pose.AimX, dy = pose.EyeY - pose.AimY, dz = pose.EyeZ - pose.AimZ;
                pose.Reach = Math.Max(0f, (float)Math.Sqrt(dx * dx + dy * dy + dz * dz) - WheelClear);
            }
            else
            {
                pose.FocusX = pose.AimX;
                pose.FocusY = pose.AimY;
                pose.FocusZ = pose.AimZ;
            }
        }

        /// <summary>A place tried for a roof: metres behind the subject and to its side, the nearest first.</summary>
        public static void Stand(int index, out float back, out float sideways)
        {
            int ring = index / 4;   // two rings of four
            back = 30f + ring * 35f;
            switch (index % 4)
            {
                case 0: sideways = 0f; break;
                case 1: sideways = 28f; break;
                case 2: sideways = -28f; break;
                default: sideways = 0f; back += 18f; break;
            }
        }

        /// <summary>How good a roof is to stand on, by its height over the street: 0 for one too low or too high, more for a higher one up to the best.</summary>
        public static float RoofWorth(float rise)
        {
            return rise >= RoofLeast && rise <= RoofMost ? Math.Min(rise, RoofBest) : 0f;
        }

        /// <summary>
        /// The camera at a roof's edge (<paramref name="edgeX"/>, <paramref name="roofY"/>, <paramref name="edgeZ"/>), a tripod's
        /// height over it, looking the way the subject faces at the city beyond and a little down.
        /// </summary>
        public static void Establish(in CineBox box, float edgeX, float roofY, float edgeZ, out CinePose pose)
        {
            pose = default(CinePose);
            pose.EyeX = edgeX;
            pose.EyeY = roofY + EyeOverRoof;
            pose.EyeZ = edgeZ;
            pose.AimX = edgeX + box.Fx * 300f;
            pose.AimY = box.Y + (roofY - box.Y) * 0.35f + 6f;
            pose.AimZ = edgeZ + box.Fz * 300f;
            pose.AnchorX = pose.EyeX;
            pose.AnchorY = pose.EyeY;
            pose.AnchorZ = pose.EyeZ;
            pose.FocusX = pose.AimX;
            pose.FocusY = pose.AimY;
            pose.FocusZ = pose.AimZ;
            pose.Free = true;
        }

        /// <summary>
        /// The camera pulled in along its line when something stands in it: <paramref name="hit"/> is the metres from the
        /// line's start to what was met (negative for nothing). False if it would have to come nearer than its share of
        /// the wanted distance, when the picture is no longer the one meant.
        /// </summary>
        public static bool Pull(float wanted, float hit, float margin, float leastShare, out float at)
        {
            at = wanted;
            if (!(wanted > 0f))
            {
                return false;
            }
            if (hit < 0f || hit >= wanted + margin)
            {
                return true;
            }
            at = hit - margin;
            return at >= wanted * leastShare;
        }

        /// <summary>Whether the subject is seen from the camera: nothing met on the line, or only within the subject's own body.</summary>
        public static bool Sees(float hit, float distance, float subjectRadius)
        {
            return hit < 0f || hit >= distance - subjectRadius;
        }

        /// <summary>Whether a point lands inside the frame with a margin, by where the lens puts it (0 to 1 across and up, metres ahead).</summary>
        public static bool InFrame(float x, float y, float ahead)
        {
            return ahead > 0.1f && x > 0.04f && x < 0.96f && y > 0.04f && y < 0.96f;
        }

        // ------------------------------------------------------------------ what is found

        /// <summary>
        /// The point with the most points within a radius of it on the flat, itself counted: its index and how many. False for
        /// fewer than <paramref name="least"/>. At most <see cref="ClusterMost"/> points are looked at, so a press costs 65,000 sums at the most.
        /// </summary>
        public static bool Densest(float[] xs, float[] zs, int count, float radius, int least, out int centre, out int size)
        {
            centre = -1;
            size = 0;
            count = Math.Min(count, Math.Min(ClusterMost, Math.Min(xs != null ? xs.Length : 0, zs != null ? zs.Length : 0)));
            float r2 = radius * radius;
            for (int i = 0; i < count; i++)
            {
                int near = 0;
                for (int k = 0; k < count; k++)
                {
                    float dx = xs[k] - xs[i], dz = zs[k] - zs[i];
                    near += dx * dx + dz * dz <= r2 ? 1 : 0;
                }
                if (near > size)
                {
                    size = near;
                    centre = i;
                }
            }
            return centre >= 0 && size >= least;
        }

        /// <summary>
        /// The way to look down a street: of the clear runs measured round the compass, the longest, with a quarter of the
        /// run behind counted in, so the middle of a long street beats its end. False if none runs <paramref name="least"/> metres.
        /// </summary>
        public static bool Avenue(float[] runs, int bearings, float least, out int best)
        {
            best = -1;
            float bestWorth = 0f;
            bearings = Math.Min(bearings, runs != null ? runs.Length : 0);
            for (int b = 0; b < bearings; b++)
            {
                if (!(runs[b] >= least))
                {
                    continue;
                }
                float behind = bearings % 2 == 0 ? runs[(b + bearings / 2) % bearings] : 0f;
                float worth = Math.Min(runs[b], AvenueMost) + 0.25f * Math.Min(behind, AvenueMost);
                if (worth > bestWorth)
                {
                    bestWorth = worth;
                    best = b;
                }
            }
            return best >= 0;
        }

        // ------------------------------------------------------------------ light

        /// <summary>The sun's height over the horizon in degrees from the turn the game gives its light about the level axis (90 is overhead, past it the sun comes down again).</summary>
        public static float Elevation(float turn)
        {
            return (float)(Math.Asin(Math.Sin(turn * Math.PI / 180.0)) * 180.0 / Math.PI);
        }

        /// <summary>
        /// The hour of the golden light, from the game's own sun by the hour (its strength, its colour temperature and its
        /// turn): in the three hours before the game puts its sun out (<paramref name="sunOff"/>), the hour at which the sun
        /// is warmest while it still has <see cref="GoldenStrong"/> of its strength, the lower sun and then the later hour
        /// where two are as warm. A sun that has all but faded gilds nothing, however low it stands. Three quarters of an
        /// hour before the sun goes where the game's curves cannot be read.
        /// </summary>
        public static float GoldenHour(float sunOff, Func<float, float> sunLux, Func<float, float> sunKelvin, Func<float, float> sunTurn)
        {
            float fallback = ClampHour(sunOff - 0.75f);
            if (sunLux == null)
            {
                return fallback;
            }
            float peak = 0f;
            for (float hour = 6f; hour <= sunOff; hour += 0.25f)
            {
                peak = Math.Max(peak, sunLux(hour));
            }
            if (!(peak > 0f))
            {
                return fallback;
            }
            float best = float.NaN, bestKelvin = float.MaxValue, bestUp = float.MaxValue;
            for (int step = 1; step <= (int)(GoldenBack * 10f); step++)   // from the last tenth before the sun goes, back
            {
                float hour = sunOff - step * 0.1f;
                if (sunLux(hour) < peak * GoldenStrong)
                {
                    continue;
                }
                float kelvin = sunKelvin != null ? sunKelvin(hour) : 6500f;
                float up = sunTurn != null ? Elevation(sunTurn(hour)) : 45f;
                if (kelvin < bestKelvin - 50f || (Math.Abs(kelvin - bestKelvin) <= 50f && up < bestUp - 1f))
                {
                    best = hour;
                    bestKelvin = kelvin;
                    bestUp = up;
                }
            }
            return float.IsNaN(best) ? fallback : ClampHour(best);
        }

        /// <summary>
        /// The four lights' hours: midday; the golden hour; the blue hour a little after both the sun is out and the lamps are
        /// on; night. Each within what the game's own time of day tool can show, and none before the one before it.
        /// </summary>
        public static void Hours(float sunOff, float lampsOn, Func<float, float> sunLux, Func<float, float> sunKelvin, Func<float, float> sunTurn, float[] into)
        {
            float golden = Math.Max(GoldenHour(sunOff, sunLux, sunKelvin, sunTurn), Midday + 1f);
            float blue = Math.Max(ClampHour(Math.Max(sunOff, lampsOn) + BlueAfter), golden);
            float night = Math.Max(ClampHour(Math.Max(Night, blue + 1f)), blue);
            into[(int)CineLight.Midday] = Midday;
            into[(int)CineLight.Golden] = golden;
            into[(int)CineLight.Blue] = blue;
            into[(int)CineLight.Night] = night;
        }

        public static float ClampHour(float hour)
        {
            return Clamp(hour, HourLeast, HourMost);
        }

        /// <summary>An hour as shots.txt writes it: "18:42".</summary>
        public static string Clock(float hour)
        {
            int minutes = (int)Math.Round(Clamp(hour, 0f, 24f) * 60f);
            return (minutes / 60 % 24).ToString("00", CultureInfo.InvariantCulture) + ":" + (minutes % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------ focus

        /// <summary>
        /// The depth of field for a subject a distance off, as HDRP's manual ranges: what is nearer than the near start or
        /// further than the far end is as soft as it gets, and all between the near end and the far start is sharp. With
        /// <paramref name="nearOnly"/> only what is close to the lens is soft.
        /// </summary>
        public static void Focus(float distance, bool nearOnly, out float nearStart, out float nearEnd, out float farStart, out float farEnd)
        {
            distance = Math.Max(0.5f, distance);
            if (nearOnly)
            {
                nearStart = 0.5f;
                nearEnd = 3f;
                farStart = 5000f;
                farEnd = 10000f;
                return;
            }
            nearStart = distance * 0.35f;
            nearEnd = distance * 0.8f;
            farStart = distance * 1.35f + 0.5f;
            farEnd = distance * 3.2f + 4f;
        }

        // ------------------------------------------------------------------ the picture

        /// <summary>True for a frame that is one flat colour, black or not: nothing was drawn into it, and the screen is read in its place.</summary>
        public static bool Flat(int darkest, int brightest)
        {
            return brightest - darkest < 6;
        }

        private static float Clamp(float value, float least, float most)
        {
            return value < least ? least : value > most ? most : value;
        }
    }
}
