using System;
using System.Text;
using Entities;
using Helpers;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// City Hall's fifth topic, the player's own politics, as pages of the game's conversation panel: standing, the run, the
    /// campaign and an office held. Pages are built when shown; nothing here runs on a tick.
    /// </summary>
    internal static class OfficePhone
    {
        private static CivicOffice Office => CivicDirector.Instance != null ? CivicDirector.Instance.Office : null;
        private static CivicGovernment Gov => CivicDirector.Instance != null ? CivicDirector.Instance.Government : null;
        private static int Day => TimeHelper.CurrentDay;
        private static int Hour => TimeHelper.CurrentHour;
        private static readonly StringBuilder Text = new StringBuilder(1024);
        private static readonly int[] Proposal = new int[4];

        private static string T(string key) => key.GetLocalization();

        private static string Fill(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        private static string Money(int amount) => CivicText.Money(amount);

        private static DialogEntry Gone() => CityHallPhone.Closing(T("reallife_civic_closed"));

        private static string Race(OfficeRace race, CivicOffice office)
        {
            return race == OfficeRace.Mayor ? T("reallife_civic_office_race_mayor") : Fill("reallife_civic_office_race_council", "district", office.DistrictName(office.HomeDistrict()));
        }

        /// <summary>The topic's first page: where the player stands in the city's politics, and the way in.</summary>
        public static DialogEntry Topic(Func<DialogEntry> next)
        {
            CivicOffice office = Office;
            CivicGovernment gov = Gov;
            if (office == null || gov == null)
            {
                return Gone();
            }
            OfficeState s = office.State;
            GovState g = gov.State;
            if (!CivicOffice.Enabled)
            {
                return CityHallPhone.Page(T("reallife_civic_office_topic_off"), "reallife_civic_button_next_topic", next);
            }
            if (s.InOffice)
            {
                string text = Fill(s.Holds == OfficeRace.Mayor ? "reallife_civic_office_topic_mayor" : "reallife_civic_office_topic_council",
                    "district", office.DistrictName(office.HomeDistrict()), "approval", Mathf.RoundToInt(office.Approval).ToString(),
                    "meeting", Mathf.Max(0, s.NextMeeting - Day).ToString(), "agenda", s.Agenda.Count.ToString(), "salary", Money(Prices.At(OfficeRules.Salary(s.Holds))));
                if (s.RecallDay > 0)
                {
                    text += "\n" + Fill("reallife_civic_office_recall_line", "days", Mathf.Max(0, s.RecallDay - Day).ToString());
                }
                if (office.Campaigning || office.Collecting)
                {
                    return CityHallPhone.Page(text, "reallife_civic_office_button_office", () => OfficeMenu(0),
                        "reallife_civic_office_button_campaign", () => office.Collecting ? Prep(0) : Campaign(0));
                }
                return CityHallPhone.Page(text, "reallife_civic_office_button_office", () => OfficeMenu(0), "reallife_civic_office_button_stand", () => StandPage(next));
            }
            if (office.Campaigning)
            {
                return CityHallPhone.Page(CampaignSummary(office, gov), "reallife_civic_office_button_campaign", () => Campaign(0), "reallife_civic_button_next_topic", next);
            }
            if (office.Collecting)
            {
                return CityHallPhone.Page(PrepSummary(office), "reallife_civic_office_button_run", () => Prep(0), "reallife_civic_button_next_topic", next);
            }
            if (s.Filed)
            {
                string text = Fill("reallife_civic_office_topic_filed", "race", Race(s.Race, office), "days", Mathf.Max(0, s.ForDay - CivicCatalogue.CampaignDays - Day).ToString());
                return CityHallPhone.Page(text, "reallife_civic_office_button_fund", () => FundPage(() => Topic(next)), "reallife_civic_button_next_topic", next);
            }
            string none = Fill("reallife_civic_office_topic_none", "day", g.ElectionDay.ToString(), "opens", (g.ElectionDay - OfficeRules.NominationsOpen).ToString(),
                "closes", OfficeRules.NominationsClose(g.ElectionDay, CivicCatalogue.CampaignDays).ToString(),
                "council", OfficeRules.SignaturesCouncil.ToString(), "mayor", OfficeRules.SignaturesMayor.ToString(),
                "deposit_council", Money(Prices.At(OfficeRules.DepositCouncil)), "deposit_mayor", Money(Prices.At(OfficeRules.DepositMayor)));
            if (s.Result != 0 && s.ResultDay > 0)
            {
                none += "\n" + Fill(s.Result == 1 ? "reallife_civic_office_last_won" : "reallife_civic_office_last_lost", "share", (s.ResultShare / 10f).ToString("0.#"));
            }
            return CityHallPhone.Page(none, "reallife_civic_office_button_stand", () => StandPage(next), "reallife_civic_button_next_topic", next);
        }

        // Standing for the council or for mayor: what it takes.
        private static DialogEntry StandPage(Func<DialogEntry> next)
        {
            CivicOffice office = Office;
            if (office == null)
            {
                return Gone();
            }
            OfficeRefusal why = office.CanStand(Day);
            if (why != OfficeRefusal.None)
            {
                return CityHallPhone.Page(T(CivicOffice.RefusalKey(why)), "reallife_civic_button_back", () => Topic(next));
            }
            string text = Fill("reallife_civic_office_stand_page", "district", office.DistrictName(office.HomeDistrict()),
                "council", OfficeRules.SignaturesCouncil.ToString(), "mayor", OfficeRules.SignaturesMayor.ToString(),
                "deposit_council", Money(Prices.At(OfficeRules.DepositCouncil)), "deposit_mayor", Money(Prices.At(OfficeRules.DepositMayor)),
                "limit_council", Money(Prices.At(OfficeRules.DonorLimitCouncil)), "limit_mayor", Money(Prices.At(OfficeRules.DonorLimitMayor)));
            return CityHallPhone.Page(text, "reallife_civic_office_button_council", () => Begun(OfficeRace.Council), "reallife_civic_office_button_mayor", () => Begun(OfficeRace.Mayor));
        }

        private static DialogEntry Begun(OfficeRace race)
        {
            CivicOffice office = Office;
            if (office == null)
            {
                return Gone();
            }
            string result = office.Stand(race, Day);
            if (!office.Collecting)
            {
                return CityHallPhone.Closing(T(result));
            }
            return CityHallPhone.Page(T(result), "reallife_civic_office_button_run", () => Prep(0));
        }

        private static string PrepSummary(CivicOffice office)
        {
            OfficeState s = office.State;
            int needed = OfficeRules.SignaturesNeeded(s.Race);
            return Fill("reallife_civic_office_topic_collecting", "race", Race(s.Race, office), "signatures", s.Signatures.ToString(), "needed", needed.ToString(),
                "planks", s.Me.Planks.ToString(), "party", CivicText.Party(s.Me), "closes", Mathf.Max(0, OfficeRules.NominationsClose(s.ForDay, CivicCatalogue.CampaignDays) - Day).ToString(),
                "deposit", Money(Prices.At(OfficeRules.Deposit(s.Race))));
        }

        private const int PrepPages = 7;

        private static DialogEntry Prep(int option)
        {
            CivicOffice office = Office;
            if (office == null || !office.Collecting)
            {
                return Office != null ? Topic(() => CityHallPhone.Closing(T("reallife_civic_office_bye"))) : Gone();
            }
            OfficeState s = office.State;
            option = (option % PrepPages + PrepPages) % PrepPages;
            int next = option + 1;
            Func<DialogEntry> more = () => Prep(next);
            switch (option)
            {
                case 0:
                    return CityHallPhone.Page(Fill("reallife_civic_office_prep_platform", "platform", CityHallPhone.Platform(s.Me), "planks", s.Me.Planks.ToString()),
                        "reallife_civic_office_button_change_platform", () => PlatformPage(0), "reallife_civic_button_next_option", more);
                case 1:
                {
                    int issue = OfficeRules.PartyIssue(s.Me.Party);
                    string text = Fill("reallife_civic_office_prep_party", "party", CivicText.Party(s.Me),
                        "line", issue >= 0 ? Fill("reallife_civic_office_party_line", "issue", T(CivicCatalogue.IssueNames[issue]),
                            "grant", Money(Prices.At(OfficeRules.PartyGrant(s.Race, s.Me.Party))))
                            : T("reallife_civic_office_party_independent"));
                    return CityHallPhone.Page(text, "reallife_civic_office_button_next_party", () =>
                    {
                        int party = s.Me.Party >= CivicCatalogue.Parties.Length ? 0 : s.Me.Party + 1;
                        office.SetParty(party >= CivicCatalogue.Parties.Length ? CivicCatalogue.Independent : party);
                        return Prep(1);
                    }, "reallife_civic_button_next_option", more);
                }
                case 2:
                    return CityHallPhone.Page(Fill("reallife_civic_office_prep_slogan", "slogan", T(CivicCatalogue.Slogans[s.Me.Slogan]), "issue", T(CivicCatalogue.IssueNames[s.Me.Lead])),
                        "reallife_civic_office_button_next_slogan", () =>
                        {
                            office.NextSlogan();
                            return Prep(2);
                        }, "reallife_civic_button_next_option", more);
                case 3:
                    return CityHallPhone.Page(Fill(s.Sheets ? "reallife_civic_office_prep_signatures_done" : "reallife_civic_office_prep_signatures",
                            "signatures", s.Signatures.ToString(), "needed", OfficeRules.SignaturesNeeded(s.Race).ToString(), "per", OfficeRules.SheetsPerBusiness.ToString()),
                        "reallife_civic_office_button_sheets", () =>
                        {
                            int names = office.Sheets();
                            return CityHallPhone.Page(Fill(names > 0 ? "reallife_civic_office_sheets_in" : "reallife_civic_office_sheets_none",
                                "count", names.ToString(), "signatures", s.Signatures.ToString()),
                                "reallife_civic_button_back", () => Prep(3));
                        }, "reallife_civic_button_next_option", more);
                case 4:
                {
                    int needed = OfficeRules.SignaturesNeeded(s.Race);
                    string text = Fill("reallife_civic_office_prep_file", "signatures", s.Signatures.ToString(), "needed", needed.ToString(), "deposit", Money(Prices.At(OfficeRules.Deposit(s.Race))),
                        "back", Mathf.RoundToInt(OfficeRules.DepositBack * 100f).ToString());
                    return CityHallPhone.Page(text, s.Incumbent || s.Signatures >= needed ? "reallife_civic_office_button_file" : "reallife_civic_office_button_file_deposit", () =>
                    {
                        string result = office.File(true, Day);
                        return office.State.Filed ? CityHallPhone.Closing(Fill(result, "race", Race(s.Race, office))) : CityHallPhone.Page(T(result), "reallife_civic_button_back", () => Prep(4));
                    }, "reallife_civic_button_next_option", more);
                }
                case 5:
                    return FundPage(() => Prep(6));
                default:
                    return CityHallPhone.Page(T("reallife_civic_office_prep_withdraw"), "reallife_civic_office_button_withdraw", () => CityHallPhone.Closing(T(office.Withdraw())),
                        "reallife_civic_button_next_option", () => Prep(0));
            }
        }

        // One page a policy: in the platform or not.
        private static DialogEntry PlatformPage(int index)
        {
            CivicOffice office = Office;
            if (office == null || !office.Collecting)
            {
                return Gone();
            }
            index = (index % CivicCatalogue.All.Length + CivicCatalogue.All.Length) % CivicCatalogue.All.Length;
            PolicyDef policy = CivicCatalogue.All[index];
            bool mine = office.InPlatform(index);
            int next = index + 1;
            string text = Fill(mine ? "reallife_civic_office_plank_in" : "reallife_civic_office_plank_out", "policy", T(policy.Name), "effect", CivicText.Full(policy),
                "issue", T(CivicCatalogue.IssueNames[(int)policy.Issue]), "planks", office.State.Me.Planks.ToString());
            return CityHallPhone.Page(text, mine ? "reallife_civic_office_button_plank_out" : "reallife_civic_office_button_plank_in", () =>
            {
                bool done = office.Toggle(index);
                return CityHallPhone.Page(T(done ? "reallife_civic_office_platform_changed" : "reallife_civic_office_platform_full") + CityHallPhone.Platform(office.State.Me),
                    "reallife_civic_button_next_policy", () => PlatformPage(next), "reallife_civic_office_button_run", () => Prep(0));
            }, "reallife_civic_button_next_policy", () => PlatformPage(next));
        }

        // The fund: what is in it, and the player's own money.
        private static DialogEntry FundPage(Func<DialogEntry> next)
        {
            CivicOffice office = Office;
            if (office == null)
            {
                return Gone();
            }
            OfficeState s = office.State;
            string text = Fill("reallife_civic_office_fund_page", "fund", Money(s.Fund), "raised", Money(s.Raised), "spent", Money(s.Spent), "own", Money(s.Own),
                "limit", Money(office.Limit), "step", Money(Prices.At(OfficeRules.OwnStep)));
            return CityHallPhone.Page(text, "reallife_civic_office_button_put_in", () =>
            {
                string result = office.PutIn();
                return result == "reallife_civic_office_no_money" ? null : CityHallPhone.Page(Fill(result, "amount", Money(Prices.At(OfficeRules.OwnStep)), "fund", Money(office.State.Fund)),
                    "reallife_civic_button_back", () => FundPage(next));
            }, "reallife_civic_button_next_option", next);
        }

        private static string CampaignSummary(CivicOffice office, CivicGovernment gov)
        {
            OfficeState s = office.State;
            int lead = office.Leader();
            Candidate leader = office.RaceCandidate(lead);
            Text.Length = 0;
            Text.Append(Fill("reallife_civic_office_topic_campaign", "race", Race(s.Race, office), "share", CivicGovernment.Percent(office.MyPoll),
                "leader", leader != null ? CivicText.Name(leader) : string.Empty, "theirs", CivicGovernment.Percent(office.RacePoll(lead)),
                "days", Mathf.Max(0, gov.State.ElectionDay - Day).ToString(), "fund", Money(s.Fund)));
            if (s.OfferKind >= 0)
            {
                Text.Append('\n').Append(Fill("reallife_civic_office_offer_waiting", "amount", Money(s.OfferAmount)));
            }
            if (s.AttackKind >= 0)
            {
                Candidate by = office.RaceCandidate(s.AttackBy);
                Text.Append('\n').Append(Fill("reallife_civic_office_attack_waiting", "name", by != null ? CivicText.Name(by) : string.Empty));
            }
            if (s.PostersHeld > 0)
            {
                Text.Append('\n').Append(Fill("reallife_civic_office_posters_held", "count", s.PostersHeld.ToString()));
            }
            return Text.ToString();
        }

        private const int CampaignPages = 7;

        private static DialogEntry Campaign(int option)
        {
            CivicOffice office = Office;
            CivicGovernment gov = Gov;
            if (office == null || gov == null || !office.Campaigning)
            {
                return Office != null ? Topic(() => CityHallPhone.Closing(T("reallife_civic_office_bye"))) : Gone();
            }
            OfficeState s = office.State;
            option = (option % CampaignPages + CampaignPages) % CampaignPages;
            int next = option + 1;
            Func<DialogEntry> more = () => Campaign(next);
            switch (option)
            {
                case 0:
                {
                    Text.Length = 0;
                    Text.Append(T("reallife_civic_office_race_head"));
                    for (int i = 0; i < office.RaceCount; i++)
                    {
                        Candidate c = office.RaceCandidate(i);
                        if (c != null)
                        {
                            Text.Append('\n').Append(i == office.MyIndex ? "> " : "- ").Append(CivicText.Name(c)).Append(" (").Append(CivicText.Party(c)).Append("): ")
                                .Append(CivicGovernment.Percent(office.RacePoll(i))).Append('%');
                        }
                    }
                    Text.Append('\n').Append(Fill("reallife_civic_office_race_days", "days", Mathf.Max(0, gov.State.ElectionDay - Day).ToString(),
                        "debate", (gov.State.ElectionDay - CivicCatalogue.DebateBefore).ToString()));
                    return CityHallPhone.Page(Text.ToString(), "reallife_civic_button_next_option", more,
                        "reallife_civic_button_back", () => Topic(() => CityHallPhone.Closing(T("reallife_civic_office_bye"))));
                }
                case 1:
                    return FundPage(more);
                case 2:
                    if (s.OfferKind < 0)
                    {
                        return CityHallPhone.Page(T("reallife_civic_office_no_offer"), "reallife_civic_button_next_option", more);
                    }
                    return CityHallPhone.Page(Fill("reallife_civic_office_offer_page", "name", OfferName(s), "amount", Money(s.OfferAmount), "what", T(OfferKey(s.OfferKind)),
                            "limit", Money(office.Limit)),
                        "reallife_civic_office_button_accept", () => CityHallPhone.Page(T(office.Accept(Day)), "reallife_civic_button_next_option", more),
                        "reallife_civic_office_button_decline", () => CityHallPhone.Page(T(office.Decline()), "reallife_civic_button_next_option", more));
                case 3:
                    return Spend(0, more);
                case 4:
                    return Endorsers(0, more);
                case 5:
                    if (s.AttackKind < 0)
                    {
                        return CityHallPhone.Page(T("reallife_civic_office_no_attack"), "reallife_civic_button_next_option", more);
                    }
                    return AttackPage(0, more);
                default:
                    return CityHallPhone.Page(Fill("reallife_civic_office_campaign_platform", "platform", CityHallPhone.Platform(s.Me), "slogan", T(CivicCatalogue.Slogans[s.Me.Slogan]),
                            "party", CivicText.Party(s.Me)),
                        "reallife_civic_button_next_option", () => Campaign(0));
            }
        }

        private static string OfferName(OfficeState s)
        {
            if (s.OfferKind == OfficeRules.OfferCash)
            {
                return T("reallife_civic_office_offer_nobody");
            }
            string person = T(CivicCatalogue.FirstNames[s.OfferFirst]) + " " + T(CivicCatalogue.LastNames[s.OfferLast]);
            return s.OfferKind == OfficeRules.OfferBusiness || s.OfferKind == OfficeRules.OfferStraw
                ? Fill("reallife_civic_office_offer_firm", "name", T(CivicCatalogue.LastNames[s.OfferLast]))
                : person;
        }

        private static string OfferKey(int kind)
        {
            switch (kind)
            {
                case OfficeRules.OfferPerson: return "reallife_civic_office_offer_person";
                case OfficeRules.OfferBusiness: return "reallife_civic_office_offer_business";
                case OfficeRules.OfferOverLimit: return "reallife_civic_office_offer_over";
                case OfficeRules.OfferCash: return "reallife_civic_office_offer_cash";
                default: return "reallife_civic_office_offer_straw";
            }
        }

        private static readonly string[] SpendKeys =
        {
            "reallife_civic_office_spend_posters", "reallife_civic_office_spend_ledger", "reallife_civic_office_spend_flare",
            "reallife_civic_office_spend_rally", "reallife_civic_office_spend_canvassers"
        };

        private static DialogEntry Spend(int item, Func<DialogEntry> more)
        {
            CivicOffice office = Office;
            if (office == null)
            {
                return Gone();
            }
            if (item >= SpendKeys.Length)
            {
                return more();
            }
            int next = item + 1;
            string text = Fill(SpendKeys[item], "price", Money(CivicOffice.Price(item)), "fund", Money(office.State.Fund), "count", OfficeRules.PostersPerRun.ToString(),
                "doors", OfficeRules.CanvasserDoors.ToString(), "from", OfficeRules.RallyFrom.ToString("00") + ":00", "to", OfficeRules.RallyTo.ToString("00") + ":00");
            return CityHallPhone.Page(text, "reallife_civic_office_button_buy", () =>
            {
                string result = office.Buy(item, Day, Hour);
                if (item == CivicOffice.BuyRally && result == "reallife_civic_office_bought_rally")
                {
                    CivicDirector director = CivicDirector.Instance;
                    if (director == null || !director.OwnRally())
                    {
                        office.RallyRefund();
                        result = "reallife_civic_office_rally_no_room";
                    }
                    else
                    {
                        return CityHallPhone.Closing(T(result));
                    }
                }
                return CityHallPhone.Page(Fill(result, "count", office.State.PostersHeld.ToString(), "fund", Money(office.State.Fund)), "reallife_civic_button_next_option", () => Spend(next, more));
            }, "reallife_civic_button_next_option", () => Spend(next, more));
        }

        private static DialogEntry Endorsers(int body, Func<DialogEntry> more)
        {
            CivicOffice office = Office;
            if (office == null)
            {
                return Gone();
            }
            if (body >= OfficeRules.Endorsers)
            {
                return more();
            }
            OfficeState s = office.State;
            int next = body + 1;
            int backs = s.Backs[body];
            Candidate backed = backs >= 0 ? office.RaceCandidate(backs) : null;
            int issue = OfficeRules.EndorserIssue[body];
            string text = Fill(backed == null ? "reallife_civic_office_body_page_open" : "reallife_civic_office_body_page_backs", "body", T(CivicOffice.EndorserKey(body)),
                "issue", issue >= 0 ? T(CivicCatalogue.IssueNames[issue]) : T("reallife_civic_office_body_everything"), "name", backed != null ? CivicText.Name(backed) : string.Empty);
            if (backed != null || (s.Asked & (1 << body)) != 0)
            {
                return CityHallPhone.Page(text, "reallife_civic_office_button_next_body", () => Endorsers(next, more), "reallife_civic_button_next_option", more);
            }
            return CityHallPhone.Page(text, "reallife_civic_office_button_ask", () =>
            {
                string result = office.Ask(body, Day);
                int now = office.State.Backs[body];
                Candidate chose = now >= 0 ? office.RaceCandidate(now) : null;
                return CityHallPhone.Page(Fill(result, "body", T(CivicOffice.EndorserKey(body)), "name", chose != null ? CivicText.Name(chose) : string.Empty),
                    "reallife_civic_office_button_next_body", () => Endorsers(next, more));
            }, "reallife_civic_office_button_next_body", () => Endorsers(next, more));
        }

        private static DialogEntry AttackPage(int page, Func<DialogEntry> more)
        {
            CivicOffice office = Office;
            if (office == null || office.State.AttackKind < 0)
            {
                return more();
            }
            OfficeState s = office.State;
            Candidate by = office.RaceCandidate(s.AttackBy);
            string name = by != null ? CivicText.Name(by) : string.Empty;
            string text = Fill("reallife_civic_office_attack_page", "name", name, "what", T(CivicOffice.AttackKey((AttackKind)s.AttackKind)));
            if (page == 0)
            {
                return CityHallPhone.Page(text, "reallife_civic_office_button_answer_straight", () => Answered(OfficeAnswer.Straight, more),
                    "reallife_civic_office_button_more_answers", () => AttackPage(1, more));
            }
            return CityHallPhone.Page(text, "reallife_civic_office_button_answer_back", () => Answered(OfficeAnswer.TurnBack, more),
                "reallife_civic_office_button_answer_none", () => Answered(OfficeAnswer.Dodge, more));
        }

        private static DialogEntry Answered(OfficeAnswer answer, Func<DialogEntry> more)
        {
            CivicOffice office = Office;
            return office == null ? Gone() : CityHallPhone.Page(T(office.AnswerAttack(answer, Day)), "reallife_civic_button_next_option", more);
        }

        private const int OfficePages = 6;

        private static DialogEntry OfficeMenu(int option)
        {
            CivicOffice office = Office;
            CivicGovernment gov = Gov;
            if (office == null || gov == null || !office.State.InOffice)
            {
                return Office != null ? Topic(() => CityHallPhone.Closing(T("reallife_civic_office_bye"))) : Gone();
            }
            OfficeState s = office.State;
            bool mayor = s.Holds == OfficeRace.Mayor;
            option = (option % OfficePages + OfficePages) % OfficePages;
            if (option == 2 && !mayor)
            {
                option = 3;
            }
            int next = option + 1;
            Func<DialogEntry> more = () => OfficeMenu(next);
            switch (option)
            {
                case 0:
                {
                    Text.Length = 0;
                    Text.Append(Fill("reallife_civic_office_agenda_head", "days", Mathf.Max(0, s.NextMeeting - Day).ToString(), "hour", OfficeRules.MeetingHour.ToString("00") + ":00"));
                    for (int i = 0; i < s.Agenda.Count; i++)
                    {
                        OfficeMotion m = s.Agenda[i];
                        PolicyDef p = CivicCatalogue.At(m.Policy);
                        Text.Append('\n').Append("- ").Append(m.Kind == OfficeMotion.KindBudget ? T("reallife_civic_office_agenda_budget") : p != null ? T(p.Name) : string.Empty);
                    }
                    if (s.Agenda.Count == 0)
                    {
                        Text.Append('\n').Append(T("reallife_civic_office_agenda_empty"));
                    }
                    Text.Append('\n').Append(T(mayor ? "reallife_civic_office_agenda_mayor" : "reallife_civic_office_agenda_council"));
                    return CityHallPhone.Page(Text.ToString(), "reallife_civic_office_button_table", () => TablePage(0), "reallife_civic_button_next_option", more);
                }
                case 1:
                {
                    BudgetWeek week = OfficeRules.Week(gov.Electorate(), gov.State.Levers, s.Budget);
                    string text = Fill("reallife_civic_office_budget_page", "levels", CivicOffice.Levels(s.Budget), "revenue", Money(Prices.At(week.Revenue)),
                        "spending", Money(Prices.At(week.Spending)), "balance", Money(Prices.At(week.Balance)),
                        "reserve", Money(Prices.At((int)Mathf.Clamp(s.Reserve, int.MinValue / 2, int.MaxValue / 2))),
                        "due", Mathf.Max(0, s.BudgetDay - Day).ToString());
                    if (!mayor)
                    {
                        return CityHallPhone.Page(text + "\n" + T("reallife_civic_office_budget_council"), "reallife_civic_button_next_option", more);
                    }
                    Array.Copy(s.Budget, Proposal, Proposal.Length);
                    return CityHallPhone.Page(text, "reallife_civic_office_button_propose", () => BudgetPage(0), "reallife_civic_button_next_option", more);
                }
                case 2:
                    return OrderPage(0, more);
                case 3:
                    return CityHallPhone.Page(T(s.Registered ? "reallife_civic_office_register_done" : "reallife_civic_office_register_page"), "reallife_civic_office_button_register",
                        () => CityHallPhone.Page(T(office.Register()), "reallife_civic_button_next_option", more), "reallife_civic_button_next_option", more);
                case 4:
                    return CityHallPhone.Page(Fill("reallife_civic_office_record_page", "attended", s.Attended.ToString(), "missed", s.Missed.ToString(), "terms", s.Terms.ToString(),
                            "runs", s.Runs.ToString(), "wins", s.Wins.ToString(), "scandals", s.Scandals.ToString(), "approval", Mathf.RoundToInt(office.Approval).ToString(),
                            "platform", CityHallPhone.Platform(mayor ? gov.State.Mayor : s.Held)),
                        "reallife_civic_button_next_option", more);
                default:
                    return CityHallPhone.Page(T(mayor ? "reallife_civic_office_standdown_mayor" : "reallife_civic_office_standdown_council"), "reallife_civic_office_button_stand_down",
                        () => CityHallPhone.Closing(T(office.StandDown(Day))), "reallife_civic_button_next_option", () => OfficeMenu(0));
            }
        }

        // A policy to put to the council: those not in force nor on their way.
        private static DialogEntry TablePage(int from)
        {
            CivicOffice office = Office;
            CivicGovernment gov = Gov;
            if (office == null || gov == null)
            {
                return Gone();
            }
            string why = office.MayTable(out int room);
            if (why != null)
            {
                return CityHallPhone.Page(T(why), "reallife_civic_button_back", () => OfficeMenu(0));
            }
            for (int index = Mathf.Max(0, from); index < CivicCatalogue.All.Length; index++)
            {
                PolicyDef policy = CivicCatalogue.All[index];
                if (gov.Settled(policy.Lever, policy.Dir) || office.OnAgenda(index))
                {
                    continue;
                }
                int next = index + 1;
                bool conflicted = office.Conflicted(index);
                string text = Fill("reallife_civic_office_table_page", "policy", T(policy.Name), "effect", CivicText.Full(policy), "room", room.ToString());
                if (conflicted)
                {
                    text += "\n" + T("reallife_civic_office_table_conflict");
                }
                bool mayor = office.State.Holds == OfficeRace.Mayor;
                return CityHallPhone.Page(text, "reallife_civic_office_button_table_this", () =>
                {
                    if (conflicted && mayor)
                    {
                        return CityHallPhone.Page(T("reallife_civic_office_table_declare_page"), "reallife_civic_office_button_declare_table",
                            () => CityHallPhone.Page(T(office.Table(index, true, Day)), "reallife_civic_button_back", () => OfficeMenu(0)),
                            "reallife_civic_office_button_table_quietly", () => CityHallPhone.Page(T(office.Table(index, false, Day)), "reallife_civic_button_back", () => OfficeMenu(0)));
                    }
                    return CityHallPhone.Page(T(office.Table(index, true, Day)), "reallife_civic_button_back", () => OfficeMenu(0));
                }, "reallife_civic_button_next_policy", () => TablePage(next));
            }
            return ZoningPage(0);
        }

        // After the policies, the new district's zoning: for each class of its lots, each use that could be allowed there, or
        // that the council allowed before and could stop allowing; then round to the policies.
        private static DialogEntry ZoningPage(int from)
        {
            CivicOffice office = Office;
            if (office == null)
            {
                return Gone();
            }
            int matters = ZoningBridges.DistrictLives() ? ZoningBridges.Classes * ZoningBridges.Uses * 2 : 0;
            for (int k = Mathf.Max(0, from); k < matters; k++)
            {
                int lotClass = k / (ZoningBridges.Uses * 2), use = k / 2 % ZoningBridges.Uses;
                bool add = k % 2 == 0;
                if (!office.MayRezone(lotClass, use, add, ZoningBridges.FirstZones(lotClass)))
                {
                    continue;
                }
                int next = k + 1;
                var m = new OfficeMotion { Kind = OfficeMotion.KindZoning, Class = lotClass, Use = use, Add = add };
                bool conflicted = office.Conflicted(m);
                string text = Fill("reallife_civic_office_zoning_page", "matter", CivicOffice.MotionWords(m), "lots", ZoningBridges.Class(lotClass),
                    "zones", ZoningBridges.UsesWords(ZoningBridges.Zones(lotClass)));
                if (conflicted)
                {
                    text += "\n" + T("reallife_civic_office_table_conflict");
                }
                bool mayor = office.State.Holds == OfficeRace.Mayor;
                return CityHallPhone.Page(text, "reallife_civic_office_button_table_this", () =>
                {
                    if (conflicted && mayor)
                    {
                        return CityHallPhone.Page(T("reallife_civic_office_table_declare_page"), "reallife_civic_office_button_declare_table",
                            () => CityHallPhone.Page(T(office.TableZoning(lotClass, use, add, true, Day)), "reallife_civic_button_back", () => OfficeMenu(0)),
                            "reallife_civic_office_button_table_quietly",
                            () => CityHallPhone.Page(T(office.TableZoning(lotClass, use, add, false, Day)), "reallife_civic_button_back", () => OfficeMenu(0)));
                    }
                    return CityHallPhone.Page(T(office.TableZoning(lotClass, use, add, true, Day)), "reallife_civic_button_back", () => OfficeMenu(0));
                }, "reallife_civic_office_button_next_matter", () => ZoningPage(next));
            }
            return CityHallPhone.Page(T("reallife_civic_office_table_none"), "reallife_civic_button_back", () => OfficeMenu(0), "reallife_civic_button_next_policy", () => TablePage(0));
        }

        // A mayor's budget, a department a page: a step up or down; then to the council.
        private static DialogEntry BudgetPage(int department)
        {
            CivicOffice office = Office;
            CivicGovernment gov = Gov;
            if (office == null || gov == null)
            {
                return Gone();
            }
            if (department >= Proposal.Length)
            {
                BudgetWeek week = OfficeRules.Week(gov.Electorate(), gov.State.Levers, Proposal);
                string text = Fill("reallife_civic_office_budget_proposal", "levels", CivicOffice.Levels(Proposal), "revenue", Money(Prices.At(week.Revenue)),
                    "spending", Money(Prices.At(week.Spending)), "balance", Money(Prices.At(week.Balance)));
                return CityHallPhone.Page(text, "reallife_civic_office_button_put_budget",
                    () => CityHallPhone.Page(T(office.ProposeBudget(Proposal, Day)), "reallife_civic_button_back", () => OfficeMenu(1)),
                    "reallife_civic_office_button_budget_again", () => BudgetPage(0));
            }
            int next = department + 1;
            string page = Fill("reallife_civic_office_budget_dept", "dept", T(CivicOffice.DepartmentKeys[department]), "level", (Proposal[department] > 0 ? "+" : string.Empty) + Proposal[department],
                "about", T(CivicOffice.DepartmentAbout[department]));
            // The first button goes a step up, and from the top round to the bottom; the second moves on.
            return CityHallPhone.Page(page, "reallife_civic_office_button_step_up", () =>
            {
                Proposal[department] = Proposal[department] >= OfficeRules.DepartmentMost ? -OfficeRules.DepartmentMost : Proposal[department] + 1;
                return BudgetPage(department);
            }, "reallife_civic_office_button_next_dept", () => BudgetPage(next));
        }

        // A mayor's order: the levers that are theirs to set, a policy a page.
        private static DialogEntry OrderPage(int from, Func<DialogEntry> more)
        {
            CivicOffice office = Office;
            CivicGovernment gov = Gov;
            if (office == null || gov == null)
            {
                return Gone();
            }
            for (int step = 0; step < CivicCatalogue.All.Length; step++)
            {
                int index = from + step;
                if (index >= CivicCatalogue.All.Length)
                {
                    break;
                }
                PolicyDef policy = CivicCatalogue.All[index];
                if (!OfficeRules.ByOrder(policy.Lever) || gov.Settled(policy.Lever, policy.Dir))
                {
                    continue;
                }
                int next = index + 1;
                return CityHallPhone.Page(Fill("reallife_civic_office_order_page", "policy", T(policy.Name), "effect", CivicText.Full(policy), "days", OfficeRules.OrderEvery.ToString()),
                    "reallife_civic_office_button_order", () => CityHallPhone.Page(T(office.Order(index, Day)), "reallife_civic_button_next_option", more),
                    "reallife_civic_button_next_policy", () => OrderPage(next, more));
            }
            return CityHallPhone.Page(T("reallife_civic_office_order_none"), "reallife_civic_button_next_option", more);
        }
    }
}
