using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Buildings.Office.Headquarters;
using Extensions;
using Streets;
using TMPro;
using UI.Smartphone.Apps.BizMan;
using UI.Smartphone.Apps.BizMan.StartBusiness;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Each tab's page read from the game's own widgets after the game filled them: the presentation in whichever view the
    /// game set (the building, renting, starting a business, another's shop, a house in the Hamptons), insight, prices,
    /// deliveries, marketing, settings and real estate; and the handover for a page only the game's own view shows. Cost: a
    /// layout a change.
    /// </summary>
    public sealed partial class BizManScreen
    {
        private readonly List<UiActionModel> _actions = new List<UiActionModel>(8);
        private readonly List<Transform> _agencies = new List<Transform>(8), _contacts = new List<Transform>(8), _kindCards = new List<Transform>(16);
        private readonly List<Toggle> _campaigns = new List<Toggle>(16);

        // ------------------------------------------------------------------------------------------------- the presentation

        private void Presentation(BizManBusiness b)
        {
            BizManPresentation pres = PageOf<BizManPresentation>(b, BizManRules.Presentation);
            if (pres == null)
            {
                Handover(b);
                return;
            }
            StartBusinessUI start = Get<StartBusinessUI>(GameContract.ShowStart, pres);
            RentBuildingUI rent = Get<RentBuildingUI>(GameContract.ShowRent, pres);
            HamptonsPurchaseBoxUI villa = Get<HamptonsPurchaseBoxUI>(GameContract.ShowVilla, pres);
            if (On(start))
            {
                Start(b, start);
                return;
            }
            Building(b, pres);
            _actions.Clear();
            if (On(rent))
            {
                Rent(rent);
            }
            else if (On(villa))
            {
                Villa(pres, villa);
            }
            else if (On(pres.businessSideAiOwned) || On(pres.sendOvertakeOfferPanel))
            {
                Rival(pres);
            }
            else
            {
                _m.Part = BizManPart.Building;
                BuyOffer(pres, false);
                if (_m.Lead == null && OnlyClassic())
                {
                    _m.Lead = UiText.T("reallife_ui_bizman_classic_hint");   // a warehouse's, factory's or headquarters' own pages are the game's view's for now
                }
            }
            Offer(_actions, Calling(pres, "PreviewBuilding"), BizManPages.DoPreview, UiButtonKind.Secondary, UiIcon.Eye, UiAction.Option, "bizman_preview_button");
            Offer(_actions, Calling(pres, "BrowseInventory"), BizManPages.DoBrowse, UiButtonKind.Secondary, UiIcon.ShoppingBag, UiAction.Count, "bizman_browse_store_inventory");
            if (pres.showEmployeesButton != null && pres.showEmployeesButton.activeInHierarchy)
            {
                Offer(_actions, pres.showEmployeesButton.GetComponentInChildren<Button>(true), BizManPages.DoEmployees, UiButtonKind.Secondary, UiIcon.People, UiAction.Count,
                    "bizman_presentation_show_employees");
            }
            Offer(_actions, Calling(pres, "TerminateContract"), BizManPages.DoTerminate, UiButtonKind.Destructive, UiIcon.Trash, UiAction.Count, "bizman_terminate_contract_button");
            Offer(_actions, Calling(pres, "SetStartBusiness"), BizManPages.DoStart, UiButtonKind.Primary, UiIcon.Store, UiAction.Alt, "bizman_start_new_business_button");
            Offer(_actions, Calling(pres, "RentBuilding"), BizManPages.DoRent, UiButtonKind.Primary, UiIcon.Key, UiAction.Alt, "bizman_rent_building_button");
            _m.Actions = _actions.ToArray();
            Unrepeat(b);
        }

        // Each fact once: the market value is left out where the offer for the building says its valuation, and the building's
        // kind where the head already names it.
        private void Unrepeat(BizManBusiness b)
        {
            bool offer = (_m.Amount != null && _m.Amount.Field == BizManPages.FieldBuy) || (_m.Second != null && _m.Second.Field == BizManPages.FieldBuy);
            for (int i = 0; i < _m.Facts.Length; i++)
            {
                bool market = offer && _m.Facts[i].Label == UiText.T("bizman_market_value");
                bool kind = _m.Facts[i].Label == UiText.T("common_building_type") && _m.Facts[i].Value == _m.Head.Kind;
                if (market || kind)
                {
                    _m.Facts[i].Value = null;
                }
            }
        }

        // The building's photo and its facts, as the game's presentation lists them.
        private void Building(BizManBusiness b, BizManPresentation pres)
        {
            Image photo = Get<Image>(GameContract.ShowPhoto, pres);
            _m.Picture = photo != null ? Pic(photo.sprite) : null;
            _m.PictureAspect = photo != null && photo.sprite != null ? Aspect(photo.sprite) : 1.6f;
            Transform top = Get<Transform>(GameContract.ShowTop, pres), bottom = Get<Transform>(GameContract.ShowBottom, pres);
            _m.FactsTitle = UiText.T("reallife_ui_bizman_the_building");
            _m.Facts = new[]
            {
                Fact("common_building_type", b.building != null ? UiText.T(b.building.BuildingType) : null),
                Fact("common_neighborhood", WordsAt(top, "Neighborhood")),
                Fact("bizman_building_total_size", WordsAt(bottom, "TotalSize")),
                Fact("bizman_traffic_index", WordsAt(top, "TrafficIndex")),
                Fact("bizman_customers_capacity", Dash(WordsAt(top, "CustomerCapacity"))),
                Fact("bizman_market_value", Dash(WordsAt(bottom, "MarketValue"))),
            };
        }

        // True when every page of the place but its building, its settings and its real estate is one the game's own view shows.
        private bool OnlyClassic()
        {
            int classic = 0;
            for (int i = 0; i < _tabs.Count; i++)
            {
                string name = _tabs[i].name;
                if (name == BizManRules.Presentation || name == BizManRules.Settings || name == BizManRules.RealEstate)
                {
                    continue;
                }
                if (BizManRules.Drawn(name, Ops))
                {
                    return false;
                }
                classic++;
            }
            return classic > 0;
        }

        // A figure the game writes as "-" when it has none: left out, rather than a row that says nothing.
        private static string Dash(string words) => words == "-" ? null : words;

        // The offer for the building: who owns it and whether it is for sale, the amount typed in the game's field and its
        // Send, as the presentation's for-sale or not-for-sale box offers them.
        private void BuyOffer(BizManPresentation pres, bool second)
        {
            Transform forSale = Get<Transform>(GameContract.ShowForSale, pres), notForSale = Get<Transform>(GameContract.ShowNotForSale, pres);
            Transform box = On(forSale) ? forSale : On(notForSale) ? notForSale : null;
            Button send = Calling(pres, "SendBuyBuildingOffer");
            if (box == null || send == null)
            {
                return;
            }
            var a = new BizManAmount
            {
                Title = UiText.T("bizman_buy_building"), Line = Join(WordsAt(box, "RivalBuildingOwner"), On(forSale) ? WordsAt(box, "BuildingPriceLabel") : Words(pres.buildingSideInfoValuation)),
                Figure = pres.buyBuildingAmountInputField != null ? pres.buyBuildingAmountInputField.text : null, Field = BizManPages.FieldBuy, CanLess = true, CanMore = true,
                Disabled = pres.buyBuildingAmountInputField == null || !pres.buyBuildingAmountInputField.interactable,
                Send = Do(BizManPages.DoBuy, UiWidgets.Label(send) ?? UiText.T("bizman_send_purchase_offer"), second ? UiButtonKind.Secondary : UiButtonKind.Primary, UiIcon.Send,
                    second ? UiAction.Count : UiAction.Alt, !send.interactable)
            };
            if (second)
            {
                _m.Second = a;
            }
            else
            {
                _m.Amount = a;
            }
        }

        private static string Join(string a, string b) => string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) ? a : a + ". " + b;

        private void Rent(RentBuildingUI rent)
        {
            _m.Part = BizManPart.Rent;
            if (On(Get<GameObject>(GameContract.RentFree, rent)))
            {
                _m.MoreTitle = UiText.T("reallife_ui_bizman_lease");
                BizManFact total = Split(Words(Get<Component>(GameContract.RentTotal, rent)));
                total.Tone = UiRole.Text;
                _m.More = new[]
                {
                    Split(Words(Get<Component>(GameContract.RentDaily, rent))),
                    Fact("ba:transaction_deposit_label", TextOf(Get<TMP_Text>(GameContract.RentDeposit, rent))),
                    Fact("reallife_ui_bizman_appliances", TextOf(Get<TMP_Text>(GameContract.RentAppliances, rent))),
                    Fact("reallife_ui_bizman_first_day", TextOf(Get<TMP_Text>(GameContract.RentFirstDay, rent))),
                    total,
                };
            }
            else
            {
                _m.Lead = Words(rent);   // rented by the player: the game's own words for what can be done with it
            }
        }

        private void Villa(BizManPresentation pres, HamptonsPurchaseBoxUI villa)
        {
            _m.Part = BizManPart.Villa;
            Button send = Calling(villa, "SendBuyBuildingOffer") ?? Calling(pres, "SendBuyBuildingOffer");
            _m.Amount = new BizManAmount
            {
                Title = UiText.T("bizman_buy_building"), Line = Words(Get<Component>(GameContract.VillaPrice, villa)) ?? Words(Get<Component>(GameContract.VillaValuation, villa)),
                Figure = villa.offerInputField != null ? villa.offerInputField.text : null, Field = BizManPages.FieldBuy, CanLess = true, CanMore = true,
                Send = Do(BizManPages.DoBuy, send != null ? UiWidgets.Label(send) : UiText.T("bizman_send_purchase_offer"), UiButtonKind.Primary, UiIcon.Send, UiAction.Alt,
                    send == null || !send.interactable)
            };
        }

        // Another's shop: what it is and who owns it, its hours, the offer to take it over, and the offer for its building.
        private void Rival(BizManPresentation pres)
        {
            _m.Part = BizManPart.Rival;
            UiFirmHeadModel h = _m.Head;
            if (On(pres.businessSideAiOwned))
            {
                h.Name = TextOf(pres.businessSideInfoBusinessName) ?? h.Name;
                h.Kind = Words(pres.businessSideInfoBusinessType) ?? h.Kind;
                h.Picture = pres.businessSideInfoIcon != null ? Pic(pres.businessSideInfoIcon.sprite) : null;
                h.Aspect = Aspect(pres.businessSideInfoIcon != null ? pres.businessSideInfoIcon.sprite : null);
                _m.PartTitle = UiText.T("reallife_ui_bizman_the_business");
                _m.Lead = Words(pres.businessSideInfoCorporation) ?? Words(pres.businessSideInfoBusinessDescription);
                _m.MoreTitle = UiText.T("bizman_schedule_opening_hours");
                var hours = new List<BizManFact>(7);
                Transform days = pres.businessSideInfoOpeningHours;
                for (int i = 0; days != null && i < days.childCount; i++)
                {
                    Transform day = days.GetChild(i);
                    string value = WordsAt(day, "Value");
                    if (value != null)
                    {
                        hours.Add(new BizManFact { Label = UiText.T("common_" + day.name.ToLowerInvariant()), Value = value });
                    }
                }
                _m.More = hours.ToArray();
            }
            Button send = Calling(pres, "SendOvertakeOffer");
            if (On(pres.sendOvertakeOfferPanel) && send != null)
            {
                _m.Amount = new BizManAmount
                {
                    Title = UiText.T("reallife_ui_bizman_take_over"), Line = Words(pres.businessSideOfferValuation) ?? Words(pres.businessSideInfoValuation),
                    Figure = pres.offerAmountInputField != null ? pres.offerAmountInputField.text : null, Field = BizManPages.FieldOffer, CanLess = true, CanMore = true,
                    Send = Do(BizManPages.DoTakeover, UiWidgets.Label(send) ?? UiText.T("bizman_send_offer_button"), UiButtonKind.Primary, UiIcon.Send, UiAction.Alt, !send.interactable)
                };
            }
            BuyOffer(pres, _m.Amount != null);
        }

        // ------------------------------------------------------------------------------------------------- insight

        private void Insight(BizManBusiness b)
        {
            _m.Part = BizManPart.Insight;
            BizManInsight see = b.bizManInsight;
            Extensions.ChartController chart = Get<Extensions.ChartController>(GameContract.SeeChart, see);
            _m.PartTitle = UiText.T("bizman_insight_customers_over_time");
            _m.Periods = new[] { UiText.T("bizman_insight_customers_chart_yesterday"), UiText.T("bizman_insight_customers_chart_last_week") };
            var data = chart != null && chart.chart != null ? chart.chart.GetChartData() : null;
            var entries = data != null && data.DataSets.Count > 0 ? data.DataSets[0].Entries : null;
            List<string> labels = chart != null && chart.chart != null ? chart.chart.AxisConfig.HorizontalAxisConfig.ValueFormatterConfig.CustomValues : null;
            int n = entries != null ? entries.Count : 0;
            _m.Period = n == 24 ? 0 : 1;   // the game's yesterday is by the hour, its week by the day
            var points = new string[n];
            var values = new float[n];
            for (int i = 0; i < n; i++)
            {
                string point = labels != null && i < labels.Count ? labels[i] : UiText.Whole(i);
                points[i] = n == 24 ? point : UiText.Fill("common_day_number", "number", point);   // the week's points are days of the game's calendar
                values[i] = entries[i].Value;
            }
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                total += values[i];
            }
            // The period's customers in all as the page's figure, over the chart they add up from.
            _m.Figures = new[] { new UiFigureModel { Label = UiText.Fill("reallife_ui_bizman_customers_total", "period", _m.Periods[_m.Period]), Value = UiText.Whole((int)Math.Round(total)) } };
            int focus = _m.Chart != null ? _m.Chart.Focus : -1;
            _m.Chart = new UiChartModel
            {
                Kind = UiChartKind.Line, Points = points, Series = new[] { new UiSeries { Label = UiText.T("bizman_insight_customers"), Values = values } },
                Format = v => UiText.Whole((int)Math.Round(v)), Focus = focus < n ? focus : -1,
                EmptyTitle = UiText.T("reallife_ui_bizman_customers_none"), EmptyBody = UiText.T("reallife_ui_bizman_customers_none_body")
            };
            _m.GradesTitle = UiText.T("bizman_insight_promotion");
            _m.Grades = new[]
            {
                Grade("reallife_ui_bizman_overall", GameContract.SeePromotion, see), Grade("bizman_traffic_index", GameContract.SeeTraffic, see),
                Grade("bizman_insight_marketing", GameContract.SeeMarketing, see)
            };
            _m.SecondTitle = UiText.T("bizman_insight_customer_satisfaction");
            if (On(Get<GameObject>(GameContract.SeeSatisfiedShown, see)))
            {
                _m.SecondGrades = new[]
                {
                    Grade("reallife_ui_bizman_overall", GameContract.SeeSatisfied, see), Grade("bizman_insight_customer_service", GameContract.SeeService, see),
                    Grade("bizman_insight_pricing", GameContract.SeePricing, see), Grade("bizman_insight_interior", GameContract.SeeInterior, see),
                    Grade("bizman_insight_cleanliness", GameContract.SeeClean, see)
                };
            }
            else
            {
                _m.SecondEmpty = UiText.T("reallife_ui_bizman_satisfaction_none");
            }
            Capacity(see);
        }

        // A graded bar as the game's own progress bar holds it, 0 to 100.
        private static BizManGrade Grade(string key, GameMember bar, BizManInsight see)
        {
            UI.Elements.ProgressBar p = Get<UI.Elements.ProgressBar>(bar, see);
            int value = p != null && p.slider != null && p.slider.maxValue > p.slider.minValue
                ? (int)Math.Round((p.slider.value - p.slider.minValue) * 100f / (p.slider.maxValue - p.slider.minValue)) : 0;
            return new BizManGrade { Label = UiText.T(key), Value = Math.Max(0, Math.Min(100, value)) };
        }

        // The capacity as the game's insight lists it: what an hour allows of the building's limit, then each item that caps it.
        private void Capacity(BizManInsight see)
        {
            _m.CapacityTitle = UiText.T("bizman_customers_capacity");
            string now = TextOf(Get<TextMeshProUGUI>(GameContract.SeeCapacityNow, see)), limit = TextOf(Get<TextMeshProUGUI>(GameContract.SeeCapacityLimit, see));
            _m.CapacityLine = now != null && limit != null && limit != "-" ? UiText.Fill("reallife_ui_bizman_capacity_line", "now", now, "limit", limit) : null;
            Transform template = Get<Transform>(GameContract.SeeCapacity, see);
            var rows = new List<BizManCapacity>(8);
            int.TryParse(limit, out int most);
            for (int i = 0; template != null && template.parent != null && i < template.parent.childCount; i++)
            {
                Transform e = template.parent.GetChild(i);
                if (e == template || !e.gameObject.activeSelf)
                {
                    continue;
                }
                string name = WordsAt(e, "Type/ItemTitle"), figure = WordsAt(e, "Type/TotalCapacityLabel");
                int shown = 0;
                if (figure != null)
                {
                    int cut = figure.IndexOf('/');
                    int.TryParse(cut > 0 ? figure.Substring(0, cut).Trim() : figure.Trim(), out shown);
                }
                int share = most > 0 ? Math.Min(100, shown * 100 / most) : 0;
                Transform icon = e.Find("Type/Icon");
                Image mark = icon != null ? icon.GetComponent<Image>() : null;
                rows.Add(new BizManCapacity { Name = name, Figure = figure, Share = share, Tone = mark != null ? Tone(mark.color) : UiRole.Muted });
            }
            _m.Capacity = rows.ToArray();
        }

        // ------------------------------------------------------------------------------------------------- prices

        private void Pricing(BizManBusiness b)
        {
            _m.Part = BizManPart.Pricing;
            BizManInventoryPricing p = b.bizManInventoryPricing;
            InventoryProductsScrollerController table = p != null ? p.inventoryProductsScrollerController : null;
            _m.MoreTitle = UiText.T("reallife_ui_bizman_this_week");
            _m.More = new[] { Week(p != null ? p.itemsSold : null, "reallife_ui_bizman_sold_week"), Week(p != null ? p.profitMargin : null, "reallife_ui_bizman_takings_week") };
            UiDataTableModel old = _m.Goods;
            var t = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("common_product"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = UiText.T("bizman_sold_last_7_days"), Kind = UiCellKind.Figure, Width = 128f },
                    new UiDataColumn { Title = UiText.T("bizman_stock_count"), Kind = UiCellKind.Figure, Width = 112f },
                    new UiDataColumn { Title = UiText.T("bizman_market_price"), Kind = UiCellKind.Money, Width = 152f },
                    new UiDataColumn { Title = UiText.T("bizman_retail_price"), Kind = UiCellKind.Stepper, Width = UiDataTable.StepperWidth + UiDataTable.MarkRoom },
                },
                EmptyIcon = UiIcon.Tag, EmptyTitle = UiText.T("reallife_ui_bizman_goods_none"), EmptyBody = UiText.T("reallife_ui_bizman_goods_none_body"),
                Offset = old != null ? old.Offset : 0f, Reveal = old != null ? old.Reveal : -1
            };
            Heads(t, table != null ? table.transform.Find("Headers") : null, BizManRules.PriceHeads);
            int n = table != null ? table.data.Count : 0;
            t.Rows = new UiDataRow[n];
            string district = b.building != null && !string.IsNullOrEmpty(b.building.Neighbourhood) ? b.building.Neighbourhood : null;   // the district the goods' market prices are of
            bool managed = district != null && PricingManagerHelper.GetPlanCoveringNeighborhood(district) != null;
            RivalGoods(district);
            for (int i = 0; i < n; i++)
            {
                t.Rows[i] = Good(table.data[i], managed);
            }
            t.RowCount = n;
            _m.Goods = t;
            BestSellers(p);
        }

        private readonly HashSet<string> _rivalGoods = new HashSet<string>();

        // The goods a rival sells in the district, as the game's price cell asks before it marks a price as dear.
        private void RivalGoods(string district)
        {
            _rivalGoods.Clear();
            List<BuildingRegistration> all = district != null && SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || string.IsNullOrEmpty(reg.businessOwnerRivalId) || reg.RentedByPlayer || reg.cachedAvailableProducts == null || GameRegistrations.District(reg) != district)
                {
                    continue;
                }
                _rivalGoods.UnionWith(reg.cachedAvailableProducts);
            }
        }

        // A good's row as the game's own price cell shows it: a bag's price fixed at nothing, a container's market price and a
        // service's stock as a dash, the price held while a pricing manager sets it, and the price marked yellow above the
        // market and red at twice the usual price, by the game's own rule (InventoryProductCellView.SetData).
        private UiDataRow Good(InventoryProductCellView.InventoryProductModel g, bool managed)
        {
            if (g == null || g.Item == null || g.RetailPriceReference == null)
            {
                return new UiDataRow { Cells = new string[5] };
            }
            Item item = g.Item;
            bool bag = item.HasTag(TagRef.Itemtag.isbag);
            bool service = (item.type & ItemType.ServiceProduct) != 0;
            float price = g.RetailPriceReference.price;
            float dear = _rivalGoods.Contains(item.itemName) ? Math.Min(item.DefaultMarketPrice, g.MarketPrice) : item.DefaultMarketPrice;
            UiRole mark = bag ? UiRole.None : price != 0f && price >= item.DefaultMarketPrice * 2f ? UiRole.Danger : price > dear ? UiRole.Warn : UiRole.None;
            int steps = bag || managed ? 0 : (price > 0f ? UiDataTable.CanLess : 0) | UiDataTable.CanMore;
            return new UiDataRow
            {
                Cells = new[]
                {
                    g.ProductName, UiText.Whole(g.SoldLastWeek), service ? "-" : UiText.Whole(g.StockCount),
                    item.HasTag(TagRef.Itemtag.isshoppingcontainer) ? "-" : g.MarketPrice.ToCurrencyFormat(), bag ? "0" : price.ToCurrencyFormat()
                },
                Values = new[] { 0, -1, -1, 0, steps },
                Tones = new[] { UiRole.None, UiRole.None, !service && g.StockCount <= 0 ? UiRole.DangerText : UiRole.None, UiRole.None, mark }
            };
        }

        // A figure of the week as the game's left side writes it: the amount, and its change against the week before in the
        // colour the game gives a rise or a fall.
        private static BizManFact Week(Transform box, string labelKey)
        {
            string change = WordsAt(box, "Horizontal/Percentage");
            bool rising = box != null && Shown(box.Find("Horizontal/PositiveIcon"));
            string amount = WordsAt(box, "Horizontal/Amount") ?? "0";
            string signed = change != null && rising && !change.StartsWith("-", StringComparison.Ordinal) ? "+" + change : change;
            return new BizManFact { Label = UiText.T(labelKey), Value = signed != null ? amount + " (" + signed + ")" : amount, Tone = rising ? UiRole.OkText : UiRole.DangerText };
        }

        private void BestSellers(BizManInventoryPricing p)
        {
            Transform template = p != null ? p.bestSellingProductEntry : null;
            var labels = new List<string>(4);
            var values = new List<string>(4);
            var shares = new List<float>(4);
            for (int i = 0; template != null && template.parent != null && i < template.parent.childCount; i++)
            {
                Transform e = template.parent.GetChild(i);
                if (e == template || !e.gameObject.activeSelf)
                {
                    continue;
                }
                string percent = WordsAt(e, "PercentageLabel");
                float.TryParse(percent != null ? percent.TrimEnd('%') : null, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float share);
                labels.Add(WordsAt(e, "ItemLabel"));
                values.Add(percent);
                shares.Add(share);
            }
            float listed = 0f;
            for (int i = 0; i < shares.Count; i++)
            {
                listed += shares[i];
            }
            // The game lists its best sellers only: the rest of the week's sales stand as the ring's grey, so each slice is its own share.
            _m.ShareRest = shares.Count > 0 && listed < 100f ? 100f - listed : 0f;
            _m.SharesTitle = UiText.T("reallife_ui_bizman_best_sellers");
            _m.ShareLabels = labels.ToArray();
            _m.ShareValues = values.ToArray();
            _m.Shares = shares.ToArray();
        }

        // A table's head from the game's own: which columns it sorts by, and the one it sorted with its order's arrow.
        private static void Heads(UiDataTableModel t, Transform heads, string[] names)
        {
            for (int c = 0; c < t.Columns.Length; c++)
            {
                string name = BizManRules.HeadOf(names, c);
                Transform head = heads != null && name != null ? heads.Find(name) : null;
                t.Columns[c].Sortable = head != null && head.GetComponent<Button>() != null;
                if (head != null && (Shown(head.Find("ArrowUp")) || Shown(head.Find("ArrowDown"))))
                {
                    t.SortColumn = c;
                    t.SortDown = Shown(head.Find("ArrowDown"));
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- deliveries

        private void Deliveries(BizManBusiness b)
        {
            _m.Part = BizManPart.Deliveries;
            BizManDeliveries deals = PageOf<BizManDeliveries>(b, BizManRules.Deliveries);
            List<DeliveryContractEntry> entries = Get<List<DeliveryContractEntry>>(GameContract.DealEntries, deals);
            DeliveryContractEntry chosen = Get<DeliveryContractEntry>(GameContract.DealChosen, deals);
            _m.ItemsTitle = UiText.T("bizman_delivery_contracts");
            _m.GoodsTitle = UiText.T("common_products");
            _m.ItemsEmpty = UiText.T("reallife_ui_bizman_contracts_none");
            _m.ItemsEmptyBody = UiText.T("reallife_ui_bizman_contracts_none_body");
            int n = entries != null ? entries.Count : 0;
            var items = new List<BizManItem>(n);
            for (int i = 0; i < n; i++)
            {
                DeliveryContractEntry e = entries[i];
                if (e == null)
                {
                    continue;
                }
                Image logo = Get<Image>(GameContract.DealLogo, e);
                items.Add(new BizManItem
                {
                    Picture = logo != null ? Pic(logo.sprite) : null, Glyph = UiIcon.Package, Name = TextOf(Get<TMP_Text>(GameContract.DealName, e)),
                    Line = TextOf(Get<TMP_Text>(GameContract.DealFee, e)), Chosen = e == chosen,
                    WarnWords = On(Get<GameObject>(GameContract.DealWarn, e)) ? UiText.T("reallife_ui_bizman_nothing_ordered") : null
                });
            }
            _m.Items = items.ToArray();
            BizManContractSettings set = Get<BizManContractSettings>(GameContract.DealSettings, deals);
            if (set == null || !set.gameObject.activeSelf)
            {
                return;
            }
            _m.PartTitle = Words(set.contractTitleLabel);
            _m.Facts = new[] { Fact("bizman_purchasingagents_next_delivery", Words(set.nextDeliveryDayLabel)), Fact("common_total", TextOf(set.totalCostLabel)) };
            _m.Lead = Words(set.lockTimeLabel);
            _m.HasRepeat = set.repeatingOrderToggle != null && set.repeatingOrderToggle.gameObject.activeInHierarchy;
            _m.Repeat = set.repeatingOrderToggle != null && set.repeatingOrderToggle.isOn;
            _m.RepeatWords = UiText.T("repeating_order");
            _actions.Clear();
            Offer(_actions, set.endContractButton, BizManPages.DoEndContract, UiButtonKind.Destructive, UiIcon.Trash, UiAction.Count, "bizman_delivery_contract_cancel_contract");
            Offer(_actions, set.cancelContractButton, BizManPages.DoCancelContract, UiButtonKind.Secondary, UiIcon.Pause, UiAction.Count, "reallife_ui_bizman_pause");
            Offer(_actions, set.urgentOrderButton, BizManPages.DoUrgent, UiButtonKind.Primary, UiIcon.Lightning, UiAction.Alt, "bizman_deliveries_urgent_order");
            Offer(_actions, set.orderButton, BizManPages.DoOrder, UiButtonKind.Primary, UiIcon.Truck, UiAction.Alt, "bizman_purchasingagents_order");
            _m.Actions = _actions.ToArray();
            BizManDeliveriesProductsScrollerController goods = set.bizManDeliveriesProductsScrollerController;
            UiDataTableModel old = _m.Goods;
            var t = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("common_product"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = UiText.T("bizman_deliveries_stock"), Kind = UiCellKind.Figure, Width = 96f },
                    new UiDataColumn { Title = UiText.T("common_price"), Kind = UiCellKind.Money, Width = 104f },
                    new UiDataColumn { Title = UiText.T("common_amount"), Kind = UiCellKind.Stepper, Width = 176f },
                },
                EmptyIcon = UiIcon.Package, EmptyTitle = UiText.T("reallife_ui_bizman_goods_none"), Offset = old != null ? old.Offset : 0f, Reveal = old != null ? old.Reveal : -1
            };
            int count = goods != null ? goods.data.Count : 0;
            t.Rows = new UiDataRow[count];
            for (int i = 0; i < count; i++)
            {
                BizManDeliveriesProductModel g = goods.data[i];
                t.Rows[i] = new UiDataRow
                {
                    Cells = new[]
                    {
                        g != null ? g.productName : null, g != null ? UiText.Whole(g.stock) : null, g != null ? g.pricePerUnit.ToCurrencyFormat() : null,
                        g != null ? UiText.Whole(g.Amount) : null
                    },
                    Values = new[] { 0, -1, 0, g == null || g.isContractActive ? 0 : (g.Amount > 0 ? UiDataTable.CanLess : 0) | UiDataTable.CanMore }
                };
            }
            t.RowCount = count;
            _m.Goods = t;
        }

        // ------------------------------------------------------------------------------------------------- marketing

        private void Marketing(BizManBusiness b)
        {
            _m.Part = BizManPart.Marketing;
            BizManMarketing ads = b.bizManMarketing;
            UI.Elements.ProgressBar efficiency = Get<UI.Elements.ProgressBar>(GameContract.AdsEfficiency, ads);
            int value = efficiency != null && efficiency.slider != null && efficiency.slider.maxValue > 0f ? (int)Math.Round(efficiency.slider.value * 100f / efficiency.slider.maxValue) : 0;
            _m.Grades = new[] { new BizManGrade { Label = UiText.T("bizman_marketing_efficiency"), Value = value } };
            _m.Facts = new[] { Fact("bizman_marketing_total_daily_expenses", TextOf(Get<TMP_Text>(GameContract.AdsDaily, ads))) };
            _m.ItemsTitle = UiText.T("reallife_ui_bizman_campaigns");
            _m.ItemsEmpty = UiText.T("reallife_ui_bizman_campaigns_none");
            _m.ItemsEmptyBody = UiText.T("bizman_marketing_no_campaigns");
            Transform agency = Get<Transform>(GameContract.AdsAgency, ads);
            Entries(agency);
            _agencies.Clear();
            _agencies.AddRange(_entries);
            _campaigns.Clear();
            var items = new BizManItem[_agencies.Count];
            var toggles = new List<BizManToggle>(8);
            for (int g = 0; g < _agencies.Count; g++)
            {
                Transform e = _agencies[g];
                Button cancel = e.Find("CancelCampaign") != null ? e.Find("CancelCampaign").GetComponent<Button>() : null;
                items[g] = new BizManItem { Name = WordsAt(e, "TopPart/AgencyName"), Value = WordsAt(e, "PricePerDayValue"), Link = UiWidgets.Shows(cancel) ? UiWidgets.Label(cancel) : null };
                Transform kind = e.Find("TopPart/MarketingTypeEntry");
                for (int i = 0; kind != null && kind.parent != null && i < kind.parent.childCount; i++)
                {
                    Transform k = kind.parent.GetChild(i);
                    Toggle on = k != kind && k.gameObject.activeSelf && k.Find("Enabled") != null ? k.Find("Enabled").GetComponent<Toggle>() : null;
                    if (on == null)
                    {
                        continue;
                    }
                    _campaigns.Add(on);
                    toggles.Add(new BizManToggle { Words = WordsAt(k, "MarketingTypeName"), On = on.isOn, Disabled = !on.interactable, Group = g });
                }
            }
            _m.Items = items;
            _m.Toggles = toggles.ToArray();
            _m.OthersTitle = UiText.T("reallife_ui_bizman_agencies");
            _m.SecondEmpty = UiText.T("reallife_ui_bizman_agencies_none");
            Transform contact = Get<Transform>(GameContract.AdsContact, ads), splitter = Get<Transform>(GameContract.AdsContactSplit, ads);
            _contacts.Clear();
            for (int i = 0; contact != null && contact.parent != null && i < contact.parent.childCount; i++)
            {
                Transform c = contact.parent.GetChild(i);
                if (c != contact && c != splitter && c.gameObject.activeSelf && c.Find("CallButton") != null)
                {
                    _contacts.Add(c);
                }
            }
            var others = new BizManItem[_contacts.Count];
            for (int i = 0; i < _contacts.Count; i++)
            {
                Transform c = _contacts[i];
                Transform logo = c.Find("BusinessLogoMask/BusinessLogo");
                Image picture = logo != null ? logo.GetComponent<Image>() : null;
                others[i] = new BizManItem
                {
                    Picture = picture != null ? Pic(picture.sprite) : null, Glyph = UiIcon.Megaphone, Name = WordsAt(c, "BusinessName"), Line = WordsAt(c, "BusinessType"),
                    Link = UiText.T("phone_call_button")
                };
            }
            _m.Others = others;
        }

        // ------------------------------------------------------------------------------------------------- settings

        private void Settings(BizManBusiness b)
        {
            _m.Part = BizManPart.Settings;
            BizManSettings set = b.bizManSettings;
            Transform page = Page(b, BizManRules.Settings);
            _m.PartTitle = UiText.T("bizman_settings_manage_business_label");
            _m.NameLabel = UiText.T("common_business_name");
            TMP_InputField name = Get<TMP_InputField>(GameContract.SetName, set);
            _m.NameText = name != null ? name.text : null;
            _m.NamePlaceholder = UiText.T("reallife_ui_bizman_name_hint");
            UI.Elements.Dropdown kind = Get<UI.Elements.Dropdown>(GameContract.SetKind, set);
            _m.KindLabel = UiText.T("common_business_type");
            _m.Kinds = Options(kind);
            _m.KindChosen = kind != null ? kind.SelectedOptionIndex : -1;
            _m.KindOpen = _kindOpen && _m.Kinds.Length > 1;
            _m.Uniforms = Calling(page, "OnConfigureUniformPresetsButtonClick") != null ? UiText.T("bizman_set_up_uniforms") : null;
            LogoCustomizer logo = Get<LogoCustomizer>(GameContract.SetLogo, set);
            _actions.Clear();
            Offer(_actions, Calling(set, "RandomizeBusinessName"), BizManPages.DoRandom, UiButtonKind.Secondary, UiIcon.Spark, UiAction.Count, "reallife_ui_bizman_randomise");
            Button save = Get<Button>(GameContract.SetSave, set);
            if (save != null && save.gameObject.activeInHierarchy)
            {
                _actions.Add(Do(BizManPages.DoSave, UiText.T("common_save_changes"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, !save.interactable));
            }
            if (logo != null && logo.gameObject.activeInHierarchy)
            {
                _m.LogoTitle = UiText.T("bizman_settings_sign_appearance_info");
                _m.LogoLabel = UiText.T("reallife_ui_bizman_logo");
                _m.Picture = _m.Head.Picture;
                _m.PictureAspect = _m.Head.Aspect;
                UI.Elements.Dropdown font = Get<UI.Elements.Dropdown>(GameContract.LogoFont, logo);
                _m.FontLabel = UiText.T("common_font");
                _m.Fonts = Options(font);
                _m.FontChosen = font != null ? font.SelectedOptionIndex : -1;
                _m.FontOpen = _fontOpen && _m.Fonts.Length > 1;
                Button saveLogo = Get<Button>(GameContract.LogoSave, logo);
                if (_logoEdit)
                {
                    ReadLogo(logo, saveLogo);   // shape and colours open: the editor takes the page
                    return;
                }
                if (saveLogo != null && saveLogo.gameObject.activeInHierarchy)
                {
                    _actions.Add(Do(BizManPages.DoSaveLogo, UiText.T("bizman_settings_save_logo"), UiButtonKind.Secondary, UiIcon.Check, UiAction.Count, !saveLogo.interactable));
                }
                _actions.Add(Do(BizManPages.DoLogoEdit, UiText.T("reallife_ui_bizman_logo_more"), UiButtonKind.Quiet, UiIcon.None, UiAction.Count));
            }
            else
            {
                _logoEdit = false;
            }
            Offer(_actions, Calling(page, "TerminateContract"), BizManPages.DoTerminate, UiButtonKind.Destructive, UiIcon.Trash, UiAction.Count, "bizman_terminate_contract_button");
            Offer(_actions, Calling(set, "ShutdownBusiness"), BizManPages.DoShutdown, UiButtonKind.Destructive, UiIcon.Close, UiAction.Count, "bizman_shutdown_business");
            _m.Actions = _actions.ToArray();
            _m.Lead = set != null && set.HasUnsavedChanges ? UiText.T("reallife_ui_bizman_unsaved") : null;
        }

        // A drop-down's options in its own words, as it shows them.
        private static string[] Options(UI.Elements.Dropdown d)
        {
            List<string> options = Get<List<string>>(GameContract.BizOptions, d);
            bool localize = d != null && GameContract.BizOptionsLocalized.Field != null && (bool)GameContract.BizOptionsLocalized.Field.GetValue(d);
            var words = new string[options != null ? options.Count : 0];
            for (int i = 0; i < words.Length; i++)
            {
                words[i] = localize ? UiText.T(options[i]) : options[i];
            }
            return words;
        }

        // ------------------------------------------------------------------------------------------------- real estate

        private void Estate(BizManBusiness b)
        {
            _m.Part = BizManPart.Estate;
            RealEstateSettings est = PageOf<RealEstateSettings>(b, BizManRules.RealEstate);
            _actions.Clear();
            if (On(Get<GameObject>(GameContract.EstateRent, est)))
            {
                _m.MoreTitle = UiText.T("bizman_real_estate_rent_management");
                _m.More = new[]
                {
                    Split(Words(Get<Component>(GameContract.EstateRentNow, est))),
                    Split(Words(Get<Component>(GameContract.EstateRentMarket, est))),
                    new BizManFact { Label = UiText.T("bizman_real_estate_occupancy"), Value = TextOf(Get<TMP_Text>(GameContract.EstateOccupancy, est)),
                        Tone = Ink(Tone(Get<TMP_Text>(GameContract.EstateOccupancy, est) != null ? Get<TMP_Text>(GameContract.EstateOccupancy, est).color : Color.clear)) },
                    new BizManFact { Label = Words(Get<Component>(GameContract.EstateDailyLabel, est)), Value = TextOf(Get<TMP_Text>(GameContract.EstateDaily, est)) },
                };
                Slider slider = Get<Slider>(GameContract.EstateSlider, est);
                _m.SliderLabel = UiText.T("reallife_ui_bizman_rent_new");
                _m.SliderFigure = TextOf(Get<TMP_Text>(GameContract.EstateNewRent, est));
                _m.SliderShare = slider != null && slider.maxValue > slider.minValue ? (slider.value - slider.minValue) / (slider.maxValue - slider.minValue) : 0f;
                _m.SliderHeld = _sliderHeld;
                _m.Lead = Words(Get<Component>(GameContract.EstateWhen, est));
                Offer(_actions, Get<Button>(GameContract.EstateApply, est), BizManPages.DoApplyRent, UiButtonKind.Secondary, UiIcon.Check, UiAction.Count, "bizman_real_estate_apply_new_rent");
            }
            if (On(Get<GameObject>(GameContract.EstateUnit, est)))
            {
                _m.SecondTitle = UiText.T("bizman_real_estate_self_occupied_unit");
                _m.SecondEmpty = Words(Get<Component>(GameContract.EstateUnitState, est));
                Offer(_actions, Get<Button>(GameContract.EstateTakeover, est), BizManPages.DoTakeoverUnit, UiButtonKind.Secondary, UiIcon.Key, UiAction.Count, "bizman_real_estate_takeover_unit");
            }
            _m.PartTitle = UiText.T("bizman_real_estate_set_for_sale");
            _m.Facts = new[]
            {
                new BizManFact { Label = Words(Get<Component>(GameContract.EstateBoughtLabel, est)), Value = TextOf(Get<TMP_Text>(GameContract.EstateBought, est)) },
                Fact("bizman_real_estate_estimated_market_price", TextOf(Get<TMP_Text>(GameContract.EstateWorth, est))),
                new BizManFact { Label = UiText.T("bizman_real_estate_your_potential_gain"), Value = TextOf(Get<TMP_Text>(GameContract.EstateGain, est)),
                    Tone = Ink(Tone(GainColour(Get<TMP_Text>(GameContract.EstateGain, est)))) },
            };
            if (On(Get<GameObject>(GameContract.EstateCancelPanel, est)))
            {
                _m.ItemsEmpty = Words(Get<Component>(GameContract.EstateForSale, est));
                Offer(_actions, Calling(est, "CancelForSale"), BizManPages.DoCancelSale, UiButtonKind.Secondary, UiIcon.Close, UiAction.Count, "bizman_real_estate_cancel_sale");
            }
            else
            {
                TMP_InputField price = Get<TMP_InputField>(GameContract.EstatePrice, est);
                Button sell = Calling(est, "SetForSale");
                _m.Amount = new BizManAmount
                {
                    Title = UiText.T("common_sales_price"), Line = string.IsNullOrEmpty(price != null ? price.text : null) ? UiText.T("bizman_real_estate_enter_sales_price") : null,
                    Figure = price != null ? price.text : null, Field = BizManPages.FieldSale, CanLess = price != null && price.text.Length > 0, CanMore = true,
                    Disabled = price == null || !price.interactable,
                    Send = Do(BizManPages.DoMarkSale, sell != null ? UiWidgets.Label(sell) ?? UiText.T("bizman_real_estate_mark_for_sale") : UiText.T("bizman_real_estate_mark_for_sale"),
                        UiButtonKind.Primary, UiIcon.Tag, UiAction.Alt, sell == null || !sell.interactable)
                };
            }
            _m.Actions = _actions.ToArray();
            BizManBuildingInfo info = Get<BizManBuildingInfo>(GameContract.BizBuildingInfo, b);
            _m.FactsTitle = UiText.T("reallife_ui_bizman_the_building");
            _m.BuildingFacts = new[]
            {
                Fact("common_neighborhood", b.building != null && !string.IsNullOrEmpty(b.building.Neighbourhood) ? UiText.T(b.building.Neighbourhood) : null),
                Fact("common_building_type", b.building != null ? UiText.T(b.building.BuildingType) : null),
                new BizManFact { Label = (Words(Get<Component>(GameContract.PlaceTaxLabel, info)) ?? string.Empty).TrimEnd(':', ' '), Value = TextOf(Get<TMP_Text>(GameContract.PlaceTax, info)) },
            };
        }

        // A line the game writes as its label and its figure in one ("Current rent: $9.40/m2") as a fact's label and value.
        private static BizManFact Split(string words)
        {
            int cut = words != null ? words.IndexOf(':') : -1;
            return cut > 0 && cut < words.Length - 1 ? new BizManFact { Label = words.Substring(0, cut).Trim(), Value = words.Substring(cut + 1).Trim() } : new BizManFact { Value = words };
        }

        // The game colours a gain's per cent in rich text inside the figure: its colour is read from the tag.
        private static Color GainColour(TMP_Text t)
        {
            string raw = t != null ? t.text : null;
            if (raw == null)
            {
                return Color.clear;
            }
            return raw.Contains("#0CA200") ? new Color(0.05f, 0.64f, 0f) : raw.Contains("#D4000B") ? new Color(0.83f, 0f, 0.04f) : Color.clear;
        }

        // ------------------------------------------------------------------------------------------------- the handover

        // A page only the game's own view shows: what it is, why it opens there and its button.
        private void Handover(BizManBusiness b)
        {
            _m.Part = BizManPart.Classic;
            bool extras = _shown == ExtrasName;
            Transform tab = null;
            for (int i = 0; i < _tabs.Count && tab == null; i++)
            {
                tab = _tabs[i].name == _shown ? _tabs[i] : null;
            }
            string page = extras ? UiText.T("reallife_ui_bizman_other_mods") : tab != null ? TabWords(tab) : UiText.T("reallife_ui_bizman_tab_building");
            bool foreign = extras || (tab != null && !UiHostRules.Own(tab.name, BizManRules.GameTabs));
            _m.ClassicIcon = extras ? UiIcon.Code : BizManRules.TabIcon(_shown);
            _m.ClassicTitle = UiText.Fill(foreign ? "reallife_ui_bizman_foreign_title" : "reallife_ui_bizman_classic_title", "page", page);
            _m.ClassicBody = UiText.T(foreign ? "reallife_ui_bizman_foreign_body" : "reallife_ui_bizman_classic_body");
            _m.Actions = new[] { new UiActionModel { Label = UiText.Fill("reallife_ui_bizman_classic_open", "page", page), Kind = UiButtonKind.Primary, Icon = UiIcon.Next, Key = UiAction.Alt,
                Command = BizManPages.Classic, Arg = BizManPages.DoClassic } };
        }
    }
}
