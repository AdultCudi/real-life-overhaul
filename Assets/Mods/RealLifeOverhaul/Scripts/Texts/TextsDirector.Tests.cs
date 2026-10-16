using System.Collections.Generic;

namespace RealLifeOverhaul.Texts
{
    /// <summary>RLO Texts' entries in Options > Tests: a sample message written now, with what it took or why the written line would be used.</summary>
    public sealed partial class TextsDirector
    {
        /// <summary>
        /// A friend's small talk written by the model now, shown as a notice when it comes back (or why not: no pack, still
        /// loading, switched off, another language, the words refused). Needs a save loaded.
        /// </summary>
        public static void TestSample()
        {
            if (Instance == null || SaveGameManager.Current == null)
            {
                TextsGame.Notice("reallife_texts_test_no_save", null);
                return;
            }
            string key;
            Dictionary<string, string> status = Status(out key);
            if (key != "reallife_texts_status_ready" || !TextsGame.English)
            {
                TextsGame.Notice(TextsGame.English ? key : "reallife_texts_test_language", status, 16f);
                return;
            }
            var r = new TextRequest
            {
                Use = TextUse.People, Key = "test:sample", Intent = "friend.news.good", Speaker = "test", Priority = TextPriority.Now, Seconds = 15.0, Day = TextsGame.Day
            };
            r.Who.Name = "Dana Whitlock";
            r.Who.First = "Dana";
            r.Who.Age = 31;
            r.Who.Job = TextWords.Jobs[13];
            r.Who.District = "Midtown";
            r.Who.Role = TextWords.Stages[3] + ", " + TextWords.Origins[3];
            r.Who.Mood = TextWords.Mood(1);
            r.Who.Style = TextWords.FriendStyle(TextsGame.Day * 7919 + 13, 0, false);
            r.Who.Player = TextsGame.PlayerName;
            r.Who.When = TextsGame.When();
            r.Slots.Set("happening", TextWords.Happening(TextsGame.Day, true));
            double asked = TextClock.Now;
            r.Done = words => TextsGame.Notice(words != null ? "reallife_texts_test_sample_done" : "reallife_texts_test_sample_none",
                new Dictionary<string, string>(2) { { "text", words ?? string.Empty }, { "seconds", (TextClock.Now - asked).ToString("0.0") } }, 16f);
            if (!Hub.Ask(r, asked))
            {
                TextsGame.Notice("reallife_texts_test_sample_none", new Dictionary<string, string>(2) { { "text", string.Empty }, { "seconds", "0" } }, 12f);
            }
        }
    }
}
