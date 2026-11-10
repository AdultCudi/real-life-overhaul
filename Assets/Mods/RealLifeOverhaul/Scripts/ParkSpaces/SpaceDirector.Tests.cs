using System;
using UI.Notification;

namespace RealLifeOverhaul.ParkSpaces
{
    /// <summary>
    /// Options, Tests: each situation the player would otherwise wait for, with real money at the real price. Each says what
    /// happened in a notice and writes one "Parking spaces check" line.
    /// </summary>
    public sealed partial class SpaceDirector
    {
        private bool TestReady()
        {
            if (!Running)
            {
                SpaceGame.Tell(NotificationType.Warning, "reallife_space_test_off", null);
                return false;
            }
            ReadParks();
            return true;
        }

        /// <summary>Options, Tests: a space rented in the first car park that has one free, at its real rent (the smallest sum a space can be had for).</summary>
        public void TestBuy()
        {
            if (!TestReady())
            {
                return;
            }
            for (int i = 0; i < _parks.Count; i++)
            {
                if (MayTake(_parks[i].Number, true, out int slot) != SpaceRefusal.None)
                {
                    continue;
                }
                long cash = SpaceGame.Cash;
                int held = _state.Count(0, SpaceHolding.PlayerIs);
                SpaceRefusal r = Rent(_parks[i].Number);
                bool right = r == SpaceRefusal.None && cash - SpaceGame.Cash == _parks[i].RentMonth && _state.Count(0, SpaceHolding.PlayerIs) == held + 1
                             && _state.At(_parks[i].Number, slot) != null;
                Logger?.Info("Parking spaces check (test, a space rented at " + _parks[i].Name + "): space " + slot + " for " + _parks[i].RentMonth + ", one more held and the cash down by the rent: "
                             + (right ? "PASS" : "FAIL (" + r + ")"));
                return;
            }
            SpaceGame.Tell(NotificationType.Warning, "reallife_space_test_none", null);
            Logger?.Info("Parking spaces check (test, a space rented): no car park of " + _parks.Count + " has a space the player may take now (none free, a limit, the cash, or a shared city).");
        }

        /// <summary>Options, Tests: the day's pass now, with an hour counted first: the charges for the cars standing in the player's spaces are paid back.</summary>
        public void TestDay()
        {
            if (!TestReady())
            {
                return;
            }
            _state.LastHour = -1;
            OnHour();
            long due = 0;
            for (int i = 0; i < _state.HourCount.Count; i++)
            {
                due += (long)_state.HourCount[i] * SpaceCatalogue.HourFee;
            }
            long before = _state.RefundedTotal;
            Pass(SpaceGame.Day);
            bool right = _state.RefundedTotal - before == due && _state.HourCount.Count == 0;
            SpaceGame.Tell(NotificationType.Info, "reallife_space_test_day", SpaceGame.Data("amount", SpaceGame.Money(due)));
            Logger?.Info("Parking spaces check (test, the day's pass now): " + due + " of the game's charges paid back for the hours counted, none left to pay twice: " + (right ? "PASS" : "FAIL"));
        }

        /// <summary>Options, Tests: the rival tycoons' look now, on the day each of them looks: one without a space may buy one, by the player's own rules.</summary>
        public void TestRival()
        {
            if (!TestReady())
            {
                return;
            }
            if (GoingPublic.InSession || SpaceBridges.RivalCount == null || SpaceBridges.RivalCount() <= 0)
            {
                SpaceGame.Tell(NotificationType.Warning, "reallife_space_test_no_rival", null);
                Logger?.Info("Parking spaces check (test, a tycoon's look): the rivals rest in a shared city, or none are there.");
                return;
            }
            int before = _state.Count(0, -2), moved = 0;
            for (int d = 0; d < SpaceCatalogue.RivalEvery * 4 && moved == 0; d++)
            {
                moved = Rivals(SpaceGame.Day + d);   // the days ahead on which they look, one by one, until one of them moves
            }
            int after = _state.Count(0, -2);
            SpaceGame.Tell(NotificationType.Info, moved > 0 ? "reallife_space_test_rival" : "reallife_space_test_rival_none", null);
            Logger?.Info("Parking spaces check (test, a tycoon's look): " + moved + " move(s), the tycoons held " + before + " and hold " + after + ": "
                         + (Math.Abs(after - before) <= moved ? "PASS" : "FAIL"));
            _revision++;
        }
    }
}
