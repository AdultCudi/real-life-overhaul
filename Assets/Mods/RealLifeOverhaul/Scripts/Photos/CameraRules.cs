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

    /// <summary>The frames the camera can put round a photo (drawn in the viewfinder and taken with it).</summary>
    public enum CameraFrame
    {
        None,
        Border,     // an instant photo's white border, deeper at the foot
        Film        // a film's black bars above and below, 2.39 to 1
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
        public const float ZoomMost = 5f;
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

        /// <summary>
        /// True where Flare's own picture works and the Camera's does not: Flare's camera button shoots the screen wherever
        /// Flare runs, its selfie button works on foot even while the game or the police hold the player.
        /// </summary>
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
            if (float.IsNaN(zoom) || zoom < ZoomLeast)
            {
                zoom = ZoomLeast;
            }
            if (notches != 0f && !float.IsNaN(notches))
            {
                zoom *= (float)Math.Pow(ZoomNotch, Math.Max(-10f, Math.Min(10f, notches)));
            }
            return Math.Max(ZoomLeast, Math.Min(ZoomMost, zoom));
        }

        /// <summary>The rear camera's field of view at a zoom: the base narrowed so that the picture is that many times larger.</summary>
        public static float FieldOfView(float zoom)
        {
            zoom = Math.Max(ZoomLeast, Math.Min(ZoomMost, zoom));
            double half = Math.Atan(Math.Tan(BaseFieldOfView * Math.PI / 360.0) / zoom);
            return (float)(half * 360.0 / Math.PI);
        }

        /// <summary>"1.0x" to "5.0x".</summary>
        public static string ZoomWords(float zoom)
        {
            return Math.Max(ZoomLeast, Math.Min(ZoomMost, zoom)).ToString("0.0", CultureInfo.InvariantCulture) + "x";
        }

        /// <summary>
        /// The rear camera turned by a drag in pixels: the same part of the view on any screen, less as the zoom narrows it.
        /// The pitch counts up from level and is kept within its bounds.
        /// </summary>
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
            new CameraFilter { NameKey = "reallife_phonecam_filter_vivid", R = 1f, G = 1f, B = 1f, Saturation = 35f, Contrast = 12f }
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

        public static readonly string[] FrameKeys = { "reallife_phonecam_frame_none", "reallife_phonecam_frame_border", "reallife_phonecam_frame_film" };

        public const float BorderSide = 0.025f;     // of the screen's height, at the top and the sides
        public const float BorderFoot = 0.085f;     // of the screen's height, at the foot
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
