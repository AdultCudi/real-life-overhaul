using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // The game's conversations on the kit over the HUD's bar, as they come while walking: the dialogue panel docked at
    // the right in each of its states (the bank's desk indoors, each game form a talk shows with what its confirm pays and
    // costs, a hotel's desk, the Business desk, the street by day and by night, an insurer's office), the scenes over the
    // courthouse, the studio, a door, an office; the same loan by phone on the call card over the night street, a tow by
    // phone, and a wage talk's mood and offer in Contacts; Uncle Fred's lines at the top. The bank, a wholesale contract, a
    // delivery, the desk, the court and its tax case, the interview, the pitch, the call and Uncle Fred also in Brazilian
    // Portuguese and in Chinese, and four in German, the longest of the mod's languages: each from the game's and the
    // mod's own files, English where neither has the words.
    static partial void AddDialog(List<Screen> screens)
    {
        foreach (string state in UiSamples.DialogStates)
        {
            screens.Add(DialogScreenOf(state, "en"));
        }
        foreach (string state in UiSamples.SceneStates)
        {
            screens.Add(SceneScreenOf(state, "en"));
        }
        foreach (string state in new[] { "fred", "fred_more", "fred_long" })
        {
            screens.Add(SpokenScreenOf(state, "en"));
        }
        foreach (string call in UiSamples.CallStates)
        {
            screens.Add(CallScreenOf(call, "en"));
        }
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            screens.Add(DialogScreenOf("bank_loan", lang));
            screens.Add(DialogScreenOf("wholesale", lang));
            screens.Add(DialogScreenOf("furniture", lang));
            screens.Add(DialogScreenOf("desk_full", lang));
            screens.Add(DialogScreenOf("street_said", lang));
            screens.Add(SceneScreenOf("court_plea", lang));
            screens.Add(SceneScreenOf("court_tax", lang));
            screens.Add(SceneScreenOf("interview_round", lang));
            screens.Add(SceneScreenOf("pitch_question", lang));
            screens.Add(SpokenScreenOf("fred_more", lang));
            screens.Add(CallScreenOf("loan", lang));
            screens.Add(CallScreenOf("mood", lang));
        }
        screens.Add(DialogScreenOf("bank_loan", "de"));
        screens.Add(DialogScreenOf("furniture", "de"));
        screens.Add(DialogScreenOf("desk_full", "de"));
        screens.Add(SceneScreenOf("court_plea", "de"));
        // The owner, 3 Nov 2026: the conversation stood over the phone. With the phone up, open and folded, it docks left of it.
        screens.Add(PhoneBeside("desk_full", false));
        screens.Add(PhoneBeside("bank_loan", true));
    }

    // A conversation with the phone up at the screen's right: the dock beside its column, the phone over the world.
    private static Screen PhoneBeside(string state, bool folded)
    {
        Screen screen = DialogScreenOf(state, "en");
        screen.Name = "dialog_" + state + (folded ? "_phone_folded" : "_phone");
        UiWords t = DialogWords("en");
        screen.Lay = (s, f, d, k) =>
        {
            UiPhone.LeftShare = UiPhone.OverWorld(f);
            try
            {
                UiSamples.DialogDocked(s, f, d, k, t, state);
                UiPhoneModel phone = UiSamples.PhoneHome(t);
                phone.Collapsed = folded;
                UiPhone.LayOver(s, phone, f, k);
            }
            finally
            {
                UiPhone.LeftShare = 1f;
            }
        };
        return screen;
    }

    // The dialogue panel in one state: where the talk is held behind it, the mouse on an answer, the keys from the first target.
    private static Screen DialogScreenOf(string state, string lang)
    {
        UiWords t = DialogWords(lang);
        string backdrop = state.StartsWith("street") ? (state == "street" ? "13.jpg" : "11.jpg")
            : state.StartsWith("desk") || state == "other_mod" || state == "haggle" || state == "installation" || state == "moving" || state == "hotel" ? "05.jpg"
            : state.StartsWith("marketing") || state == "wholesale" || state.StartsWith("import") || state == "recruitment" ? "00.jpg" : "03.jpg";
        var screen = new Screen
        {
            Name = "dialog_" + state + Suffix(lang), AppName = "Dialogue", Icon = UiIcon.Message, Backdrop = backdrop, Bar = UiSamples.WalkingBar,
            Lay = (s, f, d, k) => UiSamples.DialogDocked(s, f, d, k, t, state),
            FocusCommand = UiConversation.ChoiceCommand, HoverCommand = UiConversation.ChoiceCommand, HoverArg = 1, Walk = Moves("DDU")
        };
        switch (state)
        {
            case "bank_loan":
                screen.FocusCommand = UiFormRows.FieldCommand;
                screen.FocusArg = 1;   // the amount
                screen.HoverArg = 0;
                screen.Walk = Moves("DDUU");
                break;
            case "marketing":
                screen.FocusCommand = UiFormRows.FieldCommand;
                screen.FocusArg = 1;   // the business
                screen.HoverCommand = UiFormRows.FieldCommand;
                screen.HoverArg = 2;
                screen.Walk = Moves("DDDDDDU");
                break;
            case "marketing_list":
                screen.FocusCommand = UiFormRows.OptionCommand;
                screen.FocusArg = 0;
                screen.HoverCommand = UiFormRows.OptionCommand;
                screen.HoverArg = 2;
                screen.Walk = Moves("DDDU");
                break;
            case "other_mod":
                screen.HoverArg = 2;   // the game's own view, for another mod's piece
                break;
            case "wholesale":
            case "import":
            case "import_unset":
            case "installation":
            case "moving":
            case "insurance":
            case "insurance_unset":
            case "recruitment":
                screen.FocusCommand = UiFormRows.FieldCommand;
                screen.FocusArg = 1;   // the form's first choice, under its question
                screen.HoverArg = 0;   // the confirm, what it pays and costs under it
                screen.Walk = Moves("DDDU");
                break;
            case "furniture":
                screen.FocusCommand = UiFormRows.FieldCommand;
                screen.FocusArg = 5;   // the first line of goods
                screen.HoverArg = 0;
                screen.Walk = Moves("DDDDU");
                break;
            case "driver":
                screen.FocusCommand = UiFormRows.FieldCommand;
                screen.FocusArg = 0;   // the contract, the form's first row
                screen.HoverArg = 0;
                break;
            case "hotel":
                screen.HoverArg = 1;   // the deposit, what it costs now and later
                break;
            case "between_lines":
                screen.FocusCommand = -1;
                screen.HoverCommand = -1;
                screen.Walk = new Vector2Int[0];
                break;
        }
        return screen;
    }

    // A scene's page over its place: the court, the studio, a street door, an office.
    private static Screen SceneScreenOf(string state, string lang)
    {
        UiWords t = DialogWords(lang);
        bool talk = SceneViews.LookOf(UiSamples.ScenePageOf(t, state, new List<SceneSaid>())) == SceneLook.Talk;
        string backdrop = state.StartsWith("court") || state == "legal_file" ? "00.jpg" : state == "office_door" ? "13.jpg" : state.StartsWith("interview") || state == "venture_pitch" ? "05.jpg" : "03.jpg";
        int choice = talk ? UiConversation.ChoiceCommand : UiDecision.ChoiceCommand;
        return new Screen
        {
            Name = "scenes_" + state + Suffix(lang), AppName = "Scenes", Icon = UiIcon.People, Backdrop = backdrop, Bar = UiSamples.WalkingBar,
            Lay = (s, f, d, k) => UiSamples.Scene(s, f, d, k, t, state),
            FocusCommand = choice, HoverCommand = choice, HoverArg = 1, Walk = Moves(talk ? "DDDU" : "DDU")
        };
    }

    // Uncle Fred's line at the top of the street, the HUD's bar under it: it takes no key, so the mouse closes it.
    private static Screen SpokenScreenOf(string state, string lang)
    {
        UiWords t = DialogWords(lang);
        return new Screen
        {
            Name = "monologue_" + state + Suffix(lang), AppName = "Spoken lines", Backdrop = state == "fred_long" ? "05.jpg" : state == "fred_more" ? "11.jpg" : "13.jpg",
            Bar = UiSamples.WalkingBar, NoKeys = true, HoverCommand = UiMonologue.CloseCommand, HoverArg = 0,
            Lay = (s, f, d, k) =>
            {
                UiMonologue.Lay(s, UiSamples.Monologue(t, state), f, d);
                UiDraw.StatusBar(s, f, k);
            }
        };
    }

    // The loan by phone on the call card over the night street, a tow by phone in the street by day, or a candidate's
    // wage talk with their mood and what the offer pays in Contacts.
    private static Screen CallScreenOf(string which, string lang)
    {
        UiWords t = DialogWords(lang);
        bool card = which != "mood";
        return new Screen
        {
            Name = "dialog_phone_" + which + Suffix(lang), AppName = "Contacts", Icon = UiIcon.AddressBook, Backdrop = which == "loan" ? "11.jpg" : "13.jpg",
            Bar = () => UiSamples.ShellBar(UiSamples.RowContacts, false),
            Lay = (s, f, d, k) => UiContacts.Lay(s, UiSamples.CallSample(t, which), f, d, k),
            FocusCommand = card ? UiFormRows.FieldCommand : UiConversation.FormCommand, FocusArg = 0,
            HoverCommand = card ? UiCallCard.ChoiceCommand : UiConversation.ChoiceCommand, HoverArg = 0, Walk = Moves(which == "loan" ? "DDUU" : which == "tow" ? "DU" : "DDDUR")
        };
    }

    private static string Suffix(string lang) => lang == "en" ? string.Empty : lang == "zh-cn" ? "_zh" : "_" + lang;

    private const string DialogGameLocale = @"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\StreamingAssets\locale";
    private static readonly Dictionary<string, Dictionary<string, string>> DialogLocales = new Dictionary<string, Dictionary<string, string>>();

    // A language's words as a player in it reads them: the mod's own from its locale, the game's from the game's, the
    // sample's English where neither has them.
    private static UiWords DialogWords(string lang)
    {
        if (lang == "en")
        {
            return English;
        }
        if (!DialogLocales.TryGetValue(lang, out Dictionary<string, string> words))
        {
            words = new Dictionary<string, string>();
            DialogRead(Path.Combine(DialogGameLocale, lang + ".json"), words);
            DialogRead(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Assets", "Mods", "RealLifeOverhaul", "Locales", lang + ".json")), words);
            DialogLocales[lang] = words;
        }
        return (key, english) => key != null && words.TryGetValue(key, out string said) ? said : english;
    }

    private static void DialogRead(string path, Dictionary<string, string> into)
    {
        if (!File.Exists(path))
        {
            Debug.Log(Tag + "no words at " + path + ": the sample's English stands in");
            return;
        }
        foreach (Match pair in Regex.Matches(File.ReadAllText(path, Encoding.UTF8), "\"([^\"\\\\]+)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""))
        {
            into[pair.Groups[1].Value] = Regex.Unescape(pair.Groups[2].Value);
        }
    }
}
