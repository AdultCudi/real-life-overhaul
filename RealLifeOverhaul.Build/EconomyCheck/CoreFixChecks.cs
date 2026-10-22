using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// Faults found in Scripts/Core and Core/GameUi: the special buildings' shown set told from a sum of string hashes, so two
// sets with the same sum were one; a tab copy that was not a tab left the copies before it in the bar; the game's
// pictures searched for in all of memory the first time an app was built in play; the timing bar's marker placed by a
// width read once; one of the table rows' entry points without the guard's early return; the game's font searched for
// again on every ask while the interface had no text. And the F8 phone dump, a developer aid, taken out whole.
internal static partial class Program
{
    private static void CoreFixes()
    {
        // The phone dump: no class, key, binding or text of it left.
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var named = new List<string>();
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            if (Regex.IsMatch(File.ReadAllText(file), @"PhoneUiDump|KeyAction\.PhoneDump|\bPhoneDump\b|phone_dump"))
            {
                named.Add(System.IO.Path.GetFileName(file));
            }
        }
        Check(named.Count == 0 && !File.Exists(System.IO.Path.Combine(scripts, @"Phone\PhoneUiDump.cs")), "core fix: the F8 phone dump is gone, with its key (none left: " + string.Join(", ", named) + ")");
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(LocalesFolder(), code + ".json"));
            bool clean = words != null && !words.ContainsKey("reallife_keys_phone_dump");
            foreach (KeyValuePair<string, string> pair in words ?? new Dictionary<string, string>())
            {
                clean &= !pair.Value.Contains("{key_phone_dump}");
            }
            Check(clean, "core fix (" + code + "): no text of the phone dump's key, and no page names it");
        }

        SpecialShownChecks();

        string shell = Block(ModSource(@"Core\GameUi\AppShell.cs"), "public bool SetTabCount(int count)");
        string refusal = shell.Substring(Math.Max(0, shell.IndexOf("if (tabs[i] == null || labels[i] == null)", StringComparison.Ordinal)));
        Check(InOrder(refusal, "for (int k = i; k >= 1; k--)", "DestroyImmediate(k == i ? copy : tabs[k].gameObject);", "return false;")
              && shell.IndexOf("Tabs = tabs;", StringComparison.Ordinal) > shell.IndexOf("return false;", StringComparison.Ordinal),
            "game UI fix: a tab copy that is not a tab takes this call's copies off the bar again and leaves the game's two tabs");

        string sprites = ModSource(@"Core\GameUi\GameSprites.cs"), get = Block(sprites, "public static Sprite Get(string name)");
        Check(InOrder(get, "Found.TryGetValue(name, out sprite)", "if (_stale && Unseen())", "Look();") && Occurs(sprites, "Look();") == 2
              && Block(sprites, "private static bool Unseen()").Contains("SceneFinds.Black || ScenePrewarm.InLate || Looks.Hidden")
              && sprites.Contains("SceneFinds.InMemory<Sprite>(") && !sprites.Contains("Resources.FindObjectsOfTypeAll")
              && ModSource(@"Core\RealLifeOverhaulMod.cs").Contains("GameUi.GameSprites.Prewarm)"),
            "game UI fix: the game's pictures are found behind the loading screen, and a name first asked for later only behind a black screen or a pause");
        var late = new List<string>();
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        var asked = new List<string>();
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string code = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(code, @"GameSprites\.Want\(([^;]*)\);"))
            {
                string method = Enclosing(code, m.Index);
                if (method != "Awake" && method != "WantPictures" && method != "WantIcons")
                {
                    late.Add(System.IO.Path.GetFileName(file) + " " + method);
                }
                foreach (Match literal in Regex.Matches(m.Groups[1].Value, "\"([^\"]+)\""))
                {
                    wanted.Add(literal.Groups[1].Value);
                }
            }
            foreach (Match m in Regex.Matches(code, "GameSprites\\.Get\\(\"([^\"]+)\"\\)"))
            {
                asked.Add(m.Groups[1].Value);
            }
        }
        var unnamed = asked.FindAll(n => !wanted.Contains(n));
        Check(late.Count == 0, "game UI fix: every feature names the game's pictures it wants in its Awake, before the look (none later: " + string.Join(", ", late) + ")");
        Check(asked.Count >= 8 && unnamed.Count == 0, "game UI fix: every picture asked for by its name is named before the look (" + asked.Count + "; not named: " + string.Join(", ", unnamed) + ")");

        string bar = ModSource(@"Core\GameUi\GameBar.cs"), mark = Block(bar, "public void Mark(float value)");
        Check(mark.Contains("_mark.anchorMin = new Vector2(_markShown, 0f);") && mark.Contains("_mark.anchorMax = new Vector2(_markShown, 1f);") && !bar.Contains("_width") && !bar.Contains(".rect.width"),
            "game UI fix: the timing bar's marker is anchored at its share of the bar, right at any screen size or interface scale");

        string table = ModSource(@"Core\GameUi\DataTable.cs");
        string cell = table.Substring(Math.Max(0, table.IndexOf("class DataCellView", StringComparison.Ordinal)));
        int entries = 0, guarded = 0;
        foreach (Match m in Regex.Matches(cell, @"public override \w+ (\w+)\([^)]*\)"))
        {
            entries++;
            guarded += Regex.IsMatch(Block(cell, m.Index), @"^\{\s*if \(FrameGuard\.Off\)") ? 1 : 0;
        }
        Check(entries >= 3 && guarded == entries, "game UI fix: every entry point of the table's rows returns at once while its guard is off (" + guarded + " of " + entries + ")");

        string font = ModSource(@"Core\GameUi\GameFont.cs");
        Check(Block(font, "public static TMP_FontAsset Asset").Contains("if (_font == null && !_none)") && font.Contains("_none = _font == null;")
              && Block(font, "public static bool Refind()").Contains("_none = false;")
              && Block(ModSource(@"Core\RealLifeOverhaulMod.cs"), "private static bool FindFont()").Contains("return GameUi.GameFont.Refind();"),
            "game UI fix: a look for the game's font that found no text is not made again until a load, which looks again");

        // The shared city's ask for a part again is the market's: a guest whose prices are not the host's asks for them whole.
        Check(ModSource(@"Markets\MarketDirector.GoingPublic.cs").Contains("SharedCity.AskAgain('M');") && ModSource(@"Core\SharedCity.cs").Contains("public static void AskAgain(char id)"),
            "shared city: the market's check asks for its part again through the shared city's AskAgain");

        // What's new, with its fix mark, in some row of every language.
        Check(FixLineEverywhere("no longer stops the features it works with", "não para mais os recursos com que ele trabalha", "不再连带停止与它配合的功能",
                                "nicht mehr an, sodass die Morgenzeitung", "n'arrête plus les fonctions avec lesquelles elle travaille", "ya no detiene las funciones con las que trabaja",
                                "больше не останавливает функции", "houdt de functies waarmee die samenwerkt niet meer tegen", "artık birlikte çalıştığı özellikleri durdurmuyor"),
            "wiring fix: What's new has the line of a fault that no longer stops the features it works with");
        Check(FixLineEverywhere("the names of its special buildings up to date", "atualiza os nomes dos seus prédios especiais", "其特殊建筑的名称总会随之更新",
                                "die Namen ihrer besonderen Gebäude immer aktualisiert", "les noms de ses bâtiments spéciaux", "los nombres de sus edificios especiales",
                                "названия её особых зданий", "de namen van haar speciale gebouwen", "özel binalarının adları"),
            "special names fix: What's new has the special buildings' names line");
        Check(FixLineEverywhere("with no stray tabs", "sem abas a mais", "不会多出多余的标签页", "ohne überzählige Reiter", "sans onglet en trop", "sin pestañas de más",
                                "без лишних вкладок", "zonder losse extra tabbladen", "fazladan sekme olmadan"),
            "game UI fix: What's new has the tab bar's line");
        Check(FixLineEverywhere("are found behind the loading screen", "encontradas atrás da tela de carregamento", "会在加载画面期间找到", "hinter dem Ladebildschirm gefunden",
                                "trouvées derrière l'écran de chargement", "se buscan detrás de la pantalla de carga", "находятся за экраном загрузки",
                                "achter het laadscherm gevonden", "yükleme ekranının arkasında bulunuyor"),
            "game UI fix: What's new has the line of the pictures found behind the loading screen");
        Check(FixLineEverywhere("the marker on a timing bar stays in the right place", "o marcador de uma barra de tempo fica no lugar certo", "计时条上的标记仍会停在正确的位置",
                                "Die Markierung auf einem Timing-Balken bleibt", "le repère d'une barre de timing reste au bon endroit", "la marca de una barra de tiempo se queda en su sitio",
                                "отметка на полосе тайминга остаётся", "de markering op een timingbalk blijft", "zamanlama çubuğundaki işaret doğru yerde kalıyor"),
            "game UI fix: What's new has the timing marker's line");
    }

    // Two sets of shown buildings whose string hashes add up the same (found among made-up ids in this process, the
    // hashes being the runtime's own) are two sets: the second look sees the change.
    private static void SpecialShownChecks()
    {
        var sums = new Dictionary<uint, int>();
        string[] ids = new string[1200];
        for (int i = 0; i < ids.Length; i++)
        {
            ids[i] = "check_shown_" + i;
        }
        int a = -1, b = -1, c = -1, d = -1;
        for (int i = 0; i < ids.Length && a < 0; i++)
        {
            for (int j = i + 1; j < ids.Length; j++)
            {
                uint sum = unchecked((uint)ids[i].GetHashCode() + (uint)ids[j].GetHashCode());
                if (sums.TryGetValue(sum, out int pair))
                {
                    int p = pair / ids.Length, q = pair % ids.Length;
                    if (p != i && p != j && q != i && q != j)
                    {
                        a = p; b = q; c = i; d = j;
                        break;
                    }
                }
                else
                {
                    sums[sum] = i * ids.Length + j;
                }
            }
        }
        Check(a >= 0, "special names fix: two different pairs of ids with the same sum of hashes are found to test with");
        if (a < 0)
        {
            return;
        }
        bool firstPair = true;
        var book = new SpecialNameBook();
        book.Set(ids[a], "Check Street", 1, SpecialKind.Other, "k", null, () => firstPair);
        book.Set(ids[b], "Check Street", 2, SpecialKind.Other, "k", null, () => firstPair);
        book.Set(ids[c], "Check Street", 3, SpecialKind.Other, "k", null, () => !firstPair);
        book.Set(ids[d], "Check Street", 4, SpecialKind.Other, "k", null, () => !firstPair);
        bool first = book.LookShown();
        bool again = book.LookShown();
        int stamp = book.Stamp;
        firstPair = false;
        bool changed = book.LookShown();
        Check(!first && !again && changed && book.Stamp == stamp + 1, "special names fix: two shown sets with the same sum of hashes are told apart, and the stamp moves once");
        Check(!book.LookShown(), "special names fix: the same shown set looked at again is no change");
    }

    // The name of the method whose body holds the index: the last member signature at eight or twelve spaces before it.
    private static string Enclosing(string code, int at)
    {
        MatchCollection all = Regex.Matches(code.Substring(0, at), @"\n {8,12}(?:public |private |internal |protected )(?:static |override |sealed |new )*[\w<>\[\], .]+? (\w+)\(");
        return all.Count > 0 ? all[all.Count - 1].Groups[1].Value : string.Empty;
    }
}
