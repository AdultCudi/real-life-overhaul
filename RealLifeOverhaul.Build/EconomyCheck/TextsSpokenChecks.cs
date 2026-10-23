using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using RealLifeOverhaul.Texts;

// Checks for a spoken line and the player's own AI service (Scripts/Texts: TextsHub's spoken wait, Adopt and Started,
// TextQueue.Urge, TextStreamRead, the web client's streamed read; Wiring/StreetTexts, Street/StreetDirector.Talk,
// Wiring/TextsCalls): with the service up a line said face to face waits for it, a short pause at most
// TextsHub.SpokenMost, and the end of the wait is the pack's; a service still late gives the pack's line, then the
// written one; without a service nothing changes; a guess the service is still writing at the click is waited for, never
// asked again; the person is shown about to speak once the first words come, and the words only when they are whole; a
// streamed reply is read only to its last word; and every log line that says where a reply came from names the AI
// service, the pack or the written line.
internal static partial class Program
{
    private static void TextsSpoken()
    {
        SpokenWaitRule();
        SpokenRoute();
        SpokenAdopt();
        SpokenStream();
        SpokenStreamWeb();
        SpokenSources();
    }

    private static TextRequest SpokenAsk(string key, string intent, Action<string> done, double seconds = 1.1, bool guess = false)
    {
        TextRequest r = TextsAskOf(key, intent, null, done, TextPriority.Now, seconds, guess);
        r.Spoken = !guess;
        return r;
    }

