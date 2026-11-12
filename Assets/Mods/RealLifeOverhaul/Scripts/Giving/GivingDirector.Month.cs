using System;
using System.Globalization;
using RealLifeOverhaul.Population;
using UI.Notification;

namespace RealLifeOverhaul.Giving
{
    /// <summary>
    /// The month's pass, one step a second so no frame carries a pass over the city's households (the steps and what makes each safe to run again are GivingMonth's; this
    /// answers them with the game): the counters turn over, the standing gifts are paid, the rivals give, the charities pay.
    /// </summary>
    public sealed partial class GivingDirector : IMonthWorld
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private void MonthStep()
        {
            using var hotSpot = HotSpots.Time(HotDay);
            GivingMonth.Step(_state, this);
            _revision++;
        }

        int IMonthWorld.Day => GivingGame.Day;

        int IMonthWorld.Level => GivingGame.Level;

        // The first day of a month: each standing gift is paid as a gift of the player's, or skipped, said once, never forced.
        void IMonthWorld.PayStanding(StandingGift g, int month)
        {
            int amount = GivingRules.At(g.Amount, GivingGame.Level);
            GiveResult r = GiveToCharity((Charity)g.Charity, amount);
            if (GivingResults.Moved(r))
            {
                g.PaidTotal += amount;
                g.Skipped = false;
                return;
            }
            if (!g.Skipped)
            {
                g.Skipped = true;
                GivingGame.Tell(NotificationType.Warning, "reallife_giving_standing_skipped", GivingGame.Data("charity", GivingText.Charity(g.Charity), "amount", GivingGame.Money(amount)));
            }
            if (Verbose)
            {
                Logger?.Info("Giving check (a standing gift to " + g.Charity + " of " + amount + " in month " + month + "): not paid (" + r + "), said once: PASS");
            }
        }

        long IMonthWorld.RivalWorth(int tycoon)
        {
            Func<int, long> worthOf = GivingBridges.RivalWorth;
            return worthOf != null ? worthOf(tycoon) : -1L;
        }

        long IMonthWorld.RivalPays(int tycoon, long gift)
        {
            Func<int, long, long> pay = GivingBridges.RivalPay;
            long paid = pay != null ? Math.Max(0L, pay(tycoon, gift)) : 0L;
            if (Verbose)
            {
                Logger?.Info("Giving check (a rival's gift): tycoon " + tycoon + " gave " + paid + " of " + gift + " (nothing from the worth they grow back to): PASS");
            }
            return paid;
        }

        // A charity pays out what it holds, under its cap for the month, to the people its cause is for.
        void IMonthWorld.PayOut(Charity c, int month)
        {
            CharityBook b = _state.Book(c);
            PopulationState s = GivingGame.People();
            if (s == null)
            {
                return;   // the city's people are not here to help: the pot waits
            }
            int level = GivingGame.Level;
            long cap = GivingRules.MonthCap(c, s.Households.Live, level);
            long pay = GivingRules.PayNow(b.Pot, cap, b.PaidMonth);
            long spent = 0;
            int helped = 0;
            if (pay > 0)
            {
                switch (c)
                {
                    case Charity.FoodBank:
                        helped = GivingPeople.PayFoodBank(s, pay, level, out spent);
                        break;
                    case Charity.Housing:
                        helped = GivingPeople.PayHousing(s, pay, level, out spent);
                        break;
                    default:
                        helped = GivingPeople.PayTraining(s, pay, level, out spent);
                        break;
                }
            }
            b.Pot = Math.Max(0L, b.Pot - spent);
            b.PaidMonth += spent;
            b.PaidYear += spent;
            b.PeopleYear += helped;
            b.LastMoney = spent;
            b.LastPeople = helped;
            b.LastMonth = month;
            if (Verbose)
            {
                Logger?.Info("Giving check (" + c + " in month " + month + "): held " + (b.Pot + spent) + ", cap " + cap + ", paid " + spent + " to " + helped
                             + (c == Charity.Training ? " people" : " households")
                             + (spent <= pay ? string.Empty : ", over the pay-out") + ": " + (spent <= cap ? "PASS" : "FAIL, over the cap"));
            }
        }

        // The people of the city help one another, in the population's own terms (savings to a household in hardship).
        void IMonthWorld.Neighbours(int month)
        {
            PopulationState s = GivingGame.People();
            if (s == null)
            {
                return;
            }
            int helped = GivingPeople.Neighbours(s, month, GivingGame.Level, out long moved);
            _state.NeighbourMonth = month;
            _state.NeighbourMoney = moved;
            _state.NeighbourYear += moved;
            _state.NeighbourHelped += helped;
            if (Verbose)
            {
                Logger?.Info("Giving check (neighbours in month " + month + "): " + helped + " household(s) helped by others' savings, " + moved.ToString(Inv) + " moved: PASS");
            }
        }
    }
}
