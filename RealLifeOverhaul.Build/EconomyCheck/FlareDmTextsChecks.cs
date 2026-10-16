using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Texts;

// The answer to a message of the player's on Flare, worded by RLO Texts (a player's report, 16 Oct 2026: a city person
// asked "you up for a coffee?" answered with the written line although the model ran): its answerer read from the account
// as Flare keys it (a city person "c6", no colon; an influencer "i:3"), so every city person's answer is asked with their
// own card; which answers still want words; the player's message it answers; and an answer, or a post, handed back without
// words (the model restarted on a change of device or threads, still loading past its deadline) asked again on the next
// event while it is still to come, at most three times, its words then kept and drawn under the same key.
internal static partial class Program
{
    private static void FlareDmTexts()
    {
        FlareDmTextsSpeaker();
        FlareDmTextsWants();
        FlareDmTextsAskedAgain();
        FlareDmTextsSources();
    }

    private static void FlareDmTextsSpeaker()
    {
        Check(FlareTextsMap.DmSpeaker("c6", out bool inf, out int n) && !inf && n == 6,
            "flare messages' words: a city person keyed 'c6' (as FlareLiveRules.CityDm and the profiles key them) answers as city person 6");
        Check(FlareTextsMap.DmSpeaker("c27", out inf, out n) && !inf && n == 27,
            "flare messages' words: a city person keyed 'c27' answers as city person 27, not 7");
        Check(FlareTextsMap.DmSpeaker("i:3", out inf, out n) && inf && n == 3, "flare messages' words: an influencer keyed 'i:3' answers as influencer 3");
        Check(!FlareTextsMap.DmSpeaker("c:Ashby", out _, out _) && !FlareTextsMap.DmSpeaker("o:0", out _, out _) && !FlareTextsMap.DmSpeaker("p:host", out _, out _)
              && !FlareTextsMap.DmSpeaker("b:Crunch Time", out _, out _) && !FlareTextsMap.DmSpeaker("c", out _, out _) && !FlareTextsMap.DmSpeaker("c-1", out _, out _)
              && !FlareTextsMap.DmSpeaker("i:", out _, out _) && !FlareTextsMap.DmSpeaker(null, out _, out _),
            "flare messages' words: a candidate, the City Office, a player of the session, a business or a broken key keep the written line");
        Check(FlareTextsMap.Problems().Count == 0, "flare messages' words: the map's own checks pass");
    }

    private static void FlareDmTextsWants()
    {
        var dms = new List<FlareDm>
        {
            new FlareDm { Id = 0, With = "c6", At = 100, Key = "reallife_flare_dm_chat" },
            new FlareDm { Id = 1, With = "c6", At = 110, Mine = true, Text = "you up for a coffee?", Read = true },
            new FlareDm { Id = 2, With = "c27", At = 112, Mine = true, Text = "hello", Read = true },
            new FlareDm { Id = 3, With = "c6", At = 190, Key = "reallife_flare_dm_answer_chat" },
            new FlareDm { Id = 4, With = "o:0", At = 120, Mine = true, Text = "a pothole", Read = true },
            new FlareDm { Id = 5, With = "o:0", At = 180, Key = "reallife_flare_dm_answer_office" },
            new FlareDm { Id = 6, With = "c27", At = 200, Key = "reallife_flare_dm_answer_chat" }
        };
        const int v2 = TextIntents.DataV2, v3 = TextIntents.DataV3;
        Check(FlareTextsMap.DmWants(dms[3], 111, v2) && FlareTextsMap.DmWants(dms[3], 189, v2) && !FlareTextsMap.DmWants(dms[3], 190, v2),
            "flare messages' words: an answer wants words until its minute, never once shown (a message never changes in front of the player)");
        Check(!FlareTextsMap.DmWants(dms[1], 0, v2) && !FlareTextsMap.DmWants(dms[0], 0, v2) && !FlareTextsMap.DmWants(dms[5], 0, v2) && !FlareTextsMap.DmWants(null, 0, v2),
            "flare messages' words: the player's own, a message the city starts and, with a v2 pack, the City Office's answer have no card: the written line");
        Check(FlareTextsMap.DmWants(dms[5], 0, v3) && FlareTextsMap.DmWants(dms[3], 111, v3) && !FlareTextsMap.DmWants(dms[0], 0, v3) && !FlareTextsMap.DmWants(dms[1], 0, v3)
              && FlareTextsMap.DmWants(new FlareDm { With = "f3", At = 200, Key = FlareTextsMap.DmChat }, 0, v3) && !FlareTextsMap.DmWants(new FlareDm { With = "f3", At = 200, Key = FlareTextsMap.DmChat }, 0, v2)
              && !FlareTextsMap.DmWants(new FlareDm { With = "p:host", At = 200, Key = FlareTextsMap.DmChat }, 0, v3),
            "flare messages' words: with a v3 pack the City Office's, a friend's and every city account's answer want the model's words; the city's own first messages and another player's never");
        Check(FlareTextsMap.DmAsked(dms, dms[3]) == dms[1] && FlareTextsMap.DmAsked(dms, dms[6]) == dms[2] && FlareTextsMap.DmAsked(dms, dms[0]) == null
              && FlareTextsMap.DmAsked(dms.GetRange(3, 4), dms[3]) == null,
            "flare messages' words: an answer answers the player's latest message to the same person before it (none once it has gone)");
        Check(FlareTextsMap.DmKey(dms[3]) == "fd:c6:190", "flare messages' words: the words are kept and drawn under the answer's account and minute");
    }

