using Extensions;
using TMPro;
using UI;
using UI.DailySummary;
using UI.Topbar;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Shows the day's result (<see cref="DayRules"/>) in the two places the game shows yesterday's profit or loss: the day-end panel and the top bar's
    /// money change. Both are the game's own labels, written once a day after the game wrote them (EconomyDirector.Result.cs waits for the panel to
    /// finish its fade, behind the row's own zero alpha, so nothing flickers). The figure is read, never saved, and the game's own figure stays what
    /// it is: the bank, the tax and the rivals' ranking still read FinancialSummary.totalProfit. Cost: a few labels written a day; nothing per frame.
    /// </summary>
    public static class DayResult
    {
        private const string Negative = "NegativeArrow", Positive = "PositiveArrow";   // the top bar's arrows, children of its money change label

        /// <summary>The day's result for a day, and what is wrong with it (null if nothing). False if the game made no statement for the day.</summary>
        public static bool Compute(int day, out DayFigure figure, out string problem)
        {
            problem = null;
            if (!GameBooks.Result(day, out figure, out float parts))
            {
                return false;
            }
            problem = DayRules.Problem(figure.Game, parts, figure);
            return true;
        }

        /// <summary>The panel's text for an amount, as the game writes its own.</summary>
        public static string PanelText(float amount)
        {
            return amount.ToShortCurrencyFormat();
        }

        /// <summary>The top bar's text for an amount, as the game writes its own.</summary>
        public static string BarText(float amount)
        {
            return amount.ToShortCurrencyFormat(abbreviated: true);
        }

        /// <summary>Writes the top bar's money change and its menu copy. False if the bar is not there yet.</summary>
        public static bool WriteBar(float amount)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            Topbar bar = uis != null ? uis.topBar : null;
            if (bar == null || bar.moneyChange == null)
            {
                return false;
            }
            Write(bar.moneyChange, amount, BarText(amount), true);
            if (bar.fullMenuMoneyChange != null)
            {
                Write(bar.fullMenuMoneyChange, amount, BarText(amount), true);
            }
            return true;
        }

        /// <summary>Writes the day-end panel's income label. False if the panel is not there.</summary>
        public static bool WritePanel(float amount)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            DailySummary panel = uis != null ? uis.dailySummary : null;
            if (panel == null || panel.incomeLabel == null)
            {
                return false;
            }
            Write(panel.incomeLabel, amount, PanelText(amount), false);
            return true;
        }

        /// <summary>True once the day-end panel has faded in: the game writes its figure at that moment, behind the row that shows it, which is still clear.</summary>
        public static bool PanelReady()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            DailySummary panel = uis != null ? uis.dailySummary : null;
            return panel != null && panel.panel != null && panel.panel.alpha >= 1f;
        }

        /// <summary>True if the panel shows the text; false if it shows another or is not there.</summary>
        public static bool PanelShows(string text)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            DailySummary panel = uis != null ? uis.dailySummary : null;
            return panel != null && panel.incomeLabel != null && panel.incomeLabel.text == text;
        }

        /// <summary>True if the top bar shows the text; false if it shows another or is not there.</summary>
        public static bool BarShows(string text)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            Topbar bar = uis != null ? uis.topBar : null;
            return bar != null && bar.moneyChange != null && bar.moneyChange.text == text;
        }

        // A label as the game writes it: the text, red below nothing and lime from it; on the top bar the arrow that matches.
        private static void Write(TextMeshProUGUI label, float amount, string text, bool arrows)
        {
            if (label.text != text)
            {
                label.text = text;
            }
            Colors colors = InstanceBehavior<GlobalReferences>.Instance != null ? InstanceBehavior<GlobalReferences>.Instance.colors : null;
            if (colors != null)
            {
                label.color = amount < 0f ? colors.lightRed : colors.lime;
            }
            if (arrows)
            {
                SetArrow(label.transform.Find(Negative), amount < 0f);
                SetArrow(label.transform.Find(Positive), amount >= 0f);
                label.gameObject.SetActive(true);
            }
        }

        private static void SetArrow(Transform arrow, bool on)
        {
            if (arrow != null && arrow.gameObject.activeSelf != on)
            {
                arrow.gameObject.SetActive(on);
            }
        }
    }
}
