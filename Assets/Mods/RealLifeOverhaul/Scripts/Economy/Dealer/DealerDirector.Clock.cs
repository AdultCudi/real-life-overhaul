using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>The game's hour and day for the showrooms, called by the credit side's handlers; nothing per frame.</summary>
    public sealed partial class DealerDirector
    {
        private void OnHour()
        {
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                int now = Now;
                if (now <= _state.LastHour)
                {
                    return;
                }
                float started = Time.realtimeSinceStartup;
                if (now - _state.LastHour > CatchUp)
                {
                    _state.LastHour = now - 1;   // a gap longer than the catch-up: only this hour is worked
                }
                ReadYards();
                int offers = 0, closed = 0, drives = 0;
                for (int hour = _state.LastHour + 1; hour <= now; hour++)
                {
                    StepHour(hour, ref offers, ref closed, ref drives);
                }
                _state.LastHour = now;
                Changed();
                if (Verbose && (offers > 0 || closed > 0 || drives > 0))
                {
                    Logger?.Info("Dealership check (hour " + Save.Hour + " of day " + Save.Day + "): " + _state.Yards.Count + " dealership(s), " + offers + " offer(s) made, "
                                 + drives + " test drive(s), " + closed + " deal(s) closed by the staff; "
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.00", CultureInfo.InvariantCulture) + " ms: PASS");
                }
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        private void StepHour(int now, ref int offers, ref int closed, ref int drives)
        {
            int day = now / 24, level = Prices.Level;
            if (_state.MarketDay != day)
            {
                DealerRules.Morning(_state, day, _models, _colours, level);
                for (int y = 0; y < _state.Yards.Count; y++)
                {
                    DealerRules.CloseDay(_state, _state.Yards[y], day);
                }
            }
            Auction(now, level);
            Arrivals(now);
            int financeLeast = Prices.At(DealerCatalogue.FinanceLeast);
            for (int i = 0; i < _state.Yards.Count; i++)
            {
                DealerYard y = _state.Yards[i];
                BuildingRegistration reg = RegOf(y);
                if (reg == null)
                {
                    continue;
                }
                WorkshopHour(y, now);
                YardHour h = DealerGame.Hour(reg);
                Worths(y, level);
                DealerHour got = DealerRules.Hour(_state, y, h, now, day, _worths, financeLeast, _models, _colours, level);
                offers += got.Offers;
                Heard(y, now, got, h);
                if (h.Staff <= 0 || !h.Open)
                {
                    continue;
                }
                // The staff on shift: the customers who asked go out on their test drives, and what reaches the floor is closed.
                for (int d = y.Deals.Count - 1; d >= 0 && d < y.Deals.Count; d--)
                {
                    DealerOffer deal = y.Deals[d];
                    StockCar car = y.Car(deal.Car);
                    if (car == null || deal.Stage != OfferStage.Open)
                    {
                        continue;
                    }
                    if (deal.WantsDrive && !deal.Drove)
                    {
                        DealerRules.TestDrive(deal, car, now, _state.Seed);
                        DealerGame.Wear(car);
                        drives++;
                        continue;
                    }
                    if (DealerRules.StaffClose(deal, car, y.StaffFloor) && Close(y, reg, deal, deal.Offer, true))
                    {
                        closed++;
                    }
                }
            }
        }

        private void OnDay()
        {
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                float started = Time.realtimeSinceStartup;
                int day = Save.Day;
                ReadYards();
                int paid = 0, missed = 0, defaulted = 0, finished = 0, claims = 0, claimed = 0;
                int economy = EconomyFactor();
                for (int i = _state.Plans.Count - 1; i >= 0; i--)
                {
                    DealerPlan p = _state.Plans[i];
                    while (p.Due <= day && i < _state.Plans.Count && _state.Plans[i] == p)
                    {
                        PlanWeek w = DealerRules.Week(p, _state.Seed, economy, out int got);
                        DealerYard y = _state.Yard(p.Place);
                        BuildingRegistration reg = y != null ? RegOf(y) : null;
                        if (got > 0)
                        {
                            DealerGame.Give(got, DealerLines.FinanceKey, DealerGame.Named(reg), reg);
                            paid += got;
                            Book(y, got, 0, 0, 0);
                        }
                        if (w == PlanWeek.Missed)
                        {
                            missed++;
                        }
                        else if (w == PlanWeek.Defaulted)
                        {
                            defaulted++;
                            int back = DealerRules.Recovery(p.Worth);
                            DealerGame.Give(back, DealerLines.RecoveryKey, DealerGame.Named(reg), reg);
                            Book(y, back, 0, 0, 0);
                            Tell(NotificationType.Warning, "reallife_dealer_note_default", new Dictionary<string, string>(3)
                                { { "customer", p.Customer }, { "car", DealerGame.CarName(p.Type) }, { "amount", DealerText.Money(back) } }, TabFinance, null);
                            Repute(y, -DealerCatalogue.RepTakenBack);
                            Logger?.Info("Dealership: " + p.Customer + " stopped paying for a " + p.Type + " with " + p.Owed
                                         + " still owed; the car was taken back and sold at the trade auction for " + back + ".");
                            _state.Plans.RemoveAt(i);
                        }
                        else if (w == PlanWeek.PaidOff)
                        {
                            finished++;
                            _state.Plans.RemoveAt(i);
                        }
                    }
                }
                int floor = Prices.At(DealerCatalogue.ClaimFloor), ceiling = Prices.At(DealerCatalogue.ClaimCeiling);
                for (int i = _state.Covers.Count - 1; i >= 0; i--)
                {
                    DealerCover c = _state.Covers[i];
                    if (day > c.Until || c.Claims >= DealerCatalogue.ClaimsMost)
                    {
                        _state.Covers.RemoveAt(i);
                        continue;
                    }
                    int cost = DealerRules.Claim(c, day, _state.Seed, floor, ceiling);
                    if (cost <= 0)
                    {
                        continue;
                    }
                    DealerYard y = _state.Yard(c.Place);
                    BuildingRegistration reg = y != null ? RegOf(y) : null;
                    DealerGame.Take(cost, DealerLines.ClaimKey, DealerGame.Named(reg), reg, true);
                    Book(y, 0, 0, cost, 0);
                    Repute(y, DealerCatalogue.RepClaim);   // a promise kept
                    claims++;
                    claimed += cost;
                }
                EndEvents(day);
                Changed();
                if (Verbose)
                {
                    Logger?.Info("Dealership check (day " + day + "): " + _state.Plans.Count + " finance plan(s): " + paid + " paid in, " + missed + " week(s) missed, "
                                 + defaulted + " car(s) taken back, " + finished + " paid off; " + _state.Covers.Count + " warranty(ies): " + claims + " claim(s) of " + claimed
                                 + "; the economy at " + economy + " thousandths of the usual misses; "
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.00", CultureInfo.InvariantCulture) + " ms: PASS");
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        // How the city's economy moves the instalments missed (thousandths of the usual): more in a slowdown or a recession.
        private static int EconomyFactor()
        {
            return DealerCatalogue.CycleMisses[DealerCatalogue.Clamp((int)CityEconomy.State, 0, DealerCatalogue.CycleMisses.Length - 1)];
        }

        /// <summary>A showroom's day book: money in, money spent on stock, claims paid, margin made.</summary>
        internal void Book(DealerYard y, int takings, int bought, int claims, int margin)
        {
            if (y == null || Save == null)
            {
                return;
            }
            if (y.Today.Day != Save.Day)
            {
                DealerRules.CloseDay(_state, y, Save.Day);
            }
            y.Today.Takings += takings;
            y.Today.Bought += bought;
            y.Today.Claims += claims;
            y.Today.Margin += margin;
        }
    }
}
