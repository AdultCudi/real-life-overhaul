using System;

namespace RealLifeOverhaul
{
    /// <summary>How the player got to the place they meet somebody at: what a person who cares about the way a player turns up reads.</summary>
    public enum SocialArrival { Unknown, Foot, Bike, Transit, Car }

    /// <summary>
    /// What the game says of the player when an outing is judged, gathered once an outing by Wiring/TraitLinks and never kept, so the
    /// rules below stay free of the game. A default value says nothing is known: every passion then reads the player as it would an
    /// ordinary day.
    /// </summary>
    public struct SocialFacts
    {
        public bool FitnessKnown;        // Home's fitness is on and read
        public int Fitness;              // 0 to 100
        public int Diet;                 // -1 a poor week of meals, 0 plain, 1 a good one
        public int WealthTier;           // 0 to 5: how far the player's money has come (SocialDirector's tiers)
        public SocialArrival Arrival;
        public bool CarElectric;         // the car at the door runs on a battery
        public bool CarLuxury;           // ...and is one of the game's luxury cars
        public bool InUniform;           // the player wears a work uniform now
        public bool CharityMonth;        // the player gave to a charity this month (Giving)
        public bool Helped;              // the player helped somebody this week (ModState.Helped)
        public bool Rose;                // a promotion in the last ten days, or a business opened in the last fortnight
        public bool AtSight;             // the walk goes by one of the city's sights (Leisure)
        public bool Special;             // today is their birthday, or a year since they became partners
    }

    /// <summary>What a person's passion makes of an outing: how its worth is tilted, and how much of it the player might notice.</summary>
    public struct SocialTilt
    {
        public float AffMul, TrustMul;               // the outing's own affection and trust, times these (1: unchanged)
        public float AffAdd, TrustAdd, FamAdd;       // and then these added
        public SocialTrait Led;                      // the passion that acted (none: it did not)
        public int Clue;                             // points of what the player may notice of it, at most CluePerOuting
        public bool Good;                            // it was to their liking (else it put them off)

        public static SocialTilt None()
        {
            return new SocialTilt { AffMul = 1f, TrustMul = 1f };
        }
    }

    /// <summary>
    /// The ten passions people have besides their two traits, and what each does, game-free. A passion is rolled from the
    /// person's seed on a stream of its own, so it never moves another roll; it acts from the first day, and the player finds it
    /// out by what it does: outings that touch it, texts, the moments on an outing and gifts (clues, <see cref="Bar"/>). Every
    /// number is a small tilt: a passion leans the game, it never breaks it. COST: arithmetic on a press, an outing's end or the
    /// daily round, never per frame.
    /// </summary>
    public static class SocialPassionRules
    {
        /// <summary>The passions, in the order every table of this file and of <see cref="SocialPassionCatalogue"/> follows.</summary>
        public static readonly SocialTrait[] All =
        {
            SocialTrait.SocialButterfly, SocialTrait.Cinephile, SocialTrait.CareerFocused, SocialTrait.GoldDigger, SocialTrait.KindSoul,
            SocialTrait.Clubber, SocialTrait.LovesUniforms, SocialTrait.ChildOfEarth, SocialTrait.Romantic, SocialTrait.FitnessMinded
        };

        public const int Count = 10;

        /// <summary>The money at which each wealth tier of the social life begins (the daily round's, and a gold digger's reading of the player's worth).</summary>
        public static readonly float[] WealthTiers = { 10000f, 50000f, 250000f, 1000000f, 5000000f };

        /// <summary>How many of <see cref="WealthTiers"/> a sum of money has reached, 0 to 5.</summary>
        public static int TierOf(double money)
        {
            int tier = 0;
            while (tier < WealthTiers.Length && money >= WealthTiers[tier])
            {
                tier++;
            }
            return tier;
        }
        public const int PassionSalt = 7919;        // the stream a passion is rolled on, apart from the cast's own and FitSalt's
        public const int CluePerOuting = 3;         // points of a clue one outing can give at most
        public const float CardLeast = 0.9f;        // points of affection at which an outing's card says a passion moved it
        public const int HelpedDays = 7;            // days a good turn of the player's is still warmly remembered by a kind soul
        public const int RiseDays = 10;             // days a career focused person is glad of a promotion
        public const int OpenedDays = 14;           // ...and of a business opened

