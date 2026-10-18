using System;
using System.Collections.Generic;
using Dialogs;
using Entities;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>FamilyPhone's part for the things that can be done from a family conversation, each offered while it applies.</summary>
    public static partial class FamilyPhone
    {
        private static List<Choice> _choices;
        private static int _chatAt, _cookAt;   // where the partner's chat and the evening's routines begin

        private static List<Choice> Choices()
        {
            if (_choices != null)
            {
                return _choices;
            }
            var list = new List<Choice>(40);
            Func<Bond> bond = () => FamilyState.Bond;
            Func<FamilySave> s = () => FamilyState.S;

            _chatAt = list.Count;
            list.Add(new Choice(() => !Partner.UsesSocial && !FamilyState.HasPartner && Settings.Family,
                () => FamilyText.Say("reallife_family_do_meet"), "reallife_family_do_meet_yes", PartnerFirst));
            list.Add(new Choice(() => FamilyState.Together && (!Partner.UsesSocial || InPerson) && s().LastChatDay != TimeHelper.CurrentDay,
                () => FamilyText.Say("reallife_family_do_chat"), "reallife_family_do_chat_yes", Chat));
            list.Add(new Choice(() => HomeLife.CanPlanDate, () => FamilyText.Say("reallife_family_do_date", Args.Us.Cost(FamilyBalance.DinnerCost)), "reallife_family_do_date_yes",
                HomeLife.PlanDate));
            _cookAt = list.Count;
            list.Add(new Choice(() => HomeLife.CanCook && InPerson, () => FamilyText.Say("reallife_family_do_cook"), "reallife_family_do_cook_yes", HomeLife.Cook));
            list.Add(new Choice(() => HomeLife.CanWatch && InPerson, () => FamilyText.Say("reallife_family_do_watch"), "reallife_family_do_watch_yes", HomeLife.Watch));
            list.Add(new Choice(() => FamilyState.Together && !s().LivesIn,
                () => FamilyText.Say("reallife_family_do_move_in"), "reallife_family_do_move_in_yes", () =>
                {
                    string why = HomeLife.MoveIn(false);
                    return FamilyTalk.Close(why ?? "reallife_family_move_in_yes");
                }));
            list.Add(new Choice(() => FamilyState.Together && s().LivesIn && BuildingManager.IsInsideBuilding && !FamilyPlaces.AtHome() && InOwnHome(),
                () => FamilyText.Say("reallife_family_do_new_home"), "reallife_family_do_new_home_yes", () =>
                {
                    BuildingRegistration here = InstanceBehavior<BuildingManager>.Instance.buildingRegistration;
                    if (!FamilyPlaces.HasRoom(here, 0))
                    {
                        return FamilyTalk.Close("reallife_family_no_room");
                    }
                    FamilyPlaces.MakeHome(here);
                    return FamilyTalk.Close("reallife_family_new_home_done");
                }));

            list.Add(new Choice(() => bond() == Bond.Dating && s().Ring == 0 && Courtship.Ready() && Settings.FamilyWedding,
                () => FamilyText.Say("reallife_family_do_jeweller"), "reallife_family_do_show_way",
                () => Way(FamilyPlaces.Jeweller, "reallife_family_way_jeweller", () => Courtship.Rings(0))));
            list.Add(new Choice(() => bond() == Bond.Dating && s().Ring > 0 && Settings.FamilyWedding,
                () => FamilyText.Say("reallife_family_do_propose", Args.Us.With(FamilyText.T(Courtship.RingName(s().Ring)))), "reallife_family_do_propose_yes", () =>
                {
                    StageWhenFree(false, () => new ProposalScene(false));
                    return FamilyTalk.Close("reallife_family_propose_called");
                }));

            list.Add(new Choice(() => bond() == Bond.Engaged && !s().Wedding.Active && Settings.FamilyWedding,
                () => FamilyText.Say("reallife_family_do_plan"), "reallife_family_do_plan_yes", () => WeddingPlanner.Start(false)));
            list.Add(new Choice(() => bond() == Bond.Engaged && !s().Wedding.Active && Settings.FamilyWedding,
                () => FamilyText.Say("reallife_family_do_planner"), "reallife_family_do_show_way", () => Way(FamilyPlaces.Planner, "reallife_family_way_planner")));
            list.Add(new Choice(() => WeddingPlanner.CanInvite, () => FamilyText.Say("reallife_family_do_invite"), "reallife_family_do_invite_yes", WeddingPlanner.Invite));
            list.Add(new Choice(() => s().Wedding.Active && FamilyState.FindErrand(Errands.Flowers) != null,
                () => FamilyText.Say("reallife_family_do_florist"), "reallife_family_do_show_way", () => Way(FamilyPlaces.Florist, "reallife_family_way_florist", () =>
                {
                    // No florist in the city: ordered by telephone instead.
                    Errand errand = FamilyState.FindErrand(Errands.Flowers);
                    if (errand != null)
                    {
                        WeddingPlanner.OrderFlowers(errand);
                    }
                    return FamilyTalk.Close("reallife_family_way_florist_phone");
                })));
            list.Add(new Choice(() => s().Wedding.Active, () => FamilyText.Say("reallife_family_do_postpone", WeddingPlanner.PlanFill(s().Wedding)), "reallife_family_do_postpone_yes",
                WeddingPlanner.Postpone));
            list.Add(new Choice(() => s().Wedding.Active, () => FamilyText.Say("reallife_family_do_cancel", WeddingPlanner.PlanFill(s().Wedding)), "reallife_family_do_cancel_yes",
                WeddingPlanner.CancelAsk));
            list.Add(new Choice(() => bond() == Bond.Engaged, () => FamilyText.Say("reallife_family_do_break_off"), "reallife_family_do_break_off_yes",
                () => FamilyTalk.Either(FamilyText.Say("reallife_family_break_off_ask"), "reallife_family_break_off_sure", () =>
                {
                    Courtship.CallOff();
                    return FamilyTalk.Close("reallife_family_break_off_done");
                }, "reallife_family_apart_no", () => FamilyTalk.Close("reallife_family_apart_stayed"))));
            list.Add(new Choice(() => WeddingDay.HoneymoonOpen, () => FamilyText.Say("reallife_family_do_honeymoon", Args.Us.Cost(FamilyBalance.HoneymoonCost)),
                "reallife_family_do_honeymoon_yes", () =>
                {
                    FamilyDirector.Instance.WhenFree(WeddingDay.OfferHoneymoon);
                    return FamilyTalk.Close("reallife_family_honeymoon_asking");
                }));

            list.Add(new Choice(() => Children.CanTalk, () => FamilyText.Say("reallife_family_do_children"), "reallife_family_do_children_yes", Children.Talk));
            list.Add(new Choice(() => FamilyState.AnyUnder(FamilyBalance.SchoolFrom),
                () => FamilyText.Say("reallife_family_do_care", Args.Us.With(FamilyText.T(Children.CareName(FamilyState.Care)))), "reallife_family_do_care_yes",
                () => Children.CareStep(s().CareChoice + 1)));
            list.Add(new Choice(() => FamilyState.Smallest() != null && s().Pushchair,
                () => FamilyText.Say("reallife_family_do_walk", Args.Us.Kid(FamilyState.Smallest().Name)), "reallife_family_do_walk_yes", () =>
                {
                    StageWhenFree(true, () => new WalkScene(false));
                    return FamilyTalk.Close("reallife_family_walk_called");
                }));
            for (int i = 0; i < FamilyBalance.MaxChildren; i++)
            {
                int child = i;
                list.Add(new Choice(() => Has(child) && s().Children[child].Age >= FamilyBalance.PocketFrom,
                    () => FamilyText.Say("reallife_family_do_pocket",
                        Args.Us.Kid(s().Children[child].Name).Cost(FamilyBalance.PocketMoney[Mathf.Clamp(s().Children[child].Pocket, 0, 3)])),
                    "reallife_family_pocket_change", () => Children.PocketStep(child)));
                list.Add(new Choice(() => Has(child),
                    () => FamilyText.Say("reallife_family_do_fund",
                        Args.Us.Kid(s().Children[child].Name).Cost(s().Children[child].Fund).With(FamilyText.Money(FamilyBalance.FundStep))),
                    "reallife_family_do_fund_yes", () => Children.FundStep(child)));
            }
            list.Add(new Choice(() => ShoppingErrand() != null, () => FamilyText.Say("reallife_family_do_shopping"), "reallife_family_do_show_way", () =>
            {
                Errand errand = ShoppingErrand();
                return errand != null ? Way(Errands.PlacesOf(errand.Kind), "reallife_family_way_shop") : FamilyTalk.Close("reallife_family_menu_nothing");
            }));

            list.Add(new Choice(() => Relatives.CanGoToMoms, () => FamilyText.Say("reallife_family_do_moms"), "reallife_family_do_moms_yes", Relatives.GoToMoms));
            list.Add(new Choice(() => Relatives.InLawFavourOpen, () => FamilyText.Say("reallife_family_do_favour"), "reallife_family_do_favour_yes", Relatives.InLawFavour));
            list.Add(new Choice(null, () => FamilyText.Say("reallife_family_do_kin", Args.Us.With(Relatives.NextOfKin())), "reallife_family_kin_change", Relatives.NextOfKinStep));

            list.Add(new Choice(() => HardTimes.CanCounsel && Settings.FamilyFriction, () => FamilyText.Say("reallife_family_do_counselling", Args.Us.Cost(FamilyBalance.CounsellingCost)),
                "reallife_family_do_counselling_yes", HardTimes.Book));
            list.Add(new Choice(() => HardTimes.CanCallApart, () => FamilyText.Say("reallife_family_do_call_apart"), "reallife_family_do_chat_yes", HardTimes.CallApart));
            list.Add(new Choice(() => HardTimes.CanComeHome, () => FamilyText.Say("reallife_family_do_come_home"), "reallife_family_do_come_home_yes", HardTimes.ComeHome));
            list.Add(new Choice(() => FamilyState.Together && s().Harmony < 50f && Settings.FamilyFriction, () => FamilyText.Say("reallife_family_do_apart"), "reallife_family_do_apart_yes",
                HardTimes.AskApart));
            list.Add(new Choice(() => HardTimes.CanEnd, () => FamilyText.Say((Bond)s().BondBefore == Bond.Married ? "reallife_family_do_divorce" : "reallife_family_do_part"),
                "reallife_family_do_end_yes", HardTimes.EndAsk));

            list.Add(new Choice(null, () => FamilyText.Say("reallife_family_do_page"), "reallife_family_do_page_yes", () => FamilyPage.Page(0)));
            _choices = list;
            return _choices;
        }

        // A scene asked for from a conversation: staged once the player is free, or a notice why not.
        private static void StageWhenFree(bool outdoors, Func<FamilyScene> scene)
        {
            FamilyDirector director = FamilyDirector.Instance;
            director.WhenFree(() =>
            {
                TestRefusal why = director.SceneRefusal(outdoors);
                string refusal = why == TestRefusal.Indoors ? "reallife_family_no_walk_indoors" : why != TestRefusal.None ? "reallife_family_no_busy" : null;
                if (refusal == null)
                {
                    director.StartScene(scene(), out refusal);
                }
                if (refusal != null)
                {
                    FamilyText.Toast(NotificationType.Warning, refusal, Args.Us);
                }
            });
        }

        private static bool InPerson => DialogController.current != null && DialogController.current.dialogType != DialogType.PhoneCall && HomeLife.Present;

        private static bool Has(int child)
        {
            return child < FamilyState.S.Children.Count && !FamilyState.S.Children[child].Grown;
        }

        private static bool InOwnHome()
        {
            var manager = InstanceBehavior<BuildingManager>.Instance;
            return manager != null && manager.building != null && GoingPublic.Mine(manager.buildingRegistration) && manager.building.BuildingType == FamilyPlaces.Residential;
        }

        private static Errand ShoppingErrand()
        {
            FamilySave s = FamilyState.S;
            for (int i = 0; i < s.Errands.Count; i++)
            {
                if (s.Errands[i].Day <= 0 && Errands.PlacesOf(s.Errands[i].Kind) != null && s.Errands[i].Kind != Errands.Flowers)
                {
                    return s.Errands[i];
                }
            }
            return null;
        }

        // The nearest of a kind of place, on the game's own destination marker.
        private static DialogEntry Way(string[] kinds, string key, Func<DialogEntry> without = null)
        {
            if (kinds == null || !FamilyPlaces.Nearest(kinds, PlayerHelper.GetCityPosition(), out Venue venue))
            {
                // The city has none: what can be done by telephone instead is, so nothing hangs on a shop that is not there.
                return without != null ? without() : FamilyTalk.Close("reallife_family_way_none");
            }
            FamilyPlaces.Guide(venue);
            return FamilyTalk.Close(key, Args.Us.At(venue.Name));
        }
    }
}
