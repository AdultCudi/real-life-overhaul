using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Texts;

// Your own AI and the model names (a player's report, 29 Oct 2026: a 404 from Google for a model name the service did not know, with no word to the
// player of what to change). Every service's default model id is held to a table with the date it was checked against the service's own documentation;
// a model the service does not know (404, "model not found", or a 400 whose message says so) stops the runner with the reason, and one notice names the
// service and the model and says where to change it (Model, in Your own AI), in every language; the Guide says the same.
internal static partial class Program
{
    private static void AiModels()
    {
        AiModelTable();
        AiModelFaults();
        AiModelTexts();
    }

    // The default model of each service that names one, the pattern the service's ids follow, the date it was read in the service's documentation, and the page.
    private static readonly (TextProvider Id, string Model, string Pattern, string Verified, string Source)[] AiModelDocs =
    {
        (TextProvider.OpenAi, "gpt-6-luna", @"^gpt-[0-9]+(\.[0-9]+)?-[a-z]+$", "2026-10-31", "developers.openai.com/api/docs/models"),
        (TextProvider.Anthropic, "claude-haiku-5-5", @"^claude-[a-z]+-[0-9]+(-[0-9]+)?$", "2026-10-31", "platform.claude.com/docs/en/models/overview"),
        (TextProvider.Gemini, "gemini-3.8-flash", @"^gemini-[0-9]+(\.[0-9]+)?-[a-z]+(-[a-z]+)?$", "2026-10-31", "ai.google.dev/gemini-api/docs/models"),
        (TextProvider.OpenRouter, "google/gemma-4-31b-it:free", @"^[a-z0-9-]+/[a-z0-9.-]+(:[a-z]+)?$", "2026-10-31", "openrouter.ai/api/v1/models"),
        (TextProvider.Groq, "openai/gpt-oss-20b", @"^[a-z0-9-]+/[a-z0-9.-]+$", "2026-10-31", "console.groq.com/docs/models"),
        (TextProvider.Mistral, "mistral-small-latest", @"^mistral-[a-z]+-(latest|[0-9]{4})$", "2026-10-31", "docs.mistral.ai/getting-started/models (alias of mistral-small-2603)"),
        (TextProvider.Cerebras, "gpt-oss-120b", @"^[a-z0-9.-]+$", "2026-10-31", "inference-docs.cerebras.ai/models/overview"),
        (TextProvider.OpenCodeZen, "deepseek-v4.1-flash", @"^[a-z0-9.-]+$", "2026-10-31", "opencode.ai/docs/zen")
    };

    private static void AiModelTable()
    {
        var bad = new List<string>();
        var named = new HashSet<TextProvider>();
        foreach (var d in AiModelDocs)
        {
            named.Add(d.Id);
            TextProviderInfo p = TextProviders.Of(d.Id);
            bool dated = DateTime.TryParseExact(d.Verified, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime when) && when.Year >= 2026;
            if (string.IsNullOrEmpty(p.Model) || p.Model != d.Model || !Regex.IsMatch(p.Model, d.Pattern) || !dated || string.IsNullOrEmpty(d.Source))
            {
                bad.Add(d.Id + " " + p.Model);
            }
        }
        for (int i = 1; i < TextProviders.Count; i++)
        {
            TextProviderInfo p = TextProviders.Of(i);
            if (p.Access == TextAccess.Key && !named.Contains(p.Id))
            {
                bad.Add(p.Id + " is not in the table");
            }
        }
        Check(bad.Count == 0, "your own AI, model names: every keyed service's default model is a non-empty id of the service's pattern and is the one the table lists, with the date it was read in the service's documentation (" + string.Join(", ", bad) + ")");
        TextProviderInfo openai = TextProviders.Of(TextProvider.OpenAi);
        Check(openai.Model == "gpt-6-luna" && openai.ReasoningFor == "gpt-" && openai.Reasoning == "low"
              && TextProviders.Of(TextProvider.Gemini).Reasoning == "minimal" && "gemini-3.8-flash".Contains(TextProviders.Of(TextProvider.Gemini).ReasoningFor)
              && "claude-haiku-5-5".Contains(TextProviders.Of(TextProvider.Anthropic).ReasoningFor),
            "your own AI, model names: OpenAI's own default is the documented gpt-6-luna, and the reasoning settings still reach each default model (gpt-, gemini-, claude-haiku-5)");
        TextWebRequest r = TextServiceRules.Build(openai, openai.Base, openai.Model, TextCard.System, "who: Dana\nlang: en", ServiceKey, false, false);
        Check(r.Body.Contains("\"model\":\"gpt-6-luna\"") && r.Body.Contains("\"reasoning_effort\":\"low\""), "your own AI, model names: OpenAI is asked for gpt-6-luna with a low reasoning effort");
    }

