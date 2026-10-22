using System;
using System.Collections.Generic;
using BAModAPI;
using Localizor;
using Localizor.LanguageChangeEvent;
using TMPro;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Moving a mod app on the phone, as a long press does on a real phone: a right click on its HUD button opens the
    /// game's own list window with each place it can go; a pick moves it at once and both phones follow. Also the options
    /// page's reset and two tests. Costs nothing a frame.
    /// </summary>
    public static class PhoneMove
    {
        public static IModLogger Logger;

        private const float Width = 900f;        // the HUD's units (3,840 across); the game's list is 775: room for a folder's name and a note
        private const float EntryHeight = 104f;  // the game's own entry
        private const float NoticeSeconds = 5f;

        private static readonly DeskList List = new DeskList("RealLife PhoneMove", Width, EntryHeight);
        private static readonly object Owner = new object();
        private static readonly int[] Places =
        {
            PhoneLayoutRules.Money, PhoneLayoutRules.Work, PhoneLayoutRules.Life, PhoneLayoutRules.Business, PhoneLayoutRules.Setup,
            PhoneLayoutRules.Home, PhoneLayoutRules.Dock
        };
        private static readonly Action<int> OnPick = Picked;
        private static readonly Action OnClosed = Closed;
        private static string _app, _name;

        /// <summary>The list for the app of this button, on the HUD phone.</summary>
        public static void Open(Transform button, PhoneGridPager pager)
        {
            if (button == null || pager == null || !pager.Foldered)
            {
                return;
            }
            TravelList list = List.Take(Owner);
            if (list == null)
            {
                Notifications.ShowError("reallife_phone_move_cannot", null, false);
                return;
            }
            string id = button.name;
            string name = TitleOf(button);
            int now = PhoneLayoutPrefs.Layout.Place(id);
            int usual = PhoneLayoutRules.DefaultOf(id);
            list.Begin(T("reallife_phone_move_title", "app", name), T("reallife_phone_move_info"));
            for (int k = 0; k < Places.Length; k++)
            {
                int place = Places[k];
                bool can = PhoneLayoutRules.CanMove(now, place, pager.InDock);
                string note = place == now ? T("reallife_phone_move_here")
                    : place == PhoneLayoutRules.Dock && !can ? T("reallife_phone_move_dock_full")
                    : place == usual ? T("reallife_phone_move_usual")
                    : string.Empty;
                list.Add(k, PlaceName(place), note, "reallife_phone_move_button", can);
            }
            _app = id;
            _name = name;
            list.Open(OnPick, OnClosed);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Game UI: the list to move '" + id + "' is open: it is in " + PhoneLayoutRules.Token(now) + ", its usual place " + PhoneLayoutRules.Token(usual)
                             + ", the dock holds " + pager.InDock + " of " + PhoneLayoutRules.DockMost + ".");
            }
        }

        private static void Picked(int k)
        {
            string app = _app, name = _name;
            List.Close();
            if (app == null || k < 0 || k >= Places.Length)
            {
                return;
            }
            int place = Places[k];
            if (!PhoneLayoutPrefs.Move(app, place))
            {
                Notifications.ShowError("reallife_phone_move_cannot", null, false);
                return;
            }
            var words = new Dictionary<string, string> { { "app", name }, { "place", PlaceName(place) } };
            Notifications.Show(NotificationType.Info, "reallife_phone_moved", words, NoticeSeconds, null, null, true, false);
            if (Meta.Dev.Verbose)
            {
                PhoneGridPager hud = PhoneGridPager.Hud, strip = PhoneGridPager.Strip;
                Logger?.Info("Phone check (move): '" + app + "' to " + PhoneLayoutRules.Token(place) + "; the HUD phone " + (hud != null ? hud.Check() : "not there")
                             + "; the full menu " + (strip != null ? strip.Check() : "not there") + ".");
            }
        }

        private static void Closed()
        {
            _app = null;
            _name = null;
        }

        /// <summary>A place's name: a folder's, the home screen or the dock.</summary>
        public static string PlaceName(int place)
        {
            return place == PhoneLayoutRules.Dock ? T("reallife_phone_place_dock")
                : place >= 0 && place < PhoneLayoutRules.FolderCount ? T(PhoneLayoutRules.FolderKeys[place])
                : T("reallife_phone_place_home");
        }

        // As its button shows it, in the game's language: the text of its title's component, as PhoneApp dresses it.
        private static string TitleOf(Transform button)
        {
            var localised = button.GetComponentInChildren<TextLocalizationComponent>(true);
            var title = localised != null ? localised.GetComponent<TMP_Text>() : null;
            string words = title != null ? title.text : null;
            return string.IsNullOrEmpty(words) ? button.name : words;
        }

        /// <summary>The options page's button: every app back in its usual place, and the dock as it came.</summary>
        public static void ResetAll()
        {
            try
            {
                List.Close();
                PhoneLayoutPrefs.Reset();
                Notifications.Show(NotificationType.Success, "reallife_phone_layout_reset_done", null, NoticeSeconds, null, null, true, false);
                Logger?.Info("Game UI: every app of the mod's is back in its usual place on the phone.");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Test: every button of both phones in exactly one place (PhoneGridPager.Check).</summary>
        public static void TestCheck()
        {
            try
            {
                PhoneGridPager hud = PhoneGridPager.Hud, strip = PhoneGridPager.Strip;
                if (hud == null && strip == null)
                {
                    Notifications.Show(NotificationType.Error, "reallife_phone_test_none", null, 8f, null, null, true, false);
                    return;
                }
                string hudSays = hud != null ? hud.Check() : "not there: PASS";
                string stripSays = strip != null ? strip.Check() : "not there: PASS";
                bool ok = !hudSays.Contains("FAIL") && !stripSays.Contains("FAIL");
                Logger?.Info("Phone check (folders): the HUD phone " + hudSays + "; the full menu's row " + stripSays + "; the layout moves " + PhoneLayoutPrefs.Layout.Moves
                             + " app(s) and keeps " + PhoneLayoutPrefs.Layout.Kept + " part(s) of another version: " + (ok ? "PASS" : "FAIL") + ".");
                Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, ok ? "reallife_phone_test_pass" : "reallife_phone_test_fail", null, 8f, null, null, true, false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Test: the HUD phone opens its first folder with an app; the Back tile or the cancel key closes it.</summary>
        public static void TestFolder()
        {
            try
            {
                PhoneGridPager hud = PhoneGridPager.Hud;
                if (hud == null || !hud.Foldered)
                {
                    Notifications.Show(NotificationType.Error, hud == null ? "reallife_phone_test_none" : "reallife_phone_test_off", null, 8f, null, null, true, false);
                    return;
                }
                for (int f = 0; f < PhoneLayoutRules.FolderCount; f++)
                {
                    if (hud.Members(f) > 0)
                    {
                        hud.OpenFolder(f);
                        Notifications.Show(NotificationType.Info, "reallife_phone_test_opened", new Dictionary<string, string> { { "folder", PlaceName(f) } }, 8f, null, null, true, false);
                        Logger?.Info("Phone check (folder): " + hud.Check() + ".");
                        return;
                    }
                }
                Notifications.Show(NotificationType.Error, "reallife_phone_test_empty", null, 8f, null, null, true, false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>The window went with the city's interface (the HUD pager going): a later move makes it again.</summary>
        public static void Forget()
        {
            List.Destroy(e => Logger?.Error(e));
            _app = null;
            _name = null;
        }

        private static string T(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1]);
            }
            return text;
        }
    }
}
