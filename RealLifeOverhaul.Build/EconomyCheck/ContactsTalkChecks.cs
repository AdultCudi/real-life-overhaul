using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// The Contacts thread keeps a conversation (living/fix-contacts-thread): the owner talked to a contact and the chat pane showed no
// trace of it, because the game keeps a contact's texts only (Contact.messagesQueue) and never what a call said. The mod keeps its
// own short history of each contact's calls (Phone/Contacts/TalkLog, saved by TalkLogStore under reallife:contactstalk, written
// by TalkLogGame from every page one of its dialogs shows and from the player's replies and typed words), and the kit's thread
// (Ui/Contacts/ContactsScreen.Read.cs) lays it among the game's texts in the order it happened. Here: the history's order, caps,
// cleaning, forgetting and save, an old save without it, the order against the game's texts, and the places that keep and read it.
internal static partial class Program
{
    private static void ContactsTalk()
    {
        const int day = 60 * 24;
        var log = new TalkLog();
        Check(log.Of("mom") == null && !log.Full("mom") && log.PeopleCount == 0, "contacts talk: a contact with no calls has no history");
        bool added = log.Add("mom", 3 * day + 600, false, "Hi, sweetheart. How was your day?");
        added &= log.Add("mom", 3 * day + 601, true, "Good, thanks.");
        added &= log.Add("mom", 3 * day + 601, false, "I am glad.");
        List<TalkLine> mom = log.Of("mom");
        Check(added && mom != null && mom.Count == 3 && !mom[0].Mine && mom[1].Mine && !mom[2].Mine && mom[0].Text.StartsWith("Hi, sweetheart", StringComparison.Ordinal)
              && mom[1].Text == "Good, thanks." && mom[2].Minute == 3 * day + 601,
            "contacts talk: their line, the player's answer and their reaction are kept in the order they happened, each as whose it was");

        int changes = log.Changes;
        Check(!log.Add("mom", 3 * day + 601, false, "I am glad.") && log.Of("mom").Count == 3 && log.Changes == changes
              && log.Add("mom", 3 * day + 602, false, "I am glad."),
            "contacts talk: a page shown twice in the same minute is one line; the same words a minute later are a new line");
        Check(!log.Add(null, 5, false, "x") && !log.Add("", 5, false, "x") && !log.Add("mom", 5, false, "  ") && !log.Add("mom", 5, false, null),
            "contacts talk: no contact or no words keeps nothing");
        log.Add("alex", 9, false, "<line one>\n  line | two ^ three\t" + new string('w', 400));
        TalkLine alex = log.Of("alex")[0];
        Check(alex.Text.IndexOf('\n') < 0 && alex.Text.IndexOf('|') < 0 && alex.Text.IndexOf('^') < 0 && alex.Text.IndexOf("  ", StringComparison.Ordinal) < 0 && alex.Text.Length <= TalkLog.LineMost
              && alex.Text.StartsWith("<line one> line two three ", StringComparison.Ordinal),
            "contacts talk: a line is one plain line of at most " + TalkLog.LineMost + " characters, with nothing the save splits on");

        var long30 = new TalkLog();
        for (int i = 0; i < 45; i++)
        {
            long30.Add("tow", 100 + i, i % 2 == 0, "line " + i);
        }
        List<TalkLine> tow = long30.Of("tow");
        Check(tow.Count == TalkLog.LinesKept && tow[0].Text == "line 15" && tow[tow.Count - 1].Text == "line 44" && long30.Full("tow") && !long30.Full("mom"),
            "contacts talk: a contact keeps its newest " + TalkLog.LinesKept + " lines, the oldest go first");

        var many = new TalkLog();
        for (int i = 0; i < TalkLog.MostPeople + 5; i++)
        {
            many.Add("npc" + i, 1000 + i, false, "hello");
        }
        Check(many.PeopleCount == TalkLog.MostPeople && many.Of("npc0") == null && many.Of("npc4") == null && many.Of("npc5") != null && many.Of("npc" + (TalkLog.MostPeople + 4)) != null,
            "contacts talk: at most " + TalkLog.MostPeople + " contacts have a history, the one talked to longest ago goes first");

        Check(log.Clear("mom") && log.Of("mom") == null && !log.Clear("mom") && log.Of("alex") != null, "contacts talk: clearing a chat forgets that contact's history and no other");
        var kept = new HashSet<string>(StringComparer.Ordinal) { "alex" };
        log.Add("gone", 12, false, "bye");
        int forgot = log.Keep(kept);
        Check(forgot == 1 && log.Of("gone") == null && log.Of("alex") != null && log.PeopleCount == 1, "contacts talk: a deleted contact takes its history with it at the save");

        // The save: written, read back as written, an old save, a later one, nonsense.
        var back = new TalkLog();
        back.Add("mom", 3 * day + 600, false, "Hi, sweetheart. How was your day?");
        back.Add("mom", 3 * day + 601, true, "Good, thanks.");
        back.Add("uncle_fred", 4 * day + 5, false, "Ah, there you are.");
        string text = TalkLogStore.Write(back);
        TalkLog read = TalkLogStore.Read(text);
        Check(read != null && read.PeopleCount == 2 && read.Of("mom").Count == 2 && read.Of("mom")[1].Mine && read.Of("mom")[1].Minute == 3 * day + 601 && read.Of("uncle_fred")[0].Text == "Ah, there you are."
              && TalkLogStore.Write(read).Length == text.Length && text.StartsWith("v=1^l=", StringComparison.Ordinal) && TalkLogStore.Key == "reallife:contactstalk",
            "contacts talk: the history reads back from the save as it was written, under reallife:contactstalk");
        TalkLog none = TalkLogStore.Read(null), blank = TalkLogStore.Read(string.Empty), only = TalkLogStore.Read("v=1");
        Check(none != null && none.PeopleCount == 0 && blank != null && only != null && only.PeopleCount == 0,
            "contacts talk: a save from before the history (no line) loads with none");
        Check(TalkLogStore.Read("v=2^l=mom|5|t|hi") == null && TalkLogStore.Read("v=1^l=mom|x|t|hi") == null && TalkLogStore.Read("v=1^l=mom|5|q|hi") == null && TalkLogStore.Read("v=1^l=mom|5|t") == null
              && TalkLogStore.Read("v=1^zz") == null && TalkLogStore.Read("v=1^l=mom|-5|t|hi") == null,
            "contacts talk: a line of a later version or one that cannot be read gives nothing (and is kept as read)");
        TalkLog later = TalkLogStore.Read("v=1^l=mom|5|t|hi^q=a later kind of record");
        Check(later != null && later.Of("mom") != null && later.Of("mom").Count == 1, "contacts talk: a record of a later version's kind is passed over");

        // The thread reads the history among the texts by the game's minute; the day and time of a line.
        Check(TalkLogRules.Before(10, 11) && !TalkLogRules.Before(11, 11) && !TalkLogRules.Before(12, 11), "contacts talk: a call's line comes before a text of a later minute, after one of the same minute or earlier");
        Check(TalkLogRules.DayOf(3 * day + 61) == 3 && TalkLogRules.HourOf(3 * day + 61) == 1 && TalkLogRules.MinuteOf(3 * day + 61) == 1 && TalkLogRules.HourOf(day - 1) == 23 && TalkLogRules.DayOf(-4) == 0,
            "contacts talk: a line's game minute gives its day, hour and minute");

        // Cost: an event keeps a line; the screen looks the history up only when it lays out.
        var timed = new TalkLog();
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 2000; i++)
        {
            timed.Add("c" + i % 30, i, i % 3 == 0, "a short line of talk " + i);
            timed.Of("c" + i % 30);
        }
        watch.Stop();
        Check(watch.Elapsed.TotalMilliseconds < 200.0, "contacts talk: two thousand lines kept and looked up take under 200 ms (" + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms)");

