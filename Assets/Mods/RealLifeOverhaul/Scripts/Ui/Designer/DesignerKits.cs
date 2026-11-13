using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The interior designer on the RLO interface, through its one switch ("designer", the tools' section): the host (UiGameHost) puts the kit over the game's designer
    /// while the switch is on and gives the game's own back, exactly as found, while it is off or the designer is the blueprint creator's. The game's designer stays open
    /// and hidden under the kit, so InteriorDesignerUI.IsOpen, its tools, its panels, its undo stack and the mods that read them are all as they were. Once a second while
    /// the interface is on (UiDirector); nothing while the designer is shut.
    /// </summary>
    internal static class DesignerKits
    {
        private static readonly UiGameHost Host = new UiGameHost(new DesignerGameScreen());
        private static readonly Image[] Shields = new Image[3];

        /// <summary>Puts the host in place while the switch is on, the game's own designer while it is off.</summary>
        public static void Install()
        {
            if (UiScreenSwitches.On(DesignerFace.ScreenId))
            {
                DesignerGame.Hook();
                Host.Install();
            }
            else
            {
                Remove("its switch is off");
            }
        }

        /// <summary>The game's designer as it was: the kit gone, its hidden pieces seen again, the game's events let go.</summary>
        public static void Remove(string why)
        {
            HudHints.Keep(false);
            Host.Remove(why);
            DesignerTips.Forget();
            DesignerGame.Stale = null;
            DesignerGame.Unhook();
            Shields[0] = Shields[1] = Shields[2] = null;
        }

        /// <summary>A new city: the designer is looked for again.</summary>
        public static void Rearm()
        {
            Host.Rearm();
            DesignerGame.Forget();
            DesignerTips.Forget();
        }

        /// <summary>The host's state for the interface's line at load.</summary>
        public static string State => Host.State;

        /// <summary>True while the kit draws the designer, for the tests.</summary>
        public static bool Showing => Host.Showing;

        /// <summary>The kit's face while it is up, for the tests; null otherwise.</summary>
        internal static DesignerFace Face => Host.Screen as DesignerFace;

        /// <summary>
        /// What the face holds now, for the test: the tool in hand, how many of the business's needs are met, the ones still short and what the edit comes to; null while the
        /// kit does not draw the designer.
        /// </summary>
        internal static string Describe(out string tool, out string met, out string shortOf, out string apply)
        {
            tool = met = shortOf = apply = null;
            DesignerFace face = Face;
            if (face == null || !Host.Showing)
            {
                return null;
            }
            UiDesignerModel m = face.Model;
            tool = m.Tools.Tool >= 0 && m.Tools.Tool < m.Tools.Tools.Length ? m.Tools.Tools[m.Tools.Tool].Name : UiText.T("reallife_ui_designer_no_tool");
            met = m.Info.RequireShown ? m.Info.RequireFigure : UiText.T("reallife_ui_designer_no_needs");
            var names = new System.Text.StringBuilder();
            int named = 0, more = 0;
            for (int i = 0; i < m.Info.RequireCount; i++)
            {
                if (m.Info.Require[i].Met)
                {
                    continue;
                }
                if (named >= 3)
                {
                    more++;   // the notice names three; the rest are counted
                    continue;
                }
                names.Append(named++ > 0 ? ", " : string.Empty).Append(m.Info.Require[i].Title);
            }
            shortOf = named == 0 ? UiText.T("reallife_ui_designer_no_needs") : names + (more > 0 ? " +" + more : string.Empty);
            apply = m.OnApply ?? UiText.Money(0f);
            return met;
        }

        /// <summary>The kit's root over the room: what the tooltips stand under.</summary>
        public static Transform Root => Host.Root;

        /// <summary>The designer is the blueprint creator's, whose panels the game draws: the kit steps back for as long as it is open.</summary>
        public static void Classic() => Host.Classic();

        /// <summary>
        /// The kit's boxes that are not targets let the mouse through to the room under them, where a click would place or sell something. A clear shield under the info
        /// panel, the rising panel and the bar holds those clicks, as the game's own panels do, and nothing else.
        /// </summary>
        public static void Shield(float[] rects, int count)
        {
            RectTransform root = Host.Root;
            UiHost host = root != null ? root.GetComponent<UiHost>() : null;
            if (host == null || host.View == null)
            {
                return;
            }
            for (int i = 0; i < Shields.Length; i++)
            {
                if (Shields[i] == null)
                {
                    RectTransform r = UiView.NewRect(root, "RealLife designer shield " + i);
                    r.SetAsFirstSibling();
                    Image image = r.gameObject.AddComponent<Image>();
                    image.color = new Color(0f, 0f, 0f, 0f);
                    Shields[i] = image;
                }
                bool on = i < count;
                if (Shields[i].gameObject.activeSelf != on)
                {
                    Shields[i].gameObject.SetActive(on);
                }
                if (on)
                {
                    host.View.Place(Shields[i].rectTransform, rects[i * 4], rects[i * 4 + 1], rects[i * 4 + 2], rects[i * 4 + 3]);
                }
            }
        }
    }

    /// <summary>The game's designer as the host needs it: where its toolbar is, which of its pieces to hide, and the face that mirrors it.</summary>
    internal sealed class DesignerGameScreen : IUiGameScreen, IUiGameWay, IUiGameStands
    {
        public string Id => DesignerFace.ScreenId;

        public string Name => "the interior designer";

        public string Title => UiText.T("interior_designer");

        public UiIcon Icon => UiIcon.Sofa;

        public UiGameKind Kind => UiGameKind.Overlay;

        public bool Keys => false;

        public bool Exclusive => false;

        public bool Blocks => false;

        public Component Find()
        {
            GameObject toolbar = DesignerGame.Toolbar();
            return toolbar != null ? toolbar.transform : null;
        }

        public string Check(Component found) => DesignerGame.Check();

        public void Conceal(Component found, UiConceal conceal) => DesignerGame.Conceal(conceal);

        public IUiScreen Make(Component found) => new DesignerFace();
    }

    /// <summary>The designer's key chips: the game's own hint lines as chips, read the way the HUD reads them.</summary>
    internal static class DesignerHints
    {
        private static readonly UiPrompt[] Scratch = new UiPrompt[9];

        /// <summary>The game's hint lines into the model's prompts; how many.</summary>
        public static int Read(ref UiPrompt[] into) => HudHints.Mirror(ref into, out _);

        /// <summary>A number that changes when a hint line does.</summary>
        public static long Signature()
        {
            HudHints.Mirror(Scratch, out int sig);
            return sig;
        }

        /// <summary>The kit's keys with the hint lines' own key names on the number actions the chips carry.</summary>
        public static IUiKeys Wrap(IUiKeys inner) => HudHints.Wrap(inner);
    }
}
