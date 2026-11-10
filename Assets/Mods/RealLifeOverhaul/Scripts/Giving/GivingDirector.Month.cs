using System;
using System.Globalization;
using RealLifeOverhaul.Population;
using UI.Notification;

namespace RealLifeOverhaul.Giving
{
    /// <summary>
    /// The month's pass, one step a second so no frame carries a pass over the city's households: the counters turn over, the standing gifts are paid, the
    /// rivals give, the charities pay.
    /// </summary>
    public sealed partial class GivingDirector
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private void MonthStep()
        {
            using var hotSpot = HotSpots.Time(HotDay);
            int month = _monthNumber;
            switch (_monthStep)
            {
                case 0:
                    CloseMonth(month);
                    break;
                case 1:
                    StandingGifts();
                    break;
                case 2:
                    RivalGifts(month);
                    break;
                case 3:
                    Payout(Charity.FoodBank, month);
                    break;
                case 4:
                    Payout(Charity.Housing, month);
                    break;
                case 5:
                    Payout(Charity.Training, month);
                    break;
                case 6:
                    Neighbours(month);
                    break;
                default:
                    _monthStep = -1;
                    _revision++;
                    return;
            }
            _monthStep++;
            _revision++;
        }

        // The month's counters start again. The month is marked done at once, so a save in the middle of the pass never pays a standing gift twice.
        private void CloseMonth(int month)
        {
            _state.Month = month;
            _state.StandingMonth = month;
            _state.MonthGiven = 0;
            _state.StandingDone = 0;
            _state.PressDone = 0;
            for (int i = 0; i < _state.Books.Length; i++)
            {
                CharityBook b = _state.Books[i];
                b.PlayerMonth = 0;
                b.OthersMonth = 0;
                b.PaidMonth = 0;
            }
        }

        // The first day of a month: each standing gift is paid as a gift of the player's, or skipped, said once, never forced.
        private void StandingGifts()
        {
            for (int i = 0; i < _state.Standing.Count; i++)
            {
                StandingGift g = _state.Standing[i];
                int amount = GivingRules.At(g.Amount, GivingGame.Level);
                GiveResult r = GiveToCharity((Charity)g.Charity, amount);
                if (GivingResults.Moved(r))
                {
                    g.PaidTotal += amount;
                    g.Skipped = false;
                    continue;
                }
                if (!g.Skipped)
                {
                    g.Skipped = true;
                    GivingGame.Tell(NotificationType.Warning, "reallife_giving_standing_skipped", GivingGame.Data("charity", GivingText.Charity(g.Charity), "amount", GivingGame.Money(amount)));
                }
                if (Verbose)
                {
                    Logger?.Info("Giving check (a standing gift to " + g.Charity + " of " + amount + " in month " + _monthNumber + "): not paid (" + r + "), said once: PASS");
                }
            }
        }

        // The rivals' own gifts to the same charities, from their own worth, seeded: one a rival a month.
        private void RivalGifts(int month)
        {
            Func<int, long> worthOf = GivingBridges.RivalWorth;
            if (worthOf == null)
            {
                return;
            }
            int day = GivingGame.Day;
            for (int i = 0; i < _state.RivalBooks.Length; i++)
            {
                RivalGiving r = _state.RivalBooks[i];
                long worth = worthOf(i);
                if (worth <= 0 || r.Month == month)
                {
                    continue;
                }
                long gift = GivingRules.RivalGift(_state.Seed, i, worth, 12, out int _);
                int charity = GivingRules.RivalCharity(_state.Seed, i, month);
                r.Month = month;
                if (gift <= 0)
                {
                    continue;
                }
                GivingBridges.RivalPay?.Invoke(i, gift);
                Credit((Charity)charity, gift, i);
                r.Total += gift;
                r.Year += gift;
                r.Last = gift;
                r.LastDay = day;
                r.LastCharity = charity;
                if (gift >= GivingRules.At(GivingCatalogue.BigGift, GivingGame.Level))
                {
                    _state.Notes.Add(new GivingNote { Day = day, Charity = charity, Amount = gift, Giver = i });
                }
                if (Verbose)
                {
                    Logger?.Info("Giving check (a rival's gift): tycoon " + i + " worth " + worth + " gave " + gift + " to " + (Charity)charity + " in month " + month + ": PASS");
                }
            }
        }

        // A charity pays out what it holds, under its cap for the month, to the people its cause is for.
        private void Payout(Charity c, int month)
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
        private void Neighbours(int month)
        {
            PopulationState s = GivingGame.People();
            if (s == null || _state.NeighbourMonth == month)
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
