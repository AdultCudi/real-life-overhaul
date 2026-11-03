using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The full menu's frame on the RLO interface. While an app of the full menu is drawn by the kit (a game app through
    /// its host, a mod app through PhoneAppDef.Kit), the frame owns the menu's chrome: the menu's own backdrop picture,
    /// its top bar and its bottom bar go unseen (their leaves by a group of their own, the top bar's own picture and its
    /// row's buttons by their renderers, since other mods fill that row), so the paused city shows round the kit's window,
    /// and the status bar under the window carries the app row (the phone's first home page, then All apps), pressing
    /// each app's own full-menu button. All apps rises from its tile, docked above the status bar at the left. An app the
    /// kit does not draw yet keeps its own page in the game's chrome, the kit's window and bar gone, so nothing doubles;
    /// another mod's full-menu page is such a page. The menu's pause, its events and the game's own close (with BizMan's
    /// unsaved-changes question) stay the game's. Cost: nothing while the full menu is closed; while it shows a kit app,
    /// the apps read once a second for their news, and the chrome's pieces collected once each time it is taken.
    /// </summary>
    public static class UiShell
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "UiShell", typeof(UiShell));
        private static readonly UiConceal Chrome = new UiConceal();
        private static readonly List<Graphic> Graphics = new List<Graphic>(64);
        private static readonly List<UiGameHost> Faces = new List<UiGameHost>(8);
        private static readonly string[] BottomLeaves = { "BottomBar/LeftButtons/Help", "BottomBar/LeftButtons/SubmitBugReport", "BottomBar/RightButtons/Label" };
        private static readonly string[] TopLeaves = { "Topbar/Splitter", "Topbar/AppName", "Topbar/RightData" };

        private static Action _changed;
        private static GameObject _appsRoot;
        private static UiHost _appsHost;
        private static AllAppsScreen _apps;
        private static Canvas _appsCanvas;
        private static bool _taken, _appsOpen;

        /// <summary>True while the frame owns the full menu's chrome: a kit app shows in it.</summary>
        public static bool Taken => _taken;

        /// <summary>True while All apps stands over the window.</summary>
        public static bool AppsOpen => _appsOpen;

        /// <summary>Wires the frame to the hosts and the apps' source; once, when the interface starts.</summary>
        public static void Start()
        {
            _changed = Guard.Wrap(Apply);
            UiGameHost.FacesChanged = _changed;
            UiGameHost.Chrome = Press;
            UiShellApps.Source = ShellApps.FromPhone;
            UiShellApps.Status = Line;
        }

        // The line an app gives on All apps: the game's Contacts its unread messages, as its badge counts them.
        private static string Line(string id, int badge)
        {
            return id == "Contacts" && badge > 0 ? UiText.Fill("reallife_ui_shell_status_unread", "count", UiText.Whole(badge)) : null;
        }

        /// <summary>
        /// A mod app drawn on the kit: after PhoneApp.Install, an app whose definition has a Kit gives it here once, and the
        /// frame draws that screen over its panel whenever the interface and the screen's own switch are on.
        /// </summary>
        public static void Face(PhoneApp app, PhoneAppDef def)
        {
            if (app == null || def == null || def.Kit == null || string.IsNullOrEmpty(def.KitScreen))
            {
                return;
            }
            PhoneApp held = app;
            Faces.Add(new UiGameHost(new ModAppScreen(def.Id, def.KitScreen, def.TitleKey, def.KitIcon, () => held.Root, def.Kit)));
        }

        /// <summary>
        /// A mod app's panel was just built, asleep, on its first opening (PhoneApp.Ready): its face binds over it at once, when the
        /// interface and the screen's own switch are on, so the game's Select that follows shows the kit alone. Waiting for the next
        /// second's <see cref="Tick"/> showed the app's old panel for up to a second first. One path for every app with a Kit.
        /// </summary>
        public static void Built(string screenId)
        {
            if (!UiDirector.On || !UiScreenSwitches.On(screenId))
            {
                return;
            }
            for (int i = 0; i < Faces.Count; i++)
            {
                if (Faces[i].Id == screenId)
                {
                    Faces[i].Install();
                }
            }
            ShellPages.Built(screenId);
        }

        /// <summary>Once a second while the interface is on: the mod apps' faces put on, the apps' news read while the frame shows.</summary>
        public static void Tick()
        {
            for (int i = 0; i < Faces.Count; i++)
            {
                if (UiScreenSwitches.On(Faces[i].Id))
                {
                    Faces[i].Install();
                }
                else
                {
                    Faces[i].Remove("its switch is off");
                }
            }
            if (_taken && UiShellApps.Refresh())
            {
                UiGameHost.AgainAll();
            }
        }

        /// <summary>The frame given back: the chrome seen again, All apps gone. The city unloaded, the interface off, a fault.</summary>
        public static void Remove(string why)
        {
            CloseApps();
            Give();
            for (int i = 0; i < Faces.Count; i++)
            {
                Faces[i].Remove(why);
            }
            if (_appsRoot != null)
            {
                UnityEngine.Object.Destroy(_appsRoot);
                _appsRoot = null;
                _appsHost = null;
                _appsCanvas = null;
            }
        }

        /// <summary>A new city: the mod apps' faces looked at again.</summary>
        public static void Rearm()
        {
            for (int i = 0; i < Faces.Count; i++)
            {
                Faces[i].Rearm();
            }
        }

        /// <summary>Opens All apps on a folder (-1: the open app's), or closes it when it is up. False when the full menu is not the kit's.</summary>
        public static bool ToggleApps(int folder)
        {
            if (_appsOpen)
            {
                CloseApps();
                return true;
            }
            if (!_taken || !UiScreenSwitches.On("all_apps"))
            {
                return false;
            }
            Bind();
            if (_appsHost == null)
            {
                return false;
            }
            UiShellApps.Refresh();
            _apps.Show(folder, ShellApps.Open());
            _appsCanvas.overrideSorting = true;
            _appsCanvas.sortingOrder = UiGameHost.AppOrder() + 1;
            _appsRoot.SetActive(true);
            _appsRoot.transform.SetAsLastSibling();
            _appsHost.Show();
            _appsOpen = true;
            UiBoxes.Over = true;
            UiGameHost.AgainAll();   // the tile shows the panel is up
            return true;
        }

        // The status bar's targets and a window's close at its top level, from whichever kit screen the full menu shows.
        private static bool Press(int command, int arg)
        {
            try
            {
                if (command == UiDraw.CloseCommand)
                {
                    return CloseMenu();
                }
                if (command == UiDraw.AllAppsCommand)
                {
                    return ToggleApps(-1);
                }
                if (command == UiDraw.AppRowCommand)
                {
                    return OpenRow(arg);
                }
                return false;
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, null);
                return false;
            }
        }

        // The row's arg-th app: its own button pressed (a folder's entry opens All apps there).
        private static bool OpenRow(int index)
        {
            int seen = 0;
            for (int i = 0; i < UiShellApps.Items.Count; i++)
            {
                if (!UiShellApps.Items[i].InRow)
                {
                    continue;
                }
                if (seen++ == index)
                {
                    CloseApps();
                    return ShellApps.Press(UiShellApps.Items[i].Id);
                }
            }
            return false;
        }

        // The game's own close: its BizMan question about unsaved changes is kept.
        private static bool CloseMenu()
        {
            if (_appsOpen)
            {
                CloseApps();
                return true;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.fullMenu == null || !FullMenu.IsOpen)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                uis.fullMenu.CloseFullMenu();   // the game's own close, and all it sets off: the game's work
            }
            return true;
        }

        private static void CloseApps()
        {
            if (!_appsOpen)
            {
                return;
            }
            _appsOpen = false;
            UiBoxes.Over = false;
            if (_appsHost != null)
            {
                _appsHost.Hide();
                _appsRoot.SetActive(false);
            }
            UiGameHost.AgainAll();
        }

        // A kit app came or went in the full menu: the chrome taken or given back.
        private static void Apply()
        {
            bool want = UiGameHost.AnyInFullMenu;
            if (want && !_taken)
            {
                Take();
            }
            else if (!want && _taken)
            {
                CloseApps();
                Give();
            }
        }

        // The menu's backdrop picture, its top bar and bottom bar unseen; collected each time, so a button another mod added since is in.
        private static void Take()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            Transform canvas = uis != null && uis.fullMenu != null && uis.fullMenu.canvasGroup != null ? uis.fullMenu.canvasGroup.transform : null;
            if (canvas == null)
            {
                return;
            }
            Chrome.Clear();
            Chrome.Add(canvas.Find("RaycastBlocker"), true);   // the menu's still picture; it still holds clicks off the world under it
            Transform top = canvas.Find("Topbar");
            if (top != null)
            {
                Chrome.Add(top.GetComponent<Graphic>());   // the bar's own tint, never a group: other mods fill the bar
                for (int i = 0; i < TopLeaves.Length; i++)
                {
                    Chrome.Add(canvas.Find(TopLeaves[i]));
                }
                for (int i = 0; i < top.childCount; i++)
                {
                    Transform child = top.GetChild(i);
                    if (child.name.StartsWith(UiHostRules.ModPrefix, StringComparison.Ordinal))
                    {
                        Chrome.Add(child);   // the mod's own More apps tile beside the row
                    }
                }
                Transform row = top.Find("AppButtons");
                if (row != null)
                {
                    Graphics.Clear();
                    row.GetComponentsInChildren(true, Graphics);
                    for (int i = 0; i < Graphics.Count; i++)
                    {
                        Chrome.Add(Graphics[i]);   // the app row replaces this one: every button in it, other mods' too, is on the kit's row
                    }
                }
            }
            for (int i = 0; i < BottomLeaves.Length; i++)
            {
                Chrome.Add(canvas.Find(BottomLeaves[i]));
            }
            Chrome.Hide();
            _taken = true;
            UiShellApps.Refresh();
            UiGameHost.AgainAll();
            if (Meta.Dev.Verbose)
            {
                UiDirector.Logger?.Info("UI: the full menu's frame is the kit's (" + Chrome.Count + " pieces of the menu's chrome unseen, " + UiShellApps.Items.Count
                                        + " apps on the row and All apps).");
            }
        }

        private static void Give()
        {
            if (!_taken)
            {
                return;
            }
            Chrome.Show();
            _taken = false;
        }

        // All apps' own canvas, under the full menu's, made once a city.
        private static void Bind()
        {
            if (_appsRoot != null)
            {
                return;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            Transform canvas = uis != null && uis.fullMenu != null && uis.fullMenu.canvasGroup != null ? uis.fullMenu.canvasGroup.transform : null;
            if (canvas == null)
            {
                return;
            }
            _appsRoot = new GameObject("RealLife Interface All apps", typeof(RectTransform));
            var rect = (RectTransform)_appsRoot.transform;
            rect.SetParent(canvas, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _appsCanvas = _appsRoot.AddComponent<Canvas>();
            _appsRoot.AddComponent<GraphicRaycaster>();
            var blocker = _appsRoot.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0f);   // a click beside the panel lands here, not on the window under it
            _apps = new AllAppsScreen { Close = CloseApps, Opening = ShellApps.Press };
            _appsHost = _appsRoot.AddComponent<UiHost>();
            _appsHost.Bind(_apps, UiGame.Instance, UiGame.Instance, UiDirector.Dark, UiDirector.Motion, UiFrame.MastheadHeight, UiFrame.BandHeight,
                UiText.T("reallife_ui_shell_all_apps"), UiIcon.Grid);
            _appsHost.View.Scale = UiTokens.CanvasPerPx;
            _appsHost.Chrome = Press;
            _appsHost.Failed = e => Remove("a fault of All apps");
            _appsRoot.SetActive(false);
        }
    }
}
