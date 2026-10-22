using System;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// The services Your own AI can send a card to, in the options page's order; the number is saved, so new ones go at
    /// the end. GitHub Copilot is not offered (no documented way for another app to call it), nor Claude subscriptions
    /// (their terms forbid the sign-in in other apps): Anthropic is reached only with an API key.
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
        public string Name;              // as the log and the notices name it, the same in every language
        public TextApi Api;
        public TextAccess Access;
        public string Base;              // the API's base address, without the last part of the path
        public string Model;             // the model asked when the player names none; null for the first the service lists
        public string TokensField = "max_tokens";   // the chat API's name for the reply's length
        public bool Temperature = true;  // the service takes a sampling temperature for this kind of model
        public string Reasoning;         // a low reasoning effort sent to the models that think first; null for none
        public string ReasoningFor;      // ...only to models whose name holds this; null for every model
        public bool Titled;              // the service is told the app's name in its own header (OpenRouter's X-Title)
    }

    /// <summary>
    /// The table of services as each one's documentation gives them: base addresses, the default models (a free or the
    /// cheapest fast one; the player can name any other), and what each refuses. Game-free.
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
                Temperature = false, Reasoning = "low"   // the plan's route takes no temperature and no length
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
                Model = "deepseek-v4.1-flash"   // Zen's free models answer only OpenCode itself ("FreeTierError")
            },
            new TextProviderInfo { Id = TextProvider.Ollama, Name = "Ollama", Api = TextApi.Chat, Access = TextAccess.Local, Base = "http://127.0.0.1:11434/v1", Model = null },
            new TextProviderInfo { Id = TextProvider.LmStudio, Name = "LM Studio", Api = TextApi.Chat, Access = TextAccess.Local, Base = "http://127.0.0.1:1234/v1", Model = null }
        };

        /// <summary>How many entries the options page's list has, Off first.</summary>
        public static int Count => All.Length;

        /// <summary>The entry of a list position; Off for one out of range, such as a later RLO's saved number.</summary>
        public static TextProviderInfo Of(int index) => index > 0 && index < All.Length ? All[index] : All[0];

        public static TextProviderInfo Of(TextProvider id) => Of((int)id);

        // The options page's word for each entry, every key written out for the locale check.
        private static readonly string[] Choices =
        {
            "reallife_textsai_provider_0", "reallife_textsai_provider_1", "reallife_textsai_provider_2", "reallife_textsai_provider_3", "reallife_textsai_provider_4",
            "reallife_textsai_provider_5", "reallife_textsai_provider_6", "reallife_textsai_provider_7", "reallife_textsai_provider_8", "reallife_textsai_provider_9",
            "reallife_textsai_provider_10", "reallife_textsai_provider_11"
        };

        /// <summary>A copy of the options page's key for each entry, in order.</summary>
        public static string[] ChoiceKeys()
        {
            var keys = new string[All.Length];
            Array.Copy(Choices, keys, keys.Length);
            return keys;
        }

        /// <summary>
        /// The address a service is asked at: its base, or for a program on this PC the player's own, http or https on this PC
        /// only. Null if the player's address is not one.
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
                return null;   // the local entries never send a card elsewhere
            }
            return b;
        }
    }
}
