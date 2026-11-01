using UI;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's My Employees app and a rival's staff drawn on the RLO interface, through the host every game screen uses
    /// (UiGameHost): the game's app stays alive under the kit with only its layout ("Layout 30-70": the employee box, the
    /// tables, the filter panels and the panel of mass actions) unseen and untouchable, given back exactly as found; the
    /// kit's screens (EmployeesScreen, RivalStaffScreen) read the game's tables and box after the game and other mods filled
    /// them and press the game's own buttons and methods, so every rule of the game stays the game's. Each has its switch in
    /// Mod settings ("ui_screen_my_employees", "ui_screen_rival_staff") that gives the game's own screen back alone. Cost:
    /// what the host costs: nothing while closed; while one shows, the input reads and one box test a frame.
    /// </summary>
    public static class EmployeesKit
    {
        public const string StaffId = "my_employees";
        public const string RivalId = "rival_staff";

        private static readonly UiGameHost Staff = new UiGameHost(new StaffGameScreen());
        private static readonly UiGameHost Rival = new UiGameHost(new RivalGameScreen());

        /// <summary>Where each stands, for the interface's line at load.</summary>
        public static string State => "My Employees (" + Staff.State + "), a rival's staff (" + Rival.State + ")";

        /// <summary>True while the kit draws My Employees.</summary>
        public static bool Showing => Staff.Showing;

        /// <summary>Puts the kit over each screen whose switch is on, and gives back the one whose switch is off; asked once a second.</summary>
        public static void Install()
        {
            Keep(Staff, StaffId);
            Keep(Rival, RivalId);
        }

        private static void Keep(UiGameHost host, string id)
        {
            if (UiScreenSwitches.On(id))
            {
                host.Install();
            }
            else
            {
                host.Remove("its switch is off");
            }
        }

        /// <summary>A new city: a screen that never looked as expected in the last one is looked at again.</summary>
        public static void Rearm()
        {
            Staff.Rearm();
            Rival.Rearm();
        }

        /// <summary>The game's screens as they were: the kit gone, their layouts seen and touchable again.</summary>
        public static void Remove(string why)
        {
            Staff.Remove(why);
            Rival.Remove(why);
        }

        /// <summary>My Employees in the game's own view until it closes: something only the game's view shows.</summary>
        public static void Classic() => Staff.Classic();

        // The game's My Employees, under the full menu: its layout the one leaf the kit hides.
        private sealed class StaffGameScreen : IUiGameScreen
        {
            private const string Layout = "Layout 30-70";   // the app's own layout; anything else a mod added to the app stays

            public string Id => StaffId;

            public string Name => "the My Employees app";

            public string Title => UiText.T("smartphone_myemployees");

            public UiIcon Icon => PhoneLooks.Glyph(StaffViews.StaffApp);

            public UiGameKind Kind => UiGameKind.App;

            public Component Find()
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                return uis != null && uis.fullMenu != null ? uis.fullMenu.myEmployees : null;   // the phone is built a little after the city
            }

            public string Check(Component found)
            {
                var app = (global::UI.Smartphone.Apps.MyEmployees.MyEmployees)found;
                if (found.transform.Find(Layout) == null)
                {
                    return "its layout is not there";
                }
                return app.employeeScrollerController == null || app.candidateScrollerController == null || app.massActionsUI == null ? "its tables or its mass actions are not there" : null;
            }

            public void Conceal(Component found, UiConceal conceal) => conceal.Add(found.transform.Find(Layout));

            public IUiScreen Make(Component found) => new EmployeesScreen((global::UI.Smartphone.Apps.MyEmployees.MyEmployees)found);
        }

        // A rival's staff, a window of its own over BizMan (UIs.rivalEmployeesUi): a box, so its keys are its own while it stands.
        private sealed class RivalGameScreen : IUiGameScreen
        {
            public string Id => RivalId;

            public string Name => "a rival's staff";

            public string Title => UiText.T("smartphone_rivals");

            public UiIcon Icon => PhoneLooks.Glyph(StaffViews.RivalsApp);

            public UiGameKind Kind => UiGameKind.Box;

            public Component Find()
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                return uis != null ? uis.rivalEmployeesUi : null;
            }

            public string Check(Component found) => GameContract.RivalStaffTable.Field == null || GameContract.RivalStaffTable.Field.GetValue(found) == null
                ? "its table is not there" : null;

            // The game's window is two children of its canvas: the dimmed world (which keeps holding clicks off the world) and
            // the panel. Another mod's piece added beside them is not the game's, so it stays seen.
            private static readonly string[] GameNames = { Dimmer, "Container" };
            private const string Dimmer = "Dimmer";

            public void Conceal(Component found, UiConceal conceal)
            {
                Transform root = found.transform;
                for (int i = 0; i < root.childCount; i++)
                {
                    Transform child = root.GetChild(i);
                    if (UiHostRules.Own(child.name, GameNames) && !child.name.StartsWith(UiHostRules.ModPrefix, System.StringComparison.Ordinal))
                    {
                        conceal.Add(child, child.name == Dimmer);
                    }
                }
            }

            public IUiScreen Make(Component found) => new RivalStaffScreen((RivalEmployeesUi)found);
        }
    }
}
