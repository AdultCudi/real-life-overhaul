using System;
using System.Collections.Generic;
using BAModAPI;
using Localizor;
using Localizor.LanguageChangeEvent;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Copies of the game's own interface objects, made at run time and never shipped. Everything the mod shows in the
    /// game's interface starts here: a live object of the game is copied while it sleeps (so none of its scripts wake up
    /// in the copy), the scripts that drive the original are taken off, and what is left (the pictures, the texts, the
    /// layout, the hover and click sounds) is the game's. See RealLifeOverhaul.Build/docs/game-ui.md.
    /// </summary>
    public static class UiClone
    {
        public static IModLogger Logger;

        /// <summary>
        /// An object that is switched off and stays off: whatever is copied into it does not run Awake or OnEnable,
        /// which is what lets a script be taken off a copy before it has done anything.
        /// </summary>
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

        /// <summary>
        /// Takes the game's own scripts off a sleeping copy, except the ones that only draw or sound (keep): what drove
        /// the original must not drive the copy. A script another script on the same object needs is taken off after it.
        /// </summary>
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

        // Whether another script still to be taken off the same object says it requires this one.
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

        /// <summary>Destroys every child of a sleeping copy that is not one of those named.</summary>
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
            button.onClick = new Button.ButtonClickedEvent();   // drops the calls saved with the game's object as well
            if (click != null)
            {
                button.onClick.AddListener(() => Safe(click));
            }
            return button;
        }

        /// <summary>
        /// Runs something the player clicked, inside the guard of the class the click's code is written in (Core/Guard):
        /// nothing thrown here reaches the game's interface, a fault counts for that class as its other entry points' do,
        /// and a click of a class or feature that is switched off does nothing.
        /// </summary>
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
            try
            {
                action();
            }
            catch (Exception e)
            {
                Guard.Fail(guard, e);
            }
        }

        /// <summary>
        /// A text of the game's given a translated line: through the game's own component where the object has one
        /// (it follows a change of language by itself), written once where it has none.
        /// </summary>
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
                // Key only writes when it changes: a copy that was saved with the same key still shows its template text.
                localised.SetValue(key.GetLocalization());
                return;
            }
            var plain = text.GetComponent<TMP_Text>();
            if (plain != null)
            {
                plain.text = key.GetLocalization();
            }
        }

        /// <summary>A text of the game's given words that are already final (a name, a time, a composed line).</summary>
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

        /// <summary>One line in Player.log for something of the game's that was not where it was expected.</summary>
        public static void Fallback(string what)
        {
            Logger?.Warn("Game UI: " + what + ".");
        }
    }
}
