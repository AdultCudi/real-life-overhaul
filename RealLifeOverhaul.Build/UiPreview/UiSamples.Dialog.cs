using System.Collections.Generic;
using RealLifeOverhaul.Ui;

/// <summary>
/// The game's conversations on the kit (Scripts/Ui/Dialog), each state as the dialogue panel would hand it over: Larry
/// at Jensen Capital asking what he can do, and his loan form with its term, its rate, its payment and the sum to repay,
/// what taking it pays and costs under Confirm and the bank's own answers past the game's three (a point off the rate,
/// greyed with the reason, and a credit line instead); a marketing agency's campaign form, and its business list open; a
/// wholesale store's delivery contract, an importer's partnership with its agent chosen and before, a furniture delivery
/// with its goods, an installation and a move booked, a health insurer's partnership chosen and before (its skill not set
/// yet), a recruitment campaign and a private driver's contract, each with what its confirm pays and costs under it, and a
/// hotel desk of the mod's own with the price under each answer; the
/// mod's Business desk face to face with one matter, and after a long run of them; a passer-by in the street, before and
/// after a few words; a health insurer's counter offer with their mood in a word by their face; another mod's piece in the
/// panel offered as the game's own view; the longest names a talk can carry; and the second between two lines, when the
/// game shows no button. The same loan by phone on the call card, the point off there to be had, a tow asked for by phone
/// with its two places and its price, and a candidate's wage talk in Contacts with their mood by their line and what the
/// offer pays under Send Offer. Uncle Fred's lines as the card at the top: one with how far it has been
/// said, one of two with Skip and Next, his longest with no sound of its own, and a rival's face alone. The mod's six scene
/// windows as the kit draws them: the court's plea with who is present and what each answer costs, a tax case with the
/// city's settlement, a long charge sheet, the Legal file, the night show's question, a door in a campaign, an interview's
/// round and its end, an investor's question and their offer, a founders' pitch. Plain English as the owner reads the
/// previews; the game's and the mod's words through their keys, so the checks lay out every state in every language.
/// </summary>
internal static partial class UiSamples
{
    /// <summary>The dialogue panel's states, in the previews' order.</summary>
    public static readonly string[] DialogStates =
    {
        "bank_start", "bank_loan", "bank_invest", "marketing", "marketing_list", "wholesale", "import", "import_unset", "furniture", "installation", "moving", "insurance",
        "insurance_unset", "recruitment", "driver", "hotel", "desk", "desk_full", "street", "street_said", "haggle", "other_mod", "long_names", "between_lines"
    };

    /// <summary>The states a game form stands in, its confirm saying what it pays and costs.</summary>
    public static readonly string[] DialogFormStates =
    {
        "bank_loan", "bank_invest", "marketing", "wholesale", "import", "import_unset", "furniture", "installation", "moving", "insurance", "insurance_unset", "recruitment", "driver"
    };

