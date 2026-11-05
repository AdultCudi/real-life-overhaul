using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class EstateLive
    {
        private sealed partial class Page
        {
            // The picked row is a unit, a building, a hotel or the car hire lot; a hotel and the lot are looked after on their own tabs.
            public void Mine()
            {
                string pick = _v.Pick;
                if (pick != null && pick.StartsWith("hot:", StringComparison.Ordinal))
                {
                    _v.Tab = Array.IndexOf(NavTab, EstateDirector.TabHotel);
                    _m.Tab = _v.Tab;
                    Hotel();
                    return;
                }
                if (pick != null && pick.StartsWith("lot:", StringComparison.Ordinal))
                {
                    _v.Tab = Array.IndexOf(NavTab, EstateDirector.TabHire);
                    _v.Pick = null;
                    _m.Tab = _v.Tab;
                    Hire();
                    return;
                }
                EstateHolding h;
                int unit;
                if (Unit(pick, out h, out unit))
                {
                    UnitPage(h, unit);
                    return;
                }
                string place = Picked(pick, "own:");
                h = place != null ? _s.Holding(place) : null;
                if (h != null)
                {
                    BuildingPage(h);
                    return;
                }
                _v.Pick = null;
                Roll();
            }

            private static string Picked(string pick, string prefix) => pick != null && pick.StartsWith(prefix, StringComparison.Ordinal) ? pick.Substring(prefix.Length) : null;

            // "unt:<place>:<unit>"; false when the pick is not a unit the player lets.
            private bool Unit(string pick, out EstateHolding h, out int unit)
            {
                h = null;
                unit = 0;
                string rest = Picked(pick, "unt:");
                int colon = rest != null ? rest.LastIndexOf(':') : -1;
                if (colon <= 0 || !int.TryParse(rest.Substring(colon + 1), out unit))
                {
                    return false;
                }
                h = _s.Holding(rest.Substring(0, colon));
                return h != null && h.Managed && h.Unit(unit) != null;
            }

            private static UiIcon UseIcon(int use) => use == (int)EstateUse.Shops ? UiIcon.Store : use == (int)EstateUse.Offices ? UiIcon.Briefcase
                : use == (int)EstateUse.Storage ? UiIcon.Package : UiIcon.Home;

            private static UiRole HappyRing(int happy) =>
                happy < 0 ? UiRole.None : happy >= EstateCatalogue.HappyFair ? UiRole.Ok : happy >= EstateCatalogue.HappyPoor ? UiRole.Warn : UiRole.Danger;

            // Who a let unit is let to: the person of the people pool, or the old description of one for a lease with none.
            private struct Who
            {
                public string Name, Line;
                public float Hue;
                public bool Named;
                public int Age;
            }

            private Who WhoOf(EstateHolding h, EstateUnit u)
            {
                EstatePerson p = EstateLive.ReadPerson(u.PersonId);
                string role = EstateText.Tenant(h.Use, u.Tenant);
                if (p.Found)
                {
                    return new Who { Name = p.Name, Line = h.Use == (int)EstateUse.Homes ? p.Job : role + ", " + p.Job, Hue = UiColour.HueOf(p.Name), Named = true, Age = p.Age };
                }
                return new Who { Name = role, Line = string.Empty, Hue = UiColour.HueOf(role) };
            }

            // The state of a let unit as a chip: what the landlord should see at a glance.
            private void StateChip(EstateHolding h, EstateUnit u, out string chip, out UiRole tone)
            {
                if (!u.Let)
                {
                    chip = EstateText.Empty(u.VoidDays);
                    tone = UiRole.Warn;
                }
                else if (u.CourtDay > 0)
                {
                    chip = EstateText.T("reallife_ibox_chip_in_court");
                    tone = UiRole.Danger;
                }
                else if (u.NoticeEnd > 0)
                {
                    chip = EstateText.T("reallife_ibox_chip_notice");
                    tone = UiRole.Warn;
                }
                else if (u.Arrears > 0)
                {
                    chip = EstateText.T("reallife_ui_estate_late_days", "days", u.ArrearsDays.ToString(Inv));
                    tone = u.ArrearsDays >= 10 ? UiRole.Danger : UiRole.Warn;
                }
                else
                {
                    chip = EstateText.T("reallife_ui_estate_paid");
                    tone = UiRole.Ok;
                }
            }

            // ------------------------------------------------------------------------------------------------- the rent roll

            private static UiDataColumn[] RollColumns() => new[]
            {
                new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_unit"), Kind = UiCellKind.Tile, Width = 0f },
                new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_tenant"), Kind = UiCellKind.Person, Width = 248f },
                new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_lease_ends"), Kind = UiCellKind.Date, Width = 112f },
                new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_rent_month"), Kind = UiCellKind.Money, Width = 128f },
                new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_state"), Kind = UiCellKind.Chip, Width = 144f },
                new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_satisfaction"), Kind = UiCellKind.Meter, Width = 168f }
            };

            private void Roll()
            {
                var rows = new List<UiDataRow>(24);
                var keys = new List<string>(24);
                var lines = new List<RollLine>(24);   // the units, for the head's sums (the samples add up the same lines the same way)
                int units = 0, buildings = 0;
                for (int i = 0; i < _s.Holdings.Count; i++)
                {
                    EstateHolding h = _s.Holdings[i];
                    buildings++;
                    if (!h.Managed)
                    {
                        rows.Add(BuildingRow(h));
                        keys.Add("own:" + h.Place);
                        units++;
                        continue;
                    }
                    for (int n = 0; n < h.Units.Count; n++)
                    {
                        EstateUnit u = h.Units[n];
                        units++;
                        lines.Add(new RollLine { Let = u.Let, RentDay = u.Rent, Arrears = u.Arrears, AskDay = u.Let ? 0 : EstateCatalogue.Share(EstateRules.UnitRent(h, _c), u.Ask) });
                        rows.Add(UnitRow(h, n));
                        keys.Add(EstateDirector.UnitPick(h, n));
                    }
                }
                for (int i = 0; _d.On(EstatePart.Hotel) && i < _s.Hotels.Count; i++)
                {
                    HotelFile f = _s.Hotels[i];
                    rows.Add(OtherRow(UiIcon.Building, f.Name, EstateText.T(f.Leased ? "reallife_estate_row_hotel_leased" : "reallife_estate_row_hotel_owned"),
                        f.Takings - f.Costs, EstateText.T("reallife_estate_kind_hotel"), f.Takings < f.Costs ? UiRole.Danger : UiRole.None));
                    keys.Add("hot:" + f.Slot);
                }
                if (_d.On(EstatePart.Hire) && _s.Hire.Running)
                {
                    rows.Add(OtherRow(UiIcon.Car, EstateGame.NameOf(_s.Hire.Lot),
                        EstateText.T("reallife_estate_row_lot", "out", _s.Hire.Count(HireState.Out).ToString(Inv), "count", _s.Hire.Cars.Count.ToString(Inv)),
                        0, EstateText.T("reallife_estate_kind_lot"), UiRole.None));
                    keys.Add("lot:");
                }
                RollTotals roll = EstateFigures.Roll(lines, _monthDays);
                long due = roll.Due, owed = roll.Owed, empty = roll.EmptyAsking;
                int let = roll.Let;
                _m.Shape = EstateShape.Roll;
                _m.HeadIcon = UiIcon.Home;
                _m.HeadTitle = EstateText.T("reallife_ui_estate_roll_title");
                _m.HeadSub = _d.Waiting() > 0 ? EstateText.T("reallife_ui_estate_roll_sub", "units", units.ToString(Inv), "buildings", buildings.ToString(Inv), "waiting", _d.Waiting().ToString(Inv))
                    : EstateText.T("reallife_ui_estate_roll_sub_clear", "units", units.ToString(Inv), "buildings", buildings.ToString(Inv));
                _m.FigLabels = new[] { EstateText.T("reallife_ui_estate_fig_due"), EstateText.T("reallife_ui_estate_fig_collected"),
                    EstateText.T("reallife_ui_estate_fig_arrears"), EstateText.T("reallife_ui_estate_fig_empty") };
                _m.FigValues = new[] { Cash(due), Cash(roll.Collected), Cash(owed), Cash(empty) };
                _m.FigInks = new[] { UiRole.None, UiRole.None, owed > 0 ? UiRole.DangerText : UiRole.None, empty > 0 ? UiRole.WarnText : UiRole.None };
                var t = new UiDataTableModel { Columns = RollColumns(), Rows = rows.ToArray(), RowCount = rows.Count };
                for (int i = 0; i < t.Rows.Length; i++)
                {
                    t.Selected = -1;
                }
                if (rows.Count > 0)
                {
                    t.TotalsLabel = EstateText.T("reallife_ui_estate_total_let", "count", let.ToString(Inv));
                    t.Totals = new[] { string.Empty, string.Empty, string.Empty, Cash(due), string.Empty, string.Empty };   // the collected figure is the head's: said once
                }
                t.EmptyTitle = EstateText.T("reallife_estate_row_nothing_owned");
                t.EmptyBody = EstateText.T("reallife_ui_estate_empty_roll_body");
                t.EmptyIcon = UiIcon.Home;
                _m.Table = t;
                _m.RowKeys = keys.ToArray();
                // The two lists under it: what is due for repair, and the other landlords of the city.
                _m.ListATitle = EstateText.T("reallife_ui_estate_due_repair");
                _m.ListAEmpty = EstateText.T("reallife_ui_estate_no_repairs");
                _m.ListA = RepairRows();
                _m.ListBTitle = EstateText.T("reallife_ui_estate_other_landlords");
                _m.ListBEmpty = EstateText.T("reallife_ui_estate_no_landlords");
                _m.ListB = LandlordRows();
                // The chart under a short roll: the city's mean flat rent, a month at a time, from the ring the day turns keep.
                if (_s.RentRingCount >= 2)
                {
                    var months = new string[_s.RentRingCount];
                    var rents = new float[_s.RentRingCount];
                    for (int i = 0; i < months.Length; i++)
                    {
                        months[i] = EstateText.T("reallife_ui_estate_month_n", "month", _s.RentRingMonths[i].ToString(Inv));
                        rents[i] = _s.RentRing[i];
                    }
                    _m.Lower = EstateCharts.Line(EstateText.T("reallife_ui_estate_ring_chart"), months, rents, EstateText.T("reallife_ui_estate_rv_mean_flat"), EstateCharts.Dollars);
                }
            }

            private string Cash(long amount) => EstateText.Money((float)Math.Min(amount, int.MaxValue / 2));

            private UiDataRow UnitRow(EstateHolding h, int n)
            {
                EstateUnit u = h.Units[n];
                StateChip(h, u, out string chip, out UiRole tone);
                Who who = u.Let ? WhoOf(h, u) : default(Who);
                string unitName = EstateText.UnitName(h, n);
                var row = new UiDataRow
                {
                    Cells = new[] { unitName, u.Let ? who.Name : EstateText.T("reallife_estate_row_nobody"), u.Let ? EstateText.T("reallife_estate_row_until", "day", u.Until.ToString(Inv)) : "-",
                        u.Let ? Month(u.Rent) : Month(EstateCatalogue.Share(EstateRules.UnitRent(h, _c), u.Ask)), chip, u.Let && u.Happy >= 0 ? (u.Happy / 10).ToString(Inv) : string.Empty },
                    Values = new[] { (int)UseIcon(h.Use), u.Let ? 0 : UiDataTable.GlyphFace(UiIcon.People), 0, 0, 0, u.Let && u.Happy >= 0 ? u.Happy / 10 : -1 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, u.Let ? UiRole.None : UiRole.Muted, tone, UiRole.None },
                    Subs = new[] { EstateText.District(h.District), u.Let ? who.Line : EstateText.T("reallife_ui_estate_asking_note"), null, null, null, null },
                    Hue = who.Hue,
                    Ring = u.Let ? HappyRing(u.Happy) : UiRole.None
                };
                return row;
            }

            private UiDataRow BuildingRow(EstateHolding h)
            {
                string chip = h.SaleDay > 0 ? EstateText.T("reallife_ibox_chip_sold") : h.Busy ? EstateText.Work(h.Work) : h.Self
                    ? EstateText.T("reallife_estate_state_self") : EstateText.T("reallife_estate_state_game");
                int income = EstateRules.GameNight(EstateGame.GameRent(h), _c.RentScale);
                return new UiDataRow
                {
                    Cells = new[] { h.Name, EstateText.T("reallife_estate_kind_building"), "-", Month(income), chip, string.Empty },
                    Values = new[] { (int)UseIcon(h.Use), 0, 0, 0, 0, -1 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.Muted, h.Busy ? UiRole.Warn : UiRole.None, UiRole.None },
                    Subs = new[] { EstateText.District(h.District), EstateText.Use(h.Use), null, null, null, null }
                };
            }

            private UiDataRow OtherRow(UiIcon icon, string name, string line, int perDay, string kind, UiRole tone)
            {
                return new UiDataRow
                {
                    Cells = new[] { name, kind, "-", perDay != 0 ? Month(perDay) : "-", line, string.Empty },
                    Values = new[] { (int)icon, 0, 0, 0, 0, -1 },
                    // A hotel's or a lot's night is not rent that is due, so its figure stands in the muted ink unless it is a loss.
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, tone == UiRole.None ? UiRole.Muted : tone, UiRole.None, UiRole.None },
                    Subs = new[] { kind, string.Empty, null, null, null, null }
                };
            }

            // The calls and booked services waiting, the nearest first: what is due for repair.
            private EstateRow[] RepairRows()
            {
                var rows = new List<EstateRow>(4);
                for (int i = 0; i < _s.Holdings.Count && rows.Count < 6; i++)
                {
                    EstateHolding h = _s.Holdings[i];
                    for (int b = 0; b < h.Services.Count && rows.Count < 6; b++)
                    {
                        ServiceBooking v = h.Services[b];
                        string pick = "wrk:" + h.Place;
                        rows.Add(new EstateRow
                        {
                            Icon = UiIcon.Wrench, Title = EstateText.T("reallife_ui_estate_service_title", "fault", EstateText.FaultNoun(v.Kind), "building", h.Name), Sub = null,
                            Chip = EstateText.T("reallife_ui_estate_day", "day", v.Day.ToString(Inv)), ChipTone = UiRole.Notice, Amount = Money(v.Fee),
                            Run = -1
                        });
                    }
                    for (int n = 0; h.Managed && n < h.Units.Count && rows.Count < 6; n++)
                    {
                        EstateUnit u = h.Units[n];
                        if (!u.Let || u.Call <= 0)
                        {
                            continue;
                        }
                        string pick = EstateDirector.CallPick(h, n);
                        bool booked = u.CallAnswer == (int)CallAnswer.Contractor;
                        rows.Add(new EstateRow
                        {
                            Icon = UiIcon.Wrench, Title = EstateText.T("reallife_ui_estate_service_title", "fault", EstateText.FaultNoun(u.Call), "building", EstateText.UnitName(h, n)), Sub = null,
                            Chip = EstateText.T(booked ? "reallife_ibox_chip_booked" : "reallife_ui_estate_quote_asked"), ChipTone = booked ? UiRole.Ok : UiRole.Warn, Amount = Money(u.CallCost),
                            Run = -1
                        });
                    }
                }
                rows.Sort((a, b) => 0);
                return rows.Count > 3 ? rows.GetRange(0, 3).ToArray() : rows.ToArray();
            }

            private void OpenWorks(string pick)
            {
                _v.Tab = Array.IndexOf(NavTab, EstateDirector.TabWorks);
                _v.Pick = pick;
                _v.From = null;
            }

            // The other landlords of the city: the game's rival-owned buildings, their units and what a flat of theirs rents for, and its change on the month.
            private EstateRow[] LandlordRows()
            {
                RentMapData map = _d.Rents;
                var rows = new List<EstateRow>(3);
                var ordered = new List<RentLandlord>(map.Landlords);
                ordered.Sort((a, b) => b.Units.CompareTo(a.Units));
                for (int i = 0; i < ordered.Count && rows.Count < 3; i++)
                {
                    RentLandlord l = ordered[i];
                    int change = RentRingRules.LandlordChange(_s, l.Id);
                    string name = EstateGame.RivalName(l.Id);
                    rows.Add(new EstateRow
                    {
                        Title = EstateText.T("reallife_ui_estate_landlord_line", "name", name, "units", l.Units.ToString(Inv), "rent", Money(l.Mean(map.MonthDays))),
                        Face = name, Hue = UiColour.HueOf(name),
                        Amount = change == int.MinValue ? string.Empty : ((change >= 0 ? "+" : string.Empty) + EstateText.Share(change)),
                        AmountInk = change > 0 ? UiRole.OkText : change < 0 ? UiRole.WarnText : UiRole.None, Run = -1
                    });
                }
                return rows.ToArray();
            }
        }
    }
}
