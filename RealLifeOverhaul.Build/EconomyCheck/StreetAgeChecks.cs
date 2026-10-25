using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Street;
using RealLifeOverhaul.Texts;

// Asking somebody in the street how old they are, as what they do is asked: offered once a day, a follow-up after what they
// do, their name or their work; answered in their own manner, and always with the age, a cooler answer and a small change of
// warmth for those who find it forward, never a refusal asked round again; the age the one the People page shows once they
// are known; typed words read as it; written lines in every pack, so the data v6 contract stays as the packs were trained.
internal static partial class Program
{
    private static void StreetAge()
    {
        StreetAgeOffered();
        StreetAgeAnswers();
        StreetAgeKept();
        StreetAgeWords();
    }

    // Somebody of a nature and a manner, at a talk's score: the score is their warmth alone (no rapport yet).
    private static StreetChat StreetAgeChat(SocialTrait traits, string style, bool humour, int score)
    {
        var s = new StreetStranger
        {
            Key = 77, Seed = 4711, First = "Dana", Last = "Reyes", Age = 41, Traits = traits, Humour = humour, Style = Math.Max(0, Array.IndexOf(StreetRules.Styles, style))
        };
        return new StreetChat { Who = s, Memo = new StreetMemo { Key = 77, Day = 12 }, Approach = new StreetApproach { Warmth = score, Patience = 6 } };
    }

    private static void StreetAgeOffered()
    {
        StreetFacts f = StreetDay();
        StreetStranger s = StreetRules.Identity(55, 12, 5011, false, 47, 6, -1);
        StreetChat c = StreetChatWith(s, f);
        var list = new List<StreetAsk>();
        StreetRules.Choices(c, f, list);
        bool first = list.Contains(StreetAsk.Age);
        c.Done[(int)StreetAsk.Age] = true;
        StreetRules.Choices(c, f, list);
        bool asked = !list.Contains(StreetAsk.Age);
        StreetChat later = StreetChatWith(s, f, new StreetMemo { Key = s.Key, Day = f.Day, AgeTold = true });
        StreetRules.Choices(later, f, list);
        Check(first && asked && !list.Contains(StreetAsk.Age) && StreetLines.Asks[(int)StreetAsk.Age] == "reallife_street_ask_age",
            "street age: the question is offered from the start as what they do is, not again once asked, and not in a later talk the same day once they said it");
        bool afterJob = true, workFirst = true, afterName = true, afterWork = true;
        for (int k = 0; k < 100; k++)
        {
            StreetChat x = StreetChatOf(9400 + k, f);
            x.Turns = 2;
            x.Done[(int)StreetAsk.AboutThem] = true;
            x.Shared = true;
            x.Last = StreetLine.AboutJob;
            StreetRules.Choices(x, f, list);
            workFirst &= list[0] == StreetAsk.Work;
            afterJob &= list.IndexOf(StreetAsk.Age) >= 0 && list.IndexOf(StreetAsk.Age) < 3;
            afterName &= StreetRules.FollowUp(StreetLine.NameGive, StreetAsk.Age, x.Who.Seed) >= 0;
            afterWork &= StreetRules.FollowUp(StreetLine.WorkLikes, StreetAsk.Age, x.Who.Seed) >= 0 && StreetRules.FollowUp(StreetLine.WorkRetired, StreetAsk.Age, x.Who.Seed) >= 0;
        }
        Check(afterJob && workFirst && afterName && afterWork,
            "street age: after what they do it comes among the first three (whether they like it still first), and it follows their name and their work too");
        bool cool = StreetRules.FollowUp(StreetLine.AgeCool, StreetAsk.Area, 3) >= 0 && StreetRules.FollowUp(StreetLine.AgeCool, StreetAsk.Age, 3) < 0
                    && StreetRules.FollowUp(StreetLine.AgeGive, StreetAsk.AboutThem, 3) >= 0 && StreetRules.FollowUp(StreetLine.AgePlayful, StreetAsk.Name, 3) >= 0;
        Check(cool, "street age: after a warm answer the talk goes on to who they are; after a cool one to something practical");
    }

