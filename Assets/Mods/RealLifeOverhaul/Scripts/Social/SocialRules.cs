using System;

namespace RealLifeOverhaul
{
    /// <summary>The relationship model's rules: what each thing is worth in points of 0 to 100, when a stage is reached and lost, neglect.</summary>
    public static class SocialRules
    {
        public const int CastSize = 8;

        public const float CallFamiliarity = 3f, CallAffection = 2f, CallTrust = 0.5f;
        public const float WarmReply = 1.5f, BluntReply = -2f;
        public const float TextReply = 1f;
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
    }
}
