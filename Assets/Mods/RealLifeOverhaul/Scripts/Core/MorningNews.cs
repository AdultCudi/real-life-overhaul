using System.Collections.Generic;
using Localizor;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's news held while the player sleeps, so that it comes with the morning line as one notification (two if
    /// it is long) and not as a pile in the minute they wake (why it piles up: <see cref="MorningNewsRules"/>).
    ///
    /// Who sends through it: the weather's warnings, forecast and power cuts (Events/Weather), what is on today and
    /// tomorrow (Events/EventDirector), the city government's and the works' notices (Civic/CivicText) and the morning's
    /// papers (Ledger/LedgerDirector.Online). None of them carries a click. Home/HomeDirector opens the hold when the
    /// player lies down (<see cref="Hold"/>) and, once the moment after the wake is over, Home/HomeReport takes what was
    /// held into the morning line (<see cref="Take"/>); when no morning line goes out, each is shown as it came
    /// (<see cref="Release"/>). With the morning summary or its news switched off nothing is held, and every notice is
    /// shown at once, as before.
    ///
    /// GOING PUBLIC. Only this machine's notifications: nothing is sent, and a guest's copies of the host's city
    /// notices are held on the guest's own machine like any other.
    ///
    /// Cost: nothing per frame (no Update). A notice held is one entry of a list of at most
    /// <see cref="MorningNewsRules.MostHeld"/>; its words are looked up once, when the morning line goes out.
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

        /// <summary>True from the player lying down until the morning line takes the news or lets it go. A field read.</summary>
        public static bool Holding { get; private set; }

        /// <summary>How many notices are held now. A field read.</summary>
        public static int Count => Held.Count;

        /// <summary>Whether a sleep's news is held at all: home life, sleep, the morning summary and its news all on.</summary>
        public static bool Wanted => Settings.HomeLife && Settings.HomeSleep && Settings.HomeMorning && Settings.HomeMorningNews;

        /// <summary>
        /// A notice of the city's news: shown at once, as <see cref="Notifications.Show"/> would, or held for the morning
        /// line while the player sleeps. One with the duplicate id of a held one takes its place.
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

        /// <summary>Home: the player has lain down. Holds nothing unless <see cref="Wanted"/>.</summary>
        public static void Hold()
        {
            Holding = Wanted;
        }

        /// <summary>Home: what was held, in the order it came, added to <paramref name="into"/>; the hold is over. Returns how many.</summary>
        public static int Take(List<Notice> into)
        {
            int count = Held.Count;
            into.AddRange(Held);
            Clear();
            return count;
        }

        /// <summary>Home: no morning line goes out, or home life stops: each held notice is shown as it came; the hold is over.</summary>
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

        /// <summary>The city is unloading: what was held goes with it, unshown.</summary>
        public static void Drop()
        {
            Clear();
        }

        /// <summary>A notice shown as its sender would have shown it, with the game's sound.</summary>
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

        /// <summary>The kind of a weight.</summary>
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
