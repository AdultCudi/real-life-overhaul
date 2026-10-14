using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        /// <summary>
        /// What the title's names are filled with ("{bank}" in the text of <see cref="TitleKey"/>), as the game fills a
        /// text's names (TextLocalizationComponent.Arguments, a dictionary of name and words, or an object with one field per name); null for a title without
        /// names. The title then still follows a change of language.
        /// </summary>
        public object TitleArguments;
        /// <summary>Given one of the game's own app icons to model its picture on; may return null (the button then keeps the template's).</summary>
        public Func<Sprite, Sprite> Icon;
        /// <summary>Builds the app's panel asleep under the dormant holder it is given, from the game's own (GameUi.AppShell); null if it cannot.</summary>
        public Func<FullMenu, Transform, GameObject> Build;
        public Action Opened;
        public Action Closed;
        /// <summary>
        /// An app the mod opens itself and the player never picks (a newspaper picked up, a counter's page): its panel is
        /// among the game's own app panels and opens as they do, with no button on either phone.
        /// </summary>
        public bool NoButtons;
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
    ///
    /// THE PANEL IS BUILT WHEN IT IS FIRST OPENED. Install puts the two buttons on the phones; the panel (a copy of the
    /// game's MyEmployees app, a few hundred objects, PhoneAppDef.Build) is made the first time the app is opened
    /// (<see cref="Ready"/>), with the game standing still under the menu, instead of in a frame of play in the first
    /// seconds after a load, where some twenty apps once built theirs one a frame. Nothing about the app changes but that
    /// moment: until it is built its panel is not among the game's, which only ever looks for its own by name, and every
    /// app's page waits for its panel before it writes anything (an app's Opened, Want and Changed do nothing while it has
    /// none). An app whose panel cannot be built after all is taken off the phone at that click, with the builder's own
    /// line in Player.log, as an install that failed always was.
    ///
    /// Timing: opening (Open, Shown, Hidden) and the build are timed under names of their own ("Apps: (id) opened",
    /// "Apps: (id) built", Core/HotSpots); every click of a button the kit rewires is timed by UiClone.Safe. Per frame:
    /// nothing.
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
        private PhoneGridPager _strip;
        private Transform _dormant;
        private int _badge;
        private bool _pending;     // on the phone, its panel not built yet (Ready builds it)
        private readonly int _hotOpened, _hotBuilt;

        /// <summary>The app's panel among the game's; null until the app is first opened (<see cref="Ready"/>).</summary>
        public GameObject Root { get; private set; }
        /// <summary>On the phone: its buttons are there, and its panel is, or is still to be built when it is first opened.</summary>
        public bool Installed => (_pending ? _menu != null : Root != null) && (_def.NoButtons || (_hudButton != null && _menuButton != null));
        public bool Showing => Root != null && Root.activeInHierarchy;
        public PhoneGridPager Pager => _pager;

        private PhoneApp(PhoneAppDef def)
        {
            _def = def;
            _hotOpened = HotSpots.Slot("Apps: " + def.Id + " opened");   // (once an install: a name keeps its number for the session)
            _hotBuilt = HotSpots.Slot("Apps: " + def.Id + " built");
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
                app._pending = true;   // the panel is built when the app is first opened (Ready)
                if (def.NoButtons)
                {
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Game UI: app '" + def.Id + "' is among the phone's apps without a button: the mod opens it itself; its panel is built when it is first opened.");
                    }
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

                // The dot the game shows under the app that is open is the one picture of a round dot at hand.
                Transform selected = menuTemplate.Find("SelectedIcon");
                Image dot = selected != null ? selected.GetComponent<Image>() : null;
                app._pager = PhoneGridPager.On(hudTemplate.parent, hudTemplate, dot != null ? dot.sprite : null);
                // The full menu's row of buttons ends at the money, the date and the time beside it ("RightData").
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

        // Whether either phone has a button of that name already: an app of that name is on it, its panel perhaps not built yet.
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

        /// <summary>
        /// The app's panel is there: built now if this is the first time it is asked for (the app is being opened). False if
        /// it is not and cannot be: the game's parts were not as expected (the builder has said what was missing in
        /// Player.log), and the app is taken off the phone, as an install that failed always was; or it was taken off already.
        /// </summary>
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

        // The button each phone copies for its apps: its private "appButtonTemplate"; failing that the object of that
        // name; failing that any button of the right kind (a copy of a live button works the same: the click the game
        // adds in code is not copied with it).
        private static Transform Template(object owner, GameMember templateField, Transform under, Type buttonType, out string used)
        {
            used = "appButtonTemplate";
            try
            {
                FieldInfo field = templateField.Field;   // (resolved once: Core/GameContract)
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
                    title.Arguments = titleArguments;   // (the component writes its text again with the names filled in)
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

        /// <summary>
        /// Opens the app as the game opens its own (SmartphoneUI.OpenApp, FullMenu.ShowApp, FullMenu.SelectApp). Each of
        /// the game's steps before the panel comes on is a method of its own, so that a game lacking what one of them
        /// needs only goes without that step (Core/GameMembers): on Build 3675 the one question of BizMan's that named the
        /// game's newer HudConfirm.Show kept the whole of this from being compiled, and no app of the mod's opened.
        /// A click that opens nothing says why in Player.log, once for each app in a session.
        /// </summary>
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

        // An open that failed: said once for the app, with the member a game lacks or the error (whose whole text follows).
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

        // The ids of the apps whose failure to open has been said this session.
        private static readonly HashSet<string> UnopenedSaid = new HashSet<string>(StringComparer.Ordinal);

        private void Unopened(string why)
        {
            if (UnopenedSaid.Add(_def.Id))
            {
                Logger?.Warn("Game UI: the app '" + _def.Id + "' did not open when it was clicked: " + why + ". (Said once for each app in a session.)");
            }
        }

        // The game's first steps: the HUD phone's outline off and the building card closed. A game without either goes on without it.
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

        // Leaving BizMan's settings with changes not saved asks first, whichever app is opened next, and is refused
        // while the business has no name, as the game's own FullMenu.ShowApp does. True when that has taken the click:
        // the question is up (the app opens on its answer), or the game's own word on the name is shown.
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

        // The app opened on the answer to BizMan's question, from within the game's question box.
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

        // The panel has come on: by Open(), or with the menu when it was the app last looked at.
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
                    // No button of its own: none of the others is shown as the one open.
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

        // The panel has gone off: the menu closed, or the game opened one of its own apps.
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
                    _menu.appNameLabel.Arguments = null;   // the game's own apps set only the key of the top bar: no names of this app's are left on it
                }
                _def.Closed?.Invoke();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// Takes everything this app added off the phone. Safe to call twice, and after the game's interface has gone.
        /// The pages are the grid's and the row's, shared by every app on them: they stay, and page what is left.
        /// </summary>
        public void Remove()
        {
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

        // Out of its grid or container at once, under another name, then destroyed: an app of the same name may be put
        // on again in the same frame (the game finds an app's panel by its name, and Install refuses a second one), and
        // the pager counts at once what is left (a destroyed object stays a child until the frame is over).
        private static void Retire(GameObject thing)
        {
            if (thing == null)
            {
                return;
            }
            if (GameManager.isCitySceneBeingUnloaded)
            {
                UnityEngine.Object.Destroy(thing);   // (the interface is being taken down: nothing is moved in it)
                return;
            }
            thing.SetActive(false);
            thing.name = "RealLife Gone";
            thing.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(thing);
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
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "PanelFade", typeof(PanelFade));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

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

        private static readonly int HotUpdate = HotSpots.Slot("Core: PanelFade.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
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
