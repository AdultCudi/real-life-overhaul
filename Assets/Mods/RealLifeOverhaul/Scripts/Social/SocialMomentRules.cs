using System;

namespace RealLifeOverhaul
{
    /// <summary>What sort of moment it is: it decides what the person remembers it as, and how much a bold or a tender answer is worth.</summary>
    public enum MomentTone { Tense, Sweet, Silly }

    /// <summary>What kind of answer a reaction is. The person's traits and mood weigh each against what they like.</summary>
    public enum MomentStyle { Bold, Kind, Funny, Calm, Generous, Tender, Lively }

    /// <summary>How a reaction landed, from the worst to the best.</summary>
    public enum MomentTier { Sour, Flat, Liked, Loved }

    /// <summary>One of the three things the player can do in a moment: its button, what they say when it lands and when it does not, and what it costs.</summary>
    public sealed class MomentReaction
    {
        public MomentStyle Style;
        public string Label;     // the button
        public string Good;      // their line when it lands (liked or loved)
        public string Flat;      // ...when it does not
        public int Cost;         // money at the start's prices that this reaction spends; 0 for none
    }

    /// <summary>A moment on an outing: a situation, three reactions, and what they text about it the day after.</summary>
    public sealed class MomentDef
    {
        public int Id;           // ten for each outing kind, as a kind's number times ten and its place among the six
        public PlanKind Kind;
        public MomentTone Tone;
        public string Situation;
        public string Echo;
        public MomentReaction[] Reactions;
    }

    /// <summary>How one reaction was weighed: the score, the tier it falls in, and what it moved.</summary>
    public struct MomentResult
    {
        public float Score;
        public float Passion;            // the part of the score their passion gave, positive or negative
        public MomentTier Tier;
        public float Familiarity, Affection, Trust;
        public SocialTrait Led;          // their passion if it weighed in clearly, else none
        public int Clue;                 // points of a clue of the passion this gave
        public bool Pleased => Tier >= MomentTier.Liked;
    }

    /// <summary>
    /// The small moments that interrupt an outing, game-free: when one happens (about one outing in three, seeded, never twice in a
    /// row with the same person), which one, and how each of the three reactions is weighed against the person's traits, passion and
    /// mood. Nothing here is random on the day: the same person, outing and answer always weigh the same. COST: arithmetic, once an
    /// outing and once a press.
    /// </summary>
    public static class SocialMomentRules
    {
        public const int PerKind = 6;               // moments each outing kind has
        public const int ChancePercent = 50;        // the roll, after an outing with none: with never two in a row, about one outing in three
        public const int FirstOuting = 1;           // outings (things shared) a person must have had before the first moment: none on the first
        public const int MomentSalt = 53;           // the stream a moment is rolled on, apart from every other roll of the person
        public const int PickSalt = 61;

        public const float LovedAt = 2.0f, LikedAt = 0.9f, FlatAt = -0.3f;
        public const float EngagedAt = 0.8f;        // a passion's part of a score at which it counts as having weighed in
        public const int ClueEngaged = 2;           // points of a clue a moment their passion weighed in gives

        /// <summary>Set by Wiring/TraitLinks to the option "Moments on outings". Default on.</summary>
        public static Func<bool> OnSwitch = () => true;

        public static bool On => OnSwitch();

        /// <summary>Whether this kind of outing has moments: the eight an outing can be planned for.</summary>
        public static bool HasMoments(PlanKind kind) => kind >= PlanKind.Coffee && kind <= PlanKind.Business;

        /// <summary>
        /// Whether the outing now ending has a moment. Never the first outing with a person (they have to settle first), never straight
        /// after one that had one, and otherwise a roll of <see cref="ChancePercent"/> in a hundred from the person's seed and how many
        /// things they have shared with the player, so it is the same on every machine and no other roll moves.
        /// </summary>
        public static bool Occurs(SocialPerson p, PlanKind kind)
        {
            if (!On || p == null || !p.Met || !HasMoments(kind) || p.Shared < FirstOuting || p.MomentRow)
            {
                return false;
            }
            return SocialPassionRules.Roll100(p.Seed, p.Shared * 13 + (int)kind, MomentSalt) < ChancePercent;
        }

        /// <summary>
        /// Which of the kind's six moments they have: from the person's seed and the outing, never the one they had last. Null when the
        /// catalogue holds none for the kind.
        /// </summary>
        public static MomentDef Pick(SocialPerson p, PlanKind kind)
        {
            MomentDef first = null;
            int n = (int)(((uint)SocialRules.SlotSeed(p.Seed ^ (p.Shared * 31), SocialPassionRules.PassionSalt + PickSalt)) % (uint)PerKind);
            for (int step = 0; step < PerKind; step++)
            {
                MomentDef d = ForKind(kind, (n + step) % PerKind);
                if (d == null)
                {
                    return first;
                }
                first = first ?? d;
                if (d.Id != p.MomentLast)
                {
                    return d;
                }
            }
            return first;
        }

