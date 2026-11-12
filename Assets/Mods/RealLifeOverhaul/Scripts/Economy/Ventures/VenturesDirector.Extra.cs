using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>
    /// The director's side of what Ventures keeps besides the first deal: the terms and promises a new deal carries, the monthly look at them, what other investors hear when one
    /// is left unpaid, the valuation ring and the rivals' rounds. Runs on the day turn (once a month for the look) and when the player presses a button of the deal's page.
    /// </summary>
    public sealed partial class VenturesDirector
    {
        private VenturesExtra _extra = new VenturesExtra();
        private readonly List<DealEvent> _extraSaid = new List<DealEvent>(4);

        /// <summary>The extras of the save; null while Ventures is not running.</summary>
        public VenturesExtra Extra => _running ? _extra : null;

        /// <summary>Bumped by every change the page shows, so the kit writes it again.</summary>
        public int Revision { get; private set; }

        /// <summary>Tells the kit something it shows changed (an offer refused, a promise given up).</summary>
        public void Poke() => Revision++;

        internal static int MonthLength => Math.Max(1, GameCalendar.MonthDays);
        internal static int YearLength => Math.Max(1, GameCalendar.DaysPerYear);

        private void ExtraStart(int day)
        {
            _extra = VenturesExtraStore.Load(Save.modData);
            _extra.Prune(_file);
            VenturesExtraRules.Committed(_file, VenturesExtraRules.MonthOf(day, MonthLength));
        }

        private void ExtraSave()
        {
            if (_extra != null && Save != null)
            {
                VenturesExtraStore.Save(Save.modData, _extra);
            }
        }

        /// <summary>A deal has just been made from an offer (or a promised sum paid): the term and the promises its offer showed come with it.</summary>
        private void NewDeal(Deal deal, Investor investor, int offerKey, int day)
        {
            if (_extra.TermOf(deal.Id) != null || _extra.Promises.Exists(p => p.DealId == deal.Id))
            {
                return;   // a second round on a deal keeps the terms the first round made
            }
            DealTerm term = VenturesExtraRules.TermFor(_file, investor, offerKey, YearLength);
            if (term != null && _extra.Terms.Count < VenturesExtra.TermsMost)
            {
                term.DealId = deal.Id;
                term.Day = deal.StartDay + term.Day;
                _extra.Terms.Add(term);
            }
            Books.Businesses(_lookList);
            int firms = _lookList.Count;
            _lookList.Clear();
            var kinds = new PromiseKind[2];
            int count = VenturesExtraRules.PlannedPromises(_file, investor, offerKey, kinds);
            int made = VenturesExtraRules.MakePromises(_extra, deal, kinds, count, day, MonthLength, firms);
            Revision++;
            if (made > 0)
            {
                Tell(NotificationType.Info, "reallife_ven_note_promises", OpenOn("deal:" + deal.Id), "name", investor.Name, "count", made.ToString());
            }
        }

        // The day turn: the monthly look at promises, terms and the valuation, with what it brought said as the rest of a deal's day is.
        private void ExtraDay(int day)
        {
            int firms = 0;
            if (_extra.Promises.Count > 0)
            {
                Books.Businesses(_lookList);
                firms = _lookList.Count;
                _lookList.Clear();
            }
            int morale = VenturesBridges.Morale != null ? VenturesBridges.Morale() : -1;
            _extraSaid.Clear();
            if (!VenturesExtraRules.Month(_file, _extra, day, MonthLength, firms, morale, Level, _extraSaid))
            {
                return;
            }
            Revision++;
            for (int i = 0; i < _extraSaid.Count; i++)
            {
                Deal deal = _file.Deal(_extraSaid[i].DealId);
                if (deal != null)
                {
                    Said(_extraSaid[i], deal, default(BusinessFigures), day);
                }
            }
        }

        // What the other investors hear of a deal gone wrong, called from Said for the events that matter.
        private void Heard(DealEvent e, int day)
        {
            if (e.What == DealNews.WentCold && e.Amount > 0)
            {
                int heard = VenturesExtraRules.SpreadCold(_file, _extra, e.InvestorId, day);
                Detail("word got round that " + NameOf(e.InvestorId) + " was left unpaid: " + heard + " other investor(s) keep away until day " + (day + VenturesExtraRules.WaryDays));
            }
            else if (e.What == DealNews.PromiseBroken)
            {
                int heard = VenturesExtraRules.SpreadBroken(_file, _extra, e.InvestorId, day);
                Detail("word got round that a promise to " + NameOf(e.InvestorId) + " was broken: " + heard + " careful investor(s) keep away for " + VenturesExtraRules.BrokenWaryDays + " days");
            }
        }

        /// <summary>The quarter's report is sent to the investor of a deal: it counts for the report promise judged next.</summary>
        public bool SendReport(int dealId)
        {
            Deal deal = Running ? _file.Deal(dealId) : null;
            if (deal == null || Save == null || !VenturesExtraRules.SendReport(_extra, dealId, Save.Day))
            {
                return false;
            }
            Revision++;
            Detail("the report for deal " + dealId + " is sent on day " + Save.Day);
            return true;
        }

        /// <summary>The investor of a deal is bought out at the deal's own term (the multiple of the money put in), once its day has come. False, and nothing changed, if not now.</summary>
        public bool BuyBack(int dealId)
        {
            Deal deal = Running ? _file.Deal(dealId) : null;
            DealTerm term = deal != null ? _extra.TermOf(dealId) : null;
            if (deal == null || term == null || Save == null || Save.Day < term.Day || deal.Stage == DealStage.Called)
            {
                return false;
            }
            try
            {
                int price = VenturesExtraRules.BuyBackPrice(deal, term);
                string name = NameOf(deal.InvestorId);
                if (!VenturesExtraRules.BuyBack(_file, _extra, deal, term, _wallet))
                {
                    Notifications.ShowInsufficientMoney();
                    return false;
                }
                Revision++;
                Tell(NotificationType.Success, "reallife_ven_note_bought_out", _openInvestors, "name", name, "business", deal.Business, "amount", FinanceText.Money(price));
                Detail("deal " + dealId + " with " + name + " is bought back at its term for " + price);
                return true;
            }
            catch (Exception e)
            {
                Fault("buying back at the term", e);
                return false;
            }
        }
    }
}
