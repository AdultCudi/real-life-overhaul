using System.Collections.Generic;
using Entities;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Family
{
    /// <summary>Planning the wedding at an agency's counter or from the phone: venue, size, extras and date, a deposit now; put back or called off.</summary>
    internal static class WeddingPlanner
    {
        private static readonly string[] VenueNames =
        {
            "reallife_family_venue_cityhall", "reallife_family_venue_openair", "reallife_family_venue_restaurant", "reallife_family_venue_theatre"
        };

        private static readonly int[] CrowdByTier = { 4, 14, 30 };
        private static readonly int[] DaysAhead = { 7, 14, 21 };
        private static readonly string[] MomInvite = { "reallife_family_mom_invited_1", "reallife_family_mom_invited_2" };

        private static readonly List<PersonInfo> Close = new List<PersonInfo>(8);
        private static readonly Venue[] Found = new Venue[4];
        private static readonly bool[] Has = new bool[4];
        private static WeddingPlan _draft;
        private static int _lastMom = -1;

        public static string[] KindsOf(int venue)
        {
            return venue == 0 ? FamilyPlaces.CityHall : venue == 2 ? FamilyPlaces.Restaurant : venue == 3 ? FamilyPlaces.Theatre : null;
        }

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
            if (plan.Flowers)
            {
                sum += FamilyBalance.FlowersPrice;
            }
            if (plan.Music)
            {
                sum += FamilyBalance.MusicPrice;
            }
            if (plan.Catering)
            {
                sum += FamilyBalance.CateringPerGuest * Guests(plan);
            }
            sum *= FamilyBalance.TierFactor[Mathf.Clamp(plan.Tier, 0, 2)];
            if (plan.Planner)
            {
                sum *= 1f + FamilyBalance.PlannerFee;
            }
            return Mathf.RoundToInt(sum / 10f) * 10;
        }

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

        // A wedding in the open is held where the player stands when they choose it, on the pavement.
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
            string booked = plan.Planner ? "reallife_family_plan_booked_agency" : wantsFlowers ? "reallife_family_plan_booked_self_flowers" : "reallife_family_plan_booked_self";
            return FamilyTalk.Close(booked, PlanFill(plan));
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

        /// <summary>At a florist's counter: the flowers are ordered, and paid for with the rest on the day.</summary>
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

        /// <summary>The evening before: cold feet, theirs or the player's, or a text.</summary>
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
}
