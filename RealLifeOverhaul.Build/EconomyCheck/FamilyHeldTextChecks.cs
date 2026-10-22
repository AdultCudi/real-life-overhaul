using System;
using System.Collections.Generic;
using System.Text.Json;
using RealLifeOverhaul.Family;

// The family's texts written at night are kept for the morning. They were kept only in memory, so a save and quit at night
// lost them. Now they are kept in the family's save (FamilySave.Held) by HeldTextRules, read back at a load and sent on the
// morning after it. The rules are checked here as they are, the save's shape by a round trip, and the game's side in its source.
internal static partial class Program
{
    private static void FamilyHeldTexts()
    {
        HeldText Text(string key, int day, string contact = "reallife_partner") =>
            new HeldText { Contact = contact, Key = key, Day = day, Data = new Dictionary<string, string> { { "name", "Alex" } } };

        var held = new List<HeldText>();
        HeldTextRules.Keep(held, Text("a", 5));
        HeldTextRules.Keep(held, Text("b", 5));
        HeldTextRules.Keep(held, null);
        HeldTextRules.Keep(held, Text(string.Empty, 5));
        HeldTextRules.Keep(held, Text("c", 5, null));
        Check(held.Count == 2 && held[0].Key == "a" && held[1].Key == "b",
            "family held texts: kept in the order written; a text with no key or no contact is not kept");
        for (int i = 0; i < HeldTextRules.Most + 3; i++)
        {
            HeldTextRules.Keep(held, Text("n" + i, 5));
        }
        Check(held.Count == HeldTextRules.Most && held[0].Key == "n3" && held[held.Count - 1].Key == "n" + (HeldTextRules.Most + 2),
            "family held texts: at most a dozen are kept, the oldest let go first");

        Check(HeldTextRules.Fresh(Text("a", 5), 5) && HeldTextRules.Fresh(Text("a", 5), 6) && !HeldTextRules.Fresh(Text("a", 5), 7)
              && !HeldTextRules.Fresh(Text("a", 6), 5) && !HeldTextRules.Fresh(null, 5),
            "family held texts: one goes out the day it was written or the next morning, never later, and never from a day still to come");

        var night = new List<HeldText> { Text("old", 3), Text("first", 5), Text("second", 6) };
        List<HeldText> due = HeldTextRules.TakeDue(night, 6);
        Check(due != null && due.Count == 2 && due[0].Key == "first" && due[1].Key == "second" && night.Count == 0,
            "family held texts: in the morning the fresh ones go out in the order written, a stale one is let go, and none is left to go twice");
        Check(HeldTextRules.TakeDue(night, 6) == null && HeldTextRules.TakeDue(null, 6) == null,
            "family held texts: with nothing kept, nothing goes out");

        List<HeldText> none = HeldTextRules.Tidy(null);
        var messy = new List<HeldText> { null, Text("a", 5), new HeldText { Contact = "reallife_family", Key = "b", Day = 5, Data = null } };
        for (int i = 0; i < HeldTextRules.Most + 2; i++)
        {
            messy.Add(Text("m" + i, 5));
        }
        List<HeldText> tidy = HeldTextRules.Tidy(messy);
        Check(none != null && none.Count == 0 && tidy.Count == HeldTextRules.Most && tidy.TrueForAll(h => h != null && h.Data != null)
              && tidy[tidy.Count - 1].Key == "m" + (HeldTextRules.Most + 1),
            "family held texts: after a load a save without them has none, and a broken or overlong list is set right");

        // The save's shape: FamilySave is written by Newtonsoft, which writes public fields by name; the same by System.Text.Json.
        var options = new JsonSerializerOptions { IncludeFields = true };
        var written = new List<HeldText>
        {
            Text("reallife_family_chore_missed", 12),
            new HeldText { Contact = "reallife_family", Key = "reallife_family_school_play_invite", Day = 13, Notify = false,
                           Data = new Dictionary<string, string> { { "child", "Mia" }, { "day", "Saturday, in 2 days" }, { "amount", "$1,200.50" } } }
        };
        string line = JsonSerializer.Serialize(written, options);
        List<HeldText> read = HeldTextRules.Tidy(JsonSerializer.Deserialize<List<HeldText>>(line, options));
        bool same = read.Count == written.Count;
        for (int i = 0; same && i < read.Count; i++)
        {
            same = read[i].Contact == written[i].Contact && read[i].Key == written[i].Key && read[i].Day == written[i].Day && read[i].Notify == written[i].Notify
                   && read[i].Data.Count == written[i].Data.Count;
            foreach (KeyValuePair<string, string> pair in written[i].Data)
            {
                same &= read[i].Data.TryGetValue(pair.Key, out string value) && value == pair.Value;
            }
        }
        Check(same && line.Contains("\"Contact\"") && line.Contains("\"Notify\":false"),
            "family held texts: a save and a load give back every kept text as written: who from, the key, what fills it, the day, its notice");
        HeldText bare = HeldTextRules.Tidy(JsonSerializer.Deserialize<List<HeldText>>("[{\"Contact\":\"reallife_partner\",\"Key\":\"k\",\"Day\":4}]", options))[0];
        Check(bare.Notify && bare.Data != null && bare.Data.Count == 0,
            "family held texts: a kept text read without its notice flag or its fillers has the game's notice and an empty fill");

        string state = ModSource(@"Family\FamilyState.cs");
        int saveAt = state.IndexOf("public sealed class FamilySave", StringComparison.Ordinal);
        int saveEnd = saveAt < 0 ? -1 : state.IndexOf("\n    }", saveAt, StringComparison.Ordinal);
        string save = saveEnd > saveAt ? state.Substring(saveAt, saveEnd - saveAt) : string.Empty;   // the class, to its closing brace
        string load = MemberText(state, "public static void Load()");
        int chore = save.IndexOf("public int ChoreAskStamp;", StringComparison.Ordinal);
        int kept = save.IndexOf("public List<HeldText> Held = new List<HeldText>();", StringComparison.Ordinal);
        Check(chore > 0 && kept > chore && save.IndexOf("public ", kept + 10, StringComparison.Ordinal) < 0 && load.Contains("S.Held = HeldTextRules.Tidy(S.Held);"),
            "family held texts: they are the last field of the family's save, so an older save loads, and a load sets them right");
        string text = ModSource(@"Family\FamilyText.cs");
        string message = MemberText(text, "public static void Message(Contact contact, string key, in Args a, bool notify = true)");
        string send = MemberText(text, "public static void SendHeld()");
        Check(message.Contains("HeldTextRules.Keep(FamilyState.S.Held, new HeldText { Contact = contact.id, Key = key, Data = data, Day = TimeHelper.CurrentDay, Notify = notify });")
              && !text.Contains("private static readonly List<Held> Morning") && send.Contains("HeldTextRules.TakeDue(kept, TimeHelper.CurrentDay)") && send.Contains("HeldFrom(h.Contact)"),
            "family held texts: a text written at night is kept in the family's save, not in memory alone, and the morning sends what the save kept");
        string director = ModSource(@"Family\FamilyDirector.cs");
        int loaded = director.IndexOf("FamilyState.Load();", StringComparison.Ordinal);
        int phone = director.IndexOf("FamilyPhone.Setup();", loaded, StringComparison.Ordinal);
        int sent = director.IndexOf("FamilyText.SendHeld();", loaded, StringComparison.Ordinal);
        Check(loaded > 0 && phone > loaded && sent > phone && MemberText(director, "private void OnSaveGame()").Contains("FamilyState.Write();"),
            "family held texts: they are written with the family's save, and after a load, once the phone's contacts are back, the day's hour sends them");
        Check(FixLineEverywhere("are now kept in your save", "ficam guardadas no seu jogo salvo", "现在会保存在存档里", "mit deinem Spielstand gespeichert",
                                "enregistrés avec votre partie", "se guardan con tu partida", "сохраняются вместе с игрой",
                                "in je opgeslagen spel bewaard", "kayıtlı oyununuzla birlikte saklanıyor"),
            "family held texts: What's new has the line, with its fix mark, in every language");
    }
}
