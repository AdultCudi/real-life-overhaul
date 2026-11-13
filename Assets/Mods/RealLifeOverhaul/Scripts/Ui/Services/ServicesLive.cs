using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the Services app's pages from the cleaning service and the sales and carries every button to the director that already owns it, as the
    /// old app did: the same methods, the same refusals as notices, the same money. A page is written for a view when it changes: the rows of a table
    /// or the cards of the sale, with the words the old app had and the new ones the mockups added. Nothing a frame.
    /// </summary>
    public sealed partial class ServicesLive : IServicesSource
    {
        public static readonly ServicesLive Instance = new ServicesLive();

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] TabKeys = { "reallife_svc_tab_cleaning", "reallife_svc_tab_crew", "reallife_svc_tab_contracts", "reallife_svc_tab_sale" };
        private static readonly UiIcon[] TabIcons = { UiIcon.Spark, UiIcon.People, UiIcon.ClipboardText, UiIcon.Tag };

        private static int _wantTab = -1;
        private static string _wantPick;
        private int _local;

        private ServicesLive() { }

        /// <summary>A notice, a calendar entry or another app's button asks for a tab and a row: taken when the screen next opens or is next written.</summary>
        public static void Want(int tab, string pick)
        {
            _wantTab = tab;
            _wantPick = pick;
            Instance._local++;
        }

        /// <summary>A link's want written as a tab, a colon and a row ("3:bid:7", "2:offer:Main~4", "3:"): the Calendar's entries name their place so.</summary>
        public static void WantFrom(string want)
        {
            int colon = want != null ? want.IndexOf(':') : -1;
            if (colon > 0 && int.TryParse(want.Substring(0, colon), NumberStyles.Integer, Inv, out int tab))
            {
                Want(tab, colon + 1 < want.Length ? want.Substring(colon + 1) : null);
            }
        }

        public int Generation => (ServicesDirector.Instance != null ? ServicesDirector.Instance.Revision : 0) + _local;

        public void Opened(ServicesView view)
        {
            ServicesDirector d = ServicesDirector.Instance;
            if (d != null)
            {
                d.AppOpened();
            }
            TakeWant(view);
        }

        // A want's tab and row are put into the view once. A row of the Sell tab is a business, an offer on one or a rival's listing.
        private static void TakeWant(ServicesView v)
        {
            if (_wantTab < 0)
            {
                return;
            }
            v.Tab = Math.Max(0, Math.Min(TabKeys.Length - 1, _wantTab));
            v.Pick = _wantPick;
            v.Filter = 0;
            v.SortColumn = -1;
            v.SortDown = false;
            v.PickerOpen = false;
            v.Offset = 0f;
            v.Step = -1;
            v.SellView = 0;
            if (v.Tab == ServicesDirector.TabSale && _wantPick != null)
            {
                ServicesState state = ServicesDirector.Instance != null ? ServicesDirector.Instance.State : null;
                if (_wantPick.StartsWith("sell:", StringComparison.Ordinal))
                {
                    v.Business = _wantPick.Substring(5);
                    v.Pick = null;
                }
                else if (_wantPick.StartsWith("bid:", StringComparison.Ordinal) && state != null && int.TryParse(_wantPick.Substring(4), NumberStyles.Integer, Inv, out int id))
                {
                    SaleOffer o = state.SaleOffer(id);
                    v.Business = o != null ? o.Place : v.Business;
                }
                else if (_wantPick.StartsWith("rsale:", StringComparison.Ordinal))
                {
                    v.SellView = 1;
                }
            }
            _wantTab = -1;
            _wantPick = null;
        }

        // ------------------------------------------------------------------------------------------------- the page

        public void Fill(ServicesModel m, ServicesView v)
        {
            TakeWant(v);
            ServicesDirector d = ServicesDirector.Instance;
            ServicesState state = d != null && d.Running ? d.State : null;
            m.Title = ServicesText.T("reallife_svc_app");
            m.Icon = UiIcon.Handshake;
            v.Tab = Math.Max(0, Math.Min(TabKeys.Length - 1, v.Tab));
            m.Tab = v.Tab;
            var runs = new ServicesRuns();
            if (state == null || SaveGameManager.Current == null)
            {
                m.Nav = new UiNavEntry[0];
                m.Shape = ServicesShape.Off;
                m.OffTitle = ServicesText.T("reallife_co_off_title");
                m.OffBody = ServicesText.T("reallife_svc_off_body");
                m.OffAction = ServicesText.T("reallife_ui_svc_close");
                m.Runs = runs.ToArray();
                return;
            }
            var page = new Page(this, d, state, v, runs, m);
            m.Nav = page.Nav();
            bool on = v.Tab == ServicesDirector.TabSale ? d.SaleOn : d.CleaningOn;
            if (!on)
            {
                page.PartOff();
            }
            else
            {
                switch (v.Tab)
                {
                    case ServicesDirector.TabCrew: page.Crew(); break;
                    case ServicesDirector.TabContracts: page.Contracts(); break;
                    case ServicesDirector.TabSale: page.Sell(); break;
                    default: page.Cleaning(); break;
                }
            }
            m.Runs = runs.ToArray();
            string label = ServicesText.T("reallife_guide_app");
            m.Guide = label + GameUi.LookRules.Colon(label)
                + ServicesText.T(v.Tab == ServicesDirector.TabSale ? "reallife_meta_title_economy_sale" : "reallife_meta_title_economy_cleaning");
        }

        /// <summary>The state of one page's writing: the director, the state, the view and the model being filled, with what every part of it shares.</summary>
        private sealed partial class Page
        {
            private readonly ServicesLive _live;
            private readonly ServicesDirector _d;
            private readonly ServicesState _s;
            private readonly ServicesView _v;
            private readonly ServicesRuns _runs;
            private readonly ServicesModel _m;
            private readonly int _day;

            public Page(ServicesLive live, ServicesDirector d, ServicesState s, ServicesView v, ServicesRuns runs, ServicesModel m)
            {
                _live = live;
                _d = d;
                _s = s;
                _v = v;
                _runs = runs;
                _m = m;
                _day = SaveGameManager.Current.Day;
            }

            // A press that changes only what the page shows: the page is written again.
            private void Show(Action act)
            {
                act();
                _live._local++;
            }

            private static string Money(float amount) => ServicesText.Money(amount);

            public UiNavEntry[] Nav()
            {
                int running = ContractRules.Running(_s);
                int waiting = (_d.SaleOn ? _s.SaleOffers.Count + _s.RivalSales.Count : 0);
                string[] counts =
                {
                    _s.Clients.Count > 0 ? _s.Clients.Count.ToString(Inv) : string.Empty,
                    _s.Crew.Count > 0 ? _s.Crew.Count.ToString(Inv) : string.Empty,
                    _s.Offers.Count + running > 0 ? (_s.Offers.Count + running).ToString(Inv) : string.Empty,
                    waiting > 0 ? waiting.ToString(Inv) : string.Empty
                };
                var nav = new UiNavEntry[TabKeys.Length];
                for (int i = 0; i < nav.Length; i++)
                {
                    nav[i] = new UiNavEntry { Icon = TabIcons[i], Tint = UiRole.None, Label = ServicesText.T(TabKeys[i]), Count = counts[i] };
                }
                return nav;
            }

            // A tab whose part is off, or stopped by a fault, says so and shows nothing else.
            public void PartOff()
            {
                _m.Shape = ServicesShape.Off;
                _m.OffTitle = ServicesText.T(TabKeys[_v.Tab]);
                _m.OffBody = ServicesText.T(_v.Tab == ServicesDirector.TabSale && GoingPublicOn() ? "reallife_ui_svc_going_public" : "reallife_co_part_off");
                _m.OffAction = ServicesText.T("reallife_ui_svc_close");
            }

            private static bool GoingPublicOn() => GoingPublic.InSession;
        }
    }
}
