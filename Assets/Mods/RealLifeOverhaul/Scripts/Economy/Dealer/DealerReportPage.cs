using System;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>The Reports tab's page in place of the table, as Markets/MarketPage is: a title, the chart and the choices under it.</summary>
    internal sealed class DealerReportPage
    {
        private static readonly string[] KindKeys =
        {
            "reallife_dealer_report_sold", "reallife_dealer_report_revenue", "reallife_dealer_report_profit", "reallife_dealer_report_age", "reallife_dealer_report_visitors",
            "reallife_dealer_report_conversion", "reallife_dealer_report_arrears"
        };

        private readonly DealerApp _app;
        private AppShell _shell;
        private RectTransform _root, _view, _controls;
        private TMP_Text _title, _sub;
        private DealerChart _chart;
        private readonly long[] _values = new long[DealerCatalogue.ReportDays], _average = new long[DealerCatalogue.ReportDays];
        private readonly bool[] _have = new bool[DealerCatalogue.ReportDays];
        private readonly DealerBook _scratch = new DealerBook();

        public ReportKind Kind { get; private set; } = ReportKind.Profit;
        public string Place { get; private set; }   // the showroom shown; null for all of them

        private DealerReportPage(DealerApp app)
        {
            _app = app;
        }

        public static DealerReportPage Make(AppShell shell, DealerApp app)
        {
            try
            {
                var page = new DealerReportPage(app) { _shell = shell };
                var list = (RectTransform)shell.List;
                var go = new GameObject("ReportPage", typeof(RectTransform));
                go.transform.SetParent(list.parent, false);
                var root = (RectTransform)go.transform;
                root.anchorMin = list.anchorMin;
                root.anchorMax = list.anchorMax;
                root.pivot = list.pivot;
                root.anchoredPosition = list.anchoredPosition;
                root.sizeDelta = list.sizeDelta;
                Transform background = list.Find("ListBackground");
                if (background != null)
                {
                    GameObject copy = UnityEngine.Object.Instantiate(background.gameObject, root, false);
                    var rect = (RectTransform)copy.transform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    // The game keeps this background switched off (as MyEmployees' prefab has it), so the copy is switched on here,
                    // white as the box whose lines and selectors the page is made of; a click on it falls through.
                    var sheet = copy.GetComponent<Image>();
                    if (sheet != null)
                    {
                        sheet.color = Color.white;
                        sheet.raycastTarget = false;
                    }
                    copy.SetActive(true);
                }
                else
                {
                    var image = go.AddComponent<Image>();
                    image.color = Color.white;
                }
                page._root = root;
                page._title = Free(shell.Box.Title(string.Empty, root), new Vector2(28f, -14f), new Vector2(-56f, 56f));
                page._sub = Free(shell.Box.Line(string.Empty, root), new Vector2(28f, -70f), new Vector2(-56f, 40f));
                page._view = Area(root, "View", new Vector2(18f, 104f), new Vector2(-18f, -116f));
                page._chart = DealerChart.Make(page._view, parent => shell.Box.Line(string.Empty, parent));
                page._controls = Area(root, "Controls", new Vector2(18f, 12f), new Vector2(-18f, 0f));
                page._controls.anchorMax = new Vector2(1f, 0f);
                page._controls.sizeDelta = new Vector2(-36f, 84f);
                page._controls.pivot = new Vector2(0.5f, 0f);
                page._controls.anchoredPosition = new Vector2(0f, 10f);
                var row = page._controls.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.childControlWidth = true;
                row.childControlHeight = true;
                row.childForceExpandWidth = true;
                row.childForceExpandHeight = false;
                row.spacing = 18f;
                row.childAlignment = TextAnchor.MiddleCenter;
                go.SetActive(false);
                return page;
            }
            catch (Exception e)
            {
                DealerApp.Logger?.Error(e);
                UiClone.Fallback("the Car Showroom app's report page could not be built");
                return null;
            }
        }

        // A text of the box's lifted out of the box's layout and placed by hand along the top.
        private static TMP_Text Free(TMP_Text text, Vector2 at, Vector2 size)
        {
            var layout = text.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = true;
            }
            var fitter = text.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
            }
            var rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static RectTransform Area(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.gameObject.SetActive(false);
            }
            if (_shell != null && _shell.List != null)
            {
                _shell.List.gameObject.SetActive(true);
            }
        }

        /// <summary>The page shown and written: the figure chosen, of the showroom chosen, over the days kept.</summary>
        public void Show(DealerState s, int today)
        {
            _shell.List.gameObject.SetActive(false);
            _root.gameObject.SetActive(true);
            DealerYard yard = Place != null ? s.Yard(Place) : null;
            if (yard == null)
            {
                Place = null;
            }
            UiClone.Words(_title, DealerText.T(KindKeys[(int)Kind]) + "   <size=70%>" + (yard != null ? yard.Name : DealerText.T("reallife_dealer_report_all")) + "</size>");
            UiClone.Words(_sub, DealerText.T("reallife_dealer_report_sub", "days", DealerCatalogue.ReportDays.ToString(), "average", DealerCatalogue.AverageDays.ToString()));
            Controls(s);
            int first = DealerReport.Series(s, Place, Kind, today, DealerCatalogue.ReportDays, _values, _have, _scratch);
            DealerReport.Average(_values, _have, DealerCatalogue.ReportDays, _average);
            _chart.Show(_values, _have, _average, DealerCatalogue.ReportDays, first, Kind, DealerText.T("reallife_dealer_report_average", "days", DealerCatalogue.AverageDays.ToString()),
                DealerText.T("reallife_dealer_report_none"));
        }

        // The choices under the chart: the figure, and the showroom (all of them first).
        private void Controls(DealerState s)
        {
            for (int i = _controls.childCount - 1; i >= 0; i--)
            {
                GameObject child = _controls.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
            InfoBox box = _shell.Box;
            box.Selector(KindKeys.Length, (int)Kind, i => DealerText.T(KindKeys[i]), i => UiClone.Safe(() =>
            {
                Kind = (ReportKind)i;
                _app.Changed();
            }), _controls);
            int at = 0;
            for (int i = 0; i < s.Yards.Count; i++)
            {
                at = s.Yards[i].Place == Place ? i + 1 : at;
            }
            if (s.Yards.Count > 1)
            {
                box.Selector(s.Yards.Count + 1, at, i => i == 0 ? DealerText.T("reallife_dealer_report_all") : s.Yards[i - 1].Name, i => UiClone.Safe(() =>
                {
                    Place = i == 0 ? null : s.Yards[i - 1].Place;
                    _app.Changed();
                }), _controls);
            }
        }
    }
}
