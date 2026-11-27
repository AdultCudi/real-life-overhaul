using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

// The notice after an outing (Scripts/Social/OutingScene.After.cs, Ui/UiNotices.cs): a player found that after coffee with a friend the notice had more text than its card and was cut off after
// Mood or Affection, and asked for how far each figure is from its next step. The kit's card showed six lines of the body, and an outing's notice is a card of lines (what it cost, energy, mood and
// the three figures, the stage), so the card now takes as many as such a notice needs in any language. Each figure that has a next step in the code says how far it stands from it, and none that has
// no step ahead says anything (SocialRules.Next).
internal static partial class Program
{
    private static void SocialNext()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        SocialNextRules();
        SocialNextCard();
        SocialNextSources();
        Console.WriteLine("  the outing's notice and the next step: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static SocialPerson Person(SocialStage stage, float familiarity, float affection, float trust, bool family = false, bool movedIn = false)
    {
        return new SocialPerson
        {
            Met = true, Stage = stage, Familiarity = familiarity, Affection = affection, Trust = trust, MovedIn = movedIn, Origin = family ? SocialOrigin.Family : SocialOrigin.Street, MetDay = 1
        };
    }

    private static string Step(SocialPerson p, SocialMeter meter, bool romantic = false, bool single = true)
    {
        return SocialRules.Next(p, meter, romantic, single, out float need, out SocialGoal goal) ? goal + " " + need : "none";
    }

    private static void SocialNextRules()
    {
        SocialPerson acquaintance = Person(SocialStage.Acquaintance, 10f, 12f, 8f);
        Check(Step(acquaintance, SocialMeter.Familiarity) == "Friend 30" && Step(acquaintance, SocialMeter.Affection) == "Friend 30" && Step(acquaintance, SocialMeter.Trust) == "Friend 20",
            "next step: an acquaintance's three figures head for the friend's (30, 30, 20): " + Step(acquaintance, SocialMeter.Familiarity) + ", " + Step(acquaintance, SocialMeter.Affection) + ", " + Step(acquaintance, SocialMeter.Trust));
        SocialPerson friend = Person(SocialStage.Friend, 40f, 45f, 30f);
        Check(Step(friend, SocialMeter.Familiarity) == "CloseFriend 60" && Step(friend, SocialMeter.Affection) == "CloseFriend 60" && Step(friend, SocialMeter.Trust) == "CloseFriend 55",
            "next step: a friend's figures head for the close friend's (60, 60, 55) when no romance is possible");
        Check(Step(friend, SocialMeter.Trust, true, true) == "Date 45" && Step(friend, SocialMeter.Affection, true, true) == "Date 60" && Step(friend, SocialMeter.Familiarity, true, true) == "CloseFriend 60",
            "next step: for somebody the player could fall for the nearer goal is named, the date's (affection 60, trust 45) before the close friend's, and familiarity has only the close friend's");
        Check(Step(friend, SocialMeter.Trust, true, false) == "CloseFriend 55", "next step: with a partner already, nobody is somebody to date: the close friend's figure stays");
        SocialPerson trusted = Person(SocialStage.Friend, 40f, 45f, 50f);
        Check(Step(trusted, SocialMeter.Trust, true, true) == "CloseFriend 55" && Step(trusted, SocialMeter.Trust) == "CloseFriend 55", "next step: a figure already past the date's is shown the next it has to reach, not the one it has passed");
        SocialPerson close = Person(SocialStage.CloseFriend, 70f, 70f, 60f);
        Check(Step(close, SocialMeter.Affection) == "none" && Step(close, SocialMeter.Trust, true, true) == "none" && Step(close, SocialMeter.Familiarity, true, true) == "none",
            "next step: a close friend has nothing more to reach by the figures, and dating is asked for, not earned (shown only where a figure is short of it)");
        SocialPerson slipped = Person(SocialStage.CloseFriend, 70f, 55f, 40f);
        Check(Step(slipped, SocialMeter.Affection, true, true) == "Date 60" && Step(slipped, SocialMeter.Trust, true, true) == "Date 45" && Step(slipped, SocialMeter.Trust) == "none",
            "next step: a close friend whose figures slipped under the date's is shown the date's, if a romance is possible, and nothing otherwise");
        SocialPerson dating = Person(SocialStage.Dating, 70f, 62f, 66f);
        Check(Step(dating, SocialMeter.Affection) == "Partner 80" && Step(dating, SocialMeter.Trust) == "Partner 70" && Step(dating, SocialMeter.Familiarity) == "none",
            "next step: somebody dating heads for the partner's figures (affection 80, trust 70), familiarity asks nothing");
        SocialPerson partner = Person(SocialStage.Partner, 90f, 90f, 80f);
        Check(Step(partner, SocialMeter.Trust) == "MoveIn 85" && Step(partner, SocialMeter.Affection) == "none" && Step(Person(SocialStage.Partner, 90f, 90f, 80f, false, true), SocialMeter.Trust) == "none",
            "next step: a partner's trust heads for the move in's 85 until they live together, and affection asks nothing more");
        Check(Step(Person(SocialStage.Acquaintance, 29.6f, 30f, 20f), SocialMeter.Familiarity) == "Friend 30" && Step(Person(SocialStage.Acquaintance, 30f, 30f, 20f), SocialMeter.Familiarity) == "none",
            "next step: a figure of 29.6 is still short of 30 and shows 29, one that has reached 30 shows nothing");
        var stranger = new SocialPerson { Met = false };
        Check(!SocialRules.Next(stranger, SocialMeter.Trust, false, true, out _, out _) && !SocialRules.Next(null, SocialMeter.Trust, false, true, out _, out _), "next step: nobody not met, and nobody at all, has a step");
        // every figure the rules name is a figure the stage rules ask for
        bool honest = SocialRules.FriendFamiliarity == 30f && SocialRules.FriendAffection == 30f && SocialRules.FriendTrust == 20f && SocialRules.CloseFamiliarity == 60f && SocialRules.CloseAffection == 60f
                      && SocialRules.CloseTrust == 55f && SocialRules.DateAffection == 60f && SocialRules.DateTrust == 45f && SocialRules.PartnerAffection == 80f && SocialRules.PartnerTrust == 70f
                      && SocialRules.MoveInTrust == 85f;
        Check(honest, "next step: the figures shown are the code's own constants");
    }

    private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);

