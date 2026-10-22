using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Which of the game's cameras it has asked for: the one of its list at priority 1, the only priority the game gives
    /// the camera it wants (CameraHelper.CameraTransition). Only the cameras of ordinary play are named; every other one,
    /// including any a later update adds, is a <see cref="Moment"/>, stood aside for without a list.
    /// </summary>
    public enum GameView : byte
    {
        Moment = 0,     // anything else: placement, the designer, previews, the subway, the minigames, the free camera, the casino boat
        OnFoot = 1,     // GameManager.pedestrianCamera
        Indoor = 2,     // GameManager.indoorCamera
        Driving = 3,    // GameManager.vehicleCamera: the one the chase camera stands in for
        VehicleOther = 4,   // the game's other driving cameras: looking back, and driving inside a building or a car park
        Map = 5,        // GameManager.citymapCamera
    }

    /// <summary>What the game and the player are doing in a frame, as far as this mod's camera cares. Plain values.</summary>
    public struct CameraSignals
    {
        public GameView View;    // the game's own camera mode
        public bool Screen;      // a screen that needs the pointer is up (ThirdPersonCamera.IsUiOpen)
        public bool Talk;        // the game holds the player in a conversation, whose answers are clicked
        public bool Dead;        // the pointer is the game's; the funeral's camera is a moment like any other
        public bool OtherMod;    // another author's mod drives the camera (CameraYield's stand-down)
        public bool InCar;       // a car the chase camera follows (not a scooter or a hand truck)
        public bool CameraOn;    // the option: the third-person camera
        public bool Indoors;     // the option: the third-person camera indoors too
        public bool CarCamera;   // the option: the chase camera
        public bool MouseLook;   // the option: mouse look
        public bool CursorFreed; // Left Alt unless rebound
        public bool Focused;     // the game's window has the focus
        public bool CarMouse;    // the game's own setting: the car is steered with the mouse
        public bool OnBridge;    // the player, or the car they drive, is on a bridge's road (BridgeHold)
        public bool CloseInPicture;   // the chase camera, or the on-foot camera with this mod's figures, is still live, in a blend out too
    }

    /// <summary>What this mod's camera does in the frame. Plain values.</summary>
    public struct CameraOrders
    {
        public bool OutdoorFigures;   // the game's on-foot camera carries this mod's figures (offset, distance, lens, aim)
        public bool IndoorFigures;    // the game's indoor camera likewise
        public bool Chase;            // the chase camera is the one looked through
        public bool Look;             // mouse look on foot: the pointer locked, the mouse orbits
        public bool ChaseLook;        // mouse look in the car
        public bool HoldBridges;      // the bridge under the player is kept out of the game's camera-block hiding
        public bool Moment;           // standing aside: the game's camera is not one of ordinary play
        public bool OutdoorLinger;    // the on-foot camera's figures are given back only once it has left the picture
        public bool IndoorLinger;     // the indoor camera's likewise
    }

    /// <summary>A game camera's figures as plain numbers: what this mod takes from the game's on-foot cameras and gives back.</summary>
    public struct CameraFigures
    {
        public float OffsetX, OffsetY, OffsetZ;
        public float Near, Far;
        public float Distance;
        public float Speed;
        public float Fov;

        public bool Same(in CameraFigures other)
        {
            return OffsetX == other.OffsetX && OffsetY == other.OffsetY && OffsetZ == other.OffsetZ && Near == other.Near && Far == other.Far
                   && Distance == other.Distance && Speed == other.Speed && Fov == other.Fov;
        }
    }

    /// <summary>
    /// The handover of a game camera's figures. The figures are written down as the game has them when this mod takes the
    /// camera (<see cref="Take"/>), once, so nothing of this mod's is ever written down as the game's; <see cref="Give"/>
    /// hands those back, once. Whatever the game or the player changed while the camera was the game's is what the next
    /// take writes down. Nothing allocated.
    /// </summary>
    public struct FigureHandover
    {
        private CameraFigures _game;

        /// <summary>True while the camera carries this mod's figures.</summary>
        public bool Taken { get; private set; }

        /// <summary>The game's figures as they were taken (meaningful while <see cref="Taken"/>).</summary>
        public CameraFigures Game => _game;

        /// <summary>True, with the game's figures written down, if the camera was the game's; false if it was taken already.</summary>
        public bool Take(in CameraFigures now)
        {
            if (Taken)
            {
                return false;
            }
            _game = now;
            Taken = true;
            return true;
        }

        /// <summary>True once, with the figures it was taken with; false if it was not taken.</summary>
        public bool Give(out CameraFigures back)
        {
            back = _game;
            if (!Taken)
            {
                return false;
            }
            Taken = false;
            return true;
        }
    }

    /// <summary>
    /// The one rule every camera path of this mod goes through: when it stands aside for the game's own camera moments, and
    /// what it gives back. <see cref="CameraYield"/> reads the signals once a frame and keeps the orders; ThirdPersonCamera,
    /// ChaseCamera and BridgeHold only follow them. The signals are the game's own (its camera mode, a screen, a
    /// conversation, death), so a moment the game adds later is caught too.
    ///
    /// The figures stay on the game's on-foot and indoor cameras while its camera is one of ordinary play; for any other
    /// they are given back once the camera carrying them has left the picture (so the game's blend starts where its camera
    /// was), and at once when an option goes off or another mod takes the camera. The chase camera runs only while the game
    /// asks for its vehicleCamera. Mouse look needs the figures' camera in the picture, no screen, no conversation and a
    /// living player. The bridge under the player is held while a close camera is looked through, and through its blend out.
    ///
    /// No Unity or game type: checked offline as it ships (NavCheck).
    /// </summary>
    public static class CameraYieldRules
    {
        public const float Copied = 0.001f;   // metres: a distance the game copied from this mod's own is this near to it

        /// <summary>True for the game's cameras of ordinary play: the on-foot and indoor ones, the driving ones, the map.</summary>
        public static bool Play(GameView view)
        {
            return view != GameView.Moment;
        }

        /// <summary>The frame's orders from its signals.</summary>
        public static CameraOrders Decide(in CameraSignals s)
        {
            var o = new CameraOrders();
            bool mine = s.CameraOn && !s.OtherMod;
            bool play = Play(s.View);
            o.OutdoorFigures = mine && play;
            o.IndoorFigures = mine && play && s.Indoors;
            o.Chase = mine && s.CarCamera && s.InCar && s.View == GameView.Driving;
            bool carried = (s.View == GameView.OnFoot && o.OutdoorFigures) || (s.View == GameView.Indoor && o.IndoorFigures);
            bool pointer = s.MouseLook && !s.CursorFreed && s.Focused && !s.Screen && !s.Dead;
            o.Look = carried && pointer && !s.Talk;
            o.ChaseLook = o.Chase && pointer && !s.CarMouse;
            // The look-back camera takes a second to swing away from the deck: the hold lasts through the blend out.
            o.HoldBridges = s.OnBridge && (o.Chase || (s.View == GameView.OnFoot && o.OutdoorFigures) || (mine && s.CloseInPicture));
            o.Moment = mine && !play;
            o.OutdoorLinger = o.Moment;
            o.IndoorLinger = o.Moment && s.Indoors;
            return o;
        }

        /// <summary>
        /// The distance the player wants from the on-foot camera after a frame, within min and max. When
        /// <paramref name="fresh"/>, whatever the game wrote while it had the camera (the interior designer copies its own
        /// distance in) is not the player's: the distance wanted before is kept. Otherwise the change since this mod's last
        /// write is the player's own zoom with the scroll wheel.
        /// </summary>
        public static float Wanted(bool fresh, bool known, bool optionChanged, float option, float desired, float gameNow, float lastWritten, float min, float max)
        {
            if (!known || optionChanged)
            {
                return Clamp(option, min, max);
            }
            if (fresh)
            {
                return Clamp(desired, min, max);
            }
            return Clamp(desired + (gameNow - lastWritten), min, max);
        }

        /// <summary>
        /// What a game camera that copied the on-foot camera's distance as it opened (the placement cameras do) should have:
        /// its own distance if what it copied is this mod's last write, else what it has.
        /// </summary>
        public static float Uncopied(float copied, float lastWritten, float gameOwn)
        {
            return Math.Abs(copied - lastWritten) <= Copied ? gameOwn : copied;
        }

        /// <summary>
        /// Whether the distance is taken afresh: the camera has just been taken, or a moment of the game's ended while the
        /// camera never left the picture (the game wrote its own distance back meanwhile, which is not a zoom).
        /// </summary>
        public static bool Fresh(bool taken, bool momentEnded)
        {
            return taken || momentEnded;
        }

        private static float Clamp(float v, float min, float max)
        {
            return v < min ? min : v > max ? max : v;
        }
    }
}
