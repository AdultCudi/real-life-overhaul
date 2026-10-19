using System;
using System.Collections.Generic;
using Dialogs;
using Entities;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Notification;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// What the partner comes to expect as the player gets richer (PartnerExpectRules): once a day, a bigger home, evenings
    /// out more often, a weekly allowance, a car of their own and a second home, each said once by a text, the home's
    /// harmony cooling while one falls short and warming when it is met. The allowance is chosen from the Family contact and
    /// paid each week. With the option off nothing is expected and no allowance is paid. Cost: nothing per frame; a few
    /// comparisons, one look at the home's size and one count of the player's homes and cars once a game day.
    /// </summary>
    internal static class PartnerExpect
    {
        private const string Residential = "ba:buildingtype_residential";

        /// <summary>The weekly allowance chosen, at today's prices.</summary>
        public static int Allowance => System.Math.Max(0, FamilyState.S.PAllowance);

        /// <summary>The allowance can be chosen from a family conversation.</summary>
        public static bool CanSetAllowance => Settings.FamilyExpect && FamilyState.Together && FamilyState.S.LivesIn;

        private static int Level => PartnerExpectRules.LevelOf(SaveGameManager.Current != null ? SaveGameManager.Current.Money : 0f, FamilyState.IncomeHeld, Prices.Level);

        /// <summary>Once a day: what goes without is held against the player, and the week's allowance.</summary>
        public static void Daily(int day)
        {
            Check(day, Level, false);
        }

        /// <summary>Once a game hour in the day: a wish said, or a wish met thanked for, when the partner's texts allow (PartnerVoice).</summary>
        public static void Hourly(int day)
        {
            Check(day, Level, true);
        }

        /// <summary>
        /// A look at a level: with <paramref name="speak"/> the wishes said and the wishes met (each by a text, one a day at the
        /// most, the rest left for a later hour), else the day's shortfalls held against the player and the week's allowance.
        /// </summary>
        public static void Check(int day, int level, bool speak)
        {
            FamilySave s = FamilyState.S;
            if (!Settings.FamilyExpect || !FamilyState.Together || !s.LivesIn)
            {
                return;
            }
            int wanted = PartnerExpectRules.HomeWanted(level);
            int sqm = FamilyPlaces.Sqm(FamilyPlaces.Home());
            if (PartnerExpectRules.HomeShortOf(sqm, wanted))
            {
                if (speak && s.PHomeWish < wanted && Speak("reallife_family_expect_home", new Args { Name = Partner.Name, N = wanted.ToString() }, null))
                {
                    s.PHomeWish = wanted;
                    s.PHomeWishDay = day;
                    FamilyDirector.Logger?.Info("Family: " + Partner.Name + " would like a home of " + wanted + " square metres (this one is " + sqm + ").");
                }
                else if (!speak && s.PHomeWish >= wanted && day - s.PHomeWishDay > PartnerExpectRules.HomeGraceDays)
                {
                    FamilyState.Harmony(PartnerExpectRules.HomeShort);
                }
            }
            else if (speak && s.PHomeWish > 0 && (sqm <= 0 || sqm >= s.PHomeWish) && Speak("reallife_family_expect_home_thanks", Args.Us, null))
            {
                s.PHomeWish = 0;
                FamilyState.Harmony(PartnerExpectRules.HomeFound);
                Partner.Tell(FamilyEvent.SharedTime, PartnerExpectRules.AffectionHome);
            }
            if (speak && level > s.PDateWishLevel && PartnerExpectRules.DateDays[level] > 0 && Speak("reallife_family_expect_dates", Args.Us, PartnerVoice.Date))
            {
                s.PDateWishLevel = level;
            }
            if (!speak && PartnerExpectRules.DatesShortOf(level, day, s.LastDateDay, s.MovedInDay))
            {
                FamilyState.Harmony(PartnerExpectRules.DatesShort);
            }
            Wish(day, speak, PartnerExpectRules.WantsCar(level, day, s.PCarBackDay), PartnerCar.Has, ref s.PCarWishDay, "reallife_family_expect_car",
                "reallife_family_expect_car_thanks", 0f, PartnerVoice.Car);
            Wish(day, speak, level >= PartnerExpectRules.SecondHomeFrom, PartnerExpectRules.SecondHomeMet(Homes()), ref s.PSecondHomeWishDay,
                "reallife_family_expect_second_home", "reallife_family_expect_second_home_thanks", PartnerExpectRules.AffectionSecondHome, null);
            Week(day, level, speak);
        }

        // A word through the partner's voice, if it is free now, and an ask of a kind when <paramref name="ask"/> names one: false and nothing said if not (PartnerVoice).
        private static bool Speak(string key, in Args a, string ask)
        {
            if (!PartnerVoice.Free)
            {
                return false;
            }
            if (ask != null)
            {
                PartnerVoice.Ask(ask, key, a);
            }
            else
            {
                PartnerVoice.Say(key, a);
            }
            return true;
        }

        // One wish of their own: asked for once, held against the player each day it goes without past the grace, thanked for when met.
        private static void Wish(int day, bool speak, bool wanted, bool met, ref int askedDay, string ask, string thanks, float affection, string askKind)
        {
            switch (PartnerExpectRules.Wish(wanted, met, askedDay, day))
            {
                case WishStep.Ask:
                    if (speak && Speak(ask, Args.Us, askKind))
                    {
                        askedDay = day;
                        FamilyDirector.Logger?.Info("Family: " + Partner.Name + " asked for something of their own (" + ask + ").");
                    }
                    break;
                case WishStep.Short:
                    if (!speak)
                    {
                        FamilyState.Harmony(PartnerExpectRules.WishShort);
                    }
                    break;
                case WishStep.Met:
                    if (speak && Speak(thanks, Args.Us, null))
                    {
                        askedDay = 0;
                        FamilyState.Harmony(PartnerExpectRules.WishMet);
                        if (affection > 0f)
                        {
                            Partner.Tell(FamilyEvent.SharedTime, affection);
                        }
                    }
                    break;
            }
        }

        // The allowance they expect said once at each new level (speaking), and once a week the allowance paid and what they make of it.
        private static void Week(int day, int level, bool speak)
        {
            FamilySave s = FamilyState.S;
            int expected = Expected(level);
            if (speak)
            {
                if (level > s.PAllowanceWishLevel && expected > 0
                    && Speak("reallife_family_expect_allowance", new Args { Name = Partner.Name, Amount = FamilyText.Money(expected) }, PartnerVoice.Allowance))
                {
                    s.PAllowanceWishLevel = level;
                }
                return;
            }
            if (s.PAllowanceDay > 0 && day - s.PAllowanceDay < 7)
            {
                return;
            }
            s.PAllowanceDay = day;
            int paid = Allowance;
            if (paid > 0)
            {
                FamilyTalk.Move(-paid, "reallife_transaction_family_allowance");
                FamilyState.MoneyOut(paid, 4);
            }
            FamilyState.Harmony(PartnerExpectRules.AllowanceWeek(paid, expected, FamilyState.Bond == Bond.Married));
            float closer = PartnerExpectRules.AllowanceAffection(paid, expected);
            if (closer > 0f)
            {
                Partner.Tell(FamilyEvent.SharedTime, closer);
            }
        }

        // What they expect a week at a level, at today's prices.
        private static int Expected(int level)
        {
            return Prices.At(PartnerExpectRules.AllowanceExpected(level, Spend, Partner.Income <= 0, PartnerWork.Work == PartnerWorkRules.PartTime,
                FamilyState.Bond == Bond.Married, Children(), FamilyState.Care == Care.Nursery || FamilyState.Care == Care.Nanny));
        }

        /// <summary>Their wish for evenings out, talked over at home: a dinner date planned there and then, or the rest.</summary>
        public static DialogEntry DateTalk()
        {
            PartnerVoice.Answered(PartnerVoice.Date);
            if (!HomeLife.CanPlanDate)
            {
                return FamilyPhone.FamilyMenu(0);
            }
            return FamilyTalk.Either(FamilyText.Say("reallife_family_do_date", Args.Us.Cost(FamilyBalance.DinnerCost)), "reallife_family_do_date_yes", HomeLife.PlanDate,
                "reallife_family_menu_back", () => FamilyPhone.FamilyMenu(0));
        }

        /// <summary>Their ask for an allowance, talked over: what it is now, to change in the game's text box, or back to the rest.</summary>
        public static DialogEntry AllowanceTalk()
        {
            PartnerVoice.Answered(PartnerVoice.Allowance);
            return FamilyTalk.Either(FamilyText.Say("reallife_family_do_allowance", new Args { Name = Partner.Name, Amount = FamilyText.Money(Allowance) }),
                "reallife_family_allowance_change", TypeAllowance, "reallife_family_menu_back", () => FamilyPhone.FamilyMenu(0));
        }

        /// <summary>The weekly allowance typed in the game's text box over the conversation, which stays as it is until it closes.</summary>
        public static DialogEntry TypeAllowance()
        {
            if (!TextBox.Ask("reallife_family_allowance_placeholder", "reallife_family_allowance_set_button", Guard.Wrap<string>(Typed), null,
                    Allowance.ToString(System.Globalization.CultureInfo.InvariantCulture), false, 9))
            {
                return FamilyTalk.Close("reallife_family_car_cannot");
            }
            return null;
        }

        private static void Typed(string text)
        {
            int amount = PartnerExpectRules.ParseAllowance(text, Prices.At(PartnerExpectRules.AllowanceMost));
            if (amount < 0)
            {
                FamilyText.Toast(NotificationType.Warning, "reallife_family_allowance_bad", Args.Us);
                return;
            }
            int was = Allowance, expected = Expected(Level);
            FamilyState.S.PAllowance = amount;
            FamilyState.Harmony(PartnerExpectRules.AllowanceReactionHarmony(was, amount, expected));
            int react = PartnerExpectRules.AllowanceReaction(was, amount, expected);
            FamilyDirector.Logger?.Info("Family: " + Partner.Name + "'s allowance is " + amount + " a week (was " + was + ", they expect " + expected + "): reaction " + react + ".");
            var a = new Args { Name = Partner.Name, Amount = FamilyText.Money(amount) };
            if (DialogController.current == null)
            {
                FamilyText.Toast(NotificationType.Success, "reallife_family_allowance_set", a);
                return;
            }
            // Their answer in the conversation that is still open, and the amount as it is now: to change again, or back to the rest.
            DialogController.current.ShowEntry(Guard.Entry(FamilyTalk.Either(FamilyText.Say(Reactions[react], a),
                "reallife_family_allowance_change", TypeAllowance, "reallife_family_menu_back", () => FamilyPhone.FamilyMenu(0))));
        }

        private static readonly string[] Reactions =
        {
            "reallife_family_allowance_react_same", "reallife_family_allowance_react_up", "reallife_family_allowance_react_up_short",
            "reallife_family_allowance_react_down", "reallife_family_allowance_react_down_short", "reallife_family_allowance_react_stopped"
        };

        // The player's homes: residential buildings of theirs.
        private static int Homes()
        {
            int homes = 0;
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration r = all[i];
                homes += r != null && GoingPublic.Mine(r) && r.GetBuildingType() == Residential ? 1 : 0;
            }
            return homes;
        }

        /// <summary>How the partner is with money, a per cent of the usual: Social's traits when it keeps them, else their seed.</summary>
        private static int Spend => Partner.Kept(out PersonInfo p) && p.Spend > 0 ? p.Spend : PartnerExpectRules.SpendOf(FamilyState.S.PSeed);

        // The children at home: not grown up.
        private static int Children()
        {
            int n = 0;
            for (int i = 0; i < FamilyState.S.Children.Count; i++)
            {
                n += FamilyState.S.Children[i] != null && !FamilyState.S.Children[i].Grown ? 1 : 0;
            }
            return n;
        }

        /// <summary>Somebody new, or nobody: nothing expected yet, no allowance.</summary>
        public static void Reset(FamilySave s)
        {
            s.PHomeWish = 0;
            s.PHomeWishDay = 0;
            s.PDateWishLevel = 0;
            s.PAllowance = 0;
            s.PAllowanceDay = 0;
            s.PAllowanceWishLevel = 0;
            s.PCarWishDay = 0;
            s.PSecondHomeWishDay = 0;
            PartnerCar.Reset(s);
        }
    }
}
