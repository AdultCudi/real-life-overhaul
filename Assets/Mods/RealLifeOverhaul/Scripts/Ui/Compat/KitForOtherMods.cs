using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The small, stable way for another author's mod to fit the RLO interface (docs/reference/kit-for-other-mods.md). It is
    /// optional: a mod that never calls it is shown and held by the kit all the same (its phone apps in the app grid, All apps
    /// and the full menu's row, its tabs and pieces on the game's panels, its windows over the kit). These calls are for a mod
    /// that wants more, and each is safe to call at any time (before the city, twice, with a null): a call the kit cannot
    /// serve returns false and does nothing.
    /// <list type="bullet">
    /// <item><see cref="IsOn"/> and <see cref="IsScreenOn"/> ask whether the kit is drawing.</item>
    /// <item><see cref="RegisterApp"/> puts an app of the mod's on the phone, in the kit's grid with the icon the mod gives (or a
    /// generated one), its panel a plain Unity panel the mod builds, or a screen drawn with the kit's parts.</item>
    /// <item><see cref="AddPage"/> adds a page to BizMan's sidebar.</item>
    /// <item><see cref="AddSetting"/> adds an on/off row to Mod options, under Other mods.</item>
    /// </list>
    /// Everything the mod hands over (its build, its screen, its callbacks) runs inside the kit's guard: a fault of the mod's is
    /// blamed on it by name in Player.log and the screen it came through is the game's own for the session; the rest of the
    /// interface goes on. Cost: a list walk once a second to put a registered app on the phone; nothing a frame.
    /// </summary>
    public static class KitForOtherMods
    {
        /// <summary>The interface of this class; it only grows (a mod asking for a higher one than this has an older RLO).</summary>
        public const int ApiVersion = 1;

        /// <summary>The kit's screen id for the apps registered here, which a fault's breaker is kept under.</summary>
        private const string ScreenPrefix = "modapp_";

        private sealed class AppEntry
        {
            public string Id, Name;
            public Sprite Icon;
            public Func<Transform, GameObject> Panel;
            public Func<IUiScreen> Screen;
            public PhoneApp App;

            public string ScreenId => ScreenPrefix + Id;
        }

        /// <summary>A page another mod added to BizMan's sidebar.</summary>
        public sealed class Page
        {
            public string Id, Title;
            public Action Open;
        }

        /// <summary>An on/off row another mod added to Mod options.</summary>
        public sealed class Setting
        {
            public string Id, Label;
            public Func<bool> Get;
            public Action<bool> Set;
        }

        private static readonly List<AppEntry> Apps = new List<AppEntry>(4);
        private static readonly List<Page> BizPages = new List<Page>(4);
        private static readonly List<Setting> SettingList = new List<Setting>(4);

        /// <summary>The pages other mods added to BizMan's sidebar, in the order added.</summary>
        public static IReadOnlyList<Page> PagesOfBizMan => BizPages;

        /// <summary>The rows other mods added to Mod options, in the order added.</summary>
        public static IReadOnlyList<Setting> SettingRows => SettingList;

        /// <summary>True while the RLO interface is drawing the game's screens (the mod's interface switch and Living population on).</summary>
        public static bool IsOn => UiDirector.On;

        /// <summary>True while the kit draws one screen ("bizman", "contacts", "phone", "hud" ...; the ids Mod options lists): its switch on and its breaker closed.</summary>
        public static bool IsScreenOn(string screenId) => UiDirector.On && !string.IsNullOrEmpty(screenId) && UiScreenSwitches.On(screenId);

        /// <summary>
        /// Puts an app of the mod's on the phone: its tile in the kit's grid, the app row and All apps (Other mods), opening its
        /// panel as the game opens any app. <paramref name="id"/> names its objects and must be unique and no game app's name;
        /// <paramref name="name"/> is its title (a locale key of the game's, or the words themselves); <paramref name="icon"/> its
        /// picture (null: a tile the kit paints); <paramref name="panel"/> builds a plain Unity panel under the holder it is given
        /// (null when <paramref name="screen"/> is given); <paramref name="screen"/> a screen drawn with the kit's parts, which
        /// the kit draws over an empty panel while the interface is on. False when it cannot be registered (a name already taken).
        /// </summary>
        public static bool RegisterApp(string id, string name, Sprite icon, Func<Transform, GameObject> panel, Func<IUiScreen> screen = null)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name) || (panel == null && screen == null))
            {
                return false;
            }
            for (int i = 0; i < Apps.Count; i++)
            {
                if (Apps[i].Id == id)
                {
                    return false;
                }
            }
            Apps.Add(new AppEntry { Id = id, Name = name, Icon = icon, Panel = panel, Screen = screen });
            return true;
        }

        /// <summary>Adds a page to BizMan's sidebar: its title, and what runs when the player chooses it (the mod opens its own window or panel).</summary>
        public static bool AddPage(string appId, string pageId, string title, Action open)
        {
            if (appId != "bizman" || string.IsNullOrEmpty(pageId) || string.IsNullOrEmpty(title) || open == null)
            {
                return false;
            }
            for (int i = 0; i < BizPages.Count; i++)
            {
                if (BizPages[i].Id == pageId)
                {
                    return false;
                }
            }
            BizPages.Add(new Page { Id = pageId, Title = title, Open = open });
            return true;
        }

        /// <summary>Adds an on/off row to Mod options under Other mods: its label, what it reads and what it does when the player flips it.</summary>
        public static bool AddSetting(string id, string label, Func<bool> get, Action<bool> set)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(label) || get == null || set == null)
            {
                return false;
            }
            for (int i = 0; i < SettingList.Count; i++)
            {
                if (SettingList[i].Id == id)
                {
                    return false;
                }
            }
            SettingList.Add(new Setting { Id = id, Label = label, Get = get, Set = set });
            return true;
        }

        /// <summary>What is registered, for the self-check's line: one text for each app, page and row.</summary>
        internal static IEnumerable<string> Lines()
        {
            for (int i = 0; i < Apps.Count; i++)
            {
                yield return "app " + Apps[i].Id + (Apps[i].Screen != null ? " (kit screen)" : " (panel)");
            }
            for (int i = 0; i < BizPages.Count; i++)
            {
                yield return "BizMan page " + BizPages[i].Id;
            }
            for (int i = 0; i < SettingList.Count; i++)
            {
                yield return "setting " + SettingList[i].Id;
            }
        }

        // ------------------------------------------------------------------------------------------------- the kit's side

        /// <summary>Once a second while the interface is on: each registered app put on the phone once the phone is built (its face is the frame's, UiShell.Face).</summary>
        internal static void Install()
        {
            for (int i = 0; i < Apps.Count; i++)
            {
                if (Apps[i].App == null)
                {
                    Put(Apps[i]);
                }
            }
        }

        /// <summary>The apps taken off the phone: the interface off, the city gone, the mod unloading.</summary>
        internal static void Remove(string why)
        {
            for (int i = 0; i < Apps.Count; i++)
            {
                AppEntry e = Apps[i];
                if (e.App != null)
                {
                    e.App.Remove();
                    e.App = null;
                }
            }
        }

        private static void Put(AppEntry e)
        {
            if (!PhoneApp.PhoneUp)
            {
                return;
            }
            AppEntry entry = e;
            var def = new PhoneAppDef
            {
                Id = e.Id,
                TitleKey = e.Name,
                Icon = like => Painted(entry, like),
                Build = (menu, holder) => Panel(entry, holder)
            };
            if (e.Screen != null)
            {
                def.Kit = e.Screen;
                def.KitScreen = e.ScreenId;
                def.KitIcon = UiIcon.Code;
            }
            e.App = PhoneApp.Install(def, out bool _);
            if (e.App != null && e.Screen != null)
            {
                UiShell.Face(e.App, def);   // the frame draws the kit screen over the panel while the interface and the app's breaker allow it
            }
        }

        // The app's picture: the mod's own, or a tile the kit paints in a hue of the app's name.
        private static Sprite Painted(AppEntry e, Sprite like)
        {
            if (e.Icon != null)
            {
                return e.Icon;
            }
            UiRole hue = UiTokens.Chart(UiForeignMark.Hue(e.Id));
            uint top = UiTokens.Colour(hue);
            Color32 up = new Color32((byte)(top >> 24), (byte)(top >> 16), (byte)(top >> 8), 255);
            Color32 down = new Color32((byte)(up.r * 6 / 10), (byte)(up.g * 6 / 10), (byte)(up.b * 6 / 10), 255);
            return AppIconPainter.Paint(like, null, up, down, 0f, "AppIcon-Other-" + e.Id);
        }

        // The app's panel among the game's: the mod's own, or an empty one the kit draws over; either carries the group the game fades.
        private static GameObject Panel(AppEntry e, Transform holder)
        {
            GameObject go;
            if (e.Panel != null)
            {
                go = e.Panel(holder);
                if (go == null)
                {
                    return null;
                }
            }
            else
            {
                go = new GameObject("RealLife Page", typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(holder, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            if (go.GetComponent<CanvasGroup>() == null)
            {
                go.AddComponent<CanvasGroup>();
            }
            return go;
        }
    }
}