    private static string S(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- the rule

    private static void SpokenWaitRule()
    {
        double hold = 1.1;
        Check(Math.Abs(TextsHub.SpokenWait(hold, 1.8, 0.35) - 2.75) < 1e-9 && TextsHub.SpokenWait(hold, 10.0, 0.35) == TextsHub.SpokenMost
              && TextsHub.SpokenWait(hold, 0.1, 0.0) == hold && TextsHub.SpokenWait(6.0, 1.8, 0.35) == 6.0,
            "spoken lines: the wait for the AI service is its measured time, a margin and the pack's share (1.8 s gives " + S(TextsHub.SpokenWait(hold, 1.8, 0.35))
            + " s), at most " + S(TextsHub.SpokenMost) + " s, never less than the line's own hold (a call's 6 s stays 6 s)");
        bool rising = true;
        for (double e = 0.0; e < 6.0; e += 0.25)
        {
            rising &= TextsHub.SpokenWait(hold, e + 0.25, 0.3) >= TextsHub.SpokenWait(hold, e, 0.3);
        }
        Check(rising && TextsHub.SpokenMost >= 3.0 && TextsHub.SpokenMost <= 4.0 && TextsHub.SpokenMargin > 0 && TextsHub.PackSlack >= 0.1,
            "spoken lines: a slower service never waits less; the cap is a natural pause (3 to 4 s), with a margin and two looks kept for the pack");
    }

    // ---------------------------------------------------------------- the route

    private static void SpokenRoute()
    {
        // No service: as it always was.
        var plainQ = new TextQueue();
        plainQ.Expected(0.15);
        var plain = new TextsHub(plainQ, new TextQueue()) { Runner = new PretendRunner(), Data = TextIntents.DataV4 };
        TextRequest none = SpokenAsk("st:1:0", "friend.crash", w => { });
        Check(plain.Ask(none, 0) && plainQ.Count == 1 && none.Waits == 1.1 && plain.ServiceQueue.Count == 0 && none.ServiceMiss == null,
            "spoken lines, no AI service: the line goes to the pack and waits its own hold of 1.1 s, as before");

        // The service up: the line waits for it.
        var packQ = new TextQueue();
        var serviceQ = new TextQueue();
        packQ.Expected(0.15);
        serviceQ.Expected(1.8);
        var service = new PretendService();
        var hub = new TextsHub(packQ, serviceQ) { Runner = new PretendRunner(), Service = service, Data = TextIntents.DataV4 };
        string got = "(none)";
        TextRequest r = SpokenAsk("st:1:1", "friend.crash", w => got = w);
        bool asked = hub.Ask(r, 10);
        TextJob job = serviceQ.Take(10.05, true);
        Check(asked && job != null && packQ.Count == 0 && Math.Abs(r.Waits - 2.75) < 1e-6 && Math.Abs(job.Deadline - 12.4) < 1e-6,
            "spoken lines, your own AI: a line the service takes 1.8 s over waits for it (" + S(r.Waits) + " s in all; the service until " + S(job != null ? job.Deadline - 10 : 0)
            + " s, the rest kept for the pack), where the 1.1 s hold refused it every time");
        serviceQ.Done(job, "Heard about 5th Ave. You alright?", 1.9, null);
        hub.Look(12.0);
        Check(got == "Heard about 5th Ave. You alright?" && r.Source == TextSource.Service && r.ByService && hub.ServiceShown == 1,
            "spoken lines, your own AI: its words in 1.9 s are the line, said as the service's");

        // Still late: the pack in the time kept for it.
        serviceQ.Expected(1.8);   // (the reply above moved the measured time a little)
        got = "(none)";
        r = SpokenAsk("st:1:2", "friend.crash", w => got = w);
        hub.Ask(r, 20);
        TextJob slow = serviceQ.Take(20.05, true);
        hub.Look(22.3);
        bool waiting = got == "(none)" && packQ.Count == 0;
        hub.Look(22.45);
        TextJob packJob = packQ.Take(22.5, true);
        Check(waiting && slow != null && packJob != null && packJob.Prompt != null && Math.Abs(packJob.Deadline - 22.75) < 1e-6,
            "spoken lines, your own AI late: the line waits for the service until its share is over, then the pack is asked in the time kept for it");
        packQ.Done(packJob, "5th Ave? Glad you are in one piece.", 0.1, null);
        hub.Look(22.65);
        Check(got == "5th Ave? Glad you are in one piece." && r.Source == TextSource.Pack && r.ServiceMiss != null && r.ServiceMiss.Contains("ran out")
              && hub.ServiceFell == 1 && hub.From(r.Source) == "the pack",
            "spoken lines, your own AI late: the pack's words are the line, named as the pack's, with why the service's were not (" + r.ServiceMiss + ")");
        serviceQ.Done(slow, "Heard about 5th Ave. All right?", 3.0, null);   // (the late reply comes back and is let go)
        hub.Look(23.5);
        Check(got == "5th Ave? Glad you are in one piece." && hub.Pending == 0, "spoken lines, your own AI late: its words that come after are let go, the line said once");

        // No pack: the written line when the service is late.
        var aloneQ = new TextQueue();
        aloneQ.Expected(1.8);
        var alone = new TextsHub(new TextQueue(), aloneQ) { Service = service };
        got = "(none)";
        r = SpokenAsk("st:1:3", "friend.crash", w => got = w);
        alone.Ask(r, 30);
        alone.Look(32.3);
        bool held = got == "(none)";
        alone.Look(32.45);
        Check(held && Math.Abs(r.Waits - 2.4) < 1e-6 && got == null && r.Source == TextSource.Written && alone.From(r.Source) == "the written line",
            "spoken lines, your own AI late with no pack: the whole wait is the service's (" + S(r.Waits) + " s), then the written line");

        // A service slower than the cap: as before, the pack at once in the line's own hold.
        serviceQ.Expected(4.0);
        r = SpokenAsk("st:1:4", "friend.crash", w => { });
        Check(hub.Ask(r, 40) && packQ.Count == 1 && serviceQ.Count == 0 && r.Waits == 1.1 && r.ServiceMiss != null && r.ServiceMiss.Contains("spoken line waits"),
            "spoken lines, your own AI slower than " + S(TextsHub.SpokenMost) + " s: the pack takes the line at once, within its own hold, and says why");
        hub.Clear();
        serviceQ.Expected(1.8);

        // A line that is not spoken, and shared text, keep their rules.
        TextRequest text = TextsAskOf("ph:1", "friend.crash", null, w => { }, TextPriority.Now, 1.1);
        Check(hub.Ask(text, 50) && serviceQ.Count == 0 && text.Waits == 1.1, "spoken lines: a request not marked spoken never waits longer than its own hold");
        hub.Clear();
        TextRequest shared = SpokenAsk("fp:2", "friend.crash", w => { });
        shared.Shared = true;
        Check(hub.Ask(shared, 51) && serviceQ.Count == 0 && packQ.Count == 1 && shared.Waits == 1.1,
            "spoken lines, Going Public: shared text still never goes to the service, and waits as before");
        hub.Clear();
    }

    // ---------------------------------------------------------------- a guess the service is still writing

    private static void SpokenAdopt()
    {
        var packQ = new TextQueue();
        var serviceQ = new TextQueue();
        packQ.Expected(0.15);
        serviceQ.Expected(1.8);
        var hub = new TextsHub(packQ, serviceQ) { Runner = new PretendRunner(), Service = new PretendService(), Data = TextIntents.DataV4 };
        TextRequest guess = SpokenAsk("stg:7", "friend.crash", w => { }, 12.0, true);
        hub.Ask(guess, 0);
        TextJob job = serviceQ.Take(0.05, true);
        Check(hub.Writing("stg:7") && !hub.Writing("stg:8"), "spoken lines, a guess: the hub tells a guess the service is still writing");
        string got = "(none)";
        int started = 0;
        TextRequest line = SpokenAsk("st:7:0", "friend.crash", w => got = w);
        line.Started = () => started++;
        bool adopted = hub.Adopt("stg:7", line, 0.5);
        Check(adopted && hub.Pending == 1 && serviceQ.Count == 0 && packQ.Count == 0 && !job.Guess && job.Priority == TextPriority.Now && Math.Abs(job.Deadline - 2.4) < 1e-6
              && Math.Abs(line.Waits - 2.25) < 1e-6,
            "spoken lines, a guess: at the click the guess still being written is waited for as the line (" + S(line.Waits) + " s on what the service still needs), never asked again");
        hub.Look(0.6);
        bool quiet = started == 0;
        job.Speaking = true;
        hub.Look(0.7);
        hub.Look(0.8);
        Check(quiet && started == 1 && got == "(none)",
            "spoken lines, streamed: the asker is told once when the service's first words come, and nothing of the words is handed over yet");
        serviceQ.Done(job, "Heard about 5th Ave. You alright?", 1.2, null);
        hub.Look(1.3);
        Check(got == "Heard about 5th Ave. You alright?" && line.Source == TextSource.Service && hub.From(line.Source).StartsWith("your own AI", StringComparison.Ordinal),
            "spoken lines, a guess: its words come through the line, named as your own AI's");

        TextRequest other = SpokenAsk("stg:9", "friend.crash", w => { }, 12.0, true);
        hub.Ask(other, 5);
        TextRequest news = SpokenAsk("st:9:0", "friend.news.good", w => { });
        Check(!hub.Adopt("stg:9", news, 5.2) && hub.Pending == 0 && !hub.Writing("stg:9"),
            "spoken lines, a guess: a guess of another card is let go, and the line is asked as usual");

        var down = new TextsHub(packQ, serviceQ) { Runner = new PretendRunner(), Data = TextIntents.DataV4 };
        down.Ask(SpokenAsk("stg:10", "friend.crash", w => { }, 12.0, true), 6);
        Check(!down.Writing("stg:10") && down.TakeGuess("stg:10", out TextSource from) == null && from == TextSource.Written && down.Pending == 0,
            "spoken lines, no AI service: a guess the pack has not written is let go at the click, as before");
        down.Clear();
        hub.Clear();
    }

    // ---------------------------------------------------------------- the stream

    private static void SpokenStream()
    {
        string delta1 = "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Saw the \"}";
        string delta2 = "data: {\"type\":\"response.output_text.delta\",\"delta\":\"news. Proud of you.\"}";
        string done = "data: {\"type\":\"response.output_text.done\",\"text\":\"Saw the news. Proud of you.\"}";
        string completed = "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\"}}";
        int begun = 0;
        var read = new TextStreamRead { OnBegun = () => begun++ };
        bool r0 = read.Feed("event: response.created"), r1 = read.Feed("data: {\"type\":\"response.created\"}"), r2 = read.Feed(delta1);
        bool after1 = read.Begun && begun == 1;
        bool r3 = read.Feed(delta2), r4 = read.Feed(done), r5 = read.Feed(completed);
        Check(!r0 && !r1 && !r2 && after1 && !r3 && r4 && r5 && begun == 1 && read.Whole,
            "spoken lines, streamed: the first words are told once, and the answer is whole at its last word (output_text.done), not at the stream's close");
        var empty = new TextStreamRead();
        empty.Feed("data: {\"type\":\"response.output_text.delta\",\"delta\":\"\"}");
        Check(!empty.Begun && !empty.Feed(null) && !empty.Feed("data: {\"type\":\"response.in_progress\"}"),
            "spoken lines, streamed: an empty piece is not the first words, and other events end nothing");
        string upToDone = delta1 + "\n\n" + delta2 + "\n\n" + done + "\n";
        TextServiceResult early = TextServiceRules.Read(TextApi.Responses, new TextWebAnswer { Status = 200, Body = upToDone });
        TextServiceResult cut = TextServiceRules.Read(TextApi.Responses, new TextWebAnswer { Status = 200, Body = delta1 + "\n\n" + delta2 + "\n" });
        Check(early.Fault == TextFault.None && early.Words == "Saw the news. Proud of you." && cut.Fault == TextFault.NoWords,
            "spoken lines, streamed: a stream read to its last word is the reply, whole; one cut before it is still no reply (nothing partial)");
    }

    // A pretend ChatGPT that streams the reply, then holds the connection open for seconds before it completes: the web
    // client must come back at the last word.
    private static void SpokenStreamWeb()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serve = new Thread(() =>
        {
            try
            {
                using (TcpClient c = listener.AcceptTcpClient())
                {
                    NetworkStream net = c.GetStream();
                    var buf = new byte[8192];
                    var all = new StringBuilder();
                    while (true)
                    {
                        int n = net.Read(buf, 0, buf.Length);
                        if (n <= 0)
                        {
                            break;
                        }
                        all.Append(Encoding.UTF8.GetString(buf, 0, n));
                        string t = all.ToString();
                        int head = t.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                        var m = System.Text.RegularExpressions.Regex.Match(t, @"Content-Length:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (head >= 0 && (!m.Success || Encoding.UTF8.GetByteCount(t.Substring(head + 4)) >= int.Parse(m.Groups[1].Value)))
                        {
                            break;
                        }
                    }
                    void Send(string s)
                    {
                        byte[] b = Encoding.UTF8.GetBytes(s);
                        net.Write(b, 0, b.Length);
                        net.Flush();
                    }
                    Send("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nConnection: close\r\n\r\n");
                    Send("data: {\"type\":\"response.created\"}\n\n");
                    Thread.Sleep(150);
                    Send("data: {\"type\":\"response.output_text.delta\",\"delta\":\"On my \"}\n\n");
                    Thread.Sleep(250);
                    Send("data: {\"type\":\"response.output_text.delta\",\"delta\":\"way.\"}\n\ndata: {\"type\":\"response.output_text.done\",\"text\":\"On my way.\"}\n\n");
                    Thread.Sleep(3000);   // (the service's usage and closing events, long after the last word)
                    Send("data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\"}}\n\n");
                }
            }
            catch (Exception)
            {
                // (the client left at the last word: as it should)
            }
        }) { IsBackground = true };
        serve.Start();
        long firstAt = -1;
        var watch = Stopwatch.StartNew();
        var read = new TextStreamRead { OnBegun = () => firstAt = watch.ElapsedMilliseconds };
        var request = new TextWebRequest { Url = "http://127.0.0.1:" + port + "/backend-api/codex/responses", Body = "{}", Lines = read.Feed }.Header("Accept", "text/event-stream");
        TextWebAnswer answer = new TextWeb().Send(request, 5000);
        long ms = watch.ElapsedMilliseconds;
        listener.Stop();
        TextServiceResult result = TextServiceRules.Read(TextApi.Responses, answer);
        Check(answer.Status == 200 && result.Fault == TextFault.None && result.Words == "On my way." && ms < 2000 && firstAt >= 0 && firstAt < ms,
            "spoken lines, streamed: the web client reads the reply as it comes and comes back at its last word (" + ms + " ms; the service closed 3 s later), the first words told at " + firstAt + " ms");
    }

    // ---------------------------------------------------------------- the sources

    private static void SpokenSources()
    {
        string texts = TextsSource(@"Wiring\StreetTexts.cs"), talk = TextsSource(@"Street\StreetDirector.Talk.cs"), game = TextsSource(@"Street\StreetGame.cs") + TextsSource(@"Street\StreetHold.cs");
        string calls = TextsSource(@"Wiring\Wiring.TextsCalls.cs"), runner = TextsSource(@"Texts\TextService.cs"), web = TextsSource(@"Texts\TextWeb.cs");
        Check(texts.Contains("Spoken = !guess, Started = guess ? null : c.Speaking") && talk.Contains("_openBy = now + (float)Math.Max(StreetRules.Hold, chat.Waits)")
              && texts.Contains("c.Waits = r.Waits;") && texts.Contains("Adopted(ref guessed, r)") && texts.Contains("if (hub.Writing(key))") && calls.Contains("Spoken = true"),
            "spoken lines: street talk's lines and a call's answers are marked spoken; the panel opens by the line's own wait; a guess still being written is waited for");
        Check(talk.Contains("string how = model ? c.Heard ?? \"RLO Texts\"") && talk.Contains("\"the written line \" + key") && texts.Contains("TextsDirector.Words.From(r.Source) + \", \" + Card(r)")
              && texts.Contains("c.Heard = hub.From(from) + (said != null") && calls.Contains("TextsDirector.Words.From(r.Source) + \", \" + r.Intent")
              && !talk.Contains("\"written \" + key"),
            "spoken lines, the log: every turn line and call line names whose words they were: your own AI, the pack, or the written line");
        int line = talk.IndexOf("private DialogEntry Line(StreetLine kind", StringComparison.Ordinal);
        string lineBody = line >= 0 ? talk.Substring(line, Math.Min(700, talk.Length - line)) : string.Empty;
        int release = game.IndexOf("public void Release()", StringComparison.Ordinal);
        string releaseBody = release >= 0 ? game.Substring(release, Math.Min(400, game.Length - release)) : string.Empty;
        Check(lineBody.Contains("_hold.Quiet();") && releaseBody.Contains("Unspeak(body);") && game.Contains("ActivityAnimators.Forget(body.animator);")
              && game.Contains("if (_kind == StreetFigure.Seated)") && talk.Contains("bool awaited = _chat != null && (_pending || (_phase == 1 && _firstAsked && !_firstCame));"),
            "spoken lines, about to speak: the hands move only while a line is awaited, come to rest when it shows, and the figure is handed back to the game's animation when let go");
        Check(runner.Contains("request.Lines = read.Feed;") && runner.Contains("_start.Provider.Api == TextApi.Responses") && web.Contains("streamed ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead")
              && web.Contains("if (lines(line))"),
            "spoken lines, streamed: only the streamed route is read line by line, and the read stops at the last word; every other answer is read whole as before");
    }
}