        // The places that keep and read it.
        string guard = TextsSource(@"Core\GuardCalls.cs");
        string game = TextsSource(@"Phone\Contacts\TalkLogGame.cs");
        string person = TextsSource(@"Social\PersonDialog.cs") + TextsSource(@"Social\PersonDialog.Composer.cs") + TextsSource(@"Social\PersonDialog.CallBack.cs");
        string thread = TextsSource(@"Ui\Contacts\ContactsScreen.Read.cs");
        string screen = TextsSource(@"Ui\Contacts\ContactsScreen.cs");
        Check(System.Text.RegularExpressions.Regex.IsMatch(guard, @"^\s*TalkLogGame\.Page\(entry\);", System.Text.RegularExpressions.RegexOptions.Multiline) && game.Contains("dialog.contact") && game.Contains("page.Template != DialogEntry.TemplateType.Text") && game.Contains("OwnPages"),
            "contacts talk: every page one of the mod's dialogs shows (Guard.Entry) in a contact's call is kept as that contact's line, forms and the player's own words left out");
        Check(person.Contains("TalkLogGame.Said(SocialLife.T(warm ? topic.ReplyA : topic.ReplyB, data))") && person.Contains("Chose(topic, warm, data);") && person.Contains("TalkLogGame.Said(words);")
              && person.Contains("TalkLogGame.Own(Mine(words))"),
            "contacts talk: the answer the player pressed and the words the player typed are kept as the player's lines");
        Check(thread.Contains("TalkLogGame.Lines(c)") && thread.Contains("TalkLogRules.Before(talk[said].Minute, minute)") && thread.Contains("sig = sig * 31 + TalkLogGame.Changes;")
              && thread.Contains("TalkLogGame.Clear(open)") && screen.Contains("_clearing = b == ButtonOf(GameContract.KitClearChat) ? open : _clearing;"),
            "contacts talk: the Contacts thread lays the history among the texts, looks again when it changes, and forgets it with a cleared chat");
        Check(game.Contains("SaveHook.Keep(") && game.Contains("TalkLogStore.Write(_log)") && game.Contains("SaveLines.Unreadable(save.modData, TalkLogStore.Key") && game.Contains("_log.Keep(ids)"),
            "contacts talk: the history is read with the save (a line that cannot be read is kept as it was), written with it, and pruned of deleted contacts");
        Check(FixLineEverywhere("the Contacts chat now keeps what was said in a call",
                                "o chat dos Contatos agora guarda o que foi dito numa ligação",
                                "联系人聊天现在会保留通话中说过的内容",
                                "Der Chat in den Kontakten behält jetzt",
                                "le fil des Contacts garde maintenant ce qui a été dit pendant un appel",
                                "el chat de Contactos ahora guarda lo que se dijo en una llamada",
                                "чат в Контактах теперь хранит то, что было сказано во время звонка",
                                "de chat in Contacten bewaart nu wat er in een gesprek is gezegd",
                                "Kişiler'deki sohbet artık bir aramada söylenenleri saklıyor"),
            "contacts talk: What's new has the line of the kept conversation, with its fix mark, in some row of every language");
        bool marked = true;
        foreach ((string code, string piece) in new[] { ("pl", "czat w Kontaktach zachowuje teraz"), ("ko", "연락처 채팅이 이제 통화에서 오간 말을") })
        {
            Dictionary<string, string> own = LoadWords(System.IO.Path.Combine(LocalesFolder(), code + ".json"));
            string row = own != null ? RowHolding(own, piece) : string.Empty;
            int at = row.IndexOf(piece, StringComparison.Ordinal);
            marked &= at > 0 && row.LastIndexOf(FixMarks[code], at, StringComparison.Ordinal) >= 0;
        }
        Check(marked, "contacts talk: Polish and Korean have the line too, after their own fix marks");
    }
}
