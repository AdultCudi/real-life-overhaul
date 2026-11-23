using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class TechLive
    {
        private sealed partial class Page
        {
            // The deals of the terms of the player's sales, listed with the startups the player holds: a stake kept, an earn-out waiting, shares locked, a contract run. Open ones only.
            private void DealRows(List<UiDataRow> rows, List<string> keys)
            {
                TermFile terms = _d.Terms;
                for (int i = 0; terms != null && i < terms.Deals.Count; i++)
                {
                    TermDeal d = terms.Deals[i];
                    if (!d.Live || d.Kind == TermKind.Cash)
                    {
                        continue;
                    }
                    TermWorld w = _d.TermWorldOf(d);
                    UiDataRow row = Row(8);
                    row.Cells[0] = d.Firm;
                    row.Sub = TechText.Fill("reallife_terms_row_sub", "buyer", d.Buyer);
                    row.Values[0] = (int)TechGlyphs.Startup;
                    row.Cells[1] = SaleTermText.KindWords(d);
                    row.Cells[2] = TechText.Fill("reallife_terms_row_day", "day", (d.Kind == TermKind.Shares ? d.LockTo : d.Kind == TermKind.Earn ? d.Due : d.EndDay).ToString(Inv));
                    row.Cells[3] = Cents(_d.Level(SaleTermRules.Takings(d, w)));
                    row.Cells[4] = Cents(_d.Level(d.Price));
                    row.Cells[5] = d.Kind == TermKind.Slice ? TechText.Mille(d.Mille) : "-";
                    row.Values[5] = d.Mille / 10;
                    row.Cells[6] = Cents(_d.Level(DealWorth(d)));
                    row.Cells[7] = DealState(d, w);
                    row.Tones[7] = d.Kind == TermKind.Boss && d.Strikes > 0 ? UiRole.Warn : UiRole.Ok;
                    rows.Add(row);
                    keys.Add(TechApp.DealRow + d.Id);
                }
            }

            // What a deal still holds for the player, for its row: the stake's buyout today, the earn-out's prize, the shares' worth at the price they were paid at, a contract's
            // years yet to vest and prize.
            private static long DealWorth(TermDeal d)
            {
                switch (d.Kind)
                {
                    case TermKind.Slice: return SaleTermRules.SliceWorth(d);
                    case TermKind.Earn: return d.EarnState == 0 ? d.Further : 0L;
                    case TermKind.Shares: return d.Deferred;
                    default: return (d.EarnState == 0 ? d.Further : 0L) + d.Grant * Math.Max(0, d.Years - d.Vested) / Math.Max(1, d.Years);
                }
            }

            private string DealState(TermDeal d, in TermWorld w)
            {
                if (d.Kind == TermKind.Shares)
                {
                    return TechText.Fill("reallife_terms_row_locked", "days", SaleTermRules.LockLeft(d, _day).ToString(Inv));
                }
                if (d.Kind == TermKind.Slice)
                {
                    return TechText.T("reallife_terms_row_paying");
                }
                long aim = SaleTermRules.Aim(d);
                if (d.Kind == TermKind.Boss && !d.Open)
                {
                    return TechText.T("reallife_terms_row_handing");   // ended, the business not yet passed to the buyer
                }
                if (d.Kind == TermKind.Boss && d.EarnState != 0)
                {
                    return TechText.T("reallife_terms_row_contract");
                }
                return TechText.Fill("reallife_terms_row_target", "pct", TechText.Mille(aim > 0 ? (int)Math.Min(9990L, SaleTermRules.Progress(d, w) * 1000 / aim) : 0));
            }

            // A deal's page: its terms as written, how they stand, and for a contract the way to resign.
            private void DealPage(TermDeal d)
            {
                int id = d.Id;
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_ui_tech_back_startups");
                _m.Head = new UiHeadModel
                {
                    Name = d.Firm, Icon = TechGlyphs.Startup, Role = TechText.Fill("reallife_terms_row_sub", "buyer", d.Buyer), Status = SaleTermText.KindWords(d),
                    StatusTone = d.Open ? UiRole.Ok : UiRole.Warn
                };
                var card = new TechCard(_runs, TechText.T("reallife_ui_tech_terms_deal"));
                foreach (string line in SaleTermText.DealLines(_d, d, _day))
                {
                    card.Say(line);
                }
                if (d.Kind == TermKind.Boss && d.Open)
                {
                    card.Button(TechText.T("reallife_terms_button_resign"), UiButtonKind.Secondary, UiAction.Count, Ask(TechText.Fill("reallife_terms_confirm_resign_title", "firm", d.Firm),
                        TechText.Fill("reallife_terms_confirm_resign_body", "firm", d.Firm, "buyer", d.Buyer), () => _d.TermResign(id)));
                }
                _m.Left = new[] { card.Build() };
            }
        }
    }
}
