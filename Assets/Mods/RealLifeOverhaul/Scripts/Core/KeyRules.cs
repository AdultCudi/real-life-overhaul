using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Keys by the input system's names ("&lt;Keyboard&gt;/f3"), so a key of the mod's (a KeyCode), a binding of the game's
    /// and a key the game reads itself (a KeyCode or a Keyboard property) can be told to be the same. Game-free, so
    /// ContractCheck reads the game with the same rules Core/Keys uses on the options page. What Windows, Steam, the
    /// overlays and other mods take is listed in <see cref="Taken"/>.
    /// COST: string work when a rebind row is drawn; never per frame.
    /// </summary>
    public static class KeyRules
    {
        public const string Keyboard = "<Keyboard>/";
        public const string Mouse = "<Mouse>/";

        /// <summary>A key nobody else may have, and who has it: Windows and the overlays first, then other mods.</summary>
        public sealed class Holder
        {
            public readonly string Path;
            public readonly string Who;

            public Holder(string path, string who)
            {
                Path = path;
                Who = who;
            }
        }

        /// <summary>
        /// Keys taken outside the game and this mod: one that acts alone or changes typing, one that acts with Shift (held
        /// while running), and F4 (Alt+F4). The overlays' Alt combinations are not counted: nothing in play holds Alt.
        /// </summary>
        public static readonly Holder[] Taken =
        {
            new Holder(Keyboard + "leftMeta", "Windows: the Start menu"),
            new Holder(Keyboard + "rightMeta", "Windows: the Start menu"),
            new Holder(Keyboard + "contextMenu", "Windows: the context menu"),
            new Holder(Keyboard + "printScreen", "Windows and Steam: a screenshot"),
            new Holder(Keyboard + "capsLock", "Windows: capitals in every text box after it"),
            new Holder(Keyboard + "numLock", "Windows: the number pad becomes arrows"),
            new Holder(Keyboard + "scrollLock", "Windows: a lock light some programs read"),
            new Holder(Keyboard + "pause", "Windows: Win+Pause, the system page"),
            new Holder(Keyboard + "f4", "Windows: Alt+F4 closes the game"),
            new Holder(Keyboard + "tab", "Windows: Alt+Tab; Steam: Shift+Tab, its overlay"),
            new Holder(Keyboard + "f12", "Steam: a screenshot"),
            new Holder(Keyboard + "backquote", "Discord: Shift+`, its overlay; the game's debug console"),
            new Holder(Keyboard + "escape", "Going Public: closes its windows"),
            new Holder(Keyboard + "enter", "Going Public: sends and confirms"),
            new Holder(Keyboard + "numpadEnter", "Going Public: sends and confirms"),
            new Holder(Keyboard + "w", "Going Public: stands up from a seat"),
            new Holder(Keyboard + "a", "Going Public: stands up from a seat"),
            new Holder(Keyboard + "s", "Going Public: stands up from a seat"),
            new Holder(Keyboard + "d", "Going Public: stands up from a seat"),
            new Holder(Keyboard + "upArrow", "Going Public: its lists"),
            new Holder(Keyboard + "downArrow", "Going Public: its lists"),
            new Holder(Keyboard + "leftArrow", "Going Public: its lists"),
            new Holder(Keyboard + "rightArrow", "Going Public: its lists"),
            new Holder(Keyboard + "v", "Going Public: Ctrl+V pastes a join code; Empire Motors: its view"),
            new Holder(Keyboard + "f6", "Empire Motors: its tools"),
            new Holder(Keyboard + "f7", "Empire Motors: its tools"),
        };

        /// <summary>
        /// The input system's path of a KeyCode by name ("Alpha1"); with <paramref name="either"/>, the path covering both
        /// of a pair ("&lt;Keyboard&gt;/shift"). Null for a key without a path (a joystick button).
        /// </summary>
        public static string PathOf(string keyCode, bool either = false)
        {
            if (string.IsNullOrEmpty(keyCode) || keyCode == "None" || keyCode.StartsWith("Joystick", StringComparison.Ordinal))
            {
                return null;
            }
            switch (keyCode)
            {
                case "Mouse0": return Mouse + "leftButton";
                case "Mouse1": return Mouse + "rightButton";
                case "Mouse2": return Mouse + "middleButton";
                case "Mouse3": return Mouse + "backButton";
                case "Mouse4": return Mouse + "forwardButton";
                case "LeftShift": return Keyboard + (either ? "shift" : "leftShift");
                case "RightShift": return Keyboard + (either ? "shift" : "rightShift");
                case "LeftControl": return Keyboard + (either ? "ctrl" : "leftCtrl");
                case "RightControl": return Keyboard + (either ? "ctrl" : "rightCtrl");
                case "LeftAlt": return Keyboard + (either ? "alt" : "leftAlt");
                case "RightAlt": return Keyboard + (either ? "alt" : "rightAlt");
                case "LeftWindows":
                case "LeftCommand":
                case "LeftApple": return Keyboard + "leftMeta";
                case "RightWindows":
                case "RightCommand":
                case "RightApple": return Keyboard + "rightMeta";
                case "Return": return Keyboard + "enter";
                case "KeypadEnter": return Keyboard + "numpadEnter";
                case "KeypadPlus": return Keyboard + "numpadPlus";
                case "KeypadMinus": return Keyboard + "numpadMinus";
                case "KeypadMultiply": return Keyboard + "numpadMultiply";
                case "KeypadDivide": return Keyboard + "numpadDivide";
                case "KeypadPeriod": return Keyboard + "numpadPeriod";
                case "KeypadEquals": return Keyboard + "numpadEquals";
                case "BackQuote": return Keyboard + "backquote";
                case "Print": return Keyboard + "printScreen";
                case "Menu": return Keyboard + "contextMenu";
                case "Numlock": return Keyboard + "numLock";
                case "ScrollLock": return Keyboard + "scrollLock";
            }
            if (keyCode.Length == 6 && keyCode.StartsWith("Alpha", StringComparison.Ordinal) && char.IsDigit(keyCode[5]))
            {
                return Keyboard + keyCode[5];
            }
            if (keyCode.Length == 7 && keyCode.StartsWith("Keypad", StringComparison.Ordinal) && char.IsDigit(keyCode[6]))
            {
                return Keyboard + "numpad" + keyCode[6];
            }
            if (keyCode.StartsWith("Mouse", StringComparison.Ordinal))
            {
                return Mouse + "button" + keyCode.Substring(5);   // Mouse5 and Mouse6, named by number
            }
            // The rest are the same word with a small first letter.
            return Keyboard + char.ToLowerInvariant(keyCode[0]) + keyCode.Substring(1);
        }

        /// <summary>The path of an input system Key by its name ("F3", "Digit1", "LeftShift"), as a binding writes it.</summary>
        public static string PathOfKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key == "None" || key.StartsWith("OEM", StringComparison.Ordinal) || key.StartsWith("IME", StringComparison.Ordinal))
            {
                return null;
            }
            if (key.Length == 6 && key.StartsWith("Digit", StringComparison.Ordinal) && char.IsDigit(key[5]))
            {
                return Keyboard + key[5];
            }
            return Keyboard + char.ToLowerInvariant(key[0]) + key.Substring(1);
        }

        /// <summary>The path of the key a Keyboard property reads ("get_f3Key", "get_digit1Key"), or null for another property.</summary>
        public static string PathOfProperty(string getter)
        {
            const string Get = "get_";
            const string Tail = "Key";
            if (getter == null || getter.Length <= Get.Length + Tail.Length || !getter.StartsWith(Get, StringComparison.Ordinal)
                || !getter.EndsWith(Tail, StringComparison.Ordinal) || getter == "get_anyKey")
            {
                return null;
            }
            string name = getter.Substring(Get.Length, getter.Length - Get.Length - Tail.Length);
            if (name.Length == 6 && name.StartsWith("digit", StringComparison.Ordinal) && char.IsDigit(name[5]))
            {
                return Keyboard + name[5];
            }
            return Keyboard + name;
        }

        /// <summary>True if a binding's path takes a key: the same path, or one covering both of a pair; case ignored, as the input system does.</summary>
        public static bool Takes(string bound, string key)
        {
            if (bound == null || key == null)
            {
                return false;
            }
            if (string.Equals(bound, key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return Pair(bound, key, "shift") || Pair(bound, key, "ctrl") || Pair(bound, key, "alt") || Pair(bound, key, "meta");
        }

        private static bool Pair(string bound, string key, string both)
        {
            return string.Equals(bound, Keyboard + both, StringComparison.OrdinalIgnoreCase)
                   && (string.Equals(key, Keyboard + "left" + both, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(key, Keyboard + "right" + both, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Who of <see cref="Taken"/> has this key, or null.</summary>
        public static string TakenBy(string key)
        {
            foreach (Holder holder in Taken)
            {
                if (Takes(holder.Path, key))
                {
                    return holder.Who;
                }
            }
            return null;
        }
    }
}
