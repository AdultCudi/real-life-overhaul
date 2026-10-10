using System;
using System.Reflection;
using BAModAPI;
using Localizor.LanguageChangeEvent;
using UI;
using UI.InGameUI;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>What an app on the phone is made of.</summary>
    public sealed class PhoneAppDef
    {
        /// <summary>The name of its objects in the game's interface. The game opens an app by the name of its panel, so it must be no AppName.</summary>
        public string Id;
        public string TitleKey;
        /// <summary>Given one of the game's own app icons to model its picture on; may return null (the button then keeps the template's).</summary>
        public Func<Sprite, Sprite> Icon;
        /// <summary>Builds the app's panel asleep under the dormant holder it is given, from the game's own (GameUi.AppShell); null if it cannot.</summary>
        public Func<FullMenu, Transform, GameObject> Build;
        public Action Opened;
        public Action Closed;
    }

    /// <summary>
    /// One app on the game's phone: a button on the HUD phone's home screen, a button in the top bar of the
    /// full-screen menu, and a panel among the game's own app panels, all three copies of the game's.
    ///
    /// How the game does it (SmartphoneUI.Awake, FullMenu.Start and FullMenu.SelectApp): each of the two phones makes
    /// one button per app from an "AppButtonTemplate"; an app is a child of FullMenu.appsContainer whose name is the
    /// app's; opening an app switches every child of the container off and the one of that name on, writes the app's
    /// name into the top bar, moves the dot under the buttons, and opens the menu if it was closed. Open() below does
    /// the same for a panel the game does not know by name; when the game opens one of its own, its loop switches
    /// this one off like any other.
    /// </summary>
    public sealed class PhoneApp
    {
        public static IModLogger Logger;

        private readonly PhoneAppDef _def;
        private SmartphoneUI _phone;
        private FullMenu _menu;
        private SmartphoneAppButton _hudButton;
        private FullMenuAppButton _menuButton;
        private PhoneGridPager _pager;
        private Transform _dormant;
        private int _badge;

        public GameObject Root { get; private set; }
        public bool Installed => Root != null && _hudButton != null && _menuButton != null;
        public bool Showing => Root != null && Root.activeInHierarchy;
        public PhoneGridPager Pager => _pager;

        private PhoneApp(PhoneAppDef def)
        {
            _def = def;
        }

        /// <summary>
        /// Puts the app on the phone. Null if the game's phone is not there yet (ask again later) or is not built as
        /// expected (one line in Player.log says what was missing; nothing is left behind).
        /// </summary>
        public static PhoneApp Install(PhoneAppDef def, out bool tryAgain)
        {
            tryAgain = true;
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.smartphoneUI == null || uis.fullMenu == null || uis.fullMenu.appsContainer == null)
            {
                return null;
            }
            SmartphoneUI phone = uis.smartphoneUI;
            FullMenu menu = uis.fullMenu;
            // FullMenu makes its buttons in Start: until one is there the menu has not been set up.
            if (menu.GetComponentInChildren<FullMenuAppButton>(false) == null || phone.appButtons == null || phone.appButtons.Count == 0)
            {
                return null;
            }
            tryAgain = false;
            var app = new PhoneApp(def) { _phone = phone, _menu = menu };
            try
            {
                if (menu.appsContainer.Find(def.Id) != null || Enum.TryParse(def.Id, out AppName _))
                {
                    UiClone.Fallback("an app called '" + def.Id + "' is already on the phone, a second one is not added");
                    return null;
                }
                string templateUsed;
                Transform hudTemplate = Template(phone, typeof(SmartphoneUI), phone.transform, typeof(SmartphoneAppButton), out templateUsed);
                string menuTemplateUsed;
                Transform menuTemplate = Template(menu, typeof(FullMenu), menu.transform, typeof(FullMenuAppButton), out menuTemplateUsed);
                if (hudTemplate == null || menuTemplate == null || hudTemplate.parent == null || menuTemplate.parent == null)
                {
                    UiClone.Fallback("the phone's app button was not found (" + (hudTemplate == null ? "HUD phone" : "full menu") + "), so '" + def.Id + "' is not added");
                    return null;
                }

                app._dormant = UiClone.Dormant(menu.canvasGroup != null ? menu.canvasGroup.transform : menu.transform, "RealLife Dormant " + def.Id);
                GameObject root = def.Build != null ? def.Build(menu, app._dormant) : null;
                if (root == null)
                {
                    app.Remove();
                    return null;   // the builder has said what was missing
                }
                root.SetActive(false);
                root.name = def.Id;
                root.transform.SetParent(menu.appsContainer, false);
                if (root.GetComponent<CanvasGroup>() == null)
                {
                    root.AddComponent<CanvasGroup>();
                }
                root.AddComponent<PhoneAppRoot>().App = app;
                app.Root = root;

                Sprite like = null;
                SmartphoneAppButton persona;
                if (phone.appButtons.TryGetValue(AppName.Persona, out persona) && persona != null)
                {
                    Image picture = persona.transform.Find("Icon") != null ? persona.transform.Find("Icon").GetComponent<Image>() : null;
                    like = picture != null ? picture.sprite : null;
                }
                Sprite icon = def.Icon != null ? def.Icon(like) : null;

                Transform hud = UnityEngine.Object.Instantiate(hudTemplate, hudTemplate.parent, false);
                hud.name = def.Id;
                hud.gameObject.tag = "Untagged";
                app._hudButton = hud.GetComponent<SmartphoneAppButton>();
                Transform inMenu = UnityEngine.Object.Instantiate(menuTemplate, menuTemplate.parent, false);
                inMenu.name = def.Id;
                inMenu.gameObject.tag = "Untagged";
                app._menuButton = inMenu.GetComponent<FullMenuAppButton>();
                if (app._hudButton == null || app._menuButton == null)
                {
                    UnityEngine.Object.Destroy(hud.gameObject);
                    UnityEngine.Object.Destroy(inMenu.gameObject);
                    UiClone.Fallback("the phone's app button is no longer an app button, so '" + def.Id + "' is not added");
                    app.Remove();
                    return null;
                }
                Dress(hud, app._hudButton, def.TitleKey, icon, app.Open);
                Dress(inMenu, app._menuButton, def.TitleKey, icon, app.Open);
                app._hudButton.HideOutline();
                app._hudButton.UpdateBadgeCount(0);
                app._menuButton.HideSelectedIcon();
                hud.gameObject.SetActive(true);
                inMenu.gameObject.SetActive(true);

                // The dot the game shows under the app that is open is the one picture of a round dot at hand.
                Transform selected = menuTemplate.Find("SelectedIcon");
                Image dot = selected != null ? selected.GetComponent<Image>() : null;
                app._pager = PhoneGridPager.On(hudTemplate.parent, hudTemplate, dot != null ? dot.sprite : null);

                if (Meta.Dev.Verbose)
                {
                    int place = app._pager != null ? app._pager.PlaceOf(hud) : 0;
                    Logger?.Info("Game UI: app '" + def.Id + "' is on the phone. Button copied from " + templateUsed + " (HUD) and " + menuTemplateUsed
                                 + " (full menu); place " + place + " of " + (app._pager != null ? app._pager.Count : 0) + " apps, "
                                 + (app._pager == null ? "no pages" : app._pager.SteppedAside ? "pages left to another mod" : app._pager.Pages + " page(s)")
                                 + "; icon " + (icon == null ? "the template's" : icon.name) + ".");
                }
                return app;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("'" + def.Id + "' could not be put on the phone and is left off");
                app.Remove();
                return null;
            }
        }

        // The button each phone copies for its apps: its private "appButtonTemplate"; failing that the object of that
        // name; failing that any button of the right kind (a copy of a live button works the same: the click the game
        // adds in code is not copied with it).
        private static Transform Template(object owner, Type type, Transform under, Type buttonType, out string used)
        {
            used = "appButtonTemplate";
            try
            {
                FieldInfo field = type.GetField("appButtonTemplate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var template = field != null ? field.GetValue(owner) as Transform : null;
                if (template != null && template.GetComponent(buttonType) != null)
                {
                    return template;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Component[] all = under.GetComponentsInChildren(buttonType, true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == "AppButtonTemplate")
                {
                    used = "the object named AppButtonTemplate";
                    return all[i].transform;
                }
            }
            used = all.Length > 0 ? "the live button '" + all[0].name + "'" : "nothing";
            if (all.Length > 0)
            {
                UiClone.Fallback("the phone's button template was not found, a live app button is copied instead");
            }
            return all.Length > 0 ? all[0].transform : null;
        }

        private static void Dress(Transform button, AppButton script, string titleKey, Sprite icon, Action click)
        {
            var title = button.GetComponentInChildren<TextLocalizationComponent>(true);
            if (title != null)
            {
                UiClone.Key(title, titleKey);
            }
            if (icon != null)
            {
                script.SetIcon(icon);
            }
            if (UiClone.Rewire(button, click) == null)
            {
                UiClone.Fallback("an app button without a Button: '" + button.name + "' cannot be clicked");
            }
        }

        /// <summary>The number on the HUD button, as Contacts shows its unread messages; 0 takes it away.</summary>
        public void SetBadge(int count)
        {
            if (_hudButton == null || count == _badge)
            {
                return;
            }
            _badge = count;
            try
            {
                _hudButton.UpdateBadgeCount(count);
                if (_pager != null)
                {
                    _pager.SetAttention(_hudButton.transform, count > 0);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Opens the app as the game opens its own (SmartphoneUI.OpenApp, FullMenu.ShowApp, FullMenu.SelectApp).</summary>
        public void Open()
        {
            if (!Installed || _menu == null)
            {
                return;
            }
            try
            {
                if (_phone != null)
                {
                    _phone.StopOutliningCurrentApp();
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (uis != null && uis.buildingResume != null)
                {
                    uis.buildingResume.Close();
                }
                // Leaving BizMan's settings with changes not saved asks first, whichever app is opened next.
                if (_menu.bizMan != null && _menu.bizMan.business != null && _menu.bizMan.business.bizManSettings != null
                    && _menu.bizMan.business.bizManSettings.HasUnsavedChanges)
                {
                    HudConfirm.Show(null, "change_character_clothes_unsaved_changes_warning", Select, null, null, null, true, true);
                    return;
                }
                Select();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Select()
        {
            if (!Installed)
            {
                return;
            }
            bool wasOpen = FullMenu.IsOpen;
            Transform container = _menu.appsContainer;
            for (int i = 0; i < container.childCount; i++)
            {
                GameObject child = container.GetChild(i).gameObject;
                bool mine = child == Root;
                if (child.activeSelf != mine)
                {
                    child.SetActive(mine);
                }
            }
            var root = Root.GetComponent<PhoneAppRoot>();
            if (root != null)
            {
                root.FadeIn(wasOpen ? _menu.transitionDuration : 0f);
            }
            if (!wasOpen)
            {
                _menu.Toggle(true);
            }
        }

        // The panel has come on: by Open(), or with the menu when it was the app last looked at.
        internal void Shown()
        {
            try
            {
                if (_menu != null && _menu.appNameLabel != null)
                {
                    _menu.appNameLabel.Key = _def.TitleKey;
                }
                if (_menuButton != null)
                {
                    FullMenuAppButton[] all = _menuButton.transform.parent.GetComponentsInChildren<FullMenuAppButton>(false);
                    for (int i = 0; i < all.Length; i++)
                    {
                        all[i].HideSelectedIcon();
                    }
                    _menuButton.ShowSelectedIcon();
                }
                _def.Opened?.Invoke();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The panel has gone off: the menu closed, or the game opened one of its own apps.
        internal void Hidden()
        {
            try
            {
                if (_menuButton != null)
                {
                    _menuButton.HideSelectedIcon();
                }
                _def.Closed?.Invoke();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Takes everything this app added off the phone. Safe to call twice, and after the game's interface has gone.</summary>
        public void Remove()
        {
            if (_hudButton != null)
            {
                UnityEngine.Object.Destroy(_hudButton.gameObject);
            }
            if (_menuButton != null)
            {
                UnityEngine.Object.Destroy(_menuButton.gameObject);
            }
            if (Root != null)
            {
                UnityEngine.Object.Destroy(Root);
            }
            if (_dormant != null)
            {
                UnityEngine.Object.Destroy(_dormant.gameObject);
            }
            if (_pager != null)
            {
                UnityEngine.Object.Destroy(_pager);   // shows again whatever it had on another page
            }
            _hudButton = null;
            _menuButton = null;
            _pager = null;
            _dormant = null;
            Root = null;
        }
    }

    /// <summary>On an app's panel: tells the app when the panel comes on and goes off. It has no Update.</summary>
    public sealed class PhoneAppRoot : MonoBehaviour
    {
        internal PhoneApp App;

        private void OnEnable()
        {
            App?.Shown();
        }

        private void OnDisable()
        {
            var group = GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
            }
            App?.Hidden();
        }

        /// <summary>Fades the panel in over the game's own tenth of a second, as FullMenu.SelectApp does when the player changes app.</summary>
        internal void FadeIn(float seconds)
        {
            var group = GetComponent<CanvasGroup>();
            if (group == null)
            {
                return;
            }
            if (seconds <= 0f)
            {
                group.alpha = 1f;
                return;
            }
            PanelFade fade = GetComponent<PanelFade>() ?? gameObject.AddComponent<PanelFade>();
            fade.Begin(group, seconds);
        }
    }

    /// <summary>A fade of one canvas group on the unscaled clock (the game is paused while the menu is open). Switched on only while it lasts.</summary>
    public sealed class PanelFade : MonoBehaviour
    {
        private CanvasGroup _group;
        private float _from, _for;

        internal void Begin(CanvasGroup group, float seconds)
        {
            _group = group;
            _from = Time.unscaledTime;
            _for = seconds;
            group.alpha = 0f;
            enabled = true;
        }

        private void Update()
        {
            float t = _group != null && _for > 0f ? (Time.unscaledTime - _from) / _for : 1f;
            if (_group != null)
            {
                _group.alpha = Mathf.Clamp01(t);
            }
            if (t >= 1f)
            {
                enabled = false;
            }
        }

        private void OnDisable()
        {
            if (_group != null)
            {
                _group.alpha = 1f;
            }
            enabled = false;
        }
    }
}
