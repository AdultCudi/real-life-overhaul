using System;
using System.Globalization;

namespace RealLifeOverhaul
{
    /// <summary>What a loan to a friend asks of the day: nothing, the day they said they would pay it back, or the end of the days more they asked for.</summary>
    public enum LoanStep { None, Due, GraceOver }

    /// <summary>The relationship model's rules: what each thing is worth in points of 0 to 100, when a stage is reached and lost, neglect.</summary>
    public static class SocialRules
    {
        public const int BaseCast = 8;                // people a save starts with: Mom, Alex and one for each of six places
        public const int MaxCast = 400;               // people a save can ever know; past it nobody new comes
        public const int MeetGapDays = 1;             // game days at least between two new faces
        public const int CircleSize = 12;             // the closest people who get in touch of their own accord
        public const int CircleNewDays = 14;          // game days somebody new stays keen, however many are closer
        public const int DriftLoseDays = 60;          // game days without a word before a drifted acquaintance can go by themselves, once the cast is full

        public const float CallFamiliarity = 3f, CallAffection = 2f, CallTrust = 0.5f;
        public const float WarmReply = 1.5f, BluntReply = -2f;
        public const float TextReply = 1f;
        public const int CallTopics = 2;              // topics talked over in one call before the goodbye (PersonDialog)
        public const float PlanLead = 45f;            // game minutes from now before the earliest hour an outing is agreed for (PersonDialog)
        public const int PlanLookHours = 48;          // hours looked ahead for one they are free at: past that, "ask me again"
        public const int OpenTries = 8;               // free hours tried in turn for one at which a place nearby is open, before a walk is offered
        public const float PlanMoodNo = 0.5f;         // the chance that somebody on a rough day (mood -2) will not make plans the player asks for
        public const float SelfieFamiliarity = 1f, SelfieAffection = 2f;   // a selfie together on an outing, once an outing
        public const float SelfieWithin = 3f;                               // metres from the player the friend stands in
        public const float OnTimeTrust = 2f;
        public const float LateTrust = -3f, LateAffection = -2f;
        public const float StoodUpTrust = -12f, StoodUpAffection = -10f;
        public const float CancelledTrust = -4f, CancelledAffection = -3f;   // the player called it off
        public const float RudeAffection = -5f;
        public const float FavourTrust = 8f, FavourAffection = 4f;
        public const float FavourRefusedAffection = -2f;
        public const float BirthdayAffection = 8f, BirthdayTrust = 3f, BirthdayMissed = -4f;
        public const float ArrestTrust = -4f;
        public const float AskTrust = -2f;            // each favour asked of them
        public const float TooManyAsksTrust = -5f;    // ...and a third within a week
        public const int AskWindowDays = 7;
        public const int AsksAllowed = 2;
        public const float NotRepaidTrust = -10f;     // the player kept their money
        public const int RepayDays = 10;
        public const int LoanGraceDays = 4;           // days more a friend asks for, once, when they cannot pay a loan back on the day
        public const float ReportAtPlayer = 3f;       // metres: a report made indoors this near the player was made where they stand
        public const int LateMinutes = 20;            // game minutes after the agreed time that count as late
        public const int StoodUpMinutes = 75;         // ...and as not coming at all
        public const int EarlyMinutes = 60;           // the plan is shown from this long before

        public const float FriendFamiliarity = 30f, FriendAffection = 30f, FriendTrust = 20f;
        public const int FriendShared = 2;
        public const float CloseFamiliarity = 60f, CloseAffection = 60f, CloseTrust = 55f;
        public const int CloseShared = 5, CloseDays = 10;
        public const float DateAffection = 60f, DateTrust = 45f;
        public const float PartnerAffection = 80f, PartnerTrust = 70f;
        public const int PartnerDays = 14, PartnerDates = 4;
        public const float MoveInTrust = 85f;
        public const int MoveInDays = 21;
        public const float BreakUpAffection = 35f, BreakUpTrust = 25f;
        public const float Slack = 10f;               // a stage is kept until this far below what earned it
        public const float DriftFamiliarity = 8f;

