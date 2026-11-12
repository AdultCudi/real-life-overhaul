using System;
using System.Collections;
using System.Collections.Generic;
using Tooltip;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's tests of the tooltips (Options, Tests): a tooltip given to the game's own builder (a header, a bar, a list of
    /// skills and a tick list, as an employee's is) and held a few seconds beside the pointer, so the card can be seen drawing it and
    /// following the mouse; the net worth card over the money pill; and the tooltips switched to the game's own and back. Nothing is saved:
    /// the switch test changes the running value only.
    /// </summary>
    public sealed partial class UiDirector
    {
        private const float TooltipHold = 10f;   // seconds a tested tooltip stays
        private int _tooltipTest;                // the test last started: an older hold does not take a newer one away

        /// <summary>Options, Tests: a tooltip built through the game's TooltipSystem, held beside the pointer for a few seconds.</summary>
        public void TestTooltip()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                Slow();
                if (InstanceBehavior<TooltipSystem>.Instance == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_tooltip_none", null, 8f);
                    return;
                }
                TooltipSystem.Show();
                TooltipSystem.AddHeader(new Localizor.LanguageChangeEvent.LanguageChangeEventDataHolder { Key = UiText.T("reallife_ui_test_tooltip_title") },
                    new Localizor.LanguageChangeEvent.LanguageChangeEventDataHolder { Key = UiText.T("reallife_ui_test_tooltip_second") });
                TooltipSystem.AddSplitter();
                TooltipSystem.AddLabel(UiText.T("reallife_ui_test_tooltip_line"), new Color(0.79f, 0.82f, 0.84f, 1f));
                TooltipSystem.AddProgressBar(72, new Color32(88, 196, 140, 255));
                TooltipSystem.AddSplitter();
                TooltipSystem.AddList(new List<string> { UiText.T("reallife_ui_test_tooltip_item_a"), UiText.T("reallife_ui_test_tooltip_item_b") });
                TooltipSystem.AddSplitter();
                TooltipSystem.AddList(new List<Tuple<string, bool>> { Tuple.Create("common_open", true), Tuple.Create("common_closed", false) });
                int mine = ++_tooltipTest;
                StartCoroutine(Guard.Routine(TooltipSoon(mine, false)));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the net worth card over the money pill, held a few seconds.</summary>
        public void TestNetWorthCard()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                Slow();
                if (!TooltipKit.Money(0.86f, 0.955f, 0.07f))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_tooltip_off", null, 8f);
                    return;
                }
                int mine = ++_tooltipTest;
                StartCoroutine(Guard.Routine(TooltipSoon(mine, true)));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the tooltips switched to the game's own, and back on the next press.</summary>
        public void TestTooltipsSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                bool on = !UiScreenSwitches.On(TooltipKit.Id);
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf(TooltipKit.Id), on);
                Slow();
                Notifications.Show(NotificationType.Info, on ? "reallife_ui_test_tooltip_kit" : "reallife_ui_test_tooltip_game", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // The tested tooltip holds a while and goes as the pointer leaving it would take it away; says whether the kit drew it.
        private IEnumerator TooltipSoon(int mine, bool money)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            string key = TooltipKit.Showing ? "reallife_ui_test_tooltip_on_kit" : TooltipKit.Drawn ? "reallife_ui_test_tooltip_waiting" : "reallife_ui_test_tooltip_on_game";
            Notifications.Show(NotificationType.Info, key, null, 6f);
            yield return new WaitForSecondsRealtime(TooltipHold);
            if (mine != _tooltipTest)
            {
                yield break;
            }
            if (money)
            {
                TooltipKit.MoneyOut();
            }
            else
            {
                TooltipSystem.Hide();
            }
        }
    }
}
