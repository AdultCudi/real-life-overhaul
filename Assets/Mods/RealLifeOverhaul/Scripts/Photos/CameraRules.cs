using System;
using System.Globalization;

namespace RealLifeOverhaul.Photos
{
    /// <summary>Why the camera cannot open now, or why it closed. None: it can.</summary>
    public enum CameraRefusal
    {
        None,
        Off,          // the option is off
        NotLoaded,    // no city yet, or it is loading
        Vehicle,      // in a car, on a bicycle or a motorbike, on a boat
        Transit,      // on the bus, in the subway, in a taxi
        Busy,         // the game holds the player: an activity, a scene of its own, sleep, a hospital, a shift
        Window,       // a window of the game's is open: a question, a panel, a menu
        Custody,      // held by the police, or in prison
        Skipping,     // time is being skipped
        PhotoMode,    // photo mode is on
        Gone          // the player is not there (a scene change, the funeral)
    }

    /// <summary>
    /// The moment as PhotosGame reads it. <see cref="Busy"/> is one of the game's own scenes; <see cref="WalkHeld"/> is any
    /// walk blocker, and <see cref="OwnHold"/> says the only one is the camera's own (7319).
    /// </summary>
    public struct CameraSituation
    {
        public bool FeatureOn, Loaded, PlayerThere, InVehicle, OnTransit, Busy, WalkHeld, OwnHold, Window, Custody, Skipping, PhotoMode;
    }

    /// <summary>A light colour filter of the camera's: a tint and three of the grade's figures (HDRP's colour adjustments).</summary>
    public struct CameraFilter
    {
        public string NameKey;
        public float R, G, B;            // the tint, 0 to 1
        public float Saturation;         // -100 to 100
        public float Contrast;           // -100 to 100
        public float Exposure;           // stops
    }

    /// <summary>The frames the camera can put round a photo (drawn in the viewfinder, burned into the photo on the worker).</summary>
    public enum CameraFrame
    {
        None,
        Polaroid,   // an instant photo's white border, deeper at the foot (the old camera's Border, whose number it keeps)
        Film,       // a film's black bars above and below, 2.39 to 1
        Border      // a thin white border all round
    }

    /// <summary>What the camera is set to take: the rear camera's three looks and the selfie. The number is what a sidecar keeps.</summary>
    public enum CameraKind
    {
        Photo = 0,
        Selfie = 1,
        Portrait = 2,   // the rear camera with the far background blurred
        Wide = 3        // the rear camera's ultra wide lens
    }

    /// <summary>The shape of the photo. Unknown is a photo of the old camera, which kept the screen's own shape.</summary>
    public enum CameraAspect
    {
        Unknown = 0,
        FourThree = 1,
        SixteenNine = 2,
        Square = 3,
        Story = 4       // 9 to 16, for Flare's stories
    }

    /// <summary>The guide drawn over the viewfinder (never in the photo).</summary>
    public enum CameraGuide
    {
        Off,
        Thirds,
        Square      // equal square cells
    }

    /// <summary>
    /// The Camera's game-free rules: when it opens and closes by itself, the zoom, the aim, the filters, the frames and the
    /// grid. Checked offline (EconomyCheck); CameraMode and CameraView apply them.
    /// </summary>
    public static class CameraRules
    {
        public const float BaseFieldOfView = 60f;   // degrees: the rear camera at 1x
        public const float SelfieFieldOfView = 65f; // degrees: as photo mode's selfie
        public const float ZoomLeast = 1f;
        public const float ZoomMost = 5f;           // times, about a phone's digital zoom
        public const float WideLeast = 0.4f;        // times: the ultra wide lens (a field of view of 107 degrees)
        public const float WideMost = 1f;           // times
        public const float PortraitMost = 2.5f;     // times: a portrait lens is not a telescope
        public const int ZoomButtonNotches = 3;     // notches of the wheel a zoom button is worth
        public const int ExposureMost = 20;         // tenths of a stop, either way
        public const int ExposureStep = 5;          // tenths of a stop a press of the exposure buttons
        public const float BoostStops = 1.2f;       // stops the night boost adds
        public const float LevelTolerance = 1f;     // degrees of pitch the level line counts as level
        public const float FocusEvery = 0.2f;       // seconds between two looks for what the portrait focuses on
        public const float FocusNear = 0.8f;        // metres: the nearest the portrait focuses
        public const float FocusFar = 60f;          // metres: the farthest the portrait focuses
        public const float FocusSharp = 1.2f;       // metres behind the subject that stay sharp
        public const float FocusBlurred = 7f;       // metres behind the subject by which the blur is full
        public const int ClassicFilters = 6;        // the filters the old chrome offers; the kit's offers every one
        public const int GuideLinesMost = 8;        // lines of any guide
        public const int CountdownMost = 10;        // seconds of the longest timer
        public const float ZoomNotch = 1.15f;       // zoom factor per notch of the wheel
        public const float PitchLeast = -60f;       // degrees
        public const float PitchMost = 60f;         // degrees
        public const float DragDegrees = 0.09f;     // degrees per pixel of drag at 1080 lines
        public const float SelfieDrag = 0.06f;      // degrees per pixel of drag round the face
        public const float LensForward = 0.32f;     // metres in front of the eyes
        public const float LensBelow = 0.05f;       // metres below the eyes
        public const float NearPlane = 0.12f;       // metres: keeps the hands holding the phone out of the picture
        public const float FlashSeconds = 0.25f;    // seconds
        public const float ToastSeconds = 2.5f;     // seconds
        public const float ReadMost = 50f;          // milliseconds the screen read may take on the main thread (self-check)

