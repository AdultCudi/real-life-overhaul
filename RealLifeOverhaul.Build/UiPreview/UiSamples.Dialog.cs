using System.Collections.Generic;
using RealLifeOverhaul.Ui;

/// <summary>
/// The game's conversations on the kit (Scripts/Ui/Dialog), each state as the dialogue panel would hand it over: Larry
/// at Jensen Capital asking what he can do, and his loan form with the figures the game works out as the amount is typed;
/// a marketing agent's campaign form, and its business list open; the mod's Business desk face to face with one matter,
/// and after a long run of them; a passer-by in the street, before and after a few words; another mod's piece in the
/// panel offered as the game's own view; the longest names a talk can carry; and the second between two lines, when the
/// game shows no button. The same loan by phone on the call card, and a candidate's wage talk in Contacts with their mood
/// by their line. Uncle Fred's lines as the card at the top: one, one with one more to come, his longest, and a rival's
/// face alone. The mod's six scene windows as the kit draws them: the court's plea and a long charge sheet, the Legal
/// file, the night show's question, a door in a campaign, an interview's round and its end, an investor's question and
/// their offer, a founders' pitch. Plain English as the owner reads the previews; the game's and the mod's words through
/// their keys, so the checks lay out every state in every language.
/// </summary>
internal static partial class UiSamples
{
    /// <summary>The dialogue panel's states, in the previews' order.</summary>
    public static readonly string[] DialogStates =
    {
        "bank_start", "bank_loan", "marketing", "marketing_list", "desk", "desk_full", "street", "street_said", "other_mod", "long_names", "between_lines"
    };

    /// <summary>
    /// The states the screen checks lay out: all but the second between two lines, when the game's line has no button, so
    /// the bar has neither a choice nor a way back to show (the cancel key then does nothing in the game either).
    /// </summary>
    public static bool DialogChecked(string state) => state != "between_lines";

    /// <summary>Uncle Fred's and a rival's lines, in the previews' order.</summary>
    public static readonly string[] MonologueStates = { "fred", "fred_more", "fred_long", "rival" };

    /// <summary>The scenes' pages, in the previews' order.</summary>
    public static readonly string[] SceneStates =
    {
        "court_plea", "court_charges", "legal_file", "fame_show", "office_door", "interview_round", "interview_over", "pitch_question", "pitch_offer", "venture_pitch"
    };

    private const string Bank = "Jensen Capital";
    private const string Bakery = "Harbour Lights Bakery";

    private static string Fill(string words, params string[] pairs)
    {
        for (int i = 0; i + 1 < pairs.Length; i += 2)
        {
            words = words.Replace("{" + pairs[i] + "}", pairs[i + 1]);
        }
        return words;
    }

    // ------------------------------------------------------------------------------------------------- the dialogue panel

