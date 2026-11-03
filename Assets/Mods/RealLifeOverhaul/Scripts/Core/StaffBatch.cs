using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What a batch does to one person on My Employees' review (a raise for several, hiring several): Ready when it will be
    /// done (or was), otherwise why this one is left out. The reasons are the game's own rules and the mod's, each named.
    /// </summary>
    public enum BatchState : byte
    {
        Ready = 0,
        Rested,     // a raise: one was given lately, the rest is not over
        Waiting,    // a raise: a rival's offer or a departure is waiting on its own page
        AtRate,     // a raise: paid the going rate already
        Unknown,    // a raise: not at one of the player's businesses, the company does not know them
        NoFit,      // a hire: none of their skills is one the business needs
        NotYours,   // a hire: the business is not the player's (a Going Public session) or the person is another player's
        Gone,       // a hire: no longer a candidate (hired, discarded or expired since they were picked)
        Failed      // a hire that threw: the rest still went on
    }

    /// <summary>One person's line on a review: their words, what they are paid now and after, a working day of it, and the state.</summary>
    public struct StaffSheetLine
    {
        public string Id, Name;
        public string RoleKey;        // the game's key for their main skill; the screen puts it in words
        public string Business;       // where they work now (a raise) or are to be hired to (a hire); null for none
        public int Now, Next;         // hundredths an hour: now, and after a raise (the same when left out or hired)
        public int DayNow, DayNext;   // hundredths for a working day of each
        public BatchState State;
    }

    /// <summary>
    /// A batch waiting for the player's yes on My Employees' review sheet: who it reaches with their new pay or business,
    /// who is left out and why, and the totals, with what to do on Give and on Cancel. Game-free; the screens fill it and
    /// the review draws it. Lives only while the screen shows it.
    /// </summary>
    public sealed class StaffSheet
    {
        public bool Hire;                      // hiring several; otherwise a raise for several
        public string Subject;                 // the raise's words ("5 per cent of what each is paid") or the business hired to
        public int Picked;                     // how many were picked
        public StaffSheetLine[] Lines = new StaffSheetLine[0];
        public int Ready, Left;                // reached, and left out
        public long Now, Next, DayNow, DayNext;   // the picked employees' pay an hour and a working day in all, before and after, hundredths
        public Action Give, Cancel;

        /// <summary>True when there is something to give.</summary>
        public bool Any => Ready > 0;
    }

    /// <summary>
    /// The one review sheet My Employees may be showing. It is set when a batch is chosen, read by the screen to draw it,
    /// and gone on Give, Cancel, a change of tab, or when the game's own panel of choices it came from shuts (the app
    /// closes). <see cref="KitShows"/> is the kit's answer to "is the kit drawing My Employees": when it is not, a batch goes
    /// through the game's own question box as before. Cost: two fields; nothing runs by itself.
    /// </summary>
    public static class StaffBatch
    {
        /// <summary>Set by the kit's host: true while it draws My Employees (and its switch is on).</summary>
        public static Func<bool> KitShows;

        /// <summary>The sheet waiting for a yes; null for none.</summary>
        public static StaffSheet Sheet { get; private set; }

        /// <summary>True when a batch should be reviewed on the kit's sheet rather than in the game's question box.</summary>
        public static bool Wanted
        {
            get
            {
                try
                {
                    return KitShows != null && KitShows();
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public static void Open(StaffSheet sheet) => Sheet = sheet;

        /// <summary>Takes the sheet away without doing anything; true when there was one.</summary>
        public static bool Close()
        {
            bool had = Sheet != null;
            Sheet = null;
            return had;
        }

        /// <summary>Gives: the sheet goes first, so a second press cannot give twice; then its Give runs.</summary>
        public static bool Give()
        {
            StaffSheet sheet = Sheet;
            if (sheet == null || !sheet.Any || sheet.Give == null)
            {
                return false;
            }
            Sheet = null;
            sheet.Give();
            return true;
        }

        /// <summary>Cancels: the sheet goes and its Cancel runs; nothing was changed.</summary>
        public static bool Cancel()
        {
            StaffSheet sheet = Sheet;
            Sheet = null;
            sheet?.Cancel?.Invoke();
            return sheet != null;
        }
    }
}
