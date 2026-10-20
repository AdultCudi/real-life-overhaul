using System;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// The services Your own AI can send a card to, in the order of the options page's list (Settings.TextsService saves
    /// the number: new ones only ever go at the end). GitHub Copilot is not among them: GitHub documents no way for an
    /// app of its own to call Copilot over the web, and signing in as another app (an editor's or a partner's own sign-in)
    /// would be passing the mod off as that app. Claude subscriptions are never offered: Anthropic's terms forbid their
    /// sign-in in other apps; Anthropic is reached only with an API key.
    /// </summary>
    public enum TextProvider
    {
        Off = 0,
        ChatGpt = 1,       // Sign in with ChatGPT: the player's own ChatGPT plan, through OpenAI's sign-in for open-source apps
        OpenAi = 2,
        Anthropic = 3,
        Gemini = 4,
        OpenRouter = 5,
        Groq = 6,
        Mistral = 7,
        Cerebras = 8,
        OpenCodeZen = 9,
        Ollama = 10,
        LmStudio = 11
    }

    /// <summary>How a service is spoken to.</summary>
    public enum TextApi
    {
        Chat,        // the OpenAI-compatible chat API (POST {base}/chat/completions)
        Messages,    // Anthropic's Messages API (POST {base}/messages)
        Responses    // OpenAI's Responses API, streamed, as the ChatGPT plan's sign-in requires (POST {base}/responses)
    }

    /// <summary>How a service lets the mod in.</summary>
    public enum TextAccess
    {
        Key,         // an API key the player types
        SignIn,      // the player signs in in the browser (ChatGPT)
        Local        // a program on this PC, on 127.0.0.1: nothing to type
    }

    /// <summary>One service: where it is, how it is spoken to, its model when the player names none, and its quirks.</summary>
    public sealed class TextProviderInfo
    {
        public TextProvider Id;
        public string Name;              // as the log and the notices name it (a product's own name, the same in every language)
        public TextApi Api;
        public TextAccess Access;
        public string Base;              // the API's base address, without the last part of the path
        public string Model;             // the model asked when the player names none (null: the first the service lists)
        public string TokensField = "max_tokens";   // the chat API's name for the reply's length ("max_completion_tokens" for OpenAI's own)
        public bool Temperature = true;  // the service takes a sampling temperature for this kind of model
        public string Reasoning;         // a low reasoning effort sent to the models that think first (null: none)
        public string ReasoningFor;      // ...only to models whose name holds this (null: every model)
        public bool Titled;              // the service is told the app's name in its own header (OpenRouter's X-Title, for its list of apps)
    }

    /// <summary>
    /// The table of services (<see cref="TextProvider"/>), checked against each service's own documentation in October
    /// 2026: base addresses, the default models (each a free or the cheapest fast model of that service, the
    /// player can name any other), and what each one refuses. Game-free (EconomyCheck, TextsServiceChecks.cs).
    /// </summary>
    public static class TextProviders
    {
        public const string ChatGptAuth = "https://auth.openai.com";          // OpenAI's sign-in (Sign in with ChatGPT)
        public const string OpenAiApi = "https://api.openai.com/v1";          // the API the ChatGPT plan's tokens are for, and their "resource"

        private static readonly TextProviderInfo[] All =
        {
            new TextProviderInfo { Id = TextProvider.Off, Name = "Off" },
            new TextProviderInfo
            {
                Id = TextProvider.ChatGpt, Name = "ChatGPT", Api = TextApi.Responses, Access = TextAccess.SignIn, Base = OpenAiApi, Model = null,
                Temperature = false, Reasoning = "low"   // (the plan's route takes no temperature and no length: OpenAI's preview limits)
            },
            new TextProviderInfo
            {
                Id = TextProvider.OpenAi, Name = "OpenAI", Api = TextApi.Chat, Access = TextAccess.Key, Base = OpenAiApi, Model = "gpt-5.6-luna",
                TokensField = "max_completion_tokens", Temperature = false, Reasoning = "low", ReasoningFor = "gpt-"
            },
            new TextProviderInfo
            {
                Id = TextProvider.Anthropic, Name = "Anthropic", Api = TextApi.Messages, Access = TextAccess.Key, Base = "https://api.anthropic.com/v1",
                Model = "claude-haiku-5-5", Temperature = false, Reasoning = "low", ReasoningFor = "claude-haiku-5"
            },
            new TextProviderInfo
            {
                Id = TextProvider.Gemini, Name = "Google Gemini", Api = TextApi.Chat, Access = TextAccess.Key, Base = "https://generativelanguage.googleapis.com/v1beta/openai",
                Model = "gemini-3.8-flash", Reasoning = "minimal", ReasoningFor = "gemini-"
            },
            new TextProviderInfo
            {
                Id = TextProvider.OpenRouter, Name = "OpenRouter", Api = TextApi.Chat, Access = TextAccess.Key, Base = "https://openrouter.ai/api/v1",
                Model = "google/gemma-4-31b-it:free", Reasoning = "low", ReasoningFor = "gpt-oss", Titled = true
            },
            new TextProviderInfo
            {
                Id = TextProvider.Groq, Name = "Groq", Api = TextApi.Chat, Access = TextAccess.Key, Base = "https://api.groq.com/openai/v1",
                Model = "openai/gpt-oss-20b", Reasoning = "low", ReasoningFor = "gpt-oss"
            },
            new TextProviderInfo
            {
                Id = TextProvider.Mistral, Name = "Mistral", Api = TextApi.Chat, Access = TextAccess.Key, Base = "https://api.mistral.ai/v1", Model = "mistral-small-latest"
            },
            new TextProviderInfo
            {
                Id = TextProvider.Cerebras, Name = "Cerebras", Api = TextApi.Chat, Access = TextAccess.Key, Base = "https://api.cerebras.ai/v1",
                Model = "gpt-oss-120b", Reasoning = "low", ReasoningFor = "gpt-oss"
            },
            new TextProviderInfo
            {
                Id = TextProvider.OpenCodeZen, Name = "OpenCode Zen", Api = TextApi.Chat, Access = TextAccess.Key, Base = "https://opencode.ai/inference/openai/v1",
                Model = "deepseek-v4.1-flash"   // (Zen's free models answer only OpenCode itself: "FreeTierError", seen live in October 2026)
            },
            new TextProviderInfo { Id = TextProvider.Ollama, Name = "Ollama", Api = TextApi.Chat, Access = TextAccess.Local, Base = "http://127.0.0.1:11434/v1", Model = null },
            new TextProviderInfo { Id = TextProvider.LmStudio, Name = "LM Studio", Api = TextApi.Chat, Access = TextAccess.Local, Base = "http://127.0.0.1:1234/v1", Model = null }
        };

        /// <summary>How many entries the options page's list has (Off first).</summary>
        public static int Count => All.Length;

        /// <summary>The entry of a list position; Off for one out of range (a saved number of a later RLO).</summary>
        public static TextProviderInfo Of(int index) => index > 0 && index < All.Length ? All[index] : All[0];

        public static TextProviderInfo Of(TextProvider id) => Of((int)id);

        // The options page's word for each entry, in the list's order (every key written out: the locale check finds each one).
        private static readonly string[] Choices =
        {
            "reallife_textsai_provider_0", "reallife_textsai_provider_1", "reallife_textsai_provider_2", "reallife_textsai_provider_3", "reallife_textsai_provider_4",
            "reallife_textsai_provider_5", "reallife_textsai_provider_6", "reallife_textsai_provider_7", "reallife_textsai_provider_8", "reallife_textsai_provider_9",
            "reallife_textsai_provider_10", "reallife_textsai_provider_11"
        };

        /// <summary>The options page's key for each entry, in order: a copy, one for each entry of <see cref="All"/>.</summary>
        public static string[] ChoiceKeys()
        {
            var keys = new string[All.Length];
            Array.Copy(Choices, keys, keys.Length);
            return keys;
        }

        /// <summary>
        /// The address a service is asked at: its base, or the player's own for a program on this PC (another port), which
        /// must be http or https and, for a local one, on this PC only (127.0.0.1, localhost or ::1). Null if the player's
        /// address is not one.
        /// </summary>
        public static string Base(TextProviderInfo p, string own)
        {
            if (p == null || p.Id == TextProvider.Off)
            {
                return null;
            }
            if (p.Access != TextAccess.Local || string.IsNullOrEmpty(own))
            {
                return p.Base;
            }
            string b = own.Trim().TrimEnd('/');
            if (!System.Uri.TryCreate(b, System.UriKind.Absolute, out System.Uri uri) || (uri.Scheme != "http" && uri.Scheme != "https") || !uri.IsLoopback)
            {
                return null;   // (a program on this PC only: Your own AI's local entries never send a card elsewhere)
            }
            return b;
        }
    }
}