    // The notice of an outing at its longest, from each language's own words: what it cost, they paid, a thank-you, energy, mood, the three figures with their next step, the late line, the stage.
    private static string OutingBody(Dictionary<string, string> words, Dictionary<string, string> english)
    {
        string T(string key)
        {
            return words.TryGetValue(key, out string t) && t.Length > 0 ? t : english[key];
        }
        var b = new StringBuilder();
        b.Append(Tags.Replace(T("reallife_social_card_cost"), "")).Append("   -$12.50\n");
        b.Append(T("reallife_social_card_cost")).Append("   ").Append(T("reallife_social_card_they_paid")).Append('\n');
        b.Append(T("reallife_social_card_thanks")).Append("   +$5\n");
        b.Append(T("reallife_social_card_energy")).Append("   +8\n");
        b.Append(T("reallife_social_card_mood")).Append("   ").Append(Tags.Replace(T("reallife_social_card_mood_up"), "")).Append('\n');
        string next = T("reallife_social_card_next").Replace("{now}", "74").Replace("{need}", "90").Replace("{goal}", T("reallife_social_goal_close"));
        b.Append(T("reallife_social_card_familiarity")).Append("   +3   ").Append(next).Append('\n');
        b.Append(T("reallife_social_card_affection")).Append("   +4   ").Append(next).Append('\n');
        b.Append(T("reallife_social_card_trust")).Append("   +2   ").Append(next).Append('\n');
        b.Append(T("reallife_social_card_late")).Append('\n');
        b.Append('\n').Append(T("reallife_social_card_now")).Append("   ").Append(T("reallife_social_stage_close"));
        return b.ToString();
    }

