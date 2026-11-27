using System;

namespace RealLifeOverhaul
{
    /// <summary>What decides whether somebody who minds notices a public outing: where it was, when, who else was about, and who is looking.</summary>
    public struct CatchFacts
    {
        public PlanKind Kind;
        public bool SameDistrict;     // the place is in the neighbourhood the watcher lives in
        public bool Free;             // the watcher is free at this hour: out and about
        public int Mutual;            // friends of both who could be there, 0 to 3
        public bool Tagged;           // a post of the player's on Flare tagged the person they are with, lately
        public int Close;             // two plans in a day: 0 none, 1 the same day, 2 near in time, 3 near in time and place (<see cref="LoveRules.Closeness"/>)
        public bool Envious, Careful, Punctual;   // the watcher's traits
        public int Suspicion;         // how suspicious the watcher already is, 0 to 100
        public bool Around;           // a night got around and the word has not died down
        public bool Denied;           // a denial held: they would rather believe it
        public bool LivesTogether;    // the watcher shares the player's home
        public int Hour;              // the hour the outing began
    }

    /// <summary>What a night is judged on: the person, the evening and what the player has had to drink.</summary>
    public struct NightFacts
    {
        public NightKind Kind;
        public bool WentWell;         // the outing or the party went well
        public float Affection, Trust;
        public SocialStage Stage;
        public int Mood;              // -2 to 2
        public int Hour;
        public float Drinks;          // drinks in the blood
        public bool Taken;            // the player is with somebody
        public bool NightOwl, Flaky, Careful, Reliable, Homebody, Supportive, Envious, Generous;
        public bool Together;         // the person is the player's partner already
    }

    /// <summary>What a child of a night is judged on.</summary>
    public struct ConceiveFacts
    {
        public bool Surprise;         // the option
        public bool Children;         // Family's children option
        public bool SexesDiffer;      // one is a woman and the other a man
        public int CarrierAge;        // the one who would carry
        public int Room;              // children the family can still take (Family's most less those it holds and those on the way)
        public float Drinks;
        public bool Careful, Reliable, Flaky;
        public NightOutcome Outcome;
    }

    /// <summary>How a confrontation went, with the figures that moved for whoever found out.</summary>
    public struct LoveResult
    {
        public bool Stays;            // the one who asked stays with the player
        public bool Held;             // a denial held, nothing else changed
        public float Affection, Trust;
        public bool Double;           // it cost twice for a lie or a second chance already used
    }

    /// <summary>What a scandal comes to for how known the player is.</summary>
    public struct ScandalScale
    {
        public int Headline;          // per cent chance the City Ledger prints it
        public int FlareLost;         // per mille of followers lost
        public int FlareGained;       // per mille of followers a scandal draws
        public int Dent;              // points of business standing, fading by itself
        public bool Gossip;           // friends talk of it
    }

    /// <summary>
    /// The second relationship, nights and what comes of them: the chance of being found out and what raises it, what each answer to
    /// "who is {other}?" is worth, when a night is offered and how it turns out, when a child comes of one and what its support
    /// is, how a scandal scales with fame. Every roll is seeded (<see cref="Roll"/>) so the same cast and day give the same answer
    /// on every machine and a roll of this feature moves nobody else's. Game-free (EconomyCheck, LoveChecks.cs). COST: a few
    /// comparisons on an outing, an hour or a day.
    /// </summary>
    public static class LoveRules
    {
        // ---------------------------------------------------------------- rolls

        public const int SaltCatch = 7101, SaltForgive = 7102, SaltDeny = 7103, SaltLearn = 7104, SaltOffer = 7105, SaltYes = 7106, SaltOutcome = 7107,
            SaltChild = 7108, SaltBoy = 7109, SaltMishap = 7110, SaltScandal = 7111, SaltLine = 7112, SaltSecretOut = 7113, SaltVariant = 7114;

