using System.Collections.Generic;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The Cover tab: claims, then each business's three kinds of cover with the quotes.</summary>
    public sealed partial class CompanyApp
    {
        private int _insurer = 1;   // the insurer whose quote is showing

        partial void WriteCover(InfoBox box, CompanyState state, int day, ref string selected)
        {
            int open = 0, premiums = 0;
            for (int i = state.Claims.Count - 1; i >= 0; i--)
            {
                Claim c = state.Claims[i];
                if (c.Stage != ClaimStage.Open && c.Stage != ClaimStage.Filed)
                {
                    continue;
                }
                open += c.Stage == ClaimStage.Open ? 1 : 0;
                // The business first, as in every other row of this list, so that the column sorts as one.
                int until = c.Stage == ClaimStage.Open ? c.Day + CompanyCatalogue.ClaimWindowDays : c.PayDay;
                Pick("claim:" + c.Id, c.Name, CompanyText.T("reallife_co_row_claim", "cover", CompanyText.Cover(c.Kind)), CompanyText.Insurer(c.Insurer),
                    c.Stage == ClaimStage.Open ? CompanyText.T("reallife_co_row_claim_by", "day", until.ToString())
                        : CompanyText.T("reallife_co_row_claim_paid", "day", until.ToString()),
                    c.Stage == ClaimStage.Open ? 2 : 1, ref selected);
                Last.Kind = c.Kind;
                Last.Kind2 = CompanyTables.IsClaim;
                Last.Set(4, CompanyText.Day(until), until);
            }
            IReadOnlyList<CompanyShop> shops = _director.Shops;
            for (int i = 0; i < shops.Count; i++)
            {
                for (int k = 0; k < shops[i].File.Cover.Length; k++)
                {
                    Policy p = shops[i].File.Cover[k];
                    premiums += p.Active ? p.Premium : 0;
                    Pick("pol:" + shops[i].File.Place + ":" + k, shops[i].Name, CompanyText.Cover(k), p.Active ? CompanyText.Insurer(p.Insurer) : CompanyText.T("reallife_co_none"),
                        p.Active ? CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(p.Premium)) : CompanyText.T("reallife_co_row_uninsured"), 0, ref selected);
                    TableRow row = Last;
                    row.Kind = k;
                    row.Kind2 = p.Active ? CompanyTables.Insured : CompanyTables.Uninsured;
                    if (p.Active)
                    {
                        row.Key[3] = p.Premium;
                        row.Set(4, CompanyText.Day(p.Until), p.Until);
                    }
                }
            }
            if (_rows.Count == 0)
            {
                Say("none", CompanyText.T("reallife_co_row_no_business"));
            }

            string id = Picked("claim:");
            int number;
            Claim claim = id != null && int.TryParse(id, out number) ? state.ClaimOf(number) : null;
            string place;
            int kind;
            CompanyShop shop = Picked("pol:", out place, out kind) && kind >= 0 && kind < CompanyCatalogue.CoverKinds ? _director.ShopAt(place) : null;
            if (claim != null)
            {
                WriteClaim(box, claim);
            }
            else if (shop != null)
            {
                WritePolicy(box, shop, kind);
            }
            else
            {
                box.Title(CompanyText.T("reallife_co_tab_cover"));
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_co_cover_premiums", CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(premiums)));
                box.Row("reallife_co_cover_open", open.ToString());
                box.Headline("reallife_co_cover_what");
                box.Note(CompanyText.T("reallife_co_cover_what_fire"));
                box.Note(CompanyText.T("reallife_co_cover_what_theft"));
                box.Note(CompanyText.T("reallife_co_cover_what_liability"));
                box.Note(CompanyText.T("reallife_co_cover_pick"));
                Guide(box);
            }
        }

        private void WriteClaim(InfoBox box, Claim c)
        {
            int id = c.Id;
            box.Title(CompanyText.T("reallife_co_claim_title", "cover", CompanyText.Cover(c.Kind), "business", c.Name));
            box.Chip(CompanyText.T(c.Stage == ClaimStage.Open ? "reallife_ibox_chip_open" : c.Stage == ClaimStage.Filed ? "reallife_ibox_chip_filed"
                    : c.Stage == ClaimStage.Paid ? "reallife_ibox_chip_paid" : "reallife_ibox_chip_lapsed"),
                c.Stage == ClaimStage.Open ? CellTone.Yellow : c.Stage == ClaimStage.Paid ? CellTone.Green : c.Stage == ClaimStage.Filed ? CellTone.Plain : CellTone.Red);
            switch (c.Stage)
            {
                case ClaimStage.Open:
                    box.Line(CompanyText.T("reallife_co_claim_open", "day", (c.Day + CompanyCatalogue.ClaimWindowDays).ToString(),
                        "days", CompanyCatalogue.InsurerPayDays[c.Insurer].ToString()), CellTone.Yellow);
                    break;
                case ClaimStage.Filed:
                    box.Line(CompanyText.T("reallife_co_claim_filed", "day", c.PayDay.ToString()));
                    break;
                case ClaimStage.Paid:
                    box.Line(CompanyText.T("reallife_co_claim_paid"), CellTone.Green);
                    break;
                default:
                    box.Line(CompanyText.T("reallife_co_claim_lapsed"), CellTone.Red);
                    break;
            }
            box.Note(CompanyText.T(c.Kind == (int)CoverKind.Fire ? "reallife_co_claim_what_fire" : c.Kind == (int)CoverKind.Theft ? "reallife_co_claim_what_theft"
                : "reallife_co_claim_what_liability", "day", c.Day.ToString(), "amount", CompanyText.Money(c.Loss)));
            box.Section("reallife_ibox_sec_money");
            box.Row("reallife_co_claim_insurer", CompanyText.Insurer(c.Insurer));
            box.Row("reallife_co_claim_most", CompanyText.Money(CompanyCatalogue.CoverMost[c.Kind]));
            box.Row("reallife_co_claim_excess", CompanyText.Money(c.Excess));
            box.Row("reallife_co_claim_pays", CompanyText.Money(c.Payout), CellTone.Green);
            if (c.Stage == ClaimStage.Open)
            {
                box.Note(CompanyText.T("reallife_co_claim_counts", "most", (CompanyCatalogue.InsurerClaimsMost[c.Insurer] - 1).ToString(),
                    "loading", CompanyText.Share(CompanyCatalogue.ClaimLoading)));
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_co_claim_do", () => _director.FileClaim(id));
            }
            BackButton(box);
        }

        private void WritePolicy(InfoBox box, CompanyShop shop, int kind)
        {
            Policy p = shop.File.Cover[kind];
            string place = shop.File.Place;
            box.Title(CompanyText.T("reallife_co_policy_title", "cover", CompanyText.Cover(kind), "business", shop.Name));
            box.Chip(CompanyText.T(p.Active ? "reallife_ibox_chip_covered" : "reallife_ibox_chip_not_covered"), p.Active ? CellTone.Green : CellTone.Yellow);
            int insurer = p.Active ? p.Insurer : CompanyCatalogue.Clamp(_insurer, 0, CompanyCatalogue.Insurers - 1);
            box.Section("reallife_ibox_sec_terms");
            if (!p.Active)
            {
                box.Note(CompanyText.T("reallife_co_policy_none"));
                if (!box.HasSelector || !box.Selector(CompanyCatalogue.Insurers, insurer, CompanyText.Insurer, i =>
                    {
                        _insurer = i;
                        Refresh();
                    }))
                {
                    box.Line(CompanyText.Insurer(insurer));
                }
            }
            else
            {
                box.Row("reallife_co_claim_insurer", CompanyText.Insurer(insurer));
            }
            box.Row("reallife_co_policy_premium", CompanyText.T("reallife_co_row_a_day", "amount",
                CompanyText.Money(p.Active ? p.Premium : _director.Quote(shop, kind, insurer))));
            box.Line(CompanyText.T("reallife_co_policy_excess", "share", CompanyText.Share(CompanyCatalogue.InsurerExcessShare[insurer]),
                "least", CompanyText.Money(CompanyCatalogue.InsurerExcessLeast[insurer])));
            box.Line(CompanyText.T("reallife_co_policy_most", "most", CompanyText.Money(CompanyCatalogue.CoverMost[kind]), "days", CompanyCatalogue.InsurerPayDays[insurer].ToString()));
            box.Note(CompanyText.T("reallife_co_policy_claims", "most", (CompanyCatalogue.InsurerClaimsMost[insurer] - 1).ToString(),
                "loading", CompanyText.Share(CompanyCatalogue.ClaimLoading)));
            bool writes = CoverRules.WillWrite(p, insurer);
            int carried = CoverRules.Carried(p);
            if (p.Active)
            {
                box.Line(CompanyText.T("reallife_co_policy_term", "day", p.Until.ToString(), "claims", p.Claims.ToString()));
            }
            else if (CoverRules.Declines(p, insurer))
            {
                box.Line(CompanyText.T("reallife_co_policy_declined", "insurer", CompanyText.Insurer(insurer)), CellTone.Red);
            }
            else if (!writes)
            {
                box.Line(CompanyText.T("reallife_co_policy_refused", "insurer", CompanyText.Insurer(insurer), "claims", carried.ToString()), CellTone.Red);
            }
            else if (carried > 0)
            {
                box.Line(CompanyText.T("reallife_co_policy_carried", "claims", carried.ToString()), CellTone.Yellow);
            }
            var buttons = box.ButtonRow();
            if (p.Active)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_co_policy_cancel", () => _director.CancelCover(place, kind));
            }
            else if (writes)
            {
                int chosen = insurer;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_co_policy_buy", () => _director.BuyCover(place, kind, chosen));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
            Guide(box);
        }
    }
}