    private static void SocialNextCard()
    {
        Dictionary<string, string> en = LocaleFile("en");
        float inner = UiNotices.CardWidth - UiTokens.S4 * 3f - 40f;   // the card's text width: the tile is 40 px
        int worst = 0, most = 0;
        string worstCode = string.Empty;
        bool whole = true, checkedAll = true;
        foreach (string code in new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" })
        {
            Dictionary<string, string> words = LocaleFile(code);
            checkedAll &= words.Count > 0;
            string body = OutingBody(words, en);
            int lines = UiTextRules.Lines(body, UiStyle.Body, inner, 60, out float _);
            if (lines > worst)
            {
                worst = lines;
                worstCode = code;
            }
            most = Math.Max(most, lines);
            var cards = new UiNoticeCardsModel { Cards = new[] { new UiNotice { Icon = UiIcon.Package, Header = "Info", Body = body, Age = "Day 7, 12:00" } }, CardCount = 1 };
            var s = new UiScene();
            UiNotices.LayCards(s, cards, UiFrame.FullMenu(16f / 9f, "HUD"), true, UiDefaultKeys.Keyboard);
            bool shown = false;
            for (int i = 0; i < s.ItemCount; i++)
            {
                shown |= s.Items[i].Text == body;
            }
            whole &= shown;
        }
        Check(checkedAll, "outing notice: every language's file is read");
        Check(worst > 6, "outing notice: the notice runs past six lines, which is what the card cut it at (the longest is " + worst + " lines, in " + worstCode + ")");
        Check(whole && most <= UiNotices.BodyLines, "outing notice: the card shows the whole text in every language: the longest is " + most + " lines of the " + UiNotices.BodyLines + " the card takes, and nothing is cut");
        // a body longer than the card takes still ends at a whole word
        var longBody = new StringBuilder();
        for (int i = 0; i < 400; i++)
        {
            longBody.Append("word").Append(i).Append(' ');
        }
        var big = new UiNoticeCardsModel { Cards = new[] { new UiNotice { Icon = UiIcon.Package, Header = "Info", Body = longBody.ToString(), Age = null } }, CardCount = 1 };
        var scene = new UiScene();
        UiNotices.LayCards(scene, big, UiFrame.FullMenu(16f / 9f, "HUD"), true, UiDefaultKeys.Keyboard);
        bool ended = false;
        for (int i = 0; i < scene.ItemCount; i++)
        {
            string text = scene.Items[i].Text;
            ended |= text != null && text.StartsWith("word0 ", StringComparison.Ordinal) && text.EndsWith("…", StringComparison.Ordinal) && text.Length < longBody.Length;
        }
        Check(ended, "outing notice: a body past what the card takes (another mod's) ends at a whole word with an ellipsis");
    }

    private static void SocialNextSources()
    {
        string after = ModSource(@"Social\OutingScene.After.cs"), rules = ModSource(@"Social\SocialRules.cs"), notices = ModSource(@"Ui\UiNotices.cs");
        // The card's figures and goals are built in SocialCardText, shared with the card after meeting somebody's friends.
        string card = ModSource(@"Social\SocialCardText.cs");
        Check(after.Contains("Signed(familiarity) + NextOf(SocialMeter.Familiarity)") && after.Contains("Signed(affection) + NextOf(SocialMeter.Affection)") && after.Contains("Signed(trust) + NextOf(SocialMeter.Trust)")
              && after.Contains("NextOf(SocialMeter meter) => SocialCardText.NextOf(_who, meter);")
              && card.Contains("SocialRules.Next(who, meter, SocialLife.Romantic(who), LoveHooks.CanDate,"), "outing notice: each figure's line carries its next step from the rules, for a romance only where one is possible");
        Check(card.Contains("\"reallife_social_goal_friend\"") && card.Contains("\"reallife_social_goal_close\"") && card.Contains("\"reallife_social_goal_date\"") && card.Contains("\"reallife_social_goal_partner\"")
              && card.Contains("\"reallife_social_goal_movein\"") && card.Contains("\"reallife_social_card_next\""), "outing notice: the goals' words are whole keys");
        Check(rules.Contains("public static bool Next(SocialPerson person, SocialMeter meter,") && !rules.Contains("UnityEngine"), "outing notice: the rule is game-free");
        Check(notices.Contains("public const int BodyLines = 18;"), "outing notice: the card takes up to eighteen lines of body");
        Dictionary<string, string> en = LocaleFile("en");
        string[] keys = { "reallife_social_card_next", "reallife_social_goal_friend", "reallife_social_goal_close", "reallife_social_goal_date", "reallife_social_goal_partner", "reallife_social_goal_movein" };
        bool all = true, style = true;
        foreach (string k in keys)
        {
            string t = en.TryGetValue(k, out string v) ? v : string.Empty;
            all &= t.Length > 0;
            style &= t.IndexOf('—') < 0 && t.IndexOf('–') < 0 && t.IndexOf('!') < 0 && t.IndexOf(';') < 0;
        }
        string next = en.TryGetValue("reallife_social_card_next", out string n) ? n : string.Empty;
        Check(all && style && next.Contains("{now}") && next.Contains("{need}") && next.Contains("{goal}"), "outing notice: the next step's words are written, with the figure, the one wanted and the goal");
        Check(WhatsNewHas(en, "Fixed: the notice after a social event shows all its lines") && WhatsNewHas(en, "how far each figure is from the next step"), "outing notice: What's new tells of both");
    }
}
