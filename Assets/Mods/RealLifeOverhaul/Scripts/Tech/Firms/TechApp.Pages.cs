using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The Tech app's tabs and the page of the one thing picked on each.</summary>
    public sealed partial class TechApp
    {
        private static readonly int[] Shares = { 0, 25, 50, 75, 100 };
        private static readonly int[] Bids = { 800, 900, 1000, 1100, 1200 };

        private int _newKind = -1, _newSegment = 1, _newScope, _bid = 2, _bidFirm;
        private readonly List<FirmPlace> _takers = new List<FirmPlace>(8);

        private static long Level(long cents) => cents * Prices.Level / 1000;

        // The Guide's pages the tabs point to at their foot: the tech sector, and startups and venture capital.
        private const string SectorGuide = "reallife_meta_title_tech_sector", StartupsGuide = "reallife_meta_title_tech_startups";

        // ---------------------------------------------------------------- firms

        private TableSpec WriteFirms(InfoBox box, FirmsFile f, int day)
        {
            IReadOnlyList<FirmPlace> firms = _director.FirmPlaces;
            FirmPlace picked = null;
            string id = Picked(_picked, FirmRow), newFor = Picked(_picked, NewProductRow);
            for (int i = 0; i < firms.Count; i++)
            {
                FirmPlace firm = firms[i];
                Firm m = firm.File;
                int products = 0, contracts = 0;
                for (int p = 0; p < f.Products.Count; p++)
                {
                    products += f.Products[p].Place == m.Place && f.Products[p].Stage < 2 ? 1 : 0;
                }
                for (int c = 0; c < f.Contracts.Count; c++)
                {
                    contracts += f.Contracts[c].Place == m.Place && f.Contracts[c].Stage == (int)WorkStage.Active ? 1 : 0;
                }
                TableRow row = Row(FirmRow + m.Place);
                row.Set(0, m.Name);
                row.Set(1, TechText.FirmName(m.K));
                row.Set(2, firm.People.ToString(), firm.People);
                row.Set(3, firm.Crew.Engineering.ToString(), firm.Crew.Engineering);
                row.Set(4, products.ToString(), products);
                row.Set(5, contracts.ToString(), contracts);
                Cents(row, 6, m.Revenue30);
                Cents(row, 7, Level(FirmsRules.Arr(f, m.Place) / 12));
                row.Set(8, TechText.Mille(m.Reputation), m.Reputation, m.Reputation < 350 ? TableRow.Red : TableRow.Plain);
                picked = m.Place == id || m.Place == newFor ? firm : picked;
            }
            if (_rows.Count == 0)
            {
                Say(TechText.T("reallife_tech_row_no_firm"));
            }
            if (picked != null && newFor != null)
            {
                WriteNewProduct(box, picked);
            }
            else if (picked != null)
            {
                WriteFirm(box, f, picked, day);
            }
            else
            {
                _picked = null;
                box.Title(TechText.T("reallife_tech_path_title"));
                Path(box, f);
                Meta.Guide.Point(box, SectorGuide);
            }
            return TechColumns.Firms;
        }

        /// <summary>The way from a studio up, one line a step: done, or what it still needs.</summary>
        private void Path(InfoBox box, FirmsFile f)
        {
            int studios = 0, live = 0, people = 0;
            long monthly = 0;
            IReadOnlyList<FirmPlace> firms = _director.FirmPlaces;
            for (int i = 0; i < firms.Count; i++)
            {
                studios += firms[i].File.Kind <= (int)FirmKind.AiLab ? 1 : 0;
                people += firms[i].People;
                monthly += Level(FirmsRules.Arr(f, firms[i].File.Place) / 12);
            }
            for (int i = 0; i < f.Products.Count; i++)
            {
                live += f.Products[i].Live ? 1 : 0;
            }
            long scale = Level(TechPath.ScaleUpMonthly);
            Step(box, studios > 0, "reallife_tech_path_studio", null);
            Step(box, live > 0, "reallife_tech_path_product", null);
            Step(box, monthly >= scale && people >= TechPath.ScaleUpPeople, "reallife_tech_path_scale",
                TechText.Fill("reallife_tech_path_scale_needs", "monthly", TechText.Cents(monthly), "target", TechText.Cents(scale), "people", people.ToString(), "least", TechPath.ScaleUpPeople.ToString()));
            _director.PathMore(box);
        }

        internal static void Step(InfoBox box, bool done, string key, string needs)
        {
            box.Line(TechText.T(key) + " " + (done ? TechText.T("reallife_tech_path_done") : needs ?? TechText.T("reallife_tech_path_not_yet")));
        }

        private void WriteFirm(InfoBox box, FirmsFile f, FirmPlace firm, int day)
        {
            Firm m = firm.File;
            Crew c = firm.Crew;
            string place = m.Place;
            box.Title(TechText.T("reallife_tech_firm_title", "name", m.Name, "kind", TechText.FirmName(m.K)));
            box.Line(day < m.RegisteredUntil ? TechText.T("reallife_tech_firm_registered", "day", m.RegisteredUntil.ToString())
                : TechText.T("reallife_tech_firm_unregistered", "fee", TechText.Money(Prices.At(FirmsCatalogue.RegisterFee / 100))));
            long arr = Level(FirmsRules.Arr(f, place));
            box.Line(TechText.Fill("reallife_tech_firm_value", "value", TechText.Cents(FirmsRules.Value(m, arr, 0)), "arr", TechText.Cents(arr),
                "relief", TechText.Cents(FirmsRules.Relief(m.RnD)), "day", (m.ReliefDay + 90).ToString()));
            box.Line(TechText.Fill("reallife_tech_firm_crew", "people", firm.People.ToString(), "engineering", c.Engineering.ToString(), "testing", c.Testing.ToString(), "design", c.Design.ToString(),
                "product", c.Product.ToString(), "selling", c.Selling.ToString(), "support", c.Support.ToString(),
                "spare", firm.Spare < 1000 ? TechText.T("reallife_tech_firm_spare", "share", TechText.Mille(firm.Spare)) : string.Empty));
            box.Line(TechText.Fill("reallife_tech_firm_risks", "security", TechText.Mille(c.Security), "reliability", TechText.Mille(c.Reliability), "debt", TechText.Mille(m.Debt),
                "reputation", TechText.Mille(m.Reputation), "outages", m.Outages.ToString(), "breaches", m.Breaches.ToString()));
            if (box.HasSelector)
            {
                box.Selector((int)Focus.Count, m.Focus, i => TechText.T("reallife_tech_firm_focus", "focus", TechText.FocusName((Focus)i)), i => Do(() => _director.SetFocus(place, i)));
            }
            Transform buttons = box.ButtonRow();
            if (day >= m.RegisteredUntil - 7)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_tech_button_register", () => Do(() => _director.Register(place)));
            }
            if (FirmsCatalogue.MakesProducts[m.Kind] != 0)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_tech_button_new_product", () =>
                {
                    _picked = NewProductRow + place;
                    _newKind = -1;
                    Refresh();
                });
            }
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, SectorGuide);
        }

        private void WriteNewProduct(InfoBox box, FirmPlace firm)
        {
            Firm m = firm.File;
            string place = m.Place;
            var kinds = new List<int>(4);
            for (int k = 0; k < (int)ProductKind.Count; k++)
            {
                if (FirmsRules.Makes(m.K, (ProductKind)k))
                {
                    kinds.Add(k);
                }
            }
            box.Title(TechText.T("reallife_tech_new_title", "firm", m.Name));
            if (kinds.Count == 0)
            {
                box.Line(TechText.T("reallife_tech_new_none"));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            int at = Mathf.Max(0, kinds.IndexOf(_newKind));
            _newKind = kinds[at];
            var kind = (ProductKind)_newKind;
            if (box.HasSelector)
            {
                if (kinds.Count > 1)
                {
                    box.Selector(kinds.Count, at, i => TechText.ProductName((ProductKind)kinds[i]), i => Set(() => _newKind = kinds[i]));
                }
                if (kind == ProductKind.Saas)
                {
                    box.Selector((int)Segment.Count, _newSegment, i => TechText.SegmentName((Segment)i), i => Set(() => _newSegment = i));
                }
                if (kind == ProductKind.Game)
                {
                    box.Selector(3, _newScope, TechText.Scope, i => Set(() => _newScope = i));
                }
            }
            Transform buttons = box.ButtonRow();
            int segment = _newSegment, scope = _newScope;
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_tech_button_start", () => Do(() =>
            {
                Product made = _director.NewProduct(place, kind, (Segment)segment, scope);
                if (made != null)
                {
                    _tab = TechDirector.TabProducts;
                    _picked = ProductRow + made.Id;
                }
            }));
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
            box.Line(TechText.T(Mod + "_tech_new_about_" + kind.ToString().ToLowerInvariant()));
            Meta.Guide.Point(box, SectorGuide);
        }

        private const string Mod = "reallife";

        // ---------------------------------------------------------------- products

        private TableSpec WriteProducts(InfoBox box, FirmsFile f, int day)
        {
            Product picked = null;
            string id = Picked(_picked, ProductRow);
            for (int i = 0; i < f.Products.Count; i++)
            {
                Product p = f.Products[i];
                FirmPlace firm = _director.FirmOf(p.Place);
                TableRow row = Row(ProductRow + p.Id);
                row.Set(0, p.Name);
                row.Set(1, firm != null ? firm.File.Name : "-");
                row.Set(2, TechText.ProductName(p.K) + (p.K == ProductKind.Saas ? ", " + TechText.SegmentName(p.S) : string.Empty));
                row.Set(3, State(p), FirmsRules.CanLaunch(p) ? TableRow.Green : TableRow.Plain);
                row.Set(4, p.K == ProductKind.Game ? TechText.Mille((int)System.Math.Min(1000L, (long)p.Progress * 1000 / FirmsCatalogue.GameScope[TalentCatalogue.Clamp(p.Scope, 0, 2)]))
                    : TechText.T("reallife_tech_row_features", "ours", p.Features.ToString(), "theirs", p.RivalFeatures.ToString()), p.Features, p.Features < p.RivalFeatures ? TableRow.Yellow : TableRow.Plain);
                row.Set(5, TechText.Whole(p.Users), p.Users);
                Cents(row, 6, Level(FirmsRules.MonthlyRevenue(p)));
                int q = FirmsRules.Quality(p);
                row.Set(7, TechText.Mille(q), q, q < 600 ? TableRow.Red : TableRow.Plain);
                row.Set(8, p.Bugs.ToString(), p.Bugs);
                row.Kind = p.Stage == 0 ? TechColumns.Building : p.Stage == 1 ? TechColumns.OnSale : TechColumns.Withdrawn;
                picked = id == p.Id.ToString() ? p : picked;
            }
            if (picked != null)
            {
                WriteProduct(box, f, picked, day);
            }
            else
            {
                _picked = null;
                box.Title(TechText.T("reallife_tech_products_title"));
                box.Line(TechText.T("reallife_tech_products_about"));
                Meta.Guide.Point(box, SectorGuide);
            }
            return TechColumns.Products;
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

        private void WriteProduct(InfoBox box, FirmsFile f, Product p, int day)
        {
            FirmPlace firm = _director.FirmOf(p.Place);
            int id = p.Id;
            int rival = FirmsCatalogue.RivalStock(p.K, p.S);
            box.Title(TechText.T("reallife_tech_product_title", "name", p.Name, "kind", TechText.ProductName(p.K)));
            box.Row("reallife_tech_col_stage", State(p));
            if (p.K == ProductKind.Game)
            {
                int scope = FirmsCatalogue.GameScope[TalentCatalogue.Clamp(p.Scope, 0, 2)];
                box.Line(TechText.Fill("reallife_tech_product_game_line", "scope", TechText.Scope(p.Scope), "done", TechText.Mille((int)System.Math.Min(1000L, (long)p.Progress * 1000 / scope)),
                    "hype", TechText.Mille(p.Hype), "sold", TechText.Whole(p.Users), "takings", TechText.Cents(Level(p.Revenue))));
            }
            else
            {
                box.Line(TechText.Fill("reallife_tech_product_build", "features", p.Features.ToString(), "rival", FirmsCatalogue.RivalName(rival), "theirs", p.RivalFeatures.ToString(),
                    "next", (FirmsRules.FeatureCost(p.Features) - p.Progress).ToString()));
                box.Line(TechText.Fill("reallife_tech_product_sales", "customers", TechText.Whole(p.Users), "free", TechText.Whole(p.Free), "monthly", TechText.Cents(Level(FirmsRules.MonthlyRevenue(p))),
                    "support", FirmsRules.SupportNeeded(p).ToString(), "have", firm != null ? firm.Crew.Support.ToString() : "0"));
            }
            if (p.K == ProductKind.Model)
            {
                box.Line(TechText.Fill("reallife_tech_product_training", "done", TechText.Whole(p.Compute), "need", TechText.Whole(InfraRules.TrainNeed(p.Features)), "inference", InfraRules.Inference(p).ToString()));
            }
            box.Line(TechText.Fill("reallife_tech_product_quality", "quality", TechText.Mille(FirmsRules.Quality(p)), "bugs", p.Bugs.ToString(),
                "appeal", firm != null ? TechText.Mille(FirmsRules.Appeal(p, firm.File, firm.Crew)) : "-"));
            if (box.HasSelector && p.Stage < 2)
            {
                if (p.K != ProductKind.Game)
                {
                    box.Selector(3, p.Tier, i => TechText.Fill("reallife_tech_product_tier", "tier", TechText.Tier(i), "price", TechText.Cents(Level(FirmsCatalogue.Price[p.Segment] * (long)FirmsCatalogue.PriceTiers[i] / 1000))),
                        i => Do(() => _director.SetProduct(id, i, -1, -1)));
                }
                box.Selector(FirmsCatalogue.MarketingDay.Length, p.Marketing, TechText.MarketingLevel, i => Do(() => _director.SetProduct(id, -1, i, -1)));
                if (p.K == ProductKind.Model)
                {
                    box.Selector(InfraRules.TrainLevels.Length, p.Train, i => TechText.Fill("reallife_tech_product_train", "units", InfraRules.TrainLevels[i].ToString(),
                        "cost", TechText.Cents(_director.Level((long)InfraRules.TrainLevels[i] * InfraCatalogue.GpuUnitDay * InfraCatalogue.RentedGpuShare / 1000))), i => Do(() => _director.SetTraining(id, i)));
                }
                int at = 0;
                for (int i = 0; i < Shares.Length; i++)
                {
                    at = Shares[i] <= p.Share ? i : at;
                }
                box.Selector(Shares.Length, at, i => TechText.T("reallife_tech_product_share", "share", TechText.Percent(Shares[i])), i => Do(() => _director.SetProduct(id, -1, -1, Shares[i])));
            }
            Transform buttons = box.ButtonRow();
            if (FirmsRules.CanLaunch(p))
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_tech_button_launch", () => Do(() => _director.Launch(id)));
            }
            if (p.Stage < 2)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_tech_button_withdraw", () => HudConfirm.Show("reallife_tech_confirm_withdraw_title", "reallife_tech_confirm_withdraw_body",
                    () => UiClone.Safe(() => Do(() => _director.Withdraw(id))), null, null, null, true, true));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, SectorGuide);
        }

        // ---------------------------------------------------------------- contracts

        private TableSpec WriteContracts(InfoBox box, FirmsFile f, int day)
        {
            Contract picked = null;
            string id = Picked(_picked, ContractRow);
            for (int i = 0; i < f.Contracts.Count; i++)
            {
                Contract w = f.Contracts[i];
                FirmPlace firm = _director.FirmOf(w.Place);
                TableRow row = Row(ContractRow + w.Id);
                bool waiting = w.Stage == (int)WorkStage.Offered || w.Stage == (int)WorkStage.Tender || w.Stage == (int)WorkStage.Bid;
                row.Pinned = w.Stage == (int)WorkStage.Offered || w.Stage == (int)WorkStage.Tender;
                row.Set(0, w.Client);
                row.Set(1, TechText.Work(w.K));
                row.Set(2, firm != null ? firm.File.Name : "-");
                Cents(row, 3, Level(w.Stage == (int)WorkStage.Bid ? w.Bid : w.Value));
                int progress = w.Monthly ? -1 : w.Points > 0 ? w.Done * 1000 / w.Points : 0;
                row.Set(4, progress < 0 ? "-" : TechText.Mille(progress), progress);
                row.Set(5, w.Monthly ? "-" : waiting ? TechText.T("reallife_hire_row_day", "day", w.End.ToString()) : w.Due.ToString(), w.Monthly ? 0 : waiting ? w.End : w.Due,
                    w.Stage == (int)WorkStage.Active && !w.Monthly && day > w.Due ? TableRow.Red : TableRow.Plain);
                row.Set(6, w.Stage == (int)WorkStage.Active ? TechText.Mille(w.Satisfaction) : "-", w.Satisfaction);
                row.Set(7, TechText.StageName(w.At), waiting ? TableRow.Yellow : TableRow.Plain);
                row.Kind = waiting ? TechColumns.Waiting : w.Stage == (int)WorkStage.Active ? TechColumns.UnderWay : TechColumns.Finished;
                picked = id == w.Id.ToString() ? w : picked;
            }
            if (picked != null)
            {
                WriteContract(box, f, picked, day);
            }
            else
            {
                _picked = null;
                box.Title(TechText.T("reallife_tech_contracts_title"));
                box.Line(TechText.Fill("reallife_tech_contracts_about", "upfront", TechText.Mille(FirmsCatalogue.UpfrontShare), "late", TechText.Mille(FirmsCatalogue.LatePenalty),
                    "every", FirmsCatalogue.TenderEvery.ToString()));
                Meta.Guide.Point(box, SectorGuide);
            }
            return TechColumns.Contracts;
        }

        private void WriteContract(InfoBox box, FirmsFile f, Contract w, int day)
        {
            FirmPlace firm = _director.FirmOf(w.Place);
            int id = w.Id;
            box.Title(TechText.T("reallife_tech_contract_title", "client", w.Client, "work", TechText.Work(w.K)));
            switch (w.At)
            {
                case WorkStage.Offered:
                    box.Line(TechText.T("reallife_tech_contract_offered", "firm", firm != null ? firm.File.Name : "-", "day", w.End.ToString()));
                    break;
                case WorkStage.Tender:
                    box.Line(TechText.Fill("reallife_tech_contract_tender", "rivals", w.Rivals.ToString(), "day", w.End.ToString()));
                    break;
                case WorkStage.Bid:
                    box.Line(TechText.T("reallife_tech_contract_bid_in", "amount", TechText.Cents(Level(w.Bid)), "day", w.End.ToString()));
                    break;
                case WorkStage.Active:
                    box.Line(w.Monthly
                        ? TechText.Fill("reallife_tech_contract_running", "satisfaction", TechText.Mille(w.Satisfaction), "paid", TechText.Cents(Level(w.Paid)), "day", w.End.ToString())
                        : TechText.Fill("reallife_tech_contract_progress", "done", w.Done.ToString(), "points", w.Points.ToString(), "due", w.Due.ToString(), "paid", TechText.Cents(Level(w.Paid))));
                    break;
                default:
                    box.Line(TechText.StageName(w.At) + ". " + TechText.T("reallife_tech_contract_paid", "paid", TechText.Cents(Level(w.Paid))));
                    break;
            }
            box.Line(w.Monthly
                ? TechText.Fill("reallife_tech_contract_monthly", "value", TechText.Cents(Level(w.Value)), "points", FirmsCatalogue.ManagedPoints.ToString())
                : TechText.Fill("reallife_tech_contract_project", "value", TechText.Cents(Level(w.Value)), "points", w.Points.ToString(), "days", FirmsCatalogue.WorkDays[w.Kind].ToString(),
                    "upfront", TechText.Mille(FirmsCatalogue.UpfrontShare)));
            if (!string.IsNullOrEmpty(w.OwnPlace))
            {
                box.Line(TechText.T("reallife_tech_contract_own"));
            }
            string bidPlace = null;
            int bidShare = 0;
            if (w.At == WorkStage.Tender)
            {
                _takers.Clear();
                IReadOnlyList<FirmPlace> firms = _director.FirmPlaces;
                for (int i = 0; i < firms.Count; i++)
                {
                    if (FirmsRules.Takes(firms[i].File.K, w.K))
                    {
                        _takers.Add(firms[i]);
                    }
                }
                if (_takers.Count > 0 && box.HasSelector)
                {
                    _bidFirm = Mathf.Clamp(_bidFirm, 0, _takers.Count - 1);
                    if (_takers.Count > 1)
                    {
                        box.Selector(_takers.Count, _bidFirm, i => _takers[i].File.Name, i => Set(() => _bidFirm = i));
                    }
                    box.Selector(Bids.Length, _bid, i => TechText.Fill("reallife_tech_contract_bid", "amount", TechText.Cents(Level(FirmsRules.BidAt(w, Bids[i]))), "share", TechText.Mille(Bids[i])),
                        i => Set(() => _bid = i));
                    bidPlace = _takers[_bidFirm].File.Place;
                    bidShare = Bids[_bid];
                }
                else
                {
                    box.Line(TechText.T("reallife_tech_contract_no_taker"));
                }
            }
            Transform buttons = box.ButtonRow();
            switch (w.At)
            {
                case WorkStage.Offered:
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_tech_button_take", () => Do(() => _director.TakeContract(id)));
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_tech_button_decline", () => Do(() => _director.DeclineContract(id)));
                    break;
                case WorkStage.Tender:
                    if (bidPlace != null)
                    {
                        string place = bidPlace;
                        int share = bidShare;
                        InfoBox.Button(buttons, InfoBox.Positive, "reallife_tech_button_bid", () => Do(() => _director.BidTender(id, place, share)));
                    }
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_tech_button_decline", () => Do(() => _director.DeclineContract(id)));
                    break;
                case WorkStage.Active:
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_tech_button_drop", () => HudConfirm.Show("reallife_tech_confirm_drop_title", "reallife_tech_confirm_drop_body",
                        () => UiClone.Safe(() => Do(() => _director.DropContract(id))), null, null, null, true, true));
                    break;
            }
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
            box.Line(TechText.T(Mod + "_tech_work_about_" + w.K.ToString().ToLowerInvariant()));
            Meta.Guide.Point(box, SectorGuide);
        }
    }
}
