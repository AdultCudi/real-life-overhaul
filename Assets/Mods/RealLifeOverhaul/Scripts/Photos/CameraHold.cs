using System;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// What the camera borrows from the game, in the order it is given back: the selfie's pose first, as it puts back its
    /// own facing and near plane, and the player's walk last, so nothing moves the player before all is back.
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
        World,        // the world's clicks held (the game's mouse controller): its mask's low and high 16 bits, the hover cursor, 1 if the mask was read
        Zoom,         // the game's own zoom held (1 or 0, what it was)
        Walk,         // the player's walk held by the camera's own blocker
        Count
    }

    /// <summary>
    /// The game's state the camera changed, as it was. A part is written once (<see cref="Take"/>), so switching to the
    /// selfie and back never overwrites what the game had, and given back once (<see cref="Give"/>), so every way out puts
    /// back the same things and a second one puts back nothing. Plain numbers only, checked offline; nothing is allocated
    /// after it is made.
    /// </summary>
    public sealed class CameraHold
    {
        public const int Values = 8;

        public static readonly HoldPart[] GiveOrder = MakeOrder();

        private readonly bool[] _held = new bool[(int)HoldPart.Count];
        private readonly float[] _values = new float[(int)HoldPart.Count * Values];

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

        /// <summary>A part as it was: false, and nothing written, if it is already held.</summary>
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

        /// <summary>One of a held part's values; 0 for a part not held.</summary>
        public float Value(HoldPart part, int index)
        {
            return Held(part) && index >= 0 && index < Values ? _values[(int)part * Values + index] : 0f;
        }

        /// <summary>A held part let go, its values copied into <paramref name="into"/>; false for a part not held.</summary>
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

        /// <summary>A part let go without being put back, when what it held is gone with the scene.</summary>
        public void Forget(HoldPart part)
        {
            if (part >= 0 && part < HoldPart.Count)
            {
                _held[(int)part] = false;
            }
        }

        /// <summary>A 32-bit layer mask as two 16-bit halves, since a float holds whole numbers exactly only to 2^24.</summary>
        public static void Split(int mask, out float low, out float high)
        {
            low = mask & 0xFFFF;
            high = (mask >> 16) & 0xFFFF;
        }

        public static int Join(float low, float high)
        {
            return unchecked(((int)high << 16) | ((int)low & 0xFFFF));
        }

        /// <summary>Whether the world's clicks go back: only while the mask is still the camera's own, else the game's stands.</summary>
        public static bool WorldBack(int now, int cameras)
        {
            return now == cameras;
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
