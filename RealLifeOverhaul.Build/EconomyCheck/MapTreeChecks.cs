using System;
using System.Collections.Generic;

// The city map fix (27 Oct 2026, a player's Player.log: M only tinted the screen after the mod destroyed a tree the game still
// listed): What's new tells of it in all eleven languages, in any row (WhatsNewHas), each with its fix mark; the test's texts are
// in the three always complete languages; the test and the guard are wired. The source rule is StandardCheck's RLO-PERF-018.
internal static partial class Program
{
    private static void MapTreesFix()
    {
        string[] codes = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };
        string[] lines =
        {
            "the city map would not open (M only darkened the screen) after the mod had taken away a tree.",
            "o mapa da cidade não abria (o M só escurecia a tela) depois que o mod removeu uma árvore.",
            "模组移除一棵树后，城市地图无法打开",
            "Die Stadtkarte ließ sich nicht öffnen (M hat nur den Bildschirm abgedunkelt)",
            "la carte de la ville ne s'ouvrait pas (M assombrissait seulement l'écran)",
            "el mapa de la ciudad no se abría (M solo oscurecía la pantalla)",
            "карта города не открывалась (клавиша M только затемняла экран)",
            "de stadskaart ging niet open (M maakte alleen het scherm donker)",
            "şehir haritası açılmıyordu (M yalnızca ekranı karartıyordu)",
            "mapa miasta się nie otwierała (M tylko przyciemniał ekran)",
            "도시 지도가 열리지 않았습니다"
        };
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: ", "Naprawiono: ", "수정: " };
        string[] testKeys = { "reallife_maptrees_test", "reallife_maptrees_test_start", "reallife_maptrees_test_pass", "reallife_maptrees_test_fail", "reallife_maptrees_test_none" };
        for (int l = 0; l < codes.Length; l++)
        {
            Dictionary<string, string> words = LocaleFile(codes[l]);
            bool has = false;
            foreach (KeyValuePair<string, string> row in words)
            {
                if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal))
                {
                    has |= row.Value.Contains(marks[l]) && WhatsNewHas(words, lines[l]);
                }
            }
            Check(has, "map trees fix: What's new has its fix line in " + codes[l] + " (any row)");
            if (codes[l] == "en" || codes[l] == "pt" || codes[l] == "zh-cn")
            {
                bool all = true;
                foreach (string key in testKeys)
                {
                    all &= words.TryGetValue(key, out string text) && text.Length > 0 && (key == "reallife_maptrees_test" || text.Length <= 240);
                }
                Check(all, "map trees fix: the test's five texts are in " + codes[l]);
            }
        }
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), features = ModSource(@"Meta\FeatureList.cs"), guard = ModSource(@"Core\MapTreeGuard.cs");
        Check(mod.Contains("AddButton(\"reallife_maptrees_test\", MapTreeGuard.RunTest)") && mod.Contains("Guard.Add<MapTreeGuard>(_root)") && features.Contains(".Test(\"reallife_maptrees_test\")")
              && guard.Contains("\"Map check (trees): \"") && guard.Contains("\"Map check (trees, test): \"") && guard.Contains("PASS") && guard.Contains("FAIL"),
            "map trees fix: the guard is started, Options > Tests has the test, and both write a 'Map check (trees)' line with PASS or FAIL in Player.log");
    }
}
