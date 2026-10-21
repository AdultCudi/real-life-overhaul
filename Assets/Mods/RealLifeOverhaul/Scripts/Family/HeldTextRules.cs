using System.Collections.Generic;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// A family text written at night or in a time skip, kept for the morning (FamilyText). It is kept in the family's save
    /// (FamilySave.Held), so a save and quit at night loses none: the morning after the load it goes out as it would have.
    /// Only the player's own save holds it; nothing of it goes over Going Public.
    /// </summary>
    public sealed class HeldText
    {
        public string Contact;                      // the phone contact's id it comes from
        public string Key;                          // the text's locale key
        public Dictionary<string, string> Data;     // what fills it, as it was when written
        public int Day;                             // the day it was written
        public bool Notify = true;                  // false: its sender shows the notice itself (PartnerVoice), so the game's is left out
    }

    /// <summary>
    /// The rules of the texts kept for the morning: kept in the order written, at most <see cref="Most"/> (the oldest let go
    /// first), and each goes out at most <see cref="LateDays"/> day after it was written; a later one, or one from a day still
    /// to come (a hand-edited save), is let go. Game-free (EconomyCheck, FamilyHeldTextChecks.cs). COST: on an event or once an
    /// hour, a pass over at most a dozen.
    /// </summary>
    public static class HeldTextRules
    {
        public const int Most = 12;       // texts kept at most: a night's skip writes a few, and the save stays small
        public const int LateDays = 1;    // days after its own a kept text may still go out: the next morning, not a week on

        /// <summary>A text kept for the morning, at the end; past <see cref="Most"/> the oldest is let go.</summary>
        public static void Keep(List<HeldText> held, HeldText text)
        {
            if (held == null || !Valid(text))
            {
                return;
            }
            held.Add(text);
            while (held.Count > Most)
            {
                held.RemoveAt(0);
            }
        }

        /// <summary>Whether a kept text can still go out today: written today or the day before.</summary>
        public static bool Fresh(HeldText text, int today)
        {
            return Valid(text) && today >= text.Day && today - text.Day <= LateDays;
        }

        /// <summary>
        /// The kept texts that go out today, in the order written; the list is emptied, so none goes twice and the stale are
        /// let go. Null when there is nothing kept.
        /// </summary>
        public static List<HeldText> TakeDue(List<HeldText> held, int today)
        {
            if (held == null || held.Count == 0)
            {
                return null;
            }
            var due = new List<HeldText>(held.Count);
            for (int i = 0; i < held.Count; i++)
            {
                if (Fresh(held[i], today))
                {
                    due.Add(held[i]);
                }
            }
            held.Clear();
            return due;
        }

        /// <summary>After a load: what an older or a hand-edited save left (none, an empty line, too many) set right.</summary>
        public static List<HeldText> Tidy(List<HeldText> held)
        {
            if (held == null)
            {
                return new List<HeldText>();
            }
            held.RemoveAll(h => !Valid(h));
            while (held.Count > Most)
            {
                held.RemoveAt(0);
            }
            for (int i = 0; i < held.Count; i++)
            {
                if (held[i].Data == null)
                {
                    held[i].Data = new Dictionary<string, string>();
                }
            }
            return held;
        }

        private static bool Valid(HeldText text) => text != null && !string.IsNullOrEmpty(text.Contact) && !string.IsNullOrEmpty(text.Key);
    }
}