        /// <summary>The kind's <paramref name="n"/>th moment (0 to 5), or null.</summary>
        public static MomentDef ForKind(PlanKind kind, int n)
        {
            int at = (int)kind * PerKind + n;
            return at >= 0 && at < SocialMomentCatalogue.All.Length && SocialMomentCatalogue.All[at].Kind == kind ? SocialMomentCatalogue.All[at] : null;
        }

        /// <summary>The moment with this number, or null.</summary>
        public static MomentDef ById(int id)
        {
            if (id < 0)
            {
                return null;
            }
            MomentDef d = ForKind((PlanKind)(id / 10), id % 10);
            return d != null && d.Id == id ? d : null;
        }

        /// <summary>What they remember it as.</summary>
        public static MemoryCode MemoryOf(MomentTone tone)
        {
            return tone == MomentTone.Sweet ? MemoryCode.MomentSweet : tone == MomentTone.Tense ? MemoryCode.MomentTense : MemoryCode.MomentSilly;
        }

        // What a style is worth in a moment of this tone, before anyone's nature: a tender answer to a sweet moment, a funny one to a silly one.
        private static readonly float[][] Base =
        {
            //            Bold   Kind   Funny  Calm   Generous Tender Lively
            new[] {  0.4f, 0.6f, 0.3f, 0.2f, 0.5f, 0.5f, 0.3f },   // tense
            new[] { -0.3f, 0.5f, 0.4f, 0.2f, 0.5f, 0.9f, 0.6f },   // sweet
            new[] {  0.0f, 0.3f, 0.9f, 0.1f, 0.4f, 0.4f, 0.8f }    // silly
        };

        private static bool Eats(PlanKind kind) => kind == PlanKind.Coffee || kind == PlanKind.Meal || kind == PlanKind.Drinks;

        // How a person's two old traits lean on a style.
        private static float Old(SocialPerson p, MomentStyle s, PlanKind kind)
        {
            float v = 0f;
            if (p.Has(SocialTrait.Supportive))
            {
                v += s == MomentStyle.Kind ? 0.8f : s == MomentStyle.Tender ? 0.3f : 0f;
            }
            if (p.Has(SocialTrait.Envious) && s == MomentStyle.Generous)
            {
                v -= 0.6f;
            }
            if (p.Has(SocialTrait.Careful))
            {
                v += s == MomentStyle.Generous ? -0.7f : s == MomentStyle.Calm ? 0.4f : 0f;
            }
            if (p.Has(SocialTrait.Generous) && s == MomentStyle.Generous)
            {
                v += 0.9f;
            }
            if (p.Has(SocialTrait.Reliable))
            {
                v += s == MomentStyle.Calm ? 0.4f : s == MomentStyle.Bold ? 0.2f : 0f;
            }
            if (p.Has(SocialTrait.Flaky) && (s == MomentStyle.Funny || s == MomentStyle.Lively))
            {
                v += 0.4f;
            }
            if (p.Has(SocialTrait.Sporty))
            {
                v += s == MomentStyle.Lively ? 0.5f : s == MomentStyle.Bold ? 0.3f : 0f;
            }
            if (p.Has(SocialTrait.Foodie) && s == MomentStyle.Generous && Eats(kind))
            {
                v += 0.3f;
            }
            if (p.Has(SocialTrait.NightOwl) && (kind == PlanKind.Drinks || kind == PlanKind.Cinema) && (s == MomentStyle.Funny || s == MomentStyle.Lively))
            {
                v += 0.4f;
            }
            if (p.Has(SocialTrait.Homebody))
            {
                v += s == MomentStyle.Calm ? 0.5f : s == MomentStyle.Lively ? -0.5f : 0f;
            }
            if (p.Has(SocialTrait.Punctual) && s == MomentStyle.Calm)
            {
                v += 0.3f;
            }
            return v;
        }

