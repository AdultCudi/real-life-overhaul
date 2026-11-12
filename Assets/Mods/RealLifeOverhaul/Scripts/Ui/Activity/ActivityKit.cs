using Timemachine;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's activity panel, its time skip, its funeral and its time of day tool on the kit, through the host
    /// (UiGameHost) like any game screen, each with its own switch: the activity panel as an overlay that takes no key (the
    /// game reads its own), the time skip and the funeral as boxes over everything, the time of day tool as an overlay.
    /// The game's screens stay up and live underneath, their layouts unseen. Cost: nothing while none of them shows; while
    /// one does, its screen's looks (UiGameHost).
    /// </summary>
    public static class ActivityKit
    {
        private static readonly UiGameHost Panel = new UiGameHost(new PanelGame());
        private static readonly UiGameHost Skip = new UiGameHost(new SkipGame());
        private static readonly UiGameHost Funeral = new UiGameHost(new FuneralGame());
        private static readonly UiGameHost DayTool = new UiGameHost(new DayToolGame());
        private static readonly UiGameHost[] All = { Panel, Skip, Funeral, DayTool };

        /// <summary>Once a second while the interface is on: each host put on while its switch is on, the game's own while off.</summary>
        public static void Install()
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (UiScreenSwitches.On(All[i].Id))
                {
                    All[i].Install();
                }
                else
                {
                    All[i].Remove("its switch is off");
                }
            }
        }

        /// <summary>Every screen the game's own again.</summary>
        public static void Remove(string why)
        {
            for (int i = 0; i < All.Length; i++)
            {
                All[i].Remove(why);
            }
        }

        /// <summary>A new city: the screens are looked for again.</summary>
        public static void Rearm()
        {
            for (int i = 0; i < All.Length; i++)
            {
                All[i].Rearm();
            }
        }

        /// <summary>The screens' states for the interface's line at load.</summary>
        public static string States() => "activity panel (" + Panel.State + "), time skip (" + Skip.State + "), funeral (" + Funeral.State + "), time of day tool (" + DayTool.State + ")";

        /// <summary>The activity's strip while it shows above the status bar, for the HUD to keep its pieces clear of it; null otherwise.</summary>
        public static UiActivityModel Strip => Panel.Showing && Panel.Screen is ActivityPanelScreen screen ? screen.Strip : null;

        /// <summary>True while the activity panel is drawn on the kit (its switch on, the interface on, the panel open).</summary>
        public static bool PanelShowing => Panel.Showing;

        // Each leaf of a screen hidden, but the branch holding the game's own fade: its leaves are hidden instead, so the
        // game's fade keeps meaning what the game set it to.
        private static void ConcealAround(Transform root, Transform keep, UiConceal conceal)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (keep != null && child == keep)
                {
                    for (int k = 0; k < keep.childCount; k++)
                    {
                        conceal.Add(keep.GetChild(k));
                    }
                    conceal.Add(keep.GetComponent<Graphic>());
                }
                else if (keep != null && keep.IsChildOf(child))
                {
                    ConcealAround(child, keep, conceal);
                    conceal.Add(child.GetComponent<Graphic>());
                }
                else
                {
                    conceal.Add(child);
                }
            }
            conceal.Add(root.GetComponent<Graphic>());
        }

        // ------------------------------------------------------------------------------------------------- the activity panel

        private sealed class PanelGame : IUiGameScreen
        {
            public string Id => "activity_panel";

            public string Name => "the activity panel";

            public string Title => null;

            public UiIcon Icon => UiIcon.Briefcase;

            public UiGameKind Kind => UiGameKind.Overlay;

            public Component Find() => InstanceBehavior<UIs>.Instance != null ? InstanceBehavior<UIs>.Instance.playerActivityUI : null;

            public string Check(Component found) => GameContract.ActivityFade.Field == null ? "its fade is not there" : null;

            public void Conceal(Component found, UiConceal conceal)
            {
                var fade = GameContract.ActivityFade.Field.GetValue(found) as CanvasGroup;
                ConcealAround(found.transform, fade != null && fade.transform != found.transform ? fade.transform : null, conceal);
            }

            public IUiScreen Make(Component found) => new ActivityPanelScreen((PlayerActivity.PlayerActivityUI)found);
        }

        // ------------------------------------------------------------------------------------------------- the time skip

        private sealed class SkipGame : IUiGameScreen
        {
            public string Id => "time_skip";

            public string Name => "the time skip";

            public string Title => null;

            public UiIcon Icon => UiIcon.SkipForward;

            public UiGameKind Kind => UiGameKind.Box;

            public Component Find()
            {
                TimeMachine machine = InstanceBehavior<UIs>.Instance != null ? InstanceBehavior<UIs>.Instance.timeMachine : null;
                return machine != null ? machine.canvas : null;   // the canvas the game switches on and off as it runs
            }

            public string Check(Component found)
            {
                TimeMachine machine = InstanceBehavior<UIs>.Instance.timeMachine;
                return GameContract.SkipGoal.Field == null || GameContract.SkipDistance.Field == null ? "its goal is not there"
                    : machine.cancelButton == null ? "its Cancel is not there" : null;
            }

            public void Conceal(Component found, UiConceal conceal) => ConcealAround(found.transform, null, conceal);

            public IUiScreen Make(Component found) => new TimeSkipScreen(InstanceBehavior<UIs>.Instance.timeMachine);
        }

        // ------------------------------------------------------------------------------------------------- the funeral

        private sealed class FuneralGame : IUiGameScreen
        {
            public string Id => "funeral";

            public string Name => "the funeral";

            public string Title => null;

            public UiIcon Icon => UiIcon.Flag;

            public UiGameKind Kind => UiGameKind.Box;

            public Component Find() => InstanceBehavior<UIs>.Instance != null ? InstanceBehavior<UIs>.Instance.funeralUI : null;

            public string Check(Component found) => GameContract.FuneralBackup.Field == null ? "its backup is not there" : null;

            public void Conceal(Component found, UiConceal conceal) => ConcealAround(found.transform, null, conceal);

            public IUiScreen Make(Component found) => new FuneralScreen((FuneralUI)found, false);
        }

        // ------------------------------------------------------------------------------------------------- the time of day tool

        private sealed class DayToolGame : IUiGameScreen
        {
            public string Id => "time_of_day";

            public string Name => "the time of day tool";

            public string Title => null;

            public UiIcon Icon => UiIcon.Sun;

            public UiGameKind Kind => UiGameKind.Overlay;

            public Component Find() => GameContract.DayToolSelf.Field != null ? GameContract.DayToolSelf.Field.GetValue(null) as Component : null;

            public string Check(Component found) => GameContract.DayToolSlider.Field == null || !(GameContract.DayToolSlider.Field.GetValue(found) is Slider) ? "its slider is not there" : null;

            public void Conceal(Component found, UiConceal conceal) => ConcealAround(found.transform, null, conceal);

            public IUiScreen Make(Component found) => new TimeOfDayScreen((InGameTimeOfDayPanel)found);
        }
    }
}
