using System;

namespace RealLifeOverhaul
{
    /// <summary>One thing to do together: where, what it costs a head, how long it takes, what it is worth.</summary>
    public sealed class OutingDef
    {
        public PlanKind Kind;
        public string Name;            // "coffee"
        public string Offer;           // what they say when it is proposed, with {place} and {cost}
        public string[] Match;         // parts of the game's business type names that will do as the venue; null = anywhere
        public int Cost;               // per head
        public int Minutes;            // game minutes spent inside
        public int From, To;           // hours of the day it can be agreed for (To may be past midnight: 27 = 3 in the morning)
        public float Energy;           // of the game's hundred, to the player
        public float Familiarity, Affection, Trust;
        public SocialStage Needs = SocialStage.Acquaintance;
        public string Caption;         // shown while the time passes
        public string Transaction;
        public MemoryCode Memory;
        public string[] After;         // what they say afterwards when it went well
        public string Invite;          // the text they send when it is their idea
    }

    /// <summary>
    /// Something to talk about: what they say, the two things the player can answer and what
    /// each answer gets back. The first answer is the warm one.
    /// </summary>
    public sealed class SocialTopic
    {
        public int Id;
        public string[] Open;
        public string ReplyA, ReplyB;
        public string ReactA, ReactB;
        public bool BothFine;          // the second answer is a joke, not a slight
    }

    /// <summary>
    /// Everything about the social life that is data: who can be in the cast, what there is to
    /// do, every line anybody says. Free of game types. Every locale key is written out in full
    /// (the catalogue check reads this file).
    /// </summary>
    public static class SocialCatalogue
    {
        // ------------------------------------------------------------------ the cast

        private static readonly string[] Women =
        {
            "Dana", "Priya", "Marisol", "Keisha", "Hannah", "Mei", "Sofia", "Nadia", "Ruth", "Imani", "Greta", "Yuki", "Carmen", "Leila", "Bridget", "Tasha",
            "Ines", "Chloe", "Farah", "Odette", "Rosa", "Anika", "Joan", "Zoe"
        };
        private static readonly string[] Men =
        {
            "Marcus", "Theo", "Luis", "Dev", "Sam", "Kofi", "Anton", "Ray", "Jun", "Malik", "Pete", "Emil", "Hugo", "Omar", "Declan", "Victor",
            "Andre", "Nico", "Tariq", "Walt", "Bao", "Felix", "Isaac", "Ronan"
        };
        private static readonly string[] Surnames =
        {
            "Whitlock", "Okafor", "Reyes", "Castellano", "Brennan", "Nakamura", "Petrov", "Delgado", "Abara", "Lindqvist", "Morrow", "Shah", "Oyelaran", "Fitzgerald",
            "Kowalski", "Tran", "Haddad", "Mercer", "Vasquez", "Blum", "Achebe", "Sorensen", "Quinn", "Batista", "Ferrante", "Novak", "Idowu", "Halloran",
            "Yoon", "Marchetti", "Dupree", "Kaplan"
        };

        /// <summary>What they do for a living, as it is said after "a".</summary>
        public static readonly string[] Jobs =
        {
            "reallife_social_job_barista", "reallife_social_job_nurse", "reallife_social_job_teacher", "reallife_social_job_mechanic", "reallife_social_job_cook",
            "reallife_social_job_accountant", "reallife_social_job_bartender", "reallife_social_job_cabbie", "reallife_social_job_designer",
            "reallife_social_job_electrician", "reallife_social_job_librarian", "reallife_social_job_courier", "reallife_social_job_trainer",
            "reallife_social_job_florist", "reallife_social_job_reporter", "reallife_social_job_retired"
        };

        public static readonly string[] TraitNames =
        {
            "reallife_social_trait_punctual", "reallife_social_trait_late", "reallife_social_trait_generous", "reallife_social_trait_careful",
            "reallife_social_trait_sporty", "reallife_social_trait_foodie", "reallife_social_trait_nightowl", "reallife_social_trait_homebody",
            "reallife_social_trait_reliable", "reallife_social_trait_flaky", "reallife_social_trait_supportive", "reallife_social_trait_envious"
        };

        public static readonly string[] StageNames =
        {
            "reallife_social_stage_stranger", "reallife_social_stage_acquaintance", "reallife_social_stage_friend", "reallife_social_stage_close",
            "reallife_social_stage_dating", "reallife_social_stage_partner"
        };
        public const string StageFamily = "reallife_social_stage_family";
        public const string StageLivesWith = "reallife_social_stage_lives_with";
        public const string StageDrifted = "reallife_social_stage_drifted";

        public static readonly string[] MoodNames =
        {
            "reallife_social_mood_rough", "reallife_social_mood_low", "reallife_social_mood_fine", "reallife_social_mood_good", "reallife_social_mood_great"
        };

        /// <summary>How each origin is described under the contact's name; Mom and Alex keep the descriptions they always had.</summary>
        public static readonly string[] OriginDesc =
        {
            "reallife_contact_mom_desc", "reallife_contact_friend_desc", "reallife_social_desc_gym", "reallife_social_desc_work", "reallife_social_desc_cart",
            "reallife_social_desc_neighbour", "reallife_social_desc_helped", "reallife_social_desc_customer", "reallife_social_desc_online", "reallife_social_desc_event"
        };

        /// <summary>The first text in a new contact's thread.</summary>
        public static readonly string[] OriginWelcome =
        {
            "reallife_text_mom_welcome", "reallife_text_friend_welcome", "reallife_social_welcome_gym", "reallife_social_welcome_work", "reallife_social_welcome_cart",
            "reallife_social_welcome_neighbour", "reallife_social_welcome_helped", "reallife_social_welcome_customer", "reallife_social_welcome_online", "reallife_social_welcome_event"
        };

