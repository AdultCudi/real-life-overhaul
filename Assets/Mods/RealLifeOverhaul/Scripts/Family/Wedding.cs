using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Entities;
using Helpers;
using UI;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// Planning the wedding: with a real event planning agency at its counter (they take a fee,
    /// send the invitations and see to the flowers), or by the two of you from the phone (then
    /// the invitations are yours to send and the flowers yours to order at a florist). A date, a
    /// venue of a real kind, how grand, flowers, music, catering; a deposit now and the rest on
    /// the day. It can be put back or called off at any time, and nothing is ever left hanging.
    /// </summary>
    internal static class WeddingPlanner
    {
        private static readonly string[] VenueNames =
        {
            "reallife_family_venue_cityhall", "reallife_family_venue_openair", "reallife_family_venue_restaurant", "reallife_family_venue_theatre"
        };

        private static readonly string[] TierNames = { "reallife_family_tier_simple", "reallife_family_tier_classic", "reallife_family_tier_grand" };
        private static readonly int[] CrowdByTier = { 4, 14, 30 };
        private static readonly int[] DaysAhead = { 7, 14, 21 };
        private static readonly string[] MomInvite = { "reallife_family_mom_invited_1", "reallife_family_mom_invited_2" };

        private static readonly List<PersonInfo> Close = new List<PersonInfo>(8);
        private static readonly Venue[] Found = new Venue[4];
        private static readonly bool[] Has = new bool[4];
        private static WeddingPlan _draft;
        private static int _lastMom = -1;

        public static string VenueName(int venue) => VenueNames[Mathf.Clamp(venue, 0, 3)];
        public static string TierName(int tier) => TierNames[Mathf.Clamp(tier, 0, 2)];

        public static string[] KindsOf(int venue)
        {
            return venue == 0 ? FamilyPlaces.CityHall : venue == 2 ? FamilyPlaces.Restaurant : venue == 3 ? FamilyPlaces.Theatre : null;
        }

        // ---------------------------------------------------------------- the figures

        /// <summary>The people who would come: the named ones (Social's close people, or Mom, an old friend and the in-laws) and a crowd by how grand it is.</summary>
        public static int Guests(WeddingPlan plan)
        {
            int named = 0;
            if (Partner.UsesSocial && FamilyBridges.Social.Ready)
            {
                Close.Clear();
                named = Mathf.Clamp(FamilyBridges.Social.ClosePeople(Close, 8), 0, 8);
            }
            if (named == 0)
            {
                named = (PhoneContacts.Mom != null ? 1 : 0) + (PhoneContacts.Friend != null ? 1 : 0) + 2;   // and the in-laws
            }
            return plan.Invited ? named + CrowdByTier[Mathf.Clamp(plan.Tier, 0, 2)] : Mathf.Min(named, 3);
        }

        public static int Total(WeddingPlan plan)
        {
            float sum = FamilyBalance.Venue(Mathf.Clamp(plan.Venue, 0, 3));
            if (plan.Flowers) sum += FamilyBalance.FlowersPrice;
            if (plan.Music) sum += FamilyBalance.MusicPrice;
            if (plan.Catering) sum += FamilyBalance.CateringPerGuest * Guests(plan);
            sum *= FamilyBalance.TierFactor[Mathf.Clamp(plan.Tier, 0, 2)];
            if (plan.Planner) sum *= 1f + FamilyBalance.PlannerFee;
            return Mathf.RoundToInt(sum / 10f) * 10;
        }

        // ---------------------------------------------------------------- the conversation

        public static DialogEntry Start(bool agency)
        {
            FamilySave s = FamilyState.S;
            if (FamilyState.Bond != Bond.Engaged)
            {
                return FamilyTalk.Close(FamilyState.Bond == Bond.Married ? "reallife_family_no_already_married" : "reallife_family_no_not_engaged");
            }
            if (s.Wedding.Active)
            {
                return FamilyTalk.Close("reallife_family_plan_already", PlanFill(s.Wedding));
            }
            _draft = new WeddingPlan { Planner = agency };
            Vector3 player = PlayerHelper.GetCityPosition();
            for (int v = 0; v < 4; v++)
            {
                Has[v] = v == 1 ? Outdoors(out _) : FamilyPlaces.Nearest(KindsOf(v), player, out Found[v]);
            }
            return FamilyTalk.Then(FamilyText.Say(agency ? "reallife_family_plan_intro_agency" : "reallife_family_plan_intro_self"),
                "reallife_family_plan_begin", () => VenueStep(0));
        }

        // A wedding in the open is held where the player stands when they choose it: on the pavement, out of doors.
        private static bool Outdoors(out Vector3 spot)
        {
            spot = default;
            return !BuildingManager.IsInsideBuilding && !PlayerHelper.IsUsingVehicle && CitySpots.Pavement(PlayerHelper.GetPosition(), 3f, out spot);
        }

        private static DialogEntry VenueStep(int index)
        {
            for (int i = 0; i < 4; i++)
            {
                int v = (index + i) % 4;
                if (!Has[v])
                {
                    continue;
                }
                string place = v == 1 ? FamilyText.T("reallife_family_venue_here") : Found[v].Name;
                var fill = Args.Us.With(FamilyText.T(VenueNames[v])).At(place).Cost(FamilyBalance.Venue(v));
                return FamilyTalk.Either(FamilyText.Say("reallife_family_plan_venue", fill), "reallife_family_plan_choose", () =>
                {
                    _draft.Venue = v;
                    if (v == 1)
                    {
                        if (!Outdoors(out Vector3 here))
                        {
                            return FamilyTalk.Close("reallife_family_plan_outdoors_moved");
                        }
                        _draft.X = here.x;
                        _draft.Y = here.y;
                        _draft.Z = here.z;
                    }
                    else
                    {
                        _draft.Street = Found[v].Street;
                        _draft.Number = Found[v].Number;
                    }
                    return TierStep(1);
                }, "reallife_family_plan_other", () => VenueStep(v + 1));
            }
            return FamilyTalk.Close("reallife_family_plan_no_venue");
        }

        private static DialogEntry TierStep(int index)
        {
            int tier = (index % 3 + 3) % 3;
            return FamilyTalk.Either(FamilyText.Say(tier == 0 ? "reallife_family_plan_tier_simple" : tier == 1 ? "reallife_family_plan_tier_classic" : "reallife_family_plan_tier_grand"),
                "reallife_family_plan_choose", () =>
                {
                    _draft.Tier = tier;
                    return Extra(0);
                }, "reallife_family_plan_other", () => TierStep(tier + 1));
        }

        private static DialogEntry Extra(int which)
        {
            switch (which)
            {
                case 0:
                    return FamilyTalk.Either(FamilyText.Say(_draft.Planner ? "reallife_family_plan_flowers_agency" : "reallife_family_plan_flowers_self", Args.Us.Cost(FamilyBalance.FlowersPrice)),
                        "reallife_family_plan_yes", () => { _draft.Flowers = true; return Extra(1); }, "reallife_family_plan_no", () => { _draft.Flowers = false; return Extra(1); });
                case 1:
                    return FamilyTalk.Either(FamilyText.Say("reallife_family_plan_music", Args.Us.Cost(FamilyBalance.MusicPrice)),
                        "reallife_family_plan_yes", () => { _draft.Music = true; return Extra(2); }, "reallife_family_plan_no", () => { _draft.Music = false; return Extra(2); });
                default:
                    return FamilyTalk.Either(FamilyText.Say("reallife_family_plan_catering", Args.Us.Cost(FamilyBalance.CateringPerGuest)),
                        "reallife_family_plan_yes", () => { _draft.Catering = true; return DateStep(1); }, "reallife_family_plan_no", () => { _draft.Catering = false; return DateStep(1); });
            }
        }

        private static DialogEntry DateStep(int index)
        {
            int pick = (index % 3 + 3) % 3;
            int day = TimeHelper.CurrentDay + DaysAhead[pick];
            return FamilyTalk.Either(FamilyText.Say("reallife_family_plan_date", Args.Us.On(FamilyText.When(day))),
                "reallife_family_plan_choose", () =>
                {
                    _draft.Day = day;
                    return Summary();
                }, "reallife_family_plan_other", () => DateStep(pick + 1));
        }

        private static DialogEntry Summary()
        {
            _draft.Invited = true;   // priced for everybody; who really comes depends on the invitations
            int total = Total(_draft);
            int deposit = Mathf.RoundToInt(total * FamilyBalance.Deposit);
            var fill = PlanFill(_draft).Cost(total).Count(Guests(_draft));
            fill.Text = FamilyText.Money(deposit);
            return FamilyTalk.Either(FamilyText.Say("reallife_family_plan_summary", fill), "reallife_family_plan_book", () => Book(deposit),
                "reallife_family_plan_restart", () => VenueStep(0));
        }

        private static DialogEntry Book(int deposit)
        {
            if (!FamilyTalk.Pay(deposit, "reallife_transaction_family_wedding"))
            {
                return null;
            }
            FamilySave s = FamilyState.S;
            FamilyState.MoneyOut(deposit, 4);
            WeddingPlan plan = _draft;
            _draft = null;
            bool wantsFlowers = plan.Flowers;
            plan.Active = true;
            plan.Paid = deposit;
            plan.Invited = plan.Planner;        // an agency sends them; otherwise they are the couple's to send
            plan.Flowers = plan.Planner && wantsFlowers;
            s.Wedding = plan;
            FamilyState.DropErrand(Errands.Wedding);
            FamilyState.DropErrand(Errands.Flowers);
            Errands.Add(Errands.Wedding, plan.Day, FamilyBalance.WeddingFromHour, FamilyBalance.WeddingToHour);
            if (!plan.Planner && wantsFlowers)
            {
                Errands.Add(Errands.Flowers, 0, 0, 24);
            }
            Relatives.MomSays("reallife_family_mom_date_set");
            FamilyDirector.Logger?.Info($"Family: wedding booked for day {plan.Day} (venue {plan.Venue}, tier {plan.Tier}, planner {plan.Planner}).");
            return FamilyTalk.Close(plan.Planner ? "reallife_family_plan_booked_agency" : wantsFlowers ? "reallife_family_plan_booked_self_flowers" : "reallife_family_plan_booked_self", PlanFill(plan));
        }

        public static Args PlanFill(WeddingPlan plan)
        {
            string place = plan.Venue == 1 ? FamilyText.T("reallife_family_venue_outdoors")
                : FamilyPlaces.At(plan.Street, plan.Number, out Venue venue) ? venue.Name : FamilyText.T(VenueNames[Mathf.Clamp(plan.Venue, 0, 3)]);
            return Args.Us.At(place).On(FamilyText.When(plan.Day));
        }

        /// <summary>The test button: a classic wedding with everything, today, with nothing paid yet.</summary>
        public static void Quick(WeddingPlan plan)
        {
            plan.Active = true;
            plan.Tier = 1;
            plan.Planner = true;
            plan.Flowers = true;
            plan.Music = true;
            plan.Catering = true;
            plan.Invited = true;
            plan.Paid = 0;
            plan.ColdFeet = true;
        }

        // ---------------------------------------------------------------- invitations, flowers

        public static bool CanInvite => FamilyState.S.Wedding.Active && !FamilyState.S.Wedding.Invited;

        public static DialogEntry Invite()
        {
            WeddingPlan plan = FamilyState.S.Wedding;
            plan.Invited = true;
            Args fill = PlanFill(plan);
            FamilyText.Message(PhoneContacts.Mom, FamilyText.Pick(MomInvite, ref _lastMom), fill);
            if (PhoneContacts.Friend != null)
            {
                FamilyText.Message(PhoneContacts.Friend, "reallife_family_friend_invited", fill);
            }
            Relatives.InLawsSay("reallife_family_inlaws_invited");
            return FamilyTalk.Close("reallife_family_plan_invited", fill.Count(Guests(plan)));
        }

        /// <summary>At a florist's counter: the flowers for the day are ordered (and paid for with the rest, on the day).</summary>
        public static void OrderFlowers(Errand errand)
        {
            WeddingPlan plan = FamilyState.S.Wedding;
            FamilyState.S.Errands.Remove(errand);
            if (!plan.Active)
            {
                return;
            }
            plan.Flowers = true;
            FamilyText.Good("reallife_family_plan_flowers_ordered", Args.Us.Cost(Mathf.RoundToInt(FamilyBalance.FlowersPrice * FamilyBalance.TierFactor[Mathf.Clamp(plan.Tier, 0, 2)])));
        }

        // ---------------------------------------------------------------- putting it back, calling it off

        public static DialogEntry Postpone()
        {
            WeddingPlan plan = FamilyState.S.Wedding;
            int fee = Mathf.RoundToInt(Total(plan) * FamilyBalance.PostponeFee);
            return FamilyTalk.Either(FamilyText.Say("reallife_family_plan_postpone_ask", Args.Us.Cost(fee)), "reallife_family_plan_postpone_yes", () =>
            {
                if (!FamilyTalk.Pay(fee, "reallife_transaction_family_wedding"))
                {
                    return null;
                }
                FamilyState.MoneyOut(fee, 4);
                PutBack(plan, 7);
                FamilyState.Harmony(-3f);
                return FamilyTalk.Close("reallife_family_plan_postponed", PlanFill(plan));
            }, "reallife_family_plan_keep", () => FamilyTalk.Close("reallife_family_plan_kept", PlanFill(plan)));
        }

        public static void PutBack(WeddingPlan plan, int days)
        {
            plan.Day = Mathf.Max(plan.Day, TimeHelper.CurrentDay) + days;
            plan.Postponed++;
            plan.ColdFeet = false;
            plan.TheirColdFeet = false;
            FamilyState.DropErrand(Errands.Wedding);
            Errands.Add(Errands.Wedding, plan.Day, FamilyBalance.WeddingFromHour, FamilyBalance.WeddingToHour);
            Partner.Tell(FamilyEvent.WeddingPostponed, -2f);
            FamilyPlaces.Unguide();
        }

        public static DialogEntry CancelAsk()
        {
            WeddingPlan plan = FamilyState.S.Wedding;
            return FamilyTalk.Either(FamilyText.Say("reallife_family_plan_cancel_ask", Args.Us.Cost(plan.Paid)), "reallife_family_plan_cancel_yes", () =>
            {
                Cancel(true);
                return FamilyTalk.Close("reallife_family_plan_cancelled");
            }, "reallife_family_plan_keep", () => FamilyTalk.Close("reallife_family_plan_kept", PlanFill(plan)));
        }

        /// <summary>The plan is dropped; what was paid stays paid. The two are still engaged.</summary>
        public static void Cancel(bool byChoice)
        {
            FamilySave s = FamilyState.S;
            if (!s.Wedding.Active)
            {
                return;
            }
            s.Wedding = new WeddingPlan();
            FamilyState.DropErrand(Errands.Wedding);
            FamilyState.DropErrand(Errands.Flowers);
            FamilyPlaces.Unguide();
            if (byChoice)
            {
                FamilyState.Harmony(-15f);
                Partner.Tell(FamilyEvent.WeddingCancelled, -6f, -4f);
                FamilyState.Remember("reallife_family_memory_wedding_off", Partner.Name);
            }
        }

        // ---------------------------------------------------------------- the night before

        public static void Eve(int day, int hour)
        {
            WeddingPlan plan = FamilyState.S.Wedding;
            if (!plan.Active || plan.ColdFeet || day != plan.Day - 1 || hour < 19)
            {
                return;
            }
            plan.ColdFeet = true;
            float roll = Random.value;
            if (roll < 0.30f)
            {
                FamilyDirector.Instance.Ask("reallife_family_coldfeet_header", "reallife_family_coldfeet_mine", Args.Us,
                    "reallife_family_coldfeet_talk", "reallife_family_coldfeet_sleep",
                    () =>
                    {
                        FamilyState.Harmony(5f);
                        FamilyState.S.LastTalkDay = TimeHelper.CurrentDay;
                        FamilyText.Good("reallife_family_coldfeet_talked", Args.Us);
                    },
                    () => FamilyText.Toast(NotificationType.Info, "reallife_family_coldfeet_slept", Args.Us));
            }
            else if (roll < 0.42f)
            {
                plan.TheirColdFeet = true;
                FamilyText.Message(FamilyPhone.PartnerContact, "reallife_family_coldfeet_theirs", Args.Us);
            }
            else
            {
                FamilyText.Message(FamilyPhone.PartnerContact, "reallife_family_eve_message", Args.Us);
            }
        }
    }

    /// <summary>The day itself: beginning the ceremony, what happens if it is missed, and what follows it.</summary>
    internal static class WeddingDay
    {
        private static readonly string[] Receptions = { "reallife_family_reception_simple", "reallife_family_reception_classic", "reallife_family_reception_grand" };
        private static int _stagingFailed;

        /// <summary>The key prompt at the venue.</summary>
        public static void Begin()
        {
            FamilySave s = FamilyState.S;
            WeddingPlan plan = s.Wedding;
            FamilyDirector director = FamilyDirector.Instance;
            if (!plan.Active || FamilyState.Bond != Bond.Engaged)
            {
                FamilyState.DropErrand(Errands.Wedding);
                return;
            }
            if (plan.TheirColdFeet)
            {
                FamilyTalk.Open(() => FamilyTalk.Either(FamilyText.Say("reallife_family_coldfeet_theirs_talk"),
                    "reallife_family_coldfeet_reassure", () =>
                    {
                        plan.TheirColdFeet = false;
                        FamilyState.Harmony(4f);
                        return FamilyTalk.Close("reallife_family_coldfeet_reassured");
                    },
                    "reallife_family_coldfeet_give_time", () =>
                    {
                        WeddingPlanner.PutBack(plan, 7);
                        FamilyState.Harmony(3f);
                        return FamilyTalk.Close("reallife_family_coldfeet_given_time", WeddingPlanner.PlanFill(plan));
                    }));
                return;
            }
            TestRefusal why = director.SceneRefusal(true);
            if (why != TestRefusal.None)
            {
                FamilyText.Toast(NotificationType.Warning, "reallife_family_no_busy", Args.Us);
                return;
            }
            if (_stagingFailed >= 2 || director.Cast.Failed)
            {
                Narrated();
                return;
            }
            if (!director.StartScene(new WeddingScene(), out string refusal))
            {
                _stagingFailed++;
                FamilyText.Toast(NotificationType.Warning, _stagingFailed >= 2 ? "reallife_family_no_room_wedding_small" : refusal ?? "reallife_family_no_room_wedding", Args.Us, 10f);
            }
        }

        /// <summary>No room to set anything out, twice: the ceremony is held all the same, told rather than shown.</summary>
        public static void Narrated()
        {
            FamilyDirector director = FamilyDirector.Instance;
            director.Run(director.Passage("reallife_family_passage_ceremony", 5, Feast, () => Complete(false)));
        }

        public static void Feast()
        {
            if (FamilyState.S.Wedding.Catering)
            {
                EnergyHelper.SetCurrentHunger(100f);
            }
        }

        /// <summary>The window closed without a ceremony. Once is forgiven and the day moves; twice and the booking is lost.</summary>
        public static void Missed()
        {
            FamilySave s = FamilyState.S;
            WeddingPlan plan = s.Wedding;
            if (!plan.Active)
            {
                return;
            }
            plan.Missed++;
            if (plan.Missed <= 1)
            {
                WeddingPlanner.PutBack(plan, 3);
                FamilyState.Harmony(-6f);
                FamilyText.Message(FamilyPhone.PartnerContact, "reallife_family_wedding_missed_once", WeddingPlanner.PlanFill(plan));
                return;
            }
            WeddingPlanner.Cancel(false);
            s.BrokenPromises++;
            FamilyState.Harmony(-14f);
            Partner.Tell(FamilyEvent.WeddingCancelled, -8f, -8f);
            FamilyText.Message(FamilyPhone.PartnerContact, "reallife_family_wedding_missed_twice", Args.Us);
        }

        /// <summary>Married. The bill is settled, the presents counted, the families told; then the honeymoon is offered. With <paramref name="free"/> (the test button) no money moves at all: no bill, no presents.</summary>
        public static void Complete(bool staged, bool free = false)
        {
            FamilySave s = FamilyState.S;
            WeddingPlan plan = s.Wedding;
            if (FamilyState.Bond == Bond.Married)
            {
                return;
            }
            _stagingFailed = 0;
            int guests = WeddingPlanner.Guests(plan);
            int total = WeddingPlanner.Total(plan);
            int balance = free ? 0 : Mathf.Max(0, total - plan.Paid);
            int gifts = Mathf.RoundToInt(guests * FamilyBalance.GiftPerGuest * Mathf.Sqrt(FamilyBalance.TierFactor[Mathf.Clamp(plan.Tier, 0, 2)]) * Random.Range(0.8f, 1.2f) / 10f) * 10;
            if (free)
            {
                gifts = 0;
            }
            else
            {
                FamilyTalk.Move(-balance, "reallife_transaction_family_wedding");
                FamilyState.MoneyOut(balance, 4);
                FamilyTalk.Move(gifts, "reallife_transaction_family_gifts");
                FamilyState.MoneyIn(gifts);
            }
            int tier = Mathf.Clamp(plan.Tier, 0, 2);
            bool music = plan.Music, catering = plan.Catering, flowers = plan.Flowers;

            FamilyState.Bond = Bond.Married;
            s.MarriedDay = TimeHelper.CurrentDay;
            s.HoneymoonAsked = false;   // a new wedding, a new honeymoon
            s.HoneymoonTaken = false;
            s.SinceDay = TimeHelper.CurrentDay;
            s.NextOfKin = Partner.Name;
            s.Wedding = new WeddingPlan();
            s.LastSharedDay = TimeHelper.CurrentDay;
            FamilyState.DropErrand(Errands.Wedding);
            FamilyState.DropErrand(Errands.Flowers);
            FamilyPlaces.Unguide();
            if (!s.LivesIn && FamilyPlaces.Home() != null)
            {
                s.LivesIn = true;   // married people share a home
                s.MovedInDay = TimeHelper.CurrentDay;
                FamilyPlaces.MakeHome(FamilyPlaces.Home());
            }
            FamilyState.Harmony(15f);
            FamilyState.Remember("reallife_family_memory_married", Partner.Name);
            Partner.Tell(FamilyEvent.Married, 10f, 10f);
            FamilyModifiers.Remove(FamilyModifiers.Engaged);
            FamilyModifiers.Remove(FamilyModifiers.Together);
            FamilyModifiers.Add(FamilyModifiers.Married);
            Relatives.MomSays("reallife_family_mom_married");
            Relatives.InLawsSay("reallife_family_inlaws_married");
            FamilyDirector.Logger?.Info($"Family: married ({(staged ? "staged" : "narrated")} ceremony, {guests} guests, cost {total}, gifts {gifts}).");

            // How the party went, in a few sentences that say what was paid for.
            string story = FamilyText.T(Receptions[tier], Args.Us)
                           + " " + FamilyText.T(music ? "reallife_family_reception_music" : "reallife_family_reception_no_music", Args.Us)
                           + " " + FamilyText.T(catering ? "reallife_family_reception_catering" : "reallife_family_reception_no_catering", Args.Us)
                           + (flowers ? " " + FamilyText.T("reallife_family_reception_flowers", Args.Us) : string.Empty)
                           + (free ? string.Empty : "\n" + FamilyText.T("reallife_family_reception_money", new Args { Amount = FamilyText.Money(total), Text = FamilyText.Money(gifts), N = guests.ToString() }));
            FamilyDirector director = FamilyDirector.Instance;
            director.WhenFree(() =>
            {
                if (!FamilyTalk.Open(() => FamilyTalk.Close(FamilyText.Built(story)), AskHoneymoon))
                {
                    FamilyText.Good("reallife_family_married", Args.Us);
                }
            });
        }

        private static void AskHoneymoon()
        {
            FamilySave s = FamilyState.S;
            if (!HoneymoonOpen)
            {
                return;
            }
            s.HoneymoonAsked = true;
            FamilyDirector director = FamilyDirector.Instance;
            director.Ask("reallife_family_honeymoon_header", "reallife_family_honeymoon_body", Args.Us.Cost(FamilyBalance.HoneymoonCost).Count(FamilyBalance.HoneymoonDays),
                "reallife_family_honeymoon_yes", "reallife_family_honeymoon_no",
                () =>
                {
                    if (!HoneymoonOpen)
                    {
                        return;   // taken while the question was up
                    }
                    s.HoneymoonTaken = true;   // from here on it is not offered again; a trip that is called off gives it back (HoneymoonOver)
                    if (FamilyBridges.Travel != null && FamilyBridges.Travel.Honeymoon(Partner.Name, FamilyBalance.HoneymoonDays, went => director.Guarded(() => HoneymoonOver(went))))
                    {
                        return;   // Travel runs the trip its own way and says when it is over
                    }
                    if (!FamilyTalk.Pay(FamilyBalance.HoneymoonCost, "reallife_transaction_family_honeymoon"))
                    {
                        s.HoneymoonTaken = false;   // still on offer from the Family contact
                        return;
                    }
                    FamilyState.MoneyOut(FamilyBalance.HoneymoonCost, 4);
                    director.Run(director.Passage("reallife_family_passage_honeymoon", FamilyBalance.HoneymoonDays * 24, null, () => HoneymoonOver(true)));
                },
                () => FamilyText.Toast(NotificationType.Info, "reallife_family_honeymoon_later", Args.Us));
        }

        /// <summary>On offer from the Family contact: married, not yet taken, and the wedding not longer ago than the window.</summary>
        public static bool HoneymoonOpen => FamilyState.Bond == Bond.Married && !FamilyState.S.HoneymoonTaken
                                            && TimeHelper.CurrentDay - FamilyState.S.MarriedDay <= FamilyBalance.HoneymoonWindowDays;

        public static void OfferHoneymoon()
        {
            AskHoneymoon();
        }

        private static void HoneymoonOver(bool went)
        {
            if (!went)
            {
                FamilyState.S.HoneymoonTaken = false;
                return;
            }
            FamilyState.S.HoneymoonTaken = true;
            FamilyState.Harmony(12f);
            FamilyState.S.LastSharedDay = TimeHelper.CurrentDay;
            FamilyModifiers.Add(FamilyModifiers.Honeymoon);
            FamilyState.Remember("reallife_family_memory_honeymoon", Partner.Name);
            Partner.Tell(FamilyEvent.SharedTime, 8f, 5f);
            FamilyText.Good("reallife_family_honeymoon_back", Args.Us);
        }
    }

    /// <summary>
    /// The ceremony, out of doors at the venue. Set out under a cut to black: the officiant at
    /// the front, the family's guest and a few more in rows, flowers, an arch and a runner by
    /// what was paid for. The player and the partner walk up the aisle; vows with a choice,
    /// rings, the pronouncement, applause, a photograph; then the screen goes dark for the
    /// reception and everybody is taken away unseen. Every place anybody stands was found on
    /// the pavement and tested for room; if there is not room for the essentials, it says so.
    /// </summary>
    internal sealed class WeddingScene : FamilyScene
    {
        /// <summary>The test button's wedding: it costs nothing and brings in nothing.</summary>
        public bool Free;

        private enum Step { Gathering, Aisle, Welcome, Vows, TheirVow, Rings, Pronounce, Applause, Photo, Dance, Leaving, Cut }

        private const int MaxGuestSpots = 6;
        private const int MaxBorrowed = 4;
        private const float RingGap = 1.10f;     // metres apart, facing, that ring_present and ring_receive are made for (CityAssets)
        private const float RingSlack = 0.12f;
        private const string LeadClip = "slow_dance_a";     // the player's half of the first dance (the one who leads)
        private const string FollowClip = "slow_dance_b";   // the partner's: 0.45 m ahead of the leader, turned to face them, started at the same moment
        private const float DanceGap = 0.45f;               // metres apart, facing, that the two dance clips are made for (CityAssets)
        private const float DanceSlack = 0.10f;             // further off than this the clasped hands would be apart: no dance
        private const float DanceSeconds = 8f;              // one turn of the loop

        private static readonly string[] HeartVows = { "reallife_family_vow_heart_1", "reallife_family_vow_heart_2", "reallife_family_vow_heart_3" };
        private static readonly string[] FunnyVows = { "reallife_family_vow_funny_1", "reallife_family_vow_funny_2", "reallife_family_vow_funny_3" };
        private static readonly string[] TheirVows = { "reallife_family_vow_theirs_1", "reallife_family_vow_theirs_2", "reallife_family_vow_theirs_3", "reallife_family_vow_theirs_4" };
        private static int _lastHeart = -1, _lastFunny = -1, _lastTheirs = -1;

        private readonly Vector3[] _guestSpots = new Vector3[MaxGuestSpots];
        private readonly List<CityPerson> _borrowed = new List<CityPerson>(MaxBorrowed);
        private readonly List<Transform> _dressing = new List<Transform>(12);
        private FamilyDirector _d;
        private CityPeople _city;
        private CityPerson _partner, _officiant, _guest;
        private Vector3 _anchor, _dir, _right, _front, _mine, _theirs, _officiantSpot, _foot;
        private int _guestCount;
        private Step _step;
        private float _until;
        private float _giveUp;
        private int _vow;            // 0 not chosen yet, 1 from the heart, 2 to make them laugh
        private float _quietSince;
        private bool _setUp;
        private bool _released;
        private bool _married;
        private bool _closing;       // the partner is taking the step that puts the two a ring's distance apart
        private float _closeFrom;
        private bool _danced;        // the first dance has been offered (done or passed over): once
        private bool _dancing;       // both halves are playing

        public override string Name => "the wedding";

        public override bool Begin(FamilyDirector director, out string refusal)
        {
            _d = director;
            refusal = "reallife_family_no_room_wedding";
            if (FamilyState.Bond != Bond.Engaged || !FamilyState.S.Wedding.Active)
            {
                refusal = "reallife_family_no_not_engaged";
                return false;
            }
            Vector3 player = PlayerHelper.GetPosition();
            if (director.Cast.Failed || !CitySpots.Pavement(player, 3f, out _anchor))
            {
                return false;
            }
            PlayerController controller = PlayerHelper.PlayerController;
            Vector3 facing = controller.Character.transform.forward;
            facing.y = 0f;
            facing = facing.sqrMagnitude > 0.01f ? facing.normalized : Vector3.forward;
            bool along = CitySpots.Along(_anchor, out Vector3 run);
            bool laid = false;
            // Along the pavement the way the player faces, then the other way, then across it.
            for (int i = 0; i < 6 && !laid; i++)
            {
                Vector3 dir;
                switch (i)
                {
                    case 0: dir = along ? (Vector3.Dot(run, facing) >= 0f ? run : -run) : facing; break;
                    case 1: dir = along ? (Vector3.Dot(run, facing) >= 0f ? -run : run) : -facing; break;
                    case 2: dir = facing; break;
                    case 3: dir = Vector3.Cross(Vector3.up, along ? run : facing); break;
                    case 4: dir = -Vector3.Cross(Vector3.up, along ? run : facing); break;
                    default: dir = -facing; break;
                }
                laid = Lay(dir.normalized, 6f) || Lay(dir.normalized, 4.5f);
            }
            if (!laid)
            {
                return false;
            }
            _city = director.GetComponent<CityPeople>();
            _step = Step.Gathering;
            _giveUp = Time.time + 420f;
            if (!director.Run(SetOut()))
            {
                refusal = "reallife_family_no_busy";
                return false;
            }
            refusal = null;
            return true;
        }

        // The places everybody stands for an aisle running from the player towards dir. False unless the essentials all have room.
        private bool Lay(Vector3 dir, float length)
        {
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            Vector3 front = _anchor + dir * length;
            if (!CitySpots.Stand(front - right * 0.55f, 0.32f, out Vector3 mine) || !CitySpots.Stand(front + right * 0.55f, 0.32f, out Vector3 theirs)
                || !CitySpots.Stand(front + dir * 1.25f, 0.35f, out Vector3 officiant))
            {
                return false;
            }
            if (!CitySpots.Stand(_anchor + right * 1.0f, 0.35f, out Vector3 foot) && !CitySpots.Stand(_anchor + right * 1.0f + dir * 0.9f, 0.35f, out foot)
                && !CitySpots.Stand(_anchor + dir * 1.2f + right * 0.6f, 0.35f, out foot))
            {
                return false;
            }
            if (!CitySpots.Reachable(foot, theirs) || !CitySpots.Reachable(_anchor, mine) || Mathf.Abs(front.y - _anchor.y) > 0.6f)
            {
                return false;
            }
            _dir = dir;
            _right = right;
            _front = front;
            _mine = mine;
            _theirs = theirs;
            _officiantSpot = officiant;
            _foot = foot;
            _guestCount = 0;
            for (int row = 0; row < 3 && _guestCount < MaxGuestSpots; row++)
            {
                float back = 2.3f + row * 1.3f;
                if (back > length - 1.6f)
                {
                    break;
                }
                for (int side = 0; side < 4 && _guestCount < MaxGuestSpots; side++)
                {
                    float across = (side < 2 ? 1.45f : 2.35f) * (side % 2 == 0 ? -1f : 1f);
                    if (CitySpots.Stand(front - dir * back + right * across, 0.35f, out Vector3 spot) && CitySpots.Flat(spot - foot) > 1.2f * 1.2f)
                    {
                        _guestSpots[_guestCount++] = spot;
                    }
                }
            }
            return true;
        }

        private IEnumerator SetOut()
        {
            yield return UiFader.Fade(0.5f, "reallife_family_passage_gathering");
            Place();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return UiFader.UnFade();
            _setUp = true;
        }

        private void Place()
        {
            WeddingPlan plan = FamilyState.S.Wedding;
            FamilyCast cast = _d.Cast;
            HomeLife.Leave();
            _partner = cast.Put(0, FamilyLook.PartnerFormal, _foot, _front, NavMeshHelperIds.Npc);
            _officiant = cast.Put(1, FamilyLook.Officiant, _officiantSpot, _front - _dir, NavMeshHelperIds.Npc);
            int next = 0;
            if (_guestCount > 0)
            {
                _guest = cast.Put(2, PhoneContacts.Mom != null ? FamilyLook.Mother : FamilyLook.Guest, _guestSpots[next], _front, NavMeshHelperIds.Npc);
                if (_guest != null)
                {
                    next++;
                }
            }
            // A few more from the living city's own people, when it has any to spare, in the clothes they stand in.
            int wanted = plan.Invited ? (plan.Tier == 0 ? 1 : plan.Tier == 1 ? 3 : MaxBorrowed) : 0;
            while (_city != null && Settings.CityLife && next < _guestCount && _borrowed.Count < wanted && _city.Free > 0)
            {
                CityPerson person = _city.Take(CityLook.Civilian, _guestSpots[next], _front, false);
                next++;
                if (person != null)
                {
                    _borrowed.Add(person);
                }
            }
            // What was paid for is what is there.
            if (plan.Tier >= 1 && CitySpots.OnPavement(_front + _dir * 2.0f, out Vector3 arch))
            {
                Dress(FamilyProps.Arch, arch, -_dir);
            }
            if (plan.Flowers)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 at = _front - _dir * (i < 2 ? 0.2f : 2.0f) + _right * (i % 2 == 0 ? -1.15f : 1.15f) * (i < 2 ? 1.25f : 0.75f);
                    if (i >= 2 && _guestCount == 0)
                    {
                        break;
                    }
                    if (CitySpots.Stand(at, 0.28f, out Vector3 stand) && Apart(stand))
                    {
                        Dress(FamilyProps.Flowers, stand, -_dir);
                    }
                }
            }
            if (plan.Tier >= 2)
            {
                float length = Vector3.Dot(_front - _anchor, _dir);
                for (float along = 1.2f; along < length - 0.8f; along += 1f)
                {
                    if (CitySpots.OnPavement(_anchor + _dir * along, out Vector3 strip) && Mathf.Abs(strip.y - _anchor.y) < 0.15f)
                    {
                        Dress(FamilyProps.Runner, strip, _dir);
                    }
                }
            }
            FamilyDirector.Logger?.Info($"Family: wedding set out (partner {_partner != null}, officiant {_officiant != null}, family guest {_guest != null}, "
                                        + $"{_borrowed.Count} more guests of {_guestCount} places, {_dressing.Count} props).");
        }

        // Not where anybody stands or walks.
        private bool Apart(Vector3 spot)
        {
            if (CitySpots.Flat(spot - _mine) < 0.8f * 0.8f || CitySpots.Flat(spot - _theirs) < 0.8f * 0.8f || CitySpots.Flat(spot - _officiantSpot) < 0.8f * 0.8f
                || CitySpots.Flat(spot - _foot) < 0.9f * 0.9f)
            {
                return false;
            }
            for (int i = 0; i < _guestCount; i++)
            {
                if (CitySpots.Flat(spot - _guestSpots[i]) < 0.8f * 0.8f)
                {
                    return false;
                }
            }
            return true;
        }

        private void Dress(string id, Vector3 at, Vector3 facing)
        {
            Transform prop = _d.Props.Stand(id, at, facing);
            if (prop != null)
            {
                _dressing.Add(prop);
            }
        }

        public override void Tick(float now)
        {
            if (_step == Step.Cut)
            {
                Done = !_d.Busy;
                return;   // the screen is dark
            }
            if (!_setUp)
            {
                if (!_d.Busy)
                {
                    // Setting out was cut off before it finished: nothing is left standing, and the ceremony is told instead.
                    Release();
                    Done = true;
                    WeddingDay.Narrated();
                }
                return;
            }
            Vector3 player = PlayerHelper.GetPosition();
            if (_step != Step.Leaving && (_partner == null || _partner.Lost || _officiant == null || _officiant.Lost))
            {
                if (_step == Step.Gathering && (_partner == null || _officiant == null))
                {
                    // Nobody could be put out at all: told rather than shown.
                    Release();
                    Done = true;
                    WeddingDay.Narrated();
                    return;
                }
                Abandon("reallife_family_wedding_interrupted");
                return;
            }
            if (_step != Step.Leaving && (PlayerHelper.IsUsingVehicle || BuildingManager.IsInsideBuilding || CitySpots.Flat(player - _front) > 45f * 45f || now >= _giveUp))
            {
                Abandon("reallife_family_wedding_walked_off");
                return;
            }
            switch (_step)
            {
                case Step.Gathering:
                    _partner.WalkTo(_theirs);
                    _step = Step.Aisle;
                    break;
                case Step.Aisle:
                    _d.Objective("reallife_family_objective_title", "reallife_family_objective_aisle", Partner.Name);
                    bool there = _partner.Arrived && _partner.Near(_theirs, 0.6f);
                    if (there && _partner.Playing == null && _partner.Speed < 0.05f)
                    {
                        _partner.Stand();
                        _d.Cast.Face(_partner, _mine);
                    }
                    else if (_partner.Arrived && !there)
                    {
                        _partner.WalkTo(_theirs);   // stopped short: on again
                    }
                    // Beside them at the front, on either side: near enough, and not on top of them.
                    if (there && CitySpots.Flat(player - _front) < 1.4f * 1.4f && CitySpots.Flat(player - _partner.Position) > 0.5f * 0.5f)
                    {
                        _d.Body.Bind(PlayerHelper.PlayerController);
                        _d.Body.Hold();
                        Vector3 to = _partner.Position - player;
                        to.y = 0f;
                        if (to.sqrMagnitude > 0.04f)
                        {
                            PlayerHelper.PlayerController.Character.transform.rotation = Quaternion.LookRotation(to);
                        }
                        _d.Cast.Face(_partner, player);
                        // The rings are put on by two clips made for a pair FACING RingGap apart. The player stands where
                        // they chose to; the partner takes the step that makes it so while the officiant welcomes everybody.
                        _closing = Mathf.Abs(Mathf.Sqrt(CitySpots.Flat(player - _partner.Position)) - RingGap) > RingSlack * 0.5f
                                   && _partner.StepTo(player, RingGap);
                        _closeFrom = now;
                        _officiant.Work("talk_a", _d.Cast.Props, 0.3f);
                        FamilyText.Toast(NotificationType.Info, "reallife_family_officiant_welcome", Args.Us, 7f);
                        _step = Step.Welcome;
                        _until = now + 5f;
                    }
                    break;
                case Step.Welcome:
                    if (_closing && ((now - _closeFrom > 0.4f && _partner.Arrived && _partner.Speed < 0.05f) || now >= _until))
                    {
                        _closing = false;
                        _partner.Stand();
                        _d.Cast.Face(_partner, player);
                    }
                    if (now >= _until)
                    {
                        _officiant.Idle(0.3f);
                        _vow = 0;
                        _d.Ask("reallife_family_vow_header", "reallife_family_vow_body", Args.Us, "reallife_family_vow_heart", "reallife_family_vow_funny", () => _vow = 1, () => _vow = 2);
                        _step = Step.Vows;
                        _until = now + 60f;
                    }
                    break;
                case Step.Vows:
                    if (_vow == 0 && now < _until)
                    {
                        if (_d.HasQuestions || HudConfirm.isOpen)
                        {
                            _quietSince = 0f;
                        }
                        else if (_quietSince <= 0f)
                        {
                            _quietSince = now;
                        }
                        else if (now - _quietSince > 2.5f)
                        {
                            _vow = 1;   // the box was closed without an answer: from the heart it is
                        }
                        break;
                    }
                    if (_vow == 0)
                    {
                        _vow = 1;
                    }
                    if (!_d.Body.Clip("talk_b", 0.3f))
                    {
                        _d.Body.Rest();
                    }
                    FamilyText.Toast(NotificationType.Info, _vow == 1 ? FamilyText.Pick(HeartVows, ref _lastHeart) : FamilyText.Pick(FunnyVows, ref _lastFunny), Args.Us, 8f);
                    _step = Step.TheirVow;
                    _until = now + 6f;
                    break;
                case Step.TheirVow:
                    if (now >= _until)
                    {
                        _d.Body.Rest(0.3f);
                        _partner.Work("talk_a", _d.Cast.Props, 0.3f);
                        FamilyText.Toast(NotificationType.Info, FamilyText.Pick(TheirVows, ref _lastTheirs), Args.Us, 8f);
                        _step = Step.Rings;
                        _until = now + 6f;
                    }
                    break;
                case Step.Rings:
                    if (now >= _until)
                    {
                        _partner.Idle(0.3f);
                        // Both halves at the same moment (the same length, the ring on at 1.86 s in both): the player's
                        // hands come under the hand the partner holds out. Only standing as the pair was made for; further
                        // apart or nearer, the partner's hand would be held out at nothing, so the player's half plays alone
                        // as it did before.
                        bool paired = Mathf.Abs(Mathf.Sqrt(CitySpots.Flat(player - _partner.Position)) - RingGap) <= RingSlack
                                      && ClipLibrary.Has("ring_present") && ClipLibrary.Has("ring_receive");
                        if (!_d.Body.Clip("ring_present", 0.3f))
                        {
                            _d.Body.Clip("job_hand_over", 0.3f);   // holds something small out to the person in front: stands in when the bundle has no ring clip
                        }
                        else if (paired)
                        {
                            _partner.Work("ring_receive", _d.Cast.Props, 0.3f);
                        }
                        FamilyText.Toast(NotificationType.Info, "reallife_family_rings_exchanged", Args.Us.With(FamilyText.T(Courtship.RingName(FamilyState.S.Ring))), 6f);
                        _step = Step.Pronounce;
                        _until = now + 3.5f;
                    }
                    break;
                case Step.Pronounce:
                    if (now >= _until)
                    {
                        _d.Body.Rest(0.3f);
                        _officiant.Work("talk_a", _d.Cast.Props, 0.3f);
                        FamilyText.Toast(NotificationType.Success, "reallife_family_officiant_pronounce", Args.Us, 7f);
                        _step = Step.Applause;
                        _until = now + 3.5f;
                    }
                    break;
                case Step.Applause:
                    if (now >= _until)
                    {
                        _married = true;
                        _officiant.Work("clap", _d.Cast.Props, 0.25f);
                        if (_guest != null && !_guest.Lost)
                        {
                            _guest.Work("clap", _d.Cast.Props, 0.25f);
                        }
                        for (int i = 0; i < _borrowed.Count; i++)
                        {
                            if (!_borrowed[i].Lost)
                            {
                                // Every other guest cheers (both fists up; half a metre clear either side, which the rows
                                // give), the rest clap; each starts a little later than the last so that nobody moves in step.
                                bool cheers = i % 2 == 1 && ClipLibrary.Has("cheer_loop") && _borrowed[i].Work("cheer_loop", _d.Cast.Props, 0.25f, ClipEnd.Return, true, i * 0.37f);
                                if (!cheers)
                                {
                                    _borrowed[i].Work("clap", _d.Cast.Props, 0.25f, ClipEnd.Return, true, i * 0.3f);
                                }
                            }
                        }
                        if (!_partner.Work("cheer", _d.Cast.Props, 0.3f))
                        {
                            _partner.Work("wave_hello", _d.Cast.Props, 0.3f);
                        }
                        FamilyModifiers.Add(FamilyModifiers.Joy);
                        _step = Step.Photo;
                        _until = now + 2.2f;
                    }
                    break;
                case Step.Photo:
                    if (now >= _until && !_d.Busy)
                    {
                        _d.Body.Free();
                        _d.Run(_d.Photograph("wedding"));
                        _step = Step.Leaving;
                        _until = now + 2.5f;
                    }
                    break;
                case Step.Dance:
                    Dance(now, player);
                    break;
                case Step.Leaving:
                    if (now >= _until && !_d.Busy)
                    {
                        if (!_danced && BeginDance(now, player))
                        {
                            break;   // the couple's first dance, then the cut to the reception
                        }
                        _step = Step.Cut;
                        if (!_d.Run(_d.Passage("reallife_family_passage_reception", 4, () => { Release(); WeddingDay.Feast(); }, () => WeddingDay.Complete(true, Free))))
                        {
                            Release();
                            WeddingDay.Complete(true, Free);
                            Done = true;
                        }
                    }
                    break;
            }
        }

        // The first dance, once the photograph is taken: the partner steps in to the clips' distance (as for the rings), and both
        // halves start together. False (nothing changed) if the bundle lacks either clip.
        private bool BeginDance(float now, Vector3 player)
        {
            _danced = true;
            if (!ClipLibrary.Has(LeadClip) || !ClipLibrary.Has(FollowClip))
            {
                return false;
            }
            _d.Body.Bind(PlayerHelper.PlayerController);
            _d.Body.Hold();
            FacePartner(player);
            _d.Cast.Face(_partner, player);
            _closing = Mathf.Abs(Mathf.Sqrt(CitySpots.Flat(player - _partner.Position)) - DanceGap) > DanceSlack * 0.5f && _partner.StepTo(player, DanceGap);
            _closeFrom = now;
            _dancing = false;
            _step = Step.Dance;
            _until = now + 4f;   // the longest the step in may take
            return true;
        }

        private void FacePartner(Vector3 player)
        {
            Vector3 to = _partner.Position - player;
            to.y = 0f;
            if (to.sqrMagnitude > 0.04f)
            {
                PlayerHelper.PlayerController.Character.transform.rotation = Quaternion.LookRotation(to);
            }
        }

        // In: waits for the partner to stop, measures the gap that was really reached, and plays the pair only if it is the one
        // they were made for (further off, the clasped hands would be apart). Dancing: one turn of the loop, then both are let go.
        private void Dance(float now, Vector3 player)
        {
            if (!_dancing)
            {
                if (!(now >= _until || (now - _closeFrom > 0.4f && _partner.Arrived && _partner.Speed < 0.05f)))
                {
                    return;
                }
                _partner.Stand();
                _d.Cast.Face(_partner, player);
                FacePartner(player);
                float gap = Mathf.Sqrt(CitySpots.Flat(player - _partner.Position));
                bool fits = Mathf.Abs(gap - DanceGap) <= DanceSlack;
                if (fits && _d.Body.Clip(LeadClip, 0.4f))
                {
                    if (_partner.Work(FollowClip, _d.Cast.Props, 0.4f))
                    {
                        ClipLibrary.Played(LeadClip, "the player");
                        _dancing = true;
                        _until = now + DanceSeconds;
                        FamilyText.Toast(NotificationType.Info, "reallife_family_first_dance", Args.Us, 7f);
                    }
                    else
                    {
                        _d.Body.Rest(0.3f);   // the pair is both or neither
                    }
                }
                if (Meta.Dev.Verbose)
                {
                    FamilyDirector.Logger?.Info("Family: the first dance, " + gap.ToString("F2", CultureInfo.InvariantCulture) + " m apart (made for "
                                                + DanceGap.ToString("F2", CultureInfo.InvariantCulture) + " m, give or take "
                                                + DanceSlack.ToString("F2", CultureInfo.InvariantCulture) + "): " + (_dancing ? "played." : "not played."));
                }
                if (!_dancing)
                {
                    _d.Body.Free();
                    _step = Step.Leaving;
                    _until = now;
                }
                return;
            }
            if (now >= _until)
            {
                _d.Body.Rest(0.4f);
                _partner.Idle(0.4f);
                _d.Body.Free();
                _dancing = false;
                _step = Step.Leaving;
                _until = now;
            }
        }

        // The player left, or somebody was lost: everybody goes under a quick cut, and the day can be begun again while it lasts.
        private void Abandon(string toast)
        {
            _d.Body.Release();
            FamilyText.Toast(NotificationType.Warning, toast, Args.Us, 9f);
            _step = Step.Cut;
            if (_married)
            {
                // Vows said, rings on: married all the same, with the party skipped.
                if (!_d.Run(_d.Passage("reallife_family_passage_reception", 0, Release, () => WeddingDay.Complete(true, Free))))
                {
                    Release();
                    WeddingDay.Complete(true, Free);
                    Done = true;
                }
                return;
            }
            if (!_d.Run(_d.Passage("reallife_family_passage_gathering", 0, Release, null)))
            {
                Release();
                Done = true;
            }
        }

        private void Release()
        {
            if (_released)
            {
                return;
            }
            _released = true;
            _d.Cast.Release(_partner);
            _d.Cast.Release(_officiant);
            _d.Cast.Release(_guest);
            _partner = _officiant = _guest = null;
            for (int i = 0; i < _borrowed.Count; i++)
            {
                if (_city != null)
                {
                    _city.Release(_borrowed[i]);
                }
            }
            _borrowed.Clear();
            for (int i = 0; i < _dressing.Count; i++)
            {
                _d.Props.Return(_dressing[i]);
            }
            _dressing.Clear();
        }

        public override void End()
        {
            if (_d == null)
            {
                return;
            }
            _d.Body.Release();
            Release();
        }
    }
}