        public static float Clamp(float value) => value < 0f ? 0f : value > 100f ? 100f : value;

        public static void Change(SocialPerson person, float familiarity, float affection, float trust)
        {
            person.Familiarity = Clamp(person.Familiarity + familiarity);
            person.Affection = Clamp(person.Affection + affection);
            person.Trust = Clamp(person.Trust + trust);
            if (person.IsFamily)
            {
                // Family never falls below being friends.
                person.Familiarity = Math.Max(person.Familiarity, 55f);
                person.Affection = Math.Max(person.Affection, 45f);
                person.Trust = Math.Max(person.Trust, 40f);
            }
        }

        /// <summary>Days without a word before things start to slip.</summary>
        public static int Grace(SocialPerson person)
        {
            if (person.IsFamily)
            {
                return 6;
            }
            switch (person.Stage)
            {
                case SocialStage.Partner: return 3;
                case SocialStage.Dating: return 3;
                case SocialStage.CloseFriend: return 10;
                case SocialStage.Friend: return 7;
                default: return 4;
            }
        }

        /// <summary>True if anything changed.</summary>
        public static bool Decay(SocialPerson person, int day)
        {
            if (!person.Met || day - person.LastContactDay <= Grace(person))
            {
                return false;
            }
            Change(person, -1f, person.Together ? -1.5f : -1f, -0.5f);
            return true;
        }

        /// <summary>One step at a time, and never Dating or Partner: those are asked for, and end only by <see cref="BrokenUp"/>.</summary>
        public static SocialStage Earned(SocialPerson person, int day)
        {
            if (!person.Met)
            {
                return SocialStage.Stranger;
            }
            SocialStage stage = person.Stage < SocialStage.Acquaintance ? SocialStage.Acquaintance : person.Stage;
            if (person.Together)
            {
                return stage;
            }
            if (stage == SocialStage.Acquaintance && person.Familiarity >= FriendFamiliarity && person.Affection >= FriendAffection
                && person.Trust >= FriendTrust && (person.Shared >= FriendShared || person.IsFamily || person.Origin == SocialOrigin.OldFriend))
            {
                return SocialStage.Friend;
            }
            if (stage == SocialStage.Friend && person.Familiarity >= CloseFamiliarity && person.Affection >= CloseAffection
                && person.Trust >= CloseTrust && person.Shared >= CloseShared && day - person.MetDay >= CloseDays)
            {
                return SocialStage.CloseFriend;
            }
            if (stage == SocialStage.CloseFriend && (person.Familiarity < CloseFamiliarity - Slack || person.Affection < CloseAffection - Slack
                                                     || person.Trust < CloseTrust - Slack))
            {
                return SocialStage.Friend;
            }
            if (stage == SocialStage.Friend && !person.IsFamily && (person.Familiarity < FriendFamiliarity - Slack
                                                                    || person.Affection < FriendAffection - Slack || person.Trust < FriendTrust - Slack))
            {
                return SocialStage.Acquaintance;
            }
            return stage;
        }

        public static bool BrokenUp(SocialPerson person)
        {
            return person.Together && (person.Affection < BreakUpAffection || person.Trust < BreakUpTrust);
        }

        public static bool WouldDate(SocialPerson person)
        {
            return person.Compatible && !person.Together && person.Stage >= SocialStage.Friend
                   && person.Affection >= DateAffection && person.Trust >= DateTrust;
        }

        public static bool WouldCommit(SocialPerson person, int day)
        {
            return person.Stage == SocialStage.Dating && person.Affection >= PartnerAffection && person.Trust >= PartnerTrust
                   && day - person.StageDay >= PartnerDays && person.Dates >= PartnerDates;
        }

