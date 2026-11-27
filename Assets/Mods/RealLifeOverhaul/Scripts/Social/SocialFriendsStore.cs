using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the game of meeting the friends keeps in a save (<c>reallife:social_friends</c>): for each person who has let something slip, been
    /// brought friends or been quizzed about, which things the player has heard, when, and how the quizzes went; and whether the first hint's notice
    /// was shown. Their things themselves are never saved (<see cref="SocialInterests"/>). A save made before it has no record and loads as it
    /// always did; a record from a later version this one cannot read is written back as it was read. Game-free.
    /// Line one: <c>version|told</c>. Then one line a person: <c>slot|name|interest seed|heard|hint day|intro day|quizzes|best</c>; a person who is
    /// not in that slot by that name any more (they were let go) is left out, so nobody takes over somebody's hints. New fields go at the end.
    /// </summary>
    public static class SocialFriendsStore
    {
        public const string Key = "reallife:social_friends";
        public const int ToldHint = 1;      // bit of Told: the notice of the first hint was shown
        private const int Version = 1;
        private const char Field = '|';
        private const char Record = '\n';

        /// <summary>Which notices were shown once: <see cref="ToldHint"/>.</summary>
        public static int Told;

        private static string _foreign;     // a later version's record, kept as it was read

        public static void Reset()
        {
            Told = 0;
            _foreign = null;
        }

        /// <summary>Whether a person has anything to keep.</summary>
        private static bool Keeps(SocialPerson p)
        {
            return p != null && (p.Heard != 0 || p.InterestHintDay != 0 || p.IntroDay != 0 || p.Quizzes != 0);
        }

        public static void Save(Dictionary<string, string> data, SocialPerson[] people, int count)
        {
            if (_foreign != null)
            {
                data[Key] = _foreign;
                return;
            }
            var text = new StringBuilder(256);
            bool any = false;
            for (int i = 0; i < count && i < people.Length; i++)
            {
                SocialPerson p = people[i];
                if (!Keeps(p))
                {
                    continue;
                }
                any = true;
                text.Append(Record).Append(p.Index).Append(Field).Append(Clean(p.Name)).Append(Field).Append(SocialInterests.SeedOf(p)).Append(Field).Append(p.Heard)
                    .Append(Field).Append(p.InterestHintDay).Append(Field).Append(p.IntroDay).Append(Field).Append(p.Quizzes).Append(Field).Append(p.QuizBest);
            }
            if (!any && Told == 0)
            {
                data.Remove(Key);
                return;
            }
            data[Key] = Version.ToString(CultureInfo.InvariantCulture) + Field + Told.ToString(CultureInfo.InvariantCulture) + text;
        }

        public static void Load(Dictionary<string, string> data, SocialPerson[] people, int count)
        {
            Reset();
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return;
            }
            try
            {
                string[] lines = text.Split(Record);
                string[] head = lines[0].Split(Field);
                if (head.Length < 2 || Int(head[0]) != Version)
                {
                    _foreign = text;   // a version this one does not know: left as it is
                    return;
                }
                Told = Int(head[1]);
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] f = lines[i].Split(Field);
                    if (f.Length < 8)
                    {
                        continue;
                    }
                    int index = Int(f[0]);
                    SocialPerson p = index >= 0 && index < count && index < people.Length ? people[index] : null;
                    if (p == null || !string.Equals(p.Name, f[1], StringComparison.Ordinal))
                    {
                        continue;
                    }
                    p.InterestSeed = Int(f[2]);
                    p.Heard = Int(f[3]) & SocialInterests.AllHeard;
                    p.InterestHintDay = Math.Max(0, Int(f[4]));
                    p.IntroDay = Math.Max(0, Int(f[5]));
                    p.Quizzes = Math.Max(0, Int(f[6]));
                    p.QuizBest = Math.Max(0, Math.Min(SocialFriendsRules.Questions, Int(f[7])));
                }
            }
            catch (Exception)
            {
                Reset();   // a record that cannot be read: nobody has heard anything, and what they like is rolled from their seed as ever
            }
        }

        private static string Clean(string name)
        {
            return string.IsNullOrEmpty(name) ? string.Empty : name.Replace(Field, '/').Replace(Record, ' ').Replace('\r', ' ');
        }

        private static int Int(string text)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }
    }
}
