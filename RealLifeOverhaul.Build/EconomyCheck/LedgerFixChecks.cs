using System;
using System.Collections.Generic;

// Faults found in Scripts/Ledger: a kiosk charged for today's paper when the page could not open; and the printed
// subscription gave back every morning the round missed, paid for or not (LedgerChecks has the rule's cases).
internal static partial class Program
{
    private static void LedgerFixes()
    {
        string director = ModSource(@"Ledger\LedgerDirector.cs");
        string buy = MemberText(director, "public bool BuyCopy()");
        int test = buy.IndexOf("_page.CanShow(_state.Edition)", StringComparison.Ordinal);
        int charge = buy.IndexOf("Charge(price, CopyTransaction)", StringComparison.Ordinal);
        Check(test > 0 && charge > test && buy.Contains("\"reallife_ledger_note_no_page\"") && buy.Contains("return Read(LedgerPage.FromKiosk);"),
            "ledger fix: a kiosk tests that the page can open before it charges, says so when it cannot, and answers whether it opened");
        string page = ModSource(@"Ledger\LedgerPage.cs");
        Check(MemberText(page, "public bool Show(LedgerEdition edition, int from)").Contains("if (!CanShow(edition))")
              && MemberText(page, "public bool CanShow(LedgerEdition edition)").Contains("_special == null"),
            "ledger fix: the page opens on exactly what CanShow answers, so a copy charged for is always opened");
        Dictionary<string, string>[] langs = LanguageWords(out string[] _);
        bool said = true;
        for (int l = 0; l < 3; l++)
        {
            said &= langs[l] != null && langs[l].TryGetValue("reallife_ledger_note_no_page", out string text) && text.Length > 0 && text.Length <= 240;
        }
        Check(said, "ledger fix: the kiosk's notice is in en, pt and zh-cn, at most 240 characters");
        Check(FixLineEverywhere("delivered to your door no longer gives free mornings", "não dá mais manhãs de graça", "不再白送早晨的报纸", "verschenkt nach einer Zeit ohne Zuhause",
                                "n'offre plus de matins gratuits", "ya no regala mañanas", "больше не дарит утра",
                                "geeft geen gratis ochtenden meer", "artık ücretsiz sabahlar vermiyor"),
            "ledger fix: What's new has the subscription's line, with its fix mark, in some row of every language");
        Check(FixLineEverywhere("kiosk no longer charges for today's paper", "banca não cobra mais", "报亭不再收取", "Kiosk berechnet die heutige Zeitung nicht mehr",
                                "kiosque ne fait plus payer", "quiosco ya no cobra", "киоск больше не берёт плату",
                                "brengt geen kosten meer in rekening voor de krant van vandaag", "büfe artık bugünün gazetesi için ücret almıyor"),
            "ledger fix: What's new has the kiosk's line, with its fix mark, in some row of every language");
    }
}
