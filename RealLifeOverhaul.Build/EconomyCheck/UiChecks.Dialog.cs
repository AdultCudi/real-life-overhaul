using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static bool _dialogChecked;

    // The game's conversations on the kit, every state: the dialogue panel docked at the right with its forms, the spoken
    // line's card at the top, the scenes as the decision card or the conversation, laid out by the screen checks in every
    // language and frame with the commands their screens answer. A call's form stands on the Contacts screen's call card:
    // it is laid here with the same rules. The rules the screens read the game by, and their ways to the game, once.
    static partial void UiCasesDialog(UiScreenDef def, List<UiScreenCase> cases)
    {
        switch (def.Id)
        {
            case "dialog":
                foreach (string state in UiSamples.DialogStates)
                {
                    if (!UiSamples.DialogChecked(state))
                    {
                        continue;
                    }
                    string st = state;
                    cases.Add(new UiScreenCase
                    {
                        Def = def, Name = "dialog (" + st + ")", Title = "Dialogue", Commands = DialogCommands,
                        Lay = (s, f, dark, k, t) => UiSamples.DialogDocked(s, f, dark, k, t, st)
                    });
                }
                break;
            case "monologue":
                foreach (string state in UiSamples.MonologueStates)
                {
                    string st = state;
                    cases.Add(new UiScreenCase
                    {
                        Def = def, Name = "monologue (" + st + ")", Title = "Spoken lines", Commands = MonologueCommands, Overlay = true,
                        Lay = (s, f, dark, k, t) => UiMonologue.Lay(s, UiSamples.Monologue(t, st), f, dark)
                    });
                }
                break;
            case "scenes":
                foreach (string state in UiSamples.SceneStates)
                {
                    string st = state;
                    cases.Add(new UiScreenCase
                    {
                        Def = def, Name = "scenes (" + st + ")", Title = "Scenes", Commands = SceneCommands,
                        Lay = (s, f, dark, k, t) => UiSamples.Scene(s, f, dark, k, t, st)
                    });
                }
                break;
            default:
                return;
        }
        if (!_dialogChecked)
        {
            _dialogChecked = true;
            DialogRuleChecks();
            DialogSceneChecks();
            DialogCallChecks();
            DialogTalkLooks();
            DialogSourceChecks();
        }
    }

    private static readonly int[] DialogCommands =
    {
        UiConversation.ChoiceCommand, UiFormRows.FieldCommand, UiFormRows.LessCommand, UiFormRows.MoreCommand, UiFormRows.OptionCommand, UiFormRows.RowCommand,
        UiFormRows.SwatchCommand, UiFormRows.RemoveCommand
    };

    private static readonly int[] MonologueCommands = { UiMonologue.CloseCommand, UiMonologue.SkipCommand };

    private static readonly int[] SceneCommands = { UiDecision.ChoiceCommand, UiDecision.ButtonCommand, UiConversation.ChoiceCommand };

    // The Contacts window under a call, the call card and the form on it.
    private static readonly int[] DialogCallCommands =
    {
        UiAppWindow.NavCommand, UiAppWindow.ActionCommand, UiMessageList.RowCommand, UiMessageList.SearchCommand, UiMessageList.ClearCommand, UiContacts.SideCommand, UiContacts.ActionCommand,
        UiConversation.ChoiceCommand, UiConversation.FormCommand, UiConversation.FormLess, UiConversation.FormMore, UiCallCard.ChoiceCommand, UiCallCard.ButtonCommand,
        UiFormRows.OptionCommand, UiFormRows.RowCommand, UiFormRows.SwatchCommand, UiFormRows.RemoveCommand
    };

    // How the screens read the game: who speaks, the mood by a line, a scene list's lines, which forms the kit leaves to the
    // game, a label's lines on one, a colour's swatch, a total's weight; and the dialogue panel's answers on 1 to 9.
    private static void DialogRuleChecks()
    {
        Check(DialogRules.SpeakerName("dialog_bank_npc_name", "Banker") == "Banker" && DialogRules.SpeakerName("Rosa Stanley", null) == "Rosa Stanley"
              && DialogRules.SpeakerName("reallife-business", null) == null && DialogRules.SpeakerName("ba:businesstype_bank", null) == null
              && DialogRules.SpeakerName("emp_4411", null) == null && DialogRules.SpeakerName("  ", "  ") == null,
            "dialog: the speaker is the game's words for its name key, a plain name as it is, and never a record's id or an untranslated key");
        Check(DialogRules.MoodIcon(1) == UiIcon.SmileyHappy && DialogRules.MoodIcon(0) == UiIcon.SmileyMeh && DialogRules.MoodIcon(-1) == UiIcon.SmileySad
              && DialogRules.MoodIcon(-2) == UiIcon.None, "dialog: the mood by a line is the game's content, unsure or angry face, and nothing where the game shows none");
        DialogRules.SplitInfo("Nadia Haddad talks you through it.\n<size=80%>Question 2 of 4</size>", out string said, out string progress);
        DialogRules.SplitInfo("The interview is over.", out string plain, out string none);
        DialogRules.SplitInfo(null, out string empty, out string nothing);
        Check(said == "Nadia Haddad talks you through it." && progress == "Question 2 of 4" && plain == "The interview is over." && none == null && empty == null && nothing == null,
            "dialog: a scene list's lines split into what is said and the progress it writes small, as plain words");
        Check(DialogRules.Look(true, 0) == SceneLook.Talk && DialogRules.Look(false, DialogRules.BodyMost) == SceneLook.Decision && DialogRules.Look(false, DialogRules.BodyMost + 1) == SceneLook.Talk,
            "dialog: questions and answers, and a moment longer than the card holds, take the conversation; a short moment the card");
        Check(DialogRules.Casino("RouletteDialog") && DialogRules.Casino("BlackjackDialog") && DialogRules.Casino("SlotMachineDialog") && !DialogRules.Casino("BankDialog") && !DialogRules.Casino(null)
              && DialogRules.CasinoForm("RouletteBetSettings") && !DialogRules.CasinoForm("BankLoan") && !DialogRules.CasinoForm(null),
            "dialog: the casino's games and their bets stay the game's own; every other form is the kit's");
        Check(DialogRules.OneLine("Friendbook Ads\n$120 a day") == "Friendbook Ads · $120 a day" && DialogRules.OneLine("<b>Leave</b>") == "Leave" && DialogRules.OneLine(null) == null,
            "dialog: an answer's words stand on one line, the game's line breaks as a middle dot");
        bool swatch = DialogRules.SwatchColour(DialogRules.Swatch(18, 200, 255), out byte r, out byte g, out byte b) && r == 18 && g == 200 && b == 255;
        Check(swatch && !DialogRules.SwatchColour("dialog:swatch:12", out _, out _, out _) && !DialogRules.SwatchColour("ba:item_apple", out _, out _, out _),
            "dialog: a colour to pick keeps its colour in its picture's key, and no other key reads as one");
        Check(DialogRules.Total("TotalDailyPayment") && DialogRules.Total("PriceRow") && DialogRules.Total("PricePerDay") && !DialogRules.Total("DailyInterest")
              && DialogRules.LongFigure(new string('x', DialogRules.FigureMost + 1)) && !DialogRules.LongFigure("$245"),
            "dialog: a form's total stands out, and a figure as long as a sentence reads as a note");

        // The panel's answers on 1 to 9 in the game's order, the way out on Back with its own words.
        UiWords en = UiWords("en");
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Dialogue");
        UiSamples.DialogDocked(s, frame, true, UiDefaultKeys.Keyboard, en, "desk");
        int first = s.FindNode(UiConversation.ChoiceCommand, 0), third = s.FindNode(UiConversation.ChoiceCommand, 2);
        Check(first >= 0 && third >= 0 && s.Nodes[first].Key == UiAction.Pick1 && s.Nodes[third].Key == (UiAction)((int)UiAction.Pick1 + 2) && Prompted(s, UiAction.Back),
            "dialog: the panel's answers are numbered 1 to 9 in the game's order, and Back is the way out");
        UiSamples.DialogDocked(s, frame, true, UiDefaultKeys.Keyboard, en, "bank_loan");
        int amount = s.FindNode(UiFormRows.FieldCommand, 1), term = s.FindNode(UiFormRows.FieldCommand, 2), figure = s.FindNode(UiFormRows.FieldCommand, 4);
        int confirm = s.FindNode(UiConversation.ChoiceCommand, 0);
        Check(amount >= 0 && term >= 0 && figure < 0 && confirm >= 0 && s.Nodes[confirm].Key == UiAction.Pick1,
            "dialog: the bank's loan form takes its amount as typing and its term as a choice, its figures are read only, and Confirm is the first answer");
        UiConversationModel between = UiSamples.Dialog(en, "between_lines");
        Check(between.Typing && between.Choices.Length == 0 && between.PromptCount == 0 && !UiSamples.DialogChecked("between_lines"),
            "dialog: between two lines, while the game shows no button, the panel shows the dots and the bar no key");
    }

    // The scenes as the kit draws them: the look each page takes, the hearing that may not be left, the answers' meanings
    // whole on their lines in every language, the numbers 1 to 9, the line under a headline as the card's kicker.
    private static void DialogSceneChecks()
    {
        UiWords en = UiWords("en");
        SceneWords words = UiSamples.SceneWordsOf(en);
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Scenes");
        var s = new UiScene();
        string wrong = "none";
        foreach (string state in UiSamples.SceneStates)
        {
            var thread = new List<SceneSaid>();
            SceneLook look = SceneViews.Lay(s, UiSamples.ScenePageOf(en, state, thread), thread, words, -1f, frame, true, UiDefaultKeys.Keyboard);
            bool talk = state == "court_charges" || state == "legal_file" || state == "interview_round" || state == "pitch_question" || state == "venture_pitch";
            if (look != (talk ? SceneLook.Talk : SceneLook.Decision))
            {
                wrong = state + " laid as " + look;
            }
        }
        Check(wrong == "none", "scenes: a moment is the decision card, a round of questions and a page longer than the card (a charge sheet, the Legal file) the conversation (" + wrong + ")");

        var none = new List<SceneSaid>();
        UiDecisionModel court = SceneViews.Decision(UiSamples.ScenePageOf(en, "court_plea", none), words);
        ScenePage filePage = UiSamples.ScenePageOf(en, "legal_file", new List<SceneSaid>());
        UiConversationModel file = SceneViews.Talk(filePage, new List<SceneSaid>(), words, frame.Width);
        UiDecisionModel fileCard = SceneViews.Decision(filePage, words);
        Check(court.Buttons.Length == 0 && DialogLabelled(court.Prompts, UiAction.Back, words.Menu) && DialogLabelled(file.Prompts, UiAction.Back, words.Back)
              && fileCard.Buttons.Length == 1 && fileCard.Buttons[0].Key == UiAction.Back && file.Icon == UiIcon.Briefcase && court.Faces.Length == 4,
            "scenes: a hearing the player may not leave has no Cancel, its bar saying the cancel key opens the game's menu; the Legal file, a file nobody speaks, closes on Back");
        var heard = new List<SceneSaid>();
        UiConversationModel round = SceneViews.Talk(UiSamples.ScenePageOf(en, "interview_round", heard), heard, words, frame.Width);
        Check(round.Status == "Question 2 of 4" && round.Name == "Nadia Haddad" && round.MessageCount == 3 && round.Messages[2].From == UiFrom.Them && DialogLabelled(round.Prompts, UiAction.Back, words.Menu),
            "scenes: a round of questions names who answers, its progress in the chip, the answer heard as their line under the thread so far");
        ScenePage pitch = UiSamples.ScenePageOf(en, "pitch_question", new List<SceneSaid>());
        Check(SceneViews.Answer(pitch, 0) == "More stock and a second till, so that we stop losing sales at busy hours." && SceneViews.Answer(pitch, 9) == "Leave the meeting"
              && SceneViews.Answer(pitch, 77) == null, "scenes: an answer says its own words in the thread, its name where it has none, and nothing for one not offered");

        // Ten answers: the first nine on 1 to 9, the tenth reached by moving to it.
        var many = new ScenePage();
        many.Reset("Ten answers", null);
        for (int i = 0; i < 10; i++)
        {
            many.Entries.Add(new SceneEntry { Id = i, Name = "Answer " + (i + 1), Enabled = true });
        }
        UiDecisionModel ten = SceneViews.Decision(many, words);
        Check(ten.Choices[0].Key == UiAction.Pick1 && ten.Choices[8].Key == (UiAction)((int)UiAction.Pick1 + 8) && ten.Choices[9].Key == UiAction.Count,
            "scenes: the answers are numbered 1 to 9, and a tenth takes no number");

        // The line under the headline: the card's kicker when short and nothing else is told, else said in its paragraph.
        var shortInfo = new ScenePage();
        shortInfo.Reset("A reception at City Hall", "Day 3, the main hall");
        shortInfo.Notes.Add(new SceneNote { Name = "The mayor", Detail = "Welcome. Do take a glass." });
        var longInfo = new ScenePage();
        longInfo.Reset("A reception at City Hall", "The mayor raises a glass to the city's new businesses, yours among them, and the room turns to you.");
        UiDecisionModel kicked = SceneViews.Decision(shortInfo, words), told = SceneViews.Decision(longInfo, words);
        Check(kicked.Kicker == "Day 3, the main hall" && kicked.Body == "Welcome. Do take a glass." && told.Kicker == null && told.Body.StartsWith("The mayor raises", StringComparison.Ordinal),
            "scenes: a short line under the headline is the card's kicker, a longer one is said in its paragraph");

        // What each answer means, in every scene and the three languages the mod is written in: whole sentences, each line fitting its room.
        float room = SceneViews.ChoiceLine(SceneViews.CardWords), talkRoom = SceneViews.TalkLine(frame.Width);
        bool fits = true, whole = true;
        string where = "none";
        foreach (string lang in new[] { "en", "pt", "zh-cn", "de" })
        {
            UiWords t = UiWords(lang);
            foreach (string state in UiSamples.SceneStates)
            {
                ScenePage p = UiSamples.ScenePageOf(t, state, new List<SceneSaid>());
                foreach (float width in new[] { room, talkRoom })
                {
                    for (int i = 0; i < p.Entries.Count; i++)
                    {
                        UiConsequence[] lines = SceneViews.Meaning(p.Entries[i].Detail, width);
                        if (lines == null)
                        {
                            continue;
                        }
                        bool ok = lines.Length > 0 && lines.Length <= SceneViews.MeaningMost;
                        for (int j = 0; j < lines.Length; j++)
                        {
                            ok &= !string.IsNullOrEmpty(lines[j].Text) && (UiTextRules.Fits(lines[j].Text, UiStyle.Small, width) || j == SceneViews.MeaningMost - 1);
                        }
                        fits &= ok;
                        bool held = lines.Length < SceneViews.MeaningMost || UiTextRules.Fits(lines[lines.Length - 1].Text, UiStyle.Small, width);
                        if (held && DialogSquash(DialogJoined(lines)) != DialogSquash(ContactsRules.Plain(p.Entries[i].Detail)))
                        {
                            whole = false;
                        }
                        if (!ok || (held && !whole))
                        {
                            where = state + " in " + lang + ", answer " + i;
                        }
                    }
                }
            }
        }
        Check(fits && whole, "scenes: what each answer means is said whole, a sentence to a line, every line fitting its room, in en, pt, zh-cn and de (" + where + ")");
    }

    // A call's form on the call card (the bank's loan by phone) and a line's mood in a thread (a candidate's wage), laid
    // in every language, shape and input as the screen checks lay a screen, with the same rules.
    private static void DialogCallChecks()
    {
        var s = new UiScene();
        int broken = 0;
        string said = "none";
        foreach (string lang in ModLanguages.All)
        {
            UiWords t = UiWords(lang);
            foreach (float aspect in UiAspects)
            {
                foreach (IUiKeys keys in new IUiKeys[] { UiDefaultKeys.Keyboard, UiDefaultKeys.XboxPad })
                {
                    if (keys != UiDefaultKeys.Keyboard && lang != "en")
                    {
                        continue;
                    }
                    foreach (bool loan in new[] { true, false })
                    {
                        UiFrame frame = UiFrame.FullMenu(aspect, "Contacts");
                        UiContacts.Lay(s, loan ? UiSamples.LoanCall(t) : UiSamples.MoodCall(t), frame, true, keys);
                        string line = null;
                        int found = DialogClean(s, frame, DialogCallCommands, l => line = line ?? l);
                        if (lang == "en" && keys == UiDefaultKeys.Keyboard)
                        {
                            frame.Pointer = true;
                            UiContacts.Lay(s, loan ? UiSamples.LoanCall(t) : UiSamples.MoodCall(t), frame, true, keys);
                            found += PromptsDrawn(s) + UiCheckRules.Overlaps(s, l => line = line ?? l) + UiCheckRules.NoPrompts(s, l => line = line ?? l);
                        }
                        if (found > 0)
                        {
                            broken += found;
                            said = (loan ? "the loan by phone" : "the wage talk") + " in " + lang + " at " + aspect.ToString("0.00") + (keys.Pad != UiPad.None ? " with a pad" : string.Empty) + ": " + line;
                        }
                    }
                }
            }
        }
        Check(broken == 0, "dialog: a call's form on the call card and a mood by a line lay out clean in every language, shape and input (" + said + ")");

        UiWords en = UiWords("en");
        UiFrame f = UiFrame.FullMenu(16f / 9f, "Contacts");
        UiContacts.Lay(s, UiSamples.LoanCall(en), f, true, UiDefaultKeys.Keyboard);
        int typed = s.FindNode(UiFormRows.FieldCommand, 0), term = s.FindNode(UiFormRows.FieldCommand, 1), figure = s.FindNode(UiFormRows.FieldCommand, 3);
        int reply = s.FindNode(UiCallCard.ChoiceCommand, 0), lower = s.FindNode(UiCallCard.ChoiceCommand, 1), hangUp = s.FindNode(UiCallCard.ButtonCommand, 0);
        Check(typed >= 0 && term >= 0 && figure < 0 && reply >= 0 && s.Nodes[reply].Key == UiAction.Pick1 && lower >= 0 && s.Nodes[lower].Key == (UiAction)((int)UiAction.Pick1 + 1)
              && hangUp >= 0 && s.Nodes[hangUp].Key == UiAction.Back && s.FindNode(UiAppWindow.NavCommand, 0) < 0,
            "dialog: a call's form stands on the call card, its amount typed, its term chosen, its figures read only, Confirm on 1, the bank's own answers after it, Hang up on Back, the window not reachable under it");
        UiContacts.Lay(s, UiSamples.MoodCall(en), f, true, UiDefaultKeys.Keyboard);
        bool mood = false;
        for (int i = 0; i < s.ItemCount; i++)
        {
            mood |= s.Items[i].Kind == UiKind.Icon && s.Items[i].Icon == UiIcon.SmileyMeh;
        }
        Check(mood, "dialog: the mood the game shows by a line stands as its small glyph beside it");
    }

    // The talk past the game's own answers as the kit draws it: the bank's loan with what it costs under Confirm and the
    // bank's own answers after it, a negotiation's mood in a word by the face, Uncle Fred's Skip and Next and how far a line
    // has been said, the court with who is present and what each plea costs, the city's settlement in a tax case.
    private static void DialogTalkLooks()
    {
        UiWords en = UiWords("en");
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Dialogue");

        UiConversationModel loan = UiSamples.Dialog(en, "bank_loan");
        UiSamples.DialogDocked(s, frame, true, UiDefaultKeys.Keyboard, en, "bank_loan");
        int leave = s.FindNode(UiConversation.ChoiceCommand, 3);
        Check(loan.Choices.Length == 4 && loan.Choices[0].Lines.Length == 2 && loan.Choices[0].Lines[0].Tone > 0 && loan.Choices[0].Lines[1].Tone < 0 && loan.Choices[1].Disabled
              && loan.Choices[1].Lines.Length == 1 && loan.Choices[1].Lines[0].Text.Contains("700") && !loan.Choices[2].Disabled && leave >= 0
              && s.Nodes[leave].Key == (UiAction)((int)UiAction.Pick1 + 3) && DialogDrawn(s, "$25,000 in your account today") && DialogDrawn(s, "$29,477"),
            "dialog: the bank's loan says what taking it pays and costs under Confirm, the bank's own answers on 2 and 3 with why one is greyed, Leave on 4");

        UiConversationModel haggle = UiSamples.Dialog(en, "haggle");
        UiSamples.DialogDocked(s, frame, true, UiDefaultKeys.Keyboard, en, "haggle");
        Check(haggle.Status == "Unsure" && haggle.StatusTone == UiRole.Warn && haggle.Ring == UiRole.Warn && DialogDrawn(s, "Unsure")
              && DialogRules.MoodWord(1) == "reallife_ui_dialog_mood_friendly" && DialogRules.MoodWord(-1) == "reallife_ui_dialog_mood_upset" && DialogRules.MoodWord(-2) == null,
            "dialog: the mood the game shows in a negotiation is the ring round the face and its word beside it in the ring's colour, and nothing where the game shows none");

        UiFrame top = UiFrame.FullMenu(16f / 9f, "Spoken lines");
        UiMonologue.Lay(s, UiSamples.Monologue(en, "fred_more"), top, true);
        int next = s.FindNode(UiMonologue.CloseCommand, 0), skip = s.FindNode(UiMonologue.SkipCommand, 0);
        bool keyless = next >= 0 && skip >= 0 && s.Nodes[next].Key == UiAction.Count && s.Nodes[skip].Key == UiAction.Count;
        bool told = DialogDrawn(s, "1 of 2") && DialogDrawn(s, "Next") && DialogDrawn(s, "Skip") && DialogTrack(s);
        UiMonologue.Lay(s, UiSamples.Monologue(en, "fred"), top, true);
        bool last = s.FindNode(UiMonologue.SkipCommand, 0) < 0 && s.FindNode(UiMonologue.CloseCommand, 0) >= 0 && DialogDrawn(s, "Close") && DialogTrack(s);
        UiMonologue.Lay(s, UiSamples.Monologue(en, "fred_long"), top, true);
        bool silent = !DialogTrack(s) && s.FindNode(UiMonologue.CloseCommand, 0) >= 0;
        Check(keyless && told && last && silent,
            "monologue: a line with more to come says where it stands and has Skip and Next, the last one Close, neither taking a key; the bar shows how far it has been said, none for a line with no sound");

        SceneWords words = UiSamples.SceneWordsOf(en);
        UiFrame scene = UiFrame.FullMenu(16f / 9f, "Scenes");
        UiDecisionModel plea = SceneViews.Decision(UiSamples.ScenePageOf(en, "court_plea", new List<SceneSaid>()), words);
        UiDecisionModel tax = SceneViews.Decision(UiSamples.ScenePageOf(en, "court_tax", new List<SceneSaid>()), words);
        var thread = new List<SceneSaid>();
        SceneViews.Lay(s, UiSamples.ScenePageOf(en, "court_plea", thread), thread, words, -1f, scene, true, UiDefaultKeys.Keyboard);
        Check(plea.Faces.Length == 4 && plea.FaceRoles != null && plea.FaceRoles[1] == "Presiding" && plea.FaceRoles[3] == "The defendant" && plea.FaceRings[0] == UiRole.Danger
              && plea.FaceRings[2] == UiRole.Ok && plea.EmblemLabel == "4 present" && DialogDrawn(s, "Presiding") && DialogDrawn(s, "4 present"),
            "scenes: the court shows those present one to a row with who each is and whose side they are on, and how many are present");
        Check(plea.Choices[0].Note == "24 hours" && DialogHas(plea.Choices[0].Lines, "A fine of $1,200", -1) && DialogHas(plea.Choices[1].Lines, "About 4 in 10 chance to be cleared", 0)
              && DialogHas(plea.Choices[1].Lines, "If convicted, a fine of $2,400", -1) && DialogHas(plea.Choices[2].Lines, "A fine of $720", -1) && DialogDrawn(s, "About 4 in 10 chance to be cleared"),
            "scenes: each plea says what it costs under its meaning, the time it takes by its name, a trial its chance in words and what losing it brings");
        Check(tax.FaceRoles != null && tax.FaceRoles[0] == "For the city's tax office" && tax.Choices.Length == 3 && tax.Choices[2].Name == "Settle with the city"
              && DialogHas(tax.Choices[2].Lines, "$9,600 to the city", -1) && DialogHas(tax.Choices[2].Lines, "The charge is dropped; the inspector comes less often for a year", 1)
              && DialogHas(tax.Choices[1].Lines, "If convicted, a fine of $9,600", -1),
            "scenes: a tax case's plea offers the city's settlement in place of a deal, what a lost trial costs and the charge dropped with the inspector less often");
        var costs = new[] { new UiConsequence { Text = "A fine of $1,200" }, new UiConsequence { Text = "24 hours of community service" } };
        Check(SceneViews.Joined("No trial.", costs, "24 hours") == "No trial.\nA fine of $1,200 \u00b7 24 hours of community service \u00b7 24 hours"
              && SceneViews.Joined("No trial.", null, null) == "No trial." && SceneViews.Joined(null, costs, null) == "A fine of $1,200 \u00b7 24 hours of community service",
            "scenes: with the kit off, the game's own list says an answer's costs and time on a line under its meaning");
    }

    // Some item drawn says exactly these words.
    private static bool DialogDrawn(UiScene s, string words)
    {
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Text == words)
            {
                return true;
            }
        }
        return false;
    }

    // A meter's track drawn: how far something has gone.
    private static bool DialogTrack(UiScene s)
    {
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Kind == UiKind.Box && s.Items[i].Fill == UiRole.Track)
            {
                return true;
            }
        }
        return false;
    }

    // A choice's line with these words, in this colour.
    private static bool DialogHas(UiConsequence[] lines, string text, int tone)
    {
        for (int i = 0; lines != null && i < lines.Length; i++)
        {
            if (lines[i].Text == text && lines[i].Tone == tone)
            {
                return true;
            }
        }
        return false;
    }

    // The screens' ways to the game, as their sources show them: every press the game's own button, widget or pick, each
    // said in Player.log; the switch and the fallback through the host; the scenes' lock the game's own cancel key.
    private static void DialogSourceChecks()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string dialog = System.IO.Path.Combine(scripts, "Ui", "Dialog");
        string screen = File.ReadAllText(System.IO.Path.Combine(dialog, "DialogScreen.cs")), form = File.ReadAllText(System.IO.Path.Combine(dialog, "DialogForm.cs"));
        string kit = File.ReadAllText(System.IO.Path.Combine(dialog, "DialogKits.cs")), spoken = File.ReadAllText(System.IO.Path.Combine(dialog, "MonologueScreen.cs"));
        string scenes = File.ReadAllText(System.IO.Path.Combine(dialog, "SceneKit.cs")), desk = File.ReadAllText(System.IO.Path.Combine(dialog, "SceneDesk.cs"));
        string call = File.ReadAllText(System.IO.Path.Combine(dialog, "ContactsScreen.CallForm.cs"));
        string travel = File.ReadAllText(System.IO.Path.Combine(scripts, "Core", "GameUi", "TravelList.cs"));
        Check(screen.Contains("UiWidgets.Press(b)") && screen.Contains("\"Dialog (kit): \"") && screen.Contains("DialogKits.Classic()") && spoken.Contains("_card.Find(\"Close\")")
              && spoken.Contains("UiWidgets.Press(") && scenes.Contains("List.Pick(") && scenes.Contains("List.Close()") && call.Contains("_callForm.Press("),
            "dialog: every answer is the game's own button, the spoken line's close the game's close, a scene's answer its window's pick, each said in Player.log");
        Check(kit.Contains("UiScreenSwitches.On(DialogId)") && kit.Contains("UiScreenSwitches.On(MonologueId)") && kit.Contains("SceneKit.Install(UiScreenSwitches.On(ScenesId))")
              && kit.Contains("Talk.Remove(") && kit.Contains("Spoken.Remove("),
            "dialog: each of the three has its switch, and off gives the game's own panel back at once");
        Check(travel.Contains("!Owner.Locked && PlayerAction.Cancel.Pressed()") && desk.Contains("List.Locked = value") && scenes.Contains("if (List.Locked || !List.IsOpen)"),
            "scenes: a scene the player may not leave keeps the cancel key for the game's own menu, as the window always has, and the kit never closes it");
        string host = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "Host", "UiGameHost.cs"));
        Check(host.Contains("MiniMenu.IsOpen || global::Scenes.MainMenu.Options.IsVisible || global::BlueprintsUI.BlueprintsPanel.IsOpen") && host.Contains("_underMenu = true;")
              && host.Contains("else if (!menu && _underMenu)"),
            "dialog: the conversation and a scene go away under the game's pause menu (the game hides its HUD and its dialogue there) and are back as it closes");
        Check(screen.Contains("DialogRules.CasinoForm(form.name)") && call.Contains("DialogRules.CasinoForm(form.name)") && screen.Contains("Foreign()") && form.Contains("class DialogForm"),
            "dialog: the casino's forms and another mod's piece in the panel are answered in the game's own view");
        int owners = 0;
        foreach (string owner in new[] { @"Justice\JusticePanel.cs", @"Fame\FamePanel.cs", @"Civic\OfficePanel.cs", @"Tech\Hiring\InterviewMeeting.cs", @"Economy\Ventures\PitchMeeting.cs", @"Tech\Venture\VentureMeeting.cs" })
        {
            string text = File.ReadAllText(System.IO.Path.Combine(scripts, owner));
            owners += text.Contains("new SceneDesk(") && text.Contains(".Moment(") ? 1 : 0;
        }
        Check(owners == 6, "scenes: the six scene windows (the court, fame, office, interview, pitch and venture) tell the kit their moments (" + owners + " of 6)");

        // The tests and the switches' words, in the three languages the mod is written in.
        string[] keys =
        {
            "reallife_ui_dialog_title", "reallife_ui_dialog_in_street", "reallife_ui_test_dialog_tax_court", "reallife_ui_screen_dialog", "reallife_opt_tip_ui_screen_dialog",
            "reallife_ui_screen_monologue", "reallife_opt_tip_ui_screen_monologue", "reallife_ui_screen_scenes", "reallife_opt_tip_ui_screen_scenes", "reallife_contract_part_ui_dialog",
            "reallife_ui_test_dialog_bank", "reallife_ui_test_dialog_desk", "reallife_ui_test_dialog_court", "reallife_ui_test_dialog_fred", "reallife_ui_test_dialog_switch",
            "reallife_ui_test_dialog_off", "reallife_ui_test_dialog_busy", "reallife_ui_test_dialog_no_bank", "reallife_ui_test_dialog_bank_real", "reallife_ui_test_dialog_kit",
            "reallife_ui_test_dialog_game", "reallife_ui_test_dialog_case_open", "reallife_ui_test_dialog_tax_real", "reallife_ui_dialog_mood_friendly", "reallife_ui_dialog_mood_upset",
            "reallife_ui_dialog_loan_term_year", "reallife_ui_dialog_loan_lower_given", "reallife_ui_dialog_loan_lower_wait", "reallife_ui_dialog_loan_lower_floor",
            "reallife_ui_dialog_loan_lower_reply", "reallife_loan_talk_line_offer", "reallife_loan_talk_line_open", "reallife_loan_talk_back", "reallife_loan_talk_line_limit",
            "reallife_loan_talk_line_cost", "reallife_loan_talk_line_opened", "reallife_loan_talk_line_failed", "reallife_econ_kind_card", "reallife_justice_role_officer",
            "reallife_justice_role_inmate", "reallife_justice_role_visitor", "reallife_justice_role_clerk", "reallife_justice_court_figure_tax", "reallife_justice_court_figure_bribery",
            "reallife_justice_court_figure_campaign", "reallife_justice_court_figure_rigging", "reallife_justice_cost_days", "reallife_justice_cost_prison",
            "reallife_justice_cost_lost_prison", "reallife_justice_cost_custody", "reallife_justice_cost_lost_custody", "reallife_justice_odds_none", "reallife_justice_odds_all",
            "reallife_justice_odds_win", "reallife_justice_odds_appeal", "reallife_justice_verdict_settled", "reallife_justice_reason_trial_fine", "reallife_justice_toast_settled",
            "reallife_justice_outcome_settled", "reallife_transaction_court_settlement", "reallife_co_tax_quiet", "reallife_co_tax_quiet_value"
        };
        Check(AllIn(keys, out string missing), "dialog: the switches', the contract's, the tests' and the talk's words in en, pt and zh-cn (" + missing + ")");
        string spokenKit = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "UiMonologue.cs")), choices = File.ReadAllText(System.IO.Path.Combine(dialog, "DialogChoices.cs"));
        string loanForm = File.ReadAllText(System.IO.Path.Combine(dialog, "DialogForm.Loan.cs")), hearing = File.ReadAllText(System.IO.Path.Combine(scripts, "Justice", "CourtHearing.cs"));
        string court = File.ReadAllText(System.IO.Path.Combine(scripts, "Justice", "JusticeDirector.Court.cs")), tax = File.ReadAllText(System.IO.Path.Combine(scripts, "Economy", "Company", "CompanyDirector.Tax.cs"));
        Check(spoken.Contains("_skipLeft = Waiting();") && spoken.Contains("CloseLine();") && !spoken.Contains("InstantClose") && spokenKit.Contains("UiAction.Count, null, SkipCommand, 0"),
            "monologue: Skip closes this line and each one waiting by the game's own close as it comes, so every line's ending runs; neither button takes a key");
        Check(choices.Contains("talk.ShowEntry(Guard.Entry(next));") && loanForm.Contains("LoanConfirm.Wrap(entry, this);") && loanForm.Contains("_loanForm.dailyInterest = f.Interest;")
              && loanForm.Contains("return _own();"),
            "dialog: an answer of the dialog's own leads on through the game's ShowEntry, guarded as every page is, and the term and a point agreed go into the game's form as its own confirm makes the loan");
        Check(hearing.Contains("_fineFactor = JusticeLaw.TrialFineFactor;") && hearing.Contains("bool settle = Taxed && trial.Due > 0;") && court.Contains("TaxSettled")
              && tax.Contains("TaxRules.Audited(t, _state.Seed, _state.Age, day)"),
            "court: a conviction after a trial doubles the fine, the city settles a tax case only, and a settlement quietens the tax office's audits of the player's own businesses");
    }

    // Every rule the screen checks lay a screen by, summed; the first finding said.
    private static int DialogClean(UiScene s, UiFrame frame, int[] commands, Action<string> say)
    {
        int found = UiCheckRules.Overlaps(s, say) + UiCheckRules.Spills(s, say) + UiCheckRules.OffGrid(s, say) + UiCheckRules.Unfit(s, say) + UiCheckRules.SmallTargets(s, say)
                    + UiCheckRules.TooSmall(s, say) + UiCheckRules.Crowded(s, say) + UiCheckRules.NoPrompts(s, say) + UiCheckRules.Contradictions(s, say) + UiCheckRules.DeadSpace(s, say)
                    + UiCheckRules.Unfilled(s, frame, say) + UiCheckRules.KeyClash(s, say) + UiCheckRules.LowContrast(s, say) + UiCheckRules.RawKeys(s, say)
                    + UiCheckRules.Repeats(s, frame, say) + UiCheckRules.Stacked(s, say) + UiCheckRules.EmptyPanels(s, say) + UiCheckRules.Floating(s, say)
                    + UiCheckRules.Strays(s, say) + UiCheckRules.TypeLevels(s, say) + UiCheckRules.UnknownCommands(s, commands, say);
        int unreached = UiFocusRules.Unreached(s, out string missed);
        if (unreached > 0)
        {
            say("unreached " + missed);
        }
        return found + unreached;
    }

    private static bool DialogLabelled(UiPrompt[] prompts, UiAction action, string label)
    {
        for (int i = 0; prompts != null && i < prompts.Length; i++)
        {
            if (prompts[i].Action == action && prompts[i].Label == label)
            {
                return true;
            }
        }
        return false;
    }

    private static string DialogJoined(UiConsequence[] lines)
    {
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            text.Append(lines[i].Text);
        }
        return text.ToString();
    }

    // The words without their spaces and breaks: the same words cut into lines compare equal.
    private static string DialogSquash(string words)
    {
        var text = new System.Text.StringBuilder();
        for (int i = 0; words != null && i < words.Length; i++)
        {
            if (!char.IsWhiteSpace(words[i]))
            {
                text.Append(words[i]);
            }
        }
        return text.ToString();
    }
}
