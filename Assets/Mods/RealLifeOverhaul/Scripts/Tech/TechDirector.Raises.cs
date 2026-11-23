using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// A raise for several professionals at once, and a rival's offer matched by itself. Both go the way a single raise and a
    /// single answer always did (<see cref="Raise"/>, <see cref="TalentRules.Raised"/>, <see cref="TalentRules.Answer"/>), so the standing,
    /// the rest before the next raise and the morale are as for one. Runs on the player's click and in the night's pass, never per frame.
    /// </summary>
    public sealed partial class TechDirector
    {
        /// <summary>What a person costs an hour, hundredths: the game's payroll at the city's prices, or the mod's own.</summary>
        private static int PaidNow(Person person) => person.Game != null ? TalentGame.PaidCents(person.Game) : person.File.GameWage;

        /// <summary>What they would cost an hour after a raise of <paramref name="bp"/>, hundredths, by the cap the raise itself keeps to.</summary>
        private static int PaidAfter(Person person, int bp)
        {
            Profile p = person.File;
            if (person.Game != null)
            {
                int formula = TalentGame.FormulaCents(person.Game);
                int wage = WageRules.Rise(TalentGame.WageCents(person.Game), bp, formula);
                return PaidWage.Cents(wage, Prices.Level, PaidWage.RaiseOf(person.Game.id));
            }
            return WageRules.Rise(p.Wage, bp, TalentGame.GameRate(p.Def.GameSkill, p.Main / 1000));
        }

        /// <summary>What a raise of <paramref name="bp"/> would do for these people (their file ids), read now; nothing is given.</summary>
        internal BulkRaiseRules.Plan PlanRaises(IList<string> ids, int bp)
        {
            var plan = new BulkRaiseRules.Plan();
            if (Save == null)
            {
                return plan;
            }
            int day = Save.Day;
            for (int i = 0; ids != null && i < ids.Count; i++)
            {
                Person person = PersonOf(ids[i]);
                if (person == null)
                {
                    continue;
                }
                Profile p = person.File;
                bool leaving = p.NoticeEnd > 0;
                bool rested = !TalentRules.CanRise(p, day);
                plan.Add(PaidNow(person), leaving || rested ? 0 : PaidAfter(person, bp), rested, leaving);
            }
            return plan;
        }

        /// <summary>
        /// The raise given to each of these people it may reach, as one raise is, and one notice for all of it. Returns what was done,
        /// worked from the same pass as the plan the player saw.
        /// </summary>
        internal BulkRaiseRules.Plan GiveRaises(IList<string> ids, int bp)
        {
            var done = new BulkRaiseRules.Plan();
            if (Save == null || bp <= 0)
            {
                return done;
            }
            int day = Save.Day;
            for (int i = 0; ids != null && i < ids.Count; i++)
            {
                Person person = PersonOf(ids[i]);
                if (person == null)
                {
                    continue;
                }
                Profile p = person.File;
                bool leaving = p.NoticeEnd > 0;
                bool rested = !TalentRules.CanRise(p, day);
                int before = PaidNow(person);
                int after = leaving || rested ? 0 : PaidAfter(person, bp);
                done.Add(before, after, rested, leaving);
                if (leaving || rested || after <= before)
                {
                    continue;
                }
                Raise(person, bp, day);
                TalentRules.Raised(p, day, bp);
            }
            if (done.Raised > 0)
            {
                int month = GameCalendar.MonthDays;
                Tell(NotificationType.Success, "reallife_roles_raised_all", Data("count", done.Raised.ToString(), "percent", TechText.Bp(bp),
                    "total", TechText.Cents(done.TotalMonthly(month)), "extra", TechText.Cents(done.ExtraMonthly(month))), TabTeam, null);
                Logger?.Info("Tech: " + done.Raised + " professional(s) given a raise of " + TechText.Bp(bp) + " together from the team page, " + done.Left + " shown left out ("
                             + done.Rested + " raised lately, " + done.Leaving + " on notice, " + done.Capped + " at the top of the pay scale): " + done.ExtraMonthly(month) + " cents a month more.");
                Changed();
            }
            return done;
        }

        /// <summary>
        /// A rival's offer to a professional that was just made, matched at once when "Match rivals' offers automatically" is on and it is
        /// within the limit of the option that agrees raises by itself: as the Match it button does it, with no notice of its own
        /// (RivalMatches tells the night's matches together). False where it is not matched and the player is told as before.
        /// </summary>
        private bool MatchOffer(Person person, int day)
        {
            Profile p = person.File;
            int limit = RivalMatchRules.Limit(Settings.EconomyCompanyAgreeUpTo, Economy.CompanyCatalogue.AgreeUpToMost);
            if (!Settings.EconomyCompanyMatchRivals || !TalentRules.OfferOpen(p, day) || !RivalMatchRules.WithinBp(p.PoachBp, limit))
            {
                return false;
            }
            string rival = p.PoachBy;
            int bp = p.PoachBp, before = PaidNow(person);
            Raise(person, bp, day);
            TalentRules.Answer(p, true, day, Year);
            int extra = Math.Max(0, PaidNow(person) - before);
            RivalMatches.Matched(day, RivalMatchRules.Monthly(extra, GameCalendar.MonthDays));
            Logger?.Info("Tech: " + rival + "'s offer of " + TechText.Bp(bp) + " over their pay to " + person.Name + " matched by itself (limit " + limit + "%): "
                         + extra + " an hour more (hundredths).");
            return true;
        }
    }
}
