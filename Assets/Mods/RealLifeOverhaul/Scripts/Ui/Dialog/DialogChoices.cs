using System;
using System.Collections.Generic;
using Entities;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a dialog told of its answers (DialogAnswers) as the kit's choices, for the docked conversation and the call
    /// card alike: the lines under each of the game's buttons in their colours, and the dialog's own answers past the
    /// game's three, each taken on the line itself or leading to the dialog's next line through the game's own ShowEntry.
    /// Game side. Costs nothing a frame.
    /// </summary>
    internal static class DialogChoices
    {
        /// <summary>The lines the dialog told under one of its buttons; null for none.</summary>
        public static UiConsequence[] Lines(DialogNotes notes, DialogButton button) => notes != null ? Lines(notes.Under(button)) : null;

        /// <summary>A dialog's lines as the choice's coloured lines; null for none.</summary>
        public static UiConsequence[] Lines(IReadOnlyList<DialogCost> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return null;
            }
            var made = new UiConsequence[lines.Count];
            for (int i = 0; i < made.Length; i++)
            {
                // Money paid out (against the player, the figure falling: a price, a fee, a payment) in the red; a risk or a reason in the amber.
                int tone = Math.Sign(lines[i].Tone), change = Math.Sign(lines[i].Change);
                bool loss = lines[i].Tone <= UiConsequence.Loss || (tone < 0 && change < 0);
                made[i] = new UiConsequence { Text = DialogRules.OneLine(lines[i].Text), Tone = loss ? UiConsequence.Loss : tone, Change = change };
            }
            return made;
        }

        /// <summary>An answer of the dialog's own as a numbered choice: its words, its lines, greyed while it cannot be taken.</summary>
        public static UiDecisionChoice Choice(DialogExtra extra) =>
            new UiDecisionChoice { Name = DialogRules.OneLine(extra.Name), Lines = Lines(extra.Lines), Disabled = !extra.Enabled };

        /// <summary>
        /// Takes an answer of the dialog's own in the conversation <paramref name="talk"/> runs: on the line itself, its words
        /// said back in <paramref name="back"/>; or its next line shown by the game (<paramref name="moved"/>). False where it
        /// cannot be taken now, or the dialog's step failed (logged; the line stays as it was).
        /// </summary>
        public static bool Take(DialogExtra extra, DialogController talk, out bool moved, out string back)
        {
            moved = false;
            back = null;
            if (extra == null || !extra.Enabled || talk == null)
            {
                return false;
            }
            try
            {
                if (extra.Here != null)
                {
                    back = extra.Here();
                    return back != null;
                }
                DialogEntry next = extra.Next != null ? extra.Next() : null;
                if (next == null)
                {
                    return false;
                }
                talk.ShowEntry(Guard.Entry(next));   // the dialog's own page, guarded as every page the mod gives the game
                moved = true;
                return true;
            }
            catch (Exception e)
            {
                UiDirector.Logger?.Error(e);
                return false;
            }
        }
    }
}