        public static bool WouldMoveIn(SocialPerson person, int day)
        {
            return person.Stage == SocialStage.Partner && !person.MovedIn && person.Trust >= MoveInTrust && day - person.StageDay >= MoveInDays;
        }

        /// <summary>How much they like a kind of outing: 0.7 (not their thing) to 1.4 (exactly their thing).</summary>
        public static float Liking(SocialPerson person, PlanKind kind)
        {
            float liking = 1f;
            switch (kind)
            {
                case PlanKind.Coffee:
                    liking = With(liking, person.Has(SocialTrait.Foodie) || person.Has(SocialTrait.Homebody), 0.3f);
                    break;
                case PlanKind.Meal:
                    liking = With(With(liking, person.Has(SocialTrait.Foodie), 0.4f), person.Has(SocialTrait.Careful), -0.15f);
                    break;
                case PlanKind.Drinks:
                    liking = With(With(liking, person.Has(SocialTrait.NightOwl), 0.4f), person.Has(SocialTrait.Homebody), -0.3f);
                    break;
                case PlanKind.Cinema:
                    liking = With(With(liking, person.Has(SocialTrait.NightOwl), 0.25f), person.Has(SocialTrait.Sporty), -0.1f);
                    break;
                case PlanKind.Walk:
                    liking = With(liking, person.Has(SocialTrait.Sporty) || person.Has(SocialTrait.Homebody), 0.3f);
                    break;
                case PlanKind.Drive:
                    liking = With(liking, person.Has(SocialTrait.Homebody), -0.2f);
                    break;
                case PlanKind.Workout:
                    liking += person.Has(SocialTrait.Sporty) ? 0.4f : -0.3f;
                    break;
                case PlanKind.Business:
                    liking = With(With(liking, person.Has(SocialTrait.Supportive), 0.3f), person.Has(SocialTrait.Envious), -0.3f);
                    break;
            }
            return liking < 0.7f ? 0.7f : liking > 1.4f ? 1.4f : liking;
        }

        private static float With(float liking, bool has, float change)
        {
            return has ? liking + change : liking;
        }

        public static float CancelOdds(SocialPerson person, PlanKind kind, bool raining)
        {
            if (person.Has(SocialTrait.Reliable) || kind == PlanKind.Lift || kind == PlanKind.Moving)
            {
                return 0f;
            }
            float odds = 0.05f;
            if (person.Has(SocialTrait.Flaky))
            {
                odds += 0.13f;
            }
            if (person.Has(SocialTrait.Homebody))
            {
                odds += 0.05f;
            }
            if (person.Mood <= -2)
            {
                odds += 0.10f;
            }
            if (raining && kind == PlanKind.Walk)
            {
                odds += 0.25f;
            }
            if (person.Together)
            {
                odds *= 0.5f;
            }
            return odds;
        }

        /// <summary>
        /// A loan's step today. <paramref name="due"/> is the day they said they would pay, or, once they have asked for more
        /// days (<see cref="GraceUntil"/>), the end of those days as a negative number.
        /// </summary>
        public static LoanStep Loan(int loan, int due, int day)
        {
            if (loan <= 0)
            {
                return LoanStep.None;
            }
            if (due >= 0)
            {
                return day >= due ? LoanStep.Due : LoanStep.None;
            }
            return day >= -due ? LoanStep.GraceOver : LoanStep.None;
        }

        /// <summary>The loan's due day once they have asked for a few more days on <paramref name="day"/>: negative, so the excuse is made once.</summary>
        public static int GraceUntil(int day) => -(day + LoanGraceDays);

