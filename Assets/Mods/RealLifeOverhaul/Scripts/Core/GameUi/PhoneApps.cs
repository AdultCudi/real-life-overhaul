using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BAModAPI;
using Localizor;
using Localizor.LanguageChangeEvent;
using UI;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// One app on the game's phone: a button on each phone and a panel among the game's app panels, all copies of the
    /// game's; Open switches the panels as FullMenu.SelectApp does. The panel (a copy of MyEmployees) is built when the
    /// app is first opened (<see cref="Ready"/>). Opening and the build are timed (HotSpots); costs nothing a frame.
    /// </summary>
    public sealed class PhoneApp
    {
        public static IModLogger Logger;

        private static readonly Dictionary<string, PhoneApp> ById = new Dictionary<string, PhoneApp>(32, StringComparer.Ordinal);

        private readonly PhoneAppDef _def;
        private SmartphoneUI _phone;
        private FullMenu _menu;
        private SmartphoneAppButton _hudButton;
        private FullMenuAppButton _menuButton;
        private PhoneGridPager _pager;
        private PhoneGridPager _strip;
        private Transform _dormant;
        private int _badge;
        private bool _pending;     // on the phone, its panel not built yet
        private readonly int _hotOpened, _hotBuilt;

        /// <summary>The app's panel among the game's; null until the app is first opened (<see cref="Ready"/>).</summary>
        public GameObject Root { get; private set; }
        /// <summary>On the phone: its buttons are there, and its panel is, or is still to be built.</summary>
        public bool Installed => (_pending ? _menu != null : Root != null) && (_def.NoButtons || (_hudButton != null && _menuButton != null));
        public bool Showing => Root != null && Root.activeInHierarchy;
        public PhoneGridPager Pager => _pager;

        private PhoneApp(PhoneAppDef def)
        {
            _def = def;
            _hotOpened = HotSpots.Slot("Apps: " + def.Id + " opened");   // once an install: a name keeps its number for the session
            _hotBuilt = HotSpots.Slot("Apps: " + def.Id + " built");
        }

        /// <summary>The game's phone can take an app: its parts are there and the full menu made its buttons. At most once a second until true.</summary>
        public static bool PhoneUp
        {
            get
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (uis == null || uis.smartphoneUI == null || uis.fullMenu == null || uis.fullMenu.appsContainer == null)
                {
                    return false;
                }
                // FullMenu makes its buttons in Start.
                SmartphoneUI phone = uis.smartphoneUI;
                return uis.fullMenu.GetComponentInChildren<FullMenuAppButton>(false) != null && phone.appButtons != null && phone.appButtons.Count > 0;
            }
        }

        /// <summary>Puts the app on the phone; null if the phone is not there yet (<paramref name="tryAgain"/>) or not as expected.</summary>
        public static PhoneApp Install(PhoneAppDef def, out bool tryAgain)
        {
            tryAgain = true;
            if (!PhoneUp)
            {
                return null;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            SmartphoneUI phone = uis.smartphoneUI;
            FullMenu menu = uis.fullMenu;
            tryAgain = false;
            var app = new PhoneApp(def) { _phone = phone, _menu = menu };
            try
            {
                if (menu.appsContainer.Find(def.Id) != null || Enum.TryParse(def.Id, out AppName _) || Named(phone.transform, menu.transform, def.Id))
                {
                    UiClone.Fallback("an app called '" + def.Id + "' is already on the phone, a second one is not added");
                    return null;
                }
                string templateUsed;
                Transform hudTemplate = Template(phone, GameContract.HudAppTemplate, phone.transform, typeof(SmartphoneAppButton), out templateUsed);
                string menuTemplateUsed;
                Transform menuTemplate = Template(menu, GameContract.MenuAppTemplate, menu.transform, typeof(FullMenuAppButton), out menuTemplateUsed);
                if (hudTemplate == null || menuTemplate == null || hudTemplate.parent == null || menuTemplate.parent == null)
                {
                    UiClone.Fallback("the phone's app button was not found (" + (hudTemplate == null ? "HUD phone" : "full menu") + "), so '" + def.Id + "' is not added");
                    return null;
                }

                if (def.Build == null)
                {
                    UiClone.Fallback("'" + def.Id + "' has nothing to build its panel with, so it is not added");
                    return null;
                }
                app._dormant = UiClone.Dormant(menu.canvasGroup != null ? menu.canvasGroup.transform : menu.transform, "RealLife Dormant " + def.Id);
                app._pending = true;   // built when the app is first opened (Ready)
                if (def.NoButtons)
                {
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Game UI: app '" + def.Id + "' is among the phone's apps without a button: the mod opens it itself; its panel is built when it is first opened.");
                    }
                    ById[def.Id] = app;
                    return app;
                }

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
                Dress(hud, app._hudButton, def.TitleKey, def.TitleArguments, icon, app.Open);
                Dress(inMenu, app._menuButton, def.TitleKey, def.TitleArguments, icon, app.Open);
                app._hudButton.HideOutline();
                app._hudButton.UpdateBadgeCount(0);
                app._menuButton.HideSelectedIcon();
                hud.gameObject.SetActive(true);
                inMenu.gameObject.SetActive(true);

                // The dot under the open app: the one round dot at hand.
                Transform selected = menuTemplate.Find("SelectedIcon");
                Image dot = selected != null ? selected.GetComponent<Image>() : null;
                app._pager = PhoneGridPager.On(hudTemplate.parent, hudTemplate, dot != null ? dot.sprite : null);
                // The full menu's row ends at the money, the date and the time ("RightData").
                Transform bar = menuTemplate.parent.parent;
                app._strip = PhoneGridPager.OnStrip(menuTemplate.parent, menuTemplate, dot != null ? dot.sprite : null,
                                                    bar != null ? bar.Find("RightData") as RectTransform : null);

                if (Meta.Dev.Verbose)
                {
                    int place = app._pager != null ? app._pager.PlaceOf(hud) : 0;
                    Logger?.Info("Game UI: app '" + def.Id + "' is on the phone. Button copied from " + templateUsed + " (HUD) and " + menuTemplateUsed
                                 + " (full menu); place " + place + " of " + (app._pager != null ? app._pager.Count : 0) + " apps, "
                                 + (app._pager == null ? "no pages" : app._pager.SteppedAside ? "pages left to another mod" : app._pager.Pages + " page(s)")
                                 + "; full menu row " + (app._strip == null ? "not paged" : app._strip.SteppedAside ? "paged by another mod" : app._strip.Pages + " page(s)")
                                 + "; icon " + (icon == null ? "the template's" : icon.name) + "; its panel is built when it is first opened.");
                }
                ById[def.Id] = app;
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

        // A button of that name on either phone: an app of that name is on it, its panel perhaps not built yet.
        private static bool Named(Transform phone, Transform menu, string id)
        {
            SmartphoneAppButton[] hud = phone.GetComponentsInChildren<SmartphoneAppButton>(true);
            for (int i = 0; i < hud.Length; i++)
            {
                if (hud[i].name == id)
                {
                    return true;
                }
            }
            FullMenuAppButton[] inMenu = menu.GetComponentsInChildren<FullMenuAppButton>(true);
            for (int i = 0; i < inMenu.Length; i++)
            {
                if (inMenu[i].name == id)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The app's panel is there, built on the first call; false if it cannot be (the app is then taken off the phone).</summary>
        public bool Ready()
        {
            if (!_pending)
            {
                return Root != null;
            }
            _pending = false;
            if (_menu == null || _menu.appsContainer == null || _dormant == null)
            {
                return false;
            }
            using var hotSpot = HotSpots.Time(_hotBuilt);
            GameObject root = null;
            try
            {
                root = _def.Build(_menu, _dormant);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                root = null;
            }
            if (root == null)
            {
                UiClone.Fallback("'" + _def.Id + "' could not be built from the game's own app when it was first opened, so it is taken off the phone");
                Remove();
                return false;
            }
            root.SetActive(false);
            root.name = _def.Id;
            root.transform.SetParent(_menu.appsContainer, false);
            if (root.GetComponent<CanvasGroup>() == null)
            {
                root.AddComponent<CanvasGroup>();
            }
            root.AddComponent<PhoneAppRoot>().App = this;
            Root = root;
            return true;
        }

        // Each phone's private "appButtonTemplate"; failing that the object of that name; failing that any button of the kind
        // (a live button copies the same: the click the game adds in code is not copied).
        private static Transform Template(object owner, GameMember templateField, Transform under, Type buttonType, out string used)
        {
            used = "appButtonTemplate";
            try
            {
                FieldInfo field = templateField.Field;
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

        private static void Dress(Transform button, AppButton script, string titleKey, object titleArguments, Sprite icon, Action click)
        {
            var title = button.GetComponentInChildren<TextLocalizationComponent>(true);
            if (title != null)
            {
                UiClone.Key(title, titleKey);
                if (titleArguments != null)
                {
                    title.Arguments = titleArguments;   // the component writes its text again with the names filled in
                }
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

        /// <summary>Opens the mod's app of that id as its button does; false, with nothing done, when it is not on the phone.</summary>
        public static bool OpenById(string id)
        {
            if (string.IsNullOrEmpty(id) || !ById.TryGetValue(id, out PhoneApp app) || app == null || !app.Installed)
            {
                return false;
            }
            app.Open();
            return true;
        }

        /// <summary>The app's name as its button shows it, in the game's language; null when it is not on the phone.</summary>
        public static string TitleOf(string id)
        {
            if (string.IsNullOrEmpty(id) || !ById.TryGetValue(id, out PhoneApp app) || app == null || !app.Installed || app._def == null)
            {
                return null;
            }
            Transform named = app._hudButton != null ? app._hudButton.transform.Find("Title") : null;   // the button's own title, as the game's template names it
            TMPro.TMP_Text title = named != null ? named.GetComponent<TMPro.TMP_Text>() : null;
            if (title != null && !string.IsNullOrEmpty(title.text))
            {
                return title.text;
            }
            return string.IsNullOrEmpty(app._def.TitleKey) ? null : app._def.TitleKey.GetLocalization();
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

        /// <summary>Opens the app as the game opens its own, each game step in a method of its own; a click that opens nothing is logged once per app.</summary>
        public void Open()
        {
            using var hotSpot = HotSpots.Time(_hotOpened);
            string gone = Gone();
            if (gone != null)
            {
                Unopened(gone);
                return;
            }
            if (!Ready())
            {
                Unopened("its panel could not be built from the game's own app");
                return;
            }
            try
            {
                BeforeOpening();
                if (HeldByBizMan())
                {
                    return;
                }
                Select();
            }
            catch (Exception e)
            {
                Failed(e);
            }
        }

        // Said once for the app, with the member a game lacks or the error.
        private void Failed(Exception e)
        {
            if (Guard.Missing(e))
            {
                Unopened("this game lacks " + GameBuild.MemberOf(e.Message) + " (the game is Build " + GameBuild.Running + ", the mod is made for Build " + GameBuild.MadeFor + ")");
                return;
            }
            Unopened("an error, " + e.GetType().Name + ": " + e.Message);
            Logger?.Error(e);
        }

        // Why the app cannot be opened at all, or null.
        private string Gone()
        {
            if (ReferenceEquals(_menu, null))
            {
                return "it was never put in the full menu";
            }
            if (_menu == null)
            {
                return "the full menu it was put in is gone";
            }
            if (!_pending && Root == null)
            {
                return "its panel is gone";
            }
            if (!_def.NoButtons && _hudButton == null)
            {
                return "its button on the HUD phone is gone";
            }
            if (!_def.NoButtons && _menuButton == null)
            {
                return "its button in the full menu is gone";
            }
            return null;
        }

        private static readonly HashSet<string> UnopenedSaid = new HashSet<string>(StringComparer.Ordinal);

        private void Unopened(string why)
        {
            if (UnopenedSaid.Add(_def.Id))
            {
                Logger?.Warn("Game UI: the app '" + _def.Id + "' did not open when it was clicked: " + why + ". (Said once for each app in a session.)");
            }
        }

        // The game's first steps: the HUD phone's outline off and the building card closed, each skipped by a game without it.
        private void BeforeOpening()
        {
            try
            {
                OutlineOff();
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the HUD phone's outline when an app of the mod's is opened");
            }
            try
            {
                CardClosed();
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "closing the building card when an app of the mod's is opened");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void OutlineOff()
        {
            if (_phone != null)
            {
                _phone.StopOutliningCurrentApp();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CardClosed()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.buildingResume != null)
            {
                uis.buildingResume.Close();
            }
        }

        // Leaving BizMan's settings with changes not saved asks first, and is refused while the business has no name, as
        // FullMenu.ShowApp does. True when that has taken the click: the question is up, or the game's word on the name.
        private bool HeldByBizMan()
        {
            bool unsaved, nameless;
            try
            {
                BizManChanges(out unsaved, out nameless);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "BizMan's settings not saved when an app of the mod's is opened");
                return false;   // a game without them has none to keep
            }
            if (!unsaved)
            {
                return false;
            }
            if (nameless)
            {
                try
                {
                    NameFirst();
                }
                catch (Exception e) when (Guard.Missing(e))
                {
                    Guard.DoesWithout(e, "the game's word that a business needs a name before BizMan's settings are left");
                }
                return true;
            }
            if (!GameConfirm.TryShow(null, "change_character_clothes_unsaved_changes_warning", SelectAnswered, null, null, null, true, true))
            {
                Unopened("BizMan's settings have changes not saved, and this game's question whether to leave them could not be shown");
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void BizManChanges(out bool unsaved, out bool nameless)
        {
            BizManBusiness business = _menu.bizMan != null ? _menu.bizMan.business : null;
            unsaved = business != null && business.bizManSettings != null && business.bizManSettings.HasUnsavedChanges;
            nameless = unsaved && business.buildingRegistration != null && string.IsNullOrEmpty(business.buildingRegistration.BusinessName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void NameFirst()
        {
            global::UI.Notification.Notifications.ShowError("bizman_settings_notification_name_empty");
        }

        // The app opened on the answer to BizMan's question.
        private void SelectAnswered()
        {
            using var hotSpot = HotSpots.Time(_hotOpened);
            try
            {
                Select();
            }
            catch (Exception e)
            {
                Failed(e);
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

        // The panel came on: by Open, or with the menu when it was the app last looked at.
        internal void Shown()
        {
            using var hotSpot = HotSpots.Time(_hotOpened);
            try
            {
                if (_menu != null && _menu.appNameLabel != null)
                {
                    // Names first: an app of the same title key (another bank's) only changes its names.
                    _menu.appNameLabel.Arguments = _def.TitleArguments;
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
                    if (_strip != null)
                    {
                        _strip.ShowPageOf(_menuButton.transform);   // opened from the HUD phone or a link: its button in sight
                    }
                }
                else if (_def.NoButtons && _menu != null)
                {
                    // No button of its own: none of the others shows as the one open.
                    FullMenuAppButton[] all = _menu.GetComponentsInChildren<FullMenuAppButton>(false);
                    for (int i = 0; i < all.Length; i++)
                    {
                        all[i].HideSelectedIcon();
                    }
                }
                _def.Opened?.Invoke();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The panel went off: the menu closed, or the game opened one of its own apps.
        internal void Hidden()
        {
            using var hotSpot = HotSpots.Time(_hotOpened);
            try
            {
                if (_menuButton != null)
                {
                    _menuButton.HideSelectedIcon();
                }
                if (_def.TitleArguments != null && _menu != null && _menu.appNameLabel != null && ReferenceEquals(_menu.appNameLabel.Arguments, _def.TitleArguments))
                {
                    _menu.appNameLabel.Arguments = null;   // the game's own apps set only the key: no names of this app's are left on the bar
                }
                _def.Closed?.Invoke();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Takes everything this app added off the phone; safe twice and after the interface has gone. The shared pages stay.</summary>
        public void Remove()
        {
            if (_def != null && _def.Id != null && ById.TryGetValue(_def.Id, out PhoneApp known) && known == this)
            {
                ById.Remove(_def.Id);
            }
            Retire(_hudButton != null ? _hudButton.gameObject : null);
            Retire(_menuButton != null ? _menuButton.gameObject : null);
            Retire(Root);
            Retire(_dormant != null ? _dormant.gameObject : null);
            _hudButton = null;
            _menuButton = null;
            _pager = null;
            _strip = null;
            _dormant = null;
            _pending = false;
            Root = null;
        }

        // Out of its grid at once, renamed, then destroyed: an app of the same name may be put on in the same frame, and the
        // pager counts what is left at once (a destroyed object stays a child until the frame is over).
        private static void Retire(GameObject thing)
        {
            if (thing == null)
            {
                return;
            }
            if (GameManager.isCitySceneBeingUnloaded)
            {
                UnityEngine.Object.Destroy(thing);   // the interface is being taken down: nothing is moved in it
                return;
            }
            thing.SetActive(false);
            thing.name = "RealLife Gone";
            thing.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(thing);
        }
    }
}
