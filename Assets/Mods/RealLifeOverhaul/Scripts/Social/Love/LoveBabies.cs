using System;
using System.Collections.Generic;
using System.Globalization;
using Entities;
using Helpers;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A child that came of a night, in the game: the news (a text from the other parent, or the player's own test), the choice of how to
    /// be a parent, the birth into Family's children (the other parent marked as not the partner), the monthly support and what
    /// staying out costs. The rules are LoveRules; Family is reached through <see cref="LoveLinks"/>. COST: a short loop once a day.
    /// </summary>
    internal static class LoveBabies
    {
        private static string Pick(string[] pool, int a, int b) => LoveCatalogue.Pick(pool, LoveState.Seed, a, b);

        /// <summary>A night may have brought a child: rolled once, from the cast's seed, so the same night gives the same answer on every machine.</summary>
        public static void Conceived(SocialPerson p, in NightFacts nf, NightOutcome outcome, int day)
        {
            if (!LoveHooks.SurpriseOn || !LoveState.Loaded)
            {
                return;
            }
            LoveFile f = LoveState.File;
            bool playerFemale = LoveHooks.PlayerFemale();
            int carrier = LoveRules.Carrier(playerFemale);
            int pending = 0;
            for (int i = 0; i < f.Babies.Count; i++)
            {
                pending += f.Babies[i].Born ? 0 : 1;
            }
            int room = (LoveLinks.ChildRoom != null ? LoveLinks.ChildRoom() : 0) - pending;
            var cf = new ConceiveFacts
            {
                Surprise = Settings.LoveSurprise, Children = Settings.Family && Settings.FamilyChildren, SexesDiffer = p.Female != playerFemale,
                CarrierAge = carrier == 0 ? SocialTalk.AgeOf(p) : SocialLife.PlayerYears(), Room = room, Drinks = nf.Drinks,
                Careful = nf.Careful, Reliable = nf.Reliable, Flaky = nf.Flaky, Outcome = outcome
            };
            int chance = LoveRules.ConceiveChance(cf);
            int roll = LoveRules.Roll(LoveState.Seed, day, p.Index, LoveRules.SaltChild);
            SocialDirector.Logger?.Info("Love check (child): a night with " + p.Name + ": chance " + chance + " per mille, roll " + roll
                    + (roll < chance ? ",  a child is on the way" : ", none") + ": PASS");
            if (chance <= 0 || roll >= chance)
            {
                return;
            }
            int yearDays = Family.Children.DaysPerYear;   // the one reading of the child's year, so a baby of love keeps the family's pace
            f.Babies.Add(new LoveBaby
            {
                Person = p.Index, Day = day, NewsDay = LoveRules.NewsDay(day, yearDays), DueDay = LoveRules.DueDay(day, yearDays), Carrier = carrier,
                Girl = LoveRules.Roll(LoveState.Seed, day, p.Index, LoveRules.SaltBoy) < 500
            });
        }

        /// <summary>Once a day: the news comes, the birth, support is paid, the player's partner may hear.</summary>
        public static void Daily(int day)
        {
            LoveFile f = LoveState.File;
            for (int i = 0; i < f.Babies.Count; i++)
            {
                LoveBaby b = f.Babies[i];
                SocialPerson p = SocialState.Get(b.Person);
                if (p == null || !p.Met)
                {
                    continue;
                }
                if (!b.Told && day >= b.NewsDay)
                {
                    News(b, p);
                }
                if (b.Told && !b.Born && b.Choice == LoveChoice.None && day >= b.DueDay)
                {
                    Choose(b, p, LoveChoice.StayOut, true);
                }
                if (!b.Born && day >= b.DueDay)
                {
                    Birth(b, p, day);
                }
                if (b.Born && b.Choice == LoveChoice.Support)
                {
                    Support(b, p, day);
                }
                PartnerLearns(b, p, day);
            }
        }

        private static void News(LoveBaby b, SocialPerson p)
        {
            b.Told = true;
            Dictionary<string, string> data = SocialLife.Data(p);
            SocialPerson target = p;
            if (b.Carrier == 0)
            {
                SocialLife.SendText(p, Pick(LoveCatalogue.BabyNews, b.Day, p.Index), data, true, true);
                SocialLife.Toast(NotificationType.Warning, "reallife_love_baby_news_toast", data, 14f, Guard.Wrap(() => SocialPhone.Call(target)));
            }
            else
            {
                SocialLife.Toast(NotificationType.Warning, Pick(LoveCatalogue.BabyMe, b.Day, p.Index), data, 16f, Guard.Wrap(() => SocialPhone.Call(target)));
            }
            ModState.AddModifier(ModState.MoodDown);
            SocialDirector.Logger?.Info("Love: the news of a child with " + p.Name + " came (" + (b.Carrier == 0 ? "they carry it" : "the player carries it") + ").");
        }

        /// <summary>How the player chose to be a parent: what it does, what it costs, who hears.</summary>
        public static void Choose(LoveBaby b, SocialPerson p, LoveChoice choice, bool byDefault)
        {
            int day = SocialLife.Day;
            b.Choice = choice;
            b.ChoiceDay = day;
            Dictionary<string, string> data = SocialLife.Data(p);
            if (choice == LoveChoice.Support)
            {
                b.Support = LoveRules.SupportAmount(0f);
                b.PaidDay = b.DueDay > 0 ? Math.Max(day, b.DueDay) : day;
                data["amount"] = SocialLife.Money(Prices.At(b.Support));
            }
            if (choice != LoveChoice.StayOut)
            {
                p.Remember(MemoryCode.CoParent, day);
            }
            if (!byDefault)
            {
                string[] pool = choice == LoveChoice.Involved ? LoveCatalogue.BabyInvolved : choice == LoveChoice.Support ? LoveCatalogue.BabySupport : LoveCatalogue.BabyOut;
                SocialLife.SendText(p, Pick(pool, day, p.Index), data, true, true);
            }
            if (choice == LoveChoice.StayOut && b.Carrier == 0)
            {
                LoveRules.StayOutCosts(out float momA, out float momT, out int standing, out int followers);
                SocialPerson mom = SocialState.ByOrigin(SocialOrigin.Family);
                if (mom != null && mom.Met)
                {
                    SocialLife.Change(mom, 0f, momA, momT);
                    if (MomContact.On && Settings.FamilyRelatives)
                    {
                        SocialLife.SendText(mom, Pick(LoveCatalogue.BabyMomOut, day, p.Index), SocialLife.Data(mom, "other", SocialLife.First(p)), true, true);
                    }
                }
                if (LoveLinks.Integrity != null)
                {
                    LoveLinks.Integrity(standing);
                }
                LoveLinks.FlareGossip?.Invoke(followers, 0);
                SocialLife.Toast(NotificationType.Warning, "reallife_love_baby_out_costs", SocialLife.Data(p), 12f);
            }
            else if (choice != LoveChoice.StayOut)
            {
                SocialPerson mom = SocialState.ByOrigin(SocialOrigin.Family);
                if (mom != null && mom.Met && MomContact.On && Settings.FamilyRelatives)
                {
                    SocialLife.SendText(mom, Pick(LoveCatalogue.BabyMom, day, p.Index), SocialLife.Data(mom, "other", SocialLife.First(p)), true, true);
                }
            }
            SocialDirector.Logger?.Info("Love: the child with " + p.Name + ": the player chose " + choice + (byDefault ? " by not answering" : string.Empty) + ".");
        }

        private static void Birth(LoveBaby b, SocialPerson p, int day)
        {
            b.Born = true;
            Dictionary<string, string> data = SocialLife.Data(p);
            if (b.Choice == LoveChoice.Involved || b.Choice == LoveChoice.Support)
            {
                string name = LoveLinks.AddChild != null ? LoveLinks.AddChild(b.Girl, p.Name, p.Index, b.Choice == LoveChoice.Support, b.Carrier) : null;
                b.Name = name;
                data["child"] = name ?? SocialLife.T("reallife_love_child_unnamed");
                SocialLife.SendText(p, Pick(LoveCatalogue.BabyBorn, day, p.Index), data, true, true);
                ModState.AddModifier(ModState.MoodUpBig);
            }
            else
            {
                data["child"] = SocialLife.T("reallife_love_child_unnamed");
                SocialLife.SendText(p, Pick(LoveCatalogue.BabyBornOut, day, p.Index), data, true, true);
            }
            bool bound = SocialState.Partner != null && SocialState.Partner != p;
            if (bound || b.Choice == LoveChoice.StayOut)
            {
                LoveLife.Scandal(true, bound);
            }
            SocialDirector.Logger?.Info("Love: a child was born to " + p.Name + " (" + b.Choice + ", " + (b.Name ?? "no name in the family's list") + ").");
        }

        // A month's support, through the bank; short of the money, it waits and they say so.
        private static void Support(LoveBaby b, SocialPerson p, int day)
        {
            int months = LoveRules.MonthsDue(b.PaidDay, day);
            if (months <= 0)
            {
                return;
            }
            int amount = Prices.At(b.Support) * months;
            Dictionary<string, string> data = SocialLife.Data(p, "amount", SocialLife.Money(amount));
            if (SocialLife.Pay(amount, "reallife_transaction_love_support"))
            {
                b.PaidDay += LoveRules.SupportMonthDays * months;
                b.Behind = 0;
                SocialLife.Toast(NotificationType.Info, "reallife_love_support_paid", data, 8f);
                if (LoveRules.Roll(LoveState.Seed, day, p.Index, LoveRules.SaltLine) < 350)
                {
                    SocialLife.SendText(p, Pick(LoveCatalogue.SupportThanks, day, p.Index), data, false, true);
                }
            }
            else
            {
                b.Behind++;
                SocialLife.Toast(NotificationType.Warning, "reallife_love_support_short", data, 10f);
                SocialLife.SendText(p, Pick(LoveCatalogue.SupportLate, day, p.Index), data, true, true);
                SocialLife.Change(p, 0f, -2f, -3f);
            }
        }

        // The player's partner, who is not the other parent, hears: at once for a parent who is involved, by chance for the rest.
        private static void PartnerLearns(LoveBaby b, SocialPerson p, int day)
        {
            if (b.Partner || !b.Told || b.Choice == LoveChoice.None)
            {
                return;
            }
            SocialPerson partner = SocialState.Partner;
            LoveFile f = LoveState.File;
            if (partner == null || partner == p || f.ConfrontWho >= 0)
            {
                return;
            }
            if (LoveRules.Roll(LoveState.Seed, day, partner.Index * 7 + p.Index, LoveRules.SaltSecretOut) < LoveRules.PartnerLearns(b.Choice))
            {
                b.Partner = true;
                LoveLife.Catch(partner, p, CatchHow.Child);
            }
        }

        /// <summary>How a person's page says they are the other parent of a child, or null.</summary>
        public static LoveBaby ChildOf(SocialPerson p)
        {
            if (!LoveState.Loaded || p == null)
            {
                return null;
            }
            LoveBaby b = LoveState.File.BabyOf(p.Index);
            return b != null && b.Choice != LoveChoice.None ? b : null;
        }
    }
}
