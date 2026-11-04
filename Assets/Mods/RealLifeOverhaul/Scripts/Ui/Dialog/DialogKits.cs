using System;
using System.Collections.Generic;
using UI;
using UI.Dialog;
using UI.Monologues;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's conversations drawn on the RLO interface, through the host every game screen uses (UiGameHost): the
    /// dialogue panel of the HUD (a bank's loan, a clerk at a desk, a talk in the street, and every form the game asks
    /// in one) as a conversation docked at the right of the screen over the live scene, the spoken lines (Uncle Fred's,
    /// a rival's) as a notice card at the top centre, and the mod's scenes (a hearing, a fame or office scene, an
    /// interview, a pitch) as a decision card or a conversation (SceneKit). The game's objects stay alive under the kit
    /// with only their own layout unseen and untouchable, given back exactly as found; every press is the game's own
    /// button or widget. Each of the three has its switch in Mod settings (ui_screen_dialog, ui_screen_monologue,
    /// ui_screen_scenes). Cost: what the host costs: nothing while nothing is said; while a line shows, a look at its
    /// state four times a second.
    /// </summary>
    public static class DialogKits
    {
        public const string DialogId = "dialog", MonologueId = "monologue", ScenesId = "scenes";

        private static readonly UiGameHost Talk = new UiGameHost(new DialogGameScreen());
        private static readonly UiGameHost Spoken = new UiGameHost(new MonologueGameScreen());
        private static readonly Dictionary<string, Sprite> Pictures = new Dictionary<string, Sprite>(16);   // the game's own sprites, by the keys given them
        private static readonly Dictionary<string, Sprite> Swatches = new Dictionary<string, Sprite>(16);   // the colours' squares made here, dropped with the city
        private static bool _picturesOn;

        /// <summary>Where the kit stands, for the interface's line at load.</summary>
        public static string State => "the dialogue (" + Talk.State + "), the spoken lines (" + Spoken.State + "), the scenes (" + SceneKit.State + ")";

        /// <summary>True while the kit draws the dialogue panel.</summary>
        public static bool Showing => Talk.Showing;

        /// <summary>The kit's screens while they are made, for the tests; null before.</summary>
        internal static DialogScreen Screen => Talk.Screen as DialogScreen;

        internal static MonologueScreen SpokenScreen => Spoken.Screen as MonologueScreen;

        /// <summary>Puts the kit over the game's panels while their switches are on, and gives the game's own back while off; asked once a second.</summary>
        public static void Install()
        {
            PicturesOn();
            if (UiScreenSwitches.On(DialogId))
            {
                Talk.Install();
            }
            else
            {
                Talk.Remove("its switch is off");
            }
            if (UiScreenSwitches.On(MonologueId))
            {
                Spoken.Install();
            }
            else
            {
                Spoken.Remove("its switch is off");
            }
            SceneKit.Install(UiScreenSwitches.On(ScenesId));
        }

        /// <summary>A new city: a panel that never looked as expected in the last one is looked at again, and the last city's pictures let go.</summary>
        public static void Rearm()
        {
            Talk.Rearm();
            Spoken.Rearm();
            SceneKit.Rearm();
            foreach (Sprite made in Swatches.Values)
            {
                CodeAssets.DropSprite(made);
            }
            Swatches.Clear();
            Pictures.Clear();   // the game's own: only forgotten
        }

        /// <summary>The game's panels as they were: the kit gone, their layouts seen and touchable again.</summary>
        public static void Remove(string why)
        {
            Talk.Remove(why);
            Spoken.Remove(why);
            SceneKit.Remove(why);
        }

        /// <summary>The game's own dialogue panel until it closes: another mod's piece in it, which only the game's view shows.</summary>
        internal static void Classic() => Talk.Classic();

        /// <summary>A line the game must answer in its own panel (a casino's game): the kit steps aside until <paramref name="over"/>.</summary>
        internal static void StepAside(Func<bool> over) => Talk.StepAside(over);

        /// <summary>A sprite the game shows (the spoken line's face) by a key of its own, kept while the session lasts.</summary>
        internal static string PictureOf(Sprite sprite)
        {
            if (sprite == null)
            {
                return null;
            }
            string key = DialogRules.PictureKey + sprite.GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!Pictures.ContainsKey(key))
            {
                Pictures[key] = sprite;
            }
            return key;
        }

        // The keys this area gives its pictures, answered here; any other key goes to whoever asked before.
        private static void PicturesOn()
        {
            if (_picturesOn)
            {
                return;
            }
            _picturesOn = true;
            Func<string, Sprite> before = UiPictures.Find;
            UiPictures.Find = key => key != null && (key.StartsWith(DialogRules.SwatchKey, StringComparison.Ordinal) || key.StartsWith(DialogRules.PictureKey, StringComparison.Ordinal))
                ? Picture(key)
                : before?.Invoke(key);
        }

        // A colour's swatch is a square of that colour, made once a colour a city; a face is the game's own sprite.
        private static Sprite Picture(string key)
        {
            if (Pictures.TryGetValue(key, out Sprite known) || Swatches.TryGetValue(key, out known))
            {
                return known;
            }
            if (!DialogRules.SwatchColour(key, out byte r, out byte g, out byte b))
            {
                return null;
            }
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = key, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(r, g, b, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite made = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            made.name = key;
            Swatches[key] = made;
            return made;
        }

        // The game's dialogue panel on the HUD: its entries' scroll view the one leaf the kit hides.
        private sealed class DialogGameScreen : IUiGameScreen
        {
            private const string Entries = "Entries (Scroll View)";   // the panel's own scroll of lines; anything else a mod put in the panel stays

            public string Id => DialogId;

            public string Name => "the game's dialogue panel";

            public string Title => UiText.T("reallife_ui_dialog_title");

            public UiIcon Icon => UiIcon.Message;

            public UiGameKind Kind => UiGameKind.Panel;

            public Component Find()
            {
                DialogUI ui = Ui();
                if (ui == null)
                {
                    return null;   // the HUD is built a little after the city
                }
                var panel = GameContract.DialogPanel.Field != null ? GameContract.DialogPanel.Field.GetValue(ui) as GameObject : null;
                return panel != null ? panel.transform : (Component)ui;
            }

            public string Check(Component found)
            {
                if (found is DialogUI)
                {
                    return "its panel is not where this version keeps it";
                }
                if (found.transform.Find(Entries) == null)
                {
                    return "its lines are not there";
                }
                return GameContract.DialogTextTemplate.Field == null || GameContract.DialogEntryNow.Field == null ? "its lines are not kept as this version expects" : null;
            }

            public void Conceal(Component found, UiConceal conceal) => conceal.Add(found.transform.Find(Entries));

            public IUiScreen Make(Component found) => new DialogScreen(Ui(), found.transform);

            private static DialogUI Ui()
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                return uis != null && uis.playerHUD != null ? uis.playerHUD.dialogUI : null;
            }
        }

        // The game's spoken line on its own canvas: the card's four pieces the leaves the kit hides, the card itself left as it is.
        private sealed class MonologueGameScreen : IUiGameScreen, IUiGameWay, IUiGameInteract
        {
            public bool Keys => true;         // Interact is Next and Cancel is Skip while a line shows

            public bool Exclusive => false;   // only Interact is taken (IUiGameInteract), Cancel only when the line can be closed (TakesBack); no other key is touched

            public bool Blocks => false;      // clicks pass to the world: the card takes only its own
            private static readonly string[] Leaves = { "Background", "Image", "Message", "Close" };

            public string Id => MonologueId;

            public string Name => "the game's spoken lines";

            public string Title => UiText.T("reallife_ui_screen_monologue");

            public UiIcon Icon => UiIcon.Megaphone;

            public UiGameKind Kind => UiGameKind.Overlay;

            public Component Find()
            {
                MonologueUI ui = Ui();
                if (ui == null)
                {
                    return null;
                }
                var panel = GameContract.MonologuePanel.Field != null ? GameContract.MonologuePanel.Field.GetValue(ui) as RectTransform : null;
                return panel != null ? panel : (Component)ui;
            }

            public string Check(Component found)
            {
                if (found is MonologueUI)
                {
                    return "its card is not where this version keeps it";
                }
                return found.transform.Find("Message") == null || found.transform.Find("Close") == null ? "its words or its close are not there" : null;
            }

            public void Conceal(Component found, UiConceal conceal)
            {
                for (int i = 0; i < Leaves.Length; i++)
                {
                    conceal.Add(found.transform.Find(Leaves[i]));
                }
            }

            public IUiScreen Make(Component found) => new MonologueScreen(Ui(), found.transform);

            private static MonologueUI Ui()
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                return uis != null ? uis.monologueUI : null;
            }
        }
    }
}