    private static void FlareDmTextsAskedAgain()
    {
        var q = new TextQueue();
        var hub = new TextsHub(q) { Runner = new PretendRunner() };
        var asked = new FlareTextsAsked<string>();
        var answer = new FlareDm { Id = 3, With = "c6", At = 190, Key = "reallife_flare_dm_answer_chat" };
        string key = FlareTextsMap.DmKey(answer);
        bool Ask(double now)
        {
            if (!asked.May(key))
            {
                return false;
            }
            var c = new FlareCitizen { Index = 6, First = "Mia", Last = "Hale", Handle = "@mia_hale06", Persona = FlarePersona.Student };
            var r = new TextRequest { Use = TextUse.People, Intent = FlareTextsMap.Dm(answer.Key, FlareDmSpeaker.Citizen, TextIntents.DataV2), Key = key, Speaker = "c:6", Keep = true, Day = 6, Seconds = 90 };
            FlareTextsMap.Fill(r.Who, c, "Midtown", 77, "Saturday 11:10");
            r.History.Add(new TextSaid("Alex", "you up for a coffee?"));
            r.Done = words => asked.Ended(key, words != null);
            if (!hub.Ask(r, now))
            {
                return false;
            }
            asked.Asked(key);
            return true;
        }
        Check(Ask(0) && !asked.May(key) && !Ask(1), "flare messages' words: an answer is asked once and not again while its request waits");
        hub.Clear();   // the model stopped: the device or threads changed
        Check(asked.May(key) && hub.Store.Words(key) == null, "flare messages' words: an answer handed back without words when the model stops may be asked again");
        Check(Ask(10), "flare messages' words: on the next event the answer still to come is asked again of the restarted model");
        TextJob job = q.Take(10.1, true);
        Check(job != null && job.Key == key, "flare messages' words: the asked-again answer is the queue's job");
        if (job != null)
        {
            q.Done(job, "Coffee sounds good. Tomorrow at ten?", 0.6, null);
        }
        hub.Look(11);
        Check(hub.Store.Words(key) == "Coffee sounds good. Tomorrow at ten?" && !asked.May(key),
            "flare messages' words: its words are kept under the answer's key, drawn when it shows, and not asked again");

        var posts = new FlareTextsAsked<int>();
        posts.Asked(12);
        posts.Ended(12, false);
        posts.Asked(12);
        posts.Ended(12, false);
        Check(posts.May(12), "flare messages' words: a post handed back without words may be asked again");
        posts.Asked(12);
        posts.Ended(12, false);
        Check(!posts.May(12) && FlareTextsAsked<int>.Most == 3, "flare messages' words: after three asks without words the written line stays (no endless asking)");
        posts.Clear();
        Check(posts.May(12), "flare messages' words: a new account or a stop forgets what was asked");
    }

    private static void FlareDmTextsSources()
    {
        string texts = TextsSource(@"Social\Flare\FlareDirector.Texts.cs");
        Check(texts.Contains("FlareTextsMap.DmSpeakerOf(answer.With") && !texts.Contains("With.Substring(2)"),
            "flare messages' words: the answer's speaker is read through FlareTextsMap.DmSpeakerOf, never by cutting two characters off the key");
        Check(texts.Contains("FlareTextsMap.DmWants(m, now, data)") && texts.Contains("_textDms.Ended(key, words != null)") && texts.Contains("_textAsked.Ended(id, words != null)"),
            "flare messages' words: the hour's walk asks again every answer and post still to come that came back without words");
        Check(TextsSource(@"Social\Flare\FlareDirector.Live.cs").Contains("TextsAhead();   // (the hour's answers"),
            "flare messages' words: the walk runs on the hour");
    }
}