    /// <summary>A call's states on the call card: the bank's loan, a candidate's wage talk in the thread, a tow, the bank's investment and its funds' list open.</summary>
    public static readonly string[] CallStates = { "loan", "mood", "tow", "invest", "invest_list" };

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
        "court_plea", "court_tax", "court_charges", "legal_file", "fame_show", "office_door", "interview_round", "interview_over", "pitch_question", "pitch_offer",
        "venture_pitch", "venture_player", "scheme_offer", "scheme_fund"
    };

    private const string Bank = "Jensen Capital";
    private const string Bakery = "Harbour Lights Bakery";
    private const string Florist = "Bloom and Co";

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
            case "bank_invest":
            {
                // The game's investment form at the counter: the fund chosen, its risk, no sum typed yet, so Confirm says the
                // least the bank takes and the most the form lets in (the cash).
                Head(m, t("dialog_bank_larry", "Larry"), Bank);
                lines.Add(Them(t("dialog_bank_selector", "Hi there! How can I help you today?")));
                lines.Add(Me(t("dialog_bank_new_investment", "New investment")));
                m.Rows = InvestRows(t, true, false);
                m.RowCount = m.Rows.Length;
                choices.Add(Priced(t("common_confirm", "Confirm"),
                    Line(Fill(t("reallife_ui_dialog_cost_invest_range", "At least {least}, at most your cash, {cash}"), "least", "$1,000", "cash", "$48,210"), 0)));
                choices.Add(Choice(leave));
                break;
            }
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
                    choices.AddRange(LoanAnswers(t, false));
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
                    new UiFormRow { Kind = UiFormKind.Figure, Label = t("common_price_per_day", "Price per day"), Value = "$240", Strong = true },
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
                choices.Add(Priced(t("common_confirm", "Confirm"), Line(Fill(t("reallife_ui_dialog_cost_a_day", "{amount} a day while it runs"), "amount", "$240"), -1, -1)));
                choices.Add(Choice(leave));
                break;
            }
            case "wholesale":
            {
                // The fee's label is the one the game hangs under the business's, for the lone figure further down.
                Head(m, t("dialog_wholesale_store_npc_name", "Store Manager"), "Bronx Provisions Wholesale");
                lines.Add(Them(t("dialog_wholesale_store_start", "Hello! How can I help you?")));
                lines.Add(Me(t("dialog_wholesale_start_contract", "Start contract")));
                // The businesses as DialogForm.Contracts lists them: the game would refuse the greyed ones, and says why.
                m.Rows = new[]
                {
                    FormHeading(t("dialog_wholesale_store_delivery_contract_header", "Delivery contract")),
                    Pick(Bakery, Fill("{kind}, {district}", "kind", t("ba:businesstype_giftshop", "Gift Shop"), "district", "Midtown"), UiIcon.Store, true, null),
                    Pick("Brewed Beans", Fill("{kind}, {district}", "kind", t("ba:businesstype_coffeeshop", "Coffee Shop"), "district", "Midtown"), UiIcon.Store, false, null),
                    Pick(Florist, Fill("{kind}, {district}", "kind", t("ba:businesstype_florist", "Florist"), "district", "Garment District"), UiIcon.Store, false,
                        t("reallife_ui_dialog_has_contract", "Has a contract here")),
                    Pick("Speedy Bites", Fill("{kind}, {district}", "kind", t("ba:businesstype_fastfoodrestaurant", "Fast Food Restaurant"), "district", "Midtown"), UiIcon.Store, false,
                        t("reallife_ui_dialog_needs_storage", "Needs a storage shelf")),
                    FormBreak(),
                    FormFigure(t("dialog_delivery_fee", "Delivery fee"), "$120", false),
                    FormFigure(t("reallife_ui_dialog_first_delivery", "First delivery"), Fill(t("reallife_ui_dialog_contract_day", "Day {day}"), "day", "415"), false),
                    new UiFormRow { Kind = UiFormKind.Note, Label = t("reallife_ui_dialog_each_delivery_note", "Each delivery costs the fee plus the goods you set in BizMan.") },
                };
                choices.Add(Priced(t("dialog_accept_button", "Accept"),
                    Line(Fill(t("reallife_ui_dialog_cost_supplied", "Deliveries to {business} from day {day}"), "business", Bakery, "day", "415"), 1),
                    Line(Fill(t("reallife_ui_dialog_cost_each_delivery", "{fee} on each delivery, with the goods"), "fee", "$120"), -1, -1)));
                choices.Add(Choice(leave));
                break;
            }
            case "import":
            case "import_unset":
            {
                Head(m, t("dialog_import_npc_name", "Import Manager"), "Harbourside Freight");
                lines.Add(Them(t("dialog_import_start", "Hello and welcome! How can I help you?")));
                lines.Add(Me(t("dialog_import_create_new_partnership", "New partnership")));
                string agent = t("dialog_import_purchasing_agent", "Purchasing Agent"), ask = t("dialog_import_select_purchasing_agent", "Select purchasing agent");
                // The agents as DialogForm.Contracts lists them, each with the discount their skill gives; then what it needs and brings.
                bool picked = state == "import";
                string sub = t("reallife_ui_dialog_agent_sub", "Purchasing agent, {place}"), off = t("reallife_ui_dialog_agent_off", "{discount}% off");
                string skill = t("reallife_ui_dialog_agent_skill", "skill {skill}");
                var rows = new List<UiFormRow>
                {
                    new UiFormRow
                    {
                        Kind = UiFormKind.Heading, Label = t("dialog_import_partnership_header", "Import Partnership"),
                        Value = t("reallife_ui_dialog_agents_note", "On a shift, no partnership yet")
                    },
                    Agent("Priya Nair", Fill(sub, "place", "Harbour Lights HQ"), Fill(off, "discount", "20.5"), Fill(skill, "skill", "82"), picked),
                    Agent("Tom Eriksen", Fill(sub, "place", "Harbour Lights HQ"), Fill(off, "discount", "16"), Fill(skill, "skill", "64"), false),
                    Agent("Lena Adler", Fill(sub, "place", "Hudson Warehouse"), Fill(off, "discount", "11.8"), Fill(skill, "skill", "47"), false),
                    FormHeading(t("reallife_ui_dialog_import_needs", "What you need to start")),
                    new UiFormRow { Kind = UiFormKind.Check, Label = t("reallife_ui_dialog_import_warehouse", "A rented warehouse"), Value = "Hudson Warehouse, Hell's Kitchen", On = true },
                    new UiFormRow
                    {
                        Kind = UiFormKind.Check, Label = t("reallife_ui_dialog_import_agent_on_shift", "A purchasing agent on a shift"), On = true,
                        Value = Fill(t("reallife_ui_dialog_import_free", "{free} free of {all}"), "free", "3", "all", "5")
                    },
                    FormBreak(),
                };
                if (picked)
                {
                    rows.Add(FormFigure(t("reallife_ui_dialog_import_discount", "Discount on every import order"), "20.5%", false));
                }
                rows.Add(FormFigure(t("reallife_ui_dialog_import_orders", "Orders go out"), t("reallife_ui_dialog_import_mondays", "Mondays"), false));
                rows.Add(FormFigure(t("reallife_ui_dialog_import_products", "Products and amounts"), t("reallife_ui_dialog_import_in_bizman", "Set next, in BizMan"), true));
                m.Rows = rows.ToArray();
                choices.Add(picked
                    ? Priced(t("dialog_accept_button", "Accept"), Line(Fill(t("reallife_ui_dialog_cost_agent", "{buyer} buys for you from day {day}"), "buyer", "Priya Nair", "day", "415"), 1),
                        Line(Fill(t("reallife_ui_dialog_cost_discount", "{discount}% off every import order"), "discount", "20.5"), 1, 1))
                    : Priced(t("dialog_accept_button", "Accept"), Line(ask, 0)));   // the choice the confirm needs, in the game's own words for it
                choices.Add(Choice(leave));
                break;
            }
            case "furniture":
            {
                // The goods as the game lists them on the form, each with its steps and the button that takes it away.
                Head(m, t("dialog_furniture_store_npc_name", "Store Manager"), "Fiorello Furniture");
                lines.Add(Them(t("dialog_furniture_store_start", "Hi there, how may we help you?")));
                lines.Add(Me(t("furniture_delivery_dialog_order", "Order")));
                string slot = Fill(t("dialog_furniture_delivery_time_slot", "{day} (Day {number}) {hour}"), "day", t("common_tuesday", "Tuesday"), "number", "412", "hour", "10:00");
                m.Rows = new[]
                {
                    FormHeading(t("dialog_furniture_delivery_header", "Furniture Delivery")),
                    FormChoice(t("common_address", "Address"), Florist),
                    FormBreak(),
                    FormChoice(t("dialog_delivery_time", "Delivery time"), slot),
                    FormBreak(),
                    Goods(t, "ba:itemname_storageshelf", "Storage Shelf", "$980", "2"),
                    FormBreak(),
                    Goods(t, "ba:itemname_cashregister", "Cash Register", "$1,240", "4"),
                    FormBreak(),
                    Goods(t, "ba:itemname_ceilinglamppendant", "Ceiling Lamp (Pendant)", "$310", "6"),
                    FormBreak(),
                    new UiFormRow { Kind = UiFormKind.Action, Action = t("furniture_delivery_dialog_add_item", "Add item") },
                    FormFigure(t("dialog_delivery_fee", "Delivery fee"), "$250", false),
                    FormFigure(t("common_total_price", "Total price"), "$9,030", true),
                };
                choices.Add(Priced(t("furniture_delivery_dialog_order", "Order"), Line(Fill(t("reallife_ui_dialog_cost_items", "{count} items, {slot}"), "count", "12", "slot", slot), 0),
                    Line(Fill(t("reallife_ui_dialog_cost_on_delivery", "{amount}, paid on delivery"), "amount", "$9,030"), -1, -1)));
                choices.Add(Choice(leave));
                break;
            }
            case "installation":
            {
                const string firm = "Hudson Interiors";
                Head(m, t("dialog_interior_installation_firm_npc_name", "Interior Installation Firm Agent"), firm);
                lines.Add(Them(Fill(t("dialog_interior_installation_firm_start", "Hi and welcome to {businessName}. How may I help you today?"), "businessName", firm)));
                lines.Add(Me(t("dialog_more_help_new_order", "New Order")));
                m.Rows = new[]
                {
                    FormHeading(t("dialog_interior_installation_firm_design_settings_header", "Design Settings")),
                    FormChoice(t("common_address", "Address"), Florist),
                    FormChoice(t("dialog_interior_installation_day", "Installation Day"), Fill(t("common_day_number", "Day {number}"), "number", "415")),
                    FormChoice(t("dialog_interior_installation_design", "Design"), "Bloom and Co, third layout"),
                    FormFigure(t("dialog_interior_installation_fee", "Installation Fee"), "$2,400", false),
                    FormFigure(t("dialog_interior_installation_layout_price", "Layout Price"), "$21,640", false),
                    FormFigure(t("common_total", "Total"), "$24,040", true),
                };
                choices.Add(Priced(t("common_confirm", "Confirm"), Line(Fill(t("reallife_ui_dialog_cost_refit", "{place} is refitted on day {day}"), "place", Florist, "day", "415"), 0),
                    Line(t("reallife_ui_dialog_cost_discontinued", "Discontinued items are left out"), -1),
                    Line(Fill(t("reallife_ui_dialog_cost_on_the_day", "{amount}, paid on that day"), "amount", "$24,040"), -1, -1)));
                choices.Add(Choice(leave));
                break;
            }
            case "moving":
            {
                const string to = "120 9th Avenue";
                Head(m, t("dialog_moving_company_npc_name", "Moving Service Agent"), "Manhattan Movers");
                lines.Add(Them(t("dialog_moving_service_start", "Hi and welcome to Manhattan Movers. How may I help you today?")));
                lines.Add(Me(t("dialog_more_help_new_order", "New Order")));
                m.Rows = new[]
                {
                    FormHeading(t("dialog_moving_service_settings_header", "Moving Settings")),
                    FormChoice(t("dialog_moving_service_origin_address", "From"), Bakery),
                    FormChoice(t("dialog_moving_service_destination_address", "Destination"), to),
                    FormChoice(t("dialog_moving_service_day", "Moving Day"), Fill(t("common_day_number", "Day {number}"), "number", "414")),
                    new UiFormRow { Kind = UiFormKind.Switch, Label = t("transfer_bizman_settings", "Transfer BizMan Settings"), On = true },
                    FormFigure(t("dialog_moving_service_fee", "Moving Fee"), "$900", false),
                    FormFigure(t("dialog_moving_service_fee_per_item", "Fee per item"), "$15", false),
                    FormFigure(t("common_total", "Total"), "$1,845", true),
                };
                choices.Add(Priced(t("common_confirm", "Confirm"), Line(Fill(t("reallife_ui_dialog_cost_move", "{from} moves to {to} on day {day}"), "from", Bakery, "to", to, "day", "414"), 0),
                    Line(Fill(t("reallife_ui_dialog_cost_on_the_day", "{amount}, paid on that day"), "amount", "$1,845"), -1, -1)));
                choices.Add(Choice(leave));
                break;
            }
            case "insurance":
            case "insurance_unset":
            {
                // Before a manager is chosen the game has not written their skill: the row says so, never blank.
                Head(m, t("dialog_health_insurance_manager_npc_name", "Health Insurance Manager"), "Empire Mutual Health");
                lines.Add(Them(t("dialog_health_insurance_manager_start", "Hello. How can we help you?")));
                lines.Add(Me(t("dialog_health_insurance_manager_new_partnership", "Start new partnership")));
                string manager = t("dialog_health_insurance_partnership_hr_manager", "HR Manager"), plan = t("dialog_health_insurance_partnership_plan_type", "Plan Type");
                string skill = t("dialog_health_insurance_partnership_employee_skill", "HR Manager skill"), ask = t("dialog_health_insurance_manager_select_hr_manager", "Select HR Manager");
                bool chosen = state == "insurance";
                m.Rows = new[]
                {
                    FormHeading(t("dialog_health_insurance_partnership_header", "Health Insurance Partnership")),
                    chosen ? FormChoice(manager, "Mara Quinn") : FormUnchosen(manager, ask),
                    chosen ? FormFigure(skill, "74%", false) : new UiFormRow { Kind = UiFormKind.Figure, Label = ContactsRules.Label(skill), Hint = t("reallife_ui_dialog_figure_unset", "Not set yet") },
                    chosen ? FormChoice(plan, t("healthinsuranceplantype_silver", "Silver")) : FormUnchosen(plan, t("dialog_health_insurance_manager_select_plan_type", "Select Plan Type")),
                };
                choices.Add(Priced(t("dialog_accept_button", "Accept"), chosen ? Line(t("reallife_ui_dialog_cost_insurer_next", "The insurer names its price next"), 0) : Line(ask, 0)));
                choices.Add(Choice(leave));
                break;
            }
            case "recruitment":
            {
                const string agency = "Talent Row Recruitment";
                Head(m, t("dialog_recruitment_agency_npc_name", "Recruitment Agent"), agency);
                lines.Add(Them(Fill(t("dialog_recruitment_agency_start", "Hello and welcome to {businessName}. What type of employee are you looking to hire?"), "businessName", agency)));
                lines.Add(Me(t("dialog_recruitment_agency_start_new_campaign", "New Campaign")));
                m.Rows = new[]
                {
                    FormHeading(t("dialog_recruitment_agency_candidate_properties_header", "Candidate properties")),
                    FormChoice(t("common_business", "Business"), Florist),
                    FormChoice(t("dialog_recruitment_agency_primary_skills", "Primary Skill"), t("ba:skill_customerservice", "Customer Service")),
                    FormBreak(),
                    FormHeading(t("dialog_recruitment_agency_schedule_types", "Schedule Types")),
                    new UiFormRow { Kind = UiFormKind.Switch, Label = t("ba:jobdemand_parttime", "Part-time"), On = true },
                    new UiFormRow { Kind = UiFormKind.Switch, Label = t("ba:jobdemand_fulltime", "Full-time"), On = true },
                    new UiFormRow { Kind = UiFormKind.Slider, Label = t("dialog_recruitment_agency_amount_of_candidates", "Amount of candidates"), Value = "5", Share = 4f / 9f },
                    new UiFormRow { Kind = UiFormKind.Slider, Label = t("dialog_recruitment_agency_days_to_deliver", "Days to deliver"), Value = Fill(t("common_days", "{value} days"), "value", "4"), Share = 0.5f },
                    FormBreak(),
                    FormFigure(t("common_total_price", "Total price"), "$1,000", true),
                };
                choices.Add(Priced(t("common_confirm", "Confirm"), Line(Fill(t("reallife_ui_dialog_cost_candidates", "Up to {count} candidates within {days} days"), "count", "5", "days", "4"), 1),
                    Line(Fill(t("reallife_ui_dialog_cost_now", "{amount} now"), "amount", "$1,000"), -1, -1)));
                choices.Add(Choice(leave));
                break;
            }
            case "driver":
            {
                // The contract's own words under its choice, the game's lone lines as notes, the price a day as the total.
                const string service = "Metro Chauffeurs";
                Head(m, t("dialog_private_driver_service_npc_name", "Private Driver Service Manager"), service);
                lines.Add(Them(Fill(t("dialog_private_driver_start", "Hello and welcome to {businessName}! How can we help you today?"), "businessName", service)));
                lines.Add(Me(t("dialog_private_driver_hire_driver", "Hire driver")));
                m.Rows = new[]
                {
                    FormChoice(t("dialog_private_driver_select", "Select driver"), t("ba:private_driver_type_advanced", "Advanced Driver")),
                    new UiFormRow { Kind = UiFormKind.Note, Label = t("ba:private_driver_type_advanced_description", "Can drive vehicles available at The Hamptons Axis/Manhattan Luxury Cars, and City Cars") },
                    new UiFormRow { Kind = UiFormKind.Note, Label = Fill(t("dialog_private_driver_max_cars", "Max Cars: {number}"), "number", "2") },
                    FormBreak(),
                    FormFigure(t("dialog_private_driver_cost_per_day", "Cost per day"), "$420", true),
                };
                choices.Add(Priced(t("common_confirm", "Confirm"), Line(Fill(t("reallife_ui_dialog_cost_a_day", "{amount} a day while it runs"), "amount", "$420"), -1, -1)));
                back = t("common_cancel", "Cancel");
                choices.Add(Choice(back));
                break;
            }
            case "hotel":
            {
                // A desk of the mod's own: what each answer pays under it, from the same figures its words use.
                Head(m, t("reallife_travel_desk_name", "Reception"), "The Lexington Hotel");
                lines.Add(Them(Fill(t("reallife_travel_desk_nights", "Nights: {nights}. That comes to {total}, about {night} a night. Checkout is at 11:00 on {day}."),
                    "nights", "7", "total", "$1,260", "night", "$180", "day", t("common_tuesday", "Tuesday"))));
                lines.Add(Me(Fill(t("reallife_travel_desk_book", "Book nights: {nights}"), "nights", "7")));
                lines.Add(Them(Fill(t("reallife_travel_desk_pay",
                        "You can pay the {total} now, or leave {deposit} as a deposit and settle the remaining {later} when you leave: that is five per cent more, and if you go early you only pay for the nights you used."),
                    "total", "$1,260", "deposit", "$180", "later", "$1,134")));
                choices.Add(Priced(t("reallife_travel_desk_pay_now", "Pay now"), Line(Fill(t("reallife_ui_dialog_cost_now", "{amount} now"), "amount", "$1,260"), -1, -1)));
                choices.Add(Priced(t("reallife_travel_desk_pay_deposit", "Deposit"),
                    Line(Fill(t("reallife_ui_dialog_cost_deposit", "{amount} now and {later} when you leave"), "amount", "$180", "later", "$1,134"), -1, -1)));
                choices.Add(Choice(leave));
                break;
            }
            case "desk":
            {
                Head(m, t("reallife-business", "Business desk"), Bakery);
                lines.Add(Them(Matter(t, t("reallife_biz_undercut_name", "A competitor undercuts you"), Undercut(t))));
                choices.Add(Priced(t("reallife_biz_desk_options", "What can I do?"), Line(Due(t, "412"), 0)));
                choices.Add(Choice(t("reallife_biz_desk_next", "Next matter")));
                choices.Add(Choice(leave));
                break;
            }
            case "desk_full":
            {
                Head(m, t("reallife-business", "Business desk"), Bakery);
                lines.Add(Them(Matter(t, t("reallife_biz_health_name", "Health inspection"),
                    Fill(t("reallife_biz_health_start", "The health inspector will call at {business} tomorrow at {time}. They look at one thing: how clean the place is. It is at {clean} of 100 now; {pass} is a pass."),
                        "business", Bakery, "time", "10:00", "clean", "64", "pass", "70"))));
                lines.Add(Me(t("reallife_biz_desk_next", "Next matter")));
                lines.Add(Them(Matter(t, t("reallife_biz_supplierlate_name", "A late delivery"),
                    Fill(t("reallife_biz_supplierlate_start", "The wholesaler for {business} says the next delivery will be a day late (a lorry off the road) unless you pay {amount} for an express run. Answer today."),
                        "business", Bakery, "amount", "$180"))));
                lines.Add(Me(t("reallife_biz_desk_next", "Next matter")));
                lines.Add(Them(Matter(t, t("reallife_biz_undercut_name", "A competitor undercuts you"), Undercut(t))));
                lines.Add(Me(t("reallife_biz_desk_options", "What can I do?")));
                lines.Add(Them(Fill(t("reallife_biz_desk_option", "Choice {number} of {count}: {text}"), "number", "1", "count", "3",
                    "text", Fill(t("reallife_biz_undercut_marketing", "A week of leaflets and local adverts: {amount}. It cancels the dip at once and brings a few extra customers in for three days."), "amount", "$450"))));
                // The option's price under its answer (BusinessDesk.OptionPage: Pays), the deadline under deciding later.
                choices.Add(Priced(t("reallife_biz_undercut_do_marketing", "Run a marketing push"),
                    Line(Fill(t("reallife_ui_dialog_cost_now", "{amount} now"), "amount", "$450"), -1, -1)));
                choices.Add(Choice(t("reallife_biz_desk_another", "Another way")));
                back = t("reallife_biz_desk_later", "Decide later");
                choices.Add(Priced(back, Line(Due(t, "412"), 0)));
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
            case "haggle":
            {
                // A health insurer's counter offer, the game's negotiation: their mood by their line, and in a word by their face.
                Head(m, t("dialog_health_insurance_manager_npc_name", "Health Insurance Manager"), "Northside Health");
                lines.Add(Them(t("dialog_health_insurance_negotiation_start", "Hello. Seems like you are not satisfied with the offer. What do you propose?")));
                lines.Add(Me(Fill(t("dialog_health_insurance_player_offer", "I propose {amount} per day per employee."), "amount", "$6")));
                UiMessageModel unsure = Them(t("dialog_negotiation_acceptable_1", "Oh, hmm, interesting."));
                unsure.Mood = DialogRules.MoodIcon(0);
                lines.Add(unsure);
                UiMessageModel counter = Them(Fill(t("dialog_negotiation_counter_offer", "We would like to propose {counterOffer} instead."), "counterOffer", "$8"));
                counter.Mood = DialogRules.MoodIcon(0);
                lines.Add(counter);
                m.Ring = ContactsRules.MoodRing(0);
                m.Status = t(DialogRules.MoodWord(0), "Unsure");
                m.StatusTone = m.Ring;
                choices.Add(Choice(t("dialog_accept_button", "Accept")));
                choices.Add(Choice(t("dialog_negotiate_button", "Negotiate")));
                back = t("dialog_decline_button", "Decline");
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
        m.RowCount = m.Rows != null ? m.Rows.Length : 0;
        m.Messages = lines.ToArray();
        m.MessageCount = lines.Count;
        // The answers' keys as DialogScreen sets them: F on the first, P on the next, Escape on the way out; the first chosen.
        int leaveAt = back != null && choices.Count > 0 && choices[choices.Count - 1].Name == DialogRules.OneLine(back) ? choices.Count - 1 : -1;
        m.Choices = choices.ToArray();
        m.Chosen = DialogRules.Keys(m.Choices, leaveAt, out int _);
        m.ChoicesLabel = m.Choices.Length > 0 ? t("reallife_ui_dialog_your_answer", "Your answer") : null;
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

    /// <summary>
    /// The bank's loan form as the kit lays it (DialogForm.Loan): the amount typed, the term chosen among the bank's own, a
    /// shorter and a longer, and under it the rate a year, the payment a day and the sum to repay, as LoanTalkRules works
    /// them out by the game's own formulas: $25,000 over the bank's two years of sixty days at 9% a year.
    /// </summary>
    public static UiFormRow[] LoanRows(UiWords t, bool heading)
    {
        var rows = new List<UiFormRow>(7);
        if (heading)
        {
            rows.Add(new UiFormRow { Kind = UiFormKind.Heading, Label = t("dialog_bank_loan_amount_header", "Loan amount") });   // the line's own question
        }
        string term = Fill(t("reallife_ui_dialog_loan_term_years", "{years} years ({days} days)"), "years", "2", "days", "120");
        string years = t("reallife_ui_dialog_loan_chip_years", "{years} years");
        rows.Add(new UiFormRow { Kind = UiFormKind.Amount, Label = t("dialog_bank_amount_to_loan", "Amount to loan"), Value = "25000", CanLess = true, CanMore = true });
        rows.Add(new UiFormRow
        {
            Kind = UiFormKind.Segments, Label = t("reallife_ui_dialog_loan_term", "Term"), Value = term, Chosen = 1,
            Options = new[] { t("reallife_ui_dialog_loan_chip_year", "1 year"), Fill(years, "years", "2"), Fill(years, "years", "3") }
        });
        rows.Add(new UiFormRow { Kind = UiFormKind.Break });
        rows.Add(new UiFormRow { Kind = UiFormKind.Figure, Label = t("reallife_ui_dialog_loan_rate", "Interest rate"), Value = Fill(t("reallife_ui_dialog_loan_rate_value", "{rate}% a year"), "rate", "9") });
        rows.Add(new UiFormRow { Kind = UiFormKind.Figure, Label = t("bank_dialog_interest_daily_payment", "Daily interest ({rate}%)").Replace("{rate}", "9"), Value = "$74" });
        rows.Add(new UiFormRow { Kind = UiFormKind.Figure, Label = t("econoview_loans_daily_payment", "Daily payment"), Value = "$171" });
        rows.Add(new UiFormRow { Kind = UiFormKind.Figure, Label = t("bank_dialog_total_daily_payment", "Total daily payment"), Value = "$245", Strong = true });
        rows.Add(new UiFormRow { Kind = UiFormKind.Figure, Label = t("reallife_ui_dialog_loan_total", "Total to repay"), Value = "$29,477", Strong = true });
        return rows.ToArray();
    }

    /// <summary>
    /// The loan line's answers before Leave: Confirm with what taking it pays and costs, then the bank's own: a point off
    /// the rate (greyed with the rating it needs, or to be had), and a credit line in its place.
    /// </summary>
    public static UiDecisionChoice[] LoanAnswers(UiWords t, bool lower)
    {
        string rate = t("reallife_ui_dialog_loan_rate_value", "{rate}% a year");
        UiConsequence point = lower
            ? new UiConsequence { Text = Fill(t("reallife_ui_dialog_loan_lower_yes", "One point off, to {rate}"), "rate", Fill(rate, "rate", "8")), Tone = 1, Change = -1 }
            : new UiConsequence { Text = Fill(t("reallife_ui_dialog_loan_lower_rating", "Needs a credit rating of {need}; yours is {score}"), "need", "700", "score", "684"), Tone = -1 };
        string line = Fill(t("reallife_ui_dialog_loan_line_cost", "{thing} at {rate} a year on what you use"), "thing", t("reallife_econ_kind_overdraft", "Overdraft"), "rate", "12%");
        return new[]
        {
            new UiDecisionChoice
            {
                Name = t("common_confirm", "Confirm"),
                Lines = new[]
                {
                    new UiConsequence { Text = Fill(t("reallife_ui_dialog_loan_cost_in", "{amount} in your account today"), "amount", "$25,000"), Tone = 1, Change = 1 },
                    new UiConsequence { Text = Fill(t("reallife_ui_dialog_loan_cost_out", "{payment} a day for {days} days"), "payment", "$245", "days", "121"), Tone = UiConsequence.Loss, Change = -1 }
                }
            },
            new UiDecisionChoice { Name = t("reallife_ui_dialog_loan_lower", "Ask for a lower rate"), Disabled = !lower, Lines = new[] { point } },
            new UiDecisionChoice { Name = t("reallife_ui_dialog_loan_line", "Ask about a credit line instead"), Lines = new[] { new UiConsequence { Text = line } } }
        };
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

    // An answer with what it pays and costs under it, as the dialog or the form's confirm tells it (DialogAnswers).
    private static UiDecisionChoice Priced(string words, params UiConsequence[] lines) => new UiDecisionChoice { Name = DialogRules.OneLine(words), Lines = lines };

    // A line under an answer: for the player (+1), against them (-1) or neither, and which way their money goes; money
    // paid out (against them, falling) in the red, as DialogChoices.Lines makes it.
    private static UiConsequence Line(string text, int tone, int change = 0) =>
        new UiConsequence { Text = text, Tone = tone < 0 && change < 0 ? UiConsequence.Loss : tone, Change = change };

    // A game form's rows as DialogForm reads them: its question, a choice made or still asking, a figure, a splitter.
    private static UiFormRow FormHeading(string words) => new UiFormRow { Kind = UiFormKind.Heading, Label = words };

    private static UiFormRow FormChoice(string label, string value) => new UiFormRow { Kind = UiFormKind.Choice, Label = ContactsRules.Label(label), Value = value };

    private static UiFormRow FormUnchosen(string label, string ask) => new UiFormRow { Kind = UiFormKind.Choice, Label = ContactsRules.Label(label), Hint = ask };

    private static UiFormRow FormFigure(string label, string value, bool total) => new UiFormRow { Kind = UiFormKind.Figure, Label = ContactsRules.Label(label), Value = value, Strong = total };

    private static UiFormRow FormBreak() => new UiFormRow { Kind = UiFormKind.Break };

    // A line of a list to choose from (DialogForm.Contracts): a business with its kind and district, greyed with why it cannot be.
    private static UiFormRow Pick(string name, string where, UiIcon icon, bool chosen, string reason) =>
        new UiFormRow { Kind = UiFormKind.Pick, Label = name, Hint = where, Icon = icon, On = chosen, Disabled = reason != null, Reason = reason };

    // ...an agent with their workplace, the discount their skill gives and the skill.
    private static UiFormRow Agent(string name, string where, string off, string skill, bool chosen) =>
        new UiFormRow { Kind = UiFormKind.Pick, Label = name, Hint = where, Side = off, SideNote = skill, On = chosen };

    // A delivery's line of goods: its name and price as the game writes them, its amount with its steps and the button that takes it away.
    private static UiFormRow Goods(UiWords t, string item, string english, string price, string amount) => new UiFormRow
    {
        Kind = UiFormKind.Amount, Label = DialogRules.OneLine(Fill(t("furniture_delivery_item", "{itemName} - {itemPrice}"), "itemName", t(item, english), "itemPrice", price)),
        Value = amount, CanLess = true, CanMore = true, Remove = true
    };

    // A matter as the desk tells it: its name and business, what happened, and when it must be decided.
    // A matter as the desk says it now (BusinessDesk.IssuePage): one paragraph; its deadline goes under the answers.
    private static string Matter(UiWords t, string name, string text) =>
        Fill(t("reallife_biz_desk_matter", "{name}, {business}. {text}"), "name", name, "business", Bakery, "text", text);

    private static string Due(UiWords t, string day) =>
        Fill(t("reallife_biz_desk_due", "Decide by day {day}, {time}. Today is day {today}."), "day", day, "time", "18:00", "today", "410");

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
            Name = "Midtown Savings Bank", Icon = UiIcon.Bank, Picture = BankLogo, Role = t("contactcategoryname_finance", "Finance"), Length = "1:12",
            Line = t("dialog_bank_loan_amount_header", "Loan amount"),   // the line's header: a form's line has no message of its own
            Rows = LoanRows(t, false),
            Choices = LoanAnswers(t, true),
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

    /// <summary>A call's state on the call card: the bank's loan, the wage talk, the tow, the bank's investment (its funds' list open).</summary>
    public static UiContactsModel CallSample(UiWords t, string which) =>
        which == "loan" ? LoanCall(t) : which == "tow" ? TowCall(t) : which == "invest" ? InvestCall(t, false) : which == "invest_list" ? InvestCall(t, true) : MoodCall(t);

    /// <summary>
    /// The bank's investment form as DialogForm reads the game's (BankInvestmentSettings): the fund, a hairline, its risk as
    /// the game words it, a hairline, the sum and the daily auto-invest typed as the game formats them.
    /// </summary>
    public static UiFormRow[] InvestRows(UiWords t, bool heading, bool typed)
    {
        var rows = new List<UiFormRow>(7);
        if (heading)
        {
            rows.Add(FormHeading(t("dialog_bank_investment_header", "Investment")));   // the line's own question
        }
        string amount = t("common_amount", "Amount");
        string risk = Fill(t("dialog_bank_investment_risk_info", "{risk} (between {min}% and {max}%)"), "risk", t("priority_medium", "Medium"), "min", "-6", "max", "14");
        rows.Add(FormChoice(t("common_investment_fund", "Investment fund"), "Franklin US"));
        rows.Add(FormBreak());
        rows.Add(FormFigure(t("common_risk", "Risk"), risk, false));
        rows.Add(FormBreak());
        rows.Add(new UiFormRow { Kind = UiFormKind.Text, Label = ContactsRules.Label(amount), Value = typed ? "5,000" : string.Empty, Hint = amount });
        rows.Add(new UiFormRow { Kind = UiFormKind.Text, Label = ContactsRules.Label(t("econoview_investments_autoinvest", "Daily auto-invest")), Value = typed ? "250" : string.Empty, Hint = amount });
        return rows.ToArray();
    }

    /// <summary>
    /// The bank's investment asked for by phone: the form under the line, Confirm saying what leaves the account today and
    /// each day after; with the list open, the bank's funds under their choice, the one chosen marked.
    /// </summary>
    public static UiContactsModel InvestCall(UiWords t, bool open)
    {
        UiContactsModel m = ContactsSample(t, "call_replies");
        m.Call = new UiCallModel
        {
            Name = "Vantander Bank", Icon = UiIcon.Bank, Role = t("contactcategoryname_finance", "Finance"), Length = "0:48",
            Line = t("dialog_bank_investment_header", "Investment"),   // the line's header: a form's line has no message of its own
            Rows = InvestRows(t, false, true),
            Choices = new[]
            {
                Priced(t("common_confirm", "Confirm"), Line(Fill(t("reallife_ui_dialog_cost_invest", "{amount} from your account today"), "amount", "$5,000"), -1, -1),
                    Line(Fill(t("reallife_ui_dialog_cost_invest_daily", "Then {amount} a day from your account"), "amount", "$250"), -1, -1))
            },
            Buttons = new[] { new UiActionModel { Label = t("dialog_hang_up_button", "Hang up"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Phone, Key = UiAction.Back } },
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_choose", "Choose") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
            },
            PromptCount = 3
        };
        m.Call.RowCount = m.Call.Rows.Length;
        if (open)
        {
            m.Call.Pick.Row = 0;
            m.Call.Pick.Options = new[] { "Franklin US", "Lace Global A", "HG China Bonds", "Euro Energy High" };
            m.Call.Pick.Count = m.Call.Pick.Options.Length;
            m.Call.Pick.Chosen = 0;
        }
        return m;
    }

    /// <summary>
    /// A tow asked for by phone: the tow company's two places as the game's options, each with its price in its words, the
    /// repair shop chosen for a car that will not start, and what Accept costs under it. The form's line says nothing of
    /// its own, so the card shows the form under the caller's name.
    /// </summary>
    public static UiContactsModel TowCall(UiWords t)
    {
        UiContactsModel m = ContactsSample(t, "call_replies");
        m.Call = new UiCallModel
        {
            Name = "NY AutoTow", Icon = UiIcon.Truck, Role = t("contactcategoryname_general", "General"), Length = "0:21",
            Rows = new[]
            {
                new UiFormRow { Kind = UiFormKind.Radio, Label = t("ba:towdestination_autorepairshop", "Transport to closest auto repair shop") + " ($500)", On = true },
                new UiFormRow { Kind = UiFormKind.Radio, Label = t("ba:towdestination_gasstation", "Transport to closest gas station") + " ($350)" },
            },
            Choices = new[] { Priced(t("dialog_accept_button", "Accept"), Line(Fill(t("reallife_ui_dialog_cost_now", "{amount} now"), "amount", "$500"), -1, -1)) },
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

    /// <summary>
    /// A candidate's wage talk in Contacts, their reply to the player's offer with the mood the game shows by it, and what
    /// the offer typed pays under Send Offer beside what they asked last.
    /// </summary>
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
        if (m.Talk.Choices.Length > 0)
        {
            m.Talk.Choices[0].Lines = new[]
            {
                Line(Fill(t("reallife_ui_dialog_cost_wage", "{wage} an hour and {bonus} on signing, if they agree"), "wage", "$18.50", "bonus", "$250"), -1, -1),
                Line(Fill(t("reallife_ui_dialog_cost_asked", "They asked for {wage} an hour and {bonus}"), "wage", "$19.50", "bonus", "$400"), 0)
            };
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
                    Name = fred, Hue = UiColour.HueOf(fred), More = Fill(t("reallife_ui_monologue_count", "{index} of {total}"), "index", "1", "total", "2"), Progress = 35,
                    Next = t("reallife_ui_monologue_next", "Next"), Skip = t("reallife_ui_monologue_skip", "Skip"),
                    Line = t("ba:messagetype_tutorial_quest_find_a_job_uncle_fred",
                        "Rise and shine, kiddo! You survived your first night in New York City, congratulations! I’m sure it helped that I paid yesterday’s rent too. But from here on out, no more handouts! Today you need to find a job and start to earn your own money!")
                };
            case "fred_long":
                return new UiMonologueModel
                {
                    Name = fred, Hue = UiColour.HueOf(fred), Next = t("common_close", "Close"),   // a line with no sound of its own: no bar
                    Line = t("ba:messagetype_tutorial_quest_casino_uncle_fred",
                        "Ah, much better. With any luck, your money will be earning some nice interest over time. And speaking of luck, this calls for a celebration! Why don't you head out to the Casino and see if you can make a quick buck...or lose a few. As long as your businesses are healthy, it’s all fun and games, right?")
                };
            case "rival":
                return new UiMonologueModel
                {
                    Picture = "sample:rival", Hue = 30f, Progress = 80, Next = t("common_close", "Close"),
                    Line = "Nice little shop you have there. Enjoy it while it lasts, because my stores open on your street next week."
                };
            default:
                return new UiMonologueModel
                {
                    Name = fred, Hue = UiColour.HueOf(fred), Progress = 60, Next = t("common_close", "Close"),
                    Line = t("ba:messagetype_tutorial_quest_get_some_food_uncle_fred",
                        "Very nice, you found the apartment! But it’s looking really empty in here. Let’s get you a few essentials to fill up the place. We’ll start with some food. You can't conquer the world on an empty stomach.")
                };
        }
    }

    // ------------------------------------------------------------------------------------------------- scenes

    /// <summary>The words the scenes' screen takes from the game and the mod.</summary>
    public static SceneWords SceneWordsOf(UiWords t) => new SceneWords
    {
        Move = t("reallife_ui_prompt_move", "Move"), Choose = t("reallife_ui_prompt_choose", "Choose"), Back = t("common_cancel", "Cancel"), Menu = t("input_key_menu", "Menu"),
        Present = t("reallife_ui_scene_present", "{count} present"), Answer = t("reallife_ui_dialog_your_answer", "Your answer")
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
                    Dock(p, t, prosecutor, judge, "Whitmore & Vale", t("reallife_justice_role_prosecutor", "For the prosecution"));
                    string aside = Fill(t("reallife_justice_court_aside", "Your side of the table, quietly: on {charge}, the case against you stands at about {percent} in 100 right now."),
                        "charge", t("reallife_justice_charge_reckless", "Dangerous driving"), "percent", "62");
                    p.Notes.Add(new SceneNote { Name = judge, Detail = t("reallife_justice_court_plea", "You have heard the charges. How do you plead?") + "\n\n" + aside });
                    Answer(p, 0, t("reallife_justice_plea_guilty", "Guilty"), t("reallife_justice_plea_guilty_hint", "No trial. Convicted on every charge that stands, with a quarter off the sentence."));
                    Costs(p, Fill(t("reallife_justice_cost_hours", "{hours} hours"), "hours", "24"), Cost(Fill(t("reallife_justice_cost_fine", "A fine of {amount}"), "amount", "$1,200"), UiConsequence.Loss),
                        Cost(Fill(t("reallife_justice_cost_community", "{hours} hours of community service"), "hours", "24"), -1));
                    Answer(p, 1, t("reallife_justice_plea_not_guilty", "Not guilty"),
                        Fill(t("reallife_justice_plea_not_guilty_hint", "A trial of {rounds} rounds. The case against you starts at {percent} in 100; your answers move it."), "rounds", "3", "percent", "62"));
                    Costs(p, null, Cost(Fill(t("reallife_justice_odds", "About {count} in 10 chance to be cleared"), "count", "4"), 0),
                        Cost(Fill(t("reallife_justice_cost_lost_fine", "If convicted, a fine of {amount}"), "amount", "$2,400"), UiConsequence.Loss),
                        Cost(Fill(t("reallife_justice_cost_lost_community", "If convicted, {hours} hours of community service"), "hours", "32"), -1));
                    Answer(p, 2, t("reallife_justice_plea_deal", "Take the deal"),
                        Fill(t("reallife_justice_deal_reduced", "You plead guilty to {to} instead of {from}."), "to", t("reallife_justice_charge_evading", "Evading the police"),
                            "from", t("reallife_justice_charge_reckless", "Dangerous driving")) + " " + t("reallife_justice_deal_tail", "Sentence cut by 40%, and no prison."));
                    Costs(p, null, Cost(Fill(t("reallife_justice_cost_fine", "A fine of {amount}"), "amount", "$720"), UiConsequence.Loss));
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
            case "court_tax":
            {
                // A tax case's plea: the city's tax office against the player, and the city's settlement in place of a deal.
                string judge = t("reallife_justice_judge_1", "Judge Whitlock"), inspector = t("reallife_justice_prosecutor_1", "Prosecutor Vance");
                string kicker = Fill(t("reallife_justice_court_subtitle", "Case {number}, day {day}"), "number", "121", "day", "433");
                p.Reset(t("reallife_justice_court_title", "Criminal Court"), kicker);
                Moment(p, kicker, UiIcon.Scales, inspector, judge, "Whitmore & Vale");
                Dock(p, t, inspector, judge, "Whitmore & Vale", t("reallife_justice_role_inspector", "For the city's tax office"));
                string aside = Fill(t("reallife_justice_court_aside", "Your side of the table, quietly: on {charge}, the case against you stands at about {percent} in 100 right now."),
                    "charge", t("reallife_justice_charge_tax_evasion", "Tax evasion"), "percent", "70");
                p.Notes.Add(new SceneNote { Name = judge, Detail = t("reallife_justice_court_plea", "You have heard the charges. How do you plead?") + "\n\n" + aside });
                Answer(p, 0, t("reallife_justice_plea_guilty", "Guilty"), t("reallife_justice_plea_guilty_hint", "No trial. Convicted on every charge that stands, with a quarter off the sentence."));
                Costs(p, null, Cost(Fill(t("reallife_justice_cost_fine", "A fine of {amount}"), "amount", "$4,800"), UiConsequence.Loss));
                Answer(p, 1, t("reallife_justice_plea_not_guilty", "Not guilty"),
                    Fill(t("reallife_justice_plea_not_guilty_hint", "A trial of {rounds} rounds. The case against you starts at {percent} in 100; your answers move it."), "rounds", "3", "percent", "70"));
                Costs(p, null, Cost(Fill(t("reallife_justice_odds", "About {count} in 10 chance to be cleared"), "count", "3"), 0),
                    Cost(Fill(t("reallife_justice_cost_lost_fine", "If convicted, a fine of {amount}"), "amount", "$9,600"), UiConsequence.Loss));
                Answer(p, 2, t("reallife_justice_plea_settle", "Settle with the city"), t("reallife_justice_plea_settle_hint", "No trial and no conviction. You pay the city what a lost trial would cost."));
                Costs(p, null, Cost(Fill(t("reallife_justice_settle_cost", "{amount} to the city"), "amount", "$9,600"), UiConsequence.Loss),
                    Cost(t("reallife_justice_settle_gain", "The charge is dropped; the inspector comes less often for a year"), 1));
                p.Locked = true;
                break;
            }
            case "legal_file":
            {
                string kicker = Fill(t("reallife_justice_file_subtitle", "Day {day}, {hour}:00"), "day", "410", "hour", "14");
                p.Reset(t("reallife_justice_file_title", "Legal file"), kicker);
                Moment(p, kicker, UiIcon.Briefcase);
                // The file as LegalFile.ShowMain tells it now: the case in words over a statement of its figures.
                string cases = Fill(t("reallife_justice_file_case", "CASE {number}: {charges}."), "number", "114", "charges", t("reallife_justice_charge_reckless", "Dangerous driving"))
                               + " " + Fill(t("reallife_justice_file_case_date", "Hearing: day {day}, 9:00 to 17:00, {place}."), "day", "412", "place", "the courthouse on Centre Street");
                p.Notes.Add(new SceneNote { Name = string.Empty, Detail = cases });
                p.Statement = new[]
                {
                    FileRow(Fill(t("reallife_justice_file_counsel", "Counsel: {name}"), "name", "Whitmore & Vale"), UiLedgerKind.Line),
                    FileRow(Fill(t("reallife_justice_file_debt_fine", "Court fine: {left} left, {perday} a day"), "left", "$1,850", "perday", "$50"), UiLedgerKind.Line),
                    FileRow(Fill(t("reallife_justice_file_owed", "Owed: {amount} in all."), "amount", "$1,850"), UiLedgerKind.Total),
                    new UiLedgerLine { Kind = UiLedgerKind.Gap },
                    FileRow(t("reallife_justice_file_probation_none", "Probation: none."), UiLedgerKind.Line),
                    FileRow(t("reallife_justice_file_licence_ok", "Driving licence: not suspended."), UiLedgerKind.Line)
                };
                p.StatementWords = cases;
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
                        Fill(t("reallife_ven_meet_take_expect", "They expect its recent daily profit to grow by {growth}% within {days} days."), "growth", "15", "days", "30"));
                    Costs(p, null, new UiConsequence { Text = Fill(t("reallife_ven_meet_take_in", "{amount} is paid to you now"), "amount", "$40,000"), Tone = 1, Change = 1 },
                        new UiConsequence
                        {
                            Text = Fill(t("reallife_ven_meet_take_share", "{share} of each day's profit of {business} goes to them"), "share", "12%", "business", Bakery),
                            Tone = UiConsequence.Loss, Change = -1
                        });
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
            case "venture_player":
            {
                // The player's pitch to a firm: the firm's question and the three answers the player may give, each said whole.
                const string firm = "Northgate Capital";
                p.Reset(Fill(t("reallife_vc_meet_title_player", "Pitch to {firm}"), "firm", firm),
                    Fill(t("reallife_vc_meet_player_open", "The partners of {firm} are listening."), "firm", firm) + "\n" + t("reallife_vc_pitch_revenue", "What does the firm make a month in recurring revenue?")
                    + "\n<size=80%>" + Fill(t("reallife_hire_meet_progress", "Question {number} of {of}"), "number", "1", "of", "3") + "</size>");
                Talk(p, firm, null);
                string[] keys = { "reallife_vc_pitch_revenue_true", "reallife_vc_pitch_revenue_stretch", "reallife_vc_pitch_revenue_bold" };
                string[] fallback = { "{amount}, as the books say.", "About {amount}, counting what is signed.", "{amount}, and climbing fast." };
                string[] sums = { "$38,000", "$49,400", "$76,000" };
                for (int i = 0; i < 3; i++)
                {
                    string words = Fill(t(keys[i], fallback[i]), "amount", sums[i]);
                    p.Entries.Add(new SceneEntry { Id = i, Name = Fill(t("reallife_vc_meet_answer", "Answer {number}"), "number", (i + 1).ToString()), Detail = words, Said = words, Enabled = true });
                }
                Answer(p, 20, t("reallife_vc_meet_leave", "End the meeting"), null);
                p.Locked = true;
                break;
            }
            case "scheme_offer":
            {
                // A scheme's page as SchemePanel shows it: what it is, the two sizes with what each brings and risks, and the way out.
                p.Reset(t("reallife_scheme_claim_title", "A loss that never happened"), Bakery);
                Moment(p, Bakery, UiIcon.Briefcase);
                p.Notes.Add(new SceneNote
                {
                    Name = string.Empty,
                    Detail = Fill(t("reallife_scheme_claim_body", "{business} is insured. A claim can be put in for a loss that never happened, and the insurer pays it as any other "
                                                                + "claim.\n\nIf it is looked into, what was paid is taken back with {share} on top, the insurer ends the cover and the "
                                                                + "file goes to court."), "business", Bakery, "share", "25%")
                });
                string hint = t("reallife_scheme_claim_hint", "Claim a loss of {amount}."), gain = t("reallife_scheme_claim_gain", "Up to {amount} from the insurer, less the excess");
                Answer(p, 0, t("reallife_scheme_size_small", "A small one"), Fill(hint, "amount", "$2,000"));
                Costs(p, null, Cost(Fill(gain, "amount", "$2,000"), 1), Cost(Fill(t("reallife_scheme_risk_small", "About {chance} in 100 are looked into, more with each one on file"), "chance", "25"), 0));
                Answer(p, 1, t("reallife_scheme_size_large", "A large one"), Fill(hint, "amount", "$8,000"));
                Costs(p, null, Cost(Fill(gain, "amount", "$8,000"), 1), Cost(t("reallife_scheme_risk_large", "A sum this size is always looked into"), -1));
                Answer(p, 2, t("reallife_scheme_leave", "Leave it"), t("reallife_scheme_leave_hint", "Nothing is done."));
                p.Locked = false;   // the player may always walk away from a scheme
                break;
            }
            case "scheme_fund":
            {
                // A rival's fund on offer: put the money in, have it looked at first, or turn it down.
                const string holding = "Luthor Everything Co.";
                p.Reset(t("reallife_scheme_fund_title", "A place in a fund"), holding);
                Moment(p, holding, UiIcon.Briefcase);
                p.Notes.Add(new SceneNote
                {
                    Name = string.Empty,
                    Detail = Fill(t("reallife_scheme_fund_body", "{rival} offers you a place in a fund of {holding}: put in {amount} and take out {back} after {days} days. The offer "
                                                               + "stands until day {day}.\n\nSome funds are what they say. Some are not, and their money is gone."),
                        "rival", "Bald Luthor", "holding", holding, "amount", "$12,000", "back", "$12,960", "days", "30", "day", "418")
                });
                Answer(p, 0, Fill(t("reallife_scheme_fund_put", "Put in {amount}"), "amount", "$12,000"), t("reallife_scheme_fund_put_hint", "The money is theirs until the fund pays."));
                Costs(p, null, Cost(Fill(t("reallife_scheme_fund_put_cost", "{amount} now"), "amount", "$12,000"), UiConsequence.Loss),
                    Cost(Fill(t("reallife_scheme_fund_put_gain", "{amount} back if the fund is what it says"), "amount", "$12,960"), 0));
                Answer(p, 1, t("reallife_scheme_fund_look", "Have it looked at first"), t("reallife_scheme_fund_look_hint", "Your counsel reads the fund's papers and says what it is."));
                Costs(p, null, Cost(Fill(t("reallife_scheme_fund_look_cost", "{amount} for the look"), "amount", "$360"), UiConsequence.Loss));
                Answer(p, 2, t("reallife_scheme_fund_down", "Turn it down"), t("reallife_scheme_fund_down_hint", "Nothing is paid and the offer is gone."));
                p.Locked = false;
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

    // The answer added last: the time it takes beside its name and what it costs under its meaning (SceneList.Add, the hearing's).
    private static void Costs(ScenePage p, string time, params UiConsequence[] lines)
    {
        SceneEntry e = p.Entries[p.Entries.Count - 1];
        e.Note = time;
        e.Lines = lines;
        p.Entries[p.Entries.Count - 1] = e;
    }

    // A line as JusticePanel.Lines makes it: money paid out (UiConsequence.Loss) red behind the falling arrow.
    private static UiConsequence Cost(string text, int tone) => new UiConsequence { Text = text, Tone = tone, Change = tone <= UiConsequence.Loss ? -1 : 0 };

    // The court as JusticePanel tells it: the case against the player, the judge, the player's lawyer, and the player in
    // the dock, each with who they are and their ring (SceneList.Roles).
    private static void Dock(ScenePage p, UiWords t, string against, string judge, string counsel, string againstRole)
    {
        p.Faces = new[] { against, judge, counsel, t("reallife_justice_court_you", "You") };
        p.Roles = new[] { againstRole, t("reallife_justice_role_judge", "Presiding"), t("reallife_justice_role_counsel", "Your lawyer"), t("reallife_justice_role_defendant", "The defendant") };
        p.Rings = new[] { UiRole.Danger, UiRole.Warn, UiRole.Ok, UiRole.Accent };
    }

    // A line of the Legal file as its statement's (LegalFile.Row): its label and figure where it has both.
    private static UiLedgerLine FileRow(string line, UiLedgerKind kind) =>
        DialogRules.Pair(line, out string label, out string value)
            ? new UiLedgerLine { Kind = kind, Label = label, Value = value }
            : new UiLedgerLine { Kind = UiLedgerKind.Line, Label = line };

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
