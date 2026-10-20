using Localizor;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// RLO Texts' own words on screen (the options' status, the tests, the notice suggesting the Lite pack), and the key
    /// a model's words are shown through on the phone (<see cref="Line"/>: "{text}", the words alone). Every key written
    /// out in full (the locale check finds each one). Made when shown.
    /// </summary>
    public static class TextsText
    {
        /// <summary>The key a model's words go through where the game keeps a key and its values (the phone's threads, a spoken opener).</summary>
        public const string Line = "reallife_texts_line";

        public static string T(string key) => key.GetLocalization();

        /// <summary>"the processor": where the model runs without a GPU, for the status.</summary>
        public static string Processor() => T("reallife_texts_processor");

        /// <summary>A line of the browser's page after the ChatGPT sign-in, in the player's language, made safe for the page.</summary>
        public static string Page(string key) => "<p>" + System.Net.WebUtility.HtmlEncode(T(key)) + "</p>";
    }
}
