using System.Collections.Generic;
using RealLifeOverhaul.Ui;

/// <summary>
/// Contacts on the kit, each state as the game would hand it over, all in one world of fourteen contacts: everyone with
/// new messages and an employee open; a recruitment agency with its candidates, a rival and the logistics alerts with
/// their own actions; a candidate's offer to answer and the wage being negotiated; the tax office's notice; a search that
/// finds nobody; and a call as it starts, with its replies and with a long line; the offer again in French, the longest
/// of the mod's languages (the game's words in French, the mod's new words in English until the next batch, as a French
/// player sees them). A contact opened is read, as the game reads it. Plain English, as the owner reads the previews;
/// the labels the game or the mod already has come through their keys.
/// </summary>
internal static partial class UiSamples
{
    /// <summary>Every state of Contacts, in the previews' order.</summary>
    public static readonly string[] ContactsStates =
    {
        "all", "business", "rivals", "general", "offer", "wage", "tax", "empty", "call_start", "call_replies", "call_long", "offer_fr"
    };

    // The game's categories and the mod's groups this world's contacts stand in.
    private static readonly int[] SampleGroups = { 0, 1, 2, 3, 4, 5, 6, 101, 102 };
    private static readonly string[] SampleGroupWords = { "General", "Business", "Finance", "Furniture / Equipment", "Imports / Goods", "Employees", "Rivals", "Family", "Friends and people" };

    // Everyone, as the game lists them: the unread first, then the newest; each with the group it stands in.
    private static readonly int[] EveryoneGroups = { 5, 101, 5, 1, 5, 0, 1, 5, 6, 4, 3, 2, 2, 102 };

