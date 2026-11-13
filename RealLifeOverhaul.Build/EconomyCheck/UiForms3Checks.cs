using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.Ui;

// The third pass over the dialogue forms and what the earlier passes left (13 Nov 2026): a form's notes, entries and labels shown whole however long
// (the form grows, and scrolls where it outgrows its room), a scene answer's meaning in every line, answers that scroll where they outgrow the docked
// conversation, the game's own picture beside a form's line, a chosen option read whole under its field, the interior designer's hint chips past nine,
// the pieces of the interface that nothing filled gone (the single form field, the activity's start fields, the slider's two states, the wardrobe's
// sliders, the surgeon's members), the activity card's height fitted to its content, and the walker's classes of widget in the dialogue form's source.
// Game-free rules are run; the rest is held to the sources that carry it out.
internal static partial class Program
{
    private static void UiForms3()
    {
        UiForms3Rows();
        UiForms3Answers();
        UiForms3Dead();
        UiForms3Sources();
    }

    // The words of a text a layout drew, joined, so a cut anywhere shows as a missing word.
    private static string FormsDrawn(UiScene s, Func<UiItem, bool> pick)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Kind == UiKind.Text && pick(s.Items[i]))
            {
                sb.Append(s.Items[i].Text).Append('\n');
            }
        }
        return sb.ToString();
    }

    private static string Sentences(int count)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            sb.Append("Sentence number ").Append(i).Append(" says something a player has to read to the end before pressing a button. ");
        }
        return sb.ToString().Trim();
    }

    // A text laid in a scroll region and in view at its scroll: its place in the content less what has scrolled lies inside the viewport.
    private static bool FormsInView(UiScene s, int clip, string text)
    {
        if (clip < 0 || clip >= s.ClipCount)
        {
            return false;
        }
        UiClip c = s.Clips[clip];
        for (int i = 0; i < s.ItemCount; i++)
        {
            ref UiItem it = ref s.Items[i];
            if (it.Kind == UiKind.Text && it.Text == text && it.Clip == clip)
            {
                float top = it.Y - c.Offset;
                return top >= -0.5f && top + it.H <= c.H + 0.5f;
            }
        }
        return false;
    }

    // A text item's box holds every line its words wrap to, whatever the lines it was told.
    private static bool FormsWhole(UiScene s, string text, UiStyle style, out int lines)
    {
        lines = 0;
        for (int i = 0; i < s.ItemCount; i++)
        {
            ref UiItem it = ref s.Items[i];
            if (it.Kind != UiKind.Text || it.Text != text)
            {
                continue;
            }
            lines = UiTextRules.Lines(text, style, it.W, 0, out float _);
            return it.Style == style && it.H + 0.5f >= UiTextRules.WrappedHeight(text, style, it.W, 0) && (int)it.Value >= lines;
        }
        return false;
    }

    // ------------------------------------------------------------------------------------------------- a form's rows, whole

    private static void UiForms3Rows()
    {
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Form");
        const float w = 560f;

        // A note of many sentences takes every line it needs (it was cut at four).
        string note = Sentences(12);
        var rows = new[] { new UiFormRow { Kind = UiFormKind.Note, Label = note } };
        var s = new UiScene();
        s.Begin("note", frame.Width, frame.Height, true);
        UiFormRows.Lay(s, UiAt.Screen, rows, 1, new UiFormPick(), 20f, 20f, w);
        bool whole = FormsWhole(s, note, UiStyle.Small, out int noteLines);
        Check(whole && noteLines > 4, "forms3: a note of " + noteLines + " lines is drawn whole, in a box that holds them all");
        Check(UiCheckRules.Unfit(s, null) == 0, "forms3: a long note leaves nothing the checks call cut");

        // An entry (a line of a list with its button) of a very long text: every line, the row as tall as they need.
        string entry = Sentences(20);
        rows = new[] { new UiFormRow { Kind = UiFormKind.Entry, Label = entry, Action = "Cancel contract" } };
        s = new UiScene();
        s.Begin("entry", frame.Width, frame.Height, true);
        UiFormRows.Lay(s, UiAt.Screen, rows, 1, new UiFormPick(), 20f, 20f, w);
        whole = FormsWhole(s, entry, UiStyle.Body, out int entryLines);
        float tall = UiFormRows.RowHeight(ref rows[0], w);
        Check(whole && entryLines > 3 && tall >= entryLines * UiTextRules.LineHeight(UiStyle.Body), "forms3: an entry of " + entryLines + " lines is drawn whole and its row grows to hold them (" + (int)tall + " px)");

        // A control's label that needs four lines beside its control is drawn whole (it was cut at two).
        string label = "Deliver to the business at the end of the long street by the river where the loading dock is round the back";
        rows = new[] { new UiFormRow { Kind = UiFormKind.Amount, Label = label, Value = "1", CanLess = true, CanMore = true } };
        s = new UiScene();
        s.Begin("label", 420f, frame.Height, true);
        UiFormRows.Lay(s, UiAt.Screen, rows, 1, new UiFormPick(), 0f, 0f, 420f);
        whole = FormsWhole(s, label, UiStyle.Body, out int labelLines);
        Check(whole && labelLines > 2, "forms3: a label of " + labelLines + " lines beside its control is drawn whole");

        // A chosen option too long for its field is read whole under the row.
        string business = "The Corner Bakery and Delicatessen at the Corner of Fifth Avenue and the Long Winding Road";
        rows = new[] { new UiFormRow { Kind = UiFormKind.Choice, Label = "Business", Value = business } };
        s = new UiScene();
        s.Begin("choice", frame.Width, frame.Height, true);
        UiFormRows.Lay(s, UiAt.Screen, rows, 1, new UiFormPick(), 20f, 20f, w);
        bool read = false;
        for (int i = 0; i < s.ItemCount; i++)
        {
            read |= s.Items[i].Kind == UiKind.Text && s.Items[i].Text == business && (s.Items[i].Flags & UiFlags.Wrap) != 0;
        }
        Check(read, "forms3: a chosen option too long for its field stands whole in a line under the row");
        var short1 = new[] { new UiFormRow { Kind = UiFormKind.Choice, Label = "Business", Value = "Bakery" } };
        Check(UiFormRows.RowHeight(ref short1[0], w) < UiFormRows.RowHeight(ref rows[0], w), "forms3: a short choice keeps its one line");

        // A form taller than its room scrolls, and a note taller than the room itself shows as it scrolls past.
        rows = new[]
        {
            new UiFormRow { Kind = UiFormKind.Heading, Label = "Contract" },
            new UiFormRow { Kind = UiFormKind.Note, Label = Sentences(40) }
        };
        var pick = new UiFormPick { Most = 220f };
        s = new UiScene();
        s.Begin("scroll", frame.Width, frame.Height, true);
        UiFormRows.Lay(s, UiAt.Screen, rows, 2, pick, 20f, 20f, w);
        bool clipped = pick.Clip >= 0 && pick.Clip < s.ClipCount && s.Clips[pick.Clip].ContentHeight > s.Clips[pick.Clip].H + 1f;
        bool shown = FormsDrawn(s, it => it.Text != null && it.Text.StartsWith("Sentence number 0")).Length > 0;
        Check(clipped && shown, "forms3: a note taller than its form's room scrolls inside it and shows from its first line");
        pick.Offset = 100000f;
        s = new UiScene();
        s.Begin("scroll", frame.Width, frame.Height, true);
        UiFormRows.Lay(s, UiAt.Screen, rows, 2, pick, 20f, 20f, w);
        Check(FormsDrawn(s, it => it.Text != null && it.Text.StartsWith("Sentence number 0")).Length > 0 && pick.Offset > 0f, "forms3: scrolled to its end the note is still drawn, the words on its lines");

        // The game's own picture beside a delivery's goods: a picture item with its key, the label after it.
        rows = new[] { new UiFormRow { Kind = UiFormKind.Amount, Label = "Apples", Value = "3", CanLess = true, CanMore = true, Picture = "dialog:picture:1" } };
        s = new UiScene();
        s.Begin("picture", frame.Width, frame.Height, true);
        UiFormRows.Lay(s, UiAt.Screen, rows, 1, new UiFormPick(), 20f, 20f, w);
        bool picture = false;
        float labelX = 0f;
        for (int i = 0; i < s.ItemCount; i++)
        {
            picture |= s.Items[i].Kind == UiKind.Picture && s.Items[i].Text == "dialog:picture:1";
            labelX = s.Items[i].Kind == UiKind.Text && s.Items[i].Text == "Apples" ? s.Items[i].X : labelX;
        }
        Check(picture && labelX >= 20f + UiTokens.ControlSmall, "forms3: a line with the game's picture draws it at the left with the label after it");
        rows[0].Picture = null;
        s = new UiScene();
        s.Begin("picture", frame.Width, frame.Height, true);
        UiFormRows.Lay(s, UiAt.Screen, rows, 1, new UiFormPick(), 20f, 20f, w);
        picture = false;
        for (int i = 0; i < s.ItemCount; i++)
        {
            picture |= s.Items[i].Kind == UiKind.Picture;
        }
        Check(!picture, "forms3: a line with no picture draws none");
    }

    // ------------------------------------------------------------------------------------------------- answers, whole

    private static void UiForms3Answers()
    {
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Answers");

        // A scene answer's meaning of many sentences keeps every one (it was cut at four lines, eight where words are said).
        string detail = Sentences(14);
        float width = SceneViews.ChoiceLine(SceneViews.CardWords);
        UiConsequence[] lines = SceneViews.Meaning(detail, width);
        var joined = new StringBuilder();
        for (int i = 0; lines != null && i < lines.Length; i++)
        {
            joined.Append(lines[i].Text).Append(' ');
        }
        string squash = string.Join(" ", joined.ToString().Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        Check(lines != null && lines.Length > 8 && squash == detail && squash.IndexOf('…') < 0, "forms3: a scene answer's meaning of " + (lines != null ? lines.Length : 0) + " lines keeps every word, none cut");
        Check(SceneViews.MeaningMost == int.MaxValue && SceneViews.SaidMost == int.MaxValue, "forms3: no line limit on an answer's meaning or its said words");

        // Answers longer than the docked conversation's room scroll inside it: every line reachable, the buttons still targets.
        var answers = new UiDecisionChoice[2];
        for (int c = 0; c < 2; c++)
        {
            var many = new UiConsequence[60];
            for (int i = 0; i < many.Length; i++)
            {
                many[i] = new UiConsequence { Text = "Line " + c + "." + i + " of what this answer means" };
            }
            answers[c] = new UiDecisionChoice { Name = "Answer " + (c + 1), Lines = many };
        }
        var talk = new UiConversationModel { Name = "Judge", Messages = new[] { new UiMessageModel { From = UiFrom.Them, Text = "Well?" } }, MessageCount = 1, Choices = answers };
        var s = new UiScene();
        UiConversation.LayDocked(s, talk, frame, true, UiDefaultKeys.Keyboard);
        bool clipped = talk.ChoiceClip >= 0 && talk.ChoiceClip < s.ClipCount && s.Clips[talk.ChoiceClip].ContentHeight > s.Clips[talk.ChoiceClip].H + 1f;
        Check(clipped && FormsInView(s, talk.ChoiceClip, "Line 0.0 of what this answer means") && !FormsInView(s, talk.ChoiceClip, "Line 1.59 of what this answer means"),
            "forms3: answers longer than the conversation's room scroll inside it, from their first line");
        talk.ChoiceOffset = 1000000f;
        s = new UiScene();
        UiConversation.LayDocked(s, talk, frame, true, UiDefaultKeys.Keyboard);
        Check(FormsInView(s, talk.ChoiceClip, "Line 1.59 of what this answer means") && !FormsInView(s, talk.ChoiceClip, "Line 0.0 of what this answer means") && talk.ChoiceOffset > 0f, "forms3: scrolled to their end the last line of the last answer is drawn");
        int pressable = 0;
        for (int i = 0; i < s.NodeCount; i++)
        {
            pressable += s.Nodes[i].Command == UiConversation.ChoiceCommand ? 1 : 0;
        }
        Check(pressable >= 1, "forms3: the answers that scroll are still targets (" + pressable + " in view)");
        var few = new UiConversationModel { Name = "Judge", Messages = talk.Messages, MessageCount = 1, Choices = new[] { new UiDecisionChoice { Name = "Yes", Lines = new[] { new UiConsequence { Text = "Fine" } } } } };
        s = new UiScene();
        UiConversation.LayDocked(s, few, frame, true, UiDefaultKeys.Keyboard);
        Check(few.ChoiceClip == -1, "forms3: a few short answers stand whole, with no scroll");

        // The activity card fits its content: a card with one line and no slider is no taller than its side and the card's padding.
        var plain = new UiActivityModel { Headline = "Take a shower", Lines = new[] { new UiInfoRow { Label = "Hygiene", Value = "+40" } }, Place = "Home", Buttons = new[] { new UiSideButton { Label = "Start", Kind = UiButtonKind.Primary, Key = UiAction.Alt } } };
        float card = UiActivity.CardHeight(plain, UiDefaultKeys.Keyboard);
        Check(card <= 330f, "forms3: the activity card for a one line activity is " + (int)card + " px, fitted to its content");
    }

    // ------------------------------------------------------------------------------------------------- pieces nothing filled

    private static void UiForms3Dead()
    {
        string conv = ModSource("Ui/UiConversation.cs") + ModSource("Ui/UiConversation.Fields.cs");
        Check(!conv.Contains("FormValue") && !conv.Contains("FormOn") && !conv.Contains("FormCanLess") && !conv.Contains("FormLabel") && !conv.Contains("FormHint") && conv.Contains("public UiFormField[] Fields"),
            "forms3: the conversation's single form field, which only the samples filled, is gone; its fields and its rows stay");
        string activity = ModSource("Ui/UiActivity.cs");
        Check(!activity.Contains("StartLabel") && !activity.Contains("StartDisabled") && !activity.Contains("DurationHeld") && ModSource("Ui/Activity/ActivityPanelScreen.cs").Contains("Disabled = !b.interactable"),
            "forms3: the activity's start fields are gone, the game's own button greyed as the game greys it");
        string side = ModSource("Ui/UiSideCard.cs");
        Check(!side.Contains("SliderDisabled") && !side.Contains("SliderHeld"), "forms3: the side card's two slider states, which the game never sets, are gone");
        string wardrobe = ModSource("Ui/UiWardrobe.cs") + ModSource("Ui/Buy/WardrobeScreen.cs");
        Check(!wardrobe.Contains("SliderCount") && !wardrobe.Contains("SliderCommand") && !wardrobe.Contains("UiSliderRow") && !wardrobe.Contains("_sliders") && !wardrobe.Contains("IUiStepping"),
            "forms3: the wardrobe's sliders, held and stepping, dead since the surgeon went back to the game's own screen, are gone");
        string contract = ModSource("Core/GameContract.Buy.cs");
        Check(!contract.Contains("LookSurgery") && !contract.Contains("LookBody") && !contract.Contains("LookStrength") && !contract.Contains("LookWeight") && !contract.Contains("LookApply")
              && !contract.Contains("LookRandomize") && !contract.Contains("LookRevert"), "forms3: the surgeon's members of the game contract, which nothing uses, are gone");
        string sheets = ModSource("Ui/Designer/UiDesignerPanels.Sheets.cs"), sideCard = ModSource("Ui/UiSideCard.cs"), staff = ModSource("Ui/UiStaff.cs");
        Check(sheets.Contains("WholeLines = 64") && !sheets.Contains("ItemsWidth - UiTokens.S4 * 2f, 3)") && !sheets.Contains("NoteWidth - UiTokens.S4 * 2f, 4)") && !sheets.Contains("n.Body, UiFlags.Wrap | UiFlags.Truncate, 4)")
              && sideCard.Contains("BodyLines = 64") && sideCard.Contains("NoteLines = 64") && staff.Contains("p.Note, UiFlags.Label | UiFlags.Wrap, WholeLines)"),
            "forms3: the designer's notes and the side card's and the person page's paragraphs have no line limit");
        Check(ModSource("Ui/Designer/DesignerGame.Catalogue.cs").Contains("c.PlaceOff = View[c.Chosen].onClickItemName == null") && !ModSource("Ui/Designer/UiDesignerModel.cs").Contains("PlaceKey"),
            "forms3: the designer's Place button is greyed where the game's tile has no click, and its unused key field is gone");
        Check(!ModSource("Ui/Overlays/OverlayFactsLive.cs").Contains("TopSkill"), "forms3: the overlay facts' unused top skill is gone");
        Check(ModSource("Ui/UiCard.cs").Contains("FaceRing") && ModSource("Ui/StaffViews.cs").Contains("FaceRing = Mood("), "forms3: the card's face ring stays: the employees' training card fills it with the person's mood");
        Check(UiWardrobe.Commands.Length == 7, "forms3: the wardrobe has seven commands, none for a slider");
    }

    // ------------------------------------------------------------------------------------------------- the sources that carry it out

    private static void UiForms3Sources()
    {
        string form = ModSource("Ui/Dialog/DialogForm.cs");
        Check(form.Contains("Piece.Picture") && form.Contains("DialogKits.PictureOf") && form.Contains("row.Picture = _picture"), "forms3: the walker keeps an icon the game drew beside a line and draws it with the line");
        Check(form.Contains("Piece.Foreign") && form.Contains("_foreign != null") && form.Contains("the game's own panel answers it"),
            "forms3: a control the walker has no row for hands the line back to the game's own panel, said in the log");
        Check(form.Contains("c.Find(\"Selected\") != null") && form.Contains("Piece.Swatch : Piece.Foreign"), "forms3: only a colour's button is a swatch; an icon button of unknown meaning is not drawn as one");
        string hints = ModSource("Ui/Dialog/DialogForm.Hints.cs");
        Check(hints.Contains("BankInvestmentSettings") && hints.Contains("dialog_bank_investment_risk_info_tooltip") && form.Contains("Hints(form, heading)"),
            "forms3: the investment's risk tooltip, which the game shows on hover, stands as a note under the risk once a fund is chosen");
        string loan = ModSource("Ui/Dialog/DialogForm.Loan.cs");
        Check(loan.Contains("bank_dialog_interest_daily_payment") && loan.Contains("econoview_loans_daily_payment") && loan.Contains("bank_dialog_total_daily_payment"),
            "forms3: the loan form shows the game's three daily figures (interest, payment, total) in the game's words");
        Check(ModSource("Ui/DialogRules.cs").Contains("MostRows = 1000"), "forms3: a form reads up to a thousand rows, past anything the game's forms hold");
        string tod = ModSource("Ui/Activity/TimeOfDayScreen.cs");
        Check(tod.Contains("GameContract.DayToolText") && tod.Contains("UiWidgets.Text(_words)"), "forms3: the time of day panel shows the hour in the tool's own words");
        string designer = ModSource("Ui/Designer/DesignerFace.cs") + ModSource("Ui/Designer/DesignerKits.cs");
        Check(designer.Contains("DesignerHints.Read(ref _m.Prompts)") && designer.Contains("HudHints.Mirror(ref into, out _)"),
            "forms3: the designer's hint chips grow to every line the game shows, as the HUD's do");
        Check(ModSource("Ui/HudScreens/HudHints.cs").Contains("public static int Mirror(ref UiPrompt[] into, out int sig)"), "forms3: the hint mirror has the growing form");
        // The settings and options pages: the kit's list is built from the one layout the old pages are built from, every kind of line has its control and its press,
        // and every option of the tree is named once.
        string settings = ModSource("Ui/Settings/SettingsScreen.Rows.cs") + ModSource("Ui/Settings/SettingsScreen.cs");
        Check(settings.Contains("OptionsLayout.AppTabs") && settings.Contains("OptionsLayout.AppSet(slot.Line, value)") && settings.Contains("OptionsLayout.AppRun(slot.Line)") && settings.Contains("Keys.Rebind(")
              && settings.Contains("SlotKind.Switch") && settings.Contains("SlotKind.Slider") && settings.Contains("SlotKind.Choice") && settings.Contains("SlotKind.Key") && settings.Contains("SlotKind.Action") && settings.Contains("LineKind.Test"),
            "forms3: the kit's Settings list is built from the old pages' layout, and every control (switch, slider, choice, key, button, test tool) is pressed through the layout's own set, run and rebind");
        string mods = ModSource("Ui/Options/OptionsScreen.Mods.cs");
        Check(mods.Contains("_settings.GroupCount") && mods.Contains("_settings.GroupName(g)"), "forms3: the Options page's Mods category lists every group of the mod's own options and opens each in the Settings list");
        var seen = new HashSet<string>();
        int named = 0, repeated = 0;
        for (int g = 0; g < RealLifeOverhaul.Meta.OptionsTree.Groups.Length; g++)
        {
            for (int sec = 0; sec < RealLifeOverhaul.Meta.OptionsTree.Groups[g].Sections.Count; sec++)
            {
                RealLifeOverhaul.Meta.TreeSection section = RealLifeOverhaul.Meta.OptionsTree.Groups[g].Sections[sec];
                for (int r = 0; r < section.Rows.Count; r++)
                {
                    if (section.Rows[r].IsButton)
                    {
                        continue;
                    }
                    named += string.IsNullOrEmpty(section.Rows[r].Id) ? 0 : 1;
                    repeated += seen.Add(section.Rows[r].Id) ? 0 : 1;
                }
            }
        }
        Check(named >= 300 && repeated == 0, "forms3: every option of the tree has its id and none stands twice (" + named + " options, " + repeated + " repeated), so each is one row of the list");
        Check(ModSource("Ui/Dialog/SceneKit.cs").Contains("_answers.Clip") && ModSource("Ui/Dialog/DialogScreen.cs").Contains("_m.ChoiceClip") && ModSource("Ui/Contacts/ContactsScreen.cs").Contains("_m.Talk.ChoiceClip"),
            "forms3: the wheel scrolls the answers in the scenes, the dialogue and Contacts");
    }
}
