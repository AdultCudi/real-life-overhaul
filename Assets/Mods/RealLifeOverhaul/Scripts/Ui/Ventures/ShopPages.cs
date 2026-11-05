using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Web shop pages, the seller's side of an online marketplace: the sites as a hub with their figures, thirty days of sales and settings (delivery fee, free over, area, packing
    /// time, night orders, matching rivals), the orders open and finished, and the catalogue with each product's price and switch. Game-free.
    /// </summary>
    public static class ShopPages
    {
        public const int TabSites = 0, TabOrders = 1, TabCatalogue = 2;
        public const string SiteKey = "site:", LineKey = "line:";
        private const long FeeStep = 50, FreeStep = 500;   // cents

        public static void Fill(VentModel m, VentView v, ShopFacts f, IShopActions act, VentWords w)
        {
            m.Title = w.T("reallife_online_app", "Web shop");
            m.Icon = UiIcon.ShoppingCart;
            m.Prompts = VenPages.Prompts(w);
            var runs = new VentRuns();
            int orders = 0;
            for (int i = 0; i < f.Sites.Length; i++)
            {
                orders += f.Sites[i].Open.Length;
            }
            m.Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.Store, Label = w.T("reallife_online_tab_shops", "Sites"), Count = f.Sites.Length > 0 ? f.Sites.Length.ToString() : null },
                new UiNavEntry { Icon = UiIcon.Package, Label = w.T("reallife_online_tab_orders", "Orders"), Count = orders > 0 ? orders.ToString() : null },
                new UiNavEntry { Icon = UiIcon.Tag, Label = w.T("reallife_online_tab_catalogue", "Catalogue") }
            };
            m.Tab = Math.Max(0, Math.Min(2, v.Tab));
            if (!f.Ready)
            {
                m.Shape = VentShape.Off;
                m.OffTitle = f.OffTitle ?? w.T("reallife_ui_shop_off_title", "The Web shop is not running");
                m.OffBody = f.OffBody ?? w.T("reallife_ui_shop_off_body", "Load a save with the economy switched on to sell online from your shops and warehouses.");
                m.Runs = runs.ToArray();
                return;
            }
            if (f.Sites.Length == 0)
            {
                m.Empty = w.T("reallife_ui_shop_none", "You have no site yet. Open one for a shop or a warehouse and customers can order from it.");
                m.Runs = runs.ToArray();
                return;
            }
            ShopSiteFact site = f.Sites[0];
            for (int i = 0; i < f.Sites.Length; i++)
            {
                site = v.S1 == f.Sites[i].Key ? f.Sites[i] : site;
            }
            if (f.Sites.Length > 1)
            {
                int at = Array.IndexOf(f.Sites, site);
                m.HeadActions = new[]
                {
                    new UiActionModel
                    {
                        Label = w.F("reallife_ui_shop_site_n", "Site: {name}", "name", site.Name), Kind = UiButtonKind.Secondary, Icon = UiIcon.Store, Command = UiAppWindow.ActionCommand,
                        Arg = runs.Add(() => { v.S1 = f.Sites[(at + 1) % f.Sites.Length].Key; v.Pick = null; return true; })
                    }
                };
            }
            switch (m.Tab)
            {
                case TabOrders: Orders(m, site, w); break;
                case TabCatalogue: Catalogue(m, v, site, act, w, runs); break;
                default: Hub(m, v, site, act, w, runs); break;
            }
            m.Runs = runs.ToArray();
        }

        private static string Cents(VentWords w, long cents) => w.Price(cents);

        private static void Hub(VentModel m, VentView v, ShopSiteFact s, IShopActions act, VentWords w, VentRuns runs)
        {
            string place = s.Key;
            m.HeroFigures = new[]
            {
                new UiFigureModel { Label = w.T("reallife_ui_shop_orders_today", "Orders today"), Value = s.Orders.ToString() },
                new UiFigureModel { Label = w.T("reallife_ui_shop_takings_today", "Takings today"), Value = Cents(w, s.Takings) },
                new UiFigureModel { Label = w.T("reallife_ui_shop_reviews", "Reviews"), Value = s.Reviews.ToString() },
                new UiFigureModel { Label = w.T("reallife_ui_shop_week", "Takings in a week"), Value = Cents(w, s.Week) }
            };
            var left = new List<VentBlock>(8);
            var right = new List<VentBlock>(16);
            left.Add(VentBlock.Section(w.T("reallife_ui_shop_sales_sec", "Sales by day, 30 days")));
            if (s.Sales30.Length < 2)
            {
                left.Add(VentBlock.Note(w.T("reallife_ui_shop_sales_none", "Not yet: sales are kept day by day, and the line draws from the second day.")));
            }
            else
            {
                var vals = new float[s.Sales30.Length];
                for (int i = 0; i < vals.Length; i++)
                {
                    vals[i] = s.Sales30[i] / 100f;
                }
                left.Add(new VentBlock
                {
                    Kind = VentBlockKind.Chart, ChartHeight = 192f,
                    Chart = new UiChartModel
                    {
                        Kind = UiChartKind.Line, Points = s.SalesDays, Series = new[] { new UiSeries { Label = s.Name, Values = vals, Tint = UiRole.None } }, Format = x => w.Money(x),
                        AxisFormat = x => w.Money(x), Still = true, EvenAxis = true
                    }
                });
            }
            left.Add(new VentBlock
            {
                Kind = VentBlockKind.Items,
                Items = new[]
                {
                    new VentItem
                    {
                        Icon = UiIcon.Store, Title = s.Name, Sub = s.Business + ", " + s.Quality, Chip = s.Offline ? w.T("reallife_online_row_offline",
                            "Offline") : s.Express ? w.T("reallife_ui_shop_express", "Express") : null,
                        ChipTone = s.Offline ? UiRole.Warn : UiRole.Ok, Amount = w.F("reallife_ui_shop_stars", "{stars} stars", "stars", (s.Halves / 2f).ToString("0.0")), Run = -1
                    }
                }
            });
            var acts = new List<UiActionModel>(3);
            acts.Add(new UiActionModel { Label = w.T("reallife_ui_shop_rename", "Rename"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Pencil,
                Arg = runs.Add(() => { act.Rename(place); return true; }) });
            acts.Add(new UiActionModel { Label = w.T("reallife_ui_shop_close", "Close the site"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Close,
                Arg = runs.Add(() => { act.Close(place); return true; }) });
            if (s.CanPay)
            {
                acts.Add(new UiActionModel { Label = w.T("reallife_ui_shop_pay", "Pay hosting"), Kind = UiButtonKind.Primary, Icon = UiIcon.Coin, Key = UiAction.Alt,
                    Arg = runs.Add(() => { act.Pay(place); return true; }) });
            }
            else if (s.CanAdvertise)
            {
                acts.Add(new UiActionModel { Label = w.T("reallife_ui_shop_advertise", "Advertise"), Kind = UiButtonKind.Primary, Icon = UiIcon.Megaphone, Key = UiAction.Alt,
                    Arg = runs.Add(() => { act.Advertise(place); return true; }) });
            }
            left.Add(VentBlock.Buttons(acts.ToArray()));
            right.Add(VentBlock.Section(w.T("reallife_ui_shop_settings", "Settings customers see")));
            int fee = s.FeeCents, free = s.FreeOverCents;
            right.Add(VentBlock.Stepper(w.T("reallife_ui_shop_fee", "Delivery fee"), s.FeeCents > 0 ? Cents(w, s.FeeCents) : w.T("reallife_ui_shop_free", "Free"),
                runs.Add(() => { act.Fee(place, (int)Math.Max(0, fee - FeeStep)); return true; }), fee > 0, true));
            runs.Add(() => { act.Fee(place, (int)(fee + FeeStep)); return true; });
            right.Add(VentBlock.Stepper(w.T("reallife_ui_shop_free_over", "Free delivery over"), s.FreeOverCents > 0 ? Cents(w, s.FreeOverCents) : w.T("reallife_ui_shop_never", "Never"),
                runs.Add(() => { act.FreeOver(place, (int)Math.Max(0, free - FreeStep)); return true; }), free > 0, true));
            runs.Add(() => { act.FreeOver(place, (int)(free + FreeStep)); return true; });
            string[] packing = { w.T("reallife_ui_shop_pack_30", "30 minutes"), w.T("reallife_ui_shop_pack_60", "1 hour"), w.T("reallife_ui_shop_pack_day", "Same day") };
            int pack = s.Packing;
            right.Add(VentBlock.Choice(w.T("reallife_ui_shop_packing", "Packing time"), packing[Math.Max(0, Math.Min(2, pack))], runs.Add(() => { act.Packing(place,
                (pack + 1) % 3); return true; })));
            right.Add(VentBlock.Switch(w.T("reallife_ui_shop_night", "Take orders at night, held until 06:00"), s.Night, runs.Add(() => { act.Toggle(place, 0); return true; })));
            right.Add(VentBlock.Switch(w.T("reallife_ui_shop_match", "Match the lowest rival price"), s.Match, runs.Add(() => { act.Toggle(place, 1); return true; })));
            right.Add(VentBlock.Switch(w.T("reallife_ui_shop_stock", "Show what is in stock"), s.ShowStock, runs.Add(() => { act.Toggle(place, 2); return true; })));
            right.Add(VentBlock.Section(w.T("reallife_ui_shop_area", "Delivery area")));
            for (int i = 0; i < s.Areas.Length && i < 8; i++)
            {
                int d = i;
                right.Add(VentBlock.Switch(s.Areas[i].Name, s.Areas[i].Served, runs.Add(() => { act.Area(place, d); return true; })));
            }
            if (!string.IsNullOrEmpty(s.HostingLine))
            {
                right.Add(VentBlock.Note(s.HostingLine, s.Offline ? UiRole.Warn : UiRole.None));
            }
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static void Orders(VentModel m, ShopSiteFact s, VentWords w)
        {
            var rows = new List<UiDataRow>(s.Open.Length + s.Done.Length);
            foreach (ShopOrderFact o in s.Open)
            {
                rows.Add(OrderRow(w, o, o.Stage, UiRole.Notice));
            }
            foreach (ShopOrderFact o in s.Done)
            {
                rows.Add(OrderRow(w, o, o.Returned ? w.T("reallife_ui_shop_returned", "Returned") : o.Late ? w.T("reallife_ui_shop_late",
                    "Delivered late") : w.T("reallife_ui_shop_delivered", "Delivered"),
                    o.Returned || o.Late ? UiRole.Warn : UiRole.Ok));
            }
            var table = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = w.T("reallife_ui_shop_col_customer", "Customer"), Kind = UiCellKind.Person, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_shop_col_items", "Items"), Kind = UiCellKind.Text, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_shop_col_district", "District"), Kind = UiCellKind.Text, Width = 128f },
                    new UiDataColumn { Title = w.T("reallife_ui_shop_col_total", "Total"), Kind = UiCellKind.Money, Width = 104f },
                    new UiDataColumn { Title = w.T("reallife_ui_shop_col_stage", "Stage"), Kind = UiCellKind.Chip, Width = 152f }
                },
                Rows = rows.ToArray(), RowCount = rows.Count, Selected = -1, EmptyIcon = UiIcon.Package,
                EmptyTitle = w.T("reallife_ui_shop_no_orders", "No orders yet"), EmptyBody = w.T("reallife_ui_shop_orders_how",
                    "Orders come in as customers find the site. Open ones show their stage, finished ones are kept here.")
            };
            m.HeroFigures = new[]
            {
                new UiFigureModel { Label = w.T("reallife_ui_shop_open_orders", "Open orders"), Value = s.Open.Length.ToString() },
                new UiFigureModel { Label = w.T("reallife_ui_shop_overdue", "Overdue"), Value = s.Overdue.ToString() },
                new UiFigureModel { Label = w.T("reallife_ui_shop_takings_today", "Takings today"), Value = Cents(w, s.Takings) }
            };
            m.Left = new[] { new VentBlock { Kind = VentBlockKind.Table, Table = table, Base = UiDataTable.First } };
        }

        private static UiDataRow OrderRow(VentWords w, ShopOrderFact o, string stage, UiRole tone) => new UiDataRow
        {
            Cells = new[] { o.Customer, o.Items, o.District, Cents(w, o.Total), stage }, Values = new[] { 0, 0, 0, 0, 0 }, Hue = VentWords.HueOf(o.Customer),
            Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone }, Subs = new string[5]
        };

        private static void Catalogue(VentModel m, VentView v, ShopSiteFact s, IShopActions act, VentWords w, VentRuns runs)
        {
            var rows = new List<UiDataRow>(s.Lines.Length);
            var keys = new List<string>(s.Lines.Length);
            var steps = new List<int>(s.Lines.Length);
            string place = s.Key;
            for (int i = 0; i < s.Lines.Length; i++)
            {
                ShopLineFact l = s.Lines[i];
                string item = l.Item;
                long own = l.AskCents;
                long step = Math.Max(1, own / 20);
                int less = runs.Add(() => { act.Price(place, item, (int)Math.Max(1, own - step)); return true; });
                runs.Add(() => { act.Price(place, item, (int)(own + step)); return true; });
                steps.Add(less);
                rows.Add(new UiDataRow
                {
                    Cells = new[] { l.Name, l.Stock.ToString(), Cents(w, l.AskCents), l.GoingCents > 0 ? Cents(w, l.GoingCents) : "-", l.RivalCents > 0 ? Cents(w,
                        l.RivalCents) : "-", l.SoldWeek.ToString(),
                        l.On ? w.T("reallife_online_state_offered", "On the site") : w.T("reallife_online_state_not_offered", "Off the site") },
                    Values = new[] { (int)UiIcon.Package, 0, UiDataTable.CanLess | UiDataTable.CanMore, 0, 0 },
                    Tones = new[] { UiRole.None, l.On && l.Stock == 0 ? UiRole.DangerText : UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None,
                        l.On ? UiRole.Ok : UiRole.Muted },
                    Subs = new string[7]
                });
                keys.Add(LineKey + l.Item);
            }
            var table = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = w.T("reallife_ui_shop_col_product", "Product"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_shop_col_stock", "In stock"), Kind = UiCellKind.Figure, Width = 72f },
                    new UiDataColumn { Title = w.T("reallife_ui_shop_col_price", "Your price"), Kind = UiCellKind.Stepper, Width = UiDataTable.StepperWidth },
                    new UiDataColumn { Title = w.T("reallife_ui_shop_col_rival", "Lowest rival"), Kind = UiCellKind.Money, Width = 88f },
                    new UiDataColumn { Title = w.T("reallife_ui_shop_col_state", "State"), Kind = UiCellKind.Chip, Width = 120f }
                },
                Rows = rows.ToArray(), RowCount = rows.Count, Selected = -1, EmptyIcon = UiIcon.Tag,
                EmptyTitle = w.T("reallife_ui_shop_no_lines", "No product on the site"), EmptyBody = w.T("reallife_ui_shop_lines_how",
                    "Products in stock at the building can be put on the site and priced here.")
            };
            m.Left = new[] { new VentBlock { Kind = VentBlockKind.Table, Table = table, RowKeys = keys.ToArray(), RowRuns = steps.ToArray(), Base = UiDataTable.First } };
            // The pick of a row opens its switch beside the table.
            var right = new List<VentBlock>(4);
            ShopLineFact chosen = null;
            for (int i = 0; i < s.Lines.Length; i++)
            {
                chosen = v.Pick == LineKey + s.Lines[i].Item ? s.Lines[i] : chosen;
            }
            if (chosen != null)
            {
                string item = chosen.Item;
                bool on = chosen.On;
                right.Add(VentBlock.Section(chosen.Name));
                right.Add(VentBlock.Switch(w.T("reallife_ui_shop_on_site", "On the site"), on, runs.Add(() => { act.Offer(place, item, !on); return true; })));
                right.Add(VentBlock.Row(w.T("reallife_ui_shop_missed_week", "Asked for and not there, a week"), chosen.MissedWeek.ToString()));
            }
            else
            {
                right.Add(VentBlock.Note(w.T("reallife_ui_shop_pick_line", "Pick a product to put it on the site or take it off. Prices step with the minus and plus.")));
            }
            m.Right = right.ToArray();
        }
    }
}
