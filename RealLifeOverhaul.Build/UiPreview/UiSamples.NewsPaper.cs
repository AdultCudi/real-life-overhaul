using System;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    // The Newsstand as the live fill writes it: a row for each paper, then the Pass.
    public static NewsModel NewsStand(UiWords t, string state)
    {
        var m = new NewsModel
        {
            App = NewsApp.Stand,
            Icon = UiIcon.Newspaper,
            Title = t("reallife_ledger_online_app", "Newsstand"),
            Across = true,
            Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.Newspaper, Label = t("reallife_ledger_online_tab_titles", "Titles") },
                new UiNavEntry { Icon = UiIcon.Books, Label = t("reallife_ledger_online_tab_reading", "Reading") }
            },
            Prompts = NewsPrompts(t)
        };
        if (state == "offline" || state == "outage")
        {
            m.Shape = NewsShape.Notice;
            m.EmptyTitle = t(state == "outage" ? "reallife_news_stand_out" : "reallife_news_stand_off", state == "outage" ? "The network is in an outage" : "The home connection is down");
            m.EmptyBody = t(state == "outage" ? "reallife_news_stand_out_body" : "reallife_news_stand_off_body", "Papers you have paid for are kept.");
            m.Note = m.EmptyBody;
            return m;
        }
        if (state.StartsWith("reading", StringComparison.Ordinal))
        {
            NewsReadingSample(t, m, state);
            return m;
        }
        m.Shape = NewsShape.Stand;
        m.StandLine = t("reallife_ledger_online_box_net", "Your connection") + ": Working, at 7 5th Avenue.";
        m.Note = NewsFill(t("reallife_news_stand_sum", "{count} subscriptions, {money} a week"), "count", "2", "money", "$11");
        string lead = t("reallife_news_stand_lead", "Today's lead");
        string edition = t("reallife_ledger_online_box_today", "Today's edition");
        string week = t("reallife_news_stand_week", "a week");
        string chip = t("reallife_news_stand_chip", "Subscribed");
        string renews = t("reallife_ledger_online_state_renews", "Renews on day {day}");
        string stop = t("reallife_news_stand_stop", "Stop");
        string sub = t("reallife_news_stand_sub", "Subscribe");
        string read = t("reallife_news_stand_to_read", "Subscribe to read");
        m.Titles = new[]
        {
            new NewsTitle
            {
                Mark = "CL", Tint = UiRole.Chart1, Name = t("reallife_ledger_online_name_ledger", "The City Ledger"), Tagline = t("reallife_ledger_online_tagline_ledger", "The city's paper"),
                Covers = t("reallife_ledger_online_about_ledger", "The whole morning paper"), LeadLabel = lead, Lead = "Bank rate to rise again, by 0.25 points", EditionLabel = edition,
                Edition = t("reallife_ledger_online_today_in", "On your phone"), EditionTone = UiRole.OkText, Price = "$6", PerWeek = week, Chip = chip,
                Renews = NewsFill(renews, "day", "293"), Action = stop, ActionKind = UiButtonKind.Secondary
            },
            new NewsTitle
            {
                Mark = "EG", Tint = UiRole.Chart2, Name = t("reallife_ledger_online_name_exchange", "The Exchange Gazette"), Tagline = t("reallife_ledger_online_tagline_exchange", "Money and markets"),
                Covers = t("reallife_ledger_online_about_exchange", "The economy, business and the stock market"), LeadLabel = lead, Lead = "Harbourside Freight lifts the index to 1,285",
                EditionLabel = edition, Edition = t("reallife_ledger_online_today_in", "On your phone"), EditionTone = UiRole.OkText, Price = "$5", PerWeek = week, Chip = chip,
                Renews = NewsFill(renews, "day", "295"), Action = stop, ActionKind = UiButtonKind.Secondary
            },
            new NewsTitle
            {
                Mark = "SS", Tint = UiRole.Chart3, Name = t("reallife_ledger_online_name_street", "The Street Sheet"), Tagline = t("reallife_ledger_online_tagline_street", "What happened on the streets"),
                Covers = t("reallife_ledger_online_about_street", "Crime, accidents, the courts and City Hall"), LeadLabel = lead, Lead = "Warehouse fire in Hell's Kitchen put out in an hour",
                EditionLabel = edition, Edition = read, Price = "$3", PerWeek = week, Action = sub, ActionKind = UiButtonKind.Secondary
            },
            new NewsTitle
            {
                Mark = "WR", Tint = UiRole.Chart4, Name = t("reallife_ledger_online_name_review", "The Weekend Review"), Tagline = t("reallife_ledger_online_tagline_review", "Where to go"),
                Covers = t("reallife_ledger_online_about_review", "What's on, the city after dark and its people"), LeadLabel = lead, Lead = "Five places to hear jazz in Murray Hill this weekend",
                EditionLabel = edition, Edition = read, Price = "$3", PerWeek = week, Action = sub, ActionKind = UiButtonKind.Secondary
            },
            new NewsTitle
            {
                Pass = true, Icon = UiIcon.Newspaper, Name = t("reallife_ledger_online_name_pass", "Newsstand Pass"), Covers = t("reallife_ledger_online_about_pass", "All four titles, for less"),
                Tagline = NewsFill(t("reallife_news_stand_save", "The Pass saves {money} a week"), "money", "$5"), Price = "$12", PerWeek = week, Action = sub, ActionKind = UiButtonKind.Primary
            }
        };
        return m;
    }

    // The opening story's words after its kicker ("OPENING:"), as the paper prints its body.
    private static string NewsOpenedBody(UiWords t, string firm)
    {
        string text = NewsFill(t("reallife_ledger_opened_city", "OPENING: {firm} has opened for business in the city."), "firm", firm);
        int cut = text.IndexOfAny(new[] { ':', '：' });
        return cut < 0 ? text : text.Substring(cut + 1).Trim();
    }

    // The Reading tab as the live fill writes it: the papers to pick, the days of the open paper, the printed page, and the reader; or what to do for a paper you do not take.
    private static void NewsReadingSample(UiWords t, NewsModel m, string state)
    {
        m.Tab = 1;
        m.Shape = NewsShape.Reading;
        string[] names =
        {
            t("reallife_ledger_online_name_ledger", "The City Ledger"), t("reallife_ledger_online_name_exchange", "The Exchange Gazette"),
            t("reallife_ledger_online_name_street", "The Street Sheet"), t("reallife_ledger_online_name_review", "The Weekend Review")
        };
        int open = state == "reading_not_held" ? 2 : state == "reading_none" ? -1 : 0;
        m.ReadTitles = new UiActionModel[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            m.ReadTitles[i] = new UiActionModel { Label = names[i], Kind = i == open ? UiButtonKind.Chip : UiButtonKind.Secondary, Key = UiAction.Count, Command = NewsCommands.ReadTitle, Arg = i };
        }
        m.EmptyAction = t("reallife_ledger_online_to_titles", "Titles");
        if (state == "reading_not_held")
        {
            m.EmptyTitle = NewsFill(t("reallife_news_read_not_held", "You do not take {title}"), "title", names[2]);
            m.EmptyBody = NewsFill(t("reallife_news_read_not_held_body", "Take it out on the Titles tab to read it. A week of {title} costs {money}, and today's edition comes at once."),
                "title", names[2], "money", "$3");
            return;
        }
        if (state == "reading_none")
        {
            m.EmptyTitle = t("reallife_news_read_none", "Nothing to read yet");
            m.EmptyBody = NewsFill(t("reallife_ledger_online_nothing", "Nothing to read yet. Subscribe on the Titles tab: today's edition comes at once, then a new one each morning at {hour}."), "hour", "06:00");
            return;
        }
        string today = NewsFill(t("reallife_ledger_online_day_today", "Today, day {day}"), "day", "289");
        string earlier = t("reallife_ledger_online_day", "{weekday}, day {day}");
        m.ReadDays = new[]
        {
            new UiActionModel { Label = today, Kind = UiButtonKind.Chip, Key = UiAction.Count, Command = NewsCommands.ReadDay, Arg = 289 },
            new UiActionModel { Label = NewsFill(earlier, "weekday", "Monday", "day", "288"), Kind = UiButtonKind.Secondary, Key = UiAction.Count, Command = NewsCommands.ReadDay, Arg = 288 },
            new UiActionModel { Label = NewsFill(earlier, "weekday", "Sunday", "day", "287"), Kind = UiButtonKind.Secondary, Key = UiAction.Count, Command = NewsCommands.ReadDay, Arg = 287 }
        };
        m.ReadPrinted = new UiActionModel { Label = t("reallife_ledger_online_printed", "The printed page"), Kind = UiButtonKind.Primary, Key = UiAction.Count, Command = NewsCommands.ReadPrinted };
        m.Reader = new UiReaderModel
        {
            Title = names[0] + ", Tuesday, day 289",
            Blocks = new[]
            {
                new UiReaderBlock { Kind = UiReaderKind.Heading, Text = "Bank rate to rise again, by 0.25 points" },
                new UiReaderBlock
                {
                    Kind = UiReaderKind.Paragraph,
                    Text = "The central bank will lift its rate to 4.25% on day 291, its third rise in a month, after inflation reached 6.0%. Loans on a variable rate will cost more from the same day."
                },
                new UiReaderBlock { Kind = UiReaderKind.Heading, Text = "Crews fix nine street lights on 9th Avenue" },
                new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = "Residents reported a dark stretch near the Garment District. Work went on through the night." }
            }
        };
    }

    private static UiEditionStory Story(string head, string body) => new UiEditionStory { Headline = head, Body = body };

    // The City Ledger as the live fill writes it: the front page, the markets page, the share prices, a quiet morning, and a front page of its own.
    public static NewsModel NewsPaper(UiWords t, string state)
    {
        var m = new NewsModel
        {
            App = NewsApp.Paper,
            Icon = UiIcon.Newspaper,
            Title = t("reallife_ledger_title", "The City Ledger"),
            Across = state != "special",
            Nav = state == "special" ? new UiNavEntry[0] : new[]
            {
                new UiNavEntry { Icon = UiIcon.Newspaper, Label = t("reallife_ledger_tab_front", "Front page") },
                new UiNavEntry { Icon = UiIcon.ChartLineUp, Label = t("reallife_ledger_tab_markets", "Markets") },
                new UiNavEntry { Icon = UiIcon.ListChecks, Label = t("reallife_ledger_tab_prices", "Share prices") }
            },
            Prompts = NewsPrompts(t),
            StripNote = "Tuesday, day 289 · " + NewsFill(t("reallife_ledger_dateline_number", "Year {year}, number {number}"), "year", "3", "number", "1,102")
                        + " · " + NewsFill(t("reallife_ledger_dateline_price", "Price {price}"), "price", "$2")
        };
        if (state == "markets")
        {
            m.Tab = 1;
            m.Shape = NewsShape.PaperMarkets;
            m.Edition = NewsMarketsEdition(t);
        }
        else if (state == "prices")
        {
            m.Tab = 2;
            m.Shape = NewsShape.PaperPrices;
            m.Note = t("reallife_ledger_prices_legend", "Closing price and the move on the day, by sector.");
            m.Shares = NewsShares(t);
        }
        else if (state == "special")
        {
            m.Shape = NewsShape.Paper;
            m.StripNote = "Tuesday, day 289 · Obituaries · A special edition";
            m.Edition = NewsSpecial(t);
        }
        else
        {
            m.Shape = NewsShape.Paper;
            m.Edition = state == "quiet" ? NewsQuiet(t) : NewsFront(t);
        }
        return m;
    }

    private static UiFigureModel[] NewsRates(UiWords t)
    {
        return new[]
        {
            new UiFigureModel
            {
                Label = t("reallife_news_series_index", "City index"), Value = "1,284.60", Change = "0.80%", Rising = true, Good = true,
                Trend = new[] { 1210f, 1218f, 1214f, 1222f, 1236f, 1230f, 1241f, 1252f, 1248f, 1266f, 1275f, 1284f }
            },
            new UiFigureModel { Label = t("reallife_news_series_rate", "Bank rate"), Value = "4.00%", Trend = new[] { 3f, 3f, 3.25f, 3.5f, 3.5f, 3.75f, 4f, 4f } },
            new UiFigureModel { Label = t("reallife_news_series_inflation", "Inflation"), Value = "6.0%", Trend = new[] { 4.2f, 4.6f, 5f, 5.2f, 5.6f, 5.8f, 6f, 6f } },
            new UiFigureModel { Label = t("reallife_news_the_economy", "The economy"), Value = t("reallife_macro_state_slowdown", "Slowdown") }
        };
    }

    private static UiEditionModel NewsFront(UiWords t)
    {
        var band = new[]
        {
            new UiFigureModel
            {
                Label = t("reallife_news_series_index", "City index"), Value = "1,284.60", Change = "0.80%", Rising = true, Good = true,
                Trend = new[] { 1210f, 1218f, 1214f, 1222f, 1236f, 1230f, 1241f, 1252f, 1248f, 1266f, 1275f, 1284f }
            },
            new UiFigureModel
            {
                Label = "Harbourside Freight HSF", Value = "$53.42", Change = "1.31%", Rising = true, Good = true,
                Trend = new[] { 50f, 50.4f, 50.2f, 51f, 51.8f, 51.6f, 52.5f, 52.9f, 53.1f, 53.4f }
            },
            new UiFigureModel
            {
                Label = "Aurora Pharma AUP", Value = "$112.80", Change = "1.90%", Rising = true, Good = true,
                Trend = new[] { 104f, 105f, 106.5f, 106f, 108f, 109.5f, 110f, 111f, 112f, 112.8f }
            },
            new UiFigureModel
            {
                Label = "Kestrel Motors KST", Value = "$67.20", Change = "2.80%", Rising = false, Good = false,
                Trend = new[] { 72f, 71.5f, 70.8f, 70f, 69.6f, 68.9f, 68.2f, 68f, 67.5f, 67.2f }
            },
            new UiFigureModel
            {
                Label = "Northgate Retail NGR", Value = "$19.45", Change = "2.10%", Rising = false, Good = false,
                Trend = new[] { 21.2f, 21f, 20.6f, 20.5f, 20.1f, 20f, 19.8f, 19.6f, 19.5f, 19.45f }
            }
        };
        return new UiEditionModel
        {
            LeadIcon = UiIcon.Chart,
            LeadTint = UiRole.Accent,
            LeadKicker = t("reallife_ledger_sec_economy", "The economy"),
            LeadHeadline = "Bank rate to rise again, by 0.25 points",
            LeadStandfirst = "The central bank will lift its rate to 4.25% on day 291, its third rise in a month, after inflation reached 6.0%. Loans on a variable rate, "
                             + "including most business loans, will cost more from the same day.",
            Markets = NewsRates(t),
            Sections = new[]
            {
                new UiEditionSection
                {
                    Icon = UiIcon.MapPin, Label = t("reallife_ledger_sec_city", "The city"),
                    Stories = new[]
                    {
                        Story("Crews fix nine street lights on 9th Avenue", "Residents reported a dark stretch near the Garment District. Work went on through the night."),
                        Story("Council to vote on Uptown roadworks", "Three lanes of the new avenue would close for ten days from day 296."),
                        Story("Pedestrian struck on Madison Avenue", "A person on foot was hit by a vehicle at about 18:40 and treated by paramedics.")
                    }
                },
                new UiEditionSection
                {
                    Icon = UiIcon.Store, Label = t("reallife_ledger_sec_business", "Business"),
                    Stories = new[]
                    {
                        Story("Huang Guo's chain raised prices", "Guo Holdings put up prices in its shops on day 284."),
                        Story(NewsFill(t("reallife_ledger_head_opened_city", "{firm} opens its doors"), "firm", "Pizzeria Luigi"),
                            NewsOpenedBody(t, "Pizzeria Luigi")),
                        Story("Harbourside Freight lifts the index", "Its shares closed at $53.42 on a stronger quarter for deliveries.")
                    }
                },
                new UiEditionSection
                {
                    Icon = UiIcon.People, Label = t("reallife_ledger_sec_around", "Around town"),
                    Stories = new[]
                    {
                        Story("Jazz returns to Murray Hill", "Five places to hear it this weekend, from a basement bar to the new Galleria stage."),
                        Story("Faces of the city: a courier in Midtown", "\"Business is the same as ever, which is to say busy,\" she says."),
                        Story("Tonight: the night market on 5th Avenue", "Stalls open until 23:00.")
                    }
                },
                new UiEditionSection
                {
                    Icon = UiIcon.Cloud, Label = t("reallife_ledger_sec_weather", "Weather"),
                    Stories = new[] { Story("Dry and settled", "No rain in sight. Clear skies over the city this morning.") }
                }
            },
            BandTitle = t("reallife_news_band_title", "The markets"),
            BandNote = NewsFill(t("reallife_news_band_note", "Prices at the close of day {day}"), "day", "288"),
            Band = band
        };
    }

    private static UiEditionModel NewsQuiet(UiWords t)
    {
        return new UiEditionModel
        {
            LeadIcon = UiIcon.Newspaper,
            LeadTint = UiRole.Accent,
            LeadKicker = t("reallife_ledger_sec_city", "The city"),
            LeadHeadline = t("reallife_ledger_quiet_head", "A quiet morning"),
            LeadStandfirst = t("reallife_ledger_quiet_body", "Nothing out of the ordinary to report: the city kept itself to itself."),
            Markets = NewsRates(t),
            Sections = new[]
            {
                new UiEditionSection
                {
                    Icon = UiIcon.Cloud, Label = t("reallife_ledger_sec_weather", "Weather"),
                    Stories = new[] { Story("Dry and settled", "No rain in sight. Clear skies over the city this morning.") }
                }
            },
            QuietLine = t("reallife_news_paper_quiet_body", "The city had little to report. The next edition is at 06:00.")
        };
    }

    private static UiEditionModel NewsMarketsEdition(UiWords t)
    {
        return new UiEditionModel
        {
            LeadIcon = UiIcon.ChartLineUp,
            LeadTint = UiRole.Accent,
            LeadKicker = t("reallife_ledger_sec_markets", "Markets"),
            LeadHeadline = "Harbourside Freight lifts the index",
            LeadStandfirst = "Harbourside Freight closed at $53.42 on a stronger quarter for deliveries across the five boroughs, and the city index ended the session at 1,284.60.",
            Markets = new[]
            {
                new UiFigureModel
                {
                    Label = t("reallife_news_series_index", "City index"), Value = "1,284.60", Change = "0.80%", Rising = true, Good = true,
                    Trend = new[] { 1210f, 1218f, 1214f, 1222f, 1236f, 1230f, 1241f, 1252f, 1248f, 1266f, 1275f, 1284f }
                },
                new UiFigureModel { Label = "over five sessions", Value = "+2.40%", Good = true },
                new UiFigureModel { Label = "Power", Value = "+3.10%", Good = true },
                new UiFigureModel { Label = "Banks", Value = "-2.90%", Good = false }
            },
            Sections = new[]
            {
                new UiEditionSection
                {
                    Icon = UiIcon.ChartLineUp, Label = t("reallife_ledger_sec_markets", "Markets"),
                    Stories = new[]
                    {
                        Story("Kestrel Motors falls on a weak quarter", "Shares fell 2.8% to $67.20 after the results missed what the market expected."),
                        Story("Aurora Pharma gains on a new drug", "The company rose 1.9% to $112.80."),
                        Story("Northgate Retail slips as shoppers cut back", "Shares ended 2.1% lower at $19.45.")
                    }
                }
            },
            NoticesTitle = t("reallife_news_movers", "Movers of the session"),
            Notices = new[]
            {
                new UiEditionNotice { Icon = UiIcon.TrendUp, Text = "Brightwater Utilities", Value = "+2.40%" },
                new UiEditionNotice { Icon = UiIcon.TrendUp, Text = "Aurora Pharma", Value = "+1.90%" },
                new UiEditionNotice { Icon = UiIcon.TrendUp, Text = "Marrow & Vine", Value = "+1.60%" },
                new UiEditionNotice { Icon = UiIcon.TrendDown, Text = "Kestrel Motors", Value = "-2.80%" },
                new UiEditionNotice { Icon = UiIcon.TrendDown, Text = "Northgate Retail", Value = "-2.10%" },
                new UiEditionNotice { Icon = UiIcon.TrendDown, Text = "Cobalt Telecom", Value = "-1.40%" }
            },
            BandTitle = t("reallife_news_sectors", "Sectors"),
            BandNote = NewsFill(t("reallife_news_band_note", "Prices at the close of day {day}"), "day", "288"),
            Band = new[]
            {
                new UiFigureModel { Label = "Power", Value = "1,402.10", Change = "3.10%", Rising = true, Good = true },
                new UiFigureModel { Label = "Healthcare", Value = "988.40", Change = "2.20%", Rising = true, Good = true },
                new UiFigureModel { Label = "Logistics", Value = "1,120.60", Change = "1.90%", Rising = true, Good = true },
                new UiFigureModel { Label = "Stores", Value = "764.30", Change = "1.80%", Rising = false, Good = false },
                new UiFigureModel { Label = "Banks", Value = "1,015.00", Change = "2.90%", Rising = false, Good = false }
            }
        };
    }

    private static UiDataTableModel NewsShares(UiWords t)
    {
        string[] rows =
        {
            "Brightwater Utilities|BWU|Power|38.10|+2.40%|88,400", "Harbourside Freight|HSF|Logistics|53.42|+1.31%|148,200", "Aurora Pharma|AUP|Healthcare|112.80|+1.90%|61,000",
            "Kestrel Motors|KST|Stores|67.20|-2.80%|121,500", "Northgate Retail|NGR|Stores|19.45|-2.10%|96,800", "Marrow & Vine|MVN|Groceries|24.65|+1.60%|74,300",
            "Cobalt Telecom|CBT|Telecoms|41.30|-1.40%|52,900", "Granite Realty|GRR|Property|55.90|-0.90%|33,100", "Meridian Bank|MRB|Banks|71.15|-2.90%|47,800"
        };
        var data = new UiDataRow[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            string[] c = rows[i].Split('|');
            data[i] = new UiDataRow { Cells = c, Values = new[] { 0, 0, 0, 0, c[4].StartsWith("-") ? -1 : 1, 0 }, Tones = new UiRole[6] };
        }
        return new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_news_col_company", "Company"), Kind = UiCellKind.Text },
                new UiDataColumn { Title = t("reallife_news_col_code", "Code"), Kind = UiCellKind.Text, Width = 96f },
                new UiDataColumn { Title = t("reallife_news_col_sector", "Sector"), Kind = UiCellKind.Text, Width = 192f },
                new UiDataColumn { Title = t("reallife_news_col_close", "Close"), Kind = UiCellKind.Figure, Width = 112f },
                new UiDataColumn { Title = t("reallife_news_col_move", "Move"), Kind = UiCellKind.Trend, Width = 128f },
                new UiDataColumn { Title = t("reallife_news_col_shares", "Shares"), Kind = UiCellKind.Figure, Width = 144f }
            },
            Rows = data,
            RowCount = data.Length,
            EmptyIcon = UiIcon.ListChecks
        };
    }

    private static UiEditionModel NewsSpecial(UiWords t)
    {
        return new UiEditionModel
        {
            LeadIcon = UiIcon.Newspaper,
            LeadTint = UiRole.Accent,
            LeadKicker = "Obituary",
            LeadHeadline = "Jordan Hale has died at 38",
            LeadStandfirst = "Jordan Hale, who founded Speedy Bites on 5th Avenue, died on day 289 after collapsing at work. They had lived in the city for 289 days.",
            Sections = new[]
            {
                new UiEditionSection
                {
                    Icon = UiIcon.Newspaper, Label = "A life in the city",
                    Stories = new[]
                    {
                        Story("Three businesses and 27 people", "Speedy Bites, Bloom & Co and Pioneer Game Studio were running at the end."),
                        Story("Mourned by friends", "Priya Shah and Luis Ortega were named among those closest to them.")
                    }
                }
            },
            NoticesTitle = "In memory of",
            Notices = new[]
            {
                new UiEditionNotice { Icon = UiIcon.Info, Text = "Aged", Value = "38" },
                new UiEditionNotice { Icon = UiIcon.Info, Text = "Days in the city", Value = "289" }
            }
        };
    }

    // The permadeath ending as the live fill writes it.
    public static NewsModel NewsEnding(UiWords t, string state)
    {
        return new NewsModel
        {
            App = NewsApp.Ending,
            Shape = NewsShape.Ending,
            Decision = new UiDecisionModel
            {
                Kicker = t("reallife_pd_obit_kicker", "Obituary"),
                Title = "Alex Marlowe has died at 38",
                Portrait = "Alex Marlowe",
                PortraitLines = new[] { "Aged 38", "Founder of Speedy Bites" },
                Body = "Alex Marlowe collapsed at work on day 289 and could not be saved. They came to the city with $5,000 and left three businesses: Speedy Bites on 5th Avenue, "
                       + "Bloom & Co in the Garment District and Pioneer Game Studio. They are mourned by Priya Shah and by their close friends, Luis Ortega and Imani Brooks. "
                       + "The City Ledger ran the obituary on its front page.",
                BodyLines = 10,
                FactLabels = new[]
                {
                    t("reallife_news_end_days", "Days in the city"), t("reallife_news_end_shops", "Businesses"), t("reallife_news_end_staff", "People employed"),
                    t("reallife_news_end_worth", "Net worth"), t("reallife_news_end_rank", "Rich list")
                },
                FactValues = new[] { "289", "3", "27", "$19.64M", "#4" },
                Choices = new[]
                {
                    new UiDecisionChoice { Key = UiAction.Pick1, Name = t("reallife_news_end_load", "Load the backup") },
                    new UiDecisionChoice { Key = UiAction.Pick2, Name = t("reallife_news_end_quit", "Quit to the desktop") }
                },
                Prompts = NewsPrompts(t),
                PromptCount = 3
            }
        };
    }
}
