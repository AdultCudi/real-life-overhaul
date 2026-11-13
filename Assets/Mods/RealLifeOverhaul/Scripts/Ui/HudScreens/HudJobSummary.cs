using System.Collections.Generic;
using TMPro;
using UI.DailySummary;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The result popup after a delivery shift or a food delivery (the game's DeliveryJobSummary and FoodDeliverySummary) as
    /// a notice card, the way the day's summary is: the game's popup runs as before, hidden by leaf, and its rows are read
    /// as each fades in (the name and the figure in the colour the game gave it, the last row, the net or the total, as the
    /// large figure), with its own close button pressed for the card's button. These popups are the only readout of tips and
    /// van damage fees. A popup whose rows cannot be read in a few seconds is given back as the game draws it, so the player
    /// is never left without one. Cost: one field read four times a second while the HUD shows, a read of a few rows while a popup is up.
    /// </summary>
    internal static class HudJobSummary
    {
        private const float Seen = 0.5f;        // a row's alpha from which the game has shown it
        private const float GiveUpAfter = 3f;   // s of real time without a readable row before the game's own popup is given back
        private const int Rows = 4;             // rows besides the large figure: a notice card keeps four

        private static readonly UiConceal Held = new UiConceal();
        private static readonly List<TMP_Text> Texts = new List<TMP_Text>(4);
        private static readonly UiHudNoticeModel Card = new UiHudNoticeModel { Rows = new UiInfoRow[Rows], Chips = new string[0], ChipTones = new UiRole[0] };
        private static JobSummary _held;        // the popup now hidden by the kit
        private static JobSummary _given;       // a popup left as the game draws it
        private static Button _close;
        private static float _since;
        private static bool _on;

        /// <summary>The HUD shows: popups are looked for.</summary>
        public static void Begin() => _on = true;

        /// <summary>The HUD is gone: a popup is given back as the game draws it.</summary>
        public static void End()
        {
            if (!_on)
            {
                return;
            }
            _on = false;
            Held.Clear();
            _held = _given = null;
            _close = null;
        }

        /// <summary>The card for the popup the game has up, or null for none (or while no row has shown yet); its words' signature in <paramref name="sig"/>.</summary>
        public static UiHudNoticeModel Look(UI.DailySummary.DailySummary daily, out int sig)
        {
            sig = 0;
            if (!_on || daily == null || GameContract.JobSummaryActive.Field == null || GameContract.JobSummaryRows.Field == null)
            {
                return null;
            }
            var job = GameContract.JobSummaryActive.Field.GetValue(daily) as JobSummary;
            if (job == null)
            {
                _held = _given = null;
                _close = null;
                return null;
            }
            if (job == _given)
            {
                return null;
            }
            if (job != _held)
            {
                _close = UiWidgets.Calling(job, "OnClickClose");
                if (_close == null)
                {
                    _given = job;   // no close button to press from the card: the game's popup stays the game's, so it can be closed
                    return null;
                }
                Held.Prune();
                Held.Add(job.transform);
                Held.Hide();
                _held = job;
                _since = Time.unscaledTime;
            }
            if (!Read(job, out sig))
            {
                if (Time.unscaledTime - _since > GiveUpAfter)
                {
                    Held.Show();   // its rows are not ones this reads: the game's popup, as it draws it
                    _given = job;
                    _held = null;
                }
                return null;
            }
            return Card;
        }

        /// <summary>The card's button: the popup's own close button, as the game's click runs it.</summary>
        public static bool Press() => UiWidgets.Press(_close);

        // The popup's rows that have faded in, in the game's order: all but the last as rows, the last (the net or the total) large.
        private static bool Read(JobSummary job, out int sig)
        {
            sig = 0;
            var groups = GameContract.JobSummaryRows.Field.GetValue(job) as CanvasGroup[];
            if (groups == null)
            {
                return false;
            }
            UiHudNoticeModel c = Card;
            c.Kicker = c.Meta = c.Title = c.Body = c.Figure = c.BigLabel = c.Big = c.BigUnder = c.Footer = c.Link = c.Action = null;
            c.Icon = job is FoodDeliverySummary ? UiIcon.ForkKnife : UiIcon.Truck;
            c.IconTone = UiRole.None;
            c.BigTone = UiRole.OkText;
            c.LinkIcon = UiIcon.None;
            c.RowCount = 0;
            c.ChipCount = 0;
            c.Alpha = 1f;
            c.Kicker = UiText.T(job is FoodDeliverySummary ? "food_delivery_summary" : "delivery_job_summary");
            string bigName = null, bigFigure = null;
            Color bigColour = Color.white;
            sig = c.Kicker.GetHashCode();
            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup row = groups[i];
                if (row == null || !row.gameObject.activeSelf || row.alpha < Seen)
                {
                    continue;
                }
                Texts.Clear();
                row.GetComponentsInChildren(false, Texts);
                TMP_Text name = null, value = null;
                for (int k = 0; k < Texts.Count; k++)
                {
                    if (string.IsNullOrEmpty(Texts[k].text))
                    {
                        continue;
                    }
                    if (name == null)
                    {
                        name = Texts[k];
                    }
                    else
                    {
                        value = Texts[k];
                    }
                }
                if (name == null || value == null)
                {
                    continue;
                }
                if (bigName != null && c.RowCount < Rows)
                {
                    c.Rows[c.RowCount++] = new UiInfoRow { Label = bigName, Value = bigFigure, Ink = HudSides.Tone(bigColour, UiRole.None) };
                }
                bigName = UiWidgets.Text(name);
                bigFigure = UiWidgets.Text(value);
                bigColour = value.color;
                sig = unchecked(sig * 31 + bigName.GetHashCode() + bigFigure.GetHashCode() * 3);
            }
            if (bigName == null)
            {
                return false;
            }
            c.BigLabel = bigName;
            c.Big = bigFigure;
            UiRole tone = HudSides.Tone(bigColour, UiRole.None);
            c.BigTone = tone == UiRole.Danger ? UiRole.DangerText : tone == UiRole.Warn ? UiRole.WarnText : UiRole.OkText;
            c.Action = _close != null ? UiWidgets.Label(_close) ?? UiText.T("common_close") : null;
            sig = unchecked(sig * 31 + c.RowCount + (c.Action != null ? 1 : 0));
            return true;
        }
    }
}