        /// <summary>Whether the camera may open now, and if not the first reason that holds.</summary>
        public static CameraRefusal Refuse(CameraSituation s)
        {
            if (!s.FeatureOn)
            {
                return CameraRefusal.Off;
            }
            if (!s.Loaded)
            {
                return CameraRefusal.NotLoaded;
            }
            if (!s.PlayerThere)
            {
                return CameraRefusal.Gone;
            }
            if (s.PhotoMode)
            {
                return CameraRefusal.PhotoMode;
            }
            if (s.Custody)
            {
                return CameraRefusal.Custody;
            }
            if (s.InVehicle)
            {
                return CameraRefusal.Vehicle;
            }
            if (s.OnTransit)
            {
                return CameraRefusal.Transit;
            }
            if (s.Skipping)
            {
                return CameraRefusal.Skipping;
            }
            if (s.Window)
            {
                return CameraRefusal.Window;
            }
            if (s.Busy || (s.WalkHeld && !s.OwnHold))
            {
                return CameraRefusal.Busy;
            }
            return CameraRefusal.None;
        }

        /// <summary>Why the open camera must close now: whatever would refuse it (its own walk blocker is no reason).</summary>
        public static CameraRefusal MustClose(CameraSituation s)
        {
            return Refuse(s);
        }

        /// <summary>True where Flare's own picture works and the Camera's does not, so Flare's button takes its own.</summary>
        public static bool FlareTakesOver(CameraRefusal refusal, bool selfie)
        {
            switch (refusal)
            {
                case CameraRefusal.Busy:
                case CameraRefusal.Custody:
                    return true;
                case CameraRefusal.Vehicle:
                case CameraRefusal.Transit:
                case CameraRefusal.Skipping:
                    return !selfie;
                default:
                    return false;
            }
        }

        /// <summary>The key of a refusal's sentence (a notification), or null for none.</summary>
        public static string RefusalKey(CameraRefusal refusal)
        {
            switch (refusal)
            {
                case CameraRefusal.None: return null;
                case CameraRefusal.Off: return "reallife_phonecam_refuse_off";
                case CameraRefusal.NotLoaded: return "reallife_phonecam_refuse_loading";
                case CameraRefusal.Vehicle: return "reallife_phonecam_refuse_vehicle";
                case CameraRefusal.Transit: return "reallife_phonecam_refuse_transit";
                case CameraRefusal.Busy: return "reallife_phonecam_refuse_busy";
                case CameraRefusal.Window: return "reallife_phonecam_refuse_window";
                case CameraRefusal.Custody: return "reallife_phonecam_refuse_custody";
                case CameraRefusal.Skipping: return "reallife_phonecam_refuse_skipping";
                case CameraRefusal.PhotoMode: return "reallife_phonecam_refuse_photo_mode";
                default: return "reallife_phonecam_refuse_gone";
            }
        }

        /// <summary>The zoom after some notches of the wheel (up is in), kept between 1x and the most.</summary>
        public static float Zoom(float zoom, float notches)
        {
            return Zoom(zoom, notches, ZoomLeast, ZoomMost);
        }

        /// <summary>The zoom after some notches, kept between a lens's least and most; nonsense starts at the least.</summary>
        public static float Zoom(float zoom, float notches, float least, float most)
        {
            if (float.IsNaN(zoom) || zoom < least)
            {
                zoom = least;
            }
            if (notches != 0f && !float.IsNaN(notches))
            {
                zoom *= (float)Math.Pow(ZoomNotch, Math.Max(-10f, Math.Min(10f, notches)));
            }
            return Math.Max(least, Math.Min(most, zoom));
        }