        /// <summary>The seed of everything this feature rolls for a cast: its own, so no other roll of the social life moves.</summary>
        public static int SeedOf(int castSeed)
        {
            unchecked
            {
                uint h = (uint)castSeed * 2246822519u ^ 0x7A3Bu;
                h ^= h >> 15;
                h *= 3266489917u;
                h ^= h >> 13;
                return (int)(h & 0x7FFFFFFF) | 1;
            }
        }

        /// <summary>0 to 999, always the same for the same four numbers.</summary>
        public static int Roll(int seed, int a, int b, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)a * 2246822519u ^ (uint)b * 3266489917u ^ (uint)salt * 668265263u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (int)(h % 1000u);
            }
        }

        /// <summary>A pick among <paramref name="count"/> variants for this moment: the same for the same numbers, so the same moment never reads the same twice in a week.</summary>
        public static int Variant(int seed, int a, int b, int count)
        {
            return count <= 1 ? 0 : Roll(seed, a, b, SaltVariant) * count / 1000;
        }

        // ---------------------------------------------------------------- asking out while taken

        /// <summary>What asking somebody out comes to while the player has a partner: nothing at all (the option off, or somebody else already), or a warning first.</summary>
        public static bool WarnsBeforeAsking(bool optionOn, bool hasPartner, bool hasSecond)
        {
            return optionOn && hasPartner && !hasSecond;
        }

        /// <summary>Whether Ask them out is on the menu: with no partner as ever; with one, only for a second person while the option is on.</summary>
        public static bool MayAsk(bool optionOn, bool hasPartner, bool hasSecond)
        {
            return !hasPartner || (optionOn && !hasSecond);
        }

        // ---------------------------------------------------------------- being found out

        public const int CatchMost = 850;          // per mille: never certain
        public const int SuspicionPerSign = 20;    // points a sign adds to the watcher's suspicion
        public const int SuspicionDecay = 3;       // points a day it falls by
        public const int AroundDays = 10;          // days a night that got around raises the chance
        public const int CloseMinutes = 180;       // two outings this near in time are near

        /// <summary>Whether an outing can be seen at all: the favours (a lift, a move) and showing somebody round a business are not.</summary>
        public static bool Public(PlanKind kind)
        {
            return kind != PlanKind.Business && kind != PlanKind.Lift && kind != PlanKind.Moving;
        }

        /// <summary>Per mille: how likely an outing of this kind is to be seen by anybody who minds, before anything about the place or the people.</summary>
        public static int KindBase(PlanKind kind)
        {
            switch (kind)
            {
                case PlanKind.Walk: return 8;
                case PlanKind.Drive: return 6;
                case PlanKind.Workout: return 14;
                case PlanKind.Coffee: return 28;
                case PlanKind.Cinema: return 34;
                case PlanKind.Meal: return 40;
                case PlanKind.Drinks: return 58;
                default: return 0;
            }
        }

        /// <summary>
        /// How near two plans on one day are: 0 none, 1 the same day, 2 within <see cref="CloseMinutes"/> of each other, 3 near in time and
        /// in the same neighbourhood. Of the watcher's own last outing with the player against this one's time and place.
        /// </summary>
        public static int Closeness(LoveFile f, int watcher, int day, int minute, int district)
        {
            int best = 0;
            for (int i = 0; f != null && i < f.Outings.Count; i++)
            {
                LoveOuting o = f.Outings[i];
                if (o.Person != watcher || o.Day != day)
                {
                    continue;
                }
                int level = Math.Abs(o.Minute - minute) <= CloseMinutes ? (district >= 0 && o.District == district ? 3 : 2) : 1;
                best = Math.Max(best, level);
            }
            return best;
        }

        /// <summary>
        /// Per mille: the chance that the watcher finds out about this outing. Each factor moves it one way: the same district as their home,
        /// their free hours, a friend of both about, a post that tags the person, two plans close in time and place, their own traits
        /// (the envious, the careful and the punctual notice more), their suspicion and a night that got around; a walk at a far park is
        /// the least, a nightclub near their home the most.
        /// </summary>
        public static int Chance(in CatchFacts f)
        {
            int c = KindBase(f.Kind);
            if (c == 0)
            {
                return 0;
            }
            if (f.SameDistrict)
            {
                c += f.Kind == PlanKind.Walk ? 50 : 80;
            }
            if (f.Free)
            {
                c += 35;
            }
            c += 28 * Math.Max(0, Math.Min(3, f.Mutual));
            if (f.Tagged)
            {
                c += 110;
            }
            c += f.Close == 3 ? 230 : f.Close == 2 ? 110 : f.Close == 1 ? 30 : 0;
            if (f.Envious)
            {
                c += 55;
            }
            if (f.Careful)
            {
                c += 35;
            }
            if (f.Punctual)
            {
                c += 25;
            }
            c += Math.Max(0, Math.Min(100, f.Suspicion)) * 3 / 2;
            if (f.Around)
            {
                c += 180;
            }
            if (f.LivesTogether)
            {
                c += 45;
                if (f.Hour >= 21 || f.Hour < 4)
                {
                    c += 30;
                }
            }
            if (f.Denied)
            {
                c -= 30;
            }
            return Math.Max(0, Math.Min(CatchMost, c));
        }

        /// <summary>Whether a roll of 0 to 999 against a chance is a catch, a sign (somebody suspects, and says so) or nothing: signs come before the catch.</summary>
        public static CatchVerdict Verdict(int roll, int chance)
        {
            if (chance <= 0)
            {
                return CatchVerdict.Nothing;
            }
            if (roll < chance)
            {
                return CatchVerdict.Caught;
            }
            return roll < chance + chance / 2 + 40 ? CatchVerdict.Sign : CatchVerdict.Nothing;
        }

        /// <summary>The suspicion after a day: it falls by itself.</summary>
        public static int Settled(int suspicion, int days)
        {
            return Math.Max(0, suspicion - SuspicionDecay * Math.Max(0, days));
        }

        // ---------------------------------------------------------------- what each answer is worth

        /// <summary>
        /// Per mille: that they forgive. Closeness and trust help, the supportive and the generous forgive more, the
        /// envious and the careful less, a second chance spent or a lie found out costs.
        /// </summary>
        public static int ForgiveChance(SocialPerson watcher, LoveAnswer answer, CatchHow how, int forgivenBefore, bool deniedBefore, bool married)
        {
            int c = 400 + (int)((watcher.Affection - 50f) * 4f) + (int)((watcher.Trust - 50f) * 3f);
            if (watcher.Stage == SocialStage.Partner)
            {
                c += 40;
            }
            if (married)
            {
                c += 30;
            }
            if (watcher.Has(SocialTrait.Supportive))
            {
                c += 90;
            }
            if (watcher.Has(SocialTrait.Generous))
            {
                c += 50;
            }
            if (watcher.Has(SocialTrait.Envious))
            {
                c -= 110;
            }
            if (watcher.Has(SocialTrait.Careful))
            {
                c -= 45;
            }
            if (watcher.Has(SocialTrait.Punctual))
            {
                c -= 25;
            }
            c += answer == LoveAnswer.Confess ? 130 : answer == LoveAnswer.EndOther ? 190 : 0;
            c -= how == CatchHow.Child ? 250 : how == CatchHow.Night ? 80 : 0;
            c -= 220 * Math.Max(0, forgivenBefore);
            if (deniedBefore)
            {
                c -= 200;
            }
            return Math.Max(40, Math.Min(900, c));
        }

        /// <summary>
        /// Per mille: that a denial holds. A friend's word or a post is thin proof, seeing is not; trust helps, the
        /// envious and the careful do not let go, and a child cannot be denied.
        /// </summary>
        public static int DenyChance(SocialPerson watcher, CatchHow how, int suspicion, bool deniedBefore)
        {
            int c = how == CatchHow.Seen ? 150 : how == CatchHow.Friend ? 420 : how == CatchHow.Post ? 520 : how == CatchHow.Night ? 220 : 0;
            if (c == 0)
            {
                return 0;
            }
            c += (int)((watcher.Trust - 50f) * 5f);
            if (watcher.Has(SocialTrait.Envious))
            {
                c -= 120;
            }
            if (watcher.Has(SocialTrait.Careful))
            {
                c -= 70;
            }
            if (watcher.Has(SocialTrait.Punctual))
            {
                c -= 30;
            }
            if (suspicion > 60)
            {
                c -= 120;
            }
            if (deniedBefore)
            {
                c -= 150;
            }
            return Math.Max(0, Math.Min(800, c));
        }

        /// <summary>What forgiving, or not, does to the figures of whoever asked. A lie found out or a second chance already spent costs double.</summary>
        public static LoveResult Result(LoveAnswer answer, bool forgiven, bool held, bool deniedBefore, int forgivenBefore)
        {
            var r = new LoveResult { Double = deniedBefore || forgivenBefore > 0 };
            float scale = r.Double ? 2f : 1f;
            if (answer == LoveAnswer.Deny)
            {
                if (held)
                {
                    r.Stays = true;
                    r.Held = true;
                    return r;
                }
                r.Affection = -20f * scale;
                r.Trust = -30f * scale;
                return r;
            }
            if (answer == LoveAnswer.EndThis)
            {
                r.Affection = -15f;
                r.Trust = -10f;
                return r;
            }
            if (forgiven)
            {
                r.Stays = true;
                r.Affection = (answer == LoveAnswer.EndOther ? -3f : -5f) * scale;
                r.Trust = (answer == LoveAnswer.EndOther ? -8f : -12f) * scale;
                return r;
            }
            r.Affection = -25f * scale;
            r.Trust = -30f * scale;
            return r;
        }

        /// <summary>Per mille: that the other person learns the player was taken, when somebody found out and it was said or seen.</summary>
        public static int LearnsChance(CatchHow how, bool envious)
        {
            int c = how == CatchHow.Friend || how == CatchHow.Post ? 550 : how == CatchHow.Child ? 700 : 350;
            return envious ? Math.Min(900, c + 100) : c;
        }

        /// <summary>The figures that move for the other person who learns: affection and trust fall; they may leave on their own.</summary>
        public static void LearnsFigures(out float affection, out float trust)
        {
            affection = -12f;
            trust = -16f;
        }

        // ---------------------------------------------------------------- nights

        public const int OfferGapDays = 6;         // days after a night with somebody before they offer another
        public const int NightFloorHour = 20;      // an offer after drinks comes in the evening

        /// <summary>
        /// Per mille: the chance that somebody who is open to a romance with the player offers an evening at theirs or the player's
        /// after it went well. Affection, a good mood, the hour, drink and some traits raise it; the careful and the reliable lower it.
        /// </summary>
        public static int OfferChance(in NightFacts f)
        {
            if (!f.WentWell || f.Together)
            {
                return 0;
            }
            int c = f.Kind == NightKind.Drinks ? 110 : 70;
            c += (int)((f.Affection - 50f) * 2.2f);
            c += Math.Max(-2, Math.Min(2, f.Mood)) * 12;
            c += f.Hour >= 21 || f.Hour < 4 ? 40 : f.Hour >= 17 ? 15 : 0;
            c += DrinkLift(f.Drinks, 15, 45, 80);
            if (f.NightOwl)
            {
                c += 25;
            }
            if (f.Flaky)
            {
                c += 20;
            }
            if (f.Careful)
            {
                c -= 35;
            }
            if (f.Reliable)
            {
                c -= 15;
            }
            if (f.Homebody)
            {
                c -= 10;
            }
            if (f.Stage == SocialStage.CloseFriend)
            {
                c += 20;
            }
            return Math.Max(0, Math.Min(600, c));
        }

        // A lift by the drink: a little from one, tipsy at two, a lot from three and a half.
        private static int DrinkLift(float drinks, int some, int tipsy, int many)
        {
            return drinks >= 3.5f ? many : drinks >= 2f ? tipsy : drinks >= 1f ? some : 0;
        }

        /// <summary>Per mille: that they say yes to "come back to mine", without a second thought more the more they have had.</summary>
        public static int YesChance(in NightFacts f)
        {
            int c = 560 + (int)((f.Affection - 50f) * 4f) + (int)(Math.Min(4f, f.Drinks) * 70f);
            if (f.Flaky)
            {
                c += 80;
            }
            if (f.NightOwl)
            {
                c += 40;
            }
            if (f.Homebody)
            {
                c += 60;
            }
            if (f.Careful)
            {
                c -= 120;
            }
            if (f.Reliable)
            {
                c -= 50;
            }
            return Math.Max(80, Math.Min(950, c));
        }

        /// <summary>
        /// The weight of each outcome of a night, in the order of <see cref="NightOutcome"/>, from the person and the
        /// evening: more warmth where there is affection, more regret and mishaps with drink, word getting round when the
        /// player is with somebody.
        /// </summary>
        public static void Weights(in NightFacts f, int[] into)
        {
            int warm = 28, awkward = 22, regret = 14, more = 12, around = f.Taken ? 20 : 8, mishap = 16;
            if (f.Affection >= 70f)
            {
                warm += 14;
                more += 10;
                awkward -= 10;
            }
            else if (f.Affection < 45f)
            {
                awkward += 12;
                warm -= 10;
            }
            if (f.Trust >= 60f)
            {
                more += 4;
            }
            if (f.Drinks >= 3.5f)
            {
                regret += 18;
                mishap += 14;
                awkward += 6;
                warm -= 6;
            }
            else if (f.Drinks >= 2f)
            {
                regret += 10;
                mishap += 8;
                awkward += 6;
                warm -= 6;
            }
            if (f.Flaky)
            {
                awkward += 14;
                more -= 8;
            }
            if (f.Reliable)
            {
                warm += 8;
                awkward -= 6;
            }
            if (f.Careful)
            {
                regret += 4;
                around -= 2;
            }
            if (f.Supportive)
            {
                warm += 6;
            }
            if (f.Envious)
            {
                around += 5;
            }
            if (f.NightOwl)
            {
                mishap += 4;
            }
            if (f.Generous)
            {
                warm += 4;
            }
            if (f.Homebody)
            {
                more += 4;
            }
            if (f.Taken)
            {
                regret += 10;
            }
            into[(int)NightOutcome.Warm] = Math.Max(0, warm);
            into[(int)NightOutcome.Awkward] = Math.Max(0, awkward);
            into[(int)NightOutcome.Regret] = Math.Max(0, regret);
            into[(int)NightOutcome.WantsMore] = Math.Max(0, more);
            into[(int)NightOutcome.GotAround] = Math.Max(0, around);
            into[(int)NightOutcome.Mishap] = Math.Max(0, mishap);
        }

        /// <summary>The outcome a roll of 0 to 999 falls on, by the weights.</summary>
        public static NightOutcome Pick(int roll, int[] weights)
        {
            int total = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                total += weights[i];
            }
            if (total <= 0)
            {
                return NightOutcome.Warm;
            }
            int at = roll * total / 1000;
            for (int i = 0; i < weights.Length; i++)
            {
                at -= weights[i];
                if (at < 0)
                {
                    return (NightOutcome)i;
                }
            }
            return NightOutcome.Warm;
        }

        /// <summary>What a night does to the figures between them: familiarity, affection, trust.</summary>
        public static void Figures(NightOutcome outcome, out float familiarity, out float affection, out float trust)
        {
            switch (outcome)
            {
                case NightOutcome.Warm: familiarity = 4f; affection = 5f; trust = 3f; return;
                case NightOutcome.Awkward: familiarity = 2f; affection = -5f; trust = -2f; return;
                case NightOutcome.Regret: familiarity = 2f; affection = -3f; trust = -1f; return;
                case NightOutcome.WantsMore: familiarity = 3f; affection = 4f; trust = 2f; return;
                case NightOutcome.GotAround: familiarity = 3f; affection = 1f; trust = -1f; return;
                default: familiarity = 3f; affection = 2f; trust = 0f; return;
            }
        }

        /// <summary>Days they keep quiet after an awkward night: they never text back.</summary>
        public const int QuietDays = 6;

        /// <summary>Whether the player's own mood dips after this outcome (they are with somebody and it got round, or they regret it).</summary>
        public static bool MoodDips(NightOutcome outcome, bool taken)
        {
            return outcome == NightOutcome.Regret || outcome == NightOutcome.Awkward || (outcome == NightOutcome.GotAround && taken);
        }

        /// <summary>Whether the player's own mood lifts.</summary>
        public static bool MoodLifts(NightOutcome outcome)
        {
            return outcome == NightOutcome.Warm || outcome == NightOutcome.WantsMore;
        }

        /// <summary>A small cost of a mishap at the start's prices: a lost charger, a taxi home at dawn; tens of dollars, never more.</summary>
        public static int MishapCost(int variant)
        {
            switch (Math.Abs(variant) % 4)
            {
                case 0: return 18;   // a phone charger left behind
                case 1: return 35;   // a taxi home at dawn
                case 2: return 24;   // a breakfast to be polite
                default: return 48;  // a cracked screen on the way out of the door
            }
        }

        /// <summary>Game hours the night takes, from the hour it began: to the small hours' end and a little more, so the morning comes about half past seven.</summary>
        public static int NightHours(int hour)
        {
            int hours = hour >= 7 ? 24 - hour + 7 : 7 - hour;
            return Math.Max(6, Math.Min(12, hours + 1));
        }

        // ---------------------------------------------------------------- a child of a night

        public const int ConceiveBase = 22;        // per mille: a few in a hundred
        public const int CarrierAgeLeast = 18;
        public const int CarrierAgeMost = 44;

        /// <summary>
        /// Whether a night can bring a child at all: both options on, a woman and a man, the one who would carry of an age to, and the
        /// family able to take another child (Family's own rule: the children option and its most).
        /// </summary>
        public static bool MayConceive(in ConceiveFacts f)
        {
            return f.Surprise && f.Children && f.SexesDiffer && f.CarrierAge >= CarrierAgeLeast && f.CarrierAge <= CarrierAgeMost && f.Room > 0;
        }

        /// <summary>Per mille: the chance a night brings a child. Rare; more after drinks, less for the careful, and none where <see cref="MayConceive"/> says no.</summary>
        public static int ConceiveChance(in ConceiveFacts f)
        {
            if (!MayConceive(f))
            {
                return 0;
            }
            int c = ConceiveBase;
            if (f.Drinks >= 2f)
            {
                c = c * 16 / 10;
            }
            if (f.Careful)
            {
                c = c / 2;
            }
            if (f.Reliable)
            {
                c = c * 8 / 10;
            }
            if (f.Flaky)
            {
                c = c * 12 / 10;
            }
            return Math.Max(1, c);
        }

        /// <summary>The one who carries: the woman. The player when they are the woman (1), else the other (0).</summary>
        public static int Carrier(bool playerFemale)
        {
            return playerFemale ? 1 : 0;
        }

        /// <summary>The day the news comes: part of the way through a pregnancy, in days of a child's year.</summary>
        public static int NewsDay(int nightDay, int yearDays)
        {
            int span = Family.ChildYearRules.Span(0.3f, yearDays);
            int due = Family.ChildYearRules.Span(0.75f, yearDays);
            return nightDay + Math.Max(3, Math.Min(span, due - 3));
        }

        /// <summary>The day the child is born.</summary>
        public static int DueDay(int nightDay, int yearDays)
        {
            return nightDay + Family.ChildYearRules.Span(0.75f, yearDays);
        }

        public const int SupportBase = 400;        // a month, at the start's prices
        public const int SupportOlder = 100;       // more from the age school begins
        public const int SupportMonthDays = 30;

        /// <summary>A month's support at the start's prices; tens, a little more once the child is of school age.</summary>
        public static int SupportAmount(float childAge)
        {
            return SupportBase + (childAge >= 5f ? SupportOlder : 0);
        }

        /// <summary>Months of support due since the last payment, at most three a time (a long time away is not paid all at once).</summary>
        public static int MonthsDue(int lastPaid, int day)
        {
            return lastPaid <= 0 ? 0 : Math.Max(0, Math.Min(3, (day - lastPaid) / SupportMonthDays));
        }

        /// <summary>Per mille a day: that the player's partner learns of a child they are not the parent of, by how the player chose to be a parent.</summary>
        public static int PartnerLearns(LoveChoice choice)
        {
            return choice == LoveChoice.Involved ? 1000 : choice == LoveChoice.Support ? 25 : choice == LoveChoice.StayOut ? 12 : 0;
        }

        /// <summary>What staying out of a child's life costs: affection and trust with Mom, points of standing, per mille of Flare followers.</summary>
        public static void StayOutCosts(out float momAffection, out float momTrust, out int standing, out int followers)
        {
            momAffection = -8f;
            momTrust = -6f;
            standing = 3;
            followers = 15;
        }

        // ---------------------------------------------------------------- a scandal

        public const int ScandalGapDays = 7;       // days between two scandals the paper prints

        /// <summary>
        /// What a scandal is worth for how known the player is (their recognition, 0 to 100, the tiers of Standing): the unknown only
        /// get talked about among friends; from a local name up the Ledger may print it, Flare reacts (some lose interest, a scandal
        /// draws others) and business standing takes a small dent that fades by itself. A child counts a half more, a marriage or an
        /// engagement a third.
        /// </summary>
        public static ScandalScale Scandal(int recognition, bool child, bool bound)
        {
            Fame.FameTier tier = Fame.FameRules.TierOf(recognition);
            var s = new ScandalScale { Gossip = true };
            switch (tier)
            {
                case Fame.FameTier.Local: s.Headline = 30; s.FlareLost = 5; s.FlareGained = 8; s.Dent = 0; break;
                case Fame.FameTier.Known: s.Headline = 60; s.FlareLost = 12; s.FlareGained = 20; s.Dent = 2; break;
                case Fame.FameTier.Prominent: s.Headline = 85; s.FlareLost = 20; s.FlareGained = 35; s.Dent = 3; break;
                case Fame.FameTier.Famous: s.Headline = 100; s.FlareLost = 30; s.FlareGained = 60; s.Dent = 4; break;
                default: s.Gossip = true; return s;
            }
            if (child)
            {
                s.Headline = Math.Min(100, s.Headline * 3 / 2);
                s.FlareLost = s.FlareLost * 3 / 2;
                s.FlareGained = s.FlareGained * 3 / 2;
            }
            if (bound)
            {
                s.Headline = Math.Min(100, s.Headline * 4 / 3);
                s.FlareLost = s.FlareLost * 4 / 3;
            }
            return s;
        }

        /// <summary>Whether a scandal may be printed now: not within <see cref="ScandalGapDays"/> of the last.</summary>
        public static bool ScandalMay(int lastDay, int day)
        {
            return lastDay <= 0 || day - lastDay >= ScandalGapDays;
        }
    }
}
