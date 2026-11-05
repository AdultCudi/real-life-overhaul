using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Brands pages written from the facts: a brand as a profile with its figures, awareness beside a rival's brand and its campaigns; its settings and the booking of a
    /// campaign; the shops as a table; applicants and their terms; and franchise agreements with their exclusive area, royalties paid on time and disputes. Game-free.
    /// </summary>
    public static class BraPages
    {
        public const int TabBrands = 0, TabShops = 1, TabApplicants = 2, TabAgreements = 3;
        public const int PageProfile = 0, PageSettings = 1, PageCampaign = 2;
        public const string BrandKey = "brand:", ShopKey = "shop:", ApplicantKey = "app:", FranchiseKey = "fr:";
        public const int Radius = 600;

        public static void Fill(VentModel m, VentView v, BraFacts f, IBraActions act, VentWords w)
        {
            m.Title = w.T("reallife_brand_app", "Brands");
            m.Icon = UiIcon.Tag;
            m.Prompts = VenPages.Prompts(w);
            var runs = new VentRuns();
            m.Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.Tag, Label = w.T("reallife_ui_bra_tab_brands", "Your brands"), Count = Count(f.Brands.Length) },
                new UiNavEntry { Icon = UiIcon.Store, Label = w.T("reallife_brand_tab_shops", "Shops"), Count = Count(f.Shops.Length) },
                new UiNavEntry { Icon = UiIcon.People, Label = w.T("reallife_brand_tab_applicants", "Applicants"), Count = Count(f.Applicants.Length) },
                new UiNavEntry { Icon = UiIcon.Handshake, Label = w.T("reallife_brand_tab_agreements", "Agreements"), Count = Count(f.Franchises.Length) }
            };
            m.Tab = Math.Max(0, Math.Min(3, v.Tab));
            if (!f.Ready)
            {
                m.Shape = VentShape.Off;
                m.OffTitle = f.OffTitle ?? w.T("reallife_ui_bra_off_title", "Brands is not running");
                m.OffBody = f.OffBody ?? w.T("reallife_ui_bra_off_body", "Load a save with the economy switched on to grow a brand here.");
                m.Runs = runs.ToArray();
                return;
            }
            switch (m.Tab)
            {
                case TabShops: Shops(m, v, f, act, w, runs); break;
                case TabApplicants: Applicants(m, v, f, act, w, runs); break;
                case TabAgreements: Agreements(m, v, f, act, w, runs); break;
                default: Brands(m, v, f, act, w, runs); break;
            }
            m.Runs = runs.ToArray();
        }

        private static string Count(int n) => n > 0 ? n.ToString() : null;

        private static UiActionModel Back(VentWords w) =>
            new UiActionModel { Label = w.T("reallife_brand_back", "Back"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Back, Key = UiAction.Back,
                Command = UiAppWindow.ActionCommand, Arg = VentFace.BackArg };

        private static UiActionModel Head(string label, UiButtonKind kind, UiIcon icon, UiAction key, int run, bool disabled = false) =>
            new UiActionModel { Label = label, Kind = kind, Icon = icon, Key = key, Command = UiAppWindow.ActionCommand, Arg = run, Disabled = disabled };

        private static string Pct(int hundredths) => (hundredths / 100f).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";

        // ------------------------------------------------------------------------------------------------- Brands

        private static void Brands(VentModel m, VentView v, BraFacts f, IBraActions act, VentWords w, VentRuns runs)
        {
            if (f.Brands.Length == 0)
            {
                m.Empty = f.IntroText ?? w.T("reallife_ui_bra_none",
                    "You have no brand yet. Open the Shops tab, pick one of your shops and make it a brand: its recognition grows with its shops and standards.");
                return;
            }
            BraBrandFact b = f.Brands[0];
            for (int i = 0; i < f.Brands.Length; i++)
            {
                b = v.Pick == BrandKey + f.Brands[i].Id ? f.Brands[i] : b;
            }
            if (v.Page == PageSettings)
            {
                Settings(m, v, b, act, w, runs);
                return;
            }
            if (v.Page == PageCampaign)
            {
                Campaign(m, v, b, act, w, runs);
                return;
            }
            m.Profile = new UiProfileModel
            {
                Name = b.Name, Glyph = UiIcon.Tag, GlyphTint = UiRole.None, Role = b.Type, RoleIcon = UiIcon.Store, Employer = w.F("reallife_ui_bra_shops_badge",
                    "{count} shops", "count", b.Shops.ToString()),
                EmployerIcon = UiIcon.Store, Status = b.Offered ? w.T("reallife_ibox_chip_on_offer", "On offer") : null, StatusTone = UiRole.Ok,
                FactLabels = new[] { w.T("reallife_ui_bra_brand_value", "Brand value"), w.T("reallife_ui_bra_awareness", "Awareness"), w.T("reallife_ui_bra_fees_month",
                    "Fees a month"), w.T("reallife_ui_bra_standards", "Standards") },
                FactValues = new[] { w.Short(b.Value), Pct(b.Rec), w.Money(b.FeesMonth), b.Standards.ToString() },
                RatingLabel = w.T("reallife_ui_bra_rating", "Rating"), Rating = b.Rating
            };
            var heads = new List<UiActionModel>(4);
            if (b.BidFranchise != 0)
            {
                heads.Add(Head(w.F("reallife_ui_bra_sell_bid", "Sell for {amount}", "amount", w.Money(b.BidAmount)), UiButtonKind.Primary, UiIcon.Coin, UiAction.Alt,
                    runs.Add(() => { act.SellBrand(b.BidFranchise); return true; })));
                heads.Insert(0, Head(w.T("reallife_brand_bid_refuse", "Refuse"), UiButtonKind.Destructive, UiIcon.Close, UiAction.Count,
                    runs.Add(() => { act.RefuseBid(b.BidFranchise); return true; })));
            }
            heads.Insert(0, Head(w.T("reallife_ui_bra_new_campaign", "New campaign"), UiButtonKind.Secondary, UiIcon.Megaphone, UiAction.Option,
                runs.Add(() => { v.Page = PageCampaign; v.A = 0; v.B = 0; return true; }), b.Pushing));
            heads.Insert(0, Head(w.T("reallife_ui_bra_settings", "Settings"), UiButtonKind.Secondary, UiIcon.Gear, UiAction.Count, runs.Add(() => { v.Page = PageSettings; return true; })));
            m.HeadActions = heads.ToArray();
            var left = new List<VentBlock>(8);
            var right = new List<VentBlock>(10);
            if (f.Brands.Length > 1)
            {
                int at = Array.IndexOf(f.Brands, b);
                left.Add(VentBlock.Choice(w.T("reallife_ui_bra_brand", "Brand"), b.Name, runs.Add(() => { v.Pick = BrandKey + f.Brands[(at + 1) % f.Brands.Length].Id; return true; })));
            }
            left.Add(VentBlock.Section(w.T("reallife_ui_bra_yesterday", "Yesterday")));
            left.Add(VentBlock.Row(w.F("reallife_ui_bra_sales_all", "Sales, all {count} shops", "count", b.Shops.ToString()), w.Money(b.SalesYesterday)));
            left.Add(VentBlock.Row(w.T("reallife_ui_bra_sales_fr", "of which franchised shops"), w.Money(b.FranchiseSales)));
            left.Add(VentBlock.Row(w.T("reallife_ui_bra_royalties", "Royalties and levy from franchisees"), w.Money(b.Royalty)));
            left.Add(VentBlock.Row(w.T("reallife_ui_bra_net", "Left after support"), w.Money(b.Net), UiRole.OkText));
            left.Add(VentBlock.Row(w.T("reallife_ui_bra_earned", "Royalties earned in all"), w.Money(b.Earned)));
            AddAwareness(left, b, w);
            right.Add(VentBlock.Section(w.T("reallife_ui_bra_campaigns", "Campaigns")));
            if (b.Pushing)
            {
                right.Add(new VentBlock { Kind = VentBlockKind.Items, Items = new[] { new VentItem { Icon = UiIcon.Megaphone, Title = b.PushLine, Run = -1 } } });
            }
            else
            {
                right.Add(VentBlock.Note(w.T("reallife_ui_bra_no_campaign", "No campaign is running. A campaign lifts recognition for a few weeks.")));
            }
            right.Add(VentBlock.Section(w.T("reallife_ui_bra_districts", "Recognition by district")));
            for (int i = 0; i < b.Districts.Length; i++)
            {
                right.Add(VentBlock.Meter(b.Districts[i].Name, Math.Min(100, b.Districts[i].Rec / 100), Pct(b.Districts[i].Rec)));
            }
            right.Add(VentBlock.Section(w.T("reallife_ui_bra_standards_sec", "Standards")));
            right.Add(VentBlock.Meter(w.T("reallife_ui_bra_standards", "Standards"), b.Standards, b.Standards.ToString()));
            right.Add(VentBlock.Row(w.T("reallife_ibox_minimum", "Required minimum"), b.Minimum > 0 ? b.Minimum.ToString() : "-"));
            if (b.BestShop != null)
            {
                right.Add(VentBlock.Note(w.F("reallife_ui_bra_worst", "The shop furthest under the brand is {business}.", "business", b.BestShop)));
            }
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static void AddAwareness(List<VentBlock> into, BraBrandFact b, VentWords w)
        {
            into.Add(VentBlock.Section(w.T("reallife_ui_bra_aware_sec", "Awareness, 12 weeks")));
            if (b.Mine.Length < 2)
            {
                into.Add(VentBlock.Note(w.T("reallife_ui_bra_aware_none", "Not yet: awareness is kept week by week, and the line draws from the second week.")));
                return;
            }
            int n = b.Mine.Length;
            var mine = new float[n];
            var rival = new float[n];
            for (int i = 0; i < n; i++)
            {
                mine[i] = b.Mine[i] / 100f;
                rival[i] = i < b.Rival.Length ? b.Rival[i] / 100f : 0f;
            }
            var series = new List<UiSeries> { new UiSeries { Label = b.Name, Values = mine, Tint = UiRole.None } };
            if (!string.IsNullOrEmpty(b.RivalName))
            {
                series.Add(new UiSeries { Label = b.RivalName, Values = rival, Tint = UiRole.None });
            }
            into.Add(new VentBlock
            {
                Kind = VentBlockKind.Chart, ChartHeight = 176f,
                Chart = new UiChartModel { Kind = UiChartKind.Line, Points = b.WeekNames, Series = series.ToArray(), Format = x => (int)x + "%",
                    AxisFormat = x => (int)x + "%", Still = true, Whole = true }
            });
        }

        private static void Settings(VentModel m, VentView v, BraBrandFact b, IBraActions act, VentWords w, VentRuns runs)
        {
            m.Sub = true;
            m.Next = b.Name;
            m.HeadActions = new[] { Back(w), Head(w.T("reallife_brand_set_rename", "Rename"), UiButtonKind.Secondary, UiIcon.Pencil, UiAction.Option,
                runs.Add(() => { act.Rename(b.Id); return true; })) };
            var left = new List<VentBlock>(10);
            var right = new List<VentBlock>(10);
            int step = (b.MinimumStep + 1) % Math.Max(1, b.MinimumSteps);
            left.Add(VentBlock.Section(w.T("reallife_ibox_minimum", "Required minimum")));
            left.Add(VentBlock.Note(w.T("reallife_ui_bra_minimum_about", "The standards a franchised shop must keep: under them a shop is warned.")));
            left.Add(VentBlock.Choice(w.T("reallife_ui_bra_minimum", "Minimum standards"), b.Minimum > 0 ? b.Minimum.ToString() : "-", runs.Add(() => { act.Minimum(b.Id,
                step); return true; })));
            left.Add(VentBlock.Section(w.T("reallife_ibox_sec_franchise", "Franchise")));
            left.Add(VentBlock.Note(b.OfferLine));
            left.Add(VentBlock.Switch(w.T("reallife_ui_bra_offer", "Offer it as a franchise"), b.Offered, runs.Add(() => { act.Offer(b.Id, !b.Offered); return true; }),
                !b.Offered && !b.CanOffer));
            right.Add(VentBlock.Section(w.T("reallife_ibox_sec_shared", "Shared by the shops")));
            right.Add(VentBlock.Note(w.F("reallife_ui_bra_shared", "Member shops can follow {business}: its prices and its hours.", "business", b.FlagshipName)));
            right.Add(VentBlock.Switch(w.T("reallife_ui_bra_keep", "Keep member prices on the flagship's"), b.Keep, runs.Add(() => { act.KeepPrices(b.Id, !b.Keep); return true; })));
            if (b.Keep)
            {
                right.Add(VentBlock.Switch(w.T("reallife_ui_bra_local", "Adjust each for its district"), b.Local, runs.Add(() => { act.KeepLocal(b.Id, !b.Local); return true; })));
            }
            var acts = new List<UiActionModel>(3);
            acts.Add(new UiActionModel { Label = w.T("reallife_brand_set_give_up", "Give up the brand"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Trash,
                Arg = runs.Add(() => { act.GiveUp(b.Id); v.Page = PageProfile; return true; }) });
            acts.Add(new UiActionModel { Label = w.T("reallife_brand_set_hours", "Share hours"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Clock,
                Arg = runs.Add(() => { act.ShareHours(b.Id); return true; }) });
            if (!b.Keep)
            {
                acts.Add(new UiActionModel { Label = w.T("reallife_brand_set_prices", "Share prices now"), Kind = UiButtonKind.Primary, Icon = UiIcon.Tag, Key = UiAction.Alt,
                    Arg = runs.Add(() => { act.SharePrices(b.Id); return true; }) });
            }
            right.Add(VentBlock.Buttons(acts.ToArray()));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static void Campaign(VentModel m, VentView v, BraBrandFact b, IBraActions act, VentWords w, VentRuns runs)
        {
            m.Sub = true;
            m.Next = w.F("reallife_ui_bra_campaign_for", "A campaign for {brand}", "brand", b.Name);
            m.HeadActions = new[] { Back(w) };
            int kinds = Math.Max(1, b.PushKinds.Length);
            v.A = Math.Max(0, Math.Min(kinds - 1, v.A));
            int districts = Math.Max(1, b.PushDistricts.Length);
            v.B = Math.Max(0, Math.Min(districts - 1, v.B));
            bool local = v.A == 0 && b.PushDistricts.Length > 0;
            var left = new List<VentBlock>(8);
            left.Add(VentBlock.Section(w.T("reallife_ibox_sec_campaign", "Campaign")));
            left.Add(VentBlock.Note(w.T("reallife_ui_bra_campaign_about",
                "A local campaign lifts one district, a city-wide one every district with a shop, and creators on Flare reach the city through local posts.")));
            left.Add(VentBlock.Choice(w.T("reallife_ui_bra_kind", "Kind"), b.PushKinds.Length > 0 ? b.PushKinds[v.A] : "-", runs.Add(() => { v.A = (v.A + 1) % kinds; return true; })));
            if (local)
            {
                left.Add(VentBlock.Choice(w.T("reallife_ui_bra_district", "District"), b.PushDistricts[v.B], runs.Add(() => { v.B = (v.B + 1) % districts; return true; })));
            }
            var right = new List<VentBlock>(6);
            right.Add(VentBlock.Section(w.T("reallife_ui_bra_quote", "What it costs")));
            long cost = b.PushCosts.Length > v.A ? b.PushCosts[v.A] : 0;
            int days = b.PushDays.Length > v.A ? b.PushDays[v.A] : 0;
            right.Add(VentBlock.Row(w.T("reallife_ui_bra_cost", "Cost"), w.Money(cost)));
            right.Add(VentBlock.Row(w.T("reallife_ui_bra_days", "Runs for"), w.F("reallife_ui_bra_days_n", "{days} days", "days", days.ToString())));
            right.Add(VentBlock.Row(w.T("reallife_ui_bra_fund", "Levy in the fund"), w.Money(b.Fund)));
            int kind = v.A, district = v.B;
            right.Add(VentBlock.Buttons(new UiActionModel
            {
                Label = w.T("reallife_brand_push_book", "Book it"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt, Disabled = b.Pushing,
                Arg = runs.Add(() => { act.BookPush(b.Id, kind, district); v.Page = PageProfile; return true; })
            }));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        // ------------------------------------------------------------------------------------------------- Shops

        private static void Shops(VentModel m, VentView v, BraFacts f, IBraActions act, VentWords w, VentRuns runs)
        {
            var rows = new List<UiDataRow>(f.Shops.Length);
            var keys = new List<string>(f.Shops.Length);
            var locate = new List<int>(f.Shops.Length);
            int selected = -1;
            BraShopFact chosen = null;
            for (int i = 0; i < f.Shops.Length; i++)
            {
                BraShopFact s = f.Shops[i];
                string place = s.Place;
                rows.Add(new UiDataRow
                {
                    Cells = new[] { s.Name, s.Owner, s.Brand ?? string.Empty, s.SalesDay > 0 ? w.Money(s.SalesDay) : "-",
                        s.Rating >= 0 ? (s.Rating / 2f).ToString("0.0") : "-", s.Status, string.Empty },
                    Values = new[] { (int)UiIcon.Store, s.Mine ? UiDataTable.GlyphFace(UiIcon.Person) : 0, 0, 0, s.Rating, 0, (int)UiIcon.Locate },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, s.Kind == "plain" ? UiRole.Notice : UiRole.Ok, UiRole.None },
                    Subs = new[] { s.District, null, null, null, null, null, null }, Hue = VentWords.HueOf(s.Owner)
                });
                keys.Add(s.Key);
                locate.Add(runs.Add(() => { act.Locate(place); return true; }));
                if (v.Pick == s.Key)
                {
                    selected = i;
                    chosen = s;
                }
            }
            var table = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_shop", "Shop"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_owner", "Owner"), Kind = UiCellKind.Person, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_brand", "Brand"), Kind = UiCellKind.Text, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_day", "A day"), Kind = UiCellKind.Money, Width = 104f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_rating", "Rating"), Kind = UiCellKind.Stars, Width = 120f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_status", "Status"), Kind = UiCellKind.Chip, Width = 144f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_map", "On the map"), Kind = UiCellKind.Action, Width = 72f }
                },
                Rows = rows.ToArray(), RowCount = rows.Count, Selected = selected, EmptyIcon = UiIcon.Store,
                EmptyTitle = w.T("reallife_ui_bra_no_shops", "No shop yet"), EmptyBody = w.T("reallife_brand_row_no_brand", "Open a shop of your own, then make it a brand here.")
            };
            m.HeroFigures = new[]
            {
                new UiFigureModel { Label = w.T("reallife_ui_bra_fig_shops", "Shops"), Value = f.Shops.Length.ToString() },
                new UiFigureModel { Label = w.T("reallife_ui_bra_fig_sales", "Sales yesterday"), Value = w.Money(SumSales(f)) },
                new UiFigureModel { Label = w.T("reallife_ui_bra_fig_waiting", "Waiting for your answer"), Value = f.WaitingAnswers.ToString() }
            };
            m.Left = new[] { new VentBlock { Kind = VentBlockKind.Table, Table = table, RowKeys = keys.ToArray(), RowRuns = locate.ToArray(), Base = UiDataTable.First } };
            var right = new List<VentBlock>(8);
            if (chosen != null)
            {
                ShopDetail(right, chosen, f, act, w, runs, v);
            }
            else
            {
                right.Add(VentBlock.Note(w.T("reallife_ui_bra_shops_pick", "Pick a shop to see its standards, what it asks of you and what you can do with it.")));
            }
            m.Right = right.ToArray();
        }

        private static long SumSales(BraFacts f)
        {
            long sum = 0;
            for (int i = 0; i < f.Shops.Length; i++)
            {
                sum += f.Shops[i].SalesDay;
            }
            return sum;
        }

        private static void ShopDetail(List<VentBlock> r, BraShopFact s, BraFacts f, IBraActions act, VentWords w, VentRuns runs, VentView v)
        {
            r.Add(VentBlock.Section(s.Name));
            r.Add(VentBlock.Row(w.T("reallife_ui_bra_col_owner", "Owner"), s.Owner));
            r.Add(VentBlock.Row(w.T("reallife_ui_bra_col_status", "Status"), s.Status));
            if (s.Standards >= 0)
            {
                r.Add(VentBlock.Meter(w.T("reallife_ui_bra_standards", "Standards"), s.Standards, s.Standards.ToString()));
            }
            if (!string.IsNullOrEmpty(s.Line))
            {
                r.Add(VentBlock.Note(s.Line));
            }
            var acts = new List<UiActionModel>(4);
            string place = s.Place;
            if (s.Kind == "member")
            {
                acts.Add(new UiActionModel { Label = w.T("reallife_ui_bra_leave", "Leave the brand"), Kind = UiButtonKind.Destructive, Icon = UiIcon.SignOut,
                    Arg = runs.Add(() => { act.Leave(place); return true; }) });
            }
            else if (s.Kind == "franchise")
            {
                int id = s.FranchiseId;
                acts.Add(new UiActionModel
                {
                    Label = w.T("reallife_ui_bra_open_agreement", "Open the agreement"), Kind = UiButtonKind.Primary, Icon = UiIcon.Handshake, Key = UiAction.Alt,
                    Arg = runs.Add(() => { v.Tab = TabAgreements; v.Pick = FranchiseKey + id; return true; })
                });
            }
            else if (s.Kind == "chain")
            {
                r.Add(VentBlock.Row(w.T("reallife_ibox_leaving_costs", "Leaving costs"), w.Money(s.ChainExit)));
                if (s.CanKeep)
                {
                    r.Add(VentBlock.Switch(w.T("reallife_ui_bra_chain_keep", "Keep prices inside the chain's range"), s.Kept, runs.Add(() => { act.KeepChain(place,
                        !s.Kept); return true; })));
                }
                acts.Add(new UiActionModel { Label = w.T("reallife_ui_bra_chain_leave", "Leave the chain"), Kind = UiButtonKind.Destructive, Icon = UiIcon.SignOut,
                    Arg = runs.Add(() => { act.LeaveChain(place); return true; }) });
                acts.Add(new UiActionModel { Label = w.T("reallife_brand_chain_prices", "Put prices in range"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Tag,
                    Arg = runs.Add(() => { act.ChainPrices(place); return true; }) });
            }
            else
            {
                acts.Add(new UiActionModel
                {
                    Label = w.T("reallife_brand_plain_make", "Make it a brand"), Kind = UiButtonKind.Primary, Icon = UiIcon.Tag, Key = UiAction.Alt,
                        Arg = runs.Add(() => { act.MakeBrand(place); return true; })
                });
                for (int i = 0; i < f.JoinBrands.Length && i < 2; i++)
                {
                    int brand = f.JoinBrandIds[i];
                    acts.Add(new UiActionModel
                    {
                        Label = w.F("reallife_ui_bra_join", "Join {brand}", "brand", f.JoinBrands[i]), Kind = UiButtonKind.Secondary, Icon = UiIcon.Plus,
                            Arg = runs.Add(() => { act.JoinBrand(brand, place); return true; })
                    });
                }
                r.Add(VentBlock.Section(w.T("reallife_ibox_sec_chain", "Or take a city chain")));
                for (int i = 0; i < f.ChainNames.Length; i++)
                {
                    int chain = i;
                    r.Add(new VentBlock
                    {
                        Kind = VentBlockKind.Items,
                        Items = new[] { new VentItem { Icon = UiIcon.Store, Title = f.ChainNames[i], Amount = w.Money(f.ChainFees[i]),
                            Run = runs.Add(() => { act.TakeChain(place, chain); return true; }) } }
                    });
                }
            }
            if (acts.Count > 0)
            {
                r.Add(VentBlock.Buttons(acts.ToArray()));
            }
        }

        // ------------------------------------------------------------------------------------------------- Applicants

        private static void Applicants(VentModel m, VentView v, BraFacts f, IBraActions act, VentWords w, VentRuns runs)
        {
            var rows = new List<UiDataRow>(f.Applicants.Length);
            var keys = new List<string>(f.Applicants.Length);
            int selected = -1;
            BraApplicantFact chosen = null;
            for (int i = 0; i < f.Applicants.Length; i++)
            {
                BraApplicantFact a = f.Applicants[i];
                rows.Add(new UiDataRow
                {
                    Cells = new[] { a.Name, a.Brand, a.District, a.Temper, w.Money(a.Capital), a.Experience.ToString(), w.F("reallife_ui_bra_in_days", "In {days} days",
                        "days", a.DaysLeft.ToString()) },
                    Values = new[] { 0, 0, 0, 0, 0, 0, 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, a.DaysLeft <= 2 ? UiRole.Warn : UiRole.Notice },
                    Subs = new[] { a.Second ? w.T("reallife_ui_bra_second_shop", "Asks for a second shop") : null, null, null, null, null, null, null }, Hue = VentWords.HueOf(a.Name)
                });
                keys.Add(ApplicantKey + a.Id);
                if (v.Pick == ApplicantKey + a.Id)
                {
                    selected = i;
                    chosen = a;
                }
            }
            var table = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_applicant", "Applicant"), Kind = UiCellKind.Person, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_brand", "Brand"), Kind = UiCellKind.Text, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_district", "District"), Kind = UiCellKind.Text, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_temper", "Temper"), Kind = UiCellKind.Text, Width = 128f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_capital", "Capital"), Kind = UiCellKind.Money, Width = 104f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_years", "Years in trade"), Kind = UiCellKind.Figure, Width = 96f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_waits", "Waits"), Kind = UiCellKind.Chip, Width = 144f }
                },
                Rows = rows.ToArray(), RowCount = rows.Count, Selected = selected, EmptyIcon = UiIcon.People,
                EmptyTitle = w.T("reallife_brand_row_no_applicant", "Nobody has applied yet"),
                EmptyBody = w.T("reallife_ui_bra_applicants_how",
                    "People come to a brand that is offered as a franchise, with recognition and shops that stand up. Offer one in its settings.")
            };
            m.Left = new[] { new VentBlock { Kind = VentBlockKind.Table, Table = table, RowKeys = keys.ToArray(), Base = UiDataTable.First } };
            var right = new List<VentBlock>(14);
            if (chosen != null)
            {
                ApplicantDetail(right, chosen, act, w, runs, v);
            }
            else
            {
                right.Add(VentBlock.Note(w.T("reallife_ui_bra_applicant_pick", "Pick an applicant to read their record and set the terms of an agreement.")));
            }
            m.Right = right.ToArray();
        }

        private static void ApplicantDetail(List<VentBlock> r, BraApplicantFact a, IBraActions act, VentWords w, VentRuns runs, VentView v)
        {
            r.Add(new VentBlock
            {
                Kind = VentBlockKind.Items,
                Items = new[] { new VentItem { Face = a.Name, Hue = VentWords.HueOf(a.Name), Title = a.Name, Sub = a.Wants, Chip = a.Temper, ChipTone = UiRole.Notice, Run = -1 } }
            });
            r.Add(VentBlock.Row(w.T("reallife_ui_bra_col_capital", "Capital"), w.Money(a.Capital)));
            if (!a.Second)
            {
                r.Add(VentBlock.Row(w.T("reallife_ui_bra_col_years", "Years in trade"), a.Experience.ToString()));
                if (a.ShopsRun > 0)
                {
                    r.Add(VentBlock.Row(w.T("reallife_ui_bra_shops_run", "Shops run before, of them closed"), a.ShopsRun + ", " + a.ShopsClosed));
                }
            }
            r.Add(VentBlock.Note(a.TemperAbout));
            int sites = Math.Max(1, a.Sites.Length);
            int site = Math.Max(0, Math.Min(sites - 1, v.A));
            int length = Math.Max(0, Math.Min(Math.Max(1, a.Lengths.Length) - 1, v.B));
            int packed = v.C;
            int fee = packed & 3, royalty = (packed >> 2) & 3, levy = (packed >> 4) & 3;
            bool exclusive = ((packed >> 6) & 1) != 0;
            r.Add(VentBlock.Section(w.T("reallife_ibox_sec_terms", "Terms")));
            if (a.Sites.Length > 0)
            {
                r.Add(VentBlock.Choice(w.T("reallife_ui_bra_site", "Where"), a.Sites[site], runs.Add(() => { v.A = (site + 1) % sites; return true; })));
            }
            else
            {
                r.Add(VentBlock.Note(w.T("reallife_brand_deal_no_site", "There is no free building in their district to open in."), UiRole.Warn));
            }
            r.Add(VentBlock.Choice(w.T("reallife_ui_bra_length", "Length"), a.Lengths[length], runs.Add(() => { v.B = (length + 1) % a.Lengths.Length; return true; })));
            r.Add(VentBlock.Choice(w.T("reallife_ui_bra_fee", "Entry fee"), a.Fees[fee], runs.Add(() => { v.C = (packed & ~3) | ((fee + 1) % 3); return true; })));
            r.Add(VentBlock.Choice(w.T("reallife_ui_bra_royalty", "Royalty"), a.Royalties[royalty],
                runs.Add(() => { v.C = (packed & ~(3 << 2)) | (((royalty + 1) % 3) << 2); return true; })));
            r.Add(VentBlock.Choice(w.T("reallife_ui_bra_levy", "Marketing levy"), a.Levies[levy], runs.Add(() => { v.C = (packed & ~(3 << 4)) | (((levy + 1) % 3) << 4); return true; })));
            r.Add(VentBlock.Switch(w.F("reallife_ui_bra_exclusive", "Exclusive area, {metres} m round the shop", "metres", Radius.ToString()), exclusive,
                runs.Add(() => { v.C = packed ^ (1 << 6); return true; })));
            r.Add(VentBlock.Note(a.Answer, a.Accepts ? UiRole.None : UiRole.Warn));
            int id = a.Id;
            r.Add(VentBlock.Buttons(
                new UiActionModel { Label = w.T("reallife_brand_app_refuse", "Refuse"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Close,
                    Arg = runs.Add(() => { act.Refuse(id); v.Pick = null; return true; }) },
                new UiActionModel
                {
                    Label = w.T("reallife_brand_deal_sign", "Sign the agreement"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt,
                        Disabled = !a.Accepts || (a.Sites.Length == 0 && !a.Second),
                    Arg = runs.Add(() => { act.Sign(id, a.Sites.Length > 0 ? site : -1, fee, royalty, levy, exclusive, length); v.Pick = null; return true; })
                }));
        }

        // ------------------------------------------------------------------------------------------------- Agreements

        private static void Agreements(VentModel m, VentView v, BraFacts f, IBraActions act, VentWords w, VentRuns runs)
        {
            var rows = new List<UiDataRow>(f.Franchises.Length);
            var keys = new List<string>(f.Franchises.Length);
            int selected = -1;
            BraFranchiseFact chosen = null;
            for (int i = 0; i < f.Franchises.Length; i++)
            {
                BraFranchiseFact a = f.Franchises[i];
                rows.Add(new UiDataRow
                {
                    Cells = new[] { a.Franchisee, a.Name, a.Brand, a.District, w.Money(a.Royalty), w.Money(a.Paid), a.Until, a.Status },
                    Values = new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None,
                        a.Ask != 0 ? UiRole.Danger : a.Trouble ? UiRole.Warn : UiRole.Ok },
                    Subs = new string[8], Hue = VentWords.HueOf(a.Franchisee)
                });
                keys.Add(FranchiseKey + a.Id);
                if (v.Pick == FranchiseKey + a.Id)
                {
                    selected = i;
                    chosen = a;
                }
            }
            var table = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_franchisee", "Franchisee"), Kind = UiCellKind.Person, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_shop", "Shop"), Kind = UiCellKind.Text, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_brand", "Brand"), Kind = UiCellKind.Text, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_district", "District"), Kind = UiCellKind.Text, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_royalty_day", "Royalty a day"), Kind = UiCellKind.Money, Width = 104f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_paid_all", "Paid in all"), Kind = UiCellKind.Money, Width = 104f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_ends", "Ends"), Kind = UiCellKind.Text, Width = 96f },
                    new UiDataColumn { Title = w.T("reallife_ui_bra_col_status", "Status"), Kind = UiCellKind.Chip, Width = 160f }
                },
                Rows = rows.ToArray(), RowCount = rows.Count, Selected = selected, EmptyIcon = UiIcon.Handshake,
                EmptyTitle = w.T("reallife_ui_bra_no_agreements", "No agreements yet"),
                EmptyBody = w.T("reallife_ui_bra_agreements_how",
                    "An agreement starts when you sign an applicant. It shows the terms, the standards checks and the royalties paid month by month.")
            };
            m.Left = new[] { new VentBlock { Kind = VentBlockKind.Table, Table = table, RowKeys = keys.ToArray(), Base = UiDataTable.First } };
            var right = new List<VentBlock>(16);
            if (chosen != null)
            {
                AgreementDetail(right, chosen, act, w, runs, v);
            }
            else
            {
                right.Add(VentBlock.Note(w.T("reallife_ui_bra_agreement_pick", "Pick an agreement to read it, check the shop's standards or answer what the franchisee asks.")));
            }
            m.Right = right.ToArray();
        }

        private static void AgreementDetail(List<VentBlock> r, BraFranchiseFact a, IBraActions act, VentWords w, VentRuns runs, VentView v)
        {
            r.Add(new VentBlock
            {
                Kind = VentBlockKind.Items,
                Items = new[] { new VentItem { Face = a.Franchisee, Hue = VentWords.HueOf(a.Franchisee), Title = a.Name, Sub = a.Franchisee + ", " + a.Brand, Chip = a.Status,
                    ChipTone = a.Ask != 0 ? UiRole.Danger : UiRole.Ok, Run = -1 } }
            });
            r.Add(VentBlock.Figures(
                new UiFigureModel { Label = w.T("reallife_ui_bra_sales_month", "Sales a month"), Value = w.Money(a.Takings * 30),
                    Change = a.Target > 0 ? (a.Overs >= 0 ? "+" : string.Empty) + a.Overs + "%" : null, Rising = a.Overs >= 0, Good = a.Overs >= 0 },
                new UiFigureModel { Label = w.T("reallife_ui_bra_target", "Target"), Value = a.Target > 0 ? w.Money(a.Target) : "-" },
                new UiFigureModel { Label = w.T("reallife_ui_bra_royalty_day", "Royalty and levy a day"), Value = w.Money(a.Royalty) }));
            r.Add(VentBlock.Section(w.T("reallife_ibox_sec_terms", "Terms")));
            r.Add(VentBlock.Row(w.T("reallife_ui_bra_agreement_terms", "Fee, royalty, levy"), a.Terms));
            r.Add(VentBlock.Row(w.T("reallife_ui_bra_exclusive_area", "Exclusive area"), a.ExclusiveLine));
            r.Add(VentBlock.Row(w.T("reallife_ui_bra_col_ends", "Ends"), a.Until));
            r.Add(VentBlock.Section(w.T("reallife_ui_bra_standards_check", "Standards check")));
            r.Add(VentBlock.Note(a.StandardsLine));
            r.Add(VentBlock.Steps(w.F("reallife_ui_bra_royalties_ring", "Royalties paid on time, {n} of {m} months", "n", a.OnTime.ToString(), "m", a.Months.ToString()),
                Math.Max(1, a.Months), a.OnTime));
            if (!string.IsNullOrEmpty(a.AskLine))
            {
                r.Add(VentBlock.Note(a.AskLine, UiRole.Warn));
            }
            if (!string.IsNullOrEmpty(a.RebateLine))
            {
                r.Add(VentBlock.Note(a.RebateLine, UiRole.Warn));
            }
            if (!string.IsNullOrEmpty(a.CourtLine))
            {
                r.Add(VentBlock.Note(a.CourtLine, UiRole.Warn));
            }
            int id = a.Id;
            var acts = new List<UiActionModel>(6);
            acts.Add(new UiActionModel { Label = w.T("reallife_ui_bra_end", "End the agreement"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Close,
                Arg = runs.Add(() => { act.End(id); v.Pick = null; return true; }) });
            acts.Add(new UiActionModel { Label = w.T("reallife_brand_fr_retrain", "Retrain"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Cap,
                Arg = runs.Add(() => { act.Retrain(id); return true; }) });
            acts.Add(new UiActionModel { Label = w.T("reallife_brand_fr_warn", "Warn"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Warning,
                Arg = runs.Add(() => { act.Warn(id); return true; }), Disabled = a.Seen < 0 || a.Seen >= a.Minimum });
            if (a.Ask != 0 && a.CanDecline)
            {
                acts.Add(new UiActionModel { Label = w.T("reallife_brand_fr_decline", "Decline"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Close,
                    Arg = runs.Add(() => { act.Decline(id); return true; }) });
            }
            if (a.Ask != 0 && a.CanWaive)
            {
                acts.Add(new UiActionModel { Label = w.T("reallife_brand_fr_waive", "Waive royalties"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Coin,
                    Arg = runs.Add(() => { act.Waive(id); return true; }) });
            }
            if (a.Ask != 0 && a.CanAgree)
            {
                acts.Add(new UiActionModel
                {
                    Label = a.Ask == 4 ? w.T("reallife_brand_fr_give_way", "Give way") : w.T("reallife_brand_fr_agree", "Agree"), Kind = UiButtonKind.Primary,
                        Icon = UiIcon.Check, Key = UiAction.Alt,
                    Arg = runs.Add(() => { act.Agree(id); return true; })
                });
            }
            else
            {
                acts.Add(new UiActionModel { Label = w.T("reallife_brand_fr_inspect_send", "Inspect"), Kind = UiButtonKind.Primary, Icon = UiIcon.ShieldCheck,
                    Key = UiAction.Alt, Arg = runs.Add(() => { act.Inspect(id); return true; }) });
            }
            r.Add(VentBlock.Buttons(acts.ToArray()));
        }
    }
}
