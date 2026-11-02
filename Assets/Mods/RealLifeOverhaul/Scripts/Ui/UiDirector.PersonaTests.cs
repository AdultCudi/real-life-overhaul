using System;
using UI;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's tests of Persona, Rivals and Market Insider (Options, Tests): each app opened on the kit as its
    /// phone button opens it, and each switched back to the game's own and on again. Each says in a notice exactly why it
    /// cannot run when it cannot. Nothing is saved: a switch test changes the running value only, and the next start reads
    /// the page's.
    /// </summary>
    public sealed partial class UiDirector
    {
        /// <summary>Options, Tests: Persona, which the kit draws while its switch is on.</summary>
        public void TestPersona() => OpenOnKit(PersonaViews.App, PersonaKit.Install);

        /// <summary>Options, Tests: Rivals, which the kit draws while its switch is on.</summary>
        public void TestRivals() => OpenOnKit(RivalsViews.App, RivalsKit.Install);

        /// <summary>Options, Tests: Market Insider, which the kit draws while its switch is on.</summary>
        public void TestMarket() => OpenOnKit(MarketViews.App, MarketKit.Install);

        /// <summary>Options, Tests: Persona switched to the game's own for this run, or back onto the kit.</summary>
        public void TestPersonaSwitch() => Flip(PersonaKit.Id, "reallife_ui_test_persona_kit", "reallife_ui_test_persona_game");

        /// <summary>Options, Tests: Rivals switched to the game's own for this run, or back onto the kit.</summary>
        public void TestRivalsSwitch() => Flip(RivalsKit.Id, "reallife_ui_test_rivals_kit", "reallife_ui_test_rivals_game");

        /// <summary>Options, Tests: Market Insider switched to the game's own for this run, or back onto the kit.</summary>
        public void TestMarketSwitch() => Flip(MarketKit.Id, "reallife_ui_test_market_kit", "reallife_ui_test_market_game");

        // A game app opened as its phone button opens it, with the kit put over it first, or a notice that says why it cannot be.
        private void OpenOnKit(string app, Action install)
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (uis == null || uis.smartphoneUI == null || uis.fullMenu == null || uis.fullMenu.appsContainer == null || uis.fullMenu.appsContainer.Find(app) == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_phone", null, 8f);
                    return;
                }
                install();
                uis.smartphoneUI.OpenApp(app);
                Slow();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // A screen's own switch flipped for this run, and a notice that says which face it now has.
        private void Flip(string id, string kitKey, string gameKey)
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                bool on = !UiScreenSwitches.On(id);
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf(id), on);
                Notifications.Show(NotificationType.Info, on ? kitKey : gameKey, null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }
    }
}
