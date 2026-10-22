using System;

namespace RealLifeOverhaul.Street
{
    /// <summary>The kind of person in the street: whether they are offered a talk, and how they are stopped and given back.</summary>
    public enum StreetFigure
    {
        Walker,     // one of the game's walking pedestrians: stopped where they stand, turned by the game's own turn, walks on after
        Standing,   // one of the people standing about at a hangout spot: turned to the player, turned back as they stood after
        Seated,     // on a bench or a hangout chair: stays seated as posed (the game's sitting figure has no turn), nothing changed
        Busy        // swimming, golfing, at an exercise machine, a street performer or somebody homeless (the game's own box is theirs): never offered
    }

    /// <summary>
    /// Who is offered a talk: the nearest person roughly in front, straight ahead beating a little nearer at the side.
    /// Game-free. COST: a few multiplications and one square root a person, on the director's tick.
    /// </summary>
    public static class StreetOffer
    {
        /// <summary>Score of somebody at an offset in metres from the player facing (fx, fz): lower is better, below zero out of reach.</summary>
        public static float Score(float dx, float dy, float dz, float fx, float fz, bool current)
        {
            if (dy > StreetRules.Height || dy < -StreetRules.Height)
            {
                return -1f;
            }
            float sqr = dx * dx + dz * dz;
            float reach = current ? StreetRules.LetGo : StreetRules.Reach;
            if (sqr > reach * reach)
            {
                return -1f;
            }
            if (sqr < 0.0001f)
            {
                return 0f;   // on the player's own spot: nobody is nearer
            }
            float cos = Cos(dx, dz, fx, fz);
            if (cos < (current ? StreetRules.KeepFacing : StreetRules.Facing))
            {
                return -1f;
            }
            return sqr * (2f - cos);
        }

        /// <summary>Cosine of the angle off the facing, on the flat (1 straight ahead); -1 without a facing.</summary>
        public static float Cos(float dx, float dz, float fx, float fz)
        {
            float lengths = (float)Math.Sqrt((dx * dx + dz * dz) * (fx * fx + fz * fz));
            return lengths < 0.0001f ? -1f : (dx * fx + dz * fz) / lengths;
        }

        public static int Degrees(float cos)
        {
            return (int)Math.Round(Math.Acos(Math.Max(-1f, Math.Min(1f, cos))) * 180.0 / Math.PI);
        }

        public static bool Offered(StreetFigure kind) => kind != StreetFigure.Busy;

        /// <summary>True if their walking and navigation agent are paused while they talk.</summary>
        public static bool Stops(StreetFigure kind) => kind == StreetFigure.Walker;

        /// <summary>True if the mod turns the figure itself: the game's standing figures have no turn of their own.</summary>
        public static bool TurnsBody(StreetFigure kind) => kind == StreetFigure.Standing;

        public static bool Changes(StreetFigure kind) => Stops(kind) || TurnsBody(kind);

        /// <summary>Metres over their feet the box stands.</summary>
        public static float Lift(StreetFigure kind) => kind == StreetFigure.Seated ? 1.6f : 2.1f;
    }

    /// <summary>
    /// Whom the box is aimed at; a tick or two out of the front within <see cref="StreetRules.Grace"/> changes nothing.
    /// Game-free. COST: a few comparisons on the director's tick.
    /// </summary>
    public sealed class StreetAim
    {
        /// <summary>The person aimed at (their figure's key); 0 nobody.</summary>
        public int Key { get; private set; }

        /// <summary>When they were first and last seen in front (real seconds).</summary>
        public float Since { get; private set; }
        public float Seen { get; private set; }

        private bool _ready;

        /// <summary>One tick with who is in front now (0 nobody); true if the aim moved.</summary>
        public bool Step(int best, bool currentNear, float now)
        {
            if (Key != 0 && best == Key)
            {
                Seen = now;
                return false;
            }
            if (Key != 0 && currentNear && now - Seen <= StreetRules.Grace)
            {
                return false;   // out of the front for a moment, still near: kept
            }
            if (best == Key)
            {
                return false;   // nobody before, nobody now
            }
            Key = best;
            Since = Seen = now;
            _ready = false;
            return true;
        }

        /// <summary>True once they have been in front for <see cref="StreetRules.Dwell"/>, and while the aim stays on them.</summary>
        public bool Ready()
        {
            _ready |= Key != 0 && Seen - Since >= StreetRules.Dwell - 0.001f;
            return _ready;
        }

        /// <summary>True while the one aimed at is kept by the grace.</summary>
        public bool InGrace(float now) => Key != 0 && Seen < now;

        /// <summary>Aims at nobody, with no grace.</summary>
        public void Clear()
        {
            Key = 0;
            _ready = false;
        }
    }
}
