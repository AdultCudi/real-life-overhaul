using System;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's uniform presets' editor (UIs.employeePresetCustomizer, opened from BizMan's settings) drawn on the RLO
    /// interface through the host every game screen uses (UiGameHost): the game's editor stays alive under the kit with
    /// only its own presets box, title bar and the customiser's own controls unseen and untouchable, given back exactly as
    /// found; its 3D preview of the figure and its dark backdrop stay the game's, drawn where the game draws them. The
    /// kit's screen (UniformsScreen) reads the presets, the parts, the pieces and the colours after the game filled them and
    /// presses the game's own buttons, so the game's questions (unsaved changes, a preset deleted) stay its own. Its switch
    /// in Mod settings ("ui_screen_uniforms") gives the game's own editor back alone. Cost: what the host costs: nothing
    /// while the editor is closed; while it shows, the input reads and a look at the editor four times a second.
    /// </summary>
    public static class UniformsKit
    {
        public const string Id = "uniforms";

        private const string Box = "Container/PresetsBox", Bar = "Container/Topbar", Preview = "CharacterPreviewMask";

        // The customiser's own controls the kit draws for; its preview (CharacterPreviewMask) and anything a mod added stay.
        private static readonly string[] Leaves = { "GenderButtons", "Menu", "ElementColorPicker", "Panel", "PanelWhiteOutline", "SavePresetButton" };

        private static readonly UiGameHost Host = new UiGameHost(new UniformsGameScreen());

        /// <summary>Where it stands, for the interface's line at load.</summary>
        public static string State => "the uniform presets (" + Host.State + ")";

        /// <summary>True while the kit draws the editor.</summary>
        public static bool Showing => Host.Showing;

        /// <summary>Puts the kit over the editor while its switch is on, and gives it back while it is off; asked once a second.</summary>
        public static void Install()
        {
            if (UiScreenSwitches.On(Id))
            {
                Host.Install();
            }
            else
            {
                Host.Remove("its switch is off");
            }
        }

        /// <summary>A new city: an editor that never looked as expected in the last one is looked at again.</summary>
        public static void Rearm() => Host.Rearm();

        /// <summary>The game's editor as it was: the kit gone, its controls seen and touchable again.</summary>
        public static void Remove(string why) => Host.Remove(why);

        /// <summary>True while the game's editor is open.</summary>
        public static bool Open
        {
            get
            {
                EmployeePresetCustomizer e = Editor();
                return e != null && e.gameObject.activeInHierarchy;
            }
        }

        /// <summary>
        /// What BizMan's kit waits on while the editor stands over it: true once the editor opened and closed again, or when
        /// it never opened within two seconds (another mod kept it shut). The editor opens a frame after its button.
        /// </summary>
        public static Func<bool> Over()
        {
            float since = Time.unscaledTime;
            bool seen = false;
            return () =>
            {
                bool open = Open;
                seen |= open;
                return seen ? !open : Time.unscaledTime - since > 2f;
            };
        }

        internal static EmployeePresetCustomizer Editor()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null ? uis.employeePresetCustomizer : null;
        }

        // The game's editor as the host needs it: a canvas of its own over the full menu, its presets box and title bar, and
        // the customiser's own controls beside its preview.
        private sealed class UniformsGameScreen : IUiGameScreen
        {
            public string Id => UniformsKit.Id;

            public string Name => "the uniform presets' editor";

            public string Title => UiText.T("myemployees_configure_uniform_presets");

            public UiIcon Icon => UiIcon.PaintBrush;

            public UiGameKind Kind => UiGameKind.Box;

            public Component Find() => Editor();

            public string Check(Component found)
            {
                var e = (EmployeePresetCustomizer)found;
                if (GameContract.UniformTemplate.Field == null || GameContract.UniformNew.Field == null || GameContract.UniformEditor.Field == null
                    || GameContract.UniformSave.Field == null || GameContract.UniformPicker.Field == null || GameContract.UniformMenu.Field == null
                    || GameContract.UniformPieces.Field == null || GameContract.UniformTabPress.Field == null || GameContract.UniformTabOn.Field == null)
                {
                    return "its presets or its customiser are not as this version has them";
                }
                var u = GameContract.UniformEditor.Field.GetValue(e) as UniformCustomizer;
                if (u == null || u.transform.Find(Preview) == null || e.transform.Find(Box) == null || e.transform.Find(Bar + "/Close") == null)
                {
                    return "its preview, its presets box or its title bar is not there";
                }
                return null;
            }

            public void Conceal(Component found, UiConceal conceal)
            {
                var e = (EmployeePresetCustomizer)found;
                conceal.Add(e.transform.Find(Box));
                // The title bar shares its name with the full menu's, which mods fill: its own three leaves are hidden instead.
                Transform bar = e.transform.Find(Bar);
                for (int i = 0; bar != null && i < bar.childCount; i++)
                {
                    Transform c = bar.GetChild(i);
                    if (c.name == "Background" || c.name == "Title" || c.name == "Close")
                    {
                        conceal.Add(c);
                    }
                }
                var u = GameContract.UniformEditor.Field.GetValue(e) as UniformCustomizer;
                for (int i = 0; u != null && i < u.transform.childCount; i++)
                {
                    Transform c = u.transform.GetChild(i);
                    if (Array.IndexOf(Leaves, c.name) >= 0)
                    {
                        conceal.Add(c);
                    }
                }
            }

            public IUiScreen Make(Component found) => new UniformsScreen((EmployeePresetCustomizer)found);
        }

        /// <summary>The editor's own close button, which asks first when a change is unsaved.</summary>
        internal static UnityEngine.UI.Button CloseButton(EmployeePresetCustomizer e)
        {
            Transform t = e != null ? e.transform.Find(Bar + "/Close") : null;
            return t != null ? t.GetComponent<UnityEngine.UI.Button>() : null;
        }

        /// <summary>The kit's own root over the editor's canvas, where its px are measured, or null before the bind.</summary>
        internal static RectTransform Root => Host.Root;

        /// <summary>The customiser's preview of the figure, which the kit leaves where the game draws it.</summary>
        internal static RectTransform PreviewOf(UniformCustomizer u) => u != null ? u.transform.Find(Preview) as RectTransform : null;
    }
}