        /// <summary>
        /// Set by Wiring/TraitLinks to the option "Hidden passions". Off, nobody has a passion that acts or shows: every rule of this
        /// file hands back the ordinary game. Default on, so the checks see the feature unless they switch it.
        /// </summary>
        public static Func<bool> OnSwitch = () => true;

        public static bool On => OnSwitch();

        /// <summary>Whether this person's passion acts: the option is on, and they are met.</summary>
        public static bool Active(SocialPerson p) => p != null && p.Met && On;

        /// <summary>The index of a passion in <see cref="All"/>; -1 for none.</summary>
        public static int IndexOf(SocialTrait passion)
        {
            for (int i = 0; i < Count; i++)
            {
                if (All[i] == passion)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The passion as it acts: none while the option is off or the person is not met.</summary>
        public static SocialTrait Of(SocialPerson p) => Active(p) ? p.Passion : SocialTrait.None;

        // ---------------------------------------------------------------- the roll

        private static readonly int[] Fits = new int[Count];   // the passions that suit, for one roll at a time (the game runs one thread)

        // Mom's: what a mother of the cast can be.
        private static bool FamilyFits(int i) => i == 0 || i == 1 || i == 4 || i == 7;

        /// <summary>Whether an old trait of somebody does not sit with a passion: never a homebody who lives for the club.</summary>
        public static bool Clashes(SocialTrait old, SocialTrait passion)
        {
            if ((old & SocialTrait.Homebody) != 0 && (passion == SocialTrait.Clubber || passion == SocialTrait.SocialButterfly))
            {
                return true;
            }
            if ((old & SocialTrait.Careful) != 0 && passion == SocialTrait.GoldDigger)
            {
                return true;
            }
            if ((old & SocialTrait.Supportive) != 0 && passion == SocialTrait.GoldDigger)
            {
                return true;
            }
            if ((old & SocialTrait.Envious) != 0 && passion == SocialTrait.KindSoul)
            {
                return true;
            }
            return (old & SocialTrait.Flaky) != 0 && passion == SocialTrait.CareerFocused;
        }

        /// <summary>
        /// The passion of somebody whose seed is <paramref name="seed"/>, whose old traits are <paramref name="old"/>: one of the ten,
        /// never one that clashes with them, and for family one that suits a mother. A hash of the seed alone, on
        /// <see cref="PassionSalt"/>'s stream, so it is the same on every machine and in every version, and never takes a draw from
        /// the cast's own generator. A person made before passions existed gets theirs from the same function when their save loads.
        /// </summary>
        public static SocialTrait Roll(int seed, bool family, SocialTrait old)
        {
            int[] fits = Fits;
            int n = 0;
            for (int i = 0; i < Count; i++)
            {
                if ((!family || FamilyFits(i)) && !Clashes(old, All[i]))
                {
                    fits[n++] = i;
                }
            }
            if (n == 0)
            {
                for (int i = 0; i < Count; i++)
                {
                    fits[n++] = i;
                }
            }
            int h = SocialRules.SlotSeed(seed, PassionSalt);
            return All[fits[(int)(((uint)h >> 5) % (uint)n)]];
        }

        // ---------------------------------------------------------------- finding it out

        /// <summary>Points of clues that find a person's passion out: fewer from somebody close, and soonest from a partner.</summary>
        public static int Bar(SocialPerson p)
        {
            if (p.IsFamily || p.Together || p.Stage >= SocialStage.CloseFriend)
            {
                return 2;
            }
            return p.Stage == SocialStage.Friend ? 3 : 4;
        }

        /// <summary>
        /// A clue of their passion noticed on <paramref name="day"/>, <paramref name="points"/> of it. True the once the passion is found by it. Nothing while the
        /// option is off, the person is not met or it is found already.
        /// </summary>
        public static bool AddClue(SocialPerson p, int points, int day)
        {
            if (!Active(p) || p.PassionFound || points <= 0)
            {
                return false;
            }
            p.PassionClues = Math.Min(99, p.PassionClues + Math.Min(points, CluePerOuting));
            if (p.PassionClues < Bar(p))
            {
                return false;
            }
            p.PassionFound = true;
            p.Remember(MemoryCode.PassionFound, day);
            return true;
        }

        /// <summary>Whether a partner or a person dated long enough shows it of themselves, a day of the daily round.</summary>
        public static bool ShowsOfThemselves(SocialPerson p, int day)
        {
            return Active(p) && !p.PassionFound && p.Together && day - p.StageDay >= (p.Stage == SocialStage.Partner ? 7 : 12);
        }

        /// <summary>
        /// Whether they let a hint of it slip in a text today: still hidden, met, not within five days of the last, and a share of the
        /// days by a roll of the person and the day (so the same on every machine for the same day).
        /// </summary>
        public static bool HintDue(SocialPerson p, int day)
        {
            if (!Active(p) || p.PassionFound || p.Stage < SocialStage.Acquaintance || (p.HintDay > 0 && day - p.HintDay < 5))
            {
                return false;
            }
            return Roll100(p.Seed, day, 31) < 40;
        }

        /// <summary>A number 0 to 99 from a person's seed and a day, on a stream of its own for <paramref name="salt"/>.</summary>
        public static int Roll100(int seed, int day, int salt)
        {
            int h = SocialRules.SlotSeed(seed ^ (day * 40503), PassionSalt + salt);
            return (int)((uint)h % 100u);
        }

        /// <summary>Which of a passion's lines (<paramref name="of"/> of them) a person says today: theirs alone, never the same one two days running.</summary>
        public static int Variant(SocialPerson p, int day, int of, int salt)
        {
            return of <= 1 ? 0 : (int)((uint)SocialRules.SlotSeed(p.Seed ^ (day * 7919), PassionSalt + salt) % (uint)of);
        }

        // ---------------------------------------------------------------- what an outing is worth to them

        private static float Cap(float value, float low, float high) => value < low ? low : value > high ? high : value;

        /// <summary>
        /// How their passion tilts an outing: the figures of the outing multiplied and added to, and what the player may notice of it.
        /// Nothing (the outing as it is) while the option is off or the person has no passion in play. <paramref name="pay"/> is how the
        /// bill was settled (SceneTalk: 1 the player paid for both, 2 split, 3 theirs, 0 not answered).
        /// </summary>
        public static SocialTilt Outing(SocialPerson p, PlanKind kind, in SocialFacts f, int pay)
        {
            SocialTilt t = SocialTilt.None();
            SocialTrait passion = Of(p);
            if (passion == SocialTrait.None)
            {
                return t;
            }
            t.Led = passion;
            int clue = 0;
            switch (passion)
            {
                case SocialTrait.SocialButterfly:
                    t.AffMul = kind == PlanKind.Coffee ? 1.6f : kind == PlanKind.Drinks ? 1.5f : kind == PlanKind.Meal ? 1.4f : 1.2f;
                    clue = kind == PlanKind.Coffee || kind == PlanKind.Drinks ? 2 : kind == PlanKind.Meal ? 1 : 0;
                    break;
                case SocialTrait.Cinephile:
                    if (kind == PlanKind.Cinema)
                    {
                        t.AffMul = 1.5f;
                        t.AffAdd = 2f;
                        t.FamAdd = 1f;
                        clue = 2;
                    }
                    break;
                case SocialTrait.CareerFocused:
                    if (f.Rose)
                    {
                        t.AffAdd += 2.5f;
                        clue += 1;
                    }
                    if (kind == PlanKind.Business)
                    {
                        t.AffMul = 1.2f;
                        t.AffAdd += 2f;
                        t.TrustAdd = 0.5f;
                        clue += 2;
                    }
                    break;
                case SocialTrait.GoldDigger:
                    t.AffAdd = (f.WealthTier - 1) * 0.9f;
                    t.TrustMul = 0.5f;
                    if (f.WealthTier >= 3)
                    {
                        clue += 1;
                    }
                    if (f.Arrival == SocialArrival.Car && f.CarLuxury)
                    {
                        t.AffAdd += 3f;
                        clue += 2;
                    }
                    if (pay == 1)
                    {
                        t.AffAdd += 2f;
                        clue += 2;
                    }
                    else if (pay == 2)
                    {
                        t.AffAdd -= 1.2f;
                        clue += 1;
                    }
                    else if (pay == 3)
                    {
                        t.AffAdd -= 2f;
                        clue += 1;
                    }
                    t.AffAdd = Cap(t.AffAdd, -4f, 6f);
                    break;
                case SocialTrait.KindSoul:
                    if (f.CharityMonth)
                    {
                        t.AffAdd += 2.5f;
                        clue += 2;
                    }
                    if (f.Helped)
                    {
                        t.AffAdd += 2f;
                        clue += 2;
                    }
                    if (p.Remembers(MemoryCode.Rude))
                    {
                        t.AffAdd -= 1.5f;
                        clue += 1;
                    }
                    t.AffAdd += kind == PlanKind.Walk ? 0.5f : 0f;
                    break;
                case SocialTrait.Clubber:
                    if (kind == PlanKind.Drinks)
                    {
                        t.AffMul = 2f;
                        clue = 2;
                    }
                    else if (kind == PlanKind.Coffee || kind == PlanKind.Walk)
                    {
                        t.AffAdd = -1.5f;
                        clue = 1;
                    }
                    break;
                case SocialTrait.LovesUniforms:
                    if (f.InUniform)
                    {
                        t.AffAdd = 4f;
                        t.TrustAdd = 1f;
                        clue = 3;
                    }
                    break;
                case SocialTrait.ChildOfEarth:
                    switch (f.Arrival)
                    {
                        case SocialArrival.Foot:
                            t.AffAdd += 1.5f;
                            clue += 2;
                            break;
                        case SocialArrival.Bike:
                            t.AffAdd += 2.5f;
                            clue += 2;
                            break;
                        case SocialArrival.Transit:
                            t.AffAdd += 2f;
                            clue += 2;
                            break;
                        case SocialArrival.Car:
                            if (!f.CarElectric && kind != PlanKind.Drive)
                            {
                                t.AffAdd -= f.CarLuxury ? 4f : 3f;
                                clue += 2;
                            }
                            break;
                    }
                    if (kind == PlanKind.Walk)
                    {
                        t.AffMul = 1.4f;
                        t.AffAdd += f.AtSight ? 1.5f : 0f;
                        clue += 1;
                    }
                    else if (kind == PlanKind.Drive && !f.CarElectric)
                    {
                        t.AffAdd -= 1.5f;
                        clue += 1;
                    }
                    break;
                case SocialTrait.Romantic:
                    if (kind == PlanKind.Walk)
                    {
                        t.AffMul = 1.4f;
                        t.AffAdd += f.AtSight ? 1f : 0f;
                        clue += 2;
                    }
                    else if (kind == PlanKind.Meal)
                    {
                        t.AffAdd += 1.5f;
                        clue += 1;
                    }
                    if (f.Special)
                    {
                        t.AffMul *= 2f;
                        t.AffAdd += 2f;
                        clue += 2;
                    }
                    break;
                case SocialTrait.FitnessMinded:
                    if (kind == PlanKind.Workout)
                    {
                        t.AffMul = 2f;
                        clue += 2;
                    }
                    if (f.FitnessKnown)
                    {
                        if (f.Fitness >= 70)
                        {
                            t.AffAdd += kind == PlanKind.Workout || kind == PlanKind.Walk ? 3f : 2f;
                            clue += 1;
                        }
                        else if (f.Fitness >= 40)
                        {
                            t.AffAdd += 0.5f;
                        }
                        else if (f.Fitness < 30)
                        {
                            t.AffAdd -= 1.5f;
                            clue += 1;
                        }
                    }
                    if (kind == PlanKind.Meal && f.Diet != 0)
                    {
                        t.AffAdd += f.Diet > 0 ? 0.5f : -1f;
                        clue += 1;
                    }
                    break;
            }
            t.Clue = Math.Min(CluePerOuting, clue);
            return t;
        }

        /// <summary>
        /// Whether today is a day a romantic counts double: their birthday (<paramref name="birthdayIn"/> days to it is 0), or a whole number of
        /// years since they and the player became what they are to each other.
        /// </summary>
        public static bool IsSpecialDay(SocialPerson p, int day, int birthdayIn, int daysPerYear)
        {
            if (birthdayIn == 0)
            {
                return true;
            }
            int together = day - p.StageDay;
            return p.Together && daysPerYear > 0 && together > 0 && together % daysPerYear == 0;
        }

        /// <summary>What the outing's affection comes to once tilted, from the figure it had.</summary>
        public static float Tilted(float affection, in SocialTilt t, float quality) => affection * t.AffMul + t.AffAdd * quality;

        /// <summary>The same for trust.</summary>
        public static float TiltedTrust(float trust, in SocialTilt t, float quality) => trust * t.TrustMul + t.TrustAdd * quality;

        /// <summary>
        /// How much more or less they like a kind of outing than <see cref="SocialRules.Liking"/> says, added to it before its bounds: what
        /// they ask for and which they are glad to be asked.
        /// </summary>
        public static float Liking(SocialPerson p, PlanKind kind)
        {
            switch (Of(p))
            {
                case SocialTrait.SocialButterfly:
                    return kind == PlanKind.Coffee || kind == PlanKind.Drinks || kind == PlanKind.Meal ? 0.2f : 0.1f;
                case SocialTrait.Cinephile:
                    return kind == PlanKind.Cinema ? 0.5f : 0f;
                case SocialTrait.CareerFocused:
                    return kind == PlanKind.Business ? 0.4f : 0f;
                case SocialTrait.GoldDigger:
                    return kind == PlanKind.Meal || kind == PlanKind.Drinks ? 0.2f : kind == PlanKind.Walk ? -0.1f : 0f;
                case SocialTrait.KindSoul:
                    return kind == PlanKind.Walk ? 0.1f : 0f;
                case SocialTrait.Clubber:
                    return kind == PlanKind.Drinks ? 0.5f : kind == PlanKind.Coffee || kind == PlanKind.Walk ? -0.3f : 0f;
                case SocialTrait.ChildOfEarth:
                    return kind == PlanKind.Walk ? 0.3f : kind == PlanKind.Drive ? -0.3f : 0f;
                case SocialTrait.Romantic:
                    return kind == PlanKind.Walk || kind == PlanKind.Meal ? 0.2f : 0f;
                case SocialTrait.FitnessMinded:
                    return kind == PlanKind.Workout ? 0.5f : kind == PlanKind.Walk ? 0.2f : 0f;
                default:
                    return 0f;
            }
        }

        /// <summary>What an outing touches of a passion, in clue points, for the hint a card gives: the same table the tilt reads, 0 to 2.</summary>
        public static int Touches(SocialTrait passion, PlanKind kind)
        {
            switch (passion)
            {
                case SocialTrait.SocialButterfly: return kind == PlanKind.Coffee || kind == PlanKind.Drinks ? 2 : 0;
                case SocialTrait.Cinephile: return kind == PlanKind.Cinema ? 2 : 0;
                case SocialTrait.CareerFocused: return kind == PlanKind.Business ? 2 : 0;
                case SocialTrait.Clubber: return kind == PlanKind.Drinks ? 2 : 0;
                case SocialTrait.ChildOfEarth: return kind == PlanKind.Walk ? 1 : 0;
                case SocialTrait.Romantic: return kind == PlanKind.Walk ? 2 : 0;
                case SocialTrait.FitnessMinded: return kind == PlanKind.Workout ? 2 : 0;
                default: return 0;
            }
        }

        // ---------------------------------------------------------------- the rest of life with them

        /// <summary>What a chat or a call is worth to them: a social butterfly takes a talk for much more. Positive figures only.</summary>
        public static SocialWorth Talk(SocialPerson p, SocialWorth worth)
        {
            if (Of(p) == SocialTrait.SocialButterfly)
            {
                worth.Familiarity = worth.Familiarity > 0f ? worth.Familiarity * 1.5f : worth.Familiarity;
                worth.Affection = worth.Affection > 0f ? worth.Affection * 1.5f : worth.Affection;
            }
            return worth;
        }

        /// <summary>
        /// Days without a word before things start to slip, changed by the passion: a social butterfly two sooner (never under two), a
        /// career focused person two later.
        /// </summary>
        public static int Grace(SocialPerson p, int grace)
        {
            switch (Of(p))
            {
                case SocialTrait.SocialButterfly: return Math.Max(2, grace - 2);
                case SocialTrait.CareerFocused: return grace + 2;
                default: return grace;
            }
        }

        /// <summary>Affection more lost each day past the grace: a social butterfly takes being neglected harder.</summary>
        public static float ExtraDecay(SocialPerson p) => Of(p) == SocialTrait.SocialButterfly ? -0.5f : 0f;

        /// <summary>How much they mind a plan changed or missed: half for somebody whose own work moves plans too, as they do.</summary>
        public static float PlanMinds(SocialPerson p) => Of(p) == SocialTrait.CareerFocused ? 0.5f : 1f;

        /// <summary>Whether lateness counts against the player: nobody out clubbing at any hour minds a quarter of an hour.</summary>
        public static bool MindsLate(SocialPerson p) => Of(p) != SocialTrait.Clubber;

        /// <summary>The chance (0 to 1) more of a friend's text being a friend to introduce: a social butterfly brings people along.</summary>
        public static float IntroExtra(SocialPerson p) => Of(p) == SocialTrait.SocialButterfly ? 0.12f : 0f;

        /// <summary>How keen they are to get in touch, added to the circle's weight.</summary>
        public static float Keenness(SocialPerson p)
        {
            SocialTrait passion = Of(p);
            return passion == SocialTrait.SocialButterfly ? 1.5f : passion == SocialTrait.Clubber ? 0.5f : 0f;
        }

        /// <summary>What a party is worth to a guest: a social butterfly takes one for half as much again. Positive figures only.</summary>
        public static float Party(SocialPerson p, float gain) => Of(p) == SocialTrait.SocialButterfly && gain > 0f ? gain * 1.5f : gain;

        /// <summary>
        /// What the money does to them on a day of the daily round: a gold digger cools the day the player's money falls a step
        /// (affection and trust), a little each day the player is broke, and warms a little each day the player is well off.
        /// <paramref name="now"/> and <paramref name="before"/> are the wealth tiers today and at the last round.
        /// </summary>
        public static void Daily(SocialPerson p, int now, int before, out float affection, out float trust)
        {
            affection = 0f;
            trust = 0f;
            if (Of(p) != SocialTrait.GoldDigger || p.Stage < SocialStage.Friend)
            {
                return;
            }
            if (now < before)
            {
                affection -= 3f;
                trust -= 1f;
            }
            if (now == 0)
            {
                affection -= 0.3f;
            }
            else if (now >= 3 && p.Affection < 85f)
            {
                affection += 0.1f;
            }
        }

        /// <summary>
        /// A gift between friends (the one on a birthday): what it lands as, in affection, and a clue point. A romantic is moved by it, a
        /// gold digger finds it small; nobody else is told anything new.
        /// </summary>
        public static float Gift(SocialPerson p, out int clue, out bool lands)
        {
            clue = 0;
            lands = false;
            switch (Of(p))
            {
                case SocialTrait.Romantic:
                    clue = 1;
                    lands = true;
                    return 3f;
                case SocialTrait.GoldDigger:
                    clue = 1;
                    return -1f;
                case SocialTrait.KindSoul:
                    lands = true;
                    return 1f;
                default:
                    return 0f;
            }
        }

        /// <summary>Whether a partner this passion's person keeps their own job however well the player does: the family life asks.</summary>
        public static bool KeepsWorking(SocialPerson p) => Of(p) == SocialTrait.CareerFocused;
    }
}
