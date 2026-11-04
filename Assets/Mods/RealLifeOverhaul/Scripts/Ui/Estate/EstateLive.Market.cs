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
                var locate = new List<int>(64);
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
                    string yield = rent > 0 && f.buildingPrice > 0f ? EstateText.Yield((int)((long)rent * _c.DaysPerYear * 10000 / (long)f.buildingPrice)) : string.Empty;
                    string where = b.Place;
                    locate.Add(_runs.Do(() => EstateGame.Locate(where)));
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { f.address.ToFormattedString(), Money((int)Math.Min(int.MaxValue / 2, f.buildingPrice)),
                            (cond / 10).ToString(Inv), rent > 0 ? Month(rent) : string.Empty, yield, string.Empty },
                        Values = new[] { (int)UseIcon(use), 0, cond / 10, 0, 0, (int)UiIcon.Locate }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                        Subs = new[] { EstateText.Use(use) + ", " + EstateText.District(b.District), null, null, null, null, null }
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
                    locate.Add(-1);   // a hotel for sale has no building of the game's to move the camera to
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { EstateGame.HotelName(hs), Money(price), string.Empty, string.Empty, EstateText.T("reallife_estate_row_going_concern"), string.Empty },
                        Values = new[] { (int)UiIcon.Building, 0, -1, 0, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                        Subs = new[] { EstateText.T("reallife_estate_row_hotel") + ", " + EstateText.District(hs.Neighbourhood), null, null, null, null, null }
                    });
                    keys.Add("htl:" + hs.Id);
                }
                var cols = new[]
                {
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_building"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_price"), Kind = UiCellKind.Money, Width = 128f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_condition"), Kind = UiCellKind.Meter, Width = 136f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_rent_month"), Kind = UiCellKind.Money, Width = 112f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_yield"), Kind = UiCellKind.Figure, Width = 88f },
                    new UiDataColumn { Title = string.Empty, Kind = UiCellKind.Action, Width = 56f }
                };
                // The market's own figures stand over the table, not in a box of sentences beside it.
                TableShape(UiIcon.Tag, EstateText.T("reallife_estate_tab_market"), EstateText.T("reallife_ui_estate_market_sub", "count", rows.Count.ToString(Inv)),
                    new[] { EstateText.T("reallife_ui_estate_fig_market_level"), EstateText.T("reallife_ui_estate_fig_prices_since"), EstateText.T("reallife_ui_estate_fig_cycle") },
                    new[] { EstateText.Share(_s.Real), EstateText.Share(_c.Index), EstateText.Cycle(_c.Cycle) }, cols, rows, keys, null,
                    EstateText.T("reallife_estate_row_no_market"), EstateText.T("reallife_ui_estate_empty_market_body"));
                _m.RowRuns = locate.ToArray();
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
                _m.Card = true;
                _m.FootBack = EstateText.T("reallife_ui_estate_not_now");
                _m.Back = EstateText.T("reallife_co_back");
                _m.DetailIcon = UseIcon(use);
                _m.DetailTitle = EstateGame.NameOf(place);
                _m.DetailSub = EstateText.T("reallife_ibox_type_in", "type", EstateText.Use(use), "district", EstateText.District(b.District));
                _m.DetailLocate = EstateText.T("reallife_ui_estate_locate");
                _m.DetailLocateRun = _runs.Do(() => EstateGame.Locate(place));
                // What makes the price, as figures: the price, the game's value and the three things that move it.
                _m.Strip = new[]
                {
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_asking_price"), Value = Money(value) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_game_value"), Value = Money(b.Base) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_market_level"), Value = EstateText.Share(_s.Real) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_prices_since"), Value = EstateText.Share(_c.Index) }
                };
                var letting = new List<UiFactLine>();
                if (rent > 0)
                {
                    letting.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_listing_a_day"), Money(rent)));
                    letting.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_listing_yield"), EstateText.Yield((int)((long)rent * _c.DaysPerYear * 10000 / Math.Max(1, value)))));
                    if (b.Units > 1)
                    {
                        letting.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_listing_units", "units", b.Units.ToString(Inv)), Money(Math.Max(1, rent / b.Units))));
                    }
                }
                else
                {
                    letting.Add(UiFactLine.Say(EstateText.T("reallife_estate_listing_unfit")));
                }
                _m.Left = new[]
                {
                    new UiFactCardModel
                    {
                        Title = EstateText.T("reallife_ui_estate_let_as", "use", EstateText.Use(use)), Heading = true, Lines = letting.ToArray(),
                        Actions = new[] { _runs.Button(EstateText.T("reallife_estate_btn_offer"), UiButtonKind.Primary, UiIcon.Handshake, UiAction.Alt, () => Show(() => _d.Buy(place))) }
                    }
                };
                _m.ShareTitle = EstateText.T("reallife_ui_estate_sh_building");
                _m.ShareLabels = new[] { EstateText.T("reallife_ui_estate_m_condition"), EstateText.T("reallife_ui_estate_sh_district"), EstateText.T("reallife_ui_estate_sh_in_repair") };
                _m.ShareValues = new[] { cond / 10, Math.Min(100, EstateRules.DistrictValue(income) / 10), Math.Min(100, EstateRules.ConditionValue(cond) / 10) };
                // The other buildings for sale in the district, as short rows that open their own listing.
                GameInstance save = SaveGameManager.Current;
                var others = new List<EstateRow>(3);
                for (int i = 0; save != null && save.buildingsForSale != null && i < save.buildingsForSale.Count && others.Count < 3; i++)
                {
                    BuildingForSale f = save.buildingsForSale[i];
                    CityBuilding ob = f != null && f.address != null ? EstateGame.Find(EstateGame.Place(f.address.streetName, f.address.streetNumber)) : null;
                    if (ob == null || ob.Holding != null || ob.B == null || ob.Place == place || ob.District != b.District)
                    {
                        continue;
                    }
                    string otherPlace = ob.Place;
                    int ouse = EstateGame.UseOf(ob.B.BuildingType);
                    others.Add(new EstateRow
                    {
                        Icon = UseIcon(ouse), Title = EstateGame.NameOf(otherPlace), Sub = EstateText.Use(ouse), Amount = Money((int)Math.Min(int.MaxValue / 2, f.buildingPrice)),
                        Run = _runs.Do(() => Show(() => _v.Pick = "mkt:" + otherPlace))
                    });
                }
                _m.RelatedTitle = EstateText.T("reallife_ui_estate_also_in", "district", EstateText.District(b.District));
                _m.RelatedCount = others.Count > 0 ? others.Count.ToString(Inv) : null;
                _m.Related = others.ToArray();
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
                    UiFactLine.Say(EstateText.T("reallife_estate_hotel_price_what", "years",
                        (EstateCatalogue.HotelMultiple / 1000f).ToString("0.#", Inv), "share", EstateText.Share(EstateCatalogue.Occupancy),
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
                            UiFactLine.Say(EstateText.T("reallife_estate_hotel_lease_what", "deposit",
                                Money(HotelRules.LeaseDeposit(tier, price, _c.DaysPerYear)), "amount", Money(HotelRules.LeaseDaily(tier, price, _c.DaysPerYear)))),
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
                        Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, n.Stage == (int)InstructionStage.Offered ? UiRole.Warn
                            : n.Stage == (int)InstructionStage.Completing ? UiRole.Ok : UiRole.Notice, UiRole.None },
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
                if (!agency.Running)
                {
                    // Nothing is run yet: the section and the card that opens it, together, with no empty table under its headers.
                    _m.Shape = EstateShape.Empty;
                    _m.OffIcon = UiIcon.Key;
                    _m.OffTitle = EstateText.T("reallife_estate_row_no_agency");
                    _m.OffBody = EstateText.T("reallife_estate_agency_open_what");
                    _m.Side = AgencySide(agency);
                    _m.Side.Title = EstateText.T("reallife_estate_tab_agency");
                    _m.Side.Heading = true;
                    return;
                }
                TableShape(UiIcon.Key, EstateText.T("reallife_estate_tab_agency"), EstateGame.NameOf(agency.Office), null, null, cols, rows, keys, AgencySide(agency),
                    EstateText.T("reallife_estate_row_no_instructions"), EstateText.T("reallife_estate_agency_open_what"));
                int pending = 0;
                for (int i = 0; i < agency.Open.Count; i++)
                {
                    Instruction n = agency.Open[i];
                    pending += AgencyRules.Commission(n.Sale, n.Stage == (int)InstructionStage.Completing ? n.Price : n.Value);
                }
                _m.Strip = new[]
                {
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_instructions"), Value = agency.Open.Count.ToString(Inv) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_col_commission"), Value = Money(pending) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_negotiators"), Value = agency.Negotiators.ToString(Inv) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_hours_day"), Value = (agency.Capacity / 10f).ToString("0.#", Inv) }
                };
                var names = new List<string>(6);
                var reps = new List<int>(6);
                for (int i = 0; i < agency.Districts.Count && names.Count < 6; i++)
                {
                    if (agency.Districts[i].SqmPrice > 0)
                    {
                        names.Add(EstateText.District(agency.Districts[i].Name));
                        reps.Add(EstateCatalogue.Clamp(agency.Districts[i].Rep, 0, 1000) / 10);
                    }
                }
                _m.ShareTitle = EstateText.T("reallife_estate_agency_reputation");
                _m.ShareLabels = names.ToArray();
                _m.ShareValues = reps.ToArray();
                var chances = new List<EstateRow>(6);
                for (int i = 0; i < agency.Districts.Count && chances.Count < 6; i++)
                {
                    DistrictRep d = agency.Districts[i];
                    if (d.SqmPrice > 0)
                    {
                        chances.Add(new EstateRow
                        {
                            Icon = UiIcon.Locate, Title = EstateText.District(d.Name), Sub = EstateText.T("reallife_ui_estate_reputation_n", "value", EstateText.Share(EstateCatalogue.Clamp(d.Rep, 0, 1000))),
                            Amount = EstateText.T("reallife_ui_estate_chance_a_day", "chance", EstateText.Share(AgencyRules.InstructionChance(d.Rep, _c.Cycle))), Run = -1
                        });
                    }
                }
                _m.Related2Title = EstateText.T("reallife_ui_estate_new_instruction_chance");
                _m.Related2Count = chances.Count.ToString(Inv);
                _m.Related2 = chances.ToArray();
                // The first instruction offered to you stands over the table with its answers: taking it or turning it down is done where it is seen.
                for (int i = 0; i < agency.Open.Count && _m.Offer == null; i++)
                {
                    Instruction n = agency.Open[i];
                    if (n.Stage != (int)InstructionStage.Offered)
                    {
                        continue;
                    }
                    int oid = n.Id;
                    DistrictRep rep = agency.District(n.District);
                    _m.Offer = new EstateOffer
                    {
                        Icon = n.Sale ? UiIcon.Tag : UiIcon.Key, Title = EstateText.T(n.Sale ? "reallife_estate_row_sale" : "reallife_estate_row_letting", "district", EstateText.District(n.District)),
                        Chip = EstateText.Stage(n.Stage), Sub = EstateText.T("reallife_ui_estate_offer_sub", "sqm", n.Sqm.ToString(Inv), "ask", EstateText.Share(n.Ask)),
                        FigLabels = new[] { EstateText.T("reallife_ui_estate_offer_fee"), EstateText.T("reallife_ui_estate_offer_lapses"), EstateText.T("reallife_ui_estate_offer_chance"),
                            EstateText.T("reallife_ui_estate_col_commission") },
                        FigValues = new[] { Money(AgencyRules.TakeCost(n)), EstateText.T("reallife_ui_estate_n_days", "days", Math.Max(0, n.Day - _day).ToString(Inv)),
                            EstateText.Share(AgencyRules.DealChance(n.Ask, rep != null ? rep.Rep : 0, _c.Cycle)), Money(AgencyRules.Commission(n.Sale, n.Value)) },
                        Actions = new[]
                        {
                            _runs.Button(EstateText.T("reallife_estate_btn_decline"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() => _d.DeclineInstruction(oid))),
                            _runs.Button(EstateText.T("reallife_estate_btn_take"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, () => Show(() => _d.TakeInstruction(oid)))
                        }
                    };
                }
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
                    if (agency.Negotiators <= 0)
                    {
                        lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_agency_no_staff", "count", "0", "hours", "0"), UiRole.DangerText));
                    }
                    lines.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_fig_deals"), agency.Deals.ToString(Inv)));
                    lines.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_fig_lost"), agency.Lost.ToString(Inv)));
                    lines.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_fig_earned"), Money(agency.Commission)));
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
                        lines.Add(_runs.Step(EstateText.T("reallife_estate_agency_office"), address, null, at > 0, at < offices.Count - 1,
                            () => Show(() => _v.Office = at - 1), () => Show(() => _v.Office = at + 1)));
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
                        what.Add(UiFactLine.Say(EstateText.T("reallife_estate_ins_offered", "day", n.Day.ToString(Inv), "amount",
                            Money(AgencyRules.TakeCost(n)), "days", EstateCatalogue.InstructionLapseDays.ToString(Inv))));
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
                    UiFactLine.Say(EstateText.T("reallife_estate_ins_hours", "hours", (n.Hours / 10).ToString(Inv), "done",
                        (n.Done / 10f).ToString("0.#", Inv), "capacity", (agency.Capacity / 10f).ToString("0.#", Inv))),
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