    /// <summary>Contacts in a state.</summary>
    public static UiContactsModel ContactsSample(UiWords t, string state)
    {
        UiWords w = state.EndsWith("_fr") ? ContactsFrench(t) : t;
        string st = state.EndsWith("_fr") ? state.Substring(0, state.Length - 3) : state;
        var m = new UiContactsModel { Title = w("reallife_ui_contacts_app", "Contacts") };
        int group = st == "business" ? 2 : st == "rivals" ? 7 : st == "general" ? 1 : st == "tax" ? 3 : 0;
        ContactsGroups(m, w, group);
        m.PickTitle = w("reallife_ui_contacts_pick_title", "Pick a contact");
        m.PickBody = w("reallife_ui_contacts_pick_body", "Their messages, actions and details show here.");
        m.List.SearchPlaceholder = w("reallife_ui_contacts_search", "Search people, shops, agencies");
        m.List.EmptyTitle = w("reallife_ui_contacts_none_title", "No contact matches");
        m.List.EmptyBody = w("reallife_ui_contacts_none_body", "Try another name, or clear the search.");
        UiMessageRow[] everyone = ContactsEveryoneRows(w);
        switch (st)
        {
            case "business":
                ContactsRows(m, ContactsInGroup(w, everyone, 1), 0);
                ContactsAgency(m, w);
                break;
            case "rivals":
                ContactsRows(m, ContactsInGroup(w, everyone, 6), 0);
                ContactsRival(m, w);
                break;
            case "general":
                ContactsRows(m, ContactsInGroup(w, everyone, 0), 0);
                ContactsLogistics(m, w);
                break;
            case "offer":
            case "wage":
                ContactsRows(m, everyone, 2);
                ContactsCandidate(m, w, st == "wage");
                break;
            case "tax":
                ContactsRows(m, ContactsInGroup(w, everyone, 2), 1);
                ContactsTaxOffice(m, w);
                break;
            case "empty":
                m.List.Query = "Zanzibar";
                m.List.Found = "0";
                m.List.EmptyAction = w("reallife_ui_parts_clear_search", "Clear search");
                ContactsRows(m, new UiMessageRow[0], -1);
                ContactsEmployee(m, w);   // the one open stays open while a search finds no one, as the game keeps it
                m.HasDetail = true;
                break;
            default:
                ContactsRows(m, everyone, 4);
                ContactsEmployee(m, w);
                break;
        }
        int unread = 5 - (st == "offer" || st == "wage" ? 2 : st == "business" ? 1 : 0);   // the one opened is read
        ContactsFoot(m, w, group > 0, unread);
        if (st.StartsWith("call"))
        {
            m.Call = ContactsCall(w, st);
        }
        bool call = m.HasDetail && System.Array.Exists(m.HeadActions, a => a.Arg == ContactsRoutes.CallArg);   // as the screen offers F only while the game shows its Call
        var prompts = new List<UiPrompt>
        {
            new UiPrompt { Action = UiAction.Move, Label = w("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = w("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.TabPrev, Label = w("reallife_ui_prompt_group", "Group") },
        };
        if (call)
        {
            prompts.Add(new UiPrompt { Action = UiAction.Alt, Label = w("phone_call_button", "Call") });
        }
        prompts.Add(new UiPrompt { Action = UiAction.Back, Label = w("reallife_ui_prompt_back", "Back") });
        m.Prompts = prompts.ToArray();
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>The row a state's list has chosen, for the previews' focus; 0 where none is.</summary>
    public static int ContactsChosen(string state)
    {
        UiContactsModel m = ContactsSample((key, english) => english, state);
        for (int i = 0; i < m.List.RowCount; i++)
        {
            if (m.List.Rows[i].Selected)
            {
                return i;
            }
        }
        return 0;
    }

    private static void ContactsGroups(UiContactsModel m, UiWords t, int shown)
    {
        var nav = new UiNavEntry[SampleGroups.Length + 1];
        nav[0] = new UiNavEntry { Icon = UiIcon.AddressBook, Tint = UiIconTints.Of(UiIcon.AddressBook), Label = t("reallife_ui_contacts_all", "All contacts"), Count = EveryoneGroups.Length.ToString() };
        for (int i = 0; i < SampleGroups.Length; i++)
        {
            UiIcon icon = ContactsRules.GroupIcon(SampleGroups[i]);
            int count = 0;
            foreach (int g in EveryoneGroups)
            {
                count += g == SampleGroups[i] ? 1 : 0;
            }
            nav[i + 1] = new UiNavEntry { Icon = icon, Tint = UiIconTints.Of(icon), Label = t(ContactsRules.GroupKey(SampleGroups[i]), SampleGroupWords[i]), Count = count.ToString() };
        }
        m.Groups = nav;
        m.GroupShown = shown;
    }

    private static void ContactsFoot(UiContactsModel m, UiWords t, bool oneGroup, int unread)
    {
        m.Foot.Actions = new[]
        {
            new UiActionModel { Label = t("contacts_mark_all_as_read_button", "Mark all as read"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Check, Key = UiAction.Count, Arg = ContactsRoutes.MarkReadArg },
            new UiActionModel { Label = t("contacts_delete_all_button", "Delete all"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Trash, Key = UiAction.Count, Arg = ContactsRoutes.DeleteAllArg,
                Disabled = !oneGroup },
        };
        m.Foot.Player = "Alex Carter";
        m.Foot.PlayerHue = 40f;
        m.Foot.PlayerLine = t("reallife_ui_contacts_summary", "{count} contacts, {unread} unread").Replace("{count}", EveryoneGroups.Length.ToString()).Replace("{unread}", unread.ToString());
    }

    // The list with one row chosen: that one opened, so read, as the game marks a contact's messages read as it opens.
    private static void ContactsRows(UiContactsModel m, UiMessageRow[] rows, int selected)
    {
        if (selected >= 0 && selected < rows.Length)
        {
            rows[selected].Selected = true;
            rows[selected].Unread = 0;
        }
        m.List.Rows = rows;
        m.List.RowCount = rows.Length;
        m.HasDetail = selected >= 0;
    }

    // One group's rows of everyone, under the bands the game's order gives them.
    private static UiMessageRow[] ContactsInGroup(UiWords t, UiMessageRow[] everyone, int group)
    {
        var rows = new List<UiMessageRow>();
        for (int i = 0; i < everyone.Length; i++)
        {
            if (EveryoneGroups[i] == group)
            {
                rows.Add(everyone[i]);
            }
        }
        bool sawRead = false;
        for (int i = 0; i < rows.Count; i++)
        {
            UiMessageRow row = rows[i];
            row.Section = i == 0 && row.Unread > 0 ? t("reallife_ui_contacts_new", "New messages") : row.Unread == 0 && !sawRead && i > 0 ? t("reallife_ui_contacts_earlier", "Earlier") : null;
            sawRead |= row.Unread == 0;
            rows[i] = row;
        }
        return rows.ToArray();
    }

    private static UiMessageRow[] ContactsEveryoneRows(UiWords t)
    {
        string you = t("reallife_ui_contacts_you", "You: {text}");
        return new[]
        {
            new UiMessageRow { Name = "Luis Romero", Hue = 30f, Preview = "Van needs fuel before the afternoon run, and the left tyre looks low", Time = "10:02", Unread = 1, Section = t("reallife_ui_contacts_new", "New messages") },
            new UiMessageRow { Name = "Uncle Fred", Hue = 280f, Preview = "Proud of you, kid. Dinner on Sunday? Your aunt is cooking", Time = "09:02", Unread = 1 },
            new UiMessageRow { Name = "Nadia Haddad", Hue = 200f, Preview = "Thank you for considering me for the open position. Let's discuss the wages for this job.", Time = "08:58", Unread = 2 },
            new UiMessageRow { Name = "Anderson Recruitment Corp.", Icon = UiIcon.IdBadge, Preview = "Three candidates for the barista job are in My Employees", Time = "08:52", Unread = 1 },
            new UiMessageRow { Name = "Maya Okafor", Hue = 24f, Preview = you.Replace("{text}", "Let me look at the rota tonight."), Time = "09:20", Section = t("reallife_ui_contacts_earlier", "Earlier") },
            new UiMessageRow { Name = "Logistics Alerts", Icon = UiIcon.Truck, Preview = "Your Tuesday delivery left the warehouse", Time = "07:30" },
            new UiMessageRow { Name = "McCain's eMarketing", Icon = UiIcon.Megaphone, Preview = "Click-through on your last campaign was up 18%", Time = "Day 411" },
            new UiMessageRow { Name = "Hana Sato", Hue = 330f, Preview = "Requesting a better desk chair. My back is not happy", Time = "Day 411" },
            new UiMessageRow { Name = "Ingrid Schneider", Hue = 350f, Preview = "You won't keep Midtown to yourself for long", Time = "Day 410" },
            new UiMessageRow { Name = "Harbor Line Wholesale", Icon = UiIcon.Package, Preview = "Container of coffee beans landed. Your order ships tomorrow", Time = "Day 409" },
            new UiMessageRow { Name = "Lumen and Oak Interiors", Icon = UiIcon.Sofa, Preview = "The walnut counters you asked about are back in stock", Time = "Day 408" },
            new UiMessageRow { Name = "Midtown Savings Bank", Icon = UiIcon.Bank, Preview = "Your loan's daily payment went through", Time = "Day 406" },
            new UiMessageRow { Name = "Internal Revenue Service", Icon = UiIcon.Bank, Preview = "Notice of outstanding tax payment", Time = "Day 400" },
            new UiMessageRow { Name = "Tom Becker", Hue = 210f, Preview = "Can do Tuesday evenings if Maya takes Sunday", Time = "Day 399" },
        };
    }

    private static void ContactsHead(UiContactsModel m, UiWords t, string name, float hue, UiIcon icon, UiRole ring, string role, string employer, bool map, bool call)
    {
        m.Head = new UiHeadModel { Name = name, Hue = hue, Icon = icon, Ring = ring, Role = role, Employer = employer };
        var actions = new List<UiActionModel>(2);
        if (map)
        {
            actions.Add(new UiActionModel { Label = t("phone_view_on_map_button", "View on map"), Kind = UiButtonKind.Secondary, Icon = UiIcon.MapPin, Key = UiAction.Option, Arg = ContactsRoutes.MapArg });
        }
        if (call)
        {
            actions.Add(new UiActionModel { Label = t("phone_call_button", "Call"), Kind = UiButtonKind.Primary, Icon = UiIcon.Phone, Key = UiAction.Alt, Arg = ContactsRoutes.CallArg });
        }
        m.HeadActions = actions.ToArray();
        m.ActionsTitle = t("reallife_ui_contacts_requests", "Actions");
        m.ProfileTitle = t("reallife_ui_contacts_profile", "Profile");
        m.DetailsTitle = t("reallife_ui_contacts_details", "Details");
        m.Talk = new UiConversationModel
        {
            Name = name, ThreadStart = t("reallife_ui_contacts_start", "Start of your messages with {name}").Replace("{name}", name),
            EmptyTitle = t("reallife_ui_contacts_quiet_title", "Quiet line"), EmptyBody = t("reallife_ui_contacts_quiet_body", "No messages with them yet.")
        };
    }

    private static UiContactAction ClearChat(UiWords t) => new UiContactAction { Label = t("contacts_header_clear_chat_button", "Clear chat"), Icon = UiIcon.Trash, Clears = true };

    private static string HourlyWage(UiWords t) => ContactsRules.Label(t("dialog_candidate_salary_offer_input_hourly_wage", "Hourly wage: "));

    // An employee: their face in the ring of their mood, their skill and satisfaction graded, the wage they are paid.
    private static void ContactsEmployee(UiContactsModel m, UiWords t)
    {
        ContactsHead(m, t, "Maya Okafor", 24f, UiIcon.None, UiTokens.Grade(64), t("ba:skill_customerservice", "Customer Service"), "Bloom Cafe", false, true);
        m.Talk.Messages = new[]
        {
            new UiMessageModel { From = UiFrom.Them, Text = "Morning. The cafe is busy today.", Time = "07:58" },
            new UiMessageModel { From = UiFrom.Them, Text = "Could I drop the Sunday shift? I've picked up a class on weekends.", Time = "09:14" },
            new UiMessageModel { From = UiFrom.Them, Text = "Happy to cover Tuesday evenings instead.", Time = "09:15" },
            new UiMessageModel { From = UiFrom.Me, Text = "Let me look at the rota tonight.", Time = "09:20" },
        };
        m.Talk.MessageCount = 4;
        m.Actions = new[]
        {
            new UiContactAction { Label = t("contacts_manage_employee_button", "Manage employee"), Icon = UiIcon.IdBadge },
            new UiContactAction { Label = t("myemployees_manage_schedule", "Manage schedule"), Icon = UiIcon.Calendar }, ClearChat(t),
        };
        m.ProfileLabels = new[] { t("myemployees_jobtype", "Skill"), t("myemployees_satisfaction", "Satisfaction") };
        m.ProfileValues = new[] { 82, 64 };
        m.Details = new[]
        {
            new UiPairModel { Key = t("contacts_schedule_demands", "Schedule demands"), Value = "No weekends" },
            new UiPairModel { Key = HourlyWage(t), Value = "$22.40" },
        };
    }

    // A recruitment agency: its picture on a tile, its candidates to see, its hours and address, View on map and Call.
    private static void ContactsAgency(UiContactsModel m, UiWords t)
    {
        ContactsHead(m, t, "Anderson Recruitment Corp.", 0f, UiIcon.IdBadge, UiRole.None, "Recruitment Agency", null, true, true);
        m.Talk.Messages = new[]
        {
            new UiMessageModel { From = UiFrom.Them, Text = "Thank you for choosing Anderson Recruitment. Your barista campaign runs for seven days.", Time = "Day 410" },
            new UiMessageModel { From = UiFrom.Them, Text = "Three candidates for the barista job are in My Employees.", Time = "08:52" },
        };
        m.Talk.MessageCount = 2;
        m.Actions = new[] { new UiContactAction { Label = t("contacts_candidates_button", "Candidates"), Icon = UiIcon.People }, ClearChat(t) };
        m.Details = new[]
        {
            new UiPairModel { Key = t("contacts_today_opening_hours", "Today opening hours"), Value = "09:00 - 17:00" },
            new UiPairModel { Key = t("common_address", "Address"), Value = "12 Lexington Avenue" },
        };
    }

    // A rival: their face, what they said to the player, the way to their page in Rivals.
    private static void ContactsRival(UiContactsModel m, UiWords t)
    {
        ContactsHead(m, t, "Ingrid Schneider", 350f, UiIcon.None, UiRole.None, "Schneider Group", null, false, false);
        m.Talk.Messages = new[]
        {
            new UiMessageModel { From = UiFrom.Them, Text = "So you're the new name on Fifth Avenue. Enjoy it while the rent is cheap.", Time = "Day 405" },
            new UiMessageModel { From = UiFrom.Them, Text = "You won't keep Midtown to yourself for long.", Time = "Day 410" },
        };
        m.Talk.MessageCount = 2;
        m.Actions = new[] { new UiContactAction { Label = t("contacts_rival_button", "View Rival"), Icon = UiIcon.Flag }, ClearChat(t) };
    }

    // The logistics alerts: the deliveries' news and the way to the logistics managers in BizMan.
    private static void ContactsLogistics(UiContactsModel m, UiWords t)
    {
        ContactsHead(m, t, "Logistics Alerts", 0f, UiIcon.Truck, UiRole.None, "Deliveries", null, false, false);
        m.Talk.Messages = new[]
        {
            new UiMessageModel { From = UiFrom.Them, Text = "The truck for Bloom Cafe is loaded: 40 bags of coffee beans, 120 paper cups.", Time = "06:45" },
            new UiMessageModel { From = UiFrom.Them, Text = "Your Tuesday delivery left the warehouse.", Time = "07:30" },
        };
        m.Talk.MessageCount = 2;
        m.Actions = new[] { new UiContactAction { Label = t("contacts_manage_delivery_button", "Manage delivery"), Icon = UiIcon.Truck }, ClearChat(t) };
    }

    // A candidate who wants to talk wages: their offer's three answers, accept carrying its terms; or the negotiation itself,
    // their question with the wage and the signing bonus in the form slot.
    private static void ContactsCandidate(UiContactsModel m, UiWords t, bool negotiating)
    {
        ContactsHead(m, t, "Nadia Haddad", 200f, UiIcon.None, negotiating ? UiRole.Warn : UiRole.None, t("ba:skill_customerservice", "Customer Service"), t("common_unassigned", "Unassigned"), false, !negotiating);
        string terms = t("dialog_candidate_salary_offer_hourly_wage", "Hourly wage: <b>{price}</b>").Replace("<b>", string.Empty).Replace("</b>", string.Empty).Replace("{price}", "$19.50");
        var messages = new List<UiMessageModel>
        {
            new UiMessageModel { From = UiFrom.Them, Text = "Hello. I saw the opening for a barista at Bloom Cafe and I'd love to join the team.", Time = "Day 411" },
            new UiMessageModel { From = UiFrom.Them, Text = t("contacts_salary_negotiation_start", "Thank you for considering me for the open position! Let's discuss the wages for this job."), Time = "08:58" },
        };
        if (negotiating)
        {
            messages.Add(new UiMessageModel
            {
                From = UiFrom.Them, Time = "09:03",
                Text = t("dialog_health_insurance_negotiation_start", "Hello. Seems like you are not satisfied with the offer. What do you propose?")
            });
            m.Talk.Fields = new[]
            {
                new UiFormField { Kind = UiFormKind.Amount, Label = HourlyWage(t), Value = "$18.50", CanLess = true, CanMore = true },
                new UiFormField { Kind = UiFormKind.Amount, Label = ContactsRules.Label(t("dialog_candidate_salary_offer_input_signing_bonus", "Signing bonus: ")), Value = "$250.00", CanLess = true, CanMore = true },
            };
            m.Talk.FieldCount = 2;
            m.Talk.Choices = new[] { new UiDecisionChoice { Name = t("dialog_send_offer_button", "Send Offer") }, new UiDecisionChoice { Name = t("dialog_decline_button", "Decline") } };
        }
        else
        {
            m.Talk.Choices = new[]
            {
                new UiDecisionChoice { Name = t("dialog_accept_button", "Accept"), Lines = new[] { new UiConsequence { Text = terms } } },
                new UiDecisionChoice { Name = t("dialog_negotiate_button", "Negotiate") }, new UiDecisionChoice { Name = t("dialog_decline_button", "Decline") },
            };
        }
        m.Talk.Messages = messages.ToArray();
        m.Talk.MessageCount = messages.Count;
        m.Actions = new[] { ClearChat(t) };
        m.ProfileLabels = new[] { t("myemployees_jobtype", "Skill") };
        m.ProfileValues = new[] { 71 };
        m.Details = new[] { new UiPairModel { Key = t("contacts_schedule_demands", "Schedule demands"), Value = "Full time, no mornings" } };
    }

    // The tax office: its notice with the return as a ledger in the bubble, as the game's own table lays it out.
    private static void ContactsTaxOffice(UiContactsModel m, UiWords t)
    {
        ContactsHead(m, t, "Internal Revenue Service", 0f, UiIcon.Bank, UiRole.None, "Government", null, true, false);
        string L(string key, string english) => ContactsRules.Label(t(key, english));
        var ledger = new[]
        {
            new UiLedgerLine { Kind = UiLedgerKind.Heading, Label = L("contacts_taxes_income", "Income:") },
            new UiLedgerLine { Label = "Bloom Cafe", Value = "$84,210.00" }, new UiLedgerLine { Label = "Uptown Bakery", Value = "$41,980.50" },
            new UiLedgerLine { Kind = UiLedgerKind.Heading, Label = L("contacts_taxes_tax_deductions", "Tax Deductions:") },
            new UiLedgerLine { Label = "Rent", Value = "$18,400.00" }, new UiLedgerLine { Label = "Wages", Value = "$52,120.00" },
            new UiLedgerLine { Kind = UiLedgerKind.Subtotal, Label = L("common_subtotal", "Subtotal"), Value = "$70,520.00" },
            new UiLedgerLine { Kind = UiLedgerKind.Heading, Label = L("contacts_taxes_grand_total", "Grand Total:") },
            new UiLedgerLine { Label = L("contacts_taxes_taxable_income", "Taxable Income:"), Value = "$55,670.50" },
            new UiLedgerLine { Label = L("contacts_taxes_tax_rate", "Tax Rate:"), Value = "21%" },
            new UiLedgerLine { Kind = UiLedgerKind.Total, Label = L("contacts_taxes_total_tax_due", "Total Tax Due:"), Value = "$11,690.81" },
        };
        m.Talk.Messages = new[]
        {
            new UiMessageModel
            {
                From = UiFrom.Them, Time = "Day 400", Table = ledger,
                Text = "Notice of outstanding tax payment\n\nDear Sir or Madam:\n\nThe tax assessments for the period Day 341-400 is now available.",
                TableFoot = "Please pay any outstanding taxes by Day 420 at your local IRS office:\n18 Fulton Street\n\nSincerely,\nInternal Revenue Service"
            },
        };
        m.Talk.MessageCount = 1;
        m.Actions = new[] { new UiContactAction { Label = t("contacts_tax_button", "View taxes"), Icon = UiIcon.Receipt } };
        m.Details = new[] { new UiPairModel { Key = t("common_address", "Address"), Value = "18 Fulton Street" } };
    }

    // A call: as it starts (their greeting, the next line coming), with its replies, and with a long line.
    private static UiCallModel ContactsCall(UiWords t, string state)
    {
        var call = new UiCallModel
        {
            Name = "Uncle Fred", Hue = 280f, Ring = UiRole.Ok, Role = t("contactcategoryname_101", "Family"), Length = state == "call_start" ? "0:04" : state == "call_long" ? "2:41" : "0:37",
            Buttons = new[] { new UiActionModel { Label = t("dialog_hang_up_button", "Hang up"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Phone, Key = UiAction.Back } },
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_choose", "Choose") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
            },
            PromptCount = 3
        };
        switch (state)
        {
            case "call_start":
                call.Line = "Well, look who it is. Give me a second, I'm just parking the car.";
                call.Waiting = true;
                break;
            case "call_long":
                call.Line = "When I opened my first shop on Canal Street I had one oven, one helper and a landlord who wanted the rent before the bread was out. "
                            + "I learned two things that year. Keep three months of wages in the bank, and never sign a lease longer than you can see. "
                            + "You are doing better than I did, kid, but the rent in Midtown will go up again in spring. Have you thought about buying the building?";
                call.Choices = new[] { new UiDecisionChoice { Name = "Tell me more about buying" }, new UiDecisionChoice { Name = "Thanks, I'll think about it" } };
                break;
            default:
                call.Line = "Your aunt asks if you're coming on Sunday. She's making the lasagne again.";
                call.Choices = new[] { new UiDecisionChoice { Name = "I'll be there at six" }, new UiDecisionChoice { Name = "Not this week, sorry" } };
                break;
        }
        return call;
    }

    // The game's words in French over the words the checks and previews were given, as a French player reads Contacts: the
    // game's own keys in French, the mod's new keys in English until the next translation batch.
    private static UiWords ContactsFrench(UiWords t)
    {
        var fr = new Dictionary<string, string>
        {
            { "phone_call_button", "Appeler" }, { "phone_view_on_map_button", "Voir sur la carte" }, { "contacts_mark_all_as_read_button", "Tout marquer comme lu" },
            { "contacts_delete_all_button", "Tout supprimer" }, { "contacts_header_clear_chat_button", "Effacer la discussion" }, { "contacts_schedule_demands", "Exigences de planning" },
            { "myemployees_jobtype", "Compétence" }, { "myemployees_satisfaction", "Satisfaction" },
            { "common_unassigned", "Non affecté" }, { "contactcategoryname_general", "Général" }, { "contactcategoryname_business", "Entreprise" },
            { "contactcategoryname_finance", "Finance" }, { "contactcategoryname_furnitureandequipment", "Mobilier / Équipement" }, { "contactcategoryname_importsandgoods", "Importations / Marchandises" },
            { "contactcategoryname_employees", "Employés" }, { "contactcategoryname_rivals", "Rivaux" }, { "dialog_accept_button", "Accepter" }, { "dialog_decline_button", "Refuser" },
            { "dialog_negotiate_button", "Négocier" }, { "dialog_candidate_salary_offer_hourly_wage", "Salaire horaire : <b>{price}</b>" }, { "ba:skill_customerservice", "Service client" },
            { "contacts_salary_negotiation_start", "Merci de me considérer pour le poste ! Discutons du salaire pour cet emploi." },
        };
        return (key, english) => fr.TryGetValue(key, out string words) ? words : t(key, english);
    }
}
