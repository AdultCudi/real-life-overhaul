using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Other mods' app buttons that stand nowhere on the HUD phone's grid, found for the phone's model (PhoneItemsGame):
    /// those a mod copied onto the full menu's own row beside the game's apps (the game has no call for an app, so a mod
    /// copies the row's template), and those it hung on the HUD phone itself outside the grid. The kit hides that row and
    /// covers the phone, so without a tile of their own they could not be reached at all. Nothing of another mod's is
    /// moved or changed: its button stays where its mod put it, since the mod finds it there again, and is pressed through
    /// itself. Looked at when the phone or the full menu opens and about once a second while the kit shows, by the objects
    /// alone (<see cref="Signature"/>); names, words and listeners are read only when those changed. Cost: a walk of the
    /// row's children and the phone's objects a look, nothing allocated.
    /// </summary>
    internal static class PhoneExtras
    {
        private const int Depth = 5;   // levels of the phone walked for a button hung on it

        private static readonly List<string> Names = new List<string>(48);
        private static readonly List<string> Titles = new List<string>(48);
        private static readonly List<Button> Known = new List<Button>(4);   // the game's own controls on the phone, which the kit presses itself
        private static readonly List<Transform> Hangers = new List<Transform>(4);   // other mods' buttons hung on the phone, switched on or off, as last read

        private static Transform _row, _frame, _grid, _kit;

        /// <summary>The HUD phone the kit stands on: its container, its grid, the kit's own root there and the game's own controls on it.</summary>
        internal static void Phone(Transform frame, Transform grid, Transform kit, Button collapse, Button driver, Button skip)
        {
            _frame = frame;
            _grid = grid;
            _kit = kit;
            Known.Clear();
            Known.Add(collapse);
            Known.Add(driver);
            Known.Add(skip);
        }

        /// <summary>The kit phone went: nothing of the phone is held. The full menu's row is looked for again when asked.</summary>
        internal static void Forget()
        {
            _frame = _grid = _kit = null;
            _row = null;
            Known.Clear();
            Hangers.Clear();
        }

        /// <summary>The full menu's own row of app buttons, switched on or off; null before the game built it.</summary>
        internal static Transform Row()
        {
            if (_row == null)
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                Transform canvas = uis != null && uis.fullMenu != null && uis.fullMenu.canvasGroup != null ? uis.fullMenu.canvasGroup.transform : null;
                _row = canvas != null ? canvas.Find("Topbar/AppButtons") : null;
            }
            return _row;
        }

        /// <summary>
        /// The row's and the phone's objects folded into one number: it moves when a button comes or goes, when one of the
        /// row's is switched, and when another mod switches a button it hung on the phone. What the game switches on its own
        /// phone (the radio's row in a car) does not move it.
        /// </summary>
        internal static int Signature()
        {
            int hash = 17;
            Transform row = Row();
            for (int i = 0; row != null && i < row.childCount; i++)
            {
                Transform child = row.GetChild(i);
                hash = unchecked(hash * 31 + child.GetInstanceID() * 2 + (child.gameObject.activeSelf ? 1 : 0));
            }
            if (_frame == null)
            {
                return hash;
            }
            hash = Shape(_frame, 0, unchecked(hash * 31 + 7));
            for (int i = 0; i < Hangers.Count; i++)
            {
                hash = unchecked(hash * 31 + (Hangers[i] != null && Hangers[i].gameObject.activeSelf ? 1 : 0));
            }
            return hash;
        }

        private static int Shape(Transform under, int depth, int hash)
        {
            for (int i = 0; i < under.childCount; i++)
            {
                Transform child = under.GetChild(i);
                if (child == _grid || child == _kit)
                {
                    continue;   // the grid's buttons are the pager's, the kit's root its own
                }
                hash = unchecked(hash * 31 + child.GetInstanceID());
                if (depth < Depth)
                {
                    hash = Shape(child, depth + 1, hash);
                }
            }
            return hash;
        }

        /// <summary>
        /// Adds the extras after the grid's apps, each as another mod's app on the home screen: its button, its name among
        /// the phone's (its own, numbered when another has it) and where it stands. One the grid lists already, by its
        /// name or its words, is left out: one tile an app.
        /// </summary>
        internal static void Add(List<Transform> buttons, List<int> kinds, List<int> places, List<string> ids, List<PhoneItemFrom> from)
        {
            Names.Clear();
            Titles.Clear();
            for (int i = 0; i < buttons.Count; i++)
            {
                Names.Add(ids[i]);
                Titles.Add(kinds[i] == PhoneLayoutRules.OtherItem ? Words(buttons[i]) : null);
            }
            Transform row = Row();
            for (int i = 0; row != null && i < row.childCount; i++)
            {
                Transform child = row.GetChild(i);
                if (!Standing(child) || child.GetComponent<Button>() == null)
                {
                    continue;
                }
                string name = child.name;
                if (!UiForeignApps.Others(name, Enum.TryParse(name, out AppName _)))
                {
                    continue;
                }
                string words = Words(child);
                if (!UiForeignApps.Listed(name, words, Names, Titles, Names.Count))
                {
                    Take(child, name, words, PhoneItemFrom.Row, buttons, kinds, places, ids, from);
                }
            }
            Hangers.Clear();
            if (_frame != null)
            {
                Hung(_frame, 0, buttons, kinds, places, ids, from);
            }
        }

        // A button the row shows, or one its pager put on another page: a button its own mod switched off is not offered.
        private static bool Standing(Transform child)
        {
            if (child.gameObject.activeSelf)
            {
                return true;
            }
            var mark = child.GetComponent<PageMark>();
            return mark != null && mark.Off;
        }

        // A button another mod hung on the phone outside its grid: one its own code listens to and none of the game's does,
        // an app while its mod has it switched on. What stands under a button is its picture and its words, so the walk stops there.
        private static void Hung(Transform under, int depth, List<Transform> buttons, List<int> kinds, List<int> places, List<string> ids, List<PhoneItemFrom> from)
        {
            for (int i = 0; i < under.childCount; i++)
            {
                Transform child = under.GetChild(i);
                if (child == _grid || child == _kit)
                {
                    continue;
                }
                Button button = child.GetComponent<Button>();
                if (button != null)
                {
                    if (!Known.Contains(button) && UiForeignGame.ButtonOwner(button) != null)
                    {
                        Hangers.Add(child);
                        if (child.gameObject.activeSelf)
                        {
                            Take(child, UiForeignApps.Unique(child.name, Names, Names.Count), Words(child), PhoneItemFrom.Frame, buttons, kinds, places, ids, from);
                        }
                    }
                    continue;
                }
                if (depth < Depth)
                {
                    Hung(child, depth + 1, buttons, kinds, places, ids, from);
                }
            }
        }

        private static void Take(Transform button, string id, string words, PhoneItemFrom where, List<Transform> buttons, List<int> kinds, List<int> places, List<string> ids,
            List<PhoneItemFrom> from)
        {
            buttons.Add(button);
            kinds.Add(PhoneLayoutRules.OtherItem);
            places.Add(PhoneLayoutRules.Home);
            ids.Add(id);
            from.Add(where);
            Names.Add(id);
            Titles.Add(words);
        }

        private static string Words(Transform button)
        {
            TMP_Text label = PhoneItemsGame.TitleLabel(button);
            return label != null ? label.text : null;
        }
    }
}
