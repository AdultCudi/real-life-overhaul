using System;

namespace RealLifeOverhaul
{
    /// <summary>Where a hint is dropped: in a chat on an outing, in a call, or as a text of its own.</summary>
    public enum HintWhere { Outing, Call, Text }

    /// <summary>How the quiz went, by the answers right (0 to 3).</summary>
    public enum QuizVerdict { Disaster, One, Two, Perfect }

    /// <summary>
    /// One quiz about one person: three questions about three different kinds of thing, each with three answers of which one is true. Made once,
    /// from the person's seed, and played in the dialog; never saved.
    /// </summary>
    public sealed class SocialQuiz
    {
        public readonly int[] Kind = new int[SocialFriendsRules.Questions];      // an InterestKind for each question
        public readonly int[] Variant = new int[SocialFriendsRules.Questions];   // which of a kind's two ways of asking
        public readonly int[][] Choice =                                         // the three answers, each an index into the kind's things
        {
            new int[3], new int[3], new int[3]
        };
        public readonly int[] RightAt = new int[SocialFriendsRules.Questions];   // which of the three is the true one
        public readonly bool[] Known = new bool[SocialFriendsRules.Questions];   // the player heard it said: a sure answer, shown as known

        public int Asked;        // questions answered so far
        public int Right;        // ...and how many of them were right
        public float Familiarity, Affection, Trust;   // what the answers gave the person so far, for the card at the end

        public QuizVerdict Verdict => (QuizVerdict)Math.Max(0, Math.Min(3, Right));
        public bool Over => Asked >= SocialFriendsRules.Questions;
    }

    /// <summary>
    /// The game of "meet the friends": what people let slip about themselves (hints), and what the friends ask when they are met (the quiz) and what
    /// it is worth. Every roll is from the person's seed on a stream of its own, so no other roll of the cast moves and the same person gives the
    /// same quiz on every machine. Game-free. COST: arithmetic and one small generator, on a press or a page, never per frame.
    /// </summary>
    public static class SocialFriendsRules
    {
        public const int Questions = 3;                  // questions in a quiz, with three answers each
        public const int IntroGapDays = 10;              // game days before the same person brings friends to meet again
        public const int HintCapAcquaintance = 3;        // kinds an acquaintance lets slip, at most...
        public const int HintCapFriend = 6;              // ...a friend, and anybody closer lets all nine
        public const float OutingChance = 0.30f;         // the chance a chat on an outing holds a hint
        public const float CallChance = 0.22f;           // ...a chat in a call
        public const float TextChance = 0.14f;           // ...a text on a day they would text about something ordinary, as a line of its own

        // What the answers are worth to the person quizzed about, before the stake (Stake): the figures a right one gives and a wrong one takes.
        public const float RightFamiliarity = 1.5f, RightAffection = 3f, RightTrust = 4f;
        public const float WrongFamiliarity = 0.5f, WrongAffection = -2f, WrongTrust = -3f;
        public const float PerfectFamiliarity = 2f, PerfectAffection = 4f, PerfectTrust = 4f;   // three right: on top
        public const float DisasterFamiliarity = 2f, DisasterAffection = 4f;                     // three wrong: they laugh, and warm to you for it

        // What a first standing with their friends adds to the new person's first figures (Standing): by the answers right.
        public const float StandingThreeFamiliarity = 10f, StandingThreeAffection = 10f, StandingThreeTrust = 8f;
        public const float StandingTwoFamiliarity = 5f, StandingTwoAffection = 5f, StandingTwoTrust = 4f;
        public const float StandingNoneAffection = 4f, StandingNoneTrust = -4f;

        private const int PickSalt = 9201;
        private const int QuizSalt = 9301;

        /// <summary>How many kinds a person of this stage has let slip at most: nobody tells a stranger, an acquaintance a few, a friend most, and anybody closer everything.</summary>
        public static int HintCap(SocialStage stage)
        {
            return stage < SocialStage.Acquaintance ? 0 : stage == SocialStage.Acquaintance ? HintCapAcquaintance : stage == SocialStage.Friend ? HintCapFriend : SocialInterests.Kinds;
        }

        /// <summary>The chance a chat holds a hint, by where it is had.</summary>
        public static float Chance(HintWhere where)
        {
            return where == HintWhere.Outing ? OutingChance : where == HintWhere.Call ? CallChance : TextChance;
        }

        /// <summary>
        /// Whether a person may let something slip now: they are met, there is a kind the player has not heard, they have not let one slip
        /// today, and they have not let out all they tell at this stage.
        /// </summary>
        public static bool MayHint(SocialStage stage, int heard, int hintDay, int day)
        {
            return stage >= SocialStage.Acquaintance && hintDay != day && heard != SocialInterests.AllHeard && SocialInterests.Count(heard) < HintCap(stage);
        }

        /// <summary>
        /// Which kind they let slip: one of those not heard yet, by a hash of their seed, the day and how many are heard; -1 when all are. The same
        /// person on the same day with the same things heard says the same.
        /// </summary>
        public static int NextKind(int seed, int heard, int day)
        {
            int left = SocialInterests.Kinds - SocialInterests.Count(heard);
            if (left <= 0)
            {
                return -1;
            }
            int nth = Index(left, day, seed, PickSalt + SocialInterests.Count(heard));
            for (int k = 0; k < SocialInterests.Kinds; k++)
            {
                if (!SocialInterests.Has(heard, k) && nth-- == 0)
                {
                    return k;
                }
            }
            return -1;
        }

