using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BigAmbitions.InputSystem;
using Extensions;
using Helpers;
using Localizor;
using PlayerActivity;
using UI.Elements;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's activity panel read as the kit's words: a label in its own language, a key of the game's on its cap as
    /// the game names it (or a pad's glyph), a line's colour as a role, the glyph an activity reads as, and the state the
    /// kit draws. Game side. Cost: a few reads at a layout.
    /// </summary>
    internal static class ActivityRead
    {
        /// <summary>A label's words: its key put into words with its arguments, or the words it carries as they are.</summary>
        public static string Words(LabelInfo l)
        {
            if (l == null || string.IsNullOrEmpty(l.key))
            {
                return string.Empty;
            }
            return l.localize ? LocalizorManager.GetLocalization(l.key, l.arguments) : l.key;
        }

        /// <summary>A skill of the game's by its name: the game's own words for its key, else the key as it stands.</summary>
        public static string SkillName(string skill) => string.IsNullOrEmpty(skill) ? string.Empty : LocalizorManager.IsLocalizedKey(skill) ? skill.GetLocalization() : skill;

        /// <summary>A key and its arguments in words, as the panel's slider and bar write theirs.</summary>
        public static string Words(string key, object arguments) => string.IsNullOrEmpty(key) ? string.Empty : LocalizorManager.GetLocalization(key, arguments);

        /// <summary>A button's words.</summary>
        public static string Words(ButtonInfo b) => b == null || string.IsNullOrEmpty(b.key) ? string.Empty : LocalizorManager.GetLocalization(b.key, b.arguments);

        /// <summary>
        /// A slider end's words with the key that moves the slider there beside them (the key's name before the left end's,
        /// after the right's); the words alone where there is no name.
        /// </summary>
        public static string Capped(string key, string words, bool after)
        {
            if (string.IsNullOrEmpty(key))
            {
                return words;
            }
            return string.IsNullOrEmpty(words) ? key : after ? words + "  " + key : key + "  " + words;
        }

        /// <summary>
        /// The game's key for an action on a cap: a pad's glyph for the four the pad's face buttons are while a pad leads,
        /// the key's name as the game's own labels print it otherwise.
        /// </summary>
        public static UiGameKey Key(PlayerAction action)
        {
            UiPad pad = UiGame.Instance.Pad;
            UiAction kit = Kit(action);
            if (pad != UiPad.None && kit != UiAction.Count)
            {
                UiIcon glyph = UiKeyGlyphs.For(kit, pad);
                if (glyph != UiIcon.None)
                {
                    return new UiGameKey { Glyph = glyph };
                }
            }
            try
            {
                return new UiGameKey { Name = Name(action) };
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                return default(UiGameKey);   // a game without its bindings' names: no cap, the words alone
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string Name(PlayerAction action) => action.GetDisplayName();

        // The kit's action a game action is on the pad (its face buttons as UiGame reads them), or Count.
        private static UiAction Kit(PlayerAction action)
        {
            switch (action)
            {
                case PlayerAction.Confirm: return UiAction.Confirm;
                case PlayerAction.Cancel: return UiAction.Back;
                case PlayerAction.Interact: return UiAction.Alt;
                case PlayerAction.SecondaryInteract: return UiAction.Option;
                default: return UiAction.Count;
            }
        }

        /// <summary>A line's colour as the kit's role: the game's green a gain, its red a loss, its yellow a cost; white the words' own.</summary>
        public static UiRole Ink(Color32 c)
        {
            if (c.g > c.r + 40 && c.g >= c.b)
            {
                return UiRole.OkText;
            }
            if (c.r > c.g + 60 && c.r > c.b + 60)
            {
                return UiRole.DangerText;
            }
            if (c.r > 150 && c.g > 100 && c.b < 100)
            {
                return UiRole.WarnText;
            }
            return UiRole.None;
        }

        /// <summary>
        /// The panel's lines as rows: "Pay: $16 an hour" a label and its value, a line with no colon read across in its
        /// colour; the first <paramref name="skip"/> lines left out (a line the card already says).
        /// </summary>
        public static UiInfoRow[] Rows(LabelInfo[] labels, int skip = 0)
        {
            if (labels == null || labels.Length <= skip)
            {
                return new UiInfoRow[0];
            }
            var rows = new List<UiInfoRow>(labels.Length);
            for (int i = skip; i < labels.Length; i++)
            {
                string words = Words(labels[i]);
                if (string.IsNullOrEmpty(words))
                {
                    continue;
                }
                UiRole ink = labels[i] != null ? Ink(labels[i].color) : UiRole.None;
                int colon = words.IndexOf(": ", StringComparison.Ordinal);
                if (colon > 0 && colon < words.Length - 2)
                {
                    rows.Add(new UiInfoRow { Label = words.Substring(0, colon), Value = words.Substring(colon + 2), Ink = ink });
                }
                else
                {
                    rows.Add(new UiInfoRow { Label = words, Ink = ink });
                }
            }
            return rows.ToArray();
        }

        /// <summary>The kit's state for the game's.</summary>
        public static UiActivityState State(PlayerActivityState s)
        {
            switch (s)
            {
                case PlayerActivityState.MovingTowardsActivity: return UiActivityState.Moving;
                case PlayerActivityState.Waiting: return UiActivityState.Waiting;
                case PlayerActivityState.Started: return UiActivityState.Started;
                case PlayerActivityState.Running: return UiActivityState.Running;
                case PlayerActivityState.Finished: return UiActivityState.Finished;
                default: return UiActivityState.NotStarted;
            }
        }

        /// <summary>The glyph an activity reads as, by what it is: a bed's moon, a desk's briefcase, a class's cap, a stove's fork.</summary>
        public static UiIcon Icon(IPlayerActivity a)
        {
            string name = a != null ? a.GetType().Name : string.Empty;
            if (Has(name, "Sleep"))
            {
                return UiIcon.Moon;
            }
            if (Has(name, "Rest") || Has(name, "Sit"))
            {
                return Has(name, "Sitting") ? UiIcon.Cap : UiIcon.Sofa;
            }
            if (Has(name, "Study") || Has(name, "School") || Has(name, "Class") || Has(name, "Course") || Has(name, "Exam"))
            {
                return UiIcon.Cap;
            }
            if (Has(name, "Session") || Has(name, "Meeting"))
            {
                return UiIcon.Presentation;
            }
            if (Has(name, "Cook") || Has(name, "Eat") || Has(name, "Meal") || Has(name, "Food"))
            {
                return UiIcon.ForkKnife;
            }
            if (Has(name, "Coffee"))
            {
                return UiIcon.Coffee;
            }
            if (Has(name, "Exercise") || Has(name, "Gym") || Has(name, "Workout") || Has(name, "Train"))
            {
                return UiIcon.Heartbeat;
            }
            if (Has(name, "Shower") || Has(name, "Bath") || Has(name, "Toilet"))
            {
                return UiIcon.Drop;
            }
            if (Has(name, "Bus") || Has(name, "Ride") || Has(name, "Taxi"))
            {
                return UiIcon.Train;
            }
            if (Has(name, "Computer") || Has(name, "Code"))
            {
                return UiIcon.Cpu;
            }
            if (Has(name, "Read") || Has(name, "Book"))
            {
                return UiIcon.Newspaper;
            }
            if (Has(name, "Tidy") || Has(name, "Clean") || Has(name, "Home"))
            {
                return UiIcon.Home;
            }
            if (Has(name, "Wait") || Has(name, "Interview") || Has(name, "Visit"))
            {
                return UiIcon.Clock;
            }
            if (Has(name, "Paid") || Has(name, "Work") || Has(name, "Shift") || Has(name, "Desk") || Has(name, "Job") || Has(name, "Hustle") || Has(name, "Vendor")
                || Has(name, "Stint") || Has(name, "Skip"))
            {
                return UiIcon.Briefcase;
            }
            return UiIcon.Clock;
        }

        private static bool Has(string name, string part) => name.IndexOf(part, StringComparison.Ordinal) >= 0;
    }
}
