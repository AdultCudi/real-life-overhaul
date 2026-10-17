using System;
using UI.Notification;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>The director's side of the app: offers taken up, refused and countered, demands answered, buy-outs and meetings.</summary>
    public sealed partial class VenturesDirector
    {
        /// <summary>The app has been opened: a business a promise waited for may have opened since the last hour, and the badge's count is seen.</summary>
        public bool AppOpened()
        {
            if (!_running || _broken || Save == null)
            {
                return false;
            }
            try
            {
                if (_file.Pledges.Count > 0)
                {
                    CheckPledges(Save.Day);
                }
                _file.Unseen = 0;
                Badge();
                return true;
            }
            catch (Exception e)
            {
                Fault("opening the app", e);
                return false;
            }
        }

        public void Open(int tab)
        {
            if (_app != null && _view != null && Running)
            {
                _view.Want(tab);
                _app.Open();
            }
        }

        /// <summary>An offer taken up: the money is paid in now, or promised for a business not yet open.</summary>
        public bool Accept(int offerId)
        {
            Offer offer = Running ? _file.Offer(offerId) : null;
            Investor investor = offer != null ? _file.Investor(offer.InvestorId) : null;
            if (investor == null || Save == null)
            {
                return false;
            }
            try
            {
                int day = Save.Day;
                if (offer.ForNew)
                {
                    Pledge pledge = OfferRules.Promise(_file, offer, day, VenturesGame.CountOf(offer.TypeName));
                    if (pledge == null)
                    {
                        Tell(NotificationType.Error, "reallife_ven_note_no_room", null);
                        return false;
                    }
                    Tell(NotificationType.Success, "reallife_ven_note_pledged", _openInvestors, "name", investor.Name, "amount", FinanceText.Money(pledge.Amount),
                        "type", VenturesGame.TypeName(pledge.TypeName), "day", pledge.OpenBy.ToString());
                    Detail("the offer " + offerId + " from " + investor.Name + " is accepted: " + pledge.Amount + " promised for a " + pledge.TypeName + " opened by day " + pledge.OpenBy
                           + " (" + pledge.Had + " of that kind run now)");
                    return true;
                }
                BuildingRegistration r = VenturesGame.Business(offer.Place);
                if (r == null)
                {
                    _file.Offers.Remove(offer);
                    Tell(NotificationType.Error, "reallife_ven_note_offer_lapsed", null, "name", investor.Name);
                    return false;
                }
                int amount = offer.Amount, share = offer.ShareBp;
                Deal deal = OfferRules.Accept(_file, offer, VenturesGame.Figures(r), day);
                deal.Business = VenturesGame.Name(r);
                _wallet.Give(amount, VenturesMoney.Invested, investor.Name);
                Tell(NotificationType.Success, "reallife_ven_note_accepted", _openInvestors, "name", investor.Name, "business", deal.Business, "amount", FinanceText.Money(amount),
                    "share", VenturesText.Share(share), "target", FinanceText.Money(deal.TargetProfit), "day", deal.TargetDay.ToString());
                Detail("the offer " + offerId + " from " + investor.Name + " is accepted: " + amount + " paid in for " + share + " bp of " + deal.Business + "; the deal now stands at "
                       + deal.Amount + " for " + deal.ShareBp + " bp, target " + deal.TargetProfit + " a day (from " + deal.BaseProfit + ") by day " + deal.TargetDay);
                return true;
            }
            catch (Exception e)
            {
                Fault("accepting an offer", e);
                return false;
            }
        }

        public void Refuse(int offerId)
        {
            Offer offer = Running ? _file.Offer(offerId) : null;
            if (offer != null)
            {
                _file.Offers.Remove(offer);
                Detail("the offer " + offerId + " from " + NameOf(offer.InvestorId) + " is refused");
            }
        }

        /// <summary>The one counter an offer allows. A notification says the answer, and the offer shows any new terms.</summary>
        public void Counter(int offerId, bool moreMoney)
        {
            Offer offer = Running ? _file.Offer(offerId) : null;
            Investor investor = offer != null ? _file.Investor(offer.InvestorId) : null;
            if (investor == null || offer.Countered)
            {
                return;
            }
            try
            {
                int wasAmount = offer.Amount, wasShare = offer.ShareBp;
                BusinessFigures b = VenturesGame.Figures(VenturesGame.Business(offer.Place));
                CounterAnswer answer = OfferRules.Counter(_file, offer, investor, b, moreMoney);
                Tell(answer == CounterAnswer.Refused ? NotificationType.Warning : NotificationType.Success,
                    answer == CounterAnswer.Accepted ? "reallife_ven_note_counter_yes" : answer == CounterAnswer.HalfWay ? "reallife_ven_note_counter_half" : "reallife_ven_note_counter_no",
                    _openInvestors, "name", investor.Name, "amount", FinanceText.Money(offer.Amount), "share", VenturesText.Share(offer.ShareBp));
                Detail("the counter to offer " + offerId + " (" + (moreMoney ? "more money" : "a smaller share") + ") to " + investor.Name + " (" + investor.Temper + ", "
                       + b.ProfitableDays + " profitable days): " + answer + "; " + wasAmount + " for " + wasShare + " bp is now " + offer.Amount + " for " + offer.ShareBp + " bp");
            }
            catch (Exception e)
            {
                Fault("a counter-offer", e);
            }
        }

        /// <summary>The answer to an investor's demand: a seat on the board, or a promise to cut costs.</summary>
        public bool AnswerDemand(int dealId, bool board)
        {
            Deal deal = Running ? _file.Deal(dealId) : null;
            if (deal == null || Save == null)
            {
                return false;
            }
            BusinessFigures b = VenturesGame.Figures(VenturesGame.Business(deal.Place));
            bool done = DealRules.Answer(deal, _file.Investor(deal.InvestorId), Save.Day, board, b);
            if (done)
            {
                Detail("the demand on deal " + dealId + " is answered with " + (board ? "a seat on the board until day " + deal.BoardUntil : "a promise of costs at or under " + deal.CostTarget
                       + " a day (now " + b.AvgCost14.ToString("0") + ") on day " + deal.CostDay) + "; the next target day is " + deal.TargetDay);
            }
            return done;
        }

        public int BuyOutPrice(Deal deal)
        {
            return DealRules.BuyOutPrice(deal, VenturesGame.Figures(VenturesGame.Business(deal.Place)));
        }

        public bool BuyOut(int dealId)
        {
            Deal deal = Running ? _file.Deal(dealId) : null;
            if (deal == null)
            {
                return false;
            }
            try
            {
                BusinessFigures b = VenturesGame.Figures(VenturesGame.Business(deal.Place));
                int price = DealRules.BuyOutPrice(deal, b);
                string name = NameOf(deal.InvestorId);
                if (!DealRules.BuyOut(_file, deal, b, _wallet))
                {
                    Notifications.ShowInsufficientMoney();
                    return false;
                }
                Tell(NotificationType.Success, "reallife_ven_note_bought_out", _openInvestors, "name", name, "business", deal.Business, "amount", FinanceText.Money(price));
                Detail("deal " + dealId + " with " + name + " is bought out for " + price + " (recent profit " + b.AvgProfit14.ToString("0") + " a day, share " + deal.ShareBp + " bp, "
                       + VenturesCatalogue.BuyOutDays + " days; put in " + deal.Amount + ", paid out " + deal.PaidOut + ")");
                return true;
            }
            catch (Exception e)
            {
                Fault("buying an investor out", e);
                return false;
            }
        }

        /// <summary>Money promised for a business not yet open is given up: nothing is owed.</summary>
        public void GiveUp(int pledgeId)
        {
            for (int i = 0; Running && i < _file.Pledges.Count; i++)
            {
                if (_file.Pledges[i].Id == pledgeId)
                {
                    Detail("the promise " + pledgeId + " from " + NameOf(_file.Pledges[i].InvestorId) + " is given up: nothing is owed");
                    _file.Pledges.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>A meeting about a business run ('place') or one not yet open ('typeName'); false, with the reason said, if not now.</summary>
        public bool StartPitch(int investorId, string place, string typeName, int budget, bool test = false)
        {
            Investor investor = Running && (Settings.VenturesInvestors || test) ? _file.Investor(investorId) : null;
            if (investor == null || Save == null)
            {
                return false;
            }
            try
            {
                int day = Save.Day;
                BuildingRegistration r = string.IsNullOrEmpty(place) ? null : VenturesGame.Business(place);
                if (r == null && investor.Kind == InvestorKind.Tycoon)
                {
                    Tell(NotificationType.Error, "reallife_ven_note_not_open", null, "name", investor.Name);
                    return false;
                }
                if ((r == null && string.IsNullOrEmpty(typeName)) || !investor.Open(day) || (_meeting != null && _meeting.Active))
                {
                    Tell(NotificationType.Error, VenturesText.Never(investor, day) ? "reallife_ven_note_never" : "reallife_ven_note_not_now", null, "name", investor.Name,
                        "day", investor.OpenDay.ToString());
                    return false;
                }
                if (VenturesGame.Busy() || (VenturesBridges.Held != null && VenturesBridges.Held()))
                {
                    Tell(NotificationType.Error, "reallife_ven_note_held", null, "name", investor.Name);
                    return false;
                }
                PitchFacts facts = VenturesGame.Facts(_file, r, typeName, budget);
                if (_meeting == null)
                {
                    _meeting = new PitchMeeting(this);
                }
                PitchState state = Pitch.Begin(_file, investor, facts, r != null ? place : null, r != null ? VenturesGame.Name(r) : VenturesGame.TypeName(typeName), r != null ? null : typeName, day);
                state.Test = test;
                if (!_meeting.Begin(state, investor))
                {
                    // The window could not be made: the approach does not count.
                    investor.PitchedDay = 0;
                    _file.Pitches--;
                    Tell(NotificationType.Error, "reallife_ven_note_no_window", null);
                    return false;
                }
                if (Verbose)
                {
                    Logger?.Info("Ventures check (pitch " + state.Number + ", day " + day + "): " + investor.Name + " (" + investor.Kind + ", " + investor.Temper + ", diligence " + investor.Diligence
                                 + ") about " + state.Business + " (" + (facts.Running ? "running" : "not yet open, budget " + facts.Budget) + ", " + facts.Sector + "): " + state.Questions.Count
                                 + " questions. True figures: takings " + facts.SalesYesterday + " and " + facts.SalesWeek + ", profit " + facts.ProfitYesterday + " and " + facts.ProfitWeek
                                 + " a day, staff " + facts.Staff + ", age " + facts.AgeDays + ", profitable days " + facts.ProfitableDays + ", cash " + facts.Cash + ", rating " + facts.Rating
                                 + ", owed " + facts.Owed + ", payments " + facts.Payments + ", missed " + facts.Missed + ", convictions " + facts.Convictions + ", businesses "
                                 + facts.Businesses + ", jobs " + facts.Jobs + ", backers " + facts.Backers + ".");
                }
                return true;
            }
            catch (Exception e)
            {
                Fault("starting a meeting", e);
                return false;
            }
        }

        /// <summary>A fault in the meeting ends Ventures for the session like any other.</summary>
        internal void MeetingFault(Exception e)
        {
            Fault("a meeting", e);
        }
    }
}
