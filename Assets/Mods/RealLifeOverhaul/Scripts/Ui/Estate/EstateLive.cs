using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the Property app's pages from the property business and carries every button to the director that already owns it, as the old
    /// app did: the same methods, the same refusals as notices, the same money. A page is written for a view when it changes: the rows of
    /// a table or the cards of a page, with the words and keys the old app had and the new ones the mockups added. Nothing a frame.
    /// </summary>
    public sealed partial class EstateLive : IEstateSource
    {
        public static readonly EstateLive Instance = new EstateLive();

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // How the last inspection of a building reads: a building never inspected has no last one to call passed.
        private static string LastInspection(EstateHolding h)
        {
            if (h.LastInspect == (int)InspectResult.None)
            {
                return EstateText.T("reallife_ui_estate_last_never");
            }
            return EstateText.T(h.LastInspect == (int)InspectResult.Failed ? "reallife_ui_estate_last_failed" : "reallife_ui_estate_last_passed");
        }

        // A yield with one decimal on every row, so a column of them reads as one column ("5.0%" beside "4.1%").
        private static string YieldOne(int hundredths) => (hundredths / 100f).ToString("0.0", Inv) + "%";
        private static int _wantTab = -1;
        private static string _wantPick;
        private int _local;

        // The nav order: the sidebar's sections in the order the mockup gives them, each the director's tab number.
        private static readonly int[] NavTab = { EstateDirector.TabMine, EstateDirector.TabTenants, EstateDirector.TabWorks, EstateDirector.TabMarket,
            EstateDirector.TabAgency, EstateDirector.TabHotel, EstateDirector.TabHire };

        private EstateLive() { }

        /// <summary>A notice asks for a tab and a row: taken when the screen next opens or is next written.</summary>
        public static void Want(int tab, string pick)
        {
            _wantTab = tab;
            _wantPick = pick;
            Instance._local++;
        }

        public int Generation => (EstateDirector.Instance != null ? EstateDirector.Instance.Revision : 0) + _local;

        public void Opened(EstateView view)
        {
            EstateDirector d = EstateDirector.Instance;
            if (d != null)
            {
                d.AppOpened();
            }
            TakeWant(view);
        }

        // A notice's tab and row are put into the view once.
        private static void TakeWant(EstateView view)
        {
            if (_wantTab < 0)
            {
                return;
            }
            int nav = Array.IndexOf(NavTab, _wantTab);
            view.Tab = nav < 0 ? 0 : nav;
            view.Pick = _wantPick;
            view.From = null;
            view.ResetTable();
            _wantTab = -1;
            _wantPick = null;
        }

        // ------------------------------------------------------------------------------------------------- the page

        public void Fill(EstateModel m, EstateView v)
        {
            TakeWant(v);
            EstateDirector d = EstateDirector.Instance;
            EstateState state = d != null && d.Running ? d.State : null;
            m.Title = EstateText.T("reallife_estate_app");
            m.Icon = UiIcon.Home;
            var runs = new EstateRuns();
            m.Tab = Math.Max(0, Math.Min(NavTab.Length - 1, v.Tab));
            if (state == null || SaveGameManager.Current == null)
            {
                m.Nav = new UiNavEntry[0];
                m.Shape = EstateShape.Off;
                m.OffTitle = EstateText.T("reallife_estate_off_title");
                m.OffBody = EstateText.T("reallife_estate_off_body");
                m.OffAction = EstateText.T("reallife_ui_estate_close");
                m.Runs = runs.ToArray();
                return;
            }
            var ctx = new Page(this, d, state, v, runs, m);
            m.Nav = ctx.Nav();
            int tab = NavTab[m.Tab];
            if (tab != EstateDirector.TabMine && tab != EstateDirector.TabMarket && !PartOn(d, tab))
            {
                ctx.PartOff(tab);
            }
            else
            {
                switch (tab)
                {
                    case EstateDirector.TabMine: ctx.Mine(); break;
                    case EstateDirector.TabTenants: ctx.Tenants(); break;
                    case EstateDirector.TabWorks: ctx.Works(); break;
                    case EstateDirector.TabAgency: ctx.Agency(); break;
                    case EstateDirector.TabHotel: ctx.Hotel(); break;
                    case EstateDirector.TabHire: ctx.Hire(); break;
                    default: ctx.Market(); break;
                }
            }
            m.Runs = runs.ToArray();
            m.Guide = GuideNote(tab, v.Pick);
        }

        // The Guide's page for what shows, as the old app's pages named it at their foot: the car parks', the landlord's duties (tenants, works, a unit, a call, an applicant), or the app's.
        private static string GuideNote(int tab, string pick)
        {
            string key = pick != null && pick.StartsWith("spc:", StringComparison.Ordinal) ? "reallife_meta_title_parking_spaces"
                : tab == EstateDirector.TabTenants || tab == EstateDirector.TabWorks || (pick != null && (pick.StartsWith("unt:", StringComparison.Ordinal)
                    || pick.StartsWith("cal:", StringComparison.Ordinal) || pick.StartsWith("app:", StringComparison.Ordinal) || pick.StartsWith("wrk:", StringComparison.Ordinal)))
                    ? "reallife_meta_title_economy_estate_duties" : "reallife_meta_title_economy_estate";
            string label = EstateText.T("reallife_guide_app");
            return label + GameUi.LookRules.Colon(label) + EstateText.T(key);
        }

        private static bool PartOn(EstateDirector d, int tab)
        {
            switch (tab)
            {
                case EstateDirector.TabHotel: return d.On(EstatePart.Hotel);
                case EstateDirector.TabHire: return d.On(EstatePart.Hire);
                default: return d.On(EstatePart.Property);
            }
        }

        /// <summary>The state of one page's writing: the director, the state, the view and the model being filled, with what every part of it shares.</summary>
        private sealed partial class Page
        {
            private readonly EstateLive _live;
            private readonly EstateDirector _d;
            private readonly EstateState _s;
            private readonly EstateView _v;
            private readonly EstateRuns _runs;
            private readonly EstateModel _m;
            private readonly EstateClimate _c;
            private readonly int _day, _monthDays;

            public Page(EstateLive live, EstateDirector d, EstateState s, EstateView v, EstateRuns runs, EstateModel m)
            {
                _live = live;
                _d = d;
                _s = s;
                _v = v;
                _runs = runs;
                _m = m;
                _c = d.Climate;
                _day = SaveGameManager.Current.Day;
                _monthDays = MacroCatalogue.MonthDays(Math.Max(1, _c.DaysPerYear));
            }

            private bool Duties => _d.On(EstatePart.Duties);

            // Go to a page: a row's id, with where Back returns to.
            private void Open(string pick, string from = null)
            {
                _v.Pick = pick;
                _v.From = from;
                _v.Offset = 0f;
                _live._local++;
            }

            private void Show(Action act)
            {
                act();
                _live._local++;
            }

            private string Money(int amount) => EstateText.Money(amount);

            private string Month(int perDay) => EstateText.Money((long)perDay * _monthDays > int.MaxValue / 2 ? int.MaxValue / 2 : perDay * _monthDays);

            // ------------------------------------------------------------------------------------------------- the sidebar

            public UiNavEntry[] Nav()
            {
                int units = 0, let = 0, calls = 0, works = 0;
                for (int i = 0; i < _s.Holdings.Count; i++)
                {
                    EstateHolding h = _s.Holdings[i];
                    if (!h.Managed)
                    {
                        units++;
                        continue;
                    }
                    for (int n = 0; n < h.Units.Count; n++)
                    {
                        units++;
                        let += h.Units[n].Let ? 1 : 0;
                        calls += h.Units[n].Let && h.Units[n].Call > 0 ? 1 : 0;
                    }
                    works += h.Busy ? 1 : 0;
                }
                string[] counts =
                {
                    units > 0 ? units.ToString(Inv) : string.Empty, let > 0 ? let.ToString(Inv) : string.Empty, calls + works > 0 ? (calls + works).ToString(Inv) : string.Empty,
                    string.Empty, _s.Agency.Running ? _s.Agency.Open.Count.ToString(Inv) : string.Empty, _s.Hotels.Count > 0 ? _s.Hotels.Count.ToString(Inv) : string.Empty,
                    _s.Hire.Running ? _s.Hire.Cars.Count.ToString(Inv) : string.Empty
                };
                UiIcon[] icons = { UiIcon.Home, UiIcon.People, UiIcon.Wrench, UiIcon.Tag, UiIcon.Key, UiIcon.Building, UiIcon.Car };
                string[] groups =
                {
                    EstateText.T("reallife_ui_estate_group_mine"), null, null, EstateText.T("reallife_ui_estate_group_market"), EstateText.T("reallife_ui_estate_group_business"), null, null
                };
                string[] NavKeys = { "reallife_ui_estate_nav_mine", "reallife_ui_estate_nav_tenants", "reallife_ui_estate_nav_works",
                    "reallife_ui_estate_nav_market", "reallife_ui_estate_nav_agency", "reallife_ui_estate_nav_hotel", "reallife_ui_estate_nav_hire" };
                var nav = new UiNavEntry[NavTab.Length];
                for (int i = 0; i < nav.Length; i++)
                {
                    nav[i] = new UiNavEntry { Icon = icons[i], Tint = UiRole.None, Label = EstateText.T(NavKeys[i]), Count = counts[i], Group = groups[i] };
                }
                return nav;
            }

            // A tab whose part is off, or stopped by a fault, says so and shows nothing else.
            public void PartOff(int tab)
            {
                _m.Shape = EstateShape.Off;
                _m.OffTitle = EstateText.T(EstateTables.Tabs[tab]);
                _m.OffBody = EstateText.T("reallife_estate_part_off");
            }
        }
    }
}
