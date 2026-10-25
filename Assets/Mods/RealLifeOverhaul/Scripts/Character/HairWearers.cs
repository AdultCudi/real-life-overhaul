using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Whose strand hair a drawn renderer is, for the hair finish's search behind the black screen (CharacterQuality).
    /// The engine finds a kind of its own (a skinned mesh) from a list of that kind, a fraction of a millisecond, where a
    /// kind of the game's scripts (the game's characters) is found only by going through every object in memory, some
    /// 80 to 120 ms in the city. So the search goes from the drawn hair to its character: the nearest character above the
    /// renderer, if it wears it; else the character a whole search found wearing it, kept here by the renderer's id; else
    /// the renderer is new to this and the caller makes the whole search once (<see cref="Learn"/>). A renderer no
    /// character wears (a wig on a stand) is kept as nobody's, so it costs one whole search, not one at every door.
    ///
    /// <typeparamref name="T"/> is the character; the caller hands over how to ask whether one is still there and wears a
    /// renderer, which is the only thing that reads the engine. Game-free, checked by NavCheck (HairWearersCheck).
    /// Cost: a dictionary lookup and one question per drawn renderer of strand hair; nothing per frame.
    /// </summary>
    internal sealed class HairWearers<T> where T : class
    {
        public const int Most = 512;   // renderers kept; past it all are let go and the next whole search learns them again

        private readonly Dictionary<int, T> _known = new Dictionary<int, T>(32);
        private readonly Func<T, int, bool> _wears;

        /// <summary><paramref name="wears"/>: the character is still there and the renderer of that id is one its hair can be on.</summary>
        public HairWearers(Func<T, int, bool> wears)
        {
            _wears = wears ?? throw new ArgumentNullException(nameof(wears));
        }

        /// <summary>Renderers whose wearer, or nobody, a whole search found.</summary>
        public int Count => _known.Count;

        /// <summary>
        /// Who wears the drawn renderer <paramref name="renderer"/>: <paramref name="nearest"/> (the nearest character
        /// above it, or null) if it does; else the one learned; null with <paramref name="unknown"/> false if it was
        /// learned to be nobody's; null with <paramref name="unknown"/> true if only a whole search can say.
        /// </summary>
        public T Wearer(int renderer, T nearest, out bool unknown)
        {
            unknown = false;
            if (nearest != null && _wears(nearest, renderer))
            {
                return nearest;
            }
            if (_known.TryGetValue(renderer, out T wearer))
            {
                if (wearer == null)
                {
                    return null;   // a whole search found that no character wears it
                }
                if (_wears(wearer, renderer))
                {
                    return wearer;
                }
                _known.Remove(renderer);   // gone, or it wears other pieces now: what it was is no answer
            }
            unknown = true;
            return null;
        }

        /// <summary>What a whole search found: <paramref name="wearer"/> wears the renderer, or null for nobody.</summary>
        public void Learn(int renderer, T wearer)
        {
            if (_known.Count >= Most && !_known.ContainsKey(renderer))
            {
                _known.Clear();   // mostly renderers gone with their characters by now
            }
            _known[renderer] = wearer;
        }

        /// <summary>A new scene, or the finish stopped: nothing learned is kept.</summary>
        public void Clear()
        {
            _known.Clear();
        }
    }
}