        /// <summary>
        /// A person's age on game <paramref name="day"/> (1 the first): <paramref name="age"/> is what they were on
        /// <paramref name="ageDay"/> (SocialPerson.AgeDay: the day the cast was made, or an older save's first load with it),
        /// and each birthday after that day (<paramref name="birthday"/>, a day of the year, read as SocialTalk.BirthdayIn
        /// reads it) adds a year, so the age grows on the day the People page counts down to. Never less than
        /// <paramref name="age"/>. Worked out when shown, from saved numbers only: the same on every machine.
        /// </summary>
        public static int AgeOn(int age, int ageDay, int birthday, int day, int daysPerYear)
        {
            int perYear = Math.Max(1, daysPerYear);
            int first = ((birthday - 1) % perYear + perYear) % perYear + 1;   // the first day of the calendar that is their birthday
            return age + Math.Max(0, BirthdaysBy(day, first, perYear) - BirthdaysBy(ageDay, first, perYear));
        }

        // How many of the calendar's days 1 to day are the birthday that first falls on day first.
        private static int BirthdaysBy(int day, int first, int perYear)
        {
            return day >= first ? (day - first) / perYear + 1 : 0;
        }

        /// <summary>
        /// Whether a report made indoors names the player's own place: an interior's coordinates say nothing of the city, so it
        /// is moved to the building's door. Any other place (a parked car's break-in, a crash down the street) is in the city.
        /// </summary>
        public static bool AtPlayer(float x, float z, float playerX, float playerZ)
        {
            float dx = x - playerX, dz = z - playerZ;
            return dx * dx + dz * dz <= ReportAtPlayer * ReportAtPlayer;
        }

        /// <summary>Game minutes from the agreed time at which they turn up; negative is early. <paramref name="roll"/> is 0 to 1.</summary>
        public static float ArrivalOffset(SocialPerson person, double roll)
        {
            if (person.Has(SocialTrait.Punctual))
            {
                return -10f + (float)roll * 6f;
            }
            if (person.Has(SocialTrait.Late))
            {
                return roll < 0.55 ? 8f + (float)roll * 40f : -2f;
            }
            return roll < 0.12 ? 10f + (float)roll * 60f : -6f + (float)roll * 8f;
        }

        /// <summary>
        /// What one answer in a chat is worth, on a call or face to face (SocialTalk.Answer): the day's first exchange counts
        /// the talk itself (half as much again face to face, and a little more when they texted since yesterday and the
        /// player is getting back to them), then the answer: warm, blunt (a slight, unless either answer suits the topic) or
        /// plain. A message typed on a call counts as the warm answer (<see cref="SocialComposerRules.TypedWarm"/>).
        /// </summary>
        public static SocialWorth Chat(bool firstOfDay, bool inPerson, bool textedLately, bool warm, bool bothFine)
        {
            var worth = new SocialWorth();
            if (firstOfDay)
            {
                float scale = inPerson ? 1.5f : 1f;
                worth.Familiarity = CallFamiliarity * scale;
                worth.Affection = CallAffection * scale;
                worth.Trust = CallTrust * scale;
                if (textedLately)
                {
                    worth.Familiarity += TextReply;   // they wrote, and the player got back to them
                    worth.Affection += 0.5f;
                }
            }
            worth.Affection += warm ? WarmReply : bothFine ? 0.5f : BluntReply;
            return worth;
        }

        /// <summary>
        /// True when a call's chat is over before it starts: the day's chat with them was had on an earlier call, so the
        /// first topic of this one is "we covered everything" and worth nothing (a chat face to face is not counted here).
        /// </summary>
        public static bool ChatHad(int topicsThisCall, int lastCallDay, int day) => topicsThisCall == 0 && lastCallDay == day;

