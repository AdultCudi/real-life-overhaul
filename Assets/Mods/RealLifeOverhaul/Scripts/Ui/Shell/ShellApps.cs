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
    /// The frame's apps from the game: every button of the full menu's own row (Topbar/AppButtons, in the order the row's
    /// pager keeps: the game's, the mod's by PagerRules.ModOrder, then other mods'), the overhaul's pages opened from All
    /// apps (ShellPages), each button's news from the HUD phone's button of the same name, and the folder the player keeps
    /// it in (PhoneLayoutPrefs). Pressing presses the app's own full-menu button, so the game's ShowApp, the mod's
    /// PhoneApp.Open and other mods' handlers run as a click would; Voogle Maps, which has no full-menu button, through
    /// the HUD phone's. While the kit phone stands, the list is the phone's own model instead (<see cref="FromPhone"/>).
    /// Cost: a walk of the row's buttons when the full menu opens and once a second while it shows.
    /// </summary>
    public static class ShellApps
    {
        private const int RowMost = PagerRules.PhoneTiles;   // the phone's first home page: the app row's length
        private static readonly AppName[] GameOrder = (AppName[])Enum.GetValues(typeof(AppName));
        private static readonly string[] FirstOnHome = { "RealLifePlanner", "RealLifeFlare" };   // the old dock: first on the phone's home
        private static readonly List<Transform> Buttons = new List<Transform>(64);
        private static readonly Dictionary<string, Transform> HudByName = new Dictionary<string, Transform>(64, StringComparer.Ordinal);

        /// <summary>
        /// Fills the frame's list from the phone's own model while the kit phone stands (UiShellApps.FromPhone), then the
        /// overhaul's pages; without it (its switch off, or stood down to the classic phone) the full menu's row as before.
        /// Returns the version, which moves with the phone's and the pages'.
        /// </summary>
        public static int FromPhone(List<UiShellApp> into)
        {
            PhoneItems items = PhoneItems.Current;
            if (items == null || items.Count == 0)
            {
                return FromRow(into);
            }
            int hash = UiShellApps.FromPhone(items, into, RowMost);
            ShellPages.Add(into, ref hash);
            return hash;
        }

        /// <summary>Fills the frame's list from the full menu's row; returns its version, which moves with any app, title or badge.</summary>
        public static int FromRow(List<UiShellApp> into)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            Transform row = Row(uis);
            if (row == null)
            {
                return 0;
            }
            Hud(uis);
            Buttons.Clear();
            for (int i = 0; i < row.childCount; i++)
            {
                Transform b = row.GetChild(i);
                if (b.gameObject.activeSelf || PagerHid(b))
                {
                    Buttons.Add(b);
                }
            }
            int hash = 17;
            // The phone's home as it stands first: the old dock's two, then the game's eight in their order (the map from the
            // HUD phone, as it has no full-menu button); as many as its first page holds make the row.
            int inRow = 0;
            for (int f = 0; f < FirstOnHome.Length; f++)
            {
                inRow += Add(into, Find(FirstOnHome[f]), inRow < RowMost, ref hash) ? 1 : 0;
            }
            for (int g = 0; g < GameOrder.Length; g++)
            {
                string name = GameOrder[g].ToString();
                if (GameOrder[g] == AppName.VoogleMaps)
                {
                    if (HudByName.TryGetValue(name, out Transform map))
                    {
                        Insert(into, name, map, inRow < RowMost, ref hash);
                        inRow++;
                    }
                    continue;
                }
                inRow += Add(into, Find(name), inRow < RowMost, ref hash) ? 1 : 0;
            }
            for (int i = 0; i < Buttons.Count; i++)
            {
                Transform b = Buttons[i];
                if (Array.IndexOf(FirstOnHome, b.name) < 0 && !Enum.TryParse(b.name, out AppName _))
                {
                    Add(into, b, false, ref hash);
                }
            }
            ShellPages.Add(into, ref hash);
            return hash;
        }

        /// <summary>Presses an app's own button by its id: true when there was one to press.</summary>
        public static bool Press(string id)
        {
            if (ShellPages.Open(id))
            {
                return true;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            Transform row = Row(uis);
            Transform b = row != null ? row.Find(id) : null;
            if (b != null && b.GetComponent<Button>() is Button button && button.interactable)
            {
                button.onClick.Invoke();
                return true;
            }
            Hud(uis);
            if (HudByName.TryGetValue(id, out Transform hud) && hud.GetComponent<Button>() is Button phone && phone.interactable)
            {
                if (FullMenu.IsOpen)
                {
                    uis.fullMenu.CloseFullMenu();   // the map opens over the city, as the game's own View on map leaves the menu
                }
                phone.onClick.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>The app the full menu shows now: its panel's name among the apps, or null.</summary>
        public static string Open()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            Transform apps = uis != null && uis.fullMenu != null ? uis.fullMenu.appsContainer : null;
            for (int i = 0; apps != null && i < apps.childCount; i++)
            {
                if (apps.GetChild(i).gameObject.activeSelf)
                {
                    return apps.GetChild(i).name;
                }
            }
            return null;
        }

        private static Transform Row(UIs uis)
        {
            Transform canvas = uis != null && uis.fullMenu != null && uis.fullMenu.canvasGroup != null ? uis.fullMenu.canvasGroup.transform : null;
            return canvas != null ? canvas.Find("Topbar/AppButtons") : null;
        }

        // The HUD phone's buttons by name (its grid holds the game's, the mod's and other mods'), for their news.
        private static void Hud(UIs uis)
        {
            HudByName.Clear();
            SmartphoneUI phone = uis != null ? uis.smartphoneUI : null;
            if (phone == null || phone.appButtons == null || !phone.appButtons.TryGetValue(AppName.Persona, out SmartphoneAppButton persona) || persona == null)
            {
                return;
            }
            Transform grid = persona.transform.parent;
            for (int i = 0; grid != null && i < grid.childCount; i++)
            {
                Transform b = grid.GetChild(i);
                if (b.GetComponent<Button>() != null && !HudByName.ContainsKey(b.name))
                {
                    HudByName.Add(b.name, b);
                }
            }
        }

        // A button the row's pager put on another page (switched off), or the row's own: the mod's tiles and the template are not apps.
        private static bool PagerHid(Transform b) => b.GetComponent<FullMenuAppButton>() != null && !b.name.StartsWith(UiHostRules.ModPrefix + " ", StringComparison.Ordinal)
                                                     && b.name != "AppButtonTemplate";

        private static Transform Find(string id)
        {
            for (int i = 0; i < Buttons.Count; i++)
            {
                if (Buttons[i].name == id)
                {
                    return Buttons[i];
                }
            }
            return null;
        }

        private static bool Add(List<UiShellApp> into, Transform b, bool inRow, ref int hash)
        {
            if (b == null || b.GetComponent<FullMenuAppButton>() == null || b.name == "AppButtonTemplate" || b.name.StartsWith(UiHostRules.ModPrefix + " ", StringComparison.Ordinal))
            {
                return false;
            }
            UiShellKind kind = Enum.TryParse(b.name, out AppName _) ? UiShellKind.Game : PagerRules.ModRank(b.name) >= 0 ? UiShellKind.Mod : UiShellKind.Other;
            string title = UiWidgets.Label(b);
            Append(into, b.name, string.IsNullOrEmpty(title) ? UiText.T("reallife_ui_shell_another_app") : title, kind, inRow, ref hash);
            return true;
        }

        private static void Insert(List<UiShellApp> into, string id, Transform hud, bool inRow, ref int hash)
        {
            string title = UiWidgets.Label(hud);
            Append(into, id, string.IsNullOrEmpty(title) ? UiText.T("reallife_ui_shell_another_app") : title, UiShellKind.Game, inRow, ref hash);
        }

        /// <summary>Adds an entry made elsewhere (the overhaul's pages), with its version folded into the fill's.</summary>
        internal static void Append(List<UiShellApp> into, string id, string title, UiShellKind kind, int folder, ref int hash)
        {
            UiIcon icon = UiShellApps.IconOf(id, kind);
            into.Add(new UiShellApp { Id = id, Title = title, Icon = icon, Tint = UiIconTints.Of(icon), Kind = kind, Folder = folder });
            hash = unchecked(hash * 31 + id.GetHashCode());
        }

        private static void Append(List<UiShellApp> into, string id, string title, UiShellKind kind, bool inRow, ref int hash)
        {
            int badge = Badge(id);
            int place = kind == UiShellKind.Mod && PhoneLayoutPrefs.On && PhoneLayoutPrefs.Layout != null ? PhoneLayoutPrefs.Layout.Place(id) : PhoneLayoutRules.Home;
            UiIcon icon = UiShellApps.IconOf(id, kind);
            into.Add(new UiShellApp
            {
                Id = id, Title = title, Icon = icon, Tint = UiIconTints.Of(icon), Badge = badge, Kind = kind, InRow = inRow,
                Folder = UiShellApps.FolderOf(id, kind, place, PhoneLayoutRules.DefaultOf(id))
            });
            hash = unchecked(hash * 31 + id.GetHashCode()) * 31 + badge * 7 + (inRow ? 1 : 0);
            hash = unchecked(hash * 31 + title.GetHashCode());
        }

        // The news on the HUD phone's button of that name: its badge up and its number (a badge up with none reads 1).
        private static int Badge(string id)
        {
            if (!HudByName.TryGetValue(id, out Transform b))
            {
                return 0;
            }
            Transform badge = b.Find("Badge");
            if (badge == null || !badge.gameObject.activeSelf)
            {
                return 0;
            }
            string words = UiWidgets.Label(badge);
            if (string.IsNullOrEmpty(words))
            {
                return 1;
            }
            return int.TryParse(words.TrimEnd('+'), out int n) ? Math.Max(1, n) : 1;
        }
    }
}