    private static void AiModelFaults()
    {
        // What each service answers for a model name it does not know.
        var bodies = new[]
        {
            (404, "[{\"error\":{\"code\":404,\"message\":\"models/gemini-flash is not found for API version v1beta, or is not supported for generateContent.\",\"status\":\"NOT_FOUND\"}}]"),
            (404, "{\"error\":{\"message\":\"The model `gpt-5.6-luna` does not exist or you do not have access to it.\",\"type\":\"invalid_request_error\",\"code\":\"model_not_found\"}}"),
            (404, "{\"type\":\"error\",\"error\":{\"type\":\"not_found_error\",\"message\":\"model: claude-haiku-9\"}}"),
            (404, "{\"error\":{\"message\":\"No endpoints found for x/y:free.\",\"code\":404}}"),
            (400, "{\"object\":\"error\",\"message\":\"Invalid model: mistral-tiny-x\",\"type\":\"invalid_model\",\"code\":\"1500\"}"),
            (404, "not found")
        };
        var wrong = new List<string>();
        for (int i = 0; i < bodies.Length; i++)
        {
            TextServiceResult res = TextServiceRules.Read(TextApi.Chat, Ans(bodies[i].Item1, bodies[i].Item2));
            if (res.Fault != TextFault.Model || !TextServiceRules.Stops(res.Fault))
            {
                wrong.Add("#" + i + " " + res.Fault);
            }
        }
        Check(wrong.Count == 0, "your own AI, model names: a 404, a \"model not found\" body, or a 400 saying the model is invalid is a Model fault that stops the service until the name is mended (" + string.Join(", ", wrong) + ")");
        TextServiceResult anthropic = TextServiceRules.Read(TextApi.Messages, Ans(404, "{\"type\":\"error\",\"error\":{\"type\":\"not_found_error\",\"message\":\"model: claude-haiku-9\"}}"));
        TextServiceResult temp = TextServiceRules.Read(TextApi.Chat, Ans(400, "{\"error\":{\"message\":\"temperature is not supported with this model\"}}"));
        TextServiceResult key = TextServiceRules.Read(TextApi.Chat, Ans(400, "[{\"error\":{\"code\":400,\"message\":\"API key not valid. Please pass a valid API key.\",\"status\":\"INVALID_ARGUMENT\"}}]"));
        TextServiceResult plain = TextServiceRules.Read(TextApi.Chat, Ans(400, "{\"error\":{\"message\":\"messages must not be empty\"}}"));
        Check(anthropic.Fault == TextFault.Model && temp.Fault == TextFault.BadRequest && temp.Param == "temperature" && key.Fault == TextFault.Key && plain.Fault == TextFault.BadRequest,
            "your own AI, model names: only a model the service does not know is a Model fault; a refused temperature, a refused key and a bad request keep their own faults");

        // The runner: a 404 stops it, says the model was refused, and the model it asked is the one named.
        var logs = new List<string>();
        Action<string> log = s => { lock (logs) { logs.Add(s); } };
        TextProviderInfo gemini = TextProviders.Of(TextProvider.Gemini);
        var web = new PretendWeb { Answer = (rq, ms) => Ans(404, bodies[0].Item2) };
        var q = new TextQueue();
        var run = new TextService(q, new TextServiceStart { Provider = gemini, Base = gemini.Base, Key = ServiceKey, Model = "gemini-flash" }, web, log);
        run.Start();
        TextJob job = ServiceJob(q, "m", 20);
        run.Nudge();
        Until(() => run.State == TextServerState.Failed, 3000);
        Check(run.State == TextServerState.Failed && run.Fault == TextFault.Model && run.ModelRefused && run.Model == "gemini-flash" && job.State == TextJobState.Missed,
            "your own AI, model names: a model name the service answers 404 to stops the runner, which says the model was refused and which one it was");
        run.Dispose();
        var okWeb = new PretendWeb { Answer = (rq, ms) => Ans(200, "{\"choices\":[{\"message\":{\"content\":\"Hi.\"}}]}") };
        q = new TextQueue();
        var fine = new TextService(q, new TextServiceStart { Provider = gemini, Base = gemini.Base, Key = ServiceKey }, okWeb, log);
        fine.Start();
        TextJob good = ServiceJob(q, "g", 20);
        fine.Nudge();
        Until(() => good.State == TextJobState.Written, 3000);
        Check(!fine.ModelRefused && fine.State == TextServerState.Ready, "your own AI, model names: a service that answers is not marked as having refused the model");
        fine.Dispose();
        var none = new TextService(new TextQueue(), new TextServiceStart { Provider = gemini, Base = gemini.Base, Key = null }, okWeb, log);
        none.Start();
        Check(none.State == TextServerState.Failed && !none.ModelRefused, "your own AI, model names: a stop for another reason (no key) is not told as a model the service does not know");
    }