        /// <summary>What they say when they walk up, two ways each, by origin (from Gym on).</summary>
        public static readonly string[][] MeetOpen =
        {
            null, null,
            new[] { "reallife_social_meet_gym_1", "reallife_social_meet_gym_2" },
            new[] { "reallife_social_meet_work_1", "reallife_social_meet_work_2" },
            new[] { "reallife_social_meet_cart_1", "reallife_social_meet_cart_2" },
            new[] { "reallife_social_meet_neighbour_1", "reallife_social_meet_neighbour_2" },
            new[] { "reallife_social_meet_helped_1", "reallife_social_meet_helped_2" },
            new[] { "reallife_social_meet_customer_1", "reallife_social_meet_customer_2" },
            new[] { "reallife_social_meet_online_1", "reallife_social_meet_online_2" },
            new[] { "reallife_social_meet_event_1", "reallife_social_meet_event_2" }
        };

        // The pairs of traits a person can have: never two that contradict each other.
        private static readonly SocialTrait[][] TraitPairs =
        {
            new[] { SocialTrait.Punctual, SocialTrait.Sporty }, new[] { SocialTrait.Late, SocialTrait.Generous }, new[] { SocialTrait.Foodie, SocialTrait.Supportive },
            new[] { SocialTrait.NightOwl, SocialTrait.Flaky }, new[] { SocialTrait.Homebody, SocialTrait.Reliable }, new[] { SocialTrait.Careful, SocialTrait.Envious },
            new[] { SocialTrait.Generous, SocialTrait.NightOwl }, new[] { SocialTrait.Reliable, SocialTrait.Foodie }, new[] { SocialTrait.Sporty, SocialTrait.Supportive },
            new[] { SocialTrait.Punctual, SocialTrait.Careful }, new[] { SocialTrait.Late, SocialTrait.Homebody }, new[] { SocialTrait.Flaky, SocialTrait.Generous }
        };

        /// <summary>
        /// Rolls the cast of a new save: Mom, Alex and six people to be met, one for each place
        /// somebody can be met. Deterministic for a seed.
        /// </summary>
        public static void MakeCast(SocialPerson[] people, int seed, int day, int daysPerYear, int districts)
        {
            var random = new Random(seed);
            daysPerYear = Math.Max(8, daysPerYear);
            districts = Math.Max(1, districts);

            // Names are dealt without replacement, so no two share a first name or a surname.
            int[] first = Shuffled(random, Math.Min(Women.Length, Men.Length));
            int[] last = Shuffled(random, Surnames.Length);
            int[] pairs = Shuffled(random, TraitPairs.Length);
            int[] jobs = Shuffled(random, Jobs.Length - 1);   // the last job is "retired": Mom's
            int romances = 0;

            for (int i = 0; i < people.Length; i++)
            {
                var origin = (SocialOrigin)i;
                var p = new SocialPerson { Index = i, Origin = origin, Seed = random.Next(1, int.MaxValue) };
                p.Female = origin == SocialOrigin.Family || random.Next(2) == 0;
                p.Traits = TraitPairs[pairs[i]][0] | TraitPairs[pairs[i]][1];
                p.District = random.Next(districts);
                p.Birthday = 1 + random.Next(daysPerYear);
                p.Wants = (PlanKind)random.Next((int)PlanKind.Walk + 1);
                p.WantsDay = day;
                p.Mood = 0;
                switch (origin)
                {
                    case SocialOrigin.Family:
                        p.Name = "Mom";
                        p.Age = 56 + random.Next(10);
                        p.Job = Jobs.Length - 1;
                        p.Traits = SocialTrait.Supportive | SocialTrait.Reliable | SocialTrait.Homebody;
                        p.FreeFrom = 9;
                        p.FreeTo = 21;
                        p.Met = true;
                        p.MetDay = 0;
                        p.Familiarity = 80f;
                        p.Affection = 70f;
                        p.Trust = 75f;
                        p.Stage = SocialStage.Friend;
                        p.Shared = 9;
                        break;
                    case SocialOrigin.OldFriend:
                        p.Name = "Alex";
                        p.Age = 24 + random.Next(14);
                        p.Job = jobs[i];
                        p.Met = true;
                        p.MetDay = 0;
                        p.Familiarity = 55f;
                        p.Affection = 45f;
                        p.Trust = 45f;
                        p.Stage = SocialStage.Friend;
                        p.Shared = 4;
                        p.FreeFrom = 17;
                        p.FreeTo = 24;
                        break;
                    default:
                        p.Name = (p.Female ? Women[first[i]] : Men[first[i]]) + " " + Surnames[last[i]];
                        p.Age = 22 + random.Next(34);
                        p.Job = origin == SocialOrigin.Gym && random.Next(3) == 0 ? Array.IndexOf(Jobs, "reallife_social_job_trainer") : jobs[i];
                        if (origin == SocialOrigin.Gym)
                        {
                            p.Traits |= SocialTrait.Sporty;
                        }
                        // Three ways a week is laid out: office hours, shifts that end in the afternoon, nights.
                        int shape = p.Has(SocialTrait.NightOwl) ? 2 : random.Next(3);
                        p.FreeFrom = shape == 0 ? 17 : shape == 1 ? 14 : 11;
                        p.FreeTo = shape == 0 ? 23 : shape == 1 ? 22 : 18;
                        // Half of the people met could become more than friends, never fewer than two.
                        p.Compatible = (p.Age < 50 && random.Next(2) == 0) || (romances < 2 && i >= people.Length - 2);
                        if (p.Compatible)
                        {
                            romances++;
                        }
                        break;
                }
                p.LastContactDay = day;
                p.StageDay = day;
                people[i] = p;
            }
        }

