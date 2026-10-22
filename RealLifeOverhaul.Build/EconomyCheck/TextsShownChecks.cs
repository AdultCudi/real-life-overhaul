using System;
using RealLifeOverhaul.Texts;

// Checks for what RLO Texts shows (Texts/TextShown.cs): every line the game had counted by pack and intent, the model's
// words against the written lines and why each written line was shown (no pack, off, loading, busy, slow, refused by
// which rule, no card, the language, no longer wanted, failed), counted by the hub on each way a line goes; the hourly
// line for Player.log and its readout; the rows bounded; a new session starting again.
internal static partial class Program
{
    private static void TextsShownSix()
    {
        var t = new TextShown();
        t.Count("RLO Texts", "street.greet", TextShownWhy.Model);
        t.Count("RLO Texts", "street.greet", TextShownWhy.Model);
        t.Count("RLO Texts", "street.greet", TextShownWhy.Refused, TextVerdict.Long);
        t.Count("RLO Texts", "friend.crash", TextShownWhy.Slow);
        t.Count("RLO Texts Lite", "friend.crash", TextShownWhy.Loading);
        var why = new int[Enum.GetValues(typeof(TextShownWhy)).Length];
        var rule = new int[Enum.GetValues(typeof(TextVerdict)).Length];
        int lines = t.Totals("RLO Texts", why, rule);
        Check(lines == 4 && why[(int)TextShownWhy.Model] == 2 && why[(int)TextShownWhy.Refused] == 1 && rule[(int)TextVerdict.Long] == 1 && why[(int)TextShownWhy.Slow] == 1
              && string.Join(",", t.Packs()) == "RLO Texts,RLO Texts Lite" && t.Rows.Count == 3 && t.Rows[0].Written == 1,
            "texts v6, what was shown: each line counted by pack and intent, the model's words against the written lines, a refusal by its rule");
        string line = t.Line("day 3, 14:00");
        Check(line != null && line.StartsWith("RLO Texts check (what was shown, day 3, 14:00): RLO Texts: the model 2, written 2 (slow 1; refused 1: Long 1), by intent: street.greet 2/1, friend.crash 0/1; RLO Texts Lite: the model 0, written 1 (loading 1)", StringComparison.Ordinal)
              && t.Line("again") == null && t.Since == 0,
            "texts v6, what was shown: the hourly line for Player.log says each pack's counts and reasons by intent, once for what came since (" + line + ")");
        var big = new TextShown();
        for (int i = 0; i < TextShown.RowsMost + 50; i++)
        {
            big.Count("RLO Texts", "intent." + i, TextShownWhy.Model);
        }
        Check(big.Rows.Count == TextShown.RowsMost + 1 && big.Rows[TextShown.RowsMost].Intent == "other" && big.Rows[TextShown.RowsMost].Why[0] == 50,
            "texts v6, what was shown: at most " + TextShown.RowsMost + " pairs of pack and intent, the rest counted under 'other'");
        big.Clear();
        Check(big.Rows.Count == 0 && big.Packs().Count == 0 && big.Line(null) == null, "texts v6, what was shown: a new session starts the counts again");
        var names = new System.Collections.Generic.HashSet<string>();
        foreach (TextShownWhy w in Enum.GetValues(typeof(TextShownWhy)))
        {
            names.Add(TextShown.Name(w));
        }
        Check(names.Count == why.Length, "texts v6, what was shown: every reason has its own name in the log");

        // the hub counts each way a line goes
        var hub = new TextsHub(new TextQueue()) { Pack = "RLO Texts" };
        hub.Ask(TextsAskOf("a", "friend.crash", null, w => { }), 0);                  // no runner, a pack named: it could not run
        var none = new TextsHub(new TextQueue());
        none.Ask(TextsAskOf("b", "friend.crash", null, w => { }), 0);                 // no pack at all
        none.NotAsked(TextUse.People, false, "friend.crash");
        hub.Runner = new PretendRunner();
        hub.Allowed = (use, shared) => false;
        hub.Ask(TextsAskOf("c", "friend.crash", null, w => { }), 0);                  // switched off
        hub.Allowed = null;
        hub.English = false;
        hub.Ask(TextsAskOf("d", "friend.crash", null, w => { }), 0);                  // the pack does not write the game's language (an older pack, the game not in English)
        hub.English = true;
        var loading = new PretendRunner { State = TextServerState.Loading };
        hub.Runner = loading;
        hub.Ask(TextsAskOf("e", "friend.crash", null, w => { }, TextPriority.Now, 5), 0);   // still loading, due soon
        hub.Runner = new PretendRunner();
        hub.AheadOnly = true;
        hub.Ask(TextsAskOf("f", "friend.crash", null, w => { }, TextPriority.Now, 5), 0);   // a slow machine writes only what is asked ahead
        hub.AheadOnly = false;
        TextRequest missing = TextsAskOf("g", "friend.crash", null, w => { });
        missing.Slots.Clear();
        hub.Ask(missing, 0);                                                           // a value the card needs is missing
        hub.Ask(TextsAskOf("h", "street.day", null, w => { }), 0);                     // no such card for this pack (the hub's data 2)
        hub.Ask(TextsAskOf("i", "friend.crash", null, w => { }), 1);
        hub.Ask(TextsAskOf("i", "friend.crash", null, w => { }), 1);                   // asked again: the older gives way
        TextJob job = hub.Queue.Take(1.1, true);
        hub.Queue.Done(job, "Heard about 5th Ave! Wow!", 1.0, null);
        hub.Look(2);                                                                   // refused: '!'
        hub.Ask(TextsAskOf("j", "friend.crash", null, w => { }, TextPriority.Soon, 4), 3);
        hub.Look(9);                                                                   // its deadline passed
        hub.Ask(TextsAskOf("k", "friend.crash", null, w => { }), 10);
        job = hub.Queue.Take(10.1, true);
        hub.Queue.Done(job, "Heard about 5th Ave. You alright?", 1.0, null);
        hub.Look(11);                                                                  // the model's words
        var all = new int[why.Length];
        var rules = new int[rule.Length];
        hub.Tally.Totals("RLO Texts", all, rules);
        var nothing = new int[why.Length];
        none.Tally.Totals(TextsHub.NoPack, nothing, rules);
        string seen = "failed " + all[(int)TextShownWhy.Failed] + ", off " + all[(int)TextShownWhy.Off] + ", language " + all[(int)TextShownWhy.Language] + ", loading " + all[(int)TextShownWhy.Loading]
                      + ", slow " + all[(int)TextShownWhy.Slow] + ", no card " + all[(int)TextShownWhy.NoCard] + ", unwanted " + all[(int)TextShownWhy.Unwanted] + ", refused "
                      + all[(int)TextShownWhy.Refused] + ", model " + all[(int)TextShownWhy.Model] + "; no pack " + nothing[(int)TextShownWhy.NoPack];
        hub.Tally.Totals("RLO Texts", all, rules);
        Check(all[(int)TextShownWhy.Failed] == 1 && all[(int)TextShownWhy.Off] == 1 && all[(int)TextShownWhy.Language] == 1 && all[(int)TextShownWhy.Loading] == 1
              && all[(int)TextShownWhy.Slow] == 2 && all[(int)TextShownWhy.NoCard] == 2 && all[(int)TextShownWhy.Unwanted] == 1 && all[(int)TextShownWhy.Refused] == 1
              && rules[(int)TextVerdict.Exclaim] == 1 && all[(int)TextShownWhy.Model] == 1 && nothing[(int)TextShownWhy.NoPack] == 2,
            "texts v6, what was shown: the hub counts every way a line goes: " + seen);
    }
}