        /// <summary>One of <paramref name="length"/> by a hash of the day, the person's seed and a salt: the same on every machine, and another another day.</summary>
        public static int Index(int length, int day, int seed, int salt)
        {
            if (length <= 1)
            {
                return 0;
            }
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)day) * 16777619u;
                h = (h ^ (uint)seed) * 16777619u;
                h = (h ^ (uint)salt) * 16777619u;
                h ^= h >> 13;
                h *= 2246822519u;
                h ^= h >> 15;
                return (int)(h % (uint)length);
            }
        }

        /// <summary>
        /// The quiz for a person: three different kinds, each asked one of two ways, with the true answer and two others of the same kind, the true
        /// one at a place of its own. <paramref name="times"/> is how often the player was quizzed about them before, so the next quiz is another.
        /// A kind the player has heard (<paramref name="heard"/>) is known: a sure answer.
        /// </summary>
        public static SocialQuiz Make(int seed, int heard, int times)
        {
            var quiz = new SocialQuiz();
            var random = new Random(SocialRules.SlotSeed(seed, QuizSalt + Math.Max(0, times)));
            var kinds = new int[SocialInterests.Kinds];
            for (int k = 0; k < kinds.Length; k++)
            {
                kinds[k] = k;
            }
            var mine = new int[SocialInterests.Kinds];
            SocialInterests.Roll(seed, mine);
            var others = new int[SocialInterests.PerKind - 1];
            for (int q = 0; q < Questions; q++)
            {
                int swap = q + random.Next(kinds.Length - q);
                int kind = kinds[swap];
                kinds[swap] = kinds[q];
                kinds[q] = kind;
                quiz.Kind[q] = kind;
                quiz.Variant[q] = random.Next(2);
                quiz.Known[q] = SocialInterests.Has(heard, kind);
                int truth = mine[kind], o = 0;
                for (int v = 0; v < SocialInterests.PerKind; v++)
                {
                    if (v != truth)
                    {
                        others[o++] = v;
                    }
                }
                for (int d = 0; d < 2; d++)
                {
                    int pick = d + random.Next(others.Length - d);
                    int taken = others[pick];
                    others[pick] = others[d];
                    others[d] = taken;
                }
                int at = random.Next(3);
                quiz.RightAt[q] = at;
                int next = 0;
                for (int slot = 0; slot < 3; slot++)
                {
                    quiz.Choice[q][slot] = slot == at ? truth : others[next++];
                }
            }
            return quiz;
        }

        /// <summary>How much is at stake with this person: the closer they are, the more an answer is worth, for or against.</summary>
        public static float Stake(SocialStage stage)
        {
            return stage == SocialStage.CloseFriend ? 1.25f : stage >= SocialStage.Dating ? 1.5f : 1f;
        }

        /// <summary>What one answer is worth to the person quizzed about, at their stage.</summary>
        public static SocialWorth Answered(SocialStage stage, bool right)
        {
            float stake = Stake(stage);
            return right
                ? new SocialWorth { Familiarity = RightFamiliarity * stake, Affection = RightAffection * stake, Trust = RightTrust * stake }
                : new SocialWorth { Familiarity = WrongFamiliarity * stake, Affection = WrongAffection * stake, Trust = WrongTrust * stake };
        }

        /// <summary>
        /// What the whole quiz adds on top of its answers: three right is a feat and gives more; three wrong is a lovely disaster, which they
        /// laugh at, and give back some of what the wrong answers cost. Nothing for one or two.
        /// </summary>
        public static SocialWorth Finished(SocialStage stage, int right)
        {
            float stake = Stake(stage);
            if (right >= Questions)
            {
                return new SocialWorth { Familiarity = PerfectFamiliarity * stake, Affection = PerfectAffection * stake, Trust = PerfectTrust * stake };
            }
            return right <= 0 ? new SocialWorth { Familiarity = DisasterFamiliarity * stake, Affection = DisasterAffection * stake } : new SocialWorth();
        }

        /// <summary>The first standing the friends give a new person who comes to be met, added to what they start with: more for each answer the player got right.</summary>
        public static SocialWorth Standing(int right)
        {
            if (right >= Questions)
            {
                return new SocialWorth { Familiarity = StandingThreeFamiliarity, Affection = StandingThreeAffection, Trust = StandingThreeTrust };
            }
            if (right == 2)
            {
                return new SocialWorth { Familiarity = StandingTwoFamiliarity, Affection = StandingTwoAffection, Trust = StandingTwoTrust };
            }
            return right <= 0 ? new SocialWorth { Affection = StandingNoneAffection, Trust = StandingNoneTrust } : new SocialWorth();
        }

        /// <summary>
        /// Days before this person brings friends to be met again, 0 when they will. A date after today (a save set back) waits the gap at most. A
        /// step is never taken off the menu for it: the player is told how long.
        /// </summary>
        public static int IntroWait(int introDay, int day)
        {
            return introDay <= 0 ? 0 : Math.Min(IntroGapDays, Math.Max(0, IntroGapDays - (day - introDay)));
        }
    }
}
