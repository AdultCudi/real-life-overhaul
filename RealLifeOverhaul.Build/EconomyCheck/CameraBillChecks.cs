using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// One incident, one punishment (Justice/JusticeLaw.cs, Traffic/PolicePursuit.cs): a hit-and-run the player got away from,
// the plate seen, went to court by summons and was also billed by the traffic camera hours later, so a case dismissed
// for want of evidence was followed by its fine. The case now takes the whole bill: nothing waits beside it, a dismissal
// owes nothing, and a conviction's sentence is the fine with the damages the case holds.
internal static partial class Program
{
    private static void CameraBills()
    {
        CameraBillRules();
        CameraBillSave();
        CameraBillSource();
        CameraBillTexts();
    }

    private static void CameraBillRules()
    {
        Check(JusticeLaw.LaterBill(1200, 8000, caseTookIt: false) == 9200, "camera bill: with no court case the damages and the fine are billed later, as before");
        Check(JusticeLaw.LaterBill(1200, 8000, caseTookIt: true) == 0, "camera bill: once a case takes the incident nothing is billed later, so a dismissal is never followed by a fine");
        Check(JusticeLaw.LaterBill(0, 0, caseTookIt: false) == 0 && JusticeLaw.LaterBill(-50, -10, caseTookIt: false) == 0,
            "camera bill: nothing owed, or a figure below nothing, bills nothing");
        Check(JusticeLaw.LaterBill(400, 0, caseTookIt: false) == 400, "camera bill: damages alone, with no one hurt and no case, are still billed (a separate matter)");
        Check(JusticeLaw.ConvictionDue(8000, 0, 1200) == 9200, "camera bill: a conviction is the court's fine and the damages the case held, once each");
        Check(JusticeLaw.ConvictionDue(8000, 3000, 0) == 5000, "camera bill: what was paid at the roadside is still taken off the court's fine");
        Check(JusticeLaw.ConvictionDue(2000, 5000, 600) == 600, "camera bill: a roadside payment above the fine never eats into the damages, nor goes below nothing");
        Check(JusticeLaw.ConvictionDue(0, 0, -5) == 0, "camera bill: damages below nothing count as none");

        // The whole sequence the player met, by the rules: escaped, summoned, dismissed. Billed later plus charged by the court.
        int dismissed = JusticeLaw.LaterBill(1200, 8000, caseTookIt: true);
        int convicted = JusticeLaw.LaterBill(1200, 8000, caseTookIt: true) + JusticeLaw.ConvictionDue(8000, 0, 1200);
        int noCourt = JusticeLaw.LaterBill(1200, 8000, caseTookIt: false);
        Check(dismissed == 0 && convicted == noCourt, "camera bill: dismissed costs nothing, and a conviction costs what the bill alone would have, never both ("
                                                      + dismissed + ", " + convicted + ", " + noCourt + ")");
    }

    private static void CameraBillSave()
    {
        JusticeState.Clear();
        JusticeCase c = JusticeState.Open(4);
        c.Charges.Add(new JusticeCharge { Id = JusticeLaw.HitRunInjury, Evidence = 0.4f });
        c.Damages = 1234;
        JusticeCase none = JusticeState.Open(4);
        none.Charges.Add(new JusticeCharge { Id = JusticeLaw.HitRun, Evidence = 0.3f });
        string line = JusticeState.Write();
        JusticeState.Clear();
        JusticeState.Parse(line);
        bool back = JusticeState.Cases.Count == 2 && JusticeState.Cases[0].Damages == 1234 && JusticeState.Cases[1].Damages == 0
                    && JusticeState.Cases[0].Charges.Count == 1 && JusticeState.Cases[0].Charges[0].Id == JusticeLaw.HitRunInjury;
        Check(back, "camera bill: the damages a case holds come back from the save, its charges with them");

        // A line from before the field: the case line ends with its charges.
        string older = Regex.Replace(line, @",1234(?=;|\|)", string.Empty);
        older = Regex.Replace(older, @",0(?=\|D:)", string.Empty);
        JusticeState.Clear();
        JusticeState.Parse(older);
        bool olderReads = JusticeState.Cases.Count == 2 && JusticeState.Cases[0].Damages == 0 && JusticeState.Cases[0].Charges.Count == 1;
        Check(older != line && olderReads, "camera bill: a save from before holds no damages and still loads every case with its charges");
        JusticeState.Clear();
    }

    private static void CameraBillSource()
    {
        string pursuit = TextsSource("Traffic/PolicePursuit.cs");
        string bill = Between(pursuit, "internal static JusticeCase SummonOrBill(", "private void Reset()");
        Check(Regex.IsMatch(bill, @"Justice\.Summon\(JusticeReports\.Escaped\(killed, owed\)\)") && bill.Contains("JusticeLaw.LaterBill(owed, fine, heard != null)")
              && bill.IndexOf("Justice.Summon(", System.StringComparison.Ordinal) < bill.IndexOf("ModState.AddPendingFine(", System.StringComparison.Ordinal),
            "camera bill: the getaway's bill is queued only after the summons, for what no case took");
        string attention = TextsSource("Crime/PoliceAttention.Stop.cs");
        Check(attention.Contains("PolicePursuit.BillLater(") && !attention.Contains("AddPendingFine("),
            "camera bill: a stop the player drove away from bills through the same one path");
        int queued = 0;
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        foreach (string file in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            queued += Regex.Matches(System.IO.File.ReadAllText(file), @"ModState\.AddPendingFine\(").Count;
        }
        Check(queued == 1, "camera bill: one place in the mod queues a later fine, the one that asks whether a case took it (" + queued + ")");
        string sentence = Between(TextsSource("Justice/CourtHearing.cs"), "private void Sentence(", "if (sentence.PrisonDays > 0)");
        Check(sentence.Contains("JusticeLaw.ConvictionDue(sentence.Fine, _case.PaidAtScene, _case.Damages)"), "camera bill: a conviction charges the damages the case holds");
        string cases = TextsSource("Justice/JusticeDirector.Cases.cs");
        Check(cases.Contains("c.Damages += Mathf.Max(0, report.Damages);"), "camera bill: a summons hands its damages to the case, a joined one too");
        Check(TextsSource("Justice/JusticeSettings.cs").Contains("\"reallife_justice_test_camera\", () => JusticeDirector.Instance?.TestCameraDismissed()"),
            "camera bill: the test button that plays the reported sequence is on the options page");
    }

    private static void CameraBillTexts()
    {
        string[] keys =
        {
            "reallife_justice_sentence_damages", "reallife_justice_file_case_damages", "reallife_justice_test_camera", "reallife_justice_test_camera_pass",
            "reallife_justice_test_camera_fail"
        };
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(LocalesFolder(), lang + ".json"));
            bool all = words != null;
            foreach (string key in keys)
            {
                all &= words != null && words.TryGetValue(key, out string text) && text.Length > 0
                       && (key != "reallife_justice_sentence_damages" || (text.Contains("{amount}") && text.Contains("{due}")));
            }
            Check(all, "camera bill: every new text is in " + lang + ", the sentence's with {amount} and {due}");
        }
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        Check(en != null && en["reallife_guide_ragdoll"].Contains("When a summons follows, the case takes this bill"), "camera bill: the Guide says the case takes the bill");
        Check(AnyChangesRow(en, "no longer costs you twice"), "camera bill: What's new says so");
    }
}
