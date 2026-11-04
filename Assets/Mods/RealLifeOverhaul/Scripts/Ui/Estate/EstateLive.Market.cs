using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using Streets;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class EstateLive
    {
        private sealed partial class Page
        {
            // ------------------------------------------------------------------------------------------------- the market

            public void Market()
            {
                if (_v.Pick == "rents")
                {
                    RentsView();
                    return;
                }
                string mkt = Picked(_v.Pick, "mkt:");
                CityBuilding listed = mkt != null ? EstateGame.Find(mkt) : null;
                if (listed != null && ListingPage(listed))
                {
                    return;
                }
                string htl = Picked(_v.Pick, "htl:");
                HotelSlot slot = null;
                for (int i = 0; htl != null && i < TravelCatalogue.Hotels.Length; i++)
                {
                    slot = TravelCatalogue.Hotels[i].Id == htl ? TravelCatalogue.Hotels[i] : slot;
                }
                if (slot != null && _s.Hotel(slot.Id) == null)
                {
                    HotelForSalePage(slot);
                    return;
                }
                _v.Pick = null;
                GameInstance save = SaveGameManager.Current;
                var rows = new List<UiDataRow>(64);
                var keys = new List<string>(64);
                bool property = _d.On(EstatePart.Property);
                for (int i = 0; property && save.buildingsForSale != null && i < save.buildingsForSale.Count && rows.Count < 400; i++)
                {
                    BuildingForSale f = save.buildingsForSale[i];
                    CityBuilding b = f != null && f.address != null ? EstateGame.Find(EstateGame.Place(f.address.streetName, f.address.streetNumber)) : null;
                    if (b == null || b.Holding != null || b.B == null || b.B.IsHamptonsHouse())
                    {
                        continue;
                    }
                    int cond = EstateRules.StreetCondition(_s.Seed, b.Key), use = EstateGame.UseOf(b.B.BuildingType);
                    int rent = EstateRules.MarketRent(b.Base, use, Math.Max(0, (int)Math.Round(CityEconomy.DistrictIncome(b.District) * 10f)), cond, 0, _c);
                    string yield = rent > 0 && f.buildingPrice > 0f ? EstateText.Yield((int)((long)rent * _c.DaysPerYear * 10000 / (long)f.buildingPrice)) : "-";
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { f.address.ToFormattedString(), Money((int)Math.Min(int.MaxValue / 2, f.buildingPrice)), EstateText.Level(cond), rent > 0 ? Month(rent) : "-", yield },
                        Values = new[] { (int)UseIcon(use), 0, 0, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, EstateRules.RunDown(cond) ? UiRole.Warn : UiRole.None, UiRole.None, UiRole.None },
                        Subs = new[] { EstateText.Use(use) + ", " + EstateText.District(b.District), null, null, null, null }
                    });
                    keys.Add("mkt:" + b.Place);
                }
                for (int i = 0; _d.On(EstatePart.Hotel) && i < TravelCatalogue.Hotels.Length; i++)
                {
                    HotelSlot hs = TravelCatalogue.Hotels[i];
                    if (_s.Hotel(hs.Id) != null)
                    {
                        continue;
                    }
                    int price = EstateGame.HotelPrice(hs, _s.Real, _c.DaysPerYear);
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { EstateGame.HotelName(hs), Money(price), "-", "-", EstateText.T("reallife_estate_row_going_concern") },
                        Values = new[] { (int)UiIcon.Building, 0, 0, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                        Subs = new[] { EstateText.T("reallife_estate_row_hotel") + ", " + EstateText.District(hs.Neighbourhood), null, null, null, null }
                    });
                    keys.Add("htl:" + hs.Id);
                }
                var cols = new[]
                {
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_building"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_price"), Kind = UiCellKind.Money, Width = 128f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_condition"), Kind = UiCellKind.Text, Width = 104f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_rent_month"), Kind = UiCellKind.Money, Width = 120f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_yield"), Kind = UiCellKind.Figure, Width = 96f }
                };
                var side = new UiFactCardModel
                {
                    Title = EstateText.T("reallife_estate_tab_market"), Heading = true, Chip = EstateText.Cycle(_c.Cycle), ChipTone = UiRole.Notice,
                    Lines = new[]
                    {
                        UiFactLine.Row(EstateText.T("reallife_estate_market_level"), EstateText.Share(_s.Real)),
                        UiFactLine.Say(EstateText.T("reallife_estate_market_heading", "cycle", EstateText.Cycle(_c.Cycle), "target", EstateText.Share(EstateRules.RealTarget(_c.Cycle, _c.Quarters)))),
                        UiFactLine.Row(EstateText.T("reallife_estate_market_prices"), EstateText.Share(_c.Index)),
                        UiFactLine.Say(EstateText.T("reallife_estate_market_what")),
                        UiFactLine.Say(EstateText.T("reallife_estate_market_rents"))
                    }
                };
                TableShape(UiIcon.Tag, EstateText.T("reallife_estate_tab_market"), EstateText.T("reallife_ui_estate_market_sub", "count", rows.Count.ToString(Inv)), null, null, cols, rows, keys, side,
                    EstateText.T("reallife_estate_row_no_market"), EstateText.T("reallife_ui_estate_empty_market_body"));
                _m.TableLink = EstateText.T("reallife_ui_estate_view_map");
                _m.TableLinkRun = _runs.Do(() => Show(() => _v.Pick = "rents"));
            }

            // One of the game's listings: what makes its price, and the way to the game's own purchase screen.
            private bool ListingPage(CityBuilding b)
            {
                int cond = EstateRules.StreetCondition(_s.Seed, b.Key), income = Math.Max(0, (int)Math.Round(CityEconomy.DistrictIncome(b.District) * 10f));
                int use = EstateGame.UseOf(b.B.BuildingType);
                int value = EstateGame.Value(b, _s, _c.Index);
                int rent = EstateRules.MarketRent(b.Base, use, income, cond, 0, _c);
                string place = b.Place;
                _m.Shape = EstateShape.Detail;
                _m.Back = EstateText.T("reallife_co_back");
                _m.DetailIcon = UseIcon(use);
                _m.DetailTitle = EstateGame.NameOf(place);
                _m.DetailSub = EstateText.T("reallife_ibox_type_in", "type", EstateText.Use(use), "district", EstateText.District(b.District));
                _m.DetailChip = EstateText.Level(cond);
                _m.DetailChipTone = EstateRules.RunDown(cond) ? UiRole.Warn : UiRole.Ok;
                _m.DetailLocate = EstateText.T("reallife_ui_estate_locate");
                _m.DetailLocateRun = _runs.Do(() => EstateGame.Locate(place));
                var made = new List<UiFactLine>
                {
                    UiFactLine.Row(EstateText.T("reallife_estate_listing_price"), Money(value)),
                    UiFactLine.Say(EstateText.T("reallife_estate_listing_base", "amount", Money(b.Base))),
                    UiFactLine.Say(EstateText.T("reallife_estate_listing_market", "market", EstateText.Share(_s.Real), "prices", EstateText.Share(_c.Index))),
                    UiFactLine.Say(EstateText.T("reallife_estate_listing_district", "district", EstateText.District(b.District), "share", EstateText.Share(EstateRules.DistrictValue(income)))),
                    UiFactLine.Say(EstateText.T(EstateRules.RunDown(cond) ? "reallife_estate_listing_run_down" : "reallife_estate_listing_kept", "condition", EstateText.Level(cond),
                        "share", EstateText.Share(EstateRules.ConditionValue(cond)), "amount", Money(EstateRules.RepairCost(b.Base, cond, _c.Index))), EstateRules.RunDown(cond) ? UiRole.WarnText : UiRole.None)
                };
                var letting = new List<UiFactLine>
                {
                    UiFactLine.Say(rent > 0
                        ? EstateText.T("reallife_estate_listing_rent", "use", EstateText.Use(use), "amount", Money(rent), "yield", EstateText.Yield((int)((long)rent * _c.DaysPerYear * 10000 / Math.Max(1, value))))
                        : EstateText.T("reallife_estate_listing_unfit"))
                };
                if (b.Units > 1 && rent > 0)
                {
                    letting.Add(UiFactLine.Say(EstateText.T("reallife_estate_listing_units", "units", b.Units.ToString(Inv), "amount", Money(Math.Max(1, rent / b.Units)))));
                }
                letting.Add(UiFactLine.Say(EstateText.T("reallife_estate_listing_how")));
                _m.Left = new[] { new UiFactCardModel { Title = EstateText.T("reallife_ibox_sec_price_made"), Heading = true, Lines = made.ToArray() } };
                _m.Right = new[]
                {
                    new UiFactCardModel
                    {
                        Title = EstateText.T("reallife_ibox_sec_letting"), Heading = true, Lines = letting.ToArray(),
                        Actions = new[] { _runs.Button(EstateText.T("reallife_estate_btn_offer"), UiButtonKind.Primary, UiIcon.Handshake, UiAction.Alt, () => Show(() => _d.Buy(place))) }
                    }
                };
                return true;
            }

            private void HotelForSalePage(HotelSlot slot)
            {
                int tier = (int)slot.Tier;
                int price = EstateGame.HotelPrice(slot, _s.Real, _c.DaysPerYear);
                string id = slot.Id;
                _m.Shape = EstateShape.Detail;
                _m.Back = EstateText.T("reallife_co_back");
                _m.DetailIcon = UiIcon.Building;
                _m.DetailTitle = EstateGame.HotelName(slot);
                _m.DetailSub = EstateText.T("reallife_estate_row_hotel") + ", " + EstateText.District(slot.Neighbourhood);
                var money = new List<UiFactLine>
                {
                    UiFactLine.Row(EstateText.T("reallife_estate_listing_price"), Money(price)),
                    UiFactLine.Say(EstateText.Fill("reallife_estate_hotel_rooms", "count0", HotelRules.Rooms(tier, 0).ToString(Inv), "room0", EstateGame.RoomName(tier, 0),
                        "count1", HotelRules.Rooms(tier, 1).ToString(Inv), "room1", EstateGame.RoomName(tier, 1))),
                    UiFactLine.Say(EstateText.T("reallife_estate_hotel_price_what", "years", (EstateCatalogue.HotelMultiple / 1000f).ToString("0.#", Inv), "share", EstateText.Share(EstateCatalogue.Occupancy),
                        "market", EstateText.Share(_s.Real)))
                };
                _m.Left = new[]
                {
                    new UiFactCardModel
                    {
                        Title = EstateText.T("reallife_ibox_sec_money"), Heading = true, Lines = money.ToArray(),
                        Actions = new[]
                        {
                            _runs.Button(EstateText.T("reallife_estate_btn_lease_hotel"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() => _d.TakeHotel(id, true))),
                            _runs.Button(EstateText.T("reallife_estate_btn_buy_hotel"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, () => Show(() => _d.TakeHotel(id, false)))
                        }
                    }
                };
                _m.Right = new[]
                {
                    new UiFactCardModel
                    {
                        Lines = new[]
                        {
                            UiFactLine.Say(EstateText.T("reallife_estate_hotel_lease_what", "deposit", Money(HotelRules.LeaseDeposit(tier, price, _c.DaysPerYear)), "amount", Money(HotelRules.LeaseDaily(tier, price, _c.DaysPerYear)))),
                            UiFactLine.Say(EstateText.T("reallife_estate_hotel_held_what"))
                        }
                    }
                };
            }

            // ------------------------------------------------------------------------------------------------- the agency

            public void Agency()
            {
                AgencyFile agency = _s.Agency;
                int id;
                Instruction picked = Parse(_v.Pick, "ins:", out id) ? agency.Find(id) : null;
                if (picked != null)
                {
                    InstructionPage(agency, picked);
                    return;
                }
                _v.Pick = null;
                var rows = new List<UiDataRow>(8);
                var keys = new List<string>(8);
                for (int i = 0; i < agency.Open.Count; i++)
                {
                    Instruction n = agency.Open[i];
                    int pays = AgencyRules.Commission(n.Sale, n.Stage == (int)InstructionStage.Completing ? n.Price : n.Value);
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { EstateText.T(n.Sale ? "reallife_estate_row_sale" : "reallife_estate_row_letting", "district", EstateText.District(n.District)), Money(n.Value), Money(pays),
                            ((n.Hours - n.Done) / 10f).ToString("0.#", Inv), EstateText.Stage(n.Stage), EstateText.T("reallife_estate_row_until", "day", n.Day.ToString(Inv)) },
                        Values = new[] { (int)(n.Sale ? UiIcon.Tag : UiIcon.Key), 0, 0, 0, 0, 0 },
                        Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, n.Stage == (int)InstructionStage.Offered ? UiRole.Warn : n.Stage == (int)InstructionStage.Completing ? UiRole.Ok : UiRole.Notice, UiRole.None },
                        Subs = new[] { EstateText.T(n.Sale ? "reallife_estate_filter_sales" : "reallife_estate_filter_lettings"), null, null, null, null, null }
                    });
                    keys.Add("ins:" + n.Id);
                }
                var cols = new[]
                {
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_instruction"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_value"), Kind = UiCellKind.Money, Width = 128f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_commission"), Kind = UiCellKind.Money, Width = 128f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_hours"), Kind = UiCellKind.Figure, Width = 80f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_state"), Kind = UiCellKind.Chip, Width = 184f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_day"), Kind = UiCellKind.Date, Width = 104f }
                };
                TableShape(UiIcon.Key, EstateText.T("reallife_estate_tab_agency"), agency.Running ? EstateGame.NameOf(agency.Office) : EstateText.T("reallife_estate_row_no_agency"), null, null, cols, rows, keys, AgencySide(agency),
                    EstateText.T(agency.Running ? "reallife_estate_row_no_instructions" : "reallife_estate_row_no_agency"), EstateText.T("reallife_estate_agency_open_what"));
            }

            private static bool Parse(string pick, string prefix, out int id)
            {
                id = 0;
                return pick != null && pick.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(pick.Substring(prefix.Length), out id);
            }

            private UiFactCardModel AgencySide(AgencyFile agency)
            {
                var lines = new List<UiFactLine>(8);
                var actions = new List<UiActionModel>(1);
                if (agency.Running)
                {
                    lines.Add(UiFactLine.Row(EstateText.T("reallife_estate_agency_office"), EstateGame.NameOf(agency.Office)));
                    lines.Add(UiFactLine.Say(EstateText.T(agency.Negotiators > 0 ? "reallife_estate_agency_staff" : "reallife_estate_agency_no_staff", "count", agency.Negotiators.ToString(Inv),
                        "hours", (agency.Capacity / 10f).ToString("0.#", Inv)), agency.Negotiators > 0 ? UiRole.None : UiRole.DangerText));
                    lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_agency_totals", "deals", agency.Deals.ToString(Inv), "lost", agency.Lost.ToString(Inv), "amount", Money(agency.Commission))));
                    lines.Add(UiFactLine.Section(EstateText.T("reallife_estate_agency_reputation")));
                    for (int i = 0; i < agency.Districts.Count && i < 6; i++)
                    {
                        DistrictRep d = agency.Districts[i];
                        if (d.SqmPrice > 0)
                        {
                            lines.Add(UiFactLine.Say(EstateText.District(d.Name) + ": " + EstateText.Level(d.Rep) + ", " + EstateText.T("reallife_estate_agency_chance", "chance", EstateText.Share(AgencyRules.InstructionChance(d.Rep, _c.Cycle)))));
                        }
                    }
                    actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_close_agency"), UiButtonKind.Destructive, UiIcon.None, UiAction.Count, () => Show(() => _d.CloseAgency())));
                }
                else
                {
                    var offices = new List<BuildingRegistration>(8);
                    EstateGame.Offices(offices);
                    if (offices.Count == 0)
                    {
                        lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_agency_no_office"), UiRole.WarnText));
                    }
                    else
                    {
                        int at = Math.Max(0, Math.Min(offices.Count - 1, _v.Office));
                        string address = offices[at].Address.ToFormattedString();
                        string office = EstateGame.Place(offices[at].StreetName, offices[at].StreetNumber);
                        lines.Add(_runs.Step(EstateText.T("reallife_estate_agency_office"), address, null, at > 0, at < offices.Count - 1, () => Show(() => _v.Office = at - 1), () => Show(() => _v.Office = at + 1)));
                        lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_agency_open_what")));
                        actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_open_agency"), UiButtonKind.Primary, UiIcon.Key, UiAction.Alt, () => Show(() => _d.OpenAgency(office))));
                    }
                }
                lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_agency_what")));
                return new UiFactCardModel { Title = EstateText.T("reallife_estate_tab_agency"), Heading = true, Lines = lines.ToArray(), Actions = actions.ToArray() };
            }

            private void InstructionPage(AgencyFile agency, Instruction n)
            {
                int id = n.Id;
                DistrictRep d = agency.District(n.District);
                int rep = d != null ? d.Rep : 0;
                _m.Shape = EstateShape.Detail;
                _m.Back = EstateText.T("reallife_co_back");
                _m.DetailIcon = n.Sale ? UiIcon.Tag : UiIcon.Key;
                _m.DetailTitle = EstateText.T(n.Sale ? "reallife_estate_row_sale" : "reallife_estate_row_letting", "district", EstateText.District(n.District));
                _m.DetailChip = EstateText.Stage(n.Stage);
                _m.DetailChipTone = n.Stage == (int)InstructionStage.Offered ? UiRole.Warn : n.Stage == (int)InstructionStage.Completing ? UiRole.Ok : UiRole.Notice;
                var what = new List<UiFactLine>();
                switch ((InstructionStage)n.Stage)
                {
                    case InstructionStage.Offered:
                        what.Add(UiFactLine.Say(EstateText.T("reallife_estate_ins_offered", "day", n.Day.ToString(Inv), "amount", Money(AgencyRules.TakeCost(n)), "days", EstateCatalogue.InstructionLapseDays.ToString(Inv))));
                        break;
                    case InstructionStage.Marketing:
                        what.Add(UiFactLine.Say(EstateText.T("reallife_estate_ins_marketing", "day", n.Day.ToString(Inv))));
                        break;
                    default:
                        what.Add(UiFactLine.Say(EstateText.T("reallife_estate_ins_completing", "amount", Money(n.Price), "day", n.Day.ToString(Inv))));
                        break;
                }
                what.Add(UiFactLine.Say(EstateText.T("reallife_estate_ins_what", "sqm", n.Sqm.ToString(Inv), "amount", Money(n.Value), "ask", EstateText.Share(n.Ask))));
                var work = new List<UiFactLine>
                {
                    UiFactLine.Bar(EstateText.T("reallife_ibox_done"), n.Hours > 0 ? n.Done * 100 / n.Hours : 100),
                    UiFactLine.Say(EstateText.T("reallife_estate_ins_hours", "hours", (n.Hours / 10).ToString(Inv), "done", (n.Done / 10f).ToString("0.#", Inv), "capacity", (agency.Capacity / 10f).ToString("0.#", Inv))),
                    UiFactLine.Say(EstateText.T("reallife_estate_ins_chance", "chance", EstateText.Share(AgencyRules.DealChance(n.Ask, rep, _c.Cycle)),
                        "fall", EstateText.Share(EstateCatalogue.FallThrough[EstateCatalogue.Clamp(_c.Cycle, 0, 3)]), "reputation", EstateText.Level(rep)))
                };
                int commission = AgencyRules.Commission(n.Sale, n.Stage == (int)InstructionStage.Completing ? n.Price : n.Value);
                work.Add(UiFactLine.Say(EstateText.T(n.Sale ? "reallife_estate_ins_pays_sale" : "reallife_estate_ins_pays_let", "amount", Money(commission),
                    "share", EstateText.Share(n.Sale ? EstateCatalogue.CommissionSale : EstateCatalogue.CommissionLet))));
                var actions = new List<UiActionModel>(2);
                if ((InstructionStage)n.Stage == InstructionStage.Offered)
                {
                    actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_decline"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() =>
                    {
                        _d.DeclineInstruction(id);
                        _v.Pick = null;
                    })));
                    actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_take"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, () => Show(() => _d.TakeInstruction(id))));
                }
                _m.Left = new[] { new UiFactCardModel { Title = EstateText.T("reallife_ibox_sec_terms"), Heading = true, Lines = what.ToArray() } };
                _m.Right = new[] { new UiFactCardModel { Title = EstateText.T("reallife_ibox_sec_work"), Heading = true, Lines = work.ToArray(), Actions = actions.ToArray() } };
            }
        }
    }
}
