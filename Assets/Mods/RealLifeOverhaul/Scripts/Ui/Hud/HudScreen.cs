using System;
using UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The HUD over the running game as a screen of the kit (UiHud): its model's pieces at the screen's edges and the status
    /// bar with its pills, laid out by the HUD host (HudHost) whenever the model or what the pills show changes. It takes no
    /// key: the mouse presses. Its pills press the game's own methods, as the game's top bar does (the pause button,
    /// Topbar.ClickMoney, Topbar.ClickPersona, Topbar.OpenMiniMenu behind the gear); the app row presses each app's own
    /// button as the full menu's row does (ShellApps.Press), All apps stands the frame's All apps over the city
    /// (<see cref="AllApps"/>), the bell opens the game's own notice list (NotificationsListUI.Toggle), and a piece's own
    /// commands go to whoever put it up (<see cref="PiecePressed"/>). Cost: a layout a change; nothing a frame.
    /// </summary>
    public sealed class HudScreen : IUiScreen
    {
        /// <summary>What the HUD shows besides the bar; whoever changes it sets <see cref="Dirty"/>.</summary>
        public readonly UiHudModel Model = new UiHudModel { MoneyChange = false };

        /// <summary>A piece's own command pressed (an info panel's link, a side panel's button, a tool): true when it did something.</summary>
        public Func<int, int, bool> PiecePressed;

        /// <summary>The All apps tile pressed: the HUD host stands the frame's All apps over the city.</summary>
        public Func<bool> AllApps;

        public string Id => "hud";

        public bool Dirty { get; set; } = true;

        /// <summary>The bar's model as the last layout read it, for the money's change to stand over its pill.</summary>
        public UiStatusModel LastBar { get; private set; }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin(Id, frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            HudStatusGame.ForHud = true;   // its pills are targets here, and only here
            try
            {
                UiStatusModel bar = UiChrome.Bar != null ? UiChrome.Bar() : null;
                UiHud.AddPrompts(s, Model, bar, keys);   // first, so the pieces keep clear of their chips
                UiHud.Draw(s, Model, frame, keys, false, bar);
                UiHud.Bar(s, Model, frame, keys);
                LastBar = bar;
            }
            finally
            {
                HudStatusGame.ForHud = false;
            }
            Dirty = false;
        }

        public bool Press(int command, int arg)
        {
            switch (command)
            {
                case UiDraw.TimePillCommand:
                    return arg == UiDraw.FastSquare ? Fast() : Pause(arg == 0);
                case UiDraw.MoneyPillCommand:
                    return TopBar(true);
                case UiDraw.NeedsPillCommand:
                    return TopBar(false);
                case UiDraw.AlertsPillCommand:
                    return Notices();
                case UiDraw.SettingsPillCommand:
                    return Menu();
            }
            if (command == UiDraw.AppRowCommand)
            {
                return OpenRow(arg);
            }
            if (command == UiDraw.AllAppsCommand)
            {
                return AllApps != null && AllApps();
            }
            return PiecePressed != null && PiecePressed(command, arg);
        }

        // The row's arg-th app pressed while walking: its own button, as the full menu's row presses it (ShellApps.Press).
        private static bool OpenRow(int index)
        {
            int seen = 0;
            for (int i = 0; i < UiShellApps.Items.Count; i++)
            {
                if (UiShellApps.Items[i].InRow && seen++ == index)
                {
                    return ShellApps.Press(UiShellApps.Items[i].Id);
                }
            }
            return false;
        }

        // The time's pause or play square: the game's own pause button's method, which keeps its rules (a time lock while a
        // scene runs, Going Public's shared clock), pressed only when the game is not already so.
        private static bool Pause(bool pause)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.gameSpeed == null || uis.gameSpeed.isTimeControlDisabled)
            {
                return false;
            }
            if (uis.gameSpeed.Paused != pause)
            {
                uis.gameSpeed.TogglePause();
            }
            return true;
        }

        // The fast forward square: the game's own switch on its activity panel, flipped as its key flips it (the game's Secondary
        // key, PlayerActivityUI.OnPlayerActionPressed), while the game offers it; else a notice of when the game offers one.
        private static bool Fast()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (!HudStatusGame.FastOffered(uis))
            {
                UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, "reallife_hud_fast_why", null, 6f);
                return true;
            }
            uis.playerActivityUI.OnPlayerActionPressed(global::PlayerAction.SecondaryInteract);
            return true;
        }

        // The gear opens what the game's own top right button opens: its menu (Topbar.OpenMiniMenu, which keeps its rules,
        // refusing while a purchase or the full menu is up).
        private static bool Menu()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.topBar == null || uis.miniMenuUI == null)
            {
                return false;
            }
            uis.topBar.OpenMiniMenu();
            return true;
        }

        // The bell opens the game's own notice list, which the frame draws over (ShellNotices), as the game's own bell does.
        private static bool Notices()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.notificationsListUI == null)
            {
                return false;
            }
            uis.notificationsListUI.Toggle(true);
            return true;
        }

        // The money opens EconoView and the needs Persona, as the game's top bar does when they are clicked there.
        private static bool TopBar(bool money)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.topBar == null)
            {
                return false;
            }
            if (money)
            {
                uis.topBar.ClickMoney();
            }
            else
            {
                uis.topBar.ClickPersona();
            }
            return true;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => false;   // the HUD takes no key: Escape is the game's

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }
    }
}
