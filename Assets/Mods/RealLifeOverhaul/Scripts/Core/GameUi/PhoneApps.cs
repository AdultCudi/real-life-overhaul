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
    /// app is first opened (<see cref="Ready"/>). An install waits in a line put on one app a frame (<see cref="Pump"/>);
    /// the buttons of a line show together after its last, and the pages are laid out once for them. Opening and the build
    /// are timed (HotSpots); costs nothing a frame once the line is empty.
    /// </summary>
    public sealed class PhoneApp
    {
        public static IModLogger Logger;

        private static readonly Dictionary<string, PhoneApp> ById = new Dictionary<string, PhoneApp>(32, StringComparer.Ordinal);
        private static readonly InstallQueue<PhoneApp> Waiting = new InstallQueue<PhoneApp>();
        private static readonly PhoneNames Names = new PhoneNames();
        private static readonly List<GameObject> Made = new List<GameObject>(80);   // buttons put on since the line began, shown together after its last
        private static readonly SpreadWatch Spread = new SpreadWatch();
        private static FullMenu _upMenu;        // the full menu found with its buttons made: the phone stays up for it
        private static PhoneAppQueue _pump;
        private static bool _batch;             // a line is being put on: its self-check line is written after its last
        private static int _batchApps, _most, _heldPasses, _searchesBefore, _askedBefore;
        private static string _slowest;

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
        private bool _queued;      // waiting in the line: its buttons not made yet
        private readonly int _hotOpened, _hotBuilt;

        /// <summary>The app's panel among the game's; null until the app is first opened (<see cref="Ready"/>).</summary>
        public GameObject Root { get; private set; }
        /// <summary>On the phone, or waiting in the line to go on: its buttons are there, and its panel is, or is still to be built.</summary>
        public bool Installed => _queued ? _menu != null : (_pending ? _menu != null : Root != null) && (_def.NoButtons || (_hudButton != null && _menuButton != null));
        public bool Showing => Root != null && Root.activeInHierarchy;
        public PhoneGridPager Pager => _pager;

        private PhoneApp(PhoneAppDef def)
        {
            _def = def;
            _hotOpened = HotSpots.Slot("Apps: " + def.Id + " opened");   // once an install: a name keeps its number for the session
            _hotBuilt = HotSpots.Slot("Apps: " + def.Id + " built");
        }

        /// <summary>
        /// The game's phone can take an app: its parts are there and the full menu made its buttons (FullMenu.Start, at the
        /// load), whether the menu shows or not: the apps go on in the first moments after a load, not all in the frames the
        /// menu is first opened. Once true, one comparison for that menu.
        /// </summary>
        public static bool PhoneUp
        {
            get
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (uis == null || uis.smartphoneUI == null || uis.fullMenu == null || uis.fullMenu.appsContainer == null)
                {
                    return false;
                }
                FullMenu menu = uis.fullMenu;
                if (ReferenceEquals(menu, _upMenu))
                {
                    return true;
                }
                SmartphoneUI phone = uis.smartphoneUI;
                if (phone.appButtons == null || phone.appButtons.Count == 0 || !MenuBuilt(menu))
                {
                    return false;
                }
                _upMenu = menu;
                return true;
            }
        }

        // FullMenu.Start makes a button for each of the game's apps from its template, named after the app, under the template's
        // parent, and the menu is closed again: a look along that row, never down the whole menu.
        private static bool MenuBuilt(FullMenu menu)
        {
            Transform template = null;
            try
            {
                FieldInfo field = GameContract.MenuAppTemplate.Field;
                template = field != null ? field.GetValue(menu) as Transform : null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (template == null || template.parent == null)
            {
                return menu.GetComponentInChildren<FullMenuAppButton>(false) != null;   // without the template: a button showing
            }
            Transform row = template.parent;
            for (int i = 0; i < row.childCount; i++)
            {
                Transform child = row.GetChild(i);
                if (child != template && child.gameObject.activeSelf && Enum.TryParse(child.name, out AppName _) && child.GetComponent<FullMenuAppButton>() != null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Puts the app in the line to go on the phone, one a frame (<see cref="Pump"/>), and gives it at once: it counts as
        /// installed, and opening it puts it on there and then. Null if the phone is not there yet (<paramref name="tryAgain"/>)
        /// or the app cannot go on it.
        /// </summary>
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
            if (def == null || string.IsNullOrEmpty(def.Id))
            {
                UiClone.Fallback("an app without a name is not added to the phone");
                return null;
            }
            var app = new PhoneApp(def) { _phone = phone, _menu = menu };
            if (!_batch)
            {
                _searchesBefore = Names.Searches;   // the line this app may begin counts its search of the phones from here
                _askedBefore = Names.Asked;
            }
            try
            {
                if (menu.appsContainer.Find(def.Id) != null || Enum.TryParse(def.Id, out AppName _) || OnPhone(phone, menu, def.Id))
                {
                    UiClone.Fallback("an app called '" + def.Id + "' is already on the phone, a second one is not added");
                    return null;
                }
                if (def.Build == null)
                {
                    UiClone.Fallback("'" + def.Id + "' has nothing to build its panel with, so it is not added");
                    return null;
                }
                app._dormant = UiClone.Dormant(menu.canvasGroup != null ? menu.canvasGroup.transform : menu.transform, "RealLife Dormant " + def.Id);
                app._pending = true;   // built when the app is first opened (Ready)
                ById[def.Id] = app;
                Names.Learn(def.Id);
                if (def.NoButtons)
                {
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Game UI: app '" + def.Id + "' is among the phone's apps without a button: the mod opens it itself; its panel is built when it is first opened.");
                    }
                    return app;
                }
                app.Queue(menu);
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

        // A button of that name on either phone, or an app of the mod's under it: both phones are searched once, when first
        // seen, then asked by name, so no install searches a whole phone.
        private static bool OnPhone(SmartphoneUI phone, FullMenu menu, string id)
        {
            int phoneId = phone.GetInstanceID(), menuId = menu.GetInstanceID();
            if (!Names.Read(phoneId, menuId))
            {
                Names.Begin(phoneId, menuId);
                SmartphoneAppButton[] hud = phone.GetComponentsInChildren<SmartphoneAppButton>(true);
                for (int i = 0; i < hud.Length; i++)
                {
                    Names.Learn(hud[i].name);
                }
                FullMenuAppButton[] inMenu = menu.GetComponentsInChildren<FullMenuAppButton>(true);
                for (int i = 0; i < inMenu.Length; i++)
                {
                    Names.Learn(inMenu[i].name);
                }
            }
            return Names.Has(id);
        }

        // In the line, and the full menu's pump woken. The first of a line begins its self-check's count.
        private void Queue(FullMenu menu)
        {
            if (!_batch)
            {
                _batch = true;
                _batchApps = 0;
                _most = 0;
                _heldPasses = 0;
                _slowest = null;
                Spread.Begin();
            }
            _queued = true;
            Waiting.Add(this);
            if (_pump == null)
            {
                _pump = menu.GetComponent<PhoneAppQueue>();
                if (_pump == null)
                {
                    _pump = menu.gameObject.AddComponent<PhoneAppQueue>();
                }
            }
            _pump.enabled = true;
        }

        /// <summary>
        /// One frame of the line, from the full menu's pump: the next app put on, or after the last the line's buttons shown and
        /// the pages laid out once. False when nothing is left to do: the pump then switches itself off.
        /// </summary>
        internal static bool Pump()
        {
            if (GameManager.isCitySceneBeingUnloaded)
            {
                Drop();
                return false;
            }
            if (Waiting.Count == 0 && Made.Count == 0 && !_batch)
            {
                return false;
            }
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            if (Waiting.Count == 0)
            {
                Finish();   // a frame of its own: the buttons shown and the two pagers' one pass each
                Spread.Frame(began);
                Said();
                return false;
            }
            WorkSlice slice = WorkSlice.Of(InstallQueue<PhoneApp>.SliceMs, 1);
            int put = 0;
            while (Waiting.Next(ref slice, out PhoneApp app))
            {
                app.PutNow();
                put++;
            }
            _most = Math.Max(_most, put);
            Spread.Frame(began);
            return true;
        }

        // Taken out of the line and put on now: opened before its turn came. False if it could not be put on.
        private bool Place()
        {
            if (!_queued)
            {
                return true;
            }
            Waiting.Take(this);
            PutNow();
            if (Waiting.Count == 0)
            {
                Finish();
                Said();
            }
            return Installed;
        }

        // Put on with the pagers held: a copy's arrival lays no grid out, and the line's last has each pager lay its grid out once.
        private void PutNow()
        {
            _queued = false;
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            int passes = PhoneGridPager.Passes;
            PhoneGridPager.Holding++;
            try
            {
                Put();
            }
            finally
            {
                PhoneGridPager.Holding--;
                _heldPasses += PhoneGridPager.Passes - passes;
            }
            if (Spread.Item(_batchApps, began))
            {
                _slowest = _def.Id;
            }
            _batchApps++;
        }

        // The buttons, copied from the game's and dressed, left hidden until the line's last is on (Finish).
        private void Put()
        {
            if (_menu == null || _phone == null)
            {
                Remove();   // the interface it was meant for has gone
                return;
            }
            SmartphoneUI phone = _phone;
            FullMenu menu = _menu;
            try
            {
                string templateUsed;
                Transform hudTemplate = Template(phone, GameContract.HudAppTemplate, phone.transform, typeof(SmartphoneAppButton), out templateUsed);
                string menuTemplateUsed;
                Transform menuTemplate = Template(menu, GameContract.MenuAppTemplate, menu.transform, typeof(FullMenuAppButton), out menuTemplateUsed);
                if (hudTemplate == null || menuTemplate == null || hudTemplate.parent == null || menuTemplate.parent == null)
                {
                    UiClone.Fallback("the phone's app button was not found (" + (hudTemplate == null ? "HUD phone" : "full menu") + "), so '" + _def.Id + "' is not added");
                    Remove();
                    return;
                }

                Sprite like = null;
                SmartphoneAppButton persona;
                if (phone.appButtons.TryGetValue(AppName.Persona, out persona) && persona != null)
                {
                    Transform named = persona.transform.Find("Icon");
                    Image picture = named != null ? named.GetComponent<Image>() : null;
                    like = picture != null ? picture.sprite : null;
                }
                Sprite icon = _def.Icon != null ? _def.Icon(like) : null;   // painted once a session and kept (AppIconPainter)

                Transform hud = UnityEngine.Object.Instantiate(hudTemplate, hudTemplate.parent, false);
                hud.gameObject.SetActive(false);   // the template is asleep already; a live button copied instead is not
                hud.name = _def.Id;
                hud.gameObject.tag = "Untagged";
                _hudButton = hud.GetComponent<SmartphoneAppButton>();
                Transform inMenu = UnityEngine.Object.Instantiate(menuTemplate, menuTemplate.parent, false);
                inMenu.gameObject.SetActive(false);
                inMenu.name = _def.Id;
                inMenu.gameObject.tag = "Untagged";
                _menuButton = inMenu.GetComponent<FullMenuAppButton>();
                if (_hudButton == null || _menuButton == null)
                {
                    UnityEngine.Object.Destroy(hud.gameObject);
                    UnityEngine.Object.Destroy(inMenu.gameObject);
                    UiClone.Fallback("the phone's app button is no longer an app button, so '" + _def.Id + "' is not added");
                    Remove();
                    return;
                }
                Dress(hud, _hudButton, _def.TitleKey, _def.TitleArguments, icon, Open);
                Dress(inMenu, _menuButton, _def.TitleKey, _def.TitleArguments, icon, Open);
                _hudButton.HideOutline();
                _hudButton.UpdateBadgeCount(_badge);   // a badge asked for while it waited
                _menuButton.HideSelectedIcon();
                Made.Add(hud.gameObject);
                Made.Add(inMenu.gameObject);

                _pager = PhoneGridPager.On(hudTemplate.parent, hudTemplate);
                // The full menu's row ends at the money, the date and the time ("RightData").
                Transform bar = menuTemplate.parent.parent;
                _strip = PhoneGridPager.OnStrip(menuTemplate.parent, menuTemplate, bar != null ? bar.Find("RightData") as RectTransform : null);

                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Game UI: app '" + _def.Id + "' is on the phone. Button copied from " + templateUsed + " (HUD) and " + menuTemplateUsed
                                 + " (full menu); icon " + (icon == null ? "the template's" : icon.name)
                                 + "; shown with the others put on with it, after the last; its panel is built when it is first opened.");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("'" + _def.Id + "' could not be put on the phone and is left off");
                Remove();
            }
        }

        // After the line's last: its buttons shown together, and each pager lays its grid out once for all of them.
        private static void Finish()
        {
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            PhoneGridPager.Holding++;
            try
            {
                for (int i = 0; i < Made.Count; i++)
                {
                    if (Made[i] != null)
                    {
                        Made[i].SetActive(true);
                    }
                }
            }
            finally
            {
                PhoneGridPager.Holding--;
                Made.Clear();
            }
            PhoneGridPager hud = PhoneGridPager.Hud, strip = PhoneGridPager.Strip;
            if (hud != null)
            {
                hud.Refresh();
            }
            if (strip != null)
            {
                strip.Refresh();
            }
            if (Spread.Item(-1, began) || _slowest == null)
            {
                _slowest = "the buttons shown and the pages laid out";
            }
        }

        // The line's self-check, with Developer diagnostics on: once, after its last.
        private static void Said()
        {
            if (!_batch)
            {
                return;
            }
            _batch = false;
            if (!Meta.Dev.Verbose || _batchApps == 0)
            {
                return;
            }
            int searches = Names.Searches - _searchesBefore, asked = Names.Asked - _askedBefore;
            string verdict = _most > 1 ? "FAIL, " + _most + " apps in one frame" : searches > 1 ? "FAIL, the phones searched " + searches + " times"
                : _heldPasses > 0 ? "FAIL, the pages laid out while the apps went on" : Spread.Verdict(true);
            PhoneGridPager hud = PhoneGridPager.Hud, strip = PhoneGridPager.Strip;
            string hudSaid = hud != null ? hud.Count + " apps on the HUD phone, " + hud.Pages + " page(s)" : "no HUD pages";
            string stripSaid = strip != null ? "the full menu's row " + strip.Pages + " page(s)" : "the full menu's row not paged";
            Logger?.Info("Phone apps check (paced install): " + Spread.Said(_slowest) + "; at most " + _most + " app(s) put on in a frame (wanted 1); the phones searched "
                         + searches + " time(s) for " + asked + " name(s) asked (wanted at most 1); the pages laid out " + _heldPasses
                         + " time(s) while they went on (wanted 0), then once on each phone (" + hudSaid + "; " + stripSaid + "): " + verdict + ".");
        }

        // The interface is going with the city: nothing waits for it.
        internal static void Drop()
        {
            for (int i = 0; i < Waiting.Count; i++)
            {
                Waiting[i]._queued = false;
                Waiting[i]._menu = null;
            }
            Waiting.Clear();
            Made.Clear();
            _batch = false;
            _pump = null;
            _upMenu = null;
        }

        /// <summary>The app's panel is there, built on the first call; false if it cannot be (the app is then taken off the phone).</summary>
        public bool Ready()
        {
            if (_queued && !Place())
            {
                return false;   // asked for before its turn in the line: put on now
            }
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
            if (count == _badge)
            {
                return;
            }
            _badge = count;
            if (_hudButton == null)
            {
                return;   // still in the line: it goes on with this number
            }
            try
            {
                _hudButton.UpdateBadgeCount(count);
                if (_pager != null)
                {
                    _pager.SetAttention();
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
            if (_queued)
            {
                Place();   // opened before its turn in the line: put on now
            }
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
            if (_queued)
            {
                _queued = false;
                Waiting.Take(this);
            }
            if (_def != null && _def.Id != null && ById.TryGetValue(_def.Id, out PhoneApp known) && known == this)
            {
                ById.Remove(_def.Id);
                Names.Forget(_def.Id);   // its buttons are renamed as they go: the name is free again
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
