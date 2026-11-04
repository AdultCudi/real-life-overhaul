using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class EstateLive
    {
        private sealed partial class Page
        {
            // ------------------------------------------------------------------------------------------------- one unit

            private void UnitPage(EstateHolding h, int n)
            {
                EstateUnit u = h.Units[n];
                string place = h.Place;
                CityBuilding cb = EstateGame.Find(place);
                int sqm = cb != null && cb.B != null ? UnitRules.UnitSqm(cb.B.totalSqm, h.Units.Count) : 0;
                _m.Shape = EstateShape.Unit;
                _m.Back = EstateText.T("reallife_co_back");
                var p = new EstateUnitPage
                {
                    Title = EstateText.UnitName(h, n), District = EstateText.District(h.District), Locate = EstateText.T("reallife_ui_estate_locate"), Icon = UseIcon(h.Use),
                    LocateRun = _runs.Do(() => EstateGame.Locate(place)),
                    Rent = u.Let ? Month(u.Rent) : Month(EstateCatalogue.Share(EstateRules.UnitRent(h, _c), u.Ask)), RentPer = EstateText.T("reallife_ui_estate_a_month")
                };
                // The next inspection: a bar over the days between two.
                if (h.InspectDay > 0)
                {
                    int left = Math.Max(0, h.InspectDay - _day);
                    p.NextLabel = EstateText.T("reallife_ui_estate_next_inspection");
                    p.NextValue = left == 0 ? EstateText.T("reallife_ui_estate_today") : EstateText.T("reallife_ui_estate_in_days", "days", left.ToString(Inv));
                    p.NextFill = EstateCatalogue.Clamp(100 - left * 100 / Math.Max(1, EstateCatalogue.InspectEvery), 0, 100);
                }
                var labels = new List<string>(4);
                var values = new List<string>(4);
                if (h.Use == (int)EstateUse.Homes)
                {
                    int bedrooms = UnitRules.Bedrooms(sqm);
                    string[] bedKeys = { "reallife_ui_estate_bed_0", "reallife_ui_estate_bed_1", "reallife_ui_estate_bed_2", "reallife_ui_estate_bed_3", "reallife_ui_estate_bed_4",
                        "reallife_ui_estate_bed_5" };
                    labels.Add(EstateText.T(bedKeys[Math.Max(0, Math.Min(5, bedrooms))]));
                }
                else
                {
                    labels.Add(EstateText.T("reallife_ui_estate_floor"));
                }
                values.Add(sqm > 0 ? EstateText.T("reallife_ui_estate_sqm", "area", sqm.ToString(Inv)) : "-");
                labels.Add(EstateText.T("reallife_ui_estate_lease_ends"));
                values.Add(u.Let ? EstateText.T("reallife_ui_estate_day", "day", u.Until.ToString(Inv)) : "-");
                labels.Add(EstateText.T("reallife_ui_estate_deposit_held"));
                values.Add(u.Let ? Money(u.Deposit) : "-");
                labels.Add(EstateText.T("reallife_ui_estate_last_inspected"));
                values.Add(h.LastInspectDay > 0
                    ? EstateText.T("reallife_ui_estate_day", "day", h.LastInspectDay.ToString(Inv))
                        + (h.LastInspect == (int)InspectResult.Failed ? ", " + EstateText.T("reallife_ui_estate_failed") : string.Empty)
                    : EstateText.T("reallife_ui_estate_not_yet"));
                p.FactLabels = labels.ToArray();
                p.FactValues = values.ToArray();
                if (u.Let)
                {
                    LetUnit(h, n, u, p);
                }
                else
                {
                    EmptyUnit(h, n, u, p);
                }
                p.BuildingLink = EstateText.T("reallife_ui_estate_the_building");
                p.BuildingRun = _runs.Do(() => Open("own:" + place, EstateDirector.UnitPick(h, n)));
                // The market's rent for a unit like this when the lease ends.
                int market = EstateRules.UnitRent(h, _c);
                if (market > 0 && u.Let)
                {
                    p.MarketNote = EstateText.T("reallife_ui_estate_market_note", "amount", Month(market), "day", u.Until.ToString(Inv));
                }
                // The building's other units.
                var others = new List<EstateRow>(4);
                for (int i = 0; i < h.Units.Count && others.Count < 3; i++)
                {
                    if (i == n)
                    {
                        continue;
                    }
                    EstateUnit o = h.Units[i];
                    StateChip(h, o, out string chip, out UiRole tone);
                    int at = i;
                    Who ow = o.Let ? WhoOf(h, o) : default(Who);
                    others.Add(new EstateRow
                    {
                        Title = o.Let ? ow.Name : EstateText.T("reallife_estate_row_nobody"), Sub = EstateText.UnitName(h, i), Face = o.Let ? ow.Name
                            : null, Hue = ow.Hue, Icon = o.Let ? UiIcon.None : UseIcon(h.Use),
                        Chip = chip, ChipTone = tone, Amount = Month(o.Let ? o.Rent : EstateCatalogue.Share(market, o.Ask)), Run = _runs.Do(() => Open(EstateDirector.UnitPick(h, at), _v.From))
                    });
                }
                p.OthersTitle = EstateText.T("reallife_ui_estate_other_units");
                p.OthersCount = h.Units.Count > 1 ? (h.Units.Count - 1).ToString(Inv) : null;
                p.Others = others.ToArray();
                _m.Unit = p;
            }

            private void LetUnit(EstateHolding h, int n, EstateUnit u, EstateUnitPage p)
            {
                Who who = WhoOf(h, u);
                p.TenantTitle = EstateText.T("reallife_ui_estate_the_tenant");
                p.TenantName = who.Name;
                p.TenantHue = who.Hue;
                p.TenantRing = HappyRing(u.Happy);
                string since = u.PersonSince > 0 ? EstateText.T("reallife_ui_estate_here_since", "day", u.PersonSince.ToString(Inv)) : string.Empty;
                var line = new List<string>(3);
                if (!string.IsNullOrEmpty(who.Line))
                {
                    line.Add(who.Line);
                }
                if (who.Age > 0)
                {
                    line.Add(who.Age.ToString(Inv));
                }
                if (since.Length > 0)
                {
                    line.Add(since);
                }
                ITenantPool pool = EstateBridges.Pool != null ? EstateBridges.Pool() : null;
                if (TenantRules.IsEmployee(pool, u.PersonId))
                {
                    line.Add(EstateText.T("reallife_ui_estate_your_employee"));
                }
                p.TenantLine = string.Join(" · ", line.ToArray());
                StateChip(h, u, out string chip, out UiRole tone);
                p.TenantChip = u.Arrears > 0 || u.NoticeEnd > 0 || u.CourtDay > 0 ? chip : null;
                p.TenantChipTone = tone;
                if (who.Named)
                {
                    int id = u.PersonId;
                    p.TenantOpen = EstateText.T("reallife_ui_estate_open_page");
                    p.TenantOpenRun = _runs.Add(() => EstateLive.OpenPerson(id));
                }
                // What stands behind how satisfied they are.
                int repairs, value, condition;
                HappyTerms.Of(h, n, _day, _c, out repairs, out value, out condition);
                p.MeterLabels = new[]
                {
                    EstateText.T("reallife_ui_estate_m_satisfaction"), EstateText.T("reallife_ui_estate_m_repairs"), EstateText.T("reallife_ui_estate_m_value"), EstateText.T("reallife_ui_estate_m_condition")
                };
                p.MeterValues = new[] { u.Happy < 0 ? 50 : u.Happy / 10, repairs, value, condition };
                // The year of this flat: its share of the building's year.
                int units = Math.Max(1, h.Units.Count);
                long rent = h.RentYear / units, costs = h.CostYear / units;
                p.YearTitle = EstateText.T("reallife_ui_estate_year_title");
                p.YearLabels = new[] { EstateText.T("reallife_ui_estate_year_rent"), EstateText.T("reallife_ui_estate_year_works") };
                p.YearValues = new[] { Cash(rent), (costs > 0 ? "-" : string.Empty) + Cash(costs) };
                p.YearKeptLabel = EstateText.T("reallife_ui_estate_kept");
                p.YearKept = Cash(rent - costs);
                NeedCard(h, n, u, p, who);
                // The rent record: the running month if they owe, then the last months.
                p.RecordTitle = EstateText.T("reallife_ui_estate_record_title");
                var record = new List<EstateRecordRow>(EstateCatalogue.PaymentsKept + 1);
                if (u.Arrears > 0)
                {
                    record.Add(new EstateRecordRow
                    {
                        Due = EstateText.T("reallife_ui_estate_due_day", "day", Math.Max(1, _day - u.ArrearsDays).ToString(Inv)), What = EstateText.T("reallife_ui_estate_not_paid_yet"),
                        Chip = EstateText.T("reallife_ui_estate_late_days", "days", u.ArrearsDays.ToString(Inv)), ChipTone = u.ArrearsDays >= 10
                            ? UiRole.Danger : UiRole.Warn, Amount = Money(u.Arrears)
                    });
                }
                for (int i = 0; i < u.PaymentCount && record.Count < EstateCatalogue.PaymentsKept; i++)
                {
                    RentPayment r = u.Payments[i];
                    string chipText = r.State == (byte)PaymentState.OnTime ? EstateText.T("reallife_ui_estate_paid")
                        : r.State == (byte)PaymentState.Late ? EstateText.T("reallife_ui_estate_paid_late") : EstateText.T("reallife_ui_estate_missed");
                    record.Add(new EstateRecordRow
                    {
                        Due = EstateText.T("reallife_ui_estate_due_day", "day", r.Due.ToString(Inv)),
                        What = r.State == (byte)PaymentState.Missed ? EstateText.T("reallife_ui_estate_not_paid_yet") : EstateText.T("reallife_ui_estate_paid_on", "day", r.Paid.ToString(Inv)),
                        Chip = chipText, ChipTone = r.State == (byte)PaymentState.OnTime ? UiRole.Ok : r.State == (byte)PaymentState.Late ? UiRole.Warn : UiRole.Danger, Amount = Money(r.Amount)
                    });
                }
                if (record.Count == 0)
                {
                    p.RecordNote = EstateText.T("reallife_ui_estate_record_none");
                }
                else
                {
                    p.RecordNote = EstateText.T("reallife_ui_estate_last_months", "count", record.Count.ToString(Inv));
                }
                p.Record = record.ToArray();
                p.WorksTitle = EstateText.T("reallife_ui_estate_works_here");
                var works = WorkRows(h, n, u);
                p.Works = works.ToArray();
                p.WorksCount = works.Count > 0 ? works.Count.ToString(Inv) : null;
            }

            // What needs the landlord, the most pressing first: a tenant's request, rent that is late, a repair call waiting.
            private void NeedCard(EstateHolding h, int n, EstateUnit u, EstateUnitPage p, Who who)
            {
                string place = h.Place;
                string first = who.Named ? who.Name.Split(' ')[0] : who.Name;
                if (u.Improve > 0 && Duties)
                {
                    p.NeedKicker = EstateText.T("reallife_ui_estate_needs_you", "what", EstateText.T("reallife_ui_estate_need_request"));
                    p.NeedHead = EstateText.T("reallife_ui_estate_improve_head", "name", first, "what", EstateText.Improve(u.Improve), "amount", Money(u.ImproveCost));
                    p.NeedBody = EstateText.T("reallife_ui_estate_improve_body", "refusals", u.Refused.ToString(Inv));
                    p.NeedActions = new[]
                    {
                        _runs.Button(EstateText.T("reallife_ui_estate_make_it"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, () => Show(() => _d.AnswerImprovement(place, n, true))),
                        _runs.Button(EstateText.T("reallife_estate_btn_refuse"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() => _d.AnswerImprovement(place, n, false)))
                    };
                    return;
                }
                if (u.Arrears > 0 && Duties)
                {
                    p.NeedKicker = EstateText.T("reallife_ui_estate_needs_you", "what", EstateText.T("reallife_ui_estate_need_rent"));
                    p.NeedHead = EstateText.T("reallife_ui_estate_late_head", "name", first, "days", u.ArrearsDays.ToString(Inv), "amount", Money(u.Arrears));
                    p.NeedBody = EstateText.T("reallife_estate_ten_arrears", "amount", Money(u.Arrears), "days", u.ArrearsDays.ToString(Inv), "back",
                        EstateText.Share(EstateRules.CatchUpChance(u.Record)));
                    var actions = new List<UiActionModel>(3);
                    bool remind = !u.Reminded && !u.OnPlan && u.CourtDay == 0;
                    actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_remind"), UiButtonKind.Primary, UiIcon.None, UiAction.Alt, () => Show(() => _d.Remind(place, n)), !remind));
                    bool plan = EstateRules.MayOfferPlan(u);
                    actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_plan"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() => _d.OfferPlan(place, n)), !plan));
                    p.NeedActions = actions.ToArray();
                    if (EstateRules.MayServeNotice(h, n, _day))
                    {
                        p.NeedLink = EstateText.T("reallife_estate_btn_notice");
                        p.NeedLinkRun = _runs.Do(() => Show(() => _d.ServeNotice(place, n)));
                    }
                    else if (EstateRules.MayGoToCourt(h, n, _day))
                    {
                        p.NeedLink = EstateText.T("reallife_estate_btn_court");
                        p.NeedLinkRun = _runs.Do(() => Show(() => _d.GoToCourt(place, n)));
                    }
                    return;
                }
                if (u.Call > 0 && Duties)
                {
                    p.NeedKicker = EstateText.T("reallife_ui_estate_needs_you", "what", EstateText.T("reallife_ui_estate_need_repair"));
                    p.NeedHead = EstateText.T("reallife_ui_estate_call_head", "fault", EstateText.Fault(u.Call), "name", first);
                    p.NeedBody = EstateText.T("reallife_estate_call_since", "day", u.CallDay.ToString(Inv), "days",
                        Math.Max(0, _day - u.CallDay).ToString(Inv), "grace", EstateCatalogue.CallGraceDays.ToString(Inv));
                    bool contractor = u.CallAnswer != (int)CallAnswer.Contractor;
                    p.NeedActions = new[]
                    {
                        _runs.Button(EstateText.T("reallife_estate_btn_contractor"), UiButtonKind.Primary, UiIcon.Wrench, UiAction.Alt, () => Show(() => _d.SendContractor(place, n)), !contractor),
                        _runs.Button(EstateText.T("reallife_estate_btn_yourself"), UiButtonKind.Secondary, UiIcon.MapPin, UiAction.Option,
                            () => Show(() => _d.DoItYourself(place, n)), u.CallAnswer != (int)CallAnswer.Waiting)
                    };
                }
            }

            // This flat's works: a call waiting or booked, a request, and the visits booked at the building.
            private List<EstateRow> WorkRows(EstateHolding h, int n, EstateUnit u)
            {
                var rows = new List<EstateRow>(4);
                string place = h.Place;
                for (int b = 0; b < h.Services.Count && rows.Count < 3; b++)
                {
                    ServiceBooking v = h.Services[b];
                    rows.Add(new EstateRow
                    {
                        Icon = UiIcon.Wrench, Title = EstateText.T("reallife_ui_estate_service_name", "fault", EstateText.Fault(v.Kind)),
                        Sub = EstateText.T("reallife_ui_estate_booked_with", "who", ContractorName(v.Contractor)), Chip = EstateText.T("reallife_ui_estate_day", "day", v.Day.ToString(Inv)),
                        ChipTone = UiRole.Notice, Amount = Money(v.Fee), Run = _runs.Do(() => Show(() => OpenWorks("wrk:" + place)))
                    });
                }
                if (u.Call > 0)
                {
                    bool booked = u.CallAnswer == (int)CallAnswer.Contractor;
                    rows.Add(new EstateRow
                    {
                        Icon = UiIcon.Wrench, Title = EstateText.Fault(u.Call), Sub = EstateText.T("reallife_estate_row_since", "day", u.CallDay.ToString(Inv)),
                        Chip = EstateText.T(booked ? "reallife_ibox_chip_booked" : "reallife_ui_estate_quote_asked"), ChipTone = booked ? UiRole.Ok : UiRole.Warn, Amount = Money(u.CallCost),
                        Run = _runs.Do(() => Show(() => OpenWorks(EstateDirector.CallPick(h, n))))
                    });
                }
                if (u.Improve > 0)
                {
                    rows.Add(new EstateRow
                    {
                        Icon = UiIcon.Wrench, Title = EstateText.Improve(u.Improve), Sub = EstateText.T("reallife_ui_estate_asked_by_tenant"),
                        Chip = EstateText.T("reallife_ui_estate_waiting_you"), ChipTone = UiRole.Warn, Amount = Money(u.ImproveCost), Run = -1
                    });
                }
                return rows;
            }

            private static string ContractorName(string key)
            {
                if (string.IsNullOrEmpty(key))
                {
                    return EstateText.T("reallife_ui_estate_local_contractor");
                }
                return Population.PopulationGame.BusinessName(key);
            }

            // A unit with nobody in: what it asks, whether applicants are looked for, and the market.
            private void EmptyUnit(EstateHolding h, int n, EstateUnit u, EstateUnitPage p)
            {
                string place = h.Place;
                int market = EstateRules.UnitRent(h, _c);
                var card = new UiFactCardModel { Title = EstateText.T("reallife_ui_estate_empty_title", "days", u.VoidDays.ToString(Inv)),
                    Chip = EstateText.T("reallife_ibox_chip_empty"), ChipTone = UiRole.Warn };
                var lines = new List<UiFactLine>(8);
                lines.Add(UiFactLine.Say(market > 0
                    ? EstateText.Fill("reallife_estate_ten_market", "amount", Money(market), "use", EstateText.Use(h.Use), "yield",
                        EstateText.Share(EstateRules.Yield(h.Use, h.Income)), "condition", EstateText.Level(h.Cond))
                    : EstateText.T("reallife_estate_ten_unfit", "condition", EstateText.Level(h.Cond))));
                int steps = (EstateCatalogue.AskMost - EstateCatalogue.AskLeast) / EstateCatalogue.AskStep;
                int ask = EstateCatalogue.Clamp(u.Ask, EstateCatalogue.AskLeast, EstateCatalogue.AskMost);
                lines.Add(_runs.Step(EstateText.T("reallife_estate_ten_asking"), EstateText.Share(ask),
                    EstateText.T("reallife_estate_ten_asking_what", "amount", Money(EstateCatalogue.Share(market, ask)), "chance",
                        EstateText.Share(EstateRules.ApplicantChance(ask, h.Cond, _c.Cycle))),
                    ask > EstateCatalogue.AskLeast, ask < EstateCatalogue.AskMost,
                    () => Show(() => _d.SetAsk(place, n, ask - EstateCatalogue.AskStep)), () => Show(() => _d.SetAsk(place, n, ask + EstateCatalogue.AskStep))));
                bool toLet = u.ToLet;
                lines.Add(_runs.Switch(EstateText.T("reallife_estate_ten_to_let"), toLet, on => Show(() => _d.SetToLet(place, n, on))));
                if (u.Applicants.Count > 0)
                {
                    lines.Add(_runs.Link(EstateText.T("reallife_ui_estate_applicants", "count", u.Applicants.Count.ToString(Inv)), () => Show(() =>
                    {
                        _v.Tab = Array.IndexOf(NavTab, EstateDirector.TabTenants);
                        _v.Pick = "app:" + place + ":" + u.Applicants[0].Id;
                    })));
                }
                card.Lines = lines.ToArray();
                p.Empty = card;
                if (steps < 0)
                {
                    p.Empty = null;
                }
            }
        }
    }
}
