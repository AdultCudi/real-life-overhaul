using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The overhaul's own pages on the kit, given their entry in play: the city's people, Flare in the city and the
    /// player's tech firm, each a panel among the full menu's apps with no phone button (PhoneAppDef.NoButtons), opened
    /// from All apps, its face the kit's screen through PhoneAppDef.Kit, which the frame draws over the panel as it draws
    /// over the game's apps (ModAppScreen and UiGameHost). A page is offered only while what it shows runs and its own
    /// switch is on; with the RLO interface off they are not there at all, as they never were in the game's chrome.
    /// Their reshaping belongs to the apps that will hold them. Cost: once a second while the interface is on, a few
    /// comparisons; a page and a host made once a city when first offered.
    /// </summary>
    public static class ShellPages
    {
        private sealed class Page
        {
            public string Id;            // its panel's name among the apps (UiShellApps.ShellPeople ...)
            public string ScreenId;      // its kit screen's id in UiScreens, which its switch is keyed by
            public string TitleKey;
            public UiIcon Icon;
            public Func<bool> Ready;     // what it shows runs now
            public Func<IUiScreen> Make;
            public PhoneApp App;
            public UiGameHost Host;
        }

        private static readonly Page[] Pages =
        {
            new Page { Id = UiShellApps.ShellPeople, ScreenId = "population", TitleKey = "reallife_ui_page_population", Icon = UiIcon.People,
                Ready = () => Population.CityPopulation.Running, Make = () => new PopulationScreen { Close = CloseMenu } },
            new Page { Id = UiShellApps.ShellFirm, ScreenId = "studio", TitleKey = "reallife_ui_page_studio", Icon = UiIcon.GameController,
                Ready = () => Tech.TechDirector.Instance != null && Tech.TechDirector.Instance.FirmPlaces.Count > 0,
                Make = () => new StudioPageScreen(Tech.TechDirector.Instance.FirmPlaces[0].File.Place) { Close = CloseMenu } },
        };

        // A page's Back at its top: the full menu closes, as the game's own close does (its unsaved BizMan question kept).
        private static void CloseMenu()
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis != null && uis.fullMenu != null && FullMenu.IsOpen)
            {
                uis.fullMenu.CloseFullMenu();
            }
        }

        /// <summary>Once a second while the interface is on: each page offered while it is ready and its switch on, taken off when not.</summary>
        public static void Install()
        {
            for (int i = 0; i < Pages.Length; i++)
            {
                Page p = Pages[i];
                bool wanted = UiScreenSwitches.On(p.ScreenId) && p.Ready();
                if (!wanted)
                {
                    Take(p, "not offered now");
                    continue;
                }
                if (p.App == null)
                {
                    Put(p);
                }
                p.Host?.Install();
            }
        }

        /// <summary>A page's panel was just built on its first opening: its kit binds over it at once, when the page is offered now.</summary>
        internal static void Built(string screenId)
        {
            for (int i = 0; i < Pages.Length; i++)
            {
                Page p = Pages[i];
                if (p.ScreenId == screenId && p.App != null && p.Host != null && p.Ready())
                {
                    p.Host.Install();
                }
            }
        }

        /// <summary>Every page off: the interface switched off, the city gone or the mod unloading.</summary>
        public static void Remove(string why)
        {
            for (int i = 0; i < Pages.Length; i++)
            {
                Take(Pages[i], why);
            }
        }

        /// <summary>A new city: the pages are looked at again.</summary>
        public static void Rearm()
        {
            for (int i = 0; i < Pages.Length; i++)
            {
                Pages[i].Host?.Rearm();
            }
        }

        /// <summary>The pages offered now, as All apps lists them.</summary>
        internal static void Add(List<UiShellApp> into, ref int hash)
        {
            for (int i = 0; i < Pages.Length; i++)
            {
                if (Pages[i].App != null && Pages[i].App.Installed)
                {
                    ShellApps.Append(into, Pages[i].Id, UiText.T(Pages[i].TitleKey), UiShellKind.Page, UiShellApps.FolderOf(Pages[i].Id, UiShellKind.Page, -1), ref hash);
                }
            }
        }

        /// <summary>Opens a page by its id; false for an id that is no page or one not offered now.</summary>
        internal static bool Open(string id)
        {
            for (int i = 0; i < Pages.Length; i++)
            {
                if (Pages[i].Id == id && Pages[i].App != null)
                {
                    Pages[i].App.Open();
                    return true;
                }
            }
            return false;
        }

        /// <summary>The states of the pages' faces, for the interface's line at load.</summary>
        public static string States()
        {
            var sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < Pages.Length; i++)
            {
                sb.Append(i > 0 ? ", " : string.Empty).Append(Pages[i].ScreenId).Append(" (").Append(Pages[i].App == null ? "not offered" : Pages[i].Host.State).Append(')');
            }
            return sb.ToString();
        }

        private static void Put(Page p)
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next second, nothing made for it
            }
            Page page = p;
            p.App = PhoneApp.Install(new PhoneAppDef
            {
                Id = p.Id,
                TitleKey = p.TitleKey,
                NoButtons = true,
                Build = (menu, holder) => Panel(holder),
                Kit = p.Make,
                KitScreen = p.ScreenId   // the panel is built on the first opening: PhoneApp.Ready has the kit bind over it (UiShell.Built)
            }, out bool _);
            if (p.App != null && p.Host == null)
            {
                // One host a page for the session: it binds over whichever panel the page has in this city.
                p.Host = new UiGameHost(new ModAppScreen(p.Id, p.ScreenId, p.TitleKey, p.Icon, () => page.App != null ? page.App.Root : null, p.Make));
            }
        }

        private static void Take(Page p, string why)
        {
            if (p.Host != null)
            {
                p.Host.Remove(why);
            }
            if (p.App != null)
            {
                p.App.Remove();
                p.App = null;
            }
        }

        // The page's own panel under the kit: empty, as the kit draws all of it; it carries the group SelectApp fades.
        private static GameObject Panel(Transform holder)
        {
            var go = new GameObject("RealLife Page", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(holder, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return go;
        }
    }
}
