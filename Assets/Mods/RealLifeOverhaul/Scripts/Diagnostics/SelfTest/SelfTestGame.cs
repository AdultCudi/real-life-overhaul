using System;
using System.Collections;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;
using UI;
using UI.Load;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>
    /// What the steps ask of the game. A game member missing from a player's build fails the step that names it (the runner
    /// writes that as the step's fault and goes on). Developer diagnostic: read only by a running plan.
    /// </summary>
    internal static class SelfTestGame
    {
        /// <summary>The main menu stands: its start view is up and no scene is loading.</summary>
        public static bool MenuUp()
        {
            MainMenuController menu = InstanceBehavior<MainMenuController>.Instance;
            return menu != null && menu.startView != null && menu.startView.activeInHierarchy && !LoadScene.isLoading;
        }

        /// <summary>
        /// A city stands: a save is in, the game's own managers are up, nothing loads or unloads, and the mod's load chain has
        /// finished (its save state read, its slices of the first seconds played).
        /// </summary>
        public static bool CityUp()
        {
            return SaveGameManager.Current != null && InstanceBehavior<GameManager>.Instance != null && InstanceBehavior<UIs>.Instance != null
                   && !LoadScene.isLoading && !GameManager.isCitySceneBeingUnloaded && ModState.Loaded && DayWarm.Ready && !MenuUp();
        }

        public static string CityWords()
        {
            GameInstance save = SaveGameManager.Current;
            return save == null ? "no game" : "day " + save.Day + ", " + save.Hour.ToString("00") + ":" + ((int)save.Minute).ToString("00") + ", money " + (long)save.Money;
        }

        /// <summary>The player's money now, or -1 with no game.</summary>
        public static float MoneyNow() => SaveGameManager.Current == null ? -1f : SaveGameManager.Current.Money;

        /// <summary>The game's clock as total minutes (day, hours, minutes), or -1 with no game.</summary>
        public static float Minutes()
        {
            GameInstance save = SaveGameManager.Current;
            return save == null ? -1f : (save.Day * 24 + save.Hour) * 60f + save.Minute;
        }

        public static UIs Uis() => InstanceBehavior<UIs>.Instance;

        /// <summary>The phone's full menu is open.</summary>
        public static bool PhoneOpen() => FullMenu.IsOpen;

        public static void ClosePhone()
        {
            UIs uis = Uis();
            if (uis != null && uis.fullMenu != null)
            {
                uis.fullMenu.CloseFullMenu();
            }
        }

        public static void OpenPhone()
        {
            UIs uis = Uis();
            if (uis != null && uis.fullMenu != null)
            {
                uis.fullMenu.Toggle(true);
            }
        }

        /// <summary>The game's own question box is up: it is answered No (its own close) so a plan never waits under one.</summary>
        public static bool CloseQuestion()
        {
            if (!HudConfirm.isOpen)
            {
                return false;
            }
            HudConfirm.onClose?.Invoke();
            return true;
        }

        /// <summary>Opens a phone app by the game's name; false when the name is none or the phone is not up.</summary>
        public static bool OpenGameApp(string name)
        {
            UIs uis = Uis();
            if (uis == null || uis.smartphoneUI == null || !Enum.TryParse(name, true, out AppName app))
            {
                return false;
            }
            uis.smartphoneUI.OpenApp(app);
            return true;
        }

        public static IEnumerable<string> GameAppNames()
        {
            foreach (AppName app in Enum.GetValues(typeof(AppName)))
            {
                yield return app.ToString();
            }
        }

        /// <summary>The kit screens standing now, topmost last: every host of the kit that is up and has laid a screen. A search of the scene, done on a step, never per frame.</summary>
        public static List<UiHost> KitHosts()
        {
            var list = new List<UiHost>(4);
            foreach (UiHost host in SceneFinds.All<UiHost>("the self-test's look for the kit screens showing"))
            {
                if (host != null && host.Up && host.Screen != null && host.Scene != null && host.Scene.ItemCount > 0)
                {
                    list.Add(host);
                }
            }
            list.Sort((a, b) => Order(a).CompareTo(Order(b)));
            return list;
        }

        private static int Order(UiHost host)
        {
            Canvas canvas = host.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.sortingOrder : 0;
        }

        /// <summary>The topmost kit host showing, of that screen id when one is named; null when none.</summary>
        public static UiHost Showing(string id)
        {
            List<UiHost> hosts = KitHosts();
            for (int i = hosts.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrEmpty(id) || hosts[i].Screen.Id == id || hosts[i].Scene.Name == id)   // a piece's layout is named for the id when its host carries another's
                {
                    return hosts[i];
                }
            }
            return null;
        }

        /// <summary>
        /// The kit screen a player would be looking at: the topmost that is not one of the HUD's own pieces (the status bar, the
        /// goals and the notices stand over every city while the interface is on); of that screen id when one is named.
        /// </summary>
        public static UiHost Standing(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                return Showing(id);
            }
            List<UiHost> hosts = KitHosts();
            for (int i = hosts.Count - 1; i >= 0; i--)
            {
                UiScreenDef def = UiScreens.Find(hosts[i].Screen.Id);
                if (def == null || def.Template != UiTemplate.Hud)
                {
                    return hosts[i];
                }
            }
            return null;
        }

        /// <summary>The host a press, a typed text or the wheel goes to: the one standing, else the topmost of any kind (the HUD's own piece).</summary>
        public static UiHost Target(string id)
        {
            return Standing(id) ?? Showing(id);
        }

        /// <summary>The HUD of the kit stands: its host is made and has laid its scene (the HUD is a host of its own, not a UiHost).</summary>
        public static bool HudUp()
        {
            HudHost hud = HudHost.Instance;
            return hud != null && hud.isActiveAndEnabled && hud.Scene != null && hud.Scene.ItemCount > 0;
        }

        /// <summary>The ids of every kit screen showing now, the HUD included ("hud"), topmost last.</summary>
        public static List<string> ShowingIds()
        {
            var ids = new List<string>(6);
            if (HudUp())
            {
                ids.Add("hud");
            }
            List<UiHost> hosts = KitHosts();
            for (int i = 0; i < hosts.Count; i++)
            {
                if (!ids.Contains(hosts[i].Screen.Id))
                {
                    ids.Add(hosts[i].Screen.Id);
                }
            }
            return ids;
        }

        /// <summary>True when the kit shows that screen: its host, or the HUD (its pieces are layouts of the one HUD scene, so they count while it stands).</summary>
        public static bool IsShowing(string id)
        {
            return Showing(id) != null || (HudUp() && (id == "hud" || (id != null && id.StartsWith("hud_", StringComparison.Ordinal))));
        }

        public static string ShowingWords()
        {
            List<string> ids = ShowingIds();
            return ids.Count == 0 ? "no kit screen shows" : "kit screens showing: " + string.Join(", ", ids);
        }

        /// <summary>The ids showing besides the HUD and the one named (a screen shown alone has none).</summary>
        public static string Others(string id)
        {
            var others = new List<string>(4);
            foreach (string shown in ShowingIds())
            {
                if (shown != "hud" && shown != id && shown != "phone")
                {
                    others.Add(shown);
                }
            }
            return string.Join(", ", others);
        }

        /// <summary>The city map is open (the camera is over the map, the game's phone and bar are put away).</summary>
        public static bool MapOpen() => CityMap.IsOpen;

        private static void LeaveMap()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city != null && city.cityMap != null)
            {
                city.cityMap.Toggle();
            }
        }

        /// <summary>The game's toasts on screen cleared, so a picture holds the screen and not what a test said; the count cleared.</summary>
        public static int ClearNotices()
        {
            NotificationsUI toasts = SceneFinds.Any(typeof(NotificationsUI), "the self-test's clearing of the notices") as NotificationsUI;
            int cleared = 0;
            if (toasts != null && toasts.container != null)
            {
                foreach (Transform child in toasts.container)
                {
                    if (child != null && child.gameObject.activeSelf)
                    {
                        UnityEngine.Object.Destroy(child.gameObject);
                        cleared++;
                    }
                }
            }
            return cleared;
        }

        /// <summary>What was left standing by the last <see cref="Close"/> in spite of everything, or empty.</summary>
        public static string Residue = string.Empty;

        /// <summary>
        /// Gets the screen to the state a player's would be with nothing open: the game's question answered No, the map left, each kit screen
        /// closed by its Back, the phone's menu closed, a screen that will not close taken away (its test button pressed again if the
        /// test toggles, else the host hidden), and the notices cleared. <see cref="Residue"/> says what would not go.
        /// </summary>
        public static IEnumerator Close()
        {
            Residue = string.Empty;
            for (int round = 0; round < 8; round++)
            {
                bool did = CloseQuestion();
                if (MapOpen())
                {
                    LeaveMap();
                    did = true;
                }
                UiHost top = Standing(null);
                if (top != null)
                {
                    did |= top.SelfTestBack();
                }
                if (!did && PhoneOpen())
                {
                    ClosePhone();
                    did = true;
                }
                if (!did)
                {
                    break;
                }
                yield return new WaitForSecondsRealtime(0.4f);
            }
            UiHost left = Standing(null);
            if (left != null)
            {
                string id = left.Screen.Id;
                if (SelfTestScreenTable.Toggles(id))
                {
                    SelfTestUi.Undo(id);
                    yield return new WaitForSecondsRealtime(0.4f);
                    left = Standing(null);
                }
                if (left != null)
                {
                    left.Hide();   // it would not close by its Back: the kit takes it away
                    yield return new WaitForSecondsRealtime(0.4f);
                    left = Standing(null);
                    Residue = "'" + id + "' would not close by its Back and was hidden by force" + (left != null ? "; '" + left.Screen.Id + "' still stands" : string.Empty);
                }
            }
            if (PhoneOpen() || MapOpen())
            {
                Residue += (Residue.Length > 0 ? "; " : string.Empty) + (PhoneOpen() ? "the phone's menu is still open" : "the map is still open");
            }
            int cleared = ClearNotices();
            yield return new WaitForSecondsRealtime(cleared > 0 ? 0.5f : 0.1f);
        }

        public static bool SceneLoaded(string name)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == name)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
