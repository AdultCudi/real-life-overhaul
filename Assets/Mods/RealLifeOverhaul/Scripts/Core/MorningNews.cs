using System.Collections.Generic;
using Localizor;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's news held while the player sleeps, so it comes with the morning line instead of as a pile on waking.
    /// HomeDirector opens the hold (<see cref="Hold"/>); HomeReport takes it into the morning line (<see cref="Take"/>)
    /// or shows each as it came (<see cref="Release"/>). Going Public: this machine's notifications only, nothing is
    /// sent. Nothing per frame; at most <see cref="MorningNewsRules.MostHeld"/> notices are held.
    /// </summary>
    public static class MorningNews
    {
        /// <summary>One notice as its sender gave it.</summary>
        public struct Notice
        {
            public NotificationType Type;
            public string Key;
            public Dictionary<string, string> Data;
            public float Seconds;
            public string Same;   // the game's duplicate id: a newer notice with the same one replaces this
            public bool Track;    // kept in the save's list of notifications, as its sender asked
        }

        private static readonly List<Notice> Held = new List<Notice>(MorningNewsRules.MostHeld);
        private static readonly List<string> HeldSame = new List<string>(MorningNewsRules.MostHeld);

        /// <summary>True from the player lying down until the morning line takes the news or lets it go.</summary>
        public static bool Holding { get; private set; }

        public static int Count => Held.Count;

        /// <summary>Whether a sleep's news is held at all: home life, sleep, the morning summary and its news all on.</summary>
        public static bool Wanted => Settings.HomeLife && Settings.HomeSleep && Settings.HomeMorning && Settings.HomeMorningNews;

        /// <summary>
        /// Shows a notice at once, or holds it for the morning line while the player sleeps; one with the duplicate id
        /// of a held one takes its place.
        /// </summary>
        public static void Show(NotificationType type, string key, Dictionary<string, string> data, float seconds, string same = null, bool track = true)
        {
            var notice = new Notice { Type = type, Key = key, Data = data, Seconds = seconds, Same = same, Track = track };
            if (!Holding || !Wanted)
            {
                ShowAsCame(notice);
                return;
            }
            int at = MorningNewsRules.Same(HeldSame, HeldSame.Count, same);
            if (at >= 0)
            {
                Held[at] = notice;
                return;
            }
            if (Held.Count >= MorningNewsRules.MostHeld)
            {
                ShowAsCame(notice);
                return;
            }
            Held.Add(notice);
            HeldSame.Add(same);
        }

        /// <summary>The player has lain down; holds nothing unless <see cref="Wanted"/>.</summary>
        public static void Hold()
        {
            Holding = Wanted;
        }

        /// <summary>Adds what was held, in order, to <paramref name="into"/> and ends the hold; returns how many.</summary>
        public static int Take(List<Notice> into)
        {
            int count = Held.Count;
            into.AddRange(Held);
            Clear();
            return count;
        }

        /// <summary>No morning line goes out: shows each held notice as it came and ends the hold.</summary>
        public static void Release()
        {
            int count = Held.Count;
            if (count == 0)
            {
                Holding = false;
                return;
            }
            var shown = new List<Notice>(Held);
            Clear();
            for (int i = 0; i < count; i++)
            {
                ShowAsCame(shown[i]);
            }
        }

        /// <summary>The city is unloading: what was held goes unshown.</summary>
        public static void Drop()
        {
            Clear();
        }

        public static void ShowAsCame(Notice notice)
        {
            Notifications.Show(notice.Type, notice.Key, notice.Data, notice.Seconds, notice.Same, null, true, notice.Track);
        }

        /// <summary>A notice's words in the player's language, its placeholders filled from its data. Builds a string.</summary>
        public static string Words(Notice notice)
        {
            string text = notice.Key.GetLocalization();
            if (notice.Data != null)
            {
                foreach (KeyValuePair<string, string> pair in notice.Data)
                {
                    text = text.Replace("{" + pair.Key + "}", pair.Value ?? string.Empty);
                }
            }
            return text;
        }

        /// <summary>A kind's weight for <see cref="MorningNewsRules.Strongest"/>: a success 0, news 1, a warning 2, an error 3.</summary>
        public static int Weight(NotificationType type) =>
            type == NotificationType.Error ? 3 : type == NotificationType.Warning ? 2 : type == NotificationType.Info ? 1 : 0;

        public static NotificationType OfWeight(int weight) =>
            weight >= 3 ? NotificationType.Error : weight == 2 ? NotificationType.Warning : weight == 1 ? NotificationType.Info : NotificationType.Success;

        private static void Clear()
        {
            Held.Clear();
            HeldSame.Clear();
            Holding = false;
        }
    }
}