    /// <summary>A state of the dialogue panel, as DialogScreen reads it from the game's panel.</summary>
    public static UiConversationModel Dialog(UiWords t, string state)
    {
        var m = new UiConversationModel { Title = t("reallife_ui_dialog_title", "Conversation"), Ring = ContactsRules.MoodRing(-2), Offset = -1f };
        var lines = new List<UiMessageModel>();
        var choices = new List<UiDecisionChoice>();
        string leave = t("itempanelui_buttons_leave", "Leave");
        string back = leave;
        switch (state)
        {
            case "bank_start":
            case "bank_loan":
            {
                Head(m, t("dialog_bank_larry", "Larry"), Bank);
                lines.Add(Them(t("dialog_bank_selector", "Hi there! How can I help you today?")));
                if (state == "bank_start")
                {
                    choices.Add(Choice(t("dialog_bank_new_loan", "New loan")));
                    choices.Add(Choice(t("dialog_bank_new_investment", "New investment")));
                }
                else
                {
                    lines.Add(Me(t("dialog_bank_new_loan", "New loan")));
                    m.Rows = LoanRows(t, true);
                    m.RowCount = m.Rows.Length;
                    choices.Add(Choice(t("common_confirm", "Confirm")));
                }
                choices.Add(Choice(leave));
                break;
            }
            case "marketing":
            case "marketing_list":
            {
                const string agency = "Bright Signal Media";
                Head(m, t("dialog_marketing_agency_npc_name", "Marketing Agent"), agency);
                lines.Add(Them(Fill(t("dialog_marketing_agency_start", "Hello and welcome to {businessName}. What type of advertisement are you interested in?"), "businessName", agency)));
                lines.Add(Me(t("dialog_marketing_agency_start_new_campaign", "New campaign")));
                m.Rows = new[]
                {
                    new UiFormRow { Kind = UiFormKind.Heading, Label = t("dialog_marketing_agency_campaign_settings_header", "Campaign settings") },
                    new UiFormRow { Kind = UiFormKind.Choice, Label = t("common_business", "Business"), Value = Bakery },
                    new UiFormRow { Kind = UiFormKind.Switch, Label = t("marketingtypename_friendbookads", "Friendbook Ads"), On = true },
                    new UiFormRow { Kind = UiFormKind.Switch, Label = t("marketingtypename_jitterads", "Jitter Ads") },
                    new UiFormRow { Kind = UiFormKind.Switch, Label = t("marketingtypename_latergramads", "Latergram Ads") },
                    new UiFormRow { Kind = UiFormKind.Switch, Label = t("marketingtypename_voogleads", "Voogle Ads"), On = true },
                    new UiFormRow { Kind = UiFormKind.Figure, Label = t("common_price_per_day", "Price per day"), Value = "$240" },
                };
                m.RowCount = m.Rows.Length;
                if (state == "marketing_list")
                {
                    // The business's list open under its choice, the one chosen marked, as the game's Dropdown holds them.
                    m.Pick.Row = 1;
                    m.Pick.Options = new[] { Bakery, "Midtown Fresh Market", "Iron Works Gym", "Canal Street Books" };
                    m.Pick.Count = m.Pick.Options.Length;
                    m.Pick.Chosen = 0;
                }
                choices.Add(Choice(t("common_confirm", "Confirm")));
                choices.Add(Choice(leave));
                break;
            }
            case "desk":
            {
                Head(m, t("reallife-business", "Business desk"), Bakery);
                lines.Add(Them(Matter(t, t("reallife_biz_undercut_name", "A competitor undercuts you"), Undercut(t), "412")));
                choices.Add(Choice(t("reallife_biz_desk_options", "What can I do?")));
                choices.Add(Choice(t("reallife_biz_desk_next", "Next matter")));
                choices.Add(Choice(leave));
                break;
            }
            case "desk_full":
            {
                Head(m, t("reallife-business", "Business desk"), Bakery);
                lines.Add(Them(Matter(t, t("reallife_biz_health_name", "Health inspection"),
                    Fill(t("reallife_biz_health_start", "The health inspector will call at {business} tomorrow at {time}. They look at one thing: how clean the place is. It is at {clean} of 100 now; {pass} is a pass."),
                        "business", Bakery, "time", "10:00", "clean", "64", "pass", "70"), "411")));
                lines.Add(Me(t("reallife_biz_desk_next", "Next matter")));
                lines.Add(Them(Matter(t, t("reallife_biz_supplierlate_name", "A late delivery"),
                    Fill(t("reallife_biz_supplierlate_start", "The wholesaler for {business} says the next delivery will be a day late (a lorry off the road) unless you pay {amount} for an express run. Answer today."),
                        "business", Bakery, "amount", "$180"), "410")));
                lines.Add(Me(t("reallife_biz_desk_next", "Next matter")));
                lines.Add(Them(Matter(t, t("reallife_biz_undercut_name", "A competitor undercuts you"), Undercut(t), "412")));
                lines.Add(Me(t("reallife_biz_desk_options", "What can I do?")));
                lines.Add(Them(Fill(t("reallife_biz_desk_option", "Choice {number} of {count}: {text}"), "number", "1", "count", "3",
                    "text", Fill(t("reallife_biz_undercut_marketing", "A week of leaflets and local adverts: {amount}. It cancels the dip at once and brings a few extra customers in for three days."), "amount", "$450"))));
                choices.Add(Choice(t("reallife_biz_undercut_do_marketing", "Run a marketing push")));
                choices.Add(Choice(t("reallife_biz_desk_another", "Another way")));
                back = t("reallife_biz_desk_later", "Decide later");
                choices.Add(Choice(back));
                break;
            }
            case "street":
            case "street_said":
            {
                Head(m, t("reallife_street_stranger", "A passer-by"), t("reallife_ui_dialog_in_street", "In the street"));
                lines.Add(Them(t("reallife_street_open_warm_1", "Oh, hello. What can I do for you?")));
                if (state == "street")
                {
                    choices.Add(Choice(t("reallife_street_ask_day", "How's your day going?")));
                    choices.Add(Choice(t("reallife_social_btn_else", "Something else")));
                    back = t("reallife_street_btn_goodbye", "Goodbye");
                }
                else
                {
                    lines.Add(Me(t("reallife_street_ask_day", "How's your day going?")));
                    lines.Add(Them(t("reallife_street_day_good_2", "Not bad at all. I finished early and the sun's out.")));
                    choices.Add(Choice(t("reallife_street_ask_area", "What's good around here?")));
                    choices.Add(Choice(t("reallife_social_btn_else", "Something else")));
                    back = t("reallife_street_ask_say", "Say something of your own");
                }
                choices.Add(Choice(back));
                break;
            }
            case "other_mod":
            {
                Head(m, t("dialog_recruitment_agency_npc_name", "Recruitment Agent"), "Talent Bridge Recruitment");
                lines.Add(Them(t("dialog_recruitment_agency_already_has_campaign_new", "Hello, how can we help you today?")));
                choices.Add(Choice(t("dialog_recruitment_agency_manage_campaigns", "Manage Campaigns")));
                choices.Add(Choice(leave));
                choices.Add(Choice(t("reallife_ui_contacts_game_view", "Open the game's own view")));   // another mod's piece beside the game's lines
                break;
            }
            case "long_names":
            {
                Head(m, "Maximiliana Featherstonehaugh-Okonkwo", "Featherstonehaugh, Lindqvist and Okonkwo Talent Partners, 1200 Fifth Avenue");
                lines.Add(Them("We have shortlisted four candidates for the evening manager's post at the Hell's Kitchen warehouse, two of them with ten years in logistics."));
                lines.Add(Me(t("dialog_recruitment_agency_manage_campaigns", "Manage Campaigns")));
                lines.Add(Them("The campaign for the Midtown Fresh Market cashiers is still running and ends on day 418, when the last interviews are done."));
                choices.Add(Choice("Ask about the evening manager's candidates for the warehouse in Hell's Kitchen"));
                choices.Add(Choice("Extend the Midtown Fresh Market cashiers' campaign by another week"));
                choices.Add(Choice(leave));
                break;
            }
            case "between_lines":
            {
                Head(m, t("dialog_bank_larry", "Larry"), Bank);
                lines.Add(Them(t("dialog_bank_selector", "Hi there! How can I help you today?")));
                lines.Add(Me(t("dialog_bank_new_loan", "New loan")));
                back = null;   // the next line is coming: no button yet
                break;
            }
        }
        m.Messages = lines.ToArray();
        m.MessageCount = lines.Count;
        m.Choices = choices.ToArray();
        m.Typing = choices.Count == 0 && m.RowCount == 0;
        var prompts = new List<UiPrompt>(3);
        if (choices.Count > 0 || m.RowCount > 0)
        {
            prompts.Add(new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") });
            prompts.Add(new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_choose", "Choose") });
        }
        if (back != null)
        {
            prompts.Add(new UiPrompt { Action = UiAction.Back, Label = back });
        }
        m.Prompts = prompts.ToArray();
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>The bank's loan form as the game lays it: the amount typed, and the three figures it works out from it.</summary>
    public static UiFormRow[] LoanRows(UiWords t, bool heading)
    {
        var rows = new List<UiFormRow>(5);
        if (heading)
        {
            rows.Add(new UiFormRow { Kind = UiFormKind.Heading, Label = t("dialog_bank_loan_amount_header", "Loan amount") });   // the line's own question
        }
        rows.Add(new UiFormRow { Kind = UiFormKind.Text, Label = t("dialog_bank_amount_to_loan", "Amount to loan"), Value = "25000" });
        rows.Add(new UiFormRow { Kind = UiFormKind.Figure, Label = Fill(t("bank_dialog_interest_daily_payment", "Daily interest ({rate}%)"), "rate", "9"), Value = "$37" });
        rows.Add(new UiFormRow { Kind = UiFormKind.Figure, Label = t("econoview_loans_daily_payment", "Daily payment"), Value = "$208" });
        rows.Add(new UiFormRow { Kind = UiFormKind.Figure, Label = t("bank_dialog_total_daily_payment", "Total daily payment"), Value = "$245", Strong = true });
        return rows.ToArray();
    }

    /// <summary>The panel laid as the screen lays it, docked at the right over the live scene.</summary>
    public static void DialogDocked(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, string state) => UiConversation.LayDocked(s, Dialog(t, state), f, dark, k);

    private static void Head(UiConversationModel m, string name, string where)
    {
        m.Name = name;
        m.Hue = UiColour.HueOf(name);
        m.Role = where;
    }

    private static UiMessageModel Them(string words) => new UiMessageModel { From = UiFrom.Them, Text = words };

    private static UiMessageModel Me(string words) => new UiMessageModel { From = UiFrom.Me, Text = words };

    private static UiDecisionChoice Choice(string words) => new UiDecisionChoice { Name = DialogRules.OneLine(words) };

    // A matter as the desk tells it: its name and business, what happened, and when it must be decided.
    private static string Matter(UiWords t, string name, string text, string day) =>
        Fill(t("reallife_biz_desk_issue", "{name}: {business}\n{text}\nDecide by day {day}, {time} (today is day {today})."),
            "name", name, "business", Bakery, "text", text, "day", day, "time", "18:00", "today", "410");

    private static string Undercut(UiWords t) =>
        Fill(t("reallife_biz_undercut_start", "{rival} has opened near {business} and is undercutting you. Expect about {percent}% fewer customers for {length} days. Your Business desk has ways to answer it."),
            "rival", "Sweet Rise", "business", Bakery, "percent", "30", "length", "5");

    // ------------------------------------------------------------------------------------------------- calls

    /// <summary>The bank's loan asked for by phone: Contacts with the call card over it, the form under the line.</summary>
    public static UiContactsModel LoanCall(UiWords t)
    {
        UiContactsModel m = ContactsSample(t, "call_replies");
        m.Call = new UiCallModel
        {
            Name = "Midtown Savings Bank", Icon = UiIcon.Bank, Role = t("contactcategoryname_finance", "Finance"), Length = "1:12",
            Line = t("dialog_bank_loan_amount_header", "Loan amount"),   // the line's header: a form's line has no message of its own
            Rows = LoanRows(t, false),
            Choices = new[] { Choice(t("common_confirm", "Confirm")) },
            Buttons = new[] { new UiActionModel { Label = t("dialog_hang_up_button", "Hang up"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Phone, Key = UiAction.Back } },
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_choose", "Choose") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
            },
            PromptCount = 3
        };
        m.Call.RowCount = m.Call.Rows.Length;
        return m;
    }

    /// <summary>A candidate's wage talk in Contacts, their reply to the player's offer with the mood the game shows by it.</summary>
    public static UiContactsModel MoodCall(UiWords t)
    {
        UiContactsModel m = ContactsSample(t, "wage");
        for (int i = m.Talk.MessageCount - 1; i >= 0; i--)
        {
            if (m.Talk.Messages[i].From == UiFrom.Them)
            {
                m.Talk.Messages[i].Mood = DialogRules.MoodIcon(0);   // unsure: the game's yellow face
                break;
            }
        }
        return m;
    }

    // ------------------------------------------------------------------------------------------------- spoken lines

    /// <summary>A spoken line as MonologueScreen reads it: Uncle Fred named by his face, a rival's face alone.</summary>
    public static UiMonologueModel Monologue(UiWords t, string state)
    {
        string fred = t("uncle_fred", "Uncle Fred");
        switch (state)
        {
            case "fred_more":
                return new UiMonologueModel
                {
                    Name = fred, Hue = UiColour.HueOf(fred), More = Fill(t("reallife_ui_monologue_more", "{count} more"), "count", "1"),
                    Line = t("ba:messagetype_tutorial_quest_find_a_job_uncle_fred",
                        "Rise and shine, kiddo! You survived your first night in New York City, congratulations! I’m sure it helped that I paid yesterday’s rent too. But from here on out, no more handouts! Today you need to find a job and start to earn your own money!")
                };
            case "fred_long":
                return new UiMonologueModel
                {
                    Name = fred, Hue = UiColour.HueOf(fred),
                    Line = t("ba:messagetype_tutorial_quest_casino_uncle_fred",
                        "Ah, much better. With any luck, your money will be earning some nice interest over time. And speaking of luck, this calls for a celebration! Why don't you head out to the Casino and see if you can make a quick buck...or lose a few. As long as your businesses are healthy, it’s all fun and games, right?")
                };
            case "rival":
                return new UiMonologueModel
                {
                    Picture = "sample:rival", Hue = 30f,
                    Line = "Nice little shop you have there. Enjoy it while it lasts, because my stores open on your street next week."
                };
            default:
                return new UiMonologueModel
                {
                    Name = fred, Hue = UiColour.HueOf(fred),
                    Line = t("ba:messagetype_tutorial_quest_get_some_food_uncle_fred",
                        "Very nice, you found the apartment! But it’s looking really empty in here. Let’s get you a few essentials to fill up the place. We’ll start with some food. You can't conquer the world on an empty stomach.")
                };
        }
    }

    // ------------------------------------------------------------------------------------------------- scenes

    /// <summary>The words the scenes' screen takes from the game and the mod.</summary>
    public static SceneWords SceneWordsOf(UiWords t) => new SceneWords
    {
        Move = t("reallife_ui_prompt_move", "Move"), Choose = t("reallife_ui_prompt_choose", "Choose"), Back = t("common_cancel", "Cancel"), Menu = t("input_key_menu", "Menu")
    };

    /// <summary>A scene's page laid as SceneScreen lays it: the decision card for a moment, the conversation for questions and answers.</summary>
    public static void Scene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, string state)
    {
        var thread = new List<SceneSaid>();
        ScenePage page = ScenePageOf(t, state, thread);
        SceneViews.Lay(s, page, thread, SceneWordsOf(t), -1f, f, dark, k);
    }

    /// <summary>A scene's page as its owner fills it through its list (SceneList), and the conversation so far.</summary>
    public static ScenePage ScenePageOf(UiWords t, string state, List<SceneSaid> thread)
    {
        var p = new ScenePage();
        switch (state)
        {
            case "court_plea":
            case "court_charges":
            {
                string judge = t("reallife_justice_judge_1", "Judge Whitlock"), prosecutor = t("reallife_justice_prosecutor_1", "Prosecutor Vance");
                string kicker = Fill(t("reallife_justice_court_subtitle", "Case {number}, day {day}"), "number", "114", "day", "410");
                p.Reset(t("reallife_justice_court_title", "Criminal Court"), kicker);
                Moment(p, kicker, UiIcon.Scales, prosecutor, judge, "Whitmore & Vale");
                if (state == "court_plea")
                {
                    string aside = Fill(t("reallife_justice_court_aside", "Your side of the table, quietly: on {charge}, the case against you stands at about {percent} in 100 right now."),
                        "charge", t("reallife_justice_charge_reckless", "Dangerous driving"), "percent", "62");
                    p.Notes.Add(new SceneNote { Name = judge, Detail = t("reallife_justice_court_plea", "You have heard the charges. How do you plead?") + "\n\n" + aside });
                    Answer(p, 0, t("reallife_justice_plea_guilty", "Guilty"), t("reallife_justice_plea_guilty_hint", "No trial. Convicted on every charge that stands, with a quarter off the sentence."));
                    Answer(p, 1, t("reallife_justice_plea_not_guilty", "Not guilty"),
                        Fill(t("reallife_justice_plea_not_guilty_hint", "A trial of {rounds} rounds. The case against you starts at {percent} in 100; your answers move it."), "rounds", "3", "percent", "62"));
                    Answer(p, 2, t("reallife_justice_plea_deal", "Take the deal"),
                        Fill(t("reallife_justice_deal_reduced", "You plead guilty to {to} instead of {from}."), "to", t("reallife_justice_charge_evading", "Evading the police"),
                            "from", t("reallife_justice_charge_reckless", "Dangerous driving")) + " " + t("reallife_justice_deal_tail", "Sentence cut by 40%, and no prison."));
                }
                else
                {
                    // The charge sheet read out: a body longer than the card holds, so the page takes the conversation, which scrolls.
                    string sheet = t("reallife_justice_court_charges", "The accused is charged as follows:") + "\n"
                                   + "\n  - " + t("reallife_justice_charge_reckless", "Dangerous driving") + "\n  - " + t("reallife_justice_charge_evading", "Evading the police")
                                   + "\n  - " + t("reallife_justice_charge_hit_run_injury", "Leaving the scene of a crash with injury")
                                   + "\n  - " + t("reallife_justice_charge_injury_driving", "Causing injury by dangerous driving")
                                   + "\n\n" + Fill(t("reallife_justice_court_withdrawn", "Having looked at the evidence again, the prosecution withdraws: {charges}."), "charges", t("reallife_justice_charge_hit_run", "Leaving the scene of a crash"))
                                   + "\n\n" + t("reallife_justice_court_on_probation", "The accused was on probation at the time. If convicted, the court will take that into account.")
                                   + "\n\n" + t("reallife_justice_court_call_warrant", "I note that the accused did not come when this case was first listed, and had to be brought. That is before me too.")
                                   + "\n\n" + t("reallife_justice_court_appointed", "You have come without counsel. The court appoints the public defender to act for you.");
                    p.Notes.Add(new SceneNote { Name = prosecutor, Detail = sheet });
                    Answer(p, 0, t("reallife_justice_continue", "Continue"), null);
                }
                p.Locked = true;
                break;
            }
            case "legal_file":
            {
                string kicker = Fill(t("reallife_justice_file_subtitle", "Day {day}, {hour}:00"), "day", "410", "hour", "14");
                p.Reset(t("reallife_justice_file_title", "Legal file"), kicker);
                Moment(p, kicker, UiIcon.Briefcase);
                string body = Fill(t("reallife_justice_file_case", "CASE {number}: {charges}."), "number", "114", "charges", t("reallife_justice_charge_reckless", "Dangerous driving"))
                              + " " + Fill(t("reallife_justice_file_case_date", "Hearing: day {day}, 9:00 to 17:00, {place}."), "day", "412", "place", "the courthouse on Centre Street")
                              + "\n" + Fill(t("reallife_justice_file_counsel", "Counsel: {name}"), "name", "Whitmore & Vale")
                              + "\n" + Fill(t("reallife_justice_file_owed", "Owed: {amount} in all."), "amount", "$1,850")
                              + "\n  - " + Fill(t("reallife_justice_file_debt_fine", "Court fine: {left} left, {perday} a day"), "left", "$1,850", "perday", "$50")
                              + "\n" + t("reallife_justice_file_probation_none", "Probation: none.") + "\n" + t("reallife_justice_file_licence_ok", "Driving licence: not suspended.");
                p.Notes.Add(new SceneNote { Name = string.Empty, Detail = body });
                Answer(p, 0, t("reallife_justice_file_actions", "What I can do"), t("reallife_justice_file_actions_hint", "Pay off, answer a claim, appeal, community service."));
                Answer(p, 1, t("reallife_justice_file_record", "My record"), Fill(t("reallife_justice_file_record_hint", "Entries: {count}, and closed cases."), "count", "2"));
                Answer(p, 2, t("reallife_justice_file_close", "Close the file"), null);
                p.Locked = false;   // the one scene the player may close
                break;
            }
            case "fame_show":
            {
                string host = t("reallife_fame_show_host", "The host");
                string kicker = Fill(t("reallife_fame_scene_progress", "{place}, question {number} of {of}"), "place", t("reallife_fame_show_place", "Late Lights, studio one"), "number", "1", "of", "3");
                p.Reset(t("reallife_fame_invite_0", "Late Lights, the night show"), kicker);
                Moment(p, kicker, UiIcon.Star, host);
                p.Notes.Add(new SceneNote
                {
                    Name = host,
                    Detail = Fill(t("reallife_fame_show_open", "The band plays you on and the audience claps. The host leans forward. \"{question}\""),
                        "question", t("reallife_fame_q_rise", "You came to this city with very little. How did you get where you are?"))
                });
                Answer(p, 0, t("reallife_fame_a_rise_0", "Promote the business"), t("reallife_fame_say_rise_0", "\"Hard work, and the best shops in town. Come and see for yourself.\""));
                Answer(p, 1, t("reallife_fame_a_rise_1", "Credit the staff"), t("reallife_fame_say_rise_1", "\"A good team and some luck. My staff deserve the credit.\""));
                p.Locked = true;
                break;
            }
            case "office_door":
            {
                string speaker = t("reallife_civic_office_door_speaker", "At the door");
                const string address = "48 Mulberry Street";
                p.Reset(t("reallife_civic_office_door_title", "A door"), address);
                Moment(p, address, UiIcon.Flag, speaker);
                string theirs = t("reallife_civic_issue_safety", "safety on the streets"), mine = t("reallife_civic_issue_business", "doing business");
                p.Notes.Add(new SceneNote
                {
                    Name = speaker,
                    Detail = Fill(t("reallife_civic_office_door_open", "{who} opens the door. They are worried about {issue}."), "who", t("reallife_civic_office_door_civil", "Somebody polite but busy"), "issue", theirs)
                });
                Answer(p, 0, Fill(t("reallife_civic_office_door_talk_theirs", "Talk about {issue}"), "issue", theirs),
                    Fill(t("reallife_civic_office_hint_plank", "You stand on: {policy}."), "policy", t("reallife_civic_policy_policing_up", "More officers on the street")));
                Answer(p, 1, Fill(t("reallife_civic_office_door_talk_mine", "Talk about {issue}, your own issue"), "issue", mine), t("reallife_civic_office_hint_no_plank", "Nothing in your platform speaks to it."));
                Answer(p, 2, t("reallife_civic_office_door_ask_vote", "Ask for their vote"), t("reallife_civic_office_hint_ask", "Straight to the point: a friend says yes, a stranger minds."));
                p.Locked = true;
                break;
            }
            case "interview_round":
            case "interview_over":
            {
                const string name = "Nadia Haddad";
                string role = t("ba:skill_customerservice", "Customer Service");
                string title = Fill(t("reallife_hire_meet_title", "Interview: {name}, {role}"), "name", name, "role", role);
                if (state == "interview_round")
                {
                    // The second round: the first question and its answer already in the thread.
                    thread.Add(new SceneSaid { From = UiFrom.Them, Text = Fill(t("reallife_hire_meet_opening", "{name} sits down across the desk."), "name", name) });
                    thread.Add(new SceneSaid { From = UiFrom.Me, Text = t("reallife_hire_topic_project", "Their last project") });
                    string heard = Fill(t("reallife_hire_meet_heard_years", "{name} talks you through it: {years} of real experience, {a} at {score}."), "name", name, "years", "4 years",
                        "a", t("ba:skill_customerservice", "Customer Service"), "score", "71");
                    p.Reset(title, heard + "\n<size=80%>" + Fill(t("reallife_hire_meet_progress", "Question {number} of {of}"), "number", "2", "of", "4") + "</size>");
                    Talk(p, name, role);
                    Answer(p, 0, t("reallife_hire_topic_technical", "A technical problem"), t("reallife_hire_topic_technical_detail", "Shows how good they really are."));
                    Answer(p, 2, t("reallife_hire_topic_motive", "Why this job"), t("reallife_hire_topic_motive_detail", "Shows how they would settle in."));
                    Answer(p, 3, t("reallife_hire_topic_pay", "What they expect to be paid"), t("reallife_hire_topic_pay_detail", "Shows their ask and where they would like to work."));
                    Answer(p, 4, t("reallife_hire_topic_team", "How they work in a team"), t("reallife_hire_topic_team_detail", "Shows a little of everything."));
                    Answer(p, 9, t("reallife_hire_meet_leave", "End the interview"), t("reallife_hire_meet_leave_detail", "Thank them and finish here."));
                }
                else
                {
                    string over = t("reallife_hire_meet_over", "The interview is over.");
                    p.Reset(title, over);
                    Moment(p, over, UiIcon.IdBadge, name);
                    p.Notes.Add(new SceneNote
                    {
                        Name = name,
                        Detail = Fill(t("reallife_hire_meet_summary", "{name} is {interest} keen. Skill {skill}, asks {ask}. You can make an offer from the Applicants tab."),
                            "name", name, "interest", "fairly", "skill", "71%", "ask", "$19.50 an hour")
                    });
                    Answer(p, 10, t("reallife_hire_meet_close", "Close"), t("reallife_hire_meet_close_detail", "Back to the city."));
                }
                p.Locked = true;
                break;
            }
            case "pitch_question":
            case "pitch_offer":
            {
                const string investor = "Adele Fenwick";
                string title = Fill(t("reallife_ven_meet_title", "Meeting with {name}"), "name", investor);
                if (state == "pitch_question")
                {
                    thread.Add(new SceneSaid { From = UiFrom.Them, Text = Fill(Part(t("reallife_ven_q_f04", "What does {business} make on an average day, taking the last week?|the week's daily profit"), 0), "business", Bakery) });
                    thread.Add(new SceneSaid { From = UiFrom.Me, Text = "$1,240" });
                    string q = MoneyFor(t);
                    p.Reset(title, Part(q, 0) + "\n<size=80%>" + Fill(t("reallife_ven_meet_progress", "Question {number} of {of}"), "number", "2", "of", "5") + "</size>");
                    Talk(p, investor, null);
                    for (int i = 0; i < 3; i++)
                    {
                        string words = Part(q, i + 1);
                        p.Entries.Add(new SceneEntry { Id = i, Name = Fill(t("reallife_ven_meet_answer", "Answer {number}"), "number", (i + 1).ToString()), Detail = words, Said = words, Enabled = true });
                    }
                    Answer(p, 9, t("reallife_ven_meet_leave", "Leave the meeting"), Fill(t("reallife_ven_meet_leave_detail", "No decision is made. You can approach {name} again from day {day}."), "name", investor, "day", "417"));
                }
                else
                {
                    string result = t("reallife_ven_meet_result_yes", "The decision: an offer");
                    p.Reset(title, result);
                    Moment(p, result, UiIcon.PiggyBank, investor);
                    p.Notes.Add(new SceneNote
                    {
                        Name = investor,
                        Detail = Fill(t("reallife_ven_verdict_invest", "The answer is yes: {amount} for {share} of each day's profit of {business}."), "amount", "$40,000", "share", "12%", "business", Bakery)
                                 + " " + Fill(t("reallife_ven_verdict_helped", "What helped most was {topic}."), "topic", Part(MoneyFor(t), 4))
                    });
                    Answer(p, 0, t("reallife_ven_meet_take_title", "Accept the terms"),
                        Fill(t("reallife_ven_meet_take", "{amount} is paid to you now for {share} of each day's profit of {business}. They expect its recent daily profit to grow by {growth}% within {days} days."),
                            "amount", "$40,000", "share", "12%", "business", Bakery, "growth", "15", "days", "30"));
                    Answer(p, 1, t("reallife_ven_meet_think", "Think it over"), Fill(t("reallife_ven_meet_think_detail", "The offer waits in Ventures until day {day}. You can accept, refuse or counter it there."), "day", "417"));
                    Answer(p, 2, t("reallife_ven_meet_decline", "Decline"), null);
                }
                p.Locked = true;
                break;
            }
            case "venture_pitch":
            {
                const string startup = "Brightloop";
                thread.Add(new SceneSaid { From = UiFrom.Them, Text = Fill(t("reallife_vc_meet_founders_open", "{founder} of {name} opens the deck."), "founder", "Maya Chen", "name", startup) });
                thread.Add(new SceneSaid { From = UiFrom.Me, Text = t("reallife_vc_topic_revenue", "What is the revenue, really?") });
                p.Reset(Fill(t("reallife_vc_meet_title_founders", "Pitch: {name}"), "name", startup),
                    "About $38,000 a month, most of it from three customers who renew every quarter.\n<size=80%>" + Fill(t("reallife_hire_meet_progress", "Question {number} of {of}"), "number", "2", "of", "3") + "</size>");
                Talk(p, startup, null);
                Answer(p, 1, t("reallife_vc_topic_team", "Who is on the team?"), null);
                Answer(p, 2, t("reallife_vc_topic_market", "How big is the market?"), null);
                Answer(p, 3, t("reallife_vc_topic_burn", "How fast does the money go?"), null);
                Answer(p, 4, t("reallife_vc_topic_terms", "Are the terms fixed?"), null);
                Answer(p, 9, t("reallife_vc_meet_leave", "End the meeting"), null);
                p.Locked = true;
                break;
            }
        }
        return p;
    }

    // The page is a moment: its place or time as the kicker, the emblem, those present (SceneList.Moment).
    private static void Moment(ScenePage p, string kicker, UiIcon emblem, params string[] faces)
    {
        p.Look = SceneLook.Decision;
        p.Kicker = kicker;
        p.Emblem = emblem;
        p.Faces = faces;
        p.InfoSaid = false;
    }

    // The page is questions and answers: who speaks and who they are to the player (SceneList.Talk).
    private static void Talk(ScenePage p, string speaker, string role)
    {
        p.Look = SceneLook.Talk;
        p.Speaker = speaker;
        p.Role = role;
        p.InfoSaid = true;
    }

    private static void Answer(ScenePage p, int id, string name, string detail) => p.Entries.Add(new SceneEntry { Id = id, Name = name, Detail = detail, Enabled = true });

    // An investor's question with its three answers and its topic, as the pitch keeps them in one text.
    private static string MoneyFor(UiWords t) =>
        t("reallife_ven_q_j01", "What is the money for?|More stock and a second till, so that we stop losing sales at busy hours.|A second site in a better district within the month.|This month's rent and wages.|what the money is for");

    // One part of a pitch question's words: the question, its answers, and the topic last.
    private static string Part(string words, int index)
    {
        string[] parts = words.Split('|');
        return index < parts.Length ? parts[index] : string.Empty;
    }
}
