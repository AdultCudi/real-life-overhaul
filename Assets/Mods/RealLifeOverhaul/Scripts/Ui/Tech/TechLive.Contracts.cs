using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class TechLive
    {
        private static readonly int[] Bids = { 800, 900, 1000, 1100, 1200 };
        private static int _bid = 2, _bidFirm;

        private sealed partial class Page
        {
            private static UiRole StageTone(WorkStage s) => s == WorkStage.Offered || s == WorkStage.Active || s == WorkStage.Done ? UiRole.Ok
                    : s == WorkStage.Tender || s == WorkStage.Bid ? UiRole.Notice : UiRole.Warn;

            // ------------------------------------------------------------------------------------------------- the contracts tab

            private void ContractsTab()
            {
                string pick = _v.Pick;
                if (pick != null)
                {
                    if (Pick(pick, TechApp.ContractRow) != null)
                    {
                        ContractPage(pick);
                        return;
                    }
                    _v.Pick = null;
                }
                var rows = new List<UiDataRow>(16);
                var keys = new List<string>(16);
                var order = new List<Contract>(_f.Contracts);
                // what waits for an answer first, then what is under way, then what is over, each newest first
                order.Sort((a, b) => Rank(a).CompareTo(Rank(b)) is int r && r != 0 ? r : b.Id.CompareTo(a.Id));
                for (int i = 0; i < order.Count; i++)
                {
                    Contract w = order[i];
                    FirmPlace firm = _d.FirmOf(w.Place);
                    bool waiting = w.Stage == (int)WorkStage.Offered || w.Stage == (int)WorkStage.Tender || w.Stage == (int)WorkStage.Bid;
                    UiDataRow row = Row(7);
                    row.Cells[0] = w.Client;
                    row.Sub = TechText.Work(w.K);
                    row.Values[0] = (int)TechGlyphs.Of(TechGlyphs.Work, w.Kind);
                    row.Cells[1] = firm != null ? firm.File.Name : "-";
                    row.Cells[2] = Cents(Level(w.Stage == (int)WorkStage.Bid ? w.Bid : w.Value));
                    int progress = w.Monthly ? -1 : w.Points > 0 ? w.Done * 100 / w.Points : 0;
                    row.Cells[3] = progress < 0 ? "-" : progress.ToString(Inv) + "%";
                    row.Cells[4] = w.Monthly ? "-" : waiting ? TechText.T("reallife_hire_row_day", "day", w.End.ToString(Inv)) : TechText.Day(w.Due);
                    row.Cells[5] = w.Stage == (int)WorkStage.Active ? TechText.Mille(w.Satisfaction) : "-";
                    row.Values[5] = w.Stage == (int)WorkStage.Active ? w.Satisfaction / 10 : 0;
                    row.Cells[6] = TechText.StageName(w.At);
                    row.Tones[6] = StageTone(w.At);
                    rows.Add(row);
                    keys.Add("work:" + w.Id);
                }
                _m.Shape = TechShape.Table;
                _m.Table = new UiDataTableModel
                {
                    Columns = new[]
                    {
                        Col("reallife_tech_col_client", UiCellKind.Tile), Col("reallife_tech_col_firm", UiCellKind.Text, 132f), Col("reallife_tech_col_value", UiCellKind.Money, 100f),
                        Col("reallife_ui_tech_col_progress", UiCellKind.Text, 80f), Col("reallife_ui_tech_col_due", UiCellKind.Text, 88f), Col("reallife_tech_col_satisfaction",
                            UiCellKind.Meter, 120f),
                        Col("reallife_ui_tech_col_status", UiCellKind.Chip, 120f)
                    },
                    Rows = rows.ToArray(), RowCount = rows.Count, Selected = -1, EmptyTitle = TechText.T("reallife_tech_contracts_title"),
                    EmptyBody = TechText.Fill("reallife_tech_contracts_about", "upfront", TechText.Mille(FirmsCatalogue.UpfrontShare), "late", TechText.Mille(FirmsCatalogue.LatePenalty),
                        "every", FirmsCatalogue.TenderEvery.ToString()),
                    EmptyIcon = UiIcon.Handshake
                };
                _m.RowKeys = keys.ToArray();
                int waitingCount = 0;
                for (int i = 0; i < _f.Contracts.Count; i++)
                {
                    waitingCount += _f.Contracts[i].Stage == (int)WorkStage.Offered || _f.Contracts[i].Stage == (int)WorkStage.Tender ? 1 : 0;
                }
                _m.Next = waitingCount > 0 ? TechText.T("reallife_ui_tech_next_contracts", "count", waitingCount.ToString(Inv)) : TechText.Fill("reallife_tech_contracts_about", "upfront",
                    TechText.Mille(FirmsCatalogue.UpfrontShare), "late", TechText.Mille(FirmsCatalogue.LatePenalty), "every", FirmsCatalogue.TenderEvery.ToString());
            }

            private static int Rank(Contract w) => w.Stage == (int)WorkStage.Offered || w.Stage == (int)WorkStage.Tender ? 0 : w.Stage == (int)WorkStage.Bid ? 1
                    : w.Stage == (int)WorkStage.Active ? 2 : 3;

            // ------------------------------------------------------------------------------------------------- one contract

            private void ContractPage(string pick)
            {
                Contract w = int.TryParse(Pick(pick, TechApp.ContractRow), out int id) ? _f.ContractOf(id) : null;
                if (w == null)
                {
                    _v.Pick = null;
                    ContractsTab();
                    return;
                }
                FirmPlace firm = _d.FirmOf(w.Place);
                Idle idle = Idle.None;
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_ui_tech_back_contracts");
                _m.Head = new UiHeadModel
                {
                    Name = w.Client, Icon = TechGlyphs.Of(TechGlyphs.Work, w.Kind), Role = TechText.Work(w.K), Status = TechText.StageName(w.At),
                    StatusTone = StageTone(w.At)
                };
                var now = new TechCard(_runs, TechText.T("reallife_ibox_sec_now"));
                switch (w.At)
                {
                    case WorkStage.Offered:
                        now.Say(TechText.T("reallife_tech_contract_offered", "firm", firm != null ? firm.File.Name : "-", "day", w.End.ToString(Inv)));
                        break;
                    case WorkStage.Tender:
                        now.Say(TechText.Fill("reallife_tech_contract_tender", "rivals", w.Rivals.ToString(Inv), "day", w.End.ToString(Inv)));
                        TenderRivals(now, w);
                        break;
                    case WorkStage.Bid:
                        now.Say(TechText.T("reallife_tech_contract_bid_in", "amount", Cents(Level(w.Bid)), "day", w.End.ToString(Inv)));
                        TenderRivals(now, w);
                        break;
                    case WorkStage.Active:
                        now.Say(w.Monthly
                            ? TechText.Fill("reallife_tech_contract_running", "satisfaction", TechText.Mille(w.Satisfaction), "paid", Cents(Level(w.Paid)), "day", w.End.ToString(Inv))
                            : TechText.Fill("reallife_tech_contract_progress", "done", w.Done.ToString(Inv), "points", w.Points.ToString(Inv), "due", w.Due.ToString(Inv), "paid",
                                Cents(Level(w.Paid))));
                        if (firm != null)
                        {
                            idle = WorkLines(now, firm, w.Kind, true);
                        }
                        break;
                    default:
                        now.Say(TechText.StageName(w.At) + ". " + TechText.T("reallife_tech_contract_paid", "paid", Cents(Level(w.Paid))));
                        break;
                }
                var terms = new TechCard(_runs, TechText.T("reallife_ibox_sec_terms"));
                terms.Say(w.Monthly
                    ? TechText.Fill("reallife_tech_contract_monthly", "value", Cents(Level(w.Value)), "points", FirmsCatalogue.ManagedPoints.ToString())
                    : TechText.Fill("reallife_tech_contract_project", "value", Cents(Level(w.Value)), "points", w.Points.ToString(Inv), "days", FirmsCatalogue.WorkDays[w.Kind].ToString(),
                        "upfront", TechText.Mille(FirmsCatalogue.UpfrontShare)));
                if (!string.IsNullOrEmpty(w.OwnPlace))
                {
                    terms.Say(TechText.T("reallife_tech_contract_own"));
                }
                var actions = new List<UiActionModel>(3);
                switch (w.At)
                {
                    case WorkStage.Offered:
                        actions.Add(Button(TechText.T("reallife_tech_button_decline"), UiButtonKind.Destructive, UiAction.Count, Do(() => _d.DeclineContract(id))));
                        actions.Add(Button(TechText.T("reallife_tech_button_take"), UiButtonKind.Primary, UiAction.Alt, Do(() => _d.TakeContract(id))));
                        break;
                    case WorkStage.Tender:
                        TenderBid(terms, w, actions, id);
                        break;
                    case WorkStage.Active:
                        actions.Add(Button(TechText.T("reallife_tech_button_drop"), UiButtonKind.Destructive, UiAction.Count,
                            Ask("reallife_tech_confirm_drop_title", "reallife_tech_confirm_drop_body", () => _d.DropContract(id))));
                        break;
                }
                _m.HeadActions = actions.ToArray();
                terms.Say(TechText.T(Mod + "_tech_work_about_" + w.K.ToString().ToLowerInvariant()));
                if (firm != null && idle != Idle.None)
                {
                    WorkLinks(terms, firm, idle, false);
                }
                _m.Left = new[] { now.Build() };
                _m.Right = new[] { terms.Build() };
            }

            // A tender: which firm bids and at what share of the fair price, as steps, and the bid as the main action.
            private void TenderBid(TechCard terms, Contract w, List<UiActionModel> actions, int id)
            {
                var takers = new List<FirmPlace>(4);
                IReadOnlyList<FirmPlace> firms = _d.FirmPlaces;
                for (int i = 0; i < firms.Count; i++)
                {
                    if (FirmsRules.Takes(firms[i].File.K, w.K))
                    {
                        takers.Add(firms[i]);
                    }
                }
                actions.Add(Button(TechText.T("reallife_tech_button_decline"), UiButtonKind.Destructive, UiAction.Count, Do(() => _d.DeclineContract(id))));
                if (takers.Count == 0)
                {
                    terms.Say(TechText.T("reallife_tech_contract_no_taker"), UiRole.WarnText);
                    return;
                }
                _bidFirm = Math.Max(0, Math.Min(_bidFirm, takers.Count - 1));
                int firmAt = _bidFirm, bidAt = _bid;
                if (takers.Count > 1)
                {
                    terms.Step(TechText.T("reallife_tech_col_firm"), takers[firmAt].File.Name, null, true, true, Go(v => _bidFirm = (firmAt + takers.Count - 1) % takers.Count),
                        Go(v => _bidFirm = (firmAt + 1) % takers.Count));
                }
                terms.Step(TechText.T("reallife_ui_tech_bid"), TechText.Fill("reallife_tech_contract_bid", "amount", Cents(Level(FirmsRules.BidAt(w, Bids[bidAt]))), "share",
                    TechText.Mille(Bids[bidAt])), null,
                    bidAt > 0, bidAt < Bids.Length - 1, Go(v => _bid = bidAt - 1), Go(v => _bid = bidAt + 1));
                string place = takers[firmAt].File.Place;
                int share = Bids[bidAt];
                actions.Add(Button(TechText.T("reallife_tech_button_bid"), UiButtonKind.Primary, UiAction.Alt, Do(() => _d.BidTender(id, place, share))));
            }

            private void TenderRivals(TechCard card, Contract t)
            {
                var names = new StringBuilder(96);
                for (int r = 0; t.Council && r < t.Rivals; r++)
                {
                    int c = CompetitorRules.TenderRival(_f.Seed, t, r);
                    if (c >= 0)
                    {
                        names.Append(names.Length > 0 ? ", " : string.Empty).Append(HiringCatalogue.RivalFirms[c]);
                    }
                }
                if (names.Length > 0)
                {
                    card.Say(TechText.T("reallife_tech_contract_rivals_named", "names", names.ToString()));
                }
            }

            // Who worked yesterday on a kind of contract (-1: any of the firm's work) and for how long, or why nobody did; a line more when something is in the way from now on.
            private Idle WorkLines(TechCard card, FirmPlace firm, int kind, bool contract)
            {
                FirmFacts facts = _d.FactsOf(firm, kind);
                IReadOnlyList<Person> people = _d.People;
                var who = new StringBuilder(96);
                int shown = 0, more = 0, hours = 0;
                int own = OwnDeskRules.HoursFor(firm.OwnMinutes, firm.OwnRole, kind, firm.File.K);
                if (own > 0)
                {
                    hours += own;
                    who.Append(TechText.T("reallife_tech_work_person", "name", TechText.T("reallife_tech_work_you"), "hours", own.ToString(Inv)));
                    shown++;
                }
                for (int i = 0; i < people.Count; i++)
                {
                    Person person = people[i];
                    int h = person.HoursYesterday;
                    if (h <= 0 || person.File.Place != firm.File.Place || (kind >= 0 ? !FirmsRules.CanDo(person.File.Def, (WorkKind)kind) : !FirmsRules.WorksFor(person.File.Def, firm.File.K)))
                    {
                        continue;
                    }
                    hours += h;
                    if (shown < 4)
                    {
                        who.Append(shown > 0 ? ", " : string.Empty).Append(TechText.T("reallife_tech_work_person", "name", person.Name, "hours", h.ToString(Inv)));
                        shown++;
                    }
                    else
                    {
                        more++;
                    }
                }
                Idle now = FirmsRules.Why(facts, false);
                if (hours > 0)
                {
                    if (more > 0)
                    {
                        who.Append(' ').Append(TechText.T("reallife_tech_work_more", "count", more.ToString(Inv)));
                    }
                    card.Say(TechText.T(contract ? "reallife_tech_contract_worked" : "reallife_tech_work_yesterday", "who", who.ToString(), "hours", hours.ToString(Inv)));
                    if (now != Idle.None)
                    {
                        card.Say(TechText.T("reallife_tech_work_now", "why", TechText.IdleLine(now, facts, false)), UiRole.WarnText);
                    }
                    return now;
                }
                Idle why = FirmsRules.Why(facts, true);
                card.Say(TechText.T(contract ? "reallife_tech_contract_nobody" : "reallife_tech_work_nobody", "why", TechText.IdleLine(why == Idle.None ? Idle.Away : why, facts,
                    true)), UiRole.WarnText);
                if (now != Idle.None && now != why)
                {
                    card.Say(TechText.T("reallife_tech_work_now", "why", TechText.IdleLine(now, facts, false)), UiRole.WarnText);
                }
                return now != Idle.None ? now : why;
            }

            // What puts right what keeps a firm's people from work: the internet plan, the rota, the game's schedule.
            private void WorkLinks(TechCard card, FirmPlace firm, Idle idle, bool always)
            {
                bool net = idle == Idle.NetNone || idle == Idle.NetInstalling;
                bool schedule = always || idle == Idle.Shut || idle == Idle.NoHours || idle == Idle.NoShifts || idle == Idle.NoStations || idle == Idle.DayOff;
                bool rota = TechBridges.OpenRota != null && (always || idle == Idle.NoShifts || idle == Idle.DayOff);
                if (net)
                {
                    card.Go(TechText.T("reallife_tech_button_internet"), Do(() => _d.OpenInternet()));
                }
                if (rota)
                {
                    card.Go(TechText.T("reallife_tech_button_rota"), Do(() => _d.OpenRota(firm)));
                }
                else if (schedule)
                {
                    card.Go(TechText.T("reallife_tech_button_schedule"), Do(() => _d.OpenSchedule(firm)));
                }
            }

            // ------------------------------------------------------------------------------------------------- a firm's competitors

            private void RivalsList()
            {
                string place = _firm.File.Place;
                IReadOnlyList<Competitor> rivals = _d.RivalsOf(_firm);
                var rows = new List<UiDataRow>(16);
                var keys = new List<string>(16);
                for (int i = 0; i < rivals.Count; i++)
                {
                    Competitor c = rivals[i];
                    bool company = c.Kind == (int)RivalKind.Company;
                    UiDataRow row = Row(5);
                    row.Cells[0] = c.Name;
                    row.Sub = RivalKindWords(c);
                    row.Values[0] = (int)(company ? UiIcon.Building : c.Kind == (int)RivalKind.Startup ? TechGlyphs.Startup : UiIcon.Store);
                    row.Cells[1] = MeetWords(_firm.File.Kind, c);
                    int products = company ? ProductsAgainst(place, c.Company) : 0;
                    int tenders = company ? TendersBoth(_firm.File, c.Company) : 0;
                    row.Cells[2] = company ? products.ToString(Inv) : "-";
                    row.Values[2] = products;
                    row.Cells[3] = company ? tenders.ToString(Inv) : "-";
                    row.Values[3] = tenders;
                    row.Cells[4] = WhereShort(c);
                    rows.Add(row);
                    keys.Add("rival:" + place + "|" + c.Key);
                }
                _m.Shape = TechShape.Table;
                _m.Table = new UiDataTableModel
                {
                    Columns = new[]
                    {
                        Col("reallife_tech_col_rival", UiCellKind.Tile), Col("reallife_tech_col_meet", UiCellKind.Text, 220f), Col("reallife_tech_col_products", UiCellKind.Figure, 96f),
                        Col("reallife_tech_col_tenders", UiCellKind.Figure, 96f), Col("reallife_tech_col_where", UiCellKind.Text, 160f)
                    },
                    Rows = rows.ToArray(), RowCount = rows.Count, Selected = -1, EmptyTitle = TechText.T("reallife_ui_tech_rivals_none"),
                        EmptyBody = TechText.T("reallife_tech_rivals_how"), EmptyIcon = UiIcon.Flag
                };
                _m.RowKeys = keys.ToArray();
                _m.Next = TechText.T("reallife_tech_rivals_title", "firm", _firm.File.Name);
            }

            private void RivalPage(string both)
            {
                int bar = both != null ? both.IndexOf('|') : -1;
                string place = bar > 0 ? both.Substring(0, bar) : null, key = bar > 0 ? both.Substring(bar + 1) : null;
                FirmPlace firm = place != null ? _d.FirmOf(place) : null;
                Competitor c = null;
                IReadOnlyList<Competitor> rivals = firm != null ? _d.RivalsOf(firm) : null;
                for (int i = 0; rivals != null && i < rivals.Count; i++)
                {
                    c = rivals[i].Key == key ? rivals[i] : c;
                }
                if (c == null)
                {
                    _v.Pick = null;
                    RivalsList();
                    return;
                }
                Firm m = firm.File;
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_ui_tech_back_rivals");
                _m.Head = new UiHeadModel { Name = c.Name,
                    Icon = c.Kind == (int)RivalKind.Company ? UiIcon.Building : c.Kind == (int)RivalKind.Startup ? TechGlyphs.Startup : UiIcon.Store, Role = RivalKindWords(c) };
                var now = new TechCard(_runs, TechText.T("reallife_ibox_sec_now"));
                now.Row(TechText.T("reallife_tech_col_meet"), MeetWords(m.Kind, c));
                switch ((RivalKind)c.Kind)
                {
                    case RivalKind.Company:
                        RivalProducts(now, m, c.Company);
                        RivalTenders(now, m, c.Company);
                        now.Say(c.Street != null
                            ? TechText.Fill("reallife_tech_rival_at", "floor", c.Floor.ToString(Inv), "tower", c.Building ?? string.Empty, "address", FirmsGame.AddressWords(c.Street, c.Number))
                            : TechText.T("reallife_tech_rival_no_office"));
                        break;
                    case RivalKind.Startup:
                        Startup s = _d.Ventures != null ? _d.Ventures.StartupOf(c.Startup) : null;
                        if (s != null)
                        {
                            now.Row(TechText.T("reallife_vc_col_sector"), TechText.VentureName((Venture)s.Sector));
                            now.Row(TechText.T("reallife_vc_col_stage"), TechText.RoundName(s.R));
                        }
                        now.Say(TechText.T("reallife_tech_rival_startup_where"));
                        break;
                    default:
                        now.Row(TechText.T("reallife_tech_col_kind"), TechText.FirmName((FirmKind)Math.Max(0, Math.Min(c.Firm, (int)FirmKind.Count - 1))));
                        now.Say(c.Street != null
                            ? TechText.Fill("reallife_tech_rival_firm_at", "address", FirmsGame.AddressWords(c.Street, c.Number), "district", c.District ?? "-")
                            : TechText.T("reallife_tech_rival_no_office"));
                        break;
                }
                if (c.Kind == (int)RivalKind.Startup)
                {
                    int sid = c.Startup;
                    _m.HeadActions = new[] { Button(TechText.T("reallife_tech_button_open_startup"), UiButtonKind.Primary, UiAction.Alt, Go(v => v.Open("start:" + sid))) };
                }
                else if (c.Street != null)
                {
                    Competitor at = c;
                    _m.HeadActions = new[] { Button(TechText.T("common_setdestination"), UiButtonKind.Primary, UiAction.Alt, Do(() => _d.RivalDestination(at)), false, UiIcon.Locate) };
                }
                _m.Left = new[] { now.Build() };
            }

            private void RivalProducts(TechCard card, Firm m, int company)
            {
                int shown = 0, more = 0;
                for (int i = 0; i < _f.Products.Count; i++)
                {
                    Product p = _f.Products[i];
                    if (p.Place != m.Place || p.Stage >= 2 || CompetitorRules.RivalOf(p) != company)
                    {
                        continue;
                    }
                    if (shown >= 3)
                    {
                        more++;
                        continue;
                    }
                    shown++;
                    string share = TechText.Share(CompetitorRules.SharePpm(p));
                    card.Say(p.K == ProductKind.Game ? TechText.Fill("reallife_tech_rival_game", "product", p.Name, "sold", TechText.Whole(p.Users), "share", share)
                        : TechText.Fill("reallife_tech_rival_product", "product", p.Name, "ours", p.Features.ToString(Inv), "theirs", p.RivalFeatures.ToString(Inv), "share", share));
                }
                if (more > 0)
                {
                    card.Say(TechText.T("reallife_tech_rival_more", "count", more.ToString(Inv)));
                }
                if (shown == 0 && (CompetitorRules.CompanyMakes[company] & CompetitorRules.Makes(m.Kind)) != 0)
                {
                    card.Say(TechText.T("reallife_tech_rival_no_products"));
                }
            }

            private void RivalTenders(TechCard card, Firm m, int company)
            {
                int shown = 0, more = 0;
                for (int i = _f.Contracts.Count - 1; i >= 0; i--)
                {
                    Contract t = _f.Contracts[i];
                    if (!CompetitorRules.BothBid(_f.Seed, t, m, company))
                    {
                        continue;
                    }
                    if (shown >= 3)
                    {
                        more++;
                        continue;
                    }
                    shown++;
                    if (CompetitorRules.Decided(t))
                    {
                        long theirs = FirmsRules.RivalBid(_f.Seed, t, CompetitorRules.BidderOf(_f.Seed, t, company), FirmsRules.FairOf(t));
                        card.Say(TechText.Fill("reallife_tech_rival_tender_decided", "work", TechText.Work(t.K), "stage", TechText.StageName(t.At), "theirs", Cents(Level(theirs)),
                            "yours", Cents(Level(t.Bid))));
                    }
                    else
                    {
                        card.Say(TechText.Fill(t.At == WorkStage.Tender ? "reallife_tech_rival_tender_open" : "reallife_tech_rival_tender_bid", "work", TechText.Work(t.K), "day",
                            t.End.ToString(Inv)));
                    }
                }
                if (more > 0)
                {
                    card.Say(TechText.T("reallife_tech_rival_more", "count", more.ToString(Inv)));
                }
                if (shown == 0 && (CompetitorRules.CompanyTakes[company] & CompetitorRules.Takes(m.Kind)) != 0)
                {
                    card.Say(TechText.T("reallife_tech_rival_no_tenders"));
                }
            }

            private int ProductsAgainst(string place, int company)
            {
                int n = 0;
                for (int i = 0; i < _f.Products.Count; i++)
                {
                    Product p = _f.Products[i];
                    n += p.Place == place && p.Stage < 2 && CompetitorRules.RivalOf(p) == company ? 1 : 0;
                }
                return n;
            }

            private int TendersBoth(Firm m, int company)
            {
                int n = 0;
                for (int i = 0; i < _f.Contracts.Count; i++)
                {
                    n += CompetitorRules.BothBid(_f.Seed, _f.Contracts[i], m, company) ? 1 : 0;
                }
                return n;
            }

            private static string RivalKindWords(Competitor c)
            {
                switch ((RivalKind)c.Kind)
                {
                    case RivalKind.Company: return TechText.T("reallife_tech_rival_company");
                    case RivalKind.Startup: return TechText.T("reallife_tech_rival_startup");
                    default: return TechText.T(c.Theirs ? "reallife_tech_rival_theirs" : "reallife_tech_rival_yours");
                }
            }

            private static string MeetWords(int kind, Competitor c)
            {
                switch ((RivalKind)c.Kind)
                {
                    case RivalKind.Company:
                        int meet = CompetitorRules.Meet(kind, CompetitorRules.CompanyMakes[c.Company], CompetitorRules.CompanyTakes[c.Company]);
                        return OfferWords(meet & 0xFF, meet >> 8);
                    case RivalKind.Startup:
                        return TechText.VentureName((Venture)c.Sector);
                    default:
                        int both = CompetitorRules.Meet(kind, CompetitorRules.Makes(c.Firm), CompetitorRules.Takes(c.Firm));
                        return OfferWords(both & 0xFF, both >> 8);
                }
            }

            private static string OfferWords(int products, int work)
            {
                var words = new StringBuilder(96);
                for (int k = 0; k < (int)ProductKind.Count; k++)
                {
                    if ((products & (1 << k)) != 0)
                    {
                        words.Append(words.Length > 0 ? ", " : string.Empty).Append(TechText.ProductName((ProductKind)k));
                    }
                }
                for (int k = 0; k < (int)WorkKind.Count; k++)
                {
                    if ((work & (1 << k)) != 0)
                    {
                        words.Append(words.Length > 0 ? ", " : string.Empty).Append(TechText.Work((WorkKind)k));
                    }
                }
                return words.Length > 0 ? words.ToString() : "-";
            }

            private static string WhereShort(Competitor c)
            {
                if (c.Kind == (int)RivalKind.Company)
                {
                    return c.Building != null ? TechText.Fill("reallife_tech_rival_floor", "tower", c.Building, "floor", c.Floor.ToString(Inv)) : "-";
                }
                return c.Kind == (int)RivalKind.Firm && c.District != null ? c.District : "-";
            }
        }
    }
}
