using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the Tech app's pages from the tech sector and carries every button to the director that already owns it, as the old app did: the same methods, the same refusals as
    /// notices, the same money. A page is written for a view when it changes. The desk sessions stay at the desk: a milestone's sign-off, a playtest, a patch and a sale open
    /// the page or show the desk, and never run from here. Nothing a frame.
    /// </summary>
    public sealed partial class TechLive : ITechSource
    {
        public static readonly TechLive Instance = new TechLive();

        public const int TabFirms = 0, TabProducts = 1, TabContracts = 2, TabInfra = 3, TabStartups = 4, TabFunding = 5, Tabs = 6;

        private static int _wantTab = -1;
        private static string _wantPick;
        private int _local;
        private readonly TechWords _words = new TechWords();

        private TechLive() { }

        /// <summary>A notice or a link asks for a tab and a row: taken when the screen next opens or is next written.</summary>
        public static void Want(int tab, string pick)
        {
            _wantTab = tab;
            _wantPick = pick;
            Instance._local++;
        }

        /// <summary>A calendar entry's place in the app, "tab:pick" (the director's tab number, then the row's id): taken as a notice's want is.</summary>
        public static void WantFrom(string want)
        {
            int colon = want != null ? want.IndexOf(':') : -1;
            if (colon > 0 && int.TryParse(want.Substring(0, colon), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int tab))
            {
                Want(tab, colon + 1 < want.Length ? want.Substring(colon + 1) : null);
            }
        }

        public int Generation
        {
            get
            {
                TechDirector d = TechDirector.Instance;
                int day = SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
                return unchecked((d != null ? d.Revision : 0) * 31 + _local + day * 7919);
            }
        }

        public void Opened(TechView view)
        {
            TechDirector d = TechDirector.Instance;
            if (d != null)
            {
                d.TechOpened();
            }
            view.Opened = true;
            TakeWant(view);
        }

        private static void TakeWant(TechView view)
        {
            if (_wantTab < 0)
            {
                return;
            }
            view.Land(_wantTab, _wantPick);
            _wantTab = -1;
            _wantPick = null;
        }

        // ------------------------------------------------------------------------------------------------- the page

        public void Fill(TechModel m, TechView v)
        {
            TakeWant(v);
            TechDirector d = TechDirector.Instance;
            FirmsFile f = d != null ? d.Firms : null;
            m.Title = TechText.T("reallife_tech_app");
            m.Icon = UiIcon.Monitor;
            if (f == null || SaveGameManager.Current == null)
            {
                m.Shape = TechShape.Off;
                m.OffTitle = TechText.T("reallife_roles_off_title");
                m.OffBody = TechText.T("reallife_tech_off_body");
                m.OffAction = TechText.T("reallife_ui_tech_close");
                return;
            }
            var page = new Page(this, d, f, v, m);
            page.Write();
        }

        private void Touch() => _local++;

        /// <summary>The state of one page's writing: the director, the file, the view and the model being filled, with what every part of it shares.</summary>
        private sealed partial class Page
        {
            private readonly TechLive _live;
            private readonly TechDirector _d;
            private readonly FirmsFile _f;
            private readonly TechView _v;
            private readonly TechModel _m;
            private readonly TechRuns _runs = new TechRuns();
            private readonly int _day;
            private FirmPlace _firm;

            public Page(TechLive live, TechDirector d, FirmsFile f, TechView v, TechModel m)
            {
                _live = live;
                _d = d;
                _f = f;
                _v = v;
                _m = m;
                _day = SaveGameManager.Current.Day;
            }

            private TechWords W => _live._words;

            private static long Level(long cents) => cents * Prices.Level / 1000;

            private string Cents(long cents) => TechText.Cents(cents);

            // A press: the director's call, then the page is written again.
            private TechRun Do(Action act) => () =>
            {
                UiClone.Safe(act);
                _live.Touch();
                return true;
            };

            private TechRun Go(Action<TechView> change) => () =>
            {
                change(_v);
                _live.Touch();
                return true;
            };

            private TechRun Ask(string title, string body, Action act) => () =>
            {
                GameConfirm.Show(title, body, () => UiClone.Safe(() =>
                {
                    act();
                    _live.Touch();
                }), null, null, null, true, true);
                return true;
            };

            private int Run(TechRun run) => _runs.Add(run);

            private UiActionModel Button(string label, UiButtonKind kind, UiAction key, TechRun run, bool disabled = false, UiIcon icon = UiIcon.None) =>
                new UiActionModel { Label = label, Kind = kind, Key = key, Icon = icon, Command = TechFace.ActionCommand, Arg = Run(run), Disabled = disabled };

            private static UiRole Ink(ViewTone t) => t == ViewTone.Good ? UiRole.OkText : t == ViewTone.Warn ? UiRole.WarnText : t == ViewTone.Bad ? UiRole.DangerText : UiRole.None;

            private static UiRole Tone(ViewTone t) => t == ViewTone.Good ? UiRole.Ok : t == ViewTone.Warn ? UiRole.Warn : t == ViewTone.Bad ? UiRole.Danger : UiRole.Accent;

            private string Words(ViewLine l) => l == null ? string.Empty : TechText.Fill(l.Key, l.Pairs ?? Array.Empty<string>());

            // ------------------------------------------------------------------------------------------------- what is open

            public void Write()
            {
                _firm = _v.Firm != null ? _d.FirmOf(_v.Firm) : null;
                if (_v.Firm != null && _firm == null)
                {
                    _v.Firm = null;   // the firm is gone (sold, closed): the firms list, never a page of nothing
                    _v.Pick = null;
                    _v.Tab = TabFirms;
                }
                _m.Prompts = new[]
                {
                    new UiPrompt { Action = UiAction.Move, Label = TechText.T("reallife_ui_prompt_move") },
                    new UiPrompt { Action = UiAction.Confirm, Label = TechText.T("reallife_ui_prompt_select") },
                    new UiPrompt { Action = UiAction.Back, Label = TechText.T("reallife_ui_prompt_back") }
                };
                if (_firm != null)
                {
                    FirmNav();
                    FirmPage();
                }
                else
                {
                    TabNav();
                    TabPage();
                }
                _m.Runs = _runs.ToArray();
            }

            // The sidebar of the tabs: what the player runs, then what they build and fund.
            private void TabNav()
            {
                int tab = _v.Tab;
                string pick = _v.Pick;
                if (pick != null)
                {
                    tab = TabOf(pick, tab);
                }
                tab = Math.Max(0, Math.Min(Tabs - 1, tab));
                _v.Tab = tab;
                FirmsFile f = _f;
                int onSale = 0, waiting = 0;
                for (int i = 0; i < f.Products.Count; i++)
                {
                    onSale += f.Products[i].Stage < 2 ? 1 : 0;
                }
                for (int i = 0; i < f.Contracts.Count; i++)
                {
                    int stage = f.Contracts[i].Stage;
                    waiting += stage == (int)WorkStage.Offered || stage == (int)WorkStage.Tender ? 1 : 0;
                }
                InfraFile infra = _d.Infra;
                VentureFile venture = _d.Ventures;
                int stakes = 0;
                for (int i = 0; venture != null && i < venture.Startups.Count; i++)
                {
                    VentureRules.PlayerValue(venture.Startups[i], out _, out long put);
                    stakes += put > 0 ? 1 : 0;
                }
                string[] counts =
                {
                    Count(_d.FirmPlaces.Count), Count(onSale), Count(waiting), Count(infra != null ? infra.Centres.Count : 0), Count(stakes),
                        Count(_d.FirmPlaces.Count > 0 && venture != null ? _d.FirmPlaces.Count : 0)
                };
                UiIcon[] icons = { UiIcon.Building, UiIcon.Package, UiIcon.Handshake, TechGlyphs.Centre, TechGlyphs.Startup, UiIcon.Coin };
                string[] keys = { "reallife_tech_tab_firms", "reallife_tech_tab_products", "reallife_tech_tab_contracts", "reallife_tech_tab_infra", "reallife_tech_tab_startups",
                    "reallife_tech_tab_funding" };
                string[] groups = { TechText.T("reallife_ui_tech_group_sector"), null, null, TechText.T("reallife_ui_tech_group_build"), null, null };
                var nav = new UiNavEntry[Tabs];
                var navKeys = new string[Tabs];
                for (int i = 0; i < Tabs; i++)
                {
                    nav[i] = new UiNavEntry { Icon = icons[i], Tint = UiRole.None, Label = TechText.T(keys[i]), Count = counts[i], Group = groups[i] };
                    navKeys[i] = "tab:" + i;
                }
                _m.Nav = nav;
                _m.NavKeys = navKeys;
                _m.NavSelected = tab;
            }

            private static string Count(int n) => n > 0 ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;

            // The tab a page belongs to, by its row's kind.
            private static int TabOf(string pick, int fallback)
            {
                if (pick.StartsWith(TechApp.ProductRow, StringComparison.Ordinal))
                {
                    return TabProducts;
                }
                if (pick.StartsWith(TechApp.ContractRow, StringComparison.Ordinal))
                {
                    return TabContracts;
                }
                if (pick.StartsWith(TechApp.CentreRow, StringComparison.Ordinal) || pick.StartsWith(TechApp.CloudRow, StringComparison.Ordinal)
                    || pick.StartsWith(TechApp.FitRow, StringComparison.Ordinal))
                {
                    return TabInfra;
                }
                if (pick.StartsWith(TechApp.StartupRow, StringComparison.Ordinal))
                {
                    return TabStartups;
                }
                if (pick.StartsWith(TechApp.CapRow, StringComparison.Ordinal))
                {
                    return TabFunding;
                }
                return fallback;
            }

            private void TabPage()
            {
                switch (_v.Tab)
                {
                    case TabProducts: ProductsTab(); break;
                    case TabContracts: ContractsTab(); break;
                    case TabInfra: InfraTab(); break;
                    case TabStartups: StartupsTab(); break;
                    case TabFunding: FundingTab(); break;
                    default: FirmsTab(); break;
                }
            }

            private static string Pick(string pick, string kind) => pick != null && pick.StartsWith(kind, StringComparison.Ordinal) ? pick.Substring(kind.Length) : null;

            // The tab's own page, reset: a tab never keeps what the page before it held.
            private void Back(string words) => _m.Back = words;
        }
    }

    /// <summary>The figures as the Tech app writes them: money at today's prices, numbers, shares, days and the mod's texts.</summary>
    public sealed class TechWords : IStudioFormat
    {
        public string Money(long cents) => TechText.Cents(cents * Prices.Level / 1000);

        public string Whole(long n) => TechText.Whole(n);

        public string Mille(int thousandths) => TechText.Mille(thousandths);

        public string Day(int day) => TechText.Day(day);

        public string Text(string key) => TechText.T(key);
    }
}
