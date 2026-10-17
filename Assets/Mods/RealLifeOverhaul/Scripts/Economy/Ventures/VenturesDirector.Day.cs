using System;
using UI.Notification;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>The director's hour and day: promises that open, deals' shares and targets, offers that lapse and the weekly look.</summary>
    public sealed partial class VenturesDirector
    {
        private static readonly int HotOnHour = HotSpots.Slot("Economy: VenturesDirector.OnHour");
        // GlobalEvents.onNewHour, only while money is promised for a business not yet open: has one been opened.
        private void OnHour()
        {
            using var hotSpot = HotSpots.Time(HotOnHour);
            if (!_running || _broken || Save == null || _file.Pledges.Count == 0)
            {
                return;
            }
            try
            {
                CheckPledges(Save.Day);
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        private static readonly int HotOnDay = HotSpots.Slot("Economy: VenturesDirector.OnDay");
        // GlobalEvents.onNewDay: after the game's daily run, so yesterday's statement of every business is there.
        private void OnDay()
        {
            using var hotSpot = HotSpots.Time(HotOnDay);
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                int day = Save.Day;
                if (day == _file.SettledDay)
                {
                    return;
                }
                _wallet.BookDay = day - 1;   // the game books its daily run on the day that has ended
                InvestorsDay(day);
                _file.SettledDay = day;
                _wallet.BookDay = null;
                Badge();
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        // New offers come only while the option is on; it is read again in case it went off within the last second.
        private void InvestorsDay(int day)
        {
            for (int i = _file.Offers.Count - 1; i >= 0; i--)
            {
                Offer offer = _file.Offers[i];
                if (day > offer.LapseDay || (!offer.ForNew && VenturesGame.Business(offer.Place) == null))
                {
                    _file.Offers.RemoveAt(i);
                    Tell(NotificationType.Info, "reallife_ven_note_offer_lapsed", _openInvestors, "name", NameOf(offer.InvestorId));
                    Detail("the offer " + offer.Id + " from " + NameOf(offer.InvestorId) + " lapsed on day " + day);
                }
            }
            for (int i = _file.Pledges.Count - 1; i >= 0; i--)
            {
                Pledge pledge = _file.Pledges[i];
                if (day > pledge.OpenBy)
                {
                    _file.Pledges.RemoveAt(i);
                    Tell(NotificationType.Info, "reallife_ven_note_pledge_lapsed", _openInvestors, "name", NameOf(pledge.InvestorId), "type", VenturesGame.TypeName(pledge.TypeName));
                    Detail("the promise " + pledge.Id + " from " + NameOf(pledge.InvestorId) + " lapsed on day " + day + ": nothing is owed");
                }
            }
            CheckPledges(day);
            int level = Level;
            for (int i = _file.Deals.Count - 1; i >= 0; i--)
            {
                Deal deal = _file.Deals[i];
                BusinessFigures b = VenturesGame.Figures(VenturesGame.Business(deal.Place));
                _said.Clear();
                DealRules.RunDay(_file, deal, b, day, level, _wallet, _said, _fileClaim);
                for (int e = 0; e < _said.Count; e++)
                {
                    Said(_said[e], deal, b, day);
                }
            }
            if (Settings.VenturesInvestors && day - _file.OfferDay >= VenturesCatalogue.OfferEveryDays)
            {
                _file.OfferDay = day;
                Look(day, level);
            }
        }

        private void Said(DealEvent e, Deal deal, in BusinessFigures b, int day)
        {
            string name = NameOf(e.InvestorId), business = e.Business, amount = FinanceText.Money(e.Amount);
            if (Verbose)
            {
                Logger?.Info("Ventures check (day " + day + "): deal " + e.DealId + " with " + name + " on " + business + ": " + e.What + " " + e.Amount + " (yesterday's profit "
                             + b.ProfitYesterday.ToString("0") + ", share " + deal.ShareNow(day - 1) + " bp, recent profit " + b.AvgProfit7.ToString("0") + ", target " + deal.TargetProfit
                             + " by day " + deal.TargetDay + ", misses " + deal.Misses + ", mood " + deal.Mood + ", paid out " + deal.PaidOut + ").");
            }
            switch (e.What)
            {
                case DealNews.TargetMet:
                    Tell(NotificationType.Success, "reallife_ven_note_target_met", _openInvestors, "name", name, "business", business, "amount", amount);
                    break;
                case DealNews.Warned:
                    Tell(NotificationType.Warning, "reallife_ven_note_warned", _openInvestors, "name", name, "business", business, "amount", amount, "day", deal.TargetDay.ToString());
                    break;
                case DealNews.Demanded:
                    _file.Unseen++;
                    Tell(NotificationType.Warning, "reallife_ven_note_demand", _openInvestors, "name", name, "business", business, "day", deal.DemandDay.ToString());
                    break;
                case DealNews.DemandDefault:
                    Tell(NotificationType.Warning, deal.BoardUntil > 0 ? "reallife_ven_note_demand_seat" : "reallife_ven_note_demand_costs", _openInvestors, "name", name, "business", business,
                        "share", VenturesText.Share(deal.ShareNow(day)), "day", (deal.BoardUntil > 0 ? deal.BoardUntil : deal.CostDay).ToString());
                    break;
                case DealNews.PromiseKept:
                    Tell(NotificationType.Success, "reallife_ven_note_promise_kept", _openInvestors, "name", name, "business", business);
                    break;
                case DealNews.PromiseBroken:
                    Tell(NotificationType.Warning, "reallife_ven_note_promise_broken", _openInvestors, "name", name, "business", business, "amount", amount);
                    break;
                case DealNews.WentCold:
                    Tell(e.Amount > 0 ? NotificationType.Warning : NotificationType.Info, e.Amount > 0 ? "reallife_ven_note_cold_unpaid" : "reallife_ven_note_cold", _openInvestors,
                        "name", name, "business", business);
                    break;
                case DealNews.PaidBack:
                    Tell(NotificationType.Success, "reallife_ven_note_paid_back", _openInvestors, "name", name, "business", business);
                    break;
                case DealNews.Called:
                    _file.Unseen++;
                    Tell(NotificationType.Error, "reallife_ven_note_called", _openInvestors, "name", name, "business", business, "amount", amount, "per_day", FinanceText.Money(deal.CallPerDay));
                    break;
                case DealNews.BusinessGone:
                    Tell(NotificationType.Warning, "reallife_ven_note_gone", _openInvestors, "name", name, "business", business, "amount", amount, "per_day", FinanceText.Money(deal.CallPerDay));
                    break;
                case DealNews.Settled:
                    Tell(NotificationType.Info, "reallife_ven_note_settled", _openInvestors, "name", name, "business", business);
                    break;
                case DealNews.CallMissed:
                    Tell(NotificationType.Error, "reallife_ven_note_call_missed", _openInvestors, "name", name, "amount", amount, "count", deal.CallMisses.ToString(),
                        "most", VenturesCatalogue.CallMissesMost.ToString());
                    break;
                case DealNews.CallDone:
                    Tell(NotificationType.Success, "reallife_ven_note_call_done", _openInvestors, "name", name);
                    break;
                case DealNews.Claimed:
                    Tell(NotificationType.Error, "reallife_ven_note_claimed", _openInvestors, "name", name, "amount", amount);
                    break;
                case DealNews.Short:
                    Tell(NotificationType.Warning, "reallife_ven_note_short", _openInvestors, "name", name, "amount", amount);
                    break;
                case DealNews.BoardEnded:
                    Tell(NotificationType.Info, "reallife_ven_note_seat_ended", _openInvestors, "name", name, "business", business, "share", VenturesText.Share(deal.ShareBp));
                    break;
            }
        }

        // The weekly look: a second round where a target was met, else perhaps a tycoon for a business that does well.
        private void Look(int day, int level)
        {
            Books.Businesses(_businesses);
            int phase = VenturesBridges.Climate.Phase;
            for (int i = 0; i < _businesses.Count; i++)
            {
                BuildingRegistration r = _businesses[i];
                string place = Books.Key(r.Address);
                BusinessFigures b = VenturesGame.Figures(r);
                Deal due = null;
                for (int d = 0; d < _file.Deals.Count; d++)
                {
                    due = _file.Deals[d].Place == place && _file.Deals[d].FollowOn ? _file.Deals[d] : due;
                }
                if (due != null)
                {
                    due.FollowOn = false;
                    Investor again = _file.Investor(due.InvestorId);
                    Offer second = again != null && day >= again.ClosedUntil ? OfferRules.Make(_file, again, place, VenturesGame.Name(r), b, day, level, OfferSource.FollowOn) : null;
                    OfferMade(second, again, b, 100, 0, day);
                    continue;
                }
                if (!OfferRules.Qualifies(b) || _file.HasOffer(place))
                {
                    if (Verbose)
                    {
                        Logger?.Info("Ventures check (the weekly look, day " + day + "): " + VenturesGame.Name(r) + " made a profit on " + b.ProfitableDays + " of its last 14 days and "
                                     + b.AvgProfit7.ToString("0") + " a day lately (needs " + VenturesCatalogue.ProfitDaysNeeded + " and " + VenturesCatalogue.OfferProfitFloor + "): no offer.");
                    }
                    continue;
                }
                // A tycoon who sees people, has no offer out and no deal here; one who likes the sector first.
                Investor who = null;
                for (int v = 0; v < _file.Investors.Count; v++)
                {
                    Investor investor = _file.Investors[v];
                    if (investor.Kind == InvestorKind.Tycoon && day >= investor.ClosedUntil && !_file.HasOfferFrom(investor.Id) && !_file.HasDeal(place, investor.Id)
                        && (who == null || (investor.LikesSector(b.Sector) && !who.LikesSector(b.Sector))))
                    {
                        who = investor;
                    }
                }
                if (who == null)
                {
                    continue;
                }
                int chance = OfferRules.Chance(who.LikesSector(b.Sector), phase);
                int roll = VenturesRandom.Percent(_file.Seed, day, VenturesRandom.Of(place));
                OfferMade(roll < chance ? OfferRules.Make(_file, who, place, VenturesGame.Name(r), b, day, level, OfferSource.Inbound) : null, who, b, chance, roll, day);
            }
            _businesses.Clear();
        }

        private void OfferMade(Offer offer, Investor investor, in BusinessFigures b, int chance, int roll, int day)
        {
            if (Verbose && investor != null)
            {
                int valueDays = offer != null && offer.Source == OfferSource.FollowOn ? VenturesCatalogue.FollowOnValueDays : VenturesCatalogue.OfferValueDays;
                Logger?.Info("Ventures check (offer, day " + day + "): " + investor.Name + " (" + investor.Kind + ", " + investor.Temper + "), a business with " + b.ProfitableDays
                             + " profitable days of 14 and " + b.AvgProfit7.ToString("0") + " a day lately, chance " + chance + "%, roll " + roll + ": "
                             + (offer != null ? offer.Amount + " for " + offer.ShareBp + " bp (" + valueDays + " days of profit), growth " + offer.TargetPct + "% in " + offer.TargetDays
                                                + " days, answer by day " + offer.LapseDay
                                 : "no offer") + ".");
            }
            if (offer != null)
            {
                Tell(NotificationType.Success, offer.Source == OfferSource.FollowOn ? "reallife_ven_note_follow_on" : "reallife_ven_note_offer", _openInvestors, "name", investor.Name,
                    "business", offer.Business, "amount", FinanceText.Money(offer.Amount), "share", VenturesText.Share(offer.ShareBp), "day", offer.LapseDay.ToString());
            }
        }

        // Money promised for a business not yet open: paid, and the deal begun, once the player runs one more of that kind.
        private void CheckPledges(int day)
        {
            for (int i = _file.Pledges.Count - 1; i >= 0; i--)
            {
                Pledge pledge = _file.Pledges[i];
                BuildingRegistration opened = VenturesGame.Opened(_file, pledge);
                Investor investor = _file.Investor(pledge.InvestorId);
                if (opened == null || investor == null || day > pledge.OpenBy)
                {
                    continue;
                }
                Deal deal = OfferRules.Open(_file, pledge, Books.Key(opened.Address), VenturesGame.Name(opened), day);
                _wallet.Give(deal.Amount, VenturesMoney.Invested, investor.Name);
                Tell(NotificationType.Success, "reallife_ven_note_pledge_paid", _openInvestors, "name", investor.Name, "business", deal.Business, "amount", FinanceText.Money(deal.Amount),
                    "share", VenturesText.Share(deal.ShareBp));
                Detail("the promise " + pledge.Id + " from " + investor.Name + " is paid on day " + day + ": " + deal.Amount + " for " + deal.ShareBp + " bp of " + deal.Business);
            }
        }
    }
}
