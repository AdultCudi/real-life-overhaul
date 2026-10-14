using System;
using System.Collections;
using System.Reflection;
using BAModAPI;
using BigAmbitions.Mods;
using BigAmbitions.ModsInternal;
using Localizor;
using Localizor.LanguageChangeEvent;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Everything the mod does on a key. The order is the order on the options page.</summary>
    public enum KeyAction
    {
        CameraToggle,      // third-person cameras on and off
        Interact,          // enter a building or car, a job's or a scene's prompt, the coffee cart
        InteractAlt,       // the same, but only while the game is not offering an interaction of its own
        FreeCursor,        // give the mouse cursor back while mouse look is on
        LookBehind,        // held: look behind the car
        GentleThrottle,    // held: half throttle at most
        PerfProbe,
        PerfBench,
        PhoneDump,
        CharacterReport,
        PhotoMode,          // into and out of photo mode (Leisure/Photo)
    }

    public enum KeyModifier
    {
        None,
        Shift,
        Ctrl,
        Alt
    }

    /// <summary>
    /// The one place that knows which key does what. Every key test in the mod goes through
    /// <see cref="Down"/> or <see cref="Held"/> (one engine call each), every text that names a key through
    /// <see cref="Label"/>. Bindings are saved in PlayerPrefs under the mod's own key format and changed on
    /// the options page ("Controls"): a button per action that waits for the next key, and a reset button.
    /// A key the game itself uses is allowed, and said so beside the button.
    /// </summary>
    public static class Keys
    {
        private sealed class Binding
        {
            public readonly string Id;            // saved as m:<mod>:key_<Id>
            public readonly string LabelKey;
            public readonly KeyCode DefaultKey;
            public readonly KeyModifier DefaultModifier;
            public KeyCode Key;
            public KeyModifier Modifier;
            public KeyModifier Unless;            // another action has this key with that modifier: this one yields while it is held
            public string Label;

            public Binding(string id, string labelKey, KeyCode key, KeyModifier modifier = KeyModifier.None)
            {
                Id = id;
                LabelKey = labelKey;
                DefaultKey = Key = key;
                DefaultModifier = Modifier = modifier;
            }
        }

        // In the order of KeyAction.
        private static readonly Binding[] All =
        {
            new Binding("camera_toggle", "reallife_keys_camera_toggle", KeyCode.V),
            new Binding("interact", "reallife_keys_interact", KeyCode.F),
            new Binding("interact_alt", "reallife_keys_interact_alt", KeyCode.E),
            new Binding("free_cursor", "reallife_keys_free_cursor", KeyCode.LeftAlt),
            new Binding("look_behind", "reallife_keys_look_behind", KeyCode.Mouse1),
            new Binding("gentle_throttle", "reallife_keys_gentle_throttle", KeyCode.G),
            new Binding("perf_probe", "reallife_keys_perf_probe", KeyCode.F7),
            new Binding("perf_bench", "reallife_keys_perf_bench", KeyCode.F7, KeyModifier.Shift),
            new Binding("phone_dump", "reallife_keys_phone_dump", KeyCode.F8),
            new Binding("character_report", "reallife_keys_character_report", KeyCode.F9),
            new Binding("photo_mode", "reallife_keys_photo_mode", KeyCode.F10),
        };

        private static readonly TextLocalizationComponent[] Rows = new TextLocalizationComponent[All.Length];

        public static IModLogger Logger;

        private static string _modId;
        private static bool _capturing;     // a key is being chosen: nothing the mod does on a key may fire on it
        private static bool _hooked;
        private static bool _warned;

        /// <summary>Goes up whenever a binding changes, for anything that caches a <see cref="Label"/>.</summary>
        public static int Version { get; private set; }

        static Keys()
        {
            Refresh();
        }

        /// <summary>True in the frame the action's key goes down (with its modifier, if it has one).</summary>
        public static bool Down(KeyAction action)
        {
            Binding binding = All[(int)action];
            return !_capturing && !Suspended && !Off[(int)action] && Input.GetKeyDown(binding.Key) && Modified(binding);
        }

        /// <summary>True while the action's key is held.</summary>
        public static bool Held(KeyAction action)
        {
            Binding binding = All[(int)action];
            return !_capturing && !Suspended && !Off[(int)action] && Input.GetKey(binding.Key) && Modified(binding);
        }

        // ---------------------------------------------------------------- for Meta (the guide, the diagnostics switch)

        private static readonly bool[] Off = new bool[All.Length];

        /// <summary>While true no action fires: one of the mod's own panels (the guide) has the keyboard.</summary>
        public static bool Suspended;

        /// <summary>How many actions there are, for anything that lists them.</summary>
        public static int Count => All.Length;

        /// <summary>The en.json key of the action's name, as on the options page.</summary>
        public static string LabelKey(KeyAction action)
        {
            return All[(int)action].LabelKey;
        }

        /// <summary>Is this the en.json key of one of the actions: the label of a row that changes a key.</summary>
        public static bool IsActionLabel(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                for (int i = 0; i < All.Length; i++)
                {
                    if (All[i].LabelKey == key)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>The option id an action's binding is saved under ("key_interact"), for the settings file.</summary>
        public static string PrefsId(KeyAction action)
        {
            return "key_" + All[(int)action].Id;
        }

        /// <summary>Switches one action off or on again without touching its binding (the developer diagnostics).</summary>
        public static void SetOff(KeyAction action, bool off)
        {
            Off[(int)action] = off;
        }

        /// <summary>The fixed keys of the mod's own panels, tested only while one is open: Esc closes.</summary>
        public static bool PanelClose()
        {
            return Input.GetKeyDown(KeyCode.Escape);
        }

        /// <summary>Lines to scroll this frame: the arrow keys one, Page Up and Page Down thirty; plus or minus a thousand for Home and End.</summary>
        public static int PanelScroll()
        {
            if (!Input.anyKey)
            {
                return 0;
            }
            if (Input.GetKeyDown(KeyCode.Home))
            {
                return -1000;
            }
            if (Input.GetKeyDown(KeyCode.End))
            {
                return 1000;
            }
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                return -30;
            }
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                return 30;
            }
            return Input.GetKey(KeyCode.UpArrow) ? -1 : Input.GetKey(KeyCode.DownArrow) ? 1 : 0;
        }

        /// <summary>What was typed this frame (the guide's search), or null. Only read while a panel is open.</summary>
        public static string PanelTyped()
        {
            return Input.anyKeyDown ? Input.inputString : null;
        }

        /// <summary>What to call the action's key in a prompt: "F", "Shift+F7", "Right mouse".</summary>
        public static string Label(KeyAction action)
        {
            return All[(int)action].Label;
        }

        /// <summary>The key itself, for the one place that needs to know it is a mouse button.</summary>
        public static KeyCode Key(KeyAction action)
        {
            return All[(int)action].Key;
        }

        /// <summary>The modifier the action's key is bound with (None for the key alone).</summary>
        public static KeyModifier Modifier(KeyAction action)
        {
            return All[(int)action].Modifier;
        }

        private static bool Modified(Binding binding)
        {
            return binding.Modifier != KeyModifier.None
                ? ModifierHeld(binding.Modifier)
                : binding.Unless == KeyModifier.None || !ModifierHeld(binding.Unless);
        }

        private static bool ModifierHeld(KeyModifier modifier)
        {
            switch (modifier)
            {
                case KeyModifier.Shift: return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                case KeyModifier.Ctrl: return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                case KeyModifier.Alt: return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                default: return true;
            }
        }

        // ---------------------------------------------------------------- saved bindings

        /// <summary>Reads the saved bindings. Called once at game start, with the other options.</summary>
        public static void Load(string modId)
        {
            _modId = modId;
            foreach (Binding binding in All)
            {
                int saved = UnityEngine.PlayerPrefs.GetInt(PrefsKey(binding), -1);
                int key = saved & 0xFFFF;
                int modifier = saved >> 16;
                if (saved > 0 && Enum.IsDefined(typeof(KeyCode), key) && modifier >= 0 && modifier <= (int)KeyModifier.Alt)
                {
                    binding.Key = (KeyCode)key;
                    binding.Modifier = (KeyModifier)modifier;
                }
                else
                {
                    binding.Key = binding.DefaultKey;
                    binding.Modifier = binding.DefaultModifier;
                }
            }
            Refresh();
        }

        private static void Bind(KeyAction action, KeyCode key, KeyModifier modifier)
        {
            Binding binding = All[(int)action];
            binding.Key = key;
            binding.Modifier = modifier;
            if (_modId != null)
            {
                UnityEngine.PlayerPrefs.SetInt(PrefsKey(binding), (int)key | ((int)modifier << 16));
            }
            Refresh();
            Logger?.Info("Keys: " + binding.Id + " is now " + binding.Label + ".");
        }

        /// <summary>Every action back on the key it shipped with.</summary>
        public static void ResetAll()
        {
            foreach (Binding binding in All)
            {
                binding.Key = binding.DefaultKey;
                binding.Modifier = binding.DefaultModifier;
                if (_modId != null)
                {
                    UnityEngine.PlayerPrefs.DeleteKey(PrefsKey(binding));
                }
            }
            Refresh();
            Logger?.Info("Keys: all back to their defaults.");
        }

        // Same key format the game uses when it saves mod options.
        private static string PrefsKey(Binding binding) => "m:" + _modId + ":key_" + binding.Id;

        // After any change: the labels, and which plain binding must yield to a modified one on the same key.
        private static void Refresh()
        {
            foreach (Binding binding in All)
            {
                binding.Label = (binding.Modifier != KeyModifier.None ? binding.Modifier + "+" : string.Empty) + KeyName(binding.Key);
                binding.Unless = KeyModifier.None;
                if (binding.Modifier == KeyModifier.None)
                {
                    foreach (Binding other in All)
                    {
                        if (other != binding && other.Key == binding.Key && other.Modifier != KeyModifier.None)
                        {
                            binding.Unless = other.Modifier;
                            break;
                        }
                    }
                }
            }
            Version++;
            for (int i = 0; i < All.Length; i++)
            {
                ShowRow((KeyAction)i, false);
            }
            EmpireView.KeysChanged();   // the camera key moved off V: Empire Motors' view on V is switched on again (Camera/EmpireView)
        }

        private static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.None: return "-";
                case KeyCode.Mouse0: return "Left mouse";
                case KeyCode.Mouse1: return "Right mouse";
                case KeyCode.Mouse2: return "Middle mouse";
                case KeyCode.LeftAlt: return "Left Alt";
                case KeyCode.RightAlt: return "Right Alt";
                case KeyCode.LeftShift: return "Left Shift";
                case KeyCode.RightShift: return "Right Shift";
                case KeyCode.LeftControl: return "Left Ctrl";
                case KeyCode.RightControl: return "Right Ctrl";
                case KeyCode.Return: return "Enter";
                case KeyCode.KeypadEnter: return "Num Enter";
                case KeyCode.BackQuote: return "`";
                case KeyCode.CapsLock: return "Caps Lock";
                case KeyCode.PageUp: return "Page Up";
                case KeyCode.PageDown: return "Page Down";
            }
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            {
                return ((int)(key - KeyCode.Alpha0)).ToString();
            }
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
            {
                return "Num " + (int)(key - KeyCode.Keypad0);
            }
            if (key >= KeyCode.Mouse3 && key <= KeyCode.Mouse6)
            {
                return "Mouse " + (key - KeyCode.Mouse0 + 1);
            }
            return key.ToString();
        }

        // ---------------------------------------------------------------- the options page

        /// <summary>The "Controls" group: the reset button, then one rebind button per action.</summary>
        public static ModOptions AddControls(this ModOptions options)
        {
            if (!_hooked)
            {
                _hooked = true;
                OptionsService.OnReset += ResetAll;   // the game's own "reset all options"
            }
            options.AddHeader("reallife_keys_header");
            // A plain button first: the rebind buttons below are copies of it.
            options.AddButton("reallife_keys_reset", ResetAll);
            for (int i = 0; i < All.Length; i++)
            {
                options.AddCustom(new RebindOption((KeyAction)i, All[i].LabelKey));
            }
            return options.AddSplitter();
        }

        /// <summary>The mod is being unloaded.</summary>
        public static void Dispose()
        {
            if (_hooked)
            {
                _hooked = false;
                OptionsService.OnReset -= ResetAll;
            }
            KeyCapture.Cancel();
        }

        /// <summary>
        /// One action's row. The options builder has no key control, so the row is a copy of one of the
        /// page's own buttons: its label is the action, followed by the key ("Free the cursor:  Left Alt").
        /// </summary>
        private sealed class RebindOption : ModOption
        {
            private readonly KeyAction _action;

            public RebindOption(KeyAction action, string labelKey) : base(null, labelKey)
            {
                _action = action;
            }

            public override void SpawnUi(Transform parent, string modId)
            {
                var template = parent != null ? parent.GetComponentInChildren<ModOptionsButtonControl>(true) : null;
                if (template == null)
                {
                    if (!_warned)
                    {
                        _warned = true;
                        Logger?.Warn("Keys: the options page has no button to copy, so the keys cannot be changed there (they keep working).");
                    }
                    return;
                }
                GameObject row = UnityEngine.Object.Instantiate(template.gameObject, parent);
                row.SetActive(true);
                KeyAction action = _action;
                row.GetComponent<ModOptionsButtonControl>().Initialize(new ButtonOption(Label, () => KeyCapture.Begin(action)));
                Rows[(int)action] = row.GetComponentInChildren<TextLocalizationComponent>(true);
                ShowRow(action, false);
            }
        }

        // Writes the key (or "press a key") after the action's name on its button, if the page is open.
        private static void ShowRow(KeyAction action, bool waiting)
        {
            TextLocalizationComponent row = Rows[(int)action];
            if (row == null)
            {
                return;
            }
            Binding binding = All[(int)action];
            string text;
            if (waiting)
            {
                text = "reallife_keys_press".GetLocalization();
            }
            else
            {
                string used = GameUse(binding.Key);
                text = used == null ? binding.Label : binding.Label + "   (" + "reallife_keys_conflict".GetLocalization() + " " + used + ")";
                string other = EmpireMotors.KeyUse(binding.Key, binding.Modifier);   // another author's mod that acts on the key by itself (Core/EmpireMotors)
                if (other != null)
                {
                    text += "   (" + other.GetLocalization() + ")";
                }
            }
            row.Suffix = ":  " + text;
        }

        // ---------------------------------------------------------------- waiting for a key

        /// <summary>Exists only between a click on a rebind button and the next key (or eight seconds).</summary>
        private sealed class KeyCapture : MonoBehaviour
        {
            private static readonly FeatureGuard FrameGuard = Guard.For("keys", "Keys.KeyCapture", typeof(Keys.KeyCapture));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

            private static readonly KeyCode[] Codes = (KeyCode[])Enum.GetValues(typeof(KeyCode));
            private static KeyCapture _live;

            private KeyAction _action;
            private float _until;
            private KeyCode _modifierDown;   // a modifier key went down by itself: bound alone if it comes up alone

            public static void Begin(KeyAction action)
            {
                if (_live == null)
                {
                    var go = new GameObject("RealLifeOverhaul Key Capture");
                    DontDestroyOnLoad(go);
                    _live = go.AddComponent<KeyCapture>();
                }
                else
                {
                    ShowRow(_live._action, false);
                }
                _live._action = action;
                _live._until = Time.unscaledTime + 8f;
                _live._modifierDown = KeyCode.None;
                _capturing = true;
                ShowRow(action, true);
            }

            public static void Cancel()
            {
                if (_live != null)
                {
                    _live.End();
                }
            }

            private static readonly int HotUpdate = HotSpots.Slot("Core: KeyCapture.Update");
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
                if (Time.unscaledTime > _until || Input.GetKeyDown(KeyCode.Escape))
                {
                    End();
                    return;
                }
                if (_modifierDown != KeyCode.None && Input.GetKeyUp(_modifierDown))
                {
                    Bind(_action, _modifierDown, KeyModifier.None);
                    End();
                    return;
                }
                if (!Input.anyKeyDown)
                {
                    return;
                }
                foreach (KeyCode code in Codes)
                {
                    // The left button works the page itself; pads are left to the game.
                    if (code == KeyCode.None || code == KeyCode.Mouse0 || code >= KeyCode.JoystickButton0 || !Input.GetKeyDown(code))
                    {
                        continue;
                    }
                    if (ModifierOf(code) != KeyModifier.None)
                    {
                        _modifierDown = code;
                        continue;
                    }
                    // Whatever modifier is down with it, whichever of the two went down first.
                    KeyModifier with = KeyModifier.None;
                    for (KeyModifier modifier = KeyModifier.Shift; modifier <= KeyModifier.Alt && with == KeyModifier.None; modifier++)
                    {
                        if (ModifierHeld(modifier))
                        {
                            with = modifier;
                        }
                    }
                    Bind(_action, code, with);
                    End();
                    return;
                }
            }

            private static KeyModifier ModifierOf(KeyCode code)
            {
                switch (code)
                {
                    case KeyCode.LeftShift:
                    case KeyCode.RightShift: return KeyModifier.Shift;
                    case KeyCode.LeftControl:
                    case KeyCode.RightControl: return KeyModifier.Ctrl;
                    case KeyCode.LeftAlt:
                    case KeyCode.RightAlt: return KeyModifier.Alt;
                    default: return KeyModifier.None;
                }
            }

            private void End()
            {
                _capturing = false;
                _live = null;
                ShowRow(_action, false);
                Destroy(gameObject);
            }

            private void OnDestroy()
            {
                if (_live == this)
                {
                    _live = null;
                    _capturing = false;
                }
            }
        }

        // ---------------------------------------------------------------- the game's own keys

        // The game's defaults, for when its live bindings cannot be read (before its input is set up).
        private static readonly KeyCode[] KnownKeys =
        {
            KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.F, KeyCode.P, KeyCode.O, KeyCode.Q, KeyCode.E, KeyCode.R, KeyCode.Escape, KeyCode.Return, KeyCode.KeypadEnter,
            KeyCode.Tab, KeyCode.F12, KeyCode.LeftShift, KeyCode.T, KeyCode.Backspace, KeyCode.I, KeyCode.N, KeyCode.Space,
            KeyCode.Mouse2, KeyCode.Mouse1, KeyCode.F1, KeyCode.F2, KeyCode.F5, KeyCode.M, KeyCode.B, KeyCode.C,
            KeyCode.RightBracket, KeyCode.LeftBracket, KeyCode.H, KeyCode.LeftControl
        };

        private static readonly string[] KnownUses =
        {
            "Move", "Move", "Move", "Move", "Move", "Move", "Move", "Move",
            "Interact", "SecondaryInteract", "SpecialInteract", "RotateLeft", "RotateRight", "RotateItem", "Cancel", "Confirm", "Confirm",
            "NextOption", "Menu", "ToggleRunning", "SkipSong", "Sell", "Sleep", "OpenNotifications", "Pause",
            "AutoRun", "RightClick", "OpenHelp", "OpenBugReport", "QuickSave", "OpenMap", "OpenBizman", "ReverseCarCam",
            "VehicleRightBlinker", "VehicleLeftBlinker", "VehicleHorn", "Handbrake"
        };

        /// <summary>The game action that is on this key (as the player has it bound now), or null.</summary>
        private static string GameUse(KeyCode key)
        {
            if (key == KeyCode.None)
            {
                return null;
            }
            try
            {
                string path = InputPath(key, false);
                string either = InputPath(key, true);
                bool read = false;
                object player = GameContract.PlayerInput.Field?.GetValue(null);   // (resolved once: Core/GameContract)
                object vehicle = GameContract.VehicleInput.Field?.GetValue(null);
                string found = Bound(player, "Player", path, either, ref read) ?? Bound(vehicle, null, path, either, ref read);
                if (read)
                {
                    return found;
                }
            }
            catch (Exception)
            {
                // The input layer is not what it was: the list below still warns about the game's defaults.
            }
            int known = Array.IndexOf(KnownKeys, key);
            return known >= 0 ? KnownUses[known] : null;
        }

        // Looks through the bindings of the game's generated input wrapper without naming the input system's types,
        // which this assembly does not reference. Only run when a rebind button is drawn.
        private static string Bound(object wrapper, string onlyMap, string path, string either, ref bool read)
        {
            object asset = wrapper?.GetType().GetProperty("asset")?.GetValue(wrapper);
            if (!(asset?.GetType().GetProperty("actionMaps")?.GetValue(asset) is IEnumerable maps))
            {
                return null;
            }
            foreach (object map in maps)
            {
                Type mapType = map.GetType();
                if (onlyMap != null && (string)mapType.GetProperty("name")?.GetValue(map) != onlyMap)
                {
                    continue;
                }
                if (!(mapType.GetProperty("bindings")?.GetValue(map) is IEnumerable bindings))
                {
                    continue;
                }
                foreach (object binding in bindings)
                {
                    Type type = binding.GetType();
                    string bound = type.GetProperty("effectivePath")?.GetValue(binding) as string;
                    if (bound == null)
                    {
                        continue;
                    }
                    read = true;
                    if (string.Equals(bound, path, StringComparison.OrdinalIgnoreCase) || string.Equals(bound, either, StringComparison.OrdinalIgnoreCase))
                    {
                        return type.GetProperty("action")?.GetValue(binding) as string ?? "?";
                    }
                }
            }
            return null;
        }

        // The input system's name for a key: "<Keyboard>/f", "<Mouse>/rightButton". "either" gives the name that
        // covers both of a pair ("<Keyboard>/shift").
        private static string InputPath(KeyCode key, bool either)
        {
            switch (key)
            {
                case KeyCode.Mouse0: return "<Mouse>/leftButton";
                case KeyCode.Mouse1: return "<Mouse>/rightButton";
                case KeyCode.Mouse2: return "<Mouse>/middleButton";
                case KeyCode.Mouse3: return "<Mouse>/backButton";
                case KeyCode.Mouse4: return "<Mouse>/forwardButton";
                case KeyCode.LeftShift: return either ? "<Keyboard>/shift" : "<Keyboard>/leftShift";
                case KeyCode.RightShift: return either ? "<Keyboard>/shift" : "<Keyboard>/rightShift";
                case KeyCode.LeftControl: return either ? "<Keyboard>/ctrl" : "<Keyboard>/leftCtrl";
                case KeyCode.RightControl: return either ? "<Keyboard>/ctrl" : "<Keyboard>/rightCtrl";
                case KeyCode.LeftAlt: return either ? "<Keyboard>/alt" : "<Keyboard>/leftAlt";
                case KeyCode.RightAlt: return either ? "<Keyboard>/alt" : "<Keyboard>/rightAlt";
                case KeyCode.Return: return "<Keyboard>/enter";
                case KeyCode.KeypadEnter: return "<Keyboard>/numpadEnter";
            }
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            {
                return "<Keyboard>/" + (int)(key - KeyCode.Alpha0);
            }
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
            {
                return "<Keyboard>/numpad" + (int)(key - KeyCode.Keypad0);
            }
            // Letters, function keys, arrows, brackets and the rest are the same word with a small first letter.
            string name = key.ToString();
            return "<Keyboard>/" + char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }

    /// <summary>
    /// The fixed keys of photo mode (Leisure/Photo/PhotoMode), which works like a tool with a layout of its own
    /// rather than a set of bindings: only getting into and out of it is a rebindable action
    /// (<see cref="KeyAction.PhotoMode"/>). They are kept here so that no KeyCode lives outside this file, and are
    /// chosen from keys the game binds to nothing that acts while it is paused (not Space, which is its pause; not
    /// Q and E, which turn its camera; not F, its interaction). One engine call each, nothing allocated.
    /// </summary>
    public static class PhotoKeys
    {
        public static float Forward => Axis(KeyCode.W, KeyCode.S);
        public static float Sideways => Axis(KeyCode.D, KeyCode.A);
        public static float Rise => Axis(KeyCode.X, KeyCode.Z);
        public static float Roll => Axis(KeyCode.L, KeyCode.J);
        public static float Focus => Axis(KeyCode.U, KeyCode.Y);
        public static float Exposure => Axis(KeyCode.Period, KeyCode.Comma);
        public static bool Fast => Input.GetKey(KeyCode.LeftShift);
        public static bool Slow => Input.GetKey(KeyCode.LeftControl);
        public static bool Shoot => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        public static bool NextFilter => Input.GetKeyDown(KeyCode.K);
        public static bool ToggleBlur => Input.GetKeyDown(KeyCode.G);
        public static bool TogglePlayer => Input.GetKeyDown(KeyCode.Alpha1);
        public static bool ToggleHelp => Input.GetKeyDown(KeyCode.Alpha2);
        public static bool ResetView => Input.GetKeyDown(KeyCode.Alpha3);
        public static bool Selfie => Input.GetKeyDown(KeyCode.Alpha4);
        public static bool NextPose => Input.GetKeyDown(KeyCode.Alpha5);

        private static float Axis(KeyCode plus, KeyCode minus)
        {
            return (Input.GetKey(plus) ? 1f : 0f) - (Input.GetKey(minus) ? 1f : 0f);
        }
    }
}
