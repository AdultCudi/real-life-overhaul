using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Sessions
{
    /// <summary>One choice of a session's step: its name on the game's button, its trade-off said beside it, and the game minutes it adds.</summary>
    public sealed class SessionChoice
    {
        public string NameKey;      // a locale key with no placeholder: the game's panel shows a button's key alone
        public string TradeKey;     // what the choice gives and costs, one short line
        public int Minutes;         // game minutes it adds to the session
    }

    /// <summary>One step of a session: a question and its two or three choices (the game's panel has three keys for them).</summary>
    public sealed class SessionStep
    {
        public string TitleKey;
        public SessionChoice[] Choices;
    }

    /// <summary>What a session is asked about: the business, the day, who holds it and how well (a manager's skill; the player 100).</summary>
    public struct SessionAsk
    {
        public string Place;        // the business, as a place key (PlaceRules.Key)
        public string Name;         // the business's name, for the words
        public string Type;         // its business type
        public int Day, Hour;
        public bool ByManager;
        public string Manager;      // the manager's name when one holds it
        public int Skill;           // 0 to 100
    }

    /// <summary>
    /// What a session did, from the feature's callback: the notice that tells it (a key and its name and value pairs), and
    /// an effect kept with the session's record for so many days (two figures the feature reads back by
    /// <see cref="SessionFile.Effect"/>). A callback that changed nothing says so with Done false: the session stays due.
    /// </summary>
    public struct SessionOutcome
    {
        public bool Done;
        public string Key;
        public string[] Pairs;
        public int A, B;
        public int Days;
    }

    /// <summary>
    /// A kind of hands-on management session held at a desk in a firm's own office (DRAFT2 section 3, "Desks"): where it is
    /// held, the furniture it needs, how often, its steps of choices, how long it takes and what it changes. A feature
    /// makes one and adds it to <see cref="SessionRegistry"/>; the director does the rest. Game-free.
    /// </summary>
    public sealed class SessionDef
    {
        public string Id;                   // saved in each record: never renamed
        public string TitleKey, AboutKey;
        public string[] Types;              // the business types it is held at
        public int EveryDays = 7;           // due again so many days after it was last held
        public int Hour = 10;               // the hour of its day a manager holds it
        public int BaseMinutes;             // game minutes before the choices' own
        public bool Delegable = true;       // a manager the player hires can take it
        public bool OwnDesk = true;         // needs a desk of the owner's own: a computer workstation nobody is rostered at
        public int DesksLeast = 1;          // computer workstations the office must have, the owner's among them
        public SessionStep[] Steps = new SessionStep[0];

        /// <summary>Why it cannot be held at a business now (a key), or null. Null itself: never refused.</summary>
        public Func<SessionAsk, string> Blocked;

        /// <summary>The feature's callback: the choices made, one a step, applied to the business. Must be given.</summary>
        public Func<SessionAsk, int[], SessionOutcome> Apply;

        /// <summary>The sound choice at a step for a business, as a manager judges it (the step's index in, a choice's out); null: the first.</summary>
        public Func<SessionAsk, int, int> Pick;

        public bool HeldAt(string type)
        {
            for (int i = 0; Types != null && i < Types.Length; i++)
            {
                if (string.Equals(Types[i], type, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>What a desk lacks for a session (<see cref="SessionRules.Missing"/>).</summary>
    public enum SessionNeed
    {
        None = 0, Desks = 1, OwnDesk = 2
    }

    /// <summary>
    /// Every kind of session the features add, in the order added: one registry for the whole mod. Filled by the wiring at
    /// the mod's start and emptied when it goes; read on a click, an hour's turn and a page. Game-free.
    /// </summary>
    public static class SessionRegistry
    {
        private static readonly List<SessionDef> Defs = new List<SessionDef>(8);

        public static IReadOnlyList<SessionDef> All => Defs;

        /// <summary>Adds a kind; one with the same id takes the old one's place. False for one that is not whole (no id, no types, no callback, a step of no choice or more than three).</summary>
        public static bool Add(SessionDef def)
        {
            if (!SessionRules.Whole(def))
            {
                return false;
            }
            for (int i = 0; i < Defs.Count; i++)
            {
                if (Defs[i].Id == def.Id)
                {
                    Defs[i] = def;
                    return true;
                }
            }
            Defs.Add(def);
            return true;
        }

        public static void Remove(string id)
        {
            for (int i = Defs.Count - 1; i >= 0; i--)
            {
                if (Defs[i].Id == id)
                {
                    Defs.RemoveAt(i);
                }
            }
        }

        public static SessionDef Find(string id)
        {
            for (int i = 0; i < Defs.Count; i++)
            {
                if (Defs[i].Id == id)
                {
                    return Defs[i];
                }
            }
            return null;
        }

        /// <summary>The kinds held at a business type, in the order added, into a list of the caller's.</summary>
        public static void For(string type, List<SessionDef> into)
        {
            into.Clear();
            for (int i = 0; i < Defs.Count; i++)
            {
                if (Defs[i].HeldAt(type))
                {
                    into.Add(Defs[i]);
                }
            }
        }

        /// <summary>True if any kind is held at a business type: a look at a building's type before anything else.</summary>
        public static bool Any(string type)
        {
            for (int i = 0; i < Defs.Count; i++)
            {
                if (Defs[i].HeldAt(type))
                {
                    return true;
                }
            }
            return false;
        }

        public static void Clear() => Defs.Clear();
    }

    /// <summary>
    /// The sessions' rules: when one is due, what a desk lacks, how long the choices make it, how a manager decides and how
    /// well, when a manager holds it and when the player is reminded. Whole numbers and seeded rolls only, so every machine
    /// decides the same. Game-free: EconomyCheck's SessionChecks hold it.
    /// </summary>
    public static class SessionRules
    {
        public const int StepChoicesMost = 3;      // the game's activity panel takes three keys for choices beside Cancel
        public const int RemindHour = 9;           // the hour of a day the one reminder of the sessions waiting may go
        public const int ManagerLeast = 500;       // thousandths of the owner's strength a manager of no skill brings to a session; all of it at 100
        public const int TaskBase = 100;           // the managers' rolls of a session kind: its place in the registry after the managers' own kinds (ManagerTask)
        public const int Never = -1;               // a record's day never yet

        /// <summary>A kind fit to be held: an id, a type, the callback, every step with one to three choices.</summary>
        public static bool Whole(SessionDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id) || def.Id.IndexOfAny(Reserved) >= 0 || def.Types == null || def.Types.Length == 0 || def.Apply == null
                || def.EveryDays < 1 || def.Steps == null || def.DesksLeast < 0)
            {
                return false;
            }
            for (int i = 0; i < def.Steps.Length; i++)
            {
                SessionStep s = def.Steps[i];
                if (s == null || s.Choices == null || s.Choices.Length == 0 || s.Choices.Length > StepChoicesMost)
                {
                    return false;
                }
            }
            return true;
        }

        private static readonly char[] Reserved = { '#', '^', '|', '%' };

        /// <summary>Due: never held, or held so many days ago or more.</summary>
        public static bool Due(int held, int day, int every)
        {
            return held < 0 || day - held >= Math.Max(1, every);
        }

        /// <summary>Days until it is due again; 0 when due now.</summary>
        public static int DaysToDue(int held, int day, int every)
        {
            return Due(held, day, every) ? 0 : held + Math.Max(1, every) - day;
        }

        /// <summary>
        /// What an office lacks for a kind: enough computer workstations (with their chairs), then one of them the owner's own
        /// (nobody rostered at it), so a session needs real furniture. <paramref name="missing"/>: the workstations short.
        /// </summary>
        public static SessionNeed Missing(SessionDef def, int desks, int freeDesks, out int missing)
        {
            missing = Math.Max(0, def.DesksLeast - Math.Max(0, desks));
            if (missing > 0)
            {
                return SessionNeed.Desks;
            }
            return def.OwnDesk && freeDesks <= 0 ? SessionNeed.OwnDesk : SessionNeed.None;
        }

        /// <summary>The game minutes a session takes with its choices (one a step; an index out of range counts as the first).</summary>
        public static int Minutes(SessionDef def, int[] choices)
        {
            long m = Math.Max(0, def.BaseMinutes);
            for (int i = 0; i < def.Steps.Length; i++)
            {
                SessionChoice[] c = def.Steps[i].Choices;
                int at = choices != null && i < choices.Length && choices[i] >= 0 && choices[i] < c.Length ? choices[i] : 0;
                m += Math.Max(0, c[at].Minutes);
            }
            return (int)Math.Max(1L, Math.Min(24L * 60, m));
        }

        /// <summary>How much of the owner's strength a holder brings, thousandths: the player all of it, a manager half at no skill rising to all at 100.</summary>
        public static int Strength(bool byManager, int skill)
        {
            if (!byManager)
            {
                return 1000;
            }
            int s = Math.Max(0, Math.Min(100, skill));
            return ManagerLeast + (1000 - ManagerLeast) * s / 100;
        }

        /// <summary>
        /// A manager's choices, one a step: the kind's sound choice, or with a mistake (the managers' own chance for their
        /// skill, ManagerRules.MistakeChance) the next choice round. Rolled from the save's seed, the day, the business and
        /// the kind, so a reload and every machine give the same. Returns the steps decided by mistake.
        /// </summary>
        public static int ManagerChoices(SessionDef def, SessionAsk ask, int seed, int kindIndex, int[] into)
        {
            int mistakes = 0;
            int chance = ManagerRules.MistakeChance(ask.Skill, 0);
            for (int i = 0; i < def.Steps.Length && i < into.Length; i++)
            {
                int n = def.Steps[i].Choices.Length;
                int sound = def.Pick != null ? def.Pick(ask, i) : 0;
                sound = sound >= 0 && sound < n ? sound : 0;
                bool mistake = n > 1 && ManagerRules.Roll(seed, ask.Day, ask.Place, TaskBase + kindIndex, "step" + i) < chance;
                into[i] = mistake ? (sound + 1) % n : sound;
                mistakes += mistake ? 1 : 0;
            }
            return mistakes;
        }

        /// <summary>The owner's own choices when the panel cannot be seen (a Going Public session hides it): the sound one at each step.</summary>
        public static void SoundChoices(SessionDef def, SessionAsk ask, int[] into)
        {
            for (int i = 0; i < def.Steps.Length && i < into.Length; i++)
            {
                int n = def.Steps[i].Choices.Length;
                int sound = def.Pick != null ? def.Pick(ask, i) : 0;
                into[i] = sound >= 0 && sound < n ? sound : 0;
            }
        }

        /// <summary>A manager holds a due session from its hour of the day on (a sleep past the hour holds it at the next look).</summary>
        public static bool ManagerHolds(bool delegated, bool managerThere, bool due, int hour, int sessionHour)
        {
            return delegated && managerThere && due && hour >= sessionHour;
        }

        /// <summary>The one reminder of a day: something waits, not yet reminded today, from the reminder's hour on.</summary>
        public static bool Remind(int waiting, int remindedDay, int day, int hour)
        {
            return waiting > 0 && remindedDay != day && hour >= RemindHour;
        }

        /// <summary>The day an effect lasts until (not counting it): from today for so many days, today itself the first.</summary>
        public static int Until(int day, int days)
        {
            return days <= 0 ? day : day + days;
        }
    }
}
