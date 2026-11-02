using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Entities;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>Which of a line's buttons a cost line stands under.</summary>
    public enum DialogButton : byte
    {
        Confirm = 0,
        Second = 1,
        Cancel = 2
    }

    /// <summary>A line under an answer: what it pays or costs, for the player (+1), against them (-1) or neither (0), and which way a figure goes.</summary>
    public struct DialogCost
    {
        public string Text;
        public int Tone;
        public int Change;

        public DialogCost(string text, int tone, int change = 0)
        {
            Text = text;
            Tone = tone;
            Change = change;
        }
    }

    /// <summary>
    /// An answer past the game's three buttons: its words, its lines, whether it can be taken now, and what it does: the
    /// line it leads to (<see cref="Next"/>), or something done on the line itself (<see cref="Here"/>), which returns the
    /// words said back, shown in the thread, and keeps the line and its form as they are.
    /// </summary>
    public sealed class DialogExtra
    {
        public string Name;
        public DialogCost[] Lines;
        public bool Enabled = true;
        public Func<DialogEntry> Next;
        public Func<string> Here;
    }

    /// <summary>What a dialog told of one line's answers: the lines under each of its buttons and the answers past them.</summary>
    public sealed class DialogNotes
    {
        internal readonly List<DialogCost>[] Costs = { new List<DialogCost>(2), new List<DialogCost>(2), new List<DialogCost>(2) };
        internal readonly List<DialogExtra> Extras = new List<DialogExtra>(2);

        /// <summary>The lines under one of the line's buttons, in the order the dialog gave them.</summary>
        public IReadOnlyList<DialogCost> Under(DialogButton button) => Costs[(int)button];

        /// <summary>The answers past the game's three, in the order the dialog gave them.</summary>
        public IReadOnlyList<DialogExtra> More => Extras;
    }

    /// <summary>
    /// What a dialog tells of its answers beyond what the game's line holds: a line or two under each of its buttons (what
    /// it pays, what it costs, why it is greyed) and answers past the game's three, each leading to the dialog's own next
    /// line. Kept beside the line itself, so it goes when the line goes. The RLO interface's conversation reads it; the
    /// game's own panel, with its three buttons and no lines under them, shows the line as it always has. A dialog states a
    /// cost from the same figure its words and its deal use, so what is said and what is done never differ. Game side.
    /// Cost: nothing a frame; one lookup when the conversation reads a new line.
    /// </summary>
    public static class DialogAnswers
    {
        /// <summary>Lines under one answer at the most: the conversation's row holds this many under its name.</summary>
        public const int LinesMost = 3;

        /// <summary>Answers past the game's three at the most, so every answer keeps a number key.</summary>
        public const int ExtrasMost = 5;

        private static readonly ConditionalWeakTable<DialogEntry, DialogNotes> Kept = new ConditionalWeakTable<DialogEntry, DialogNotes>();

        /// <summary>A line under one of the line's buttons. Returns the line, so a page is told as it is made.</summary>
        public static DialogEntry Cost(this DialogEntry entry, DialogButton button, string text, int tone, int change = 0)
        {
            if (entry == null || string.IsNullOrEmpty(text))
            {
                return entry;
            }
            List<DialogCost> lines = Notes(entry).Costs[(int)button];
            if (lines.Count < LinesMost)
            {
                lines.Add(new DialogCost(text, tone, change));
            }
            return entry;
        }

        /// <summary>
        /// A line in the mod's words under one of the line's buttons, <paramref name="key"/> filled with its pairs: a sum the
        /// player hands over (against them, its arrow down). <see cref="Gets"/> is a sum they receive (for them, its arrow up),
        /// <see cref="Says"/> what happens, neither. Each returns the line, so a dialog states its cost from the same figure
        /// its words use.
        /// </summary>
        public static DialogEntry Pays(this DialogEntry entry, DialogButton button, string key, params string[] pairs) => entry.Cost(button, FinanceText.Fill(key, pairs), -1, -1);

        /// <summary>A sum the player receives, under one of the line's buttons (for them, its arrow up). Returns the line.</summary>
        public static DialogEntry Gets(this DialogEntry entry, DialogButton button, string key, params string[] pairs) => entry.Cost(button, FinanceText.Fill(key, pairs), 1, 1);

        /// <summary>What an answer does, neither for nor against the player, under one of the line's buttons. Returns the line.</summary>
        public static DialogEntry Says(this DialogEntry entry, DialogButton button, string key, params string[] pairs) => entry.Cost(button, FinanceText.Fill(key, pairs), 0);

        /// <summary>
        /// An answer past the game's three: its words, the line it leads to (the dialog's own, made when it is taken), whether
        /// it can be taken now and the lines under it. Returns the line.
        /// </summary>
        public static DialogEntry Extra(this DialogEntry entry, string name, Func<DialogEntry> next, bool enabled, params DialogCost[] lines)
        {
            return Add(entry, new DialogExtra { Name = name, Next = next, Enabled = enabled && next != null, Lines = lines });
        }

        /// <summary>An answer past the game's three that is answered on the line itself; <paramref name="here"/> returns what is said back. Returns the line.</summary>
        public static DialogEntry ExtraHere(this DialogEntry entry, string name, Func<string> here, bool enabled, params DialogCost[] lines)
        {
            return Add(entry, new DialogExtra { Name = name, Here = here, Enabled = enabled && here != null, Lines = lines });
        }

        /// <summary>The answers past the game's three told so far forgotten, so a line read again is told afresh. Returns the line.</summary>
        public static DialogEntry NoExtras(this DialogEntry entry)
        {
            DialogNotes notes = Of(entry);
            notes?.Extras.Clear();
            return entry;
        }

        /// <summary>The lines under one of the line's buttons forgotten, so they are told afresh. Returns the line.</summary>
        public static DialogEntry NoCosts(this DialogEntry entry, DialogButton button)
        {
            DialogNotes notes = Of(entry);
            notes?.Costs[(int)button].Clear();
            return entry;
        }

        /// <summary>What was told of a line, or null for nothing.</summary>
        public static DialogNotes Of(DialogEntry entry)
        {
            return entry != null && Kept.TryGetValue(entry, out DialogNotes notes) ? notes : null;
        }

        private static DialogEntry Add(DialogEntry entry, DialogExtra extra)
        {
            if (entry == null || string.IsNullOrEmpty(extra.Name))
            {
                return entry;
            }
            DialogNotes notes = Notes(entry);
            if (notes.Extras.Count < ExtrasMost)
            {
                notes.Extras.Add(extra);
            }
            return entry;
        }

        private static DialogNotes Notes(DialogEntry entry)
        {
            return Kept.GetValue(entry, _ => new DialogNotes());
        }
    }
}
