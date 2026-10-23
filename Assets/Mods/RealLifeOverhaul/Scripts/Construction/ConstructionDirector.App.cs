using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part for the screen: the Construction app on the phone, the game's notifications that open it on what
    /// they are about, its badge, the words of a site's notice board, and the ribbon on an opening day in the game's own
    /// panel at the place (GameUi.DoorPanel). Once a second: the app put on, and the opening's place looked at while one is on.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        public const int TabOverview = 0, TabLand = 1, TabProjects = 2, TabTenders = 3, TabCrews = 4, TabCompany = 5, TabRivals = 6;

        private static readonly string[] AppGlyphs = { "Icon-Building", "Icon-Edit", "Icon-Info" };
        private const float RibbonReach = 4f;   // m from the opening's place: the panel offers the ribbon
        private const int OpeningFrom = 10, OpeningTo = 18;   // the hours an opening is held

        private ConstructionApp _view;
        private PhoneApp _app;
        private bool _gaveUp;
        private DoorPanel _ribbon;
        private Action _cut;
        private int _ribbonFor;
        private Func<string> _ribbonWhat;

        /// <summary>True while the ribbon's panel is up, and in the frame its key ran: the mod's own interact key stands aside.</summary>
        public static bool TakesInteractKey => Instance != null && Instance._ribbon != null && Instance._ribbon.TakesKey;

        private void AppSecond()
        {
            if (_app == null && !_gaveUp)
            {
                Install();
            }
        }

        private void Install()
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new ConstructionApp(this);
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_bld_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(246, 186, 90, 255), new Color32(196, 112, 28, 255), 0.42f, "AppIcon-Builders"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Construction: the game's phone is not built as this version expects, so the Construction app is left off; the sites and the passes still run.");
                }
                return;
            }
            _view = view;
        }

        private static Sprite Glyph()
        {
            for (int i = 0; i < AppGlyphs.Length; i++)
            {
                Sprite sprite = GameSprites.Get(AppGlyphs[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        private void RemoveApp()
        {
            if (_app != null)
            {
                _app.Remove();
                _app = null;
            }
            _view = null;
        }

        private void AppChanged()
        {
            _view?.Changed();
        }

        /// <summary>Opens the app on a tab with a row picked (0 for none).</summary>
        public void Open(int tab, int pick)
        {
            if (_app != null && _view != null && Running)
            {
                _view.Want(tab, pick);
                _app.Open();
            }
        }

        /// <summary>A notice of the game's that opens the app on what it is about: the project's or crew's name, a second text, a count.</summary>
        private void Tell(NotificationType type, string key, string name, string key2, int tab, int pick, int count = 0)
        {
            var data = new Dictionary<string, string>(4) { { "name", name ?? string.Empty } };
            if (key2 != null)
            {
                data["what"] = ConstructionText.T(key2);
            }
            data["count"] = count.ToString(CultureInfo.InvariantCulture);
            ConstructionGame.Tell(type, key, data, () => Open(tab, pick));
        }

        // ------------------------------------------------------------------ the notice board

        /// <summary>A site's notice board's words, when it is the player's company's works.</summary>
        public string NoticeText(ProjectFile p)
        {
            SiteFacts site = SiteOf(p.SiteId);
            int percent = site.Known && site.Hours > 0f ? Mathf.Clamp(Mathf.RoundToInt(site.Done / site.Hours * 100f), 0, 100) : 0;
            WorkStage stage = (WorkStage)Math.Max(0, p.StageSeen);
            int due = DueInspection(p), failed = FailedInspection(p);
            string next = p.Rework > 0 ? ConstructionText.T("reallife_bld_next_rework")
                : failed >= 0 ? ConstructionText.T("reallife_bld_next_failed", "what", ConstructionText.T(BuildCatalogue.InspectionKeys[failed]))
                : due >= 0 ? ConstructionText.T("reallife_bld_next_book", "what", ConstructionText.T(BuildCatalogue.InspectionKeys[due]))
                : !SiteRules.Supplied(p.Have, p.Need) ? ConstructionText.T(p.OrderDay > 0 ? "reallife_bld_next_delivery" : "reallife_bld_next_order")
                : _state.CrewsOn(p.Id) == 0 ? ConstructionText.T("reallife_bld_next_crew") : ConstructionText.T("reallife_bld_next_working");
            return ConstructionText.Fill("reallife_bld_board", "company", NoticeName(p.SiteId) ?? string.Empty, "project", ProjectName(p),
                "stage", ConstructionText.T(BuildCatalogue.StageKeys[(int)stage]), "percent", percent.ToString(), "crews", _state.CrewsOn(p.Id).ToString(), "next", next);
        }

        // ------------------------------------------------------------------ the ribbon

        // Once a second while running: on an opening day between ten and six, the panel at the building's front while the
        // player stands there; the opening held without them once six has passed.
        private void SitesSecond()
        {
            int day = ConstructionGame.Day, hour = ConstructionGame.Hour;
            ProjectFile opening = null;
            for (int i = 0; i < _state.Projects.Count && opening == null; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (p.Stage == ProjectStage.Finished && !p.Opened && p.Opening == day)
                {
                    opening = p;
                }
            }
            if (opening == null || (hour < OpeningFrom && !_testOpening) || !ConstructionGame.Outdoors)
            {
                HidePanels();
                return;
            }
            if (hour >= OpeningTo && !_testOpening)
            {
                Open(opening, false);
                return;
            }
            if (!OpeningPlace(opening, out Vector3 place))
            {
                HidePanels();
                return;
            }
            Vector3 player = ConstructionGame.Player;
            Vector3 flat = place - player;
            flat.y = 0f;
            if (flat.sqrMagnitude > RibbonReach * RibbonReach)
            {
                HidePanels();
                return;
            }
            if (_ribbon == null)
            {
                _ribbon = new DoorPanel(Logger, "Construction check (ribbon panel)");
                _cut = _ribbon.Press(() => CutRibbon(_ribbonFor));
                _ribbonWhat = () => "opening day at project " + _ribbonFor;
            }
            if (_ribbonFor != opening.Id || !_ribbon.Up)
            {
                _ribbonFor = opening.Id;
                WorldPanel panel = _ribbon.Panel;
                panel.Lines(ConstructionText.T("reallife_bld_ribbon_title", "name", ProjectName(opening)), ConstructionText.T("reallife_bld_ribbon_line"), null, WorldPanel.White());
                panel.Button(0, "reallife_bld_ribbon_cut", _cut, true, true);
                panel.Button(1, null, null, false, false);
                panel.Button(2, null, null, false, false);
                panel.Button(3, null, null, false, false);
                panel.Follow = null;
            }
            _ribbon.Show(place, player, 2.1f, _ribbonWhat);
        }

        // Where an opening is held: a plot's front on its street, a rebuilt building's door.
        private bool OpeningPlace(ProjectFile p, out Vector3 place)
        {
            place = Vector3.zero;
            if (!string.IsNullOrEmpty(p.Lot) && Lot(p.Lot, out LotView lot))
            {
                double a = lot.Yaw * Math.PI / 180.0;
                float half = lot.Depth * 0.5f + 0.8f;   // just out in front of the land's front, on the pavement
                place = new Vector3(lot.X - (float)Math.Sin(a) * half, lot.Y, lot.Z - (float)Math.Cos(a) * half);
                return true;
            }
            if (ConstructionGame.Door(p.Street, p.Number, out Vector3 door, out Vector3 outward))
            {
                place = door + outward * 1.2f;
                return true;
            }
            return false;
        }

        private void HidePanels()
        {
            _ribbon?.Hide("no opening here now");
        }
    }
}
