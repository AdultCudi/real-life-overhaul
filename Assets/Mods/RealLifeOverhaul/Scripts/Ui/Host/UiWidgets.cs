using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's own widgets, read and pressed through themselves, so a kit screen never works out the game's data and
    /// every listener runs as a click would run it (the game's, other mods' patches, Going Public's). The vanilla object is
    /// the truth for what shows (activeSelf), what can be pressed (interactable), its words and its tint. The host plays
    /// the click itself when a press answers true. What the listeners do is the game's own work, run inside a HotSpots.Game
    /// scope, so the session record keeps it out of the mod's figures. Cost: what each call does, only when a screen asks;
    /// nothing a frame.
    /// </summary>
    public static class UiWidgets
    {
        private static readonly List<TMP_Text> Texts = new List<TMP_Text>(8);

        /// <summary>A button the player could press now: there, shown and not held.</summary>
        public static bool Shows(Button b) => b != null && b.gameObject.activeInHierarchy && b.interactable;

        /// <summary>A button that is there and shown, pressable or not (the kit draws a held one disabled).</summary>
        public static bool There(Component c) => c != null && c.gameObject.activeSelf;

        /// <summary>The words on a button (or any piece): its first text shown.</summary>
        public static string Label(Component c)
        {
            if (c == null)
            {
                return null;
            }
            Texts.Clear();
            try
            {
                c.GetComponentsInChildren(false, Texts);
                for (int i = 0; i < Texts.Count; i++)
                {
                    if (!string.IsNullOrEmpty(Texts[i].text))
                    {
                        return MapText.OneLine(Texts[i].text);
                    }
                }
            }
            catch (Exception e) when (UiForeignGame.Spoilt(e, c))
            {
                // another mod's text threw as it was read (said once, with its mod): this one label is left empty, nothing else is touched
            }
            return null;
        }

        /// <summary>A text's words with the game's rich text taken off: the kit sets its own type.</summary>
        public static string Text(TMP_Text t)
        {
            try
            {
                return t != null ? MapText.Plain(t.text) : null;
            }
            catch (Exception e) when (UiForeignGame.Spoilt(e, t))
            {
                return null;   // another mod's text threw (said once, with its mod): the label is left empty
            }
        }

        /// <summary>A text's colour, as the game tints it (Going Public tints relayed notices by their sender).</summary>
        public static Color Tint(Graphic g) => g != null ? g.color : Color.clear;

        /// <summary>A toggle's state.</summary>
        public static bool On(Toggle t) => t != null && t.isOn;

        /// <summary>A drop-down's options' words and the chosen one's index; -1 when there is none.</summary>
        public static int Options(TMP_Dropdown d, List<string> into)
        {
            into.Clear();
            if (d == null)
            {
                return -1;
            }
            for (int i = 0; i < d.options.Count; i++)
            {
                into.Add(MapText.OneLine(d.options[i].text));
            }
            return d.value;
        }

        /// <summary>An input field's words.</summary>
        public static string Field(TMP_InputField f) => f != null ? f.text : null;

        /// <summary>A button pressed as a click presses it; false when it is not there to press.</summary>
        public static bool Press(Button b)
        {
            if (!Shows(b))
            {
                UiRefusals.Because(Refusal(b, true));
                return false;
            }
            Hand(b);
            return true;
        }

        /// <summary>
        /// A button pressed as a click presses it, where the game keeps the button's own object switched off until the mouse
        /// is over its row (UI.HoverEffect switches a row's outline, which holds the row's button, on under the pointer and
        /// off again: an order's goods in the items list). The kit hides the game's rows from the pointer, so that object
        /// never comes on, and asking whether it shows would refuse every press. The row itself must show and the button
        /// must not be held; false otherwise.
        /// </summary>
        public static bool PressUnder(Button b, Component row)
        {
            if (!ShowsUnder(b, row))
            {
                UiRefusals.Because(b != null && row != null && !row.gameObject.activeInHierarchy ? "the game's row '" + row.name + "' does not show" : Refusal(b, false));
                return false;
            }
            Hand(b);
            return true;
        }

        /// <summary>Whether a button the game switches on only under the pointer could be pressed now: its row shows and it is not held.</summary>
        public static bool ShowsUnder(Button b, Component row) => b != null && row != null && row.gameObject.activeInHierarchy && b.interactable;

        // Why a button takes no press, in words for the refusal's line: made only when one is refused.
        private static string Refusal(Button b, bool mustShow)
        {
            if (b == null)
            {
                return "the game's button is not there";
            }
            if (mustShow && !b.gameObject.activeInHierarchy)
            {
                return "the game's button '" + b.name + "' is switched off (its own object or one above it)";
            }
            return "the game's button '" + b.name + "' is held by the game (not interactable)";
        }

        /// <summary>
        /// A button of the game's pressed as a click presses it, by a caller that has checked it may be pressed: its listeners
        /// (the game's, other mods', Going Public's) run as the game's own work (HotSpots.Game), not the mod's.
        /// </summary>
        public static void Hand(Button b)
        {
            using (HotSpots.Game())
            {
                b.onClick.Invoke();
            }
        }

        /// <summary>A toggle set through itself, so its listeners hear it; false when it cannot be changed now.</summary>
        public static bool Set(Toggle t, bool on)
        {
            if (t == null || !t.gameObject.activeInHierarchy || !t.interactable)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                t.isOn = on;
            }
            return true;
        }

        /// <summary>
        /// A toggle set through itself while the game keeps the view it stands in switched off for the page shown (BizMan's header,
        /// with its open switch, on the building's and the real estate's tabs): the caller has seen that the toggle and its own row
        /// are on, so only that view hides it, and its listeners hear the change as they would a click. False when it is not there,
        /// switched off itself or held.
        /// </summary>
        public static bool SetUnder(Toggle t, bool on)
        {
            if (t == null || !t.gameObject.activeSelf || !t.interactable)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                t.isOn = on;
            }
            return true;
        }

        /// <summary>A drop-down's option chosen through itself; false when it has no such option or is held.</summary>
        public static bool Pick(TMP_Dropdown d, int option)
        {
            if (d == null || !d.interactable || option < 0 || option >= d.options.Count)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                d.value = option;
            }
            return true;
        }

        /// <summary>An input field's words set through itself (its change listeners run), then submitted when asked.</summary>
        public static bool Type(TMP_InputField f, string text, bool submit)
        {
            if (f == null || !f.interactable)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                f.text = text ?? string.Empty;
                if (submit)
                {
                    f.onSubmit?.Invoke(f.text);
                    f.onEndEdit?.Invoke(f.text);
                }
            }
            return true;
        }

        /// <summary>A slider moved through itself; false when held.</summary>
        public static bool Slide(Slider s, float value)
        {
            if (s == null || !s.interactable)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                s.value = value;
            }
            return true;
        }

        /// <summary>
        /// The button under a root whose click the game wired in the Inspector to a public method of that name (a box's
        /// ClickConfirm, a panel's OnClose), so the kit presses the button the game itself would; null when none is.
        /// </summary>
        public static Button Calling(Component root, string method)
        {
            if (root == null)
            {
                return null;
            }
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                UnityEvent click = buttons[i].onClick;
                int n = click.GetPersistentEventCount();
                for (int k = 0; k < n; k++)
                {
                    if (click.GetPersistentMethodName(k) == method)
                    {
                        return buttons[i];
                    }
                }
            }
            return null;
        }

        /// <summary>The children of a container that are neither the game's (by the names it gives them) nor the mod's: another mod's.</summary>
        public static int Foreign(Transform container, string[] gameNames, List<Transform> into)
        {
            into.Clear();
            if (container == null)
            {
                return 0;
            }
            for (int i = 0; i < container.childCount; i++)
            {
                Transform child = container.GetChild(i);
                if (!UiHostRules.Own(child.name, gameNames))
                {
                    into.Add(child);
                }
            }
            return into.Count;
        }

    }
}
