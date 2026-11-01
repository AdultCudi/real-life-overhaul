using System;
using UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The HUD over the running game as a screen of the kit (UiHud): its model's pieces at the screen's edges and the status
    /// bar with its pills, laid out by the HUD host (HudHost) whenever the model or what the pills show changes. It takes no
    /// key: the mouse presses. Its pills press the game's own methods, as the game's top bar does (the pause button,
    /// Topbar.ClickMoney, Topbar.ClickPersona); the app row and the alerts go to whoever owns them (UiHud.ChromePressed,
    /// UiHud.AlertsPressed), and a piece's own commands to whoever put it up (<see cref="PiecePressed"/>). Cost: a layout a
    /// change; nothing a frame.
    /// </summary>
    public sealed class HudScreen : IUiScreen
    {
        private const int FrameFirst = UiDraw.CloseCommand + 10, FrameLast = UiDraw.CloseCommand + 19;   // the app row and All apps: the frame's own (9010 to 9019)

        /// <summary>What the HUD shows besides the bar; whoever changes it sets <see cref="Dirty"/>.</summary>
        public readonly UiHudModel Model = new UiHudModel { MoneyChange = false };

        /// <summary>A piece's own command pressed (an info panel's link, a side panel's button, a tool): true when it did something.</summary>
        public Func<int, int, bool> PiecePressed;

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
                UiHud.Draw(s, Model, frame, keys, false);
                for (int i = 0; i < Model.PromptCount; i++)
                {
                    s.AddPrompt(Model.Prompts[i].Action, Model.Prompts[i].Label);
                }
                UiDraw.PromptBar(s, frame, keys);
                LastBar = UiChrome.Bar != null ? UiChrome.Bar() : null;
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
                    return Pause();
                case UiDraw.MoneyPillCommand:
                    return TopBar(true);
                case UiDraw.NeedsPillCommand:
                    return TopBar(false);
                case UiDraw.AlertsPillCommand:
                    return UiHud.AlertsPressed != null && UiHud.AlertsPressed();
            }
            if (command >= FrameFirst && command <= FrameLast)
            {
                return UiHud.ChromePressed != null && UiHud.ChromePressed(command, arg);
            }
            return PiecePressed != null && PiecePressed(command, arg);
        }

        // The game's pause button: its own method, which keeps its rules (a time lock while a scene runs, Going Public's shared clock).
        private static bool Pause()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.gameSpeed == null || uis.gameSpeed.isTimeControlDisabled)
            {
                return false;
            }
            uis.gameSpeed.TogglePause();
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
