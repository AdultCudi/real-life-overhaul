using System;
using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.SaveSystem;
using Helpers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Buying and what the player holds on the RLO interface (Options, the buying section): the checkout, buying a vehicle,
    /// manage cargo and the clothes shop, the hairdresser and the surgeon through the host (UiGameHost) like any game
    /// screen; the item panel through a keeper of its own (ItemPanelKit), since Going Public copies the game's whole panel
    /// and nothing may be added to it. Each screen has its own switch, and each reads the game's panel and presses the
    /// game's own buttons, so every rule stays the game's and an NPC's purchase never passes through here. Cost: nothing
    /// while none is open; while one is, its host's input and a look at the game's panel four times a second.
    /// </summary>
    public static class BuyKits
    {
        public const string CheckoutId = "checkout", VehicleId = "vehicle_purchase", ItemPanelId = "item_panel", CargoId = "manage_cargo",
            WardrobeId = "wardrobe";

        // The item panel's readings: a mile in kilometres (the fuel over a distance is FuelUseRules), and how near a lane the car
        // must be for its limit to hold.
        internal const float MilesPerKm = 0.621371f, OnTheRoad = 8f;

        private static readonly UiGameHost Checkout = new UiGameHost(new CheckoutGame());
        private static readonly UiGameHost Vehicle = new UiGameHost(new VehicleBuyGame());
        private static readonly UiGameHost Cargo = new UiGameHost(new CargoGame());
        // The surgeon is not here: its screen has the face sliders, the eye and skin colours and the zoom, which the kit does not draw, so the game's own
        // screen stays whole while it is open.
        private static readonly UiGameHost[] Wardrobes =
        {
            new UiGameHost(new WardrobeGame(WardrobeKind.Clothes)), new UiGameHost(new WardrobeGame(WardrobeKind.Hair))
        };

        /// <summary>Once a second while the interface is on: each screen's host put on while its switch is on, the game's own while off.</summary>
        public static void Install()
        {
            Switch(Checkout);
            Switch(Vehicle);
            Switch(Cargo);
            for (int i = 0; i < Wardrobes.Length; i++)
            {
                Switch(Wardrobes[i]);
            }
            ItemPanelKit.Install();
        }

        /// <summary>Every buying screen the game's own again, exactly as found.</summary>
        public static void Remove(string why)
        {
            Checkout.Remove(why);
            Vehicle.Remove(why);
            Cargo.Remove(why);
            for (int i = 0; i < Wardrobes.Length; i++)
            {
                Wardrobes[i].Remove(why);
            }
            ItemPanelKit.Remove(why);
        }

        /// <summary>A new city: every screen is looked for again, the pictures of the last one let go.</summary>
        public static void Rearm()
        {
            Checkout.Rearm();
            Vehicle.Rearm();
            Cargo.Rearm();
            for (int i = 0; i < Wardrobes.Length; i++)
            {
                Wardrobes[i].Rearm();
            }
            ItemPanelKit.Rearm();
            BuyPictures.Clear();
        }

        /// <summary>The screens' states for the interface's line at load.</summary>
        public static string States() => "the checkout (" + Checkout.State + "), buying a vehicle (" + Vehicle.State + "), manage cargo (" + Cargo.State
                                         + "), the clothes shop (" + Wardrobes[0].State + "), the hairdresser (" + Wardrobes[1].State + "), the surgeon (the game's own), the item panel ("
                                         + ItemPanelKit.State + ")";

        /// <summary>True while the kit draws the checkout, for the tests.</summary>
        internal static bool CheckoutShowing => Checkout.Showing;

        internal static bool VehicleShowing => Vehicle.Showing;

        internal static bool CargoShowing => Cargo.Showing;

        /// <summary>
        /// The order of manage cargo's canvas while the kit draws it, int.MinValue otherwise: the item panel stands one over
        /// it (ItemPanelKit.Lift). Asked with the panel's readings, four times a second.
        /// </summary>
        internal static int CargoOrder
        {
            get
            {
                RectTransform root = Cargo.Showing ? Cargo.Root : null;
                Canvas canvas = root != null ? root.GetComponent<Canvas>() : null;
                return canvas != null ? canvas.sortingOrder : int.MinValue;
            }
        }

        internal static bool WardrobeShowing => Wardrobes[0].Showing || Wardrobes[1].Showing || Wardrobes[2].Showing;

        private static void Switch(UiGameHost host)
        {
            if (UiScreenSwitches.On(host.Id))
            {
                host.Install();
            }
            else
            {
                host.Remove("its switch is off");
            }
        }

        // ------------------------------------------------------------------------------------------------- reading the game's panels

        private static readonly List<TMP_Text> Texts = new List<TMP_Text>(8);

        /// <summary>A field of the game's read from its contract line; null when this version lacks it.</summary>
        internal static T Get<T>(GameMember member, object owner) where T : class => owner != null && member.Field != null ? member.Field.GetValue(owner) as T : null;

        /// <summary>A value field of the game's read from its contract line, or the fallback when this version lacks it.</summary>
        internal static T Value<T>(GameMember member, object owner, T fallback) where T : struct
        {
            object v = owner != null && member.Field != null ? member.Field.GetValue(owner) : null;
            return v is T t ? t : fallback;
        }

        /// <summary>The words a piece shows, its rich text and the key the game adds to a button's words taken off; null when not shown.</summary>
        internal static string Words(Component c) => c != null && c.gameObject.activeInHierarchy ? NoKey(UiWidgets.Label(c)) : null;

        /// <summary>The words at a path under a root; null when not there or not shown.</summary>
        internal static string WordsAt(Transform root, string path)
        {
            Transform t = root != null ? root.Find(path) : null;
            return t != null ? Words(t) : null;
        }

        /// <summary>Every text a piece shows, in order, into a list the caller keeps.</summary>
        internal static int AllWords(Transform root, List<string> into)
        {
            into.Clear();
            if (root == null)
            {
                return 0;
            }
            Texts.Clear();
            root.GetComponentsInChildren(false, Texts);
            for (int i = 0; i < Texts.Count; i++)
            {
                string w = UiWidgets.Text(Texts[i]);
                if (!string.IsNullOrEmpty(w))
                {
                    into.Add(w.Trim());
                }
            }
            return into.Count;
        }

        // The game writes its key after a button's words ("Purchase [F]"): the kit draws the key as a cap of its own.
        internal static string NoKey(string words)
        {
            if (string.IsNullOrEmpty(words) || words[words.Length - 1] != ']')
            {
                return words;
            }
            int at = words.LastIndexOf(" [", StringComparison.Ordinal);
            return at > 0 ? words.Substring(0, at) : words;
        }

        /// <summary>The street of an address as the game writes it ("12 Main Street" gives "Main Street"); the words themselves without a number.</summary>
        internal static string StreetOf(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                return null;
            }
            int i = 0;
            while (i < address.Length && char.IsDigit(address[i]))
            {
                i++;
            }
            return i > 0 && i + 1 < address.Length && address[i] == ' ' ? address.Substring(i + 1) : address;
        }

        /// <summary>The copies the game made of a template, in order, shown: the template's own parent's children but the template.</summary>
        internal static int Copies(Transform template, List<Transform> into)
        {
            into.Clear();
            Transform parent = template != null ? template.parent : null;
            if (parent == null)
            {
                return 0;
            }
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c != template && c.gameObject.activeSelf)
                {
                    into.Add(c);
                }
            }
            return into.Count;
        }

        /// <summary>A share from the game's percent ("64%", "64 %"), or -1 when the words hold none.</summary>
        internal static float Percent(string words)
        {
            if (string.IsNullOrEmpty(words))
            {
                return -1f;
            }
            int end = words.IndexOf('%');
            if (end <= 0)
            {
                return -1f;
            }
            int start = end - 1;
            while (start > 0 && (char.IsDigit(words[start - 1]) || words[start - 1] == '.' || words[start - 1] == ','))
            {
                start--;
            }
            string n = words.Substring(start, end - start).Trim().Replace(',', '.');
            return float.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? Mathf.Clamp01(v / 100f) : -1f;
        }

        /// <summary>A reading's tone by how full it is: low in red, getting low in amber, else the words' own.</summary>
        internal static UiRole Tone(float share) => share < 0f ? UiRole.None : share < 0.2f ? UiRole.Danger : share < 0.4f ? UiRole.Warn : UiRole.None;

        /// <summary>The colour as the kit names a swatch.</summary>
        internal static int Rgb(Color c) => (Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255) << 16) | (Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255) << 8)
                                            | Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);

        /// <summary>The prompts every buying screen shows on its bar: move and select.</summary>
        internal static UiPrompt[] Prompts() => new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") },
            new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") },
            new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") }
        };

        /// <summary>A game key of the player's own binding, as the game writes it after its buttons' words.</summary>
        internal static UiGameKey Key(PlayerAction action)
        {
            try
            {
                return new UiGameKey { Name = BigAmbitions.InputSystem.InputActionHelper.GetDisplayName(action) };
            }
            catch (Exception)
            {
                return default(UiGameKey);   // a binding the game has not loaded yet: the cap is left off
            }
        }
    }

    /// <summary>
    /// The game's own pictures for the buying screens, by keys the kit's layouts carry: an item's icon as the game draws it
    /// (asked once an item, then remembered), a sprite of the game's panel (a category's, a piece's), and a square of one
    /// colour for a swatch the game draws as a tint. Let go with each city. Cost: a dictionary look a picture drawn; an
    /// item's icon is loaded the first time only, as the game's own panels load it.
    /// </summary>
    internal static class BuyPictures
    {
        private const string Key = "buy:";
        private const int Most = 768;   // sprites remembered a city: the clothes screen's pieces are the most
        private static readonly List<Sprite> Sprites = new List<Sprite>(64);
        private static readonly Dictionary<Sprite, string> Keys = new Dictionary<Sprite, string>(64);
        private static readonly Dictionary<string, string> ItemKeys = new Dictionary<string, string>(64, StringComparer.Ordinal);
        private static readonly Dictionary<int, Sprite> Squares = new Dictionary<int, Sprite>(32);
        private static bool _on;

        /// <summary>The key a sprite of the game's is drawn by; null for none.</summary>
        internal static string Of(Sprite sprite)
        {
            if (sprite == null)
            {
                return null;
            }
            On();
            if (Keys.TryGetValue(sprite, out string key))
            {
                return key;
            }
            if (Sprites.Count >= Most)
            {
                return null;
            }
            key = Key + Sprites.Count.ToString(CultureInfo.InvariantCulture);
            Sprites.Add(sprite);
            Keys[sprite] = key;
            return key;
        }

        /// <summary>The key of an item's icon as the game draws it; null when the game has none for it.</summary>
        internal static string Item(string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                return null;
            }
            if (ItemKeys.TryGetValue(itemName, out string key))
            {
                return key;
            }
            Sprite icon = null;
            try
            {
                icon = AddressableResolver.IsValidAddressableKey(PlayerHelper.GetItemNameIconPath(itemName)) ? ItemHelper.GetIcon(itemName) : null;
            }
            catch (Exception)
            {
                icon = null;   // an item with no icon of its own: its glyph stands in
            }
            key = Of(icon);
            ItemKeys[itemName] = key;
            return key;
        }

        /// <summary>A sprite's width over its height, so its tile letterboxes it.</summary>
        internal static float Aspect(Sprite sprite) => sprite != null && sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;

        /// <summary>The pictures of the last city let go.</summary>
        internal static void Clear()
        {
            Sprites.Clear();
            Keys.Clear();
            ItemKeys.Clear();
            foreach (Sprite square in Squares.Values)
            {
                if (square != null)
                {
                    UnityEngine.Object.Destroy(square.texture);
                    UnityEngine.Object.Destroy(square);
                }
            }
            Squares.Clear();
        }

        // Its own keys answered among the kit's pictures, added once a session; any other key is another part's.
        private static void On()
        {
            if (_on)
            {
                return;
            }
            _on = true;
            UiPictures.Answer(Find);
        }

        private static Sprite Find(string key)
        {
            if (key == null)
            {
                return null;
            }
            if (key.StartsWith(Key, StringComparison.Ordinal))
            {
                return int.TryParse(key.Substring(Key.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) && i >= 0 && i < Sprites.Count ? Sprites[i] : null;
            }
            int rgb = UiDraw.SwatchColour(key);
            return rgb >= 0 ? Square(rgb) : null;
        }

        // One square of a colour, made the first time it is asked for.
        private static Sprite Square(int rgb)
        {
            if (Squares.TryGetValue(rgb, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "RealLife swatch", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            var c = new Color32((byte)((rgb >> 16) & 255), (byte)((rgb >> 8) & 255), (byte)(rgb & 255), 255);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            sprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Squares[rgb] = sprite;
            On();
            return sprite;
        }
    }
}