        /// <summary>
        /// The first whole hour from a time (game minutes) at which an outing can be agreed with somebody: the outing's hours,
        /// their free hours that day and not their sleep; negative if none within <see cref="PlanLookHours"/>. The day and hour
        /// are read from the minutes as the game's own Timestamp reads them. Asked once when an outing is put forward, and that
        /// hour is the one then agreed, so what is offered is never turned down a step later. At most 48 small steps, on a step
        /// of a call, never per frame; nothing allocated.
        /// </summary>
        public static float FirstSlot(SocialPerson person, OutingDef def, float from, Func<int, bool> weekend)
        {
            float start = (float)Math.Ceiling(from / 60f) * 60f;
            for (int i = 0; i < PlanLookHours; i++)
            {
                float at = start + i * 60f;
                int day = (int)Math.Floor(at / 60f / 24f);
                int hour = (int)Math.Floor((at - day * 60f * 24f) / 60f);
                bool open = hour >= def.From && hour < def.To || (def.To > 24 && hour < def.To - 24);
                if (open && person.FreeAt(hour, weekend(day)) && !person.Asleep(hour))
                {
                    return at;
                }
            }
            return -1f;
        }

        /// <summary>
        /// Whether a place is open for the whole of an outing that starts at a time (game minutes) and lasts some minutes:
        /// every hour from the one it starts in to the one its last minute falls in, by <paramref name="open"/> (the day and
        /// the hour of that day, read from the minutes as <see cref="FirstSlot"/> reads them). A cinema that shuts at 22:00 will
        /// not do for a film at 21:00. At most a few steps, on a step of a call, never per frame; nothing allocated.
        /// </summary>
        public static bool OpenThrough(float at, int minutes, Func<int, int, bool> open)
        {
            int first = (int)Math.Floor(at / 60f);
            int last = (int)Math.Floor((at + Math.Max(1, minutes) - 1f) / 60f);
            for (int h = first; h <= last; h++)
            {
                if (!open(h / 24, h % 24))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Whether somebody will not make plans the player asks for: only on a rough day (mood -2), and only by the one draw
        /// (0 to 1) made for the whole call, so the answer is given before anything is put forward and never changes within it.
        /// Their own invitation always stands.
        /// </summary>
        public static bool MoodSaysNo(int mood, bool theirIdea, float roll) => !theirIdea && mood <= -2 && roll < PlanMoodNo;

        /// <summary>
        /// The closeness a person needs to be among the <see cref="CircleSize"/> closest people met; 0 while no more than that
        /// many are met, so everybody is. <paramref name="scratch"/> holds at least CircleSize. One pass; nothing allocated.
        /// </summary>
        public static int CircleBar(SocialPerson[] people, int count, int[] scratch)
        {
            int held = 0;
            for (int i = 0; i < count; i++)
            {
                SocialPerson p = people[i];
                if (p == null || !p.Met)
                {
                    continue;
                }
                int closeness = p.Closeness;
                if (held == CircleSize && closeness <= scratch[CircleSize - 1])
                {
                    continue;
                }
                int at = held < CircleSize ? held++ : CircleSize - 1;   // a new place, or the least close one's
                while (at > 0 && scratch[at - 1] < closeness)
                {
                    scratch[at] = scratch[at - 1];
                    at--;
                }
                scratch[at] = closeness;
            }
            return held < CircleSize ? 0 : scratch[CircleSize - 1];
        }

        /// <summary>
        /// Whether somebody met gets in touch of their own accord: family, a partner, anyone met in the last
        /// <see cref="CircleNewDays"/> days, and the closest (<see cref="CircleBar"/>). With a big cast the rest stay quiet
        /// until the player calls them.
        /// </summary>
        public static bool InCircle(SocialPerson person, int day, int bar)
        {
            return person.Met && (person.IsFamily || person.Together || day - person.MetDay <= CircleNewDays || person.Closeness >= bar);
        }

        /// <summary>The chance an hour that one of the circle texts or asks something: as many as <see cref="CircleSize"/> count.</summary>
        public static float TextChance(int circle) => 0.09f + 0.02f * Math.Max(0, Math.Min(circle, CircleSize));

        /// <summary>The name of the call dialog type of the person in slot <paramref name="index"/>, as the game's hash is made from.</summary>
        public static string DialogTypeName(int index) => "reallife_socialp" + index.ToString(CultureInfo.InvariantCulture) + "_calldialogtype";

        /// <summary>The seed of everything rolled for the person in slot <paramref name="index"/> past the first eight: theirs alone.</summary>
        public static int SlotSeed(int castSeed, int index)
        {
            unchecked
            {
                uint h = (uint)castSeed * 2654435761u ^ (uint)index * 2246822519u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h & 0x7FFFFFFF) | 1;
            }
        }

        /// <summary>
        /// The seed of whoever takes the slot of somebody the player lost touch with: from the one who leaves, so the slot never
        /// rolls the same face again, and the same on every machine for the same cast.
        /// </summary>
        public static int ReplaceSeed(int castSeed, int index, int goneSeed)
        {
            return SlotSeed(unchecked(castSeed ^ (goneSeed * 16777619)), index);
        }

        /// <summary>
        /// Whether the player can lose touch with somebody, and if not the first reason: family and the old friend stay, a
        /// partner ends it with a talk (<see cref="BrokenUp"/>), and nobody another part of their life still needs goes: who
        /// lives with the player or is asked to, a plan with them, money between them, a Flare influencer's account.
        /// </summary>
        public static LoseBlock LoseBlockOf(SocialPerson person, bool planWith, bool livesWith, bool onFlare)
        {
            if (person == null || !person.Met)
            {
                return LoseBlock.NotKnown;
            }
            if (person.IsFamily)
            {
                return LoseBlock.Family;
            }
            if (person.Origin == SocialOrigin.OldFriend)
            {
                return LoseBlock.OldFriend;   // the phone's own friend contact, which the mod had before the social life
            }
            if (person.Together)
            {
                return LoseBlock.Together;
            }
            if (livesWith || person.MovedIn)
            {
                return LoseBlock.LivesWith;
            }
            if (planWith)
            {
                return LoseBlock.Plan;
            }
            if (person.Debt > 0 || person.Loan > 0)
            {
                return LoseBlock.Money;
            }
            return onFlare ? LoseBlock.Flare : LoseBlock.None;
        }

        /// <summary>
        /// Whether somebody may be let go by themselves to make room once every slot of the cast is taken: an acquaintance
        /// who drifted away and has had no word for <see cref="DriftLoseDays"/> days. The others wait for the player.
        /// </summary>
        public static bool DriftedAway(SocialPerson person, int day)
        {
            return person != null && person.Met && person.Drifted && person.Stage == SocialStage.Acquaintance && day - person.LastContactDay >= DriftLoseDays;
        }

        /// <summary>
        /// Of the people who may be let go by themselves (<see cref="DriftedAway"/>, and nothing holding them, as
        /// <paramref name="held"/> says), the one longest without a word, the lowest slot on a tie; -1 for nobody. One pass.
        /// </summary>
        public static int LongestDrifted(SocialPerson[] people, int count, int day, Func<SocialPerson, bool> held)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                SocialPerson p = people[i];
                if (DriftedAway(p, day) && (held == null || !held(p)) && (best < 0 || p.LastContactDay < people[best].LastContactDay))
                {
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// The newest acquaintance the player could lose touch with, by the day they were met and then the slot; -1 for
        /// nobody. For the test button. One pass.
        /// </summary>
        public static int NewestLoseable(SocialPerson[] people, int count, Func<SocialPerson, LoseBlock> block)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                SocialPerson p = people[i];
                if (p != null && p.Met && p.Stage == SocialStage.Acquaintance && block(p) == LoseBlock.None && (best < 0 || p.MetDay >= people[best].MetDay))
                {
                    best = i;
                }
            }
            return best;
        }
    }

    /// <summary>What something done together is worth, in points of 0 to 100 (<see cref="SocialRules.Chat"/>).</summary>
    public struct SocialWorth
    {
        public float Familiarity, Affection, Trust;
    }
}
