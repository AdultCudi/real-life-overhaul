using System.Collections.Generic;
using RealLifeOverhaul.Economy.Ventures;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Sell tab: offers that wait for an answer, every business with its worth, prices still being paid and promises
    /// still kept; in the box, a business's value with an asking price to choose, or an offer with its terms.
    /// </summary>
    public sealed partial class ServicesApp
    {
        private readonly List<BuildingRegistration> _sellable = new List<BuildingRegistration>(16);
        private int _step = -1;   // the asking price on the selector; -1: the listing's own, or the value

        private void WriteSale(InfoBox box, ServicesState state, int day, ref string selected)
        {
            SaleOffer bid = null;
            bool isPicked;
            _spec = ServicesTables.Sale;
            for (int i = 0; i < state.SaleOffers.Count; i++)
            {
                SaleOffer o = state.SaleOffers[i];
                SaleListing l = state.Listing(o.Place);
                TableRow row = Pick("bid:" + o.Id, ServicesTables.Bid, ref selected, out isPicked);
                row.Set(0, l != null ? l.Name : "?");
                row.Set(1, ServicesText.Buyer(o));
                row.Set(3, ServicesText.Money(o.Price), o.Price);
                row.Set(5, ServicesText.T("reallife_econ_day", "day", o.Deadline.ToString()), o.Deadline);
                row.Set(6, ServicesText.T("reallife_svc_row_answer_by", "day", o.Deadline.ToString()), TableRow.Yellow);
                row.NoTotal = true;   // the total is what the businesses are worth
                bid = isPicked ? o : bid;
            }
            BuildingRegistration picked = null;
            ServicesGame.Sellable(_sellable);
            for (int i = 0; i < _sellable.Count; i++)
            {
                BuildingRegistration reg = _sellable[i];
                string place = ServicesGame.Place(reg);
                SaleListing l = state.Listing(place);
                SaleBreakdown b = _director.Value(reg);
                TableRow row = Pick("sell:" + place, l != null ? ServicesTables.Listed : ServicesTables.Unlisted, ref selected, out isPicked);
                row.Set(0, ServicesGame.Name(reg));
                row.Set(1, ServicesText.District(reg.Neighborhood));
                row.Set(2, ServicesText.Money(b.Value), b.Value);
                if (l != null)
                {
                    int offers = state.OffersFor(place);
                    row.Set(3, ServicesText.Money(l.Asking), l.Asking);
                    row.Set(4, offers.ToString(), offers);
                }
                row.Set(6, ServicesText.T(l != null ? "reallife_svc_row_on_market" : "reallife_svc_row_not_listed"), l != null ? TableRow.Green : TableRow.Plain);
                picked = isPicked ? reg : picked;
            }
            if (_sellable.Count == 0)
            {
                Say("none", ServicesText.T("reallife_co_row_no_business"));
            }
            _sellable.Clear();
            for (int i = 0; i < state.Dues.Count; i++)
            {
                SaleDue d = state.Dues[i];
                Say("due:" + i, ServicesText.Fill("reallife_svc_row_due", "business", d.Name, "count", d.Left.ToString(), "amount", ServicesText.Money(d.Part), "day", d.NextDay.ToString()));
            }
            for (int i = 0; i < state.Clauses.Count; i++)
            {
                SaleClause c = state.Clauses[i];
                Say("clause:" + i, ServicesText.T("reallife_svc_row_clause", "kind", ServicesGame.TypeName(c.Type), "district", ServicesText.District(c.District), "day", c.Until.ToString()));
            }
            if (bid != null)
            {
                WriteBid(box, state, bid);
            }
            else if (picked != null)
            {
                WriteBusiness(box, state, picked);
            }
            else
            {
                box.Title(ServicesText.T("reallife_svc_tab_sale"));
                box.Note(ServicesText.T("reallife_svc_sale_says_value", "days", VenturesCatalogue.OfferValueDays.ToString()));
                box.Note(ServicesText.T("reallife_svc_sale_says_market"));
                box.Note(ServicesText.T("reallife_svc_sale_says_quick", "share", ServicesCatalogue.QuickPct.ToString()));
                box.Note(ServicesText.T("reallife_svc_sale_says_after"));
                box.Note(ServicesText.T("reallife_svc_sale_pick"));
                Guide(box);
            }
        }

        private void WriteBusiness(InfoBox box, ServicesState state, BuildingRegistration reg)
        {
            string place = ServicesGame.Place(reg);
            SaleListing l = state.Listing(place);
            SaleBreakdown b = _director.Value(reg);
            int step = ServicesCatalogue.Clamp(_step >= 0 ? _step : l != null ? l.Step : ServicesCatalogue.AskingDefault, 0, ServicesCatalogue.AskingPct.Length - 1);
            int value = b.Value;
            box.Title(ServicesGame.Name(reg));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_svc_value_base", ServicesText.T("reallife_svc_value_base_value", "amount", ServicesText.Money(b.Base), "days", VenturesCatalogue.OfferValueDays.ToString()));
            box.Note(ServicesText.Fill("reallife_svc_value_goodwill", "type", ServicesText.Percent(b.TypePct), "district", ServicesText.Percent(b.DistrictPct),
                "economy", ServicesText.Percent(b.ClimatePct), "books", ServicesText.Percent(b.HistoryPct), "amount", ServicesText.Money(b.Goodwill)));
            box.Row("reallife_svc_value_fixtures", ServicesText.Money(b.Fixtures));
            box.Row("reallife_svc_value_worth", ServicesText.Money(b.Value));
            if (b.Investors + b.Loans > 0)
            {
                box.Line(ServicesText.T("reallife_svc_value_owed", "investors", ServicesText.Money(b.Investors), "loans", ServicesText.Money(b.Loans), "left", ServicesText.Signed(b.Net)));
            }
            box.Line(ServicesText.T("reallife_svc_value_deposit", "amount", ServicesText.Money(b.Deposit), "quick", ServicesText.Money(SaleRules.Quick(b.Value))));
            if (!box.HasSelector || !box.Selector(ServicesCatalogue.AskingPct.Length, step, i => ServicesText.Fill("reallife_svc_asking", "share", ServicesText.Percent(ServicesCatalogue.AskingPct[i]),
                    "amount", ServicesText.Money(SaleRules.Asking(value, i)), "days", ServicesCatalogue.AskingGapDays[i].ToString(), "most", ServicesCatalogue.AskingOffersMost[i].ToString()), i =>
                {
                    _step = i;
                    Refresh();
                }))
            {
                box.Line(ServicesText.Fill("reallife_svc_asking", "share", ServicesText.Percent(ServicesCatalogue.AskingPct[step]), "amount", ServicesText.Money(SaleRules.Asking(value, step)),
                    "days", ServicesCatalogue.AskingGapDays[step].ToString(), "most", ServicesCatalogue.AskingOffersMost[step].ToString()));
            }
            box.Note(ServicesText.T("reallife_svc_asking_says", "days", ServicesCatalogue.AskingGapDays[step].ToString(), "most", ServicesCatalogue.AskingOffersMost[step].ToString()));
            if (l != null)
            {
                box.Line(ServicesText.T("reallife_svc_listed_since", "day", l.ListedDay.ToString(), "amount", ServicesText.Money(l.Asking), "count", state.OffersFor(place).ToString()));
            }
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, l == null ? "reallife_svc_list_do" : "reallife_svc_list_change", () => _director.List(place, step));
            if (l != null)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_svc_list_withdraw", () => _director.Withdraw(place));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
            InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_svc_quick_do", () => _director.QuickSale(place));
            Guide(box);
        }

        private void WriteBid(InfoBox box, ServicesState state, SaleOffer o)
        {
            int id = o.Id;
            SaleListing l = state.Listing(o.Place);
            BuildingRegistration reg = ServicesGame.Find(o.Place);
            SaleBreakdown b = _director.Value(reg);
            SaleMoney m = SaleRules.Settle(o.Price, SaleRules.Parts(state, o.Parts), b, ServicesGame.Cash);
            bool counter = SaleRules.CanCounter(o, l != null ? l.Asking : o.Price);
            int share = b.Value > 0 ? Mathf.RoundToInt(o.Price * 100f / b.Value) : 0;
            box.Title(ServicesText.T("reallife_svc_bid_title", "business", l != null ? l.Name : "?"));
            box.Subtitle(ServicesText.T("reallife_svc_bid_from", "buyer", ServicesText.Buyer(o), "kind", ServicesText.Kind(o.Buyer)));
            if (o.Countered)
            {
                box.Chip(ServicesText.T("reallife_ibox_chip_final"), CellTone.Yellow);
            }
            string until = o.Countered ? "reallife_svc_bid_until_final" : counter ? "reallife_svc_bid_until" : "reallife_svc_bid_until_asking";
            box.Line(ServicesText.T(until, "day", o.Deadline.ToString(), "share", ServicesCatalogue.CounterStepPct.ToString()), CellTone.Yellow);
            box.Section("reallife_ibox_sec_offer");
            box.Row("reallife_svc_bid_price", ServicesText.T("reallife_svc_bid_price_value", "amount", ServicesText.Money(o.Price), "share", ServicesText.Percent(share)));
            box.Line(m.PartsLeft > 0
                ? ServicesText.T("reallife_svc_bid_parts", "now", ServicesText.Money(m.Now), "part", ServicesText.Money(m.Part), "count", m.PartsLeft.ToString())
                : ServicesText.T("reallife_svc_bid_once"));
            box.Section("reallife_ibox_sec_terms");
            box.Line(ServicesText.T(o.KeepsStaff ? "reallife_svc_bid_staff_keep" : "reallife_svc_bid_staff_not"), o.KeepsStaff ? CellTone.Green : CellTone.Yellow);
            box.Line(o.ClauseDays > 0
                ? ServicesText.T("reallife_svc_bid_clause", "days", o.ClauseDays.ToString(), "share", ServicesCatalogue.ClausePenaltyPct.ToString())
                : ServicesText.T("reallife_svc_bid_no_clause"));
            if (m.Investors + m.Loans > 0)
            {
                box.Line(ServicesText.T(m.Short > 0 ? "reallife_svc_bid_owed_short" : "reallife_svc_bid_owed", "investors", ServicesText.Money(m.Investors), "loans", ServicesText.Money(m.Loans),
                    "left", ServicesText.Signed(m.Left)), m.Short > 0 ? CellTone.Red : CellTone.Plain);
            }
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_svc_bid_accept", () => _director.Accept(id));
            if (counter)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_svc_bid_counter", () => _director.Counter(id));
            }
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_svc_bid_refuse", () =>
            {
                _pick = null;
                _director.Refuse(id);
            });
            BackButton(box);
        }
    }
}
