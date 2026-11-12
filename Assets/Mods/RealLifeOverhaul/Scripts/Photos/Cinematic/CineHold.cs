using System;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// What the cinematic shots borrow from the game, in the order it is given back: the picture's own target first, so the
    /// camera draws to the screen again, then the light, the look and the lens, the camera's place and its brain, the
    /// interface and the pointer, and last the game's speed and the player's walk, so nothing moves before all is back.
    /// </summary>
    public enum CinePart
    {
        Line,         // the progress line's own canvas: taken away
        Target,       // the picture's render target on the camera: off it, the screen again
        Light,        // the game's time of day tool: whether it was on (1 or 0), its forced hour and its shown hour
        Look,         // the depth of field and the vignette (a volume of the tool's own): taken away
        Lens,         // the field of view and the near plane
        Layers,       // the layers the camera draws, as its mask's low and high 16 bits: the markers' layers are left out of a picture
        Place,        // the camera's position (x, y, z) and rotation (x, y, z, w)
        Brain,        // the engine's camera brain: on or off (1 or 0)
        StreetTurn,   // the game's street camera: its angle and distance
        IndoorTurn,   // the game's indoor camera: its angle and distance
        Hud,          // the game's interface shown or hidden (1 or 0)
        Pointer,      // the pointer's lock and whether it showed
        World,        // the world's clicks held: the mask's low and high 16 bits, the hover cursor, 1 if the mask was read
        Zoom,         // the game's own zoom held (1 or 0, what it was)
        Speed,        // the game paused by the tool: whether its time control was locked before (1 or 0)
        Walk,         // the player's walk held by the tool's own blocker
        Count
    }

    /// <summary>
    /// The game's state the shots changed, as it was. A part is written once (<see cref="Take"/>), so a second shot never
    /// overwrites what the game had, and given back once (<see cref="Give"/>), so the run's end, Esc, a fault and the city
    /// unloading all put back the same things and a second one puts back nothing. Plain numbers, checked offline; nothing
    /// is allocated after it is made.
    /// </summary>
    public sealed class CineHold
    {
        public const int Values = 8;

        public static readonly CinePart[] GiveOrder = MakeOrder();

        private readonly bool[] _held = new bool[(int)CinePart.Count];
        private readonly float[] _values = new float[(int)CinePart.Count * Values];

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

        public bool Held(CinePart part)
        {
            return part >= 0 && part < CinePart.Count && _held[(int)part];
        }

        /// <summary>A part as it was: false, and nothing written, if it is already held.</summary>
        public bool Take(CinePart part, float a = 0f, float b = 0f, float c = 0f, float d = 0f, float e = 0f, float f = 0f, float g = 0f, float h = 0f)
        {
            if (part < 0 || part >= CinePart.Count || _held[(int)part])
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
        public float Value(CinePart part, int index)
        {
            return Held(part) && index >= 0 && index < Values ? _values[(int)part * Values + index] : 0f;
        }

        /// <summary>A held part let go, its values copied into <paramref name="into"/>; false for a part not held.</summary>
        public bool Give(CinePart part, float[] into)
        {
            if (!Held(part))
            {
                return false;
            }
            if (into != null)
            {
                Array.Copy(_values, (int)part * Values, into, 0, Math.Min(Values, into.Length));
            }
            _held[(int)part] = false;
            return true;
        }

        /// <summary>Whether every part a shot touches (bits of <see cref="CinePart"/>) is one the hold gives back.</summary>
        public static bool Restores(int touched)
        {
            int given = 0;
            for (int i = 0; i < GiveOrder.Length; i++)
            {
                given |= 1 << (int)GiveOrder[i];
            }
            return touched != 0 && (touched & ~given) == 0;
        }

        private static CinePart[] MakeOrder()
        {
            var order = new CinePart[(int)CinePart.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = (CinePart)i;
            }
            return order;
        }
    }
}
