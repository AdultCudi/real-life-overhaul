using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Dialogs;
using Entities;
using Helpers;
using Localizor;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// "City Hall" on the phone. The game has no way for a mod to add an app, so City Hall is a
    /// contact whose call is a set of pages (the game's own conversation panel: a text and up to
    /// three buttons). Four topics, gone through with "Next topic" and opened with the other
    /// button:
    ///   The mayor      who governs, approval, the council, every policy in force with what it
    ///                  changes for the player in numbers, what is on its way, the weekly account
    ///   The election   when it is; during a campaign each candidate with slogan, platform and
    ///                  poll, and what the player can do for them (donate, endorse, volunteer);
    ///                  the debate once it has been held; the count by district afterwards
    ///   Construction   the sites nearest the player with phase and days left, complaints,
    ///                  reports, calling in a permit favour; ordering a refit of the player's own shopfront
    ///   Petitions      the one that is open (sign it), or starting one
    /// The same panel, face to face, is the ballot at a polling place and a site's notice board.
    /// Everything is built when a page is shown; nothing here runs on a tick.
    /// </summary>
    public static class CityHallPhone
    {
        public const string ContactId = "reallife-cityhall";
        private const string DialogName = "reallife_cityhall_calldialogtype";
        private const string BallotName = "reallife_ballot_calldialogtype";
        private const string NoticeName = "reallife_sitenotice_calldialogtype";

        internal static readonly StringBuilder Text = new StringBuilder(1024);

        /// <summary>The site whose notice is being read.</summary>
        internal static SiteState NoticeSite;

        public static void Setup()
        {
            if (!Settings.PhoneContacts)
            {
                return;
            }
            try
            {
                ContactPictures.Add(ContactId);   // the game's own logo shapes or portrait camera (Phone/Contacts)
                Contact contact = Contact.GetContact(ContactId, ContactCategoryName.General, "reallife_contact_cityhall_desc", null, false, true);
                var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogName);
                contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
                CallDialogFactory.RegisterDialog(dialogType, () => new CityHallDialog());
                if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
                {
                    contact.SendMessage(new TextMessage("reallife_text_cityhall_welcome"), notify: false);
                }
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                CivicDirector.Logger?.Warn("Civic: the City Hall contact could not be added; elections and sites still run, and the test buttons still work.");
            }
        }

        /// <summary>The ballot, face to face at a polling place.</summary>
        public static void OpenBallot()
        {
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(BallotName);
            CallDialogFactory.RegisterDialog(dialogType, () => new BallotDialog());
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog);
        }

        /// <summary>A site's notice board, read where it stands.</summary>
        public static void OpenNotice(SiteState site)
        {
            NoticeSite = site;
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(NoticeName);
            CallDialogFactory.RegisterDialog(dialogType, () => new SiteNoticeDialog());
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog);
        }

        // ---------------------------------------------------------------- what every page shares

        /// <summary>A page: text made here, a button that does something, a button that goes on, and hang up.</summary>
        internal static DialogEntry Page(string text, string confirmKey, Func<DialogEntry> onConfirm, string secondKey = null, Func<DialogEntry> onSecond = null)
        {
            var entry = new DialogEntry
            {
                messageData = "reallife_civic_page".Localize(new { text }),
                OnCancel = Leave
            };
            if (onConfirm != null)
            {
                entry.ConfirmTextOverride = confirmKey.Localize();
                entry.OnConfirm = onConfirm;
            }
            if (onSecond != null)
            {
                entry.SecondOptionTextOverride = secondKey;
                entry.OnSecondOption = onSecond;
            }
            return entry;
        }

        /// <summary>A last line: on the phone the call ends by itself, in person there is a leave button.</summary>
        internal static DialogEntry Closing(string text)
        {
            bool phone = DialogController.current.dialogType == DialogType.PhoneCall;
            return new DialogEntry
            {
                messageData = "reallife_civic_page".Localize(new { text }),
                OnVisible = phone ? DialogController.current.FinishDialog : (Action)null,
                OnCancel = phone ? (Action)null : DialogController.current.FinishDialog
            };
        }

        private static void Leave()
        {
            DialogController.current.CancelDialog();
        }

        internal static string PolicyLine(PolicyDef policy)
        {
            return "- " + CivicText.T(policy.Name) + ": " + CivicText.Effect(policy);
        }

        internal static string Platform(Candidate candidate)
        {
            var text = new StringBuilder(256);
            for (int i = 0; i < candidate.Planks; i++)
            {
                PolicyDef policy = CivicCatalogue.At(candidate.Platform[i]);
                if (policy != null)
                {
                    text.Append('\n').Append(PolicyLine(policy));
                }
            }
            return text.Length > 0 ? text.ToString() : "\n" + CivicText.T("reallife_civic_platform_none");
        }

    }

    public class CityHallDialog : Dialog
    {
        private const int Topics = 4;

        public CityHallDialog()
        {
            npcNameKey = CityHallPhone.ContactId;
            CivicDirector director = CivicDirector.Instance;
            if (director == null || !director.Running)
            {
                DialogController.current.ShowEntry(CityHallPhone.Closing(CivicText.T("reallife_civic_closed")));
                return;
            }
            DialogController.current.ShowEntry(Topic(0));
        }

        private static CivicGovernment Gov => CivicDirector.Instance != null ? CivicDirector.Instance.Government : null;
        private static CivicWorks Works => CivicDirector.Instance != null ? CivicDirector.Instance.Works : null;
        private static int Day => TimeHelper.CurrentDay;
        private static int Hour => TimeHelper.CurrentHour;

        private static DialogEntry Gone()
        {
            return CityHallPhone.Closing(CivicText.T("reallife_civic_closed"));
        }

        // ---------------------------------------------------------------- the four topics

        private static DialogEntry Topic(int index)
        {
            CivicGovernment gov = Gov;
            CivicWorks works = Works;
            if (gov == null || works == null)
            {
                return Gone();
            }
            index = (index % Topics + Topics) % Topics;
            GovState state = gov.State;
            string text;
            Func<DialogEntry> open;
            string openKey;
            switch (index)
            {
                case 0:
                {
                    int inForce = 0;
                    for (int i = 0; i < state.Levers.Length; i++)
                    {
                        inForce += state.Levers[i] != 0 ? 1 : 0;
                    }
                    text = CivicText.T("reallife_civic_topic_mayor")
                        .Replace("{name}", CivicText.Name(state.Mayor)).Replace("{party}", CivicText.Party(state.Mayor))
                        .Replace("{approval}", Mathf.RoundToInt(state.Approval).ToString()).Replace("{policies}", inForce.ToString())
                        .Replace("{pending}", state.Pending.Count.ToString()).Replace("{council}", CouncilLine(state));
                    open = () => Policies(0);
                    openKey = "reallife_civic_button_policies";
                    break;
                }
                case 1:
                    text = ElectionSummary(gov);
                    open = () => state.Phase == ElectionPhase.Campaign ? CandidatePage(0) : state.Winner >= 0 && state.CandidateCount > 0 ? Results(0) : Topic(2);
                    openKey = state.Phase == ElectionPhase.Campaign ? "reallife_civic_button_candidates" : state.Winner >= 0 ? "reallife_civic_button_results" : "reallife_civic_button_next_topic";
                    break;
                case 2:
                    text = CivicText.T("reallife_civic_topic_works", "count", works.State.Sites.Count.ToString(), "done", works.State.Completed.ToString(), "favours", state.Favours.ToString());
                    open = () => SitePage(0);
                    openKey = "reallife_civic_button_sites";
                    break;
                default:
                    text = state.PetitionPolicy >= 0
                        ? CivicText.T("reallife_civic_topic_petition_open", "policy", CivicText.T(CivicCatalogue.All[state.PetitionPolicy].Name),
                            "signed", state.PetitionSigned.ToString("N0"), "needed", state.PetitionNeeded.ToString("N0"))
                        : CivicText.T("reallife_civic_topic_petition_none", "needed", Mathf.Max(200, Mathf.RoundToInt(gov.Electorate() * CivicCatalogue.PetitionShare)).ToString("N0"),
                            "electorate", gov.Electorate().ToString("N0"));
                    open = () => state.PetitionPolicy >= 0 ? PetitionPage() : StartPetition(0);
                    openKey = state.PetitionPolicy >= 0 ? "reallife_civic_button_petition" : "reallife_civic_button_start_petition";
                    break;
            }
            int next = index + 1;
            return CityHallPhone.Page(text, openKey, open, "reallife_civic_button_next_topic", () => Topic(next));
        }

        private static string CouncilLine(GovState state)
        {
            int own = 0;
            for (int i = 0; i < state.Council.Length; i++)
            {
                own += state.Council[i] == state.Mayor.Party ? 1 : 0;
            }
            return CivicText.T("reallife_civic_council_line", "own", own.ToString(), "seats", state.Council.Length.ToString());
        }

        private static string ElectionSummary(CivicGovernment gov)
        {
            GovState state = gov.State;
            int left = state.ElectionDay - Day;
            if (state.Phase == ElectionPhase.Campaign)
            {
                int lead = gov.Leader();
                District district = gov.PlayerDistrict();
                string where = district != null && district.PollingAddress != null
                    ? CivicText.T("reallife_civic_polling_where", "address", district.PollingAddress, "district", CivicCity.Name(district))
                    : string.Empty;
                string key = left <= 0 ? (state.Voted >= 0 ? "reallife_civic_topic_election_today_voted" : "reallife_civic_topic_election_today") : "reallife_civic_topic_election_campaign";
                return CivicText.T(key).Replace("{days}", Mathf.Max(0, left).ToString()).Replace("{count}", state.CandidateCount.ToString())
                    .Replace("{leader}", CivicText.Name(state.Candidates[lead])).Replace("{share}", CivicGovernment.Percent(state.Polls[lead])).Replace("{where}", where)
                    .Replace("{issue}", CivicText.T(CivicCatalogue.IssueNames[gov.TopIssue()]));
            }
            if (state.Phase == ElectionPhase.Counted && state.Winner >= 0)
            {
                return CivicText.T("reallife_civic_topic_election_counted", "name", CivicText.Name(state.Candidates[state.Winner]), "turnout", state.Turnout.ToString("N0"));
            }
            return CivicText.T("reallife_civic_topic_election_quiet", "days", Mathf.Max(0, left).ToString(),
                "campaign", Mathf.Max(0, left - CivicCatalogue.CampaignDays).ToString(), "weeks", CivicCatalogue.TermWeeks.ToString());
        }

        // ---------------------------------------------------------------- the mayor: account, policies in force, policies on their way

        private static DialogEntry Policies(int index)
        {
            CivicGovernment gov = Gov;
            if (gov == null)
            {
                return Gone();
            }
            GovState state = gov.State;
            // Page 0 is the weekly account; then one page for each lever that is not where the city came; then one for what is on its way.
            if (index == 0)
            {
                CivicEffects.Account account = CivicEffects.Reckon(state);
                StringBuilder text = CityHallPhone.Text;
                text.Length = 0;
                text.Append(CivicText.T("reallife_civic_account_head", "days", Mathf.Max(0, state.SettleDay - Day).ToString()));
                Line(text, "reallife_civic_account_levy", account.Levy);
                Line(text, "reallife_civic_account_tax", account.Tax);
                Line(text, "reallife_civic_account_trade", account.Trade);
                Line(text, account.Grants <= 0f ? "reallife_civic_account_grants" : "reallife_civic_account_licence", account.Grants);
                Line(text, "reallife_civic_account_rent", account.Rent + account.Landlord);
                Line(text, "reallife_civic_account_wages", account.Wages);
                float total = Mathf.Round(account.Total);
                text.Append('\n').Append(Mathf.Abs(total) < 1f ? CivicText.T("reallife_civic_account_nothing")
                    : CivicText.T(total > 0f ? "reallife_civic_account_total_pay" : "reallife_civic_account_total_get", "amount", CivicText.Money(Mathf.Abs(total))));
                return CityHallPhone.Page(text.ToString(), "reallife_civic_button_next", () => Policies(1), "reallife_civic_button_back", () => Topic(0));
            }
            int seen = 0;
            for (int lever = 0; lever < CivicCatalogue.Levers; lever++)
            {
                PolicyDef policy = CivicCatalogue.Find((Lever)lever, state.Levers[lever]);
                if (policy == null || ++seen != index)
                {
                    continue;
                }
                bool promised = false;
                for (int i = 0; i < state.Mayor.Planks; i++)
                {
                    promised |= state.Mayor.Platform[i] == CivicCatalogue.IndexOf(policy);
                }
                string text = CivicText.T(promised ? "reallife_civic_policy_page_promised" : "reallife_civic_policy_page", "policy", CivicText.T(policy.Name), "effect", CivicText.Effect(policy));
                int next = index + 1;
                return CityHallPhone.Page(text, "reallife_civic_button_next", () => Policies(next), "reallife_civic_button_back", () => Topic(0));
            }
            if (index == seen + 1)
            {
                StringBuilder text = CityHallPhone.Text;
                text.Length = 0;
                if (state.Pending.Count == 0)
                {
                    text.Append(CivicText.T(seen == 0 ? "reallife_civic_policies_none" : "reallife_civic_pending_none"));
                }
                else
                {
                    text.Append(CivicText.T("reallife_civic_pending_head"));
                    for (int i = 0; i < state.Pending.Count; i++)
                    {
                        PendingChange change = state.Pending[i];
                        PolicyDef policy = CivicCatalogue.Find((Lever)change.Lever, change.Position);
                        PolicyDef old = CivicCatalogue.Find((Lever)change.Lever, state.Levers[change.Lever]);
                        text.Append('\n').Append(policy != null
                            ? CivicText.T("reallife_civic_pending_line", "days", Mathf.Max(0, change.Day - Day).ToString(), "policy", CivicText.T(policy.Name), "effect", CivicText.Effect(policy))
                            : CivicText.T("reallife_civic_pending_ends", "days", Mathf.Max(0, change.Day - Day).ToString(), "policy", old != null ? CivicText.T(old.Name) : string.Empty));
                    }
                }
                return CityHallPhone.Page(text.ToString(), "reallife_civic_button_back", () => Topic(0), "reallife_civic_button_next_topic", () => Topic(1));
            }
            return Topic(0);
        }

        private static void Line(StringBuilder text, string nameKey, float amount)
        {
            amount = Mathf.Round(amount);
            if (Mathf.Abs(amount) < 1f)
            {
                return;
            }
            text.Append('\n').Append("- ").Append(CivicText.T(nameKey)).Append(": ")
                .Append(CivicText.T(amount > 0f ? "reallife_civic_you_pay" : "reallife_civic_you_get", "amount", CivicText.Money(Mathf.Abs(amount))));
        }

        // ---------------------------------------------------------------- the election: candidates, what to do for them, the debate, the count

        private static DialogEntry CandidatePage(int index)
        {
            CivicGovernment gov = Gov;
            if (gov == null || !gov.Campaigning)
            {
                return Topic(1);
            }
            GovState state = gov.State;
            if (index >= state.CandidateCount)
            {
                return state.DebateHeld ? Debate(0) : Topic(1);
            }
            Candidate candidate = state.Candidates[index];
            string text = CivicText.T(candidate.Incumbent ? "reallife_civic_candidate_incumbent" : "reallife_civic_candidate")
                .Replace("{name}", CivicText.Name(candidate)).Replace("{party}", CivicText.Party(candidate))
                .Replace("{slogan}", CivicText.T(CivicCatalogue.Slogans[candidate.Slogan])).Replace("{issue}", CivicText.T(CivicCatalogue.IssueNames[candidate.Lead]))
                .Replace("{platform}", CityHallPhone.Platform(candidate)).Replace("{share}", CivicGovernment.Percent(state.Polls[index]))
                .Replace("{given}", CivicText.Money(state.Donated[index]));
            int next = index + 1;
            return CityHallPhone.Page(text, "reallife_civic_button_support", () => Support(index, 0), "reallife_civic_button_next_candidate", () => CandidatePage(next));
        }

        // Three things the player can do for a candidate, one page each.
        private static DialogEntry Support(int candidate, int option)
        {
            CivicGovernment gov = Gov;
            if (gov == null || !gov.Campaigning || candidate >= gov.State.CandidateCount)
            {
                return Topic(1);
            }
            GovState state = gov.State;
            string name = CivicText.Name(state.Candidates[candidate]);
            int next = option + 1;
            switch (option)
            {
                case 0:
                    return CityHallPhone.Page(CivicText.T("reallife_civic_donate_offer").Replace("{name}", name).Replace("{amount}", CivicText.Money(CivicCatalogue.DonationStepNow))
                            .Replace("{cap}", CivicText.Money(CivicCatalogue.DonationCapNow)).Replace("{given}", CivicText.Money(state.Donated[candidate]))
                            .Replace("{per}", CivicText.Money(CivicCatalogue.FavourPerNow)),
                        "reallife_civic_button_donate", () =>
                        {
                            string result = Gov != null ? Gov.Donate(candidate) : "reallife_civic_closed";
                            return result == "reallife_civic_donate_broke" ? null
                                : CityHallPhone.Page(CivicText.T(result, "name", name, "amount", CivicText.Money(CivicCatalogue.DonationStepNow)), "reallife_civic_button_back", () => CandidatePage(candidate));
                        },
                        "reallife_civic_button_next_option", () => Support(candidate, next));
                case 1:
                    return CityHallPhone.Page(CivicText.T("reallife_civic_endorse_offer").Replace("{name}", name)
                            .Replace("{points}", (CivicGovernment.EndorsementWeight() * 35f).ToString("F1")).Replace("{businesses}", CivicCity.PlayerBusinesses().ToString()),
                        "reallife_civic_button_endorse", () =>
                        {
                            string result = Gov != null ? Gov.Endorse(candidate) : "reallife_civic_closed";
                            string who = Gov != null && Gov.State.Endorsed >= 0 ? CivicText.Name(Gov.State.Candidates[Gov.State.Endorsed]) : name;
                            return CityHallPhone.Page(CivicText.T(result, "name", who), "reallife_civic_button_back", () => CandidatePage(candidate));
                        },
                        "reallife_civic_button_next_option", () => Support(candidate, next));
                default:
                    return CityHallPhone.Page(CivicText.T("reallife_civic_volunteer_offer", "name", name),
                        "reallife_civic_button_volunteer", () =>
                        {
                            CivicDirector director = CivicDirector.Instance;
                            string refused = director != null ? director.Volunteer.Begin(candidate, Hour) : "reallife_civic_closed";
                            return CityHallPhone.Closing(CivicText.T(refused ?? "reallife_civic_volunteer_go", "name", name));
                        },
                        "reallife_civic_button_back", () => CandidatePage(candidate));
            }
        }

        // The debate: the moderator asks about what the city is most worried about, then about the bill; each candidate answers from their platform.
        private static DialogEntry Debate(int index)
        {
            CivicGovernment gov = Gov;
            if (gov == null || gov.State.CandidateCount == 0)
            {
                return Topic(1);
            }
            GovState state = gov.State;
            if (index >= state.CandidateCount)
            {
                return Topic(1);
            }
            int issue = gov.TopIssue();
            Candidate candidate = state.Candidates[index];
            PolicyDef onIssue = null, other = null;
            for (int i = 0; i < candidate.Planks; i++)
            {
                PolicyDef policy = CivicCatalogue.At(candidate.Platform[i]);
                if (policy == null)
                {
                    continue;
                }
                if (onIssue == null && (int)policy.Issue == issue)
                {
                    onIssue = policy;
                }
                else if (other == null)
                {
                    other = policy;
                }
            }
            StringBuilder text = CityHallPhone.Text;
            text.Length = 0;
            text.Append(CivicText.T("reallife_civic_debate_question", "issue", CivicText.T(CivicCatalogue.IssueNames[issue])));
            text.Append('\n').Append(CivicText.Name(candidate)).Append(" (").Append(CivicText.Party(candidate)).Append("): ");
            if (onIssue != null)
            {
                text.Append(CivicText.T(onIssue.Say));
            }
            else
            {
                text.Append(CivicText.T("reallife_civic_debate_pivot", "issue", CivicText.T(CivicCatalogue.IssueNames[candidate.Lead])));
            }
            if (other != null)
            {
                text.Append('\n').Append(CivicText.T("reallife_civic_debate_second")).Append(' ').Append(CivicText.T(other.Say));
            }
            int next = index + 1;
            return CityHallPhone.Page(text.ToString(), "reallife_civic_button_next_candidate", () => Debate(next), "reallife_civic_button_back", () => Topic(1));
        }

        // The count: the city first, then a page for each district.
        private static DialogEntry Results(int page)
        {
            CivicGovernment gov = Gov;
            CivicDirector director = CivicDirector.Instance;
            if (gov == null || director == null || gov.State.Winner < 0 || gov.State.CandidateCount == 0)
            {
                return Topic(1);
            }
            GovState state = gov.State;
            List<District> districts = director.City.Districts;
            if (page > districts.Count)
            {
                return Topic(1);
            }
            StringBuilder text = CityHallPhone.Text;
            text.Length = 0;
            if (page == 0)
            {
                text.Append(CivicText.T("reallife_civic_results_city", "turnout", state.Turnout.ToString("N0"), "electorate", gov.Electorate().ToString("N0")));
            }
            else
            {
                text.Append(CivicText.T("reallife_civic_results_district", "district", CivicCity.Name(districts[page - 1]), "electorate", districts[page - 1].Electorate.ToString("N0")));
            }
            for (int c = 0; c < state.CandidateCount; c++)
            {
                int votes = page == 0 ? gov.CityVotes(c) : gov.DistrictVotes(page - 1, c);
                text.Append('\n').Append(c == state.Winner ? "> " : "- ").Append(CivicText.Name(state.Candidates[c])).Append(": ").Append(votes.ToString("N0"));
            }
            if (page == 0 && state.Voted >= 0)
            {
                text.Append('\n').Append(CivicText.T("reallife_civic_results_yours", "name", CivicText.Name(state.Candidates[Mathf.Min(state.Voted, state.CandidateCount - 1)])));
            }
            else if (page == 0)
            {
                text.Append('\n').Append(CivicText.T("reallife_civic_results_no_vote"));
            }
            int next = page + 1;
            return CityHallPhone.Page(text.ToString(), "reallife_civic_button_next_district", () => Results(next), "reallife_civic_button_back", () => Topic(1));
        }

        // ---------------------------------------------------------------- construction

        private static readonly List<SiteState> Sorted = new List<SiteState>(8);

        private static List<SiteState> NearestFirst()
        {
            Sorted.Clear();
            CivicWorks works = Works;
            if (works == null)
            {
                return Sorted;
            }
            Vector3 player = PlayerHelper.PlayerController != null ? PlayerHelper.GetCityPosition() : Vector3.zero;
            for (int i = 0; i < works.State.Sites.Count; i++)
            {
                SiteState site = works.State.Sites[i];
                float sqr = CivicCity.Flat(new Vector3(site.X, site.Y, site.Z) - player);
                int at = 0;
                while (at < Sorted.Count && CivicCity.Flat(new Vector3(Sorted[at].X, Sorted[at].Y, Sorted[at].Z) - player) <= sqr)
                {
                    at++;
                }
                Sorted.Insert(at, site);
            }
            return Sorted;
        }

        internal static string SiteText(CivicWorks works, SiteState site, bool withDistance)
        {
            Vector3 player = PlayerHelper.PlayerController != null ? PlayerHelper.GetCityPosition() : Vector3.zero;
            string text = CivicText.T("reallife_civic_site_page")
                .Replace("{project}", CivicWorks.ProjectName(site)).Replace("{address}", CivicCity.AddressText(site.Street, site.Number))
                .Replace("{contractor}", CivicWorks.ContractorName(site)).Replace("{phase}", (site.Phase + 1).ToString()).Replace("{phases}", CivicCatalogue.PhasesPerKind.ToString())
                .Replace("{doing}", CivicWorks.PhaseName(site)).Replace("{days}", works.DaysLeft(site, Day, Hour).ToString())
                .Replace("{opens}", CivicCatalogue.SiteOpens(works.Permits).ToString()).Replace("{shuts}", CivicCatalogue.SiteShuts(works.Permits).ToString());
            if (withDistance)
            {
                text += "\n" + CivicText.T("reallife_civic_site_distance", "metres", Mathf.RoundToInt(Mathf.Sqrt(CivicCity.Flat(new Vector3(site.X, site.Y, site.Z) - player))).ToString());
            }
            if (site.RainHours > 0)
            {
                text += "\n" + CivicText.T("reallife_civic_site_rain", "hours", site.RainHours.ToString());
            }
            if (site.HaltUntil > Day)
            {
                text += "\n" + CivicText.T("reallife_civic_site_halted");
            }
            else if (site.QuietUntil >= Day)
            {
                text += "\n" + CivicText.T("reallife_civic_site_quiet");
            }
            if (site.NearMine)
            {
                text += "\n" + CivicText.T("reallife_civic_site_next_door");
            }
            if (site.Mine)
            {
                text += "\n" + CivicText.T("reallife_civic_site_yours");
            }
            return text;
        }

        private static DialogEntry SitePage(int index)
        {
            CivicWorks works = Works;
            if (works == null)
            {
                return Gone();
            }
            List<SiteState> sites = NearestFirst();
            if (index >= sites.Count)
            {
                return RefitOffer(0);
            }
            SiteState site = sites[index];
            int id = site.Id, next = index + 1;
            return CityHallPhone.Page(SiteText(works, site, true), "reallife_civic_button_site_actions", () => SiteAction(id, 0, index),
                "reallife_civic_button_next_site", () => SitePage(next));
        }

        // What can be done about a site: a noise complaint, a report that it is unsafe, a permit favour, a day's labouring (if Jobs offers it).
        internal static DialogEntry SiteAction(int siteId, int option, int backTo)
        {
            CivicWorks works = Works;
            CivicGovernment gov = Gov;
            SiteState site = works != null ? works.Find(siteId) : null;
            if (site == null || gov == null)
            {
                return CityHallPhone.Closing(CivicText.T("reallife_civic_site_gone"));
            }
            Func<DialogEntry> back = () => backTo >= 0 ? SitePage(backTo) : CityHallPhone.Closing(CivicText.T("reallife_civic_notice_bye"));
            int next = option + 1;
            switch (option)
            {
                case 0:
                    return CityHallPhone.Page(CivicText.T(works.Permits > 0 ? "reallife_civic_complain_offer_fast" : works.Permits < 0 ? "reallife_civic_complain_offer_strict" : "reallife_civic_complain_offer"),
                        "reallife_civic_button_complain", () => CityHallPhone.Page(CivicText.T(works.Complain(site, Day)), "reallife_civic_button_back", back),
                        "reallife_civic_button_next_option", () => SiteAction(siteId, next, backTo));
                case 1:
                    return CityHallPhone.Page(CivicText.T("reallife_civic_unsafe_offer"),
                        "reallife_civic_button_report", () =>
                        {
                            if (site.Reported)
                            {
                                return CityHallPhone.Page(CivicText.T("reallife_civic_unsafe_already"), "reallife_civic_button_back", back);
                            }
                            bool was = works.ReportUnsafe(site, Day);
                            return CityHallPhone.Page(CivicText.T(was ? "reallife_civic_unsafe_right" : "reallife_civic_unsafe_wrong"), "reallife_civic_button_back", back);
                        },
                        "reallife_civic_button_next_option", () => SiteAction(siteId, next, backTo));
                case 2:
                    if (gov.State.Favours <= 0 || site.Favoured)
                    {
                        return SiteAction(siteId, next, backTo);
                    }
                    return CityHallPhone.Page(CivicText.T("reallife_civic_favour_offer", "favours", gov.State.Favours.ToString(), "odds", Mathf.RoundToInt(CivicCatalogue.PayToPlayOdds * 100f).ToString()),
                        "reallife_civic_button_favour", () =>
                        {
                            site.Favoured = true;
                            bool story = gov.SpendFavour(Day);
                            return CityHallPhone.Page(CivicText.T(story ? "reallife_civic_favour_story" : "reallife_civic_favour_done"), "reallife_civic_button_back", back);
                        },
                        "reallife_civic_button_next_option", () => SiteAction(siteId, next, backTo));
                case 3:
                    if (CivicBridges.Labouring == null || !works.Working(site, Day, Hour))
                    {
                        return SiteAction(siteId, next, backTo);
                    }
                    return CityHallPhone.Page(CivicText.T("reallife_civic_labour_offer"),
                        "reallife_civic_button_labour", () =>
                        {
                            bool started = false;
                            try
                            {
                                started = CivicBridges.Labouring(new Vector3(site.X, site.Y, site.Z));
                            }
                            catch (Exception e)
                            {
                                CivicDirector.Logger?.Error(e);
                            }
                            return CityHallPhone.Closing(CivicText.T(started ? "reallife_civic_labour_go" : "reallife_civic_labour_no"));
                        },
                        "reallife_civic_button_back", back);
                default:
                    return back();
            }
        }

        // The player's own shopfront: three bids, cheapest and slowest first.
        private static DialogEntry RefitOffer(int bid)
        {
            CivicWorks works = Works;
            CivicDirector director = CivicDirector.Instance;
            if (works == null || director == null)
            {
                return Gone();
            }
            BuildingRegistration shop = director.RefitCandidate(out string reason);
            if (shop == null)
            {
                return CityHallPhone.Page(CivicText.T(reason), "reallife_civic_button_back", () => Topic(2), "reallife_civic_button_next_topic", () => Topic(3));
            }
            if (bid >= CivicCatalogue.RefitPrice.Length)
            {
                return Topic(2);
            }
            string address = shop.Address.ToFormattedStringSafe();
            int price = CivicCatalogue.RefitPriceNow(bid), hours = CivicCatalogue.RefitHours[bid], next = bid + 1;
            string text = CivicText.T("reallife_civic_refit_bid").Replace("{address}", address).Replace("{contractor}", CivicText.T(CivicCatalogue.Contractors[(bid * 3 + 1) % CivicCatalogue.Contractors.Length]))
                .Replace("{amount}", CivicText.Money(price)).Replace("{days}", Mathf.CeilToInt(hours / 10f).ToString()).Replace("{bid}", (bid + 1).ToString());
            return CityHallPhone.Page(text, "reallife_civic_button_accept_bid", () =>
                {
                    string result = director.OrderRefit(shop, bid);
                    return CityHallPhone.Closing(CivicText.T(result, "address", address, "amount", CivicText.Money(price)));
                },
                "reallife_civic_button_next_bid", () => RefitOffer(next));
        }

        // ---------------------------------------------------------------- petitions

        private static DialogEntry PetitionPage()
        {
            CivicGovernment gov = Gov;
            if (gov == null || gov.State.PetitionPolicy < 0)
            {
                return Topic(3);
            }
            GovState state = gov.State;
            PolicyDef policy = CivicCatalogue.All[state.PetitionPolicy];
            string text = CivicText.T(state.PetitionMine ? "reallife_civic_petition_page_mine" : "reallife_civic_petition_page")
                .Replace("{policy}", CivicText.T(policy.Name)).Replace("{effect}", CivicText.Effect(policy))
                .Replace("{signed}", state.PetitionSigned.ToString("N0")).Replace("{needed}", state.PetitionNeeded.ToString("N0"))
                .Replace("{days}", Mathf.Max(0, state.PetitionEnds - Day).ToString());
            if (state.PetitionISigned)
            {
                return CityHallPhone.Page(text + "\n" + CivicText.T("reallife_civic_petition_signed_already"), "reallife_civic_button_back", () => Topic(3), "reallife_civic_button_next_topic", () => Topic(0));
            }
            return CityHallPhone.Page(text, "reallife_civic_button_sign", () =>
                {
                    bool signed = Gov != null && Gov.SignPetition();
                    return CityHallPhone.Page(CivicText.T(signed ? "reallife_civic_petition_signed" : "reallife_civic_petition_signed_already", "count", (1 + 5 * Mathf.Min(10, CivicCity.PlayerBusinesses())).ToString()),
                        "reallife_civic_button_back", () => Topic(3));
                },
                "reallife_civic_button_back", () => Topic(3));
        }

        // Starting one: the policies that are not in force, one page each.
        private static DialogEntry StartPetition(int from)
        {
            CivicGovernment gov = Gov;
            if (gov == null)
            {
                return Gone();
            }
            GovState state = gov.State;
            for (int step = 0; step < CivicCatalogue.All.Length; step++)
            {
                int index = (from + step) % CivicCatalogue.All.Length;
                PolicyDef policy = CivicCatalogue.All[index];
                if (state.Levers[(int)policy.Lever] == policy.Dir)
                {
                    continue;
                }
                int next = index + 1;
                return CityHallPhone.Page(CivicText.T("reallife_civic_petition_propose", "policy", CivicText.T(policy.Name), "effect", CivicText.Effect(policy)),
                    "reallife_civic_button_start_this", () =>
                    {
                        string result = Gov != null ? Gov.StartPetition(index, Day) : "reallife_civic_closed";
                        return CityHallPhone.Closing(CivicText.T(result, "policy", CivicText.T(policy.Name), "needed", Gov != null ? Gov.State.PetitionNeeded.ToString("N0") : string.Empty));
                    },
                    "reallife_civic_button_next_policy", () => StartPetition(next));
            }
            return Topic(3);
        }
    }

    /// <summary>The ballot at a polling place: one page a candidate, a vote on the button.</summary>
    public class BallotDialog : Dialog
    {
        public BallotDialog()
        {
            npcNameKey = CityHallPhone.ContactId;
            DialogController.current.ShowEntry(Ballot(0));
        }

        private static DialogEntry Ballot(int index)
        {
            CivicGovernment gov = CivicDirector.Instance != null ? CivicDirector.Instance.Government : null;
            if (gov == null)
            {
                return CityHallPhone.Closing(CivicText.T("reallife_civic_closed"));
            }
            GovState state = gov.State;
            int day = TimeHelper.CurrentDay, hour = TimeHelper.CurrentHour;
            if (!gov.PollsOpen(day, hour))
            {
                return CityHallPhone.Closing(CivicText.T("reallife_civic_vote_closed"));
            }
            if (state.Voted >= 0)
            {
                return CityHallPhone.Closing(CivicText.T("reallife_civic_vote_already"));
            }
            index = (index % state.CandidateCount + state.CandidateCount) % state.CandidateCount;
            Candidate candidate = state.Candidates[index];
            string text = CivicText.T("reallife_civic_ballot").Replace("{number}", (index + 1).ToString()).Replace("{count}", state.CandidateCount.ToString())
                .Replace("{name}", CivicText.Name(candidate)).Replace("{party}", CivicText.Party(candidate))
                .Replace("{slogan}", CivicText.T(CivicCatalogue.Slogans[candidate.Slogan])).Replace("{platform}", CityHallPhone.Platform(candidate));
            int next = index + 1;
            return CityHallPhone.Page(text, "reallife_civic_button_vote", () =>
                {
                    CivicGovernment now = CivicDirector.Instance != null ? CivicDirector.Instance.Government : null;
                    string result = now != null ? now.Vote(index, TimeHelper.CurrentDay, TimeHelper.CurrentHour) : "reallife_civic_closed";
                    return CityHallPhone.Closing(CivicText.T(result, "name", CivicText.Name(candidate), "hour", CivicCatalogue.ResultHour.ToString()));
                },
                "reallife_civic_button_next_candidate", () => Ballot(next));
        }
    }

    /// <summary>A site's notice board: what, who, how far along, when it ends; and the inspector's number.</summary>
    public class SiteNoticeDialog : Dialog
    {
        public SiteNoticeDialog()
        {
            npcNameKey = CityHallPhone.ContactId;
            CivicDirector director = CivicDirector.Instance;
            SiteState site = CityHallPhone.NoticeSite;
            if (director == null || site == null || director.Works.Find(site.Id) == null)
            {
                DialogController.current.ShowEntry(CityHallPhone.Closing(CivicText.T("reallife_civic_site_gone")));
                return;
            }
            int id = site.Id;
            DialogController.current.ShowEntry(CityHallPhone.Page(CityHallDialog.SiteText(director.Works, site, false),
                "reallife_civic_button_site_actions", () => CityHallDialog.SiteAction(id, 0, -1)));
        }
    }

    internal static class CivicAddress
    {
        /// <summary>An address as the game writes it; never throws.</summary>
        public static string ToFormattedStringSafe(this Address address)
        {
            try
            {
                return Streets.AddressHelper.ToFormattedString(address);
            }
            catch (Exception)
            {
                return address != null ? address.streetNumber + " " + address.streetName : string.Empty;
            }
        }
    }
}