    private static void StreetAgeAnswers()
    {
        StreetFacts f = StreetDay();
        // Plain people answer plainly and like being asked; those who find it forward say it shortly and cool a little.
        StreetChat plain = StreetAgeChat(SocialTrait.Punctual | SocialTrait.Sporty, "warm", false, 0);
        StreetOutcome p = StreetRules.Answer(plain, StreetAsk.Age, f, 1);
        StreetChat careful = StreetAgeChat(SocialTrait.Punctual | SocialTrait.Careful, "warm", false, 0);
        StreetOutcome c = StreetRules.Answer(careful, StreetAsk.Age, f, 1);
        StreetChat envious = StreetAgeChat(SocialTrait.Careful | SocialTrait.Envious, "warm", false, 0);
        StreetOutcome e = StreetRules.Answer(envious, StreetAsk.Age, f, 1);
        Check(p.Line == StreetLine.AgeGive && p.Rapport == 1 && c.Line == StreetLine.AgeCool && c.Rapport == -1 && e.Line == StreetLine.AgeCool && e.Rapport == -1 && !p.Ends && !c.Ends,
            "street age: somebody plain says it and warms a point; the careful and the envious find it forward from a stranger: a cool answer, a point less, and the talk goes on");
        StreetChat warmCareful = StreetAgeChat(SocialTrait.Careful, "warm", false, StreetRules.AgeForwardWarmAt);
        StreetOutcome w = StreetRules.Answer(warmCareful, StreetAsk.Age, f, 1);
        Check(w.Line == StreetLine.AgeGive && w.Rapport == 0 && StreetRules.Answer(StreetAgeChat(SocialTrait.Careful, "warm", false, StreetRules.AgeForwardWarmAt - 1), StreetAsk.Age, f, 1).Line == StreetLine.AgeCool,
            "street age: once the talk is warm (" + StreetRules.AgeForwardWarmAt + ") somebody who finds it forward says it plainly, but takes nothing from it");
        StreetChat cold = StreetAgeChat(SocialTrait.Sporty, "warm", false, StreetRules.AgeCoolBelow - 1);
        StreetOutcome k = StreetRules.Answer(cold, StreetAsk.Age, f, 1);
        Check(k.Line == StreetLine.AgeCool && k.Rapport == 0 && StreetRules.Answer(StreetAgeChat(SocialTrait.Sporty, "warm", false, StreetRules.AgeCoolBelow), StreetAsk.Age, f, 1).Line == StreetLine.AgeGive,
            "street age: anybody cool to the player says it shortly (below " + StreetRules.AgeCoolBelow + "), as they would not say what they do, but it costs nothing with somebody plain");
        bool manner = StreetRules.Answer(StreetAgeChat(SocialTrait.Sporty, "playful", false, 0), StreetAsk.Age, f, 1).Line == StreetLine.AgePlayful
                      && StreetRules.Answer(StreetAgeChat(SocialTrait.Sporty, "cheeky", false, 0), StreetAsk.Age, f, 1).Line == StreetLine.AgePlayful
                      && StreetRules.Answer(StreetAgeChat(SocialTrait.Sporty, "formal", true, StreetRules.AgeTeaseFrom), StreetAsk.Age, f, 1).Line == StreetLine.AgePlayful
                      && StreetRules.Answer(StreetAgeChat(SocialTrait.Sporty, "formal", true, StreetRules.AgeTeaseFrom - 1), StreetAsk.Age, f, 1).Line == StreetLine.AgeGive
                      && StreetRules.Answer(StreetAgeChat(SocialTrait.Careful, "playful", false, 0), StreetAsk.Age, f, 1).Line == StreetLine.AgeCool;
        Check(manner, "street age: in their own manner: the playful and the cheeky make the player guess first, so does somebody with a sense of humour once warm; forward is forward whatever the manner");
        // Never a refusal, never asked round again: every answer at every score and nature names the age, and a second asking gets the same number.
        bool always = true, same = true;
        SocialTrait[] natures = { SocialTrait.None, SocialTrait.Careful, SocialTrait.Envious, SocialTrait.Generous | SocialTrait.NightOwl };
        foreach (SocialTrait t in natures)
        {
            foreach (string style in StreetRules.Styles)
            {
                for (int score = -6; score <= 6; score++)
                {
                    for (int h = 0; h < 2; h++)
                    {
                        StreetChat x = StreetAgeChat(t, style, h == 1, score);
                        StreetOutcome o = StreetRules.Answer(x, StreetAsk.Age, f, score + 7);
                        always &= StreetCards.Aged(o.Line) && o.Line != StreetLine.AgeAgain && !o.Ends && o.Rapport >= -1 && o.Rapport <= 1;
                        same &= StreetRules.Answer(StreetAgeChat(t, style, h == 1, score), StreetAsk.Age, f, score + 7).Line == o.Line;
                        x.Memo.AgeTold = true;
                        StreetOutcome again = StreetRules.Answer(x, StreetAsk.Age, f, score + 7);
                        always &= again.Line == StreetLine.AgeAgain && again.Rapport == 0 && !again.Ends;
                    }
                }
            }
        }
        Check(always && same, "street age: at every score, nature and manner the answer says the age, never ends the talk and moves warmth a point at most; asked again they repeat it; the same person answers alike");
    }

