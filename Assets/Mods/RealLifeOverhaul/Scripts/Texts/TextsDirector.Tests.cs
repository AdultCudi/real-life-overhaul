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
            TextRequest r = SampleRequest();   // (with Your own AI up, it goes the way every message goes: the service first)
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