        // How their passion leans on a style in this outing: the part of the weighing that can give it away.
        private static float PassionLean(SocialTrait passion, MomentStyle s, PlanKind kind, bool inUniform)
        {
            switch (passion)
            {
                case SocialTrait.SocialButterfly:
                    return s == MomentStyle.Lively ? 1.4f : s == MomentStyle.Funny ? 0.6f : s == MomentStyle.Calm ? -0.5f : 0f;
                case SocialTrait.Cinephile:
                    if (kind == PlanKind.Cinema)
                    {
                        return s == MomentStyle.Calm ? 1.6f : s == MomentStyle.Bold ? -1f : s == MomentStyle.Lively ? -0.6f : s == MomentStyle.Tender ? 0.4f : 0f;
                    }
                    return s == MomentStyle.Funny ? 0.3f : 0f;
                case SocialTrait.CareerFocused:
                    return s == MomentStyle.Bold ? 0.7f : s == MomentStyle.Calm ? 0.5f : s == MomentStyle.Tender ? -0.3f : 0f;
                case SocialTrait.GoldDigger:
                    return s == MomentStyle.Generous ? 1.8f : s == MomentStyle.Calm ? -0.3f : s == MomentStyle.Kind ? -0.2f : 0f;
                case SocialTrait.KindSoul:
                    return s == MomentStyle.Kind ? 1.8f : s == MomentStyle.Bold ? -0.8f : s == MomentStyle.Funny ? -0.3f : s == MomentStyle.Generous ? 0.4f : 0f;
                case SocialTrait.Clubber:
                    return s == MomentStyle.Lively ? 1.5f : s == MomentStyle.Funny ? 0.6f : s == MomentStyle.Calm ? -1f : s == MomentStyle.Kind ? -0.1f : 0f;
                case SocialTrait.LovesUniforms:
                    return (inUniform ? 0.8f : 0f) + (s == MomentStyle.Bold ? 0.4f : 0f);
                case SocialTrait.ChildOfEarth:
                    return (s == MomentStyle.Kind ? 0.8f : s == MomentStyle.Calm ? 0.5f : s == MomentStyle.Generous ? -0.5f : 0f) + (kind == PlanKind.Walk && s == MomentStyle.Calm ? 0.5f : 0f);
                case SocialTrait.Romantic:
                    return s == MomentStyle.Tender ? 1.8f : s == MomentStyle.Funny ? 0.4f : s == MomentStyle.Bold ? -0.4f : 0f;
                case SocialTrait.FitnessMinded:
                    return (s == MomentStyle.Lively ? 1.4f : s == MomentStyle.Bold ? 0.3f : s == MomentStyle.Generous ? -0.6f : 0f) + (Eats(kind) && s == MomentStyle.Generous ? -0.8f : 0f);
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// How the reaction <paramref name="reaction"/> of <paramref name="def"/> is weighed for this person: the tone's worth of its style,
        /// their mood (0.3 a step), how close they are, their two old traits, and their passion (only while the option for passions is
        /// on). The score decides the tier and the figures; a passion that leaned on the score clearly is named in <see cref="MomentResult.Led"/>
        /// and gives a clue when it is still hidden. <paramref name="inUniform"/>: the player wears a work uniform.
        /// </summary>
        public static MomentResult Weigh(SocialPerson p, MomentDef def, int reaction, bool inUniform)
        {
            MomentReaction r = def.Reactions[reaction];
            float score = Base[(int)def.Tone][(int)r.Style] + p.Mood * 0.3f + (p.Stage >= SocialStage.Friend ? 0.3f : 0f)
                          + (p.Together && r.Style == MomentStyle.Tender ? 0.4f : 0f) + Old(p, r.Style, def.Kind);
            SocialTrait passion = SocialPassionRules.Of(p);
            float lean = passion == SocialTrait.None ? 0f : PassionLean(passion, r.Style, def.Kind, inUniform);
            score += lean;
            var res = new MomentResult { Score = score, Passion = lean };
            res.Tier = score >= LovedAt ? MomentTier.Loved : score >= LikedAt ? MomentTier.Liked : score >= FlatAt ? MomentTier.Flat : MomentTier.Sour;
            switch (res.Tier)
            {
                case MomentTier.Loved: res.Familiarity = 1.5f; res.Affection = 4f; res.Trust = 1.5f; break;
                case MomentTier.Liked: res.Familiarity = 1f; res.Affection = 2.5f; res.Trust = 1f; break;
                case MomentTier.Flat: res.Familiarity = 0.5f; res.Affection = 0.5f; res.Trust = 0f; break;
                default: res.Familiarity = 0f; res.Affection = -2f; res.Trust = -1f; break;
            }
            if (passion != SocialTrait.None && Math.Abs(lean) >= EngagedAt)
            {
                res.Led = passion;
                res.Clue = p.PassionFound ? 0 : ClueEngaged;
            }
            return res;
        }

        /// <summary>The tier's closing line list.</summary>
        public static string[] Closers(MomentTier tier)
        {
            return tier == MomentTier.Loved ? SocialMomentCatalogue.Loved : tier == MomentTier.Liked ? SocialMomentCatalogue.Liked
                : tier == MomentTier.Flat ? SocialMomentCatalogue.Flat : SocialMomentCatalogue.Sour;
        }
    }
}
