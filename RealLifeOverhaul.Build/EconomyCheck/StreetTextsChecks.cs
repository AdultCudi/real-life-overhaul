using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RealLifeOverhaul.Street;
using RealLifeOverhaul.Texts;

// Street talk worded by RLO Texts: every line the game decides has its card of data v4 (registered with data version 4,
// so only a v4 pack is asked), what of data v3 fits until then, the stranger's roles as the generator writes them, the mood
// leaned the way the game decided, words of chat never shown, and the source's promises: asked at the first priority
// within the hold, v4 first, the written line otherwise.
internal static partial class Program
{
    private static void StreetCardsUsed()
    {
        var missing = new List<string>();
        int v4 = 0, v3 = 0;
        for (int i = 0; i < (int)StreetLine.Count; i++)
        {
            var line = (StreetLine)i;
            string intent = StreetCards.V4(line, out string branch, out int lean);
            if (intent == null)
            {
                if (line != StreetLine.Waiting && line != StreetLine.DirAsk)
                {
                    missing.Add(line + " has no card");
                }
                continue;
            }
            TextIntent t = TextIntents.Find(intent, branch);
            if (t == null || t.Data != TextIntents.DataV4 || t.Voice != "stranger" || t.Channel != "spoken" || t.Face != TextFace.Friend || t.Write != TextIntents.StreetWrite)
            {
                missing.Add(line + ": " + intent + "#" + branch + (t == null ? " not registered" : " not a v4 street card"));
            }
            else
            {
                v4++;
            }
            string old = StreetCards.V3(line, out string reason, out _);
            if (old != null)
            {
                TextIntent o = TextIntents.Find(old);
                if (o == null || o.Data != TextIntents.DataV3)
                {
                    missing.Add(line + ": v3's " + old + " is not registered as data v3");
                }
                else
                {
                    v3++;
                }
                if (old.StartsWith("citizen.dm.invite_", StringComparison.Ordinal) && old != "citizen.dm.invite_yes"
                    && Array.IndexOf(new[] { "they barely know you", "they are busy at the moment", "they are seeing someone", "they are not interested", "you were rude to them before", "they are too busy" }, reason) < 0)
                {
                    missing.Add(line + ": the reason '" + reason + "' is not one v3 learnt");
                }
            }
        }
        Check(missing.Count == 0 && v4 == (int)StreetLine.Count - 2, "street talk, RLO Texts: every line the game decides has its registered data v4 card (" + v4 + "), and v3's where one fits ("
                                                                    + v3 + "; " + string.Join("; ", missing) + ")");
        int street = 0;
        foreach (TextIntent t in TextIntents.All)
        {
            street += t.Key.StartsWith("street.", StringComparison.Ordinal) ? 1 : 0;
        }
        Check(street == 70, "street talk, RLO Texts: data v4's 70 cards are registered, one per branch the game decides (" + street + ")");

        // The roles and the moods as the generator writes them (its own cards, the fixtures).
        string path = System.IO.Path.Combine(TextsBuildFolder(), "EconomyCheck", "TextsCards.jsonl");
        string fixtures = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
        Check(fixtures.Contains("role: " + StreetCards.Role("street.greet", "stranger") + "\\n") && fixtures.Contains("role: " + StreetCards.Role("street.greet", "again") + "\\n")
              && StreetCards.Role("street.name", "known") == StreetCards.Role("street.greet", "again") && StreetCards.Role("street.directions", "found") == StreetCards.Role("street.greet", "stranger"),
            "street talk, RLO Texts: the stranger's two roles are the generator's own words");
        Check(StreetCards.Leaned(-2, 1) == 0 && StreetCards.Leaned(2, 1) == 2 && StreetCards.Leaned(2, -1) == 0 && StreetCards.Leaned(-1, -1) == -1 && StreetCards.Leaned(1, 0) == 1,
            "street talk, RLO Texts: the mood leans the way the game decided (up for a yes, down for a no), as the training's cards");
        StreetCards.V4(StreetLine.ComplimentYes, out _, out int up);
        StreetCards.V4(StreetLine.ComplimentNo, out _, out int down);
        StreetCards.V4(StreetLine.DirFound, out _, out int none);
        Check(up == 1 && down == -1 && none == 0, "street talk, RLO Texts: a compliment that lands leans up, one that falls flat down, the way somewhere not at all");
        Check(StreetCards.Chatty("Send me a message on Flare.") && StreetCards.Chatty("Follow me, I post daily") && StreetCards.Chatty("I'm @dana")
              && !StreetCards.Chatty("Bean There, on 5th Avenue. Follow your nose.") && !StreetCards.Chatty(null) && !StreetCards.Chatty("Lovely day for it."),
            "street talk, RLO Texts: words that speak of chat (Flare, messages, posts, a handle) are never shown; plain speech is");

        string texts = TextsSource(@"Wiring\StreetTexts.cs");
        Check(texts.Contains("Priority = TextPriority.Now") && texts.Contains("Seconds = guess ? Street.StreetRules.GuessSeconds : Street.StreetRules.Hold")
              && texts.IndexOf("RequestV4(c, line, data, back, false)", StringComparison.Ordinal) < texts.IndexOf("RequestV3(c, line, data, back, false)", StringComparison.Ordinal)
              && texts.Contains("TextsDirector.Words.Wants(TextUse.People, false)") && !texts.Contains("Shared = true") && !texts.Contains("Keep = true"),
            "street talk, RLO Texts: asked at the first priority within the hold, data v4 first, only while people's texts may be worded; nothing kept or shared");
        Check(TextsSource(@"Wiring\StreetLinks.cs").Contains("StreetTexts.Connect();") && TextsSource(@"Street\StreetDirector.Talk.cs").Contains("Words(_chat, line, data, player, _onWords)"),
            "street talk, RLO Texts: the conversation asks for every line it shows");
    }
}
