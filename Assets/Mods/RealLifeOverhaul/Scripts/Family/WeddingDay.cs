using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Family
{
    /// <summary>The day itself: beginning the ceremony, what happens if it is missed, and what follows it.</summary>
    internal static class WeddingDay
    {
        private static readonly string[] Receptions = { "reallife_family_reception_simple", "reallife_family_reception_classic", "reallife_family_reception_grand" };
        private static int _stagingFailed;

        /// <summary>What the key prompt at the venue does.</summary>
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

        /// <summary>Married: the bill settled, the presents counted, the families told, then the honeymoon offered. Free (the test): no money moves.</summary>
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
                           + (free ? string.Empty
                               : "\n" + FamilyText.T("reallife_family_reception_money", new Args { Amount = FamilyText.Money(total), Text = FamilyText.Money(gifts), N = guests.ToString() }));
            FamilyDirector director = FamilyDirector.Instance;
            director.WhenFree(() =>
            {
                if (!FamilyTalk.Open(() => FamilyTalk.Close(FamilyText.Built(story)), OfferHoneymoon))
                {
                    FamilyText.Good("reallife_family_married", Args.Us);
                }
            });
        }

        /// <summary>The honeymoon, asked as a question; from the Family contact too while it is open.</summary>
        public static void OfferHoneymoon()
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
}