        private static int[] Shuffled(Random random, int count)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }
            for (int i = count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int swap = order[i];
                order[i] = order[j];
                order[j] = swap;
            }
            return order;
        }

        // ------------------------------------------------------------------ things to do together

        public static readonly OutingDef[] Outings =
        {
            new OutingDef
            {
                Kind = PlanKind.Coffee, Name = "reallife_social_outing_coffee", Offer = "reallife_social_offer_coffee", Match = new[] { "coffeeshop" },
                Cost = 6, Minutes = 25, From = 7, To = 19, Energy = 6f, Familiarity = 6f, Affection = 5f, Trust = 2f,
                Caption = "reallife_social_tm_coffee", Transaction = "reallife_transaction_social_coffee", Memory = MemoryCode.Coffee,
                After = new[] { "reallife_social_after_coffee_1", "reallife_social_after_coffee_2", "reallife_social_after_coffee_3" }, Invite = "reallife_social_invite_coffee"
            },
            new OutingDef
            {
                Kind = PlanKind.Meal, Name = "reallife_social_outing_meal", Offer = "reallife_social_offer_meal", Match = new[] { "restaurant" },
                Cost = 24, Minutes = 70, From = 11, To = 23, Energy = 10f, Familiarity = 8f, Affection = 7f, Trust = 3f,
                Caption = "reallife_social_tm_meal", Transaction = "reallife_transaction_social_meal", Memory = MemoryCode.Meal,
                After = new[] { "reallife_social_after_meal_1", "reallife_social_after_meal_2", "reallife_social_after_meal_3" }, Invite = "reallife_social_invite_meal"
            },
            new OutingDef
            {
                Kind = PlanKind.Drinks, Name = "reallife_social_outing_drinks", Offer = "reallife_social_offer_drinks", Match = new[] { "nightclub" },
                Cost = 32, Minutes = 110, From = 19, To = 27, Energy = -6f, Familiarity = 8f, Affection = 8f, Trust = 2f, Needs = SocialStage.Friend,
                Caption = "reallife_social_tm_drinks", Transaction = "reallife_transaction_social_drinks", Memory = MemoryCode.Drinks,
                After = new[] { "reallife_social_after_drinks_1", "reallife_social_after_drinks_2", "reallife_social_after_drinks_3" }, Invite = "reallife_social_invite_drinks"
            },
            new OutingDef
            {
                Kind = PlanKind.Cinema, Name = "reallife_social_outing_cinema", Offer = "reallife_social_offer_cinema", Match = new[] { "cinema", "theater" },
                Cost = 18, Minutes = 130, From = 12, To = 24, Energy = 2f, Familiarity = 7f, Affection = 6f, Trust = 2f,
                Caption = "reallife_social_tm_cinema", Transaction = "reallife_transaction_social_cinema", Memory = MemoryCode.Cinema,
                After = new[] { "reallife_social_after_cinema_1", "reallife_social_after_cinema_2", "reallife_social_after_cinema_3" }, Invite = "reallife_social_invite_cinema"
            },
            new OutingDef
            {
                Kind = PlanKind.Walk, Name = "reallife_social_outing_walk", Offer = "reallife_social_offer_walk", Match = null,
                Cost = 0, Minutes = 0, From = 7, To = 22, Energy = -3f, Familiarity = 7f, Affection = 6f, Trust = 3f,
                Caption = null, Transaction = null, Memory = MemoryCode.Walk,
                After = new[] { "reallife_social_after_walk_1", "reallife_social_after_walk_2", "reallife_social_after_walk_3" }, Invite = "reallife_social_invite_walk"
            },
            new OutingDef
            {
                Kind = PlanKind.Drive, Name = "reallife_social_outing_drive", Offer = "reallife_social_offer_drive", Match = null,
                Cost = 0, Minutes = 0, From = 8, To = 24, Energy = 0f, Familiarity = 7f, Affection = 6f, Trust = 4f, Needs = SocialStage.Friend,
                Caption = null, Transaction = null, Memory = MemoryCode.Drive,
                After = new[] { "reallife_social_after_drive_1", "reallife_social_after_drive_2", "reallife_social_after_drive_3" }, Invite = "reallife_social_invite_drive"
            },
            new OutingDef
            {
                Kind = PlanKind.Workout, Name = "reallife_social_outing_workout", Offer = "reallife_social_offer_workout", Match = new[] { "gym" },
                Cost = 15, Minutes = 60, From = 6, To = 22, Energy = -14f, Familiarity = 8f, Affection = 5f, Trust = 4f,
                Caption = "reallife_social_tm_workout", Transaction = "reallife_transaction_social_workout", Memory = MemoryCode.Workout,
                After = new[] { "reallife_social_after_workout_1", "reallife_social_after_workout_2", "reallife_social_after_workout_3" }, Invite = "reallife_social_invite_workout"
            },
            new OutingDef
            {
                Kind = PlanKind.Business, Name = "reallife_social_outing_business", Offer = "reallife_social_offer_business", Match = null,
                Cost = 0, Minutes = 30, From = 8, To = 22, Energy = 0f, Familiarity = 6f, Affection = 6f, Trust = 5f, Needs = SocialStage.Friend,
                Caption = "reallife_social_tm_business", Transaction = null, Memory = MemoryCode.Business,
                After = new[] { "reallife_social_after_business_1", "reallife_social_after_business_2", "reallife_social_after_business_3" }, Invite = "reallife_social_invite_business"
            },
            new OutingDef
            {
                Kind = PlanKind.Lift, Name = "reallife_social_outing_lift", Offer = "reallife_social_offer_lift", Match = null,
                Cost = 0, Minutes = 0, From = 0, To = 24, Energy = 0f, Familiarity = 4f, Affection = SocialRules.FavourAffection, Trust = SocialRules.FavourTrust,
                Caption = null, Transaction = "reallife_transaction_social_lift", Memory = MemoryCode.Lift,
                After = new[] { "reallife_social_after_lift_1", "reallife_social_after_lift_2", "reallife_social_after_lift_3" }, Invite = "reallife_social_invite_lift"
            },
            new OutingDef
            {
                Kind = PlanKind.Moving, Name = "reallife_social_outing_moving", Offer = "reallife_social_offer_moving", Match = null,
                Cost = 0, Minutes = 0, From = 0, To = 24, Energy = -12f, Familiarity = 6f, Affection = SocialRules.FavourAffection + 2f, Trust = SocialRules.FavourTrust + 2f,
                Caption = null, Transaction = "reallife_transaction_social_moving", Memory = MemoryCode.Moving,
                After = new[] { "reallife_social_after_moving_1", "reallife_social_after_moving_2", "reallife_social_after_moving_3" }, Invite = "reallife_social_invite_moving"
            }
        };

        public static OutingDef Outing(PlanKind kind) => Outings[(int)kind];

        /// <summary>What a memory is called on the People page.</summary>
        public static string MemoryName(MemoryCode code)
        {
            switch (code)
            {
                case MemoryCode.Met: return "reallife_social_memory_met";
                case MemoryCode.Coffee: return "reallife_social_memory_coffee";
                case MemoryCode.Meal: return "reallife_social_memory_meal";
                case MemoryCode.Drinks: return "reallife_social_memory_drinks";
                case MemoryCode.Cinema: return "reallife_social_memory_cinema";
                case MemoryCode.Walk: return "reallife_social_memory_walk";
                case MemoryCode.Drive: return "reallife_social_memory_drive";
                case MemoryCode.Workout: return "reallife_social_memory_workout";
                case MemoryCode.Business: return "reallife_social_memory_business";
                case MemoryCode.Lift: return "reallife_social_memory_lift";
                case MemoryCode.Moving: return "reallife_social_memory_moving";
                case MemoryCode.Loan: return "reallife_social_memory_loan";
                case MemoryCode.Borrowed: return "reallife_social_memory_borrowed";
                case MemoryCode.Birthday: return "reallife_social_memory_birthday";
                case MemoryCode.StoodUp: return "reallife_social_memory_stoodup";
                case MemoryCode.Late: return "reallife_social_memory_late";
                case MemoryCode.Rude: return "reallife_social_memory_rude";
                case MemoryCode.Arrest: return "reallife_social_memory_arrest";
                case MemoryCode.Crash: return "reallife_social_memory_crash";
                case MemoryCode.AskedOut: return "reallife_social_memory_askedout";
                case MemoryCode.Official: return "reallife_social_memory_official";
                case MemoryCode.MovedIn: return "reallife_social_memory_movedin";
                case MemoryCode.BreakUp: return "reallife_social_memory_breakup";
                case MemoryCode.Introduced: return "reallife_social_memory_introduced";
                case MemoryCode.DroppedBy: return "reallife_social_memory_droppedby";
                case MemoryCode.NotRepaid: return "reallife_social_memory_notrepaid";
                case MemoryCode.Posted: return "reallife_social_memory_posted";
                default: return "reallife_social_memory_met";
            }
        }

        // ------------------------------------------------------------------ on the phone

        public static readonly string[] CallAcquaintance = { "reallife_social_call_acq_1", "reallife_social_call_acq_2", "reallife_social_call_acq_3" };
        public static readonly string[] CallFriend = { "reallife_social_call_friend_1", "reallife_social_call_friend_2", "reallife_social_call_friend_3", "reallife_social_call_friend_4" };
        public static readonly string[] CallClose = { "reallife_social_call_close_1", "reallife_social_call_close_2", "reallife_social_call_close_3" };
        public static readonly string[] CallLove = { "reallife_social_call_love_1", "reallife_social_call_love_2", "reallife_social_call_love_3", "reallife_social_call_love_4" };
        public static readonly string[] CallMom = { "reallife_social_call_mom_1", "reallife_social_call_mom_2", "reallife_social_call_mom_3" };
        public static readonly string[] CallAgain = { "reallife_social_call_again_1", "reallife_social_call_again_2", "reallife_social_call_again_3" };
        public static readonly string[] CallDrifted = { "reallife_social_call_drifted_1", "reallife_social_call_drifted_2" };
        public static readonly string[] Voicemail = { "reallife_social_voicemail_1", "reallife_social_voicemail_2" };
        public static readonly string[] RingOpen = { "reallife_social_ring_open_1", "reallife_social_ring_open_2", "reallife_social_ring_open_3" };
        public static readonly string[] ChatBye = { "reallife_social_chat_bye_1", "reallife_social_chat_bye_2", "reallife_social_chat_bye_3", "reallife_social_chat_bye_4" };
        public static readonly string[] ChatByeMom = { "reallife_social_chat_bye_mom_1", "reallife_social_chat_bye_mom_2" };
        public static readonly string[] PlanYes = { "reallife_social_plan_yes_1", "reallife_social_plan_yes_2", "reallife_social_plan_yes_3" };
        public static readonly string[] PlanBusy = { "reallife_social_plan_busy_1", "reallife_social_plan_busy_2" };
        public static readonly string[] PlanMood = { "reallife_social_plan_mood_1", "reallife_social_plan_mood_2" };
        public static readonly string[] PlanCancelled = { "reallife_social_plan_cancelled_1", "reallife_social_plan_cancelled_2" };
        public static readonly string[] AskOutYes = { "reallife_social_askout_yes_1", "reallife_social_askout_yes_2" };
        public static readonly string[] AskOutNo = { "reallife_social_askout_no_1", "reallife_social_askout_no_2" };
        public static readonly string[] BirthdayThanks = { "reallife_social_birthday_thanks_1", "reallife_social_birthday_thanks_2", "reallife_social_birthday_thanks_3" };
        public static readonly string[] LoanAsk = { "reallife_social_loan_ask_1", "reallife_social_loan_ask_2" };
        public static readonly string[] LoanThanks = { "reallife_social_loan_thanks_1", "reallife_social_loan_thanks_2" };
        public static readonly string[] LoanRefused = { "reallife_social_loan_refused_1", "reallife_social_loan_refused_2" };
        public static readonly string[] BorrowYes = { "reallife_social_borrow_yes_1", "reallife_social_borrow_yes_2" };
        public static readonly string[] BorrowNo = { "reallife_social_borrow_no_1", "reallife_social_borrow_no_2" };

        /// <summary>
        /// Everything there is to talk about. Which of them can come up in a given call is decided
        /// by what really happened (SocialTalk.Fits); the ids are the ones that method switches on.
        /// </summary>
        public const int TopicCrash = 0, TopicArrest = 1, TopicAccident = 2, TopicCrime = 3, TopicShopGood = 4, TopicShopBad = 5, TopicWork = 6,
            TopicGoodDay = 7, TopicBadDay = 8, TopicTheirJob = 9, TopicRain = 10, TopicFine = 11, TopicMemory = 12, TopicBirthday = 13, TopicRich = 14,
            TopicSick = 15, TopicBreakIn = 16, TopicKnocked = 17, TopicDistrict = 18, TopicQuiet = 19;

        public static readonly SocialTopic[] Topics =
        {
            new SocialTopic { Id = TopicCrash, Open = new[] { "reallife_social_topic_crash_1", "reallife_social_topic_crash_2" },
                ReplyA = "reallife_social_topic_crash_a", ReplyB = "reallife_social_topic_crash_b", ReactA = "reallife_social_topic_crash_ra", ReactB = "reallife_social_topic_crash_rb", BothFine = true },
            new SocialTopic { Id = TopicArrest, Open = new[] { "reallife_social_topic_arrest_1", "reallife_social_topic_arrest_2" },
                ReplyA = "reallife_social_topic_arrest_a", ReplyB = "reallife_social_topic_arrest_b", ReactA = "reallife_social_topic_arrest_ra", ReactB = "reallife_social_topic_arrest_rb" },
            new SocialTopic { Id = TopicAccident, Open = new[] { "reallife_social_topic_accident_1", "reallife_social_topic_accident_2" },
                ReplyA = "reallife_social_topic_accident_a", ReplyB = "reallife_social_topic_accident_b", ReactA = "reallife_social_topic_accident_ra", ReactB = "reallife_social_topic_accident_rb", BothFine = true },
            new SocialTopic { Id = TopicCrime, Open = new[] { "reallife_social_topic_crime_1", "reallife_social_topic_crime_2" },
                ReplyA = "reallife_social_topic_crime_a", ReplyB = "reallife_social_topic_crime_b", ReactA = "reallife_social_topic_crime_ra", ReactB = "reallife_social_topic_crime_rb", BothFine = true },
            new SocialTopic { Id = TopicShopGood, Open = new[] { "reallife_social_topic_shopgood_1", "reallife_social_topic_shopgood_2" },
                ReplyA = "reallife_social_topic_shopgood_a", ReplyB = "reallife_social_topic_shopgood_b", ReactA = "reallife_social_topic_shopgood_ra", ReactB = "reallife_social_topic_shopgood_rb" },
            new SocialTopic { Id = TopicShopBad, Open = new[] { "reallife_social_topic_shopbad_1", "reallife_social_topic_shopbad_2" },
                ReplyA = "reallife_social_topic_shopbad_a", ReplyB = "reallife_social_topic_shopbad_b", ReactA = "reallife_social_topic_shopbad_ra", ReactB = "reallife_social_topic_shopbad_rb" },
            new SocialTopic { Id = TopicWork, Open = new[] { "reallife_social_topic_work_1", "reallife_social_topic_work_2" },
                ReplyA = "reallife_social_topic_work_a", ReplyB = "reallife_social_topic_work_b", ReactA = "reallife_social_topic_work_ra", ReactB = "reallife_social_topic_work_rb", BothFine = true },
            new SocialTopic { Id = TopicGoodDay, Open = new[] { "reallife_social_topic_goodday_1", "reallife_social_topic_goodday_2", "reallife_social_topic_goodday_3" },
                ReplyA = "reallife_social_topic_goodday_a", ReplyB = "reallife_social_topic_goodday_b", ReactA = "reallife_social_topic_goodday_ra", ReactB = "reallife_social_topic_goodday_rb" },
            new SocialTopic { Id = TopicBadDay, Open = new[] { "reallife_social_topic_badday_1", "reallife_social_topic_badday_2", "reallife_social_topic_badday_3" },
                ReplyA = "reallife_social_topic_badday_a", ReplyB = "reallife_social_topic_badday_b", ReactA = "reallife_social_topic_badday_ra", ReactB = "reallife_social_topic_badday_rb" },
            new SocialTopic { Id = TopicTheirJob, Open = new[] { "reallife_social_topic_theirjob_1", "reallife_social_topic_theirjob_2" },
                ReplyA = "reallife_social_topic_theirjob_a", ReplyB = "reallife_social_topic_theirjob_b", ReactA = "reallife_social_topic_theirjob_ra", ReactB = "reallife_social_topic_theirjob_rb" },
            new SocialTopic { Id = TopicRain, Open = new[] { "reallife_social_topic_rain_1", "reallife_social_topic_rain_2" },
                ReplyA = "reallife_social_topic_rain_a", ReplyB = "reallife_social_topic_rain_b", ReactA = "reallife_social_topic_rain_ra", ReactB = "reallife_social_topic_rain_rb", BothFine = true },
            new SocialTopic { Id = TopicFine, Open = new[] { "reallife_social_topic_fine_1", "reallife_social_topic_fine_2" },
                ReplyA = "reallife_social_topic_fine_a", ReplyB = "reallife_social_topic_fine_b", ReactA = "reallife_social_topic_fine_ra", ReactB = "reallife_social_topic_fine_rb", BothFine = true },
            new SocialTopic { Id = TopicMemory, Open = new[] { "reallife_social_topic_memory_1", "reallife_social_topic_memory_2" },
                ReplyA = "reallife_social_topic_memory_a", ReplyB = "reallife_social_topic_memory_b", ReactA = "reallife_social_topic_memory_ra", ReactB = "reallife_social_topic_memory_rb" },
            new SocialTopic { Id = TopicBirthday, Open = new[] { "reallife_social_topic_birthday_1", "reallife_social_topic_birthday_2" },
                ReplyA = "reallife_social_topic_birthday_a", ReplyB = "reallife_social_topic_birthday_b", ReactA = "reallife_social_topic_birthday_ra", ReactB = "reallife_social_topic_birthday_rb" },
            new SocialTopic { Id = TopicRich, Open = new[] { "reallife_social_topic_rich_1", "reallife_social_topic_rich_2" },
                ReplyA = "reallife_social_topic_rich_a", ReplyB = "reallife_social_topic_rich_b", ReactA = "reallife_social_topic_rich_ra", ReactB = "reallife_social_topic_rich_rb" },
            new SocialTopic { Id = TopicSick, Open = new[] { "reallife_social_topic_sick_1", "reallife_social_topic_sick_2" },
                ReplyA = "reallife_social_topic_sick_a", ReplyB = "reallife_social_topic_sick_b", ReactA = "reallife_social_topic_sick_ra", ReactB = "reallife_social_topic_sick_rb", BothFine = true },
            new SocialTopic { Id = TopicBreakIn, Open = new[] { "reallife_social_topic_breakin_1", "reallife_social_topic_breakin_2" },
                ReplyA = "reallife_social_topic_breakin_a", ReplyB = "reallife_social_topic_breakin_b", ReactA = "reallife_social_topic_breakin_ra", ReactB = "reallife_social_topic_breakin_rb", BothFine = true },
            new SocialTopic { Id = TopicKnocked, Open = new[] { "reallife_social_topic_knocked_1", "reallife_social_topic_knocked_2" },
                ReplyA = "reallife_social_topic_knocked_a", ReplyB = "reallife_social_topic_knocked_b", ReactA = "reallife_social_topic_knocked_ra", ReactB = "reallife_social_topic_knocked_rb", BothFine = true },
            new SocialTopic { Id = TopicDistrict, Open = new[] { "reallife_social_topic_district_1", "reallife_social_topic_district_2" },
                ReplyA = "reallife_social_topic_district_a", ReplyB = "reallife_social_topic_district_b", ReactA = "reallife_social_topic_district_ra", ReactB = "reallife_social_topic_district_rb", BothFine = true },
            new SocialTopic { Id = TopicQuiet, Open = new[] { "reallife_social_topic_quiet_1", "reallife_social_topic_quiet_2", "reallife_social_topic_quiet_3" },
                ReplyA = "reallife_social_topic_quiet_a", ReplyB = "reallife_social_topic_quiet_b", ReactA = "reallife_social_topic_quiet_ra", ReactB = "reallife_social_topic_quiet_rb", BothFine = true }
        };

        // ------------------------------------------------------------------ face to face

        public static readonly string[] GreetAcquaintance = { "reallife_social_greet_acq_1", "reallife_social_greet_acq_2", "reallife_social_greet_acq_3" };
        public static readonly string[] GreetFriend = { "reallife_social_greet_friend_1", "reallife_social_greet_friend_2", "reallife_social_greet_friend_3" };
        public static readonly string[] GreetClose = { "reallife_social_greet_close_1", "reallife_social_greet_close_2", "reallife_social_greet_close_3" };
        public static readonly string[] GreetLove = { "reallife_social_greet_love_1", "reallife_social_greet_love_2", "reallife_social_greet_love_3" };
        public static readonly string[] GreetLate = { "reallife_social_greet_late_1", "reallife_social_greet_late_2", "reallife_social_greet_late_3" };
        public static readonly string[] GreetLateEasy = { "reallife_social_greet_late_easy_1", "reallife_social_greet_late_easy_2" };
        public static readonly string[] TheyAreLate = { "reallife_social_they_late_1", "reallife_social_they_late_2", "reallife_social_they_late_3" };
        public static readonly string[] Parting = { "reallife_social_part_1", "reallife_social_part_2", "reallife_social_part_3", "reallife_social_part_4" };
        public static readonly string[] PartingLove = { "reallife_social_part_love_1", "reallife_social_part_love_2" };
        public static readonly string[] WentBadly = { "reallife_social_after_bad_1", "reallife_social_after_bad_2", "reallife_social_after_bad_3" };
        public static readonly string[] WalkTalk = { "reallife_social_walk_talk_1", "reallife_social_walk_talk_2", "reallife_social_walk_talk_3", "reallife_social_walk_talk_4", "reallife_social_walk_talk_5" };
        public static readonly string[] WalkBehind = { "reallife_social_walk_behind_1", "reallife_social_walk_behind_2" };
        public static readonly string[] RideTalk = { "reallife_social_ride_talk_1", "reallife_social_ride_talk_2", "reallife_social_ride_talk_3", "reallife_social_ride_talk_4" };
        public static readonly string[] RideJolt = { "reallife_social_ride_jolt_1", "reallife_social_ride_jolt_2", "reallife_social_ride_jolt_3" };
        public static readonly string[] RideCrash = { "reallife_social_ride_crash_1", "reallife_social_ride_crash_2" };
        public static readonly string[] MovingTalk = { "reallife_social_moving_talk_1", "reallife_social_moving_talk_2", "reallife_social_moving_talk_3" };
        public static readonly string[] LeftBehind = { "reallife_social_left_1", "reallife_social_left_2" };
        public static readonly string[] MeetGaveUp = { "reallife_social_meet_gaveup_1", "reallife_social_meet_gaveup_2" };
        public static readonly string[] MeetNumbers = { "reallife_social_meet_numbers_1", "reallife_social_meet_numbers_2" };
        public static readonly string[] MeetWarm = { "reallife_social_meet_warm_1", "reallife_social_meet_warm_2", "reallife_social_meet_warm_3" };
        public static readonly string[] MeetCool = { "reallife_social_meet_cool_1", "reallife_social_meet_cool_2" };
        public static readonly string[] MeetBrushed = { "reallife_social_meet_brushed_1", "reallife_social_meet_brushed_2" };
        public static readonly string[] Introduces = { "reallife_social_intro_1", "reallife_social_intro_2", "reallife_social_intro_3" };
        public static readonly string[] DropBy = { "reallife_social_dropby_1", "reallife_social_dropby_2", "reallife_social_dropby_3" };
        public static readonly string[] DropByShopGood = { "reallife_social_dropby_good_1", "reallife_social_dropby_good_2" };
        public static readonly string[] DropByShopQuiet = { "reallife_social_dropby_quiet_1", "reallife_social_dropby_quiet_2" };
        public static readonly string[] DropByStreet = { "reallife_social_dropby_street_1", "reallife_social_dropby_street_2" };
        public static readonly string[] Closed = { "reallife_social_closed_1", "reallife_social_closed_2" };

        // ------------------------------------------------------------------ texts

        public static readonly string[] TextNews =
        {
            "reallife_social_text_news_1", "reallife_social_text_news_2", "reallife_social_text_news_3", "reallife_social_text_news_4", "reallife_social_text_news_5",
            "reallife_social_text_news_6", "reallife_social_text_news_7", "reallife_social_text_news_8", "reallife_social_text_news_9", "reallife_social_text_news_10",
            "reallife_social_text_news_11", "reallife_social_text_news_12"
        };
        public static readonly string[] TextBadDay = { "reallife_social_text_badday_1", "reallife_social_text_badday_2", "reallife_social_text_badday_3" };
        public static readonly string[] TextCancel = { "reallife_social_text_cancel_1", "reallife_social_text_cancel_2", "reallife_social_text_cancel_3", "reallife_social_text_cancel_4" };
        public static readonly string[] TextCancelRain = { "reallife_social_text_cancel_rain_1", "reallife_social_text_cancel_rain_2" };
        public static readonly string[] TextStoodUp = { "reallife_social_text_stoodup_1", "reallife_social_text_stoodup_2", "reallife_social_text_stoodup_3", "reallife_social_text_stoodup_4" };
        public static readonly string[] TextThanks = { "reallife_social_text_thanks_1", "reallife_social_text_thanks_2", "reallife_social_text_thanks_3" };
        public static readonly string[] TextBirthdayOwn = { "reallife_social_text_birthday_own_1", "reallife_social_text_birthday_own_2" };
        public static readonly string[] TextBirthdayMom = { "reallife_social_text_birthday_mom_1", "reallife_social_text_birthday_mom_2" };
        public static readonly string[] TextBirthdayMissed = { "reallife_social_text_birthday_missed_1", "reallife_social_text_birthday_missed_2" };
        public static readonly string[] TextRichKind = { "reallife_social_text_rich_kind_1", "reallife_social_text_rich_kind_2" };
        public static readonly string[] TextRichSour = { "reallife_social_text_rich_sour_1", "reallife_social_text_rich_sour_2" };
        public static readonly string[] TextArrestKind = { "reallife_social_text_arrest_kind_1", "reallife_social_text_arrest_kind_2" };
        public static readonly string[] TextArrestStern = { "reallife_social_text_arrest_stern_1", "reallife_social_text_arrest_stern_2" };
        public static readonly string[] TextArrestMom = { "reallife_social_text_arrest_mom_1", "reallife_social_text_arrest_mom_2" };
        public static readonly string[] TextCrash = { "reallife_social_text_crash_1", "reallife_social_text_crash_2" };
        public static readonly string[] TextBreakIn = { "reallife_social_text_breakin_1", "reallife_social_text_breakin_2" };
        public static readonly string[] TextMissedCall = { "reallife_social_text_missed_1", "reallife_social_text_missed_2" };
        public static readonly string[] TextDrift = { "reallife_social_text_drift_1", "reallife_social_text_drift_2", "reallife_social_text_drift_3" };
        public static readonly string[] TextLoanBack = { "reallife_social_text_loan_back_1", "reallife_social_text_loan_back_2" };
        public static readonly string[] TextLoanExcuse = { "reallife_social_text_loan_excuse_1", "reallife_social_text_loan_excuse_2" };
        public static readonly string[] TextDebt = { "reallife_social_text_debt_1", "reallife_social_text_debt_2" };
        public static readonly string[] TextHome = { "reallife_social_text_home_1", "reallife_social_text_home_2", "reallife_social_text_home_3" };
        public static readonly string[] TextLove = { "reallife_social_text_love_1", "reallife_social_text_love_2", "reallife_social_text_love_3" };
        public static readonly string[] TextBreakUp = { "reallife_social_text_breakup_1", "reallife_social_text_breakup_2" };
        public static readonly string[] TextIntro = { "reallife_social_text_intro_1", "reallife_social_text_intro_2" };
        public static readonly string[] TextLiftAsk = { "reallife_social_text_lift_1", "reallife_social_text_lift_2" };
        public static readonly string[] TextLoanAsk = { "reallife_social_text_loan_1", "reallife_social_text_loan_2" };
        public static readonly string[] TextMovingAsk = { "reallife_social_text_moving_1", "reallife_social_text_moving_2" };

        // ------------------------------------------------------------------ the newspaper

        public static readonly string[] NewsAccidentMinor = { "reallife_news_accident_minor_1", "reallife_news_accident_minor_2", "reallife_news_accident_minor_3" };
        public static readonly string[] NewsAccidentModerate = { "reallife_news_accident_moderate_1", "reallife_news_accident_moderate_2", "reallife_news_accident_moderate_3" };
        public static readonly string[] NewsAccidentSevere = { "reallife_news_accident_severe_1", "reallife_news_accident_severe_2", "reallife_news_accident_severe_3" };
        public static readonly string[] NewsPedestrianHurt = { "reallife_news_ped_hurt_1", "reallife_news_ped_hurt_2" };
        public static readonly string[] NewsPedestrianKilled = { "reallife_news_ped_killed_1", "reallife_news_ped_killed_2" };
        public static readonly string[] NewsHitAndRun = { "reallife_news_hitrun_1", "reallife_news_hitrun_2" };
        public static readonly string[] NewsCrime = { "reallife_news_crime_1", "reallife_news_crime_2", "reallife_news_crime_3" };
        public static readonly string[] NewsArrest = { "reallife_news_arrest_1", "reallife_news_arrest_2", "reallife_news_arrest_3" };
        public static readonly string[] NewsTally = { "reallife_news_tally_1", "reallife_news_tally_2" };
        public static readonly string[] NewsQuiet = { "reallife_news_quiet_1", "reallife_news_quiet_2", "reallife_news_quiet_3", "reallife_news_quiet_4" };
        public static readonly string[] NewsRainNow = { "reallife_news_rain_now_1", "reallife_news_rain_now_2" };
        public static readonly string[] NewsRained = { "reallife_news_rained_1", "reallife_news_rained_2" };
        public static readonly string[] NewsDry = { "reallife_news_dry_1", "reallife_news_dry_2", "reallife_news_dry_3" };
        public static readonly string[] NewsShopGood = { "reallife_news_shop_good_1", "reallife_news_shop_good_2", "reallife_news_shop_good_3" };
        public static readonly string[] NewsShopLoss = { "reallife_news_shop_loss_1", "reallife_news_shop_loss_2" };
        public static readonly string[] NewsShopNone = { "reallife_news_shop_none_1", "reallife_news_shop_none_2", "reallife_news_shop_none_3" };
        public static readonly string[] NewsBreakIn = { "reallife_news_breakin_1", "reallife_news_breakin_2" };
        public static readonly string[] NewsCarBreakIn = { "reallife_news_carbreakin_1", "reallife_news_carbreakin_2" };
        public static readonly string[] NewsPlayerCrash = { "reallife_news_playercrash_1", "reallife_news_playercrash_2" };
        public static readonly string[] NewsPlayerArrest = { "reallife_news_playerarrest_1", "reallife_news_playerarrest_2" };
        public static readonly string[] NewsPlayerKnocked = { "reallife_news_playerknocked_1", "reallife_news_playerknocked_2" };
        public static readonly string[] NewsPersonBirthday = { "reallife_news_person_birthday_1", "reallife_news_person_birthday_2" };
        public static readonly string[] NewsPersonJob = { "reallife_news_person_job_1", "reallife_news_person_job_2", "reallife_news_person_job_3" };
        public static readonly string[] NewsPersonSeen = { "reallife_news_person_seen_1", "reallife_news_person_seen_2" };
        public static readonly string[] NewsFiller =
        {
            "reallife_news_filler_1", "reallife_news_filler_2", "reallife_news_filler_3", "reallife_news_filler_4", "reallife_news_filler_5", "reallife_news_filler_6",
            "reallife_news_filler_7", "reallife_news_filler_8", "reallife_news_filler_9", "reallife_news_filler_10", "reallife_news_filler_11", "reallife_news_filler_12",
            "reallife_news_filler_13", "reallife_news_filler_14", "reallife_news_filler_15", "reallife_news_filler_16", "reallife_news_filler_17", "reallife_news_filler_18",
            "reallife_news_filler_19", "reallife_news_filler_20", "reallife_news_filler_21", "reallife_news_filler_22", "reallife_news_filler_23", "reallife_news_filler_24",
            "reallife_news_filler_25", "reallife_news_filler_26", "reallife_news_filler_27", "reallife_news_filler_28", "reallife_news_filler_29", "reallife_news_filler_30",
            "reallife_news_filler_31", "reallife_news_filler_32", "reallife_news_filler_33", "reallife_news_filler_34", "reallife_news_filler_35", "reallife_news_filler_36",
            "reallife_news_filler_37", "reallife_news_filler_38", "reallife_news_filler_39", "reallife_news_filler_40"
        };

        /// <summary>What a crime of the catalogue is called in print ("a mugging").</summary>
        public static string CrimeNoun(string id)
        {
            switch (id)
            {
                case "foot_chase": return "reallife_news_noun_foot_chase";
                case "questioning": return "reallife_news_noun_questioning";
                case "disturbance": return "reallife_news_noun_disturbance";
                case "snatch": return "reallife_news_noun_snatch";
                case "mugging": return "reallife_news_noun_mugging";
                case "assault": return "reallife_news_noun_assault";
                case "graffiti": return "reallife_news_noun_graffiti";
                case "car_breakin": return "reallife_news_noun_car_breakin";
                case "drunk": return "reallife_news_noun_drunk";
                case "street_vendor": return "reallife_news_noun_street_vendor";
                case "panhandler": return "reallife_news_noun_panhandler";
                case "fare_dodger": return "reallife_news_noun_fare_dodger";
                case "drug_deal": return "reallife_news_noun_drug_deal";
                case "car_theft": return "reallife_news_noun_car_theft";
                case "traffic_stop": return "reallife_news_noun_traffic_stop";
                case "reckless_driver": return "reallife_news_noun_reckless_driver";
                case "parking_ticket": return "reallife_news_noun_parking_ticket";
                case "shop_theft_own": return "reallife_news_noun_shoplifter";
                case "shoplifter": return "reallife_news_noun_shoplifter";
                case "holdup": return "reallife_news_noun_holdup";
                case "player_pickpocket": return "reallife_news_noun_pickpocket";
                case "player_mugging": return "reallife_news_noun_mugging";
                default: return "reallife_news_noun_incident";
            }
        }
    }

    /// <summary>
    /// One of several lines for the same moment, never the one said last time for that moment.
    /// The last pick of each pool is remembered for the session by the pool itself (its array),
    /// so two people greeting one after the other do not say the same thing either.
    /// </summary>
    public static class SocialLines
    {
        private static readonly System.Collections.Generic.Dictionary<string[], int> Last = new System.Collections.Generic.Dictionary<string[], int>(64);
        private static readonly Random Dice = new Random();

        public static string Pick(string[] pool)
        {
            if (pool == null || pool.Length == 0)
            {
                return null;
            }
            int index = Dice.Next(pool.Length);
            if (pool.Length > 1 && Last.TryGetValue(pool, out int last) && last == index)
            {
                index = (index + 1 + Dice.Next(pool.Length - 1)) % pool.Length;
            }
            Last[pool] = index;
            return pool[index];
        }

        public static void Forget()
        {
            Last.Clear();
        }
    }
}
