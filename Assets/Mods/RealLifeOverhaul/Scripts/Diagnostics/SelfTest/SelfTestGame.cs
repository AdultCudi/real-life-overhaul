using System;
using System.Collections;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;
using UI;
using UI.Load;
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
                if (string.IsNullOrEmpty(id) || hosts[i].Screen.Id == id)
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

        public static string ShowingWords()
        {
            List<UiHost> hosts = KitHosts();
            if (hosts.Count == 0)
            {
                return "no kit screen shows";
            }
            var ids = new List<string>(hosts.Count);
            for (int i = 0; i < hosts.Count; i++)
            {
                ids.Add(hosts[i].Screen.Id);
            }
            return "kit screens showing: " + string.Join(", ", ids);
        }

        /// <summary>Closes what stands: the game's question, each kit screen by its Back, then the phone's menu; the frames it needs are waited here.</summary>
        public static IEnumerator Close()
        {
            for (int round = 0; round < 6; round++)
            {
                bool did = CloseQuestion();
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
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.4f);
            }
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
