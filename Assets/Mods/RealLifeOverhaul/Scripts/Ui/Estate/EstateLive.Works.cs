using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class EstateLive
    {
        private sealed partial class Page
        {
            // A unit's or a building's own page, whichever tab the player came from. True when one was written.
            private bool CommonSub()
            {
                EstateHolding h;
                int unit;
                if (Unit(_v.Pick, out h, out unit))
                {
                    UnitPage(h, unit);
                    return true;
                }
                string place = Picked(_v.Pick, "own:");
                h = place != null ? _s.Holding(place) : null;
                if (h != null)
                {
                    BuildingPage(h);
                    return true;
                }
                return false;
            }

            // A table's page: a head, the table, and a card beside it.
            private void TableShape(UiIcon icon, string title, string sub, string[] figLabels, string[] figValues, UiDataColumn[] columns,
                List<UiDataRow> rows, List<string> keys, UiFactCardModel side,
                string emptyTitle, string emptyBody)
            {
                _m.Shape = EstateShape.Table;
                _m.HeadIcon = icon;
                _m.HeadTitle = string.IsNullOrEmpty(sub) ? title : sub;   // named once: the sidebar says which section, the page says what it holds
                _m.HeadSub = null;
                _m.FigLabels = figLabels ?? new string[0];
                _m.FigValues = figValues ?? new string[0];
                _m.Table = new UiDataTableModel { Columns = columns, Rows = rows.ToArray(), RowCount = rows.Count, EmptyTitle = emptyTitle, EmptyBody = emptyBody, EmptyIcon = icon };
                _m.RowKeys = keys.ToArray();
                _m.Side = side;
                if (side != null)
                {
                    side.Title = null;
                }
            }

            // ------------------------------------------------------------------------------------------------- tenants

            public void Tenants()
            {
                if (CommonSub())
                {
                    return;
                }
                string appPlace = Picked(_v.Pick, "app:");
                if (appPlace != null && ApplicantPage(appPlace))
                {
                    return;
                }
                _v.Pick = null;
                var rows = new List<UiDataRow>(24);
                var keys = new List<string>(24);
                int let = 0, empty = 0, owed = 0;
                for (int i = 0; i < _s.Holdings.Count; i++)
                {
                    EstateHolding h = _s.Holdings[i];
                    if (!h.Managed)
                    {
                        continue;
                    }
                    for (int n = 0; n < h.Units.Count; n++)
                    {
                        EstateUnit u = h.Units[n];
                        let += u.Let ? 1 : 0;
                        empty += u.Let ? 0 : 1;
                        owed += u.Arrears;
                        StateChip(h, u, out string chip, out UiRole tone);
                        Who who = u.Let ? WhoOf(h, u) : default(Who);
                        rows.Add(new UiDataRow
                        {
                            Cells = new[] { EstateText.UnitName(h, n), u.Let ? who.Name : EstateText.T("reallife_estate_row_nobody"), u.Let
                                ? Month(u.Rent) : "-", u.Let ? EstateText.Record(u.Record) : "-", chip,
                                u.Let && u.Happy >= 0 ? (u.Happy / 10).ToString(Inv) : string.Empty },
                            Values = new[] { (int)UseIcon(h.Use), u.Let ? 0 : UiDataTable.NoFace, 0, u.Let ? u.Record : -1, 0, u.Let && u.Happy >= 0 ? u.Happy / 10 : -1 },
                            Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone, UiRole.None },
                            Subs = new[] { EstateText.District(h.District), u.Let ? who.Line : null, null, null, null, null }, Hue = who.Hue, Ring = u.Let ? HappyRing(u.Happy) : UiRole.None
                        });
                        keys.Add(EstateDirector.UnitPick(h, n));
                        for (int a = 0; a < u.Applicants.Count; a++)
                        {
                            RentApplicant p = u.Applicants[a];
                            rows.Add(new UiDataRow
                            {
                                Cells = new[] { EstateText.UnitName(h, n), EstateText.Tenant(h.Use, p.Kind), Month(p.Offer),
                                    EstateText.Record(p.Record), EstateText.T("reallife_estate_filter_applicants"), string.Empty },
                                Values = new[] { (int)UseIcon(h.Use), 0, 0, p.Record, 0, -1 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Ok, UiRole.None },
                                Subs = new[] { EstateText.District(h.District),
                                    EstateText.T("reallife_estate_row_until", "day", p.Lapse.ToString(Inv)), null, null, null, null }, Hue = UiColour.HueOf(EstateText.Tenant(h.Use, p.Kind))
                            });
                            keys.Add("app:" + h.Place + ":" + p.Id);
                        }
                    }
                }
                var cols = new[]
                {
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_unit"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_tenant"), Kind = UiCellKind.Person, Width = 208f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_rent_month"), Kind = UiCellKind.Money, Width = 104f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_record"), Kind = UiCellKind.Meter, Width = 112f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_state"), Kind = UiCellKind.Chip, Width = 128f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_satisfaction"), Kind = UiCellKind.Meter, Width = 128f }
                };
                int happySum = 0, happyCount = 0;
                for (int i = 0; i < _s.Holdings.Count; i++)
                {
                    for (int n = 0; _s.Holdings[i].Managed && n < _s.Holdings[i].Units.Count; n++)
                    {
                        EstateUnit hu = _s.Holdings[i].Units[n];
                        if (hu.Let && hu.Happy >= 0)
                        {
                            happySum += hu.Happy / 10;
                            happyCount++;
                        }
                    }
                }
                TableShape(UiIcon.People, EstateText.T("reallife_estate_tab_tenants"),
                    EstateText.T("reallife_ui_estate_tenants_sub", "let", let.ToString(Inv), "empty", empty.ToString(Inv)),
                    new[] { EstateText.T("reallife_estate_tenants_let"), EstateText.T("reallife_estate_tenants_empty"), EstateText.T("reallife_estate_tenants_owed"), EstateText.T("reallife_ui_estate_fig_mean_happy") },
                    new[] { let.ToString(Inv), empty.ToString(Inv), Money(owed), happyCount > 0 ? EstateText.Share(happySum / happyCount * 10) : string.Empty }, cols, rows, keys, null,
                    EstateText.T(_s.Holdings.Count == 0 ? "reallife_estate_row_nothing_owned" : "reallife_estate_row_none_managed"), EstateText.T("reallife_ui_estate_empty_tenants_body"));
                _m.FigInks = new[] { UiRole.None, empty > 0 ? UiRole.WarnText : UiRole.None, owed > 0 ? UiRole.DangerText : UiRole.None, UiRole.None };
            }

            // An applicant for a unit: what they offer, what that is against the market, and their record. True when it was written.
            private bool ApplicantPage(string rest)
            {
                int colon = rest.LastIndexOf(':');
                int id;
                if (colon <= 0 || !int.TryParse(rest.Substring(colon + 1), out id))
                {
                    return false;
                }
                string place = rest.Substring(0, colon);
                EstateHolding h = _s.Holding(place);
                int unit;
                RentApplicant a = h != null ? h.ApplicantOf(id, out unit) : null;
                if (a == null)
                {
                    return false;
                }
                h.ApplicantOf(id, out unit);
                int market = EstateRules.UnitRent(h, _c);
                _m.Shape = EstateShape.Detail;
                _m.Card = true;
                _m.Back = EstateText.T("reallife_co_back");
                _m.DetailIcon = UiIcon.People;
                _m.DetailTitle = EstateText.Tenant(h.Use, a.Kind);
                _m.DetailFace = _m.DetailTitle;   // the applicant has a face, as every person has
                _m.DetailSub = EstateText.UnitName(h, unit);
                _m.DetailChip = EstateText.Record(a.Record);
                _m.DetailChipTone = a.Record >= 60 ? UiRole.Ok : UiRole.Warn;
                _m.Strip = new[]
                {
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_their_offer"), Value = Money(a.Offer) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_ch_market_rent"), Value = Money(market) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_of_market"), Value = EstateText.Share(market > 0 ? (int)((long)a.Offer * 1000 / market) : 0) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_deposit"), Value = Money(a.Offer * EstateCatalogue.DepositDays) }
                };
                var terms = new List<UiFactLine>
                {
                    UiFactLine.Row(EstateText.T("reallife_ui_estate_term_lease"), EstateText.T("reallife_ui_estate_n_days", "days", a.Term.ToString(Inv))),
                    UiFactLine.Row(EstateText.T("reallife_ui_estate_term_until"), EstateText.T("reallife_ui_estate_day", "day", a.Lapse.ToString(Inv)))
                };
                var actions = new List<UiActionModel>(2);
                int at = unit;
                actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_refuse"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() =>
                {
                    _d.Refuse(place, id);
                    _v.Pick = null;
                })));
                actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_accept"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, () => Show(() =>
                {
                    int taken = _d.Accept(place, id);
                    _v.Pick = taken >= 0 ? EstateDirector.UnitPick(h, taken) : null;
                }), h.Units[unit].Let || h.Busy));
                _m.Left = new[] { new UiFactCardModel { Title = EstateText.T("reallife_ibox_sec_terms"), Heading = true, Lines = terms.ToArray(), Actions = actions.ToArray() } };
                _m.ShareTitle = EstateText.T("reallife_ui_estate_sh_record");
                _m.ShareLabels = new[] { EstateText.T("reallife_ui_estate_col_record"), EstateText.T("reallife_ui_estate_sh_pays"), EstateText.T("reallife_ui_estate_sh_catches") };
                _m.ShareValues = new[] { a.Record, 100 - EstateRules.MissChance(a.Record, _c.Cycle) / 10, EstateRules.CatchUpChance(a.Record) / 10 };
                _m.RelatedTitle = EstateText.T("reallife_ui_estate_the_unit_they_want");
                _m.Related = new[]
                {
                    new EstateRow
                    {
                        Icon = UseIcon(h.Use), Title = EstateText.UnitName(h, unit), Sub = EstateText.District(h.District), Chip = EstateText.T("reallife_estate_state_empty", "days", h.Units[unit].VoidDays.ToString(Inv)),
                        ChipTone = UiRole.Warn, Amount = Money(market), Run = _runs.Do(() => Open(EstateDirector.UnitPick(h, at), null))
                    }
                };
                return at >= 0;
            }

            // ------------------------------------------------------------------------------------------------- works

            public void Works()
            {
                if (CommonSub())
                {
                    return;
                }
                string callPick = Picked(_v.Pick, "cal:");
                if (callPick != null && CallPage(callPick))
                {
                    return;
                }
                string wrk = Picked(_v.Pick, "wrk:");
                EstateHolding w = wrk != null ? _s.Holding(wrk) : null;
                if (w != null)
                {
                    WorksPage(w);
                    return;
                }
                _v.Pick = null;
                var rows = new List<UiDataRow>(16);
                var keys = new List<string>(16);
                for (int i = 0; Duties && i < _s.Holdings.Count; i++)
                {
                    EstateHolding h = _s.Holdings[i];
                    for (int n = 0; h.Managed && n < h.Units.Count; n++)
                    {
                        EstateUnit u = h.Units[n];
                        if (!u.Let || u.Call <= 0)
                        {
                            continue;
                        }
                        bool booked = u.CallAnswer == (int)CallAnswer.Contractor;
                        rows.Add(new UiDataRow
                        {
                            Cells = new[] { EstateText.UnitName(h, n), EstateText.Fault(u.Call),
                                EstateText.T(booked ? "reallife_ibox_chip_booked" : u.CallAnswer == (int)CallAnswer.Yourself ? "reallife_ibox_chip_due" : "reallife_ibox_chip_open"),
                                Money(u.CallCost), EstateText.T("reallife_estate_row_since", "day", u.CallDay.ToString(Inv)) },
                            Values = new[] { (int)UiIcon.Wrench, 0, 0, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, booked ? UiRole.Ok : UiRole.Warn, UiRole.None, UiRole.None },
                            Subs = new[] { EstateText.District(h.District), null, null, null, null }
                        });
                        keys.Add(EstateDirector.CallPick(h, n));
                    }
                }
                for (int i = 0; i < _s.Holdings.Count; i++)
                {
                    EstateHolding h = _s.Holdings[i];
                    int repair = EstateRules.Quote(h, EstateWork.Repair, -1, _c);
                    string state = h.Busy ? EstateText.Work(h.Work) : h.Cond < EstateCatalogue.ConditionUnfit
                        ? EstateText.T("reallife_ui_estate_unfit") : h.Cond < EstateCatalogue.ConditionPoor ? EstateText.T("reallife_ui_estate_poor")
                        : EstateText.T("reallife_ui_estate_sound");
                    UiRole tone = h.Busy ? UiRole.Notice : h.Cond < EstateCatalogue.ConditionUnfit ? UiRole.Danger : h.Cond < EstateCatalogue.ConditionPoor ? UiRole.Warn : UiRole.Ok;
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { h.Name, EstateText.T("reallife_ui_estate_condition_level", "condition", EstateText.Level(h.Cond), "level",
                            h.Level.ToString(Inv)), state, repair > 0 ? Money(repair) : "-",
                            h.Busy ? EstateText.T("reallife_estate_row_until", "day", h.WorkEnd.ToString(Inv)) : "-" },
                        Values = new[] { (int)UseIcon(h.Use), 0, 0, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, tone, UiRole.None, UiRole.None },
                        Subs = new[] { EstateText.Use(h.Use) + ", " + EstateText.District(h.District), null, null, null, null }
                    });
                    keys.Add("wrk:" + h.Place);
                }
                var cols = new[]
                {
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_building"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_what"), Kind = UiCellKind.Text, Width = 200f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_state"), Kind = UiCellKind.Chip, Width = 144f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_cost"), Kind = UiCellKind.Money, Width = 120f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_day"), Kind = UiCellKind.Date, Width = 112f }
                };
                int open = 0, bookedCalls = 0, unfit = 0, callsCost = 0;
                for (int i = 0; Duties && i < _s.Holdings.Count; i++)
                {
                    EstateHolding h = _s.Holdings[i];
                    unfit += h.Cond < EstateCatalogue.ConditionUnfit ? 1 : 0;
                    for (int n = 0; h.Managed && n < h.Units.Count; n++)
                    {
                        EstateUnit u = h.Units[n];
                        if (u.Let && u.Call > 0)
                        {
                            bookedCalls += u.CallAnswer == (int)CallAnswer.Contractor ? 1 : 0;
                            open += u.CallAnswer == (int)CallAnswer.Contractor ? 0 : 1;
                            callsCost += u.CallCost;
                        }
                    }
                }
                TableShape(UiIcon.Wrench, EstateText.T("reallife_estate_tab_works"),
                    EstateText.T("reallife_ui_estate_works_sub", "count", rows.Count.ToString(Inv)),
                    new[] { EstateText.T("reallife_ui_estate_fig_open_calls"), EstateText.T("reallife_ui_estate_fig_booked"), EstateText.T("reallife_ui_estate_fig_unfit"), EstateText.T("reallife_ui_estate_fig_calls_cost") },
                    new[] { open.ToString(Inv), bookedCalls.ToString(Inv), unfit.ToString(Inv), Money(callsCost) }, cols, rows, keys, null,
                    EstateText.T("reallife_estate_row_nothing_owned"), EstateText.T("reallife_ui_estate_empty_works_body"));
            }

            // One call: what it is, how long it has waited, what the tenant thinks, and the two ways to answer it.
            private bool CallPage(string rest)
            {
                int colon = rest.LastIndexOf(':');
                int unit;
                if (colon <= 0 || !int.TryParse(rest.Substring(colon + 1), out unit))
                {
                    return false;
                }
                string place = rest.Substring(0, colon);
                EstateHolding h = _s.Holding(place);
                EstateUnit u = h != null ? h.Unit(unit) : null;
                if (u == null || !u.Let || u.Call <= 0)
                {
                    return false;
                }
                bool contractor = u.CallAnswer == (int)CallAnswer.Contractor, yours = u.CallAnswer == (int)CallAnswer.Yourself;
                int materials = EstateRules.DiyCost(u.CallCost), minutes = EstateCatalogue.CallMinutes[EstateCatalogue.Clamp(u.Call, 0, EstateCatalogue.CallKinds)];
                _m.Shape = EstateShape.Detail;
                _m.Card = true;
                _m.Back = EstateText.T("reallife_co_back");
                _m.DetailIcon = UiIcon.Wrench;
                _m.DetailTitle = EstateText.UnitName(h, unit);
                _m.DetailSub = EstateText.Fault(u.Call);
                _m.DetailChip = EstateText.T(contractor ? "reallife_ibox_chip_booked" : yours ? "reallife_ibox_chip_due" : "reallife_ibox_chip_open");
                _m.DetailChipTone = u.CallAnswer == (int)CallAnswer.Waiting ? UiRole.Danger : UiRole.Warn;
                _m.DetailLocate = EstateText.T("reallife_ui_estate_locate");
                _m.DetailLocateRun = _runs.Do(() => EstateGame.Locate(place));
                // The prices and the waiting are figures; the card keeps what is to be done.
                _m.Strip = new[]
                {
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_ch_contractor"), Value = Money(u.CallCost) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_ch_yourself"), Value = Money(materials) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_reported"), Value = EstateText.T("reallife_ui_estate_days_ago", "days", Math.Max(0, _day - u.CallDay).ToString(Inv)) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_grace"), Value = EstateText.T("reallife_ui_estate_n_days", "days", EstateCatalogue.CallGraceDays.ToString(Inv)) }
                };
                var lines = new List<UiFactLine>();
                if (contractor)
                {
                    lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_call_booked", "day", u.CallDue.ToString(Inv)), UiRole.OkText));
                }
                else if (yours)
                {
                    lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_call_yours", "day", u.CallDue.ToString(Inv), "minutes",
                        minutes.ToString(Inv), "amount", Money(materials)), UiRole.WarnText));
                }
                else
                {
                    lines.Add(UiFactLine.Say(EstateText.T("reallife_ui_estate_call_minutes", "minutes", minutes.ToString(Inv))));
                }
                int repairsTerm, valueTerm, conditionTerm;
                HappyTerms.Of(h, unit, _day, _c, out repairsTerm, out valueTerm, out conditionTerm);
                _m.ShareTitle = EstateText.T("reallife_ui_estate_sh_call");
                _m.ShareLabels = new[] { EstateText.T("reallife_ui_estate_m_condition"), EstateText.T("reallife_ui_estate_m_satisfaction"), EstateText.T("reallife_ui_estate_m_repairs") };
                _m.ShareValues = new[] { conditionTerm, u.Happy < 0 ? 50 : u.Happy / 10, repairsTerm };
                Who who = WhoOf(h, u);
                StateChip(h, u, out string stateChip, out UiRole stateTone);
                _m.RelatedTitle = EstateText.T("reallife_ui_estate_the_tenant");
                _m.Related = new[]
                {
                    new EstateRow
                    {
                        Face = who.Name, Hue = who.Hue, Title = who.Name, Sub = who.Line, Chip = stateChip, ChipTone = stateTone, Amount = Month(u.Rent),
                        Run = _runs.Do(() => Open(EstateDirector.UnitPick(h, unit), null))
                    }
                };
                var actions = new List<UiActionModel>(2)
                {
                    _runs.Button(EstateText.T("reallife_estate_btn_yourself"), UiButtonKind.Secondary, UiIcon.MapPin, UiAction.Option,
                        () => Show(() => _d.DoItYourself(place, unit)), u.CallAnswer != (int)CallAnswer.Waiting),
                    _runs.Button(EstateText.T("reallife_estate_btn_contractor"), UiButtonKind.Primary, UiIcon.Wrench, UiAction.Alt, () => Show(() => _d.SendContractor(place, unit)), contractor)
                };
                _m.Left = new[] { new UiFactCardModel { Title = EstateText.T("reallife_ibox_sec_quote"), Heading = true, Lines = lines.ToArray(), Actions = actions.ToArray() } };
                return true;
            }

            private int ToUseNow => _v.ToUse;

            // One building's works: its condition, repair and refurbishing, a change of use, and the services booked ahead.
            private void WorksPage(EstateHolding h)
            {
                string place = h.Place;
                _m.Shape = EstateShape.Detail;
                _m.Card = true;
                _m.Back = EstateText.T("reallife_co_back");
                _m.DetailIcon = UiIcon.Wrench;
                _m.DetailTitle = h.Name;
                _m.DetailSub = EstateText.T("reallife_ibox_type_in", "type", EstateText.Use(h.Use), "district", EstateText.District(h.District));
                _m.Strip = new[]
                {
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_m_condition"), Value = EstateText.Level(h.Cond) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_worth_of_best"), Value = EstateText.Share(EstateRules.ConditionValue(h.Cond)) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_rents_of_best"), Value = EstateText.Share(EstateRules.ConditionRent(h.Cond)) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_booked"), Value = h.Services.Count.ToString(Inv) }
                };
                var cond = new List<UiFactLine>();
                var actions = new List<UiActionModel>(3);
                if (h.Busy)
                {
                    cond.Add(UiFactLine.Say(EstateText.T("reallife_estate_works_busy", "work", EstateText.Work(h.Work), "day", h.WorkEnd.ToString(Inv), "amount", Money(h.WorkCost)), UiRole.WarnText));
                }
                else
                {
                    int repair = EstateRules.Quote(h, EstateWork.Repair, -1, _c), refurbish = EstateRules.Quote(h, EstateWork.Refurbish, -1, _c);
                    if (repair > 0)
                    {
                        cond.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_works_repair_costs"), Money(repair)));
                        cond.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_works_repair_takes"), EstateText.T("reallife_ui_estate_n_days", "days", EstateRules.RepairDays(h.Cond, _c.PermitLever).ToString(Inv))));
                        actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_repair"), UiButtonKind.Primary, UiIcon.Wrench, UiAction.Alt,
                            () => Show(() => _d.StartWork(place, EstateWork.Repair, -1))));
                    }
                    if (refurbish > 0)
                    {
                        int gain = EstateRules.LevelGain(h, h.Level + 1, _s.Real, _c.Index);
                        cond.Add(UiFactLine.Say(EstateText.T("reallife_estate_works_refurbish", "level", (h.Level + 1).ToString(Inv), "amount", Money(refurbish),
                            "days", EstateRules.SiteDays(EstateCatalogue.LevelDays[h.Level + 1], _c.PermitLever).ToString(Inv))));
                        cond.Add(UiFactLine.Say(EstateText.T(gain >= refurbish ? "reallife_estate_works_gain_more" : "reallife_estate_works_gain_less", "gain", Money(gain),
                            "rent", EstateText.Share(EstateCatalogue.LevelRent[h.Level + 1] - EstateCatalogue.LevelRent[h.Level]), "market", EstateText.Share(_s.Real))));
                        actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_refurbish"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option,
                            () => Show(() => _d.StartWork(place, EstateWork.Refurbish, -1))));
                    }
                    else
                    {
                        cond.Add(UiFactLine.Say(EstateText.T(h.Level >= EstateCatalogue.LevelMost ? "reallife_estate_works_level_most" : "reallife_estate_works_need_empty")));
                    }
                }
                var left = new List<UiFactCardModel> { new UiFactCardModel { Title = EstateText.T("reallife_ibox_sec_works"), Heading = true, Lines = cond.ToArray(), Actions = actions.ToArray() } };
                if (!h.Busy && h.Managed && !h.AnyLet && !h.Self)
                {
                    left.Add(UseCard(h));
                }
                _m.Left = left.ToArray();
                UiFactCardModel service = ServiceCard(h);
                _m.GridTitle = EstateText.T("reallife_ui_estate_book_a_visit");
                _m.Grid = service.Actions;   // the visits that can be booked stand as equal buttons under the card, never a ragged run in it
                service.Actions = new UiActionModel[0];
                _m.Right = new[] { service };
            }

            // A change of use: the council first, then the builders.
            private UiFactCardModel UseCard(EstateHolding h)
            {
                string place = h.Place;
                var lines = new List<UiFactLine>(4);
                var actions = new List<UiActionModel>(1);
                if (h.PlanUse >= 0)
                {
                    int convert = EstateRules.Quote(h, EstateWork.Convert, -1, _c);
                    lines.Add(UiFactLine.Say(EstateText.Fill("reallife_estate_works_convert", "use", EstateText.Use(h.PlanUse), "amount",
                        Money(convert), "days", EstateRules.SiteDays(EstateCatalogue.ConvertDays, _c.PermitLever).ToString(Inv),
                        "yield", EstateText.Share(EstateRules.Yield(h.PlanUse, h.Income)))));
                    actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_convert"), UiButtonKind.Primary, UiIcon.None, UiAction.Count,
                        () => Show(() => _d.StartWork(place, EstateWork.Convert, -1))));
                }
                else
                {
                    int to = ToUseNow >= 0 && ToUseNow < EstateCatalogue.Uses && ToUseNow != h.Use ? ToUseNow : (h.Use + 1) % EstateCatalogue.Uses;
                    int use = h.Use;
                    lines.Add(_runs.Step(EstateText.T("reallife_estate_works_use"), EstateText.Use(to), null, true, true,
                        () => Show(() => _v.ToUse = NextUse(to, use, -1)), () => Show(() => _v.ToUse = NextUse(to, use, 1))));
                    if (to != h.Use)
                    {
                        lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_works_planning", "fee",
                            Money(EstateRules.PlanningFee(h.Base, _c.Index)), "days", EstateCatalogue.PlanningDays.ToString(Inv),
                            "chance", EstateText.Share(EstateCatalogue.PlanningChance[EstateCatalogue.Lever(_c.PermitLever)]))));
                        lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_works_yields", "now",
                            EstateText.Share(EstateRules.Yield(h.Use, h.Income)), "then", EstateText.Share(EstateRules.Yield(to, h.Income)),
                            "use", EstateText.Use(to))));
                        actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_planning"), UiButtonKind.Primary, UiIcon.None, UiAction.Count,
                            () => Show(() => _d.StartWork(place, EstateWork.Planning, to))));
                    }
                    else
                    {
                        lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_works_same_use")));
                    }
                }
                lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_works_use_note")));
                return new UiFactCardModel { Title = EstateText.T("reallife_estate_works_use"), Heading = true, Lines = lines.ToArray(), Actions = actions.ToArray() };
            }

            private static int NextUse(int to, int own, int step)
            {
                int n = EstateCatalogue.Uses;
                int next = to;
                for (int i = 0; i < n; i++)
                {
                    next = (next + step + n) % n;
                    if (next != own)
                    {
                        return next;
                    }
                }
                return to;
            }

            // The visits booked ahead: a planned service keeps a kind of fault down for a season, and is paid on its day.
            private UiFactCardModel ServiceCard(EstateHolding h)
            {
                string place = h.Place;
                var lines = new List<UiFactLine>(8);
                lines.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_service_keeps"), EstateText.T("reallife_ui_estate_n_days", "days", ServiceRules.Season(_c.DaysPerYear).ToString(Inv))));
                for (int b = 0; b < h.Services.Count; b++)
                {
                    ServiceBooking v = h.Services[b];
                    int at = v.Kind;   // the booking by its kind: a morning's re-sorting never cancels another
                    lines.Add(_runs.Link(EstateText.Fill("reallife_ui_estate_service_line", "fault", EstateText.Fault(v.Kind), "day",
                        v.Day.ToString(Inv), "who", ContractorName(v.Contractor), "amount", Money(v.Fee)),
                        () => Show(() => _d.CancelService(place, at))));
                }
                var actions = new List<UiActionModel>(EstateCatalogue.CallKinds);
                for (int kind = 1; kind <= EstateCatalogue.CallKinds; kind++)
                {
                    int k = kind;
                    bool booked = h.Services.Exists(s => s.Kind == k) || h.Services.Count >= EstateCatalogue.ServicesMost || !Duties;
                    actions.Add(_runs.Button(EstateText.T("reallife_ui_estate_book_kind", "fault", EstateText.Fault(kind), "amount",
                        Money(ServiceRules.Fee(h, kind, _c.Index))), UiButtonKind.Secondary, UiIcon.Calendar,
                        UiAction.Count, () => Show(() => _d.BookService(place, k)), booked));
                }
                return new UiFactCardModel { Title = EstateText.T("reallife_ui_estate_servicing"), Heading = true, Lines = lines.ToArray(), Actions = actions.ToArray() };
            }

        }
    }
}
