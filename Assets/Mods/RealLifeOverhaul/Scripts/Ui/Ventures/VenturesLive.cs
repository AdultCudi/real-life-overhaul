using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Ventures;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the Ventures pages from the investors' file and the game and carries every button to the director that already owns it, as the old app did: the same methods, the
    /// same refusals as notices, the same question boxes. A page is written for a view when the director's revision moves or the player pressed something; nothing a frame.
    /// </summary>
    public sealed partial class VenturesLive : IVentSource, IVenActions
    {
        public static readonly VenturesLive Instance = new VenturesLive();
        public const string ScreenId = "ventures";

        private static int _wantTab = -1;
        private static string _wantPick;
        private int _local;
        private readonly VentWords _words = new VentWords((key, english) => FinanceText.T(key), x => FinanceText.Money(x));
        private VentView _view;

        private VenturesLive()
        {
        }

        /// <summary>A notice asks for a tab and a row: taken when the screen next opens or is next written.</summary>
        public static void Want(int tab, string pick)
        {
            _wantTab = tab;
            _wantPick = pick;
            Instance._local++;
        }

        public int Generation => (VenturesDirector.Instance != null ? VenturesDirector.Instance.Revision : 0) + _local;

        public void Opened(VentView view)
        {
            VenturesDirector d = VenturesDirector.Instance;
            if (d != null)
            {
                d.AppOpened();
            }
            TakeWant(view);
        }

        private static void TakeWant(VentView view)
        {
            if (_wantTab < 0)
            {
                return;
            }
            view.Tab = Math.Max(0, Math.Min(1, _wantTab));
            view.Pick = _wantPick;
            view.From = null;
            view.Page = VenPages.PageList;
            _wantTab = -1;
            _wantPick = null;
        }

        public bool Back(VentView view)
        {
            if (view.Tab == VenPages.TabInvestors && view.Page > VenPages.PageList)
            {
                // Each step of raising money goes back to the one before it: who to the business or its kind, then the kind to the business.
                view.Page = view.Page == VenPages.PageWho && view.S1 == null ? VenPages.PageType : VenPages.PageBusiness;
                if (view.Page == VenPages.PageBusiness)
                {
                    view.S2 = null;
                }
                _local++;
                return true;
            }
            return false;
        }

        public void Fill(VentModel m, VentView v)
        {
            _view = v;
            TakeWant(v);
            VenturesDirector d = VenturesDirector.Instance;
            VenFacts f = d != null && d.Running && SaveGameManager.Current != null ? Facts(d, v) : new VenFacts { Ready = false };
            VenPages.Fill(m, v, f, this, _words);
        }

        // ------------------------------------------------------------------------------------------------- the buttons

        public void Accept(int offerId)
        {
            VenturesDirector d = VenturesDirector.Instance;
            VenturesFile file = d != null ? d.File : null;
            Offer offer = file != null ? file.Offer(offerId) : null;
            if (offer == null)
            {
                _local++;
                return;
            }
            string name = d.NameOf(offer.InvestorId);
            string business = offer.ForNew ? VenturesGame.TypeName(offer.TypeName) : VenturesText.Business(offer.Place, offer.Business);
            GameConfirm.Show("reallife_ven_confirm_accept_title".Localize(new { name }),
                (offer.ForNew ? "reallife_ven_confirm_accept_new" : "reallife_ven_confirm_accept").Localize(new
                {
                    name, amount = FinanceText.Money(offer.Amount), share = VenturesText.Share(offer.ShareBp), business, growth = offer.TargetPct.ToString(),
                        days = offer.TargetDays.ToString(),
                    open = VenturesCatalogue.PledgeDays.ToString()
                }),
                Guard.Wrap(() =>
                {
                    d.Accept(offerId);
                    _view.Pick = null;
                    _local++;
                }), null, "reallife_ven_accept", "common_cancel", false, false);
        }

        public void Refuse(int offerId)
        {
            VenturesDirector d = VenturesDirector.Instance;
            if (d != null)
            {
                d.Refuse(offerId);
                _view.Pick = null;
                d.Poke();
            }
            _local++;
        }

        public void Counter(int offerId, bool moreMoney)
        {
            VenturesDirector d = VenturesDirector.Instance;
            d?.Counter(offerId, moreMoney);
            _local++;
        }

        public void Demand(int dealId, bool board)
        {
            VenturesDirector d = VenturesDirector.Instance;
            d?.AnswerDemand(dealId, board);
            _local++;
        }

        public void BuyOut(int dealId)
        {
            VenturesDirector d = VenturesDirector.Instance;
            VenturesFile file = d != null ? d.File : null;
            Deal deal = file != null ? file.Deal(dealId) : null;
            if (deal == null)
            {
                _local++;
                return;
            }
            string name = d.NameOf(deal.InvestorId);
            GameConfirm.Show("reallife_ven_confirm_buy_out_title".Localize(new { name }),
                (deal.Stage == DealStage.Called ? "reallife_ven_confirm_pay_off" : "reallife_ven_confirm_buy_out").Localize(new
                {
                    name, amount = FinanceText.Money(d.BuyOutPrice(deal)), business = VenturesText.Business(deal.Place, deal.Business), share = VenturesText.Share(deal.ShareBp)
                }),
                Guard.Wrap(() =>
                {
                    d.BuyOut(dealId);
                    _view.Pick = null;
                    _local++;
                }), null, deal.Stage == DealStage.Called ? "reallife_ven_pay_off" : "reallife_ven_buy_out", "common_cancel", false, false);
        }

        public void BuyBack(int dealId)
        {
            VenturesDirector d = VenturesDirector.Instance;
            VenturesFile file = d != null ? d.File : null;
            Deal deal = file != null ? file.Deal(dealId) : null;
            DealTerm term = deal != null ? d.Extra.TermOf(dealId) : null;
            if (term == null)
            {
                _local++;
                return;
            }
            string name = d.NameOf(deal.InvestorId);
            GameConfirm.Show("reallife_ven_confirm_buy_out_title".Localize(new { name }),
                "reallife_ven_confirm_buy_back".Localize(new { name, amount = FinanceText.Money(VenturesExtraRules.BuyBackPrice(deal, term)),
                    business = VenturesText.Business(deal.Place, deal.Business) }),
                Guard.Wrap(() =>
                {
                    d.BuyBack(dealId);
                    _view.Pick = null;
                    _local++;
                }), null, "reallife_ven_buy_back_btn", "common_cancel", false, false);
        }

        public void Report(int dealId)
        {
            VenturesDirector d = VenturesDirector.Instance;
            d?.SendReport(dealId);
            _local++;
        }

        public void GiveUp(int pledgeId)
        {
            VenturesDirector d = VenturesDirector.Instance;
            if (d != null)
            {
                d.GiveUp(pledgeId);
                _view.Pick = null;
                d.Poke();
            }
            _local++;
        }

        public void Pitch(int investorId, string place, string type, int budgetStep)
        {
            VenturesDirector d = VenturesDirector.Instance;
            if (d != null)
            {
                d.StartPitch(investorId, place, place != null ? null : type, VenturesCatalogue.BudgetAt(budgetStep));
            }
            _local++;
        }

        public void Locate(string place)
        {
            if (!EstateGame.Locate(place))
            {
                UI.Notification.Notifications.Show(UI.Notification.NotificationType.Warning, "reallife_ven_note_no_place", null, 6f);
            }
            _local++;
        }

        public void OpenBizMan()
        {
            VenturesGame.OpenBizMan();
        }
    }
}
