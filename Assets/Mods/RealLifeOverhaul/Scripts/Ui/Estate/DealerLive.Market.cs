using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class DealerLive
    {
        private sealed partial class Page
        {
            private string AuctionState(DealerListing l)
            {
                if (l.Gone)
                {
                    return l.Leader == 0 ? DealerText.T("reallife_dealer_state_won") : DealerText.T("reallife_dealer_state_sold_on");
                }
                if (l.Leader == 0)
                {
                    return DealerText.T("reallife_dealer_state_leading");
                }
                return l.Leader > 0 ? DealerText.T("reallife_dealer_state_rival", "rival",
                    DealerCatalogue.RivalNames[(l.Leader - 1) % DealerCatalogue.RivalNames.Length]) : DealerText.T("reallife_dealer_state_no_bid");
            }

            private int Choice(string what, int fallback) => _v.ChoiceFor == what && _v.Choice >= 0 ? _v.Choice : fallback;

            private void SetChoice(string what, int step)
            {
                _v.ChoiceFor = what;
                _v.Choice = step;
            }

            // ------------------------------------------------------------------------------------------------- the market

            public void Market()
            {
                int id;
                DealerListing picked = Parse(_v.Pick, "list:", out id) ? _s.Listing(id) : null;
                if (picked != null)
                {
                    switch (picked.Kind)
                    {
                        case ListingKind.Used: UsedPage(picked); return;
                        case ListingKind.Auction: AuctionPage(picked); return;
                        default: NewPage(picked); return;
                    }
                }
                _v.Pick = null;
                var rows = new List<UiDataRow>(16);
                var keys = new List<string>(16);
                int news = 0, used = 0, lots = 0;
                for (int i = 0; i < _s.Market.Count; i++)
                {
                    DealerListing l = _s.Market[i];
                    if (l.Gone && l.Kind != ListingKind.Auction)
                    {
                        continue;
                    }
                    int shown = l.Inspected ? l.Condition : l.Shown;
                    bool auction = l.Kind == ListingKind.Auction;
                    int left = Mathf.Max(0, l.Ends - _now);
                    string state = auction ? (l.Gone ? DealerText.T("reallife_dealer_ended") : DealerText.Hours(left)) : DealerText.T("reallife_dealer_today");
                    string chip = auction ? AuctionState(l) : l.Kind == ListingKind.Used
                        ? (l.Inspected ? DealerText.T("reallife_dealer_state_inspected") : DealerText.T("reallife_dealer_state_not_inspected")) : DealerText.T("reallife_dealer_state_new");
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { DealerGame.CarName(l.Type), DealerText.Kind(l.Kind), DealerText.Km(l.Km), Mathf.Clamp(shown, 0, 100).ToString(Inv),
                            Money(l.Kind == ListingKind.Used && l.Countered > 0 ? l.Countered : l.Asking), state, chip },
                        Values = new[] { (int)(auction ? UiIcon.Tag : l.Kind == ListingKind.Used ? UiIcon.Car : UiIcon.Store), 0, 0, Mathf.Clamp(shown, 0, 100), 0, 0, 0 },
                        Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, auction
                            ? (l.Leader == 0 ? UiRole.Ok : l.Leader > 0 ? UiRole.Warn : UiRole.Notice) : UiRole.Notice },
                        Subs = new[] { DealerGame.ColourName(l.Colour), null, null, null, null, null, null },
                        Pictures = new[] { DealerLive.PictureOf(l.Type, out float _), null, null, null, null, null, null }
                    });
                    keys.Add("list:" + l.Id);
                    news += l.Kind == ListingKind.Wholesale ? 1 : 0;
                    used += l.Kind == ListingKind.Used ? 1 : 0;
                    lots += auction ? 1 : 0;
                }
                var cols = new[]
                {
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_car"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_kind"), Kind = UiCellKind.Text, Width = 104f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_km"), Kind = UiCellKind.Figure, Width = 96f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_condition"), Kind = UiCellKind.Meter, Width = 136f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_price"), Kind = UiCellKind.Money, Width = 112f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_time"), Kind = UiCellKind.Text, Width = 96f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_state"), Kind = UiCellKind.Chip, Width = 216f }
                };
                string sub = _s.NoBidsUntil >= _day ? DealerText.T("reallife_dealer_market_no_bids", "day", FinanceText.Day(_s.NoBidsUntil + 1))
                    : DealerText.Fill("reallife_ui_dealer_market_sub", "new", news.ToString(Inv), "used", used.ToString(Inv), "lots", lots.ToString(Inv));
                TableShape(UiIcon.Tag, DealerText.T("reallife_dealer_market_title"), sub, cols, rows, keys, null,
                    DealerText.T("reallife_ui_dealer_empty_market"), DealerText.T("reallife_dealer_market_about"));
                _m.FigLabels = new[] { DealerText.T("reallife_dealer_cash"), DealerText.T("reallife_ui_dealer_fig_new"), DealerText.T("reallife_ui_dealer_fig_used"), DealerText.T("reallife_ui_dealer_fig_lots") };
                _m.FigValues = new[] { Money(DealerGame.Cash), news.ToString(Inv), used.ToString(Inv), lots.ToString(Inv) };
            }

            private static bool Parse(string pick, string prefix, out int id)
            {
                id = 0;
                return pick != null && pick.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(pick.Substring(prefix.Length), out id);
            }

            private void NewPage(DealerListing l)
            {
                int worth = _d.Worth(l);
                DetailHead(UiIcon.Store, DealerGame.CarName(l.Type), DealerGame.ColourName(l.Colour), DealerText.T("reallife_dealer_state_new"), UiRole.Notice);
                var lines = new List<UiFactLine> { UiFactLine.Row(DealerText.T("reallife_ui_dealer_l_km"), DealerText.Km(l.Km)) };
                string yard = YardStep(lines);
                DealerYard to = yard != null ? _s.Yard(yard) : null;
                int price = to != null ? DealerRules.WholesalePrice(l.Asking, to.Rep) : l.Asking;
                if (price < l.Asking)
                {
                    lines.Add(UiFactLine.Say(DealerText.T("reallife_dealer_new_perk", "price", Money(l.Asking)), UiRole.OkText));
                }
                if (yard == null)
                {
                    lines.Add(UiFactLine.Say(DealerText.T(_s.Yards.Count == 0 ? "reallife_dealer_refuse_off" : "reallife_dealer_no_room"), UiRole.WarnText));
                }
                var actions = new List<UiActionModel>(1);
                if (yard != null)
                {
                    int lid = l.Id;
                    actions.Add(_runs.Button(DealerText.T("reallife_dealer_button_buy"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, () => Show(() => Confirm("reallife_dealer_q_buy_title",
                        DealerText.T("reallife_dealer_q_buy", "car", DealerGame.CarName(l.Type), "price", Money(price)), () =>
                        {
                            if (_d.Buy(lid, yard, price))
                            {
                                _v.Pick = null;
                            }
                        }, false))));
                }
                _m.Left = new[] { new UiFactCardModel { Title = DealerText.T("reallife_ibox_sec_offer"), Heading = true, Lines = lines.ToArray(), Actions = actions.ToArray() } };
                _m.Card = true;
                _m.Strip = new[]
                {
                    new UiFigureModel { Label = DealerText.T("reallife_ui_dealer_ch_price"), Value = Money(price) },
                    new UiFigureModel { Label = DealerText.T("reallife_ui_dealer_ch_worth"), Value = Money(worth) },
                    new UiFigureModel { Label = DealerText.T("reallife_ui_dealer_ch_asking"), Value = Money(DealerRules.Asking(worth)) }
                };
            }

            private void UsedPage(DealerListing l)
            {
                int lid = l.Id;
                DetailHead(UiIcon.Car, DealerGame.CarName(l.Type), DealerGame.ColourName(l.Colour),
                    DealerText.T(l.Inspected ? "reallife_ibox_chip_inspected" : "reallife_ibox_chip_not_inspected"), l.Inspected ? UiRole.Ok : UiRole.Warn);
                _m.Card = true;
                var car = new List<UiFactLine>(4);
                car.Add(UiFactLine.Row(DealerText.T("reallife_ui_dealer_l_km"), DealerText.Km(l.Km)));
                if (l.Inspected)
                {
                    car.Add(UiFactLine.Row(DealerText.T("reallife_dealer_used_worth"), Money(_d.Worth(l))));
                    car.Add(UiFactLine.Say(DealerText.T("reallife_ui_dealer_faults_are", "faults", DealerText.Faults(l.Faults))));
                }
                else
                {
                    car.Add(UiFactLine.Say(DealerText.T("reallife_ui_dealer_faults_unknown")));
                }
                _m.ShareTitle = DealerText.T(l.Inspected ? "reallife_ui_dealer_sh_inspected" : "reallife_ui_dealer_sh_said");
                _m.ShareLabels = new[] { DealerText.T("reallife_ui_dealer_l_condition") };
                _m.ShareValues = new[] { Mathf.Clamp(l.Inspected ? l.Condition : l.Shown, 0, 100) };
                _m.Right = new[] { new UiFactCardModel { Title = DealerText.T("reallife_ibox_sec_car"), Heading = true, Lines = car.ToArray() } };
                var lines = new List<UiFactLine>(6);
                int placesFree = 0;
                for (int i = 0; i < _s.Yards.Count; i++)
                {
                    placesFree += _s.Room(_s.Yards[i]);
                }
                _m.Strip = new[]
                {
                    new UiFigureModel { Label = DealerText.T("reallife_ibox_asking"), Value = Money(l.Asking) },
                    new UiFigureModel { Label = DealerText.T("reallife_ui_dealer_last_counter"), Value = l.Countered > 0 ? Money(l.Countered) : DealerText.T("reallife_dealer_none") },
                    new UiFigureModel { Label = DealerText.T("reallife_ui_dealer_offers_left"), Value = (DealerCatalogue.HagglesMost - l.Haggles).ToString(Inv) },
                    new UiFigureModel { Label = DealerText.T("reallife_ui_dealer_free_places"), Value = placesFree.ToString(Inv) }
                };
                string yard = YardStep(lines);
                if (yard == null)
                {
                    lines.Add(UiFactLine.Say(DealerText.T(_s.Yards.Count == 0 ? "reallife_dealer_refuse_off" : "reallife_dealer_no_room"), UiRole.WarnText));
                }
                string what = "used:" + l.Id;
                int step = Choice(what, DealerRules.HaggleSteps - 3);
                int offer = DealerRules.HaggleAt(l.Asking, step);
                lines.Add(_runs.Step(DealerText.T("reallife_ui_dealer_your_offer"), Money(offer), null, step > 0, step < DealerRules.HaggleSteps - 1,
                    () => Show(() => SetChoice(what, step - 1)), () => Show(() => SetChoice(what, step + 1))));
                var actions = new List<UiActionModel>(3);
                if (!l.Inspected)
                {
                    int fee = _d.InspectionFee(l);
                    actions.Add(_runs.Button(DealerText.T("reallife_dealer_button_inspect"), UiButtonKind.Secondary, UiIcon.Eye, UiAction.Count,
                        () => Show(() => Confirm("reallife_dealer_q_inspect_title",
                        DealerText.T("reallife_dealer_q_inspect", "car", DealerGame.CarName(l.Type), "fee", Money(fee)), () => _d.Inspect(lid, yard), false))));
                }
                if (yard != null)
                {
                    int price = l.Countered > 0 ? l.Countered : l.Asking;
                    actions.Add(_runs.Button(DealerText.T("reallife_dealer_button_buy"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() => Confirm("reallife_dealer_q_buy_title",
                        DealerText.T("reallife_dealer_q_buy", "car", DealerGame.CarName(l.Type), "price", Money(price)), () =>
                        {
                            if (_d.Buy(lid, yard, price))
                            {
                                _v.Pick = null;
                            }
                        }, false))));
                    actions.Add(_runs.Button(DealerText.T("reallife_dealer_button_offer"), UiButtonKind.Primary, UiIcon.Handshake, UiAction.Alt, () => Show(() =>
                    {
                        int counter;
                        HaggleAnswer a = _d.Haggle(lid, yard, offer, out counter);
                        if (a == HaggleAnswer.Taken)
                        {
                            _v.Pick = null;
                        }
                        else if (a == HaggleAnswer.WalkedAway)
                        {
                            UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, "reallife_dealer_note_owner_gone", null, 7f);
                            _v.Pick = null;
                        }
                    })));
                }
                _m.Left = new[] { new UiFactCardModel { Title = DealerText.T("reallife_ibox_sec_offer"), Heading = true, Lines = lines.ToArray(), Actions = actions.ToArray() } };
                // The other used cars on the market, as short rows that open their own page.
                var like = new List<EstateRow>(3);
                for (int i = 0; i < _s.Market.Count && like.Count < 3; i++)
                {
                    DealerListing o = _s.Market[i];
                    if (o.Id == l.Id || o.Gone || o.Kind != ListingKind.Used)
                    {
                        continue;
                    }
                    int oid = o.Id;
                    like.Add(new EstateRow
                    {
                        Icon = UiIcon.Car, Title = DealerGame.CarName(o.Type), Sub = DealerGame.ColourName(o.Colour) + ", " + DealerText.Km(o.Km), Amount = Money(o.Asking),
                        Chip = DealerText.T(o.Inspected ? "reallife_dealer_state_inspected" : "reallife_dealer_state_not_inspected"), ChipTone = o.Inspected ? UiRole.Ok : UiRole.Warn,
                        Run = _runs.Do(() => Show(() => _v.Pick = "list:" + oid))
                    });
                }
                _m.Related2Title = DealerText.T("reallife_ui_dealer_like_it");
                _m.Related2Count = like.Count > 0 ? like.Count.ToString(Inv) : null;
                _m.Related2 = like.ToArray();
            }

            // ------------------------------------------------------------------------------------------------- the auction

            // A lot as a card over the world: the car, what it is worth, what winning it would leave, the bidders and the bid to place.
            private void AuctionPage(DealerListing l)
            {
                int worth = _d.Worth(l), lid = l.Id;
                int next = DealerRules.NextBid(l, worth), inc = DealerRules.Increment(worth), most = DealerRules.BidMost(worth);
                int bid = _v.BidFor == lid && _v.Bid >= next ? Math.Min(_v.Bid, most) : next;
                int fee = DealerRules.BuyersFee(bid, Prices.At(DealerCatalogue.BuyersFeeLeast));
                int free = 0;
                for (int i = 0; i < _s.Yards.Count; i++)
                {
                    free += _s.Room(_s.Yards[i]);
                }
                float aspect;
                string picture = DealerLive.PictureOf(l.Type, out aspect);
                int left = Mathf.Max(0, l.Ends - _now);
                var a = new EstateAuction
                {
                    Kicker = DealerText.T("reallife_ui_dealer_auction_kicker"), Title = DealerGame.CarName(l.Type), Picture = picture, Aspect = aspect, Back = DealerText.T("reallife_ui_dealer_back"),
                    Icon = DealerGame.CarName(l.Type).ToLowerInvariant().Contains("truck") || DealerGame.CarName(l.Type).ToLowerInvariant().Contains("van") || DealerGame.CarName(l.Type).ToLowerInvariant().Contains("pickup") ? UiIcon.Truck : UiIcon.Car
                };
                var facts = new List<UiFactLine>(10)
                {
                    UiFactLine.Row(DealerText.T("reallife_ui_dealer_l_worth"), Money(worth)),
                    UiFactLine.Bar(DealerText.T("reallife_ui_dealer_l_condition"), Mathf.Clamp(l.Inspected ? l.Condition : l.Shown, 0, 100)),
                    UiFactLine.Row(DealerText.T("reallife_ui_dealer_l_km"), DealerText.Km(l.Km)),
                    UiFactLine.Row(DealerText.T("reallife_ui_dealer_free_bays"), free.ToString(Inv), free == 0 ? UiRole.DangerText : UiRole.None),
                    UiFactLine.Section(DealerText.T("reallife_ui_dealer_if_you_win")),
                    UiFactLine.Row(DealerText.T("reallife_ui_dealer_your_bid"), Money(bid)),
                    UiFactLine.Row(DealerText.T("reallife_ui_dealer_buyers_fee", "share", DealerText.Share(DealerCatalogue.BuyersFee)), Money(fee)),
                    UiFactLine.Row(DealerText.T("reallife_ui_dealer_you_pay"), Money(bid + fee)),
                    UiFactLine.Row(DealerText.T("reallife_ui_dealer_worth_lot"), Money(worth)),
                    UiFactLine.Row(DealerText.T("reallife_ui_dealer_left_for_you"), (worth - bid - fee >= 0 ? "+" : "-")
                        + Money(Math.Abs(worth - bid - fee)), worth - bid - fee >= 0 ? UiRole.OkText : UiRole.DangerText)
                };
                // What the car is is the left card; what it comes to if you win stands with the bidding at the right.
                int win = facts.FindIndex(f => f.Kind == UiFactKind.Heading);
                var ifWin = win >= 0 ? facts.GetRange(win, facts.Count - win) : new List<UiFactLine>();
                if (win >= 0)
                {
                    facts.RemoveRange(win, facts.Count - win);
                }
                a.Left = new UiFactCardModel { Lines = facts.ToArray() };
                var right = new List<UiFactLine>(24);
                right.Add(UiFactLine.Row(DealerText.T("reallife_ui_dealer_current_bid"), Money(l.Asking)));
                right.Add(UiFactLine.Row(DealerText.T("reallife_ui_dealer_held_by"), AuctionState(l)));
                right.Add(UiFactLine.Bar(DealerText.T("reallife_ui_dealer_time_left_label"),
                    Mathf.Clamp(left * 100 / Math.Max(1, DealerCatalogue.LotHours[DealerCatalogue.LotHours.Length - 1]), 0, 100), DealerText.Hours(left)));
                if (!l.Gone)
                {
                    right.Add(_runs.Step(DealerText.T("reallife_ui_dealer_your_bid"), Money(bid),
                        DealerText.T("reallife_ui_dealer_bid_hint", "step", Money(inc), "least", Money(next)), bid > next, bid < most,
                        () => Show(() =>
                        {
                            _v.BidFor = lid;
                            _v.Bid = Math.Max(next, bid - inc);
                        }), () => Show(() =>
                        {
                            _v.BidFor = lid;
                            _v.Bid = Math.Min(most, bid + inc);
                        })));
                }
                right.AddRange(ifWin);
                if (l.BidLogCount > 0)
                {
                    right.Add(UiFactLine.Section(DealerText.T("reallife_ui_dealer_bidders", "count", l.BidLogCount.ToString(Inv))));
                }
                else
                {
                    right.Add(UiFactLine.Say(DealerText.T("reallife_ui_dealer_no_bids")));
                }
                for (int i = 0; i < l.BidLogCount && i < DealerCatalogue.BidsKept; i++)
                {
                    BidEntry b = l.BidLog[i];
                    string who = b.Bidder == 0 ? DealerText.T("reallife_ui_dealer_you") : DealerCatalogue.RivalNames[(b.Bidder - 1) % DealerCatalogue.RivalNames.Length];
                    int ago = Math.Max(0, _now - b.Hour);
                    right.Add(UiFactLine.Who(who, ago == 0 ? DealerText.T("reallife_ui_dealer_this_hour") : ago == 1
                        ? DealerText.T("reallife_ui_dealer_hour_ago") : DealerText.T("reallife_ui_dealer_hours_ago", "hours", ago.ToString(Inv)),
                        Money(b.Amount), b.Bidder == 0 ? UiRole.AccentText : UiRole.None));
                }
                if (l.Bids > l.BidLogCount)
                {
                    right.Add(UiFactLine.Say(DealerText.T("reallife_ui_dealer_earlier_bids", "count", (l.Bids - l.BidLogCount).ToString(Inv))));
                }
                if (l.Gone)
                {
                    right.Add(UiFactLine.Say(DealerText.T("reallife_dealer_lot_over")));
                }
                // The other lots of the auction, under this car's facts.
                var other = new List<EstateRow>(3);
                for (int i = 0; i < _s.Market.Count && other.Count < 3; i++)
                {
                    DealerListing o = _s.Market[i];
                    if (o.Id == l.Id || o.Gone || o.Kind != ListingKind.Auction)
                    {
                        continue;
                    }
                    int oid = o.Id;
                    string glyph = DealerGame.CarName(o.Type).ToLowerInvariant();
                    other.Add(new EstateRow
                    {
                        Icon = glyph.Contains("truck") || glyph.Contains("pickup") || glyph.Contains("van") ? UiIcon.Truck : UiIcon.Car, Title = DealerGame.CarName(o.Type),
                        Sub = DealerText.T("reallife_ui_dealer_n_hours_left", "hours", Mathf.Max(0, o.Ends - _now).ToString(Inv)), Amount = Money(o.Asking),
                        Chip = AuctionState(o), ChipTone = o.Leader == 0 ? UiRole.Ok : o.Leader > 0 ? UiRole.Warn : UiRole.Notice,
                        Run = _runs.Do(() => Show(() => _v.Pick = "list:" + oid))
                    });
                }
                _m.RelatedTitle = DealerText.T("reallife_ui_dealer_other_lots");
                _m.RelatedCount = other.Count > 0 ? other.Count.ToString(Inv) : null;
                _m.Related = other.ToArray();
                var actions = new List<UiActionModel>(3);
                actions.Add(_runs.Button(DealerText.T("reallife_ui_dealer_walk_away"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() => _v.Pick = null)));
                if (!l.Inspected && !l.Gone)
                {
                    int inspection = _d.InspectionFee(l);
                    actions.Add(_runs.Button(DealerText.T("reallife_dealer_button_inspect"), UiButtonKind.Secondary, UiIcon.Eye, UiAction.Count,
                        () => Show(() => Confirm("reallife_dealer_q_inspect_title",
                        DealerText.T("reallife_dealer_q_inspect", "car", DealerGame.CarName(l.Type), "fee", Money(inspection)), () => _d.Inspect(lid, null), false))));
                }
                if (!l.Gone)
                {
                    actions.Add(_runs.Button(DealerText.T("reallife_ui_dealer_bid_button", "amount", Money(bid)), UiButtonKind.Primary, UiIcon.Check,
                        UiAction.Alt, () => Show(() => _d.Bid(lid, bid)), l.Leader == 0));
                }
                a.Right = new UiFactCardModel { Title = DealerText.T("reallife_ui_dealer_bidding_on", "car", DealerGame.CarName(l.Type)),
                    Heading = false, Sub = DealerText.T("reallife_ui_dealer_colour_line", "colour", DealerGame.ColourName(l.Colour)),
                    Lines = right.ToArray(), Actions = actions.ToArray() };
                _m.Shape = EstateShape.Auction;
                _m.Card = true;
                _m.Auction = a;
            }
        }
    }
}
