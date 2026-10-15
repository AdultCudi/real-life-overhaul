using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Which of the game's own cameras is the one it has asked for: the one of its list (CameraHelper.Init, GameManager's
    /// start-up) at priority 1, the only priority the game gives the camera it wants (CameraHelper.CameraTransition).
    /// Only the cameras of ordinary play are named; every other camera of the game's, and any it adds in a later update,
    /// is a <see cref="Moment"/>, so a new moment of the game's is stood aside for without anyone adding it to a list.
    /// </summary>
    public enum GameView : byte
    {
        Moment = 0,     // anything else: placement, the interior designer, a building's preview, the subway, the minigames, the free camera, the casino boat, a car's preview, a camera off the game's list (no camera of the list at priority 1)
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
        public bool Talk;        // the game holds the player in a conversation (any of its dialogs), whose answers are clicked
        public bool Dead;        // the player is dead: the pointer is the game's; the funeral's own camera is a moment like any other
        public bool OtherMod;    // another author's mod drives the camera (CameraYield's stand-down)
        public bool InCar;       // the player drives a car the chase camera follows (not a scooter or a hand truck)
        public bool CameraOn;    // the option: the third-person camera
        public bool Indoors;     // the option: the third-person camera indoors too
        public bool CarCamera;   // the option: the chase camera
        public bool MouseLook;   // the option: mouse look
        public bool CursorFreed; // the player freed the pointer (Left Alt unless rebound)
        public bool Focused;     // the game's window has the focus
        public bool CarMouse;    // the game's own setting: the car is steered with the mouse
        public bool OnBridge;    // the player, or the car the player drives, is on a bridge's road (BridgeHold, asked four times a second)
        public bool CloseInPicture;   // this mod's close camera (the chase camera, or the on-foot camera with its figures) is still live, in a blend out too
    }

    /// <summary>What this mod's camera does in the frame. Plain values.</summary>
    public struct CameraOrders
    {
        public bool OutdoorFigures;   // the game's on-foot camera carries this mod's figures (offset, distance, lens, aim)
        public bool IndoorFigures;    // the game's indoor camera likewise
        public bool Chase;            // the chase camera is the one looked through
        public bool Look;             // mouse look on foot (pointer locked, the mouse orbits)
        public bool ChaseLook;        // mouse look in the car
        public bool HoldBridges;      // the bridge the player is on is kept out of the game's camera-block hiding (BridgeHold)
        public bool Moment;           // standing aside for a moment of the game's: its camera is not one of ordinary play
        public bool OutdoorLinger;    // the on-foot camera's figures are given back only once it has left the picture (not live, nor in a blend)
        public bool IndoorLinger;     // the indoor camera's likewise
    }

    /// <summary>
    /// A game camera's figures as plain numbers: its offset's three parts, its nearest and furthest distance, its
    /// distance and the speed it eases to it, and its lens. What this mod takes from the game's on-foot cameras and gives
    /// back.
    /// </summary>
    public struct CameraFigures
    {
        public float OffsetX, OffsetY, OffsetZ;
        public float Near, Far;
        public float Distance;
        public float Speed;
        public float Fov;

        /// <summary>True if every figure is the same as the other's.</summary>
        public bool Same(in CameraFigures other)
        {
            return OffsetX == other.OffsetX && OffsetY == other.OffsetY && OffsetZ == other.OffsetZ && Near == other.Near && Far == other.Far
                   && Distance == other.Distance && Speed == other.Speed && Fov == other.Fov;
        }
    }

    /// <summary>
    /// THE HANDOVER OF A GAME CAMERA'S FIGURES, AS A RULE. The figures are written down as the game has them at the moment
    /// this mod takes the camera (<see cref="Take"/>), once: taking a camera already taken keeps the first figures, so that
    /// nothing of this mod's is ever written down as the game's. Giving it back (<see cref="Give"/>) answers the figures
    /// taken, once. The next take writes down the figures the game has then, so whatever the game or the player changed
    /// while the camera was the game's (a zoom of its own, a distance the game copied in) is what is given back after it.
    /// Two flags and eight numbers; nothing allocated.
    /// </summary>
    public struct FigureHandover
    {
        private CameraFigures _game;

        /// <summary>True while the camera is taken (it carries this mod's figures).</summary>
        public bool Taken { get; private set; }

        /// <summary>The game's figures as they were taken (meaningful while <see cref="Taken"/>).</summary>
        public CameraFigures Game => _game;

        /// <summary>The camera taken: true, and the game's figures written down, if it was the game's; false (nothing written) if it was taken already.</summary>
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

        /// <summary>The camera given back: true once, with the figures it was taken with; false if it was not taken.</summary>
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
    /// When this mod's camera stands aside for the game's own camera moments, and what it gives back: THE ONE RULE every
    /// camera path of this mod goes through (<see cref="CameraYield"/> reads the signals once a frame and keeps the
    /// orders; ThirdPersonCamera, ChaseCamera and BridgeHold only follow them).
    ///
    /// The signals are the game's own: which of its cameras it has asked for (<see cref="GameView"/>), whether a screen
    /// that needs the pointer is up, whether it holds the player in a conversation, and whether the player is dead.
    /// A moment the game adds later is caught by the same signals: a camera that is not one of ordinary play, a screen,
    /// or a blocker on the player's walk.
    ///
    ///   the figures on the game's on-foot and indoor cameras    kept while the game's camera is one of ordinary play
    ///                                                           (on foot, indoors, a car, the map); given back exactly
    ///                                                           as taken for any other camera of the game's, once the
    ///                                                           camera carrying them has left the picture (so the
    ///                                                           game's blend starts where its camera was); at once when
    ///                                                           an option is switched off or another mod takes the
    ///                                                           camera. The player's death changes nothing by itself:
    ///                                                           the funeral's camera is a moment, held through to its cut
    ///   the chase camera                                        only while the game asks for its vehicleCamera
    ///   mouse look on foot                                      only while the camera looked through is the one carrying
    ///                                                           the figures, no screen is up and the game does not hold
    ///                                                           the player in a conversation (the mod's own seats, bus
    ///                                                           rides and activities block the walk too, and keep it);
    ///                                                           none while the player is dead
    ///   the bridge held                                         the one under the player (or the car), while this mod's
    ///                                                           close camera is looked through out of doors (the chase
    ///                                                           camera, or the on-foot camera) and through the blend out
    ///                                                           of it; never a bridge overhead, which the game hides
    ///
    /// No Unity or game type: checked offline as it ships (NavCheck, CameraYieldCheck.cs).
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
            // Only the bridge under the player, only under this mod's close camera, and on through the blend out of it (the
            // game's look-back camera takes a second to swing away from the deck).
            o.HoldBridges = s.OnBridge && (o.Chase || (s.View == GameView.OnFoot && o.OutdoorFigures) || (mine && s.CloseInPicture));
            o.Moment = mine && !play;
            o.OutdoorLinger = o.Moment;
            o.IndoorLinger = o.Moment && s.Indoors;
            return o;
        }

        /// <summary>
        /// The distance the player wants from the on-foot camera after a frame. <paramref name="fresh"/>: the camera has just
        /// been taken (first, or back from a moment of the game's): whatever the game wrote while it had the camera (the
        /// interior designer copies its own distance in) is the game's, not the player's, so the distance wanted before is
        /// kept, or the option's if there was none or the option changed. Otherwise the change since this mod's last write
        /// is the player's own zoom with the game's scroll wheel. Always within <paramref name="min"/> and <paramref name="max"/>.
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
        /// What a camera of the game's that took over should have in place of a distance it copied from the on-foot camera
        /// (the placement camera of the interior designer and of placing an item copies the indoor camera's distance as it
        /// opens): the game's own distance if what it copied is this mod's last write, else what it has (it was not ours).
        /// </summary>
        public static float Uncopied(float copied, float lastWritten, float gameOwn)
        {
            return Math.Abs(copied - lastWritten) <= Copied ? gameOwn : copied;
        }

        /// <summary>
        /// Whether the on-foot camera's distance is taken afresh this frame (<see cref="Wanted"/>): when the camera has just been
        /// taken, and in the first frame after a moment of the game's ended while the camera never left the picture (the
        /// interior designer shut within its blend: the game wrote its own distance back meanwhile, which is not a zoom).
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
