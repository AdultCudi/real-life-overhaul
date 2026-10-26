using System;
using System.Collections.Generic;

// The font stutter fix (26 Oct 2026, a player's session: a 97 ms scene search for the game's font in play): What's new tells
// of it in every language, in any of its rows (WhatsNewHas), each with its fix mark. The source rule is StandardCheck's
// RLO-PERF-017.
internal static partial class Program
{
    private static void FontFix()
    {
        string[] codes = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" };
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: " };
        for (int l = 0; l < codes.Length; l++)
        {
            Dictionary<string, string> words = LocaleFile(codes[l]);
            bool has = false;
            foreach (KeyValuePair<string, string> row in words)
            {
                if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal))
                {
                    has |= row.Value.Contains(marks[l]) && WhatsNewHas(words, FontLine(codes[l]));
                }
            }
            Check(has, "font stutter fix: What's new has its fix line in " + codes[l] + " (any row)");
        }
    }

    // A piece of each language's line that no other line shares.
    private static string FontLine(string code)
    {
        switch (code)
        {
            case "pt": return "A fonte do jogo agora é achada de novo pelo nome";
            case "zh-cn": return "按名称重新找到游戏的字体";
            case "de": return "Die Schrift des Spiels wird jetzt über ihren Namen wiedergefunden";
            case "fr": return "La police du jeu est maintenant retrouvée par son nom";
            case "es": return "La fuente del juego ahora se vuelve a encontrar por su nombre";
            case "ru": return "Шрифт игры теперь снова находится по имени";
            case "nl": return "Het lettertype van het spel wordt nu opnieuw gevonden via zijn naam";
            case "tr": return "Oyunun yazı tipi artık adıyla yeniden bulunuyor";
            default: return "The game's font is now found again by its name";
        }
    }
}
