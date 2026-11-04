using System;
using System.Collections.Generic;
using System.Globalization;
using Extensions;
using Localizor;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the Car Showroom's pages from the dealership and carries every button to the director that already owns it, as the old app did:
    /// the same methods, the same refusals as notices, the same questions of the game's own before anything that costs or cannot be undone.
    /// A page is written for a view when it changes. Nothing a frame.
    /// </summary>
    public sealed partial class DealerLive : IEstateSource
    {
        public const string ScreenId = "dealer";

        public static readonly DealerLive Instance = new DealerLive();

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static int _wantTab = -1;
        private static string _wantPick;
        private int _local;

        // The sidebar's order, each the director's tab number: the showrooms first, then the cars, the market, and the money.
        private static readonly int[] NavTab = { DealerDirector.TabLots, DealerDirector.TabStock, DealerDirector.TabMarket, DealerDirector.TabOffers,
            DealerDirector.TabFinance, DealerDirector.TabGrowth, DealerDirector.TabReports };
        private static readonly string[] TabKeys =
        {
            "reallife_dealer_tab_stock", "reallife_dealer_tab_market", "reallife_dealer_tab_offers", "reallife_dealer_tab_finance", "reallife_dealer_tab_lots", "reallife_dealer_tab_growth",
            "reallife_dealer_tab_reports"
        };

        private DealerLive() { }

        /// <summary>A notice asks for a tab and a row: taken when the screen next opens or is next written.</summary>
        public static void Want(int tab, string pick)
        {
            _wantTab = tab;
            _wantPick = pick;
            Instance._local++;
        }

        // The game's picture of a car by its type, asked of the game once a type: the key the catalogue's cards and the auction card carry.
        public const string PictureKey = "vehicle:";
        private static readonly Dictionary<string, Sprite> Pictures = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static bool _picturesOn;

        private static void PicturesOn()
        {
            if (_picturesOn)
            {
                return;
            }
            _picturesOn = true;
            Func<string, Sprite> before = UiPictures.Find;
            UiPictures.Find = key => key != null && key.StartsWith(PictureKey, StringComparison.Ordinal) ? Picture(key.Substring(PictureKey.Length)) : before?.Invoke(key);
        }

        private static Sprite Picture(string type)
        {
            Sprite sprite;
            if (Pictures.TryGetValue(type, out sprite))
            {
                return sprite;
            }
            try
            {
                sprite = ShowroomPhotos.IconOf(type);
            }
            catch (Exception)
            {
                sprite = null;
            }
            Pictures[type] = sprite;
            return sprite;
        }

        /// <summary>The picture's key for a vehicle type, and its shape (width over height) so the tile never stretches it.</summary>
        internal static string PictureOf(string type, out float aspect)
        {
            PicturesOn();
            Sprite s = Picture(type);
            aspect = s != null && s.rect.height > 0f ? s.rect.width / s.rect.height : 1.6f;
            return s != null ? PictureKey + type : null;
        }

        public int Generation => (DealerDirector.Instance != null ? DealerDirector.Instance.Revision : 0) + _local;

        public void Opened(EstateView view)
        {
            DealerDirector d = DealerDirector.Instance;
            if (d != null)
            {
                d.AppOpened();
            }
            TakeWant(view);
        }

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
            _wantTab = -1;
            _wantPick = null;
        }

        public void Fill(EstateModel m, EstateView v)
        {
            TakeWant(v);
            DealerDirector d = DealerDirector.Instance;
            DealerState state = d != null && d.Running ? d.State : null;
            m.Title = DealerText.T("reallife_dealer_app");
            m.Icon = UiIcon.Car;
            m.TabsAcross = true;
            var runs = new EstateRuns();
            m.Tab = Math.Max(0, Math.Min(NavTab.Length - 1, v.Tab));
            if (state == null || SaveGameManager.Current == null)
            {
                m.Nav = new UiNavEntry[0];
                m.Shape = EstateShape.Off;
                m.OffTitle = DealerText.T("reallife_dealer_off_title");
                m.OffBody = DealerText.T("reallife_dealer_off_body");
                m.Runs = runs.ToArray();
                return;
            }
            PicturesOn();
            var page = new Page(this, d, state, v, runs, m);
            m.Nav = page.Nav();
            switch (NavTab[m.Tab])
            {
                case DealerDirector.TabStock: page.Stock(); break;
                case DealerDirector.TabMarket: page.Market(); break;
                case DealerDirector.TabOffers: page.Offers(); break;
                case DealerDirector.TabFinance: page.Finance(); break;
                case DealerDirector.TabGrowth: page.Growth(); break;
                case DealerDirector.TabReports: page.Reports(); break;
                default: page.Lots(); break;
            }
            m.Runs = runs.ToArray();
        }

        /// <summary>The state of one page's writing: the director, the state, the view and the model, with what every part of it shares.</summary>
        private sealed partial class Page
        {
            private readonly DealerLive _live;
            private readonly DealerDirector _d;
            private readonly DealerState _s;
            private readonly EstateView _v;
            private readonly EstateRuns _runs;
            private readonly EstateModel _m;
            private readonly int _now, _day, _level;

            public Page(DealerLive live, DealerDirector d, DealerState s, EstateView v, EstateRuns runs, EstateModel m)
            {
                _live = live;
                _d = d;
                _s = s;
                _v = v;
                _runs = runs;
                _m = m;
                _day = SaveGameManager.Current.Day;
                _now = _day * 24 + SaveGameManager.Current.Hour;
                _level = Prices.Level;
            }

            private string Money(int amount) => DealerText.Money(amount);

            private void Show(Action act)
            {
                act();
                _live._local++;
            }

            private void Open(string pick, string from = null)
            {
                _v.Pick = pick;
                _v.From = from;
                _v.Offset = 0f;
                _live._local++;
            }

            private static string Picked(string pick, string prefix) => pick != null && pick.StartsWith(prefix, StringComparison.Ordinal) ? pick.Substring(prefix.Length) : null;

            // Anything that costs or cannot be undone is asked first, with the game's own question.
            private static void Confirm(string titleKey, string body, Action yes, bool negative)
            {
                GameConfirm.Show(titleKey.GetLocalization(), body, () => UiClone.Safe(yes), null, null, null, false, negative);
            }

            public UiNavEntry[] Nav()
            {
                int cars = _d.CarCount(), offers = _d.Waiting(), lots = _s.Yards.Count;
                string[] counts = { lots > 0 ? lots.ToString(Inv) : string.Empty, cars > 0 ? cars.ToString(Inv) : string.Empty, string.Empty,
                    offers > 0 ? offers.ToString(Inv) : string.Empty, string.Empty, string.Empty, string.Empty };
                UiIcon[] icons = { UiIcon.Store, UiIcon.Car, UiIcon.Tag, UiIcon.Handshake, UiIcon.Receipt, UiIcon.Rocket, UiIcon.Chart };
                var nav = new UiNavEntry[NavTab.Length];
                for (int i = 0; i < nav.Length; i++)
                {
                    nav[i] = new UiNavEntry { Icon = icons[i], Tint = UiRole.None, Label = DealerText.T(TabKeys[NavTab[i]]), Count = counts[i] };
                }
                return nav;
            }

            // A table's page: a head, the table, and a card beside it.
            private void TableShape(UiIcon icon, string title, string sub, UiDataColumn[] columns, List<UiDataRow> rows, List<string> keys, UiFactCardModel side, string emptyTitle, string emptyBody)
            {
                _m.Shape = EstateShape.Table;
                _m.HeadIcon = icon;
                _m.HeadTitle = title;
                _m.HeadSub = sub;
                _m.Table = new UiDataTableModel { Columns = columns, Rows = rows.ToArray(), RowCount = rows.Count, EmptyTitle = emptyTitle, EmptyBody = emptyBody, EmptyIcon = icon };
                _m.RowKeys = keys.ToArray();
                _m.Side = side;
            }

            private void DetailHead(UiIcon icon, string title, string sub, string chip, UiRole tone)
            {
                _m.Shape = EstateShape.Detail;
                _m.Back = DealerText.T("reallife_ui_dealer_back");
                _m.DetailIcon = icon;
                _m.DetailTitle = title;
                _m.DetailSub = sub;
                _m.DetailChip = chip;
                _m.DetailChipTone = tone;
            }

            // The showroom a car bought goes to: the one chosen, if it has room; a step among those with room if there are several.
            private string YardStep(List<UiFactLine> lines)
            {
                var room = new List<DealerYard>(_s.Yards.Count);
                for (int i = 0; i < _s.Yards.Count; i++)
                {
                    if (_s.Room(_s.Yards[i]) > 0)
                    {
                        room.Add(_s.Yards[i]);
                    }
                }
                if (room.Count == 0)
                {
                    return null;
                }
                int at = 0;
                for (int i = 0; i < room.Count; i++)
                {
                    at = room[i].Place == _yardChoice ? i : at;
                }
                _yardChoice = room[at].Place;
                string words = DealerText.T("reallife_dealer_goes_to", "name", room[at].Name, "free", _s.Room(room[at]).ToString(Inv));
                if (room.Count > 1)
                {
                    lines.Add(_runs.Step(DealerText.T("reallife_ui_dealer_goes_to_label"), words, null, at > 0, at < room.Count - 1,
                        () => Show(() => _yardChoice = room[at - 1].Place), () => Show(() => _yardChoice = room[at + 1].Place)));
                }
                else
                {
                    lines.Add(UiFactLine.Say(words));
                }
                return _yardChoice;
            }

            private static string _yardChoice;
        }
    }
}
