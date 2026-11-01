using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // Another mod's own picture for its tile, read from that mod's files where they are kept for study (local only).
    private const string BankPicture = "../../../evidence/othermods/EmpireBank/EmpireBank.Resources.EmpireBankAppIcon.png";

    // The phone over the day streets (02, 13) and the night (11), at the bottom right as the game holds it, every state
    // laid out by its own screen. The walk crosses the tiles and drops onto the nav bar.
    static partial void AddPhone(List<Screen> screens)
    {
        System.Func<string, Sprite> before = UiPictures.Find;
        UiPictures.Find = key => UiDraw.PictureHandle(key) > 0 ? SamplePicture(UiDraw.PictureHandle(key)) : before != null ? before(key) : null;
        // Each walk reaches every target of its state: the tiles row by row, the head, the radio's buttons, the nav bar.
        Vector2Int[] walk = Moves("RRUDDLLDRRDL");
        Add(screens, "phone_home", "02.jpg", walk, t => UiSamples.Phone(t));
        Add(screens, "phone_home_page2", "13.jpg", Moves("RRUDDLLDRDLR"), t => UiSamples.Phone(t, -1, 1));
        Add(screens, "phone_home_night", "11.jpg", walk, t => UiSamples.Phone(t));
        Add(screens, "phone_folder", "02.jpg", Moves("RRULDDRRDLL"), t => UiSamples.Phone(t, PhoneLayoutRulesBusiness));
        // Business with two of work's apps moved into it by the player: eleven apps, two pages; the second with the radio.
        Add(screens, "phone_folder_page2", "13.jpg", Moves("RURLDDDLDR"), t => UiSamples.Phone(t, PhoneLayoutRulesBusiness, 1, true, false, true, false, true,
            UiSamples.PhoneApps(t, true, null, null, 2)));
        Add(screens, "phone_one_page", "13.jpg", Moves("RRULDD"), t => UiSamples.Phone(t, PhoneLayoutRulesWork));
        Add(screens, "phone_radio_tall", "11.jpg", Moves("RRUDDLLDRRDDLDR"), t => UiSamples.Phone(t, -1, 0, true));
        Add(screens, "phone_driver", "02.jpg", Moves("RRUDDLLDRRDDDLDR"), t => UiSamples.Phone(t, -1, 0, true, true));
        Add(screens, "phone_next_empty", "13.jpg", walk, t => UiSamples.Phone(t, -1, 0, false, false, false));
        Add(screens, "phone_others", "02.jpg", Moves("RRUDDLLDRDDLDR"), t => UiSamples.Phone(t, -1, 1, true));
        Add(screens, "phone_collapsed", "02.jpg", Moves("L"), t => UiSamples.Phone(t, -1, 0, false, false, true, true), UiPhone.ControlCommand, UiPhone.Collapse);
        // The longest words: German, the mod's own names from de.json, the game's as its German phone writes them.
        Add(screens, "phone_longest", "13.jpg", walk, t => UiSamples.Phone(German, -1, 0, false, false, true, false, true,
            UiSamples.PhoneApps(German, true, German, GermanGame), GermanNext));
        Add(screens, "phone_longest_page2", "13.jpg", Moves("RRUDDLLDRDLR"), t => UiSamples.Phone(German, -1, 1, false, false, true, false, true,
            UiSamples.PhoneApps(German, true, German, GermanGame), GermanNext));
        Add(screens, "phone_longest_folder", "13.jpg", Moves("RRULDDRRDLL"), t => UiSamples.Phone(German, PhoneLayoutRulesBusiness, 0, false, false, true, false, true,
            UiSamples.PhoneApps(German, true, German, GermanGame), GermanNext));
    }

    private const int PhoneLayoutRulesWork = 1, PhoneLayoutRulesBusiness = 3;   // the folders' places (PhoneLayoutRules)

    private static void Add(List<Screen> screens, string name, string backdrop, Vector2Int[] walk, System.Func<UiWords, PhoneScreen> make, int hover = UiPhone.AppCommand,
        int hoverArg = 4)
    {
        screens.Add(new Screen { Name = name, AppName = "Phone", Backdrop = backdrop, Walk = walk, HoverCommand = hover, HoverArg = hoverArg,
            Lay = (s, f, d, k) => make(English).Lay(s, f, d, k) });
    }

    private static readonly string[] GermanGame = { "Persona", "Kontakte", "Meine Mitarbeiter", "BizMan", "EconoView", "Voogle Maps", "Market Insider", "Rivalen" };

    private static readonly UiInfoRow[] GermanNext =
    {
        new UiInfoRow { Label = "16:00", Value = "Wöchentliche Teambesprechung bei Lantern Bay Games" }, new UiInfoRow { Label = "18:30", Value = "Fitnessstudio bei Iron Works" },
        new UiInfoRow { Label = "Morgen", Value = "Spieltest von Cinder Valley" }, new UiInfoRow { Label = "Donnerstag", Value = "Miete fällig für 70 Fifth Avenue" },
    };

    private static Dictionary<string, string> _german;

    // The mod's German words, read from its own de.json; a key it lacks reads in English, as in the game.
    private static string German(string key, string english)
    {
        if (_german == null)
        {
            _german = new Dictionary<string, string>();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Assets/Mods/RealLifeOverhaul/Locales/de.json"));
            if (File.Exists(path))
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(line, @"^\s*""([^""]+)"": ""(.*)"",?$");
                    if (m.Success)
                    {
                        _german[m.Groups[1].Value] = System.Text.RegularExpressions.Regex.Unescape(m.Groups[2].Value);
                    }
                }
            }
        }
        return _german.TryGetValue(key, out string words) ? words : english;
    }

    private static Sprite _bank;

    private static Sprite SamplePicture(int handle)
    {
        if (handle != UiSamples.PictureBank)
        {
            return null;
        }
        if (_bank == null)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", BankPicture));
            if (!File.Exists(path))
            {
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            tex.LoadImage(File.ReadAllBytes(path));
            _bank = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
        return _bank;
    }
}