    // The age said is the person's real age as the mod holds it: the figure's, clamped as the cast keeps it, and, once they are
    // somebody the player knows, the People page's (met in the street, their age is true from that day, so the two agree).
    private static void StreetAgeKept()
    {
        Check(StreetRules.Identity(5, 5, 5, true, 12, 6, -1).Age == 18 && StreetRules.Identity(5, 5, 5, true, 97, 6, -1).Age == 85 && StreetRules.Identity(5, 5, 5, true, 47, 6, -1).Age == 47,
            "street age: the age is the figure's own, kept within the cast's 18 to 85");
        bool agree = true;
        for (int perYear = 8; perYear <= 400; perYear += 49)
        {
            for (int birthday = 1; birthday <= perYear; birthday += 3)
            {
                for (int day = 1; day < 3 * perYear; day += 17)
                {
                    agree &= SocialRules.AgeOn(47, day, birthday, day, perYear) == 47;
                }
            }
        }
        bool grows = SocialRules.AgeOn(47, 10, 30, 30, 60) == 48 && SocialRules.AgeOn(47, 1, 30, 200, 60) > 47;
        Check(agree && grows, "street age: an age true from the day they were met shows the same on the People page that day, whatever their birthday, and grows at the next birthday; "
                              + "a seat rolled on day 1 would have shown more (why the street sets the day)");
        string links = TextsSource(@"Wiring\StreetLinks.cs"), talk = TextsSource(@"Street\StreetDirector.Talk.cs");
        Check(links.Contains("p.AgeDay = Mathf.Max(1, SocialLife.Day);") && links.Contains("SocialTalk.AgeOf(p)") && links.Contains("StreetBridges.AgeOf = AgeOf;")
              && talk.Contains("{ \"age\", AgeShown(c).ToString(System.Globalization.CultureInfo.InvariantCulture) }") && talk.Contains("StreetBridges.AgeOf(cast)"),
            "street age: met in the street, the age is true from that day; somebody known says the People page's age (SocialTalk.AgeOf), anybody else the figure's");
        Check(TextsSource(@"Street\StreetGuide.cs").Contains("\"reallife_street_test_age\", () => StreetDirector.Instance?.TestAge()") && TextsSource(@"Street\StreetDirector.Tests.cs").Contains("TestBegin(true);")
              && TextsSource(@"Street\StreetDirector.Outcomes.cs").Contains("Street talk check (age): "),
            "street age: a test button asks the nearest passer-by, and the answer writes its check line");
    }

    private static void StreetAgeWords()
    {
        bool typed = StreetSay.AskOf(StreetSay.Read("How old are you?", null).Kind) == StreetAsk.Age && StreetSay.AskOf(StreetSay.Read("may I ask your age", null).Kind) == StreetAsk.Age
                     && StreetSay.AskOf(StreetSay.Read("what year were you born", null).Kind) == StreetAsk.Age
                     && StreetSay.Read("what do you do", null).Kind == StreetSayKind.AboutThem && StreetSay.Read("I manage a garage", null).Kind != StreetSayKind.Age
                     && StreetSay.Read("how are you", null).Kind == StreetSayKind.Day;
        Check(typed, "street age: typed words asking their age count as the question; what they do, how they are and words that only contain \"age\" do not");
        bool written = true, noTopic = true;
        foreach (StreetLine l in new[] { StreetLine.AgeGive, StreetLine.AgePlayful, StreetLine.AgeCool, StreetLine.AgeAgain })
        {
            written &= StreetCards.V4(l, out _, out _) == null && StreetCards.V5(l, true, out _, out _) == null && StreetCards.V3(l, out _, out _) == null
                       && StreetCards.Written(l, TextIntents.DataV4) && StreetCards.Written(l, TextIntents.DataV5) && StreetCards.Written(l, TextIntents.DataV6);
            noTopic &= TextMemoryTopics.Street(l) == null && TextMemoryTopics.Tone(l) == null;
        }
        Check(written && noTopic, "street age: its answers are written in every pack and leave nothing in a stranger's memory, so RLO Texts' data v6 contract is the packs' as trained");
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("street age", words, codes))
        {
            return;
        }
        var lacking = new List<string>();
        foreach (StreetLine l in new[] { StreetLine.AgeGive, StreetLine.AgePlayful, StreetLine.AgeCool, StreetLine.AgeAgain })
        {
            foreach (string key in StreetLines.Lines[(int)l])
            {
                for (int i = 0; i < codes.Length && i < 3; i++)
                {
                    if (!words[i].TryGetValue(key, out string v) || v.IndexOf("{age}", StringComparison.Ordinal) < 0)
                    {
                        lacking.Add(codes[i] + ":" + key);
                    }
                }
            }
        }
        Check(lacking.Count == 0, "street age: every written answer says the age ({age}) in en, pt and zh-cn (" + string.Join(", ", lacking) + ")");
        Check(AnyChangesRow(words[0], "how old they are") && AnyChangesRow(words[1], "perguntar a idade") && AnyChangesRow(words[2], "多大年纪"),
            "street age: What's new says it in three languages");
    }
}
