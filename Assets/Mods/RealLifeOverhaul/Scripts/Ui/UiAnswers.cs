using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// One question that many parts answer, each for its own keys (a picture by its key: the city map's lines, other mods' app icons on
    /// the phone, a dialogue's faces, a swatch of colour). A part is added once and gives its answer for its own keys and null for any
    /// other; a question asks the parts in turn, newest first, until one answers. No part holds another's answer or calls it, so parts
    /// added in any order, again in a later city, or again while another stands over them can never wrap each other round in a ring.
    /// The parts were once a chain of answers each holding the one before it: two of them took the one before from a field again each
    /// time they were not on top, the second city of a session closed a ring through the phone's, and the city map's first picture
    /// went round it until the stack ran out and the game closed with no log. A question asked from inside an answer is refused (null),
    /// said once a session. Game-free. Cost: a call per part on a question, on a layout only; nothing allocated once the parts are in.
    /// </summary>
    public sealed class UiAnswers<T> where T : class
    {
        private readonly List<Func<string, T>> _parts;
        private readonly UiReentry _asking = new UiReentry();
        private readonly string _place;

        /// <param name="place">The question's name in the line said at a refusal.</param>
        /// <param name="capacity">The parts expected, so adding them grows nothing.</param>
        public UiAnswers(string place, int capacity)
        {
            _place = place;
            _parts = new List<Func<string, T>>(capacity);
        }

        /// <summary>The parts added.</summary>
        public int Count => _parts.Count;

        /// <summary>Adds a part, asked before every part added earlier. One already in (the same method on the same object) is not added again; false then, or for none.</summary>
        public bool Add(Func<string, T> part)
        {
            if (part == null)
            {
                return false;
            }
            for (int i = 0; i < _parts.Count; i++)
            {
                if (_parts[i].Equals(part))
                {
                    return false;
                }
            }
            _parts.Add(part);
            return true;
        }

        /// <summary>The newest part's answer for the key, or null when no part has one, the key is null, or the question came back from inside an answer.</summary>
        public T Ask(string key)
        {
            if (key == null)
            {
                return null;
            }
            if (!_asking.Enter())
            {
                UiReentry.Refused(_place, key);
                return null;
            }
            try
            {
                for (int i = _parts.Count - 1; i >= 0; i--)
                {
                    T found = _parts[i](key);
                    if (found != null)
                    {
                        return found;
                    }
                }
                return null;
            }
            finally
            {
                _asking.Leave();
            }
        }
    }
}
