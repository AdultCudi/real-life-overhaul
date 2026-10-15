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

    /// <summary>What the camera knows of the moment, read by the game's side (PhotosGame) and judged here.</summary>
    public struct CameraSituation
    {
        public bool FeatureOn, Loaded, PlayerThere, InVehicle, OnTransit, Busy, Window, Custody, Skipping, PhotoMode;
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
    /// The Camera's rules in plain numbers: when it opens and when it closes by itself, the zoom, the aim, the filters, the
    /// frames and the grid. No Unity or game type, so every rule is run as it is by the offline checks (EconomyCheck,
    /// PhotoChecks.cs); CameraMode and CameraView do what these say.
    ///
    /// WHERE IT WORKS. On foot, out of doors and indoors alike (the rear camera looks from where the phone is held; the
    /// selfie turns the lens to the player, who holds the phone out as photo mode's selfie does). Refused, with the reason
    /// in a notice: in any vehicle (car, bicycle, motorbike, boat), on the bus, the subway or in a taxi, while the game holds
    /// the player (an activity, a scene of the game's own, a shift, a hospital, the funeral), while a window of the game's is
    /// open (a question, a conversation's panel), in custody or prison, while time is skipped, and while photo mode is on.
    /// Once open, the camera closes by itself, everything put back, when any of those comes about (a window opens, a question
    /// is asked, a scene starts), when the phone, the map or the game's menu is opened, and when the city unloads.
    /// </summary>
    public static class CameraRules
    {
        public const float BaseFieldOfView = 60f;   // degrees: the rear camera at 1x, about a phone's main lens
        public const float SelfieFieldOfView = 65f; // degrees: a phone's front camera (as photo mode's selfie)
        public const float ZoomLeast = 1f;
        public const float ZoomMost = 5f;           // a phone's digital zoom, about
        public const float ZoomNotch = 1.15f;       // the zoom one notch of the wheel gives
        public const float PitchLeast = -60f;       // degrees the rear camera looks down at most...
        public const float PitchMost = 60f;         // ...and up
        public const float DragDegrees = 0.09f;     // degrees of turn for a pixel of drag at 1080 lines (a sweep of the screen is about a half turn)
        public const float SelfieDrag = 0.06f;      // ...and of the selfie's arc round the face (photo mode's selfie takes degrees)
        public const float LensForward = 0.32f;     // metres in front of the eyes the phone is held for the rear camera...
        public const float LensBelow = 0.05f;       // ...and below them
        public const float NearPlane = 0.12f;       // metres: the hands holding the phone are nearer than this and are not drawn
        public const float FlashSeconds = 0.25f;    // the white of the shutter, fading
        public const float ToastSeconds = 2.5f;     // a line in the viewfinder ("saved") stays this long
        public const float ReadMost = 50f;          // milliseconds the one read of the screen on the main thread is wanted under (the self-check)

        // ---------------------------------------------------------------- opening and closing

        /// <summary>Whether the camera may open now, and if not why (the first reason that holds, in this order).</summary>
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
            if (s.Busy)
            {
                return CameraRefusal.Busy;
            }
            return CameraRefusal.None;
        }

        /// <summary>
        /// Whether the camera, open, must close now, and why: everything that refuses it, but "busy", which the camera's own
        /// hold on the player's walk makes true (it is read before the camera holds it, and the game's own reasons for it
        /// come with a window or a scene, which are read).
        /// </summary>
        public static CameraRefusal MustClose(CameraSituation s)
        {
            s.Busy = false;
            return Refuse(s);
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

        // ---------------------------------------------------------------- the zoom and the aim

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

        /// <summary>The zoom as the viewfinder writes it: "1.0x" to "5.0x".</summary>
        public static string ZoomWords(float zoom)
        {
            return Math.Max(ZoomLeast, Math.Min(ZoomMost, zoom)).ToString("0.0", CultureInfo.InvariantCulture) + "x";
        }

        /// <summary>
        /// The rear camera turned by a drag (pixels, at a screen of <paramref name="screenHeight"/> lines; right and up look right
        /// and up, the pitch counted up from level): the turn is the same
        /// part of the view whatever the screen, smaller as the zoom narrows it; the pitch kept within its bounds.
        /// </summary>
        public static void Aim(ref float yaw, ref float pitch, float dragX, float dragY, float screenHeight, float zoom)
        {
            float scale = DragDegrees * 1080f / Math.Max(240f, screenHeight) / Math.Max(ZoomLeast, Math.Min(ZoomMost, zoom));
            yaw = Wrap(yaw + dragX * scale);
            pitch = Math.Max(PitchLeast, Math.Min(PitchMost, pitch + dragY * scale));
        }

        public const float SeatTurn = 80f;          // degrees either way the rear camera turns from a seat's facing (the body stays as the seat has it)

        /// <summary>The rear camera's turn kept within <see cref="SeatTurn"/> of a seat's facing: seated, the body does not turn with it.</summary>
        public static float Seated(float yaw, float seatYaw)
        {
            float off = Wrap(yaw - seatYaw);
            return Wrap(seatYaw + Math.Max(-SeatTurn, Math.Min(SeatTurn, off)));
        }

        /// <summary>The selfie's arc round the face moved by a drag: degrees for photo mode's selfie (its own bounds hold it).</summary>
        public static float SelfieTurn(float drag, float screenHeight)
        {
            return drag * SelfieDrag * 1080f / Math.Max(240f, screenHeight);
        }

        /// <summary>An angle as -180 to 180 degrees.</summary>
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

        // ---------------------------------------------------------------- the filters

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

        /// <summary>A filter asked for, kept among those there are.</summary>
        public static int ClampFilter(int filter)
        {
            return filter < 0 || filter >= Filters.Length ? 0 : filter;
        }

        // ---------------------------------------------------------------- the frames

        public static readonly string[] FrameKeys = { "reallife_phonecam_frame_none", "reallife_phonecam_frame_border", "reallife_phonecam_frame_film" };

        public const float BorderSide = 0.025f;     // of the screen's height: the white border at the top and the sides...
        public const float BorderFoot = 0.085f;     // ...and at the foot, where an instant photo is held
        public const float FilmShape = 2.39f;       // the film frame's width to its height

        /// <summary>
        /// The film frame's bar above and below, as a part of the screen's height, for a screen of this width to height: what is
        /// left between them is 2.39 to 1. None for a screen as wide as that or wider.
        /// </summary>
        public static float FilmBar(float aspect)
        {
            if (!(aspect > 0f) || aspect >= FilmShape)
            {
                return 0f;
            }
            return (1f - aspect / FilmShape) / 2f;
        }

        /// <summary>A frame asked for, kept among those there are.</summary>
        public static CameraFrame ClampFrame(int frame)
        {
            return frame < 0 || frame >= FrameKeys.Length ? CameraFrame.None : (CameraFrame)frame;
        }

        // ---------------------------------------------------------------- the grid

        /// <summary>The rule of thirds: where the lines stand, as parts of the width (or the height) of the view.</summary>
        public static readonly float[] GridLines = { 1f / 3f, 2f / 3f };
    }
}