    private static void AiModelTexts()
    {
        var codes = new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };
        string key = TextServiceRules.ModelUnknownKey;
        bool fits = true, holes = true, plain = true;
        string worst = string.Empty;
        foreach (string code in codes)
        {
            Dictionary<string, string> texts = LocaleFile(code);
            string v = texts.TryGetValue(key, out string t) ? t : string.Empty;
            string filled = v.Replace("{service}", "Google Gemini").Replace("{model}", "gemini-3.8-flash-lite-preview");
            if (filled.Length > 240 || v.Length == 0)
            {
                fits = false;
                worst += code + " " + filled.Length + " ";
            }
            holes &= v.Contains("{service}") && v.Contains("{model}") && !v.Contains("{why}");
            plain &= !v.Contains("  ") && !v.Contains("!") && !v.Contains("—") && !v.Contains("–");
        }
        Check(fits, "your own AI, model names: the notice is there and 240 characters or fewer with its values filled, in all eleven languages (" + worst + ")");
        Check(holes && plain, "your own AI, model names: each notice names the {service} and the {model} and has no double spaces, exclamation marks or dashes");
        Dictionary<string, string> en = LocaleFile("en");
        Check(en[key].Contains(en["reallife_textsai_model"]) && en[key].Contains(en["reallife_textsai_header"]),
            "your own AI, model names: the notice names the Model option and Your own AI, where the name is changed");
        bool guide = true;
        foreach (string code in codes)
        {
            Dictionary<string, string> texts = LocaleFile(code);
            string g = texts["reallife_guide_textsai"];
            guide &= g.Contains("<b>" + texts["reallife_textsai_model"] + "</b>") && g.Contains("404") && g.Contains("model not found") && g.Contains("<b>" + texts["reallife_textsai_test"] + "</b>");
        }
        Check(guide, "your own AI, model names: the Guide's page says what the notice means and where to change the name, in all eleven languages");
        string service = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts", "Texts", "TextsDirector.Service.cs")));
        Check(service.Contains("s.ModelRefused && s.State == TextServerState.Failed") && service.Contains("_modelTold") && service.Contains("TextServiceRules.ModelUnknownKey"),
            "your own AI, model names: the director tells the notice once for each start, and the status line uses it for a refused model");

        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Fixed: Your own AI now says when the service does not know the model name" },
            { "pt", "Corrigido: Sua própria IA agora diz quando o serviço não conhece o nome do modelo" },
            { "zh-cn", "已修复：当服务不认识模型名时" },
            { "de", "Behoben: Deine eigene KI sagt jetzt, wenn der Dienst den Modellnamen nicht kennt" },
            { "fr", "Corrigé : Votre propre IA indique maintenant quand le service ne connaît pas le nom du modèle" },
            { "es", "Corregido: Tu propia IA ahora dice cuándo el servicio no conoce el nombre del modelo" },
            { "ru", "Исправлено: Ваш собственный ИИ теперь сообщает, когда сервис не знает название модели" },
            { "nl", "Opgelost: Je eigen AI zegt nu wanneer de dienst de modelnaam niet kent" },
            { "tr", "Düzeltildi: Kendi yapay zekânız artık hizmet model adını tanımadığında" },
            { "pl", "Naprawiono: Własna AI mówi teraz, gdy usługa nie zna nazwy modelu" },
            { "ko", "수정: 서비스가 모델 이름을 모를 때 내 AI가 이제" }
        };
        bool every = true, clean = true;
        string lacking = string.Empty;
        foreach (KeyValuePair<string, string> pair in lines)
        {
            Dictionary<string, string> texts = LocaleFile(pair.Key);
            bool has = WhatsNewHas(texts, pair.Value);
            every &= has;
            lacking += has ? string.Empty : pair.Key + " ";
            foreach (KeyValuePair<string, string> row in texts)
            {
                if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains(pair.Value))
                {
                    clean &= !row.Value.Contains("  ") && !row.Value.Contains("!") && !row.Value.Contains("—") && !row.Value.Contains("–") && Occurs(row.Value, pair.Value) == 1;
                }
            }
        }
        Check(lines.Count == 11 && every, "your own AI, model names: What's new tells of the fix, with its fix mark, in all eleven languages (" + (lacking.Length > 0 ? "lacking " + lacking : "none lacking") + ")");
        Check(clean, "your own AI, model names: the fix line is said once in its row, with no double spaces, exclamation marks or dashes");
    }
}
