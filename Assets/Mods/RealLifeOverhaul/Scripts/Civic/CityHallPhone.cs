using System;
using System.Text;
using BAModAPI;
using Dialogs;
using Entities;
using Localizor;
using UI.Smartphone.Apps.Contacts;

namespace RealLifeOverhaul
{
    /// <summary>
    /// "City Hall" on the phone: a contact whose call is pages of the game's conversation panel, as a mod cannot add an app.
    /// The same panel, face to face, is the ballot and a site's notice board. Pages are built when shown, never on a tick.
    /// </summary>
    public static class CityHallPhone
    {
        public const string ContactId = "reallife-cityhall";
        private const string DialogName = "reallife_cityhall_calldialogtype";
        private const string BallotName = "reallife_ballot_calldialogtype";
        private const string NoticeName = "reallife_sitenotice_calldialogtype";

        internal static readonly StringBuilder Text = new StringBuilder(1024);

        internal static SiteState NoticeSite;

        public static void Setup()
        {
            if (!Settings.PhoneContacts)
            {
                return;
            }
            try
            {
                ContactPictures.Add(ContactId);   // the game's own logo shapes or portrait camera
                Contact contact = Contact.GetContact(ContactId, ContactCategoryName.General, "reallife_contact_cityhall_desc", null, false, true);
                var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogName);
                contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
                CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new CityHallDialog()));
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

        public static void OpenBallot()
        {
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(BallotName);
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new BallotDialog()));
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog);
        }

        public static void OpenNotice(SiteState site)
        {
            NoticeSite = site;
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(NoticeName);
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new SiteNoticeDialog()));
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog);
        }

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

    /// <summary>The ballot at a polling place: one page a candidate, a vote on the button.</summary>
    public sealed class BallotDialog : Dialog
    {
        public BallotDialog()
        {
            npcNameKey = CityHallPhone.ContactId;
            DialogController.current.ShowEntry(Guard.Entry(Ballot(0)));
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
    public sealed class SiteNoticeDialog : Dialog
    {
        public SiteNoticeDialog()
        {
            npcNameKey = CityHallPhone.ContactId;
            CivicDirector director = CivicDirector.Instance;
            SiteState site = CityHallPhone.NoticeSite;
            if (director == null || site == null || director.Works.Find(site.Id) == null)
            {
                DialogController.current.ShowEntry(Guard.Entry(CityHallPhone.Closing(CivicText.T("reallife_civic_site_gone"))));
                return;
            }
            int id = site.Id;
            DialogController.current.ShowEntry(Guard.Entry(CityHallPhone.Page(CityHallDialog.SiteText(director.Works, site, false),
                "reallife_civic_button_site_actions", () => CityHallDialog.SiteAction(id, 0, -1))));
        }
    }

    /// <summary>Addresses as the game writes them.</summary>
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
