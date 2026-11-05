using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class TechLive
    {
        // What a new product has chosen and not yet started: the kind, the segment and the size, kept over the page's writing.
        private static int _newKind = -1, _newSegment = 1, _newScope;
        private static readonly int[] Shares = { 0, 25, 50, 75, 100 };

        private sealed partial class Page
        {
            // ------------------------------------------------------------------------------------------------- the products tab

            private void ProductsTab()
            {
                string pick = _v.Pick;
                if (pick != null)
                {
                    if (Pick(pick, TechApp.NewProductRow) != null)
                    {
                        _firm = _d.FirmOf(Pick(pick, TechApp.NewProductRow));
                        if (_firm != null)
                        {
                            NewProductPage(pick);
                            return;
                        }
                    }
                    else if (Pick(pick, TechApp.ProductRow) != null)
                    {
                        ProductPage(pick);
                        return;
                    }
                    _v.Pick = null;
                }
                var rows = new List<UiDataRow>(16);
                var keys = new List<string>(16);
                for (int i = _f.Products.Count - 1; i >= 0; i--)
                {
                    rows.Add(ProductDataRow(_f.Products[i]));
                    keys.Add("prod:" + _f.Products[i].Id);
                }
                _m.Shape = TechShape.Table;
                _m.Table = ProductTable(rows, TechText.T("reallife_tech_products_none"));
                _m.RowKeys = keys.ToArray();
                var makers = Makers();
                if (makers.Count > 0)
                {
                    string place = makers[0].File.Place;
                    _m.Next = TechText.Fill("reallife_tech_products_room_all", "all", FirmsRules.LiveCount(_f, null).ToString(Inv), "all_most", ProductsMost.ToString(Inv),
                        "firm_most", ProductsPerFirm.ToString(Inv), "kept", FirmsCatalogue.WithdrawnKept.ToString());
                    _m.Actions = new[] { Button(TechText.T("reallife_tech_button_new_product"), UiButtonKind.Primary, UiAction.Alt, Go(v => v.Open("newprod:" + place))) };
                    _newKind = -1;
                }
                else
                {
                    _m.Next = TechText.T("reallife_tech_products_no_maker");
                }
            }

            private List<FirmPlace> Makers()
            {
                var list = new List<FirmPlace>(8);
                IReadOnlyList<FirmPlace> firms = _d.FirmPlaces;
                for (int i = 0; i < firms.Count; i++)
                {
                    if (FirmsCatalogue.MakesProducts[firms[i].File.Kind] != 0)
                    {
                        list.Add(firms[i]);
                    }
                }
                return list;
            }

            private static string State(Product p)
            {
                if (p.Stage == 2)
                {
                    return TechText.T("reallife_tech_state_withdrawn");
                }
                if (p.Stage == 1)
                {
                    return TechText.T("reallife_tech_state_on_sale");
                }
                return FirmsRules.CanLaunch(p) ? TechText.T("reallife_tech_state_ready") : TechText.T("reallife_tech_state_building");
            }

            private static UiRole StateTone(Product p) => p.Stage == 2 ? UiRole.Warn : p.Stage == 1 ? UiRole.Ok : FirmsRules.CanLaunch(p) ? UiRole.Ok : UiRole.Notice;

            private UiDataRow ProductDataRow(Product p)
            {
                FirmPlace firm = _d.FirmOf(p.Place);
                UiDataRow row = Row(8);
                row.Cells[0] = p.Name;
                row.Sub = TechText.ProductName(p.K) + (p.K == ProductKind.Saas ? ", " + TechText.SegmentName(p.S) : string.Empty);
                row.Values[0] = (int)TechGlyphs.Of(TechGlyphs.Product, p.Kind);
                row.Cells[1] = firm != null ? firm.File.Name : "-";
                row.Cells[2] = State(p);
                row.Tones[2] = StateTone(p);
                row.Cells[3] = p.K == ProductKind.Game ? TechText.Mille((int)Math.Min(1000L, (long)p.Progress * 1000 / FirmsCatalogue.GameScope[TalentCatalogue.Clamp(p.Scope, 0, 2)]))
                    : TechText.T("reallife_tech_row_features", "ours", p.Features.ToString(Inv), "theirs", p.RivalFeatures.ToString(Inv));
                row.Cells[4] = TechText.Whole(p.Users);
                row.Values[4] = (int)Math.Min(int.MaxValue, p.Users);
                row.Cells[5] = Cents(Level(FirmsRules.MonthlyRevenue(p)));
                int studio = p.K == ProductKind.Game ? _d.StudioQualityOf(p.Id) : -1;
                int quality = studio >= 0 ? studio : FirmsRules.Quality(p) / 10;
                row.Cells[6] = quality.ToString(Inv);
                row.Values[6] = Math.Max(0, Math.Min(100, quality));
                row.Cells[7] = p.Bugs.ToString(Inv);
                row.Values[7] = p.Bugs;
                return row;
            }

            private static UiDataTableModel ProductTable(List<UiDataRow> rows, string empty) => new UiDataTableModel
            {
                Columns = new[]
                {
                    Col("reallife_tech_col_product", UiCellKind.Tile), Col("reallife_tech_col_firm", UiCellKind.Text, 132f), Col("reallife_ui_tech_col_status", UiCellKind.Chip, 112f),
                    Col("reallife_ui_tech_col_progress", UiCellKind.Text, 96f), Col("reallife_ui_tech_col_customers", UiCellKind.Figure, 84f), Col("reallife_ui_tech_col_month", UiCellKind.Money, 96f),
                    Col("reallife_ui_tech_col_quality", UiCellKind.Figure, 72f, true), Col("reallife_tech_col_bugs", UiCellKind.Figure, 56f)
                },
                Rows = rows.ToArray(), RowCount = rows.Count, Selected = -1, EmptyTitle = empty, EmptyBody = TechText.T("reallife_tech_products_about"), EmptyIcon = UiIcon.Package
            };

            // ------------------------------------------------------------------------------------------------- one product

            private void ProductPage(string pick)
            {
                Product p = int.TryParse(Pick(pick, TechApp.ProductRow), out int id) ? _f.ProductOf(id) : null;
                if (p == null)
                {
                    _v.Pick = null;
                    if (_firm != null) { FirmProducts(); } else { ProductsTab(); }
                    return;
                }
                if (p.K == ProductKind.Game && GamePage(p))
                {
                    return;
                }
                FirmPlace firm = _d.FirmOf(p.Place);
                bool studio = firm != null && _d.StudioRuns(firm.File.K);
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T(_firm != null && !(studio && p.K == ProductKind.Game) ? "reallife_ui_tech_back_firm" : "reallife_tech_tab_products");
                _m.Head = new UiHeadModel
                {
                    Name = p.Name, Icon = TechGlyphs.Of(TechGlyphs.Product, p.Kind), Role = TechText.ProductName(p.K) + (firm != null ? ", " + firm.File.Name : string.Empty),
                    Status = State(p), StatusTone = StateTone(p)
                };
                var actions = new List<UiActionModel>(2);
                int pid = p.Id;
                if (p.Stage < 2)
                {
                    actions.Add(Button(TechText.T("reallife_tech_button_withdraw"), UiButtonKind.Destructive, UiAction.Count,
                        Ask("reallife_tech_confirm_withdraw_title", "reallife_tech_confirm_withdraw_body", () => _d.Withdraw(pid))));
                }
                if (FirmsRules.CanLaunch(p))
                {
                    actions.Add(Button(TechText.T("reallife_tech_button_launch"), UiButtonKind.Primary, UiAction.Alt, Do(() => _d.Launch(pid))));
                }
                _m.HeadActions = actions.ToArray();
                _m.Figures = new[]
                {
                    new UiFigureModel { Label = TechText.T("reallife_tech_col_customers"), Value = TechText.Whole(p.Users) },
                    new UiFigureModel { Label = TechText.T("reallife_ui_tech_col_month"), Value = Cents(Level(FirmsRules.MonthlyRevenue(p))) },
                    new UiFigureModel { Label = TechText.T("reallife_ui_tech_col_quality"), Value = TechText.Mille(FirmsRules.Quality(p)) },
                    new UiFigureModel { Label = TechText.T("reallife_tech_col_bugs"), Value = p.Bugs.ToString(Inv) }
                };
                var now = new TechCard(_runs, TechText.T("reallife_ibox_sec_now"));
                ProductLines(now, p, firm);
                _m.Left = new[] { now.Build() };
                _m.Right = new[] { ProductSettings(p) };
            }

            // The lines the old page wrote for a product, in the order it wrote them.
            private void ProductLines(TechCard card, Product p, FirmPlace firm)
            {
                if (p.K == ProductKind.Game)
                {
                    int scope = FirmsCatalogue.GameScope[TalentCatalogue.Clamp(p.Scope, 0, 2)];
                    card.Say(TechText.Fill("reallife_tech_product_game_line", "scope", TechText.Scope(p.Scope), "done", TechText.Mille((int)Math.Min(1000L, (long)p.Progress * 1000 / scope)),
                        "hype", TechText.Mille(p.Hype), "sold", TechText.Whole(p.Users), "takings", Cents(Level(p.Revenue))));
                }
                else
                {
                    string support = TechText.Fill("reallife_tech_product_sales", "customers", TechText.Whole(p.Users), "free", TechText.Whole(p.Free), "monthly",
                        Cents(Level(FirmsRules.MonthlyRevenue(p))),
                        "support", FirmsRules.SupportNeeded(p).ToString(Inv), "have", firm != null ? firm.Crew.Support.ToString(Inv) : "0");
                    if (_d.SectorOn)
                    {
                        card.Say(TechText.Fill("reallife_tech_mkt_product_build", "features", p.Features.ToString(Inv), "next", (FirmsRules.FeatureCost(p.Features) - p.Progress).ToString(Inv)));
                        card.Say(support);
                    }
                    else
                    {
                        card.Say(TechText.Fill("reallife_tech_product_build", "features", p.Features.ToString(Inv), "rival", FirmsCatalogue.RivalName(FirmsCatalogue.RivalStock(p.K, p.S)),
                            "theirs", p.RivalFeatures.ToString(Inv), "next", (FirmsRules.FeatureCost(p.Features) - p.Progress).ToString(Inv)));
                        if (p.Features >= FirmsCatalogue.FeaturesMost)
                        {
                            card.Say(TechText.Fill("reallife_tech_product_features_full", "most", FirmsCatalogue.FeaturesMost.ToString()), UiRole.WarnText);
                        }
                        card.Say(support);
                        if (p.K == ProductKind.Licence && p.Stage < 2 && firm != null)
                        {
                            long rate = FirmsRules.LicenceRate(FirmsRules.Appeal(p, firm.File, firm.Crew), FirmsRules.CycleSpend[TalentCatalogue.Clamp(HiringGame.Cycle, 0, 3)], firm.Crew.Selling);
                            card.Say(TechText.Fill(firm.Crew.Selling > 0 ? "reallife_tech_product_deals" : "reallife_tech_product_deals_none", "days", FirmsRules.LicenceDealDays(rate).ToString(Inv)));
                        }
                    }
                }
                if (p.K == ProductKind.Model)
                {
                    card.Say(TechText.Fill("reallife_tech_product_training", "done", TechText.Whole(p.Compute), "need", TechText.Whole(InfraRules.TrainNeed(p.Features)), "inference",
                        InfraRules.Inference(p).ToString(Inv)));
                }
                if (_d.SectorOn)
                {
                    card.Say(TechText.Fill("reallife_tech_mkt_product_quality", "quality", TechText.Mille(SectorRules.Soundness(p.Bugs, FirmsRules.Allowance(p))), "bugs", p.Bugs.ToString(Inv)));
                    ViewSection market = _d.SectorMarketSection(p, W);
                    for (int i = 0; market != null && i < market.Lines.Count; i++)
                    {
                        card.Say(Words(market.Lines[i]), Ink(market.Lines[i].Tone));
                    }
                }
                else
                {
                    card.Say(TechText.Fill("reallife_tech_product_quality", "quality", TechText.Mille(FirmsRules.Quality(p)), "bugs", p.Bugs.ToString(Inv),
                        "appeal", firm != null ? TechText.Mille(FirmsRules.Appeal(p, firm.File, firm.Crew)) : "-"));
                }
            }

            // The price, the marketing, an AI model's training and the share of the firm's time, each a step between a minus and a plus.
            private UiFactCardModel ProductSettings(Product p)
            {
                var card = new TechCard(_runs, TechText.T("reallife_ui_tech_settings"));
                if (p.Stage >= 2)
                {
                    return card.Say(TechText.T("reallife_ui_tech_withdrawn_note")).Build();
                }
                int id = p.Id;
                if (p.K != ProductKind.Game)
                {
                    string TierWords(int i) => TechText.Fill("reallife_tech_product_tier", "tier", TechText.Tier(i), "price",
                        Cents(Level(FirmsCatalogue.Price[p.Segment] * (long)FirmsCatalogue.PriceTiers[i] / 1000)));
                    card.Step(TechText.T("reallife_ui_tech_price"), TierWords(p.Tier), null, p.Tier > 0, p.Tier < 2, Do(() => _d.SetProduct(id, p.Tier - 1, -1, -1)),
                        Do(() => _d.SetProduct(id, p.Tier + 1, -1, -1)));
                }
                int levels = FirmsCatalogue.MarketingDay.Length;
                card.Step(TechText.T("reallife_ui_tech_marketing"), TechText.MarketingLevel(p.Marketing), null, p.Marketing > 0, p.Marketing < levels - 1,
                    Do(() => _d.SetProduct(id, -1, p.Marketing - 1, -1)),
                    Do(() => _d.SetProduct(id, -1, p.Marketing + 1, -1)));
                if (p.K == ProductKind.Model)
                {
                    string TrainWords(int i) => TechText.Fill("reallife_tech_product_train", "units", InfraRules.TrainLevels[i].ToString(Inv),
                        "cost", Cents(_d.Level((long)InfraRules.TrainLevels[i] * InfraCatalogue.GpuUnitDay * InfraCatalogue.RentedGpuShare / 1000)));
                    int last = InfraRules.TrainLevels.Length - 1;
                    card.Step(TechText.T("reallife_ui_tech_training"), TrainWords(p.Train), null, p.Train > 0, p.Train < last, Do(() => _d.SetTraining(id, p.Train - 1)),
                        Do(() => _d.SetTraining(id, p.Train + 1)));
                }
                int at = 0;
                for (int i = 0; i < Shares.Length; i++)
                {
                    at = Shares[i] <= p.Share ? i : at;
                }
                card.Step(TechText.T("reallife_ui_tech_time_share"), TechText.T("reallife_tech_product_share", "share", TechText.Percent(Shares[at])), null, at > 0, at < Shares.Length - 1,
                    Do(() => _d.SetProduct(id, -1, -1, Shares[at - 1])), Do(() => _d.SetProduct(id, -1, -1, Shares[at + 1])));
                return card.Build();
            }

            // ------------------------------------------------------------------------------------------------- a new product

            private void NewProductPage(string pick)
            {
                string place = Pick(pick, TechApp.NewProductRow);
                FirmPlace firm = place != null ? _d.FirmOf(place) : null;
                if (firm == null)
                {
                    _v.Pick = null;
                    return;
                }
                Firm m = firm.File;
                if (FirmsRules.Makes(m.K, ProductKind.Game) && _d.StudioRuns(m.K))
                {
                    ConceptPage(firm);
                    return;
                }
                var kinds = new List<int>(4);
                for (int k = 0; k < (int)ProductKind.Count; k++)
                {
                    if (FirmsRules.Makes(m.K, (ProductKind)k))
                    {
                        kinds.Add(k);
                    }
                }
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_ui_tech_back_products");
                _m.Head = new UiHeadModel { Name = TechText.T("reallife_tech_new_title", "firm", m.Name), Icon = UiIcon.Package, Role = TechText.FirmName(m.K) };
                int room = FirmsRules.Room(_f, place, _d.SectorOn);
                var card = new TechCard(_runs, TechText.T("reallife_ui_tech_new_product"));
                card.Say(TechText.Fill("reallife_tech_products_room", "firm", FirmsRules.LiveCount(_f, place).ToString(Inv), "firm_most", ProductsPerFirm.ToString(Inv),
                    "all", FirmsRules.LiveCount(_f, null).ToString(Inv), "all_most", ProductsMost.ToString(Inv)), room == FirmsRules.RoomYes ? UiRole.None : UiRole.WarnText);
                if (room != FirmsRules.RoomYes)
                {
                    card.Say(TechText.T(room == FirmsRules.RoomFirmFull ? "reallife_tech_new_full_firm" : "reallife_tech_new_full_all"), UiRole.WarnText);
                }
                var makers = Makers();
                int maker = makers.IndexOf(firm);
                if (makers.Count > 1 && maker >= 0)
                {
                    card.Step(TechText.T("reallife_tech_col_firm"), m.Name, null, true, true,
                        Go(v => { v.Pick = TechApp.NewProductRow + makers[(maker + makers.Count - 1) % makers.Count].File.Place; _newKind = -1; }),
                        Go(v => { v.Pick = TechApp.NewProductRow + makers[(maker + 1) % makers.Count].File.Place; _newKind = -1; }));
                }
                if (kinds.Count == 0)
                {
                    card.Say(TechText.T("reallife_tech_new_none"));
                    _m.Left = new[] { card.Build() };
                    return;
                }
                int at = Math.Max(0, kinds.IndexOf(_newKind));
                _newKind = kinds[at];
                var kind = (ProductKind)_newKind;
                if (kinds.Count > 1)
                {
                    card.Step(TechText.T("reallife_ui_tech_kind"), TechText.ProductName(kind), null, true, true, Go(v => _newKind = kinds[(at + kinds.Count - 1) % kinds.Count]),
                        Go(v => _newKind = kinds[(at + 1) % kinds.Count]));
                }
                if (kind == ProductKind.Saas)
                {
                    int s = _newSegment;
                    int n = (int)Segment.Count;
                    card.Step(TechText.T("reallife_ui_tech_segment"), TechText.SegmentName((Segment)s), null, true, true, Go(v => _newSegment = (s + n - 1) % n), Go(v => _newSegment = (s + 1) % n));
                }
                if (kind == ProductKind.Game)
                {
                    int s = _newScope;
                    card.Step(TechText.T("reallife_ui_tech_size"), TechText.Scope(s), null, true, true, Go(v => _newScope = (s + 2) % 3), Go(v => _newScope = (s + 1) % 3));
                }
                card.Say(Need(kind));
                card.Say(TechText.T("reallife_tech_new_about_" + kind.ToString().ToLowerInvariant()));
                int segment = _newSegment, scope = _newScope;
                if (room == FirmsRules.RoomYes)
                {
                    _m.HeadActions = new[]
                    {
                        Button(TechText.T("reallife_tech_button_start"), UiButtonKind.Primary, UiAction.Alt, () =>
                        {
                            UiClone.Safe(() =>
                            {
                                Product made = _d.NewProduct(place, kind, (Segment)segment, scope);
                                if (made != null)
                                {
                                    _v.Pick = "prod:" + made.Id;
                                    _v.From = null;
                                    if (_firm == null)
                                    {
                                        _v.Tab = TabProducts;
                                    }
                                }
                            });
                            _live.Touch();
                            return true;
                        })
                    };
                }
                _m.Left = new[] { card.Build() };
            }

            private static string Need(ProductKind kind)
            {
                string features = FirmsCatalogue.LaunchFeatures.ToString();
                switch (kind)
                {
                    case ProductKind.Saas: return TechText.T("reallife_tech_needs_saas", "features", features);
                    case ProductKind.App: return TechText.T("reallife_tech_needs_app", "features", features);
                    case ProductKind.Licence: return TechText.T("reallife_tech_needs_licence", "features", features, "price", TechText.Money(Prices.At(FirmsCatalogue.LicenceDeal / 100)));
                    case ProductKind.Game: return TechText.T("reallife_tech_needs_game", "points", FirmsCatalogue.GameScope[0].ToString());
                    default: return TechText.T("reallife_tech_needs_model");
                }
            }

            // A studio's game begins as a concept: its choices as steps, and the one action that takes it to the desk to be pitched (or greenlights it where no desk runs).
            private void ConceptPage(FirmPlace firm)
            {
                StudioScreen v = _d.StudioConcept(firm, W);
                string place = firm.File.Place;
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_ui_tech_back_products");
                _m.Head = new UiHeadModel { Name = TechText.Fill(v.TitleKey, v.TitlePairs ?? Array.Empty<string>()), Icon = UiIcon.GameController,
                    Role = TechText.T(v.ChipKey ?? "reallife_studio_chip_draft") };
                var left = new List<UiFactCardModel>(3);
                var right = new List<UiFactCardModel>(3);
                for (int i = 0; i < v.Sections.Count; i++)
                {
                    (i == 0 ? left : right).Add(SectionCard(v.Sections[i], place, 0));
                }
                if (v.Next != null)
                {
                    right.Add(new TechCard(_runs).Say(Words(v.Next), Ink(v.Next.Tone)).Build());
                }
                if (v.Primary != null && v.Primary.Enabled)
                {
                    _m.HeadActions = new[] { Button(TechText.T(v.Primary.Key), UiButtonKind.Primary, UiAction.Alt, ActRun(v.Primary, place, 0)) };
                }
                _m.Left = left.ToArray();
                _m.Right = right.ToArray();
            }

            private void Quiet(StringBuilder sb)
            {
                sb.Clear();
            }
        }
    }
}