        /// <summary>The least zoom of a kind of shot: the wide lens goes below 1x.</summary>
        public static float ZoomLeastOf(CameraKind kind) => kind == CameraKind.Wide ? WideLeast : ZoomLeast;

        /// <summary>The most zoom of a kind of shot.</summary>
        public static float ZoomMostOf(CameraKind kind) => kind == CameraKind.Wide ? WideMost : kind == CameraKind.Portrait ? PortraitMost : ZoomMost;

        /// <summary>The zoom a kind of shot starts at: the wide lens at its widest, the others at 1x.</summary>
        public static float ZoomStartOf(CameraKind kind) => ZoomLeastOf(kind);

        /// <summary>The three lens buttons of a kind of shot, as zoom factors.</summary>
        public static float[] Lenses(CameraKind kind)
        {
            switch (kind)
            {
                case CameraKind.Wide: return new[] { 0.4f, 0.6f, 1f };
                case CameraKind.Portrait: return new[] { 1f, 1.5f, 2.5f };
                default: return new[] { 1f, 2f, 5f };
            }
        }

        /// <summary>The index of the lens button a zoom is at (within a twentieth), or -1.</summary>
        public static int LensAt(CameraKind kind, float zoom)
        {
            float[] lenses = Lenses(kind);
            for (int i = 0; i < lenses.Length; i++)
            {
                if (Math.Abs(lenses[i] - zoom) < 0.05f)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The next exposure, in tenths of a stop, a step up (+1) or down (-1), kept within the most either way.</summary>
        public static int Exposure(int tenths, int direction)
        {
            int next = tenths + (direction < 0 ? -ExposureStep : direction > 0 ? ExposureStep : 0);
            return Math.Max(-ExposureMost, Math.Min(ExposureMost, next));
        }

        /// <summary>"+0.5", "0.0", "-1.5": the exposure in stops.</summary>
        public static string ExposureWords(int tenths)
        {
            tenths = Math.Max(-ExposureMost, Math.Min(ExposureMost, tenths));
            return (tenths > 0 ? "+" : string.Empty) + (tenths / 10f).ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>The timer after a press: off, 3 seconds, 10 seconds, off.</summary>
        public static int Timer(int seconds)
        {
            return seconds <= 0 ? 3 : seconds < 10 ? 10 : 0;
        }

        /// <summary>The seconds still to count when the timer started at <paramref name="seconds"/> and <paramref name="elapsed"/> have gone; 0 means shoot.</summary>
        public static int Counting(int seconds, float elapsed)
        {
            return Math.Max(0, (int)Math.Ceiling(Math.Min(CountdownMost, seconds) - Math.Max(0f, elapsed)));
        }

        /// <summary>The guide after a press: off, thirds, squares, off.</summary>
        public static CameraGuide Guide(CameraGuide guide)
        {
            return guide == CameraGuide.Off ? CameraGuide.Thirds : guide == CameraGuide.Thirds ? CameraGuide.Square : CameraGuide.Off;
        }

        /// <summary>The guide's lines in a view of this size, as positions from the left (verticals) and from the top (horizontals); returns how many of each.</summary>
        public static void GuideLines(CameraGuide guide, float width, float height, float[] vertical, float[] horizontal, out int verticals, out int horizontals)
        {
            verticals = horizontals = 0;
            if (guide == CameraGuide.Off || width <= 0f || height <= 0f)
            {
                return;
            }
            if (guide == CameraGuide.Thirds)
            {
                vertical[0] = width * GridLines[0];
                vertical[1] = width * GridLines[1];
                horizontal[0] = height * GridLines[0];
                horizontal[1] = height * GridLines[1];
                verticals = horizontals = 2;
                return;
            }
            float cell = height / 3f;   // squares: three rows, and as many columns as the width holds, laid from the middle out
            horizontal[0] = cell;
            horizontal[1] = cell * 2f;
            horizontals = 2;
            for (int k = 1; verticals + 2 <= vertical.Length && width / 2f - k * cell > 1f; k++)
            {
                vertical[verticals++] = width / 2f - k * cell;
                vertical[verticals++] = width / 2f + k * cell;
            }
        }

        /// <summary>A shape as width over height; 16 to 9 for one not known.</summary>
        public static float AspectRatio(CameraAspect aspect)
        {
            switch (aspect)
            {
                case CameraAspect.FourThree: return 4f / 3f;
                case CameraAspect.Square: return 1f;
                case CameraAspect.Story: return 9f / 16f;
                default: return 16f / 9f;
            }
        }

        /// <summary>The next shape of the camera's cycle: 16 to 9, 4 to 3, 1 to 1, 9 to 16, back to 16 to 9.</summary>
        public static CameraAspect NextAspect(CameraAspect aspect)
        {
            switch (aspect)
            {
                case CameraAspect.SixteenNine: return CameraAspect.FourThree;
                case CameraAspect.FourThree: return CameraAspect.Square;
                case CameraAspect.Square: return CameraAspect.Story;
                default: return CameraAspect.SixteenNine;
            }
        }

        public static CameraAspect ClampAspect(int aspect) => aspect >= 1 && aspect <= 4 ? (CameraAspect)aspect : CameraAspect.SixteenNine;

        public static readonly string[] AspectKeys =
        {
            "reallife_phonecam_aspect_unknown", "reallife_phonecam_aspect_43", "reallife_phonecam_aspect_169", "reallife_phonecam_aspect_11", "reallife_phonecam_aspect_916"
        };

        /// <summary>The largest rectangle of a shape, centred in a screen, in whole pixels.</summary>
        public static void CropRect(int screenWidth, int screenHeight, CameraAspect aspect, out int x, out int y, out int width, out int height)
        {
            float ratio = AspectRatio(aspect);
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                x = y = width = height = 0;
                return;
            }
            if ((float)screenWidth / screenHeight >= ratio)
            {
                height = screenHeight;
                width = Math.Min(screenWidth, (int)Math.Round(screenHeight * ratio));
            }
            else
            {
                width = screenWidth;
                height = Math.Min(screenHeight, (int)Math.Round(screenWidth / ratio));
            }
            x = (screenWidth - width) / 2;
            y = (screenHeight - height) / 2;
        }

        /// <summary>
        /// Where the horizon stands in the view, 0 at the top and 1 at the foot, for a pitch (up positive) and the field of view
        /// across the height: a pinhole's. The view's middle at level.
        /// </summary>
        public static float HorizonAt(float pitchDegrees, float fieldOfView)
        {
            double half = Math.Tan(Math.Max(1f, Math.Min(170f, fieldOfView)) * Math.PI / 360.0);
            double at = 0.5 + Math.Tan(Math.Max(-89f, Math.Min(89f, pitchDegrees)) * Math.PI / 180.0) / (2.0 * half);
            return (float)Math.Max(-1.0, Math.Min(2.0, at));
        }

        /// <summary>Whether the camera is level, the horizon in the middle within <see cref="LevelTolerance"/> degrees.</summary>
        public static bool Level(float pitchDegrees) => Math.Abs(pitchDegrees) <= LevelTolerance;

        /// <summary>The far blur of a portrait: where it starts and where it is full, from the metres to the subject; the near is never blurred.</summary>
        public static void Blur(float subject, out float farStart, out float farEnd)
        {
            float d = float.IsNaN(subject) ? FocusFar : Math.Max(FocusNear, Math.Min(FocusFar, subject));
            farStart = d + FocusSharp;
            farEnd = d + FocusBlurred;
        }

        /// <summary>Whether the portrait looks for its subject again: the aim changed, and a fifth of a second has gone since it last looked.</summary>
        public static bool FocusDue(bool changed, float now, float lastLooked)
        {
            return changed && now - lastLooked >= FocusEvery;
        }

        /// <summary>The rear camera's field of view at a zoom: the base narrowed so that the picture is that many times larger.</summary>
        public static float FieldOfView(float zoom)
        {
            zoom = Math.Max(WideLeast, Math.Min(ZoomMost, zoom));
            double half = Math.Atan(Math.Tan(BaseFieldOfView * Math.PI / 360.0) / zoom);
            return (float)(half * 360.0 / Math.PI);
        }

        /// <summary>"0.4x" to "5.0x".</summary>
        public static string ZoomWords(float zoom)
        {
            return Math.Max(WideLeast, Math.Min(ZoomMost, zoom)).ToString("0.0", CultureInfo.InvariantCulture) + "x";
        }

        /// <summary>Turns the rear camera by a drag in pixels, the same part of the view on any screen; the pitch kept in bounds.</summary>
        public static void Aim(ref float yaw, ref float pitch, float dragX, float dragY, float screenHeight, float zoom)
        {
            float scale = DragDegrees * 1080f / Math.Max(240f, screenHeight) / Math.Max(ZoomLeast, Math.Min(ZoomMost, zoom));
            yaw = Wrap(yaw + dragX * scale);
            pitch = Math.Max(PitchLeast, Math.Min(PitchMost, pitch + dragY * scale));
        }

        public const float SeatTurn = 80f;          // degrees either way from a seat's facing

        /// <summary>The rear camera's turn kept within <see cref="SeatTurn"/> of a seat's facing: a seated body does not turn.</summary>
        public static float Seated(float yaw, float seatYaw)
        {
            float off = Wrap(yaw - seatYaw);
            return Wrap(seatYaw + Math.Max(-SeatTurn, Math.Min(SeatTurn, off)));
        }

        /// <summary>Degrees round the face for a drag, for photo mode's selfie (which keeps its own bounds).</summary>
        public static float SelfieTurn(float drag, float screenHeight)
        {
            return drag * SelfieDrag * 1080f / Math.Max(240f, screenHeight);
        }

        /// <summary>An angle as -180 to 180 degrees; 0 for NaN or infinity.</summary>
        public static float Wrap(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
            {
                return 0f;
            }
            degrees %= 360f;
            if (degrees > 180f)
            {
                degrees -= 360f;
            }
            else if (degrees <= -180f)
            {
                degrees += 360f;
            }
            return degrees;
        }

        public static readonly CameraFilter[] Filters =
        {
            new CameraFilter { NameKey = "reallife_phonecam_filter_none", R = 1f, G = 1f, B = 1f },
            new CameraFilter { NameKey = "reallife_phonecam_filter_warm", R = 1f, G = 0.93f, B = 0.82f, Saturation = 8f },
            new CameraFilter { NameKey = "reallife_phonecam_filter_cool", R = 0.85f, G = 0.93f, B = 1f, Saturation = -4f },
            new CameraFilter { NameKey = "reallife_phonecam_filter_mono", R = 1f, G = 1f, B = 1f, Saturation = -100f, Contrast = 15f },
            new CameraFilter { NameKey = "reallife_phonecam_filter_vivid", R = 1f, G = 1f, B = 1f, Saturation = 35f, Contrast = 12f },
            new CameraFilter { NameKey = "reallife_phonecam_filter_fade", R = 0.96f, G = 0.94f, B = 0.90f, Saturation = -28f, Contrast = -20f, Exposure = 0.2f },
            // Added with the camera's rebuild: a number a photo's sidecar keeps, so these come after the first six.
            new CameraFilter { NameKey = "reallife_phonecam_filter_sepia", R = 1f, G = 0.89f, B = 0.74f, Saturation = -60f, Contrast = 8f },
            new CameraFilter { NameKey = "reallife_phonecam_filter_golden", R = 1f, G = 0.88f, B = 0.70f, Saturation = 18f, Contrast = 6f, Exposure = 0.15f },
            new CameraFilter { NameKey = "reallife_phonecam_filter_matte", R = 0.97f, G = 0.97f, B = 1f, Saturation = -12f, Contrast = -35f, Exposure = 0.25f },
            new CameraFilter { NameKey = "reallife_phonecam_filter_chrome", R = 0.92f, G = 0.97f, B = 1f, Saturation = 14f, Contrast = 30f },
            new CameraFilter { NameKey = "reallife_phonecam_filter_dream", R = 1f, G = 0.93f, B = 0.97f, Saturation = -8f, Contrast = -18f, Exposure = 0.45f }
        };

        /// <summary>Whether a filter grades the picture at all (the first does not).</summary>
        public static bool Graded(int filter)
        {
            return filter > 0 && filter < Filters.Length;
        }

        public static int ClampFilter(int filter)
        {
            return filter < 0 || filter >= Filters.Length ? 0 : filter;
        }

        public static readonly string[] FrameKeys = { "reallife_phonecam_frame_none", "reallife_phonecam_frame_polaroid", "reallife_phonecam_frame_film", "reallife_phonecam_frame_border" };

        public const float BorderSide = 0.025f;     // of the photo's height, at the top and the sides of a Polaroid
        public const float BorderFoot = 0.085f;     // of the photo's height, at the foot of a Polaroid
        public const float EdgeSide = 0.012f;       // of the photo's shorter side, all round a thin border
        public const float FilmShape = 2.39f;       // width to height

        /// <summary>The film frame's bar above and below as a part of the screen's height; 0 for a screen as wide as the film.</summary>
        public static float FilmBar(float aspect)
        {
            if (!(aspect > 0f) || aspect >= FilmShape)
            {
                return 0f;
            }
            return (1f - aspect / FilmShape) / 2f;
        }

        public static CameraFrame ClampFrame(int frame)
        {
            return frame < 0 || frame >= FrameKeys.Length ? CameraFrame.None : (CameraFrame)frame;
        }

        /// <summary>The rule of thirds, as parts of the view's width or height.</summary>
        public static readonly float[] GridLines = { 1f / 3f, 2f / 3f };
    }
}
