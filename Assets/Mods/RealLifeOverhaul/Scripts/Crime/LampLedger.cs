using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Whether a crossing's light counts against a person on foot. The game's junction holds its amber until every car has left it,
    /// so a stopped or wrecked car can keep the cars' red lamp dark for minutes; the lamp not lit for longer than a junction's cycle
    /// is taken as the light being stuck, and then the crossing is free (the game's own pedestrians still wait, that is the game's).
    /// </summary>
    internal sealed class LampLedger
    {
        /// <summary>Seconds the cars' red lamp may stay dark before it is taken to be stuck: longer than a junction's green and amber together.</summary>
        public const float StuckAfter = 60f;

        private const int Most = 128;   // lamps watched at once; more than a walk past a few junctions needs

        private readonly Dictionary<int, float> _darkSince = new Dictionary<int, float>(16);

        public int Count => _darkSince.Count;

        /// <summary>A look at a lamp: lit, or dark from now on if it was not already.</summary>
        public void See(int lamp, bool lit, float now)
        {
            if (lit)
            {
                _darkSince.Remove(lamp);
            }
            else if (!_darkSince.ContainsKey(lamp))
            {
                if (_darkSince.Count >= Most)
                {
                    _darkSince.Clear();
                }
                _darkSince[lamp] = now;
            }
        }

        /// <summary>True if the lamp has been seen dark for longer than <see cref="StuckAfter"/>.</summary>
        public bool Stuck(int lamp, float now)
        {
            return _darkSince.TryGetValue(lamp, out float since) && now - since >= StuckAfter;
        }

        /// <summary>May a person walk: the lamp is lit, or it has been dark so long that the light is stuck.</summary>
        public bool MayWalk(int lamp, bool lit, float now)
        {
            return lit || Stuck(lamp, now);
        }

        public void Clear()
        {
            _darkSince.Clear();
        }
    }
}
