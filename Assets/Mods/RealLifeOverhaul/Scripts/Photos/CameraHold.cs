using System;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// What the camera borrows from the game, in the order it is given back (the order of this list is the order of
    /// <see cref="CameraHold.GiveOrder"/>): the selfie's pose first (the body's facing and the camera's near plane are the
    /// selfie's own), then the body's facing the rear camera turned, the grade, the lens, where the camera stood, the
    /// engine's camera brain, the two cameras of the game's whose turn the mouse can move meanwhile, the hidden HUD, the
    /// pointer, the world's clicks, and last the player's walk (so that nothing moves the player before all is back).
    /// </summary>
    public enum HoldPart
    {
        Selfie,       // photo mode's selfie pose (Leisure/Photo/Selfie): it puts back its own
        Facing,       // the body's facing: x, y, z, w of its rotation
        Grade,        // the filter's grade (a volume of the camera's own): nothing to read, it is taken away
        Lens,         // the field of view and the near plane
        Place,        // the camera's position (x, y, z) and rotation (x, y, z, w)
        Brain,        // the engine's camera brain: on or off (1 or 0)
        StreetTurn,   // the game's street camera: its angle and distance
        IndoorTurn,   // the game's indoor camera: its angle and distance
        Hud,          // the game's interface shown or hidden (1 or 0)
        Pointer,      // the pointer's lock and whether it showed
        World,        // the world's clicks held (the game's mouse controller)
        Zoom,         // the game's own zoom held (1 or 0, what it was)
        Walk,         // the player's walk held by the camera's own blocker
        Count
    }

    /// <summary>
    /// THE RESTORE OF THE CAMERA'S STATE, AS A RULE. Everything the camera changes is first written here as it was
    /// (<see cref="Take"/>), once: taking a part that is already held keeps the first values, so that turning from the rear
    /// camera to the selfie and back, or a second shot, never overwrites what the game had. Leaving gives each part held
    /// back exactly once, in <see cref="GiveOrder"/> (<see cref="Give"/> answers true once per part with its values, then
    /// false), so that every way out (the close button, the cancel key, a window opening, the city unloading, the option
    /// switched off, a fault) puts back the same things, and a second way out puts back nothing twice.
    ///
    /// No Unity or game type: the values are plain numbers (a rotation as its four parts, a switch as 1 or 0), so the
    /// offline checks run it as it is (EconomyCheck, PhotoChecks.cs). CameraMode reads the game's values into it and writes
    /// them back. Nothing is allocated after it is made.
    /// </summary>
    public sealed class CameraHold
    {
        public const int Values = 8;

        /// <summary>The order in which the parts are given back.</summary>
        public static readonly HoldPart[] GiveOrder = MakeOrder();

        private readonly bool[] _held = new bool[(int)HoldPart.Count];
        private readonly float[] _values = new float[(int)HoldPart.Count * Values];

        /// <summary>True while any part is held.</summary>
        public bool Any
        {
            get
            {
                for (int i = 0; i < _held.Length; i++)
                {
                    if (_held[i])
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public bool Held(HoldPart part)
        {
            return part >= 0 && part < HoldPart.Count && _held[(int)part];
        }

        /// <summary>A part as it was, written once: true if it is held now for the first time, false (and nothing written) if it already was.</summary>
        public bool Take(HoldPart part, float a = 0f, float b = 0f, float c = 0f, float d = 0f, float e = 0f, float f = 0f, float g = 0f, float h = 0f)
        {
            if (part < 0 || part >= HoldPart.Count || _held[(int)part])
            {
                return false;
            }
            int at = (int)part * Values;
            _values[at] = a;
            _values[at + 1] = b;
            _values[at + 2] = c;
            _values[at + 3] = d;
            _values[at + 4] = e;
            _values[at + 5] = f;
            _values[at + 6] = g;
            _values[at + 7] = h;
            _held[(int)part] = true;
            return true;
        }

        /// <summary>One of a held part's values (0 for a part not held).</summary>
        public float Value(HoldPart part, int index)
        {
            return Held(part) && index >= 0 && index < Values ? _values[(int)part * Values + index] : 0f;
        }

        /// <summary>
        /// A part given back: true once, with its values in <paramref name="into"/> (at least <see cref="Values"/> long), and the
        /// part no longer held; false for a part that is not held (nothing to put back).
        /// </summary>
        public bool Give(HoldPart part, float[] into)
        {
            if (!Held(part))
            {
                return false;
            }
            int at = (int)part * Values;
            if (into != null)
            {
                Array.Copy(_values, at, into, 0, Math.Min(Values, into.Length));
            }
            _held[(int)part] = false;
            return true;
        }

        /// <summary>A part let go without being put back (what it held is gone with the scene).</summary>
        public void Forget(HoldPart part)
        {
            if (part >= 0 && part < HoldPart.Count)
            {
                _held[(int)part] = false;
            }
        }

        private static HoldPart[] MakeOrder()
        {
            var order = new HoldPart[(int)HoldPart.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = (HoldPart)i;
            }
            return order;
        }
    }
}
