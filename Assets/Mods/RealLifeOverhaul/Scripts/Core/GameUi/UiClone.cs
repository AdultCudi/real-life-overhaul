using System;
using System.Collections.Generic;
using BAModAPI;
using Localizor;
using Localizor.LanguageChangeEvent;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Copies of the game's own interface objects, made at run time and never shipped: a live object is copied while
    /// asleep (none of its scripts wake in the copy), the scripts that drive the original are taken off, and what is left
    /// is the game's. See RealLifeOverhaul.Build/docs/game-ui.md.
    /// </summary>
    public static class UiClone
    {
        public static IModLogger Logger;

        /// <summary>An object switched off for good: a copy in it runs no Awake or OnEnable, so its scripts can be taken off first.</summary>
        public static Transform Dormant(Transform parent, string name)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.SetActive(false);
            holder.transform.SetParent(parent, false);
            return holder.transform;
        }

        /// <summary>A copy of a live object, asleep under a dormant holder. Null (and one log line) if there is nothing to copy.</summary>
        public static GameObject Asleep(GameObject original, Transform dormant, string name)
        {
            if (original == null || dormant == null)
            {
                Fallback("nothing to copy for '" + name + "'");
                return null;
            }
            GameObject copy = UnityEngine.Object.Instantiate(original, dormant, false);
            copy.name = name;
            if (!copy.CompareTag("Untagged"))
            {
                copy.tag = "Untagged";   // the game's "UiTemplate" tag marks what Transform.ResetTemplate may destroy
            }
            return copy;
        }

        /// <summary>Takes the game's scripts off a sleeping copy but those <paramref name="keep"/> wants; a script another requires goes after it.</summary>
        public static void StripGameScripts(GameObject copy, Func<MonoBehaviour, bool> keep)
        {
            if (copy == null)
            {
                return;
            }
            var all = new List<MonoBehaviour>(64);
            copy.GetComponentsInChildren(true, all);
            var gameAssembly = typeof(SmartphoneUI).Assembly;
            all.RemoveAll(m => m == null || m.GetType().Assembly != gameAssembly || (keep != null && keep(m)));
            for (int pass = 0; pass < 4 && all.Count > 0; pass++)
            {
                for (int i = all.Count - 1; i >= 0; i--)
                {
                    MonoBehaviour script = all[i];
                    if (script == null)
                    {
                        all.RemoveAt(i);
                    }
                    else if (pass == 3 || !Needed(script, all))
                    {
                        UnityEngine.Object.DestroyImmediate(script);
                        all.RemoveAt(i);
                    }
                }
            }
        }

        // Another script still to be taken off the same object requires this one.
        private static bool Needed(MonoBehaviour script, List<MonoBehaviour> others)
        {
            Type type = script.GetType();
            for (int i = 0; i < others.Count; i++)
            {
                MonoBehaviour other = others[i];
                if (other == null || other == script || other.gameObject != script.gameObject)
                {
                    continue;
                }
                object[] needs = other.GetType().GetCustomAttributes(typeof(RequireComponent), true);
                for (int n = 0; n < needs.Length; n++)
                {
                    var need = (RequireComponent)needs[n];
                    if (Is(need.m_Type0, type) || Is(need.m_Type1, type) || Is(need.m_Type2, type))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool Is(Type wanted, Type actual)
        {
            return wanted != null && wanted.IsAssignableFrom(actual);
        }

        public static void KeepOnly(Transform parent, params string[] names)
        {
            if (parent == null)
            {
                return;
            }
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Array.IndexOf(names, child.name) < 0)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        /// <summary>A child by its path ("Left/WhiteBox"); null and one log line if the game no longer has it.</summary>
        public static Transform Need(Transform root, string path, string what)
        {
            Transform found = root != null ? root.Find(path) : null;
            if (found == null)
            {
                Fallback(what + ": '" + path + "' is not in the game's interface");
            }
            return found;
        }

        /// <summary>A button of the game's with nothing left on it of what it did there, and the given click.</summary>
        public static Button Rewire(Component under, Action click)
        {
            Button button = under != null ? under.GetComponent<Button>() : null;
            if (button == null)
            {
                return null;
            }
            button.onClick = new Button.ButtonClickedEvent();   // drops the calls saved with the game's object too
            if (click != null)
            {
                button.onClick.AddListener(() => Safe(click));
            }
            return button;
        }

        /// <summary>Runs a click inside the guard of the class its code is in, timed under that class and method ("Click: FinanceApp.Build").</summary>
        public static void Safe(Action action)
        {
            if (action == null)
            {
                return;
            }
            FeatureGuard guard = Guard.OfHandler(action);
            if (guard != null && guard.Off)
            {
                return;
            }
            using var hotSpot = HotSpots.Time(Guard.HandlerSlot(action, "Click"));
            try
            {
                action();
            }
            catch (Exception e)
            {
                Guard.Fail(guard, e);
            }
        }

        /// <summary>A text given a translated line: through the game's component where it has one (it follows the language), else written once.</summary>
        public static void Key(Component text, string key)
        {
            if (text == null)
            {
                return;
            }
            var localised = text.GetComponent<TextLocalizationComponent>();
            if (localised != null)
            {
                localised.Arguments = null;
                localised.Prefix = string.Empty;
                localised.Suffix = string.Empty;
                localised.Key = key;
                // Key writes only when it changes: a copy saved with the same key would keep its template's text.
                localised.SetValue(key.GetLocalization());
                return;
            }
            var plain = text.GetComponent<TMP_Text>();
            if (plain != null)
            {
                plain.text = key.GetLocalization();
            }
        }

        /// <summary>A text given final words (a name, a time, a composed line).</summary>
        public static void Words(Component text, string words)
        {
            if (text == null)
            {
                return;
            }
            var localised = text.GetComponent<TextLocalizationComponent>();
            if (localised != null)
            {
                localised.Arguments = null;
                localised.Prefix = string.Empty;
                localised.Suffix = string.Empty;
                localised.SetValue(words ?? string.Empty, true);   // clears the key: a language change must not write over it
                return;
            }
            var plain = text.GetComponent<TMP_Text>();
            if (plain != null)
            {
                plain.text = words ?? string.Empty;
            }
        }

        /// <summary>A station list's entry made the list's one template, asleep: the game's other entries gone, its label set for two lines.</summary>
        internal static void EntryTemplate(Transform entry, Transform label)
        {
            Transform content = entry.parent;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                if (child != entry)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
            entry.name = "Entry";
            if (!entry.CompareTag("Untagged"))
            {
                entry.tag = "Untagged";
            }
            var entryRect = (RectTransform)entry;
            entryRect.sizeDelta = new Vector2(entryRect.sizeDelta.x, 104f);   // the game's 88.8 holds one line; this holds a name and a smaller line
            var text = label.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.richText = true;
                text.fontStyle &= ~FontStyles.UpperCase;   // the name gets the game's capitals by its own tag; the line under it reads as a sentence
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Ellipsis;
            }
            entry.gameObject.SetActive(false);
        }

        /// <summary>A copy's canvas group, if it has one, opaque and taking clicks, whatever state the original was copied in.</summary>
        internal static void Visible(CanvasGroup group)
        {
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }
        }

        /// <summary>Nothing under a window left selected: its entries are reused, and the confirm key presses whatever is.</summary>
        internal static void Deselect(Transform root)
        {
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null && events.currentSelectedGameObject.transform.IsChildOf(root))
            {
                events.SetSelectedGameObject(null);
            }
        }

        /// <summary>One line in Player.log for something of the game's that was not where it was expected.</summary>
        public static void Fallback(string what)
        {
            Logger?.Warn("Game UI: " + what + ".");
        }
    }
}
