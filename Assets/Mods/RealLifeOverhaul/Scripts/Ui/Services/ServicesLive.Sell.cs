using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Ventures;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class ServicesLive
    {
        private sealed partial class Page
        {
            private readonly List<BuildingRegistration> _regs = new List<BuildingRegistration>(16);
            private readonly List<SaleRaise> _raises = new List<SaleRaise>(4);

            // ------------------------------------------------------------------------------------------------- the Sell tab

            public void Sell()
            {
                _m.Shape = ServicesShape.Sell;
                var p = new ServicesSell { PickerTitle = ServicesText.T("reallife_ui_svc_sell_pick_title") };
                _m.Sell = p;
                ServicesGame.Sellable(_regs);
                int count = _regs.Count;
                p.Businesses = new string[count];
                p.BusinessKeys = new string[count];
                for (int i = 0; i < count; i++)
                {
                    string place = ServicesGame.Place(_regs[i]);
                    string district = ServicesText.District(GameRegistrations.District(_regs[i]));
                    p.Businesses[i] = string.IsNullOrEmpty(district) ? ServicesGame.Name(_regs[i]) : ServicesGame.Name(_regs[i]) + ", " + district;
                    p.BusinessKeys[i] = place;
                }
                int at = ChosenBusiness(p);
                p.Chosen = at;
                p.PickerOpen = _v.PickerOpen && count > 1;
                bool rivals = _v.SellView == 1;
                BuildingRegistration reg = at >= 0 ? _regs[at] : null;
                SaleBreakdown b = reg != null ? _d.Value(reg) : default(SaleBreakdown);
                string chosenPlace = reg != null ? ServicesGame.Place(reg) : null;
                int offersHere = chosenPlace != null ? _s.OffersFor(chosenPlace) : 0;
                p.Views = new[]
                {
                    ServicesRuns.Chip(ServicesText.T("reallife_ui_svc_view_offers", "count", offersHere.ToString(Inv)), ServicesFace.ViewCommand, 0),
                    ServicesRuns.Chip(ServicesText.T("reallife_ui_svc_view_rivals", "count", _s.RivalSales.Count.ToString(Inv)), ServicesFace.ViewCommand, 1)
                };
                p.View = rivals ? 1 : 0;
                if (reg == null)
                {
                    NoBusiness(p);
                }
                else
                {
                    Worth(p, reg, b);
                }
                Table(p, reg, chosenPlace, b, rivals);
                string refusal = reg != null ? Refusal(reg) : null;
                if (rivals)
                {
                    RivalCard(p);
                    Price(p, reg, chosenPlace, b, refusal);
                }
                else
                {
                    OfferCard(p, reg, chosenPlace, b, refusal);
                    Price(p, reg, chosenPlace, b, refusal);
                }
                After(p);
                _regs.Clear();
            }

            // The business the page is about: the one the view names, the one an offer picked is for, or the first.
            private int ChosenBusiness(ServicesSell p)
            {
                string place = _v.Business;
                if (_v.Pick != null && _v.Pick.StartsWith("bid:", StringComparison.Ordinal) && int.TryParse(_v.Pick.Substring(4), System.Globalization.NumberStyles.Integer, Inv, out int id))
                {
                    SaleOffer o = _s.SaleOffer(id);
                    place = o != null ? o.Place : place;
                }
                for (int i = 0; place != null && i < p.BusinessKeys.Length; i++)
                {
                    if (string.Equals(p.BusinessKeys[i], place, StringComparison.Ordinal))
                    {
                        _v.Business = place;
                        return i;
                    }
                }
                _v.Business = p.BusinessKeys.Length > 0 ? p.BusinessKeys[0] : null;
                return p.BusinessKeys.Length > 0 ? 0 : -1;
            }

            // Why the sale cannot complete now, or null: the director's own refusals, shown before a press instead of after it.
            private string Refusal(BuildingRegistration reg)
            {
                return ServicesDirector.SaleRefusal(reg, out string key) ? null : ServicesText.T(key);
            }

            private void NoBusiness(ServicesSell p)
            {
                p.WorthLabel = ServicesText.T("reallife_ui_svc_worth_today");
                p.Worth = ServicesText.T("reallife_ui_svc_no_business");
                p.WorthSub = ServicesText.T("reallife_ui_svc_no_business_sub");
                p.Valued = new UiFactCardModel
                {
                    Title = ServicesText.T("reallife_ui_svc_sec_valued"), Heading = true,
                    Lines = new[] { UiFactLine.Say(ServicesText.T("reallife_svc_sale_says_value", "days", VenturesCatalogue.OfferValueDays.ToString(Inv))) }
                };
            }

            // The worth as the hero figure, how it is valued, and what would raise it before the sale.
            private void Worth(ServicesSell p, BuildingRegistration reg, SaleBreakdown b)
            {
                p.WorthLabel = ServicesText.T("reallife_ui_svc_worth_today");
                p.Worth = Money(b.Value);
                p.WorthSub = ServicesText.T("reallife_ui_svc_worth_sub", "days", VenturesCatalogue.OfferValueDays.ToString(Inv));
                var valued = new List<UiFactLine>(10)
                {
                    UiFactLine.Row(ServicesText.T("reallife_svc_value_base"), ServicesText.T("reallife_svc_value_base_value", "amount", Money(b.Base), "days",
                        VenturesCatalogue.OfferValueDays.ToString(Inv))),
                    UiFactLine.Row(ServicesText.T("reallife_ui_svc_goodwill"), Money(b.Goodwill)),
                    UiFactLine.Say(ServicesText.Fill("reallife_ui_svc_goodwill_by", "type", ServicesText.Percent(b.TypePct), "district", ServicesText.Percent(b.DistrictPct),
                        "economy", ServicesText.Percent(b.ClimatePct), "books", ServicesText.Percent(b.HistoryPct)), UiRole.Muted)
                };
                if (b.QualityPct != 100 || b.InteriorPts != 0 || b.RatingPts != 0 || b.SkillPts != 0)
                {
                    valued.Add(UiFactLine.Say(ServicesText.Fill("reallife_svc_value_quality", "share", ServicesText.Percent(b.QualityPct), "interior", Points(b.InteriorPts),
                        "rating", Points(b.RatingPts), "staff", Points(b.SkillPts)), UiRole.Muted));
                }
                valued.Add(UiFactLine.Row(ServicesText.T("reallife_svc_value_fixtures"), Money(b.Fixtures)));
                valued.Add(UiFactLine.Row(ServicesText.T("reallife_svc_value_worth"), Money(b.Value), UiRole.Text));
                if (b.Investors > 0)
                {
                    valued.Add(UiFactLine.Row(ServicesText.T("reallife_ui_svc_investors"), "-" + Money(b.Investors)));
                }
                if (b.Loans > 0)
                {
                    valued.Add(UiFactLine.Row(ServicesText.T("reallife_ui_svc_loans_to_repay"), "-" + Money(b.Loans)));
                }
                if (b.Investors + b.Loans > 0)
                {
                    valued.Add(UiFactLine.Row(ServicesText.T("reallife_ui_svc_left_at_value"), ServicesText.Signed(b.Net), b.Net < 0 ? UiRole.DangerText : UiRole.None));
                }
                if (b.Deposit > 0)
                {
                    valued.Add(UiFactLine.Row(ServicesText.T("reallife_ui_svc_deposit_back"), Money(b.Deposit)));
                }
                p.Valued = new UiFactCardModel { Title = ServicesText.T("reallife_ui_svc_sec_valued"), Heading = true, Lines = valued.ToArray() };

                SaleRules.Raises(ServicesGame.Inputs(reg), _raises);
                var raise = new List<UiFactLine>(4);
                for (int i = 0; i < _raises.Count; i++)
                {
                    SaleRaise r = _raises[i];
                    raise.Add(UiFactLine.Tick(ServicesText.RaiseLabel(r) + ". " + ServicesText.RaiseValue(r), r.Met));
                }
                if (_raises.Count == 0)
                {
                    raise.Add(UiFactLine.Say(ServicesText.T("reallife_svc_raise_none")));
                }
                _raises.Clear();
                p.Raise = new UiFactCardModel { Title = ServicesText.T("reallife_ui_svc_sec_raise"), Heading = true, Lines = raise.ToArray() };
            }

            private static string Points(int points) => points > 0 ? "+" + points.ToString(Inv) : points.ToString(Inv);

            // What the buyers of earlier sales still owe, and the promises that still bind.
            private void After(ServicesSell p)
            {
                if (_s.Dues.Count == 0 && _s.Clauses.Count == 0)
                {
                    return;
                }
                var lines = new List<UiFactLine>(4);
                for (int i = 0; i < _s.Dues.Count && lines.Count < 3; i++)
                {
                    SaleDue due = _s.Dues[i];
                    lines.Add(UiFactLine.Say(ServicesText.Fill("reallife_svc_row_due", "business", due.Name, "count", due.Left.ToString(Inv), "amount", Money(due.Part),
                        "day", due.NextDay.ToString(Inv))));
                }
                for (int i = 0; i < _s.Clauses.Count && lines.Count < 4; i++)
                {
                    SaleClause c = _s.Clauses[i];
                    lines.Add(UiFactLine.Say(ServicesText.T("reallife_svc_row_clause", "kind", ServicesGame.TypeName(c.Type), "district",
                        ServicesText.District(c.District), "day", c.Until.ToString(Inv))));
                }
                p.After = new UiFactCardModel { Title = ServicesText.T("reallife_ui_svc_sec_after"), Heading = true, Lines = lines.ToArray() };
            }

            // ------------------------------------------------------------------------------------------------- the offers, or the rivals' listings

            private void Table(ServicesSell p, BuildingRegistration reg, string place, SaleBreakdown b, bool rivals)
            {
                var rows = new List<UiDataRow>(8);
                var keys = new List<string>(8);
                UiDataColumn[] columns;
                string emptyTitle, emptyBody;
                if (rivals)
                {
                    columns = new[]
                    {
                        new UiDataColumn { Title = ServicesText.T("reallife_co_col_business"), Kind = UiCellKind.Tile, Width = 0f },
                        new UiDataColumn { Title = ServicesText.T("reallife_ui_svc_col_seller"), Kind = UiCellKind.Person, Width = 248f },
                        new UiDataColumn { Title = ServicesText.T("reallife_ui_svc_col_asking"), Kind = UiCellKind.Money, Width = 128f },
                        new UiDataColumn { Title = ServicesText.T("reallife_ui_svc_col_takings_week"), Kind = UiCellKind.Money, Width = 152f },
                        new UiDataColumn { Title = ServicesText.T("reallife_ui_svc_col_expires"), Kind = UiCellKind.Date, Width = 104f }
                    };
                    for (int i = 0; i < _s.RivalSales.Count; i++)
                    {
                        RivalSale v = _s.RivalSales[i];
                        rows.Add(new UiDataRow
                        {
                            Cells = new[] { v.Business, v.SellerName, Money(v.Price), Money(v.Weekly), ServicesText.T("reallife_econ_day", "day", v.Deadline.ToString(Inv)) },
                            Values = new[] { (int)UiIcon.Store, 0, 0, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                            Subs = new[] { v.Buyer.Length > 0 ? ServicesText.T("reallife_ui_svc_then_taken", "buyer",
                                v.BuyerName) : ServicesText.T("reallife_ui_svc_then_kept"), ServicesText.T("reallife_ui_svc_rival_owner"), null, null, null },
                            Hue = UiColour.HueOf(v.SellerName), Ring = UiRole.None
                        });
                        keys.Add("rsale:" + v.Id);
                    }
                    emptyTitle = ServicesText.T("reallife_ui_svc_no_rival_sales");
                    emptyBody = ServicesText.T("reallife_ui_svc_no_rival_sales_body");
                    p.OffersTitle = ServicesText.T("reallife_ui_svc_view_rivals_title");
                    p.OffersNote = ServicesText.T("reallife_ui_svc_rival_note");
                }
                else
                {
                    columns = new[]
                    {
                        new UiDataColumn { Title = ServicesText.T("reallife_ui_svc_col_buyer"), Kind = UiCellKind.Person, Width = 0f },
                        new UiDataColumn { Title = ServicesText.T("reallife_ui_svc_col_offer"), Kind = UiCellKind.Money, Width = 128f },
                        new UiDataColumn { Title = ServicesText.T("reallife_ui_svc_col_against"), Kind = UiCellKind.Meter, Width = 192f },
                        new UiDataColumn { Title = ServicesText.T("reallife_ui_svc_col_terms"), Kind = UiCellKind.Text, Width = 232f },
                        new UiDataColumn { Title = ServicesText.T("reallife_ui_svc_col_expires"), Kind = UiCellKind.Date, Width = 104f }
                    };
                    for (int i = 0; place != null && i < _s.SaleOffers.Count; i++)
                    {
                        SaleOffer o = _s.SaleOffers[i];
                        if (!string.Equals(o.Place, place, StringComparison.Ordinal))
                        {
                            continue;
                        }
                        string buyer = ServicesText.Buyer(o);
                        rows.Add(new UiDataRow
                        {
                            Cells = new[] { buyer, Money(o.Price), Against(o.Price, b.Value), Terms(o), ServicesText.T("reallife_econ_day", "day", o.Deadline.ToString(Inv)) },
                            Values = new[] { 0, 0, Standing(o.Price, b.Value), 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                            Subs = new[] { ServicesText.Who(o), null, null, null, null }, Hue = UiColour.HueOf(buyer), Ring = UiRole.None
                        });
                        keys.Add("bid:" + o.Id);
                    }
                    SaleListing l = place != null ? _s.Listing(place) : null;
                    emptyTitle = ServicesText.T(l != null ? "reallife_ui_svc_no_offers_listed" : place != null ? "reallife_ui_svc_no_offers_unlisted" : "reallife_ui_svc_no_offers_none");
                    emptyBody = ServicesText.T(l != null ? "reallife_ui_svc_no_offers_listed_body" : "reallife_ui_svc_no_offers_unlisted_body");
                    p.OffersTitle = ServicesText.T("reallife_ui_svc_view_offers_title");
                    p.OffersNote = rows.Count > 0 ? ServicesText.T("reallife_ui_svc_offers_note", "count", rows.Count.ToString(Inv)) : null;
                }
                int picked = keys.IndexOf(_v.Pick);
                if (_v.Pick != null && picked < 0)
                {
                    _v.Pick = null;   // an offer that lapsed, was refused or accepted: its card gives way to the first
                }
                if (_v.Pick == null && keys.Count > 0)
                {
                    picked = 0;   // the first row is the one the page is about until another is chosen: a sale page is never a table without a decision beside it
                }
                p.Offers = new UiDataTableModel
                {
                    Columns = columns, Rows = rows.ToArray(), RowCount = rows.Count, Selected = picked, EmptyTitle = emptyTitle, EmptyBody = emptyBody,
                    EmptyIcon = UiIcon.Tag
                };
                p.OfferKeys = keys.ToArray();
                _picked = picked >= 0 ? keys[picked] : null;
            }

            private string _picked;

            // How far an offer is from the worth, as a signed share with a decimal ("+4.5%").
            private static string Against(int price, int worth)
            {
                if (worth <= 0)
                {
                    return string.Empty;
                }
                float share = (price - worth) * 100f / worth;
                return (share >= 0f ? "+" : "-") + Math.Abs(share).ToString("0.0", Inv) + "%";
            }

            // The meter behind it: 60% of the worth is the empty bar, 120% the full one, so the worth itself stands about two thirds along and reads green.
            private static int Standing(int price, int worth)
            {
                if (worth <= 0)
                {
                    return 0;
                }
                return Math.Max(0, Math.Min(100, (int)Math.Round((price * 100f / worth - 60f) * 100f / 60f)));
            }

            private static string Terms(SaleOffer o)
            {
                string pay = o.Parts > 1 ? ServicesText.T("reallife_ui_svc_term_weeks", "count", (o.Parts - 1).ToString(Inv)) : ServicesText.T("reallife_ui_svc_term_cash");
                return o.KeepsStaff ? ServicesText.T("reallife_ui_svc_term_keeps", "pay", pay) : pay;
            }

            // ------------------------------------------------------------------------------------------------- what accepting leaves

            private void OfferCard(ServicesSell p, BuildingRegistration reg, string place, SaleBreakdown b, string refusal)
            {
                SaleOffer o = null;
                if (_picked != null && _picked.StartsWith("bid:", StringComparison.Ordinal) && int.TryParse(_picked.Substring(4), System.Globalization.NumberStyles.Integer, Inv, out int id))
                {
                    o = _s.SaleOffer(id);
                }
                if (o == null || reg == null)
                {
                    SaleListing l = place != null ? _s.Listing(place) : null;
                    p.Accept = new UiFactCardModel
                    {
                        Title = ServicesText.T("reallife_ui_svc_sec_accept_none"), Heading = true,
                        Lines = new[] { UiFactLine.Say(ServicesText.T(reg == null ? "reallife_svc_sale_says_after" : l != null ? "reallife_ui_svc_accept_wait" : "reallife_ui_svc_accept_list_first")) }
                    };
                    return;
                }
                int id2 = o.Id;
                SaleListing listing = _s.Listing(o.Place);
                SaleMoney m = SaleRules.Settle(o.Price, SaleRules.Parts(_s, o.Parts), b, ServicesGame.Cash);
                bool counter = SaleRules.CanCounter(o, listing != null ? listing.Asking : o.Price);
                string buyer = ServicesText.Buyer(o);
                int tax = TaxOn(m.Gain);
                var lines = new List<UiFactLine>(16)
                {
                    UiFactLine.Row(ServicesText.T("reallife_ui_svc_row_offer"), Money(o.Price)),
                    UiFactLine.Say(o.Countered ? ServicesText.T("reallife_svc_bid_until_final", "day", o.Deadline.ToString(Inv))
                        : counter ? ServicesText.T("reallife_svc_bid_until", "day", o.Deadline.ToString(Inv), "share", ServicesCatalogue.CounterStepPct.ToString(Inv))
                        : ServicesText.T("reallife_svc_bid_until_asking", "day", o.Deadline.ToString(Inv)), o.Countered ? UiRole.WarnText : UiRole.Muted)
                };
                if (m.PartsLeft > 0)
                {
                    lines.Add(UiFactLine.Row(ServicesText.T("reallife_ui_svc_row_paid_day"), Money(m.Now)));
                    lines.Add(UiFactLine.Row(ServicesText.T("reallife_ui_svc_row_then"), ServicesText.T("reallife_ui_svc_then_value", "count", m.PartsLeft.ToString(Inv), "amount", Money(m.Part))));
                }
                if (m.Investors > 0)
                {
                    lines.Add(UiFactLine.Row(ServicesText.T("reallife_ui_svc_investors_bought"), "-" + Money(m.Investors)));
                }
                if (m.Loans > 0)
                {
                    lines.Add(UiFactLine.Row(ServicesText.T("reallife_ui_svc_loans_repaid"), "-" + Money(m.Loans)));
                }
                if (tax > 0)
                {
                    lines.Add(UiFactLine.Row(ServicesText.T("reallife_ui_svc_tax_gain"), "-" + Money(tax)));
                }
                lines.Add(UiFactLine.Row(ServicesText.T("reallife_ui_svc_row_receive"), ServicesText.Signed(m.Left), m.Short > 0 ? UiRole.DangerText : UiRole.OkText));
                lines.Add(UiFactLine.Space());
                if (m.Investors + m.Loans > 0)
                {
                    lines.Add(UiFactLine.Tick(m.Short > 0 ? ServicesText.T("reallife_ui_svc_check_short", "amount", Money(m.Short)) : ServicesText.T("reallife_ui_svc_check_repaid"), m.Short <= 0));
                }
                lines.Add(UiFactLine.Tick(ServicesText.T(o.KeepsStaff ? "reallife_svc_bid_staff_keep" : "reallife_svc_bid_staff_not"), o.KeepsStaff));
                if (refusal != null)
                {
                    lines.Add(UiFactLine.Tick(refusal, false));
                }
                lines.Add(UiFactLine.Section(ServicesText.T("reallife_ui_svc_sec_give_up")));
                SaleInputs inputs = ServicesGame.Inputs(reg);
                lines.Add(UiFactLine.Row(ServicesText.T("reallife_ui_svc_row_profit_day"), Money((int)Math.Round(inputs.AvgProfit7))));
                lines.Add(UiFactLine.Say(ServicesText.T(reg.RentedByPlayer ? "reallife_ui_svc_lease_ends" : "reallife_ui_svc_building_stays", "amount", Money(b.Deposit))));
                lines.Add(UiFactLine.Say(o.ClauseDays > 0
                    ? ServicesText.T("reallife_svc_bid_clause", "days", o.ClauseDays.ToString(Inv), "share", ServicesCatalogue.ClausePenaltyPct.ToString(Inv))
                    : ServicesText.T("reallife_svc_bid_no_clause")));
                var actions = new List<UiActionModel>(3)
                {
                    _runs.Button(ServicesText.T("reallife_svc_bid_refuse"), UiButtonKind.Secondary, UiIcon.None, UiAction.Count, () => _d.Refuse(id2))
                };
                if (counter)
                {
                    actions.Add(_runs.Button(ServicesText.T("reallife_svc_bid_counter"), UiButtonKind.Secondary, UiIcon.None, UiAction.Count, () => _d.Counter(id2)));
                }
                actions.Add(_runs.Button(ServicesText.T("reallife_svc_bid_accept"), UiButtonKind.Primary, UiIcon.None, UiAction.Alt, () => _d.Accept(id2), refusal != null || m.Short > 0));
                p.Accept = new UiFactCardModel
                {
                    Title = ServicesText.T("reallife_ui_svc_sec_accept", "buyer", buyer), Heading = true, Lines = lines.ToArray(), Actions = actions.ToArray()
                };
            }

            // The tax a sale's gain adds to the quarter's return, as the game's rate on it: an estimate, paid with the return.
            private static int TaxOn(int gain) => gain > 0 ? ServicesCatalogue.Pct(gain, CompanyGame.TaxPercent) : 0;

            private void RivalCard(ServicesSell p)
            {
                RivalSale v = null;
                if (_picked != null && _picked.StartsWith("rsale:", StringComparison.Ordinal) && int.TryParse(_picked.Substring(6), System.Globalization.NumberStyles.Integer, Inv, out int id))
                {
                    v = _s.RivalSale(id);
                }
                if (v == null)
                {
                    p.Accept = new UiFactCardModel
                    {
                        Title = ServicesText.T("reallife_ui_svc_sec_rival_none"), Heading = true,
                        Lines = new[] { UiFactLine.Say(ServicesText.T("reallife_ui_svc_no_rival_sales_body")) }
                    };
                    return;
                }
                int id2 = v.Id;
                var lines = new List<UiFactLine>(8)
                {
                    UiFactLine.Row(ServicesText.T("reallife_svc_bid_price"), Money(v.Price)),
                    UiFactLine.Row(ServicesText.T("reallife_ui_svc_col_takings_week"), Money(v.Weekly)),
                    UiFactLine.Say(ServicesText.T(v.Buyer.Length > 0 ? "reallife_svc_rsale_then" : "reallife_svc_rsale_keeps", "day", v.Deadline.ToString(Inv), "buyer",
                        v.BuyerName, "seller", v.SellerName),
                        UiRole.WarnText),
                    UiFactLine.Say(ServicesText.T("reallife_svc_rsale_how"))
                };
                p.Accept = new UiFactCardModel
                {
                    Title = ServicesText.T("reallife_svc_rsale_title", "business", v.Business), Heading = true, Lines = lines.ToArray(),
                    Actions = new[] { _runs.Button(ServicesText.T("reallife_svc_rsale_buy"), UiButtonKind.Primary, UiIcon.None, UiAction.Alt, () => _d.BuyRival(id2)) }
                };
            }

            // ------------------------------------------------------------------------------------------------- the price to list it at

            private void Price(ServicesSell p, BuildingRegistration reg, string place, SaleBreakdown b, string refusal)
            {
                if (reg == null)
                {
                    p.Price = new UiFactCardModel
                    {
                        Title = ServicesText.T("reallife_ui_svc_sec_price"), Heading = true,
                        Lines = new[] { UiFactLine.Say(ServicesText.T("reallife_svc_sale_says_market")) }
                    };
                    p.PriceFoot = new UiFactCardModel { Lines = new[] { UiFactLine.Say(ServicesText.T("reallife_svc_sale_says_quick", "share", ServicesCatalogue.QuickPct.ToString(Inv))) } };
                    return;
                }
                SaleListing l = _s.Listing(place);
                int last = ServicesCatalogue.AskingPct.Length - 1;
                int step = ServicesCatalogue.Clamp(_v.Step >= 0 ? _v.Step : l != null ? l.Step : ServicesCatalogue.AskingDefault, 0, last);
                _v.Step = step;
                int value = b.Value;
                int asking = SaleRules.Asking(value, step);
                p.Price = new UiFactCardModel
                {
                    Title = ServicesText.T("reallife_ui_svc_sec_price"), Heading = true,
                    Lines = new[]
                    {
                        _runs.Step(ServicesText.T("reallife_ui_svc_row_asking"), Money(asking), ServicesText.T("reallife_ui_svc_price_hint", "share",
                            ServicesText.Percent(ServicesCatalogue.AskingPct[step])),
                            step > 0, step < last, () => Show(() => _v.Step = step - 1), () => Show(() => _v.Step = step + 1))
                    }
                };
                var points = new string[last + 1];
                var reads = new string[last + 1];
                var offers = new float[last + 1];
                var tints = new UiRole[last + 1];
                for (int i = 0; i <= last; i++)
                {
                    points[i] = ServicesText.Percent(ServicesCatalogue.AskingPct[i]);
                    reads[i] = Money(SaleRules.Asking(value, i));
                    offers[i] = ServicesCatalogue.AskingOffersMost[i];
                    tints[i] = i == step ? UiRole.Accent : UiRole.Muted;
                }
                p.PriceBars = new UiChartModel
                {
                    Kind = UiChartKind.Bars, Title = ServicesText.T("reallife_ui_svc_bars_title"), Points = points, Reads = reads, BarTints = tints, Whole = true, Focus = step,
                    Series = new[] { new UiSeries { Label = ServicesText.T("reallife_ui_svc_bars_series"), Values = offers } },
                    Format = v => ((int)Math.Round(v)).ToString(Inv)
                };
                var foot = new List<UiFactLine>(3)
                {
                    UiFactLine.Say(ServicesText.T("reallife_svc_asking_says", "days", ServicesCatalogue.AskingGapDays[step].ToString(Inv), "most",
                        ServicesCatalogue.AskingOffersMost[step].ToString(Inv)))
                };
                if (l != null)
                {
                    foot.Add(UiFactLine.Say(ServicesText.T("reallife_svc_listed_since", "day", l.ListedDay.ToString(Inv), "amount", Money(l.Asking), "count", _s.OffersFor(place).ToString(Inv))));
                }
                if (refusal != null)
                {
                    foot.Add(UiFactLine.Tick(refusal, false));
                }
                string name = place;
                var actions = new List<UiActionModel>(3)
                {
                    _runs.Button(ServicesText.T("reallife_ui_svc_quick_at", "amount", Money(SaleRules.Quick(value))), UiButtonKind.Secondary, UiIcon.None, UiAction.Count, () => _d.QuickSale(name),
                        refusal != null)
                };
                if (l != null)
                {
                    actions.Add(_runs.Button(ServicesText.T("reallife_svc_list_withdraw"), UiButtonKind.Secondary, UiIcon.None, UiAction.Count, () => _d.Withdraw(name)));
                }
                bool same = l != null && l.Step == step;
                actions.Add(_runs.Button(ServicesText.T(l == null ? "reallife_ui_svc_list_at" : "reallife_ui_svc_change_to", "amount", Money(asking)), UiButtonKind.Secondary, UiIcon.None,
                    UiAction.Option, () => _d.List(name, step), refusal != null || same));
                p.PriceFoot = new UiFactCardModel { Lines = foot.ToArray(), Actions = actions.ToArray() };
            }
        }
    }
}
